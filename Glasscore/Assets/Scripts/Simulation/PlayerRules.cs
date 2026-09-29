namespace Glasscore.Simulation
{
    /// <summary>Movement, recoil (Newton's third law) and tile-load constants. SI units throughout.</summary>
    public static class PlayerRules
    {
        public const float MaxHealth = 100f;
        public const float Mass = 80f;                 // kg
        public const float Gravity = 20f;              // m/s² (stylised, snappier than 9.81)
        public const float WalkSpeed = 7f;             // m/s
        public const float GroundAcceleration = 60f;   // m/s²
        public const float AirAcceleration = 12f;      // m/s²
        public const float GroundFriction = 10f;       // 1/s, exponential
        public const float AirDrag = 0.1f;             // 1/s
        public const float JumpSpeed = 7f;             // m/s → ~1.2 m apex
        public const float MaxSpeed = 40f;             // m/s clamp to keep recoil stacking sane
        public const float GroundProbeDistance = 0.2f; // m below feet that still counts as grounded
        public const float CapsuleRadius = 0.4f;
        public const float CapsuleHeight = 1.8f;

        public const float JumpVaultTileDamage = 10f;
        public const float CompressionDamagePerImpulse = 0.04f; // HP per N·s pressed into a tile
        public const float WeightLoadDps = 1.5f;                // HP/s while standing on a weakened tile

        public const float AnchorDuration = 1.5f;       // s
        public const float AnchorCooldown = 4f;         // s (from anchor end)
        public const float AnchorLoadMultiplier = 2f;   // suction boots double the load on the tile

        public const float KillPlaneBelowLowestLayer = 25f; // m
    }

    public struct RecoilResult
    {
        /// <summary>Velocity change applied to the shooter (m/s).</summary>
        public Vec3 VelocityDelta;
        /// <summary>Impulse (N·s) pushed into the tile under the shooter.</summary>
        public float CompressionImpulse;
    }

    public static class RecoilModel
    {
        /// <summary>
        /// Force_Recoil = Shot_Energy * -Player_Look_Direction.
        /// Mid-air: the whole impulse moves the player (fire down → launched up).
        /// Grounded: the downward component is resisted by the floor and becomes compression.
        /// Anchored: the player cannot move, so the full impulse — doubled by the suction load — is
        /// pushed into the tile.
        /// </summary>
        public static RecoilResult Compute(float shotEnergy, Vec3 lookDirection, bool grounded, bool anchored)
        {
            Vec3 impulse = -lookDirection.Normalized * shotEnergy;
            var result = new RecoilResult();

            if (anchored)
            {
                result.VelocityDelta = Vec3.Zero;
                result.CompressionImpulse = shotEnergy * PlayerRules.AnchorLoadMultiplier;
                return result;
            }

            if (grounded && impulse.Y < 0f)
            {
                result.CompressionImpulse = -impulse.Y;
                impulse.Y = 0f;
            }

            result.VelocityDelta = impulse / PlayerRules.Mass;
            return result;
        }

        /// <summary>
        /// Compression only hurts tiles that are already weakened — unless the shooter is anchored,
        /// in which case suction boots transfer the load into any glass.
        /// </summary>
        public static float CompressionTileDamage(float compressionImpulse, bool tileWeakened, bool anchored)
        {
            if (compressionImpulse <= 0f) return 0f;
            if (!tileWeakened && !anchored) return 0f;
            return compressionImpulse * PlayerRules.CompressionDamagePerImpulse;
        }

        /// <summary>Static weight-load damage per tick for a player standing on a weakened tile.</summary>
        public static float WeightLoadDamage(float dt, bool tileWeakened, bool anchored)
        {
            if (!tileWeakened) return 0f;
            return PlayerRules.WeightLoadDps * (anchored ? PlayerRules.AnchorLoadMultiplier : 1f) * dt;
        }

        /// <summary>Jumping off a fragile tile (standard glass or any weakened tile) damages it.</summary>
        public static float JumpVaultDamage(GlassType type, float integrity) =>
            GlassCatalog.IsFragile(type, integrity) ? PlayerRules.JumpVaultTileDamage : 0f;
    }

    public static class ProjectileMath
    {
        /// <summary>Closed-form ballistic position — tick-exact and identical during resimulation.</summary>
        public static Vec3 PositionAt(Vec3 origin, Vec3 velocity, float gravityScale, float t)
        {
            return origin + velocity * t + new Vec3(0f, -0.5f * PlayerRules.Gravity * gravityScale * t * t, 0f);
        }
    }
}
