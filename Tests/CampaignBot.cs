using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RePlanet.Core;

/// <summary>
/// Spielt die komplette Kampagne solo über die echte, serverautoritative Spiellogik:
/// TERRA → PYRA → PELAGIA (freie Reise) → Sprungantrieb → NIVALIS.
/// Bewegung läuft mit realer Maximalgeschwindigkeit (und Umwegfaktor) durch Game.Move, jede Aktion geht durch
/// Game.Apply, die Zeit durch Game.Tick – genau wie bei einem echten Spieler. Nachts und bei Stürmen sucht der Bot
/// einen Unterschlupf (oder baut einen Notunterschlupf) und schläft; Notabschaltungen werden gezählt.
/// Liefert Balancing-Messwerte (erster Verkauf, erstes Upgrade, erste sichtbare Veränderung, Dauer je Planet …).
/// </summary>
public class CampaignBot
{
    public readonly Game G;
    public WorldState S { get { return G.S; } }
    const string Pid = "bot";
    readonly PlayerData P;
    const float Dt = 0.25f;

    // ------------------------------------------------------------ Verhaltensprofil
    /// <summary>Anteil der Höchstgeschwindigkeit, der im Mittel erreicht wird.</summary>
    public float SpeedFactor = 1f;
    /// <summary>Umwegfaktor gegenüber der Luftlinie (Straßen, Hindernisse).</summary>
    public float Detour = 1.3f;
    /// <summary>Zusätzliche Sekunden je Sammelaktion (Zielen, Anfahren).</summary>
    public float PickupDelay;
    /// <summary>Zusätzliche Sekunden je Stationsbesuch (Menüs lesen, entscheiden).</summary>
    public float StationDelay;
    public string ProfileName = "Bot (optimal)";

    /// <summary>Annahmen für einen zügig spielenden Menschen (Schätzung, keine Messung).</summary>
    public static CampaignBot Human()
    {
        return new CampaignBot { SpeedFactor = 0.8f, Detour = 1.5f, PickupDelay = 2f, StationDelay = 6f, ProfileName = "Mensch-Modell (geschätzt)" };
    }

    // ------------------------------------------------------------ Messwerte
    public double FirstSale = -1, FirstUpgrade = -1, FirstVisibleChange = -1, FirstAreaClean = -1;
    public string FirstUpgradeId, FirstVisibleWhat;
    public readonly Dictionary<string, double> PlanetStart = new Dictionary<string, double>();
    public readonly Dictionary<string, double> PlanetDone = new Dictionary<string, double>();
    public readonly List<string> Log = new List<string>();
    public readonly List<string> ProgressLines = new List<string>();
    /// <summary>Credits-Verlauf: Spielzeit (s), Kontostand, insgesamt verdient.</summary>
    public readonly List<double[]> CreditHistory = new List<double[]>();
    public int Actions, Rejections, SheltersBuilt, Sleeps, StormSleeps, ShelterTrips, Deliveries, CraneHauls, Travels;
    /// <summary>TNT: Müllberge sprengen, wo es sich lohnt (früh in einem Bereich, Berg in der Nähe, genug Geld übrig).</summary>
    public bool UseTnt = true;
    public int TntThrows, TntBlasts, TntPieces;
    public long TntSpent;
    readonly HashSet<string> blastSkip = new HashSet<string>();
    /// <summary>Nächte/Stürme im Hangar bzw. im Laderaum des Transportschiffs verbracht.</summary>
    public int HangarSleeps, ShipSleeps;
    public long DeliveryCredits;
    /// <summary>Spielzeit, die der Bot auf die Abklingzeit einer Lieferung gewartet hat, bzw. im Unterschlupf einen Sturm abgewartet hat (s).</summary>
    public double DeliveryWaitTime, StormWaitTime;
    /// <summary>Gezahlte Liefergebühren.</summary>
    public long DeliveryFees;
    public int Shutdowns { get { return (int)S.Stat("shutdowns"); } }
    public double TimeLimit = 60 * 3600;     // Spielzeit
    public double RealTimeLimit = 600;       // Sekunden Echtzeit
    public bool Echo = true;                 // Fortschritt auf die Konsole
    public readonly Stopwatch Real = new Stopwatch();
    public readonly Stopwatch SwTick = new Stopwatch(), SwPatch = new Stopwatch(), SwSearch = new Stopwatch(), SwAct = new Stopwatch();
    public long Steps;

    readonly HashSet<string> skip = new HashSet<string>();
    string lastErr;
    double nextReport = 600;
    int stepsSincePatch;
    bool sheltering;
    long saveFor; // Sparziel: Einkaufsliste kauft nur, was darüber hinaus übrig ist

    public CampaignBot(WorldState w = null)
    {
        G = new Game(w ?? Game.NewWorld("Bot-Welt"));
        P = G.Join(Pid, "Bot");
    }

    double T { get { return S.PlayTime; } }
    PlanetLayout L { get { return WorldGen.Get(S.CurrentPlanet); } }
    PlanetState PS { get { return S.Cur; } }

    public static string Clock(double t) { return string.Format("{0:00}:{1:00}:{2:00}", (int)(t / 3600), (int)(t / 60) % 60, (int)t % 60); }
    void Note(string s) { Log.Add("[" + Clock(T) + "] " + s); }

