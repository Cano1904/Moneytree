using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Glasscore.Net
{
    public enum TransportEventType : byte
    {
        Connected,
        Disconnected,
        Reliable,
        Unreliable,
    }

    public struct TransportEvent
    {
        public TransportEventType Type;
        public int ConnectionId;
        public byte[] Payload;
    }

    /// <summary>
    /// Dual-channel transport: TCP carries reliable, ordered messages (lobby, chat, RPC_ShatterTile,
    /// tile deltas); UDP carries latency-critical unreliable traffic (inputs, snapshots, ping, voice).
    /// Every UDP datagram starts with [u16 magic][u32 token]; the token issued in Welcome binds a
    /// client's UDP endpoint to its TCP connection, which also survives NAT port rebinding.
    /// All socket I/O runs on background threads; the owner drains <see cref="Poll"/> on its own thread.
    /// </summary>
    public sealed class ServerTransport : IDisposable
    {
        private sealed class Connection
        {
            public int Id;
            public TcpClient Tcp;
            public NetworkStream Stream;
            public uint Token;
            public IPEndPoint UdpEndpoint;
            public readonly object WriteLock = new object();
            public volatile bool Closed;
        }

        private readonly ConcurrentQueue<TransportEvent> _events = new ConcurrentQueue<TransportEvent>();
        private readonly ConcurrentDictionary<int, Connection> _connections = new ConcurrentDictionary<int, Connection>();
        private readonly ConcurrentDictionary<uint, int> _tokenToConnection = new ConcurrentDictionary<uint, int>();
        private readonly Random _random = new Random();
        private TcpListener _listener;
        private UdpClient _udp;
        private volatile bool _running;
        private int _nextId;

        public int Port { get; private set; }

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
            IgnoreUdpConnectionReset(_udp);
            _running = true;
            new Thread(AcceptLoop) { IsBackground = true, Name = "GC-Accept" }.Start();
            new Thread(UdpLoop) { IsBackground = true, Name = "GC-UdpServer" }.Start();
        }

        public bool Poll(out TransportEvent ev) => _events.TryDequeue(out ev);

        public uint GetToken(int connectionId) => _connections.TryGetValue(connectionId, out var c) ? c.Token : 0;

        public string GetAddress(int connectionId)
        {
            try { return _connections.TryGetValue(connectionId, out var c) ? c.Tcp.Client.RemoteEndPoint?.ToString() : null; }
            catch (ObjectDisposedException) { return null; }
        }

        public void SendReliable(int connectionId, NetWriter w)
        {
            if (!_connections.TryGetValue(connectionId, out var c) || c.Closed) return;
            try
            {
                lock (c.WriteLock) WriteFrame(c.Stream, w.Data, w.Length);
            }
            catch (Exception)
            {
                Close(c);
            }
        }

        public void SendUnreliable(int connectionId, NetWriter w)
        {
            if (!_connections.TryGetValue(connectionId, out var c) || c.UdpEndpoint == null || c.Closed) return;
            try { _udp.Send(w.Data, w.Length, c.UdpEndpoint); }
            catch (Exception) { /* UDP is best effort */ }
        }

        public void Disconnect(int connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var c)) Close(c);
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch (Exception) { if (!_running) return; continue; }

                client.NoDelay = true;
                var c = new Connection { Id = Interlocked.Increment(ref _nextId), Tcp = client, Stream = client.GetStream() };
                lock (_random)
                {
                    do { c.Token = (uint)_random.Next(1, int.MaxValue) ^ ((uint)_random.Next() << 1); }
                    while (c.Token == 0 || _tokenToConnection.ContainsKey(c.Token));
                }
                _connections[c.Id] = c;
                _tokenToConnection[c.Token] = c.Id;
                _events.Enqueue(new TransportEvent { Type = TransportEventType.Connected, ConnectionId = c.Id });
                new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "GC-Conn" + c.Id }.Start();
            }
        }

        private void ReadLoop(Connection c)
        {
            try
            {
                while (_running && !c.Closed)
                {
                    byte[] frame = ReadFrame(c.Stream);
                    if (frame == null) break;
                    _events.Enqueue(new TransportEvent { Type = TransportEventType.Reliable, ConnectionId = c.Id, Payload = frame });
                }
            }
            catch (Exception) { /* connection dropped */ }
            Close(c);
        }

        private void UdpLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                byte[] data;
                try { data = _udp.Receive(ref any); }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }

                if (data.Length < 7) continue;
                ushort magic = (ushort)(data[0] | (data[1] << 8));
                if (magic != Protocol.UdpMagic) continue;
                uint token = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));
                if (!_tokenToConnection.TryGetValue(token, out int id) || !_connections.TryGetValue(id, out var c)) continue;
                c.UdpEndpoint = new IPEndPoint(any.Address, any.Port);

                var payload = new byte[data.Length - 6];
                Buffer.BlockCopy(data, 6, payload, 0, payload.Length);
                _events.Enqueue(new TransportEvent { Type = TransportEventType.Unreliable, ConnectionId = id, Payload = payload });
            }
        }

        private void Close(Connection c)
        {
            if (c.Closed) return;
            c.Closed = true;
            try { c.Tcp.Close(); } catch (Exception) { }
            _connections.TryRemove(c.Id, out _);
            _tokenToConnection.TryRemove(c.Token, out _);
            _events.Enqueue(new TransportEvent { Type = TransportEventType.Disconnected, ConnectionId = c.Id });
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Stop(); } catch (Exception) { }
            try { _udp?.Close(); } catch (Exception) { }
            foreach (var c in _connections.Values) { try { c.Tcp.Close(); } catch (Exception) { } }
            _connections.Clear();
        }

        // ── framing helpers shared with the client ──

        internal static void WriteFrame(Stream stream, byte[] data, int length)
        {
            if (length > Protocol.MaxReliableFrame) throw new IOException("Frame too large.");
            var header = new byte[] { (byte)length, (byte)(length >> 8), (byte)(length >> 16) };
            stream.Write(header, 0, 3);
            stream.Write(data, 0, length);
        }

        internal static byte[] ReadFrame(Stream stream)
        {
            var header = new byte[3];
            if (!ReadExact(stream, header, 3)) return null;
            int length = header[0] | (header[1] << 8) | (header[2] << 16);
            if (length <= 0 || length > Protocol.MaxReliableFrame) throw new IOException("Bad frame length.");
            var frame = new byte[length];
            if (!ReadExact(stream, frame, length)) return null;
            return frame;
        }

        private static bool ReadExact(Stream s, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buffer, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        /// <summary>Windows reports ICMP port-unreachable as a socket error on the next Receive; disable that.</summary>
        internal static void IgnoreUdpConnectionReset(UdpClient udp)
        {
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception) { /* not supported on this platform */ }
        }
    }

    public sealed class ClientTransport : IDisposable
    {
        private readonly ConcurrentQueue<TransportEvent> _events = new ConcurrentQueue<TransportEvent>();
        private readonly object _writeLock = new object();
        private TcpClient _tcp;
        private NetworkStream _stream;
        private UdpClient _udp;
        private volatile bool _running;
        private uint _token;
        private readonly NetWriter _udpWriter = new NetWriter(1500);

        public bool IsConnected => _running;
        public IPEndPoint ServerEndpoint { get; private set; }

        public async Task ConnectAsync(string host, int port, int timeoutMs = 5000)
        {
            IPAddress address = await ResolveAsync(host).ConfigureAwait(false);
            ServerEndpoint = new IPEndPoint(address, port);
            _tcp = new TcpClient(address.AddressFamily) { NoDelay = true };
            Task connect = _tcp.ConnectAsync(address, port);
            if (await Task.WhenAny(connect, Task.Delay(timeoutMs)).ConfigureAwait(false) != connect)
            {
                _tcp.Close();
                throw new TimeoutException($"Could not reach {host}:{port}.");
            }
            await connect.ConfigureAwait(false);
            _stream = _tcp.GetStream();
            _udp = new UdpClient(address.AddressFamily);
            ServerTransport.IgnoreUdpConnectionReset(_udp);
            _udp.Connect(ServerEndpoint);
            _running = true;
            new Thread(TcpLoop) { IsBackground = true, Name = "GC-ClientTcp" }.Start();
            new Thread(UdpLoop) { IsBackground = true, Name = "GC-ClientUdp" }.Start();
        }

        private static async Task<IPAddress> ResolveAsync(string host)
        {
            if (IPAddress.TryParse(host, out var ip)) return ip;
            var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            foreach (var a in addresses) if (a.AddressFamily == AddressFamily.InterNetwork) return a;
            if (addresses.Length > 0) return addresses[0];
            throw new IOException($"Unknown host '{host}'.");
        }

        public void SetToken(uint token) => _token = token;

        public bool Poll(out TransportEvent ev) => _events.TryDequeue(out ev);

        public void SendReliable(NetWriter w)
        {
            if (!_running) return;
            try { lock (_writeLock) ServerTransport.WriteFrame(_stream, w.Data, w.Length); }
            catch (Exception) { Shutdown(); }
        }

        /// <summary>Prepends magic + token; <paramref name="w"/> holds [MsgType][payload].</summary>
        public void SendUnreliable(NetWriter w)
        {
            if (!_running || _token == 0) return;
            lock (_udpWriter)
            {
                _udpWriter.Reset();
                _udpWriter.UShort(Protocol.UdpMagic);
                _udpWriter.UInt(_token);
                _udpWriter.Bytes(w.Data, 0, w.Length);
                try { _udp.Send(_udpWriter.Data, _udpWriter.Length); }
                catch (Exception) { /* best effort */ }
            }
        }

        private void TcpLoop()
        {
            try
            {
                while (_running)
                {
                    byte[] frame = ServerTransport.ReadFrame(_stream);
                    if (frame == null) break;
                    _events.Enqueue(new TransportEvent { Type = TransportEventType.Reliable, Payload = frame });
                }
            }
            catch (Exception) { }
            Shutdown();
        }

        private void UdpLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                byte[] data;
                try { data = _udp.Receive(ref any); }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }
                catch (NullReferenceException) { return; }
                if (data.Length < 3) continue;
                ushort magic = (ushort)(data[0] | (data[1] << 8));
                if (magic != Protocol.UdpMagic) continue;
                var payload = new byte[data.Length - 2];
                Buffer.BlockCopy(data, 2, payload, 0, payload.Length);
                _events.Enqueue(new TransportEvent { Type = TransportEventType.Unreliable, Payload = payload });
            }
        }

        private void Shutdown()
        {
            if (!_running) return;
            _running = false;
            try { _tcp?.Close(); } catch (Exception) { }
            try { _udp?.Close(); } catch (Exception) { }
            _events.Enqueue(new TransportEvent { Type = TransportEventType.Disconnected });
        }

        public void Dispose() => Shutdown();
    }

    /// <summary>Server-side unreliable framing helper: [magic][MsgType][payload] (no token server → client).</summary>
    public static class UdpFraming
    {
        public static void BeginServerPacket(NetWriter w, MsgType type)
        {
            w.Reset();
            w.UShort(Protocol.UdpMagic);
            w.Byte((byte)type);
        }
    }
}
