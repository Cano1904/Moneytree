using System;
using System.IO;
using System.Threading;
using Glasscore.Net;
using Raylib_cs;

namespace Glasscore.Desktop
{
    /// <summary>
    /// GLASSCORE desktop client. Double-click GLASSCORE.exe to play.
    ///   GLASSCORE.exe --server [--port 27015] [--code AB3K9Z]   → headless dedicated server
    ///   GLASSCORE.exe --screenshot out.png [--demo]             → render one frame (used for automated checks)
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--server") >= 0) return RunServer(args);

            var config = DesktopConfig.Load();
            string screenshot = ArgValue(args, "--screenshot");
            int demoFrames = int.TryParse(ArgValue(args, "--frames"), out int f) ? f : 90;

            var flags = ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint;
            if (config.VSync) flags |= ConfigFlags.VSyncHint;
            Raylib.SetConfigFlags(flags);
            Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
            var size = DesktopConfig.WindowSizes[config.WindowSize];
            Raylib.InitWindow(size.W, size.H, "GLASSCORE");
            Raylib.SetExitKey(KeyboardKey.Null); // ESC opens the pause menu instead of quitting
            Raylib.InitAudioDevice();
            Raylib.SetWindowMinSize(960, 540);
            ApplyDisplay(config);

            var context = new MainThreadContext();
            SynchronizationContext.SetSynchronizationContext(context);

            Ui.LoadFont();
            Sfx.Init();
            config.ApplyAudio();
            var app = new App(config);
            if (Array.IndexOf(args, "--demo") >= 0) Demo.Start(app);
            if (int.TryParse(ArgValue(args, "--pause-at"), out int pauseAt)) Demo.PauseAtFrame = pauseAt;

            int frame = 0;
            while (!Raylib.WindowShouldClose() && !app.QuitRequested)
            {
                context.Pump();
                Sfx.Update();
                Demo.Tick(app, frame);
                app.Frame();
                frame++;
                if (screenshot != null && frame >= demoFrames)
                {
                    Image img = Raylib.LoadImageFromScreen();
                    Raylib.ExportImage(img, screenshot);
                    Raylib.UnloadImage(img);
                    break;
                }
            }

            app.Shutdown();
            Raylib.CloseAudioDevice();
            Raylib.CloseWindow();
            return 0;
        }

        public static void ApplyDisplay(DesktopConfig config)
        {
            var size = DesktopConfig.WindowSizes[config.WindowSize];
            if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
            if (Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode)) Raylib.ToggleBorderlessWindowed();
            int monitor = Raylib.GetCurrentMonitor();
            switch (config.DisplayMode)
            {
                case 1:
                    Raylib.ToggleBorderlessWindowed();
                    break;
                case 2:
                    Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
                    Raylib.ToggleFullscreen();
                    break;
                default:
                    int w = Math.Min(size.W, Math.Max(960, Raylib.GetMonitorWidth(monitor)));
                    int h = Math.Min(size.H, Math.Max(540, Raylib.GetMonitorHeight(monitor)));
                    Raylib.SetWindowSize(w, h);
                    break;
            }
            Raylib.SetTargetFPS(config.VSync ? Math.Max(60, Raylib.GetMonitorRefreshRate(monitor)) : 240);
        }

        private static string ArgValue(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static int RunServer(string[] args)
        {
            var config = new ServerConfig
            {
                Dedicated = true,
                ServerName = "GLASSCORE Dedicated",
                DataDirectory = Path.Combine(DesktopConfig.Directory, "server"),
                Log = Console.WriteLine,
            };
            if (int.TryParse(ArgValue(args, "--port"), out int port)) config.Port = port;
            config.LobbyCode = ArgValue(args, "--code");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            using var server = new GameServer(config);
            server.Start();
            Console.WriteLine($"GLASSCORE dedicated server - lobby code {server.LobbyCode}, port {server.Port}. Ctrl+C to stop.");
            server.RunLoop(cts.Token);
            return 0;
        }
    }

    /// <summary>
    /// Scripted smoke test (--demo): hosts a lobby, adds three bots, readies up and starts a match,
    /// so an automated run can render and screenshot real gameplay.
    /// </summary>
    public static class Demo
    {
        private static bool _on;
        public static int PauseAtFrame;
        private static int _step;

        public static void Start(App app)
        {
            _on = true;
            app.GetType().GetMethod("CreateLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(app, null);
        }

        public static void Tick(App app, int frame)
        {
            if (!_on || app.Client == null) return;
            var c = app.Client;
            if (PauseAtFrame > 0 && frame == PauseAtFrame) app.GetType().GetField("_paused", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(app, true);
            if (_step == 0 && c.Status == ClientStatus.Lobby) { c.HostAddBot(); c.HostAddBot(); c.HostAddBot(); _step = 1; }
            else if (_step == 1 && c.Lobby.Roster.Count == 4) { c.SetReady(true); _step = 2; }
            else if (_step == 2 && c.Lobby.Roster.TrueForAll(r => r.Ready)) { c.HostStart(); _step = 3; }
        }
    }
}
