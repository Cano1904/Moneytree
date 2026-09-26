using System;
using System.Collections.Generic;
using System.Linq;
using Glasscore.Simulation;
using Xunit;

public class DeterminismTests
{
    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var a = new DeterministicRandom(42);
        var b = new DeterministicRandom(42);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void NextFloatStaysInUnitInterval()
    {
        var r = new DeterministicRandom(7);
        for (int i = 0; i < 100000; i++)
        {
            float f = r.NextFloat();
            Assert.InRange(f, 0f, 0.99999999f);
        }
    }

    [Fact]
    public void FractureSeedIgnoresSubMillimetreNoise()
    {
        var p = new Vec3(1.2345f, 0.05f, -3.2101f);
        var q = new Vec3(1.2345f + 0.0001f, 0.05f, -3.2101f - 0.0001f);
        Assert.Equal(DeterministicHash.FractureSeed(99, 12, p), DeterministicHash.FractureSeed(99, 12, q));
        Assert.NotEqual(DeterministicHash.FractureSeed(99, 12, p), DeterministicHash.FractureSeed(99, 13, p));
    }
}

public class VoronoiTests
{
    [Theory]
    [InlineData(20, 0.2f)]
    [InlineData(112, 1f)]
    public void CellsTileTheWholePane(int count, float force)
    {
        var sites = VoronoiFracture2D.GenerateSites(1234, new Vec2(0.3f, -0.4f), 1f, 1f, count, force);
        Assert.True(sites.Length >= count * 0.9);
        var cells = VoronoiFracture2D.ComputeCells(sites, 1f, 1f);
        float area = cells.Sum(c => c.Area);
        Assert.InRange(area, 3.999f, 4.001f);
        foreach (var c in cells) Assert.True(VoronoiFracture2D.ContainsPoint(c.Polygon, c.Site));
    }

    [Fact]
    public void SameSeedSameFracture()
    {
        var a = VoronoiFracture2D.GenerateSites(5, Vec2.Zero, 1f, 1f, 50, 0.5f);
        var b = VoronoiFracture2D.GenerateSites(5, Vec2.Zero, 1f, 1f, 50, 0.5f);
        Assert.Equal(a, b);
        var c = VoronoiFracture2D.GenerateSites(6, Vec2.Zero, 1f, 1f, 50, 0.5f);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void PrismHasCapsAndWalls()
    {
        var poly = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) };
        var mesh = PrismMeshBuilder.Build(poly, 0.1f, new Vec2(0.5f, 0.5f), 1f);
        // 2 caps × 2 triangles + 4 walls × 2 triangles = 12 triangles
        Assert.Equal(36, mesh.Triangles.Length);
        Assert.Equal(mesh.Vertices.Length, mesh.Normals.Length);
    }
}

public class GlassTests
{
    private static TileGridModel Grid() => new TileGridModel(new TileLayout(MapCatalog.Greenhouse));

    private static int FindTile(TileGridModel g, GlassType type, bool wantStandardNeighbours = false)
    {
        var buf = new int[4];
        for (int i = 0; i < g.TileCount; i++)
        {
            if (g.GetGlassType(i) != type) continue;
            if (!wantStandardNeighbours) return i;
            int n = g.GetNeighbors(i, buf);
            if (n == 4 && Enumerable.Range(0, 4).All(k => g.GetGlassType(buf[k]) == GlassType.Standard)) return i;
        }
        throw new InvalidOperationException("no tile");
    }

    [Fact]
    public void HeavyProjectileInstantlyShattersStandardGlass()
    {
        var g = Grid();
        int t = FindTile(g, GlassType.Standard);
        var ev = new TileDamageResolver().ApplyImpact(g, t, 1f, heavy: true, DamageSource.Projectile);
        Assert.True(ev[0].Shattered);
        Assert.Equal(0f, g.GetIntegrity(t));
    }

    [Fact]
    public void ReinforcedGlassAbsorbsNinetyPercent()
    {
        var g = Grid();
        int t = FindTile(g, GlassType.Reinforced);
        new TileDamageResolver().ApplyImpact(g, t, 100f, heavy: true, DamageSource.Projectile);
        Assert.Equal(290f, g.GetIntegrity(t), 3);
    }

