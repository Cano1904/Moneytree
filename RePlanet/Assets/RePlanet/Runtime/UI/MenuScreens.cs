using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Hauptmenü, Neues Spiel, Planetenwahl, Spielstände, Pause, Laden, Meldungen.</summary>
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
            // Mittige Abdunklung hinter Titel und Knopfspalte (stufenloser Verlauf zu den Seiten)
            float bw = Mathf.Min(440f, VW - 80f);
            float cx = VW * 0.5f;
            if (Event.current.type == EventType.Repaint)
            {
                float half = bw * 0.5f + 400f;
                var col = new Color(0.01f, 0.05f, 0.07f, UISkin.Contrast ? 0.7f : 0.42f);
                UISkin.Tex(new Rect(cx - half, 0, half * 2f, VH), ColumnFade(), col);
            }

            float titleH = Mathf.Min(150f, VH * 0.16f);
            float top = Mathf.Max(24f, VH * 0.07f);
            float reveal = DrawLogo(top, titleH);
            DrawTagline(top + titleH + 2f, reveal);

            bool canContinue = mainSlot != null;
            float y = top + titleH + 70f;
            int n = 6; // Fortsetzen, Neues Spiel, Koop, Spielstände, Einstellungen, Beenden
            float bh = Mathf.Clamp((VH - y - 110f) / n - 10f, 34f, 54f);
            float step = bh + 10f;
            float left = cx - bw * 0.5f;

            if (MenuButton(new Rect(left, y, bw, bh), L("Fortsetzen"), canContinue))
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
            if (MenuButton(new Rect(left, y, bw, bh), L("Neues Spiel"))) UIState.Open(UIScreen.NewGame);
            y += step;
            if (MenuButton(new Rect(left, y, bw, bh), L("Koop"))) OpenSub(UIScreen.Coop, UIScreen.MainMenu);
            y += step;
            if (MenuButton(new Rect(left, y, bw, bh), L("Spielstände"))) OpenSub(UIScreen.Saves, UIScreen.MainMenu);
            y += step;
            if (MenuButton(new Rect(left, y, bw, bh), L("Einstellungen"))) OpenSub(UIScreen.Settings, UIScreen.MainMenu);
            y += step;
            if (confirm == "quit")
            {
                float hw = (bw - 10) * 0.5f;
                if (MenuButton(new Rect(left, y, hw, bh), "Ja, beenden", true, true)) app.QuitGame();
                if (MenuButton(new Rect(left + hw + 10, y, hw, bh), "Abbrechen")) confirm = null;
            }
            else if (MenuButton(new Rect(left, y, bw, bh), L("Beenden"))) confirm = "quit";

            // Steuerung und Versionsnummer (klein, unten mittig)
            string ver = "Version " + Application.version;
            string hint = InputMap.UsingPad ? "Steuerkreuz/Stick · A: Bestätigen · B: Zurück" : "Pfeiltasten/Maus · Eingabe: Bestätigen · Esc: Zurück";
            GUI.Label(new Rect(0, VH - 58, VW, 24), UISkin.Col(hint, UISkin.TextDim), SmallCenter());
            GUI.Label(new Rect(0, VH - 34, VW, 24), UISkin.Col(ver, UISkin.TextDim * new Color(1, 1, 1, 0.7f)), SmallCenter());
        }

        // ------------------------------------------------------------------ Hauptmenü: Logo, Unterzeile, Knöpfe
        static Texture2D columnFade;

        /// <summary>Waagerechter Verlauf (Mitte deckend, Ränder weich auslaufend) für die Abdunklung hinter dem Menü.</summary>
        static Texture2D ColumnFade()
        {
            if (columnFade != null) return columnFade;
            const int n = 256;
            columnFade = new Texture2D(n, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n];
            for (int i = 0; i < n; i++)
            {
                float d = Mathf.Abs((i + 0.5f) / n * 2f - 1f);      // 0 Mitte … 1 Rand
                float a = 1f - Mathf.SmoothStep(0.45f, 1f, d);       // innerer Bereich voll, dann weich aus
                px[i] = new Color(1f, 1f, 1f, a);
            }
            columnFade.SetPixels(px);
            columnFade.Apply();
            return columnFade;
        }

        float logoStart = -10f, lastMainDraw = -10f;
        const string LogoWord = "RE:PLANET";

        /// <summary>
        /// Schriftzug „RE:PLANET“ in der Logo-Schrift, gesperrt, mit weichem Türkis-Glühen, Schlagschatten und einer
        /// Lichtkante, die alle 7 s darüber wandert. Beim Öffnen des Menüs fahren die Buchstaben nacheinander ein.
        /// Gibt den Einblend-Fortschritt (0–1) zurück.
        /// </summary>
        float DrawLogo(float top, float h)
        {
            float now = Time.unscaledTime;
            if (now - lastMainDraw > 0.5f) logoStart = now; // Menü (wieder) geöffnet
            lastMainDraw = now;
            float t = now - logoStart;
            float done = Mathf.Clamp01((t - 0.2f) / (0.45f + LogoWord.Length * 0.07f));
            if (Event.current.type != EventType.Repaint) return done;

            var st = UISkin.Logo;
            int size = (int)Mathf.Clamp(h * 0.7f, 44, 112);
            st.fontSize = size;
            float track = size * 0.09f;
            float total = UISkin.TrackedWidth(st, LogoWord, track);
            float x0 = (VW - total) * 0.5f, x = x0;
            float sweep = Mathf.Repeat(now, 7f) / 1.3f - 0.15f; // Lichtkante läuft von links nach rechts, dann Pause
            bool hc = UISkin.Contrast;
            var old = st.normal.textColor;
            for (int i = 0; i < LogoWord.Length; i++)
            {
                string ch = UISkin.Chr(LogoWord[i]);
                float cw = st.CalcSize(UISkin.Tmp(ch)).x;
                float a = Mathf.Clamp01((t - 0.2f - i * 0.07f) / 0.45f);
                a = a * a * (3f - 2f * a);
                var r = new Rect(x, top + (1f - a) * size * 0.2f, cw + 6f, h);
                bool colon = LogoWord[i] == ':';
                float centre = (x - x0 + cw * 0.5f) / Mathf.Max(1f, total);
                float shine = hc ? 0f : Mathf.Clamp01(1f - Mathf.Abs(sweep - centre) * 7f);
                if (!hc)
                {
                    // Glühen: acht leicht versetzte, sehr transparente Kopien
                    Color g = colon ? UISkin.Accent : UISkin.Teal;
                    g.a = (0.045f + 0.07f * shine) * a;
                    st.normal.textColor = g;
                    float rad = size * 0.05f;
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI * 0.25f;
                        GUI.Label(new Rect(r.x + Mathf.Cos(ang) * rad, r.y + Mathf.Sin(ang) * rad, r.width, r.height), ch, st);
                    }
                }
                st.normal.textColor = new Color(0f, 0f, 0f, (hc ? 1f : 0.55f) * a);
                GUI.Label(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), ch, st);
                Color c = colon ? UISkin.Accent : UISkin.Text;
                c = Color.Lerp(c, Color.white, shine * 0.7f);
                c.a = a;
                st.normal.textColor = c;
                GUI.Label(r, ch, st);
                x += cw + track;
            }
            st.normal.textColor = old;
            return done;
        }

        /// <summary>Unterzeile „EINE ZWEITE CHANCE“, weit gesperrt, zwischen zwei zur Mitte hin aufleuchtenden Linien.</summary>
        void DrawTagline(float y, float reveal)
        {
            if (Event.current.type != EventType.Repaint || reveal <= 0f) return;
            var st = UISkin.Tagline;
            st.fontSize = (int)Mathf.Clamp(VH * 0.021f, 15, 24);
            string text = L("Eine zweite Chance").ToUpperInvariant();
            float track = st.fontSize * 0.45f;
            float w = UISkin.TrackedWidth(st, text, track);
            float h = st.fontSize * 1.8f;
            float x = (VW - w) * 0.5f;
            float a = reveal * reveal;
            var tc = UISkin.Contrast ? UISkin.Text : Color.Lerp(UISkin.TextDim, UISkin.Text, 0.35f);
            tc.a = a;
            UISkin.Tracked(x + 1.5f, y + 2f, h, text, st, track, new Color(0, 0, 0, 0.5f * a));
            UISkin.Tracked(x, y, h, text, st, track, tc);
            // Linien links und rechts (außen transparent, zur Schrift hin kräftiger), mit Akzentpunkt
            float len = Mathf.Min(240f, VW * 0.15f) * reveal, gap = st.fontSize * 1.1f, cy = y + h * 0.5f;
            const int seg = 16;
            for (int i = 0; i < seg; i++)
            {
                float f = (i + 1f) / seg;
                var lc = UISkin.Teal; lc.a = 0.6f * f * a;
                float sw = len / seg;
                UISkin.Rect(new Rect(x - gap - len + i * sw, cy - 0.75f, sw + 0.5f, 1.5f), lc);
                UISkin.Rect(new Rect(x + w + gap + len - (i + 1) * sw, cy - 0.75f, sw + 0.5f, 1.5f), lc);
            }
            var dc = UISkin.Accent; dc.a = a;
            UISkin.Tex(new Rect(x - gap + 2f, cy - 3f, 6f, 6f), UISkin.Circle, dc);
            UISkin.Tex(new Rect(x + w + gap - 8f, cy - 3f, 6f, 6f), UISkin.Circle, dc);
        }

        /// <summary>Hauptmenü-Knopf: Großbuchstaben in Exo 2, bei Maus/Fokus orange Akzentleisten links und rechts.</summary>
        bool MenuButton(Rect r, string text, bool enabled = true, bool warm = false)
        {
            bool clicked = UINav.Button(r, text.ToUpperInvariant(), enabled, warm ? UISkin.MenuButtonSel : UISkin.MenuButton, UISkin.MenuButtonOff);
            if (enabled && Event.current.type == EventType.Repaint && (UINav.IsHover(r) || (UINav.LastFocused && UINav.KeyboardMode)))
            {
                var c = UISkin.Accent;
                UISkin.Rect(new Rect(r.x + 2f, r.y + 7f, 3f, r.height - 14f), c);
                UISkin.Rect(new Rect(r.xMax - 5f, r.y + 7f, 3f, r.height - 14f), c);
                c.a = 0.18f;
                UISkin.Rect(new Rect(r.x + 5f, r.y + 7f, 10f, r.height - 14f), c);
                UISkin.Rect(new Rect(r.xMax - 15f, r.y + 7f, 10f, r.height - 14f), c);
            }
            return clicked;
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

        GUIStyle psTitle, psName;

        /// <summary>
        /// Planetenwahl über der Weltall-Szene (<see cref="PlanetSelectScene"/>): schlanke Liste links (Name, Untertitel),
        /// darunter Stimmung und Beschreibung des gewählten Planeten. Auswahl (Maus, Pfeiltasten, Controller) lässt die Kamera
        /// zum Planeten gleiten; Bestätigen startet den Anflug. Ohne Szene: bisherige Karten.
        /// </summary>
        void DrawPlanetSelect(GameApp app)
        {
            var scene = PlanetSelectScene.I;
            if (scene == null || !scene.Active) { DrawPlanetSelectCards(app); return; }
            if (scene.Descending)
            {
                GUI.Label(new Rect(VW - 330, VH - 44, 310, 28), UISkin.Col("Beliebige Taste: überspringen", new Color(1, 1, 1, 0.5f)), SmallRight());
                return;
            }
            if (psTitle == null || psTitle.fontSize != UISkin.H2.fontSize + 6)
            {
                psTitle = new GUIStyle(UISkin.H2) { fontSize = UISkin.H2.fontSize + 6, alignment = TextAnchor.MiddleLeft };
                psName = new GUIStyle(UISkin.H1) { alignment = TextAnchor.MiddleLeft };
            }
            // weicher Schatten links hinter der Liste
            if (Event.current.type == EventType.Repaint)
                for (int i = 0; i < 12; i++)
                    UISkin.Rect(new Rect(i * 48f, 0, 48f, VH), new Color(0.005f, 0.015f, 0.035f, (UISkin.Contrast ? 0.85f : 0.55f) * (1f - i / 12f)));

            float x = Mathf.Max(40f, VW * 0.045f);
            float y = Mathf.Max(28f, VH * 0.06f);
            float lw = Mathf.Min(430f, VW * 0.34f);
            UISkin.Shadow(new Rect(x, y, VW * 0.6f, 48), "Wohin fliegt MIKO zuerst?", psTitle);
            y += 46;
            GUI.Label(new Rect(x + 2, y, VW * 0.6f, 26), UISkin.Col("Startplanet wählen – die anderen erreichst du später mit dem Transportschiff.", UISkin.TextDim), UISkin.LabelSmall);
            y += 48;

            var list = new List<PlanetDef>(5);
            foreach (var id in GameData.PlanetOrder) list.Add(GameData.Planets[id]);
            string pick = null;
            for (int i = 0; i < list.Count; i++)
            {
                var pd = list[i];
                bool locked = !pd.StartPlanet;
                var r = new Rect(x, y, lw, 58);
                bool focused;
                bool click = UINav.Area(r, out focused);
                bool hover = UINav.IsHover(r);
                if ((focused && UINav.KeyboardMode) || hover) pick = pd.Id;
                bool sel = scene.Focused == pd.Id;
                var accent = UISkin.FromRgb(pd.Accent);
                if (Event.current.type == EventType.Repaint)
                {
                    if (sel || hover || focused) UISkin.RoundRect(r, new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.3f, sel ? 0.75f : 0.45f));
                    if (sel) UISkin.RoundRect(new Rect(r.x, r.y + 8, 4, r.height - 16), accent);
                    if (focused && UINav.KeyboardMode) UISkin.OutlineRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UISkin.FocusCol);
                }
                UISkin.Tex(new Rect(r.x + 18, r.y + 17, 24, 24), UISkin.Circle, locked ? accent * new Color(0.5f, 0.5f, 0.5f, 1f) : accent);
                GUI.Label(new Rect(r.x + 54, r.y + 4, r.width - 150, 28), UISkin.Col("<b>" + pd.Name + "</b>", locked ? UISkin.TextDim : sel ? accent : UISkin.Text), UISkin.Label);
                GUI.Label(new Rect(r.x + 54, r.y + 30, r.width - 64, 24), UISkin.Col(pd.Subtitle, UISkin.TextDim), UISkin.LabelTiny);
                if (locked) GUI.Label(new Rect(r.xMax - 110, r.y + 4, 100, 28), UISkin.Col("später", UISkin.Story), SmallRight());
                if (click)
                {
                    if (locked) { AudioManager.Ui("beep_error"); scene.Focus(pd.Id); }
                    else { AudioManager.Ui("ui_click"); scene.Descend(pd.Id); return; }
                }
                y += 64;
            }
            if (pick == null && UINav.KeyboardMode && UINav.Focus >= 0 && UINav.Focus < list.Count) pick = list[UINav.Focus].Id;
            if (pick != null) scene.Focus(pick);

            // Angaben zum gewählten Planeten
            PlanetDef cur = null;
            if (scene.Focused != null && GameData.Planets.TryGetValue(scene.Focused, out cur))
            {
                y += 14;
                var accent = UISkin.FromRgb(cur.Accent);
                UISkin.Shadow(new Rect(x, y, lw + 200, 46), UISkin.Col(cur.Name, accent), psName);
                y += 46;
                float mh = UISkin.TextHeight(UISkin.WrapSmall, cur.Mood, lw);
                GUI.Label(new Rect(x, y, lw, mh + 4), UISkin.Col(cur.Mood, UISkin.Warn), UISkin.WrapSmall);
                y += mh + 8;
                string desc = cur.StartPlanet ? cur.Description : (string.IsNullOrEmpty(cur.UnlockHint) ? "Das Finale – wird später erreichbar." : cur.UnlockHint);
                float dh = UISkin.TextHeight(UISkin.WrapSmall, desc, lw);
                float room = VH - 150f - y;
                if (dh > room) dh = Mathf.Max(0f, room);
                if (dh > 10f) GUI.Label(new Rect(x, y, lw, dh + 4), UISkin.Col(desc, UISkin.Text), UISkin.WrapSmall);
                y += dh + 6;
            }

            float by = VH - 84f;
            bool canLand = cur != null && cur.StartPlanet;
            if (UINav.Button(new Rect(x, by, lw * 0.62f, 50), canLand ? "Hier landen ›" : "Noch gesperrt", canLand, UISkin.ButtonSel) && canLand) { scene.Descend(cur.Id); return; }
            if (UINav.Button(new Rect(x + lw * 0.62f + 12, by, lw * 0.38f - 12, 50), "‹ " + L("Zurück"))) Back();
        }

        void DrawPlanetSelectCards(GameApp app)
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
