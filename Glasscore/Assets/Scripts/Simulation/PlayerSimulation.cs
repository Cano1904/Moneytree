using System;

namespace Glasscore.Simulation
{
    [Flags]
    public enum InputButtons : ushort
    {
        None = 0,
        Jump = 1 << 0,
        Fire = 1 << 1,
        Anchor = 1 << 2,
        NextWeapon = 1 << 3,
        PrevWeapon = 1 << 4,
    }

    /// <summary>One tick of player intent. Sent client → server every tick (sequence-numbered).</summary>
    public struct PlayerInput
    {
        public uint Sequence;
        public float MoveX;   // strafe  -1..1
        public float MoveY;   // forward -1..1
        public float Yaw;     // degrees, 0 = +Z
        public float Pitch;   // degrees, positive looks down (Unity convention), clamped ±89
        public InputButtons Buttons;

        public bool Has(InputButtons b) => (Buttons & b) != 0;

        public static PlayerInput Neutral(uint seq, float yaw, float pitch) =>
            new PlayerInput { Sequence = seq, Yaw = yaw, Pitch = pitch };
    }

    /// <summary>
    /// Complete per-player simulation state. Everything the movement step reads or writes lives here
    /// so the client can reset to a server snapshot and replay unacknowledged inputs (reconciliation).
    /// </summary>
    public struct PlayerState
    {
        public byte Slot;
        public bool Active;    // participating in the match
        public bool Alive;
        public Vec3 Position;  // feet
        public Vec3 Velocity;
        public float Yaw;
        public float Pitch;
        public float Health;
        public bool Grounded;
        public int GroundTile;
        public byte Weapon;
        public float FireCooldown;
        public float AnchorTime;      // > 0 while anchored
        public float AnchorCooldown;
        public float SpawnProtection;
        public InputButtons PrevButtons;
        public byte Team;

        public bool Anchored => AnchorTime > 0f;
        public Vec3 EyePosition => Position + new Vec3(0f, 1.6f, 0f);
    }

    /// <summary>What a single movement step did — the server turns this into tile damage / projectiles.</summary>
    public struct PlayerStepEvents
    {
        public bool Fired;
        public byte FiredWeapon;
        public Vec3 MuzzleOrigin;
        public Vec3 AimDirection;
        public float CompressionImpulse;
        public int CompressionTile;
        public bool Jumped;
        public int JumpTile;
        public bool AnchorStarted;
        public bool AnchorBroken;
        public bool WeaponSwitched;
    }

    /// <summary>Read-only tile query used for collision (server model or client replica).</summary>
    public interface ITileQuery
    {
        TileLayout Layout { get; }
        bool IsSolid(int tileId);
    }

    public static class PlayerSimulation
    {
        public const float HalfWidth = PlayerRules.CapsuleRadius;
        public const float Height = PlayerRules.CapsuleHeight;
        private const float Skin = 0.001f;
        private const float WeaponSwitchDelay = 0.25f;

        [ThreadStatic] private static System.Collections.Generic.List<int> _scratch;

        private static System.Collections.Generic.List<int> Scratch()
        {
            if (_scratch == null) _scratch = new System.Collections.Generic.List<int>(64);
            _scratch.Clear();
            return _scratch;
        }

        public static Vec3 LookDirection(float yawDeg, float pitchDeg)
        {
            double yaw = yawDeg * Math.PI / 180.0;
            double pitch = pitchDeg * Math.PI / 180.0;
            double cp = Math.Cos(pitch);
            return new Vec3((float)(cp * Math.Sin(yaw)), (float)(-Math.Sin(pitch)), (float)(cp * Math.Cos(yaw)));
        }

