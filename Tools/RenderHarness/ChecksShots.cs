// Bilder aus dem laufenden Spiel (Software-Renderer der Prüfumgebung, keine Unity-Aufnahmen): startet das Spiel wie
// „run“, stellt je Aufnahme Planet, Tageszeit, MIKO-Position/-Blick und optional Schlaf ein, lässt einige Bilder laufen und
// zeichnet dann alle aktiven Meshes der Szene (Welt, Figuren, Anlagen, MIKO) plus ein Bild Instanz-Draws (Müll, Pflanzen,
// Tiere, Hintergrund) aus Sicht der Spielkamera bzw. einer festen Kamera.
//   dotnet run -- shots <Ordner> [Filter]     (Filter = Teil des Aufnahmenamens)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RePlanet;
using RePlanet.Core;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class Checks
{
    public class Shot
    {
        public string Name, Planet = "terra";
        public float Phase = 0.45f;
        public Vector3 Miko; public float MikoYawDeg;
        /// <summary>Feste Kamera (sonst die Spielkamera hinter MIKO mit <see cref="RigYaw"/>/<see cref="RigPitch"/>).</summary>
        public Vector3? Cam, Target;
        public float RigYaw = float.NaN, RigPitch = 12f, RigDist = 6.5f;
        public bool Sleep;
        public float Settle = 1.5f;
        /// <summary>Nach dem Einstellen zusätzlich so lange weiterlaufen (z. B. für Schlaf-Animationen).</summary>
        public float After;
    }

    static List<Shot> ShotList()
    {
        var l = new List<Shot>();
        // Eigene Aufnahmen: RP_SHOTS="name,planet,phase,mikoX,mikoZ,mikoYaw,camX,camY,camZ,zielX,zielY,zielZ;…"
        // (Kamera- und Zielhöhe relativ zum Boden an ihrer Stelle; Kamera "-" = Spielkamera mit Blick mikoYaw)
        var custom = Environment.GetEnvironmentVariable("RP_SHOTS");
        if (!string.IsNullOrEmpty(custom))
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var spec in custom.Split(';'))
            {
                var f = spec.Split(',');
                if (f.Length < 6) continue;
                float F(int i) => float.Parse(f[i], inv);
                var sh = new Shot { Name = f[0], Planet = f[1], Phase = F(2), Miko = new Vector3(F(3), 0, F(4)), MikoYawDeg = F(5) };
                if (f.Length >= 12)
                {
                    var lay = WorldGen.Get(sh.Planet);
                    sh.Cam = new Vector3(F(6), lay.GroundAt(F(6), F(8)) + F(7), F(8));
                    sh.Target = new Vector3(F(9), lay.GroundAt(F(9), F(11)) + F(10), F(11));
                }
                else { sh.RigYaw = F(5); if (f.Length > 6) sh.RigPitch = F(6); if (f.Length > 7) sh.RigDist = F(7); }
                l.Add(sh);
            }
            return l;
        }
        foreach (var p in GameData.PlanetOrder)
        {
            var b = WorldGen.Get(p).Base;
            float gy = b.Center.y;
            // Stützpunkt von vorn (Tag), schräg von links, von rechts, Hangar innen, Nacht
            l.Add(new Shot { Name = p + "_station_tag", Planet = p, Phase = 0.35f, Miko = new Vector3(-2f, gy, -126f), Cam = new Vector3(6f, gy + 7.5f, -108f), Target = new Vector3(-1f, gy + 3.2f, -145f) });
            l.Add(new Shot { Name = p + "_station_links", Planet = p, Phase = 0.35f, Miko = new Vector3(-2f, gy, -126f), Cam = new Vector3(18f, gy + 4f, -124f), Target = new Vector3(-2f, gy + 3.5f, -146f) });
            l.Add(new Shot { Name = p + "_station_rechts", Planet = p, Phase = 0.35f, Miko = new Vector3(-2f, gy, -126f), Cam = new Vector3(-22f, gy + 5f, -122f), Target = new Vector3(0f, gy + 3.5f, -146f) });
            l.Add(new Shot { Name = p + "_hangar_innen", Planet = p, Phase = 0.35f, Miko = new Vector3(-2.5f, gy, -145.2f), MikoYawDeg = 180f, Cam = new Vector3(0.8f, gy + 2.6f, -142.6f), Target = new Vector3(-4.5f, gy + 1.0f, -148.2f) });
            l.Add(new Shot { Name = p + "_station_nacht", Planet = p, Phase = 0.9f, Miko = new Vector3(0f, gy, -134f), MikoYawDeg = 180f, Cam = new Vector3(3f, gy + 4.5f, -116f), Target = new Vector3(-1f, gy + 3f, -145f) });
        }
        // Schlaf im Hangar (TERRA): MIKO auf dem Ladeplatz, Nahaufnahme
        {
            var b = WorldGen.Get("terra").Base; float gy = b.Center.y;
            l.Add(new Shot { Name = "terra_schlaf", Planet = "terra", Phase = 0.9f, Miko = new Vector3(b.Hangar.Spot.x, gy, b.Hangar.Spot.z), MikoYawDeg = 180f, Sleep = true, After = 4f, Cam = new Vector3(b.Hangar.Spot.x + 2.2f, gy + 1.6f, b.Hangar.Spot.z + 2.4f), Target = new Vector3(b.Hangar.Spot.x, gy + 0.6f, b.Hangar.Spot.z) });
            l.Add(new Shot { Name = "terra_wach", Planet = "terra", Phase = 0.9f, Miko = new Vector3(b.Hangar.Spot.x, gy, b.Hangar.Spot.z), MikoYawDeg = 180f, Sleep = false, After = 1f, Cam = new Vector3(b.Hangar.Spot.x + 2.2f, gy + 1.6f, b.Hangar.Spot.z + 2.4f), Target = new Vector3(b.Hangar.Spot.x, gy + 0.6f, b.Hangar.Spot.z) });
        }
        // Bildfehler aus den Nutzer-Screenshots nachgestellt
        l.Add(new Shot { Name = "terra_bug20_abend", Planet = "terra", Phase = 0.55f, Miko = new Vector3(-2f, 0f, -128f), MikoYawDeg = 180f, RigYaw = 182f, RigPitch = 8f });
        l.Add(new Shot { Name = "pelagia_bug21_huegel", Planet = "pelagia", Phase = 0.35f, Miko = new Vector3(-60f, 0f, -20f), MikoYawDeg = 0f, RigYaw = 20f, RigPitch = 22f });
        return l;
    }

    public static int Shots(string outDir, string filter)
    {
        Directory.CreateDirectory(outDir);
        var dir = Application.persistentDataPath;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        World.OnAdded = OnAdded;
        Debug.Quiet = true;
        Graphics.Record = false;
        Screen.width = 1920; Screen.height = 1080;
        Begin("Start");
        foreach (var lt in new[] { RuntimeInitializeLoadType.BeforeSceneLoad, RuntimeInitializeLoadType.AfterSceneLoad })
            foreach (var t in typeof(GameApp).Assembly.GetTypes())
                foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var at = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (at == null || at.loadType != lt || m.GetParameters().Length != 0) continue;
                    try { m.Invoke(null, null); } catch (TargetInvocationException e) { Report(t.Name + "." + m.Name, e.InnerException ?? e); }
                }
        var app = Object.FindObjectOfType<GameApp>();
        if (app == null) { Console.WriteLine("GameApp fehlt"); return 2; }
        ShipArrival.AllowCinematic = false; // Landeanflug-Zwischensequenz würde die Kamera übernehmen
        Run(1.5f);
        AudioManager.FakeIntroTime = 0;
        app.BeginNewGame("Bildwelt", "slot1", false);
        Run(0.5f, 1f / 30f, t => AudioManager.FakeIntroTime = t);
        Input.Held.Add(KeyCode.Space); Run(1.3f); Input.Held.Clear();
        Run(0.5f);
        app.StartNewWorld("terra");
        Run(2f);
        if (!app.InGame) { Console.WriteLine("Spiel startet nicht"); return 2; }
        var g = HostGame(app);
        string pid = app.Client.Pid;
        foreach (var s in ShotList())
        {
            if (filter != null && !s.Name.Contains(filter)) continue;
            Begin("Aufnahme " + s.Name);
            if (app.W.CurrentPlanet != s.Planet)
            {
                g.S.Unlocked.Add(s.Planet);
                g.S.Players[pid].Pos = WorldGen.Get(app.W.CurrentPlanet).Base.Stations["ship"];
                var r = g.Apply(pid, new JObj().Set("a", "travel").Set("planet", s.Planet).Set("rid", "shot" + s.Name), true);
                if (!r.Ok) { Fail("Reise nach " + s.Planet + ": " + r.Err); continue; }
                Run(1.5f);
            }
            if (g.S.Players[pid].Sleeping) { g.Apply(pid, new JObj().Set("a", "wake").Set("rid", "w" + s.Name), true); Run(0.3f); }
            SetPhase(g, s.Planet, s.Phase);
            var miko = new V3(s.Miko.x, WorldGen.Get(s.Planet).GroundAt(s.Miko.x, s.Miko.z), s.Miko.z);
            Teleport(g, pid, miko);
            SetMikoYaw(g, pid, s.MikoYawDeg * Mathf.Deg2Rad);
            if (!float.IsNaN(s.RigYaw)) { CameraRig.I.Yaw = s.RigYaw; CameraRig.I.Pitch = s.RigPitch; CameraRig.I.Distance = s.RigDist; }
            Run(s.Settle, 1f / 30f, t => { if (!float.IsNaN(s.RigYaw)) CameraRig.I.Yaw = s.RigYaw; });
            if (s.Sleep)
            {
                var sr = g.Apply(pid, new JObj().Set("a", "sleep").Set("rid", "sl" + s.Name), true);
                Info("Schlafen: " + (sr.Ok ? "ok" : sr.Err));
            }
            if (s.After > 0f) Run(s.After, 1f / 30f, t => { if (!float.IsNaN(s.RigYaw)) CameraRig.I.Yaw = s.RigYaw; });
            // ein Bild mit Aufzeichnung der Instanz-Draws
            Graphics.Calls.Clear(); Graphics.Record = true;
            Frame(1f / 30f);
            Graphics.Record = false;
            var extra = new List<(Mesh, Matrix4x4, Material[])>();
            foreach (var c in Graphics.Calls)
            {
                var mats = new Material[c.Mesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = i == c.Sub ? c.Mat : null;
                for (int k = 0; k < c.Count; k++) extra.Add((c.Mesh, c.M[k], mats));
            }
            Graphics.Calls.Clear();
            Program.scene.Clear();
            var terrainTex = typeof(WorldView).GetField("terrainTex", BF).GetValue(WorldView.I);
            int renderers = 0;
            foreach (var go in World.Objects)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var mf = go.GetComponent<MeshFilter>(); var mr = go.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || !mr.enabled || mf.sharedMesh == null) continue;
                var sm = mr.sharedMaterials;
                if ((sm.Length == 0 || sm[0] == null) && (mf.sharedMesh.name ?? "").StartsWith("terrain"))
                    sm = new[] { new Material((Shader)null) { name = "Gelände", mainTexture = terrainTex } };
                Program.scene.Add((mf.sharedMesh, go.transform.localToWorldMatrix, sm));
                renderers++;
            }
            Vector3 cam, target;
            if (s.Cam.HasValue) { cam = s.Cam.Value; target = s.Target.Value; }
            else { var ct = CameraRig.I.Cam.transform; cam = ct.position; target = cam + ct.forward * 10f; }
            var me = app.Me;
            Info($"Zwischensequenz {CameraRig.I.Cinematic}, Modus {app.Mode}, UI {UIState.Screen}, {renderers} Renderer, {extra.Count} Instanzen, Kamera {cam} → {target}, MIKO {PlayerController.I.RenderPos} schläft {me.Sleeping}");
            // Schlagschatten der Gebäude (RP_SCHATTEN=1): Sonne wie im Spiel (Atmosphere), Boxen aus dem Layout
            if (Environment.GetEnvironmentVariable("RP_SCHATTEN") == "1" && Atmosphere.I != null && Atmosphere.I.Sun != null)
            {
                Program.ShadowLayout = WorldGen.Get(s.Planet);
                Program.SunDir = -Atmosphere.I.Sun.transform.forward;
                Info($"Sonne: Richtung {Program.SunDir}, Höhe {Mathf.Asin(Mathf.Clamp(Program.SunDir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg:0.0}°, Schattenweite {QualitySettings.shadowDistance:0} m");
            }
            else Program.ShadowLayout = null;
            Program.Render(System.IO.Path.Combine(outDir, s.Name + ".ppm"), cam, target, extra, s.Planet);
        }
        CollectWarnings();
        Problems.RemoveAll(p => p.Contains("wird nicht unterstützt – Rückfall auf Standard"));
        Console.WriteLine(Problems.Count == 0 ? "Aufnahmen: keine Probleme." : "Aufnahmen: " + Problems.Count + " Probleme");
        foreach (var p in Problems) Console.WriteLine(" - " + p.Split('\n')[0]);
        return Problems.Count == 0 ? 0 : 1;
    }

    static void SetMikoYaw(Game g, string pid, float yaw)
    {
        g.S.Players[pid].Yaw = yaw;
        var pc = PlayerController.I;
        var f = typeof(PlayerController).GetField("ms", BF);
        object ms = f.GetValue(pc);
        ms.GetType().GetField("Yaw").SetValue(ms, yaw);
        f.SetValue(pc, ms);
    }
}
