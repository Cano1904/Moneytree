using System;
using System.Linq;
using RePlanet.Core;

public static class TestHelpers
{
    public static Game NewGame(out PlayerData p, string pid = "p1")
    {
        var g = new Game(Game.NewWorld("Test"));
        p = g.Join(pid, "Tester");
        return g;
    }

    public static void Teleport(Game g, string pid, V3 pos)
    {
        g.AllowTeleport(pid);
        if (!g.Move(pid, pos, 0, false, 0, "grab", 0.1f)) throw new Exception("Teleport abgelehnt");
    }

    public static V3 Station(Game g, string s) { return WorldGen.Get(g.S.CurrentPlanet).Base.Stations[s]; }

    public static ObjView FirstGrabbable(Game g, PlayerData p, Func<ObjView, bool> extra = null)
    {
        foreach (var o in Rules.All(g.S.Cur))
        {
            if (o.Gate >= 0 || o.IsStatic == false) continue;
            if (extra != null && !extra(o)) continue;
            if (Rules.CollectCheck(g.S, o, "grab", o.Pos, 0) == null) return o;
        }
        return null;
    }

    public static ActResult Grab(Game g, PlayerData p, ObjView o)
    {
        Teleport(g, p.Id, new V3(o.Pos.x + 0.5f, o.Pos.y, o.Pos.z));
        g.Tick(0.3f);
        return g.Apply(p.Id, new JObj().Set("a", "grab").Set("o", o.Key), true);
    }
}

