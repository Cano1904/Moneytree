using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace Glasscore.Desktop
{
    /// <summary>
    /// Procedurally synthesised audio (same recipes as the Unity AudioService): glass tinks, cracks,
    /// shard showers, tempered crumble, stone launchers and a generative ambient music loop.
    /// </summary>
    public static class Sfx
    {
        public const string TinkHigh = "tink", CrackSharp = "crack", Shatter = "shatter", ShatterSmall = "shatter_small",
            Crumble = "crumble", CrackSmall = "crack_small", FireLight = "fire_light", FireHeavy = "fire_heavy",
            FireBoulder = "fire_boulder", FireScatter = "fire_scatter", Impact = "impact", Jump = "jump", Anchor = "anchor",
            Hit = "hit", Knockout = "ko", Beep = "beep", Go = "go", Wave = "wave", Siren = "siren";

        private const int Rate = 44100;
        private static readonly Dictionary<string, Sound[]> Sounds = new Dictionary<string, Sound[]>();
        private static readonly Dictionary<string, int> Next = new Dictionary<string, int>();
        private static Sound _music;
        private static bool _ready;

        public static float SfxGain = 0.8f;
        public static float MusicGain = 0.4f;

        public static void Init()
        {
            if (!Raylib.IsAudioDeviceReady()) return; // no sound card: play silently
            var rng = new Random(1904);
            Add(TinkHigh, 0.35f, (t, i) => Tink(t, 4200f, 0.09f) * 0.6f);
            Add(CrackSharp, 0.45f, CrackSynth(rng, 0.9f));
            Add(CrackSmall, 0.25f, CrackSynth(rng, 0.5f));
            Add(Shatter, 1.8f, ShatterSynth(rng, 70, 1.0f));
            Add(ShatterSmall, 1.0f, ShatterSynth(rng, 30, 0.6f));
            Add(Crumble, 1.4f, CrumbleSynth(rng));
            Add(FireLight, 0.25f, WhooshSynth(rng, 0.08f, 160f, 0.5f));
            Add(FireHeavy, 0.45f, WhooshSynth(rng, 0.15f, 90f, 0.8f));
            Add(FireBoulder, 0.8f, WhooshSynth(rng, 0.3f, 55f, 1f));
            Add(FireScatter, 0.35f, WhooshSynth(rng, 0.1f, 120f, 0.9f));
            Add(Impact, 0.5f, (t, i) => (float)(Math.Sin(2 * Math.PI * 85 * t) * Math.Exp(-t * 14)) * 0.8f + Tink(t, 2600f, 0.05f) * 0.3f);
            Add(Jump, 0.25f, WhooshSynth(rng, 0.12f, 300f, 0.25f));
            Add(Anchor, 0.5f, (t, i) => (float)(Math.Sin(2 * Math.PI * (140 - 90 * t) * t) * Math.Exp(-t * 7)) * 0.9f);
            Add(Hit, 0.1f, (t, i) => (float)(Math.Sin(2 * Math.PI * 1900 * t) * Math.Exp(-t * 50)) * 0.5f);
            Add(Knockout, 1.0f, (t, i) => Arp(t, new[] { 1760f, 1318f, 1047f, 880f }, 0.12f));
            Add(Beep, 0.2f, (t, i) => (float)(Math.Sin(2 * Math.PI * 880 * t) * Math.Min(1, (0.2 - t) * 30)) * 0.4f);
            Add(Go, 0.7f, (t, i) => (float)((Math.Sin(2 * Math.PI * 880 * t) + Math.Sin(2 * Math.PI * 1320 * t) + Math.Sin(2 * Math.PI * 1760 * t)) * Math.Exp(-t * 4)) * 0.25f);
            Add(Wave, 1.6f, (t, i) => (float)(Math.Sin(2 * Math.PI * 38 * t) * Math.Sin(Math.PI * t / 1.6)) * 0.9f);
            Add(Siren, 2.0f, (t, i) => (float)(Math.Sin(2 * Math.PI * (600 + 250 * Math.Sin(2 * Math.PI * 1.5 * t)) * t) * Math.Sin(Math.PI * t / 2.0)) * 0.3f);
            _music = ToSound(BuildMusic());
            _ready = true;
        }

        public static void Update()
        {
            if (!_ready) return;
            Raylib.SetSoundVolume(_music, MusicGain * 0.6f);
            if (!Raylib.IsSoundPlaying(_music)) Raylib.PlaySound(_music);
        }

        /// <param name="pan">0.5 = centre (raylib: 1 = left, 0 = right).</param>
        public static void Play(string id, float volume = 1f, float pitch = 1f, float pan = 0.5f)
        {
            if (!_ready || !Sounds.TryGetValue(id, out var voices)) return;
            int n = Next.TryGetValue(id, out int k) ? k : 0;
            Next[id] = (n + 1) % voices.Length;
            Sound s = voices[n];
            Raylib.SetSoundVolume(s, Math.Clamp(volume * SfxGain, 0f, 1f));
            Raylib.SetSoundPitch(s, pitch);
            Raylib.SetSoundPan(s, Math.Clamp(pan, 0f, 1f));
            Raylib.PlaySound(s);
        }

        private static void Add(string id, float seconds, Func<double, int, float> f)
        {
            float[] data = Render(seconds, f);
            var voices = new Sound[4]; // polyphony: the same sound can overlap itself
            voices[0] = ToSound(data);
            for (int v = 1; v < voices.Length; v++) voices[v] = Raylib.LoadSoundAlias(voices[0]);
            Sounds[id] = voices;
        }

        private static float[] Render(float seconds, Func<double, int, float> sample)
        {
            int n = (int)Math.Ceiling(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample(i / (double)Rate, i);
                peak = Math.Max(peak, Math.Abs(data[i]));
            }
            float gain = peak > 0.89f ? 0.89f / peak : 1f;
            int fade = Math.Min(n, Rate / 200);
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
                if (i >= n - fade) data[i] *= (n - i) / (float)fade;
            }
            return data;
        }

        private static unsafe Sound ToSound(float[] data)
        {
            IntPtr buffer = Marshal.AllocHGlobal(data.Length * sizeof(short));
            short* p = (short*)buffer;
            for (int i = 0; i < data.Length; i++) p[i] = (short)(Math.Clamp(data[i], -1f, 1f) * 32767f);
            var wave = new Wave { SampleCount = (uint)data.Length, SampleRate = Rate, SampleSize = 16, Channels = 1, Data = (void*)buffer };
            Sound s = Raylib.LoadSoundFromWave(wave); // copies the samples
            Marshal.FreeHGlobal(buffer);
            return s;
        }

        private static float Tink(double t, float f, float decay)
        {
            if (t < 0) return 0f;
            double s = Math.Sin(2 * Math.PI * f * t) + Math.Sin(2 * Math.PI * f * 1.52 * t) * 0.6 + Math.Sin(2 * Math.PI * f * 2.31 * t) * 0.35;
            return (float)(s * Math.Exp(-t / decay) * Math.Min(1.0, t * 4000));
        }

        private static Func<double, int, float> CrackSynth(Random rng, float intensity)
        {
            var noise = Noise(rng, Rate);
            float prev = 0f;
            return (t, i) =>
            {
                float n = noise[i % noise.Length];
                float hp = n - prev;
                prev = n;
                float thump = (float)(Math.Sin(2 * Math.PI * 120 * t) * Math.Exp(-t * 30)) * 0.5f;
                return (float)(hp * Math.Exp(-t * 40) * 1.4 * intensity) + thump * intensity + Tink(t - 0.01, 3100f, 0.06f) * 0.35f + Tink(t - 0.03, 5300f, 0.04f) * 0.25f;
            };
        }

        private static Func<double, int, float> ShatterSynth(Random rng, int shards, float size)
        {
            var times = new double[shards]; var freqs = new float[shards]; var decays = new float[shards]; var amps = new float[shards];
            for (int k = 0; k < shards; k++)
            {
                double u = rng.NextDouble();
                times[k] = u * u * 1.2 * size;
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
                return s + noise[(i * 3) % noise.Length] * (float)Math.Exp(-t * 3.5) * 0.12f;
            };
        }

        private static Func<double, int, float> CrumbleSynth(Random rng)
        {
            const int clicks = 220;
            var times = new double[clicks]; var freqs = new float[clicks];
            for (int k = 0; k < clicks; k++) { times[k] = Math.Pow(rng.NextDouble(), 1.6) * 1.2; freqs[k] = 2500f + (float)rng.NextDouble() * 5000f; }
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

        private static Func<double, int, float> WhooshSynth(Random rng, float length, float thumpHz, float weight)
        {
            var noise = Noise(rng, Rate);
            float lp = 0f;
            return (t, i) =>
            {
                lp += (noise[i % noise.Length] - lp) * 0.08f;
                float thump = (float)(Math.Sin(2 * Math.PI * thumpHz * t * (1 - t)) * Math.Exp(-t * 18)) * weight;
                return (float)(lp * Math.Exp(-t / length) * 3.0) + thump;
            };
        }

        private static float Arp(double t, float[] notes, float step)
        {
            float s = 0f;
            for (int k = 0; k < notes.Length; k++) s += Tink(t - k * step, notes[k], 0.18f) * 0.4f;
            return s;
        }

        private static float[] Noise(Random rng, int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)(rng.NextDouble() * 2 - 1);
            return a;
        }

        private static float[] BuildMusic()
        {
            const float seconds = 16f;
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f },
                new[] { 130.81f, 196f, 261.63f }, new[] { 196f, 246.94f, 293.66f },
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
                    pad += (float)(Math.Sin(2 * Math.PI * f * t) + Math.Sin(2 * Math.PI * f * 1.003 * t) * 0.8 + Math.Sin(2 * Math.PI * f * 0.5 * t) * 0.5);
                lp += (pad - lp) * 0.02f;
                int step = (int)(t / 0.25);
                float note = chords[bar][step % 3] * (step % 8 < 4 ? 4f : 8f);
                data[i] = (float)(lp * 0.09 * env) + Tink(t % 0.25, note, 0.12f) * 0.18f;
            }
            return data;
        }
    }
}
