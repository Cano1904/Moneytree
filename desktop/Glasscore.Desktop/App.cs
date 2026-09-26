using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Glasscore.Net;
using Glasscore.Simulation;
using Raylib_cs;

namespace Glasscore.Desktop
{
    public enum Screen { MainMenu, Customization, Join, Busy, Lobby, Match }

    public sealed class LookState
    {
        public float Yaw, Pitch;
    }

    /// <summary>Makes `await` continue on the render thread (raylib is single-threaded).</summary>
    public sealed class MainThreadContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback, object)> _queue = new ConcurrentQueue<(SendOrPostCallback, object)>();
        public override void Post(SendOrPostCallback d, object state) => _queue.Enqueue((d, state));
        public override void Send(SendOrPostCallback d, object state) => _queue.Enqueue((d, state));
        public void Pump()
        {
            while (_queue.TryDequeue(out var item)) item.Item1(item.Item2);
        }
    }

    public sealed class App
    {
        private static readonly string[] MenuItems = { "QUICK MATCH", "CREATE LOBBY", "JOIN LOBBY", "CUSTOMIZATION", "SETTINGS", "QUIT" };
        private static readonly string[] Tabs = { "GRAPHICS", "AUDIO", "CONTROLS", "ONLINE" };
        private static readonly string[] Modes = { "Free-for-all", "Teams (2)" };
        private static readonly string[] Visibility = { "Public", "Private" };
        private static readonly string[] Quality = { "Low", "Medium", "High" };

        public readonly DesktopConfig Config;
        public readonly LookState Look = new LookState();
        public GameClient Client { get; private set; }
        public Screen Screen = Screen.MainMenu;
        public bool QuitRequested;

        private readonly DesktopInput _input;
        private readonly List<ChatLine> _chat = new List<ChatLine>();
        private GameServer _hosted;
        private MatchView _match;
        private bool _settingsOpen, _paused, _confirmLeave, _searching;
        private string _busyText = "";
        private double _searchStarted;
        private string _notice;
        private double _noticeUntil;
        private string _joinCode = "";
        private string _chatInput = "";
        private int _menuFocus, _pauseFocus, _tab;
        private int _rebinding = -1;
        private double _rebindTime;
        private LobbySettings _edit;
        private double _lastSettingsSend;
        private int _lastBeep = -1;
        private RenderTexture2D _sceneTarget;
        private Shader _blur;
        private int _blurTexelLoc, _blurAmountLoc;
        private float _blurAmount;
        private readonly MenuPane _pane = new MenuPane();

        public App(DesktopConfig config)
        {
            Config = config;
            _input = new DesktopInput(config, Look);
            _pane.Init();
            _blur = Raylib.LoadShaderFromMemory(null, BlurShader);
            _blurTexelLoc = Raylib.GetShaderLocation(_blur, "texel");
            _blurAmountLoc = Raylib.GetShaderLocation(_blur, "amount");
        }

        public string NameOf(int slot) => Client?.Lobby.Find(slot)?.Name ?? "Player " + (slot + 1);
        public RosterEntry Lobby(int slot) => Client?.Lobby.Find(slot);

        private void Notice(string text, double seconds = 5)
        {
            _notice = text;
            _noticeUntil = Raylib.GetTime() + seconds;
        }

        // ───────────────────────────── frame ─────────────────────────────

        public void Frame()
        {
            float dt = Math.Min(Raylib.GetFrameTime(), 0.1f);
            bool inMatch = Screen == Screen.Match && Client != null && Client.Status == ClientStatus.Match && _match != null;
            bool gameplay = inMatch && !_paused && !_settingsOpen;
            _input.Gameplay = gameplay;
            _input.Update(dt);
            if (inMatch && !_settingsOpen && _input.PausePressed) _paused = !_paused;
            if (gameplay && Raylib.IsWindowFocused()) { if (!Raylib.IsCursorHidden()) Raylib.DisableCursor(); }
            else if (Raylib.IsCursorHidden()) Raylib.EnableCursor();

            Client?.Update(dt, Raylib.GetTime());
            _match?.Update(dt);
            if (Raylib.IsKeyPressed(KeyboardKey.F12)) TakeScreenshot();

            EnsureTarget();
            Raylib.BeginTextureMode(_sceneTarget);
            Raylib.ClearBackground(new Color(3, 8, 16, 255));
            DrawSky();
            if (inMatch)
            {
                Raylib.BeginMode3D(_match.Camera);
                _match.Draw3D();
                Raylib.EndMode3D();
            }
            else
            {
                _pane.Draw();
            }
            Raylib.EndTextureMode();

            _blurAmount = MoveTowards(_blurAmount, inMatch && (_paused || _settingsOpen) ? 3f : 0f, dt * 12f);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            var src = new Rectangle(0, 0, _sceneTarget.Texture.Width, -_sceneTarget.Texture.Height);
            if (_blurAmount > 0.05f)
            {
                // Pause: the match keeps running underneath a live Gaussian blur.
                Raylib.SetShaderValue(_blur, _blurTexelLoc, new Vector2(1f / _sceneTarget.Texture.Width, 1f / _sceneTarget.Texture.Height), ShaderUniformDataType.Vec2);
                Raylib.SetShaderValue(_blur, _blurAmountLoc, _blurAmount, ShaderUniformDataType.Float);
                Raylib.BeginShaderMode(_blur);
                Raylib.DrawTextureRec(_sceneTarget.Texture, src, Vector2.Zero, Color.White);
                Raylib.EndShaderMode();
            }
            else Raylib.DrawTextureRec(_sceneTarget.Texture, src, Vector2.Zero, Color.White);

            Ui.BeginFrame();
            Ui.Modal = _settingsOpen;
            switch (Screen)
            {
                case Screen.MainMenu:
                case Screen.Join:
                case Screen.Busy:
                    DrawMainMenu();
                    break;
                case Screen.Customization: DrawCustomization(); break;
                case Screen.Lobby: DrawLobby(); break;
                case Screen.Match: DrawMatch(); break;
            }
            Ui.Modal = false;
            if (_settingsOpen) DrawSettings();
            if (!string.IsNullOrEmpty(_notice) && Raylib.GetTime() < _noticeUntil)
            {
                Ui.Rect(Ui.Width / 2 - 560, Ui.Height - 150, 1120, 56, new Color(13, 5, 8, 235));
                Ui.Frame(Ui.Width / 2 - 560, Ui.Height - 150, 1120, 56, Palette.Magenta);
                Ui.TextIn(_notice, Ui.Width / 2 - 560, Ui.Height - 150, 1120, 56, 22, Color.White, 1);
            }
            Ui.EndFrame();
            Raylib.EndDrawing();
        }

        private void EnsureTarget()
        {
            int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
            if (_sceneTarget.Texture.Width == w && _sceneTarget.Texture.Height == h) return;
            if (_sceneTarget.Id != 0) Raylib.UnloadRenderTexture(_sceneTarget);
            _sceneTarget = Raylib.LoadRenderTexture(w, h);
            Raylib.SetTextureFilter(_sceneTarget.Texture, TextureFilter.Bilinear);
        }

        private static void DrawSky()
        {
            int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
            Raylib.DrawRectangleGradientV(0, 0, w, h / 2, new Color(3, 5, 15, 255), new Color(13, 46, 71, 255));
            Raylib.DrawRectangleGradientV(0, h / 2, w, h - h / 2, new Color(13, 46, 71, 255), new Color(0, 0, 3, 255));
            var rng = new Random(7);
            for (int i = 0; i < 140; i++) Raylib.DrawPixel(rng.Next(w), rng.Next(h / 2), new Color(255, 255, 255, rng.Next(60, 200)));
        }

        private void TakeScreenshot()
        {
            string dir = System.IO.Path.Combine(DesktopConfig.Directory, "Clips");
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, $"glasscore_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Image img = Raylib.LoadImageFromScreen();
            Raylib.ExportImage(img, file);
            Raylib.UnloadImage(img);
            Notice("Screenshot saved: " + file, 4);
        }

        // ───────────────────────────── main menu ─────────────────────────────

        private void DrawMainMenu()
        {
            float pulse = Ui.Pulse(2.2f, 0.55f);
            Ui.Text("GLASSCORE", 110, 80, 132, Palette.WithAlpha(Palette.Cyan, pulse), glow: true);
            Ui.Text("Wer im Glashaus sitzt, sollte nicht mit Steinen werfen.", 118, 225, 24, new Color(180, 240, 255, 190));
            Ui.TextIn($"{Config.PlayerName}  -  Rating {Config.Rating}", Ui.Width - 700, Ui.Height - 60, 670, 40, 22, new Color(255, 255, 255, 150), 2);

            bool interactive = Screen == Screen.MainMenu && !_settingsOpen;
            if (interactive)
            {
                _menuFocus = (_menuFocus + _input.MenuNavigate() + MenuItems.Length) % MenuItems.Length;
                if (_input.MenuConfirm) { Ui.ClickFeedback(); Activate(_menuFocus); }
            }
            for (int i = 0; i < MenuItems.Length; i++)
            {
                if (Ui.Button("main." + i, 120, 330 + i * 84, 440, 66, MenuItems[i], 30, interactive && _input.UsingGamepad && _menuFocus == i, interactive))
                {
                    _menuFocus = i;
                    Activate(i);
                }
            }

            if (Screen == Screen.Join) DrawJoin();
            if (Screen == Screen.Busy) DrawBusy();
        }

        private void Activate(int i)
        {
            switch (i)
            {
                case 0: QuickMatch(); break;
                case 1: CreateLobby(); break;
                case 2: _joinCode = ""; Ui.Focus = "join"; Screen = Screen.Join; break;
                case 3: Screen = Screen.Customization; break;
                case 4: OpenSettings(); break;
                case 5: QuitRequested = true; break;
            }
        }

        private void DrawJoin()
        {
            float x = Ui.Width / 2 - 360, y = 380;
            Ui.Panel(x, y, 720, 330, "JOIN LOBBY");
            Ui.Text("6-character friend code, or host:port for a dedicated server:", x + 30, y + 64, 20, new Color(255, 255, 255, 200));
            Ui.Focus = "join";
            _joinCode = Ui.TextField("join", x + 30, y + 110, 660, 70, _joinCode, 40, 34);
            string norm = LobbyCode.Normalize(_joinCode);
            Ui.Text(LobbyCode.IsValid(norm) ? "Code " + norm : (_joinCode.Contains('.') || _joinCode.Contains(':') ? "Direct connect" : ""), x + 30, y + 192, 20, Palette.Cyan);
            if (Ui.Button("join.go", x + 30, y + 240, 300, 60, "JOIN", 28) || Raylib.IsKeyPressed(KeyboardKey.Enter)) JoinLobby(_joinCode);
            if (Ui.Button("join.back", x + 390, y + 240, 300, 60, "BACK", 28) || _input.MenuBack) { Ui.Focus = null; Screen = Screen.MainMenu; }
        }

        private void DrawBusy()
        {
            float x = Ui.Width / 2 - 380, y = 420;
            Ui.Panel(x, y, 760, 240, _searching ? "QUICK MATCH" : "CONNECTING");
            string dots = new string('.', 1 + (int)(Raylib.GetTime() * 3) % 3);
            Ui.Text(_busyText + dots, x + 30, y + 70, 24, Color.White);
            if (_searching)
            {
                Ui.Text($"Elapsed {Raylib.GetTime() - _searchStarted:0}s - skill window widens every 5s", x + 30, y + 110, 18, new Color(255, 255, 255, 150));
                Ui.Bar(x + 30, y + 150, 700, 6, (float)(Raylib.GetTime() * 0.5 % 1.0), Palette.Cyan);
                if (Ui.Button("busy.cancel", x + 230, y + 170, 300, 54, "CANCEL", 24) || _input.MenuBack) { _searching = false; Screen = Screen.MainMenu; }
            }
        }

        // ───────────────────────────── customization ─────────────────────────────

        private void DrawCustomization()
        {
            float x = 110, y = 300, w = Ui.Width - 220;
            Ui.Text("GLASSCORE", 110, 80, 90, Palette.WithAlpha(Palette.Cyan, Ui.Pulse(2.2f, 0.55f)), glow: true);
            Ui.Panel(x, y, w, 640, "CUSTOMIZATION");
            Ui.Text("PLAYER NAME", x + 30, y + 70, 20, Palette.Cyan);
            Config.PlayerName = Ui.TextField("name", x + 30, y + 100, 420, 54, Config.PlayerName, LobbyRules.PlayerNameMaxLength);
            Ui.Text("WEAPON SKINS", x + 30, y + 185, 20, Palette.Cyan);
            for (int wpn = 0; wpn < WeaponCatalog.Count; wpn++)
            {
                float ry = y + 225 + wpn * 70;
                Ui.TextIn(WeaponCatalog.Get(wpn).DisplayName, x + 30, ry, 380, 60, 21, Color.White);
                int skin = Config.WeaponSkins[wpn];
                if (Ui.Button("sk.l" + wpn, x + 420, ry + 8, 44, 44, "<", 24)) skin = (skin + Palette.Skins.Length - 1) % Palette.Skins.Length;
                var s = Palette.Skins[skin];
                Ui.Rect(x + 476, ry + 12, 36, 36, s.Body);
                Ui.Frame(x + 476, ry + 12, 36, 36, s.Glow, 3);
                Ui.TextIn(s.Name, x + 522, ry, 170, 60, 20, Color.White);
                if (Ui.Button("sk.r" + wpn, x + 700, ry + 8, 44, 44, ">", 24)) skin = (skin + 1) % Palette.Skins.Length;
                Config.WeaponSkins[wpn] = skin;
            }
            float rx = x + 880;
            Ui.Text("SUCTION-BOOT TRAIL", rx, y + 70, 20, Palette.Cyan);
            for (int t = 0; t < Palette.Trails.Length; t++)
            {
                if (Ui.Button("trail" + t, rx, y + 110 + t * 64, 420, 54, Palette.Trails[t].Name, 22, false, true, Config.Trail == t ? Palette.Green : (Color?)null)) Config.Trail = t;
                Ui.Rect(rx + 440, y + 130 + t * 64, 160, 14, Palette.Trails[t].Color);
            }
            if (Ui.Button("cust.save", x + 30, y + 555, 320, 60, "SAVE", 28)) { SaveProfile(); Notice("Loadout saved.", 2.5); }
            if (Ui.Button("cust.back", x + w - 350, y + 555, 320, 60, "BACK", 28) || (_input.MenuBack && Ui.Focus == null)) { SaveProfile(); Screen = Screen.MainMenu; }
        }

        private void SaveProfile()
        {
            Config.Validate();
            Config.Save();
            if (Client != null && Client.Status != ClientStatus.Disconnected) Client.SetProfile(Config.PlayerName, (byte)Config.WeaponSkins[0], (byte)Config.Trail);
        }

        // ───────────────────────────── networking flows ─────────────────────────────

        private async void QuickMatch()
        {
            if (_searching) return;
            _searching = true;
            Screen = Screen.Busy;
            _searchStarted = Raylib.GetTime();
            try
            {
                while (_searching)
                {
                    float elapsed = (float)(Raylib.GetTime() - _searchStarted);
                    int window = MatchmakingPolicy.WindowAt(elapsed);
                    _busyText = window == int.MaxValue ? "Searching all skill levels" : $"Searching rating {Config.Rating} +/- {window}";
                    List<DiscoveryInfo> found = await DiscoveryClient.SearchAsync("", Config.ServerHostList, 800);
                    if (!_searching) return;
                    DiscoveryInfo best = found.Where(f => f.IsPublic && f.Joinable && (window == int.MaxValue || Math.Abs(f.AverageMmr - Config.Rating) <= window))
                        .OrderByDescending(f => f.Dedicated).ThenBy(f => Math.Abs(f.AverageMmr - Config.Rating)).ThenBy(f => f.RoundTripMs).FirstOrDefault();
                    if (best != null)
                    {
                        _searching = false;
                        await ConnectTo(best.Address.ToString(), best.GamePort);
                        return;
                    }
                    if (elapsed >= MatchmakingPolicy.CreateSessionAfterSeconds)
                    {
                        _searching = false;
                        await HostLobby(true);
                        Notice("No open match found - hosting a public lobby. Add bots or wait for players.", 6);
                        return;
                    }
                    await Task.Delay(1200);
                }
            }
            catch (Exception ex)
            {
                _searching = false;
                Fail("Matchmaking failed: " + ex.Message);
            }
        }

        private async void CreateLobby()
        {
            try { await HostLobby(false); }
            catch (Exception ex) { Fail("Could not create lobby: " + ex.Message); }
        }

        private async Task HostLobby(bool isPublic)
        {
            StopHosted();
            Screen = Screen.Busy;
            _busyText = "Creating lobby";
            Exception last = null;
            for (int port = Protocol.DefaultPort; port < Protocol.DefaultPort + 10 && _hosted == null; port++)
            {
                var server = new GameServer(new ServerConfig
                {
                    Port = port,
                    ServerName = Config.PlayerName + "'s Lobby",
                    Settings = new LobbySettings { IsPublic = isPublic, MaxPlayers = 8 },
                    DataDirectory = System.IO.Path.Combine(DesktopConfig.Directory, "server"),
                });
                try { server.StartThread(); _hosted = server; }
                catch (SocketException ex) { server.Dispose(); last = ex; }
            }
            if (_hosted == null) throw last ?? new Exception("No free port.");
            await ConnectTo("127.0.0.1", _hosted.Port);
        }

        private async void JoinLobby(string input)
        {
            string raw = (input ?? "").Trim();
            Ui.Focus = null;
            try
            {
                if (raw.Contains('.') || raw.Contains(':') || raw.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    string host = raw;
                    int port = Protocol.DefaultPort;
                    int colon = raw.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(raw.Substring(colon + 1), out int p)) { host = raw.Substring(0, colon); port = p; }
                    await ConnectTo(host, port);
                    return;
                }
                string code = LobbyCode.Normalize(raw);
                if (!LobbyCode.IsValid(code)) { Notice("Lobby codes have 6 characters (A-Z without I/O, digits 2-9)."); return; }
                Screen = Screen.Busy;
                _busyText = "Looking for lobby " + code;
                List<DiscoveryInfo> found = await DiscoveryClient.SearchAsync(code, Config.ServerHostList, 1200);
                DiscoveryInfo hit = found.FirstOrDefault(f => f.Code == code);
                if (hit == null) { Fail($"Lobby {code} not found on your network or configured servers."); return; }
                if (hit.InMatch) { Fail($"Lobby {code} is in a match - try again when it is back in the lobby."); return; }
                await ConnectTo(hit.Address.ToString(), hit.GamePort);
            }
            catch (Exception ex) { Fail("Join failed: " + ex.Message); }
        }

        private async Task ConnectTo(string host, int port)
        {
            Screen = Screen.Busy;
            _busyText = $"Connecting to {host}:{port}";
            DisposeClient();
            _chat.Clear();
            var c = new GameClient { InputProvider = _input.Sample };
            c.OnConnected += () => { Screen = Screen.Lobby; };
            c.OnDisconnected += OnDisconnected;
            c.OnChat += l => { _chat.Add(l); if (_chat.Count > 60) _chat.RemoveAt(0); };
            c.OnMatchStart += OnMatchStart;
            c.OnReturnToLobby += () => { TearDownMatch(); _paused = false; Screen = Screen.Lobby; };
            c.OnResults += () =>
            {
                foreach (var r in c.Results) if (r.Slot == c.LocalSlot) { Config.Rating = r.NewRating; Config.Save(); }
            };
            Client = c;
            try { await c.ConnectAsync(host, port, Config.PlayerName, Config.PlayerKey, Config.Rating, (byte)Config.WeaponSkins[0], (byte)Config.Trail); }
            catch (Exception ex) { Fail($"Could not connect to {host}:{port} - {ex.Message}"); }
        }

        private void OnMatchStart(MatchInfo info)
        {
            TearDownMatch();
            _match = new MatchView(this, Client, info);
            Screen = Screen.Match;
            _paused = false;
            _confirmLeave = false;
            Look.Yaw = 0f;
            Look.Pitch = 0f;
            Client.SendSceneLoaded();
        }

        private void OnDisconnected(string reason)
        {
            bool wasIn = Screen == Screen.Lobby || Screen == Screen.Match || Screen == Screen.Busy;
            TearDownMatch();
            StopHosted();
            _paused = false;
            Screen = Screen.MainMenu;
            if (wasIn && !string.IsNullOrEmpty(reason)) Notice(reason, 6);
        }

        private void Fail(string message)
        {
            DisposeClient();
            StopHosted();
            Screen = Screen.MainMenu;
            Notice(message, 7);
        }

        private void LeaveToMenu()
        {
            if (Client != null)
            {
                if (Client.Status == ClientStatus.Match) Client.LeaveMatch();
                else Client.Disconnect("Left the lobby.");
            }
            TearDownMatch();
            DisposeClient();
            StopHosted();
            _paused = false;
            Screen = Screen.MainMenu;
        }

        private void TearDownMatch()
        {
            _match?.Detach();
            _match = null;
        }

        private void DisposeClient()
        {
            if (Client == null) return;
            Client.OnDisconnected -= OnDisconnected;
            Client.Dispose();
            Client = null;
        }

        private void StopHosted()
        {
            _hosted?.Dispose();
            _hosted = null;
        }

        public void Shutdown()
        {
            Config.Save();
            DisposeClient();
            StopHosted();
        }

        // ───────────────────────────── lobby ─────────────────────────────

        private void DrawLobby()
        {
            GameClient c = Client;
            if (c == null) return;
            LobbyView lobby = c.Lobby;
            bool host = c.IsHost;
            RosterEntry me = lobby.Find(c.LocalSlot);

            Ui.Text("LOBBY", 60, 26, 56, Palette.Cyan, glow: true);
            Ui.Text(lobby.ServerName + (c.ServerDedicated ? "  -  dedicated server" : ""), 62, 94, 22, new Color(255, 255, 255, 170));
            float cx = Ui.Width - 620;
            Ui.Panel(cx, 36, 560, 90);
            Ui.TextIn("LOBBY CODE", cx + 20, 36, 160, 90, 20, new Color(255, 255, 255, 180));
            Ui.TextIn(lobby.Code, cx + 180, 36, 250, 90, 46, Color.White, 0, glow: true);
            if (Ui.Button("copy", cx + 440, 58, 100, 46, "COPY", 20)) { Raylib.SetClipboardText(lobby.Code); Notice($"Lobby code {lobby.Code} copied.", 2.5); }

            // Roster.
            Ui.Panel(60, 150, 860, 560, $"PLAYERS  {lobby.Roster.Count}/{lobby.Settings.MaxPlayers}");
            for (int i = 0; i < lobby.Roster.Count; i++)
            {
                RosterEntry e = lobby.Roster[i];
                float ry = 210 + i * 60;
                Color sc = Palette.Slot(e.Slot);
                Ui.Rect(76, ry, 828, 54, Palette.WithAlpha(sc, e.Slot == c.LocalSlot ? 0.14f : 0.06f));
                Ui.Rect(76, ry, 6, 54, sc);
                string team = lobby.Settings.Teams ? (e.Team == 0 ? "  [CYAN]" : "  [MAGENTA]") : "";
                Ui.TextIn((e.Slot == lobby.HostSlot ? "* " : "  ") + e.Name + team, 92, ry, 360, 54, 24, Color.White);
                var skin = Palette.Skins[Math.Clamp((int)e.Skin, 0, Palette.Skins.Length - 1)];
                Ui.Rect(466, ry + 13, 28, 28, skin.Body);
                Ui.Frame(466, ry + 13, 28, 28, skin.Glow, 2);
                Ui.Rect(502, ry + 20, 40, 14, Palette.Trails[Math.Clamp((int)e.Trail, 0, Palette.Trails.Length - 1)].Color);
                var q = LobbyRules.ClassifyPing(e.PingMs);
                Color pc = q == LobbyRules.PingQuality.Good ? Palette.Green : q == LobbyRules.PingQuality.Fair ? Palette.Gold : Palette.Red;
                int bars = q == LobbyRules.PingQuality.Good ? 3 : q == LobbyRules.PingQuality.Fair ? 2 : 1;
                for (int b = 0; b < 3; b++) Ui.Rect(566 + b * 10, ry + 36 - b * 8, 7, 8 + b * 8, b < bars ? pc : new Color(255, 255, 255, 40));
                Ui.TextIn(e.PingMs + " ms", 602, ry, 90, 54, 18, pc);
                Ui.TextIn(e.Mmr.ToString(), 692, ry, 70, 54, 18, new Color(255, 255, 255, 130));
                Ui.TextIn(e.Ready ? "READY" : "--", 760, ry, 70, 54, 18, e.Ready ? Palette.Green : Palette.Red);
                if (host && e.Slot != c.LocalSlot && Ui.Button("kick" + e.Slot, 836, ry + 9, 62, 36, "KICK", 15, false, true, Palette.Red)) c.HostKick(e.Slot);
            }

            DrawLobbySettings(c, lobby, host);
            DrawChat(c);

            float y = Ui.Height - 110;
            bool ready = me != null && me.Ready;
            if (Ui.Button("ready", 60, y, 300, 70, ready ? "READY!" : "READY", 30, false, true, ready ? Palette.Green : Palette.Cyan) || _input.PadPressed(GamepadButton.RightFaceDown)) c.SetReady(!ready);
            bool allReady = lobby.Roster.Count >= LobbyRules.MinPlayerLimit && lobby.Roster.All(r => r.Ready);
            if (host)
            {
                if (Ui.Button("start", 380, y, 340, 70, "START GAME", 30, false, allReady, Palette.Magenta) || (allReady && _input.PadPressed(GamepadButton.MiddleRight))) c.HostStart();
                if (Ui.Button("bot", 740, y, 200, 70, "+ BOT", 28, false, lobby.Roster.Count < lobby.Settings.MaxPlayers) || _input.PadPressed(GamepadButton.RightFaceUp)) c.HostAddBot();
                if (lobby.Roster.Count < 2) Ui.Text("Alone? Add bots with + BOT, then READY and START GAME.", 960, y + 22, 20, Palette.Gold);
            }
            else Ui.TextIn(allReady ? "Waiting for the host to start..." : "Waiting for everyone to be READY...", 380, y, 560, 70, 22, new Color(255, 255, 255, 180));

            if (c.LobbyCountdown >= 0f) Ui.TextIn($"MATCH STARTS IN {Math.Ceiling(c.LobbyCountdown)}", 0, Ui.Height / 2 - 60, Ui.Width, 120, 64, Color.White, 1, glow: true);
            if (Ui.Button("lsettings", Ui.Width - 660, y, 280, 70, "SETTINGS", 26)) OpenSettings();
            if (Ui.Button("leave", Ui.Width - 360, y, 300, 70, "LEAVE", 30, false, true, Palette.Red)) LeaveToMenu();
        }

        private void DrawLobbySettings(GameClient c, LobbyView lobby, bool host)
        {
            if (_edit == null || !host || SameSettings(_edit, lobby.Settings) || Raylib.GetTime() - _lastSettingsSend > 1.0) _edit = lobby.Settings.Clone();
            float x = Ui.Width - 900, y = 150, w = 840;
            Ui.Panel(x, y, w, 560, host ? "MATCH SETTINGS" : "MATCH SETTINGS (host only)");
            float ix = x + 24, iw = w - 48, iy = y + 64, h = 54;
            var s = _edit.Clone();
            string[] maps = MapCatalog.All.Select(m => m.Name).ToArray();
            s.MapId = (byte)Ui.Selector("map", ix, iy, iw, h, "Map", maps, s.MapId, host); iy += h + 8;
            s.MaxPlayers = (byte)Math.Round(Ui.Slider("players", ix, iy, iw, h, "Player Limit", s.MaxPlayers, 2, 8, s.MaxPlayers.ToString(), host)); iy += h + 8;
            s.MaxScore = (byte)Math.Round(Ui.Slider("score", ix, iy, iw, h, "Max Score", s.MaxScore, MatchTimings.MinMaxScore, MatchTimings.MaxMaxScore, s.MaxScore.ToString(), host)); iy += h + 8;
            s.Teams = Ui.Selector("mode", ix, iy, iw, h, "Mode", Modes, s.Teams ? 1 : 0, host) == 1; iy += h + 8;
            s.FriendlyFire = Ui.Toggle("ff", ix, iy, iw, h, "Friendly Fire" + (s.Teams ? "" : "  (Teams only)"), s.FriendlyFire, host); iy += h + 8;
            s.IsPublic = Ui.Selector("vis", ix, iy, iw, h, "Lobby Type", Visibility, s.IsPublic ? 0 : 1, host) == 0; iy += h + 8;
            s.Competitive = Ui.Toggle("comp", ix, iy, iw, h, "Competitive (leaving costs rating)", s.Competitive, host);
            if (host && !SameSettings(s, _edit))
            {
                _edit = s;
                c.HostApplySettings(s);
                _lastSettingsSend = Raylib.GetTime();
            }
        }

        private static bool SameSettings(LobbySettings a, LobbySettings b) =>
            a.MapId == b.MapId && a.MaxPlayers == b.MaxPlayers && a.MaxScore == b.MaxScore && a.Teams == b.Teams &&
            a.FriendlyFire == b.FriendlyFire && a.IsPublic == b.IsPublic && a.Competitive == b.Competitive;

        private void DrawChat(GameClient c)
        {
            float x = Ui.Width / 2 - 520, y = 725;
            Ui.Panel(x, y, 1040, 230);
            int lines = 5;
            int start = Math.Max(0, _chat.Count - lines);
            for (int i = start; i < _chat.Count; i++)
            {
                ChatLine l = _chat[i];
                float ly = y + 10 + (i - start) * 30;
                Ui.TextIn(l.IsSystem ? "*" : l.Name + ":", x + 12, ly, 200, 30, 20, l.IsSystem ? Palette.Gold : Palette.Slot(l.Slot), 2);
                Ui.TextIn(l.Text, x + 222, ly, 800, 30, 20, l.IsSystem ? new Color(255, 255, 255, 160) : Color.White);
            }
            _chatInput = Ui.TextField("chat", x + 12, y + 176, 860, 44, _chatInput, ChatFilter.MaxLength, 22);
            bool enter = Ui.Focus == "chat" && Raylib.IsKeyPressed(KeyboardKey.Enter);
            if ((Ui.Button("send", x + 884, y + 176, 144, 44, "SEND", 20) || enter) && _chatInput.Trim().Length > 0)
            {
                c.SendChat(_chatInput);
                _chatInput = "";
            }
        }

        // ───────────────────────────── settings ─────────────────────────────

        private void OpenSettings()
        {
            _settingsOpen = true;
            _rebinding = -1;
        }

        private void DrawSettings()
        {
            Ui.Rect(0, 0, Ui.Width, Ui.Height, new Color(0, 0, 0, 140));
            float px = Ui.Width / 2 - 720, py = 80, pw = 1440, ph = 900;
            Ui.Panel(px, py, pw, ph);
            Ui.Text("SETTINGS", px + 30, py + 16, 44, Palette.Cyan, glow: true);
            for (int i = 0; i < Tabs.Length; i++)
                if (Ui.Button("tab" + i, px + 30 + i * 250, py + 90, 230, 54, Tabs[i], 22, false, true, _tab == i ? Palette.Magenta : (Color?)null)) { _tab = i; _rebinding = -1; }

            float x = px + 30, y = py + 170, w = 900, h = 56;
            switch (_tab)
            {
                case 0:
                    int size = Ui.Selector("res", x, y, w, h, "Resolution (window)", DesktopConfig.WindowSizes.Select(s => $"{s.W} x {s.H}").ToArray(), Config.WindowSize); y += h + 12;
                    int mode = Ui.Selector("disp", x, y, w, h, "Display Mode", DesktopConfig.DisplayModes, Config.DisplayMode); y += h + 12;
                    if (size != Config.WindowSize || mode != Config.DisplayMode) { Config.WindowSize = size; Config.DisplayMode = mode; Program.ApplyDisplay(Config); }
                    Config.FractureQuality = (int)Math.Round(Ui.Slider("frac", x, y, w, h, "Fracture Physics Quality", Config.FractureQuality, 0, 2, Quality[Config.FractureQuality])); y += h;
                    Ui.Text(DesktopConfig.QualityNames[Config.FractureQuality], x + w * 0.44f, y - 6, 18, new Color(255, 255, 255, 150)); y += 36;
                    bool vs = Ui.Toggle("vsync", x, y, w, h, "V-Sync", Config.VSync); y += h + 12;
                    if (vs != Config.VSync) { Config.VSync = vs; Program.ApplyDisplay(Config); }
                    Config.FieldOfView = MathF.Round(Ui.Slider("fov", x, y, w, h, "Field of View", Config.FieldOfView, 70, 110, $"{Config.FieldOfView:0}")); y += h + 12;
                    break;
                case 1:
                    Config.MasterVolume = Ui.Slider("vm", x, y, w, h, "Master Volume", Config.MasterVolume, 0, 1, Pct(Config.MasterVolume)); y += h + 12;
                    Config.SfxVolume = Ui.Slider("vs", x, y, w, h, "SFX Volume (Glass ASMR)", Config.SfxVolume, 0, 1, Pct(Config.SfxVolume)); y += h + 12;
                    Config.MusicVolume = Ui.Slider("vmu", x, y, w, h, "Music Volume", Config.MusicVolume, 0, 1, Pct(Config.MusicVolume)); y += h + 24;
                    Ui.Text("Voice chat is available in the Unity edition of GLASSCORE.", x, y, 20, new Color(255, 255, 255, 150));
                    Config.ApplyAudio();
                    break;
                case 2:
                    DrawControls(x, y);
                    break;
                case 3:
                    Ui.Text("PLAYER NAME", x, y, 20, Palette.Cyan); y += 36;
                    Config.PlayerName = Ui.TextField("sname", x, y, 500, 52, Config.PlayerName, LobbyRules.PlayerNameMaxLength); y += 80;
                    Ui.Text("DEDICATED SERVER HOSTS (comma separated, used by Quick Match and lobby codes)", x, y, 20, Palette.Cyan); y += 36;
                    Config.ServerHosts = Ui.TextField("hosts", x, y, 1100, 52, Config.ServerHosts, 300); y += 80;
                    Ui.Text($"Rating: {Config.Rating}   -   Screenshots (F12): {System.IO.Path.Combine(DesktopConfig.Directory, "Clips")}", x, y, 20, Color.White);
                    break;
            }

            if (Ui.Button("s.apply", px + 30, py + ph - 90, 300, 64, "APPLY", 28)) { Config.Validate(); Config.Save(); Config.ApplyAudio(); SaveProfile(); Notice("Settings saved.", 2); }
            if (Ui.Button("s.back", px + pw - 330, py + ph - 90, 300, 64, "BACK", 28) || (_rebinding < 0 && Ui.Focus == null && _input.MenuBack))
            {
                Config.Validate();
                Config.Save();
                Config.ApplyAudio();
                _settingsOpen = false;
            }
        }

        private void DrawControls(float x, float y)
        {
            if (_rebinding >= 0 && Raylib.GetTime() - _rebindTime > 0.2)
            {
                int code = DesktopInput.PollAnyBinding();
                if (code != 0)
                {
                    if (code == (int)KeyboardKey.Escape && _rebinding != (int)Bind.Pause) _rebinding = -1;
                    else { Config.Bindings[_rebinding] = code; _rebinding = -1; Sfx.Play(Sfx.CrackSharp, 0.5f); }
                }
            }
            Ui.Text("ACTION", x, y, 18, Palette.Cyan);
            Ui.Text("KEYBOARD / MOUSE", x + 460, y, 18, Palette.Cyan);
            Ui.Text("GAMEPAD (fixed)", x + 820, y, 18, Palette.Cyan);
            string[] pad = { "Left Stick", "Left Stick", "Left Stick", "Left Stick", "A / Cross", "RT / R2", "LT / L2", "RB / R1", "LB / L1", "Start" };
            for (int i = 0; i < Config.Bindings.Length; i++)
            {
                float ry = y + 36 + i * 44;
                Ui.TextIn(DesktopConfig.BindName((Bind)i), x, ry, 440, 40, 20, Color.White);
                string label = _rebinding == i ? "press a key..." : DesktopConfig.BindLabel(Config.Bindings[i]);
                if (Ui.Button("bind" + i, x + 460, ry + 2, 320, 38, label, 18, false, true, _rebinding == i ? Palette.Magenta : (Color?)null))
                {
                    _rebinding = i;
                    _rebindTime = Raylib.GetTime();
                }
                Ui.TextIn(pad[i], x + 820, ry, 300, 40, 18, new Color(255, 255, 255, 150));
            }
            float by = y + 36 + Config.Bindings.Length * 44 + 10;
            Config.MouseSensitivity = Ui.Slider("sens", x, by, 700, 44, "Mouse Sensitivity", Config.MouseSensitivity, 0.2f, 4f, $"{Config.MouseSensitivity:0.0}");
            Config.GamepadLookSpeed = Ui.Slider("pads", x, by + 48, 700, 44, "Gamepad Look Speed", Config.GamepadLookSpeed, 60, 400, $"{Config.GamepadLookSpeed:0}");
            Config.InvertY = Ui.Toggle("inv", x + 760, by, 340, 44, "Invert Y", Config.InvertY);
            if (Ui.Button("reset", x + 760, by + 50, 340, 44, "RESET DEFAULTS", 18)) Config.Bindings = DesktopConfig.DefaultBindings();
        }

        private static string Pct(float v) => $"{Math.Round(v * 100)}%";

        // ───────────────────────────── match UI ─────────────────────────────

        private void DrawMatch()
        {
            GameClient c = Client;
            if (c == null || _match == null) return;
            MatchPhase phase = c.Phase;
            bool post = phase == MatchPhase.Highlights || phase == MatchPhase.Results || phase == MatchPhase.ReturnToLobby;
            if (!_paused && !_settingsOpen) DrawHud(c);
            if (post) DrawResults(c);
            if (_paused && !_settingsOpen) DrawPause(c);
        }

        private void DrawHud(GameClient c)
        {
            float W = Ui.Width, H = Ui.Height;
            PlayerState me = c.LocalPlayer;
            MatchPhase phase = c.Phase;
            double now = Raylib.GetTime();

            // Name tags.
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if (i == c.LocalSlot) continue;
                PlayerState o = c.RenderPlayers[i];
                if (!o.Active || !o.Alive) continue;
                Vector3 world = MatchView.V(o.Position) + new Vector3(0, 2.15f, 0);
                Vector3 toTag = world - _match.Camera.Position;
                if (Vector3.Dot(toTag, _match.Camera.Target - _match.Camera.Position) <= 0) continue;
                Vector2 sp = Raylib.GetWorldToScreen(world, _match.Camera);
                float gx = sp.X / Ui.Scale, gy = sp.Y / Ui.Scale;
                Ui.TextIn(NameOf(i), gx - 150, gy - 34, 300, 30, 20, Palette.Slot(i), 1);
                Ui.Bar(gx - 40, gy - 4, 80, 5, o.Health / PlayerRules.MaxHealth, Palette.Slot(i));
            }

            float remaining = c.PhaseTimeRemaining;
            int secs = (int)Math.Ceiling(Math.Max(0, remaining));
            Ui.TextIn($"{secs / 60}:{secs % 60:00}", 0, 18, W, 60, 46, phase == MatchPhase.Cascade || phase == MatchPhase.Overtime ? Palette.Red : Color.White, 1, glow: true);
            Ui.TextIn(PhaseLabel(phase), 0, 74, W, 30, 20, Palette.Cyan, 1);

            if (phase == MatchPhase.Countdown)
            {
                int n = (int)Math.Ceiling(remaining);
                Ui.TextIn(n > 3 ? "GET READY" : n > 0 ? n.ToString() : "SHATTER!", 0, H / 2 - 170, W, 160, n > 3 ? 64 : 140, Palette.Cyan, 1, glow: true);
                if (n != _lastBeep) { _lastBeep = n; if (n >= 1 && n <= 3) Sfx.Play(Sfx.Beep); }
            }
            else if (phase == MatchPhase.Loading) Ui.TextIn("Waiting for all players to load the arena...", 0, H / 2 - 40, W, 80, 32, Color.White, 1);

            if (!string.IsNullOrEmpty(_match.Banner) && now < _match.BannerUntil) Ui.TextIn(_match.Banner, 0, 150, W, 70, 42, _match.BannerColor, 1, glow: true);

            // Scoreboard.
            var order = Enumerable.Range(0, GameWorld.MaxPlayers).Where(i => c.RenderPlayers[i].Active).OrderByDescending(i => c.Scores[i]).ToList();
            float sy = 20;
            int target = c.Match?.MaxScore ?? MatchTimings.DefaultMaxScore;
            if (c.Match != null && c.Match.Teams)
            {
                int t0 = order.Where(i => c.RenderPlayers[i].Team == 0).Sum(i => c.Scores[i]);
                int t1 = order.Where(i => c.RenderPlayers[i].Team != 0).Sum(i => c.Scores[i]);
                Ui.TextIn($"CYAN {t0}  -  MAGENTA {t1}   (to {target})", W - 400, sy, 380, 34, 22, Color.White, 2);
            }
            else Ui.TextIn($"FIRST TO {target}", W - 400, sy, 380, 30, 18, new Color(255, 255, 255, 150), 2);
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                float ry = sy + 36 + k * 34;
                if (i == c.LocalSlot) Ui.Rect(W - 400, ry, 380, 32, Palette.WithAlpha(Palette.Slot(i), 0.15f));
                Ui.TextIn(NameOf(i), W - 390, ry, 300, 32, 20, Palette.Slot(i));
                Ui.TextIn(c.Scores[i].ToString(), W - 80, ry, 50, 32, 22, Color.White, 2);
            }

            // Kill feed.
            for (int i = 0; i < _match.Feed.Count; i++)
            {
                var f = _match.Feed[i];
                double age = now - f.Time;
                if (age > 6) continue;
                Ui.Text(f.Text, 24, 24 + i * 32, 20, Palette.WithAlpha(f.Color, (float)Math.Clamp(6 - age, 0, 1)));
            }

            if (me.Alive)
            {
                float cx = W / 2, cy = H / 2;
                Color ch = new Color(0, 255, 255, 230);
                Ui.Rect(cx - 1.5f, cy - 12, 3, 8, ch); Ui.Rect(cx - 1.5f, cy + 4, 3, 8, ch);
                Ui.Rect(cx - 12, cy - 1.5f, 8, 3, ch); Ui.Rect(cx + 4, cy - 1.5f, 8, 3, ch);
                if (now < _match.HitMarkerUntil) Ui.TextIn("X", cx - 30, cy - 30, 60, 60, 40, Color.White, 1);

                Ui.Text("INTEGRITY", 40, H - 132, 18, new Color(255, 255, 255, 150));
                Ui.Bar(40, H - 100, 360, 22, me.Health / PlayerRules.MaxHealth, me.Health > 35 ? Palette.Cyan : Palette.Red);
                Ui.Text(((int)Math.Ceiling(me.Health)).ToString(), 410, H - 108, 30, Color.White);
                if (me.SpawnProtection > 0f) Ui.Text("SPAWN PROTECTION", 40, H - 170, 20, Palette.Green);

                if (me.Grounded && me.GroundTile >= 0 && c.Tiles != null && c.Match != null)
                {
                    GlassType gt = c.Match.Layout.Types[me.GroundTile];
                    float integ = c.Tiles.GetIntegrity(me.GroundTile), max = GlassCatalog.Get(gt).MaxIntegrity;
                    bool weak = GlassCatalog.IsWeakened(gt, integ);
                    string name = gt == GlassType.Standard ? "FLOAT GLASS" : gt == GlassType.Tempered ? "TEMPERED" : "BULLETPROOF";
                    Color col = weak ? Palette.Red : Palette.GlassEdge(gt);
                    Ui.Text($"FLOOR: {name}  {Math.Ceiling(integ)}/{max:0}" + (weak ? "  - WEAKENED!" : ""), 40, H - 66, 18, col);
                    Ui.Bar(40, H - 36, 360, 8, integ / max, col);
                }

                WeaponSpec w = WeaponCatalog.Get(me.Weapon);
                Ui.TextIn(w.DisplayName, W - 660, H - 140, 620, 40, 26, Color.White, 2);
                Ui.TextIn($"recoil dv {w.RecoilDeltaV:0.0} m/s  -  {(w.Heavy ? "HEAVY" : "light")}", W - 660, H - 104, 620, 30, 18, new Color(255, 255, 255, 150), 2);
                for (int i = 0; i < WeaponCatalog.Count; i++) Ui.Rect(W - 400 + i * 92, H - 84, 84, 6, i == me.Weapon ? Palette.Cyan : new Color(255, 255, 255, 50));
                Ui.Bar(W - 400, H - 66, 360, 10, 1f - me.FireCooldown / Math.Max(0.01f, w.FireInterval), Palette.Cyan);
                string anchor = me.Anchored ? "ANCHORED" : me.AnchorCooldown > 0f ? $"ANCHOR {me.AnchorCooldown:0.0}s" : "ANCHOR READY";
                Ui.TextIn(anchor, W - 660, H - 50, 620, 30, 18, me.Anchored ? Palette.Magenta : me.AnchorCooldown > 0f ? new Color(255, 255, 255, 120) : Palette.Green, 2);
            }
            else if (MatchClock.IsCombatPhase(phase) && me.Active)
            {
                double left = Math.Max(0, _match.LocalRespawnAt - now);
                Ui.TextIn(_match.LastKiller >= 0 ? "Shattered by " + NameOf(_match.LastKiller) : "You broke your own glass house", 0, H / 2 - 90, W, 70, 36, Palette.Red, 1, glow: true);
                Ui.TextIn(left > 0 ? $"Respawning in {left:0.0}" : "Waiting for a safe spawn pad...", 0, H / 2 - 20, W, 50, 26, Color.White, 1);
            }

            if (now < _match.DamageFlashUntil) Ui.Rect(0, 0, W, H, new Color(255, 25, 38, (int)(46 * (_match.DamageFlashUntil - now) / 0.25)));
            Ui.TextIn($"{c.PingMs} ms   -   F12 screenshot   -   ESC pause", 0, H - 40, W, 30, 16, new Color(255, 255, 255, 120), 1);
        }

        private void DrawPause(GameClient c)
        {
            float W = Ui.Width;
            Ui.TextIn("MATCH IN PROGRESS", 0, 200, W, 80, 56, Palette.WithAlpha(Color.White, Ui.Pulse(3f, 0.45f)), 1, glow: true);
            int secs = (int)Math.Ceiling(Math.Max(0, c.PhaseTimeRemaining));
            Ui.TextIn($"Latency {c.PingMs} ms  -  {PhaseLabel(c.Phase)}  -  {secs / 60}:{secs % 60:00}", 0, 280, W, 40, 24, new Color(255, 255, 255, 190), 1);
            if (_confirmLeave)
            {
                float bx = W / 2 - 420;
                Ui.Panel(bx, 400, 840, 260, "LEAVE MATCH?");
                bool comp = c.Lobby.Settings.Competitive;
                Ui.Text(comp ? "Competitive match: leaving counts as a loss and costs extra rating." : "You will return to the main menu.", bx + 30, 480, 22, comp ? Palette.Red : Color.White);
                if (Ui.Button("lv.yes", bx + 30, 570, 360, 64, "LEAVE", 28, false, true, Palette.Red)) { _confirmLeave = false; LeaveToMenu(); }
                if (Ui.Button("lv.no", bx + 450, 570, 360, 64, "STAY", 28) || _input.MenuBack) _confirmLeave = false;
                return;
            }
            string[] items = { "RESUME", "OPTIONS", "LEAVE MATCH" };
            _pauseFocus = (_pauseFocus + _input.MenuNavigate() + items.Length) % items.Length;
            bool confirm = _input.MenuConfirm;
            for (int i = 0; i < items.Length; i++)
            {
                bool clicked = Ui.Button("pause" + i, W / 2 - 220, 400 + i * 90, 440, 70, items[i], 30, _input.UsingGamepad && _pauseFocus == i, true, i == 2 ? Palette.Red : (Color?)null);
                if (clicked || (confirm && _pauseFocus == i))
                {
                    if (confirm) Ui.ClickFeedback();
                    if (i == 0) _paused = false;
                    else if (i == 1) OpenSettings();
                    else _confirmLeave = true;
                }
            }
        }

        private void DrawResults(GameClient c)
        {
            float W = Ui.Width, px = W / 2 - 820, py = 110;
            Ui.Panel(px, py, 1640, 860);
            Ui.Text(c.EndReason == MatchEndReason.OvertimeExpired ? "DRAW" : "MATCH RESULTS", px + 30, py + 14, 50, Palette.Cyan, glow: true);
            float left = c.Phase == MatchPhase.Highlights ? c.PhaseTimeRemaining + MatchTimings.Results : c.PhaseTimeRemaining;
            Ui.TextIn($"Back to lobby in {Math.Ceiling(left)}s", px + 1000, py + 24, 610, 50, 22, new Color(255, 255, 255, 150), 2);
            string[] headers = { "#", "PLAYER", "SCORE", "KO", "FALLS", "TILES", "RATING" };
            float[] cols = { 0, 60, 460, 580, 680, 800, 920 };
            float x = px + 30, y = py + 110;
            for (int i = 0; i < headers.Length; i++) Ui.Text(headers[i], x + cols[i], y, 18, Palette.Cyan);
            y += 40;
            if (c.Results.Count == 0) Ui.Text("Tallying results...", x, y, 24, new Color(255, 255, 255, 180));
            foreach (ResultRow r in c.Results)
            {
                if (r.Slot == c.LocalSlot) Ui.Rect(x - 10, y, 1080, 40, Palette.WithAlpha(Palette.Slot(r.Slot), 0.14f));
                Ui.TextIn(r.Placement.ToString(), x + cols[0], y, 60, 40, 26, r.Placement == 1 ? Palette.Gold : Color.White);
                Ui.TextIn(r.Name, x + cols[1], y, 390, 40, 24, Palette.Slot(r.Slot));
                Ui.TextIn(r.Score.ToString(), x + cols[2], y, 100, 40, 24, Color.White);
                Ui.TextIn(r.Knockouts.ToString(), x + cols[3], y, 100, 40, 22, Color.White);
                Ui.TextIn(r.Deaths.ToString(), x + cols[4], y, 100, 40, 22, Color.White);
                Ui.TextIn(r.TilesShattered.ToString(), x + cols[5], y, 100, 40, 22, Color.White);
                Ui.TextIn($"{r.NewRating} ({(r.RatingDelta >= 0 ? "+" : "")}{r.RatingDelta})", x + cols[6], y, 200, 40, 22, r.RatingDelta >= 0 ? Palette.Green : Palette.Red);
                y += 44;
            }
            float hx = px + 1120, hy = py + 110;
            Ui.Text("HIGHLIGHT REEL", hx, hy, 20, Palette.Cyan);
            if (c.Highlights.Count == 0) Ui.Text("No highlights this round.", hx, hy + 44, 20, new Color(255, 255, 255, 150));
            for (int i = 0; i < c.Highlights.Count && i < 6; i++)
            {
                Highlight h = c.Highlights[i];
                Ui.Rect(hx, hy + 40 + i * 70, 490, 62, new Color(0, 255, 255, 16));
                Ui.TextIn($"{NameOf(h.Slot)} - {h.Label}", hx + 12, hy + 40 + i * 70, 470, 62, 20, Color.White);
            }
            if (Ui.Button("shot", hx, py + 760, 490, 50, "SAVE SCREENSHOT (F12)", 18)) TakeScreenshot();
        }

        private static string PhaseLabel(MatchPhase p) => p switch
        {
            MatchPhase.Loading => "LOADING ARENA",
            MatchPhase.Countdown => "COUNTDOWN",
            MatchPhase.Live => "LIVE",
            MatchPhase.Cascade => "FRACTURE CASCADE",
            MatchPhase.Overtime => "OVERTIME",
            MatchPhase.MatchEnd => "MATCH OVER",
            MatchPhase.Highlights => "HIGHLIGHTS",
            MatchPhase.Results => "RESULTS",
            _ => "RETURNING TO LOBBY",
        };

        private static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;

        private const string BlurShader = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
