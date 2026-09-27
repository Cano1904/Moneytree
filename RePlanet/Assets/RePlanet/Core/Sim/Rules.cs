using System;
using System.Collections.Generic;
using System.Text;

namespace RePlanet.Core
{
    /// <summary>Sicht auf ein Weltobjekt (statisch aus dem Ausgangszustand oder dynamisch).</summary>
    public class ObjView
    {
        public string Key;
        public TrashType T;
        public V3 Pos;
        public float Rot, Scale = 1f;
        public bool Frozen, Underwater, IsStatic;
        public int Area, Zone = -1, Gate = -1, Sid = -1;
        public DynObj D;
    }

    public struct EnergyInfo { public float Supply, Demand, Efficiency; }

    /// <summary>
    /// Reine Regelfunktionen. Werden vom Server (Autorität) und vom Client (Anzeige, Begründungen) identisch genutzt.
    /// </summary>
    public static class Rules
    {
        public const float GrowTime = 90f;
        public const float LiftTime = 4f;

        // ------------------------------------------------------------ Objekte
        public static ObjView Obj(PlanetState ps, string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2) return null;
            if (key[0] == 's')
            {
                int id;
                if (!int.TryParse(key.Substring(1), out id)) return null;
                if (ps.Removed.Get(id)) return null;
                var t = WorldGen.Get(ps.Id).Get(id);
                if (t == null) return null;
                return FromStatic(ps, t);
            }
            if (key[0] == 'd')
            {
                DynObj d;
                if (!ps.Dyn.TryGetValue(key, out d)) return null;
                return FromDyn(d);
            }
            return null;
        }

        public static ObjView FromStatic(PlanetState ps, TrashObj t)
        {
            return new ObjView
            {
                Key = "s" + t.Id, T = t.Def, Pos = t.Pos, Rot = t.Rot, Scale = t.Scale, Frozen = t.Frozen && !ps.Thawed.Contains(t.Id),
                Underwater = t.Underwater, IsStatic = true, Area = t.Area, Zone = t.Zone, Gate = t.Gate, Sid = t.Id
            };
        }

        public static ObjView FromDyn(DynObj d)
        {
            return new ObjView { Key = d.Id, T = d.Def, Pos = d.Pos, Rot = d.Rot, Frozen = d.Frozen, Underwater = d.Underwater, Area = d.Area, D = d };
        }

        /// <summary>Alle aufsammelbaren Objekte (ohne getragene).</summary>
        public static IEnumerable<ObjView> All(PlanetState ps)
        {
            var l = WorldGen.Get(ps.Id);
            for (int i = 0; i < l.Trash.Count; i++)
            {
                if (ps.Removed.Get(i)) continue;
                yield return FromStatic(ps, l.Trash[i]);
            }
            foreach (var d in ps.Dyn.Values)
            {
                if (d.CarriedBy != null) continue;
                yield return FromDyn(d);
            }
        }

        public static float ObjRadius(TrashType t)
        {
            if (t.Crane) return 4.5f;
            if (t.Oil) return 2.2f;
            if (t.Mass >= 20) return 1.8f;
            return 0.5f * t.Size;
        }

        // ------------------------------------------------------------ Werkzeuge
        public static string[] ToolIds = { "grab", "vacuum", "magnet", "cutter", "heat", "filter", "seeder" };

        public static string ToolName(string tool)
        {
            switch (tool)
            {
                case "grab": return "Greifarm";
                case "vacuum": return "Müllsauger";
                case "magnet": return "Magnetarm";
                case "cutter": return "Schneidgerät";
                case "heat": return "Wärmemodul";
                case "filter": return "Filtermodul";
                case "seeder": return "Bio-Modul";
            }
            return tool;
        }

        public static bool HasTool(WorldState s, string tool)
        {
            if (tool == "grab") return true;
            return s.TechLevel(tool) > 0;
        }

        public static bool IsDiving(string planet, V3 p) { return planet == "pelagia" && p.y < Terrain.WaterLevel(planet) - 1.1f; }

