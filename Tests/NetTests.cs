using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RePlanet.Core;

/// <summary>Zeichnet empfangene Nachrichtentypen auf (Reihenfolge prüfen), leitet sonst alles durch.</summary>
public class RecordingTransport : IClientTransport
{
    readonly IClientTransport inner;
    public readonly List<string> Types = new List<string>();
    public readonly List<JObj> Messages = new List<JObj>();
    public RecordingTransport(IClientTransport inner) { this.inner = inner; }
    public bool Connected { get { return inner.Connected; } }
    public bool Failed { get { return inner.Failed; } }
    public string Error { get { return inner.Error; } }
    public void Send(string msg) { inner.Send(msg); }
    public bool TryReceive(out string msg)
    {
        if (!inner.TryReceive(out msg)) return false;
        JObj o;
        if (Json.TryParseObj(msg, out o)) { Types.Add(o.Str("t")); Messages.Add(o); }
        return true;
    }
    public void Close() { inner.Close(); }
    public int Count(string type) { return Types.Count(t => t == type); }
}

/// <summary>Testaufbau: Host im Prozess (lokal) und/oder dedizierter Hub, Gäste lokal oder über echtes TCP.</summary>
public class NetRig : IDisposable
{
    public HostServer Host;
    public SessionHub Hub;
    public TcpServerTransport Tcp;
    public readonly List<GameClient> Clients = new List<GameClient>();
    public int Port;
    public float Dt = 0.02f;

    public static NetRig HostWithLocalHost(out GameClient hostClient, bool online = true, string hostPid = "host")
    {
        var r = new NetRig();
        r.Host = new HostServer(Game.NewWorld("Koop-Test"), hostPid);
        if (online)
        {
            string err;
            if (!r.Host.OpenOnline(0, out err)) throw new Exception("TCP-Start: " + err);
            r.Port = r.Host.Port;
        }
        var hc = r.Local(hostPid, "Host");
        r.PumpUntil(() => hc.Joined, "Host tritt bei");
        hostClient = hc;
        return r;
    }

    public static NetRig Dedicated()
    {
        var r = new NetRig();
        r.Hub = new SessionHub(true);
        r.Tcp = new TcpServerTransport();
        string err;
        if (!r.Tcp.Start(0, out err)) throw new Exception("TCP-Start: " + err);
        r.Hub.AddTransport(r.Tcp);
        r.Port = r.Tcp.Port;
        return r;
    }

    public Session Session { get { return Host != null ? Host.Session : Hub.Sessions.FirstOrDefault(); } }
    public Game Game { get { return Session.Game; } }
    public string Code { get { return Session.Code; } }

    public GameClient Local(string pid, string name, bool record = false)
    {
        IClientTransport t = Host.ConnectLocal();
        if (record) t = new RecordingTransport(t);
        var c = new GameClient(t);
        c.Hello(pid, name, Host.Session.Code, null, null, true);
        Clients.Add(c);
        return c;
    }

    public GameClient Tcp4(string pid, string name, string code, string token = null, bool create = false, bool record = false)
    {
        var tt = new TcpClientTransport();
        tt.Connect("127.0.0.1", Port, 3000);
        IClientTransport t = tt;
        if (record) t = new RecordingTransport(t);
        var c = new GameClient(t);
        c.Hello(pid, name, code, null, token, false, create);
        Clients.Add(c);
        return c;
    }

    public void Step()
    {
        if (Host != null) Host.Update(Dt); else Hub.Update(Dt);
        foreach (var c in Clients.ToList()) c.Update(Dt);
    }

    /// <summary>Server und Clients abwechselnd pumpen, bis die Bedingung gilt (Echtzeit-Zeitlimit, da TCP in Threads läuft).</summary>
    public void PumpUntil(Func<bool> cond, string what, double timeoutSec = 10)
    {
        var sw = Stopwatch.StartNew();
        while (!cond())
        {
            Step();
            if (sw.Elapsed.TotalSeconds > timeoutSec) throw new AssertException("Zeitüberschreitung: " + what);
            Thread.Sleep(1);
        }
    }

