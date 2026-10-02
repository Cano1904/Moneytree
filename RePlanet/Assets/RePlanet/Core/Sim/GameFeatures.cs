using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Serverseitige Zusatzsysteme: Sturm abwarten (Zeitraffer), Schnellreise über Lichtpunkte, Helferroboter,
    /// Weltereignisse (Meteoritenschauer, Versorgungsabwurf, freigelegte Deponie) und Erfolge.
    /// </summary>
    public partial class Game
    {
        /// <summary>Zeitraffer beim gemeinsamen Abwarten eines Sturms im Unterschlupf.</summary>
        public const float WaitTimeScale = 4f;
        bool waitFast;
        float achTimer;
        /// <summary>Vom Server versetzte Spieler (Schnellreise): Positionsmeldungen fern der neuen Stelle werden korrigiert,
        /// bis der Client sie übernommen hat – sonst könnte ein verspätetes Paket MIKO an den alten Ort zurückholen.</summary>
        readonly HashSet<string> pinnedPos = new HashSet<string>();

        /// <summary>false = Positionsmeldung ablehnen (Client wird auf die Serverposition korrigiert).</summary>
        bool CheckPinned(PlayerData p, V3 pos)
        {
            if (!pinnedPos.Contains(p.Id)) return true;
            if (V3.DistXZ(pos, p.Pos) > 4f) return false;
            pinnedPos.Remove(p.Id);
            return true;
        }

        /// <summary>Faktor, mit dem die Sitzung die Echtzeit in Spielzeit umrechnet (×4, solange alle den Sturm abwarten).</summary>
        public float TimeScale { get { return waitFast ? WaitTimeScale : 1f; } }

        // ================================================================== Sturm abwarten
        ActResult ActWait(PlayerData p)
        {
            if (p.TowTimer > 0) return ActResult.Fail("MIKO ist abgeschaltet.");
            if (p.Vehicle != null) return ActResult.Fail("Zum Abwarten erst aussteigen.");
            if (!S.Cur.StormActive) return ActResult.Fail("Gerade tobt kein Sturm.");
            int kind = Rules.ShelterKind(S, S.Cur, p.Pos);
            if (kind == 0) return ActResult.Fail("Hier ist kein Unterschlupf. Suche einen " + GameData.Planets[S.CurrentPlanet].ShelterName + " oder baue einen Notunterschlupf (" + Rules.ShelterCost + " Credits).");
            p.Waiting = true;
            p.Sleeping = false;
            p.ShelterKind = kind;
            DPl(p.Id);
            int online = 0, waiting = 0;
            foreach (var q in S.Players.Values) if (q.Online) { online++; if (q.Waiting || q.Sleeping) waiting++; }
            Fx(new JObj().Set("k", "wait").Set("pid", p.Id).Set("n", waiting).Set("of", online));
            return ActResult.OK(new JObj().Set("waiting", waiting).Set("online", online));
        }

        /// <summary>Nach dem Sturmende: Abwartende stehen auf, Erfolgszähler, evtl. freigelegte Deponie.</summary>
        void AfterStorm(PlanetState ps)
        {
            bool sheltered = false;
            foreach (var p in S.Players.Values)
            {
                if (!p.Online) continue;
                if (p.Waiting || p.ShelterKind > 0) sheltered = true;
                if (p.Waiting) { p.Waiting = false; DPl(p.Id); }
            }
            waitFast = false;
            if (sheltered) { S.AddStat("stormsWaited", 1); DW("stats"); }
            MaybeUncoverDump(ps);
        }

        // ================================================================== Schnellreise
        ActResult ActFastTravel(PlayerData p, JObj a)
        {
            int zone = a.Int("z", -2);
            if (Since(p.Id, "ftravel") < GameData.FastTravelCooldown) return ActResult.Fail("Das Lichtnetz lädt noch …");
            float cost; V3 dest;
            var err = Rules.FastTravelCheck(S, S.Cur, p, zone, out cost, out dest);
            if (err != null) return ActResult.Fail(err);
            Mark(p.Id, "ftravel");
            var from = p.Pos;
            p.Energy = Math.Max(0f, p.Energy - cost);
            p.Pos = dest;
            p.Sleeping = false; p.Waiting = false;
            pinnedPos.Add(p.Id);
            moveBudget.Remove(p.Id);
            DPl(p.Id);
            // Helfer, die MIKO folgen, reisen mit
            int k = 0;
            foreach (var b in S.Cur.Bots.Values)
                if (b.Follow == p.Id)
                {
                    float ang = 2.4f + k++ * 1.1f;
                    b.Pos = new V3(dest.x + M.Cos(ang) * 2.2f, 0, dest.z + M.Sin(ang) * 2.2f);
                    b.Pos.y = WorldGen.Get(S.CurrentPlanet).GroundAt(b.Pos.x, b.Pos.z);
                    DP("bots");
                }
            S.AddStat("fastTravels", 1); DW("stats");
            string name = zone < 0 ? "Stützpunkt" : WorldGen.Get(S.CurrentPlanet).Zones[zone].Name;
            Fx(new JObj().Set("k", "fasttravel").Set("pid", p.Id).Set("from", from.ToJson(1)).Set("pos", dest.ToJson(1)).Set("name", name).Set("cost", Json.R(cost, 1)));
            return ActResult.OK(new JObj().Set("pos", dest.ToJson()).Set("cost", Json.R(cost, 1)));
        }

        // ================================================================== Helferroboter
        ActResult ActBotFix(PlayerData p, JObj a)
        {
            var l = WorldGen.Get(S.CurrentPlanet);
            string id = a.Str("s");
            var spot = l.Bots.Find(x => x.Id == id);
            if (spot == null) return ActResult.Fail("Unbekannter Roboter.");
            if (V3.DistXZ(p.Pos, spot.Pos) > 4.5f) return ActResult.Fail("Zu weit entfernt.");
            var why = Rules.HelperRepairCheck(S, S.Cur, id);
            if (why != null) return ActResult.Fail(why);
            int credits = GameData.HelperCredits(S.CurrentPlanet);
            float dt = ProgressDt(p.Id, "botfix", a.Float("dt", 0.25f));
            string key = "bot:" + id;
            float prog;
            S.Cur.Progress.TryGetValue(key, out prog);
            prog += dt / 3f;
            if (prog < 1f) { S.Cur.Progress[key] = prog; return ActResult.OK(new JObj().Set("p", Json.R(prog, 3))); }
            S.Cur.Progress.Remove(key);
            Spend(credits);
            ConsumeMaterials(GameData.HelperMats(S.CurrentPlanet));
            var bot = new HelperBot { Id = id, Home = spot.Pos, Pos = spot.Pos, Yaw = spot.Yaw, Timer = 2f };
            S.Cur.Bots[id] = bot;
            DP("bots");
            S.AddStat("helpersFixed", 1); DW("stats");
            Fx(new JObj().Set("k", "botfixed").Set("s", id).Set("pid", p.Id).Set("pos", spot.Pos.ToJson(1)).Set("name", spot.Name));
            SaveReason = "Helfer repariert";
            return ActResult.OK(new JObj().Set("done", true));
        }

        ActResult ActBotFollow(PlayerData p, JObj a)
        {
            HelperBot b;
            if (!S.Cur.Bots.TryGetValue(a.Str("s") ?? "", out b)) return ActResult.Fail("Hier ist kein Helfer.");
            if (b.Follow == p.Id) return ActBotStay(p, a);
            if (V3.DistXZ(p.Pos, b.Pos) > 6f) return ActResult.Fail("Zu weit entfernt.");
            b.Follow = p.Id; b.State = 4; b.Target = null;
            DP("bots");
            Fx(new JObj().Set("k", "botfollow").Set("s", b.Id).Set("pid", p.Id));
            return ActResult.OK();
        }

        ActResult ActBotStay(PlayerData p, JObj a)
        {
            HelperBot b;
            if (!S.Cur.Bots.TryGetValue(a.Str("s") ?? "", out b)) return ActResult.Fail("Hier ist kein Helfer.");
            if (b.Follow != p.Id && V3.DistXZ(p.Pos, b.Pos) > 6f) return ActResult.Fail("Zu weit entfernt.");
            var err = Rules.HelperWorkplaceCheck(S.Cur, b.Pos);
            if (err != null) return ActResult.Fail(err);
            b.Follow = null; b.Home = b.Pos; b.State = 0; b.Timer = 0.5f; b.Target = null;
            DP("bots");
            Fx(new JObj().Set("k", "botstay").Set("s", b.Id).Set("pid", p.Id).Set("pos", b.Pos.ToJson(1)));
            return ActResult.OK();
        }

        /// <summary>Folgende Helfer bleiben vor einer Reise stehen und arbeiten dort weiter.</summary>
        void StopFollowing(PlanetState ps, string pid = null)
        {
            foreach (var b in ps.Bots.Values)
                if (b.Follow != null && (pid == null || b.Follow == pid)) { b.Follow = null; b.Home = b.Pos; b.State = 0; b.Target = null; DP("bots"); }
        }

        /// <summary>Bewegt den Helfer in Richtung Ziel; true = angekommen.</summary>
        bool MoveBot(HelperBot b, V3 goal, float speed, float stop, float dt)
        {
            float dx = goal.x - b.Pos.x, dz = goal.z - b.Pos.z;
            float d = M.Sqrt(dx * dx + dz * dz);
            if (d <= stop) return true;
            float step = Math.Min(speed * dt, d - stop + 0.01f);
            var l = WorldGen.Get(S.CurrentPlanet);
            float nx = b.Pos.x + dx / d * step, nz = b.Pos.z + dz / d * step;
            b.Pos = new V3(nx, Math.Max(l.GroundAt(nx, nz), Terrain.WaterLevel(S.CurrentPlanet)), nz);
            b.Yaw = M.Atan2(dx, dz);
            return d - step <= stop;
        }

        void UpdateHelpers(float dt)
        {
            var ps = S.Cur;
            if (ps.Bots.Count == 0) return;
            var reserved = new HashSet<string>();
            foreach (var d in Drones) if (d.Target != null) reserved.Add(d.Target);
            foreach (var b in ps.Bots.Values) if (b.Target != null) reserved.Add(b.Target);
            foreach (var b in ps.Bots.Values)
            {
                if (b.Follow != null)
                {
                    PlayerData f;
                    if (!S.Players.TryGetValue(b.Follow, out f) || !f.Online || f.TowTimer > 0)
                    {
                        b.Follow = null; b.Home = b.Pos; b.State = 0; DP("bots");
                        continue;
                    }
                    b.State = 4;
                    MoveBot(b, f.Pos, GameData.HelperFollowSpeed, 2.4f, dt);
                    continue;
                }
                if (ps.StormActive) continue; // Helfer ducken sich im Sturm weg und warten
                switch (b.State)
                {
                    case 0:
                    case 4:
                        {
                            b.State = 0;
                            b.Timer -= dt;
                            if (b.Timer > 0) break;
                            b.Timer = 1.5f;
                            var t = FindBotTarget(ps, b, reserved);
                            if (t != null) { b.Target = t.Key; reserved.Add(t.Key); b.State = 1; }
                            else if (b.Load.Count > 0 || V3.DistXZ(b.Pos, b.Home) > 1.5f) b.State = 2;
                            break;
                        }
                    case 1:
                        {
                            var o = Rules.Obj(ps, b.Target);
                            if (o == null || !Rules.HelperCanPick(S, ps, o)) { reserved.Remove(b.Target ?? ""); b.Target = null; b.State = 0; b.Timer = 0.3f; break; }
                            if (MoveBot(b, o.Pos, GameData.HelperSpeed, 0.9f, dt))
                            {
                                RemoveObj(o);
                                b.Load.Add(new Item { T = o.T.Id });
                                CountCollected(o.T);
                                DP("bots");
                                Fx(new JObj().Set("k", "botpick").Set("s", b.Id).Set("o", o.Key).Set("t", o.T.Id).Set("pos", o.Pos.ToJson(1)));
                                b.Target = null;
                                b.Timer = GameData.HelperPickPause;
                                b.State = b.Load.Count >= GameData.HelperLoad ? 2 : 5;
                            }
                            break;
                        }
                    case 5: // kurze Pause nach dem Aufheben (Helfer sammeln bewusst langsam)
                        b.Timer -= dt;
                        if (b.Timer <= 0) { b.State = 0; b.Timer = 0f; }
                        break;
                    case 2:
                        if (MoveBot(b, b.Home, GameData.HelperSpeed, 0.6f, dt))
                        {
                            if (b.Load.Count > 0) { b.State = 3; b.Timer = 2f; }
                            else { b.State = 0; b.Timer = 4f; }
                        }
                        break;
                    case 3:
                        b.Timer -= dt;
                        if (b.Timer > 0) break;
                        {
                            int n = AddToStorage(b.Load, Grade.Unsorted, false);
                            if (n > 0)
                            {
                                var st = WorldGen.Get(ps.Id).Base.Stations["storage"];
                                Fx(new JObj().Set("k", "botsend").Set("s", b.Id).Set("n", n).Set("pos", b.Pos.ToJson(1)).Set("to", st.ToJson(1)));
                                DP("bots");
                            }
                            if (b.Load.Count > 0) b.Timer = 10f; // Lager voll – später erneut
                            else { b.State = 0; b.Timer = 0.5f; }
                        }
                        break;
                }
            }
        }

        ObjView FindBotTarget(PlanetState ps, HelperBot b, HashSet<string> reserved)
        {
            ObjView best = null; float bd = float.MaxValue;
            foreach (var o in Rules.All(ps))
            {
                if (Math.Abs(o.Pos.x - b.Home.x) > GameData.HelperRadius || Math.Abs(o.Pos.z - b.Home.z) > GameData.HelperRadius) continue;
                if (V3.DistXZ(o.Pos, b.Home) > GameData.HelperRadius) continue;
                if (reserved.Contains(o.Key)) continue;
                if (!Rules.HelperCanPick(S, ps, o)) continue;
                float d = V3.DistXZ(o.Pos, b.Pos);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ================================================================== Weltereignisse
        int LyingEventObjects(PlanetState ps)
        {
            int n = 0;
            foreach (var d in ps.Dyn.Values) if (d.Ev > 0) n++;
            return n;
        }

        bool AreaOpen(PlanetState ps, int area) { for (int g = 0; g < area; g++) if (!Rules.GateOpen(ps, g)) return false; return true; }

        int PickOpenArea(PlanetState ps, Rng r)
        {
            var open = new List<int>();
            for (int a = 0; a < 3; a++) if (AreaOpen(ps, a)) open.Add(a);
            return open.Count == 0 ? 0 : open[r.Range(0, open.Count)];
        }

        /// <summary>Freie, trockene Stelle im Bereich (nicht im Stützpunkt, nicht in Gebäuden, nicht an Durchgängen).</summary>
        bool EventSpot(PlanetState ps, Rng r, int area, V3? near, float radius, out V3 pos)
        {
            var l = WorldGen.Get(ps.Id);
            float water = Terrain.WaterLevel(ps.Id);
            float z0 = area == 0 ? -140f : area == 1 ? -42f : 58f, z1 = area == 0 ? -58f : area == 1 ? 42f : 140f;
            for (int i = 0; i < 160; i++)
            {
                float x, z;
                if (near.HasValue)
                {
                    float ang = r.Range(0f, 6.283f), rad = M.Sqrt(r.Next()) * radius;
                    x = near.Value.x + M.Cos(ang) * rad; z = near.Value.z + M.Sin(ang) * rad;
                }
                else { x = r.Range(-130f, 130f); z = r.Range(z0, z1); }
                if (Math.Abs(x) > 140f || z < z0 || z > z1) continue;
                if (x >= l.Base.MinX - 6f && x <= l.Base.MaxX + 6f && z >= l.Base.MinZ - 6f && z <= l.Base.MaxZ + 6f) continue;
                if (Math.Abs(z + 50f) < 8f || Math.Abs(z - 50f) < 8f) continue;
                if (l.BlockedStatic(x, z, 1.2f)) continue;
                float y = l.GroundAt(x, z);
                if (y < water + 0.3f) continue;
                bool nearSite = false;
                foreach (var s in l.ProjectSites) if (V3.DistXZ(new V3(x, 0, z), s) < 10f) nearSite = true;
                if (nearSite) continue;
                pos = new V3(x, y, z);
                return true;
            }
            pos = default(V3);
            return false;
        }

        void UpdateEvents(float dt)
        {
            var ps = S.Cur;
            if (S.PlayTime < GameData.EventFirstAfter) return;
            if (ps.NextEvent <= 0)
            {
                var r0 = new Rng(GameData.Planets[ps.Id].Seed + 911 + (int)S.PlayTime);
                ps.NextEvent = S.PlayTime + r0.Range(120f, 360f);
                DP("ev");
                return;
            }
            if (S.PlayTime < ps.NextEvent || ps.StormActive || ps.StormWarn) return;
            var r = new Rng(GameData.Planets[ps.Id].Seed * 31 + ps.EventCount * 7919 + 17);
            ps.EventCount++;
            ps.NextEvent = S.PlayTime + r.Range(GameData.EventGapMin, GameData.EventGapMax);
            DP("ev");
            if (LyingEventObjects(ps) >= GameData.EventMaxLying) return;
            if (r.Chance(0.6f)) MeteorShower(ps, r);
            else SupplyDrop(ps, r);
        }

        DynObj SpawnEventObj(PlanetState ps, string type, V3 pos, int area, int ev)
        {
            var d = SpawnDyn(type, pos, area, false, false);
            d.Ev = ev;
            UpdateDyn(d);
            return d;
        }

        /// <summary>Meteoritenschauer: 6–9 wertvolle Splitter gehen in einem zugänglichen Bereich nieder.</summary>
        public bool MeteorShower(PlanetState ps, Rng r)
        {
            int area = PickOpenArea(ps, r);
            V3 center;
            if (!EventSpot(ps, r, area, null, 0, out center)) return false;
            int n = 6 + r.Range(0, 4);
            var pts = new List<object>();
            for (int i = 0; i < n; i++)
            {
                V3 p;
                if (!EventSpot(ps, r, area, center, 14f, out p)) continue;
                SpawnEventObj(ps, "meteorit", p, area, 1);
                pts.Add(p.ToJson(1));
            }
            if (pts.Count == 0) return false;
            S.AddStat("events", 1); DW("stats");
            Fx(new JObj().Set("k", "meteor").Set("pos", center.ToJson(1)).Set("pts", pts).Set("n", pts.Count).Set("name", GameData.Planets[ps.Id].AreaNames[area]));
            return true;
        }

        /// <summary>Versorgungsabwurf: eine (selten zwei) Kisten mit Ersatzteilen landen an einem Fallschirm.</summary>
        public bool SupplyDrop(PlanetState ps, Rng r)
        {
            int area = PickOpenArea(ps, r);
            V3 center;
            if (!EventSpot(ps, r, area, null, 0, out center)) return false;
            int n = r.Chance(0.3f) ? 2 : 1;
            var pts = new List<object>();
            for (int i = 0; i < n; i++)
            {
                V3 p = center;
                if (i > 0 && !EventSpot(ps, r, area, center, 5f, out p)) continue;
                SpawnEventObj(ps, "versorgungskiste", p, area, 2);
                pts.Add(p.ToJson(1));
            }
            S.AddStat("events", 1); DW("stats");
            Fx(new JObj().Set("k", "supply").Set("pos", center.ToJson(1)).Set("pts", pts).Set("n", pts.Count).Set("name", GameData.Planets[ps.Id].AreaNames[area]));
            return true;
        }

        /// <summary>Nach manchen Stürmen liegt eine verschüttete Deponie frei: 10–14 Müllteile des Bereichs.</summary>
        void MaybeUncoverDump(PlanetState ps)
        {
            if (S.PlayTime < GameData.EventFirstAfter) return;
            var r = new Rng(GameData.Planets[ps.Id].Seed * 17 + ps.StormCount * 104729 + 5);
            if (!r.Chance(GameData.DumpChance)) return;
            if (LyingEventObjects(ps) >= GameData.EventMaxLying) return;
            UncoverDump(ps, r);
        }

        public bool UncoverDump(PlanetState ps, Rng r)
        {
            int area = PickOpenArea(ps, r);
            var types = new List<string>();
            foreach (var s in GameData.Planets[ps.Id].Spawns[area])
            {
                var t = GameData.Trash[s.Type];
                if (t.Crane || t.Oil || t.Floating || t.Hazard > 1 || t.Mass > 12f) continue;
                if (t.Grab || t.Magnet || t.CutInto != null) types.Add(t.Id);
            }
            if (types.Count == 0) return false;
            V3 center;
            if (!EventSpot(ps, r, area, null, 0, out center)) return false;
            int n = 10 + r.Range(0, 5);
            int placed = 0;
            for (int i = 0; i < n; i++)
            {
                V3 p;
                if (!EventSpot(ps, r, area, center, 7f, out p)) continue;
                SpawnEventObj(ps, types[r.Range(0, types.Count)], p, area, 3);
                placed++;
            }
            if (placed == 0) return false;
            S.AddStat("events", 1); DW("stats");
            Fx(new JObj().Set("k", "dump").Set("pos", center.ToJson(1)).Set("n", placed).Set("name", GameData.Planets[ps.Id].AreaNames[area]));
            return true;
        }

        // ================================================================== Erfolge
        /// <summary>Prüft alle Erfolge; announce = false beim Laden (schaltet still frei, ohne Meldungsflut).</summary>
        public void EvaluateAchievements(bool announce = true)
        {
            foreach (var a in GameData.Achievements)
            {
                if (S.Achievements.Contains(a.Id)) continue;
                if (Rules.AchievementCounter(S, a) < a.Target) continue;
                S.Achievements.Add(a.Id);
                DW("ach");
                UnlockCosmetic(a.Reward, true);
                if (announce)
                {
                    string rn = GameData.Cosmetics.ContainsKey(a.Reward) ? GameData.Cosmetics[a.Reward].Name : a.Reward;
                    Fx(new JObj().Set("k", "achievement").Set("id", a.Id).Set("name", a.Name).Set("reward", a.Reward).Set("rewardName", rn));
                }
            }
        }

        void TickFeatures(float dt)
        {
            UpdateHelpers(dt);
            TickTnt(dt);
            UpdateEvents(dt);
            achTimer += dt;
            if (achTimer > 1f) { achTimer = 0; EvaluateAchievements(); }
        }
    }
}
