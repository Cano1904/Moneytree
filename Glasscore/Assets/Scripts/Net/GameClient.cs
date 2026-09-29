using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Glasscore.Simulation;

namespace Glasscore.Net
{
    public enum ClientStatus
    {
        Disconnected,
        Connecting,
        Lobby,
        Match,
    }

    public sealed class MatchInfo
    {
        public int MapId;
        public int MatchSeed;
        public int MaxScore;
        public bool FriendlyFire;
        public bool Teams;
        public TileLayout Layout;
    }

    public struct ShatterEvent
    {
        public int TileId;
        public Vec3 ImpactPoint;
        public float Force;
        public int Instigator;
    }

    public struct ChatLine
    {
        public byte Slot;
        public string Name;
        public string Text;
        public bool IsSystem => Slot == Protocol.SystemSlot;
    }

    /// <summary>
    /// Client session: lobby state, client-side prediction of the local player with server
    /// reconciliation, and 100 ms snapshot interpolation for everyone else. All callbacks fire from
    /// <see cref="Update"/>, i.e. on the caller's (Unity main) thread.
    /// </summary>
    public sealed class GameClient : IDisposable
    {
        private const int SnapshotBufferSize = 32;
        private const int MaxPendingInputs = 180;
        private const float SnapCorrectionDistance = 4f;
        private const float CorrectionHalfLife = 0.06f;

        private readonly ClientTransport _transport = new ClientTransport();
        private readonly NetWriter _w = new NetWriter(1024);
        private readonly NetWriter _uw = new NetWriter(1024);
        private readonly List<PlayerInput> _pending = new List<PlayerInput>();
        private readonly SnapshotData[] _snapshots = new SnapshotData[SnapshotBufferSize];
        private readonly SnapshotData _incoming = new SnapshotData();
        private int _snapshotCount;
        private int _latestSnapshotTick = -1;
        private double _latestSnapshotTime;
        private double _serverClock;
        private double _now;
        private double _lastPing = -10;
        private float _tickAccumulator;
        private uint _sequence;
        private bool _receivedSnapshot;
        private bool _sceneLoaded;
        private ushort _voiceSeq;

        public ClientStatus Status { get; private set; } = ClientStatus.Disconnected;
        public string LastError { get; private set; }
        public byte LocalSlot { get; private set; }
        public int TickRate { get; private set; } = MatchTimings.TickRate;
        public string ServerName { get; private set; } = string.Empty;
        public bool ServerDedicated { get; private set; }
        public LobbyView Lobby { get; private set; } = new LobbyView();
        public float LobbyCountdown { get; private set; } = -1f;
        public MatchInfo Match { get; private set; }
        public TileGridModel Tiles { get; private set; }
        public MatchPhase Phase { get; private set; }
        public int PhaseStartTick { get; private set; }
        public MatchEndReason EndReason { get; private set; }
        public PlayerState LocalPlayer;
        public Vec3 CorrectionOffset { get; private set; }
        public readonly PlayerState[] RenderPlayers = new PlayerState[GameWorld.MaxPlayers];
        public readonly short[] Scores = new short[GameWorld.MaxPlayers];
        public readonly List<ProjectileView> RenderProjectiles = new List<ProjectileView>();
        public readonly List<Highlight> Highlights = new List<Highlight>();
        public readonly List<ResultRow> Results = new List<ResultRow>();
        public int PingMs { get; private set; }
        public double ServerTickEstimate => _serverClock;
        public bool IsHost => Lobby != null && Lobby.HostSlot == LocalSlot;

        /// <summary>Supplies this tick's input (Unity samples keyboard/mouse/gamepad).</summary>
        public Func<uint, PlayerInput> InputProvider;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action OnLobbyUpdated;
        public event Action<ChatLine> OnChat;
        public event Action<MatchInfo> OnMatchStart;
        public event Action<MatchPhase> OnPhaseChanged;
        public event Action<ShatterEvent> OnShatter;
        public event Action<WorldEvent> OnWorldEvent;
        public event Action<PlayerStepEvents> OnLocalAction;
        public event Action OnHighlights;
        public event Action OnResults;
        public event Action OnReturnToLobby;
        public event Action<byte, ushort, byte[]> OnVoice;

        private string _name, _key;
        private int _mmr;
        private byte _skin, _trail;
        private string _disconnectReason;

        public GameClient()
        {
            for (int i = 0; i < SnapshotBufferSize; i++) _snapshots[i] = new SnapshotData();
        }

