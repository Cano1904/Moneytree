using System;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Zeichnet alle Menüs, das HUD und Overlays per IMGUI und verarbeitet die Menü-Tasten.
    /// Aufgeteilt auf mehrere Dateien (partial): Menüs, Einstellungen, Koop, Spielmenü, Karte, HUD, Bau- und Fotomodus.
    /// </summary>
    public partial class UIRoot : MonoBehaviour
    {
        public static UIRoot I { get; private set; }

        /// <summary>true, wenn die Maus frei sein soll (Menü offen, Bauansicht, Fotomodus-Panel sichtbar).</summary>
        public static bool WantsCursor
        {
            get
            {
                var s = UIState.Screen;
                if (s == UIScreen.Intro || s == UIScreen.Ending || s == UIScreen.Loading) return false;
                if (s == UIScreen.Photo) return PhotoMode.Active && I != null && I.photoPanel;
                if (s != UIScreen.None) return true;
                if (BuildMode.Active) return true;
                var app = GameApp.I;
                return app == null || !app.InGame;
            }
        }

        // Virtuelle Bildschirmgröße (nach Skalierung über GUI.matrix)
        float scale = 1f, VW = 1920f, VH = 1080f;
        UIScreen lastScreen = (UIScreen)(-1);
        int screenChangedFrame;
        /// <summary>Zurück-Ziel für Spielstände/Einstellungen/Koop (Hauptmenü oder Pause).</summary>
        UIScreen subReturn = UIScreen.MainMenu;
        bool autoOpenCoop;
        float autoOpenTimeout;
        string guiError;
        float guiErrorUntil;
        string confirm;          // offene Sicherheitsabfrage (Schlüssel)
        float fps, fpsAcc; int fpsFrames;
        float textScaleUsed;

        static string L(string de) { return Loc.T(de); }

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
        }

        // ================================================================== Update: Eingaben, Mauszeiger
        void Update()
        {
            var app = GameApp.I;
            if (app == null) return;

            fpsAcc += Time.unscaledDeltaTime; fpsFrames++;
            if (fpsAcc >= 0.5f) { fps = fpsFrames / fpsAcc; fpsAcc = 0; fpsFrames = 0; }

            var screen = UIState.Screen;
            bool fresh = false;
            if (screen != lastScreen)
            {
                OnScreenChanged(lastScreen, screen);
                lastScreen = screen;
                screenChangedFrame = Time.frameCount;
                fresh = true;
            }

            bool navActive = !fresh && capturing == null && NavScreen(screen);
            UINav.Tick(navActive);

            if (capturing != null) UpdateCapture(app);
            else if (!fresh) HandleHotkeys(app);

            // Nach „Spielstand laden & hosten“: sobald die Welt läuft, Koop öffnen
            if (autoOpenCoop)
            {
                autoOpenTimeout -= Time.unscaledDeltaTime;
                if (app.InGame && app.IsHost)
                {
                    autoOpenCoop = false;
                    string inv, err;
                    if (app.OpenCoop(out inv, out err)) { coopError = null; OpenSub(UIScreen.Coop, UIScreen.None); Hud.Show("Koop geöffnet – Einladung teilen!", ToastKind.Success, 5f); }
                    else Hud.Show("Koop konnte nicht geöffnet werden: " + err, ToastKind.Error, 8f);
                }
                else if (autoOpenTimeout <= 0 || app.Mode == AppMode.Menu && UIState.Screen != UIScreen.Loading) autoOpenCoop = false;
            }

            // Bauansicht nur ohne offenes Menü
            if (BuildMode.Active && UIState.Screen != UIScreen.None) BuildMode.Active = false;
            if (!PhotoMode.Active && UIState.Screen == UIScreen.Photo) UIState.Open(UIScreen.None);

            ProcessToasts(app);
            PruneToasts();
            UpdateBuildInput(app);
            UpdateSettingsApply(app);
            UpdateMapBuild(app);
            UpdateCursor(app);
        }

        static bool NavScreen(UIScreen s)
        {
            switch (s)
            {
                case UIScreen.None: return false; // Bauansicht: Maus + Mausrad/Bild↑↓/LB/RB
                case UIScreen.Intro:
                case UIScreen.Ending:
                case UIScreen.Loading: return false;
                case UIScreen.Photo: return I != null && I.photoPanel;
            }
            return true;
        }

        void UpdateCursor(GameApp app)
        {
            try
            {
                if (WantsCursor)
                {
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else if (app.InGame)
                {
                    if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = false;
                }
            }
            catch (Exception) { }
        }

        void OnScreenChanged(UIScreen from, UIScreen to)
        {
            UINav.ResetFocus();
            confirm = null;
            if (from == UIScreen.Menu && to != UIScreen.Menu) UIState.Station = null;
            switch (to)
            {
                case UIScreen.Menu: OnMenuOpened(); break;
                case UIScreen.MainMenu: RefreshMainMenu(); break;
                case UIScreen.Saves: RefreshSaves(); break;
                case UIScreen.NewGame: OnNewGameOpened(); break;
                case UIScreen.PlanetSelect: planetPreviewed = null; break;
                case UIScreen.Coop: coopError = null; joinInit = false; RefreshSaves(); break;
                case UIScreen.Settings: pendingRes = -1; pendingWindow = -1; portText = null; resList = null; captureMsg = null; capturing = null; break;
                case UIScreen.Map: mapZoom = Mathf.Max(mapZoom, 1f); break;
            }
        }

        /// <summary>Öffnet einen Unterbildschirm (Spielstände/Einstellungen/Koop) und merkt sich das Zurück-Ziel.</summary>
        void OpenSub(UIScreen s, UIScreen returnTo)
        {
            subReturn = returnTo;
            UIState.Open(s);
        }

        void Back()
        {
            AudioManager.Ui("ui_back");
            var app = GameApp.I;
            switch (UIState.Screen)
            {
                case UIScreen.Saves:
                case UIScreen.Settings:
                case UIScreen.Coop:
                    if (app != null && !app.InGame && (subReturn == UIScreen.Pause || subReturn == UIScreen.None || subReturn == UIScreen.Menu)) subReturn = UIScreen.MainMenu;
                    UIState.Open(subReturn);
                    break;
                case UIScreen.NewGame:
                case UIScreen.Credits:
                    UIState.Open(UIScreen.MainMenu);
                    break;
                case UIScreen.PlanetSelect:
                    if (app != null) app.Mode = AppMode.Menu;
                    if (WorldView.I != null) WorldView.I.BuildMenuBackdrop();
                    UIState.Open(UIScreen.MainMenu);
                    break;
                case UIScreen.Message:
                    UIState.Open(UIState.ReturnTo);
                    break;
                case UIScreen.Pause:
                case UIScreen.Menu:
                case UIScreen.Map:
                case UIScreen.Travel:
                    UIState.Open(app != null && app.InGame ? UIScreen.None : UIScreen.MainMenu);
                    break;
                case UIScreen.Photo:
                    ExitPhoto();
                    break;
            }
        }

        /// <summary>Taste nur über die Tastaturbelegung (ohne Controller-Steuerkreuz, das in Menüs navigiert).</summary>
        static bool KeyDown(GameAction a)
        {
            var k = InputMap.Get(a);
            return k != KeyCode.None && k < KeyCode.Mouse0 && Input.GetKeyDown(k);
        }

        void HandleHotkeys(GameApp app)
        {
            var s = UIState.Screen;
            bool editing = UINav.Editing;
            bool back = !editing && InputMap.NavBack();
            bool start = Input.GetKeyDown(KeyCode.JoystickButton7);
            bool inGame = app.InGame;

            if (s == UIScreen.None)
            {
                if (!inGame) return;
                if (BuildMode.Active)
                {
                    if (InputMap.Down(GameAction.Pause) || InputMap.Down(GameAction.Build) || back) { ExitBuild(); AudioManager.Ui("ui_back"); return; }
                    if (InputMap.Down(GameAction.Menu)) { ExitBuild(); OpenMenu(UIState.MenuTab); return; }
                    return;
                }
                if (InputMap.Down(GameAction.Pause)) { UIState.Open(UIScreen.Pause); AudioManager.Ui("ui_click"); return; }
                if (InputMap.Down(GameAction.Menu)) { OpenMenu(UIState.MenuTab); return; }
                if (InputMap.Down(GameAction.Inventory)) { OpenMenu("inventory"); return; }
                if (InputMap.Down(GameAction.Missions)) { OpenMenu("missions"); return; }
                if (InputMap.Down(GameAction.Map)) { UIState.Open(UIScreen.Map); AudioManager.Ui("ui_click"); return; }
                if (InputMap.Down(GameAction.Build)) { ToggleBuild(app); return; }
                if (InputMap.Down(GameAction.Photo)) { EnterPhoto(); return; }
                if (InputMap.Down(GameAction.QuickSave)) { app.SaveNow(); return; }
                return;
            }

            switch (s)
            {
                case UIScreen.Photo:
                    if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton3)) { photoPanel = !photoPanel; AudioManager.Ui("ui_click"); return; }
                    if (Input.GetKeyDown(KeyCode.F12) || (!photoPanel && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)))) { PhotoMode.RequestCapture = true; return; }
                    if (InputMap.Down(GameAction.Pause) || InputMap.Down(GameAction.Photo) || back) { ExitPhoto(); AudioManager.Ui("ui_back"); }
                    return;
                case UIScreen.Menu:
                    if (editing) return;
                    if (back || start || KeyDown(GameAction.Menu)) { Back(); return; }
                    if (KeyDown(GameAction.Inventory)) { if (menuTab == "inventory") Back(); else SetMenuTab("inventory"); return; }
                    if (KeyDown(GameAction.Missions)) { if (menuTab == "missions") Back(); else SetMenuTab("missions"); return; }
                    if (KeyDown(GameAction.Map) || Input.GetKeyDown(KeyCode.JoystickButton6)) { if (menuTab == "map") Back(); else SetMenuTab("map"); return; }
                    if (InputMap.NavTabLeft()) { CycleMenuTab(-1); return; }
                    if (InputMap.NavTabRight()) { CycleMenuTab(1); return; }
                    return;
                case UIScreen.Map:
                    if (back || start || KeyDown(GameAction.Map) || Input.GetKeyDown(KeyCode.JoystickButton6)) { Back(); return; }
                    if (KeyDown(GameAction.Menu)) { OpenMenu("map"); return; }
                    if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.JoystickButton5)) ZoomMap(1);
                    if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.JoystickButton4)) ZoomMap(-1);
                    return;
                case UIScreen.Pause:
                    if (back || (inGame && InputMap.Down(GameAction.Pause))) { if (confirm != null) { confirm = null; AudioManager.Ui("ui_back"); } else Back(); }
                    return;
                case UIScreen.Settings:
                    if (editing) return;
                    if (back) { Back(); return; }
                    if (InputMap.NavTabLeft()) { settingsTab = (settingsTab + SettingsTabs.Length - 1) % SettingsTabs.Length; UINav.ResetFocus(); AudioManager.Ui("ui_click"); }
                    if (InputMap.NavTabRight()) { settingsTab = (settingsTab + 1) % SettingsTabs.Length; UINav.ResetFocus(); AudioManager.Ui("ui_click"); }
                    return;
                case UIScreen.Saves:
                case UIScreen.Coop:
                case UIScreen.NewGame:
                case UIScreen.PlanetSelect:
                case UIScreen.Credits:
                case UIScreen.Travel:
                    if (editing) return;
                    if (back) { if (confirm != null) { confirm = null; AudioManager.Ui("ui_back"); } else Back(); }
                    return;
                case UIScreen.Message:
                    if (back) Back();
                    return;
            }
        }

        // ================================================================== Modi
        void OpenMenu(string tab)
        {
            if (string.IsNullOrEmpty(tab)) tab = "inventory";
            UIState.MenuTab = tab;
            UIState.Station = null;
            UIState.Open(UIScreen.Menu);
            AudioManager.Ui("ui_click");
        }

        void ToggleBuild(GameApp app)
        {
            if (BuildMode.Active) { ExitBuild(); return; }
            var w = app.W; var me = app.Me;
            if (w == null || me == null) return;
            if (me.Vehicle != null) { Hud.Show("Zum Bauen erst aussteigen.", ToastKind.Info); return; }
            var bl = WorldGen.Get(w.CurrentPlanet).Base;
            if (!bl.InBase(me.Pos.x, me.Pos.z)) { Hud.Show("Bauen geht nur am Stützpunkt.", ToastKind.Info); AudioManager.Ui("beep_error"); return; }
            BuildMode.Active = true;
            BuildMode.MoveId = -1;
            BuildMode.RequestDemolish = false;
            BuildMode.RequestPlace = false;
            buildScroll = 0;
            AudioManager.Ui("ui_click");
        }

        void ExitBuild()
        {
            BuildMode.Active = false;
            BuildMode.MoveId = -1;
            BuildMode.Type = null;
            BuildMode.RequestDemolish = false;
            BuildMode.RequestPlace = false;
        }

        void EnterPhoto()
        {
            ExitBuild();
            PhotoMode.Active = true;
            PhotoMode.Fov = GameApp.I != null ? GameApp.I.Settings.Fov : 55f;
            PhotoMode.Roll = 0f;
            PhotoMode.ShowBefore = false;
            photoPanel = true;
            UIState.Open(UIScreen.Photo);
            AudioManager.Ui("ui_click");
        }

        void ExitPhoto()
        {
            PhotoMode.Active = false;
            PhotoMode.ShowBefore = false;
            PhotoMode.Roll = 0f;
            UIState.Open(UIScreen.None);
        }

        // ================================================================== OnGUI
        void OnGUI()
        {
            var app = GameApp.I;
            if (app == null) return;
            UISkin.Ensure(app.Settings.HighContrast);
            GUI.depth = -1000;
            // Textgröße erst nach dem Loslassen des Reglers übernehmen (sonst springt das Layout unter der Maus)
            if (!Input.GetMouseButton(0) || textScaleUsed <= 0f) textScaleUsed = app.Settings.TextScale;
            float ts = Mathf.Clamp(textScaleUsed, 0.8f, 1.6f);
            scale = Mathf.Max(0.3f, Screen.height / 1080f * ts);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            VW = Screen.width / scale; VH = Screen.height / scale;
            var oldColor = GUI.color;
            UINav.BeginPass();
            var screen = UIState.Screen;
            try
            {
                DrawScreen(app, screen);
            }
            catch (ExitGUIException) { UINav.AbortScrolls(); throw; }
            catch (Exception e)
            {
                UINav.AbortScrolls();
                GUI.color = oldColor;
                if (guiError != e.Message) Debug.LogException(e);
                guiError = e.GetType().Name + ": " + e.Message;
                guiErrorUntil = Time.unscaledTime + 8f;
            }
            try
            {
                DrawEntryOverlay();
                bool photoClean = screen == UIScreen.Photo && PhotoMode.HideHud && !photoPanel;
                if (screen != UIScreen.Intro && screen != UIScreen.Ending && screen != UIScreen.Loading && !photoClean) DrawToasts(app);
                if (guiError != null && Time.unscaledTime < guiErrorUntil)
                {
                    var r = new Rect(20, VH - 70, Mathf.Min(900, VW - 40), 50);
                    UISkin.PanelBox(r);
                    GUI.Label(new Rect(r.x + 14, r.y, r.width - 28, r.height), UISkin.Col("Anzeigefehler: " + guiError, UISkin.Bad), UISkin.LabelSmall);
                }
            }
            catch (ExitGUIException) { throw; }
            catch (Exception e) { if (guiError == null) Debug.LogException(e); }
            GUI.color = oldColor;
            UINav.EndPass();
            GUI.matrix = Matrix4x4.identity;
        }

        /// <summary>Atmosphäreneintritt nach der Planetenwahl: Glühen über dem ganzen Bild (auch während des Ladens).</summary>
        void DrawEntryOverlay()
        {
            var ps = PlanetSelectScene.I;
            if (ps == null || ps.Overlay <= 0.001f || Event.current.type != EventType.Repaint) return;
            float a = Mathf.Clamp01(ps.Overlay);
            var c = ps.OverlayColor;
            UISkin.Rect(new Rect(0, 0, VW, VH), new Color(c.r, c.g, c.b, a));
            float s = Mathf.Max(VW, VH) * 1.3f;
            UISkin.Tex(new Rect((VW - s) * 0.5f, (VH - s) * 0.5f, s, s), UISkin.Circle, new Color(1f, 0.97f, 0.9f, a * 0.55f));
        }

        void DrawScreen(GameApp app, UIScreen screen)
        {
            switch (screen)
            {
                case UIScreen.None:
                    if (app.InGame)
                    {
                        if (!(PhotoMode.Active && PhotoMode.HideHud)) DrawHud(app);
                        if (BuildMode.Active) DrawBuild(app);
                    }
                    break;
                case UIScreen.MainMenu: DrawMainMenu(app); break;
                case UIScreen.NewGame: DrawNewGame(app); break;
                case UIScreen.PlanetSelect: DrawPlanetSelect(app); break;
                case UIScreen.Saves: DrawSaves(app); break;
                case UIScreen.Settings: DrawSettings(app); break;
                case UIScreen.Coop: DrawCoop(app); break;
                case UIScreen.Pause: DrawPause(app); break;
                case UIScreen.Menu: DrawGameMenu(app); break;
                case UIScreen.Map: DrawMapScreen(app); break;
                case UIScreen.Photo: DrawPhoto(app); break;
                case UIScreen.Loading: DrawLoading(app); break;
                case UIScreen.Message: DrawMessage(app); break;
                case UIScreen.Travel: DrawTravelScreen(app); break;
                case UIScreen.Credits: UIState.Open(UIScreen.MainMenu); break; // Mitwirkende entfernt
                case UIScreen.Intro:
                case UIScreen.Ending:
                    // Die Regisseure zeichnen Untertitel und Überspringen-Hinweis selbst.
                    break;
            }
        }

        // ================================================================== Gemeinsame Zeichenhilfen
        /// <summary>Abdunklung hinter Menüs im Spiel (die 3D-Welt bleibt sichtbar).</summary>
        void Dim(float a = 0.55f)
        {
            if (Event.current.type != EventType.Repaint) return;
            UISkin.Rect(new Rect(0, 0, VW, VH), new Color(0.01f, 0.04f, 0.05f, UISkin.Contrast ? Mathf.Max(a, 0.8f) : a));
        }

        void Vignette()
        {
            if (Event.current.type != EventType.Repaint) return;
            UISkin.Tex(new Rect(0, 0, VW, VH), UISkin.Vignette, Color.white);
            if (UISkin.Contrast) UISkin.Rect(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.5f));
        }

        /// <summary>Hintergrund für Unterbildschirme: im Spiel abdunkeln, im Hauptmenü Vignette.</summary>
        void Backdrop(GameApp app)
        {
            if (app.InGame) Dim(); else Vignette();
        }

        Rect CenterRect(float w, float h)
        {
            w = Mathf.Min(w, VW - 40); h = Mathf.Min(h, VH - 40);
            return new Rect((VW - w) * 0.5f, (VH - h) * 0.5f, w, h);
        }

        /// <summary>Panel mit Titelzeile; liefert den Innenbereich.</summary>
        Rect Window(Rect r, string title, bool closeButton = true)
        {
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x + 24, r.y + 12, r.width - 200, 44), title, UISkin.H2);
            UISkin.Rect(new Rect(r.x + 20, r.y + 60, r.width - 40, 2), new Color(UISkin.Accent.r, UISkin.Accent.g, UISkin.Accent.b, 0.6f));
            if (closeButton)
            {
                // Nicht im Fokus-Ring (sonst stünde der Fokus zuerst auf „Zurück“) – per Esc/B erreichbar
                var br = new Rect(r.xMax - 196, r.y + 14, 176, 38);
                if (GUI.Button(br, "‹ " + L("Zurück") + (InputMap.UsingPad ? " (B)" : " (Esc)"), UISkin.ButtonSmall)) Back();
            }
            return new Rect(r.x + 24, r.y + 74, r.width - 48, r.height - 90);
        }

        static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int h = (int)(seconds / 3600), m = (int)(seconds % 3600 / 60);
            return h > 0 ? h + " Std. " + m.ToString("00") + " Min." : m + " Min.";
        }

        /// <summary>Zahl mit deutschem Tausenderpunkt (ohne Kulturabhängigkeit).</summary>
        static string Num(long v)
        {
            bool neg = v < 0;
            string d = (neg ? -v : v).ToString();
            if (d.Length <= 3) return neg ? "-" + d : d;
            var sb = new System.Text.StringBuilder(d.Length + d.Length / 3 + 1);
            if (neg) sb.Append('-');
            int first = d.Length % 3;
            if (first > 0) sb.Append(d, 0, first);
            for (int i = first; i < d.Length; i += 3)
            {
                if (sb.Length > (neg ? 1 : 0)) sb.Append('.');
                sb.Append(d, i, 3);
            }
            return sb.ToString();
        }

        string KeyHint(GameAction a) { return "[" + InputMap.Label(a) + "]"; }
    }
}
