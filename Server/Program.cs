using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using RePlanet.Core;

namespace RePlanet.Server
{
    /// <summary>
    /// Dedizierter Koop-Server für RE:PLANET (reines .NET, ohne Unity).
    /// Nutzt exakt dieselbe serverautoritative Simulation (Core/Net/Session.cs) wie der Host im Spiel.
    /// </summary>
    public static class Program
    {
        const string Usage =
@"RE:PLANET – dedizierter Koop-Server

Aufruf:
  dotnet run --project Server -- [Optionen]
  (oder nach dem Veröffentlichen: RePlanet.Server [Optionen])

Optionen:
  --port <Zahl>       TCP-Port (Standard: 7777). Muss in Firewall/Router freigegeben sein.
  --saves <Ordner>    Ordner für Spielstände (Standard: ./server-saves).
                      Jede Sitzung wird als <Ordner>/<Sitzungscode>.rpsave gespeichert (+ .bak.rpsave).
  --code <CODE>       Sitzungscode der dauerhaften Server-Welt (4–12 Zeichen, A–Z und 0–9).
                      Ohne Angabe: Code des zuletzt gespeicherten Standes im Ordner, sonst ein neuer Zufallscode.
  --world <Name>      Name einer neu angelegten Server-Welt (Standard: ""Server-Welt"").
  --planet <Planet>   Startplanet einer neuen Welt: terra, pyra oder pelagia (Standard: terra).
  --no-world          Keine dauerhafte Welt öffnen; nur Sitzungen, die Clients selbst erstellen.
  --help              Diese Hilfe.

Mitspieler treten im Spiel über Pause › Koop › „Beitreten“ mit der Einladung <Adresse>:<Port>/<CODE> bei.
Der erste Spieler in der dauerhaften Welt wird Host (darf reisen und teure Käufe freigeben).
Beenden mit Strg+C – alle Welten werden vorher gespeichert.";

        static readonly object logLock = new object();
        static volatile bool stopRequested;
        static readonly List<IDisposable> signalRegs = new List<IDisposable>();

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            int port = 7777;
            string savesDir = "./server-saves";
            string code = null, worldName = "Server-Welt", planet = "terra";
            bool persistentWorld = true;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (a.ToLowerInvariant())
                {
                    case "-h": case "--help": case "/?": case "--hilfe":
                        Console.WriteLine(Usage);
                        return 0;
                    case "--port":
                        if (next == null || !int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
                            return Fail("Ungültiger Port: " + (next ?? "(fehlt)") + ". Erlaubt: 1–65535.");
                        i++;
                        break;
                    case "--saves":
                        if (string.IsNullOrWhiteSpace(next)) return Fail("--saves braucht einen Ordner.");
                        savesDir = next; i++;
                        break;
                    case "--code":
                        if (next == null || !ValidCode(next)) return Fail("Ungültiger Sitzungscode: " + (next ?? "(fehlt)") + ". Erlaubt: 4–12 Zeichen A–Z und 0–9.");
                        code = next.Trim().ToUpperInvariant(); i++;
                        break;
                    case "--world":
                        if (string.IsNullOrWhiteSpace(next)) return Fail("--world braucht einen Namen.");
                        worldName = next.Trim(); i++;
                        break;
                    case "--planet":
                        {
                            string p = (next ?? "").Trim().ToLowerInvariant();
                            PlanetDef pd;
                            if (!GameData.Planets.TryGetValue(p, out pd) || !pd.StartPlanet)
                                return Fail("Ungültiger Startplanet: " + (next ?? "(fehlt)") + ". Erlaubt: terra, pyra, pelagia.");
                            planet = p; i++;
                            break;
                        }
                    case "--no-world":
                        persistentWorld = false;
                        break;
                    default:
                        return Fail("Unbekannte Option: " + a + "\n\n" + Usage);
                }
            }

            GameData.EnsureLoaded();
            SaveStore store;
            try { store = new SaveStore(Path.GetFullPath(savesDir)); }
            catch (Exception e) { return Fail("Spielstandordner nicht nutzbar (" + savesDir + "): " + e.Message); }

