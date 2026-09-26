using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Glasscore.Net;
using Glasscore.Simulation;
using Xunit;

public class NetworkTests
{
    private sealed class Harness : IDisposable
    {
        public readonly GameServer Server;
        public readonly List<GameClient> Clients = new List<GameClient>();
        public double Time;
        private const double Dt = 1.0 / 60.0;

        public Harness(bool discovery = false, string code = null)
        {
            Server = new GameServer(new ServerConfig { Port = 0, EnableDiscovery = discovery, LobbyCode = code, ServerName = "Test" });
            Server.Start();
        }

        public GameClient Join(string name)
        {
            var c = new GameClient();
            c.ConnectAsync("127.0.0.1", Server.Port, name, "key-" + name, 1000, 0, 0).Wait(5000);
            Clients.Add(c);
            RunUntil(() => c.Status == ClientStatus.Lobby, 5);
            return c;
        }

        /// <summary>Advances simulated time in 1/60 s steps while real sockets deliver messages.</summary>
        public bool RunUntil(Func<bool> condition, double simulatedSeconds, bool realtimeSlack = true)
        {
            double end = Time + simulatedSeconds;
            int spins = 0;
            while (Time < end)
            {
                if (condition()) return true;
                Time += Dt;
                Server.Update(Time);
                foreach (var c in Clients) c.Update((float)Dt, Time);
                if (realtimeSlack && (++spins % 4 == 0)) Thread.Sleep(1);
            }
            return condition();
        }

        public void Dispose()
        {
            foreach (var c in Clients) c.Dispose();
            Server.Dispose();
        }
    }

    [Fact]
    public void LobbyReadyStartAndPredictedMovement()
    {
        using var h = new Harness();
        var host = h.Join("Alice");
        var guest = h.Join("Bob");
        var chat = new List<ChatLine>();
        guest.OnChat += chat.Add;

        Assert.True(h.RunUntil(() => host.Lobby.Roster.Count == 2 && guest.Lobby.Roster.Count == 2, 3));
        Assert.True(host.IsHost);
        Assert.False(guest.IsHost);

        // Host-only settings: the guest's request is ignored.
        guest.HostApplySettings(new LobbySettings { MapId = 2, MaxPlayers = 4, MaxScore = 5 });
        host.HostApplySettings(new LobbySettings { MapId = 1, MaxPlayers = 4, MaxScore = 7 });
        Assert.True(h.RunUntil(() => guest.Lobby.Settings.MapId == 1 && guest.Lobby.Settings.MaxScore == 7, 3));

        host.SendChat("<b>hallo</b>");
        Assert.True(h.RunUntil(() => chat.Any(c => c.Text == "hallo" && c.Name == "Alice"), 3));

        // Start requires everyone ready.
        host.SetReady(true);
        host.HostStart();
        h.RunUntil(() => false, 4);
        Assert.Equal(ClientStatus.Lobby, host.Status);

        guest.SetReady(true);
        h.RunUntil(() => guest.Lobby.Roster.All(r => r.Ready), 3);
        host.HostStart();
        Assert.True(h.RunUntil(() => host.Status == ClientStatus.Match && guest.Status == ClientStatus.Match, 6));
        Assert.Equal(1, host.Match.MapId);

        // Walk forward on the host; the guest idles.
        host.InputProvider = seq => new PlayerInput { MoveY = 1f, Yaw = host.LocalPlayer.Yaw, Pitch = 0f };
        guest.InputProvider = seq => PlayerInput.Neutral(seq, guest.LocalPlayer.Yaw, 0f);
        host.SendSceneLoaded();
        guest.SendSceneLoaded();

        Assert.True(h.RunUntil(() => h.Server.World.Clock.Phase == MatchPhase.Live, 10));
        Vec3 start = h.Server.World.Players[host.LocalSlot].Position;
        Assert.True(h.RunUntil(() => (h.Server.World.Players[host.LocalSlot].Position - start).Length > 3f, 5));

        // Prediction tracks the authoritative position closely (well under a metre).
        h.RunUntil(() => false, 0.5);
        var server = h.Server.World.Players[host.LocalSlot];
        Assert.True((server.Position - host.LocalPlayer.Position).Length < 1.5f,
            $"server {server.Position} predicted {host.LocalPlayer.Position}");

        // The guest sees the host through interpolation.
        Assert.True(guest.RenderPlayers[host.LocalSlot].Alive);
        Assert.True((guest.RenderPlayers[host.LocalSlot].Position - server.Position).Length < 3f);

        // Fire a cobble at the floor ahead and watch tile deltas / RPC_ShatterTile arrive.
        var shatters = new List<ShatterEvent>();
        guest.OnShatter += shatters.Add;
        // Stop walking and make sure the host stands on glass (it may have walked off an edge).
        host.InputProvider = seq => PlayerInput.Neutral(seq, host.LocalPlayer.Yaw, 0f);
        var world = h.Server.World;
        Assert.True(h.RunUntil(() => world.Players[host.LocalSlot].Alive && world.Players[host.LocalSlot].Grounded, 8));
        h.RunUntil(() => false, 0.3);

        // Switch once to the heavy cobble launcher, then aim at the nearest intact standard-glass tile.
        Vec3 eye = world.Players[host.LocalSlot].EyePosition;
        int target = -1;
        float best = float.MaxValue;
        for (int t = 0; t < world.Layout.Count; t++)
        {
            if (world.Layout.Types[t] != GlassType.Standard || !world.Tiles.IsSolid(t)) continue;
            Vec3 d = world.Layout.Centers[t] - eye;
            float horizontal = d.XZ.Length;
            if (d.Y > -0.5f || horizontal < 2.5f || horizontal > 9f) continue;
            if (horizontal < best) { best = horizontal; target = t; }
        }
        Assert.True(target >= 0, "no standard tile in range");
        Vec3 aim = world.Layout.Centers[target] - eye;
        float yaw = (float)(Math.Atan2(aim.X, aim.Z) * 180 / Math.PI);
        float pitch = (float)(Math.Atan2(-aim.Y, aim.XZ.Length) * 180 / Math.PI);
        uint switchSeq = 0;
        host.InputProvider = seq =>
        {
            if (switchSeq == 0) switchSeq = seq;
            var buttons = seq == switchSeq ? InputButtons.NextWeapon : (seq > switchSeq + 30 ? InputButtons.Fire : InputButtons.None);
            return new PlayerInput { Yaw = yaw, Pitch = pitch, Buttons = buttons };
        };
        Assert.True(h.RunUntil(() => shatters.Count > 0, 8));
        int tile = shatters[0].TileId;
        Assert.False(guest.Tiles.IsSolid(tile));
        Assert.False(h.Server.World.Tiles.IsSolid(tile));
    }

