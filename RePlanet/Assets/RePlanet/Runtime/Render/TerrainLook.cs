using System;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Gelände- und Wasser-Materialien im stilisierten Look (eigene Shader Resources/RePlanetTerrain.shader und
    /// Resources/RePlanetWater.shader) samt prozedural erzeugter, kachelbarer Rausch- und Wellentexturen.
    /// Werden die Shader nicht unterstützt, gibt es die bisherigen Standard-Materialien (gleiches Verhalten wie vorher).
    /// </summary>
    public static class TerrainLook
    {
        static Texture2D noiseTex, waterNormals;
        static Shader terrainShader, waterShader;
        static bool loaded;

        class Style
        {
            public Color RockA, RockB, TintA, TintB, Rim;
            public float Variation = 0.25f, Saturation = 1.2f, Detail = 0.6f, Strata = 0.35f, Gloss = 0.08f;
            public Color ShallowClean = Mats.C(0x3CE0CC), DeepClean = Mats.C(0x0A3C74), ShallowDirty = Mats.C(0x7A8A5A), DeepDirty = Mats.C(0x24382C);
            public Color FoamClean = Mats.C(0xF4FAFF), FoamDirty = Mats.C(0xC8C0A0);
        }

        static Style For(string planet)
        {
            switch (planet)
            {
                case "pyra":
                    return new Style { RockA = Mats.C(0xB0582E), RockB = Mats.C(0x5A2418), TintA = Mats.C(0xFF9050), TintB = Mats.C(0xA83A50), Rim = Mats.C(0xFF7A3A), Variation = 0.3f, Saturation = 1.3f, Strata = 0.5f };
                case "pelagia":
                    return new Style { RockA = Mats.C(0xB08A9A), RockB = Mats.C(0x4A5A6A), TintA = Mats.C(0xFFC0B0), TintB = Mats.C(0x60C8B8), Rim = Mats.C(0xFFB0C8), Variation = 0.25f, Saturation = 1.2f, Strata = 0.4f };
                case "nivalis":
                    return new Style { RockA = Mats.C(0x7A78A8), RockB = Mats.C(0x34345A), TintA = Mats.C(0xD0DCFF), TintB = Mats.C(0xA898E8), Rim = Mats.C(0xA090E8), Variation = 0.2f, Saturation = 1.1f, Strata = 0.3f, Gloss = 0.35f,
                        ShallowClean = Mats.C(0x7AD0E0), DeepClean = Mats.C(0x1A3A6A) };
                default:
                    return new Style { RockA = Mats.C(0x9A7858), RockB = Mats.C(0x4E3E32), TintA = Mats.C(0xF0C070), TintB = Mats.C(0x70B8A8), Rim = Mats.C(0xF0C890), Variation = 0.25f, Saturation = 1.25f, Strata = 0.35f };
            }
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            terrainShader = Mats.CustomShader("RePlanetTerrain", "RePlanet/Terrain");
            waterShader = Mats.CustomShader("RePlanetWater", "RePlanet/Water");
        }

        // ================================================================== Gelände
        /// <summary>Gelände-Material: gemalte Geländetextur als Grundfarbe, darüber Hangfels, Farbvariation und Detail.</summary>
        public static Material CreateTerrain(string planet, Texture2D tex)
        {
            Load();
            var st = For(planet);
            if (terrainShader != null)
            {
                try
                {
                    var m = new Material(terrainShader) { name = "Terrain_" + planet };
                    m.mainTexture = tex;
                    m.SetTexture("_NoiseTex", NoiseTex());
                    m.SetColor("_RockA", st.RockA);
                    m.SetColor("_RockB", st.RockB);
                    m.SetColor("_TintA", st.TintA);
                    m.SetColor("_TintB", st.TintB);
                    m.SetColor("_RimColor", st.Rim);
                    m.SetVector("_Look", new Vector4(st.Variation, st.Saturation, st.Detail, st.Strata));
                    float water = Terrain.WaterLevel(planet);
                    m.SetVector("_Slope", new Vector4(0.22f, 0.42f, water, water > -50f ? 1f : 0f));
                    m.SetFloat("_Glossiness", st.Gloss);
                    SetRoads(m, planet);
                    return m;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Gelände-Shader: " + e.Message); }
            }
            var f = Mats.Unique(Mats.Opaque, Color.white);
            f.mainTexture = tex;
            f.SetFloat("_Glossiness", planet == "nivalis" ? 0.35f : 0.08f);
            return f;
        }

        /// <summary>Straßensegmente des Layouts an den Gelände-Shader (Asphalt, Markierungen, Gehwege).</summary>
        static void SetRoads(Material m, string planet)
        {
            try
            {
                var layout = RePlanet.Core.WorldGen.Get(planet);
                var a = new Vector4[16]; var b = new Vector4[16];
                int n = 0;
                foreach (var r in layout.Roads)
                {
                    if (n >= 16) break;
                    a[n] = new Vector4(r[0], r[1], r[2], r[3]);
                    b[n] = new Vector4(r[4], 0, 0, 0);
                    n++;
                }
                m.SetVectorArray("_RoadA", a);
                m.SetVectorArray("_RoadB", b);
                m.SetFloat("_RoadCount", n);
                bool marks = planet == "terra" || planet == "nivalis" || planet == "pelagia";
                Color asphalt = planet == "pyra" ? new Color(0.42f, 0.25f, 0.18f) : planet == "nivalis" ? new Color(0.32f, 0.34f, 0.38f) : new Color(0.17f, 0.17f, 0.18f);
                m.SetColor("_RoadColor", new Color(asphalt.r, asphalt.g, asphalt.b, marks ? 1f : 0f));
                m.SetVector("_RoadStyle", new Vector4(planet == "pelagia" ? 1f : 0f, planet == "terra" || planet == "nivalis" ? 1f : 0f, planet == "pyra" ? 0.4f : 1f, planet == "nivalis" ? 1f : 0f));
                var bs = layout.Base;
                m.SetVector("_BaseRect", new Vector4(bs.MinX, bs.MinZ, bs.MaxX, bs.MaxZ));
                m.SetVector("_GridRect", new Vector4(bs.GridX0, bs.GridZ0, bs.GridX0 + bs.GridW * bs.Cell, bs.GridZ0 + bs.GridH * bs.Cell));
                m.SetVector("_GridCell", new Vector4(bs.Cell, bs.DropZone.x, bs.DropZone.z, bs.DropRadius));
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Straßen im Gelände-Shader: " + e.Message); }
        }

        // ================================================================== Wasser
        static bool IsCustom(Material m) { return m != null && waterShader != null && m.shader == waterShader; }

        /// <summary>Wasser-Material (eigener Shader); Rückfall: Standard-Transparent mit der übergebenen Wellen-Normalmap.</summary>
        public static Material CreateWater(string planet, Func<Texture2D> fallbackNormals)
        {
            Load();
            var st = For(planet);
            if (waterShader != null)
            {
                try
                {
                    var m = new Material(waterShader) { name = "Water_" + planet };
                    m.SetTexture("_BumpMap", WaterNormals());
                    m.SetTexture("_NoiseTex", NoiseTex());
                    SetWaterClarity(m, planet, 0f);
                    return m;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Wasser-Shader: " + e.Message); }
            }
            var f = Mats.Unique(Mats.Water, new Color(0.25f, 0.45f, 0.38f, 0.72f));
            try
            {
                f.SetTexture("_BumpMap", fallbackNormals());
                f.EnableKeyword("_NORMALMAP");
                f.SetFloat("_BumpScale", 0.55f);
                f.mainTextureScale = new Vector2(90f, 90f);
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Wasser-Normalmap: " + e.Message); }
            return f;
        }

        /// <summary>Wasserqualität 0 (verschmutzt, trüb) … 1 (klar, türkis).</summary>
        public static void SetWaterClarity(Material m, string planet, float q)
        {
            if (m == null) return;
            q = Mathf.Clamp01(q);
            if (!IsCustom(m))
            {
                m.color = Color.Lerp(new Color(0.3f, 0.42f, 0.33f, 0.8f), new Color(0.12f, 0.72f, 0.78f, 0.62f), q);
                return;
            }
            var st = For(planet);
            m.SetColor("_ShallowColor", Color.Lerp(st.ShallowDirty, st.ShallowClean, q));
            m.SetColor("_DeepColor", Color.Lerp(st.DeepDirty, st.DeepClean, q));
            m.SetColor("_FoamColor", Color.Lerp(st.FoamDirty, st.FoamClean, q));
            m.SetFloat("_Clarity", Mathf.Lerp(1.2f, 4.5f, q));
            m.SetVector("_AlphaRange", new Vector4(Mathf.Lerp(0.6f, 0.3f, q), Mathf.Lerp(0.97f, 0.92f, q), 0, 0));
            m.SetFloat("_Reflect", Mathf.Lerp(0.8f, 1f, q));
        }

        /// <summary>Wellenbewegung: der eigene Shader animiert selbst, das Standard-Material über den Texturversatz.</summary>
        public static void AnimateWater(Material m, float t)
        {
            if (m == null || IsCustom(m)) return;
            m.mainTextureOffset = new Vector2(t * 0.004f, t * 0.0025f);
        }

        // ================================================================== Texturen
        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            return (Hash(x, y, seed) & 0xFFFFFF) / 16777215f;
        }

        /// <summary>Kachelbares Wertrauschen (u, v in 0..1, Periode in Zellen).</summary>
        static float Periodic(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0, y0, period, seed), b = Lattice(x0 + 1, y0, period, seed);
            float c = Lattice(x0, y0 + 1, period, seed), d = Lattice(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int per = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Periodic(u, v, per, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                per *= 2;
            }
            return sum / norm;
        }

        static byte Stretch(float v, float contrast)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(((v - 0.5f) * contrast + 0.5f) * 255f), 0, 255);
        }

        /// <summary>Kachelbare RGBA-Rauschtextur: R grob, G mittel, B fein, A sehr fein (je Kanal eigene Frequenz).</summary>
        public static Texture2D NoiseTex()
        {
            if (noiseTex != null) return noiseTex;
            const int N = 256;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    px[y * N + x] = new Color32(
                        Stretch(Fbm(u, v, 4, 5, 11), 2.0f),
                        Stretch(Fbm(u, v, 8, 4, 23), 2.0f),
                        Stretch(Fbm(u, v, 16, 3, 37), 1.9f),
                        Stretch(Fbm(u, v, 32, 2, 53), 1.8f));
                }
            noiseTex = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "RP_Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            noiseTex.SetPixels32(px);
            noiseTex.Apply(true, false);
            return noiseTex;
        }

        /// <summary>Kachelbare Wellen-Normalmap aus fbm-Rauschen mit scharfen Kämmen (RG = Normale, B/A = 1).</summary>
        public static Texture2D WaterNormals()
        {
            if (waterNormals != null) return waterNormals;
            const int N = 256;
            var h = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float f = Fbm(u, v, 6, 5, 71);
                    float ridge = 1f - Mathf.Abs(Fbm(u, v, 4, 3, 91) * 2f - 1f);
                    h[y * N + x] = f * 0.7f + ridge * ridge * 0.45f;
                }
            var px = new Color32[N * N];
            const float k = 9f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                    float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                    var n = new Vector3(-dx * k, -dy * k, 1f).normalized;
                    px[y * N + x] = new Color32((byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255f, 0, 255), (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255f, 0, 255), 255, 255);
                }
            waterNormals = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "RP_WaterNormals", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            waterNormals.SetPixels32(px);
            waterNormals.Apply(true, false);
            return waterNormals;
        }
    }
}
