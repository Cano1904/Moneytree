using System;
using System.Collections.Generic;
using System.Linq;
using RePlanet.Core;

/// <summary>
/// Spielt die komplette Kampagne solo über die echte, serverautoritative Spiellogik.
/// Bewegung läuft mit realer Maximalgeschwindigkeit (und Umwegfaktor) durch die Simulation,
/// jede Aktion geht durch Game.Apply – genau wie bei einem echten Spieler.
/// Liefert zusätzlich Balancing-Zeiten (erster Verkauf, erstes Upgrade, Dauer je Planet).
/// </summary>
public class CampaignBot
{
    public readonly Game G;
    public WorldState S { get { return G.S; } }
    const string Pid = "bot";
    PlayerData P;
    const float Dt = 0.25f;
    const float Detour = 1.3f;
    public double FirstSale = -1, FirstUpgrade = -1, FirstVisibleChange = -1;
    public readonly Dictionary<string, double> PlanetDone = new Dictionary<string, double>();
    public readonly List<string> Log = new List<string>();
    public int Actions, Rejections;
    readonly HashSet<string> skip = new HashSet<string>();
    string lastErr;
    int stuckGuard;
    public double TimeLimit = 60 * 3600;

    public CampaignBot(WorldState w = null)
    {
        G = new Game(w ?? Game.NewWorld("Bot-Welt"));
        P = G.Join(Pid, "Bot");
    }

    double T { get { return S.PlayTime; } }
    PlanetLayout L { get { return WorldGen.Get(S.CurrentPlanet); } }
    PlanetState PS { get { return S.Cur; } }

    void Note(string s) { Log.Add(string.Format("[{0:00}:{1:00}:{2:00}] {3}", (int)(T / 3600), (int)(T / 60) % 60, (int)T % 60, s)); }

    void Step(float dt)
    {
        G.Tick(dt);
        G.SaveReason = null;
        var patch = G.BuildPatch();
        if (patch != null && patch.Arr("fx") != null)
            foreach (var f in patch.Arr("fx"))
            {
                var fo = f as JObj;
                string k = fo.Str("k");
                if ((k == "zone" || k == "awaken") && FirstVisibleChange < 0) FirstVisibleChange = T;
                if (k == "awaken" || k == "gate" || k == "unlock" || k == "areaclean" || k == "ending" || k == "mission_done") Note("FX " + k + " " + (fo.Str("name") ?? fo.Str("title") ?? fo.Str("area") ?? fo.Str("planet") ?? ""));
            }
        if (T > TimeLimit) throw new Exception("Zeitlimit überschritten – Kampagne nicht abgeschlossen. Letzter Fehler: " + lastErr);
    }

    void Wait(float seconds) { for (float t = 0; t < seconds; t += Dt) Step(Dt); }

    bool Act(JObj a)
    {
        Actions++;
        var r = G.Apply(Pid, a, true);
        if (!r.Ok) { Rejections++; lastErr = r.Err; }
        return r.Ok;
    }

    ActResult ActR(JObj a) { Actions++; var r = G.Apply(Pid, a, true); if (!r.Ok) { Rejections++; lastErr = r.Err; } return r; }

    float YFor(V3 p, V3 target)
    {
        float g = Terrain.HeightAt(S.CurrentPlanet, p.x, p.z);
        float water = Terrain.WaterLevel(S.CurrentPlanet);
        if (g < water - 0.6f)
        {
            if (V3.DistXZ(p, target) < 6f && target.y < water - 1.1f) return Math.Max(g + 0.45f, target.y);
            return water - 0.35f;
        }
        return g;
    }

    void MoveTo(V3 target, float stop)
    {
        VehicleState v = P.Vehicle != null ? PS.Vehicles[P.Vehicle] : null;
        float speed = v != null ? v.Def.Speed : Motor.WalkSpeed;
        int guard = 0;
        while (true)
        {
            float d = V3.DistXZ(P.Pos, target);
            if (d <= stop) break;
            float step = Math.Min(speed * Dt / Detour, d - stop + 0.01f);
            float dx = (target.x - P.Pos.x) / d, dz = (target.z - P.Pos.z) / d;
            var np = new V3(P.Pos.x + dx * step, 0, P.Pos.z + dz * step);
            np.y = YFor(np, target);
            if (!G.Move(Pid, np, M.Atan2(dx, dz), false, 0, "grab", Dt)) throw new Exception("Bewegung abgelehnt bei " + np);
            Step(Dt);
            if (++guard > 100000) throw new Exception("MoveTo hängt");
        }
        // Höhe am Ziel anpassen (Tauchen)
        var fin = new V3(P.Pos.x, YFor(P.Pos, target), P.Pos.z);
        if (Math.Abs(fin.y - P.Pos.y) > 0.01f) { G.Move(Pid, fin, P.Yaw, false, 0, "grab", Dt); Step(Dt); }
    }

