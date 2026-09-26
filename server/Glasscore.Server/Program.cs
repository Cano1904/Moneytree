using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Glasscore.Net;
using Glasscore.Simulation;

namespace Glasscore.Server
{
    /// <summary>
    /// Headless GLASSCORE dedicated server.
    ///
    ///   GlasscoreServer [--port 27015] [--name "My Server"] [--code AB3K9Z] [--map 0..2]
    ///                   [--max-players 2..8] [--max-score 5..25] [--private] [--teams]
    ///                   [--friendly-fire] [--competitive] [--data ./data]
    ///
    /// Public dedicated servers auto-start as soon as every connected player is READY.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var config = new ServerConfig
            {
                Dedicated = true,
                ServerName = "GLASSCORE Dedicated",
                DataDirectory = Path.Combine(AppContext.BaseDirectory, "data"),
                Log = line => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {line}"),
            };

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {a}");
                    int NextInt() => int.Parse(Next(), CultureInfo.InvariantCulture);
                    switch (a)
                    {
                        case "--port": config.Port = NextInt(); break;
                        case "--name": config.ServerName = Next(); break;
                        case "--code": config.LobbyCode = Next(); break;
                        case "--map": config.Settings.MapId = (byte)GcMath.Clamp(NextInt(), 0, MapCatalog.All.Length - 1); break;
                        case "--max-players": config.Settings.MaxPlayers = (byte)LobbyRules.ClampPlayerLimit(NextInt(), 0); break;
                        case "--max-score": config.Settings.MaxScore = (byte)LobbyRules.ClampMaxScore(NextInt()); break;
                        case "--private": config.Settings.IsPublic = false; break;
                        case "--teams": config.Settings.Teams = true; break;
                        case "--friendly-fire": config.Settings.FriendlyFire = true; break;
                        case "--competitive": config.Settings.Competitive = true; break;
                        case "--data": config.DataDirectory = Next(); break;
                        case "--no-discovery": config.EnableDiscovery = false; break;
                        case "-h":
                        case "--help":
                            PrintHelp();
                            return 0;
                        default:
                            throw new ArgumentException($"Unknown option {a}");
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                Console.Error.WriteLine(ex.Message);
                PrintHelp();
                return 2;
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            using var server = new GameServer(config);
            server.Start();
            Console.WriteLine($"GLASSCORE dedicated server — lobby code {server.LobbyCode}, port {server.Port}. Ctrl+C to stop.");
            server.RunLoop(cts.Token);
            Console.WriteLine("Server stopped.");
            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("GlasscoreServer [--port 27015] [--name NAME] [--code CODE] [--map 0-2] [--max-players 2-8]");
            Console.WriteLine("                [--max-score 5-25] [--private] [--teams] [--friendly-fire] [--competitive]");
            Console.WriteLine("                [--data DIR] [--no-discovery]");
        }
    }
}
