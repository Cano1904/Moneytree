using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Glasscore.Simulation;

namespace Glasscore.Net
{
    public sealed class ServerConfig
    {
        public int Port = Protocol.DefaultPort;
        public string ServerName = "GLASSCORE";
        /// <summary>6-char friend code; generated when null.</summary>
        public string LobbyCode;
        public bool Dedicated;
        public LobbySettings Settings = new LobbySettings();
        /// <summary>Directory for ratings.tsv; null disables persistence.</summary>
        public string DataDirectory;
        public bool EnableDiscovery = true;
        public int TickRate = MatchTimings.TickRate;
        public int SnapshotInterval = 2; // ticks → 30 Hz snapshots
        public Action<string> Log;
    }

    /// <summary>
    /// Authoritative GLASSCORE server: lobby management (host privileges, ready checks, chat) and the
    /// 60 Hz match simulation. Runs headless in the .NET dedicated server, or on a background thread
    /// inside the game when a player hosts a lobby (listen server).
    /// </summary>
    public sealed class GameServer : IDisposable
    {
        private sealed class Seat
        {
            public int ConnectionId;
            public byte Slot;
            public string Name = "Glazier";
            public string PlayerKey = string.Empty;
            public byte Skin;
            public byte Trail;
            public bool Ready;
            public byte Team;
            public int Mmr = RatingSystem.StartingRating;
            public int Games;
            public int JoinOrder;
            public ushort PingMs;
            public bool Connected = true;
            public double DisconnectedAt;
            public bool InMatch;
            public uint LastReceivedSeq;
            public uint LastProcessedSeq;
            public PlayerInput LastInput;
            public readonly Queue<PlayerInput> Inputs = new Queue<PlayerInput>();
        }

        private const int MaxQueuedInputs = 10;

        private readonly ServerConfig _config;
        private readonly ServerTransport _transport = new ServerTransport();
        private readonly Seat[] _seats = new Seat[GameWorld.MaxPlayers];
        private readonly Dictionary<int, Seat> _byConnection = new Dictionary<int, Seat>();
        private readonly HashSet<int> _pendingConnections = new HashSet<int>();
        private readonly ChatFilter _chat = new ChatFilter();
        private readonly RatingStore _ratings;
        private readonly NetWriter _w = new NetWriter(4096);
        private readonly NetWriter _uw = new NetWriter(4096);
        private readonly PlayerInput[] _inputs = new PlayerInput[GameWorld.MaxPlayers];
        private readonly SnapshotData _snapshot = new SnapshotData();
        private readonly Random _random = new Random();
        private DiscoveryResponder _discovery;
        private Thread _thread;
        private volatile bool _running;

        private LobbySettings _settings;
        private byte _hostSlot = Protocol.SystemSlot;
        private int _joinCounter;
        private bool _lobbyDirty = true;
        private double _lastLobbyBroadcast;
        private double _startCountdownAt = -1;
        private GameWorld _world;
        private bool _resultsSent;
        private double _time;
        private double _accumulator;

        public string LobbyCode { get; }
        public int Port => _transport.Port;
        public bool InMatch => _world != null;
        public GameWorld World => _world;
        public int PlayerCount { get { int n = 0; foreach (var s in _seats) if (s != null && s.Connected) n++; return n; } }