uniform sampler2D texture0;
uniform vec2 texel;
uniform float amount;
out vec4 finalColor;
void main()
{
    vec3 sum = vec3(0.0);
    float wsum = 0.0;
    for (int x = -4; x <= 4; x++)
    for (int y = -4; y <= 4; y++)
    {
        float w = exp(-float(x * x + y * y) / 8.0);
        sum += texture(texture0, fragTexCoord + vec2(x, y) * texel * amount).rgb * w;
        wsum += w;
    }
    finalColor = vec4(sum / wsum * (1.0 - 0.1 * amount), 1.0);
}";
    }

    /// <summary>Main-menu background: a neon-framed pane shattering in slow motion and rewinding, looped.</summary>
    public sealed class MenuPane
    {
        private const float Cycle = 8f, ShatterAt = 1.6f, RewindAt = 5.6f;
        private readonly List<(Vector3[] tris, Vector3 rest, Vector3 vel, Vector3 spin)> _shards = new List<(Vector3[], Vector3, Vector3, Vector3)>();
        private bool _played;
        // raylib is right-handed: negative X is on the right of the screen when looking down +Z.
        private Camera3D _camera = new Camera3D { Position = new Vector3(0, 0.2f, -5.2f), Target = new Vector3(-0.9f, 0, 0), Up = Vector3.UnitY, FovY = 50f, Projection = CameraProjection.Perspective };

        public void Init()
        {
            var cells = VoronoiFracture2D.ComputeCells(VoronoiFracture2D.GenerateSites(20240926UL, new Vec2(0.25f, 0.1f), 1f, 1f, 48, 0.8f), 1f, 1f);
            var rng = new DeterministicRandom(77);
            foreach (var cell in cells)
            {
                Vector3[] tris = ShardSystem.Triangles(cell);
                for (int i = 0; i < tris.Length; i++) tris[i] = new Vector3(tris[i].X, tris[i].Z, tris[i].Y); // pane faces the camera
                var rest = new Vector3(cell.Centroid.X, cell.Centroid.Y, 0f);
                Vector3 outward = Vector3.Normalize(rest - new Vector3(0.25f, 0.1f, 0f) + new Vector3(1e-3f, 0, 0));
                _shards.Add((tris, rest, outward * rng.Range(0.3f, 1.1f) + new Vector3(0, 0, -rng.Range(0.2f, 1.2f)),
                    new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f))));
            }
        }

        public void Draw()
        {
            float t = (float)(Raylib.GetTime() % Cycle);
            var center = new Vector3(-1.9f, 0f, 0f);
            _camera.Position = new Vector3(MathF.Sin(t / Cycle * MathF.PI * 2f) * 0.25f, 0.2f, -5.2f);
            Raylib.BeginMode3D(_camera);
            Rlgl.DisableBackfaceCulling();
            Rlgl.DisableDepthMask();
            Raylib.DrawCircle3D(center, 1.75f, Vector3.UnitY, 0f, new Color(0, 255, 255, 200));
            Raylib.DrawCircle3D(center, 1.78f, Vector3.UnitY, 0f, new Color(0, 255, 255, 90));
            if (t < ShatterAt)
            {
                _played = false;
                Raylib.DrawCube(center, 2f, 2f, 0.1f, new Color(140, 217, 255, 60));
                Raylib.DrawCubeWires(center, 2f, 2f, 0.1f, new Color(0, 255, 255, 220));
            }
            else
            {
                if (!_played) { _played = true; Sfx.Play(Sfx.ShatterSmall, 0.35f, 0.8f); }
                float flight;
                if (t < RewindAt) flight = (t - ShatterAt) * 0.35f;
                else
                {
                    float k = Math.Clamp((t - RewindAt) / (Cycle - RewindAt), 0f, 1f);
                    flight = (RewindAt - ShatterAt) * 0.35f * (1f - k * k * (3f - 2f * k));
                }
                foreach (var s in _shards)
                {
                    Vector3 pos = center + s.rest + s.vel * flight;
                    Matrix4x4 m = s.spin.LengthSquared() > 0 ? Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(s.spin), flight * 1.2f) : Matrix4x4.Identity;
                    for (int i = 0; i + 2 < s.tris.Length; i += 3)
                        Raylib.DrawTriangle3D(Vector3.Transform(s.tris[i], m) + pos, Vector3.Transform(s.tris[i + 1], m) + pos, Vector3.Transform(s.tris[i + 2], m) + pos, new Color(160, 230, 255, 90));
                    Raylib.DrawLine3D(Vector3.Transform(s.tris[0], m) + pos, Vector3.Transform(s.tris[1], m) + pos, new Color(200, 255, 255, 200));
                }
            }
            Rlgl.EnableDepthMask();
            Rlgl.EnableBackfaceCulling();
            Raylib.EndMode3D();
        }
    }
}
