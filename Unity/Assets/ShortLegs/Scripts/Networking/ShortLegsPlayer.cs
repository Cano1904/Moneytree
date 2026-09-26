using System;
using System.Collections.Generic;
using ShortLegs.Core;
using ShortLegs.Gameplay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShortLegs.Networking
{
    public readonly struct ShrinkEventInfo
    {
        public readonly int Slot;
        public readonly float LegScale;
        public readonly int LieCount;
        public readonly string Reason;
        public readonly bool Framed;

        public ShrinkEventInfo(int slot, float legScale, int lieCount, string reason, bool framed)
        {
            Slot = slot;
            LegScale = legScale;
            LieCount = lieCount;
            Reason = reason;
            Framed = framed;
        }
    }

    /// <summary>
    /// Networked player pawn. Leg scale is server-owned state (late-join safe); the shrink moment is
    /// broadcast with <see cref="RPC_SyncBoneScale"/> so every client plays it in sync (GDD §1.2, §1.4).
    /// Clients only ever send intents; the server decides what is a lie.
    /// </summary>
    [RequireComponent(typeof(LegRig))]
    public sealed class ShortLegsPlayer : NetworkBehaviour
    {
        public static readonly List<ShortLegsPlayer> All = new List<ShortLegsPlayer>();
        public static ShortLegsPlayer Local { get; private set; }
        public static event Action<ShrinkEventInfo> AnyShrinkEvent;

        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> LieCount = new NetworkVariable<int>(0);
        public readonly NetworkVariable<float> LegScale = new NetworkVariable<float>(1f);
        public readonly NetworkVariable<bool> IsGhost = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> IsPausedBadge = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [SerializeField] private float baseWalkSpeed = 3.2f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [Tooltip("Extra slack for latency before the server corrects a client position.")]
        [SerializeField] private float movementTolerance = 1.35f;

        /// <summary>Only meaningful on the owning client — roles are never replicated to others.</summary>
        public PlayerRole LocalRole { get; private set; } = PlayerRole.Unknown;
        public event Action<PlayerRole> RoleRevealed;
        public event Action<ShrinkEventInfo> ShrinkEventPlayed;

        public LocomotionCaps Caps => ShrinkMatrix.Caps(LieCount.Value);
        public float BaseWalkSpeed => baseWalkSpeed;
        public float SprintMultiplier => sprintMultiplier;

        private LegRig _rig;
        private ShrinkAwareLocomotion _locomotion;
        private Vector3 _lastValidPosition;
        private double _lastValidTime;

        private void Awake()
        {
            _rig = GetComponent<LegRig>();
            _locomotion = GetComponent<ShrinkAwareLocomotion>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsOwner)
            {
                Local = this;
                DisplayName.Value = SettingsNameOrDefault();
            }

            // Late joiners / reconnects snap to the current height without replaying the animation.
            _rig.SetScaleImmediate(LegScale.Value);
            LegScale.OnValueChanged += OnLegScaleChanged;

            _lastValidPosition = transform.position;
            _lastValidTime = NetworkManager.ServerTime.Time;

            if (IsServer && MatchDirector.Instance != null) MatchDirector.Instance.ServerRegisterPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            LegScale.OnValueChanged -= OnLegScaleChanged;
            if (IsServer && MatchDirector.Instance != null) MatchDirector.Instance.ServerUnregisterPlayer(this);
        }

        private void OnLegScaleChanged(float previous, float current)
        {
            // The RPC normally drives the tween; this catches the case where the RPC arrives before/without the state.
            if (!_rig.IsAnimating && !Mathf.Approximately(_rig.CurrentScale, current)) _rig.SetScaleImmediate(current);
        }

        private static string SettingsNameOrDefault()
        {
            var name = Settings.SettingsManager.Instance != null ? Settings.SettingsManager.Instance.Current.PlayerName : null;
            if (string.IsNullOrWhiteSpace(name)) name = "Detective";
            return name.Length > 28 ? name.Substring(0, 28) : name;
        }

        // ───────────────────────────── SERVER API ─────────────────────────────

        /// <summary>Server only: one lie → 25% shorter legs, synced to every client.</summary>
        public void ServerApplyLie(string reason, bool framed)
        {
            if (!IsServer) throw new InvalidOperationException("Only the server may shrink legs.");
            LieCount.Value += 1;
            LegScale.Value = ShrinkMatrix.LegScale(LieCount.Value);
            RPC_SyncBoneScale(LegScale.Value, LieCount.Value, reason, framed);
        }

        public void ServerSetGhost(bool ghost)
        {
            if (IsServer) IsGhost.Value = ghost;
        }

        /// <summary>RPC_SyncBoneScale() from the spec — server entry point for the shrink broadcast.</summary>
        public void RPC_SyncBoneScale(float scale, int lies, string reason, bool framed)
        {
            FixedString128Bytes r = default;
            r.CopyFromTruncated(reason ?? string.Empty);
            SyncBoneScaleRpc(scale, lies, r, framed);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SyncBoneScaleRpc(float scale, int lies, FixedString128Bytes reason, bool framed)
        {
            _rig.PlayShrink(scale, lies);
            var info = new ShrinkEventInfo(Slot.Value, scale, lies, reason.ToString(), framed);
            ShrinkEventPlayed?.Invoke(info);
            AnyShrinkEvent?.Invoke(info);
        }

        [Rpc(SendTo.Owner)]
        public void RoleAssignedRpc(PlayerRole role)
        {
            LocalRole = role;
            RoleRevealed?.Invoke(role);
        }

        [Rpc(SendTo.Owner)]
        public void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            if (_locomotion != null) _locomotion.Teleport(position, rotation);
            else transform.SetPositionAndRotation(position, rotation);
        }

        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            _lastValidPosition = position;
            _lastValidTime = NetworkManager.ServerTime.Time;
            TeleportRpc(position, rotation);
        }

        // ─────────────────────── SERVER MOVEMENT VALIDATION ───────────────────────

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            double now = NetworkManager.ServerTime.Time;
            float dt = (float)(now - _lastValidTime);
            if (dt < 0.2f) return;

            var caps = Caps;
            float maxSpeed = baseWalkSpeed * caps.SpeedMultiplier * (caps.CanSprint ? sprintMultiplier : 1f);
            Vector3 delta = transform.position - _lastValidPosition;
            delta.y = 0f; // falling is fine, horizontal speed-hacking is not
            float allowed = maxSpeed * dt * movementTolerance + 0.25f;

            if (delta.magnitude > allowed)
            {
                // Short legs can't outrun the truth: snap the owner back.
                TeleportRpc(_lastValidPosition, transform.rotation);
            }
            else
            {
                _lastValidPosition = transform.position;
            }
            _lastValidTime = now;
        }

        // ───────────────────────────── CLIENT → SERVER ─────────────────────────────

        private bool FromOwner(RpcParams p) => p.Receive.SenderClientId == OwnerClientId;

        [Rpc(SendTo.Server)]
        public void SubmitStatementRpc(StatementPacket packet, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandleStatement(this, packet);
        }

        [Rpc(SendTo.Server)]
        public void InspectSpotRpc(int spotIndex, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandleInspect(this, spotIndex);
        }

        [Rpc(SendTo.Server)]
        public void PresentEvidenceRpc(int statementIndex, FixedString64Bytes clueId, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandlePresentEvidence(this, statementIndex, clueId.ToString());
        }

        [Rpc(SendTo.Server)]
        public void CallEmergencyMeetingRpc(RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandleEmergencyMeeting(this);
        }

        [Rpc(SendTo.Server)]
        public void CastVoteRpc(int targetSlot, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandleVote(this, targetSlot);
        }

        [Rpc(SendTo.Server)]
        public void PlantClueRpc(StatementPacket fakeFact, int spotIndex, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandlePlantClue(this, fakeFact, spotIndex);
        }

        [Rpc(SendTo.Server)]
        public void SabotageLightsRpc(RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) MatchDirector.Instance?.ServerHandleSabotage(this);
        }

        [Rpc(SendTo.Server)]
        public void SetPausedRpc(bool paused, RpcParams rpcParams = default)
        {
            if (FromOwner(rpcParams)) IsPausedBadge.Value = paused;
        }
    }
}