public static class EconomyTests
{
    [Test]
    public static void Sammeln_veraendert_Objekt_und_Inventar()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var o = TestHelpers.FirstGrabbable(g, p);
        var r = TestHelpers.Grab(g, p, o);
        Assert.True(r.Ok, "Aufheben sollte klappen: " + r.Err);
        Assert.True(Rules.Obj(g.S.Cur, o.Key) == null, "Objekt muss aus der Welt entfernt sein");
        Assert.True(g.S.Cur.Removed.Get(o.Sid), "Objekt muss im Bitset als entfernt markiert sein");
        Assert.Equal(1, p.Bin.Count, "Behälter enthält genau ein Objekt");
        Assert.Equal(o.T.Id, p.Bin[0].T, "Richtiger Typ im Behälter");
        var r2 = g.Apply(p.Id, new JObj().Set("a", "grab").Set("o", o.Key), true);
        Assert.False(r2.Ok, "Zweites Aufheben desselben Objekts muss scheitern");
        Assert.Equal(1, p.Bin.Count, "Keine Duplikation");
    }

    [Test]
    public static void Voller_Behaelter_ungeeignetes_Werkzeug_und_fehlendes_Geld()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        // Behälter künstlich füllen
        while (Item.Volume(p.Bin) < g.S.BinCapacity - 0.1f) p.Bin.Add(new Item { T = "zeitung" });
        var o = TestHelpers.FirstGrabbable(g, p);
        var r = TestHelpers.Grab(g, p, o);
        Assert.False(r.Ok, "Voller Behälter muss abgelehnt werden");
        Assert.True(r.Err.StartsWith("Behälter voll"), "Verständliche Meldung bei vollem Behälter: " + r.Err);
        // Ungeeignetes Werkzeug: Einkaufswagen (nur Magnet)
        p.Bin.Clear();
        var cart = Rules.All(g.S.Cur).First(x => x.T.Id == "einkaufswagen");
        var rc = TestHelpers.Grab(g, p, cart);
        Assert.False(rc.Ok, "Einkaufswagen darf nicht mit dem Greifarm gehen");
        Assert.True(rc.Err.Contains("Magnetarm"), "Meldung nennt das nötige Werkzeug: " + rc.Err);
        // Fehlendes Geld
        g.S.Credits = 10;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "workshop"));
        var rb = g.Apply(p.Id, new JObj().Set("a", "buytech").Set("id", "magnet"), true);
        Assert.False(rb.Ok, "Kauf ohne Geld muss scheitern");
        Assert.True(rb.Err.Contains("Credits"), "Meldung nennt fehlende Credits: " + rb.Err);
        Assert.Equal(10L, g.S.Credits, "Guthaben unverändert");
    }

    [Test]
    public static void Verkauf_verguetet_genau_einmal()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Cur.Store("glas").S = 20;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "sell"));
        long before = g.S.Credits;
        var r = g.Apply(p.Id, new JObj().Set("a", "sell").Set("m", "glas").Set("g", 1).Set("n", 20), true);
        Assert.True(r.Ok, "Verkauf klappt: " + r.Err);
        Assert.Equal(before + 20 * GameData.Materials["glas"].Price, g.S.Credits, "Genau der Materialwert");
        var r2 = g.Apply(p.Id, new JObj().Set("a", "sell").Set("m", "glas").Set("g", 1).Set("n", 20), true);
        Assert.False(r2.Ok, "Zweiter Verkauf derselben Ware muss scheitern");
        Assert.Equal(before + 20 * GameData.Materials["glas"].Price, g.S.Credits, "Keine zweite Vergütung");
        var r3 = g.Apply(p.Id, new JObj().Set("a", "sell").Set("m", "glas").Set("g", 1).Set("n", -5), true);
        Assert.False(r3.Ok, "Negative Mengen werden abgelehnt");
    }

    [Test]
    public static void Keine_negativen_Guthaben()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Credits = 0;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "trader"));
        foreach (var id in GameData.TechOrder) g.Apply(p.Id, new JObj().Set("a", "buytech").Set("id", id), true);
        g.Apply(p.Id, new JObj().Set("a", "buymat").Set("m", "glas").Set("n", 5), true);
        g.Apply(p.Id, new JObj().Set("a", "buyship"), true);
        g.Apply(p.Id, new JObj().Set("a", "build").Set("t", "lager").Set("x", 0).Set("z", 0).Set("r", 0), true);
        Assert.True(g.S.Credits >= 0, "Guthaben darf nie negativ werden");
        Assert.Equal(0L, g.S.Credits, "Nichts wurde gekauft");
        Assert.Equal(0, g.S.TechLevel("bin"), "Kein Upgrade ohne Geld");
    }

    [Test]
    public static void Keine_profitable_Kauf_Verkauf_Schleife()
    {
        // Einkaufspreis > bester möglicher Verkaufspreis (Ballen + alle Boni) für jedes handelbare Material
        foreach (var m in GameData.Materials.Values)
        {
            if (!m.Buyable) continue;
            float bestSell = m.Price * GameData.BaleFactor * 1.15f * 1.2f;
            Assert.True(GameData.BuyPrice(m.Id) > bestSell, "Kauf/Verkauf-Schleife bei " + m.Id);
            float contract = m.Price * GameData.ContractFactor;
            Assert.True(GameData.BuyPrice(m.Id) > contract, "Auftragsschleife bei " + m.Id);
        }
        // Praxis: kaufen, pressen (Presse), verkaufen → Verlust
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Credits = 10000;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "trader"));
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "buymat").Set("m", "metall").Set("n", 100), true).Ok, "Kauf");
        g.S.Cur.Store("metall").S -= 100; g.S.Cur.Store("metall").B += 10; // bestmögliche Veredelung simuliert
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "sell"));
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "sell").Set("m", "metall").Set("g", 2).Set("n", 10), true).Ok, "Verkauf");
        Assert.True(g.S.Credits < 10000, "Schleife darf keinen Gewinn bringen (" + g.S.Credits + ")");
        // Bauen + Abreißen ist ein Verlustgeschäft
        long c0 = g.S.Credits;
        g.S.Cur.Store("metall").S = 100;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "build"));
        var rb = g.Apply(p.Id, new JObj().Set("a", "build").Set("t", "lager").Set("x", 2).Set("z", 1).Set("r", 0), true);
        Assert.True(rb.Ok, "Bauen: " + rb.Err);
        int bid = rb.Data.Int("b");
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "demolish").Set("b", bid), true).Ok, "Abriss");
        Assert.True(g.S.Credits < c0, "Bau/Abriss-Schleife bringt keinen Gewinn");
    }

    [Test]
    public static void Upgrades_wirken_messbar()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        float cap0 = g.S.BinCapacity;
        g.S.Credits = 1000;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "workshop"));
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "buytech").Set("id", "bin"), true).Ok, "Behälter-Upgrade");
        Assert.True(g.S.BinCapacity > cap0, "Kapazität steigt");
        Assert.Equal(1000L - GameData.Tech["bin"].Levels[1].Cost, g.S.Credits, "Preis korrekt abgezogen");
        // Magnet: vorher unmöglich, danach möglich
        var cart = Rules.All(g.S.Cur).First(x => x.T.Id == "einkaufswagen");
        Assert.True(Rules.CollectCheck(g.S, cart, "magnet", cart.Pos, 0) != null, "Ohne Magnet nicht möglich");
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "buytech").Set("id", "magnet"), true).Ok, "Magnet kaufen");
        Assert.True(Rules.CollectCheck(g.S, cart, "magnet", cart.Pos, 0) == null, "Mit Magnet möglich");
    }

    [Test]
    public static void Magnetwelle_begrenzt_Reichweite_und_Menge()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Tech["magnet"] = 1; g.S.Tech["bin"] = 4;
        // Viele Dosen um eine Position legen
        var center = new V3(60, 0, -100);
        for (int i = 0; i < 20; i++) g.S.Cur.Dyn["dx" + i] = new DynObj { Id = "dx" + i, Type = "dose", Pos = new V3(center.x + (i % 5) * 0.8f, 0, center.z + 1 + (i / 5) * 0.8f), Area = 0 };
        TestHelpers.Teleport(g, p.Id, center);
        g.Tick(1f);
        var r = g.Apply(p.Id, new JObj().Set("a", "magnet").Set("charge", 1f).Set("dir", Json.Arr(0, 1)), true);
        Assert.True(r.Ok, "Magnetwelle: " + r.Err);
        Assert.True(r.Data.Int("n") <= 6, "Stufe 1 sammelt höchstens 6 Teile (" + r.Data.Int("n") + ")");
        var r2 = g.Apply(p.Id, new JObj().Set("a", "magnet").Set("charge", 1f), true);
        Assert.False(r2.Ok, "Direkt danach lädt der Magnet noch");
    }

    [Test]
    public static void Bauwerke_pruefen_Kollision_und_ziehen_Ressourcen_ab()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Credits = 2000;
        g.S.Cur.Store("metall").S = 40;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "build"));
        var r = g.Apply(p.Id, new JObj().Set("a", "build").Set("t", "sortierer").Set("x", 0).Set("z", 0).Set("r", 0), true);
        Assert.True(r.Ok, "Bauen: " + r.Err);
        Assert.Equal(2000L - 350, g.S.Credits, "Credits abgezogen");
        Assert.Equal(25, g.S.Cur.Store("metall").S, "Material abgezogen");
        var r2 = g.Apply(p.Id, new JObj().Set("a", "build").Set("t", "presse").Set("x", 1).Set("z", 0).Set("r", 0), true);
        Assert.False(r2.Ok, "Überlappung muss abgelehnt werden");
        Assert.True(r2.Err.StartsWith("Kollision"), "Meldung: " + r2.Err);
        var r3 = g.Apply(p.Id, new JObj().Set("a", "build").Set("t", "presse").Set("x", 33).Set("z", 0).Set("r", 0), true);
        Assert.False(r3.Ok, "Außerhalb der Baufläche muss abgelehnt werden");
        Assert.Equal(2000L - 350, g.S.Credits, "Fehlversuche kosten nichts");
        // Sortieranlage arbeitet mit echtem Material
        Assert.True(g.S.Cur.Buildings[0].Connected, "Anlage an der Sammelschiene ist verbunden");
        g.S.Cur.Store("glas").U = 10;
        for (int i = 0; i < 40; i++) g.Tick(0.25f);
        Assert.True(g.S.Cur.Store("glas").S > 0, "Sortieranlage hat sortiert");
        Assert.Equal(10, g.S.Cur.Store("glas").U + g.S.Cur.Store("glas").S, "Keine Materialvermehrung");
        // Umsetzen kostet nichts
        long c = g.S.Credits;
        var rm = g.Apply(p.Id, new JObj().Set("a", "move").Set("b", g.S.Cur.Buildings[0].Id).Set("x", 5).Set("z", 0).Set("r", 1), true);
        Assert.True(rm.Ok, "Umsetzen: " + rm.Err);
        Assert.Equal(c, g.S.Credits, "Umsetzen ist kostenlos");
    }

    [Test]
    public static void Leere_Batterie_ist_keine_Sackgasse()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        g.S.Tech["vacuum"] = 1;
        p.Energy = 0;
        var o = TestHelpers.FirstGrabbable(g, p);
        var r = TestHelpers.Grab(g, p, o);
        Assert.True(r.Ok, "Greifarm funktioniert im Notbetrieb: " + r.Err);
        var rv = g.Apply(p.Id, new JObj().Set("a", "vacuum"), true);
        Assert.False(rv.Ok, "Sauger braucht Energie");
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "charge"));
        for (int i = 0; i < 40; i++) g.Tick(0.25f);
        Assert.True(p.Energy > 50, "Am Ladeplatz wird geladen (" + p.Energy + ")");
        // Respawn aus jeder Lage
        TestHelpers.Teleport(g, p.Id, new V3(120, 0, -60));
        Assert.True(g.Apply(p.Id, new JObj().Set("a", "respawn"), true).Ok, "Respawn");
    }

    [Test]
    public static void Projektressourcen_bleiben_beschaffbar()
    {
        // Jedes Projektmaterial ist entweder kaufbar oder auf dem Planeten reichlich vorhanden
        foreach (var pr in GameData.Projects.Values)
            foreach (var kv in pr.Mats)
            {
                var md = GameData.Materials[kv.Key];
                Assert.True(md.Buyable, "Projektmaterial muss beim Händler kaufbar sein: " + kv.Key);
                var l = WorldGen.Get(pr.Planet);
                long units = l.Trash.Where(t => t.Gate < 0).Sum(t => t.Def.Yield.ContainsKey(kv.Key) ? t.Def.Yield[kv.Key] : 0);
                var dels = GameData.Planets[pr.Planet].Deliveries.Any(d => GameData.Trash[d].Yield.ContainsKey(kv.Key));
                Assert.True(units > 0 || dels, "Material " + kv.Key + " ist auf " + pr.Planet + " nicht sammelbar");
            }
    }
}