        public GameServer(ServerConfig config)
        {
            _config = config;
            _settings = config.Settings.Clone();
            _ratings = new RatingStore(config.DataDirectory);
            // Fully qualified: inside this class "LobbyCode" names the property.
            string code = Glasscore.Simulation.LobbyCode.Normalize(config.LobbyCode);
            if (!Glasscore.Simulation.LobbyCode.IsValid(code))
            {
                var rng = new DeterministicRandom((ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount);
                code = Glasscore.Simulation.LobbyCode.Generate(ref rng);
            }
            LobbyCode = code;
        }

        private void Log(string msg) => _config.Log?.Invoke($"[server {LobbyCode}] {msg}");

        public void Start()
        {
            _transport.Start(_config.Port);
            if (_config.EnableDiscovery)
            {
                _discovery = new DiscoveryResponder(BuildDiscoveryInfo);
                if (!_discovery.Start()) Log("LAN discovery unavailable (port in use); direct IP joins only.");
            }
            _running = true;
            Log($"listening on TCP/UDP {Port}, lobby code {LobbyCode}");
        }

        /// <summary>Starts the server loop on a background thread (listen-server mode).</summary>
        public void StartThread()
        {
            Start();
            _thread = new Thread(() => RunLoop()) { IsBackground = true, Name = "GC-Server" };
            _thread.Start();
        }

        /// <summary>Blocking loop for the dedicated server.</summary>
        public void RunLoop(CancellationToken token = default)
        {
            var sw = Stopwatch.StartNew();
            while (_running && !token.IsCancellationRequested)
            {
                Update(sw.Elapsed.TotalSeconds);
                Thread.Sleep(1);
            }
        }

        public void Update(double now)
        {
            double dt = _time == 0 ? 0 : now - _time;
            _time = now;
            if (dt > 0.25) dt = 0.25; // never spiral after a stall

            PumpTransport();

            double tickDt = 1.0 / _config.TickRate;
            _accumulator += dt;
            while (_accumulator >= tickDt)
            {
                _accumulator -= tickDt;
                if (_world != null) MatchTick();
            }

            if (_world == null) LobbyUpdate();
            ExpireDisconnectedSeats();
        }

        // ───────────────────────────── transport ─────────────────────────────

        private void PumpTransport()
        {
            while (_transport.Poll(out var ev))
            {
                switch (ev.Type)
                {
                    case TransportEventType.Connected:
                        _pendingConnections.Add(ev.ConnectionId);
                        break;
                    case TransportEventType.Disconnected:
                        _pendingConnections.Remove(ev.ConnectionId);
                        OnDisconnected(ev.ConnectionId);
                        break;
                    case TransportEventType.Reliable:
                        try { HandleReliable(ev.ConnectionId, ev.Payload); }
                        catch (FormatException) { _transport.Disconnect(ev.ConnectionId); }
                        break;
                    case TransportEventType.Unreliable:
                        try { HandleUnreliable(ev.ConnectionId, ev.Payload); }
                        catch (FormatException) { }
                        break;
                }
            }
        }

        private void HandleReliable(int conn, byte[] data)
        {
            var r = new NetReader(data);
            var type = (MsgType)r.Byte();

            if (type == MsgType.Hello)
            {
                HandleHello(conn, ref r);
                return;
            }

            if (!_byConnection.TryGetValue(conn, out Seat seat)) return;
            bool isHost = seat.Slot == _hostSlot;

            switch (type)
            {
                case MsgType.SetProfile:
                    seat.Name = LobbyRules.SanitizePlayerName(r.String());
                    seat.Skin = r.Byte();
                    seat.Trail = r.Byte();
                    _lobbyDirty = true;
                    break;

                case MsgType.SetReady:
                    if (_world != null) break;
                    seat.Ready = r.Bool();
                    if (!seat.Ready) _startCountdownAt = -1;
                    _lobbyDirty = true;
                    break;

                case MsgType.HostSettings:
                    if (!isHost || _world != null) break;
                    var s = LobbySettings.Read(ref r);
                    s.MapId = (byte)GcMath.Clamp(s.MapId, 0, MapCatalog.All.Length - 1);
                    s.MaxPlayers = (byte)LobbyRules.ClampPlayerLimit(s.MaxPlayers, PlayerCount);
                    s.MaxScore = (byte)LobbyRules.ClampMaxScore(s.MaxScore);
                    _settings = s;
                    AssignTeams();
                    _lobbyDirty = true;
                    break;

                case MsgType.HostKick:
                    if (!isHost) break;
                    byte target = r.Byte();
                    if (target == seat.Slot || target >= _seats.Length || _seats[target] == null) break;
                    Seat victim = _seats[target];
                    SystemChat($"{victim.Name} was kicked by the host.");
                    if (victim.Connected)
                    {
                        Begin(MsgType.Kicked);
                        _transport.SendReliable(victim.ConnectionId, _w);
                        _transport.Disconnect(victim.ConnectionId);
                    }
                    RemoveSeat(victim);
                    break;

                case MsgType.HostStart:
                    if (!isHost || _world != null) break;
                    if (CanStart()) BeginStartCountdown();
                    else SystemChat("Everyone must be READY (minimum 2 players).");
                    break;

                case MsgType.Chat:
                    string text = _chat.Process(seat.Slot, r.String(), _time);
                    if (text == null) break;
                    Begin(MsgType.ChatBroadcast);
                    _w.Byte(seat.Slot);
                    _w.String(seat.Name, 64);
                    _w.String(text, 512);
                    BroadcastReliable();
                    break;

                case MsgType.SceneLoaded:
                    if (_world != null) _world.Loaded[seat.Slot] = true;
                    break;

                case MsgType.LeaveMatch:
                    _transport.Disconnect(conn);
                    break;
            }
        }

        private void HandleHello(int conn, ref NetReader r)
        {
            ushort version = r.UShort();
            string name = LobbyRules.SanitizePlayerName(r.String());
            string key = r.String();
            int claimedMmr = r.Int();
            byte skin = r.Byte();
            byte trail = r.Byte();

            if (version != Protocol.Version)
            {
                Reject(conn, $"Version mismatch (server {Protocol.Version}, client {version}).");
                return;
            }

            // Reconnect into a reserved seat (same player key) — works mid-match.
            Seat seat = null;
            foreach (var s in _seats)
                if (s != null && !s.Connected && !string.IsNullOrEmpty(key) && s.PlayerKey == key) { seat = s; break; }

            if (seat == null)
            {
                if (_world != null) { Reject(conn, "Match in progress."); return; }
                if (PlayerCount >= _settings.MaxPlayers) { Reject(conn, "Lobby is full."); return; }
                int slot = Array.IndexOf(_seats, null);
                if (slot < 0) { Reject(conn, "Lobby is full."); return; }
                var record = _ratings.Get(key, claimedMmr);
                seat = new Seat { Slot = (byte)slot, PlayerKey = key ?? string.Empty, JoinOrder = _joinCounter++, Mmr = record.Rating, Games = record.Games };
                _seats[slot] = seat;
                if (_hostSlot == Protocol.SystemSlot) _hostSlot = seat.Slot;
            }

            _pendingConnections.Remove(conn);
            seat.ConnectionId = conn;
            seat.Connected = true;
            seat.Name = name;
            seat.Skin = skin;
            seat.Trail = trail;
            seat.Inputs.Clear();
            seat.LastReceivedSeq = 0;
            seat.LastProcessedSeq = 0;
            _byConnection[conn] = seat;
            AssignTeams();

            Begin(MsgType.Welcome);
            _w.Byte(seat.Slot);
            _w.UInt(_transport.GetToken(conn));
            _w.UShort((ushort)_config.TickRate);
            _w.String(LobbyCode, 16);
            _w.String(_config.ServerName, 64);
            _w.Bool(_config.Dedicated);
            _transport.SendReliable(conn, _w);

            SystemChat($"{seat.Name} joined.");
            _lobbyDirty = true;
            BroadcastLobby();

            if (_world != null)
            {
                // Rejoin: re-activate in the simulation with preserved stats, then sync state.
                _world.AddPlayer(seat.Slot, seat.Team, keepStats: true);
                seat.InMatch = true;
                SendMatchStart(conn);
                SendFullTileState(conn);
                SendPhase(conn);
            }
            Log($"{seat.Name} joined slot {seat.Slot} ({_transport.GetAddress(conn)})");
        }

        private void Reject(int conn, string reason)
        {
            Begin(MsgType.Reject);
            _w.String(reason, 256);
            _transport.SendReliable(conn, _w);
            _transport.Disconnect(conn);
        }

        private void HandleUnreliable(int conn, byte[] data)
        {
            if (!_byConnection.TryGetValue(conn, out Seat seat)) return;
            var r = new NetReader(data);
            var type = (MsgType)r.Byte();
            switch (type)
            {
                case MsgType.Input:
                    int count = r.Byte();
                    for (int i = 0; i < count; i++)
                    {
                        PlayerInput input = Codec.ReadInput(ref r);
                        if (input.Sequence <= seat.LastReceivedSeq) continue;
                        seat.LastReceivedSeq = input.Sequence;
                        seat.Inputs.Enqueue(input);
                    }
                    if (seat.Inputs.Count > MaxQueuedInputs)
                    {
                        // Client is running ahead (e.g. after a hitch): skip old inputs to stay responsive,
                        // but carry their button presses forward so a tap (jump, weapon switch) is never lost.
                        InputButtons carried = InputButtons.None;
                        while (seat.Inputs.Count > MaxQueuedInputs)
                        {
                            var dropped = seat.Inputs.Dequeue();
                            carried |= dropped.Buttons;
                            seat.LastProcessedSeq = dropped.Sequence;
                        }
                        var queued = seat.Inputs.ToArray();
                        queued[0].Buttons |= carried;
                        seat.Inputs.Clear();
                        foreach (var q in queued) seat.Inputs.Enqueue(q);
                    }
                    break;

                case MsgType.Ping:
                    double clientTime = r.Double();
                    ushort lastRtt = r.UShort();
                    seat.PingMs = lastRtt;
                    _world?.SetLatency(seat.Slot, lastRtt / 1000f);
                    UdpFraming.BeginServerPacket(_uw, MsgType.Pong);
                    _uw.Double(clientTime);
                    _uw.Int(_world?.Tick ?? 0);
                    _transport.SendUnreliable(conn, _uw);
                    break;

                case MsgType.Voice:
                    ushort seq = r.UShort();
                    int len = r.Remaining;
                    if (len <= 0 || len > 1200) break;
                    byte[] audio = r.Bytes(len);
                    UdpFraming.BeginServerPacket(_uw, MsgType.Voice);
                    _uw.Byte(seat.Slot);
                    _uw.UShort(seq);
                    _uw.Bytes(audio, 0, audio.Length);
                    foreach (var other in _seats)
                        if (other != null && other.Connected && other != seat) _transport.SendUnreliable(other.ConnectionId, _uw);
                    break;

                case MsgType.UdpHello:
                    break; // endpoint registration happens in the transport
            }
        }

        private void OnDisconnected(int conn)
        {
            if (!_byConnection.TryGetValue(conn, out Seat seat)) return;
            _byConnection.Remove(conn);
            _chat.Forget(seat.Slot);

            if (_world != null && seat.InMatch)
            {
                // Hold the seat for a reconnect; the leaver is penalised if they never return.
                seat.Connected = false;
                seat.DisconnectedAt = _time;
                _world.RemovePlayer(seat.Slot);
                SystemChat($"{seat.Name} disconnected ({(int)Protocol.ReconnectGrace}s to reconnect).");
            }
            else
            {
                SystemChat($"{seat.Name} left.");
                RemoveSeat(seat);
            }
            Log($"{seat.Name} disconnected");
        }

        private void RemoveSeat(Seat seat)
        {
            _seats[seat.Slot] = null;
            if (_world != null) _world.RemovePlayer(seat.Slot);
            if (seat.Connected) _byConnection.Remove(seat.ConnectionId);
            if (_hostSlot == seat.Slot) MigrateHost();
            _startCountdownAt = -1;
            AssignTeams();
            _lobbyDirty = true;
            BroadcastLobby();
        }

        private void MigrateHost()
        {
            var remaining = new List<(int, int)>();
            foreach (var s in _seats) if (s != null && s.Connected) remaining.Add((s.Slot, s.JoinOrder));
            int next = LobbyRules.NextHost(remaining);
            _hostSlot = next < 0 ? Protocol.SystemSlot : (byte)next;
            if (next >= 0) SystemChat($"{_seats[next].Name} is now the host.");
        }

        private void ExpireDisconnectedSeats()
        {
            if (_world != null) return; // decided at match end (leave penalty)
            foreach (var s in _seats)
                if (s != null && !s.Connected && _time - s.DisconnectedAt > Protocol.ReconnectGrace) RemoveSeat(s);
        }

        private void AssignTeams()
        {
            // Alternate by join order so teams stay balanced as players come and go.
            var order = new List<Seat>();
            foreach (var s in _seats) if (s != null) order.Add(s);
            order.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));
            for (int i = 0; i < order.Count; i++) order[i].Team = _settings.Teams ? (byte)(i % 2) : order[i].Slot;
        }

