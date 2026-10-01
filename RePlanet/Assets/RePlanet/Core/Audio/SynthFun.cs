using System;

namespace RePlanet.Core
{
    /// <summary>
    /// Klänge für TNT und Schatzfunde: zischende Zündschnur (nahtloser Loop), Countdown-Piepser (normal/letzte Sekunde),
    /// Wurf, große aber nicht schrille Explosion (dumpfer Schlag, Knall, grollender Nachhall, rieselnder Schutt),
    /// landende Trümmer und das funkelnde „Fund!“-Motiv.
    /// </summary>
    public static partial class Synth
    {
        public static readonly string[] FunSfx = { "tnt_fuse", "tnt_beep", "tnt_beep_hi", "tnt_throw", "tnt_boom", "debris_land", "treasure", "dizzy" };

        static float[] SfxFun(string id, Rng rng)
        {
            int r = SfxRate;
            switch (id)
            {
                case "tnt_fuse": return TntFuse(r, rng);
                case "tnt_beep": return TntBeep(r, 1760f, 0.09f);
                case "tnt_beep_hi": return TntBeep(r, 2350f, 0.07f);
                case "tnt_throw": return TntThrow(r, rng);
                case "tnt_boom": return TntBoom(r, rng);
                case "debris_land": return DebrisLand(r, rng);
                case "treasure": return TreasureJingle(r);
                case "dizzy": return Dizzy(r);
            }
            return null;
        }

