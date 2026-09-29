using System.Collections.Generic;

namespace Glasscore.Simulation
{
    public enum GlassType : byte
    {
        None = 0,        // empty grid cell (no tile)
        Standard = 1,    // Standard Float Glass
        Tempered = 2,    // Tempered Safety Glass
        Reinforced = 3,  // Reinforced Bulletproof Glass
    }

    public enum BreakStyle : byte
    {
        VoronoiShatter,  // sharp shards (lethal burst), fracture pattern from RPC_ShatterTile seed
        CubeCrumble,     // tempered glass: whole pane collapses into tiny non-lethal cubes at once
    }

    public enum TileVisualState : byte
    {
        Intact,
        Cracked,     // cosmetic cracks (< 50% integrity)
        Spiderweb,   // tempered glass below its spiderweb threshold
        Destroyed,
    }

    public enum DamageSource : byte
    {
        Projectile,
        Splash,
        Cascade,
        Compression,
        JumpVault,
        WeightLoad,
        Erosion,
        PressureWave,
    }

    public readonly struct GlassSpec
    {
        public readonly GlassType Type;
        public readonly float MaxIntegrity;
        /// <summary>Fraction of incoming damage ignored (0.9 = absorbs 90%).</summary>
        public readonly float DamageAbsorb;
        /// <summary>Fraction of kinetic impact energy forwarded, split among intact orthogonal neighbours.</summary>
        public readonly float NeighborTransfer;
        /// <summary>Integrity below which the tile renders as a spiderweb (tempered only, else 0).</summary>
        public readonly float SpiderwebThreshold;
        /// <summary>A heavy projectile destroys the tile outright regardless of integrity.</summary>
        public readonly bool HeavyInstantShatter;
        /// <summary>Projectiles stop on this tile even when they destroy it.</summary>
        public readonly bool BlocksProjectiles;
        public readonly BreakStyle BreakStyle;
        /// <summary>Damage dealt by the shard burst to players near the tile when it breaks.</summary>
        public readonly float ShardDamage;
        public readonly float ShardRadius;

        public GlassSpec(GlassType type, float maxIntegrity, float absorb, float transfer, float spiderweb,
            bool heavyInstant, bool blocks, BreakStyle breakStyle, float shardDamage, float shardRadius)
        {
            Type = type;
            MaxIntegrity = maxIntegrity;
            DamageAbsorb = absorb;
            NeighborTransfer = transfer;
            SpiderwebThreshold = spiderweb;
            HeavyInstantShatter = heavyInstant;
            BlocksProjectiles = blocks;
            BreakStyle = breakStyle;
            ShardDamage = shardDamage;
            ShardRadius = shardRadius;
        }
    }

    public static class GlassCatalog
    {
        public const float TileSize = 2.0f;       // metres (X and Z)
        public const float TileThickness = 0.1f;  // metres (Y)
        public const float WeakenedFraction = 0.5f;
        public const int MaxCascadeDepth = 3;
        public const float MinCascadeDamage = 1.0f;

        public static readonly GlassSpec Standard = new GlassSpec(
            GlassType.Standard, 50f, absorb: 0f, transfer: 0.5f, spiderweb: 0f,
            heavyInstant: true, blocks: false, BreakStyle.VoronoiShatter, shardDamage: 15f, shardRadius: 1.75f);

        public static readonly GlassSpec Tempered = new GlassSpec(
            GlassType.Tempered, 100f, absorb: 0f, transfer: 0f, spiderweb: 30f,
            heavyInstant: false, blocks: true, BreakStyle.CubeCrumble, shardDamage: 0f, shardRadius: 0f);

        public static readonly GlassSpec Reinforced = new GlassSpec(
            GlassType.Reinforced, 300f, absorb: 0.9f, transfer: 0f, spiderweb: 0f,
            heavyInstant: false, blocks: true, BreakStyle.VoronoiShatter, shardDamage: 10f, shardRadius: 1.5f);

        public static GlassSpec Get(GlassType type)
        {
            switch (type)
            {
                case GlassType.Standard: return Standard;
                case GlassType.Tempered: return Tempered;
                case GlassType.Reinforced: return Reinforced;
                default: return default;
            }
        }

        public static bool IsWeakened(GlassType type, float integrity)
        {
            var spec = Get(type);
            return spec.MaxIntegrity > 0f && integrity < spec.MaxIntegrity * WeakenedFraction;
        }

        /// <summary>"Fragile" for the jump-vault rule: standard glass, or any tile that is weakened.</summary>
        public static bool IsFragile(GlassType type, float integrity) =>
            type == GlassType.Standard || IsWeakened(type, integrity);