        /// <summary>Kann das Objekt mit dem Werkzeug in den Behälter? null = ja, sonst ein verständlicher Grund.</summary>
        public static string CollectCheck(WorldState s, ObjView o, string tool, V3 playerPos, float binUsed)
        {
            if (o == null) return "Nichts in Reichweite.";
            var t = o.T;
            if (o.D != null && o.D.CarriedBy != null) return "Wird gerade transportiert.";
            if (t.Crane) return t.Name + ": viel zu schwer – nur mit dem Kranfahrzeug bewegbar" + (t.CutInto != null ? " oder mit dem Schneidgerät zerlegbar." : ".");
            if (t.Oil) return "Ölteppich: mit dem Filtermodul reinigen.";
            if (o.Frozen) return "Eingefroren – zuerst mit dem Wärmemodul auftauen.";
            if (o.Underwater && s.CurrentPlanet == "pelagia" && !IsDiving(s.CurrentPlanet, playerPos))
                return s.TechLevel("dive") > 0 ? "Liegt unter Wasser – abtauchen (Strg/C bzw. LB)." : "Liegt unter Wasser – Tauchmodul nötig (Werkstatt).";
            if (t.Hazard > s.TechVal("hazard")) return "Gefahrstoff Klasse " + t.Hazard + ": Gefahrgutbehälter Stufe " + t.Hazard + " nötig.";
            switch (tool)
            {
                case "grab":
                    if (!t.Grab) return t.Magnet ? "Zu sperrig für den Greifarm – Magnetarm (Stufe " + t.MinMagnet + ") nutzen." : t.CutInto != null ? "Zu groß – mit dem Schneidgerät zerlegen." : "Mit dem Greifarm nicht greifbar.";
                    if (t.Mass > s.TechVal("grab")) return "Zu schwer für den Greifarm (" + t.Mass.ToString("0.#") + " kg > " + s.TechVal("grab").ToString("0") + " kg)." + (t.CutInto != null ? " Schneidgerät zerlegt es." : t.Magnet ? " Der Magnetarm schafft es." : " Greifarm verbessern.");
                    break;
                case "vacuum":
                    if (s.TechLevel("vacuum") <= 0) return "Kein Müllsauger vorhanden (Werkstatt).";
                    if (!t.Vacuum) return "Zu schwer oder sperrig für den Sauger.";
                    break;
                case "magnet":
                    if (s.TechLevel("magnet") <= 0) return "Kein Magnetarm vorhanden (Werkstatt).";
                    if (!t.Magnet) return "Nicht magnetisch" + (t.Yield.ContainsKey("kupfer") ? " (Kupfer ist nicht magnetisch)." : ".");
                    if (s.TechLevel("magnet") < t.MinMagnet) return "Magnet zu schwach – Stufe " + t.MinMagnet + " nötig.";
                    break;
                default:
                    return "Dieses Werkzeug sammelt nicht.";
            }
            float vol = Math.Max(0.25f, t.Volume);
            if (binUsed + vol > s.BinCapacity + 0.001f) return "Behälter voll (" + binUsed.ToString("0") + "/" + s.BinCapacity.ToString("0") + ") – zum Stützpunkt bringen" + (s.TechLevel("press") > 0 ? " oder pressen (Taste R)." : ".");
            return null;
        }

        public static string CutCheck(WorldState s, ObjView o)
        {
            if (o == null) return "Nichts zum Zerlegen in Reichweite.";
            if (s.TechLevel("cutter") <= 0) return "Kein Schneidgerät vorhanden (Werkstatt).";
            if (o.T.CutInto == null) return "Kann nicht zerlegt werden.";
            if (s.TechLevel("cutter") < o.T.MinCutter) return "Schneidgerät Stufe " + o.T.MinCutter + " nötig.";
            if (o.Frozen) return "Eingefroren – zuerst auftauen.";
            if (o.D != null && o.D.CarriedBy != null) return "Wird gerade transportiert.";
            return null;
        }

        // ------------------------------------------------------------ Gebiete
        public static float Cleanliness(PlanetState ps, int area)
        {
            var l = WorldGen.Get(ps.Id);
            if (l.AreaWeight[area] <= 0) return 1f;
            return M.Clamp01(ps.RemovedWeight[area] / l.AreaWeight[area]);
        }

        public static bool GateOpen(PlanetState ps, int gate)
        {
            var l = WorldGen.Get(ps.Id);
            foreach (var id in l.Gates[gate].Objects) if (!ps.Removed.Get(id)) return false;
            return true;
        }

