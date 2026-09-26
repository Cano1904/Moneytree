using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    public enum AppScreen
    {
        MainMenu,
        Customization,
        JoinLobby,
        Searching,
        Connecting,
        Lobby,
        Match,
    }

    /// <summary>
    /// Root of the GLASSCORE client. Core loop:
    /// Main Menu → Online Lobby / Matchmaking → server spawn → arena combat → fracture cascade →
    /// clip export → post-game lobby. Everything (camera, lights, UI, audio) is created in code, so the
    /// build needs just one empty scene.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public GameSettings Settings { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public CloudSave Cloud { get; private set; }
        public InputService Input { get; private set; }
        public Camera MainCamera { get; private set; }
        public CameraPostFx PostFx { get; private set; }
        public Material SkyMaterial { get; private set; }
        public FractureVisualizer Fracture { get; private set; }
        public ClipRecorder Clips { get; private set; }
        public VoiceChat Voice { get; private set; }
        public GameClient Client { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public MenuBackdrop Backdrop { get; private set; }

        public AppScreen Screen { get; set; } = AppScreen.MainMenu;
        public bool SettingsOpen { get; private set; }
        public bool Paused { get; private set; }
        public bool IsHostingServer => _hostedServer != null;
        public string HostedLobbyCode => _hostedServer?.LobbyCode;
        public readonly List<ChatLine> Chat = new List<ChatLine>();
        public string Notice { get; private set; }
        public float NoticeUntil { get; private set; }
        public string SearchStatus { get; private set; } = string.Empty;
        public float SearchStartedAt { get; private set; }
        public int SearchWindow { get; private set; }

        private GameServer _hostedServer;
        private bool _searching;
        private Light _sun;
        private MainMenuScreens _menus;
        private LobbyScreen _lobbyScreen;
        private SettingsScreen _settingsScreen;
        private MatchScreens _matchScreens;

        // ───────────────────────────── lifecycle ─────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true; // matches keep simulating when alt-tabbed

            Settings = GameSettings.Load();
            Settings.ApplyDisplay();
            Cloud = new CloudSave();
            Profile = Cloud.LoadLocal();
            Input = new InputService(Settings);

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            audioGo.AddComponent<AudioService>().Init(Settings);

            BuildCameraAndLights();

            Fracture = new GameObject("Fracture").AddComponent<FractureVisualizer>();
            Fracture.transform.SetParent(transform, false);
            Fracture.Init(Settings);

            Clips = new ClipRecorder();
            PostFx.Recorder = Clips;
            Voice = new VoiceChat(transform);

            Backdrop = new GameObject("MenuBackdrop").AddComponent<MenuBackdrop>();
            Backdrop.transform.SetParent(transform, false);
            Backdrop.Init(MainCamera);

            _menus = new MainMenuScreens();
            _lobbyScreen = new LobbyScreen();
            _settingsScreen = new SettingsScreen();
            _matchScreens = new MatchScreens();

            StartCoroutine(Cloud.Pull(Profile, remote => Profile = remote));
        }

        private void BuildCameraAndLights()
        {
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(transform, false);
            camGo.tag = "MainCamera";
            MainCamera = camGo.AddComponent<Camera>();
            MainCamera.nearClipPlane = 0.05f;
            MainCamera.farClipPlane = 600f;
            MainCamera.clearFlags = CameraClearFlags.Skybox;
            MainCamera.allowHDR = false;
            camGo.AddComponent<AudioListener>();
            PostFx = camGo.AddComponent<CameraPostFx>();

            SkyMaterial = Mats.Sky();
            RenderSettings.skybox = SkyMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.32f, 0.4f);

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(0.75f, 0.9f, 1f);
            _sun.intensity = 1.1f;
            _sun.shadows = LightShadows.None;
        }

        private void OnApplicationQuit() => Shutdown();

        private void OnDestroy()
        {
            if (Instance == this) Shutdown();
        }

        private void Shutdown()
        {
            Settings?.Save();
            Voice?.Dispose();
            Client?.Dispose();
            Client = null;
            _hostedServer?.Dispose();
            _hostedServer = null;
            Clips?.Dispose();
        }

        // ───────────────────────────── per frame ─────────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool inMatch = Screen == AppScreen.Match && Client != null && Client.Status == ClientStatus.Match;
            bool combatInput = inMatch && !Paused && !SettingsOpen;
            Input.GameplayEnabled = combatInput;
            Input.SetCursorForGameplay(combatInput && Application.isFocused);
            Input.Update(dt);

            if (inMatch && !SettingsOpen && Input.PausePressed) SetPaused(!Paused);
            PostFx.TargetBlur = inMatch && (Paused || SettingsOpen) ? 1f : 0f;

            Client?.Update(dt, Time.unscaledTimeAsDouble);
            if (Client != null)
            {
                Voice.Update(Client, Settings, Input, slot =>
                {
                    if (Screen != AppScreen.Match || Client.Status != ClientStatus.Match) return null;
                    PlayerState p = Client.RenderPlayers[slot];
                    return p.Alive ? p.Position.ToUnity() : (Vector3?)null;
                });
            }

            Backdrop.gameObject.SetActive(Screen != AppScreen.Match);
            Clips.Enabled = inMatch; // the rolling clip buffer only records gameplay
        }

        private void OnGUI()
        {
            Ui.Begin();
            switch (Screen)
            {
                case AppScreen.MainMenu:
                case AppScreen.Customization:
                case AppScreen.JoinLobby:
                case AppScreen.Searching:
                case AppScreen.Connecting:
                    _menus.Draw(this);
                    break;
                case AppScreen.Lobby:
                    _lobbyScreen.Draw(this);
                    break;
                case AppScreen.Match:
                    _matchScreens.Draw(this);
                    break;
            }
            if (SettingsOpen) _settingsScreen.Draw(this);
            DrawNotice();
            Ui.End();
        }

        private void DrawNotice()
        {
            if (string.IsNullOrEmpty(Notice) || Time.unscaledTime > NoticeUntil) return;
            var r = new Rect(Ui.Width / 2 - 520, Ui.Height - 150, 1040, 56);
            Ui.Rect(r, new Color(0.05f, 0.02f, 0.03f, 0.9f));
            Ui.Frame(r, Ui.Magenta, 2f);
            Ui.Label(r, Notice, 24, Color.white, TextAnchor.MiddleCenter);
        }

        public void ShowNotice(string text, float seconds = 5f)
        {
            Notice = text;
            NoticeUntil = Time.unscaledTime + seconds;
        }

        // ───────────────────────────── menu actions ─────────────────────────────

        public void OpenSettings()
        {
            _settingsScreen.Open(this);
            SettingsOpen = true;
        }

        public void CloseSettings()
        {
            SettingsOpen = false;
            Ui.CloseDropdowns();
        }

        public void SaveProfile()
        {
            Cloud.SaveLocal(Profile);
            StartCoroutine(Cloud.Push(Profile));
            if (Client != null && Client.Status != ClientStatus.Disconnected)
                Client.SetProfile(Profile.PlayerName, Profile.PrimarySkin, (byte)Profile.Trail);
        }

        public void Quit()
        {
            Shutdown();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>[QUICK MATCH]: skill-based search over LAN + configured servers; hosts a public lobby if none fits.</summary>
        public async void QuickMatch()
        {
            if (_searching) return;
            _searching = true;
            Screen = AppScreen.Searching;
            SearchStartedAt = Time.unscaledTime;
            int mmr = Profile.Rating;
            try
            {
                while (_searching)
                {
                    float elapsed = Time.unscaledTime - SearchStartedAt;
                    SearchWindow = MatchmakingPolicy.WindowAt(elapsed);
                    SearchStatus = SearchWindow == int.MaxValue ? "Searching all skill levels…" : $"Searching rating {mmr} ± {SearchWindow}…";

                    List<DiscoveryInfo> found = await DiscoveryClient.SearchAsync(string.Empty, Settings.ServerHostList, 800);
                    if (!_searching) return;

                    DiscoveryInfo best = found
                        .Where(f => f.IsPublic && f.Joinable && (SearchWindow == int.MaxValue || Math.Abs(f.AverageMmr - mmr) <= SearchWindow))
                        .OrderByDescending(f => f.Dedicated)
                        .ThenBy(f => Math.Abs(f.AverageMmr - mmr))
                        .ThenBy(f => f.RoundTripMs)
                        .FirstOrDefault();

                    if (best != null)
                    {
                        _searching = false;
                        await ConnectTo(best.Address.ToString(), best.GamePort);
                        return;
                    }

                    if (elapsed >= MatchmakingPolicy.CreateSessionAfterSeconds)
                    {
                        // Nobody to join yet: open a public lobby that other searchers will find.
                        _searching = false;
                        await HostLobby(isPublic: true);
                        ShowNotice("No open match found — hosting a public lobby. Others searching will join you.", 6f);
                        return;
                    }
                    await Task.Delay(1200);
                }
            }
            catch (Exception ex)
            {
                _searching = false;
                FailToMenu("Matchmaking failed: " + ex.Message);
            }
        }

        public void CancelSearch()
        {
            _searching = false;
            Screen = AppScreen.MainMenu;
        }

        /// <summary>[CREATE LOBBY]: private lobby with a 6-character friend code (listen server).</summary>
        public async void CreateLobby()
        {
            try { await HostLobby(isPublic: false); }
            catch (Exception ex) { FailToMenu("Could not create lobby: " + ex.Message); }
        }

        private async Task HostLobby(bool isPublic)
        {
            StopHostedServer();
            Screen = AppScreen.Connecting;
            SearchStatus = "Creating lobby…";
            var settings = new LobbySettings { IsPublic = isPublic, MaxPlayers = 8, MaxScore = MatchTimings.DefaultMaxScore };
            Exception last = null;
            for (int port = Protocol.DefaultPort; port < Protocol.DefaultPort + 10; port++)
            {
                var server = new GameServer(new ServerConfig
                {
                    Port = port,
                    ServerName = Profile.PlayerName + "'s Lobby",
                    Settings = settings,
                    Log = line => Debug.Log(line),
                });
                try
                {
                    server.StartThread();
                    _hostedServer = server;
                    break;
                }
                catch (SocketException ex)
                {
                    server.Dispose(); // port taken (another lobby on this PC): try the next one
                    last = ex;
                }
            }
            if (_hostedServer == null) throw last ?? new Exception("No free port.");
            await ConnectTo("127.0.0.1", _hostedServer.Port);
        }

        /// <summary>[JOIN LOBBY]: 6-char friend code via LAN/server discovery, or a direct "host[:port]".</summary>
        public async void JoinLobby(string input)
        {
            string raw = (input ?? string.Empty).Trim();
            try
            {
                if (raw.Contains(".") || raw.Contains(":") || raw.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    string host = raw;
                    int port = Protocol.DefaultPort;
                    int colon = raw.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(raw.Substring(colon + 1), out int p)) { host = raw.Substring(0, colon); port = p; }
                    await ConnectTo(host, port);
                    return;
                }

                string code = LobbyCode.Normalize(raw);
                if (!LobbyCode.IsValid(code))
                {
                    ShowNotice("Lobby codes have 6 characters (letters A–Z without I/O, digits 2–9).");
                    return;
                }
                Screen = AppScreen.Connecting;
                SearchStatus = $"Looking for lobby {code}…";
                List<DiscoveryInfo> found = await DiscoveryClient.SearchAsync(code, Settings.ServerHostList, 1200);
                DiscoveryInfo hit = found.FirstOrDefault(f => f.Code == code);
                if (hit == null)
                {
                    FailToMenu($"Lobby {code} not found on your network or configured servers.");
                    return;
                }
                if (hit.InMatch) { FailToMenu($"Lobby {code} is in a match — try again when it returns to the lobby."); return; }
                await ConnectTo(hit.Address.ToString(), hit.GamePort);
            }
            catch (Exception ex)
            {
                FailToMenu("Join failed: " + ex.Message);
            }
        }

        private async Task ConnectTo(string host, int port)
        {
            Screen = AppScreen.Connecting;
            SearchStatus = $"Connecting to {host}:{port}…";
            DisposeClient();
            Chat.Clear();
            var client = new GameClient { InputProvider = Input.Sample };
            client.OnConnected += () => { Screen = AppScreen.Lobby; };
            client.OnDisconnected += HandleDisconnected;
            client.OnChat += line =>
            {
                Chat.Add(line);
                if (Chat.Count > 60) Chat.RemoveAt(0);
            };
            client.OnMatchStart += HandleMatchStart;
            client.OnReturnToLobby += HandleReturnToLobby;
            client.OnVoice += (slot, seq, data) => Voice.Receive(slot, seq, data);
            client.OnHighlights += HandleHighlights;
            client.OnResults += HandleResults;
            Client = client;
            try
            {
                await client.ConnectAsync(host, port, Profile.PlayerName, Profile.PlayerKey, Profile.Rating, Profile.PrimarySkin, (byte)Profile.Trail);
            }
            catch (Exception ex)
            {
                FailToMenu($"Could not connect to {host}:{port} — {ex.Message}");
            }
        }

        private void HandleDisconnected(string reason)
        {
            bool wasInGame = Screen == AppScreen.Lobby || Screen == AppScreen.Match || Screen == AppScreen.Connecting;
            TearDownMatch();
            StopHostedServer();
            SetPaused(false);
            Screen = AppScreen.MainMenu;
            if (wasInGame && !string.IsNullOrEmpty(reason)) ShowNotice(reason, 6f);
        }

        private void FailToMenu(string message)
        {
            DisposeClient();
            StopHostedServer();
            Screen = AppScreen.MainMenu;
            ShowNotice(message, 7f);
        }

        /// <summary>[LEAVE] / [LEAVE MATCH]: graceful disconnect back to the main menu.</summary>
        public void LeaveToMenu()
        {
            if (Client != null)
            {
                if (Client.Status == ClientStatus.Match) Client.LeaveMatch();
                else Client.Disconnect("Left the lobby.");
            }
            TearDownMatch();
            DisposeClient();
            StopHostedServer();
            SetPaused(false);
            Screen = AppScreen.MainMenu;
        }

        private void DisposeClient()
        {
            if (Client == null) return;
            Client.OnDisconnected -= HandleDisconnected;
            Client.Dispose();
            Client = null;
        }

        private void StopHostedServer()
        {
            _hostedServer?.Dispose();
            _hostedServer = null;
        }

        // ───────────────────────────── match lifecycle ─────────────────────────────

        private void HandleMatchStart(MatchInfo match)
        {
            TearDownMatch();
            Clips.ClearMarkers();
            Presenter = new GameObject("Match").AddComponent<MatchPresenter>();
            Presenter.Init(this, Client, match);
            Screen = AppScreen.Match;
            Input.Yaw = 0f;
            Input.Pitch = 0f;
            SetPaused(false);
            Client.SendSceneLoaded(); // arena built: tell the server we are ready for the countdown
        }

        private void HandleHighlights()
        {
            // Map server ticks to local time for the clip buffer.
            double now = Time.unscaledTimeAsDouble;
            foreach (var h in Client.Highlights)
            {
                double local = now - (Client.ServerTickEstimate - h.Tick) / Client.TickRate;
                string who = Presenter != null ? Presenter.NameOf(h.Slot) : "Player";
                Clips.Mark(local, $"{who} — {h.Label}");
            }
        }

        private void HandleResults()
        {
            foreach (var row in Client.Results)
            {
                if (row.Slot != Client.LocalSlot) continue;
                Profile.Rating = row.NewRating;
                SaveProfile();
            }
        }

        private void HandleReturnToLobby()
        {
            TearDownMatch();
            SetPaused(false);
            Screen = AppScreen.Lobby;
        }

        private void TearDownMatch()
        {
            if (Presenter != null) Destroy(Presenter.gameObject);
            Presenter = null;
            RenderSettings.skybox = SkyMaterial;
        }

        public void SetPaused(bool paused)
        {
            // Multiplayer pause: Time.timeScale stays 1. Inputs are cut, HUD hidden, the view blurred.
            Paused = paused;
            if (!paused) Ui.CloseDropdowns();
        }
    }
}
