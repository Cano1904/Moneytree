using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Einstellungen mit Reitern: Grafik, Audio, Steuerung (inkl. Tastenbelegung), Barrierefreiheit, Sonstiges.</summary>
    public partial class UIRoot
    {
        int settingsTab;
        static readonly string[] SettingsTabs = { "Grafik", "Audio", "Steuerung", "Barrierefreiheit", "Sonstiges" };
        GameAction? capturing;
        float captureStart;
        string captureMsg;
        bool settingsDirty, settingsSaveDirty;
        float settingsApplyAt;
        // Auflösung/Fenstermodus werden erst per „Übernehmen“ gesetzt
        int pendingRes = -1, pendingWindow = -1;
        List<Vector2Int> resList;
        string[] resNames;
        string portText;

        static readonly string[] AaNames = { "Aus", "2× MSAA", "4× MSAA", "8× MSAA" };
        static readonly string[] ParticleNames = { "Wenig", "Mittel", "Viel" };

        static readonly GameAction[] BindOrder =
        {
            GameAction.MoveForward, GameAction.MoveBack, GameAction.MoveLeft, GameAction.MoveRight, GameAction.Sprint,
            GameAction.Interact, GameAction.UseTool, GameAction.AltTool, GameAction.ToolNext, GameAction.ToolPrev,
            GameAction.Tool1, GameAction.Tool2, GameAction.Tool3, GameAction.Tool4, GameAction.Tool5, GameAction.Tool6, GameAction.Tool7,
            GameAction.Press, GameAction.Vehicle, GameAction.VehicleReset, GameAction.DiveUp, GameAction.DiveDown,
            GameAction.Sleep, GameAction.Shelter, GameAction.Emote,
            GameAction.Menu, GameAction.Inventory, GameAction.Missions, GameAction.Map, GameAction.Build, GameAction.RotateBuild,
            GameAction.Photo, GameAction.QuickSave, GameAction.Pause, GameAction.Radio, GameAction.PerfOverlay,
        };

        static readonly string[,] PadTable =
        {
            { "Linker Stick", "Bewegen / im Menü: Auswahl" },
            { "Rechter Stick", "Kamera" },
            { "RT", "Werkzeug benutzen" },
            { "LT", "Magnet aufladen / Zweitfunktion" },
            { "A", "Interagieren · Auftauchen · im Menü: Bestätigen" },
            { "B", "Abtauchen · im Menü: Zurück" },
            { "X", "Pressen" },
            { "Y", "Ein-/Aussteigen · Fotomodus: Panel ein/aus" },
            { "LB / RB", "Werkzeug wechseln · im Menü: Reiter wechseln" },
            { "Back", "Karte" },
            { "Start", "Pause" },
            { "L3 (Stick drücken)", "Sprinten" },
            { "R3 (Stick drücken)", "Fotomodus" },
            { "Steuerkreuz ↑", "Spielmenü" },
            { "Steuerkreuz →", "Bauansicht" },
            { "Steuerkreuz ↓", "Schlafen" },
            { "Steuerkreuz ←", "Roboterlaut" },
            { "Bauansicht", "A platzieren · Y drehen · X umsetzen · Back abreißen · LB/RB Bauwerk · LT/RT Kategorie · B abbrechen" },
        };

        void EnsureResolutions()
        {
            if (resList != null) return;
            resList = new List<Vector2Int> { Vector2Int.zero };
            try
            {
                foreach (var r in Screen.resolutions)
                {
                    var v = new Vector2Int(r.width, r.height);
                    if (!resList.Contains(v)) resList.Add(v);
                }
            }
            catch (Exception) { }
            resList.Sort((a, b) => a == Vector2Int.zero ? -1 : b == Vector2Int.zero ? 1 : (a.x * 10000 + a.y).CompareTo(b.x * 10000 + b.y));
            resNames = new string[resList.Count];
            for (int i = 0; i < resList.Count; i++) resNames[i] = resList[i] == Vector2Int.zero ? L("Bildschirm (") + Screen.currentResolution.width + " × " + Screen.currentResolution.height + ")" : resList[i].x + " × " + resList[i].y;
        }

        int CurrentResIndex(Settings s)
        {
            EnsureResolutions();
            var v = new Vector2Int(s.ResWidth, s.ResHeight);
            int i = resList.IndexOf(v);
            return i < 0 ? 0 : i;
        }

        void MarkSettings()
        {
            settingsDirty = true;
            settingsSaveDirty = true;
        }

        /// <summary>Übernimmt Änderungen gedrosselt (Regler ziehen erzeugt viele Änderungen) und speichert beim Loslassen.</summary>
        void UpdateSettingsApply(GameApp app)
        {
            if (settingsDirty && Time.unscaledTime >= settingsApplyAt)
            {
                settingsDirty = false;
                settingsApplyAt = Time.unscaledTime + 0.1f;
                try { app.Settings.Apply(); } catch (Exception e) { Debug.LogWarning(e.Message); }
            }
            if (settingsSaveDirty && !settingsDirty && !Input.GetMouseButton(0) && !UINav.Editing)
            {
                settingsSaveDirty = false;
                app.Settings.Save();
            }
        }

        void UpdateCapture(GameApp app)
        {
            if (capturing == null) return;
            if (Time.unscaledTime - captureStart < 0.2f) return;
            var k = InputMap.CaptureKey();
            if (k == KeyCode.None) return;
            var a = capturing.Value;
            capturing = null;
            if (k == KeyCode.Escape) { captureMsg = L("Abgebrochen."); AudioManager.Ui("ui_back"); return; }
            // Wer verliert die Taste?
            string lost = null;
            foreach (GameAction other in Enum.GetValues(typeof(GameAction)))
                if (other != a && InputMap.Get(other) == k && other != GameAction.RotateBuild && a != GameAction.RotateBuild) { string n; lost = InputMap.Names.TryGetValue(other, out n) ? L(n) : other.ToString(); }
            InputMap.Rebind(a, k);
            app.Settings.Save();
            string an; InputMap.Names.TryGetValue(a, out an); an = L(an);
            captureMsg = "„" + (an ?? a.ToString()) + "“ → " + InputMap.KeyName(k) + (lost != null ? UISkin.Col("   · „" + lost + L("“ ist jetzt nicht belegt."), UISkin.Warn) : "");
            AudioManager.Ui("ui_click");
        }

        void DrawSettings(GameApp app)
        {
            Backdrop(app);
            var s = app.Settings;
            var r = CenterRect(1220, 900);
            var inner = Window(r, L("Einstellungen"));
            string[] tabNames = new string[SettingsTabs.Length];
            for (int i = 0; i < tabNames.Length; i++) tabNames[i] = L(SettingsTabs[i]);
            GUI.Label(new Rect(inner.x, inner.y - 4, 60, 40), UISkin.Col(InputMap.UsingPad ? "LB" : "Q", UISkin.TextDim), UISkin.LabelSmall);
            GUI.Label(new Rect(inner.xMax - 40, inner.y - 4, 40, 40), UISkin.Col(InputMap.UsingPad ? "RB" : "E", UISkin.TextDim), UISkin.LabelSmall);
            int nt = UINav.Tabs(new Rect(inner.x + 40, inner.y - 4, inner.width - 80, 42), settingsTab, tabNames);
            if (nt != settingsTab) { settingsTab = nt; UINav.ResetScroll(201); }
            var view = new Rect(inner.x, inner.y + 50, inner.width, inner.height - 50);
            const int key = 201;
            UINav.BeginScroll(key, view);
            float w = UINav.ScrollWidth(key, view);
            float y = 4;
            switch (settingsTab)
            {
                case 0: y = SettingsGraphics(app, s, w, y); break;
                case 1: y = SettingsAudio(app, s, w, y); break;
                case 2: y = SettingsControls(app, s, w, y); break;
                case 3: y = SettingsAccess(app, s, w, y); break;
                default: y = SettingsMisc(app, s, w, y); break;
            }
            UINav.EndScroll(y + 10);
        }

        const float RowH = 46f, RowStep = 52f;

        float Heading(string text, float w, float y)
        {
            GUI.Label(new Rect(4, y + 6, w, 32), text, UISkin.H3);
            return y + 44;
        }

        float Note(string text, float w, float y)
        {
            float h = UISkin.TextHeight(UISkin.WrapSmall, text, w - 20);
            GUI.Label(new Rect(10, y, w - 20, h + 4), text, UISkin.WrapSmall);
            return y + h + 10;
        }

        float SettingsGraphics(GameApp app, Settings s, float w, float y)
        {
            EnsureResolutions();
            int q = UINav.Choice(new Rect(0, y, w, RowH), L("Qualität"), s.Quality, Settings.QualityNames);
            if (q != s.Quality) { s.ApplyPreset(q); MarkSettings(); }
            y += RowStep;
            y = Note(L("Die Qualitätsstufe setzt Schatten, Kantenglättung, Sichtweite, Partikel und Render-Skalierung. Einzelwerte lassen sich danach anpassen."), w, y);

            y = Heading(L("Anzeige"), w, y);
            if (pendingWindow < 0) pendingWindow = s.WindowMode;
            if (pendingRes < 0) pendingRes = CurrentResIndex(s);
            pendingWindow = UINav.Choice(new Rect(0, y, w, RowH), L("Fenstermodus"), pendingWindow, Settings.WindowModeNames);
            y += RowStep;
            pendingRes = UINav.Choice(new Rect(0, y, w, RowH), L("Auflösung"), pendingRes, resNames);
            y += RowStep;
            bool changed = pendingWindow != s.WindowMode || pendingRes != CurrentResIndex(s);
            if (UINav.Button(new Rect(w * 0.42f, y, 260, 40), L("Übernehmen"), changed, UISkin.ButtonSmall))
            {
                var v = resList[Mathf.Clamp(pendingRes, 0, resList.Count - 1)];
                s.ResWidth = v.x; s.ResHeight = v.y; s.WindowMode = pendingWindow;
                s.Apply(); s.Save();
                Hud.Show(L("Anzeige übernommen."), ToastKind.Info, 2f);
            }
            if (changed) GUI.Label(new Rect(w * 0.42f + 276, y, w * 0.58f - 280, 40), UISkin.Col(L("Noch nicht übernommen"), UISkin.Warn), UISkin.LabelSmall);
            else if (Application.isEditor) GUI.Label(new Rect(w * 0.42f + 276, y, w * 0.58f - 280, 40), L("Im Editor ohne Wirkung"), UISkin.LabelSmall);
            y += RowStep;
            bool vs = UINav.Toggle(new Rect(0, y, w, RowH), s.VSync, "VSync");
            if (vs != s.VSync) { s.VSync = vs; MarkSettings(); }
            y += RowStep;
            string[] fpsNames = new string[Settings.FpsOptions.Length];
            int fi = 0;
            for (int i = 0; i < fpsNames.Length; i++) { fpsNames[i] = Settings.FpsOptions[i] == 0 ? L("Unbegrenzt") : Settings.FpsOptions[i] + " FPS"; if (Settings.FpsOptions[i] == s.FpsLimit) fi = i; }
            int nfi = UINav.Choice(new Rect(0, y, w, RowH), L("Bildrate begrenzen"), fi, fpsNames);
            if (nfi != fi) { s.FpsLimit = Settings.FpsOptions[nfi]; MarkSettings(); }
            y += RowStep;
            if (s.VSync && s.FpsLimit > 0) y = Note(L("Hinweis: Die Begrenzung wirkt nur bei ausgeschaltetem VSync."), w, y);

            y = Heading(L("Bildqualität"), w, y);
            int sh = UINav.Choice(new Rect(0, y, w, RowH), L("Schatten"), Mathf.Clamp(s.Shadows, 0, 3), Settings.ShadowNames);
            if (sh != s.Shadows) { s.Shadows = sh; MarkSettings(); }
            y += RowStep;
            int ai = Mathf.Max(0, Array.IndexOf(Settings.AaOptions, s.AntiAliasing));
            int nai = UINav.Choice(new Rect(0, y, w, RowH), L("Kantenglättung"), ai, AaNames);
            if (nai != ai) { s.AntiAliasing = Settings.AaOptions[nai]; MarkSettings(); }
            y += RowStep;
            float vd = UINav.Slider(new Rect(0, y, w, RowH), L("Sichtweite"), s.ViewDistance, 0.5f, 1.5f, 0.05f, (s.ViewDistance * 100).ToString("0") + " %");
            if (Mathf.Abs(vd - s.ViewDistance) > 1e-4f) { s.ViewDistance = vd; MarkSettings(); }
            y += RowStep;
            float rs = UINav.Slider(new Rect(0, y, w, RowH), L("Render-Skalierung"), s.RenderScale, 0.5f, 1f, 0.05f, (s.RenderScale * 100).ToString("0") + " %");
            if (Mathf.Abs(rs - s.RenderScale) > 1e-4f) { s.RenderScale = rs; MarkSettings(); }
            y += RowStep;
            int pa = UINav.Choice(new Rect(0, y, w, RowH), L("Partikel"), Mathf.Clamp(s.Particles, 0, 2), ParticleNames);
            if (pa != s.Particles) { s.Particles = pa; MarkSettings(); }
            y += RowStep;
            float br = UINav.Slider(new Rect(0, y, w, RowH), L("Helligkeit"), s.Brightness, 0.7f, 1.3f, 0.05f, (s.Brightness * 100).ToString("0") + " %");
            if (Mathf.Abs(br - s.Brightness) > 1e-4f) { s.Brightness = br; MarkSettings(); }
            y += RowStep;
            float fov = UINav.Slider(new Rect(0, y, w, RowH), L("Sichtfeld"), s.Fov, 45f, 90f, 1f, s.Fov.ToString("0") + "°");
            if (Mathf.Abs(fov - s.Fov) > 1e-4f) { s.Fov = fov; MarkSettings(); }
            y += RowStep;
            bool sf = UINav.Toggle(new Rect(0, y, w, RowH), s.ShowFps, L("Leistungsanzeige") + " [" + InputMap.Label(GameAction.PerfOverlay) + "]");
            if (sf != s.ShowFps) { s.ShowFps = sf; MarkSettings(); }
            y += RowStep;
            y = Note(L("Oben links: Bildrate (aktuell, Minimum und Mittel der letzten 5 Sekunden), Bildzeit, Draw-Calls, Qualitätsstufe und Auflösung."), w, y);
            bool ds = UINav.Toggle(new Rect(0, y, w, RowH), s.DetailShaders, "Detail-Shader (experimentell, wirkt nach Neustart)");
            if (ds != s.DetailShaders) { s.DetailShaders = ds; MarkSettings(); }
            y += RowStep;
            return y;
        }

        float VolumeSlider(string label, float v, float w, float y, out float nv)
        {
            nv = UINav.Slider(new Rect(0, y, w, RowH), label, v, 0f, 1f, 0.05f, (v * 100).ToString("0") + " %");
            return y + RowStep;
        }

        float SettingsAudio(GameApp app, Settings s, float w, float y)
        {
            float nv;
            y = VolumeSlider(L("Gesamtlautstärke"), s.MasterVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.MasterVolume) > 1e-4f) { s.MasterVolume = nv; MarkSettings(); }
            y = VolumeSlider(L("Musik"), s.MusicVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.MusicVolume) > 1e-4f) { s.MusicVolume = nv; MarkSettings(); }
            y = VolumeSlider(L("Effekte"), s.SfxVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.SfxVolume) > 1e-4f) { s.SfxVolume = nv; MarkSettings(); }
            y = VolumeSlider(L("Umgebung"), s.AmbientVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.AmbientVolume) > 1e-4f) { s.AmbientVolume = nv; MarkSettings(); }
            y = VolumeSlider(L("Oberfläche"), s.UiVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.UiVolume) > 1e-4f) { s.UiVolume = nv; MarkSettings(); }
            y = VolumeSlider(L("Stimmen & Roboterlaute"), s.VoiceVolume, w, y, out nv);
            if (Mathf.Abs(nv - s.VoiceVolume) > 1e-4f) { s.VoiceVolume = nv; MarkSettings(); }
            bool mu = UINav.Toggle(new Rect(0, y, w, RowH), s.MuteWhenUnfocused, L("Stumm, wenn das Spiel nicht im Vordergrund ist"));
            if (mu != s.MuteWhenUnfocused) { s.MuteWhenUnfocused = mu; MarkSettings(); }
            y += RowStep;
            bool nig = UINav.Toggle(new Rect(0, y, w, RowH), s.NarratorInGame, L("Erzähler im Spiel"));
            if (nig != s.NarratorInGame) { s.NarratorInGame = nig; if (!nig) Narrator.ClearGameLines(); MarkSettings(); }
            y += RowStep;
            y = Note(L("Der Erzähler spricht zu besonderen Momenten einen kurzen Satz – jeder nur einmal pro Spielstand. Ohne Aufnahme erscheint der Satz als Untertitel."), w, y);
            return y;
        }

        float SettingsControls(GameApp app, Settings s, float w, float y)
        {
            float ms = UINav.Slider(new Rect(0, y, w, RowH), L("Mausempfindlichkeit"), s.MouseSensitivity, 0.1f, 4f, 0.05f, s.MouseSensitivity.ToString("0.00") + "×");
            if (Mathf.Abs(ms - s.MouseSensitivity) > 1e-4f) { s.MouseSensitivity = ms; MarkSettings(); }
            y += RowStep;
            float ps = UINav.Slider(new Rect(0, y, w, RowH), L("Controller-Empfindlichkeit"), s.PadSensitivity, 0.1f, 4f, 0.05f, s.PadSensitivity.ToString("0.00") + "×");
            if (Mathf.Abs(ps - s.PadSensitivity) > 1e-4f) { s.PadSensitivity = ps; MarkSettings(); }
            y += RowStep;
            bool iy = UINav.Toggle(new Rect(0, y, w, RowH), s.InvertY, L("Y-Achse umkehren"));
            if (iy != s.InvertY) { s.InvertY = iy; MarkSettings(); }
            y += RowStep;
            bool ha = UINav.Toggle(new Rect(0, y, w, RowH), s.HoldActions, L("Werkzeuge halten statt umschalten"));
            if (ha != s.HoldActions) { s.HoldActions = ha; MarkSettings(); }
            y += RowStep;
            y = Note(s.HoldActions ? L("Werkzeug wirkt, solange die Taste gehalten wird.") : L("Einmal drücken startet das Werkzeug, erneut drücken stoppt es (schont die Hände)."), w, y);

            y = Heading(L("Tasten belegen") + L(" (Tastatur & Maus)"), w, y);
            if (capturing != null)
            {
                string an; InputMap.Names.TryGetValue(capturing.Value, out an); an = L(an);
                GUI.Label(new Rect(10, y, w - 20, 34), UISkin.Col(L("Neue Taste für „") + an + L("“ drücken … (Esc bricht ab)"), UISkin.Accent), UISkin.LabelBold);
                y += 40;
            }
            else if (!string.IsNullOrEmpty(captureMsg))
            {
                GUI.Label(new Rect(10, y, w - 20, 34), captureMsg, UISkin.LabelSmall);
                y += 40;
            }
            float colW = (w - 20) * 0.5f;
            for (int i = 0; i < BindOrder.Length; i++)
            {
                var a = BindOrder[i];
                int col = i % 2;
                float x = col * (colW + 20);
                if (col == 0 && i > 0) y += 46;
                string name; InputMap.Names.TryGetValue(a, out name); name = L(name);
                GUI.Label(new Rect(x + 10, y, colW * 0.55f, 40), name ?? a.ToString(), UISkin.Label);
                bool isCap = capturing != null && capturing.Value == a;
                string keyText = isCap ? "…" : InputMap.KeyName(InputMap.Get(a));
                if (UINav.Button(new Rect(x + colW * 0.56f, y + 2, colW * 0.44f, 38), keyText, capturing == null || isCap, isCap ? UISkin.ButtonSel : UISkin.ButtonSmall))
                {
                    capturing = a;
                    captureStart = Time.unscaledTime;
                    captureMsg = null;
                    UINav.CancelPending();
                }
            }
            y += 52;
            if (UINav.Button(new Rect(0, y, 320, 42), L("Standard wiederherstellen"), capturing == null, UISkin.ButtonSmall))
            {
                InputMap.ResetDefaults();
                s.Save();
                captureMsg = L("Standardbelegung wiederhergestellt.");
            }
            y += 54;
            y = Note(L("„Bauwerk drehen“ darf dieselbe Taste wie eine andere Aktion nutzen – es wirkt nur in der Bauansicht. Menüs lassen sich immer mit Pfeiltasten, Eingabe und Esc bedienen; im Spielmenü wechseln Q/E die Reiter."), w, y);

            y = Heading(L("Controller (feste Belegung)"), w, y);
            int rows = PadTable.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                var rr = new Rect(0, y, w, 34);
                if (i % 2 == 0) UISkin.RoundRect(rr, new Color(1, 1, 1, 0.04f));
                UISkin.KeyCap(12, y + 5, L(PadTable[i, 0]), 24);
                GUI.Label(new Rect(w * 0.3f, y, w * 0.7f, 34), L(PadTable[i, 1]), UISkin.LabelSmall);
                y += 36;
            }
            y += 6;
            y = Note(L("Controller werden über Unitys klassischen Input Manager (XInput) gelesen. Tastatur/Maus und Controller können jederzeit gewechselt werden – Hinweise im Spiel passen sich an."), w, y);
            return y;
        }

        float SettingsAccess(GameApp app, Settings s, float w, float y)
        {
            int hi = UINav.Choice(new Rect(0, y, w, RowH), L("Hinweise"), Mathf.Clamp(s.Hints, 0, 2), Settings.HintNames);
            if (hi != s.Hints) { s.Hints = hi; MarkSettings(); }
            y += RowStep;
            y = Note(s.Hints == Settings.HintsOff ? L("Keine Tasten- und Tipp-Hinweise; nur Warnungen und wichtige Meldungen.")
                : s.Hints == Settings.HintsMinimal ? L("Ruhiges HUD: Tastensymbol und ein Wort direkt am Objekt, kurze Meldungen. Das Ziel blendet sich aus – mit [") + InputMap.Label(GameAction.Missions) + L("] oder bei einem neuen Ziel kommt es zurück.")
                : L("Alle Hinweise als ausführlicher Text (Tastenhilfe, Ziel dauerhaft, vollständige Meldungen)."), w, y);
            bool st = UINav.Toggle(new Rect(0, y, w, RowH), s.Subtitles, L("Untertitel"));
            if (st != s.Subtitles) { s.Subtitles = st; MarkSettings(); }
            y += RowStep;
            float ts = UINav.Slider(new Rect(0, y, w, RowH), L("Textgröße"), s.TextScale, 0.8f, 1.6f, 0.1f, (s.TextScale * 100).ToString("0") + " %");
            if (Mathf.Abs(ts - s.TextScale) > 1e-4f) { s.TextScale = ts; MarkSettings(); }
            y += RowStep;
            bool cs = UINav.Toggle(new Rect(0, y, w, RowH), s.CameraShake, L("Kamerawackeln"));
            if (cs != s.CameraShake) { s.CameraShake = cs; MarkSettings(); }
            y += RowStep;
            bool hc = UINav.Toggle(new Rect(0, y, w, RowH), s.HighContrast, L("Hoher Kontrast"));
            if (hc != s.HighContrast) { s.HighContrast = hc; MarkSettings(); }
            y += RowStep;
            bool rf = UINav.Toggle(new Rect(0, y, w, RowH), s.ReduceFlashing, L("Blitze und Lichtblitze reduzieren"));
            if (rf != s.ReduceFlashing) { s.ReduceFlashing = rf; MarkSettings(); }
            y += RowStep;
            y = Note(L("Materialien werden immer mit Form-Symbol und Farbe gezeigt (z. B. ◯ Glas, △ Kunststoff, ⬡ Metall), damit sie auch ohne Farbsehen unterscheidbar sind. Hoher Kontrast nutzt schwarze Flächen, weiße Schrift und gelbe Fokusrahmen."), w, y);
            return y;
        }

        float SettingsMisc(GameApp app, Settings s, float w, float y)
        {
            int li = Mathf.Max(0, Array.IndexOf(Loc.Languages, s.Language));
            int nli = UINav.Choice(new Rect(0, y, w, RowH), L("Sprache") + L(" / Language"), li, Loc.LanguageNames);
            if (nli != li) { s.Language = Loc.Languages[nli]; Loc.Lang = s.Language; Loc.ApplyToData(); BuildMode.Category = null; MarkSettings(); }
            y += RowStep;
            GUI.Label(new Rect(10, y, w * 0.4f, RowH), L("Spielername"), UISkin.Label);
            string nm = UINav.TextField(new Rect(w * 0.42f, y + 2, w * 0.58f - 6, RowH - 4), s.PlayerName, 20, "set_name");
            if (nm != s.PlayerName) { s.PlayerName = nm; settingsSaveDirty = true; }
            y += RowStep;
            y = Note(L("Der Name ist für Mitspieler sichtbar. Er gilt ab der nächsten Sitzung."), w, y);
            if (portText == null) portText = s.CoopPort.ToString();
            GUI.Label(new Rect(10, y, w * 0.4f, RowH), L("Koop-Port (TCP)"), UISkin.Label);
            string pt = UINav.TextField(new Rect(w * 0.42f, y + 2, 200, RowH - 4), portText, 5, "set_port");
            if (pt != portText)
            {
                portText = pt;
                int p;
                if (int.TryParse(pt, out p) && p >= 1024 && p <= 65535) { s.CoopPort = p; settingsSaveDirty = true; }
            }
            int pp;
            bool portOk = int.TryParse(portText, out pp) && pp >= 1024 && pp <= 65535;
            GUI.Label(new Rect(w * 0.42f + 216, y, w * 0.58f - 220, RowH), portOk ? L("Aktiv: ") + s.CoopPort : UISkin.Col(L("Bitte 1024–65535 eingeben"), UISkin.Warn), UISkin.LabelSmall);
            y += RowStep;
            y = Note(L("Für Spiele über das Internet muss dieser TCP-Port am Router des Hosts weitergeleitet werden (oder ein VPN wie Tailscale/ZeroTier genutzt werden)."), w, y);
            y = Heading(L("Ordner"), w, y);
            y = PathRow(L("Spielstände"), app.SaveDir, w, y);
            y = PathRow(L("Fotos"), app.PhotoDir, w, y);
            y = PathRow(L("Einstellungen"), Application.persistentDataPath, w, y);
            return y;
        }

        float PathRow(string label, string path, float w, float y)
        {
            GUI.Label(new Rect(10, y, 180, 40), label, UISkin.Label);
            GUI.Label(new Rect(190, y, w - 380, 40), path, UISkin.LabelSmall);
            if (UINav.Button(new Rect(w - 180, y + 2, 170, 36), L("Kopieren"), true, UISkin.ButtonSmall))
            {
                GUIUtility.systemCopyBuffer = path;
                Hud.Show(L("Pfad kopiert."), ToastKind.Info, 2f);
            }
            return y + 46;
        }
    }
}
