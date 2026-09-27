using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Zentrale, datengetriebene Spieldaten. Alle Preise, Werte und Aufträge stehen hier,
    /// damit Balancing ohne Codeänderung an anderer Stelle möglich ist (siehe docs/WIRTSCHAFT.md).
    /// </summary>
    public static class GameData
    {
        public const int StartCredits = 60;
        public const float UnsortedFactor = 0.5f;
        public const float SortedFactor = 1.0f;
        public const float BaleFactor = 1.3f;
        public const int BaleUnits = 10;
        /// <summary>Einkaufspreis beim Materialhändler als Vielfaches des sortierten Verkaufspreises.</summary>
        public const float BuyFactor = 2.0f;
        public const float ContractFactor = 1.2f;
        public const int BaseStorageCap = 400;
        public const float BaseEnergy = 6f;
        public const float AreaCleanThreshold = 0.85f;
        public const int GuestExpensiveThreshold = 800;
        public const int MaxPlayers = 4;
        public const float InteractRange = 3.2f;
        public const float StationRange = 5.5f;

        public static readonly Dictionary<string, MaterialDef> Materials = new Dictionary<string, MaterialDef>();
        public static readonly List<string> MaterialOrder = new List<string>();
        public static readonly Dictionary<string, TrashType> Trash = new Dictionary<string, TrashType>();
        public static readonly Dictionary<string, TechDef> Tech = new Dictionary<string, TechDef>();
        public static readonly List<string> TechOrder = new List<string>();
        public static readonly Dictionary<string, VehicleDef> Vehicles = new Dictionary<string, VehicleDef>();
        public static readonly Dictionary<string, BuildingDef> Buildings = new Dictionary<string, BuildingDef>();
        public static readonly List<string> BuildingOrder = new List<string>();
        public static readonly Dictionary<string, PlanetDef> Planets = new Dictionary<string, PlanetDef>();
        public static readonly List<string> PlanetOrder = new List<string>();
        public static readonly Dictionary<string, ProjectDef> Projects = new Dictionary<string, ProjectDef>();
        public static readonly List<MissionDef> Missions = new List<MissionDef>();
        public static readonly Dictionary<string, LoreDef> Lore = new Dictionary<string, LoreDef>();
        public static readonly List<string> LoreOrder = new List<string>();
        public static readonly Dictionary<string, CosmeticDef> Cosmetics = new Dictionary<string, CosmeticDef>();
        public static readonly List<string> CosmeticOrder = new List<string>();
        public static readonly int[] ShipLevelCost = { 0, 2500, 7000, 14000 };
        public static readonly string[] ShipLevelName = { "Transportschiff (Grundausstattung)", "Sprungantrieb I", "Sprungantrieb II", "Sprungantrieb III" };

        static GameData()
        {
            DefineMaterials();
            DefineTrash();
            DefineTech();
            DefineVehicles();
            DefineBuildings();
            DefinePlanets();
            DefineProjects();
            DefineMissions();
            DefineLore();
            DefineCosmetics();
            Validate();
        }

        public static void EnsureLoaded() { }

        // ------------------------------------------------------------------ Materialien
        static void Mat(string id, string name, int price, bool pressable, bool buyable, string shape, string symbol, uint color, int hazard = 0, int disposal = 0)
        {
            Materials[id] = new MaterialDef { Id = id, Name = name, Price = price, Pressable = pressable, Buyable = buyable, Shape = shape, Symbol = symbol, Color = color, Hazard = hazard, DisposalBonus = disposal };
            MaterialOrder.Add(id);
        }

        static void DefineMaterials()
        {
            // Form + Symbol unterscheiden Materialien zusätzlich zur Farbe (Barrierefreiheit).
            Mat("papier", "Papier", 3, true, true, "square", "▤", 0xD9CBA3);
            Mat("glas", "Glas", 4, false, true, "circle", "◯", 0x7FD6C9);
            Mat("kunststoff", "Kunststoff", 3, true, true, "triangle", "△", 0xF2C14E);
            Mat("metall", "Metall", 6, true, true, "hexagon", "⬡", 0x9AA5B1);
            Mat("stahl", "Stahl", 7, true, true, "bar", "▬", 0x6E7B8B);
            Mat("kupfer", "Kupfer", 12, true, true, "ring", "◎", 0xD1804A);
            Mat("elektronik", "Elektronik", 14, false, true, "chip", "▦", 0x4CAF6A);
            Mat("netz", "Netze & Seile", 4, true, true, "grid", "#", 0x4F8FBF);
            Mat("akku", "Akkus", 16, false, true, "battery", "▮", 0x8FD14F, 1);
            Mat("seltenmetall", "Seltene Metalle", 30, false, true, "diamond", "◆", 0xB48CE0);
            Mat("gefahrstoff", "Gefahrstoffe", 0, false, false, "hazard", "⚠", 0xE8493A, 2, 10);
            Mat("oel", "Altöl", 0, false, false, "drop", "●", 0x2E2A26, 1, 6);
        }

        // ------------------------------------------------------------------ Müllarten
        static TrashType T(string id, string name, string shape, uint color, float mass, float vol, string yields)
        {
            var t = new TrashType { Id = id, Name = name, Shape = shape, Color = color, Mass = mass, Volume = vol };
            foreach (var part in yields.Split(','))
            {
                var kv = part.Split(':');
                t.Yield[kv[0].Trim()] = int.Parse(kv[1]);
            }
            Trash[id] = t;
            return t;
        }

        static void DefineTrash()
        {
            // TERRA – Haushaltsmüll
            T("zeitung", "Zeitungsbündel", "sheet", 0xE0D6BC, 0.5f, 1f, "papier:1").Vacuum = true;
            T("karton", "Karton", "box", 0xB8925A, 1f, 2f, "papier:2");
            T("glasflasche", "Glasflasche", "bottle", 0x5FAF8F, 0.6f, 1f, "glas:1");
            var pf = T("plastikflasche", "Plastikflasche", "pbottle", 0x6FB7E8, 0.2f, 1f, "kunststoff:1"); pf.Vacuum = true;
            var tu = T("tuete", "Plastiktüte", "bag", 0xF4F1E8, 0.1f, 0.5f, "kunststoff:1"); tu.Vacuum = true;
            var dose = T("dose", "Getränkedose", "can", 0xC43D3D, 0.2f, 0.5f, "metall:1"); dose.Vacuum = true; dose.Magnet = true;
            var toaster = T("toaster", "Toaster", "appliance", 0xC9CED3, 2f, 3f, "metall:2,elektronik:1"); toaster.Magnet = true;
            var mw = T("mikrowelle", "Mikrowelle", "appliance", 0xE6E6E6, 8f, 6f, "metall:4,elektronik:2,glas:1"); mw.Size = 1.5f; mw.CutInto = new[] { "blech", "platine", "scherben" };
            var kk = T("kuehlschrank", "Kühlschrank", "fridge", 0xEDEDED, 30f, 12f, "metall:9,gefahrstoff:1,elektronik:1"); kk.Grab = false; kk.CutInto = new[] { "blech", "blech", "blech", "kaeltemittel", "platine" }; kk.CutTime = 4f;
            var ew = T("einkaufswagen", "Einkaufswagen", "cart", 0xB9C2C9, 8f, 5f, "metall:5"); ew.Grab = false; ew.Magnet = true; ew.MinMagnet = 1;
            var aw = T("autowrack", "Autowrack", "car", 0x8A5A44, 80f, 30f, "stahl:14,glas:3,kunststoff:3,elektronik:2"); aw.Grab = false; aw.Crane = true; aw.CutInto = new[] { "stahlstueck", "stahlstueck", "stahlstueck", "scherben", "platine", "blech" }; aw.CutTime = 6f; aw.Size = 1f;
            var bus = T("buswrack", "Umgestürzter Stadtbus", "bus", 0xE0A030, 220f, 90f, "stahl:40,glas:10,kunststoff:8,elektronik:4"); bus.Grab = false; bus.Crane = true;
            var fe = T("farbeimer", "Alter Farbeimer", "barrel", 0xD8D14A, 1.5f, 1.5f, "gefahrstoff:1"); fe.Hazard = 1; fe.Size = 0.6f;
            var bat = T("batterie", "Altbatterie", "battery", 0x3A3A3A, 0.3f, 0.3f, "akku:1"); bat.Hazard = 1;
            var gg = T("gartengeraet", "Rostiges Gartengerät", "tool", 0x8C6B4A, 1.5f, 2f, "metall:2"); gg.Magnet = true;
            T("blumentopf", "Kunststoff-Blumentopf", "pot", 0xC0603A, 0.4f, 1f, "kunststoff:1").Vacuum = true;
            var tv = T("fernseher", "Alter Fernseher", "appliance", 0x2F2F35, 5f, 5f, "elektronik:2,glas:2,kunststoff:1"); tv.Size = 1.2f;

            // Teile aus dem Zerlegen (dynamische Objekte)
            var blech = T("blech", "Blechteil", "sheetmetal", 0xA7B0B8, 3f, 2f, "metall:3"); blech.Magnet = true;
            T("platine", "Platine", "board", 0x2E8B57, 0.3f, 0.5f, "elektronik:1");
            T("scherben", "Glasscherben", "shards", 0x9ED9CF, 1.5f, 1f, "glas:2");
            var km = T("kaeltemittel", "Kältemittelbehälter", "barrel", 0xE8493A, 1f, 1f, "gefahrstoff:1"); km.Hazard = 1; km.Size = 0.5f;
            var ss = T("stahlstueck", "Stahlstück", "chunk", 0x6E7B8B, 4f, 2f, "stahl:4"); ss.Magnet = true;
            T("netzstueck", "Netzstück", "netpiece", 0x4F8FBF, 1f, 1f, "netz:2").Vacuum = true;
            var ks = T("kabelstueck", "Kupferkabel-Stück", "cable", 0xD1804A, 1f, 1f, "kupfer:1");
            ks.Size = 0.6f;
            var ak2 = T("akkuzelle", "Akkuzelle", "battery", 0x8FD14F, 0.5f, 0.5f, "akku:1"); ak2.Hazard = 1;
            var sk = T("seltenkern", "Seltenmetall-Kern", "core", 0xB48CE0, 2f, 0.5f, "seltenmetall:1");
            sk.Size = 0.8f;

            // PYRA – Industrie
            var sch = T("schrauben", "Schraubenhaufen", "bolts", 0x7F868C, 0.4f, 0.5f, "metall:1"); sch.Magnet = true; sch.Vacuum = true;
            var st = T("stahltraeger", "Stahlträger", "girder", 0x5E6B78, 12f, 6f, "stahl:8"); st.Grab = false; st.Magnet = true; st.MinMagnet = 2; st.CutInto = new[] { "stahlstueck", "stahlstueck" };
            var kk2 = T("kupferkabel", "Kupferkabeltrommel", "cable", 0xC77A45, 3f, 2f, "kupfer:3"); kk2.Size = 1.1f;
            var zr = T("zahnrad", "Zahnrad", "gear", 0x7D6E5E, 2f, 1f, "stahl:2"); zr.Magnet = true;
            var mt = T("maschinenteil", "Maschinenteil", "machine", 0x8A7F70, 6f, 4f, "metall:3,elektronik:1,kupfer:1"); mt.Magnet = true; mt.MinMagnet = 1;
            var mb = T("motorblock", "Motorblock", "engine", 0x4A4640, 25f, 8f, "stahl:12,kupfer:2"); mb.Grab = false; mb.CutInto = new[] { "stahlstueck", "stahlstueck", "stahlstueck", "kabelstueck", "kabelstueck" }; mb.CutTime = 4f;
            var fw = T("fahrzeugwrack", "Fahrzeugwrack", "truck", 0x9B4A2E, 90f, 40f, "stahl:24,kupfer:4,elektronik:2,glas:2"); fw.Grab = false; fw.Crane = true; fw.CutInto = new[] { "stahlstueck", "stahlstueck", "stahlstueck", "stahlstueck", "kabelstueck", "platine" }; fw.CutTime = 7f;
            var gf = T("gefahrstofffass", "Gefahrstofffass (gekennzeichnet)", "drum", 0xE8B33A, 5f, 3f, "gefahrstoff:3"); gf.Hazard = 2;
            var rs = T("rohrstueck", "Rohrstück", "pipe", 0x8F7A66, 3f, 3f, "stahl:3"); rs.Magnet = true;
            var gw = T("grosswrack", "Verkeiltes Fabrikfahrzeug", "truck", 0xB0512E, 240f, 100f, "stahl:50,kupfer:10,elektronik:4"); gw.Grab = false; gw.Crane = true; gw.Size = 1.35f;

            // PELAGIA – Ozean
            var kan = T("kanister", "Plastikkanister", "canister", 0xE6E1D3, 1f, 2f, "kunststoff:2"); kan.Floating = true;
            var gn = T("geisternetz", "Geisternetz", "net", 0x3E7A9E, 6f, 5f, "netz:6"); gn.Grab = false; gn.CutInto = new[] { "netzstueck", "netzstueck", "netzstueck" }; gn.CutTime = 2.5f;
            var es = T("elektroschrott", "Elektroschrott", "ewaste", 0x3C4A3C, 1.5f, 1f, "elektronik:2");
            var sp = T("schiffsteil", "Schiffsteil", "hullpart", 0x7A4E3A, 6f, 4f, "stahl:4"); sp.Magnet = true; sp.MinMagnet = 2;
            var bo = T("boje", "Kaputte Boje", "buoy", 0xE85D3A, 2f, 3f, "kunststoff:3"); bo.Floating = true;
            var ol = T("oelteppich", "Ölteppich", "oil", 0x151515, 0f, 2f, "oel:4"); ol.Grab = false; ol.Oil = true; ol.Floating = true; ol.Size = 2.2f;
            var pb = T("treibgut", "Treibende Plastikflasche", "pbottle", 0x8FC7F0, 0.2f, 1f, "kunststoff:1"); pb.Floating = true; pb.Vacuum = true;
            var tt = T("treibtuete", "Treibende Tüte", "bag", 0xEDEDE0, 0.1f, 0.5f, "kunststoff:1"); tt.Floating = true; tt.Vacuum = true;
            var wt = T("wrackteil", "Gesunkenes Wrackteil", "hullpart", 0x5B3F31, 20f, 10f, "stahl:10"); wt.Grab = false; wt.Magnet = true; wt.MinMagnet = 2;
            var ank = T("anker", "Anker mit Kette", "anchor", 0x4B4F55, 8f, 4f, "stahl:5"); ank.Magnet = true;

            // NIVALIS – Eis & Technik
            var ak = T("akku", "Industrie-Akku", "battery", 0x8FD14F, 2f, 1f, "akku:2"); ak.Hazard = 1;
            var pl = T("platinenstapel", "Platinenstapel", "board", 0x2E8B57, 1f, 1f, "elektronik:2");
            var sr = T("serverrack", "Serverschrank", "rack", 0x2C3440, 20f, 10f, "elektronik:6,metall:4,kupfer:2"); sr.Grab = false; sr.CutInto = new[] { "platine", "platine", "platine", "blech", "kabelstueck", "kabelstueck" }; sr.CutTime = 4f;
            var sc = T("seltenmetall", "Seltenmetall-Kern", "core", 0xB48CE0, 3f, 1f, "seltenmetall:2");
            var em = T("eismaschine", "Eingefrorene Maschine", "frozen", 0xA8D8F0, 15f, 8f, "metall:6,elektronik:2,kupfer:1"); em.Grab = false; em.CutInto = new[] { "blech", "blech", "platine", "kabelstueck" }; em.CutTime = 3f;
            var kt = T("kabeltrommel", "Kabeltrommel", "cable", 0xC77A45, 4f, 2f, "kupfer:3");
            var so = T("solarbruch", "Solarpanel-Bruch", "panel", 0x2A3B6B, 3f, 3f, "glas:2,elektronik:1");
            var sh = T("shuttlewrack", "Abgestürztes Shuttle", "shuttle", 0xDDE3EA, 260f, 110f, "metall:40,seltenmetall:6,elektronik:10,glas:6"); sh.Grab = false; sh.Crane = true; sh.Size = 1.2f;
            var dr = T("drohnenwrack", "Drohnenwrack", "drone", 0x6C7A89, 2.5f, 2f, "metall:1,elektronik:2"); dr.Magnet = true;

        }

        // ------------------------------------------------------------------ Werkzeuge / Upgrades
        static TechDef Tc(string id, string name, string cat, string desc, string effect, params TechLevel[] levels)
        {
            var t = new TechDef { Id = id, Name = name, Category = cat, Desc = desc, Effect = effect };
            t.Levels.AddRange(levels);
            Tech[id] = t;
            TechOrder.Add(id);
            return t;
        }

        static TechLevel L(int cost, float value, string label) { return new TechLevel(cost, value, label); }

        static void DefineTech()
        {
            Tc("bin", "Müllbehälter", "Behälter", "Mehr Volumen im sichtbaren Rückenbehälter.", "Kapazität",
                L(0, 12, "12 Vol."), L(90, 20, "20 Vol."), L(320, 30, "30 Vol."), L(900, 45, "45 Vol."), L(2200, 65, "65 Vol."));
            Tc("grab", "Greifarm", "Werkzeug", "Hebt einzelne Objekte. Stärke = maximale Masse.", "Tragkraft",
                L(0, 3, "3 kg"), L(250, 6, "6 kg"), L(900, 12, "12 kg"));
            Tc("vacuum", "Müllsauger", "Werkzeug", "Saugt leichte Objekte (bis 1 kg) im Kegel vor MIKO ein.", "Saugrate",
                L(0, 0, "nicht vorhanden"), L(110, 3, "3 Obj./s, 4 m"), L(600, 5, "5 Obj./s, 6 m"), L(1600, 8, "8 Obj./s, 8 m"));
            Tc("magnet", "Magnetarm", "Werkzeug", "Zieht Metall an. Aufladen + Loslassen = Magnetwelle.", "Wellen-Reichweite",
                L(0, 0, "nicht vorhanden"), L(420, 7, "7 m, 6 Teile"), L(1400, 11, "11 m, 12 Teile"), L(3400, 15, "15 m, 20 Teile"));
            Tc("cutter", "Schneidgerät", "Werkzeug", "Zerlegt große Objekte und Netze in tragbare Teile.", "Schnitttempo",
                L(0, 0, "nicht vorhanden"), L(1100, 1f, "Tempo 1,0×"), L(3000, 1.8f, "Tempo 1,8×"));
            Tc("press", "Müllpresse", "Werkzeug", "Presst Papier, Kunststoff, Metall, Stahl, Kupfer und Netze im Behälter auf halbes Volumen.", "Pressfaktor",
                L(0, 1f, "keine Presse"), L(1300, 0.5f, "Volumen ×0,5"));
            Tc("battery", "Akku", "Energie", "Mehr Energie für Werkzeuge und Sprint.", "Kapazität",
                L(0, 100, "100 E"), L(280, 150, "150 E"), L(1000, 220, "220 E"), L(2600, 320, "320 E"));
            Tc("efficiency", "Energieeffizienz", "Energie", "Geräte verbrauchen weniger Energie.", "Verbrauch",
                L(0, 1f, "100 %"), L(700, 0.8f, "80 %"), L(2000, 0.65f, "65 %"));
            Tc("hazard", "Gefahrgutbehälter", "Behälter", "Erlaubt sicheres Sammeln gekennzeichneter Gefahrstoffe.", "Gefahrenklasse",
                L(0, 0, "keine"), L(400, 1, "Klasse 1"), L(1500, 2, "Klasse 1–2"));
            Tc("trailer", "Anhänger", "Behälter", "MIKO zieht einen kleinen Anhänger mit zusätzlichem Volumen.", "Zusatzvolumen",
                L(0, 0, "kein Anhänger"), L(1600, 25, "+25 Vol."));
            Tc("seeder", "Bio-Modul", "Werkzeug", "Pflanzt Setzlinge, setzt Riffmodule und Flechtenkulturen (ökologische Aktionen).", "Modul",
                L(0, 0, "nicht vorhanden"), L(700, 1, "vorhanden"));
            Tc("heat", "Wärmemodul", "Werkzeug", "Taut eingefrorene Objekte auf. Nötig auf NIVALIS.", "Tautempo",
                L(0, 0, "nicht vorhanden"), L(2400, 1f, "Tempo 1,0×"), L(5200, 2f, "Tempo 2,0×")).RequiresPlanet = "nivalis";
            Tc("insulation", "Isolation", "Energie", "Halbiert den Kälteverbrauch auf NIVALIS.", "Kälteverbrauch",
                L(0, 1.6f, "×1,6 bei Kälte"), L(1800, 1.1f, "×1,1 bei Kälte")).RequiresPlanet = "nivalis";
            Tc("dive", "Tauchmodul", "Werkzeug", "Druckfeste Hülle: MIKO kann unter Wasser tauchen und sammeln.", "Tauchen",
                L(0, 0, "nur schwimmen"), L(2600, 1, "tauchen bis 14 m")).RequiresPlanet = "pelagia";
            Tc("filter", "Filtermodul", "Werkzeug", "Reinigt Ölteppiche und belastetes Wasser.", "Filterrate",
                L(0, 0, "nicht vorhanden"), L(2000, 1f, "Rate 1,0×"), L(4200, 2f, "Rate 2,0×")).RequiresPlanet = "pelagia";
            Tc("drones", "Drohnentechnik", "Automatisierung", "Verbessert Sammeldrohnen der Drohnenhangars (Radius und Kapazität).", "Einsatzradius",
                L(0, 35, "35 m, 3 Vol."), L(1800, 50, "50 m, 5 Vol."), L(4500, 70, "70 m, 8 Vol."));
        }

        public static float TechValue(string id, int level)
        {
            TechDef t;
            if (!Tech.TryGetValue(id, out t)) return 0;
            level = M.Clamp(level, 0, t.MaxLevel);
            return t.Levels[level].Value;
        }

        // ------------------------------------------------------------------ Fahrzeuge
        static void DefineVehicles()
        {
            Vehicles["rover"] = new VehicleDef { Id = "rover", Name = "Transportrover", Desc = "Schneller Transporter mit Ladefläche (40 Vol.) und Ansaugschacht. Trägt auch Wracks vom Kran.", Cost = 2200, Speed = 14f, Capacity = 40, Radius = 1.8f };
            Vehicles["crane"] = new VehicleDef { Id = "crane", Name = "Kranfahrzeug", Desc = "Hebt schwere Wracks an und setzt sie auf den Transportrover oder am Stützpunkt ab.", Cost = 3600, Speed = 7f, Capacity = 0, Radius = 2.2f };
            Vehicles["boat"] = new VehicleDef { Id = "boat", Name = "Sammelboot", Desc = "Fischt Treibgut beim Überfahren ein (60 Vol.).", Cost = 4800, Speed = 12f, Capacity = 60, Radius = 2.0f, Planet = "pelagia", Water = true };
        }

        // ------------------------------------------------------------------ Gebäude
        static BuildingDef B(string id, string name, string cat, int w, int h, int cost, string mats, string desc)
        {
            var b = new BuildingDef { Id = id, Name = name, Category = cat, W = w, H = h, Cost = cost, Desc = desc };
            if (!string.IsNullOrEmpty(mats))
                foreach (var part in mats.Split(','))
                {
                    var kv = part.Split(':');
                    b.Mats[kv[0].Trim()] = int.Parse(kv[1]);
                }
            Buildings[id] = b;
            BuildingOrder.Add(id);
            return b;
        }

        static void DefineBuildings()
        {
            var fb = B("foerderband", "Förderband", "Logistik", 1, 1, 15, "metall:2", "Verbindet Anlagen mit dem Stützpunkt-Lager. Transportiert echtes Material.");
            fb.Connector = true; fb.EnergyUse = 0.2f; fb.Color = 0x444A52;
            var so = B("sortierer", "Sortieranlage", "Verarbeitung", 3, 2, 350, "metall:15", "Sortiert unsortiertes Material (1,5 Einheiten/s bei voller Energie).");
            so.Machine = true; so.EnergyUse = 3f; so.Rate = 1.5f; so.MaxCount = 3; so.Color = 0x3FA7A0;
            var pr = B("presse", "Ballenpresse", "Verarbeitung", 2, 2, 550, "metall:25", "Presst 10 sortierte Einheiten zu einem Ballen (alle 5 s). Ballen erzielen 30 % mehr.");
            pr.Machine = true; pr.EnergyUse = 4f; pr.Rate = 0.2f; pr.MaxCount = 3; pr.Color = 0xE07B39;
            var la = B("lager", "Lagerhalle", "Logistik", 2, 2, 180, "metall:10", "+300 Lagerkapazität am Stützpunkt.");
            la.StorageBonus = 300; la.MaxCount = 6; la.Color = 0x8C7A5B;
            var sol = B("solar", "Solarfeld", "Energie", 2, 2, 220, "glas:10,elektronik:3", "+4 Energie für Anlagen.");
            sol.EnergyGen = 4f; sol.MaxCount = 8; sol.Color = 0x2B3F73;
            var gen = B("generator", "Recycling-Generator", "Energie", 2, 2, 700, "stahl:20,kupfer:10", "+10 Energie. Verfügbar nach Energieversorgung auf PYRA.");
            gen.EnergyGen = 10f; gen.MaxCount = 4; gen.RequiresPlanetProject = "pyra_p2"; gen.Color = 0x9B4A2E;
            var dh = B("drohnenhangar", "Drohnenhangar", "Automatisierung", 2, 2, 1100, "metall:20,elektronik:10", "Zwei Sammeldrohnen holen leichte Objekte im Einsatzradius und bringen sie ins Lager.");
            dh.Machine = true; dh.EnergyUse = 3f; dh.MaxCount = 2; dh.Color = 0x5DA9E9;
            var ls = B("ladestation", "Schnellladestation", "Energie", 1, 1, 260, "kupfer:4,metall:4", "Lädt MIKO am Stützpunkt dreimal so schnell.");
            ls.EnergyUse = 1f; ls.MaxCount = 2; ls.Color = 0xF2C14E;
            var lt = B("laterne", "Laterne", "Deko", 1, 1, 20, "metall:1,glas:1", "Warmes Licht für den Stützpunkt.");
            lt.Color = 0xFFD27A;
            var bk = B("bank", "Parkbank", "Deko", 1, 1, 15, "metall:1", "Ein Platz zum Ausruhen – auch für Roboter.");
            bk.Color = 0x8C6B4A;
            var bb = B("beet", "Blumenbeet", "Deko", 1, 1, 25, "kunststoff:2", "Kleines Beet aus recyceltem Kunststoff.");
            bb.Color = 0x6FBF4A;
        }

        // ------------------------------------------------------------------ Planeten
        static List<TrashSpawn> S(params object[] pairs)
        {
            var l = new List<TrashSpawn>();
            for (int i = 0; i < pairs.Length; i += 2) l.Add(new TrashSpawn((string)pairs[i], (int)pairs[i + 1]));
            return l;
        }

        static void DefinePlanets()
        {
            var terra = new PlanetDef
            {
                Id = "terra", Name = "TERRA", Subtitle = "Die vergessene Erde", Order = 0, Seed = 11021,
                Description = "Goldenes Licht über verlassenen Hochhäusern, Einkaufsstraßen und überwucherten Parks.",
                SkyTop = 0x6FA3D8, SkyHorizon = 0xF3C98B, Fog = 0xE6C08A, Ground = 0x8C8676, Ground2 = 0x6F7F4F, Accent = 0xFFB347, Sun = 0xFFE2B0,
                FogDensity = 0.010f, SunIntensity = 1.2f,
                AreaNames = new[] { "Wohnviertel", "Einkaufszentrum", "Botanischer Bezirk" },
                AreaDesc = new[] { "Reihenhäuser, Spielplatz und die alte Buslinie 7.", "Die Konsum-Meile von KONSUMA mit ihren Parkdecks.", "Parks und das zentrale Gewächshaus." },
                EcoAction = "plant", EcoName = "Setzling pflanzen", RepairName = "Straßenlaterne reparieren", Music = "terra",
                MusicScale = new[] { 0, 2, 4, 7, 9 }, MusicRoot = 220f,
                Deliveries = new[] { "zeitung", "karton", "glasflasche", "plastikflasche", "dose", "toaster", "gartengeraet", "fernseher" },
                UnlockHint = "Von Anfang an verfügbar."
            };
            terra.Spawns[0] = S("zeitung", 34, "karton", 20, "glasflasche", 30, "plastikflasche", 34, "tuete", 26, "dose", 30, "toaster", 6, "blumentopf", 10, "farbeimer", 4, "batterie", 6, "fernseher", 4);
            terra.Spawns[1] = S("zeitung", 24, "karton", 30, "glasflasche", 34, "plastikflasche", 36, "tuete", 30, "dose", 34, "toaster", 10, "mikrowelle", 10, "kuehlschrank", 6, "einkaufswagen", 8, "autowrack", 4, "fernseher", 10, "farbeimer", 6, "batterie", 10);
            terra.Spawns[2] = S("zeitung", 16, "karton", 16, "glasflasche", 36, "plastikflasche", 30, "tuete", 20, "dose", 26, "blumentopf", 30, "gartengeraet", 20, "mikrowelle", 6, "kuehlschrank", 4, "autowrack", 3, "farbeimer", 8, "fernseher", 6);
            terra.Gates[0] = new GateDef { Type = "einkaufswagen", Count = 6, Hint = "Eine Barrikade aus Einkaufswagen blockiert die Straße. Ein Magnetarm zieht sie heraus." };
            terra.Gates[1] = new GateDef { Type = "buswrack", Count = 1, Hint = "Ein umgestürzter Bus blockiert den Weg. Hebe ihn mit dem Kran an und transportiere ihn ab." };
            AddPlanet(terra);

            var pyra = new PlanetDef
            {
                Id = "pyra", Name = "PYRA", Subtitle = "Die rostrote Industriewelt", Order = 1, Seed = 22877, ShipLevelRequired = 1, UnlockProject = "terra_p2",
                Description = "Roter Wüstensand, verlassene Fabriken, Schrottschluchten und gigantische Förderanlagen.",
                SkyTop = 0xC26A48, SkyHorizon = 0xF0A56B, Fog = 0xD9875A, Ground = 0xB4583A, Ground2 = 0x8E3F2A, Accent = 0xFF7A3D, Sun = 0xFFC99A,
                FogDensity = 0.012f, SunIntensity = 1.15f, Storms = true,
                AreaNames = new[] { "Schrottmarkt", "Fabrikgürtel", "Gießerei" },
                AreaDesc = new[] { "Der alte Umschlagplatz für Altmetall.", "Endlose Hallen und Förderanlagen.", "Hochöfen, Schlackenfelder und das geplante Recyclingwerk." },
                EcoAction = "plant", EcoName = "Staubbinder-Kaktus pflanzen", RepairName = "Förderknoten reparieren", Music = "pyra",
                MusicScale = new[] { 0, 3, 5, 7, 10 }, MusicRoot = 196f,
                Deliveries = new[] { "schrauben", "zahnrad", "kupferkabel", "maschinenteil", "rohrstueck", "stahlstueck" },
                UnlockHint = "Sprungantrieb I und das Projekt „Wasserkreislauf“ auf TERRA."
            };
            pyra.Spawns[0] = S("schrauben", 40, "zahnrad", 26, "kupferkabel", 20, "rohrstueck", 20, "maschinenteil", 12, "stahltraeger", 8, "dose", 20, "gefahrstofffass", 6, "motorblock", 5);
            pyra.Spawns[1] = S("schrauben", 36, "zahnrad", 26, "kupferkabel", 26, "rohrstueck", 26, "maschinenteil", 20, "stahltraeger", 14, "motorblock", 8, "fahrzeugwrack", 4, "gefahrstofffass", 10);
            pyra.Spawns[2] = S("schrauben", 30, "zahnrad", 24, "kupferkabel", 30, "rohrstueck", 24, "maschinenteil", 20, "stahltraeger", 16, "motorblock", 10, "fahrzeugwrack", 5, "gefahrstofffass", 12);
            pyra.Gates[0] = new GateDef { Type = "stahltraeger", Count = 5, Hint = "Verkeilte Stahlträger. Starker Magnet (Stufe 2) oder Schneidgerät nötig." };
            pyra.Gates[1] = new GateDef { Type = "grosswrack", Count = 1, Hint = "Ein verkeiltes Fabrikfahrzeug versperrt das Tor zur Gießerei. Kran und Transporter!" };
            AddPlanet(pyra);

            var pel = new PlanetDef
            {
                Id = "pelagia", Name = "PELAGIA", Subtitle = "Der vermüllte Ozeanplanet", Order = 2, Seed = 33419, ShipLevelRequired = 2, UnlockProject = "pyra_p2",
                Description = "Türkisfarbene Lagunen, Inselstädte, überflutete Straßen und schwimmende Müllinseln.",
                SkyTop = 0x4FA7D9, SkyHorizon = 0xBFE9F0, Fog = 0x9ED8E0, Ground = 0xD8C9A0, Ground2 = 0x6FA65A, Accent = 0x2FD1C5, Sun = 0xFFF4DA,
                FogDensity = 0.008f, SunIntensity = 1.25f, Water = true, WaterLevel = 0f,
                AreaNames = new[] { "Hafen", "Küstensiedlung", "Lagune" },
                AreaDesc = new[] { "Kräne, Kaimauern und ein verstopftes Hafenbecken.", "Stelzenhäuser auf kleinen Inseln.", "Das tiefe Herz von Pelagia mit versunkenen Ruinen." },
                EcoAction = "reef", EcoName = "Riffmodul setzen", RepairName = "Leuchtboje reparieren", Music = "pelagia",
                MusicScale = new[] { 0, 2, 4, 6, 7, 9, 11 }, MusicRoot = 246.94f,
                Deliveries = new[] { "kanister", "treibgut", "boje", "netzstueck", "elektroschrott", "anker" },
                UnlockHint = "Sprungantrieb II und das Projekt „Energieversorgung“ auf PYRA."
            };
            pel.Spawns[0] = S("kanister", 24, "treibgut", 40, "treibtuete", 30, "boje", 10, "geisternetz", 8, "oelteppich", 6, "anker", 8, "elektroschrott", 14, "schiffsteil", 8, "glasflasche", 16);
            pel.Spawns[1] = S("kanister", 26, "treibgut", 40, "treibtuete", 34, "boje", 12, "geisternetz", 12, "oelteppich", 8, "elektroschrott", 22, "schiffsteil", 12, "anker", 6, "glasflasche", 14);
            pel.Spawns[2] = S("kanister", 20, "treibgut", 36, "treibtuete", 30, "boje", 10, "geisternetz", 14, "oelteppich", 12, "elektroschrott", 34, "schiffsteil", 18, "anker", 8);
            pel.Gates[0] = new GateDef { Type = "geisternetz", Count = 4, Hint = "Riesige Geisternetze sperren die Durchfahrt. Das Schneidgerät zerteilt sie." };
            pel.Gates[1] = new GateDef { Type = "wrackteil", Count = 3, Hint = "Gesunkene Wrackteile blockieren den Kanal. Tauchen und mit starkem Magnet (Stufe 2) bergen." };
            AddPlanet(pel);

            var niv = new PlanetDef
            {
                Id = "nivalis", Name = "NIVALIS", Subtitle = "Die eingefrorene Zukunft", Order = 3, Seed = 44753, ShipLevelRequired = 3, UnlockProject = "pelagia_p2",
                Description = "Blaue Eislandschaften, stillgelegte Forschungsstädte, Raumhäfen und Polarlichter.",
                SkyTop = 0x0E1B3D, SkyHorizon = 0x3C6E9E, Fog = 0x9DB8D6, Ground = 0xDCE8F2, Ground2 = 0xB5CDE3, Accent = 0x7DF9FF, Sun = 0xCFE3FF,
                FogDensity = 0.011f, SunIntensity = 0.85f, Cold = true, Aurora = true,
                AreaNames = new[] { "Forschungsviertel", "Rechenzentrum", "Raumhafen" },
                AreaDesc = new[] { "Labore unter Kuppeln aus Glas.", "Die Server, die das Programm ZWEITE CHANCE berechneten.", "Von hier startete die letzte Arche." },
                EcoAction = "lichen", EcoName = "Flechtenkultur ansiedeln", RepairName = "Wärmeknoten reparieren", Music = "nivalis",
                MusicScale = new[] { 0, 2, 3, 7, 8 }, MusicRoot = 174.61f,
                Deliveries = new[] { "akku", "platinenstapel", "kabeltrommel", "solarbruch", "drohnenwrack", "seltenmetall" },
                UnlockHint = "Sprungantrieb III und das Projekt „Filterstationen“ auf PELAGIA."
            };
            niv.Spawns[0] = S("akku", 30, "platinenstapel", 30, "kabeltrommel", 16, "solarbruch", 16, "drohnenwrack", 18, "eismaschine", 6, "serverrack", 4, "seltenmetall", 8);
            niv.Spawns[1] = S("akku", 30, "platinenstapel", 40, "kabeltrommel", 20, "solarbruch", 14, "drohnenwrack", 16, "eismaschine", 8, "serverrack", 12, "seltenmetall", 12);
            niv.Spawns[2] = S("akku", 36, "platinenstapel", 26, "kabeltrommel", 20, "solarbruch", 24, "drohnenwrack", 24, "eismaschine", 10, "serverrack", 6, "seltenmetall", 18);
            niv.Gates[0] = new GateDef { Type = "eismaschine", Count = 4, Hint = "Eingefrorene Maschinen blockieren die Straße. Erst auftauen (Wärmemodul), dann zerlegen." };
            niv.Gates[1] = new GateDef { Type = "shuttlewrack", Count = 1, Hint = "Ein abgestürztes Shuttle versperrt den Raumhafen. Kran und Transporter!" };
            AddPlanet(niv);
        }

        static void AddPlanet(PlanetDef p) { Planets[p.Id] = p; PlanetOrder.Add(p.Id); }

        // ------------------------------------------------------------------ Großprojekte
        static ProjectDef P(string id, string planet, int area, string name, string desc, int credits, string mats, float time, string effect, params string[] requires)
        {
            var p = new ProjectDef { Id = id, Planet = planet, Area = area, Name = name, Desc = desc, Credits = credits, BuildTime = time, Effect = effect, Requires = requires };
            foreach (var part in mats.Split(','))
            {
                var kv = part.Split(':');
                p.Mats[kv[0].Trim()] = int.Parse(kv[1]);
            }
            Projects[id] = p;
            return p;
        }

        static void DefineProjects()
        {
            P("terra_p1", "terra", 0, "Licht für das Wohnviertel", "Straßenbeleuchtung und Buslinie 7 wieder in Betrieb nehmen.", 250, "glas:30,metall:25,elektronik:6", 15f, "lights");
            P("terra_p2", "terra", 1, "Wasserkreislauf reaktivieren", "Pumpen, Brunnen und Leitungen des Einkaufszentrums reparieren.", 700, "metall:50,kunststoff:40,elektronik:12,glas:20", 20f, "water", "terra_p1").EnergyBonus = 3;
            var tg = P("terra_p3", "terra", 2, "Zentrales Gewächshaus", "Das große Gewächshaus mit den versiegelten Samen wiederaufbauen. GROSSPROJEKT", 1600, "glas:70,metall:60,elektronik:24,kunststoff:30", 30f, "greenhouse", "terra_p2");
            tg.Great = true; tg.EnergyBonus = 4;

            P("pyra_p1", "pyra", 0, "Handelsposten reaktivieren", "Der alte Schrottmarkt kauft wieder an: +15 % auf Verkäufe auf PYRA.", 900, "stahl:50,kupfer:15", 18f, "trade");
            P("pyra_p2", "pyra", 1, "Energieversorgung", "Windturbinen und Kupferleitungen: +15 Energie auf PYRA, Recycling-Generator baubar.", 2200, "stahl:90,kupfer:40,elektronik:15", 24f, "power", "pyra_p1").EnergyBonus = 15;
            var pg = P("pyra_p3", "pyra", 2, "Recyclingwerk", "Die Gießerei wird zum Recyclingwerk: +20 % auf Ballen überall. GROSSPROJEKT", 5000, "stahl:180,kupfer:70,elektronik:35,metall:30", 32f, "recycling", "pyra_p2");
            pg.Great = true; pg.EnergyBonus = 10;

            P("pelagia_p1", "pelagia", 0, "Hafenbecken und Kaimauer", "Hafenbecken säubern und die Kaimauer abdichten.", 1800, "kunststoff:50,stahl:40", 20f, "harbor");
            P("pelagia_p2", "pelagia", 1, "Filterstationen", "Drei Filterstationen reinigen das Küstenwasser.", 3600, "netz:50,elektronik:30,stahl:40", 24f, "filters", "pelagia_p1").EnergyBonus = 5;
            var peg = P("pelagia_p3", "pelagia", 2, "Wasserreinigung und Riff", "Die Lagune wird gereinigt, das Riff kann wieder wachsen. GROSSPROJEKT", 7000, "kunststoff:90,netz:70,elektronik:45,stahl:60", 34f, "reef", "pelagia_p2");
            peg.Great = true; peg.EnergyBonus = 8;

            P("nivalis_p1", "nivalis", 0, "Laborheizung", "Die Kuppellabore werden wieder warm.", 3200, "elektronik:40,metall:30,akku:20", 20f, "heat");
            P("nivalis_p2", "nivalis", 1, "Rechenzentrum", "Die Server des Programms ZWEITE CHANCE laufen wieder an.", 6500, "elektronik:80,seltenmetall:18,kupfer:40", 26f, "servers", "nivalis_p1").EnergyBonus = 6;
            var ng = P("nivalis_p3", "nivalis", 2, "Wärme- und Energienetz", "Das Netz verbindet den Raumhafen – und sendet das Signal an die Arche. GROSSPROJEKT", 11000, "akku:60,seltenmetall:36,metall:80,kupfer:50", 36f, "grid", "nivalis_p2");
            ng.Great = true; ng.EnergyBonus = 12;
        }

        public static string ProjectId(string planet, int area) { return planet + "_p" + (area + 1); }

        // ------------------------------------------------------------------ Aufträge
        static MissionDef Mi(string id, string planet, string kind, string title, string desc, string type, string param, int target, int reward, string cosmetic = null, string prereq = null)
        {
            var m = new MissionDef { Id = id, Planet = planet, Kind = kind, Title = title, Desc = desc, Type = type, Param = param, Target = target, RewardCredits = reward, RewardCosmetic = cosmetic, Prereq = prereq };
            Missions.Add(m);
            return m;
        }

        static void DefineMissions()
        {
            // Tutorial – spielbare erste Mission
            Mi("tut_move", "terra", "tutorial", "Aufwachen", "Fahre ein Stück durch das Wohnviertel.", "move", null, 8, 0);
            Mi("tut_collect", "terra", "tutorial", "Erste Handgriffe", "Sammle 5 Müllobjekte mit dem Greifarm auf.", "collect_any", null, 5, 10, null, "tut_move");
            Mi("tut_zone", "terra", "tutorial", "Der Spielplatz", "Räume den Spielplatz neben dem Stützpunkt komplett auf.", "zone", "terra:0", 1, 25, null, "tut_collect");
            Mi("tut_sell", "terra", "tutorial", "Erster Verkauf", "Bringe deine Ladung zum Stützpunkt und verkaufe sie am Verkaufsterminal.", "sell_any", null, 1, 15, null, "tut_zone");
            Mi("tut_upgrade", "terra", "tutorial", "Besser werden", "Kaufe in der Werkstatt dein erstes Upgrade.", "buy_tech", null, 1, 20, "stern", "tut_sell");
            Mi("tut_sort", "terra", "tutorial", "Sortieren lohnt sich", "Lagere Müll ein und sortiere 10 Einheiten am Sortiertisch.", "sort", null, 10, 25, null, "tut_upgrade");

            // TERRA Nebenaufträge
            Mi("terra_bus", "terra", "side", "Busroute freilegen", "Räume die Haltestelle der Linie 7 frei.", "zone", "terra:1", 1, 60);
            Mi("terra_glass", "terra", "side", "Scherben bringen Glück", "Sammle 40 Einheiten Glas.", "collect_mat", "glas", 40, 80);
            Mi("terra_lamps", "terra", "side", "Es werde Licht", "Repariere 5 Straßenlaternen.", "repair", "terra", 5, 120, "antenne");
            Mi("terra_workshop", "terra", "side", "Werkstatt bergen", "Räume die alte Werkstatt im Einkaufszentrum aus.", "zone", "terra:5", 1, 150);
            Mi("terra_hazard", "terra", "side", "Farbeimer entsorgen", "Entsorge 6 Einheiten Gefahrstoffe fachgerecht.", "dispose", null, 6, 90);

            // PYRA
            Mi("pyra_copper", "pyra", "side", "Kupferfieber", "Sammle 60 Einheiten Kupfer.", "collect_mat", "kupfer", 60, 300);
            Mi("pyra_bales", "pyra", "side", "Ballen für den Markt", "Verkaufe 10 Ballen.", "sell_bales", null, 10, 400);
            Mi("pyra_nodes", "pyra", "side", "Das Band läuft", "Repariere 6 Förderknoten.", "repair", "pyra", 6, 450, "zahnrad");
            Mi("pyra_drums", "pyra", "side", "Gefahrgut sichern", "Entsorge 30 Einheiten Gefahrstoffe.", "dispose", null, 30, 500);

            // PELAGIA
            Mi("pel_oil", "pelagia", "side", "Schwarzes Wasser", "Beseitige 8 Ölteppiche.", "oil", null, 8, 700);
            Mi("pel_nets", "pelagia", "side", "Netze kappen", "Sammle 80 Einheiten Netze.", "collect_mat", "netz", 80, 600);
            Mi("pel_harbor", "pelagia", "side", "Hafenbecken reinigen", "Räume das innere Hafenbecken auf.", "zone", "pelagia:1", 1, 500);
            Mi("pel_buoys", "pelagia", "side", "Leuchtbojen", "Repariere 6 Leuchtbojen.", "repair", "pelagia", 6, 650, "muschel");

            // NIVALIS
            Mi("niv_akkus", "nivalis", "side", "Eingefrorene Akkus sichern", "Taue 15 eingefrorene Objekte auf und berge sie.", "thaw", null, 15, 900);
            Mi("niv_rare", "nivalis", "side", "Seltene Schätze", "Sammle 30 Einheiten seltene Metalle.", "collect_mat", "seltenmetall", 30, 1200);
            Mi("niv_heat", "nivalis", "side", "Warme Knoten", "Repariere 6 Wärmeknoten.", "repair", "nivalis", 6, 1000, "muetze");
            Mi("niv_archive", "nivalis", "side", "Das Archiv", "Finde alle Fundstücke auf NIVALIS.", "lore_planet", "nivalis", 4, 800);
        }

        // ------------------------------------------------------------------ Fundstücke / Geschichte
        static void Lo(string id, string planet, string title, string text)
        {
            Lore[id] = new LoreDef { Id = id, Planet = planet, Title = title, Text = text };
            LoreOrder.Add(id);
        }

        static void DefineLore()
        {
            Lo("terra_1", "terra", "KONSUMA-Werbetafel", "„Warum reparieren? NEU ist besser! KONSUMA liefert alles in 12 Minuten. Alles. Sofort. Immer neu.“ – Darunter hat jemand mit Kreide geschrieben: „Und wohin mit dem Alten?“");
            Lo("terra_2", "terra", "Notiz einer Busfahrerin", "Letzte Fahrt der Linie 7. Die Straßen sind zu, der Bus bleibt hier. Morgen bringen uns die Shuttles zur Arche HORIZONT. „Nur fünf Jahre“, sagen sie. Ich lasse den Schlüssel stecken. Für wen auch immer.");
            Lo("terra_3", "terra", "Kinderzeichnung", "Ein krakeliger Baum mit einer Sonne. Darunter: „Wenn wir zurückkommen, ist er riesengroß. – Lina, 7 Jahre“");
            Lo("terra_4", "terra", "Tagebuch der Gärtnerin", "Ich habe die letzten Samen im zentralen Gewächshaus versiegelt. Tomaten, Linden, Mohn. Irgendwer wird sie finden. Vielleicht kein Mensch. Das wäre auch in Ordnung.");
            Lo("pyra_1", "pyra", "Schichtplan der Gießerei", "Quartalsziel: +18 %. Nächstes Quartal: +22 %. Unter der Tabelle, klein gedruckt: „Recycling-Anteil: 0 %. Begründung: nicht wirtschaftlich.“");
            Lo("pyra_2", "pyra", "Warnschild am Tor", "GEFAHRSTOFFLAGER VOLL. Neue Fässer bitte VOR dem Tor abstellen. – Die Verwaltung");
            Lo("pyra_3", "pyra", "Sprachmemo eines Ingenieurs", "„Ich hatte die Pläne für ein Recyclingwerk fertig. Alles durchgerechnet. Der Vorstand hat gelacht. Jetzt stehen wir am Raumhafen, und die Berge sind höher als die Hochöfen.“");
            Lo("pyra_4", "pyra", "Sandverwehte Postkarte", "„Grüße von PYRA! Die Kakteenfelder blühen gerade, rot und orange, so weit man sehen kann.“ – Das Datum ist fünfzig Jahre alt.");
            Lo("pelagia_1", "pelagia", "Hafenlogbuch", "Tag 212: Wieder drei Netze über Bord. Kosten für die Entsorgung an Land: zu hoch. Das Meer ist groß, sagt der Kapitän.");
            Lo("pelagia_2", "pelagia", "Flaschenpost", "„An wen auch immer: Wir gehen heute an Bord. Ich lasse diese Flasche hier, damit das Meer wenigstens eine gute Nachricht trägt: Wir haben es verstanden. Zu spät, aber verstanden.“");
            Lo("pelagia_3", "pelagia", "Forschungsboje B-7", "Messreihe Riff Süd: Lebende Korallen 71 % … 44 % … 12 % … 0 %. Letzter Eintrag: „Die Filterstationen hätten gereicht.“");
            Lo("pelagia_4", "pelagia", "Kinderbuch „Der kleine Wal“", "„… und als das Wasser wieder klar war, kam der kleine Wal zurück in die Lagune und sang das schönste Lied, das die Insel je gehört hatte.“");
            Lo("nivalis_1", "nivalis", "Serverprotokoll: ZWEITE CHANCE", "PROGRAMM ZWEITE CHANCE. Bedingung: Wiederherstellung der vier Kolonialwelten. Sensor: Lebenszeichen, Wasserqualität, Energienetz. Aktion: Signal an Arche-Flotte. Rückkehr freigegeben.");
            Lo("nivalis_2", "nivalis", "Anzeigetafel Raumhafen", "ARCHE HORIZONT – ABFLUG 06:40 – GATE 3 – PÜNKTLICH. Darunter blinkt seit Jahrzehnten: „Rückflug: unbekannt.“");
            Lo("nivalis_3", "nivalis", "Brief der Direktorin", "„Wir haben die Roboter zurückgelassen, weil wir uns selbst nicht zugetraut haben, aufzuräumen. Ich hoffe, einer von ihnen gibt nicht auf. Wenn du das liest, kleiner Freund: danke.“");
            Lo("nivalis_4", "nivalis", "Tauwasser-Messung", "Eisdicke rückläufig seit Wiederinbetriebnahme der Wärmeknoten. Erste Flechten auf der Südseite der Kuppel B. Ein grüner Punkt in all dem Blau.");
        }

        // ------------------------------------------------------------------ Kosmetik (ohne Spielvorteile)
        static void Co(string id, string kind, string name, uint value, string hint, bool def = false)
        {
            Cosmetics[id] = new CosmeticDef { Id = id, Kind = kind, Name = name, Value = value, Hint = hint, Default = def };
            CosmeticOrder.Add(id);
        }

        static void DefineCosmetics()
        {
            Co("c_tuerkis", "color", "Türkis (Original)", 0x2EC4B6, "Standard", true);
            Co("c_salbei", "color", "Salbeigrün", 0x8DB580, "Großprojekt auf TERRA");
            Co("c_sand", "color", "Wüstensand", 0xD9A566, "Großprojekt auf PYRA");
            Co("c_koralle", "color", "Koralle", 0xFF7F66, "Großprojekt auf PELAGIA");
            Co("c_nachtblau", "color", "Nachtblau", 0x2B3A67, "Großprojekt auf NIVALIS");
            Co("c_sonnengelb", "color", "Sonnengelb", 0xFFD23F, "Kampagne abgeschlossen");
            Co("a_orange", "accent", "Orange (Original)", 0xFF8C2E, "Standard", true);
            Co("a_weiss", "accent", "Weiß", 0xF2F2F2, "Tutorial abgeschlossen");
            Co("a_magenta", "accent", "Magenta", 0xE0457B, "Alle Fundstücke auf TERRA");
            Co("s_none", "sticker", "Kein Aufkleber", 0, "Standard", true);
            Co("stern", "sticker", "Stern", 0xFFD23F, "Erstes Upgrade");
            Co("zahnrad", "sticker", "Zahnrad", 0xC0C0C0, "Auftrag „Das Band läuft“");
            Co("welle", "sticker", "Welle", 0x3FA7D6, "Großprojekt auf PELAGIA");
            Co("flocke", "sticker", "Schneeflocke", 0xCFEFFF, "Großprojekt auf NIVALIS");
            Co("herz", "sticker", "Herz", 0xE0457B, "Kampagne abgeschlossen");
            Co("x_none", "attach", "Kein Anbauteil", 0, "Standard", true);
            Co("antenne", "attach", "Antenne", 0xFF8C2E, "Auftrag „Es werde Licht“");
            Co("blume", "attach", "Blume", 0xFF6FA8, "Großprojekt auf TERRA");
            Co("muschel", "attach", "Muschel", 0xFFD1B8, "Auftrag „Leuchtbojen“");
            Co("muetze", "attach", "Strickmütze", 0xD94A4A, "Auftrag „Warme Knoten“");
            Co("faehnchen", "attach", "Fähnchen", 0x2EC4B6, "Kampagne abgeschlossen");
        }

        /// <summary>Kosmetische Belohnungen für Großprojekte / Meilensteine.</summary>
        public static string[] CosmeticsForProject(string projectId)
        {
            switch (projectId)
            {
                case "terra_p3": return new[] { "c_salbei", "blume" };
                case "pyra_p3": return new[] { "c_sand" };
                case "pelagia_p3": return new[] { "c_koralle", "welle" };
                case "nivalis_p3": return new[] { "c_nachtblau", "flocke" };
                default: return new string[0];
            }
        }

        // ------------------------------------------------------------------ Hilfen
        public static int SellPrice(string mat, Grade grade, float bonusFactor)
        {
            MaterialDef m;
            if (!Materials.TryGetValue(mat, out m)) return 0;
            float f = grade == Grade.Unsorted ? UnsortedFactor : grade == Grade.Sorted ? SortedFactor : BaleFactor * BaleUnits;
            return (int)Math.Floor(m.Price * f * bonusFactor);
        }

        public static int BuyPrice(string mat)
        {
            MaterialDef m;
            if (!Materials.TryGetValue(mat, out m) || !m.Buyable) return 0;
            return (int)Math.Ceiling(m.Price * BuyFactor);
        }

        static void Validate()
        {
            foreach (var t in Trash.Values)
            {
                foreach (var y in t.Yield.Keys)
                    if (!Materials.ContainsKey(y)) throw new Exception("Unbekanntes Material " + y + " in " + t.Id);
                if (t.CutInto != null)
                    foreach (var c in t.CutInto)
                        if (!Trash.ContainsKey(c)) throw new Exception("Unbekanntes Zerlegeteil " + c);
            }
            foreach (var p in Planets.Values)
            {
                for (int a = 0; a < 3; a++)
                    foreach (var s in p.Spawns[a])
                        if (!Trash.ContainsKey(s.Type)) throw new Exception("Unbekannter Mülltyp " + s.Type);
                foreach (var g in p.Gates) if (!Trash.ContainsKey(g.Type)) throw new Exception("Unbekannter Tortyp " + g.Type);
                foreach (var d in p.Deliveries) if (!Trash.ContainsKey(d)) throw new Exception("Unbekannte Lieferung " + d);
            }
            foreach (var pr in Projects.Values)
            {
                foreach (var m in pr.Mats.Keys) if (!Materials.ContainsKey(m)) throw new Exception("Unbekanntes Material im Projekt " + pr.Id);
                foreach (var r in pr.Requires) if (!Projects.ContainsKey(r)) throw new Exception("Unbekannte Voraussetzung " + r);
            }
            foreach (var b in Buildings.Values)
                foreach (var m in b.Mats.Keys) if (!Materials.ContainsKey(m)) throw new Exception("Unbekanntes Material im Gebäude " + b.Id);
            foreach (var mi in Missions)
                if (mi.RewardCosmetic != null && !Cosmetics.ContainsKey(mi.RewardCosmetic)) throw new Exception("Unbekannte Kosmetik " + mi.RewardCosmetic);
        }
    }
}