    V3 Station(string s) { return L.Base.Stations[s]; }

    void EnsureEnergy(float min)
    {
        if (P.Energy >= min) return;
        if (P.Vehicle != null) Act(new JObj().Set("a", "vexit"));
        MoveTo(Station("charge"), 3f);
        int g = 0;
        while (P.Energy < S.MaxEnergy * 0.97f && g++ < 2000) Step(Dt);
    }

    // ------------------------------------------------------------ Wirtschaft
    Dictionary<string, int> Reserve()
    {
        var r = new Dictionary<string, int>();
        for (int a = 0; a < 3; a++)
        {
            var pid = GameData.ProjectId(S.CurrentPlanet, a);
            if (PS.Projects[pid].Started) continue;
            foreach (var kv in GameData.Projects[pid].Mats) r[kv.Key] = (r.ContainsKey(kv.Key) ? r[kv.Key] : 0) + kv.Value;
        }
        foreach (var m in new[] { "metall", "glas", "stahl", "kupfer", "elektronik", "kunststoff" }) r[m] = (r.ContainsKey(m) ? r[m] : 0) + 30;
        return r;
    }

    void Deposit()
    {
        if (P.Bin.Count == 0) return;
        if (P.Vehicle != null) Act(new JObj().Set("a", "vexit"));
        // Gefahrstoffe direkt entsorgen
        if (P.Bin.Any(i => GameData.Trash[i.T].Yield.Keys.Any(m => GameData.Materials[m].DisposalBonus > 0)))
        {
            MoveTo(Station("disposal"), 3f);
            Act(new JObj().Set("a", "dispose"));
        }
        if (P.Bin.Count == 0) return;
        if (FirstSale < 0)
        {
            // Wie ein neuer Spieler: der erste Verkauf geht direkt aus dem Behälter
            MoveTo(Station("sell"), 3f);
            if (Act(new JObj().Set("a", "sellbin"))) { FirstSale = T; Note("Erster Verkauf"); }
            return;
        }
        MoveTo(Station("storage"), 3f);
        if (!Act(new JObj().Set("a", "deposit")) && lastErr != null && lastErr.StartsWith("Lager voll"))
        {
            SellSurplus(true);
            MoveTo(Station("storage"), 3f);
            if (!Act(new JObj().Set("a", "deposit")))
            {
                MoveTo(Station("sell"), 3f);
                Act(new JObj().Set("a", "sellbin"));
            }
        }
        BaseChores();
    }

