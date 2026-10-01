using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Dekorative, nicht interaktive Müllmassen – streng getrennt von den sammelbaren Objekten (TrashRenderer):
    /// <list type="bullet">
    /// <item>Hintergrund-Ring außerhalb der Spielfläche (Müllberge, Würfeltürme, Ruinen, Kräne, Wracks, Müllinseln …)
    /// als zusammengefasste Meshes in Winkelsektoren (Frustum-Culling je Sektor, keine Schatten).</item>
    /// <item>Fernes Gelände unter dem Ring, damit die Silhouetten nicht in der Luft stehen.</item>
    /// <item>Streumüll-Teppich (Tausende Dosen, Flaschen, Tüten …), Müllhaufen an Hauswänden, Müll auf Dächern und
    /// treibender Plastikteppich – per GPU-Instancing in Zellen. Die Menge nimmt mit der Sauberkeit des Bereichs ab
    /// (Instanzen sind nach Schwellwert sortiert: es wird nur ein Präfix gezeichnet, ohne Neuaufbau).</item>
    /// </list>
    /// </summary>
    public class Backdrop
    {
        // ================================================================== Streumüll: Datenstrukturen
        class Kind
        {
            public Mesh Mesh; public Material Mat; public bool Heap;
        }

        class Cell
        {
            public Bounds B;
            public int Area;
            public Matrix4x4[][][] Chunks; // [kind][chunk][instance]
            public int[] Count;            // Instanzen je Art
        }

        struct Spec
        {
            public string Mesh; public Color Col; public string Tpl; public float Gloss, Scale, Weight;
            public Spec(string mesh, Color col, float weight, float scale = 1f, string tpl = Mats.Opaque, float gloss = -1f)
            { Mesh = mesh; Col = col; Weight = weight; Scale = scale; Tpl = tpl; Gloss = gloss; }
        }

        const float CellW = 75f, CellD = 100f;
        const int CellsX = 4, CellsZ = 3;

        readonly string planet;
        readonly PlanetLayout layout;
        readonly Transform root;
        readonly PlanetDef def;
        readonly List<Kind> kinds = new List<Kind>();
        readonly Cell[] cells = new Cell[CellsX * CellsZ];
        readonly float[] density = { 1f, 1f, 1f };
        readonly float[] targetDensity = { 1f, 1f, 1f };
        readonly Plane[] planes = new Plane[6];
        /// <summary>Streumüll-Grüppchen näher als dies (m, Mitte) an der Kamera werden nicht gezeichnet.</summary>
        public const float NearCull = 1.7f;
        readonly Matrix4x4[] nearBuf = new Matrix4x4[1023];
        readonly List<Box> tmpBoxes = new List<Box>();
        Dictionary<long, List<Vector2>> trashHash;

        /// <summary>Anzahl der in diesem Bild gezeichneten Streumüll-Instanzen (Statistik/Fehlersuche).</summary>
        public int LastDrawn { get; private set; }
        public int TotalInstances { get; private set; }
        public int RingVertices { get; private set; }

        /// <summary>Dachhöhe eines Gebäudes an Position x (NaN = kein begehbares Flachdach). Kommt von WorldView.</summary>
        public System.Func<Box, float, float> RoofAt;

        public Backdrop(string planet, PlanetLayout layout, Transform root)
        {
            this.planet = planet; this.layout = layout; this.root = root;
            def = GameData.Planets[planet];
        }

        public void Build()
        {
            if (planet != "pelagia") BuildFarGround();
            BuildRing();
            BuildLitter();
        }

        /// <summary>Zielmenge des Streumülls je Bereich (0 = sauber, 1 = Ausgangszustand).</summary>
        public void SetDensity(int area, float f, bool instant)
        {
            targetDensity[area] = Mathf.Clamp01(f);
            if (instant) density[area] = targetDensity[area];
        }

        // ================================================================== Hilfen
        static Material Opq(Color c, float gloss = -1f) { return Mats.Get(Mats.Opaque, Q(c), null, gloss); }
        static Material Met(Color c) { return Mats.Get(Mats.Metal, Q(c)); }
        static Material Glow(Color c, float k) { return Mats.Get(Mats.Emissive, c, c * k); }
        static Color Q(Color c) { return new Color(Mathf.Round(c.r * 32) / 32f, Mathf.Round(c.g * 32) / 32f, Mathf.Round(c.b * 32) / 32f, 1f); }

        float FarHeight(float x, float z)
        {
            if (planet == "pelagia") return 0f;
            float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float edge = Terrain.HeightAt(planet, Mathf.Clamp(x, -155, 155), Mathf.Clamp(z, -155, 155)) + (Mathf.Min(d, 170f) - 152f) * 0.6f;
            if (d <= 170f) return edge - 0.45f;
            float t = Mathf.Clamp01((d - 170f) / 380f);
            t = t * t * (3f - 2f * t);
            float hills = Noise.Fbm(x * 0.006f, z * 0.006f, def.Seed + 404, 3);
            float amp = planet == "nivalis" ? 55f : planet == "pyra" ? 45f : 26f;
            return edge + t * (10f + hills * amp) + (d - 170f) * 0.015f;
        }

        // ================================================================== Fernes Gelände
        void BuildFarGround()
        {
            float[] rings = { 167.5f, 172f, 180f, 195f, 220f, 260f, 320f, 400f, 520f, 700f };
            const int N = 128;
            var b = new MeshBuilder();
            var pts = new Vector3[rings.Length, N + 1];
            for (int r = 0; r < rings.Length; r++)
                for (int i = 0; i <= N; i++)
                {
                    float a = i / (float)N * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float k = rings[r] / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)); // Quadrat → innerer Rand passt zum Gelände
                    float round = Mathf.Clamp01((rings[r] - 200f) / 300f);      // nach außen allmählich kreisförmig
                    float dist = Mathf.Lerp(k, rings[r] * 1.15f, round);
                    float x = ca * dist, z = sa * dist;
                    pts[r, i] = new Vector3(x, FarHeight(x, z), z);
                }
            for (int r = 0; r < rings.Length - 1; r++)
                for (int i = 0; i < N; i++)
                {
                    var p0 = pts[r, i]; var p1 = pts[r, i + 1]; var q0 = pts[r + 1, i]; var q1 = pts[r + 1, i + 1];
                    b.Face(p0, q0, q1, p1, Vector3.up);
                }
            var go = new GameObject("FarGround");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.Build("farground");
            var mr = go.AddComponent<MeshRenderer>();
            var g = Mats.C(def.Ground) * 0.82f; g.a = 1;
            if (planet == "nivalis") g = new Color(0.8f, 0.86f, 0.92f);
            mr.sharedMaterial = Opq(g, planet == "nivalis" ? 0.3f : 0.05f);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ================================================================== Hintergrund-Ring
        const int Sectors = 12;
        readonly MultiBuilder[] sectors = new MultiBuilder[Sectors];

        MultiBuilder SectorAt(float x, float z)
        {
            float a = Mathf.Atan2(z, x);
            int s = Mathf.Clamp((int)((a + Mathf.PI) / (Mathf.PI * 2f) * Sectors), 0, Sectors - 1);
            return sectors[s] ?? (sectors[s] = new MultiBuilder { UsePalette = true });
        }

        Material[] junk;      // Palette der Müllmassen
        Material dark, steel, glowRed, windowDark, snow, ice;
        /// <summary>Ferne Lichtpunkte (Fenster, Laternen) – nachts hell, am Tag kaum sichtbar; Warnleuchten blinken.</summary>
        Material farLights, blinkRed;
        static readonly Color FarWarm = new Color(1f, 0.72f, 0.42f), WarnRed = new Color(1f, 0.18f, 0.1f);

        void BuildRing()
        {
            var rng = new Rng(def.Seed + 9001);
            switch (planet)
            {
                case "pyra":
                    junk = new[] { Opq(new Color(0.6f, 0.3f, 0.18f)), Opq(new Color(0.36f, 0.2f, 0.14f)), Opq(new Color(0.7f, 0.5f, 0.34f)), Opq(new Color(0.42f, 0.4f, 0.4f)), Opq(new Color(0.74f, 0.56f, 0.22f)), Opq(new Color(0.5f, 0.25f, 0.2f)) };
                    break;
                case "pelagia":
                    junk = new[] { Opq(new Color(0.88f, 0.87f, 0.82f)), Opq(new Color(0.3f, 0.55f, 0.75f)), Opq(new Color(0.9f, 0.76f, 0.3f)), Opq(new Color(0.8f, 0.32f, 0.26f)), Opq(new Color(0.35f, 0.6f, 0.45f)), Opq(new Color(0.45f, 0.28f, 0.2f)) };
                    break;
                case "nivalis":
                    junk = new[] { Opq(new Color(0.28f, 0.3f, 0.34f)), Opq(new Color(0.5f, 0.54f, 0.6f)), Opq(new Color(0.45f, 0.32f, 0.27f)), Opq(new Color(0.85f, 0.45f, 0.22f)), Opq(new Color(0.62f, 0.7f, 0.78f)), Opq(new Color(0.36f, 0.42f, 0.5f)) };
                    break;
                default:
                    junk = new[] { Opq(new Color(0.52f, 0.34f, 0.24f)), Opq(new Color(0.62f, 0.55f, 0.44f)), Opq(new Color(0.46f, 0.46f, 0.47f)), Opq(new Color(0.38f, 0.45f, 0.55f)), Opq(new Color(0.6f, 0.32f, 0.28f)), Opq(new Color(0.45f, 0.47f, 0.33f)) };
                    break;
            }
            dark = Opq(new Color(0.2f, 0.19f, 0.19f));
            steel = Met(new Color(0.5f, 0.5f, 0.52f));
            blinkRed = Mats.Unique(Mats.Emissive, WarnRed);
            Mats.SetEmission(blinkRed, WarnRed * 3f);
            glowRed = blinkRed;
            farLights = Mats.Unique(Mats.Emissive, FarWarm);
            Mats.SetEmission(farLights, FarWarm * 0.1f);
            windowDark = Opq(new Color(0.12f, 0.13f, 0.15f), 0.7f);
            snow = Opq(new Color(0.93f, 0.96f, 1f), 0.25f);
            ice = Opq(new Color(0.72f, 0.85f, 0.95f), 0.85f);

            // 1) Die Silhouetten-Punkte aus dem Layout (175–300 m) in reichem Detail
            foreach (var p in layout.Props)
            {
                if (p.Kind != "skyline" && p.Kind != "mesa" && p.Kind != "farisland" && p.Kind != "icepeak") continue;
                var at = new Vector3(p.Pos.x, FarHeight(p.Pos.x, p.Pos.z), p.Pos.z);
                var mb = SectorAt(at.x, at.z);
                switch (p.Kind)
                {
                    case "skyline":
                        if (p.Style == 9) CubeTower(mb, at, rng, 25f + p.Scale * 22f);
                        else RuinTower(mb, at, rng, 30 + p.Style * 18 + rng.Range(0f, 30f), 12f + rng.Range(0f, 6f));
                        break;
                    case "mesa": Mesa(mb, at, rng, 18f * p.Scale + 8f, 25f + p.Style * 9f); break;
                    case "farisland": GarbageIsland(mb, at, rng, 14f + p.Scale * 10f, true); break;
                    case "icepeak": IcePeak(mb, at, rng, 16f + p.Scale * 8f, 40f + p.Style * 14f); break;
                }
            }
            // 2) Eigener, dichter Ring aus Müllbergen und planetentypischen Strukturen
            var placed = new List<Vector4>();
            foreach (var p in layout.Props)
                if (p.Kind == "skyline" || p.Kind == "mesa" || p.Kind == "farisland" || p.Kind == "icepeak") placed.Add(new Vector4(p.Pos.x, 0, p.Pos.z, 12f * p.Scale));
            System.Func<float, float, float, bool> free = (x, z, r) =>
            {
                float sq = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (sq - r * 0.6f < 166f) return false;
                foreach (var q in placed) { float dx = q.x - x, dz = q.z - z; if (dx * dx + dz * dz < (q.w + r) * (q.w + r) * 0.55f) return false; }
                return true;
            };
            System.Func<float, float, float, Vector3> spot = (dMin, dMax, r) =>
            {
                for (int k = 0; k < 40; k++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f);
                    float d = Mathf.Lerp(dMin, dMax, Mathf.Pow(rng.Next(), 1.4f));
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float x = ca * d / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)) * Mathf.Lerp(1f, Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)), 0.5f);
                    float z = sa * d / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)) * Mathf.Lerp(1f, Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)), 0.5f);
                    if (!free(x, z, r)) continue;
                    placed.Add(new Vector4(x, 0, z, r));
                    return new Vector3(x, FarHeight(x, z), z);
                }
                return new Vector3(float.NaN, 0, 0);
            };
            int mountains = planet == "pelagia" ? 10 : 34;
            for (int i = 0; i < mountains; i++)
            {
                float r = rng.Range(16f, 42f);
                var at = spot(178f + r * 0.6f, 460f, r);
                if (float.IsNaN(at.x)) continue;
                JunkMountain(SectorAt(at.x, at.z), at, rng, r, r * rng.Range(0.45f, 0.9f), planet == "nivalis");
            }
            switch (planet)
            {
                case "terra":
                    for (int i = 0; i < 26; i++) { var at = spot(172f, 330f, 7f); if (!float.IsNaN(at.x)) CubeTower(SectorAt(at.x, at.z), at, rng, rng.Range(18f, 60f)); }
                    for (int i = 0; i < 14; i++) { var at = spot(200f, 420f, 12f); if (!float.IsNaN(at.x)) RuinTower(SectorAt(at.x, at.z), at, rng, rng.Range(40f, 95f), rng.Range(12f, 20f)); }
                    for (int i = 0; i < 5; i++) { var at = spot(190f, 360f, 8f); if (!float.IsNaN(at.x)) Crane(SectorAt(at.x, at.z), at, rng, rng.Range(40f, 62f), rng.Chance(0.4f)); }
                    break;
                case "pyra":
                    for (int i = 0; i < 12; i++) { var at = spot(180f, 380f, 22f); if (!float.IsNaN(at.x)) ScrapRidge(SectorAt(at.x, at.z), at, rng, rng.Range(40f, 90f), rng.Range(12f, 26f)); }
                    for (int i = 0; i < 8; i++) { var at = spot(185f, 360f, 10f); if (!float.IsNaN(at.x)) Conveyor(SectorAt(at.x, at.z), at, rng); }
                    for (int i = 0; i < 7; i++) { var at = spot(185f, 380f, 8f); if (!float.IsNaN(at.x)) Crane(SectorAt(at.x, at.z), at, rng, rng.Range(35f, 55f), true); }
                    for (int i = 0; i < 9; i++) { var at = spot(190f, 420f, 5f); if (!float.IsNaN(at.x)) Smokestack(SectorAt(at.x, at.z), at, rng, rng.Range(30f, 60f)); }
                    break;
                case "pelagia":
                    for (int i = 0; i < 22; i++) { var at = spot(172f, 380f, 18f); if (!float.IsNaN(at.x)) GarbageIsland(SectorAt(at.x, at.z), at, rng, rng.Range(10f, 30f), false); }
                    for (int i = 0; i < 9; i++) { var at = spot(185f, 400f, 30f); if (!float.IsNaN(at.x)) ShipWreck(SectorAt(at.x, at.z), at, rng, rng.Range(45f, 110f)); }
                    for (int i = 0; i < 14; i++) { var at = spot(168f, 320f, 22f); if (!float.IsNaN(at.x)) PlasticCarpet(SectorAt(at.x, at.z), at, rng, rng.Range(14f, 34f)); }
                    for (int i = 0; i < 3; i++) { var at = spot(220f, 400f, 14f); if (!float.IsNaN(at.x)) OilRig(SectorAt(at.x, at.z), at, rng); }
                    break;
                case "nivalis":
                    for (int i = 0; i < 14; i++) { var at = spot(180f, 420f, 16f); if (!float.IsNaN(at.x)) IcePeak(SectorAt(at.x, at.z), at, rng, rng.Range(12f, 26f), rng.Range(35f, 80f)); }
                    for (int i = 0; i < 11; i++) { var at = spot(176f, 360f, 6f); if (!float.IsNaN(at.x)) AntennaMast(SectorAt(at.x, at.z), at, rng, rng.Range(28f, 70f)); }
                    for (int i = 0; i < 6; i++) { var at = spot(180f, 340f, 12f); if (!float.IsNaN(at.x)) BuriedDome(SectorAt(at.x, at.z), at, rng, rng.Range(9f, 18f)); }
                    break;
            }
            BuildFarSilhouettes(rng);
            var ringGo = new GameObject("Backdrop");
            ringGo.transform.SetParent(root, false);
            int verts = 0;
            for (int s = 0; s < Sectors; s++)
            {
                if (sectors[s] == null || sectors[s].Empty) continue;
                verts += sectors[s].VertexCount;
                sectors[s].BuildCombined("Sektor" + s, ringGo.transform, false);
                sectors[s] = null;
            }
            RingVertices = verts;
        }

        Material J(Rng rng) { return junk[rng.Range(0, junk.Length)]; }

        /// <summary>
        /// Tiefenstaffelung: eine zweite, ferne Silhouettenreihe (430–620 m) aus einfachen Körpern – Hochhäuser,
        /// Schornsteine, Tafelberge, Schiffe, Eisgipfel je Planet – mit Lichtpunkten und blinkenden Warnleuchten.
        /// Wenige Ecken je Körper; der Höhennebel der Nachbearbeitung staffelt sie farbig nach hinten.
        /// </summary>
        void BuildFarSilhouettes(Rng rng)
        {
            var far1 = Opq(planet == "pyra" ? new Color(0.36f, 0.2f, 0.16f) : planet == "nivalis" ? new Color(0.62f, 0.68f, 0.78f) : planet == "pelagia" ? new Color(0.36f, 0.4f, 0.44f) : new Color(0.4f, 0.39f, 0.38f));
            var far2 = Opq(planet == "pyra" ? new Color(0.28f, 0.16f, 0.13f) : planet == "nivalis" ? new Color(0.5f, 0.56f, 0.68f) : planet == "pelagia" ? new Color(0.3f, 0.33f, 0.37f) : new Color(0.33f, 0.32f, 0.32f));
            int n = planet == "pelagia" ? 14 : 34;
            for (int i = 0; i < n; i++)
            {
                float a = (i + rng.Range(0.1f, 0.9f)) / n * Mathf.PI * 2f;
                float d = rng.Range(440f, 610f);
                var at = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                at.y = FarHeight(at.x, at.z) - 2f;
                var mb = SectorAt(at.x, at.z);
                var m = rng.Chance(0.5f) ? far1 : far2;
                var yaw = Quaternion.Euler(0, rng.Range(0f, 90f), 0);
                var old = mb.M;
                mb.M = Matrix4x4.TRS(at, yaw, Vector3.one);
                switch (planet)
                {
                    case "terra":
                        {
                            // Hochhausgruppe mit Rücksprüngen, Antennen und Lichtpunkten
                            int towers = rng.Range(1, 4);
                            for (int t = 0; t < towers; t++)
                            {
                                float w = rng.Range(14f, 32f), h = rng.Range(45f, 150f);
                                var off = new Vector3(rng.Range(-25f, 25f), 0, rng.Range(-25f, 25f));
                                mb.For(m).Box(off + new Vector3(0, h * 0.5f, 0), new Vector3(w, h, w * rng.Range(0.7f, 1.2f)));
                                if (rng.Chance(0.6f)) mb.For(m).Box(off + new Vector3(0, h + h * 0.08f, 0), new Vector3(w * 0.6f, h * 0.16f, w * 0.6f));
                                if (rng.Chance(0.5f)) { mb.For(steel).Box(off + new Vector3(0, h * 1.16f + 6f, 0), new Vector3(0.6f, 12f, 0.6f)); mb.For(blinkRed).Box(off + new Vector3(0, h * 1.16f + 12.5f, 0), Vector3.one * 1.2f); }
                                for (int k = rng.Range(4, 12); k > 0; k--)
                                    mb.For(farLights).Box(off + new Vector3(rng.Range(-w * 0.4f, w * 0.4f), rng.Range(4f, h - 4f), -w * 0.5f - 0.2f), new Vector3(1.6f, 1.2f, 0.3f));
                            }
                            break;
                        }
                    case "pyra":
                        {
                            float r = rng.Range(20f, 45f), h = rng.Range(25f, 60f);
                            mb.For(m).Cylinder(Vector3.zero, r, h, 9, true, r * 0.8f);
                            if (rng.Chance(0.4f)) { float sh = rng.Range(40f, 80f); mb.For(far2).Cylinder(new Vector3(r * 0.5f, h, 0), 2.2f, sh, 8, true, 1.7f); mb.For(blinkRed).Box(new Vector3(r * 0.5f, h + sh + 1f, 0), Vector3.one * 1.4f); }
                            for (int k = rng.Range(0, 4); k > 0; k--) mb.For(farLights).Box(new Vector3(rng.Range(-r * 0.6f, r * 0.6f), h + 0.5f, rng.Range(-r * 0.6f, r * 0.6f)), Vector3.one * 1.3f);
                            break;
                        }
                    case "pelagia":
                        {
                            // ferne Frachter/Wracks und Bohrinseln am Horizont mit Positionslichtern
                            float len = rng.Range(60f, 140f);
                            mb.For(m).Box(new Vector3(0, 3f, 0), new Vector3(len, 10f, len * 0.16f));
                            mb.For(far2).Box(new Vector3(-len * 0.35f, 13f, 0), new Vector3(len * 0.14f, 12f, len * 0.13f));
                            for (int k = rng.Range(2, 6); k > 0; k--) mb.For(far1).Box(new Vector3(rng.Range(-len * 0.3f, len * 0.4f), 9.5f, 0), new Vector3(len * 0.08f, 5f, len * 0.13f));
                            mb.For(steel).Box(new Vector3(-len * 0.35f, 24f, 0), new Vector3(0.5f, 12f, 0.5f));
                            mb.For(blinkRed).Box(new Vector3(-len * 0.35f, 30.5f, 0), Vector3.one * 1.3f);
                            for (int k = rng.Range(3, 8); k > 0; k--) mb.For(farLights).Box(new Vector3(rng.Range(-len * 0.45f, len * 0.45f), rng.Range(5f, 18f), -len * 0.08f - 0.2f), new Vector3(1.4f, 1f, 0.3f));
                            break;
                        }
                    default:
                        {
                            float r = rng.Range(25f, 55f), h = rng.Range(60f, 150f);
                            mb.For(Opq(new Color(0.78f, 0.84f, 0.92f))).Cylinder(Vector3.zero, r, h, 7, true, 0f);
                            mb.For(m).Cylinder(new Vector3(r * 0.5f, 0, r * 0.2f), r * 0.6f, h * 0.6f, 6, true, 0f);
                            if (rng.Chance(0.35f)) { mb.For(steel).Box(new Vector3(-r * 0.8f, 20f, 0), new Vector3(0.8f, 40f, 0.8f)); mb.For(blinkRed).Box(new Vector3(-r * 0.8f, 41f, 0), Vector3.one * 1.4f); }
                            break;
                        }
                }
                mb.M = old;
            }
        }

        /// <summary>Turm aus gepressten Müllwürfeln (Müll-Skyline): 2×2 Würfel je Lage, leicht versetzt und verbeult.</summary>
        void CubeTower(MultiBuilder mb, Vector3 at, Rng rng, float height)
        {
            float c = rng.Range(3.2f, 4.2f);
            int layers = Mathf.Max(3, (int)(height / c));
            int k = rng.Chance(0.3f) ? 3 : 2;
            float lean = rng.Range(-0.04f, 0.04f), leanZ = rng.Range(-0.04f, 0.04f);
            float yaw = rng.Range(0f, 90f);
            var rot = Quaternion.Euler(0, yaw, 0);
            for (int l = 0; l < layers; l++)
            {
                int kk = l > layers - 3 && k == 3 ? 2 : k;
                for (int i = 0; i < kk; i++)
                    for (int j = 0; j < kk; j++)
                    {
                        if (l == layers - 1 && rng.Chance(0.35f)) continue; // unregelmäßige Oberkante
                        var local = new Vector3((i - (kk - 1) * 0.5f) * c + rng.Range(-0.25f, 0.25f) + lean * l * c, l * c + c * 0.5f, (j - (kk - 1) * 0.5f) * c + rng.Range(-0.25f, 0.25f) + leanZ * l * c);
                        mb.For(J(rng)).BoxJ(at + rot * local, Vector3.one * c * 0.97f, new Vector3(rng.Range(-2f, 2f), yaw + rng.Range(-6f, 6f), rng.Range(-2f, 2f)), c * 0.05f, rng.Range(0, 9999));
                    }
            }
            // Einzelne heruntergefallene Würfel am Fuß
            for (int i = 0; i < 6; i++)
            {
                float a = rng.Range(0f, 6.28f), r = rng.Range(k * c * 0.8f, k * c * 1.8f);
                mb.For(J(rng)).BoxJ(at + new Vector3(Mathf.Cos(a) * r, c * 0.4f, Mathf.Sin(a) * r), Vector3.one * c * 0.95f, new Vector3(rng.Range(-20f, 20f), rng.Range(0f, 90f), rng.Range(-20f, 20f)), c * 0.06f, rng.Range(0, 9999));
            }
            if (height > 40f) mb.For(glowRed).Sphere(at + Vector3.up * (layers * c + 0.6f), 0.6f, 6, 4);
        }

        /// <summary>Hochhausruine: Fensterbänder, Pfeiler, abgebrochene Ecken (vier Viertel unterschiedlicher Höhe).</summary>
        void RuinTower(MultiBuilder mb, Vector3 at, Rng rng, float h, float w)
        {
            var wall = Opq(new Color(0.5f, 0.48f, 0.45f) * rng.Range(0.8f, 1.1f));
            var yaw = Quaternion.Euler(0, rng.Range(0f, 90f), 0);
            var old = mb.M;
            mb.M = Matrix4x4.TRS(at, yaw, Vector3.one);
            float hw = w * 0.5f;
            float common = h * rng.Range(0.55f, 0.8f);
            float[] qh = new float[4];
            for (int q = 0; q < 4; q++) qh[q] = q == 0 ? h : Mathf.Lerp(common, h, rng.Next()) - (rng.Chance(0.35f) ? rng.Range(4f, 14f) : 0f);
            for (int q = 0; q < 4; q++)
            {
                float cx = (q % 2 == 0 ? -1 : 1) * hw * 0.5f, cz = (q < 2 ? -1 : 1) * hw * 0.5f;
                float top = Mathf.Max(common * 0.8f, qh[q]);
                mb.For(wall).Box(new Vector3(cx, top * 0.5f, cz), new Vector3(hw, top, hw));
                if (top > common + 3f) // abgebrochene Geschossdecke ragt heraus
                    mb.For(dark).BoxRot(new Vector3(cx * 1.1f, top - rng.Range(0.5f, 3f), cz * 1.1f), new Vector3(hw * 0.9f, 0.4f, hw * 0.6f), new Vector3(rng.Range(-12f, 12f), rng.Range(0f, 90f), rng.Range(-12f, 12f)));
            }
            // Fensterbänder (umlaufend) und Pfeiler bis zur gemeinsamen Höhe
            for (float y = 3f; y < common - 1.5f; y += 3.2f)
                mb.For(windowDark).Box(new Vector3(0, y + 0.9f, 0), new Vector3(w + 0.06f, 1.3f, w + 0.06f));
            int cols = Mathf.Max(3, (int)(w / 3f));
            for (int i = 1; i < cols; i++)
            {
                float u = -hw + i * w / cols;
                mb.For(wall).Box(new Vector3(u, common * 0.5f, 0), new Vector3(0.6f, common, w + 0.2f));
                mb.For(wall).Box(new Vector3(0, common * 0.5f, u), new Vector3(w + 0.2f, common, 0.6f));
            }
            if (rng.Chance(0.5f)) { mb.For(steel).Cylinder(new Vector3(hw * 0.3f, h, -hw * 0.3f), 0.25f, rng.Range(5f, 10f), 5); mb.For(glowRed).Sphere(new Vector3(hw * 0.3f, h + 8f, -hw * 0.3f), 0.5f, 6, 4); }
            // vereinzelt erleuchtete Fenster (Lichtpunkte in der Nacht)
            for (int k = rng.Range(3, 11); k > 0; k--)
            {
                float y = 3f + Mathf.FloorToInt(rng.Range(0f, Mathf.Max(1f, (common - 5f) / 3.2f))) * 3.2f + 0.9f;
                int face = rng.Range(0, 4);
                float u = rng.Range(-hw + 1f, hw - 1f);
                var p = face == 0 ? new Vector3(u, y, hw + 0.08f) : face == 1 ? new Vector3(u, y, -hw - 0.08f) : face == 2 ? new Vector3(hw + 0.08f, y, u) : new Vector3(-hw - 0.08f, y, u);
                mb.For(farLights).Box(p, face < 2 ? new Vector3(1.4f, 1.0f, 0.06f) : new Vector3(0.06f, 1.0f, 1.4f));
            }
            mb.M = old;
            // Müll am Fuß
            mb.For(J(rng)).Blob(at + new Vector3(rng.Range(-3f, 3f), -0.5f, rng.Range(-3f, 3f)), w * 0.9f, w * 0.35f, 12, 3, rng.Range(0, 999), 0.3f);
        }

        /// <summary>Großer Müllberg: Haufen mit Würfeln, Autowracks, Fässern, Reifen und Rohren auf der Oberfläche.</summary>
        void JunkMountain(MultiBuilder mb, Vector3 at, Rng rng, float r, float h, bool snowy)
        {
            int seed = rng.Range(0, 99999);
            mb.For(snowy ? junk[0] : dark).Blob(at + Vector3.down * 1f, r, h, 20, 6, seed, 0.28f);
            mb.For(J(rng)).Blob(at + new Vector3(r * 0.25f, -0.5f, -r * 0.2f), r * 0.6f, h * 0.8f, 14, 4, seed + 1, 0.3f);
            if (snowy) mb.For(snow).Blob(at + Vector3.up * h * 0.35f, r * 0.72f, h * 0.68f, 16, 4, seed, 0.3f);
            int n = (int)Mathf.Clamp(r * 1.6f, 24, 70);
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = Mathf.Sqrt(rng.Next()) * 0.95f;
                float y = Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr)) * h * 0.92f - 1f;
                var p = at + new Vector3(Mathf.Cos(a) * rr * r, y, Mathf.Sin(a) * rr * r);
                var m = J(rng);
                float s = rng.Range(1.2f, 3.6f);
                var e = new Vector3(rng.Range(-40f, 40f), rng.Range(0f, 360f), rng.Range(-40f, 40f));
                switch (i % 6)
                {
                    case 0: mb.For(m).BoxJ(p, Vector3.one * s, e, s * 0.08f, i + seed); break;              // Pressballen
                    case 1: // Autowrack
                        {
                            var o = mb.M; mb.M = Matrix4x4.TRS(p, Quaternion.Euler(e), Vector3.one * s * 0.6f);
                            mb.For(m).Box(new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 4f));
                            mb.For(m).Box(new Vector3(0, 1.15f, -0.3f), new Vector3(1.5f, 0.55f, 2f));
                            mb.For(windowDark).Box(new Vector3(0, 1.15f, 0.72f), new Vector3(1.4f, 0.45f, 0.05f));
                            mb.M = o; break;
                        }
                    case 2: mb.For(i % 4 == 0 ? steel : m).Tube(p, p + Quaternion.Euler(e) * Vector3.up * s * 1.6f, s * 0.25f, 7, true); break; // Fass/Rohr
                    case 3: mb.For(dark).TorusRot(p, e, s * 0.45f, s * 0.18f, 10, 5); break;                   // Reifen
                    case 4: mb.For(m).BoxRot(p, new Vector3(s * 2f, s * 0.1f, s * 1.3f), e); break;              // Blech
                    default: mb.For(steel).Beam(p, p + Quaternion.Euler(e) * Vector3.forward * s * 2.5f, s * 0.18f); break; // Träger
                }
            }
        }

        /// <summary>Gitterkran (Baukran/Hafenkran), optional umgeknickt.</summary>
        void Crane(MultiBuilder mb, Vector3 at, Rng rng, float h, bool broken)
        {
            var col = planet == "pyra" ? Opq(new Color(0.7f, 0.42f, 0.2f)) : Opq(new Color(0.85f, 0.62f, 0.2f));
            var old = mb.M;
            float tilt = broken ? rng.Range(6f, 18f) : 0f;
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(tilt, rng.Range(0f, 360f), 0), Vector3.one);
            float w = 2.2f, seg = 4f;
            int n = (int)(h / seg);
            var c = new[] { new Vector3(-w, 0, -w), new Vector3(w, 0, -w), new Vector3(w, 0, w), new Vector3(-w, 0, w) };
            for (int k = 0; k < 4; k++) mb.For(col).Beam(c[k] * 0.5f, c[k] * 0.5f + Vector3.up * n * seg, 0.3f);
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 4; k++)
                {
                    var a = c[k] * 0.5f + Vector3.up * i * seg; var b = c[(k + 1) % 4] * 0.5f + Vector3.up * (i + 1) * seg;
                    mb.For(col).Beam(a, b, 0.16f);
                }
            float top = n * seg;
            float jib = h * rng.Range(0.6f, 0.9f);
            float jibTilt = broken ? rng.Range(-35f, -12f) : 0f;
            var jr = Quaternion.Euler(jibTilt, 0, 0);
            var j0 = new Vector3(0, top + 1f, 0);
            for (int s = 0; s < 3; s++)
            {
                var off = s == 0 ? new Vector3(-0.8f, 0, 0) : s == 1 ? new Vector3(0.8f, 0, 0) : new Vector3(0, 1.6f, 0);
                mb.For(col).Beam(j0 + off, j0 + jr * (Vector3.forward * jib) + off, 0.22f);
            }
            for (float z = 0; z < jib - 2f; z += 3f)
            {
                mb.For(col).Beam(j0 + jr * new Vector3(-0.8f, 0, z), j0 + jr * new Vector3(0, 1.6f, z + 1.5f), 0.12f);
                mb.For(col).Beam(j0 + jr * new Vector3(0.8f, 0, z), j0 + jr * new Vector3(0, 1.6f, z + 1.5f), 0.12f);
            }
            mb.For(col).Beam(j0, j0 + Vector3.back * jib * 0.3f, 0.6f);
            mb.For(dark).Box(j0 + Vector3.back * jib * 0.28f + Vector3.down * 1f, new Vector3(3f, 2.4f, 3f));
            mb.For(col).Box(j0 + new Vector3(1.8f, -1.2f, 0.5f), new Vector3(2f, 2.2f, 2.2f));
            mb.For(windowDark).Box(j0 + new Vector3(1.8f, -1.1f, 1.62f), new Vector3(1.8f, 1.2f, 0.05f));
            var tip = j0 + jr * (Vector3.forward * jib * 0.85f);
            mb.For(dark).Beam(tip, tip + Vector3.down * rng.Range(8f, 20f), 0.1f);
            mb.For(glowRed).Sphere(j0 + Vector3.up * 2.4f, 0.5f, 6, 4);
            mb.M = old;
        }

        /// <summary>Tafelberg aus Gesteinsschichten mit Schrott auf dem Plateau (PYRA).</summary>
        void Mesa(MultiBuilder mb, Vector3 at, Rng rng, float r, float h)
        {
            var strata = new[] { Opq(new Color(0.62f, 0.3f, 0.2f)), Opq(new Color(0.7f, 0.38f, 0.24f)), Opq(new Color(0.55f, 0.26f, 0.18f)), Opq(new Color(0.76f, 0.46f, 0.3f)) };
            int layers = 4 + rng.Range(0, 3);
            float y = -2f;
            for (int i = 0; i < layers; i++)
            {
                float lh = h / layers * rng.Range(0.8f, 1.2f);
                float r0 = r * Mathf.Lerp(1.25f, 0.8f, i / (float)layers), r1 = r0 * rng.Range(0.9f, 0.98f);
                mb.For(strata[i % strata.Length]).Cylinder(at + Vector3.up * y, r0, lh, 11, true, r1);
                y += lh;
            }
            for (int i = 0; i < 8; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = rng.Range(0f, r * 0.6f);
                var p = at + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                if (i % 2 == 0) mb.For(J(rng)).BoxJ(p + Vector3.up * 1.2f, Vector3.one * 2.6f, new Vector3(0, rng.Range(0f, 90f), rng.Range(-15f, 15f)), 0.2f, i);
                else mb.For(steel).Beam(p, p + new Vector3(rng.Range(-3f, 3f), rng.Range(4f, 9f), rng.Range(-3f, 3f)), 0.4f);
            }
            // Geröll am Fuß
            for (int i = 0; i < 5; i++) { float a = rng.Range(0f, 6.28f); mb.For(strata[i % 4]).Blob(at + new Vector3(Mathf.Cos(a) * r * 1.2f, -1f, Mathf.Sin(a) * r * 1.2f), r * 0.35f, r * 0.2f, 8, 3, i + (int)at.x, 0.3f); }
        }

        /// <summary>Lang gezogene Schrottschlucht-Kante mit herausragenden Trägern (PYRA).</summary>
        void ScrapRidge(MultiBuilder mb, Vector3 at, Rng rng, float len, float h)
        {
            var old = mb.M;
            float yaw = Mathf.Atan2(at.x, at.z) * Mathf.Rad2Deg + 90f + rng.Range(-20f, 20f); // grob tangential zum Ring
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), new Vector3(1f, 1f, len / (h * 2f)));
            mb.For(junk[1]).Blob(Vector3.down, h * 1.1f, h, 14, 5, rng.Range(0, 999), 0.35f);
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), Vector3.one);
            for (int i = 0; i < 18; i++)
            {
                float z = rng.Range(-len * 0.45f, len * 0.45f);
                float y = h * Mathf.Sqrt(Mathf.Max(0f, 1f - (z / (len * 0.5f)) * (z / (len * 0.5f)))) * rng.Range(0.6f, 0.95f);
                var p = new Vector3(rng.Range(-h * 0.5f, h * 0.5f), y, z);
                if (i % 3 == 0) mb.For(steel).Beam(p, p + new Vector3(rng.Range(-6f, 6f), rng.Range(3f, 10f), rng.Range(-4f, 4f)), 0.5f);
                else mb.For(J(rng)).BoxJ(p, new Vector3(rng.Range(2f, 5f), rng.Range(1.5f, 3f), rng.Range(2f, 6f)), new Vector3(rng.Range(-30f, 30f), rng.Range(0f, 90f), rng.Range(-30f, 30f)), 0.4f, i);
            }
            mb.M = old;
        }

        /// <summary>Rostige Förderanlage: schräge Gitterbrücke auf Stützen hinauf zu einem Haufen (PYRA).</summary>
        void Conveyor(MultiBuilder mb, Vector3 at, Rng rng)
        {
            var old = mb.M;
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(0, rng.Range(0f, 360f), 0), Vector3.one);
            float len = rng.Range(40f, 70f), rise = rng.Range(14f, 26f);
            var rust = junk[0];
            var a = new Vector3(0, 2f, 0); var b = new Vector3(0, 2f + rise, len);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(rust).Beam(a + new Vector3(s * 1.2f, 0, 0), b + new Vector3(s * 1.2f, 0, 0), 0.35f);
                mb.For(rust).Beam(a + new Vector3(s * 1.2f, 1.6f, 0), b + new Vector3(s * 1.2f, 1.6f, 0), 0.25f);
            }
            int n = (int)(len / 3f);
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n); var q = Vector3.Lerp(a, b, (i + 1) / (float)n);
                mb.For(rust).Beam(p + new Vector3(-1.2f, 0, 0), q + new Vector3(-1.2f, 1.6f, 0), 0.12f);
                mb.For(rust).Beam(p + new Vector3(1.2f, 0, 0), q + new Vector3(1.2f, 1.6f, 0), 0.12f);
            }
            mb.For(dark).Beam(a + Vector3.up * 0.3f, b + Vector3.up * 0.3f, 2.0f, 0.15f);
            for (int i = 1; i < 4; i++)
            {
                var p = Vector3.Lerp(a, b, i / 4f);
                mb.For(rust).Beam(new Vector3(-1.4f, -2f, p.z), new Vector3(-1.2f, p.y, p.z), 0.4f);
                mb.For(rust).Beam(new Vector3(1.4f, -2f, p.z), new Vector3(1.2f, p.y, p.z), 0.4f);
            }
            var end = mb.M.MultiplyPoint3x4(new Vector3(0, 0, len + 4f));
            mb.M = old;
            end.y = FarHeight(end.x, end.z);
            JunkMountain(mb, end, rng, rng.Range(9f, 14f), rise * rng.Range(0.8f, 1.0f), false);
        }

        /// <summary>Schornstein mit Ringen und Warnleuchte.</summary>
        void Smokestack(MultiBuilder mb, Vector3 at, Rng rng, float h)
        {
            var c = Opq(new Color(0.45f, 0.3f, 0.25f) * rng.Range(0.85f, 1.1f));
            mb.For(c).Cylinder(at + Vector3.down, 2.6f, h, 12, true, 1.8f);
            for (float y = h * 0.2f; y < h; y += h * 0.22f) mb.For(dark).Cylinder(at + Vector3.up * y, Mathf.Lerp(2.65f, 1.85f, y / h), 0.8f, 12);
            mb.For(Opq(new Color(0.85f, 0.85f, 0.82f))).Cylinder(at + Vector3.up * (h - 4f), 1.95f, 2.2f, 12);
            mb.For(glowRed).Sphere(at + Vector3.up * (h + 0.3f), 0.5f, 6, 4);
        }

        /// <summary>Schwimmende Müllinsel aus bunten Kunststoffmassen, Kisten und Wracksteilen (PELAGIA).</summary>
        void GarbageIsland(MultiBuilder mb, Vector3 at, Rng rng, float r, bool green)
        {
            int seed = rng.Range(0, 9999);
            at.y = planet == "pelagia" ? -0.6f : at.y;
            mb.For(green ? Opq(new Color(0.35f, 0.5f, 0.3f)) : junk[5]).Blob(at, r, r * 0.16f + 1.5f, 18, 4, seed, 0.35f);
            for (int i = 0; i < 7; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = rng.Range(0.1f, 0.75f) * r;
                mb.For(J(rng)).Blob(at + new Vector3(Mathf.Cos(a) * rr, 0.3f, Mathf.Sin(a) * rr), r * rng.Range(0.18f, 0.35f), rng.Range(1.2f, 3.2f), 10, 3, seed + i, 0.4f);
            }
            int n = (int)(r * 1.5f);
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = Mathf.Sqrt(rng.Next()) * r * 0.85f;
                var p = at + new Vector3(Mathf.Cos(a) * rr, (r * 0.16f + 1.5f) * (1f - rr / r) + 0.4f, Mathf.Sin(a) * rr);
                float s = rng.Range(0.8f, 2.2f);
                var e = new Vector3(rng.Range(-25f, 25f), rng.Range(0f, 360f), rng.Range(-25f, 25f));
                if (i % 4 == 0) mb.For(dark).TorusRot(p, e, s * 0.45f, s * 0.18f, 10, 4);
                else if (i % 4 == 1) mb.For(J(rng)).Tube(p, p + Quaternion.Euler(e) * Vector3.up * s * 1.2f, s * 0.3f, 7, true);
                else mb.For(J(rng)).BoxJ(p, new Vector3(s * 1.4f, s, s), e, s * 0.1f, i + seed);
            }
            if (green)
                for (int i = 0; i < 4; i++) // abgestorbene Palmen
                {
                    float a = rng.Range(0f, 6.28f), rr = rng.Range(0f, r * 0.4f);
                    var p = at + new Vector3(Mathf.Cos(a) * rr, r * 0.12f, Mathf.Sin(a) * rr);
                    mb.For(dark).Tube(p, p + new Vector3(rng.Range(-2f, 2f), rng.Range(7f, 12f), rng.Range(-2f, 2f)), 0.35f, 5);
                }
        }

        /// <summary>Flacher Plastikteppich auf dem Wasser.</summary>
        void PlasticCarpet(MultiBuilder mb, Vector3 at, Rng rng, float r)
        {
            at.y = 0.05f;
            int seed = rng.Range(0, 9999);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(0, rng.Range(0f, 360f), 0), new Vector3(1f, 1f, rng.Range(0.4f, 0.8f)));
            mb.For(junk[0]).Blob(Vector3.down * 0.25f, r, 0.45f, 18, 2, seed, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = rng.Range(0f, 0.7f) * r;
                mb.For(J(rng)).Blob(new Vector3(Mathf.Cos(a) * rr, -0.2f, Mathf.Sin(a) * rr), r * rng.Range(0.15f, 0.3f), 0.5f, 9, 2, seed + i, 0.45f);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = Mathf.Sqrt(rng.Next()) * r * 0.9f;
                mb.For(J(rng)).BoxJ(new Vector3(Mathf.Cos(a) * rr, 0.25f, Mathf.Sin(a) * rr), new Vector3(rng.Range(0.8f, 2f), 0.5f, rng.Range(0.6f, 1.4f)), new Vector3(0, rng.Range(0f, 90f), rng.Range(-10f, 10f)), 0.15f, i + seed);
            }
            mb.M = o;
        }

        /// <summary>Halb gesunkenes Schiffswrack mit Aufbauten und verrutschten Containern (PELAGIA).</summary>
        void ShipWreck(MultiBuilder mb, Vector3 at, Rng rng, float len)
        {
            var hullMat = rng.Chance(0.5f) ? junk[5] : Opq(new Color(0.22f, 0.26f, 0.3f));
            var red = Opq(new Color(0.55f, 0.2f, 0.16f));
            float beam = len * rng.Range(0.14f, 0.18f), depth = beam * 0.75f;
            float roll = rng.Range(-28f, 28f), pitch = rng.Range(-8f, 10f);
            var old = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(at.x, -depth * rng.Range(0.35f, 0.6f), at.z), Quaternion.Euler(pitch, rng.Range(0f, 360f), roll), Vector3.one);
            const int S = 10;
            var sec = new Vector3[S + 1, 5];
            for (int i = 0; i <= S; i++)
            {
                float t = i / (float)S;
                float z = (t - 0.5f) * len;
                float wBow = t > 0.75f ? Mathf.Sqrt(Mathf.Max(0.02f, 1f - (t - 0.75f) / 0.25f)) : t < 0.08f ? Mathf.Lerp(0.8f, 1f, t / 0.08f) : 1f;
                float hw = beam * 0.5f * wBow;
                float sheer = depth + (t > 0.8f ? (t - 0.8f) * depth * 1.2f : 0f);
                sec[i, 0] = new Vector3(-hw, sheer, z);
                sec[i, 1] = new Vector3(-hw * 0.95f, depth * 0.25f, z);
                sec[i, 2] = new Vector3(0, 0, z);
                sec[i, 3] = new Vector3(hw * 0.95f, depth * 0.25f, z);
                sec[i, 4] = new Vector3(hw, sheer, z);
            }
            for (int i = 0; i < S; i++)
                for (int k = 0; k < 4; k++)
                {
                    var outw = (sec[i, k] + sec[i, k + 1]) * 0.5f; outw.z = 0; outw.y -= depth * 0.6f;
                    mb.For(k == 1 || k == 2 ? red : hullMat).Face(sec[i, k], sec[i + 1, k], sec[i + 1, k + 1], sec[i, k + 1], outw);
                }
            for (int i = 0; i < S; i++) mb.For(dark).Face(sec[i, 0], sec[i, 4], sec[i + 1, 4], sec[i + 1, 0], Vector3.up);
            mb.For(hullMat).Face(sec[0, 0], sec[0, 1], sec[0, 3], sec[0, 4], Vector3.back);
            mb.For(red).TriFace(sec[0, 1], sec[0, 2], sec[0, 3], Vector3.back);
            // Aufbauten achtern
            var sup = Opq(new Color(0.85f, 0.85f, 0.8f));
            float sz = -len * 0.36f;
            mb.For(sup).Box(new Vector3(0, depth + beam * 0.35f, sz), new Vector3(beam * 0.8f, beam * 0.7f, len * 0.1f));
            mb.For(sup).Box(new Vector3(0, depth + beam * 0.85f, sz), new Vector3(beam * 0.95f, beam * 0.3f, len * 0.07f));
            mb.For(windowDark).Box(new Vector3(0, depth + beam * 0.88f, sz + len * 0.036f), new Vector3(beam * 0.9f, beam * 0.12f, 0.1f));
            mb.For(dark).Cylinder(new Vector3(0, depth + beam, sz - len * 0.04f), beam * 0.12f, beam * 0.5f, 8);
            // Container
            if (rng.Chance(0.7f))
            {
                float cw = 2.4f, cl = 6f, ch = 2.6f;
                for (float z = -len * 0.25f; z < len * 0.3f; z += cl + 0.4f)
                    for (float x = -beam * 0.4f + cw * 0.5f; x < beam * 0.4f; x += cw + 0.1f)
                    {
                        int stack = rng.Range(0, 4);
                        for (int s = 0; s < stack; s++)
                            mb.For(J(rng)).BoxRot(new Vector3(x + rng.Range(-0.2f, 0.2f), depth + ch * (s + 0.5f), z + rng.Range(-0.3f, 0.3f)), new Vector3(cw, ch, cl), new Vector3(0, rng.Range(-4f, 4f), s > 1 ? rng.Range(-8f, 8f) : 0f));
                    }
            }
            mb.M = old;
            // abgerutschte Container im Wasser
            for (int i = 0; i < 4; i++)
                mb.For(J(rng)).BoxRot(at + new Vector3(rng.Range(-len * 0.4f, len * 0.4f), -0.5f, rng.Range(-len * 0.4f, len * 0.4f)), new Vector3(2.4f, 2.6f, 6f), new Vector3(rng.Range(-25f, 25f), rng.Range(0f, 360f), rng.Range(-25f, 25f)));
        }

        /// <summary>Verlassene Bohrinsel auf Stelzen (PELAGIA).</summary>
        void OilRig(MultiBuilder mb, Vector3 at, Rng rng)
        {
            var old = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(at.x, 0, at.z), Quaternion.Euler(0, rng.Range(0f, 90f), rng.Range(-4f, 4f)), Vector3.one);
            var rust = junk[5];
            for (int i = 0; i < 4; i++) mb.For(rust).Cylinder(new Vector3(i % 2 == 0 ? -9 : 9, -6, i < 2 ? -9 : 9), 1.2f, 22f, 8);
            for (int i = 0; i < 4; i++) mb.For(rust).Beam(new Vector3(i % 2 == 0 ? -9 : 9, 2, i < 2 ? -9 : 9), new Vector3(i % 2 == 0 ? 9 : -9, 12, i < 2 ? -9 : 9), 0.4f);
            mb.For(dark).Box(new Vector3(0, 16.5f, 0), new Vector3(26, 1.2f, 26));
            mb.For(J(rng)).Box(new Vector3(-5, 20, -5), new Vector3(10, 6, 8));
            mb.For(windowDark).Box(new Vector3(-5, 21, -0.95f), new Vector3(9, 1.2f, 0.1f));
            var derrick = steel;
            for (int i = 0; i < 4; i++) mb.For(derrick).Beam(new Vector3(i % 2 == 0 ? 3 : 9, 17, i < 2 ? 2 : 8), new Vector3(6, 45, 5), 0.3f);
            for (int k = 1; k < 7; k++)
            {
                float y = 17 + k * 4f, f = 1f - k / 7.5f;
                mb.For(derrick).Box(new Vector3(6, y, 5), new Vector3(6 * f + 0.3f, 0.25f, 6 * f + 0.3f));
            }
            mb.For(glowRed).Sphere(new Vector3(6, 45.5f, 5), 0.6f, 6, 4);
            mb.For(rust).Beam(new Vector3(10, 17, -10), new Vector3(28, 24, -14), 0.6f); // Fackelausleger
            mb.M = old;
        }

        /// <summary>Zerklüftete Eisspitzen aus mehreren gekippten Kegeln (NIVALIS).</summary>
        void IcePeak(MultiBuilder mb, Vector3 at, Rng rng, float r, float h)
        {
            int n = 3 + rng.Range(0, 3);
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0f, 6.28f), rr = i == 0 ? 0 : rng.Range(r * 0.4f, r * 0.9f);
                var p = at + new Vector3(Mathf.Cos(a) * rr, -3f, Mathf.Sin(a) * rr);
                float hh = i == 0 ? h : h * rng.Range(0.35f, 0.7f);
                var o = mb.M;
                mb.M = Matrix4x4.TRS(p, Quaternion.Euler(rng.Range(-8f, 8f), rng.Range(0f, 360f), rng.Range(-8f, 8f)), Vector3.one);
                mb.For(i % 2 == 0 ? snow : ice).Cylinder(Vector3.zero, i == 0 ? r : r * rng.Range(0.4f, 0.7f), hh, 6, true, 0f);
                mb.M = o;
            }
            // Eingefrorener Schrott am Fuß
            for (int i = 0; i < 6; i++)
            {
                float a = rng.Range(0f, 6.28f);
                var p = at + new Vector3(Mathf.Cos(a) * r * 0.9f, rng.Range(0f, 3f), Mathf.Sin(a) * r * 0.9f);
                mb.For(J(rng)).BoxJ(p, new Vector3(rng.Range(2f, 5f), rng.Range(1.5f, 3f), rng.Range(2f, 4f)), new Vector3(rng.Range(-30f, 30f), rng.Range(0f, 90f), rng.Range(-30f, 30f)), 0.3f, i);
            }
        }

        /// <summary>Antennenmast (Dreiecksgitter) mit Schüsseln und Abspannseilen, teils umgeknickt (NIVALIS).</summary>
        void AntennaMast(MultiBuilder mb, Vector3 at, Rng rng, float h)
        {
            var col = rng.Chance(0.5f) ? Opq(new Color(0.85f, 0.35f, 0.25f)) : Opq(new Color(0.8f, 0.82f, 0.85f));
            bool broken = rng.Chance(0.35f);
            var old = mb.M;
            mb.M = Matrix4x4.TRS(at, Quaternion.Euler(broken ? rng.Range(5f, 14f) : 0f, rng.Range(0f, 360f), 0), Vector3.one);
            float w = 1.4f, seg = 3f;
            int n = (int)(h / seg);
            var c = new Vector3[3];
            for (int k = 0; k < 3; k++) c[k] = new Vector3(Mathf.Cos(k * 2.094f) * w, 0, Mathf.Sin(k * 2.094f) * w);
            for (int k = 0; k < 3; k++) mb.For(col).Beam(c[k], c[k] * 0.4f + Vector3.up * n * seg, 0.18f);
            for (int i = 0; i < n; i++)
            {
                float f0 = Mathf.Lerp(1f, 0.4f, i / (float)n), f1 = Mathf.Lerp(1f, 0.4f, (i + 1) / (float)n);
                for (int k = 0; k < 3; k++) mb.For(col).Beam(c[k] * f0 + Vector3.up * i * seg, c[(k + 1) % 3] * f1 + Vector3.up * (i + 1) * seg, 0.08f);
            }
            var dish = Opq(new Color(0.9f, 0.92f, 0.95f));
            for (int d = 0; d < 2; d++)
            {
                float y = h * (0.55f + d * 0.25f);
                var o = mb.M;
                mb.M = mb.M * Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(-70 + d * 20, d * 130, 0), Vector3.one);
                mb.For(dish).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(1.4f, 0.4f), new Vector2(2f, 0.9f) }, 10, true);
                mb.M = o;
            }
            mb.For(glowRed).Sphere(new Vector3(0, n * seg + 0.4f, 0), 0.45f, 6, 4);
            mb.M = old;
            for (int k = 0; k < 3; k++) // Abspannseile
            {
                float a = k * 2.094f + 0.5f;
                var g = at + new Vector3(Mathf.Cos(a) * h * 0.5f, 0, Mathf.Sin(a) * h * 0.5f);
                g.y = FarHeight(g.x, g.z);
                mb.For(dark).Beam(g, at + Vector3.up * h * 0.7f, 0.06f);
            }
            if (broken) mb.For(snow).Blob(at + Vector3.down * 0.5f, 5f, 2f, 8, 3, (int)at.x, 0.3f);
        }

        /// <summary>Halb verschüttete Forschungskuppel (NIVALIS).</summary>
        void BuriedDome(MultiBuilder mb, Vector3 at, Rng rng, float r)
        {
            mb.For(Opq(new Color(0.78f, 0.84f, 0.9f), 0.6f)).Sphere(at + Vector3.down * r * 0.35f, r, 16, 10, 0.8f);
            for (int i = 0; i < 4; i++) mb.For(dark).TorusRot(at + Vector3.down * r * 0.35f, new Vector3(90, i * 45, 0), r * 1.005f, 0.25f, 20, 4);
            mb.For(snow).Blob(at + new Vector3(r * 0.4f, -1f, -r * 0.3f), r * 1.1f, r * 0.55f, 14, 4, (int)at.z, 0.35f);
            mb.For(windowDark).Box(at + new Vector3(0, r * 0.2f, r * 0.72f), new Vector3(r * 0.4f, r * 0.25f, 0.2f));
        }

        // ================================================================== Streumüll aufbauen
        List<Spec> GroundSpecs()
        {
            var l = new List<Spec>();
            switch (planet)
            {
                case "pyra":
                    l.Add(new Spec("scrap", new Color(0.55f, 0.3f, 0.2f), 2.2f, 1.1f));
                    l.Add(new Spec("scrap", new Color(0.38f, 0.36f, 0.35f), 1.2f, 1f, Mats.Metal));
                    l.Add(new Spec("pipe", new Color(0.5f, 0.32f, 0.22f), 0.9f));
                    l.Add(new Spec("tire", new Color(0.12f, 0.12f, 0.12f), 0.45f));
                    l.Add(new Spec("can", new Color(0.62f, 0.62f, 0.64f), 0.9f, 1f, Mats.Metal));
                    l.Add(new Spec("carton", new Color(0.36f, 0.26f, 0.2f), 0.6f));
                    l.Add(new Spec("shards", new Color(0.45f, 0.3f, 0.18f), 0.6f, 1f, Mats.Opaque, 0.85f));
                    l.Add(new Spec("plank", new Color(0.42f, 0.3f, 0.2f), 0.5f));
                    l.Add(new Spec("bottle", new Color(0.45f, 0.28f, 0.12f), 0.5f, 1f, Mats.Opaque, 0.85f));
                    break;
                case "pelagia":
                    l.Add(new Spec("pbottle", new Color(0.55f, 0.78f, 0.92f), 1.8f, 1f, Mats.Opaque, 0.7f));
                    l.Add(new Spec("pbottle", new Color(0.9f, 0.9f, 0.88f), 0.9f, 1f, Mats.Opaque, 0.6f));
                    l.Add(new Spec("bag", new Color(0.92f, 0.92f, 0.88f), 1.0f));
                    l.Add(new Spec("float", new Color(0.95f, 0.5f, 0.15f), 0.7f));
                    l.Add(new Spec("rope", new Color(0.3f, 0.55f, 0.72f), 0.8f));
                    l.Add(new Spec("crate", new Color(0.3f, 0.5f, 0.75f), 0.35f));
                    l.Add(new Spec("can", new Color(0.8f, 0.25f, 0.2f), 0.6f, 1f, Mats.Metal));
                    l.Add(new Spec("sandal", new Color(0.95f, 0.8f, 0.2f), 0.5f));
                    l.Add(new Spec("shards", new Color(0.4f, 0.65f, 0.55f), 0.3f, 1f, Mats.Opaque, 0.85f));
                    break;
                case "nivalis":
                    l.Add(new Spec("board", new Color(0.18f, 0.5f, 0.32f), 1.0f));
                    l.Add(new Spec("cable", new Color(0.2f, 0.2f, 0.22f), 0.9f));
                    l.Add(new Spec("scrap", new Color(0.45f, 0.48f, 0.52f), 1.1f, 1f, Mats.Metal));
                    l.Add(new Spec("can", new Color(0.3f, 0.45f, 0.75f), 0.6f, 1f, Mats.Metal));
                    l.Add(new Spec("bag", new Color(0.25f, 0.27f, 0.3f), 0.6f));
                    l.Add(new Spec("carton", new Color(0.7f, 0.62f, 0.48f), 0.5f));
                    l.Add(new Spec("paper", new Color(0.9f, 0.9f, 0.92f), 0.8f));
                    l.Add(new Spec("snowjunk", new Color(0.92f, 0.95f, 0.98f), 0.8f, 1.2f));
                    break;
                default:
                    l.Add(new Spec("can", new Color(0.78f, 0.2f, 0.18f), 1.0f, 1f, Mats.Metal));
                    l.Add(new Spec("can", new Color(0.2f, 0.42f, 0.75f), 0.8f, 1f, Mats.Metal));
                    l.Add(new Spec("canflat", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f, Mats.Metal));
                    l.Add(new Spec("bottle", new Color(0.3f, 0.55f, 0.35f), 0.7f, 1f, Mats.Opaque, 0.85f));
                    l.Add(new Spec("pbottle", new Color(0.6f, 0.8f, 0.92f), 0.9f, 1f, Mats.Opaque, 0.7f));
                    l.Add(new Spec("bag", new Color(0.92f, 0.92f, 0.88f), 1.1f));
                    l.Add(new Spec("bag", new Color(0.16f, 0.17f, 0.18f), 0.7f));
                    l.Add(new Spec("carton", new Color(0.7f, 0.55f, 0.36f), 1.0f));
                    l.Add(new Spec("paper", new Color(0.88f, 0.86f, 0.8f), 1.4f));
                    l.Add(new Spec("cup", new Color(0.92f, 0.9f, 0.85f), 0.6f));
                    l.Add(new Spec("tire", new Color(0.12f, 0.12f, 0.12f), 0.25f));
                    l.Add(new Spec("shards", new Color(0.55f, 0.75f, 0.65f), 0.5f, 1f, Mats.Opaque, 0.85f));
                    l.Add(new Spec("plank", new Color(0.45f, 0.34f, 0.24f), 0.3f));
                    break;
            }
            return l;
        }

        List<Spec> HeapSpecs()
        {
            var l = new List<Spec>();
            switch (planet)
            {
                case "pyra":
                    l.Add(new Spec("heapscrap", new Color(0.5f, 0.28f, 0.18f), 1f, 1f));
                    l.Add(new Spec("heapscrap", new Color(0.36f, 0.34f, 0.33f), 0.7f, 1f, Mats.Metal));
                    l.Add(new Spec("tirestack", new Color(0.12f, 0.12f, 0.12f), 0.4f));
                    break;
                case "pelagia":
                    l.Add(new Spec("heapnet", new Color(0.25f, 0.5f, 0.66f), 1f));
                    l.Add(new Spec("heapbags", new Color(0.88f, 0.88f, 0.84f), 0.7f));
                    l.Add(new Spec("cratestack", new Color(0.3f, 0.5f, 0.75f), 0.4f));
                    l.Add(new Spec("cratestack", new Color(0.9f, 0.55f, 0.2f), 0.3f));
                    break;
                case "nivalis":
                    l.Add(new Spec("heapsnow", new Color(0.9f, 0.94f, 0.98f), 1f));
                    l.Add(new Spec("heapscrap", new Color(0.4f, 0.43f, 0.48f), 0.7f, 1f, Mats.Metal));
                    l.Add(new Spec("heapbags", new Color(0.2f, 0.22f, 0.25f), 0.5f));
                    break;
                default:
                    l.Add(new Spec("heapbags", new Color(0.14f, 0.15f, 0.16f), 1f));
                    l.Add(new Spec("heapbags", new Color(0.3f, 0.42f, 0.3f), 0.5f));
                    l.Add(new Spec("heapbags", new Color(0.35f, 0.5f, 0.7f), 0.35f));
                    l.Add(new Spec("heapcarton", new Color(0.7f, 0.55f, 0.36f), 0.8f));
                    l.Add(new Spec("tirestack", new Color(0.12f, 0.12f, 0.12f), 0.3f));
                    break;
            }
            return l;
        }

        List<Spec> FloatSpecs()
        {
            return new List<Spec>
            {
                new Spec("pbottle", new Color(0.55f, 0.78f, 0.92f), 2f, 1.1f, Mats.Opaque, 0.7f),
                new Spec("pbottle", new Color(0.92f, 0.92f, 0.9f), 1f, 1.1f, Mats.Opaque, 0.6f),
                new Spec("bag", new Color(0.92f, 0.92f, 0.88f), 1.2f, 1.2f),
                new Spec("float", new Color(0.95f, 0.5f, 0.15f), 0.6f, 1.2f),
                new Spec("foam", new Color(0.95f, 0.95f, 0.92f), 0.9f),
                new Spec("crate", new Color(0.3f, 0.5f, 0.75f), 0.25f),
                new Spec("bag", new Color(0.3f, 0.55f, 0.4f), 0.5f, 1.2f),
            };
        }

        static Mesh LitterMesh(string name)
        {
            return MeshKit.Get("litter_" + name, b => LitterGeom(b, name));
        }

        /// <summary>
        /// Müll-Grüppchen: mehrere verschiedene Kleinteile (Dosen, Flaschen, Tüten …) in einem Mesh, jedes mit seiner
        /// Farbe über die Farbpalette – ein Draw-Call zeichnet so viele Teile unterschiedlicher Art und Farbe.
        /// </summary>
        int AddClusterKind(List<Spec> specs, float sum, int variant, int pieces, float radius, float tilt)
        {
            var dust = new Color(0.44f, 0.41f, 0.37f);
            var mesh = MeshKit.Get("litterclu_" + planet + "_" + variant + "_" + pieces + "_" + radius, b =>
            {
                var r = new Rng(def.Seed + 7100 + variant * 31 + pieces);
                for (int k = 0; k < pieces; k++)
                {
                    float x = r.Next() * sum; int si = specs.Count - 1;
                    for (int i = 0; i < specs.Count; i++) { x -= specs[i].Weight; if (x <= 0) { si = i; break; } }
                    var sp = specs[si];
                    var col = Color.Lerp(sp.Col, dust, 0.35f) * 0.88f;
                    Material target; Vector2 uv;
                    if (!Palette.Route(Opq(col), out target, out uv)) uv = Vector2.zero;
                    b.FixedUV = uv;
                    float a = r.Range(0f, 6.283f), rr = k == 0 ? 0f : Mathf.Sqrt(r.Next()) * radius;
                    b.M = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr), Quaternion.Euler(r.Range(-tilt, tilt), r.Range(0f, 360f), r.Range(-tilt, tilt)), Vector3.one * sp.Scale * r.Range(0.8f, 1.3f));
                    LitterGeom(b, sp.Mesh);
                }
                b.FixedUV = null; b.M = Matrix4x4.identity;
            });
            kinds.Add(new Kind { Mesh = mesh, Mat = Palette.Matte, Heap = false });
            return kinds.Count - 1;
        }

        static void LitterGeom(MeshBuilder b, string name)
        {
            {
                switch (name)
                {
                    case "can":
                        {
                            // liegende Getränkedose: Mantel mit eingezogenen Enden, Bördelrand, Deckel mit Lasche, leichte Delle
                            b.CylinderX(new Vector3(0, 0.065f, 0), 0.064f, 0.17f, 12);
                            b.CylinderX(new Vector3(-0.1f, 0.065f, 0), 0.056f, 0.02f, 12);
                            b.CylinderX(new Vector3(0.1f, 0.065f, 0), 0.056f, 0.02f, 12);
                            b.TorusRot(new Vector3(0.112f, 0.065f, 0), new Vector3(0, 0, 90), 0.054f, 0.006f, 12, 3);
                            b.BoxRot(new Vector3(0.113f, 0.09f, 0), new Vector3(0.006f, 0.025f, 0.018f), new Vector3(0, 0, 0));
                            b.BoxJ(new Vector3(-0.02f, 0.12f, 0.03f), new Vector3(0.07f, 0.012f, 0.04f), new Vector3(10, 0, 0), 0.01f, 5);
                            break;
                        }
                    case "canflat": b.BoxJ(new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.05f, 0.2f), new Vector3(0, 0, 8), 0.02f, 3); b.Cylinder(new Vector3(0, 0.0f, 0.1f), 0.06f, 0.02f, 6); break;
                    case "bottle":
                        {
                            var o = b.M; b.M = o * Matrix4x4.TRS(new Vector3(-0.15f, 0.08f, 0), Quaternion.Euler(0, 0, -88), Vector3.one);
                            b.Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.08f, 0), new Vector2(0.085f, 0.2f), new Vector2(0.03f, 0.3f), new Vector2(0.028f, 0.38f), new Vector2(0, 0.39f) }, 6);
                            b.M = o; break;
                        }
                    case "pbottle":
                        {
                            var o = b.M; b.M = o * Matrix4x4.TRS(new Vector3(-0.17f, 0.07f, 0), Quaternion.Euler(8, 0, -86), new Vector3(1f, 1f, 0.7f));
                            b.Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.09f, 0.01f), new Vector2(0.095f, 0.22f), new Vector2(0.035f, 0.32f), new Vector2(0.03f, 0.37f), new Vector2(0, 0.38f) }, 6);
                            b.M = o; break;
                        }
                    case "bag": b.Crumple(new Vector3(0, 0.1f, 0), 0.2f, 0.5f, 7, 0.3f, 6, 4); b.Crumple(new Vector3(0.12f, 0.2f, 0.04f), 0.06f, 1f, 8, 0.3f, 4, 3); break;
                    case "carton": b.BoxJ(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.1f, 0.38f), new Vector3(0, 0, 3), 0.04f, 5); b.BoxRot(new Vector3(0.3f, 0.02f, 0.05f), new Vector3(0.18f, 0.015f, 0.36f), new Vector3(0, 0, -12)); break;
                    case "paper": b.BoxJ(new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.012f, 0.22f), Vector3.zero, 0.02f, 7); b.BoxJ(new Vector3(0.18f, 0.03f, 0.1f), new Vector3(0.12f, 0.06f, 0.1f), new Vector3(20, 30, 10), 0.03f, 8); break;
                    case "cup": b.Lathe(new Vector3(-0.06f, 0.05f, 0), new[] { new Vector2(0.035f, 0), new Vector2(0.05f, 0.14f), new Vector2(0.052f, 0.15f) }, 7, true); break;
                    case "tire": b.TorusRot(new Vector3(0, 0.12f, 0), new Vector3(0, 0, 8), 0.3f, 0.11f, 10, 5); break;
                    case "shards": for (int i = 0; i < 4; i++) b.BoxJ(new Vector3((i - 1.5f) * 0.08f, 0.012f, (i % 2) * 0.07f), new Vector3(0.09f, 0.012f, 0.06f), new Vector3(0, i * 50, 0), 0.025f, i); break;
                    case "plank": b.BoxJ(new Vector3(0, 0.03f, 0), new Vector3(0.15f, 0.04f, 1.1f), new Vector3(0, 0, 4), 0.02f, 9); b.BoxRot(new Vector3(0.1f, 0.06f, 0.3f), new Vector3(0.12f, 0.03f, 0.6f), new Vector3(0, 40, 0)); break;
                    case "scrap": b.BoxJ(new Vector3(0, 0.05f, 0), new Vector3(0.35f, 0.08f, 0.25f), new Vector3(0, 0, 10), 0.05f, 4); b.Beam(new Vector3(-0.2f, 0.02f, 0.1f), new Vector3(0.25f, 0.1f, 0.3f), 0.03f); break;
                    case "pipe": b.Tube(new Vector3(-0.35f, 0.06f, 0), new Vector3(0.35f, 0.06f, 0.08f), 0.06f, 6, true); break;
                    case "float": b.Crumple(new Vector3(0, 0.1f, 0), 0.11f, 0.9f, 3, 0.05f, 7, 5); break;
                    case "rope": b.Tube(new Vector3(-0.3f, 0.02f, 0), new Vector3(0, 0.03f, 0.2f), 0.025f, 4); b.Tube(new Vector3(0, 0.03f, 0.2f), new Vector3(0.25f, 0.02f, -0.1f), 0.025f, 4); b.Tube(new Vector3(0.25f, 0.02f, -0.1f), new Vector3(0.05f, 0.02f, -0.25f), 0.025f, 4); break;
                    case "crate":
                        b.BoxNoBottom(new Vector3(0, 0.14f, 0), new Vector3(0.6f, 0.28f, 0.4f));
                        for (int i = 0; i < 3; i++) b.Box(new Vector3(-0.2f + i * 0.2f, 0.14f, 0.205f), new Vector3(0.1f, 0.12f, 0.01f));
                        break;
                    case "sandal": b.BoxJ(new Vector3(0, 0.015f, 0), new Vector3(0.1f, 0.03f, 0.26f), Vector3.zero, 0.01f, 3); b.TorusRot(new Vector3(0, 0.03f, 0.06f), new Vector3(0, 0, 90), 0.04f, 0.008f, 6, 3); break;
                    case "board": b.Box(new Vector3(0, 0.012f, 0), new Vector3(0.22f, 0.015f, 0.16f)); b.Box(new Vector3(0.03f, 0.028f, 0), new Vector3(0.06f, 0.02f, 0.06f)); break;
                    case "cable": b.TorusRot(new Vector3(0, 0.02f, 0), Vector3.zero, 0.15f, 0.018f, 10, 3); b.Tube(new Vector3(0.15f, 0.02f, 0), new Vector3(0.45f, 0.02f, 0.12f), 0.018f, 3); break;
                    case "snowjunk": b.Crumple(new Vector3(0, 0, 0), 0.35f, 0.4f, 9, 0.3f, 7, 4); b.BoxJ(new Vector3(0.15f, 0.12f, 0.05f), new Vector3(0.2f, 0.1f, 0.14f), new Vector3(20, 30, 0), 0.03f, 2); break;
                    case "foam": b.BoxJ(new Vector3(0, 0.03f, 0), new Vector3(0.4f, 0.06f, 0.3f), new Vector3(0, 0, 0), 0.05f, 5); break;
                    // Haufen (an Wänden, auf Dächern)
                    case "heapbags":
                        b.Crumple(new Vector3(0, 0.2f, 0), 0.45f, 0.75f, 1, 0.22f, 8, 5);
                        b.Crumple(new Vector3(0.55f, 0.18f, 0.15f), 0.38f, 0.8f, 2, 0.22f, 8, 5);
                        b.Crumple(new Vector3(-0.5f, 0.16f, 0.2f), 0.36f, 0.8f, 3, 0.22f, 8, 5);
                        b.Crumple(new Vector3(0.2f, 0.52f, 0.05f), 0.32f, 0.8f, 4, 0.22f, 8, 5);
                        b.Crumple(new Vector3(-0.2f, 0.12f, 0.55f), 0.3f, 0.7f, 5, 0.22f, 7, 4);
                        b.Crumple(new Vector3(0.45f, 0.1f, 0.6f), 0.22f, 0.7f, 6, 0.25f, 6, 4);
                        break;
                    case "heapcarton":
                        b.BoxJ(new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.5f, 0.55f), new Vector3(0, 10, 0), 0.04f, 1);
                        b.BoxJ(new Vector3(0.1f, 0.68f, 0.05f), new Vector3(0.55f, 0.36f, 0.45f), new Vector3(0, -15, 6), 0.04f, 2);
                        b.BoxJ(new Vector3(0.65f, 0.2f, 0.2f), new Vector3(0.45f, 0.4f, 0.4f), new Vector3(0, 30, 0), 0.04f, 3);
                        b.BoxJ(new Vector3(-0.6f, 0.06f, 0.3f), new Vector3(0.7f, 0.1f, 0.5f), new Vector3(0, 25, 4), 0.04f, 4);
                        b.BoxRot(new Vector3(-0.3f, 0.3f, -0.1f), new Vector3(0.05f, 0.6f, 0.5f), new Vector3(0, 5, 18));
                        break;
                    case "heapscrap":
                        b.Blob(Vector3.zero, 0.9f, 0.45f, 8, 3, 3, 0.35f);
                        b.BoxJ(new Vector3(0.2f, 0.4f, 0.1f), new Vector3(0.5f, 0.3f, 0.4f), new Vector3(20, 30, 10), 0.06f, 5);
                        b.Beam(new Vector3(-0.8f, 0.1f, -0.2f), new Vector3(0.5f, 0.7f, 0.3f), 0.07f);
                        b.Tube(new Vector3(-0.3f, 0.3f, 0.5f), new Vector3(0.6f, 0.2f, 0.6f), 0.1f, 6, true);
                        b.BoxRot(new Vector3(-0.4f, 0.35f, 0.1f), new Vector3(0.6f, 0.04f, 0.5f), new Vector3(30, 20, 15));
                        break;
                    case "heapnet":
                        b.Crumple(new Vector3(0, 0, 0), 0.9f, 0.45f, 11, 0.3f, 9, 5);
                        for (int i = 0; i < 5; i++) b.Tube(new Vector3(-0.8f + i * 0.4f, 0.05f, -0.8f), new Vector3(-0.6f + i * 0.3f, 0.4f, 0.1f), 0.03f, 4);
                        for (int i = 0; i < 4; i++) b.Tube(new Vector3(-0.8f, 0.05f, -0.6f + i * 0.4f), new Vector3(0.1f, 0.4f, -0.4f + i * 0.3f), 0.03f, 4);
                        break;
                    case "heapsnow":
                        b.Crumple(new Vector3(0, 0, 0), 0.9f, 0.5f, 13, 0.25f, 9, 5);
                        b.BoxJ(new Vector3(0.3f, 0.35f, 0.1f), new Vector3(0.4f, 0.3f, 0.35f), new Vector3(10, 30, 20), 0.05f, 6);
                        b.Beam(new Vector3(-0.6f, 0.2f, 0.2f), new Vector3(-0.1f, 0.7f, -0.2f), 0.06f);
                        break;
                    case "tirestack":
                        for (int i = 0; i < 4; i++) b.TorusRot(new Vector3(i == 3 ? 0.6f : 0.03f * i, i == 3 ? 0.12f : 0.12f + i * 0.22f, i == 3 ? 0.4f : 0), new Vector3(i == 3 ? 20 : 0, 0, i == 3 ? 70 : 0), 0.32f, 0.12f, 10, 5);
                        break;
                    case "cratestack":
                        b.BoxNoBottom(new Vector3(0, 0.15f, 0), new Vector3(0.6f, 0.3f, 0.4f));
                        b.BoxNoBottom(new Vector3(0.02f, 0.45f, 0.01f), new Vector3(0.6f, 0.3f, 0.4f));
                        b.BoxRot(new Vector3(0.55f, 0.18f, 0.2f), new Vector3(0.6f, 0.3f, 0.4f), new Vector3(0, 35, 12));
                        break;
                    default: b.Box(new Vector3(0, 0.1f, 0), Vector3.one * 0.2f); break;
                }
            }
        }

        int AddKind(Spec s, bool heap)
        {
            // Deko-Müll ist staubiger und matter als sammelbare Objekte, damit diese hervorstechen
            var dust = new Color(0.44f, 0.41f, 0.37f);
            var col = Color.Lerp(s.Col, dust, heap ? 0.2f : 0.35f) * (heap ? 0.95f : 0.88f);
            var m = s.Tpl == Mats.Metal ? Met(col) : Opq(col, s.Gloss);
            kinds.Add(new Kind { Mesh = LitterMesh(s.Mesh), Mat = m, Heap = heap });
            return kinds.Count - 1;
        }

        static int CellIndex(float x, float z)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((x + 150f) / CellW), 0, CellsX - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((z + 150f) / CellD), 0, CellsZ - 1);
            return cz * CellsX + cx;
        }

        /// <summary>Wie weit liegt der Punkt in einer Straße? &gt; 0 = auf der Fahrbahn (Meter bis zur Kante).</summary>
        float RoadInset(float x, float z)
        {
            float best = -99f;
            foreach (var r in layout.Roads)
            {
                float hw = r[4] * 0.5f, inset;
                if (Mathf.Abs(r[0] - r[2]) < 0.01f)
                {
                    if (z < Mathf.Min(r[1], r[3]) || z > Mathf.Max(r[1], r[3])) continue;
                    inset = hw - Mathf.Abs(x - r[0]);
                }
                else
                {
                    if (x < Mathf.Min(r[0], r[2]) || x > Mathf.Max(r[0], r[2])) continue;
                    inset = hw - Mathf.Abs(z - r[1]);
                }
                if (inset > best) best = inset;
            }
            return best;
        }

        bool Blocked(float x, float z, float margin)
        {
            layout.Query(x, z, margin + 1f, tmpBoxes);
            foreach (var b in tmpBoxes)
                if (b.Solid && b.Gate < 0 && b.DuneSet < 0 && b.Contains(x, z, margin)) return true;
            return false;
        }

        bool NearTrash(float x, float z, float r)
        {
            int gx = Mathf.FloorToInt(x / 4f), gz = Mathf.FloorToInt(z / 4f);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    List<Vector2> l;
                    if (!trashHash.TryGetValue(((long)(gx + i) << 32) ^ (uint)(gz + j), out l)) continue;
                    foreach (var p in l) if ((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z) < r * r) return true;
                }
            return false;
        }

        /// <summary>Gemeinsame Sperrflächen: Stützpunkt, Projektplätze, Tore, Pflanz-/Reparaturstellen, Unterschlüpfe.</summary>
        bool KeepOut(float x, float z)
        {
            if (Mathf.Abs(x) > 147f || Mathf.Abs(z) > 147f) return true;
            if (layout.Base.InBase(x, z) || (Mathf.Abs(x) < 40f && z < -88f)) return true;
            if (Mathf.Abs(x) < 15f && (Mathf.Abs(z + 50f) < 9f || Mathf.Abs(z - 50f) < 9f)) return true;
            foreach (var p in layout.ProjectSites) if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < 13f * 13f) return true;
            foreach (var s in layout.Eco) if ((s.Pos.x - x) * (s.Pos.x - x) + (s.Pos.z - z) * (s.Pos.z - z) < 9f) return true;
            foreach (var s in layout.Repairs) if ((s.Pos.x - x) * (s.Pos.x - x) + (s.Pos.z - z) * (s.Pos.z - z) < 9f) return true;
            foreach (var s in layout.Shelters) if ((s.Pos.x - x) * (s.Pos.x - x) + (s.Pos.z - z) * (s.Pos.z - z) < 16f) return true;
            foreach (var s in layout.LoreSpots) if ((s.Pos.x - x) * (s.Pos.x - x) + (s.Pos.z - z) * (s.Pos.z - z) < 4f) return true;
            return false;
        }

        float ZoneFactor(float x, float z)
        {
            foreach (var zd in layout.Zones)
            {
                float dx = zd.Center.x - x, dz = zd.Center.z - z;
                if (dx * dx + dz * dz < (zd.Radius + 1.5f) * (zd.Radius + 1.5f)) return 0.35f;
            }
            return 1f;
        }

        void BuildLitter()
        {
            // Raumhash der sammelbaren Objekte – Deko-Müll hält Abstand, damit Ziele lesbar bleiben
            trashHash = new Dictionary<long, List<Vector2>>();
            foreach (var t in layout.Trash)
            {
                long k = ((long)Mathf.FloorToInt(t.Pos.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(t.Pos.z / 4f);
                List<Vector2> l;
                if (!trashHash.TryGetValue(k, out l)) { l = new List<Vector2>(); trashHash[k] = l; }
                l.Add(new Vector2(t.Pos.x, t.Pos.z));
            }
            var tmp = new List<KeyValuePair<float, Matrix4x4>>[cells.Length][];
            var ground = GroundSpecs(); var heaps = HeapSpecs();
            float gSum = 0;
            foreach (var g in ground) gSum += g.Weight;
            // Streumüll als Grüppchen (8 Varianten mit je 3–6 Teilen)
            const int ClusterVariants = 8;
            var gIdx = new int[ClusterVariants];
            for (int i = 0; i < ClusterVariants; i++) gIdx[i] = AddClusterKind(ground, gSum, i, 3 + i % 4, 0.9f, 7f);
            var hIdx = new int[heaps.Count]; float hSum = 0;
            for (int i = 0; i < heaps.Count; i++) { hIdx[i] = AddKind(heaps[i], true); hSum += heaps[i].Weight; }
            List<Spec> floats = planet == "pelagia" ? FloatSpecs() : null;
            int[] fIdx = null; float fSum = 0;
            if (floats != null)
            {
                foreach (var f in floats) fSum += f.Weight;
                fIdx = new int[4];
                for (int i = 0; i < 4; i++) fIdx[i] = AddClusterKind(floats, fSum, 20 + i, 5 + i, 1.4f, 10f);
            }
            for (int c = 0; c < cells.Length; c++)
            {
                tmp[c] = new List<KeyValuePair<float, Matrix4x4>>[kinds.Count];
                for (int k = 0; k < kinds.Count; k++) tmp[c][k] = new List<KeyValuePair<float, Matrix4x4>>();
            }
            var rng = new Rng(def.Seed + 5150);
            System.Func<List<Spec>, float, int> pick = (specs, sum) =>
            {
                float r = rng.Next() * sum;
                for (int i = 0; i < specs.Count; i++) { r -= specs[i].Weight; if (r <= 0) return i; }
                return specs.Count - 1;
            };
            System.Action<int, Vector3, float, float, float> add = (kind, p, yaw, scale, tilt) =>
            {
                int ci = CellIndex(p.x, p.z);
                var m = Matrix4x4.TRS(p, Quaternion.Euler(rng.Range(-tilt, tilt), yaw, rng.Range(-tilt, tilt)), Vector3.one * scale);
                tmp[ci][kind].Add(new KeyValuePair<float, Matrix4x4>(rng.Next(), m));
            };

            // 1) Streumüll am Boden
            // Anzahl der Grüppchen (je ~4,5 Teile): insgesamt etwa 15 000–23 000 Kleinteile
            int target = planet == "pelagia" ? 3400 : planet == "nivalis" ? 3800 : 5200;
            int placedGround = 0;
            for (int i = 0; i < target * 6 && placedGround < target; i++)
            {
                float x = rng.Range(-147f, 147f), z = rng.Range(-147f, 147f);
                if (KeepOut(x, z)) continue;
                float inset = RoadInset(x, z);
                float dens;
                if (inset > 1.4f) continue;                               // Fahrbahnmitte bleibt frei
                else if (inset > -0.2f) dens = 1f;                         // Rinnstein: besonders viel
                else dens = 0.25f + 0.75f * Mathf.Clamp01(Noise.Fbm(x * 0.04f, z * 0.04f, def.Seed + 61, 3) * 1.9f - 0.45f);
                if (Blocked(x, z, 0.25f)) continue;
                if (Blocked(x, z, 2.5f)) dens = Mathf.Min(1f, dens + 0.45f); // an Hauswänden sammelt sich Müll
                dens *= ZoneFactor(x, z);
                if (rng.Next() > dens) continue;
                float h = Terrain.HeightAt(planet, x, z);
                if (planet == "pelagia" && h < 0.35f) continue;
                if (Mathf.Abs(Terrain.HeightAt(planet, x + 0.6f, z) - h) > 0.5f) continue;
                if (NearTrash(x, z, 1.1f)) continue;
                add(gIdx[rng.Range(0, gIdx.Length)], new Vector3(x, h, z), rng.Range(0f, 360f), rng.Range(0.85f, 1.2f), 4f);
                placedGround++;
            }

            // 2) Haufen an Hauswänden und 3) Müll auf Dächern
            int heapCount = 0, roofCount = 0;
            foreach (var bx in layout.Colliders)
            {
                if (bx.Gate >= 0 || bx.DuneSet >= 0 || !bx.Solid) continue;
                bool building = bx.Kind == "house" || bx.Kind == "mall" || bx.Kind == "shed" || bx.Kind == "hall" || bx.Kind == "warehouse" || bx.Kind == "serverhall"
                    || bx.Kind == "foundry" || bx.Kind == "hangar" || bx.Kind == "tower" || (bx.Kind == "areawall" && planet == "terra") || bx.Kind == "cityedge";
                if (!building) continue;
                for (int face = 0; face < 4; face++)
                {
                    float len = face < 2 ? bx.Hx * 2 : bx.Hz * 2;
                    for (float u = 2f; u < len - 2f; u += rng.Range(4f, 9f))
                    {
                        if (!rng.Chance(0.45f)) continue;
                        float off = rng.Range(0.7f, 1.1f);
                        float x, z, yaw;
                        switch (face)
                        {
                            case 0: x = bx.Cx - bx.Hx + u; z = bx.Cz + bx.Hz + off; yaw = 0; break;
                            case 1: x = bx.Cx - bx.Hx + u; z = bx.Cz - bx.Hz - off; yaw = 180; break;
                            case 2: x = bx.Cx + bx.Hx + off; z = bx.Cz - bx.Hz + u; yaw = 90; break;
                            default: x = bx.Cx - bx.Hx - off; z = bx.Cz - bx.Hz + u; yaw = 270; break;
                        }
                        if (KeepOut(x, z) || RoadInset(x, z) > 0.2f || Blocked(x, z, 0.3f) || NearTrash(x, z, 1.8f)) continue;
                        float h = Terrain.HeightAt(planet, x, z);
                        if (planet == "pelagia" && h < 0.35f) continue;
                        int s = pick(heaps, hSum);
                        add(hIdx[s], new Vector3(x, h - 0.05f, z), yaw + rng.Range(-35f, 35f), heaps[s].Scale * rng.Range(0.75f, 1.25f) * ZoneFactor(x, z), 4f);
                        heapCount++;
                        // kleiner Streumüll rund um den Haufen
                        for (int k = 0; k < 4; k++)
                        {
                            float a = rng.Range(0f, 6.28f), r = rng.Range(0.9f, 2.2f);
                            float lx = x + Mathf.Cos(a) * r, lz = z + Mathf.Sin(a) * r;
                            if (Blocked(lx, lz, 0.2f) || RoadInset(lx, lz) > 1.2f || NearTrash(lx, lz, 1f)) continue;
                            add(gIdx[rng.Range(0, gIdx.Length)], new Vector3(lx, Terrain.HeightAt(planet, lx, lz), lz), rng.Range(0f, 360f), rng.Range(0.8f, 1.1f), 4f);
                        }
                    }
                }
                // Dächer (nur flache Dächer; die Höhe liefert WorldView – Ruinen sind stellenweise abgebrochen)
                if (RoofAt != null && (bx.Kind == "house" || bx.Kind == "mall" || bx.Kind == "serverhall" || (bx.Kind == "areawall" && planet == "terra")))
                {
                    int n = Mathf.Clamp((int)(bx.Hx * bx.Hz * 0.06f), 1, 7);
                    for (int k = 0; k < n; k++)
                    {
                        if (!rng.Chance(0.6f)) continue;
                        float x = bx.Cx + rng.Range(-bx.Hx + 1f, bx.Hx - 1f), z = bx.Cz + rng.Range(-bx.Hz + 1f, bx.Hz - 1f);
                        float roof = RoofAt(bx, x);
                        if (float.IsNaN(roof)) continue;
                        int s = pick(heaps, hSum);
                        add(hIdx[s], new Vector3(x, roof + 0.02f, z), rng.Range(0f, 360f), heaps[s].Scale * rng.Range(0.8f, 1.4f), 3f);
                        roofCount++;
                    }
                }
            }

            // 4) Treibender Plastikteppich (PELAGIA)
            int floatCount = 0;
            if (floats != null)
            {
                for (int p = 0; p < 34; p++)
                {
                    float cx = rng.Range(-140f, 140f), cz = rng.Range(-140f, 140f);
                    if (Terrain.HeightAt(planet, cx, cz) > -1.2f || KeepOut(cx, cz)) continue;
                    if (Mathf.Abs(cx - 62f) < 12f && Mathf.Abs(cz + 100f) < 12f) continue; // Bootsanleger freihalten
                    float r = rng.Range(3f, 8f);
                    int n = (int)(r * r * 0.35f);
                    for (int k = 0; k < n; k++)
                    {
                        float a = rng.Range(0f, 6.28f), rr = Mathf.Sqrt(rng.Next()) * r;
                        float x = cx + Mathf.Cos(a) * rr * 1.4f, z = cz + Mathf.Sin(a) * rr;
                        if (Terrain.HeightAt(planet, x, z) > -0.6f || Blocked(x, z, 0.3f) || NearTrash(x, z, 1.4f)) continue;
                        add(fIdx[rng.Range(0, fIdx.Length)], new Vector3(x, 0.02f, z), rng.Range(0f, 360f), rng.Range(0.8f, 1.2f), 6f);
                        floatCount++;
                    }
                }
            }

            // In Zellen und 1023er-Blöcke überführen, nach Schwellwert sortiert
            int total = 0;
            for (int c = 0; c < cells.Length; c++)
            {
                int cx = c % CellsX, cz = c / CellsX;
                float x0 = -150f + cx * CellW, z0 = -150f + cz * CellD;
                var cell = new Cell
                {
                    Area = PlanetLayout.AreaOf(z0 + CellD * 0.5f),
                    B = new Bounds(new Vector3(x0 + CellW * 0.5f, 18f, z0 + CellD * 0.5f), new Vector3(CellW + 4f, 76f, CellD + 4f)),
                    Chunks = new Matrix4x4[kinds.Count][][],
                    Count = new int[kinds.Count],
                };
                for (int k = 0; k < kinds.Count; k++)
                {
                    var l = tmp[c][k];
                    l.Sort((a, b) => a.Key.CompareTo(b.Key));
                    int n = l.Count;
                    cell.Count[k] = n;
                    total += n;
                    int chunks = (n + 1022) / 1023;
                    cell.Chunks[k] = new Matrix4x4[chunks][];
                    for (int j = 0; j < chunks; j++)
                    {
                        int m = Mathf.Min(1023, n - j * 1023);
                        var arr = new Matrix4x4[m];
                        for (int q = 0; q < m; q++) arr[q] = l[j * 1023 + q].Value;
                        cell.Chunks[k][j] = arr;
                    }
                }
                cells[c] = cell;
            }
            TotalInstances = total;
            Palette.Flush();
            trashHash = null;
            Debug.Log("[RE:PLANET] Hintergrund " + planet + ": " + total + " Deko-Instanzen (Boden " + placedGround + ", Haufen " + heapCount + ", Dach " + roofCount + ", treibend " + floatCount + "), Ring " + RingVertices + " Ecken.");
        }

        // ================================================================== Zeichnen
        /// <summary>Zeichnet den Streumüll (einmal pro Bild aus WorldView.Update).</summary>
        public void Draw(Camera cam, float dt)
        {
            if (cam == null) return;
            for (int a = 0; a < 3; a++) density[a] = Mathf.MoveTowards(density[a], targetDensity[a], dt * 0.25f);
            // Lichtpunkte in der Ferne: nachts warm, Warnleuchten blinken (1,6-s-Takt)
            var wv = WorldView.I;
            var world = wv != null ? wv.World : null;
            float dark = world != null ? Rules.Darkness(Rules.DayPhase(world, planet)) : 0f;
            if (farLights != null) Mats.SetEmission(farLights, FarWarm * Mathf.Lerp(0.08f, 2.4f, dark));
            if (blinkRed != null) Mats.SetEmission(blinkRed, WarnRed * (Mathf.Repeat(Time.time, 1.6f) < 0.8f ? Mathf.Lerp(1.5f, 4f, dark) : 0.25f));
            var st = GameApp.I != null ? GameApp.I.Settings : null;
            int quality = st != null ? st.Quality : 2;
            float view = st != null ? st.ViewDistance : 1f;
            bool shadows = st == null || st.Shadows >= 2;
            float qScale = quality <= 0 ? 0.3f : quality == 1 ? 0.55f : quality == 2 ? 0.8f : 1f;
            float far = 80f * view;
            float heapFar = 130f * view;
            var cp = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            bool instancing = SystemInfo.supportsInstancing;
            int drawn = 0;
            for (int c = 0; c < cells.Length; c++)
            {
                var cell = cells[c];
                if (cell == null) continue;
                float dens = density[cell.Area];
                if (dens <= 0.001f) continue;
                float dist = Mathf.Sqrt(cell.B.SqrDistance(cp));
                if (dist > heapFar) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, cell.B)) continue;
                // entfernte Zellen dünner zeichnen (kleiner Müll wird dort ohnehin winzig)
                float lod = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((dist - 25f) / 55f));
                for (int k = 0; k < kinds.Count; k++)
                {
                    var kind = kinds[k];
                    if (!kind.Heap && dist > far) continue;
                    int n = Mathf.CeilToInt(cell.Count[k] * dens * qScale * (kind.Heap ? 1f : lod));
                    if (n <= 0) continue;
                    var sh = kind.Heap && shadows && dist < 30f ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    var chunks = cell.Chunks[k];
                    for (int j = 0; j < chunks.Length && n > 0; j++)
                    {
                        int m = Mathf.Min(n, chunks[j].Length);
                        if (instancing)
                        {
                            // Zelle unter der Kamera: Kleinteile direkt vor der Nahebene auslassen – eine Dose 0,5 m vor der
                            // Kamera (Kamera am Hang dicht über dem Boden) füllte sonst als riesiger Klotz das halbe Bild
                            if (!kind.Heap && dist < 2f)
                            {
                                int mm = 0;
                                var src = chunks[j];
                                for (int q = 0; q < m; q++)
                                {
                                    float dx = src[q].m03 - cp.x, dy = src[q].m13 - cp.y, dz = src[q].m23 - cp.z;
                                    if (dx * dx + dy * dy + dz * dz < NearCull * NearCull) continue;
                                    nearBuf[mm++] = src[q];
                                }
                                if (mm > 0) Graphics.DrawMeshInstanced(kind.Mesh, 0, kind.Mat, nearBuf, mm, null, sh, true, 0, null);
                            }
                            else Graphics.DrawMeshInstanced(kind.Mesh, 0, kind.Mat, chunks[j], m, null, sh, true, 0, null);
                        }
                        n -= m; drawn += m;
                    }
                }
            }
            LastDrawn = drawn;
        }
    }
}
