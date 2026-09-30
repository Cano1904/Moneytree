using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Planetenwahl als Weltall-Szene: große prozedurale Planetenkugeln (Oberfläche, Wolken, Atmosphärensaum je Planet;
    /// NIVALIS abgedunkelt), Sternenhimmel, farbige Nebel, Sonne, Asteroidengürtel, eine Raumstation und vorbeiziehende Frachter.
    /// Die Kamera gleitet sanft zum ausgewählten Planeten (<see cref="Focus"/>). Beim Bestätigen (<see cref="Descend"/>) fliegt
    /// sie auf den Planeten zu, tritt in die Atmosphäre ein (Glühen – als Überblendung von der Oberfläche gezeichnet,
    /// <see cref="Overlay"/>) und startet dann die Welt; die Landung zeigt <see cref="ShipArrival"/>.
    /// Die Bühne liegt weit abseits der Spielwelt; die Menüwelt wird währenddessen ausgeblendet. Nebel, Hintergrund,
    /// Sichtweite und Licht werden nur für diese Szene gesetzt und danach zurückgestellt.
    /// Licht: Die Sonne steht links vor der Kamera (Planeten zu gut zwei Dritteln beleuchtet, weiche Tag-Nacht-Grenze
    /// rechts); die Planeten rechnen ihr Licht selbst (Resources/RePlanetPlanet.shader: Terminator-Streulicht, Ozeanglanz,
    /// Atmosphärenschimmer, Nachtseite in kühlem Streulicht) und haben einen Atmosphärensaum
    /// (Resources/RePlanetPlanetAtmosphere.shader, Gegenlicht-Sichel). Fehlen die Shader: Standard-Material + Leuchtring.
    /// Für den Bildlook setzt die Szene eine feste Belichtung, kräftigen Bloom und satte Farben (Atmosphere.Look).
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class PlanetSelectScene : MonoBehaviour
    {
        public static PlanetSelectScene I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(PlanetSelectScene))) GameApp.Components.Add(typeof(PlanetSelectScene));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<PlanetSelectScene>() == null) GameApp.I.gameObject.AddComponent<PlanetSelectScene>();
        }

        /// <summary>Bühne ist aufgebaut und aktiv (die Oberfläche zeichnet dann die schlanke Auswahl).</summary>
        public bool Active { get { return active && built; } }
        /// <summary>Anflug läuft (Oberfläche zeigt nur noch den Hinweis zum Überspringen).</summary>
        public bool Descending { get { return descending; } }
        /// <summary>Überblendung 0..1 (Atmosphäreneintritt, Laden) – von der Oberfläche über alles gezeichnet.</summary>
        public float Overlay { get; private set; }
        public Color OverlayColor { get; private set; } = new Color(1f, 0.8f, 0.6f);
        /// <summary>Aktuell ausgewählter Planet.</summary>
        public string Focused { get { return focusId; } }

        static readonly Vector3 StageOrigin = new Vector3(0f, 2000f, 60000f);

        sealed class Body
        {
            public string Id;
            public Transform T, Clouds, Rim, Atmo;
            public Material SurfMat, AtmoMat;
            public float R;
            public bool Locked;
            public Color RimColor;
        }

        readonly List<Body> bodies = new List<Body>();
        readonly List<Transform> billboards = new List<Transform>();
        readonly List<Transform> ships = new List<Transform>();
        readonly List<Vector3> shipA = new List<Vector3>(), shipB = new List<Vector3>();
        readonly List<float> shipSpeed = new List<float>(), shipPhase = new List<float>();
        Transform stage, belt, stationRing;
        Material beacon;
        // Richtung ZUR Sonne: links oben, leicht hinter der Kamera (die Kamera blickt etwa nach +z)
        Vector3 sunDir = new Vector3(-0.88f, 0.3f, -0.36f).normalized;
        static readonly Color SunLight = new Color(1f, 0.95f, 0.86f);
        const float SunIntensity = 1.5f, PlanetLight = 2.6f;
        Shader planetShader, atmoShader;
        bool shadersLoaded;
        bool built, active, descending, started;
        string focusId;
        float descentT, orbit, idleT;
        Vector3 camPos, camLook, camVel, lookVel, descentFrom, descentLook;
        string descentId;

        // Gesicherter Zustand
        bool saved;
        CameraClearFlags oldClear; Color oldBg; float oldFar, oldNear;
        bool oldFog;
        GameObject hiddenRoot;

        static Texture2D soft, rimTex, cloudTex;

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
        }

        void OnDestroy() { if (I == this) I = null; }

        // ================================================================== Steuerung von der Oberfläche
        /// <summary>Kamera sanft zu diesem Planeten gleiten lassen.</summary>
        public void Focus(string planetId)
        {
            if (descending || planetId == null || planetId == focusId) return;
            focusId = planetId;
            idleT = 0f;
            if (active) AudioManager.Play("whoosh", null, 0.25f, 1.4f);
        }

        /// <summary>Anflug auf den Planeten, danach <see cref="GameApp.StartNewWorld"/>. Ohne aktive Bühne sofort starten.</summary>
        public void Descend(string planetId)
        {
            if (descending) return;
            var app = GameApp.I;
            if (app == null) return;
            if (!Active || Find(planetId) == null) { app.StartNewWorld(planetId); return; }
            Focus(planetId);
            descending = true;
            descentId = planetId;
            descentT = 0f;
            started = false;
            descentFrom = camPos;
            descentLook = camLook;
            var b = Find(planetId);
            OverlayColor = b != null ? Color.Lerp(b.RimColor, new Color(1f, 0.75f, 0.5f), 0.55f) : new Color(1f, 0.8f, 0.6f);
            AudioManager.Play("whoosh", null, 0.9f, 0.7f);
            AudioManager.Loop("planetsel_wind", "wind_loop", true, null, 0.2f, 0.8f);
        }

        Body Find(string id) { foreach (var b in bodies) if (b.Id == id) return b; return null; }

        // ================================================================== Schleife
        void LateUpdate()
        {
            var app = GameApp.I;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool want = app != null && (app.Mode == AppMode.PlanetSelect && UIState.Screen == UIScreen.PlanetSelect || descending && !started);
            if (want && !active) Begin();
            else if (!want && active) End();

            // Überblendung nach dem Start der Welt langsam zurücknehmen
            if (!active)
            {
                if (Overlay > 0f)
                {
                    bool loading = app != null && app.Mode == AppMode.Loading;
                    if (!loading) Overlay = Mathf.MoveTowards(Overlay, 0f, dt / 1.4f);
                }
                return;
            }
            try { Animate(app, dt); }
            catch (Exception e) { Debug.LogException(e); descending = false; End(); }
        }

        void Begin()
        {
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); built = false; }
            if (!built) return;
            active = true;
            started = false;
            stage.gameObject.SetActive(true);
            var cam = CameraRig.I != null ? CameraRig.I.Cam : Camera.main;
            if (cam != null && !saved)
            {
                oldClear = cam.clearFlags; oldBg = cam.backgroundColor; oldFar = cam.farClipPlane; oldNear = cam.nearClipPlane;
                oldFog = RenderSettings.fog;
                saved = true;
            }
            if (CameraRig.I != null) CameraRig.I.Cinematic = true;
            if (WorldView.I != null && WorldView.I.Root != null && WorldView.I.Root.gameObject.activeSelf)
            {
                hiddenRoot = WorldView.I.Root.gameObject;
                hiddenRoot.SetActive(false);
            }
            if (focusId == null || Find(focusId) == null)
            {
                foreach (var b in bodies) if (!b.Locked) { focusId = b.Id; break; }
            }
            var fb = Find(focusId);
            if (fb != null) { FocusPose(fb, orbit, out camPos, out camLook); camPos += (camPos - fb.T.position).normalized * fb.R * 2.5f; }
            camVel = lookVel = Vector3.zero;
            Overlay = 0f;
        }

        void End()
        {
            active = false;
            if (stage != null) stage.gameObject.SetActive(false);
            var cam = CameraRig.I != null ? CameraRig.I.Cam : Camera.main;
            if (cam != null && saved)
            {
                cam.clearFlags = oldClear; cam.backgroundColor = oldBg; cam.farClipPlane = oldFar; cam.nearClipPlane = oldNear;
                RenderSettings.fog = oldFog;
            }
            saved = false;
            if (CameraRig.I != null) CameraRig.I.Cinematic = false;
            if (hiddenRoot != null) hiddenRoot.SetActive(true);
            hiddenRoot = null;
            AudioManager.Loop("planetsel_wind", "wind_loop", false);
            if (!started) { descending = false; Overlay = 0f; }
        }

        void Animate(GameApp app, float dt)
        {
            var cam = CameraRig.I != null ? CameraRig.I.Cam : Camera.main;
            if (cam == null) return;
            // Bildlook für das All (nach Atmosphere, dank Ausführungsreihenfolge)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.013f, 0.032f);
            cam.farClipPlane = 40000f;
            cam.nearClipPlane = 0.5f;
            RenderSettings.fog = false;
            // Umgebungslicht: kühles Streulicht aus dem All, damit Schattenseiten (Station, Asteroiden, Frachter) Form zeigen
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.21f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.12f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.06f, 0.1f);
            var sun = Atmosphere.I != null ? Atmosphere.I.Sun : RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                sun.color = SunLight;
                sun.intensity = SunIntensity;
            }
            SpaceLook();

            orbit += dt * 1.6f;
            idleT += dt;
            // Planeten, Wolken, Station, Gürtel, Schiffe
            foreach (var b in bodies)
            {
                b.T.Rotate(0f, dt * (b.Locked ? 0.4f : 1.2f), 0f, Space.Self);
                if (b.Clouds != null) b.Clouds.Rotate(0f, dt * 0.7f, 0f, Space.Self);
                if (b.SurfMat != null) b.SurfMat.SetVector("_SunDirW", sunDir);
                if (b.AtmoMat != null)
                {
                    b.AtmoMat.SetVector("_SunDirW", sunDir);
                    var c = b.T.position;
                    b.AtmoMat.SetVector("_Center", new Vector4(c.x, c.y, c.z, b.R));
                }
            }
            if (belt != null) belt.Rotate(0f, dt * 0.35f, 0f, Space.Self);
            if (stationRing != null) stationRing.Rotate(0f, 0f, dt * 6f, Space.Self);
            if (beacon != null) Mats.SetEmission(beacon, Mathf.Repeat(Time.unscaledTime * 0.8f, 1f) < 0.15f ? new Color(4f, 0.5f, 0.3f) : new Color(0.3f, 0.04f, 0.02f));
            float tt = Time.unscaledTime;
            for (int i = 0; i < ships.Count; i++)
            {
                float f = Mathf.Repeat(tt * shipSpeed[i] + shipPhase[i], 1f);
                var p = Vector3.Lerp(shipA[i], shipB[i], f);
                ships[i].localPosition = p;
                ships[i].localRotation = Quaternion.LookRotation(shipB[i] - shipA[i]);
            }

            // Kamera
            Vector3 wantPos, wantLook;
            var fb = Find(descending ? descentId : focusId);
            if (fb == null) return;
            if (!descending)
            {
                FocusPose(fb, orbit, out wantPos, out wantLook);
                camPos = Vector3.SmoothDamp(camPos, wantPos, ref camVel, 1.3f, Mathf.Infinity, dt);
                camLook = Vector3.SmoothDamp(camLook, wantLook, ref lookVel, 1.0f, Mathf.Infinity, dt);
            }
            else
            {
                descentT += dt;
                bool skip = descentT > 0.3f && (Input.anyKeyDown || InputMap.NavBack());
                const float dur = 3.4f;
                if (skip) descentT = Mathf.Max(descentT, dur);
                float u = Mathf.Clamp01(descentT / dur);
                float e = u * u * u;
                var center = fb.T.position;
                var toCam = (descentFrom - center).normalized;
                var surface = center + toCam * fb.R * 1.01f;
                camPos = Vector3.Lerp(descentFrom, surface, e);
                camLook = Vector3.Lerp(descentLook, center, Mathf.Clamp01(u * 1.6f));
                float glow = Mathf.Clamp01((u - 0.55f) / 0.4f);
                Overlay = Mathf.Max(Overlay, glow * glow);
                AudioManager.Loop("planetsel_wind", "wind_loop", true, null, 0.2f + 0.7f * u, 0.8f + 0.6f * u);
                if (u >= 1f && !started)
                {
                    started = true;
                    Overlay = 1f;
                    AudioManager.Play("whoosh", null, 1f, 0.5f);
                    AudioManager.Loop("planetsel_wind", "wind_loop", false);
                    descending = false;
                    try { app.StartNewWorld(descentId); }
                    catch (Exception ex) { Debug.LogException(ex); Overlay = 0f; }
                    return;
                }
            }
            var fwd = camLook - camPos;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            float shake = descending ? Mathf.Clamp01((descentT / 3.4f - 0.5f) * 2f) * 0.004f : 0f;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(fwd.normalized + UnityEngine.Random.insideUnitSphere * shake, Vector3.up));
            // Leuchtflächen zur Kamera drehen
            var rot = cam.transform.rotation;
            foreach (var bb in billboards) if (bb != null) bb.rotation = rot;
        }

        /// <summary>
        /// Bildlook im All (nach Atmosphere, dank Ausführungsreihenfolge; Atmosphere rechnet ihn jedes Bild neu, nach dem
        /// Verlassen der Szene gilt also wieder der normale Look): feste Belichtung statt der automatischen (das All ist
        /// fast schwarz), kräftiger Bloom für Sonne, Sterne und Nebel, satte Farben, keine Konturen/Verdeckung/Strahlen.
        /// </summary>
        void SpaceLook()
        {
            var at = Atmosphere.I;
            if (at == null) return;
            var L = at.Look;
            L.AutoExposure = false;
            L.Exposure = 0.95f;
            L.Contrast = 1.08f;
            L.Saturation = 1.28f;
            L.Vibrance = 0.55f;
            L.SplitAmount = 0.2f;
            L.ShadowTint = new Color(0.36f, 0.3f, 0.78f);
            L.HighlightTint = new Color(1f, 0.86f, 0.7f);
            L.Vignette = 0.42f;
            L.VignetteColor = new Color(0.14f, 0.1f, 0.24f);
            L.Bloom = 0.38f;
            L.BloomThreshold = 0.85f;
            L.ShaftStrength = 0f;
            L.Outline = 0f;
            L.AO = 0f;
            L.FogSky = 0f;
            L.SunDir = sunDir;
            L.SunColor = SunLight * SunIntensity;
        }

        /// <summary>Kameralage für einen Planeten: seitlich-vorn, der Planet sitzt rechts der Bildmitte (links ist Platz für die Auswahl).</summary>
        void FocusPose(Body b, float orbitDeg, out Vector3 pos, out Vector3 look)
        {
            var c = b.T.position;
            var dir = Quaternion.Euler(0f, -20f + Mathf.Sin(orbitDeg * Mathf.Deg2Rad * 0.6f) * 8f, 0f) * new Vector3(-0.25f, 0.18f, -1f).normalized;
            pos = c + dir * b.R * 3.3f;
            var right = Vector3.Cross(Vector3.up, (c - pos).normalized).normalized;
            look = c - right * b.R * 0.95f;
        }

        // ================================================================== Bühnenbau
        void Build()
        {
            if (built) return;
            var root = new GameObject("Planetenwahl_All").transform;
            root.SetParent(transform, false);
            root.position = StageOrigin;
            stage = root;
            bodies.Clear(); billboards.Clear(); ships.Clear(); shipA.Clear(); shipB.Clear(); shipSpeed.Clear(); shipPhase.Clear();

            BuildStars(root);
            BuildSun(root);

            Vector3[] slots = { new Vector3(0f, 0f, 0f), new Vector3(950f, 140f, 520f), new Vector3(-1050f, -90f, 760f), new Vector3(260f, 90f, 1900f), new Vector3(-600f, 200f, 2400f) };
            float[] radii = { 170f, 120f, 190f, 150f, 130f };
            int i = 0;
            foreach (var id in GameData.PlanetOrder)
            {
                if (i >= slots.Length) break;
                PlanetDef pd;
                if (!GameData.Planets.TryGetValue(id, out pd)) continue;
                bodies.Add(BuildPlanet(root, pd, slots[i], radii[i], i));
                i++;
            }
            BuildBelt(root);
            if (bodies.Count > 0) BuildStation(root, bodies[0]);
            BuildShips(root);
            root.gameObject.SetActive(false);
            built = true;
        }

        void BuildStars(Transform root)
        {
            var r = new System.Random(4711);
            Func<float> rnd = () => (float)r.NextDouble();
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            int n = 2600;
            var band = Quaternion.Euler(64f, 25f, 0f);
            for (int k = 0; k < n; k++)
            {
                Vector3 d;
                if (k % 3 == 0) { float a = rnd() * Mathf.PI * 2f; d = band * new Vector3(Mathf.Cos(a), (rnd() - 0.5f) * 0.22f, Mathf.Sin(a)); }
                else d = new Vector3(rnd() * 2f - 1f, rnd() * 2f - 1f, rnd() * 2f - 1f);
                if (d.sqrMagnitude < 1e-4f) continue;
                d.Normalize();
                var c = Color.Lerp(new Color(0.7f, 0.82f, 1f), new Color(1f, 0.85f, 0.7f), rnd());
                c *= 0.7f + rnd() * 0.3f; c.a = 1f;
                // gut sichtbar: 2–5 Pixel bei 1080p, einzelne große Sterne
                float size = (34f + rnd() * 60f) * (rnd() < 0.04f ? 2.4f : 1f);
                Quad(verts, uvs, cols, tris, d * 16000f, d, size, c);
            }
            // Farbige Nebel (große, schwache Flächen)
            Color[] neb = { new Color(0.55f, 0.22f, 0.85f), new Color(0.15f, 0.45f, 0.95f), new Color(0.95f, 0.3f, 0.45f), new Color(1f, 0.55f, 0.2f), new Color(0.15f, 0.8f, 0.75f) };
            for (int k = 0; k < 34; k++)
            {
                var d = (band * new Vector3(Mathf.Cos(k * 0.61f), (rnd() - 0.5f) * 0.35f, Mathf.Sin(k * 0.61f))).normalized;
                var c = neb[k % neb.Length] * (0.22f + rnd() * 0.22f); c.a = 1f;
                Quad(verts, uvs, cols, tris, d * 15000f, d, 3000f + rnd() * 5200f, c);
            }
            // helle Nebelkerne (kleiner, kräftiger) für Tiefe
            for (int k = 0; k < 10; k++)
            {
                var d = (band * new Vector3(Mathf.Cos(k * 1.7f + 0.4f), (rnd() - 0.5f) * 0.25f, Mathf.Sin(k * 1.7f + 0.4f))).normalized;
                var c = Color.Lerp(neb[(k * 2) % neb.Length], Color.white, 0.25f) * (0.35f + rnd() * 0.2f); c.a = 1f;
                Quad(verts, uvs, cols, tris, d * 14800f, d, 1100f + rnd() * 1600f, c);
            }
            var m = new Mesh { name = "Sterne", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 40000f);
            var go = new GameObject("Sterne");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = TexMat(Mats.ParticleAdd, Soft(), Color.white, "Sterne");
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        /// <summary>Quadrat senkrecht zu n (zeigt zur Bühnenmitte – die Kamera ist immer nahe der Mitte).</summary>
        static void Quad(List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> t, Vector3 c, Vector3 n, float size, Color color)
        {
            var up = Mathf.Abs(n.y) > 0.95f ? Vector3.forward : Vector3.up;
            var a = Vector3.Cross(up, n).normalized * size * 0.5f;
            var b = Vector3.Cross(n, a).normalized * size * 0.5f;
            int s = v.Count;
            v.Add(c - a - b); v.Add(c - a + b); v.Add(c + a + b); v.Add(c + a - b);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
            for (int k = 0; k < 4; k++) col.Add(color);
            // beidseitig, damit die Wicklung keine Rolle spielt
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
        }

        void BuildSun(Transform root)
        {
            var p = sunDir * 12000f;
            Billboard(root, p, 1400f, Soft(), new Color(1f, 0.97f, 0.9f), "Sonnenkern");
            Billboard(root, p, 5200f, Soft(), new Color(1f, 0.62f, 0.3f) * 0.45f, "Sonnenhof");
            Billboard(root, p, 11000f, Soft(), new Color(1f, 0.5f, 0.2f) * 0.12f, "Sonnenschein");
        }

        Transform Billboard(Transform parent, Vector3 localPos, float size, Texture tex, Color c, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = TexMat(Mats.ParticleAdd, tex, c, name);
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            billboards.Add(go.transform);
            return go.transform;
        }

        static Mesh quadMesh;
        static Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "Leuchtfläche" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        Body BuildPlanet(Transform root, PlanetDef pd, Vector3 localPos, float radius, int index)
        {
            var b = new Body { Id = pd.Id, R = radius, Locked = !pd.StartPlanet };
            var t = new GameObject("Planet_" + pd.Id).transform;
            t.SetParent(root, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(12f + index * 7f, index * 40f, -8f + index * 5f);
            b.T = t;
            var sphere = MeshKit.Get("planetsel_sphere", mb => mb.Sphere(Vector3.zero, 1f, 64, 40));
            var surf = new GameObject("Oberflaeche");
            surf.transform.SetParent(t, false);
            surf.transform.localScale = Vector3.one * radius;
            surf.AddComponent<MeshFilter>().sharedMesh = sphere;
            var mr = surf.AddComponent<MeshRenderer>();
            var rim = UISkinColor(pd.SkyHorizon);
            b.RimColor = rim;
            LoadShaders();
            var surfTex = SurfaceTexture(pd, index);
            Material mat = null;
            if (planetShader != null)
            {
                try
                {
                    mat = new Material(planetShader) { name = "Planet_" + pd.Id };
                    mat.mainTexture = surfTex;
                    // gesperrte Planeten: etwas blasser und entsättigt, aber gut beleuchtet (sie sollen sichtbar bleiben)
                    mat.color = b.Locked ? new Color(0.68f, 0.7f, 0.78f) : Color.white;
                    mat.SetVector("_SunDirW", sunDir);
                    // eigenes, kräftigeres Licht für die Planeten (Held der Szene; die Oberflächentexturen sind eher dunkel)
                    mat.SetColor("_SunColor", SunLight * PlanetLight);
                    mat.SetColor("_NightColor", Color.Lerp(new Color(0.05f, 0.06f, 0.12f), rim * 0.08f, 0.35f));
                    mat.SetColor("_AtmoColor", AtmoTint(pd.Id, rim) * (b.Locked ? 0.55f : 1f));
                    mat.SetVector("_Params", new Vector4(b.Locked ? 0.6f : 1f, pd.Water ? 1f : 0.35f, 1f, b.Locked ? 0.7f : 1.15f));
                    b.SurfMat = mat;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Planeten-Shader: " + e.Message); mat = null; }
            }
            if (mat == null)
            {
                mat = new Material(Mats.Template(Mats.Opaque)) { name = "Planet_" + pd.Id };
                mat.mainTexture = surfTex;
                mat.color = b.Locked ? new Color(0.6f, 0.62f, 0.68f) : Color.white;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", pd.Water ? 0.45f : 0.12f);
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // Wolkenhülle
            var cl = new GameObject("Wolken");
            cl.transform.SetParent(t, false);
            cl.transform.localScale = Vector3.one * radius * 1.018f;
            cl.AddComponent<MeshFilter>().sharedMesh = sphere;
            var cr = cl.AddComponent<MeshRenderer>();
            var cmat = new Material(Mats.Template(Mats.Fade)) { name = "Wolken_" + pd.Id };
            cmat.mainTexture = CloudTexture();
            cmat.color = CloudTint(pd.Id, b.Locked);
            cr.sharedMaterial = cmat;
            cr.shadowCastingMode = ShadowCastingMode.Off;
            b.Clouds = cl.transform;
            // Atmosphärensaum: eigene Hülle (Dichte aus dem Sichtstrahl) oder – ohne Shader – Leuchtfläche zur Kamera
            if (atmoShader != null)
            {
                try
                {
                    const float H = 0.045f;
                    var at = new GameObject("Atmosphaere");
                    at.transform.SetParent(t, false);
                    at.transform.localScale = Vector3.one * radius * (1f + H * 6f);
                    at.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var ar = at.AddComponent<MeshRenderer>();
                    var am = new Material(atmoShader) { name = "Atmosphaere_" + pd.Id };
                    am.SetColor("_AtmoColor", AtmoTint(pd.Id, rim) * (b.Locked ? 0.45f : 1f));
                    am.SetVector("_SunDirW", sunDir);
                    am.SetColor("_SunColor", SunLight * SunIntensity);
                    var c = root.TransformPoint(localPos);
                    am.SetVector("_Center", new Vector4(c.x, c.y, c.z, radius));
                    am.SetVector("_Params", new Vector4(b.Locked ? 0.6f : 1f, H, 1f, 0.3f));
                    ar.sharedMaterial = am;
                    ar.shadowCastingMode = ShadowCastingMode.Off;
                    ar.receiveShadows = false;
                    b.Atmo = at.transform;
                    b.AtmoMat = am;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Atmosphären-Shader: " + e.Message); b.AtmoMat = null; }
            }
            if (b.AtmoMat == null)
            {
                var rimC = b.Locked ? rim * 0.35f : rim * 0.9f;
                b.Rim = Billboard(root, localPos, radius * 2.5f, RimTexture(), rimC, "Saum_" + pd.Id);
            }
            return b;
        }

        static Color UISkinColor(uint rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f); }

        void LoadShaders()
        {
            if (shadersLoaded) return;
            shadersLoaded = true;
            planetShader = Mats.CustomShader("RePlanetPlanet", "RePlanet/Planet");
            atmoShader = Mats.CustomShader("RePlanetPlanetAtmosphere", "RePlanet/PlanetAtmosphere");
        }

        /// <summary>Atmosphärenfarbe je Planet (kräftig wie im Stilvorbild: Türkis, Glutorange, Blau, Violett).</summary>
        static Color AtmoTint(string id, Color rim)
        {
            switch (id)
            {
                case "terra": return new Color(0.35f, 0.75f, 1f);
                case "pyra": return new Color(1f, 0.5f, 0.2f);
                case "pelagia": return new Color(0.3f, 0.95f, 0.95f);
                case "nivalis": return new Color(0.62f, 0.55f, 1f);
                default: return Color.Lerp(rim, new Color(0.4f, 0.7f, 1f), 0.4f);
            }
        }

        static Color CloudTint(string id, bool locked)
        {
            Color c;
            switch (id)
            {
                case "terra": c = new Color(0.78f, 0.76f, 0.7f, 0.85f); break;   // Smog
                case "pyra": c = new Color(0.6f, 0.45f, 0.38f, 0.55f); break;    // Asche
                case "pelagia": c = new Color(1f, 1f, 1f, 0.9f); break;
                case "nivalis": c = new Color(0.9f, 0.95f, 1f, 0.8f); break;
                default: c = new Color(1f, 1f, 1f, 0.7f); break;
            }
            if (locked) { c.r *= 0.5f; c.g *= 0.5f; c.b *= 0.55f; }
            return c;
        }

        // ------------------------------------------------------------------ Oberflächen (prozedural)
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1274126177 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Noise3(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = p.x - x0, fy = p.y - y0, fz = p.z - z0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float a = Mathf.Lerp(Hash(x0, y0, z0, seed), Hash(x0 + 1, y0, z0, seed), fx);
            float b = Mathf.Lerp(Hash(x0, y0 + 1, z0, seed), Hash(x0 + 1, y0 + 1, z0, seed), fx);
            float c = Mathf.Lerp(Hash(x0, y0, z0 + 1, seed), Hash(x0 + 1, y0, z0 + 1, seed), fx);
            float d = Mathf.Lerp(Hash(x0, y0 + 1, z0 + 1, seed), Hash(x0 + 1, y0 + 1, z0 + 1, seed), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        static float Fbm(Vector3 p, int seed, int oct = 5)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++) { s += Noise3(p, seed + i * 17) * amp; norm += amp; p *= 2.03f; amp *= 0.5f; }
            return s / norm;
        }

        static Vector3 SphereDir(int x, int y, int w, int h)
        {
            float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
            float ph = u * Mathf.PI * 2f, th = (1f - v) * Mathf.PI;
            return new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
        }

        static Texture2D SurfaceTexture(PlanetDef pd, int index)
        {
            const int w = 384, h = 192;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Oberflaeche_" + pd.Id, wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            var px = new Color32[w * h];
            var g1 = UISkinColor(pd.Ground); var g2 = UISkinColor(pd.Ground2); var acc = UISkinColor(pd.Accent);
            int seed = 100 + index * 31;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var d = SphereDir(x, y, w, h);
                    float n = Fbm(d * 2.2f + Vector3.one * 7f, seed);
                    float m = Fbm(d * 5.5f + Vector3.one * 3f, seed + 5, 4);
                    float lat = Mathf.Abs(d.y);
                    Color c;
                    switch (pd.Id)
                    {
                        case "terra":
                            {
                                if (n < 0.5f) c = Color.Lerp(new Color(0.07f, 0.13f, 0.16f), new Color(0.16f, 0.26f, 0.28f), n / 0.5f);
                                else
                                {
                                    c = Color.Lerp(new Color(0.42f, 0.36f, 0.26f), new Color(0.3f, 0.34f, 0.22f), m);
                                    if (m > 0.62f) c = Color.Lerp(c, new Color(0.55f, 0.53f, 0.5f), (m - 0.62f) * 2.5f); // Müllfelder / Städte
                                    c = Color.Lerp(c, new Color(0.5f, 0.42f, 0.3f), Mathf.Clamp01((n - 0.62f) * 4f));
                                }
                                if (lat > 0.84f) c = Color.Lerp(c, new Color(0.78f, 0.78f, 0.74f), Mathf.Clamp01((lat - 0.84f) * 10f));
                                break;
                            }
                        case "pyra":
                            {
                                c = Color.Lerp(new Color(0.32f, 0.12f, 0.07f), new Color(0.78f, 0.4f, 0.18f), n);
                                c = Color.Lerp(c, new Color(0.9f, 0.62f, 0.34f), Mathf.Clamp01((m - 0.55f) * 2f));
                                float crack = Mathf.Abs(n - 0.5f);
                                if (crack < 0.012f) c = Color.Lerp(new Color(1f, 0.55f, 0.15f), c, crack / 0.012f);
                                break;
                            }
                        case "pelagia":
                            {
                                c = Color.Lerp(new Color(0.03f, 0.16f, 0.34f), new Color(0.08f, 0.42f, 0.58f), Mathf.Clamp01(n * 1.3f - 0.1f));
                                if (n > 0.66f) c = Color.Lerp(new Color(0.75f, 0.7f, 0.5f), new Color(0.3f, 0.48f, 0.26f), Mathf.Clamp01((n - 0.66f) * 8f));
                                if (lat > 0.9f) c = Color.Lerp(c, Color.white, Mathf.Clamp01((lat - 0.9f) * 12f));
                                break;
                            }
                        case "nivalis":
                            {
                                c = Color.Lerp(new Color(0.62f, 0.72f, 0.84f), new Color(0.95f, 0.97f, 1f), n);
                                if (Mathf.Abs(m - 0.5f) < 0.02f) c = Color.Lerp(new Color(0.35f, 0.5f, 0.7f), c, Mathf.Abs(m - 0.5f) / 0.02f);
                                break;
                            }
                        default:
                            c = Color.Lerp(g1, g2, n);
                            c = Color.Lerp(c, acc, Mathf.Clamp01((m - 0.7f) * 2f) * 0.4f);
                            break;
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static Texture2D CloudTexture()
        {
            if (cloudTex != null) return cloudTex;
            const int w = 384, h = 192;
            cloudTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "PlanetWolken", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var d = SphereDir(x, y, w, h);
                    // Bänder entlang der Breitengrade + Rauschen
                    var q = new Vector3(d.x * 3f, d.y * 7f, d.z * 3f);
                    float n = Fbm(q + Vector3.one * 11f, 901);
                    float a = Mathf.Clamp01((n - 0.5f) / 0.2f);
                    a = a * a * (3f - 2f * a) * 0.9f;
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            cloudTex.SetPixels32(px);
            cloudTex.Apply(true);
            return cloudTex;
        }

        static Texture2D Soft()
        {
            if (soft != null) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "PlanetselSoft", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = Mathf.Pow(a, 2.2f);
                    byte v = (byte)(a * 255);
                    px[y * n + x] = new Color32(v, v, v, v);
                }
            soft.SetPixels32(px);
            soft.Apply(true);
            return soft;
        }

        /// <summary>Ring-Verlauf für den Atmosphärensaum: Maximum am Planetenrand (Radius 1/1,25 der Fläche).</summary>
        static Texture2D RimTexture()
        {
            if (rimTex != null) return rimTex;
            const int n = 256;
            rimTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "PlanetselSaum", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            const float edge = 0.8f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (r < edge) a = Mathf.Pow(r / edge, 10f) * 0.55f;           // innen nur ein dünner Rand
                    else a = Mathf.Pow(Mathf.Clamp01(1f - (r - edge) / (1f - edge)), 2.2f);
                    byte v = (byte)(Mathf.Clamp01(a) * 255);
                    px[y * n + x] = new Color32(v, v, v, v);
                }
            rimTex.SetPixels32(px);
            rimTex.Apply(true);
            return rimTex;
        }

        // ------------------------------------------------------------------ Asteroiden, Station, Schiffe
        void BuildBelt(Transform root)
        {
            var go = new GameObject("Asteroidenguertel").transform;
            go.SetParent(root, false);
            go.localPosition = new Vector3(0f, -40f, 600f);
            go.localRotation = Quaternion.Euler(4f, 0f, -6f);
            belt = go;
            var r = new System.Random(77);
            Func<float> rnd = () => (float)r.NextDouble();
            var mb = new MultiBuilder();
            var rock = Mats.Get(Mats.Opaque, new Color(0.36f, 0.33f, 0.3f));
            var rock2 = Mats.Get(Mats.Opaque, new Color(0.46f, 0.4f, 0.34f));
            for (int k = 0; k < 170; k++)
            {
                float a = rnd() * Mathf.PI * 2f;
                float rad = 1300f + (rnd() - 0.5f) * 260f;
                var p = new Vector3(Mathf.Cos(a) * rad, (rnd() - 0.5f) * 70f, Mathf.Sin(a) * rad);
                float s = 3f + rnd() * rnd() * 22f;
                mb.M = Matrix4x4.TRS(p, Quaternion.Euler(rnd() * 360f, rnd() * 360f, rnd() * 360f), Vector3.one);
                mb.For(k % 3 == 0 ? rock2 : rock).Crumple(Vector3.zero, s, 0.6f + rnd() * 0.5f, k, 0.35f, 8, 6);
            }
            mb.M = Matrix4x4.identity;
            var built = mb.Build("Brocken", go, false);
            foreach (var mr in built.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildStation(Transform root, Body near)
        {
            var st = new GameObject("Raumstation").transform;
            st.SetParent(root, false);
            st.localPosition = near.T.localPosition + new Vector3(near.R * 1.7f, near.R * 0.45f, -near.R * 0.9f);
            st.localRotation = Quaternion.Euler(18f, 30f, 0f);
            var hull = Mats.Get(Mats.Metal, new Color(0.78f, 0.8f, 0.84f));
            var dark = Mats.Get(Mats.Opaque, new Color(0.2f, 0.21f, 0.24f));
            var panel = Mats.Get(Mats.Emissive, new Color(0.1f, 0.18f, 0.35f), new Color(0.05f, 0.12f, 0.3f));
            var win = Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.55f), new Color(2.2f, 1.7f, 1f));
            beacon = Mats.Unique(Mats.Emissive, new Color(1f, 0.2f, 0.1f));
            var mb = new MultiBuilder();
            mb.For(hull).CylinderZ(Vector3.zero, 3.2f, 26f, 16);
            mb.For(dark).CylinderZ(new Vector3(0f, 0f, 14f), 2f, 3f, 12);
            mb.For(hull).Sphere(new Vector3(0f, 0f, -14f), 5f, 16, 10);
            for (int k = 0; k < 8; k++) mb.For(win).Box(new Vector3(Mathf.Cos(k * 0.785f) * 3.22f, Mathf.Sin(k * 0.785f) * 3.22f, 0f), new Vector3(0.6f, 0.6f, 14f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(dark).Box(new Vector3(s * 12f, 0f, 6f), new Vector3(18f, 0.4f, 0.4f));
                for (int k = 0; k < 3; k++) mb.For(panel).Box(new Vector3(s * (8f + k * 5.2f), 0f, 6f), new Vector3(4.8f, 0.12f, 9f));
            }
            mb.For(beacon).Sphere(new Vector3(0f, 3.6f, 13f), 0.6f, 8, 5);
            mb.Build("Rumpf", st, false);
            var ring = new GameObject("Ring").transform;
            ring.SetParent(st, false);
            ring.localPosition = new Vector3(0f, 0f, -2f);
            var rb = new MultiBuilder();
            rb.For(hull).TorusRot(Vector3.zero, new Vector3(90f, 0f, 0f), 22f, 2.4f, 48, 8);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                rb.For(dark).Beam(Vector3.zero, new Vector3(Mathf.Cos(a) * 21f, Mathf.Sin(a) * 21f, 0f), 0.8f);
                rb.For(win).Box(new Vector3(Mathf.Cos(a + 0.5f) * 22f, Mathf.Sin(a + 0.5f) * 22f, 2.35f), new Vector3(2.5f, 2.5f, 0.1f));
            }
            rb.Build("RingMesh", ring, false);
            stationRing = ring;
            st.localScale = Vector3.one * 1.6f;
        }

        void BuildShips(Transform root)
        {
            var hull = Mats.Get(Mats.Metal, new Color(0.7f, 0.72f, 0.76f));
            var dark = Mats.Get(Mats.Opaque, new Color(0.18f, 0.19f, 0.22f));
            var accent = Mats.Get(Mats.Opaque, new Color(0.95f, 0.55f, 0.18f));
            var glow = Mats.Get(Mats.Emissive, new Color(0.6f, 0.85f, 1f), new Color(2.4f, 3.2f, 4.2f));
            var mb = new MultiBuilder();
            mb.For(hull).Box(new Vector3(0f, 0f, 0f), new Vector3(3f, 2.2f, 16f));
            mb.M = Matrix4x4.TRS(new Vector3(0f, 0f, 8f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 0.7f));
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(1.6f, 0.05f), new Vector2(1.4f, 2f), new Vector2(0.6f, 3.4f), new Vector2(0f, 3.8f) }, 12);
            mb.M = Matrix4x4.identity;
            for (int k = 0; k < 3; k++)
                for (int s = -1; s <= 1; s += 2)
                    mb.For(k % 2 == 0 ? accent : dark).Box(new Vector3(s * 2.6f, -0.3f, -4f + k * 3.6f), new Vector3(2.2f, 2f, 3.2f)); // Container
            mb.For(dark).Box(new Vector3(0f, 0f, -8.6f), new Vector3(4f, 2.6f, 1.6f));
            for (int s = -1; s <= 1; s += 2) mb.For(glow).CylinderZ(new Vector3(s * 1.1f, 0f, -9.5f), 0.7f, 0.2f, 10);
            var proto = mb.Build("Frachter", root, false);
            proto.SetActive(false);
            Vector3[] a = { new Vector3(-2600f, 260f, 300f), new Vector3(2200f, -120f, 1400f), new Vector3(-400f, 420f, -900f) };
            Vector3[] b = { new Vector3(2600f, 120f, 900f), new Vector3(-2400f, 60f, 200f), new Vector3(600f, 380f, 2800f) };
            float[] sp = { 0.012f, 0.009f, 0.015f };
            float[] sc = { 1.4f, 1.1f, 0.9f };
            for (int i = 0; i < a.Length; i++)
            {
                var s = Instantiate(proto, root).transform;
                s.gameObject.SetActive(true);
                s.name = "Frachter_" + i;
                s.localScale = Vector3.one * sc[i];
                var tr = Billboard(s, new Vector3(0f, 0f, -11f), 9f, Soft(), new Color(0.55f, 0.8f, 1f), "Triebwerksglühen");
                tr.localScale = Vector3.one * 9f;
                ships.Add(s); shipA.Add(a[i]); shipB.Add(b[i]); shipSpeed.Add(sp[i]); shipPhase.Add(i * 0.37f);
            }
        }

        static Material TexMat(string template, Texture tex, Color c, string name)
        {
            var m = new Material(Mats.Template(template)) { name = name };
            m.mainTexture = tex;
            m.color = c;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c * 0.5f);
            return m;
        }
    }
}
