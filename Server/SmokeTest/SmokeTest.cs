using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using RePlanet.Core;

namespace RePlanet.SmokeTest
{
    /// <summary>
    /// Rauchtest gegen einen laufenden dedizierten Server:
    ///   dotnet run --project Server/SmokeTest -- --host 127.0.0.1 --port 7777 [--saves ./server-saves] [--code CODE]
    /// Prüft: Sitzung erstellen, Beitritt per Code, gegenseitige Sichtbarkeit, Emote, Aktion, falscher Code,
    /// Speichern auf dem Server, Verlassen; optional den Beitritt zur dauerhaften Server-Welt (--code).
    /// </summary>
    public static class Program
    {
        static int pass, fail;
        static readonly List<GameClient> clients = new List<GameClient>();
        static string host = "127.0.0.1";
        static int port = 7777;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string saves = null, persistentCode = null;
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "--host": host = args[i + 1]; break;
                    case "--port": port = int.Parse(args[i + 1]); break;
                    case "--saves": saves = args[i + 1]; break;
                    case "--code": persistentCode = args[i + 1].ToUpperInvariant(); break;
                    default: Console.Error.WriteLine("Unbekannte Option " + args[i]); return 2;
                }
            }
            GameData.EnsureLoaded();
            Console.WriteLine("Rauchtest gegen " + host + ":" + port);

            // 1) Sitzung erstellen
            var a = Connect("smoke-a", "Tester A", null, true);
            Check("Sitzung erstellen (Client A)", Pump(() => a.Joined || a.FatalError != null, 10), () => a.Joined ? "Code " + a.Code : a.FatalError);
            if (!a.Joined) return Finish();
            Check("A ist Host auf dediziertem Server", a.IsHost && a.Dedicated && !string.IsNullOrEmpty(a.Code), () => "host=" + a.IsHost + " dedicated=" + a.Dedicated);

            // 2) Beitritt per Code
            var b = Connect("smoke-b", "Tester B", a.Code, false);
            Check("Beitritt per Sitzungscode (Client B)", Pump(() => b.Joined || b.FatalError != null, 10), () => b.Joined ? "beigetreten" : b.FatalError);
            if (!b.Joined) return Finish();
            Check("B ist Gast, Host ist A", !b.IsHost && b.HostPid == "smoke-a", () => "host=" + b.IsHost + " hostPid=" + b.HostPid);

            // 3) Gegenseitige Sichtbarkeit (Zustands-Patch + Positionspakete)
            bool bSeen = Pump(() => OnlineIn(a, "smoke-b") && a.Players.ContainsKey("smoke-b"), 5);
            Check("Spieler B ist bei A sichtbar", bSeen, () => "Spieler bei A: " + string.Join(", ", Names(a)));
            bool aSeen = Pump(() => OnlineIn(b, "smoke-a") && b.Players.ContainsKey("smoke-a"), 5);
            Check("Spieler A ist bei B sichtbar", aSeen, () => "Spieler bei B: " + string.Join(", ", Names(b)));

            // 4) Emote von B kommt bei A an
            string emoteFrom = null;
            a.Emote += (pid, e) => { if (e == "hallo") emoteFrom = pid; };
            b.SendEmote("hallo");
            Check("Emote von B erreicht A", Pump(() => emoteFrom != null, 5), () => "von " + emoteFrom);

            // 5) Aktion mit Serverantwort
            ActResult res = null;
            b.Act(new JObj().Set("a", "wake"), r => res = r);
            Check("Aktion von B wird vom Server beantwortet", Pump(() => res != null, 5) && res.Ok, () => res == null ? "keine Antwort" : "ok=" + res.Ok + " " + res.Err);

            // 6) Falscher Code
            var x = Connect("smoke-x", "Tester X", "ZZZZ99", false);
            Check("Falscher Code wird abgelehnt", Pump(() => x.FatalError != null, 5) && !x.Joined, () => x.FatalError);

            // 7) Speichern auf dem Server
            string saved = null;
            a.SaveReceived += (text, reason) => saved = reason;
            a.RequestSave("Rauchtest");
            Check("Host-Speicheranfrage liefert Spielstand", Pump(() => saved != null, 5), () => "Anlass: " + saved);
            if (saves != null)
            {
                string file = Path.Combine(saves, a.Code + ".rpsave");
                Check("Server hat " + a.Code + ".rpsave geschrieben", Pump(() => File.Exists(file), 5), () => file);
                if (File.Exists(file))
                {
                    string err;
                    var w = SaveCodec.Decode(File.ReadAllText(file, Encoding.UTF8), out err);
                    Check("Gespeicherter Stand ist gültig", w != null && w.Players.ContainsKey("smoke-a") && w.Players.ContainsKey("smoke-b"), () => err ?? "Spieler: " + (w == null ? "-" : string.Join(", ", w.Players.Keys)));
                }
            }

            // 8) Gast verlässt die Sitzung
            b.Leave();
            Check("A sieht, dass B gegangen ist", Pump(() => !OnlineIn(a, "smoke-b"), 5), () => "online=" + OnlineIn(a, "smoke-b"));

            // 9) Host verlässt → Sitzung endet, Code ist danach ungültig
            string code = a.Code;
            a.Leave();
            Pump(() => false, 0.7);
            var y = Connect("smoke-y", "Tester Y", code, false);
            Check("Nach Host-Verlassen ist die Sitzung beendet", Pump(() => y.FatalError != null || y.Joined, 5) && !y.Joined, () => y.Joined ? "noch offen" : y.FatalError);

            // 10) Optional: dauerhafte Server-Welt
            if (persistentCode != null)
            {
                var p1 = Connect("smoke-p1", "Tester P1", persistentCode, false);
                Check("Beitritt zur dauerhaften Welt " + persistentCode, Pump(() => p1.Joined || p1.FatalError != null, 10) && p1.Joined, () => p1.Joined ? "Host=" + p1.IsHost : p1.FatalError);
                if (p1.Joined)
                {
                    var p2 = Connect("smoke-p2", "Tester P2", persistentCode, false);
                    Check("Zweiter Spieler in der dauerhaften Welt", Pump(() => p2.Joined || p2.FatalError != null, 10) && p2.Joined && !p2.IsHost, () => p2.Joined ? "Host=" + p2.IsHost : p2.FatalError);
                    Check("P2 ist bei P1 sichtbar", Pump(() => OnlineIn(p1, "smoke-p2") && p1.Players.ContainsKey("smoke-p2"), 5), () => string.Join(", ", Names(p1)));
                    p2.Leave();
                    p1.Leave();
                }
            }
            Pump(() => false, 0.3);
            foreach (var c in clients) c.T.Close();
            return Finish();
        }

        static GameClient Connect(string pid, string name, string code, bool create)
        {
            var t = new TcpClientTransport();
            t.Connect(host, port);
            var c = new GameClient(t);
            c.Hello(pid, name, code, null, null, false, create);
            clients.Add(c);
            return c;
        }

        static bool OnlineIn(GameClient c, string pid)
        {
            PlayerData p;
            return c.W != null && c.W.Players.TryGetValue(pid, out p) && p.Online;
        }

        static IEnumerable<string> Names(GameClient c)
        {
            if (c.W == null) yield break;
            foreach (var p in c.W.Players.Values) yield return p.Name + (p.Online ? "" : " (offline)");
        }

        /// <summary>Aktualisiert alle Clients, bis die Bedingung erfüllt ist oder die Zeit abläuft.</summary>
        static bool Pump(Func<bool> done, double seconds)
        {
            var sw = Stopwatch.StartNew();
            double last = 0;
            while (true)
            {
                double now = sw.Elapsed.TotalSeconds;
                foreach (var c in clients) c.Update((float)(now - last));
                last = now;
                if (done()) return true;
                if (now > seconds) return false;
                Thread.Sleep(15);
            }
        }

        static void Check(string name, bool ok, Func<string> detail)
        {
            string d = "";
            try { d = detail() ?? ""; } catch (Exception) { }
            if (ok) { pass++; Console.WriteLine("OK      " + name + (d.Length > 0 ? " – " + d : "")); }
            else { fail++; Console.WriteLine("FEHLER  " + name + (d.Length > 0 ? " – " + d : "")); }
        }

        static int Finish()
        {
            Console.WriteLine("Rauchtest: " + pass + " bestanden, " + fail + " fehlgeschlagen.");
            return fail == 0 && pass > 0 ? 0 : 1;
        }
    }
}
