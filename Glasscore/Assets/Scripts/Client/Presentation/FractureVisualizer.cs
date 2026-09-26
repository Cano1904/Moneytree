using System.Collections.Generic;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Client-side execution of RPC_ShatterTile. The server only sends (tileId, impactPoint, force);
    /// every client derives the same seed via DeterministicHash.FractureSeed and builds identical Voronoi
    /// shards locally — Low uses 4 pre-fractured patterns, Medium 20 live cells, High 112 cells.
    /// Tempered glass instead crumbles into GPU-instanced non-lethal cubes all at once.
    /// </summary>
    public sealed class FractureVisualizer : MonoBehaviour
    {
        public const int DebrisLayer = 1; // built-in "TransparentFX" layer: shards ignore each other

        private sealed class Shard
        {
            public GameObject Go;
            public Mesh Mesh;
            public Rigidbody Body;
            public BoxCollider Collider;
            public MeshRenderer Renderer;
            public float DieAt;
            public float SpawnedAt;
            public Vector3 BaseScale = Vector3.one;
        }

        private struct Cube
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 Spin;
            public float DieAt;
            public Color Color;
        }

        private readonly List<Shard> _active = new List<Shard>();
        private readonly Stack<Shard> _pool = new Stack<Shard>();
        private readonly List<Cube> _cubes = new List<Cube>();
        private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];
        private readonly Vector4[] _colors = new Vector4[1023];
        private List<FractureCell>[] _prebaked;
        private MaterialPropertyBlock _mpb;
        private MaterialPropertyBlock _cubeMpb;
        private GameSettings _settings;
        private Material _cubeMaterial;

        public void Init(GameSettings settings)
        {
            _settings = settings;
            _mpb = new MaterialPropertyBlock();
            _cubeMpb = new MaterialPropertyBlock();
            _cubeMaterial = Mats.Glass;
            Physics.IgnoreLayerCollision(DebrisLayer, DebrisLayer, true);

            // Low quality: four patterns fractured once at load ("pre-fractured chunks").
            _prebaked = new List<FractureCell>[4];
            for (int p = 0; p < 4; p++)
            {
                var sites = VoronoiFracture2D.GenerateSites((ulong)(p + 1) * 7919UL, Vec2.Zero, 1f, 1f, 12, 0.5f);
                _prebaked[p] = VoronoiFracture2D.ComputeCells(sites, 1f, 1f);
            }
        }

        public void Shatter(ArenaView arena, int tileId, Vec3 impactPoint, float force)
        {
            if (arena == null || arena.Layout == null || tileId < 0 || tileId >= arena.Layout.Count) return;
            TileLayout layout = arena.Layout;
            GlassType type = layout.Types[tileId];
            GlassSpec spec = GlassCatalog.Get(type);
            Vector3 center = layout.Centers[tileId].ToUnity();
            arena.HideTile(tileId);

            if (spec.BreakStyle == BreakStyle.CubeCrumble)
            {
                Crumble(center, Mats.GlassTint(type), force);
                AudioService.Instance?.PlayAt(AudioService.Crumble, center, 1f);
                return;
            }

            ulong seed = DeterministicHash.FractureSeed(arena.MatchSeed, tileId, impactPoint);
            Vec2 local = (impactPoint - layout.Centers[tileId]).XZ;
            FractureProfile profile = _settings.Fracture;

            List<FractureCell> cells;
            float rotation = 0f;
            if (profile.Prebaked)
            {
                cells = _prebaked[(int)(seed % 4UL)];
                rotation = (seed >> 8) % 4UL * 90f;
            }
            else
            {
                Vec2[] sites = VoronoiFracture2D.GenerateSites(seed, local, 1f, 1f, profile.SiteCount, force);
                cells = VoronoiFracture2D.ComputeCells(sites, 1f, 1f);
            }

            var rng = new DeterministicRandom(seed ^ 0x5DEECE66DUL);
            Quaternion rot = Quaternion.Euler(0f, rotation, 0f);
            Vector3 impactLocal = new Vector3(local.X, 0f, local.Y);
            Color tint = Mats.GlassTint(type);
            tint.a = Mathf.Max(0.3f, tint.a + 0.15f);

            foreach (FractureCell cell in cells)
            {
                Shard s = Rent(profile.MaxActiveFragments);
                Mats_FillShard(s, cell);
                Vector3 offset = rot * new Vector3(cell.Centroid.X, 0f, cell.Centroid.Y);
                s.Go.transform.position = center + offset;
                s.Go.transform.rotation = rot;
                s.BaseScale = Vector3.one;
                s.Go.transform.localScale = Vector3.one;

                Vector3 outward = offset - impactLocal;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.insideUnitSphere;
                float speed = (1.2f + 6f * force) * (0.5f + rng.NextFloat()) / (0.6f + (offset - impactLocal).magnitude);
                Vector3 v = outward * speed + Vector3.up * rng.Range(-0.5f, 2.5f) * force + Vector3.down * 1.5f;
                Phys.SetVelocity(s.Body, v);
                s.Body.angularVelocity = new Vector3(rng.Range(-8f, 8f), rng.Range(-8f, 8f), rng.Range(-8f, 8f)) * force;

                _mpb.Clear();
                _mpb.SetColor("_Color", tint);
                _mpb.SetColor("_EdgeColor", Mats.GlassEdge(type));
                _mpb.SetFloat("_Crack", 0.2f);
                s.Renderer.SetPropertyBlock(_mpb);

                s.SpawnedAt = Time.time;
                s.DieAt = Time.time + profile.FragmentLifetime * rng.Range(0.7f, 1.3f);
            }

            AudioService.Instance?.PlayAt(cells.Count > 16 ? AudioService.Shatter : AudioService.ShatterSmall, center, 0.7f + 0.5f * force);
        }

        private void Mats_FillShard(Shard s, FractureCell cell)
        {
            MeshData data = PrismMeshBuilder.Build(cell.Polygon, GlassCatalog.TileThickness, cell.Centroid, 1f);
            Meshes.Fill(s.Mesh, data);
            Bounds b = s.Mesh.bounds;
            s.Collider.center = b.center;
            s.Collider.size = new Vector3(Mathf.Max(0.02f, b.size.x), GlassCatalog.TileThickness, Mathf.Max(0.02f, b.size.z));
            s.Body.mass = Mathf.Max(0.05f, cell.Area * 2.5f * 0.1f * 10f);
        }

        private Shard Rent(int cap)
        {
            while (_active.Count >= cap)
            {
                Return(_active[0]);
                _active.RemoveAt(0);
            }

            Shard s;
            if (_pool.Count > 0) s = _pool.Pop();
            else
            {
                s = new Shard { Go = new GameObject("Shard"), Mesh = new Mesh { name = "ShardMesh" } };
                s.Go.layer = DebrisLayer;
                s.Go.transform.SetParent(transform, false);
                s.Go.AddComponent<MeshFilter>().sharedMesh = s.Mesh;
                s.Renderer = s.Go.AddComponent<MeshRenderer>();
                s.Renderer.sharedMaterial = Mats.Shard;
                s.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.Collider = s.Go.AddComponent<BoxCollider>();
                s.Body = s.Go.AddComponent<Rigidbody>();
                s.Body.interpolation = RigidbodyInterpolation.None;
                s.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            s.Go.SetActive(true);
            s.Body.isKinematic = false;
            Phys.SetVelocity(s.Body, Vector3.zero);
            s.Body.angularVelocity = Vector3.zero;
            s.Body.WakeUp();
            _active.Add(s);
            return s;
        }

        private void Return(Shard s)
        {
            s.Body.isKinematic = true;
            s.Go.SetActive(false);
            _pool.Push(s);
        }

        private void Crumble(Vector3 center, Color tint, float force)
        {
            int grid = _settings.Fracture.CrumbleGrid;
            float cell = GlassCatalog.TileSize / grid;
            float now = Time.time;
            for (int x = 0; x < grid; x++)
                for (int z = 0; z < grid; z++)
                {
                    if (_cubes.Count >= _matrices.Length * 3) _cubes.RemoveAt(0);
                    var p = center + new Vector3((x + 0.5f) * cell - 1f, 0f, (z + 0.5f) * cell - 1f);
                    var c = tint;
                    c.a = 0.55f;
                    _cubes.Add(new Cube
                    {
                        Position = p,
                        Velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 1.5f) * (0.3f + force), Random.Range(-1f, 1f)),
                        Rotation = Random.rotation,
                        Spin = Random.insideUnitSphere * 360f,
                        DieAt = now + Random.Range(1.6f, 2.6f),
                        Color = c,
                    });
                }
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Shard s = _active[i];
                float left = s.DieAt - now;
                if (left <= 0f || s.Go.transform.position.y < -PlayerRules.KillPlaneBelowLowestLayer)
                {
                    Return(s);
                    _active.RemoveAt(i);
                }
                else if (left < 0.4f)
                {
                    s.Go.transform.localScale = s.BaseScale * (left / 0.4f);
                }
            }

            UpdateCubes(Time.deltaTime, now);
        }

        private void UpdateCubes(float dt, float now)
        {
            if (_cubes.Count == 0) return;
            int batch = 0;
            float size = GlassCatalog.TileSize / Mathf.Max(1, _settings.Fracture.CrumbleGrid) * 0.8f;
            for (int i = _cubes.Count - 1; i >= 0; i--)
            {
                Cube c = _cubes[i];
                if (now >= c.DieAt) { _cubes.RemoveAt(i); continue; }
                c.Velocity.y -= PlayerRules.Gravity * 0.6f * dt;
                c.Position += c.Velocity * dt;
                c.Rotation = Quaternion.Euler(c.Spin * dt) * c.Rotation;
                _cubes[i] = c;

                float fade = Mathf.Clamp01((c.DieAt - now) / 0.5f);
                _matrices[batch] = Matrix4x4.TRS(c.Position, c.Rotation, Vector3.one * size);
                Color col = c.Color;
                col.a *= fade;
                _colors[batch] = col;
                if (++batch == _matrices.Length)
                {
                    Flush(batch);
                    batch = 0;
                }
            }
            if (batch > 0) Flush(batch);
        }

        private void Flush(int count)
        {
            _cubeMpb.Clear();
            _cubeMpb.SetVectorArray("_Color", _colors);
            Graphics.DrawMeshInstanced(Meshes.Cube, 0, _cubeMaterial, _matrices, count, _cubeMpb, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        public void ClearAll()
        {
            foreach (var s in _active) Return(s);
            _active.Clear();
            _cubes.Clear();
        }
    }
}

namespace Glasscore.Client
{
    /// <summary>Rigidbody.velocity was renamed linearVelocity in Unity 6.</summary>
    public static class Phys
    {
        public static void SetVelocity(Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }
    }
}
