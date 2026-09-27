using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    public partial class Game
    {
        const string GuestDenied = "Teure Käufe, Abriss und Reisen sind dem Host vorbehalten (Host kann den Vertrauensmodus aktivieren).";

        bool Allowed(bool isHost, long cost) { return isHost || S.TrustGuests || cost < GameData.GuestExpensiveThreshold; }

        bool NearStation(PlayerData p, string station, float extra = 0f)
        {
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            V3 pos;
            if (!b.Stations.TryGetValue(station, out pos)) return false;
            return V3.DistXZ(p.Pos, pos) <= GameData.StationRange + extra;
        }

        bool InBase(PlayerData p)
        {
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            return p.Pos.x >= b.MinX - 4 && p.Pos.x <= b.MaxX + 4 && p.Pos.z >= b.MinZ - 4 && p.Pos.z <= b.MaxZ + 4;
        }

        /// <summary>Zeit seit der letzten Aktion dieser Art – begrenzt Aktionsraten (Schutz gegen manipulierte Clients).</summary>
        float Since(string pid, string key)
        {
            double t;
            string k = pid + "|" + key;
            float since = lastAct.TryGetValue(k, out t) ? (float)(Now - t) : 10f;
            return since;
        }

        void Mark(string pid, string key) { lastAct[pid + "|" + key] = Now; }

        float ProgressDt(string pid, string key, float requested)
        {
            float since = Since(pid, key);
            Mark(pid, key);
            return M.Clamp(Math.Min(requested, since + 0.12f), 0f, 0.5f);
        }

        bool InReach(PlayerData p, ObjView o, float reach)
        {
            float d = V3.DistXZ(p.Pos, o.Pos);
            if (d > reach + Rules.ObjRadius(o.T)) return false;
            float dy = Math.Abs(p.Pos.y - o.Pos.y);
            return dy <= 3.5f + (o.T.Crane ? 3f : 0f);
        }

        /// <summary>Wendet eine Spieleraktion an. Nur der Host/Server ruft das auf.</summary>
        public ActResult Apply(string pid, JObj a, bool isHost)
        {
            PlayerData p;
            if (a == null) return ActResult.Fail("Ungültige Aktion.");
            if (!S.Players.TryGetValue(pid, out p) || !p.Online) return ActResult.Fail("Spieler nicht verbunden.");
            string kind = a.Str("a", "");
            try
            {
                switch (kind)
                {
                    case "grab": return ActGrab(p, a);
                    case "vacuum": return ActVacuum(p, a);
                    case "magnet": return ActMagnet(p, a);
                    case "cut": return ActCut(p, a);
                    case "thaw": return ActThaw(p, a);
                    case "filter": return ActFilter(p, a);
                    case "press": return ActPress(p);
                    case "deposit": return ActDeposit(p);
                    case "sellbin": return ActSellBin(p);
                    case "sell": return ActSell(p, a);
                    case "sortm": return ActSortManual(p, a);
                    case "dispose": return ActDispose(p);
                    case "buytech": return ActBuyTech(p, a, isHost);
                    case "buyveh": return ActBuyVehicle(p, a, isHost);
                    case "buyship": return ActBuyShip(p, isHost);
                    case "buymat": return ActBuyMat(p, a, isHost);
                    case "build": return ActBuild(p, a, isHost);
                    case "move": return ActMoveBuilding(p, a);
                    case "demolish": return ActDemolish(p, a, isHost);
                    case "repair": return ActRepair(p, a);
                    case "plant": return ActPlant(p, a);
                    case "lore": return ActLore(p, a);
                    case "view": return ActView(p, a);
                    case "project": return ActProject(p, a, isHost);
                    case "venter": return ActVehicleEnter(p, a);
                    case "vexit": ExitVehicle(p); return ActResult.OK();
                    case "vreset": return ActVehicleReset(p, a);
                    case "vload": return ActVehicleLoad(p, a);
                    case "vunload": return ActVehicleUnload(p);
                    case "vcollect": return ActVehicleCollect(p, a);
                    case "boatnet": return ActBoatNet(p);
                    case "clift": return ActCraneLift(p, a);
                    case "cdrop": return ActCraneDrop(p);
                    case "help": return ActHelp(p, a);
                    case "travel": return ActTravel(p, a, isHost);
                    case "delivery": return ActDelivery(p);
                    case "contract": return ActContract(p);
                    case "trust":
                        if (!isHost) return ActResult.Fail("Nur der Host kann das ändern.");
                        S.TrustGuests = a.Bool("v"); DW("flags"); return ActResult.OK();
                    case "cosm": return ActCosmetic(p, a);
                    case "endingSeen": S.EndingSeen = true; DW("flags"); return ActResult.OK();
                    case "introSeen": S.IntroSeen = true; DW("flags"); return ActResult.OK();
                    case "respawn": return ActRespawn(p);
                }
            }
            catch (Exception e)
            {
                return ActResult.Fail("Interner Fehler: " + e.Message);
            }
            return ActResult.Fail("Unbekannte Aktion: " + kind);
        }

        // ------------------------------------------------------------ Sammeln
        ActResult ActGrab(PlayerData p, JObj a)
        {
            if (p.Vehicle != null) return ActResult.Fail("Im Fahrzeug nicht möglich.");
            if (Since(p.Id, "grab") < 0.15f) return ActResult.Fail(null);
            var o = Rules.Obj(S.Cur, a.Str("o"));
            if (o == null) return ActResult.Fail("Schon eingesammelt.");
            if (!InReach(p, o, GameData.InteractRange)) return ActResult.Fail("Zu weit entfernt.");
            var err = Rules.CollectCheck(S, o, "grab", p.Pos, Item.Volume(p.Bin));
            if (err != null) return ActResult.Fail(err);
            Mark(p.Id, "grab");
            if (p.Energy > 0) UseEnergy(p, 0.3f);
            Collect(p, o, "grab");
            return ActResult.OK(new JObj().Set("o", o.Key));
        }

        void Collect(PlayerData p, ObjView o, string tool)
        {
            RemoveObj(o);
            p.Bin.Add(new Item { T = o.T.Id });
            CountCollected(o.T);
            DPl(p.Id);
            Fx(new JObj().Set("k", "collect").Set("o", o.Key).Set("pid", p.Id).Set("tool", tool).Set("t", o.T.Id).Set("pos", o.Pos.ToJson(1)));
        }

        ActResult ActVacuum(PlayerData p, JObj a)
        {
            if (p.Vehicle != null) return ActResult.Fail("Im Fahrzeug nicht möglich.");
            if (S.TechLevel("vacuum") <= 0) return ActResult.Fail("Kein Müllsauger vorhanden (Werkstatt).");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            float since = Since(p.Id, "vac");
            if (since < 0.18f) return ActResult.Fail(null);
            Mark(p.Id, "vac");
            float rate = S.TechVal("vacuum");
            float range = 2f + 2f * S.TechLevel("vacuum");
            int maxN = Math.Max(1, (int)Math.Round(rate * Math.Min(since, 0.5f)));
            var dir = a.Floats("dir");
            float dx = dir != null && dir.Length >= 2 ? dir[0] : M.Sin(p.Yaw), dz = dir != null && dir.Length >= 2 ? dir[1] : M.Cos(p.Yaw);
            float dl = M.Sqrt(dx * dx + dz * dz); if (dl < 0.01f) { dx = 0; dz = 1; dl = 1; }
            dx /= dl; dz /= dl;
            var cands = new List<ObjView>();
            foreach (var o in Rules.All(S.Cur))
            {
                if (!o.T.Vacuum) continue;
                float ox = o.Pos.x - p.Pos.x, oz = o.Pos.z - p.Pos.z;
                float d = M.Sqrt(ox * ox + oz * oz);
                if (d > range || Math.Abs(o.Pos.y - p.Pos.y) > 3f) continue;
                if (d > 0.8f && (ox * dx + oz * dz) / d < 0.6f) continue;
                cands.Add(o);
            }
            cands.Sort((x, y) => V3.DistXZ(x.Pos, p.Pos).CompareTo(V3.DistXZ(y.Pos, p.Pos)));
            int n = 0; string lastErr = null;
            var got = new List<object>();
            foreach (var o in cands)
            {
                if (n >= maxN) break;
                var err = Rules.CollectCheck(S, o, "vacuum", p.Pos, Item.Volume(p.Bin));
                if (err != null) { lastErr = err; if (err.StartsWith("Behälter voll")) break; continue; }
                Collect(p, o, "vacuum");
                got.Add(o.Key);
                n++;
            }
            if (n > 0) UseEnergy(p, 0.35f * n);
            if (n == 0 && lastErr != null) return ActResult.Fail(lastErr);
            return ActResult.OK(new JObj().Set("n", n).Set("got", got));
        }

        ActResult ActMagnet(PlayerData p, JObj a)
        {
            if (p.Vehicle != null) return ActResult.Fail("Im Fahrzeug nicht möglich.");
            int lvl = S.TechLevel("magnet");
            if (lvl <= 0) return ActResult.Fail("Kein Magnetarm vorhanden (Werkstatt).");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            if (Since(p.Id, "mag") < 0.6f) return ActResult.Fail("Magnet lädt noch …");
            float charge = M.Clamp(a.Float("charge", 1f), 0.2f, 1f);
            float range = S.TechVal("magnet") * (0.45f + 0.55f * charge);
            int maxN = lvl == 1 ? 6 : lvl == 2 ? 12 : 20;
            maxN = Math.Max(1, (int)Math.Round(maxN * (0.4f + 0.6f * charge)));
            var cands = new List<ObjView>();
            float fx0 = M.Sin(p.Yaw), fz0 = M.Cos(p.Yaw);
            var dir = a.Floats("dir");
            if (dir != null && dir.Length >= 2) { float l = M.Sqrt(dir[0] * dir[0] + dir[1] * dir[1]); if (l > 0.01f) { fx0 = dir[0] / l; fz0 = dir[1] / l; } }
            foreach (var o in Rules.All(S.Cur))
            {
                if (!o.T.Magnet) continue;
                float ox = o.Pos.x - p.Pos.x, oz = o.Pos.z - p.Pos.z;
                float d = M.Sqrt(ox * ox + oz * oz);
                if (d > range + Rules.ObjRadius(o.T) || Math.Abs(o.Pos.y - p.Pos.y) > 4f) continue;
                if (d > 1f && (ox * fx0 + oz * fz0) / d < -0.1f) continue; // vordere Halbkugel
                cands.Add(o);
            }
            cands.Sort((x, y) => V3.DistXZ(x.Pos, p.Pos).CompareTo(V3.DistXZ(y.Pos, p.Pos)));
            var chain = new List<object>();
            string lastErr = null;
            foreach (var o in cands)
            {
                if (chain.Count >= maxN) break;
                var err = Rules.CollectCheck(S, o, "magnet", p.Pos, Item.Volume(p.Bin));
                if (err != null) { lastErr = err; if (err.StartsWith("Behälter voll")) break; continue; }
                RemoveObj(o);
                p.Bin.Add(new Item { T = o.T.Id });
                CountCollected(o.T);
                chain.Add(Json.Arr(o.Key, o.T.Id, Json.R(o.Pos.x, 1), Json.R(o.Pos.y, 1), Json.R(o.Pos.z, 1)));
            }
            Mark(p.Id, "mag");
            UseEnergy(p, 4f + 0.4f * chain.Count);
            DPl(p.Id);
            Fx(new JObj().Set("k", "magnetwave").Set("pid", p.Id).Set("chain", chain).Set("r", Json.R(range, 1)));
            if (chain.Count == 0) return ActResult.Fail(lastErr ?? "Kein Metall in Reichweite.");
            return ActResult.OK(new JObj().Set("n", chain.Count));
        }

        ActResult ActCut(PlayerData p, JObj a)
        {
            if (p.Vehicle != null) return ActResult.Fail("Im Fahrzeug nicht möglich.");
            var o = Rules.Obj(S.Cur, a.Str("o"));
            var err = Rules.CutCheck(S, o);
            if (err != null) return ActResult.Fail(err);
            if (!InReach(p, o, GameData.InteractRange + 0.5f)) return ActResult.Fail("Zu weit entfernt.");
            if (o.Underwater && !Rules.IsDiving(S.CurrentPlanet, p.Pos)) return ActResult.Fail("Liegt unter Wasser – abtauchen.");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            float dt = ProgressDt(p.Id, "cut", a.Float("dt", 0.25f));
            string key = "cut:" + o.Key;
            float prog;
            S.Cur.Progress.TryGetValue(key, out prog);
            prog += dt * S.TechVal("cutter") / o.T.CutTime;
            UseEnergy(p, 1.5f * dt);
            if (prog < 1f)
            {
                S.Cur.Progress[key] = prog;
                return ActResult.OK(new JObj().Set("p", Json.R(prog, 3)));
            }
            S.Cur.Progress.Remove(key);
            // Zerlegen: Objekt entfernen, Teile erzeugen
            RemoveObj(o);
            var pieces = new List<object>();
            int i = 0;
            foreach (var part in o.T.CutInto)
            {
                float ang = i * 2.4f + o.Rot;
                float r = 1.2f + 0.5f * (i % 3) + Rules.ObjRadius(o.T) * 0.5f;
                var pos = new V3(o.Pos.x + M.Cos(ang) * r, o.Pos.y, o.Pos.z + M.Sin(ang) * r);
                if (!o.Underwater && pos.y > -50) pos.y = Math.Max(Terrain.HeightAt(S.CurrentPlanet, pos.x, pos.z), S.CurrentPlanet == "pelagia" && GameData.Trash[part].Floating ? 0f : -100f);
                if (S.CurrentPlanet == "pelagia" && !o.Underwater && Terrain.HeightAt(S.CurrentPlanet, pos.x, pos.z) < -0.5f) pos.y = Math.Max(pos.y, -0.3f);
                var d = SpawnDyn(part, pos, o.Area, o.Underwater, false);
                pieces.Add(d.Id);
                i++;
            }
            S.AddStat("dismantled", 1);
            DW("stats");
            Fx(new JObj().Set("k", "cut").Set("o", o.Key).Set("pos", o.Pos.ToJson(1)).Set("pieces", pieces));
            return ActResult.OK(new JObj().Set("done", true).Set("pieces", pieces));
        }

        ActResult ActThaw(PlayerData p, JObj a)
        {
            if (S.TechLevel("heat") <= 0) return ActResult.Fail("Kein Wärmemodul vorhanden (Werkstatt, ab NIVALIS).");
            var o = Rules.Obj(S.Cur, a.Str("o"));
            if (o == null) return ActResult.Fail("Nichts in Reichweite.");
            if (!o.Frozen) return ActResult.Fail("Nicht eingefroren.");
            if (!InReach(p, o, GameData.InteractRange + 0.5f)) return ActResult.Fail("Zu weit entfernt.");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            float dt = ProgressDt(p.Id, "thaw", a.Float("dt", 0.25f));
            string key = "thaw:" + o.Key;
            float prog;
            S.Cur.Progress.TryGetValue(key, out prog);
            float thawTime = 2.5f + o.T.Mass * 0.12f;
            prog += dt * S.TechVal("heat") / thawTime;
            UseEnergy(p, 2f * dt);
            if (prog < 1f) { S.Cur.Progress[key] = prog; return ActResult.OK(new JObj().Set("p", Json.R(prog, 3))); }
            S.Cur.Progress.Remove(key);
            if (o.IsStatic) { S.Cur.Thawed.Add(o.Sid); DP("thaw"); }
            else { o.D.Frozen = false; UpdateDyn(o.D); }
            S.AddStat("thawed", 1); DW("stats");
            Fx(new JObj().Set("k", "thawed").Set("o", o.Key).Set("pos", o.Pos.ToJson(1)));
            return ActResult.OK(new JObj().Set("done", true));
        }

        ActResult ActFilter(PlayerData p, JObj a)
        {
            if (S.TechLevel("filter") <= 0) return ActResult.Fail("Kein Filtermodul vorhanden (Werkstatt, ab PELAGIA).");
            var o = Rules.Obj(S.Cur, a.Str("o"));
            if (o == null || !o.T.Oil) return ActResult.Fail("Kein Ölteppich in Reichweite.");
            if (!InReach(p, o, GameData.InteractRange + 1f)) return ActResult.Fail("Zu weit entfernt.");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            if (Item.Volume(p.Bin) + o.T.Volume > S.BinCapacity) return ActResult.Fail("Behälter voll – das Altöl braucht Platz im Filtertank.");
            float dt = ProgressDt(p.Id, "filter", a.Float("dt", 0.25f));
            string key = "oil:" + o.Key;
            float prog;
            S.Cur.Progress.TryGetValue(key, out prog);
            prog += dt * S.TechVal("filter") / 4f;
            UseEnergy(p, 1.5f * dt);
            if (prog < 1f) { S.Cur.Progress[key] = prog; return ActResult.OK(new JObj().Set("p", Json.R(prog, 3))); }
            S.Cur.Progress.Remove(key);
            RemoveObj(o);
            p.Bin.Add(new Item { T = o.T.Id });
            DPl(p.Id);
            CountCollected(o.T);
            S.AddStat("oil", 1); DW("stats");
            Fx(new JObj().Set("k", "oilclean").Set("o", o.Key).Set("pos", o.Pos.ToJson(1)));
            return ActResult.OK(new JObj().Set("done", true));
        }

        ActResult ActPress(PlayerData p)
        {
            if (S.TechLevel("press") <= 0) return ActResult.Fail("Keine Müllpresse vorhanden (Werkstatt).");
            if (p.Energy <= 0.01f) return ActResult.Fail(NoEnergy);
            int n = 0;
            foreach (var it in p.Bin)
            {
                if (it.P) continue;
                var mat = GameData.Trash[it.T].MainMaterial;
                if (!GameData.Materials[mat].Pressable) continue;
                it.P = true; n++;
            }
            if (n == 0) return ActResult.Fail("Nichts Pressbares im Behälter (Papier, Kunststoff, Metall, Stahl, Kupfer, Netze).");
            UseEnergy(p, 3f);
            DPl(p.Id);
            Fx(new JObj().Set("k", "press").Set("pid", p.Id).Set("n", n));
            return ActResult.OK(new JObj().Set("n", n));
        }

        // ------------------------------------------------------------ Stützpunkt
        ActResult ActDeposit(PlayerData p)
        {
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            if (!NearStation(p, "storage", 1.5f) && V3.DistXZ(p.Pos, b.DropZone) > b.DropRadius + 1f) return ActResult.Fail("Zum Lager des Stützpunkts fahren.");
            if (p.Bin.Count == 0) return ActResult.Fail("Behälter ist leer.");
            int before = p.Bin.Count;
            int moved = AddToStorage(p.Bin, Grade.Unsorted, false);
            DPl(p.Id);
            if (moved == 0) return ActResult.Fail("Lager voll (" + S.Cur.StorageUsed() + "/" + S.Cur.StorageCap() + ") – verkaufen oder Lagerhalle bauen.");
            Fx(new JObj().Set("k", "deposit").Set("pid", p.Id).Set("n", moved));
            return ActResult.OK(new JObj().Set("n", moved).Set("left", p.Bin.Count));
        }

        ActResult ActSellBin(PlayerData p)
        {
            if (!NearStation(p, "sell", 1f)) return ActResult.Fail("Zum Verkaufsterminal fahren.");
            if (p.Bin.Count == 0) return ActResult.Fail("Behälter ist leer.");
            long total = 0; int n = 0;
            for (int i = 0; i < p.Bin.Count; i++)
            {
                var t = GameData.Trash[p.Bin[i].T];
                bool hazardous = false;
                foreach (var kv in t.Yield) if (GameData.Materials[kv.Key].Price <= 0) hazardous = true;
                if (hazardous) continue;
                foreach (var kv in t.Yield) total += Rules.SellValue(S, kv.Key, Grade.Unsorted, kv.Value);
                p.Bin.RemoveAt(i); i--; n++;
            }
            if (n == 0) return ActResult.Fail("Gefahrstoffe bitte bei der Entsorgung abgeben.");
            Earn(total);
            S.AddStat("sales", 1);
            DPl(p.Id);
            Fx(new JObj().Set("k", "sell").Set("pid", p.Id).Set("c", total));
            return ActResult.OK(new JObj().Set("credits", total).Set("n", n));
        }

        ActResult ActSell(PlayerData p, JObj a)
        {
            if (!NearStation(p, "sell", 1f)) return ActResult.Fail("Zum Verkaufsterminal fahren.");
            string mat = a.Str("m");
            MaterialDef md;
            if (mat == null || !GameData.Materials.TryGetValue(mat, out md)) return ActResult.Fail("Unbekanntes Material.");
            if (md.Price <= 0) return ActResult.Fail("Gefahrstoffe werden bei der Entsorgung abgegeben, nicht verkauft.");
            var g = (Grade)M.Clamp(a.Int("g"), 0, 2);
            int n = a.Int("n");
            var e = S.Cur.Store(mat);
            int have = g == Grade.Unsorted ? e.U : g == Grade.Sorted ? e.S : e.B;
            if (n <= 0) n = have;
            if (n <= 0 || n > have) return ActResult.Fail("Nicht genug " + md.Name + " im Lager.");
            long value = Rules.SellValue(S, mat, g, n);
            if (g == Grade.Unsorted) e.U -= n; else if (g == Grade.Sorted) e.S -= n; else e.B -= n;
            DP("storage");
            Earn(value);
            S.AddStat("sales", 1);
            if (g == Grade.Bale) S.AddStat("balesSold", n);
            Fx(new JObj().Set("k", "sell").Set("pid", p.Id).Set("c", value));
            return ActResult.OK(new JObj().Set("credits", value));
        }

        ActResult ActSortManual(PlayerData p, JObj a)
        {
            if (!NearStation(p, "sort", 1f)) return ActResult.Fail("Zum Sortiertisch fahren.");
            float dt = ProgressDt(p.Id, "sort", a.Float("dt", 0.25f));
            string key = "sortacc:" + p.Id;
            float acc;
            S.Cur.Progress.TryGetValue(key, out acc);
            acc += dt * 2.5f;
            int n = 0;
            while (acc >= 1f)
            {
                string best = null; int bu = 0;
                foreach (var kv in S.Cur.Storage)
                    if (kv.Value.U > bu && GameData.Materials[kv.Key].Price > 0) { bu = kv.Value.U; best = kv.Key; }
                if (best == null) { acc = 0; break; }
                S.Cur.Storage[best].U--; S.Cur.Storage[best].S++;
                acc -= 1f; n++;
            }
            S.Cur.Progress[key] = acc;
            if (n > 0)
            {
                S.AddStat("sorted", n); DW("stats"); DP("storage");
                Fx(new JObj().Set("k", "sort").Set("pid", p.Id).Set("n", n));
            }
            else if (acc == 0) return ActResult.Fail("Nichts Unsortiertes im Lager – zuerst Müll einlagern.");
            return ActResult.OK(new JObj().Set("n", n));
        }

        ActResult ActDispose(PlayerData p)
        {
            if (!NearStation(p, "disposal", 1f)) return ActResult.Fail("Zur Entsorgungsstation fahren.");
            long credits = 0; int units = 0;
            for (int i = 0; i < p.Bin.Count; i++)
            {
                var t = GameData.Trash[p.Bin[i].T];
                bool hazardous = false;
                foreach (var kv in t.Yield) if (GameData.Materials[kv.Key].DisposalBonus > 0) hazardous = true;
                if (!hazardous) continue;
                foreach (var kv in t.Yield)
                {
                    var md = GameData.Materials[kv.Key];
                    if (md.DisposalBonus > 0) { credits += md.DisposalBonus * kv.Value; units += kv.Value; }
                    else { var e = S.Cur.Store(kv.Key); e.U += kv.Value; DP("storage"); }
                }
                p.Bin.RemoveAt(i); i--;
            }
            foreach (var kv in S.Cur.Storage)
            {
                var md = GameData.Materials[kv.Key];
                if (md.DisposalBonus <= 0) continue;
                int n = kv.Value.U + kv.Value.S + kv.Value.B * GameData.BaleUnits;
                if (n <= 0) continue;
                credits += md.DisposalBonus * n; units += n;
                kv.Value.U = kv.Value.S = kv.Value.B = 0;
                DP("storage");
            }
            if (units == 0) return ActResult.Fail("Keine Gefahrstoffe dabei oder im Lager.");
            Earn(credits);
            S.AddStat("disposed", units); DW("stats");
            DPl(p.Id);
            Fx(new JObj().Set("k", "dispose").Set("pid", p.Id).Set("c", credits));
            return ActResult.OK(new JObj().Set("credits", credits).Set("units", units));
        }

        // ------------------------------------------------------------ Käufe
        ActResult ActBuyTech(PlayerData p, JObj a, bool isHost)
        {
            if (!InBase(p)) return ActResult.Fail("Upgrades gibt es in der Werkstatt am Stützpunkt.");
            string id = a.Str("id");
            TechDef t;
            if (id == null || !GameData.Tech.TryGetValue(id, out t)) return ActResult.Fail("Unbekanntes Upgrade.");
            int lvl = S.TechLevel(id);
            if (lvl >= t.MaxLevel) return ActResult.Fail("Bereits voll ausgebaut.");
            if (t.RequiresPlanet != null && !S.Unlocked.Contains(t.RequiresPlanet)) return ActResult.Fail("Erst verfügbar, wenn " + GameData.Planets[t.RequiresPlanet].Name + " erreichbar ist.");
            int cost = t.Levels[lvl + 1].Cost;
            if (!Allowed(isHost, cost)) return ActResult.Fail(GuestDenied);
            if (S.Credits < cost) return ActResult.Fail("Es fehlen " + (cost - S.Credits) + " Credits.");
            Spend(cost);
            S.Tech[id] = lvl + 1;
            DW("tech");
            S.AddStat("techBought", 1); DW("stats");
            if (id == "battery") foreach (var pl in S.Players.Values) { pl.Energy = Math.Min(S.MaxEnergy, pl.Energy + (S.MaxEnergy - t.Levels[lvl].Value)); DPl(pl.Id); }
            Fx(new JObj().Set("k", "upgrade").Set("id", id).Set("lvl", lvl + 1).Set("pid", p.Id));
            return ActResult.OK();
        }

        ActResult ActBuyVehicle(PlayerData p, JObj a, bool isHost)
        {
            if (!InBase(p)) return ActResult.Fail("Fahrzeuge gibt es in der Garage am Stützpunkt.");
            string id = a.Str("id");
            VehicleDef v;
            if (id == null || !GameData.Vehicles.TryGetValue(id, out v)) return ActResult.Fail("Unbekanntes Fahrzeug.");
            if (S.OwnedVehicles.Contains(id)) return ActResult.Fail("Bereits vorhanden.");
            if (v.Planet != null && !S.Unlocked.Contains(v.Planet)) return ActResult.Fail("Erst auf " + GameData.Planets[v.Planet].Name + " nutzbar.");
            if (!Allowed(isHost, v.Cost)) return ActResult.Fail(GuestDenied);
            if (S.Credits < v.Cost) return ActResult.Fail("Es fehlen " + (v.Cost - S.Credits) + " Credits.");
            Spend(v.Cost);
            S.OwnedVehicles.Add(id);
            DW("owned");
            EnsureVehicles();
            Fx(new JObj().Set("k", "vehicle").Set("id", id));
            return ActResult.OK();
        }

        ActResult ActBuyShip(PlayerData p, bool isHost)
        {
            if (!InBase(p)) return ActResult.Fail("Am Landeplatz des Stützpunkts aufrüsten.");
            int lvl = S.ShipLevel;
            if (lvl >= GameData.ShipLevelCost.Length - 1) return ActResult.Fail("Das Transportschiff ist voll ausgebaut.");
            int cost = GameData.ShipLevelCost[lvl + 1];
            if (!Allowed(isHost, cost)) return ActResult.Fail(GuestDenied);
            if (S.Credits < cost) return ActResult.Fail("Es fehlen " + (cost - S.Credits) + " Credits.");
            Spend(cost);
            S.ShipLevel = lvl + 1;
            DW("ship");
            CheckUnlocks();
            Fx(new JObj().Set("k", "ship").Set("lvl", S.ShipLevel));
            return ActResult.OK();
        }

        ActResult ActBuyMat(PlayerData p, JObj a, bool isHost)
        {
            if (!NearStation(p, "trader", 1f)) return ActResult.Fail("Zum Materialhändler fahren.");
            string mat = a.Str("m");
            MaterialDef md;
            if (mat == null || !GameData.Materials.TryGetValue(mat, out md) || !md.Buyable) return ActResult.Fail("Dieses Material wird nicht gehandelt.");
            int n = M.Clamp(a.Int("n", 10), 1, 200);
            long cost = (long)GameData.BuyPrice(mat) * n;
            if (!Allowed(isHost, cost)) return ActResult.Fail(GuestDenied);
            if (S.Credits < cost) return ActResult.Fail("Es fehlen " + (cost - S.Credits) + " Credits.");
            if (S.Cur.StorageUsed() + n > S.Cur.StorageCap()) return ActResult.Fail("Lager voll.");
            Spend(cost);
            S.Cur.Store(mat).S += n;
            DP("storage");
            Fx(new JObj().Set("k", "buymat").Set("m", mat).Set("n", n));
            return ActResult.OK();
        }

        // ------------------------------------------------------------ Bauen
        ActResult ActBuild(PlayerData p, JObj a, bool isHost)
        {
            if (!InBase(p)) return ActResult.Fail("Nur am Stützpunkt baubar.");
            string type = a.Str("t");
            BuildingDef def;
            if (type == null || !GameData.Buildings.TryGetValue(type, out def)) return ActResult.Fail("Unbekanntes Bauwerk.");
            int gx = a.Int("x"), gz = a.Int("z"), rot = M.Clamp(a.Int("r"), 0, 3);
            var err = Rules.CanPlace(S, S.Cur, type, gx, gz, rot, -1);
            if (err != null) return ActResult.Fail(err);
            if (Rules.CountOf(S.Cur, type) >= def.MaxCount) return ActResult.Fail("Maximal " + def.MaxCount + "× " + def.Name + " pro Stützpunkt.");
            if (!Allowed(isHost, def.Cost)) return ActResult.Fail(GuestDenied);
            if (S.Credits < def.Cost || !HasMaterials(def.Mats)) return ActResult.Fail("Es fehlen: " + Rules.MissingText(S.Cur, def.Mats, def.Cost, S.Credits) + ".");
            Spend(def.Cost);
            ConsumeMaterials(def.Mats);
            var b = new Building { Id = S.Cur.NextBuildingId++, Type = type, Gx = gx, Gz = gz, Rot = rot };
            S.Cur.Buildings.Add(b);
            AfterBuildChange();
            Fx(new JObj().Set("k", "build").Set("b", b.Id).Set("t", type));
            return ActResult.OK(new JObj().Set("b", b.Id));
        }

        void AfterBuildChange()
        {
            Rules.UpdateConnectivity(S.Cur);
            energyDirty = true;
            RebuildDrones();
            DP("buildings");
        }

        ActResult ActMoveBuilding(PlayerData p, JObj a)
        {
            if (!InBase(p)) return ActResult.Fail("Nur am Stützpunkt möglich.");
            int id = a.Int("b");
            var b = S.Cur.Buildings.Find(x => x.Id == id);
            if (b == null) return ActResult.Fail("Bauwerk nicht gefunden.");
            int gx = a.Int("x"), gz = a.Int("z"), rot = M.Clamp(a.Int("r"), 0, 3);
            var err = Rules.CanPlace(S, S.Cur, b.Type, gx, gz, rot, b.Id);
            if (err != null) return ActResult.Fail(err);
            b.Gx = gx; b.Gz = gz; b.Rot = rot;
            AfterBuildChange();
            Fx(new JObj().Set("k", "movebuild").Set("b", b.Id));
            return ActResult.OK();
        }

        ActResult ActDemolish(PlayerData p, JObj a, bool isHost)
        {
            if (!isHost && !S.TrustGuests) return ActResult.Fail(GuestDenied);
            if (!InBase(p)) return ActResult.Fail("Nur am Stützpunkt möglich.");
            int id = a.Int("b");
            var b = S.Cur.Buildings.Find(x => x.Id == id);
            if (b == null) return ActResult.Fail("Bauwerk nicht gefunden.");
            S.Cur.Buildings.Remove(b);
            // 50 % Rückerstattung (verhindert Gewinnschleifen)
            Earn(b.Def.Cost / 2);
            foreach (var kv in b.Def.Mats) S.Cur.Store(kv.Key).S += kv.Value / 2;
            DP("storage");
            AfterBuildChange();
            Fx(new JObj().Set("k", "demolish").Set("b", id));
            return ActResult.OK();
        }

        // ------------------------------------------------------------ Welt
        ActResult ActRepair(PlayerData p, JObj a)
        {
            var l = WorldGen.Get(S.CurrentPlanet);
            string id = a.Str("s");
            var spot = l.Repairs.Find(x => x.Id == id);
            if (spot == null) return ActResult.Fail("Unbekannter Reparaturpunkt.");
            if (S.Cur.Repaired.Contains(id)) return ActResult.Fail("Bereits repariert.");
            if (V3.DistXZ(p.Pos, spot.Pos) > 4.5f) return ActResult.Fail("Zu weit entfernt.");
            var cost = Rules.RepairCost(S.CurrentPlanet);
            if (!HasMaterials(cost)) return ActResult.Fail("Für die Reparatur fehlen im Lager: " + Rules.MissingText(S.Cur, cost, 0, 0) + ".");
            float dt = ProgressDt(p.Id, "repair", a.Float("dt", 0.25f));
            string key = "rep:" + id;
            float prog;
            S.Cur.Progress.TryGetValue(key, out prog);
            prog += dt / 2.5f;
            if (prog < 1f) { S.Cur.Progress[key] = prog; return ActResult.OK(new JObj().Set("p", Json.R(prog, 3))); }
            S.Cur.Progress.Remove(key);
            ConsumeMaterials(cost);
            S.Cur.Repaired.Add(id);
            DP("repairs");
            Earn(15);
            Fx(new JObj().Set("k", "repaired").Set("s", id).Set("pos", spot.Pos.ToJson(1)));
            return ActResult.OK(new JObj().Set("done", true));
        }

        ActResult ActPlant(PlayerData p, JObj a)
        {
            if (S.TechLevel("seeder") <= 0) return ActResult.Fail("Bio-Modul nötig (Werkstatt).");
            var l = WorldGen.Get(S.CurrentPlanet);
            string id = a.Str("s");
            var spot = l.Eco.Find(x => x.Id == id);
            if (spot == null) return ActResult.Fail("Unbekannter Pflanzplatz.");
            if (S.Cur.Eco.ContainsKey(id)) return ActResult.Fail("Hier wächst schon etwas.");
            if (V3.DistXZ(p.Pos, spot.Pos) > 4.5f) return ActResult.Fail("Zu weit entfernt.");
            if (!S.Cur.Projects[GameData.ProjectId(S.CurrentPlanet, spot.Area)].Done)
                return ActResult.Fail("Erst die Infrastruktur wiederherstellen (Projekt „" + GameData.Projects[GameData.ProjectId(S.CurrentPlanet, spot.Area)].Name + "“) – dann folgt die Begrünung.");
            const int cost = 15;
            if (!Spend(cost)) return ActResult.Fail("Es fehlen " + (cost - S.Credits) + " Credits für das Saatgut.");
            S.Cur.Eco[id] = S.PlayTime;
            DP("eco");
            Fx(new JObj().Set("k", "plant").Set("s", id).Set("pos", spot.Pos.ToJson(1)));
            return ActResult.OK();
        }

        ActResult ActLore(PlayerData p, JObj a)
        {
            var l = WorldGen.Get(S.CurrentPlanet);
            string id = a.Str("s");
            var spot = l.LoreSpots.Find(x => x.Id == id);
            if (spot == null) return ActResult.Fail("Unbekanntes Fundstück.");
            if (V3.DistXZ(p.Pos, spot.Pos) > 4.5f) return ActResult.Fail("Zu weit entfernt.");
            if (!S.Lore.Add(id)) return ActResult.Fail("Bereits gefunden.");
            DW("lore");
            int terra = 0;
            foreach (var x in S.Lore) if (GameData.Lore.ContainsKey(x) && GameData.Lore[x].Planet == "terra") terra++;
            if (terra >= 4) UnlockCosmetic("a_magenta");
            Fx(new JObj().Set("k", "lore").Set("id", id).Set("pid", p.Id));
            return ActResult.OK(new JObj().Set("id", id));
        }

        ActResult ActView(PlayerData p, JObj a)
        {
            var l = WorldGen.Get(S.CurrentPlanet);
            string id = a.Str("s");
            var spot = l.Viewpoints.Find(x => x.Id == id);
            if (spot == null) return ActResult.Fail("Unbekannter Aussichtspunkt.");
            if (V3.DistXZ(p.Pos, spot.Pos) > 7f) return ActResult.Fail("Zu weit entfernt.");
            if (S.Cur.Views.Add(id)) { DP("views"); Fx(new JObj().Set("k", "view").Set("s", id)); }
            return ActResult.OK();
        }

        ActResult ActProject(PlayerData p, JObj a, bool isHost)
        {
            int area = M.Clamp(a.Int("area"), 0, 2);
            var pid = GameData.ProjectId(S.CurrentPlanet, area);
            var pd = GameData.Projects[pid];
            var site = WorldGen.Get(S.CurrentPlanet).ProjectSites[area];
            if (V3.DistXZ(p.Pos, site) > 10f) return ActResult.Fail("Zum Projektplatz fahren.");
            var why = Rules.ProjectCheck(S, pid);
            if (why != null) return ActResult.Fail(why);
            if (!Allowed(isHost, pd.Credits)) return ActResult.Fail(GuestDenied);
            Spend(pd.Credits);
            ConsumeMaterials(pd.Mats);
            var st = S.Cur.Projects[pid];
            st.Started = true; st.Progress = 0;
            DP("projects");
            Fx(new JObj().Set("k", "projectstart").Set("project", pid).Set("name", pd.Name));
            SaveReason = "Projekt gestartet";
            return ActResult.OK();
        }

        // ------------------------------------------------------------ Fahrzeuge
        ActResult ActVehicleEnter(PlayerData p, JObj a)
        {
            string id = a.Str("v");
            VehicleState v;
            if (id == null || !S.Cur.Vehicles.TryGetValue(id, out v)) return ActResult.Fail("Fahrzeug nicht vorhanden.");
            if (p.Vehicle != null) return ActResult.Fail("Du sitzt schon in einem Fahrzeug.");
            if (v.Driver != null && v.Driver != p.Id && IsOnline(v.Driver)) return ActResult.Fail("Das Fahrzeug wird bereits gesteuert.");
            if (V3.DistXZ(p.Pos, v.Pos) > v.Def.Radius + 3.5f) return ActResult.Fail("Zu weit entfernt.");
            v.Driver = p.Id;
            p.Vehicle = id;
            p.Pos = v.Pos;
            teleportOk.Add(p.Id);
            DP("vehicles"); DPl(p.Id);
            Fx(new JObj().Set("k", "venter").Set("v", id).Set("pid", p.Id));
            return ActResult.OK();
        }

        ActResult ActVehicleReset(PlayerData p, JObj a)
        {
            string id = a.Str("v") ?? p.Vehicle;
            VehicleState v;
            if (id == null || !S.Cur.Vehicles.TryGetValue(id, out v)) return ActResult.Fail("Fahrzeug nicht vorhanden.");
            if (v.Driver != null && v.Driver != p.Id && IsOnline(v.Driver)) return ActResult.Fail("Das Fahrzeug wird gerade gesteuert.");
            // Getragenes Wrack bleibt am alten Ort (kein Teleportieren von Ladung)
            DropCarried(v, v.Pos);
            v.Pos = VehicleSpawn(id);
            v.Yaw = 0;
            if (v.Driver != null)
            {
                PlayerData d;
                if (S.Players.TryGetValue(v.Driver, out d)) { d.Pos = v.Pos; teleportOk.Add(d.Id); DPl(d.Id); }
            }
            DP("vehicles");
            Fx(new JObj().Set("k", "vreset").Set("v", id));
            return ActResult.OK(new JObj().Set("pos", v.Pos.ToJson()));
        }

        void DropCarried(VehicleState v, V3 at)
        {
            if (v.Carry == null) return;
            DynObj d;
            if (S.Cur.Dyn.TryGetValue(v.Carry, out d))
            {
                d.CarriedBy = null;
                float y = Terrain.HeightAt(S.CurrentPlanet, at.x, at.z);
                d.Pos = new V3(at.x, y, at.z);
                d.Area = PlanetLayout.AreaOf(at.z);
                UpdateDyn(d);
            }
            v.Carry = null;
            DP("vehicles");
        }

        ActResult ActVehicleLoad(PlayerData p, JObj a)
        {
            string id = a.Str("v", "rover");
            VehicleState v;
            if (!S.Cur.Vehicles.TryGetValue(id, out v) || v.Def.Capacity <= 0) return ActResult.Fail("Kein Transportfahrzeug in der Nähe.");
            if (V3.DistXZ(p.Pos, v.Pos) > v.Def.Radius + 3.5f) return ActResult.Fail("Zu weit vom Fahrzeug entfernt.");
            if (p.Bin.Count == 0) return ActResult.Fail("Behälter ist leer.");
            int n = 0;
            for (int i = 0; i < p.Bin.Count; i++)
            {
                if (Item.Volume(v.Cargo) + p.Bin[i].Vol > v.Def.Capacity) continue;
                v.Cargo.Add(p.Bin[i]); p.Bin.RemoveAt(i); i--; n++;
            }
            if (n == 0) return ActResult.Fail("Ladefläche voll.");
            DP("vehicles"); DPl(p.Id);
            Fx(new JObj().Set("k", "vload").Set("v", id).Set("n", n));
            return ActResult.OK(new JObj().Set("n", n));
        }

        ActResult ActVehicleUnload(PlayerData p)
        {
            VehicleState v;
            if (p.Vehicle == null || !S.Cur.Vehicles.TryGetValue(p.Vehicle, out v)) return ActResult.Fail("Du sitzt in keinem Fahrzeug.");
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            bool atBase = V3.DistXZ(v.Pos, b.DropZone) <= b.DropRadius + 5f || V3.DistXZ(v.Pos, b.Stations["storage"]) < 10f
                          || (v.Id == "boat" && V3.DistXZ(v.Pos, b.BoatSpot) < 14f);
            if (!atBase) return ActResult.Fail("Zum Abladeplatz am Stützpunkt fahren.");
            int moved = 0; long wreckUnits = 0;
            if (v.Carry != null)
            {
                DynObj d;
                if (S.Cur.Dyn.TryGetValue(v.Carry, out d))
                {
                    var items = new List<Item> { new Item { T = d.Type } };
                    if (AddToStorage(items, Grade.Unsorted, true) > 0)
                    {
                        wreckUnits = d.Def.TotalUnits;
                        S.Cur.Dyn.Remove(d.Id); dynRemovals.Add(d.Id); dynUpserts.Remove(d.Id);
                        CountCollected(d.Def);
                        Fx(new JObj().Set("k", "wreckdone").Set("t", d.Type).Set("units", wreckUnits));
                    }
                }
                v.Carry = null;
            }
            if (v.Cargo.Count > 0) moved = AddToStorage(v.Cargo, Grade.Unsorted, false);
            DP("vehicles");
            if (moved == 0 && wreckUnits == 0) return ActResult.Fail(v.Cargo.Count > 0 ? "Lager voll – verkaufen oder Lagerhalle bauen." : "Nichts geladen.");
            Fx(new JObj().Set("k", "deposit").Set("pid", p.Id).Set("n", moved));
            return ActResult.OK(new JObj().Set("n", moved).Set("wreck", wreckUnits));
        }

        ActResult ActVehicleCollect(PlayerData p, JObj a)
        {
            VehicleState v;
            if (p.Vehicle == null || !S.Cur.Vehicles.TryGetValue(p.Vehicle, out v) || v.Id != "rover") return ActResult.Fail("Nur mit dem Transportrover.");
            if (Since(p.Id, "vcol") < 0.2f) return ActResult.Fail(null);
            Mark(p.Id, "vcol");
            float fx0 = M.Sin(v.Yaw), fz0 = M.Cos(v.Yaw);
            var front = new V3(v.Pos.x + fx0 * 2.2f, v.Pos.y, v.Pos.z + fz0 * 2.2f);
            int n = 0; string lastErr = null;
            var got = new List<object>();
            foreach (var o in new List<ObjView>(Rules.All(S.Cur)))
            {
                if (n >= 2) break;
                if (V3.DistXZ(o.Pos, front) > 3.2f + Rules.ObjRadius(o.T)) continue;
                var t = o.T;
                if (t.Crane || t.Oil || o.Frozen || o.Underwater || o.Gate >= 0) continue;
                if (!(t.Grab || t.Vacuum) || t.Mass > 6f) { lastErr = t.Name + " passt nicht in den Ansaugschacht."; continue; }
                if (t.Hazard > S.TechVal("hazard")) { lastErr = "Gefahrgutbehälter nötig."; continue; }
                if (Item.Volume(v.Cargo) + Math.Max(0.25f, t.Volume) > v.Def.Capacity) { lastErr = "Ladefläche voll – zum Stützpunkt fahren."; break; }
                RemoveObj(o);
                v.Cargo.Add(new Item { T = t.Id });
                CountCollected(t);
                got.Add(o.Key);
                Fx(new JObj().Set("k", "collect").Set("o", o.Key).Set("pid", p.Id).Set("tool", "rover").Set("t", t.Id).Set("pos", o.Pos.ToJson(1)));
                n++;
            }
            if (n > 0) DP("vehicles");
            if (n == 0 && lastErr != null) return ActResult.Fail(lastErr);
            return ActResult.OK(new JObj().Set("n", n).Set("got", got));
        }

        ActResult ActBoatNet(PlayerData p)
        {
            VehicleState v;
            if (p.Vehicle == null || !S.Cur.Vehicles.TryGetValue(p.Vehicle, out v) || v.Id != "boat") return ActResult.Fail("Nur mit dem Sammelboot.");
            if (Since(p.Id, "boat") < 0.2f) return ActResult.Fail(null);
            Mark(p.Id, "boat");
            int n = 0;
            foreach (var o in new List<ObjView>(Rules.All(S.Cur)))
            {
                if (n >= 3) break;
                if (!o.T.Floating || o.T.Oil || o.Gate >= 0) continue;
                if (V3.DistXZ(o.Pos, v.Pos) > 4.5f) continue;
                if (Item.Volume(v.Cargo) + Math.Max(0.25f, o.T.Volume) > v.Def.Capacity) return ActResult.Fail("Netz voll – im Hafen abladen.");
                RemoveObj(o);
                v.Cargo.Add(new Item { T = o.T.Id });
                CountCollected(o.T);
                Fx(new JObj().Set("k", "collect").Set("o", o.Key).Set("pid", p.Id).Set("tool", "boat").Set("t", o.T.Id).Set("pos", o.Pos.ToJson(1)));
                n++;
            }
            if (n > 0) DP("vehicles");
            return ActResult.OK(new JObj().Set("n", n));
        }

        ActResult ActHelp(PlayerData p, JObj a)
        {
            string key = a.Str("o");
            var o = Rules.Obj(S.Cur, key);
            if (o == null || !o.T.Crane) return ActResult.Fail("Hier gibt es nichts anzuheben.");
            if (V3.DistXZ(p.Pos, o.Pos) > 8f) return ActResult.Fail("Näher heranfahren.");
            Dictionary<string, double> h;
            if (!helpers.TryGetValue(key, out h)) { h = new Dictionary<string, double>(); helpers[key] = h; }
            h[p.Id] = Now;
            Fx(new JObj().Set("k", "help").Set("o", key).Set("pid", p.Id));
            return ActResult.OK();
        }

        ActResult ActCraneLift(PlayerData p, JObj a)
        {
            VehicleState v;
            if (p.Vehicle == null || !S.Cur.Vehicles.TryGetValue(p.Vehicle, out v) || v.Id != "crane") return ActResult.Fail("Dafür brauchst du das Kranfahrzeug.");
            if (v.Carry != null) return ActResult.Fail("Der Kran trägt bereits eine Last.");
            string key = a.Str("o");
            var o = Rules.Obj(S.Cur, key);
            if (o == null) return ActResult.Fail("Kein Wrack in Reichweite.");
            if (!o.T.Crane) return ActResult.Fail("Das ist zu klein für den Kran – mit dem Greifarm sammeln.");
            if (V3.DistXZ(v.Pos, o.Pos) > 9f + Rules.ObjRadius(o.T)) return ActResult.Fail("Näher an das Wrack fahren.");
            float dt = ProgressDt(p.Id, "lift", a.Float("dt", 0.25f));
            int help = 0;
            Dictionary<string, double> h;
            if (helpers.TryGetValue(key, out h))
                foreach (var kv in h)
                    if (kv.Key != p.Id && Now - kv.Value < 1.2 && IsOnline(kv.Key)) help++;
            string pk = "lift:" + key;
            float prog;
            S.Cur.Progress.TryGetValue(pk, out prog);
            float massFactor = Math.Max(1f, o.T.Mass / 90f);
            prog += dt * (1f + 0.75f * help) / (Rules.LiftTime * massFactor);
            if (prog < 1f) { S.Cur.Progress[pk] = prog; return ActResult.OK(new JObj().Set("p", Json.R(prog, 3)).Set("help", help)); }
            S.Cur.Progress.Remove(pk);
            helpers.Remove(key);
            // Statisches Wrack → dynamisch (hängt am Kran)
            DynObj d;
            if (o.IsStatic)
            {
                RemoveObj(o);
                d = SpawnDyn(o.T.Id, new V3(v.Pos.x, v.Pos.y + 3f, v.Pos.z), o.Area, false, false);
            }
            else d = o.D;
            d.CarriedBy = v.Id;
            d.Pos = new V3(v.Pos.x, v.Pos.y + 3f, v.Pos.z);
            UpdateDyn(d);
            v.Carry = d.Id;
            DP("vehicles");
            Fx(new JObj().Set("k", "lifted").Set("o", key).Set("d", d.Id).Set("help", help));
            return ActResult.OK(new JObj().Set("done", true).Set("d", d.Id));
        }

        ActResult ActCraneDrop(PlayerData p)
        {
            VehicleState v;
            if (p.Vehicle == null || !S.Cur.Vehicles.TryGetValue(p.Vehicle, out v) || v.Id != "crane") return ActResult.Fail("Dafür brauchst du das Kranfahrzeug.");
            if (v.Carry == null) return ActResult.Fail("Der Kran trägt nichts.");
            DynObj d;
            if (!S.Cur.Dyn.TryGetValue(v.Carry, out d)) { v.Carry = null; DP("vehicles"); return ActResult.Fail("Last verloren."); }
            // 1) Auf den Transportrover setzen
            VehicleState rover;
            if (S.Cur.Vehicles.TryGetValue("rover", out rover) && rover.Carry == null && V3.DistXZ(rover.Pos, v.Pos) < 11f)
            {
                d.CarriedBy = "rover";
                d.Pos = new V3(rover.Pos.x, rover.Pos.y + 2f, rover.Pos.z);
                rover.Carry = d.Id;
                v.Carry = null;
                UpdateDyn(d);
                DP("vehicles");
                Fx(new JObj().Set("k", "onrover").Set("d", d.Id));
                return ActResult.OK(new JObj().Set("to", "rover"));
            }
            // 2) Am Stützpunkt direkt verwerten
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            if (V3.DistXZ(v.Pos, b.DropZone) <= b.DropRadius + 6f)
            {
                var items = new List<Item> { new Item { T = d.Type } };
                AddToStorage(items, Grade.Unsorted, true);
                S.Cur.Dyn.Remove(d.Id); dynRemovals.Add(d.Id); dynUpserts.Remove(d.Id);
                CountCollected(d.Def);
                v.Carry = null;
                DP("vehicles");
                Fx(new JObj().Set("k", "wreckdone").Set("t", d.Type).Set("units", d.Def.TotalUnits));
                return ActResult.OK(new JObj().Set("to", "base"));
            }
            // 3) Vor dem Kran abstellen
            var at = new V3(v.Pos.x + M.Sin(v.Yaw) * 6f, 0, v.Pos.z + M.Cos(v.Yaw) * 6f);
            DropCarried(v, at);
            Fx(new JObj().Set("k", "dropped").Set("d", d.Id));
            return ActResult.OK(new JObj().Set("to", "ground"));
        }

        // ------------------------------------------------------------ Reisen, Lieferungen, Aufträge
        ActResult ActTravel(PlayerData p, JObj a, bool isHost)
        {
            if (!isHost) return ActResult.Fail("Nur der Host kann das Transportschiff starten.");
            string pl = a.Str("planet");
            if (pl == null || !GameData.Planets.ContainsKey(pl)) return ActResult.Fail("Unbekanntes Ziel.");
            if (!S.Unlocked.Contains(pl)) return ActResult.Fail(GameData.Planets[pl].UnlockHint);
            if (pl == S.CurrentPlanet) return ActResult.Fail("Du bist bereits hier.");
            if (!NearStation(p, "ship", 4f)) return ActResult.Fail("Zum Transportschiff am Landeplatz fahren.");
            foreach (var pd in S.Players.Values)
            {
                ExitVehicle(pd);
            }
            foreach (var v in S.Cur.Vehicles.Values) { DropCarried(v, v.Pos); v.Driver = null; }
            foreach (var d in Drones) if (d.Items.Count > 0) AddToStorage(d.Items, Grade.Unsorted, true);
            Drones.Clear();
            S.CurrentPlanet = pl;
            OnPlanetEnter(true);
            foreach (var pd in S.Players.Values)
            {
                pd.Pos = SpawnPos(pd.Id);
                pd.Vehicle = null;
                teleportOk.Add(pd.Id);
                DPl(pd.Id);
            }
            fullPlanet = true;
            DW("flags");
            SaveReason = "Planetenwechsel";
            EvaluateMissions();
            Fx(new JObj().Set("k", "travel").Set("planet", pl));
            return ActResult.OK();
        }

        ActResult ActDelivery(PlayerData p)
        {
            if (!InBase(p)) return ActResult.Fail("Lieferungen werden am Stützpunkt bestellt.");
            foreach (var d in S.Cur.Dyn.Values) if (d.Delivery) return ActResult.Fail("Die letzte Lieferung liegt noch am Abladeplatz.");
            var def = GameData.Planets[S.CurrentPlanet];
            var b = WorldGen.Get(S.CurrentPlanet).Base;
            int count = 14;
            for (int i = 0; i < count; i++)
            {
                string type = def.Deliveries[(int)((S.NextDyn + i * 7) % def.Deliveries.Length)];
                float ang = i * 0.449f * 6.283f / 2.8f, r = 2.5f + (i % 4) * 1.1f;
                var pos = new V3(b.DropZone.x + M.Cos(ang) * r, b.DropZone.y, b.DropZone.z + M.Sin(ang) * r * 0.8f);
                pos.y = Terrain.HeightAt(S.CurrentPlanet, pos.x, pos.z);
                SpawnDyn(type, pos, 0, false, true);
            }
            Fx(new JObj().Set("k", "delivery").Set("n", count));
            return ActResult.OK(new JObj().Set("n", count));
        }

        ActResult ActContract(PlayerData p)
        {
            if (!NearStation(p, "contracts", 1f)) return ActResult.Fail("Zur Auftragstafel fahren.");
            string mat; int n, reward;
            Rules.Contract(S.CurrentPlanet, S.Cur.ContractIdx, out mat, out n, out reward);
            var e = S.Cur.Store(mat);
            if (e.S < n) return ActResult.Fail("Es fehlen " + (n - e.S) + " sortierte Einheiten " + GameData.Materials[mat].Name + ".");
            e.S -= n;
            S.Cur.ContractIdx++;
            DP("storage"); DP("misc");
            Earn(reward);
            Fx(new JObj().Set("k", "contract").Set("c", reward));
            return ActResult.OK(new JObj().Set("credits", reward));
        }

        ActResult ActCosmetic(PlayerData p, JObj a)
        {
            string[] keys = { "color", "accent", "sticker", "attach" };
            string[] kinds = { "color", "accent", "sticker", "attach" };
            for (int i = 0; i < keys.Length; i++)
            {
                var id = a.Str(keys[i]);
                if (id == null) continue;
                CosmeticDef c;
                if (!GameData.Cosmetics.TryGetValue(id, out c) || c.Kind != kinds[i]) return ActResult.Fail("Unbekannte Kosmetik.");
                if (!c.Default && !S.CosmeticUnlocks.Contains(id) && !a.Bool("personal")) return ActResult.Fail("Noch nicht freigeschaltet: " + c.Hint);
                if (i == 0) p.Color = id; else if (i == 1) p.Accent = id; else if (i == 2) p.Sticker = id; else p.Attach = id;
            }
            DPl(p.Id);
            return ActResult.OK();
        }

        ActResult ActRespawn(PlayerData p)
        {
            ExitVehicle(p);
            p.Pos = SpawnPos(p.Id);
            teleportOk.Add(p.Id);
            DPl(p.Id);
            return ActResult.OK(new JObj().Set("pos", p.Pos.ToJson()));
        }
    }
}
