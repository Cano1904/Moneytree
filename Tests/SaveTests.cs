using System;
using System.IO;
using System.Linq;
using RePlanet.Core;

/// <summary>Speicherformat: Rundlauf, Prüfsumme, Backup, Migration, Fortsetzen nach Neustart.</summary>
public static class SaveTests
{
    static WorldState RoundTrip(WorldState w)
    {
        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(w), out err);
        Assert.True(w2 != null, "Spielstand muss lesbar sein: " + err);
        return w2;
    }

    /// <summary>Eine Welt mit möglichst vielen gespeicherten Teilen (entfernt, dynamisch, Lager, Bauten, Wetter …).</summary>
    static Game PlayedWorld(out PlayerData p)
    {
        var g = TestHelpers.NewGame(out p);
        foreach (var o in TestKit.Grabbables(g, 4)) Assert.True(TestHelpers.Grab(g, p, o).Ok, "Aufheben");
        g.S.Credits = 5000;
        g.S.Cur.Store("metall").S = 60; g.S.Cur.Store("glas").U = 7; g.S.Cur.Store("papier").B = 2;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "build"));
        Assert.True(g.Apply(p.Id, TestKit.A("build").Set("t", "sortierer").Set("x", 0).Set("z", 0).Set("r", 0), true).Ok, "Bauen");
        Assert.True(g.Apply(p.Id, TestKit.A("buytech").Set("id", "bin"), true).Ok, "Upgrade");
        Assert.True(g.Apply(p.Id, TestKit.A("buyveh").Set("id", "rover"), true).Ok, "Rover");
        Assert.True(g.Apply(p.Id, TestKit.A("delivery"), true).Ok, "Lieferung (dynamische Objekte)");
        TestKit.Ticks(g, 30f);
        return g;
    }

    [Test]
    public static void Rundlauf_ergibt_identische_Welt()
    {
        PlayerData p;
        var g = PlayedWorld(out p);
        var w = g.S;
        string before = Json.Write(w.ToJson(true));
        var w2 = RoundTrip(w);
        string after = Json.Write(w2.ToJson(true));
        Assert.Equal(before, after, "Kodieren → Dekodieren muss dieselbe Welt ergeben");
        Assert.Equal(w.Credits, w2.Credits, "Credits");
        Assert.Equal(w.Cur.Removed.Count, w2.Cur.Removed.Count, "Anzahl entfernter Objekte");
        Assert.True(w2.Cur.Dyn.Count >= 14, "Dynamische Objekte (Lieferung) gespeichert");
        Assert.Equal(1, w2.Cur.Buildings.Count, "Bauwerk gespeichert");
        Assert.True(w2.OwnedVehicles.Contains("rover"), "Fahrzeug gespeichert");
        Assert.Equal(1, w2.TechLevel("bin"), "Upgrade gespeichert");
        // Zweiter Durchlauf bleibt stabil (keine schleichende Rundungsdrift)
        Assert.Equal(after, Json.Write(RoundTrip(w2).ToJson(true)), "Zweiter Rundlauf stabil");
        // Der geladene Stand ist spielbar
        var g2 = new Game(w2);
        var p2 = g2.Join(p.Id, "Tester");
        TestKit.Ticks(g2, 5f);
        Assert.Equal(w.Cur.RemovedWeight[0], w2.Cur.RemovedWeight[0], "Abgeleitete Reinigungswerte neu berechnet");
    }

    [Test]
    public static void Manipulierte_Pruefsumme_oder_Daten_werden_erkannt()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Credits = 123;
        string text = SaveCodec.Encode(g.S);
        var env = Json.ParseObj(text);
        string err;

        var bad = Json.ParseObj(text); bad["checksum"] = "00000000";
        Assert.True(SaveCodec.Decode(Json.Write(bad), out err) == null, "Falsche Prüfsumme muss abgelehnt werden");
        Assert.True(err != null && err.Contains("Prüfsumme"), "Verständliche Meldung: " + err);

        var cheat = Json.ParseObj(text);
        cheat["payload"] = env.Str("payload").Replace("\"credits\":123", "\"credits\":999999");
        Assert.True(cheat.Str("payload") != env.Str("payload"), "Testvoraussetzung: Nutzdaten verändert");
        Assert.True(SaveCodec.Decode(Json.Write(cheat), out err) == null, "Veränderte Nutzdaten müssen abgelehnt werden");

        Assert.True(SaveCodec.Decode(text.Substring(0, text.Length / 2), out err) == null, "Abgeschnittene Datei wird abgelehnt");
        Assert.True(SaveCodec.Decode("", out err) == null, "Leere Datei wird abgelehnt");
        var future = Json.ParseObj(text); future["version"] = WorldState.CurrentVersion + 1;
        Assert.True(SaveCodec.Decode(Json.Write(future), out err) == null && err.Contains("neueren"), "Neuere Version wird verständlich abgelehnt: " + err);
        Assert.True(SaveCodec.Decode(text, out err) != null, "Original bleibt lesbar: " + err);
    }

    [Test]
    public static void SaveStore_laedt_Backup_bei_beschaedigtem_Hauptstand()
    {
        string dir = TestKit.TempDir("store");
        try
        {
            var store = new SaveStore(dir);
            PlayerData p;
            var g = TestHelpers.NewGame(out p);
            string err;
            g.S.Credits = 111;
            Assert.True(store.Save("slot1", g.S, out err), "Erstes Speichern: " + err);
            Assert.False(File.Exists(store.BackupOf("slot1")), "Beim ersten Speichern gibt es noch kein Backup");
            g.S.Credits = 222;
            Assert.True(store.Save("slot1", g.S, out err), "Zweites Speichern: " + err);
            Assert.True(File.Exists(store.BackupOf("slot1")), "Backup des vorherigen Stands angelegt");
            Assert.False(File.Exists(store.PathOf("slot1") + ".tmp"), "Keine Temporärdatei übrig");

            bool fromBackup;
            var w = store.Load("slot1", out err, out fromBackup);
            Assert.True(w != null && !fromBackup, "Hauptstand wird geladen");
            Assert.Equal(222L, w.Credits, "Neuester Stand");

            // Hauptstand beschädigen (Datenträgerfehler simulieren)
            var text = File.ReadAllText(store.PathOf("slot1"));
            File.WriteAllText(store.PathOf("slot1"), text.Substring(0, text.Length - 40) + "XXXX");
            w = store.Load("slot1", out err, out fromBackup);
            Assert.True(w != null, "Backup muss geladen werden: " + err);
            Assert.True(fromBackup, "Kennzeichnung: aus Backup geladen");
            Assert.Equal(111L, w.Credits, "Backup enthält den vorherigen Stand");
            Assert.True(err != null && err.Contains("Backup"), "Hinweis für den Spieler: " + err);
            var info = store.Info("slot1");
            Assert.True(info != null && info.HasBackup, "Slot-Info zeigt Backup");

            // Speichern über einen beschädigten Hauptstand ersetzt NICHT das gute Backup
            g.S.Credits = 333;
            Assert.True(store.Save("slot1", g.S, out err), "Speichern trotz beschädigtem Hauptstand: " + err);
            w = store.Load("slot1", out err, out fromBackup);
            Assert.Equal(333L, w.Credits, "Neuer Hauptstand");
            File.WriteAllText(store.PathOf("slot1"), "{kaputt");
            w = store.Load("slot1", out err, out fromBackup);
            Assert.True(w != null && fromBackup && w.Credits == 111L, "Gutes Backup blieb erhalten");

            // Beide beschädigt → verständlicher Fehler statt Absturz
            File.WriteAllText(store.BackupOf("slot1"), "nichts");
            w = store.Load("slot1", out err, out fromBackup);
            Assert.True(w == null && err != null, "Beide unbrauchbar → Fehlermeldung: " + err);
            // Fehlender Slot
            w = store.Load("slot3", out err, out fromBackup);
            Assert.True(w == null && err != null, "Fehlender Slot → Meldung");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Test]
    public static void Migration_v1_auf_aktuelles_Format()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var o = TestKit.Grabbables(g, 1)[0];
        Assert.True(TestHelpers.Grab(g, p, o).Ok, "Aufheben");
        g.S.Credits = 4321;
        // v1-Nutzdaten nachbauen: "money" statt "credits", keine Statistiken, keine Intro-Markierung, Planeten ohne "misc"
        var v1 = g.S.ToJson(true);
        v1["version"] = 1;
        v1["money"] = v1["credits"]; v1.Remove("credits");
        v1.Remove("stats");
        v1.Obj("flags").Remove("is");
        foreach (var kv in v1.Obj("planets")) ((JObj)kv.Value).Remove("misc");
        string err;
        var w = SaveCodec.Decode(TestKit.Envelope(v1, 1), out err);
        Assert.True(w != null, "v1-Spielstand muss ladbar sein: " + err);
        Assert.Equal(WorldState.CurrentVersion, w.Version, "Version angehoben");
        Assert.Equal(4321L, w.Credits, "Guthaben aus \"money\" übernommen");
        Assert.True(w.IntroSeen, "Intro gilt bei alten Ständen als gesehen");
        Assert.True(w.Planet("terra").Visited, "Planet gilt als besucht");
        Assert.True(w.Planet("terra").Removed.Get(o.Sid), "Entfernte Objekte bleiben entfernt");
        Assert.True(w.Stats != null, "Statistiken angelegt");
        // Direkter Aufruf: v2 → v3
        var v2 = g.S.ToJson(true); v2.Obj("flags").Remove("is");
        var m = SaveCodec.Migrate(v2, 2);
        Assert.Equal(WorldState.CurrentVersion, m.Int("version"), "Migrate setzt die Version");
        Assert.True(m.Obj("flags").Bool("is"), "Migrate ergänzt die Intro-Markierung");
        // Migrierter Stand wird beim nächsten Speichern im aktuellen Format geschrieben
        var again = SaveCodec.Decode(SaveCodec.Encode(w), out err);
        Assert.True(again != null && again.Credits == 4321L, "Neu gespeichert und geladen: " + err);
        Assert.Equal(WorldState.CurrentVersion, Json.ParseObj(SaveCodec.Encode(w)).Int("version"), "Aktuelle Version im Umschlag");
    }

    [Test]
    public static void Entfernte_Objekte_bleiben_nach_Laden_und_Planetenwechsel_entfernt()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var terraObjs = TestKit.Grabbables(g, 3);
        foreach (var o in terraObjs) Assert.True(TestHelpers.Grab(g, p, o).Ok, "Aufheben auf TERRA");
        // Zerlegeteile (dynamisch) entstehen und eines wird eingesammelt
        g.S.Tech["cutter"] = 1; g.S.Tech["bin"] = 4;
        var mw = Rules.All(g.S.Cur).First(x => x.IsStatic && x.T.Id == "mikrowelle" && x.Gate < 0);
        TestHelpers.Teleport(g, p.Id, new V3(mw.Pos.x + 1f, mw.Pos.y, mw.Pos.z));
        ActResult rc = null;
        for (int i = 0; i < 200; i++) { g.Tick(0.25f); rc = g.Apply(p.Id, TestKit.A("cut").Set("o", mw.Key).Set("dt", 0.25f), true); if (rc.Ok && rc.Data.Bool("done")) break; }
        Assert.True(rc.Ok && rc.Data.Bool("done"), "Zerlegen: " + rc.Err);
        var pieces = rc.Data.Arr("pieces").Cast<string>().ToList();
        Assert.True(pieces.Count >= 3, "Teile entstanden");
        var firstPiece = Rules.Obj(g.S.Cur, pieces[0]);
        TestHelpers.Teleport(g, p.Id, new V3(firstPiece.Pos.x + 0.5f, firstPiece.Pos.y, firstPiece.Pos.z));
        g.Tick(0.3f);
        var rg = g.Apply(p.Id, TestKit.A("grab").Set("o", pieces[0]), true);
        if (!rg.Ok) rg = g.Apply(p.Id, TestKit.A("magnet").Set("dir", Json.Arr(-1, 0)), true);
        int terraRemoved = g.S.Planet("terra").Removed.Count;
        int terraDyn = g.S.Planet("terra").Dyn.Count;

        // Reise nach PYRA, dort ebenfalls sammeln
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "ship"));
        var rt = g.Apply(p.Id, TestKit.A("travel").Set("planet", "pyra"), true);
        Assert.True(rt.Ok, "Reise nach PYRA: " + rt.Err);
        var pyraObjs = TestKit.Grabbables(g, 2);
        p.Bin.Clear();
        foreach (var o in pyraObjs) Assert.True(TestHelpers.Grab(g, p, o).Ok, "Aufheben auf PYRA");

        // Speichern + Laden (Neustart)
        var w2 = RoundTrip(g.S);
        var g2 = new Game(w2);
        var p2 = g2.Join(p.Id, "Tester");
        Assert.Equal("pyra", w2.CurrentPlanet, "Weiter auf PYRA");
        foreach (var o in pyraObjs) Assert.True(Rules.Obj(w2.Cur, o.Key) == null, "PYRA-Objekt bleibt entfernt: " + o.Key);

        // Zurück nach TERRA
        TestHelpers.Teleport(g2, p2.Id, TestHelpers.Station(g2, "ship"));
        rt = g2.Apply(p2.Id, TestKit.A("travel").Set("planet", "terra"), true);
        Assert.True(rt.Ok, "Reise nach TERRA: " + rt.Err);
        foreach (var o in terraObjs) Assert.True(Rules.Obj(w2.Cur, o.Key) == null, "TERRA-Objekt bleibt entfernt: " + o.Key);
        Assert.True(Rules.Obj(w2.Cur, mw.Key) == null, "Zerlegte Mikrowelle bleibt entfernt");
        Assert.Equal(terraRemoved, w2.Cur.Removed.Count, "Gleiche Anzahl entfernter Objekte auf TERRA");
        Assert.Equal(terraDyn, w2.Cur.Dyn.Count, "Gleiche dynamische Objekte auf TERRA");
        foreach (var d in pieces) Assert.Equal(g.S.Planet("terra").Dyn.ContainsKey(d), w2.Cur.Dyn.ContainsKey(d), "Zerlegeteil " + d + " unverändert");

        // Nochmal speichern/laden auf TERRA und nach PYRA wechseln
        var w3 = RoundTrip(w2);
        var g3 = new Game(w3);
        var p3 = g3.Join(p.Id, "Tester");
        TestHelpers.Teleport(g3, p3.Id, TestHelpers.Station(g3, "ship"));
        Assert.True(g3.Apply(p3.Id, TestKit.A("travel").Set("planet", "pyra"), true).Ok, "Erneute Reise");
        foreach (var o in pyraObjs) Assert.True(Rules.Obj(w3.Cur, o.Key) == null, "PYRA-Objekt bleibt nach zweitem Laden entfernt");
    }

    [Test]
    public static void Erledigte_Auftraege_werden_nach_Laden_nicht_erneut_verguetet()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        // Tutorial: ein Stück fahren, dann 5 Objekte sammeln (Belohnung 10 Credits)
        var start = p.Pos;
        TestKit.WalkTo(g, p.Id, new V3(start.x + 12, 0, start.z + 12));
        TestKit.Ticks(g, 1f);
        Assert.Equal(2, g.S.Missions["tut_move"].Status, "Aufwachen erledigt");
        foreach (var o in TestKit.Grabbables(g, 5)) Assert.True(TestHelpers.Grab(g, p, o).Ok, "Aufheben");
        TestKit.Ticks(g, 1f);
        Assert.Equal(2, g.S.Missions["tut_collect"].Status, "Erste Handgriffe erledigt");
        Assert.True(g.S.Missions["tut_collect"].Claimed, "Belohnung ausgezahlt");
        // Recyclingauftrag an der Auftragstafel
        g.S.Cur.Store("glas").S = 40;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "contracts"));
        var rc = g.Apply(p.Id, TestKit.A("contract"), true);
        Assert.True(rc.Ok, "Auftrag: " + rc.Err);
        long credits = g.S.Credits;
        long earned = g.S.Stat("credEarned");
        Assert.True(earned >= 10 + rc.Data.Int("credits"), "Belohnungen verbucht");

        for (int round = 0; round < 3; round++)
        {
            var w2 = RoundTrip(g.S);
            var g2 = new Game(w2);
            var p2 = g2.Join(p.Id, "Tester");
            TestKit.Ticks(g2, 10f);
            Assert.Equal(credits, w2.Credits, "Nach Laden keine erneute Vergütung (Runde " + round + ")");
            Assert.Equal(earned, w2.Stat("credEarned"), "Verdienst-Statistik unverändert");
            Assert.Equal(2, w2.Missions["tut_collect"].Status, "Auftrag bleibt erledigt");
            Assert.True(w2.Missions["tut_collect"].Claimed, "Belohnung bleibt abgeholt");
            Assert.Equal(1, w2.Cur.ContractIdx, "Nächster Recyclingauftrag bleibt bestehen");
            string mat; int n, reward;
            Rules.Contract("terra", w2.Cur.ContractIdx, out mat, out n, out reward);
            Assert.True(mat != "glas" || n != 20, "Der erledigte Recyclingauftrag kommt nicht wieder");
            g = g2; p = p2;
        }
    }

    [Test]
    public static void Neustart_setzt_an_derselben_Stelle_fort()
    {
        // Wie im Spiel: Welt speichern, Prozess beenden, über HostServer + lokalen Client neu starten.
        string dir = TestKit.TempDir("resume");
        try
        {
            var store = new SaveStore(dir);
            PlayerData p;
            var g = TestHelpers.NewGame(out p, "profil-1");
            TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "ship"));
            Assert.True(g.Apply(p.Id, TestKit.A("travel").Set("planet", "pyra"), true).Ok, "Reise nach PYRA");
            // Bis zur Nacht spielen, am Stützpunkt schlafen → Tag-Versatz entsteht
            TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
            int guard = 0;
            while (!Rules.IsNight(g.S, "pyra") && guard++ < 10000) g.Tick(0.5f);
            Assert.True(g.Apply(p.Id, TestKit.A("sleep"), true).Ok, "Schlafen am Stützpunkt");
            g.Tick(0.3f);
            Assert.True(g.S.Cur.DayOffset > 0, "Tag-Versatz durch Schlafen");
            // Irgendwo draußen stehen bleiben
            var spot = new V3(30f, 0, -80f);
            spot.y = Terrain.HeightAt("pyra", spot.x, spot.z);
            TestKit.WalkTo(g, p.Id, spot, 0.05f);
            g.S.Credits = 1234;
            var savedPos = p.Pos;
            double offset = g.S.Cur.DayOffset;
            float phase = Rules.DayPhase(g.S, "pyra");
            string err;
            Assert.True(store.Save("auto", g.S, out err), "Speichern: " + err);

            // Neustart
            bool fromBackup;
            var w = store.Load("auto", out err, out fromBackup);
            Assert.True(w != null, "Laden: " + err);
            var host = new HostServer(w, "profil-1");
            var client = new GameClient(host.ConnectLocal());
            client.Hello("profil-1", "Tester", host.Session.Code, null, null, true);
            for (int i = 0; i < 20 && !client.Joined; i++) { host.Update(0.05f); client.Update(0.05f); }
            Assert.True(client.Joined, "Lokaler Client verbunden: " + client.FatalError);
            Assert.Equal("pyra", client.W.CurrentPlanet, "Planet");
            Assert.Equal(1234L, client.W.Credits, "Credits");
            Assert.Equal(Math.Round(offset, 2), Math.Round(client.W.Planet("pyra").DayOffset, 2), "Tag-Versatz");
            Assert.True(Math.Abs(Rules.DayPhase(client.W, "pyra") - phase) < 0.001f, "Tageszeit beim Client (" + phase + " / " + Rules.DayPhase(client.W, "pyra") + ")");
            TestKit.Near(savedPos, client.Me.Pos, 0.05f, "Position im Snapshot für den Client");
            TestKit.Near(savedPos, host.Session.Game.S.Players["profil-1"].Pos, 0.05f, "Position auf dem Server");
            // Weiterfahren von dort ist gültig (keine Korrektur)
            var next = new V3(savedPos.x + 0.5f, Terrain.HeightAt("pyra", savedPos.x + 0.5f, savedPos.z), savedPos.z);
            Assert.True(host.Session.Game.Move("profil-1", next, 0, false, 0, "grab", 0.1f), "Bewegung ab gespeicherter Position wird akzeptiert");
            host.Shutdown("Testende");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Test]
    public static void Gespeicherte_Position_hinter_geschlossenem_Tor_faellt_auf_Stuetzpunkt_zurueck()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        Assert.False(Rules.GateOpen(g.S.Cur, 0), "Testvoraussetzung: Tor 0 geschlossen");
        p.Pos = new V3(20, Terrain.HeightAt("terra", 20, 10), 10); // Bereich 1 (hinter dem Tor)
        var w2 = RoundTrip(g.S);
        var g2 = new Game(w2);
        var p2 = g2.Join(p.Id, "Tester");
        Assert.True(WorldGen.Get("terra").Base.InBase(p2.Pos.x, p2.Pos.z), "Unerreichbare Position → Start am Stützpunkt (" + p2.Pos + ")");
        // Ungültige Werte ebenso
        p2.Pos = new V3(float.NaN, 0, 0);
        g2.Leave(p2.Id);
        var p3 = g2.Join(p.Id, "Tester");
        Assert.True(p3.Pos.IsFinite && WorldGen.Get("terra").Base.InBase(p3.Pos.x, p3.Pos.z), "Ungültige Position → Stützpunkt");
    }
}
