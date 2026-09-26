using System.Collections.Generic;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>Runtime-created materials (shaders live in Resources/Shaders so builds include them).</summary>
    public static class Mats
    {
        private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();
        private static Material _glass, _shard;

        private static Shader Get(string name)
        {
            if (Shaders.TryGetValue(name, out var s) && s != null) return s;
            s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError($"[GLASSCORE] Shader '{name}' missing from build; falling back.");
                s = Shader.Find("Unlit/Color") ?? Shader.Find("Hidden/InternalErrorShader");
            }
            Shaders[name] = s;
            return s;
        }

        public static Material Glass
        {
            get
            {
                if (_glass == null) _glass = new Material(Get("Glasscore/Glass")) { enableInstancing = true, name = "Glass" };
                return _glass;
            }
        }

        public static Material Shard
        {
            get
            {
                if (_shard == null)
                {
                    _shard = new Material(Get("Glasscore/Glass")) { enableInstancing = true, name = "Shard" };
                    _shard.SetColor("_Color", new Color(0.7f, 0.95f, 1f, 0.35f));
                }
                return _shard;
            }
        }

        public static Material Lit(Color albedo, Color emission)
        {
            var m = new Material(Get("Glasscore/Lit")) { enableInstancing = true };
            m.SetColor("_Color", albedo);
            m.SetColor("_Emission", emission);
            return m;
        }

        public static Material Neon(Color color, float intensity = 1.5f)
        {
            var m = new Material(Get("Glasscore/Neon")) { enableInstancing = true };
            m.SetColor("_Color", color);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static Material Sky() => new Material(Get("Glasscore/Sky"));
        public static Material Blur() => new Material(Get("Hidden/Glasscore/Blur"));

        public static Color GlassTint(GlassType type)
        {
            switch (type)
            {
                case GlassType.Standard: return new Color(0.55f, 0.85f, 1f, 0.16f);
                case GlassType.Tempered: return new Color(0.45f, 1f, 0.8f, 0.24f);
                case GlassType.Reinforced: return new Color(0.35f, 0.5f, 1f, 0.40f);
                default: return Color.clear;
            }
        }

        public static Color GlassEdge(GlassType type)
        {
            switch (type)
            {
                case GlassType.Standard: return new Color(0f, 1f, 1f);
                case GlassType.Tempered: return new Color(0.2f, 1f, 0.6f);
                case GlassType.Reinforced: return new Color(0.4f, 0.5f, 1f);
                default: return Color.white;
            }
        }
    }

    /// <summary>Primitive meshes harvested once from Unity's built-in primitives.</summary>
    public static class Meshes
    {
        private static Mesh _cube, _sphere, _capsule, _cylinder, _quad;

        private static Mesh Harvest(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            Mesh m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return m;
        }

        public static Mesh Cube => _cube != null ? _cube : (_cube = Harvest(PrimitiveType.Cube));
        public static Mesh Sphere => _sphere != null ? _sphere : (_sphere = Harvest(PrimitiveType.Sphere));
        public static Mesh Capsule => _capsule != null ? _capsule : (_capsule = Harvest(PrimitiveType.Capsule));
        public static Mesh Cylinder => _cylinder != null ? _cylinder : (_cylinder = Harvest(PrimitiveType.Cylinder));
        public static Mesh Quad => _quad != null ? _quad : (_quad = Harvest(PrimitiveType.Quad));

        public static Mesh Ring(float radius, float width, int segments = 48)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * (radius - width * 0.5f);
                verts[i * 2 + 1] = dir * (radius + width * 0.5f);
                int n = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "Ring", vertices = verts, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Copies engine-agnostic mesh buffers (Voronoi prisms) into a Unity mesh.</summary>
        public static void Fill(Mesh target, MeshData data)
        {
            target.Clear();
            int n = data.Vertices.Length;
            var v = new Vector3[n];
            var nn = new Vector3[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = data.Vertices[i].ToUnity();
                nn[i] = data.Normals[i].ToUnity();
                uv[i] = new Vector2(data.Uvs[i].X, data.Uvs[i].Y);
            }
            target.vertices = v;
            target.normals = nn;
            target.uv = uv;
            target.triangles = data.Triangles;
            target.RecalculateBounds();
        }
    }

    public static class VecExt
    {
        public static Vector3 ToUnity(this Vec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Vec3 ToSim(this Vector3 v) => new Vec3(v.x, v.y, v.z);
    }
}
