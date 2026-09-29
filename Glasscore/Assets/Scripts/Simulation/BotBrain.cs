using System;

namespace Glasscore.Simulation
{
    /// <summary>
    /// Server-side AI opponent. Produces one <see cref="PlayerInput"/> per tick from the authoritative
    /// world — exactly what a human client would send — so bots obey every rule (recoil, fragile glass,
    /// cooldowns) without special cases. Behaviour: pick the nearest enemy, keep a weapon-appropriate
    /// distance, strafe, never walk off an edge, step away from weakened glass, and lead shots for drop.
    /// </summary>
    public sealed class BotBrain
    {
        public static readonly string[] Names = { "Scherbe", "Kiesel", "Splitter", "Prisma", "Quarz", "Obsidian", "Facette", "Kristall" };

        private readonly int _slot;
        private DeterministicRandom _rng;
        private readonly float _aimError;      // degrees of aim noise
        private readonly float _turnSpeed;     // degrees per second
        private float _yaw, _pitch;
        private float _strafeDir = 1f;
        private float _strafeTimer;
        private float _jumpTimer;
        private int _target = -1;
        private float _retargetTimer;
        private uint _sequence;
        private bool _switchHeld;

        public BotBrain(int slot, ulong seed, float skill = 0.5f)
        {
            _slot = slot;
            _rng = new DeterministicRandom(seed ^ (ulong)(slot * 7919 + 1));
            skill = GcMath.Clamp01(skill);
            _aimError = GcMath.Lerp(9f, 2.5f, skill);
            _turnSpeed = GcMath.Lerp(220f, 540f, skill);
            _strafeTimer = 1f;
            _jumpTimer = _rng.Range(2f, 6f);
        }

        public PlayerInput Think(GameWorld world, float dt)
        {
            var input = new PlayerInput { Sequence = ++_sequence };
            PlayerState me = world.Players[_slot];
            if (!me.Alive)
            {
                _yaw = me.Yaw;
                _pitch = 0f;
                input.Yaw = _yaw;
                return input;
            }

            _retargetTimer -= dt;
            if (_target < 0 || _retargetTimer <= 0f || !world.Players[_target].Alive || !world.Players[_target].Active)
            {
                _target = PickTarget(world, me);
                _retargetTimer = 1.5f;
            }

            float distance = 0f;
            if (_target >= 0)
            {
                PlayerState enemy = world.Players[_target];
                Vec3 delta = enemy.Position - me.Position;
                distance = delta.Length;

                // Weapon choice by range; cycle one press at a time (edge-triggered like a human).
                int wanted = distance < 7f ? WeaponCatalog.Splitter.Id : distance > 22f ? WeaponCatalog.Kiesel.Id : WeaponCatalog.Pflasterstein.Id;
                if (me.Weapon != wanted)
                {
                    if (!_switchHeld) input.Buttons |= InputButtons.NextWeapon;
                    _switchHeld = !_switchHeld;
                }

                // Aim with lead for projectile drop and target motion.
                WeaponSpec w = WeaponCatalog.Get(me.Weapon);
                Vec3 eye = me.EyePosition;
                Vec3 aimPoint = enemy.Position + new Vec3(0f, 0.9f, 0f);
                float flight = (aimPoint - eye).Length / w.ProjectileSpeed;
                aimPoint = aimPoint + enemy.Velocity * flight * 0.8f;
                aimPoint.Y += 0.5f * PlayerRules.Gravity * w.GravityScale * flight * flight;
                Vec3 to = aimPoint - eye;
                float wantYaw = (float)(Math.Atan2(to.X, to.Z) * 180.0 / Math.PI);
                float wantPitch = (float)(-Math.Atan2(to.Y, to.XZ.Length) * 180.0 / Math.PI);

                _yaw = TurnTowards(_yaw, wantYaw + _rng.Range(-_aimError, _aimError) * 0.15f, _turnSpeed * dt);
                _pitch = GcMath.Clamp(MoveTowards(_pitch, wantPitch, _turnSpeed * dt), -80f, 80f);

                float yawError = Math.Abs(DeltaAngle(_yaw, wantYaw));
                float pitchError = Math.Abs(_pitch - wantPitch);
                if (yawError < _aimError && pitchError < _aimError && distance < 45f && me.FireCooldown <= 0f && _rng.NextFloat() < 0.6f)
                    input.Buttons |= InputButtons.Fire;
            }
            else
            {
                _yaw += 40f * dt; // idle scan
            }

            // Movement: hold a preferred distance and strafe; veto any step into the void.
            _strafeTimer -= dt;
            if (_strafeTimer <= 0f)
            {
                _strafeDir = _rng.NextFloat() < 0.5f ? -1f : 1f;
                _strafeTimer = _rng.Range(0.8f, 2.2f);
            }
            float preferred = me.Weapon == WeaponCatalog.Splitter.Id ? 6f : 14f;
            float forward = _target < 0 ? 0.6f : (distance > preferred + 3f ? 1f : distance < preferred - 3f ? -0.7f : 0f);
            float strafe = _strafeDir * 0.8f;

            // Standing on weakened glass: move off it.
            if (me.Grounded && me.GroundTile >= 0 && GlassCatalog.IsWeakened(world.Layout.Types[me.GroundTile], world.Tiles.GetIntegrity(me.GroundTile)))
                forward = 1f;

            if (!IsSafe(world, me, _yaw, forward, strafe))
            {
                if (IsSafe(world, me, _yaw, forward, -strafe)) strafe = -strafe;
                else if (IsSafe(world, me, _yaw, 0f, strafe)) forward = 0f;
                else if (IsSafe(world, me, _yaw, -forward, 0f)) { forward = -forward; strafe = 0f; }
                else { forward = 0f; strafe = 0f; }
                _strafeDir = -_strafeDir;
            }
            input.MoveY = forward;
            input.MoveX = strafe;

            _jumpTimer -= dt;
            if (_jumpTimer <= 0f)
            {
                _jumpTimer = _rng.Range(2.5f, 7f);
                if (me.Grounded && IsSafe(world, me, _yaw, forward, strafe, 2.5f)) input.Buttons |= InputButtons.Jump;
            }

            input.Yaw = _yaw;
            input.Pitch = _pitch;
            return input;
        }

