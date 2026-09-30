using System;
using System.Collections.Generic;
using System.Linq;
using RePlanet.Core;

/// <summary>Tag/Nacht, Energie ohne Unterschlupf, Notabschaltung + Abschleppen, Schlafen, Notunterschlupf, Stürme.</summary>
public static class WeatherTests
{
    /// <summary>Tickt und sammelt alle Effekt-Ereignisse (fx) der Patches.</summary>
    static List<JObj> Run(Game g, float seconds, float dt = 0.25f, Func<bool> until = null)
    {
        var fx = new List<JObj>();
        for (float t = 0; t < seconds; t += dt)
        {
            g.Tick(dt);
            var patch = g.BuildPatch();
            if (patch != null && patch.Arr("fx") != null) foreach (var f in patch.Arr("fx")) fx.Add((JObj)f);
            if (until != null && until()) break;
        }
        return fx;
    }

    static bool Has(List<JObj> fx, string kind) { return fx.Any(f => f.Str("k") == kind); }

    /// <summary>Freie Stelle im Gelände ohne Unterschlupf in der Nähe (Bereich 0).</summary>
    static V3 OpenSpot(Game g, float minDistToShelter = 13f, V3? awayFrom = null, bool buildable = true)
    {
        var ps = g.S.Cur;
        for (float z = -56; z > -150; z -= 3)
            for (float x = -140; x < 140; x += 3)
            {
                var pos = new V3(x, Terrain.HeightAt(ps.Id, x, z), z);
                if (awayFrom.HasValue && V3.DistXZ(pos, awayFrom.Value) < 20f) continue;
                if (Rules.ShelterKind(g.S, ps, pos) != 0) continue;
                if (buildable && Rules.CanBuildShelter(g.S, ps, pos) != null) continue;
                if (!buildable && WorldGen.Get(ps.Id).BlockedStatic(pos.x, pos.z, 1.8f)) continue;
                V3 at; float d;
                Rules.NearestShelter(g.S, ps, pos, out at, out d);
                if (d < minDistToShelter) continue;
                return pos;
            }
        throw new Exception("Keine freie Stelle gefunden");
    }

    static void RunUntilNight(Game g)
    {
        int guard = 0;
        while (!Rules.IsNight(g.S, g.S.CurrentPlanet) && guard++ < 20000) g.Tick(0.5f);
        Assert.True(Rules.IsNight(g.S, g.S.CurrentPlanet), "Nacht erreicht");
    }

    [Test]
    public static void Nacht_beginnt_nach_der_Tageslaenge()
    {
        foreach (var pl in GameData.PlanetOrder)
        {
            var def = GameData.Planets[pl];
            var w = Game.NewWorld("Nacht", def.StartPlanet ? pl : "terra");
            // Ein neues Spiel beginnt am Vormittag
            Assert.False(Rules.IsNight(w, pl), pl + ": Spielbeginn ist Tag");
            double toNight = (Rules.NightStart - 0.3) * def.DayLength;
            w.PlayTime = toNight - 1; Assert.False(Rules.IsNight(w, pl), pl + ": kurz vor Nachtbeginn noch Tag");
            w.PlayTime = toNight + 1; Assert.True(Rules.IsNight(w, pl), pl + ": Nacht nach " + toNight + " s");
            double toMorning = (1 + Rules.NightEnd - 0.3) * def.DayLength;
            w.PlayTime = toMorning - 1; Assert.True(Rules.IsNight(w, pl), pl + ": vor Tagesanbruch noch Nacht");
            w.PlayTime = toMorning + 1; Assert.False(Rules.IsNight(w, pl), pl + ": danach wieder Tag");
            w.PlayTime = toNight + def.DayLength + 1; Assert.True(Rules.IsNight(w, pl), pl + ": Zyklus wiederholt sich");
            w.PlayTime = (1.0 - 0.3) * def.DayLength; // Mitternacht
            Assert.True(Rules.Darkness(Rules.DayPhase(w, pl)) > 0.99f, pl + ": um Mitternacht dunkel");
            w.PlayTime = (0.5 - 0.3) * def.DayLength; // Mittag
            Assert.True(Rules.Darkness(Rules.DayPhase(w, pl)) < 0.01f, pl + ": mittags hell");
        }
        // Durch die Simulation: Ereignis „nightfall“ zur richtigen Zeit
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage")); // geschützt – kein Energieverlust stört
        double expected = (Rules.NightStart - 0.3) * GameData.Planets["terra"].DayLength;
        var fx = Run(g, (float)expected + 5, 0.5f, () => Rules.IsNight(g.S, "terra"));
        Assert.True(Has(fx, "nightfall"), "Ereignis Nachteinbruch");
        Assert.True(Math.Abs(g.S.PlayTime - expected) < 1.0, "Nacht nach " + g.S.PlayTime.ToString("0") + " s (erwartet " + expected.ToString("0") + " s)");
    }