    void SellSurplus(bool aggressive)
    {
        var res = Reserve();
        bool moved = false;
        foreach (var kv in PS.Storage.ToList())
        {
            var md = GameData.Materials[kv.Key];
            if (md.Price <= 0) continue;
            int reserve = res.ContainsKey(kv.Key) ? res[kv.Key] : 0;
            var e = kv.Value;
            int total = e.U + e.S + e.B * GameData.BaleUnits;
            if (total <= reserve && !aggressive) continue;
            if (!moved) { MoveTo(Station("sell"), 3f); moved = true; }
            if (e.B > 0 && e.U + e.S >= reserve) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 2).Set("n", e.B));
            int extra = e.U + e.S - reserve;
            if (extra > 0)
            {
                int fromS = Math.Min(e.S, extra);
                if (fromS > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 1).Set("n", fromS));
                extra -= fromS;
                if (extra > 0 && e.U >= extra) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 0).Set("n", extra));
            }
        }
    }

    void Dispose()
    {
        bool any = PS.Storage.Any(kv => GameData.Materials[kv.Key].DisposalBonus > 0 && kv.Value.U + kv.Value.S > 0);
        if (!any) return;
        MoveTo(Station("disposal"), 3f);
        Act(new JObj().Set("a", "dispose"));
    }

    // ------------------------------------------------------------ Einkaufsliste
    static readonly Dictionary<string, string[]> Shopping = new Dictionary<string, string[]>
    {
        { "terra", new[] { "t:bin", "t:vacuum", "b:sortierer", "t:bin", "t:magnet", "t:grab", "t:battery", "t:hazard", "b:presse", "t:cutter", "b:lager", "v:crane", "t:bin", "t:press", "t:vacuum" } },
        { "pyra", new[] { "t:magnet", "t:hazard", "b:sortierer", "b:presse", "b:lager", "t:grab", "t:efficiency", "t:battery", "t:bin", "b:lager" } },
        { "pelagia", new[] { "t:dive", "t:filter", "b:sortierer", "b:presse", "b:lager", "b:lager", "t:cutter" } },
        { "nivalis", new[] { "t:heat", "t:insulation", "b:sortierer", "b:presse", "b:lager", "b:lager", "t:heat" } },
    };

    string NextPurchase(out int cost)
    {
        cost = 0;
        var counts = new Dictionary<string, int>();
        foreach (var item in Shopping[S.CurrentPlanet])
        {
            counts[item] = (counts.ContainsKey(item) ? counts[item] : 0) + 1;
            var kind = item.Substring(0, 1); var id = item.Substring(2);
            if (kind == "t")
            {
                int want = counts[item] + (S.CurrentPlanet == "terra" ? 0 : BaseLevel(id));
                if (S.TechLevel(id) >= Math.Min(want, GameData.Tech[id].MaxLevel)) continue;
                cost = GameData.Tech[id].Levels[S.TechLevel(id) + 1].Cost;
                return item;
            }
            if (kind == "v")
            {
                if (S.OwnedVehicles.Contains(id)) continue;
                cost = GameData.Vehicles[id].Cost; return item;
            }
            if (kind == "b")
            {
                if (Rules.CountOf(PS, id) >= counts[item]) continue;
                cost = GameData.Buildings[id].Cost; return item;
            }
        }
        return null;
    }

    readonly Dictionary<string, int> levelsAtArrival = new Dictionary<string, int>();
    int BaseLevel(string id) { int v; return levelsAtArrival.TryGetValue(S.CurrentPlanet + id, out v) ? v : 0; }

    void TryBuy()
    {
        for (int i = 0; i < 6; i++)
        {
            int cost;
            var item = NextPurchase(out cost);
            if (item == null || S.Credits < cost) return;
            var kind = item.Substring(0, 1); var id = item.Substring(2);
            bool ok = false;
            if (kind == "t") { MoveTo(Station("workshop"), 3f); ok = Act(new JObj().Set("a", "buytech").Set("id", id)); if (ok && FirstUpgrade < 0) { FirstUpgrade = T; Note("Erstes Upgrade: " + id); } }
            else if (kind == "v") { MoveTo(Station("workshop"), 3f); ok = Act(new JObj().Set("a", "buyveh").Set("id", id)); }
            else
            {
                var def = GameData.Buildings[id];
                if (def.Mats.Any(kv => PS.Available(kv.Key) < kv.Value)) return;
                ok = PlaceBuilding(id);
            }
            if (ok) { skip.Clear(); Note("Gekauft: " + item + " (Credits " + S.Credits + ")"); }
            else return;
        }
    }

    bool PlaceBuilding(string type)
    {
        MoveTo(Station("build"), 3f);
        var bl = L.Base;
        for (int gz = 0; gz < bl.GridH; gz++)
            for (int gx = 0; gx < bl.GridW; gx++)
                if (Rules.CanPlace(S, PS, type, gx, gz, 0, -1) == null)
                    return Act(new JObj().Set("a", "build").Set("t", type).Set("x", gx).Set("z", gz).Set("r", 0));
        return false;
    }

    void BaseChores()
    {
        Dispose();
        // Manuell sortieren, solange noch keine Sortieranlage läuft
        if (Rules.CountOf(PS, "sortierer") == 0 && PS.Storage.Values.Sum(e => e.U) > 10)
        {
            MoveTo(Station("sort"), 3f);
            for (int i = 0; i < 40 && PS.Storage.Values.Sum(e => e.U) > 0; i++) { Act(new JObj().Set("a", "sortm").Set("dt", 0.25f)); Step(Dt); }
        }
        if (PS.StorageUsed() > PS.StorageCap() * 0.8f) SellSurplus(false);
        int cost;
        var next = NextPurchase(out cost);
        if (next != null && S.Credits < cost) SellSurplus(false);
        TryBuy();
    }

    // ------------------------------------------------------------ Sammeln
    string NeededTech(ObjView o)
    {
        var t = o.T;
        if (t.Crane) return S.OwnedVehicles.Contains("crane") ? null : "v:crane";
        if (t.Hazard > S.TechVal("hazard")) return "t:hazard";
        if (o.Frozen && S.TechLevel("heat") == 0) return "t:heat";
        if (o.Underwater && S.TechLevel("dive") == 0) return "t:dive";
        if (t.Oil) return S.TechLevel("filter") > 0 ? null : "t:filter";
        if (t.Grab && t.Mass <= S.TechVal("grab")) return null;
        if (t.Magnet && S.TechLevel("magnet") >= t.MinMagnet) return null;
        if (t.CutInto != null && S.TechLevel("cutter") >= t.MinCutter) return null;
        if (t.Magnet) return "t:magnet";
        if (t.CutInto != null) return "t:cutter";
        return "t:grab";
    }

    ObjView Nearest(Func<ObjView, bool> filter)
    {
        ObjView best = null; float bd = float.MaxValue;
        foreach (var o in Rules.All(PS))
        {
            if (skip.Contains(o.Key)) continue;
            if (!filter(o)) continue;
            if (NeededTech(o) != null) continue;
            float d = V3.DistXZ(o.Pos, P.Pos);
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    bool Accessible(int area) { return Rules.AreaStage(S, PS, area) >= 1; }

    void Process(ObjView o)
    {
        var t = o.T;
        if (t.Crane) { CraneHaul(o); return; }
        float need = Math.Max(0.25f, t.Volume);
        if (Item.Volume(P.Bin) + need > S.BinCapacity) Deposit();
        if (P.Energy < 15) EnsureEnergy(15);
        MoveTo(o.Pos, Math.Max(1.2f, Rules.ObjRadius(t) + 1f));
        int guard = 0;
        if (o.Frozen)
        {
            while (Rules.Obj(PS, o.Key) != null && Rules.Obj(PS, o.Key).Frozen && guard++ < 400)
            {
                if (!Act(new JObj().Set("a", "thaw").Set("o", o.Key).Set("dt", Dt))) { EnsureEnergy(40); MoveTo(o.Pos, Rules.ObjRadius(t) + 1f); }
                Step(Dt);
            }
            o = Rules.Obj(PS, o.Key);
            if (o == null) return;
        }
        if (t.Oil)
        {
            while (Rules.Obj(PS, o.Key) != null && guard++ < 400)
            {
                if (!Act(new JObj().Set("a", "filter").Set("o", o.Key).Set("dt", Dt)))
                {
                    if (lastErr != null && lastErr.StartsWith("Behälter voll")) { Deposit(); MoveTo(o.Pos, 2f); }
                    else if (lastErr == Game_NoEnergy()) { EnsureEnergy(40); MoveTo(o.Pos, 2f); }
                    else { skip.Add(o.Key); return; }
                }
                Step(Dt);
            }
            return;
        }
        string err = Rules.CollectCheck(S, o, "grab", P.Pos, Item.Volume(P.Bin));
        if (err == null) { if (!Act(new JObj().Set("a", "grab").Set("o", o.Key))) skip.Add(o.Key); Step(0.5f); return; }
        if (t.Magnet && S.TechLevel("magnet") >= t.MinMagnet)
        {
            var dir = new List<object> { o.Pos.x - P.Pos.x, o.Pos.z - P.Pos.z };
            if (!Act(new JObj().Set("a", "magnet").Set("charge", 1f).Set("dir", dir)))
            {
                if (lastErr != null && lastErr.StartsWith("Behälter voll")) Deposit();
                else if (lastErr != null && lastErr.StartsWith("Akku")) EnsureEnergy(50);
                else if (lastErr != null && lastErr.StartsWith("Magnet lädt")) Step(0.6f);
                else skip.Add(o.Key);
            }
            Step(1.0f);
            return;
        }
        if (t.CutInto != null && S.TechLevel("cutter") >= t.MinCutter)
        {
            while (Rules.Obj(PS, o.Key) != null && guard++ < 600)
            {
                if (!Act(new JObj().Set("a", "cut").Set("o", o.Key).Set("dt", Dt)))
                {
                    if (lastErr != null && lastErr.StartsWith("Akku")) { EnsureEnergy(50); MoveTo(o.Pos, Rules.ObjRadius(t) + 1f); }
                    else { skip.Add(o.Key); return; }
                }
                Step(Dt);
            }
            return;
        }
        skip.Add(o.Key);
    }

    static string Game_NoEnergy() { return "Akku leer – Notbetrieb. Fahre zum Ladeplatz am Stützpunkt (nur der Greifarm funktioniert)."; }

    void CraneHaul(ObjView o)
    {
        Deposit();
        var crane = PS.Vehicles["crane"];
        MoveTo(crane.Pos, 2f);
        if (!Act(new JObj().Set("a", "venter").Set("v", "crane"))) throw new Exception("Kran: " + lastErr);
        MoveTo(o.Pos, 7f);
        int guard = 0;
        while (crane.Carry == null && guard++ < 400)
        {
            if (!Act(new JObj().Set("a", "clift").Set("o", o.Key).Set("dt", Dt))) throw new Exception("Anheben: " + lastErr);
            Step(Dt);
        }
        MoveTo(L.Base.DropZone, 4f);
        if (!Act(new JObj().Set("a", "cdrop"))) throw new Exception("Absetzen: " + lastErr);
        Act(new JObj().Set("a", "vexit"));
        Note("Wrack mit dem Kran geborgen: " + o.T.Name);
    }

    /// <summary>Beschafft Geld über ausdrücklich bestellte Schrottlieferungen, falls der Müll im Zugang aufgebraucht ist.</summary>
    void EarnViaDelivery()
    {
        MoveTo(Station("contracts"), 3f);
        if (!Act(new JObj().Set("a", "delivery")) && !PS.Dyn.Values.Any(d => d.Delivery)) throw new Exception("Lieferung: " + lastErr);
        int guard = 0;
        while (PS.Dyn.Values.Any(d => d.Delivery && d.CarriedBy == null) && guard++ < 200)
        {
            var o = Nearest(x => x.D != null && x.D.Delivery);
            if (o == null) break;
            Process(o);
        }
        Deposit();
        SellSurplus(false);
    }

    // ------------------------------------------------------------ Hauptschleife
    public void Run()
    {
        Note("Start auf TERRA");
        while (!S.CampaignDone)
        {
            if (++stuckGuard > 20000) throw new Exception("Bot steckt fest. Letzter Fehler: " + lastErr);
            var planet = S.CurrentPlanet;
            int area = -1;
            for (int a = 0; a < 3; a++) if (!PS.Projects[GameData.ProjectId(planet, a)].Done) { area = a; break; }
            if (area < 0) { NextPlanet(); continue; }
            int stage = Rules.AreaStage(S, PS, area);
            if (stage == 0) { ClearGate(area - 1); continue; }
            if (stage == 1) { CollectRound(area); continue; }
            var pid = GameData.ProjectId(planet, area);
            var st = PS.Projects[pid];
            if (st.Started) { Wait(2f); continue; }
            StartProject(area);
        }
        Note("Kampagne abgeschlossen");
    }

    void CollectRound(int area)
    {
        // Behälter füllen: bevorzugt im Zielbereich, sonst in allen zugänglichen Bereichen
        var o = Nearest(x => x.Area == area && x.Gate < 0 && x.IsStatic) ?? Nearest(x => x.Gate < 0 && Accessible(x.Area));
        if (o != null) { Process(o); return; }
        // Nichts Sammelbares mehr: fehlende Technik besorgen
        var need = new Dictionary<string, int>();
        foreach (var x in Rules.All(PS))
        {
            if (x.Area != area || x.Gate >= 0) continue;
            var n = NeededTech(x);
            if (n != null) need[n] = (need.ContainsKey(n) ? need[n] : 0) + 1;
        }
        skip.Clear();
        if (need.Count == 0) { Deposit(); Wait(1f); return; }
        var want = need.OrderByDescending(kv => kv.Value).First().Key;
        Acquire(want);
    }

    void Acquire(string item)
    {
        var kind = item.Substring(0, 1); var id = item.Substring(2);
        int cost = kind == "t" ? GameData.Tech[id].Levels[Math.Min(S.TechLevel(id) + 1, GameData.Tech[id].MaxLevel)].Cost : GameData.Vehicles[id].Cost;
        Deposit();
        int guard = 0;
        while (S.Credits < cost && guard++ < 60)
        {
            SellSurplus(false);
            if (S.Credits >= cost) break;
            EarnViaDelivery();
        }
        MoveTo(Station("workshop"), 3f);
        bool ok = kind == "t" ? Act(new JObj().Set("a", "buytech").Set("id", id)) : Act(new JObj().Set("a", "buyveh").Set("id", id));
        if (!ok) throw new Exception("Kauf " + item + " fehlgeschlagen: " + lastErr);
        if (FirstUpgrade < 0 && kind == "t") FirstUpgrade = T;
        skip.Clear();
        Note("Beschafft (benötigt): " + item);
    }

    void ClearGate(int g)
    {
        var gate = L.Gates[g];
        foreach (var id in gate.Objects)
        {
            var o = Rules.Obj(PS, "s" + id);
            if (o == null) continue;
            var need = NeededTech(o);
            if (need != null) { Acquire(need); return; }
            // Tor-Objekte liegen im Durchgang; der Bot nähert sich von der zugänglichen Seite
            Process(o);
            return;
        }
    }

    void StartProject(int area)
    {
        var pid = GameData.ProjectId(S.CurrentPlanet, area);
        var pd = GameData.Projects[pid];
        int guard = 0;
        while (true)
        {
            if (guard++ > 400) throw new Exception("Projekt " + pid + " nicht startbar: " + Rules.ProjectCheck(S, pid));
            var why = Rules.ProjectCheck(S, pid);
            if (why == null) break;
            // Material fehlt → weiter sammeln (auch andere Bereiche), notfalls kaufen
            var missingMat = pd.Mats.FirstOrDefault(kv => PS.Available(kv.Key) < kv.Value);
            if (missingMat.Key != null)
            {
                var o = Nearest(x => x.Gate < 0 && Accessible(x.Area) && x.T.Yield.ContainsKey(missingMat.Key));
                if (o != null) { Process(o); if (P.Bin.Count > 0 && Item.Volume(P.Bin) > S.BinCapacity * 0.7f) Deposit(); continue; }
                Deposit();
                if (PS.Available(missingMat.Key) >= missingMat.Value) continue;
                int n = missingMat.Value - PS.Available(missingMat.Key);
                long cost = (long)GameData.BuyPrice(missingMat.Key) * n;
                while (S.Credits < cost + pd.Credits) { SellSurplus(false); if (S.Credits >= cost + pd.Credits) break; EarnViaDelivery(); }
                MoveTo(Station("trader"), 3f);
                if (!Act(new JObj().Set("a", "buymat").Set("m", missingMat.Key).Set("n", n))) throw new Exception("Materialkauf: " + lastErr);
                Note("Material gekauft: " + n + " " + missingMat.Key);
                continue;
            }
            if (S.Credits < pd.Credits)
            {
                Deposit();
                SellSurplus(false);
                if (S.Credits < pd.Credits)
                {
                    var o = Nearest(x => x.Gate < 0 && Accessible(x.Area));
                    if (o != null) Process(o); else EarnViaDelivery();
                }
                continue;
            }
            throw new Exception("Projekt " + pid + ": " + why);
        }
        MoveTo(L.ProjectSites[area], 6f);
        if (!Act(new JObj().Set("a", "project").Set("area", area))) throw new Exception("Projektstart: " + lastErr);
        Note("Projekt gestartet: " + pd.Name);
    }

    void NextPlanet()
    {
        PlanetDone[S.CurrentPlanet] = T;
        int idx = GameData.PlanetOrder.IndexOf(S.CurrentPlanet);
        if (idx + 1 >= GameData.PlanetOrder.Count) { Wait(1); return; }
        var next = GameData.PlanetOrder[idx + 1];
        var pd = GameData.Planets[next];
        Deposit();
        while (S.ShipLevel < pd.ShipLevelRequired)
        {
            int cost = GameData.ShipLevelCost[S.ShipLevel + 1];
            int guard = 0;
            while (S.Credits < cost && guard++ < 200)
            {
                SellSurplus(true);
                if (S.Credits >= cost) break;
                var o = Nearest(x => x.Gate < 0 && Accessible(x.Area));
                if (o != null) { Process(o); if (Item.Volume(P.Bin) > S.BinCapacity * 0.7f) Deposit(); }
                else EarnViaDelivery();
            }
            MoveTo(Station("ship"), 3f);
            if (!Act(new JObj().Set("a", "buyship"))) throw new Exception("Schiff: " + lastErr);
            Note("Transportschiff Stufe " + S.ShipLevel);
        }
        if (!S.Unlocked.Contains(next)) throw new Exception(next + " nicht freigeschaltet");
        SellSurplus(true);
        MoveTo(Station("ship"), 3f);
        if (!Act(new JObj().Set("a", "travel").Set("planet", next))) throw new Exception("Reise: " + lastErr);
        foreach (var t in GameData.TechOrder) levelsAtArrival[next + t] = S.TechLevel(t);
        skip.Clear();
        Note("Reise nach " + pd.Name);
    }
}