        /// <summary>
        /// Advances one player by one tick. Used identically by the authoritative server and by the
        /// owning client's prediction/reconciliation, so it must only depend on (state, input, tiles, dt).
        /// </summary>
        public static PlayerStepEvents Step(ref PlayerState s, PlayerInput input, ITileQuery tiles, float dt, bool allowMovement, bool allowCombat)
        {
            var ev = new PlayerStepEvents { CompressionTile = -1, JumpTile = -1 };
            s.Yaw = input.Yaw;
            s.Pitch = GcMath.Clamp(input.Pitch, -89f, 89f);
            if (!s.Alive || !s.Active)
            {
                s.PrevButtons = input.Buttons;
                return ev;
            }

            InputButtons pressed = input.Buttons & ~s.PrevButtons;
            s.PrevButtons = input.Buttons;

            s.FireCooldown = Math.Max(0f, s.FireCooldown - dt);
            s.AnchorCooldown = Math.Max(0f, s.AnchorCooldown - dt);
            s.SpawnProtection = Math.Max(0f, s.SpawnProtection - dt);

            // Ground tile may have been shattered since last tick.
            if (s.Grounded && (s.GroundTile < 0 || !tiles.IsSolid(s.GroundTile))) s.Grounded = false;

            if (s.Anchored)
            {
                s.AnchorTime = Math.Max(0f, s.AnchorTime - dt);
                if (!s.Grounded)
                {
                    s.AnchorTime = 0f;
                    ev.AnchorBroken = true;
                }
                if (s.AnchorTime <= 0f) s.AnchorCooldown = PlayerRules.AnchorCooldown;
            }

            if (allowCombat)
            {
                if ((pressed & InputButtons.NextWeapon) != 0 || (pressed & InputButtons.PrevWeapon) != 0)
                {
                    int dir = (pressed & InputButtons.NextWeapon) != 0 ? 1 : -1;
                    s.Weapon = (byte)WeaponCatalog.Cycle(s.Weapon, dir);
                    s.FireCooldown = Math.Max(s.FireCooldown, WeaponSwitchDelay);
                    ev.WeaponSwitched = true;
                }

                if ((pressed & InputButtons.Anchor) != 0 && s.Grounded && !s.Anchored && s.AnchorCooldown <= 0f)
                {
                    s.AnchorTime = PlayerRules.AnchorDuration;
                    ev.AnchorStarted = true;
                }
            }

            if (allowMovement && (pressed & InputButtons.Jump) != 0 && s.Grounded && !s.Anchored)
            {
                s.Velocity.Y = PlayerRules.JumpSpeed;
                ev.Jumped = true;
                ev.JumpTile = s.GroundTile;
                s.Grounded = false;
            }

            if (allowCombat && input.Has(InputButtons.Fire) && s.FireCooldown <= 0f)
            {
                WeaponSpec w = WeaponCatalog.Get(s.Weapon);
                s.FireCooldown = w.FireInterval;
                Vec3 look = LookDirection(s.Yaw, s.Pitch);
                RecoilResult recoil = RecoilModel.Compute(w.ShotEnergy, look, s.Grounded, s.Anchored);
                s.Velocity += recoil.VelocityDelta;
                if (recoil.VelocityDelta.Y > 0f) s.Grounded = false;
                ev.Fired = true;
                ev.FiredWeapon = s.Weapon;
                ev.AimDirection = look;
                ev.MuzzleOrigin = s.EyePosition + look * 0.6f;
                ev.CompressionImpulse = recoil.CompressionImpulse;
                ev.CompressionTile = s.Grounded ? s.GroundTile : -1;
                s.SpawnProtection = 0f; // firing forfeits spawn protection
            }

            if (s.Anchored)
            {
                s.Velocity = Vec3.Zero; // suction boots freeze position
            }
            else
            {
                ApplyLocomotion(ref s, allowMovement ? input : default, dt);
                Move(ref s, tiles, dt);
            }

            return ev;
        }

        private static void ApplyLocomotion(ref PlayerState s, PlayerInput input, float dt)
        {
            double yaw = s.Yaw * Math.PI / 180.0;
            var forward = new Vec2((float)Math.Sin(yaw), (float)Math.Cos(yaw));
            var right = new Vec2(forward.Y, -forward.X);
            Vec2 wish = forward * GcMath.Clamp(input.MoveY, -1f, 1f) + right * GcMath.Clamp(input.MoveX, -1f, 1f);
            float wishLen = wish.Length;
            if (wishLen > 1f) { wish = wish / wishLen; wishLen = 1f; }
            Vec2 wishDir = wishLen > 1e-4f ? wish / wishLen : Vec2.Zero;
            float wishSpeed = wishLen * PlayerRules.WalkSpeed;

            var vh = new Vec2(s.Velocity.X, s.Velocity.Z);
            if (s.Grounded)
            {
                vh = vh * (float)Math.Exp(-PlayerRules.GroundFriction * dt);
                vh = Accelerate(vh, wishDir, wishSpeed, PlayerRules.GroundAcceleration, dt);
            }
            else
            {
                vh = vh * (float)Math.Exp(-PlayerRules.AirDrag * dt);
                vh = Accelerate(vh, wishDir, wishSpeed, PlayerRules.AirAcceleration, dt);
                s.Velocity.Y -= PlayerRules.Gravity * dt;
            }

            s.Velocity.X = vh.X;
            s.Velocity.Z = vh.Y;

            float speed = s.Velocity.Length;
            if (speed > PlayerRules.MaxSpeed) s.Velocity = s.Velocity * (PlayerRules.MaxSpeed / speed);
        }

