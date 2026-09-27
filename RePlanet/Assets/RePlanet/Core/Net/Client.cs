using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Puffer für flüssige Darstellung entfernter Objekte (Interpolation mit ~120 ms Verzögerung).</summary>
    public class Interp
    {
        struct Snap { public double T; public V3 P; public float Yaw; public int Flags; }
        readonly List<Snap> buf = new List<Snap>();
        public void Push(double t, V3 p, float yaw, int flags)
        {
            if (buf.Count > 0 && t <= buf[buf.Count - 1].T) buf.Clear();
            buf.Add(new Snap { T = t, P = p, Yaw = yaw, Flags = flags });
            if (buf.Count > 20) buf.RemoveAt(0);
        }
        public bool Sample(double t, out V3 p, out float yaw, out int flags)
        {
            p = V3.Zero; yaw = 0; flags = 0;
            if (buf.Count == 0) return false;
            if (t <= buf[0].T || buf.Count == 1) { p = buf[0].P; yaw = buf[0].Yaw; flags = buf[0].Flags; return true; }
            for (int i = 1; i < buf.Count; i++)
            {
                if (buf[i].T >= t)
                {
                    var a = buf[i - 1]; var b = buf[i];
                    float k = (float)((t - a.T) / Math.Max(1e-4, b.T - a.T));
                    p = a.P + (b.P - a.P) * k;
                    yaw = a.Yaw + M.WrapAngle(b.Yaw - a.Yaw) * k;
                    flags = b.Flags;
                    return true;
                }
            }
            var last = buf[buf.Count - 1];
            p = last.P; yaw = last.Yaw; flags = last.Flags;
            return true;
        }
    }

    /// <summary>
    /// Client-Seite: hält eine Kopie des Weltzustands, wendet Patches an und schickt Eingaben/Aktionen.
    /// Wird von Unity (Darstellung) und von den automatisierten Tests benutzt.
    /// </summary>
    public class GameClient
    {
        public IClientTransport T;
        public WorldState W;
        public string Pid, HostPid, Token, Code;
        public bool IsHost, Joined, Dedicated;
        public string FatalError;
        public bool Ended;
        public double ServerTime;
        public float PingMs;
        public readonly Dictionary<string, Interp> Players = new Dictionary<string, Interp>();
        public readonly Dictionary<string, Interp> Vehicles = new Dictionary<string, Interp>();
        public readonly List<float[]> Drones = new List<float[]>();
        readonly Dictionary<string, Action<ActResult>> pending = new Dictionary<string, Action<ActResult>>();
        readonly List<JObj> unsentActs = new List<JObj>();
        int ridCounter;
        readonly string ridPrefix = Ids.Code(4);
        double localClock, lastPing;
        double serverTimeAt;

        public event Action<JObj> Fx;
        public event Action<string, string> SaveReceived;
        public event Action<V3> Corrected;
        public event Action<string> Notice;
        public event Action<string> PlanetChanged;
        public event Action<string, string> Emote;
        public event Action Welcomed;
        public event Action<ActResult, JObj> ActionFailed;

        public GameClient(IClientTransport t) { T = t; }

        public void Hello(string pid, string name, string code, JObj cosmetics, string token = null, bool local = false, bool create = false, string saveText = null)
        {
            Pid = pid;
            var h = new JObj().Set("t", "hello").Set("v", Session.ProtocolVersion).Set("id", pid).Set("name", name).Set("code", code ?? "").Set("local", local);
            if (token != null) h["token"] = token;
            if (cosmetics != null) h["cos"] = cosmetics;
            if (create) { h["create"] = true; if (saveText != null) h["save"] = saveText; }
            helloMsg = Json.Write(h);
        }
        string helloMsg;
        bool helloSent;

        public PlayerData Me { get { PlayerData p; return W != null && Pid != null && W.Players.TryGetValue(Pid, out p) ? p : null; } }

        /// <summary>Geschätzte aktuelle Serverzeit (für Interpolation).</summary>
        public double RenderTime { get { return ServerTime + (localClock - serverTimeAt) - 0.12; } }

        public void Update(float dt)
        {
            localClock += dt;
            if (!helloSent && helloMsg != null && T.Connected) { T.Send(helloMsg); helloSent = true; }
            string msg;
            int guard = 0;
            while (T.TryReceive(out msg) && guard++ < 2000) Handle(msg);
            if (T.Failed && !Ended && FatalError == null) FatalError = T.Error ?? "Verbindung verloren.";
            if (Joined && localClock - lastPing > 2.0)
            {
                lastPing = localClock;
                T.Send(Json.Write(new JObj().Set("t", "ping").Set("ts", localClock)));
            }
        }

        void Handle(string text)
        {
            JObj m;
            if (!Json.TryParseObj(text, out m)) return;
            switch (m.Str("t"))
            {
                case "welcome":
                    Pid = m.Str("pid");
                    IsHost = m.Bool("host");
                    HostPid = m.Str("hostPid");
                    Token = m.Str("token");
                    Code = m.Str("code");
                    Dedicated = m.Bool("dedicated");
                    W = WorldState.FromJson(m.Obj("snapshot"));
                    ServerTime = W.PlayTime; serverTimeAt = localClock;
                    Joined = true;
                    foreach (var a in unsentActs) T.Send(Json.Write(a));
                    unsentActs.Clear();
                    Welcomed?.Invoke();
                    PlanetChanged?.Invoke(W.CurrentPlanet);
                    break;
                case "patch": if (W != null) ApplyPatch(m); break;
                case "pos": if (W != null) ApplyPos(m); break;
                case "res":
                    {
                        string rid = m.Str("rid");
                        var r = new ActResult { Ok = m.Bool("ok"), Err = m.Str("err"), Data = m.Obj("data") };
                        Action<ActResult> cb;
                        if (rid != null && pending.TryGetValue(rid, out cb))
                        {
                            pending.Remove(rid);
                            cb?.Invoke(r);
                        }
                        break;
                    }
                case "corr":
                    Corrected?.Invoke(V3.FromArr(m.Floats("p")));
                    break;
                case "save":
                    SaveReceived?.Invoke(m.Str("data"), m.Str("reason"));
                    break;
                case "notice":
                    Notice?.Invoke(m.Str("msg"));
                    break;
                case "emote":
                    Emote?.Invoke(m.Str("pid"), m.Str("e"));
                    break;
                case "pong":
                    PingMs = (float)((localClock - m.Num("ts")) * 1000.0);
                    break;
                case "hostleft":
                    Ended = true;
                    FatalError = m.Str("msg") ?? "Der Host hat die Sitzung beendet.";
                    break;
                case "ended":
                    Ended = true;
                    break;
                case "err":
                    if (m.Bool("fatal")) { FatalError = m.Str("msg"); Ended = true; }
                    else Notice?.Invoke(m.Str("msg"));
                    break;
            }
        }

        public void ApplyPatch(JObj o)
        {
            if (o.Has("planetFull"))
            {
                var ps = PlanetState.FromJson(o.Obj("planetFull"));
                W.Planets[ps.Id] = ps;
                W.CurrentPlanet = o.Str("planet", ps.Id);
                Vehicles.Clear();
                Drones.Clear();
                PlanetChanged?.Invoke(W.CurrentPlanet);
            }
            var w = o.Obj("w");
            if (w != null) foreach (var kv in w) W.PartFromJson(kv.Key, kv.Value);
            string pid = o.Str("pid", W.CurrentPlanet);
            var planet = W.Planet(pid);
            var p = o.Obj("p");
            if (p != null) foreach (var kv in p) planet.PartFromJson(kv.Key, kv.Value);
            var du = o.Arr("dynU");
            if (du != null) foreach (var d in du) { var dy = DynObj.FromJson(d as JObj); if (dy != null) planet.Dyn[dy.Id] = dy; }
            var dr = o.Arr("dynR");
            if (dr != null) foreach (var d in dr) if (d is string) planet.Dyn.Remove((string)d);
            var pl = o.Obj("pl");
            if (pl != null)
                foreach (var kv in pl)
                {
                    var pd = PlayerData.FromJson(kv.Value as JObj);
                    if (pd == null) continue;
                    PlayerData old;
                    if (W.Players.TryGetValue(kv.Key, out old) && kv.Key == Pid)
                    {
                        // eigene Position bleibt lokal (Vorhersage) – der Server korrigiert per "corr"
                        pd.Pos = old.Pos; pd.Yaw = old.Yaw;
                    }
                    W.Players[kv.Key] = pd;
                }
            var fx = o.Arr("fx");
            if (fx != null) foreach (var f in fx) { var fo = f as JObj; if (fo != null) Fx?.Invoke(fo); }
        }

        void ApplyPos(JObj m)
        {
            ServerTime = m.Num("t");
            serverTimeAt = localClock;
            W.PlayTime = ServerTime;
            var p = m.Obj("p");
            if (p != null)
                foreach (var kv in p)
                {
                    var a = kv.Value as List<object>;
                    if (a == null || a.Count < 5) continue;
                    Interp ip;
                    if (!Players.TryGetValue(kv.Key, out ip)) { ip = new Interp(); Players[kv.Key] = ip; }
                    var pos = new V3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0));
                    ip.Push(ServerTime, pos, (float)Json.ToDouble(a[3], 0), (int)Json.ToDouble(a[4], 0));
                    PlayerData pd;
                    if (kv.Key != Pid && W.Players.TryGetValue(kv.Key, out pd)) { pd.Pos = pos; pd.Yaw = (float)Json.ToDouble(a[3], 0); }
                }
            var v = m.Obj("v");
            if (v != null)
                foreach (var kv in v)
                {
                    var a = kv.Value as List<object>;
                    if (a == null || a.Count < 4) continue;
                    Interp ip;
                    if (!Vehicles.TryGetValue(kv.Key, out ip)) { ip = new Interp(); Vehicles[kv.Key] = ip; }
                    var pos = new V3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0));
                    ip.Push(ServerTime, pos, (float)Json.ToDouble(a[3], 0), 0);
                    VehicleState vs;
                    if (W.Cur.Vehicles.TryGetValue(kv.Key, out vs) && vs.Driver != Pid) { vs.Pos = pos; vs.Yaw = (float)Json.ToDouble(a[3], 0); }
                }
            Drones.Clear();
            var d = m.Arr("d");
            if (d != null)
                foreach (var o in d)
                {
                    var a = o as List<object>;
                    if (a == null || a.Count < 4) continue;
                    Drones.Add(new[] { (float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0), (float)Json.ToDouble(a[3], 0) });
                }
        }

        /// <summary>Sendet eine Aktion mit eindeutiger Anfrage-ID (verhindert Doppelverarbeitung bei Wiederholung).</summary>
        public string Act(JObj a, Action<ActResult> cb = null)
        {
            string rid = ridPrefix + (++ridCounter);
            pending[rid] = r =>
            {
                if (!r.Ok && r.Err != null) ActionFailed?.Invoke(r, a);
                cb?.Invoke(r);
            };
            var msg = new JObj().Set("t", "act").Set("rid", rid).Set("a", a);
            if (Joined) T.Send(Json.Write(msg)); else unsentActs.Add(msg);
            return rid;
        }

        /// <summary>Nur für Tests: dieselbe Anfrage erneut senden.</summary>
        public void Resend(string rid, JObj a) { T.Send(Json.Write(new JObj().Set("t", "act").Set("rid", rid).Set("a", a))); }

        public int PendingCount { get { return pending.Count; } }

        public void SendInput(V3 p, float yaw, bool sprint, int flags, string tool, float dt)
        {
            if (!Joined) return;
            var me = Me;
            if (me != null) { me.Pos = p; me.Yaw = yaw; }
            T.Send(Json.Write(new JObj().Set("t", "in").Set("p", p.ToJson(3)).Set("y", Json.R(yaw, 3)).Set("s", sprint).Set("f", flags).Set("tool", tool).Set("dt", Json.R(dt, 3))));
        }

        public void RequestSave(string reason) { T.Send(Json.Write(new JObj().Set("t", "reqsave").Set("reason", reason))); }
        public void SendEmote(string e) { T.Send(Json.Write(new JObj().Set("t", "emote").Set("e", e))); }

        public void Leave()
        {
            if (Joined) T.Send(Json.Write(new JObj().Set("t", "leave")));
            Ended = true;
        }
    }

    /// <summary>
    /// Host im Spielprozess: Solo = nur lokale Verbindung; Koop = zusätzlich TCP-Port für Gäste.
    /// </summary>
    public class HostServer
    {
        public readonly SessionHub Hub;
        public readonly Session Session;
        readonly LocalServerTransport local = new LocalServerTransport();
        TcpServerTransport tcp;
        public int Port { get { return tcp != null ? tcp.Port : 0; } }
        public bool Online { get { return tcp != null; } }

        public HostServer(WorldState world, string hostPid)
        {
            Hub = new SessionHub(false);
            Hub.AddTransport(local);
            Session = Hub.CreateSession(world);
            Session.HostPid = hostPid;
        }

        public LocalClientTransport ConnectLocal() { return local.CreateClient(); }

        public bool OpenOnline(int port, out string error)
        {
            error = null;
            if (tcp != null) return true;
            var t = new TcpServerTransport();
            if (!t.Start(port, out error)) return false;
            tcp = t;
            Hub.AddTransport(t);
            return true;
        }

        public void CloseOnline()
        {
            if (tcp == null) return;
            Hub.RemoveTransport(tcp);
            tcp.Stop();
            tcp = null;
        }

        public void Update(float dt, bool simulate = true) { Hub.Update(dt, simulate); }

        public void Shutdown(string reason)
        {
            Hub.Shutdown(reason);
            tcp = null;
        }

        /// <summary>Einladungstext: Adresse, Port und Sitzungscode.</summary>
        public string InviteText(string address) { return address + ":" + Port + "/" + Session.Code; }

        public static bool ParseInvite(string text, out string host, out int port, out string code)
        {
            host = null; port = 7777; code = null;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.StartsWith("replanet://")) text = text.Substring(11);
            string addr = text;
            int slash = text.IndexOf('/');
            if (slash >= 0) { addr = text.Substring(0, slash); code = text.Substring(slash + 1).Trim().ToUpperInvariant(); }
            int colon = addr.LastIndexOf(':');
            if (colon > 0)
            {
                int p;
                if (!int.TryParse(addr.Substring(colon + 1), out p) || p <= 0 || p > 65535) return false;
                port = p;
                addr = addr.Substring(0, colon);
            }
            host = addr.Trim();
            return host.Length > 0;
        }

        public static List<string> LocalAddresses()
        {
            var l = new List<string>();
            try
            {
                foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip)) l.Add(ip.ToString());
            }
            catch { }
            if (l.Count == 0) l.Add("127.0.0.1");
            return l;
        }
    }
}