    [Fact]
    public void KickAndHostMigration()
    {
        using var h = new Harness();
        var a = h.Join("A");
        var b = h.Join("B");
        var c = h.Join("C");
        Assert.True(h.RunUntil(() => a.Lobby.Roster.Count == 3, 3));
        string reason = null;
        c.OnDisconnected += r => reason = r;
        a.HostKick(c.LocalSlot);
        Assert.True(h.RunUntil(() => c.Status == ClientStatus.Disconnected, 3));
        Assert.Contains("kicked", reason);

        a.Disconnect();
        Assert.True(h.RunUntil(() => b.IsHost, 5));
    }

    [Fact]
    public void DiscoveryFindsLobbyByCode()
    {
        using var h = new Harness(discovery: true, code: "GLAS55");
        var found = DiscoveryClient.SearchAsync("GLAS55", null, 800).Result;
        Assert.Contains(found, f => f.Code == "GLAS55" && f.GamePort == h.Server.Port);
        var none = DiscoveryClient.SearchAsync("ZZZZZZ", null, 300).Result;
        Assert.DoesNotContain(none, f => f.Code == "GLAS55");
    }

    [Fact]
    public void CodecRoundTrips()
    {
        var s = new SnapshotData { ServerTick = 1234, AckSequence = 99, Phase = MatchPhase.Cascade, PhaseStartTick = 1000 };
        s.Players[3] = new PlayerState { Slot = 3, Active = true, Alive = true, Position = new Vec3(1, 2, 3), Health = 55, Weapon = 2, GroundTile = 17, AnchorTime = 0.5f };
        s.Scores[3] = 4;
        s.Projectiles.Add(new ProjectileView { Id = 7, Owner = 3, Weapon = 1, Position = new Vec3(4, 5, 6), Velocity = new Vec3(0, 0, 40) });
        var w = new NetWriter();
        Codec.WriteSnapshot(w, s, 1 << 3);
        var r = new NetReader(w.ToArray());
        var back = new SnapshotData();
        Codec.ReadSnapshot(ref r, back);
        Assert.Equal(1234, back.ServerTick);
        Assert.Equal(99u, back.AckSequence);
        Assert.Equal(new Vec3(1, 2, 3), back.Players[3].Position);
        Assert.Equal(17, back.Players[3].GroundTile);
        Assert.True(back.Players[3].Anchored);
        Assert.Equal(4, back.Scores[3]);
        Assert.Single(back.Projectiles);
        Assert.Equal(0, r.Remaining);
    }
}
