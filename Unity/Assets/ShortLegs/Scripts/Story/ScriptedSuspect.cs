using System;
using System.Collections.Generic;
using ShortLegs.Audio;
using ShortLegs.Cases;
using ShortLegs.Core;
using ShortLegs.Gameplay;
using UnityEngine;

namespace ShortLegs.Story
{
    [Serializable]
    public sealed class ScriptedLine
    {
        [TextArea] public string Line;
        public FactTemplate Claim;
    }

    /// <summary>An AI suspect in Story Mode (GDD §8.2). Uses the same LegRig and Shrink Matrix as players.</summary>
    [RequireComponent(typeof(LegRig))]
    public sealed class ScriptedSuspect : MonoBehaviour
    {
        [SerializeField] private string displayName = "Baron Brumm";
        [Tooltip("NPC ids start at 100 so they never collide with player slots.")]
        [SerializeField] private int subjectId = 100;
        [SerializeField] private List<ScriptedLine> alibi = new List<ScriptedLine>();
        [SerializeField, TextArea] private string confession = "Fine! FINE! I took the Money Tree!";
        [SerializeField] private bool isCulprit;
        [SerializeField] private VoicePitchShifter voice;
        [SerializeField] private Animator faceAnimator;

        private static readonly int FaceHash = Animator.StringToHash("FaceState"); // 0 calm, 1 sweat, 2 panic stare

        public string DisplayName => displayName;
        public int SubjectId => subjectId;
        public bool IsCulprit => isCulprit;
        public IReadOnlyList<ScriptedLine> Alibi => alibi;
        public string Confession => confession;
        public int LieCount { get; private set; }
        public bool HasSpoken { get; set; }
        public event Action<ScriptedSuspect> Shrunk;

        private LegRig _rig;

        private void Awake() => _rig = GetComponent<LegRig>();

        public void ApplyLie()
        {
            LieCount++;
            _rig.PlayShrink(ShrinkMatrix.LegScale(LieCount), LieCount);
            if (voice != null) voice.SetLieCount(LieCount);
            if (faceAnimator != null) faceAnimator.SetInteger(FaceHash, LieCount >= ShrinkMatrix.CrawlLies ? 2 : 1);
            Shrunk?.Invoke(this);
        }
    }
}
