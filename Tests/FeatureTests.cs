using System;
using System.Collections.Generic;
using System.Linq;
using RePlanet.Core;

/// <summary>Lieferlimit, Sturm abwarten, Helferroboter, Erfolge, Schnellreise, Weltereignisse, alte Spielstände.</summary>
public static class FeatureTests
{
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

    static WorldState RoundTrip(WorldState w)
    {
        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(w), out err);
        Assert.True(w2 != null, "Spielstand lesbar: " + err);
        return w2;
    }

    // ================================================================== Lieferungen
    [Test]
    public static void Lieferung_kostet_Gebuehr_und_hat_Abklingzeit()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "contracts"));
        g.S.Credits = 100;
        int fee = GameData.DeliveryFee("terra");
        var r = g.Apply(p.Id, TestKit.A("delivery"), true);
        Assert.True(r.Ok, "Erste Lieferung: " + r.Err);
        Assert.Equal(100L - fee, g.S.Credits, "Gebühr abgezogen");
        Assert.Equal((long)1, g.S.Stat("deliveries"), "Statistik");
        // Teile abräumen: trotzdem noch Abklingzeit
        foreach (var d in ps.Dyn.Values.Where(d => d.Delivery).ToList()) ps.Dyn.Remove(d.Id);
        var r2 = g.Apply(p.Id, TestKit.A("delivery"), true);
        Assert.False(r2.Ok, "Sofort nachbestellen abgelehnt");
        Assert.True(r2.Err.Contains("Frachter"), "Meldung nennt Wartezeit: " + r2.Err);
        float wait;
        Assert.True(Rules.DeliveryCheck(g.S, ps, out wait) != null && wait > GameData.DeliveryCooldown - 1f, "Restzeit " + wait);
        // Abklingzeit übersteht Speichern/Laden
        var w2 = RoundTrip(g.S);
        Assert.True(Math.Abs(w2.Cur.NextDelivery - ps.NextDelivery) < 0.2, "Abklingzeit gespeichert");
        Run(g, GameData.DeliveryCooldown + 1f, 0.5f);
        g.S.Credits = fee - 1;
        var r3 = g.Apply(p.Id, TestKit.A("delivery"), true);
        Assert.False(r3.Ok, "Ohne Geld keine Lieferung");
        Assert.True(r3.Err.Contains("Liefergebühr"), "Meldung: " + r3.Err);
        g.S.Credits = fee;
        Assert.True(g.Apply(p.Id, TestKit.A("delivery"), true).Ok, "Nach der Abklingzeit wieder möglich");
        Assert.Equal(0L, g.S.Credits, "Genau die Gebühr bezahlt");
    }

    // ================================================================== Sturm abwarten
    [Test]
    public static void Sturm_nicht_verschlafbar_Abwarten_mit_Zeitraffer_im_Koop()
    {
        var g = new Game(Game.NewWorld("Sturm"));
        var a = g.Join("a", "A"); var b = g.Join("b", "B");
        var storage = TestHelpers.Station(g, "storage");
        TestHelpers.Teleport(g, "a", storage);
        TestHelpers.Teleport(g, "b", new V3(storage.x + 2, storage.y, storage.z));
        var ps = g.S.Cur;
        // Tag sicherstellen, dann Sturm auslösen
        Assert.False(Rules.IsNight(g.S, "terra"), "Testvoraussetzung: Tag");
        ps.StormTimer = GameData.Planets["terra"].StormEvery + 1f;
        Run(g, 1f);
        Assert.True(ps.StormActive, "Sturm läuft");
        Assert.True(g.Apply("a", TestKit.A("wait"), true).Ok, "A wartet ab");
        Run(g, 1f);
        Assert.Equal(1f, g.TimeScale, "Kein Zeitraffer, solange B nicht abwartet");
        Assert.True(g.Apply("b", TestKit.A("sleep"), true).Ok, "B „schläft“ → wartet tagsüber ab");
        Assert.True(b.Waiting && !b.Sleeping, "B wartet ab");
        Run(g, 1f);
        Assert.Equal(Game.WaitTimeScale, g.TimeScale, "Zeitraffer ×4, wenn alle abwarten");
        Assert.True(ps.StormActive, "Sturm wird nicht beendet");
        // Wer hinausgeht, beendet das Abwarten → kein Zeitraffer mehr
        TestHelpers.Teleport(g, "b", new V3(storage.x, storage.y, storage.z + 40));
        Run(g, 1f);
        Assert.False(b.Waiting, "B hat den Unterschlupf verlassen");
        Assert.Equal(1f, g.TimeScale, "Zeitraffer aus");
        // Abwarten draußen abgelehnt
        Assert.False(g.Apply("b", TestKit.A("wait"), true).Ok, "Abwarten nur im Unterschlupf");
        TestHelpers.Teleport(g, "b", storage);
        Assert.True(g.Apply("b", TestKit.A("wait"), true).Ok, "B wartet wieder ab");
        long before = g.S.Stat("stormsWaited");
        var fx = Run(g, GameData.Planets["terra"].StormDuration + 2f, 0.25f, () => !ps.StormActive);
        Assert.False(ps.StormActive, "Sturm endet nach seiner Dauer");
        Assert.False(a.Waiting || b.Waiting, "Abwarten beendet");
        Assert.Equal(before + 1, g.S.Stat("stormsWaited"), "Sturm im Unterschlupf abgewartet (Statistik)");
        // Ohne Sturm kein Abwarten
        Assert.False(g.Apply("a", TestKit.A("wait"), true).Ok, "Ohne Sturm nichts abzuwarten");
    }

    [Test]
    public static void Sitzung_rechnet_Zeitraffer_in_Spielzeit_um()
    {
        var w = Game.NewWorld("Raffer");
        var host = new HostServer(w, "h");
        var g = host.Session.Game;
        g.Join("h", "H");
        TestHelpers.Teleport(g, "h", TestHelpers.Station(g, "storage"));
        g.S.Cur.StormTimer = GameData.Planets["terra"].StormEvery + 1f;
        host.Session.Update(0.1f);
        host.Session.Update(0.3f);
        Assert.True(g.S.Cur.StormActive, "Sturm");
        Assert.True(g.Apply("h", TestKit.A("wait"), true).Ok, "Abwarten");
        host.Session.Update(0.3f); // Zeitraffer wird im nächsten Takt erkannt
        double t0 = g.S.PlayTime;
        for (int i = 0; i < 10; i++) host.Session.Update(0.1f);
        double adv = g.S.PlayTime - t0;
        Assert.True(adv > 3.5 && adv < 4.5, "1 s Echtzeit ≈ 4 s Spielzeit (" + adv.ToString("0.00") + ")");
    }

    // ================================================================== Helferroboter
    static Game HelperWorld(out PlayerData p, out Spot spot)
    {
        var g = TestHelpers.NewGame(out p);
        spot = WorldGen.Get("terra").Bots[0];
        g.S.Credits = 5000;
        g.S.Cur.Store("metall").S = 100; g.S.Cur.Store("elektronik").S = 40;
        return g;
    }

    static ActResult Fix(Game g, PlayerData p, Spot spot)
    {
        TestHelpers.Teleport(g, p.Id, new V3(spot.Pos.x + 1.5f, spot.Pos.y, spot.Pos.z));
        ActResult r = null;
        for (int i = 0; i < 40; i++)
        {
            g.Tick(0.25f);
            r = g.Apply(p.Id, TestKit.A("botfix").Set("s", spot.Id).Set("dt", 0.25f), true);
            if (!r.Ok || (r.Data != null && r.Data.Bool("done"))) break;
        }
        return r;
    }

    [Test]
    public static void Helferroboter_reparieren_sammeln_und_liefern_ins_Lager()
    {
        PlayerData p; Spot spot;
        var g = HelperWorld(out p, out spot);
        var ps = g.S.Cur;
        Assert.Equal(3, WorldGen.Get("terra").Bots.Count, "Drei defekte Helfer je Planet");
        foreach (var pl in GameData.PlanetOrder)
            foreach (var s in WorldGen.Get(pl).Bots)
                Assert.True(Terrain.HeightAt(pl, s.Pos.x, s.Pos.z) >= Terrain.WaterLevel(pl) && !WorldGen.Get(pl).Base.InBase(s.Pos.x, s.Pos.z), pl + ": Helfer an Land, außerhalb des Stützpunkts");
        // Ohne Material keine Reparatur
        g.S.Cur.Store("elektronik").S = 0;
        var rf = Fix(g, p, spot);
        Assert.False(rf.Ok, "Ohne Elektronik abgelehnt");
        Assert.True(rf.Err.Contains("Elektronik"), "Meldung nennt fehlendes Material: " + rf.Err);
        g.S.Cur.Store("elektronik").S = 40;
        long credits = g.S.Credits;
        var r = Fix(g, p, spot);
        Assert.True(r.Ok && r.Data.Bool("done"), "Repariert: " + r.Err);
        Assert.Equal(credits - GameData.HelperCredits("terra"), g.S.Credits, "Credits bezahlt");
        Assert.True(ps.Bots.ContainsKey(spot.Id), "Helfer läuft");
        Assert.False(g.Apply(p.Id, TestKit.A("botfix").Set("s", spot.Id), true).Ok, "Kein zweites Mal");
        // Kleinen Müll neben den Arbeitsort legen und abwarten
        int storeBefore = ps.StorageUsed();
        var drop = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            float ang = i * 1.05f;
            var pos = new V3(spot.Pos.x + M.Cos(ang) * 6f, 0, spot.Pos.z + M.Sin(ang) * 6f);
            pos.y = Terrain.HeightAt("terra", pos.x, pos.z);
            var d = new DynObj { Id = "dtest" + i, Type = "dose", Pos = pos, Area = spot.Area };
            ps.Dyn[d.Id] = d; drop.Add(d.Id);
        }
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        var fx = Run(g, 180f, 0.25f, () => drop.All(id => !ps.Dyn.ContainsKey(id)) && ps.Bots[spot.Id].Load.Count == 0);
        Assert.True(drop.All(id => !ps.Dyn.ContainsKey(id)), "Helfer hat alle Dosen aufgesammelt");
        Assert.True(fx.Any(f => f.Str("k") == "botpick"), "Aufheben sichtbar (Ereignis)");
        Assert.True(fx.Any(f => f.Str("k") == "botsend"), "Rohrpost zum Lager");
        Assert.True(ps.StorageUsed() >= storeBefore + 6, "Im Lager angekommen (" + storeBefore + " → " + ps.StorageUsed() + ")");
        // Radius: weit entfernter Müll bleibt liegen
        var far = new DynObj { Id = "dfar", Type = "dose", Pos = new V3(spot.Pos.x + GameData.HelperRadius + 8f, 0, spot.Pos.z), Area = spot.Area };
        far.Pos.y = Terrain.HeightAt("terra", far.Pos.x, far.Pos.z);
        ps.Dyn[far.Id] = far;
        Run(g, 30f);
        Assert.True(ps.Dyn.ContainsKey("dfar"), "Außerhalb des Arbeitsradius wird nichts gesammelt");
        // Positionspaket enthält den Helfer
        var pos0 = g.PosPacket();
        Assert.True(pos0.Arr("b") != null && pos0.Arr("b").Count == 1, "Helfer im Positionspaket (Koop)");
        // Folgen und neuen Arbeitsort festlegen
        var bot = ps.Bots[spot.Id];
        TestHelpers.Teleport(g, p.Id, new V3(bot.Pos.x + 2f, bot.Pos.y, bot.Pos.z));
        Assert.True(g.Apply(p.Id, TestKit.A("botfollow").Set("s", spot.Id), true).Ok, "Helfer folgt");
        var target = new V3(bot.Pos.x + 20f, 0, bot.Pos.z);
        target.y = Terrain.HeightAt("terra", target.x, target.z);
        TestKit.WalkTo(g, p.Id, target);
        Run(g, 5f);
        Assert.True(V3.DistXZ(bot.Pos, p.Pos) < 4f, "Helfer ist mitgefahren (" + V3.DistXZ(bot.Pos, p.Pos).ToString("0.0") + " m)");
        var rs = g.Apply(p.Id, TestKit.A("botstay").Set("s", spot.Id), true);
        Assert.True(rs.Ok, "Hier arbeiten: " + rs.Err);
        Assert.True(V3.DistXZ(bot.Home, target) < 5f, "Neuer Arbeitsort");
        Assert.True(bot.Follow == null, "Folgt nicht mehr");
        // Gespeichert und wieder geladen
        var w2 = RoundTrip(g.S);
        Assert.True(w2.Cur.Bots.ContainsKey(spot.Id), "Helfer gespeichert");
        Assert.True(V3.DistXZ(w2.Cur.Bots[spot.Id].Home, bot.Home) < 0.2f, "Arbeitsort gespeichert");
        Assert.True(g.S.Achievements.Contains("ach_helfer1") || Run(g, 2f) != null && g.S.Achievements.Contains("ach_helfer1"), "Erfolg „Neue Freunde“");
    }

    [Test]
    public static void Helfer_sammeln_keine_Gefahrstoffe_und_nichts_Schweres()
    {
        PlayerData p; Spot spot;
        var g = HelperWorld(out p, out spot);
        var ps = g.S.Cur;
        Assert.True(Fix(g, p, spot).Ok, "Repariert");
        var types = new[] { "farbeimer", "kuehlschrank", "stahlstueck" };
        for (int i = 0; i < types.Length; i++)
        {
            var pos = new V3(spot.Pos.x + 3f + i, 0, spot.Pos.z + 3f);
            pos.y = Terrain.HeightAt("terra", pos.x, pos.z);
            ps.Dyn["dh" + i] = new DynObj { Id = "dh" + i, Type = types[i], Pos = pos, Area = spot.Area };
        }
        Run(g, 40f);
        for (int i = 0; i < types.Length; i++) Assert.True(ps.Dyn.ContainsKey("dh" + i), types[i] + " bleibt liegen");
    }

    // ================================================================== Schnellreise
    [Test]
    public static void Schnellreise_zwischen_leuchtenden_Lichtpunkten()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur;
        var l = WorldGen.Get("terra");
        var zone = l.Zones[0];
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        var r0 = g.Apply(p.Id, TestKit.A("fasttravel").Set("z", 0), true);
        Assert.False(r0.Ok, "Ungeräumter Lichtpunkt ist kein Ziel");
        Assert.True(r0.Err.Contains("leuchtet noch nicht"), "Meldung: " + r0.Err);
        foreach (var id in zone.Objects) ps.Removed.Set(id);
        ps.RecomputeDerived();
        Assert.True(Rules.ZoneCleared(ps, 0), "Lichtpunkt geräumt");
        p.Energy = 100f;
        // Voller Behälter: nicht erlaubt
        for (int i = 0; i < 20; i++) p.Bin.Add(new Item { T = "karton" });
        var rl = g.Apply(p.Id, TestKit.A("fasttravel").Set("z", 0), true);
        Assert.False(rl.Ok, "Voller Behälter blockiert");
        Assert.True(rl.Err.Contains("Behälter"), "Meldung: " + rl.Err);
        p.Bin.Clear();
        // Sturm: gesperrt
        ps.StormActive = true;
        Assert.False(g.Apply(p.Id, TestKit.A("fasttravel").Set("z", 0), true).Ok, "Im Sturm gesperrt");
        ps.StormActive = false;
        // Reise vom Stützpunkt zum Lichtpunkt
        var from = p.Pos;
        float cost; V3 dest;
        Assert.True(Rules.FastTravelCheck(g.S, ps, p, 0, out cost, out dest) == null, "Prüfung ok");
        var r = g.Apply(p.Id, TestKit.A("fasttravel").Set("z", 0), true);
        Assert.True(r.Ok, "Schnellreise: " + r.Err);
        Assert.True(V3.DistXZ(p.Pos, zone.Center) < 12f, "Am Lichtpunkt angekommen");
        Assert.True(Math.Abs(p.Energy - (100f - cost)) < 0.01f, "Energie abgezogen (" + cost.ToString("0.0") + ")");
        Assert.False(l.BlockedStatic(p.Pos.x, p.Pos.z, 0.5f), "Ankunft nicht in einer Wand");
        Assert.Equal((long)1, g.S.Stat("fastTravels"), "Statistik");
        // Normale Bewegung danach wird angenommen (kein Rücksetzen)
        Assert.True(g.Move(p.Id, new V3(p.Pos.x + 0.3f, p.Pos.y, p.Pos.z), 0, false, 0, "grab", 0.1f), "Bewegung nach der Reise");
        // Mitten im Gelände (kein Lichtpunkt) geht es nicht
        TestKit.Ticks(g, 6f);
        var open = new V3(0, 0, 20); open.y = Terrain.HeightAt("terra", 0, 20);
        TestHelpers.Teleport(g, p.Id, open);
        var rf = g.Apply(p.Id, TestKit.A("fasttravel").Set("z", -1), true);
        Assert.False(rf.Ok, "Start nur an Lichtpunkt oder Stützpunkt");
        // Zurück vom Lichtpunkt zum Stützpunkt
        TestHelpers.Teleport(g, p.Id, dest);
        TestKit.Ticks(g, 6f);
        var rb = g.Apply(p.Id, TestKit.A("fasttravel").Set("z", -1), true);
        Assert.True(rb.Ok, "Zurück zum Stützpunkt: " + rb.Err);
        Assert.True(l.Base.InBase(p.Pos.x, p.Pos.z), "Am Stützpunkt");
        Assert.True(from.x != 0 || from.z != 0, "Startpunkt gesetzt");
    }

    // ================================================================== Weltereignisse
    [Test]
    public static void Weltereignisse_Meteoriten_Versorgung_Deponie()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "storage"));
        // Vor der Einführungszeit keine Ereignisse
        var fx = Run(g, 60f, 0.5f);
        Assert.False(fx.Any(f => f.Str("k") == "meteor" || f.Str("k") == "supply"), "In den ersten Minuten keine Ereignisse");
        g.S.PlayTime = GameData.EventFirstAfter + 1;
        fx = Run(g, 2f, 0.5f);
        Assert.True(ps.NextEvent > g.S.PlayTime, "Erstes Ereignis geplant");
        g.S.PlayTime = ps.NextEvent + 0.1;
        ps.StormActive = false; ps.StormWarn = false; ps.StormTimer = 0;
        fx = Run(g, 1f, 0.5f);
        var ev = fx.FirstOrDefault(f => f.Str("k") == "meteor" || f.Str("k") == "supply");
        Assert.True(ev != null, "Ereignis ausgelöst und angekündigt");
        int lying = ps.Dyn.Values.Count(d => d.Ev > 0);
        Assert.True(lying >= 1, "Ereignisfunde liegen in der Welt (" + lying + ")");
        var l = WorldGen.Get("terra");
        foreach (var d in ps.Dyn.Values.Where(d => d.Ev > 0))
        {
            Assert.False(l.Base.InBase(d.Pos.x, d.Pos.z), "Nicht im Stützpunkt");
            Assert.False(l.BlockedStatic(d.Pos.x, d.Pos.z, 0.5f), "Nicht in Gebäuden");
            Assert.True(d.Area == 0, "Nur im zugänglichen Bereich (Tore zu)");
        }
        // Gezielt: Meteoritenschauer und Deponie
        Assert.True(g.MeteorShower(ps, new Rng(5)), "Meteoritenschauer");
        var met = ps.Dyn.Values.Where(d => d.Ev == 1).ToList();
        Assert.True(met.Count >= 6, "Mehrere Splitter (" + met.Count + ")");
        Assert.True(met.All(d => d.Type == "meteorit"), "Meteoritensplitter");
        Assert.True(g.UncoverDump(ps, new Rng(9)), "Deponie freigelegt");
        Assert.True(ps.Dyn.Values.Count(d => d.Ev == 3) >= 10, "Deponie mit vielen Teilen");
        Assert.True(g.SupplyDrop(ps, new Rng(3)), "Versorgungsabwurf");
        Assert.True(ps.Dyn.Values.Any(d => d.Ev == 2 && d.Type == "versorgungskiste"), "Versorgungskiste");
        // Einsammeln zählt für den Erfolg
        var m0 = met[0];
        TestHelpers.Teleport(g, p.Id, new V3(m0.Pos.x + 0.5f, m0.Pos.y, m0.Pos.z));
        g.Tick(0.3f);
        var rg = g.Apply(p.Id, TestKit.A("grab").Set("o", m0.Id), true);
        Assert.True(rg.Ok, "Splitter aufheben: " + rg.Err);
        Assert.Equal((long)1, g.S.Stat("eventItems"), "Ereignisfund gezählt");
        // Speichern: Markierung bleibt erhalten
        var w2 = RoundTrip(g.S);
        Assert.Equal(ps.Dyn.Values.Count(d => d.Ev > 0), w2.Cur.Dyn.Values.Count(d => d.Ev > 0), "Ereignisfunde gespeichert");
        Assert.Equal(ps.EventCount, w2.Cur.EventCount, "Ereigniszähler gespeichert");
    }

    // ================================================================== Erfolge
    [Test]
    public static void Erfolge_schalten_Kosmetik_frei_und_melden_sich()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        Assert.Equal(25, GameData.Achievements.Count, "20 Erfolge + 5 Schatz-Sätze");
        foreach (var a in GameData.Achievements)
        {
            Assert.True(GameData.Cosmetics.ContainsKey(a.Reward), a.Id + ": Belohnung existiert");
            Assert.True(GameData.Cosmetics[a.Reward].Hint.Contains(a.Name), a.Id + ": Hinweis nennt den Erfolg");
        }
        Assert.Equal(GameData.Achievements.Count, GameData.Achievements.Select(a => a.Reward).Distinct().Count(), "Jede Belohnung einmalig");
        Assert.False(g.S.Achievements.Contains("ach_sammeln1"), "Anfangs nicht erreicht");
        var def = GameData.AchievementById("ach_sammeln1");
        Assert.True(Rules.AchievementProgress(g.S, def) < 0.01f, "Fortschritt 0");
        g.S.Stats["collected"] = 50;
        Assert.True(Math.Abs(Rules.AchievementProgress(g.S, def) - 0.5f) < 0.01f, "Fortschritt 50 %");
        g.S.Stats["collected"] = 100;
        var fx = Run(g, 1.5f);
        Assert.True(g.S.Achievements.Contains("ach_sammeln1"), "Erfolg erreicht");
        Assert.True(g.S.CosmeticUnlocks.Contains(def.Reward), "Kosmetik freigeschaltet");
        var ach = fx.FirstOrDefault(f => f.Str("k") == "achievement");
        Assert.True(ach != null && ach.Str("id") == "ach_sammeln1", "Meldung für den Hinweis");
        Assert.True(fx.Any(f => f.Str("k") == "cosmetic" && f.Bool("quiet")), "Kosmetik-Meldung leise (keine Doppelmeldung)");
        // Belohnung ist anlegbar
        Assert.True(g.Apply(p.Id, TestKit.A("cosm").Set("accent", def.Reward), true).Ok, "Belohnung anlegbar");
        // Nicht doppelt
        fx = Run(g, 1.5f);
        Assert.False(fx.Any(f => f.Str("k") == "achievement"), "Keine zweite Meldung");
        // Rundlauf
        var w2 = RoundTrip(g.S);
        Assert.True(w2.Achievements.Contains("ach_sammeln1"), "Erfolg gespeichert");
        // Beim Laden eines Standes mit erreichtem Zähler: still freischalten
        w2.Stats["sorted"] = 5000;
        var g2 = new Game(w2);
        Assert.True(w2.Achievements.Contains("ach_sortieren"), "Beim Laden nachgetragen");
        var patch = g2.BuildPatch();
        bool announced = patch != null && patch.Arr("fx") != null && patch.Arr("fx").Any(f => ((JObj)f).Str("k") == "achievement");
        Assert.False(announced, "Beim Laden keine Meldungsflut");
    }

    // ================================================================== Alte Spielstände
    [Test]
    public static void Alter_Spielstand_ohne_neue_Teile_laedt()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Stats["collected"] = 12;
        var o = g.S.ToJson(true);
        // Stand wie vor den Zusatzsystemen: ohne Erfolge, Helfer, Ereignisse, Lieferabklingzeit, Ereignismarken
        o.Remove("ach");
        foreach (var kv in o.Obj("planets"))
        {
            var po = kv.Value as JObj;
            po.Remove("bots"); po.Remove("ev");
            var misc = po.Obj("misc"); misc.Remove("dn");
        }
        string text = TestKit.Envelope(o, WorldState.CurrentVersion);
        string err;
        var w = SaveCodec.Decode(text, out err);
        Assert.True(w != null, "Alter Stand lesbar: " + err);
        Assert.Equal(0, w.Achievements.Count, "Keine Erfolge");
        Assert.Equal(0, w.Cur.Bots.Count, "Keine Helfer");
        Assert.True(w.Cur.NextDelivery == 0 && w.Cur.NextEvent == 0, "Standardwerte");
        var g2 = new Game(w);
        g2.Join(p.Id, "Tester");
        TestKit.Ticks(g2, 5f);
        TestHelpers.Teleport(g2, p.Id, TestHelpers.Station(g2, "contracts"));
        Assert.True(g2.Apply(p.Id, TestKit.A("delivery"), true).Ok, "Lieferung sofort möglich");
    }
}
