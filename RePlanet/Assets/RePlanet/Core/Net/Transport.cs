using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RePlanet.Core
{
    public enum NetEventType { Connect, Message, Disconnect }

    public struct NetEvent
    {
        public NetEventType Type;
        public int Conn;
        public string Data;
        public string Remote;
    }

    public interface IServerTransport
    {
        void Poll(List<NetEvent> into);
        void Send(int conn, string msg);
        void Close(int conn);
        void Stop();
    }

    public interface IClientTransport
    {
        bool Connected { get; }
        bool Failed { get; }
        string Error { get; }
        void Send(string msg);
        bool TryReceive(out string msg);
        void Close();
    }

    static class NetIds
    {
        static int next = 1;
        public static int Next() { return Interlocked.Increment(ref next); }
    }

    /// <summary>Längenpräfix-Rahmen (4 Byte Big-Endian + UTF-8).</summary>
    public static class Framing
    {
        public const int MaxFrame = 8 * 1024 * 1024;

        public static byte[] Encode(string msg)
        {
            var body = Encoding.UTF8.GetBytes(msg);
            var buf = new byte[body.Length + 4];
            buf[0] = (byte)(body.Length >> 24); buf[1] = (byte)(body.Length >> 16); buf[2] = (byte)(body.Length >> 8); buf[3] = (byte)body.Length;
            Buffer.BlockCopy(body, 0, buf, 4, body.Length);
            return buf;
        }

        static bool ReadExact(Stream s, byte[] buf, int len)
        {
            int off = 0;
            while (off < len)
            {
                int n = s.Read(buf, off, len - off);
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        public static string Read(Stream s)
        {
            var head = new byte[4];
            if (!ReadExact(s, head, 4)) return null;
            int len = (head[0] << 24) | (head[1] << 16) | (head[2] << 8) | head[3];
            if (len < 0 || len > MaxFrame) throw new IOException("Ungültige Nachrichtengröße " + len);
            var body = new byte[len];
            if (!ReadExact(s, body, len)) return null;
            return Encoding.UTF8.GetString(body);
        }
    }

    // ------------------------------------------------------------------ Lokal (Solo / Host im selben Prozess)
    public class LocalServerTransport : IServerTransport
    {
        readonly ConcurrentQueue<NetEvent> events = new ConcurrentQueue<NetEvent>();
        readonly Dictionary<int, LocalClientTransport> clients = new Dictionary<int, LocalClientTransport>();

        public LocalClientTransport CreateClient()
        {
            var c = new LocalClientTransport(this, NetIds.Next());
            lock (clients) clients[c.Id] = c;
            events.Enqueue(new NetEvent { Type = NetEventType.Connect, Conn = c.Id, Remote = "lokal" });
            return c;
        }

        internal void FromClient(int id, string msg) { events.Enqueue(new NetEvent { Type = NetEventType.Message, Conn = id, Data = msg }); }
        internal void ClientClosed(int id)
        {
            lock (clients) { if (!clients.Remove(id)) return; }
            events.Enqueue(new NetEvent { Type = NetEventType.Disconnect, Conn = id });
        }

        public void Poll(List<NetEvent> into) { NetEvent e; while (events.TryDequeue(out e)) into.Add(e); }

        public void Send(int conn, string msg)
        {
            LocalClientTransport c;
            lock (clients) clients.TryGetValue(conn, out c);
            if (c != null) c.Deliver(msg);
        }

        public void Close(int conn)
        {
            LocalClientTransport c;
            lock (clients) { clients.TryGetValue(conn, out c); clients.Remove(conn); }
            if (c != null) c.ServerClosed();
        }

        public bool Owns(int conn) { lock (clients) return clients.ContainsKey(conn); }

        public void Stop()
        {
            List<LocalClientTransport> all;
            lock (clients) { all = new List<LocalClientTransport>(clients.Values); clients.Clear(); }
            foreach (var c in all) c.ServerClosed();
        }
    }

    public class LocalClientTransport : IClientTransport
    {
        readonly LocalServerTransport server;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        public readonly int Id;
        volatile bool open = true;
        public LocalClientTransport(LocalServerTransport s, int id) { server = s; Id = id; }
        public bool Connected { get { return open; } }
        public bool Failed { get { return !open; } }
        public string Error { get; private set; }
        internal void Deliver(string msg) { inbox.Enqueue(msg); }
        internal void ServerClosed() { open = false; Error = "Lokale Sitzung beendet."; }
        public void Send(string msg) { if (open) server.FromClient(Id, msg); }
        public bool TryReceive(out string msg) { return inbox.TryDequeue(out msg); }
        public void Close() { if (!open) return; open = false; server.ClientClosed(Id); }
    }

    // ------------------------------------------------------------------ TCP
    public class TcpServerTransport : IServerTransport
    {
        class Conn
        {
            public int Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public BlockingCollection<byte[]> Outbox = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 4096);
            public volatile bool Closed;
            public string Remote;
        }

        TcpListener listener;
        Thread acceptThread;
        volatile bool running;
        readonly ConcurrentQueue<NetEvent> events = new ConcurrentQueue<NetEvent>();
        readonly Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
        public int Port { get; private set; }

        public bool Start(int port, out string error)
        {
            error = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                running = true;
                acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "RePlanet-Accept" };
                acceptThread.Start();
                return true;
            }
            catch (SocketException e)
            {
                error = e.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? "Port " + port + " ist bereits belegt. Anderen Port in den Einstellungen wählen."
                    : "Server konnte nicht gestartet werden: " + e.Message;
                return false;
            }
        }

        void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    client.NoDelay = true;
                    var c = new Conn { Id = NetIds.Next(), Client = client, Stream = client.GetStream(), Remote = client.Client.RemoteEndPoint.ToString() };
                    lock (conns) conns[c.Id] = c;
                    events.Enqueue(new NetEvent { Type = NetEventType.Connect, Conn = c.Id, Remote = c.Remote });
                    new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "RePlanet-Read-" + c.Id }.Start();
                    new Thread(() => WriteLoop(c)) { IsBackground = true, Name = "RePlanet-Write-" + c.Id }.Start();
                }
                catch (Exception)
                {
                    if (!running) return;
                    Thread.Sleep(50);
                }
            }
        }

        void ReadLoop(Conn c)
        {
            try
            {
                while (running && !c.Closed)
                {
                    var msg = Framing.Read(c.Stream);
                    if (msg == null) break;
                    events.Enqueue(new NetEvent { Type = NetEventType.Message, Conn = c.Id, Data = msg });
                }
            }
            catch (Exception) { }
            Drop(c);
        }

        void WriteLoop(Conn c)
        {
            try
            {
                foreach (var buf in c.Outbox.GetConsumingEnumerable())
                {
                    if (c.Closed) break;
                    c.Stream.Write(buf, 0, buf.Length);
                }
            }
            catch (Exception) { }
            Drop(c);
        }

        void Drop(Conn c)
        {
            bool first = false;
            lock (conns)
            {
                if (!c.Closed) { c.Closed = true; first = true; }
                conns.Remove(c.Id);
            }
            if (!first) return;
            try { c.Outbox.CompleteAdding(); } catch { }
            try { c.Client.Close(); } catch { }
            events.Enqueue(new NetEvent { Type = NetEventType.Disconnect, Conn = c.Id });
        }

        public void Poll(List<NetEvent> into) { NetEvent e; while (events.TryDequeue(out e)) into.Add(e); }

        public void Send(int conn, string msg)
        {
            Conn c;
            lock (conns) conns.TryGetValue(conn, out c);
            if (c == null || c.Closed) return;
            try
            {
                if (!c.Outbox.TryAdd(Framing.Encode(msg))) Drop(c); // Client hängt – trennen statt Speicher zu füllen
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>Sendet noch ausstehende Daten und trennt dann.</summary>
        public void Close(int conn)
        {
            Conn c;
            lock (conns) conns.TryGetValue(conn, out c);
            if (c == null) return;
            new Thread(() =>
            {
                for (int i = 0; i < 40 && c.Outbox.Count > 0 && !c.Closed; i++) Thread.Sleep(25);
                Drop(c);
            }) { IsBackground = true }.Start();
        }

        public bool Owns(int conn) { lock (conns) return conns.ContainsKey(conn); }

        public void Stop()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            List<Conn> all;
            lock (conns) all = new List<Conn>(conns.Values);
            foreach (var c in all) Drop(c);
        }
    }

    public class TcpClientTransport : IClientTransport
    {
        TcpClient client;
        NetworkStream stream;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly BlockingCollection<byte[]> outbox = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 4096);
        volatile bool connected, failed, closed;
        public bool Connected { get { return connected && !failed; } }
        public bool Failed { get { return failed; } }
        public string Error { get; private set; }

        /// <summary>Verbindet asynchron. Fehler landen verständlich formuliert in <see cref="Error"/>.</summary>
        public void Connect(string host, int port, int timeoutMs = 6000)
        {
            new Thread(() =>
            {
                try
                {
                    client = new TcpClient { NoDelay = true };
                    var ar = client.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        Fail("Keine Antwort von " + host + ":" + port + " (Zeitüberschreitung). Prüfe Adresse, Firewall und Portweiterleitung.");
                        try { client.Close(); } catch { }
                        return;
                    }
                    client.EndConnect(ar);
                    stream = client.GetStream();
                    connected = true;
                    new Thread(WriteLoop) { IsBackground = true, Name = "RePlanet-ClientWrite" }.Start();
                    while (!closed)
                    {
                        var msg = Framing.Read(stream);
                        if (msg == null) break;
                        inbox.Enqueue(msg);
                    }
                    if (!closed) Fail("Verbindung zum Host wurde getrennt.");
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.ConnectionRefused) Fail("Verbindung abgelehnt: Auf " + host + ":" + port + " läuft keine RE:PLANET-Sitzung.");
                    else if (e.SocketErrorCode == SocketError.HostNotFound || e.SocketErrorCode == SocketError.NoData) Fail("Adresse „" + host + "“ wurde nicht gefunden.");
                    else Fail("Netzwerkfehler: " + e.Message);
                }
                catch (Exception e)
                {
                    if (!closed) Fail("Verbindung verloren: " + e.Message);
                }
            }) { IsBackground = true, Name = "RePlanet-ClientRead" }.Start();
        }

        void WriteLoop()
        {
            try
            {
                foreach (var b in outbox.GetConsumingEnumerable())
                {
                    if (closed) break;
                    stream.Write(b, 0, b.Length);
                }
            }
            catch (Exception e) { if (!closed) Fail("Senden fehlgeschlagen: " + e.Message); }
        }

        void Fail(string msg)
        {
            if (failed) return;
            Error = msg;
            failed = true;
            connected = false;
        }

        public void Send(string msg)
        {
            if (closed || failed) return;
            try { outbox.TryAdd(Framing.Encode(msg)); } catch (InvalidOperationException) { }
        }

        public bool TryReceive(out string msg) { return inbox.TryDequeue(out msg); }

        public void Close()
        {
            if (closed) return;
            closed = true;
            connected = false;
            new Thread(() =>
            {
                for (int i = 0; i < 20 && outbox.Count > 0; i++) Thread.Sleep(25);
                try { outbox.CompleteAdding(); } catch { }
                try { client?.Close(); } catch { }
            }) { IsBackground = true }.Start();
        }
    }
}
