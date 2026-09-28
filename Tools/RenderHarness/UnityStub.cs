// Minimaler UnityEngine-Ersatz für die Prüf-Umgebung (nur was die Render-Dateien benutzen).
using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public enum LightProbeUsage { Off, BlendProbes }
}

namespace UnityEngine
{
    using UnityEngine.Rendering;

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector2(x / m, y / m) : new Vector2(0, 0); } }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
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
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a * s);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static implicit operator Color32(Color c) => new Color32((byte)Mathf.Clamp(c.r * 255, 0, 255), (byte)Mathf.Clamp(c.g * 255, 0, 255), (byte)Mathf.Clamp(c.b * 255, 0, 255), (byte)Mathf.Clamp(c.a * 255, 0, 255));
        public override int GetHashCode() => (r, g, b, a).GetHashCode();
        public override bool Equals(object o) => o is Color c && c.r == r && c.g == g && c.b == b && c.a == a;
    }

    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }

    public static class ColorUtility { public static string ToHtmlStringRGBA(Color c) { Color32 k = c; return $"{k.r:X2}{k.g:X2}{k.b:X2}{k.a:X2}"; } }

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
            f = f.normalized;
            var r = Vector3.Cross(up, f).normalized;
            if (r.sqrMagnitude < 1e-6f) r = Vector3.Cross(Vector3.forward, f).normalized;
            var u = Vector3.Cross(f, r);
            // Matrix with columns r,u,f → quaternion
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22;
            if (tr > 0) { float s = (float)Math.Sqrt(tr + 1) * 2; return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            if (m00 > m11 && m00 > m22) { float s = (float)Math.Sqrt(1 + m00 - m11 - m22) * 2; return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            if (m11 > m22) { float s = (float)Math.Sqrt(1 + m11 - m00 - m22) * 2; return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            { float s = (float)Math.Sqrt(1 + m22 - m00 - m11) * 2; return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
        }
        public static Quaternion LookRotation(Vector3 f) => LookRotation(f, Vector3.up);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => b;
    }

    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
        public static Matrix4x4 identity => new Matrix4x4 { m00 = 1, m11 = 1, m22 = 1, m33 = 1 };
        public static Matrix4x4 TRS(Vector3 t, Quaternion q, Vector3 s)
        {
            var c0 = q * new Vector3(s.x, 0, 0); var c1 = q * new Vector3(0, s.y, 0); var c2 = q * new Vector3(0, 0, s.z);
            return new Matrix4x4 { m00 = c0.x, m10 = c0.y, m20 = c0.z, m01 = c1.x, m11 = c1.y, m21 = c1.z, m02 = c2.x, m12 = c2.y, m22 = c2.z, m03 = t.x, m13 = t.y, m23 = t.z, m33 = 1 };
        }
        public static Matrix4x4 Scale(Vector3 s) => new Matrix4x4 { m00 = s.x, m11 = s.y, m22 = s.z, m33 = 1 };
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            float[] A = a.Arr(), B = b.Arr(), R = new float[16];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += A[i * 4 + k] * B[k * 4 + j]; R[i * 4 + j] = s; }
            r.Set(R); return r;
        }
        float[] Arr() => new[] { m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33 };
        void Set(float[] R) { m00 = R[0]; m01 = R[1]; m02 = R[2]; m03 = R[3]; m10 = R[4]; m11 = R[5]; m12 = R[6]; m13 = R[7]; m20 = R[8]; m21 = R[9]; m22 = R[10]; m23 = R[11]; m30 = R[12]; m31 = R[13]; m32 = R[14]; m33 = R[15]; }
        public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23);
        public Vector3 MultiplyPoint(Vector3 p) => MultiplyPoint3x4(p);
        public Vector3 MultiplyVector(Vector3 p) => new Vector3(m00 * p.x + m01 * p.y + m02 * p.z, m10 * p.x + m11 * p.y + m12 * p.z, m20 * p.x + m21 * p.y + m22 * p.z);
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 c, Vector3 s) { center = c; size = s; }
        public float SqrDistance(Vector3 p)
        {
            var h = size * 0.5f; float dx = Math.Max(0, Math.Abs(p.x - center.x) - h.x), dy = Math.Max(0, Math.Abs(p.y - center.y) - h.y), dz = Math.Max(0, Math.Abs(p.z - center.z) - h.z);
            return dx * dx + dy * dy + dz * dz;
        }
    }
    public struct Plane { }
    public static class GeometryUtility
    {
        public static void CalculateFrustumPlanes(Camera c, Plane[] p) { }
        public static bool TestPlanesAABB(Plane[] p, Bounds b) => true;
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI, Deg2Rad = PI / 180f, Rad2Deg = 180f / PI;
        public static float Sin(float v) => (float)Math.Sin(v); public static float Cos(float v) => (float)Math.Cos(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x); public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Abs(float v) => Math.Abs(v); public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b); public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v; public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Round(float v) => (float)Math.Round(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v); public static int CeilToInt(float v) => (int)Math.Ceiling(v); public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float Repeat(float t, float l) => t - (float)Math.Floor(t / l) * l;
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return d; }
        public static float Sign(float v) => v >= 0 ? 1 : -1;
    }

    public static class Random { static System.Random r = new System.Random(1); public static float value => (float)r.NextDouble(); public static float Range(float a, float b) => a + (b - a) * value; public static int Range(int a, int b) => r.Next(a, b); }
    public static class Time { public static float deltaTime = 0.5f, time = 10f, unscaledDeltaTime = 0.5f; }
    public static class Debug { public static void Log(object o) => Console.WriteLine(o); public static void LogWarning(object o) => Console.WriteLine("WARN " + o); public static void LogException(Exception e) => Console.WriteLine("EXC " + e); }
    public static class SystemInfo { public static bool supportsInstancing => true; }

    public class Object
    {
        public string name = "";
        public static void Destroy(Object o) { if (o is GameObject g) g.destroyed = true; }
        public static void DestroyImmediate(Object o) { }
        public static bool operator ==(Object a, Object b) { bool an = a is null || (a is GameObject ga && ga.destroyed) || (a is Component ca && ca.gameObject != null && ca.gameObject.destroyed); bool bn = b is null || (b is GameObject gb && gb.destroyed) || (b is Component cb && cb.gameObject != null && cb.gameObject.destroyed); if (an || bn) return an && bn; return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static implicit operator bool(Object o) => o != null;
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component => gameObject.GetComponentsInChildren<T>(inactive);
        public T AddComponent<T>() where T : Component => gameObject.AddComponent<T>();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> children = new List<Transform>();
        public Vector3 localPosition = Vector3.zero, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public void SetParent(Transform p, bool keep) { parent?.children.Remove(this); parent = p; p?.children.Add(this); }
        public Matrix4x4 localToWorldMatrix => (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity) * Matrix4x4.TRS(localPosition, localRotation, localScale);
        public Vector3 position { get => localToWorldMatrix.MultiplyPoint3x4(Vector3.zero); set { localPosition = parent == null ? value : value; } }
        public Quaternion rotation { get { var q = localRotation; var p = parent; while (p != null) { q = p.localRotation * q; p = p.parent; } return q; } set { localRotation = value; } }
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 TransformPoint(Vector3 p) => localToWorldMatrix.MultiplyPoint3x4(p);
        public void Rotate(float x, float y, float z, Space s) { localRotation = localRotation * Quaternion.Euler(x, y, z); }
        public void Rotate(float x, float y, float z) { Rotate(x, y, z, Space.Self); }
    }
    public enum Space { World, Self }

    public class GameObject : Object
    {
        public bool destroyed, activeSelf = true;
        public readonly List<Component> comps = new List<Component>();
        public Transform transform;
        public GameObject() : this("GameObject") { }
        public GameObject(string n) { name = n; transform = new Transform { gameObject = this }; comps.Add(transform); }
        public T AddComponent<T>() where T : Component { var c = (T)Activator.CreateInstance(typeof(T)); c.gameObject = this; comps.Add(c); Harness.OnAdd(c); return c; }
        public T GetComponent<T>() where T : Component { foreach (var c in comps) if (c is T t) return t; return null; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component
        {
            var l = new List<T>(); void Walk(Transform t) { foreach (var c in t.gameObject.comps) if (c is T k) l.Add(k); foreach (var ch in t.children) Walk(ch); }
            Walk(transform); return l.ToArray();
        }
        public void SetActive(bool a) { activeSelf = a; }
    }

    public class Mesh : Object
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>();
        public List<List<int>> T = new List<List<int>>();
        public IndexFormat indexFormat;
        int subs = 1;
        public int subMeshCount { get => subs; set { subs = value; while (T.Count < value) T.Add(new List<int>()); } }
        public void SetVertices(List<Vector3> v) { V = new List<Vector3>(v); }
        public void SetNormals(List<Vector3> n) { N = new List<Vector3>(n); }
        public List<Vector2> UV = new List<Vector2>(); public List<Vector2> UV2 = new List<Vector2>(); public void SetUVs(int ch, List<Vector2> u) { if (ch == 0) UV = new List<Vector2>(u); else UV2 = new List<Vector2>(u); }
        public void SetColors(List<Color> c) { }
        public void SetTriangles(List<int> t, int sub, bool calc = true) { subMeshCount = Math.Max(subMeshCount, sub + 1); T[sub] = new List<int>(t); }
        public void GetVertices(List<Vector3> l) { l.Clear(); l.AddRange(V); }
        public void GetNormals(List<Vector3> l) { l.Clear(); l.AddRange(N); }
        public void GetTriangles(List<int> l, int s) { l.Clear(); if (s < T.Count) l.AddRange(T[s]); }
        public uint GetIndexCount(int s) => (uint)(s < T.Count ? T[s].Count : 0);
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public Vector3[] vertices { get => V.ToArray(); set => V = new List<Vector3>(value); }
        public Vector2[] uv { set { } }
        public int[] triangles { set { subMeshCount = 1; T[0] = new List<int>(value); } }
    }

    public class Texture : Object { public TextureWrapMode wrapMode; public FilterMode filterMode; public int anisoLevel; }
    public class Texture2D : Texture
    {
        public int W, H; public Color[] Px;
        public Texture2D(int w, int h, TextureFormat f, bool mip) { W = w; H = h; Px = new Color[w * h]; }
        public Texture2D(int w, int h, TextureFormat f, bool mip, bool lin) : this(w, h, f, mip) { }
        public void SetPixels32(Color32[] p) { }
        public void SetPixel(int x, int y, Color c) { Px[y * W + x] = c; }
        public Color Sample(Vector2 uv) { int x = Mathf.Clamp((int)(uv.x * W), 0, W - 1), y = Mathf.Clamp((int)(uv.y * H), 0, H - 1); return Px[y * W + x]; }
        public void Apply(bool m) { }
        public void Apply(bool m, bool r) { }
    }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp, Repeat }
    public enum FilterMode { Trilinear, Bilinear, Point }

    public class Shader : Object { public static Shader Find(string n) => new Shader { name = n }; public bool isSupported => false; }
    public class Material : Object
    {
        public Color color = Color.white; public Color emission = Color.black; public string tpl; public Shader shader; public int renderQueue;
        public bool enableInstancing; public object mainTexture; public Vector2 mainTextureScale, mainTextureOffset;
        public Material(Shader s) { shader = s; }
        public Material(Material m) { tpl = m.name; color = m.color; foreach (var kv in m.floats) floats[kv.Key] = kv.Value; foreach (var k in m.keys) keys.Add(k); }
        public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public readonly HashSet<string> keys = new HashSet<string>();
        public bool HasProperty(string p) => true;
        public float GetFloat(string p) => floats.TryGetValue(p, out var v) ? v : 0.2f;
        public bool IsKeywordEnabled(string k) => keys.Contains(k);
        public void SetFloat(string p, float v) { floats[p] = v; }
        public void SetInt(string p, int v) { }
        public void SetColor(string p, Color c) { if (p == "_EmissionColor") emission = c; }
        public void SetTexture(string p, object t) { }
        public void EnableKeyword(string k) { keys.Add(k); } public void DisableKeyword(string k) { keys.Remove(k); }
        public void SetOverrideTag(string a, string b) { }
    }
    public static class Resources
    {
        public static T Load<T>(string n) where T : class { if (typeof(T) == typeof(Material)) return new Material(new Shader()) { name = n } as T; return null; }
        public static T GetBuiltinResource<T>(string n) where T : class => null;
    }
    public class Font : Object { public Material material; public static event Action<Font> textureRebuilt; }
    public class Renderer : Component
    {
        public Material sharedMaterial { get => mats.Length > 0 ? mats[0] : null; set => mats = new[] { value }; }
        public Material[] mats = new Material[0];
        public Material[] sharedMaterials { get => mats; set => mats = value; }
        public ShadowCastingMode shadowCastingMode; public bool receiveShadows;
    }
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class TextMesh : Component { public Font font; public string text; public int fontSize; public float characterSize; public TextAnchor anchor; public TextAlignment alignment; public Color color; }
    public enum TextAnchor { MiddleCenter } public enum TextAlignment { Center }
    public class Camera : Behaviour { public static Camera main; public float fieldOfView = 60f; }
    public class MaterialPropertyBlock { }

    public static class Graphics
    {
        public struct Call { public Mesh Mesh; public int Sub; public Material Mat; public Matrix4x4[] M; public int Count; }
        public static readonly List<Call> Calls = new List<Call>();
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] arr, int count, MaterialPropertyBlock p, ShadowCastingMode s, bool r, int layer, Camera c)
            => Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = arr, Count = count });
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> l, MaterialPropertyBlock p, ShadowCastingMode s, bool r)
            => Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = l.ToArray(), Count = l.Count });
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer, Camera c, int sub, MaterialPropertyBlock p, ShadowCastingMode s, bool r)
            => Calls.Add(new Call { Mesh = m, Sub = sub, Mat = mat, M = new[] { mx }, Count = 1 });
        public static void DrawMesh(Mesh m, Matrix4x4 mx, Material mat, int layer) => DrawMesh(m, mx, mat, layer, null, 0, null, ShadowCastingMode.On, true);
    }

    // Partikelsystem (nur für Brunnen): alles ohne Wirkung
    public class ParticleSystem : Component
    {
        public struct MainModule { public MinMax startLifetime, startSpeed, startSize; public float gravityModifier; public ColorMM startColor; public int maxParticles; public bool loop, playOnAwake; public ParticleSystemSimulationSpace simulationSpace; }
        public struct ShapeModule { public ParticleSystemShapeType shapeType; public float angle, radius; public bool enabled; }
        public struct EmissionModule { public MinMax rateOverTime; public bool enabled; }
        public struct MinMax { public static implicit operator MinMax(float f) => new MinMax(); }
        public struct ColorMM { public static implicit operator ColorMM(Color c) => new ColorMM(); }
        public MainModule main; public ShapeModule shape; public EmissionModule emission;
        public void Stop(bool a, ParticleSystemStopBehavior b) { }
        public void Play() { }
    }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public enum ParticleSystemShapeType { Cone }
    public enum ParticleSystemSimulationSpace { World }
    public class ParticleSystemRenderer : Renderer { }
}
