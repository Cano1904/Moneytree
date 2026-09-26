using System;
using ShortLegs.Cases;
using ShortLegs.Core;
using Unity.Collections;
using Unity.Netcode;

namespace ShortLegs.Networking
{
    public enum PlayerRole : byte { Unknown = 0, Investigator = 1, Liar = 2 }

    public enum MatchPhase : byte
    {
        Lobby, RoleReveal, Briefing, OpeningAlibis, Investigation, Meeting, FinalMeeting, Accusation, Reveal,
    }

    public enum MatchOutcome : byte
    {
        None,
        InvestigatorsWinAccusation,
        InvestigatorsWinLiarExposed,
        InvestigatorsWinLiarAbandoned,
        LiarWinsTimeUp,
        LiarWinsInvestigatorsFramed,
    }

    public enum VerdictKind : byte { LiarCaught, InsufficientEvidence, InvestigatorFramed, NoMajority }

    /// <summary>Host-controlled lobby settings (GDD §3.1).</summary>
    [Serializable]
    public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
    {
        public int MaxLiesAllowed;
        public bool ProximityVoice;
        public MapId Map;
        public int MatchMinutes;
        public int MeetingsPerPlayer;

        public const int MinPlayers = 4;
        public const int MaxPlayers = 8;
        public const int CluesRequired = 3;

        public static LobbySettings Default => new LobbySettings
        {
            MaxLiesAllowed = 4,
            ProximityVoice = true,
            Map = MapId.GrandManor,
            MatchMinutes = 12,
            MeetingsPerPlayer = 1,
        };

        public LobbySettings Clamped()
        {
            var s = this;
            s.MaxLiesAllowed = Math.Clamp(s.MaxLiesAllowed, 1, 4);
            s.MatchMinutes = Math.Clamp(s.MatchMinutes, 8, 20);
            s.MeetingsPerPlayer = Math.Clamp(s.MeetingsPerPlayer, 1, 3);
            if (!Enum.IsDefined(typeof(MapId), s.Map)) s.Map = MapId.GrandManor;
            return s;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MaxLiesAllowed);
            serializer.SerializeValue(ref ProximityVoice);
            serializer.SerializeValue(ref Map);
            serializer.SerializeValue(ref MatchMinutes);
            serializer.SerializeValue(ref MeetingsPerPlayer);
        }

        public bool Equals(LobbySettings o) =>
            MaxLiesAllowed == o.MaxLiesAllowed && ProximityVoice == o.ProximityVoice && Map == o.Map &&
            MatchMinutes == o.MatchMinutes && MeetingsPerPlayer == o.MeetingsPerPlayer;
    }

    /// <summary>Wire format of one Evidence Binding Wheel statement.</summary>
    public struct StatementPacket : INetworkSerializable
    {
        public int SubjectId;
        public Predicate Predicate;
        public int ValueId;
        public int TimeSlot;
        public bool Negated;
        public FixedString64Bytes BoundClueId;

        public StatementPacket(FactClaim claim, string boundClueId = null)
        {
            SubjectId = claim.SubjectId;
            Predicate = claim.Predicate;
            ValueId = claim.ValueId;
            TimeSlot = claim.TimeSlot;
            Negated = claim.Negated;
            BoundClueId = boundClueId ?? string.Empty;
        }

        public FactClaim ToClaim() => new FactClaim(SubjectId, Predicate, ValueId, TimeSlot, Negated);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SubjectId);
            serializer.SerializeValue(ref Predicate);
            serializer.SerializeValue(ref ValueId);
            serializer.SerializeValue(ref TimeSlot);
            serializer.SerializeValue(ref Negated);
            serializer.SerializeValue(ref BoundClueId);
        }
    }
}