    public void PumpFor(double realSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < realSeconds) { Step(); Thread.Sleep(1); }
    }

    /// <summary>Aktion senden und auf die Antwort warten.</summary>
    public ActResult Act(GameClient c, JObj a)
    {
        ActResult res = null;
        c.Act(a, r => res = r);
        PumpUntil(() => res != null, "Antwort auf " + a.Str("a"));
        return res;
    }

    /// <summary>Server-seitiges Versetzen (Testhilfe, entspricht einem Teleport durch das Spiel).</summary>
    public void Place(string pid, V3 pos)
    {
        Game.AllowTeleport(pid);
        if (!Game.Move(pid, pos, 0, false, 0, "grab", 0.1f)) throw new Exception("Versetzen abgelehnt");
    }

    public V3 Station(string s) { return WorldGen.Get(Game.S.CurrentPlanet).Base.Stations[s]; }

    public void Dispose()
    {
        foreach (var c in Clients) { try { c.T.Close(); } catch { } }
        try { if (Host != null) Host.Shutdown("Testende"); } catch { }
        try { if (Hub != null) Hub.Shutdown("Testende"); } catch { }
    }
}

/// <summary>Koop-Netzwerk: Beitritt, Gleichzeitigkeit, Idempotenz, Rechte, spätes Beitreten, Wiederverbinden, Host-Ende, Manipulation.</summary>
public static class NetTests
{
    [Test]
    public static void Vier_Clients_per_TCP_ein_fuenfter_wird_abgelehnt()
    {
        using (var rig = NetRig.Dedicated())
        {
            // Dedizierter Server: der erste Client erstellt die Sitzung, die anderen treten mit Code bei
            var c0 = rig.Tcp4("p0", "Anna", null, null, true);
            rig.PumpUntil(() => c0.Joined, "Sitzung erstellen");
            Assert.True(c0.IsHost, "Ersteller ist Host");
            string code = c0.Code;
            Assert.True(!string.IsNullOrEmpty(code), "Sitzungscode vergeben");
            var others = new List<GameClient>();
            for (int i = 1; i < 4; i++) others.Add(rig.Tcp4("p" + i, "Gast " + i, code));
            rig.PumpUntil(() => others.All(c => c.Joined), "Drei Gäste treten bei");
            Assert.Equal(4, rig.Session.OnlineCount, "Vier Spieler online");
            Assert.True(others.All(c => !c.IsHost && c.HostPid == "p0"), "Gäste kennen den Host");
            // Alle sehen alle (Positionspakete)
            rig.PumpUntil(() => rig.Clients.Take(4).All(c => c.Players.Count == 4), "Positionspakete aller Spieler");

            var c5 = rig.Tcp4("p5", "Zu spät", code);
            rig.PumpUntil(() => c5.FatalError != null, "Ablehnung des fünften Clients");
            Assert.True(c5.FatalError.Contains("voll"), "Meldung „voll“: " + c5.FatalError);
            Assert.False(c5.Joined, "Fünfter ist nicht beigetreten");
            Assert.Equal(4, rig.Session.OnlineCount, "Weiterhin vier Spieler");
            rig.PumpUntil(() => c5.T.Failed, "Verbindung des Abgelehnten wird getrennt");

            // Unbekannter Code wird verständlich abgelehnt
            var wrong = rig.Tcp4("px", "Falsch", "ZZZZZZ");
            rig.PumpUntil(() => wrong.FatalError != null, "Falscher Code");
            Assert.True(wrong.FatalError.Contains("nicht gefunden"), "Meldung: " + wrong.FatalError);

            // Nach dem Verlassen eines Gastes ist wieder Platz
            others[2].Leave();
            rig.PumpUntil(() => rig.Session.OnlineCount == 3, "Gast verlässt die Sitzung");
            var c6 = rig.Tcp4("p6", "Nachrücker", code);
            rig.PumpUntil(() => c6.Joined || c6.FatalError != null, "Nachrücker");
            Assert.True(c6.Joined, "Nachrücker tritt bei: " + c6.FatalError);
        }
    }

