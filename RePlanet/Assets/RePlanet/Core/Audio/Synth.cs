using System;
using System.Collections.Generic;
using System.IO;

namespace RePlanet.Core
{
    /// <summary>
    /// Prozedurale Klangerzeugung: alle Soundeffekte und die cinematische Musik von RE:PLANET werden hier berechnet.
    /// Keine Fremd-Samples – vollständig eigene Inhalte (siehe docs/LIZENZEN.md).
    /// </summary>
    public static class Synth
    {
        public const int SfxRate = 44100;
        public const int MusicRate = 22050;

        // ================================================================== Grundbausteine
        static float Sine(double phase) { return (float)Math.Sin(phase * 2 * Math.PI); }

        /// <summary>Bandbegrenzter Sägezahn (PolyBLEP).</summary>
        static float Saw(double phase, double inc)
        {
            double t = phase - Math.Floor(phase);
            double v = 2 * t - 1;
            if (t < inc) { double x = t / inc; v -= x + x - x * x - 1; }
            else if (t > 1 - inc) { double x = (t - 1) / inc; v -= x * x + x + x + 1; }
            return (float)v;
        }

        static float Env(float t, float a, float d, float s, float r, float len)
        {
            if (t < 0) return 0;
            float v;
            if (t < a) v = t / Math.Max(a, 1e-4f);
            else if (t < a + d) v = 1 - (1 - s) * ((t - a) / Math.Max(d, 1e-4f));
            else v = s;
            if (t > len) v *= Math.Max(0, 1 - (t - len) / Math.Max(r, 1e-4f));
            return v;
        }

        static float Freq(float root, float semis) { return root * (float)Math.Pow(2, semis / 12.0); }

        class OnePole
        {
            float z;
            public float Lp(float x, float cutoff, int rate)
            {
                float a = (float)Math.Exp(-2 * Math.PI * cutoff / rate);
                z = x * (1 - a) + z * a;
                return z;
            }
        }

        /// <summary>Zustandsvariablen-Filter (für Formanten/Bandpass).</summary>
        class Svf
        {
            float low, band;
            public float Band(float x, float f, float q, int rate)
            {
                float fc = 2f * (float)Math.Sin(Math.PI * Math.Min(f, rate * 0.24f) / rate);
                low += fc * band;
                float high = x - low - band / q;
                band += fc * high;
                return band;
            }
            public float Low(float x, float f, float q, int rate)
            {
                float fc = 2f * (float)Math.Sin(Math.PI * Math.Min(f, rate * 0.24f) / rate);
                low += fc * band;
                float high = x - low - band / q;
                band += fc * high;
                return low;
            }
        }

        /// <summary>Einfacher Freeverb-artiger Hall für räumliche, cinematische Klangflächen.</summary>
        public static void Reverb(float[] buf, int rate, float mix, float room, bool wrap)
        {
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] alls = { 556, 441, 341 };
            float scale = rate / 44100f;
            var cb = new float[combs.Length][];
            var ci = new int[combs.Length];
            var cf = new float[combs.Length];
            for (int i = 0; i < combs.Length; i++) cb[i] = new float[Math.Max(8, (int)(combs[i] * scale * 1.6f))];
            var ab = new float[alls.Length][];
            var ai = new int[alls.Length];
            for (int i = 0; i < alls.Length; i++) ab[i] = new float[Math.Max(8, (int)(alls[i] * scale * 1.6f))];
            float damp = 0.35f;
            int n = buf.Length;
            var wet = new float[n];
            int passes = wrap ? 2 : 1; // zweiter Durchlauf füllt den Hallanfang für nahtlose Loops
            for (int pass = 0; pass < passes; pass++)
                for (int k = 0; k < n; k++)
                {
                    float x = buf[k] * 0.3f;
                    float o = 0;
                    for (int c = 0; c < cb.Length; c++)
                    {
                        float y = cb[c][ci[c]];
                        cf[c] = y * (1 - damp) + cf[c] * damp;
                        cb[c][ci[c]] = x + cf[c] * room;
                        ci[c] = (ci[c] + 1) % cb[c].Length;
                        o += y;
                    }
                    for (int a = 0; a < ab.Length; a++)
                    {
                        float y = ab[a][ai[a]];
                        ab[a][ai[a]] = o + y * 0.5f;
                        ai[a] = (ai[a] + 1) % ab[a].Length;
                        o = y - o;
                    }
                    if (pass == passes - 1) wet[k] = o;
                }
            for (int k = 0; k < n; k++) buf[k] = buf[k] * (1 - mix * 0.5f) + wet[k] * mix;
        }

