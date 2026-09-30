using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Daten für Lieferungen (Gebühr, Abklingzeit), Helferroboter, Schnellreise, Weltereignisse und Erfolge.
    /// Balancing-Werte stehen gesammelt hier (Wirkung mit dem Kampagnen-Bot messen: <c>cd Tests && dotnet run -c Release -- balance</c>).
    /// </summary>
    public static partial class GameData
    {
        // ------------------------------------------------------------------ Schrottlieferungen
        /// <summary>Teile je Lieferung.</summary>
        public const int DeliveryParts = 14;
        /// <summary>Abklingzeit nach einer Bestellung (Spielsekunden, je Planet).</summary>
        public const float DeliveryCooldown = 60f;

        /// <summary>Liefergebühr: rund 9 % des sortierten (18 % des unsortierten) Werts einer Lieferung auf diesem Planeten.</summary>
        public static int DeliveryFee(string planet)
        {
            switch (planet)
            {
                case "pyra": return 30;
                case "pelagia": return 20;
                case "nivalis": return 45;
                default: return 15;
            }
        }

        // ------------------------------------------------------------------ Helferroboter
        /// <summary>Arbeitsradius um den Arbeitsort (m).</summary>
        public const float HelperRadius = 14f;
        /// <summary>Fahrgeschwindigkeit beim Arbeiten bzw. beim Folgen (m/s).</summary>
        public const float HelperSpeed = 2.2f, HelperFollowSpeed = 7.5f;
        /// <summary>Pause je aufgesammeltem Objekt (s) – Helfer sammeln bewusst langsam.</summary>
        public const float HelperPickPause = 2.5f;
        /// <summary>Objekte je Rohrpost-Kapsel.</summary>
        public const int HelperLoad = 5;
        /// <summary>Höchstgewicht (kg) der Objekte, die ein Helfer aufhebt.</summary>
        public const float HelperMaxMass = 2.5f;

        public static int HelperCredits(string planet)
        {
            switch (planet)
            {
                case "pyra": return 260;
                case "pelagia": return 380;
                case "nivalis": return 520;
                default: return 140;
            }
        }

        public static Dictionary<string, int> HelperMats(string planet)
        {
            switch (planet)
            {
                case "pyra": return new Dictionary<string, int> { { "stahl", 8 }, { "kupfer", 3 } };
                case "pelagia": return new Dictionary<string, int> { { "kunststoff", 8 }, { "elektronik", 3 } };
                case "nivalis": return new Dictionary<string, int> { { "kupfer", 6 }, { "elektronik", 4 } };
                default: return new Dictionary<string, int> { { "metall", 8 }, { "elektronik", 2 } };
            }
        }

        // ------------------------------------------------------------------ Schnellreise über Lichtpunkte
        /// <summary>Energiekosten: Grundbetrag + je Meter Luftlinie.</summary>
        public const float FastTravelBase = 4f, FastTravelPerMeter = 0.04f;
        /// <summary>Höchstens so viel des Behälters darf gefüllt sein (das Lichtnetz überträgt MIKO, keine volle Ladung).</summary>
        public const float FastTravelMaxLoad = 0.25f;
        public const float FastTravelCooldown = 5f;

        // ------------------------------------------------------------------ Weltereignisse
        /// <summary>Erstes Ereignis frühestens nach dieser Spielzeit (s) – die Einführung bleibt ungestört.</summary>
        public const float EventFirstAfter = 900f;
        /// <summary>Abstand zwischen zwei Ereignissen (s, zufällig dazwischen).</summary>
        public const float EventGapMin = 720f, EventGapMax = 1080f;
        /// <summary>Anteil der Stürme, nach denen eine verschüttete Deponie freiliegt.</summary>
        public const float DumpChance = 0.4f;
        /// <summary>Höchstzahl liegender Ereignisfunde je Planet (sonst fällt das Ereignis aus).</summary>
        public const int EventMaxLying = 40;

        public static AchievementDef AchievementById(string id)
        {
            if (id == null) return null;
            foreach (var a in Achievements) if (a.Id == id) return a;
            return null;
        }

        public static readonly List<AchievementDef> Achievements = new List<AchievementDef>();

        static void Ach(string id, string name, string desc, string counter, long target, string reward, long div = 1, string unit = "")
        {
            Achievements.Add(new AchievementDef { Id = id, Name = name, Desc = desc, Counter = counter, Target = target, Reward = reward, Div = div, Unit = unit });
            if (!Cosmetics.ContainsKey(reward)) throw new Exception("Unbekannte Erfolgs-Belohnung " + reward);
            Cosmetics[reward].Hint = "Erfolg „" + name + "“";
        }

        static void DefineFeatures()
        {
            // Ereignisfunde
            var mt = T("meteorit", "Meteoritensplitter", "chunk", 0x4B3F63, 2.4f, 1f, "seltenmetall:1,metall:2");
            mt.Magnet = true; mt.Size = 0.8f; mt.Desc = "Frisch vom Himmel gefallen – noch warm und voller seltener Metalle.";
            var vk = T("versorgungskiste", "Versorgungskiste", "box", 0xE8872E, 2.8f, 3f, "elektronik:2,kupfer:2,kunststoff:2");
            vk.Size = 1.1f; vk.Desc = "Ein alter Abwurfbehälter der Arche-Flotte, randvoll mit Ersatzteilen.";

            // Kosmetik als Erfolgs-Belohnung (Hinweis wird von Ach gesetzt)
            Co("c_minze", "color", "Minzgrün", 0x7FD8B0, "");
            Co("c_graphit", "color", "Graphit", 0x4A4F57, "");
            Co("c_lavendel", "color", "Lavendel", 0xA890D8, "");
            Co("c_perlweiss", "color", "Perlweiß", 0xEDEAE0, "");
            Co("c_rost", "color", "Retro-Rost", 0xA65A38, "");
            Co("c_polarlicht", "color", "Polarlicht", 0x3FE0A0, "");
            Co("a_limette", "accent", "Limette", 0xB8E04A, "");
            Co("a_himmel", "accent", "Himmelblau", 0x6CC4F0, "");
            Co("a_kupfer", "accent", "Kupfer", 0xC87B4A, "");
            Co("a_gold", "accent", "Gold", 0xE8C04A, "");
            Co("a_marine", "accent", "Marineblau", 0x2F4F8F, "");
            Co("krone", "sticker", "Krone", 0xE8C04A, "");
            Co("blatt", "sticker", "Blatt", 0x6FBF4A, "");
            Co("blitz", "sticker", "Blitz", 0xFFD23F, "");
            Co("mond", "sticker", "Mond", 0xCFD8FF, "");
            Co("komet", "sticker", "Komet", 0xB48CE0, "");
            Co("tropfen", "sticker", "Tropfen", 0x3FA7D6, "");
            Co("rundumleuchte", "attach", "Rundumleuchte", 0xFF9A2E, "");
            Co("gluehbirne", "attach", "Glühbirne", 0xFFE08A, "");
            Co("propeller", "attach", "Propellermütze", 0x3FA7D6, "");

            // 20 Erfolge – Zähler aus der Statistik bzw. aus dem Spielstand (siehe Rules.AchievementCounter)
            Ach("ach_sammeln1", "Erste Ernte", "Sammle 100 Objekte.", "collected", 100, "a_limette");
            Ach("ach_sammeln2", "Fleißiger Sammler", "Sammle 1 000 Objekte.", "collected", 1000, "c_minze");
            Ach("ach_sammeln3", "Müllmeister", "Sammle 4 000 Objekte.", "collected", 4000, "c_graphit");
            Ach("ach_strecke", "Langstrecke", "Lege 20 km zu Fuß zurück.", "moved", 20 * 100000L, "a_himmel", 100000, " km");
            Ach("ach_verkauf", "Marktschreier", "Verkaufe 100-mal am Verkaufsterminal.", "sales", 100, "a_kupfer");
            Ach("ach_ballen", "Ballenkönig", "Verkaufe 40 Ballen.", "balesSold", 40, "krone");
            Ach("ach_sortieren", "Ordnung muss sein", "Sortiere 1 000 Einheiten.", "sorted", 1000, "c_lavendel");
            Ach("ach_entsorgen", "Saubere Sache", "Entsorge 60 Einheiten Gefahrstoffe fachgerecht.", "disposed", 60, "tropfen");
            Ach("ach_zerlegen", "Schrauber", "Zerlege 30 große Objekte mit dem Schneidgerät.", "dismantled", 30, "a_gold");
            Ach("ach_kran", "Kranführer", "Hebe 6 Wracks mit dem Kran an.", "lifted", 6, "rundumleuchte");
            Ach("ach_licht", "Lichtbringer", "Repariere 20 Laternen, Knoten oder Bojen.", "repairs", 20, "gluehbirne");
            Ach("ach_archiv", "Chronist", "Finde alle 16 Fundstücke.", "lore", 16, "c_perlweiss");
            Ach("ach_gruen", "Grüner Daumen", "Belebe 20 ökologische Plätze.", "planted", 20, "blatt");
            Ach("ach_sturm", "Sturmerprobt", "Warte 5 Stürme im Unterschlupf ab.", "stormsWaited", 5, "blitz");
            Ach("ach_nacht", "Nachtruhe", "Schlafe 15 Nächte durch.", "nights", 15, "mond");
            Ach("ach_helfer1", "Neue Freunde", "Repariere einen Helferroboter.", "helpers", 1, "c_rost");
            Ach("ach_helfer6", "Roboterfamilie", "Repariere 6 Helferroboter.", "helpers", 6, "propeller");
            Ach("ach_ereignis", "Sternschnuppe", "Birg 15 Ereignisfunde (Meteoriten, Versorgungskisten, Deponien).", "eventItems", 15, "komet");
            Ach("ach_reise", "Lichtnetz", "Reise 10-mal schnell zwischen Lichtpunkten.", "fastTravels", 10, "c_polarlicht");
            Ach("ach_liefer", "Stammkunde", "Bestelle 10 Schrottlieferungen.", "deliveries", 10, "a_marine");
        }
    }
}