        public static TileVisualState VisualState(GlassType type, float integrity)
        {
            if (type == GlassType.None || integrity <= 0f) return TileVisualState.Destroyed;
            var spec = Get(type);
            if (spec.SpiderwebThreshold > 0f && integrity < spec.SpiderwebThreshold) return TileVisualState.Spiderweb;
            if (integrity < spec.MaxIntegrity * WeakenedFraction) return TileVisualState.Cracked;
            return TileVisualState.Intact;
        }
    }

    /// <summary>Storage abstraction: backed by a Fusion NetworkArray on the server, by arrays in tests.</summary>
    public interface ITileStore
    {
        int TileCount { get; }
        GlassType GetGlassType(int tileId);
        float GetIntegrity(int tileId);
        void SetIntegrity(int tileId, float integrity);
        /// <summary>Writes orthogonal same-layer neighbour IDs into buffer (length ≥ 4), returns count.</summary>
        int GetNeighbors(int tileId, int[] buffer);
    }

    public struct TileDamageEvent
    {
        public int TileId;
        public float Applied;
        public float Remaining;
        public bool Shattered;
        public DamageSource Source;
        public int Depth;
    }

    /// <summary>
    /// Server-only damage resolution: absorption, heavy instant-shatter, and the 50% kinetic transfer
    /// cascade (breadth-first, bounded by depth and a minimum energy so it always terminates).
    /// </summary>
    public sealed class TileDamageResolver
    {
        private readonly List<TileDamageEvent> _events = new List<TileDamageEvent>(32);
        private readonly Queue<(int id, float raw, int depth)> _queue = new Queue<(int, float, int)>();
        private readonly HashSet<int> _visited = new HashSet<int>();
        private readonly int[] _neighbors = new int[4];
        private readonly int[] _aliveNeighbors = new int[4];

        public IReadOnlyList<TileDamageEvent> Events => _events;

        /// <summary>Projectile/splash impact. Clears and fills <see cref="Events"/>.</summary>
        public IReadOnlyList<TileDamageEvent> ApplyImpact(ITileStore store, int tileId, float rawDamage, bool heavy, DamageSource source)
        {
            _events.Clear();
            _queue.Clear();
            _visited.Clear();

            _queue.Enqueue((tileId, rawDamage, 0));
            _visited.Add(tileId);

            while (_queue.Count > 0)
            {
                var (id, raw, depth) = _queue.Dequeue();
                GlassType type = store.GetGlassType(id);
                float integrity = store.GetIntegrity(id);
                if (type == GlassType.None || integrity <= 0f) continue;

                GlassSpec spec = GlassCatalog.Get(type);
                bool instant = depth == 0 && heavy && spec.HeavyInstantShatter;
                float applied = instant ? integrity : raw * (1f - spec.DamageAbsorb);
                if (applied > integrity) applied = integrity;
                float remaining = integrity - applied;
                store.SetIntegrity(id, remaining);

                _events.Add(new TileDamageEvent
                {
                    TileId = id,
                    Applied = applied,
                    Remaining = remaining,
                    Shattered = remaining <= 0f,
                    Source = depth == 0 ? source : DamageSource.Cascade,
                    Depth = depth,
                });

                if (spec.NeighborTransfer <= 0f || depth >= GlassCatalog.MaxCascadeDepth) continue;

                int count = store.GetNeighbors(id, _neighbors);
                int alive = 0;
                for (int i = 0; i < count; i++)
                {
                    int n = _neighbors[i];
                    if (store.GetGlassType(n) != GlassType.None && store.GetIntegrity(n) > 0f) _aliveNeighbors[alive++] = n;
                }
                if (alive == 0) continue;

                float share = raw * spec.NeighborTransfer / alive;
                if (share < GlassCatalog.MinCascadeDamage) continue;

                for (int i = 0; i < alive; i++)
                {
                    int n = _aliveNeighbors[i];
                    if (_visited.Add(n)) _queue.Enqueue((n, share, depth + 1));
                }
            }

            return _events;
        }

        /// <summary>Non-propagating damage (compression, vaulting, weight load, erosion). Still absorbed.</summary>
        public TileDamageEvent ApplyDirect(ITileStore store, int tileId, float rawDamage, DamageSource source)
        {
            GlassType type = store.GetGlassType(tileId);
            float integrity = store.GetIntegrity(tileId);
            if (type == GlassType.None || integrity <= 0f || rawDamage <= 0f)
                return new TileDamageEvent { TileId = tileId, Remaining = integrity, Source = source };

            var spec = GlassCatalog.Get(type);
            // Erosion ignores absorption so the cascade phase can collapse reinforced glass too.
            float absorb = source == DamageSource.Erosion ? 0f : spec.DamageAbsorb;
            float applied = rawDamage * (1f - absorb);
            if (applied > integrity) applied = integrity;
            float remaining = integrity - applied;
            store.SetIntegrity(tileId, remaining);
            return new TileDamageEvent { TileId = tileId, Applied = applied, Remaining = remaining, Shattered = remaining <= 0f, Source = source };
        }
    }
}