        // ───────────────────────────── lobby ─────────────────────────────

        private bool CanStart()
        {
            var flags = new List<bool>();
            foreach (var s in _seats) if (s != null && s.Connected) flags.Add(s.Ready);
            return LobbyRules.CanStart(flags);
        }

        private void BeginStartCountdown()
        {
            _startCountdownAt = _time + LobbyRules.StartCountdownSeconds;
            Begin(MsgType.LobbyCountdown);
            _w.Float(LobbyRules.StartCountdownSeconds);
            BroadcastReliable();
        }

        private void LobbyUpdate()
        {
            if (_config.Dedicated && _settings.IsPublic && _startCountdownAt < 0 && CanStart()) BeginStartCountdown();

            if (_startCountdownAt >= 0)
            {
                if (!CanStart())
                {
                    _startCountdownAt = -1;
                    Begin(MsgType.LobbyCountdown);
                    _w.Float(-1f);
                    BroadcastReliable();
                }
                else if (_time >= _startCountdownAt)
                {
                    _startCountdownAt = -1;
                    StartMatch();
                    return;
                }
            }

            if (_lobbyDirty || _time - _lastLobbyBroadcast > 2.0) BroadcastLobby();
        }

        private void BroadcastLobby()
        {
            _lobbyDirty = false;
            _lastLobbyBroadcast = _time;
            Begin(MsgType.LobbyState);
            BuildLobbyView().Write(_w);
            BroadcastReliable();
        }

