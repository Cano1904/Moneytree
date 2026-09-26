using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Lobby-Verwaltung. Left: roster (name, ping, skin preview, ready ✓/✗). Right: match settings
    /// (host-only: map, player limit 2–8, friendly fire, max score, public/private, code). Bottom: chat.
    /// </summary>
    public sealed class LobbyScreen
    {
        private static readonly string[] Modes = { "Free-for-all", "Teams (2)" };
        private static readonly string[] Visibility = { "Public", "Private" };
        private string _chatInput = string.Empty;
        private LobbySettings _edit;
        private float _lastSend;
        private bool _dirty;
        private Vector2 _chatScroll;
        private int _seenChat;

        public void Draw(GameApp app)
        {
            GameClient c = app.Client;
            if (c == null) return;
            LobbyView lobby = c.Lobby;
            bool host = c.IsHost;
            RosterEntry me = lobby.Find(c.LocalSlot);

            Ui.Label(new Rect(60, 30, 900, 70), "LOBBY", 56, Ui.Cyan, TextAnchor.MiddleLeft, glow: true);
            Ui.Label(new Rect(62, 92, 900, 34), lobby.ServerName + (c.ServerDedicated ? "  ·  dedicated server" : string.Empty), 22, new Color(1, 1, 1, 0.65f));

            // Lobby code, big and copyable.
            var codeRect = new Rect(Ui.Width - 620, 36, 560, 90);
            Ui.PanelBox(codeRect);
            Ui.Label(new Rect(codeRect.x + 20, codeRect.y, 200, codeRect.height), "LOBBY CODE", 20, new Color(1, 1, 1, 0.7f));
            Ui.Label(new Rect(codeRect.x + 180, codeRect.y, 250, codeRect.height), lobby.Code, 46, Color.white, TextAnchor.MiddleLeft, glow: true);
            if (Ui.NeonButton("lobby.copy", new Rect(codeRect.xMax - 120, codeRect.y + 22, 100, 46), "COPY", 20))
            {
                GUIUtility.systemCopyBuffer = lobby.Code;
                app.ShowNotice($"Lobby code {lobby.Code} copied to clipboard.", 2.5f);
            }

            DrawRoster(app, lobby, host);
            DrawSettings(app, lobby, host);
            DrawChat(app);

            // Ready / Start / Leave.
            float y = Ui.Height - 110;
            bool ready = me != null && me.Ready;
            if (Ui.NeonButton("lobby.ready", new Rect(60, y, 320, 70), ready ? "✓ READY" : "READY", 30, false, true, ready ? Ui.Green : Ui.Cyan)
                || app.Input.PadPressed(KeyCode.JoystickButton0))
                c.SetReady(!ready);

            bool allReady = lobby.Roster.Count >= LobbyRules.MinPlayerLimit && lobby.Roster.TrueForAll(r => r.Ready);
            if (host)
            {
                if (Ui.NeonButton("lobby.start", new Rect(400, y, 360, 70), "START GAME", 30, false, allReady, Ui.Magenta)
                    || (allReady && app.Input.PadPressed(KeyCode.JoystickButton7)))
                    c.HostStart();
            }
            else
            {
                Ui.Label(new Rect(400, y, 520, 70), allReady ? "Waiting for the host to start…" : "Waiting for everyone to be READY…", 22, new Color(1, 1, 1, 0.7f));
            }

            if (c.LobbyCountdown >= 0f)
                Ui.Label(new Rect(Ui.Width / 2 - 400, Ui.Height / 2 - 60, 800, 120), $"MATCH STARTS IN {Mathf.CeilToInt(c.LobbyCountdown)}", 64, Color.white, TextAnchor.MiddleCenter, glow: true);

            if (Ui.NeonButton("lobby.settings", new Rect(Ui.Width - 700, y, 300, 70), "SETTINGS", 26)) app.OpenSettings();
            if (Ui.NeonButton("lobby.leave", new Rect(Ui.Width - 380, y, 320, 70), "LEAVE", 30, false, true, Ui.Red)
                || app.Input.PadPressed(KeyCode.JoystickButton6))
                app.LeaveToMenu();
        }

        private static void DrawRoster(GameApp app, LobbyView lobby, bool host)
        {
            var panel = new Rect(60, 150, 860, 560);
            Ui.PanelBox(panel, $"PLAYERS  {lobby.Roster.Count}/{lobby.Settings.MaxPlayers}");
            for (int i = 0; i < lobby.Roster.Count; i++)
            {
                RosterEntry e = lobby.Roster[i];
                var row = new Rect(panel.x + 16, panel.y + 60 + i * 60, panel.width - 32, 54);
                Color slotColor = Cosmetics.SlotColor(e.Slot);
                Ui.Rect(row, new Color(slotColor.r, slotColor.g, slotColor.b, e.Slot == app.Client.LocalSlot ? 0.14f : 0.06f));
                Ui.Rect(new Rect(row.x, row.y, 6, row.height), slotColor);

                string crown = e.Slot == lobby.HostSlot ? "★ " : "   ";
                string team = lobby.Settings.Teams ? (e.Team == 0 ? "  [CYAN]" : "  [MAGENTA]") : string.Empty;
                Ui.Label(new Rect(row.x + 16, row.y, 360, row.height), crown + e.Name + team + (e.Connected ? string.Empty : " (reconnecting)"), 24, Color.white);

                // Skin + trail preview swatches.
                var skin = Cosmetics.WeaponSkins[Mathf.Clamp(e.Skin, 0, Cosmetics.WeaponSkins.Length - 1)];
                Ui.Rect(new Rect(row.x + 390, row.y + 13, 28, 28), skin.Primary);
                Ui.Frame(new Rect(row.x + 390, row.y + 13, 28, 28), skin.Emission, 2f);
                var trail = Cosmetics.Trails[Mathf.Clamp(e.Trail, 0, Cosmetics.Trails.Length - 1)];
                Ui.Rect(new Rect(row.x + 426, row.y + 20, 40, 14), trail.Start);

                // Ping bars.
                LobbyRules.PingQuality q = LobbyRules.ClassifyPing(e.PingMs);
                Color pc = q == LobbyRules.PingQuality.Good ? Ui.Green : q == LobbyRules.PingQuality.Fair ? new Color(1f, 0.85f, 0.2f) : Ui.Red;
                int bars = q == LobbyRules.PingQuality.Good ? 3 : q == LobbyRules.PingQuality.Fair ? 2 : 1;
                for (int b = 0; b < 3; b++)
                    Ui.Rect(new Rect(row.x + 490 + b * 10, row.y + 36 - b * 8, 7, 8 + b * 8), b < bars ? pc : new Color(1, 1, 1, 0.15f));
                Ui.Label(new Rect(row.x + 526, row.y, 90, row.height), e.PingMs + " ms", 18, pc);

                Ui.Label(new Rect(row.x + 616, row.y, 70, row.height), e.Mmr.ToString(), 18, new Color(1, 1, 1, 0.5f));

                // Ready status: green check / red X.
                Ui.Label(new Rect(row.x + 690, row.y, 50, row.height), e.Ready ? "✓" : "✗", 34, e.Ready ? Ui.Green : Ui.Red, TextAnchor.MiddleCenter);

                if (host && e.Slot != app.Client.LocalSlot &&
                    Ui.NeonButton("kick." + e.Slot, new Rect(row.xMax - 86, row.y + 9, 76, 36), "KICK", 16, false, true, Ui.Red))
                    app.Client.HostKick(e.Slot);
            }
        }

        private void DrawSettings(GameApp app, LobbyView lobby, bool host)
        {
            if (_edit == null || !host || !_dirty) _edit = lobby.Settings.Clone();
            var panel = new Rect(Ui.Width - 900, 150, 840, 560);
            Ui.PanelBox(panel, host ? "MATCH SETTINGS" : "MATCH SETTINGS (host only)");
            float x = panel.x + 24, w = panel.width - 48, y = panel.y + 64, h = 54;

            string[] maps = new string[MapCatalog.All.Length];
            for (int i = 0; i < maps.Length; i++) maps[i] = MapCatalog.All[i].Name;
            var s = _edit;
            int map = Ui.Dropdown("set.map", new Rect(x, y, w, h), "Map", maps, s.MapId, host); y += h + 8;
            int maxPlayers = Mathf.RoundToInt(Ui.Slider("set.players", new Rect(x, y, w, h), "Player Limit", s.MaxPlayers, LobbyRules.MinPlayerLimit, LobbyRules.MaxPlayerLimit, s.MaxPlayers.ToString(), host)); y += h + 8;
            int maxScore = Mathf.RoundToInt(Ui.Slider("set.score", new Rect(x, y, w, h), "Max Score", s.MaxScore, MatchTimings.MinMaxScore, MatchTimings.MaxMaxScore, s.MaxScore.ToString(), host)); y += h + 8;
            int mode = Ui.Dropdown("set.mode", new Rect(x, y, w, h), "Mode", Modes, s.Teams ? 1 : 0, host); y += h + 8;
            bool ff = Ui.Toggle("set.ff", new Rect(x, y, w, h), "Friendly Fire" + (s.Teams ? string.Empty : "  (Teams only)"), s.FriendlyFire, host); y += h + 8;
            int vis = Ui.Dropdown("set.vis", new Rect(x, y, w, h), "Lobby Type", Visibility, s.IsPublic ? 0 : 1, host); y += h + 8;
            bool comp = Ui.Toggle("set.comp", new Rect(x, y, w, h), "Competitive (ranked, leaving costs rating)", s.Competitive, host); y += h + 8;

            if (!host) return;
            var next = new LobbySettings
            {
                MapId = (byte)map, MaxPlayers = (byte)maxPlayers, MaxScore = (byte)maxScore, Teams = mode == 1,
                FriendlyFire = ff, IsPublic = vis == 0, Competitive = comp,
            };
            if (!Same(next, _edit)) { _edit = next; _dirty = true; }
            if (_dirty && !Same(_edit, lobby.Settings) && Time.unscaledTime - _lastSend > 0.25f && GUIUtility.hotControl == 0)
            {
                app.Client.HostApplySettings(_edit);
                _lastSend = Time.unscaledTime;
            }
            if (_dirty && Same(_edit, lobby.Settings)) _dirty = false;
        }

        private static bool Same(LobbySettings a, LobbySettings b) =>
            a.MapId == b.MapId && a.MaxPlayers == b.MaxPlayers && a.MaxScore == b.MaxScore && a.Teams == b.Teams &&
            a.FriendlyFire == b.FriendlyFire && a.IsPublic == b.IsPublic && a.Competitive == b.Competitive;

        private void DrawChat(GameApp app)
        {
            var panel = new Rect(Ui.Width / 2 - 520, 725, 1040, 230);
            Ui.PanelBox(panel);
            var view = new Rect(panel.x + 12, panel.y + 10, panel.width - 24, panel.height - 70);
            float lineH = 30f;
            var content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, app.Chat.Count * lineH));
            if (app.Chat.Count != _seenChat)
            {
                _seenChat = app.Chat.Count;
                _chatScroll.y = content.height;
            }
            _chatScroll = GUI.BeginScrollView(view, _chatScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < app.Chat.Count; i++)
            {
                ChatLine l = app.Chat[i];
                Color nameColor = l.IsSystem ? new Color(1f, 0.85f, 0.3f) : Cosmetics.SlotColor(l.Slot);
                Ui.Label(new Rect(0, i * lineH, 200, lineH), l.IsSystem ? "•" : l.Name + ":", 20, nameColor, TextAnchor.MiddleRight);
                Ui.Label(new Rect(210, i * lineH, content.width - 210, lineH), l.Text, 20, l.IsSystem ? new Color(1, 1, 1, 0.6f) : Color.white);
            }
            GUI.EndScrollView();

            var field = new Rect(panel.x + 12, panel.yMax - 54, panel.width - 180, 44);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "chat";
            _chatInput = Ui.TextField("chat", field, _chatInput, ChatFilter.MaxLength);
            if ((Ui.NeonButton("chat.send", new Rect(panel.xMax - 156, panel.yMax - 54, 144, 44), "SEND", 20) || enter) && _chatInput.Trim().Length > 0)
            {
                app.Client.SendChat(_chatInput);
                _chatInput = string.Empty;
                if (enter) Event.current.Use();
            }
        }
    }
}
