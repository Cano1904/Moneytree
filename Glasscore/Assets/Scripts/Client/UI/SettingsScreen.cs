using System;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Einstellungen: Graphics, Audio (incl. VOIP), Controls (interactive remapping matrix) and Online.
    /// Opens from the main menu, the lobby, or on top of the pause menu. Values persist via PlayerPrefs.
    /// </summary>
    public sealed class SettingsScreen
    {
        private static readonly string[] Tabs = { "GRAPHICS", "AUDIO", "CONTROLS", "ONLINE" };
        private static readonly string[] ResolutionNames = { "1920 × 1080", "2560 × 1440", "3840 × 2160" };
        private static readonly string[] DisplayModes = { "Fullscreen", "Borderless Window", "Windowed" };
        private static readonly string[] QualityNames = { "Low · pre-fractured chunks", "Medium · real-time 20 pieces", "High · full Voronoi 100+" };

        private int _tab;
        private GameAction? _rebinding;
        private bool _rebindingPad;
        private int _rebindFrame;
        private string[] _micDevices = { GameSettings.SystemDefaultDevice };
        private Vector2 _scroll;

        public void Open(GameApp app)
        {
            _micDevices = VoiceChat.InputDevices();
            _rebinding = null;
        }

        public void Draw(GameApp app)
        {
            GameSettings s = app.Settings;
            Ui.Rect(new Rect(0, 0, Ui.Width, Ui.Height), new Color(0f, 0f, 0f, 0.55f));
            var panel = new Rect(Ui.Width / 2 - 720, 80, 1440, 900);
            Ui.PanelBox(panel);
            Ui.Label(new Rect(panel.x + 30, panel.y + 16, 500, 60), "SETTINGS", 44, Ui.Cyan, TextAnchor.MiddleLeft, glow: true);

            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rect(panel.x + 30 + i * 250, panel.y + 90, 230, 54);
                if (Ui.NeonButton("tab." + i, r, Tabs[i], 22, false, true, _tab == i ? Ui.Magenta : (Color?)null))
                {
                    _tab = i;
                    _rebinding = null;
                    Ui.CloseDropdowns();
                }
            }

            var body = new Rect(panel.x + 30, panel.y + 170, panel.width - 60, panel.height - 280);
            switch (_tab)
            {
                case 0: DrawGraphics(s, body); break;
                case 1: DrawAudio(app, s, body); break;
                case 2: DrawControls(app, s, body); break;
                case 3: DrawOnline(app, s, body); break;
            }

            float by = panel.yMax - 90;
            if (Ui.NeonButton("settings.apply", new Rect(panel.x + 30, by, 300, 64), "APPLY", 28))
            {
                s.Save();
                s.ApplyDisplay();
                app.SaveProfile();
                app.ShowNotice("Settings saved.", 2f);
            }
            if (Ui.NeonButton("settings.back", new Rect(panel.xMax - 330, by, 300, 64), "BACK", 28) || (_rebinding == null && app.Input.MenuBack))
            {
                s.Save();
                s.ApplyDisplay();
                app.CloseSettings();
            }
        }

        private static void DrawGraphics(GameSettings s, Rect r)
        {
            float x = r.x, w = 900, y = r.y, h = 58;
            s.ResolutionIndex = Ui.Dropdown("gfx.res", new Rect(x, y, w, h), "Resolution", ResolutionNames, s.ResolutionIndex); y += h + 14;
            s.DisplayMode = (DisplayModeOption)Ui.Dropdown("gfx.mode", new Rect(x, y, w, h), "Display Mode", DisplayModes, (int)s.DisplayMode); y += h + 14;
            int q = Mathf.RoundToInt(Ui.Slider("gfx.fracture", new Rect(x, y, w, h), "Fracture Physics Quality", (int)s.FractureQuality, 0, 2, s.FractureQuality.ToString()));
            s.FractureQuality = (FractureQuality)q; y += h;
            Ui.Label(new Rect(x + w * 0.44f, y - 10, w * 0.56f, 30), QualityNames[q], 18, new Color(1, 1, 1, 0.6f)); y += 34;
            s.VSync = Ui.Toggle("gfx.vsync", new Rect(x, y, w, h), "V-Sync", s.VSync); y += h + 14;
            s.FieldOfView = Mathf.Round(Ui.Slider("gfx.fov", new Rect(x, y, w, h), "Field of View", s.FieldOfView, 70f, 110f, s.FieldOfView.ToString("0") + "°")); y += h + 14;
            Ui.Label(new Rect(x, y + 10, w, 30), "Display changes apply with APPLY or BACK.", 18, new Color(1, 1, 1, 0.5f));
        }

        private void DrawAudio(GameApp app, GameSettings s, Rect r)
        {
            float x = r.x, w = 900, y = r.y, h = 54;
            s.MasterVolume = Ui.Slider("aud.master", new Rect(x, y, w, h), "Master Volume", s.MasterVolume, 0f, 1f, Pct(s.MasterVolume)); y += h + 10;
            s.SfxVolume = Ui.Slider("aud.sfx", new Rect(x, y, w, h), "SFX Volume (Glass ASMR)", s.SfxVolume, 0f, 1f, Pct(s.SfxVolume)); y += h + 10;
            s.MusicVolume = Ui.Slider("aud.music", new Rect(x, y, w, h), "Music Volume", s.MusicVolume, 0f, 1f, Pct(s.MusicVolume)); y += h + 22;

            Ui.Label(new Rect(x, y, w, 34), "VOICE CHAT (VOIP)", 20, Ui.Cyan); y += 40;
            s.VoiceEnabled = Ui.Toggle("aud.voip", new Rect(x, y, w, h), "Voice Chat", s.VoiceEnabled); y += h + 10;
            int micIndex = Math.Max(0, Array.IndexOf(_micDevices, s.VoiceInputDevice));
            micIndex = Ui.Dropdown("aud.mic", new Rect(x, y, w, h), "Input Device", _micDevices, micIndex, s.VoiceEnabled);
            s.VoiceInputDevice = _micDevices[Mathf.Clamp(micIndex, 0, _micDevices.Length - 1)]; y += h + 10;
            Ui.Dropdown("aud.out", new Rect(x, y, w, h), "Output Device", new[] { GameSettings.SystemDefaultDevice }, 0, s.VoiceEnabled); y += h + 2;
            Ui.Label(new Rect(x + w * 0.44f, y, w * 0.56f, 26), "Output follows the Windows default playback device.", 16, new Color(1, 1, 1, 0.45f)); y += 34;
            s.PushToTalk = Ui.Toggle("aud.ptt", new Rect(x, y, w, h), "Push-To-Talk (V)  ·  off = open mic with voice detection", s.PushToTalk, s.VoiceEnabled); y += h + 10;
            s.VoiceVolume = Ui.Slider("aud.voice", new Rect(x, y, w, h), "Voice Volume", s.VoiceVolume, 0f, 1f, Pct(s.VoiceVolume), s.VoiceEnabled); y += h + 10;
            if (!string.IsNullOrEmpty(app.Voice.MicError)) Ui.Label(new Rect(x, y, w, 30), app.Voice.MicError, 18, Ui.Red);
        }

        private void DrawControls(GameApp app, GameSettings s, Rect r)
        {
            // Rebinding capture: the next key/button press is bound (Esc cancels keyboard rebinding).
            // (skip the frame of the click itself, otherwise Left Click would bind instantly)
            if (_rebinding.HasValue && Event.current.type == EventType.Repaint && Time.frameCount > _rebindFrame)
            {
                KeyCode k = InputService.PollAnyKeyDown(_rebindingPad);
                if (k != KeyCode.None)
                {
                    if (!_rebindingPad && k == KeyCode.Escape && _rebinding.Value != GameAction.Pause) { _rebinding = null; }
                    else
                    {
                        (_rebindingPad ? s.Gamepad : s.Keyboard)[(int)_rebinding.Value] = k;
                        AudioService.Instance?.Play(AudioService.CrackSharp, 0.5f);
                        _rebinding = null;
                    }
                }
            }

            float x = r.x, y = r.y;
            Ui.Label(new Rect(x, y, 420, 36), "ACTION", 18, Ui.Cyan);
            Ui.Label(new Rect(x + 440, y, 300, 36), "KEYBOARD / MOUSE", 18, Ui.Cyan, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(x + 760, y, 300, 36), "GAMEPAD", 18, Ui.Cyan, TextAnchor.MiddleCenter);
            y += 40;

            var view = new Rect(x, y, 1100, r.height - 150);
            int count = Enum.GetValues(typeof(GameAction)).Length;
            var content = new Rect(0, 0, view.width - 20, count * 46f);
            _scroll = GUI.BeginScrollView(view, _scroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < count; i++)
            {
                var a = (GameAction)i;
                float ry = i * 46f;
                Ui.Label(new Rect(0, ry, 420, 42), GameSettings.ActionLabel(a), 20, Color.white);
                bool waitingKey = _rebinding == a && !_rebindingPad;
                bool waitingPad = _rebinding == a && _rebindingPad;
                string keyLabel = waitingKey ? "press a key…" : GameSettings.KeyLabel(s.Keyboard[i], a, false);
                string padLabel = waitingPad ? "press a button…" : GameSettings.KeyLabel(s.Gamepad[i], a, true);
                if (Ui.NeonButton("bind.key." + i, new Rect(440, ry + 2, 300, 40), keyLabel, 18, false, true, waitingKey ? Ui.Magenta : (Color?)null))
                {
                    _rebinding = a;
                    _rebindingPad = false;
                    _rebindFrame = Time.frameCount;
                }
                bool padBindable = a != GameAction.MoveForward && a != GameAction.MoveBack && a != GameAction.MoveLeft && a != GameAction.MoveRight;
                if (Ui.NeonButton("bind.pad." + i, new Rect(760, ry + 2, 300, 40), padLabel, 18, false, padBindable, waitingPad ? Ui.Magenta : (Color?)null))
                {
                    _rebinding = a;
                    _rebindingPad = true;
                    _rebindFrame = Time.frameCount;
                }
            }
            GUI.EndScrollView();

            float by = view.yMax + 10;
            s.MouseSensitivity = Ui.Slider("ctl.mouse", new Rect(x, by, 700, 44), "Mouse Sensitivity", s.MouseSensitivity, 0.2f, 6f, s.MouseSensitivity.ToString("0.0"));
            s.GamepadLookSpeed = Ui.Slider("ctl.pad", new Rect(x, by + 48, 700, 44), "Gamepad Look Speed", s.GamepadLookSpeed, 60f, 400f, s.GamepadLookSpeed.ToString("0"));
            s.InvertY = Ui.Toggle("ctl.invert", new Rect(x + 760, by, 340, 44), "Invert Y", s.InvertY);
            if (Ui.NeonButton("ctl.reset", new Rect(x + 760, by + 50, 340, 44), "RESET DEFAULTS", 18)) s.ResetBindings();
        }

        private static void DrawOnline(GameApp app, GameSettings s, Rect r)
        {
            float x = r.x, y = r.y;
            Ui.Label(new Rect(x, y, 600, 34), "PLAYER NAME", 20, Ui.Cyan); y += 40;
            app.Profile.PlayerName = Ui.TextField("online.name", new Rect(x, y, 500, 52), app.Profile.PlayerName, Simulation.LobbyRules.PlayerNameMaxLength); y += 80;

            Ui.Label(new Rect(x, y, 1100, 34), "DEDICATED SERVER HOSTS (comma separated, searched by Quick Match and lobby codes)", 20, Ui.Cyan); y += 40;
            s.ServerHosts = Ui.TextField("online.hosts", new Rect(x, y, 1100, 52), s.ServerHosts, 300); y += 80;

            Ui.Label(new Rect(x, y, 1100, 30), $"Rating: {app.Profile.Rating}   ·   Cloud save: {app.Cloud.Status}" + (app.Cloud.CloudEnabled ? string.Empty : " (set GLASSCORE_CLOUD_URL to sync)"), 20, Color.white); y += 40;
            Ui.Label(new Rect(x, y, 1100, 30), "Clips folder: " + ClipRecorder.ClipDirectory, 18, new Color(1, 1, 1, 0.6f)); y += 36;
            if (Ui.NeonButton("online.clips", new Rect(x, y, 320, 48), "OPEN CLIPS FOLDER", 18)) ClipRecorder.OpenClipFolder();
        }

        private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }
}
