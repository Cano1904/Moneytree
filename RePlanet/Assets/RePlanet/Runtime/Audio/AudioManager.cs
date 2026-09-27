using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// SCHNITTSTELLE (wird vom Audio-Strang vollständig implementiert).
    /// Spielt prozedural erzeugte Effekte (Core/Audio/Synth) und die mehrspurige Musik.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        void Awake() { I = this; }

        /// <summary>Einmaliger Effekt; pos = null → 2D (Oberfläche).</summary>
        public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitch = 1f) { }

        /// <summary>Dauerklang an/aus (z. B. Sauger, Motor). key identifiziert die Quelle.</summary>
        public static void Loop(string key, string clipId, bool on, Vector3? pos = null, float volume = 1f, float pitch = 1f) { }

        public static void Ui(string id) { Play(id, null, 1f, 1f); }

        public static void PlayIntro() { }
        public static void StopIntro() { }
        /// <summary>Sekunden seit Intro-Start (DSP-genau), −1 wenn nicht bereit.</summary>
        public static double IntroTime { get { return -1; } }
        public static bool IntroReady { get { return false; } }
        public static void PlayEnding() { }
        public static void StopEnding() { }
    }
}
