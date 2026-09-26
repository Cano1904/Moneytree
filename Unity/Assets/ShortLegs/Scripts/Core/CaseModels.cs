using System;
using System.Collections.Generic;

namespace ShortLegs.Core
{
    /// <summary>What a fact or claim says about its subject.</summary>
    public enum Predicate : byte
    {
        WasIn = 0,      // Subject was in <room>            (exclusive: one room per time slot)
        Had = 1,        // Subject had <object>
        Touched = 2,    // Subject touched <object>
        Saw = 3,        // Subject saw <player/npc>
        WasAsleep = 4,  // Subject was asleep (value ignored, use 0)
    }

    public static class PredicateRules
    {
        /// <summary>
        /// Exclusive predicates can only hold one value per subject and time slot, so a positive
        /// claim with a different value contradicts the evidence ("I was in the Library" vs. "was in the Study").
        /// </summary>
        public static bool IsExclusive(Predicate p) => p == Predicate.WasIn || p == Predicate.WasAsleep;
    }

    /// <summary>
    /// One atomic, machine-checkable proposition. Built by the Evidence Binding Wheel
    /// (Subject + Claim + Value) and by clues (what the evidence proves).
    /// </summary>
    [Serializable]
    public struct FactClaim : IEquatable<FactClaim>
    {
        public const int AnySubject = -1;   // "Nobody" / "Somebody"
        public const int AnyTime = -1;

        public int SubjectId;       // player slot 0..7, NPCs 100+
        public Predicate Predicate;
        public int ValueId;         // room / object / person id from the case's id table
        public int TimeSlot;        // e.g. 23 = 23:00, or AnyTime
        public bool Negated;        // "did NOT ..." / "Nobody ..."

        public FactClaim(int subjectId, Predicate predicate, int valueId, int timeSlot = AnyTime, bool negated = false)
        {
            SubjectId = subjectId;
            Predicate = predicate;
            ValueId = valueId;
            TimeSlot = timeSlot;
            Negated = negated;
        }

        public bool Equals(FactClaim o) =>
            SubjectId == o.SubjectId && Predicate == o.Predicate && ValueId == o.ValueId &&
            TimeSlot == o.TimeSlot && Negated == o.Negated;

        public override bool Equals(object obj) => obj is FactClaim o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(SubjectId, (int)Predicate, ValueId, TimeSlot, Negated);
        public override string ToString() =>
            $"[{SubjectId}] {(Negated ? "NOT " : "")}{Predicate} {ValueId} @{(TimeSlot == AnyTime ? "*" : TimeSlot.ToString())}";

        /// <summary>
        /// True when <paramref name="claim"/> cannot be true if <paramref name="evidence"/> is true.
        /// Unknowns are never lies: a claim nothing speaks against is not punished.
        /// </summary>
        public static bool Contradicts(in FactClaim claim, in FactClaim evidence)
        {
            if (claim.Predicate != evidence.Predicate) return false;
            if (claim.TimeSlot != AnyTime && evidence.TimeSlot != AnyTime && claim.TimeSlot != evidence.TimeSlot) return false;

            bool subjectMatch = claim.SubjectId == evidence.SubjectId || claim.SubjectId == AnySubject;
            if (!subjectMatch) return false;

            if (claim.Negated)
            {
                // "Nobody touched the Money Tree" vs. evidence "Player 3 touched the Money Tree"
                return !evidence.Negated && claim.ValueId == evidence.ValueId;
            }

            // A positive "somebody did X" is never contradicted by a single fact.
            if (claim.SubjectId == AnySubject) return false;

            if (evidence.Negated)
            {
                // "I had the key" vs. evidence "Player 2 did NOT have the key"
                return claim.ValueId == evidence.ValueId;
            }

            return PredicateRules.IsExclusive(claim.Predicate) && claim.ValueId != evidence.ValueId;
        }
    }

    /// <summary>A fact a clue proves, with how much it weighs in the Deception Index.</summary>
    [Serializable]
    public struct EvidenceFact
    {
        public FactClaim Fact;
        public float Weight;

        public EvidenceFact(FactClaim fact, float weight = 1f)
        {
            Fact = fact;
            Weight = weight;
        }
    }

    /// <summary>A physical clue placed in the crime scene.</summary>
    [Serializable]
    public sealed class Clue
    {
        public string Id;
        public string DisplayName;
        public string Description;
        /// <summary>True clues count toward the 3 required for a valid accusation.</summary>
        public bool IsTrueClue;
        /// <summary>Planted by the Liar: its facts are fake and can frame truthful players.</summary>
        public bool IsPlanted;
        public int PlantedBySlot = -1;
        public List<EvidenceFact> Facts = new List<EvidenceFact>();
    }

    /// <summary>Something a player or NPC said, as evaluated by the server.</summary>
    [Serializable]
    public struct Statement
    {
        public int SpeakerId;
        public FactClaim Claim;
        public string BoundClueId;  // optional evidence the speaker offers as proof
        public double ServerTime;

        public Statement(int speakerId, FactClaim claim, double serverTime, string boundClueId = null)
        {
            SpeakerId = speakerId;
            Claim = claim;
            ServerTime = serverTime;
            BoundClueId = boundClueId;
        }
    }
}