        static void Normalize(float[] b, float peak)
        {
            float m = 0;
            foreach (var v in b) m = Math.Max(m, Math.Abs(v));
            if (m < 1e-6f) return;
            float g = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * g * 1.1f) / (float)Math.Tanh(1.1f);
        }

        static void Add(float[] buf, int start, float[] src, float gain, bool wrap)
        {
            for (int i = 0; i < src.Length; i++)
            {
                int k = start + i;
                if (wrap) { k %= buf.Length; if (k < 0) k += buf.Length; }
                else if (k < 0 || k >= buf.Length) continue;
                buf[k] += src[i] * gain;
            }
        }

        static float[] Loopify(float[] b, int fade)
        {
            int n = b.Length;
            fade = Math.Min(fade, n / 4);
            var r = new float[n - fade];
            Array.Copy(b, r, r.Length);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                r[i] = b[i] * t + b[n - fade + i] * (1 - t);
            }
            return r;
        }

        // ================================================================== Instrumente
        /// <summary>Streicherfläche: verstimmte Sägezähne, Tiefpass mit langsamer Bewegung.</summary>
        static float[] Strings(float f, float dur, float attack, float release, int rate, float bright, int seed)
        {
            int n = (int)((dur + release) * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            double[] det = { -0.0045, 0.0, 0.0052 };
            var ph = new double[det.Length];
            for (int v = 0; v < det.Length; v++) ph[v] = rng.Next();
            var lp1 = new OnePole(); var lp2 = new OnePole();
            float lfoP = rng.Next();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                float vib = 1 + 0.0025f * Sine(t * 5.1 + lfoP);
                for (int v = 0; v < det.Length; v++)
                {
                    double inc = f * (1 + det[v]) * vib / rate;
                    ph[v] += inc;
                    s += Saw(ph[v], inc);
                }
                s /= det.Length;
                float cut = (500 + 900 * bright) * (1 + 0.25f * Sine(t * 0.13 + lfoP));
                s = lp2.Lp(lp1.Lp(s, cut, rate), cut * 1.4f, rate);
                o[i] = s * Env(t, attack, 0.5f, 0.85f, release, dur);
            }
            return o;
        }

        /// <summary>Klavier: additive Teiltöne mit leichter Inharmonizität und Hammer-Anschlag.</summary>
        static float[] Piano(float f, float vel, int rate, int seed)
        {
            float dur = 3.2f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            int partials = 9;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                for (int k = 1; k <= partials; k++)
                {
                    float fk = f * k * (1 + 0.0004f * k * k);
                    if (fk > rate * 0.45f) break;
                    float amp = (float)Math.Pow(k, -1.25) * (float)Math.Exp(-t * (1.1f + 0.55f * k));
                    s += amp * Sine(fk * t);
                }
                float hammer = t < 0.012f ? (rng.Next() * 2 - 1) * (1 - t / 0.012f) * 0.25f : 0;
                o[i] = (s + hammer) * vel * Math.Min(1f, t * 400f);
            }
            return o;
        }

        /// <summary>Chor „Aah“: Sägezahn durch drei Formantfilter, Vibrato, mehrere Stimmen.</summary>
        static float[] Choir(float f, float dur, int rate, int seed)
        {
            float release = 2.5f;
            int n = (int)((dur + release) * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            float[] formF = { 730, 1090, 2440 };
            float[] formA = { 1f, 0.5f, 0.25f };
            int voices = 3;
            for (int v = 0; v < voices; v++)
            {
                var filt = new Svf[3];
                for (int k = 0; k < 3; k++) filt[k] = new Svf();
                double ph = rng.Next();
                float det = 1 + (v - 1) * 0.004f;
                float vp = rng.Next() * 6f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float vib = 1 + 0.006f * Sine(t * (4.8 + v * 0.3) + vp) * Math.Min(1f, t / 1.5f);
                    double inc = f * det * vib / rate;
                    ph += inc;
                    float src = Saw(ph, inc);
                    float s = 0;
                    for (int k = 0; k < 3; k++) s += filt[k].Band(src, formF[k], 8f, rate) * formA[k];
                    o[i] += s * Env(t, 1.6f, 0.5f, 0.9f, release, dur) / voices;
                }
            }
            return o;
        }

        /// <summary>Glocke/Celesta per FM-Synthese.</summary>
        static float[] Bell(float f, float vel, int rate, float ratio = 3.5f, float dur = 3f)
        {
            int n = (int)(dur * rate);
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float idx = 2.2f * (float)Math.Exp(-t * 2.5f);
                float m = Sine(f * ratio * t) * idx;
                o[i] = (float)Math.Sin(2 * Math.PI * f * t + m) * (float)Math.Exp(-t * 1.6f) * vel * Math.Min(1f, t * 800f);
            }
            return o;
        }

        static float[] Bass(float f, float dur, int rate)
        {
            float release = 1.5f;
            int n = (int)((dur + release) * rate);
            var o = new float[n];
            var lp = new OnePole();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = Sine(f * t) + 0.35f * Sine(2 * f * t) + 0.12f * Sine(3 * f * t);
                s = (float)Math.Tanh(s * 1.2f);
                o[i] = lp.Lp(s, 400, rate) * Env(t, 0.6f, 0.4f, 0.8f, release, dur);
            }
            return o;
        }

        /// <summary>Pauke/Boom für cinematische Akzente.</summary>
        static float[] Timpani(float f, float vel, int rate, int seed)
        {
            int n = (int)(2.5f * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var lp = new OnePole();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float ff = f * (1 + 0.5f * (float)Math.Exp(-t * 18));
                ph += ff / rate;
                float body = Sine(ph) * (float)Math.Exp(-t * 2.2f);
                float noise = lp.Lp(rng.Next() * 2 - 1, 900, rate) * (float)Math.Exp(-t * 30);
                o[i] = (body + noise * 0.6f) * vel;
            }
            return o;
        }

        // ================================================================== Musik
        public class Chord { public int Root; public int Third; public bool Add9; public Chord(int r, int th, bool add9 = false) { Root = r; Third = th; Add9 = add9; } }

        static Chord C(int root, char q, bool add9 = false) { return new Chord(root, q == 'M' ? 4 : q == 'm' ? 3 : 5, add9); }

        public static Chord[] Progression(string id)
        {
            switch (id)
            {
                case "pyra": return new[] { C(0, 'm'), C(10, 'M'), C(8, 'M'), C(10, 'M'), C(0, 'm'), C(5, 'm'), C(8, 'M', true), C(7, 'M') };
                case "pelagia": return new[] { C(0, 'M', true), C(2, 'M'), C(0, 'M'), C(2, 'M'), C(9, 'm'), C(7, 'M'), C(4, 'm'), C(2, 'M', true) };
                case "nivalis": return new[] { C(0, 'm', true), C(8, 'M'), C(3, 'M'), C(10, 'M'), C(0, 'm'), C(8, 'M', true), C(5, 'm'), C(7, 's') };
                case "menu": return new[] { C(0, 'M', true), C(9, 'm'), C(5, 'M', true), C(7, 's'), C(0, 'M'), C(4, 'm'), C(5, 'M', true), C(7, 'M') };
                default: return new[] { C(0, 'M', true), C(7, 'M'), C(9, 'm'), C(5, 'M'), C(0, 'M'), C(4, 'm'), C(5, 'M', true), C(7, 's') };
            }
        }

        public static float RootFor(string id)
        {
            if (GameData.Planets.ContainsKey(id)) return GameData.Planets[id].MusicRoot;
            return 220f;
        }

        /// <summary>Stimmführung: Akkordtöne möglichst nah an der vorherigen Lage (−3 … +16 Halbtöne).</summary>
        static int[] Voice(Chord c, int[] prev)
        {
            var tones = new List<int> { c.Root, c.Root + c.Third, c.Root + 7, c.Add9 ? c.Root + 14 : c.Root + 12 };
            var res = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int t = tones[i];
                while (t < -3) t += 12;
                while (t > 16) t -= 12;
                if (prev != null)
                {
                    int best = t; int bd = 999;
                    for (int o = -12; o <= 12; o += 12)
                    {
                        int cand = t + o;
                        if (cand < -5 || cand > 19) continue;
                        int d = Math.Abs(cand - prev[i]);
                        if (d < bd) { bd = d; best = cand; }
                    }
                    t = best;
                }
                res[i] = t;
            }
            Array.Sort(res);
            return res;
        }

        public static readonly string[] StemNames = { "pad", "piano", "bass", "choir", "bells", "perc" };

        public class MusicSet
        {
            public string Id;
            public int Rate;
            public float Length;
            public Dictionary<string, float[]> Stems = new Dictionary<string, float[]>();
        }

        /// <summary>Erzeugt die sechs Musikspuren eines Planeten (32-s-Loop, 8 Akkorde à 4 s), nahtlos loopbar.</summary>
        public static MusicSet Music(string id)
        {
            int rate = MusicRate;
            float chordLen = 4f;
            var prog = Progression(id);
            float length = prog.Length * chordLen;
            int n = (int)(length * rate);
            float root = RootFor(id);
            int[] scale = GameData.Planets.ContainsKey(id) ? GameData.Planets[id].MusicScale : new[] { 0, 2, 4, 7, 9 };
            var set = new MusicSet { Id = id, Rate = rate, Length = length };
            foreach (var s in StemNames) set.Stems[s] = new float[n];
            int[] prev = null;
            var rng = new Rng((int)Hash.Fnv1a(id));
            float bright = id == "nivalis" ? 0.9f : id == "pelagia" ? 0.7f : id == "pyra" ? 0.45f : 0.6f;
            for (int ci = 0; ci < prog.Length; ci++)
            {
                var ch = prog[ci];
                var v = Voice(ch, prev);
                prev = v;
                int start = (int)(ci * chordLen * rate);
                // Streicher
                foreach (var t in v)
                    Add(set.Stems["pad"], start, Strings(Freq(root, t), chordLen, 1.4f, 2.2f, rate, bright, ci * 31 + t), 0.22f, true);
                // Bass
                int br = ch.Root; while (br > 6) br -= 12;
                Add(set.Stems["bass"], start, Bass(Freq(root, br - 12), chordLen, rate), 0.5f, true);
                // Klavier-Arpeggio (Achtel bei 60 BPM)
                int[] pat = ci % 2 == 0 ? new[] { 0, 1, 2, 3, 2, 1, 2, 3 } : new[] { 0, 2, 1, 3, 2, 3, 1, 2 };
                for (int k = 0; k < 8; k++)
                {
                    if (k == 7 && ci % 4 == 3) continue;
                    float vel = 0.35f + 0.15f * (k % 4 == 0 ? 1 : 0) + rng.Range(-0.05f, 0.05f);
                    int note = v[pat[k]] + 12;
                    Add(set.Stems["piano"], start + (int)(k * 0.5f * rate), Piano(Freq(root, note), vel, rate, ci * 8 + k), 0.5f, true);
                }
                // Chor auf den oberen Tönen
                Add(set.Stems["choir"], start, Choir(Freq(root, v[2] + 12), chordLen, rate, ci * 5), 0.35f, true);
                Add(set.Stems["choir"], start, Choir(Freq(root, v[3] + 12), chordLen, rate, ci * 5 + 1), 0.28f, true);
                // Glocken-Melodie aus der Skala
                int count = 1 + (ci % 2);
                for (int k = 0; k < count; k++)
                {
                    int deg = scale[rng.Range(0, scale.Length)];
                    float when = k == 0 ? 0.5f : 2.5f + rng.Range(0f, 0.5f);
                    Add(set.Stems["bells"], start + (int)(when * rate), Bell(Freq(root, deg + 24), 0.35f, rate), 0.5f, true);
                }
                // Pauke auf Takt 1 und 5, leiser Puls sonst
                if (ci % 4 == 0) Add(set.Stems["perc"], start, Timpani(Freq(root, br - 24), 0.9f, rate, ci), 0.8f, true);
                else Add(set.Stems["perc"], start + (int)(2f * rate), Timpani(Freq(root, br - 24), 0.35f, rate, ci + 50), 0.6f, true);
            }
            float[] rev = { 0.45f, 0.35f, 0.15f, 0.55f, 0.6f, 0.4f };
            for (int i = 0; i < StemNames.Length; i++)
            {
                var b = set.Stems[StemNames[i]];
                Reverb(b, rate, rev[i], 0.86f, true);
                Normalize(b, i == 0 ? 0.55f : 0.5f);
            }
            return set;
        }

        /// <summary>Cinematischer Intro-Score (~100 s), passend zu den Einstellungen der Zwischensequenz.</summary>
        public static float[] IntroScore(out int rate, out float length)
        {
            rate = MusicRate;
            length = IntroTimeline.Total;
            int n = (int)((length + 4) * rate);
            var o = new float[n];
            float root = 220f;
            // Abschnitte (siehe IntroTimeline): Moll-Melancholie → Stille → warme Wendung → Aufbruch in Dur
            var sad = new[] { C(9, 'm'), C(5, 'M'), C(0, 'M'), C(7, 'M') };      // vi IV I V
            var hope = new[] { C(0, 'M', true), C(7, 'M'), C(9, 'm'), C(5, 'M', true) };
            int[] prev = null;
            // 0–14 s: einsames Klavier + tiefer Bordun
            Add(o, 0, Bass(Freq(root, -12 + 9 - 12), 14f, rate), 0.25f, false);
            int[] motif = { 21, 19, 16, 14, 16, 12, 14, 9 };
            for (int k = 0; k < motif.Length; k++) Add(o, (int)((1.0f + k * 1.5f) * rate), Piano(Freq(root, motif[k]), 0.5f, rate, k), 0.6f, false);
            // 14–46 s: Streicher (Moll), ab 30 s Chor (Archen starten)
            for (int c = 0; c < 8; c++)
            {
                var ch = sad[c % 4];
                var v = Voice(ch, prev); prev = v;
                float t0 = 14f + c * 4f;
                float amp = 0.12f + 0.03f * c;
                foreach (var t in v) Add(o, (int)(t0 * rate), Strings(Freq(root, t), 4f, c == 0 ? 3f : 1.2f, 2.5f, rate, 0.5f, c * 7 + t), amp, false);
                int br = ch.Root; while (br > 6) br -= 12;
                Add(o, (int)(t0 * rate), Bass(Freq(root, br - 12), 4f, rate), 0.35f, false);
                if (t0 >= 30f)
                {
                    Add(o, (int)(t0 * rate), Choir(Freq(root, v[2] + 12), 4f, rate, c), 0.25f, false);
                    Add(o, (int)(t0 * rate), Choir(Freq(root, v[3] + 12), 4f, rate, c + 9), 0.2f, false);
                }
                if (c == 4) Add(o, (int)(t0 * rate), Timpani(Freq(root, -24), 1f, rate, 3), 0.8f, false);
            }
            // 46–60 s: Roboter schalten ab – einzelne Töne, Leere
            int[] fall = { 16, 14, 12, 9, 7 };
            for (int k = 0; k < fall.Length; k++) Add(o, (int)((47f + k * 2.6f) * rate), Piano(Freq(root, fall[k]), 0.35f - k * 0.04f, rate, 40 + k), 0.6f, false);
            Add(o, (int)(46f * rate), Strings(Freq(root, -3), 13f, 3f, 3f, rate, 0.2f, 99), 0.1f, false);
            // 60–75 s: MIKO erwacht – warme Fläche, Klavier-Arpeggio
            prev = null;
            for (int c = 0; c < 4; c++)
            {
                var v = Voice(hope[c], prev); prev = v;
                float t0 = 60f + c * 3.75f;
                foreach (var t in v) Add(o, (int)(t0 * rate), Strings(Freq(root, t), 3.75f, 1.5f, 2f, rate, 0.55f, 200 + c * 5 + t), 0.13f, false);
                for (int k = 0; k < 6; k++) Add(o, (int)((t0 + k * 0.62f) * rate), Piano(Freq(root, v[k % 4] + 12), 0.3f, rate, 300 + c * 6 + k), 0.45f, false);
            }
            // 75–88 s: Der Keimling – Celesta + Chor-Anschwellen
            int[] sprout = { 24, 28, 31, 35, 36, 31, 28, 33 };
            for (int k = 0; k < sprout.Length; k++) Add(o, (int)((75.5f + k * 1.4f) * rate), Bell(Freq(root, sprout[k]), 0.45f, rate), 0.5f, false);
            for (int c = 0; c < 3; c++)
            {
                var v = Voice(hope[c], prev); prev = v;
                float t0 = 75f + c * 4.3f;
                foreach (var t in v) Add(o, (int)(t0 * rate), Strings(Freq(root, t), 4.3f, 1.5f, 2f, rate, 0.65f, 400 + c * 5 + t), 0.16f + c * 0.03f, false);
                Add(o, (int)(t0 * rate), Choir(Freq(root, v[3] + 12), 4.3f, rate, 50 + c), 0.2f + c * 0.08f, false);
            }
            // 88–100 s: Aufbruch – volles Orchester, Pauke, Titel
            prev = null;
            for (int c = 0; c < 3; c++)
            {
                var v = Voice(hope[(c + 3) % 4], prev); prev = v;
                float t0 = 88f + c * 4f;
                foreach (var t in v) Add(o, (int)(t0 * rate), Strings(Freq(root, t), 4f, 0.6f, 3.5f, rate, 0.8f, 500 + c * 5 + t), 0.2f, false);
                foreach (var t in v) Add(o, (int)(t0 * rate), Strings(Freq(root, t - 12), 4f, 0.6f, 3.5f, rate, 0.6f, 600 + c * 5 + t), 0.12f, false);
                Add(o, (int)(t0 * rate), Choir(Freq(root, v[2] + 12), 4f, rate, 70 + c), 0.3f, false);
                Add(o, (int)(t0 * rate), Choir(Freq(root, v[3] + 12), 4f, rate, 80 + c), 0.28f, false);
                int br = hope[(c + 3) % 4].Root; while (br > 6) br -= 12;
                Add(o, (int)(t0 * rate), Bass(Freq(root, br - 12), 4f, rate), 0.45f, false);
                Add(o, (int)(t0 * rate), Timpani(Freq(root, br - 24), 1f, rate, 90 + c), 0.9f, false);
            }
            Add(o, (int)(96f * rate), Bell(Freq(root, 36), 0.6f, rate, 3.5f, 4f), 0.6f, false);
            Reverb(o, rate, 0.45f, 0.88f, false);
            Normalize(o, 0.85f);
            return o;
        }

        // ================================================================== Soundeffekte
        public static readonly string[] SfxIds =
        {
            "glass", "plastic", "paper", "metal", "magnet_charge", "magnet_wave", "vacuum_loop", "cut_loop", "heat_loop", "filter_loop",
            "bale", "press", "coin", "beep_happy", "beep_sad", "beep_curious", "beep_ok", "beep_error", "ui_click", "ui_hover", "ui_back",
            "build", "repair", "plant", "splash", "bubbles", "ice_crack", "steam", "awaken", "gate_open", "whoosh", "drone_loop",
            "engine_loop", "machine_loop", "wind_loop", "water_loop", "storm_loop", "mission", "lore", "wheels_loop", "mission_new",
            "crane", "unload", "grab", "zone"
        };

        public static float[] Sfx(string id)
        {
            int r = SfxRate;
            var rng = new Rng((int)Hash.Fnv1a(id));
            switch (id)
            {
                case "glass":
                    {
                        var o = new float[(int)(0.6f * r)];
                        float[] starts = { 0f, 0.055f, 0.12f };
                        foreach (var st in starts)
                        {
                            float f0 = rng.Range(2200, 3200);
                            float[] ratios = { 1f, 1.58f, 2.37f, 3.1f };
                            for (int i = 0; i < o.Length; i++)
                            {
                                float t = i / (float)r - st;
                                if (t < 0) continue;
                                float s = 0;
                                foreach (var q in ratios) s += Sine(f0 * q * t) * (float)Math.Exp(-t * (18 + 10 * q)) / q;
                                o[i] += s * 0.5f;
                            }
                        }
                        Normalize(o, 0.8f);
                        return o;
                    }
                case "plastic":
                    {
                        var o = new float[(int)(0.25f * r)];
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            o[i] = Sine(520 * t + 30 * t * t) * (float)Math.Exp(-t * 28) + lp.Lp(rng.Next() * 2 - 1, 2500, r) * (float)Math.Exp(-t * 60) * 0.6f;
                        }
                        Normalize(o, 0.7f);
                        return o;
                    }
                case "paper":
                    {
                        var o = new float[(int)(0.35f * r)];
                        var bp = new Svf();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float am = 0.5f + 0.5f * Sine(t * 37 + rng.Next() * 0.2);
                            o[i] = bp.Band(rng.Next() * 2 - 1, 3500, 1.2f, r) * am * Env(t, 0.02f, 0.1f, 0.6f, 0.15f, 0.2f);
                        }
                        Normalize(o, 0.6f);
                        return o;
                    }
                case "metal":
                case "grab":
                    {
                        float len = id == "grab" ? 0.3f : 0.7f;
                        var o = new float[(int)(len * r)];
                        float[] parts = id == "grab" ? new[] { 900f, 1500f } : new[] { 310f, 812f, 1297f, 1950f, 2630f };
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float s = 0;
                            for (int k = 0; k < parts.Length; k++) s += Sine(parts[k] * t) * (float)Math.Exp(-t * (6 + 5 * k)) / (1 + k * 0.5f);
                            s += (rng.Next() * 2 - 1) * (float)Math.Exp(-t * 80) * 0.7f;
                            o[i] = s;
                        }
                        Normalize(o, id == "grab" ? 0.5f : 0.8f);
                        return o;
                    }
                case "magnet_charge":
                    {
                        var o = new float[(int)(1.2f * r)];
                        double ph = 0;
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float f = 70 + 170 * t * t;
                            double inc = f / r; ph += inc;
                            o[i] = Saw(ph, inc) * 0.4f * (0.7f + 0.3f * Sine(t * (8 + 20 * t))) * Math.Min(1f, t * 8);
                        }
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++) o[i] = lp.Lp(o[i], 1800, r);
                        Normalize(o, 0.6f);
                        return o;
                    }
                case "magnet_wave":
                    {
                        var o = new float[(int)(1.4f * r)];
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float m = Sine(40 * t) * 3 * (float)Math.Exp(-t * 3);
                            o[i] = (float)Math.Sin(2 * Math.PI * (300 - 150 * t) * t + m) * (float)Math.Exp(-t * 2.5f) * 0.6f;
                        }
                        for (int k = 0; k < 10; k++)
                        {
                            int st = (int)((0.15f + k * 0.08f) * r);
                            float f = rng.Range(1400, 2600);
                            for (int i = 0; i < (int)(0.12f * r) && st + i < o.Length; i++)
                            {
                                float t = i / (float)r;
                                o[st + i] += Sine(f * t) * (float)Math.Exp(-t * 45) * 0.35f;
                            }
                        }
                        Reverb(o, r, 0.25f, 0.7f, false);
                        Normalize(o, 0.8f);
                        return o;
                    }
                case "vacuum_loop":
                    {
                        var o = new float[(int)(1.25f * r)];
                        var bp = new Svf(); var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            o[i] = bp.Band(rng.Next() * 2 - 1, 1400 + 200 * Sine(t * 3), 0.9f, r) * 0.6f + Sine(880 * t) * 0.08f + lp.Lp(rng.Next() * 2 - 1, 300, r) * 0.5f;
                        }
                        o = Loopify(o, (int)(0.25f * r));
                        Normalize(o, 0.5f);
                        return o;
                    }
                case "cut_loop":
                    {
                        var o = new float[(int)(1.25f * r)];
                        double ph = 0;
                        var bp = new Svf();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            double inc = (180 + 8 * Sine(t * 13)) / r; ph += inc;
                            float spark = rng.Next() < 0.004f ? 1f : 0f;
                            o[i] = Saw(ph, inc) * 0.35f + bp.Band(rng.Next() * 2 - 1, 5000, 2f, r) * 0.4f + spark * 0.5f;
                        }
                        o = Loopify(o, (int)(0.25f * r));
                        Normalize(o, 0.5f);
                        return o;
                    }
                case "heat_loop":
                case "steam":
                case "filter_loop":
                    {
                        float len = id == "steam" ? 1.2f : 1.25f;
                        var o = new float[(int)(len * r)];
                        var bp = new Svf(); var lp = new OnePole();
                        float center = id == "filter_loop" ? 700 : 3000;
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float n0 = rng.Next() * 2 - 1;
                            o[i] = bp.Band(n0, center + 400 * Sine(t * 2), 1f, r) * 0.6f + lp.Lp(n0, 150, r) * (id == "heat_loop" ? 1.2f : 0.3f);
                            if (id == "filter_loop") o[i] += Sine(t * (120 + 20 * Sine(t * 6))) * 0.15f;
                            if (id == "steam") o[i] *= Env(t, 0.05f, 0.3f, 0.6f, 0.6f, 0.6f);
                        }
                        if (id != "steam") o = Loopify(o, (int)(0.25f * r));
                        Normalize(o, 0.5f);
                        return o;
                    }
                case "bale":
                case "press":
                    {
                        var o = new float[(int)(0.9f * r)];
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float thump = Sine((70 + 60 * (float)Math.Exp(-t * 30)) * t) * (float)Math.Exp(-t * 7);
                            float hiss = id == "press" ? lp.Lp(rng.Next() * 2 - 1, 1500, r) * Env(t, 0.1f, 0.2f, 0.5f, 0.2f, 0.35f) * 0.5f : 0;
                            float clack = t > 0.28f && t < 0.3f ? (rng.Next() * 2 - 1) * 0.8f : 0;
                            o[i] = thump + hiss + clack + (t > 0.3f ? Sine(1800 * t) * (float)Math.Exp(-(t - 0.3f) * 40) * 0.3f : 0);
                        }
                        Normalize(o, 0.85f);
                        return o;
                    }
                case "coin":
                    {
                        var o = new float[(int)(0.9f * r)];
                        float[] notes = { 1318.5f, 1568f, 2093f };
                        for (int k = 0; k < notes.Length; k++) Add(o, (int)(k * 0.07f * r), Bell(notes[k], 0.5f, r, 2f, 0.8f), 1f, false);
                        Normalize(o, 0.7f);
                        return o;
                    }
                case "beep_happy":
                case "beep_sad":
                case "beep_curious":
                case "beep_ok":
                    {
                        float len = id == "beep_ok" ? 0.12f : 0.5f;
                        var o = new float[(int)(len * r)];
                        double ph = 0;
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float f;
                            if (id == "beep_happy") f = t < 0.2f ? 800 + 1600 * t : 1000 + 2500 * (t - 0.22f);
                            else if (id == "beep_sad") f = 750 - 700 * t;
                            else if (id == "beep_curious") f = 650 + 350 * (float)Math.Sin(t * 12);
                            else f = 1150;
                            ph += f / r;
                            float gap = (id == "beep_happy" && t > 0.19f && t < 0.23f) ? 0 : 1;
                            o[i] = (Sine(ph) * 0.8f + Sine(ph * 2) * 0.15f) * gap * Env(t, 0.01f, 0.05f, 0.8f, 0.08f, len - 0.08f);
                        }
                        Normalize(o, 0.55f);
                        return o;
                    }
                case "beep_error":
                    {
                        var o = new float[(int)(0.35f * r)];
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float on = (t < 0.12f || (t > 0.18f && t < 0.3f)) ? 1 : 0;
                            o[i] = (Sine(290 * t) > 0 ? 0.5f : -0.5f) * on * 0.6f;
                        }
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++) o[i] = lp.Lp(o[i], 2000, r);
                        Normalize(o, 0.45f);
                        return o;
                    }
                case "ui_click":
                case "ui_hover":
                case "ui_back":
                    {
                        float len = 0.08f;
                        var o = new float[(int)(len * r)];
                        float f = id == "ui_click" ? 1800 : id == "ui_hover" ? 2600 : 1100;
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            o[i] = Sine(f * t - (id == "ui_back" ? 2000 * t * t : 0)) * (float)Math.Exp(-t * 60);
                        }
                        Normalize(o, id == "ui_hover" ? 0.2f : 0.4f);
                        return o;
                    }
                case "build":
                case "repair":
                    {
                        var o = new float[(int)(1.0f * r)];
                        int hits = id == "build" ? 3 : 10;
                        for (int k = 0; k < hits; k++)
                        {
                            float st = id == "build" ? k * 0.22f : k * 0.05f;
                            for (int i = 0; i < (int)(0.2f * r); i++)
                            {
                                int j = (int)(st * r) + i;
                                if (j >= o.Length) break;
                                float t = i / (float)r;
                                o[j] += (id == "build" ? Sine(160 * t) * (float)Math.Exp(-t * 25) + Sine(1240 * t) * (float)Math.Exp(-t * 18) * 0.4f : (rng.Next() * 2 - 1) * (float)Math.Exp(-t * 200)) * 0.8f;
                            }
                        }
                        if (id == "repair") Add(o, (int)(0.62f * r), Bell(1760, 0.5f, r, 2f, 0.4f), 1f, false);
                        Normalize(o, 0.75f);
                        return o;
                    }
                case "plant":
                    {
                        var o = new float[(int)(1.2f * r)];
                        for (int i = 0; i < (int)(0.1f * r); i++) { float t = i / (float)r; o[i] = Sine((200 + 400 * t * 10) * t) * (float)Math.Exp(-t * 40); }
                        float[] notes = { 784f, 988f, 1175f, 1568f };
                        for (int k = 0; k < notes.Length; k++) Add(o, (int)((0.1f + k * 0.09f) * r), Bell(notes[k], 0.35f, r, 2f, 0.9f), 1f, false);
                        Reverb(o, r, 0.3f, 0.75f, false);
                        Normalize(o, 0.7f);
                        return o;
                    }
                case "splash":
                    {
                        var o = new float[(int)(0.8f * r)];
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            o[i] = lp.Lp(rng.Next() * 2 - 1, 3000 * (float)Math.Exp(-t * 3) + 300, r) * Env(t, 0.005f, 0.15f, 0.3f, 0.5f, 0.2f);
                        }
                        Normalize(o, 0.7f);
                        return o;
                    }
                case "bubbles":
                    {
                        var o = new float[(int)(0.9f * r)];
                        for (int k = 0; k < 7; k++)
                        {
                            int st = (int)(rng.Range(0f, 0.7f) * r);
                            float f = rng.Range(500, 1300);
                            for (int i = 0; i < (int)(0.08f * r) && st + i < o.Length; i++)
                            {
                                float t = i / (float)r;
                                o[st + i] += Sine(f * t + 3000 * t * t) * (float)Math.Exp(-t * 50) * 0.4f;
                            }
                        }
                        Normalize(o, 0.5f);
                        return o;
                    }
                case "ice_crack":
                    {
                        var o = new float[(int)(0.7f * r)];
                        for (int k = 0; k < 14; k++)
                        {
                            int st = (int)(rng.Range(0f, 0.5f) * r);
                            float f = rng.Range(2000, 6000);
                            for (int i = 0; i < (int)(0.04f * r) && st + i < o.Length; i++)
                            {
                                float t = i / (float)r;
                                o[st + i] += ((rng.Next() * 2 - 1) * 0.6f + Sine(f * t) * 0.4f) * (float)Math.Exp(-t * 120);
                            }
                        }
                        Normalize(o, 0.6f);
                        return o;
                    }
                case "awaken":
                    {
                        float len = 6f;
                        var o = new float[(int)(len * r / 2)];
                        int rr = r / 2;
                        float root = 261.6f;
                        int[] chord = { 0, 4, 7, 11, 14 };
                        foreach (var c in chord) Add(o, 0, Strings(Freq(root, c), 3.5f, 1.6f, 2.2f, rr, 0.8f, c), 0.18f, false);
                        for (int k = 0; k < 8; k++) Add(o, (int)((1.2f + k * 0.28f) * rr), Bell(Freq(root, chord[k % 5] + 24), 0.35f, rr), 0.5f, false);
                        Add(o, 0, Timpani(65f, 0.9f, rr, 1), 0.8f, false);
                        Reverb(o, rr, 0.45f, 0.86f, false);
                        Normalize(o, 0.85f);
                        return Upsample(o);
                    }
                case "gate_open":
                    {
                        var o = new float[(int)(2.5f * r)];
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float groan = Saw(t * (60 + 20 * Sine(t * 1.3)), 60f / r) * 0.4f;
                            o[i] = lp.Lp(groan + (rng.Next() * 2 - 1) * 0.3f, 500, r) * Env(t, 0.3f, 0.5f, 0.6f, 0.8f, 1.4f);
                        }
                        Add(o, (int)(1.3f * r), Bell(523.25f, 0.4f, r, 2f, 1.2f), 1f, false);
                        Add(o, (int)(1.3f * r), Bell(659.25f, 0.3f, r, 2f, 1.2f), 1f, false);
                        Add(o, (int)(1.3f * r), Bell(783.99f, 0.3f, r, 2f, 1.2f), 1f, false);
                        Normalize(o, 0.8f);
                        return o;
                    }
                case "whoosh":
                    {
                        var o = new float[(int)(2.0f * r)];
                        var bp = new Svf();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            o[i] = bp.Band(rng.Next() * 2 - 1, 200 + 3000 * (float)Math.Sin(Math.PI * t / 2.0), 2f, r) * (float)Math.Sin(Math.PI * t / 2.0);
                        }
                        Normalize(o, 0.7f);
                        return o;
                    }
                case "drone_loop":
                case "engine_loop":
                case "wheels_loop":
                    {
                        var o = new float[(int)(1.25f * r)];
                        var lp = new OnePole();
                        double ph = 0;
                        float f0 = id == "drone_loop" ? 190 : id == "engine_loop" ? 55 : 35;
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            double inc = (f0 * (1 + 0.03f * Sine(t * 7))) / r; ph += inc;
                            float s = Saw(ph, inc) * 0.5f + (rng.Next() * 2 - 1) * (id == "wheels_loop" ? 0.6f : 0.15f);
                            o[i] = lp.Lp(s, id == "drone_loop" ? 2500 : 700, r);
                        }
                        o = Loopify(o, (int)(0.25f * r));
                        Normalize(o, 0.45f);
                        return o;
                    }
                case "machine_loop":
                    {
                        var o = new float[(int)(2.25f * r)];
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float hum = Sine(100 * t) * 0.4f + Sine(150 * t) * 0.2f;
                            float clank = (t % 0.5f) < 0.03f ? (rng.Next() * 2 - 1) * (float)Math.Exp(-(t % 0.5f) * 60) : 0;
                            o[i] = lp.Lp(hum + clank * 0.8f, 1500, r);
                        }
                        o = Loopify(o, (int)(0.25f * r));
                        Normalize(o, 0.4f);
                        return o;
                    }
                case "wind_loop":
                case "storm_loop":
                case "water_loop":
                    {
                        var o = new float[(int)(5f * r)];
                        var bp = new Svf(); var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            float n0 = rng.Next() * 2 - 1;
                            if (id == "water_loop")
                            {
                                float wave = 0.5f + 0.5f * Sine(t * 0.25) * Sine(t * 0.61 + 0.3);
                                o[i] = lp.Lp(n0, 600 + 1200 * wave, r) * (0.3f + 0.7f * wave);
                            }
                            else
                            {
                                float gust = 0.5f + 0.5f * Sine(t * (id == "storm_loop" ? 0.7 : 0.2)) * Sine(t * 0.37 + 1);
                                o[i] = bp.Band(n0, 300 + 900 * gust, id == "storm_loop" ? 0.8f : 1.5f, r) * (0.3f + gust) + (id == "storm_loop" ? lp.Lp(n0, 120, r) * 1.5f : 0);
                            }
                        }
                        o = Loopify(o, (int)(1f * r));
                        Normalize(o, 0.5f);
                        return o;
                    }
                case "mission":
                case "zone":
                    {
                        var o = new float[(int)(2.0f * r / 2)];
                        int rr = r / 2;
                        float root = id == "zone" ? 392f : 523.25f;
                        int[] seq = id == "zone" ? new[] { 0, 7, 12 } : new[] { 0, 4, 7, 12 };
                        for (int k = 0; k < seq.Length; k++) Add(o, (int)(k * 0.1f * rr), Piano(Freq(root, seq[k]), 0.5f, rr, k), 0.6f, false);
                        for (int k = 0; k < seq.Length; k++) Add(o, (int)(k * 0.1f * rr), Bell(Freq(root, seq[k] + 12), 0.3f, rr, 2f, 1.5f), 0.4f, false);
                        Reverb(o, rr, 0.3f, 0.8f, false);
                        Normalize(o, 0.75f);
                        return Upsample(o);
                    }
                case "mission_new":
                case "lore":
                    {
                        var o = new float[(int)(1.6f * r)];
                        float[] notes = id == "lore" ? new[] { 659.25f, 830.6f, 987.8f, 1318.5f } : new[] { 880f, 1318.5f };
                        for (int k = 0; k < notes.Length; k++) Add(o, (int)(k * 0.16f * r), Bell(notes[k], 0.35f, r, id == "lore" ? 1.41f : 2f, 1.2f), 1f, false);
                        Reverb(o, r, 0.35f, 0.82f, false);
                        Normalize(o, 0.6f);
                        return o;
                    }
                case "crane":
                    {
                        var o = new float[(int)(1.5f * r)];
                        double ph = 0;
                        var lp = new OnePole();
                        for (int i = 0; i < o.Length; i++)
                        {
                            float t = i / (float)r;
                            double inc = (90 + 30 * t) / r; ph += inc;
                            float chain = (t * 14 % 1) < 0.1f ? (rng.Next() * 2 - 1) * 0.5f : 0;
                            o[i] = lp.Lp(Saw(ph, inc) * 0.5f + chain, 1200, r) * Env(t, 0.1f, 0.2f, 0.8f, 0.3f, 1.2f);
                        }
                        Normalize(o, 0.6f);
                        return o;
                    }
                case "unload":
                    {
                        var o = new float[(int)(1.0f * r)];
                        for (int k = 0; k < 8; k++)
                        {
                            int st = (int)(k * 0.08f * r);
                            float f = rng.Range(200, 900);
                            for (int i = 0; i < (int)(0.15f * r) && st + i < o.Length; i++)
                            {
                                float t = i / (float)r;
                                o[st + i] += (Sine(f * t) * 0.5f + (rng.Next() * 2 - 1) * 0.5f) * (float)Math.Exp(-t * 35);
                            }
                        }
                        Normalize(o, 0.6f);
                        return o;
                    }
            }
            return new float[64];
        }

        static float[] Upsample(float[] half)
        {
            var o = new float[half.Length * 2];
            for (int i = 0; i < half.Length; i++)
            {
                float a = half[i], b = i + 1 < half.Length ? half[i + 1] : a;
                o[2 * i] = a; o[2 * i + 1] = (a + b) * 0.5f;
            }
            return o;
        }

        // ================================================================== Export (Tests, Dokumentation)
        public static void WriteWav(string path, float[] data, int rate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var v in data) w.Write((short)(M.Clamp(v, -1f, 1f) * 32000));
            }
        }
    }

    /// <summary>Zeitplan der Intro-Zwischensequenz (Musik und Bild laufen synchron).</summary>
    public static class IntroTimeline
    {
        public const float Total = 100f;
        public class Shot { public float Start, End; public string Id; public string[] Lines; }

        /// <summary>
        /// Die Handlung folgt der Grundidee von WALL·E (vermüllte Erde, ein Konzern, Flucht auf Archen,
        /// ein einzelner Roboter arbeitet weiter und findet einen Keimling) – mit eigenen Figuren, Namen und Bildern.
        /// </summary>
        public static readonly Shot[] Shots =
        {
            new Shot { Start = 0, End = 14, Id = "skyline", Lines = new[] { "Erde, im Jahr 2100.", "Die Menschen hatten alles. Und sie warfen alles weg." } },
            new Shot { Start = 14, End = 30, Id = "megastore", Lines = new[] { "Der Konzern KONSUMA versprach: „Alles. Sofort. Immer neu.“", "Bis der Müll höher war als die Hochhäuser." } },
            new Shot { Start = 30, End = 46, Id = "arks", Lines = new[] { "Die Menschen stiegen in die großen Archen. „Nur für fünf Jahre“, hieß es.", "Auf vier Welten blieben Recyclingroboter zurück, um aufzuräumen." } },
            new Shot { Start = 46, End = 60, Id = "shutdown", Lines = new[] { "Aus fünf Jahren wurden fünfzig.", "Einer nach dem anderen gaben die Roboter auf." } },
            new Shot { Start = 60, End = 75, Id = "home", Lines = new[] { "Nur einer arbeitete weiter.", "MIKO. Jeden Morgen. Würfel für Würfel." } },
            new Shot { Start = 75, End = 88, Id = "sprout", Lines = new[] { "Und eines Tages fand MIKO etwas, das es seit Jahrzehnten nicht mehr gab.", "Einen Keimling." } },
            new Shot { Start = 88, End = 100, Id = "ship", Lines = new[] { "Ein altes Signal erwachte: PROGRAMM ZWEITE CHANCE.", "Wenn das Leben zurückkehrt, kehren auch wir zurück." } },
        };
    }
}
