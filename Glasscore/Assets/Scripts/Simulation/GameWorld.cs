using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    public enum KnockoutCause : byte
    {
        Damage,
        Fall,
        Shards,
    }

    public enum WorldEventType : byte
    {
        ShatterTile,       // → RPC_ShatterTile(tileId, impactPoint, force)
        Knockout,
        PlayerHit,
        ProjectileImpact,
        Respawn,
        AnchorStarted,
        PressureWave,
        CascadeWarning,
        ErosionWave,
    }

    public struct WorldEvent
    {
        public WorldEventType Type;
        public int A;          // tile id / victim slot / respawned slot
        public int B;          // attacker slot / wave index / weapon
        public int C;          // knockout cause / tile id for impacts
        public Vec3 Point;
        public float Value;    // force / damage
    }

    public sealed class MatchConfig
    {
        public int MapId;
        public int MatchSeed;
        public int MaxScore = MatchTimings.DefaultMaxScore;
        public bool FriendlyFire;
        public bool Teams;
        public bool Competitive;
    }

    public struct PlayerStats
    {
        public int Score;
        public int Knockouts;
        public int Deaths;
        public int SelfKnockouts;
        public int TilesShattered;
        public float DamageDealt;
    }

    public sealed class Projectile
    {
        public int Id;
        public byte Owner;
        public byte Weapon;
        public Vec3 Origin;
        public Vec3 Velocity;
        public Vec3 Position;
        public float Age;
        public float DamageScale = 1f;
        public int LagTicks;
        public bool FiredAirborne;
        public int PierceTile = -1;

        public Vec3 CurrentVelocity => Velocity + new Vec3(0f, -PlayerRules.Gravity * WeaponCatalog.Get(Weapon).GravityScale * Age, 0f);
    }

    /// <summary>
    /// The authoritative match simulation. Runs on the dedicated server (or the listen-server thread of
    /// a hosting client) at 60 Hz. Everything here is engine-agnostic so it can be unit tested and run
    /// in the headless .NET dedicated server.
    /// </summary>
    public sealed class GameWorld : ITileQuery
    {
        public const int MaxPlayers = 8;
        public const int HistoryLength = 64;
        public const int MaxLagCompensationTicks = 20;
        public const float ProjectileRadius = 0.15f;

        public readonly MatchConfig Config;
        public readonly TileLayout Layout;
        public readonly TileGridModel Tiles;
        public readonly MatchClock Clock;
        public readonly PlayerState[] Players = new PlayerState[MaxPlayers];
        public readonly PlayerStats[] Stats = new PlayerStats[MaxPlayers];
        public readonly bool[] Loaded = new bool[MaxPlayers];
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<WorldEvent> Events = new List<WorldEvent>();
        public readonly List<MatchEvent> MatchEvents = new List<MatchEvent>();
        public readonly HighlightTracker Highlights;
        public readonly float DeltaTime;
        public readonly float KillPlaneY;

        public int Tick { get; private set; }

        private readonly TileDamageResolver _resolver = new TileDamageResolver();
        private readonly KillAttribution _attribution;
        private readonly bool[] _destabilized;
        private readonly float _erosionMaxRadius;
        private readonly List<int> _waveTiles = new List<int>();
        private readonly float[] _respawnTimer = new float[MaxPlayers];
        private readonly int[] _lagTicks = new int[MaxPlayers];
        private readonly Vec3[,] _history = new Vec3[MaxPlayers, HistoryLength];
        private readonly bool[,] _historyAlive = new bool[MaxPlayers, HistoryLength];
        private readonly (float distance, bool airborne)[] _lastShotInfo = new (float, bool)[MaxPlayers];
        private readonly List<Vec3> _enemyScratch = new List<Vec3>(MaxPlayers);
        private int _nextProjectileId = 1;

        public GameWorld(MatchConfig config, int tickRate = MatchTimings.TickRate)
        {
            Config = config;
            Layout = new TileLayout(MapCatalog.Get(config.MapId));
            Tiles = new TileGridModel(Layout);
            Clock = new MatchClock(0, tickRate);
            DeltaTime = 1f / tickRate;
            Highlights = new HighlightTracker(tickRate);
            _attribution = new KillAttribution(MatchTimings.ToTicks(MatchTimings.AttributionWindow, tickRate));
            _destabilized = new bool[Layout.Count];
            _erosionMaxRadius = ErosionPlanner.MaxRadius(Layout);
            KillPlaneY = -PlayerRules.KillPlaneBelowLowestLayer;
            for (int i = 0; i < MaxPlayers; i++) Players[i].Slot = (byte)i;
        }

        TileLayout ITileQuery.Layout => Layout;
        public bool IsSolid(int tileId) => Tiles.IsSolid(tileId);

        public bool IsDestabilized(int tileId) => _destabilized[tileId];

        // ───────────────────────────── roster ─────────────────────────────

        /// <param name="keepStats">true when a disconnected player reconnects mid-match.</param>
        public void AddPlayer(int slot, byte team, bool keepStats = false)
        {
            Players[slot] = new PlayerState { Slot = (byte)slot, Active = true, Alive = false, Team = team, GroundTile = -1, Health = PlayerRules.MaxHealth };
            if (!keepStats) Stats[slot] = default;
            // Joining mid-match respawns after the normal delay; at match start SpawnAll handles it.
            _respawnTimer[slot] = keepStats ? MatchTimings.Respawn : 0f;
        }

        public void RemovePlayer(int slot)
        {
            Players[slot].Active = false;
            Players[slot].Alive = false;
            Loaded[slot] = false;
            _attribution.Forget(slot);
        }

        public void SetLatency(int slot, float rttSeconds)
        {
            // Favor-the-shooter: rewind targets by one-way latency + the 100 ms interpolation delay.
            _lagTicks[slot] = GcMath.Clamp((int)Math.Round((rttSeconds * 0.5f + 0.1f) / DeltaTime), 0, MaxLagCompensationTicks);
        }

        public int ActivePlayerCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxPlayers; i++) if (Players[i].Active) n++;
                return n;
            }
        }

        public bool AllLoaded
        {
            get
            {
                for (int i = 0; i < MaxPlayers; i++) if (Players[i].Active && !Loaded[i]) return false;
                return true;
            }
        }

        public int TeamScore(int team)
        {
            int s = 0;
            for (int i = 0; i < MaxPlayers; i++) if (Players[i].Active && Players[i].Team == team) s += Stats[i].Score;
            return s;
        }

        public void GetLeader(out int leaderScore, out bool tied)
        {
            leaderScore = int.MinValue;
            tied = false;
            if (Config.Teams)
            {
                int a = TeamScore(0), b = TeamScore(1);
                leaderScore = Math.Max(a, b);
                tied = a == b;
                return;
            }
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (!Players[i].Active) continue;
                int s = Stats[i].Score;
                if (s > leaderScore) { leaderScore = s; tied = false; }
                else if (s == leaderScore) tied = true;
            }
            if (leaderScore == int.MinValue) leaderScore = 0;
        }

        // ───────────────────────────── tick ─────────────────────────────

        /// <summary>Advances the world by one tick. inputs[slot] is that player's input for this tick.</summary>
        public void Step(PlayerInput[] inputs)
        {
            Tick++;
            Events.Clear();
            MatchEvents.Clear();
            Tiles.ClearDirty();

            GetLeader(out int leader, out bool tied);
            Clock.Advance(Tick, new MatchInputs
            {
                ConnectedPlayers = ActivePlayerCount,
                AllPlayersLoaded = AllLoaded,
                LeaderScore = leader,
                LeaderTied = tied,
                MaxScore = Config.MaxScore,
            }, MatchEvents);

            foreach (var me in MatchEvents) HandleMatchEvent(me);

            MatchPhase phase = Clock.Phase;
            bool combat = MatchClock.IsCombatPhase(phase);

            if (phase == MatchPhase.Cascade || phase == MatchPhase.Overtime) StepErosion();

            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (!Players[slot].Active) continue;
                StepPlayer(slot, inputs[slot], combat);
            }

            StepProjectiles(combat);
            StepRespawns(phase);
            RecordHistory();
        }

        private void HandleMatchEvent(MatchEvent me)
        {
            switch (me.Type)
            {
                case MatchEventType.PhaseChanged:
                    if (me.Phase == MatchPhase.Countdown) SpawnAll();
                    if (me.Phase == MatchPhase.MatchEnd) Projectiles.Clear();
                    break;
                case MatchEventType.PressureWave:
                    for (int i = 0; i < Layout.Count; i++)
                    {
                        if (Layout.Types[i] != GlassType.Standard) continue;
                        ApplyDirectAndCheck(i, MatchTimings.PressureWaveDamage, DamageSource.PressureWave, -1, 0.2f);
                    }
                    Events.Add(new WorldEvent { Type = WorldEventType.PressureWave });
                    break;
                case MatchEventType.CascadeWarning:
                    Events.Add(new WorldEvent { Type = WorldEventType.CascadeWarning });
                    break;
                case MatchEventType.ErosionWave:
                    int n = ErosionPlanner.CollectWave(Layout, Tiles, _destabilized, _erosionMaxRadius, me.WaveIndex, _waveTiles);
                    Events.Add(new WorldEvent { Type = WorldEventType.ErosionWave, B = me.WaveIndex, A = n });
                    break;
            }
        }

        private void StepErosion()
        {
            for (int i = 0; i < Layout.Count; i++)
            {
                if (!_destabilized[i] || !Tiles.IsSolid(i)) continue;
                float drain = ErosionPlanner.DrainPerTick(Layout.MaxIntegrity(i), DeltaTime);
                ApplyDirectAndCheck(i, drain, DamageSource.Erosion, -1, 0.3f);
            }
        }

        private void StepPlayer(int slot, PlayerInput input, bool combat)
        {
            ref PlayerState p = ref Players[slot];
            bool wasGrounded = p.Grounded;
            PlayerStepEvents ev = PlayerSimulation.Step(ref p, input, this, DeltaTime, combat, combat);
            if (!p.Alive) return;

            if (ev.AnchorStarted) Events.Add(new WorldEvent { Type = WorldEventType.AnchorStarted, A = slot });

            if (ev.Jumped && ev.JumpTile >= 0)
            {
                float dmg = RecoilModel.JumpVaultDamage(Layout.Types[ev.JumpTile], Tiles.GetIntegrity(ev.JumpTile));
                if (ApplyDirectAndCheck(ev.JumpTile, dmg, DamageSource.JumpVault, slot, 0.25f)) Highlights.OnOwnFloorBroken(slot, Tick);
            }

            if (ev.Fired)
            {
                SpawnProjectiles(slot, ev, !wasGrounded);
                if (ev.CompressionTile >= 0)
                {
                    int t = ev.CompressionTile;
                    bool weakened = GlassCatalog.IsWeakened(Layout.Types[t], Tiles.GetIntegrity(t));
                    float dmg = RecoilModel.CompressionTileDamage(ev.CompressionImpulse, weakened, p.Anchored);
                    if (ApplyDirectAndCheck(t, dmg, DamageSource.Compression, slot, 0.5f)) Highlights.OnOwnFloorBroken(slot, Tick);
                }
            }

            if (combat && p.Grounded && p.GroundTile >= 0)
            {
                int t = p.GroundTile;
                bool weakened = GlassCatalog.IsWeakened(Layout.Types[t], Tiles.GetIntegrity(t));
                float load = RecoilModel.WeightLoadDamage(DeltaTime, weakened, p.Anchored);
                if (load > 0f && ApplyDirectAndCheck(t, load, DamageSource.WeightLoad, slot, 0.2f)) Highlights.OnOwnFloorBroken(slot, Tick);
            }

            if (p.Position.Y < KillPlaneY) Knockout(slot, KnockoutCause.Fall);
        }

        private void SpawnProjectiles(int slot, PlayerStepEvents ev, bool airborne)
        {
            WeaponSpec w = WeaponCatalog.Get(ev.FiredWeapon);
            var rng = new DeterministicRandom(DeterministicHash.Combine((ulong)(uint)Config.MatchSeed, (ulong)(uint)Tick, (ulong)slot));
            for (int i = 0; i < w.Pellets; i++)
            {
                Vec3 dir = ev.AimDirection;
                if (w.SpreadDegrees > 0f) dir = ApplySpread(dir, w.SpreadDegrees, ref rng);
                var proj = new Projectile
                {
                    Id = _nextProjectileId++,
                    Owner = (byte)slot,
                    Weapon = w.Id,
                    Origin = ev.MuzzleOrigin,
                    Position = ev.MuzzleOrigin,
                    Velocity = dir * w.ProjectileSpeed,
                    LagTicks = _lagTicks[slot],
                    FiredAirborne = airborne,
                };
                Projectiles.Add(proj);
            }
        }

        public static Vec3 ApplySpread(Vec3 dir, float spreadDegrees, ref DeterministicRandom rng)
        {
            // Build an orthonormal basis around dir and offset within a cone.
            Vec3 up = Math.Abs(dir.Y) > 0.95f ? new Vec3(1f, 0f, 0f) : Vec3.Up;
            Vec3 right = Cross(up, dir).Normalized;
            Vec3 realUp = Cross(dir, right);
            float angle = rng.NextFloat() * 6.2831853f;
            float radius = (float)Math.Tan(spreadDegrees * Math.PI / 180.0) * (float)Math.Sqrt(rng.NextFloat());
            return (dir + right * ((float)Math.Cos(angle) * radius) + realUp * ((float)Math.Sin(angle) * radius)).Normalized;
        }

        private static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        private void StepProjectiles(bool combat)
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                Projectile pr = Projectiles[i];
                WeaponSpec w = WeaponCatalog.Get(pr.Weapon);
                Vec3 from = pr.Position;
                pr.Age += DeltaTime;
                Vec3 to = ProjectileMath.PositionAt(pr.Origin, pr.Velocity, w.GravityScale, pr.Age);
                pr.Position = to;

                bool remove = pr.Age >= w.MaxLifetime || to.Y < KillPlaneY;
                if (!combat) remove = true;

                if (!remove)
                {
                    int tile = PlayerSimulation.RaycastTiles(this, from, to, ProjectileRadius, out float tileT, out Vec3 tilePoint);
                    int victim = RaycastPlayers(pr, from, to, out float playerT, out Vec3 playerPoint);

                    if (victim >= 0 && playerT <= tileT)
                    {
                        HitPlayer(pr, w, victim, playerPoint);
                        if (w.SplashRadius > 0f) Splash(pr, w, playerPoint, victim);
                        remove = true;
                    }
                    else if (tile >= 0)
                    {
                        remove = HitTile(pr, w, tile, tilePoint);
                        if (w.SplashRadius > 0f) Splash(pr, w, tilePoint, -1);
                    }
                }

                if (remove) Projectiles.RemoveAt(i);
            }
        }

        private int RaycastPlayers(Projectile pr, Vec3 from, Vec3 to, out float bestT, out Vec3 point)
        {
            bestT = float.MaxValue;
            point = to;
            int best = -1;
            int rewindTick = Tick - pr.LagTicks;
            Vec3 d = to - from;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (slot == pr.Owner || !Players[slot].Active) continue;
                if (!TryGetHistoricPosition(slot, rewindTick, out Vec3 feet)) continue;
                PlayerSimulation.GetPlayerBounds(feet, out Vec3 min, out Vec3 max);
                var r = new Vec3(ProjectileRadius, ProjectileRadius, ProjectileRadius);
                if (PlayerSimulation.SegmentAabb(from, d, min - r, max + r, out float t) && t < bestT)
                {
                    bestT = t;
                    best = slot;
                    point = from + d * t;
                }
            }
            return best;
        }

        private bool TryGetHistoricPosition(int slot, int tick, out Vec3 feet)
        {
            if (tick >= Tick || Tick - tick >= HistoryLength)
            {
                feet = Players[slot].Position;
                return Players[slot].Alive;
            }
            int idx = ((tick % HistoryLength) + HistoryLength) % HistoryLength;
            feet = _history[slot, idx];
            return _historyAlive[slot, idx] && Players[slot].Alive;
        }

        private void RecordHistory()
        {
            int idx = Tick % HistoryLength;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                _history[slot, idx] = Players[slot].Position;
                _historyAlive[slot, idx] = Players[slot].Active && Players[slot].Alive;
            }
        }

        private void HitPlayer(Projectile pr, WeaponSpec w, int victim, Vec3 point)
        {
            Vec3 dir = pr.CurrentVelocity.Normalized;
            float impulse = w.ShotEnergy / w.Pellets * w.KnockbackScale * pr.DamageScale;
            Vec3 dv = (dir + new Vec3(0f, 0.2f, 0f)).Normalized * (impulse / PlayerRules.Mass);
            _lastShotInfo[pr.Owner] = ((point - pr.Origin).Length, pr.FiredAirborne);
            DamagePlayer(victim, pr.Owner, w.PlayerDamage * pr.DamageScale, dv, KnockoutCause.Damage, point);
            Events.Add(new WorldEvent { Type = WorldEventType.ProjectileImpact, A = -1, B = pr.Weapon, Point = point });
        }

        /// <returns>true if the projectile is consumed.</returns>
        private bool HitTile(Projectile pr, WeaponSpec w, int tile, Vec3 point)
        {
            GlassSpec spec = GlassCatalog.Get(Layout.Types[tile]);
            var events = _resolver.ApplyImpact(Tiles, tile, w.TileDamage * pr.DamageScale, w.Heavy, DamageSource.Projectile);
            float force = GcMath.Clamp01(w.TileDamage / WeaponCatalog.Findling.TileDamage);
            bool hitShattered = false;
            for (int e = 0; e < events.Count; e++)
            {
                TileDamageEvent te = events[e];
                if (!te.Shattered) continue;
                bool isHit = te.TileId == tile;
                hitShattered |= isHit;
                OnTileShattered(te.TileId, isHit ? point : Layout.Centers[te.TileId], isHit ? force : force * 0.5f, pr.Owner);
            }
            Events.Add(new WorldEvent { Type = WorldEventType.ProjectileImpact, A = tile, B = pr.Weapon, Point = point, C = tile });

            if (hitShattered && w.Heavy && !spec.BlocksProjectiles)
            {
                pr.DamageScale *= 0.5f; // punches through standard glass with half energy
                return false;
            }
            return true;
        }

        private void Splash(Projectile pr, WeaponSpec w, Vec3 point, int directVictim)
        {
            float r = w.SplashRadius;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (!Players[slot].Active || !Players[slot].Alive || slot == directVictim) continue;
                Vec3 center = Players[slot].Position + new Vec3(0f, 0.9f, 0f);
                Vec3 delta = center - point;
                float dist = delta.Length;
                if (dist > r) continue;
                float falloff = 1f - dist / r;
                Vec3 dv = (delta.Normalized + new Vec3(0f, 0.3f, 0f)).Normalized * (w.ShotEnergy * w.KnockbackScale * falloff / PlayerRules.Mass);
                if (slot == pr.Owner)
                {
                    // Self-splash: knockback only (rocket jumping), never damage.
                    Players[slot].Velocity += dv;
                    if (dv.Y > 0f) Players[slot].Grounded = false;
                    continue;
                }
                DamagePlayer(slot, pr.Owner, w.PlayerDamage * 0.6f * falloff, dv, KnockoutCause.Damage, center);
            }

            for (int i = 0; i < Layout.Count; i++)
            {
                if (!Tiles.IsSolid(i)) continue;
                float dist = (Layout.Centers[i] - point).Length;
                if (dist > r || dist < 0.01f) continue;
                float dmg = w.TileDamage * 0.35f * (1f - dist / r);
                if (dmg < 1f) continue;
                var te = _resolver.ApplyDirect(Tiles, i, dmg, DamageSource.Splash);
                if (te.Shattered) OnTileShattered(i, Layout.Centers[i], 0.4f, pr.Owner);
            }
        }

        private bool ApplyDirectAndCheck(int tile, float damage, DamageSource source, int instigator, float force)
        {
            if (damage <= 0f) return false;
            var te = _resolver.ApplyDirect(Tiles, tile, damage, source);
            if (te.Shattered)
            {
                OnTileShattered(tile, Layout.Centers[tile], force, instigator);
                return true;
            }
            return false;
        }

        private void OnTileShattered(int tile, Vec3 impact, float force, int instigator)
        {
            Events.Add(new WorldEvent { Type = WorldEventType.ShatterTile, A = tile, B = instigator, Point = impact, Value = force });
            if (instigator >= 0)
            {
                Stats[instigator].TilesShattered++;
                Highlights.OnTileShattered(instigator, Tick);
            }

            GlassSpec spec = GlassCatalog.Get(Layout.Types[tile]);
            if (spec.ShardDamage <= 0f) return; // tempered glass crumbles into harmless cubes
            Vec3 c = Layout.Centers[tile];
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (!Players[slot].Active || !Players[slot].Alive) continue;
                Vec3 feet = Players[slot].Position;
                float dy = feet.Y - c.Y;
                if (dy < -2.5f || dy > 1.5f) continue;
                if ((feet.XZ - c.XZ).Length > spec.ShardRadius + GlassCatalog.TileSize * 0.5f) continue;
                DamagePlayer(slot, instigator, spec.ShardDamage, Vec3.Zero, KnockoutCause.Shards, feet);
            }
        }

        public void DamagePlayer(int victim, int attacker, float amount, Vec3 velocityDelta, KnockoutCause cause, Vec3 point)
        {
            ref PlayerState p = ref Players[victim];
            if (!p.Active || !p.Alive || p.SpawnProtection > 0f) return;
            if (Config.Teams && !Config.FriendlyFire && attacker >= 0 && attacker != victim && Players[attacker].Team == p.Team) return;

            p.Health -= amount;
            p.Velocity += velocityDelta;
            if (velocityDelta.Y > 0f) p.Grounded = false;
            if (attacker >= 0 && attacker != victim)
            {
                _attribution.RecordHit(victim, attacker, Tick);
                Stats[attacker].DamageDealt += amount;
            }
            Events.Add(new WorldEvent { Type = WorldEventType.PlayerHit, A = victim, B = attacker, Value = amount, Point = point });
            if (p.Health <= 0f) Knockout(victim, cause);
        }

        public void Knockout(int victim, KnockoutCause cause)
        {
            ref PlayerState p = ref Players[victim];
            if (!p.Alive) return;
            p.Alive = false;
            p.Velocity = Vec3.Zero;
            p.AnchorTime = 0f;
            _respawnTimer[victim] = MatchTimings.Respawn;

            if (!MatchClock.IsCombatPhase(Clock.Phase)) return;

            int attacker = _attribution.ResolveKnockout(victim, Tick);
            Stats[victim].Deaths++;
            if (attacker >= 0 && attacker != victim)
            {
                Stats[attacker].Score += KillAttribution.KnockoutPoints;
                Stats[attacker].Knockouts++;
            }
            else
            {
                Stats[victim].Score = KillAttribution.ApplySelfKnockout(Stats[victim].Score);
                Stats[victim].SelfKnockouts++;
            }

            var shot = attacker >= 0 ? _lastShotInfo[attacker] : default;
            Highlights.OnKnockout(attacker, victim, Tick, cause, shot.distance, shot.airborne);
            Events.Add(new WorldEvent { Type = WorldEventType.Knockout, A = victim, B = attacker, C = (int)cause, Point = p.Position });
        }

        private void StepRespawns(MatchPhase phase)
        {
            if (!MatchClock.IsCombatPhase(phase)) return;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (!Players[slot].Active || Players[slot].Alive) continue;
                _respawnTimer[slot] -= DeltaTime;
                if (_respawnTimer[slot] <= 0f) Spawn(slot, MatchTimings.SpawnProtection);
            }
        }

        private void SpawnAll()
        {
            for (int slot = 0; slot < MaxPlayers; slot++)
                if (Players[slot].Active) Spawn(slot, 0f);
        }

        /// <returns>false if the arena has fully collapsed and there is nowhere to stand.</returns>
        public bool Spawn(int slot, float protection)
        {
            _enemyScratch.Clear();
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (i == slot || !Players[i].Active || !Players[i].Alive) continue;
                if (Config.Teams && Players[i].Team == Players[slot].Team) continue;
                _enemyScratch.Add(Players[i].Position);
            }
            int tile = SpawnSelector.Select(Layout, Tiles, _enemyScratch, DeterministicHash.Combine((ulong)(uint)Config.MatchSeed, (ulong)(uint)Tick, (ulong)slot));
            if (tile < 0) return false;

            Vec3 c = Layout.Centers[tile];
            ref PlayerState p = ref Players[slot];
            p.Alive = true;
            p.Health = PlayerRules.MaxHealth;
            p.Position = new Vec3(c.X, c.Y + GlassCatalog.TileThickness * 0.5f + 0.002f, c.Z);
            p.Velocity = Vec3.Zero;
            p.Grounded = true;
            p.GroundTile = tile;
            p.AnchorTime = 0f;
            p.AnchorCooldown = 0f;
            p.FireCooldown = 0.5f;
            p.SpawnProtection = protection;
            p.Yaw = (float)(Math.Atan2(-c.X, -c.Z) * 180.0 / Math.PI);
            p.Pitch = 0f;
            _attribution.Forget(slot);
            Events.Add(new WorldEvent { Type = WorldEventType.Respawn, A = slot, C = tile, Point = p.Position });
            return true;
        }

        /// <summary>Final placements (1 = best). Team mode ranks every member by team score.</summary>
        public int[] ComputePlacements()
        {
            var scores = new int[MaxPlayers];
            for (int i = 0; i < MaxPlayers; i++)
                scores[i] = !Players[i].Active ? int.MinValue : (Config.Teams ? TeamScore(Players[i].Team) : Stats[i].Score);
            return RatingSystem.PlacementsFromScores(scores);
        }
    }
}
