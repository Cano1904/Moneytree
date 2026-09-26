using System;
using System.Collections.Generic;
using ShortLegs.Core;
using UnityEngine;

namespace ShortLegs.Cases
{
    public enum SubjectToken
    {
        TheLiar = 0,     // resolved at match start to the secret Liar's slot
        FixedId = 1,     // an NPC or a fixed id (Story Mode)
    }

    [Serializable]
    public struct FactTemplate
    {
        public SubjectToken Subject;
        public int FixedSubjectId;
        public Predicate Predicate;
        public int ValueId;
        public int TimeSlot;
        public bool Negated;
        [Min(0f)] public float Weight;

        public EvidenceFact Resolve(int liarSlot)
        {
            int subject = Subject == SubjectToken.TheLiar ? liarSlot : FixedSubjectId;
            return new EvidenceFact(new FactClaim(subject, Predicate, ValueId, TimeSlot, Negated), Weight <= 0f ? 1f : Weight);
        }
    }

    [Serializable]
    public sealed class ClueTemplate
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public bool IsTrueClue = true;
        /// <summary>Index into the map's ClueSpot list.</summary>
        public int SpotIndex;
        public List<FactTemplate> Facts = new List<FactTemplate>();
    }

    [Serializable]
    public struct NamedId
    {
        public int Id;
        public string Name;
    }

    /// <summary>A crime (GDD §8): the clues, what they prove and the id tables for the Evidence Binding Wheel.</summary>
    [CreateAssetMenu(menuName = "Short Legs/Case Definition", fileName = "Case_")]
    public sealed class CaseDefinition : ScriptableObject
    {
        public string CaseTitle = "The Missing Money Tree";
        [TextArea(3, 8)] public string Briefing;
        public MapId Map = MapId.GrandManor;
        public List<NamedId> Rooms = new List<NamedId>();
        public List<NamedId> Objects = new List<NamedId>();
        public List<int> TimeSlots = new List<int> { 22, 23, 0 };
        public List<ClueTemplate> Clues = new List<ClueTemplate>();

        public List<Clue> BuildClues(int liarSlot)
        {
            var result = new List<Clue>(Clues.Count);
            foreach (var t in Clues)
            {
                var clue = new Clue
                {
                    Id = t.Id,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    IsTrueClue = t.IsTrueClue,
                };
                foreach (var f in t.Facts) clue.Facts.Add(f.Resolve(liarSlot));
                result.Add(clue);
            }
            return result;
        }

        public ClueTemplate FindTemplate(string clueId) => Clues.Find(c => c.Id == clueId);
    }

    public enum MapId : byte
    {
        GrandManor = 0,
        SunkenYacht = 1,
        NightTrain = 2,
        FrozenLodge = 3,
    }
}
