// UnityEngine-Ersatz für die Prüfumgebung: Mathematik, Hierarchie und Komponenten mit Unity-Semantik,
// Darstellung ohne Wirkung (Geometrie wird gesammelt, damit Program.cs sie zählen und rastern kann).
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public enum LightProbeUsage { Off, BlendProbes }
    public enum ReflectionProbeUsage { Off, BlendProbes, BlendProbesAndSkybox, Simple }
}

namespace UnityEngine
{
    using UnityEngine.Rendering;

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 up => new Vector2(0, 1);
        public static Vector2 right => new Vector2(1, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator *(float s, Vector2 a) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : new Vector2(0, 0); } }
        public void Normalize() { this = normalized; }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector2 ClampMagnitude(Vector2 v, float m) => v.magnitude > m ? v.normalized * m : v;
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object o) => o is Vector2 v && v.x == x && v.y == y;
        public override int GetHashCode() => (x, y).GetHashCode();
        public float this[int i] { get => i == 0 ? x : i == 1 ? y : throw new IndexOutOfRangeException("Vector2"); set { if (i == 0) x = value; else if (i == 1) y = value; else throw new IndexOutOfRangeException("Vector2"); } }
        public override string ToString() => $"({x:0.00}, {y:0.00})";
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0);
        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v.x == x && v.y == y && v.z == z;
        public override int GetHashCode() => (x, y, z).GetHashCode();
        public float this[int i] { get => i == 0 ? x : i == 1 ? y : i == 2 ? z : throw new IndexOutOfRangeException("Vector3"); set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else throw new IndexOutOfRangeException("Vector3"); } }
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 Normalize(Vector3 v) => v.normalized;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Lerp(a, b, t);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 ClampMagnitude(Vector3 v, float m) => v.magnitude > m ? v.normalized * m : v;
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float time, float maxSpeed, float dt)
        {
            float vx = vel.x, vy = vel.y, vz = vel.z;
            var r = new Vector3(Mathf.SmoothDamp(c.x, t.x, ref vx, time, maxSpeed, dt), Mathf.SmoothDamp(c.y, t.y, ref vy, time, maxSpeed, dt), Mathf.SmoothDamp(c.z, t.z, ref vz, time, maxSpeed, dt));
            vel = new Vector3(vx, vy, vz); return r;
        }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float time) => SmoothDamp(c, t, ref vel, time, float.PositiveInfinity, Time.deltaTime);
        public static Vector3 Project(Vector3 v, Vector3 n) { float d = Dot(n, n); return d < 1e-12f ? zero : n * (Dot(v, n) / d); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Project(v, n);
        public static Vector3 Reflect(Vector3 v, Vector3 n) => v - 2f * Dot(v, n) * n;
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { var a = t - c; float m = a.magnitude; return m <= d || m == 0 ? t : c + a / m * d; }
        public static float Angle(Vector3 a, Vector3 b)
        {
            float den = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (den < 1e-15f) return 0f;
            return (float)Math.Acos(Mathf.Clamp(Dot(a, b) / den, -1f, 1f)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis) { float ang = Angle(a, b); return Dot(axis, Cross(a, b)) < 0 ? -ang : ang; }
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1);
        public static Color grey => gray;
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color yellow => new Color(1, 0.92f, 0.016f, 1);
        public static Color cyan => new Color(0, 1, 1, 1);
        public static Color magenta => new Color(1, 0, 1, 1);
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a * s);
        public static Color operator *(float s, Color c) => c * s;
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator /(Color c, float s) => new Color(c.r / s, c.g / s, c.b / s, c.a / s);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static bool operator ==(Color a, Color b) => a.Equals(b);
        public static bool operator !=(Color a, Color b) => !a.Equals(b);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static Color LerpUnclamped(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        public static implicit operator Color32(Color c) => new Color32((byte)Mathf.Clamp(Mathf.Round(c.r * 255), 0, 255), (byte)Mathf.Clamp(Mathf.Round(c.g * 255), 0, 255), (byte)Mathf.Clamp(Mathf.Round(c.b * 255), 0, 255), (byte)Mathf.Clamp(Mathf.Round(c.a * 255), 0, 255));
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        public static implicit operator Color(Vector4 v) => new Color(v.x, v.y, v.z, v.w);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public float maxColorComponent => Math.Max(r, Math.Max(g, b));
        public Color linear => new Color(Mathf.Pow(r, 2.2f), Mathf.Pow(g, 2.2f), Mathf.Pow(b, 2.2f), a);
        public Color gamma => new Color(Mathf.Pow(r, 1 / 2.2f), Mathf.Pow(g, 1 / 2.2f), Mathf.Pow(b, 1 / 2.2f), a);
        public float this[int i] { get => i == 0 ? r : i == 1 ? g : i == 2 ? b : i == 3 ? a : throw new IndexOutOfRangeException("Color"); }
        public static Color HSVToRGB(float h, float s, float v)
        {
            h = Mathf.Repeat(h, 1f) * 6f; int i = (int)Math.Floor(h); float f = h - i;
            float p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            switch (i % 6) { case 0: return new Color(v, t, p); case 1: return new Color(q, v, p); case 2: return new Color(p, v, t); case 3: return new Color(p, q, v); case 4: return new Color(t, p, v); default: return new Color(v, p, q); }
        }
        public static void RGBToHSV(Color c, out float h, out float s, out float v)
        {
            float max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b)), d = max - min;
            v = max; s = max > 0 ? d / max : 0; h = 0;
            if (d > 0) { if (max == c.r) h = ((c.g - c.b) / d) % 6; else if (max == c.g) h = (c.b - c.r) / d + 2; else h = (c.r - c.g) / d + 4; h /= 6f; if (h < 0) h += 1; }
        }
        public override int GetHashCode() => (r, g, b, a).GetHashCode();
        public override bool Equals(object o) => o is Color c && c.r == r && c.g == g && c.b == b && c.a == a;
        public override string ToString() => $"RGBA({r:0.000}, {g:0.000}, {b:0.000}, {a:0.000})";
    }

    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }

    public static class ColorUtility
    {
        public static string ToHtmlStringRGBA(Color c) { Color32 k = c; return $"{k.r:X2}{k.g:X2}{k.b:X2}{k.a:X2}"; }
        public static string ToHtmlStringRGB(Color c) { Color32 k = c; return $"{k.r:X2}{k.g:X2}{k.b:X2}"; }
        public static bool TryParseHtmlString(string s, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.TrimStart('#');
            try
            {
                if (s.Length != 6 && s.Length != 8) return false;
                byte P(int i) => Convert.ToByte(s.Substring(i, 2), 16);
                c = new Color32(P(0), P(2), P(4), s.Length == 8 ? P(6) : (byte)255);
                return true;
            }
            catch { return false; }
        }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        static Quaternion Axis(Vector3 a, float deg)
        {
            float r = deg * Mathf.Deg2Rad * 0.5f; float s = (float)Math.Sin(r);
            a = a.normalized; return new Quaternion(a.x * s, a.y * s, a.z * s, (float)Math.Cos(r));
        }
        public static Quaternion AngleAxis(float deg, Vector3 axis) => Axis(axis, deg);
        public static Quaternion Euler(float x, float y, float z) => Axis(Vector3.up, y) * Axis(Vector3.right, x) * Axis(Vector3.forward, z);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            var t = Vector3.Cross(u, v) * 2f;
            return v + t * q.w + Vector3.Cross(u, t);
        }
        public static bool operator ==(Quaternion a, Quaternion b) => Dot(a, b) > 0.999999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public override bool Equals(object o) => o is Quaternion q && q.x == x && q.y == y && q.z == z && q.w == w;
        public override int GetHashCode() => (x, y, z, w).GetHashCode();
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static Quaternion Inverse(Quaternion q) { float n = Dot(q, q); if (n < 1e-12f) return identity; return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n); }
        public Quaternion normalized { get { float n = (float)Math.Sqrt(Dot(this, this)); return n < 1e-6f ? identity : new Quaternion(x / n, y / n, z / n, w / n); } }
        public static float Angle(Quaternion a, Quaternion b) { float d = Math.Min(Math.Abs(Dot(a, b)), 1f); return d > 0.999999f ? 0f : (float)Math.Acos(d) * 2f * Mathf.Rad2Deg; }
        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            from = from.normalized; to = to.normalized;
            float d = Vector3.Dot(from, to);
            if (d > 0.99999f) return identity;
            if (d < -0.99999f)
            {
                var ax = Vector3.Cross(Vector3.right, from);
                if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(Vector3.up, from);
                return Axis(ax, 180f);
            }
            var c = Vector3.Cross(from, to);
            return Axis(c, (float)Math.Acos(Mathf.Clamp(d, -1, 1)) * Mathf.Rad2Deg);
        }
        public static Quaternion LookRotation(Vector3 f, Vector3 up)
        {
            // Unity: Nullvektor → Meldung „Look rotation viewing vector is zero“ und Identität
            if (f.sqrMagnitude < 1e-12f) { UnityWarnings.Add("Look rotation viewing vector is zero"); return identity; }
            NanGuard.Check(f, "Quaternion.LookRotation");
            f = f.normalized;
            var r = Vector3.Cross(up, f).normalized;
            if (r.sqrMagnitude < 1e-6f) r = Vector3.Cross(Vector3.forward, f).normalized;
            if (r.sqrMagnitude < 1e-6f) r = Vector3.Cross(Vector3.right, f).normalized;
            var u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22;
            if (tr > 0) { float s = (float)Math.Sqrt(tr + 1) * 2; return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            if (m00 > m11 && m00 > m22) { float s = (float)Math.Sqrt(1 + m00 - m11 - m22) * 2; return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            if (m11 > m22) { float s = (float)Math.Sqrt(1 + m11 - m00 - m22) * 2; return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            { float s = (float)Math.Sqrt(1 + m22 - m00 - m11) * 2; return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
        }
        public static Quaternion LookRotation(Vector3 f) => LookRotation(f, Vector3.up);
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float d = Dot(a, b);
            if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            if (d > 0.9995f) return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            float th = (float)Math.Acos(d), s = (float)Math.Sin(th);
            float wa = (float)Math.Sin((1 - t) * th) / s, wb = (float)Math.Sin(t * th) / s;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Slerp(a, b, t);
        public static Quaternion RotateTowards(Quaternion a, Quaternion b, float maxDeg) { float ang = Angle(a, b); return ang < 1e-4f ? b : SlerpUnclamped(a, b, Math.Min(1f, maxDeg / ang)); }
        public Vector3 eulerAngles
        {
            get
            {
                // Unity-Reihenfolge ZXY (y, dann x, dann z)
                float sinx = 2f * (w * x - y * z);
                sinx = Mathf.Clamp(sinx, -1f, 1f);
                float ex = (float)Math.Asin(sinx);
                float ey, ez;
                if (Math.Abs(sinx) < 0.9999f)
                {
                    ey = (float)Math.Atan2(2f * (w * y + x * z), 1f - 2f * (x * x + y * y));
                    ez = (float)Math.Atan2(2f * (w * z + x * y), 1f - 2f * (x * x + z * z));
                }
                else { ey = (float)Math.Atan2(-2f * (x * z - w * y), 1f - 2f * (y * y + z * z)); ez = 0; }
                return new Vector3(Mathf.Repeat(ex * Mathf.Rad2Deg, 360f), Mathf.Repeat(ey * Mathf.Rad2Deg, 360f), Mathf.Repeat(ez * Mathf.Rad2Deg, 360f));
            }
            set { this = Euler(value); }
        }
        public override string ToString() => $"({x:0.000}, {y:0.000}, {z:0.000}, {w:0.000})";
    }

    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
        public static Matrix4x4 identity => new Matrix4x4 { m00 = 1, m11 = 1, m22 = 1, m33 = 1 };
        public static Matrix4x4 zero => new Matrix4x4();
        public static Matrix4x4 TRS(Vector3 t, Quaternion q, Vector3 s)
        {
            var c0 = q * new Vector3(s.x, 0, 0); var c1 = q * new Vector3(0, s.y, 0); var c2 = q * new Vector3(0, 0, s.z);
            return new Matrix4x4 { m00 = c0.x, m10 = c0.y, m20 = c0.z, m01 = c1.x, m11 = c1.y, m21 = c1.z, m02 = c2.x, m12 = c2.y, m22 = c2.z, m03 = t.x, m13 = t.y, m23 = t.z, m33 = 1 };
        }
        public static Matrix4x4 Scale(Vector3 s) => new Matrix4x4 { m00 = s.x, m11 = s.y, m22 = s.z, m33 = 1 };
        public static Matrix4x4 Translate(Vector3 t) => TRS(t, Quaternion.identity, Vector3.one);
        public static Matrix4x4 Rotate(Quaternion q) => TRS(Vector3.zero, q, Vector3.one);
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            float[] A = a.Arr(), B = b.Arr(), R = new float[16];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += A[i * 4 + k] * B[k * 4 + j]; R[i * 4 + j] = s; }
            r.Set(R); return r;
        }
        public static Vector4 operator *(Matrix4x4 m, Vector4 v) => new Vector4(
            m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03 * v.w, m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13 * v.w,
            m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23 * v.w, m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33 * v.w);
        float[] Arr() => new[] { m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33 };
        void Set(float[] R) { m00 = R[0]; m01 = R[1]; m02 = R[2]; m03 = R[3]; m10 = R[4]; m11 = R[5]; m12 = R[6]; m13 = R[7]; m20 = R[8]; m21 = R[9]; m22 = R[10]; m23 = R[11]; m30 = R[12]; m31 = R[13]; m32 = R[14]; m33 = R[15]; }
        public float this[int r, int c] { get => Arr()[r * 4 + c]; set { var a = Arr(); a[r * 4 + c] = value; Set(a); } }
        public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23);
        public Vector3 MultiplyPoint(Vector3 p)
        {
            var v = MultiplyPoint3x4(p); float w = m30 * p.x + m31 * p.y + m32 * p.z + m33;
            return Math.Abs(w - 1f) < 1e-7f || w == 0 ? v : v / w;
        }
        public Vector3 MultiplyVector(Vector3 p) => new Vector3(m00 * p.x + m01 * p.y + m02 * p.z, m10 * p.x + m11 * p.y + m12 * p.z, m20 * p.x + m21 * p.y + m22 * p.z);
        public Vector4 GetColumn(int i) => i == 0 ? new Vector4(m00, m10, m20, m30) : i == 1 ? new Vector4(m01, m11, m21, m31) : i == 2 ? new Vector4(m02, m12, m22, m32) : new Vector4(m03, m13, m23, m33);
        public Vector3 GetPosition() => new Vector3(m03, m13, m23);
        public Vector3 lossyScale => new Vector3(((Vector3)GetColumn(0)).magnitude, ((Vector3)GetColumn(1)).magnitude, ((Vector3)GetColumn(2)).magnitude);
        public Quaternion rotation => Quaternion.LookRotation(GetColumn(2), GetColumn(1));
        public Matrix4x4 transpose { get { var a = Arr(); var t = new float[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) t[i * 4 + j] = a[j * 4 + i]; var r = new Matrix4x4(); r.Set(t); return r; } }
        public Matrix4x4 inverse
        {
            get
            {
                var a = Arr(); var inv = new double[16]; var m = new double[16]; for (int i = 0; i < 16; i++) m[i] = a[i];
                // Gauß-Jordan
                var aug = new double[4, 8];
                for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) aug[i, j] = m[i * 4 + j]; aug[i, 4 + i] = 1; }
                for (int c = 0; c < 4; c++)
                {
                    int piv = c; for (int r = c + 1; r < 4; r++) if (Math.Abs(aug[r, c]) > Math.Abs(aug[piv, c])) piv = r;
                    if (Math.Abs(aug[piv, c]) < 1e-12) return zero; // Unity liefert für singuläre Matrizen die Nullmatrix
                    if (piv != c) for (int j = 0; j < 8; j++) { var t = aug[c, j]; aug[c, j] = aug[piv, j]; aug[piv, j] = t; }
                    double d = aug[c, c]; for (int j = 0; j < 8; j++) aug[c, j] /= d;
                    for (int r = 0; r < 4; r++) if (r != c) { double f = aug[r, c]; for (int j = 0; j < 8; j++) aug[r, j] -= f * aug[c, j]; }
                }
                var res = new float[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) res[i * 4 + j] = (float)aug[i, 4 + j];
                var o = new Matrix4x4(); o.Set(res); return o;
            }
        }
        public bool ValidTRS() => true;
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 c, Vector3 s) { center = c; size = s; }
        public Vector3 extents { get => size * 0.5f; set => size = value * 2f; }
        public Vector3 min => center - extents; public Vector3 max => center + extents;
        public void Encapsulate(Vector3 p) { var mn = Vector3.Min(min, p); var mx = Vector3.Max(max, p); center = (mn + mx) * 0.5f; size = mx - mn; }
        public void Encapsulate(Bounds b) { Encapsulate(b.min); Encapsulate(b.max); }
        public void Expand(float a) { size += Vector3.one * a; }
        public bool Contains(Vector3 p) => p.x >= min.x && p.y >= min.y && p.z >= min.z && p.x <= max.x && p.y <= max.y && p.z <= max.z;
        public bool Intersects(Bounds b) => min.x <= b.max.x && max.x >= b.min.x && min.y <= b.max.y && max.y >= b.min.y && min.z <= b.max.z && max.z >= b.min.z;
        public float SqrDistance(Vector3 p)
        {
            var h = size * 0.5f; float dx = Math.Max(0, Math.Abs(p.x - center.x) - h.x), dy = Math.Max(0, Math.Abs(p.y - center.y) - h.y), dz = Math.Max(0, Math.Abs(p.z - center.z) - h.z);
            return dx * dx + dy * dy + dz * dz;
        }
    }
    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d.normalized; }
        public Vector3 GetPoint(float t) => origin + direction * t;
    }
    public struct Plane
    {
        public Vector3 normal; public float distance;
        public Plane(Vector3 n, Vector3 p) { normal = n.normalized; distance = -Vector3.Dot(normal, p); }
        public Plane(Vector3 n, float d) { normal = n.normalized; distance = d; }
        public bool Raycast(Ray r, out float enter)
        {
            float vd = Vector3.Dot(r.direction, normal), np = -Vector3.Dot(r.origin, normal) - distance;
            if (Math.Abs(vd) < 1e-6f) { enter = 0; return false; }
            enter = np / vd; return enter > 0;
        }
        public float GetDistanceToPoint(Vector3 p) => Vector3.Dot(normal, p) + distance;
    }
    public static class GeometryUtility
    {
        public static void CalculateFrustumPlanes(Camera c, Plane[] p) { if (p == null || p.Length < 6) throw new ArgumentException("Planes array must be of length 6."); }
        public static Plane[] CalculateFrustumPlanes(Camera c) => new Plane[6];
        public static bool TestPlanesAABB(Plane[] p, Bounds b) => true;
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI, Deg2Rad = PI / 180f, Rad2Deg = 180f / PI, Epsilon = 1.401298E-45f, Infinity = float.PositiveInfinity, NegativeInfinity = float.NegativeInfinity;
        public static float Sin(float v) => (float)Math.Sin(v); public static float Cos(float v) => (float)Math.Cos(v); public static float Tan(float v) => (float)Math.Tan(v);
        public static float Asin(float v) => (float)Math.Asin(v); public static float Acos(float v) => (float)Math.Acos(v); public static float Atan(float v) => (float)Math.Atan(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x); public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Exp(float v) => (float)Math.Exp(v); public static float Log(float v) => (float)Math.Log(v); public static float Log(float v, float b) => (float)Math.Log(v, b); public static float Log10(float v) => (float)Math.Log10(v);
        public static float Abs(float v) => Math.Abs(v); public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b); public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(params float[] v) { if (v.Length == 0) return 0; float m = v[0]; foreach (var x in v) m = Math.Min(m, x); return m; }
        public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(params float[] v) { if (v.Length == 0) return 0; float m = v[0]; foreach (var x in v) m = Math.Max(m, x); return m; }
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v; public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float LerpAngle(float a, float b, float t) => a + DeltaAngle(a, b) * Clamp01(t);
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static float SmoothDamp(float c, float target, ref float vel, float time, float maxSpeed, float dt)
        {
            time = Math.Max(0.0001f, time); float omega = 2f / time, x = omega * dt, exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = c - target, orig = target, maxC = maxSpeed * time; change = Clamp(change, -maxC, maxC); target = c - change;
            float temp = (vel + omega * change) * dt; vel = (vel - omega * temp) * exp; float o = target + (change + temp) * exp;
            if (orig - c > 0f == o > orig) { o = orig; vel = (o - orig) / dt; }
            return o;
        }
        public static float SmoothDamp(float c, float target, ref float vel, float time) => SmoothDamp(c, target, ref vel, time, float.PositiveInfinity, Time.deltaTime);
        public static float Round(float v) => (float)Math.Round(v, MidpointRounding.ToEven);
        public static float Floor(float v) => (float)Math.Floor(v); public static float Ceil(float v) => (float)Math.Ceiling(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v); public static int CeilToInt(float v) => (int)Math.Ceiling(v); public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static float Repeat(float t, float l) => Clamp(t - (float)Math.Floor(t / l) * l, 0f, l);
        public static float PingPong(float t, float l) { t = Repeat(t, l * 2f); return l - Math.Abs(t - l); }
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static float MoveTowardsAngle(float c, float t, float d) { float dl = DeltaAngle(c, t); if (-d < dl && dl < d) return t; return MoveTowards(c, c + dl, d); }
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return d; }
        public static float Sign(float v) => v >= 0 ? 1 : -1;
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;
        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p <<= 1; return p; }
        public static float GammaToLinearSpace(float v) => Pow(v, 2.2f);
        public static float LinearToGammaSpace(float v) => Pow(v, 1 / 2.2f);

        /// <summary>Klassisches Perlin-Rauschen (Werte ca. 0…1 wie in Unity).</summary>
        public static float PerlinNoise(float x, float y)
        {
            int xi = (int)Math.Floor(x) & 255, yi = (int)Math.Floor(y) & 255;
            float xf = x - (float)Math.Floor(x), yf = y - (float)Math.Floor(y);
            float u = Fade(xf), v = Fade(yf);
            int aa = P[P[xi] + yi], ab = P[P[xi] + yi + 1], ba = P[P[xi + 1] + yi], bb = P[P[xi + 1] + yi + 1];
            float x1 = LerpUnclamped(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = LerpUnclamped(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Clamp01((LerpUnclamped(x1, x2, v) + 1f) * 0.5f);
        }
        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Grad(int h, float x, float y) { switch (h & 3) { case 0: return x + y; case 1: return -x + y; case 2: return x - y; default: return -x - y; } }
        static readonly int[] P = MakePerm();
        static int[] MakePerm() { var r = new System.Random(7); var p = new int[512]; var b = new int[256]; for (int i = 0; i < 256; i++) b[i] = i; for (int i = 255; i > 0; i--) { int j = r.Next(i + 1); (b[i], b[j]) = (b[j], b[i]); } for (int i = 0; i < 512; i++) p[i] = b[i & 255]; return p; }
    }

    public static class Random
    {
        static System.Random r = new System.Random(1);
        public static void InitState(int s) { r = new System.Random(s); }
        public static float value => (float)r.NextDouble();
        public static float Range(float a, float b) => a + (b - a) * value;
        public static int Range(int a, int b) => b <= a ? a : r.Next(a, b);
        public static Vector3 insideUnitSphere { get { Vector3 v; do { v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); } while (v.sqrMagnitude > 1f); return v; } }
        public static Vector3 onUnitSphere { get { Vector3 v; do { v = insideUnitSphere; } while (v.sqrMagnitude < 1e-4f); return v.normalized; } }
        public static Vector2 insideUnitCircle { get { Vector2 v; do { v = new Vector2(Range(-1f, 1f), Range(-1f, 1f)); } while (v.sqrMagnitude > 1f); return v; } }
        public static Quaternion rotation => Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
        public static Color ColorHSV() => Color.HSVToRGB(value, value, value);
    }

    public static class Time
    {
        public static float deltaTime = 0.02f, time = 10f, unscaledDeltaTime = 0.02f, unscaledTime = 10f, realtimeSinceStartup = 10f, timeScale = 1f, smoothDeltaTime = 0.02f, fixedDeltaTime = 0.02f, timeSinceLevelLoad = 10f;
        public static int frameCount = 1;
        /// <summary>Prüfumgebung: ein Bild weiter.</summary>
        public static void Advance(float dt)
        {
            deltaTime = dt * timeScale; unscaledDeltaTime = dt; smoothDeltaTime = dt;
            time += deltaTime; unscaledTime += dt; realtimeSinceStartup += dt; timeSinceLevelLoad += dt; frameCount++;
        }
    }

    /// <summary>Warnungen, die Unity nur in die Konsole schreibt (z. B. LookRotation mit Nullvektor).</summary>
    public static class UnityWarnings
    {
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public static void Add(string w) { Counts.TryGetValue(w, out var n); Counts[w] = n + 1; }
    }

    public static class Debug
    {
        public static readonly List<string> Errors = new List<string>();
        public static readonly List<string> Warnings = new List<string>();
        public static bool Quiet;
        public static void Log(object o) { if (!Quiet) Console.WriteLine(o); }
        public static void LogFormat(string f, params object[] a) => Log(string.Format(f, a));
        public static void LogWarning(object o) { Warnings.Add(o?.ToString()); Console.WriteLine("WARN " + o); }
        public static void LogWarningFormat(string f, params object[] a) => LogWarning(string.Format(f, a));
        public static void LogError(object o) { Errors.Add(o?.ToString()); Console.WriteLine("ERROR " + o); }
        public static void LogException(Exception e) { Errors.Add(e.ToString()); Console.WriteLine("EXC " + e); }
        public static void LogException(Exception e, Object ctx) => LogException(e);
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
        public static void DrawRay(Vector3 a, Vector3 b, Color c) { }
        public static bool isDebugBuild => true;
    }
    public static class SystemInfo
    {
        public static bool supportsInstancing => true;
        public static bool supportsComputeShaders => false;
        public static int graphicsMemorySize => 4096; public static int systemMemorySize => 16384; public static int maxTextureSize => 16384;
        public static string graphicsDeviceName => "Prüfumgebung"; public static string operatingSystem => "Linux"; public static string deviceUniqueIdentifier => "harness";
        public static bool SupportsRenderTextureFormat(RenderTextureFormat f) => true;
        public static bool SupportsTextureFormat(TextureFormat f) => true;
    }

    // ------------------------------------------------------------------ Objekte, Komponenten, Hierarchie
    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;
        internal bool destroyedObj;
        public static readonly List<Object> PendingDestroy = new List<Object>();
        public static void Destroy(Object o) { if (o is null) return; PendingDestroy.Add(o); }
        public static void Destroy(Object o, float t) => Destroy(o);
        public static void DestroyImmediate(Object o) { if (o is null) return; MarkDestroyed(o); }
        /// <summary>Unity zerstört am Bildende – die Prüfumgebung ruft das nach jedem Bild auf.</summary>
        public static void FlushDestroy() { var l = new List<Object>(PendingDestroy); PendingDestroy.Clear(); foreach (var o in l) MarkDestroyed(o); }
        static void MarkDestroyed(Object o)
        {
            if (o is GameObject g) { g.DestroyTree(); return; }
            if (o is Transform) { Debug.LogError("Destroying the transform component is not permitted"); return; }
            if (o is Component c) { c.destroyedObj = true; c.gameObject?.comps.Remove(c); World.Unregister(c); return; }
            o.destroyedObj = true;
        }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T o) where T : Object => Instantiate(o, null);
        public static T Instantiate<T>(T o, Transform parent) where T : Object
        {
            if (o is null || o.IsDead) throw new ArgumentException("The Object you want to instantiate is null.");
            GameObject src = o as GameObject ?? (o as Component)?.gameObject;
            if (src == null) return o; // Assets (Mesh, Material) – Kopie ohne Prüfwert
            var copy = CloneTree(src);
            copy.name = src.name + "(Clone)";
            if (parent != null) copy.transform.SetParent(parent, false);
            if (o is GameObject) return copy as T;
            return copy.GetComponent(o.GetType()) as T;
        }
        static readonly System.Reflection.MethodInfo memberwise = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static GameObject CloneTree(GameObject src)
        {
            var g = new GameObject(src.name) { activeSelf = src.activeSelf, tag = src.tag, layer = src.layer };
            g.transform.localPosition = src.transform.localPosition; g.transform.localRotation = src.transform.localRotation; g.transform.localScale = src.transform.localScale;
            foreach (var c in src.comps)
            {
                if (c is Transform) continue;
                var k = (Component)memberwise.Invoke(c, null);
                k.gameObject = g; g.comps.Add(k); World.Register(k); World.OnAdded?.Invoke(k);
            }
            foreach (var ch in new List<Transform>(src.transform.children)) CloneTree(ch.gameObject).transform.SetParent(g.transform, false);
            return g;
        }
        public static T FindObjectOfType<T>() where T : Object { foreach (var c in World.All) if (c is T t && !c.destroyedObj) return t; return null; }
        public static T[] FindObjectsOfType<T>() where T : Object { var l = new List<T>(); foreach (var c in World.All) if (c is T t && !c.destroyedObj) l.Add(t); return l.ToArray(); }
        public virtual bool IsDead => destroyedObj;
        public static bool operator ==(Object a, Object b)
        {
            bool an = a is null || a.IsDead, bn = b is null || b.IsDead;
            if (an || bn) return an && bn;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static implicit operator bool(Object o) => o != null;
        public int GetInstanceID() => GetHashCode();
        public override string ToString() => name + " (" + GetType().Name + ")";
    }

    /// <summary>Alle lebenden Komponenten – für den Bildtakt der Prüfumgebung (Awake/Start/Update/LateUpdate per Reflexion).</summary>
    public static class World
    {
        public static readonly HashSet<Component> All = new HashSet<Component>();
        public static readonly List<MonoBehaviour> Behaviours = new List<MonoBehaviour>();
        public static readonly HashSet<GameObject> Objects = new HashSet<GameObject>();
        public static void Register(Component c) { All.Add(c); if (c is MonoBehaviour b) Behaviours.Add(b); }
        public static void Unregister(Component c) { All.Remove(c); if (c is MonoBehaviour b) Behaviours.Remove(b); }
        public static Action<Component> OnAdded;
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public override bool IsDead => destroyedObj || (gameObject is object && gameObject.destroyedObj);
        /// <summary>Wie Unity: Zugriff auf ein zerstörtes Objekt wirft MissingReferenceException.</summary>
        public Transform transform { get { if (IsDead) throw new MissingReferenceException("The object of type '" + GetType().Name + "' has been destroyed but you are still trying to access it."); return gameObject.transform; } }
        public string tag { get => gameObject.tag; set => gameObject.tag = value; }
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public Component GetComponent(Type t) => gameObject.GetComponent(t);
        public bool TryGetComponent<T>(out T c) where T : Component { c = GetComponent<T>(); return c != null; }
        public T GetComponentInChildren<T>() where T : Component => gameObject.GetComponentInChildren<T>();
        public T GetComponentInParent<T>() where T : Component { var t = transform; while (t != null) { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; t = t.parent; } return null; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component => gameObject.GetComponentsInChildren<T>(inactive);
        public T[] GetComponentsInChildren<T>() where T : Component => gameObject.GetComponentsInChildren<T>(false);
        public T[] GetComponents<T>() where T : Component => gameObject.GetComponents<T>();
        public T AddComponent<T>() where T : Component => gameObject.AddComponent<T>();
        public bool CompareTag(string t) => gameObject.tag == t;
        public void SendMessage(string m) { }
    }
    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy && !IsDead;
    }
    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s) { } }
    public class WaitForEndOfFrame : YieldInstruction { }
    public class MonoBehaviour : Behaviour
    {
        internal bool awoken, started;
        public bool useGUILayout = true;
        /// <summary>Koroutinen laufen bis zum ersten yield sofort; der Rest wird bei jedem Bild fortgesetzt (Wartezeiten ignoriert).</summary>
        public Coroutine StartCoroutine(IEnumerator e) { Coroutines.Add(e); return new Coroutine(); }
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { Coroutines.Clear(); }
        internal readonly List<IEnumerator> Coroutines = new List<IEnumerator>();
        public void Invoke(string m, float t) { }
        public void CancelInvoke() { }
        public static void print(object o) => Debug.Log(o);
    }

    public enum Space { World, Self }

    public class Transform : Component, IEnumerable
    {
        Transform _parent;
        public readonly List<Transform> children = new List<Transform>();
        Vector3 _lp = Vector3.zero, _ls = Vector3.one;
        Quaternion _lr = Quaternion.identity;
        public Vector3 localPosition { get => _lp; set { NanGuard.Check(value, "Transform.localPosition (" + gameObject?.name + ")"); _lp = value; } }
        public Vector3 localScale { get => _ls; set { NanGuard.Check(value, "Transform.localScale (" + gameObject?.name + ")"); _ls = value; } }
        public Quaternion localRotation { get => _lr; set { NanGuard.Check(value, "Transform.localRotation (" + gameObject?.name + ")"); _lr = value.normalized; } }
        public Transform parent { get => _parent; set => SetParent(value, true); }
        public int childCount => children.Count;
        public Transform GetChild(int i) { if (i < 0 || i >= children.Count) throw new UnityException("Transform child out of bounds"); return children[i]; }
        public IEnumerator GetEnumerator() => new List<Transform>(children).GetEnumerator();
        public Transform root { get { var t = this; while (t._parent != null) t = t._parent; return t; } }
        public void SetParent(Transform p) => SetParent(p, true);
        public void SetParent(Transform p, bool worldPositionStays)
        {
            if (p == this) throw new UnityException("Cannot parent a transform to itself");
            Vector3 wp = position; Quaternion wr = rotation; Vector3 ws = lossyScale;
            _parent?.children.Remove(this); _parent = p; p?.children.Add(this);
            if (worldPositionStays)
            {
                position = wp; rotation = wr;
                var ps = p != null ? p.lossyScale : Vector3.one;
                localScale = new Vector3(ps.x != 0 ? ws.x / ps.x : 0, ps.y != 0 ? ws.y / ps.y : 0, ps.z != 0 ? ws.z / ps.z : 0);
            }
        }
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public void DetachChildren() { foreach (var c in new List<Transform>(children)) c.SetParent(null, true); }
        public Transform Find(string n) { foreach (var c in children) if (c.gameObject.name == n) return c; return null; }
        public Matrix4x4 localToWorldMatrix => (_parent != null ? _parent.localToWorldMatrix : Matrix4x4.identity) * Matrix4x4.TRS(_lp, _lr, _ls);
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;
        public Vector3 position
        {
            get => localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            set { NanGuard.Check(value, "Transform.position (" + gameObject?.name + ")"); _lp = _parent == null ? value : _parent.localToWorldMatrix.inverse.MultiplyPoint3x4(value); }
        }
        public Quaternion rotation
        {
            get { var q = _lr; var p = _parent; while (p != null) { q = p._lr * q; p = p._parent; } return q; }
            set { NanGuard.Check(value, "Transform.rotation (" + gameObject?.name + ")"); _lr = (_parent == null ? value : Quaternion.Inverse(_parent.rotation) * value).normalized; }
        }
        public Vector3 lossyScale { get { var s = _ls; var p = _parent; while (p != null) { s = Vector3.Scale(s, p._ls); p = p._parent; } return s; } }
        public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value); }
        public Vector3 localEulerAngles { get => _lr.eulerAngles; set => localRotation = Quaternion.Euler(value); }
        public Vector3 forward { get => rotation * Vector3.forward; set => rotation = Quaternion.LookRotation(value); }
        public Vector3 right { get => rotation * Vector3.right; set => rotation = Quaternion.FromToRotation(Vector3.right, value); }
        public Vector3 up { get => rotation * Vector3.up; set => rotation = Quaternion.FromToRotation(Vector3.up, value); }
        public Vector3 TransformPoint(Vector3 p) => localToWorldMatrix.MultiplyPoint3x4(p);
        public Vector3 TransformPoint(float x, float y, float z) => TransformPoint(new Vector3(x, y, z));
        public Vector3 InverseTransformPoint(Vector3 p) => worldToLocalMatrix.MultiplyPoint3x4(p);
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public Vector3 TransformVector(Vector3 v) => localToWorldMatrix.MultiplyVector(v);
        public Vector3 InverseTransformVector(Vector3 v) => worldToLocalMatrix.MultiplyVector(v);
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void SetLocalPositionAndRotation(Vector3 p, Quaternion r) { localPosition = p; localRotation = r; }
        public void Rotate(float x, float y, float z, Space s) { if (s == Space.Self) localRotation = _lr * Quaternion.Euler(x, y, z); else rotation = Quaternion.Euler(x, y, z) * rotation; }
        public void Rotate(float x, float y, float z) { Rotate(x, y, z, Space.Self); }
        public void Rotate(Vector3 e, Space s = Space.Self) => Rotate(e.x, e.y, e.z, s);
        public void Rotate(Vector3 axis, float angle, Space s = Space.Self) { if (s == Space.Self) localRotation = _lr * Quaternion.AngleAxis(angle, axis); else rotation = Quaternion.AngleAxis(angle, axis) * rotation; }
        public void RotateAround(Vector3 pt, Vector3 axis, float ang) { var q = Quaternion.AngleAxis(ang, axis); position = pt + q * (position - pt); rotation = q * rotation; }
        public void Translate(Vector3 d, Space s = Space.Self) { if (s == Space.Self) position += TransformDirection(d); else position += d; }
        public void Translate(float x, float y, float z) => Translate(new Vector3(x, y, z));
        public void LookAt(Vector3 p) { var d = p - position; if (d.sqrMagnitude > 1e-12f) rotation = Quaternion.LookRotation(d); }
        public void LookAt(Vector3 p, Vector3 up) { var d = p - position; if (d.sqrMagnitude > 1e-12f) rotation = Quaternion.LookRotation(d, up); }
        public void LookAt(Transform t) => LookAt(t.position);
        public bool IsChildOf(Transform p) { var t = this; while (t != null) { if (t == p) return true; t = t._parent; } return false; }
        public int GetSiblingIndex() => _parent != null ? _parent.children.IndexOf(this) : 0;
    }

    public class UnityException : Exception { public UnityException(string m) : base(m) { } }
    public class MissingReferenceException : Exception { public MissingReferenceException(string m) : base(m) { } }

    public struct Scene { public bool IsValid() => true; public string name => "Harness"; }

    public class GameObject : Object
    {
        public bool activeSelf = true;
        public string tag = "Untagged";
        public int layer;
        public bool isStatic;
        public readonly List<Component> comps = new List<Component>();
        internal Transform tr;
        public Transform transform { get { if (destroyedObj) throw new MissingReferenceException("The object of type 'GameObject' (" + name + ") has been destroyed but you are still trying to access it."); return tr; } }
        public Scene scene => new Scene();
        public GameObject() : this("GameObject") { }
        public GameObject(string n, params Type[] components)
        {
            name = n; tr = new Transform { gameObject = this }; comps.Add(tr); World.Objects.Add(this);
            foreach (var t in components) AddComponent(t);
        }
        public GameObject gameObject => this;
        public bool activeInHierarchy { get { if (destroyedObj) return false; var t = tr; while (t != null) { if (!t.gameObject.activeSelf) return false; t = t.parent; } return true; } }
        internal void DestroyTree()
        {
            if (destroyedObj) return;
            foreach (var c in new List<Transform>(tr.children)) c.gameObject.DestroyTree();
            foreach (var c in comps) CallIfExists(c, "OnDestroy");
            destroyedObj = true;
            foreach (var c in comps) { c.destroyedObj = true; World.Unregister(c); }
            World.Objects.Remove(this);
            tr.parent?.children.Remove(tr);
        }
        internal static void CallIfExists(Component c, string m)
        {
            var mi = c.GetType().GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi == null || mi.GetParameters().Length != 0) return;
            try { mi.Invoke(c, null); }
            catch (System.Reflection.TargetInvocationException e) { Debug.LogException(e.InnerException); }
        }
        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));
        public Component AddComponent(Type t)
        {
            if (destroyedObj) throw new MissingReferenceException("AddComponent auf zerstörtem GameObject " + name);
            if (t == typeof(Transform)) return tr;
            var c = (Component)Activator.CreateInstance(t, true);
            c.gameObject = this; comps.Add(c);
            World.Register(c);
            Harness.OnAdd(c);
            World.OnAdded?.Invoke(c);
            return c;
        }
        public T GetComponent<T>() where T : Component { foreach (var c in comps) if (c is T t && !c.destroyedObj) return t; return null; }
        public Component GetComponent(Type t) { foreach (var c in comps) if (t.IsInstanceOfType(c) && !c.destroyedObj) return c; return null; }
        public T[] GetComponents<T>() where T : Component { var l = new List<T>(); foreach (var c in comps) if (c is T t) l.Add(t); return l.ToArray(); }
        public T GetComponentInChildren<T>() where T : Component { var a = GetComponentsInChildren<T>(false); return a.Length > 0 ? a[0] : null; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component
        {
            var l = new List<T>(); void Walk(Transform t) { if (!inactive && !t.gameObject.activeSelf) return; foreach (var c in t.gameObject.comps) if (c is T k) l.Add(k); foreach (var ch in t.children) Walk(ch); }
            Walk(tr); return l.ToArray();
        }
        public T[] GetComponentsInChildren<T>() where T : Component => GetComponentsInChildren<T>(false);
        public void SetActive(bool a) { if (destroyedObj) throw new MissingReferenceException("SetActive auf zerstörtem GameObject " + name); activeSelf = a; }
        public bool CompareTag(string t) => tag == t;
        public static GameObject Find(string n) { foreach (var g in World.Objects) if (g.name == n && g.activeInHierarchy) return g; return null; }
        public static GameObject CreatePrimitive(PrimitiveType t) { var g = new GameObject(t.ToString()); g.AddComponent<MeshFilter>().sharedMesh = new Mesh(); g.AddComponent<MeshRenderer>(); return g; }
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Mesh : Object
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>();
        public List<List<int>> T = new List<List<int>>();
        public List<Color> Col = new List<Color>();
        public IndexFormat indexFormat;
        public Bounds bounds;
        int subs = 1;
        public Mesh() { T.Add(new List<int>()); }
        public int vertexCount => V.Count;
        public int subMeshCount { get => subs; set { subs = value; while (T.Count < value) T.Add(new List<int>()); } }
        void CheckIndices(List<int> t, string what)
        {
            if (indexFormat == IndexFormat.UInt16 && V.Count > 65535) throw new ArgumentException(what + ": mehr als 65535 Ecken bei 16-Bit-Indizes (" + name + ")");
            foreach (var i in t) if (i < 0 || i >= V.Count) throw new ArgumentException(what + ": Index " + i + " außerhalb 0…" + (V.Count - 1) + " (" + name + ")");
            if (t.Count % 3 != 0) throw new ArgumentException(what + ": Indexanzahl kein Vielfaches von 3 (" + name + ")");
        }
        void CheckVerts(List<Vector3> v, string what) { foreach (var p in v) NanGuard.Check(p, what + " (" + name + ")"); }
        public void SetVertices(List<Vector3> v) { CheckVerts(v, "Mesh.SetVertices"); V = new List<Vector3>(v); }
        public void SetVertices(Vector3[] v) => SetVertices(new List<Vector3>(v));
        public void SetNormals(List<Vector3> n) { if (n.Count != V.Count) throw new ArgumentException("Mesh.SetNormals: Anzahl " + n.Count + " ≠ Ecken " + V.Count + " (" + name + ")"); N = new List<Vector3>(n); }
        public void SetNormals(Vector3[] n) => SetNormals(new List<Vector3>(n));
        public List<Vector2> UV = new List<Vector2>(); public List<Vector2> UV2 = new List<Vector2>();
        public void SetUVs(int ch, List<Vector2> u) { if (u.Count != V.Count) throw new ArgumentException("Mesh.SetUVs: Anzahl ≠ Ecken (" + name + ")"); if (ch == 0) UV = new List<Vector2>(u); else UV2 = new List<Vector2>(u); }
        public void SetUVs(int ch, List<Vector4> u) { if (u.Count != V.Count) throw new ArgumentException("Mesh.SetUVs: Anzahl ≠ Ecken (" + name + ")"); }
        public void SetUVs(int ch, List<Vector3> u) { if (u.Count != V.Count) throw new ArgumentException("Mesh.SetUVs: Anzahl ≠ Ecken (" + name + ")"); }
        public void SetUVs(int ch, Vector2[] u) => SetUVs(ch, new List<Vector2>(u));
        public void SetColors(List<Color> c) { if (c.Count != V.Count) throw new ArgumentException("Mesh.SetColors: Anzahl ≠ Ecken (" + name + ")"); Col = new List<Color>(c); }
        public void SetColors(List<Color32> c) { if (c.Count != V.Count) throw new ArgumentException("Mesh.SetColors: Anzahl ≠ Ecken (" + name + ")"); }
        public void SetColors(Color[] c) => SetColors(new List<Color>(c));
        public void SetTangents(List<Vector4> t) { }
        public void SetTriangles(List<int> t, int sub, bool calc = true) { if (sub < 0 || sub >= subMeshCount) throw new IndexOutOfRangeException("Mesh.SetTriangles: Untermesh " + sub + " ≥ subMeshCount " + subMeshCount + " (" + name + ")"); CheckIndices(t, "Mesh.SetTriangles"); T[sub] = new List<int>(t); }
        public void SetTriangles(int[] t, int sub, bool calc = true) => SetTriangles(new List<int>(t), sub, calc);
        public void SetIndices(List<int> t, MeshTopology top, int sub, bool calc = true) { if (sub >= subMeshCount) throw new IndexOutOfRangeException("Mesh.SetIndices"); foreach (var i in t) if (i < 0 || i >= V.Count) throw new ArgumentException("Mesh.SetIndices: Index außerhalb"); T[sub] = top == MeshTopology.Triangles ? new List<int>(t) : new List<int>(); }
        public void SetIndices(int[] t, MeshTopology top, int sub, bool calc = true) => SetIndices(new List<int>(t), top, sub, calc);
        public void GetVertices(List<Vector3> l) { l.Clear(); l.AddRange(V); }
        public void GetNormals(List<Vector3> l) { l.Clear(); l.AddRange(N); }
        public void GetUVs(int ch, List<Vector2> l) { l.Clear(); l.AddRange(UV); }
        public void GetColors(List<Color> l) { l.Clear(); l.AddRange(Col); }
        public void GetTriangles(List<int> l, int s) { l.Clear(); if (s < T.Count) l.AddRange(T[s]); }
        public int[] GetTriangles(int s) { if (s < 0 || s >= subMeshCount) throw new IndexOutOfRangeException("Mesh.GetTriangles"); return T[s].ToArray(); }
        public int[] GetIndices(int s) => GetTriangles(s);
        public uint GetIndexCount(int s) => (uint)(s < T.Count ? T[s].Count : 0);
        public void RecalculateBounds() { if (V.Count == 0) { bounds = new Bounds(); return; } var b = new Bounds(V[0], Vector3.zero); foreach (var p in V) b.Encapsulate(p); bounds = b; }
        public void RecalculateNormals()
        {
            var n = new Vector3[V.Count];
            for (int s = 0; s < subMeshCount; s++) for (int k = 0; k + 2 < T[s].Count; k += 3) { int a = T[s][k], b = T[s][k + 1], c = T[s][k + 2]; var f = Vector3.Cross(V[b] - V[a], V[c] - V[a]); n[a] += f; n[b] += f; n[c] += f; }
            N = new List<Vector3>(); foreach (var v in n) N.Add(v.normalized);
        }
        public void RecalculateTangents() { }
        public void UploadMeshData(bool noLonger) { }
        public void MarkDynamic() { }
        public void Clear() { V.Clear(); N.Clear(); UV.Clear(); Col.Clear(); foreach (var t in T) t.Clear(); }
        public void Clear(bool keep) => Clear();
        public Vector3[] vertices { get => V.ToArray(); set { CheckVerts(new List<Vector3>(value), "Mesh.vertices"); V = new List<Vector3>(value); } }
        public Vector3[] normals { get => N.ToArray(); set => N = new List<Vector3>(value); }
        public Vector2[] uv { get => UV.ToArray(); set => UV = new List<Vector2>(value); }
        public Color[] colors { get => Col.ToArray(); set => Col = new List<Color>(value); }
        public Color32[] colors32 { set { } }
        public Vector4[] tangents { set { } }
        public int[] triangles { get { var l = new List<int>(); for (int s = 0; s < subMeshCount; s++) l.AddRange(T[s]); return l.ToArray(); } set { subMeshCount = 1; CheckIndices(new List<int>(value), "Mesh.triangles"); T[0] = new List<int>(value); } }
    }
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

    public class Texture : Object
    {
        public TextureWrapMode wrapMode, wrapModeU, wrapModeV, wrapModeW; public FilterMode filterMode; public int anisoLevel; public float mipMapBias;
        public virtual int width => 0; public virtual int height => 0;
    }
    public class Texture2D : Texture
    {
        public int W, H; public Color[] Px;
        public override int width => W; public override int height => H;
        public Texture2D(int w, int h) : this(w, h, TextureFormat.RGBA32, true) { }
        public Texture2D(int w, int h, TextureFormat f, bool mip)
        {
            if (w <= 0 || h <= 0) throw new UnityException("Texture2D: ungültige Größe " + w + "×" + h);
            W = w; H = h; Px = new Color[w * h];
        }
        public Texture2D(int w, int h, TextureFormat f, bool mip, bool lin) : this(w, h, f, mip) { }
        public Texture2D(int w, int h, TextureFormat f, int mips, bool lin) : this(w, h, f, mips > 1) { }
        static Texture2D white, black;
        public static Texture2D whiteTexture { get { if (white == null) { white = new Texture2D(4, 4); for (int i = 0; i < 16; i++) white.Px[i] = Color.white; } return white; } }
        public static Texture2D blackTexture { get { if (black == null) { black = new Texture2D(4, 4); for (int i = 0; i < 16; i++) black.Px[i] = Color.black; } return black; } }
        public void SetPixels32(Color32[] p) { if (p.Length != W * H) throw new UnityException("SetPixels32: Größe " + p.Length + " ≠ " + W * H + " (" + name + ")"); for (int i = 0; i < p.Length; i++) Px[i] = p[i]; }
        public void SetPixels(Color[] p) { if (p.Length != W * H) throw new UnityException("SetPixels: Größe " + p.Length + " ≠ " + W * H + " (" + name + ")"); Array.Copy(p, Px, p.Length); }
        public void SetPixels(Color[] p, int mip) => SetPixels(p);
        public void SetPixel(int x, int y, Color c) { if (x < 0 || y < 0 || x >= W || y >= H) return; Px[y * W + x] = c; }
        public Color GetPixel(int x, int y) => Px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];
        public Color GetPixelBilinear(float u, float v) => Sample(new Vector2(u, v));
        public Color[] GetPixels() => (Color[])Px.Clone();
        public Color32[] GetPixels32() { var r = new Color32[Px.Length]; for (int i = 0; i < r.Length; i++) r[i] = Px[i]; return r; }
        public void SetPixelData<T>(T[] d, int mip) { }
        public void LoadRawTextureData(byte[] d) { }
        public Color Sample(Vector2 uv) { float u = uv.x - (float)Math.Floor(uv.x), v = uv.y - (float)Math.Floor(uv.y); int x = Mathf.Clamp((int)(u * W), 0, W - 1), y = Mathf.Clamp((int)(v * H), 0, H - 1); return Px[y * W + x]; }
        public void Apply() { }
        public void Apply(bool m) { }
        public void Apply(bool m, bool nonReadable) { }
        public void ReadPixels(Rect r, int x, int y) { }
        public byte[] EncodeToPNG() => new byte[8];
        public bool LoadImage(byte[] d) => true;
        public void Compress(bool hq) { }
        public TextureFormat format => TextureFormat.RGBA32;
    }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, R8 = 63, RGBAHalf = 17, RGBAFloat = 20, RFloat = 18, RHalf = 15, R16 = 9, DXT1 = 10, DXT5 = 12 }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum FilterMode { Point, Bilinear, Trilinear }

    public class Shader : Object
    {
        public static Shader Find(string n) => new Shader { name = n };
        public bool isSupported => Environment.GetEnvironmentVariable("RP_EIGENE_SHADER") == "1"; // Standard: Rückfallpfad; RP_EIGENE_SHADER=1 simuliert die eigenen Shader
        public int maximumLOD;
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        public static int PropertyToID(string n) { if (!ids.TryGetValue(n, out var i)) { i = ids.Count + 1; ids[n] = i; } return i; }
        public static void SetGlobalFloat(string n, float v) { }
        public static void SetGlobalFloat(int n, float v) { }
        public static void SetGlobalColor(string n, Color v) { }
        public static void SetGlobalVector(string n, Vector4 v) { }
        public static void SetGlobalTexture(string n, Texture t) { }
        public static void EnableKeyword(string k) { }
        public static void DisableKeyword(string k) { }
    }
    public class Material : Object
    {
        public Color color = Color.white; public Color emission = Color.black; public string tpl; public Shader shader; public int renderQueue;
        public bool enableInstancing; public object mainTexture; public Vector2 mainTextureScale = Vector2.one, mainTextureOffset;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public Material(Shader s) { shader = s; }
        public Material(Material m) { if (m is null) throw new ArgumentNullException("Material(Material): Vorlage fehlt"); tpl = m.name; shader = m.shader; color = m.color; emission = m.emission; mainTexture = m.mainTexture; foreach (var kv in m.floats) floats[kv.Key] = kv.Value; foreach (var k in m.keys) keys.Add(k); }
        public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public readonly HashSet<string> keys = new HashSet<string>();
        public bool HasProperty(string p) => true;
        public bool HasProperty(int p) => true;
        public float GetFloat(string p) => floats.TryGetValue(p, out var v) ? v : 0.2f;
        public float GetFloat(int p) => 0.2f;
        public int GetInt(string p) => (int)GetFloat(p);
        public Color GetColor(string p) => p == "_EmissionColor" ? emission : color;
        public Vector4 GetVector(string p) => new Vector4();
        public Texture GetTexture(string p) => mainTexture as Texture;
        public bool IsKeywordEnabled(string k) => keys.Contains(k);
        void Nan(float v, string p) { if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArithmeticException("Material " + name + ": " + p + " = " + v); }
        public void SetFloat(string p, float v) { Nan(v, p); floats[p] = v; }
        public void SetFloat(int p, float v) { Nan(v, "#" + p); }
        public void SetInt(string p, int v) { floats[p] = v; }
        public void SetInteger(string p, int v) { floats[p] = v; }
        public void SetColor(string p, Color c) { Nan(c.r + c.g + c.b + c.a, p); if (p == "_EmissionColor") emission = c; if (p == "_Color") color = c; }
        public void SetColor(int p, Color c) { Nan(c.r + c.g + c.b + c.a, "#" + p); }
        public void SetVector(string p, Vector4 v) { Nan(v.x + v.y + v.z + v.w, p); }
        public void SetVector(int p, Vector4 v) { Nan(v.x + v.y + v.z + v.w, "#" + p); }
        public void SetMatrix(string p, Matrix4x4 m) { }
        public void SetTexture(string p, object t) { if (p == "_MainTex") mainTexture = t; }
        public void SetTexture(int p, object t) { }
        public void SetTextureScale(string p, Vector2 s) { }
        public void SetTextureOffset(string p, Vector2 s) { }
        public void SetFloatArray(string p, float[] v) { }
        public void SetVectorArray(string p, Vector4[] v) { }
        public void SetColorArray(string p, Color[] v) { }
        public void EnableKeyword(string k) { keys.Add(k); } public void DisableKeyword(string k) { keys.Remove(k); }
        public void SetOverrideTag(string a, string b) { }
        public void SetPass(int p) { }
        public void CopyPropertiesFromMaterial(Material m) { color = m.color; }
        public string[] shaderKeywords { get { var a = new string[keys.Count]; keys.CopyTo(a); return a; } set { keys.Clear(); foreach (var k in value) keys.Add(k); } }
    }
    public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, EmissiveIsBlack = 4, AnyEmissive = 3 }
    public static class Resources
    {
        /// <summary>Prüfumgebung: Sprachaufnahmen (Resources/Voice) simulieren, Länge in Sekunden (0 = keine).</summary>
        public static float FakeVoiceLength;
        public static T Load<T>(string n) where T : class
        {
            if (typeof(T) == typeof(Material)) return new Material(new Shader()) { name = n } as T;
            if (typeof(T) == typeof(AudioClip) && FakeVoiceLength > 0 && n.StartsWith("Voice/")) return AudioClip.Create(n, (int)(FakeVoiceLength * 48000), 1, 48000, false) as T;
            return null;
        }
        public static Object Load(string n) => null;
        public static T[] LoadAll<T>(string n) where T : class => new T[0];
        public static T GetBuiltinResource<T>(string n) where T : class => typeof(T) == typeof(Font) ? new Font { name = n } as T : null;
        public static T[] FindObjectsOfTypeAll<T>() where T : Object => Object.FindObjectsOfType<T>();
        public static void UnloadUnusedAssets() { }
    }
    public class Font : Object
    {
        public Material material = new Material(new Shader());
        public static event Action<Font> textureRebuilt;
        public void RequestCharactersInTexture(string s, int size = 0, FontStyle st = FontStyle.Normal) { }
        public bool GetCharacterInfo(char c, out CharacterInfo info, int size = 0, FontStyle st = FontStyle.Normal) { info = new CharacterInfo { advance = 10 }; return true; }
        public static Font CreateDynamicFontFromOSFont(string n, int size) => new Font { name = n };
        public static string[] GetOSInstalledFontNames() => new[] { "Arial" };
    }
    public struct CharacterInfo { public int advance, glyphWidth, glyphHeight, minX, maxX, minY, maxY; public Vector2 uvBottomLeft, uvBottomRight, uvTopLeft, uvTopRight; }
    public class Renderer : Component
    {
        public Material sharedMaterial { get => mats.Length > 0 ? mats[0] : null; set => mats = new[] { value }; }
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public Material[] mats = new Material[0];
        public Material[] sharedMaterials { get => mats; set => mats = value ?? new Material[0]; }
        public Material[] materials { get => mats; set => mats = value ?? new Material[0]; }
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On; public bool receiveShadows = true, enabled = true, allowOcclusionWhenDynamic = true;
        public LightProbeUsage lightProbeUsage; public ReflectionProbeUsage reflectionProbeUsage;
        public bool isVisible => true;
        public Bounds bounds => new Bounds(transform.position, Vector3.one);
        public int sortingOrder;
        public MotionVectorGenerationMode motionVectorGenerationMode;
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
    }
    public enum MotionVectorGenerationMode { Camera, Object, ForceNoMotion }
    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { public Mesh sharedMesh; }
    public class MeshFilter : Component { public Mesh sharedMesh; public Mesh mesh { get => sharedMesh; set => sharedMesh = value; } }
    public class TextMesh : Component { public Font font; public string text; public int fontSize; public float characterSize = 1f, lineSpacing = 1f, offsetZ; public TextAnchor anchor; public TextAlignment alignment; public Color color = Color.white; public FontStyle fontStyle; public bool richText; }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment { Left, Center, Right }
    public class Camera : Behaviour
    {
        static Camera _main;
        /// <summary>Wie Unity: erste aktive Kamera mit Tag „MainCamera“ (oder explizit gesetzt).</summary>
        public static Camera main
        {
            get
            {
                if (_main != null && _main.isActiveAndEnabled) return _main;
                foreach (var c in World.All) if (c is Camera k && k.isActiveAndEnabled && k.gameObject.tag == "MainCamera") return k;
                return null;
            }
            set => _main = value;
        }
        public static Camera current => main;
        public float fieldOfView = 60f, nearClipPlane = 0.3f, farClipPlane = 1000f, aspect = 16f / 9f, depth, orthographicSize = 5f;
        public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        public Color backgroundColor = Color.black;
        public bool allowMSAA = true, allowHDR = true, orthographic, useOcclusionCulling = true, forceIntoRenderTexture;
        public RenderTexture targetTexture;
        public int cullingMask = -1; public int pixelWidth => Screen.width; public int pixelHeight => Screen.height;
        public DepthTextureMode depthTextureMode;
        public Rect rect = new Rect(0, 0, 1, 1);
        public float[] layerCullDistances = new float[32];
        public bool layerCullSpherical;
        public enum MonoOrStereoscopicEye { Left, Right, Mono }
        public void Render() { }
        public void ResetProjectionMatrix() { }
        public Matrix4x4 projectionMatrix = Matrix4x4.identity;
        public Matrix4x4 worldToCameraMatrix => transform.worldToLocalMatrix;
        public Matrix4x4 GetStereoProjectionMatrix(Camera.StereoscopicEye e) => projectionMatrix;
        public enum StereoscopicEye { Left, Right }
        public void CalculateFrustumCorners(Rect r, float z, MonoOrStereoscopicEye eye, Vector3[] outCorners)
        {
            if (outCorners == null || outCorners.Length < 4) throw new ArgumentException("outCorners");
            float h = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * z, w = h * aspect;
            outCorners[0] = new Vector3(-w, -h, z); outCorners[1] = new Vector3(-w, h, z); outCorners[2] = new Vector3(w, h, z); outCorners[3] = new Vector3(w, -h, z);
        }
        public Vector3 WorldToScreenPoint(Vector3 p)
        {
            var l = transform.InverseTransformPoint(p);
            float f = Screen.height * 0.5f / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (Math.Abs(l.z) < 1e-6f) return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, l.z);
            return new Vector3(Screen.width * 0.5f + l.x / l.z * f, Screen.height * 0.5f + l.y / l.z * f, l.z);
        }
        public Vector3 WorldToViewportPoint(Vector3 p) { var s = WorldToScreenPoint(p); return new Vector3(s.x / Screen.width, s.y / Screen.height, s.z); }
        public Ray ScreenPointToRay(Vector3 s)
        {
            float f = Screen.height * 0.5f / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            var d = new Vector3((s.x - Screen.width * 0.5f) / f, (s.y - Screen.height * 0.5f) / f, 1f);
            return new Ray(transform.position, transform.TransformDirection(d));
        }
        public Ray ViewportPointToRay(Vector3 v) => ScreenPointToRay(new Vector3(v.x * Screen.width, v.y * Screen.height, 0));
        public Vector3 ScreenToWorldPoint(Vector3 s) => ScreenPointToRay(s).GetPoint(s.z);
    }
    public class MaterialPropertyBlock
    {
        public void SetColor(string p, Color c) { } public void SetColor(int p, Color c) { }
        public void SetFloat(string p, float f) { } public void SetFloat(int p, float f) { }
        public void SetVector(string p, Vector4 v) { } public void SetVector(int p, Vector4 v) { }
        public void SetVectorArray(string p, Vector4[] v) { } public void SetVectorArray(int p, Vector4[] v) { } public void SetVectorArray(string p, List<Vector4> v) { } public void SetVectorArray(int p, List<Vector4> v) { }
        public void SetFloatArray(string p, float[] v) { } public void SetFloatArray(int p, float[] v) { } public void SetFloatArray(int p, List<float> v) { }
        public void SetTexture(string p, Texture t) { }
        public void Clear() { }
    }

    public static class Graphics
    {
        public struct Call { public Mesh Mesh; public int Sub; public Material Mat; public Matrix4x4[] M; public int Count; }
        public static readonly List<Call> Calls = new List<Call>();
        public static bool Record = true;
        public static long Draws, Instances;
        static void Check(Mesh m, int sub, Material mat, int count, int arrLen, string what)
        {
            Draws++; Instances += count;
            if (m == null) throw new ArgumentNullException(what + ": mesh");
            if (mat == null) throw new ArgumentNullException(what + ": material");
            if (sub < 0 || sub >= m.subMeshCount) throw new IndexOutOfRangeException(what + ": Untermesh " + sub + " / " + m.subMeshCount + " (" + m.name + ")");
            if (count > 1023) throw new ArgumentOutOfRangeException(what + ": mehr als 1023 Instanzen (" + count + ")");
            if (count < 0 || count > arrLen) throw new ArgumentOutOfRangeException(what + ": count " + count + " > Feld " + arrLen);
        }
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count, MaterialPropertyBlock p, ShadowCastingMode s, bool r, int layer, Camera c)
        {
            Check(m, sub, mat, count, arr.Length, "DrawMeshInstanced");
            if (Record) Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = arr, Count = count });
        }
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count, MaterialPropertyBlock p, ShadowCastingMode s, bool r, int layer) => DrawMeshInstanced(m, sub, mat, arr, count, p, s, r, layer, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count, MaterialPropertyBlock p, ShadowCastingMode s, bool r) => DrawMeshInstanced(m, sub, mat, arr, count, p, s, r, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count, MaterialPropertyBlock p) => DrawMeshInstanced(m, sub, mat, arr, count, p, ShadowCastingMode.On, true, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count) => DrawMeshInstanced(m, sub, mat, arr, count, null, ShadowCastingMode.On, true, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr) => DrawMeshInstanced(m, sub, mat, arr, arr.Length, null, ShadowCastingMode.On, true, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l, MaterialPropertyBlock p, ShadowCastingMode s, bool r, int layer, Camera c)
        {
            Check(m, sub, mat, l.Count, l.Count, "DrawMeshInstanced(List)");
            if (Record) Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = l.ToArray(), Count = l.Count });
        }
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l, MaterialPropertyBlock p, ShadowCastingMode s, bool r, int layer) => DrawMeshInstanced(m, sub, mat, l, p, s, r, layer, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l, MaterialPropertyBlock p, ShadowCastingMode s, bool r) => DrawMeshInstanced(m, sub, mat, l, p, s, r, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l, MaterialPropertyBlock p) => DrawMeshInstanced(m, sub, mat, l, p, ShadowCastingMode.On, true, 0, null);
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l) => DrawMeshInstanced(m, sub, mat, l, null, ShadowCastingMode.On, true, 0, null);
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c, int sub, MaterialPropertyBlock p, ShadowCastingMode s, bool r)
        {
            Check(m, sub, mat, 1, 1, "DrawMesh");
            if (Record) Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = new[] { mx }, Count = 1 });
        }
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c, int sub, MaterialPropertyBlock p, bool castShadows, bool r) => DrawMesh(m, mx, mat, layer, c, sub, p, castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, r);
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c, int sub, MaterialPropertyBlock p) => DrawMesh(m, mx, mat, layer, c, sub, p, ShadowCastingMode.On, true);
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c, int sub) => DrawMesh(m, mx, mat, layer, c, sub, null, ShadowCastingMode.On, true);
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c) => DrawMesh(m, mx, mat, layer, c, 0, null, ShadowCastingMode.On, true);
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer) => DrawMesh(m, mx, mat, layer, null, 0, null, ShadowCastingMode.On, true);
        public static void DrawMesh(Mesh m, Vector3 p, Quaternion q, Material mat, int layer) => DrawMesh(m, Matrix4x4.TRS(p, q, Vector3.one), mat, layer);
        public static void Blit(Texture a, RenderTexture b) { }
        public static void Blit(Texture a, RenderTexture b, Material m) { }
        public static void Blit(Texture a, RenderTexture b, Material m, int pass) { }
    }
}