        private LobbyView BuildLobbyView()
        {
            var v = new LobbyView { Code = LobbyCode, ServerName = _config.ServerName, HostSlot = _hostSlot, InMatch = _world != null, Settings = _settings };
            foreach (var s in _seats)
            {
                if (s == null) continue;
                v.Roster.Add(new RosterEntry { Slot = s.Slot, Name = s.Name, Skin = s.Skin, Trail = s.Trail, Ready = s.Ready, PingMs = s.PingMs, Team = s.Team, Mmr = s.Mmr, Connected = s.Connected });
            }
            return v;
        }

        private DiscoveryInfo BuildDiscoveryInfo()
        {
            // Called from the discovery thread: only read immutable/atomic values.
            int players = 0, mmrSum = 0;
            var seats = _seats;
            for (int i = 0; i < seats.Length; i++)
            {
                var s = seats[i];
                if (s == null) continue;
                players++;
                mmrSum += s.Mmr;
            }
            var settings = _settings;
            return new DiscoveryInfo
            {
                Code = LobbyCode,
                ServerName = _config.ServerName,
                MapId = settings.MapId,
                Players = (byte)players,
                MaxPlayers = settings.MaxPlayers,
                IsPublic = settings.IsPublic,
                InMatch = _world != null,
                AverageMmr = players > 0 ? mmrSum / players : RatingSystem.StartingRating,
                GamePort = (ushort)Port,
                Dedicated = _config.Dedicated,
            };
        }

