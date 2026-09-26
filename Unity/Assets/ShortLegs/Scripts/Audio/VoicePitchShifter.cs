using ShortLegs.Core;
using ShortLegs.Networking;
using UnityEngine;
using UnityEngine.Audio;

namespace ShortLegs.Audio
{
    /// <summary>
    /// +15% voice pitch per lie (GDD §5.2). Live VOIP can't use AudioSource.pitch (it would drift the
    /// stream buffer), so each voice source is routed into a mixer group that carries a Pitch Shifter
    /// effect preset for that lie level: Voice_L0 = 1.00, L1 = 1.15, L2 = 1.30, L3 = 1.45, L4 = 1.60.
    /// </summary>
    public sealed class VoicePitchShifter : MonoBehaviour
    {
        [Tooltip("Index = lie count. Each group has a Pitch Shifter effect set to ShrinkMatrix.VoicePitch(index).")]
        [SerializeField] private AudioMixerGroup[] pitchGroupsByLies = new AudioMixerGroup[5];
        [Tooltip("The VOIP participant tap (Vivox) and/or NPC dialogue sources.")]
        [SerializeField] private AudioSource[] voiceSources;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip shrinkSqueak;

        private ShortLegsPlayer _player;

        private void Awake() => _player = GetComponentInParent<ShortLegsPlayer>();

        private void OnEnable()
        {
            if (_player == null) return;
            _player.LieCount.OnValueChanged += OnLiesChanged;
            SetLieCount(_player.LieCount.Value, playSting: false);
        }

        private void OnDisable()
        {
            if (_player != null) _player.LieCount.OnValueChanged -= OnLiesChanged;
        }

        private void OnLiesChanged(int previous, int current) => SetLieCount(current);

        /// <summary>Called for NPCs directly, and for players via the replicated LieCount.</summary>
        public void SetLieCount(int lies, bool playSting = true)
        {
            int i = Mathf.Clamp(lies, 0, pitchGroupsByLies.Length - 1);
            var group = pitchGroupsByLies.Length > 0 ? pitchGroupsByLies[i] : null;
            if (group != null)
                foreach (var src in voiceSources) if (src != null) src.outputAudioMixerGroup = group;

            if (playSting && sfxSource != null && shrinkSqueak != null)
            {
                sfxSource.pitch = ShrinkMatrix.VoicePitch(lies); // one-shot SFX may use plain pitch
                sfxSource.PlayOneShot(shrinkSqueak);
            }
        }

        /// <summary>Vivox hands us a participant tap at runtime; register it here.</summary>
        public void AttachVoiceSource(AudioSource source)
        {
            var list = new System.Collections.Generic.List<AudioSource>(voiceSources ?? new AudioSource[0]) { source };
            voiceSources = list.ToArray();
            SetLieCount(_player != null ? _player.LieCount.Value : 0, playSting: false);
        }

#if UNITY_EDITOR
        [ContextMenu("Log expected pitch per group")]
        private void LogExpected()
        {
            for (int i = 0; i < pitchGroupsByLies.Length; i++)
                Debug.Log($"Voice_L{i}: Pitch Shifter pitch = {ShrinkMatrix.VoicePitch(i):0.00}");
        }
#endif
    }
}
