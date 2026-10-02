using System;
using System.Collections.Generic;
using System.Linq;
using RePlanet.Core;

/// <summary>Schätze im Müll: Daten, deterministische Verteilung, Finden, Sätze mit Erfolg und Kosmetik, alte Spielstände.</summary>
public static class TreasureTests
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

    [Test]
    public static void Dreissig_Schaetze_auf_vier_Planeten()
    {
        Assert.Equal(30, GameData.Treasures.Count, "30 Schätze");
        Assert.Equal(30, GameData.Treasures.Select(t => t.Id).Distinct().Count(), "Eindeutige IDs");
        Assert.Equal(30, GameData.Treasures.Select(t => t.Name).Distinct().Count(), "Eindeutige Namen");
        foreach (var pl in GameData.PlanetOrder)
        {
            var l = GameData.TreasuresOf(pl);
            Assert.True(l.Count >= 7 && l.Count <= 8, pl + ": 7–8 Schätze");
            Assert.True(l.Any(t => t.Rarity == 2) && l.Any(t => t.Rarity == 1) && l.Any(t => t.Rarity == 0), pl + ": alle Seltenheiten");
            var ach = GameData.AchievementById(GameData.TreasureAchievement(pl));
            Assert.True(ach != null && ach.Target == l.Count && GameData.Cosmetics.ContainsKey(ach.Reward), pl + ": Satz-Erfolg mit Kosmetik");
        }
        foreach (var t in GameData.Treasures)
        {
            Assert.True(t.Desc.Length > 30 && t.Name.Length > 3, t.Id + ": Name und Text");
            Assert.True(Loc.TryTranslate(t.Name, "en") != null && Loc.TryTranslate(t.Desc, "en") != null, t.Id + ": englisch");
        }
    }

    [Test]
    public static void Verteilung_ist_deterministisch_je_Weltsamen()
    {
        var a = new WorldState { TreasureSeed = 12345 };
        var b = new WorldState { TreasureSeed = 12345 };
        var c = new WorldState { TreasureSeed = 999 };
        bool differs = false;
        foreach (var pl in GameData.PlanetOrder)
        {
            var ma = Treasures.Carriers(a, pl); var mb = Treasures.Carriers(b, pl); var mc = Treasures.Carriers(c, pl);
            Assert.Equal(GameData.TreasuresOf(pl).Count, ma.Count, pl + ": jeder Schatz hat einen Träger");
            Assert.True(ma.All(kv => mb.ContainsKey(kv.Key) && mb[kv.Key] == kv.Value), pl + ": gleicher Samen → gleiche Träger");
            if (!ma.All(kv => mc.ContainsKey(kv.Key) && mc[kv.Key] == kv.Value)) differs = true;
            var l = WorldGen.Get(pl);
            foreach (var kv in ma)
            {
                var t = l.Trash[kv.Key];
                Assert.True(t.Gate < 0 && !t.Def.Crane && !t.Def.Oil, pl + ": Träger ist normaler Müll");
                var def = GameData.TreasureById[kv.Value];
                if (def.Rarity == 0) Assert.True(t.Area <= 1, "Häufige Schätze vorn");
                if (def.Rarity == 2) Assert.True(t.Area == 2, "Legendäre Schätze im letzten Bereich");
            }
        }
        Assert.True(differs, "Anderer Samen → andere Verteilung");
    }

    /// <summary>Greifbarer Träger eines Schatzes auf TERRA (mit ausgebauter Technik).</summary>
    static KeyValuePair<int, string> GrabbableCarrier(Game g, Func<TreasureDef, bool> filter = null)
    {
        g.S.Tech["grab"] = 2; g.S.Tech["hazard"] = 2; g.S.Tech["bin"] = 4;
        var ps = g.S.Cur;
        foreach (var kv in Treasures.Carriers(g.S, "terra"))
        {
            if (filter != null && !filter(GameData.TreasureById[kv.Value])) continue;
            var o = Rules.Obj(ps, "s" + kv.Key);
            if (o != null && Rules.CollectCheck(g.S, o, "grab", o.Pos, 0) == null) return kv;
        }
        return new KeyValuePair<int, string>(-1, null);
    }

    [Test]
    public static void Schatz_finden_landet_in_der_Vitrine_nicht_im_Behaelter()
    {
        PlayerData p;
        Game g = null; KeyValuePair<int, string> c = default(KeyValuePair<int, string>);
        for (int i = 0; i < 20 && (g == null || c.Key < 0); i++) { g = TestHelpers.NewGame(out p); c = GrabbableCarrier(g); }
        p = g.S.Players["p1"];
        Assert.True(c.Key >= 0, "Greifbarer Träger gefunden");
        var o = Rules.Obj(g.S.Cur, "s" + c.Key);
        var r = TestHelpers.Grab(g, p, o);
        Assert.True(r.Ok, "Aufheben: " + r.Err);
        var patch = g.BuildPatch();
        var fx = patch.Arr("fx").Cast<JObj>().FirstOrDefault(f => f.Str("k") == "treasure");
        Assert.True(fx != null && fx.Str("id") == c.Value && fx.Str("pid") == p.Id, "Fund-Ereignis mit Finder");
        Assert.True(g.S.TreasureFound.Contains(c.Value), "Schatz gefunden");
        Assert.Equal(1, p.Bin.Count, "Im Behälter nur der Müll selbst – der Schatz ist unverkäuflich");
        Assert.Equal(o.T.Id, p.Bin[0].T, "Müll im Behälter");
        Assert.True(Treasures.In(g.S, "terra", c.Key) == null, "Kein zweites Mal");
        Assert.Equal((long)1, g.S.Stat("treasures"), "Statistik");
        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(g.S), out err);
        Assert.True(w2.TreasureFound.Contains(c.Value) && w2.TreasureSeed == g.S.TreasureSeed, "Fund und Samen gespeichert");
    }

    [Test]
    public static void Vollstaendiger_Satz_gibt_Erfolg_und_Kosmetik()
    {
        PlayerData p;
        Game g = null; KeyValuePair<int, string> c = default(KeyValuePair<int, string>);
        for (int i = 0; i < 20 && (g == null || c.Key < 0); i++) { g = TestHelpers.NewGame(out p); c = GrabbableCarrier(g); }
        p = g.S.Players["p1"];
        foreach (var t in GameData.TreasuresOf("terra")) if (t.Id != c.Value) g.S.TreasureFound.Add(t.Id);
        Run(g, 1.5f);
        Assert.False(g.S.Achievements.Contains("ach_schatz_terra"), "Noch nicht komplett");
        Assert.True(TestHelpers.Grab(g, p, Rules.Obj(g.S.Cur, "s" + c.Key)).Ok, "Letzten Schatz gefunden");
        var fx = Run(g, 1.5f);
        Assert.True(g.S.Achievements.Contains("ach_schatz_terra"), "Erfolg „Kramkiste“");
        Assert.True(g.S.CosmeticUnlocks.Contains("a_bernstein"), "Kosmetik freigeschaltet");
        Assert.True(fx.Any(f => f.Str("k") == "achievement" && f.Str("id") == "ach_schatz_terra"), "Meldung");
    }

    [Test]
    public static void Alter_Stand_Schatz_im_schon_eingesammelten_Muell_zieht_um()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        var o = g.S.ToJson(true);
        o.Remove("treasure");
        string err;
        var w = SaveCodec.Decode(TestKit.Envelope(o, WorldState.CurrentVersion), out err);
        Assert.True(w != null, err);
        // Träger nach dem abgeleiteten Samen bestimmen und einen davon „schon eingesammelt“ markieren
        w.TreasureSeed = Treasures.DeriveSeed(w);
        var map = Treasures.Carriers(w, "terra");
        var victim = map.First();
        w.TreasureSeed = 0;
        w.Cur.Removed.Set(victim.Key);
        w.Cur.RecomputeDerived();
        var g2 = new Game(w);
        Assert.True(w.TreasureMoved.ContainsKey(victim.Value), "Schatz ist umgezogen");
        int to = w.TreasureMoved[victim.Value];
        Assert.False(w.Cur.Removed.Get(to), "Neuer Träger liegt noch");
        Assert.Equal(victim.Value, Treasures.In(w, "terra", to), "Schatz steckt im neuen Träger");
        var map2 = Treasures.Carriers(w, "terra");
        Assert.True(map2.Where(kv => !w.TreasureFound.Contains(kv.Value)).All(kv => !w.Cur.Removed.Get(kv.Key)), "Alle Schätze bleiben findbar");
        var w3 = SaveCodec.Decode(SaveCodec.Encode(w), out err);
        Assert.Equal(to, w3.TreasureMoved[victim.Value], "Umzug gespeichert");
    }
}