    [Fact]
    public void TemperedGlassSpiderwebsBelowThirty()
    {
        Assert.Equal(TileVisualState.Intact, GlassCatalog.VisualState(GlassType.Tempered, 100f));
        Assert.Equal(TileVisualState.Cracked, GlassCatalog.VisualState(GlassType.Tempered, 45f));
        Assert.Equal(TileVisualState.Spiderweb, GlassCatalog.VisualState(GlassType.Tempered, 29f));
        Assert.Equal(TileVisualState.Destroyed, GlassCatalog.VisualState(GlassType.Tempered, 0f));
        var g = Grid();
        int t = FindTile(g, GlassType.Tempered);
        var ev = new TileDamageResolver().ApplyImpact(g, t, 60f, heavy: true, DamageSource.Projectile);
        Assert.False(ev[0].Shattered); // heavy does not instantly break tempered glass
    }

    [Fact]
    public void StandardGlassPassesHalfTheEnergyToNeighbours()
    {
        var g = Grid();
        int t = FindTile(g, GlassType.Standard, wantStandardNeighbours: true);
        var events = new TileDamageResolver().ApplyImpact(g, t, 40f, heavy: false, DamageSource.Projectile);
        Assert.Equal(10f, g.GetIntegrity(t), 3);
        var firstRing = events.Where(e => e.Depth == 1).ToList();
        Assert.Equal(4, firstRing.Count);
        foreach (var e in firstRing) Assert.Equal(5f, e.Applied, 3); // 40 * 0.5 / 4
        Assert.All(events, e => Assert.True(e.Depth <= GlassCatalog.MaxCascadeDepth));
    }
}

public class RecoilTests
{
    [Fact]
    public void FiringDownMidAirLaunchesUp()
    {
        var r = RecoilModel.Compute(960f, new Vec3(0, -1, 0), grounded: false, anchored: false);
        Assert.Equal(12f, r.VelocityDelta.Y, 3);
        Assert.Equal(0f, r.CompressionImpulse);
    }

    [Fact]
    public void FiringUpWhileGroundedCompressesTheTile()
    {
        var r = RecoilModel.Compute(480f, new Vec3(0, 1, 0), grounded: true, anchored: false);
        Assert.Equal(0f, r.VelocityDelta.Y, 3);
        Assert.Equal(480f, r.CompressionImpulse, 3);
        Assert.Equal(0f, RecoilModel.CompressionTileDamage(480f, tileWeakened: false, anchored: false));
        Assert.Equal(19.2f, RecoilModel.CompressionTileDamage(480f, tileWeakened: true, anchored: false), 3);
    }

    [Fact]
    public void AnchorFreezesPlayerAndDoublesLoad()
    {
        var r = RecoilModel.Compute(480f, new Vec3(0, 0, 1), grounded: true, anchored: true);
        Assert.Equal(Vec3.Zero, r.VelocityDelta);
        Assert.Equal(960f, r.CompressionImpulse, 3);
        Assert.Equal(2f * RecoilModel.WeightLoadDamage(1f, true, false), RecoilModel.WeightLoadDamage(1f, true, true), 3);
    }

    [Fact]
    public void VaultingOffFragileGlassDamagesIt()
    {
        Assert.Equal(10f, RecoilModel.JumpVaultDamage(GlassType.Standard, 50f));
        Assert.Equal(0f, RecoilModel.JumpVaultDamage(GlassType.Reinforced, 300f));
        Assert.Equal(10f, RecoilModel.JumpVaultDamage(GlassType.Reinforced, 100f));
    }
}

public class MapTests
{
    [Fact]
    public void EveryMapHasEightSpawnPadsAndValidLayout()
    {
        foreach (var map in MapCatalog.All)
        {
            var layout = new TileLayout(map);
            Assert.Equal(8, layout.SpawnPadCount);
            Assert.InRange(layout.Count, 50, TileLayout.MaxTiles);
        }
    }
}

