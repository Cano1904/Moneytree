using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Hauptmenü, Neues Spiel, Planetenwahl, Spielstände, Pause, Mitwirkende, Laden, Meldungen.</summary>
    public partial class UIRoot
    {
        // ------------------------------------------------------------ Zustand
        string mainSlot;
        SlotInfo mainSlotInfo;
        float mainRefresh;
        List<SlotInfo> savesCache = new List<SlotInfo>();
        string savesMsg; bool savesMsgError;
        string ngName = "Meine Welt";
        int ngSlot;
        SlotInfo ngExisting;
        int ngExistingFor = -1;
        string planetPreviewed;
        readonly Dictionary<string, Texture2D> gradients = new Dictionary<string, Texture2D>();
        static readonly string[] SlotLabels = { "Automatisch", "Spielstand 1", "Spielstand 2", "Spielstand 3" };

        void RefreshMainMenu()
        {
            var app = GameApp.I;
            if (app == null || app.Saves == null) return;
            try
            {
                mainSlot = app.Saves.MostRecentSlot();
                mainSlotInfo = mainSlot != null ? app.Saves.Info(mainSlot) : null;
            }
            catch (System.Exception e) { mainSlot = null; mainSlotInfo = null; Debug.LogWarning(e.Message); }
            mainRefresh = Time.unscaledTime + 3f;
        }

        void RefreshSaves()
        {
            var app = GameApp.I;
            savesCache.Clear();
            if (app == null || app.Saves == null) return;
            try
            {
                foreach (var slot in SaveStore.Slots)
                {
                    var info = app.Saves.Info(slot);
                    savesCache.Add(info ?? new SlotInfo { Slot = slot });
                }
            }
            catch (System.Exception e) { savesMsg = e.Message; savesMsgError = true; }
        }

        // ================================================================== Hauptmenü
        void DrawMainMenu(GameApp app)
        {
            if (Time.unscaledTime > mainRefresh) RefreshMainMenu();
            Vignette();
            // Mittige Abdunklung hinter Titel und Knopfspalte (weich zu den Seiten)
            float bw = Mathf.Min(440f, VW - 80f);
            float cx = VW * 0.5f;
            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < 10; i++)
                {
                    float a = (UISkin.Contrast ? 0.7f : 0.36f) * (1f - i / 10f);
                    float half = bw * 0.5f + 40f + i * 36f;
                    UISkin.Rect(new Rect(cx - half, 0, 36f, VH), new Color(0.01f, 0.05f, 0.07f, a));
                    UISkin.Rect(new Rect(cx + half - 36f, 0, 36f, VH), new Color(0.01f, 0.05f, 0.07f, a));
                }
                UISkin.Rect(new Rect(cx - bw * 0.5f - 40f + 36f, 0, bw + 80f - 72f, VH), new Color(0.01f, 0.05f, 0.07f, UISkin.Contrast ? 0.7f : 0.36f));
            }

            float titleH = Mathf.Min(150f, VH * 0.16f);
            var ts = UISkin.Title;
            int oldSize = ts.fontSize;
            ts.fontSize = (int)Mathf.Clamp(titleH * 0.8f, 48, 120);
            ts.alignment = TextAnchor.MiddleCenter;
            float top = Mathf.Max(24f, VH * 0.07f);
            UISkin.Shadow(new Rect(0, top, VW, titleH), UISkin.Col("RE", UISkin.Text) + UISkin.Col(":", UISkin.Accent) + UISkin.Col("PLANET", UISkin.Text), ts);
            ts.fontSize = oldSize;
            var sub = UISkin.Subtitle;
            sub.alignment = TextAnchor.MiddleCenter;
            UISkin.Shadow(new Rect(0, top + titleH - 6, VW, 44), "Eine zweite Chance", sub);

            bool canContinue = mainSlot != null;
            float y = top + titleH + 64f;
            int n = 7;
            float bh = Mathf.Clamp((VH - y - 110f) / n - 10f, 34f, 54f);
            float step = bh + 10f;
            float left = cx - bw * 0.5f;

            if (UINav.Button(new Rect(left, y, bw, bh), L("Fortsetzen"), canContinue))
            {
                app.Continue(mainSlot);
            }
            y += step;
            if (canContinue && mainSlotInfo != null)
            {
                string info = mainSlotInfo.Error != null ? UISkin.Col(mainSlotInfo.Error, UISkin.Warn)
                    : (mainSlotInfo.World ?? "Welt") + " · " + mainSlotInfo.Planet + " · " + FormatTime(mainSlotInfo.Playtime);
                GUI.Label(new Rect(0, y - 8, VW, 24), UISkin.Col(info, UISkin.TextDim), SmallCenter());
                y += 18f;
            }
            if (UINav.Button(new Rect(left, y, bw, bh), L("Neues Spiel"))) UIState.Open(UIScreen.NewGame);
            y += step;
            if (UINav.Button(new Rect(left, y, bw, bh), L("Koop"))) OpenSub(UIScreen.Coop, UIScreen.MainMenu);
            y += step;
            if (UINav.Button(new Rect(left, y, bw, bh), L("Spielstände"))) OpenSub(UIScreen.Saves, UIScreen.MainMenu);
            y += step;
            if (UINav.Button(new Rect(left, y, bw, bh), L("Einstellungen"))) OpenSub(UIScreen.Settings, UIScreen.MainMenu);
            y += step;
            if (UINav.Button(new Rect(left, y, bw, bh), "Mitwirkende")) UIState.Open(UIScreen.Credits);
            y += step;
            if (confirm == "quit")
            {
                float hw = (bw - 10) * 0.5f;
                if (UINav.Button(new Rect(left, y, hw, bh), "Ja, beenden", true, UISkin.ButtonSel)) app.QuitGame();
                if (UINav.Button(new Rect(left + hw + 10, y, hw, bh), "Abbrechen")) confirm = null;
            }
            else if (UINav.Button(new Rect(left, y, bw, bh), L("Beenden"))) confirm = "quit";

            // Versionshinweis und Steuerung (klein, unten mittig)
            string ver = "Version " + Application.version + " · Unity " + Application.unityVersion + " · Inspiriert von WALL·E – eigene Figuren und Welten";
            string hint = InputMap.UsingPad ? "Steuerkreuz/Stick · A: Bestätigen · B: Zurück" : "Pfeiltasten/Maus · Eingabe: Bestätigen · Esc: Zurück";
            GUI.Label(new Rect(0, VH - 58, VW, 24), UISkin.Col(hint, UISkin.TextDim), SmallCenter());
            GUI.Label(new Rect(0, VH - 34, VW, 24), UISkin.Col(ver, UISkin.TextDim * new Color(1, 1, 1, 0.7f)), SmallCenter());
        }

        // ================================================================== Neues Spiel
        void OnNewGameOpened()
        {
            var app = GameApp.I;
            if (app == null) return;
            if (string.IsNullOrEmpty(ngName)) ngName = "Meine Welt";
            // Freien Slot vorschlagen
            ngSlot = 0;
            try
            {
                for (int i = 1; i < SaveStore.Slots.Length; i++)
                    if (app.Saves.Info(SaveStore.Slots[i]) == null) { ngSlot = i; break; }
            }
            catch (System.Exception) { }
            ngExistingFor = -1;
        }

        void DrawNewGame(GameApp app)
        {
            Vignette();
            var r = CenterRect(760, 460);
            var inner = Window(r, L("Neues Spiel"));
            float y = inner.y + 6;
            GUI.Label(new Rect(inner.x, y, inner.width, 30), "Name der Welt", UISkin.LabelSmall);
            y += 32;
            ngName = UINav.TextField(new Rect(inner.x, y, inner.width, 44), ngName, 32, "ng_name");
            y += 60;
            ngSlot = UINav.Choice(new Rect(inner.x, y, inner.width, 46), "Speicherplatz", ngSlot, SlotLabels);
            y += 54;
            if (ngExistingFor != ngSlot)
            {
                ngExistingFor = ngSlot;
                try { ngExisting = app.Saves.Info(SaveStore.Slots[ngSlot]); } catch (System.Exception) { ngExisting = null; }
            }
            string warn;
            if (ngExisting != null)
                warn = UISkin.Col("⚠ Überschreibt den vorhandenen Stand: ", UISkin.Warn) + (ngExisting.World ?? "?") + " · " + ngExisting.Planet + " · " + FormatTime(ngExisting.Playtime)
                    + " · " + Num(ngExisting.Credits) + " Credits" + (string.IsNullOrEmpty(ngExisting.Saved) ? "" : " · gespeichert " + ngExisting.Saved);
            else warn = UISkin.Col("Dieser Speicherplatz ist frei.", UISkin.Good);
            GUI.Label(new Rect(inner.x + 10, y, inner.width - 20, 60), warn, UISkin.WrapSmall);
            y += 66;
            float bw = (inner.width - 14) * 0.5f;
            var go = new Rect(inner.x, inner.yMax - 56, bw, 52);
            if (UINav.Button(go, "Los geht's!", true, UISkin.ButtonSel))
            {
                app.BeginNewGame(string.IsNullOrEmpty(ngName) ? "Meine Welt" : ngName.Trim(), SaveStore.Slots[ngSlot], true);
            }
            if (UINav.Button(new Rect(go.xMax + 14, go.y, bw, 52), L("Zurück"))) Back();
        }

        // ================================================================== Planetenwahl
        Texture2D Gradient(PlanetDef pd)
        {
            Texture2D t;
            if (gradients.TryGetValue(pd.Id, out t) && t != null) return t;
            t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var top = UISkin.FromRgb(pd.SkyTop);
            var hor = UISkin.FromRgb(pd.SkyHorizon);
            var gr = UISkin.FromRgb(pd.Ground);
            for (int i = 0; i < 64; i++)
            {
                float f = i / 63f; // 0 unten, 1 oben
                Color c = f > 0.35f ? Color.Lerp(hor, top, (f - 0.35f) / 0.65f) : Color.Lerp(gr * 0.6f, hor, f / 0.35f);
                c.a = 0.92f;
                t.SetPixel(0, i, c);
            }
            t.Apply();
            gradients[pd.Id] = t;
            return t;
        }

        void DrawPlanetSelect(GameApp app)
        {
            Vignette();
            UISkin.Shadow(new Rect(0, Mathf.Max(20, VH * 0.05f), VW, 60), "Wohin fliegt MIKO zuerst?", UISkin.H1);
            GUI.Label(new Rect(0, Mathf.Max(20, VH * 0.05f) + 56, VW, 30), "Wähle den Startplaneten – die anderen erreichst du später mit dem Transportschiff.", UISkin.LabelCenter);

            var list = new List<PlanetDef>(3);
            foreach (var id in GameData.PlanetOrder) { var pd = GameData.Planets[id]; if (pd.StartPlanet) list.Add(pd); }
            int n = Mathf.Max(1, list.Count);
            float gap = 26f;
            float cw = Mathf.Min(500f, (VW - 80f - gap * (n - 1)) / n);
            float top = Mathf.Max(20, VH * 0.05f) + 110f;
            float ch = Mathf.Min(640f, VH - top - 150f);
            float x0 = (VW - (cw * n + gap * (n - 1))) * 0.5f;
            string focusedPlanet = null;
            for (int i = 0; i < list.Count; i++)
            {
                var pd = list[i];
                var cr = new Rect(x0 + i * (cw + gap), top, cw, ch);
                bool focused;
                bool click = UINav.Area(cr, out focused);
                bool hover = UINav.IsHover(cr);
                if ((focused && UINav.KeyboardMode) || hover) focusedPlanet = pd.Id;
                DrawPlanetCard(cr, pd, focused || hover);
                if (click) { app.StartNewWorld(pd.Id); return; }
            }
            if (focusedPlanet == null && list.Count > 0 && UINav.Focus < list.Count) focusedPlanet = list[Mathf.Clamp(UINav.Focus, 0, list.Count - 1)].Id;
            if (focusedPlanet != null && focusedPlanet != planetPreviewed && Event.current.type == EventType.Repaint)
            {
                planetPreviewed = focusedPlanet;
                if (WorldView.I != null) WorldView.I.PreviewPlanet(focusedPlanet);
            }

            GUI.Label(new Rect(0, top + ch + 16, VW, 30), UISkin.Col("NIVALIS – das Finale – wird später erreichbar.", UISkin.Story), UISkin.LabelCenter);
            if (UINav.Button(new Rect(VW * 0.5f - 110, VH - 72, 220, 48), "‹ " + L("Zurück"))) Back();
        }

        void DrawPlanetCard(Rect r, PlanetDef pd, bool highlight)
        {
            var accent = UISkin.FromRgb(pd.Accent);
            if (Event.current.type == EventType.Repaint)
            {
                UISkin.Sliced(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UISkin.Round, highlight ? accent : new Color(accent.r, accent.g, accent.b, 0.35f));
                GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Gradient(pd), ScaleMode.StretchToFill, true);
                UISkin.Rect(new Rect(r.x + 2, r.y + r.height * 0.42f, r.width - 4, r.height * 0.58f - 2), new Color(0.02f, 0.06f, 0.08f, UISkin.Contrast ? 0.95f : 0.78f));
                // Planetenkugel
                float ps = Mathf.Min(r.width * 0.42f, r.height * 0.28f);
                var pr = new Rect(r.center.x - ps * 0.5f, r.y + r.height * 0.21f - ps * 0.5f, ps, ps);
                UISkin.Tex(new Rect(pr.x - ps * 0.12f, pr.y - ps * 0.12f, ps * 1.24f, ps * 1.24f), UISkin.Circle, new Color(accent.r, accent.g, accent.b, 0.25f));
                UISkin.Tex(pr, UISkin.Circle, UISkin.FromRgb(pd.Ground));
                UISkin.Tex(new Rect(pr.x + ps * 0.12f, pr.y + ps * 0.08f, ps * 0.5f, ps * 0.45f), UISkin.Circle, new Color(1, 1, 1, 0.14f));
                UISkin.Tex(new Rect(pr.x + ps * 0.45f, pr.y + ps * 0.5f, ps * 0.4f, ps * 0.35f), UISkin.Circle, UISkin.FromRgb(pd.Ground2, 0.8f));
            }
            float y = r.y + r.height * 0.42f + 12;
            var h = UISkin.H1;
            h.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(r.x + 22, y, r.width - 44, 44), UISkin.Col(pd.Name, accent), h);
            h.alignment = TextAnchor.MiddleCenter;
            y += 44;
            GUI.Label(new Rect(r.x + 22, y, r.width - 44, 28), pd.Subtitle, UISkin.LabelBold);
            y += 32;
            float th = UISkin.TextHeight(UISkin.WrapSmall, pd.Mood, r.width - 44);
            GUI.Label(new Rect(r.x + 22, y, r.width - 44, th + 4), UISkin.Col(pd.Mood, UISkin.Warn), UISkin.WrapSmall);
            y += th + 8;
            float dh = UISkin.TextHeight(UISkin.WrapSmall, pd.Description, r.width - 44);
            GUI.Label(new Rect(r.x + 22, y, r.width - 44, dh + 4), pd.Description, UISkin.WrapSmall);
            y += dh + 10;
            for (int a = 0; a < 3 && y < r.yMax - 60; a++)
            {
                UISkin.Tex(new Rect(r.x + 24, y + 7, 12, 12), UISkin.Shape("dot"), accent);
                GUI.Label(new Rect(r.x + 44, y, r.width - 66, 26), pd.AreaNames[a], UISkin.LabelSmall);
                y += 26;
            }
            var btn = new Rect(r.x + 22, r.yMax - 54, r.width - 44, 40);
            if (Event.current.type == EventType.Repaint)
            {
                UISkin.Sliced(btn, highlight ? UISkin.BtnSel : UISkin.Btn);
                GUI.Label(btn, highlight ? "Hier landen ›" : "Auswählen", UISkin.LabelCenter);
            }
        }

        // ================================================================== Spielstände
        void DrawSaves(GameApp app)
        {
            Backdrop(app);
            var r = CenterRect(1180, 820);
            var inner = Window(r, L("Spielstände"));
            bool inGame = app.InGame;
            bool host = inGame && app.IsHost;
            float y = inner.y;
            GUI.Label(new Rect(inner.x, y, inner.width - 180, 28), "Ordner: " + app.SaveDir, UISkin.LabelSmall);
            if (UINav.Button(new Rect(inner.xMax - 170, y - 2, 170, 32), "Pfad kopieren", true, UISkin.ButtonSmall))
            {
                GUIUtility.systemCopyBuffer = app.SaveDir;
                Hud.Show("Pfad kopiert.", ToastKind.Info, 2f);
            }
            y += 36;
            if (!string.IsNullOrEmpty(savesMsg))
            {
                GUI.Label(new Rect(inner.x, y, inner.width, 44), UISkin.Col(savesMsg, savesMsgError ? UISkin.Bad : UISkin.Good), UISkin.WrapSmall);
                y += 46;
            }
            if (inGame && !host)
                GUI.Label(new Rect(inner.x, y, inner.width, 28), UISkin.Col("Als Gast kannst du nicht speichern – die Welt gehört dem Host. Laden beendet die Koop-Sitzung.", UISkin.Warn), UISkin.LabelSmall);
            else if (inGame)
                GUI.Label(new Rect(inner.x, y, inner.width, 28), "Aktueller Speicherplatz: " + GameApp.SlotName(app.Slot) + ". Beim Laden eines anderen Stands wird die laufende Welt vorher gespeichert.", UISkin.LabelSmall);
            y += 34;

            var view = new Rect(inner.x, y, inner.width, inner.yMax - y);
            const int key = 101;
            UINav.BeginScroll(key, view);
            float w = UINav.ScrollWidth(key, view);
            float cy = 0;
            foreach (var info in savesCache)
            {
                cy += DrawSaveRow(app, new Rect(0, cy, w, 0), info, inGame, host) + 12;
            }
            UINav.EndScroll(cy);
        }

        float DrawSaveRow(GameApp app, Rect r, SlotInfo info, bool inGame, bool host)
        {
            bool exists = info.World != null || info.Error != null || info.HasBackup;
            float h = 132f;
            r.height = h;
            UISkin.PanelBoxLight(r);
            bool current = inGame && app.Slot == info.Slot;
            GUI.Label(new Rect(r.x + 18, r.y + 8, 260, 30), GameApp.SlotName(info.Slot) + (current ? UISkin.Col("  (aktuell)", UISkin.Accent) : ""), UISkin.H3);
            if (!exists)
            {
                GUI.Label(new Rect(r.x + 18, r.y + 44, r.width - 300, 30), UISkin.Col("(leer)", UISkin.TextDim), UISkin.Label);
            }
            else
            {
                string line1 = (info.World ?? "Welt") + (string.IsNullOrEmpty(info.Planet) ? "" : " · " + info.Planet);
                GUI.Label(new Rect(r.x + 18, r.y + 42, r.width - 360, 28), line1, UISkin.LabelBold);
                string line2 = "Spielzeit " + FormatTime(info.Playtime) + " · " + Num(info.Credits) + " Credits · Wiederherstellung " + (info.Restoration * 100).ToString("0") + " %"
                    + (info.Campaign ? " · Kampagne abgeschlossen" : "");
                GUI.Label(new Rect(r.x + 18, r.y + 70, r.width - 360, 26), line2, UISkin.LabelSmall);
                string line3 = (string.IsNullOrEmpty(info.Saved) ? "" : "Gespeichert: " + info.Saved) + (info.HasBackup ? "   · Backup vorhanden" : "");
                if (info.Error != null) line3 += "   " + UISkin.Col("⚠ " + info.Error, UISkin.Warn);
                GUI.Label(new Rect(r.x + 18, r.y + 96, r.width - 360, 26), line3, UISkin.LabelSmall);
            }

            // Knöpfe rechts
            float bx = r.xMax - 330, bw = 150, bh = 36;
            string delKey = "del:" + info.Slot;
            if (confirm == delKey)
            {
                GUI.Label(new Rect(bx, r.y + 8, 310, 30), UISkin.Col("Wirklich löschen?", UISkin.Warn), UISkin.LabelBold);
                if (UINav.Button(new Rect(bx, r.y + 44, bw, bh), "Ja, löschen", true, UISkin.ButtonSel))
                {
                    confirm = null;
                    bool ok = app.Saves.Delete(info.Slot);
                    savesMsg = ok ? GameApp.SlotName(info.Slot) + " gelöscht." : "Löschen fehlgeschlagen.";
                    savesMsgError = !ok;
                    RefreshSaves(); RefreshMainMenu();
                }
                if (UINav.Button(new Rect(bx + bw + 10, r.y + 44, bw, bh), "Abbrechen")) confirm = null;
                return h;
            }
            string loadKey = "load:" + info.Slot;
            if (confirm == loadKey)
            {
                GUI.Label(new Rect(bx, r.y + 8, 320, 30), UISkin.Col(host ? "Laufende Welt speichern und wechseln?" : "Sitzung verlassen und laden?", UISkin.Warn), UISkin.LabelSmall);
                if (UINav.Button(new Rect(bx, r.y + 44, bw, bh), "Ja, laden", true, UISkin.ButtonSel))
                {
                    confirm = null;
                    if (host) app.SaveNow(null, true);
                    app.Continue(info.Slot);
                }
                if (UINav.Button(new Rect(bx + bw + 10, r.y + 44, bw, bh), "Abbrechen")) confirm = null;
                return h;
            }
            bool loadable = exists && (info.World != null || info.HasBackup);
            if (UINav.Button(new Rect(bx, r.y + 10, bw, bh), L("Laden"), loadable && !current, UISkin.ButtonSmall))
            {
                if (inGame) confirm = loadKey;
                else app.Continue(info.Slot);
            }
            if (UINav.Button(new Rect(bx + bw + 10, r.y + 10, bw, bh), "Exportieren", exists && info.World != null, UISkin.ButtonSmall))
            {
                string err;
                var path = app.Saves.Export(info.Slot, Path.Combine(Application.persistentDataPath, "exports"), out err);
                if (path != null) { savesMsg = "Exportiert nach: " + path; savesMsgError = false; }
                else { savesMsg = "Export fehlgeschlagen: " + err; savesMsgError = true; }
            }
            if (inGame && host)
            {
                if (UINav.Button(new Rect(bx, r.y + 52, bw, bh), "Hier speichern", true, UISkin.ButtonSmall))
                {
                    if (app.SaveNow(info.Slot)) { app.Slot = info.Slot; savesMsg = "Gespeichert in " + GameApp.SlotName(info.Slot) + "."; savesMsgError = false; }
                    RefreshSaves();
                }
            }
            if (UINav.Button(new Rect(bx + bw + 10, r.y + 52, bw, bh), "Löschen", exists && !current, UISkin.ButtonSmall)) confirm = delKey;
            return h;
        }

        // ================================================================== Pause
        void DrawPause(GameApp app)
        {
            Dim(0.5f);
            bool host = app.IsHost;
            int n = 8;
            float bh = Mathf.Clamp((VH - 260) / n - 10, 36, 52);
            float h = 110 + n * (bh + 10) + 60;
            var r = CenterRect(520, h);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 50), L("Pause"), UISkin.H1);
            if (app.CoopActive)
                GUI.Label(new Rect(r.x, r.y + 64, r.width, 28), UISkin.Col("Koop: das Spiel läuft weiter", UISkin.Warn), UISkin.LabelCenter);
            else if (app.InGame)
                GUI.Label(new Rect(r.x, r.y + 64, r.width, 28), "Die Welt steht still.", UISkin.LabelCenter);
            float y = r.y + 104, x = r.x + 40, w = r.width - 80;
            float step = bh + 10;

            if (confirm == "menu" || confirm == "quit")
            {
                string q = confirm == "menu"
                    ? (host ? "Zurück ins Hauptmenü? Die Welt wird gespeichert" + (app.CoopActive ? " und die Mitspieler werden getrennt." : ".") : "Die Koop-Sitzung verlassen?")
                    : (host ? "Spiel beenden? Die Welt wird vorher gespeichert." : "Spiel beenden und die Sitzung verlassen?");
                float th = UISkin.TextHeight(UISkin.WrapCenter, q, w);
                GUI.Label(new Rect(x, y, w, th + 6), q, UISkin.WrapCenter);
                y += th + 20;
                if (UINav.Button(new Rect(x, y, w, bh), confirm == "menu" ? "Ja, zum Hauptmenü" : "Ja, beenden", true, UISkin.ButtonSel))
                {
                    if (confirm == "menu") { confirm = null; app.LeaveToMenu(); }
                    else app.QuitGame();
                }
                y += step;
                if (UINav.Button(new Rect(x, y, w, bh), "Abbrechen")) confirm = null;
                return;
            }

            if (UINav.Button(new Rect(x, y, w, bh), L("Fortsetzen"), true, UISkin.ButtonSel)) { UIState.Open(UIScreen.None); AudioManager.Ui("ui_back"); }
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Speichern") + (host ? " (" + GameApp.SlotName(app.Slot) + ")" : " – nur Host"), host)) app.SaveNow();
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Spielstände"))) OpenSub(UIScreen.Saves, UIScreen.Pause);
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Koop"))) { coopError = null; OpenSub(UIScreen.Coop, UIScreen.Pause); }
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Einstellungen"))) OpenSub(UIScreen.Settings, UIScreen.Pause);
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Fotomodus") + "  " + KeyHint(GameAction.Photo))) EnterPhoto();
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Hauptmenü"))) confirm = "menu";
            y += step;
            if (UINav.Button(new Rect(x, y, w, bh), L("Beenden"))) confirm = "quit";
        }

        // ================================================================== Mitwirkende
        void DrawCredits(GameApp app)
        {
            Vignette();
            var r = CenterRect(900, 760);
            var inner = Window(r, "Mitwirkende");
            string[] lines =
            {
                "<b>RE:PLANET – Eine zweite Chance</b>",
                "",
                UISkin.Col("Idee & Auftrag", UISkin.Accent),
                "Ein Spiel über Aufräumen, Reparieren und Hoffnung – für alle, die Dinge lieber retten als wegwerfen.",
                "",
                UISkin.Col("Inspiration", UISkin.Accent),
                "Inspiriert von WALL·E (Pixar) – eigene Figuren und Welten. MIKO und alle Planeten sind eigenständige Schöpfungen.",
                "",
                UISkin.Col("Technik", UISkin.Accent),
                "Unity · prozedurale Grafik (Gelände, Himmel, Modelle, Texturen) · prozedurale Audiosynthese (Effekte, Musik, Intro)",
                "Serverautoritative Simulation für Solo und Online-Koop (1–4 Spieler)",
                "",
                UISkin.Col("Inhalte", UISkin.Accent),
                "Alle Inhalte – Modelle, Klänge, Musik, Texte – sind eigen erzeugt. Es werden keine fremden Assets verwendet.",
                "",
                UISkin.Col("Danke fürs Spielen!", UISkin.Good),
            };
            float y = inner.y + 4;
            foreach (var l in lines)
            {
                if (l.Length == 0) { y += 12; continue; }
                float th = UISkin.TextHeight(UISkin.Wrap, l, inner.width);
                GUI.Label(new Rect(inner.x, y, inner.width, th + 4), l, UISkin.Wrap);
                y += th + 4;
            }
        }

        // ================================================================== Laden & Meldungen
        void DrawLoading(GameApp app)
        {
            if (Event.current.type == EventType.Repaint) UISkin.Rect(new Rect(0, 0, VW, VH), new Color(0.02f, 0.07f, 0.09f, 0.72f));
            var r = CenterRect(700, 170);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 40), string.IsNullOrEmpty(app.LoadingText) ? "Laden …" : app.LoadingText, UISkin.LabelCenter);
            var bar = new Rect(r.x + 50, r.y + 90, r.width - 100, 16);
            UISkin.Bar(bar, 0, UISkin.Teal);
            if (Event.current.type == EventType.Repaint)
            {
                float t = Mathf.Repeat(Time.unscaledTime * 0.6f, 1.3f) - 0.3f;
                float x0 = Mathf.Clamp01(t), x1 = Mathf.Clamp01(t + 0.3f);
                if (x1 > x0) UISkin.RoundRect(new Rect(bar.x + bar.width * x0, bar.y, bar.width * (x1 - x0), bar.height), UISkin.Teal);
            }
            GUI.Label(new Rect(r.x, r.y + 120, r.width, 30), "MIKO fährt die Systeme hoch …", UISkin.LabelSmall);
        }

        void DrawMessage(GameApp app)
        {
            Backdrop(app);
            string text = UIState.MessageText ?? "";
            float th = UISkin.TextHeight(UISkin.WrapCenter, text, 660);
            var r = CenterRect(740, Mathf.Min(VH - 60, th + 200));
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x, r.y + 18, r.width, 44), UIState.MessageTitle ?? "Hinweis", UISkin.H1);
            GUI.Label(new Rect(r.x + 40, r.y + 80, r.width - 80, th + 10), text, UISkin.WrapCenter);
            if (UINav.Button(new Rect(r.center.x - 110, r.yMax - 70, 220, 50), "OK", true, UISkin.ButtonSel))
                UIState.Open(UIState.ReturnTo);
        }
    }
}
