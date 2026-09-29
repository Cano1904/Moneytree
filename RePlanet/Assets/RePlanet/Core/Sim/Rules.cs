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
            if (pd.StartPlanet) return true;
            if (s.ShipLevel < pd.ShipLevelRequired) return false;
            if (pd.UnlockProject != null)
            {
                var pr = GameData.Projects[pd.UnlockProject];
                if (!s.Planet(pr.Planet).Projects[pr.Id].Done) return false;
            }
            foreach (var up in pd.UnlockProjects)
            {
                var pr = GameData.Projects[up];
                if (!s.Planet(pr.Planet).Projects[pr.Id].Done) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ Tag/Nacht, Wind, Unterschlupf
        public const float NightStart = 0.8f, NightEnd = 0.22f, Morning = 0.26f;

        /// <summary>Tageszeit 0..1 (0 = Mitternacht, 0,5 = Mittag). Deterministisch aus Spielzeit und Schlaf-Versatz.</summary>
        public static float DayPhase(WorldState s, string planet)
        {
            var def = GameData.Planets[planet];
            double off = s.Planets.ContainsKey(planet) ? s.Planets[planet].DayOffset : 0;
            double x = (s.PlayTime + off) / def.DayLength + 0.3;
            return (float)(x - Math.Floor(x));
        }

        public static bool IsNight(float phase) { return phase >= NightStart || phase < NightEnd; }
        public static bool IsNight(WorldState s, string planet) { return IsNight(DayPhase(s, planet)); }

        /// <summary>0 = heller Tag, 1 = tiefe Nacht (weiche Dämmerung).</summary>
        public static float Darkness(float phase)
        {
            float d = Math.Abs(phase - 0.5f) * 2f; // 0 mittags, 1 mitternachts
            return M.Smooth(M.InvLerp(0.5f, 0.66f, d));
        }

        public static float SecondsUntilMorning(WorldState s, string planet)
        {
            float ph = DayPhase(s, planet);
            float d = Morning - ph;
            if (d < 0) d += 1f;
            return d * GameData.Planets[planet].DayLength;
        }

        /// <summary>Windstärke 0..1 und Richtung (Bogenmaß). Böen deterministisch aus der Spielzeit; Stürme verstärken stark.</summary>
        public static float Wind(WorldState s, string planet, out float dirX, out float dirZ)
        {
            var def = GameData.Planets[planet];
            var ps = s.Planet(planet);
            float t = (float)s.PlayTime;
            float dir = 0.7f + 0.6f * M.Sin(t * 0.013f + def.Seed) + (ps.StormActive ? 0.3f * M.Sin(t * 0.07f) : 0f);
            dirX = M.Sin(dir); dirZ = M.Cos(dir);
            float gust = 0.5f + 0.5f * M.Sin(t * 0.9f + def.Seed * 0.1f) * M.Sin(t * 0.37f + 1.3f);
            float w = def.WindBase * (0.6f + 0.8f * gust);
            if (IsNight(s, planet)) w *= 1.25f;
            if (ps.StormWarn) w = Math.Max(w, 0.35f + 0.1f * gust);
            if (ps.StormActive) w = 0.75f + 0.25f * gust;
            return M.Clamp01(w);
        }

        /// <summary>Arten von Schutz (Rückgabe von <see cref="ShelterKind"/>).</summary>
        public const int ShelterNone = 0, ShelterBase = 1, ShelterField = 2, ShelterHangar = 3, ShelterShip = 4;

        /// <summary>Innenraum (Hangar, Schiff)? Dort ist man geschützt, kann schlafen, aber keine Werkzeuge benutzen.</summary>
        public static bool Indoors(int shelterKind) { return shelterKind >= ShelterHangar; }

        /// <summary>Kurze Meldung für abgelehnte Werkzeug-/Sammelaktionen im Innenraum.</summary>
        public const string IndoorsDenied = "Im Unterschlupf – hier drinnen wird nicht gesammelt.";

        /// <summary>Aktionen, die in Innenräumen nicht gehen (Sammeln, Werkzeuge, Kran).</summary>
        public static readonly HashSet<string> IndoorsBlockedActions = new HashSet<string>
        {
            "grab", "vacuum", "magnet", "cut", "thaw", "filter", "plant", "vcollect", "boatnet", "clift", "cdrop", "help",
        };

        /// <summary>Begehbarer Schutzraum an dieser Stelle (Hangar, Laderaum) oder null.</summary>
        public static ShelterRoom RoomAt(PlanetLayout l, V3 pos)
        {
            var rooms = l.Base.Rooms;
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].Contains(pos)) return rooms[i];
            return null;
        }

        /// <summary>
        /// 0 = ungeschützt, 1 = Stützpunkt (im Freien), 2 = Unterschlupf im Gelände (vorhanden oder selbst gebaut),
        /// 3 = im Hangar des Hauptgebäudes, 4 = im Laderaum des Transportschiffs.
        /// </summary>
        public static int ShelterKind(WorldState s, PlanetState ps, V3 pos)
        {
            var l = WorldGen.Get(ps.Id);
            var b = l.Base;
            var room = RoomAt(l, pos);
            if (room != null) return room.Kind;
            if (V3.DistXZ(pos, b.Stations["storage"]) < 8f || V3.DistXZ(pos, b.Stations["garage"]) < 7f || V3.DistXZ(pos, b.Stations["charge"]) < 5f) return 1;
            foreach (var sh in l.Shelters) if (V3.DistXZ(pos, sh.Pos) < 3.6f && Math.Abs(pos.y - sh.Pos.y) < 3f) return 2;
            foreach (var sh in ps.Shelters) if (V3.DistXZ(pos, sh) < 3.6f && Math.Abs(pos.y - sh.y) < 3f) return 2;
            return 0;
        }

        public static bool NearestShelter(WorldState s, PlanetState ps, V3 pos, out V3 at, out float dist)
        {
            var l = WorldGen.Get(ps.Id);
            at = l.Base.Stations["storage"];
            dist = V3.DistXZ(pos, at);
            foreach (var sh in l.Shelters) { float d = V3.DistXZ(pos, sh.Pos); if (d < dist) { dist = d; at = sh.Pos; } }
            foreach (var sh in ps.Shelters) { float d = V3.DistXZ(pos, sh); if (d < dist) { dist = d; at = sh; } }
            return true;
        }

        public const int ShelterCost = 80;
        public const int MaxShelters = 8;

        public static string CanBuildShelter(WorldState s, PlanetState ps, V3 pos)
        {
            var l = WorldGen.Get(ps.Id);
            if (l.Base.InBase(pos.x, pos.z)) return "Am Stützpunkt gibt es schon ein Dach.";
            if (ps.Shelters.Count >= MaxShelters) return "Höchstens " + MaxShelters + " Notunterschlüpfe pro Planet.";
            if (Terrain.HeightAt(ps.Id, pos.x, pos.z) < Terrain.WaterLevel(ps.Id) - 0.3f) return "Nicht im Wasser baubar.";
            if (l.BlockedStatic(pos.x, pos.z, 1.8f)) return "Kein Platz – freie Fläche suchen.";
            foreach (var sh in l.Shelters) if (V3.DistXZ(pos, sh.Pos) < 12f) return "Hier in der Nähe gibt es schon einen Unterschlupf.";
            foreach (var sh in ps.Shelters) if (V3.DistXZ(pos, sh) < 12f) return "Hier in der Nähe gibt es schon einen Unterschlupf.";
            return null;
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
            // Planet fertig → andere Startplaneten, dann das Finale
            foreach (var pl in GameData.PlanetOrder)
            {
                var pd = GameData.Planets[pl];
                if (!pd.StartPlanet || pl == ps.Id) continue;
                if (!s.Planet(pl).Projects[GameData.ProjectId(pl, 2)].Done) return "Mit dem Transportschiff nach " + pd.Name + " reisen: " + pd.Subtitle + ".";
            }
            foreach (var pl in GameData.PlanetOrder)
            {
                if (s.Unlocked.Contains(pl)) continue;
                var pd = GameData.Planets[pl];
                if (s.ShipLevel < pd.ShipLevelRequired) return "Finale: Transportschiff aufrüsten – " + GameData.ShipLevelName[pd.ShipLevelRequired] + " (" + GameData.ShipLevelCost[pd.ShipLevelRequired] + " Credits) für " + pd.Name + ".";
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
