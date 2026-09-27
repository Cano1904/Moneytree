using System;
using System.Linq;
using RePlanet.Core;

/// <summary>Kampagnen-Bot mit Balancing-Auswertung: <c>dotnet run -c Release -- balance</c>.</summary>
public static class BalanceRun
{
    public static int Run() { return Run(false); }

    public static int Run(bool human)
    {
        var bot = human ? CampaignBot.Human() : new CampaignBot();
        bot.RealTimeLimit = 280;
        Console.WriteLine("Profil: " + bot.ProfileName);
        try { bot.Run(); }
        catch (Exception e)
        {
            Console.WriteLine("ABBRUCH: " + e.Message);
            foreach (var l in bot.Log.Skip(Math.Max(0, bot.Log.Count - 60))) Console.WriteLine(l);
            Console.WriteLine(e.StackTrace);
            Profile(bot);
            return 1;
        }
        Console.WriteLine();
        Console.WriteLine("---- Verlauf ----");
        foreach (var l in bot.Log) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("---- Ergebnis ----");
        foreach (var l in Summary(bot)) Console.WriteLine(l);
        Profile(bot);
        return 0;
    }

    static string Min(double s) { return s < 0 ? "–" : (s / 60).ToString("0.0") + " min"; }

    public static string[] Summary(CampaignBot bot)
    {
        var S = bot.G.S;
        var l = new System.Collections.Generic.List<string>();
        l.Add("Profil: " + bot.ProfileName + " (Geschwindigkeit ×" + bot.SpeedFactor + ", Umweg ×" + bot.Detour + ", +" + bot.PickupDelay + " s je Aufnahme, +" + bot.StationDelay + " s je Station)");
        l.Add("Kampagne abgeschlossen: " + (S.CampaignDone ? "ja" : "nein") + " (WorldState.CampaignDone)");
        l.Add("Gesamtspielzeit: " + (S.PlayTime / 3600).ToString("0.00") + " h");
        l.Add("Erster Verkauf: " + Min(bot.FirstSale));
        l.Add("Erstes Upgrade: " + Min(bot.FirstUpgrade) + " (" + bot.FirstUpgradeId + ")");
        l.Add("Erste sichtbare Veränderung: " + Min(bot.FirstVisibleChange) + " (" + bot.FirstVisibleWhat + ")");
        l.Add("Erster Bereich gereinigt (85 %): " + Min(bot.FirstAreaClean));
        foreach (var pl in GameData.PlanetOrder)
        {
            double a, b;
            if (bot.PlanetStart.TryGetValue(pl, out a) && bot.PlanetDone.TryGetValue(pl, out b))
                l.Add(GameData.Planets[pl].Name + ": " + ((b - a) / 3600).ToString("0.00") + " h (Ankunft " + CampaignBot.Clock(a) + ", fertig " + CampaignBot.Clock(b) + ")");
        }
        l.Add("Notabschaltungen: " + bot.Shutdowns + ", Schlafpausen: " + bot.Sleeps + " (davon Sturm am Tag: " + bot.StormSleeps + "), Unterschlupf-Wege: " + bot.ShelterTrips + ", Notunterschlüpfe gebaut: " + bot.SheltersBuilt);
        l.Add("Kranbergungen: " + bot.CraneHauls + ", Lieferungen bestellt: " + bot.Deliveries + " (Erlös " + bot.DeliveryCredits + " Credits), Reisen: " + bot.Travels);
        l.Add("Credits am Ende: " + S.Credits + ", insgesamt verdient: " + S.Stat("credEarned") + ", Objekte gesammelt: " + S.Stat("collected"));
        l.Add("Aktionen: " + bot.Actions + ", abgelehnt: " + bot.Rejections);
        l.Add("Credits-Verlauf (alle 10 Spielminuten: Zeit → Kontostand / verdient):");
        var sb = new System.Text.StringBuilder("  ");
        int i = 0;
        foreach (var c in bot.CreditHistory)
        {
            sb.Append(CampaignBot.Clock(c[0]).Substring(0, 5) + " " + c[1] + "/" + c[2] + "  ");
            if (++i % 6 == 0) { l.Add(sb.ToString()); sb.Clear(); sb.Append("  "); }
        }
        if (sb.Length > 2) l.Add(sb.ToString());
        return l.ToArray();
    }

    static void Profile(CampaignBot bot)
    {
        Console.WriteLine("---- Laufzeit ----");
        Console.WriteLine("Echtzeit " + bot.Real.Elapsed.TotalSeconds.ToString("0.0") + " s, Simulationsschritte " + bot.Steps +
            ", Game.Tick " + bot.SwTick.Elapsed.TotalSeconds.ToString("0.0") + " s, BuildPatch " + bot.SwPatch.Elapsed.TotalSeconds.ToString("0.0") +
            " s, Zielsuche " + bot.SwSearch.Elapsed.TotalSeconds.ToString("0.0") + " s, Game.Apply " + bot.SwAct.Elapsed.TotalSeconds.ToString("0.0") + " s");
    }
}
