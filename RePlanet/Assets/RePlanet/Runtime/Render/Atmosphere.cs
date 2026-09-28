using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Himmel, Licht, Nebel und Wetter. Treibt den eigenen Himmels-Shader (Resources/RePlanetSky.shader) mit
    /// planetenspezifischen Paletten für Tag, Dämmerung, Nacht und Sturm. Fällt auf Skybox/Procedural zurück.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        public static Atmosphere I { get; private set; }
        public Light Sun { get; private set; }
        Material sky;
        bool customSky;
        ParticleSystem weather, stars;
        ParticleSystemRenderer weatherRenderer;
        readonly List<Light> lampPool = new List<Light>();
        readonly List<Vector3> litLamps = new List<Vector3>();
        float lightning, nextLightning = 5f;
        string weatherKind;
        public float Darkness { get; private set; }
        public bool Underwater { get; private set; }
        /// <summary>Zeitliche Übersteuerung (Menü/Intro): Tageszeit 0..1, &lt;0 = aus.</summary>
        public float ForcePhase = -1f;
        public string ForcePlanet;
        public float ForceStorm = -1f;

        class Palette
        {
            public Color Zenith, Horizon, Haze, Ground, CloudLight, CloudShadow, NebA, NebB, Sun, Fog;
            public float Cover, Nebula, HazeStrength;
        }

        class SkyBody { public Vector3 Dir; public float Radius; public Color A, B, Rim; public float Bands, Seed, RimStrength; }

        class PlanetSky
        {
            public Palette Day, Dusk, Night, Storm;
            public SkyBody P1, P2;
            public float SunAzimuth, CloudScale = 0.9f, CloudDensity = 2.4f, Aurora;
            public Color AuroraA = new Color(0.2f, 1f, 0.6f), AuroraB = new Color(0.6f, 0.3f, 1f);
            public float FogDay = 0.008f, FogStorm = 0.03f;
            /// <summary>Farbkorrektur (Split-Toning) der Nachbearbeitung: Tönung der Schatten und der Lichter.</summary>
            public Color GradeShadow = new Color(0.3f, 0.45f, 0.55f), GradeHighlight = new Color(1f, 0.85f, 0.65f);
            public float Saturation = 1.2f, Contrast = 1.18f;
            /// <summary>Wolkenbank (aufgetürmte Wolken am Horizont): Stärke und Höhe.</summary>
            public float Bank = 0.8f, BankHeight = 1f;
        }

        /// <summary>
        /// Bildlook für die Nachbearbeitung (PostFX): Sonnenstand/-farbe, Luftperspektive, Farbkorrektur je Planet.
        /// Wird von Atmosphere jedes Bild aus Palette, Tageszeit und Wetter berechnet.
        /// </summary>
        public class LookInfo
        {
            public Vector3 SunDir = new Vector3(0.3f, 0.5f, 0.8f).normalized;
            public Color SunColor = new Color(1f, 0.9f, 0.75f);
            public Texture SkyCube;
            public float FogSky = 0.85f, FogMax = 1f, FogFalloff = 0.035f, FogBase = 0f, FogLinear = 0.35f, FogSunScatter = 0.5f;
            public float Exposure = 1.1f, Contrast = 1.18f, Saturation = 1.2f, Vibrance = 0.45f, SplitAmount = 0.3f;
            public Color ShadowTint = new Color(0.3f, 0.45f, 0.55f), HighlightTint = new Color(1f, 0.85f, 0.65f);
            public Color VignetteColor = new Color(0.3f, 0.25f, 0.4f);
            public float Vignette = 0.45f, Bloom = 0.22f, BloomThreshold = 0.9f, ShaftStrength = 0.6f, ShaftThreshold = 0.45f;
        }

        public static readonly LookInfo DefaultLook = new LookInfo();
        /// <summary>Aktueller Bildlook (für PostFX).</summary>
        public readonly LookInfo Look = new LookInfo();

        static Palette Pal(uint zen, uint hor, uint haze, uint ground, uint cl, uint cs, uint na, uint nb, uint sun, uint fog, float cover, float neb, float hazeS)
        {
            return new Palette
            {
                Zenith = Mats.C(zen), Horizon = Mats.C(hor), Haze = Mats.C(haze), Ground = Mats.C(ground), CloudLight = Mats.C(cl), CloudShadow = Mats.C(cs),
                NebA = Mats.C(na), NebB = Mats.C(nb), Sun = Mats.C(sun), Fog = Mats.C(fog), Cover = cover, Nebula = neb, HazeStrength = hazeS
            };
        }

        static SkyBody Body(Vector3 dir, float deg, uint a, uint b, uint rim, float bands, float seed, float rimS)
        {
            return new SkyBody { Dir = dir.normalized, Radius = deg * Mathf.Deg2Rad, A = Mats.C(a), B = Mats.C(b), Rim = Mats.C(rim), Bands = bands, Seed = seed, RimStrength = rimS };
        }

        static readonly Dictionary<string, PlanetSky> skies = new Dictionary<string, PlanetSky>
        {
            // TERRA: goldener Horizont unter türkisem Zenit, großer blasser Mond knapp über dem Horizont
            { "terra", new PlanetSky {
                Day = Pal(0x1F8FA8, 0xF7C77E, 0xF2D4A0, 0x6B5E4E, 0xFFF0D0, 0x3E6A7A, 0xFFB060, 0x2AA0A8, 0xFFE2A8, 0xD8C49A, 0.34f, 0.12f, 0.42f),
                Dusk = Pal(0x243A6E, 0xFF8A40, 0xF4A060, 0x4A3A36, 0xFFB070, 0x3A4A70, 0xFF7A45, 0x2A6A8A, 0xFF9A50, 0xD08A5E, 0.4f, 0.25f, 0.6f),
                Night = Pal(0x04101E, 0x16263A, 0x1E2E44, 0x0E0C12, 0x2E4058, 0x0C1420, 0x4A6A9A, 0x1A3A5A, 0x9DC6FF, 0x16202E, 0.4f, 0.45f, 0.4f),
                Storm = Pal(0x6E6258, 0xB09070, 0xA88A6E, 0x5A4C40, 0xB89C80, 0x5A4A40, 0x8A6A50, 0x4A3A30, 0xE0C090, 0xA48A70, 0.95f, 0.05f, 0.9f),
                P1 = Body(new Vector3(0.45f, 0.2f, 0.8f), 13f, 0xC8D0E0, 0x8C96AE, 0x9EC8FF, 0.15f, 1.7f, 1f),
                P2 = Body(new Vector3(-0.62f, 0.16f, 0.55f), 3f, 0xE8C8A8, 0xB08868, 0xFFD8A8, 0f, 4.2f, 0.6f),
                SunAzimuth = 200f, CloudDensity = 4f, FogDay = 0.007f, FogStorm = 0.028f,
                GradeShadow = Mats.C(0x1E7A8C), GradeHighlight = Mats.C(0xFFC878), Saturation = 1.22f, Bank = 0.85f } },
            // PYRA: orange-rote Wolkenmassen, dunkles Dunstband, dunkler Riesenplanet mit Lichtsaum
            { "pyra", new PlanetSky {
                Day = Pal(0x8A2412, 0xFF8A3A, 0x3A4A48, 0x5A2A1E, 0xFFB070, 0x6A1A10, 0xFF6A2A, 0x7A1A2A, 0xFFC080, 0xA8583A, 0.42f, 0.3f, 0.55f),
                Dusk = Pal(0x3A1026, 0xFF5A20, 0x1E3438, 0x3A1812, 0xFF7A3A, 0x4A1018, 0xFF4A20, 0x4A1040, 0xFF8040, 0x7A3A30, 0.46f, 0.45f, 0.65f),
                Night = Pal(0x0C0508, 0x3A140C, 0x1A2224, 0x120806, 0x4A2014, 0x14080A, 0xC0502A, 0x3A0C2A, 0xFFB080, 0x2A140E, 0.6f, 0.6f, 0.6f),
                Storm = Pal(0x8A4A2A, 0xC8784A, 0xB0663E, 0x6A3420, 0xD08A5A, 0x6A3420, 0xC0602E, 0x5A2418, 0xFFB070, 0xB06A44, 1f, 0.1f, 1f),
                P1 = Body(new Vector3(-0.45f, 0.22f, 0.79f), 18f, 0x2A3048, 0x485070, 0xFFC890, 0.35f, 2.3f, 1.3f),
                P2 = Body(new Vector3(0.55f, 0.3f, 0.62f), 3.5f, 0xC06A4A, 0x7A3A2A, 0xFF9A6A, 0f, 7.1f, 0.5f),
                SunAzimuth = 160f, CloudScale = 0.7f, CloudDensity = 4.2f, FogDay = 0.009f, FogStorm = 0.05f,
                GradeShadow = Mats.C(0x5A1E48), GradeHighlight = Mats.C(0xFFA050), Saturation = 1.1f, Contrast = 1.2f, Bank = 1f, BankHeight = 1.2f } },
            // PELAGIA: türkiser Zenit, rosa Horizont, heller Mond, weiche Wolkentürme über dem Meer
            { "pelagia", new PlanetSky {
                Day = Pal(0x1FA8B8, 0xFFC2CE, 0xFFD6DC, 0x4A7A88, 0xFFE4EC, 0x3E8A9E, 0xFF8AC0, 0x3ABCC0, 0xFFF0DA, 0xE6C4CC, 0.32f, 0.14f, 0.4f),
                Dusk = Pal(0x3A4A8A, 0xFF9A90, 0xF090A8, 0x3A4A5A, 0xFFC0B0, 0x6A4A80, 0xFF7AA8, 0x3A7AB0, 0xFFB08A, 0xC08898, 0.38f, 0.3f, 0.6f),
                Night = Pal(0x0A0818, 0x2A1A3A, 0x2E2240, 0x0A1018, 0x40304E, 0x100C1A, 0xA05AC8, 0x2A3A8A, 0xC8C8FF, 0x1E1830, 0.35f, 0.5f, 0.45f),
                Storm = Pal(0x4A5460, 0x8A9AA4, 0x7A8A94, 0x2A3A44, 0xA0B0B8, 0x3A4650, 0x6A7A8A, 0x2A3440, 0xC8D8E0, 0x6E7E88, 0.98f, 0.05f, 0.85f),
                P1 = Body(new Vector3(-0.3f, 0.2f, 0.86f), 15f, 0xE8DCE0, 0xB8A4AE, 0xFFF0F4, 0.05f, 3.9f, 0.9f),
                P2 = Body(new Vector3(0.62f, 0.28f, 0.6f), 3.5f, 0xFFFFFF, 0xC0C8D8, 0xE0E8FF, 0f, 8.8f, 0.4f),
                SunAzimuth = 240f, CloudScale = 1.1f, CloudDensity = 3.6f, FogDay = 0.006f, FogStorm = 0.035f,
                GradeShadow = Mats.C(0x1E8A98), GradeHighlight = Mats.C(0xFFC0D0), Bank = 0.9f } },
            // NIVALIS: blau-violetter Himmel, zwei Monde, Polarlicht (nachts kräftig, am Tag als Schleier)
            { "nivalis", new PlanetSky {
                Day = Pal(0x18206A, 0xA088E8, 0xB8B0E8, 0xB8C8D8, 0xECE4FF, 0x3A2E7A, 0x6A8AFF, 0x8A3ACC, 0xFFE8D8, 0xA8B0D8, 0.33f, 0.35f, 0.42f),
                Dusk = Pal(0x121640, 0xE07A6A, 0x9A7AB0, 0x8A96A8, 0xE89A90, 0x302A66, 0x8A6AFF, 0x3A1A7A, 0xFF9A7A, 0x6A6A94, 0.38f, 0.45f, 0.55f),
                Night = Pal(0x030612, 0x0E1A30, 0x142440, 0x2A3444, 0x2A3450, 0x080C16, 0x3A6AC8, 0x1A0C3A, 0xB8D0FF, 0x101A2C, 0.3f, 0.55f, 0.4f),
                Storm = Pal(0x8A98AA, 0xC8D4E0, 0xD0DAE4, 0x9AA8B8, 0xE8F0F8, 0x7A8698, 0x9AAAC8, 0x5A6A8A, 0xE8F0FF, 0xC0CCD8, 1f, 0.05f, 1f),
                P1 = Body(new Vector3(0.55f, 0.16f, 0.76f), 11f, 0x9AA4B8, 0x5A6478, 0xFFC8A0, 0.1f, 6.2f, 1.1f),
                P2 = Body(new Vector3(0.22f, 0.26f, 0.93f), 6.5f, 0xB8A898, 0x786858, 0xFFB890, 0.2f, 9.4f, 0.9f),
                SunAzimuth = 20f, Aurora = 1f, CloudDensity = 3.8f, FogDay = 0.009f, FogStorm = 0.06f,
                GradeShadow = Mats.C(0x4A38A8), GradeHighlight = Mats.C(0xC8DCFF), Saturation = 1.15f, Bank = 0.7f } },
        };

        void Awake()
        {
            I = this;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.78f; // Schatten nehmen die (farbige) Himmelsaufhellung an
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.35f;
            Sun.intensity = 1.1f;
            RenderSettings.sun = Sun;
            // Nahbereich der Schattenkaskaden feiner auflösen (die Distanz selbst legen die Einstellungen fest)
            QualitySettings.shadowCascade2Split = 0.22f;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            var sh = Resources.Load<Shader>("RePlanetSky") ?? Shader.Find("RePlanet/Sky");
            if (sh != null && sh.isSupported) { sky = new Material(sh); customSky = true; }
            else sky = new Material(Mats.Template(Mats.Sky));
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            BuildWeather();
            BuildReflections();
            BuildStars();
            for (int i = 0; i < 6; i++)
            {
                var l = new GameObject("LampLight" + i).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point; l.range = 14f; l.intensity = 0f; l.color = new Color(1f, 0.8f, 0.5f);
                l.shadows = LightShadows.None;
                lampPool.Add(l);
            }
        }

        void BuildWeather()
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(transform, false);
            weather = go.AddComponent<ParticleSystem>();
            weather.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = weather.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 3.5f; main.startSpeed = 0f; main.startSize = 0.08f;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = weather.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60, 22, 60);
            var vel = weather.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            var noise = weather.noise;
            noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f;
            weatherRenderer = go.GetComponent<ParticleSystemRenderer>();
            weatherRenderer.sharedMaterial = Mats.Get(Mats.Particle, Color.white);
            weatherRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            weather.Play();
        }

        void BuildStars()
        {
            // Sterne übernimmt der eigene Himmels-Shader; nur beim Rückfall-Himmel als Partikel
            if (customSky) return;
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            stars = go.AddComponent<ParticleSystem>();
            var main = stars.main;
            main.loop = false; main.playOnAwake = false; main.startLifetime = 1e6f; main.startSpeed = 0; main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = stars.emission; em.enabled = false;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Get(Mats.ParticleAdd, Color.white);
            var rng = new System.Random(3);
            var parts = new ParticleSystem.Particle[900];
            for (int i = 0; i < parts.Length; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) + 0.05f;
                parts[i].position = d.normalized * 380f; // lokal um die Kamera, innerhalb der kleinsten Sichtweite (420 m)
                parts[i].startSize = 1.5f + (float)rng.NextDouble() * 2.5f;
                parts[i].startColor = Color.white;
                parts[i].remainingLifetime = 1e6f; parts[i].startLifetime = 1e6f;
            }
            stars.SetParticles(parts, parts.Length);
        }

        static Palette Lerp(Palette a, Palette b, float t)
        {
            return new Palette
            {
                Zenith = Color.Lerp(a.Zenith, b.Zenith, t), Horizon = Color.Lerp(a.Horizon, b.Horizon, t), Haze = Color.Lerp(a.Haze, b.Haze, t),
                Ground = Color.Lerp(a.Ground, b.Ground, t), CloudLight = Color.Lerp(a.CloudLight, b.CloudLight, t), CloudShadow = Color.Lerp(a.CloudShadow, b.CloudShadow, t),
                NebA = Color.Lerp(a.NebA, b.NebA, t), NebB = Color.Lerp(a.NebB, b.NebB, t), Sun = Color.Lerp(a.Sun, b.Sun, t), Fog = Color.Lerp(a.Fog, b.Fog, t),
                Cover = Mathf.Lerp(a.Cover, b.Cover, t), Nebula = Mathf.Lerp(a.Nebula, b.Nebula, t), HazeStrength = Mathf.Lerp(a.HazeStrength, b.HazeStrength, t)
            };
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            string planet = ForcePlanet ?? (WorldView.I != null ? WorldView.I.Planet : null) ?? "terra";
            PlanetSky ps;
            if (!skies.TryGetValue(planet, out ps)) ps = skies["terra"];
            var w = WorldView.I != null ? WorldView.I.World : null;
            float phase = ForcePhase >= 0 ? ForcePhase : (w != null ? Rules.DayPhase(w, planet) : 0.4f);
            float storm = 0f, warn = 0f;
            if (ForceStorm >= 0) storm = ForceStorm;
            else if (w != null)
            {
                var st = w.Planet(planet);
                storm = st.StormActive ? 1f : 0f;
                warn = st.StormWarn ? 0.4f : 0f;
            }
            stormBlend = Mathf.MoveTowards(stormBlend, Mathf.Max(storm, warn), Time.deltaTime * 0.25f);
            float dark = Rules.Darkness(phase);
            Darkness = dark;
            // Sonnenstand: Aufgang 0,25 – Mittag 0,5 – Untergang 0,75
            float elev = Mathf.Sin((phase - 0.25f) * Mathf.PI * 2f);
            float duskAmt = Mathf.Clamp01(1f - Mathf.Abs(elev) * 3.2f);
            var pal = Lerp(ps.Day, ps.Dusk, duskAmt);
            pal = Lerp(pal, ps.Night, dark);
            pal = Lerp(pal, ps.Storm, stormBlend * (1f - dark * 0.6f));
            float elevDeg = Mathf.Lerp(-12f, 62f, (elev + 1f) * 0.5f);
            var sunRot = Quaternion.Euler(Mathf.Max(elevDeg, 3f), ps.SunAzimuth, 0);
            Vector3 sunDir = sunRot * Vector3.back; // Richtung ZUR Sonne
            if (elev < -0.05f)
            {
                // Nachts leuchtet der große Himmelskörper als Mondlicht
                Sun.transform.rotation = Quaternion.LookRotation(-ps.P1.Dir);
                Sun.color = Color.Lerp(new Color(0.55f, 0.65f, 1f), pal.Sun, 0.2f);
                Sun.intensity = 0.28f * (1f - stormBlend * 0.5f);
            }
            else
            {
                Sun.transform.rotation = sunRot;
                Sun.color = pal.Sun;
                Sun.intensity = Mathf.Lerp(0.35f, GameData.Planets.ContainsKey(planet) ? GameData.Planets[planet].SunIntensity : 1.1f, Mathf.Clamp01(elev * 2.5f)) * (1f - stormBlend * 0.55f);
            }
            // Fotomodus-Belichtung: mit Nachbearbeitung wirkt sie dort (vor dem Tonemapping), sonst auf Licht und Himmel
            float bright = (GameApp.I != null ? GameApp.I.Settings.Brightness : 1f) * (PhotoMode.Active && !PostFX.Running ? PhotoMode.Exposure : 1f);
            // Mit HDR-Tonemapping darf die Sonne kräftiger sein (Lichter werden weich abgerollt statt abgeschnitten)
            Sun.intensity *= bright * (PostFX.Running ? 1.3f : 1f);
            if (GameApp.I != null) Sun.shadows = GameApp.I.Settings.Shadows == 0 ? LightShadows.None : GameApp.I.Settings.Shadows == 1 ? LightShadows.Hard : LightShadows.Soft;

            // Blitze im Sturm
            if (storm > 0.5f)
            {
                nextLightning -= Time.deltaTime;
                if (nextLightning <= 0)
                {
                    nextLightning = Random.Range(4f, 12f);
                    bool calm = GameApp.I != null && GameApp.I.Settings.ReduceFlashing;
                    lightning = calm ? 0.25f : 1f;
                    AudioManager.Play("thunder", null, 0.8f);
                }
            }
            lightning = Mathf.MoveTowards(lightning, 0, Time.deltaTime * 3f);

            // Unter Wasser?
            float water = Terrain.WaterLevel(planet);
            Underwater = cam != null && cam.transform.position.y < water - 0.05f;

            // Himmel
            float t = Time.time;
            if (customSky)
            {
                sky.SetColor("_ZenithColor", pal.Zenith);
                sky.SetColor("_HorizonColor", pal.Horizon);
                sky.SetColor("_HazeColor", pal.Haze);
                sky.SetColor("_GroundColor", pal.Ground);
                sky.SetFloat("_HazeStrength", pal.HazeStrength);
                sky.SetVector("_SunDir", sunDir);
                sky.SetColor("_SunColor", pal.Sun * (1f - dark * 0.9f));
                sky.SetFloat("_SunIntensity", elev > -0.05f ? 3f * (1f - stormBlend * 0.8f) : 0f);
                sky.SetFloat("_SunSize", 0.03f + duskAmt * 0.015f);
                sky.SetColor("_CloudLight", pal.CloudLight);
                sky.SetColor("_CloudShadow", pal.CloudShadow);
                sky.SetFloat("_CloudCover", pal.Cover);
                sky.SetFloat("_CloudDensity", ps.CloudDensity + stormBlend * 1.5f);
                sky.SetFloat("_CloudScale", ps.CloudScale);
                sky.SetFloat("_CloudSpeed", 0.02f + stormBlend * 0.08f);
                float wx = 1f, wz = 0.3f;
                if (w != null) Rules.Wind(w, planet, out wx, out wz);
                sky.SetVector("_CloudWind", new Vector4(wx, 0, wz, 0));
                sky.SetColor("_NebulaA", pal.NebA);
                sky.SetColor("_NebulaB", pal.NebB);
                sky.SetFloat("_NebulaStrength", pal.Nebula * (1f - stormBlend));
                sky.SetFloat("_StarStrength", dark * (1f - stormBlend * 0.9f));
                SetBody("_P1", ps.P1, stormBlend, dark);
                SetBody("_P2", ps.P2, stormBlend, dark);
                sky.SetColor("_AuroraA", ps.AuroraA);
                sky.SetColor("_AuroraB", ps.AuroraB);
                sky.SetFloat("_AuroraStrength", ps.Aurora * Mathf.Lerp(0.3f, 1f, dark) * (1f - stormBlend));
                sky.SetFloat("_BankStrength", ps.Bank * Mathf.Lerp(1f, 0.7f, dark) * (1f + stormBlend * 0.25f));
                sky.SetFloat("_BankHeight", ps.BankHeight * (1f + stormBlend * 0.4f));
                sky.SetFloat("_SunGlow", elev > -0.05f ? (1f + duskAmt * 0.8f) * (1f - stormBlend * 0.7f) : 0f);
                sky.SetFloat("_Exposure", (1f + lightning * 1.5f) * bright);
                sky.SetFloat("_SkyTime", t);
                sky.SetFloat("_Detail", GameApp.I == null || GameApp.I.Settings.Quality >= 2 ? 1f : 0f);
            }
            else
            {
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", pal.Zenith);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", pal.Ground);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(1.3f, 0.2f, dark) * bright);
                if (stars != null) stars.gameObject.SetActive(dark > 0.3f);
            }

            UpdateLook(ps, pal, sunDir, elev, dark, duskAmt, water);

            // Nebel: Farbe = Horizontdunst, damit Landschaft und Himmel verschmelzen
            float view = GameApp.I != null ? GameApp.I.Settings.ViewDistance : 1f;
            float fogDensity = Mathf.Lerp(ps.FogDay, ps.FogStorm, stormBlend) / Mathf.Max(0.5f, view);
            var fogColor = Color.Lerp(pal.Fog, pal.Haze, 0.35f);
            if (Underwater)
            {
                fogColor = Color.Lerp(new Color(0.05f, 0.25f, 0.3f), new Color(0.02f, 0.06f, 0.1f), dark);
                fogDensity = 0.06f;
            }
            RenderSettings.fogColor = fogColor * (1f + lightning * 0.6f);
            RenderSettings.fogDensity = fogDensity;
            // Umgebungslicht aus dem Himmel: Zenit (mit Wolkenlicht aufgehellt) von oben, Horizontdunst von der Seite,
            // Boden + warmes Sonnen-Rückstrahlen von unten – farbige Schatten statt grauer
            float day = 1f - dark;
            RenderSettings.ambientSkyColor = Color.Lerp(pal.Zenith, pal.CloudLight, 0.3f) * Mathf.Lerp(1.0f, 0.55f, dark) * bright + Color.white * lightning * 0.5f;
            RenderSettings.ambientEquatorColor = Color.Lerp(pal.Horizon, pal.Haze, 0.3f) * Mathf.Lerp(0.8f, 0.35f, dark) * bright;
            RenderSettings.ambientGroundColor = (pal.Ground * 0.55f + pal.Sun * 0.12f * day * (1f - stormBlend)) * bright;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Lerp(420f, 900f, Mathf.Clamp01((view - 0.5f)));
                if (stars != null) stars.transform.position = cam.transform.position; // Sterne wandern mit der Kamera
                UpdateWeather(cam, planet, storm, dark);
                UpdateLampPool(cam, dark);
                UpdateReflections(cam);
            }
        }

        float stormBlend;

        /// <summary>Bildlook für die Nachbearbeitung aus Palette, Tageszeit und Wetter.</summary>
        void UpdateLook(PlanetSky ps, Palette pal, Vector3 sunDir, float elev, float dark, float duskAmt, float water)
        {
            var L = Look;
            L.SunDir = sunDir;
            float day = Mathf.Clamp01(elev * 4f + 0.2f);
            L.SunColor = pal.Sun * Mathf.Lerp(0.35f, 1.3f, day) * (1f - stormBlend * 0.7f);
            L.SkyCube = probe != null ? probe.texture : null;
            L.FogSky = Underwater ? 0f : 0.85f;
            L.FogFalloff = Underwater ? 0f : 0.035f;
            L.FogBase = water > -50f ? water : 0f;
            L.FogLinear = Underwater ? 2.5f : 0.35f + stormBlend * 0.6f;
            L.FogSunScatter = Underwater ? 0f : (0.35f + duskAmt * 0.5f) * (1f - dark) * (1f - stormBlend * 0.6f);
            L.FogMax = 1f;
            L.Exposure = Mathf.Lerp(0.85f, 1.05f, dark); // ACES hebt Mitteltöne an → etwas unter 1
            L.Contrast = Mathf.Lerp(ps.Contrast, 1.05f, stormBlend * 0.7f);
            L.Saturation = Mathf.Lerp(ps.Saturation, 1.0f, stormBlend * 0.6f) * Mathf.Lerp(1f, 0.92f, dark);
            L.Vibrance = 0.45f;
            L.SplitAmount = Mathf.Lerp(0.3f, 0.42f, dark) * (1f - stormBlend * 0.4f);
            L.ShadowTint = Color.Lerp(ps.GradeShadow, pal.Zenith, dark * 0.5f);
            L.HighlightTint = Color.Lerp(ps.GradeHighlight, pal.Sun, duskAmt * 0.5f);
            L.VignetteColor = Color.Lerp(new Color(0.2f, 0.18f, 0.26f), ps.GradeShadow * 0.5f, 0.5f);
            L.Vignette = 0.5f;
            L.Bloom = Mathf.Lerp(0.2f, 0.32f, Mathf.Max(duskAmt, dark));
            L.BloomThreshold = Mathf.Lerp(0.95f, 0.7f, dark);
            L.ShaftStrength = elev > -0.02f && !Underwater ? (0.5f + duskAmt * 0.6f) * (1f - stormBlend * 0.85f) * (1f - dark) : 0f;
            L.ShaftThreshold = Mathf.Lerp(0.55f, 0.35f, duskAmt);
            if (Underwater)
            {
                L.ShadowTint = new Color(0.1f, 0.4f, 0.5f);
                L.HighlightTint = new Color(0.6f, 1f, 0.95f);
                L.Saturation = 1.05f;
            }
        }

        // ------------------------------------------------------------ Reflexionen
        // Die Szene ist leer (keine gebackene Beleuchtung), daher gäbe es ohne eigene Sonde keine Umgebungsreflexion
        // für Metall und Wasser. Eine kleine Echtzeit-Sonde rendert nur den (animierten) Himmel und folgt der Kamera.
        ReflectionProbe probe;
        float probeTimer;

        void BuildReflections()
        {
            try
            {
                QualitySettings.realtimeReflectionProbes = true;
                var go = new GameObject("SkyReflection");
                go.transform.SetParent(transform, false);
                probe = go.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
                probe.cullingMask = 0; // nur Himmel
                probe.resolution = 64;
                probe.hdr = true;
                probe.size = new Vector3(2000f, 2000f, 2000f);
                probe.importance = 0;
                probe.RenderProbe();
            }
            catch (System.Exception e) { Debug.LogWarning("[RE:PLANET] Reflexionssonde: " + e.Message); probe = null; }
        }

        void UpdateReflections(Camera cam)
        {
            if (probe == null) return;
            probe.transform.position = cam.transform.position;
            probeTimer -= Time.unscaledDeltaTime;
            if (probeTimer > 0) return;
            probeTimer = 1.5f; // Himmel ändert sich langsam; Neuberechnung verteilt über mehrere Bilder
            probe.RenderProbe();
        }

        void SetBody(string p, SkyBody b, float storm, float dark)
        {
            sky.SetVector(p + "Dir", new Vector4(b.Dir.x, b.Dir.y, b.Dir.z, b.Radius));
            sky.SetColor(p + "ColorA", b.A);
            sky.SetColor(p + "ColorB", b.B);
            sky.SetColor(p + "Rim", b.Rim);
            // Auch am Tag deutlich sichtbar (Stil: große Himmelskörper über dem Horizont)
            float vis = Mathf.Lerp(0.85f, 1f, dark) * (1f - storm * 0.85f);
            sky.SetVector(p + "Params", new Vector4(b.Bands, b.Seed, vis, b.RimStrength));
        }

        void UpdateWeather(Camera cam, string planet, float storm, float dark)
        {
            if (weather == null) return;
            int quality = GameApp.I != null ? GameApp.I.Settings.Particles : 2;
            weather.transform.position = cam.transform.position + cam.transform.forward * 12f;
            float wx = 1, wz = 0.3f, wind = 0.2f;
            var w = WorldView.I != null ? WorldView.I.World : null;
            if (w != null) wind = Rules.Wind(w, planet, out wx, out wz);
            string kind = planet == "nivalis" ? "snow" : planet == "pyra" ? "sand" : planet == "pelagia" ? (storm > 0.5f ? "rain" : "spray") : "dust";
            if (Underwater) kind = "bubbles";
            var main = weather.main;
            var em = weather.emission;
            var vel = weather.velocityOverLifetime;
            float rate;
            float speed = wind * (storm > 0.5f ? 22f : 7f);
            if (kind != weatherKind)
            {
                weatherKind = kind;
                weather.Clear();
                Color c = kind == "snow" ? new Color(1, 1, 1, 0.9f) : kind == "sand" ? new Color(0.85f, 0.5f, 0.3f, 0.55f) : kind == "rain" ? new Color(0.7f, 0.8f, 0.9f, 0.5f)
                    : kind == "bubbles" ? new Color(0.8f, 0.95f, 1f, 0.5f) : kind == "spray" ? new Color(1, 1, 1, 0.35f) : new Color(1f, 0.9f, 0.75f, 0.35f);
                weatherRenderer.sharedMaterial = Mats.Get(Mats.Particle, c);
                main.startColor = c;
                main.startSize = kind == "snow" ? 0.09f : kind == "rain" ? 0.05f : kind == "bubbles" ? 0.08f : 0.06f;
                weatherRenderer.renderMode = kind == "rain" ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
                weatherRenderer.velocityScale = kind == "rain" ? 0.08f : 0f;
            }
            switch (kind)
            {
                case "snow": rate = 120 + storm * 900; vel.x = wx * speed; vel.y = -1.5f - storm * 2; vel.z = wz * speed; break;
                case "sand": rate = 40 + wind * 120 + storm * 1400; vel.x = wx * speed * 1.3f; vel.y = -0.2f; vel.z = wz * speed * 1.3f; break;
                case "rain": rate = 1600; vel.x = wx * speed * 0.4f; vel.y = -14f; vel.z = wz * speed * 0.4f; break;
                case "bubbles": rate = 60; vel.x = 0; vel.y = 1.2f; vel.z = 0; break;
                case "spray": rate = 30 + wind * 80; vel.x = wx * speed; vel.y = 0.2f; vel.z = wz * speed; break;
                default: rate = 30 + wind * 60 + storm * 700; vel.x = wx * speed; vel.y = -0.1f; vel.z = wz * speed; break;
            }
            rate *= quality == 0 ? 0.25f : quality == 1 ? 0.6f : 1f;
            em.rateOverTime = rate;
        }

        void UpdateLampPool(Camera cam, float dark)
        {
            if (WorldView.I == null) return;
            if (dark < 0.2f) { foreach (var l in lampPool) l.intensity = 0; return; }
            WorldView.I.CollectLitLamps(cam.transform.position, 60f, litLamps);
            litLamps.Sort((a, b) => (a - cam.transform.position).sqrMagnitude.CompareTo((b - cam.transform.position).sqrMagnitude));
            for (int i = 0; i < lampPool.Count; i++)
            {
                if (i < litLamps.Count)
                {
                    lampPool[i].transform.position = litLamps[i] + Vector3.down * 0.4f;
                    lampPool[i].intensity = 1.6f * dark;
                }
                else lampPool[i].intensity = 0;
            }
        }
    }
}
