namespace Glasscore.Simulation
{
    /// <summary>
    /// xorshift128+ PRNG seeded through SplitMix64. Bit-identical on every platform and runtime
    /// (unlike UnityEngine.Random or System.Random), which is what lets the server send only a
    /// tile ID + impact point and have every client derive the same Voronoi fracture pattern.
    /// </summary>
    public struct DeterministicRandom
    {
        private ulong _s0;
        private ulong _s1;

        public DeterministicRandom(ulong seed)
        {
            ulong z = seed;
            _s0 = SplitMix64(ref z);
            _s1 = SplitMix64(ref z);
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
        }

        public static ulong SplitMix64(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>Uniform float in [0, 1) built from 24 random bits — exact on every FPU.</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive) => minInclusive + (maxExclusive - minInclusive) * NextFloat();

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }
    }

    public static class DeterministicHash
    {
        public static ulong Mix(ulong a, ulong b)
        {
            ulong state = a ^ (b * 0xC2B2AE3D27D4EB4FUL);
            return DeterministicRandom.SplitMix64(ref state);
        }

        public static ulong Combine(ulong a, ulong b, ulong c) => Mix(Mix(a, b), c);

        /// <summary>
        /// Quantizes a world coordinate to whole millimetres so tiny float differences between
        /// server serialization and client deserialization can never change the fracture seed.
        /// </summary>
        public static int QuantizeMm(float value) => (int)System.Math.Round(value * 1000.0);

        /// <summary>
        /// The one canonical fracture seed. The server computes it for validation/replays;
        /// every client recomputes it from the RPC_ShatterTile arguments.
        /// </summary>
        public static ulong FractureSeed(int matchSeed, int tileId, Vec3 impactPoint)
        {
            ulong h = Mix((ulong)(uint)matchSeed, (ulong)(uint)tileId);
            h = Mix(h, (ulong)(uint)QuantizeMm(impactPoint.X));
            h = Mix(h, (ulong)(uint)QuantizeMm(impactPoint.Y));
            return Mix(h, (ulong)(uint)QuantizeMm(impactPoint.Z));
        }
    }
}
