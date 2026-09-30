using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Wind und Wolken im Bild: <b>Wolkenschatten</b> ziehen mit dem Wind über Gelände, Gebäude und Figuren, und die
    /// gepflanzten Ökologie-Pflanzen wiegen sich (der Bodenbewuchs wiegt sich in <see cref="FloraRenderer"/>).
    /// Die Wolkenschatten rechnet die Nachbearbeitung (Resources/RePlanetPostFX.shader, Pass „Zusammensetzen“): aus der
    /// Tiefe wird der Weltpunkt bestimmt, entlang der Sonnenrichtung auf eine Wolkenebene projiziert und in einer
    /// kachelbaren Rauschtextur nachgeschlagen. Dieser Baustein setzt nur globale Shader-Werte – ohne Nachbearbeitung
    /// (Qualität „Niedrig“) oder ohne eigenen Shader bleibt das Bild wie bisher (sicherer Rückfall).
    /// Stärke und Bedeckung folgen dem Wetter (Wolkendecke der Himmelspalette, Sturm, Sonnenstand).
    /// </summary>
    public class WindLook : MonoBehaviour
    {
        public static WindLook I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(WindLook))) GameApp.Components.Add(typeof(WindLook));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<WindLook>() == null) GameApp.I.gameObject.AddComponent<WindLook>();
        }

        /// <summary>Kantenlänge der Wolkenkachel in Metern und Höhe der gedachten Wolkenebene.</summary>
        public const float TileMeters = 260f, CloudHeight = 220f;
        const int N = 128;

        Texture2D noise;
        Vector2 offset;
        /// <summary>Aktuelle Werte (Anzeige/Prüfumgebung): Schattenstärke 0..1, Bedeckungsschwelle, Wind 0..1.</summary>
        public float Strength { get; private set; }
        public float Coverage { get; private set; }
        public float Wind { get; private set; }
        public Vector2 Offset { get { return offset; } }

        void Awake()
        {
            I = this;
            noise = BuildNoise();
        }

        void OnDestroy()
        {
            if (noise != null) Destroy(noise);
            if (I == this) I = null;
        }

        /// <summary>Kachelbares, weiches Wolkenrauschen (periodisches Wertrauschen, 4 Oktaven) – Kanäle RGB und A gleich.</summary>
        static Texture2D BuildNoise()
        {
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "RP_Wolkenschatten", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            float min = float.MaxValue, max = float.MinValue;
            var val = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = 0, amp = 0.55f, tot = 0;
                    int period = 4;
                    for (int o = 0; o < 4; o++)
                    {
                        v += amp * Periodic(x / (float)N * period, y / (float)N * period, period, 71 + o * 13);
                        tot += amp; amp *= 0.5f; period *= 2;
                    }
                    v /= tot;
                    val[y * N + x] = v;
                    min = Mathf.Min(min, v); max = Mathf.Max(max, v);
                }
            for (int i = 0; i < val.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((val[i] - min) / Mathf.Max(1e-4f, max - min) * 255f), 0, 255);
                px[i] = new Color32(b, b, b, b);
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Wertrauschen mit Periode p (Gitterwerte wiederholen sich) und weicher Interpolation.</summary>
        static float Periodic(float x, float y, int p, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash.Float2(Mod(x0, p), Mod(y0, p), seed), b = Hash.Float2(Mod(x0 + 1, p), Mod(y0, p), seed);
            float c = Hash.Float2(Mod(x0, p), Mod(y0 + 1, p), seed), d = Hash.Float2(Mod(x0 + 1, p), Mod(y0 + 1, p), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Mod(int v, int p) { int r = v % p; return r < 0 ? r + p : r; }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var wv = WorldView.I;
            var atm = Atmosphere.I;
            var w = wv != null ? wv.World : null;
            string planet = atm != null && atm.ForcePlanet != null ? atm.ForcePlanet : wv != null ? wv.Planet : null;
            float dx = 1f, dz = 0.3f, wind = 0.25f;
            if (w != null && planet != null && w.Planets.ContainsKey(planet)) wind = Rules.Wind(w, planet, out dx, out dz);
            Wind = wind;
            // Wolken ziehen mit dem Wind (Weltmeter → Kachel-UV)
            float speed = 3f + wind * 11f;
            offset += new Vector2(dx, dz) * (speed * dt / TileMeters);
            offset.x = Mathf.Repeat(offset.x, 1f); offset.y = Mathf.Repeat(offset.y, 1f);

            float strength = 0f, coverage = 0.6f;
            if (atm != null && noise != null)
            {
                float day = Mathf.Clamp01(atm.SunElevation * 4f);
                float stormB = atm.StormBlend;
                strength = 0.3f * day * (1f - stormB * 0.75f) * (atm.Underwater ? 0f : 1f);
                // mehr Wolkendecke → niedrigere Schwelle → mehr Fläche im Schatten
                coverage = Mathf.Lerp(0.72f, 0.3f, Mathf.Clamp01(atm.CloudCover + stormB * 0.3f));
            }
            Strength = strength; Coverage = coverage;
            Shader.SetGlobalTexture("_RP_CloudShadowTex", noise);
            Shader.SetGlobalVector("_RP_CloudShadow", new Vector4(1f / TileMeters, strength, coverage, 0.12f));
            Shader.SetGlobalVector("_RP_CloudShadowOffset", new Vector4(offset.x, offset.y, CloudHeight, 0f));

            SwayEcoPlants(wv, wind, dx, dz);
        }

        /// <summary>Gepflanzte Bäume, Kakteen, Riffe und Flechten neigen sich leicht im Wind (Drehung um den Fußpunkt).</summary>
        void SwayEcoPlants(WorldView wv, float wind, float dx, float dz)
        {
            if (wv == null || wv.Root == null) return;
            var plants = wv.EcoPlants;
            if (plants == null) return;
            float t = Time.time;
            foreach (var kv in plants)
            {
                var tr = kv.Value;
                if (tr == null || !tr.gameObject.activeSelf) continue;
                var p = tr.position;
                float ph = p.x * 0.19f + p.z * 0.23f;
                float deg = wind * wind * 3.5f + Mathf.Sin(t * (1.1f + wind * 1.6f) + ph) * (0.6f + wind * 3f);
                tr.localRotation = Quaternion.Euler(dz * deg, 0f, -dx * deg);
            }
        }
    }
}