        public static bool ZoneCleared(PlanetState ps, int zone)
        {
            var l = WorldGen.Get(ps.Id);
            if (zone < 0 || zone >= l.Zones.Count) return false;
            foreach (var id in l.Zones[zone].Objects) if (!ps.Removed.Get(id)) return false;
            return true;
        }

        public static float EcoGrowth(WorldState s, PlanetState ps, string spot)
        {
            double t;
            if (!ps.Eco.TryGetValue(spot, out t)) return 0f;
            return M.Clamp01((float)((s.PlayTime - t) / GrowTime));
        }

        public static float EcoFraction(WorldState s, PlanetState ps, int area)
        {
            var l = WorldGen.Get(ps.Id);
            int n = 0; float sum = 0;
            foreach (var e in l.Eco) if (e.Area == area) { n++; sum += EcoGrowth(s, ps, e.Id); }
            return n == 0 ? 0 : sum / n;
        }

        /// <summary>Wiederherstellungsstufe 0–4: Zugang, Hauptmüll, Infrastruktur, Ökologie.</summary>
        public static int AreaStage(WorldState s, PlanetState ps, int area)
        {
            bool s1 = area == 0 || GateOpen(ps, area - 1);
            if (!s1) return 0;
            if (Cleanliness(ps, area) < GameData.AreaCleanThreshold) return 1;
            if (!ps.Projects[GameData.ProjectId(ps.Id, area)].Done) return 2;
            if (EcoFraction(s, ps, area) < 0.999f) return 3;
            return 4;
        }

        public static readonly string[] StageNames = { "Zugang versperrt", "Zugang frei", "Hauptmüll entfernt", "Infrastruktur repariert", "Ökologie wiederhergestellt" };

        public static float PlanetRestoration(WorldState s, PlanetState ps)
        {
            float sum = 0;
            for (int a = 0; a < 3; a++)
            {
                sum += Cleanliness(ps, a) * 0.4f;
                if (ps.Projects[GameData.ProjectId(ps.Id, a)].Done) sum += 0.35f;
                sum += EcoFraction(s, ps, a) * 0.25f;
            }
            return M.Clamp01(sum / 3f);
        }

        // ------------------------------------------------------------ Stützpunkt / Energie
        public static void UpdateConnectivity(PlanetState ps)
        {
            var bl = WorldGen.Get(ps.Id).Base;
            var cell = new Dictionary<int, Building>();
            foreach (var b in ps.Buildings)
            {
                b.Connected = false;
                for (int x = 0; x < b.W; x++) for (int z = 0; z < b.H; z++) cell[(b.Gx + x) * 1000 + (b.Gz + z)] = b;
            }
            var queue = new Queue<Building>();
            foreach (var b in ps.Buildings)
                if (b.Def.Category != "Deko" && b.Gz == 0) { b.Connected = true; queue.Enqueue(b); }
            while (queue.Count > 0)
            {
                var b = queue.Dequeue();
                for (int x = -1; x <= b.W; x++)
                    for (int z = -1; z <= b.H; z++)
                    {
                        bool edge = (x == -1 || x == b.W) ^ (z == -1 || z == b.H);
                        if (!edge) continue;
                        Building nb;
                        if (cell.TryGetValue((b.Gx + x) * 1000 + (b.Gz + z), out nb) && !nb.Connected && nb.Def.Category != "Deko")
                        {
                            nb.Connected = true;
                            queue.Enqueue(nb);
                        }
                    }
            }
        }

        public static EnergyInfo Energy(PlanetState ps)
        {
            var e = new EnergyInfo { Supply = GameData.BaseEnergy };
            foreach (var kv in ps.Projects)
                if (kv.Value.Done) e.Supply += GameData.Projects[kv.Key].EnergyBonus;
            foreach (var b in ps.Buildings)
            {
                e.Supply += b.Def.EnergyGen;
                if (b.Def.EnergyUse > 0 && (b.Connected || !b.Def.Machine)) e.Demand += b.Def.EnergyUse;
            }
            e.Efficiency = e.Demand <= 0.001f ? 1f : M.Clamp01(e.Supply / e.Demand);
            return e;
        }