    [Test]
    public static void Ohne_Unterschlupf_sinkt_nachts_die_Energie()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, p.Id, spot);
        // Tagsüber im Stillstand: keine Entladung (leichtes Nachladen)
        float e0 = p.Energy = 80;
        Run(g, 10f);
        Assert.False(p.Exposed, "Tagsüber nicht ausgesetzt");
        Assert.True(p.Energy >= e0, "Tagsüber kein Verlust (" + p.Energy + ")");
        RunUntilNight(g);
        Run(g, 1f);
        Assert.True(p.Exposed, "Nachts ohne Unterschlupf ausgesetzt");
        float e1 = p.Energy;
        Run(g, 20f);
        float drop = e1 - p.Energy;
        Assert.True(drop > 8f && drop < 14f, "Nachts sinkt die Energie (≈0,55/s): " + drop.ToString("0.0") + " in 20 s");
        // Im Unterschlupf (Stützpunkt) nicht mehr – dort wird geladen
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        float e2 = p.Energy;
        Run(g, 2f);
        Assert.False(p.Exposed, "Im Stützpunkt geschützt");
        Assert.True(p.Energy > e2, "Am Stützpunkt wird geladen");
        // Im Fahrzeug gilt man als geschützt
        g.S.OwnedVehicles.Add("rover"); g.EnsureVehicles();
        var rover = g.S.Cur.Vehicles["rover"];
        TestHelpers.Teleport(g, p.Id, new V3(rover.Pos.x + 1, rover.Pos.y, rover.Pos.z));
        Assert.True(g.Apply(p.Id, TestKit.A("venter").Set("v", "rover"), true).Ok, "Einsteigen");
        TestHelpers.Teleport(g, p.Id, spot);
        float e3 = p.Energy;
        Run(g, 5f);
        Assert.False(p.Exposed, "Im Fahrzeug nicht ausgesetzt");
        Assert.True(p.Energy >= e3 - 0.01f, "Im Fahrzeug kein Nachtverlust");
    }

    [Test]
    public static void Leerer_Akku_nachts_Notabschaltung_und_Abschleppen()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, p.Id, spot);
        RunUntilNight(g);
        p.Energy = 3f;
        var fx = Run(g, 30f, 0.25f, () => p.TowTimer > 0);
        Assert.True(p.TowTimer > 0, "Notabschaltung bei leerem Akku");
        Assert.True(Has(fx, "shutdown"), "Ereignis Notabschaltung");
        Assert.Equal(1L, g.S.Stat("shutdowns"), "Notabschaltung gezählt");
        // Während der Abschaltung: keine Bewegung, kein Schlafen
        Assert.False(g.Move(p.Id, new V3(spot.x + 1, spot.y, spot.z), 0, false, 0, "grab", 0.2f), "Bewegung abgelehnt");
        Assert.False(g.Apply(p.Id, TestKit.A("sleep"), true).Ok, "Schlafen abgelehnt");
        Assert.False(g.Apply(p.Id, TestKit.A("press"), true).Ok, "Werkzeug-/Weltaktionen abgelehnt");
        long credits = g.S.Credits; int bin = p.Bin.Count;
        fx = Run(g, 10f, 0.25f, () => p.TowTimer <= 0);
        Assert.True(Has(fx, "towed"), "Ereignis Abschleppen");
        var charge = TestHelpers.Station(g, "charge");
        TestKit.Near(charge, p.Pos, 0.01f, "Abgeschleppt zum Ladeplatz");
        Assert.True(Math.Abs(p.Energy - g.S.MaxEnergy * 0.4f) < 1f, "Energie teilweise wiederhergestellt (" + p.Energy + ")");
        Assert.False(Rules.IsNight(g.S, "terra"), "Allein im Spiel: Die Nacht ist beim Abschleppen vergangen (Zeitverlust)");
        Assert.Equal(credits, g.S.Credits, "Nichts verloren (Credits)");
        Assert.Equal(bin, p.Bin.Count, "Nichts verloren (Behälter)");
        // Danach wieder normal spielbar und am Ladeplatz geladen
        Assert.True(g.Move(p.Id, new V3(charge.x + 0.5f, charge.y, charge.z), 0, false, 0, "grab", 0.2f), "Bewegung wieder möglich");
        Run(g, 10f);
        Assert.True(p.Energy > g.S.MaxEnergy * 0.95f, "Am Ladeplatz vollgeladen (" + p.Energy + ")");
    }

    [Test]
    public static void Notabschaltung_im_Koop_ueberspringt_die_Nacht_nicht()
    {
        var g = new Game(Game.NewWorld("Koop"));
        var a = g.Join("a", "A"); var b = g.Join("b", "B");
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, "a", spot);
        TestHelpers.Teleport(g, "b", TestHelpers.Station(g, "storage"));
        RunUntilNight(g);
        a.Energy = 1f;
        Run(g, 20f, 0.25f, () => a.TowTimer <= 0 && g.S.Stat("shutdowns") > 0);
        Assert.Equal(1L, g.S.Stat("shutdowns"), "Abschaltung");
        TestKit.Near(TestHelpers.Station(g, "charge"), a.Pos, 0.01f, "Abgeschleppt");
        Assert.True(Rules.IsNight(g.S, "terra"), "Mitspieler wach → die Nacht läuft weiter");
    }

    [Test]
    public static void Schlafen_nur_im_Unterschlupf_und_nur_nachts()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        var rd = g.Apply(p.Id, TestKit.A("sleep"), true);
        Assert.False(rd.Ok, "Tagsüber ohne Sturm kein Schlaf");
        Assert.True(rd.Err.Contains("nicht müde"), "Meldung: " + rd.Err);
        RunUntilNight(g);
        TestHelpers.Teleport(g, p.Id, spot);
        g.Tick(0.3f);
        var ro = g.Apply(p.Id, TestKit.A("sleep"), true);
        Assert.False(ro.Ok, "Schlafen außerhalb eines Unterschlupfs abgelehnt");
        Assert.True(ro.Err.Contains("Unterschlupf"), "Meldung nennt Abhilfe: " + ro.Err);
        Assert.False(p.Sleeping, "Schläft nicht");
        // Im Fahrzeug ebenfalls nicht
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        g.S.OwnedVehicles.Add("rover"); g.EnsureVehicles();
        var rover = g.S.Cur.Vehicles["rover"];
        TestHelpers.Teleport(g, p.Id, new V3(rover.Pos.x + 1, rover.Pos.y, rover.Pos.z));
        Assert.True(g.Apply(p.Id, TestKit.A("venter").Set("v", "rover"), true).Ok, "Einsteigen");
        Assert.False(g.Apply(p.Id, TestKit.A("sleep"), true).Ok, "Im Fahrzeug kein Schlaf");
        g.Apply(p.Id, TestKit.A("vexit"), true);
        // Im Unterschlupf des Geländes
        var shelter = WorldGen.Get("terra").Shelters[0];
        TestHelpers.Teleport(g, p.Id, shelter.Pos);
        g.Tick(0.3f);
        Assert.Equal(2, Rules.ShelterKind(g.S, g.S.Cur, p.Pos), "Unterschlupf im Gelände erkannt");
        p.Energy = 30;
        var rs = g.Apply(p.Id, TestKit.A("sleep"), true);
        Assert.True(rs.Ok, "Schlafen im Unterschlupf: " + rs.Err);
        var fx = Run(g, 1f);
        Assert.True(Has(fx, "morning"), "Morgen-Ereignis");
        Assert.False(Rules.IsNight(g.S, "terra"), "Nach dem Schlafen ist Tag");
        Assert.True(Rules.DayPhase(g.S, "terra") >= Rules.NightEnd && Rules.DayPhase(g.S, "terra") < 0.35f, "Es ist Morgen (" + Rules.DayPhase(g.S, "terra") + ")");
        Assert.False(p.Sleeping, "Aufgewacht");
        Assert.True(p.Energy >= g.S.MaxEnergy - 0.01f, "Ausgeruht mit vollem Akku");
        Assert.True(g.S.Cur.DayOffset > 0, "Tag-Versatz gespeichert");
        Assert.Equal("Nach dem Schlafen", g.SaveReason, "Speicherpunkt nach dem Schlafen");
    }

    [Test]
    public static void Im_Koop_wird_es_erst_Morgen_wenn_alle_schlafen()
    {
        var g = new Game(Game.NewWorld("Koop"));
        var a = g.Join("a", "A"); var b = g.Join("b", "B"); var c = g.Join("c", "C");
        g.Leave("c"); // offline zählt nicht mit
        var storage = TestHelpers.Station(g, "storage");
        TestHelpers.Teleport(g, "a", storage);
        TestHelpers.Teleport(g, "b", new V3(storage.x + 2, storage.y, storage.z));
        RunUntilNight(g);
        var r = g.Apply("a", TestKit.A("sleep"), true);
        Assert.True(r.Ok && r.Data.Int("sleeping") == 1 && r.Data.Int("online") == 2, "A schläft (1/2)");
        Run(g, 5f);
        Assert.True(Rules.IsNight(g.S, "terra"), "Solange B wach ist, bleibt es Nacht");
        Assert.True(a.Sleeping, "A schläft weiter");
        // A verlässt den Unterschlupf → wacht auf
        TestHelpers.Teleport(g, "a", new V3(storage.x, storage.y, storage.z + 30));
        Run(g, 1f);
        Assert.False(a.Sleeping, "Wer den Unterschlupf verlässt, wacht auf");
        TestHelpers.Teleport(g, "a", storage);
        Assert.True(g.Apply("a", TestKit.A("sleep"), true).Ok, "A schläft wieder");
        Assert.True(g.Apply("b", TestKit.A("sleep"), true).Ok, "B schläft");
        var fx = Run(g, 1f);
        Assert.True(Has(fx, "morning"), "Alle schlafen → Morgen");
        Assert.False(Rules.IsNight(g.S, "terra"), "Tag");
        Assert.False(a.Sleeping || b.Sleeping, "Beide wach");
    }

    [Test]
    public static void Notunterschlupf_Kosten_Abstand_Hoechstzahl()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        // Am Stützpunkt nicht nötig
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        var rb = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.True(!rb.Ok && rb.Err.Contains("Stützpunkt"), "Am Stützpunkt abgelehnt: " + rb.Err);
        // Zu wenig Geld
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, p.Id, spot);
        g.S.Credits = Rules.ShelterCost - 1;
        var rm = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.True(!rm.Ok && rm.Err.Contains("Credits"), "Zu wenig Credits: " + rm.Err);
        Assert.Equal((long)Rules.ShelterCost - 1, g.S.Credits, "Nichts abgebucht");
        // Bauen
        g.S.Credits = 10000;
        long c0 = g.S.Credits;
        var ok = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.True(ok.Ok, "Notunterschlupf gebaut: " + ok.Err);
        Assert.Equal(c0 - Rules.ShelterCost, g.S.Credits, "Kosten " + Rules.ShelterCost + " Credits");
        Assert.Equal(1, g.S.Cur.Shelters.Count, "Gespeichert im Planetenzustand");
        Assert.Equal(2, Rules.ShelterKind(g.S, g.S.Cur, p.Pos), "Dort ist man geschützt");
        // Mindestabstand
        var l = WorldGen.Get("terra");
        V3 close = spot; bool found5 = false;
        for (int k = 0; k < 64 && !found5; k++)
        {
            float ang = k * 0.4f, r = 5f + (k % 6);
            var c = new V3(spot.x + M.Cos(ang) * r, 0, spot.z + M.Sin(ang) * r);
            c.y = Terrain.HeightAt("terra", c.x, c.z);
            if (!l.Base.InBase(c.x, c.z) && !l.BlockedStatic(c.x, c.z, 1.8f)) { close = c; found5 = true; }
        }
        Assert.True(found5, "Freie Stelle in 5–11 m Abstand gefunden");
        TestHelpers.Teleport(g, p.Id, close);
        var near = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.True(!near.Ok && near.Err.Contains("Nähe"), "Mindestabstand: " + near.Err);
        // Neben einem Unterschlupf des Geländes ebenfalls nicht
        var ls = WorldGen.Get("terra").Shelters.First(s => s.Area == 0);
        TestHelpers.Teleport(g, p.Id, new V3(ls.Pos.x + 4, Terrain.HeightAt("terra", ls.Pos.x + 4, ls.Pos.z), ls.Pos.z));
        var nearL = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.False(nearL.Ok, "Neben vorhandenem Unterschlupf abgelehnt");
        // Höchstzahl: bis 8 bauen, der neunte wird abgelehnt
        int guard = 0;
        while (g.S.Cur.Shelters.Count < Rules.MaxShelters && guard++ < 20)
        {
            var s = OpenSpot(g);
            TestHelpers.Teleport(g, p.Id, s);
            var r = g.Apply(p.Id, TestKit.A("shelter"), true);
            Assert.True(r.Ok, "Weiterer Notunterschlupf: " + r.Err);
        }
        Assert.Equal(Rules.MaxShelters, g.S.Cur.Shelters.Count, "Höchstzahl erreicht");
        var s9 = OpenSpot(g, 13f, null, false);
        TestHelpers.Teleport(g, p.Id, s9);
        long c1 = g.S.Credits;
        var r9 = g.Apply(p.Id, TestKit.A("shelter"), true);
        Assert.True(!r9.Ok && r9.Err.Contains("Höchstens"), "Neunter abgelehnt: " + r9.Err);
        Assert.Equal(c1, g.S.Credits, "Ablehnung kostet nichts");
        // Nachts im eigenen Unterschlupf schlafen
        TestHelpers.Teleport(g, p.Id, g.S.Cur.Shelters[0]);
        RunUntilNight(g);
        Assert.True(g.Apply(p.Id, TestKit.A("sleep"), true).Ok, "Schlafen im Notunterschlupf");
        Run(g, 1f);
        Assert.False(Rules.IsNight(g.S, "terra"), "Morgen");
        // Gespeichert und geladen
        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(g.S), out err);
        Assert.Equal(Rules.MaxShelters, w2.Cur.Shelters.Count, "Notunterschlüpfe im Spielstand");
        // Auf PELAGIA nicht im Wasser
        var gp = new Game(Game.NewWorld("See", "pelagia"));
        var pp = gp.Join("p", "P");
        gp.S.Credits = 1000;
        V3 water = V3.Zero; bool found = false;
        for (float x = -140; x < 140 && !found; x += 4)
            for (float z = -140; z < 140 && !found; z += 4)
                if (Terrain.HeightAt("pelagia", x, z) < -3f && !WorldGen.Get("pelagia").Base.InBase(x, z)) { water = new V3(x, -0.35f, z); found = true; }
        Assert.True(found, "Wasserstelle gefunden");
        TestHelpers.Teleport(gp, "p", water);
        var rw = gp.Apply("p", TestKit.A("shelter"), true);
        Assert.True(!rw.Ok && rw.Err.Contains("Wasser"), "Nicht im Wasser: " + rw.Err);
    }

    [Test]
    public static void Sturmzyklus_Warnung_Sturm_Ende_und_Duenenwechsel()
    {
        var g = new Game(Game.NewWorld("Wüste", "pyra"));
        var p = g.Join("p", "P");
        var def = GameData.Planets["pyra"];
        var ps = g.S.Cur;
        TestHelpers.Teleport(g, "p", TestHelpers.Station(g, "storage"));
        var env = new MotorEnv("pyra");
        env.Sync(g.S, null);
        int set0 = env.ActiveDuneSet;
        Assert.Equal(0, set0, "Anfangs Dünen-Set 0");
        var dune0 = WorldGen.Get("pyra").Colliders.First(b => b.DuneSet == 0);
        var dune1 = WorldGen.Get("pyra").Colliders.First(b => b.DuneSet == 1);
        Assert.True(env.Solid(dune0) && !env.Solid(dune1), "Nur Set 0 blockiert");

        var fx = Run(g, def.StormEvery - 31f, 0.5f);
        Assert.False(ps.StormWarn || ps.StormActive, "Vor der Warnzeit ruhig");
        fx = Run(g, 2f, 0.5f);
        Assert.True(ps.StormWarn && !ps.StormActive, "Sturmwarnung 30 s vorher");
        Assert.True(Has(fx, "stormwarn"), "Ereignis Warnung");
        float w; float dx, dz;
        w = Rules.Wind(g.S, "pyra", out dx, out dz);
        Assert.True(w >= 0.35f, "Wind frischt bei Warnung auf (" + w + ")");
        fx = Run(g, 30f, 0.5f, () => ps.StormActive);
        Assert.True(ps.StormActive, "Sturm beginnt");
        Assert.True(fx.Any(f => f.Str("k") == "storm" && f.Bool("on")), "Ereignis Sturmbeginn");
        Assert.True(Rules.Wind(g.S, "pyra", out dx, out dz) >= 0.75f, "Sturmwind");
        // Draußen im Sturm (auch tagsüber) ausgesetzt, am Stützpunkt nicht
        Assert.False(Rules.IsNight(g.S, "pyra"), "Testvoraussetzung: tagsüber");
        Assert.False(p.Exposed, "Am Stützpunkt geschützt");
        var spot = OpenSpot(g);
        TestHelpers.Teleport(g, "p", spot);
        float e0 = p.Energy;
        Run(g, 10f);
        Assert.True(p.Exposed, "Im Sturm ohne Unterschlupf ausgesetzt");
        Assert.True(e0 - p.Energy > 7f, "Sturm entlädt (≈0,9/s): " + (e0 - p.Energy).ToString("0.0"));
        TestHelpers.Teleport(g, "p", TestHelpers.Station(g, "storage"));
        fx = Run(g, def.StormDuration + 5, 0.5f, () => !ps.StormActive);
        Assert.False(ps.StormActive, "Sturm endet nach " + def.StormDuration + " s");
        Assert.Equal(1, ps.StormCount, "Sturmzähler");
        var end = fx.FirstOrDefault(f => f.Str("k") == "storm" && !f.Bool("on"));
        Assert.True(end != null && end.Bool("dunes"), "Ende-Ereignis meldet Dünenwechsel (PYRA)");
        env.Sync(g.S, null);
        Assert.Equal(1, env.ActiveDuneSet, "Dünen-Set gewechselt");
        Assert.True(!env.Solid(dune0) && env.Solid(dune1), "Jetzt blockiert Set 1");
        // Nächster Sturm (tagsüber): „Schlafen“ im Unterschlupf heißt abwarten – der Sturm wird nicht übersprungen
        Run(g, def.StormEvery + 2, 0.5f, () => ps.StormActive);
        Assert.True(ps.StormActive, "Zweiter Sturm");
        Assert.False(Rules.IsNight(g.S, "pyra"), "Testvoraussetzung: zweiter Sturm tagsüber");
        var rs = g.Apply("p", TestKit.A("sleep"), true);
        Assert.True(rs.Ok, "Im Sturm abwarten erlaubt: " + rs.Err);
        Assert.True(p.Waiting && !p.Sleeping, "Wartet ab statt zu schlafen");
        Run(g, 1f);
        Assert.True(ps.StormActive, "Sturm läuft weiter (nicht verschlafbar)");
        Assert.Equal(Game.WaitTimeScale, g.TimeScale, "Zeitraffer, solange alle abwarten");
        fx = Run(g, def.StormDuration + 5, 0.5f, () => !ps.StormActive);
        Assert.False(ps.StormActive, "Sturm endet regulär");
        Assert.False(p.Waiting, "Abwarten endet mit dem Sturm");
        Assert.Equal(1f, g.TimeScale, "Zeitraffer vorbei");
        Assert.Equal(2, ps.StormCount, "Zweiter Sturm gezählt");
        env.Sync(g.S, null);
        Assert.Equal(0, env.ActiveDuneSet, "Dünen wieder Set 0");
        // Andere Planeten haben Stürme, aber keinen Dünenwechsel
        var gt = new Game(Game.NewWorld("Stadt"));
        gt.Join("t", "T");
        var fxt = Run(gt, GameData.Planets["terra"].StormEvery + GameData.Planets["terra"].StormDuration + 2, 0.5f, () => gt.S.Cur.StormCount > 0);
        Assert.Equal(1, gt.S.Cur.StormCount, "Auch TERRA hat Stürme");
        var tend = fxt.First(f => f.Str("k") == "storm" && !f.Bool("on"));
        Assert.False(tend.Bool("dunes"), "Kein Dünenwechsel auf TERRA");
        var envT = new MotorEnv("terra"); envT.Sync(gt.S, null);
        Assert.Equal(-1, envT.ActiveDuneSet, "Keine Dünen auf TERRA");
    }

    // ================================================================== Hangar und Transportschiff als Schutzräume

    /// <summary>Fährt MIKO mit dem echten Motor (Kollision) über Wegpunkte; liefert den Endzustand.</summary>
    static MoverState WalkMotor(MotorEnv env, MoverState s, params V3[] points)
    {
        const float dt = 0.05f;
        foreach (var pt in points)
            for (int i = 0; i < 400; i++)
            {
                float dx = pt.x - s.Pos.x, dz = pt.z - s.Pos.z, d = M.Sqrt(dx * dx + dz * dz);
                if (d < 0.25f) break;
                float k = Math.Min(1f, d / 0.6f);
                Motor.StepRobot(ref s, env, dx / d * k, dz / d * k, false, 0f, 1f, false, dt);
            }
        return s;
    }

    /// <summary>Lenkt ein Fahrzeug mit dem echten Motor über Wegpunkte.</summary>
    static MoverState DriveMotor(MotorEnv env, VehicleDef def, MoverState s, params V3[] points)
    {
        const float dt = 0.05f;
        foreach (var pt in points)
            for (int i = 0; i < 600; i++)
            {
                float dx = pt.x - s.Pos.x, dz = pt.z - s.Pos.z, d = M.Sqrt(dx * dx + dz * dz);
                if (d < 0.6f) break;
                float diff = M.WrapAngle(M.Atan2(dx, dz) - s.Yaw);
                float steer = M.Clamp(diff * 3f, -1f, 1f);
                float throttle = Math.Abs(diff) > 1.2f ? 0.3f : Math.Min(1f, 0.25f + d * 0.15f);
                Motor.StepVehicle(ref s, env, def, throttle, steer, dt);
            }
        return s;
    }

    static MoverState StartAt(V3 p, float yaw) { return new MoverState { Pos = p, Yaw = yaw }; }

    [Test]
    public static void Hangar_und_Schiff_sind_befahrbar_Waende_bleiben_fest()
    {
        foreach (var pl in GameData.PlanetOrder)
        {
            var w = Game.NewWorld("Test", pl);
            var l = WorldGen.Get(pl);
            var b = l.Base;
            var env = new MotorEnv(pl);
            env.Sync(w, null);
            var ps = w.Planet(pl);
            Assert.True(b.Hangar != null && b.Ship != null, pl + ": Hangar und Laderaum definiert");
            foreach (var room in b.Rooms)
            {
                string n = pl + "/" + room.Id;
                var door = new V3(room.Door.Cx, room.Door.Y0, room.Door.Cz);
                var outside = new V3(room.Outside.x, l.GroundAt(room.Outside.x, room.Outside.z), room.Outside.z);
                // Hineinlaufen durch Tor bzw. über die Rampe
                var s = WalkMotor(env, StartAt(outside, 0), door, room.Spot);
                Assert.True(V3.DistXZ(s.Pos, room.Spot) < 0.5f, n + ": MIKO erreicht den Innenraum (" + s.Pos + ")");
                Assert.Equal(room.Kind, Rules.ShelterKind(w, ps, s.Pos), n + ": Innenraum erkannt");
                Assert.True(Rules.Indoors(Rules.ShelterKind(w, ps, s.Pos)), n + ": gilt als drinnen");
                float floor = Math.Max(room.Inner.Y0, Terrain.HeightAt(pl, s.Pos.x, s.Pos.z));
                Assert.True(Math.Abs(s.Pos.y - floor) < 0.05f, n + ": steht auf dem Boden des Innenraums (y " + s.Pos.y.ToString("0.00") + " statt " + floor.ToString("0.00") + ")");
                // … und wieder hinaus
                s = WalkMotor(env, s, door, outside);
                Assert.True(V3.DistXZ(s.Pos, outside) < 0.5f, n + ": MIKO fährt wieder hinaus (" + s.Pos + ")");
                Assert.True(Rules.RoomAt(l, s.Pos) == null, n + ": draußen");
                Assert.Equal(0, Rules.Indoors(Rules.ShelterKind(w, ps, s.Pos)) ? 1 : 0, n + ": draußen nicht mehr drinnen");
                // Durch die Wand geht es nicht: quer zur Eingangsrichtung auf die Raummitte zu
                float sx = room.OutZ, sz = -room.OutX;
                float reach = Math.Abs(sx) > 0.5f ? room.Inner.Hx : room.Inner.Hz;
                var side = new V3(room.Inner.Cx + sx * (reach + 3f), 0, room.Inner.Cz + sz * (reach + 3f));
                side.y = l.GroundAt(side.x, side.z);
                var sw = WalkMotor(env, StartAt(side, 0), new V3(room.Inner.Cx, room.Inner.Y0, room.Inner.Cz));
                Assert.True(Rules.RoomAt(l, sw.Pos) == null, n + ": Wand hält MIKO auf (" + sw.Pos + ")");
                // Der Rover fährt ebenfalls hinein und rückwärts wieder hinaus
                var rover = GameData.Vehicles["rover"];
                float yawIn = M.Atan2(-room.OutX, -room.OutZ);
                var start = new V3(outside.x + room.OutX * 4f, 0, outside.z + room.OutZ * 4f);
                start.y = l.GroundAt(start.x, start.z);
                var vs = DriveMotor(env, rover, StartAt(start, yawIn), outside, door, room.Spot);
                Assert.True(Rules.RoomAt(l, vs.Pos) != null, n + ": Rover fährt hinein (" + vs.Pos + ")");
                vs.Speed = 0;
                for (int i = 0; i < 400 && Rules.RoomAt(l, vs.Pos) != null; i++) Motor.StepVehicle(ref vs, env, rover, -1f, 0f, 0.05f);
                for (int i = 0; i < 40; i++) Motor.StepVehicle(ref vs, env, rover, -1f, 0f, 0.05f);
                Assert.True(Rules.RoomAt(l, vs.Pos) == null, n + ": Rover fährt wieder hinaus (" + vs.Pos + ")");
            }
        }
    }

    [Test]
    public static void Im_Hangar_und_im_Schiff_geschuetzt_und_Schlafen_moeglich()
    {
        foreach (var roomId in new[] { "hangar", "ship" })
        {
            PlayerData p;
            var g = TestHelpers.NewGame(out p);
            var b = WorldGen.Get("terra").Base;
            var room = roomId == "hangar" ? b.Hangar : b.Ship;
            RunUntilNight(g);
            TestHelpers.Teleport(g, p.Id, room.Spot);
            p.Energy = 50f;
            Run(g, 10f);
            Assert.Equal(room.Kind, p.ShelterKind, roomId + ": Schutzraum erkannt");
            Assert.False(p.Exposed, roomId + ": nachts drinnen nicht ausgesetzt");
            Assert.True(p.Energy >= 50f - 0.01f, roomId + ": kein Nachtverbrauch (" + p.Energy.ToString("0.0") + ")");
            if (roomId == "hangar") Assert.True(p.Energy > 60f, "Hangar: Ladesäule lädt (" + p.Energy.ToString("0.0") + ")");
            // Sturm: ebenfalls geschützt
            var ps = g.S.Cur;
            ps.StormTimer = GameData.Planets["terra"].StormEvery + 1f;
            Run(g, 2f);
            Assert.True(ps.StormActive, roomId + ": Sturm läuft");
            float e = p.Energy;
            Run(g, 5f);
            Assert.False(p.Exposed, roomId + ": im Sturm drinnen nicht ausgesetzt");
            Assert.True(p.Energy >= e - 0.01f, roomId + ": kein Sturmverbrauch");
            // Schlafen überspringt die Nacht – der Sturm läuft weiter und wird drinnen abgewartet
            var rs = g.Apply(p.Id, TestKit.A("sleep"), true);
            Assert.True(rs.Ok, roomId + ": Schlafen erlaubt: " + rs.Err);
            var fx = Run(g, 1f);
            Assert.True(Has(fx, "morning"), roomId + ": Morgen nach dem Schlafen");
            Assert.False(Rules.IsNight(g.S, "terra"), roomId + ": Nacht vorbei");
            Assert.True(ps.StormActive, roomId + ": Sturm lässt sich nicht verschlafen");
            Assert.True(p.Waiting, roomId + ": nach dem Aufwachen wird der Sturm abgewartet");
            Run(g, GameData.Planets["terra"].StormDuration + 2f, 0.25f, () => !ps.StormActive);
            Assert.False(ps.StormActive || p.Waiting, roomId + ": Sturm vorbei, Abwarten beendet");
            // Draußen gelten weiter die bisherigen Regeln
            RunUntilNight(g);
            TestHelpers.Teleport(g, p.Id, OpenSpot(g));
            Run(g, 1f);
            Assert.True(p.Exposed, roomId + ": draußen nachts wieder ausgesetzt");
        }
    }

    [Test]
    public static void Im_Unterschlupf_kein_Sammeln_und_keine_Werkzeuge()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        foreach (var t in new[] { "vacuum", "magnet", "cutter", "heat", "filter" }) g.S.Tech[t] = 1;
        var o = TestHelpers.FirstGrabbable(g, p);
        var b = WorldGen.Get("terra").Base;
        foreach (var room in b.Rooms)
        {
            TestHelpers.Teleport(g, p.Id, room.Spot);
            g.Tick(0.3f);
            foreach (var a in new[] { TestKit.A("grab").Set("o", o.Key), TestKit.A("vacuum").Set("dir", Json.Arr(1, 0)), TestKit.A("magnet").Set("charge", 1).Set("dir", Json.Arr(1, 0)),
                                      TestKit.A("cut").Set("o", o.Key).Set("dt", 0.25f), TestKit.A("thaw").Set("o", o.Key).Set("dt", 0.25f), TestKit.A("filter").Set("o", o.Key).Set("dt", 0.25f) })
            {
                var r = g.Apply(p.Id, a, true);
                Assert.False(r.Ok, room.Id + ": " + a.Str("a") + " abgelehnt");
                Assert.True(r.Err.StartsWith("Im Unterschlupf"), room.Id + ": kurze Meldung (" + r.Err + ")");
            }
            Assert.Equal(0, p.Bin.Count, room.Id + ": nichts eingesammelt");
        }
        // Draußen klappt das Sammeln wie gewohnt
        var rg = TestHelpers.Grab(g, p, o);
        Assert.True(rg.Ok, "Draußen sammeln: " + rg.Err);
        Assert.Equal(1, p.Bin.Count, "Eingesammelt");
        // Einlagern ist kein Sammeln: aus dem Hangar direkt hinter dem Tor erreicht man die Lager-Annahme
        TestHelpers.Teleport(g, p.Id, new V3(0, b.Hangar.Spot.y, -143f));
        g.Tick(0.3f);
        Assert.Equal(Rules.ShelterHangar, Rules.ShelterKind(g.S, g.S.Cur, p.Pos), "Im Hangar hinter dem Tor");
        var rd = g.Apply(p.Id, TestKit.A("deposit"), true);
        Assert.True(rd.Ok, "Einlagern aus dem Hangar: " + rd.Err);
    }

    [Test]
    public static void Aussteigen_im_Hangar_und_im_Schiff_bleibt_drinnen()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.OwnedVehicles.Add("rover"); g.EnsureVehicles();
        var l = WorldGen.Get("terra");
        foreach (var room in l.Base.Rooms)
        {
            var rover = g.S.Cur.Vehicles["rover"];
            TestHelpers.Teleport(g, p.Id, new V3(rover.Pos.x + 1, rover.Pos.y, rover.Pos.z));
            Assert.True(g.Apply(p.Id, TestKit.A("venter").Set("v", "rover"), true).Ok, room.Id + ": Einsteigen");
            // Rover im Raum, quer zur Eingangsrichtung → die Aussteigeseite zeigt auf eine Wand
            var inside = new V3(room.Inner.Cx, room.Inner.Y0, room.Inner.Cz);
            g.AllowTeleport(p.Id);
            Assert.True(g.Move(p.Id, inside, M.Atan2(room.OutZ, -room.OutX), false, 0, "grab", 0.1f), room.Id + ": Rover im Raum");
            g.Tick(0.3f);
            Assert.True(g.Apply(p.Id, TestKit.A("vexit"), true).Ok, room.Id + ": Aussteigen");
            Assert.True(room.Contains(p.Pos, -0.5f), room.Id + ": nach dem Aussteigen im Innenraum (" + p.Pos + ")");
            Assert.True(Math.Abs(p.Pos.y - l.GroundAt(p.Pos.x, p.Pos.z)) < 0.01f, room.Id + ": auf dem Boden");
        }
    }
}