public class MovementTests
{
    private static (GameWorld world, int tile) WorldWithPlayer()
    {
        var world = new GameWorld(new MatchConfig { MapId = 0, MatchSeed = 1 });
        world.AddPlayer(0, 0);
        world.Spawn(0, 0f);
        return (world, world.Players[0].GroundTile);
    }

    [Fact]
    public void StandingPlayerStaysGrounded()
    {
        var (w, tile) = WorldWithPlayer();
        var p = w.Players[0];
        float y = p.Position.Y;
        for (int i = 0; i < 120; i++) PlayerSimulation.Step(ref p, default, w, w.DeltaTime, true, true);
        Assert.True(p.Grounded);
        Assert.Equal(tile, p.GroundTile);
        Assert.Equal(y, p.Position.Y, 2);
    }

    [Fact]
    public void WalkingReachesWalkSpeed()
    {
        var (w, _) = WorldWithPlayer();
        var p = w.Players[0];
        var input = new PlayerInput { MoveY = 1f, Yaw = p.Yaw };
        for (int i = 0; i < 30; i++) PlayerSimulation.Step(ref p, input, w, w.DeltaTime, true, true);
        float speed = new Vec2(p.Velocity.X, p.Velocity.Z).Length;
        Assert.InRange(speed, PlayerRules.WalkSpeed * 0.9f, PlayerRules.WalkSpeed * 1.01f);
    }

    [Fact]
    public void FallingFastNeverTunnelsThroughGlass()
    {
        var (w, tile) = WorldWithPlayer();
        var p = w.Players[0];
        p.Position = p.Position + new Vec3(0, 30f, 0);
        p.Velocity = new Vec3(0, -40f, 0);
        p.Grounded = false;
        for (int i = 0; i < 180; i++) PlayerSimulation.Step(ref p, default, w, w.DeltaTime, true, true);
        Assert.True(p.Grounded);
        Assert.True(p.Position.Y > w.Layout.Centers[tile].Y);
    }

    [Fact]
    public void RocketJumpFromTheGround()
    {
        var (w, _) = WorldWithPlayer();
        var p = w.Players[0];
        p.Weapon = WeaponCatalog.Findling.Id;
        p.FireCooldown = 0f;
        var fire = new PlayerInput { Pitch = 89f, Yaw = p.Yaw, Buttons = InputButtons.Fire };
        var ev = PlayerSimulation.Step(ref p, fire, w, w.DeltaTime, true, true);
        Assert.True(ev.Fired);
        Assert.False(p.Grounded);
        Assert.True(p.Velocity.Y > 10f);
    }

    [Fact]
    public void AnchorOnlyWhenGroundedAndBlocksMovement()
    {
        var (w, _) = WorldWithPlayer();
        var p = w.Players[0];
        var anchor = new PlayerInput { Buttons = InputButtons.Anchor, MoveY = 1f };
        var ev = PlayerSimulation.Step(ref p, anchor, w, w.DeltaTime, true, true);
        Assert.True(ev.AnchorStarted);
        Vec3 pos = p.Position;
        var push = new PlayerInput { MoveY = 1f, Buttons = InputButtons.Anchor };
        for (int i = 0; i < 60; i++) PlayerSimulation.Step(ref p, push, w, w.DeltaTime, true, true);
        Assert.Equal(pos, p.Position);
        for (int i = 0; i < 60; i++) PlayerSimulation.Step(ref p, push, w, w.DeltaTime, true, true);
        Assert.False(p.Anchored); // 1.5 s elapsed
        Assert.True(p.AnchorCooldown > 0f);
    }
}

public class MatchTests
{
    private static GameWorld TwoPlayerWorld(int maxScore = 10)
    {
        var w = new GameWorld(new MatchConfig { MapId = 1, MatchSeed = 77, MaxScore = maxScore });
        w.AddPlayer(0, 0);
        w.AddPlayer(1, 1);
        w.Loaded[0] = w.Loaded[1] = true;
        return w;
    }

