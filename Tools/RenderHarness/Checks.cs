// Laufzeitprüfung ohne Unity: startet das Spiel wie Unity (GameApp → alle Bausteine), treibt Awake/Start/Update/
// LateUpdate/OnGUI Bild für Bild und meldet jede Ausnahme mit Stacktrace. Szenarien:
//   intro   – kompletter Intro-Ablauf 0…102 s (Bühnenaufbau aller Einstellungen, jede Qualitätsstufe), Überspringen
//   game    – neues Spiel, alle vier Planeten (Planetenwechsel, Tag, Nacht, Sturm, Fotomodus-Vorher, Fahren/Laufen)
//   ending  – Abspann komplett
//   all     – alles nacheinander (Standard)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RePlanet;
using RePlanet.Core;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public static class Checks
{
    // ================================================================== Bildtakt wie Unity
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Dictionary<(Type, string), MethodInfo> methods = new Dictionary<(Type, string), MethodInfo>();
    static readonly HashSet<Component> awoken = new HashSet<Component>(), started = new HashSet<Component>();
    public static string Phase = "";
    static int problemsAtPhaseStart;

    static MethodInfo M(Type t, string name)
    {
        if (!methods.TryGetValue((t, name), out var m))
        {
            m = null;
            for (var k = t; k != null && k != typeof(MonoBehaviour); k = k.BaseType)
            {
                m = k.GetMethod(name, BF | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) break;
            }
            methods[(t, name)] = m;
        }
        return m;
    }

    public static readonly Dictionary<string, int> CallCounts = new Dictionary<string, int>();
    static void Call(Component c, string name)
    {
        var m = M(c.GetType(), name);
        if (m == null) return;
        string key = c.GetType().Name + "." + name;
        CallCounts.TryGetValue(key, out var cn); CallCounts[key] = cn + 1;
        try { m.Invoke(c, null); }
        catch (TargetInvocationException e)
        {
            var inner = e.InnerException ?? e;
            Report(c.GetType().Name + "." + name, inner);
        }
    }

    public static readonly List<string> Problems = new List<string>();
    static readonly Dictionary<string, int> problemCounts = new Dictionary<string, int>();

    static void Report(string where, Exception e)
    {
        string key = where + ": " + e.GetType().Name + ": " + e.Message;
        problemCounts.TryGetValue(key, out var n);
        problemCounts[key] = n + 1;
        if (n == 0)
        {
            Problems.Add("[" + Phase + "] " + key + "\n" + e.StackTrace);
            Console.WriteLine("  AUSNAHME [" + Phase + "] " + key + "\n" + Indent(e.StackTrace));
        }
    }

    static string Indent(string s) => s == null ? "" : "      " + s.Replace("\n", "\n      ");

    static void OnAdded(Component c)
    {
        if (c is MonoBehaviour && c.gameObject.activeInHierarchy && awoken.Add(c)) { Call(c, "Awake"); Call(c, "OnEnable"); }
    }

    static int Order(Component c) => c.GetType().GetCustomAttribute<DefaultExecutionOrderAttribute>()?.order ?? 0;

    static List<MonoBehaviour> Live()
    {
        var l = new List<MonoBehaviour>();
        foreach (var mb in World.Behaviours.ToArray()) if (!mb.IsDead) l.Add(mb);
        return l.OrderBy(Order).ToList();
    }

    static bool Active(MonoBehaviour b) => !b.IsDead && b.enabled && b.gameObject.activeInHierarchy;

    /// <summary>Ein Bild: Start (einmalig) → Update → Koroutinen → LateUpdate → OnGUI (Layout+Repaint) → Zerstören.</summary>
    public static void Frame(float dt)
    {
        Time.Advance(dt);
        var live = Live();
        foreach (var b in live) if (Active(b) && awoken.Add(b)) { Call(b, "Awake"); Call(b, "OnEnable"); }
        foreach (var b in live) if (Active(b) && started.Add(b)) Call(b, "Start");
        foreach (var b in live) if (Active(b)) Call(b, "Update");
        foreach (var b in live)
        {
            if (!Active(b) || b.Coroutines.Count == 0) continue;
            foreach (var co in b.Coroutines.ToArray())
            {
                try { if (!co.MoveNext()) b.Coroutines.Remove(co); }
                catch (Exception e) { b.Coroutines.Remove(co); Report(b.GetType().Name + ".Koroutine", e); }
            }
        }
        foreach (var b in live) if (Active(b)) Call(b, "LateUpdate");
        foreach (var t in new[] { EventType.Layout, EventType.Repaint })
        {
            Event.current.type = t;
            foreach (var b in live) if (Active(b)) Call(b, "OnGUI");
        }
        Object.FlushDestroy();
        Input.Down.Clear();
    }

    public static void Run(float seconds, float dt = 1f / 30f, Action<float> each = null)
    {
        int n = Math.Max(1, (int)Math.Round(seconds / dt));
        for (int i = 0; i < n; i++) { each?.Invoke(i * dt); Frame(dt); }
    }

    static void Begin(string phase)
    {
        Phase = phase;
        problemsAtPhaseStart = Problems.Count + Debug.Errors.Count;
        Console.WriteLine("== " + phase);
    }

    static readonly List<string> expectedWarnings = new List<string>();
    static int warnSeen;
    static void CollectWarnings()
    {
        for (; warnSeen < Debug.Warnings.Count; warnSeen++)
        {
            var w = Debug.Warnings[warnSeen] ?? "";
            Problems.Add("[" + Phase + "] Warnung: " + w);
        }
        for (; errSeen < Debug.Errors.Count; errSeen++) Problems.Add("[" + Phase + "] Fehler (Debug.Log*): " + Debug.Errors[errSeen].Split('\n')[0]);
    }
    static int errSeen;

    static void Fail(string msg) { Problems.Add("[" + Phase + "] " + msg); Console.WriteLine("  FEHLER: " + msg); }
    static void Info(string msg) { Console.WriteLine("  " + msg); }

    // ================================================================== Einstieg
    public static int Main(string what)
    {
        var dir = Application.persistentDataPath;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        World.OnAdded = OnAdded;
        Debug.Quiet = true;
        Graphics.Record = false;
        Screen.width = 1920; Screen.height = 1080;

        Begin("Start");
        var go = new GameObject("RE:PLANET");
        var app = go.AddComponent<GameApp>();
        Run(1.5f);
        if (app.Mode != AppMode.Menu) Fail("Nach dem Start nicht im Hauptmenü: " + app.Mode);
        if (Camera.main == null) Fail("Keine Hauptkamera");
        CollectWarnings();

        bool all = what == "all";
        if (all || what == "intro") IntroChecks(app);
        if (all || what == "game") GameChecks(app, all || what == "game");
        if (all || what == "ending") EndingCheck(app);
        CollectWarnings();

        Console.WriteLine();
        Console.WriteLine("Aufrufe: " + string.Join(", ", CallCounts.Where(kv => !kv.Key.EndsWith(".OnGUI")).OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value)));
        Console.WriteLine("Instanz-Draws: " + Graphics.Draws + " mit " + Graphics.Instances + " Instanzen");
        foreach (var kv in UnityWarnings.Counts) Console.WriteLine("Unity-Konsolenmeldung (" + kv.Value + "×): " + kv.Key);
        if (Problems.Count == 0) { Console.WriteLine("Laufzeitprüfung: keine Ausnahmen, keine Warnungen."); return 0; }
        Console.WriteLine("Laufzeitprüfung: " + Problems.Count + " Probleme");
        foreach (var p in Problems) Console.WriteLine(" - " + p.Split('\n')[0]);
        return 1;
    }

    // ================================================================== Intro
    static void IntroChecks(GameApp app)
    {
        var intro = Object.FindObjectOfType<IntroDirector>();
        if (intro == null) { Fail("IntroDirector fehlt"); return; }
        var shotsField = typeof(IntroDirector).GetField("shots", BF);
        var activeField = typeof(IntroDirector).GetField("activeShot", BF);
        var playingField = typeof(IntroDirector).GetField("playing", BF);
        var animErr = typeof(IntroDirector).GetField("animFailed", BF);

        // Bühnenaufbau in allen Qualitätsstufen (andere Detailstufe → andere Anzahlen, Zufallsindizes)
        foreach (int q in new[] { 0, 1, 3, 2 })
        {
            Begin("Intro Qualität " + q);
            app.Settings.Quality = q;
            AudioManager.FakeIntroTime = 0;
            app.PlayIntroOnly();
            var shots = (Dictionary<string, Transform>)shotsField.GetValue(intro);
            foreach (var s in IntroTimeline.Shots)
            {
                Transform tr;
                if (!shots.TryGetValue(s.Id, out tr)) { Fail("Einstellung " + s.Id + " fehlt"); continue; }
                int mr = tr.gameObject.GetComponentsInChildren<MeshRenderer>(true).Length;
                int ps = tr.gameObject.GetComponentsInChildren<ParticleSystem>(true).Length;
                int verts = tr.gameObject.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.vertexCount);
                if (q == 2) Info($"Einstellung {s.Id,-10} {mr,4} Renderer {verts,8} Ecken {ps,3} Partikelsysteme");
                if (mr == 0) Fail("Einstellung " + s.Id + " ist leer (Qualität " + q + ")");
            }
            if (q != 2)
            {
                // jede Einstellung einmal betreten und animieren
                foreach (var s in IntroTimeline.Shots)
                    for (int i = 0; i < 6; i++) { AudioManager.FakeIntroTime = s.Start + 0.2f + i * (s.End - s.Start) / 6f; Frame(1f / 30f); }
                AudioManager.FakeIntroTime = IntroTimeline.Total + 2.5f; Run(0.2f);
                if (app.Mode != AppMode.Menu) Fail("Intro (Qualität " + q + ") endet nicht im Menü: " + app.Mode);
                CollectWarnings();
                continue;
            }
            // kompletter Ablauf in Echtzeitschritten, synchron zur „Musik“
            string last = null;
            Run(IntroTimeline.Total + 2.5f, 1f / 30f, t =>
            {
                AudioManager.FakeIntroTime = t;
                var a = (string)activeField.GetValue(intro);
                if (a != last && a != null) { last = a; }
            });
            if (((HashSet<string>)animErr.GetValue(intro)).Count > 0) Fail("Intro: Animationsfehler in " + string.Join(", ", (HashSet<string>)animErr.GetValue(intro)));
            if ((bool)playingField.GetValue(intro)) Fail("Intro läuft nach 102 s noch");
            if (app.Mode != AppMode.Menu) Fail("Intro endet nicht im Menü: " + app.Mode);
            if (GameObject.Find("IntroStage") != null) Fail("Intro-Bühne nach dem Ende nicht abgebaut");
            if (CameraRig.I != null && CameraRig.I.Cinematic) Fail("Kamera bleibt nach dem Intro im Zwischensequenz-Modus");
            CollectWarnings();
        }

        // Ohne Musik: nach 10 s läuft das Bild frei weiter; Überspringen durch Gedrückthalten
        Begin("Intro ohne Musik + Überspringen");
        AudioManager.FakeIntroTime = -1;
        app.PlayIntroOnly();
        Run(14f);
        Input.Held.Add(KeyCode.Escape);
        Run(1.3f);
        Input.Held.Clear();
        if ((bool)playingField.GetValue(intro)) Fail("Überspringen beendet das Intro nicht");
        if (app.Mode != AppMode.Menu) Fail("Nach dem Überspringen nicht im Menü: " + app.Mode);
        CollectWarnings();
    }

    // ================================================================== Spiel
    static Game HostGame(GameApp app) => app.Host?.Session?.Game;

    static void GameChecks(GameApp app, bool full)
    {
        Begin("Neues Spiel (Intro überspringen → Planetenwahl → Welt)");
        AudioManager.FakeIntroTime = 0;
        app.BeginNewGame("Prüfwelt", "slot1", false);
        if (app.Mode != AppMode.Intro) Fail("Neues Spiel startet kein Intro (Modus " + app.Mode + ")");
        Run(0.5f, 1f / 30f, t => AudioManager.FakeIntroTime = t);
        Input.Held.Add(KeyCode.Space);
        Run(1.3f);
        Input.Held.Clear();
        if (app.Mode != AppMode.PlanetSelect) Fail("Nach dem Intro keine Planetenwahl: " + app.Mode);
        foreach (var p in GameData.PlanetOrder) { WorldView.I?.PreviewPlanet(p); Run(0.2f); }
        app.StartNewWorld("terra");
        Run(2f);
        if (!app.InGame) { Fail("Spiel startet nicht (Modus " + app.Mode + ", Fehler " + app.Client?.FatalError + ")"); return; }
        CollectWarnings();

        var g = HostGame(app);
        string pid = app.Client.Pid;
        foreach (var planet in GameData.PlanetOrder)
        {
            Begin("Planet " + planet);
            if (app.W.CurrentPlanet != planet)
            {
                g.S.Unlocked.Add(planet);
                var ship = WorldGen.Get(app.W.CurrentPlanet).Base.Stations["ship"];
                g.S.Players[pid].Pos = ship;
                var r = g.Apply(pid, new JObj().Set("a", "travel").Set("planet", planet).Set("rid", "t" + planet), true);
                if (!r.Ok) { Fail("Reise nach " + planet + " abgelehnt: " + r.Err); continue; }
                Run(1.5f);
                if (app.W.CurrentPlanet != planet) { Fail("Client nicht auf " + planet); continue; }
                if (WorldView.I.Planet != planet) Fail("WorldView zeigt " + WorldView.I.Planet + " statt " + planet);
            }
            var p0 = PlayerController.I.RenderPos;
            PlaySome(app, 4f, "Tag");
            Info("MIKO bewegt: " + (PlayerController.I.RenderPos - p0).magnitude.ToString("0.0") + " m, Welt " + WorldView.I.Planet + ", Nacht " + Rules.IsNight(app.W, planet) + ", Sturm " + app.W.Cur.StormActive);
            { var me = app.Me; var sp = g.S.Players[pid]; Info($"  Client: Pos {me.Pos.x:0.0},{me.Pos.z:0.0} Energie {me.Energy:0} Schlaf {me.Sleeping} Abschlepp {me.TowTimer:0.0} Fahrzeug {me.Vehicle}; Server: Pos {sp.Pos.x:0.0},{sp.Pos.z:0.0} Phase {Rules.DayPhase(g.S, planet):0.00} Client-Phase {Rules.DayPhase(app.W, planet):0.00} Pause {app.Paused} UI {UIState.BlocksGameplay}"); }
            SetPhase(g, planet, 0.95f);
            PlaySome(app, 7f, "Nacht");
            var ps = g.S.Planet(planet);
            ps.StormTimer = GameData.Planets[planet].StormEvery + 1f;
            PlaySome(app, 7f, "Sturm (Nacht)");
            Info("Nacht " + Rules.IsNight(app.W, planet) + " (Phase " + Rules.DayPhase(app.W, planet).ToString("0.00") + "), Sturm " + app.W.Cur.StormActive + ", geöffnete Menüs " + menusOpened);
            if (!Rules.IsNight(app.W, planet)) Fail("Nacht kam beim Client nicht an");
            if (!app.W.Cur.StormActive) Fail("Sturm kam beim Client nicht an");
            SetPhase(g, planet, 0.5f);
            PlaySome(app, 7f, "Sturm (Tag)");
            // Fotomodus mit Vorher-Ansicht
            PhotoMode.Active = true; PhotoMode.ShowBefore = true;
            Run(1.5f);
            PhotoMode.RequestCapture = true;
            Run(0.5f);
            PhotoMode.ShowBefore = false;
            Run(0.5f);
            PhotoMode.Active = false;
            Run(0.5f);
            CameraSurvey(app, planet);
            CollectWarnings();
        }
    }

    /// <summary>Tageszeit auf dem Server setzen (Versatz) – kommt mit dem nächsten Wetterabgleich beim Client an.</summary>
    static void SetPhase(Game g, string planet, float phase)
    {
        var ps = g.S.Planet(planet);
        float now = Rules.DayPhase(g.S, planet);
        float d = phase - now; if (d < 0) d += 1f;
        ps.DayOffset += d * GameData.Planets[planet].DayLength;
    }

    /// <summary>Laufen, Kamera drehen, Werkzeuge wechseln, Aktion auslösen.</summary>
    static void PlaySome(GameApp app, float seconds, string what)
    {
        Info(what + " …");
        var keys = new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
        Run(seconds, 1f / 30f, t =>
        {
            CheckLiveCamera(what);
            // Ersatz-Oberfläche: geöffnete Stations-/Spielmenüs sofort wieder schließen (wie ein Spieler mit Esc)
            if (UIState.Screen == UIScreen.Menu || UIState.Screen == UIScreen.Map || UIState.Screen == UIScreen.Pause || UIState.Screen == UIScreen.Travel) { menusOpened++; UIState.Open(UIScreen.None); }
            Input.Held.Clear();
            int seg = (int)(t / 0.8f);
            Input.Held.Add(keys[seg % 4]);
            if (seg % 3 == 0) Input.Held.Add(KeyCode.LeftShift);
            Input.Axes["Mouse X"] = (float)Math.Sin(t * 1.3) * 3f;
            Input.Axes["Mouse Y"] = (float)Math.Cos(t * 0.7) * 1f;
            if ((int)(t * 30) % 45 == 0) Input.Down.Add(KeyCode.Alpha1 + (seg % 7));
            if ((int)(t * 30) % 20 == 0) { Input.Down.Add(KeyCode.Mouse0); Input.Held.Add(KeyCode.Mouse0); }
            if ((int)(t * 30) % 60 == 0) Input.Down.Add(KeyCode.E);
        });
        Input.Held.Clear(); Input.Axes.Clear();
    }

    // ================================================================== Kamera: nie in Wänden/Gebäuden
    /// <summary>Feste Box nach denselben Regeln wie die Bewegung (Tore offen/zu, aktive Dünen, gebaute Anlagen).</summary>
    static bool SolidForCam(MotorEnv env, Box b) => b.Kind != "gateblock" && (env != null ? env.Solid(b) : b.Solid && b.Gate < 0 && b.DuneSet < 0);

    static bool InsideBox(MotorEnv env, PlanetLayout l, Vector3 pos, float margin, List<Box> tmp, out Box hit)
    {
        hit = null;
        if (env != null) env.Query(pos.x, pos.z, margin + 0.5f, tmp); else l.Query(pos.x, pos.z, margin + 0.5f, tmp);
        foreach (var k in tmp)
        {
            if (!SolidForCam(env, k)) continue;
            if (k.Contains(pos.x, pos.z, margin) && pos.y > k.Y0 - margin && pos.y < k.Y0 + k.H + margin) { hit = k; return true; }
        }
        return false;
    }

    /// <summary>Während des Spielens: die echte Kamera darf nie in einer festen Box oder unter dem Gelände stehen.</summary>
    static void CheckLiveCamera(string what)
    {
        var rig = CameraRig.I; var wv = WorldView.I;
        if (rig == null || wv == null || wv.Layout == null || rig.Cinematic || PhotoMode.Active) return;
        var pos = rig.Cam.transform.position;
        var env = PlayerController.I?.Env;
        Box b;
        if (InsideBox(env, wv.Layout, pos, 0.1f, new List<Box>(), out b)) liveInside.Add(what + ": Kamera " + pos + " in " + k0(b) + ", MIKO " + PlayerController.I?.RenderPos);
        float g = RePlanet.Core.Terrain.HeightAt(wv.Planet, pos.x, pos.z);
        if (pos.y < g + 0.1f) liveInside.Add(what + ": Kamera " + pos + " unter dem Gelände (" + g.ToString("0.00") + ")");
    }
    static readonly List<string> liveInside = new List<string>();
    static int menusOpened;

    static void CameraSurvey(GameApp app, string planet)
    {
        if (liveInside.Count > 0) { Fail("Kamera im Spiel " + liveInside.Count + "× in Hindernis/Gelände, z. B. " + liveInside[0]); liveInside.Clear(); }
        var rig = CameraRig.I; var pc = PlayerController.I; var wv = WorldView.I;
        if (rig == null || pc == null || wv == null) return;
        var env = pc.Env;
        var collide = typeof(CameraRig).GetMethod("Collide", BF);
        var tmp = new List<Box>();
        int tested = 0, inside = 0, below = 0, behindWall = 0, tooClose = 0;
        string example = null, exampleSight = null;
        var l = wv.Layout;
        var rng = new System.Random(3);
        var boxes = new List<Box>(l.Colliders);
        if (env != null) boxes.AddRange(env.Extra);
        // Stichproben entlang von Wänden: MIKO neben eine feste Box, Kamera aus allen Richtungen und Neigungen
        foreach (var b in boxes)
        {
            if (!SolidForCam(env, b)) continue;
            if (rng.NextDouble() > 0.35) continue;
            for (int side = 0; side < 4; side++)
            {
                float ox = side == 0 ? b.Hx + 0.85f : side == 1 ? -b.Hx - 0.85f : 0f, oz = side == 2 ? b.Hz + 0.85f : side == 3 ? -b.Hz - 0.85f : 0f;
                float px = b.Cx + ox, pz = b.Cz + oz;
                float gy = RePlanet.Core.Terrain.HeightAt(planet, px, pz);
                float water = RePlanet.Core.Terrain.WaterLevel(planet);
                if (water > -50f && gy < water - 0.6f) gy = water - 0.35f; // schwimmt
                var target = new Vector3(px, gy + 1.4f, pz);
                Box hb;
                if (InsideBox(env, l, new Vector3(px, gy + 0.5f, pz), 0.8f, tmp, out hb)) continue; // dort kann MIKO nicht stehen
                for (int yaw = 0; yaw < 360; yaw += 30)
                    foreach (float pitch in new[] { -20f, 10f, 45f })
                    {
                        var rot = Quaternion.Euler(pitch, yaw, 0);
                        var wanted = target - rot * Vector3.forward * 6.5f;
                        var pos = (Vector3)collide.Invoke(rig, new object[] { target, wanted });
                        tested++;
                        if (InsideBox(env, l, pos, 0.15f, tmp, out hb)) { inside++; if (example == null) example = $"{k0(hb)} Ziel {target} Kamera {pos}"; }
                        float g2 = RePlanet.Core.Terrain.HeightAt(planet, pos.x, pos.z);
                        if (pos.y < g2 + 0.2f) below++;
                        if ((pos - target).magnitude < 0.9f) tooClose++;
                        // Sichtlinie Kamera → MIKO frei?
                        var dir = target - pos; float len = dir.magnitude; dir /= Math.Max(1e-4f, len);
                        for (float t = 0.05f; t < len - 0.3f; t += 0.1f)
                        {
                            var p = pos + dir * t;
                            if (InsideBox(env, l, p, 0f, tmp, out hb)) { behindWall++; if (exampleSight == null) exampleSight = $"{k0(hb)} Ziel {target} Kamera {pos}"; break; }
                        }
                    }
            }
        }
        Info($"Kamera-Stichproben {planet}: {tested} Stellungen, {inside} in Wänden, {below} unter Gelände, {behindWall} mit verdeckter Sicht, {tooClose} näher als 0,9 m");
        if (inside > 0) Fail($"Kamera {inside}× in Wand/Gebäude ({planet}), z. B. {example}");
        if (below > 0) Fail($"Kamera {below}× unter dem Gelände ({planet})");
        if (behindWall > 0) Fail($"Sicht Kamera→MIKO {behindWall}× durch Wand verdeckt ({planet}), z. B. {exampleSight}");
    }
    static string k0(Box b) => $"{b.Kind} ({b.Cx:0.0}, {b.Cz:0.0}) {b.Hx * 2:0.0}×{b.Hz * 2:0.0}×{b.H:0.0}";

    // ================================================================== Abspann
    static void EndingCheck(GameApp app)
    {
        Begin("Abspann");
        var ed = Object.FindObjectOfType<EndingDirector>();
        if (ed == null) { Fail("EndingDirector fehlt"); return; }
        if (!app.InGame) { Info("(ohne laufendes Spiel)"); }
        var fx = typeof(GameApp).GetMethod("FxToast", BF);
        fx.Invoke(app, new object[] { new JObj().Set("k", "ending") });
        if (app.Mode != AppMode.Ending && app.InGame) Fail("Abspann startet nicht");
        Run(55f, 1f / 20f);
        if (app.Mode == AppMode.Ending) Fail("Abspann endet nicht");
        if (CameraRig.I != null && CameraRig.I.Cinematic) Fail("Kamera bleibt nach dem Abspann im Zwischensequenz-Modus");
        Run(1f);
        CollectWarnings();
    }
}
