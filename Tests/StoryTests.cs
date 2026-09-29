using System;
using System.Collections.Generic;
using RePlanet.Core;

/// <summary>Erzählerzeilen im Spiel und Radio-Freischaltungen: einmal pro Spielstand, gespeichert, alte Stände kompatibel.</summary>
public static class StoryTests
{
    /// <summary>Entfernt allen Müll eines Bereichs (ohne Sperren) direkt im Zustand.</summary>
    static void CleanArea(WorldState w, string planet, int area)
    {
        var ps = w.Planet(planet);
        var l = WorldGen.Get(planet);
        for (int i = 0; i < l.Trash.Count; i++)
        {
            var t = l.Trash[i];
            if (t.Area == area && t.Gate < 0) ps.Removed.Set(t.Id);
        }
        ps.RecomputeDerived();
    }

    [Test]
    public static void Katalog_Erzaehlerzeilen_und_Radio_vollstaendig()
    {
        Assert.True(Story.Lines.Count >= 20, "Mindestens 20 Erzählerzeilen");
        var files = new HashSet<string>();
        for (int i = 0; i < Story.Lines.Count; i++)
        {
            var l = Story.Lines[i];
            Assert.Equal("game_" + (i + 1).ToString("00"), l.File, "Dateiname fortlaufend");
            Assert.True(files.Add(l.File), "Datei doppelt: " + l.File);
            Assert.True(l.MaxDuration >= 3f && l.MaxDuration <= 8f, "Maximale Dauer sinnvoll: " + l.Id);
            Assert.True(!string.IsNullOrEmpty(l.Text) && l.Text.Length < 110, "Kurzer Satz: " + l.Id);
        }
        foreach (var t in Story.Tracks)
        {
            Assert.True(Array.IndexOf(Synth.MusicIds, t.Piece) >= 0, "Radio-Stück nutzt vorhandene Musik: " + t.Id);
            Assert.Equal(Synth.StemNames.Length, t.Mix.Length, "Mischung je Stem: " + t.Id);
        }
        Assert.True(Story.TrackById.ContainsKey("theme"), "Hauptthema vorhanden");
    }

    [Test]
    public static void Erzaehlerzeile_laeuft_nur_einmal_pro_Spielstand()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        Assert.True(g.S.Narrated.Contains("land_terra"), "Erste Landung auf TERRA wird beim Beitritt erzählt");
        var patch = g.BuildPatch();
        bool sent = false;
        foreach (var f in patch.Arr("fx")) { var fo = f as JObj; if (fo != null && fo.Str("k") == "narrate" && fo.Str("id") == "land_terra") sent = true; }
        Assert.True(sent, "Clients erhalten das Ereignis „narrate“");
        Assert.True(g.Narrate("first_night"), "Erstes Mal: wird erzählt");
        Assert.False(g.Narrate("first_night"), "Zweites Mal: still");
        Assert.False(g.Narrate("gibt_es_nicht"), "Unbekannte Zeile wird ignoriert");

        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(g.S), out err);
        Assert.True(w2 != null, "Spielstand lesbar: " + err);
        Assert.True(w2.Narrated.Contains("first_night") && w2.Narrated.Contains("land_terra"), "Erzählte Zeilen werden gespeichert");
        var g2 = new Game(w2);
        g2.Join(p.Id, "Tester");
        Assert.False(g2.Narrate("first_night"), "Nach dem Laden nicht erneut");
        Assert.True(g2.Narrate("first_storm"), "Andere Zeilen weiterhin möglich");
    }

    [Test]
    public static void Radio_Freischaltung_durch_Reinigung_und_Speicherung()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        Assert.True(g.S.RadioUnlocked.Contains("theme"), "Hauptthema von Anfang an frei");
        Assert.False(g.S.RadioUnlocked.Contains("terra_0"), "Planetenstück noch gesperrt");
        CleanArea(g.S, "terra", 0);
        Assert.Equal(1, g.SyncRadio(true), "Reinigung schaltet genau ein Stück frei");
        Assert.True(g.S.RadioUnlocked.Contains("terra_0"), "TERRA-Stück frei");
        var patch = g.BuildPatch();
        Assert.True(patch.Obj("w") != null && patch.Obj("w").ContainsKey("story"), "Freischaltung wird an Clients verteilt");

        string err;
        var w2 = SaveCodec.Decode(SaveCodec.Encode(g.S), out err);
        Assert.True(w2 != null && w2.RadioUnlocked.Contains("terra_0") && w2.RadioUnlocked.Contains("theme"), "Radio-Freischaltungen gespeichert: " + err);
        Assert.Equal(2, Story.Unlocked(w2).Count, "Zwei freie Stücke");
        Assert.Equal(2, Story.Unlocked(w2, "terra").Count, "TERRA-Sender: Thema + TERRA-Stück");
    }

    [Test]
    public static void Alter_Spielstand_ohne_Story_Teil_bleibt_ladbar()
    {
        // Spielstand im Format vor dieser Änderung: kein Teil „story“
        var w = Game.NewWorld("Alt");
        w.Planet("terra").Visited = true;
        w.PlayTime = 5000;
        w.Stats["collected"] = 40;
        CleanArea(w, "terra", 0);
        var payload = w.ToJson(true);
        payload.Remove("story");
        Assert.False(payload.ContainsKey("story"), "Testvoraussetzung: alter Stand ohne story");
        string err;
        var old = SaveCodec.Decode(TestKit.Envelope(payload, WorldState.CurrentVersion), out err);
        Assert.True(old != null, "Alter Spielstand lesbar: " + err);
        Assert.False(old.StoryLoaded, "Fehlender Teil wird erkannt");
        Assert.Equal(0, old.RadioUnlocked.Count, "Noch nichts freigeschaltet");

        var g = new Game(old);
        Assert.True(old.StoryLoaded, "Nach dem Start nachgezogen");
        Assert.True(old.RadioUnlocked.Contains("theme") && old.RadioUnlocked.Contains("terra_0"), "Radio aus dem Fortschritt abgeleitet");
        Assert.True(old.Narrated.Contains("land_terra") && old.Narrated.Contains("first_deposit") && old.Narrated.Contains("first_night"),
            "Längst vergangene Anlässe gelten als erzählt");
        Assert.False(old.Narrated.Contains("first_lore"), "Noch nicht eingetretene Anlässe bleiben offen");
        g.Join("p1", "Tester");
        Assert.False(g.Narrate("land_terra"), "Landung wird nicht nachträglich erzählt");

        // Älteres Format (v2) über die Migration ebenfalls
        var v2 = w.ToJson(true);
        v2.Remove("story");
        var w3 = SaveCodec.Decode(TestKit.Envelope(v2, 2), out err);
        Assert.True(w3 != null, "v2-Stand lesbar: " + err);
        new Game(w3);
        Assert.True(w3.RadioUnlocked.Contains("terra_0"), "v2: Radio abgeleitet");
    }

    [Test]
    public static void Ereignisse_loesen_Zeilen_aus()
    {
        PlayerData p;
        var g = TestHelpers.NewGame(out p);
        // Nacht abwarten (Tageslänge TERRA 840 s)
        int guard = 0;
        while (!g.S.Narrated.Contains("first_night") && guard++ < 4000) g.Tick(0.5f);
        Assert.True(g.S.Narrated.Contains("first_night"), "Erste Nacht wird erzählt");
        // Zweiter Spieler
        g.Join("p2", "Gast");
        Assert.True(g.S.Narrated.Contains("coop_join"), "Mitspieler beigetreten wird erzählt");
    }
}
