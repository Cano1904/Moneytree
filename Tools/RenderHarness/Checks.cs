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
        // Wie Unity beim Start: alle [RuntimeInitializeOnLoadMethod] (erst BeforeSceneLoad, dann AfterSceneLoad; GameApp.Boot ist dabei)
        foreach (var lt in new[] { RuntimeInitializeLoadType.BeforeSceneLoad, RuntimeInitializeLoadType.AfterSceneLoad })
            foreach (var t in typeof(GameApp).Assembly.GetTypes())
                foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var at = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (at == null || at.loadType != lt || m.GetParameters().Length != 0) continue;
                    try { m.Invoke(null, null); }
                    catch (TargetInvocationException e) { Report(t.Name + "." + m.Name, e.InnerException ?? e); }
                }
        var app = Object.FindObjectOfType<GameApp>();
        if (app == null) { Console.WriteLine("GameApp wurde nicht erzeugt"); return 2; }
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

        // Mit Sprachaufnahmen (Erzähler plant Zeilen, Untertitel folgen den Aufnahmen), Untertitel an und aus
        foreach (bool subs in new[] { true, false })
        {
            Begin("Intro mit Sprachaufnahmen, Untertitel " + (subs ? "an" : "aus"));
            Resources.FakeVoiceLength = 5.2f;
            ClearVoiceCache(); // der Erzähler merkt sich fehlende Aufnahmen (im Spiel kommen keine nachträglich hinzu)
            app.Settings.Subtitles = subs;
            AudioManager.FakeIntroTime = 0;
            app.PlayIntroOnly();
            int lines = 0; string lastLine = null; bool recSeen = false;
            Run(IntroTimeline.Total + 2.5f, 1f / 15f, t =>
            {
                AudioManager.FakeIntroTime = t;
                if (Hud.Subtitle != null && Hud.Subtitle != lastLine) { lastLine = Hud.Subtitle; lines++; }
                if (Narrator.HasRecordings) recSeen = true;
            });
            if (!recSeen) Fail("Erzähler findet die Aufnahmen nicht");
            Info("Untertitelzeilen gezeigt: " + lines + ", Aufnahmen erkannt: " + recSeen);
            if (subs && lines < 10) Fail("Zu wenige Untertitel mit Aufnahmen: " + lines);
            if (!subs && lines > 0) Fail("Untertitel trotz ausgeschalteter Untertitel: " + lines);
            if (app.Mode != AppMode.Menu) Fail("Intro (mit Stimme) endet nicht im Menü: " + app.Mode);
            CollectWarnings();
        }
        Resources.FakeVoiceLength = 0; app.Settings.Subtitles = true;
        ClearVoiceCache();

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

    static void ClearVoiceCache()
    {
        var f = typeof(Narrator).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static);
        ((System.Collections.IDictionary)f.GetValue(null)).Clear();
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
            BuildOut(app, g, pid, planet);
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

    /// <summary>Aktion direkt auf dem Server an einer bestimmten Stelle (wie ein Spieler, der dort steht).</summary>
    static ActResult At(Game g, string pid, V3 pos, JObj a)
    {
        g.S.Players[pid].Pos = pos;
        return g.Apply(pid, a.Set("rid", "h" + (++rid)), true);
    }
    static int rid;

    /// <summary>Spieler an eine Stelle versetzen – auf dem Server und in der Vorhersage des Clients (sonst korrigiert einer den anderen).</summary>
    static void Teleport(Game g, string pid, V3 pos)
    {
        g.S.Players[pid].Pos = pos;
        var pc = PlayerController.I;
        var f = typeof(PlayerController).GetField("ms", BF);
        object ms = f.GetValue(pc);
        ms.GetType().GetField("Pos").SetValue(ms, pos);
        ms.GetType().GetField("Vel").SetValue(ms, new V3(0, 0, 0));
        f.SetValue(pc, ms);
    }

    /// <summary>Ausbau: Upgrades, Fahrzeuge, Schiff, Anlagen, Lieferung, Fahren in jedem Fahrzeug, Schlafen, Notabschaltung.</summary>
    static void BuildOut(GameApp app, Game g, string pid, string planet)
    {
        Info("Ausbau …");
        var l = WorldGen.Get(planet); var b = l.Base;
        g.S.Credits = 10_000_000;
        foreach (var mat in GameData.Materials.Keys) { var e = g.S.Cur.Store(mat); e.U += 400; e.B += 40; }
        int ok = 0;
        foreach (var t in GameData.Tech.Keys) for (int i = 0; i < 6; i++) if (At(g, pid, b.Spawn, new JObj().Set("a", "buytech").Set("id", t)).Ok) ok++;
        foreach (var v in GameData.Vehicles.Keys) if (At(g, pid, b.Spawn, new JObj().Set("a", "buyveh").Set("id", v)).Ok) ok++;
        for (int i = 0; i < 4; i++) if (At(g, pid, b.Spawn, new JObj().Set("a", "buyship")).Ok) ok++;
        int built = 0;
        foreach (var kv in GameData.Buildings)
            for (int x = 0; x < b.GridW && built < 40; x += 2)
            {
                bool placed = false;
                for (int z = 0; z < b.GridH; z += 2)
                    if (At(g, pid, b.Spawn, new JObj().Set("a", "build").Set("t", kv.Key).Set("x", x).Set("z", z).Set("r", (x + z) % 4)).Ok) { built++; placed = true; break; }
                if (placed) break;
            }
        At(g, pid, b.Spawn, new JObj().Set("a", "delivery"));
        Info($"  {ok} Käufe, {built} Anlagen gebaut, Fahrzeuge: {string.Join(", ", g.S.Cur.Vehicles.Keys)}");
        PlaySome(app, 3f, "Stützpunkt ausgebaut");
        // jedes Fahrzeug: einsteigen, fahren, aussteigen
        foreach (var vid in new List<string>(g.S.Cur.Vehicles.Keys))
        {
            var v = g.S.Cur.Vehicles[vid];
            Teleport(g, pid, v.Pos); // Client und Server gemeinsam, sonst schiebt die alte Client-Position das Fahrzeug weg
            Run(0.3f);
            var r = At(g, pid, v.Pos, new JObj().Set("a", "venter").Set("v", vid));
            if (!r.Ok) { Info("  Einsteigen in " + vid + ": " + r.Err); continue; }
            // Client übernimmt die Position vom Server (sonst korrigiert er zurück)
            Run(0.5f);
            if (app.Me.Vehicle != vid) { Info("  Client sitzt nicht in " + vid); continue; }
            var v0 = PlayerController.I.RenderPos;
            Run(4f, 1f / 30f, t =>
            {
                CheckLiveCamera("Fahren " + vid);
                Input.Held.Clear(); Input.Held.Add(KeyCode.W);
                if (t > 1.5f) Input.Held.Add(t < 3f ? KeyCode.A : KeyCode.D);
                if ((int)(t * 30) % 25 == 0) { Input.Down.Add(KeyCode.Mouse0); Input.Held.Add(KeyCode.Mouse0); }
            });
            Input.Held.Clear();
            float drove = (PlayerController.I.RenderPos - v0).magnitude;
            Info("  gefahren mit " + vid + ": " + drove.ToString("0.0") + " m");
            if (drove < 3f)
            {
                var vs = g.S.Cur.Vehicles[vid]; var def = GameData.Vehicles[vid];
                var sb = new System.Text.StringBuilder();
                for (int k = -8; k <= 8; k += 2) sb.Append(RePlanet.Core.Terrain.HeightAt(planet, vs.Pos.x, vs.Pos.z + k).ToString("0.0")).Append(' ');
                Fail($"{vid} kommt vom Abstellplatz nicht weg: Pos {vs.Pos.x:0.0}/{vs.Pos.z:0.0}, Blick {vs.Yaw:0.00}, Wasser {RePlanet.Core.Terrain.WaterLevel(planet):0.0}, Boden z−8…z+8: {sb}");
            }
            g.Apply(pid, new JObj().Set("a", "vexit").Set("rid", "x" + (++rid)), true);
            Run(0.5f);
        }
        // Schlafen im Stützpunkt (nachts) und Notabschaltung draußen
        SetPhase(g, planet, 0.9f);
        Run(6f);
        var st = b.Stations["storage"];
        Teleport(g, pid, new V3(st.x + 2f, st.y, st.z + 2f));
        Run(0.5f);
        var sr = g.Apply(pid, new JObj().Set("a", "sleep").Set("rid", "s" + (++rid)), true);
        Run(1.5f);
        Info("  Schlafen: " + (sr.Ok ? "ok, Client schläft " + app.Me.Sleeping : sr.Err));
        Run(3f);
        g.Apply(pid, new JObj().Set("a", "wake").Set("rid", "w" + (++rid)), true);
        // Notabschaltung draußen: Akku leer → Abschleppdrohne → Ladestation
        var p = g.S.Players[pid];
        SetPhase(g, planet, 0.9f);
        var outside = new V3(b.Spawn.x, b.Spawn.y, b.Spawn.z + 40f);
        outside.y = RePlanet.Core.Terrain.HeightAt(planet, outside.x, outside.z);
        Teleport(g, pid, outside);
        p.Energy = 0.3f;
        bool towed = false;
        Run(12f, 1f / 30f, t => { if (p.TowTimer > 0) towed = true; });
        Info("  Notabschaltung: " + (towed ? "abgeschleppt" : "nicht ausgelöst") + ", jetzt bei " + p.Pos.x.ToString("0") + "/" + p.Pos.z.ToString("0") + ", Energie " + p.Energy.ToString("0"));
        if (!towed) Fail("Notabschaltung wurde nicht ausgelöst");
        Run(4f);
        SetPhase(g, planet, 0.45f);
        Run(6f);
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

    static readonly List<(Vector3 cam, Vector3 target)> camSamples = new List<(Vector3, Vector3)>();

    static void CameraSurvey(GameApp app, string planet)
    {
        camSamples.Clear();
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
                if (Math.Abs(px) > 149f || Math.Abs(pz) > 149f) continue; // dort kann MIKO nicht hin (Weltgrenze)
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
                        camSamples.Add((pos, target));
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
        DecorSurvey(planet);
        {
            var decoF = typeof(CameraRig).GetField("deco", BF);
            var ensure = typeof(CameraRig).GetMethod("EnsureDeco", BF);
            decoF.SetValue(rig, null);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ensure.Invoke(rig, new object[] { wv });
            long mainMs = sw.ElapsedMilliseconds;
            for (int i = 0; i < 2000 && decoF.GetValue(rig) == null; i++) { System.Threading.Thread.Sleep(5); ensure.Invoke(rig, new object[] { wv }); }
            var dg = decoF.GetValue(rig);
            if (dg == null) { Fail("Deko-Raster der Kamera wird nicht fertig"); return; }
            var tl = (List<float>)dg.GetType().GetField("t", BF).GetValue(dg);
            var cl = (System.Collections.IDictionary)dg.GetType().GetField("cells", BF).GetValue(dg);
            long refs = 0; foreach (System.Collections.DictionaryEntry e in cl) refs += ((List<int>)e.Value).Count;
            Info($"Deko-Raster der Kamera: {tl.Count / 11} Dreiecke, {cl.Count} Zellen, {refs} Einträge, Hauptthread {mainMs} ms, fertig nach {sw.ElapsedMilliseconds} ms, ≈ {(tl.Count * 4 + refs * 4 + cl.Count * 48) / 1048576} MB");
        }
        Info($"Kamera-Stichproben {planet}: {tested} Stellungen, {inside} in Wänden, {below} unter Gelände, {behindWall} mit verdeckter Sicht, {tooClose} näher als 0,9 m");
        if (inside > 0) Fail($"Kamera {inside}× in Wand/Gebäude ({planet}), z. B. {example}");
        if (below > 0) Fail($"Kamera {below}× unter dem Gelände ({planet})");
        if (behindWall > 0) Fail($"Sicht Kamera→MIKO {behindWall}× durch Wand verdeckt ({planet}), z. B. {exampleSight}");
    }
    // ------------------------------------------------------------------ Kamera gegen die tatsächlich gezeichnete Geometrie
    /// <summary>
    /// Prüft die Kamerastellungen der Stichprobe gegen alle Dreiecke der Welt (statische Meshes + ein Bild Instanz-Draws:
    /// Hintergrund, Müll, Pflanzen, Figuren) – findet Deko, die über die Kollisionsboxen hinausragt.
    /// </summary>
    static void DecorSurvey(string planet)
    {
        var tris = new List<(Vector3 a, Vector3 b, Vector3 c, string name)>();
        void AddMesh(Mesh m, Matrix4x4 mx, string name, int sub = -1)
        {
            if (m == null || m.V.Count == 0) return;
            var w = new Vector3[m.V.Count];
            for (int i = 0; i < w.Length; i++) w[i] = mx.MultiplyPoint3x4(m.V[i]);
            for (int s = 0; s < m.subMeshCount; s++)
            {
                if (sub >= 0 && s != sub) continue;
                var t = m.T[s];
                for (int k = 0; k + 2 < t.Count; k += 3)
                {
                    var a = w[t[k]]; if (Math.Abs(a.x) > 170 || Math.Abs(a.z) > 170) continue;
                    tris.Add((a, w[t[k + 1]], w[t[k + 2]], name));
                }
            }
        }
        var wv = WorldView.I;
        foreach (var mf in wv.Root.gameObject.GetComponentsInChildren<MeshFilter>(false))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;
            string n = mf.sharedMesh.name ?? "";
            if (n.StartsWith("terrain") || n == "water") continue;
            AddMesh(mf.sharedMesh, mf.transform.localToWorldMatrix, Path(mf.transform));
        }
        foreach (var mf in ActorsView.I != null ? ActorsView.I.gameObject.GetComponentsInChildren<MeshFilter>(false) : new MeshFilter[0])
            if (mf.sharedMesh != null && !mf.transform.IsChildOf(CameraRig.I.transform.Find("MainCamera") ?? mf.transform.root)) AddMesh(mf.sharedMesh, mf.transform.localToWorldMatrix, "Figur/Anlage " + Path(mf.transform));
        Graphics.Calls.Clear(); Graphics.Record = true;
        Frame(1f / 30f);
        Graphics.Record = false;
        foreach (var c in Graphics.Calls)
            for (int k = 0; k < c.Count; k++)
            {
                var p = c.M[k].GetPosition();
                if (Math.Abs(p.x) > 175 || Math.Abs(p.z) > 175) continue;
                AddMesh(c.Mesh, c.M[k], "Instanz " + c.Mesh.name, c.Sub);
            }
        Graphics.Calls.Clear();
        // Raster 2 m
        var grid = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            var (a, b, c, _) = tris[i];
            int x0 = (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x)) / 2f), x1 = (int)Math.Floor(Math.Max(a.x, Math.Max(b.x, c.x)) / 2f);
            int z0 = (int)Math.Floor(Math.Min(a.z, Math.Min(b.z, c.z)) / 2f), z1 = (int)Math.Floor(Math.Max(a.z, Math.Max(b.z, c.z)) / 2f);
            if ((x1 - x0) * (z1 - z0) > 400) continue; // riesige Flächen (Himmel, Boden) auslassen
            for (int gx = x0; gx <= x1; gx++) for (int gz = z0; gz <= z1; gz++)
                { if (!grid.TryGetValue((gx, gz), out var l)) grid[(gx, gz)] = l = new List<int>(); l.Add(i); }
        }
        var hits = new Dictionary<string, int>();
        int clipped = 0, targetInside = 0; string ex = null;
        foreach (var (cam, target) in camSamples)
        {
            {
                // MIKO selbst steckt in gezeichneter Geometrie (Deko ragt über die Kollisionsbox hinaus) – kein Kamerafehler
                float bt = float.MaxValue;
                int tx = (int)Math.Floor(target.x / 2f), tz = (int)Math.Floor(target.z / 2f);
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                        if (grid.TryGetValue((tx + dx, tz + dz), out var tl)) foreach (var i in tl) if (tris[i].name.Contains("/Buildings")) bt = Math.Min(bt, PointTri(target, tris[i].a, tris[i].b, tris[i].c));
                if (bt < 0.25f) { targetInside++; continue; }
            }
            float best = float.MaxValue; string what = null;
            int gx = (int)Math.Floor(cam.x / 2f), gz = (int)Math.Floor(cam.z / 2f);
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue((gx + dx, gz + dz), out var l)) continue;
                    foreach (var i in l) { float d = PointTri(cam, tris[i].a, tris[i].b, tris[i].c); if (d < best) { best = d; what = tris[i].name; } }
                }
            if (best < 0.14f)
            {
                clipped++;
                string key = Group(what);
                hits.TryGetValue(key, out var n); hits[key] = n + 1;
                if (ex == null) ex = $"Kamera {cam} (MIKO {target}) {best:0.00} m vor {what}";
            }
        }
        Info($"Deko-Prüfung {planet}: {tris.Count} Dreiecke, {camSamples.Count} Kamerastellungen, {clipped} näher als die Nahebene an gezeichneter Geometrie; {targetInside} Stellungen, in denen MIKOs Kopfpunkt selbst in Gebäude-Deko steckt (übersprungen)");
        foreach (var kv in hits.OrderByDescending(k => k.Value).Take(12)) Info($"    {kv.Value,5}× {kv.Key}");
        if (ex != null) Info("    z. B. " + ex);
        decorClips[planet] = clipped;
    }
    public static readonly Dictionary<string, int> decorClips = new Dictionary<string, int>();

    static string Group(string n) { if (n == null) return "?"; var parts = n.Split('/'); return parts.Length > 2 ? parts[0] + "/" + parts[1] + "/" + parts[2] : n; }
    static string Path(Transform t) { var l = new List<string>(); while (t != null && l.Count < 6) { l.Insert(0, t.gameObject.name); t = t.parent; } return string.Join("/", l); }

    static float PointTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Ericson, Real-Time Collision Detection: nächster Punkt auf Dreieck
        var ab = b - a; var ac = c - a; var ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return (p - a).magnitude;
        var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return (p - b).magnitude;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) { float v = d1 / (d1 - d3); return (p - (a + ab * v)).magnitude; }
        var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return (p - c).magnitude;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) { float w = d2 / (d2 - d6); return (p - (a + ac * w)).magnitude; }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (p - (b + (c - b) * w)).magnitude; }
        float denom = 1f / (va + vb + vc); float vv = vb * denom, ww = vc * denom;
        return (p - (a + ab * vv + ac * ww)).magnitude;
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
