using System;
using System.Collections.Generic;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// All GLASSCORE audio is synthesised at startup ("glass ASMR" without asset files): layered sine
    /// partials for glass tinks, filtered noise for cracks, stochastic shard showers for shatters, and a
    /// generative ambient music loop. Volumes follow Master × SFX / Music from the settings.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public const string TinkHigh = "SFX_Glass_Tink_High";
        public const string CrackSharp = "SFX_Glass_Crack_Sharp";
        public const string Shatter = "SFX_Glass_Shatter";
        public const string ShatterSmall = "SFX_Glass_Shatter_Small";
        public const string Crumble = "SFX_Glass_Crumble";
        public const string CrackSmall = "SFX_Glass_Crack_Small";
        public const string FireLight = "SFX_Fire_Light";
        public const string FireHeavy = "SFX_Fire_Heavy";
        public const string FireBoulder = "SFX_Fire_Boulder";
        public const string FireScatter = "SFX_Fire_Scatter";
        public const string Impact = "SFX_Stone_Impact";
        public const string Jump = "SFX_Jump";
        public const string Anchor = "SFX_Anchor";
        public const string Hit = "SFX_Hitmarker";
        public const string Knockout = "SFX_Knockout";
        public const string Beep = "SFX_Countdown_Beep";
        public const string Go = "SFX_Countdown_Go";
        public const string Wave = "SFX_Pressure_Wave";
        public const string Siren = "SFX_Cascade_Siren";

        private const int Rate = 44100;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> _pool2D = new List<AudioSource>();
        private readonly List<AudioSource> _pool3D = new List<AudioSource>();
        private AudioSource _music;
        private GameSettings _settings;
        private int _next2D, _next3D;

        public static AudioService Instance { get; private set; }

        public void Init(GameSettings settings)
        {
            Instance = this;
            _settings = settings;
            var rng = new System.Random(1904);

            _clips[TinkHigh] = Make(TinkHigh, 0.35f, (t, i) => Tink(t, 4200f, 0.09f) * 0.6f);
            _clips[CrackSharp] = Make(CrackSharp, 0.45f, CrackSynth(rng, 0.9f));
            _clips[CrackSmall] = Make(CrackSmall, 0.25f, CrackSynth(rng, 0.5f));
            _clips[Shatter] = Make(Shatter, 1.8f, ShatterSynth(rng, 70, 1.0f));
            _clips[ShatterSmall] = Make(ShatterSmall, 1.0f, ShatterSynth(rng, 30, 0.6f));
            _clips[Crumble] = Make(Crumble, 1.4f, CrumbleSynth(rng));
            _clips[FireLight] = Make(FireLight, 0.25f, WhooshSynth(rng, 0.08f, 160f, 0.5f));
            _clips[FireHeavy] = Make(FireHeavy, 0.45f, WhooshSynth(rng, 0.15f, 90f, 0.8f));
            _clips[FireBoulder] = Make(FireBoulder, 0.8f, WhooshSynth(rng, 0.3f, 55f, 1f));
            _clips[FireScatter] = Make(FireScatter, 0.35f, WhooshSynth(rng, 0.1f, 120f, 0.9f));
            _clips[Impact] = Make(Impact, 0.5f, (t, i) => (float)(Math.Sin(2 * Math.PI * 85 * t) * Math.Exp(-t * 14)) * 0.8f + Tink(t, 2600f, 0.05f) * 0.3f);
            _clips[Jump] = Make(Jump, 0.25f, WhooshSynth(rng, 0.12f, 300f, 0.25f));
            _clips[Anchor] = Make(Anchor, 0.5f, (t, i) => (float)(Math.Sin(2 * Math.PI * (140 - 90 * t) * t) * Math.Exp(-t * 7)) * 0.9f);
            _clips[Hit] = Make(Hit, 0.1f, (t, i) => (float)(Math.Sin(2 * Math.PI * 1900 * t) * Math.Exp(-t * 50)) * 0.5f);
            _clips[Knockout] = Make(Knockout, 1.0f, (t, i) => Arp(t, new[] { 1760f, 1318f, 1047f, 880f }, 0.12f));
            _clips[Beep] = Make(Beep, 0.2f, (t, i) => (float)(Math.Sin(2 * Math.PI * 880 * t) * Math.Min(1, (0.2 - t) * 30)) * 0.4f);
            _clips[Go] = Make(Go, 0.7f, (t, i) => (float)((Math.Sin(2 * Math.PI * 880 * t) + Math.Sin(2 * Math.PI * 1320 * t) + Math.Sin(2 * Math.PI * 1760 * t)) * Math.Exp(-t * 4)) * 0.25f);
            _clips[Wave] = Make(Wave, 1.6f, (t, i) => (float)(Math.Sin(2 * Math.PI * 38 * t) * Math.Sin(Math.PI * t / 1.6)) * 0.9f);
            _clips[Siren] = Make(Siren, 2.0f, (t, i) => (float)(Math.Sin(2 * Math.PI * (600 + 250 * Math.Sin(2 * Math.PI * 1.5 * t)) * t) * Math.Sin(Math.PI * t / 2.0)) * 0.3f);

            for (int i = 0; i < 10; i++) _pool2D.Add(NewSource(false));
            for (int i = 0; i < 32; i++) _pool3D.Add(NewSource(true));

            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = BuildMusic(rng);
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = settings.MusicGain;
            _music.Play();
        }

        private AudioSource NewSource(bool spatial)
        {
            var go = new GameObject(spatial ? "Sfx3D" : "Sfx2D");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3f;
            src.maxDistance = 70f;
            src.dopplerLevel = 0f;
            return src;
        }

        private void Update()
        {
            if (_music != null && _settings != null) _music.volume = _settings.MusicGain * 0.6f;
            AudioListener.volume = 1f;
        }

        public void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(id, out var clip) || _settings == null) return;
            AudioSource src = _pool2D[_next2D++ % _pool2D.Count];
            src.pitch = pitch;
            src.PlayOneShot(clip, volume * _settings.SfxGain);
        }

        public void PlayAt(string id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(id, out var clip) || _settings == null) return;
            AudioSource src = _pool3D[_next3D++ % _pool3D.Count];
            src.transform.position = position;
            src.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            src.PlayOneShot(clip, volume * _settings.SfxGain);
        }

        // ───────────────────────────── synthesis ─────────────────────────────

        private static AudioClip Make(string name, float seconds, Func<double, int, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample(i / (double)Rate, i);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Normalise to -1 dBFS and add a 5 ms fade-out to avoid clicks.
            float gain = peak > 0.89f ? 0.89f / peak : 1f;
            int fade = Math.Min(n, Rate / 200);
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
                if (i >= n - fade) data[i] *= (n - i) / (float)fade;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Inharmonic glass partials (ratios of a struck plate) with exponential decay.</summary>
        private static float Tink(double t, float f, float decay)
        {
            if (t < 0) return 0f;
            double s = Math.Sin(2 * Math.PI * f * t) * 1.0 + Math.Sin(2 * Math.PI * f * 1.52 * t) * 0.6 + Math.Sin(2 * Math.PI * f * 2.31 * t) * 0.35;
            return (float)(s * Math.Exp(-t / decay) * Math.Min(1.0, t * 4000));
        }

        private static Func<double, int, float> CrackSynth(System.Random rng, float intensity)
        {
            var noise = Noise(rng, Rate);
            float prev = 0f;
            return (t, i) =>
            {
                float n = noise[i % noise.Length];
                float hp = n - prev; // first-difference high-pass: brittle "snap"
                prev = n;
                double env = Math.Exp(-t * 40);
                float thump = (float)(Math.Sin(2 * Math.PI * 120 * t) * Math.Exp(-t * 30)) * 0.5f;
                return (float)(hp * env * 1.4 * intensity) + thump * intensity + Tink(t - 0.01, 3100f, 0.06f) * 0.35f + Tink(t - 0.03, 5300f, 0.04f) * 0.25f;
            };
        }

        private static Func<double, int, float> ShatterSynth(System.Random rng, int shards, float size)
        {
            var times = new double[shards];
            var freqs = new float[shards];
            var decays = new float[shards];
            var amps = new float[shards];
            for (int k = 0; k < shards; k++)
            {
                double u = rng.NextDouble();
                times[k] = u * u * 1.2 * size;             // dense at the start, sparse tail
                freqs[k] = 1800f + (float)rng.NextDouble() * 7000f;
                decays[k] = 0.02f + (float)rng.NextDouble() * 0.09f;
                amps[k] = 0.25f + (float)rng.NextDouble() * 0.5f;
            }
            var crack = CrackSynth(rng, 1f);
            var noise = Noise(rng, Rate);
            return (t, i) =>
            {
                float s = crack(t, i) * 0.8f;
                for (int k = 0; k < shards; k++)
                {
                    double dt = t - times[k];
                    if (dt < 0 || dt > decays[k] * 6) continue;
                    s += Tink(dt, freqs[k], decays[k]) * amps[k] * 0.35f;
                }
                s += noise[(i * 3) % noise.Length] * (float)Math.Exp(-t * 3.5) * 0.12f;
                return s;
            };
        }

        private static Func<double, int, float> CrumbleSynth(System.Random rng)
        {
            const int clicks = 220;
            var times = new double[clicks];
            var freqs = new float[clicks];
            for (int k = 0; k < clicks; k++)
            {
                times[k] = Math.Pow(rng.NextDouble(), 1.6) * 1.2;
                freqs[k] = 2500f + (float)rng.NextDouble() * 5000f;
            }
            var noise = Noise(rng, Rate);
            return (t, i) =>
            {
                float s = noise[i % noise.Length] * (float)Math.Exp(-t * 2.5) * 0.25f;
                for (int k = 0; k < clicks; k++)
                {
                    double dt = t - times[k];
                    if (dt < 0 || dt > 0.03) continue;
                    s += (float)(Math.Sin(2 * Math.PI * freqs[k] * dt) * Math.Exp(-dt * 250)) * 0.3f;
                }
                return s;
            };
        }

        private static Func<double, int, float> WhooshSynth(System.Random rng, float length, float thumpHz, float weight)
        {
            var noise = Noise(rng, Rate);
            float lp = 0f;
            return (t, i) =>
            {
                lp += (noise[i % noise.Length] - lp) * 0.08f; // one-pole low-pass: air
                double env = Math.Exp(-t / length);
                float thump = (float)(Math.Sin(2 * Math.PI * thumpHz * t * (1 - t)) * Math.Exp(-t * 18)) * weight;
                return (float)(lp * env * 3.0) + thump;
            };
        }

        private static float Arp(double t, float[] notes, float step)
        {
            float s = 0f;
            for (int k = 0; k < notes.Length; k++) s += Tink(t - k * step, notes[k], 0.18f) * 0.4f;
            return s;
        }

        private static float[] Noise(System.Random rng, int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)(rng.NextDouble() * 2 - 1);
            return a;
        }

        /// <summary>16 s generative ambient loop: detuned pad chords (Am–F–C–G) + glassy arpeggio.</summary>
        private static AudioClip BuildMusic(System.Random rng)
        {
            const float seconds = 16f;
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f },
                new[] { 130.81f, 196f, 261.63f },
                new[] { 196f, 246.94f, 293.66f },
            };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                int bar = (int)(t / 4.0) % 4;
                double inBar = t % 4.0;
                double env = Math.Min(1.0, inBar * 2) * Math.Min(1.0, (4.0 - inBar) * 2);
                float pad = 0f;
                foreach (float f in chords[bar])
                {
                    pad += (float)(Math.Sin(2 * Math.PI * f * t) + Math.Sin(2 * Math.PI * f * 1.003 * t) * 0.8 + Math.Sin(2 * Math.PI * f * 0.5 * t) * 0.5);
                }
                lp += (pad - lp) * 0.02f;
                float arp = 0f;
                double stepT = t % 0.25;
                int step = (int)(t / 0.25);
                float note = chords[bar][step % 3] * (step % 8 < 4 ? 4f : 8f);
                arp = Tink(stepT, note, 0.12f) * 0.18f;
                data[i] = (float)(lp * 0.09 * env) + arp;
            }
            var clip = AudioClip.Create("Music_Glasscore_Ambient", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
