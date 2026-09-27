using System;
using System.Collections.Generic;
using System.Text;

namespace RePlanet.Core
{
    /// <summary>Einfacher 3D-Vektor ohne Unity-Abhängigkeit.</summary>
    [Serializable]
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static readonly V3 Zero = new V3(0, 0, 0);
        public static V3 operator +(V3 a, V3 b) { return new V3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static V3 operator *(V3 a, float s) { return new V3(a.x * s, a.y * s, a.z * s); }
        public float Length { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public float LengthXZ { get { return (float)Math.Sqrt(x * x + z * z); } }
        public static float Dist(V3 a, V3 b) { return (a - b).Length; }
        public static float DistXZ(V3 a, V3 b) { float dx = a.x - b.x, dz = a.z - b.z; return (float)Math.Sqrt(dx * dx + dz * dz); }
        public bool IsFinite { get { return M.Finite(x) && M.Finite(y) && M.Finite(z); } }
        public List<object> ToJson(int dec = 2) { return Json.Arr(Json.R(x, dec), Json.R(y, dec), Json.R(z, dec)); }
        public static V3 FromArr(float[] a)
        {
            if (a == null || a.Length < 3) return Zero;
            return new V3(a[0], a[1], a[2]);
        }
        public override string ToString() { return "(" + x.ToString("0.00") + ", " + y.ToString("0.00") + ", " + z.ToString("0.00") + ")"; }
    }

    public static class M
    {
        public const float PI = 3.14159265f;
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float InvLerp(float a, float b, float v) { return Math.Abs(b - a) < 1e-6f ? 0f : Clamp01((v - a) / (b - a)); }
        public static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
        public static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(Math.Max(0, v)); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Abs(float v) { return v < 0 ? -v : v; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Frac(float v) { return v - (float)Math.Floor(v); }
        public static float WrapAngle(float a)
        {
            while (a > PI) a -= 2 * PI;
            while (a < -PI) a += 2 * PI;
            return a;
        }
        public static float MoveTowardsAngle(float cur, float target, float maxDelta)
        {
            float d = WrapAngle(target - cur);
            if (Math.Abs(d) <= maxDelta) return target;
            return cur + Math.Sign(d) * maxDelta;
        }
    }

    /// <summary>Deterministischer Zufallsgenerator (Mulberry32). Identisch in Client und Server.</summary>
    public class Rng
    {
        uint state;
        public Rng(int seed) { state = (uint)seed ^ 0x9E3779B9u; if (state == 0) state = 1; }
        public uint NextU()
        {
            state += 0x6D2B79F5u;
            uint t = state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return t ^ (t >> 14);
        }
        public float Next() { return (NextU() >> 8) / 16777216f; }
        public float Range(float a, float b) { return a + (b - a) * Next(); }
        public int Range(int a, int bExclusive) { if (bExclusive <= a) return a; return a + (int)(NextU() % (uint)(bExclusive - a)); }
        public bool Chance(float p) { return Next() < p; }
        public T Pick<T>(IList<T> list) { return list[Range(0, list.Count)]; }
    }

    public static class Hash
    {
        public static uint Fnv1a(string s)
        {
            uint h = 2166136261u;
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                h ^= (byte)(c & 0xFF); h *= 16777619u;
                h ^= (byte)(c >> 8); h *= 16777619u;
            }
            return h;
        }
        public static int Int2(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)y * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)(h ^ (h >> 16));
            }
        }
        public static float Float2(int x, int y, int seed) { return ((uint)Int2(x, y, seed) & 0xFFFFFF) / 16777216f; }
    }

    /// <summary>Deterministisches Value-Noise für Terrain.</summary>
    public static class Noise
    {
        public static float Value(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            float a = Hash.Float2(xi, yi, seed), b = Hash.Float2(xi + 1, yi, seed);
            float c = Hash.Float2(xi, yi + 1, seed), d = Hash.Float2(xi + 1, yi + 1, seed);
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return M.Lerp(M.Lerp(a, b, u), M.Lerp(c, d, u), v);
        }
        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0, amp = 0.5f, freq = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x * freq, y * freq, seed + o * 131) * amp;
                norm += amp; amp *= 0.5f; freq *= 2.03f;
            }
            return sum / norm;
        }
    }

    /// <summary>Kompaktes Bitset für entfernte Weltobjekte (Speicherung als Base64).</summary>
    public class BitSet
    {
        byte[] bits;
        public int Count { get; private set; }
        public BitSet(int size) { bits = new byte[(Math.Max(1, size) + 7) / 8]; }
        public int Capacity { get { return bits.Length * 8; } }
        void Ensure(int i)
        {
            if (i < Capacity) return;
            var nb = new byte[Math.Max(bits.Length * 2, (i + 8) / 8)];
            Array.Copy(bits, nb, bits.Length);
            bits = nb;
        }
        public bool Get(int i) { if (i < 0 || i >= Capacity) return false; return (bits[i >> 3] & (1 << (i & 7))) != 0; }
        public bool Set(int i)
        {
            if (i < 0) return false;
            Ensure(i);
            if (Get(i)) return false;
            bits[i >> 3] |= (byte)(1 << (i & 7));
            Count++;
            return true;
        }
        public void Clear() { Array.Clear(bits, 0, bits.Length); Count = 0; }
        public string ToBase64() { return Convert.ToBase64String(bits); }
        public static BitSet FromBase64(string s, int size)
        {
            var b = new BitSet(size);
            if (string.IsNullOrEmpty(s)) return b;
            byte[] data = Convert.FromBase64String(s);
            b.Ensure(data.Length * 8 - 1);
            Array.Copy(data, b.bits, data.Length);
            int c = 0;
            for (int i = 0; i < data.Length * 8; i++) if (b.Get(i)) c++;
            b.Count = c;
            return b;
        }
        public IEnumerable<int> Indices()
        {
            for (int i = 0; i < Capacity; i++) if (Get(i)) yield return i;
        }
    }

    public static class Ids
    {
        static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
        static readonly Random rnd = new Random();
        public static string Code(int len)
        {
            var sb = new StringBuilder(len);
            lock (rnd) for (int i = 0; i < len; i++) sb.Append(Alphabet[rnd.Next(Alphabet.Length)]);
            return sb.ToString();
        }
        public static string Token() { return Guid.NewGuid().ToString("N"); }
    }
}