        private static Vec2 Accelerate(Vec2 v, Vec2 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishSpeed <= 0f) return v;
            float current = Vec2.Dot(v, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return v;
            float step = Math.Min(accel * dt * wishSpeed, add);
            return v + wishDir * step;
        }

        /// <summary>Axis-separated swept AABB against the tile slabs (Y first so landing is exact).</summary>
        private static void Move(ref PlayerState s, ITileQuery tiles, float dt)
        {
            Vec3 d = s.Velocity * dt;

            float dy = SweepAxis(ref s, tiles, 1, d.Y, out bool hitY);
            s.Position.Y += dy;
            if (hitY)
            {
                if (d.Y < 0f) s.Grounded = true;
                s.Velocity.Y = 0f;
            }

            float dx = SweepAxis(ref s, tiles, 0, d.X, out bool hitX);
            s.Position.X += dx;
            if (hitX) s.Velocity.X = 0f;

            float dz = SweepAxis(ref s, tiles, 2, d.Z, out bool hitZ);
            s.Position.Z += dz;
            if (hitZ) s.Velocity.Z = 0f;

            UpdateGround(ref s, tiles);
        }

        private static float SweepAxis(ref PlayerState s, ITileQuery tiles, int axis, float delta, out bool hit)
        {
            hit = false;
            if (delta == 0f) return 0f;
            GetPlayerBounds(s.Position, out Vec3 pMin, out Vec3 pMax);
            TileLayout layout = tiles.Layout;
            float half = GlassCatalog.TileSize * 0.5f;
            float halfT = GlassCatalog.TileThickness * 0.5f;

            // Candidate volume = player bounds expanded along the sweep.
            Vec3 cMin = pMin, cMax = pMax;
            if (axis == 0) { if (delta > 0) cMax.X += delta; else cMin.X += delta; }
            else if (axis == 1) { if (delta > 0) cMax.Y += delta; else cMin.Y += delta; }
            else { if (delta > 0) cMax.Z += delta; else cMin.Z += delta; }

            float result = delta;
            var candidates = Scratch();
            QueryTiles(layout, cMin, cMax, candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                int id = candidates[i];
                if (!tiles.IsSolid(id)) continue;
                Vec3 c = layout.Centers[id];
                Vec3 tMin = new Vec3(c.X - half, c.Y - halfT, c.Z - half);
                Vec3 tMax = new Vec3(c.X + half, c.Y + halfT, c.Z + half);

                // Must overlap on the two other axes.
                bool overlaps = true;
                for (int a = 0; a < 3 && overlaps; a++)
                {
                    if (a == axis) continue;
                    if (Get(pMax, a) <= Get(tMin, a) + Skin || Get(pMin, a) >= Get(tMax, a) - Skin) overlaps = false;
                }
                if (!overlaps) continue;

                if (result > 0f)
                {
                    float gap = Get(tMin, axis) - Get(pMax, axis);
                    if (gap >= -Skin && gap < result) { result = Math.Max(0f, gap - Skin); hit = true; }
                }
                else
                {
                    float gap = Get(tMax, axis) - Get(pMin, axis); // ≤ 0 when tile is below/behind
                    if (gap <= Skin && gap > result) { result = Math.Min(0f, gap + Skin); hit = true; }
                }
            }
            return result;
        }

        private static void UpdateGround(ref PlayerState s, ITileQuery tiles)
        {
            if (s.Velocity.Y > 0.01f) { s.Grounded = false; s.GroundTile = -1; return; }
            TileLayout layout = tiles.Layout;
            float half = GlassCatalog.TileSize * 0.5f;
            float halfT = GlassCatalog.TileThickness * 0.5f;
            int best = -1;
            float bestOverlap = 0f;
            GetPlayerBounds(s.Position, out Vec3 pMin, out Vec3 pMax);
            var probeMin = new Vec3(pMin.X, pMin.Y - PlayerRules.GroundProbeDistance, pMin.Z);
            var probeMax = new Vec3(pMax.X, pMin.Y + Skin * 4f, pMax.Z);
            Vec3 pos = s.Position;
            var candidates = Scratch();
            QueryTiles(layout, probeMin, probeMax, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                int id = candidates[i];
                if (!tiles.IsSolid(id)) continue;
                Vec3 c = layout.Centers[id];
                float top = c.Y + halfT;
                if (top > pos.Y + Skin * 4f || top < pos.Y - PlayerRules.GroundProbeDistance) continue;
                float ox = Math.Min(pMax.X, c.X + half) - Math.Max(pMin.X, c.X - half);
                float oz = Math.Min(pMax.Z, c.Z + half) - Math.Max(pMin.Z, c.Z - half);
                if (ox <= 0f || oz <= 0f) continue;
                float overlap = ox * oz;
                if (overlap > bestOverlap) { bestOverlap = overlap; best = id; }
            }

            if (best >= 0)
            {
                float top = layout.Centers[best].Y + halfT;
                bool wasGrounded = s.Grounded;
                // Snap down onto the tile only when already standing (walking off small steps) or landing.
                if (wasGrounded || pos.Y - top < Skin * 8f) s.Position.Y = top + Skin;
                s.Grounded = true;
                s.GroundTile = best;
                if (s.Velocity.Y < 0f) s.Velocity.Y = 0f;
            }
            else
            {
                s.Grounded = false;
                s.GroundTile = -1;
            }
        }