    [Fact]
    public void FullTimelineWithTieGoesThroughOvertimeToLobby()
    {
        var w = TwoPlayerWorld();
        var inputs = new PlayerInput[GameWorld.MaxPlayers];
        var phases = new List<MatchPhase> { w.Clock.Phase };
        bool pressure = false, warning = false;
        int waves = 0;
        for (int i = 0; i < 40000 && w.Clock.Phase != MatchPhase.ReturnToLobby; i++)
        {
            w.Step(inputs);
            foreach (var e in w.Events)
            {
                pressure |= e.Type == WorldEventType.PressureWave;
                warning |= e.Type == WorldEventType.CascadeWarning;
                if (e.Type == WorldEventType.ErosionWave) waves++;
            }
            if (phases[phases.Count - 1] != w.Clock.Phase) phases.Add(w.Clock.Phase);
        }
        Assert.Equal(new[]
        {
            MatchPhase.Loading, MatchPhase.Countdown, MatchPhase.Live, MatchPhase.Cascade, MatchPhase.Overtime,
            MatchPhase.MatchEnd, MatchPhase.Highlights, MatchPhase.Results, MatchPhase.ReturnToLobby,
        }, phases);
        Assert.True(pressure);
        Assert.True(warning);
        Assert.Equal(MatchTimings.ErosionWaveCount, waves);
        Assert.Equal(MatchEndReason.OvertimeExpired, w.Clock.EndReason);

        // Only the safe core survives the cascade.
        for (int t = 0; t < w.Layout.Count; t++)
            if (w.Tiles.IsSolid(t)) Assert.True(w.Layout.RadialDistance(t) <= MatchTimings.ErosionSafeRadius + 0.01f);
        Assert.Contains(Enumerable.Range(0, w.Layout.Count), t => w.Tiles.IsSolid(t));
    }

    [Fact]
    public void ScoreLimitEndsTheMatch()
    {
        var w = TwoPlayerWorld(maxScore: 5);
        var inputs = new PlayerInput[GameWorld.MaxPlayers];
        while (w.Clock.Phase != MatchPhase.Live) w.Step(inputs);
        for (int k = 0; k < 5; k++)
        {
            while (!w.Players[1].Alive || w.Players[1].SpawnProtection > 0f) w.Step(inputs);
            w.DamagePlayer(1, 0, 200f, Vec3.Zero, KnockoutCause.Damage, Vec3.Zero);
        }
        w.Step(inputs);
        Assert.Equal(5, w.Stats[0].Score);
        Assert.Equal(MatchPhase.MatchEnd, w.Clock.Phase);
        Assert.Equal(MatchEndReason.ScoreLimit, w.Clock.EndReason);
        Assert.Equal(1, w.ComputePlacements()[0]);
        Assert.Equal(2, w.ComputePlacements()[1]);
    }

    [Fact]
    public void FallingWithoutAttackerCostsAPoint()
    {
        var w = TwoPlayerWorld();
        var inputs = new PlayerInput[GameWorld.MaxPlayers];
        while (w.Clock.Phase != MatchPhase.Live) w.Step(inputs);
        w.Stats[0].Score = 2;
        w.Players[0].Position = new Vec3(0, -100, 0);
        w.Step(inputs);
        Assert.Equal(1, w.Stats[0].Score);
        Assert.False(w.Players[0].Alive);
    }

    [Fact]
    public void ProjectileHitsDamageGlass()
    {
        var w = TwoPlayerWorld();
        var inputs = new PlayerInput[GameWorld.MaxPlayers];
        while (w.Clock.Phase != MatchPhase.Live) w.Step(inputs);
        var p = w.Players[0];
        inputs[0] = new PlayerInput { Sequence = 1, Yaw = p.Yaw, Pitch = 60f, Buttons = InputButtons.Fire };
        bool impact = false;
        for (int i = 0; i < 60 && !impact; i++)
        {
            w.Step(inputs);
            impact = w.Events.Any(e => e.Type == WorldEventType.ProjectileImpact && e.A >= 0);
        }
        Assert.True(impact);
        Assert.Contains(Enumerable.Range(0, w.Layout.Count), t => w.Tiles.GetIntegrity(t) < w.Layout.MaxIntegrity(t));
    }

    [Fact]
    public void AttributionWindowExpires()
    {
        var k = new KillAttribution(360);
        k.RecordHit(1, 0, 100);
        Assert.Equal(0, k.ResolveKnockout(1, 400));
        k.RecordHit(1, 0, 100);
        Assert.Equal(KillAttribution.NoAttacker, k.ResolveKnockout(1, 500));
    }
}

