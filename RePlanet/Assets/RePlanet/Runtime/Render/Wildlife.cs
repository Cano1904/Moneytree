using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Zurückkehrende Tiere: Mit dem Wiederherstellungsgrad eines Bereichs (Sauberkeit, Projekt, Ökologie) kehren
    /// Vögel (Schwärme, die kreisen, landen, auf Dächern und am Boden sitzen und bei Annäherung auffliegen), Bodentiere
    /// (TERRA: Hasen, Füchse · PYRA: Echsen · PELAGIA: Krabben · NIVALIS: Polarfüchse, pinguinartige Vögel) und
    /// Fischschwärme (PELAGIA) zurück. Einfache KI: Umherstreifen um einen festen Heimatplatz, Schwarmverhalten
    /// (Boids), Flucht vor MIKO und Fahrzeugen, nachts und im Sturm verstecken sich die meisten.
    /// Reine Darstellung auf jedem Client: Heimatplätze entstehen deterministisch aus dem Planeten-Seed, die Anzahl
    /// folgt dem replizierten Zustand – Mitspieler sehen dieselben Tiere an denselben Orten (Bewegung nicht synchron).
    /// Zeichnung per GPU-Instancing (je Art und Körperteil ein Stapel), Simulation und Zeichnung nur in Kameranähe,
    /// Anzahl nach Qualitätsstufe begrenzt.
    /// </summary>
    public class Wildlife : MonoBehaviour
    {
        public static Wildlife I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(Wildlife))) GameApp.Components.Add(typeof(Wildlife));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<Wildlife>() == null) GameApp.I.gameObject.AddComponent<Wildlife>();
        }

        const int Ground = 0, Flyer = 1, Swimmer = 2;
        const int GaitHop = 0, GaitTrot = 1, GaitDart = 2, GaitSide = 3, GaitWaddle = 4;
        const int SIdle = 0, SWalk = 1, SFlee = 2;
        const int GOff = 0, GCircle = 1, GLand = 2, GPerch = 3, GLeave = 4;

        class Species
        {
            public string Id;
            public int Kind, Gait;
            public InstanceBatch Body, Extra, ExtraL;
            public Vector3 Pivot;
            public float Size = 1f, Walk = 1f, Run = 5f, Flee = 6f, Threshold = 0.2f, Wander = 10f, View = 70f;
            public int PerArea = 6, Colony = 1, Groups = 2, GroupSize = 6;
            public float Alt = 12f, FlySpeed = 7f;
            public bool Glider, Beach, Nocturnal, Roofs = true;
        }

        class Animal
        {
            public Species Sp; public int Area, Slot;
            public Vector3 Home, Pos, Vel, Target, Offset, Perch;
            public float Yaw, Pitch, Roll, Timer, Phase, Appear, Anim, Flap, FlapTimer, Speed;
            public int State;
            public bool Wanted, Alive, Sim;
            public uint R;
            public float Rand() { R = R * 1664525u + 1013904223u; return (R >> 8) / 16777216f; }
        }

        class Group
        {
            public Species Sp; public int Area, Index;
            public Vector3 Home, Center, Perch;
            public float Angle, Radius, Alt, Timer, Dir = 1f, Appear;
            public int State;
            public bool Wanted, Alive;
            public readonly List<Animal> Members = new List<Animal>();
            public uint R;
            public float Rand() { R = R * 1664525u + 1013904223u; return (R >> 8) / 16777216f; }
        }

        string planet;
        PlanetLayout layout;
        float water;
        int seed;
        readonly List<Species> species = new List<Species>();
        readonly List<Animal> ground = new List<Animal>();
        readonly List<Group> groups = new List<Group>();
        readonly List<Vector3>[] roofPerches = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
        readonly List<Box> tmp = new List<Box>();
        readonly List<Vector4> threats = new List<Vector4>();
        readonly float[] restoration = new float[3];
        float stateTimer, dark, storm;
        Vector3 camPos, camFwd;

        /// <summary>Statistik: lebende Tiere, davon gezeichnet, Instanzen und Zeichenaufrufe im letzten Bild.</summary>
        public int AliveCount { get; private set; }
        public int DrawnCount { get; private set; }
        public int BatchCalls { get; private set; }
        public int SlotCount { get { int n = ground.Count; foreach (var g in groups) n += g.Members.Count; return n; } }
        public string Planet { get { return planet; } }

        void Awake() { I = this; }

        // ================================================================== Arten
        static Material O(Color c) { return Mats.Get(Mats.Opaque, c); }
        static Material[] Set(Color a, Color b, Color c) { return new[] { O(a), O(b), O(c) }; }

        Species Add(string id, int kind, Mesh body, Material[] mats)
        {
            var s = new Species { Id = id, Kind = kind, Body = new InstanceBatch(id, body, mats) };
            species.Add(s);
            return s;
        }

        void AddWings(Species s, Material[] wingMats)
        {
            s.Extra = new InstanceBatch(s.Id + "_fluegelR", AnimalMeshes.Wing(true), wingMats);
            s.ExtraL = new InstanceBatch(s.Id + "_fluegelL", AnimalMeshes.Wing(false), wingMats);
        }

        void DefineSpecies(string p)
        {
            var dark = new Color(0.1f, 0.09f, 0.08f);
            switch (p)
            {
                case "pyra":
                    {
                        var s = Add("echse", Ground, AnimalMeshes.Lizard(), Set(new Color(0.78f, 0.5f, 0.28f), new Color(0.2f, 0.62f, 0.58f), dark));
                        s.Gait = GaitDart; s.Size = 1.25f; s.Walk = 3.4f; s.Run = 7f; s.Flee = 4.5f; s.Wander = 7f; s.PerArea = 12; s.Threshold = 0.18f; s.View = 45f; s.Colony = 1;
                        var f = Add("falke", Flyer, AnimalMeshes.Bird(), Set(new Color(0.42f, 0.28f, 0.2f), new Color(0.85f, 0.75f, 0.6f), new Color(0.95f, 0.75f, 0.2f)));
                        AddWings(f, new[] { O(new Color(0.4f, 0.27f, 0.19f)), O(new Color(0.18f, 0.12f, 0.1f)) });
                        f.Size = 0.95f; f.Groups = 2; f.GroupSize = 2; f.Threshold = 0.3f; f.Alt = 30f; f.FlySpeed = 9f; f.Glider = true; f.Flee = 9f; f.View = 150f; f.Roofs = true;
                        var k = Add("sandfink", Flyer, AnimalMeshes.Bird(), Set(new Color(0.72f, 0.52f, 0.36f), new Color(0.92f, 0.85f, 0.7f), dark));
                        AddWings(k, new[] { O(new Color(0.62f, 0.42f, 0.3f)), O(new Color(0.3f, 0.2f, 0.15f)) });
                        k.Size = 0.27f; k.Groups = 2; k.GroupSize = 6; k.Threshold = 0.12f; k.Alt = 8f; k.FlySpeed = 6f; k.Flee = 6f; k.View = 70f;
                        break;
                    }
                case "pelagia":
                    {
                        var g = Add("moewe", Flyer, AnimalMeshes.Bird(), Set(new Color(0.95f, 0.95f, 0.93f), new Color(1f, 1f, 1f), new Color(0.95f, 0.7f, 0.15f)));
                        AddWings(g, new[] { O(new Color(0.66f, 0.7f, 0.75f)), O(new Color(0.12f, 0.12f, 0.13f)) });
                        g.Size = 0.58f; g.Groups = 3; g.GroupSize = 5; g.Threshold = 0.1f; g.Alt = 11f; g.FlySpeed = 7.5f; g.Glider = true; g.Flee = 7f; g.View = 110f;
                        var c = Add("krabbe", Ground, AnimalMeshes.Crab(), Set(new Color(0.85f, 0.32f, 0.2f), new Color(0.95f, 0.75f, 0.55f), dark));
                        c.Gait = GaitSide; c.Size = 1.1f; c.Walk = 0.9f; c.Run = 3.2f; c.Flee = 3.5f; c.Wander = 5f; c.PerArea = 10; c.Threshold = 0.15f; c.Beach = true; c.View = 40f; c.Colony = 3;
                        var a = Add("fisch_a", Swimmer, AnimalMeshes.Fish(), Set(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.9f, 0.75f), dark));
                        a.Extra = new InstanceBatch("fisch_a_schwanz", AnimalMeshes.FishTail(), new[] { O(new Color(1f, 0.45f, 0.15f)) });
                        a.Pivot = AnimalMeshes.FishTailPivot; a.Size = 0.38f; a.Groups = 3; a.GroupSize = 7; a.Threshold = 0.12f; a.Flee = 5f; a.View = 45f;
                        var bf = Add("fisch_b", Swimmer, AnimalMeshes.Fish(), Set(new Color(0.2f, 0.65f, 0.85f), new Color(0.85f, 0.95f, 1f), dark));
                        bf.Extra = new InstanceBatch("fisch_b_schwanz", AnimalMeshes.FishTail(), new[] { O(new Color(0.95f, 0.85f, 0.25f)) });
                        bf.Pivot = AnimalMeshes.FishTailPivot; bf.Size = 0.32f; bf.Groups = 3; bf.GroupSize = 8; bf.Threshold = 0.3f; bf.Flee = 5f; bf.View = 45f;
                        break;
                    }
                case "nivalis":
                    {
                        var pg = Add("pinguin", Ground, AnimalMeshes.Penguin(), Set(new Color(0.12f, 0.14f, 0.2f), new Color(0.95f, 0.96f, 0.98f), new Color(1f, 0.6f, 0.15f)));
                        pg.Gait = GaitWaddle; pg.Size = 1f; pg.Walk = 0.55f; pg.Run = 3.6f; pg.Flee = 6f; pg.Wander = 6f; pg.PerArea = 12; pg.Colony = 6; pg.Threshold = 0.25f; pg.View = 70f;
                        var fx = Add("polarfuchs", Ground, AnimalMeshes.Fox(), Set(new Color(0.9f, 0.92f, 0.95f), new Color(1f, 1f, 1f), new Color(0.25f, 0.27f, 0.3f)));
                        fx.Extra = new InstanceBatch("polarfuchs_schwanz", AnimalMeshes.FoxTail(), new[] { O(new Color(0.9f, 0.92f, 0.95f)), O(new Color(1f, 1f, 1f)) });
                        fx.Pivot = AnimalMeshes.FoxTailPivot; fx.Gait = GaitTrot; fx.Walk = 1.4f; fx.Run = 7f; fx.Flee = 11f; fx.Wander = 18f; fx.PerArea = 3; fx.Threshold = 0.45f; fx.Nocturnal = true; fx.View = 75f;
                        var sb = Add("schneeammer", Flyer, AnimalMeshes.Bird(), Set(new Color(0.95f, 0.95f, 0.97f), new Color(1f, 1f, 1f), dark));
                        AddWings(sb, new[] { O(new Color(0.85f, 0.87f, 0.9f)), O(new Color(0.15f, 0.15f, 0.17f)) });
                        sb.Size = 0.28f; sb.Groups = 2; sb.GroupSize = 8; sb.Threshold = 0.12f; sb.Alt = 8f; sb.FlySpeed = 6f; sb.Flee = 6f; sb.View = 70f;
                        break;
                    }
                default:
                    {
                        var sp = Add("spatz", Flyer, AnimalMeshes.Bird(), Set(new Color(0.55f, 0.38f, 0.25f), new Color(0.9f, 0.82f, 0.68f), new Color(0.2f, 0.17f, 0.15f)));
                        AddWings(sp, new[] { O(new Color(0.5f, 0.34f, 0.22f)), O(new Color(0.25f, 0.18f, 0.13f)) });
                        sp.Size = 0.3f; sp.Groups = 3; sp.GroupSize = 7; sp.Threshold = 0.1f; sp.Alt = 9f; sp.FlySpeed = 6f; sp.Flee = 6f; sp.View = 80f;
                        var h = Add("hase", Ground, AnimalMeshes.Rabbit(), Set(new Color(0.55f, 0.45f, 0.36f), new Color(0.95f, 0.93f, 0.88f), dark));
                        h.Gait = GaitHop; h.Walk = 1.1f; h.Run = 6.5f; h.Flee = 7f; h.Wander = 9f; h.PerArea = 10; h.Colony = 3; h.Threshold = 0.25f; h.View = 60f;
                        var f = Add("fuchs", Ground, AnimalMeshes.Fox(), Set(new Color(0.86f, 0.45f, 0.17f), new Color(0.97f, 0.95f, 0.9f), new Color(0.13f, 0.1f, 0.09f)));
                        f.Extra = new InstanceBatch("fuchs_schwanz", AnimalMeshes.FoxTail(), new[] { O(new Color(0.86f, 0.45f, 0.17f)), O(new Color(0.97f, 0.95f, 0.9f)) });
                        f.Pivot = AnimalMeshes.FoxTailPivot; f.Gait = GaitTrot; f.Walk = 1.4f; f.Run = 7f; f.Flee = 11f; f.Wander = 18f; f.PerArea = 3; f.Threshold = 0.5f; f.Nocturnal = true; f.View = 75f;
                        break;
                    }
            }
        }

        // ================================================================== Aufbau (deterministisch je Planet)
        static float AreaZ0(int a) { return a == 0 ? -147f : a == 1 ? -46f : 54f; }
        static float AreaZ1(int a) { return a == 0 ? -54f : a == 1 ? 46f : 147f; }

        void Build(string p)
        {
            planet = p;
            species.Clear(); ground.Clear(); groups.Clear();
            foreach (var l in roofPerches) l.Clear();
            layout = WorldGen.Get(p);
            water = Terrain.WaterLevel(p);
            seed = GameData.Planets[p].Seed;
            DefineSpecies(p);
            CollectRoofPerches();
            for (int si = 0; si < species.Count; si++)
            {
                var sp = species[si];
                for (int a = 0; a < 3; a++)
                {
                    if (sp.Kind == Ground)
                    {
                        Vector3 colony = Vector3.zero; bool colonyOk = false;
                        for (int slot = 0; slot < sp.PerArea; slot++)
                        {
                            var rng = new Rng(seed * 7919 + si * 1009 + a * 101 + slot * 13 + 17);
                            if (slot % sp.Colony == 0) colonyOk = FindHome(sp, a, rng, Vector3.zero, false, out colony);
                            if (!colonyOk) continue;
                            Vector3 home;
                            if (sp.Colony > 1 ? !FindHome(sp, a, rng, colony, true, out home) : !FindHome(sp, a, rng, colony, true, out home)) continue;
                            ground.Add(new Animal { Sp = sp, Area = a, Slot = slot, Home = home, Pos = home, Yaw = rng.Range(0f, 360f), R = (uint)(seed * 31 + si * 977 + a * 57 + slot * 7 + 1), Phase = rng.Range(0f, 6.28f) });
                        }
                    }
                    else
                    {
                        for (int gi = 0; gi < sp.Groups; gi++)
                        {
                            var rng = new Rng(seed * 6007 + si * 811 + a * 97 + gi * 29 + 3);
                            Vector3 home;
                            if (!FindHome(sp, a, rng, Vector3.zero, false, out home)) continue;
                            var g = new Group { Sp = sp, Area = a, Index = gi, Home = home, Center = home, Radius = rng.Range(8f, 22f), Angle = rng.Range(0f, 6.28f), Dir = rng.Chance(0.5f) ? 1f : -1f, R = (uint)(seed * 13 + si * 131 + a * 17 + gi * 3 + 5) };
                            for (int m = 0; m < sp.GroupSize; m++)
                            {
                                float ang = m / (float)sp.GroupSize * 6.283f + rng.Range(-0.3f, 0.3f);
                                float rad = sp.Kind == Swimmer ? rng.Range(0.4f, 1.8f) : rng.Range(0.8f, 3.2f);
                                g.Members.Add(new Animal
                                {
                                    Sp = sp, Area = a, Slot = gi * 100 + m, Home = home, Pos = home,
                                    Offset = new Vector3(Mathf.Cos(ang) * rad, rng.Range(-0.8f, 0.8f) * (sp.Kind == Swimmer ? 0.5f : 1f), Mathf.Sin(ang) * rad),
                                    Phase = rng.Range(0f, 6.28f), R = (uint)(seed * 17 + si * 7 + a * 5 + gi * 3 + m * 11 + 9)
                                });
                            }
                            groups.Add(g);
                        }
                    }
                }
            }
            for (int a = 0; a < 3; a++) restoration[a] = -1f;
        }

        /// <summary>Dachkanten intakter Flachdächer (Sitzplätze für Vögel), je Bereich.</summary>
        void CollectRoofPerches()
        {
            var wv = WorldView.I;
            foreach (var b in layout.Colliders)
            {
                if (!b.Solid || b.Gate >= 0 || b.DuneSet >= 0 || b.H < 2.5f || b.H > 36f || b.Hx < 1.5f || b.Hz < 1.5f) continue;
                if (b.Kind == "cliff" || b.Kind == "icewall" || b.Kind == "areawall" || b.Kind == "cityedge") continue;
                if (layout.Base.InBase(b.Cx, b.Cz)) continue;
                int area = PlanetLayout.AreaOf(b.Cz);
                for (int k = 0; k < 3; k++)
                {
                    float x = b.Cx + (k - 1) * b.Hx * 0.6f;
                    float z = b.Cz + ((k * 7 + (int)b.Cx) % 3 - 1) * b.Hz * 0.5f;
                    float top = b.Y0 + b.H;
                    if (wv != null && wv.Planet == planet)
                    {
                        float r = wv.RoofAt(b, x);
                        if (float.IsNaN(r)) continue;
                        top = Mathf.Max(top, r);
                    }
                    float st = LifeCommon.SolidTop(layout, x, z, tmp);
                    if (!float.IsNaN(st)) top = Mathf.Max(top, st);
                    roofPerches[area].Add(new Vector3(x, top, z));
                }
            }
        }

        bool FindHome(Species sp, int area, Rng rng, Vector3 near, bool useNear, out Vector3 home)
        {
            home = Vector3.zero;
            for (int t = 0; t < 60; t++)
            {
                float x, z;
                if (useNear)
                {
                    float ang = rng.Range(0f, 6.283f), rad = rng.Range(0.5f, sp.Colony > 1 ? 3.5f : 1.5f);
                    x = near.x + Mathf.Cos(ang) * rad; z = near.z + Mathf.Sin(ang) * rad;
                    if (t > 20) { useNear = false; continue; }
                }
                else if (t < 30 && sp.Kind != Swimmer && !sp.Beach)
                {
                    // Nähe von Pflanzstellen, Lichtpunkten und dem Projektplatz des Bereichs
                    V3 anchor;
                    int pick = rng.Range(0, 3);
                    if (pick == 0 && layout.Eco.Count > 0) anchor = layout.Eco[rng.Range(0, layout.Eco.Count)].Pos;
                    else if (pick == 1 && layout.Zones.Count > 0) anchor = layout.Zones[rng.Range(0, layout.Zones.Count)].Center;
                    else anchor = layout.ProjectSites[area];
                    float ang = rng.Range(0f, 6.283f), rad = rng.Range(3f, 24f);
                    x = anchor.x + Mathf.Cos(ang) * rad; z = anchor.z + Mathf.Sin(ang) * rad;
                }
                else { x = rng.Range(-138f, 138f); z = rng.Range(AreaZ0(area) + 3f, AreaZ1(area) - 3f); }
                if (PlanetLayout.AreaOf(z) != area || z < AreaZ0(area) || z > AreaZ1(area)) continue;
                bool ok = sp.Kind == Ground ? GroundOk(sp, x, z) : sp.Kind == Swimmer ? WaterOk(x, z, 2.2f) : AirHomeOk(x, z);
                if (!ok) continue;
                home = new Vector3(x, sp.Kind == Swimmer ? water - 1f : Terrain.HeightAt(planet, x, z), z);
                return true;
            }
            return false;
        }

        bool GroundOk(Species sp, float x, float z)
        {
            if (!LifeCommon.InWorld(x, z, 4f)) return false;
            if (layout.Base.InBase(x, z)) return false;
            float h = Terrain.HeightAt(planet, x, z);
            if (water > -50f)
            {
                if (sp.Beach) { if (h < water - 0.45f || h > water + 1.4f) return false; }
                else if (h < water + 0.2f) return false;
            }
            return !LifeCommon.Solid(layout, x, z, 0.25f + 0.35f * sp.Size, tmp);
        }

        bool WaterOk(float x, float z, float depth)
        {
            if (water < -50f || !LifeCommon.InWorld(x, z, 4f)) return false;
            return Terrain.HeightAt(planet, x, z) < water - depth && !LifeCommon.Solid(layout, x, z, 0.8f, tmp);
        }

        bool AirHomeOk(float x, float z)
        {
            if (!LifeCommon.InWorld(x, z, 12f) || layout.Base.InBase(x, z)) return false;
            if (water > -50f && Terrain.HeightAt(planet, x, z) < water + 0.3f) return true; // Möwen über dem Wasser
            return !LifeCommon.Solid(layout, x, z, 0.5f, tmp);
        }

        // ================================================================== Aktualisierung
        void Update()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null || wv.World == null || string.IsNullOrEmpty(wv.Planet)) { AliveCount = DrawnCount = 0; return; }
            if (planet != wv.Planet) Build(wv.Planet);
            var w = wv.World;
            PlanetState ps;
            if (!w.Planets.TryGetValue(planet, out ps)) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var cam = Camera.main;
            if (cam != null) { camPos = cam.transform.position; camFwd = cam.transform.forward; }
            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                stateTimer = 0.5f;
                UpdateWanted(w, ps);
            }
            GatherThreats(w);
            Simulate(dt);
            Render();
        }

        /// <summary>Wie viele Tiere je Art und Bereich gerade da sein wollen (Wiederherstellung, Tageszeit, Sturm, Qualität).</summary>
        void UpdateWanted(WorldState w, PlanetState ps)
        {
            for (int a = 0; a < 3; a++) restoration[a] = LifeCommon.AreaRestoration(w, ps, a);
            dark = Rules.Darkness(Rules.DayPhase(w, planet));
            storm = ps.StormActive ? 1f : 0f;
            float qs = LifeCommon.QualityScale;
            foreach (var sp in species)
            {
                for (int a = 0; a < 3; a++)
                {
                    float f = LifeCommon.Smooth(sp.Threshold, Mathf.Min(1f, sp.Threshold + 0.45f), restoration[a]);
                    float mod = 1f;
                    if (sp.Kind == Ground && !sp.Nocturnal && dark > 0.55f) mod *= 0.3f;
                    if (storm > 0.5f) mod *= sp.Kind == Swimmer ? 1f : sp.Kind == Flyer ? 0.25f : 0.1f;
                    if (sp.Kind == Ground)
                    {
                        int want = Mathf.RoundToInt(sp.PerArea * qs * f * mod);
                        foreach (var an in ground) if (an.Sp == sp && an.Area == a) an.Wanted = an.Slot < want;
                    }
                    else
                    {
                        int want = Mathf.RoundToInt(sp.Groups * Mathf.Max(qs, 0.5f) * f * mod + (f > 0.05f && mod > 0.2f ? 0.49f : 0f));
                        foreach (var g in groups) if (g.Sp == sp && g.Area == a) g.Wanted = g.Index < want;
                    }
                }
            }
        }

        /// <summary>Wovor Tiere fliehen: Roboter aller Spieler (x, y, z, Radiusfaktor) und Fahrzeuge (größerer Radius).</summary>
        void GatherThreats(WorldState w)
        {
            threats.Clear();
            var av = ActorsView.I;
            if (av == null || w == null) return;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online) continue;
                var r = av.RobotOf(p.Id);
                if (r != null && r.gameObject.activeInHierarchy) { var q = r.transform.position; threats.Add(new Vector4(q.x, q.y, q.z, 1f)); }
                else if (p.Vehicle != null) { var q = av.VehiclePos(p.Vehicle); threats.Add(new Vector4(q.x, q.y, q.z, 1.7f)); }
            }
        }

        bool NearestThreat(Vector3 p, float radius, bool flat, out Vector3 at)
        {
            at = Vector3.zero;
            float best = float.MaxValue;
            foreach (var t in threats)
            {
                float dx = p.x - t.x, dz = p.z - t.z, dy = flat ? 0f : p.y - t.y;
                float d2 = dx * dx + dz * dz + dy * dy;
                float r = radius * t.w;
                if (d2 < r * r && d2 < best) { best = d2; at = new Vector3(t.x, t.y, t.z); }
            }
            return best < float.MaxValue;
        }

        void Simulate(float dt)
        {
            float simR = 140f * LifeCommon.ViewScale;
            foreach (var an in ground)
            {
                float dx = an.Pos.x - camPos.x, dz = an.Pos.z - camPos.z;
                an.Sim = dx * dx + dz * dz < simR * simR || threats.Count > 0 && NearestThreat(an.Pos, 60f, true, out _);
                if (!an.Sim && !an.Alive) { if (an.Wanted) { an.Alive = true; an.Appear = 1f; an.Pos = an.Home; } continue; }
                if (!an.Sim) { if (!an.Wanted) { an.Alive = false; an.Appear = 0f; } continue; }
                UpdateGround(an, dt);
            }
            foreach (var g in groups)
            {
                float dx = g.Center.x - camPos.x, dz = g.Center.z - camPos.z;
                bool sim = dx * dx + dz * dz < simR * simR;
                if (g.Sp.Kind == Swimmer) UpdateSchool(g, dt, sim); else UpdateFlock(g, dt, sim);
            }
        }

        // ------------------------------------------------------------------ Bodentiere
        void UpdateGround(Animal an, float dt)
        {
            var sp = an.Sp;
            if (an.Wanted)
            {
                if (!an.Alive) { an.Alive = true; an.Appear = 0f; an.Pos = an.Home; an.State = SIdle; an.Timer = 1f + an.Rand() * 2f; }
                an.Appear = Mathf.MoveTowards(an.Appear, 1f, dt * 0.8f);
            }
            else if (an.Alive)
            {
                an.Appear = Mathf.MoveTowards(an.Appear, 0f, dt * 0.8f);
                if (an.Appear <= 0f) { an.Alive = false; return; }
            }
            if (!an.Alive) return;

            Vector3 threat;
            if (an.State != SFlee && NearestThreat(an.Pos, sp.Flee, true, out threat))
            {
                an.State = SFlee;
                an.Timer = 1.4f + an.Rand() * 1.4f;
                var away = an.Pos - threat; away.y = 0;
                if (away.sqrMagnitude < 1e-4f) away = new Vector3(Mathf.Sin(an.Yaw * Mathf.Deg2Rad), 0, Mathf.Cos(an.Yaw * Mathf.Deg2Rad));
                float jitter = (an.Rand() - 0.5f) * 0.9f;
                var dir = Quaternion.Euler(0, jitter * Mathf.Rad2Deg, 0) * away.normalized;
                an.Target = an.Pos + dir * 30f;
            }
            an.Timer -= dt;
            float speed = 0f;
            switch (an.State)
            {
                case SIdle:
                    if (an.Timer <= 0f)
                    {
                        // neues Ziel in der Nähe des Heimatplatzes
                        for (int t = 0; t < 6; t++)
                        {
                            float ang = an.Rand() * 6.283f, rad = (0.3f + an.Rand() * 0.7f) * sp.Wander;
                            var tg = an.Home + new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad);
                            if (PlanetLayout.AreaOf(tg.z) != an.Area || !GroundOk(sp, tg.x, tg.z)) continue;
                            an.Target = tg; an.State = SWalk;
                            an.Timer = sp.Gait == GaitDart ? 0.25f + an.Rand() * 0.6f : 8f + an.Rand() * 6f;
                            break;
                        }
                        if (an.State == SIdle) an.Timer = 1f + an.Rand() * 2f;
                    }
                    break;
                case SWalk:
                    speed = sp.Walk;
                    if (an.Timer <= 0f || Flat(an.Target - an.Pos).sqrMagnitude < 0.09f) { an.State = SIdle; an.Timer = sp.Gait == GaitDart ? 0.4f + an.Rand() * 1.6f : 1.5f + an.Rand() * 5f; speed = 0f; }
                    break;
                case SFlee:
                    speed = sp.Run;
                    if (an.Timer <= 0f) { an.State = SIdle; an.Timer = 1.5f + an.Rand() * 2f; speed = 0f; }
                    break;
            }
            an.Speed = Mathf.MoveTowards(an.Speed, speed, dt * (an.State == SFlee ? 20f : 6f));
            if (an.Speed > 0.01f)
            {
                var to = Flat(an.Target - an.Pos);
                float targetYaw = LifeCommon.YawOf(to.x, to.z, an.Yaw);
                float moveYaw = targetYaw;
                an.Yaw = LifeCommon.TurnTowards(an.Yaw, sp.Gait == GaitSide ? targetYaw + 90f : targetYaw, dt * (an.State == SFlee ? 720f : 240f));
                float rad = (sp.Gait == GaitSide ? moveYaw : an.Yaw) * Mathf.Deg2Rad;
                var step = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * an.Speed * dt;
                var np = an.Pos + step;
                if (PlanetLayout.AreaOf(np.z) == an.Area && GroundOk(sp, np.x, np.z)) an.Pos = np;
                else
                {
                    // Hindernis: auf der Flucht seitlich ausweichen, sonst stehen bleiben und neu überlegen
                    if (an.State == SFlee)
                    {
                        var side = Quaternion.Euler(0, an.Rand() < 0.5f ? 70f : -70f, 0) * step.normalized;
                        an.Target = an.Pos + side * 20f;
                    }
                    else { an.State = SIdle; an.Timer = 0.5f + an.Rand(); an.Speed = 0f; }
                }
            }
            an.Anim += dt * (an.Speed > 0.05f ? 4f + an.Speed * 2.2f : 1.3f);
            float h = Terrain.HeightAt(planet, an.Pos.x, an.Pos.z);
            an.Pos.y = h;
            Gait(an, h);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        /// <summary>Gangart: Hüpfen, Traben, Huschen, Seitwärtslaufen, Watscheln (und Bauchrutschen auf der Flucht).</summary>
        void Gait(Animal an, float h)
        {
            var sp = an.Sp;
            float moving = Mathf.Clamp01(an.Speed / Mathf.Max(0.1f, sp.Walk));
            an.Pitch = 0f; an.Roll = 0f;
            switch (sp.Gait)
            {
                case GaitHop:
                    {
                        float s = Mathf.Abs(Mathf.Sin(an.Anim * 1.2f));
                        an.Pos.y = h + s * (0.12f + 0.1f * Mathf.Clamp01(an.Speed / 4f)) * moving;
                        an.Pitch = (Mathf.Cos(an.Anim * 1.2f) * 12f) * moving;
                        if (an.Speed < 0.05f) an.Pitch = Mathf.Sin(an.Anim * 0.7f + an.Phase) > 0.6f ? 10f : 0f; // schnuppern
                        break;
                    }
                case GaitTrot:
                    an.Pos.y = h + Mathf.Abs(Mathf.Sin(an.Anim * 1.6f)) * 0.03f * moving;
                    if (an.Speed < 0.05f) an.Pitch = Mathf.Sin(an.Anim * 0.5f + an.Phase) > 0.5f ? 18f : 0f; // schnuppert am Boden
                    break;
                case GaitDart:
                    an.Roll = 0f;
                    an.Pitch = an.Speed < 0.05f ? -6f : 0f; // hebt den Kopf in der Pause
                    break;
                case GaitSide:
                    an.Pos.y = h + Mathf.Abs(Mathf.Sin(an.Anim * 3f)) * 0.015f * moving;
                    break;
                case GaitWaddle:
                    if (an.State == SFlee && an.Speed > sp.Walk * 1.5f) { an.Pitch = 72f; an.Pos.y = h + 0.12f; } // Bauchrutschen
                    else { an.Roll = Mathf.Sin(an.Anim * 1.4f) * (4f + 9f * moving); an.Pos.y = h + Mathf.Abs(Mathf.Sin(an.Anim * 1.4f)) * 0.02f; }
                    break;
            }
        }

        // ------------------------------------------------------------------ Vogelschwärme
        void UpdateFlock(Group g, float dt, bool sim)
        {
            var sp = g.Sp;
            if (g.Wanted && !g.Alive)
            {
                g.Alive = true; g.State = GCircle; g.Appear = 0f; g.Timer = 12f + g.Rand() * 20f;
                g.Alt = sp.Alt * (0.8f + g.Rand() * 0.5f);
                g.Center = g.Home + Vector3.up * (g.Alt + 12f);
                foreach (var m in g.Members) { m.Alive = true; m.Pos = g.Center + m.Offset; m.Vel = Vector3.zero; m.Appear = 0f; }
            }
            if (!g.Alive) return;
            if (!g.Wanted && g.State != GLeave) { g.State = GLeave; g.Timer = 6f; }
            g.Appear = Mathf.MoveTowards(g.Appear, g.State == GLeave ? 0f : 1f, dt * (g.State == GLeave ? 0.25f : 0.6f));
            if (g.State == GLeave && g.Appear <= 0f)
            {
                g.Alive = false; g.State = GOff;
                foreach (var m in g.Members) m.Alive = false;
                return;
            }
            if (!sim) { foreach (var m in g.Members) { m.Appear = g.Appear; m.Sim = false; } return; }

            g.Timer -= dt;
            bool nightRest = dark > 0.6f || storm > 0.5f;
            // Zustandswechsel des Schwarms
            switch (g.State)
            {
                case GCircle:
                    if (g.Timer <= 0f || nightRest)
                    {
                        if (ChoosePerch(g)) { g.State = GLand; g.Timer = 14f; }
                        else g.Timer = 8f + g.Rand() * 10f;
                    }
                    break;
                case GLand:
                    {
                        bool all = true;
                        foreach (var m in g.Members) if (m.State != 1) { all = false; break; }
                        if (all || g.Timer <= 0f) { g.State = GPerch; g.Timer = nightRest ? 30f : 10f + g.Rand() * 18f; foreach (var m in g.Members) { m.State = 1; m.Vel = Vector3.zero; } }
                        break;
                    }
                case GPerch:
                    {
                        Vector3 th;
                        bool scared = NearestThreat(g.Perch, sp.Flee + 2f, true, out th);
                        if (scared || (g.Timer <= 0f && !nightRest))
                        {
                            g.State = GCircle; g.Timer = scared ? 6f + g.Rand() * 4f : 14f + g.Rand() * 22f;
                            g.Alt = sp.Alt * (0.8f + g.Rand() * 0.5f);
                            foreach (var m in g.Members) { m.State = 0; m.Vel = Vector3.up * (2.5f + m.Rand() * 2f) + (m.Pos - th).normalized * (scared ? 3f : 0.5f); m.FlapTimer = 1.2f; }
                            if (scared) g.Angle += 1.5f * g.Dir;
                        }
                        break;
                    }
            }
            // Mittelpunkt des Schwarms: Kreis um den Heimatplatz, sicher über Gelände und Dächern
            float angSpeed = sp.FlySpeed / Mathf.Max(6f, g.Radius) * g.Dir;
            g.Angle += angSpeed * dt;
            var ring = g.Home + new Vector3(Mathf.Cos(g.Angle) * g.Radius, 0, Mathf.Sin(g.Angle) * g.Radius);
            float floor = Mathf.Max(Terrain.HeightAt(planet, ring.x, ring.z), water > -50f ? water : -1000f);
            float top = LifeCommon.SolidTop(layout, ring.x, ring.z, tmp);
            if (!float.IsNaN(top)) floor = Mathf.Max(floor, top + 2f);
            float wantY = floor + g.Alt + (g.State == GLeave ? 30f : 0f);
            ring.y = Mathf.Lerp(g.Center.y, wantY, Mathf.Clamp01(dt * 0.8f));
            if (g.State == GLeave) ring += new Vector3(Mathf.Cos(g.Angle), 0, Mathf.Sin(g.Angle)) * 10f;
            g.Center = ring;
            var tangent = new Vector3(-Mathf.Sin(g.Angle), 0, Mathf.Cos(g.Angle)) * g.Dir;
            foreach (var m in g.Members) { m.Sim = true; m.Appear = g.Appear; UpdateBird(g, m, tangent, dt); }
        }

        bool ChoosePerch(Group g)
        {
            var sp = g.Sp;
            // Dächer (falls vorhanden und erwünscht) oder Boden in der Nähe des Heimatplatzes
            var roofs = roofPerches[g.Area];
            if (sp.Roofs && roofs.Count > 0 && g.Rand() < 0.55f)
            {
                for (int t = 0; t < 6; t++)
                {
                    var r = roofs[(int)(g.Rand() * roofs.Count) % roofs.Count];
                    if (Flat(r - g.Home).magnitude < 45f) { g.Perch = r; return true; }
                }
            }
            for (int t = 0; t < 10; t++)
            {
                float ang = g.Rand() * 6.283f, rad = 3f + g.Rand() * 16f;
                float x = g.Home.x + Mathf.Cos(ang) * rad, z = g.Home.z + Mathf.Sin(ang) * rad;
                if (!LifeCommon.InWorld(x, z, 5f) || layout.Base.InBase(x, z) || PlanetLayout.AreaOf(z) != g.Area) continue;
                float h = Terrain.HeightAt(planet, x, z);
                if (water > -50f && h < water + 0.2f) continue;
                if (LifeCommon.Solid(layout, x, z, 2.5f, tmp)) continue;
                g.Perch = new Vector3(x, h, z);
                return true;
            }
            return false;
        }

        void UpdateBird(Group g, Animal m, Vector3 tangent, float dt)
        {
            var sp = g.Sp;
            float foot = AnimalMeshes.BirdFoot * sp.Size;
            if (g.State == GPerch && m.State == 1)
            {
                // Sitzen: picken, umschauen, kleine Hüpfer
                m.Timer -= dt;
                if (m.Timer <= 0f) { m.Timer = 0.6f + m.Rand() * 2.2f; m.Target = new Vector3((m.Rand() - 0.5f) * 40f, 0, 0); m.Anim = m.Rand() < 0.4f ? 0.35f : 0f; }
                m.Yaw = LifeCommon.TurnTowards(m.Yaw, m.Yaw + m.Target.x, dt * 180f);
                m.Anim = Mathf.Max(0f, m.Anim - dt);
                m.Pitch = m.Anim > 0f ? 35f * Mathf.Sin(m.Anim / 0.35f * Mathf.PI) : 0f;
                m.Roll = 0f; m.Flap = 0f;
                return;
            }
            Vector3 desired;
            if (g.State == GLand || (g.State == GPerch && m.State != 1))
            {
                var spot = PerchSpot(g, m);
                var flat = Flat(spot - m.Pos);
                desired = spot + Vector3.up * Mathf.Min(18f, flat.magnitude * 0.7f);
                if (flat.magnitude < 0.35f && Mathf.Abs(m.Pos.y - spot.y) < 0.4f) { m.State = 1; m.Pos = spot; m.Vel = Vector3.zero; m.Pitch = 0f; m.Roll = 0f; return; }
                if (flat.magnitude < 2.5f) desired = spot;
            }
            else
            {
                var rot = Quaternion.Euler(0, g.Angle * Mathf.Rad2Deg * 0.5f, 0);
                desired = g.Center + rot * m.Offset + Vector3.up * Mathf.Sin(Time.time * 0.7f + m.Phase) * 0.6f;
            }
            // Boids: Anziehung zum Wunschpunkt, Dämpfung, Abstand zu Nachbarn
            var acc = (desired - m.Pos) * 1.6f - m.Vel * 0.9f;
            foreach (var o in g.Members)
            {
                if (o == m) continue;
                var d = m.Pos - o.Pos;
                float d2 = d.sqrMagnitude;
                if (d2 < 1.2f && d2 > 1e-5f) acc += d / d2 * 1.2f;
            }
            m.Vel += acc * dt;
            float maxV = sp.FlySpeed * 1.7f;
            if (m.Vel.sqrMagnitude > maxV * maxV) m.Vel = m.Vel.normalized * maxV;
            var np = m.Pos + m.Vel * dt;
            // nie in Gebäude oder unter das Gelände
            float gy = Terrain.HeightAt(planet, np.x, np.z);
            float st = LifeCommon.SolidTop(layout, np.x, np.z, tmp);
            float minY = Mathf.Max(gy + foot, float.IsNaN(st) ? -1000f : st + foot);
            if (water > -50f) minY = Mathf.Max(minY, water + 0.3f);
            if (np.y < minY) { np.y = minY; if (m.Vel.y < 0) m.Vel.y = 0; }
            if (!LifeCommon.Finite(np)) np = g.Center;
            m.Pos = np;
            var hv = Flat(m.Vel);
            float spd = hv.magnitude;
            float newYaw = LifeCommon.YawOf(hv.x, hv.z, m.Yaw);
            float turn = Mathf.DeltaAngle(m.Yaw, newYaw);
            m.Yaw = LifeCommon.TurnTowards(m.Yaw, newYaw, dt * 360f);
            m.Pitch = Mathf.Clamp(-Mathf.Atan2(m.Vel.y, Mathf.Max(0.5f, spd)) * Mathf.Rad2Deg, -35f, 35f);
            m.Roll = Mathf.Lerp(m.Roll, Mathf.Clamp(-turn * 2.5f, -40f, 40f), dt * 4f);
            // Flügelschlag: steigen, langsam oder Böen; Segler gleiten meist
            m.FlapTimer -= dt;
            bool flapping = m.Vel.y > 0.4f || spd < sp.FlySpeed * 0.6f || m.FlapTimer > 0f;
            if (!flapping && m.Rand() < dt * (sp.Glider ? 0.08f : 0.5f)) m.FlapTimer = 0.6f + m.Rand() * 0.8f;
            if (flapping)
            {
                float hz = sp.Size < 0.4f ? 14f : sp.Glider ? 4.5f : 8f;
                m.Phase += dt * hz;
                m.Flap = Mathf.Sin(m.Phase) * (sp.Glider ? 32f : 48f);
            }
            else m.Flap = Mathf.Lerp(m.Flap, sp.Glider ? 6f : 10f, dt * 5f);
        }

        Vector3 PerchSpot(Group g, Animal m)
        {
            var o = new Vector3(m.Offset.x, 0, m.Offset.z) * 0.55f;
            var p = g.Perch + o;
            float foot = AnimalMeshes.BirdFoot * g.Sp.Size;
            float st = LifeCommon.SolidTop(layout, p.x, p.z, tmp);
            float gy = Terrain.HeightAt(planet, p.x, p.z);
            if (!float.IsNaN(st)) p.y = Mathf.Max(st, gy) + foot;
            else if (g.Perch.y > gy + 1f) { p = g.Perch; p.y += foot; } // Dachkante: nicht daneben in die Luft setzen
            else p.y = gy + foot;
            return p;
        }

        // ------------------------------------------------------------------ Fischschwärme
        void UpdateSchool(Group g, float dt, bool sim)
        {
            var sp = g.Sp;
            if (g.Wanted && !g.Alive)
            {
                g.Alive = true; g.State = GCircle; g.Appear = 0f; g.Center = g.Home; g.Perch = g.Home; g.Timer = 0f;
                foreach (var m in g.Members) { m.Alive = true; m.Pos = g.Center + m.Offset; m.Vel = Vector3.zero; m.Yaw = g.Rand() * 360f; }
            }
            if (!g.Alive) return;
            g.Appear = Mathf.MoveTowards(g.Appear, g.Wanted ? 1f : 0f, dt * 0.5f);
            if (!g.Wanted && g.Appear <= 0f) { g.Alive = false; foreach (var m in g.Members) m.Alive = false; return; }
            if (!sim) { foreach (var m in g.Members) { m.Appear = g.Appear; m.Sim = false; } return; }
            // Schwarmziel: zufällige tiefe Stelle nahe dem Heimatplatz; Flucht vor tauchenden Robotern
            g.Timer -= dt;
            Vector3 th;
            bool flee = NearestThreat(g.Center, sp.Flee, false, out th);
            if (flee)
            {
                var away = Flat(g.Center - th).normalized;
                var tg = g.Center + away * 10f;
                if (WaterOk(tg.x, tg.z, 1.8f)) { g.Perch = tg; g.Timer = 3f; }
            }
            if (g.Timer <= 0f || Flat(g.Perch - g.Center).sqrMagnitude < 1f)
            {
                for (int t = 0; t < 6; t++)
                {
                    float ang = g.Rand() * 6.283f, rad = 3f + g.Rand() * 14f;
                    var tg = g.Home + new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad);
                    if (!WaterOk(tg.x, tg.z, 1.8f)) continue;
                    g.Perch = tg; break;
                }
                g.Timer = 5f + g.Rand() * 8f;
            }
            float speed = flee ? 4.5f : 1.3f;
            var to = Flat(g.Perch - g.Center);
            if (to.sqrMagnitude > 1e-4f) g.Center += to.normalized * Mathf.Min(to.magnitude, speed * dt);
            float bed = Terrain.HeightAt(planet, g.Center.x, g.Center.z);
            g.Center.y = Mathf.Lerp(bed + 0.8f, water - 0.8f, 0.45f + 0.25f * Mathf.Sin(Time.time * 0.2f + g.Index));
            foreach (var m in g.Members)
            {
                m.Sim = true; m.Appear = g.Appear;
                var rot = Quaternion.Euler(0, Time.time * 20f * (g.Dir) + m.Phase * 30f, 0);
                var desired = g.Center + rot * m.Offset;
                var acc = (desired - m.Pos) * 2.2f - m.Vel * 1.2f;
                m.Vel += acc * dt;
                if (m.Vel.sqrMagnitude > 36f) m.Vel = m.Vel.normalized * 6f;
                var np = m.Pos + m.Vel * dt;
                float b = Terrain.HeightAt(planet, np.x, np.z);
                np.y = Mathf.Clamp(np.y, b + 0.25f, water - 0.35f);
                if (b > water - 0.7f) np = m.Pos; // nicht ins Flache
                if (!LifeCommon.Finite(np)) np = g.Center;
                m.Pos = np;
                var hv = Flat(m.Vel);
                m.Yaw = LifeCommon.TurnTowards(m.Yaw, LifeCommon.YawOf(hv.x, hv.z, m.Yaw), dt * 240f);
                m.Phase += dt * (6f + hv.magnitude * 4f);
                m.Flap = Mathf.Sin(m.Phase) * 25f;
                m.Pitch = Mathf.Clamp(-m.Vel.y * 10f, -20f, 20f);
            }
        }

        // ================================================================== Zeichnen
        void Render()
        {
            foreach (var sp in species) { sp.Body.Clear(); if (sp.Extra != null) sp.Extra.Clear(); if (sp.ExtraL != null) sp.ExtraL.Clear(); }
            int alive = 0, drawn = 0;
            float vs = LifeCommon.ViewScale;
            foreach (var an in ground)
            {
                if (!an.Alive) continue;
                alive++;
                if (!Visible(an.Pos, an.Sp.View * vs)) continue;
                drawn++;
                var sp = an.Sp;
                var mx = Matrix4x4.TRS(an.Pos, Quaternion.Euler(an.Pitch, an.Yaw, an.Roll), Vector3.one * (sp.Size * Mathf.Max(0.01f, an.Appear)));
                sp.Body.Add(mx);
                if (sp.Extra != null)
                {
                    float sway = Mathf.Sin(an.Anim * (an.Speed > 0.05f ? 1.6f : 0.6f) + an.Phase) * (an.Speed > 0.05f ? 14f : 22f);
                    sp.Extra.Add(mx * Matrix4x4.TRS(sp.Pivot, Quaternion.Euler(an.State == SFlee ? -25f : 0f, sway, 0), Vector3.one));
                }
            }
            foreach (var g in groups)
            {
                if (!g.Alive) continue;
                var sp = g.Sp;
                foreach (var m in g.Members)
                {
                    if (!m.Alive) continue;
                    alive++;
                    if (!Visible(m.Pos, sp.View * vs)) continue;
                    drawn++;
                    var mx = Matrix4x4.TRS(m.Pos, Quaternion.Euler(m.Pitch, m.Yaw, m.Roll), Vector3.one * (sp.Size * Mathf.Max(0.01f, m.Appear)));
                    sp.Body.Add(mx);
                    if (sp.Kind == Flyer)
                    {
                        bool perched = g.State == GPerch && m.State == 1;
                        var pr = AnimalMeshes.WingPivot; var pl = new Vector3(-pr.x, pr.y, pr.z);
                        if (perched)
                        {
                            sp.Extra.Add(mx * Matrix4x4.TRS(pr, Quaternion.Euler(0, 96f, -12f), new Vector3(0.55f, 1f, 0.8f)));
                            sp.ExtraL.Add(mx * Matrix4x4.TRS(pl, Quaternion.Euler(0, -96f, 12f), new Vector3(0.55f, 1f, 0.8f)));
                        }
                        else
                        {
                            sp.Extra.Add(mx * Matrix4x4.TRS(pr, Quaternion.Euler(0, 0, m.Flap), Vector3.one));
                            sp.ExtraL.Add(mx * Matrix4x4.TRS(pl, Quaternion.Euler(0, 0, -m.Flap), Vector3.one));
                        }
                    }
                    else if (sp.Extra != null) sp.Extra.Add(mx * Matrix4x4.TRS(sp.Pivot, Quaternion.Euler(0, m.Flap, 0), Vector3.one));
                }
            }
            AliveCount = alive; DrawnCount = drawn;
            int calls = 0;
            bool sh = LifeCommon.SmallShadows;
            foreach (var sp in species)
            {
                bool shadows = sh && sp.Kind != Swimmer;
                calls += Calls(sp.Body); sp.Body.Draw(shadows, 1);
                if (sp.Extra != null) { calls += Calls(sp.Extra); sp.Extra.Draw(shadows && sp.Kind == Ground, 1); }
                if (sp.ExtraL != null) { calls += Calls(sp.ExtraL); sp.ExtraL.Draw(false); }
            }
            BatchCalls = calls;
        }

        static int Calls(InstanceBatch b) { return b.Count == 0 ? 0 : ((b.Count + 1022) / 1023) * b.Mesh.subMeshCount; }

        bool Visible(Vector3 p, float view)
        {
            var d = p - camPos;
            float d2 = d.sqrMagnitude;
            if (d2 > view * view) return false;
            if (d2 > 64f && Vector3.Dot(d, camFwd) < -0.2f * Mathf.Sqrt(d2)) return false; // hinter der Kamera
            return true;
        }

        // ================================================================== Abfragen (MIKOs Neugier, Prüfumgebung)
        /// <summary>Nächstes sichtbares Tier (am Boden oder sitzend) im Umkreis – MIKO schaut neugierig hin.</summary>
        public bool NearestAnimal(Vector3 p, float radius, out Vector3 at)
        {
            at = Vector3.zero;
            float best = radius * radius;
            bool found = false;
            foreach (var an in ground)
            {
                if (!an.Alive || an.Appear < 0.5f) continue;
                float d2 = (an.Pos - p).sqrMagnitude;
                if (d2 < best) { best = d2; at = an.Pos; found = true; }
            }
            foreach (var g in groups)
            {
                if (!g.Alive) continue;
                foreach (var m in g.Members)
                {
                    if (!m.Alive || m.Appear < 0.5f) continue;
                    float d2 = (m.Pos - p).sqrMagnitude;
                    if (d2 < best) { best = d2; at = m.Pos; found = true; }
                }
            }
            return found;
        }

        /// <summary>
        /// Plausibilitätsprüfung aller lebenden Tiere: endliche Positionen, Bodentiere nicht in Wänden und nicht unter
        /// dem Gelände, Vögel nicht in Gebäuden, Fische im Wasser. Liefert die Anzahl der Verstöße (Beispiele in <paramref name="problems"/>).
        /// </summary>
        public int Validate(List<string> problems)
        {
            int bad = 0;
            void Bad(string s) { bad++; if (problems != null && problems.Count < 6) problems.Add(s); }
            foreach (var an in ground)
            {
                if (!an.Alive) continue;
                if (!LifeCommon.Finite(an.Pos)) { Bad(an.Sp.Id + ": Position NaN"); continue; }
                float h = Terrain.HeightAt(planet, an.Pos.x, an.Pos.z);
                if (an.Pos.y < h - 0.05f) Bad(an.Sp.Id + " unter dem Gelände bei " + an.Pos);
                if (LifeCommon.Solid(layout, an.Pos.x, an.Pos.z, 0f, tmp)) Bad(an.Sp.Id + " in einer Wand bei " + an.Pos);
                if (water > -50f && !an.Sp.Beach && h < water) Bad(an.Sp.Id + " im Wasser bei " + an.Pos);
            }
            foreach (var g in groups)
            {
                if (!g.Alive) continue;
                foreach (var m in g.Members)
                {
                    if (!m.Alive) continue;
                    if (!LifeCommon.Finite(m.Pos)) { Bad(g.Sp.Id + ": Position NaN"); continue; }
                    float h = Terrain.HeightAt(planet, m.Pos.x, m.Pos.z);
                    if (g.Sp.Kind == Swimmer)
                    {
                        if (m.Pos.y > water - 0.1f || m.Pos.y < h - 0.05f) Bad(g.Sp.Id + " außerhalb des Wassers bei " + m.Pos + " (Boden " + h.ToString("0.00") + ")");
                        continue;
                    }
                    if (m.Pos.y < h - 0.05f) Bad(g.Sp.Id + " unter dem Gelände bei " + m.Pos);
                    float st = LifeCommon.SolidTop(layout, m.Pos.x, m.Pos.z, tmp);
                    if (!float.IsNaN(st) && m.Pos.y < st - 0.05f) Bad(g.Sp.Id + " in einem Gebäude bei " + m.Pos + " (Oberkante " + st.ToString("0.0") + ")");
                }
            }
            return bad;
        }

        /// <summary>Lebende Tiere je Art (Prüfumgebung, Statistik).</summary>
        public Dictionary<string, int> CountBySpecies()
        {
            var d = new Dictionary<string, int>();
            foreach (var sp in species) d[sp.Id] = 0;
            foreach (var an in ground) if (an.Alive) d[an.Sp.Id]++;
            foreach (var g in groups) if (g.Alive) foreach (var m in g.Members) if (m.Alive) d[g.Sp.Id]++;
            return d;
        }

        /// <summary>Wiederherstellungsgrad der Bereiche, wie ihn die Tiere zuletzt gesehen haben.</summary>
        public float Restoration(int area) { return area >= 0 && area < 3 ? restoration[area] : 0f; }
    }
}