    // ------------------------------------------------------------ Zeit
    void Step(float dt)
    {
        Steps++;
        SwTick.Start(); G.Tick(dt); SwTick.Stop();
        // Patches wie im Spiel regelmäßig einsammeln (hier nur für Ereignisse/Messungen)
        if (++stepsSincePatch >= 4)
        {
            stepsSincePatch = 0;
            G.SaveReason = null;
            SwPatch.Start();
            var patch = G.BuildPatch();
            SwPatch.Stop();
            if (patch != null && patch.Arr("fx") != null)
                foreach (var f in patch.Arr("fx")) OnFx((JObj)f);
        }
        if (T >= nextReport) Report();
        if (T > TimeLimit) throw new Exception("Spielzeitlimit überschritten – Kampagne nicht abgeschlossen. Letzter Fehler: " + lastErr);
        if (Real.Elapsed.TotalSeconds > RealTimeLimit) throw new Exception("Echtzeitlimit (" + RealTimeLimit + " s) überschritten bei Spielzeit " + Clock(T) + ". Letzter Fehler: " + lastErr);
    }

    void OnFx(JObj fo)
    {
        string k = fo.Str("k");
        if ((k == "zone" || k == "areaclean" || k == "awaken") && FirstVisibleChange < 0) { FirstVisibleChange = T; FirstVisibleWhat = k + " " + (fo.Str("name") ?? ""); }
        if (k == "areaclean" && FirstAreaClean < 0) FirstAreaClean = T;
        if (k == "awaken" || k == "gate" || k == "unlock" || k == "areaclean" || k == "ending" || k == "mission_done" || k == "shutdown")
            Note("Ereignis " + k + " " + (fo.Str("name") ?? fo.Str("title") ?? fo.Str("planet") ?? ""));
    }

    void Report()
    {
        nextReport += 600;
        CreditHistory.Add(new[] { T, S.Credits, S.Stat("credEarned") });
        int area = CurrentArea();
        string stage = area < 0 ? "fertig" : "Bereich " + area + " Stufe " + Rules.AreaStage(S, PS, area) + " (" + (Rules.Cleanliness(PS, area) * 100).ToString("0") + " %)";
        var line = string.Format("[{0}] {1,-8} {2,-28} Credits {3,6} | gesammelt {4,5} | Notabschaltungen {5} | Schlaf {6} | Notunterschlüpfe {7} | Echtzeit {8:0.0} s",
            Clock(T).Substring(0, 5), GameData.Planets[S.CurrentPlanet].Name, stage, S.Credits, S.Stat("collected"), Shutdowns, Sleeps, SheltersBuilt, Real.Elapsed.TotalSeconds);
        ProgressLines.Add(line);
        if (Echo) Console.WriteLine(line);
    }

    void Wait(float seconds) { for (float t = 0; t < seconds; t += Dt) Step(Dt); }

    void WaitTow()
    {
        int g = 0;
        while (P.TowTimer > 0 && g++ < 400) Step(Dt);
    }

    bool Act(JObj a) { return ActR(a).Ok; }

    static readonly HashSet<string> PickupActs = new HashSet<string> { "grab", "vacuum", "magnet" };
    static readonly HashSet<string> StationActs = new HashSet<string> { "sell", "sellbin", "deposit", "dispose", "buytech", "buyveh", "buyship", "buymat", "build", "delivery", "project", "travel", "contract", "shelter", "sortm" };
    string lastStation; double lastStationT = -100;

    ActResult ActR(JObj a)
    {
        Actions++;
        SwAct.Start();
        var r = G.Apply(Pid, a, true);
        SwAct.Stop();
        if (!r.Ok) { Rejections++; lastErr = r.Err; }
        string kind = a.Str("a");
        if (r.Ok && PickupDelay > 0 && PickupActs.Contains(kind)) Wait(PickupDelay);
        if (StationDelay > 0 && StationActs.Contains(kind) && (kind != lastStation || T - lastStationT > 15))
        {
            lastStation = kind;
            Wait(StationDelay);
            lastStationT = T;
        }
        return r;
    }

    // ------------------------------------------------------------ Bewegung
    float YFor(V3 p, V3 target)
    {
        float g = L.GroundAt(p.x, p.z); // Gelände bzw. Schiffsrampe/Laderaumboden
        float water = Terrain.WaterLevel(S.CurrentPlanet);
        if (g < water - 0.6f)
        {
            if (V3.DistXZ(p, target) < 6f && target.y < water - 1.1f) return Math.Max(g + 0.45f, target.y);
            return water - 0.35f;
        }
        return g;
    }