        public async Task ConnectAsync(string host, int port, string playerName, string playerKey, int mmr, byte skin, byte trail)
        {
            Status = ClientStatus.Connecting;
            LastError = null;
            _name = playerName; _key = playerKey; _mmr = mmr; _skin = skin; _trail = trail;
            try
            {
                await _transport.ConnectAsync(host, port).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Status = ClientStatus.Disconnected;
                LastError = ex.Message;
                throw;
            }
            Begin(MsgType.Hello);
            _w.UShort(Protocol.Version);
            _w.String(_name, 64);
            _w.String(_key, 64);
            _w.Int(_mmr);
            _w.Byte(_skin);
            _w.Byte(_trail);
            _transport.SendReliable(_w);
        }

        // ───────────────────────────── commands ─────────────────────────────

        public void SetReady(bool ready) { Begin(MsgType.SetReady); _w.Bool(ready); _transport.SendReliable(_w); }

        public void SetProfile(string name, byte skin, byte trail)
        {
            Begin(MsgType.SetProfile); _w.String(name, 64); _w.Byte(skin); _w.Byte(trail); _transport.SendReliable(_w);
        }

        public void SendChat(string text) { Begin(MsgType.Chat); _w.String(text, 512); _transport.SendReliable(_w); }
        public void HostApplySettings(LobbySettings s) { Begin(MsgType.HostSettings); s.Write(_w); _transport.SendReliable(_w); }
        public void HostKick(byte slot) { Begin(MsgType.HostKick); _w.Byte(slot); _transport.SendReliable(_w); }
        public void HostStart() { Begin(MsgType.HostStart); _transport.SendReliable(_w); }
        public void HostAddBot() { Begin(MsgType.HostAddBot); _transport.SendReliable(_w); }

        /// <summary>Unity calls this once the arena visuals are built; the server waits for everyone.</summary>
        public void SendSceneLoaded()
        {
            _sceneLoaded = true;
            Begin(MsgType.SceneLoaded);
            _transport.SendReliable(_w);
        }

        public void LeaveMatch()
        {
            Begin(MsgType.LeaveMatch);
            _transport.SendReliable(_w);
            Disconnect("Left the match.");
        }

        public void SendVoice(byte[] data, int length)
        {
            if (Status == ClientStatus.Disconnected || length <= 0 || length > 1200) return;
            _uw.Reset();
            _uw.Byte((byte)MsgType.Voice);
            _uw.UShort(_voiceSeq++);
            _uw.Bytes(data, 0, length);
            _transport.SendUnreliable(_uw);
        }

        public void Disconnect(string reason = "Disconnected.")
        {
            _disconnectReason = reason;
            _transport.Dispose();
        }

        // ───────────────────────────── update ─────────────────────────────

        public void Update(float deltaTime, double now)
        {
            _now = now;
            Pump();
            if (Status == ClientStatus.Disconnected || Status == ClientStatus.Connecting) return;

            if (now - _lastPing >= 1.0)
            {
                _lastPing = now;
                _uw.Reset();
                _uw.Byte((byte)MsgType.Ping);
                _uw.Double(now);
                _uw.UShort((ushort)Math.Min(PingMs, ushort.MaxValue));
                _transport.SendUnreliable(_uw);
            }

            if (LobbyCountdown > 0f) LobbyCountdown = Math.Max(0f, LobbyCountdown - deltaTime);

            if (Status != ClientStatus.Match || !_sceneLoaded) return;

            AdvanceServerClock(deltaTime);
            CorrectionOffset = CorrectionOffset * GcMath.DecayFactor(CorrectionHalfLife, deltaTime);

            float dt = 1f / TickRate;
            _tickAccumulator += deltaTime;
            if (_tickAccumulator > 0.25f) _tickAccumulator = 0.25f;
            while (_tickAccumulator >= dt)
            {
                _tickAccumulator -= dt;
                PredictTick(dt);
            }

            Interpolate();
        }

        private void PredictTick(float dt)
        {
            _sequence++;
            PlayerInput input = InputProvider != null ? InputProvider(_sequence) : PlayerInput.Neutral(_sequence, LocalPlayer.Yaw, LocalPlayer.Pitch);
            input.Sequence = _sequence;
            input = Codec.QuantizeInput(input);
            _pending.Add(input);
            if (_pending.Count > MaxPendingInputs) _pending.RemoveAt(0);

            if (_receivedSnapshot)
            {
                bool combat = MatchClock.IsCombatPhase(Phase);
                PlayerStepEvents ev = PlayerSimulation.Step(ref LocalPlayer, input, Tiles, dt, combat, combat);
                if (ev.Fired || ev.Jumped || ev.AnchorStarted || ev.AnchorBroken || ev.WeaponSwitched) OnLocalAction?.Invoke(ev);
            }

            _uw.Reset();
            _uw.Byte((byte)MsgType.Input);
            int count = Math.Min(Protocol.InputRedundancy, _pending.Count);
            _uw.Byte((byte)count);
            for (int i = _pending.Count - count; i < _pending.Count; i++) Codec.WriteInput(_uw, _pending[i]);
            _transport.SendUnreliable(_uw);
        }