        public static string CanPlace(WorldState s, PlanetState ps, string type, int gx, int gz, int rot, int ignoreId)
        {
            BuildingDef def;
            if (!GameData.Buildings.TryGetValue(type, out def)) return "Unbekanntes Bauwerk.";
            if (def.RequiresPlanetProject != null)
            {
                var pd = GameData.Projects[def.RequiresPlanetProject];
                if (!s.Planet(pd.Planet).Projects[pd.Id].Done) return "Erst nach dem Projekt „" + pd.Name + "“ verfügbar.";
            }
            var bl = WorldGen.Get(ps.Id).Base;
            int w = rot % 2 == 0 ? def.W : def.H, h = rot % 2 == 0 ? def.H : def.W;
            if (gx < 0 || gz < 0 || gx + w > bl.GridW || gz + h > bl.GridH) return "Außerhalb der Baufläche.";
            foreach (var b in ps.Buildings)
            {
                if (b.Id == ignoreId) continue;
                if (gx < b.Gx + b.W && gx + w > b.Gx && gz < b.Gz + b.H && gz + h > b.Gz) return "Kollision mit " + b.Def.Name + ".";
            }
            return null;
        }

        public static int CountOf(PlanetState ps, string type)
        {
            int n = 0;
            foreach (var b in ps.Buildings) if (b.Type == type) n++;
            return n;
        }

        // ------------------------------------------------------------ Wirtschaft
        public static float SellBonus(WorldState s, Grade g)
        {
            float f = 1f;
            if (s.CurrentPlanet == "pyra" && s.Planet("pyra").Projects["pyra_p1"].Done) f *= 1.15f;
            if (g == Grade.Bale && s.Planets.ContainsKey("pyra") && s.Planet("pyra").Projects["pyra_p3"].Done) f *= 1.2f;
            return f;
        }

        public static int SellValue(WorldState s, string mat, Grade g, int n)
        {
            return GameData.SellPrice(mat, g, SellBonus(s, g)) * Math.Max(0, n);
        }

        public static int BinValue(WorldState s, List<Item> bin)
        {
            int v = 0;
            foreach (var it in bin)
                foreach (var kv in GameData.Trash[it.T].Yield)
                    v += SellValue(s, kv.Key, Grade.Unsorted, kv.Value);
            return v;
        }

        public static Dictionary<string, int> RepairCost(string planet)
        {
            switch (planet)
            {
                case "pyra": return new Dictionary<string, int> { { "stahl", 3 }, { "kupfer", 1 } };
                case "pelagia": return new Dictionary<string, int> { { "kunststoff", 3 }, { "elektronik", 1 } };
                case "nivalis": return new Dictionary<string, int> { { "kupfer", 2 }, { "elektronik", 1 } };
                default: return new Dictionary<string, int> { { "metall", 2 }, { "glas", 1 } };
            }
        }

        public static readonly Dictionary<string, string[]> ContractMats = new Dictionary<string, string[]>
        {
            { "terra", new[] { "glas", "metall", "papier", "kunststoff" } },
            { "pyra", new[] { "stahl", "kupfer", "metall" } },
            { "pelagia", new[] { "kunststoff", "netz", "elektronik" } },
            { "nivalis", new[] { "elektronik", "akku", "kupfer" } },
        };

        public static void Contract(string planet, int idx, out string mat, out int n, out int reward)
        {
            var mats = ContractMats[planet];
            mat = mats[idx % mats.Length];
            n = 20 + 10 * (idx % 3);
            reward = (int)Math.Ceiling(GameData.Materials[mat].Price * n * GameData.ContractFactor);
        }

