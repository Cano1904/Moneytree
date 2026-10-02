using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Lichtpunkt bzw. Stützpunkt als Ziel der Schnellreise.</summary>
    public class TravelPoint
    {
        /// <summary>Index des Lichtpunkts (<see cref="ZoneDef.Index"/>) oder -1 für den Stützpunkt.</summary>
        public int Zone;
        public string Name;
        public V3 Pos;
        /// <summary>Leuchtet (Lichtpunkt komplett geräumt) bzw. immer beim Stützpunkt.</summary>
        public bool Lit;
    }

    /// <summary>Regeln für Lieferungen, Schnellreise, Helferroboter, Ereignisse und Erfolge (Server und Anzeige).</summary>
    public static partial class Rules
    {
        // ------------------------------------------------------------ Schrottlieferungen
        /// <summary>null = Lieferung bestellbar; sonst Grund. <paramref name="wait"/> = verbleibende Abklingzeit (s).</summary>
        public static string DeliveryCheck(WorldState s, PlanetState ps, out float wait)
        {
            wait = 0f;
            foreach (var d in ps.Dyn.Values) if (d.Delivery) return "Die letzte Lieferung liegt noch am Abladeplatz.";
            double left = ps.NextDelivery - s.PlayTime;
            if (left > 0.05) { wait = (float)left; return "Der nächste Frachter startet in " + FormatWait(left) + "."; }
            int fee = GameData.DeliveryFee(ps.Id);
            if (s.Credits < fee) return "Es fehlen " + (fee - s.Credits) + " Credits für die Liefergebühr (" + fee + " Credits).";
            return null;
        }

        /// <summary>Wartezeit als „m:ss“.</summary>
        public static string FormatWait(double seconds)
        {
            int t = (int)Math.Ceiling(Math.Max(0, seconds));
            return (t / 60) + ":" + (t % 60).ToString("00") + " min";
        }

        // ------------------------------------------------------------ Schnellreise
        /// <summary>Stützpunkt und alle Lichtpunkte des Planeten; leuchtende sind Reiseziele.</summary>
        public static List<TravelPoint> TravelPoints(PlanetState ps)
        {
            var l = WorldGen.Get(ps.Id);
            var list = new List<TravelPoint> { new TravelPoint { Zone = -1, Name = "Stützpunkt", Pos = l.Base.Spawn, Lit = true } };
            foreach (var z in l.Zones) list.Add(new TravelPoint { Zone = z.Index, Name = z.Name, Pos = z.Center, Lit = ZoneCleared(ps, z.Index) });
            return list;
        }

        /// <summary>Ankunftsort: Stützpunkt-Startplatz bzw. freie, trockene Stelle neben der Laterne des Lichtpunkts.</summary>
        public static V3 TravelArrival(PlanetLayout l, int zone)
        {
            if (zone < 0 || zone >= l.Zones.Count) return l.Base.Spawn;
            var c = l.Zones[zone].Center;
            float water = Terrain.WaterLevel(l.Id);
            for (int ring = 1; ring <= 5; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785f + ring * 0.3f, r = 1.5f + ring * 1.5f;
                    float x = c.x + M.Cos(a) * r, z = c.z + M.Sin(a) * r;
                    if (l.BlockedStatic(x, z, 0.9f)) continue;
                    float y = l.GroundAt(x, z);
                    if (y < water + 0.2f) continue;
                    return new V3(x, y, z);
                }
            return new V3(c.x + 2f, l.GroundAt(c.x + 2f, c.z), c.z);
        }

        public static float FastTravelCost(V3 from, V3 to)
        {
            return GameData.FastTravelBase + V3.DistXZ(from, to) * GameData.FastTravelPerMeter;
        }

        /// <summary>Leuchtender Reisepunkt, an dem der Spieler gerade steht (Umkreis 12 m bzw. Stützpunktfläche), sonst null.</summary>
        public static TravelPoint TravelPointAt(PlanetState ps, V3 pos)
        {
            var l = WorldGen.Get(ps.Id);
            foreach (var t in TravelPoints(ps))
            {
                if (!t.Lit) continue;
                if (t.Zone < 0 ? l.Base.InBase(pos.x, pos.z) || V3.DistXZ(pos, t.Pos) < 12f : V3.DistXZ(pos, t.Pos) < 12f) return t;
            }
            return null;
        }

        /// <summary>
        /// Prüft eine Schnellreise zum Ziel <paramref name="zone"/> (-1 = Stützpunkt). null = möglich; <paramref name="cost"/> = Energie.
        /// Regeln: Start an einem leuchtenden Lichtpunkt oder am Stützpunkt, Ziel leuchtet, kein Sturm, nicht im Fahrzeug,
        /// Behälter höchstens zu einem Viertel gefüllt, genug Energie.
        /// </summary>
        public static string FastTravelCheck(WorldState s, PlanetState ps, PlayerData p, int zone, out float cost, out V3 dest)
        {
            cost = 0f;
            var l = WorldGen.Get(ps.Id);
            dest = l.Base.Spawn;
            if (zone < -1 || zone >= l.Zones.Count) return "Unbekanntes Reiseziel.";
            if (zone >= 0 && !ZoneCleared(ps, zone)) return "Der Lichtpunkt „" + l.Zones[zone].Name + "“ leuchtet noch nicht – erst komplett aufräumen.";
            dest = TravelArrival(l, zone);
            cost = FastTravelCost(p.Pos, dest);
            if (p.TowTimer > 0) return "MIKO ist abgeschaltet.";
            if (p.Vehicle != null) return "Erst aussteigen – Fahrzeuge reisen nicht über das Lichtnetz.";
            if (ps.StormActive) return "Im Sturm ist das Lichtnetz gestört – erst den Sturm abwarten.";
            var here = TravelPointAt(ps, p.Pos);
            if (here == null) return "Schnellreise startet an einem leuchtenden Lichtpunkt oder am Stützpunkt.";
            if (here.Zone == zone || V3.DistXZ(p.Pos, dest) < 15f) return "Du bist schon dort.";
            float load = Item.Volume(p.Bin), cap = Math.Max(1f, s.BinCapacity);
            if (load > cap * GameData.FastTravelMaxLoad + 0.001f)
                return "Behälter zu voll (" + load.ToString("0") + "/" + cap.ToString("0") + ") – das Lichtnetz überträgt höchstens ein Viertel Ladung. Erst einlagern.";
            if (p.Energy < cost + 1f) return "Zu wenig Energie für die Reise (" + cost.ToString("0") + " nötig).";
            return null;
        }

        // ------------------------------------------------------------ Helferroboter
        /// <summary>Darf ein Helferroboter dieses Objekt aufsammeln? (klein, leicht, ungefährlich, an Land, außerhalb des Stützpunkts)</summary>
        public static bool HelperCanPick(WorldState s, PlanetState ps, ObjView o)
        {
            if (o == null) return false;
            var t = o.T;
            if (o.Gate >= 0 || t.Crane || t.Oil || t.Hazard > 0 || o.Frozen || o.Underwater) return false;
            if (t.Mass > GameData.HelperMaxMass || !(t.Grab || t.Vacuum)) return false;
            if (o.D != null && (o.D.CarriedBy != null || o.D.Delivery)) return false;
            var l = WorldGen.Get(ps.Id);
            if (l.Base.InBase(o.Pos.x, o.Pos.z)) return false;
            if (Terrain.HeightAt(ps.Id, o.Pos.x, o.Pos.z) < Terrain.WaterLevel(ps.Id) - 0.2f) return false;
            return true;
        }

        /// <summary>Kosten der Reparatur als Text („140 Credits, 8 Metall, 2 Elektronik“).</summary>
        public static string HelperCostText(string planet)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(GameData.HelperCredits(planet)).Append(" Credits");
            foreach (var kv in GameData.HelperMats(planet)) sb.Append(", ").Append(kv.Value).Append(' ').Append(GameData.Materials[kv.Key].Name);
            return sb.ToString();
        }

        /// <summary>null = der Helfer kann hier repariert werden, sonst Grund (Kosten, bereits repariert).</summary>
        public static string HelperRepairCheck(WorldState s, PlanetState ps, string spotId)
        {
            var l = WorldGen.Get(ps.Id);
            var spot = l.Bots.Find(x => x.Id == spotId);
            if (spot == null) return "Unbekannter Roboter.";
            if (ps.Bots.ContainsKey(spotId)) return "Dieser Helfer läuft bereits.";
            var mats = GameData.HelperMats(ps.Id);
            int credits = GameData.HelperCredits(ps.Id);
            var missing = MissingText(ps, mats, credits, s.Credits);
            if (missing.Length > 0) return "Für die Reparatur fehlen: " + missing + ".";
            return null;
        }

        /// <summary>Kann ein Helfer hier arbeiten? null = ja.</summary>
        public static string HelperWorkplaceCheck(PlanetState ps, V3 pos)
        {
            var l = WorldGen.Get(ps.Id);
            if (l.Base.InBase(pos.x, pos.z)) return "Am Stützpunkt gibt es nichts zu sammeln – such einen Platz draußen.";
            if (RoomAt(l, pos) != null) return "Drinnen gibt es nichts zu sammeln.";
            if (Terrain.HeightAt(ps.Id, pos.x, pos.z) < Terrain.WaterLevel(ps.Id) - 0.2f) return "Helfer arbeiten nur an Land.";
            return null;
        }

        // ------------------------------------------------------------ Erfolge
        /// <summary>Aktueller Zählerstand eines Erfolgs (Statistik oder aus dem Spielstand abgeleitet).</summary>
        public static long AchievementCounter(WorldState s, AchievementDef a)
        {
            switch (a.Counter)
            {
                case "repairs": { long n = 0; foreach (var ps in s.Planets.Values) n += ps.Repaired.Count; return Math.Max(n, s.Stat("repairsDone")); }
                case "lore": return s.Lore.Count;
                case "helpers": { long n = 0; foreach (var ps in s.Planets.Values) n += ps.Bots.Count; return Math.Max(n, s.Stat("helpersFixed")); }
                case "treasure": return Treasures.FoundCount(s, null);
            }
            if (a.Counter.StartsWith("treasure:", StringComparison.Ordinal)) return Treasures.FoundCount(s, a.Counter.Substring(9));
            return s.Stat(a.Counter);
        }

        public static float AchievementProgress(WorldState s, AchievementDef a)
        {
            if (s.Achievements.Contains(a.Id)) return 1f;
            return M.Clamp01(AchievementCounter(s, a) / (float)Math.Max(1, a.Target));
        }
    }
}
