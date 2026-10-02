using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RePlanet;
using RePlanet.Core;
using UnityEngine;

public static class Program
{
    class Tri { public Vector3 A, B, C; public Color Col; }

    internal static readonly List<(Mesh mesh, Matrix4x4 m, Material[] mats)> scene = new List<(Mesh, Matrix4x4, Material[])>();

    /// <summary>Anzahl der Dreiecke, deren Vorderseite (Cross(b−a, c−a)) von den Ecken-Normalen weg zeigt.</summary>
    static (int bad, int total) CheckMesh(Mesh m)
    {
        int bad = 0, total = 0;
        if (m.N.Count != m.V.Count) return (-1, 0);
        for (int s = 0; s < m.subMeshCount; s++)
        {
            var t = m.T[s];
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                var a = m.V[t[k]]; var b = m.V[t[k + 1]]; var c = m.V[t[k + 2]];
                var cr = Vector3.Cross(b - a, c - a);
                if (cr.magnitude < 1e-4f * (b - a).magnitude * (c - a).magnitude + 1e-9f) continue; // entartet (Pol/Spitze)
                total++;
                var n = m.N[t[k]] + m.N[t[k + 1]] + m.N[t[k + 2]];
                if (Vector3.Dot(cr, n) < 0) bad++;
            }
        }
        return (bad, total);
    }

    static void CheckPrimitives()
    {
        var prims = new Dictionary<string, Action<MeshBuilder>>
        {
            { "Box", b => b.Box(new Vector3(1, 2, 3), new Vector3(1, 2, 3)) },
            { "BoxNoBottom", b => b.BoxNoBottom(Vector3.zero, new Vector3(1, 2, 3)) },
            { "BoxRot", b => b.BoxRot(Vector3.zero, new Vector3(1, 2, 3), new Vector3(20, 40, 60)) },
            { "BoxJ", b => b.BoxJ(Vector3.zero, new Vector3(1, 2, 3), new Vector3(20, 40, 60), 0.2f, 7) },
            { "Beam", b => b.Beam(new Vector3(0, 0, 0), new Vector3(1, 3, 2), 0.2f) },
            { "Tube", b => b.Tube(new Vector3(0, 0, 0), new Vector3(1, 3, 2), 0.2f, 6, true, 0.1f) },
            { "Prism", b => b.Prism(Vector3.zero, new Vector3(4, 2, 3)) },
            { "Cylinder", b => b.Cylinder(Vector3.zero, 1, 2, 12, true, 0.5f) },
            { "Kegel", b => b.Cylinder(Vector3.zero, 1, 2, 12, true, 0f) },
            { "CylinderX", b => b.CylinderX(Vector3.zero, 1, 2, 12) },
            { "CylinderZ", b => b.CylinderZ(Vector3.zero, 1, 2, 12) },
            { "Disc(oben)", b => b.Disc(Vector3.zero, 1, 12, true) },
            { "Disc(unten)", b => b.Disc(Vector3.zero, 1, 12, false) },
            { "Sphere", b => b.Sphere(Vector3.zero, 1, 12, 8, 0.7f) },
            { "Crumple", b => b.Crumple(Vector3.zero, 1, 0.6f, 3, 0.25f, 8, 5) },
            { "Lathe", b => b.Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(1, 0.2f), new Vector2(1.2f, 1), new Vector2(0.5f, 2), new Vector2(0, 2.1f) }, 12) },
            { "Torus", b => b.Torus(Vector3.zero, 2, 0.4f, 14, 6) },
            { "TorusRot", b => b.TorusRot(Vector3.zero, new Vector3(90, 30, 0), 2, 0.4f, 14, 6) },
            { "Blob", b => b.Blob(Vector3.zero, 2, 1, 12, 4, 5, 0.3f) },
            { "Face", b => b.Face(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0), Vector3.back) },
            { "Face(umgekehrt)", b => b.Face(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0), Vector3.back) },
            { "TriFace", b => b.TriFace(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), Vector3.forward) },
        };
        foreach (var kv in prims)
        {
            foreach (var withM in new[] { false, true })
            {
                var b = new MeshBuilder();
                if (withM) b.M = Matrix4x4.TRS(new Vector3(3, 1, -2), Quaternion.Euler(30, 70, 10), new Vector3(1.5f, 0.7f, 1.2f));
                kv.Value(b);
                var r = CheckMesh(b.Build(kv.Key));
                Console.WriteLine($"  {kv.Key,-16} {(withM ? "mit Matrix" : "lokal     ")}: {r.bad,4} von {r.total,4} Dreiecken verkehrt");
            }
        }
    }

    public static int Main(string[] args)
    {
        // RP_EIGENE_SHADER=1: eigene Shader gelten als unterstützt UND der (standardmäßig abgeschaltete) Oberflächen-Shader ist erlaubt
        SurfaceLook.ForceDetailShaders = Environment.GetEnvironmentVariable("RP_EIGENE_SHADER") == "1";
        // Laufzeitprüfung (Intro, Spiel auf allen Planeten, Abspann): dotnet run -- run [intro|game|ending|all]
        if (args.Length > 0 && args[0] == "run") return Checks.Main(args.Length > 1 ? args[1] : "all");
        // Bilder aus dem laufenden Spiel (alle Figuren, Anlagen, Instanzen): dotnet run -- shots <Ordner> [Filter]
        if (args.Length > 0 && args[0] == "shots") return Checks.Shots(args.Length > 1 ? args[1] : "shots", args.Length > 2 ? args[2] : null);
        if (args.Length > 0 && args[0] == "check")
        {
            Console.WriteLine("Primitive:");
            CheckPrimitives();
            return 0;
        }
        string planet = args.Length > 0 ? args[0] : "terra";
        string outDir = args.Length > 1 ? args[1] : ".";
        string state = args.Length > 2 ? args[2] : "dirty";
        Directory.CreateDirectory(outDir);
        var app = (GameApp)null;
        // Menüpfad: WorldView nutzt die Vorschauwelt (GameApp.I bleibt null)
        var cam = new GameObject("Cam").AddComponent<Camera>();
        Camera.main = cam;
        var wvGo = new GameObject("World");
        var wv = wvGo.AddComponent<WorldView>();
        typeof(WorldView).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(wv, null);
        var world = Game.NewWorld("Vorschau", GameData.Planets[planet].StartPlanet ? planet : "terra");
        world.CurrentPlanet = planet;
        world.PlayTime = 0.55f * GameData.Planets[planet].DayLength;
        if (state == "clean")
        {
            var ps = world.Planet(planet);
            var l = WorldGen.Get(planet);
            for (int a = 0; a < 3; a++) ps.RemovedWeight[a] = l.AreaWeight[a];
            foreach (var p in ps.Projects.Values) p.Done = true;
            world.PlayTime += 10000;
            foreach (var e in l.Eco) ps.Eco[e.Id] = 0;
        }
        typeof(WorldView).GetField("menuWorld", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wv, world);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        wv.Build(planet, true);
        Console.WriteLine("Aufbau: " + sw.ElapsedMilliseconds + " ms");
        // Szene einsammeln
        int verts = 0, tris = 0, renderers = 0, subDraws = 0;
        void Walk(Transform t, bool active)
        {
            active = active && t.gameObject.activeSelf;
            var mf = t.gameObject.GetComponent<MeshFilter>(); var mr = t.gameObject.GetComponent<MeshRenderer>();
            if (active && mf != null && mr != null && mf.sharedMesh != null)
            {
                var sm = mr.sharedMaterials;
                // Gelände: eigener Shader fehlt in der Prüfumgebung → mit der gemalten Geländetextur zeichnen
                if ((sm.Length == 0 || sm[0] == null) && (mf.sharedMesh.name ?? "").StartsWith("terrain"))
                    sm = new[] { new Material((Shader)null) { name = "Gelände", mainTexture = typeof(WorldView).GetField("terrainTex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wv) } };
                scene.Add((mf.sharedMesh, t.localToWorldMatrix, sm));
                renderers++; verts += mf.sharedMesh.V.Count;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) { tris += mf.sharedMesh.T[s].Count / 3; if (mf.sharedMesh.T[s].Count > 0) subDraws++; }
            }
            foreach (var c in t.children) Walk(c, active);
        }
        Walk(wv.Root, true);
        // Aufschlüsselung nach Gruppen (erste Ebene unter Root)
        var groups = new SortedDictionary<string, int[]>();
        foreach (var ch in wv.Root.children)
        {
            string key = ch.gameObject.name.Split('_')[0];
            if (key.StartsWith("Eco")) key = "Eco"; if (key.StartsWith("Lore")) key = "Lore"; if (key.StartsWith("ZoneLamp")) key = "ZoneLamp";
            if (!groups.ContainsKey(key)) groups[key] = new int[3];
            foreach (var mf in ch.gameObject.GetComponentsInChildren<MeshFilter>(true))
            {
                groups[key][0]++;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) if (mf.sharedMesh.T[s].Count > 0) groups[key][1]++;
                groups[key][2] += mf.sharedMesh.V.Count;
            }
        }
        foreach (var kv in groups) if (kv.Value[1] > 8) Console.WriteLine($"  Gruppe {kv.Key,-22} {kv.Value[0],4} Renderer {kv.Value[1],5} Draws {kv.Value[2],8} Ecken");
        Console.WriteLine($"Statisch: {renderers} Renderer, {subDraws} Untermesh-Draws, {verts} Ecken, {tris} Dreiecke");
        { var mats = new HashSet<Material>(); foreach (var e in scene) foreach (var m in e.mats) if (m != null) mats.Add(m); Console.WriteLine("Verschiedene Materialien (statisch): " + mats.Count); }
        var bd = wv.Backdrop;
        if (bd != null) Console.WriteLine($"Hintergrund: {bd.TotalInstances} Instanzen, Ring {bd.RingVertices} Ecken");
        // Sammelbare Objekte
        var psx = world.Planet(planet);
        int trashN = 0;
        foreach (var o in Rules.All(psx))
        {
            var mesh = TrashRenderer.MeshFor(o.T);
            scene.Add((mesh, TrashRenderer.MatrixFor(o), TrashRenderer.MaterialsFor(o.T)));
            trashN++;
        }
        Console.WriteLine("Sammelbare Objekte: " + trashN);
        { var types = new HashSet<string>(); int before = 0, after = 0; foreach (var t in WorldGen.Get(planet).Trash) if (types.Add(t.Type)) { var src = MeshKit.Trash(t.Def.Shape); for (int q = 0; q < src.subMeshCount; q++) if (src.T[q].Count > 0) before++; after += TrashRenderer.MeshFor(t.Def).subMeshCount; }
          Console.WriteLine($"Müll-Typen: {types.Count}, Untermeshes vorher {before}, nachher {after}"); }
        // Formen der belebten Welt (Tiere, Stadtleben, Spuren) mitprüfen
        AnimalMeshes.PrewarmAll(); CityLife.PrewarmMeshes(); TntView.PrewarmMeshes(); TreasureModels.PrewarmAll(); GroundMarks.QuadMesh(); for (int ps = 0; ps < 3; ps++) GroundMarks.PuddleMesh(ps);
        // Wicklungsprüfung aller Meshes dieser Welt (Szene + gemeinsamer Zwischenspeicher)
        {
            var seen = new HashSet<Mesh>();
            int badMeshes = 0, badTris = 0, allTris = 0;
            var cacheField = typeof(MeshKit).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static);
            var cached = (Dictionary<string, Mesh>)cacheField.GetValue(null);
            var all = new List<Mesh>();
            foreach (var e in scene) all.Add(e.mesh);
            all.AddRange(cached.Values);
            foreach (var m in all)
            {
                if (!seen.Add(m)) continue;
                var r = CheckMesh(m);
                if (r.bad < 0) continue;
                allTris += r.total; badTris += r.bad;
                if (r.bad > 0) { badMeshes++; if (badMeshes < 25) Console.WriteLine($"  VERKEHRT: {m.name}: {r.bad}/{r.total}"); }
            }
            Console.WriteLine($"Wicklung: {seen.Count} Meshes, {allTris} Dreiecke geprüft, {badTris} verkehrt in {badMeshes} Meshes");
        }
        // Farbpalette: zeigen die Paletten-UVs (UV0) auf belegte Texel, und sind die Teile farbig statt weiß?
        {
            var palScene = new List<(Mesh, Matrix4x4, Material[])>(scene);
            var miko = RobotModel.Create(null, "MIKO_Pruefung");
            foreach (var mf in miko.gameObject.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.gameObject.GetComponent<MeshRenderer>();
                if (mr != null && mf.sharedMesh != null) palScene.Add((mf.sharedMesh, Matrix4x4.identity, mr.sharedMaterials));
            }
            CheckPalette(palScene);
        }
        // Flora
        var flora = wvGo.AddComponent<FloraRenderer>();
        var floraUpdate = typeof(FloraRenderer).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

        var views = Views(planet);
        int vi = 0;
        foreach (var v in views)
        {
            cam.transform.localPosition = v.pos;
            cam.transform.localRotation = Quaternion.LookRotation(v.target - v.pos);
            Graphics.Calls.Clear();
            bd?.Draw(cam, 100f);
            floraUpdate.Invoke(flora, null);
            int calls = Graphics.Calls.Count, inst = 0;
            foreach (var c in Graphics.Calls) inst += c.Count;
            var extra = new List<(Mesh, Matrix4x4, Material[])>();
            foreach (var c in Graphics.Calls)
            {
                var mats = new Material[c.Mesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = i == c.Sub ? c.Mat : null;
                for (int k = 0; k < c.Count; k++) extra.Add((c.Mesh, c.M[k], mats));
            }
            Console.WriteLine($"Ansicht {vi} ({v.name}): {calls} Instanz-Draws, {inst} Instanzen");
            Render(Path.Combine(outDir, $"{planet}_{state}_{vi}_{v.name}.ppm"), v.pos, v.target, extra, planet);
            vi++;
        }
        return 0;
    }

    /// <summary>
    /// Prüft alle Untermeshes mit Paletten-Material (Hauptextur „RP_Palette…“/„MIKO_Palette“): UV0 muss auf die Mitte eines
    /// belegten Texels zeigen; gezählt wird, wie viele Ecken (nahezu) reinweiß wären. Meldet Auffälligkeiten.
    /// </summary>
    static void CheckPalette(List<(Mesh mesh, Matrix4x4 m, Material[] mats)> list)
    {
        int verts = 0, white = 0, empty = 0, offCenter = 0, noUv = 0, subs = 0;
        var colors = new HashSet<Color>();
        var shaders = new SortedDictionary<string, int>();
        foreach (var (mesh, m, mats) in list)
        {
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var mat = mats[s];
                if (mat == null || !(mat.mainTexture is Texture2D pt) || pt.name == null) continue;
                if (!pt.name.StartsWith("RP_Palette") && pt.name != "MIKO_Palette") continue;
                subs++;
                string sn = (mat.shader != null && !string.IsNullOrEmpty(mat.shader.name) ? mat.shader.name + ":" : "") + (mat.name ?? "?");
                shaders[sn] = shaders.TryGetValue(sn, out var c0) ? c0 + 1 : 1;
                if (mesh.UV.Count != mesh.V.Count) { noUv++; continue; }
                var seenIdx = new HashSet<int>();
                foreach (var i in mesh.T[s])
                {
                    if (!seenIdx.Add(i)) continue;
                    verts++;
                    var uv = mesh.UV[i];
                    float fx = uv.x * pt.W, fy = uv.y * pt.H;
                    if (Math.Abs(fx - Math.Floor(fx) - 0.5f) > 0.01f || Math.Abs(fy - Math.Floor(fy) - 0.5f) > 0.01f) offCenter++;
                    var c = pt.Sample(uv) * mat.color;
                    if (c.r == 0 && c.g == 0 && c.b == 0 && c.a == 0) { empty++; continue; }
                    if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) white++;
                    colors.Add(new Color((float)Math.Round(c.r, 2), (float)Math.Round(c.g, 2), (float)Math.Round(c.b, 2), 1));
                }
            }
        }
        var sh = new List<string>(); foreach (var kv in shaders) sh.Add(kv.Key + " x" + kv.Value);
        Console.WriteLine($"Palette: {subs} Untermeshes ({string.Join(", ", sh)}), {verts} Ecken, {colors.Count} Farben, {white} weiß, {empty} auf unbelegtem Texel, {offCenter} neben der Texelmitte, {noUv} ohne UV0");
        if (subs == 0 || verts == 0) Console.WriteLine("  FEHLER: keine Paletten-Teile gefunden");
        if (empty > 0 || offCenter > 0 || noUv > 0) Console.WriteLine("  FEHLER: Paletten-UVs fehlerhaft");
        if (verts > 0 && white > verts * 0.05f) Console.WriteLine("  FEHLER: mehr als 5 % der Paletten-Ecken reinweiß");
    }

    static List<(string name, Vector3 pos, Vector3 target)> Views(string planet)
    {
        var l = new List<(string, Vector3, Vector3)>();
        float gy = Terrain.HeightAt(planet, 0, -125);
        // Eigene Ansichten: RP_VIEWS="name,px,py,pz,tx,ty,tz;…" (y relativ zur Stützpunkthöhe)
        var custom = Environment.GetEnvironmentVariable("RP_VIEWS");
        if (!string.IsNullOrEmpty(custom))
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var spec in custom.Split(';'))
            {
                var f = spec.Split(',');
                if (f.Length != 7) continue;
                float F(int i) => float.Parse(f[i], inv);
                l.Add((f[0], new Vector3(F(1), gy + F(2), F(3)), new Vector3(F(4), gy + F(5), F(6))));
            }
            return l;
        }
        l.Add(("basis", new Vector3(2, gy + 9, -112), new Vector3(2, gy + 1, -138)));
        l.Add(("stationen", new Vector3(-6, gy + 2.5f, -120), new Vector3(-16, gy + 1.5f, -132)));
        l.Add(("schiff", new Vector3(14, gy + 3, -128), new Vector3(27, gy + 3, -143)));
        l.Add(("hangar", new Vector3(-1.5f, gy + 2.2f, -142.8f), new Vector3(-4.5f, gy + 1.2f, -148.5f)));
        l.Add(("laderaum", new Vector3(21.2f, gy + 2.6f, -143.8f), new Vector3(30f, gy + 1.8f, -142.6f)));
        l.Add(("heck", new Vector3(8f, gy + 4f, -136f), new Vector3(24f, gy + 2f, -143f)));
        l.Add(("strasse", new Vector3(3, gy + 2.2f, -100), new Vector3(3, gy + 7, -60)));
        l.Add(("luft", new Vector3(-60, 110, -260), new Vector3(0, 0, -20)));
        l.Add(("horizont", new Vector3(0, gy + 8f, -60), new Vector3(-150, 20, 60)));
        if (planet == "pelagia")
        {
            l.Add(("hafenmauer", new Vector3(-28, 3f, -64), new Vector3(20, 2f, -50)));
            l.Add(("hafen", new Vector3(40, 4f, -85), new Vector3(80, 3f, -60)));
        }
        else if (planet == "terra")
        {
            l.Add(("gasse", new Vector3(-50, 2.2f, -80), new Vector3(-80, 4f, -110)));
            l.Add(("zone", new Vector3(-58, 3.5f, -108), new Vector3(-66, 0f, -120)));
        }
        else l.Add(("bereich", new Vector3(-40, 4f, -20), new Vector3(-80, 3f, 20)));
        return l;
    }

    /// <summary>Optional: Schlagschatten der Kollisionsboxen (Gebäude) auf dem Gelände, Richtung ZUR Sonne.</summary>
    internal static PlanetLayout ShadowLayout;
    internal static Vector3 SunDir;

    static bool InBoxShadow(Vector3 wp)
    {
        var tmp = shadowTmp;
        var d = SunDir.normalized;
        if (d.y < 0.02f) return false;
        for (float t = 0.6f; t < 160f; t += 0.6f)
        {
            float x = wp.x + d.x * t, y = wp.y + 0.05f + d.y * t, z = wp.z + d.z * t;
            if (y > 45f || Math.Abs(x) > 200 || Math.Abs(z) > 200) return false;
            ShadowLayout.Query(x, z, 0.05f, tmp);
            foreach (var b in tmp)
                if (b.Solid && b.Kind != "gateblock" && b.Contains(x, z, 0f) && y > b.Y0 && y < b.Y0 + b.H) return true;
        }
        return false;
    }
    static readonly List<Box> shadowTmp = new List<Box>();

    internal static void Render(string file, Vector3 pos, Vector3 target, List<(Mesh, Matrix4x4, Material[])> extra, string planet)
    {
        const int W = 960, H = 540;
        var zb = new float[W * H]; var px = new float[W * H * 3];
        // Fehlersuche: RP_PICK="x,y;x,y" meldet je Bildpunkt das vorderste Mesh (Name, Material, Tiefe)
        var pickIdx = new Dictionary<int, string>();
        string curName = null;
        var pickEnv = Environment.GetEnvironmentVariable("RP_PICK");
        if (!string.IsNullOrEmpty(pickEnv))
            foreach (var pp in pickEnv.Split(';'))
            {
                var xy = pp.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out var qx) && int.TryParse(xy[1], out var qy) && qx >= 0 && qx < W && qy >= 0 && qy < H) pickIdx[qy * W + qx] = "–";
            }
        var fog = planet == "pyra" ? new Color(0.85f, 0.6f, 0.45f) : planet == "pelagia" ? new Color(0.7f, 0.85f, 0.88f) : planet == "nivalis" ? new Color(0.7f, 0.78f, 0.88f) : new Color(0.88f, 0.78f, 0.65f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x; zb[i] = float.MaxValue;
                float t = y / (float)H; var sky = Color.Lerp(new Color(0.45f, 0.6f, 0.85f), fog, t * 1.6f);
                px[i * 3] = sky.r; px[i * 3 + 1] = sky.g; px[i * 3 + 2] = sky.b;
            }
        var f = (target - pos).normalized; var r = Vector3.Cross(Vector3.up, f).normalized; var u = Vector3.Cross(f, r);
        float foc = H * 0.5f / (float)Math.Tan(30 * Math.PI / 180);
        var L = new Vector3(-0.4f, 0.75f, -0.5f).normalized;
        bool water = planet == "pelagia";
        var ground = Mats.C(GameData.Planets[planet].Ground);
        Texture2D terrTex = null; float terrLit = 1f;
        void Draw(Mesh mesh, Matrix4x4 m, Material[] mats)
        {
            if (mesh.name == "water") return;
            bool terrain = mesh.name.StartsWith("terrain");
            var wverts = new Vector3[mesh.V.Count];
            for (int i = 0; i < wverts.Length; i++) wverts[i] = m.MultiplyPoint3x4(mesh.V[i]);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (s >= mats.Length || mats[s] == null) continue;
                var mat = mats[s];
                var tl = mesh.T[s];
                for (int k = 0; k + 2 < tl.Count; k += 3)
                {
                    Vector3 a = wverts[tl[k]], b = wverts[tl[k + 1]], c = wverts[tl[k + 2]];
                    var n = Vector3.Cross(b - a, c - a); // Vorderseite nach Unity-Konvention (Handbuch-Beispiel Quad)
                    if (Vector3.Dot(n, pos - a) <= 0) continue; // Rückseite wird weggeschnitten
                    n = n.normalized;
                    float lit = 0.45f + 0.55f * Math.Max(0, Vector3.Dot(n, L));
                    var baseCol = terrain ? ground : mat.color;
                    if (mat.mainTexture is Texture2D pt && mesh.UV.Count == mesh.V.Count) baseCol = pt.Sample(mesh.UV[tl[k]]);
                    // Gelände: Textur je Bildpunkt (UV = Weltlage, wie WorldView.BuildTerrain)
                    terrTex = terrain ? mat.mainTexture as Texture2D : null; terrLit = lit;
                    var col = baseCol * lit + mat.emission * 0.6f;
                    if (pickIdx.Count > 0) curName = mesh.name + " [" + (mat.name ?? "?") + "] Dreieck " + (k / 3) + " " + a + " " + b + " " + c;
                    Raster(a, b, c, col);
                }
            }
        }
        void Raster(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            Vector3 P(Vector3 p) { var d = p - pos; return new Vector3(Vector3.Dot(d, r), Vector3.Dot(d, u), Vector3.Dot(d, f)); }
            var va = P(a); var vb = P(b); var vc = P(c);
            const float near = 0.3f;
            if (va.z < near && vb.z < near && vc.z < near) return;
            if (va.z < near || vb.z < near || vc.z < near)
            {
                // An der Nahebene abschneiden (sonst fehlen große Flächen, die hinter die Kamera reichen – z. B. Innenräume)
                var poly = new List<Vector3>();
                var src = new[] { va, vb, vc };
                for (int e = 0; e < 3; e++)
                {
                    var p0 = src[e]; var p1 = src[(e + 1) % 3];
                    bool in0 = p0.z >= near, in1 = p1.z >= near;
                    if (in0) poly.Add(p0);
                    if (in0 != in1) poly.Add(Vector3.Lerp(p0, p1, (near - p0.z) / (p1.z - p0.z)));
                }
                for (int e = 1; e + 1 < poly.Count; e++) RasterView(poly[0], poly[e], poly[e + 1], col);
                return;
            }
            RasterView(va, vb, vc, col);
        }
        void RasterView(Vector3 va, Vector3 vb, Vector3 vc, Color col)
        {
            float ax = W * 0.5f + va.x / va.z * foc, ay = H * 0.5f - va.y / va.z * foc;
            float bx = W * 0.5f + vb.x / vb.z * foc, by = H * 0.5f - vb.y / vb.z * foc;
            float cx = W * 0.5f + vc.x / vc.z * foc, cy = H * 0.5f - vc.y / vc.z * foc;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            if (x0 > x1 || y0 > y1) return;
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float qx = x + 0.5f, qy = y + 0.5f;
                    float w0 = ((bx - qx) * (cy - qy) - (by - qy) * (cx - qx)) / area;
                    float w1 = ((cx - qx) * (ay - qy) - (cy - qy) * (ax - qx)) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float z = 1f / (w0 / va.z + w1 / vb.z + w2 / vc.z);
                    int i = y * W + x;
                    if (z >= zb[i]) continue;
                    zb[i] = z;
                    if (pickIdx.Count > 0 && pickIdx.ContainsKey(i)) pickIdx[i] = curName + " Tiefe " + z.ToString("0.00");
                    float fd = z * 0.0068f; float fogK = 1f - (float)Math.Exp(-fd * fd);
                    var pc = col;
                    if (terrTex != null)
                    {
                        var wp = pos + r * ((qx - W * 0.5f) / foc * z) - u * ((qy - H * 0.5f) / foc * z) + f * z;
                        pc = terrTex.Sample(new Vector2((wp.x + 150f) / 300f, (wp.z + 150f) / 300f)) * terrLit;
                        if (ShadowLayout != null && z < 140f && InBoxShadow(wp)) pc *= 0.5f;
                    }
                    var cc = Color.Lerp(pc, fog, fogK);
                    px[i * 3] = cc.r; px[i * 3 + 1] = cc.g; px[i * 3 + 2] = cc.b;
                }
        }
        foreach (var (mesh, m, mats) in scene) Draw(mesh, m, mats);
        foreach (var (mesh, m, mats) in extra) Draw(mesh, m, mats);
        if (water)
        {
            // Wasserebene halbtransparent darüber
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var dir = (f + r * ((x + 0.5f - W * 0.5f) / foc) - u * ((y + 0.5f - H * 0.5f) / foc));
                    if (dir.y >= -1e-4f) continue;
                    float t = -pos.y / dir.y; // Wasser bei y = 0
                    var hit = pos + dir * t;
                    float z = Vector3.Dot(hit - pos, f);
                    int i = y * W + x;
                    if (z >= zb[i]) continue;
                    float fd = z * 0.0068f; float fogK = 1f - (float)Math.Exp(-fd * fd);
                    var wc = Color.Lerp(new Color(0.25f, 0.45f, 0.4f), fog, fogK);
                    px[i * 3] = px[i * 3] * 0.35f + wc.r * 0.65f; px[i * 3 + 1] = px[i * 3 + 1] * 0.35f + wc.g * 0.65f; px[i * 3 + 2] = px[i * 3 + 2] * 0.35f + wc.b * 0.65f;
                }
        }
        foreach (var kv in pickIdx) Console.WriteLine($"  Bildpunkt {kv.Key % W},{kv.Key / W}: {kv.Value}");
        using (var fs = new FileStream(file, FileMode.Create))
        {
            var head = System.Text.Encoding.ASCII.GetBytes($"P6 {W} {H} 255\n");
            fs.Write(head, 0, head.Length);
            var data = new byte[W * H * 3];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)Math.Max(0, Math.Min(255, px[i] * 255));
            fs.Write(data, 0, data.Length);
        }
    }
}