        private int PickTarget(GameWorld world, PlayerState me)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if (i == _slot) continue;
                PlayerState o = world.Players[i];
                if (!o.Active || !o.Alive || o.SpawnProtection > 0f) continue;
                if (world.Config.Teams && o.Team == me.Team) continue;
                float d = (o.Position - me.Position).LengthSquared;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>True if there is intact glass under the point we would reach by moving this way.</summary>
        private static bool IsSafe(GameWorld world, PlayerState me, float yawDeg, float forward, float strafe, float lookahead = 1.3f)
        {
            if (!me.Grounded) return true;
            if (Math.Abs(forward) < 0.01f && Math.Abs(strafe) < 0.01f) return true;
            double yaw = yawDeg * Math.PI / 180.0;
            var f = new Vec2((float)Math.Sin(yaw), (float)Math.Cos(yaw));
            var r = new Vec2(f.Y, -f.X);
            Vec2 dir = f * forward + r * strafe;
            float len = dir.Length;
            if (len < 1e-3f) return true;
            dir = dir / len;
            var probe = new Vec3(me.Position.X + dir.X * lookahead, me.Position.Y, me.Position.Z + dir.Y * lookahead);
            float half = GlassCatalog.TileSize * 0.5f;
            var list = new System.Collections.Generic.List<int>(8);
            PlayerSimulation.QueryTiles(world.Layout, new Vec3(probe.X - 0.1f, probe.Y - 1.5f, probe.Z - 0.1f), new Vec3(probe.X + 0.1f, probe.Y + 0.2f, probe.Z + 0.1f), list);
            foreach (int id in list)
            {
                if (!world.Tiles.IsSolid(id)) continue;
                Vec3 c = world.Layout.Centers[id];
                if (Math.Abs(probe.X - c.X) <= half && Math.Abs(probe.Z - c.Z) <= half && c.Y <= probe.Y + 0.2f && c.Y >= probe.Y - 1.5f)
                {
                    if (GlassCatalog.IsWeakened(world.Layout.Types[id], world.Tiles.GetIntegrity(id))) continue;
                    return true;
                }
            }
            return false;
        }

        private static float DeltaAngle(float a, float b) => ((b - a) % 360f + 540f) % 360f - 180f;

        private static float TurnTowards(float current, float target, float maxStep)
        {
            float d = DeltaAngle(current, target);
            if (Math.Abs(d) <= maxStep) return target;
            return current + Math.Sign(d) * maxStep;
        }

        private static float MoveTowards(float current, float target, float maxStep)
        {
            if (Math.Abs(target - current) <= maxStep) return target;
            return current + Math.Sign(target - current) * maxStep;
        }
    }
}
