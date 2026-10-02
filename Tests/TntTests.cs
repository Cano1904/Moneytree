using System;
using System.Collections.Generic;
using System.Linq;
using RePlanet.Core;

/// <summary>TNT: Kaufen, Wurfregeln, deterministische Wurfbahn, Müllberge sprengen, Treffer im Koop, Host-Einstellung, alte Spielstände.</summary>
public static class TntTests
{
    static List<JObj> Run(Game g, float seconds, float dt = 0.25f)
    {
        var fx = new List<JObj>();
        for (float t = 0; t < seconds; t += dt)
        {
            g.Tick(dt);
            var patch = g.BuildPatch();
            if (patch != null && patch.Arr("fx") != null) foreach (var f in patch.Arr("fx")) fx.Add((JObj)f);
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

    /// <summary>Freie, trockene Stelle (nicht im Stützpunkt, nicht in Gebäuden, kein Müllberg) in der Nähe.</summary>
    public static V3 FreeSpot(PlanetState ps, V3 near, float minR, float maxR, int seed = 1, float baseMargin = 8f)
    {
        var l = WorldGen.Get(ps.Id);
        var r = new Rng(seed);
        float water = Terrain.WaterLevel(ps.Id);
        for (int i = 0; i < 2000; i++)
        {
            float a = r.Range(0f, 6.283f), d = r.Range(minR, maxR);
            float x = near.x + M.Cos(a) * d, z = near.z + M.Sin(a) * d;
            if (Math.Abs(x) > 140 || Math.Abs(z) > 140) continue;
            if (x >= l.Base.MinX - baseMargin && x <= l.Base.MaxX + baseMargin && z >= l.Base.MinZ - baseMargin && z <= l.Base.MaxZ + baseMargin) continue;
            if (l.BlockedStatic(x, z, 1.2f)) continue;
            if (Rules.HeapAt(ps, new V3(x, 0, z), 1.5f) >= 0) continue;
            float y = l.GroundAt(x, z);
            if (y < water + 0.3f) continue;
            if (Rules.ShelterKind(new WorldState(), ps, new V3(x, y, z)) > 0) continue;
            return new V3(x, y, z);
        }
        throw new Exception("Keine freie Stelle bei " + near);
    }

    /// <summary>Sprengbarer Müllberg in Bereich 0 und eine Wurfposition, von der aus ein Wurf ihn sicher trifft.</summary>
    static int BlastableHeap(Game g, out V3 from, out V3 dir, out float charge)
    {
        var ps = g.S.Cur; var l = WorldGen.Get(ps.Id);
        for (int i = 0; i < l.Mounds.Count; i++)
        {
            var m = l.Mounds[i];
            if (m.Area != 0 || Rules.MoundBlastCheck(g.S, ps, i) != null) continue;
            for (int k = 0; k < 6; k++)
            {
                V3 spot;
                try { spot = FreeSpot(ps, m.Pos, m.Radius + 9f, m.Radius + 13f, 10 + k); } catch (Exception) { continue; }
                TntFlight f;
                if (Rules.TntAim(ps, spot, m.Pos, m.Radius * 0.6f, out dir, out charge, out f) && (f.Mound == i || Rules.HeapAt(ps, f.Pos, GameData.TntHeapReach) == i))
                { from = spot; return i; }
            }
        }
        throw new Exception("Kein sprengbarer Müllberg gefunden");
    }

    static JObj Throw(V3 dir, float charge) { return TestKit.A("tnt").Set("dir", Json.Arr(dir.x, dir.y, dir.z)).Set("s", charge); }

    // ================================================================== Wurfbahn
    [Test]
    public static void Wurfbahn_ist_deterministisch_und_kraft_bestimmt_die_Weite()
    {
        var g = new Game(Game.NewWorld("Bahn"));
        var ps = g.S.Cur;
        var spot = FreeSpot(ps, new V3(0, 0, -100), 0, 25);
        var dir = Rules.TntDirection(0.5f, 0.7f);
        float last = -1;
        foreach (var c in new[] { 0.1f, 0.5f, 1f })
        {
            var vel = Rules.TntVelocity(dir, c);
            var start = Rules.TntStart(spot, vel);
            var path1 = new List<V3>(); var path2 = new List<V3>();
            var a = Rules.TntSimulate(ps, start, vel, path1);
            var b = Rules.TntSimulate(ps, start, vel, path2);
            Assert.True(a.Pos.x == b.Pos.x && a.Pos.z == b.Pos.z && a.Time == b.Time && path1.Count == path2.Count, "Gleiche Eingabe → gleiche Bahn");
            Assert.True(a.Time > 0.2f && a.Time <= GameData.TntMaxFlight, "Flugzeit plausibel (" + a.Time + ")");
            Assert.True(V3.Dist(path1[path1.Count - 1], a.Pos) < 0.01f, "Bahn endet an der Ruhelage");
            if (a.Land == 0) Assert.True(Math.Abs(a.Pos.y - WorldGen.Get("terra").GroundAt(a.Pos.x, a.Pos.z)) < 0.3f, "Liegt auf dem Boden");
            float d = V3.DistXZ(a.Pos, spot);
            Assert.True(d > last, "Mehr Wurfkraft → weiter (" + d.ToString("0.0") + " m)");
            last = d;
        }
        Assert.True(last > 15f, "Voller Wurf fliegt weit (" + last.ToString("0.0") + " m)");
        // Wasser: PELAGIA, Wurf aufs Meer versinkt
        var gp = new Game(Game.NewWorld("Meer", "pelagia"));
        var pp = gp.S.Cur;
        bool sank = false;
        var lp = WorldGen.Get("pelagia");
        for (int i = 0; i < 400 && !sank; i++)
        {
            var r = new Rng(i);
            var p = new V3(r.Range(-140f, 140f), 0, r.Range(-140f, 140f));
            p.y = lp.GroundAt(p.x, p.z);
            if (p.y < 0.3f || lp.BlockedStatic(p.x, p.z, 1f)) continue;
            for (int k = 0; k < 8 && !sank; k++)
            {
                var vel = Rules.TntVelocity(Rules.TntDirection(k * 0.785f, 0.7f), 1f);
                var f = Rules.TntSimulate(pp, Rules.TntStart(p, vel), vel);
                if (f.Land == 1) { sank = true; Assert.True(Math.Abs(f.Pos.y - Terrain.WaterLevel("pelagia")) < 0.01f, "Versinkt an der Wasseroberfläche"); }
            }
        }
        Assert.True(sank, "Ein Wurf aufs Meer landet im Wasser");
    }

    // ================================================================== Kaufen und Regeln
    [Test]
    public static void TNT_kaufen_und_Wurfregeln()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur;
        g.S.Credits = 1000;
        var outside = FreeSpot(ps, new V3(0, 0, -95), 0, 20);
        TestHelpers.Teleport(g, p.Id, outside);
        var r0 = g.Apply(p.Id, TestKit.A("buytnt"), true);
        Assert.False(r0.Ok, "Unterwegs kein Kauf");
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "workshop"));
        Assert.False(g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.5f), true).Ok, "Ohne Ladung kein Wurf");
        Assert.True(g.Apply(p.Id, TestKit.A("buytnt").Set("n", 1), true).Ok, "Eine Ladung gekauft");
        Assert.Equal(1000L - GameData.TntPrice("terra"), g.S.Credits, "Preis bezahlt");
        var rf = g.Apply(p.Id, TestKit.A("buytnt").Set("n", 5), true);
        Assert.True(rf.Ok, "Auffüllen");
        Assert.Equal(GameData.TntMaxCarry, p.Tnt, "Höchstens " + GameData.TntMaxCarry + " Ladungen");
        Assert.Equal(1000L - GameData.TntPrice("terra") * GameData.TntMaxCarry, g.S.Credits, "Nur die passenden Ladungen bezahlt");
        Assert.False(g.Apply(p.Id, TestKit.A("buytnt"), true).Ok, "Vorrat voll");
        // im Stützpunkt kein Wurf
        var rb = g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.5f), true);
        Assert.False(rb.Ok, "Im Stützpunkt nicht werfen");
        Assert.True(rb.Err.Contains("Stützpunkt"), "Meldung: " + rb.Err);
        // Sturm
        TestHelpers.Teleport(g, p.Id, outside);
        ps.StormActive = true;
        Assert.False(g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.5f), true).Ok, "Im Sturm nicht werfen");
        ps.StormActive = false;
        // Wurf klappt, Abklingzeit
        var r1 = g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.4f), true);
        Assert.True(r1.Ok, "Wurf: " + r1.Err);
        Assert.Equal(GameData.TntMaxCarry - 1, p.Tnt, "Eine Ladung weniger");
        Assert.False(g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.4f), true).Ok, "Kurze Wartezeit zwischen Würfen");
        Assert.Equal(1, ps.Tnt.Count, "Eine scharfe Ladung");
        // Speichern: Ladungen und Vorrat bleiben
        var w2 = RoundTrip(g.S);
        Assert.Equal(1, w2.Cur.Tnt.Count, "Scharfe Ladung gespeichert");
        Assert.Equal(p.Tnt, w2.Players[p.Id].Tnt, "Vorrat gespeichert");
        var fx = Run(g, GameData.TntFuse + 1f);
        Assert.Equal(0, ps.Tnt.Count, "Explodiert nach der Zündschnur");
        Assert.True(fx.Any(f => f.Str("k") == "tntboom"), "Explosionsereignis");
        // Fahrzeug
        g.S.OwnedVehicles.Add("rover"); g.EnsureVehicles();
        TestHelpers.Teleport(g, p.Id, ps.Vehicles["rover"].Pos);
        Assert.True(g.Apply(p.Id, TestKit.A("venter").Set("v", "rover"), true).Ok, "Einsteigen");
        Assert.False(g.Apply(p.Id, Throw(new V3(0, 1, 1), 0.4f), true).Ok, "Nicht aus dem Fahrzeug");
    }

    // ================================================================== Müllberg sprengen
    [Test]
    public static void Muellberg_zerfaellt_in_sammelbare_Stuecke()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur; var l = WorldGen.Get("terra");
        V3 from, dir; float charge;
        int m = BlastableHeap(g, out from, out dir, out charge);
        var mound = l.Mounds[m];
        p.Tnt = 3;
        TestHelpers.Teleport(g, p.Id, from);
        float clean0 = Rules.Cleanliness(ps, 0);
        float scale0 = Rules.MoundScale(ps, m);
        var repaired = new HashSet<string>(ps.Repaired); int projects = ps.Projects.Count(kv => kv.Value.Done);
        var r = g.Apply(p.Id, Throw(dir, charge), true);
        Assert.True(r.Ok, "Wurf: " + r.Err);
        Assert.Equal(m, r.Data.Int("m", Rules.HeapAt(ps, V3.FromArr(r.Data.Floats("pos")), GameData.TntHeapReach)), "Bahn trifft den Müllberg");
        var fx = Run(g, GameData.TntFuse + 1f);
        var boom = fx.First(f => f.Str("k") == "tntboom");
        Assert.Equal(1, ps.Blasts(m), "Eine Sprengstufe");
        var pieces = ps.Dyn.Values.Where(d => d.Heap == m + 1).ToList();
        int expect = (int)Math.Round((GameData.TntPiecesPerStage + (mound.Radius >= 7f ? 2 : 0)) * (1f - GameData.TntDustShare));
        Assert.True(pieces.Count >= expect - 4 && pieces.Count <= expect, "Stücke abzüglich Staub (" + pieces.Count + " von " + expect + ")");
        Assert.Equal(pieces.Count, boom.Arr("pieces").Count, "Stücke im Ereignis");
        foreach (var d in pieces)
        {
            Assert.False(l.BlockedStatic(d.Pos.x, d.Pos.z, 0.5f), "Stück nicht in einer Wand");
            Assert.False(l.Base.InBase(d.Pos.x, d.Pos.z), "Stück nicht im Stützpunkt");
            Assert.True(V3.DistXZ(d.Pos, mound.Pos) < mound.Radius + 6f, "Stück liegt beim Müllberg");
            Assert.Equal(mound.Area, d.Area, "Bereich des Müllbergs");
        }
        Assert.True(Rules.MoundScale(ps, m) < scale0 - 0.2f, "Müllberg geschrumpft");
        Assert.True(repaired.SetEquals(ps.Repaired) && projects == ps.Projects.Count(kv => kv.Value.Done), "Reparaturen und Projekte unversehrt");
        // Abklingzeit: sofort nochmal → keine weitere Stufe
        TestKit.Ticks(g, GameData.TntThrowCooldown + 0.1f);
        TestHelpers.Teleport(g, p.Id, from);
        TntFlight f2;
        Rules.TntAim(ps, from, mound.Pos, mound.Radius * 0.5f, out dir, out charge, out f2);
        Assert.True(g.Apply(p.Id, Throw(dir, charge), true).Ok, "Zweiter Wurf");
        fx = Run(g, GameData.TntFuse + 1f);
        Assert.Equal(1, ps.Blasts(m), "Staub legt sich noch – keine zweite Stufe");
        Assert.True(fx.Any(f => f.Str("k") == "tntboom" && (f.Str("why") ?? "").Contains("Staub")), "Grund gemeldet: " + string.Join(" | ", fx.Where(f => f.Str("k") == "tntboom").Select(f => Json.Write(f))));
        // Stücke einsammeln zählt zum Hauptmüll (gedeckelt)
        g.S.Tech["grab"] = 2; g.S.Tech["bin"] = 4; g.S.Tech["hazard"] = 2;
        foreach (var d in pieces.ToList())
        {
            var o = Rules.Obj(ps, d.Id);
            if (o == null || Rules.CollectCheck(g.S, o, "grab", o.Pos, Item.Volume(p.Bin)) != null) continue;
            var rg = TestHelpers.Grab(g, p, o);
            Assert.True(rg.Ok, "Stück aufheben: " + rg.Err);
        }
        Assert.True(ps.HeapWeight[0] > 0, "Gewicht der Stücke gezählt");
        Assert.True(Rules.Cleanliness(ps, 0) > clean0, "Sauberkeit steigt durch Müllberg-Stücke");
        ps.HeapWeight[0] = 99999;
        Assert.True(Rules.HeapCredit(ps, 0) <= l.AreaWeight[0] * GameData.TntCleanShare + 0.01f, "Anteil gedeckelt");
        var w2 = RoundTrip(g.S);
        Assert.Equal(1, w2.Cur.Blasts(m), "Sprengstufe gespeichert");
        Assert.True(w2.Cur.HeapWeight[0] > 0, "Gewicht gespeichert");
        Assert.True(w2.Cur.Dyn.Values.Count(d => d.Heap == m + 1) > 0 || pieces.All(d => !ps.Dyn.ContainsKey(d.Id)), "Stück-Markierung gespeichert");
    }

    // ================================================================== Treffer im Koop, Schutz, Einstellung
    static void Setup2(out Game g, out PlayerData a, out PlayerData b, out V3 pa, out V3 pb, out V3 dir, out float charge)
    {
        g = new Game(Game.NewWorld("Koop-TNT"));
        a = g.Join("a", "Anna"); b = g.Join("b", "Ben");
        var ps = g.S.Cur;
        pb = FreeSpot(ps, new V3(0, 0, -95), 0, 30, 3);
        pa = default(V3); dir = default(V3); charge = 0;
        for (int k = 0; k < 40; k++)
        {
            var cand = FreeSpot(ps, pb, 9f, 13f, 50 + k);
            TntFlight f;
            if (Rules.TntAim(ps, cand, pb, 1.2f, out dir, out charge, out f) && f.Land == 0 && Rules.HeapAt(ps, f.Pos, GameData.TntHeapReach) < 0) { pa = cand; break; }
        }
        Assert.True(pa.x != 0 || pa.z != 0, "Wurfposition gefunden");
        TestHelpers.Teleport(g, "a", pa); TestHelpers.Teleport(g, "b", pb);
        a.Tnt = 3;
    }

    [Test]
    public static void Treffer_wirft_Mitspieler_harmlos_und_Helfer_bleiben_heil()
    {
        Game g; PlayerData a, b; V3 pa, pb, dir; float charge;
        Setup2(out g, out a, out b, out pa, out pb, out dir, out charge);
        var ps = g.S.Cur;
        // Helfer in der Nähe des Ziels
        var spot = WorldGen.Get("terra").Bots[0];
        ps.Bots[spot.Id] = new HelperBot { Id = spot.Id, Home = pb, Pos = new V3(pb.x + 1.5f, pb.y, pb.z), Timer = 99f, State = 3 };
        float energy = b.Energy; int bin = b.Bin.Count;
        b.Bin.Add(new Item { T = "dose" });
        var r = g.Apply("a", Throw(dir, charge), true);
        Assert.True(r.Ok, "Wurf: " + r.Err);
        var fx = Run(g, GameData.TntFuse + 0.6f);
        var boom = fx.First(f => f.Str("k") == "tntboom");
        Assert.True(boom.Arr("hits") != null && boom.Arr("hits").Any(h => ((List<object>)h)[0] as string == "b"), "Ben getroffen (Ereignis)");
        float moved = V3.DistXZ(b.Pos, pb);
        Assert.True(moved >= GameData.TntKnockMin * 0.5f && moved <= GameData.TntKnockMax + 0.5f, "Ben fliegt ein Stück (" + moved.ToString("0.0") + " m)");
        Assert.False(WorldGen.Get("terra").BlockedStatic(b.Pos.x, b.Pos.z, 0.4f), "Nicht in einer Wand gelandet");
        Assert.True(b.Online && b.TowTimer <= 0 && Math.Abs(b.Energy - energy) < 0.5f, "Keine Abschaltung, kein Energieverlust");
        Assert.Equal(bin + 1, b.Bin.Count, "Nichts verloren");
        Assert.True(g.Stunned("b"), "Kurz benommen");
        var rg = g.Apply("b", TestKit.A("grab").Set("o", "s0"), true);
        Assert.False(rg.Ok, "Benommen: keine Werkzeuge");
        Assert.True(rg.Err.Contains("benommen"), "Meldung: " + rg.Err);
        Assert.Equal((long)1, g.S.Stat("tntHits"), "Treffer gezählt");
        var bot = ps.Bots[spot.Id];
        Assert.True(V3.DistXZ(bot.Pos, V3.FromArr(boom.Floats("pos"))) >= GameData.TntKnockRadius, "Helfer hat sich in Sicherheit gebracht");
        Assert.True(ps.Bots.ContainsKey(spot.Id), "Helfer unversehrt");
        TestKit.Ticks(g, GameData.TntKnockAir + GameData.TntStun + 0.2f);
        Assert.False(g.Stunned("b"), "Benommenheit vergeht");
    }

    [Test]
    public static void Host_Einstellung_TNT_trifft_Mitspieler()
    {
        Game g; PlayerData a, b; V3 pa, pb, dir; float charge;
        Setup2(out g, out a, out b, out pa, out pb, out dir, out charge);
        Assert.True(g.S.TntHitsPlayers, "Standard: an");
        Assert.False(g.Apply("b", TestKit.A("tnthits").Set("v", false), false).Ok, "Nur der Host darf umstellen");
        Assert.True(g.Apply("a", TestKit.A("tnthits").Set("v", false), true).Ok, "Host stellt aus");
        Assert.False(RoundTrip(g.S).TntHitsPlayers, "Einstellung wird mit der Welt gespeichert");
        Assert.True(g.Apply("a", Throw(dir, charge), true).Ok, "Wurf");
        Run(g, GameData.TntFuse + 0.6f);
        Assert.True(V3.DistXZ(b.Pos, pb) < 0.01f, "Mitspieler bleibt stehen");
        Assert.False(g.Stunned("b"), "Nicht benommen");
        // der Werfer selbst fliegt trotzdem, wenn er zu nah steht
        TestKit.Ticks(g, 1.5f);
        TestHelpers.Teleport(g, "a", pb);
        Assert.True(g.Apply("a", Throw(new V3(0, 1, 0.05f), 0f), true).Ok, "Wurf vor die eigenen Füße");
        Run(g, GameData.TntFuse + 0.6f);
        Assert.True(V3.DistXZ(a.Pos, pb) > 1f, "Werfer fliegt selbst (Sicherheitsabstand)");
        Assert.True(V3.DistXZ(b.Pos, pb) < 0.01f, "Mitspieler weiterhin verschont");
    }

    [Test]
    public static void Wurf_in_den_Stuetzpunkt_erlischt_und_Ladung_kommt_zurueck()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur; var l = WorldGen.Get("terra");
        var center = new V3((l.Base.MinX + l.Base.MaxX) * 0.5f, 0, (l.Base.MinZ + l.Base.MaxZ) * 0.5f);
        V3 from = default(V3), dir = default(V3); float charge = 0; bool ok = false;
        for (int k = 0; k < 60 && !ok; k++)
        {
            var cand = FreeSpot(ps, center, 10f, 45f, 200 + k, 5f);
            TntFlight f;
            if (Rules.TntAim(ps, cand, center, 6f, out dir, out charge, out f) && f.Land == 3) { from = cand; ok = true; }
        }
        Assert.True(ok, "Wurf in den Stützpunkt möglich");
        p.Tnt = 1;
        TestHelpers.Teleport(g, p.Id, from);
        Assert.True(g.Apply(p.Id, Throw(dir, charge), true).Ok, "Wurf");
        Assert.Equal(0, p.Tnt, "Ladung unterwegs");
        var fx = Run(g, GameData.TntFuse + 1f);
        var boom = fx.First(f => f.Str("k") == "tntboom");
        Assert.Equal("base", boom.Str("fizzle"), "Erloschen im Stützpunkt");
        Assert.Equal(1, p.Tnt, "Ladung zurück");
        Assert.Equal((long)0, g.S.Stat("tntBooms"), "Keine Explosion");
    }

    // ================================================================== Netz: zwei Spieler über die Sitzung
    [Test]
    public static void Koop_Sitzung_Treffer_und_Einstellung_kommen_bei_allen_an()
    {
        GameClient hc;
        using (var rig = NetRig.HostWithLocalHost(out hc, false))
        {
            var gc = rig.Local("guest", "Gast");
            rig.PumpUntil(() => gc.Joined, "Gast tritt bei");
            var g = rig.Game;
            var ps = g.S.Cur;
            var pb = FreeSpot(ps, new V3(0, 0, -95), 0, 30, 3);
            V3 pa = default(V3), dir = default(V3); float charge = 0;
            for (int k = 0; k < 40; k++)
            {
                var cand = FreeSpot(ps, pb, 9f, 13f, 50 + k);
                TntFlight f;
                if (Rules.TntAim(ps, cand, pb, 1.2f, out dir, out charge, out f) && f.Land == 0 && Rules.HeapAt(ps, f.Pos, GameData.TntHeapReach) < 0) { pa = cand; break; }
            }
            g.S.Players["host"].Pos = pa; g.S.Players["guest"].Pos = pb;
            g.AllowTeleport("host"); g.AllowTeleport("guest");
            g.S.Players["host"].Tnt = 2;
            var booms = new List<JObj>();
            gc.Fx += f => { if (f.Str("k") == "tntboom") booms.Add(f); };
            var r = rig.Act(hc, Throw(dir, charge));
            Assert.True(r.Ok, "Wurf über die Sitzung: " + r.Err);
            rig.PumpUntil(() => gc.W.Cur.Tnt.Count > 0, "Scharfe Ladung beim Gast");
            rig.PumpUntil(() => booms.Count > 0, "Explosion beim Gast", 20);
            Assert.True(booms[0].Arr("hits") != null && booms[0].Arr("hits").Any(h => ((List<object>)h)[0] as string == "guest"), "Gast sieht den eigenen Treffer");
            Assert.True(V3.DistXZ(g.S.Players["guest"].Pos, pb) > 1f, "Server hat den Gast versetzt");
            rig.PumpUntil(() => gc.W.Cur.Tnt.Count == 0, "Ladung beim Gast weg");
            // Einstellung: nur Host, kommt beim Gast an
            Assert.False(rig.Act(gc, TestKit.A("tnthits").Set("v", false)).Ok, "Gast darf nicht umstellen");
            Assert.True(rig.Act(hc, TestKit.A("tnthits").Set("v", false)).Ok, "Host stellt um");
            rig.PumpUntil(() => !gc.W.TntHitsPlayers, "Einstellung beim Gast");
            // Schätze: gleicher Weltsamen → gleiche Funkelstellen bei allen
            Assert.Equal(g.S.TreasureSeed, gc.W.TreasureSeed, "Weltsamen beim Gast");
            var a1 = Treasures.Carriers(g.S, "terra"); var a2 = Treasures.Carriers(gc.W, "terra");
            Assert.True(a1.Count == a2.Count && a1.All(kv => a2.ContainsKey(kv.Key) && a2[kv.Key] == kv.Value), "Gleiche Schatz-Träger beim Gast");
        }
    }

    // ================================================================== Alte Spielstände
    [Test]
    public static void Alter_Spielstand_ohne_TNT_und_Schaetze_laedt()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        p.Tnt = 2;
        var o = g.S.ToJson(true);
        o.Remove("tnt"); o.Remove("treasure");
        foreach (var kv in o.Obj("planets")) (kv.Value as JObj).Remove("tnt");
        foreach (var kv in o.Obj("players")) (kv.Value as JObj).Remove("tnt");
        string err;
        var w = SaveCodec.Decode(TestKit.Envelope(o, WorldState.CurrentVersion), out err);
        Assert.True(w != null, "Alter Stand lesbar: " + err);
        Assert.True(w.TntHitsPlayers, "TNT trifft Mitspieler: Standard an");
        Assert.Equal(0, w.Players[p.Id].Tnt, "Kein TNT im alten Stand");
        Assert.Equal(0, w.Cur.Tnt.Count + w.Cur.MoundBlasts.Count, "Keine Ladungen/Sprengungen");
        Assert.Equal(0, w.TreasureSeed, "Kein Samen im alten Stand");
        var g2 = new Game(w);
        Assert.True(w.TreasureSeed != 0, "Samen beim Laden festgelegt");
        Assert.Equal(Treasures.DeriveSeed(w), w.TreasureSeed, "Samen aus Weltname und Erstellzeit (bei jedem Laden gleich)");
        g2.Join(p.Id, "Tester");
        TestKit.Ticks(g2, 2f);
        TestHelpers.Teleport(g2, p.Id, TestHelpers.Station(g2, "workshop"));
        g2.S.Credits = 500;
        Assert.True(g2.Apply(p.Id, TestKit.A("buytnt"), true).Ok, "TNT im alten Stand kaufbar");
    }
}
