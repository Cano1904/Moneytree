using System;
using System.Diagnostics;
using RePlanet.Core;
public static class BalanceRun
{
    public static int Run()
    {
        var sw = Stopwatch.StartNew();
        var bot = new CampaignBot();
        try { bot.Run(); }
        catch (Exception e) { Console.WriteLine("ABBRUCH: " + e.Message); foreach (var l in bot.Log) Console.WriteLine(l); Console.WriteLine(e.StackTrace); return 1; }
        foreach (var l in bot.Log) Console.WriteLine(l);
        Console.WriteLine($"Echtzeit {sw.ElapsedMilliseconds} ms, Aktionen {bot.Actions}, abgelehnt {bot.Rejections}");
        Console.WriteLine($"Erster Verkauf {bot.FirstSale / 60:0.0} min, erstes Upgrade {bot.FirstUpgrade / 60:0.0} min, erste sichtbare Veränderung {bot.FirstVisibleChange / 60:0.0} min");
        foreach (var kv in bot.PlanetDone) Console.WriteLine($"{kv.Key} fertig nach {kv.Value / 3600:0.00} h");
        Console.WriteLine($"Kampagne: {bot.G.S.PlayTime / 3600:0.00} h Spielzeit, Credits {bot.G.S.Credits}");
        return 0;
    }
}
