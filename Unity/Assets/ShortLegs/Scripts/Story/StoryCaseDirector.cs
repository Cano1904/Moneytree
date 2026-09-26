using System;
using System.Collections.Generic;
using ShortLegs.Cases;
using ShortLegs.Core;
using UnityEngine;

namespace ShortLegs.Story
{
    /// <summary>
    /// Singleplayer case flow (GDD §8.2). Lies stay latent until the detective PRESENTS the matching clue
    /// (auto-resolve is off in Story Mode). 3 lies → the culprit crawls and confesses; 3 wrong
    /// presentations → case lost.
    /// </summary>
    public sealed class StoryCaseDirector : MonoBehaviour
    {
        public static StoryCaseDirector Instance { get; private set; }

        [SerializeField] private CaseDefinition caseDefinition;
        [SerializeField] private List<ScriptedSuspect> suspects = new List<ScriptedSuspect>();
        [SerializeField] private int wrongPresentationsAllowed = 3;
        [SerializeField] private int liesUntilConfession = 3;
        [SerializeField] private string rewardHatId = "hat_brumms_beanie";

        public event Action<ScriptedSuspect, string> SuspectSaid;       // suspect, line
        public event Action<string> ClueFound;                           // clue id
        public event Action<ScriptedSuspect, int> LieExposed;            // suspect, statement index
        public event Action<int> WrongPresentation;                      // remaining
        public event Action<ScriptedSuspect> CaseSolved;
        public event Action CaseLost;

        public DeceptionEngine Engine { get; private set; }
        public IReadOnlyList<string> FoundClues => _found;
        public bool IsOver { get; private set; }

        private readonly List<string> _found = new List<string>();
        private readonly Dictionary<int, ScriptedSuspect> _speakerOf = new Dictionary<int, ScriptedSuspect>();
        private int _wrong;

        private void Awake()
        {
            Instance = this;
            Engine = new DeceptionEngine(caseDefinition.BuildClues(liarSlot: -1), autoResolveOnDiscovery: false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Beat 4: the suspect gives their alibi; every line becomes a judged statement.</summary>
        public void Interrogate(ScriptedSuspect suspect)
        {
            if (IsOver || suspect.HasSpoken) return;
            suspect.HasSpoken = true;
            foreach (var line in suspect.Alibi)
            {
                var fact = line.Claim.Resolve(-1).Fact;
                fact.SubjectId = suspect.SubjectId; // "I ..." always refers to the speaker
                var record = Engine.SubmitStatement(new Statement(suspect.SubjectId, fact, Time.timeAsDouble), out _);
                _speakerOf[record.Index] = suspect;
                SuspectSaid?.Invoke(suspect, line.Line);
            }
        }

        public bool Discover(string clueId)
        {
            if (IsOver || Engine.IsDiscovered(clueId) || !Engine.TryGetClue(clueId, out _)) return false;
            Engine.DiscoverClue(clueId);
            _found.Add(clueId);
            ClueFound?.Invoke(clueId);
            return true;
        }

        /// <summary>The Evidence Binding Wheel's "Present" button.</summary>
        public bool Present(int statementIndex, string clueId)
        {
            if (IsOver || !_speakerOf.TryGetValue(statementIndex, out var suspect)) return false;

            if (!Engine.PresentEvidence(statementIndex, clueId, out _))
            {
                _wrong++;
                WrongPresentation?.Invoke(Mathf.Max(0, wrongPresentationsAllowed - _wrong));
                if (_wrong >= wrongPresentationsAllowed) { IsOver = true; CaseLost?.Invoke(); }
                return false;
            }

            suspect.ApplyLie();
            LieExposed?.Invoke(suspect, statementIndex);

            if (suspect.IsCulprit && suspect.LieCount >= liesUntilConfession)
            {
                IsOver = true;
                SuspectSaid?.Invoke(suspect, suspect.Confession);
                Progression.Unlock(rewardHatId);
                Progression.Unlock($"case_{caseDefinition.name}");
                CaseSolved?.Invoke(suspect);
            }
            return true;
        }

        /// <summary>Scripted follow-up questions (e.g. beat 9) add new statements mid-case.</summary>
        public int AskFollowUp(ScriptedSuspect suspect, ScriptedLine line)
        {
            var fact = line.Claim.Resolve(-1).Fact;
            fact.SubjectId = suspect.SubjectId;
            var record = Engine.SubmitStatement(new Statement(suspect.SubjectId, fact, Time.timeAsDouble), out _);
            _speakerOf[record.Index] = suspect;
            SuspectSaid?.Invoke(suspect, line.Line);
            return record.Index;
        }
    }
}
