// Laufzeitprüfung der Zusatzsysteme: Weltereignisse (Meteoriten, Versorgung, Deponie) mit Darstellung, Helferroboter
// reparieren/mitnehmen/absetzen (Modelle im Bild), Schnellreise zu einem leuchtenden Lichtpunkt, Sturm abwarten (Zeitraffer).
using System;
using System.Linq;
using RePlanet;
using RePlanet.Core;
using UnityEngine;

public static partial class Checks
{
    static void FeatureChecks(GameApp app, Game g, string pid, string planet)
    {
        Info("Zusatzsysteme …");
        var l = WorldGen.Get(planet);
        var ps = g.S.Cur;
        var b = l.Base;
        ps.StormActive = false; ps.StormWarn = false; ps.StormTimer = 0;
        SetPhase(g, planet, 0.45f);
        Run(1f);

        // Weltereignisse
        int before = ps.Dyn.Values.Count(d => d.Ev > 0);
        bool m = g.MeteorShower(ps, new Rng(11)), s = g.SupplyDrop(ps, new Rng(12)), d0 = g.UncoverDump(ps, new Rng(13));
        Run(8f);
        int after = app.W.Cur.Dyn.Values.Count(d => d.Ev > 0);
        Info($"  Ereignisse: Meteoriten {m}, Versorgung {s}, Deponie {d0}; Ereignisfunde beim Client {after} (vorher {before})");
        if (!m || !s || !d0) Fail("Weltereignis ließ sich nicht auslösen");
        if (after <= before) Fail("Ereignisfunde kommen beim Client nicht an");

        // Helferroboter: reparieren, Modell sichtbar, mitnehmen, absetzen
        var spot = l.Bots[0];
        var near = new V3(spot.Pos.x + 1.5f, l.GroundAt(spot.Pos.x + 1.5f, spot.Pos.z), spot.Pos.z);
        Teleport(g, pid, near);
        Run(0.5f);
        g.S.Credits += 5000;
        foreach (var kv in GameData.HelperMats(planet)) g.S.Cur.Store(kv.Key).S += kv.Value * 2;
        ActResult r = null;
        for (int i = 0; i < 30; i++)
        {
            r = At(g, pid, near, new JObj().Set("a", "botfix").Set("s", spot.Id).Set("dt", 0.25f));
            Run(0.25f);
            if (!r.Ok || r.Data != null && r.Data.Bool("done")) break;
        }
        Run(2f);
        bool fixedOk = app.W.Cur.Bots.ContainsKey(spot.Id);
        Info("  Helfer repariert: " + (r != null && r.Ok ? "ok" : r?.Err) + ", beim Client " + fixedOk + ", Modelle " + (FeaturesView.I != null ? FeaturesView.I.GetComponentsInChildren<RobotModel>(true).Length : -1));
        if (!fixedOk) Fail("Helferroboter wird nicht repariert/repliziert: " + r?.Err);
        if (FeaturesView.I == null || FeaturesView.I.GetComponentsInChildren<RobotModel>(true).Length < l.Bots.Count) Fail("Helferroboter nicht dargestellt");
        var fr = At(g, pid, near, new JObj().Set("a", "botfollow").Set("s", spot.Id));
        if (!fr.Ok) Fail("Helfer folgt nicht: " + fr.Err);
        Run(2f);
        var sr = g.Apply(pid, new JObj().Set("a", "botstay").Set("s", spot.Id).Set("rid", "bs" + planet), true);
        Info("  Helfer mitnehmen/absetzen: " + (fr.Ok ? "ok" : fr.Err) + " / " + (sr.Ok ? "ok" : sr.Err));
        Run(6f);

        // Schnellreise: Lichtpunkt 0 räumen, vom Stützpunkt dorthin reisen
        var zone = l.Zones[0];
        foreach (var id in zone.Objects) ps.Removed.Set(id);
        ps.RecomputeDerived();
        g.MarkAllDirty();
        g.S.Players[pid].Bin.Clear();
        g.S.Players[pid].Energy = g.S.MaxEnergy;
        Teleport(g, pid, b.Spawn);
        Run(1f);
        var tr = g.Apply(pid, new JObj().Set("a", "fasttravel").Set("z", 0).Set("rid", "ft" + planet), true);
        Run(2f);
        float dz = V3.DistXZ(app.Me.Pos, zone.Center);
        Info("  Schnellreise zu „" + zone.Name + "“: " + (tr.Ok ? "ok" : tr.Err) + ", Client " + dz.ToString("0.0") + " m vom Lichtpunkt, Vorhersage " + (PlayerController.I.RenderPos - new Vector3(zone.Center.x, zone.Center.y, zone.Center.z)).magnitude.ToString("0.0") + " m");
        if (!tr.Ok) Fail("Schnellreise abgelehnt: " + tr.Err);
        else if (dz > 14f) Fail("Client nach der Schnellreise nicht am Lichtpunkt (" + dz.ToString("0.0") + " m)");

        // Sturm abwarten am Stützpunkt: Zeitraffer, Sturm läuft weiter
        Teleport(g, pid, new V3(b.Stations["storage"].x + 2f, b.Stations["storage"].y, b.Stations["storage"].z + 2f));
        Run(0.5f);
        ps.StormTimer = GameData.Planets[planet].StormEvery + 1f;
        Run(1.5f);
        var wr = g.Apply(pid, new JObj().Set("a", "sleep").Set("rid", "wt" + planet), true);
        Run(1.5f);
        Info("  Sturm abwarten: " + (wr.Ok ? "ok" : wr.Err) + ", Client wartet " + app.Me.Waiting + ", Zeitraffer ×" + g.TimeScale + ", Sturm " + app.W.Cur.StormActive);
        if (!wr.Ok || !app.Me.Waiting) Fail("Abwarten im Sturm klappt nicht: " + wr.Err);
        if (g.TimeScale < 3.9f) Fail("Kein Zeitraffer beim Abwarten");
        if (!app.W.Cur.StormActive) Fail("Sturm wurde übersprungen");
        g.Apply(pid, new JObj().Set("a", "wake").Set("rid", "wk" + planet), true);
        ps.StormActive = false; ps.StormTimer = 0;
        Run(1f);
        Info("  Erfolge: " + g.S.Achievements.Count + "/" + GameData.Achievements.Count);
    }
}
