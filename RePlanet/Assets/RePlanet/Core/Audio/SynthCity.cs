using System;

namespace RePlanet.Core
{
    /// <summary>
    /// Zusätzliche Klänge: Radio-Ansagen (kurze synthetische Senderkennungen zwischen den Stücken) und die Klangschichten
    /// der wiederbelebten Stadt (Singvögel, Möwen, Blätterrauschen, Brunnen, fernes Stadtleben). Alle Loops nahtlos.
    /// </summary>
    public static partial class Synth
    {
        /// <summary>Radio-Kennungen (Senderjingles).</summary>
        public static readonly string[] RadioJingles = { "radio_jingle_1", "radio_jingle_2", "radio_jingle_3" };

        /// <summary>Klangschichten der Stadt (Ambience, werden bei Bedarf geladen).</summary>
        public static readonly string[] CityLayers = { "city_birds", "city_gulls", "city_leaves", "city_fountain", "city_life" };

        static bool IsCity(string id) { return id.StartsWith("city_", StringComparison.Ordinal); }

        /// <summary>Erzeugt einen der zusätzlichen Klänge (null = unbekannt).</summary>
        static float[] SfxExtra(string id, Rng rng)
        {
            int r = SfxRate;
            switch (id)
            {
                case "radio_jingle_1": return RadioJingle(0, r, rng);
                case "radio_jingle_2": return RadioJingle(1, r, rng);
                case "radio_jingle_3": return RadioJingle(2, r, rng);
                case "city_birds": return CityBirds(r, rng, false);
                case "city_gulls": return CityBirds(r, rng, true);
                case "city_leaves": return CityLeaves(r, rng);
                case "city_fountain": return CityFountain(r, rng);
                case "city_life": return CityLife(r, rng);
            }
            return null;
        }