        private void SystemChat(string text)
        {
            Begin(MsgType.ChatBroadcast);
            _w.Byte(Protocol.SystemSlot);
            _w.String("SYSTEM", 64);
            _w.String(text, 512);
            BroadcastReliable();
        }

        // ───────────────────────────── match ─────────────────────────────

        private void StartMatch()
        {
            var config = new MatchConfig
            {
                MapId = _settings.MapId,
                MatchSeed = _random.Next(1, int.MaxValue),
                MaxScore = _settings.MaxScore,
                FriendlyFire = _settings.FriendlyFire,
                Teams = _settings.Teams,
                Competitive = _settings.Competitive,
            };
            _world = new GameWorld(config, _config.TickRate);
            _resultsSent = false;
            AssignTeams();
            foreach (var s in _seats)
            {
                if (s == null || !s.Connected) continue;
                s.InMatch = true;
                s.Inputs.Clear();
                s.LastProcessedSeq = s.LastReceivedSeq;
                s.LastInput = default;
                _world.AddPlayer(s.Slot, s.Team);
                _world.SetLatency(s.Slot, s.PingMs / 1000f);
            }

            foreach (var s in _seats) if (s != null && s.Connected) SendMatchStart(s.ConnectionId);
            _lobbyDirty = true;
            BroadcastLobby();
            Log($"match started on {MapCatalog.Get(config.MapId).Name} (seed {config.MatchSeed})");
        }

