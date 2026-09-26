using System;
using UnityEngine;

namespace ShortLegs.Gameplay
{
    /// <summary>
    /// Scales the humanoid leg bones on Y (feet keep their size) and lowers the hips so feet stay
    /// planted (GDD §1.4). Runs in LateUpdate, after the Animator has written the pose.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class LegRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [Tooltip("Visual root that is lowered as the legs shrink (usually the model child, not the network root).")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private float shrinkDuration = 0.6f;
        [Tooltip("Overshoot of the 'boing' ease.")]
        [SerializeField] private float overshoot = 1.9f;
        [Tooltip("Never scale to exactly zero (degenerate matrices); 4 lies = 'stuck' at this size.")]
        [SerializeField] private float minimumVisualScale = 0.06f;

        public float CurrentScale { get; private set; } = 1f;
        public bool IsAnimating => _t < 1f;
        public event Action<int> ShrinkStarted;   // lie count — face swap, SFX, VFX hook

        private static readonly HumanBodyBones[] LegBones =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        };

        private Transform[] _bones = Array.Empty<Transform>();
        private Transform _leftFoot;
        private float _legLength;           // hips → ground at scale 1, measured once at bind pose
        private float _baseHeight, _baseCenterY, _baseModelY;
        private float _from = 1f, _to = 1f, _t = 1f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (modelRoot == null && animator != null) modelRoot = animator.transform;

            if (animator != null && animator.isHuman)
            {
                _bones = new Transform[LegBones.Length];
                for (int i = 0; i < LegBones.Length; i++) _bones[i] = animator.GetBoneTransform(LegBones[i]);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (hips != null && _leftFoot != null) _legLength = Mathf.Abs(hips.position.y - _leftFoot.position.y);
            }

            if (characterController != null)
            {
                _baseHeight = characterController.height;
                _baseCenterY = characterController.center.y;
            }
            if (modelRoot != null) _baseModelY = modelRoot.localPosition.y;
        }

        /// <summary>Animated shrink, called from RPC_SyncBoneScale on every client.</summary>
        public void PlayShrink(float targetScale, int lieCount)
        {
            _from = CurrentScale;
            _to = Mathf.Clamp01(targetScale);
            _t = 0f;
            ShrinkStarted?.Invoke(lieCount);
        }

        /// <summary>No animation — late join, reconnect, menu reset.</summary>
        public void SetScaleImmediate(float scale)
        {
            _from = _to = CurrentScale = Mathf.Clamp01(scale);
            _t = 1f;
            ApplyCollider(CurrentScale);
        }

        private void LateUpdate()
        {
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.01f, shrinkDuration));
                CurrentScale = Mathf.LerpUnclamped(_from, _to, EaseOutBack(_t, overshoot));
                ApplyCollider(CurrentScale);
            }
            ApplyBones(CurrentScale);
        }

        private void ApplyBones(float scale)
        {
            float s = Mathf.Max(minimumVisualScale, scale);
            // Assumes Y runs along the bone (Mixamo / UE-style humanoids). Scale the thighs only: the shin
            // inherits it through the hierarchy, so it is kept at 1 to avoid double-shrinking.
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                if (b == null) continue;
                bool lower = i % 2 == 1;
                var ls = b.localScale;
                b.localScale = new Vector3(ls.x, lower ? 1f : s, ls.z);
            }
            // Feet would inherit the squash from the thigh; undo it so shoes stay the same size.
            if (_leftFoot != null)
            {
                var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                UnsquashFoot(_leftFoot, s);
                if (rightFoot != null) UnsquashFoot(rightFoot, s);
            }

            if (modelRoot != null)
            {
                var p = modelRoot.localPosition;
                p.y = _baseModelY - _legLength * (1f - s);
                modelRoot.localPosition = p;
            }
        }

        private static void UnsquashFoot(Transform foot, float s)
        {
            var ls = foot.localScale;
            foot.localScale = new Vector3(ls.x, 1f / s, ls.z);
        }

        private void ApplyCollider(float scale)
        {
            if (characterController == null || _baseHeight <= 0f) return;
            float s = Mathf.Max(minimumVisualScale, Mathf.Clamp01(scale));
            float height = _baseHeight - _legLength * (1f - s);
            height = Mathf.Max(characterController.radius * 2f, height);
            characterController.height = height;
            var c = characterController.center;
            c.y = _baseCenterY - (_baseHeight - height) * 0.5f;
            characterController.center = c;
        }

        private static float EaseOutBack(float t, float s)
        {
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