        private void AdvanceServerClock(float deltaTime)
        {
            if (_latestSnapshotTick < 0) return;
            _serverClock += deltaTime * TickRate;
            double target = _latestSnapshotTick + (_now - _latestSnapshotTime) * TickRate;
            double diff = target - _serverClock;
            if (Math.Abs(diff) > TickRate * 0.5) _serverClock = target;
            else _serverClock += diff * 0.1;
        }

        public float PhaseTimeRemaining
        {
            get
            {
                float duration = MatchClock.PhaseDuration(Phase);
                if (duration <= 0f) return 0f;
                float elapsed = (float)((_serverClock - PhaseStartTick) / TickRate);
                return Math.Max(0f, duration - elapsed);
            }
        }

        private void Interpolate()
        {
            double renderTick = _serverClock - Protocol.InterpolationDelay * TickRate;
            SnapshotData a = null, b = null;
            for (int i = 0; i < _snapshotCount; i++)
            {
                SnapshotData s = _snapshots[i];
                if (s.ServerTick <= renderTick && (a == null || s.ServerTick > a.ServerTick)) a = s;
                if (s.ServerTick >= renderTick && (b == null || s.ServerTick < b.ServerTick)) b = s;
            }
            if (a == null) a = b;
            if (b == null) b = a;
            if (a == null) return;

            float t = b.ServerTick == a.ServerTick ? 0f : (float)((renderTick - a.ServerTick) / (b.ServerTick - a.ServerTick));
            t = GcMath.Clamp01(t);

            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if (i == LocalSlot)
                {
                    RenderPlayers[i] = LocalPlayer;
                    continue;
                }
                PlayerState pa = a.Players[i], pb = b.Players[i];
                PlayerState r = pb;
                // Never interpolate across a respawn teleport.
                if (pa.Alive && pb.Alive && (pb.Position - pa.Position).LengthSquared < 25f)
                {
                    r.Position = pa.Position + (pb.Position - pa.Position) * t;
                    r.Yaw = LerpAngle(pa.Yaw, pb.Yaw, t);
                    r.Pitch = pa.Pitch + (pb.Pitch - pa.Pitch) * t;
                }
                RenderPlayers[i] = r;
            }

            // Projectiles: extrapolate the newest snapshot ballistically (they are fast and short-lived).
            RenderProjectiles.Clear();
            SnapshotData newest = Newest();
            if (newest == null) return;
            float age = (float)((_serverClock - newest.ServerTick) / TickRate);
            foreach (var p in newest.Projectiles)
            {
                if (p.Owner == LocalSlot) continue; // the shooter renders its own predicted projectile
                var w = WeaponCatalog.Get(p.Weapon);
                var v = p;
                v.Position = ProjectileMath.PositionAt(p.Position, p.Velocity, w.GravityScale, Math.Max(0f, age));
                RenderProjectiles.Add(v);
            }
        }

        private SnapshotData Newest()
        {
            SnapshotData n = null;
            for (int i = 0; i < _snapshotCount; i++) if (n == null || _snapshots[i].ServerTick > n.ServerTick) n = _snapshots[i];
            return n;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + d * t;
        }

        // ───────────────────────────── receive ─────────────────────────────

        private void Pump()
        {
            while (_transport.Poll(out var ev))
            {
                switch (ev.Type)
                {
                    case TransportEventType.Disconnected:
                        HandleDisconnected();
                        break;
                    case TransportEventType.Reliable:
                        try { HandleReliable(ev.Payload); }
                        catch (FormatException) { }
                        break;
                    case TransportEventType.Unreliable:
                        try { HandleUnreliable(ev.Payload); }
                        catch (FormatException) { }
                        break;
                }
            }
        }

        private void HandleDisconnected()
        {
            if (Status == ClientStatus.Disconnected) return;
            Status = ClientStatus.Disconnected;
            Match = null;
            string reason = _disconnectReason ?? LastError ?? "Connection to server lost.";
            LastError = reason;
            OnDisconnected?.Invoke(reason);
        }

