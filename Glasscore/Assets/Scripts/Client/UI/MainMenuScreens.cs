using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>Main menu (Hauptmenü), Join Lobby dialog, matchmaking overlay and Customization.</summary>
    public sealed class MainMenuScreens
    {
        private static readonly string[] Items = { "QUICK MATCH", "CREATE LOBBY", "JOIN LOBBY", "CUSTOMIZATION", "SETTINGS", "QUIT" };
        private int _focus;
        private string _joinCode = string.Empty;
        private int _previewWeapon;

        public void Draw(GameApp app)
        {
            DrawTitle(app);
            switch (app.Screen)
            {
                case AppScreen.MainMenu: DrawMain(app); break;
                case AppScreen.JoinLobby: DrawMain(app, interactive: false); DrawJoin(app); break;
                case AppScreen.Searching:
                case AppScreen.Connecting: DrawMain(app, interactive: false); DrawSearching(app); break;
                case AppScreen.Customization: DrawCustomization(app); break;
            }
        }

        private static void DrawTitle(GameApp app)
        {
            // "GLASSCORE" — futuristic minimalist neon, pulsing cyan #00FFFF.
            float pulse = Ui.Pulse(2.2f, 0.55f);
            var cyan = new Color(0f, 1f, 1f, pulse);
            Ui.Label(new Rect(110, 90, 1000, 150), "GLASSCORE", 132, cyan, TextAnchor.MiddleLeft, glow: true);
            Ui.Label(new Rect(118, 225, 1000, 40), "Wer im Glashaus sitzt, sollte nicht mit Steinen werfen.", 24, new Color(0.7f, 0.95f, 1f, 0.75f));
            Ui.Label(new Rect(Ui.Width - 620, Ui.Height - 60, 590, 40), $"{app.Profile.PlayerName}  ·  Rating {app.Profile.Rating}", 22, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight);
        }

        private void DrawMain(GameApp app, bool interactive = true)
        {
            if (interactive && !app.SettingsOpen)
            {
                _focus = Ui.NavigateFocus(_focus, Items.Length, app.Input);
                if (app.Input.MenuConfirm) { Ui.Click(); Activate(app, _focus); return; }
            }

            float x = 120, y = 330, w = 440, h = 66, gap = 18;
            for (int i = 0; i < Items.Length; i++)
            {
                var r = new Rect(x, y + i * (h + gap), w, h);
                bool clicked = Ui.NeonButton("main." + i, r, Items[i], 30, focused: interactive && _focus == i && app.Input.UsingGamepad, enabled: interactive && !app.SettingsOpen);
                if (clicked) { _focus = i; Activate(app, i); }
            }
        }

        private void Activate(GameApp app, int index)
        {
            switch (index)
            {
                case 0: app.QuickMatch(); break;
                case 1: app.CreateLobby(); break;
                case 2: _joinCode = string.Empty; app.Screen = AppScreen.JoinLobby; break;
                case 3: app.Screen = AppScreen.Customization; break;
                case 4: app.OpenSettings(); break;
                case 5: app.Quit(); break;
            }
        }

        private void DrawJoin(GameApp app)
        {
            var r = new Rect(Ui.Width / 2 - 360, 380, 720, 330);
            Ui.PanelBox(r, "JOIN LOBBY");
            Ui.Label(new Rect(r.x + 30, r.y + 60, r.width - 60, 40), "Enter the 6-character friend code (or host:port for a dedicated server):", 20, new Color(1, 1, 1, 0.8f));
            var field = new Rect(r.x + 30, r.y + 115, r.width - 60, 70);
            _joinCode = Ui.TextField("joincode", field, _joinCode, 40);
            GUI.FocusControl("joincode");

            bool submit = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            string normalized = LobbyCode.Normalize(_joinCode);
            Ui.Label(new Rect(r.x + 30, r.y + 190, r.width - 60, 30),
                LobbyCode.IsValid(normalized) ? $"Code {normalized}" : (_joinCode.Contains(".") || _joinCode.Contains(":") ? "Direct connect" : " "), 20, Ui.Cyan);

            if (Ui.NeonButton("join.go", new Rect(r.x + 30, r.yMax - 90, 300, 60), "JOIN", 28) || submit)
            {
                if (submit) Event.current.Use();
                app.JoinLobby(_joinCode);
            }
            if (Ui.NeonButton("join.back", new Rect(r.xMax - 330, r.yMax - 90, 300, 60), "BACK", 28) || app.Input.MenuBack)
                app.Screen = AppScreen.MainMenu;
        }

        private static void DrawSearching(GameApp app)
        {
            var r = new Rect(Ui.Width / 2 - 380, 420, 760, 240);
            Ui.PanelBox(r, app.Screen == AppScreen.Searching ? "QUICK MATCH" : "CONNECTING");
            float t = Time.unscaledTime;
            string dots = new string('.', 1 + (int)(t * 3) % 3);
            Ui.Label(new Rect(r.x + 30, r.y + 70, r.width - 60, 40), app.SearchStatus + dots, 24, Color.white);
            if (app.Screen == AppScreen.Searching)
            {
                float elapsed = t - app.SearchStartedAt;
                Ui.Label(new Rect(r.x + 30, r.y + 110, r.width - 60, 30), $"Elapsed {elapsed:0}s · skill window widens every {MatchmakingPolicy.WindowStepSeconds:0}s", 18, new Color(1, 1, 1, 0.6f));
                Ui.Bar(new Rect(r.x + 30, r.y + 150, r.width - 60, 6), Mathf.Repeat(t * 0.5f, 1f), Ui.Cyan);
                if (Ui.NeonButton("search.cancel", new Rect(r.center.x - 150, r.yMax - 75, 300, 56), "CANCEL", 26) || app.Input.MenuBack)
                    app.CancelSearch();
            }
        }

        // ───────────────────────────── customization ─────────────────────────────

        private void DrawCustomization(GameApp app)
        {
            PlayerProfile p = app.Profile;
            var panel = new Rect(110, 300, Ui.Width - 220, 640);
            Ui.PanelBox(panel, "CUSTOMIZATION");
            Ui.Label(new Rect(panel.xMax - 520, panel.y + 12, 500, 32), "Cloud: " + app.Cloud.Status, 18, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);

            Ui.Label(new Rect(panel.x + 30, panel.y + 70, 400, 34), "PLAYER NAME", 20, Ui.Cyan);
            p.PlayerName = Ui.TextField("profile.name", new Rect(panel.x + 30, panel.y + 105, 400, 52), p.PlayerName, LobbyRules.PlayerNameMaxLength);

            Ui.Label(new Rect(panel.x + 30, panel.y + 185, 600, 34), "WEAPON SKINS", 20, Ui.Cyan);
            for (int w = 0; w < WeaponCatalog.Count; w++)
            {
                var row = new Rect(panel.x + 30, panel.y + 225 + w * 70, 760, 60);
                WeaponSpec spec = WeaponCatalog.Get(w);
                bool sel = _previewWeapon == w;
                if (sel) Ui.Rect(row, new Color(0f, 1f, 1f, 0.07f));
                Ui.Label(new Rect(row.x + 10, row.y, 380, row.height), spec.DisplayName, 20, sel ? Color.white : new Color(1, 1, 1, 0.8f));
                int skin = p.WeaponSkins[w];
                if (Ui.NeonButton("skin.prev." + w, new Rect(row.x + 400, row.y + 8, 44, 44), "◀", 22)) { skin = (skin - 1 + Cosmetics.WeaponSkins.Length) % Cosmetics.WeaponSkins.Length; _previewWeapon = w; }
                var s = Cosmetics.WeaponSkins[skin];
                Ui.Rect(new Rect(row.x + 456, row.y + 12, 36, 36), s.Primary);
                Ui.Frame(new Rect(row.x + 456, row.y + 12, 36, 36), s.Emission, 3f);
                Ui.Label(new Rect(row.x + 502, row.y, 170, row.height), s.Name, 20, Color.white);
                if (Ui.NeonButton("skin.next." + w, new Rect(row.x + 680, row.y + 8, 44, 44), "▶", 22)) { skin = (skin + 1) % Cosmetics.WeaponSkins.Length; _previewWeapon = w; }
                p.WeaponSkins[w] = skin;
            }

            var right = new Rect(panel.x + 860, panel.y + 70, panel.width - 890, 460);
            Ui.Label(new Rect(right.x, right.y, right.width, 34), "SUCTION-BOOT TRAIL", 20, Ui.Cyan);
            for (int t = 0; t < Cosmetics.Trails.Length; t++)
            {
                var tr = Cosmetics.Trails[t];
                var r = new Rect(right.x, right.y + 45 + t * 64, Mathf.Min(460, right.width), 54);
                if (Ui.NeonButton("trail." + t, r, tr.Name, 22, false, true, p.Trail == t ? Ui.Green : (Color?)null)) p.Trail = t;
                // Gradient preview strip.
                for (int k = 0; k < 20; k++)
                {
                    Color c = Color.Lerp(tr.Start, tr.End, k / 19f);
                    Ui.Rect(new Rect(r.xMax + 20 + k * 12, r.y + 20, 12, 14), c);
                }
            }

            if (Ui.NeonButton("custom.save", new Rect(panel.x + 30, panel.yMax - 85, 320, 60), "SAVE", 28))
            {
                app.SaveProfile();
                app.ShowNotice("Loadout saved.", 2.5f);
            }
            if (Ui.NeonButton("custom.back", new Rect(panel.xMax - 350, panel.yMax - 85, 320, 60), "BACK", 28) || app.Input.MenuBack)
            {
                app.SaveProfile();
                app.Screen = AppScreen.MainMenu;
            }
        }
    }
}
