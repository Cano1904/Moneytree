using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RePlanet.Core
{
    /// <summary>
    /// Prozedurale Klangerzeugung von RE:PLANET: Soundeffekte, Ambience-Loops, die mehrspurige cinematische Musik
    /// (sieben Stems je Planet) und der Intro-Score. Vollständig eigene, berechnete Inhalte – keine Fremd-Samples (siehe docs/AUDIO.md).
    /// Alle Einstiegspunkte sind threadsicher (kein gemeinsamer veränderlicher Zustand) und laufen im AudioManager auf Hintergrund-Threads.
    /// </summary>
    public static partial class Synth
    {
        public const int SfxRate = 44100;
        public const int MusicRate = 22050;
        /// <summary>Stand der Klangerzeugung. Bei jeder hörbaren Änderung erhöhen – der Disk-Cache des AudioManagers hängt daran.</summary>
        public const int Version = 3;

        // ================================================================== Grundbausteine
        const double TwoPi = Math.PI * 2;
        static float Sine(double phase) { return (float)Math.Sin(phase * TwoPi); }

        /// <summary>Bandbegrenzter Sägezahn (PolyBLEP).</summary>
        static float Saw(double phase, double inc)
        {
            double t = phase - Math.Floor(phase);
            return SawW(t, inc);
        }

        /// <summary>PolyBLEP-Sägezahn für eine bereits auf [0,1) gefaltete Phase.</summary>
        static float SawW(double t, double inc)
        {
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

        /// <summary>Schnelle, glatte tanh-Näherung (Sättigung).</summary>
        static float FastTanh(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        /// <summary>Koeffizient eines Einpol-Tiefpasses.</summary>
        static float Pole(float cutoff, int rate) { return (float)Math.Exp(-TwoPi * Math.Max(5f, cutoff) / rate); }

        /// <summary>Frequenzkoeffizient des Zustandsvariablen-Filters (stabil begrenzt).</summary>
        static float SvfC(float f, int rate) { return 2f * (float)Math.Sin(Math.PI * Math.Max(10f, Math.Min(f, rate * 0.16f)) / rate); }

        class OnePole
        {
            float z;
            public float Lp(float x, float cutoff, int rate)
            {
                float a = (float)Math.Exp(-2 * Math.PI * cutoff / rate);
                z = x * (1 - a) + z * a;
                return z;
            }
            public float LpA(float x, float a) { z = x + (z - x) * a; return z; }
        }

        /// <summary>Zustandsvariablen-Filter (für Formanten/Bandpass/Resonanzen).</summary>
        class Svf
        {
            float low, band;
            public float Band(float x, float f, float q, int rate)
            {
                float fc = 2f * (float)Math.Sin(Math.PI * Math.Min(f, rate * 0.2f) / rate);
                return BandC(x, fc, 1f / q);
            }
            public float Low(float x, float f, float q, int rate)
            {
                float fc = 2f * (float)Math.Sin(Math.PI * Math.Min(f, rate * 0.2f) / rate);
                low += fc * band;
                float high = x - low - band / q;
                band += fc * high;
                return low;
            }
            /// <summary>Bandpass mit vorberechnetem Koeffizienten; damp = 1/Güte. Verstärkung in Bandmitte ≈ 1/damp.</summary>
            public float BandC(float x, float fc, float damp)
            {
                low += fc * band;
                float high = x - low - damp * band;
                band += fc * high;
                return band;
            }
        }

        /// <summary>Periodische, glatte Zufallsfunktion (Fourier-Reihe mit ganzzahligen Zyklen je Periode) – dadurch nahtlos loopbar. Effektivwert 0,5.</summary>
        sealed class PNoise
        {
            readonly float[] w, a, p;
            public PNoise(Rng r, float period, float minSec, float maxSec, int terms)
            {
                period = Math.Max(0.5f, period);
                int kMin = Math.Max(1, (int)Math.Round(period / Math.Max(minSec, maxSec)));
                int kMax = Math.Max(kMin + 1, (int)Math.Round(period / Math.Max(0.05f, Math.Min(minSec, maxSec))));
                w = new float[terms]; a = new float[terms]; p = new float[terms];
                double sq = 0;
                for (int i = 0; i < terms; i++)
                {
                    int k = kMin + (int)((kMax - kMin) * Math.Pow(r.Next(), 1.6));
                    w[i] = (float)(TwoPi * k / period);
                    a[i] = 0.35f + 0.65f * r.Next();
                    p[i] = r.Next() * 6.2831853f;
                    sq += a[i] * a[i] * 0.5;
                }
                float g = 0.5f / (float)Math.Sqrt(Math.Max(1e-6, sq));
                for (int i = 0; i < terms; i++) a[i] *= g;
            }
            public float At(float t)
            {
                float s = 0;
                for (int i = 0; i < w.Length; i++) s += a[i] * (float)Math.Sin(w[i] * t + p[i]);
                return s;
            }
        }

        /// <summary>Hall (Freeverb-Struktur: 8 Kammfilter, 4 Allpässe, Vorverzögerung). wrap = zweiter Durchlauf füllt den Hallanfang für nahtlose Loops.</summary>
        public static void Reverb(float[] buf, int rate, float mix, float room, bool wrap)
        {
            if (mix <= 0f || buf.Length < 16) return;
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            int[] alls = { 556, 441, 341, 225 };
            float scale = rate / 44100f * 1.45f;
            var cb = new float[combs.Length][];
            var ci = new int[combs.Length];
            var cf = new float[combs.Length];
            for (int i = 0; i < combs.Length; i++) cb[i] = new float[Math.Max(8, (int)(combs[i] * scale))];
            var ab = new float[alls.Length][];
            var ai = new int[alls.Length];
            for (int i = 0; i < alls.Length; i++) ab[i] = new float[Math.Max(8, (int)(alls[i] * scale))];
            int preLen = Math.Max(1, (int)(0.022f * rate));
            var pre = new float[preLen]; int pi = 0;
            float damp = 0.32f;
            int n = buf.Length;
            var wet = new float[n];
            int passes = wrap ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
                for (int k = 0; k < n; k++)
                {
                    float x = pre[pi]; pre[pi] = buf[k] * 0.2f; pi = pi + 1 == preLen ? 0 : pi + 1;
                    float o = 0;
                    for (int c = 0; c < cb.Length; c++)
                    {
                        var line = cb[c]; int idx = ci[c];
                        float y = line[idx];
                        cf[c] = y * (1 - damp) + cf[c] * damp;
                        line[idx] = x + cf[c] * room;
                        ci[c] = idx + 1 == line.Length ? 0 : idx + 1;
                        o += y;
                    }
                    for (int a = 0; a < ab.Length; a++)
                    {
                        var line = ab[a]; int idx = ai[a];
                        float y = line[idx];
                        line[idx] = o + y * 0.5f;
                        ai[a] = idx + 1 == line.Length ? 0 : idx + 1;
                        o = y - o;
                    }
                    if (pass == passes - 1) wet[k] = o;
                }
            for (int k = 0; k < n; k++) buf[k] = buf[k] * (1 - mix * 0.4f) + wet[k] * mix;
        }

        static void Normalize(float[] b, float peak)
        {
            float m = 0;
            foreach (var v in b) m = Math.Max(m, Math.Abs(v));
            if (m < 1e-6f) return;
            float g = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * g * 1.1f) / (float)Math.Tanh(1.1f);
        }

        /// <summary>Normalisiert auf einen Effektivwert und begrenzt Spitzen weich (für Ambience-Loops mit gleichmäßiger Lautheit).</summary>
        static void NormalizeRms(float[] b, float rms, float ceiling)
        {
            double s = 0;
            foreach (var v in b) s += v * v;
            float cur = (float)Math.Sqrt(s / Math.Max(1, b.Length));
            if (cur < 1e-7f) return;
            float g = rms / cur;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
            SoftLimit(b, ceiling);
        }

        /// <summary>Weicher Begrenzer: unterhalb des Knies linear, darüber sanft gegen die Obergrenze.</summary>
        static void SoftLimit(float[] b, float ceiling)
        {
            float knee = ceiling * 0.7f, room = ceiling - knee;
            for (int i = 0; i < b.Length; i++)
            {
                float v = b[i], a = Math.Abs(v);
                if (a <= knee) continue;
                float y = knee + room * FastTanh((a - knee) / room);
                b[i] = v < 0 ? -y : y;
            }
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

        /// <summary>Faltet das Ende (Länge fade) mit gleicher Leistung auf den Anfang – für Loops aus Rauschen/Oszillatoren.</summary>
        static float[] Loopify(float[] b, int fade)
        {
            int n = b.Length;
            fade = Math.Min(fade, n / 4);
            var r = new float[n - fade];
            Array.Copy(b, r, r.Length);
            for (int i = 0; i < fade; i++)
            {
                float t = (i + 0.5f) / fade;
                r[i] = b[i] * (float)Math.Sin(t * Math.PI * 0.5) + b[n - fade + i] * (float)Math.Cos(t * Math.PI * 0.5);
            }
            return r;
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

        // ================================================================== Einzelinstrumente (werden je Stück zwischengespeichert)
        /// <summary>Klavier: Teiltöne mit Inharmonizität, zwei leicht verstimmte Saiten, zweistufiger Ausklang, Hammergeräusch.</summary>
        static float[] PianoTone(float f, int rate, float hard, int seed)
        {
            float dur = Math.Min(6f, 1.8f + 260f / Math.Max(40f, f));
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            double fn = Math.Sqrt(f / 261.6);
            for (int k = 1; k <= 10; k++)
            {
                double fk = f * k * Math.Sqrt(1 + 0.00035 * k * k);
                if (fk > rate * 0.42) break;
                float amp = (float)(Math.Pow(k, -1.05) * Math.Pow(0.3 + 0.7 * hard, (k - 1) * 0.55));
                double d1 = 2.4 + 1.2 * k * fn, d2 = 0.3 + 0.3 * k * fn;
                for (int s = 0; s < 2; s++)
                {
                    double fr = fk * (s == 0 ? 1.0 : 1.0006 + 0.0004 * rng.Next());
                    double w = TwoPi * fr / rate, c = Math.Cos(w), sn = Math.Sin(w);
                    double x = 1, y = 0, e1 = 0.55 * amp * 0.5, e2 = 0.45 * amp * 0.5;
                    double r1 = Math.Exp(-d1 / rate), r2 = Math.Exp(-d2 / rate);
                    for (int i = 0; i < n; i++)
                    {
                        double nx = x * c - y * sn; y = x * sn + y * c; x = nx;
                        o[i] += (float)(y * (e1 + e2));
                        e1 *= r1; e2 *= r2;
                        if (e1 + e2 < 2e-5) break;
                    }
                }
            }
            var lp = new OnePole(); float la = Pole(1500f + 2500f * hard, rate);
            int hn = Math.Min(n, (int)(0.02f * rate));
            for (int i = 0; i < hn; i++) o[i] += lp.LpA(rng.Next() * 2 - 1, la) * (1f - i / (float)hn) * 0.1f * hard;
            int att = Math.Max(1, (int)(0.002f * rate));
            for (int i = 0; i < att && i < n; i++) o[i] *= i / (float)att;
            int rel = Math.Min(n / 3, (int)(0.3f * rate));
            for (int i = 0; i < rel; i++) o[n - 1 - i] *= i / (float)rel;
            return o;
        }

        /// <summary>Spiccato-Streicher (kurz gestrichen): drei verstimmte Sägezähne, Filterhüllkurve, Bogengeräusch.</summary>
        static float[] Spicc(float f, int rate, float bright, int seed)
        {
            float dur = 0.5f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            double p1 = rng.Next(), p2 = rng.Next(), p3 = rng.Next();
            double i1 = f * 0.9965 / rate, i2 = f / rate, i3 = f * 1.0045 / rate;
            float z1 = 0, z2 = 0;
            float cutMax = Math.Min(f * 9f, 900f + 2600f * bright);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = t < 0.006f ? t / 0.006f : (float)(0.8 * Math.Exp(-(t - 0.006) / 0.07) + 0.2 * Math.Exp(-(t - 0.006) / 0.26));
                p1 += i1; if (p1 >= 1) p1 -= 1;
                p2 += i2; if (p2 >= 1) p2 -= 1;
                p3 += i3; if (p3 >= 1) p3 -= 1;
                float s = (SawW(p1, i1) + SawW(p2, i2) + SawW(p3, i3)) * 0.577f;
                float a = Pole(cutMax * (0.3f + 0.7f * env), rate);
                z1 = s + (z1 - s) * a; z2 = z1 + (z2 - z1) * a;
                float scratch = (rng.Next() * 2 - 1) * (float)Math.Exp(-t / 0.007f) * 0.12f;
                o[i] = (z2 + scratch) * env;
            }
            for (int i = 0; i < 200 && i < n; i++) o[n - 1 - i] *= i / 200f;
            return o;
        }

        /// <summary>Gezupfte Saite (Karplus-Strong) – warmes Pizzicato.</summary>
        static float[] Pluck(float f, int rate, int seed)
        {
            float dur = Math.Min(2.2f, 0.8f + 100f / f);
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            int len = Math.Max(2, (int)Math.Round(rate / f - 0.5));
            var buf = new float[len];
            float lp = 0;
            for (int i = 0; i < len; i++) { lp += ((rng.Next() * 2 - 1) - lp) * 0.45f; buf[i] = lp; }
            int idx = 0; float body = 0;
            for (int i = 0; i < n; i++)
            {
                int j = idx + 1; if (j >= len) j = 0;
                float y = buf[idx];
                buf[idx] = (buf[idx] + buf[j]) * 0.5f * 0.995f;
                idx = j;
                body = y + (body - y) * 0.3f;
                o[i] = body * 1.6f;
            }
            int rel = Math.Min(n / 3, (int)(0.25f * rate));
            for (int i = 0; i < rel; i++) o[n - 1 - i] *= i / (float)rel;
            return o;
        }

        /// <summary>Taiko: tiefer Körper mit Tonhöhenabfall, Fellschlag, Luftstoß.</summary>
        static float[] Taiko(int rate, float f0, float decay, float slapF, int seed)
        {
            float dur = Math.Min(3.2f, decay * 5f + 0.3f);
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var bp = new Svf(); float bpc = SvfC(slapF, rate);
            var lp = new OnePole(); float la = Pole(380f, rate);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float bend = 1f + 0.8f * (float)Math.Exp(-t / 0.022f) + 0.1f * (float)Math.Exp(-t / 0.25f);
                p1 += f0 * bend / rate; p2 += f0 * 1.59f * bend / rate;
                float body = (float)Math.Sin(p1 * TwoPi) * (float)Math.Exp(-t / decay) + 0.3f * (float)Math.Sin(p2 * TwoPi) * (float)Math.Exp(-t / (decay * 0.4f));
                float w = rng.Next() * 2 - 1;
                float slap = bp.BandC(w, bpc, 1.1f) * (float)Math.Exp(-t / 0.014f) * 1.8f;
                float thump = lp.LpA(w, la) * (float)Math.Exp(-t / 0.04f) * 2.4f;
                o[i] = FastTanh(body * 1.5f) * 0.9f + slap + thump;
            }
            for (int i = 0; i < 24 && i < n; i++) o[i] *= i / 24f;
            return o;
        }

        /// <summary>Pauke: gestimmte Kesselmoden und Schlägelgeräusch.</summary>
        static float[] Timpani(float f, int rate, int seed)
        {
            float dur = 3.2f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            float[] ratio = { 1f, 1.504f, 1.742f, 2.0f, 2.245f };
            float[] dec = { 1.4f, 0.85f, 0.6f, 0.5f, 0.32f };
            float[] amp = { 1f, 0.55f, 0.3f, 0.28f, 0.14f };
            for (int m = 0; m < ratio.Length; m++)
            {
                double w = TwoPi * f * ratio[m] / rate, c = Math.Cos(w), s = Math.Sin(w), x = 1, y = 0;
                double e = amp[m], r = Math.Exp(-1.0 / (dec[m] * rate));
                for (int i = 0; i < n; i++) { double nx = x * c - y * s; y = x * s + y * c; x = nx; o[i] += (float)(y * e); e *= r; }
            }
            var lp = new OnePole(); float la = Pole(1400f, rate);
            for (int i = 0; i < n; i++) { float t = i / (float)rate; if (t > 0.06f) break; o[i] += lp.LpA(rng.Next() * 2 - 1, la) * (float)Math.Exp(-t / 0.006f) * 1.4f; }
            for (int i = 0; i < 16; i++) o[i] *= i / 16f;
            return o;
        }

        /// <summary>Amboss/Metallschlag (PYRA): unharmonische Stabmoden.</summary>
        static float[] Anvil(int rate, float f0, int seed)
        {
            float dur = 1.6f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };
            float[] dec = { 0.8f, 0.45f, 0.22f, 0.11f };
            float[] amp = { 1f, 0.6f, 0.35f, 0.2f };
            for (int m = 0; m < ratio.Length; m++)
            {
                if (f0 * ratio[m] > rate * 0.42f) continue;
                double w = TwoPi * f0 * ratio[m] / rate, c = Math.Cos(w), s = Math.Sin(w), x = 1, y = 0;
                double e = amp[m], r = Math.Exp(-1.0 / (dec[m] * rate));
                for (int i = 0; i < n; i++) { double nx = x * c - y * s; y = x * s + y * c; x = nx; o[i] += (float)(y * e) * 0.5f; e *= r; }
            }
            var lp = new OnePole(); float la = Pole(4000f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                if (t > 0.3f) break;
                o[i] += lp.LpA(rng.Next() * 2 - 1, la) * (float)Math.Exp(-t / 0.004f) * 0.9f + (float)Math.Sin(TwoPi * 120 * t) * (float)Math.Exp(-t / 0.04f) * 0.7f;
            }
            return o;
        }

        /// <summary>Maschinen-Klappern (kurzer Rauschstoß durch Resonatoren).</summary>
        static float[] Clank(int rate, int seed)
        {
            int n = (int)(0.45f * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var f1 = new Svf(); var f2 = new Svf(); var f3 = new Svf();
            float c1 = SvfC(330f, rate), c2 = SvfC(870f, rate), c3 = SvfC(1650f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float x = (rng.Next() * 2 - 1) * (float)Math.Exp(-t / 0.004f);
                o[i] = (f1.BandC(x, c1, 0.025f) + f2.BandC(x, c2, 0.03f) * 0.8f + f3.BandC(x, c3, 0.035f) * 0.6f) * 0.05f + x * 0.3f;
            }
            return o;
        }

        /// <summary>Kurzer Uhr-/Mechanik-Tick.</summary>
        static float[] Tick(int rate, int seed)
        {
            int n = (int)(0.08f * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                o[i] = (rng.Next() * 2 - 1) * (float)Math.Exp(-t / 0.0015f) * 0.6f + (float)Math.Sin(TwoPi * 2300 * t) * (float)Math.Exp(-t / 0.01f) * 0.4f;
            }
            return o;
        }

        /// <summary>Sub-Einschlag („Boom“): tiefer Sinus mit Tonhöhenabfall.</summary>
        static float[] Boom(int rate, int seed)
        {
            float dur = 3.6f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var lp = new OnePole(); float la = Pole(320f, rate);
            double p = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                p += (33f + 75f * (float)Math.Exp(-t / 0.07f)) / rate;
                float body = (float)Math.Sin(p * TwoPi) * (float)Math.Exp(-t / 1.1f);
                o[i] = FastTanh(body * 1.8f) * 0.85f + lp.LpA(rng.Next() * 2 - 1, la) * (float)Math.Exp(-t / 0.05f) * 2.2f;
            }
            for (int i = 0; i < 30; i++) o[i] *= i / 30f;
            return o;
        }

        /// <summary>Einschlag-Rauschen („Crash“) mit metallischem Schimmer.</summary>
        static float[] Crash(int rate, int seed)
        {
            float dur = 3.2f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var lp = new OnePole(); float la = Pole(1600f, rate);
            float[] pf = { 3150f, 4120f, 5290f, 6630f, 7410f, 8230f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float w = rng.Next() * 2 - 1;
                float hp = w - lp.LpA(w, la);
                float ring = 0;
                for (int k = 0; k < pf.Length; k++) if (pf[k] < rate * 0.45f) ring += (float)Math.Sin(TwoPi * pf[k] * t + k);
                o[i] = hp * (float)Math.Exp(-t / 0.85f) * 0.8f + ring * 0.02f * (float)Math.Exp(-t / 0.6f);
            }
            for (int i = 0; i < 20; i++) o[i] *= i / 20f;
            return o;
        }

        /// <summary>Anschwellendes Rauschen (Brandung/„Ocean Drum“).</summary>
        static float[] Swish(int rate, float len, int seed)
        {
            int n = (int)(len * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var lp = new OnePole();
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float e = (float)Math.Pow(Math.Sin(Math.PI * Math.Pow(u, 0.7)), 2);
                o[i] = lp.LpA(rng.Next() * 2 - 1, Pole(500f + 2200f * e, rate)) * e;
            }
            return o;
        }

        /// <summary>Riser: aufsteigender Rausch-Sweep plus Glissando, endet genau auf dem Zielschlag.</summary>
        static float[] Riser(int rate, float len, int seed)
        {
            int n = (int)(len * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            var bp = new Svf();
            var lp = new OnePole();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = 220f * (float)Math.Pow(20.0, u);
                float env = (float)Math.Pow(u, 2.2);
                float nz = bp.BandC(rng.Next() * 2 - 1, SvfC(f, rate), 0.6f) * 0.6f;
                double inc = (98.0 * Math.Pow(2, u * 1.5)) / rate; ph += inc; if (ph >= 1) ph -= 1;
                float saw = lp.LpA(SawW(ph, inc), Pole(400f + 2800f * u, rate)) * (float)Math.Pow(u, 3) * 0.35f;
                o[i] = (nz + saw) * env;
            }
            int fe = Math.Min(n, (int)(0.006f * rate));
            for (int i = 0; i < fe; i++) o[n - 1 - i] *= i / (float)fe;
            return o;
        }

        /// <summary>Rückwärts-Becken: anschwellendes Rauschen in einen Einschlag hinein.</summary>
        static float[] RevSwell(int rate, float len, int seed)
        {
            var c = Crash(rate, seed);
            int n = Math.Min(c.Length, (int)(len * rate));
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = c[n - 1 - i];
            int fe = Math.Min(n, (int)(0.004f * rate));
            for (int i = 0; i < fe; i++) o[n - 1 - i] *= i / (float)fe;
            return o;
        }

        /// <summary>„Braam“: massiver Blechbläser-Cluster (Grundton, Oktave, Quinte, Terz) mit Sub, Verzerrung und sich schlagartig öffnendem Filter.</summary>
        static float[] Braam(float f, int third, int rate, int seed)
        {
            float dur = 4.6f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            float[] mult = { 1f, 2f, 3f, 4f, 4f * (float)Math.Pow(2, third / 12.0) };
            float[] g = { 1f, 0.9f, 0.6f, 0.45f, 0.3f };
            int V = mult.Length * 2;
            var ph = new double[V]; var inc = new double[V]; var gg = new float[V];
            for (int v = 0; v < V; v++)
            {
                ph[v] = rng.Next();
                inc[v] = f * mult[v / 2] * (v % 2 == 0 ? 0.997 : 1.004) / rate;
                gg[v] = g[v / 2];
            }
            float z1 = 0, z2 = 0;
            double sp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float amp = t < 0.05f ? t / 0.05f : t < 1.7f ? 1f : (float)Math.Exp(-(t - 1.7f) / 1.0f);
                float fenv = t < 0.12f ? t / 0.12f : 0.15f + 0.85f * (float)Math.Exp(-(t - 0.12f) / 0.85f);
                float s = 0;
                for (int v = 0; v < V; v++) { ph[v] += inc[v]; if (ph[v] >= 1) ph[v] -= 1; s += SawW(ph[v], inc[v]) * gg[v]; }
                s = FastTanh(s * 0.55f);
                float a = Pole(160f + 2300f * fenv, rate);
                z1 = s + (z1 - s) * a; z2 = z1 + (z2 - z1) * a;
                sp += f / rate;
                o[i] = (z2 * 0.9f + (float)Math.Sin(sp * TwoPi) * 0.5f) * amp;
            }
            return o;
        }

        /// <summary>Kurzer, harter Posaunenakzent (Grundton, Quinte, Oktave).</summary>
        static float[] Stab(float f, int rate, int seed)
        {
            float dur = 1.1f;
            int n = (int)(dur * rate);
            var o = new float[n];
            var rng = new Rng(seed);
            float[] mult = { 1f, 1.5f, 2f };
            var ph = new double[6]; var inc = new double[6];
            for (int v = 0; v < 6; v++) { ph[v] = rng.Next(); inc[v] = f * mult[v / 2] * (v % 2 == 0 ? 0.998 : 1.003) / rate; }
            float z1 = 0, z2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = t < 0.012f ? t / 0.012f : (float)Math.Exp(-(t - 0.012f) / 0.28f);
                float s = 0;
                for (int v = 0; v < 6; v++) { ph[v] += inc[v]; if (ph[v] >= 1) ph[v] -= 1; s += SawW(ph[v], inc[v]); }
                s = FastTanh(s * 0.5f * (1f + env));
                float a = Pole(220f + 2600f * env * env, rate);
                z1 = s + (z1 - s) * a; z2 = z1 + (z2 - z1) * a;
                o[i] = z2 * env;
            }
            return o;
        }

        // ================================================================== Harmonik
        public class Chord
        {
            public int Root; public int Third; public bool Add9;
            /// <summary>Zusatzton über dem Grundton (10 = kleine, 11 = große Septime), −1 = keiner.</summary>
            public int Ext = -1;
            /// <summary>Basston für Umkehrungen/Orgelpunkt; int.MinValue = Grundton.</summary>
            public int Bass = int.MinValue;
            public Chord(int r, int th, bool add9 = false) { Root = r; Third = th; Add9 = add9; }
            public int BassNote { get { return Bass == int.MinValue ? Root : Bass; } }
            public int Top { get { return Ext >= 0 ? Root + Ext : Add9 ? Root + 14 : Root + 12; } }
        }

        /// <summary>Akkord: q = 'M' Dur, 'm' Moll, 's' sus4, '2' sus2.</summary>
        static Chord C(int root, char q, bool add9 = false) { return new Chord(root, q == 'M' ? 4 : q == 'm' ? 3 : q == '2' ? 2 : 5, add9); }
        static Chord Cs(int root, char q, int bass) { var c = C(root, q); c.Bass = bass; return c; }
        static Chord Cx(int root, char q, int ext) { var c = C(root, q); c.Ext = ext; return c; }

        static int Pc(int s) { return ((s % 12) + 12) % 12; }

        /// <summary>Vierstimmiger Satz im Bereich [lo, hi] mit möglichst kleinen Stimmbewegungen und ausgewogenen Abständen.</summary>
        static int[] Voicing(Chord c, int[] prev, int lo, int hi)
        {
            int[] tones = { c.Root, c.Root + c.Third, c.Root + 7, c.Top };
            var opts = new List<int>[4];
            for (int i = 0; i < 4; i++)
            {
                opts[i] = new List<int>();
                int first = lo + Pc(tones[i] - lo);
                for (int v = first; v <= hi; v += 12) opts[i].Add(v);
                if (opts[i].Count == 0) opts[i].Add(first);
            }
            int[] best = null; float bestCost = float.MaxValue;
            var s = new int[4];
            float center = (lo + hi) * 0.5f;
            foreach (int a in opts[0]) foreach (int b in opts[1]) foreach (int c2 in opts[2]) foreach (int d in opts[3])
                        {
                            s[0] = a; s[1] = b; s[2] = c2; s[3] = d; Array.Sort(s);
                            float cost = 0;
                            for (int i = 0; i < 3; i++) { int gap = s[i + 1] - s[i]; if (gap < 2) cost += 9; else if (gap > 9) cost += (gap - 9) * 1.5f; }
                            float mean = (s[0] + s[1] + s[2] + s[3]) * 0.25f;
                            cost += Math.Abs(mean - center) * 0.35f;
                            if (prev != null) for (int i = 0; i < 4; i++) cost += Math.Abs(s[i] - prev[i]);
                            if (cost < bestCost) { bestCost = cost; best = (int[])s.Clone(); }
                        }
            return best;
        }

        static int BassPlace(int semi, int prev, int lo)
        {
            int v = lo + Pc(semi - lo);
            if (prev != int.MinValue)
            {
                int best = v, bd = Math.Abs(v - prev);
                int[] alt = { v - 12, v + 12 };
                foreach (int c in alt) if (c >= lo - 4 && c <= lo + 14 && Math.Abs(c - prev) < bd) { bd = Math.Abs(c - prev); best = c; }
                v = best;
            }
            return v;
        }

        /// <summary>Kleinster Ton ≥ base+1 mit derselben Tonklasse.</summary>
        static int Above(int semi, int baseSemi) { return baseSemi + 1 + Pc(semi - baseSemi - 1); }

        struct MelNote { public float Beat, Dur, Semi; public bool Rest; }

        /// <summary>Melodie-Notation: „Halbton:Schläge“ (Standard 1 Schlag), „r“ = Pause, „|“ = Taktstrich (jeder Takt beginnt auf seinem Raster).</summary>
        static List<MelNote> ParseMel(string s, int beatsPerBar)
        {
            var res = new List<MelNote>();
            var bars = s.Split('|');
            for (int b = 0; b < bars.Length; b++)
            {
                float beat = b * beatsPerBar;
                foreach (var tok0 in bars[b].Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string tok = tok0; float dur = 1f;
                    int c = tok.IndexOf(':');
                    if (c >= 0) { dur = float.Parse(tok.Substring(c + 1), CultureInfo.InvariantCulture); tok = tok.Substring(0, c); }
                    bool rest = tok == "r";
                    float semi = rest ? 0 : float.Parse(tok, CultureInfo.InvariantCulture);
                    res.Add(new MelNote { Beat = beat, Dur = dur, Semi = semi, Rest = rest });
                    beat += dur;
                }
            }
            return res;
        }

        // ================================================================== Musik-Engine
        const int CR = 32; // Steuerrate: Hüllkurven, Tonhöhe und Filter werden alle 32 Samples neu berechnet

        struct Note { public float T, Dur, Abs, Vel; public Note(float t, float d, float abs, float v) { T = t; Dur = d; Abs = abs; Vel = v; } }

        /// <summary>Klangfarbe einer durchgehenden (legato) Stimme.</summary>
        sealed class Spec
        {
            public int Kind;              // 0 Streicher, 1 Chor/Stimme, 2 Blech, 3 Flageolett, 4 Sub/Kontrabass
            public int Saws = 3;
            public float Detune = 0.006f;
            public float Attack = 0.4f, Release = 0.9f, Glide = 0.06f;
            public float CutLo = 500f, CutHi = 3000f, CutPitch;
            public float VibDepth = 0.0035f, VibRate = 5.2f, VibDelay = 0.5f;
            public float Drive, Accent = 0.12f, Breath, Tremolo;
            public float[] Form, FormGain; public float FormQ = 5f;
            public Spec Clone() { return (Spec)MemberwiseClone(); }
        }

        static readonly float[] VowelA = { 760f, 1180f, 2800f }, VowelAGain = { 1f, 0.6f, 0.22f };
        static readonly float[] VowelO = { 430f, 820f, 2650f }, VowelOGain = { 1f, 0.4f, 0.08f };
        static readonly float[] VowelU = { 320f, 780f, 2300f }, VowelUGain = { 1f, 0.25f, 0.05f };

        sealed class LineDef
        {
            public string Stem; public Spec Sp; public float Gain, Lo, Hi, G0, G1; public int Seed;
            public readonly List<Note> Notes = new List<Note>();
        }

        /// <summary>
        /// Arrangement: sammelt Einzelklänge (sofort platziert) und Legato-Stimmen (am Ende berechnet) für alle Stems.
        /// Bei Loops wird alles zyklisch behandelt (Ausklänge wandern an den Anfang, Stimmen werden überblendet gefaltet).
        /// </summary>
        sealed class Arr
        {
            public readonly int Rate; public readonly bool Loop; public readonly float Len; public readonly int N;
            public readonly Dictionary<string, float[]> S = new Dictionary<string, float[]>();
            public readonly Rng R;
            readonly Dictionary<string, float[]> cache = new Dictionary<string, float[]>();
            readonly Dictionary<string, LineDef> lines = new Dictionary<string, LineDef>();
            readonly List<string> lineOrder = new List<string>();
            readonly List<float> arcT = new List<float>(), arcV = new List<float>();
            readonly int seed;

            // aktuelles Taktraster (je Abschnitt)
            public float Root = 220f, Bpm = 80f, T0;
            public int Beats = 4, Bars;
            public Chord[] Ch; public int[][] Up; public int[] Bs;
            int[] prevUp; int prevBs = int.MinValue;
            public int HornShift;

            public Arr(int rate, bool loop, float len, float tail, int seed)
            {
                Rate = rate; Loop = loop; Len = len; this.seed = seed;
                N = (int)((len + (loop ? 0f : tail)) * rate);
                R = new Rng(seed);
                foreach (var s in StemNames) S[s] = new float[N];
            }

            public float Beat { get { return 60f / Bpm; } }
            public float BarLen { get { return Beat * Beats; } }
            public float T(int bar, float beat = 0f) { return T0 + bar * BarLen + beat * Beat; }
            public int Semi(float hz) { return (int)Math.Round(12 * Math.Log(hz / Root, 2)); }
            public float Hz(float semi) { return Root * (float)Math.Pow(2, semi / 12.0); }
            float AbsOf(float semi) { return semi + 12f * (float)Math.Log(Root / 440.0, 2); }
            static float HzAbs(float abs) { return 440f * (float)Math.Pow(2, abs / 12.0); }
            float J(float ms) { return R.Range(-ms, ms) * 0.001f; }

            // ------------------------------------------------------------ Spannungsbogen
            public void ArcPoint(float t, float v)
            {
                int i = arcT.Count;
                while (i > 0 && arcT[i - 1] > t) i--;
                arcT.Insert(i, t); arcV.Insert(i, v);
            }

            public float ArcAt(float t)
            {
                int n = arcT.Count;
                if (n == 0) return 0.5f;
                if (Loop) { t %= Len; if (t < 0) t += Len; }
                int lo = 0, hi = n - 1, idx = -1;
                while (lo <= hi) { int m = (lo + hi) >> 1; if (arcT[m] <= t) { idx = m; lo = m + 1; } else hi = m - 1; }
                if (idx < 0)
                {
                    if (!Loop) return arcV[0];
                    float tp = arcT[n - 1] - Len;
                    return arcV[n - 1] + (arcV[0] - arcV[n - 1]) * M.Clamp01((t - tp) / Math.Max(1e-4f, arcT[0] - tp));
                }
                if (idx == n - 1)
                {
                    if (!Loop) return arcV[n - 1];
                    float tn = arcT[0] + Len;
                    return arcV[idx] + (arcV[0] - arcV[idx]) * M.Clamp01((t - arcT[idx]) / Math.Max(1e-4f, tn - arcT[idx]));
                }
                return arcV[idx] + (arcV[idx + 1] - arcV[idx]) * M.Clamp01((t - arcT[idx]) / Math.Max(1e-4f, arcT[idx + 1] - arcT[idx]));
            }

            /// <summary>Neuer Abschnitt: Taktraster, Akkorde (ein Akkord je Takt), Spannungsbogen je Taktbeginn.</summary>
            public void Section(float t0, float root, float bpm, int beats, Chord[] ch, float[] arc)
            {
                if (Math.Abs(root - Root) > 0.01f && prevUp != null)
                {
                    // Tonartwechsel: Stimmführung in neue Bezugsgröße umrechnen
                    int d = (int)Math.Round(12 * Math.Log(Root / root, 2));
                    for (int i = 0; i < prevUp.Length; i++) prevUp[i] += d;
                    if (prevBs != int.MinValue) prevBs += d;
                }
                T0 = t0; Root = root; Bpm = bpm; Beats = beats; Ch = ch; Bars = ch.Length;
                Up = new int[Bars][]; Bs = new int[Bars];
                int lo = Semi(185f), hi = Semi(690f), blo = Semi(52f);
                int passes = Loop ? 2 : 1;
                for (int p = 0; p < passes; p++)
                    for (int b = 0; b < Bars; b++)
                    {
                        Up[b] = Voicing(ch[b], prevUp, lo, hi);
                        prevUp = Up[b];
                        Bs[b] = BassPlace(ch[b].BassNote, prevBs, blo);
                        prevBs = Bs[b];
                    }
                double avg = 0;
                for (int b = 0; b < Bars; b++) avg += (Up[b][0] + Up[b][1] + Up[b][2]) / 3.0;
                avg /= Math.Max(1, Bars);
                HornShift = Hz((float)avg) > 390f ? -12 : 0;
                if (arc != null) for (int b = 0; b < Bars && b < arc.Length; b++) ArcPoint(T(b), arc[b]);
            }

            // ------------------------------------------------------------ Einzelklänge
            public float[] Get(string key, Func<float[]> make)
            {
                float[] v;
                if (!cache.TryGetValue(key, out v)) { v = make(); cache[key] = v; }
                return v;
            }

            public void Hit(string stem, float t, float[] smp, float gain)
            {
                if (smp == null || gain == 0f) return;
                var dst = S[stem];
                int start = (int)Math.Round(t * Rate);
                if (Loop)
                {
                    start %= N; if (start < 0) start += N;
                    int k = start;
                    for (int i = 0; i < smp.Length; i++) { dst[k] += smp[i] * gain; if (++k >= N) k = 0; }
                }
                else
                {
                    for (int i = 0; i < smp.Length; i++) { int k = start + i; if (k < 0) continue; if (k >= N) break; dst[k] += smp[i] * gain; }
                }
            }

            int Key(float semi) { return (int)Math.Round(AbsOf(semi) * 10); }
            public float[] PianoS(float semi, bool hard) { float hz = Hz(semi); int k = Key(semi); return Get("pn" + k + (hard ? "h" : "s"), () => PianoTone(hz, Rate, hard ? 0.95f : 0.45f, k * 7 + 3)); }
            public float[] SpiccS(float semi, float bright) { float hz = Hz(semi); int k = Key(semi); int bk = (int)(bright * 10); return Get("sp" + k + ":" + bk, () => Spicc(hz, Rate, bk / 10f, k * 13 + 1)); }
            public float[] PluckS(float semi) { float hz = Hz(semi); int k = Key(semi); return Get("pl" + k, () => Pluck(hz, Rate, k * 17 + 5)); }
            public float[] StabS(float semi) { float hz = Hz(semi); int k = Key(semi); return Get("st" + k, () => Stab(hz, Rate, k * 19 + 7)); }
            public float[] TimpS(float semi) { float hz = Hz(semi); int k = Key(semi); return Get("tp" + k, () => Timpani(hz, Rate, k * 23 + 9)); }

            /// <summary>Pauke/Schlagwerk nach Musterzeichen.</summary>
            public float[] Kit(char c, int bar)
            {
                switch (char.ToUpperInvariant(c))
                {
                    case 'L': return Get("L", () => Taiko(Rate, 57f, 0.55f, 620f, seed + 1));
                    case 'M': return Get("M", () => Taiko(Rate, 96f, 0.28f, 1050f, seed + 2));
                    case 'S': return Get("S", () => Taiko(Rate, 190f, 0.09f, 2300f, seed + 3));
                    case 'T': { int r = TimpSemi(bar); return TimpS(r); }
                    case 'A': return Get("A", () => Anvil(Rate, 610f, seed + 4));
                    case 'C': return Get("C", () => Clank(Rate, seed + 5));
                    case 'K': return Get("K", () => Tick(Rate, seed + 6));
                    case 'W': return Get("W", () => Swish(Rate, 1.6f, seed + 7));
                    case 'B': return Get("B", () => Boom(Rate, seed + 8));
                }
                return null;
            }

            int TimpSemi(int bar)
            {
                int r = Ch[bar].BassNote;
                int lo = Semi(78f);
                return lo + Pc(r - lo);
            }

            // ------------------------------------------------------------ Legato-Stimmen
            public void Line(string name, string stem, Spec sp, float gain, float lo, float hi, float g0, float g1)
            {
                if (lines.ContainsKey(name)) return;
                lines[name] = new LineDef { Stem = stem, Sp = sp, Gain = gain, Lo = lo, Hi = hi, G0 = g0, G1 = g1, Seed = (int)Hash.Fnv1a(name) ^ seed };
                lineOrder.Add(name);
            }

            public void Note(string line, float t, float dur, float semi, float vel)
            {
                LineDef ld;
                if (lines.TryGetValue(line, out ld)) ld.Notes.Add(new Note(t, dur, AbsOf(semi), vel));
            }

            float Dyn(LineDef ld, float t)
            {
                float a = ArcAt(t);
                float g = ld.G1 > ld.G0 ? M.Smooth(M.InvLerp(ld.G0, ld.G1, a)) : 1f;
                return ld.Lo + (ld.Hi - ld.Lo) * g;
            }

            void RenderLine(LineDef ld)
            {
                if (ld.Notes.Count == 0) return;
                var notes = new List<Note>(ld.Notes.Count * 3);
                foreach (var nt in ld.Notes)
                {
                    notes.Add(nt);
                    if (Loop) { notes.Add(new Note(nt.T - Len, nt.Dur, nt.Abs, nt.Vel)); notes.Add(new Note(nt.T + Len, nt.Dur, nt.Abs, nt.Vel)); }
                }
                notes.Sort((x, y) => x.T.CompareTo(y.T));
                var sp = ld.Sp;
                int pre = Loop ? (int)(3f * Rate) : 0, fold = Loop ? (int)(1.5f * Rate) : 0;
                int total = pre + N + fold;
                int nc = total / CR + 2;
                var cF = new float[nc]; var cA = new float[nc]; var cC = new float[nc];
                float dtc = CR / (float)Rate;
                float kA = 1f - (float)Math.Exp(-dtc / Math.Max(0.004f, sp.Attack));
                float kR = 1f - (float)Math.Exp(-dtc / Math.Max(0.004f, sp.Release));
                float kG = 1f - (float)Math.Exp(-dtc / Math.Max(0.004f, sp.Glide));
                var rng = new Rng(ld.Seed);
                float vibPh = rng.Next() * 6.283f;
                int cur = -1, next = 0;
                float amp = 0;
                double la = notes[0].Abs;
                bool any = false;
                for (int k = 0; k < nc; k++)
                {
                    float t = (k * CR - pre) / (float)Rate;
                    while (next < notes.Count && notes[next].T <= t) { cur = next; next++; }
                    float target = 0, since = 9f; double tl = la;
                    if (cur >= 0)
                    {
                        var nt = notes[cur];
                        if (t < nt.T + nt.Dur)
                        {
                            since = t - nt.T;
                            target = nt.Vel * Dyn(ld, t) * (1f + sp.Accent * (float)Math.Exp(-since / 0.18f));
                            tl = nt.Abs;
                        }
                    }
                    if (target > 0) { if (amp < 0.002f) la = tl; else la += (tl - la) * kG; }
                    amp += (target - amp) * (target > amp ? kA : kR);
                    if (amp > 1e-4f) any = true;
                    float vib = sp.VibDepth * Math.Min(1f, since / Math.Max(0.01f, sp.VibDelay));
                    float f = 440f * (float)Math.Pow(2, la / 12.0) * (1f + vib * (float)Math.Sin(TwoPi * sp.VibRate * t + vibPh));
                    cF[k] = f; cA[k] = amp;
                    float bright = (float)Math.Pow(Math.Min(1.2f, amp), 1.3);
                    float cut = sp.CutPitch > 0 ? f * sp.CutPitch * (0.75f + 0.5f * bright) : sp.CutLo + (sp.CutHi - sp.CutLo) * bright;
                    cC[k] = Math.Min(cut, Rate * 0.45f);
                }
                if (!any) return;

                var tmp = new float[total];
                int K = Math.Max(1, sp.Saws);
                var ph = new double[K]; var det = new float[K]; var drR = new float[K]; var drP = new float[K]; var inc = new double[K];
                for (int j = 0; j < K; j++)
                {
                    ph[j] = rng.Next();
                    float spread = K == 1 ? 0f : (2f * j / (K - 1) - 1f);
                    det[j] = 1f + sp.Detune * spread * (0.75f + 0.5f * rng.Next());
                    drR[j] = 0.5f + 1.8f * rng.Next(); drP[j] = rng.Next() * 6.283f;
                }
                float invK = 1f / (float)Math.Sqrt(K);
                float z1 = 0, z2 = 0;
                Svf f1 = null, f2 = null, f3 = null;
                float fc1 = 0, fc2 = 0, fc3 = 0, fd = 1f / Math.Max(1f, sp.FormQ);
                float g1 = 0, g2 = 0, g3 = 0;
                if (sp.Kind == 1)
                {
                    f1 = new Svf(); f2 = new Svf(); f3 = new Svf();
                    var form = sp.Form ?? VowelA; var fg = sp.FormGain ?? VowelAGain;
                    fc1 = SvfC(form[0], Rate); fc2 = SvfC(form[1], Rate); fc3 = SvfC(form[2], Rate);
                    g1 = fg[0] * fd * 2.2f; g2 = fg[1] * fd * 2.2f; g3 = fg[2] * fd * 2.2f;
                }
                float soft = Pole(3600f, Rate);
                uint ns = (uint)ld.Seed | 1u;
                for (int k = 0; k < nc - 1; k++)
                {
                    int i0 = k * CR; if (i0 >= total) break;
                    int i1 = Math.Min(total, i0 + CR);
                    float a0 = cA[k], a1 = cA[k + 1];
                    if (a0 < 1e-5f && a1 < 1e-5f) { z1 *= 0.9f; z2 *= 0.9f; continue; }
                    float t = (i0 - pre) / (float)Rate;
                    float f = cF[k];
                    for (int j = 0; j < K; j++) inc[j] = f * det[j] * (1f + 0.0016f * (float)Math.Sin(drR[j] * t + drP[j])) / Rate;
                    float pa = Pole(cC[k], Rate);
                    float trem = sp.Tremolo > 0 ? 1f - sp.Tremolo * (0.5f + 0.5f * (float)Math.Sin(TwoPi * 11.5 * t)) : 1f;
                    float a = a0 * trem, da = (a1 - a0) * trem / CR;
                    float drive = 1f + sp.Drive * (0.4f + 1.6f * Math.Min(1f, a0));
                    int kind = sp.Kind;
                    for (int i = i0; i < i1; i++)
                    {
                        float s = 0;
                        for (int j = 0; j < K; j++)
                        {
                            double p = ph[j] + inc[j]; if (p >= 1.0) p -= 1.0; ph[j] = p;
                            s += SawW(p, inc[j]);
                        }
                        s *= invK;
                        float y;
                        if (kind == 1)
                        {
                            ns ^= ns << 13; ns ^= ns >> 17; ns ^= ns << 5;
                            float src = s + ((ns * 2.3283064e-10f) * 2f - 1f) * sp.Breath;
                            y = f1.BandC(src, fc1, fd) * g1 + f2.BandC(src, fc2, fd) * g2 + f3.BandC(src, fc3, fd) * g3;
                            z1 = y + (z1 - y) * soft; y = z1;
                        }
                        else if (kind == 2)
                        {
                            float x = FastTanh(s * drive) / FastTanh(drive);
                            z1 = x + (z1 - x) * pa; z2 = z1 + (z2 - z1) * pa; y = z2;
                        }
                        else
                        {
                            z1 = s + (z1 - s) * pa; z2 = z1 + (z2 - z1) * pa; y = z2;
                        }
                        tmp[i] += y * a * ld.Gain;
                        a += da;
                    }
                }

                var dst = S[ld.Stem];
                if (Loop)
                {
                    for (int i = 0; i < N; i++)
                    {
                        float v = tmp[pre + i];
                        if (i < fold)
                        {
                            float x = (i + 0.5f) / fold;
                            v = v * (float)Math.Sin(x * Math.PI * 0.5) + tmp[pre + N + i] * (float)Math.Cos(x * Math.PI * 0.5);
                        }
                        dst[i] += v;
                    }
                }
                else for (int i = 0; i < N && i < total; i++) dst[i] += tmp[i];
            }

            // ------------------------------------------------------------ Standardbesetzung
            public void DefStrings(float bright, float gain, float lo, float attack = 0.5f)
            {
                var sp = new Spec { Kind = 0, Saws = 4, Detune = 0.0065f, Attack = attack, Release = 1.0f, Glide = 0.07f, CutLo = 700f + 900f * bright, CutHi = 2300f + 4300f * bright, VibDepth = 0.003f, VibRate = 5.3f, VibDelay = 0.5f, Accent = 0.1f };
                for (int v = 0; v < 4; v++) Line("str" + v, "pad", sp, gain, lo, 1f, 0f, 1f);
                var cel = sp.Clone(); cel.CutLo = 380f; cel.CutHi = 1500f + 600f * bright; cel.VibDepth = 0.0022f;
                Line("strB", "pad", cel, gain * 1.25f, lo, 1f, 0f, 1f);
                var high = sp.Clone(); high.Attack = attack * 1.6f; high.CutHi += 700f;
                Line("strHi", "pad", high, gain * 0.8f, 0f, 1f, 0.7f, 0.95f);
            }

            public void Strings(int b0, int b1, float vel = 1f)
            {
                for (int b = b0; b < b1; b++)
                {
                    float t = T(b), d = BarLen + 0.06f;
                    for (int v = 0; v < 4; v++) Note("str" + v, t, d, Up[b][v], vel);
                    Note("strB", t, d, Bs[b] + 12, vel);
                }
            }

            public void StringsHigh(int b0, int b1) { for (int b = b0; b < b1; b++) Note("strHi", T(b), BarLen + 0.06f, Up[b][3] + 12, 1f); }

            public void DefSub(float gain, float lo)
            {
                var sp = new Spec { Kind = 4, Saws = 1, Attack = 0.6f, Release = 1.1f, Glide = 0.1f, CutPitch = 1.8f, VibDepth = 0f, Accent = 0.15f };
                Line("sub", "bass", sp, gain * 1.35f, lo, 1f, 0f, 1f);
            }

            public void Sub(int b0, int b1) { for (int b = b0; b < b1; b++) Note("sub", T(b), BarLen + 0.06f, Bs[b], 1f); }

            public void DefChoir(string name, char vowel, float gain, float g0, float g1, float attack = 1.0f)
            {
                var sp = new Spec
                {
                    Kind = 1, Saws = 4, Detune = 0.009f, Attack = attack, Release = 1.5f, Glide = 0.12f, VibDepth = 0.0045f, VibRate = 4.9f, VibDelay = 0.9f, Breath = 0.15f, FormQ = 5f, Accent = 0.05f,
                    Form = vowel == 'a' ? VowelA : vowel == 'u' ? VowelU : VowelO,
                    FormGain = vowel == 'a' ? VowelAGain : vowel == 'u' ? VowelUGain : VowelOGain
                };
                for (int v = 0; v < 3; v++) Line(name + v, "choir", sp, gain, 0f, 1f, g0, g1);
            }

            public void Choir(string name, int b0, int b1, int shift = 0)
            {
                for (int b = b0; b < b1; b++) for (int v = 0; v < 3; v++) Note(name + v, T(b), BarLen + 0.08f, Up[b][v + 1] + shift, 1f);
            }

            public void DefHorns(float gain, float g0, float g1, float drive)
            {
                var sp = new Spec { Kind = 2, Saws = 3, Detune = 0.0045f, Attack = 0.5f, Release = 0.9f, Glide = 0.08f, CutLo = 260f, CutHi = 2300f, Drive = drive, VibDepth = 0.002f, VibDelay = 0.8f, Accent = 0.18f };
                for (int v = 0; v < 3; v++) Line("hn" + v, "brass", sp, gain, 0f, 1f, g0, g1);
                var m = sp.Clone(); m.Attack = 0.09f; m.Release = 0.28f; m.Glide = 0.045f; m.CutHi = 2900f; m.Accent = 0.3f; m.VibDepth = 0.003f;
                Line("hm", "brass", m, gain * 1.7f, 0.35f, 1f, 0f, 1f);
                var tb = m.Clone(); tb.CutHi = 2000f; tb.Drive = drive * 1.3f;
                Line("hm2", "brass", tb, gain * 1.3f, 0.35f, 1f, 0f, 1f);
                Line("hc", "brass", m, gain * 1.1f, 0.35f, 1f, 0f, 1f);
            }

            public void Horns(int b0, int b1)
            {
                for (int b = b0; b < b1; b++) for (int v = 0; v < 3; v++) Note("hn" + v, T(b), BarLen + 0.06f, Up[b][v] + HornShift, 1f);
            }

            public void DefFlag(float gain, float lo)
            {
                var sp = new Spec { Kind = 3, Saws = 2, Detune = 0.002f, Attack = 1.4f, Release = 2.0f, Glide = 0.25f, CutPitch = 1.35f, VibDepth = 0.0015f, VibRate = 4.2f, VibDelay = 1f, Tremolo = 0.12f, Accent = 0f };
                Line("fl0", "pad", sp, gain, lo, 1f, 0f, 1f);
                Line("fl1", "pad", sp, gain * 0.8f, lo, 1f, 0f, 1f);
            }

            /// <summary>Legato-Melodie in eine Stimme schreiben (Notation siehe ParseMel).</summary>
            public void Mel(string line, int bar0, string mel, int shift, float vel, float legato = 0.95f)
            {
                foreach (var m in ParseMel(mel, Beats)) if (!m.Rest) Note(line, T(bar0, m.Beat), m.Dur * Beat * legato, m.Semi + shift, vel);
            }

            public void PianoMel(int bar0, string mel, float vel, int shift = 0)
            {
                foreach (var m in ParseMel(mel, Beats))
                    if (!m.Rest) Hit("piano", T(bar0, m.Beat) + J(6f), PianoS(m.Semi + shift, vel > 0.62f), vel * R.Range(0.9f, 1.08f));
            }

            /// <summary>Gebrochene Akkorde: Zeichen je Schritt '0'–'3' = Akkordstimme, 'b' = Basston, '.' = Pause.</summary>
            public void PianoArp(int b0, int b1, string pat, float vel, int shift)
            {
                int steps = pat.Length;
                for (int b = b0; b < b1; b++)
                {
                    float dyn = 0.6f + 0.4f * ArcAt(T(b));
                    for (int k = 0; k < steps; k++)
                    {
                        char c = pat[k];
                        int semi;
                        if (c >= '0' && c <= '3') semi = Up[b][c - '0'] + shift;
                        else if (c == 'b') semi = Bs[b] + 12 + shift;
                        else continue;
                        float v = vel * dyn * (k == 0 ? 1.15f : k % (steps / Beats) == 0 ? 1f : 0.8f) * R.Range(0.9f, 1.08f);
                        Hit("piano", T(b) + k * BarLen / steps + J(7f), PianoS(semi, v > 0.62f), v);
                    }
                }
            }

            /// <summary>Kraftvolle Klavierakzente: tiefe Oktave auf Schlag 1, Akkord auf der Taktmitte.</summary>
            public void PianoHits(int b0, int b1, float vel)
            {
                for (int b = b0; b < b1; b++)
                {
                    float t = T(b);
                    Hit("piano", t, PianoS(Bs[b], true), vel);
                    Hit("piano", t + 0.004f, PianoS(Bs[b] + 12, true), vel * 0.8f);
                    float tm = T(b, Beats >= 4 ? 2f : 1f);
                    for (int v = 0; v < 3; v++) Hit("piano", tm + v * 0.006f, PianoS(Up[b][v], false), vel * 0.45f);
                }
            }

            /// <summary>
            /// Rhythmisches Ostinato. Muster je Takt: R/r Basston, F/f Quinte, O/o Oktave, T/t Terz, U/u oberste, X/x zweithöchste Akkordstimme, '.' Pause; Großbuchstabe = Akzent.
            /// inst: 0 Spiccato, 1 Pizzicato, 2 Posaunenakzent, 3 Klavier. Unterhalb der Schwelle g0 des Spannungsbogens schweigt das Ostinato.
            /// </summary>
            public void Ost(string stem, int b0, int b1, string pat, int inst, float vel, int shift, float g0, float bright)
            {
                int steps = pat.Length;
                for (int b = b0; b < b1; b++)
                {
                    float arc = ArcAt(T(b) + 0.01f);
                    if (arc < g0) continue;
                    float dyn = 0.45f + 0.55f * M.Smooth(M.InvLerp(g0, 1f, arc));
                    int bass = Bs[b] + 12 + shift;
                    var ch = Ch[b];
                    for (int k = 0; k < steps; k++)
                    {
                        char c = pat[k];
                        if (c == '.' || c == ' ') continue;
                        bool acc = char.IsUpper(c);
                        int semi;
                        switch (char.ToUpperInvariant(c))
                        {
                            case 'R': semi = bass; break;
                            case 'F': semi = Above(ch.Root + 7, bass); break;
                            case 'O': semi = bass + 12; break;
                            case 'T': semi = Above(ch.Root + ch.Third, bass); break;
                            case 'U': semi = Up[b][3] + shift; break;
                            case 'X': semi = Up[b][2] + shift; break;
                            default: continue;
                        }
                        float v = (acc ? 1f : 0.55f) * vel * dyn * R.Range(0.88f, 1.1f);
                        float t = T(b) + k * BarLen / steps + J(4f);
                        float[] smp = inst == 0 ? SpiccS(semi, bright) : inst == 1 ? PluckS(semi) : inst == 2 ? StabS(semi) : PianoS(semi, acc);
                        Hit(stem, t, smp, v);
                    }
                }
            }

            /// <summary>Schlagwerk je Takt nach Intensitätsstufe (Spannungsbogen): L/M/S Taiko groß/mittel/klein, T Pauke, A Amboss, C Klappern, K Tick, W Brandung, B Boom.</summary>
            public void Drums(int b0, int b1, string[] levels, float vel, float t1 = 0.35f, float t2 = 0.6f, float t3 = 0.85f)
            {
                for (int b = b0; b < b1; b++)
                {
                    float arc = ArcAt(T(b) + 0.01f);
                    int lvl = arc < t1 ? 0 : arc < t2 ? 1 : arc < t3 ? 2 : 3;
                    Pattern(b, levels[Math.Min(lvl, levels.Length - 1)], vel * (0.6f + 0.4f * arc));
                }
            }

            public void Pattern(int b, string pat, float vel)
            {
                int steps = pat.Length;
                for (int k = 0; k < steps; k++)
                {
                    char c = pat[k];
                    if (c == '.' || c == ' ') continue;
                    var smp = Kit(c, b);
                    float v = (char.IsUpper(c) ? 1f : 0.5f) * vel * R.Range(0.88f, 1.1f);
                    Hit("perc", T(b) + k * BarLen / steps + J(5f), smp, v);
                }
            }

            /// <summary>Trommelwirbel-Steigerung (16tel, dann 32tel im letzten Schlag) von fromBeat bis Taktende.</summary>
            public void Fill(int bar, float fromBeat, float vel)
            {
                float t0 = T(bar, fromBeat), t1 = T(bar + 1);
                var m = Kit('M', bar); var s = Kit('S', bar);
                float step = Beat / 4f;
                int i = 0;
                for (float t = t0; t < t1 - 0.001f; t += step, i++)
                {
                    float u = (t - t0) / Math.Max(0.01f, t1 - t0);
                    Hit("perc", t + J(4f), i % 2 == 0 ? m : s, vel * (0.3f + 0.7f * u) * (i % 4 == 0 ? 1f : 0.75f));
                    if (t > t1 - Beat) Hit("perc", t + step * 0.5f + J(3f), s, vel * 0.5f * u);
                }
            }

            public void TimpRoll(float t0, float t1, float semi, float v0, float v1)
            {
                var smp = TimpS(semi);
                int i = 0;
                for (float t = t0; t < t1; t += 1f / 14f, i++)
                {
                    float u = (t - t0) / Math.Max(0.01f, t1 - t0);
                    Hit("perc", t + J(6f), smp, (v0 + (v1 - v0) * u * u) * (i % 2 == 0 ? 0.32f : 0.26f));
                }
            }

            public void RiserTo(float tEnd, float len, float gain)
            {
                int k = (int)(len * 10);
                Hit("perc", tEnd - k / 10f, Get("ri" + k, () => Riser(Rate, k / 10f, seed + k)), gain);
            }

            /// <summary>Großer Einschlag: Boom, Crash, große Taiko, Rückwärts-Becken davor und optional ein Braam des Taktakkords.</summary>
            public void Impact(float t, int bar, bool braam, float size)
            {
                Hit("perc", t, Kit('B', bar), 0.55f * size);
                Hit("perc", t, Get("crash", () => Crash(Rate, seed + 11)), 0.28f * size);
                Hit("perc", t, Kit('L', bar), 0.9f * size);
                float rl = Math.Min(2.5f, Beat * 2f);
                int k = (int)(rl * 10);
                Hit("perc", t - k / 10f, Get("rv" + k, () => RevSwell(Rate, k / 10f, seed + 12)), 0.3f * size);
                if (braam)
                {
                    var c = Ch[bar];
                    int r = Semi(38f) + Pc(c.Root - Semi(38f));
                    float hz = Hz(r);
                    int key = Key(r);
                    Hit("brass", t, Get("br" + key + ":" + c.Third, () => Braam(hz, c.Third, Rate, key)), 0.75f * size);
                }
            }

            public void Wind(int style, float level, Func<float, float> env = null)
            {
                WindInto(S["wind"], Rate, Loop, style, 0f, level * 2.2f, seed ^ 0x5157, env);
            }

            // ------------------------------------------------------------ Abschluss
            public void Finish(Func<string, float> reverb, float room)
            {
                foreach (var name in lineOrder) RenderLine(lines[name]);
                foreach (var s in StemNames) Reverb(S[s], Rate, reverb(s), room, Loop);
                foreach (var s in StemNames)
                {
                    var b = S[s];
                    for (int i = 0; i < b.Length; i++) if (float.IsNaN(b[i]) || float.IsInfinity(b[i])) b[i] = 0f;
                }
            }
        }

        // ================================================================== Wind
        sealed class WindStyle
        {
            public float BodyLo, BodyHi, BodyGain;
            public float HowlGain, HowlQ = 8f, HowlBase = 300f;
            public float WhistleGain, WhLo = 900f, WhHi = 1800f;
            public float RumbleGain, HissGain, GrainGain;
            public float GustMin = 1.5f, GustMax = 9f;
        }

        /// <summary>Windcharakter: 0 Staub (TERRA), 1 Sand (PYRA), 2 See (PELAGIA), 3 Schneesturm (NIVALIS), 4 sanfter Höhenwind (Menü), 5 ruhige Nachtluft.</summary>
        static WindStyle WindFor(int style)
        {
            switch (style)
            {
                case 1: return new WindStyle { BodyLo = 380, BodyHi = 2400, BodyGain = 0.9f, HowlGain = 0.9f, HowlQ = 7f, HowlBase = 250f, WhistleGain = 0.35f, WhLo = 750, WhHi = 1500, RumbleGain = 0.45f, HissGain = 0.45f, GrainGain = 0.35f, GustMin = 1.2f, GustMax = 7f };
                case 2: return new WindStyle { BodyLo = 280, BodyHi = 1500, BodyGain = 1.0f, HowlGain = 0.45f, HowlQ = 5f, HowlBase = 220f, WhistleGain = 0.12f, WhLo = 900, WhHi = 1400, RumbleGain = 0.55f, HissGain = 0.15f, GrainGain = 0f, GustMin = 2.5f, GustMax = 11f };
                case 3: return new WindStyle { BodyLo = 450, BodyHi = 3200, BodyGain = 0.85f, HowlGain = 1.0f, HowlQ = 11f, HowlBase = 330f, WhistleGain = 0.55f, WhLo = 1100, WhHi = 2600, RumbleGain = 0.35f, HissGain = 0.6f, GrainGain = 0.12f, GustMin = 1.0f, GustMax = 6f };
                case 4: return new WindStyle { BodyLo = 300, BodyHi = 1400, BodyGain = 0.9f, HowlGain = 0.4f, HowlQ = 9f, HowlBase = 290f, WhistleGain = 0.15f, WhLo = 900, WhHi = 1600, RumbleGain = 0.2f, HissGain = 0.1f, GrainGain = 0f, GustMin = 3f, GustMax = 12f };
                case 5: return new WindStyle { BodyLo = 200, BodyHi = 800, BodyGain = 1.0f, HowlGain = 0.15f, HowlQ = 6f, HowlBase = 240f, WhistleGain = 0f, RumbleGain = 0.2f, HissGain = 0.05f, GrainGain = 0f, GustMin = 4f, GustMax = 14f };
                default: return new WindStyle { BodyLo = 320, BodyHi = 1900, BodyGain = 0.9f, HowlGain = 0.6f, HowlQ = 8f, HowlBase = 280f, WhistleGain = 0.2f, WhLo = 850, WhHi = 1700, RumbleGain = 0.35f, HissGain = 0.2f, GrainGain = 0.08f, GustMin = 1.6f, GustMax = 9f };
            }
        }

        /// <summary>
        /// Böiger, gefilterter Rauschwind mit Heulresonanzen, zeitweisem Pfeifen, Grollen, Zischen und Sandkörnern.
        /// storm (0..1) macht ihn dauerhaft stark und rau. Bei loop wird das Ende überblendet auf den Anfang gefaltet (nahtlos).
        /// </summary>
        static void WindInto(float[] dst, int rate, bool loop, int style, float storm, float level, int seed, Func<float, float> env)
        {
            var ws = WindFor(style);
            int n = dst.Length;
            int fold = loop ? Math.Min(n / 4, (int)(1.5f * rate)) : 0;
            int total = n + fold;
            var tmp = new float[total];
            var rng = new Rng(seed);
            float per = (loop ? n : total) / (float)rate;
            float gmin = ws.GustMin * (1f - 0.4f * storm), gmax = ws.GustMax * (1f - 0.3f * storm);
            var gust = new PNoise(rng, per, gmin, gmax, 9);
            var slow = new PNoise(rng, per, 6f, Math.Max(12f, per * 0.5f), 4);
            var hd = new[] { new PNoise(rng, per, 2f, 10f, 5), new PNoise(rng, per, 2.5f, 12f, 5), new PNoise(rng, per, 3f, 14f, 5) };
            var wh = new PNoise(rng, per, 0.8f, 5f, 6);
            var whf = new PNoise(rng, per, 1.2f, 6f, 5);
            float[] hr = { 1f, 1.52f, 2.31f };
            float[] hg = { 1f, 0.7f, 0.45f };
            var hs = new[] { new Svf(), new Svf(), new Svf() };
            var whs = new Svf();
            var hc = new float[3];
            float b1 = 0, b2 = 0, r1 = 0, r2 = 0, h1 = 0, gl = 0;
            uint ns = (uint)seed * 2654435761u | 1u;
            float fdH = 1f / ws.HowlQ, fdW = 1f / 45f;
            float hisA = Pole(3200f, rate);
            float grainDecay = (float)Math.Exp(-1.0 / (0.0006 * rate));
            for (int k0 = 0; k0 < total; k0 += CR)
            {
                float t = k0 / (float)rate;
                float g = M.Clamp01(0.5f + 0.9f * gust.At(t));
                g = g * g * (3 - 2 * g);
                float sl = M.Clamp01(0.55f + 0.8f * slow.At(t));
                float gg = M.Lerp(g * (0.5f + 0.5f * sl), 0.62f + 0.38f * g, storm);
                float e = env != null ? env(t) : 1f;
                float cutA = Pole(ws.BodyLo + (ws.BodyHi - ws.BodyLo) * gg, rate);
                float rumA = Pole(65f + 70f * gg, rate);
                for (int j = 0; j < 3; j++) hc[j] = SvfC(ws.HowlBase * hr[j] * (float)Math.Pow(2, 0.3 * hd[j].At(t) + 0.5 * gg), rate);
                float whOn = M.Clamp01((wh.At(t) - 0.25f) * 3f) * gg;
                float whC = SvfC(ws.WhLo + (ws.WhHi - ws.WhLo) * M.Clamp01(0.5f + 0.9f * whf.At(t)), rate);
                float grainP = ws.GrainGain > 0 ? (0.0008f + 0.012f * gg * gg) * (1f + 2f * storm) : 0f;
                float howlAmt = ws.HowlGain * gg * gg * (1f + storm) * fdH * 2.4f;
                float bodyAmt = ws.BodyGain * (0.2f + gg) * (1f + 0.5f * storm);
                float rumAmt = ws.RumbleGain * (0.3f + gg + storm) * 3f;
                float hissAmt = ws.HissGain * gg * (1f + storm);
                float whAmt = ws.WhistleGain * whOn * fdW * 7f;
                int k1 = Math.Min(total, k0 + CR);
                for (int i = k0; i < k1; i++)
                {
                    ns ^= ns << 13; ns ^= ns >> 17; ns ^= ns << 5;
                    float w = (ns * 2.3283064e-10f) * 2f - 1f;
                    b1 = w + (b1 - w) * cutA; b2 = b1 + (b2 - b1) * cutA;
                    float howl = hs[0].BandC(w, hc[0], fdH) * hg[0] + hs[1].BandC(w, hc[1], fdH) * hg[1] + hs[2].BandC(w, hc[2], fdH) * hg[2];
                    float whis = whOn > 0.001f ? whs.BandC(w, whC, fdW) : 0f;
                    r1 = w + (r1 - w) * rumA; r2 = r1 + (r2 - r1) * rumA;
                    h1 = w + (h1 - w) * hisA;
                    if (grainP > 0 && ((ns >> 8) & 0xFFFF) < grainP * 65536f) gl = (w > 0 ? 1f : -1f) * ws.GrainGain * (0.3f + 0.7f * gg);
                    float grain = gl; gl *= grainDecay;
                    tmp[i] = (b2 * bodyAmt + howl * howlAmt + whis * whAmt + r2 * rumAmt + (w - h1) * hissAmt + grain) * level * e;
                }
            }
            if (loop)
            {
                for (int i = 0; i < n; i++)
                {
                    float v = tmp[i];
                    if (i < fold)
                    {
                        float x = (i + 0.5f) / fold;
                        v = v * (float)Math.Sin(x * Math.PI * 0.5) + tmp[n + i] * (float)Math.Cos(x * Math.PI * 0.5);
                    }
                    dst[i] += v;
                }
            }
            else for (int i = 0; i < n; i++) dst[i] += tmp[i];
        }
    }

    public static partial class Synth
    {
        // ================================================================== Musik (öffentliche Schnittstelle)
        /// <summary>Musik-Stems: Streicherfläche, Klavier, Bass (Ostinato/Sub), Chor, Blechbläser, Schlagwerk, Wind.</summary>
        public static readonly string[] StemNames = { "pad", "piano", "bass", "choir", "brass", "perc", "wind" };

        /// <summary>Alle Musikstücke: Menü, vier Planeten, Abspann.</summary>
        public static readonly string[] MusicIds = { "menu", "terra", "pyra", "pelagia", "nivalis", "ending" };

        public class MusicSet
        {
            public string Id;
            public int Rate;
            public float Length;
            public float Bpm;
            public int BeatsPerBar;
            public Dictionary<string, float[]> Stems = new Dictionary<string, float[]>();
            /// <summary>Effektivwert je Stem nach der Normalisierung.</summary>
            public Dictionary<string, float> Rms = new Dictionary<string, float>();
        }

        public static float RootFor(string id)
        {
            if (id == "menu" || id == "ending") return 146.83f;
            if (GameData.Planets.ContainsKey(id)) return GameData.Planets[id].MusicRoot;
            return 220f;
        }

        static float StemReverb(string s)
        {
            switch (s)
            {
                case "pad": return 0.3f;
                case "piano": return 0.36f;
                case "bass": return 0.1f;
                case "choir": return 0.42f;
                case "brass": return 0.28f;
                case "perc": return 0.24f;
                default: return 0.05f;
            }
        }

        /// <summary>
        /// Erzeugt die sieben Stems eines Stücks („menu“, Planeten-ID oder „ending“) als nahtlosen Loop (Rate 22050, 32–51 s).
        /// Alle Stems gemeinsam normalisiert: Summe aller Stems bei voller Lautstärke erreicht höchstens 0,89 – kein Clipping.
        /// </summary>
        public static MusicSet Music(string id)
        {
            Arr a;
            switch (id)
            {
                case "menu": a = PieceMenu(); break;
                case "pyra": a = PiecePyra(); break;
                case "pelagia": a = PiecePelagia(); break;
                case "nivalis": a = PieceNivalis(); break;
                case "ending": a = PieceEnding(); break;
                default: a = PieceTerra(id ?? "terra"); break;
            }
            bool wide = id == "pelagia" || id == "nivalis";
            a.Finish(s => StemReverb(s) + (wide && s != "bass" && s != "wind" ? 0.08f : 0f), wide ? 0.9f : 0.87f);
            var set = new MusicSet { Id = id, Rate = a.Rate, Length = a.Len, Bpm = a.Bpm, BeatsPerBar = a.Beats };
            SoftLimit(a.S["perc"], PeakOf(a.S["perc"]) * 0.8f);
            var mix = new float[a.N];
            foreach (var s in StemNames) { var b = a.S[s]; for (int i = 0; i < mix.Length; i++) mix[i] += b[i]; }
            float peak = PeakOf(mix);
            float g = peak > 1e-6f ? 0.89f / peak : 1f;
            foreach (var s in StemNames)
            {
                var b = a.S[s];
                double sq = 0;
                for (int i = 0; i < b.Length; i++) { b[i] *= g; sq += b[i] * b[i]; }
                set.Stems[s] = b;
                set.Rms[s] = (float)Math.Sqrt(sq / Math.Max(1, b.Length));
            }
            return set;
        }

        static float PeakOf(float[] b) { float m = 0; foreach (var v in b) { float x = Math.Abs(v); if (x > m) m = x; } return m; }

        static Arr NewLoop(string id, float bpm, int beats, int bars)
        {
            return new Arr(MusicRate, true, bars * beats * 60f / bpm, 0f, (int)Hash.Fnv1a("music:" + id));
        }

        /// <summary>Hauptmenü: episches Hauptthema in d-Moll, Höhepunkt über VI–VII–I nach D-Dur; treibende Sechzehntel-Ostinati.</summary>
        static Arr PieceMenu()
        {
            var a = NewLoop("menu", 84f, 4, 12);
            var ch = new[] { C(0, 'm'), C(8, 'M'), C(3, 'M'), C(10, 'M'), C(0, 'm'), C(8, 'M'), C(5, 'm'), C(7, 'M'), C(8, 'M'), C(10, 'M'), C(0, 'M', true), C(0, 's') };
            var arc = new[] { 0.3f, 0.36f, 0.45f, 0.52f, 0.6f, 0.68f, 0.78f, 0.9f, 1f, 1f, 0.96f, 0.55f };
            a.Section(0f, RootFor("menu"), 84f, 4, ch, arc);
            a.DefStrings(0.65f, 0.16f, 0.35f); a.Strings(0, 12); a.StringsHigh(8, 11);
            a.DefSub(0.3f, 0.45f); a.Sub(0, 12);
            a.Ost("bass", 0, 4, "R.......r.......", 0, 0.8f, 0, 0f, 0.5f);
            a.Ost("bass", 4, 12, "RrrRrrRrRrrRrrRr", 0, 0.9f, 0, 0.45f, 0.55f);
            a.Ost("perc", 8, 11, "UuuUuuUuUuuUuuUu", 0, 0.35f, 0, 0.8f, 0.8f);
            a.PianoMel(0, "24:1.5 31:0.5 31:2 | 27:2 24:2 | 27:2 24:1 22:1 | 22:3 r:1", 0.55f);
            a.PianoArp(4, 8, "0..2..3.", 0.45f, 12);
            a.PianoHits(8, 11, 0.8f);
            a.DefHorns(0.11f, 0.45f, 0.8f, 1.2f); a.Horns(4, 12);
            a.Mel("hm", 4, "12:1.5 19:0.5 19:1 17:0.5 15:0.5 | 15:2 12:1 10:1 | 12:1 14:0.5 15:0.5 17:2 | 14:2 11:1 14:1 | 24:1.5 22:0.5 20:1 17:1 | 17:1 19:1 22:2 | 24:4 | 19:2 17:2", 0, 1f);
            a.Mel("hm2", 8, "24:1.5 22:0.5 20:1 17:1 | 17:1 19:1 22:2 | 24:4", -12, 1f);
            a.DefChoir("ch", 'a', 0.3f, 0.6f, 0.9f); a.Choir("ch", 6, 12);
            a.Drums(0, 12, new[] { "L...............", "L.......M...m...", "L..mL...M..mL.m.", "L.mmL.m.LmM.L.mm" }, 0.8f);
            a.Fill(7, 2f, 0.8f);
            a.TimpRoll(a.T(3, 2), a.T(4), -12, 0.15f, 0.7f);
            a.RiserTo(a.T(8), a.BarLen, 0.35f);
            a.Impact(a.T(8), 8, true, 1f);
            a.Impact(a.T(10), 10, true, 0.7f);
            a.Wind(4, 0.35f);
            return a;
        }

        /// <summary>TERRA: goldene Melancholie → Hoffnung. A-Dur mit Moll-Einfärbungen (iv, bVI, bVII), Klavier, Streicher, Horn.</summary>
        static Arr PieceTerra(string id)
        {
            var a = NewLoop(id, 72f, 4, 12);
            var ch = new[] { C(0, 'M'), Cs(7, 'M', 11), C(9, 'm'), C(5, 'M'), C(5, 'm'), Cs(0, 'M', 4), Cx(2, 'm', 10), C(7, 'M'), C(8, 'M'), C(10, 'M'), C(0, 'M', true), Cs(5, 'M', 0) };
            var arc = new[] { 0.25f, 0.3f, 0.36f, 0.44f, 0.5f, 0.56f, 0.66f, 0.8f, 0.9f, 0.97f, 1f, 0.5f };
            a.Section(0f, RootFor(id), 72f, 4, ch, arc);
            a.DefStrings(0.5f, 0.17f, 0.35f, 0.6f); a.Strings(0, 12); a.StringsHigh(8, 11);
            a.DefSub(0.28f, 0.4f); a.Sub(0, 12);
            a.PianoMel(0, "16:1.5 14:0.5 12:2 | 11:1.5 9:0.5 7:2 | 9:1 12:1 16:1 14:1 | 14:2 9:1 r:1 | 17:1.5 15:0.5 12:1 8:1 | 16:2 12:1 14:1 | 14:1 17:1 21:1 19:1 | 19:2 14:1 11:1", 0.55f);
            a.PianoArp(8, 12, "0.23.1.2", 0.32f, 12);
            a.Ost("bass", 4, 7, "R...r.F.R...r.F.", 0, 0.75f, 0, 0.45f, 0.45f);
            a.Ost("bass", 7, 11, "R.rrR.rrR.rrR.rr", 0, 0.85f, 0, 0.45f, 0.5f);
            a.DefHorns(0.11f, 0.5f, 0.85f, 0.8f); a.Horns(4, 12);
            a.Mel("hc", 4, "8:4 | 7:4 | 5:4 | 7:2 11:2", 0, 0.8f);
            a.Mel("hm", 8, "12:1 15:1 20:2 | 19:1 17:1 22:2 | 24:2 21:1 19:1 | 21:2 17:2", 0, 1f);
            a.Mel("hm2", 8, "12:1 15:1 20:2 | 19:1 17:1 22:2 | 24:2 21:1 19:1", -12, 0.9f);
            a.DefChoir("ch", 'a', 0.3f, 0.68f, 0.95f); a.Choir("ch", 7, 12);
            a.Drums(0, 12, new[] { "................", "L...............", "L.......M...m...", "L..mL...M.m.L.m." }, 0.75f);
            a.TimpRoll(a.T(7, 2), a.T(8), -12, 0.2f, 0.9f);
            a.RiserTo(a.T(8), a.BarLen * 0.75f, 0.3f);
            a.Impact(a.T(8), 8, false, 0.8f);
            a.Impact(a.T(10), 10, true, 0.75f);
            a.Wind(0, 0.4f);
            return a;
        }

        /// <summary>PYRA: rau, heiß, industriell. g-Moll/dorisch, stampfende Taiko, Ambosse, Posaunen, tiefer Männerchor, heulender Wüstenwind.</summary>
        static Arr PiecePyra()
        {
            var a = NewLoop("pyra", 100f, 4, 16);
            var ch = new[] { C(0, 'm'), C(0, 'm'), C(8, 'M'), C(10, 'M'), C(0, 'm'), C(5, 'M'), C(8, 'M'), C(7, 'M'), C(0, 'm'), C(3, 'M'), C(10, 'M'), C(7, 'M'), C(8, 'M'), C(5, 'M'), C(7, 's'), C(7, 'M') };
            var arc = new[] { 0.3f, 0.33f, 0.4f, 0.45f, 0.55f, 0.6f, 0.7f, 0.82f, 1f, 0.95f, 1f, 0.9f, 0.7f, 0.6f, 0.5f, 0.42f };
            a.Section(0f, RootFor("pyra"), 100f, 4, ch, arc);
            a.DefStrings(0.3f, 0.15f, 0.3f, 0.45f); a.Strings(0, 16); a.StringsHigh(8, 12);
            a.DefSub(0.32f, 0.5f); a.Sub(0, 16);
            a.Ost("bass", 0, 16, "RrrRrrRrRrrRrrRr", 0, 0.9f, 0, 0f, 0.35f);
            a.Ost("brass", 4, 8, "R...............", 2, 0.4f, 0, 0.5f, 0f);
            a.Ost("brass", 8, 12, "R..R..R.....R...", 2, 0.55f, 0, 0.8f, 0f);
            a.Ost("piano", 0, 4, "..u...u...u...u.", 3, 0.3f, 12, 0f, 0f);
            a.PianoHits(4, 16, 0.75f);
            a.DefHorns(0.12f, 0.55f, 0.85f, 1.8f); a.Horns(6, 14);
            a.Mel("hm", 8, "7:1.5 10:0.5 12:2 | 15:1.5 14:0.5 10:2 | 12:1 10:1 14:2 | 14:2 11:2", 0, 1f);
            a.Mel("hm2", 8, "7:1.5 10:0.5 12:2 | 15:1.5 14:0.5 10:2 | 12:1 10:1 14:2 | 14:2 11:2", -12, 1f);
            a.DefChoir("ch", 'a', 0.32f, 0.6f, 0.9f); a.Choir("ch", 6, 14, -12);
            a.Drums(0, 16, new[] { "L.......c.......", "L.....m.A...L.c.", "L..mL.m.A.mmL.c.", "L.mmL.mLA.mmLmcA" }, 0.8f, 0.35f, 0.58f, 0.85f);
            a.Drums(0, 16, new[] { "..c...c...c...c.", "..c...c...c...c.", "..c.C.c...c.C.c.", "c.c.C.c.c.c.C.c." }, 0.35f);
            a.Fill(7, 2f, 0.85f);
            a.RiserTo(a.T(8), a.BarLen, 0.35f);
            a.Impact(a.T(8), 8, true, 1f);
            a.Impact(a.T(10), 10, true, 0.8f);
            a.Impact(a.T(12), 12, false, 0.6f);
            a.Wind(1, 0.45f);
            return a;
        }

        /// <summary>PELAGIA: weit, atmend, ozeanisch. H-lydisch im 3/4-Takt, zwei Wellenbögen, Chor, Wellen-Swells, walartige Gleitklänge.</summary>
        static Arr PiecePelagia()
        {
            var a = NewLoop("pelagia", 66f, 3, 14);
            var ch = new[] { C(0, 'M', true), Cs(2, 'M', 0), Cx(0, 'M', 11), Cs(2, 'M', 0), C(9, 'm'), C(4, 'm'), C(2, 'M'), C(7, 'M'), C(0, 'M', true), Cs(2, 'M', 0), C(9, 'm'), C(4, 'm'), C(2, 'M'), C(7, 's') };
            var arc = new[] { 0.3f, 0.4f, 0.52f, 0.65f, 0.75f, 0.62f, 0.45f, 0.4f, 0.55f, 0.72f, 0.88f, 1f, 0.8f, 0.5f };
            a.Section(0f, RootFor("pelagia"), 66f, 3, ch, arc);
            a.DefStrings(0.55f, 0.16f, 0.35f, 0.9f); a.Strings(0, 14); a.StringsHigh(10, 13);
            var sw = new Spec { Kind = 4, Saws = 2, Detune = 0.004f, Attack = 1.3f, Release = 1.6f, Glide = 0.3f, CutPitch = 2.2f, VibDepth = 0f, Accent = 0f };
            a.Line("swell", "bass", sw, 0.36f, 0.5f, 1f, 0f, 1f);
            for (int b = 0; b < 14; b += 2) a.Note("swell", a.T(b), a.BarLen * 1.15f, a.Bs[b], 1f);
            a.Ost("bass", 2, 14, "R...F...O...", 1, 0.8f, 0, 0.38f, 0f);
            a.PianoMel(0, "12:1 14:1 16:1 | 18:2 16:1 | 14:1 11:2 | 9:3 | 11:1 12:1 16:1 | 16:2 14:1 | 14:3", 0.5f);
            a.PianoArp(7, 14, "b.2.3.", 0.3f, 12);
            a.DefChoir("co", 'o', 0.3f, 0.3f, 0.65f, 1.4f); a.Choir("co", 0, 14);
            a.DefChoir("ca", 'a', 0.26f, 0.8f, 0.97f); a.Choir("ca", 9, 13);
            var wh = new Spec { Kind = 1, Saws = 2, Detune = 0.003f, Attack = 1.1f, Release = 1.8f, Glide = 0.75f, VibDepth = 0.009f, VibRate = 1.4f, VibDelay = 0.4f, Breath = 0.05f, FormQ = 4f, Form = VowelU, FormGain = VowelUGain, Accent = 0f };
            a.Line("whale", "brass", wh, 0.5f, 0.6f, 1f, 0f, 1f);
            a.Mel("whale", 2, "-5:4.5 2:3 -3:1.5", 0, 1f, 1.05f);
            a.Mel("whale", 8, "0:4 7:3 2:2", 0, 1f, 1.05f);
            a.DefHorns(0.09f, 0.55f, 0.85f, 0.35f); a.Horns(3, 6); a.Horns(9, 14);
            a.Mel("hm", 9, "18:2 16:1 | 16:1 14:1 12:1 | 16:2 11:1 | 14:3", 0, 0.8f);
            a.Drums(0, 14, new[] { "W...........", "W.....w.....", "L.....W.....", "L..m..L.m.W." }, 0.6f, 0.4f, 0.62f, 0.86f);
            a.TimpRoll(a.T(3, 1), a.T(4), -12, 0.1f, 0.6f);
            a.TimpRoll(a.T(10, 1), a.T(11), -12, 0.15f, 0.85f);
            a.RiserTo(a.T(11), a.BarLen, 0.22f);
            a.Impact(a.T(11), 11, false, 0.6f);
            a.Wind(2, 0.6f);
            return a;
        }

        /// <summary>NIVALIS: eisig, erhaben, Finale. f-Moll, hohe Flageoletts, kalter Chor, spärliches Klavier, Blechchoral, Schneesturm.</summary>
        static Arr PieceNivalis()
        {
            var a = NewLoop("nivalis", 64f, 4, 10);
            var ch = new[] { C(0, 'm', true), Cx(8, 'M', 11), C(3, 'M'), C(10, 'M'), C(0, 'm'), C(8, 'M'), C(5, 'm'), C(7, 'M'), C(8, 'M', true), C(10, 'M') };
            var arc = new[] { 0.2f, 0.25f, 0.32f, 0.4f, 0.5f, 0.62f, 0.78f, 0.9f, 1f, 0.6f };
            a.Section(0f, RootFor("nivalis"), 64f, 4, ch, arc);
            a.DefFlag(0.13f, 0.6f);
            for (int b = 0; b < 10; b++)
            {
                a.Note("fl0", a.T(b), a.BarLen + 0.1f, a.Up[b][3] + 12, 1f);
                a.Note("fl1", a.T(b), a.BarLen + 0.1f, a.Up[b][1] + 24, 1f);
            }
            a.DefStrings(0.9f, 0.16f, 0.22f, 0.8f); a.Strings(0, 10); a.StringsHigh(6, 9);
            a.DefSub(0.3f, 0.35f); a.Sub(0, 10);
            a.PianoMel(0, "31:2 r:2 | 27:1.5 24:2.5 | r:1 31:1 27:2 | 26:4 | 24:2 31:2 | 32:2 27:2", 0.42f);
            a.DefChoir("co", 'o', 0.24f, 0.25f, 0.55f, 1.6f); a.Choir("co", 2, 10);
            a.DefChoir("ca", 'a', 0.26f, 0.75f, 0.95f); a.Choir("ca", 6, 10);
            a.DefHorns(0.11f, 0.45f, 0.8f, 0.6f); a.Horns(4, 10);
            a.Mel("hm", 6, "17:2 20:1 17:1 | 19:2 23:2 | 24:3 27:1 | 26:2 22:2", 0, 1f);
            a.Mel("hm2", 7, "19:2 23:2 | 24:3 27:1", -12, 0.9f);
            a.Ost("bass", 4, 6, "R.......R.......", 0, 0.7f, 0, 0.45f, 0.5f);
            a.Ost("bass", 6, 9, "R.rrR.rrR.rrR.rr", 0, 0.85f, 0, 0.6f, 0.55f);
            a.Drums(0, 10, new[] { "................", "T...............", "L.......T.......", "L..mL...L.m.L.mm" }, 0.8f);
            a.TimpRoll(a.T(7, 2), a.T(8), -12, 0.2f, 1f);
            a.RiserTo(a.T(8), a.BarLen, 0.3f);
            a.Impact(a.T(6), 6, false, 0.5f);
            a.Impact(a.T(8), 8, true, 1f);
            a.Wind(3, 0.5f);
            return a;
        }

        /// <summary>Abspann: triumphal-hoffnungsvoll. Das Hauptthema des Menüs in D-Dur.</summary>
        static Arr PieceEnding()
        {
            var a = NewLoop("ending", 76f, 4, 16);
            var ch = new[] { C(0, 'M', true), Cs(7, 'M', 11), C(9, 'm'), C(5, 'M'), Cs(0, 'M', 4), C(5, 'M'), C(7, 's'), C(7, 'M'), C(8, 'M'), C(10, 'M'), C(0, 'M'), Cx(0, 'M', 11), C(5, 'M'), C(7, 'M'), C(0, 'M', true), Cs(5, 'M', 0) };
            var arc = new[] { 0.3f, 0.34f, 0.4f, 0.48f, 0.55f, 0.6f, 0.68f, 0.8f, 0.9f, 0.96f, 1f, 1f, 0.9f, 0.85f, 0.72f, 0.45f };
            a.Section(0f, RootFor("ending"), 76f, 4, ch, arc);
            a.DefStrings(0.75f, 0.16f, 0.35f); a.Strings(0, 16); a.StringsHigh(8, 14);
            a.DefSub(0.28f, 0.45f); a.Sub(0, 16);
            a.PianoMel(0, "24:1.5 31:0.5 31:1 29:0.5 28:0.5 | 26:2 23:1 26:1 | 24:1 26:1 28:2 | 29:2 33:1 31:1", 0.55f);
            a.PianoArp(4, 8, "0..2..3.", 0.42f, 12);
            a.PianoArp(12, 16, "0.2.3.2.", 0.3f, 12);
            a.Ost("bass", 4, 14, "R.rrR.rrR.rrR.rr", 0, 0.85f, 0, 0.45f, 0.55f);
            a.Ost("perc", 8, 12, "UuuUuuUuUuuUuuUu", 0, 0.3f, 0, 0.85f, 0.8f);
            a.DefHorns(0.11f, 0.45f, 0.8f, 1.0f); a.Horns(4, 16);
            a.Mel("hm", 4, "19:1.5 24:0.5 24:2 | 24:1 23:1 21:2 | 21:1 19:1 17:2 | 16:2 14:1 11:1 | 24:1.5 22:0.5 20:1 17:1 | 17:1 19:1 22:2 | 24:4 | 28:2 26:1 24:1 | 26:2 24:1 21:1 | 19:2 23:2 | 24:4", 0, 1f);
            a.Mel("hm2", 8, "24:1.5 22:0.5 20:1 17:1 | 17:1 19:1 22:2 | 24:4 | 28:2 26:1 24:1", -12, 0.9f);
            a.DefChoir("ch", 'a', 0.3f, 0.6f, 0.9f); a.Choir("ch", 7, 16);
            a.Drums(0, 16, new[] { "................", "L.......M.......", "L..mL...M..mL...", "L.mmL.m.LmM.L.mm" }, 0.75f);
            a.Fill(7, 2f, 0.8f);
            a.RiserTo(a.T(8), a.BarLen, 0.3f);
            a.Impact(a.T(8), 8, true, 0.9f);
            a.Impact(a.T(10), 10, true, 1f);
            a.TimpRoll(a.T(13, 2), a.T(14), -12, 0.2f, 0.7f);
            a.Impact(a.T(14), 14, false, 0.6f);
            a.Wind(4, 0.3f);
            return a;
        }

        /// <summary>Stückweise lineare Kurve aus Paaren (Zeit, Wert).</summary>
        static float Pw(float t, float[] p)
        {
            if (t <= p[0]) return p[1];
            for (int i = 2; i < p.Length; i += 2)
                if (t <= p[i]) return p[i - 1] + (p[i + 1] - p[i - 1]) * (t - p[i - 2]) / Math.Max(1e-4f, p[i] - p[i - 2]);
            return p[p.Length - 1];
        }

        /// <summary>
        /// Cinematischer Intro-Score (100 s + 4 s Ausklang), synchron zu IntroTimeline:
        /// 0–14 Einsamkeit, 14–30 mechanischer Aufbau, 30–46 Archen (erster Höhepunkt), 46–60 Leere,
        /// 60–75 MIKOs Alltag (warm), 75–88 Keimling (Staunen → Steigerung), 88–100 Hauptthema in D-Dur (großer Höhepunkt).
        /// </summary>
        public static float[] IntroScore(out int rate, out float length)
        {
            rate = MusicRate;
            length = IntroTimeline.Total;
            var a = new Arr(rate, false, length, 4f, 0x1A7B0);
            const float A = 220f, D = 146.83f;

            // ---------------- 0–14: Skyline – Bordun, hohe Flageoletts, einsames Klavier
            a.Section(0f, A, 60f, 4, new[] { C(0, 'm'), C(8, 'M'), C(5, 'm'), C(7, 'M') }, new[] { 0.1f, 0.14f, 0.18f, 0.24f });
            a.DefStrings(0.5f, 0.17f, 0.28f, 0.8f);
            a.DefSub(0.3f, 0.4f);
            a.DefFlag(0.1f, 0.7f);
            a.DefChoir("ch", 'a', 0.3f, 0.55f, 0.9f);
            a.DefChoir("co", 'o', 0.28f, 0.3f, 0.6f, 1.4f);
            a.DefHorns(0.11f, 0.45f, 0.85f, 1.1f);
            var trem = new Spec { Kind = 0, Saws = 4, Detune = 0.006f, Attack = 0.8f, Release = 1.2f, CutLo = 1400f, CutHi = 4200f, VibDepth = 0.002f, Tremolo = 0.55f, Accent = 0f };
            a.Line("trem", "pad", trem, 0.12f, 0f, 1f, 0.4f, 0.75f);
            a.Hit("perc", 0.05f, a.Kit('B', 0), 0.55f);
            a.Note("sub", 0f, 14f, -24, 1f);
            a.Note("fl0", 1.5f, 12.5f, 31, 0.8f);
            a.Strings(0, 4, 0.7f);
            a.PianoMel(0, "r:2 19:1.5 15:0.5 | 17:1 19:1 22:1.5 20:0.5 | 19:1 17:1 15:2 | r:2", 0.5f);
            a.RiserTo(14f, 1.5f, 0.12f);

            // ---------------- 14–30: KONSUMA – mechanisches Ostinato, Ticken, wachsende Spannung
            a.Section(14f, A, 90f, 4, new[] { C(0, 'm'), C(8, 'M'), C(5, 'm'), C(7, 'M'), C(0, 'm'), C(7, 'M') }, new[] { 0.35f, 0.42f, 0.5f, 0.58f, 0.68f, 0.8f });
            a.Strings(0, 6); a.Sub(0, 6);
            a.Ost("bass", 0, 6, "RrrRrrRrRrrRrrRr", 0, 0.85f, 0, 0f, 0.45f);
            a.Drums(0, 6, new[] { "K.k.K.k.K.k.K.k." }, 0.3f);
            a.Ost("brass", 2, 6, "R.......R.......", 2, 0.45f, 0, 0f, 0f);
            a.Pattern(2, "L...............", 0.7f);
            a.Pattern(3, "L.......L.......", 0.75f);
            a.Pattern(4, "L..mL...L..mL...", 0.8f);
            a.Fill(5, 0f, 0.8f);
            a.Horns(3, 6);
            a.PianoHits(4, 6, 0.55f);
            a.RiserTo(30f, 2.6f, 0.35f);

            // ---------------- 30–46: Die Archen starten – Braam, Chor, Blech, volle Trommeln
            a.Section(30f, A, 90f, 4, new[] { C(0, 'm'), C(8, 'M'), C(3, 'M'), C(10, 'M'), C(8, 'M'), C(7, 'M') }, new[] { 0.92f, 0.95f, 0.97f, 1f, 1f, 0.8f });
            a.Impact(30f, 0, true, 1f);
            a.Strings(0, 6); a.StringsHigh(0, 5); a.Sub(0, 6);
            a.Choir("ch", 0, 6);
            a.Horns(0, 6);
            const string tragic = "12:1.5 15:0.5 19:2 | 20:2 19:1 15:1 | 19:1.5 17:0.5 15:2 | 14:4 | 17:2 15:1 12:1 | 11:4";
            a.Mel("hm", 0, tragic, 0, 1f);
            a.Mel("hm2", 0, tragic, -12, 0.9f);
            a.Ost("bass", 0, 6, "RrrRrrRrRrrRrrRr", 0, 1f, 0, 0f, 0.55f);
            a.Ost("perc", 0, 4, "UuuUuuUuUuuUuuUu", 0, 0.3f, 0, 0f, 0.8f);
            for (int b = 0; b < 4; b++) a.Pattern(b, "L.mmL.m.LmM.L.mm", 0.95f);
            a.Pattern(4, "L...L...L...L...", 1f);
            a.Impact(a.T(5), 5, true, 0.9f);
            a.ArcPoint(44.5f, 0.6f); a.ArcPoint(46f, 0.12f);

            // ---------------- 46–60: Roboter schalten ab – Leere, fallende Einzeltöne
            a.ArcPoint(50f, 0.1f); a.ArcPoint(58f, 0.12f);
            a.Note("fl0", 47f, 11f, 31, 0.6f);
            a.Note("sub", 46.5f, 9f, -24, 0.5f);
            float[] fall = { 19, 17, 15, 14, 12 };
            for (int k = 0; k < fall.Length; k++) a.Hit("piano", 47f + k * 2.6f, a.PianoS(fall[k], false), 0.45f - k * 0.05f);

            // ---------------- 60–75: MIKO arbeitet weiter – warmes A-Dur, Pizzicato „Würfel für Würfel“
            a.Section(60f, A, 64f, 4, new[] { C(0, 'M'), Cs(7, 'M', 11), C(9, 'm'), C(5, 'M') }, new[] { 0.3f, 0.34f, 0.38f, 0.42f });
            a.Strings(0, 4, 0.9f); a.Sub(0, 4);
            a.Ost("bass", 0, 4, "R.F.O.F.R.F.O.F.", 1, 0.7f, 0, 0f, 0f);
            a.PianoMel(0, "16:1.5 14:0.5 12:2 | 11:1.5 9:0.5 7:2 | 9:1 12:1 16:1 14:1 | 14:2 9:1 r:1", 0.5f);

            // ---------------- 75–88: Der Keimling – Staunen, Chor, Tremolo, dann Steigerung
            a.Section(75f, A, 240f / 3.25f, 4, new[] { C(8, 'M'), C(10, 'M'), C(0, 's'), C(0, 'M') }, new[] { 0.42f, 0.5f, 0.62f, 0.78f });
            a.ArcPoint(87.9f, 0.95f);
            a.Strings(0, 4); a.StringsHigh(1, 4); a.Sub(0, 4);
            for (int b = 0; b < 4; b++) a.Note("trem", a.T(b), a.BarLen + 0.06f, a.Up[b][3] + 12, 1f);
            a.Choir("co", 0, 4);
            a.Choir("ch", 2, 4);
            a.PianoArp(0, 3, "0.1.2.3.", 0.3f, 12);
            a.Horns(2, 4);
            a.Ost("bass", 2, 4, "R.rrR.rrR.rrR.rr", 0, 0.8f, 0, 0f, 0.5f);
            a.Pattern(2, "L.......M.......", 0.6f);
            a.Fill(3, 1f, 0.9f);
            a.TimpRoll(84.75f, 88f, -12, 0.15f, 1f);
            a.RiserTo(88f, 3.2f, 0.45f);

            // ---------------- 88–100: PROGRAMM ZWEITE CHANCE – Hauptthema in D-Dur, großer Höhepunkt
            a.Section(88f, D, 84f, 4, new[] { C(0, 'M', true), C(8, 'M'), C(10, 'M'), C(0, 'M', true) }, new[] { 1f, 1f, 1f, 0.97f });
            a.Impact(88f, 0, true, 1f);
            a.Strings(0, 3); a.StringsHigh(0, 3); a.Sub(0, 3);
            a.Choir("ch", 0, 3); a.Horns(0, 3);
            float tf = a.T(3), fl = 100.6f - tf;
            for (int v = 0; v < 4; v++) a.Note("str" + v, tf, fl, a.Up[3][v], 1f);
            a.Note("strB", tf, fl, a.Bs[3] + 12, 1f);
            a.Note("strHi", tf, fl, a.Up[3][3] + 12, 1f);
            a.Note("sub", tf, fl, a.Bs[3], 1f);
            for (int v = 0; v < 3; v++) { a.Note("ch" + v, tf, fl, a.Up[3][v + 1], 1f); a.Note("hn" + v, tf, fl, a.Up[3][v] + a.HornShift, 1f); }
            const string theme = "12:1.5 19:0.5 19:1 17:0.5 16:0.5 | 24:1.5 22:0.5 20:1 17:1 | 17:1 19:1 22:2 | 24:5";
            a.Mel("hm", 0, theme, 0, 1f);
            a.Mel("hm2", 0, theme, -12, 0.95f);
            a.Ost("bass", 0, 3, "RrrRrrRrRrrRrrRr", 0, 1f, 0, 0f, 0.6f);
            a.Ost("perc", 0, 3, "UuuUuuUuUuuUuuUu", 0, 0.32f, 0, 0f, 0.85f);
            a.Pattern(0, "L.mmL.m.LmM.L.mm", 1f);
            a.Pattern(1, "L.mmL.m.LmM.L.mm", 1f);
            a.Pattern(2, "L.mmL.m.LmMmLmMm", 1f);
            a.Fill(2, 3f, 1f);
            a.Impact(tf, 3, true, 1.1f);
            a.Pattern(3, "L.......L.......", 0.7f);
            a.ArcPoint(99.5f, 0.9f); a.ArcPoint(102f, 0.2f); a.ArcPoint(104f, 0f);

            // Wind: Einsamkeit am Anfang und in der Leere, sonst im Hintergrund
            float[] wenv = { 0f, 0f, 1.5f, 0.75f, 13f, 0.6f, 15f, 0.25f, 29f, 0.2f, 31f, 0.1f, 45f, 0.12f, 47f, 0.7f, 59f, 0.75f, 61f, 0.3f, 74f, 0.25f, 76f, 0.15f, 88f, 0.08f, 104f, 0.05f };
            a.Wind(0, 0.5f, t => Pw(t, wenv));

            a.Finish(s => StemReverb(s) + (s == "bass" || s == "wind" ? 0f : 0.06f), 0.88f);
            var o = new float[a.N];
            foreach (var s in StemNames) { var b = a.S[s]; for (int i = 0; i < o.Length; i++) o[i] += b[i]; }
            float peak = PeakOf(o);
            if (peak > 1e-6f) { float g = 0.9f / peak; for (int i = 0; i < o.Length; i++) o[i] *= g; }
            int fe = Math.Min(o.Length, (int)(0.5f * rate));
            for (int i = 0; i < fe; i++) o[o.Length - 1 - i] *= i / (float)fe;
            return o;
        }

        // ================================================================== Soundeffekte
        public static readonly string[] SfxIds =
        {
            "glass", "plastic", "paper", "metal", "magnet_charge", "magnet_wave", "vacuum_loop", "cut_loop", "heat_loop", "filter_loop",
            "bale", "press", "coin", "beep_happy", "beep_sad", "beep_curious", "beep_ok", "beep_error", "ui_click", "ui_hover", "ui_back",
            "build", "repair", "plant", "splash", "bubbles", "ice_crack", "steam", "awaken", "gate_open", "whoosh", "drone_loop",
            "engine_loop", "machine_loop", "wind_loop", "water_loop", "storm_loop", "mission", "lore", "wheels_loop", "mission_new",
            "crane", "unload", "grab", "zone",
            "wind_terra", "wind_pyra", "wind_pelagia", "wind_nivalis", "storm_terra", "storm_pyra", "storm_pelagia", "storm_nivalis",
            "night_ambience", "thunder"
        };

        /// <summary>true für lange Ambience-Loops (werden im Spiel erst bei Bedarf geladen).</summary>
        public static bool IsAmbience(string id)
        {
            return id.StartsWith("wind_", StringComparison.Ordinal) || id.StartsWith("storm_", StringComparison.Ordinal) || id == "water_loop" || id == "night_ambience";
        }

        static int WindStyleOf(string id)
        {
            if (id.EndsWith("pyra", StringComparison.Ordinal)) return 1;
            if (id.EndsWith("pelagia", StringComparison.Ordinal)) return 2;
            if (id.EndsWith("nivalis", StringComparison.Ordinal)) return 3;
            return 0;
        }

        /// <summary>Glockenähnlicher FM-Klang – nur noch für kurze Oberflächen-Signale (Münzen, Hinweise), nicht in der Musik.</summary>
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

        static float[] PianoFx(float f, float vel, int rate, int seed) { var p = PianoTone(f, rate, 0.6f, seed); for (int i = 0; i < p.Length; i++) p[i] *= vel; return p; }

        /// <summary>Addiert ein Ereignis zyklisch (für nahtlose Loops).</summary>
        static void AddWrap(float[] dst, int start, float[] src, float gain) { Add(dst, start, src, gain, true); }

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
                        // Projekt abgeschlossen: kurzer orchestraler Aufschwung (Streicher, Chor, Horn, Klavierfunkeln, Pauke) – ohne Glocken
                        var a = new Arr(MusicRate, false, 5f, 1.5f, 77);
                        a.Section(0f, 261.63f, 60f, 4, new[] { C(0, 'M', true), C(0, 'M', true) }, new[] { 0.45f, 0.9f });
                        a.ArcPoint(0.9f, 1f); a.ArcPoint(3.5f, 0.7f); a.ArcPoint(6f, 0.25f);
                        a.DefStrings(0.8f, 0.18f, 0.3f, 0.35f);
                        a.DefChoir("ch", 'a', 0.3f, 0f, 0.5f, 0.6f);
                        a.DefHorns(0.12f, 0f, 0.5f, 0.8f);
                        a.DefSub(0.3f, 0.5f);
                        for (int v = 0; v < 4; v++) a.Note("str" + v, 0f, 3.2f, a.Up[0][v], 1f);
                        a.Note("strB", 0f, 3.2f, a.Bs[0] + 12, 1f);
                        a.Note("sub", 0f, 3.2f, a.Bs[0], 1f);
                        for (int v = 0; v < 3; v++) { a.Note("ch" + v, 0.15f, 3f, a.Up[0][v + 1], 1f); a.Note("hn" + v, 0.2f, 3f, a.Up[0][v] + a.HornShift, 1f); }
                        int[] sparkle = { 24, 28, 31, 35, 38 };
                        for (int k = 0; k < sparkle.Length; k++) a.Hit("piano", 0.9f + k * 0.12f, a.PianoS(sparkle[k], false), 0.45f);
                        a.Hit("perc", 0f, a.TimpS(-12), 0.7f);
                        a.Hit("perc", 0f, a.Kit('B', 0), 0.4f);
                        a.Finish(s => StemReverb(s) + 0.1f, 0.86f);
                        var o = new float[a.N];
                        foreach (var s in StemNames) { var b = a.S[s]; for (int i = 0; i < o.Length; i++) o[i] += b[i]; }
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
                case "wind_terra":
                case "wind_pyra":
                case "wind_pelagia":
                case "wind_nivalis":
                case "storm_loop":
                case "storm_terra":
                case "storm_pyra":
                case "storm_pelagia":
                case "storm_nivalis":
                    {
                        bool storm = id.StartsWith("storm", StringComparison.Ordinal);
                        int style = WindStyleOf(id);
                        var o = new float[(int)(10f * r)];
                        WindInto(o, r, true, style, storm ? 1f : 0f, 1f, (int)Hash.Fnv1a(id), null);
                        if (storm) StormExtras(o, r, style, rng);
                        NormalizeRms(o, storm ? 0.2f : 0.13f, 0.95f);
                        return o;
                    }
                case "water_loop": return WaterLoop(r, rng);
                case "night_ambience": return NightAmbience(r, rng);
                case "thunder": return Thunder(r, rng);
                case "mission":
                case "zone":
                    {
                        var o = new float[(int)(2.0f * r / 2)];
                        int rr = r / 2;
                        float root = id == "zone" ? 392f : 523.25f;
                        int[] seq = id == "zone" ? new[] { 0, 7, 12 } : new[] { 0, 4, 7, 12 };
                        for (int k = 0; k < seq.Length; k++) Add(o, (int)(k * 0.1f * rr), PianoFx(Freq(root, seq[k]), 0.5f, rr, k), 0.6f, false);
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

        /// <summary>Planetentypische Sturmzutaten: Trümmerklappern (TERRA), Blechschlagen (PYRA), Regen/Gischt (PELAGIA), Graupel (NIVALIS).</summary>
        static void StormExtras(float[] o, int r, int style, Rng rng)
        {
            int n = o.Length;
            if (style == 2)
            {
                // Regen: dichte, hochpassgefilterte Tropfen + Gischtwellen (periodisch → nahtlos)
                var lp = new OnePole(); float la = Pole(2500f, r);
                var sp = new PNoise(rng, n / (float)r, 2.5f, 5f, 4);
                int fold = r;
                var tmp = new float[n + fold];
                var lp2 = new OnePole();
                for (int i = 0; i < tmp.Length; i++)
                {
                    float t = i / (float)r;
                    float w = rng.Next() * 2 - 1;
                    float hp = w - lp.LpA(w, la);
                    float drop = rng.Next() < 0.004f ? (rng.Next() * 2 - 1) * 2f : 0f;
                    float spray = M.Clamp01(0.4f + sp.At(t)) ;
                    tmp[i] = hp * 0.35f + drop * 0.3f + lp2.LpA(w, Pole(900f + 1500f * spray, r)) * spray * spray * 0.9f;
                }
                for (int i = 0; i < n; i++)
                {
                    float v = tmp[i];
                    if (i < fold) { float x = (i + 0.5f) / fold; v = v * (float)Math.Sin(x * Math.PI * 0.5) + tmp[n + i] * (float)Math.Cos(x * Math.PI * 0.5); }
                    o[i] += v * 0.08f;
                }
                return;
            }
            // Ereignisse (zyklisch addiert): Klappern/Blech/Graupel
            int count = style == 1 ? 7 : style == 3 ? 18 : 10;
            for (int k = 0; k < count; k++)
            {
                int st = rng.Range(0, n);
                int len = (int)((style == 1 ? 0.5f : 0.12f) * r);
                var ev = new float[len];
                var bp = new Svf();
                float f = style == 1 ? rng.Range(300f, 700f) : style == 3 ? rng.Range(3000f, 6000f) : rng.Range(1200f, 3000f);
                float c = SvfC(f, r);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)r;
                    float x = (rng.Next() * 2 - 1) * (float)Math.Exp(-t / (style == 1 ? 0.01f : 0.003f));
                    ev[i] = bp.BandC(x, c, style == 1 ? 0.04f : 0.3f) * (style == 1 ? 0.12f : 0.5f) + x * 0.3f;
                }
                AddWrap(o, st, ev, style == 1 ? 0.05f : 0.03f);
            }
        }

        /// <summary>Wellen am Ufer: Brandung, Schwappen, vereinzelte Spritzer (10-s-Loop).</summary>
        static float[] WaterLoop(int r, Rng rng)
        {
            int n = 10 * r, fold = r;
            var tmp = new float[n + fold];
            float per = n / (float)r;
            var drift = new PNoise(rng, per, 1.5f, 5f, 6);
            var lp1 = new OnePole(); var lp2 = new OnePole(); var bp = new Svf();
            float wc = SvfC(2400f, r);
            for (int k0 = 0; k0 < tmp.Length; k0 += CR)
            {
                float t = k0 / (float)r;
                float wv = 0.5f + 0.5f * (float)Math.Sin(TwoPi * 2 * t / per + 0.7);
                wv = M.Clamp01(wv * 0.85f + 0.3f * drift.At(t) + 0.1f);
                float wash = (float)Math.Pow(M.Clamp01(0.5f + 0.5f * (float)Math.Sin(TwoPi * 2 * t / per - 0.6)), 3);
                float la = Pole(250f + 1300f * wv, r);
                int k1 = Math.Min(tmp.Length, k0 + CR);
                for (int i = k0; i < k1; i++)
                {
                    float w = rng.Next() * 2 - 1;
                    float surf = lp2.LpA(lp1.LpA(w, la), la) * (0.3f + 0.7f * wv) * 2f;
                    tmp[i] = surf + bp.BandC(w, wc, 1.2f) * wash * 0.25f;
                }
            }
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = tmp[i];
                if (i < fold) { float x = (i + 0.5f) / fold; v = v * (float)Math.Sin(x * Math.PI * 0.5) + tmp[n + i] * (float)Math.Cos(x * Math.PI * 0.5); }
                o[i] = v;
            }
            // Schwappen und Tropfen
            for (int k = 0; k < 14; k++)
            {
                int len = (int)(rng.Range(0.06f, 0.15f) * r);
                var ev = new float[len];
                var lp = new OnePole(); float la = Pole(rng.Range(700f, 1800f), r);
                for (int i = 0; i < len; i++) { float u = i / (float)len; ev[i] = lp.LpA(rng.Next() * 2 - 1, la) * (float)Math.Sin(Math.PI * u) ; }
                AddWrap(o, rng.Range(0, n), ev, 0.5f);
            }
            for (int k = 0; k < 6; k++)
            {
                int len = (int)(0.06f * r);
                var ev = new float[len];
                float f = rng.Range(700f, 1500f);
                for (int i = 0; i < len; i++) { float t = i / (float)r; ev[i] = (float)Math.Sin(TwoPi * (f * t + 2500 * t * t)) * (float)Math.Exp(-t * 60); }
                AddWrap(o, rng.Range(0, n), ev, 0.08f);
            }
            NormalizeRms(o, 0.12f, 0.95f);
            return o;
        }

        /// <summary>Nacht: ruhige Luft, Grillen in wechselnden Gruppen, fernes Knarzen (12-s-Loop).</summary>
        static float[] NightAmbience(int r, Rng rng)
        {
            int n = 12 * r;
            var o = new float[n];
            WindInto(o, r, true, 5, 0f, 0.5f, 0x4E17, null);
            float per = n / (float)r;
            int[] counts = { 22, 17, 29 };
            float[] cf = { 4300f, 4850f, 5400f };
            float[] cg = { 0.35f, 0.25f, 0.18f };
            for (int c = 0; c < 3; c++)
            {
                var gate = new PNoise(rng, per, 2f, 6f, 4);
                int pulses = 3 + c % 2;
                for (int k = 0; k < counts[c]; k++)
                {
                    float t0 = k * per / counts[c] + rng.Range(0f, 0.04f);
                    float gv = gate.At(t0);
                    if (gv < -0.15f) continue;
                    int pl = (int)(0.011f * r), gap = (int)(0.021f * r);
                    var ev = new float[pulses * gap + pl];
                    for (int p = 0; p < pulses; p++)
                        for (int i = 0; i < pl; i++)
                        {
                            float e = (float)Math.Sin(Math.PI * i / pl);
                            int j = p * gap + i;
                            ev[j] += (float)Math.Sin(TwoPi * cf[c] * j / r) * e * e;
                        }
                    AddWrap(o, (int)(t0 * r), ev, cg[c] * M.Clamp01(0.5f + gv) * 0.05f);
                }
            }
            // fernes Knarzen (Metall/Holz)
            float[] creakAt = { 3.1f, 8.4f };
            foreach (var ct in creakAt)
            {
                int len = (int)(0.9f * r);
                var ev = new float[len];
                var f1 = new Svf(); var f2 = new Svf(); var lp = new OnePole();
                float c1 = SvfC(640f, r), c2 = SvfC(1350f, r), la = Pole(1800f, r);
                double ph = 0;
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len;
                    double prev = ph; ph += (32 + 22 * u) / r;
                    float imp = Math.Floor(ph) != Math.Floor(prev) ? 1f : 0f;
                    float x = f1.BandC(imp, c1, 0.1f) + f2.BandC(imp, c2, 0.12f) * 0.6f;
                    ev[i] = lp.LpA(x, la) * (float)Math.Sin(Math.PI * u);
                }
                AddWrap(o, (int)(ct * r), ev, 0.06f);
            }
            NormalizeRms(o, 0.05f, 0.9f);
            return o;
        }

        /// <summary>Sturm-Donner: Einschläge mit mehreren Knallen, danach langes rollendes Grollen.</summary>
        static float[] Thunder(int r, Rng rng)
        {
            float len = 7f;
            var o = new float[(int)(len * r)];
            var lpC = new OnePole(); var lr1 = new OnePole(); var lr2 = new OnePole(); var bpM = new Svf();
            var mod = new PNoise(rng, len, 0.12f, 1.5f, 10);
            float[] cracks = { 0.02f, 0.13f, 0.3f, 0.52f };
            float ca = Pole(5000f, r), mc = SvfC(380f, r);
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)r;
                float w = rng.Next() * 2 - 1;
                float crack = 0;
                for (int c = 0; c < cracks.Length; c++) if (t >= cracks[c]) crack += (float)Math.Exp(-(t - cracks[c]) / 0.035f) * (c == 0 ? 1f : 0.55f);
                float renv = (t < 0.25f ? t / 0.25f : (float)Math.Exp(-(t - 0.25f) / 1.9f)) * M.Clamp01(0.55f + 0.9f * mod.At(t));
                float ra = Pole(90f + 180f * (float)Math.Exp(-t / 1.5f), r);
                float rum = lr2.LpA(lr1.LpA(w, ra), ra) * renv * 6f;
                float mid = bpM.BandC(w, mc, 1.2f) * renv * (float)Math.Exp(-t / 0.9f) * 0.5f;
                o[i] = lpC.LpA(w, ca) * crack * 0.9f + rum + mid;
            }
            int fe = (int)(0.5f * r);
            for (int i = 0; i < fe; i++) o[o.Length - 1 - i] *= i / (float)fe;
            Normalize(o, 0.9f);
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
            new Shot { Start = 0, End = 14, Id = "skyline", Lines = new[] { "Es gab einmal eine Welt, die alles hatte.", "Und alles, was sie hatte, warf sie fort." } },
            new Shot { Start = 14, End = 30, Id = "megastore", Lines = new[] { "KONSUMA versprach uns das Glück. „Alles. Sofort. Immer neu.“", "Wir kauften und kauften … bis der Müll unsere Städte überragte." } },
            new Shot { Start = 30, End = 46, Id = "arks", Lines = new[] { "Dann bauten wir Archen. „Nur für fünf Jahre“, sagten sie.", "Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen." } },
            new Shot { Start = 46, End = 60, Id = "shutdown", Lines = new[] { "Aus fünf Jahren wurden fünfzig.", "Eine Maschine nach der anderen … verstummte." } },
            new Shot { Start = 60, End = 75, Id = "home", Lines = new[] { "Nur eine nicht. Eine kleine, sture Maschine.", "MIKO. Jeden Morgen. Würfel für Würfel." } },
            new Shot { Start = 75, End = 88, Id = "sprout", Lines = new[] { "Bis MIKO eines Tages etwas fand, das längst verloren war.", "Einen Keimling. Klein. Grün. Lebendig." } },
            new Shot { Start = 88, End = 100, Id = "ship", Lines = new[] { "Ein altes Signal erwachte: PROGRAMM ZWEITE CHANCE.", "Wenn das Leben zurückkehrt … kehren auch wir zurück." } },
        };
    }
}
