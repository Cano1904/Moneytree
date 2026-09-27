using System.Collections.Generic;

namespace RePlanet.Core
{
    public enum Grade { Unsorted = 0, Sorted = 1, Bale = 2 }

    public class MaterialDef
    {
        public string Id, Name, Symbol, Shape;
        /// <summary>Preis pro Einheit (sortiert) in Credits.</summary>
        public int Price;
        public bool Pressable, Buyable;
        public int Hazard;
        /// <summary>Vergütung aus dem Umweltfonds je fachgerecht entsorgter Einheit (Gefahrstoffe).</summary>
        public int DisposalBonus;
        public uint Color;
    }

    public class TrashType
    {
        public string Id, Name, Shape;
        public uint Color;
        public float Mass, Volume, Size = 1f;
        public Dictionary<string, int> Yield = new Dictionary<string, int>();
        public bool Grab = true, Vacuum, Magnet;
        public int MinMagnet = 1, MinCutter = 1, Hazard;
        /// <summary>Zerlegbar mit Schneidgerät in diese Teile.</summary>
        public string[] CutInto;
        public bool Crane, Floating, Oil, Lore;
        public float CutTime = 3f;
        public string Desc;
        public int TotalUnits { get { int n = 0; foreach (var kv in Yield) n += kv.Value; return n; } }
        public string MainMaterial
        {
            get
            {
                string best = null; int bv = -1;
                foreach (var kv in Yield) if (kv.Value > bv) { bv = kv.Value; best = kv.Key; }
                return best ?? "metall";
            }
        }
    }

    public class TechLevel
    {
        public int Cost;
        public float Value;
        public string Label;
        public TechLevel(int cost, float value, string label) { Cost = cost; Value = value; Label = label; }
    }

    public class TechDef
    {
        public string Id, Name, Desc, Category, Effect, RequiresPlanet;
        public List<TechLevel> Levels = new List<TechLevel>();
        public int MaxLevel { get { return Levels.Count - 1; } }
    }

    public class VehicleDef
    {
        public string Id, Name, Desc, Planet; // Planet = nur auf diesem Planeten nutzbar (null = überall)
        public int Cost;
        public float Speed, Capacity, Radius;
        public bool Water;
    }

    public class BuildingDef
    {
        public string Id, Name, Desc, Category;
        public int W = 1, H = 1, Cost, MaxCount = 99;
        public Dictionary<string, int> Mats = new Dictionary<string, int>();
        public float EnergyUse, EnergyGen, StorageBonus, Rate;
        public bool Machine, Connector;
        public string RequiresPlanetProject; // z. B. "pyra_p2"
        public uint Color;
    }

    public class ProjectDef
    {
        public string Id, Name, Desc, Planet, Effect;
        public int Area, Credits;
        public float BuildTime = 20f, EnergyBonus;
        public Dictionary<string, int> Mats = new Dictionary<string, int>();
        public string[] Requires = new string[0];
        public bool Great;
    }

    public class TrashSpawn
    {
        public string Type; public int Count;
        public TrashSpawn(string t, int c) { Type = t; Count = c; }
    }

    public class GateDef
    {
        public string Type; public int Count; public string Hint;
    }

    public class PlanetDef
    {
        public string Id, Name, Subtitle, Description, UnlockHint;
        public int Order, Seed, ShipLevelRequired;
        public string UnlockProject;
        public uint SkyTop, SkyHorizon, Fog, Ground, Ground2, Accent, Sun;
        public float FogDensity, SunIntensity = 1.1f, WaterLevel = -100f;
        public bool Water, Storms, Cold, Aurora;
        public string[] AreaNames = new string[3];
        public string[] AreaDesc = new string[3];
        public List<TrashSpawn>[] Spawns = new List<TrashSpawn>[3];
        public GateDef[] Gates = new GateDef[2];
        public string EcoAction, EcoName, RepairName, Music;
        public string[] Deliveries;
        public int[] MusicScale;
        public float MusicRoot;
        /// <summary>Alle diese Projekte müssen abgeschlossen sein (Finale-Planet).</summary>
        public string[] UnlockProjects = new string[0];
        public bool StartPlanet;
        public string Mood;
        // Tag/Nacht und Wetter
        public float DayLength = 840f, StormEvery = 420f, StormDuration = 75f, WindBase = 0.15f;
        public string StormName = "Sturm", ShelterName = "Unterschlupf";
    }

    public class MissionDef
    {
        public string Id, Planet, Kind, Title, Desc, Type, Param;
        public int Target, RewardCredits;
        public string RewardCosmetic, Prereq;
    }

    public class LoreDef { public string Id, Planet, Title, Text; }

    public class CosmeticDef { public string Id, Kind, Name, Hint; public uint Value; public bool Default; }
}