        private void HandleReliable(byte[] data)
        {
            var r = new NetReader(data);
            var type = (MsgType)r.Byte();
            switch (type)
            {
                case MsgType.Welcome:
                    LocalSlot = r.Byte();
                    _transport.SetToken(r.UInt());
                    TickRate = r.UShort();
                    Lobby.Code = r.String();
                    ServerName = r.String();
                    ServerDedicated = r.Bool();
                    Status = ClientStatus.Lobby;
                    _uw.Reset();
                    _uw.Byte((byte)MsgType.UdpHello);
                    _transport.SendUnreliable(_uw);
                    _lastPing = -10;
                    OnConnected?.Invoke();
                    break;

                case MsgType.Reject:
                    _disconnectReason = r.String();
                    LastError = _disconnectReason;
                    break;

                case MsgType.Kicked:
                    _disconnectReason = "You were kicked by the host.";
                    break;

                case MsgType.LobbyState:
                    Lobby = LobbyView.Read(ref r);
                    OnLobbyUpdated?.Invoke();
                    break;

                case MsgType.LobbyCountdown:
                    LobbyCountdown = r.Float();
                    OnLobbyUpdated?.Invoke();
                    break;

                case MsgType.ChatBroadcast:
                    OnChat?.Invoke(new ChatLine { Slot = r.Byte(), Name = r.String(), Text = r.String() });
                    break;

                case MsgType.MatchStart:
                    Match = new MatchInfo { MapId = r.Byte(), MatchSeed = r.Int(), MaxScore = r.Byte(), FriendlyFire = r.Bool(), Teams = r.Bool() };
                    int startTick = r.Int();
                    Match.Layout = new TileLayout(MapCatalog.Get(Match.MapId));
                    Tiles = new TileGridModel(Match.Layout);
                    Phase = MatchPhase.Loading;
                    PhaseStartTick = startTick;
                    _serverClock = startTick;
                    _latestSnapshotTick = -1;
                    _snapshotCount = 0;
                    _receivedSnapshot = false;
                    _sceneLoaded = false;
                    _pending.Clear();
                    LocalPlayer = new PlayerState { Slot = LocalSlot, GroundTile = -1 };
                    CorrectionOffset = Vec3.Zero;
                    Highlights.Clear();
                    Results.Clear();
                    Array.Clear(Scores, 0, Scores.Length);
                    LobbyCountdown = -1f;
                    Status = ClientStatus.Match;
                    OnMatchStart?.Invoke(Match);
                    break;

                case MsgType.TileState:
                    if (Tiles == null) break;
                    int n = r.UShort();
                    for (int i = 0; i < n && i < Tiles.TileCount; i++) Tiles.SetIntegrity(i, TileGridModel.Dequantize(r.UShort()));
                    break;

                case MsgType.TileDelta:
                    if (Tiles == null) break;
                    int count = r.UShort();
                    for (int i = 0; i < count; i++)
                    {
                        int id = r.UShort();
                        float integrity = TileGridModel.Dequantize(r.UShort());
                        if (id < Tiles.TileCount) Tiles.SetIntegrity(id, integrity);
                    }
                    break;

                case MsgType.ShatterTile:
                    var shatter = new ShatterEvent { TileId = r.Int(), ImpactPoint = r.Vec3(), Force = r.Float(), Instigator = r.Short() };
                    if (Tiles != null && shatter.TileId < Tiles.TileCount) Tiles.SetIntegrity(shatter.TileId, 0f);
                    OnShatter?.Invoke(shatter);
                    break;

                case MsgType.MatchPhase:
                    var phase = (MatchPhase)r.Byte();
                    PhaseStartTick = r.Int();
                    r.Int();
                    EndReason = (MatchEndReason)r.Byte();
                    SetPhase(phase);
                    break;

                case MsgType.WorldEvent:
                    var we = new WorldEvent { Type = (WorldEventType)r.Byte(), A = r.Int(), B = r.Int(), C = r.Int(), Point = r.Vec3(), Value = r.Float() };
                    OnWorldEvent?.Invoke(we);
                    break;

                case MsgType.Highlights:
                    Highlights.Clear();
                    int hc = r.Byte();
                    for (int i = 0; i < hc; i++)
                    {
                        var h = new Highlight { Slot = r.Byte(), Tick = r.Int(), Type = (HighlightType)r.Byte(), Count = r.Byte(), Score = r.Short() };
                        r.String();
                        Highlights.Add(h);
                    }
                    OnHighlights?.Invoke();
                    break;

                case MsgType.Results:
                    Results.Clear();
                    EndReason = (MatchEndReason)r.Byte();
                    int rc = r.Byte();
                    for (int i = 0; i < rc; i++)
                    {
                        Results.Add(new ResultRow
                        {
                            Slot = r.Byte(), Name = r.String(), Score = r.Short(), Knockouts = r.Short(), Deaths = r.Short(),
                            TilesShattered = r.Short(), Placement = r.Byte(), RatingDelta = r.Short(), NewRating = r.Int(),
                        });
                    }
                    Results.Sort((x, y) => x.Placement != y.Placement ? x.Placement.CompareTo(y.Placement) : y.Score.CompareTo(x.Score));
                    OnResults?.Invoke();
                    break;

                case MsgType.ReturnToLobby:
                    Status = ClientStatus.Lobby;
                    Match = null;
                    Tiles = null;
                    _sceneLoaded = false;
                    OnReturnToLobby?.Invoke();
                    break;
            }
        }