        public static void GetPlayerBounds(Vec3 feet, out Vec3 min, out Vec3 max)
        {
            min = new Vec3(feet.X - HalfWidth, feet.Y, feet.Z - HalfWidth);
            max = new Vec3(feet.X + HalfWidth, feet.Y + Height, feet.Z + HalfWidth);
        }

        private static float Get(Vec3 v, int axis) => axis == 0 ? v.X : (axis == 1 ? v.Y : v.Z);

        /// <summary>Grid-accelerated broadphase: collects tiles whose cell overlaps the given bounds.</summary>
        public static void QueryTiles(TileLayout layout, Vec3 min, Vec3 max, System.Collections.Generic.List<int> results)
        {
            MapDefinition map = layout.Map;
            float size = GlassCatalog.TileSize;
            float halfT = GlassCatalog.TileThickness * 0.5f;
            int w = map.Width, h = map.Height;
            int c0 = (int)Math.Floor(min.X / size + (w - 1) * 0.5f + 0.5f) - 1;
            int c1 = (int)Math.Floor(max.X / size + (w - 1) * 0.5f + 0.5f) + 1;
            int r0 = (int)Math.Floor((h - 1) * 0.5f - max.Z / size + 0.5f) - 1;
            int r1 = (int)Math.Floor((h - 1) * 0.5f - min.Z / size + 0.5f) + 1;
            c0 = Math.Max(0, c0); r0 = Math.Max(0, r0);
            c1 = Math.Min(w - 1, c1); r1 = Math.Min(h - 1, r1);

            for (int l = 0; l < map.LayerCount; l++)
            {
                float y = l * map.LayerSpacing;
                if (y + halfT < min.Y - 0.5f || y - halfT > max.Y + 0.5f) continue;
                for (int r = r0; r <= r1; r++)
                    for (int c = c0; c <= c1; c++)
                    {
                        int id = layout.IdAt(l, r, c);
                        if (id >= 0) results.Add(id);
                    }
            }
        }

        /// <summary>Segment vs tile slab test used by projectiles. Returns tile id or -1.</summary>
        public static int RaycastTiles(ITileQuery tiles, Vec3 from, Vec3 to, float radius, out float hitT, out Vec3 hitPoint)
        {
            hitT = float.MaxValue;
            hitPoint = to;
            int best = -1;
            TileLayout layout = tiles.Layout;
            float half = GlassCatalog.TileSize * 0.5f + radius;
            float halfT = GlassCatalog.TileThickness * 0.5f + radius;
            var min = new Vec3(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Min(from.Z, to.Z));
            var max = new Vec3(Math.Max(from.X, to.X), Math.Max(from.Y, to.Y), Math.Max(from.Z, to.Z));
            float bestT = float.MaxValue;
            Vec3 d = to - from;
            var candidates = Scratch();
            QueryTiles(layout, min, max, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                int id = candidates[i];
                if (!tiles.IsSolid(id)) continue;
                Vec3 c = layout.Centers[id];
                if (SegmentAabb(from, d, new Vec3(c.X - half, c.Y - halfT, c.Z - half), new Vec3(c.X + half, c.Y + halfT, c.Z + half), out float t) && t < bestT)
                {
                    bestT = t;
                    best = id;
                }
            }

            if (best >= 0)
            {
                hitT = bestT;
                hitPoint = from + d * bestT;
            }
            return best;
        }

        /// <summary>Slab method. t in [0,1] along from + d*t.</summary>
        public static bool SegmentAabb(Vec3 from, Vec3 d, Vec3 min, Vec3 max, out float tHit)
        {
            float tMin = 0f, tMax = 1f;
            tHit = 0f;
            for (int a = 0; a < 3; a++)
            {
                float o = Get(from, a), dir = Get(d, a), lo = Get(min, a), hi = Get(max, a);
                if (Math.Abs(dir) < 1e-8f)
                {
                    if (o < lo || o > hi) return false;
                    continue;
                }
                float inv = 1f / dir;
                float t1 = (lo - o) * inv, t2 = (hi - o) * inv;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
            }
            tHit = tMin;
            return true;
        }
    }
}
