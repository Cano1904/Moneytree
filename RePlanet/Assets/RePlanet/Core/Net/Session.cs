using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Eine Koop-Sitzung (1–4 Spieler). Serverautoritativ: Nur hier wird die Welt verändert.
    /// Läuft identisch im Solo-Spiel (lokale Verbindung), beim Host (lokal + TCP) und auf dem dedizierten Server.
    /// </summary>
    public class Session
    {
        public const int ProtocolVersion = 1;
        public const float ReconnectWindow = 120f;
        public const float HostGrace = 30f;

        public readonly string Code;
        public readonly Game Game;
        public readonly bool Dedicated;
        public string HostPid;
        public bool Closed { get; private set; }
        public string CloseReason { get; private set; }

        readonly Action<int, string> send;
        readonly Action<int> close;
        readonly Dictionary<int, string> connPid = new Dictionary<int, string>();
        readonly Dictionary<string, int> pidConn = new Dictionary<string, int>();
        readonly Dictionary<string, string> tokens = new Dictionary<string, string>();
        readonly Dictionary<string, RidCache> rids = new Dictionary<string, RidCache>();
        readonly Dictionary<int, RateLimit> rates = new Dictionary<int, RateLimit>();
        readonly Dictionary<string, double> lastInput = new Dictionary<string, double>();
        double clock;
        float patchTimer, posTimer;
        double hostLostAt = -1;

        /// <summary>Server-seitige Sicherung (dedizierter Server): Text + Anlass.</summary>
        public Action<string, string> OnServerSave;
        public Action<string> Log;

        class RidCache
        {
            readonly Dictionary<string, string> map = new Dictionary<string, string>();
            readonly Queue<string> order = new Queue<string>();
            public bool TryGet(string rid, out string res) { return map.TryGetValue(rid, out res); }
            public void Put(string rid, string res)
            {
                if (map.ContainsKey(rid)) return;
                map[rid] = res; order.Enqueue(rid);
                while (order.Count > 512) map.Remove(order.Dequeue());
            }
        }

        class RateLimit { public double Window; public int Count; }

        public Session(string code, WorldState world, bool dedicated, Action<int, string> send, Action<int> close)
        {
            Code = code;
            Dedicated = dedicated;
            this.send = send;
            this.close = close;
            Game = new Game(world);
        }

        public int OnlineCount
        {
            get { int n = 0; foreach (var p in Game.S.Players.Values) if (p.Online) n++; return n; }
        }

        public bool HasConn(int conn) { return connPid.ContainsKey(conn); }

        public List<string> OnlineNames()
        {
            var l = new List<string>();
            foreach (var p in Game.S.Players.Values) if (p.Online) l.Add(p.Name + (p.Id == HostPid ? " (Host)" : ""));
            return l;
        }

        void Send(int conn, JObj o) { send(conn, Json.Write(o)); }

        void Broadcast(string text, int except = -1)
        {
            foreach (var c in new List<int>(connPid.Keys)) if (c != except) send(c, text);
        }

        void Reject(int conn, string msg)
        {
            Send(conn, new JObj().Set("t", "err").Set("msg", msg).Set("fatal", true));
            close(conn);
        }

        /// <summary>Anmeldung eines Clients. Gibt null zurück, wenn der Beitritt geklappt hat.</summary>
        public string Join(int conn, JObj hello)
        {
            if (Closed) { Reject(conn, "Die Sitzung wurde beendet."); return "closed"; }
            if (hello.Int("v") != ProtocolVersion) { Reject(conn, "Spielversion passt nicht zum Host (Protokoll " + hello.Int("v") + " statt " + ProtocolVersion + ")."); return "version"; }
            string pid = hello.Str("id");
            if (string.IsNullOrEmpty(pid) || pid.Length > 64) { Reject(conn, "Ungültiges Spielerprofil."); return "profile"; }
            string name = (hello.Str("name") ?? "MIKO").Trim();
            if (name.Length == 0) name = "MIKO";
            int existing;
            if (pidConn.TryGetValue(pid, out existing))
            {
                string tok;
                if (tokens.TryGetValue(pid, out tok) && hello.Str("token") == tok)
                {
                    // Wiederverbinden: alte Verbindung ersetzen
                    connPid.Remove(existing);
                    pidConn.Remove(pid);
                    close(existing);
                }
                else { Reject(conn, "Dieses Spielerprofil ist bereits in der Sitzung."); return "dup"; }
            }
            if (OnlineCount >= GameData.MaxPlayers && !(Game.S.Players.ContainsKey(pid) && Game.S.Players[pid].Online))
            { Reject(conn, "Die Sitzung ist voll (" + GameData.MaxPlayers + "/" + GameData.MaxPlayers + ")."); return "full"; }

            if (HostPid == null) HostPid = pid;
            bool isHost = pid == HostPid;
            string token;
            if (!tokens.TryGetValue(pid, out token)) { token = Ids.Token(); tokens[pid] = token; }
            connPid[conn] = pid;
            pidConn[pid] = conn;
            if (!rids.ContainsKey(pid)) rids[pid] = new RidCache();
            if (isHost) hostLostAt = -1;

            var p = Game.Join(pid, name);
            var cos = hello.Obj("cos");
            if (cos != null)
            {
                var ca = new JObj().Set("a", "cosm").Set("personal", true);
                foreach (var k in new[] { "color", "accent", "sticker", "attach" }) if (cos.Str(k) != null) ca[k] = cos.Str(k);
                Game.Apply(pid, ca, isHost);
            }
            // Vollständiger Zustand für spätes Beitreten
            var welcome = new JObj()
                .Set("t", "welcome")
                .Set("pid", pid)
                .Set("host", isHost)
                .Set("hostPid", HostPid)
                .Set("token", token)
                .Set("code", Code)
                .Set("dedicated", Dedicated)
                .Set("snapshot", Game.S.ToJson(false));
            Send(conn, welcome);
            Log?.Invoke(name + " ist beigetreten (" + OnlineCount + "/" + GameData.MaxPlayers + ").");
            return null;
        }

        public void Handle(int conn, string text)
        {
            string pid;
            if (!connPid.TryGetValue(conn, out pid)) return;
            // Ratenbegrenzung: max. 150 Nachrichten pro Sekunde
            RateLimit rl;
            if (!rates.TryGetValue(conn, out rl)) { rl = new RateLimit(); rates[conn] = rl; }
            if (clock - rl.Window > 1.0) { rl.Window = clock; rl.Count = 0; }
            if (++rl.Count > 150) return;

            JObj m;
            if (!Json.TryParseObj(text, out m)) return;
            bool isHost = pid == HostPid;
            switch (m.Str("t"))
            {
                case "in":
                    {
                        double last;
                        float dt = lastInput.TryGetValue(pid, out last) ? (float)(clock - last) : 0.1f;
                        lastInput[pid] = clock;
                        var pos = V3.FromArr(m.Floats("p"));
                        if (!Game.Move(pid, pos, m.Float("y"), m.Bool("s"), m.Int("f"), m.Str("tool"), Math.Max(dt, m.Float("dt", 0.066f))))
                        {
                            PlayerData p;
                            if (Game.S.Players.TryGetValue(pid, out p))
                                Send(conn, new JObj().Set("t", "corr").Set("p", p.Pos.ToJson()));
                        }
                        break;
                    }
                case "act":
                    {
                        string rid = m.Str("rid");
                        var act = m.Obj("a");
                        if (rid == null || act == null) return;
                        string cached;
                        if (rids[pid].TryGet(rid, out cached)) { send(conn, cached); return; } // doppelt gesendet → keine erneute Verarbeitung
                        var res = Game.Apply(pid, act, isHost);
                        var ro = res.ToJson().Set("t", "res").Set("rid", rid);
                        string rs = Json.Write(ro);
                        rids[pid].Put(rid, rs);
                        send(conn, rs);
                        if (act.Str("a") == "travel" && res.Ok) FlushSave("Planetenwechsel");
                        break;
                    }
                case "reqsave":
                    if (isHost) FlushSave(m.Str("reason", "manuell"));
                    break;
                case "emote":
                    {
                        string e = m.Str("e");
                        if (e == null || e.Length > 16) return;
                        Broadcast(Json.Write(new JObj().Set("t", "emote").Set("pid", pid).Set("e", e)));
                        break;
                    }
                case "ping":
                    Send(conn, new JObj().Set("t", "pong").Set("ts", m.Num("ts")));
                    break;
                case "leave":
                    Leave(conn, pid, true);
                    break;
            }
        }

        void Leave(int conn, string pid, bool graceful)
        {
            if (pid == HostPid)
            {
                if (graceful || !Dedicated)
                {
                    FlushSave("Host verlässt die Sitzung");
                    End("Der Host hat die Sitzung beendet. Der Spielstand wurde beim Host gesichert.");
                }
                else
                {
                    // Host-Verbindung abgerissen: kurz warten, ob er zurückkommt
                    Game.Leave(pid);
                    connPid.Remove(conn); pidConn.Remove(pid);
                    hostLostAt = clock;
                    ServerSave("Host-Verbindung verloren");
                    Broadcast(Json.Write(new JObj().Set("t", "notice").Set("msg", "Verbindung zum Host unterbrochen – warte bis zu " + (int)HostGrace + " Sekunden …")));
                }
                return;
            }
            Game.Leave(pid);
            connPid.Remove(conn);
            pidConn.Remove(pid);
            rates.Remove(conn);
            if (graceful) close(conn);
            Log?.Invoke("Spieler " + pid + " hat die Sitzung verlassen.");
        }

        public void Disconnect(int conn)
        {
            string pid;
            if (!connPid.TryGetValue(conn, out pid)) return;
            Leave(conn, pid, false);
        }

        void ServerSave(string reason)
        {
            if (OnServerSave != null) OnServerSave(SaveCodec.Encode(Game.S), reason);
        }

        /// <summary>Schickt den aktuellen Stand an den Host (der ihn lokal speichert).</summary>
        public void FlushSave(string reason)
        {
            Game.SaveReason = null;
            string text = SaveCodec.Encode(Game.S);
            int hc;
            if (HostPid != null && pidConn.TryGetValue(HostPid, out hc))
                Send(hc, new JObj().Set("t", "save").Set("reason", reason).Set("data", text));
            if (OnServerSave != null) OnServerSave(text, reason);
        }

        /// <summary>Beendet die Sitzung für alle (Gäste erhalten eine verständliche Meldung).</summary>
        public void End(string reason)
        {
            if (Closed) return;
            Closed = true;
            CloseReason = reason;
            foreach (var kv in new List<KeyValuePair<int, string>>(connPid))
            {
                if (kv.Value != HostPid) Send(kv.Key, new JObj().Set("t", "hostleft").Set("msg", reason));
                else Send(kv.Key, new JObj().Set("t", "ended").Set("msg", reason));
                close(kv.Key);
            }
            connPid.Clear(); pidConn.Clear();
        }

        public void Update(float dt)
        {
            if (Closed) return;
            clock += dt;
            Game.Tick(dt);
            patchTimer += dt;
            posTimer += dt;
            if (patchTimer >= 1f / 30f)
            {
                patchTimer = 0;
                var patch = Game.BuildPatch();
                if (patch != null)
                {
                    patch["t"] = "patch";
                    Broadcast(Json.Write(patch));
                }
            }
            if (posTimer >= 1f / 15f)
            {
                posTimer = 0;
                var pos = Game.PosPacket();
                pos["t"] = "pos";
                Broadcast(Json.Write(pos));
            }
            if (Game.SaveReason != null) FlushSave(Game.SaveReason);
            if (hostLostAt >= 0 && clock - hostLostAt > HostGrace)
            {
                ServerSave("Host nicht zurückgekehrt");
                End("Der Host ist nicht zurückgekehrt. Die Welt wurde gesichert.");
            }
        }
    }

    /// <summary>
    /// Verteilt Verbindungen auf Sitzungen. Im Spiel (Host) gibt es genau eine Sitzung,
    /// auf dem dedizierten Server beliebig viele (per Sitzungscode).
    /// </summary>
    public class SessionHub
    {
        readonly List<IServerTransport> transports = new List<IServerTransport>();
        readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
        readonly Dictionary<int, Session> connSession = new Dictionary<int, Session>();
        readonly Dictionary<int, IServerTransport> connTransport = new Dictionary<int, IServerTransport>();
        readonly Dictionary<int, double> pendingSince = new Dictionary<int, double>();
        readonly List<NetEvent> events = new List<NetEvent>();
        double clock;
        public readonly bool AllowCreate;
        public Action<string> Log;
        public Func<string, WorldState, string, Session> SessionFactory;
        public Action<Session, string, string> OnServerSave;

        public SessionHub(bool allowCreate) { AllowCreate = allowCreate; }

        public void AddTransport(IServerTransport t) { transports.Add(t); }
        public void RemoveTransport(IServerTransport t) { transports.Remove(t); }
        public IEnumerable<Session> Sessions { get { return sessions.Values; } }

        public Session CreateSession(WorldState w, string code = null)
        {
            code = code ?? NewCode();
            var s = new Session(code, w, AllowCreate, SendTo, CloseConn);
            s.Log = Log;
            if (OnServerSave != null) s.OnServerSave = (text, reason) => OnServerSave(s, text, reason);
            sessions[code] = s;
            return s;
        }

        string NewCode()
        {
            string c;
            do c = Ids.Code(6); while (sessions.ContainsKey(c));
            return c;
        }

        void SendTo(int conn, string msg)
        {
            IServerTransport t;
            if (connTransport.TryGetValue(conn, out t)) t.Send(conn, msg);
        }

        void CloseConn(int conn)
        {
            IServerTransport t;
            if (connTransport.TryGetValue(conn, out t)) t.Close(conn);
            connSession.Remove(conn);
        }

        public void Update(float dt)
        {
            clock += dt;
            foreach (var t in transports)
            {
                events.Clear();
                t.Poll(events);
                foreach (var e in events)
                {
                    switch (e.Type)
                    {
                        case NetEventType.Connect:
                            connTransport[e.Conn] = t;
                            pendingSince[e.Conn] = clock;
                            break;
                        case NetEventType.Message:
                            Session s;
                            if (connSession.TryGetValue(e.Conn, out s)) s.Handle(e.Conn, e.Data);
                            else HandleHello(e.Conn, e.Data);
                            break;
                        case NetEventType.Disconnect:
                            Session ds;
                            if (connSession.TryGetValue(e.Conn, out ds)) ds.Disconnect(e.Conn);
                            connSession.Remove(e.Conn);
                            connTransport.Remove(e.Conn);
                            pendingSince.Remove(e.Conn);
                            break;
                    }
                }
            }
            // Verbindungen ohne Anmeldung nach 15 s trennen
            foreach (var kv in new List<KeyValuePair<int, double>>(pendingSince))
                if (clock - kv.Value > 15) { pendingSince.Remove(kv.Key); CloseConn(kv.Key); }
            foreach (var s in new List<Session>(sessions.Values))
            {
                s.Update(dt);
                if (s.Closed) { sessions.Remove(s.Code); Log?.Invoke("Sitzung " + s.Code + " beendet: " + s.CloseReason); }
            }
        }

        void HandleHello(int conn, string text)
        {
            JObj m;
            if (!Json.TryParseObj(text, out m) || m.Str("t") != "hello")
            {
                SendTo(conn, Json.Write(new JObj().Set("t", "err").Set("msg", "Unbekanntes Protokoll.").Set("fatal", true)));
                CloseConn(conn);
                return;
            }
            pendingSince.Remove(conn);
            Session s = null;
            if (m.Bool("create"))
            {
                if (!AllowCreate)
                {
                    SendTo(conn, Json.Write(new JObj().Set("t", "err").Set("msg", "Dieser Host erstellt keine neuen Sitzungen.").Set("fatal", true)));
                    CloseConn(conn); return;
                }
                WorldState w = null;
                string save = m.Str("save");
                if (!string.IsNullOrEmpty(save))
                {
                    string err;
                    w = SaveCodec.Decode(save, out err);
                    if (w == null)
                    {
                        SendTo(conn, Json.Write(new JObj().Set("t", "err").Set("msg", "Spielstand wurde abgelehnt: " + err).Set("fatal", true)));
                        CloseConn(conn); return;
                    }
                }
                s = CreateSession(w ?? Game.NewWorld(m.Str("world", "Koop-Welt")));
                s.HostPid = m.Str("id");
            }
            else
            {
                string code = (m.Str("code") ?? "").Trim().ToUpperInvariant();
                if (!sessions.TryGetValue(code, out s))
                {
                    // Im Spiel-Host gibt es nur eine Sitzung – lokale Verbindung ohne Code darf beitreten
                    if (!AllowCreate && sessions.Count == 1 && string.IsNullOrEmpty(code) && m.Bool("local"))
                        foreach (var only in sessions.Values) s = only;
                    if (s == null)
                    {
                        SendTo(conn, Json.Write(new JObj().Set("t", "err").Set("msg", "Sitzung „" + code + "“ wurde nicht gefunden. Code prüfen.").Set("fatal", true)));
                        CloseConn(conn); return;
                    }
                }
            }
            if (s.Join(conn, m) == null) connSession[conn] = s;
        }

        public void Shutdown(string reason)
        {
            foreach (var s in new List<Session>(sessions.Values)) s.End(reason);
            Update(0);
            foreach (var t in transports) t.Stop();
        }
    }
}
