using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    public class Item
    {
        public string T;
        public bool P; // gepresst
        public float Vol
        {
            get
            {
                var d = GameData.Trash[T];
                return Math.Max(0.25f, d.Volume) * (P ? 0.5f : 1f);
            }
        }
        public List<object> ToJson() { return Json.Arr(T, P ? 1 : 0); }
        public static Item FromJson(object o)
        {
            var a = o as List<object>;
            if (a == null || a.Count < 1 || !(a[0] is string)) return null;
            var t = (string)a[0];
            if (!GameData.Trash.ContainsKey(t)) return null;
            return new Item { T = t, P = a.Count > 1 && Json.ToDouble(a[1], 0) > 0 };
        }
        public static List<object> ListToJson(List<Item> l)
        {
            var r = new List<object>();
            foreach (var i in l) r.Add(i.ToJson());
            return r;
        }
        public static List<Item> ListFromJson(List<object> l)
        {
            var r = new List<Item>();
            if (l == null) return r;
            foreach (var o in l) { var it = Item.FromJson(o); if (it != null) r.Add(it); }
            return r;
        }
        public static float Volume(List<Item> l) { float v = 0; foreach (var i in l) v += i.Vol; return v; }
    }

    public class StorageEntry
    {
        public int U, S, B;
        public int CapUnits { get { return U + S + B * 5; } }
    }

    public class Building
    {
        public int Id, Gx, Gz, Rot;
        public string Type;
        public float Acc;         // Laufzeit-Akkumulator (Maschinen)
        public bool Connected;    // abgeleitet
        public BuildingDef Def { get { return GameData.Buildings[Type]; } }
        public int W { get { return Rot % 2 == 0 ? Def.W : Def.H; } }
        public int H { get { return Rot % 2 == 0 ? Def.H : Def.W; } }
        public JObj ToJson() { return new JObj().Set("id", Id).Set("t", Type).Set("x", Gx).Set("z", Gz).Set("r", Rot); }
        public static Building FromJson(JObj o)
        {
            if (o == null) return null;
            var t = o.Str("t");
            if (t == null || !GameData.Buildings.ContainsKey(t)) return null;
            return new Building { Id = o.Int("id"), Type = t, Gx = o.Int("x"), Gz = o.Int("z"), Rot = M.Clamp(o.Int("r"), 0, 3) };
        }
    }

    /// <summary>Dynamisches Weltobjekt (Zerlegeteile, Lieferungen, versetzte Wracks).</summary>
    public class DynObj
    {
        public string Id, Type, CarriedBy;
        public V3 Pos;
        public float Rot;
        public int Area;
        public bool Underwater, Frozen, Delivery;
        public TrashType Def { get { return GameData.Trash[Type]; } }
        public JObj ToJson()
        {
            var o = new JObj().Set("id", Id).Set("t", Type).Set("p", Pos.ToJson()).Set("r", Json.R(Rot)).Set("a", Area);
            if (Underwater) o["uw"] = true;
            if (Frozen) o["fr"] = true;
            if (Delivery) o["dl"] = true;
            if (CarriedBy != null) o["cb"] = CarriedBy;
            return o;
        }
        public static DynObj FromJson(JObj o)
        {
            if (o == null) return null;
            var t = o.Str("t");
            if (t == null || !GameData.Trash.ContainsKey(t)) return null;
            return new DynObj
            {
                Id = o.Str("id"), Type = t, Pos = V3.FromArr(o.Floats("p")), Rot = o.Float("r"), Area = M.Clamp(o.Int("a"), 0, 2),
                Underwater = o.Bool("uw"), Frozen = o.Bool("fr"), Delivery = o.Bool("dl"), CarriedBy = o.Str("cb")
            };
        }
    }

    public class VehicleState
    {
        public string Id;
        public V3 Pos;
        public float Yaw;
        public List<Item> Cargo = new List<Item>();
        public string Driver; // Laufzeit, nicht gespeichert
        public string Carry;  // Schlüssel eines getragenen Objekts ("d12")
        public VehicleDef Def { get { return GameData.Vehicles[Id]; } }
        public JObj ToJson(bool withDriver)
        {
            var o = new JObj().Set("id", Id).Set("p", Pos.ToJson()).Set("y", Json.R(Yaw, 3)).Set("c", Item.ListToJson(Cargo));
            if (Carry != null) o["k"] = Carry;
            if (withDriver && Driver != null) o["d"] = Driver;
            return o;
        }
        public static VehicleState FromJson(JObj o)
        {
            if (o == null) return null;
            var id = o.Str("id");
            if (id == null || !GameData.Vehicles.ContainsKey(id)) return null;
            return new VehicleState { Id = id, Pos = V3.FromArr(o.Floats("p")), Yaw = o.Float("y"), Cargo = Item.ListFromJson(o.Arr("c")), Carry = o.Str("k"), Driver = o.Str("d") };
        }
    }

    public class ProjectState
    {
        public bool Started, Done;
        public float Progress;
    }

    public class PlanetState
    {
        public string Id;
        public BitSet Removed;
        public HashSet<int> Thawed = new HashSet<int>();
        public Dictionary<string, DynObj> Dyn = new Dictionary<string, DynObj>();
        public Dictionary<string, StorageEntry> Storage = new Dictionary<string, StorageEntry>();
        public List<Building> Buildings = new List<Building>();
        public int NextBuildingId = 1;
        public Dictionary<string, ProjectState> Projects = new Dictionary<string, ProjectState>();
        public HashSet<string> Repaired = new HashSet<string>();
        public Dictionary<string, double> Eco = new Dictionary<string, double>(); // Spot → Pflanzzeitpunkt (Spielzeit)
        public HashSet<string> Views = new HashSet<string>();
        public Dictionary<string, VehicleState> Vehicles = new Dictionary<string, VehicleState>();
        public int StormCount;
        public float StormTimer;
        public bool StormActive, StormWarn;
        public double DayOffset;
        public List<V3> Shelters = new List<V3>(); // selbst gebaute Notunterschlüpfe
        public int ContractIdx;
        public bool Visited;

        // abgeleitet / Laufzeit
        public float[] RemovedWeight = new float[3];
        public Dictionary<string, float> Progress = new Dictionary<string, float>(); // Schneiden/Tauen/Filtern/Anheben

        public PlanetState(string id)
        {
            Id = id;
            var l = WorldGen.Get(id);
            Removed = new BitSet(l.Trash.Count);
            for (int a = 0; a < 3; a++) Projects[GameData.ProjectId(id, a)] = new ProjectState();
        }

        public StorageEntry Store(string mat)
        {
            StorageEntry e;
            if (!Storage.TryGetValue(mat, out e)) { e = new StorageEntry(); Storage[mat] = e; }
            return e;
        }

        public int StorageUsed()
        {
            int n = 0;
            foreach (var e in Storage.Values) n += e.CapUnits;
            return n;
        }

        public int StorageCap()
        {
            float c = GameData.BaseStorageCap;
            foreach (var b in Buildings) c += b.Def.StorageBonus;
            return (int)c;
        }

        public int Available(string mat)
        {
            StorageEntry e;
            return Storage.TryGetValue(mat, out e) ? e.U + e.S : 0;
        }

        public void RecomputeDerived()
        {
            var l = WorldGen.Get(Id);
            RemovedWeight = new float[3];
            foreach (int i in Removed.Indices())
            {
                var t = l.Get(i);
                if (t == null || t.Gate >= 0) continue;
                RemovedWeight[t.Area] += WorldGen.Weight(t.Type);
            }
        }

        // ------------------------------------------------------------ Serialisierung (Teile)
        public static readonly string[] Parts = { "rm", "thaw", "dyn", "storage", "buildings", "projects", "repairs", "eco", "views", "vehicles", "weather", "misc", "shelters" };

        public object PartToJson(string part)
        {
            switch (part)
            {
                case "rm": return Removed.ToBase64();
                case "thaw": { var l = new List<object>(); foreach (var i in Thawed) l.Add(i); return l; }
                case "dyn": { var l = new List<object>(); foreach (var d in Dyn.Values) l.Add(d.ToJson()); return l; }
                case "storage":
                    {
                        var o = new JObj();
                        foreach (var kv in Storage) if (kv.Value.U + kv.Value.S + kv.Value.B > 0) o[kv.Key] = Json.Arr(kv.Value.U, kv.Value.S, kv.Value.B);
                        return o;
                    }
                case "buildings":
                    {
                        var l = new List<object>();
                        foreach (var b in Buildings) l.Add(b.ToJson());
                        return new JObj().Set("list", l).Set("next", NextBuildingId);
                    }
                case "projects":
                    {
                        var o = new JObj();
                        foreach (var kv in Projects) o[kv.Key] = Json.Arr(kv.Value.Started ? 1 : 0, kv.Value.Done ? 1 : 0, Json.R(kv.Value.Progress, 3));
                        return o;
                    }
                case "repairs": return new List<object>(Repaired);
                case "eco": { var o = new JObj(); foreach (var kv in Eco) o[kv.Key] = Math.Round(kv.Value, 1); return o; }
                case "views": return new List<object>(Views);
                case "vehicles": { var l = new List<object>(); foreach (var v in Vehicles.Values) l.Add(v.ToJson(true)); return l; }
                case "weather": return new JObj().Set("sc", StormCount).Set("st", Json.R(StormTimer, 1)).Set("sa", StormActive).Set("sw", StormWarn).Set("do", Math.Round(DayOffset, 2));
                case "shelters": { var l = new List<object>(); foreach (var v in Shelters) l.Add(v.ToJson(1)); return l; }
                case "misc": return new JObj().Set("ci", ContractIdx).Set("vis", Visited);
            }
            return null;
        }

        public void PartFromJson(string part, object v)
        {
            var l = WorldGen.Get(Id);
            switch (part)
            {
                case "rm": Removed = BitSet.FromBase64(v as string, l.Trash.Count); RecomputeDerived(); break;
                case "thaw": Thawed.Clear(); foreach (var i in (v as List<object>) ?? new List<object>()) Thawed.Add((int)Json.ToDouble(i, -1)); break;
                case "dyn":
                    Dyn.Clear();
                    foreach (var o in (v as List<object>) ?? new List<object>()) { var d = DynObj.FromJson(o as JObj); if (d != null && d.Id != null) Dyn[d.Id] = d; }
                    break;
                case "storage":
                    Storage.Clear();
                    var so = v as JObj;
                    if (so != null)
                        foreach (var kv in so)
                        {
                            if (!GameData.Materials.ContainsKey(kv.Key)) continue;
                            var a = kv.Value as List<object>;
                            if (a == null || a.Count < 3) continue;
                            Storage[kv.Key] = new StorageEntry { U = Math.Max(0, (int)Json.ToDouble(a[0], 0)), S = Math.Max(0, (int)Json.ToDouble(a[1], 0)), B = Math.Max(0, (int)Json.ToDouble(a[2], 0)) };
                        }
                    break;
                case "buildings":
                    Buildings.Clear();
                    var bo = v as JObj;
                    if (bo != null)
                    {
                        foreach (var o in bo.Arr("list") ?? new List<object>()) { var b = Building.FromJson(o as JObj); if (b != null) Buildings.Add(b); }
                        NextBuildingId = Math.Max(bo.Int("next", 1), 1);
                        foreach (var b in Buildings) NextBuildingId = Math.Max(NextBuildingId, b.Id + 1);
                    }
                    break;
                case "projects":
                    var po = v as JObj;
                    if (po != null)
                        foreach (var kv in po)
                        {
                            if (!Projects.ContainsKey(kv.Key)) continue;
                            var a = kv.Value as List<object>;
                            if (a == null || a.Count < 3) continue;
                            Projects[kv.Key] = new ProjectState { Started = Json.ToDouble(a[0], 0) > 0, Done = Json.ToDouble(a[1], 0) > 0, Progress = (float)Json.ToDouble(a[2], 0) };
                        }
                    break;
                case "repairs": Repaired.Clear(); foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string) Repaired.Add((string)o); break;
                case "eco":
                    Eco.Clear();
                    var eo = v as JObj;
                    if (eo != null) foreach (var kv in eo) Eco[kv.Key] = Json.ToDouble(kv.Value, 0);
                    break;
                case "views": Views.Clear(); foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string) Views.Add((string)o); break;
                case "vehicles":
                    Vehicles.Clear();
                    foreach (var o in (v as List<object>) ?? new List<object>()) { var vs = VehicleState.FromJson(o as JObj); if (vs != null) Vehicles[vs.Id] = vs; }
                    break;
                case "weather":
                    var wo = v as JObj;
                    if (wo != null) { StormCount = wo.Int("sc"); StormTimer = wo.Float("st"); StormActive = wo.Bool("sa"); StormWarn = wo.Bool("sw"); DayOffset = wo.Num("do"); }
                    break;
                case "shelters":
                    Shelters.Clear();
                    foreach (var o in (v as List<object>) ?? new List<object>())
                    {
                        var a = o as List<object>;
                        if (a != null && a.Count >= 3) Shelters.Add(new V3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0)));
                    }
                    break;
                case "misc":
                    var mo = v as JObj;
                    if (mo != null) { ContractIdx = mo.Int("ci"); Visited = mo.Bool("vis"); }
                    break;
            }
        }

        public JObj ToJson(bool forSave)
        {
            var o = new JObj().Set("id", Id);
            foreach (var p in Parts)
            {
                if (forSave && p == "vehicles")
                {
                    var l = new List<object>();
                    foreach (var v in Vehicles.Values) l.Add(v.ToJson(false));
                    o[p] = l;
                    continue;
                }
                o[p] = PartToJson(p);
            }
            return o;
        }

        public static PlanetState FromJson(JObj o)
        {
            var id = o.Str("id");
            if (id == null || !GameData.Planets.ContainsKey(id)) throw new FormatException("Unbekannter Planet im Spielstand");
            var ps = new PlanetState(id);
            foreach (var p in Parts) if (o.ContainsKey(p)) ps.PartFromJson(p, o[p]);
            ps.RecomputeDerived();
            return ps;
        }
    }

    public class PlayerData
    {
        public string Id, Name = "MIKO";
        public List<Item> Bin = new List<Item>();
        public float Energy = 100f;
        public V3 Pos;
        public float Yaw;
        public string Vehicle;       // Laufzeit
        public bool Online;          // Laufzeit
        public double LastMoveTime;  // Laufzeit
        public float Moved;          // Laufzeit (für Tutorial)
        public string Color = "c_tuerkis", Accent = "a_orange", Sticker = "s_none", Attach = "x_none";
        public int Flags;            // Laufzeit: Animation/Werkzeug für andere Clients
        public string Tool = "grab"; // Laufzeit
        public bool Sleeping, Exposed; // Laufzeit: schläft im Unterschlupf / ist Nacht oder Sturm ausgesetzt
        public float TowTimer = -1f;   // Laufzeit: > 0 = Notabschaltung, Abschleppdrohne unterwegs
        public int ShelterKind;        // Laufzeit: 0 draußen, 1 Stützpunkt, 2 Unterschlupf im Gelände

        public JObj ToJson(bool forSave)
        {
            var o = new JObj().Set("id", Id).Set("n", Name).Set("bin", Item.ListToJson(Bin)).Set("e", Json.R(Energy, 1)).Set("p", Pos.ToJson()).Set("y", Json.R(Yaw, 3))
                .Set("cos", Json.Arr(Color, Accent, Sticker, Attach));
            if (!forSave) { o["v"] = Vehicle; o["on"] = Online; o["tool"] = Tool; o["sl"] = Sleeping; o["ex"] = Exposed; o["tow"] = Json.R(TowTimer, 1); o["sk"] = ShelterKind; }
            return o;
        }

        public static PlayerData FromJson(JObj o)
        {
            if (o == null || o.Str("id") == null) return null;
            var p = new PlayerData { Id = o.Str("id"), Name = o.Str("n", "MIKO"), Bin = Item.ListFromJson(o.Arr("bin")), Energy = o.Float("e", 100), Pos = V3.FromArr(o.Floats("p")), Yaw = o.Float("y"), Vehicle = o.Str("v"), Online = o.Bool("on"), Tool = o.Str("tool", "grab"),
                Sleeping = o.Bool("sl"), Exposed = o.Bool("ex"), TowTimer = o.Float("tow", -1f), ShelterKind = o.Int("sk") };
            var cos = o.Strs("cos");
            if (cos.Count == 4) { p.Color = cos[0]; p.Accent = cos[1]; p.Sticker = cos[2]; p.Attach = cos[3]; }
            return p;
        }
    }

    public class MissionState
    {
        public int Status;   // 0 gesperrt, 1 aktiv, 2 erledigt
        public long Base;    // Zählerstand bei Aktivierung
        public long Progress;
        public bool Claimed;
    }

    /// <summary>Der gesamte Spielstand (gehört dem Host).</summary>
    public class WorldState
    {
        public const int CurrentVersion = 3;
        public int Version = CurrentVersion;
        public string CurrentPlanet = "terra";
        public long Credits = GameData.StartCredits;
        public int ShipLevel;
        public HashSet<string> Unlocked = new HashSet<string> { "terra", "pyra", "pelagia" };
        public Dictionary<string, int> Tech = new Dictionary<string, int>();
        public HashSet<string> OwnedVehicles = new HashSet<string>();
        public Dictionary<string, PlanetState> Planets = new Dictionary<string, PlanetState>();
        public Dictionary<string, MissionState> Missions = new Dictionary<string, MissionState>();
        public HashSet<string> Lore = new HashSet<string>();
        public HashSet<string> CosmeticUnlocks = new HashSet<string>();
        public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        public Dictionary<string, long> Stats = new Dictionary<string, long>();
        public bool CampaignDone, EndingSeen, TrustGuests, IntroSeen;
        public double PlayTime;
        public long NextDyn = 1;
        public string WorldName = "Neue Welt";
        public string StartPlanet = "terra";
        public string Created = "";

        public PlanetState Planet(string id)
        {
            PlanetState p;
            if (!Planets.TryGetValue(id, out p)) { p = new PlanetState(id); Planets[id] = p; }
            return p;
        }
        public PlanetState Cur { get { return Planet(CurrentPlanet); } }

        public int TechLevel(string id) { int v; return Tech.TryGetValue(id, out v) ? v : 0; }
        public float TechVal(string id) { return GameData.TechValue(id, TechLevel(id)); }
        public long Stat(string k) { long v; return Stats.TryGetValue(k, out v) ? v : 0; }
        public void AddStat(string k, long d) { Stats[k] = Stat(k) + d; }

        public float BinCapacity { get { return TechVal("bin") + TechVal("trailer"); } }
        public float MaxEnergy { get { return TechVal("battery"); } }

        public static readonly string[] Parts = { "credits", "ship", "unlocked", "tech", "owned", "missions", "lore", "cosm", "stats", "flags" };

        public object PartToJson(string part)
        {
            switch (part)
            {
                case "credits": return Credits;
                case "ship": return ShipLevel;
                case "unlocked": return new List<object>(Unlocked);
                case "tech": { var o = new JObj(); foreach (var kv in Tech) o[kv.Key] = kv.Value; return o; }
                case "owned": return new List<object>(OwnedVehicles);
                case "missions":
                    {
                        var o = new JObj();
                        foreach (var kv in Missions) o[kv.Key] = Json.Arr(kv.Value.Status, kv.Value.Base, kv.Value.Progress, kv.Value.Claimed ? 1 : 0);
                        return o;
                    }
                case "lore": return new List<object>(Lore);
                case "cosm": return new List<object>(CosmeticUnlocks);
                case "stats": { var o = new JObj(); foreach (var kv in Stats) o[kv.Key] = kv.Value; return o; }
                case "flags": return new JObj().Set("cd", CampaignDone).Set("es", EndingSeen).Set("tg", TrustGuests).Set("is", IntroSeen).Set("pt", Math.Round(PlayTime, 2)).Set("nd", NextDyn).Set("wn", WorldName).Set("cr", Created).Set("sp", StartPlanet);
            }
            return null;
        }

        public void PartFromJson(string part, object v)
        {
            switch (part)
            {
                case "credits": Credits = Math.Max(0, (long)Json.ToDouble(v, 0)); break;
                case "ship": ShipLevel = M.Clamp((int)Json.ToDouble(v, 0), 0, GameData.ShipLevelCost.Length - 1); break;
                case "unlocked":
                    Unlocked.Clear();
                    foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string && GameData.Planets.ContainsKey((string)o)) Unlocked.Add((string)o);
                    foreach (var pd in GameData.Planets.Values) if (pd.StartPlanet) Unlocked.Add(pd.Id);
                    break;
                case "tech":
                    Tech.Clear();
                    var to = v as JObj;
                    if (to != null) foreach (var kv in to) if (GameData.Tech.ContainsKey(kv.Key)) Tech[kv.Key] = M.Clamp((int)Json.ToDouble(kv.Value, 0), 0, GameData.Tech[kv.Key].MaxLevel);
                    break;
                case "owned":
                    OwnedVehicles.Clear();
                    foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string && GameData.Vehicles.ContainsKey((string)o)) OwnedVehicles.Add((string)o);
                    break;
                case "missions":
                    Missions.Clear();
                    var mo = v as JObj;
                    if (mo != null)
                        foreach (var kv in mo)
                        {
                            var a = kv.Value as List<object>;
                            if (a == null || a.Count < 4) continue;
                            Missions[kv.Key] = new MissionState { Status = (int)Json.ToDouble(a[0], 0), Base = (long)Json.ToDouble(a[1], 0), Progress = (long)Json.ToDouble(a[2], 0), Claimed = Json.ToDouble(a[3], 0) > 0 };
                        }
                    break;
                case "lore": Lore.Clear(); foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string) Lore.Add((string)o); break;
                case "cosm": CosmeticUnlocks.Clear(); foreach (var o in (v as List<object>) ?? new List<object>()) if (o is string) CosmeticUnlocks.Add((string)o); break;
                case "stats":
                    Stats.Clear();
                    var so = v as JObj;
                    if (so != null) foreach (var kv in so) Stats[kv.Key] = (long)Json.ToDouble(kv.Value, 0);
                    break;
                case "flags":
                    var fo = v as JObj;
                    if (fo != null)
                    {
                        CampaignDone = fo.Bool("cd"); EndingSeen = fo.Bool("es"); TrustGuests = fo.Bool("tg"); IntroSeen = fo.Bool("is");
                        PlayTime = fo.Num("pt"); NextDyn = Math.Max(1, fo.Long("nd", 1)); WorldName = fo.Str("wn", "Welt"); Created = fo.Str("cr", "");
                        StartPlanet = fo.Str("sp", "terra"); if (!GameData.Planets.ContainsKey(StartPlanet)) StartPlanet = "terra";
                    }
                    break;
            }
        }

        public JObj ToJson(bool forSave)
        {
            var o = new JObj().Set("version", Version).Set("planet", CurrentPlanet);
            foreach (var p in Parts) o[p] = PartToJson(p);
            var planets = new JObj();
            foreach (var kv in Planets) planets[kv.Key] = kv.Value.ToJson(forSave);
            o["planets"] = planets;
            var players = new JObj();
            foreach (var kv in Players) players[kv.Key] = kv.Value.ToJson(forSave);
            o["players"] = players;
            return o;
        }

        public static WorldState FromJson(JObj o)
        {
            var w = new WorldState();
            w.Version = o.Int("version", CurrentVersion);
            var planet = o.Str("planet", "terra");
            w.CurrentPlanet = GameData.Planets.ContainsKey(planet) ? planet : "terra";
            foreach (var p in Parts) if (o.ContainsKey(p)) w.PartFromJson(p, o[p]);
            var planets = o.Obj("planets");
            if (planets != null)
                foreach (var kv in planets)
                {
                    if (!GameData.Planets.ContainsKey(kv.Key)) continue;
                    w.Planets[kv.Key] = PlanetState.FromJson(kv.Value as JObj);
                }
            var players = o.Obj("players");
            if (players != null)
                foreach (var kv in players)
                {
                    var pd = PlayerData.FromJson(kv.Value as JObj);
                    if (pd != null) w.Players[pd.Id] = pd;
                }
            w.Planet(w.CurrentPlanet);
            return w;
        }
    }
}