public class MetaTests
{
    [Fact]
    public void EloIsZeroSumAndPenalisesLeavers()
    {
        var entries = new List<RatingEntry>
        {
            new RatingEntry { PlayerId = 0, Rating = 1000, GamesPlayed = 30, Placement = 1 },
            new RatingEntry { PlayerId = 1, Rating = 1000, GamesPlayed = 30, Placement = 2 },
        };
        var d = RatingSystem.ComputeDeltas(entries, competitive: false);
        Assert.Equal(12, d[0]);
        Assert.Equal(-12, d[1]);
        entries[0] = new RatingEntry { PlayerId = 0, Rating = 1000, GamesPlayed = 30, Placement = 1, LeftEarly = true };
        var c = RatingSystem.ComputeDeltas(entries, competitive: true);
        Assert.Equal(-12 - RatingSystem.LeavePenalty, c[0]);
    }

    [Fact]
    public void MatchmakingWindowWidens()
    {
        Assert.Equal(100, MatchmakingPolicy.WindowAt(0));
        Assert.Equal(150, MatchmakingPolicy.WindowAt(5));
        Assert.Equal(600, MatchmakingPolicy.WindowAt(59));
        Assert.Equal(int.MaxValue, MatchmakingPolicy.WindowAt(60));
        Assert.Equal(new List<int> { 5, 4, 6 }, MatchmakingPolicy.CandidateBuckets(1050, 150));
    }

    [Fact]
    public void LobbyCodesAreValidAndNormalised()
    {
        var r = new DeterministicRandom(3);
        for (int i = 0; i < 100; i++) Assert.True(LobbyCode.IsValid(LobbyCode.Generate(ref r)));
        Assert.Equal("AB3K9Z", LobbyCode.Normalize(" ab3-k9z "));
        Assert.False(LobbyCode.IsValid("AB3K9"));
        Assert.False(LobbyCode.IsValid("AB3KO0"));
    }

    [Fact]
    public void LobbyRulesEnforceReadyAndLimits()
    {
        Assert.False(LobbyRules.CanStart(new[] { true }));
        Assert.False(LobbyRules.CanStart(new[] { true, false }));
        Assert.True(LobbyRules.CanStart(new[] { true, true }));
        Assert.Equal(5, LobbyRules.ClampPlayerLimit(3, 5));
        Assert.Equal(8, LobbyRules.ClampPlayerLimit(12, 2));
        Assert.Equal(7, LobbyRules.NextHost(new List<(int, int)> { (3, 9), (7, 2), (1, 5) }));
    }

    [Fact]
    public void ChatIsSanitisedAndRateLimited()
    {
        var f = new ChatFilter(new[] { "badword" });
        Assert.Equal("hi b there", f.Process(0, "  <b>hi</b>   b\tthere ", 0));
        Assert.Null(f.Process(0, "spam", 0.1));
        Assert.Equal("*******", f.Process(0, "badword", 1.0));
        Assert.Equal(ChatFilter.MaxLength, f.Clean(new string('x', 500)).Length);
    }

    [Fact]
    public void HighlightsDetectMultiKnockoutAndGlassHouse()
    {
        var h = new HighlightTracker(60);
        h.OnKnockout(0, 1, 100, KnockoutCause.Damage, 10f, false);
        h.OnKnockout(0, 2, 160, KnockoutCause.Damage, 10f, false);
        h.OnKnockout(0, 3, 200, KnockoutCause.Damage, 50f, true);
        h.OnOwnFloorBroken(4, 300);
        h.OnKnockout(-1, 4, 400, KnockoutCause.Fall, 0f, false);
        var types = h.All.Select(x => x.Type).ToList();
        Assert.Single(types, HighlightType.MultiKnockout);
        Assert.Contains(HighlightType.GlassHouse, types);
        Assert.Contains(HighlightType.LongShot, types);
        Assert.Contains(HighlightType.AirStrike, types);
        Assert.Equal(3, h.All.First(x => x.Type == HighlightType.MultiKnockout).Count);
    }
}