        private void SetPhase(MatchPhase phase)
        {
            if (phase == Phase) return;
            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private void HandleUnreliable(byte[] data)
        {
            var r = new NetReader(data);
            var type = (MsgType)r.Byte();
            switch (type)
            {
                case MsgType.Pong:
                    double sent = r.Double();
                    PingMs = (int)Math.Max(0, Math.Round((_now - sent) * 1000.0));
                    break;

                case MsgType.Snapshot:
                    if (Status != ClientStatus.Match || Tiles == null) break;
                    Codec.ReadSnapshot(ref r, _incoming);
                    if (_incoming.ServerTick <= _latestSnapshotTick) break; // stale / reordered
                    StoreSnapshot(_incoming);
                    break;

                case MsgType.Voice:
                    byte slot = r.Byte();
                    ushort seq = r.UShort();
                    OnVoice?.Invoke(slot, seq, r.Bytes(r.Remaining));
                    break;
            }
        }

        private void StoreSnapshot(SnapshotData s)
        {
            // Overwrite the oldest slot in the ring.
            SnapshotData slot;
            if (_snapshotCount < SnapshotBufferSize) slot = _snapshots[_snapshotCount++];
            else
            {
                slot = _snapshots[0];
                for (int i = 1; i < SnapshotBufferSize; i++) if (_snapshots[i].ServerTick < slot.ServerTick) slot = _snapshots[i];
            }
            slot.ServerTick = s.ServerTick;
            slot.AckSequence = s.AckSequence;
            slot.Phase = s.Phase;
            slot.PhaseStartTick = s.PhaseStartTick;
            Array.Copy(s.Players, slot.Players, s.Players.Length);
            Array.Copy(s.Scores, slot.Scores, s.Scores.Length);
            slot.Projectiles.Clear();
            slot.Projectiles.AddRange(s.Projectiles);

            bool first = _latestSnapshotTick < 0;
            _latestSnapshotTick = s.ServerTick;
            _latestSnapshotTime = _now;
            if (first) _serverClock = s.ServerTick;
            Array.Copy(s.Scores, Scores, Scores.Length);
            PhaseStartTick = s.PhaseStartTick;
            SetPhase(s.Phase);

            Reconcile(s);
        }

        /// <summary>
        /// Server reconciliation: adopt the authoritative state for the last input the server processed,
        /// replay every newer input through the same simulation, and turn any difference into a
        /// visual offset that decays over ~100 ms (smooth snap-back instead of a pop).
        /// </summary>
        private void Reconcile(SnapshotData s)
        {
            PlayerState server = s.Players[LocalSlot];
            if (!server.Active) return;

            int drop = 0;
            while (drop < _pending.Count && _pending[drop].Sequence <= s.AckSequence) drop++;
            if (drop > 0) _pending.RemoveRange(0, drop);

            Vec3 before = LocalPlayer.Position;
            bool wasAlive = LocalPlayer.Alive;
            bool combat = MatchClock.IsCombatPhase(s.Phase);
            float dt = 1f / TickRate;

            LocalPlayer = server;
            if (server.Alive)
            {
                for (int i = 0; i < _pending.Count; i++)
                    PlayerSimulation.Step(ref LocalPlayer, _pending[i], Tiles, dt, combat, combat);
            }

            if (!_receivedSnapshot || !wasAlive || !LocalPlayer.Alive)
            {
                CorrectionOffset = Vec3.Zero;
            }
            else
            {
                Vec3 error = before - LocalPlayer.Position;
                CorrectionOffset = error.Length > SnapCorrectionDistance ? Vec3.Zero : CorrectionOffset + error;
            }
            _receivedSnapshot = true;
        }

        private void Begin(MsgType type)
        {
            _w.Reset();
            _w.Byte((byte)type);
        }

        public void Dispose() => _transport.Dispose();
    }
}
