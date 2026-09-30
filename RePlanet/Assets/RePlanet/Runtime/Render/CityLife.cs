using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// „Stadt erwacht 2.0“: Wiederhergestellte Bereiche werden lebendig – je höher der Wiederherstellungsgrad, desto mehr:
    /// mehr beleuchtete Fenster (eigener Fenster-Shader: Anteil je Bereich), stärkere Brunnen, Lichthöfe um Laternen
    /// bei Nacht, kleine Elektroautos und (TERRA) Straßenbahnen auf den Straßen, Fahnen, die im Wind wehen, und
    /// holografische Schilder über Projektplätzen und alten Werbetafeln.
    /// Voraussetzung ist das abgeschlossene Projekt des Bereichs (wie beim bisherigen „Stadt erwacht“). Fahrzeuge fahren
    /// auf vorab berechneten freien Straßenabschnitten, halten vor MIKO, Mitspielern, Fahrzeugen, anderen Autos und
    /// noch liegendem großem Müll, und wenden, wenn es nicht weitergeht. Plätze und Anzahl sind deterministisch aus
    /// Layout und repliziertem Zustand – alle Mitspieler sehen dieselbe Stadt. Zeichnung per Instancing.
    /// </summary>
    public class CityLife : MonoBehaviour
    {
        public static CityLife I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(CityLife))) GameApp.Components.Add(typeof(CityLife));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<CityLife>() == null) GameApp.I.gameObject.AddComponent<CityLife>();
        }

        // ------------------------------------------------------------------ Straßen
        class Run
        {
            public Vector2 A, Dir;        // Mittellinie: Punkt bei s = 0 und Richtung
            public float S0, S1, Width, Lane;
            public int Area;
            public bool TramOk;
            public readonly List<Vector3> Trash = new List<Vector3>(); // x = s, y = seitlich, z = Objekt-ID
            public Vector2 Right { get { return new Vector2(Dir.y, -Dir.x); } }
        }

        class Car
        {
            public int Index, Area, Variant, State; // 0 fahren, 1 warten, 2 wenden
            public bool Tram, Wanted, Alive;
            public Run Run;
            public float S, Sign = 1f, Speed, Wait, Turn, Appear, Yaw;
            public Vector3 Pos;
        }

        class Flag { public Vector3 Base; public int Area, Variant; public float Threshold, Phase; }
        class Holo { public Vector3 Pos; public int Area, Icon; public float Phase; }

        const float TramLen = 13f;
        string planet;
        PlanetLayout layout;
        readonly List<Run> runs = new List<Run>();
        readonly List<Car> cars = new List<Car>();
        readonly List<Flag> flags = new List<Flag>();
        readonly List<Holo> holos = new List<Holo>();
        readonly List<Box> tmp = new List<Box>();
        readonly float[] restoration = new float[3];
        readonly bool[] awake = new bool[3];
        readonly List<Vector4> threats = new List<Vector4>();
        float timer, dark, windX = 1f, windZ = 0.3f, wind = 0.2f;

        InstanceBatch[] carBatches;
        InstanceBatch tramBatch, poleBatch, holoRing, holoBeam, haloBatch;
        InstanceBatch[] clothBatches;
        readonly Dictionary<int, InstanceBatch> holoIcons = new Dictionary<int, InstanceBatch>();
        Material holoIconMat;
        Texture2D haloTex;

        /// <summary>Statistik (Prüfumgebung/Leistung).</summary>
        public int CarsAlive { get; private set; }
        public int TramsAlive { get; private set; }
        public int FlagsShown { get; private set; }
        public int HolosShown { get; private set; }
        public int HalosShown { get; private set; }
        public int RunCount { get { return runs.Count; } }
        public int CarSlots { get { return cars.Count; } }

        void Awake() { I = this; }

        void OnDestroy()
        {
            if (haloTex != null) Destroy(haloTex);
            if (I == this) I = null;
        }

        // ================================================================== Formen
        public static Mesh CarMesh()
        {
            return MeshKit.Get("stadt_auto", b =>
            {
                b.Sub = 0;
                b.BevelBox(new Vector3(0, 0.58f, 0), new Vector3(1.7f, 0.52f, 3.4f), 0.14f);
                b.BevelBox(new Vector3(0, 1.08f, -0.15f), new Vector3(1.5f, 0.5f, 1.9f), 0.16f);
                b.Sub = 1;
                b.Box(new Vector3(0, 1.1f, 0.83f), new Vector3(1.32f, 0.36f, 0.04f));
                b.Box(new Vector3(0, 1.1f, -1.12f), new Vector3(1.3f, 0.34f, 0.04f));
                foreach (var sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * 0.755f, 1.1f, -0.15f), new Vector3(0.03f, 0.32f, 1.6f));
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1.1f, 1.1f }) b.CylinderX(new Vector3(sx * 0.78f, 0.32f, sz), 0.32f, 0.26f, 12);
                b.BevelBox(new Vector3(0, 0.36f, 1.7f), new Vector3(1.72f, 0.18f, 0.12f), 0.04f);
                b.BevelBox(new Vector3(0, 0.36f, -1.7f), new Vector3(1.72f, 0.18f, 0.12f), 0.04f);
                b.Sub = 3;
                foreach (var sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * 0.58f, 0.66f, 1.705f), new Vector3(0.34f, 0.12f, 0.03f));
                b.Sub = 4;
                foreach (var sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * 0.62f, 0.68f, -1.705f), new Vector3(0.26f, 0.12f, 0.03f));
                b.Sub = 0;
            });
        }

        public static Mesh TramMesh()
        {
            return MeshKit.Get("stadt_bahn", b =>
            {
                b.Sub = 0;
                b.BevelBox(new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.5f, TramLen - 0.6f), 0.25f);
                b.BevelBox(new Vector3(0, 1.6f, TramLen * 0.5f - 0.35f), new Vector3(2.3f, 2.1f, 0.5f), 0.3f);
                b.BevelBox(new Vector3(0, 1.6f, -TramLen * 0.5f + 0.35f), new Vector3(2.3f, 2.1f, 0.5f), 0.3f);
                b.Sub = 1;
                foreach (var sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * 1.26f, 2.05f, 0), new Vector3(0.04f, 0.95f, TramLen - 2.2f));
                b.Box(new Vector3(0, 2.0f, TramLen * 0.5f - 0.08f), new Vector3(1.8f, 1.0f, 0.04f));
                b.Box(new Vector3(0, 2.0f, -TramLen * 0.5f + 0.08f), new Vector3(1.8f, 1.0f, 0.04f));
                b.Sub = 2;
                b.Box(new Vector3(0, 0.35f, 0), new Vector3(2.2f, 0.4f, TramLen - 2f));
                b.Box(new Vector3(0, 3.1f, 0), new Vector3(0.8f, 0.18f, 3f));
                b.Beam(new Vector3(0, 3.2f, -0.6f), new Vector3(0, 3.9f, 0.4f), 0.05f);
                b.Beam(new Vector3(0, 3.9f, 0.4f), new Vector3(0, 4.1f, -0.2f), 0.05f);
                b.Box(new Vector3(0, 4.12f, -0.2f), new Vector3(1.2f, 0.05f, 0.08f));
                b.Sub = 3;
                foreach (var sz in new[] { -1f, 1f })
                    foreach (var sx in new[] { -0.7f, 0.7f }) b.Box(new Vector3(sx, 0.9f, sz * (TramLen * 0.5f + 0.02f)), new Vector3(0.3f, 0.14f, 0.03f));
                b.Sub = 0;
            });
        }

        public static Mesh PoleMesh()
        {
            return MeshKit.Get("stadt_fahnenmast", b =>
            {
                b.Cylinder(Vector3.zero, 0.09f, 0.25f, 8, true, 0.07f);
                b.Cylinder(Vector3.zero, 0.05f, 6.1f, 8, true, 0.035f);
                b.Sphere(new Vector3(0, 6.15f, 0), 0.08f, 8, 5);
            });
        }

        /// <summary>Ein Stoffsegment der Fahne (0,55 m breit), hängt vom Gelenk bei x = 0 nach +X.</summary>
        public static Mesh ClothMesh()
        {
            return MeshKit.Get("stadt_fahnenstoff", b =>
            {
                var a = new Vector3(0, 0, 0); var c = new Vector3(0.56f, 0, 0); var d = new Vector3(0.56f, -0.95f, 0); var e = new Vector3(0, -0.95f, 0);
                b.Face(a, c, d, e, Vector3.forward);
                b.Face(a, c, d, e, Vector3.back);
            });
        }

        public static Mesh HoloRingMesh() { return MeshKit.Get("stadt_holo_ring", b => { b.Torus(Vector3.zero, 1.6f, 0.05f, 28, 4); b.Torus(new Vector3(0, -0.35f, 0), 1.2f, 0.035f, 24, 4); }); }
        public static Mesh HoloBeamMesh() { return MeshKit.Get("stadt_holo_strahl", b => b.Cylinder(Vector3.zero, 0.35f, 1f, 12, false, 1.3f)); }

        public static Mesh HoloIconMesh(int icon)
        {
            return MeshKit.Get("stadt_holo_symbol" + icon, b =>
            {
                b.UVRect = SurfaceLook.IconRect(icon);
                float h = 1.3f;
                b.Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Vector3.back);
                b.Quad(new Vector3(h, -h, 0), new Vector3(-h, -h, 0), new Vector3(-h, h, 0), new Vector3(h, h, 0), Vector3.forward);
            });
        }

        public static Mesh HaloMesh() { return MeshKit.Get("stadt_lichthof", b => b.Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), Vector3.back)); }

        public static void PrewarmMeshes()
        {
            CarMesh(); TramMesh(); PoleMesh(); ClothMesh(); HoloRingMesh(); HoloBeamMesh(); HaloMesh();
            foreach (var i in new[] { SurfaceLook.Icon.Leaf, SurfaceLook.Icon.Recycle, SurfaceLook.Icon.Drop, SurfaceLook.Icon.Star }) HoloIconMesh(i);
        }

        static Texture2D HaloTexture()
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "RP_Lichthof", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (0.6f + 0.4f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        // ================================================================== Aufbau je Planet
        static Color[] Paints(string p)
        {
            switch (p)
            {
                case "pyra": return new[] { new Color(0.72f, 0.33f, 0.18f), new Color(0.85f, 0.7f, 0.45f), new Color(0.4f, 0.42f, 0.45f), new Color(0.95f, 0.55f, 0.15f) };
                case "pelagia": return new[] { new Color(0.2f, 0.7f, 0.7f), new Color(1f, 0.5f, 0.45f), new Color(0.95f, 0.95f, 0.92f), new Color(0.3f, 0.45f, 0.8f) };
                case "nivalis": return new[] { new Color(0.92f, 0.94f, 0.97f), new Color(0.25f, 0.45f, 0.8f), new Color(0.85f, 0.25f, 0.2f), new Color(0.35f, 0.38f, 0.42f) };
                default: return new[] { new Color(0.18f, 0.68f, 0.64f), new Color(1f, 0.58f, 0.2f), new Color(0.95f, 0.95f, 0.92f), new Color(0.95f, 0.8f, 0.25f) };
            }
        }

        static Color[] FlagColors(string p)
        {
            switch (p)
            {
                case "pyra": return new[] { new Color(0.95f, 0.55f, 0.15f), new Color(0.25f, 0.6f, 0.55f), new Color(0.95f, 0.9f, 0.8f) };
                case "pelagia": return new[] { new Color(0.2f, 0.75f, 0.8f), new Color(1f, 0.55f, 0.6f), new Color(1f, 0.95f, 0.6f) };
                case "nivalis": return new[] { new Color(0.4f, 0.6f, 1f), new Color(0.75f, 0.45f, 1f), new Color(0.3f, 0.95f, 0.75f) };
                default: return new[] { new Color(0.3f, 0.75f, 0.35f), new Color(1f, 0.6f, 0.2f), new Color(0.2f, 0.65f, 0.85f) };
            }
        }

        void Build(string p)
        {
            planet = p;
            layout = WorldGen.Get(p);
            runs.Clear(); cars.Clear(); flags.Clear(); holos.Clear(); holoIcons.Clear();
            var glass = Mats.Get(Mats.Opaque, new Color(0.12f, 0.16f, 0.2f), null, 0.9f);
            var darkM = Mats.Get(Mats.Opaque, new Color(0.1f, 0.1f, 0.11f));
            var head = Mats.Get(Mats.Emissive, new Color(1f, 0.95f, 0.8f), new Color(1.6f, 1.5f, 1.2f));
            var tail = Mats.Get(Mats.Emissive, new Color(1f, 0.2f, 0.12f), new Color(1.5f, 0.2f, 0.1f));
            var paints = Paints(p);
            carBatches = new InstanceBatch[paints.Length];
            for (int i = 0; i < paints.Length; i++)
                carBatches[i] = new InstanceBatch("auto" + i, CarMesh(), new[] { Mats.Get(Mats.Metal, paints[i]), glass, darkM, head, tail });
            tramBatch = new InstanceBatch("bahn", TramMesh(), new[] { Mats.Get(Mats.Opaque, new Color(0.95f, 0.92f, 0.82f)), glass, darkM, head });
            poleBatch = new InstanceBatch("mast", PoleMesh(), new[] { Mats.Get(Mats.Metal, new Color(0.75f, 0.77f, 0.8f)) });
            var fc = FlagColors(p);
            clothBatches = new InstanceBatch[fc.Length];
            for (int i = 0; i < fc.Length; i++) clothBatches[i] = new InstanceBatch("fahne" + i, ClothMesh(), new[] { Mats.Get(Mats.Opaque, fc[i]) });
            var cyan = p == "pyra" ? new Color(1f, 0.7f, 0.35f) : p == "nivalis" ? new Color(0.6f, 0.75f, 1f) : new Color(0.35f, 1f, 0.9f);
            holoRing = new InstanceBatch("holo_ring", HoloRingMesh(), new[] { Mats.Get(Mats.Emissive, cyan, cyan * 2.2f) });
            holoBeam = new InstanceBatch("holo_strahl", HoloBeamMesh(), new[] { Mats.Get(Mats.Fade, new Color(cyan.r, cyan.g, cyan.b, 0.1f)) });
            holoIconMat = Mats.Unique(Mats.ParticleAdd, new Color(cyan.r, cyan.g, cyan.b, 0.85f));
            holoIconMat.mainTexture = SurfaceLook.IconAtlas();
            if (haloTex == null) haloTex = HaloTexture();
            var haloMat = Mats.Unique(Mats.ParticleAdd, new Color(1f, 0.8f, 0.5f, 0.5f));
            haloMat.mainTexture = haloTex;
            haloBatch = new InstanceBatch("lichthof", HaloMesh(), new[] { haloMat });
            BuildRuns();
            BuildCars();
            BuildFlags();
            BuildHolos();
            for (int a = 0; a < 3; a++) { restoration[a] = -1f; awake[a] = false; }
        }

        static float AreaZ0(int a) { return a == 0 ? -147f : a == 1 ? -47f : 53f; }
        static float AreaZ1(int a) { return a == 0 ? -53f : a == 1 ? 47f : 147f; }

        /// <summary>Freie Straßenabschnitte je Bereich (beide Fahrspuren ohne Gebäude, Requisiten und Stützpunkt).</summary>
        void BuildRuns()
        {
            // Requisiten in einem 8-m-Raster für schnelle Nähe-Abfragen
            var propGrid = new Dictionary<long, List<Vector2>>();
            foreach (var pr in layout.Props)
            {
                if (pr.Kind == "skyline" || pr.Kind == "mesa" || pr.Kind == "farisland" || pr.Kind == "icepeak") continue;
                long key = ((long)Mathf.FloorToInt(pr.Pos.x / 8f) << 32) ^ (uint)Mathf.FloorToInt(pr.Pos.z / 8f);
                List<Vector2> l;
                if (!propGrid.TryGetValue(key, out l)) propGrid[key] = l = new List<Vector2>();
                l.Add(new Vector2(pr.Pos.x, pr.Pos.z));
            }
            bool PropNear(float x, float z, float r)
            {
                int cx = Mathf.FloorToInt(x / 8f), cz = Mathf.FloorToInt(z / 8f);
                for (int i = -1; i <= 1; i++)
                    for (int k = -1; k <= 1; k++)
                    {
                        List<Vector2> l;
                        if (!propGrid.TryGetValue(((long)(cx + i) << 32) ^ (uint)(cz + k), out l)) continue;
                        foreach (var q in l) if ((q.x - x) * (q.x - x) + (q.y - z) * (q.y - z) < r * r) return true;
                    }
                return false;
            }
            foreach (var r in layout.Roads)
            {
                var a = new Vector2(r[0], r[1]); var b = new Vector2(r[2], r[3]);
                var d = b - a; float len = d.magnitude;
                if (len < 10f) continue;
                var dir = d / len;
                float width = r[4];
                float lane = width * 0.25f;
                var right = new Vector2(dir.y, -dir.x);
                for (int area = 0; area < 3; area++)
                {
                    float s = 0f, start = -1f;
                    for (; s <= len; s += 1f)
                    {
                        var c = a + dir * s;
                        bool free = PlanetLayout.AreaOf(c.y) == area && c.y > AreaZ0(area) && c.y < AreaZ1(area) && LifeCommon.InWorld(c.x, c.y, 4f);
                        if (free)
                            foreach (var off in new[] { -lane, 0f, lane })
                            {
                                var q = c + right * off;
                                if (layout.Base.InBase(q.x, q.y) || Mathf.Abs(q.x) < 42f && q.y < -86f && q.y > -156f || LifeCommon.Solid(layout, q.x, q.y, 1.4f, tmp) || PropNear(q.x, q.y, 1.9f) || layout.GroundAt(q.x, q.y) > Terrain.HeightAt(planet, q.x, q.y) + 0.05f)
                                { free = false; break; }
                                if (Terrain.WaterLevel(planet) > -50f && Terrain.HeightAt(planet, q.x, q.y) < Terrain.WaterLevel(planet) + 0.2f) { free = false; break; }
                            }
                        if (free && start < 0f) start = s;
                        if ((!free || s + 1f > len) && start >= 0f)
                        {
                            float end = free ? s : s - 1f;
                            if (end - start >= 24f) runs.Add(new Run { A = a, Dir = dir, S0 = start, S1 = end, Width = width, Lane = lane, Area = area, TramOk = planet == "terra" && width >= 12f && end - start >= 50f });
                            start = -1f;
                        }
                    }
                }
            }
            // Großer Müll auf der Fahrbahn: Autos halten davor, solange er liegt
            foreach (var t in layout.Trash)
            {
                if (t.Def.Size < 0.6f && !t.Def.Crane) continue;
                var p = new Vector2(t.Pos.x, t.Pos.z);
                foreach (var run in runs)
                {
                    var rel = p - run.A;
                    float s = Vector2.Dot(rel, run.Dir), lat = Vector2.Dot(rel, run.Right);
                    if (s < run.S0 - 3f || s > run.S1 + 3f || Mathf.Abs(lat) > run.Width * 0.5f + 0.5f) continue;
                    run.Trash.Add(new Vector3(s, lat, t.Id));
                }
            }
        }

        void BuildCars()
        {
            if (runs.Count == 0) return;
            var seed = GameData.Planets[planet].Seed;
            for (int area = 0; area < 3; area++)
            {
                var mine = runs.FindAll(r => r.Area == area);
                if (mine.Count == 0) continue;
                for (int i = 0; i < 7; i++)
                {
                    var rng = new Rng(seed * 313 + area * 71 + i * 17 + 5);
                    var run = mine[rng.Range(0, mine.Count)];
                    cars.Add(new Car { Index = cars.Count, Area = area, Variant = rng.Range(0, 4), Run = run, S = rng.Range(run.S0 + 8f, run.S1 - 8f), Sign = rng.Chance(0.5f) ? 1f : -1f });
                }
                var tramRun = mine.Find(r => r.TramOk);
                if (tramRun != null)
                    cars.Add(new Car { Index = cars.Count, Area = area, Tram = true, Run = tramRun, S = (tramRun.S0 + tramRun.S1) * 0.5f, Sign = area % 2 == 0 ? 1f : -1f });
            }
        }

        void BuildFlags()
        {
            var rng = new Rng(GameData.Planets[planet].Seed + 909);
            // Fahnen rund um die Projektplätze
            for (int a = 0; a < 3; a++)
            {
                var c = layout.ProjectSites[a];
                for (int k = 0; k < 6; k++)
                {
                    float ang = k / 6f * Mathf.PI * 2f + 0.3f;
                    TryFlag(c.x + Mathf.Cos(ang) * 13f, c.z + Mathf.Sin(ang) * 13f, a, rng);
                }
            }
            // Fahnen am Straßenrand (auf dem Gehweg, alle 18 m)
            foreach (var run in runs)
            {
                for (float s = run.S0 + 6f; s < run.S1 - 6f; s += 18f)
                {
                    float side = ((int)(s / 18f) % 2 == 0) ? 1f : -1f;
                    var q = run.A + run.Dir * s + run.Right * side * (run.Width * 0.5f + 1.1f);
                    TryFlag(q.x, q.y, run.Area, rng);
                }
            }
        }

        void TryFlag(float x, float z, int area, Rng rng)
        {
            float th = rng.Next(), ph = rng.Range(0f, 6.28f); int v = rng.Range(0, 3);
            if (flags.Count >= 90 || !LifeCommon.InWorld(x, z, 4f) || layout.Base.InBase(x, z) || PlanetLayout.AreaOf(z) != area) return;
            if (LifeCommon.Solid(layout, x, z, 0.8f, tmp) || LifeCommon.OnRoad(layout, x, z, -0.2f)) return;
            float h = Terrain.HeightAt(planet, x, z);
            if (Terrain.WaterLevel(planet) > -50f && h < Terrain.WaterLevel(planet) + 0.3f) return;
            foreach (var f in flags) if ((f.Base - new Vector3(x, h, z)).sqrMagnitude < 9f) return;
            flags.Add(new Flag { Base = new Vector3(x, h, z), Area = area, Variant = v, Threshold = th, Phase = ph });
        }

        void BuildHolos()
        {
            int[] icons = { SurfaceLook.Icon.Leaf, SurfaceLook.Icon.Recycle, SurfaceLook.Icon.Drop, SurfaceLook.Icon.Star };
            for (int a = 0; a < 3; a++)
            {
                var c = layout.ProjectSites[a];
                holos.Add(new Holo { Pos = new Vector3(c.x, Terrain.HeightAt(planet, c.x, c.z), c.z), Area = a, Icon = planet == "pelagia" ? SurfaceLook.Icon.Drop : icons[a % 2], Phase = a * 1.7f });
            }
            int n = 0;
            foreach (var pr in layout.Props)
            {
                if (pr.Kind != "billboard" || n >= 6) continue;
                int a = Mathf.Clamp(pr.Area, 0, 2);
                holos.Add(new Holo { Pos = new Vector3(pr.Pos.x, pr.Pos.y, pr.Pos.z), Area = a, Icon = icons[(n + 1) % icons.Length], Phase = n * 2.3f });
                n++;
            }
            foreach (var h in holos) if (!holoIcons.ContainsKey(h.Icon)) holoIcons[h.Icon] = new InstanceBatch("holo_symbol" + h.Icon, HoloIconMesh(h.Icon), new[] { holoIconMat });
        }

        // ================================================================== Aktualisierung
        void Update()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null || wv.World == null || string.IsNullOrEmpty(wv.Planet)) return;
            if (planet != wv.Planet) Build(wv.Planet);
            var w = wv.World;
            PlanetState ps;
            if (!w.Planets.TryGetValue(planet, out ps)) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            timer -= dt;
            if (timer <= 0f)
            {
                timer = 0.5f;
                float sum = 0f;
                for (int a = 0; a < 3; a++)
                {
                    restoration[a] = LifeCommon.AreaRestoration(w, ps, a);
                    awake[a] = LifeCommon.AreaAwake(ps, a);
                    if (awake[a]) wv.SetWindowShare(a, Mathf.Lerp(0.3f, 0.82f, restoration[a]));
                    sum += restoration[a];
                }
                wv.SetFountainStrength(sum / 3f);
                dark = Rules.Darkness(Rules.DayPhase(w, planet));
                float qs = LifeCommon.QualityScale;
                foreach (var c in cars)
                {
                    if (c.Tram) { c.Wanted = awake[c.Area] && restoration[c.Area] >= 0.6f && LifeCommon.Quality >= 1; continue; }
                    int want = awake[c.Area] ? Mathf.RoundToInt(6f * Mathf.Max(0.5f, qs) * LifeCommon.Smooth(0.35f, 1f, restoration[c.Area])) : 0;
                    if (ps.StormActive) want = Mathf.Min(want, 1);
                    int rank = 0;
                    foreach (var o in cars) if (o.Area == c.Area && !o.Tram && o.Index < c.Index) rank++;
                    c.Wanted = rank < want;
                }
            }
            wind = Rules.Wind(w, planet, out windX, out windZ);
            GatherThreats(w);
            foreach (var c in cars) UpdateCar(c, ps, dt);
            Render();
        }

        void GatherThreats(WorldState w)
        {
            threats.Clear();
            var av = ActorsView.I;
            if (av == null) return;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online) continue;
                var r = av.RobotOf(p.Id);
                if (r != null && r.gameObject.activeInHierarchy) { var q = r.transform.position; threats.Add(new Vector4(q.x, q.y, q.z, 1.2f)); }
            }
            foreach (var v in w.Cur.Vehicles.Values)
            {
                Vector3 q; float y;
                if (av.VehiclePose(v.Id, out q, out y)) threats.Add(new Vector4(q.x, q.y, q.z, 3f));
            }
        }

        Vector3 CarPos(Car c, float s, float lat)
        {
            var p = c.Run.A + c.Run.Dir * s + c.Run.Right * lat;
            return new Vector3(p.x, Terrain.HeightAt(planet, p.x, p.y), p.y);
        }

        void UpdateCar(Car c, PlanetState ps, float dt)
        {
            if (c.Wanted && !c.Alive) { c.Alive = true; c.Appear = 0f; c.State = 0; c.Speed = 0f; }
            if (!c.Alive) return;
            c.Appear = Mathf.MoveTowards(c.Appear, c.Wanted ? 1f : 0f, dt * 0.6f);
            if (!c.Wanted && c.Appear <= 0f) { c.Alive = false; return; }
            var run = c.Run;
            float lane = c.Tram ? 0f : run.Lane;
            float cruise = c.Tram ? 6f : 7.5f;
            float half = c.Tram ? TramLen * 0.5f : 1.8f;
            var fwd = new Vector3(run.Dir.x, 0, run.Dir.y) * c.Sign;
            if (c.State == 2)
            {
                // Wenden im Halbkreis auf die Gegenspur
                c.Turn += dt / 2.2f;
                float th = Mathf.Clamp01(c.Turn) * Mathf.PI;
                float lat = c.Sign * lane * Mathf.Cos(th);
                float sAdv = c.S + c.Sign * lane * Mathf.Sin(th);
                c.Pos = CarPos(c, sAdv, lat);
                c.Yaw = Mathf.Atan2(run.Dir.x * c.Sign, run.Dir.y * c.Sign) * Mathf.Rad2Deg + Mathf.Clamp01(c.Turn) * 180f;
                if (c.Turn >= 1f) { c.State = 0; c.Sign = -c.Sign; c.Speed = 0f; }
                return;
            }
            // Hindernis voraus?
            bool blocked = false;
            var here = CarPos(c, c.S, c.Sign * lane);
            foreach (var t in threats)
            {
                var d = new Vector3(t.x - here.x, 0, t.z - here.z);
                float ahead = Vector3.Dot(d, fwd), side = Mathf.Abs(Vector3.Dot(d, new Vector3(fwd.z, 0, -fwd.x)));
                if (ahead > -1f && ahead < half + 7f && side < 1.6f + t.w * 0.5f) { blocked = true; break; }
            }
            if (!blocked)
                foreach (var o in cars)
                {
                    if (o == c || !o.Alive) continue;
                    var d = o.Pos - here; d.y = 0;
                    float ahead = Vector3.Dot(d, fwd), side = Mathf.Abs(Vector3.Dot(d, new Vector3(fwd.z, 0, -fwd.x)));
                    float oh = o.Tram ? TramLen * 0.5f : 1.8f;
                    if (ahead > 0f && ahead < half + oh + 4f && side < (o.Tram || c.Tram ? 2.2f : 1.7f))
                    {
                        // Gegenseitige Sicht (Kreuzung): der Wagen mit der kleineren Nummer fährt zuerst
                        var ofwd = new Vector3(o.Run.Dir.x, 0, o.Run.Dir.y) * o.Sign;
                        bool mutual = Vector3.Dot(-d, ofwd) > 0f && Mathf.Abs(Vector3.Dot(-d, new Vector3(ofwd.z, 0, -ofwd.x))) < 1.7f;
                        if (!mutual || c.Index > o.Index) { blocked = true; break; }
                    }
                }
            if (!blocked)
                foreach (var tr in run.Trash)
                {
                    float ahead = (tr.x - c.S) * c.Sign;
                    if (ahead < 0f || ahead > half + 6f || Mathf.Abs(tr.y - c.Sign * lane) > 2f) continue;
                    if (ps.Removed.Get((int)tr.z)) continue;
                    blocked = true; break;
                }
            // Wenden braucht vor dem Abschnittsende Platz (Halbkreis mit Radius = Spurabstand)
            float margin = half + lane + 1.5f;
            float end = c.Sign > 0 ? run.S1 - margin : run.S0 + margin;
            float toEnd = (end - c.S) * c.Sign;
            bool atEnd = toEnd <= 0.3f;
            // vorausschauend bremsen (3 m/s²), damit der Wagen vor dem Abschnittsende steht
            float vMax = Mathf.Min(cruise, Mathf.Sqrt(2f * 3f * Mathf.Max(0f, toEnd - 0.2f)));
            float want = blocked || atEnd ? 0f : vMax;
            c.Speed = Mathf.MoveTowards(c.Speed, want, dt * (blocked ? 9f : want < c.Speed ? 5f : 2.5f));
            if (!blocked && c.Speed * dt > toEnd) c.Speed = Mathf.Max(0f, toEnd / Mathf.Max(dt, 1e-4f));
            if (c.Speed < 0.05f && (blocked || atEnd))
            {
                c.Wait += dt;
                if (c.Wait > (atEnd ? (c.Tram ? 5f : 0.6f) : 4.5f))
                {
                    c.Wait = 0f;
                    if (c.Tram) c.Sign = -c.Sign; // Zweirichtungsbahn: fährt einfach zurück
                    else { c.State = 2; c.Turn = 0f; }
                }
            }
            else c.Wait = 0f;
            c.S = Mathf.Clamp(c.S + c.Sign * c.Speed * dt, run.S0, run.S1);
            c.Pos = CarPos(c, c.S, c.Sign * lane);
            c.Yaw = Mathf.Atan2(run.Dir.x * c.Sign, run.Dir.y * c.Sign) * Mathf.Rad2Deg;
        }

        // ================================================================== Zeichnen
        void Render()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cp = cam.transform.position;
            float vs = LifeCommon.ViewScale;
            foreach (var b in carBatches) b.Clear();
            tramBatch.Clear(); poleBatch.Clear(); holoRing.Clear(); holoBeam.Clear(); haloBatch.Clear();
            foreach (var b in clothBatches) b.Clear();
            foreach (var b in holoIcons.Values) b.Clear();
            int carsN = 0, trams = 0;
            float carView = 160f * vs;
            foreach (var c in cars)
            {
                if (!c.Alive) continue;
                if (c.Tram) trams++; else carsN++;
                if ((c.Pos - cp).sqrMagnitude > carView * carView) continue;
                var mx = Matrix4x4.TRS(c.Pos, Quaternion.Euler(0, c.Yaw, 0), Vector3.one * Mathf.Max(0.01f, c.Appear));
                if (c.Tram) tramBatch.Add(mx); else carBatches[c.Variant % carBatches.Length].Add(mx);
            }
            CarsAlive = carsN; TramsAlive = trams;
            bool sh = LifeCommon.SmallShadows;
            foreach (var b in carBatches) b.Draw(sh, 0);
            tramBatch.Draw(sh, 0);

            // Fahnen: wehen in Windrichtung, hängen bei Flaute
            int fl = 0;
            float t = Time.time;
            float windYaw = Mathf.Atan2(windX, windZ) * Mathf.Rad2Deg - 90f;
            float flagView = 110f * vs;
            foreach (var f in flags)
            {
                if (!awake[f.Area] || f.Threshold > LifeCommon.Smooth(0.35f, 1f, restoration[f.Area])) continue;
                if ((f.Base - cp).sqrMagnitude > flagView * flagView) continue;
                fl++;
                poleBatch.Add(Matrix4x4.TRS(f.Base, Quaternion.identity, Vector3.one));
                var m = Matrix4x4.TRS(f.Base + Vector3.up * 5.95f, Quaternion.Euler(0, windYaw + Mathf.Sin(t * 0.3f + f.Phase) * 10f, 0), Vector3.one);
                float droop = (1f - Mathf.Clamp01(wind * 1.4f)) * 17f;
                for (int k = 0; k < 4; k++)
                {
                    float wave = Mathf.Sin(t * (3f + wind * 5f) - k * 0.9f + f.Phase) * (6f + wind * 16f) * (0.4f + k * 0.25f);
                    m = m * Matrix4x4.TRS(k == 0 ? Vector3.zero : new Vector3(0.56f, 0, 0), Quaternion.Euler(0, wave, -droop), Vector3.one);
                    clothBatches[f.Variant % clothBatches.Length].Add(m);
                }
            }
            FlagsShown = fl;
            poleBatch.Draw(sh);
            foreach (var b in clothBatches) b.Draw(sh);

            // Hologramme: schwebendes Symbol, Ringe, Lichtsäule; leichtes Flackern
            int ho = 0;
            foreach (var h in holos)
            {
                if (!awake[h.Area] || restoration[h.Area] < 0.55f) continue;
                if ((h.Pos - cp).sqrMagnitude > 200f * 200f) continue;
                ho++;
                float y = h.Pos.y + 10.5f + Mathf.Sin(t * 0.8f + h.Phase) * 0.25f;
                float flick = Mathf.Sin(t * 23f + h.Phase * 5f) > 0.97f ? 0.85f : 1f;
                var top = new Vector3(h.Pos.x, y, h.Pos.z);
                holoIcons[h.Icon].Add(Matrix4x4.TRS(top, Quaternion.Euler(0, t * 35f + h.Phase * 40f, 0), new Vector3(flick, 1f, 1f)));
                holoRing.Add(Matrix4x4.TRS(top + Vector3.down * 1.6f, Quaternion.Euler(0, -t * 20f, 0), Vector3.one));
                holoBeam.Add(Matrix4x4.TRS(h.Pos + Vector3.up * 0.1f, Quaternion.identity, new Vector3(1f, y - 1.6f - h.Pos.y, 1f)));
            }
            HolosShown = ho;
            holoRing.Draw(false); holoBeam.Draw(false);
            foreach (var b in holoIcons.Values) b.Draw(false);

            // Lichthöfe um leuchtende Laternen (nachts, in erwachten Bereichen), zur Kamera gedreht
            int hl = 0;
            var wv = WorldView.I;
            if (dark > 0.25f && wv != null && LifeCommon.Quality >= 1)
            {
                float haloView = 80f * vs;
                for (int a = 0; a < 3; a++)
                {
                    if (!awake[a]) continue;
                    var lamps = wv.LampPositions(a);
                    if (lamps == null) continue;
                    foreach (var lp in lamps)
                    {
                        var d = cp - lp;
                        float d2 = d.sqrMagnitude;
                        if (d2 > haloView * haloView || d2 < 0.25f || hl >= 200) continue;
                        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        float pitch = -Mathf.Atan2(d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) * Mathf.Rad2Deg;
                        float s = (1.8f + restoration[a] * 1.2f) * Mathf.Clamp01((dark - 0.25f) * 2f);
                        haloBatch.Add(Matrix4x4.TRS(lp + Vector3.down * 0.2f, Quaternion.Euler(pitch, yaw + 180f, 0), Vector3.one * s));
                        hl++;
                    }
                }
            }
            HalosShown = hl;
            haloBatch.Draw(false);
        }

        /// <summary>Prüfumgebung: Autos endlich, auf der Fahrbahn, nicht in Gebäuden; Fahnen nicht in Wänden.</summary>
        public int Validate(List<string> problems)
        {
            int bad = 0;
            foreach (var c in cars)
            {
                if (!c.Alive) continue;
                if (!LifeCommon.Finite(c.Pos)) { bad++; if (problems.Count < 6) problems.Add("Stadtfahrzeug mit NaN-Position"); continue; }
                if (LifeCommon.Solid(layout, c.Pos.x, c.Pos.z, 0f, tmp))
                {
                    bad++;
                    string what = "";
                    foreach (var b in tmp) if (b.Solid && b.Contains(c.Pos.x, c.Pos.z, 0f)) what = b.Kind + " (" + b.Cx.ToString("0.0") + "/" + b.Cz.ToString("0.0") + ", Tor " + b.Gate + ")";
                    if (problems.Count < 6) problems.Add("Stadtfahrzeug in " + what + " bei " + c.Pos + ", Abschnitt s " + c.Run.S0.ToString("0") + "…" + c.Run.S1.ToString("0") + " bei s " + c.S.ToString("0.0") + ", Zustand " + c.State);
                }
                if (!LifeCommon.OnRoad(layout, c.Pos.x, c.Pos.z, 1.5f)) { bad++; if (problems.Count < 6) problems.Add("Stadtfahrzeug neben der Straße bei " + c.Pos); }
            }
            foreach (var f in flags)
                if (LifeCommon.Solid(layout, f.Base.x, f.Base.z, 0f, tmp)) { bad++; if (problems.Count < 6) problems.Add("Fahne in einer Wand bei " + f.Base); }
            return bad;
        }

        /// <summary>Gefahrene Strecke aller Stadtfahrzeuge seit dem letzten Aufruf (Prüfumgebung).</summary>
        public float SumS()
        {
            float s = 0f;
            foreach (var c in cars) if (c.Alive) s += c.S * 0.37f + c.Pos.x * 0.11f + c.Pos.z * 0.13f;
            return s;
        }
    }
}
