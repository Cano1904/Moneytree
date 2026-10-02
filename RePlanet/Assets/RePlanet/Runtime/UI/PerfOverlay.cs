using System;
using RePlanet.Core;
using Unity.Profiling;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Leistungsanzeige oben links (Einstellung „Leistungsanzeige“, Taste F3): FPS aktuell / Minimum und Mittel der letzten
    /// 5 Sekunden, Bildzeit (Mittel/Spitze), Draw-Calls/Batches/SetPass/Dreiecke (über ProfilerRecorder – in manchen
    /// Release-Builds liefert Unity diese Zähler nicht, dann „–“), Speicher, Qualitätsstufe und Auflösung,
    /// dazu Flacker-Diagnose (Neuaufbauten der Schriftatlanten, Starts der Logo-Animation).
    /// Die Bildzeiten werden immer mitgeschrieben (billig); Zähler laufen nur, solange die Anzeige sichtbar ist.
    /// </summary>
    public partial class UIRoot
    {
        const float PerfWindow = 5f;
        readonly float[] perfDt = new float[2048];
        readonly float[] perfAt = new float[2048];
        int perfHead, perfCount;
        float perfNextText;
        string perfText;
        float perfTextH;
        ProfilerRecorder recDraw, recBatches, recSetPass, recTris, recMem;
        bool perfRecording;

        bool PerfVisible(GameApp app)
        {
            if (app == null || app.Settings == null || !app.Settings.ShowFps) return false;
            var s = UIState.Screen;
            // Saubere Fotos: im Fotomodus mit ausgeblendetem HUD keine Anzeige
            if (s == UIScreen.Photo && PhotoMode.HideHud && !photoPanel) return false;
            return true;
        }

        /// <summary>Aus Update: Bildzeit mitschreiben, F3 auswerten, Zähler starten/stoppen.</summary>
        void UpdatePerf(GameApp app)
        {
            float now = Time.unscaledTime;
            perfDt[perfHead] = Time.unscaledDeltaTime;
            perfAt[perfHead] = now;
            perfHead = (perfHead + 1) % perfDt.Length;
            if (perfCount < perfDt.Length) perfCount++;

            if (capturing == null && !UINav.Editing && InputMap.Down(GameAction.PerfOverlay))
            {
                app.Settings.ShowFps = !app.Settings.ShowFps;
                app.Settings.Save();
                AudioManager.Ui("ui_click");
                perfNextText = 0f;
            }
            bool want = PerfVisible(app);
            if (want != perfRecording) SetPerfRecorders(want);
        }

        void SetPerfRecorders(bool on)
        {
            perfRecording = on;
            try
            {
                if (on)
                {
                    recDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                    recBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                    recSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                    recTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                    recMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
                }
                else DisposePerfRecorders();
            }
            catch (Exception e) { Debug.LogWarning("[Leistungsanzeige] Zähler: " + e.Message); }
        }

        void DisposePerfRecorders()
        {
            try
            {
                recDraw.Dispose(); recBatches.Dispose(); recSetPass.Dispose(); recTris.Dispose(); recMem.Dispose();
            }
            catch (Exception) { }
        }

        void OnDisable() { if (perfRecording) { perfRecording = false; DisposePerfRecorders(); } }

        static string Rec(ProfilerRecorder r)
        {
            try { return r.Valid && r.LastValue > 0 ? Loc.Num(r.LastValue) : "–"; }
            catch (Exception) { return "–"; }
        }

        static string Big(long v)
        {
            if (v >= 1000000) return (v / 1000000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " M";
            if (v >= 10000) return (v / 1000f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " k";
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        string BuildPerfText(GameApp app)
        {
            float now = Time.unscaledTime;
            float maxDt = 0f, sum5 = 0f, sumRecent = 0f;
            int n5 = 0, nRecent = 0;
            for (int i = 0; i < perfCount; i++)
            {
                int k = (perfHead - 1 - i + perfDt.Length) % perfDt.Length;
                float age = now - perfAt[k];
                if (age > PerfWindow) break;
                float dt = perfDt[k];
                if (dt <= 0f) continue;
                if (dt > maxDt) maxDt = dt;
                sum5 += dt; n5++;
                if (age <= 0.5f) { sumRecent += dt; nRecent++; }
            }
            float cur = nRecent > 0 && sumRecent > 0 ? nRecent / sumRecent : 0f;
            float avg = n5 > 0 && sum5 > 0 ? n5 / sum5 : 0f;
            float min = maxDt > 0 ? 1f / maxDt : 0f;
            float ms = n5 > 0 ? sum5 / n5 * 1000f : 0f;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Color fc = cur >= 55f ? UISkin.Good : cur >= 30f ? UISkin.Warn : UISkin.Bad;
            Color mc = min >= 50f ? UISkin.Good : min >= 25f ? UISkin.Warn : UISkin.Bad;
            string l1 = "<b>" + UISkin.Col(cur.ToString("0", ci) + " FPS", fc) + "</b>  " + UISkin.Col(Loc.F("min {0} · Ø {1} (5 s)", UISkin.Col(min.ToString("0", ci), mc), avg.ToString("0", ci)), UISkin.TextDim);
            string l2 = Loc.F("Bildzeit {0} ms · Spitze {1} ms", ms.ToString("0.0", ci), (maxDt * 1000f).ToString("0.0", ci));
            string tris = "–";
            try { if (recTris.Valid && recTris.LastValue > 0) tris = Big(recTris.LastValue); } catch (Exception) { }
            string l3 = Loc.F("Draw-Calls {0} · Batches {1} · SetPass {2} · Dreiecke {3}", Rec(recDraw), Rec(recBatches), Rec(recSetPass), tris);
            long mem = 0;
            try { if (recMem.Valid) mem = recMem.LastValue; } catch (Exception) { }
            if (mem <= 0) mem = GC.GetTotalMemory(false);
            string q = "?";
            try
            {
                var names = QualitySettings.names;
                int ql = QualitySettings.GetQualityLevel();
                q = ql >= 0 && ql < names.Length ? names[ql] : ql.ToString(ci);
            }
            catch (Exception) { }
            var s = app.Settings;
            string preset = Loc.T(Settings.QualityNames[Mathf.Clamp(s.Quality, 0, Settings.QualityNames.Length - 1)]);
            string l4 = Loc.F("Qualität {0} (Unity: {1}) · {2}×{3} · Render {4} % · Speicher {5} MB", preset, q, Screen.width, Screen.height,
                Mathf.RoundToInt(s.RenderScale * 100f), (mem / (1024f * 1024f)).ToString("0", ci));
            // Flacker-Diagnose: Neuaufbauten der Schriftatlanten (je einer kann ein Bild mit falschen Glyphen zeigen) und
            // Starts der Logo-Animation im Hauptmenü (mehr als einer pro Öffnen = Neustart)
            string l5 = Loc.F("Schriftatlas neu: {0} · Logo-Starts: {1}", UISkin.FontRebuilds, LogoStarts);
            return l1 + "\n" + UISkin.Col(l2 + "\n" + l3 + "\n" + l4, UISkin.Text) + "\n" + UISkin.Col(l5, UISkin.TextDim);
        }

        /// <summary>Höhe der Anzeige (0 = aus) – das HUD rückt oben links darunter.</summary>
        float PerfOverlayHeight(GameApp app) { return PerfVisible(app) && perfText != null ? perfTextH + 14f : 0f; }

        void DrawPerfOverlay(GameApp app)
        {
            if (!PerfVisible(app)) return;
            if (perfText == null || Time.unscaledTime >= perfNextText)
            {
                perfNextText = Time.unscaledTime + 0.25f;
                perfText = BuildPerfText(app);
                perfTextH = UISkin.TextHeight(UISkin.LabelTiny, perfText, 900f);
            }
            float w = Mathf.Min(VW - 16f, UISkin.TextWidth(UISkin.LabelTiny, perfText) + 20f);
            var r = new Rect(6, 4, w, perfTextH + 10);
            if (Event.current.type == EventType.Repaint) UISkin.RoundRect(r, new Color(0f, 0f, 0f, UISkin.Contrast ? 0.9f : 0.55f));
            GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 16, perfTextH + 2), perfText, UISkin.LabelTiny);
        }
    }
}