    /// <summary>Fährt in Schritten zum Ziel. Nachts/bei Sturm wird unterwegs ein Unterschlupf aufgesucht (außer im Fahrzeug).</summary>
    void MoveTo(V3 target, float stop, bool shelterCheck = true)
    {
        if (Environment.GetEnvironmentVariable("BOTTRACE") != null && V3.DistXZ(P.Pos, target) > 30) Console.WriteLine("  M " + Clock(T) + " " + P.Pos + " -> " + target + " " + new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name);
        int guard = 0;
        while (true)
        {
            if (P.TowTimer > 0) WaitTow();
            if (shelterCheck && !sheltering && P.Vehicle == null && Danger()) Shelter();
            VehicleState v = P.Vehicle != null ? PS.Vehicles[P.Vehicle] : null;
            float speed = v != null ? v.Def.Speed : Motor.WalkSpeed;
            float d = V3.DistXZ(P.Pos, target);
            if (d <= stop) break;
            float step = Math.Min(speed * SpeedFactor * Dt / Detour, d - stop + 0.01f);
            float dx = (target.x - P.Pos.x) / d, dz = (target.z - P.Pos.z) / d;
            var np = new V3(P.Pos.x + dx * step, 0, P.Pos.z + dz * step);
            np.y = YFor(np, target);
            if (!G.Move(Pid, np, M.Atan2(dx, dz), false, 0, "grab", Dt))
            {
                if (P.TowTimer > 0) continue;
                throw new Exception("Bewegung abgelehnt bei " + np);
            }
            Step(Dt);
            if (++guard > 100000) throw new Exception("MoveTo hängt");
        }
        // Höhe am Ziel anpassen (Tauchen)
        var fin = new V3(P.Pos.x, YFor(P.Pos, target), P.Pos.z);
        if (Math.Abs(fin.y - P.Pos.y) > 0.01f) { G.Move(Pid, fin, P.Yaw, false, 0, "grab", Dt); Step(Dt); }
    }

    V3 Station(string s) { return L.Base.Stations[s]; }

    void ExitVehicle() { if (P.Vehicle != null) Act(new JObj().Set("a", "vexit")); }

    void EnsureEnergy(float min)
    {
        if (P.Energy >= min) return;
        ExitVehicle();
        MoveTo(Station("charge"), 3f);
        int g = 0;
        while (P.Energy < S.MaxEnergy * 0.97f && g++ < 2000) Step(Dt);
    }

    // ------------------------------------------------------------ Nacht und Sturm
    bool Danger() { return Rules.IsNight(S, S.CurrentPlanet) || PS.StormActive || PS.StormWarn; }

    bool AreaAccessible(int area) { for (int g = 0; g < area; g++) if (!Rules.GateOpen(PS, g)) return false; return true; }

    V3 NearestShelter(out float dist)
    {
        var at = Station("storage");
        dist = V3.DistXZ(P.Pos, at);
        foreach (var sh in L.Shelters)
        {
            if (!AreaAccessible(PlanetLayout.AreaOf(sh.Pos.z))) continue;
            float d = V3.DistXZ(P.Pos, sh.Pos);
            if (d < dist) { dist = d; at = sh.Pos; }
        }
        foreach (var sh in PS.Shelters) { float d = V3.DistXZ(P.Pos, sh); if (d < dist) { dist = d; at = sh; } }
        return at;
    }

    /// <summary>Unterschlupf aufsuchen (oder Notunterschlupf bauen) und schlafen, bis Nacht/Sturm vorbei sind.</summary>
    void Shelter()
    {
        if (P.Vehicle != null) return;
        sheltering = true;
        ShelterTrips++;
        try
        {
            int guard = 0;
            while (Danger() && guard++ < 200)
            {
                if (P.TowTimer > 0) { WaitTow(); continue; }
                int kind = Rules.ShelterKind(S, PS, P.Pos);
                // Am Stützpunkt: hinein in den Hangar oder den Laderaum des Schiffs (je nachdem, was näher liegt)
                if (kind == Rules.ShelterBase) { EnterRoom(); kind = Rules.ShelterKind(S, PS, P.Pos); }
                if (kind != 0)
                {
                    bool night = Rules.IsNight(S, S.CurrentPlanet), storm = PS.StormActive;
                    // Nachts schlafen (überspringt die Nacht); tagsüber im Sturm „schlafen“ = abwarten (der Sturm läuft weiter)
                    if ((night || storm) && Act(new JObj().Set("a", "sleep")))
                    {
                        Sleeps++; if (storm && !night) StormSleeps++;
                        if (kind == Rules.ShelterHangar) HangarSleeps++;
                        if (kind == Rules.ShelterShip) ShipSleeps++;
                        int g = 0;
                        while ((P.Sleeping || P.Waiting) && g++ < 800)
                        {
                            if (P.Waiting) StormWaitTime += Dt;
                            Step(Dt);
                        }
                    }
                    else Step(Dt); // Sturmwarnung: im Unterschlupf abwarten, bis der Sturm beginnt
                    continue;
                }
                float dist;
                var at = NearestShelter(out dist);
                if (dist > 45f && S.Credits >= Rules.ShelterCost + 150 && Rules.CanBuildShelter(S, PS, P.Pos) == null)
                {
                    if (Act(new JObj().Set("a", "shelter"))) { SheltersBuilt++; Note("Notunterschlupf gebaut (" + dist.ToString("0") + " m bis zum nächsten)"); continue; }
                }
                MoveTo(at, 1.5f, false);
            }
        }
        finally { sheltering = false; }
    }

