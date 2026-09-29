// Ersatz für Laufzeit-Bausteine, deren Prüfung ohne echte Engine nichts bringt (Oberfläche, Klang, eigene Shader).
using System;
using RePlanet.Core;
using UnityEngine;

public static class Harness
{
    public static void OnAdd(UnityEngine.Component c)
    {
        // [RequireComponent] wie in Unity
        if (c is UnityEngine.ParticleSystem) c.gameObject.AddComponent<UnityEngine.ParticleSystemRenderer>();
        if (c is UnityEngine.TextMesh && c.gameObject.GetComponent<UnityEngine.MeshRenderer>() == null) c.gameObject.AddComponent<UnityEngine.MeshRenderer>();
    }
}

namespace RePlanet
{
    /// <summary>Ersatz für TerrainLook (eigene Shader) – die Prüfumgebung zählt nur Geometrie.</summary>
    public static class TerrainLook
    {
        public static UnityEngine.Material CreateTerrain(string planet, UnityEngine.Texture2D tex) { return null; }
        public static UnityEngine.Material CreateWater(string planet, System.Func<UnityEngine.Texture2D> fallbackNormals) { return null; }
        public static void SetWaterClarity(UnityEngine.Material m, string planet, float q) { }
        public static void AnimateWater(UnityEngine.Material m, float t) { }
    }

    public class PostFX : MonoBehaviour
    {
        public static PostFX I { get; private set; }
        public static bool Running => false;
        public void Configure(int q, bool calm) { }
    }

    public class UIRoot : MonoBehaviour
    {
        public static bool WantsCursor;
    }

    /// <summary>Klang-Ersatz: IntroTime gibt die Prüfumgebung vor (Sequenzzeit wie vom Score).</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }
        public static double FakeIntroTime = -1;
        /// <summary>
        /// Wie der echte AudioManager statt vorgegebener Zeit: nach PlayIntro ist der Score erst nach <see cref="ScoreReadyIn"/> s
        /// erzeugt, startet dann vorgemerkt 0,2 s später und läuft mit der Echtzeit (auch während langer Bilder); StopIntro beendet ihn.
        /// </summary>
        public static bool SimulateScore;
        public static float ScoreReadyIn;
        public static int IntroStops;
        static double scoreStart = -1;
        public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitch = 1f) { }
        public static void Loop(string key, string clipId, bool on, Vector3? pos = null, float volume = 1f, float pitch = 1f) { }
        public static void Ui(string id) { }
        public static void PlayIntro() { scoreStart = Time.realtimeSinceStartup + ScoreReadyIn + 0.2; }
        public static void StopIntro() { FakeIntroTime = -1; scoreStart = -1; IntroStops++; }
        /// <summary>Der Aufbau vor PlayIntro dauerte in Wirklichkeit s Sekunden (die Prüfumgebung rechnet die Bildzeit erst danach).</summary>
        public static void DelayScore(double s) { if (scoreStart >= 0) scoreStart += s; }
        public static double IntroTime
        {
            get
            {
                if (!SimulateScore) return FakeIntroTime;
                if (scoreStart < 0 || Time.realtimeSinceStartup < scoreStart) return -1;
                double t = Time.realtimeSinceStartup - scoreStart;
                return t > IntroTimeline.Total + 0.5 ? -1 : t;
            }
        }
        public static bool IntroReady => false;
        public static void PlayEnding() { }
        public static void StopEnding() { }
        public static void DuckMusic(float amount) { }
        public static float VoiceGain => 1f;
        public static Transform VoiceParent => null;
    }
}
