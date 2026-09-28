using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Baut die sichtbare Welt eines Planeten aus dem deterministischen Layout (Core/World) und hält sie mit dem
    /// replizierten Weltzustand synchron: Gelände, Gebäude, Requisiten, Stützpunkt, Lichter, Vegetation, Wasser.
    /// </summary>
    public partial class WorldView : MonoBehaviour
    {
        public static WorldView I { get; private set; }
        public string Planet { get; private set; }
        public PlanetLayout Layout { get; private set; }
        public Transform Root { get; private set; }

        /// <summary>Welt für die Darstellung: im Spiel die replizierte Welt, im Menü eine unberührte Vorschauwelt.</summary>
        public WorldState World
        {
            get
            {
                var w = GameApp.I != null ? GameApp.I.W : null;
                return w ?? menuWorld;
            }
        }
        WorldState menuWorld;

        // Umschaltbare Elemente
        class Switchable { public GameObject Off, On; public string Project; public int Area = -1; public bool State; }
        readonly List<Switchable> projectSwitches = new List<Switchable>();
        readonly Dictionary<int, Material> windowMats = new Dictionary<int, Material>();
        readonly Dictionary<int, Material> lampMats = new Dictionary<int, Material>();
        readonly List<Vector3>[] lampPositions = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
        readonly List<GameObject>[] duneSets = { new List<GameObject>(), new List<GameObject>() };
        readonly List<Transform> mounds = new List<Transform>();
        readonly Dictionary<int, GameObject> zoneLamps = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Material> zoneLampMats = new Dictionary<int, Material>();
        readonly Dictionary<string, GameObject[]> repairVisuals = new Dictionary<string, GameObject[]>();
        readonly Dictionary<string, Transform> ecoVisuals = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> loreVisuals = new Dictionary<string, GameObject>();
        readonly List<KeyValuePair<Transform, float>> trees = new List<KeyValuePair<Transform, float>>();
        readonly List<Transform> spinners = new List<Transform>();
        readonly List<ParticleSystem> fountains = new List<ParticleSystem>();
        readonly Dictionary<string, Transform> projectSites = new Dictionary<string, Transform>();
        readonly List<GameObject> builtShelters = new List<GameObject>();
        Backdrop backdrop;
        bool lastBefore;
        /// <summary>Dekorative Müllmassen des aktuellen Planeten (Statistik: Instanzen, Ring-Ecken).</summary>
        public Backdrop Backdrop { get { return backdrop; } }
        Material terrainMat, waterMat;
        Texture2D terrainTex;
        GameObject water;
        float refreshTimer;
        float[] lastClean = new float[3];
        float[] lastEco = new float[3];
        bool terrainDirty;
        System.Threading.Thread texThread;
        volatile Color32[] pendingTex;
        public const int TexSize = 512;

        /// <summary>Lichtstärke je Bereich (0..1), für „Stadt erwacht“-Übergänge animiert.</summary>
        readonly float[] areaLight = new float[3];
        readonly float[] awakenT = { -1f, -1f, -1f };

        void Awake()
        {
            I = this;
        }

        void Start()
        {
            if (GameApp.I != null)
            {
                GameApp.I.OnPlanetChanged += p => { if (GameApp.I.InGame || GameApp.I.Mode == AppMode.Loading) Build(p); };
                GameApp.I.OnSessionStarted += () => { if (GameApp.I.W != null) Build(GameApp.I.W.CurrentPlanet); };
                GameApp.I.OnSessionEnded += () => BuildMenuBackdrop();
                GameApp.I.OnFx += OnFx;
            }
            BuildMenuBackdrop();
        }

        /// <summary>Planetenwahl: zeigt den gewählten Planeten mit seiner Stimmung als Hintergrund.</summary>
        public void PreviewPlanet(string planet)
        {
            if (planet == null || !GameData.Planets.ContainsKey(planet) || GameApp.I == null || GameApp.I.W != null) return;
            if (planet == Planet) return;
            menuWorld = Game.NewWorld("Vorschau", GameData.Planets[planet].StartPlanet ? planet : "terra");
            menuWorld.CurrentPlanet = planet;
            menuWorld.PlayTime = 0.55f * GameData.Planets[planet].DayLength;
            Build(planet, true);
        }

        public void BuildMenuBackdrop()
        {
            menuWorld = Game.NewWorld("Menü", "terra");
            menuWorld.PlayTime = 0.55f * GameData.Planets["terra"].DayLength; // Abendstimmung
            Build("terra", true);
        }

        public void Build(string planet, bool force = false)
        {
            if (!force && planet == Planet && Root != null) return;
            try
            {
                Clear();
                Planet = planet;
                Layout = WorldGen.Get(planet);
                Root = new GameObject("Planet_" + planet).transform;
                Root.SetParent(transform, false);
                BuildTerrain();
                BuildWater();
                BuildBoxes();
                BuildProps();
                BuildBase();
                BuildSpots();
                BuildMounds();
                BuildSkyline();
                for (int a = 0; a < 3; a++) { lastClean[a] = -1; lastEco[a] = -1; areaLight[a] = -1; awakenT[a] = -1; }
                Refresh(true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Hud.Show("Welt konnte nicht vollständig aufgebaut werden: " + e.Message, ToastKind.Error, 8f);
            }
        }

        void Clear()
        {
            if (Root != null)
            {
                // Eigene (nicht geteilte) Meshes freigeben – sonst wächst der Speicher bei jedem Planetenwechsel
                foreach (var mf in Root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !MeshKit.IsShared(mf.sharedMesh)) Destroy(mf.sharedMesh);
                Destroy(Root.gameObject);
            }
            Root = null;
            projectSwitches.Clear(); windowMats.Clear(); lampMats.Clear();
            foreach (var l in lampPositions) l.Clear();
            duneSets[0].Clear(); duneSets[1].Clear();
            mounds.Clear(); zoneLamps.Clear(); zoneLampMats.Clear(); repairVisuals.Clear(); ecoVisuals.Clear(); loreVisuals.Clear();
            trees.Clear(); spinners.Clear(); fountains.Clear(); projectSites.Clear(); spinnerProjects.Clear(); builtShelters.Clear();
            backdrop = null;
            if (terrainTex != null) Destroy(terrainTex);
            terrainTex = null;
        }

        PlanetDef Def { get { return GameData.Planets[Planet]; } }

        // ================================================================== Gelände
        void BuildTerrain()
        {
            const float half = 170f, step = 2f;
            int n = (int)(half * 2 / step) + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = -half + x * step, wz = -half + z * step;
                    float h = Terrain.HeightAt(Planet, Mathf.Clamp(wx, -155, 155), Mathf.Clamp(wz, -155, 155));
                    if (Mathf.Abs(wx) > 152 || Mathf.Abs(wz) > 152) h += (Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) - 152) * 0.6f;
                    verts[z * n + x] = new Vector3(wx, h, wz);
                    uvs[z * n + x] = new Vector2((wx + 150f) / 300f, (wz + 150f) / 300f);
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
                }
            var mesh = new Mesh { name = "terrain_" + Planet, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Terrain");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            terrainTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            terrainMat = Mats.Unique(Mats.Opaque, Color.white);
            terrainMat.mainTexture = terrainTex;
            terrainMat.SetFloat("_Glossiness", Planet == "nivalis" ? 0.35f : 0.08f);
            mr.sharedMaterial = terrainMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            terrainTex.SetPixels32(GenerateTerrainTexture(Planet, CleanFactors(), EcoFactors()));
            terrainTex.Apply(true);
        }

        float[] CleanFactors()
        {
            var w = World; var r = new float[3];
            if (w == null || Layout == null) return r;
            var ps = w.Planet(Planet);
            for (int a = 0; a < 3; a++) r[a] = Rules.Cleanliness(ps, a);
            return r;
        }

        float[] EcoFactors()
        {
            var w = World; var r = new float[3];
            if (w == null || Layout == null) return r;
            var ps = w.Planet(Planet);
            for (int a = 0; a < 3; a++) r[a] = Rules.EcoFraction(w, ps, a) * (ps.Projects[GameData.ProjectId(Planet, a)].Done ? 1f : 0.3f);
            return r;
        }

        /// <summary>Erzeugt die Geländetextur (läuft auch im Hintergrund-Thread). Verschmutzung nimmt mit der Reinigung ab, Begrünung wächst mit der Ökologie.</summary>
        public static Color32[] GenerateTerrainTexture(string planet, float[] clean, float[] eco)
        {
            var def = GameData.Planets[planet];
            var layout = WorldGen.Get(planet);
            Color g1 = Mats.C(def.Ground), g2 = Mats.C(def.Ground2);
            Color road = planet == "pyra" ? new Color(0.45f, 0.26f, 0.19f) : planet == "nivalis" ? new Color(0.55f, 0.6f, 0.68f) : new Color(0.28f, 0.28f, 0.3f);
            Color grime = planet == "pyra" ? new Color(0.33f, 0.2f, 0.14f) : planet == "nivalis" ? new Color(0.62f, 0.66f, 0.7f) : new Color(0.36f, 0.33f, 0.28f);
            Color grass = planet == "nivalis" ? new Color(0.45f, 0.7f, 0.6f) : planet == "pyra" ? new Color(0.62f, 0.48f, 0.25f) : new Color(0.35f, 0.6f, 0.28f);
            Color sand = new Color(0.86f, 0.8f, 0.62f), seabed = new Color(0.25f, 0.42f, 0.45f);
            int N = TexSize;
            var px = new Color32[N * N];
            float water = Terrain.WaterLevel(planet);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float wx = -150f + (x + 0.5f) / N * 300f, wz = -150f + (y + 0.5f) / N * 300f;
                    float h = Terrain.HeightAt(planet, wx, wz);
                    float n1 = Noise.Fbm(wx * 0.05f, wz * 0.05f, def.Seed + 5, 3);
                    float n2 = Noise.Value(wx * 0.6f, wz * 0.6f, def.Seed + 9);
                    Color c = Color.Lerp(g1, g2, Mathf.Clamp01(n1 * 1.4f - 0.2f));
                    c *= 0.9f + n2 * 0.18f;
                    int area = PlanetLayout.AreaOf(wz);
                    float dirt = (1f - Mathf.Clamp01(clean[area])) * Mathf.Clamp01(n1 * 1.8f - 0.3f);
                    c = Color.Lerp(c, grime, dirt * 0.55f);
                    float green = Mathf.Clamp01(eco[area]) * Mathf.Clamp01(n1 * 2f - 0.6f);
                    if (planet != "pelagia" || h > 0.4f) c = Color.Lerp(c, grass, green * 0.8f);
                    // Straßen
                    float rd = RoadDist(layout, wx, wz);
                    if (rd < 0)
                    {
                        c = road * (0.92f + n2 * 0.1f);
                        c = Color.Lerp(c, grime, dirt * 0.3f);
                        if (planet == "terra" && rd > -0.3f) c = new Color(0.85f, 0.82f, 0.7f);
                    }
                    if (planet == "pelagia")
                    {
                        if (h < water + 0.25f && h > water - 1.2f) c = sand * (0.95f + n2 * 0.08f);
                        else if (h <= water - 1.2f) c = Color.Lerp(sand, seabed, Mathf.Clamp01((water - 1.2f - h) / 6f)) * (0.9f + n2 * 0.15f);
                    }
                    if (planet == "nivalis") c = Color.Lerp(c, Color.white, Mathf.Clamp01(h / 4f) * 0.4f);
                    if (layout.Base.InBase(wx, wz)) c = Color.Lerp(c, new Color(0.62f, 0.62f, 0.6f), 0.55f);
                    // Bauraster markieren
                    var b = layout.Base;
                    if (wx >= b.GridX0 && wx <= b.GridX0 + b.GridW * b.Cell && wz >= b.GridZ0 && wz <= b.GridZ0 + b.GridH * b.Cell)
                    {
                        float gx = (wx - b.GridX0) % b.Cell, gz = (wz - b.GridZ0) % b.Cell;
                        if (gx < 0.12f || gz < 0.12f) c = Color.Lerp(c, new Color(1f, 0.6f, 0.2f), 0.35f);
                    }
                    if (V3.DistXZ(new V3(wx, 0, wz), b.DropZone) < b.DropRadius && V3.DistXZ(new V3(wx, 0, wz), b.DropZone) > b.DropRadius - 0.35f) c = new Color(1f, 0.75f, 0.2f);
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            return px;
        }

        static float RoadDist(PlanetLayout l, float x, float z)
        {
            float best = 99f;
            foreach (var r in l.Roads)
            {
                float hw = r[4] * 0.5f;
                float d;
                if (Mathf.Abs(r[0] - r[2]) < 0.01f) // senkrecht
                {
                    if (z < Mathf.Min(r[1], r[3]) || z > Mathf.Max(r[1], r[3])) continue;
                    d = Mathf.Abs(x - r[0]) - hw;
                    if (d < 0 && Mathf.Abs(Mathf.Abs(x - r[0])) < 0.15f && Mathf.Repeat(z, 6f) < 3f) return -0.1f; // Mittellinie
                }
                else
                {
                    if (x < Mathf.Min(r[0], r[2]) || x > Mathf.Max(r[0], r[2])) continue;
                    d = Mathf.Abs(z - r[1]) - hw;
                    if (d < 0 && Mathf.Abs(z - r[1]) < 0.15f && Mathf.Repeat(x, 6f) < 3f) return -0.1f;
                }
                best = Mathf.Min(best, d);
            }
            return best < 0 ? -1f : best;
        }

        void RequestTerrainUpdate()
        {
            if (texThread != null && texThread.IsAlive) { terrainDirty = true; return; }
            var planet = Planet; var clean = CleanFactors(); var eco = EcoFactors();
            texThread = new System.Threading.Thread(() =>
            {
                try { pendingTex = GenerateTerrainTexture(planet, clean, eco); }
                catch (Exception) { }
            }) { IsBackground = true };
            texThread.Start();
        }

        // ================================================================== Wasser
        void BuildWater()
        {
            if (!Def.Water) return;
            water = new GameObject("Water");
            water.transform.SetParent(Root, false);
            var b = new MeshBuilder();
            b.Quad(new Vector3(-400, 0, -400), new Vector3(-400, 0, 400), new Vector3(400, 0, 400), new Vector3(400, 0, -400), Vector3.up);
            water.AddComponent<MeshFilter>().sharedMesh = b.Build("water");
            var mr = water.AddComponent<MeshRenderer>();
            waterMat = Mats.Unique(Mats.Water, new Color(0.25f, 0.45f, 0.38f, 0.72f));
            try
            {
                waterMat.SetTexture("_BumpMap", WaveNormals());
                waterMat.EnableKeyword("_NORMALMAP");
                waterMat.SetFloat("_BumpScale", 0.55f);
                waterMat.mainTextureScale = new Vector2(90f, 90f);
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Wasser-Normalmap: " + e.Message); }
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            water.transform.localPosition = new Vector3(0, Terrain.WaterLevel(Planet), 0);
        }

        static Texture2D waveTex;

        /// <summary>Kachelbare Wellen-Normalmap aus überlagerten Sinuswellen (ganzzahlige Frequenzen → nahtlos).</summary>
        static Texture2D WaveNormals()
        {
            if (waveTex != null) return waveTex;
            const int N = 256;
            var h = new float[N * N];
            var waves = new[] { new Vector3(1, 2, 0.0f), new Vector3(3, -1, 1.3f), new Vector3(-2, 5, 2.1f), new Vector3(7, 3, 0.7f), new Vector3(-6, -9, 3.3f), new Vector3(11, -4, 5.1f), new Vector3(-13, 7, 1.9f) };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N, sum = 0;
                    for (int k = 0; k < waves.Length; k++)
                    {
                        var w = waves[k];
                        float amp = 1f / (1f + k * 0.6f);
                        sum += Mathf.Sin((w.x * u + w.y * v) * Mathf.PI * 2f + w.z) * amp;
                    }
                    h[y * N + x] = sum;
                }
            waveTex = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                    float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                    var n = new Vector3(-dx * 2.2f, -dy * 2.2f, 1f).normalized;
                    // RG = Normale, A = 1 (funktioniert mit UnpackNormal für RGB- und DXT5nm-Kodierung)
                    px[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), 255, 255);
                }
            waveTex.SetPixels32(px);
            waveTex.Apply(true);
            return waveTex;
        }

        // ================================================================== Gebäude aus den Kollisionsboxen
        Material WindowMat(int area)
        {
            Material m;
            if (windowMats.TryGetValue(area, out m)) return m;
            m = Mats.Unique(Mats.Emissive, new Color(0.08f, 0.1f, 0.12f));
            m.SetFloat("_Glossiness", 0.9f);
            Mats.SetEmission(m, Color.black);
            windowMats[area] = m;
            return m;
        }

        Material LampMat(int area)
        {
            Material m;
            if (lampMats.TryGetValue(area, out m)) return m;
            m = Mats.Unique(Mats.Emissive, new Color(0.35f, 0.33f, 0.28f));
            Mats.SetEmission(m, Color.black);
            lampMats[area] = m;
            return m;
        }

        static Color Quantize(Color c)
        {
            return new Color(Mathf.Round(c.r * 24) / 24f, Mathf.Round(c.g * 24) / 24f, Mathf.Round(c.b * 24) / 24f, 1f);
        }

        void BuildDune(Box bx)
        {
            var b = new MeshBuilder();
            b.M = Matrix4x4.TRS(new Vector3(bx.Cx, Terrain.HeightAt(Planet, bx.Cx, bx.Cz) - 0.3f, bx.Cz), Quaternion.identity, new Vector3(bx.Hx * 1.3f, bx.H, bx.Hz * 1.1f));
            b.Blob(Vector3.zero, 1f, 1f, 12, 4, (int)(bx.Cx * 7 + bx.Cz), 0.15f);
            var go = new GameObject("Dune");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.Build("dune");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.Opaque, new Color(0.8f, 0.45f, 0.3f));
            duneSets[Mathf.Clamp(bx.DuneSet, 0, 1)].Add(go);
        }

        void BuildGreenhouse(Box bx)
        {
            var ruin = new MultiBuilder();
            var restored = new MultiBuilder();
            var frame = Mats.Get(Mats.Metal, new Color(0.35f, 0.38f, 0.36f));
            var glass = Mats.Get(Mats.Fade, new Color(0.7f, 0.9f, 0.95f, 0.35f));
            var plant = Mats.Get(Mats.Opaque, new Color(0.25f, 0.6f, 0.25f));
            float y0 = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            var c = new Vector3(bx.Cx, y0, bx.Cz);
            for (int i = 0; i <= 8; i++)
            {
                float x = -bx.Hx + i * bx.Hx * 2 / 8f;
                var arcM = Matrix4x4.TRS(c + new Vector3(x, 0, 0), Quaternion.identity, Vector3.one);
                ruin.M = arcM; restored.M = arcM;
                for (int k = 0; k < 8; k++)
                {
                    float a0 = k / 8f * Mathf.PI, a1 = (k + 1) / 8f * Mathf.PI;
                    var p0 = new Vector3(0, Mathf.Sin(a0) * bx.H, Mathf.Cos(a0) * bx.Hz);
                    var p1 = new Vector3(0, Mathf.Sin(a1) * bx.H, Mathf.Cos(a1) * bx.Hz);
                    var mid = (p0 + p1) * 0.5f;
                    float ang = Mathf.Atan2(p1.y - p0.y, p1.z - p0.z) * Mathf.Rad2Deg;
                    bool broken = (i * 7 + k * 3) % 5 == 0;
                    if (!broken) ruin.For(frame).BoxRot(mid, new Vector3(0.25f, 0.25f, (p1 - p0).magnitude), new Vector3(-ang, 0, 0));
                    restored.For(frame).BoxRot(mid, new Vector3(0.25f, 0.25f, (p1 - p0).magnitude), new Vector3(-ang, 0, 0));
                }
            }
            restored.M = Matrix4x4.TRS(c, Quaternion.identity, new Vector3(bx.Hx * 2, bx.H, bx.Hz));
            restored.For(glass).Lathe(Vector3.zero, new[] { new Vector2(1f, 0), new Vector2(0.92f, 0.4f), new Vector2(0.7f, 0.75f), new Vector2(0.38f, 0.95f), new Vector2(0f, 1f) }, 16);
            restored.M = Matrix4x4.identity;
            for (int i = 0; i < 14; i++)
                restored.For(plant).Sphere(c + new Vector3(-bx.Hx + 2 + (i % 7) * (bx.Hx * 2 - 4) / 6f, 1.2f, (i < 7 ? -1 : 1) * bx.Hz * 0.4f), 1.4f, 8, 6);
            var off = ruin.Build("GreenhouseRuin", Root);
            var on = restored.Build("GreenhouseRestored", Root);
            projectSwitches.Add(new Switchable { Off = off, On = on, Project = "terra_p3" });
        }

        // ================================================================== Aktualisierung
        void Update()
        {
            if (Root == null || Layout == null) return;
            float dt = Time.deltaTime;
            if (pendingTex != null && terrainTex != null)
            {
                terrainTex.SetPixels32(pendingTex);
                terrainTex.Apply(true);
                pendingTex = null;
                if (terrainDirty) { terrainDirty = false; RequestTerrainUpdate(); }
            }
            foreach (var s in spinners) if (s != null) s.Rotate(0, 0, 60f * dt, Space.Self);
            AnimateLights(dt);
            if (backdrop != null) backdrop.Draw(Camera.main, dt);
            refreshTimer -= dt;
            if (refreshTimer <= 0) { refreshTimer = 0.4f; Refresh(false); }
            if (water != null)
            {
                float t = Time.time;
                water.transform.localPosition = new Vector3(Mathf.Sin(t * 0.1f) * 0.5f, Terrain.WaterLevel(Planet) + Mathf.Sin(t * 0.6f) * 0.05f, 0);
                if (waterMat != null) waterMat.mainTextureOffset = new Vector2(t * 0.004f, t * 0.0025f);
            }
        }

        void Refresh(bool first)
        {
            var w = World;
            if (w == null) return;
            var ps = w.Planet(Planet);
            // Projekte: Lichter/Bauwerke umschalten
            foreach (var s in projectSwitches)
            {
                bool on = ps.Projects.ContainsKey(s.Project) && ps.Projects[s.Project].Done;
                if (on != s.State || first)
                {
                    s.State = on;
                    if (s.Off != null) s.Off.SetActive(!on);
                    if (s.On != null) s.On.SetActive(on);
                }
            }
            for (int a = 0; a < 3; a++)
            {
                bool done = ps.Projects[GameData.ProjectId(Planet, a)].Done;
                if (areaLight[a] < 0) areaLight[a] = done ? 1 : 0;
                if (done && awakenT[a] < 0 && areaLight[a] < 1) awakenT[a] = 0f; // Übergang starten
                float c = Rules.Cleanliness(ps, a), e = Rules.EcoFraction(w, ps, a);
                if (Mathf.Abs(c - lastClean[a]) > 0.08f || Mathf.Abs(e - lastEco[a]) > 0.08f || (first && a == 2))
                {
                    if (!first) RequestTerrainUpdate();
                    lastClean[a] = c; lastEco[a] = e;
                }
            }
            // Dünen (PYRA): aktives Set wechselt mit jedem Sturm
            int active = ps.StormCount % 2;
            for (int k = 0; k < 2; k++) foreach (var g in duneSets[k]) if (g != null && g.activeSelf != (k == active)) g.SetActive(k == active);
            // Müllberge schrumpfen mit der Reinigung ihres Bereichs
            bool before = PhotoMode.Active && PhotoMode.ShowBefore;
            // Streumüll, Wandhaufen und Dachmüll nehmen mit der Sauberkeit ab (Fotomodus-Vorher: voll)
            if (backdrop != null)
                for (int a = 0; a < 3; a++) backdrop.SetDensity(a, before ? 1f : LitterFactor(ps, a), first || before != lastBefore);
            lastBefore = before;
            for (int i = 0; i < mounds.Count && i < Layout.Mounds.Count; i++)
            {
                var m = Layout.Mounds[i];
                float s = before ? 1f : MoundScale(ps, m.Area);
                mounds[i].localScale = new Vector3(m.Radius * s, m.Height * s, m.Radius * s);
                mounds[i].gameObject.SetActive(s > 0.06f);
            }
            // Lichtpunkte
            foreach (var kv in zoneLamps)
            {
                bool clean = Rules.ZoneCleared(ps, kv.Key) && !before;
                Mats.SetEmission(zoneLampMats[kv.Key], clean ? new Color(1f, 0.8f, 0.45f) * 2.2f : Color.black);
            }
            // Reparaturpunkte
            foreach (var kv in repairVisuals)
            {
                bool rep = ps.Repaired.Contains(kv.Key) && !before;
                if (kv.Value[0] != null) kv.Value[0].SetActive(!rep);
                if (kv.Value[1] != null) kv.Value[1].SetActive(rep);
            }
            // Begrünung
            foreach (var kv in ecoVisuals)
            {
                float g = before ? 0 : Rules.EcoGrowth(w, ps, kv.Key);
                bool planted = ps.Eco.ContainsKey(kv.Key) && !before;
                kv.Value.gameObject.SetActive(planted);
                if (planted) kv.Value.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, M.Smooth(g));
            }
            // Bäume färben sich mit der Ökologie des Bereichs
            foreach (var kv in trees)
            {
                if (kv.Key == null) continue;
                int area = PlanetLayout.AreaOf(kv.Key.position.z);
                float eco = before ? 0 : Rules.EcoFraction(w, ps, area);
                kv.Key.gameObject.SetActive(eco > kv.Value);
            }
            foreach (var kv in loreVisuals) if (kv.Value != null) kv.Value.SetActive(!w.Lore.Contains(kv.Key));
            // Selbst gebaute Notunterschlüpfe
            if (builtShelters.Count != ps.Shelters.Count)
            {
                foreach (var g in builtShelters) if (g != null) Destroy(g);
                builtShelters.Clear();
                foreach (var sh in ps.Shelters) builtShelters.Add(BuildShelter(new Vector3(sh.x, sh.y, sh.z), 0f, true));
            }
            // Wasserfarbe (PELAGIA wird mit den Projekten klarer)
            if (waterMat != null)
            {
                float q = 0;
                if (ps.Projects.ContainsKey("pelagia_p1") && ps.Projects["pelagia_p1"].Done) q += 0.3f;
                if (ps.Projects.ContainsKey("pelagia_p2") && ps.Projects["pelagia_p2"].Done) q += 0.35f;
                if (ps.Projects.ContainsKey("pelagia_p3") && ps.Projects["pelagia_p3"].Done) q += 0.35f;
                if (before) q = 0;
                waterMat.color = Color.Lerp(new Color(0.3f, 0.42f, 0.33f, 0.8f), new Color(0.12f, 0.72f, 0.78f, 0.62f), q);
            }
            foreach (var ps2 in fountains)
            {
                if (ps2 == null) continue;
                var em = ps2.emission;
                bool on = ps.Projects.ContainsKey("terra_p2") && ps.Projects["terra_p2"].Done && !before;
                em.enabled = on;
            }
        }

        public static float MoundScale(PlanetState ps, int area)
        {
            return Mathf.Clamp01(1f - Rules.Cleanliness(ps, area) * 1.05f);
        }

        /// <summary>Stadtlichter: nachts und nach dem Projekt des Bereichs; „Stadt erwacht“ schaltet sie nacheinander ein.</summary>
        void AnimateLights(float dt)
        {
            var w = World;
            if (w == null) return;
            var ps = w.Planet(Planet);
            float dark = Rules.Darkness(Rules.DayPhase(w, Planet));
            bool before = PhotoMode.Active && PhotoMode.ShowBefore;
            for (int a = 0; a < 3; a++)
            {
                bool done = ps.Projects[GameData.ProjectId(Planet, a)].Done && !before;
                float target = done ? 1f : 0f;
                if (awakenT[a] >= 0) { awakenT[a] += dt; areaLight[a] = Mathf.Clamp01(awakenT[a] / 6f); if (awakenT[a] > 6f) awakenT[a] = -1f; }
                else areaLight[a] = Mathf.MoveTowards(Mathf.Max(0, areaLight[a]), target, dt);
                float lit = areaLight[a] * Mathf.Lerp(0.25f, 1f, dark);
                Material m;
                if (windowMats.TryGetValue(a, out m)) Mats.SetEmission(m, new Color(1f, 0.78f, 0.45f) * lit * 1.6f);
                if (lampMats.TryGetValue(a, out m)) Mats.SetEmission(m, new Color(1f, 0.85f, 0.55f) * areaLight[a] * Mathf.Lerp(0.4f, 3f, dark));
            }
        }

        /// <summary>Lampen in der Nähe, die gerade leuchten (für den Punktlicht-Pool der Atmosphäre).</summary>
        public void CollectLitLamps(Vector3 near, float radius, List<Vector3> result)
        {
            result.Clear();
            for (int a = 0; a < 3; a++)
            {
                if (areaLight[a] < 0.5f) continue;
                foreach (var p in lampPositions[a]) if ((p - near).sqrMagnitude < radius * radius) result.Add(p);
            }
        }

        public V3 ProjectSite(int area) { return Layout.ProjectSites[area]; }

        // ================================================================== Ereignisse
        void OnFx(JObj f)
        {
            if (f.Str("k") == "awaken" && World != null)
            {
                string pid = f.Str("project");
                ProjectDef pd;
                if (pid != null && GameData.Projects.TryGetValue(pid, out pd) && pd.Planet == Planet)
                {
                    awakenT[pd.Area] = 0f;
                    areaLight[pd.Area] = 0f;
                    RequestTerrainUpdate();
                }
            }
        }
    }
}
