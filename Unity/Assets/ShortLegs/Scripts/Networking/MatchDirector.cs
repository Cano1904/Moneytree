using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ShortLegs.Cases;
using ShortLegs.Core;
using ShortLegs.Gameplay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShortLegs.Networking
{
    /// <summary>
    /// Authoritative match flow (GDD §3, §8.1): role assignment, the phase timeline, clues, statements,
    /// meetings, votes and win conditions. All truth lives here and never leaves the server.
    /// </summary>
    public sealed class MatchDirector : NetworkBehaviour
    {
        public static MatchDirector Instance { get; private set; }

        [SerializeField] private CaseDefinition caseDefinition;
        [SerializeField] private List<ClueSpot> clueSpots = new List<ClueSpot>();
        [SerializeField] private Transform[] meetingSeats = Array.Empty<Transform>();
        [SerializeField] private float interactRange = 2.5f;

        [Header("Timeline (seconds)")]
        [SerializeField] private float roleRevealDuration = 5f;
        [SerializeField] private float briefingDuration = 5f;
        [SerializeField] private float openingAlibiDuration = 30f;
        [SerializeField] private float meetingDiscussionDuration = 60f;
        [SerializeField] private float finalMeetingDiscussionDuration = 30f;
        [SerializeField] private float voteDuration = 30f;
        [SerializeField] private float finalMeetingLeadTime = 120f;
        [Tooltip("First automatic meeting at this fraction of the match (5:00 of 12:00).")]
        [SerializeField, Range(0.1f, 0.9f)] private float firstMeetingFraction = 5f / 12f;
        [SerializeField] private float lightsOutDuration = 20f;
        [SerializeField] private int maxStatementsPerPhase = 3;

        // ── Replicated, public state ──
        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        public readonly NetworkVariable<double> PhaseEndsAt = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> MatchEndsAt = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> LightsOutUntil = new NetworkVariable<double>(0);
        public readonly NetworkVariable<LobbySettings> Settings = new NetworkVariable<LobbySettings>(LobbySettings.Default);
        public readonly NetworkVariable<int> TrueCluesFound = new NetworkVariable<int>(0);
        public readonly NetworkVariable<MatchOutcome> Outcome = new NetworkVariable<MatchOutcome>(MatchOutcome.None);
        public readonly NetworkVariable<int> RevealedLiarSlot = new NetworkVariable<int>(-1);

        // ── Client-side events for UI / notebook ──
        public event Action<int, int, StatementPacket> StatementLogged;              // index, speaker slot, statement
        public event Action<string, string, string, int> ClueDiscovered;             // id, name, description, finder slot
        public event Action<VerdictKind, int> VerdictAnnounced;                      // kind, target slot
        public event Action<int, bool> EvidencePresented;                            // statement index, exposed?
        public event Action<int, int, string, bool> LieReplayEntry;                  // speaker, statement index, clue, framed
        public event Action<MatchPhase> PhaseChanged;

        // ── Server-only state ──
        private readonly Dictionary<int, ShortLegsPlayer> _players = new Dictionary<int, ShortLegsPlayer>();
        private readonly Dictionary<int, int> _votes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _meetingsLeft = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _statementsThisPhase = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _spotToClue = new Dictionary<int, string>();
        private DeceptionEngine _engine;
        private int _liarSlot = -1;
        private int _seed;
        private int _plantsUsed;
        private bool _sabotageUsed;
        private bool _firstMeetingDone;
        private bool _finalMeetingDone;
        private bool _currentMeetingIsFinal;
        private double _firstMeetingAt;

        private double Now => NetworkManager.ServerTime.Time;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Phase.OnValueChanged += (_, p) => PhaseChanged?.Invoke(p);
            if (!IsServer) return;
            // Players that spawned before the director.
            foreach (var p in ShortLegsPlayer.All) ServerRegisterPlayer(p);
        }

        // ═════════════════════════════ PLAYERS ═════════════════════════════

        public void ServerRegisterPlayer(ShortLegsPlayer player)
        {
            if (!IsServer || _players.ContainsValue(player)) return;
            if (Phase.Value != MatchPhase.Lobby) { player.ServerSetGhost(true); return; } // spectators mid-match
            for (int slot = 0; slot < LobbySettings.MaxPlayers; slot++)
            {
                if (_players.ContainsKey(slot)) continue;
                _players[slot] = player;
                player.Slot.Value = slot;
                return;
            }
            player.ServerSetGhost(true);
        }

        public void ServerUnregisterPlayer(ShortLegsPlayer player)
        {
            if (!IsServer) return;
            int slot = player.Slot.Value;
            if (!_players.TryGetValue(slot, out var p) || p != player) return;
            _players.Remove(slot);

            if (!IsMatchRunning) return;
            if (slot == _liarSlot) End(MatchOutcome.InvestigatorsWinLiarAbandoned);
            else CheckInvestigatorWipe();
        }

        public PlayerRole ServerGetRole(int slot) =>
            slot == _liarSlot ? PlayerRole.Liar : _players.ContainsKey(slot) ? PlayerRole.Investigator : PlayerRole.Unknown;

        private bool IsMatchRunning => Phase.Value != MatchPhase.Lobby && Phase.Value != MatchPhase.Reveal;

        private bool IsAlive(int slot) => _players.TryGetValue(slot, out var p) && !p.IsGhost.Value;

        private int AliveInvestigators()
        {
            int n = 0;
            foreach (var kv in _players) if (kv.Key != _liarSlot && !kv.Value.IsGhost.Value) n++;
            return n;
        }

        // ═════════════════════════════ MATCH START ═════════════════════════════

        /// <summary>Host only. Locks settings, draws the secret Liar and starts the timeline.</summary>
        public bool ServerStartMatch(LobbySettings settings)
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return false;
            if (_players.Count < LobbySettings.MinPlayers)
            {
                Debug.LogWarning($"[ShortLegs] Need {LobbySettings.MinPlayers} players, have {_players.Count}.");
                return false;
            }

            Settings.Value = settings.Clamped();

            // CSPRNG seed, logged for dispute replay; roles are derived from it deterministically.
            _seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            var rng = new System.Random(_seed);
            var slots = new List<int>(_players.Keys);
            slots.Sort();
            _liarSlot = slots[rng.Next(slots.Count)];
            Debug.Log($"[ShortLegs] Match seed {_seed}");

            _engine = new DeceptionEngine(caseDefinition.BuildClues(_liarSlot), autoResolveOnDiscovery: true);
            _spotToClue.Clear();
            foreach (var t in caseDefinition.Clues) _spotToClue[t.SpotIndex] = t.Id;
            for (int i = 0; i < clueSpots.Count; i++)
                clueSpots[i].ServerSetup(i, _spotToClue.ContainsKey(i));

            _meetingsLeft.Clear();
            _votes.Clear();
            foreach (var kv in _players)
            {
                _meetingsLeft[kv.Key] = Settings.Value.MeetingsPerPlayer;
                kv.Value.ServerSetGhost(false);
                // SendTo.Owner: nobody else ever receives who the Liar is.
                kv.Value.RoleAssignedRpc(kv.Key == _liarSlot ? PlayerRole.Liar : PlayerRole.Investigator);
            }

            _plantsUsed = 0;
            _sabotageUsed = false;
            _firstMeetingDone = _finalMeetingDone = false;
            TrueCluesFound.Value = 0;
            Outcome.Value = MatchOutcome.None;
            RevealedLiarSlot.Value = -1;

            EnterPhase(MatchPhase.RoleReveal, roleRevealDuration);
            return true;
        }

        // ═════════════════════════════ TIMELINE ═════════════════════════════

        private void Update()
        {
            if (!IsServer || !IsSpawned || !IsMatchRunning) return;
            double now = Now;

            switch (Phase.Value)
            {
                case MatchPhase.RoleReveal when now >= PhaseEndsAt.Value:
                    double length = Settings.Value.MatchMinutes * 60.0;
                    MatchEndsAt.Value = now + length;
                    _firstMeetingAt = now + length * firstMeetingFraction;
                    SendBriefing(caseDefinition.CaseTitle, caseDefinition.Briefing ?? string.Empty);
                    EnterPhase(MatchPhase.Briefing, briefingDuration);
                    break;

                case MatchPhase.Briefing when now >= PhaseEndsAt.Value:
                    EnterPhase(MatchPhase.OpeningAlibis, openingAlibiDuration);
                    break;

                case MatchPhase.OpeningAlibis when now >= PhaseEndsAt.Value:
                    EnterPhase(MatchPhase.Investigation, 0);
                    break;

                case MatchPhase.Investigation:
                    if (now >= MatchEndsAt.Value) { End(MatchOutcome.LiarWinsTimeUp); break; }
                    if (!_finalMeetingDone && now >= MatchEndsAt.Value - finalMeetingLeadTime) BeginMeeting(final: true);
                    else if (!_firstMeetingDone && now >= _firstMeetingAt) BeginMeeting(final: false);
                    break;

                case MatchPhase.Meeting when now >= PhaseEndsAt.Value:
                case MatchPhase.FinalMeeting when now >= PhaseEndsAt.Value:
                    _votes.Clear();
                    EnterPhase(MatchPhase.Accusation, voteDuration);
                    break;

                case MatchPhase.Accusation when now >= PhaseEndsAt.Value:
                    ResolveVote();
                    break;
            }
        }

        private void EnterPhase(MatchPhase phase, float duration)
        {
            Phase.Value = phase;
            PhaseEndsAt.Value = duration > 0 ? Now + duration : 0;
            _statementsThisPhase.Clear();
        }

        private void BeginMeeting(bool final)
        {
            _currentMeetingIsFinal = final;
            if (final) _finalMeetingDone = true;
            _firstMeetingDone = true; // an emergency meeting also counts as Meeting I

            int seat = 0;
            foreach (var kv in _players)
            {
                if (seat >= meetingSeats.Length) break;
                var t = meetingSeats[seat++];
                kv.Value.ServerTeleport(t.position, t.rotation);
            }
            EnterPhase(final ? MatchPhase.FinalMeeting : MatchPhase.Meeting,
                final ? finalMeetingDiscussionDuration : meetingDiscussionDuration);
        }

        private void End(MatchOutcome outcome)
        {
            if (Phase.Value == MatchPhase.Reveal) return;
            Outcome.Value = outcome;
            RevealedLiarSlot.Value = _liarSlot;
            EnterPhase(MatchPhase.Reveal, 0);

            // "Lie Replay": every exposed lie of the match, in order.
            if (_engine != null)
                foreach (var r in _engine.Log)
                    if (r.IsExposedLie)
                        SendLieReplayEntry(r.Statement.SpeakerId, r.Index, r.ExposedByClueId ?? string.Empty, r.WasFramed);
        }

        // ═════════════════════════════ STATEMENTS ═════════════════════════════

        public void ServerHandleStatement(ShortLegsPlayer speaker, StatementPacket packet)
        {
            var phase = Phase.Value;
            bool talkPhase = phase == MatchPhase.OpeningAlibis || phase == MatchPhase.Meeting || phase == MatchPhase.FinalMeeting;
            int slot = speaker.Slot.Value;
            if (!talkPhase || !IsAlive(slot) || !IsValidClaim(packet)) return;

            int limit = phase == MatchPhase.OpeningAlibis ? 1 : maxStatementsPerPhase;
            _statementsThisPhase.TryGetValue(slot, out int said);
            if (said >= limit) return;
            _statementsThisPhase[slot] = said + 1;

            string bound = packet.BoundClueId.ToString();
            if (!string.IsNullOrEmpty(bound) && !_engine.IsDiscovered(bound)) packet.BoundClueId = default;

            var record = _engine.SubmitStatement(
                new Statement(slot, packet.ToClaim(), Now, packet.BoundClueId.ToString()), out var detection);
            StatementLoggedRpc(record.Index, slot, packet);
            if (detection.HasValue) ApplyLie(detection.Value);
        }

        private bool IsValidClaim(StatementPacket p)
        {
            if (!Enum.IsDefined(typeof(Predicate), p.Predicate)) return false;
            bool subjectOk = p.SubjectId == FactClaim.AnySubject || _players.ContainsKey(p.SubjectId) || p.SubjectId >= 100;
            bool timeOk = p.TimeSlot == FactClaim.AnyTime || caseDefinition.TimeSlots.Contains(p.TimeSlot);
            return subjectOk && timeOk;
        }

        private void ApplyLie(LieDetection d)
        {
            if (!_players.TryGetValue(d.SpeakerId, out var liar)) return;
            string clueName = d.ClueId != null && _engine.TryGetClue(d.ClueId, out var c) ? c.DisplayName : "evidence";
            liar.ServerApplyLie($"Contradicted by {clueName}", d.Record.WasFramed);

            if (!ShrinkMatrix.IsExposed(liar.LieCount.Value, Settings.Value.MaxLiesAllowed)) return;

            if (d.SpeakerId == _liarSlot)
            {
                End(MatchOutcome.InvestigatorsWinLiarExposed);
            }
            else
            {
                // An investigator shrunk to nothing (usually framed) is out of the game.
                liar.ServerSetGhost(true);
                CheckInvestigatorWipe();
            }
        }

        // ═════════════════════════════ CLUES ═════════════════════════════

        public void ServerHandleInspect(ShortLegsPlayer player, int spotIndex)
        {
            if (Phase.Value != MatchPhase.Investigation || !IsAlive(player.Slot.Value)) return;
            if (spotIndex < 0 || spotIndex >= clueSpots.Count || !_spotToClue.TryGetValue(spotIndex, out var clueId)) return;
            if (!_engine.TryGetClue(clueId, out var clue) || _engine.IsDiscovered(clueId)) return;
            if (Vector3.Distance(player.transform.position, clueSpots[spotIndex].transform.position) > interactRange) return;

            var exposed = _engine.DiscoverClue(clueId);
            clueSpots[spotIndex].ServerMarkDiscovered();
            TrueCluesFound.Value = _engine.DiscoveredTrueClueCount;
            SendClueDiscovered(clueId, clue.DisplayName ?? clueId, clue.Description ?? string.Empty, player.Slot.Value);

            // Latent lies catch up — possibly several at once.
            foreach (var d in exposed)
            {
                ApplyLie(d);
                if (Phase.Value == MatchPhase.Reveal) return;
            }
        }

        public void ServerHandlePresentEvidence(ShortLegsPlayer presenter, int statementIndex, string clueId)
        {
            var phase = Phase.Value;
            if ((phase != MatchPhase.Meeting && phase != MatchPhase.FinalMeeting) || !IsAlive(presenter.Slot.Value)) return;
            bool exposed = _engine.PresentEvidence(statementIndex, clueId, out var detection);
            EvidencePresentedRpc(statementIndex, exposed);
            if (exposed) ApplyLie(detection);
        }

        public void ServerHandlePlantClue(ShortLegsPlayer player, StatementPacket fakeFact, int spotIndex)
        {
            int slot = player.Slot.Value;
            int allowedPlants = _firstMeetingDone ? 2 : 1;
            if (slot != _liarSlot || Phase.Value != MatchPhase.Investigation || _plantsUsed >= allowedPlants) return;
            if (spotIndex < 0 || spotIndex >= clueSpots.Count || _spotToClue.ContainsKey(spotIndex)) return;
            if (Vector3.Distance(player.transform.position, clueSpots[spotIndex].transform.position) > interactRange) return;
            // The Liar can only frame living investigators.
            if (fakeFact.SubjectId == _liarSlot || !IsAlive(fakeFact.SubjectId) || !IsValidClaim(fakeFact)) return;

            _plantsUsed++;
            var clue = new Clue
            {
                Id = $"planted_{_plantsUsed}",
                DisplayName = clueSpots[spotIndex].DecoyName,
                Description = clueSpots[spotIndex].DecoyDescription,
                IsTrueClue = true,       // looks genuine to everyone...
                IsPlanted = true,        // ...but never counts toward the 3 required clues.
                PlantedBySlot = slot,
            };
            clue.Facts.Add(new EvidenceFact(fakeFact.ToClaim(), 1f));
            _engine.RegisterClue(clue);
            _spotToClue[spotIndex] = clue.Id;
            clueSpots[spotIndex].ServerSetup(spotIndex, true);
        }

        public void ServerHandleSabotage(ShortLegsPlayer player)
        {
            if (player.Slot.Value != _liarSlot || _sabotageUsed || Phase.Value != MatchPhase.Investigation) return;
            _sabotageUsed = true;
            LightsOutUntil.Value = Now + lightsOutDuration;
        }

        // ═════════════════════════════ MEETINGS & VOTES ═════════════════════════════

        public void ServerHandleEmergencyMeeting(ShortLegsPlayer player)
        {
            int slot = player.Slot.Value;
            if (Phase.Value != MatchPhase.Investigation || !IsAlive(slot)) return;
            if (!_meetingsLeft.TryGetValue(slot, out int left) || left <= 0) return;
            _meetingsLeft[slot] = left - 1;
            BeginMeeting(final: false);
        }

        public void ServerHandleVote(ShortLegsPlayer voter, int targetSlot)
        {
            int slot = voter.Slot.Value;
            if (Phase.Value != MatchPhase.Accusation || !IsAlive(slot)) return;
            if (targetSlot != -1 && (targetSlot == slot || !IsAlive(targetSlot))) return;
            _votes[slot] = targetSlot;

            int alive = 0;
            foreach (var kv in _players) if (!kv.Value.IsGhost.Value) alive++;
            if (_votes.Count >= alive) ResolveVote();
        }

        private void ResolveVote()
        {
            int alive = 0;
            foreach (var kv in _players) if (!kv.Value.IsGhost.Value) alive++;

            var tally = new Dictionary<int, int>();
            foreach (var kv in _votes)
                if (kv.Value >= 0 && IsAlive(kv.Key)) tally[kv.Value] = tally.TryGetValue(kv.Value, out int n) ? n + 1 : 1;

            int target = -1;
            foreach (var kv in tally) if (kv.Value * 2 > alive) target = kv.Key; // strict majority of the living
            _votes.Clear();

            if (target < 0)
            {
                VerdictRpc(VerdictKind.NoMajority, -1);
            }
            else if (target == _liarSlot)
            {
                if (TrueCluesFound.Value >= LobbySettings.CluesRequired)
                {
                    VerdictRpc(VerdictKind.LiarCaught, target);
                    End(MatchOutcome.InvestigatorsWinAccusation);
                    return;
                }
                VerdictRpc(VerdictKind.InsufficientEvidence, target);
            }
            else
            {
                _players[target].ServerSetGhost(true);
                VerdictRpc(VerdictKind.InvestigatorFramed, target);
                if (CheckInvestigatorWipe()) return;
            }

            if (Now >= MatchEndsAt.Value) End(MatchOutcome.LiarWinsTimeUp);
            else EnterPhase(MatchPhase.Investigation, 0);
        }

        private bool CheckInvestigatorWipe()
        {
            if (!IsMatchRunning || AliveInvestigators() > 1) return false;
            End(MatchOutcome.LiarWinsInvestigatorsFramed);
            return true;
        }

        // ═════════════════════════════ BROADCASTS ═════════════════════════════

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastBriefingRpc(FixedString128Bytes title, FixedString512Bytes briefing) =>
            Debug.Log($"[ShortLegs] {title}: {briefing}");

        private void SendBriefing(string title, string briefing)
        {
            FixedString128Bytes t = default; t.CopyFromTruncated(title);
            FixedString512Bytes b = default; b.CopyFromTruncated(briefing);
            BroadcastBriefingRpc(t, b);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void StatementLoggedRpc(int index, int speakerSlot, StatementPacket packet) =>
            StatementLogged?.Invoke(index, speakerSlot, packet);

        private void SendClueDiscovered(string id, string name, string description, int finderSlot)
        {
            FixedString64Bytes i = default; i.CopyFromTruncated(id);
            FixedString64Bytes n = default; n.CopyFromTruncated(name);
            FixedString512Bytes d = default; d.CopyFromTruncated(description);
            ClueDiscoveredRpc(i, n, d, finderSlot);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ClueDiscoveredRpc(FixedString64Bytes id, FixedString64Bytes name, FixedString512Bytes description, int finderSlot) =>
            ClueDiscovered?.Invoke(id.ToString(), name.ToString(), description.ToString(), finderSlot);

        [Rpc(SendTo.ClientsAndHost)]
        private void EvidencePresentedRpc(int statementIndex, bool exposed) => EvidencePresented?.Invoke(statementIndex, exposed);

        [Rpc(SendTo.ClientsAndHost)]
        private void VerdictRpc(VerdictKind kind, int target) => VerdictAnnounced?.Invoke(kind, target);

        private void SendLieReplayEntry(int speaker, int statementIndex, string clueId, bool framed)
        {
            FixedString64Bytes c = default; c.CopyFromTruncated(clueId);
            LieReplayEntryRpc(speaker, statementIndex, c, framed);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void LieReplayEntryRpc(int speaker, int statementIndex, FixedString64Bytes clueId, bool framed) =>
            LieReplayEntry?.Invoke(speaker, statementIndex, clueId.ToString(), framed);

        // ═════════════════════════════ CLIENT HELPERS ═════════════════════════════

        public float SecondsLeftInMatch => MatchEndsAt.Value <= 0 ? 0f : Mathf.Max(0f, (float)(MatchEndsAt.Value - NetworkManager.ServerTime.Time));
        public float SecondsLeftInPhase => PhaseEndsAt.Value <= 0 ? 0f : Mathf.Max(0f, (float)(PhaseEndsAt.Value - NetworkManager.ServerTime.Time));
        public bool LightsOut => NetworkManager != null && NetworkManager.ServerTime.Time < LightsOutUntil.Value;
    }
}
