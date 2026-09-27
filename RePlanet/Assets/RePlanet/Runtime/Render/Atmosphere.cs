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
        }

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
            // TERRA: goldene Melancholie, großer blasser Mond
            { "terra", new PlanetSky {
                Day = Pal(0x4F86CC, 0xF6C488, 0xEFC49A, 0x6B5E4E, 0xFFE8C8, 0x8E7A86, 0xE58A55, 0x6B4A8A, 0xFFE2B0, 0xE6C29A, 0.3f, 0.1f, 0.45f),
                Dusk = Pal(0x3B3566, 0xFF8A4A, 0xF09A6A, 0x4A3A36, 0xFFB078, 0x5A3A52, 0xFF7A45, 0x55306E, 0xFF9A55, 0xD9885E, 0.55f, 0.22f, 0.65f),
                Night = Pal(0x060A1C, 0x1E1A33, 0x2A2440, 0x0E0C12, 0x3A3650, 0x12101C, 0x5A3C7A, 0x1A2A5A, 0x9DB6FF, 0x1C1A2C, 0.35f, 0.45f, 0.45f),
                Storm = Pal(0x6E6258, 0xB09070, 0xA88A6E, 0x5A4C40, 0xB89C80, 0x5A4A40, 0x8A6A50, 0x4A3A30, 0xE0C090, 0xA48A70, 0.95f, 0.05f, 0.9f),
                P1 = Body(new Vector3(0.45f, 0.38f, 0.8f), 12f, 0xC8D0E0, 0x8C96AE, 0x9EC8FF, 0.15f, 1.7f, 1f),
                P2 = Body(new Vector3(-0.62f, 0.22f, 0.55f), 2.6f, 0xE8C8A8, 0xB08868, 0xFFD8A8, 0f, 4.2f, 0.6f),
                SunAzimuth = 200f, FogDay = 0.007f, FogStorm = 0.028f } },
            // PYRA: orange Nebelwolken, dunkles Dunstband, dunkler Riesenplanet mit Lichtsaum
            { "pyra", new PlanetSky {
                Day = Pal(0x7A2E1E, 0xF09058, 0x2E4646, 0x5A2A1E, 0xFFA066, 0x6E2A1A, 0xFF7A3D, 0x6A1E2A, 0xFFC99A, 0x9A5A40, 0.72f, 0.3f, 0.7f),
                Dusk = Pal(0x3A1426, 0xFF6A2E, 0x1E3438, 0x3A1812, 0xFF7A3A, 0x4A1418, 0xFF5A2A, 0x4A1040, 0xFF8A45, 0x7A3A30, 0.78f, 0.45f, 0.75f),
                Night = Pal(0x0C0508, 0x3A140C, 0x1A2224, 0x120806, 0x4A2014, 0x14080A, 0xC0502A, 0x3A0C2A, 0xFFB080, 0x2A140E, 0.6f, 0.6f, 0.6f),
                Storm = Pal(0x8A4A2A, 0xC8784A, 0xB0663E, 0x6A3420, 0xD08A5A, 0x6A3420, 0xC0602E, 0x5A2418, 0xFFB070, 0xB06A44, 1f, 0.1f, 1f),
                P1 = Body(new Vector3(-0.45f, 0.42f, 0.79f), 17f, 0x2A3048, 0x485070, 0xFFC890, 0.35f, 2.3f, 1.3f),
                P2 = Body(new Vector3(0.55f, 0.55f, 0.62f), 3.5f, 0xC06A4A, 0x7A3A2A, 0xFF9A6A, 0f, 7.1f, 0.5f),
                SunAzimuth = 160f, CloudScale = 0.7f, CloudDensity = 2.8f, FogDay = 0.009f, FogStorm = 0.05f } },
            // PELAGIA: pastell-rosa, heller Mond, weiche Wolken
            { "pelagia", new PlanetSky {
                Day = Pal(0xD08AB0, 0xFFDCD0, 0xF4D2D2, 0x5A7A8A, 0xFFF4F4, 0xC896AE, 0xFF9AC8, 0x8A6AC8, 0xFFF0DA, 0xEAC8CC, 0.22f, 0.14f, 0.5f),
                Dusk = Pal(0x6A4A8A, 0xFFA890, 0xE890A0, 0x3A4A5A, 0xFFC0B0, 0x7A5070, 0xFF7AA8, 0x5A3A9A, 0xFFB08A, 0xC08898, 0.45f, 0.3f, 0.65f),
                Night = Pal(0x0A0818, 0x2A1A3A, 0x2E2240, 0x0A1018, 0x40304E, 0x100C1A, 0xA05AC8, 0x2A3A8A, 0xC8C8FF, 0x1E1830, 0.35f, 0.5f, 0.45f),
                Storm = Pal(0x4A5460, 0x8A9AA4, 0x7A8A94, 0x2A3A44, 0xA0B0B8, 0x3A4650, 0x6A7A8A, 0x2A3440, 0xC8D8E0, 0x6E7E88, 0.98f, 0.05f, 0.85f),
                P1 = Body(new Vector3(-0.3f, 0.4f, 0.86f), 14f, 0xE8DCE0, 0xB8A4AE, 0xFFF0F4, 0.05f, 3.9f, 0.9f),
                P2 = Body(new Vector3(0.62f, 0.5f, 0.6f), 3.5f, 0xFFFFFF, 0xC0C8D8, 0xE0E8FF, 0f, 8.8f, 0.4f),
                SunAzimuth = 240f, CloudScale = 1.1f, CloudDensity = 2.0f, FogDay = 0.006f, FogStorm = 0.035f } },
            // NIVALIS: eisige Dämmerung, zwei Monde, Polarlicht
            { "nivalis", new PlanetSky {
                Day = Pal(0x1C2A5A, 0xE8B090, 0xC8B4B8, 0xB8C8D8, 0xF0D0C0, 0x46465E, 0x6A8AFF, 0x2A2A6A, 0xFFE0C8, 0xA8B4C8, 0.32f, 0.3f, 0.5f),
                Dusk = Pal(0x141A40, 0xE07A4A, 0x9A7A8A, 0x8A96A8, 0xE89A70, 0x302A46, 0x8A6AFF, 0x2A1A5A, 0xFF9A6A, 0x6A6A84, 0.4f, 0.4f, 0.6f),
                Night = Pal(0x030612, 0x0E1A30, 0x142440, 0x2A3444, 0x2A3450, 0x080C16, 0x3A6AC8, 0x1A0C3A, 0xB8D0FF, 0x101A2C, 0.3f, 0.55f, 0.4f),
                Storm = Pal(0x8A98AA, 0xC8D4E0, 0xD0DAE4, 0x9AA8B8, 0xE8F0F8, 0x7A8698, 0x9AAAC8, 0x5A6A8A, 0xE8F0FF, 0xC0CCD8, 1f, 0.05f, 1f),
                P1 = Body(new Vector3(0.55f, 0.34f, 0.76f), 10f, 0x9AA4B8, 0x5A6478, 0xFFC8A0, 0.1f, 6.2f, 1.1f),
                P2 = Body(new Vector3(0.22f, 0.3f, 0.93f), 6.5f, 0xB8A898, 0x786858, 0xFFB890, 0.2f, 9.4f, 0.9f),
                SunAzimuth = 20f, Aurora = 1f, CloudDensity = 2.2f, FogDay = 0.009f, FogStorm = 0.06f } },
        };

        void Awake()
        {
            I = this;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.8f;
            Sun.intensity = 1.1f;
            RenderSettings.sun = Sun;
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
            float bright = (GameApp.I != null ? GameApp.I.Settings.Brightness : 1f) * (PhotoMode.Active ? PhotoMode.Exposure : 1f); // Fotomodus: Belichtung
            Sun.intensity *= bright;
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
                sky.SetFloat("_AuroraStrength", ps.Aurora * dark * (1f - stormBlend));
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
            RenderSettings.ambientSkyColor = pal.Zenith * Mathf.Lerp(1.0f, 0.55f, dark) * bright + Color.white * lightning * 0.5f;
            RenderSettings.ambientEquatorColor = pal.Horizon * Mathf.Lerp(0.8f, 0.35f, dark) * bright;
            RenderSettings.ambientGroundColor = pal.Ground * 0.55f * bright;
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
            float vis = Mathf.Lerp(0.55f, 1f, dark) * (1f - storm * 0.85f);
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
