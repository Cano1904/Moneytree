using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Erzählerstimme für Intro und Abspann.
    /// <list type="bullet">
    /// <item>Spielt Sprachaufnahmen ab, die als AudioClips in einem Ordner <c>Resources/Voice</c> liegen
    /// (<c>intro_01</c> … <c>intro_14</c>, <c>ending_01</c> … <c>ending_05</c>; .wav/.ogg/.mp3 – Unity importiert sie selbst).
    /// Sprechertext, Startzeiten und Aufnahmehinweise: <c>docs/SPRECHERTEXT.md</c>.</item>
    /// <item>Jede Zeile startet zum festen Zeitpunkt der Sequenz, sample-genau per PlayScheduled (im Intro synchron zu
    /// <see cref="AudioManager.IntroTime"/>). Kommt eine Zeile zu spät an die Reihe, setzt sie mit Versatz ein.</item>
    /// <item>Während gesprochen wird, senkt der AudioManager die Musik weich ab (Ducking); Lautstärke = Einstellung „Sprache“.</item>
    /// <item>Optionaler Kino-Klang (<see cref="CinemaSound"/>): dezente Tiefenanhebung, sanfte Kompression, leichte Sättigung
    /// und etwas Raum – er macht eine trockene Aufnahme „größer“, ohne sie zu verfremden.</item>
    /// <item>Fehlen Aufnahmen, bleibt es still (keine Meldung) – die Sequenzen zeigen dann wie bisher nur Untertitel.</item>
    /// </list>
    /// Öffentliche Aufrufe werfen nie Ausnahmen.
    /// </summary>
    public class Narrator : MonoBehaviour
    {
        /// <summary>Eine Sprecherzeile: Dateiname (ohne Endung), Start in Sekunden ab Sequenzbeginn, maximale Dauer, Untertitel.</summary>
        public class Cue { public string File; public float Start, MaxDuration; public string Text; }

        /// <summary>Kino-Klang an/aus (Tiefen, Kompression, Raum). Wirkt sofort, auch während eine Zeile läuft.</summary>
        public static bool CinemaSound = true;

        /// <summary>Unterordner in einem Resources-Ordner, aus dem die Aufnahmen geladen werden.</summary>
        public const string Folder = "Voice/";

        const float DuckAmount = 0.55f;   // Musik während der Sprache auf ≈ 45 % (≈ −7 dB)
        const float Lookahead = 0.35f;     // so früh wird eine Zeile auf der DSP-Uhr vorgemerkt

        static Narrator inst;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        AudioSource src;
        AudioReverbFilter reverb;
        Cue[] cues = new Cue[0];
        AudioClip[] clips = new AudioClip[0];
        int next, current = -1;
        float fade = 1f, fadeSpeed = 4f;
        bool fadingOut;

        // ================================================================== Öffentliche Schnittstelle
        /// <summary>Bereitet eine Sequenz vor (lädt vorhandene Aufnahmen). Bereits laufende Sprache wird beendet.</summary>
        public static void Begin(Cue[] sequence)
        {
            try
            {
                var n = Ensure();
                if (n != null) n.Setup(sequence ?? new Cue[0]);
            }
            catch (Exception e) { Debug.LogWarning("[Erzähler] Begin: " + e.Message); }
        }

        /// <summary>Jedes Bild mit der aktuellen Sequenzzeit aufrufen; startet fällige Zeilen.</summary>
        public static void Tick(float t)
        {
            if (inst == null) return;
            try { inst.DoTick(t); }
            catch (Exception e) { Debug.LogWarning("[Erzähler] Tick: " + e.Message); inst.cues = new Cue[0]; inst.clips = new AudioClip[0]; }
        }

        /// <summary>Beendet die Sprache (weich ausgeblendet) und verwirft ausstehende Zeilen – z. B. beim Überspringen.</summary>
        public static void Stop(float fadeTime = 0.25f)
        {
            if (inst == null) return;
            try
            {
                inst.next = inst.cues.Length;
                inst.cues = new Cue[0];
                inst.clips = new AudioClip[0];
                inst.current = -1;
                if (inst.src != null && inst.src.isPlaying)
                {
                    inst.fadingOut = true;
                    inst.fadeSpeed = 1f / Mathf.Max(0.02f, fadeTime);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Erzähler] Stop: " + e.Message); }
        }

        /// <summary>Untertitel zur Zeit t: Text der Zeile, deren Fenster t enthält (mit Aufnahme: bis kurz nach ihrem Ende), sonst null.</summary>
        public static string SubtitleAt(float t)
        {
            if (inst == null) return null;
            try
            {
                string text = null;
                for (int i = 0; i < inst.cues.Length; i++)
                {
                    var c = inst.cues[i];
                    if (t < c.Start) break;
                    var clip = inst.clips[i];
                    float end = c.Start + (clip != null ? Mathf.Min(clip.length, c.MaxDuration + 1.5f) + 0.4f : c.MaxDuration + 0.5f);
                    if (t < end) text = c.Text;
                }
                return text;
            }
            catch (Exception) { return null; }
        }

        /// <summary>true, wenn für die laufende Sequenz mindestens eine Aufnahme gefunden wurde.</summary>
        public static bool HasRecordings
        {
            get
            {
                if (inst == null) return false;
                foreach (var c in inst.clips) if (c != null) return true;
                return false;
            }
        }

        /// <summary>true, solange eine Zeile hörbar ist (oder unmittelbar bevorsteht).</summary>
        public static bool Speaking { get { return inst != null && inst.src != null && inst.src.isPlaying && !inst.fadingOut; } }

        // ================================================================== Sprechertexte
        // Startzeit und maximale Dauer je Zeile relativ zum Beginn der Einstellung: { Start 1, Dauer 1, Start 2, Dauer 2 }.
        // Die Zeilen fallen in ruhige Momente des Scores und enden vor der Überblendung (0,8 s) am Ende der Einstellung.
        static readonly float[][] IntroSlots =
        {
            new[] { 1.5f, 5.0f, 7.5f, 5.5f },   // 0–14    Skyline
            new[] { 1.5f, 6.5f, 9.0f, 6.0f },   // 14–30   Megastore
            new[] { 1.5f, 6.0f, 8.5f, 6.5f },   // 30–46   Archen
            new[] { 1.5f, 5.0f, 7.5f, 5.5f },   // 46–60   Roboter schalten ab
            new[] { 1.5f, 5.5f, 8.0f, 6.0f },   // 60–75   MIKOs Zuhause
            new[] { 1.5f, 5.5f, 8.0f, 4.5f },   // 75–88   Keimling
            new[] { 0.8f, 4.0f, 6.0f, 5.5f },   // 88–100  Schiff / Titel
        };

        /// <summary>Die 14 Intro-Zeilen (intro_01 … intro_14); Texte aus <see cref="IntroTimeline"/>, Zeiten aus IntroSlots.</summary>
        public static Cue[] IntroCues()
        {
            var list = new List<Cue>();
            var shots = IntroTimeline.Shots;
            for (int s = 0; s < shots.Length; s++)
            {
                var sh = shots[s];
                float len = sh.End - sh.Start;
                float[] slot = s < IntroSlots.Length ? IntroSlots[s] : new[] { 1.5f, len * 0.5f - 2f, len * 0.5f + 0.5f, len * 0.5f - 2f };
                for (int l = 0; l < sh.Lines.Length && l < 2; l++)
                    list.Add(new Cue { File = "intro_" + (list.Count + 1).ToString("00"), Start = sh.Start + slot[l * 2], MaxDuration = slot[l * 2 + 1], Text = sh.Lines[l] });
            }
            return list.ToArray();
        }

        /// <summary>Die fünf Zeilen des Abspanns (ending_01 … ending_05), passend zu den Tafeln des EndingDirector.</summary>
        public static Cue[] EndingCues()
        {
            return new[]
            {
                new Cue { File = "ending_01", Start = 1.5f, MaxDuration = 5.0f, Text = "Und eines Abends leuchteten neue Lichter am Himmel." },
                new Cue { File = "ending_02", Start = 7.0f, MaxDuration = 4.5f, Text = "Die Arche HORIZONT kam nach Hause." },
                new Cue { File = "ending_03", Start = 13.0f, MaxDuration = 5.0f, Text = "Vier Welten. Vier zweite Chancen." },
                new Cue { File = "ending_04", Start = 37.0f, MaxDuration = 3.5f, Text = "Danke, MIKO." },
                new Cue { File = "ending_05", Start = 41.5f, MaxDuration = 6.5f, Text = "Was wir fortgeworfen hatten … hast du uns zurückgegeben." },
            };
        }

        // ================================================================== Aufbau
        static Narrator Ensure()
        {
            if (inst != null) return inst;
            var go = new GameObject("Erzaehler");
            var parent = AudioManager.VoiceParent;
            if (parent != null) go.transform.SetParent(parent, false);
            // Reihenfolge der Komponenten = Reihenfolge der Filterkette: Quelle → Klangformung (OnAudioFilterRead) → Raum
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.priority = 0;
            s.spatialBlend = 0f;
            s.bypassReverbZones = true;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            var n = go.AddComponent<Narrator>();
            n.src = s;
            n.InitFx();
            try
            {
                var r = go.AddComponent<AudioReverbFilter>();
                r.reverbPreset = AudioReverbPreset.User;
                r.dryLevel = 0f;
                r.room = -1700f;          // mB: Raum dezent unter dem Direktsignal
                r.roomHF = -1300f;
                r.roomLF = 0f;
                r.decayTime = 1.25f;
                r.decayHFRatio = 0.55f;
                r.reflectionsLevel = -1500f;
                r.reflectionsDelay = 0.012f;
                r.reverbLevel = -1000f;
                r.reverbDelay = 0.028f;
                r.hfReference = 5000f;
                r.lfReference = 250f;
                r.diffusion = 90f;
                r.density = 85f;
                n.reverb = r;
            }
            catch (Exception e) { Debug.LogWarning("[Erzähler] Hall nicht verfügbar: " + e.Message); }
            inst = n;
            return n;
        }

        void OnDestroy() { if (inst == this) inst = null; }

        static AudioClip Load(string file)
        {
            AudioClip c;
            if (cache.TryGetValue(file, out c) && (c != null || !Application.isEditor)) return c;
            try { c = Resources.Load<AudioClip>(Folder + file); }
            catch (Exception) { c = null; }
            if (c != null && c.loadState == AudioDataLoadState.Unloaded) { try { c.LoadAudioData(); } catch (Exception) { } }
            cache[file] = c;
            return c;
        }

        void Setup(Cue[] sequence)
        {
            if (src != null) src.Stop();
            fadingOut = false; fade = 1f;
            cues = sequence;
            clips = new AudioClip[sequence.Length];
            for (int i = 0; i < sequence.Length; i++) clips[i] = sequence[i] != null && !string.IsNullOrEmpty(sequence[i].File) ? Load(sequence[i].File) : null;
            next = 0; current = -1;
        }

        // ================================================================== Ablauf
        void DoTick(float t)
        {
            if (src == null || cues.Length == 0) return;
            while (next < cues.Length)
            {
                var c = cues[next];
                var clip = clips[next];
                if (c == null || clip == null) { if (c == null || t >= c.Start) { next++; continue; } break; }
                float delay = c.Start - t;
                if (delay > Lookahead) break;
                bool busy = src.isPlaying && !fadingOut;
                if (delay > 0f)
                {
                    if (busy) break; // vorige Zeile läuft noch: erst zum Startzeitpunkt übernehmen
                    src.Stop();
                    src.clip = clip;
                    src.time = 0f;
                    src.PlayScheduled(AudioSettings.dspTime + delay);
                }
                else
                {
                    float late = -delay;
                    if (late > clip.length - 0.3f) { next++; continue; } // verpasst (z. B. nach langer Ladepause)
                    src.Stop();
                    src.clip = clip;
                    src.time = 0f;
                    if (late > 0.04f) { try { src.time = Mathf.Min(late, clip.length - 0.3f); } catch (Exception) { } }
                    src.Play();
                }
                fadingOut = false; fade = 1f;
                current = next;
                next++;
                break;
            }
        }

        void Update()
        {
            if (src == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (fadingOut)
            {
                fade = Mathf.MoveTowards(fade, 0f, dt * fadeSpeed);
                if (fade <= 0f) { src.Stop(); src.clip = null; fadingOut = false; fade = 1f; }
            }
            src.volume = Mathf.Clamp01(AudioManager.VoiceGain * fade);
            fxOn = CinemaSound;
            if (reverb != null && reverb.enabled != CinemaSound) reverb.enabled = CinemaSound;
            if (src.isPlaying && !fadingOut) AudioManager.DuckMusic(DuckAmount);
        }

        // ================================================================== Kino-Klang (Audio-Thread)
        // Tiefen-Anhebung (Low-Shelf +4 dB bei 140 Hz), etwas weniger Schärfe (High-Shelf −2,5 dB ab 7,5 kHz),
        // sanfte Kompression (2,5 : 1 ab −20 dBFS, +3 dB Ausgleich) und eine sehr leichte Sättigung für Wärme.
        volatile bool fxOn = true;
        volatile bool fxReady;
        float[] ls = new float[5], hs = new float[5];
        readonly float[,] lsZ = new float[8, 2], hsZ = new float[8, 2];
        float env, atk, rel;

        void InitFx()
        {
            float sr = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000f;
            ls = Shelf(sr, 140f, 4f, true);
            hs = Shelf(sr, 7500f, -2.5f, false);
            atk = 1f - Mathf.Exp(-1f / (0.004f * sr));
            rel = 1f - Mathf.Exp(-1f / (0.16f * sr));
            fxReady = true;
        }

        /// <summary>Kuhschwanz-Filter nach dem RBJ-Kochbuch (Flankensteilheit S = 1); Rückgabe b0, b1, b2, a1, a2 (normiert).</summary>
        static float[] Shelf(float sr, float f0, float gainDb, bool low)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * Math.Min(f0, sr * 0.45) / sr;
            double cw = Math.Cos(w0), sw = Math.Sin(w0);
            double alpha = sw / 2.0 * Math.Sqrt(2.0); // S = 1: sqrt((A + 1/A)(1/S − 1) + 2) = sqrt(2)
            double sq = 2.0 * Math.Sqrt(A) * alpha;
            double b0, b1, b2, a0, a1, a2;
            if (low)
            {
                b0 = A * ((A + 1) - (A - 1) * cw + sq);
                b1 = 2 * A * ((A - 1) - (A + 1) * cw);
                b2 = A * ((A + 1) - (A - 1) * cw - sq);
                a0 = (A + 1) + (A - 1) * cw + sq;
                a1 = -2 * ((A - 1) + (A + 1) * cw);
                a2 = (A + 1) + (A - 1) * cw - sq;
            }
            else
            {
                b0 = A * ((A + 1) + (A - 1) * cw + sq);
                b1 = -2 * A * ((A - 1) + (A + 1) * cw);
                b2 = A * ((A + 1) + (A - 1) * cw - sq);
                a0 = (A + 1) - (A - 1) * cw + sq;
                a1 = 2 * ((A - 1) - (A + 1) * cw);
                a2 = (A + 1) - (A - 1) * cw - sq;
            }
            return new[] { (float)(b0 / a0), (float)(b1 / a0), (float)(b2 / a0), (float)(a1 / a0), (float)(a2 / a0) };
        }

        static float Biquad(float x, float[] k, float[,] z, int ch)
        {
            // transponierte Direktform II
            float y = k[0] * x + z[ch, 0];
            z[ch, 0] = k[1] * x - k[3] * y + z[ch, 1];
            z[ch, 1] = k[2] * x - k[4] * y;
            return y;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!fxOn || !fxReady || data == null || channels <= 0) return;
            int chs = Math.Min(channels, 8);
            const float thr = 0.1f, ratio = 2.5f, makeup = 1.4f, drive = 1.25f;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float peak = 0f;
                for (int c = 0; c < chs; c++)
                {
                    float y = Biquad(data[i + c], ls, lsZ, c);
                    y = Biquad(y, hs, hsZ, c);
                    data[i + c] = y;
                    float a = y < 0 ? -y : y;
                    if (a > peak) peak = a;
                }
                env += (peak - env) * (peak > env ? atk : rel);
                float g = env > thr ? (float)(thr * Math.Pow(env / thr, 1.0 / ratio) / env) : 1f;
                g *= makeup;
                for (int c = 0; c < chs; c++)
                {
                    float y = data[i + c] * g;
                    data[i + c] = (float)Math.Tanh(y * drive) / drive; // sanfte Sättigung, begrenzt Spitzen weich
                }
            }
            if (float.IsNaN(env) || float.IsInfinity(env)) { env = 0f; Array.Clear(lsZ, 0, lsZ.Length); Array.Clear(hsZ, 0, hsZ.Length); }
        }
    }
}
