using System;
using System.Linq;
using Glasscore.Net;
using Glasscore.Simulation;
using Xunit;
using Xunit.Abstractions;

public class BotTests
{
    private readonly ITestOutputHelper _out;
    public BotTests(ITestOutputHelper output) { _out = output; }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BotsPlayAFullMatchOnEveryMap(int map)
    {
        var world = new GameWorld(new MatchConfig { MapId = map, MatchSeed = 1234 + map, MaxScore = 25 });
        var brains = new BotBrain[4];
        for (int i = 0; i < 4; i++)
        {
            world.AddPlayer(i, (byte)i);
            world.Loaded[i] = true;
            brains[i] = new BotBrain(i, 99, 0.55f);
        }
        var inputs = new PlayerInput[GameWorld.MaxPlayers];
        int ticks = 0;
        while (world.Clock.Phase != MatchPhase.MatchEnd && ticks < 60 * 60 * 10)
        {
            for (int i = 0; i < 4; i++) inputs[i] = brains[i].Think(world, world.DeltaTime);
            world.Step(inputs);
            ticks++;
        }
        int kos = world.Stats.Take(4).Sum(s => s.Knockouts);
        int self = world.Stats.Take(4).Sum(s => s.SelfKnockouts);
        int tiles = world.Stats.Take(4).Sum(s => s.TilesShattered);
        _out.WriteLine($"map {map}: {ticks / 60}s, KOs {kos}, self {self}, tiles {tiles}, end {world.Clock.EndReason}");
        Assert.Equal(MatchPhase.MatchEnd, world.Clock.Phase);
        Assert.True(kos >= 6, $"bots scored only {kos} knockouts");
        Assert.True(self < kos, $"bots fell on their own too often ({self} self vs {kos} KOs)");
    }

    [Fact]
    public void HostCanAddBotsAndStartAlone()
    {
        using var server = new GameServer(new ServerConfig { Port = 0, EnableDiscovery = false });
        server.Start();
        var c = new GameClient();
        c.ConnectAsync("127.0.0.1", server.Port, "Solo", "solo", 1000, 0, 0).Wait(5000);
        double t = 0;
        bool Run(Func<bool> cond, double seconds)
        {
            double end = t + seconds; int spins = 0;
            while (t < end)
            {
                if (cond()) return true;
                t += 1 / 60.0; server.Update(t); c.Update(1 / 60f, t);
                if (++spins % 4 == 0) System.Threading.Thread.Sleep(1);
            }
            return cond();
        }
        Assert.True(Run(() => c.Status == ClientStatus.Lobby, 5));
        c.HostAddBot();
        c.HostAddBot();
        Assert.True(Run(() => c.Lobby.Roster.Count == 3, 3));
        c.SetReady(true);
        Assert.True(Run(() => c.Lobby.Roster.All(r => r.Ready), 3));
        c.HostStart();
        Assert.True(Run(() => c.Status == ClientStatus.Match, 6));
        c.InputProvider = seq => PlayerInput.Neutral(seq, 0, 0);
        c.SendSceneLoaded();
        Assert.True(Run(() => server.World != null && server.World.Clock.Phase == MatchPhase.Live, 10));
        Assert.True(Run(() => server.World.Projectiles.Count > 0, 10), "bots never fired");
    }
}