    /// <summary>Durch das Tor bzw. über die Rampe in den nächsten Innenraum (Hangar oder Laderaum) fahren.</summary>
    void EnterRoom()
    {
        ShelterRoom best = null; float bd = float.MaxValue;
        foreach (var r in L.Base.Rooms) { float d = V3.DistXZ(P.Pos, r.Outside); if (d < bd) { bd = d; best = r; } }
        if (best == null) return;
        MoveTo(best.Outside, 0.5f, false);
        MoveTo(new V3(best.Door.Cx, best.Door.Y0, best.Door.Cz), 0.5f, false);
        MoveTo(best.Spot, 0.5f, false);
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
        ExitVehicle();
        // Gefahrstoffe direkt entsorgen
        if (P.Bin.Any(i => GameData.Trash[i.T].Yield.Keys.Any(m => GameData.Materials[m].DisposalBonus > 0)))
        {
            MoveTo(Station("disposal"), 3f);
            Act(new JObj().Set("a", "dispose"));
        }
        if (P.Bin.Count == 0) return;
        if (FirstSale < 0)
        {
            // Wie ein neuer Spieler (Tutorial „Erster Verkauf“): direkt aus dem Behälter verkaufen
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

    /// <summary>Verkauft, was über die Reserve (Projektmaterial + Grundstock) hinausgeht – Ballen zuerst (bester Preis).
    /// aggressive: alles verkaufen. Geht nur zum Terminal, wenn es wirklich etwas zu verkaufen gibt.</summary>
    void SellSurplus(bool aggressive)
    {
        var res = Reserve();
        bool moved = false;
        foreach (var kv in PS.Storage.ToList())
        {
            var md = GameData.Materials[kv.Key];
            if (md.Price <= 0) continue;
            var e = kv.Value;
            int reserve = aggressive ? 0 : (res.ContainsKey(kv.Key) ? res[kv.Key] : 0);
            int excess = e.U + e.S + e.B * GameData.BaleUnits - reserve;
            if (excess <= 0) continue;
            int nb = Math.Min(e.B, excess / GameData.BaleUnits); excess -= nb * GameData.BaleUnits;
            int ns = Math.Min(e.S, excess); excess -= ns;
            int nu = Math.Min(e.U, excess);
            if (nb + ns + nu == 0) continue;
            if (!moved) { ExitVehicle(); MoveTo(Station("sell"), 3f); moved = true; }
            if (nb > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 2).Set("n", nb));
            if (ns > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 1).Set("n", ns));
            if (nu > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 0).Set("n", nu));
        }
    }

    /// <summary>Verkauft alles außer den genannten Materialien (Platz im Lager schaffen).</summary>
    void SellExcept(IEnumerable<string> keep)
    {
        var k = new HashSet<string>(keep);
        bool moved = false;
        foreach (var kv in PS.Storage.ToList())
        {
            if (k.Contains(kv.Key) || GameData.Materials[kv.Key].Price <= 0) continue;
            var e = kv.Value;
            if (e.U + e.S + e.B == 0) continue;
            if (!moved) { MoveTo(Station("sell"), 3f); moved = true; }
            if (e.B > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 2).Set("n", e.B));
            if (e.S > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 1).Set("n", e.S));
            if (e.U > 0) Act(new JObj().Set("a", "sell").Set("m", kv.Key).Set("g", 0).Set("n", e.U));
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
    // t: Upgrade (je Eintrag eine Stufe über dem Stand bei Ankunft), b: Bauwerk (Anzahl), v: Fahrzeug
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

    void Bought(string kind, string id)
    {
        if (kind == "t" && FirstUpgrade < 0) { FirstUpgrade = T; FirstUpgradeId = id; Note("Erstes Upgrade: " + id); }
        skip.Clear();
    }

    void TryBuy()
    {
        for (int i = 0; i < 6; i++)
        {
            int cost;
            var item = NextPurchase(out cost);
            if (item == null || S.Credits - cost < saveFor) return;
            var kind = item.Substring(0, 1); var id = item.Substring(2);
            bool ok = false;
            if (kind == "t") { MoveTo(Station("workshop"), 3f); ok = Act(new JObj().Set("a", "buytech").Set("id", id)); }
            else if (kind == "v") { MoveTo(Station("workshop"), 3f); ok = Act(new JObj().Set("a", "buyveh").Set("id", id)); }
            else
            {
                var def = GameData.Buildings[id];
                if (def.Mats.Any(kv => PS.Available(kv.Key) < kv.Value)) return;
                ok = PlaceBuilding(id);
            }
            if (ok) { Bought(kind, id); Note("Gekauft: " + item + " (Credits " + S.Credits + ")"); }
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
        // Manuell sortieren, solange noch keine Sortieranlage läuft (auch Tutorial „Sortieren lohnt sich“)
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
        if (o.Frozen && S.TechLevel("heat") == 0) return "t:heat";
        if (t.Crane)
        {
            if (S.OwnedVehicles.Contains("crane")) return null;
            if (t.CutInto != null && S.TechLevel("cutter") >= t.MinCutter) return null;
            return "v:crane";
        }
        if (t.Hazard > S.TechVal("hazard")) return "t:hazard";
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
        SwSearch.Start();
        ObjView best = null; float bd = float.MaxValue;
        foreach (var o in Rules.All(PS))
        {
            if (skip.Contains(o.Key)) continue;
            float d = V3.DistXZ(o.Pos, P.Pos);
            if (d >= bd) continue;
            if (!filter(o)) continue;
            if (NeededTech(o) != null) continue;
            bd = d; best = o;
        }
        SwSearch.Stop();
        return best;
    }

    bool Accessible(int area) { return Rules.AreaStage(S, PS, area) >= 1; }

    void Process(ObjView o)
    {
        if (Environment.GetEnvironmentVariable("BOTTRACE") != null) Console.WriteLine("P " + Clock(T) + " " + o.Key + " " + o.T.Id + " d=" + V3.DistXZ(P.Pos, o.Pos).ToString("0") + " a" + o.Area + " cr " + S.Credits + " bin " + Item.Volume(P.Bin) + "/" + S.BinCapacity + " E " + P.Energy.ToString("0") + " pos " + P.Pos + " -> " + o.Pos);
        var t = o.T;
        if (t.Crane && S.OwnedVehicles.Contains("crane")) { CraneHaul(o); return; }
        ExitVehicle();
        float need = Math.Max(0.25f, t.Volume);
        if (Item.Volume(P.Bin) + need > S.BinCapacity) Deposit();
        if (P.Energy < 15) EnsureEnergy(15);
        MoveTo(o.Pos, Math.Max(1.2f, Rules.ObjRadius(t) + 1f));
        if (Rules.Obj(PS, o.Key) == null) return;
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
                    else if (lastErr != null && lastErr.StartsWith("Akku")) { EnsureEnergy(40); MoveTo(o.Pos, 2f); }
                    else { skip.Add(o.Key); return; }
                }
                Step(Dt);
            }
            return;
        }
        string err = Rules.CollectCheck(S, o, "grab", P.Pos, Item.Volume(P.Bin));
        if (err == null)
        {
            // Sauger sammelt leichte Objekte ringsum gleich mit (wie ein Spieler, der den Sauger schwenkt)
            if (t.Vacuum && S.TechLevel("vacuum") > 0 && P.Energy > 5)
            {
                var dir = new List<object> { o.Pos.x - P.Pos.x, o.Pos.z - P.Pos.z };
                if (Act(new JObj().Set("a", "vacuum").Set("dir", dir)) && Rules.Obj(PS, o.Key) == null) { Step(Dt); return; }
            }
            if (!Act(new JObj().Set("a", "grab").Set("o", o.Key))) skip.Add(o.Key);
            Step(Dt);
            return;
        }
        if (t.Magnet && S.TechLevel("magnet") >= t.MinMagnet)
        {
            var dir = new List<object> { o.Pos.x - P.Pos.x, o.Pos.z - P.Pos.z };
            if (!Act(new JObj().Set("a", "magnet").Set("charge", 1f).Set("dir", dir)))
            {
                if (lastErr != null && lastErr.StartsWith("Behälter voll")) Deposit();
                else if (lastErr != null && lastErr.StartsWith("Akku")) EnsureEnergy(50);
                else if (lastErr != null && lastErr.StartsWith("Magnet lädt")) Wait(0.5f);
                else skip.Add(o.Key);
            }
            Wait(0.75f);
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
        if (err.StartsWith("Behälter voll")) { Deposit(); return; }
        skip.Add(o.Key);
    }

    void CraneHaul(ObjView o)
    {
        Deposit();
        var crane = PS.Vehicles["crane"];
        if (P.Vehicle != "crane")
        {
            ExitVehicle();
            MoveTo(crane.Pos, 2f);
            if (!Act(new JObj().Set("a", "venter").Set("v", "crane"))) throw new Exception("Kran: " + lastErr);
        }
        MoveTo(o.Pos, 7f);
        int guard = 0;
        while (crane.Carry == null && guard++ < 400)
        {
            if (!Act(new JObj().Set("a", "clift").Set("o", o.Key).Set("dt", Dt))) throw new Exception("Anheben: " + lastErr);
            Step(Dt);
        }
        MoveTo(L.Base.DropZone, 4f);
        if (!Act(new JObj().Set("a", "cdrop"))) throw new Exception("Absetzen: " + lastErr);
        ExitVehicle();
        CraneHauls++;
        Note("Wrack mit dem Kran geborgen: " + o.T.Name);
    }

    /// <summary>Beschafft Geld über ausdrücklich bestellte Schrottlieferungen, falls der Müll im Zugang aufgebraucht ist.
    /// false, wenn keine Lieferung möglich ist (z. B. liegt noch ein Teil am Abladeplatz, das MIKO noch nicht heben kann).</summary>
    bool TryDelivery()
    {
        ExitVehicle();
        long before = S.Stat("credEarned");
        bool pending = PS.Dyn.Values.Any(d => d.Delivery);
        if (!pending)
        {
            MoveTo(Station("contracts"), 3f);
            // Abklingzeit: am Stützpunkt warten (ein Spieler würde sortieren, verkaufen oder aufräumen)
            float wait;
            if (Rules.DeliveryCheck(S, PS, out wait) != null && wait > 0)
            {
                float w = Math.Min(wait + 0.5f, 60f);
                DeliveryWaitTime += w;
                Wait(w);
                return true;
            }
            if (!Act(new JObj().Set("a", "delivery"))) return false;
            Deliveries++;
            DeliveryFees += GameData.DeliveryFee(S.CurrentPlanet);
        }
        int guard = 0, got = 0;
        while (PS.Dyn.Values.Any(d => d.Delivery && d.CarriedBy == null) && guard++ < 200)
        {
            var o = Nearest(x => x.D != null && x.D.Delivery);
            if (o == null) break;
            Process(o); got++;
        }
        Deposit();
        SellSurplus(false);
        DeliveryCredits += S.Stat("credEarned") - before;
        return got > 0;
    }

    /// <summary>Ein Schritt Geldverdienen: sammeln, Lieferung, Überschuss (notfalls auch Reserven) verkaufen, passendes Werkzeug kaufen.</summary>
    void EarnStep(string purpose)
    {
        if (P.Vehicle == null && Danger()) Shelter();
        long c0 = S.Credits;
        SellSurplus(false);
        if (S.Credits > c0) return;
        var o = Nearest(x => x.Gate < 0 && Accessible(x.Area));
        if (o != null) { Process(o); if (Item.Volume(P.Bin) > S.BinCapacity * 0.7f) Deposit(); return; }
        if (P.Bin.Count > 0) { Deposit(); return; }
        if (TryDelivery()) return;
        SellSurplus(true);
        if (S.Credits > c0) { Note("Reserven verkauft für " + purpose + " (Credits " + S.Credits + ")"); return; }
        // Günstigstes Werkzeug, das noch liegenden Müll zugänglich macht
        var need = new Dictionary<string, int>();
        foreach (var x in Rules.All(PS))
        {
            if (x.Gate >= 0 || !Accessible(x.Area) || (x.D != null && !x.D.Delivery && false)) continue;
            var n = NeededTech(x);
            if (n != null) need[n] = (need.ContainsKey(n) ? need[n] : 0) + 1;
        }
        string cheapest = null; int cc = int.MaxValue;
        foreach (var n in need.Keys)
        {
            int c = ItemCost(n);
            if (c <= S.Credits && c < cc) { cc = c; cheapest = n; }
        }
        if (cheapest != null)
        {
            long keep = saveFor; saveFor = 0;
            try { Buy(cheapest); } finally { saveFor = keep; }
            Note("Werkzeug für liegengebliebenen Müll gekauft: " + cheapest + " (für " + purpose + ")");
            return;
        }
        if (Environment.GetEnvironmentVariable("BOTDBG") != null)
        {
            var rem = Rules.All(PS).Where(x => x.Gate < 0 && Accessible(x.Area)).GroupBy(x => x.T.Id + (x.D != null ? (x.D.Delivery ? "[L]" : "[d]") : "") + "→" + NeededTech(x)).Select(gp => gp.Key + "×" + gp.Count());
            Console.WriteLine("REST: " + string.Join(", ", rem));
            Console.WriteLine("LAGER: " + string.Join(", ", PS.Storage.Select(kv => kv.Key + " " + kv.Value.U + "/" + kv.Value.S + "/" + kv.Value.B)));
            Console.WriteLine("MISSIONEN: " + string.Join(", ", S.Missions.Where(kv => kv.Value.Status == 1).Select(kv => kv.Key + " " + kv.Value.Progress)));
            Console.WriteLine("TECH: " + string.Join(", ", S.Tech.Select(kv => kv.Key + kv.Value)) + " sauber " + Rules.Cleanliness(PS, 0));
        }
        throw new Exception("Sackgasse: kein Geld für " + purpose + " (Credits " + S.Credits + "), nichts mehr sammelbar, keine Lieferung möglich. Offene Technik: " + string.Join(", ", need.Keys));
    }

    int ItemCost(string item)
    {
        var kind = item.Substring(0, 1); var id = item.Substring(2);
        return kind == "t" ? GameData.Tech[id].Levels[Math.Min(S.TechLevel(id) + 1, GameData.Tech[id].MaxLevel)].Cost : GameData.Vehicles[id].Cost;
    }

    void Buy(string item)
    {
        var kind = item.Substring(0, 1); var id = item.Substring(2);
        ExitVehicle();
        MoveTo(Station("workshop"), 3f);
        bool ok = kind == "t" ? Act(new JObj().Set("a", "buytech").Set("id", id)) : Act(new JObj().Set("a", "buyveh").Set("id", id));
        if (!ok) throw new Exception("Kauf " + item + " fehlgeschlagen: " + lastErr);
        Bought(kind, id);
    }

    // ------------------------------------------------------------ Hauptschleife
    int CurrentArea()
    {
        for (int a = 0; a < 3; a++) if (!PS.Projects[GameData.ProjectId(S.CurrentPlanet, a)].Done) return a;
        return -1;
    }

    public void Run()
    {
        Real.Start();
        PlanetStart[S.CurrentPlanet] = T;
        Note("Start auf " + GameData.Planets[S.CurrentPlanet].Name);
        int loops = 0;
        double lastT = -1; int idle = 0;
        while (!S.CampaignDone)
        {
            if (++loops > 500000) throw new Exception("Bot steckt fest (Schleifenzahl). Letzter Fehler: " + lastErr);
            // Schutz gegen Endlosschleifen ohne Zeitfortschritt
            if (T == lastT) { if (++idle > 200) throw new Exception("Bot macht keinen Fortschritt bei " + Clock(T) + ". Letzter Fehler: " + lastErr); }
            else { idle = 0; lastT = T; }
            if (P.TowTimer > 0) WaitTow();
            if (P.Vehicle == null && Danger()) { Shelter(); continue; }
            var planet = S.CurrentPlanet;
            int area = CurrentArea();
            if (area < 0) { NextPlanet(); continue; }
            if (Tutorial()) continue;
            int stage = Rules.AreaStage(S, PS, area);
            if (stage == 0) { ClearGate(area - 1); continue; }
            if (stage == 1) { CollectRound(area); continue; }
            var pid = GameData.ProjectId(planet, area);
            var st = PS.Projects[pid];
            if (st.Started) { Wait(2f); continue; }
            StartProject(area);
        }
        if (!PlanetDone.ContainsKey(S.CurrentPlanet)) PlanetDone[S.CurrentPlanet] = T;
        Note("Kampagne abgeschlossen");
        Report();
        Real.Stop();
    }

    /// <summary>Der Anfang wie im Tutorial: Lichtpunkt am Stützpunkt aufräumen, dann der erste Verkauf.</summary>
    bool Tutorial()
    {
        if (S.CurrentPlanet != S.StartPlanet) return false;
        MissionState ms;
        bool zoneDone = S.Missions.TryGetValue("tut_zone", out ms) && ms.Status == 2;
        if (!zoneDone)
        {
            var o = Nearest(x => x.Zone == 0 && x.IsStatic);
            if (o == null) return false;
            Process(o);
            return true;
        }
        if (FirstSale < 0 && P.Bin.Count > 0) { Deposit(); return true; }
        return false;
    }

    void CollectRound(int area)
    {
        if (TryBlast(area)) return;
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

    /// <summary>
    /// Sprengt einen nahen Müllberg im aktuellen Bereich, wenn es Zeit spart: Bereich schon zu 45–83 % sauber (der restliche Müll liegt verstreut), kein Sparziel offen, Anteil der Berg-Stücke
    /// am Hauptmüll noch nicht ausgeschöpft, Berg höchstens 30 m entfernt, nach dem Kauf bleibt Geld für das Sparziel übrig.
    /// Danach werden die Stücke (dicht beieinander) eingesammelt.
    /// </summary>
    bool TryBlast(int area)
    {
        if (!UseTnt || P.Vehicle != null || Danger()) return false;
        float clean = Rules.Cleanliness(PS, area);
        if (clean < 0.45f || clean > 0.83f || saveFor > 0) return false;
        float cap = L.AreaWeight[area] * GameData.TntCleanShare;
        if (Rules.HeapCredit(PS, area) >= cap - 15f) return false; // eine Sprengung (≈ 12 Stücke) muss sich noch anrechnen lassen
        int price = GameData.TntPrice(S.CurrentPlanet);
        int best = -1; float bd = 30f;
        for (int i = 0; i < L.Mounds.Count; i++)
        {
            var m = L.Mounds[i];
            if (m.Area != area || blastSkip.Contains(S.CurrentPlanet + i) || Rules.MoundBlastCheck(S, PS, i) != null) continue;
            float d = V3.DistXZ(P.Pos, m.Pos) - m.Radius;
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) return false;
        if (P.Tnt == 0)
        {
            long spare = S.Credits - saveFor - 400;
            int n = (int)Math.Min(GameData.TntMaxCarry, spare / price);
            if (n < 2) return false;
            Deposit();
            MoveTo(Station("workshop"), 3f);
            if (!Act(new JObj().Set("a", "buytnt").Set("n", n))) return false;
            TntSpent += (long)price * n;
        }
        var mound = L.Mounds[best];
        float sc = Rules.MoundScale(PS, best);
        var r = new Rng(best * 31 + (int)T);
        float water = Terrain.WaterLevel(S.CurrentPlanet);
        V3 spot = default(V3), dir = default(V3); float charge = 0; bool found = false;
        for (int k = 0; k < 24 && !found; k++)
        {
            float a = M.Atan2(P.Pos.z - mound.Pos.z, P.Pos.x - mound.Pos.x) + (k % 2 == 0 ? 1 : -1) * (k / 2) * 0.35f;
            float d = mound.Radius * sc + 8.5f + r.Range(0f, 3f);
            float x = mound.Pos.x + M.Cos(a) * d, z = mound.Pos.z + M.Sin(a) * d;
            if (Math.Abs(x) > 144 || Math.Abs(z) > 144 || L.Base.InBase(x, z) || L.BlockedStatic(x, z, 1f)) continue;
            float y = L.GroundAt(x, z);
            if (y < water + 0.3f || Rules.HeapAt(PS, new V3(x, 0, z), 1.5f) >= 0) continue;
            for (int g = 0; g < area; g++) if (!Rules.GateOpen(PS, g)) continue;
            spot = new V3(x, y, z);
            TntFlight f;
            if (Rules.TntAim(PS, spot, mound.Pos, Math.Max(1.5f, mound.Radius * sc * 0.6f), out dir, out charge, out f)
                && (f.Mound == best || Rules.HeapAt(PS, f.Pos, GameData.TntHeapReach) == best)) found = true;
        }
        if (!found) { blastSkip.Add(S.CurrentPlanet + best); return false; }
        MoveTo(spot, 0.3f);
        if (Danger()) return true;
        int stage0 = PS.Blasts(best);
        if (!Act(new JObj().Set("a", "tnt").Set("dir", Json.Arr(dir.x, dir.y, dir.z)).Set("s", charge))) { blastSkip.Add(S.CurrentPlanet + best); return false; }
        TntThrows++;
        int guard = 0;
        while (PS.Tnt.Count > 0 && guard++ < 100) Step(Dt);
        if (PS.Blasts(best) > stage0) TntBlasts++;
        guard = 0;
        ObjView piece;
        while (Rules.HeapCredit(PS, area) < cap && (piece = Nearest(x => x.D != null && x.D.Heap == best + 1)) != null && guard++ < 40) { Process(piece); TntPieces++; }
        return true;
    }

    void Acquire(string item)
    {
        var kind = item.Substring(0, 1); var id = item.Substring(2);
        int targetLevel = kind == "t" ? S.TechLevel(id) + 1 : 1;
        Func<bool> have = () => kind == "t" ? S.TechLevel(id) >= targetLevel : S.OwnedVehicles.Contains(id);
        int cost = ItemCost(item);
        saveFor = cost;
        try
        {
            Deposit();
            int guard = 0;
            while (!have() && S.Credits < cost && guard++ < 2000) EarnStep(item);
            Deposit();
        }
        finally { saveFor = 0; }
        if (have()) return; // inzwischen über die Einkaufsliste gekauft
        if (S.Credits < cost) throw new Exception("Kein Geld für " + item);
        Buy(item);
        Note("Beschafft (benötigt): " + item + " (Credits " + S.Credits + ")");
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

    void StartProject(int area, int retry = 0)
    {
        var pid = GameData.ProjectId(S.CurrentPlanet, area);
        var pd = GameData.Projects[pid];
        int guard = 0;
        while (true)
        {
            if (guard++ > 3000) throw new Exception("Projekt " + pid + " nicht startbar: " + Rules.ProjectCheck(S, pid));
            saveFor = pd.Credits + pd.Mats.Sum(kv => (long)GameData.BuyPrice(kv.Key) * Math.Max(0, kv.Value - PS.Available(kv.Key)));
            if (P.Vehicle == null && Danger()) Shelter();
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
                int n = Math.Min(200, missingMat.Value - PS.Available(missingMat.Key));
                long cost = (long)GameData.BuyPrice(missingMat.Key) * n;
                int g2 = 0;
                while (S.Credits < cost + pd.Credits && g2++ < 2000) EarnStep(pd.Name);
                Deposit();
                if (PS.StorageUsed() + n > PS.StorageCap()) SellSurplus(false);
                if (PS.StorageUsed() + n > PS.StorageCap()) SellExcept(pd.Mats.Keys);
                MoveTo(Station("trader"), 3f);
                if (!Act(new JObj().Set("a", "buymat").Set("m", missingMat.Key).Set("n", n))) throw new Exception("Materialkauf: " + lastErr);
                Note("Material gekauft: " + n + " " + missingMat.Key + " (Credits " + S.Credits + ")");
                continue;
            }
            if (S.Credits < pd.Credits) { EarnStep(pd.Name); continue; }
            throw new Exception("Projekt " + pid + ": " + why);
        }
        saveFor = 0;
        ExitVehicle();
        MoveTo(L.ProjectSites[area], 6f);
        // Unterwegs kann ein Notunterschlupf Credits gekostet haben → erneut ansparen
        if (Rules.ProjectCheck(S, pid) != null && retry < 5) { StartProject(area, retry + 1); return; }
        if (!Act(new JObj().Set("a", "project").Set("area", area))) throw new Exception("Projektstart: " + lastErr);
        Note("Projekt gestartet: " + pd.Name + " (Credits " + S.Credits + ")");
    }

    /// <summary>Nächstes Ziel: die übrigen Startplaneten der Reihe nach (freie Reise), dann Sprungantrieb und NIVALIS.</summary>
    void NextPlanet()
    {
        if (!PlanetDone.ContainsKey(S.CurrentPlanet)) { PlanetDone[S.CurrentPlanet] = T; Note(GameData.Planets[S.CurrentPlanet].Name + " abgeschlossen"); }
        string next = null;
        foreach (var pl in GameData.PlanetOrder)
            if (pl != S.CurrentPlanet && !S.Planet(pl).Projects[GameData.ProjectId(pl, 2)].Done) { next = pl; break; }
        if (next == null) { Wait(1); return; }
        var pd = GameData.Planets[next];
        ExitVehicle();
        Deposit();
        while (S.ShipLevel < pd.ShipLevelRequired)
        {
            int cost = GameData.ShipLevelCost[S.ShipLevel + 1];
            saveFor = cost;
            int guard = 0;
            while (S.Credits < cost && guard++ < 5000) EarnStep(GameData.ShipLevelName[S.ShipLevel + 1]);
            Deposit();
            SellSurplus(true);
            MoveTo(Station("ship"), 3f);
            saveFor = 0;
            if (!Act(new JObj().Set("a", "buyship"))) throw new Exception("Schiff: " + lastErr);
            Note("Transportschiff: " + GameData.ShipLevelName[S.ShipLevel] + " (Credits " + S.Credits + ")");
        }
        if (!S.Unlocked.Contains(next)) throw new Exception(next + " nicht freigeschaltet");
        SellSurplus(true);
        MoveTo(Station("ship"), 3f);
        if (!Act(new JObj().Set("a", "travel").Set("planet", next))) throw new Exception("Reise: " + lastErr);
        Travels++;
        foreach (var t in GameData.TechOrder) levelsAtArrival[next + t] = S.TechLevel(t);
        skip.Clear();
        if (!PlanetStart.ContainsKey(next)) PlanetStart[next] = T;
        Note("Reise nach " + pd.Name + " (Credits " + S.Credits + ")");
    }
}
