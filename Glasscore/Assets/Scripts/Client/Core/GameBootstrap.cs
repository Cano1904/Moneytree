using System;
using System.IO;
using System.Threading;
using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Entry point. The build contains a single empty scene; this creates the whole game at startup.
    ///   GLASSCORE.exe                 → the game client (main menu)
    ///   GLASSCORE.exe -batchmode -nographics -server [-port 27015] [-code AB3K9Z] [-map 1] [-private]
    ///                                 → headless dedicated server (same as server/GlasscoreServer)
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-server") >= 0)
            {
                RunDedicatedServer(args);
                return;
            }
            if (GameApp.Instance == null) new GameObject("GLASSCORE").AddComponent<GameApp>();
        }

        private static void RunDedicatedServer(string[] args)
        {
            Application.targetFrameRate = 120;
            var config = new ServerConfig
            {
                Dedicated = true,
                ServerName = "GLASSCORE Dedicated",
                DataDirectory = Path.Combine(Application.persistentDataPath, "server"),
                Log = line => Debug.Log(line),
            };
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-port": if (int.TryParse(next, out int port)) config.Port = port; break;
                    case "-code": config.LobbyCode = next; break;
                    case "-name": if (next != null) config.ServerName = next; break;
                    case "-map": if (int.TryParse(next, out int map)) config.Settings.MapId = (byte)Mathf.Clamp(map, 0, MapCatalog.All.Length - 1); break;
                    case "-private": config.Settings.IsPublic = false; break;
                    case "-teams": config.Settings.Teams = true; break;
                    case "-competitive": config.Settings.Competitive = true; break;
                }
            }
            var host = new GameObject("DedicatedServer").AddComponent<DedicatedServerHost>();
            host.Run(config);
        }
    }

    /// <summary>Drives a <see cref="GameServer"/> from Unity's loop when the player build runs with -server.</summary>
    public sealed class DedicatedServerHost : MonoBehaviour
    {
        private GameServer _server;

        public void Run(ServerConfig config)
        {
            DontDestroyOnLoad(gameObject);
            _server = new GameServer(config);
            _server.Start();
            Debug.Log($"[GLASSCORE] Dedicated server running. Lobby code {_server.LobbyCode}, port {_server.Port}.");
        }

        private void Update() => _server?.Update(Time.unscaledTimeAsDouble);

        private void OnApplicationQuit()
        {
            _server?.Dispose();
            _server = null;
        }
    }
}
