using System;
using System.Linq;
using RePlanet.Core;

/// <summary>
/// Die komplette Kampagne ist solo abschließbar (Kampagnen-Bot über die echte Spiellogik, mit Zeitlimit).
/// Der Test ist schnell (≈1–2 s), weil die Simulation ohne Darstellung läuft: ~7 h Spielzeit entsprechen
/// ~100 000 Aufrufen von Game.Tick(0,25 s) à wenige Mikrosekunden. Jede Spielsekunde wird simuliert
/// (keine Abkürzung); das prüfen die Zusatzbedingungen unten (Schrittzahl ≈ Spielzeit / 0,25 s, gesammelte Objekte).
/// </summary>
public static class CampaignTests
{
    [Test]
    public static void Kampagne_ist_solo_abschliessbar()
    {
        var bot = new CampaignBot { RealTimeLimit = 120, Echo = false };
        bot.Run();
        var S = bot.G.S;
        Assert.True(S.CampaignDone, "Kampagne laut Spielzustand abgeschlossen (WorldState.CampaignDone)");
        foreach (var pl in GameData.PlanetOrder)
            for (int a = 0; a < 3; a++)
                Assert.True(S.Planet(pl).Projects[GameData.ProjectId(pl, a)].Done, "Projekt " + GameData.ProjectId(pl, a) + " abgeschlossen");
        Assert.Equal(1, S.ShipLevel, "Sprungantrieb gekauft");
        Assert.True(S.Unlocked.Contains("nivalis"), "NIVALIS freigeschaltet");
        Assert.True(S.CosmeticUnlocks.Contains("c_sonnengelb"), "Kampagnen-Belohnung freigeschaltet");
        // Reihenfolge: TERRA → PYRA → PELAGIA → NIVALIS
        var order = bot.PlanetStart.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        Assert.Equal("terra,pyra,pelagia,nivalis", string.Join(",", order), "Reisereihenfolge");
        Assert.True(S.Credits >= 0, "Guthaben nie negativ");
        Assert.True(bot.Sleeps > 0, "Nächte/Stürme im Unterschlupf verbracht");
        Assert.True(bot.HangarSleeps + bot.ShipSleeps > 0, "Hangar bzw. Laderaum als Schutzraum genutzt (Hangar " + bot.HangarSleeps + ", Schiff " + bot.ShipSleeps + ")");
        Assert.True(S.PlayTime > 3 * 3600, "Mehrere Stunden Spielzeit simuliert (" + (S.PlayTime / 3600).ToString("0.0") + " h)");
        Assert.True(bot.Steps >= S.PlayTime / 0.25 * 0.95, "Jede Spielsekunde über Game.Tick simuliert (" + bot.Steps + " Schritte)");
        Assert.True(S.Stat("collected") > 3000, "Tausende Objekte über Game.Apply eingesammelt (" + S.Stat("collected") + ")");
        Assert.True(bot.CraneHauls >= 3, "Tor-Wracks mit dem Kran geborgen");
        Console.WriteLine("           Kampagne: " + (S.PlayTime / 3600).ToString("0.00") + " h Spielzeit (Bot), " + bot.Real.Elapsed.TotalSeconds.ToString("0.0") + " s Echtzeit, Notabschaltungen " + bot.Shutdowns + ", Schlaf im Hangar/Schiff " + bot.HangarSleeps + "/" + bot.ShipSleeps);
    }
}
