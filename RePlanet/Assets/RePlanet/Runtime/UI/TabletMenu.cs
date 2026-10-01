using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Ein Reiter („App“) auf MIKOs Feldtablet. Die Reiterleiste ist datengetrieben: eigene Reiter lassen sich mit
    /// <see cref="UIRoot.RegisterMenuTab"/> hinzufügen und erscheinen automatisch im Tablet (Symbol, Beschriftung, Q/E, LB/RB).
    /// </summary>
    public sealed class MenuTabDef
    {
        /// <summary>Kennung (z. B. „inventory“, auch <see cref="UIState.MenuTab"/>).</summary>
        public string Id;
        /// <summary>Deutscher Name (Schlüssel für <see cref="Loc.T"/>; Englisch in LocEn*.cs eintragen).</summary>
        public string Name;
        /// <summary>Symbolform für <see cref="UISkin.Shape"/> (z. B. „star“, „book“, „trophy“).</summary>
        public string Icon;
        /// <summary>Zeichnet den Inhalt in den Bildschirmbereich (virtuelle Koordinaten).</summary>
        public Action<UIRoot, GameApp, Rect> Draw;
        /// <summary>Optional: Reiter nur zeigen, wenn true (null = immer).</summary>
        public Func<GameApp, bool> Visible;
    }

    /// <summary>
    /// Spielmenü als MIKOs robustes Feldtablet: Gehäuse mit Gummiecken, Schrauben, Kamera und Status-LEDs, Bildschirm mit
    /// Glasreflex, sehr leichten Scanlinien/Rauschen und Leuchten, Statusleiste (Planet, Uhrzeit, Signal, Credits, Akku),
    /// App-Reiterleiste mit prozeduralen Symbolen, Öffnen (Hochgleiten + Hochfahren), Schließen und Reiterwechsel animiert.
    /// Hoher Kontrast: schlicht (schwarz/weiß/gelb), ohne Effekte. Alle Texturen stammen aus <see cref="UISkin"/>.
    /// </summary>
    public partial class UIRoot
    {
        // ------------------------------------------------------------ Reiter (datengetrieben)
        static readonly string[] MenuTabs = { "inventory", "missions", "map", "workshop", "storage", "archive", "achievements", "robot", "radio", "coop" };
        static readonly string[] MenuTabNames = { "Inventar", "Aufträge", "Karte", "Werkstatt", "Lager", "Archiv", "Erfolge", "Roboter", "Radio", "Koop" };
        static readonly string[] MenuTabIcons = { "bag", "list", "pin", "wrench", "crate", "book", "trophy", "robot", "radio", "people" };

        static readonly List<MenuTabDef> extraTabs = new List<MenuTabDef>();
        static readonly List<string> extraAfter = new List<string>();
        static int tabsVersion;
        int tabsBuilt = -1;
        readonly List<MenuTabDef> allTabs = new List<MenuTabDef>(16);
        readonly List<MenuTabDef> visibleTabs = new List<MenuTabDef>(16);

        /// <summary>
        /// Weiteren Reiter anmelden (z. B. aus einer neuen partial-Datei per [RuntimeInitializeOnLoadMethod]).
        /// after = Kennung des Reiters, hinter dem er erscheint (null = vor „Koop“ am Ende). Gleiche Kennung ersetzt.
        /// </summary>
        public static void RegisterMenuTab(string id, string germanName, string icon, Action<UIRoot, GameApp, Rect> draw, string after = null, Func<GameApp, bool> visible = null)
        {
            if (string.IsNullOrEmpty(id) || draw == null) return;
            for (int i = extraTabs.Count - 1; i >= 0; i--)
                if (extraTabs[i].Id == id) { extraTabs.RemoveAt(i); extraAfter.RemoveAt(i); }
            extraTabs.Add(new MenuTabDef { Id = id, Name = germanName ?? id, Icon = icon ?? "dot", Draw = draw, Visible = visible });
            extraAfter.Add(after);
            tabsVersion++;
        }

        List<MenuTabDef> AllMenuTabs()
        {
            if (tabsBuilt == tabsVersion && allTabs.Count > 0) return allTabs;
            tabsBuilt = tabsVersion;
            allTabs.Clear();
            for (int i = 0; i < MenuTabs.Length; i++)
            {
                string id = MenuTabs[i];
                allTabs.Add(new MenuTabDef { Id = id, Name = MenuTabNames[i], Icon = MenuTabIcons[i], Draw = (ui, app, c) => ui.DrawBuiltinTab(id, app, c) });
            }
            for (int i = 0; i < extraTabs.Count; i++)
            {
                var d = extraTabs[i];
                bool builtin = false;
                for (int k = 0; k < allTabs.Count; k++) if (allTabs[k].Id == d.Id) { allTabs[k] = d; builtin = true; break; }
                if (builtin) continue;
                int at = -1;
                if (extraAfter[i] != null) for (int k = 0; k < allTabs.Count; k++) if (allTabs[k].Id == extraAfter[i]) { at = k + 1; break; }
                if (at < 0) { at = allTabs.Count; for (int k = 0; k < allTabs.Count; k++) if (allTabs[k].Id == "coop") { at = k; break; } }
                allTabs.Insert(at, d);
            }
            return allTabs;
        }

        /// <summary>Sichtbare Reiter dieses Bildes (Liste wird wiederverwendet – keine Speicheranforderung).</summary>
        List<MenuTabDef> VisibleMenuTabs(GameApp app)
        {
            var all = AllMenuTabs();
            visibleTabs.Clear();
            foreach (var d in all)
            {
                bool vis = true;
                if (d.Visible != null) { try { vis = d.Visible(app); } catch (Exception) { vis = true; } }
                if (vis) visibleTabs.Add(d);
            }
            return visibleTabs;
        }

        int TabIndex(List<MenuTabDef> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return -1;
        }

        void SetMenuTab(string t)
        {
            var list = GameApp.I != null ? VisibleMenuTabs(GameApp.I) : AllMenuTabs();
            if (TabIndex(list, t) < 0) t = list.Count > 0 ? list[0].Id : "inventory";
            if (menuTab != t)
            {
                int a = TabIndex(list, menuTab), b = TabIndex(list, t);
                tabSwitchDir = a < 0 || b < 0 ? 0 : b > a ? 1 : -1;
                tabSwitchAt = Time.unscaledTime;
                UINav.ResetFocus();
                AudioManager.Ui("ui_click");
            }
            menuTab = t;
            UIState.MenuTab = t;
            confirm = null;
        }

        void CycleMenuTab(int d)
        {
            var list = GameApp.I != null ? VisibleMenuTabs(GameApp.I) : AllMenuTabs();
            if (list.Count == 0) return;
            int i = TabIndex(list, menuTab);
            if (i < 0) i = 0;
            SetMenuTab(list[(i + d + list.Count) % list.Count].Id);
        }

        void DrawBuiltinTab(string id, GameApp app, Rect content)
        {
            switch (id)
            {
                case "inventory": TabInventory(app, content); break;
                case "missions": TabMissions(app, content); break;
                case "map":
                    {
                        float side = Mathf.Min(380f, content.width * 0.3f);
                        float ms = Mathf.Min(content.height, content.width - side - 30);
                        DrawMap(app, new Rect(content.x, content.y, ms, ms));
                        DrawMapSide(app, new Rect(content.x + ms + 30, content.y, content.width - ms - 30, content.height));
                        break;
                    }
                case "workshop": TabWorkshop(app, content); break;
                case "storage": TabStorage(app, content); break;
                case "archive": TabArchive(app, content); break;
                case "achievements": TabAchievements(app, content); break;
                case "robot": TabRobot(app, content); break;
                case "radio": TabRadio(app, content); break;
                case "coop":
                    {
                        const int key = 408;
                        UINav.BeginScroll(key, content);
                        float y = DrawCoopContent(app, UINav.ScrollWidth(key, content), 0);
                        UINav.EndScroll(y + 10);
                        break;
                    }
            }
        }

        // ------------------------------------------------------------ Animation
        const float TabletOpenDur = 0.28f, TabletBootDelay = 0.1f, TabletBootDur = 0.24f, TabletCloseDur = 0.2f, TabSwitchDur = 0.18f;
        float tabletOpenAt = -10f, tabletCloseAt = -10f, tabSwitchAt = -10f;
        int tabSwitchDir;
        float tabPillX = -1f, tabPillW = -1f;

        static float EaseOut(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }

        /// <summary>Aus OnScreenChanged: Öffnen/Schließen des Tablets zeitlich festhalten.</summary>
        void OnTabletScreenChanged(UIScreen from, UIScreen to)
        {
            if (to == UIScreen.Menu) { tabletOpenAt = Time.unscaledTime; tabletCloseAt = -10f; tabPillX = -1f; }
            else if (from == UIScreen.Menu && to == UIScreen.None) tabletCloseAt = Time.unscaledTime;
            else tabletCloseAt = -10f;
        }

        // ------------------------------------------------------------ Aufteilung
        struct TabletRects { public Rect Outer, Screen, Status, Tabs, Content; public float B, Side; }

        /// <summary>Gehäuse füllt das Bild bis auf einen Rand, höchstens 2,2 : 1 (21:9 bleibt ein Tablet), skaliert mit VH.</summary>
        TabletRects TabletLayout()
        {
            var t = new TabletRects();
            float m = Mathf.Clamp(VH * 0.02f, 8f, 24f);
            t.B = Mathf.Clamp(VH * 0.034f, 20f, 40f);
            t.Side = t.B * 1.2f; // seitlich etwas breiter: Griffflächen
            float h = VH - 2f * m;
            float w = Mathf.Min(VW - 2f * m, h * 2.2f);
            t.Outer = new Rect((VW - w) * 0.5f, m, w, h);
            t.Screen = new Rect(t.Outer.x + t.Side, t.Outer.y + t.B, t.Outer.width - 2f * t.Side, t.Outer.height - 2f * t.B);
            float pad = Mathf.Clamp(t.B * 0.5f, 12f, 18f);
            float sh = Mathf.Clamp(VH * 0.03f, 26f, 32f);
            t.Status = new Rect(t.Screen.x + pad, t.Screen.y + 6f, t.Screen.width - 2f * pad, sh);
            float th = Mathf.Clamp(VH * 0.064f, 52f, 70f);
            t.Tabs = new Rect(t.Screen.x + pad, t.Status.yMax + 6f, t.Screen.width - 2f * pad, th);
            float cy = t.Tabs.yMax + 14f;
            t.Content = new Rect(t.Screen.x + pad + 6f, cy, t.Screen.width - 2f * pad - 12f, t.Screen.yMax - cy - pad);
            return t;
        }

        // ------------------------------------------------------------ Spielmenü
        void DrawGameMenu(GameApp app)
        {
            if (!app.InGame) { UIState.Open(UIScreen.MainMenu); return; }
            bool hc = UISkin.Contrast;
            bool calm = app.Settings.ReduceFlashing;
            float now = Time.unscaledTime;
            float open = EaseOut((now - tabletOpenAt) / (hc ? 0.12f : TabletOpenDur));
            float boot = hc ? open : Mathf.Clamp01((now - tabletOpenAt - TabletBootDelay) / TabletBootDur);
            boot = boot * boot * (3f - 2f * boot);
            Dim(0.55f * (hc ? 1f : Mathf.Lerp(0.35f, 1f, open)));

            var T = TabletLayout();
            var tabs = VisibleMenuTabs(app);
            if (TabIndex(tabs, menuTab) < 0 && tabs.Count > 0) { menuTab = tabs[0].Id; UIState.MenuTab = menuTab; }

            var baseM = GUI.matrix;
            var oldCol = GUI.color;
            try
            {
                if (open < 1f && !hc)
                {
                    // Hochgleiten und leicht heranwachsen (um die Mitte des Gehäuses)
                    float s = Mathf.Lerp(0.94f, 1f, open);
                    var piv = new Vector3(T.Outer.center.x, T.Outer.center.y, 0f);
                    var off = new Vector3(0f, (1f - open) * VH * 0.1f, 0f);
                    GUI.matrix = baseM * Matrix4x4.TRS(piv + off, Quaternion.identity, new Vector3(s, s, 1f)) * Matrix4x4.TRS(-piv, Quaternion.identity, Vector3.one);
                }
                float bodyA = hc ? open : Mathf.Clamp01(open * 1.8f);
                SetTabletFade(bodyA);
                DrawTabletBody(app, T);
                DrawTabletScreenBase(T, boot, calm);

                SetTabletFade(bodyA * boot);
                DrawTabletStatus(app, T.Status);
                DrawTabletTabs(app, T.Tabs, tabs);

                // Inhalt mit Reiterwechsel (kurz einblenden und aus der Wechselrichtung hereingleiten)
                float se = hc ? 1f : EaseOut((now - tabSwitchAt) / TabSwitchDur);
                var cm = GUI.matrix;
                if (se < 1f && tabSwitchDir != 0) GUI.matrix = cm * Matrix4x4.TRS(new Vector3(tabSwitchDir * 18f * (1f - se), 0f, 0f), Quaternion.identity, Vector3.one);
                SetTabletFade(bodyA * boot * Mathf.Lerp(0.15f, 1f, se));
                var content = T.Content;
                if (actMsg != null && Time.unscaledTime < actMsgUntil)
                {
                    var mr = new Rect(content.x, content.yMax - 42, content.width, 42);
                    content.height -= 50;
                    DrawActionBanner(mr);
                }
                int cur = TabIndex(tabs, menuTab);
                if (cur >= 0) tabs[cur].Draw(this, app, content);
                GUI.matrix = cm;

                SetTabletFade(bodyA);
                DrawTabletScreenOverlay(T);
            }
            finally
            {
                GUI.matrix = baseM;
                GUI.color = oldCol;
                UISkin.Fade = 1f;
            }
        }

        /// <summary>Deckkraft für Texte (GUI.color) und Zeichenhilfen (UISkin.Fade) gemeinsam setzen.</summary>
        static void SetTabletFade(float a)
        {
            a = Mathf.Clamp01(a);
            UISkin.Fade = a;
            GUI.color = new Color(1f, 1f, 1f, a);
        }

        /// <summary>Ergebnis einer Aktion als Benachrichtigungskarte unten im Bildschirm.</summary>
        void DrawActionBanner(Rect mr)
        {
            var col = actMsgErr ? UISkin.Bad : UISkin.Good;
            if (Event.current.type == EventType.Repaint)
            {
                UISkin.RoundRect(mr, actMsgErr ? new Color(0.35f, 0.07f, 0.05f, 0.94f) : new Color(0.04f, 0.26f, 0.14f, 0.94f));
                UISkin.RoundRect(new Rect(mr.x + 4, mr.y + 7, 5, mr.height - 14), col);
                UISkin.Tex(new Rect(mr.x + 18, mr.y + 11, 20, 20), UISkin.Shape(actMsgErr ? "hazard" : "star"), col);
            }
            GUI.Label(new Rect(mr.x + 48, mr.y, mr.width - 64, mr.height), L(actMsg), UISkin.Label);
        }

        // ------------------------------------------------------------ Gehäuse
        string bezelSrc, bezelText;

        void DrawTabletBody(GameApp app, TabletRects T)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool hc = UISkin.Contrast;
            var o = T.Outer;
            float B = T.B, S = T.Side;
            if (!hc) UISkin.SlicedB(new Rect(o.x - 22f, o.y - 12f, o.width + 44f, o.height + 44f), UISkin.TabletShadow, UISkin.ShadowBorder, new Color(0f, 0f, 0f, 0.65f));
            UISkin.SlicedB(o, UISkin.TabletBody, UISkin.BodyBorder, Color.white);
            if (hc) return;

            // Gummi-Stoßecken (orange), je ein „L“ aus zwei abgerundeten Leisten
            var bump = Color.Lerp(UISkin.Accent, new Color(0.25f, 0.1f, 0.02f), 0.25f);
            var bumpHi = Color.Lerp(UISkin.Accent, Color.white, 0.25f); bumpHi.a = 0.5f;
            float k = B * 0.42f, len = B * 2.6f;
            for (int c = 0; c < 4; c++)
            {
                bool right = (c & 1) == 1, bottom = c >= 2;
                float x0 = right ? o.xMax - len + 3f : o.x - 3f, y0 = bottom ? o.yMax - k + 3f : o.y - 3f;
                float xv = right ? o.xMax - k + 3f : o.x - 3f, yv = bottom ? o.yMax - len + 3f : o.y - 3f;
                UISkin.RoundRect(new Rect(x0, y0, len, k), bump);
                UISkin.RoundRect(new Rect(xv, yv, k, len), bump);
                UISkin.Rect(new Rect(x0 + 8f, y0 + 2f, len - 16f, 1.5f), bumpHi);
            }

            // Griffrillen seitlich
            float gy = o.center.y - B * 1.2f;
            for (int i = 0; i < 6; i++)
            {
                float yy = gy + i * B * 0.48f;
                for (int side = 0; side < 2; side++)
                {
                    float gx = side == 0 ? o.x + S * 0.28f : o.xMax - S * 0.72f;
                    UISkin.Rect(new Rect(gx, yy, S * 0.44f, 2f), new Color(0f, 0f, 0f, 0.35f));
                    UISkin.Rect(new Rect(gx, yy + 2f, S * 0.44f, 1f), new Color(1f, 1f, 1f, 0.07f));
                }
            }

            // Schrauben in den Ecken der Einfassung
            float ss = Mathf.Max(9f, B * 0.36f);
            UISkin.Tex(new Rect(o.x + S * 0.62f - ss * 0.5f, o.y + B * 0.62f - ss * 0.5f, ss, ss), UISkin.TabletScrew, Color.white);
            UISkin.Tex(new Rect(o.xMax - S * 0.62f - ss * 0.5f, o.y + B * 0.62f - ss * 0.5f, ss, ss), UISkin.TabletScrew, Color.white);
            UISkin.Tex(new Rect(o.x + S * 0.62f - ss * 0.5f, o.yMax - B * 0.62f - ss * 0.5f, ss, ss), UISkin.TabletScrew, Color.white);
            UISkin.Tex(new Rect(o.xMax - S * 0.62f - ss * 0.5f, o.yMax - B * 0.62f - ss * 0.5f, ss, ss), UISkin.TabletScrew, Color.white);

            // Kamera oben mittig
            float cs = Mathf.Max(8f, B * 0.36f), ccx = o.center.x, ccy = o.y + B * 0.5f;
            UISkin.Tex(new Rect(ccx - cs * 0.8f, ccy - cs * 0.8f, cs * 1.6f, cs * 1.6f), UISkin.Circle, new Color(0f, 0f, 0f, 0.45f));
            UISkin.Tex(new Rect(ccx - cs * 0.5f, ccy - cs * 0.5f, cs, cs), UISkin.Circle, new Color(0.02f, 0.05f, 0.07f, 1f));
            UISkin.Tex(new Rect(ccx - cs * 0.5f, ccy - cs * 0.5f, cs, cs), UISkin.Ring, new Color(UISkin.Teal.r, UISkin.Teal.g, UISkin.Teal.b, 0.45f));
            UISkin.Tex(new Rect(ccx - cs * 0.22f, ccy - cs * 0.28f, cs * 0.2f, cs * 0.2f), UISkin.Circle, new Color(1f, 1f, 1f, 0.55f));

            // Status-LEDs oben rechts (ruhig leuchtend, kein Blinken): Betrieb, Funk (Koop), Akku
            var me = app.Me; var w = app.W;
            float ef = me != null && w != null ? me.Energy / Mathf.Max(1f, w.MaxEnergy) : 1f;
            float lx = o.xMax - S - 12f, ly = o.y + B * 0.5f, ls = Mathf.Max(6f, B * 0.2f);
            for (int i = 0; i < 3; i++)
            {
                Color lc = i == 0 ? UISkin.Good : i == 1 ? (app.CoopActive ? UISkin.Teal : new Color(0.3f, 0.4f, 0.42f)) : (ef < 0.15f ? UISkin.Bad : ef < 0.35f ? UISkin.Warn : UISkin.Good);
                float x = lx - i * ls * 2.6f;
                bool lit = !(i == 1 && !app.CoopActive);
                if (lit) UISkin.Tex(new Rect(x - ls * 1.3f, ly - ls * 1.3f, ls * 2.6f, ls * 2.6f), UISkin.Circle, new Color(lc.r, lc.g, lc.b, 0.22f));
                UISkin.Tex(new Rect(x - ls * 0.5f, ly - ls * 0.5f, ls, ls), UISkin.Circle, lc);
            }

            // Lautsprecherschlitze unten links, Gravur unten mittig
            float gx0 = o.x + S + 10f, gy0 = o.yMax - B * 0.5f;
            for (int i = 0; i < 7; i++)
                UISkin.RoundRect(new Rect(gx0 + i * 7f, gy0 - B * 0.18f, 3f, B * 0.36f), new Color(0f, 0f, 0f, 0.4f));
            string src = L("MIKO · Feldtablet");
            if (!ReferenceEquals(src, bezelSrc)) { bezelSrc = src; bezelText = src.ToUpperInvariant(); }
            GUI.Label(new Rect(o.x, o.yMax - B, o.width, B), bezelText, UISkin.BezelMark);
        }

        // ------------------------------------------------------------ Bildschirm
        void DrawTabletScreenBase(TabletRects T, float boot, bool calm)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool hc = UISkin.Contrast;
            var s = T.Screen;
            UISkin.SlicedB(s, UISkin.TabletScreen, UISkin.ScreenBorder, Color.white);
            if (hc) return;
            // Leuchten des Bildschirms: weich von den Rändern nach innen
            var g = UISkin.Teal; g.a = 0.07f * boot;
            UISkin.Tex(new Rect(s.x + 3f, s.y + 3f, s.width - 6f, s.height - 6f), UISkin.EdgeGlow, g);
            // Hochfahren: waagerechte Lichtlinie, die sich zum ganzen Bild öffnet und dabei ausblendet (stetig, kein Blitz)
            if (boot < 1f && !calm)
            {
                float e = boot;
                float cy = s.center.y, lh = Mathf.Lerp(2f, s.height - 8f, e * e);
                var band = UISkin.Teal; band.a = 0.16f * (1f - e);
                UISkin.Rect(new Rect(s.x + 6f, cy - lh * 0.5f, s.width - 12f, lh), band);
                var line = Color.Lerp(UISkin.Teal, Color.white, 0.4f); line.a = 0.45f * (1f - e);
                float lw = (s.width - 12f) * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(e * 3f));
                UISkin.Rect(new Rect(s.center.x - lw * 0.5f, cy - 1f, lw, 2f), line);
            }
        }

        /// <summary>Über dem Inhalt: Scanlinien, festes Rauschen, Glasreflex (sehr schwach, nicht im Hohen Kontrast).</summary>
        void DrawTabletScreenOverlay(TabletRects T)
        {
            if (UISkin.Contrast || Event.current.type != EventType.Repaint) return;
            var s = new Rect(T.Screen.x + 3f, T.Screen.y + 3f, T.Screen.width - 6f, T.Screen.height - 6f);
            UISkin.Tiled(s, UISkin.TabletScan, 1f, 4f, new Color(1f, 1f, 1f, 0.05f));
            UISkin.Tiled(s, UISkin.TabletNoise, 1.5f, 1.5f, new Color(1f, 1f, 1f, 0.022f));
            UISkin.Tex(new Rect(s.x, s.y, s.width * 0.78f, s.height * 0.85f), UISkin.TabletGlass, new Color(1f, 1f, 1f, 0.05f));
        }

        // ------------------------------------------------------------ Statusleiste
        int stClockMins = -1; string stClock;
        long stCredits = long.MinValue; string stCreditsText;
        int stBattery = -1; string stBatteryText;
        int stPlayers = -1; string stSignalText, stSignalSrc;

        void DrawTabletStatus(GameApp app, Rect r)
        {
            var w = app.W; var me = app.Me;
            if (w == null) return;
            bool hc = UISkin.Contrast;
            bool rep = Event.current.type == EventType.Repaint;
            if (rep && !hc) UISkin.RoundRect(r, new Color(0f, 0f, 0f, 0.2f));
            float cy = r.center.y, ih = Mathf.Min(18f, r.height - 8f);
            float x = r.x + 10f;
            // MIKO-OS und Planet
            UISkin.Tex(new Rect(x, cy - ih * 0.5f, ih, ih), UISkin.Shape("robot"), UISkin.Teal);
            x += ih + 6f;
            GUI.Label(new Rect(x, r.y, 90f, r.height), "MIKO-OS", UISkin.StatusBold);
            x += UISkin.TextWidth(UISkin.StatusBold, "MIKO-OS") + 14f;
            PlanetDef pd;
            if (GameData.Planets.TryGetValue(w.CurrentPlanet ?? "", out pd))
            {
                var pc = UISkin.FromRgb(pd.Accent);
                UISkin.Tex(new Rect(x, cy - 5f, 10f, 10f), UISkin.Circle, pc);
                x += 16f;
                var ps = UISkin.StatusBold;
                var oc = ps.normal.textColor;
                if (!hc) ps.normal.textColor = pc;
                GUI.Label(new Rect(x, r.y, 220f, r.height), pd.Name, ps);
                ps.normal.textColor = oc;
            }

            // Mitte: Uhrzeit (Planetentag) mit Sonne/Mond
            float phase = Rules.DayPhase(w, w.CurrentPlanet);
            int mins = (int)(phase * 24f * 60f);
            if (mins != stClockMins) { stClockMins = mins; stClock = (mins / 60).ToString("00") + ":" + (mins % 60).ToString("00"); }
            bool night = Rules.IsNight(phase);
            float cw = UISkin.TextWidth(UISkin.StatusBold, stClock);
            float cx = r.center.x - (cw + ih + 6f) * 0.5f;
            UISkin.Tex(new Rect(cx, cy - ih * 0.5f, ih, ih), UISkin.Shape(night ? "moon" : "sun"), night ? new Color(0.75f, 0.82f, 1f) : UISkin.Warn);
            GUI.Label(new Rect(cx + ih + 6f, r.y, cw + 8f, r.height), stClock, UISkin.StatusBold);

            // Rechts: Akku (MIKO), Credits, Signal
            float xr = r.xMax - 10f;
            float ef = me != null ? Mathf.Clamp01(me.Energy / Mathf.Max(1f, w.MaxEnergy)) : 0f;
            int pct = Mathf.RoundToInt(ef * 100f);
            if (pct != stBattery) { stBattery = pct; stBatteryText = pct + " %"; }
            Color bc = ef < 0.15f ? UISkin.Bad : ef < 0.35f ? UISkin.Warn : UISkin.Good;
            float bw = 26f, bh = 13f;
            var body = new Rect(xr - bw - 3f, cy - bh * 0.5f, bw, bh);
            if (rep)
            {
                UISkin.RoundRect(body, new Color(UISkin.Text.r, UISkin.Text.g, UISkin.Text.b, 0.8f));
                UISkin.RoundRect(new Rect(body.x + 1.5f, body.y + 1.5f, body.width - 3f, body.height - 3f), new Color(0.02f, 0.08f, 0.1f, 1f));
                UISkin.Rect(new Rect(body.x + 3f, body.y + 3f, (body.width - 6f) * ef, body.height - 6f), bc);
                UISkin.Rect(new Rect(body.xMax, cy - 3f, 3f, 6f), new Color(UISkin.Text.r, UISkin.Text.g, UISkin.Text.b, 0.8f));
            }
            float tw = UISkin.TextWidth(UISkin.StatusBold, stBatteryText);
            xr = body.x - 6f - tw;
            GUI.Label(new Rect(xr, r.y, tw + 6f, r.height), stBatteryText, UISkin.StatusBold);
            xr -= 22f;

            if (w.Credits != stCredits) { stCredits = w.Credits; stCreditsText = Num(w.Credits) + " Cr"; }
            tw = UISkin.TextWidth(UISkin.StatusBold, stCreditsText);
            xr -= tw;
            GUI.Label(new Rect(xr, r.y, tw + 6f, r.height), stCreditsText, UISkin.StatusBold);
            UISkin.Tex(new Rect(xr - ih - 6f, cy - ih * 0.5f, ih, ih), UISkin.Shape("coin"), UISkin.Warn);
            xr -= ih + 6f + 22f;

            int players = 0;
            foreach (var p in w.Players.Values) if (p.Online) players++;
            bool coop = app.CoopActive;
            string sigSrc = coop ? null : L("Solo");
            if (players != stPlayers || !ReferenceEquals(sigSrc, stSignalSrc) || stSignalText == null)
            {
                stPlayers = players; stSignalSrc = sigSrc;
                stSignalText = coop ? players + "/" + GameData.MaxPlayers : sigSrc;
            }
            tw = UISkin.TextWidth(UISkin.StatusText, stSignalText);
            xr -= tw;
            GUI.Label(new Rect(xr, r.y, tw + 6f, r.height), stSignalText, coop ? UISkin.StatusBold : UISkin.StatusText);
            UISkin.Tex(new Rect(xr - ih - 6f, cy - ih * 0.5f, ih, ih), UISkin.Shape("signal"), coop ? UISkin.Teal : new Color(UISkin.TextDim.r, UISkin.TextDim.g, UISkin.TextDim.b, 0.5f));
        }

        // ------------------------------------------------------------ Reiterleiste (Apps)
        void DrawTabletTabs(GameApp app, Rect r, List<MenuTabDef> list)
        {
            bool hc = UISkin.Contrast;
            bool rep = Event.current.type == EventType.Repaint;
            bool pad = InputMap.UsingPad;
            float kh = 26f;
            float kw = Mathf.Max(kh, UISkin.Key.CalcSize(UISkin.Tmp(pad ? "RB" : "E")).x + 8f);
            float closeW = Mathf.Clamp(r.height * 1.5f, 78f, 104f);
            var bar = new Rect(r.x + kw + 8f, r.y, r.width - 2f * (kw + 8f) - closeW - 10f, r.height);
            UISkin.KeyCap(r.x, r.center.y - kh * 0.5f, pad ? "LB" : "Q", kh);
            UISkin.KeyCap(bar.xMax + 8f, r.center.y - kh * 0.5f, pad ? "RB" : "E", kh);
            if (rep) UISkin.RoundRect(bar, hc ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0.26f));

            int n = Mathf.Max(1, list.Count);
            const float gap = 4f;
            float tw = (bar.width - 8f - gap * (n - 1)) / n;
            float th = bar.height - 8f;
            // Passen alle Beschriftungen? Sonst nur die gewählte (App-Leiste mit Symbolen)
            float maxL = 0f;
            for (int i = 0; i < list.Count; i++) maxL = Mathf.Max(maxL, UISkin.TextWidth(UISkin.TabLabel, L(list[i].Name)));
            bool allLabels = maxL + 8f <= tw;
            int cur = TabIndex(list, menuTab);

            // Auswahl-Pille gleitet zum gewählten Reiter
            if (cur >= 0)
            {
                float target = bar.x + 4f + cur * (tw + gap);
                if (rep)
                {
                    if (tabPillX < 0f || Mathf.Abs(tabPillW - tw) > 0.5f || hc) tabPillX = target;
                    else tabPillX += (target - tabPillX) * (1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, 0.1f) * 18f));
                    tabPillW = tw;
                    var pr = new Rect(tabPillX, bar.y + 4f, tw, th);
                    UISkin.Sliced(pr, UISkin.BtnSel);
                    if (!hc) UISkin.RoundRect(new Rect(pr.center.x - tw * 0.22f, pr.yMax - 4f, tw * 0.44f, 3f), UISkin.Accent);
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                var tile = new Rect(bar.x + 4f + i * (tw + gap), bar.y + 4f, tw, th);
                bool focused;
                bool click = UINav.Area(tile, out focused);
                bool sel = i == cur;
                bool hover = UINav.IsHover(tile) || (focused && UINav.KeyboardMode);
                if (rep && hover && !sel) UISkin.RoundRect(tile, new Color(1f, 1f, 1f, hc ? 0.15f : 0.07f));
                bool label = allLabels || sel;
                float isz = Mathf.Min(th * (label ? 0.44f : 0.56f), 28f);
                float iy = label ? tile.y + th * 0.12f : tile.center.y - isz * 0.5f;
                Color ic = sel ? UISkin.Accent : hover ? UISkin.Text : UISkin.TextDim;
                UISkin.Tex(new Rect(tile.center.x - isz * 0.5f, iy, isz, isz), UISkin.Shape(d.Icon), ic);
                if (label) GUI.Label(new Rect(tile.x - 20f, iy + isz + 1f, tile.width + 40f, th - (iy - tile.y) - isz - 2f), L(d.Name), sel ? UISkin.TabLabelSel : UISkin.TabLabel);
                if (click && !sel) SetMenuTab(d.Id);
            }

            // Schließen (wie eine App-Kachel ganz rechts)
            var cr = new Rect(r.xMax - closeW, r.y + 4f, closeW, r.height - 8f);
            bool cf;
            bool close = UINav.Area(cr, out cf);
            bool ch = UINav.IsHover(cr) || (cf && UINav.KeyboardMode);
            if (rep) UISkin.RoundRect(cr, ch ? new Color(UISkin.Bad.r * 0.45f, UISkin.Bad.g * 0.3f, UISkin.Bad.b * 0.3f, 0.9f) : hc ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0.26f));
            float cis = Mathf.Min(cr.height * 0.36f, 20f);
            UISkin.Tex(new Rect(cr.center.x - cis * 0.5f, cr.y + cr.height * 0.16f, cis, cis), UISkin.Shape("cross"), ch ? UISkin.Text : UISkin.TextDim);
            GUI.Label(new Rect(cr.x - 10f, cr.y + cr.height * 0.16f + cis + 1f, cr.width + 20f, cr.height * 0.84f - cis - 2f), L("Zurück") + (pad ? " (B)" : ""), ch ? UISkin.TabLabelSel : UISkin.TabLabel);
            if (close) Back();
        }

        // ------------------------------------------------------------ Schließen (nach dem Wechsel zu „kein Menü“)
        /// <summary>Kurzes Abtauchen des Tablets nach dem Schließen; nur Darstellung, keine Bedienelemente.</summary>
        void DrawTabletClosing(GameApp app)
        {
            float e = (Time.unscaledTime - tabletCloseAt) / TabletCloseDur;
            if (e < 0f || e >= 1f || UISkin.Contrast || Event.current.type != EventType.Repaint) return;
            float k = e * e;
            Dim(0.55f * (1f - e));
            var T = TabletLayout();
            var baseM = GUI.matrix;
            var oldCol = GUI.color;
            try
            {
                float s = 1f - 0.05f * k;
                var piv = new Vector3(T.Outer.center.x, T.Outer.center.y, 0f);
                var off = new Vector3(0f, k * VH * 0.14f, 0f);
                GUI.matrix = baseM * Matrix4x4.TRS(piv + off, Quaternion.identity, new Vector3(s, s, 1f)) * Matrix4x4.TRS(-piv, Quaternion.identity, Vector3.one);
                SetTabletFade(1f - e);
                DrawTabletBody(app, T);
                UISkin.SlicedB(T.Screen, UISkin.TabletScreen, UISkin.ScreenBorder, Color.white);
                // Bild „schaltet ab“: schmaler werdende Linie in der Mitte
                var sc = T.Screen;
                var line = Color.Lerp(UISkin.Teal, Color.white, 0.3f); line.a = 0.35f * (1f - e);
                float lw = (sc.width - 12f) * (1f - e);
                UISkin.Rect(new Rect(sc.center.x - lw * 0.5f, sc.center.y - 1f, lw, 2f), line);
            }
            finally
            {
                GUI.matrix = baseM;
                GUI.color = oldCol;
                UISkin.Fade = 1f;
            }
        }

        // ------------------------------------------------------------ Inhalte im Tablet-Stil
        /// <summary>Inhaltskarte (abgerundet, feine Kontur); hi = hervorgehoben (orange Kontur).</summary>
        static void Card(Rect r, bool hi = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            UISkin.Sliced(r, hi ? UISkin.CardHi : UISkin.Card);
        }
    }
}
