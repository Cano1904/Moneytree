using System;
using System.Linq;
using RePlanet.Core;

/// <summary>Die komplette Kampagne ist solo abschließbar (Kampagnen-Bot über die echte Spiellogik, mit Zeitlimit).</summary>
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
        Console.WriteLine("           Kampagne: " + (S.PlayTime / 3600).ToString("0.00") + " h Spielzeit (Bot), " + bot.Real.Elapsed.TotalSeconds.ToString("0.0") + " s Echtzeit, Notabschaltungen " + bot.Shutdowns);
    }
}
