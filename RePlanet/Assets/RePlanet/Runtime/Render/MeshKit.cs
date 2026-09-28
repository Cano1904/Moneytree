using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Baut prozedurale Meshes aus Grundformen (alle Formen des Spiels entstehen hier – keine Fremdmodelle).
    /// Über <see cref="Sub"/> lassen sich Untermeshes füllen (z. B. 0 = Grundfarbe, 1 = dunkle Teile, 2 = Metall …),
    /// die später mit je einem eigenen Material gezeichnet werden.
    /// </summary>
    public class MeshBuilder
    {
        public const int MaxSub = 8;
        static readonly List<int> emptyTris = new List<int>();

        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> col = new List<Color>();
        readonly List<int>[] ts = new List<int>[MaxSub];
        public Matrix4x4 M = Matrix4x4.identity;
        public Color Tint = Color.white;
        /// <summary>Aktuelles Untermesh (0 … MaxSub-1), in das neue Dreiecke geschrieben werden.</summary>
        public int Sub;
        public int VertexCount { get { return v.Count; } }

        int V(Vector3 p, Vector3 normal, Vector2 u)
        {
            v.Add(M.MultiplyPoint3x4(p));
            n.Add(M.MultiplyVector(normal).normalized);
            uv.Add(u);
            col.Add(Tint);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            var l = ts[Sub];
            if (l == null) { l = new List<int>(); ts[Sub] = l; }
            l.Add(a); l.Add(b); l.Add(c);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i0 = V(a, normal, new Vector2(0, 0)), i1 = V(b, normal, new Vector2(1, 0)), i2 = V(c, normal, new Vector2(1, 1)), i3 = V(d, normal, new Vector2(0, 1));
            Tri(i0, i2, i1); Tri(i0, i3, i2);
        }

        /// <summary>Viereck mit automatisch bestimmter Normale; die Vorderseite zeigt in Richtung <paramref name="outward"/>.</summary>
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            var nr = Vector3.Cross(b - a, c - a);
            if (nr.sqrMagnitude < 1e-12f) nr = outward;
            if (Vector3.Dot(nr, outward) < 0) { var t = b; b = d; d = t; nr = -nr; }
            Quad(a, b, c, d, nr.normalized);
        }

        /// <summary>Dreieck, dessen Vorderseite in Richtung <paramref name="outward"/> zeigt.</summary>
        public void TriFace(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var nr = -Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nr, outward) < 0) { var t = b; b = c; c = t; nr = -nr; }
            nr = nr.sqrMagnitude < 1e-12f ? outward.normalized : nr.normalized;
            int i0 = V(a, nr, new Vector2(0, 0)), i1 = V(b, nr, new Vector2(1, 0)), i2 = V(c, nr, new Vector2(0.5f, 1));
            Tri(i0, i1, i2);
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

        /// <summary>Quader ohne Unterseite (für Teile, die auf dem Boden oder an einer Wand liegen – spart Ecken).</summary>
        public void BoxNoBottom(Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, Vector3.back);
            Quad(p101, p111, p011, p001, Vector3.forward);
            Quad(p001, p011, p010, p000, Vector3.left);
            Quad(p100, p110, p111, p101, Vector3.right);
            Quad(p010, p011, p111, p110, Vector3.up);
        }

        /// <summary>Quader mit Rotation um die Y-Achse (Grad) und optionaler Neigung.</summary>
        public void BoxRot(Vector3 c, Vector3 s, Vector3 euler)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
            Box(Vector3.zero, s);
            M = old;
        }

        /// <summary>Verbeulter Quader: Ecken zufällig verschoben (Pressballen, Schrottbrocken, Scherben).</summary>
        public void BoxJ(Vector3 c, Vector3 s, Vector3 euler, float jitter, int seed)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
            var h = s * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var q = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                q += new Vector3(Hash01(i, 1, seed) - 0.5f, Hash01(i, 2, seed) - 0.5f, Hash01(i, 3, seed) - 0.5f) * 2f * jitter;
                p[i] = q;
            }
            Face(p[0], p[2], p[3], p[1], Vector3.back);
            Face(p[4], p[5], p[7], p[6], Vector3.forward);
            Face(p[0], p[4], p[6], p[2], Vector3.left);
            Face(p[1], p[3], p[7], p[5], Vector3.right);
            Face(p[2], p[6], p[7], p[3], Vector3.up);
            Face(p[0], p[1], p[5], p[4], Vector3.down);
            M = old;
        }

        /// <summary>Balken mit quadratischem Querschnitt von a nach b (Gitterträger, Geländer, Streben).</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float h = -1f)
        {
            if (h < 0) h = w;
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);
            var old = M;
            M = old * Matrix4x4.TRS((a + b) * 0.5f, rot, Vector3.one);
            Box(Vector3.zero, new Vector3(w, h, len));
            M = old;
        }

        /// <summary>Rohr/Seil von a nach b.</summary>
        public void Tube(Vector3 a, Vector3 b, float r, int seg = 6, bool caps = false, float rEnd = -1f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var old = M;
            M = old * Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
            Cylinder(Vector3.zero, r, len, seg, caps, rEnd);
            M = old;
        }

        /// <summary>Satteldach-Prisma: Grundfläche size.x × size.z, First entlang X in Höhe size.y (Mitte der Grundfläche bei c).</summary>
        public void Prism(Vector3 c, Vector3 s)
        {
            float hx = s.x * 0.5f, hz = s.z * 0.5f, hy = s.y;
            Vector3 a0 = c + new Vector3(-hx, 0, -hz), a1 = c + new Vector3(hx, 0, -hz), b0 = c + new Vector3(-hx, 0, hz), b1 = c + new Vector3(hx, 0, hz);
            Vector3 r0 = c + new Vector3(-hx, hy, 0), r1 = c + new Vector3(hx, hy, 0);
            Face(a0, a1, r1, r0, new Vector3(0, hz, -hy));
            Face(b0, r0, r1, b1, new Vector3(0, hz, hy));
            TriFace(a0, r0, b0, Vector3.left);
            TriFace(a1, b1, r1, Vector3.right);
            Face(a0, b0, b1, a1, Vector3.down);
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

        /// <summary>Zerknautschte Kugel (Tüten, Knäuel, Moos, Schneehaufen).</summary>
        public void Crumple(Vector3 c, float r, float squashY, int seed, float amount, int seg = 7, int rings = 5)
        {
            int start = v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float pv = y / (float)rings;
                float th = pv * Mathf.PI;
                for (int x = 0; x <= seg; x++)
                {
                    float ph = x / (float)seg * Mathf.PI * 2;
                    float j = (y == 0 || y == rings) ? 1f : 1f + amount * (Hash01(x % seg, y, seed) - 0.5f) * 2f;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    V(c + new Vector3(d.x * r * j, d.y * r * squashY * j, d.z * r * j), (d + new Vector3(Hash01(x % seg, y, seed + 7) - 0.5f, 0, Hash01(x % seg, y, seed + 9) - 0.5f) * amount).normalized, new Vector2(x / (float)seg, 1 - pv));
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

        /// <summary>Torus mit beliebiger Lage (euler) – Reifen, Kettenglieder, Fender.</summary>
        public void TorusRot(Vector3 c, Vector3 euler, float R, float r, int seg = 14, int sides = 6)
        {
            var old = M;
            M = old * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
            Torus(Vector3.zero, R, r, seg, sides);
            M = old;
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

        /// <summary>Hängt alle Ecken und Dreiecke (aller Untermeshes) an fremde Listen an.</summary>
        public void AppendTo(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            int baseIdx = verts.Count;
            verts.AddRange(v); norms.AddRange(n); uvs.AddRange(uv);
            for (int s = 0; s < MaxSub; s++)
            {
                var l = ts[s];
                if (l == null) continue;
                for (int i = 0; i < l.Count; i++) tris.Add(l[i] + baseIdx);
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
            int subs = 1;
            for (int i = 0; i < MaxSub; i++) if (ts[i] != null && ts[i].Count > 0) subs = i + 1;
            m.subMeshCount = subs;
            for (int i = 0; i < subs; i++) m.SetTriangles(ts[i] ?? emptyTris, i, false);
            m.RecalculateBounds();
            return m;
        }

        public void Clear()
        {
            v.Clear(); n.Clear(); uv.Clear(); col.Clear();
            for (int i = 0; i < MaxSub; i++) if (ts[i] != null) ts[i].Clear();
            M = Matrix4x4.identity; Sub = 0;
        }
    }

    /// <summary>Mehrere Materialien → je ein MeshBuilder; ergibt ein GameObject mit Untermeshes.</summary>
    public class MultiBuilder
    {
        readonly Dictionary<Material, MeshBuilder> parts = new Dictionary<Material, MeshBuilder>();
        readonly List<Material> order = new List<Material>();
        public Matrix4x4 M = Matrix4x4.identity;

        public MeshBuilder For(Material m)
        {
            MeshBuilder b;
            if (!parts.TryGetValue(m, out b)) { b = new MeshBuilder(); parts[m] = b; order.Add(m); }
            b.M = M;
            return b;
        }

        public bool Empty
        {
            get { foreach (var kv in parts) if (kv.Value.VertexCount > 0) return false; return true; }
        }

        public int VertexCount
        {
            get { int n = 0; foreach (var kv in parts) n += kv.Value.VertexCount; return n; }
        }

        public GameObject Build(string name, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            foreach (var mat in order)
            {
                var b = parts[mat];
                if (b.VertexCount == 0) continue;
                var child = new GameObject(mat.name);
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = b.Build(name + "_" + mat.name);
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            return go;
        }

        /// <summary>
        /// Ein einziges Mesh mit einem Untermesh je Material (ein Renderer, gleiche Zahl an Draw-Calls, aber nur ein
        /// Objekt für das Culling). Für räumlich zusammengefasste Blöcke (Stadtviertel, Hintergrund-Sektoren).
        /// </summary>
        public GameObject BuildCombined(string name, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var mats = new List<Material>();
            var tris = new List<List<int>>();
            foreach (var mat in order)
            {
                var b = parts[mat];
                if (b.VertexCount == 0) continue;
                var all = new List<int>();
                b.AppendTo(verts, norms, uvs, all);
                mats.Add(mat);
                tris.Add(all);
            }
            if (mats.Count == 0) return go;
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs);
            m.subMeshCount = mats.Count;
            for (int i = 0; i < mats.Count; i++) m.SetTriangles(tris[i], i, false);
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }
    }

    /// <summary>
    /// Mehrere MultiBuilder in einem räumlichen Raster: Jeder Block wird ein eigenes Objekt, damit Unity
    /// unsichtbare Blöcke (und ihre Schatten) wegschneidet, statt die ganze Stadt in jedem Bild zu zeichnen.
    /// </summary>
    public class ChunkBuilder
    {
        readonly Dictionary<long, MultiBuilder> chunks = new Dictionary<long, MultiBuilder>();
        readonly float size;
        public ChunkBuilder(float chunkSize) { size = chunkSize; }

        public MultiBuilder At(float x, float z)
        {
            long k = ((long)Mathf.FloorToInt(x / size) << 32) ^ (uint)Mathf.FloorToInt(z / size);
            MultiBuilder mb;
            if (!chunks.TryGetValue(k, out mb)) { mb = new MultiBuilder(); chunks[k] = mb; }
            return mb;
        }

        public GameObject Build(string name, Transform parent, bool shadows)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            int i = 0;
            foreach (var kv in chunks)
            {
                if (kv.Value.Empty) continue;
                kv.Value.BuildCombined(name + "_" + (i++), go.transform, shadows);
            }
            return go;
        }
    }

    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>Untermesh-Belegung der Müllformen.</summary>
        public const int Main = 0, Dark = 1, Metal = 2, Light = 3, Signal = 4;
        public const int TrashSlots = 5;

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

        /// <summary>
        /// Formen der Müllobjekte (Einheitsgröße ~1 m, Ursprung am Boden). Jede Materialklasse hat eine eigene Silhouette.
        /// Untermeshes: 0 = Grundfarbe des Typs, 1 = dunkel (Gummi, Glas, Kunststoff), 2 = Metall, 3 = hell (Etikett, Papier), 4 = Signalfarbe.
        /// </summary>
        public static Mesh Trash(string shape)
        {
            return Get("trash_" + shape, b => BuildTrash(b, shape));
        }

        static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            int s = b.Sub;
            b.Sub = Dark; b.CylinderX(c, r, w, 12);
            b.Sub = Metal; b.CylinderX(c + new Vector3(Mathf.Sign(c.x) * 0.01f, 0, 0), r * 0.55f, w + 0.02f, 8);
            b.Sub = s;
        }

        static void BuildTrash(MeshBuilder b, string shape)
        {
            switch (shape)
            {
                case "sheet": // Zeitungsbündel mit Schnur und losen Blättern
                    b.Sub = Main;
                    b.BoxJ(new Vector3(0, 0.12f, 0), new Vector3(0.7f, 0.24f, 0.5f), Vector3.zero, 0.015f, 11);
                    b.BoxRot(new Vector3(0.05f, 0.25f, 0.02f), new Vector3(0.62f, 0.02f, 0.46f), new Vector3(0, 12, 0));
                    b.BoxRot(new Vector3(0.52f, 0.01f, 0.18f), new Vector3(0.42f, 0.01f, 0.3f), new Vector3(0, 38, 3));
                    b.BoxRot(new Vector3(-0.5f, 0.012f, -0.2f), new Vector3(0.36f, 0.01f, 0.28f), new Vector3(0, -24, -4));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.12f, 0.1f), new Vector3(0.72f, 0.25f, 0.025f));
                    b.Box(new Vector3(0.15f, 0.12f, 0), new Vector3(0.025f, 0.25f, 0.52f));
                    for (int i = 0; i < 4; i++) b.BoxRot(new Vector3(0.05f + (i % 2) * 0.02f, 0.262f, -0.12f + i * 0.07f), new Vector3(i == 0 ? 0.42f : 0.3f, 0.004f, i == 0 ? 0.05f : 0.02f), new Vector3(0, 12, 0));
                    break;
                case "box": // Karton mit aufgeklappten Laschen
                    b.Sub = Main;
                    b.BoxJ(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), Vector3.zero, 0.03f, 5);
                    b.BoxRot(new Vector3(0, 0.66f, 0.36f), new Vector3(0.78f, 0.02f, 0.3f), new Vector3(-62, 0, 0));
                    b.BoxRot(new Vector3(0, 0.64f, -0.38f), new Vector3(0.78f, 0.02f, 0.3f), new Vector3(70, 0, 0));
                    b.BoxRot(new Vector3(0.46f, 0.66f, 0), new Vector3(0.28f, 0.02f, 0.56f), new Vector3(0, 0, -58));
                    b.BoxRot(new Vector3(-0.44f, 0.6f, 0.02f), new Vector3(0.24f, 0.02f, 0.56f), new Vector3(0, 0, 88));
                    b.Sub = Light;
                    b.Box(new Vector3(0.15f, 0.32f, 0.305f), new Vector3(0.26f, 0.16f, 0.01f));
                    b.Sub = Dark;
                    b.Box(new Vector3(-0.2f, 0.36f, 0.305f), new Vector3(0.04f, 0.16f, 0.01f));
                    b.Box(new Vector3(-0.28f, 0.36f, 0.305f), new Vector3(0.04f, 0.16f, 0.01f));
                    b.Box(new Vector3(0, 0.3f, -0.301f), new Vector3(0.1f, 0.6f, 0.01f));
                    break;
                case "bottle":
                    b.Sub = Main;
                    b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.115f, 0f), new Vector2(0.125f, 0.02f), new Vector2(0.13f, 0.34f), new Vector2(0.1f, 0.42f), new Vector2(0.05f, 0.5f), new Vector2(0.045f, 0.62f), new Vector2(0.052f, 0.64f), new Vector2(0f, 0.66f) }, 12);
                    b.Sub = Light;
                    b.Cylinder(new Vector3(0, 0.12f, 0), 0.134f, 0.15f, 12, false);
                    b.Sub = Signal;
                    b.Cylinder(new Vector3(0, 0.5f, 0), 0.058f, 0.05f, 10, false, 0.05f);
                    b.Sub = Metal;
                    b.Cylinder(new Vector3(0, 0.635f, 0), 0.056f, 0.04f, 10);
                    break;
                case "pbottle": // zerdrückte, liegende Plastikflasche
                    {
                        b.Sub = Main;
                        var old = b.M;
                        b.M = old * Matrix4x4.TRS(new Vector3(-0.3f, 0.11f, 0), Quaternion.Euler(0, 0, -86), new Vector3(1f, 1f, 0.72f));
                        b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.13f, 0.01f), new Vector2(0.15f, 0.08f), new Vector2(0.12f, 0.16f), new Vector2(0.15f, 0.25f), new Vector2(0.14f, 0.36f), new Vector2(0.12f, 0.42f), new Vector2(0.05f, 0.55f), new Vector2(0.045f, 0.6f), new Vector2(0f, 0.61f) }, 9);
                        b.Sub = Signal;
                        b.Cylinder(new Vector3(0, 0.26f, 0), 0.152f, 0.1f, 9, false);
                        b.Sub = Light;
                        b.Cylinder(new Vector3(0, 0.58f, 0), 0.055f, 0.06f, 8);
                        b.M = old;
                        break;
                    }
                case "bag": // zerknautschte Tüte mit Henkeln
                    b.Sub = Main;
                    b.Crumple(new Vector3(0, 0.17f, 0), 0.3f, 0.58f, 31, 0.22f, 9, 6);
                    b.Crumple(new Vector3(0.05f, 0.36f, 0.02f), 0.09f, 1.1f, 32, 0.3f, 6, 4);
                    b.TorusRot(new Vector3(-0.04f, 0.42f, 0.05f), new Vector3(80, 20, 0), 0.08f, 0.015f, 10, 4);
                    b.TorusRot(new Vector3(0.1f, 0.41f, -0.02f), new Vector3(70, -30, 10), 0.08f, 0.015f, 10, 4);
                    b.Sub = Signal;
                    b.BoxRot(new Vector3(0.02f, 0.2f, 0.27f), new Vector3(0.16f, 0.12f, 0.02f), new Vector3(-20, 0, 0));
                    break;
                case "can": // liegende, eingedrückte Getränkedose
                    b.Sub = Main;
                    b.CylinderX(new Vector3(0, 0.09f, 0), 0.09f, 0.24f, 12);
                    b.Sub = Metal;
                    b.CylinderX(new Vector3(0.135f, 0.09f, 0), 0.08f, 0.03f, 12);
                    b.CylinderX(new Vector3(-0.135f, 0.09f, 0), 0.084f, 0.03f, 12);
                    b.Box(new Vector3(0.152f, 0.12f, 0), new Vector3(0.006f, 0.03f, 0.02f));
                    b.Sub = Light;
                    b.CylinderX(new Vector3(0.02f, 0.09f, 0), 0.092f, 0.06f, 12);
                    b.Sub = Dark;
                    b.BoxRot(new Vector3(-0.04f, 0.17f, 0.03f), new Vector3(0.08f, 0.02f, 0.05f), new Vector3(0, 20, 10));
                    break;
                case "appliance": // Mikrowelle/Toaster/Fernseher
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.33f, 0), new Vector3(0.7f, 0.56f, 0.5f));
                    b.Sub = Dark;
                    b.Box(new Vector3(-0.08f, 0.33f, 0.252f), new Vector3(0.42f, 0.36f, 0.02f));
                    for (int i = 0; i < 4; i++) b.Box(new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0.025f, i < 2 ? -0.2f : 0.2f), new Vector3(0.06f, 0.05f, 0.06f));
                    for (int i = 0; i < 5; i++) b.Box(new Vector3(-0.351f, 0.36f + i * 0.04f, 0), new Vector3(0.01f, 0.012f, 0.3f));
                    b.Tube(new Vector3(0.2f, 0.1f, -0.25f), new Vector3(0.35f, 0.02f, -0.5f), 0.012f, 4);
                    b.Tube(new Vector3(0.35f, 0.02f, -0.5f), new Vector3(0.1f, 0.02f, -0.75f), 0.012f, 4);
                    b.Sub = Metal;
                    b.Box(new Vector3(0.26f, 0.33f, 0.252f), new Vector3(0.14f, 0.44f, 0.02f));
                    b.Box(new Vector3(-0.08f, 0.525f, 0.252f), new Vector3(0.44f, 0.03f, 0.022f));
                    b.Sub = Signal;
                    b.Box(new Vector3(0.26f, 0.46f, 0.265f), new Vector3(0.1f, 0.05f, 0.01f));
                    b.Sub = Light;
                    for (int i = 0; i < 3; i++) b.Cylinder(new Vector3(0.26f, 0.26f - i * 0.07f, 0.262f), 0.022f, 0.012f, 8);
                    break;
                case "fridge": // alter Kühlschrank, leicht verzogen, Tür einen Spalt offen
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.92f, 0), new Vector3(0.8f, 1.76f, 0.72f));
                    b.BoxRot(new Vector3(0.02f, 1.47f, 0.4f), new Vector3(0.78f, 0.6f, 0.07f), new Vector3(0, -8, 0));
                    b.Box(new Vector3(0, 0.62f, 0.39f), new Vector3(0.78f, 1.08f, 0.06f));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.07f, 0.34f), new Vector3(0.7f, 0.12f, 0.06f));
                    for (int i = 0; i < 4; i++) b.Box(new Vector3(0, 0.05f + i * 0.03f, 0.372f), new Vector3(0.6f, 0.01f, 0.01f));
                    b.BoxJ(new Vector3(-0.25f, 1.2f, 0.425f), new Vector3(0.22f, 0.15f, 0.01f), Vector3.zero, 0.02f, 3);
                    b.BoxJ(new Vector3(0.28f, 0.3f, 0.425f), new Vector3(0.14f, 0.24f, 0.01f), Vector3.zero, 0.03f, 4);
                    b.Sub = Metal;
                    b.Box(new Vector3(0.32f, 1.3f, 0.47f), new Vector3(0.04f, 0.32f, 0.05f));
                    b.Box(new Vector3(0.32f, 0.9f, 0.43f), new Vector3(0.04f, 0.32f, 0.05f));
                    b.Box(new Vector3(-0.38f, 1.72f, 0.4f), new Vector3(0.06f, 0.06f, 0.06f));
                    b.Box(new Vector3(-0.38f, 0.12f, 0.4f), new Vector3(0.06f, 0.06f, 0.06f));
                    b.Sub = Signal;
                    b.Box(new Vector3(-0.1f, 1.55f, 0.445f), new Vector3(0.07f, 0.07f, 0.01f));
                    b.Box(new Vector3(0.12f, 1.36f, 0.445f), new Vector3(0.06f, 0.08f, 0.01f));
                    b.Sub = Light;
                    b.Box(new Vector3(-0.05f, 0.95f, 0.425f), new Vector3(0.18f, 0.22f, 0.005f));
                    break;
                case "cart": // Einkaufswagen aus Drahtgitter
                    {
                        b.Sub = Main;
                        float y0 = 0.45f, y1 = 1.0f, hx = 0.3f, z0 = -0.45f, z1 = 0.45f;
                        for (int i = 0; i <= 6; i++)
                        {
                            float z = Mathf.Lerp(z0, z1, i / 6f);
                            b.Beam(new Vector3(-hx, y0, z), new Vector3(-hx - 0.04f, y1, z), 0.018f);
                            b.Beam(new Vector3(hx, y0, z), new Vector3(hx + 0.04f, y1, z), 0.018f);
                        }
                        for (int i = 0; i <= 4; i++)
                        {
                            float x = Mathf.Lerp(-hx, hx, i / 4f);
                            b.Beam(new Vector3(x * 0.9f, y0, z1), new Vector3(x, y1, z1 + 0.06f), 0.018f);
                            b.Beam(new Vector3(x, y0 + 0.1f, z0), new Vector3(x * 1.1f, y1, z0 - 0.06f), 0.018f);
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            float y = Mathf.Lerp(y0, y1, k / 2f);
                            b.Beam(new Vector3(-hx - k * 0.02f, y, z0 - k * 0.03f), new Vector3(-hx - k * 0.02f, y, z1 + k * 0.03f), 0.025f);
                            b.Beam(new Vector3(hx + k * 0.02f, y, z0 - k * 0.03f), new Vector3(hx + k * 0.02f, y, z1 + k * 0.03f), 0.025f);
                            b.Beam(new Vector3(-hx - k * 0.02f, y, z1 + k * 0.03f), new Vector3(hx + k * 0.02f, y, z1 + k * 0.03f), 0.025f);
                            b.Beam(new Vector3(-hx - k * 0.02f, y, z0 - k * 0.03f), new Vector3(hx + k * 0.02f, y, z0 - k * 0.03f), 0.025f);
                        }
                        b.Sub = Metal;
                        b.Box(new Vector3(0, y0, 0), new Vector3(0.6f, 0.02f, 0.9f));
                        b.Box(new Vector3(0, 0.22f, 0.05f), new Vector3(0.52f, 0.02f, 0.75f));
                        b.Beam(new Vector3(-0.26f, 0.08f, 0.38f), new Vector3(-hx, y0, z1), 0.03f);
                        b.Beam(new Vector3(0.26f, 0.08f, 0.38f), new Vector3(hx, y0, z1), 0.03f);
                        b.Beam(new Vector3(-0.26f, 0.08f, -0.34f), new Vector3(-hx, y0, z0 + 0.1f), 0.03f);
                        b.Beam(new Vector3(0.26f, 0.08f, -0.34f), new Vector3(hx, y0, z0 + 0.1f), 0.03f);
                        b.Beam(new Vector3(-hx, y1, z0 - 0.06f), new Vector3(-hx, y1 + 0.08f, z0 - 0.2f), 0.025f);
                        b.Beam(new Vector3(hx, y1, z0 - 0.06f), new Vector3(hx, y1 + 0.08f, z0 - 0.2f), 0.025f);
                        b.Sub = Dark;
                        b.CylinderX(new Vector3(0.26f, 0.05f, 0.38f), 0.05f, 0.04f, 8); b.CylinderX(new Vector3(-0.26f, 0.05f, 0.38f), 0.05f, 0.04f, 8);
                        b.CylinderX(new Vector3(0.26f, 0.05f, -0.34f), 0.05f, 0.04f, 8); b.CylinderX(new Vector3(-0.26f, 0.05f, -0.34f), 0.05f, 0.04f, 8);
                        b.Sub = Signal;
                        b.CylinderX(new Vector3(0, y1 + 0.08f, z0 - 0.2f), 0.035f, 0.66f, 8);
                        break;
                    }
                case "car": // Autowrack mit Scheiben, Rädern, Stoßstangen
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.6f, 0.05f), new Vector3(1.8f, 0.62f, 3.9f));
                    b.BoxRot(new Vector3(0, 0.95f, 1.45f), new Vector3(1.7f, 0.1f, 1.1f), new Vector3(4, 0, 0));
                    b.BoxJ(new Vector3(0, 1.18f, -0.3f), new Vector3(1.58f, 0.5f, 1.9f), Vector3.zero, 0.05f, 21);
                    b.Box(new Vector3(0, 1.44f, -0.35f), new Vector3(1.45f, 0.06f, 1.5f));
                    b.Box(new Vector3(0.93f, 1.1f, 0.35f), new Vector3(0.08f, 0.08f, 0.14f));
                    b.Sub = Dark;
                    b.BoxRot(new Vector3(0, 1.2f, 0.68f), new Vector3(1.46f, 0.46f, 0.04f), new Vector3(-30, 0, 0));
                    b.BoxRot(new Vector3(0, 1.2f, -1.27f), new Vector3(1.4f, 0.4f, 0.04f), new Vector3(28, 0, 0));
                    b.Box(new Vector3(0.8f, 1.2f, -0.3f), new Vector3(0.02f, 0.36f, 1.6f));
                    b.Box(new Vector3(-0.8f, 1.2f, -0.3f), new Vector3(0.02f, 0.36f, 1.6f));
                    b.Box(new Vector3(0.905f, 0.6f, 0.45f), new Vector3(0.01f, 0.5f, 0.02f));
                    b.Box(new Vector3(-0.905f, 0.6f, -0.35f), new Vector3(0.01f, 0.5f, 0.02f));
                    b.BoxJ(new Vector3(0.5f, 0.92f, -1.2f), new Vector3(0.5f, 0.02f, 0.4f), Vector3.zero, 0.04f, 3);
                    b.Sub = Metal;
                    b.Box(new Vector3(0, 0.42f, 2.03f), new Vector3(1.84f, 0.18f, 0.12f));
                    b.Box(new Vector3(0, 0.42f, -1.93f), new Vector3(1.84f, 0.18f, 0.12f));
                    b.Box(new Vector3(0, 0.66f, 2.0f), new Vector3(0.9f, 0.16f, 0.04f));
                    b.Sub = Light;
                    b.Box(new Vector3(0.62f, 0.72f, 2.0f), new Vector3(0.3f, 0.14f, 0.05f));
                    b.Box(new Vector3(-0.62f, 0.72f, 2.0f), new Vector3(0.3f, 0.14f, 0.05f));
                    b.Sub = Signal;
                    b.Box(new Vector3(0.65f, 0.74f, -1.9f), new Vector3(0.28f, 0.14f, 0.05f));
                    b.Box(new Vector3(-0.65f, 0.74f, -1.9f), new Vector3(0.28f, 0.14f, 0.05f));
                    Wheel(b, new Vector3(0.84f, 0.34f, 1.3f), 0.35f, 0.26f);
                    Wheel(b, new Vector3(-0.84f, 0.34f, 1.3f), 0.35f, 0.26f);
                    Wheel(b, new Vector3(0.84f, 0.34f, -1.25f), 0.35f, 0.26f);
                    b.Sub = Dark;
                    b.BoxRot(new Vector3(-0.84f, 0.12f, -1.25f), new Vector3(0.26f, 0.16f, 0.72f), new Vector3(0, 0, 8)); // platter Reifen
                    break;
                case "bus": // umgestürzter Stadtbus (liegt auf der Seite)
                    {
                        var old = b.M;
                        b.M = old * Matrix4x4.TRS(new Vector3(0, 1.3f, 0), Quaternion.Euler(0, 0, 80), Vector3.one);
                        b.Sub = Main;
                        b.Box(Vector3.zero, new Vector3(2.6f, 3.0f, 11f));
                        b.Sub = Light;
                        b.Box(new Vector3(0, -0.55f, 0), new Vector3(2.64f, 0.3f, 10.9f));
                        b.Box(new Vector3(0, 1.45f, 0), new Vector3(2.5f, 0.12f, 10.8f));
                        b.Sub = Dark;
                        b.Box(new Vector3(1.31f, 0.55f, 0.3f), new Vector3(0.04f, 1.0f, 9.2f));
                        b.Box(new Vector3(-1.31f, 0.55f, 0.3f), new Vector3(0.04f, 1.0f, 9.2f));
                        b.Box(new Vector3(0, 0.45f, 5.51f), new Vector3(2.3f, 1.5f, 0.04f));
                        b.Box(new Vector3(1.32f, -0.2f, 3.6f), new Vector3(0.04f, 2.2f, 1.1f));
                        for (int i = 0; i < 2; i++)
                        {
                            float z = i == 0 ? 3.5f : -3.2f;
                            b.CylinderX(new Vector3(-1.12f, -1.5f, z), 0.55f, 0.4f, 12);
                            b.CylinderX(new Vector3(1.12f, -1.5f, z), 0.55f, 0.4f, 12);
                        }
                        b.Sub = Metal;
                        b.Box(new Vector3(0, -1.3f, 5.55f), new Vector3(2.6f, 0.3f, 0.12f));
                        b.Box(new Vector3(0, -1.3f, -5.55f), new Vector3(2.6f, 0.3f, 0.12f));
                        b.Box(new Vector3(0.4f, 1.55f, -2f), new Vector3(1.2f, 0.2f, 1.6f));
                        for (int i = 0; i < 2; i++) { b.CylinderX(new Vector3(-1.34f, -1.5f, i == 0 ? 3.5f : -3.2f), 0.3f, 0.06f, 8); b.CylinderX(new Vector3(1.34f, -1.5f, i == 0 ? 3.5f : -3.2f), 0.3f, 0.06f, 8); }
                        b.Sub = Signal;
                        b.Box(new Vector3(0, 1.25f, 5.52f), new Vector3(1.8f, 0.3f, 0.04f));
                        b.M = old;
                        break;
                    }
                case "truck":
                    b.Sub = Main;
                    b.Box(new Vector3(0, 1.1f, 0.9f), new Vector3(2.4f, 0.3f, 4.6f));
                    b.Box(new Vector3(0, 1.6f, 1.0f), new Vector3(0.1f, 0.8f, 4.3f));
                    b.Box(new Vector3(1.15f, 1.55f, 1.0f), new Vector3(0.1f, 0.7f, 4.3f));
                    b.Box(new Vector3(-1.15f, 1.55f, 1.0f), new Vector3(0.1f, 0.7f, 4.3f));
                    b.Box(new Vector3(0, 1.55f, 3.2f), new Vector3(2.4f, 0.7f, 0.1f));
                    b.BoxJ(new Vector3(0, 2.0f, -2.0f), new Vector3(2.3f, 1.9f, 1.8f), Vector3.zero, 0.06f, 8);
                    b.Box(new Vector3(0, 1.0f, -2.95f), new Vector3(2.3f, 0.9f, 0.3f));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 2.35f, -2.92f), new Vector3(2.0f, 0.8f, 0.04f));
                    b.Box(new Vector3(1.16f, 2.35f, -2.0f), new Vector3(0.02f, 0.7f, 1.0f));
                    b.Box(new Vector3(-1.16f, 2.35f, -2.0f), new Vector3(0.02f, 0.7f, 1.0f));
                    b.Box(new Vector3(0, 0.75f, 0.9f), new Vector3(1.8f, 0.35f, 5.2f));
                    b.Sub = Metal;
                    b.Box(new Vector3(0, 1.1f, -3.12f), new Vector3(1.4f, 0.5f, 0.06f));
                    b.Box(new Vector3(0, 0.62f, -3.1f), new Vector3(2.44f, 0.22f, 0.2f));
                    b.Cylinder(new Vector3(1.05f, 1.4f, -1.2f), 0.08f, 2.2f, 8);
                    b.Sub = Light;
                    b.Box(new Vector3(0.85f, 1.05f, -3.13f), new Vector3(0.3f, 0.18f, 0.04f));
                    b.Box(new Vector3(-0.85f, 1.05f, -3.13f), new Vector3(0.3f, 0.18f, 0.04f));
                    b.Sub = Signal;
                    b.Box(new Vector3(0, 3.0f, -2.2f), new Vector3(1.2f, 0.12f, 0.3f));
                    for (int i = 0; i < 6; i++) Wheel(b, new Vector3(i % 2 == 0 ? 1.2f : -1.2f, 0.5f, -2f + (i / 2) * 2f), 0.5f, 0.36f);
                    break;
                case "barrel": // Farbeimer / Kältemittelbehälter
                    b.Sub = Main;
                    b.Cylinder(Vector3.zero, 0.3f, 0.7f, 14);
                    b.Sub = Metal;
                    b.Torus(new Vector3(0, 0.18f, 0), 0.302f, 0.018f, 14, 4);
                    b.Torus(new Vector3(0, 0.52f, 0), 0.302f, 0.018f, 14, 4);
                    b.Cylinder(new Vector3(0, 0.7f, 0), 0.31f, 0.04f, 14);
                    b.TorusRot(new Vector3(0, 0.74f, 0), new Vector3(90, 0, 0), 0.26f, 0.012f, 12, 4);
                    b.Sub = Light;
                    b.Cylinder(new Vector3(0, 0.26f, 0), 0.304f, 0.2f, 14, false);
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.36f, 0.3f), new Vector3(0.12f, 0.05f, 0.02f));
                    b.Crumple(new Vector3(0.28f, 0.02f, 0.2f), 0.18f, 0.08f, 5, 0.3f, 8, 3);
                    break;
                case "drum": // Gefahrstofffass mit Warnraute
                    b.Sub = Main;
                    b.Cylinder(Vector3.zero, 0.4f, 1.0f, 16);
                    b.Sub = Metal;
                    b.Torus(new Vector3(0, 0.33f, 0), 0.402f, 0.025f, 16, 4); b.Torus(new Vector3(0, 0.67f, 0), 0.402f, 0.025f, 16, 4);
                    b.Torus(new Vector3(0, 0.99f, 0), 0.39f, 0.03f, 16, 4);
                    b.Cylinder(new Vector3(0.18f, 1.0f, 0.1f), 0.05f, 0.03f, 8);
                    b.Sub = Dark;
                    b.Cylinder(new Vector3(0, 0.02f, 0), 0.404f, 0.1f, 16, false);
                    b.BoxRot(new Vector3(0, 0.02f, 0.46f), new Vector3(0.5f, 0.03f, 0.25f), new Vector3(0, 10, 0));
                    b.Sub = Signal;
                    b.BoxRot(new Vector3(0, 0.5f, 0.395f), new Vector3(0.26f, 0.26f, 0.02f), new Vector3(0, 0, 45));
                    b.Sub = Light;
                    b.BoxRot(new Vector3(0, 0.5f, 0.405f), new Vector3(0.19f, 0.19f, 0.02f), new Vector3(0, 0, 45));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.53f, 0.418f), new Vector3(0.03f, 0.09f, 0.01f));
                    b.Box(new Vector3(0, 0.45f, 0.418f), new Vector3(0.03f, 0.03f, 0.01f));
                    break;
                case "battery":
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.14f, 0), new Vector3(0.25f, 0.28f, 0.4f));
                    b.Sub = Light;
                    b.Box(new Vector3(0.126f, 0.14f, 0), new Vector3(0.01f, 0.14f, 0.32f));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.29f, 0), new Vector3(0.24f, 0.02f, 0.38f));
                    b.Box(new Vector3(0.132f, 0.14f, 0.06f), new Vector3(0.01f, 0.04f, 0.12f));
                    b.Sub = Metal;
                    b.Cylinder(new Vector3(0.05f, 0.3f, 0.12f), 0.028f, 0.05f, 8);
                    b.Sub = Signal;
                    b.Cylinder(new Vector3(0.05f, 0.3f, -0.12f), 0.028f, 0.05f, 8);
                    break;
                case "tool": // rostiger Spaten mit Holzstiel
                    b.Sub = Main;
                    b.Beam(new Vector3(-0.1f, 0.05f, -0.55f), new Vector3(0.1f, 0.06f, 0.4f), 0.05f);
                    b.Sub = Dark;
                    b.Beam(new Vector3(-0.2f, 0.05f, -0.6f), new Vector3(0.02f, 0.05f, -0.56f), 0.05f);
                    b.Sub = Metal;
                    b.BoxRot(new Vector3(0.14f, 0.05f, 0.58f), new Vector3(0.28f, 0.03f, 0.36f), new Vector3(8, 12, 0));
                    b.Box(new Vector3(0.11f, 0.06f, 0.41f), new Vector3(0.08f, 0.06f, 0.08f));
                    b.Sub = Main;
                    b.BoxRot(new Vector3(-0.3f, 0.03f, 0.2f), new Vector3(0.05f, 0.05f, 0.7f), new Vector3(0, -50, 0));
                    b.Sub = Metal;
                    for (int i = 0; i < 5; i++) b.BoxRot(new Vector3(-0.52f + i * 0.04f, 0.03f, 0.48f + i * 0.03f), new Vector3(0.015f, 0.02f, 0.14f), new Vector3(0, -50, 0));
                    break;
                case "pot":
                    b.Sub = Main;
                    b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.2f, 0.26f), new Vector2(0.235f, 0.27f), new Vector2(0.235f, 0.32f), new Vector2(0.2f, 0.32f), new Vector2(0.19f, 0.29f) }, 12);
                    b.Sub = Dark;
                    b.Cylinder(new Vector3(0, 0.26f, 0), 0.19f, 0.02f, 12);
                    b.Beam(new Vector3(0, 0.27f, 0), new Vector3(0.05f, 0.55f, 0.02f), 0.015f);
                    b.Beam(new Vector3(0.03f, 0.45f, 0.01f), new Vector3(0.12f, 0.52f, -0.02f), 0.01f);
                    b.Crumple(new Vector3(0.24f, 0.03f, 0.1f), 0.1f, 0.3f, 3, 0.3f, 6, 3);
                    break;
                case "sheetmetal": // Wellblech, verbogen
                    {
                        b.Sub = Main;
                        for (int i = 0; i < 8; i++)
                        {
                            float z = -0.35f + i * 0.1f;
                            float bend = Mathf.Sin(i * 0.5f) * 0.06f;
                            b.BoxRot(new Vector3(0, 0.1f + bend + (i % 2) * 0.025f, z), new Vector3(0.9f, 0.012f, 0.105f), new Vector3((i % 2 == 0 ? 28 : -28), 0, 6));
                        }
                        b.BoxRot(new Vector3(0.25f, 0.24f, 0.05f), new Vector3(0.45f, 0.02f, 0.4f), new Vector3(-25, 30, 18));
                        b.Sub = Dark;
                        b.BoxJ(new Vector3(-0.2f, 0.13f, 0.1f), new Vector3(0.22f, 0.01f, 0.16f), new Vector3(0, 0, 6), 0.03f, 9);
                        b.BoxJ(new Vector3(0.1f, 0.14f, -0.2f), new Vector3(0.16f, 0.01f, 0.12f), new Vector3(0, 0, 6), 0.03f, 10);
                        break;
                    }
                case "board": // Platine mit Chips und Kondensatoren
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.025f, 0.3f));
                    b.Sub = Dark;
                    b.Box(new Vector3(-0.08f, 0.045f, 0.04f), new Vector3(0.12f, 0.025f, 0.12f));
                    b.Box(new Vector3(0.1f, 0.045f, -0.07f), new Vector3(0.1f, 0.02f, 0.06f));
                    b.Box(new Vector3(0.1f, 0.045f, 0.08f), new Vector3(0.08f, 0.02f, 0.05f));
                    b.Sub = Metal;
                    b.Box(new Vector3(0, 0.035f, -0.14f), new Vector3(0.3f, 0.008f, 0.02f));
                    b.Cylinder(new Vector3(-0.14f, 0.03f, -0.08f), 0.02f, 0.06f, 8);
                    b.Cylinder(new Vector3(-0.1f, 0.03f, -0.09f), 0.015f, 0.045f, 8);
                    b.Sub = Signal;
                    b.Box(new Vector3(0.16f, 0.04f, 0.0f), new Vector3(0.03f, 0.02f, 0.03f));
                    b.Sub = Light;
                    b.Box(new Vector3(-0.08f, 0.058f, 0.04f), new Vector3(0.06f, 0.002f, 0.02f));
                    break;
                case "shards": // Scherben und Flaschenhals
                    b.Sub = Main;
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 0.9f, r = 0.08f + (i % 3) * 0.1f;
                        b.BoxJ(new Vector3(Mathf.Cos(a) * r, 0.04f, Mathf.Sin(a) * r), new Vector3(0.16f - (i % 2) * 0.05f, 0.015f, 0.1f), new Vector3(i * 17 % 40 - 20, i * 60, i * 11 % 30), 0.035f, i + 40);
                    }
                    b.BoxJ(new Vector3(0.05f, 0.1f, 0.02f), new Vector3(0.14f, 0.2f, 0.015f), new Vector3(-20, 30, 12), 0.04f, 51);
                    b.Lathe(new Vector3(-0.15f, 0.03f, 0.12f), new[] { new Vector2(0.09f, 0f), new Vector2(0.07f, 0.06f), new Vector2(0.04f, 0.1f), new Vector2(0.035f, 0.2f), new Vector2(0.04f, 0.21f) }, 8);
                    break;
                case "chunk": // Stahlbrocken mit blanker Schnittfläche
                    b.Sub = Main;
                    b.BoxJ(new Vector3(0, 0.24f, 0), new Vector3(0.72f, 0.42f, 0.5f), new Vector3(8, 25, 5), 0.08f, 13);
                    b.BoxJ(new Vector3(0.36f, 0.1f, 0.3f), new Vector3(0.28f, 0.2f, 0.22f), new Vector3(0, 60, 12), 0.05f, 14);
                    b.Sub = Metal;
                    b.BoxRot(new Vector3(-0.34f, 0.26f, 0.12f), new Vector3(0.02f, 0.34f, 0.4f), new Vector3(8, 25, 5));
                    b.Sub = Dark;
                    b.BoxJ(new Vector3(0.05f, 0.46f, 0), new Vector3(0.3f, 0.02f, 0.2f), new Vector3(8, 25, 5), 0.03f, 15);
                    break;
                case "cable": // Kabeltrommel auf der Kante mit losem Ende
                    b.Sub = Main;
                    b.CylinderX(new Vector3(0, 0.36f, 0), 0.26f, 0.42f, 14);
                    for (int i = 0; i < 4; i++) b.TorusRot(new Vector3(-0.15f + i * 0.1f, 0.36f, 0), new Vector3(0, 0, 90), 0.265f, 0.02f, 14, 4);
                    b.Tube(new Vector3(0.1f, 0.62f, 0), new Vector3(0.35f, 0.3f, 0.35f), 0.03f, 5);
                    b.Tube(new Vector3(0.35f, 0.3f, 0.35f), new Vector3(0.25f, 0.03f, 0.7f), 0.03f, 5);
                    b.Tube(new Vector3(0.25f, 0.03f, 0.7f), new Vector3(-0.2f, 0.03f, 0.85f), 0.03f, 5);
                    b.Sub = Dark;
                    b.CylinderX(new Vector3(0.24f, 0.36f, 0), 0.36f, 0.05f, 16);
                    b.CylinderX(new Vector3(-0.24f, 0.36f, 0), 0.36f, 0.05f, 16);
                    b.Sub = Metal;
                    b.CylinderX(new Vector3(0, 0.36f, 0), 0.06f, 0.58f, 8);
                    break;
                case "bolts": // Haufen aus Schrauben und Muttern
                    b.Sub = Dark;
                    b.Crumple(new Vector3(0, 0.0f, 0), 0.22f, 0.3f, 17, 0.25f, 8, 4);
                    for (int i = 0; i < 11; i++)
                    {
                        float a = i * 2.39f, r = 0.05f + (i % 4) * 0.05f;
                        var p = new Vector3(Mathf.Cos(a) * r, 0.03f + (i % 3) * 0.02f, Mathf.Sin(a) * r);
                        var dir = Quaternion.Euler(0, i * 47, 70 + (i % 3) * 8) * Vector3.up;
                        b.Sub = Main;
                        b.Tube(p, p + dir * 0.16f, 0.014f, 6);
                        b.Sub = Metal;
                        b.Tube(p - dir * 0.02f, p + dir * 0.005f, 0.028f, 6, true);
                    }
                    for (int i = 0; i < 5; i++) b.TorusRot(new Vector3(-0.1f + i * 0.06f, 0.02f, 0.14f - (i % 2) * 0.25f), new Vector3(90 + i * 10, i * 30, 0), 0.022f, 0.01f, 6, 4);
                    break;
                case "girder": // Doppel-T-Träger mit Laschen
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.08f, 4.5f));
                    b.Box(new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.08f, 4.5f));
                    b.Box(new Vector3(0, 0.25f, 0), new Vector3(0.06f, 0.35f, 4.5f));
                    for (int i = -2; i <= 2; i++) b.Box(new Vector3(0, 0.25f, i * 1f), new Vector3(0.46f, 0.32f, 0.03f));
                    b.Sub = Metal;
                    b.Box(new Vector3(0.05f, 0.25f, 2.2f), new Vector3(0.03f, 0.3f, 0.3f));
                    b.Box(new Vector3(-0.05f, 0.25f, -2.2f), new Vector3(0.03f, 0.3f, 0.3f));
                    b.Sub = Dark;
                    b.BoxJ(new Vector3(0.1f, 0.5f, 0.8f), new Vector3(0.3f, 0.01f, 0.6f), Vector3.zero, 0.04f, 1);
                    b.BoxJ(new Vector3(-0.1f, 0.5f, -1.4f), new Vector3(0.25f, 0.01f, 0.5f), Vector3.zero, 0.04f, 2);
                    break;
                case "gear":
                    b.Sub = Main;
                    b.Cylinder(Vector3.zero, 0.34f, 0.12f, 20);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * 360f;
                        b.BoxRot(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.37f, 0.06f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.37f), new Vector3(0.09f, 0.12f, 0.07f), new Vector3(0, -a, 0));
                    }
                    b.Sub = Metal;
                    b.Cylinder(new Vector3(0, 0, 0), 0.1f, 0.16f, 12);
                    b.Sub = Dark;
                    b.Cylinder(new Vector3(0, 0, 0), 0.05f, 0.165f, 8);
                    for (int i = 0; i < 4; i++) b.Cylinder(new Vector3(Mathf.Cos(i * 1.57f) * 0.21f, 0.0f, Mathf.Sin(i * 1.57f) * 0.21f), 0.06f, 0.125f, 8);
                    break;
                case "machine": // Maschinenteil mit Motor, Rohren und Schaltkasten
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.7f));
                    b.Sub = Metal;
                    b.CylinderX(new Vector3(0.1f, 0.72f, 0.05f), 0.2f, 0.55f, 12);
                    for (int i = 0; i < 5; i++) b.CylinderX(new Vector3(-0.1f + i * 0.1f, 0.72f, 0.05f), 0.23f, 0.02f, 12);
                    b.Box(new Vector3(0, 0.02f, 0), new Vector3(1.0f, 0.04f, 0.8f));
                    b.Sub = Dark;
                    b.Tube(new Vector3(-0.35f, 0.6f, -0.2f), new Vector3(-0.35f, 0.95f, -0.2f), 0.06f, 8);
                    b.Tube(new Vector3(-0.35f, 0.95f, -0.2f), new Vector3(-0.1f, 1.0f, -0.25f), 0.06f, 8);
                    for (int i = 0; i < 4; i++) b.Box(new Vector3(0.451f, 0.2f + i * 0.08f, 0), new Vector3(0.01f, 0.03f, 0.5f));
                    b.Sub = Light;
                    b.Box(new Vector3(-0.2f, 0.34f, 0.36f), new Vector3(0.3f, 0.26f, 0.03f));
                    b.Sub = Signal;
                    b.Cylinder(new Vector3(-0.25f, 0.38f, 0.37f), 0.035f, 0.03f, 8);
                    break;
                case "engine": // Motorblock mit Zylinderkopf, Riemenscheibe und Krümmer
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.8f, 1.5f));
                    b.BoxRot(new Vector3(-0.35f, 0.95f, 0), new Vector3(0.45f, 0.3f, 1.45f), new Vector3(0, 0, 25));
                    b.BoxRot(new Vector3(0.35f, 0.95f, 0), new Vector3(0.45f, 0.3f, 1.45f), new Vector3(0, 0, -25));
                    b.Sub = Metal;
                    b.BoxRot(new Vector3(-0.45f, 1.12f, 0), new Vector3(0.3f, 0.08f, 1.4f), new Vector3(0, 0, 25));
                    b.BoxRot(new Vector3(0.45f, 1.12f, 0), new Vector3(0.3f, 0.08f, 1.4f), new Vector3(0, 0, -25));
                    for (int i = 0; i < 4; i++) { b.Tube(new Vector3(0.62f, 0.8f, -0.5f + i * 0.33f), new Vector3(0.78f, 0.55f, -0.3f + i * 0.2f), 0.05f, 6); }
                    b.CylinderZ(new Vector3(0, 0.55f, 0.78f), 0.25f, 0.06f, 14);
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.08f, 0), new Vector3(0.8f, 0.16f, 1.3f));
                    b.CylinderZ(new Vector3(0, 0.55f, 0.82f), 0.12f, 0.06f, 10);
                    b.CylinderZ(new Vector3(0.25f, 0.95f, 0.8f), 0.1f, 0.06f, 10);
                    b.Sub = Signal;
                    for (int i = 0; i < 4; i++) b.Tube(new Vector3(-0.5f, 1.15f, -0.5f + i * 0.33f), new Vector3(-0.1f, 1.3f, -0.4f + i * 0.25f), 0.018f, 4);
                    break;
                case "pipe": // Rohrstück mit Flanschen
                    b.Sub = Main;
                    b.CylinderZ(new Vector3(0, 0.25f, 0), 0.24f, 1.8f, 12);
                    b.Sub = Metal;
                    b.CylinderZ(new Vector3(0, 0.25f, 0.88f), 0.32f, 0.07f, 12);
                    b.CylinderZ(new Vector3(0, 0.25f, -0.88f), 0.32f, 0.07f, 12);
                    for (int i = 0; i < 6; i++) b.CylinderZ(new Vector3(Mathf.Cos(i * 1.047f) * 0.28f, 0.25f + Mathf.Sin(i * 1.047f) * 0.28f, 0.93f), 0.025f, 0.05f, 6);
                    b.Sub = Dark;
                    b.CylinderZ(new Vector3(0, 0.25f, 0.925f), 0.2f, 0.01f, 12);
                    b.CylinderZ(new Vector3(0, 0.25f, -0.925f), 0.2f, 0.01f, 12);
                    b.BoxJ(new Vector3(0.1f, 0.48f, 0.2f), new Vector3(0.2f, 0.01f, 0.5f), Vector3.zero, 0.04f, 5);
                    break;
                case "canister": // Kanister mit Griffen und Prägung
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.33f, 0), new Vector3(0.45f, 0.66f, 0.25f));
                    b.BoxRot(new Vector3(0, 0.3f, 0.128f), new Vector3(0.36f, 0.05f, 0.02f), new Vector3(0, 0, 45));
                    b.BoxRot(new Vector3(0, 0.3f, 0.128f), new Vector3(0.36f, 0.05f, 0.02f), new Vector3(0, 0, -45));
                    b.BoxRot(new Vector3(0, 0.3f, -0.128f), new Vector3(0.36f, 0.05f, 0.02f), new Vector3(0, 0, 45));
                    b.BoxRot(new Vector3(0, 0.3f, -0.128f), new Vector3(0.36f, 0.05f, 0.02f), new Vector3(0, 0, -45));
                    b.Box(new Vector3(-0.12f, 0.74f, 0), new Vector3(0.04f, 0.14f, 0.05f));
                    b.Box(new Vector3(-0.02f, 0.74f, 0), new Vector3(0.04f, 0.14f, 0.05f));
                    b.Box(new Vector3(-0.07f, 0.8f, 0), new Vector3(0.24f, 0.04f, 0.06f));
                    b.Sub = Signal;
                    b.BoxRot(new Vector3(0.15f, 0.7f, 0), new Vector3(0.06f, 0.12f, 0.06f), new Vector3(0, 0, -30));
                    b.Cylinder(new Vector3(0.19f, 0.74f, 0), 0.045f, 0.05f, 8);
                    b.Sub = Dark;
                    b.Box(new Vector3(0.12f, 0.12f, 0.13f), new Vector3(0.14f, 0.1f, 0.005f));
                    break;
                case "ewaste": // alter Röhrenmonitor mit Tastatur
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.2f, -0.02f), new Vector3(0.42f, 0.34f, 0.3f));
                    b.Box(new Vector3(0, 0.2f, -0.2f), new Vector3(0.3f, 0.24f, 0.12f));
                    b.Sub = Dark;
                    b.Box(new Vector3(0, 0.21f, 0.132f), new Vector3(0.34f, 0.26f, 0.02f));
                    b.Tube(new Vector3(0, 0.1f, -0.26f), new Vector3(0.1f, 0.02f, -0.45f), 0.015f, 4);
                    b.Sub = Light;
                    b.BoxRot(new Vector3(0.12f, 0.03f, 0.35f), new Vector3(0.42f, 0.03f, 0.15f), new Vector3(0, -15, 5));
                    b.Sub = Dark;
                    for (int i = 0; i < 3; i++) b.BoxRot(new Vector3(0.12f + Mathf.Sin(-15 * Mathf.Deg2Rad) * (i - 1) * 0.04f, 0.047f, 0.35f + (i - 1) * 0.04f), new Vector3(0.36f, 0.006f, 0.025f), new Vector3(0, -15, 5));
                    b.Sub = Signal;
                    b.Box(new Vector3(0.15f, 0.07f, 0.142f), new Vector3(0.02f, 0.02f, 0.01f));
                    break;
                case "hullpart": // gebogene Rumpfplatte mit Bullauge und Nähten
                    {
                        b.Sub = Main;
                        const int segs = 6;
                        for (int i = 0; i < segs; i++)
                        {
                            float a0 = Mathf.Lerp(-40, 40, i / (float)segs), a1 = Mathf.Lerp(-40, 40, (i + 1) / (float)segs);
                            float am = (a0 + a1) * 0.5f * Mathf.Deg2Rad;
                            b.BoxRot(new Vector3(Mathf.Sin(am) * 1.3f, 1.5f - Mathf.Cos(am) * 1.3f + 0.2f, 0), new Vector3(1.3f * (a1 - a0) * Mathf.Deg2Rad + 0.02f, 0.1f, 2.1f), new Vector3(0, 0, am * Mathf.Rad2Deg));
                        }
                        b.Sub = Metal;
                        for (int i = 1; i < segs; i += 2)
                        {
                            float am = Mathf.Lerp(-40, 40, i / (float)segs) * Mathf.Deg2Rad;
                            b.BoxRot(new Vector3(Mathf.Sin(am) * 1.25f, 1.5f - Mathf.Cos(am) * 1.25f + 0.2f, 0), new Vector3(0.05f, 0.04f, 2.1f), new Vector3(0, 0, am * Mathf.Rad2Deg));
                        }
                        b.TorusRot(new Vector3(0.3f, 0.52f, 0.3f), new Vector3(0, 0, 13), 0.2f, 0.04f, 12, 4);
                        b.Sub = Dark;
                        b.Cylinder(new Vector3(0.3f, 0.5f, 0.3f), 0.17f, 0.03f, 12);
                        b.Sub = Light;
                        for (int i = 0; i < 6; i++) b.Crumple(new Vector3(-0.6f + i * 0.2f, 0.25f + Mathf.Abs(-0.6f + i * 0.2f) * 0.3f, -0.8f + (i % 3) * 0.2f), 0.06f, 0.6f, i, 0.3f, 5, 3);
                        break;
                    }
                case "buoy":
                    b.Sub = Main;
                    b.Sphere(new Vector3(0, 0.3f, 0), 0.45f, 12, 8);
                    b.Sub = Light;
                    b.Cylinder(new Vector3(0, 0.26f, 0), 0.46f, 0.12f, 12, false, 0.455f);
                    b.Sub = Metal;
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.094f;
                        b.Beam(new Vector3(Mathf.Cos(a) * 0.25f, 0.65f, Mathf.Sin(a) * 0.25f), new Vector3(0, 1.3f, 0), 0.03f);
                    }
                    b.Torus(new Vector3(0, 0.95f, 0), 0.13f, 0.02f, 10, 4);
                    b.TorusRot(new Vector3(0, -0.12f, 0), new Vector3(90, 0, 0), 0.07f, 0.02f, 8, 4);
                    b.Sub = Signal;
                    b.Cylinder(new Vector3(0, 1.3f, 0), 0.07f, 0.14f, 8);
                    break;
                case "oil":
                    b.Blob(Vector3.zero, 2.2f, 0.02f, 16, 2, 5, 0.35f);
                    break;
                case "anchor": // Anker mit Stock, Flunken und Kette
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.62f, 0), new Vector3(0.14f, 1.1f, 0.14f));
                    b.BoxRot(new Vector3(0.28f, 0.16f, 0), new Vector3(0.62f, 0.12f, 0.14f), new Vector3(0, 0, 32));
                    b.BoxRot(new Vector3(-0.28f, 0.16f, 0), new Vector3(0.62f, 0.12f, 0.14f), new Vector3(0, 0, -32));
                    b.BoxRot(new Vector3(0.55f, 0.36f, 0), new Vector3(0.2f, 0.28f, 0.1f), new Vector3(0, 0, 30));
                    b.BoxRot(new Vector3(-0.55f, 0.36f, 0), new Vector3(0.2f, 0.28f, 0.1f), new Vector3(0, 0, -30));
                    b.Sub = Metal;
                    b.CylinderZ(new Vector3(0, 1.05f, 0), 0.05f, 0.9f, 8);
                    b.TorusRot(new Vector3(0, 1.28f, 0), new Vector3(90, 0, 0), 0.13f, 0.035f, 10, 4);
                    for (int i = 0; i < 6; i++)
                        b.TorusRot(new Vector3(0.15f + i * 0.16f, 0.05f, 0.25f + i * 0.08f), new Vector3(i % 2 == 0 ? 90 : 0, 20, 0), 0.08f, 0.025f, 8, 4);
                    break;
                case "rack": // Serverschrank mit Einschüben, LEDs und Kabeln
                    b.Sub = Main;
                    b.Box(new Vector3(0, 1.0f, 0), new Vector3(0.9f, 2.0f, 0.9f));
                    b.Sub = Metal;
                    for (int i = 0; i < 7; i++) b.Box(new Vector3(0, 0.3f + i * 0.24f, 0.455f), new Vector3(0.78f, 0.16f, 0.02f));
                    b.Box(new Vector3(0, 1.99f, 0), new Vector3(0.92f, 0.04f, 0.92f));
                    b.Sub = Dark;
                    for (int i = 0; i < 7; i++) b.Box(new Vector3(-0.1f, 0.3f + i * 0.24f, 0.467f), new Vector3(0.4f, 0.06f, 0.01f));
                    b.Tube(new Vector3(0.2f, 0.4f, -0.46f), new Vector3(0.3f, 0.02f, -0.8f), 0.03f, 5);
                    b.Tube(new Vector3(-0.1f, 0.7f, -0.46f), new Vector3(-0.2f, 0.02f, -0.9f), 0.03f, 5);
                    b.Sub = Signal;
                    for (int i = 0; i < 7; i++) b.Box(new Vector3(0.3f, 0.3f + i * 0.24f, 0.468f), new Vector3(0.04f, 0.03f, 0.01f));
                    b.Sub = Light;
                    b.Box(new Vector3(0, 1.9f, 0.455f), new Vector3(0.4f, 0.06f, 0.01f));
                    break;
                case "core": // Seltenmetall-Kern: Kristall in Halterung
                    b.Sub = Main;
                    b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0.06f), new Vector2(0.2f, 0.3f), new Vector2(0f, 0.62f) }, 6);
                    b.Cylinder(new Vector3(0.14f, 0.18f, 0.06f), 0.07f, 0.25f, 5, true, 0f);
                    b.Sub = Metal;
                    b.TorusRot(new Vector3(0, 0.3f, 0), new Vector3(90, 0, 0), 0.26f, 0.025f, 12, 4);
                    b.Torus(new Vector3(0, 0.3f, 0), 0.24f, 0.02f, 12, 4);
                    b.Sub = Dark;
                    b.Cylinder(new Vector3(0, 0, 0), 0.22f, 0.07f, 10, true, 0.18f);
                    break;
                case "frozen": // eingefrorene Maschine mit Eiszapfen
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 1.2f));
                    b.Sub = Dark;
                    b.CylinderX(new Vector3(0, 1.1f, 0.4f), 0.25f, 1.0f, 10);
                    b.Cylinder(new Vector3(0.5f, 0.3f, 0.605f), 0.28f, 0.02f, 12);
                    for (int i = 0; i < 4; i++) b.Box(new Vector3(-0.801f, 0.35f + i * 0.12f, 0), new Vector3(0.01f, 0.05f, 0.9f));
                    b.Sub = Metal;
                    b.Tube(new Vector3(-0.6f, 1.2f, -0.3f), new Vector3(-0.6f, 1.6f, -0.3f), 0.08f, 8);
                    b.Box(new Vector3(0, 1.21f, 0), new Vector3(1.62f, 0.04f, 1.22f));
                    b.Sub = Light;
                    for (int i = 0; i < 7; i++) { var top = new Vector3(-0.7f + i * 0.23f, 1.2f - (i % 2) * 0.02f, 0.62f); b.Tube(top, top + Vector3.down * (0.18f + (i % 3) * 0.12f), 0.035f + (i % 3) * 0.01f, 5, true, 0f); }
                    b.Crumple(new Vector3(0.1f, 1.22f, 0), 0.6f, 0.2f, 4, 0.2f, 9, 3);
                    break;
                case "panel": // zerbrochenes Solarpanel
                    {
                        var old = b.M;
                        b.M = old * Matrix4x4.TRS(new Vector3(-0.2f, 0.22f, 0), Quaternion.Euler(20, 10, 0), Vector3.one);
                        b.Sub = Main;
                        b.Box(Vector3.zero, new Vector3(0.75f, 0.04f, 0.8f));
                        b.Sub = Metal;
                        for (int i = 0; i < 4; i++) b.Box(new Vector3(-0.28f + i * 0.19f, 0.022f, 0), new Vector3(0.012f, 0.012f, 0.78f));
                        for (int i = 0; i < 3; i++) b.Box(new Vector3(0, 0.022f, -0.2f + i * 0.2f), new Vector3(0.74f, 0.012f, 0.012f));
                        b.Box(new Vector3(0.385f, 0, 0), new Vector3(0.03f, 0.05f, 0.82f));
                        b.Box(new Vector3(-0.385f, 0, 0), new Vector3(0.03f, 0.05f, 0.82f));
                        b.Box(new Vector3(0, 0, 0.41f), new Vector3(0.8f, 0.05f, 0.03f));
                        b.M = old * Matrix4x4.TRS(new Vector3(0.45f, 0.06f, 0.1f), Quaternion.Euler(-4, 30, 8), Vector3.one);
                        b.Sub = Main;
                        b.BoxJ(Vector3.zero, new Vector3(0.45f, 0.04f, 0.6f), Vector3.zero, 0.03f, 6);
                        b.Sub = Metal;
                        b.Box(new Vector3(0.05f, 0.022f, 0), new Vector3(0.012f, 0.012f, 0.58f));
                        b.M = old;
                        b.Sub = Dark;
                        b.Beam(new Vector3(-0.2f, 0.0f, 0.3f), new Vector3(-0.2f, 0.18f, 0.25f), 0.05f);
                        b.BoxJ(new Vector3(0.1f, 0.03f, -0.45f), new Vector3(0.2f, 0.02f, 0.14f), new Vector3(0, 20, 0), 0.04f, 3);
                        break;
                    }
                case "shuttle": // abgestürztes Shuttle mit Tragflächen, Cockpit und Triebwerken
                    {
                        b.Sub = Main;
                        var old = b.M;
                        b.M = old * Matrix4x4.TRS(new Vector3(0, 1.6f, -4.3f), Quaternion.Euler(90, 0, 4), new Vector3(1f, 1f, 1.1f));
                        b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(1.35f, 0.05f), new Vector2(1.5f, 1.5f), new Vector2(1.5f, 5.5f), new Vector2(1.3f, 7.2f), new Vector2(0.8f, 8.3f), new Vector2(0.25f, 8.9f), new Vector2(0f, 9.0f) }, 14);
                        b.M = old;
                        b.BoxRot(new Vector3(2.4f, 0.75f, -1.5f), new Vector3(3.6f, 0.22f, 3.4f), new Vector3(0, -12, 6));
                        b.BoxRot(new Vector3(-2.3f, 0.35f, -1.3f), new Vector3(3.0f, 0.22f, 3.0f), new Vector3(8, 14, -14));
                        b.BoxRot(new Vector3(0, 3.1f, -3.4f), new Vector3(0.2f, 2.2f, 2.0f), new Vector3(-18, 0, 5));
                        b.Sub = Dark;
                        b.BoxRot(new Vector3(0, 2.25f, 3.1f), new Vector3(1.6f, 0.5f, 1.2f), new Vector3(-28, 0, 4));
                        b.Box(new Vector3(0, 0.25f, -0.2f), new Vector3(2.2f, 0.12f, 7.6f));
                        b.BoxJ(new Vector3(1.2f, 1.2f, 0.5f), new Vector3(0.02f, 1.2f, 1.6f), new Vector3(0, 0, 4), 0.2f, 7);
                        b.BoxJ(new Vector3(-0.9f, 2.6f, -1.0f), new Vector3(1.0f, 0.02f, 1.6f), new Vector3(0, 0, 4), 0.2f, 8);
                        b.Sub = Metal;
                        b.CylinderZ(new Vector3(0.75f, 1.2f, -4.9f), 0.5f, 1.4f, 12);
                        b.CylinderZ(new Vector3(-0.75f, 1.2f, -4.9f), 0.5f, 1.4f, 12);
                        b.Sub = Signal;
                        b.BoxRot(new Vector3(2.9f, 0.88f, -1.6f), new Vector3(1.4f, 0.24f, 0.4f), new Vector3(0, -12, 6));
                        b.Box(new Vector3(0, 1.5f, 1.0f), new Vector3(3.05f, 0.25f, 1.5f));
                        b.Sub = Dark;
                        b.CylinderZ(new Vector3(0.75f, 1.2f, -5.62f), 0.38f, 0.04f, 12);
                        b.CylinderZ(new Vector3(-0.75f, 1.2f, -5.62f), 0.38f, 0.04f, 12);
                        break;
                    }
                case "drone":
                    b.Sub = Main;
                    b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.14f, 0.4f));
                    b.Sphere(new Vector3(0, 0.2f, 0), 0.14f, 10, 5, 0.5f);
                    b.Sub = Dark;
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(i % 2 == 0 ? 0.35f : -0.35f, 0.15f, i < 2 ? 0.35f : -0.35f);
                        b.Beam(new Vector3(0, 0.13f, 0), p, 0.04f);
                        b.Cylinder(p + Vector3.down * 0.02f, 0.035f, 0.06f, 8);
                        b.Cylinder(p + Vector3.up * 0.04f, 0.15f, 0.01f, 12);
                    }
                    b.Sub = Metal;
                    for (int i = 0; i < 4; i++) b.Box(new Vector3(i % 2 == 0 ? 0.15f : -0.15f, 0.02f, i < 2 ? 0.12f : -0.12f), new Vector3(0.02f, 0.1f, 0.02f));
                    b.Sub = Signal;
                    b.Sphere(new Vector3(0, 0.08f, 0.2f), 0.04f, 8, 5);
                    break;
                case "netpiece":
                case "net":
                    Net(b, shape == "net" ? 1.2f : 0.45f, shape == "net" ? 0.55f : 0.16f, shape == "net" ? 9 : 6, shape == "net" ? 3 : 1);
                    break;
                default:
                    b.Box(new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f));
                    break;
            }
        }

        /// <summary>
        /// Geisternetz als Knäuel: ein dunkler Kern, darüber ein über den Haufen drapiertes Maschengitter aus Seilen,
        /// lose Seilenden und Schwimmer (Signalfarbe).
        /// </summary>
        static void Net(MeshBuilder b, float radius, float height, int grid, int floats)
        {
            b.Sub = Dark;
            b.Crumple(new Vector3(0, 0, 0), radius * 0.82f, height / radius * 0.95f, 23, 0.25f, 10, 5);
            b.Sub = Main;
            System.Func<float, float, Vector3> surf = (u, w) =>
            {
                float x = (u * 2 - 1) * radius, z = (w * 2 - 1) * radius;
                float d = Mathf.Sqrt(x * x + z * z) / radius;
                float h = Mathf.Max(0.02f, (1f - d * d)) * height;
                h += (MeshBuilder.Hash01((int)(u * 37), (int)(w * 37), 5) - 0.5f) * height * 0.25f;
                return new Vector3(x + Mathf.Sin(w * 9f) * radius * 0.05f, Mathf.Max(0.015f, h), z + Mathf.Sin(u * 7f) * radius * 0.05f);
            };
            float rope = Mathf.Max(0.012f, radius * 0.018f);
            for (int i = 0; i <= grid; i++)
            {
                float t = i / (float)grid;
                for (int k = 0; k < grid; k++)
                {
                    float a = k / (float)grid, c = (k + 1) / (float)grid;
                    var p0 = surf(t, a); var p1 = surf(t, c);
                    var q0 = surf(a, t); var q1 = surf(c, t);
                    if (new Vector2(p0.x, p0.z).magnitude < radius * 1.05f) b.Tube(p0, p1, rope, 4);
                    if (new Vector2(q0.x, q0.z).magnitude < radius * 1.05f) b.Tube(q0, q1, rope, 4);
                }
            }
            // lose Seilenden
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.4f;
                var s = new Vector3(Mathf.Cos(a) * radius * 0.8f, 0.03f, Mathf.Sin(a) * radius * 0.8f);
                var e = s + new Vector3(Mathf.Cos(a + 0.6f), 0, Mathf.Sin(a + 0.6f)) * radius * 0.6f;
                b.Tube(s, (s + e) * 0.5f + new Vector3(0.05f, 0.02f, -0.05f) * radius, rope * 1.4f, 4);
                b.Tube((s + e) * 0.5f + new Vector3(0.05f, 0.02f, -0.05f) * radius, e, rope * 1.4f, 4);
            }
            b.Sub = Signal;
            for (int i = 0; i < floats; i++)
            {
                float a = i * 2.3f + 1f;
                var p = new Vector3(Mathf.Cos(a) * radius * 0.55f, 0, Mathf.Sin(a) * radius * 0.55f);
                p.y = surf((p.x / radius + 1) * 0.5f, (p.z / radius + 1) * 0.5f).y + radius * 0.06f;
                b.Sphere(p, radius * 0.11f, 8, 5, 0.8f);
            }
        }

        /// <summary>Müllberg mit Pressballen, Reifen, Rohren und Blechen. Untermeshes: 0 Haufen, 1 bunte Teile, 2 Metall, 3 dunkle Teile.</summary>
        public static Mesh Mound(int seed)
        {
            return Get("mound_" + seed, b =>
            {
                b.Sub = 0;
                b.Blob(Vector3.zero, 1f, 1f, 16, 6, seed, 0.25f);
                for (int i = 0; i < 34; i++)
                {
                    float a = MeshBuilder.Hash01(i, 1, seed) * Mathf.PI * 2, r = 0.15f + MeshBuilder.Hash01(i, 2, seed) * 0.8f;
                    float h = Mathf.Sqrt(Mathf.Max(0, 1 - r * r)) * 0.95f;
                    var p = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                    float s = 0.07f + 0.1f * MeshBuilder.Hash01(i, 3, seed);
                    int kind = i % 5;
                    b.Sub = kind == 0 ? 2 : kind == 1 ? 3 : 1;
                    switch (kind)
                    {
                        case 0: b.Tube(p - new Vector3(s, -s * 0.3f, 0), p + new Vector3(s * 1.5f, s, s * 0.5f), s * 0.2f, 6); break;
                        case 1: b.TorusRot(p, new Vector3(70 + i * 7, i * 37, 0), s * 0.9f, s * 0.35f, 10, 4); break;
                        case 2: b.BoxJ(p, new Vector3(s * 1.2f, s * 0.9f, s), new Vector3(i * 23, i * 41, i * 7), s * 0.15f, i + seed); break;
                        case 3: b.BoxRot(p, new Vector3(s * 2.2f, s * 0.12f, s * 1.4f), new Vector3(i * 13, i * 29, i * 17)); break;
                        default: b.BoxRot(p, Vector3.one * s * 1.1f, new Vector3(i * 23, i * 41, i * 7)); break;
                    }
                }
            });
        }
    }
}