        private void SendMatchStart(int conn)
        {
            MatchConfig c = _world.Config;
            Begin(MsgType.MatchStart);
            _w.Byte((byte)c.MapId);
            _w.Int(c.MatchSeed);
            _w.Byte((byte)c.MaxScore);
            _w.Bool(c.FriendlyFire);
            _w.Bool(c.Teams);
            _w.Int(_world.Tick);
            _transport.SendReliable(conn, _w);
        }

        private void SendFullTileState(int conn)
        {
            Begin(MsgType.TileState);
            _w.UShort((ushort)_world.Layout.Count);
            for (int i = 0; i < _world.Layout.Count; i++) _w.UShort(TileGridModel.Quantize(_world.Tiles.GetIntegrity(i)));
            _transport.SendReliable(conn, _w);
        }

        private void SendPhase(int conn)
        {
            WritePhase();
            _transport.SendReliable(conn, _w);
        }

        private void WritePhase()
        {
            Begin(MsgType.MatchPhase);
            _w.Byte((byte)_world.Clock.Phase);
            _w.Int(_world.Clock.PhaseStartTick);
            _w.Int(_world.Tick);
            _w.Byte((byte)_world.Clock.EndReason);
        }

        private void MatchTick()
        {
            // 1) one input per player per tick (repeat the last one if the queue ran dry)
            foreach (var s in _seats)
            {
                if (s == null || !s.InMatch || !s.Connected) continue;
                if (s.Inputs.Count > 0)
                {
                    s.LastInput = s.Inputs.Dequeue();
                    s.LastProcessedSeq = s.LastInput.Sequence;
                }
                _inputs[s.Slot] = s.LastInput;
            }

            // 2) simulate
            _world.Step(_inputs);

            // 3) reliable events (ordered on TCP: tile deltas → shatter RPCs → gameplay events)
            if (_world.Tiles.DirtyTiles.Count > 0)
            {
                Begin(MsgType.TileDelta);
                var dirty = _world.Tiles.DirtyTiles;
                _w.UShort((ushort)dirty.Count);
                for (int i = 0; i < dirty.Count; i++)
                {
                    _w.UShort((ushort)dirty[i]);
                    _w.UShort(TileGridModel.Quantize(_world.Tiles.GetIntegrity(dirty[i])));
                }
                BroadcastReliable();
            }

            foreach (var e in _world.Events)
            {
                if (e.Type == WorldEventType.ShatterTile)
                {
                    // RPC_ShatterTile(int tileID, Vector3 impactPoint, float force): clients derive the
                    // Voronoi seed from (matchSeed, tileID, impactPoint) — no fragment data on the wire.
                    Begin(MsgType.ShatterTile);
                    _w.Int(e.A);
                    _w.Vec3(e.Point);
                    _w.Float(e.Value);
                    _w.Short((short)e.B);
                    BroadcastReliable();
                }
                else
                {
                    Begin(MsgType.WorldEvent);
                    _w.Byte((byte)e.Type);
                    _w.Int(e.A);
                    _w.Int(e.B);
                    _w.Int(e.C);
                    _w.Vec3(e.Point);
                    _w.Float(e.Value);
                    BroadcastReliable();
                }
            }

            foreach (var me in _world.MatchEvents)
            {
                if (me.Type != MatchEventType.PhaseChanged) continue;
                WritePhase();
                BroadcastReliable();
                OnPhaseEntered(me.Phase);
                if (_world == null) return;
            }

            // 4) snapshots
            if (_world.Tick % _config.SnapshotInterval == 0) SendSnapshots();
        }

        private void SendSnapshots()
        {
            _snapshot.ServerTick = _world.Tick;
            _snapshot.Phase = _world.Clock.Phase;
            _snapshot.PhaseStartTick = _world.Clock.PhaseStartTick;
            int mask = 0;
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                _snapshot.Players[i] = _world.Players[i];
                _snapshot.Scores[i] = (short)_world.Stats[i].Score;
                if (_world.Players[i].Active) mask |= 1 << i;
            }
            _snapshot.Projectiles.Clear();
            foreach (var p in _world.Projectiles)
                _snapshot.Projectiles.Add(new ProjectileView { Id = p.Id, Owner = p.Owner, Weapon = p.Weapon, Position = p.Position, Velocity = p.CurrentVelocity });