        /// <summary>Zündschnur: dichtes Knistern über gefiltertem Zischen, 1,2 s nahtlos.</summary>
        static float[] TntFuse(int r, Rng rng)
        {
            int n = (int)(1.35f * r);
            var o = new float[n];
            var hp = new OnePole(); var bp = new Svf();
            var mod = new PNoise(rng, 1.35f, 0.05f, 0.4f, 8);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)r;
                float w = rng.Next() * 2 - 1;
                float hiss = w - hp.Lp(w, 2500f, r);
                float body = bp.Band(w, 4200f + 900f * mod.At(t), 1.4f, r) * 0.6f;
                o[i] = (hiss * 0.35f + body) * (0.7f + 0.3f * mod.At(t * 1.7f));
            }
            // Knistern: kurze Klicks
            for (int k = 0; k < 90; k++)
            {
                int at = rng.Range(0, n);
                float amp = 0.3f + 0.7f * rng.Next();
                int len = (int)(0.004f * r);
                for (int j = 0; j < len; j++) o[(at + j) % n] += amp * (float)Math.Exp(-j / (0.0009f * r)) * (rng.Next() * 2 - 1);
            }
            var loop = Loopify(o, (int)(0.15f * r));
            Normalize(loop, 0.55f);
            return loop;
        }

        /// <summary>Countdown-Piepser: kurzer, weicher Rechteckton mit schneller Hüllkurve.</summary>
        static float[] TntBeep(int r, float f, float len)
        {
            var o = new float[(int)((len + 0.04f) * r)];
            var lp = new OnePole();
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)r;
                float sq = Sine(f * t) + 0.3f * Sine(f * 3f * t);
                o[i] = lp.Lp(sq, 6000f, r) * Env(t, 0.004f, 0.02f, 0.8f, 0.03f, len);
            }
            Normalize(o, 0.6f);
            return o;
        }

        /// <summary>Wurf: kurzes Aufschwingen (gefiltertes Rauschen) mit leisem Holzklappern.</summary>
        static float[] TntThrow(int r, Rng rng)
        {
            var o = new float[(int)(0.55f * r)];
            var bp = new Svf();
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)r;
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, t / 0.5f));
                o[i] = bp.Band(rng.Next() * 2 - 1, 500f + 2400f * t / 0.5f, 2.2f, r) * env;
            }
            for (int k = 0; k < 3; k++)
            {
                int at = (int)((0.02f + k * 0.035f) * r);
                for (int j = 0; j < (int)(0.03f * r) && at + j < o.Length; j++)
                    o[at + j] += 0.35f * (float)Math.Sin(TwoPi * (900 + 150 * k) * j / r) * (float)Math.Exp(-j / (0.006f * r));
            }
            Normalize(o, 0.55f);
            return o;
        }

        /// <summary>
        /// Explosion (≈ 3,4 s): dumpfer Tiefschlag (fallender Sinus), heller Knall, breites grollendes Rauschen mit langem Abklingen
        /// und rieselnder Schutt. Bewusst ohne schrille Spitzen, damit sie „groß, aber angenehm“ bleibt.
        /// </summary>
        static float[] TntBoom(int r, Rng rng)
        {
            float len = 3.4f;
            var o = new float[(int)(len * r)];
            var lp1 = new OnePole(); var lp2 = new OnePole(); var lpC = new OnePole(); var bp = new Svf();
            double ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)r;
                float w = rng.Next() * 2 - 1;
                // Tiefschlag 90 → 32 Hz
                float f = 32f + 58f * (float)Math.Exp(-t / 0.12f);
                ph += f / r;
                float thump = (float)Math.Sin(ph * TwoPi) * (float)Math.Exp(-t / 0.45f) * Math.Min(1f, t * 400f);
                // Knall
                float crack = lpC.Lp(w, 3200f, r) * (float)Math.Exp(-t / 0.05f);
                // Grollen
                float renv = (t < 0.06f ? t / 0.06f : (float)Math.Exp(-(t - 0.06f) / 0.85f));
                float rumble = lp2.Lp(lp1.Lp(w, 380f, r), 260f, r) * renv * 3.2f;
                float mid = bp.Band(w, 900f * (float)Math.Exp(-t / 0.6f) + 200f, 1.1f, r) * renv * 0.45f;
                o[i] = thump * 1.1f + crack * 0.8f + rumble + mid;
            }
            // Schutt: einzelne kleine Einschläge
            for (int k = 0; k < 40; k++)
            {
                float t0 = 0.5f + 2.3f * (float)Math.Pow(rng.Next(), 0.8);
                int at = (int)(t0 * r);
                float amp = 0.08f + 0.12f * rng.Next();
                float fr = 300f + 900f * rng.Next();
                for (int j = 0; j < (int)(0.05f * r) && at + j < o.Length; j++)
                    o[at + j] += amp * (float)Math.Sin(TwoPi * fr * j / r) * (float)Math.Exp(-j / (0.01f * r)) * (0.5f + 0.5f * (rng.Next() * 2 - 1));
            }
            Reverb(o, r, 0.22f, 0.85f, false);
            int fe = (int)(0.4f * r);
            for (int i = 0; i < fe; i++) o[o.Length - 1 - i] *= i / (float)fe;
            SoftLimit(o, 0.95f);
            Normalize(o, 0.92f);
            return o;
        }

        /// <summary>Landende Trümmerstücke: dumpfes Klopfen mit kurzem Scheppern.</summary>
        static float[] DebrisLand(int r, Rng rng)
        {
            var o = new float[(int)(0.5f * r)];
            var lp = new OnePole();
            for (int k = 0; k < 5; k++)
            {
                int at = (int)((k * 0.06f + 0.05f * rng.Next()) * r);
                float fr = 140f + 220f * rng.Next();
                for (int j = 0; j < (int)(0.18f * r) && at + j < o.Length; j++)
                {
                    float t = j / (float)r;
                    o[at + j] += (float)Math.Sin(TwoPi * fr * t) * (float)Math.Exp(-t / 0.05f) * 0.6f + lp.Lp(rng.Next() * 2 - 1, 1800f, r) * (float)Math.Exp(-t / 0.02f) * 0.5f;
                }
            }
            Normalize(o, 0.6f);
            return o;
        }

        /// <summary>„Fund!“: aufsteigendes, funkelndes Glockenmotiv in Dur mit kleinem Glitzern darüber.</summary>
        static float[] TreasureJingle(int r)
        {
            var o = new float[(int)(2.2f * r / 2)];
            int rr = r / 2;
            float root = 587.33f; // D5
            int[] seq = { 0, 4, 7, 12, 16 };
            for (int k = 0; k < seq.Length; k++)
            {
                Add(o, (int)(k * 0.085f * rr), Bell(Freq(root, seq[k]), 0.45f, rr, 3.5f, 1.4f), 0.55f, false);
                Add(o, (int)(k * 0.085f * rr), PianoFx(Freq(root, seq[k] - 12), 0.35f, rr, k), 0.35f, false);
            }
            // Glitzern
            var rng = new Rng(4711);
            for (int k = 0; k < 14; k++)
            {
                int at = (int)((0.35f + k * 0.045f) * rr);
                Add(o, at, Bell(Freq(root, 24 + rng.Range(0, 8)), 0.12f, rr, 5f, 0.5f), 0.4f, false);
            }
            Reverb(o, rr, 0.35f, 0.8f, false);
            Normalize(o, 0.75f);
            return Upsample(o);
        }

        /// <summary>Benommen: kleines, kreisendes Zwitschern (Vögelchen-Sterne um den Kopf).</summary>
        static float[] Dizzy(int r)
        {
            var o = new float[(int)(1.3f * r)];
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)r;
                float wob = (float)Math.Sin(TwoPi * 5.5f * t);
                float f = 2200f + 500f * wob;
                o[i] = (float)Math.Sin(TwoPi * f * t) * 0.25f * Env(t, 0.02f, 0.2f, 0.7f, 0.4f, 0.9f) * (0.6f + 0.4f * (float)Math.Sin(TwoPi * 11f * t));
            }
            Normalize(o, 0.4f);
            return o;
        }
    }
}