        // ================================================================== Radio-Ansage
        /// <summary>
        /// Senderkennung (≈ 3,8 s): Sendersuchlauf (gefiltertes Rauschen mit Pfeifton), ein Glockenmotiv und eine kleine
        /// „Ansage“ aus Roboter-Silben (Formant-Synthese, Sprachmelodie „Ra-di-o Zwei-te Chan-ce“).
        /// </summary>
        static float[] RadioJingle(int variant, int r, Rng rng)
        {
            float len = 3.8f;
            var o = new float[(int)(len * r)];
            // Sendersuchlauf
            {
                int n = (int)(0.7f * r);
                var bp = new Svf();
                double ph = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)r, u = t / 0.7f;
                    float f = 500f + 2600f * u * u;
                    float w = rng.Next() * 2 - 1;
                    float noise = bp.BandC(w, SvfC(f, r), 0.25f) * 0.35f;
                    ph += (1800f - 900f * u + 120f * (float)Math.Sin(t * 40)) / r;
                    float whistle = Sine(ph) * 0.06f * (1 - u);
                    float env = Math.Min(1f, t * 20f) * (1f - u * u);
                    o[i] += (noise + whistle) * env;
                }
            }
            // Glockenmotiv
            float[][] motifs =
            {
                new[] { 523.25f, 659.25f, 783.99f, 1046.5f },
                new[] { 392f, 523.25f, 587.33f, 783.99f },
                new[] { 659.25f, 587.33f, 523.25f, 783.99f },
            };
            var mo = motifs[variant % motifs.Length];
            for (int k = 0; k < mo.Length; k++)
            {
                int st = (int)((0.55f + k * 0.17f) * r);
                Add(o, st, PianoFx(mo[k], 0.5f, r, 11 + k + variant * 7), 0.55f, false);
                Add(o, st, Bell(mo[k] * 2f, 0.25f, r, 2f, 1.4f), 0.35f, false);
            }
            // Ansage: Silben mit Vokal-Formanten (a, i, o, ei, e, a, e)
            float[,] vowels = { { 800, 1200 }, { 300, 2300 }, { 500, 900 }, { 700, 1800 }, { 450, 1900 }, { 800, 1250 }, { 420, 2000 } };
            float[] pitch = { 150, 165, 140, 170, 155, 175, 130 };
            float[] dur = { 0.11f, 0.09f, 0.14f, 0.16f, 0.1f, 0.13f, 0.18f };
            float at = 1.45f + variant * 0.05f;
            for (int sIdx = 0; sIdx < pitch.Length; sIdx++)
            {
                int n = (int)(dur[sIdx] * r);
                var syl = new float[n];
                var f1 = new Svf(); var f2 = new Svf();
                float c1 = SvfC(vowels[sIdx, 0], r), c2 = SvfC(vowels[sIdx, 1], r);
                double ph = 0;
                float p0 = pitch[sIdx] * (1f + 0.04f * variant);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)r;
                    double prev = ph;
                    ph += p0 * (1f - 0.15f * t / dur[sIdx]) / r;
                    float pulse = Math.Floor(ph) != Math.Floor(prev) ? 1f : 0f;
                    float x = f1.BandC(pulse, c1, 0.12f) + f2.BandC(pulse, c2, 0.15f) * 0.6f;
                    float e = (float)Math.Sin(Math.PI * i / n);
                    syl[i] = x * e * e;
                }
                Add(o, (int)(at * r), syl, 0.9f, false);
                at += dur[sIdx] + (sIdx == 2 ? 0.12f : 0.035f);
            }
            // Schlussakkord
            int end = (int)(2.7f * r);
            Add(o, end, PianoFx(mo[0] * 0.5f, 0.45f, r, 91), 0.5f, false);
            Add(o, end, PianoFx(mo[2] * 0.5f, 0.35f, r, 92), 0.4f, false);
            Reverb(o, r, 0.28f, 0.75f, false);
            int fe = (int)(0.4f * r);
            for (int i = 0; i < fe; i++) o[o.Length - 1 - i] *= i / (float)fe;
            Normalize(o, 0.7f);
            return o;
        }

        // ================================================================== Stadtklänge
        /// <summary>Singvögel (TERRA/PYRA) bzw. Möwen (PELAGIA): einzelne Rufe zyklisch verteilt, 12-s-Loop.</summary>
        static float[] CityBirds(int r, Rng rng, bool gulls)
        {
            int n = 12 * r;
            var o = new float[n];
            // leiser Luftteppich, damit die Pausen nicht „leer“ klingen
            {
                var lp = new OnePole(); float la = Pole(1800f, r);
                var tmp = new float[n + r];
                for (int i = 0; i < tmp.Length; i++) tmp[i] = lp.LpA(rng.Next() * 2 - 1, la) * 0.05f;
                var bed = Loopify(tmp, r);
                for (int i = 0; i < n; i++) o[i] += bed[i];
            }
            if (gulls)
            {
                for (int k = 0; k < 9; k++)
                {
                    int calls = rng.Range(1, 4);
                    float t0 = rng.Range(0f, 12f), g = rng.Range(0.35f, 1f);
                    for (int c = 0; c < calls; c++) AddWrap(o, (int)((t0 + c * 0.32f) * r), GullCall(r, rng), 0.3f * g);
                }
            }
            else
            {
                // drei „Arten“: Triller, Zwei-Ton-Ruf, Zwitschern
                for (int k = 0; k < 26; k++)
                {
                    int kind = rng.Range(0, 3);
                    float t0 = rng.Range(0f, 12f), g = rng.Range(0.25f, 1f);
                    AddWrap(o, (int)(t0 * r), BirdCall(r, rng, kind), 0.22f * g);
                }
            }
            NormalizeRms(o, gulls ? 0.07f : 0.06f, 0.9f);
            return o;
        }

        static float[] BirdCall(int r, Rng rng, int kind)
        {
            float baseF = rng.Range(2600f, 4200f);
            int notes = kind == 0 ? rng.Range(6, 12) : kind == 1 ? 2 : rng.Range(3, 6);
            float noteLen = kind == 0 ? 0.035f : kind == 1 ? 0.16f : 0.07f;
            float gap = kind == 0 ? 0.018f : kind == 1 ? 0.09f : 0.05f;
            int n = (int)((notes * (noteLen + gap) + 0.05f) * r);
            var o = new float[n];
            double ph = 0;
            for (int k = 0; k < notes; k++)
            {
                int st = (int)(k * (noteLen + gap) * r), len = (int)(noteLen * r);
                float f0 = kind == 1 ? baseF * (k == 0 ? 1.12f : 0.84f) : baseF * rng.Range(0.9f, 1.15f);
                float sweep = kind == 0 ? rng.Range(-0.3f, 0.3f) : kind == 1 ? -0.12f : rng.Range(0.1f, 0.5f);
                for (int i = 0; i < len && st + i < n; i++)
                {
                    float u = i / (float)len;
                    float f = f0 * (1f + sweep * u) + 120f * (float)Math.Sin(u * 40f);
                    ph += f / r;
                    float e = (float)Math.Sin(Math.PI * u);
                    o[st + i] += (Sine(ph) * 0.85f + Sine(ph * 2) * 0.1f) * e * e;
                }
            }
            return o;
        }

        static float[] GullCall(int r, Rng rng)
        {
            float len = rng.Range(0.35f, 0.55f);
            int n = (int)(len * r);
            var o = new float[n];
            var f1 = new Svf(); var f2 = new Svf();
            float c1 = SvfC(1300f, r), c2 = SvfC(2700f, r);
            double ph = 0;
            float f0 = rng.Range(750f, 1000f);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = f0 * (1.25f - 0.55f * u) * (1f + 0.02f * (rng.Next() - 0.5f));
                ph += f / r;
                float saw = SawW((float)(ph - Math.Floor(ph)), f / r);
                float x = f1.BandC(saw, c1, 0.3f) + f2.BandC(saw, c2, 0.4f) * 0.5f;
                float e = u < 0.1f ? u / 0.1f : (float)Math.Pow(1f - (u - 0.1f) / 0.9f, 1.5);
                o[i] = x * e * 0.5f;
            }
            return o;
        }

        /// <summary>Wind in Bäumen: hochfrequentes Rauschen, von langsamen Böen und schnellem Flattern moduliert (10-s-Loop).</summary>
        static float[] CityLeaves(int r, Rng rng)
        {
            int n = 10 * r, fold = r;
            var tmp = new float[n + fold];
            float per = (n + fold) / (float)r;
            var gust = new PNoise(rng, per, 1.2f, 4f, 6);
            var flutter = new PNoise(rng, per, 0.06f, 0.2f, 8);
            var bp = new Svf(); var bp2 = new Svf();
            float c1 = SvfC(3200f, r), c2 = SvfC(6200f, r);
            for (int i = 0; i < tmp.Length; i++)
            {
                float t = i / (float)r;
                float g = M.Clamp01(0.45f + 0.9f * gust.At(t));
                float fl = 0.7f + 0.6f * flutter.At(t);
                float w = rng.Next() * 2 - 1;
                tmp[i] = (bp.BandC(w, c1, 0.9f) * 0.6f + bp2.BandC(w, c2, 1.1f) * 0.3f) * g * g * fl;
            }
            var o = Loopify(tmp, fold);
            NormalizeRms(o, 0.08f, 0.9f);
            return o;
        }

        /// <summary>Brunnen: rauschendes Wasser mit dichten Tropfen (8-s-Loop).</summary>
        static float[] CityFountain(int r, Rng rng)
        {
            int n = 8 * r, fold = r / 2;
            var tmp = new float[n + fold];
            var lp = new OnePole(); var bp = new Svf();
            float la = Pole(2600f, r), bc = SvfC(900f, r);
            for (int i = 0; i < tmp.Length; i++)
            {
                float w = rng.Next() * 2 - 1;
                tmp[i] = lp.LpA(w, la) * 0.5f + bp.BandC(w, bc, 1.4f) * 0.25f;
            }
            var o = Loopify(tmp, fold);
            for (int k = 0; k < 220; k++)
            {
                int len = (int)(0.03f * r);
                var ev = new float[len];
                float f = rng.Range(900f, 2600f);
                for (int i = 0; i < len; i++) { float t = i / (float)r; ev[i] = (float)Math.Sin(TwoPi * (f * t + 3000 * t * t)) * (float)Math.Exp(-t * 90); }
                AddWrap(o, rng.Range(0, o.Length), ev, rng.Range(0.03f, 0.1f));
            }
            NormalizeRms(o, 0.11f, 0.95f);
            return o;
        }

        /// <summary>
        /// Fernes Stadtleben (14-s-Loop): tiefes Verkehrsrauschen mit vorbeifahrenden Autos, eine Straßenbahn mit Klingel,
        /// gedämpftes Stimmengemurmel (Formant-Silben, stark tiefpassgefiltert).
        /// </summary>
        static float[] CityLife(int r, Rng rng)
        {
            int n = 14 * r, fold = r;
            var tmp = new float[n + fold];
            float per = (n + fold) / (float)r;
            var swell = new PNoise(rng, per, 1.5f, 5f, 6);
            var lp1 = new OnePole(); var lp2 = new OnePole();
            for (int i = 0; i < tmp.Length; i++)
            {
                float t = i / (float)r;
                float s = M.Clamp01(0.5f + 0.8f * swell.At(t));
                float la = Pole(160f + 260f * s, r);
                float w = rng.Next() * 2 - 1;
                tmp[i] = lp2.LpA(lp1.LpA(w, la), la) * (0.6f + 0.8f * s) * 2.2f;
            }
            var o = Loopify(tmp, fold);
            // vorbeifahrende Autos (Doppler-Brummen)
            for (int k = 0; k < 4; k++)
            {
                int len = (int)(2.4f * r);
                var ev = new float[len];
                var lp = new OnePole();
                double ph = 0;
                float f0 = rng.Range(70f, 110f);
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len;
                    float f = f0 * (1.06f - 0.12f * u);
                    ph += f / r;
                    float e = (float)Math.Pow(Math.Sin(Math.PI * u), 2);
                    ev[i] = lp.Lp(SawW((float)(ph - Math.Floor(ph)), f / r) * 0.4f + (rng.Next() * 2 - 1) * 0.5f, 500f, r) * e;
                }
                AddWrap(o, rng.Range(0, o.Length), ev, 0.12f);
            }
            // Straßenbahn: Rumpeln + Klingel (ding-ding)
            {
                int st = (int)(rng.Range(2f, 10f) * r);
                int len = (int)(4.5f * r);
                var ev = new float[len];
                var lp = new OnePole();
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len, t = i / (float)r;
                    float clack = (t * 2.2f % 1f) < 0.03f ? (rng.Next() * 2 - 1) : 0f;
                    ev[i] = lp.Lp((rng.Next() * 2 - 1) * 0.6f + clack * 1.5f, 320f, r) * (float)Math.Sin(Math.PI * u);
                }
                AddWrap(o, st, ev, 0.25f);
                var bell = Bell(1180f, 0.4f, r, 2.76f, 1.2f);
                AddWrap(o, st + (int)(1.2f * r), bell, 0.05f);
                AddWrap(o, st + (int)(1.45f * r), bell, 0.05f);
            }
            // Stimmengemurmel
            float[,] vowels = { { 700, 1150 }, { 400, 2000 }, { 500, 900 }, { 600, 1700 } };
            for (int k = 0; k < 40; k++)
            {
                int v = rng.Range(0, 4);
                float d = rng.Range(0.08f, 0.2f);
                int len = (int)(d * r);
                var ev = new float[len];
                var f1 = new Svf(); var f2 = new Svf(); var lp = new OnePole();
                float c1 = SvfC(vowels[v, 0], r), c2 = SvfC(vowels[v, 1], r);
                double ph = 0;
                float p0 = rng.Range(110f, 230f);
                for (int i = 0; i < len; i++)
                {
                    double prev = ph; ph += p0 / r;
                    float pulse = Math.Floor(ph) != Math.Floor(prev) ? 1f : 0f;
                    float x = f1.BandC(pulse, c1, 0.15f) + f2.BandC(pulse, c2, 0.2f) * 0.5f;
                    float e = (float)Math.Sin(Math.PI * i / len);
                    ev[i] = lp.Lp(x, 1100f, r) * e;
                }
                AddWrap(o, rng.Range(0, o.Length), ev, 0.18f);
            }
            NormalizeRms(o, 0.07f, 0.9f);
            return o;
        }
    }
}
