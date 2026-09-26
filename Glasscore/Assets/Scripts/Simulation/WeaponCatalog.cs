namespace Glasscore.Simulation
{
    /// <summary>
    /// Stone-throwing arsenal ("…sollte nicht mit Steinen werfen"). ShotEnergy is the impulse in N·s that
    /// is applied to the projectile's target AND, reversed, to the shooter (Δv = ShotEnergy / 80 kg).
    /// </summary>
    public readonly struct WeaponSpec
    {
        public readonly byte Id;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly float PlayerDamage;     // per pellet
        public readonly float TileDamage;       // per pellet, before glass absorption
        public readonly float ShotEnergy;       // N·s per trigger pull (all pellets combined)
        public readonly float FireInterval;     // s between shots
        public readonly float ProjectileSpeed;  // m/s
        public readonly float GravityScale;
        public readonly bool Heavy;             // heavy projectiles instantly shatter standard glass
        public readonly int Pellets;
        public readonly float SpreadDegrees;
        public readonly float SplashRadius;     // m, 0 = none
        public readonly float KnockbackScale;   // fraction of ShotEnergy transferred to a hit player
        public readonly float MaxLifetime;      // s

        public WeaponSpec(byte id, string key, string displayName, float playerDamage, float tileDamage, float shotEnergy,
            float fireInterval, float projectileSpeed, float gravityScale, bool heavy, int pellets, float spreadDegrees,
            float splashRadius, float knockbackScale, float maxLifetime)
        {
            Id = id; Key = key; DisplayName = displayName;
            PlayerDamage = playerDamage; TileDamage = tileDamage; ShotEnergy = shotEnergy;
            FireInterval = fireInterval; ProjectileSpeed = projectileSpeed; GravityScale = gravityScale;
            Heavy = heavy; Pellets = pellets; SpreadDegrees = spreadDegrees; SplashRadius = splashRadius;
            KnockbackScale = knockbackScale; MaxLifetime = maxLifetime;
        }

        public float RecoilDeltaV => ShotEnergy / PlayerRules.Mass;
    }

    public static class WeaponCatalog
    {
        public static readonly WeaponSpec Kiesel = new WeaponSpec(0, "kiesel", "KIESEL · Pebble Sling",
            playerDamage: 12f, tileDamage: 20f, shotEnergy: 160f, fireInterval: 0.2f, projectileSpeed: 70f,
            gravityScale: 0.3f, heavy: false, pellets: 1, spreadDegrees: 0f, splashRadius: 0f, knockbackScale: 0.75f, maxLifetime: 2f);

        public static readonly WeaponSpec Pflasterstein = new WeaponSpec(1, "pflasterstein", "PFLASTERSTEIN · Cobble Launcher",
            playerDamage: 30f, tileDamage: 60f, shotEnergy: 480f, fireInterval: 0.8f, projectileSpeed: 40f,
            gravityScale: 1f, heavy: true, pellets: 1, spreadDegrees: 0f, splashRadius: 0f, knockbackScale: 0.75f, maxLifetime: 3f);

        public static readonly WeaponSpec Findling = new WeaponSpec(2, "findling", "FINDLING · Boulder Mortar",
            playerDamage: 55f, tileDamage: 140f, shotEnergy: 960f, fireInterval: 2f, projectileSpeed: 28f,
            gravityScale: 1f, heavy: true, pellets: 1, spreadDegrees: 0f, splashRadius: 3f, knockbackScale: 0.9f, maxLifetime: 4f);

        public static readonly WeaponSpec Splitter = new WeaponSpec(3, "splitter", "SPLITTER · Shard Scatter",
            playerDamage: 6f, tileDamage: 12f, shotEnergy: 400f, fireInterval: 1f, projectileSpeed: 55f,
            gravityScale: 0.5f, heavy: false, pellets: 8, spreadDegrees: 6f, splashRadius: 0f, knockbackScale: 0.6f, maxLifetime: 1.2f);

        public static readonly WeaponSpec[] All = { Kiesel, Pflasterstein, Findling, Splitter };

        public static int Count => All.Length;

        public static WeaponSpec Get(int id) => All[GcMath.Clamp(id, 0, All.Length - 1)];

        public static int Cycle(int current, int direction)
        {
            int n = All.Length;
            return ((current + direction) % n + n) % n;
        }
    }
}