            Log("RE:PLANET-Server startet (Protokoll " + Session.ProtocolVersion + ", max. " + GameData.MaxPlayers + " Spieler je Sitzung).");
            Log("Spielstände: " + store.Dir);

            var hub = new SessionHub(true);
            hub.Log = Log;
            hub.OnServerSave = (session, text, reason) =>
            {
                string err;
                if (store.SaveText(session.Code, text, out err))
                    Log("Gesichert: Sitzung " + session.Code + " (" + reason + ") → " + Path.GetFileName(store.PathOf(session.Code)) + ", " + (text.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB");
                else
                    Log("FEHLER beim Sichern von " + session.Code + " (" + reason + "): " + err);
            };

            var tcp = new TcpServerTransport();
            string startError;
            if (!tcp.Start(port, out startError)) return Fail(startError);
            hub.AddTransport(tcp);
            Log("Lausche auf TCP-Port " + tcp.Port + ".");

            if (persistentWorld && code == null) code = MostRecentCode(store) ?? Ids.Code(6);
            bool persistentBroken = false;
            if (persistentWorld && EnsurePersistent(hub, store, code, worldName, planet) == null) persistentBroken = true;
            if (persistentWorld && !persistentBroken)
            {
                foreach (var ip in HostServer.LocalAddresses()) Log("Einladung (LAN/VPN): " + ip + ":" + tcp.Port + "/" + code);
                Log("Aus dem Internet: <öffentliche IP>:" + tcp.Port + "/" + code + " (TCP-Port " + tcp.Port + " im Router weiterleiten).");
            }
            else if (!persistentWorld) Log("Keine dauerhafte Welt (--no-world): Clients können eigene Sitzungen erstellen.");

            RegisterSignals();
            Log("Bereit. Beenden mit Strg+C.");

            var known = new HashSet<string>();
            foreach (var s in hub.Sessions) known.Add(s.Code);
            var sw = Stopwatch.StartNew();
            double last = sw.Elapsed.TotalSeconds, lastPersistCheck = 0, lastStatus = last;
            const double Step = 1.0 / 30.0;
            int errorCount = 0;
            while (!stopRequested)
            {
                double now = sw.Elapsed.TotalSeconds;
                float dt = (float)Math.Min(0.25, Math.Max(0, now - last));
                last = now;
                try
                {
                    // Die Simulation ruht, solange niemand verbunden ist.
                    bool anyone = false;
                    foreach (var s in hub.Sessions) if (s.OnlineCount > 0) { anyone = true; break; }
                    hub.Update(dt, anyone);

                    // Neue (von Clients erstellte) Sitzungen melden
                    foreach (var s in hub.Sessions)
                        if (known.Add(s.Code) && s.Code != code) Log("Neue Sitzung " + s.Code + " – Welt „" + s.Game.S.WorldName + "“.");
                    known.RemoveWhere(c => { foreach (var s in hub.Sessions) if (s.Code == c) return false; return true; });

                    // Dauerhafte Welt nach Sitzungsende (z. B. Host hat verlassen) aus dem Spielstand neu öffnen
                    if (persistentWorld && !persistentBroken && now - lastPersistCheck > 1.0)
                    {
                        lastPersistCheck = now;
                        if (EnsurePersistent(hub, store, code, worldName, planet) == null) persistentBroken = true;
                    }
                    if (now - lastStatus > 1800)
                    {
                        lastStatus = now;
                        int players = 0, sessions = 0;
                        foreach (var s in hub.Sessions) { sessions++; players += s.OnlineCount; }
                        Log("Status: " + sessions + " Sitzung(en), " + players + " Spieler online.");
                    }
                }
                catch (Exception e)
                {
                    errorCount++;
                    if (errorCount <= 5 || errorCount % 100 == 0) Log("FEHLER in der Serverschleife (#" + errorCount + "): " + e);
                }
                double spent = sw.Elapsed.TotalSeconds - now;
                int sleep = (int)((Step - spent) * 1000);
                Thread.Sleep(Math.Max(1, sleep));
            }

            Shutdown(hub);
            return 0;
        }

        /// <summary>Stellt sicher, dass die dauerhafte Welt als Sitzung offen ist. null = Spielstand unbrauchbar.</summary>
        static Session EnsurePersistent(SessionHub hub, SaveStore store, string code, string worldName, string planet)
        {
            foreach (var s in hub.Sessions) if (s.Code == code) return s;
            WorldState w;
            if (File.Exists(store.PathOf(code)) || File.Exists(store.BackupOf(code)))
            {
                string err; bool fromBackup;
                w = store.Load(code, out err, out fromBackup);
                if (w == null)
                {
                    Log("FEHLER: Spielstand " + store.PathOf(code) + " ist nicht ladbar (" + err + "). Die Welt wird NICHT geöffnet, damit nichts überschrieben wird.");
                    return null;
                }
                if (fromBackup) Log("Warnung: " + err);
                Log("Welt „" + w.WorldName + "“ aus " + Path.GetFileName(store.PathOf(code)) + " geladen.");
            }
            else
            {
                w = Game.NewWorld(worldName, planet);
                Log("Neue Welt „" + w.WorldName + "“ angelegt (Startplanet " + GameData.Planets[planet].Name + ").");
            }
            var session = hub.CreateSession(w, code);
            // HostPid bleibt leer: Wer zuerst beitritt, wird Host der Sitzung.
            Log("Dauerhafte Welt geöffnet – Sitzungscode " + code + ".");
            return session;
        }

        /// <summary>Code des zuletzt geschriebenen Spielstands im Ordner (Dateiname ohne Endung).</summary>
        static string MostRecentCode(SaveStore store)
        {
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            try
            {
                foreach (var f in Directory.GetFiles(store.Dir, "*.rpsave"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || !ValidCode(name)) continue;
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > bestTime) { bestTime = t; best = name.ToUpperInvariant(); }
                }
            }
            catch (Exception) { }
            return best;
        }