            foreach (var s in _seats)
            {
                if (s == null || !s.Connected) continue;
                _snapshot.AckSequence = s.LastProcessedSeq;
                UdpFraming.BeginServerPacket(_uw, MsgType.Snapshot);
                Codec.WriteSnapshot(_uw, _snapshot, mask);
                _transport.SendUnreliable(s.ConnectionId, _uw);
            }
        }

        private void OnPhaseEntered(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.MatchEnd:
                    SendHighlights();
                    break;
                case MatchPhase.Results:
                    SendResults();
                    break;
                case MatchPhase.ReturnToLobby:
                    if (!_resultsSent) SendResults();
                    EndMatch();
                    break;
            }
        }

        private void SendHighlights()
        {
            List<Highlight> top = _world.Highlights.Top(5);
            Begin(MsgType.Highlights);
            _w.Byte((byte)top.Count);
            foreach (var h in top)
            {
                _w.Byte(h.Slot);
                _w.Int(h.Tick);
                _w.Byte((byte)h.Type);
                _w.Byte((byte)Math.Min(255, h.Count));
                _w.Short((short)h.Score);
                _w.String(h.Label, 64);
            }
            BroadcastReliable();
        }

        private void SendResults()
        {
            _resultsSent = true;
            int[] placements = _world.ComputePlacements();
            var entries = new List<RatingEntry>();
            foreach (var s in _seats)
            {
                if (s == null || !s.InMatch) continue;
                entries.Add(new RatingEntry { PlayerId = s.Slot, Rating = s.Mmr, GamesPlayed = s.Games, Placement = Math.Max(1, placements[s.Slot]), LeftEarly = !s.Connected });
            }
            Dictionary<int, int> deltas = RatingSystem.ComputeDeltas(entries, _settings.Competitive);

            Begin(MsgType.Results);
            _w.Byte((byte)_world.Clock.EndReason);
            _w.Byte((byte)entries.Count);
            foreach (var e in entries)
            {
                Seat s = _seats[e.PlayerId];
                int delta = deltas.TryGetValue(e.PlayerId, out int d) ? d : 0;
                s.Mmr += delta;
                s.Games++;
                _ratings.Set(s.PlayerKey, new RatingStore.Record { Rating = s.Mmr, Games = s.Games });
                PlayerStats st = _world.Stats[s.Slot];
                _w.Byte(s.Slot);
                _w.String(s.Name, 64);
                _w.Short((short)st.Score);
                _w.Short((short)st.Knockouts);
                _w.Short((short)st.Deaths);
                _w.Short((short)st.TilesShattered);
                _w.Byte((byte)(s.Connected ? placements[s.Slot] : entries.Count));
                _w.Short((short)delta);
                _w.Int(s.Mmr);
            }
            BroadcastReliable();
            try { _ratings.Save(); } catch (Exception ex) { Log("could not save ratings: " + ex.Message); }
        }

        private void EndMatch()
        {
            _world = null;
            foreach (var s in _seats)
            {
                if (s == null) continue;
                s.InMatch = false;
                s.Ready = false;
                if (!s.Connected) RemoveSeat(s);
            }
            Begin(MsgType.ReturnToLobby);
            BroadcastReliable();
            _lobbyDirty = true;
            BroadcastLobby();
            Log("match finished, back to lobby");
        }

        // ───────────────────────────── helpers ─────────────────────────────

        private void Begin(MsgType type)
        {
            _w.Reset();
            _w.Byte((byte)type);
        }

        private void BroadcastReliable()
        {
            foreach (var s in _seats)
                if (s != null && s.Connected) _transport.SendReliable(s.ConnectionId, _w);
        }

        public void Dispose()
        {
            _running = false;
            try { _thread?.Join(500); } catch (Exception) { }
            _discovery?.Dispose();
            _transport.Dispose();
        }
    }
}
