using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    public class ActResult
    {
        public bool Ok;
        public string Err;
        public JObj Data;
        public static ActResult OK(JObj d = null) { return new ActResult { Ok = true, Data = d }; }
        public static ActResult Fail(string e) { return new ActResult { Ok = false, Err = e }; }
        public JObj ToJson()
        {
            var o = new JObj().Set("ok", Ok);
            if (Err != null) o["err"] = Err;
            if (Data != null) o["data"] = Data;
            return o;
        }
    }

    public class Drone
    {
        public int Hangar, Index, State;
        public V3 Pos, Home;
        public string Target;
        public float Load, Search;
        public List<Item> Items = new List<Item>();
    }

    /// <summary>
    /// Autoritative Spielsimulation. Läuft im Solo-Spiel im Spielprozess, im Koop beim Host bzw. auf dem dedizierten Server.
    /// Clients schicken nur Absichten; alle Zustandsänderungen passieren hier.
    /// </summary>
    public partial class Game
    {
        public WorldState S;
        public double Now { get { return S.PlayTime; } }

        // Replikation
        readonly HashSet<string> dirtyWorld = new HashSet<string>();
        readonly HashSet<string> dirtyPlanet = new HashSet<string>();
        readonly HashSet<string> dirtyPlayers = new HashSet<string>();
        readonly Dictionary<string, DynObj> dynUpserts = new Dictionary<string, DynObj>();
        readonly HashSet<string> dynRemovals = new HashSet<string>();
        readonly List<object> fx = new List<object>();
        bool fullPlanet;

        /// <summary>Wird gesetzt, wenn ein sinnvoller Speicherpunkt erreicht ist (Projekt, Reise, Intervall).</summary>
        public string SaveReason;

        public List<Drone> Drones = new List<Drone>();
        readonly Dictionary<string, double> lastAct = new Dictionary<string, double>();
        readonly Dictionary<string, Dictionary<string, double>> helpers = new Dictionary<string, Dictionary<string, double>>();
        readonly HashSet<string> teleportOk = new HashSet<string>();
        /// <summary>Bewegungsguthaben je Spieler in Metern (wächst mit der Zeit, begrenzter Vorrat für Netzschwankungen).</summary>
        readonly Dictionary<string, float> moveBudget = new Dictionary<string, float>();
        readonly HashSet<string> announcedEco = new HashSet<string>();
        readonly float[] lastClean = new float[3];
        float missionTimer, saveTimer, regenTimer, weatherSync;
        bool wasNight;
        EnergyInfo energy;
        bool energyDirty = true;

        public Game(WorldState s)
        {
            GameData.EnsureLoaded();
            S = s;
            foreach (var ps in S.Planets.Values) ps.RecomputeDerived();
            OnPlanetEnter(false);
            EvaluateMissions();
            EvaluateAchievements(false);
        }

        public static WorldState NewWorld(string name, string startPlanet = "terra")
        {
            if (startPlanet == null || !GameData.Planets.ContainsKey(startPlanet) || !GameData.Planets[startPlanet].StartPlanet) startPlanet = "terra";
            var w = new WorldState { WorldName = string.IsNullOrEmpty(name) ? "Neue Welt" : name, Created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") };
            w.Unlocked.Clear();
            foreach (var pd in GameData.Planets.Values) if (pd.StartPlanet) w.Unlocked.Add(pd.Id);
            w.CurrentPlanet = startPlanet;
            w.StartPlanet = startPlanet;
            w.Planet(startPlanet).Visited = true;
            foreach (var c in GameData.Cosmetics.Values) if (c.Default) w.CosmeticUnlocks.Add(c.Id);
            return w;
        }

        void OnPlanetEnter(bool announce)
        {
            var ps = S.Cur;
            ps.Visited = true;
            Rules.UpdateConnectivity(ps);
            energyDirty = true;
            EnsureVehicles();
            RebuildDrones();
            for (int a = 0; a < 3; a++) lastClean[a] = Rules.Cleanliness(ps, a);
            announcedEco.Clear();
            var l = WorldGen.Get(ps.Id);
            foreach (var e in l.Eco) if (Rules.EcoGrowth(S, ps, e.Id) >= 1f) announcedEco.Add(e.Id);
            wasNight = Rules.IsNight(S, ps.Id);
            if (announce) Fx(new JObj().Set("k", "arrive").Set("planet", ps.Id));
        }

        // ------------------------------------------------------------ Dirty-Tracking
        void DW(string part) { dirtyWorld.Add(part); }
        void DP(string part) { dirtyPlanet.Add(part); }
        void DPl(string pid) { dirtyPlayers.Add(pid); }
        void Fx(JObj f) { fx.Add(f); }
        public void MarkAllDirty() { fullPlanet = true; foreach (var p in WorldState.Parts) DW(p); foreach (var p in S.Players.Keys) DPl(p); }

        /// <summary>Sammelt alle Änderungen seit dem letzten Aufruf als Patch für die Clients (null = nichts).</summary>
        public JObj BuildPatch()
        {
            if (dirtyWorld.Count == 0 && dirtyPlanet.Count == 0 && dirtyPlayers.Count == 0 && dynUpserts.Count == 0 && dynRemovals.Count == 0 && fx.Count == 0 && !fullPlanet)
                return null;
            var o = new JObj();
            if (fullPlanet)
            {
                o["planet"] = S.CurrentPlanet;
                o["planetFull"] = S.Cur.ToJson(false);
                dirtyPlanet.Clear(); dynUpserts.Clear(); dynRemovals.Clear();
                fullPlanet = false;
            }
            if (dirtyWorld.Count > 0)
            {
                var w = new JObj();
                foreach (var p in dirtyWorld) w[p] = S.PartToJson(p);
                o["w"] = w;
                dirtyWorld.Clear();
            }
            if (dirtyPlanet.Count > 0)
            {
                var pp = new JObj();
                foreach (var p in dirtyPlanet) pp[p] = S.Cur.PartToJson(p);
                o["p"] = pp;
                o["pid"] = S.CurrentPlanet;
                dirtyPlanet.Clear();
            }
            if (dynUpserts.Count > 0)
            {
                var l = new List<object>();
                foreach (var d in dynUpserts.Values) l.Add(d.ToJson());
                o["dynU"] = l;
                o["pid"] = S.CurrentPlanet;
                dynUpserts.Clear();
            }
            if (dynRemovals.Count > 0)
            {
                o["dynR"] = new List<object>(dynRemovals);
                o["pid"] = S.CurrentPlanet;
                dynRemovals.Clear();
            }
            if (dirtyPlayers.Count > 0)
            {
                var pl = new JObj();
                foreach (var id in dirtyPlayers)
                {
                    PlayerData p;
                    if (S.Players.TryGetValue(id, out p)) pl[id] = p.ToJson(false);
                }
                o["pl"] = pl;
                dirtyPlayers.Clear();
            }
            if (fx.Count > 0)
            {
                o["fx"] = new List<object>(fx);
                fx.Clear();
            }
            return o;
        }

        /// <summary>Schnelle Positionsdaten (Spieler, Fahrzeuge, Drohnen) – werden ~15× pro Sekunde gesendet.</summary>
        public JObj PosPacket()
        {
            // Serverzeit unter "st": "t" ist im Netzprotokoll der Nachrichtentyp und wird von der Sitzung mit "pos" belegt.
            var o = new JObj().Set("st", Math.Round(S.PlayTime, 2));
            var pl = new JObj();
            foreach (var p in S.Players.Values)
                if (p.Online) pl[p.Id] = Json.Arr(Json.R(p.Pos.x), Json.R(p.Pos.y), Json.R(p.Pos.z), Json.R(p.Yaw, 3), p.Flags);
            o["p"] = pl;
            var vv = new JObj();
            foreach (var v in S.Cur.Vehicles.Values) vv[v.Id] = Json.Arr(Json.R(v.Pos.x), Json.R(v.Pos.y), Json.R(v.Pos.z), Json.R(v.Yaw, 3));
            o["v"] = vv;
            var dl = new List<object>();
            foreach (var d in Drones) dl.Add(Json.Arr(Json.R(d.Pos.x, 1), Json.R(d.Pos.y, 1), Json.R(d.Pos.z, 1), d.Items.Count > 0 ? 1 : 0));
            o["d"] = dl;
            if (S.Cur.Bots.Count > 0)
            {
                var bl = new List<object>();
                foreach (var b in S.Cur.Bots.Values) bl.Add(Json.Arr(b.Id, Json.R(b.Pos.x, 2), Json.R(b.Pos.y, 2), Json.R(b.Pos.z, 2), Json.R(b.Yaw, 2), b.State));
                o["b"] = bl;
            }
            return o;
        }

        // ------------------------------------------------------------ Spieler
        public PlayerData Join(string pid, string name)
        {
            PlayerData p;
            bool returning = S.Players.TryGetValue(pid, out p);
            if (!returning)
            {
                p = new PlayerData { Id = pid, Energy = S.MaxEnergy };
                S.Players[pid] = p;
            }
            p.Name = string.IsNullOrEmpty(name) ? "MIKO" : (name.Length > 20 ? name.Substring(0, 20) : name);
            p.Online = true;
            p.Vehicle = null;
            // Bekannte Spieler (geladener Spielstand, Wiederverbinden) machen dort weiter, wo sie aufgehört haben.
            if (!returning || !CanResumeAt(p.Pos)) p.Pos = SpawnPos(pid);
            p.Energy = Math.Min(p.Energy, S.MaxEnergy);
            teleportOk.Add(pid);
            DPl(pid);
            Fx(new JObj().Set("k", "join").Set("pid", pid).Set("name", p.Name));
            return p;
        }

        public void Leave(string pid)
        {
            PlayerData p;
            if (!S.Players.TryGetValue(pid, out p)) return;
            ExitVehicle(p);
            p.Online = false;
            DPl(pid);
            Fx(new JObj().Set("k", "leave").Set("pid", pid).Set("name", p.Name));
        }

        /// <summary>Gespeicherte Position gültig und erreichbar (im Spielfeld, Bereich über offene Tore zugänglich)?</summary>
        bool CanResumeAt(V3 pos)
        {
            if (!pos.IsFinite || Math.Abs(pos.x) > 149 || Math.Abs(pos.z) > 149 || pos.y < -40 || pos.y > 120) return false;
            if (pos.x == 0 && pos.y == 0 && pos.z == 0) return false; // kein Ort gespeichert
            int area = PlanetLayout.AreaOf(pos.z);
            for (int g = 0; g < area; g++) if (!Rules.GateOpen(S.Cur, g)) return false;
            return true;
        }

        V3 SpawnPos(string pid)
        {
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            int idx = 0, i = 0;
            foreach (var k in S.Players.Keys) { if (k == pid) idx = i; i++; }
            float ox = (idx % 4 - 1.5f) * 2.5f;
            return new V3(b.Spawn.x + ox, b.Spawn.y, b.Spawn.z);
        }

        public bool IsOnline(string pid) { PlayerData p; return S.Players.TryGetValue(pid, out p) && p.Online; }

        /// <summary>Positionsmeldung eines Clients. Liefert false, wenn sie abgelehnt wurde (Client wird korrigiert).</summary>
        public bool Move(string pid, V3 pos, float yaw, bool sprint, int flags, string tool, float realDt)
        {
            PlayerData p;
            if (!S.Players.TryGetValue(pid, out p) || !p.Online) return false;
            if (!pos.IsFinite || Math.Abs(pos.x) > 160 || Math.Abs(pos.z) > 160 || pos.y < -40 || pos.y > 120) return false;
            realDt = M.Clamp(realDt, 0f, 1.0f);
            if (!CheckPinned(p, pos)) return false;
            VehicleState veh = null;
            if (p.Vehicle != null) S.Cur.Vehicles.TryGetValue(p.Vehicle, out veh);
            float limit = veh != null ? veh.Def.Speed * 1.3f : 10f;
            float dist = V3.DistXZ(pos, p.Pos);
            bool tele = teleportOk.Remove(pid);
            // Guthaben statt Kulanz je Paket: viele kleine Pakete ergeben so keinen Geschwindigkeitsvorteil.
            // Vorrat = 1 s Höchstgeschwindigkeit + 3 m (gebündelt eintreffende Pakete, Korrekturen).
            float cap = limit * 1.4f + 3f, budget;
            if (!moveBudget.TryGetValue(pid, out budget)) budget = cap;
            budget = Math.Min(cap, budget + limit * 1.4f * realDt);
            if (!tele && dist > budget + 0.3f) { moveBudget[pid] = budget; return false; }
            moveBudget[pid] = tele ? cap : budget - dist; // Toleranz wird ebenfalls verrechnet (kann leicht negativ werden)
            if (!tele && veh == null)
            {
                // Energieverbrauch fürs Fahren
                float cost = dist * 0.012f * (sprint ? 3f : 1f) * EnergyFactor();
                if (cost > 0) { p.Energy = Math.Max(0, p.Energy - cost); DPl(pid); }
                p.Moved += dist;
                S.AddStat("moved", (long)Math.Round(dist * 100)); // Zentimeter
            }
            if (dist > 0.05f) p.LastMoveTime = Now;
            if (p.TowTimer > 0 && !tele) return false; // abgeschaltet – wartet auf Abschleppdrohne
            if ((p.Sleeping || p.Waiting) && dist > 0.6f) { p.Sleeping = false; p.Waiting = false; DPl(pid); }
            p.Pos = pos;
            p.Yaw = yaw;
            p.Flags = flags;
            if (tool != null && tool != p.Tool && (Array.IndexOf(Rules.ToolIds, tool) >= 0)) { p.Tool = tool; DPl(pid); }
            if (veh != null)
            {
                veh.Pos = pos;
                veh.Yaw = yaw;
                if (veh.Carry != null)
                {
                    DynObj d;
                    if (S.Cur.Dyn.TryGetValue(veh.Carry, out d)) d.Pos = new V3(pos.x, pos.y + 3f, pos.z);
                }
            }
            return true;
        }

        public void AllowTeleport(string pid) { teleportOk.Add(pid); }

        float EnergyFactor()
        {
            float f = S.TechVal("efficiency");
            if (GameData.Planets[S.CurrentPlanet].Cold) f *= S.TechVal("insulation");
            return f;
        }

        bool UseEnergy(PlayerData p, float amount)
        {
            if (p.Energy <= 0.01f) return false;
            p.Energy = Math.Max(0, p.Energy - amount * EnergyFactor());
            DPl(p.Id);
            return true;
        }

        const string NoEnergy = "Akku leer – Notbetrieb. Fahre zum Ladeplatz am Stützpunkt (nur der Greifarm funktioniert).";

        // ------------------------------------------------------------ Fahrzeuge
        public void EnsureVehicles()
        {
            var ps = S.Cur;
            var b = WorldGen.Get(ps.Id).Base;
            foreach (var id in S.OwnedVehicles)
            {
                var def = GameData.Vehicles[id];
                if (def.Planet != null && def.Planet != ps.Id) continue;
                if (ps.Vehicles.ContainsKey(id)) continue;
                ps.Vehicles[id] = new VehicleState { Id = id, Pos = VehicleSpawn(id), Yaw = 0 };
                DP("vehicles");
            }
            foreach (var v in ps.Vehicles.Values) v.Driver = null;
        }

        public V3 VehicleSpawn(string id)
        {
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            if (id == "boat") return b.BoatSpot;
            float gy = b.Center.y;
            if (id == "crane") return new V3(-26, gy, -119);
            return new V3(-16, gy, -120);
        }

        void ExitVehicle(PlayerData p)
        {
            if (p.Vehicle == null) return;
            VehicleState v;
            if (S.Cur.Vehicles.TryGetValue(p.Vehicle, out v) && v.Driver == p.Id)
            {
                v.Driver = null;
                DP("vehicles");
                // Aussteigen neben dem Fahrzeug – im Hangar/Laderaum bleibt MIKO drinnen (nicht in oder hinter der Wand)
                float side = v.Def.Radius + 1.2f;
                var np = new V3(v.Pos.x + M.Cos(v.Yaw) * side, v.Pos.y, v.Pos.z - M.Sin(v.Yaw) * side);
                var l = WorldGen.Get(S.CurrentPlanet);
                var room = Rules.RoomAt(l, v.Pos);
                if (room != null && !room.Contains(np, -(Motor.RobotRadius + 0.1f)))
                {
                    var other = new V3(v.Pos.x - M.Cos(v.Yaw) * side, v.Pos.y, v.Pos.z + M.Sin(v.Yaw) * side);
                    np = room.Contains(other, -(Motor.RobotRadius + 0.1f)) ? other : room.Clamp(np, Motor.RobotRadius + 0.1f);
                }
                if (v.Id == "boat") np = new V3(np.x, 0f, np.z);
                else np.y = l.GroundAt(np.x, np.z);
                p.Pos = np;
                teleportOk.Add(p.Id);
            }
            p.Vehicle = null;
            DPl(p.Id);
        }

        // ------------------------------------------------------------ Objekte
        string NewDynId() { return "d" + (S.NextDyn++); }

        DynObj SpawnDyn(string type, V3 pos, int area, bool underwater, bool delivery)
        {
            var d = new DynObj { Id = NewDynId(), Type = type, Pos = pos, Rot = (float)((S.NextDyn * 1.37) % 6.283), Area = area, Underwater = underwater, Delivery = delivery };
            S.Cur.Dyn[d.Id] = d;
            dynUpserts[d.Id] = d;
            dynRemovals.Remove(d.Id);
            DW("flags");
            return d;
        }

        void UpdateDyn(DynObj d) { dynUpserts[d.Id] = d; dynRemovals.Remove(d.Id); }

        void RemoveObj(ObjView o)
        {
            var ps = S.Cur;
            if (o.IsStatic)
            {
                if (!ps.Removed.Set(o.Sid)) return;
                var t = WorldGen.Get(ps.Id).Get(o.Sid);
                if (t.Gate < 0) ps.RemovedWeight[t.Area] += WorldGen.Weight(t.Type);
                DP("rm");
                if (t.Zone >= 0 && Rules.ZoneCleared(ps, t.Zone))
                {
                    var z = WorldGen.Get(ps.Id).Zones[t.Zone];
                    Fx(new JObj().Set("k", "zone").Set("z", t.Zone).Set("name", z.Name));
                }
                if (t.Gate >= 0 && Rules.GateOpen(ps, t.Gate))
                {
                    var def = GameData.Planets[ps.Id];
                    Fx(new JObj().Set("k", "gate").Set("g", t.Gate).Set("area", def.AreaNames[t.Gate + 1]));
                    SaveReason = "Zugang freigelegt";
                }
                if (t.Gate < 0)
                {
                    float c = Rules.Cleanliness(ps, t.Area);
                    if (lastClean[t.Area] < GameData.AreaCleanThreshold && c >= GameData.AreaCleanThreshold)
                    {
                        Fx(new JObj().Set("k", "areaclean").Set("a", t.Area).Set("name", GameData.Planets[ps.Id].AreaNames[t.Area]));
                        SaveReason = "Bereich gereinigt";
                    }
                    lastClean[t.Area] = c;
                }
            }
            else
            {
                if (o.D != null && o.D.Ev > 0 && ps.Dyn.ContainsKey(o.Key)) { S.AddStat("eventItems", 1); DW("stats"); }
                if (ps.Dyn.Remove(o.Key))
                {
                    dynRemovals.Add(o.Key);
                    dynUpserts.Remove(o.Key);
                }
            }
            ps.Progress.Remove("cut:" + o.Key);
            ps.Progress.Remove("lift:" + o.Key);
        }

        void CountCollected(TrashType t)
        {
            S.AddStat("collected", 1);
            foreach (var kv in t.Yield) S.AddStat("mat:" + kv.Key, kv.Value);
            DW("stats");
        }

        // ------------------------------------------------------------ Lager
        int AddToStorage(List<Item> items, Grade g, bool allowOverflow)
        {
            var ps = S.Cur;
            int cap = ps.StorageCap();
            int moved = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var t = GameData.Trash[items[i].T];
                int units = t.TotalUnits;
                if (!allowOverflow && ps.StorageUsed() + units > cap) continue;
                foreach (var kv in t.Yield)
                {
                    var e = ps.Store(kv.Key);
                    if (g == Grade.Sorted) e.S += kv.Value; else e.U += kv.Value;
                }
                items.RemoveAt(i); i--;
                moved++;
            }
            if (moved > 0) DP("storage");
            return moved;
        }

        bool HasMaterials(Dictionary<string, int> mats)
        {
            foreach (var kv in mats) if (S.Cur.Available(kv.Key) < kv.Value) return false;
            return true;
        }

        void ConsumeMaterials(Dictionary<string, int> mats)
        {
            var ps = S.Cur;
            foreach (var kv in mats)
            {
                var e = ps.Store(kv.Key);
                int need = kv.Value;
                int fromS = Math.Min(e.S, need); e.S -= fromS; need -= fromS;
                int fromU = Math.Min(e.U, need); e.U -= fromU; need -= fromU;
                // Rest aus Ballen (die Ballenpresse bündelt sortiertes Material automatisch); angebrochene Ballen bleiben als sortierte Einheiten
                while (need > 0 && e.B > 0) { e.B--; int take = Math.Min(GameData.BaleUnits, need); e.S += GameData.BaleUnits - take; need -= take; }
                if (need > 0) throw new InvalidOperationException("Materialprüfung übersprungen");
            }
            DP("storage");
        }

        bool Spend(long credits)
        {
            if (credits < 0 || S.Credits < credits) return false;
            S.Credits -= credits;
            DW("credits");
            return true;
        }

        void Earn(long credits)
        {
            if (credits <= 0) return;
            S.Credits += credits;
            S.AddStat("credEarned", credits);
            DW("credits"); DW("stats");
        }

        // ------------------------------------------------------------ Tick
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, 0.5f);
            S.PlayTime += dt;
            var ps = S.Cur;
            var pdef = GameData.Planets[ps.Id];

            // Großprojekte bauen
            for (int a = 0; a < 3; a++)
            {
                var pid = GameData.ProjectId(ps.Id, a);
                var st = ps.Projects[pid];
                if (st.Started && !st.Done)
                {
                    st.Progress = Math.Min(1f, st.Progress + dt / GameData.Projects[pid].BuildTime);
                    DP("projects");
                    if (st.Progress >= 1f) CompleteProject(pid);
                }
            }

            if (energyDirty) { energy = Rules.Energy(ps); energyDirty = false; }
            UpdateMachines(dt);
            UpdateDrones(dt);
            TickFeatures(dt);

            // Wetter: Wind, Sturmwarnung und Stürme auf allen Planeten
            ps.StormTimer += dt;
            if (!ps.StormActive)
            {
                if (!ps.StormWarn && ps.StormTimer > pdef.StormEvery - 30f)
                {
                    ps.StormWarn = true; DP("weather");
                    Fx(new JObj().Set("k", "stormwarn").Set("name", pdef.StormName));
                }
                if (ps.StormTimer > pdef.StormEvery)
                {
                    ps.StormActive = true; ps.StormWarn = false; ps.StormTimer = 0; DP("weather");
                    Fx(new JObj().Set("k", "storm").Set("on", true).Set("name", pdef.StormName));
                }
            }
            else if (ps.StormTimer > pdef.StormDuration)
            {
                EndStorm(ps, pdef);
            }
            weatherSync += dt;
            if (weatherSync > 5f) { weatherSync = 0; DP("weather"); }

            // Nacht und Sturm: Unterschlupf suchen, Energie laden, Notabschaltung
            bool night = Rules.IsNight(S, ps.Id);
            if (night != wasNight)
            {
                wasNight = night;
                Fx(new JObj().Set("k", night ? "nightfall" : "daybreak"));
            }
            regenTimer += dt;
            bool slow = regenTimer >= 0.25f;
            float rt = regenTimer;
            if (slow) regenTimer = 0;
            var charge = WorldGen.Get(ps.Id).Base.Stations["charge"];
            var store = WorldGen.Get(ps.Id).Base.Stations["storage"];
            bool fast = Rules.CountOf(ps, "ladestation") > 0;
            int online = 0, sleepers = 0, resting = 0;
            foreach (var p in S.Players.Values)
            {
                if (!p.Online) continue;
                online++;
                if (p.TowTimer > 0)
                {
                    p.TowTimer -= dt;
                    if (p.TowTimer <= 0) Tow(p);
                    continue;
                }
                if (!slow) { if (p.Sleeping) sleepers++; if (p.Sleeping || p.Waiting) resting++; continue; }
                int kind = Rules.ShelterKind(S, ps, p.Pos);
                if (kind != p.ShelterKind)
                {
                    if (kind >= Rules.ShelterField) { S.AddStat("shelterVisits", 1); DW("stats"); }
                    p.ShelterKind = kind; DPl(p.Id);
                }
                bool exposed = (night || ps.StormActive) && kind == 0 && p.Vehicle == null;
                if (exposed != p.Exposed) { p.Exposed = exposed; DPl(p.Id); }
                if (p.Sleeping && kind == 0) { p.Sleeping = false; DPl(p.Id); }
                if (p.Waiting && (kind == 0 || !ps.StormActive)) { p.Waiting = false; DPl(p.Id); }
                float max = S.MaxEnergy;
                if (exposed)
                {
                    float drain = ((night ? 0.55f : 0f) + (ps.StormActive ? 0.9f : 0f)) * EnergyFactor();
                    p.Energy = Math.Max(0, p.Energy - drain * rt);
                    DPl(p.Id);
                    if (p.Energy <= 0.01f) Shutdown(p);
                    continue;
                }
                float rate = 0;
                // Ladeplatz, Lager-Annahme und die Ladesäule im Hangar laden schnell
                if (V3.DistXZ(p.Pos, charge) < 6f || V3.DistXZ(p.Pos, store) < 6f || kind == Rules.ShelterHangar) rate = 16f * (fast ? 3f : 1f);
                else if (p.Sleeping) rate = 3f;
                else if (!night && Now - p.LastMoveTime > 2.0) rate = pdef.Cold ? 0.3f : 0.6f;
                if (p.Energy > max) { p.Energy = max; DPl(p.Id); }
                else if (rate > 0 && p.Energy < max)
                {
                    p.Energy = Math.Min(max, p.Energy + rate * rt);
                    DPl(p.Id);
                }
                if (p.Sleeping) sleepers++;
                if (p.Sleeping || p.Waiting) resting++;
            }
            // Sturm gemeinsam im Unterschlupf abwarten → Zeitraffer (der Sturm läuft weiter, nur schneller)
            waitFast = online > 0 && resting == online && ps.StormActive;
            // Alle schlafen geschützt → die Nacht wird übersprungen. Ein Sturm lässt sich nicht verschlafen:
            // er läuft weiter, die Schläfer warten ihn im Unterschlupf ab (Zeitraffer, siehe TimeScale).
            if (online > 0 && sleepers == online && night)
            {
                ps.DayOffset += Rules.SecondsUntilMorning(S, ps.Id); DP("weather");
                foreach (var p in S.Players.Values)
                {
                    if (!p.Online) continue;
                    p.Sleeping = false;
                    p.Waiting = ps.StormActive && p.ShelterKind > 0;
                    p.Energy = S.MaxEnergy;
                    DPl(p.Id);
                }
                S.AddStat("nights", 1); DW("stats");
                wasNight = Rules.IsNight(S, ps.Id);
                Fx(new JObj().Set("k", "morning").Set("storm", ps.StormActive));
                SaveReason = "Nach dem Schlafen";
            }

            // Wachstum
            var l = WorldGen.Get(ps.Id);
            foreach (var e in l.Eco)
            {
                if (announcedEco.Contains(e.Id)) continue;
                if (ps.Eco.ContainsKey(e.Id) && Rules.EcoGrowth(S, ps, e.Id) >= 1f)
                {
                    announcedEco.Add(e.Id);
                    Fx(new JObj().Set("k", "grown").Set("s", e.Id));
                    if (Rules.EcoFraction(S, ps, e.Area) >= 0.999f)
                    {
                        Fx(new JObj().Set("k", "eco").Set("a", e.Area).Set("name", pdef.AreaNames[e.Area]));
                        SaveReason = "Ökologie abgeschlossen";
                    }
                }
            }

            missionTimer += dt;
            if (missionTimer > 0.5f) { missionTimer = 0; EvaluateMissions(); }
            saveTimer += dt;
            if (saveTimer > 120f) { saveTimer = 0; if (SaveReason == null) SaveReason = "auto"; }
        }

        void EndStorm(PlanetState ps, PlanetDef pdef)
        {
            ps.StormActive = false; ps.StormWarn = false; ps.StormTimer = 0; ps.StormCount++;
            DP("weather");
            Fx(new JObj().Set("k", "storm").Set("on", false).Set("name", pdef.StormName).Set("dunes", pdef.Storms));
            AfterStorm(ps);
        }

        /// <summary>Energie leer bei Nacht/Sturm ohne Unterschlupf: MIKO schaltet ab, eine Drohne schleppt es zum Stützpunkt.</summary>
        void Shutdown(PlayerData p)
        {
            if (p.TowTimer > 0) return;
            ExitVehicle(p);
            p.Sleeping = false;
            p.TowTimer = 6f;
            DPl(p.Id);
            S.AddStat("shutdowns", 1); DW("stats");
            Fx(new JObj().Set("k", "shutdown").Set("pid", p.Id).Set("name", p.Name));
        }

        void Tow(PlayerData p)
        {
            var ps = S.Cur;
            p.TowTimer = -1f;
            p.Pos = WorldGen.Get(ps.Id).Base.Stations["charge"];
            p.Energy = S.MaxEnergy * 0.4f;
            p.Exposed = false;
            teleportOk.Add(p.Id);
            // Zeitverlust: Allein im Spiel vergeht die Nacht während des Abschleppens
            int online = 0;
            foreach (var q in S.Players.Values) if (q.Online) online++;
            if (online == 1 && Rules.IsNight(S, ps.Id)) { ps.DayOffset += Rules.SecondsUntilMorning(S, ps.Id); DP("weather"); }
            DPl(p.Id);
            Fx(new JObj().Set("k", "towed").Set("pid", p.Id).Set("pos", p.Pos.ToJson(1)));
        }

        void UpdateMachines(float dt)
        {
            var ps = S.Cur;
            float eff = energy.Efficiency;
            bool changed = false;
            foreach (var b in ps.Buildings)
            {
                if (!b.Def.Machine || !b.Connected) continue;
                if (b.Type == "sortierer")
                {
                    b.Acc += b.Def.Rate * eff * dt;
                    while (b.Acc >= 1f)
                    {
                        string best = null; int bu = 0;
                        foreach (var kv in ps.Storage)
                            if (kv.Value.U > bu && GameData.Materials[kv.Key].Price > 0) { bu = kv.Value.U; best = kv.Key; }
                        if (best == null) { b.Acc = Math.Min(b.Acc, 1f); break; }
                        ps.Storage[best].U--; ps.Storage[best].S++;
                        b.Acc -= 1f;
                        S.AddStat("sorted", 1);
                        changed = true;
                    }
                }
                else if (b.Type == "presse")
                {
                    b.Acc += dt * eff;
                    if (b.Acc >= 5f)
                    {
                        string best = null; int bs = GameData.BaleUnits - 1;
                        foreach (var kv in ps.Storage)
                            if (GameData.Materials[kv.Key].Pressable && kv.Value.S > bs) { bs = kv.Value.S; best = kv.Key; }
                        if (best != null)
                        {
                            ps.Storage[best].S -= GameData.BaleUnits;
                            ps.Storage[best].B++;
                            b.Acc = 0;
                            changed = true;
                            Fx(new JObj().Set("k", "bale").Set("b", b.Id).Set("m", best));
                        }
                        else b.Acc = 5f;
                    }
                }
            }
            if (changed) { DP("storage"); DW("stats"); }
        }

        // ------------------------------------------------------------ Drohnen
        public void RebuildDrones()
        {
            var ps = S.Cur;
            var keep = new List<Drone>();
            var bl = WorldGen.Get(ps.Id).Base;
            foreach (var b in ps.Buildings)
            {
                if (b.Type != "drohnenhangar") continue;
                var home = bl.CellCenter(b.Gx, b.Gz, b.W, b.H);
                home.y = Terrain.HeightAt(ps.Id, home.x, home.z) + 3f;
                for (int i = 0; i < 2; i++)
                {
                    var ex = Drones.Find(d => d.Hangar == b.Id && d.Index == i);
                    if (ex != null) { ex.Home = home; keep.Add(ex); }
                    else keep.Add(new Drone { Hangar = b.Id, Index = i, Pos = home, Home = home, Search = i * 0.3f });
                }
            }
            // Beladene Drohnen abgerissener Hangars liefern ihre Ladung ab
            foreach (var d in Drones)
                if (!keep.Contains(d) && d.Items.Count > 0) AddToStorage(d.Items, Grade.Unsorted, true);
            Drones = keep;
        }

        bool DroneEligible(ObjView o)
        {
            var t = o.T;
            if (o.Gate >= 0 || t.Crane || t.Oil || t.Hazard > 0 || o.Frozen || o.Underwater) return false;
            if (t.Mass > 3f || !(t.Grab || t.Vacuum)) return false;
            if (o.D != null && o.D.CarriedBy != null) return false;
            return true;
        }

        void UpdateDrones(float dt)
        {
            if (Drones.Count == 0) return;
            var ps = S.Cur;
            float radius = S.TechVal("drones");
            float cap = S.TechLevel("drones") == 0 ? 3f : S.TechLevel("drones") == 1 ? 5f : 8f;
            float eff = energy.Efficiency;
            float speed = 9f * Math.Max(0.2f, eff);
            var reserved = new HashSet<string>();
            foreach (var d in Drones) if (d.Target != null) reserved.Add(d.Target);
            foreach (var hb in S.Cur.Bots.Values) if (hb.Target != null) reserved.Add(hb.Target);
            foreach (var d in Drones)
            {
                Building hangar = ps.Buildings.Find(b => b.Id == d.Hangar);
                bool active = hangar != null && hangar.Connected && eff > 0.01f;
                if (d.State == 0)
                {
                    if (!active) continue;
                    d.Search -= dt;
                    if (d.Search > 0) continue;
                    d.Search = 0.6f;
                    var tgt = FindDroneTarget(ps, d.Home, d.Pos, radius, reserved);
                    if (tgt != null) { d.Target = tgt.Key; reserved.Add(tgt.Key); d.State = 1; }
                }
                else if (d.State == 1)
                {
                    var o = Rules.Obj(ps, d.Target);
                    if (o == null || !DroneEligible(o))
                    {
                        d.Target = null;
                        d.State = d.Items.Count > 0 ? 2 : 0;
                        continue;
                    }
                    var goal = new V3(o.Pos.x, o.Pos.y + 1.5f, o.Pos.z);
                    if (FlyTo(d, goal, speed, dt))
                    {
                        RemoveObj(o);
                        d.Items.Add(new Item { T = o.T.Id });
                        d.Load += Math.Max(0.25f, o.T.Volume);
                        CountCollected(o.T);
                        Fx(new JObj().Set("k", "dronepick").Set("o", o.Key));
                        d.Target = null;
                        var next = d.Load + 1f <= cap ? FindDroneTarget(ps, d.Home, d.Pos, radius, reserved) : null;
                        if (next != null) { d.Target = next.Key; reserved.Add(next.Key); d.State = 1; }
                        else d.State = 2;
                    }
                }
                else if (d.State == 2)
                {
                    if (FlyTo(d, d.Home, speed, dt))
                    {
                        AddToStorage(d.Items, Grade.Unsorted, false);
                        if (d.Items.Count == 0) { d.Load = 0; d.State = 0; }
                        else { d.Search = 3f; } // Lager voll – warten
                    }
                }
            }
        }

        bool FlyTo(Drone d, V3 goal, float speed, float dt)
        {
            var delta = goal - d.Pos;
            float dist = delta.Length;
            float step = speed * dt;
            if (dist <= step || dist < 0.3f) { d.Pos = goal; return true; }
            d.Pos = d.Pos + delta * (step / dist);
            if (V3.DistXZ(goal, d.Pos) > 8f)
            {
                float ground = Math.Max(Terrain.HeightAt(S.CurrentPlanet, d.Pos.x, d.Pos.z), Terrain.WaterLevel(S.CurrentPlanet));
                d.Pos.y = M.Lerp(d.Pos.y, Math.Max(goal.y, ground) + 7f, Math.Min(1f, dt * 2f));
            }
            return false;
        }

        ObjView FindDroneTarget(PlanetState ps, V3 home, V3 from, float radius, HashSet<string> reserved)
        {
            ObjView best = null; float bd = float.MaxValue;
            foreach (var o in Rules.All(ps))
            {
                if (reserved.Contains(o.Key)) continue;
                if (V3.DistXZ(o.Pos, home) > radius) continue;
                if (!DroneEligible(o)) continue;
                float d = V3.DistXZ(o.Pos, from);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ------------------------------------------------------------ Projekte / Fortschritt
        void CompleteProject(string pid)
        {
            var pd = GameData.Projects[pid];
            var ps = S.Planet(pd.Planet);
            var st = ps.Projects[pid];
            if (st.Done) return;
            st.Done = true; st.Progress = 1f;
            DP("projects");
            energyDirty = true;
            foreach (var c in GameData.CosmeticsForProject(pid)) UnlockCosmetic(c);
            Fx(new JObj().Set("k", "awaken").Set("project", pid).Set("great", pd.Great).Set("name", pd.Name).Set("area", pd.Area));
            CheckUnlocks();
            CheckCampaign();
            SaveReason = "Projekt abgeschlossen";
        }

        /// <summary>quiet = ohne eigene Meldung (z. B. bei Erfolgen, deren Meldung die Belohnung nennt).</summary>
        void UnlockCosmetic(string id, bool quiet = false)
        {
            if (!GameData.Cosmetics.ContainsKey(id)) return;
            if (S.CosmeticUnlocks.Add(id))
            {
                DW("cosm");
                var f = new JObj().Set("k", "cosmetic").Set("id", id).Set("name", GameData.Cosmetics[id].Name);
                if (quiet) f["quiet"] = true;
                Fx(f);
            }
        }

        void CheckUnlocks()
        {
            foreach (var pl in GameData.PlanetOrder)
            {
                if (S.Unlocked.Contains(pl)) continue;
                if (Rules.PlanetUnlockable(S, pl))
                {
                    S.Unlocked.Add(pl);
                    DW("unlocked");
                    Fx(new JObj().Set("k", "unlock").Set("planet", pl).Set("name", GameData.Planets[pl].Name));
                }
            }
        }

        void CheckCampaign()
        {
            if (S.CampaignDone) return;
            foreach (var pl in GameData.PlanetOrder)
                if (!S.Planet(pl).Projects[GameData.ProjectId(pl, 2)].Done) return;
            S.CampaignDone = true;
            DW("flags");
            UnlockCosmetic("c_sonnengelb"); UnlockCosmetic("herz"); UnlockCosmetic("faehnchen");
            Fx(new JObj().Set("k", "ending"));
            SaveReason = "Kampagne abgeschlossen";
        }

        // ------------------------------------------------------------ Missionen
        long Counter(MissionDef m)
        {
            switch (m.Type)
            {
                case "move": return S.Stat("moved") / 100;
                case "collect_any": return S.Stat("collected");
                case "collect_mat": return S.Stat("mat:" + m.Param);
                case "sell_any": return S.Stat("sales");
                case "buy_tech": return S.Stat("techBought");
                case "sort": return S.Stat("sorted");
                case "dispose": return S.Stat("disposed");
                case "sell_bales": return S.Stat("balesSold");
                case "oil": return S.Stat("oil");
                case "thaw": return S.Stat("thawed");
                case "shelter_visit": return S.Stat("shelterVisits");
            }
            return 0;
        }

        static bool IsCounter(string type) { return type != "zone" && type != "repair" && type != "lore_planet"; }

        long Measure(MissionDef m, MissionState ms)
        {
            switch (m.Type)
            {
                case "zone":
                    {
                        var parts = m.Param.Split(':');
                        var ps = S.Planet(parts[0] == "start" ? S.StartPlanet : parts[0]);
                        return Rules.ZoneCleared(ps, int.Parse(parts[1])) ? 1 : 0;
                    }
                case "repair": return S.Planet(m.Param).Repaired.Count;
                case "lore_planet":
                    {
                        int n = 0;
                        foreach (var id in S.Lore) if (GameData.Lore.ContainsKey(id) && GameData.Lore[id].Planet == m.Param) n++;
                        return n;
                    }
            }
            return Counter(m) - ms.Base;
        }

        public void EvaluateMissions()
        {
            foreach (var m in GameData.Missions)
            {
                MissionState ms;
                if (!S.Missions.TryGetValue(m.Id, out ms)) { ms = new MissionState(); S.Missions[m.Id] = ms; DW("missions"); }
                if (ms.Status == 2) continue;
                if (ms.Status == 0)
                {
                    if (m.Prereq != null)
                    {
                        MissionState pre;
                        if (!S.Missions.TryGetValue(m.Prereq, out pre) || pre.Status != 2) continue;
                    }
                    if (m.Kind != "tutorial" && !S.Planet(m.Planet).Visited) continue;
                    ms.Status = 1;
                    ms.Base = IsCounter(m.Type) ? Counter(m) : 0;
                    ms.Progress = 0;
                    DW("missions");
                    if (m.Kind != "tutorial") Fx(new JObj().Set("k", "mission_new").Set("id", m.Id).Set("title", m.Title));
                }
                long prog = Math.Max(0, Measure(m, ms));
                if (prog != ms.Progress) { ms.Progress = prog; DW("missions"); }
                if (prog >= m.Target)
                {
                    ms.Status = 2;
                    DW("missions");
                    if (!ms.Claimed)
                    {
                        ms.Claimed = true;
                        Earn(m.RewardCredits);
                        if (m.RewardCosmetic != null) UnlockCosmetic(m.RewardCosmetic);
                        if (m.Id == "tut_sort") UnlockCosmetic("a_weiss");
                    }
                    Fx(new JObj().Set("k", "mission_done").Set("id", m.Id).Set("title", m.Title).Set("reward", m.RewardCredits));
                }
            }
        }

        public List<string> AvailablePlanetsForTravel()
        {
            var l = new List<string>();
            foreach (var p in GameData.PlanetOrder) if (S.Unlocked.Contains(p)) l.Add(p);
            return l;
        }
    }
}
