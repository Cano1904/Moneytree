// Laufzeitprüfung der „belebten Welt“: zurückkehrende Tiere, Stadtleben (Autos, Bahnen, Fahnen, Hologramme, Lichthöfe),
// Spuren/Staub/Pfützen, Wind (Pflanzen, Wolkenschatten) sowie MIKOs Mimik und Gesten. Je Planet wird der Zustand
// vorübergehend auf „vollständig wiederhergestellt“ gesetzt (Server und Client-Replik), geprüft und zurückgesetzt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RePlanet;
using RePlanet.Core;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class Checks
{
    class Saved
    {
        public float[] Weight; public Dictionary<string, bool> Done = new Dictionary<string, bool>(); public Dictionary<string, double> Eco;
    }

    static Saved SaveLife(PlanetState ps)
    {
        var s = new Saved { Weight = (float[])ps.RemovedWeight.Clone(), Eco = new Dictionary<string, double>(ps.Eco) };
        foreach (var kv in ps.Projects) s.Done[kv.Key] = kv.Value.Done;
        return s;
    }

    static void RestoreLife(PlanetState ps, Saved s)
    {
        Array.Copy(s.Weight, ps.RemovedWeight, 3);
        foreach (var kv in s.Done) if (ps.Projects.ContainsKey(kv.Key)) ps.Projects[kv.Key].Done = kv.Value;
        ps.Eco.Clear(); foreach (var kv in s.Eco) ps.Eco[kv.Key] = kv.Value;
    }

    static void Restore100(WorldState w, PlanetState ps, string planet)
    {
        var l = WorldGen.Get(planet);
        for (int a = 0; a < 3; a++) ps.RemovedWeight[a] = l.AreaWeight[a];
        foreach (var p in ps.Projects.Values) p.Done = true;
        foreach (var e in l.Eco) ps.Eco[e.Id] = w.PlayTime - 100000.0;
    }

    /// <summary>Mittlere Dauer (ms) eines Update-Aufrufs einer Komponente (Prüfumgebung – nur als Richtwert).</summary>
    static double TimeUpdate(Component c, int n = 60)
    {
        var m = M(c.GetType(), "Update") ?? M(c.GetType(), "LateUpdate");
        if (m == null) return 0;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++) m.Invoke(c, null);
        return sw.Elapsed.TotalMilliseconds / n;
    }

    static void LifeChecks(GameApp app, Game g, string pid, string planet)
    {
        Info("Belebte Welt …");
        var wl = Wildlife.I; var city = CityLife.I; var marks = GroundMarks.I; var wind = WindLook.I; var gest = MikoGestures.I;
        if (wl == null || city == null || marks == null || wind == null || gest == null) { Fail("Bausteine der belebten Welt fehlen (Tiere " + (wl != null) + ", Stadt " + (city != null) + ", Spuren " + (marks != null) + ", Wind " + (wind != null) + ", Gesten " + (gest != null) + ")"); return; }
        Run(1f);
        Info($"  Ausgangszustand: {wl.AliveCount} Tiere (Plätze {wl.SlotCount}), {city.CarsAlive} Autos, Fahrspur-Abschnitte {city.RunCount}, Autoplätze {city.CarSlots}");

        var sps = g.S.Planet(planet); var cps = app.W.Cur;
        var saveS = SaveLife(sps); var saveC = SaveLife(cps);
        Restore100(g.S, sps, planet); Restore100(app.W, cps, planet);
        SetPhase(g, planet, 0.42f);
        // MIKO an den Projektplatz von Bereich 1 (freie Stelle daneben)
        var l = WorldGen.Get(planet);
        var site = l.ProjectSites[1];
        var tmp = new List<Box>();
        V3 spot = site;
        for (int k = 0; k < 40; k++)
        {
            float ang = k * 0.9f, r = 9f + k * 0.4f;
            float x = site.x + Mathf.Cos(ang) * r, z = site.z + Mathf.Sin(ang) * r;
            if (LifeCommon.Solid(l, x, z, 1.2f, tmp) || (RePlanet.Core.Terrain.WaterLevel(planet) > -50 && RePlanet.Core.Terrain.HeightAt(planet, x, z) < 0.3f)) continue;
            spot = new V3(x, l.GroundAt(x, z), z); break;
        }
        Teleport(g, pid, spot);
        var probs = new List<string>();
        int animalBad = 0;
        Run(20f, 1f / 30f, t => { if ((int)(t * 30) % 3 == 0) animalBad += wl.Validate(probs); });
        if (animalBad > 0) Fail($"Tiere in 20 s {animalBad}× ungültig, z. B. {string.Join(" | ", probs.Take(3))}");
        probs.Clear();
        var bySp = wl.CountBySpecies();
        Info($"  Wiederhergestellt (Tag): {wl.AliveCount} Tiere lebend, {wl.DrawnCount} gezeichnet, {wl.BatchCalls} Instanz-Aufrufe – " + string.Join(", ", bySp.Select(kv => kv.Key + " " + kv.Value)));
        if (wl.AliveCount == 0) Fail("Keine Tiere trotz vollständiger Wiederherstellung (" + planet + ")");
        int bad = wl.Validate(probs);
        if (bad > 0) Fail($"Tiere: {bad} Verstöße, z. B. {string.Join(" | ", probs)}");
        Info($"  Zeit je Update (Prüfumgebung): Tiere {TimeUpdate(wl):0.00} ms, Stadt {TimeUpdate(city):0.00} ms, Spuren {TimeUpdate(marks):0.00} ms, Wind {TimeUpdate(wind):0.00} ms, Pflanzen {TimeUpdate(Object.FindObjectOfType<FloraRenderer>(), 20):0.00} ms");

        // Tiere fliehen vor MIKO: auf das nächste Bodentier zufahren
        {
            Vector3 an;
            if (wl.NearestAnimal(PlayerController.I.RenderPos, 60f, out an))
            {
                var before = an;
                Teleport(g, pid, new V3(an.x + 1.5f, l.GroundAt(an.x + 1.5f, an.z), an.z));
                Run(2.5f);
                Vector3 after;
                bool still = wl.NearestAnimal(new Vector3(an.x + 1.5f, an.y, an.z), 2.5f, out after);
                Info("  Flucht: Tier bei " + before.ToString() + (still ? " noch in 2,5 m Nähe" : " ist geflohen"));
                probs.Clear();
                if (wl.Validate(probs) > 0) Fail("Tiere nach Flucht: " + string.Join(" | ", probs));
            }
            Teleport(g, pid, spot);
            Run(1f);
        }

        // Stadtleben
        float s0 = city.SumS();
        int carBad = 0; var carProbs = new List<string>();
        Run(20f, 1f / 30f, t => carBad += city.Validate(carProbs));
        float s1 = city.SumS();
        if (carBad > 0) Fail($"Stadtfahrzeuge in 20 s {carBad}× ungültig, z. B. {string.Join(" | ", carProbs.Take(3))}");
        Info($"  Stadt: {city.CarsAlive} Autos, {city.TramsAlive} Bahnen, {city.FlagsShown} Fahnen, {city.HolosShown} Hologramme (Fahrspur-Abschnitte {city.RunCount})");
        if (city.RunCount > 0 && city.CarsAlive == 0) Fail("Keine Stadtfahrzeuge trotz erwachter Bereiche (" + planet + ")");
        if (city.CarsAlive > 0 && Math.Abs(s1 - s0) < 0.01f) Fail("Stadtfahrzeuge bewegen sich nicht");
        probs.Clear();
        if (city.Validate(probs) > 0) Fail("Stadtleben: " + string.Join(" | ", probs));
        if (city.HolosShown == 0) Info("    (kein Hologramm in Sichtweite)");

        // Wind
        var flora = Object.FindObjectOfType<FloraRenderer>();
        Info($"  Wind {wind.Wind:0.00}: Pflanzen-Ausschlag {flora.MaxShear:0.000}, Wolkenschatten Stärke {wind.Strength:0.00}, Schwelle {wind.Coverage:0.00}");
        if (float.IsNaN(flora.MaxShear) || flora.MaxShear > 0.6f) Fail("Pflanzen wiegen sich unplausibel: " + flora.MaxShear);
        if (wind.Strength < 0f || wind.Strength > 0.5f) Fail("Wolkenschatten-Stärke außerhalb 0…0,5: " + wind.Strength);

        // Spuren: ein Rad auf weichem Boden, danach MIKO selbst fahren lassen
        {
            var p0 = PlayerController.I.RenderPos;
            Vector3? soft = null;
            for (int k = 0; k < 60 && soft == null; k++)
            {
                float x = p0.x + Mathf.Cos(k * 1.3f) * (2f + k * 0.5f), z = p0.z + Mathf.Sin(k * 1.3f) * (2f + k * 0.5f);
                if (marks.SoftGround(x, z) && marks.SoftGround(x, z + 1f)) soft = new Vector3(x, RePlanet.Core.Terrain.HeightAt(planet, x, z), z);
            }
            int before = marks.LiveStamps;
            if (soft.HasValue)
            {
                marks.TestTrack(soft.Value, soft.Value + new Vector3(0, 0, 1f));
                Run(0.1f);
                if (marks.LiveStamps <= before) Fail("Spur auf weichem Boden wurde nicht angelegt (" + planet + " bei " + soft.Value + ")");
            }
            else Info("    (kein weicher Boden in der Nähe)");
            long dust0 = marks.DustPuffs;
            Run(3f, 1f / 30f, t => { Input.Held.Clear(); Input.Held.Add(KeyCode.W); Input.Held.Add(KeyCode.LeftShift); if (t > 1.5f) Input.Held.Add(KeyCode.A); });
            Input.Held.Clear();
            probs.Clear();
            Info($"  Spuren: {marks.LiveStamps} lebend (Platz {marks.Capacity}), {marks.DrawnStamps} gezeichnet, Staubstöße {marks.DustPuffs - dust0}");
            if (marks.Validate(probs) > 0) Fail("Spuren: " + string.Join(" | ", probs));
        }

        // MIKO: Stimmung, Gesten, Roboterlaut beim Mitspieler
        {
            var r = ActorsView.I.LocalRobot;
            if (r == null) Fail("Kein eigener Roboter für Mimik-Prüfung");
            else
            {
                int g0 = r.GestureCount;
                app.Client.SendEmote("happy");
                Run(0.6f);
                if (r.GestureCount == g0) Fail("Roboterlaut löst beim eigenen Roboter keine Geste aus (Weg über den Server)");
                r.Gesture("wave", r.transform.position + r.transform.forward * 3f);
                Run(1f);
                var fx = typeof(MikoGestures).GetMethod("OnFx", BF);
                fx.Invoke(gest, new object[] { new JObj().Set("k", "deposit").Set("pid", pid).Set("n", 3) });
                Run(1.2f);
                Info($"  MIKO: Stimmung {RobotModel.MoodNames[r.Mood]}, Gesten {r.GestureCount - g0}, Freudensprünge gesamt {gest.Hops}");
                if (r.GestureCount - g0 < 3) Fail("MIKO-Gesten werden nicht abgespielt (" + (r.GestureCount - g0) + ")");
                if (!LifeCommon.Finite(r.transform.position)) Fail("MIKO-Position nach Gesten ungültig");
            }
        }

        // Nacht: Bodentiere verstecken sich, Vögel landen, Laternen bekommen Lichthöfe
        int dayAlive = wl.AliveCount;
        SetPhase(g, planet, 0.93f);
        Run(12f);
        var rob = ActorsView.I.LocalRobot;
        Info($"  Nacht: {wl.AliveCount} Tiere (Tag {dayAlive}), Lichthöfe {city.HalosShown}, MIKO-Stimmung {(rob != null ? RobotModel.MoodNames[rob.Mood] : "–")}");
        probs.Clear();
        if (wl.Validate(probs) > 0) Fail("Tiere nachts: " + string.Join(" | ", probs));

        // Sturm: fast alle verstecken sich; Regen auf PELAGIA macht Pfützen
        SetPhase(g, planet, 0.45f);
        sps.StormTimer = GameData.Planets[planet].StormEvery + 1f;
        Run(10f);
        Info($"  Sturm: {wl.AliveCount} Tiere, Nässe {marks.Wetness:0.00}, MIKO-Stimmung {(rob != null ? RobotModel.MoodNames[rob.Mood] : "–")}, Autos {city.CarsAlive}");
        sps.StormTimer = GameData.Planets[planet].StormDuration + 1f;
        Run(8f);
        Info($"  Nach dem Sturm: Nässe {marks.Wetness:0.00}, Pfützenplätze {marks.PuddleSpots}, sichtbar {marks.VisiblePuddles}");
        if (GroundMarks.RainPlanet(planet) && marks.PuddleSpots == 0) Fail("Keine Pfützenplätze auf " + planet);
        if (GroundMarks.RainPlanet(planet) && marks.Wetness <= 0.01f) Fail("Regen hinterlässt keine Nässe auf " + planet);

        // Fotomodus „Vorher“: alles Leben verschwindet
        PhotoMode.Active = true; PhotoMode.ShowBefore = true;
        Run(7f);
        int beforeAlive = wl.AliveCount, beforeCars = city.CarsAlive;
        PhotoMode.ShowBefore = false; PhotoMode.Active = false;
        Info($"  Vorher-Ansicht: {beforeAlive} Tiere, {beforeCars} Autos");
        if (beforeCars > 0) Fail("Stadtfahrzeuge in der Vorher-Ansicht sichtbar");

        // zurücksetzen
        RestoreLife(sps, saveS); RestoreLife(cps, saveC);
        Run(3f);
        probs.Clear();
        if (wl.Validate(probs) > 0) Fail("Tiere nach dem Zurücksetzen: " + string.Join(" | ", probs));
        Info($"  Zurückgesetzt: {wl.AliveCount} Tiere, Instanz-Draws gesamt {InstanceBatch.TotalCalls}, Instanzen gesamt {InstanceBatch.TotalDrawn}");
        CollectWarnings();
    }
}
