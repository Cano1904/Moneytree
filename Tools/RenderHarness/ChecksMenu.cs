// Hauptmenü-Stabilität („Flackern im Hauptmenü“): 20 s Menü-Hintergrund Bild für Bild mitschreiben –
// Belichtung (wie PostFX sie berechnet), Nebel/Umgebungslicht, ein-/ausgeschaltete Renderer/Lichter/Kameras,
// Kamerawechsel und -sprünge, Instanz-Draws je Bild, Neuberechnungen der Reflexionssonde – plus die Logo-Uhr
// (MenuLogoClock) mit Rucklern. Ausgabe: Kennzahlen; Fehler, wenn etwas pro Bild umschaltet oder springt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RePlanet;
using RePlanet.Core;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class Checks
{
    static void MenuStabilityChecks(GameApp app)
    {
        LogoClockCheck();

        Begin("Hauptmenü: 20 s ruhig (Flackern)");
        app.Mode = AppMode.Menu;
        WorldView.I?.BuildMenuBackdrop();
        UIState.Open(UIScreen.MainMenu);
        Run(2f, 1f / 60f); // einschwingen (Sonde, Nebelanteil, Kamera)

        var atm = Atmosphere.I;
        if (atm == null) { Fail("Atmosphere fehlt im Hauptmenü"); return; }
        var probeF = typeof(Atmosphere).GetField("probe", BF);
        var probe = probeF?.GetValue(atm) as ReflectionProbe;
        if (probe != null && probe.timeSlicingMode != UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing)
            Fail("Reflexionssonde rechnet mit Zeitscheiben (" + probe.timeSlicingMode + ") – Würfelseiten zeitweise unterschiedlich alt");

        float bright = app.Settings.Brightness;
        var state = new Dictionary<Component, bool>();
        var toggles = new Dictionary<Component, int>();
        var expo = new List<float>(); var fogSky = new List<float>(); var fogLum = new List<float>(); var sunI = new List<float>(); var amb = new List<float>();
        var drawsPerFrame = new List<long>();
        var camSteps = new List<float>();
        Camera cam0 = Camera.main; int camChanges = 0; Vector3 lastPos = cam0 != null ? cam0.transform.position : Vector3.zero;
        int bornRenderers = 0, deadRenderers = 0;
        var knownRenderers = new HashSet<Component>();
        foreach (var c in World.All) if (c is Renderer) knownRenderers.Add(c);
        long lastDraws = Graphics.Draws; int lastFrameNo = Time.frameCount;
        int probe0 = ReflectionProbe.Renders;
        int frames = 0;
        bool hitchDone = false;

        bool On(Component c)
        {
            if (c.IsDead) return false;
            bool act = c.gameObject.activeInHierarchy;
            if (c is Renderer r) return r.enabled && act;
            if (c is Behaviour b) return b.enabled && act;
            return act;
        }

        void Sample()
        {
            frames++;
            expo.Add(Atmosphere.AutoExposureFor(bright));
            fogSky.Add(atm.Look.FogSky);
            var fc = RenderSettings.fogColor; fogLum.Add(0.2126f * fc.r + 0.7152f * fc.g + 0.0722f * fc.b);
            sunI.Add(atm.Sun != null ? atm.Sun.intensity : 0f);
            var ae = RenderSettings.ambientEquatorColor; amb.Add(ae.r + ae.g + ae.b);
            int fd = Math.Max(1, Time.frameCount - lastFrameNo); lastFrameNo = Time.frameCount;
            drawsPerFrame.Add((Graphics.Draws - lastDraws) / fd); lastDraws = Graphics.Draws;
            var cam = Camera.main;
            if (cam != cam0) { camChanges++; cam0 = cam; }
            if (cam != null) { var p = cam.transform.position; camSteps.Add((p - lastPos).magnitude); lastPos = p; }
            var alive = new HashSet<Component>();
            foreach (var c in World.All)
            {
                if (!(c is Renderer || c is Light || c is Camera)) continue;
                if (c is Renderer) { alive.Add(c); if (knownRenderers.Add(c)) bornRenderers++; }
                bool on = On(c);
                if (state.TryGetValue(c, out var was) && was != on) { toggles.TryGetValue(c, out var n); toggles[c] = n + 1; }
                state[c] = on;
            }
            foreach (var c in knownRenderers) if (!alive.Contains(c)) deadRenderers++;
            knownRenderers.IntersectWith(alive);
        }

        Run(20f, 1f / 60f, tt =>
        {
            Sample();
            // ein Ruckler mitten im Menü (wie Musik-Clips anlegen / Speicherbereinigung): darf nichts umschalten
            if (!hitchDone && tt >= 10f) { hitchDone = true; Frame(0.8f); }
        });

        static (float mean, float relSd, float maxRelStep) Stat(List<float> v)
        {
            if (v.Count == 0) return (0, 0, 0);
            float mean = v.Average();
            float sd = (float)Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Average());
            float maxStep = 0f;
            for (int i = 1; i < v.Count; i++) maxStep = Math.Max(maxStep, Math.Abs(v[i] - v[i - 1]));
            float m = Math.Max(1e-5f, Math.Abs(mean));
            return (mean, sd / m, maxStep / m);
        }

        var se = Stat(expo); var sf = Stat(fogSky); var sl = Stat(fogLum); var ss = Stat(sunI); var sa = Stat(amb);
        Info($"{frames} Bilder (60 Hz, dazu ein Ruckler von 0,8 s)");
        Info($"Belichtung: Mittel {se.mean:0.000}, Streuung {se.relSd * 100:0.00} %, größter Sprung {se.maxRelStep * 100:0.00} %");
        Info($"Nebel-Himmelsanteil {sf.mean:0.00} (Streuung {sf.relSd * 100:0.00} %), Nebelhelligkeit Streuung {sl.relSd * 100:0.00} %, Sonne Streuung {ss.relSd * 100:0.00} %, Umgebung Streuung {sa.relSd * 100:0.00} %");
        long dMin = drawsPerFrame.Count > 1 ? drawsPerFrame.Skip(1).Min() : 0, dMax = drawsPerFrame.Count > 1 ? drawsPerFrame.Skip(1).Max() : 0;
        Info($"Instanz-Draws je Bild: {dMin} … {dMax}");
        float meanStep = camSteps.Count > 1 ? camSteps.Skip(1).Average() : 0f, maxCamStep = camSteps.Count > 1 ? camSteps.Skip(1).Max() : 0f;
        Info($"Kamera: {camChanges} Wechsel, Schritt Ø {meanStep * 100:0.00} cm, größter {maxCamStep * 100:0.00} cm (Ruckler-Bild eingeschlossen)");
        Info($"Reflexionssonde: {ReflectionProbe.Renders - probe0} Neuberechnungen in 20 s, Zeitscheiben {probe?.timeSlicingMode.ToString() ?? "–"}");
        Info($"Renderer neu/entfernt: {bornRenderers}/{deadRenderers}");
        var flicker = toggles.Where(kv => kv.Value >= 4).OrderByDescending(kv => kv.Value).ToList();
        Info($"Umschaltende Renderer/Lichter/Kameras: {toggles.Count} (davon ≥ 4× in 20 s: {flicker.Count})");
        foreach (var kv in flicker.Take(6)) Info($"   {kv.Key.GetType().Name} „{kv.Key.gameObject.name}“: {kv.Value}×");

        if (se.relSd > 0.01f || se.maxRelStep > 0.005f) Fail($"Belichtung schwankt im Menü (Streuung {se.relSd * 100:0.00} %, Sprung {se.maxRelStep * 100:0.00} %)");
        if (sl.maxRelStep > 0.01f || ss.maxRelStep > 0.01f || sa.maxRelStep > 0.01f) Fail("Nebel/Sonne/Umgebungslicht springen im Menü");
        if (camChanges > 0) Fail("Hauptkamera wechselt im Menü " + camChanges + "×");
        if (meanStep > 0 && maxCamStep > meanStep * 60f + 0.05f) Fail($"Kamera springt im Menü ({maxCamStep:0.00} m in einem Bild)");
        if (flicker.Count > 0) Fail(flicker.Count + " Renderer/Lichter/Kameras schalten im Menü wiederholt um (z. B. „" + flicker[0].Key.gameObject.name + "“ " + flicker[0].Value + "×)");
        if (bornRenderers > 50 || deadRenderers > 50) Fail($"Renderer werden im Menü laufend neu erzeugt/entfernt ({bornRenderers}/{deadRenderers})");
        if (dMax - dMin > Math.Max(8, dMax / 5)) Fail($"Instanz-Draws je Bild schwanken stark ({dMin} … {dMax})");
        CollectWarnings();
    }

    /// <summary>Logo-Einflug: Ruckler dürfen die Animation nicht neu starten; nur ein echtes Öffnen tut das.</summary>
    static void LogoClockCheck()
    {
        Begin("Hauptmenü: Logo-Animation bei Rucklern");
        // Bildfolge: 1 s mit 60 Hz, ein Bild 0,8 s, 1 s, ein Bild 1,5 s, 3 s; je Bild zwei OnGUI-Ereignisse (Layout, Repaint)
        var dts = new List<float>();
        for (int i = 0; i < 60; i++) dts.Add(1f / 60f);
        dts.Add(0.8f);
        for (int i = 0; i < 60; i++) dts.Add(1f / 60f);
        dts.Add(1.5f);
        for (int i = 0; i < 180; i++) dts.Add(1f / 60f);

        var clk = new MenuLogoClock();
        clk.Restart();
        int frame = 100; float prev = -1f; bool monotonic = true; float minAlphaAfter = 1f;
        // Alte Logik zum Vergleich: Neustart, wenn seit dem letzten Menü-Bild mehr als 0,5 s vergangen sind
        float now = 50f, lastDraw = -10f; int oldRestarts = 0;
        float elapsed = 0f;
        foreach (var dt in dts)
        {
            frame++; now += dt; elapsed += dt;
            for (int ev = 0; ev < 2; ev++)
            {
                float t = clk.Tick(frame, dt);
                if (t < prev - 1e-6f) monotonic = false;
                prev = t;
                if (now - lastDraw > 0.5f) oldRestarts++;
                lastDraw = now;
            }
            if (elapsed > 4f) for (int i = 0; i < 9; i++) minAlphaAfter = Math.Min(minAlphaAfter, MenuLogoClock.LetterAlpha(prev, i));
        }
        Info($"Logo-Uhr: {clk.Starts} Start(s), Zeit stetig {monotonic}, nach 4 s alle Buchstaben sichtbar {minAlphaAfter >= 0.999f}; alte Logik: {oldRestarts} Start(s) bei denselben Bildern");
        if (clk.Starts != 1) Fail("Logo-Animation startet bei Rucklern neu (" + clk.Starts + " Starts)");
        if (!monotonic) Fail("Logo-Zeit läuft rückwärts");
        if (minAlphaAfter < 0.999f) Fail("Logo nach dem Einflug nicht vollständig sichtbar");
        if (MenuLogoClock.Reveal(prev, 9) < 0.999f) Fail("Einblend-Fortschritt erreicht nicht 1");
        // Echtes Öffnen: Neustart im nächsten Bild bei 0
        clk.Restart();
        float t0 = clk.Tick(frame + 1, 1f / 60f);
        if (clk.Starts != 2 || t0 != 0f) Fail("Restart() startet die Animation nicht neu");
        if (oldRestarts < 3) Fail("Vergleich: alte Logik hätte hier neu starten müssen (Prüfung wirkungslos?)");
    }
}