    [Test]
    public static void Gleichzeitiges_Greifen_nur_einer_gewinnt()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var g = rig.Tcp4("gast", "Gast", rig.Code);
            rig.PumpUntil(() => g.Joined, "Gast tritt bei");
            rig.Game.S.Tech["bin"] = 4;
            var objs = TestKit.Grabbables(rig.Game, 6);
            int okCount = 0;
            foreach (var o in objs)
            {
                rig.Place("host", new V3(o.Pos.x + 0.6f, o.Pos.y, o.Pos.z));
                rig.Place("gast", new V3(o.Pos.x - 0.6f, o.Pos.y, o.Pos.z));
                rig.PumpFor(0.02); // Greif-Sperrzeit (0,15 s Spielzeit) verstreichen lassen
                for (int i = 0; i < 12; i++) rig.Step();
                ActResult rh = null, rg = null;
                // Beide senden im selben Moment
                g.Act(TestKit.A("grab").Set("o", o.Key), r => rg = r);
                h.Act(TestKit.A("grab").Set("o", o.Key), r => rh = r);
                rig.PumpUntil(() => rh != null && rg != null, "Beide Antworten");
                int ok = (rh.Ok ? 1 : 0) + (rg.Ok ? 1 : 0);
                Assert.Equal(1, ok, "Genau einer ist erfolgreich (" + o.Key + ": Host " + rh.Err + ", Gast " + rg.Err + ")");
                okCount += ok;
                var loser = rh.Ok ? rg : rh;
                Assert.True(loser.Err != null && loser.Err.Contains("eingesammelt"), "Verlierer erfährt den Grund: " + loser.Err);
            }
            var hs = rig.Game.S.Players["host"]; var gs = rig.Game.S.Players["gast"];
            Assert.Equal(objs.Count, hs.Bin.Count + gs.Bin.Count, "Jedes Objekt genau einmal in einem Behälter");
            Assert.Equal(objs.Count, (int)rig.Game.S.Stat("collected"), "Statistik zählt jedes Objekt einmal");
            // Replikate der Clients stimmen mit dem Server überein
            rig.PumpFor(0.1);
            foreach (var c in new[] { h, g })
                foreach (var o in objs) Assert.True(c.W.Cur.Removed.Get(o.Sid), "Client sieht " + o.Key + " als entfernt");
            Assert.Equal(hs.Bin.Count, g.W.Players["host"].Bin.Count, "Gast sieht Host-Behälter korrekt");
            Assert.Equal(gs.Bin.Count, h.W.Players["gast"].Bin.Count, "Host sieht Gast-Behälter korrekt");
        }
    }

    [Test]
    public static void Doppelt_gesendete_Aktion_wird_nur_einmal_verarbeitet()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var g = rig.Tcp4("gast", "Gast", rig.Code, null, false, true);
            rig.PumpUntil(() => g.Joined, "Gast tritt bei");
            rig.Game.S.Cur.Store("glas").S = 30;
            rig.Place("gast", rig.Station("sell"));
            long before = rig.Game.S.Credits;
            var a = TestKit.A("sell").Set("m", "glas").Set("g", 1).Set("n", 10);
            int callbacks = 0; ActResult first = null;
            string rid = g.Act(a, r => { callbacks++; first = r; });
            g.Resend(rid, a); // sofortige Wiederholung (z. B. Netzaussetzer)
            rig.PumpUntil(() => ((RecordingTransport)g.T).Count("res") >= 2, "Zwei Antworten");
            g.Resend(rid, a); // späte Wiederholung
            rig.PumpUntil(() => ((RecordingTransport)g.T).Count("res") >= 3, "Dritte Antwort");
            long value = 10 * GameData.Materials["glas"].Price;
            Assert.True(first != null && first.Ok, "Verkauf klappt: " + (first != null ? first.Err : "keine Antwort"));
            Assert.Equal(before + value, rig.Game.S.Credits, "Nur einmal vergütet");
            Assert.Equal(20, rig.Game.S.Cur.Store("glas").S, "Nur einmal abgezogen");
            Assert.Equal(1, callbacks, "Rückruf beim Client genau einmal");
            var res = ((RecordingTransport)g.T).Messages.Where(m => m.Str("t") == "res").ToList();
            Assert.True(res.All(m => m.Str("rid") == rid && m.Bool("ok") && m.Obj("data").Long("credits") == value), "Wiederholungen liefern das gespeicherte Ergebnis");
            // Eine neue Anfrage (neue ID) wird dagegen normal verarbeitet
            Assert.True(rig.Act(g, a).Ok, "Neue Anfrage");
            Assert.Equal(before + 2 * value, rig.Game.S.Credits, "Zweiter echter Verkauf");
            // Die ID eines anderen Spielers kollidiert nicht
            rig.Place("host", rig.Station("sell"));
            h.Resend(rid, a);
            rig.PumpFor(0.05);
            Assert.Equal(before + 3 * value, rig.Game.S.Credits, "Gleiche Anfrage-ID eines anderen Spielers wird eigenständig verarbeitet");
        }
    }

    [Test]
    public static void Gast_darf_teure_Kaeufe_Abriss_und_Reisen_nicht_ausser_im_Vertrauensmodus()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var g = rig.Tcp4("gast", "Gast", rig.Code);
            rig.PumpUntil(() => g.Joined, "Gast tritt bei");
            var S = rig.Game.S;
            S.Credits = 20000;
            S.Cur.Store("metall").S = 100; S.Cur.Store("elektronik").S = 20;
            rig.Place("host", rig.Station("build"));
            var rb = rig.Act(h, TestKit.A("build").Set("t", "lager").Set("x", 0).Set("z", 0).Set("r", 0));
            Assert.True(rb.Ok, "Host baut: " + rb.Err);
            int bid = rb.Data.Int("b");
            rig.Place("gast", rig.Station("workshop"));
            long c0 = S.Credits;

            Assert.True(GameData.Tech["bin"].Levels[1].Cost < GameData.GuestExpensiveThreshold, "Testvoraussetzung: günstiges Upgrade");
            Assert.True(rig.Act(g, TestKit.A("buytech").Set("id", "bin")).Ok, "Günstiges Upgrade ist für Gäste erlaubt");
            c0 = S.Credits;
            var denied = new List<ActResult>
            {
                rig.Act(g, TestKit.A("buyveh").Set("id", "rover")),                                  // 2200
                rig.Act(g, TestKit.A("buytech").Set("id", "cutter")),                               // 1100
                rig.Act(g, TestKit.A("build").Set("t", "drohnenhangar").Set("x", 10).Set("z", 0).Set("r", 0)), // 1100
                rig.Act(g, TestKit.A("buyship")),                                                    // 9000
                rig.Act(g, TestKit.A("demolish").Set("b", bid)),
            };
            foreach (var r in denied) Assert.True(!r.Ok && r.Err.Contains("Host"), "Gast abgelehnt: " + r.Err);
            rig.Place("gast", rig.Station("ship"));
            var rt = rig.Act(g, TestKit.A("travel").Set("planet", "pyra"));
            Assert.True(!rt.Ok && rt.Err.Contains("Host"), "Reisen nur durch den Host: " + rt.Err);
            Assert.Equal(c0, S.Credits, "Keine Credits abgezogen");
            Assert.False(S.OwnedVehicles.Contains("rover"), "Kein Fahrzeug gekauft");
            Assert.Equal(1, S.Cur.Buildings.Count, "Nichts abgerissen oder gebaut");
            Assert.Equal("terra", S.CurrentPlanet, "Nicht gereist");

            // Nur der Host kann den Vertrauensmodus schalten
            var rtg = rig.Act(g, TestKit.A("trust").Set("v", true));
            Assert.False(rtg.Ok, "Gast kann Vertrauensmodus nicht setzen");
            Assert.False(S.TrustGuests, "Vertrauensmodus bleibt aus");
            Assert.True(rig.Act(h, TestKit.A("trust").Set("v", true)).Ok, "Host aktiviert Vertrauensmodus");
            rig.PumpUntil(() => g.W.TrustGuests, "Gast sieht den Vertrauensmodus");

            rig.Place("gast", rig.Station("workshop"));
            var rv = rig.Act(g, TestKit.A("buyveh").Set("id", "rover"));
            Assert.True(rv.Ok, "Mit Vertrauen darf der Gast teuer kaufen: " + rv.Err);
            Assert.True(rig.Act(g, TestKit.A("buytech").Set("id", "cutter")).Ok, "Teures Upgrade mit Vertrauen");
            Assert.Equal(c0 - GameData.Vehicles["rover"].Cost - GameData.Tech["cutter"].Levels[1].Cost, S.Credits, "Korrekt abgebucht");
            Assert.True(rig.Act(g, TestKit.A("demolish").Set("b", bid)).Ok, "Abriss mit Vertrauen");
            rig.Place("gast", rig.Station("ship"));
            rt = rig.Act(g, TestKit.A("travel").Set("planet", "pyra"));
            Assert.False(rt.Ok, "Reisen bleibt dem Host vorbehalten");
            // Host kann reisen
            rig.Place("host", rig.Station("ship"));
            Assert.True(rig.Act(h, TestKit.A("travel").Set("planet", "pyra")).Ok, "Host reist");
            rig.PumpUntil(() => g.W.CurrentPlanet == "pyra", "Gast reist mit");
        }
    }

    [Test]
    public static void Spaeter_Beitritt_erhaelt_Snapshot_mit_entfernten_Objekten()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var S = rig.Game.S;
            var objs = TestKit.Grabbables(rig.Game, 5);
            foreach (var o in objs)
            {
                rig.Place("host", new V3(o.Pos.x + 0.5f, o.Pos.y, o.Pos.z));
                for (int i = 0; i < 10; i++) rig.Step();
                var r = rig.Act(h, TestKit.A("grab").Set("o", o.Key));
                Assert.True(r.Ok, "Host sammelt: " + r.Err);
            }
            rig.Place("host", rig.Station("contracts"));
            Assert.True(rig.Act(h, TestKit.A("delivery")).Ok, "Lieferung bestellt (dynamische Objekte)");
            S.Credits = 777;
            S.Cur.Store("glas").S = 12;
            var dynIds = S.Cur.Dyn.Keys.ToList();
            rig.PumpFor(0.05);

            var late = rig.Tcp4("spaet", "Spät", rig.Code);
            rig.PumpUntil(() => late.Joined, "Später Beitritt");
            var w = late.W;
            foreach (var o in objs)
            {
                Assert.True(w.Cur.Removed.Get(o.Sid), "Entfernt im Snapshot: " + o.Key);
                Assert.True(Rules.Obj(w.Cur, o.Key) == null, "Nicht sichtbar für den Nachzügler: " + o.Key);
            }
            Assert.Equal(S.Cur.Removed.Count, w.Cur.Removed.Count, "Gleiche Anzahl entfernter Objekte");
            foreach (var d in dynIds) Assert.True(w.Cur.Dyn.ContainsKey(d), "Dynamisches Objekt im Snapshot: " + d);
            Assert.Equal(777L, w.Credits, "Credits im Snapshot");
            Assert.Equal(12, w.Cur.Store("glas").S, "Lager im Snapshot");
            Assert.Equal(objs.Count, w.Players["host"].Bin.Count, "Behälter des Hosts im Snapshot");
            Assert.True(Math.Abs(w.PlayTime - S.PlayTime) < 1.0, "Spielzeit im Snapshot");
            // Danach laufen Positionspakete und die Spielzeit des Clients läuft mit (Tag/Nacht)
            double t0 = late.W.PlayTime;
            rig.PumpFor(0.3);
            Assert.True(late.W.PlayTime > t0, "Spielzeit des Clients wird fortgeschrieben (" + t0 + " → " + late.W.PlayTime + ")");
            Assert.True(Math.Abs(late.W.PlayTime - S.PlayTime) < 0.5, "Client-Zeit folgt der Serverzeit");
            Assert.Equal(Rules.IsNight(S, "terra"), Rules.IsNight(late.W, "terra"), "Client und Server sind sich über Tag/Nacht einig");
            Assert.True(late.Players.ContainsKey("host"), "Positionspakete kommen an");
        }
    }

    [Test]
    public static void Wiederverbinden_mit_Token_behaelt_Spieler_und_Behaelter()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var g = rig.Tcp4("gast", "Gast", rig.Code);
            rig.PumpUntil(() => g.Joined, "Gast tritt bei");
            string token = g.Token;
            Assert.True(!string.IsNullOrEmpty(token), "Token erhalten");
            var o = TestKit.Grabbables(rig.Game, 1)[0];
            rig.Place("gast", new V3(o.Pos.x + 0.5f, o.Pos.y, o.Pos.z));
            for (int i = 0; i < 10; i++) rig.Step();
            Assert.True(rig.Act(g, TestKit.A("grab").Set("o", o.Key)).Ok, "Gast sammelt");
            var posBefore = rig.Game.S.Players["gast"].Pos;

            // Verbindung reißt ab (kein „leave“)
            g.T.Close();
            rig.PumpUntil(() => !rig.Game.S.Players["gast"].Online, "Server bemerkt den Abbruch");
            Assert.Equal(1, rig.Session.OnlineCount, "Nur noch der Host online");
            rig.PumpFor(0.1);

            var g2 = rig.Tcp4("gast", "Gast", rig.Code, token);
            rig.PumpUntil(() => g2.Joined || g2.FatalError != null, "Wiederverbinden");
            Assert.True(g2.Joined, "Wiederverbunden: " + g2.FatalError);
            Assert.Equal("gast", g2.Pid, "Gleicher Spieler");
            Assert.Equal(token, g2.Token, "Gleicher Token");
            Assert.Equal(1, rig.Game.S.Players["gast"].Bin.Count, "Behälterinhalt auf dem Server erhalten");
            Assert.Equal(1, g2.Me.Bin.Count, "Behälterinhalt beim Client");
            Assert.Equal(o.T.Id, g2.Me.Bin[0].T, "Richtiger Inhalt");
            TestKit.Near(posBefore, rig.Game.S.Players["gast"].Pos, 0.05f, "Position bleibt beim Wiederverbinden");
            Assert.Equal(2, rig.Game.S.Players.Count, "Kein zusätzlicher Spieler angelegt");

            // Doppelte Anmeldung ohne passenden Token wird abgelehnt, mit Token ersetzt sie die alte Verbindung
            var thief = rig.Tcp4("gast", "Dieb", rig.Code, "falsch");
            rig.PumpUntil(() => thief.FatalError != null, "Ablehnung ohne Token");
            Assert.True(thief.FatalError.Contains("bereits"), "Meldung: " + thief.FatalError);
            var g3 = rig.Tcp4("gast", "Gast", rig.Code, token);
            rig.PumpUntil(() => g3.Joined, "Übernahme mit Token");
            rig.PumpUntil(() => g2.T.Failed || g2.FatalError != null, "Alte Verbindung wird getrennt");
            Assert.Equal(2, rig.Session.OnlineCount, "Weiterhin zwei Spieler");
            Assert.Equal(1, g3.Me.Bin.Count, "Behälter auch nach Übernahme");
        }
    }

    [Test]
    public static void Host_verlaesst_Sitzung_Gaeste_erhalten_hostleft_Host_vorher_Spielstand()
    {
        var rig = new NetRig();
        rig.Host = new HostServer(Game.NewWorld("Koop-Test"), "host");
        string err;
        Assert.True(rig.Host.OpenOnline(0, out err), "TCP: " + err);
        rig.Port = rig.Host.Port;
        using (rig)
        {
            var h = rig.Local("host", "Host", true);
            rig.PumpUntil(() => h.Joined, "Host");
            var g1 = rig.Tcp4("g1", "Gast 1", rig.Code);
            var g2 = rig.Local("g2", "Gast 2");
            rig.PumpUntil(() => g1.Joined && g2.Joined, "Gäste");
            rig.Game.S.Credits = 4242;
            string saved = null;
            h.SaveReceived += (data, reason) => saved = data;

            h.Leave();
            rig.PumpUntil(() => g1.FatalError != null && g2.FatalError != null, "Gäste werden informiert");
            Assert.True(g1.Ended && g2.Ended, "Sitzung für Gäste beendet");
            Assert.True(g1.FatalError.Contains("Host"), "Verständliche Meldung: " + g1.FatalError);
            var rec = (RecordingTransport)h.T;
            rig.PumpUntil(() => rec.Types.Contains("ended"), "Host erhält Abschluss");
            int iSave = rec.Types.IndexOf("save"), iEnd = rec.Types.IndexOf("ended");
            Assert.True(iSave >= 0 && iSave < iEnd, "Host erhält den Spielstand vor dem Ende (" + string.Join(",", rec.Types.Where(t => t != "pos" && t != "patch")) + ")");
            Assert.True(saved != null, "Speicher-Ereignis beim Host");
            var w = SaveCodec.Decode(saved, out err);
            Assert.True(w != null && w.Credits == 4242, "Spielstand ist gültig und aktuell: " + err);
            Assert.True(rig.Session.Closed, "Sitzung geschlossen");
            Assert.Equal(0, rig.Host.Hub.Sessions.Count(), "Sitzung aus dem Hub entfernt");
            rig.PumpUntil(() => g1.T.Failed, "TCP-Verbindung des Gastes getrennt");
        }
    }

    [Test]
    public static void Dedizierter_Server_wartet_auf_Host_und_sichert()
    {
        using (var rig = NetRig.Dedicated())
        {
            var saves = new List<string>();
            rig.Hub.OnServerSave = (s, text, reason) => saves.Add(reason);
            var host = rig.Tcp4("host", "Host", null, null, true);
            rig.PumpUntil(() => host.Joined, "Host erstellt Sitzung");
            var g = rig.Tcp4("gast", "Gast", host.Code);
            string notice = null;
            g.Notice += m => notice = m;
            rig.PumpUntil(() => g.Joined, "Gast");
            string token = host.Token;
            host.T.Close(); // Verbindungsabbruch des Hosts
            rig.PumpUntil(() => notice != null, "Gast erhält Hinweis");
            Assert.True(notice.Contains("Host"), "Hinweis: " + notice);
            Assert.True(saves.Count >= 1, "Server sichert bei Host-Verlust");
            Assert.False(g.Ended, "Sitzung läuft zunächst weiter");
            // Host kommt innerhalb der Frist zurück
            var host2 = rig.Tcp4("host", "Host", host.Code, token);
            rig.PumpUntil(() => host2.Joined, "Host kehrt zurück");
            Assert.True(host2.IsHost, "Wieder Host");
            // Erneuter Abbruch, diesmal ohne Rückkehr → nach der Frist Ende mit hostleft
            host2.T.Close();
            rig.Dt = 0.25f;
            rig.PumpUntil(() => g.Ended, "Frist abgelaufen", 20);
            Assert.True(g.FatalError != null && g.FatalError.Contains("Host"), "hostleft: " + g.FatalError);
            Assert.True(saves.Any(r => r.Contains("nicht zurückgekehrt")), "Abschlusssicherung: " + string.Join(", ", saves));
        }
    }

    [Test]
    public static void Manipulierte_Nachrichten_werden_ignoriert_und_Positionen_korrigiert()
    {
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h))
        {
            var g = rig.Tcp4("gast", "Gast", rig.Code);
            rig.PumpUntil(() => g.Joined, "Gast");
            var S = rig.Game.S;
            long credits = S.Credits;
            // Rohe, manipulierte Nachrichten
            string[] raw =
            {
                "{\"t\":\"act\",\"rid\":\"m1\",\"a\":{\"a\":\"credits\",\"v\":999999}}",
                "{\"t\":\"patch\",\"w\":{\"credits\":999999}}",
                "{\"t\":\"welcome\",\"snapshot\":{\"credits\":999999}}",
                "{\"t\":\"act\",\"rid\":\"m2\",\"a\":{\"a\":\"sell\",\"m\":\"glas\",\"g\":1,\"n\":-50}}",
                "{\"t\":\"act\",\"rid\":\"m3\",\"a\":{\"a\":\"sell\",\"m\":\"glas\",\"g\":7,\"n\":1000}}",
                "{\"t\":\"act\",\"rid\":\"m4\",\"a\":{\"a\":\"sellbin\"}}",
                "{\"t\":\"act\",\"rid\":\"m5\",\"a\":{\"a\":\"contract\"}}",
                "{\"t\":\"act\",\"rid\":\"m6\",\"a\":{\"a\":\"trust\",\"v\":true}}",
                "{\"t\":\"act\",\"rid\":\"m7\",\"a\":{\"a\":\"buytech\",\"id\":\"bin\",\"cost\":-5000}}",
                "{\"t\":\"act\",\"rid\":\"m8\",\"a\":null}",
                "{\"t\":\"act\",\"a\":{\"a\":\"sellbin\"}}",
                "{\"t\":\"in\",\"p\":[0,0,-121],\"credits\":999999}",
                "{\"t\":\"reqsave\",\"reason\":\"hack\"}",
                "{{{ kein json",
                "{\"t\":\"hello\",\"v\":1,\"id\":\"host\",\"name\":\"Ich bin Host\"}",
            };
            S.Credits = 50; credits = 50; // zu wenig für das Upgrade
            foreach (var m in raw) g.T.Send(m);
            rig.PumpFor(0.3);
            Assert.Equal(credits, S.Credits, "Credits unverändert");
            Assert.Equal(0, S.TechLevel("bin"), "Kein Upgrade erschlichen");
            Assert.False(S.TrustGuests, "Kein Vertrauensmodus");
            Assert.Equal("host", rig.Session.HostPid, "Host bleibt Host");
            Assert.False(g.IsHost, "Gast bleibt Gast");
            Assert.True(g.FatalError == null && !g.T.Failed, "Verbindung bleibt stabil");
            Assert.Equal(2, rig.Session.OnlineCount, "Beide weiterhin online");
            // Lokale Manipulation der Client-Kopie ändert nichts am Server und wird überschrieben
            g.W.Credits = 999999;
            S.Cur.Store("glas").S = 10;
            rig.Place("gast", rig.Station("sell"));
            var rs = rig.Act(g, TestKit.A("sell").Set("m", "glas").Set("g", 1).Set("n", 10));
            Assert.True(rs.Ok, "Normaler Verkauf: " + rs.Err);
            rig.PumpFor(0.1);
            Assert.Equal(S.Credits, g.W.Credits, "Client-Kopie folgt dem Server");
            Assert.Equal(50L + 10 * GameData.Materials["glas"].Price, S.Credits, "Nur der echte Erlös");

            // Positionen: Teleport, ungültige Zahlen, außerhalb der Welt → Korrektur
            int corr = 0; V3 lastCorr = V3.Zero;
            g.Corrected += p => { corr++; lastCorr = p; };
            var server = S.Players["gast"].Pos;
            g.SendInput(new V3(server.x + 60, server.y, server.z + 60), 0, false, 0, "grab", 0.066f);
            rig.PumpUntil(() => corr >= 1, "Korrektur nach Teleport");
            TestKit.Near(server, S.Players["gast"].Pos, 0.01f, "Server-Position unverändert");
            TestKit.Near(server, lastCorr, 0.05f, "Korrektur nennt die Server-Position");
            g.T.Send("{\"t\":\"in\",\"p\":[\"NaN\",0,0],\"dt\":0.066}");
            rig.PumpUntil(() => corr >= 2, "Korrektur nach NaN");
            g.T.Send("{\"t\":\"in\",\"p\":[\"Infinity\",0,-120],\"dt\":0.066}");
            rig.PumpUntil(() => corr >= 3, "Korrektur nach Unendlich");
            g.SendInput(new V3(500, 0, 0), 0, false, 0, "grab", 0.066f);
            rig.PumpUntil(() => corr >= 4, "Korrektur außerhalb der Welt");
            TestKit.Near(server, S.Players["gast"].Pos, 0.01f, "Server-Position weiterhin unverändert");
            Assert.True(S.Players["gast"].Pos.IsFinite, "Keine ungültigen Zahlen übernommen");
            // Normale Schritte werden akzeptiert
            int before = corr;
            var p0 = S.Players["gast"].Pos;
            for (int i = 1; i <= 10; i++)
            {
                var np = new V3(p0.x + 0.4f * i, p0.y, p0.z + 0.2f * i);
                g.SendInput(np, 0, false, 0, "grab", 1f / 15f);
                rig.PumpFor(1.0 / 60);
            }
            rig.PumpFor(0.1);
            Assert.Equal(before, corr, "Gültige Bewegung wird nicht korrigiert");
            TestKit.Near(new V3(p0.x + 4f, p0.y, p0.z + 2f), S.Players["gast"].Pos, 0.05f, "Server übernimmt gültige Bewegung");
        }
    }

    [Test]
    public static void Geschwindigkeitsbetrug_mit_gefaelschter_Zeitangabe_wird_korrigiert()
    {
        // Lokaler Gast: deterministische Reihenfolge der Nachrichten (kein TCP-Zeitverhalten)
        GameClient h;
        using (var rig = NetRig.HostWithLocalHost(out h, false))
        {
            var g = rig.Local("gast", "Gast");
            rig.PumpUntil(() => g.Joined, "Gast");
            int corr = 0;
            g.Corrected += p => corr++;
            var S = rig.Game.S;
            var start = new V3(0, Terrain.HeightAt("terra", 0, -80), -80);
            rig.Place("gast", start);
            // Ein Frame nach dem Versetzen, damit das Teleport-Recht verbraucht ist
            g.SendInput(start, 0, false, 0, "grab", 1f / 15f); rig.Step();
            // 30 Pakete mit 15/s (2 s Serverzeit), jedes behauptet dt = 1 s und springt 14 m weiter
            var pos = start;
            for (int i = 0; i < 30; i++)
            {
                pos = new V3(pos.x, pos.y, pos.z + 14f);
                if (pos.z > 40) pos = new V3(pos.x + 14f, pos.y, 40);
                g.SendInput(pos, 0, true, 0, "grab", 1.0f);
                rig.Dt = 1f / 15f;
                rig.Step();
            }
            rig.Dt = 0.02f;
            rig.PumpFor(0.05);
            float moved = V3.DistXZ(start, S.Players["gast"].Pos);
            Assert.True(corr > 0, "Betrug wird korrigiert");
            // Erlaubt: Sprint mit Toleranz über 2 s ≈ 10 m/s · 1,4 · 3 s (inkl. 1 s Guthaben) + Kulanz je Paket
            Assert.True(moved < 80f, "Server-Position folgt dem Betrug nicht (" + moved.ToString("0") + " m in 2 s)");
            // Ehrliche Bewegung mit Netzschwankungen (Pakete kommen gebündelt an) bleibt erlaubt
            int before = corr;
            var p0 = S.Players["gast"].Pos;
            var cur = p0;
            for (int burst = 0; burst < 10; burst++)
            {
                for (int k = 0; k < 3; k++)
                {
                    cur = new V3(cur.x, cur.y, cur.z - 6.5f / 15f);
                    g.SendInput(new V3(cur.x, Terrain.HeightAt("terra", cur.x, cur.z), cur.z), 0, false, 0, "grab", 1f / 15f);
                }
                rig.Dt = 3f / 15f; // Server sieht die drei Pakete erst nach 0,2 s gemeinsam
                rig.Step();
            }
            rig.Dt = 0.02f;
            Assert.Equal(before, corr, "Ehrliche Bewegung mit gebündelten Paketen wird nicht korrigiert");
        }
    }
}
