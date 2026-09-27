using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>Baut prozedurale Meshes aus Grundformen (alle Formen des Spiels entstehen hier – keine Fremdmodelle).</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> col = new List<Color>();
        readonly List<int> t = new List<int>();
        public Matrix4x4 M = Matrix4x4.identity;
        public Color Tint = Color.white;
        public int VertexCount { get { return v.Count; } }

        int V(Vector3 p, Vector3 normal, Vector2 u)
        {
            v.Add(M.MultiplyPoint3x4(p));
            n.Add(M.MultiplyVector(normal).normalized);
            uv.Add(u);
            col.Add(Tint);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i0 = V(a, normal, new Vector2(0, 0)), i1 = V(b, normal, new Vector2(1, 0)), i2 = V(c, normal, new Vector2(1, 1)), i3 = V(d, normal, new Vector2(0, 1));
            Tri(i0, i2, i1); Tri(i0, i3, i2);
        }

        /// <summary>Quader um center mit Kantenlängen size (flach schattiert).</summary>
        public void Box(Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, Vector3.back);
            Quad(p101, p111, p011, p001, Vector3.forward);
            Quad(p001, p011, p010, p000, Vector3.left);
            Quad(p100, p110, p111, p101, Vector3.right);
            Quad(p010, p011, p111, p110, Vector3.up);
            Quad(p001, p000, p100, p101, Vector3.down);
        }

        /// <summary>Quader mit Rotation um die Y-Achse (Grad) und optionaler Neigung.</summary>
        public void BoxRot(Vector3 c, Vector3 s, Vector3 euler)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
            Box(Vector3.zero, s);
            M = old;
        }

        /// <summary>Zylinder/Kegelstumpf entlang Y (Mitte unten bei c).</summary>
        public void Cylinder(Vector3 c, float r, float h, int seg = 12, bool caps = true, float rTop = -1f)
        {
            if (rTop < 0) rTop = r;
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var normal = new Vector3(ca, (r - rTop) / Mathf.Max(h, 0.001f), sa).normalized;
                V(c + new Vector3(ca * r, 0, sa * r), normal, new Vector2(i / (float)seg, 0));
                V(c + new Vector3(ca * rTop, h, sa * rTop), normal, new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = start + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                Tri(a0, a1, b1); Tri(a0, b1, b0);
            }
            if (!caps) return;
            if (rTop > 0.001f) Disc(c + Vector3.up * h, rTop, seg, true);
            if (r > 0.001f) Disc(c, r, seg, false);
        }

        public void Disc(Vector3 c, float r, int seg, bool up)
        {
            var normal = up ? Vector3.up : Vector3.down;
            int center = V(c, normal, new Vector2(0.5f, 0.5f));
            int first = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                V(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), normal, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                if (up) Tri(center, first + i + 1, first + i);
                else Tri(center, first + i, first + i + 1);
            }
        }

        /// <summary>Liegender Zylinder entlang X.</summary>
        public void CylinderX(Vector3 c, float r, float len, int seg = 12)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c + new Vector3(-len * 0.5f, 0, 0), Quaternion.Euler(0, 0, -90), Vector3.one);
            Cylinder(Vector3.zero, r, len, seg);
            M = old;
        }

        public void CylinderZ(Vector3 c, float r, float len, int seg = 12)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c + new Vector3(0, 0, -len * 0.5f), Quaternion.Euler(90, 0, 0), Vector3.one);
            Cylinder(Vector3.zero, r, len, seg);
            M = old;
        }

        public void Sphere(Vector3 c, float r, int seg = 10, int rings = 7, float squashY = 1f)
        {
            int start = v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float pv = y / (float)rings;
                float th = pv * Mathf.PI;
                for (int x = 0; x <= seg; x++)
                {
                    float pu = x / (float)seg;
                    float ph = pu * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    V(c + new Vector3(d.x * r, d.y * r * squashY, d.z * r), d, new Vector2(pu, 1 - pv));
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int a = start + y * (seg + 1) + x, b = a + seg + 1;
                    Tri(a, a + 1, b + 1); Tri(a, b + 1, b);
                }
        }

        /// <summary>Rotationskörper aus Profil (x = Radius, y = Höhe).</summary>
        public void Lathe(Vector3 c, Vector2[] profile, int seg = 12)
        {
            int start = v.Count;
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)], next = profile[Mathf.Min(profile.Length - 1, i + 1)];
                var tan = (next - prev).normalized;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var normal = (dir * tan.y - Vector3.up * tan.x).normalized;
                    V(c + dir * profile[i].x + Vector3.up * profile[i].y, normal, new Vector2(s / (float)seg, i / (float)(profile.Length - 1)));
                }
            }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a = start + i * (seg + 1) + s, b = a + seg + 1;
                    Tri(a, b + 1, a + 1); Tri(a, b, b + 1);
                }
        }

        public void Torus(Vector3 c, float R, float r, int seg = 14, int sides = 6)
        {
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var center = new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2;
                    var nrm = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    V(c + center + nrm * r, nrm, new Vector2(i / (float)seg, j / (float)sides));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * (sides + 1) + j, b = a + sides + 1;
                    Tri(a, b, b + 1); Tri(a, b + 1, a + 1);
                }
        }

        /// <summary>Unregelmäßiger Haufen (für Müllberge, Felsen, Schneewehen).</summary>
        public void Blob(Vector3 c, float r, float h, int seg, int rings, int seed, float noise)
        {
            int start = v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float pv = y / (float)rings;
                float th = pv * Mathf.PI * 0.5f;
                for (int x = 0; x <= seg; x++)
                {
                    float ph = x / (float)seg * Mathf.PI * 2;
                    int xi = x % seg;
                    float jit = 1f + noise * (Hash01(xi, y, seed) - 0.5f) * 2f;
                    if (y == rings) jit = 1f;
                    var d = new Vector3(Mathf.Cos(th) * Mathf.Cos(ph), Mathf.Sin(th), Mathf.Cos(th) * Mathf.Sin(ph));
                    V(c + new Vector3(d.x * r * jit, d.y * h * jit, d.z * r * jit), new Vector3(d.x, d.y * 1.5f, d.z).normalized, new Vector2(x / (float)seg, pv));
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int a = start + y * (seg + 1) + x, b = a + seg + 1;
                    Tri(a, b + 1, a + 1); Tri(a, b, b + 1);
                }
        }

        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)y * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        public Mesh Build(string name, bool colors = false)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            if (colors) m.SetColors(col);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public void Clear() { v.Clear(); n.Clear(); uv.Clear(); col.Clear(); t.Clear(); M = Matrix4x4.identity; }
    }

    /// <summary>Mehrere Materialien → je ein MeshBuilder; ergibt ein GameObject mit Untermeshes.</summary>
    public class MultiBuilder
    {
        readonly Dictionary<Material, MeshBuilder> parts = new Dictionary<Material, MeshBuilder>();
        public Matrix4x4 M = Matrix4x4.identity;

        public MeshBuilder For(Material m)
        {
            MeshBuilder b;
            if (!parts.TryGetValue(m, out b)) { b = new MeshBuilder(); parts[m] = b; }
            b.M = M;
            return b;
        }

        public GameObject Build(string name, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            foreach (var kv in parts)
            {
                if (kv.Value.VertexCount == 0) continue;
                var child = new GameObject(kv.Key.name);
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = kv.Value.Build(name + "_" + kv.Key.name);
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            return go;
        }
    }

    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Get(string key, System.Action<MeshBuilder> build)
        {
            Mesh m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            var b = new MeshBuilder();
            build(b);
            m = b.Build(key);
            cache[key] = m;
            return m;
        }

        public static Mesh Cube { get { return Get("cube", b => b.Box(Vector3.zero, Vector3.one)); } }
        public static Mesh Cylinder { get { return Get("cyl", b => b.Cylinder(new Vector3(0, -0.5f, 0), 0.5f, 1f, 16)); } }
        public static Mesh Sphere { get { return Get("sphere", b => b.Sphere(Vector3.zero, 0.5f, 14, 10)); } }
        public static Mesh Quad { get { return Get("quad", b => b.Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), Vector3.back)); } }

        /// <summary>Formen der Müllobjekte (Einheitsgröße ~1 m, Ursprung am Boden). Jede Materialklasse hat eine eigene Silhouette.</summary>
        public static Mesh Trash(string shape)
        {
            return Get("trash_" + shape, b => BuildTrash(b, shape));
        }

        static void BuildTrash(MeshBuilder b, string shape)
        {
            switch (shape)
            {
                case "sheet": // Zeitungsbündel
                    b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.7f, 0.24f, 0.5f));
                    b.BoxRot(new Vector3(0.05f, 0.27f, 0.02f), new Vector3(0.6f, 0.06f, 0.45f), new Vector3(0, 12, 0));
                    break;
                case "box":
                    b.Box(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f));
                    b.BoxRot(new Vector3(-0.35f, 0.62f, 0), new Vector3(0.05f, 0.02f, 0.58f), new Vector3(0, 0, 40));
                    break;
                case "bottle":
                    b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.13f, 0.35f), new Vector2(0.05f, 0.5f), new Vector2(0.045f, 0.65f), new Vector2(0f, 0.66f) }, 10);
                    break;
                case "pbottle":
                    b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.14f, 0.02f), new Vector2(0.15f, 0.3f), new Vector2(0.12f, 0.42f), new Vector2(0.05f, 0.55f), new Vector2(0.05f, 0.62f), new Vector2(0f, 0.63f) }, 8);
                    break;
                case "bag":
                    b.Sphere(new Vector3(0, 0.18f, 0), 0.3f, 7, 5, 0.6f);
                    b.Box(new Vector3(0, 0.38f, 0), new Vector3(0.12f, 0.12f, 0.05f));
                    break;
                case "can":
                    b.Cylinder(Vector3.zero, 0.09f, 0.3f, 10);
                    break;
                case "appliance":
                    b.Box(new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.6f, 0.5f));
                    b.Box(new Vector3(0.15f, 0.3f, 0.26f), new Vector3(0.35f, 0.4f, 0.03f));
                    break;
                case "fridge":
                    b.Box(new Vector3(0, 0.9f, 0), new Vector3(0.8f, 1.8f, 0.75f));
                    b.Box(new Vector3(0.3f, 1.2f, 0.39f), new Vector3(0.05f, 0.4f, 0.05f));
                    break;
                case "cart":
                    b.Box(new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.5f, 0.9f));
                    b.Box(new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.05f, 0.8f));
                    b.Cylinder(new Vector3(0.25f, 0, 0.35f), 0.05f, 0.1f, 6); b.Cylinder(new Vector3(-0.25f, 0, 0.35f), 0.05f, 0.1f, 6);
                    b.Cylinder(new Vector3(0.25f, 0, -0.35f), 0.05f, 0.1f, 6); b.Cylinder(new Vector3(-0.25f, 0, -0.35f), 0.05f, 0.1f, 6);
                    b.CylinderX(new Vector3(0, 1.05f, -0.5f), 0.03f, 0.6f, 6);
                    break;
                case "car":
                    b.Box(new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 4.0f));
                    b.Box(new Vector3(0, 1.15f, -0.2f), new Vector3(1.6f, 0.6f, 2.0f));
                    for (int i = 0; i < 4; i++) b.CylinderX(new Vector3(i % 2 == 0 ? 0.9f : -0.9f, 0.35f, i < 2 ? 1.3f : -1.3f), 0.35f, 0.25f, 10);
                    break;
                case "bus":
                    // umgestürzt: liegt auf der Seite
                    b.BoxRot(new Vector3(0, 1.3f, 0), new Vector3(2.6f, 3.0f, 11f), new Vector3(0, 0, 80));
                    b.BoxRot(new Vector3(-0.3f, 2.6f, 0), new Vector3(0.2f, 1.2f, 10.5f), new Vector3(0, 0, 80));
                    break;
                case "truck":
                    b.Box(new Vector3(0, 0.9f, 0.8f), new Vector3(2.4f, 1.2f, 4.5f));
                    b.Box(new Vector3(0, 1.9f, -2.0f), new Vector3(2.3f, 1.9f, 1.8f));
                    for (int i = 0; i < 6; i++) b.CylinderX(new Vector3(i % 2 == 0 ? 1.2f : -1.2f, 0.5f, -2f + (i / 2) * 2f), 0.5f, 0.35f, 10);
                    break;
                case "barrel":
                    b.Cylinder(Vector3.zero, 0.3f, 0.7f, 12);
                    b.Torus(new Vector3(0, 0.35f, 0), 0.3f, 0.02f, 12, 4);
                    break;
                case "drum":
                    b.Cylinder(Vector3.zero, 0.4f, 1.0f, 12);
                    b.Torus(new Vector3(0, 0.3f, 0), 0.4f, 0.025f, 12, 4); b.Torus(new Vector3(0, 0.7f, 0), 0.4f, 0.025f, 12, 4);
                    break;
                case "battery":
                    b.Box(new Vector3(0, 0.15f, 0), new Vector3(0.25f, 0.3f, 0.4f));
                    b.Cylinder(new Vector3(0.05f, 0.3f, 0.1f), 0.03f, 0.05f, 6); b.Cylinder(new Vector3(0.05f, 0.3f, -0.1f), 0.03f, 0.05f, 6);
                    break;
                case "tool":
                    b.BoxRot(new Vector3(0, 0.05f, 0), new Vector3(0.06f, 0.06f, 1.2f), new Vector3(0, 20, 0));
                    b.BoxRot(new Vector3(0.18f, 0.05f, 0.55f), new Vector3(0.35f, 0.05f, 0.2f), new Vector3(0, 20, 0));
                    break;
                case "pot":
                    b.Cylinder(Vector3.zero, 0.15f, 0.3f, 10, true, 0.22f);
                    break;
                case "sheetmetal":
                    b.BoxRot(new Vector3(0, 0.1f, 0), new Vector3(0.9f, 0.05f, 0.7f), new Vector3(8, 0, 12));
                    b.BoxRot(new Vector3(0.2f, 0.2f, 0), new Vector3(0.5f, 0.05f, 0.5f), new Vector3(-20, 30, 5));
                    break;
                case "board":
                    b.Box(new Vector3(0, 0.03f, 0), new Vector3(0.4f, 0.04f, 0.3f));
                    b.Box(new Vector3(0.05f, 0.07f, 0.02f), new Vector3(0.1f, 0.04f, 0.1f));
                    break;
                case "shards":
                    for (int i = 0; i < 5; i++) b.BoxRot(new Vector3((i - 2) * 0.12f, 0.05f, (i % 2) * 0.15f), new Vector3(0.15f, 0.05f, 0.1f), new Vector3(i * 17, i * 60, i * 11));
                    break;
                case "chunk":
                    b.BoxRot(new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.4f, 0.5f), new Vector3(10, 25, 5));
                    break;
                case "netpiece":
                case "net":
                    {
                        float s = shape == "net" ? 2.2f : 0.8f;
                        for (int i = 0; i < 5; i++) b.BoxRot(new Vector3((i - 2) * s * 0.2f, 0.1f, 0), new Vector3(0.03f, 0.05f, s), new Vector3(0, 5 * i, 0));
                        for (int i = 0; i < 5; i++) b.BoxRot(new Vector3(0, 0.12f, (i - 2) * s * 0.2f), new Vector3(s, 0.05f, 0.03f), new Vector3(0, -4 * i, 0));
                        if (shape == "net") b.Blob(new Vector3(0, 0, 0), 0.9f, 0.6f, 7, 3, 3, 0.4f);
                        break;
                    }
                case "cable":
                    b.Cylinder(new Vector3(0, 0, 0), 0.35f, 0.08f, 12); b.Cylinder(new Vector3(0, 0.42f, 0), 0.35f, 0.08f, 12);
                    b.Cylinder(new Vector3(0, 0.08f, 0), 0.12f, 0.34f, 8);
                    b.Torus(new Vector3(0, 0.25f, 0), 0.22f, 0.07f, 12, 5);
                    break;
                case "bolts":
                    for (int i = 0; i < 7; i++) b.BoxRot(new Vector3((i % 3 - 1) * 0.12f, 0.04f + (i / 3) * 0.05f, (i % 2) * 0.1f - 0.05f), new Vector3(0.04f, 0.04f, 0.18f), new Vector3(0, i * 50, 0));
                    break;
                case "girder":
                    b.Box(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.1f, 4.5f));
                    b.Box(new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.1f, 4.5f));
                    b.Box(new Vector3(0, 0.25f, 0), new Vector3(0.08f, 0.35f, 4.5f));
                    break;
                case "gear":
                    b.Cylinder(Vector3.zero, 0.35f, 0.12f, 14);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * 360f;
                        b.BoxRot(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.38f, 0.06f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.38f), new Vector3(0.1f, 0.12f, 0.08f), new Vector3(0, -a, 0));
                    }
                    break;
                case "machine":
                    b.Box(new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.7f));
                    b.CylinderX(new Vector3(0, 0.55f, 0.36f), 0.18f, 0.5f, 10);
                    b.Cylinder(new Vector3(-0.3f, 0.7f, -0.2f), 0.08f, 0.35f, 8);
                    break;
                case "engine":
                    b.Box(new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1.0f, 1.6f));
                    for (int i = 0; i < 4; i++) b.Cylinder(new Vector3(0, 1.0f, -0.6f + i * 0.4f), 0.14f, 0.25f, 8);
                    break;
                case "pipe":
                    b.CylinderZ(new Vector3(0, 0.25f, 0), 0.25f, 1.8f, 10);
                    break;
                case "canister":
                    b.Box(new Vector3(0, 0.35f, 0), new Vector3(0.45f, 0.7f, 0.25f));
                    b.Cylinder(new Vector3(0.12f, 0.7f, 0), 0.05f, 0.1f, 6);
                    b.Box(new Vector3(-0.05f, 0.78f, 0), new Vector3(0.2f, 0.05f, 0.05f));
                    break;
                case "ewaste":
                    b.Box(new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.2f, 0.4f));
                    b.BoxRot(new Vector3(0.1f, 0.25f, 0), new Vector3(0.35f, 0.05f, 0.3f), new Vector3(0, 30, 15));
                    break;
                case "hullpart":
                    b.BoxRot(new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.15f, 2.2f), new Vector3(0, 0, 30));
                    b.BoxRot(new Vector3(0.3f, 0.2f, 0.5f), new Vector3(0.6f, 0.6f, 0.1f), new Vector3(0, 20, 0));
                    break;
                case "buoy":
                    b.Sphere(new Vector3(0, 0.3f, 0), 0.45f, 10, 7);
                    b.Cylinder(new Vector3(0, 0.6f, 0), 0.06f, 0.8f, 6);
                    break;
                case "oil":
                    b.Blob(Vector3.zero, 2.2f, 0.02f, 16, 2, 5, 0.35f);
                    break;
                case "anchor":
                    b.Box(new Vector3(0, 0.6f, 0), new Vector3(0.15f, 1.2f, 0.15f));
                    b.BoxRot(new Vector3(0.3f, 0.12f, 0), new Vector3(0.6f, 0.12f, 0.15f), new Vector3(0, 0, 30));
                    b.BoxRot(new Vector3(-0.3f, 0.12f, 0), new Vector3(0.6f, 0.12f, 0.15f), new Vector3(0, 0, -30));
                    b.Torus(new Vector3(0, 1.25f, 0), 0.15f, 0.04f, 10, 4);
                    break;
                case "rack":
                    b.Box(new Vector3(0, 1.0f, 0), new Vector3(0.9f, 2.0f, 0.9f));
                    for (int i = 0; i < 6; i++) b.Box(new Vector3(0, 0.3f + i * 0.28f, 0.46f), new Vector3(0.8f, 0.05f, 0.02f));
                    break;
                case "core":
                    b.BoxRot(new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.4f, 0.4f), new Vector3(45, 45, 0));
                    break;
                case "frozen":
                    b.Box(new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 1.2f));
                    b.CylinderX(new Vector3(0, 1.1f, 0.4f), 0.25f, 1.0f, 10);
                    break;
                case "panel":
                    b.BoxRot(new Vector3(0, 0.2f, 0), new Vector3(1.2f, 0.05f, 0.8f), new Vector3(20, 10, 0));
                    break;
                case "shuttle":
                    b.CylinderZ(new Vector3(0, 1.6f, 0), 1.5f, 9f, 12);
                    b.BoxRot(new Vector3(0, 1.0f, -1f), new Vector3(8f, 0.3f, 3f), new Vector3(0, 0, 8));
                    b.Cylinder(new Vector3(0, 0, 4.5f), 1.2f, 0.01f, 10, true, 1.2f);
                    break;
                case "drone":
                    b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.15f, 0.4f));
                    for (int i = 0; i < 4; i++) b.Cylinder(new Vector3(i % 2 == 0 ? 0.35f : -0.35f, 0.15f, i < 2 ? 0.35f : -0.35f), 0.15f, 0.03f, 8);
                    break;
                default:
                    b.Box(new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f));
                    break;
            }
        }

        /// <summary>Müllberg mit Würfel- und Flaschenstücken.</summary>
        public static Mesh Mound(int seed)
        {
            return Get("mound_" + seed, b =>
            {
                b.Blob(Vector3.zero, 1f, 1f, 14, 6, seed, 0.25f);
                for (int i = 0; i < 18; i++)
                {
                    float a = MeshBuilder.Hash01(i, 1, seed) * Mathf.PI * 2, r = 0.2f + MeshBuilder.Hash01(i, 2, seed) * 0.7f;
                    float h = Mathf.Sqrt(Mathf.Max(0, 1 - r * r)) * 0.95f;
                    b.BoxRot(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), Vector3.one * (0.1f + 0.12f * MeshBuilder.Hash01(i, 3, seed)), new Vector3(i * 23, i * 41, i * 7));
                }
            });
        }
    }
}