        public static string MissingText(PlanetState ps, Dictionary<string, int> mats, long credits, long have)
        {
            var sb = new StringBuilder();
            if (credits > have) sb.Append((credits - have) + " Credits");
            foreach (var kv in mats)
            {
                int a = ps.Available(kv.Key);
                if (a < kv.Value)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append((kv.Value - a) + " " + GameData.Materials[kv.Key].Name);
                }
            }
            return sb.ToString();
        }

        public static string ProjectCheck(WorldState s, string projectId)
        {
            ProjectDef pd;
            if (!GameData.Projects.TryGetValue(projectId, out pd)) return "Unbekanntes Projekt.";
            var ps = s.Planet(pd.Planet);
            var st = ps.Projects[projectId];
            if (st.Done) return "Bereits abgeschlossen.";
            if (st.Started) return "Wird gerade gebaut.";
            foreach (var r in pd.Requires)
            {
                var rp = GameData.Projects[r];
                if (!s.Planet(rp.Planet).Projects[r].Done) return "Zuerst „" + rp.Name + "“ abschließen.";
            }
            int stage = AreaStage(s, ps, pd.Area);
            if (stage < 2)
            {
                if (stage == 0) return "Der Zugang zum Bereich ist noch versperrt.";
                return "Erst den Hauptmüll entfernen: " + (Cleanliness(ps, pd.Area) * 100).ToString("0") + " % von " + (GameData.AreaCleanThreshold * 100).ToString("0") + " %.";
            }
            var missing = MissingText(ps, pd.Mats, pd.Credits, s.Credits);
            if (missing.Length > 0) return "Es fehlen: " + missing + ".";
            return null;
        }

        public static bool PlanetUnlockable(WorldState s, string planet)
        {
            var pd = GameData.Planets[planet];
            if (s.ShipLevel < pd.ShipLevelRequired) return false;
            if (pd.UnlockProject != null)
            {
                var pr = GameData.Projects[pd.UnlockProject];
                if (!s.Planet(pr.Planet).Projects[pr.Id].Done) return false;
            }
            return true;
        }

        /// <summary>Aktuelles Hauptziel als kurzer Text für das HUD.</summary>
        public static string CurrentObjective(WorldState s)
        {
            foreach (var m in GameData.Missions)
            {
                if (m.Kind != "tutorial") continue;
                MissionState ms;
                if (s.Missions.TryGetValue(m.Id, out ms) && ms.Status == 1)
                    return m.Desc + (m.Target > 1 ? " (" + Math.Min(ms.Progress, m.Target) + "/" + m.Target + ")" : "");
            }
            var ps = s.Cur;
            for (int a = 0; a < 3; a++)
            {
                var pid = GameData.ProjectId(ps.Id, a);
                var pst = ps.Projects[pid];
                if (pst.Done) continue;
                var pd = GameData.Projects[pid];
                var def = GameData.Planets[ps.Id];
                int stage = AreaStage(s, ps, a);
                if (stage == 0) return def.Gates[a - 1].Hint;
                if (stage == 1) return def.AreaNames[a] + " aufräumen: " + (Cleanliness(ps, a) * 100).ToString("0") + " % / " + (GameData.AreaCleanThreshold * 100).ToString("0") + " %";
                if (pst.Started) return "„" + pd.Name + "“ wird gebaut … " + (pst.Progress * 100).ToString("0") + " %";
                var why = ProjectCheck(s, pid);
                return "Projekt „" + pd.Name + "“ starten" + (why != null ? " – " + why : " (am Projektplatz).");
            }
            // Planet fertig → nächster Planet
            foreach (var pl in GameData.PlanetOrder)
            {
                if (s.Unlocked.Contains(pl)) continue;
                var pd = GameData.Planets[pl];
                if (s.ShipLevel < pd.ShipLevelRequired) return "Transportschiff aufrüsten: " + GameData.ShipLevelName[pd.ShipLevelRequired] + " (" + GameData.ShipLevelCost[pd.ShipLevelRequired] + " Credits) für " + pd.Name + ".";
                return pd.UnlockHint;
            }
            if (!s.CampaignDone)
            {
                foreach (var pl in GameData.PlanetOrder)
                {
                    var gp = GameData.ProjectId(pl, 2);
                    if (!s.Planet(pl).Projects[gp].Done) return "Großprojekt auf " + GameData.Planets[pl].Name + " abschließen: „" + GameData.Projects[gp].Name + "“.";
                }
            }
            for (int a = 0; a < 3; a++)
                if (EcoFraction(s, ps, a) < 1f && ps.Projects[GameData.ProjectId(ps.Id, a)].Done)
                    return "Ökologie in " + GameData.Planets[ps.Id].AreaNames[a] + ": " + GameData.Planets[ps.Id].EcoName + " (Bio-Modul).";
            return "Freies Spiel: Recyclingaufträge, Lieferungen und die letzten Ecken aufräumen.";
        }
    }
}
