using System;
using System.Linq;
using RePlanet.Core;

/// <summary>Regressionstests für Fehler, die der Kampagnen-Bot aufgedeckt hat.</summary>
public static class LogicTests
{
    [Test]
    public static void Lieferung_enthaelt_gemischte_Teile()
    {
        foreach (var pl in new[] { "terra", "pyra", "pelagia" })
        {
            var g = new Game(Game.NewWorld("Lieferung", pl));
            var p = g.Join("p", "P");
            var def = GameData.Planets[pl];
            for (int round = 0; round < 3; round++)
            {
                TestHelpers.Teleport(g, "p", TestHelpers.Station(g, "contracts"));
                var r = g.Apply("p", TestKit.A("delivery"), true);
                Assert.True(r.Ok, pl + ": Lieferung bestellt: " + r.Err);
                var items = g.S.Cur.Dyn.Values.Where(d => d.Delivery).ToList();
                Assert.Equal(14, items.Count, pl + ": 14 Teile");
                int kinds = items.Select(d => d.Type).Distinct().Count();
                Assert.True(kinds >= Math.Min(def.Deliveries.Length, 5), pl + ": Lieferung ist gemischt (" + kinds + " Sorten: " + string.Join(",", items.Select(d => d.Type).Distinct()) + ")");
                // Abräumen, damit die nächste Lieferung möglich ist
                foreach (var d in items) g.S.Cur.Dyn.Remove(d.Id);
            }
        }
    }

    [Test]
    public static void Ballenpresse_macht_Material_nicht_unbrauchbar_fuer_Projekte_und_Bauten()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var ps = g.S.Cur;
        g.S.Credits = 5000;
        ps.Store("metall").S = 25;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "build"));
        var rb = g.Apply(p.Id, TestKit.A("build").Set("t", "presse").Set("x", 0).Set("z", 0).Set("r", 0), true);
        Assert.True(rb.Ok, "Presse gebaut: " + rb.Err);
        // Material für das erste Projekt (TERRA: Glas 30, Metall 25, Elektronik 6) – die Presse bündelt Metall zu Ballen
        ps.Store("metall").S = 25; ps.Store("glas").S = 30; ps.Store("elektronik").S = 6;
        TestKit.Ticks(g, 30f);
        Assert.True(ps.Store("metall").B >= 2, "Presse hat Metall zu Ballen gepresst (" + ps.Store("metall").B + ")");
        int units = ps.Store("metall").U + ps.Store("metall").S + ps.Store("metall").B * GameData.BaleUnits;
        Assert.Equal(25, units, "Keine Materialvermehrung/-vernichtung");
        // Bereich 0 als gereinigt markieren
        var l = WorldGen.Get("terra");
        foreach (var t in l.Trash) if (t.Area == 0 && t.Gate < 0) ps.Removed.Set(t.Id);
        ps.RecomputeDerived();
        Assert.True(Rules.ProjectCheck(g.S, "terra_p1") == null, "Gepresstes Metall zählt für das Projekt: " + Rules.ProjectCheck(g.S, "terra_p1"));
        TestHelpers.Teleport(g, p.Id, l.ProjectSites[0]);
        var rp = g.Apply(p.Id, TestKit.A("project").Set("area", 0), true);
        Assert.True(rp.Ok, "Projektstart mit Ballen: " + rp.Err);
        var m = ps.Store("metall");
        Assert.Equal(0, m.U + m.S + m.B * GameData.BaleUnits, "Genau 25 Metall verbraucht (Ballen aufgebrochen)");
        // Bauwerk aus Ballen
        m.B = 2; m.S = 0; m.U = 0;
        TestHelpers.Teleport(g, p.Id, TestHelpers.Station(g, "build"));
        var rl = g.Apply(p.Id, TestKit.A("build").Set("t", "lager").Set("x", 10).Set("z", 0).Set("r", 0), true);
        Assert.True(rl.Ok, "Lagerhalle aus Ballen: " + rl.Err);
        Assert.Equal(10, m.U + m.S + m.B * GameData.BaleUnits, "10 Metall verbraucht, Rest bleibt");
    }
}