        static bool ValidCode(string c)
        {
            if (c == null) return false;
            c = c.Trim();
            if (c.Length < 4 || c.Length > 12) return false;
            foreach (char ch in c)
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))) return false;
            return true;
        }

        static void Shutdown(SessionHub hub)
        {
            Log("Server wird beendet – Welten werden gesichert …");
            const string msg = "Der Server wurde beendet. Die Welt wurde auf dem Server gesichert.";
            try
            {
                foreach (var s in new List<Session>(hub.Sessions))
                {
                    s.FlushSave("Server wird beendet");
                    s.End(msg);
                }
                // Abschiedsnachrichten noch zustellen lassen
                for (int i = 0; i < 10; i++) { hub.Update(0.05f, false); Thread.Sleep(50); }
                hub.Shutdown(msg);
            }
            catch (Exception e) { Log("FEHLER beim Beenden: " + e.Message); }
            foreach (var r in signalRegs) try { r.Dispose(); } catch (Exception) { }
            Log("Beendet.");
        }

        static void RegisterSignals()
        {
            Console.CancelKeyPress += (s, e) =>
            {
                if (stopRequested) return; // zweites Strg+C beendet sofort
                e.Cancel = true;
                stopRequested = true;
                Log("Strg+C erkannt.");
            };
            foreach (var sig in new[] { PosixSignal.SIGTERM, PosixSignal.SIGHUP, PosixSignal.SIGQUIT })
            {
                try
                {
                    var name = sig.ToString();
                    signalRegs.Add(PosixSignalRegistration.Create(sig, ctx =>
                    {
                        ctx.Cancel = true;
                        if (!stopRequested) Log("Signal " + name + " erkannt.");
                        stopRequested = true;
                    }));
                }
                catch (Exception) { /* Plattform ohne dieses Signal */ }
            }
        }

        static void Log(string msg)
        {
            lock (logLock) Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + msg);
        }

        static int Fail(string msg)
        {
            Console.Error.WriteLine("Fehler: " + msg);
            return 2;
        }
    }
}
