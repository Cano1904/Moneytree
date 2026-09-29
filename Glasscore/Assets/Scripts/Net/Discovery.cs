using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Glasscore.Net
{
    public sealed class DiscoveryInfo
    {
        public string Code;
        public string ServerName;
        public byte MapId;
        public byte Players;
        public byte MaxPlayers;
        public bool IsPublic;
        public bool InMatch;
        public int AverageMmr;
        public ushort GamePort;
        public bool Dedicated;

        // Filled in by the searching client.
        public IPAddress Address;
        public int RoundTripMs;

        public void Write(NetWriter w)
        {
            w.String(Code, 16); w.String(ServerName, 64); w.Byte(MapId); w.Byte(Players); w.Byte(MaxPlayers);
            w.Bool(IsPublic); w.Bool(InMatch); w.Int(AverageMmr); w.UShort(GamePort); w.Bool(Dedicated);
        }

        public static DiscoveryInfo Read(ref NetReader r) => new DiscoveryInfo
        {
            Code = r.String(), ServerName = r.String(), MapId = r.Byte(), Players = r.Byte(), MaxPlayers = r.Byte(),
            IsPublic = r.Bool(), InMatch = r.Bool(), AverageMmr = r.Int(), GamePort = r.UShort(), Dedicated = r.Bool(),
        };

        public bool Joinable => !InMatch && Players < MaxPlayers;
    }

    /// <summary>
    /// Answers "who hosts lobby CODE?" / "any public lobby?" queries on UDP 27100. Several servers on one
    /// machine can share the port (SO_REUSEADDR) and all receive broadcast queries.
    /// </summary>
    public sealed class DiscoveryResponder : IDisposable
    {
        private static readonly uint QueryMagic = 0x51444347;  // "GCDQ"
        internal static readonly uint ReplyMagic = 0x52444347; // "GCDR"

        private readonly Func<DiscoveryInfo> _info;
        private UdpClient _udp;
        private volatile bool _running;

        public DiscoveryResponder(Func<DiscoveryInfo> info) { _info = info; }

        public bool Start(int port = Protocol.DiscoveryPort)
        {
            try
            {
                _udp = new UdpClient();
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                ServerTransport.IgnoreUdpConnectionReset(_udp);
            }
            catch (SocketException)
            {
                return false; // discovery unavailable; direct IP joins still work
            }
            _running = true;
            new Thread(Loop) { IsBackground = true, Name = "GC-Discovery" }.Start();
            return true;
        }

        private void Loop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var w = new NetWriter(256);
            while (_running)
            {
                byte[] data;
                try { data = _udp.Receive(ref remote); }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }

                try
                {
                    var r = new NetReader(data);
                    if (r.UInt() != QueryMagic || r.UShort() != Protocol.Version) continue;
                    string code = r.String();
                    DiscoveryInfo info = _info();
                    if (info == null) continue;
                    bool match = string.IsNullOrEmpty(code) ? info.IsPublic : string.Equals(code, info.Code, StringComparison.Ordinal);
                    if (!match) continue;

                    w.Reset();
                    w.UInt(ReplyMagic);
                    w.UShort(Protocol.Version);
                    info.Write(w);
                    _udp.Send(w.Data, w.Length, remote);
                }
                catch (FormatException) { }
                catch (SocketException) { }
            }
        }

        public void Dispose()
        {
            _running = false;
            try { _udp?.Close(); } catch (Exception) { }
        }

        internal static void WriteQuery(NetWriter w, string code)
        {
            w.UInt(QueryMagic);
            w.UShort(Protocol.Version);
            w.String(code ?? string.Empty, 16);
        }
    }

    public static class DiscoveryClient
    {
        /// <summary>
        /// Broadcasts a query on the LAN (plus unicast to any configured server hosts) and collects
        /// replies for <paramref name="timeoutMs"/>. Empty code = list public lobbies (Quick Match).
        /// </summary>
        public static Task<List<DiscoveryInfo>> SearchAsync(string code, IEnumerable<string> extraHosts, int timeoutMs = 700)
        {
            var hosts = extraHosts == null ? new List<string>() : new List<string>(extraHosts);
            return Task.Run(() => Search(code, hosts, timeoutMs));
        }

        private static List<DiscoveryInfo> Search(string code, List<string> extraHosts, int timeoutMs)
        {
            var results = new List<DiscoveryInfo>();
            var seen = new HashSet<string>();
            using (var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                udp.EnableBroadcast = true;
                ServerTransport.IgnoreUdpConnectionReset(udp);
                var w = new NetWriter(64);
                DiscoveryResponder.WriteQuery(w, code);
                var sentAt = DateTime.UtcNow;

                TrySend(udp, w, new IPEndPoint(IPAddress.Broadcast, Protocol.DiscoveryPort));
                TrySend(udp, w, new IPEndPoint(IPAddress.Loopback, Protocol.DiscoveryPort));
                foreach (string host in extraHosts)
                {
                    if (string.IsNullOrWhiteSpace(host)) continue;
                    try
                    {
                        foreach (var addr in Dns.GetHostAddresses(host.Trim()))
                            if (addr.AddressFamily == AddressFamily.InterNetwork)
                                TrySend(udp, w, new IPEndPoint(addr, Protocol.DiscoveryPort));
                    }
                    catch (Exception) { /* unresolvable host */ }
                }

                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var remote = new IPEndPoint(IPAddress.Any, 0);
                while (DateTime.UtcNow < deadline)
                {
                    int left = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                    if (left <= 0) break;
                    udp.Client.ReceiveTimeout = left;
                    byte[] data;
                    try { data = udp.Receive(ref remote); }
                    catch (SocketException) { break; }

                    try
                    {
                        var r = new NetReader(data);
                        if (r.UInt() != DiscoveryResponder.ReplyMagic || r.UShort() != Protocol.Version) continue;
                        var info = DiscoveryInfo.Read(ref r);
                        info.Address = remote.Address;
                        info.RoundTripMs = (int)(DateTime.UtcNow - sentAt).TotalMilliseconds;
                        // The same server answers once per interface (broadcast + loopback); keep one.
                        if (seen.Add(info.Code + ":" + info.GamePort)) results.Add(info);
                    }
                    catch (FormatException) { }
                }
            }
            return results;
        }

        private static void TrySend(UdpClient udp, NetWriter w, IPEndPoint ep)
        {
            try { udp.Send(w.Data, w.Length, ep); } catch (Exception) { }
        }
    }
}
