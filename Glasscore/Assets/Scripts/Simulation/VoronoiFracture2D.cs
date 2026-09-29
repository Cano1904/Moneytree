using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    public sealed class FractureCell
    {
        public Vec2 Site;
        /// <summary>Convex polygon, counter-clockwise, in tile-local XZ coordinates (metres).</summary>
        public Vec2[] Polygon;
        public Vec2 Centroid;
        public float Area;
    }

    /// <summary>
    /// Voronoi fracture of a thin tile. Tiles are 2m x 2m x 0.1m, so the fracture is solved in 2D
    /// (tile plane) and each cell is extruded to the tile thickness by <see cref="PrismMeshBuilder"/>.
    /// Cells are built by clipping the tile rectangle with the perpendicular-bisector half-planes of
    /// every other site (O(n^2) Sutherland–Hodgman), which is exact, allocation-light and fast enough
    /// for 128 sites on a worker thread (&lt; 1 ms on a desktop CPU).
    /// </summary>
    public static class VoronoiFracture2D
    {
        /// <summary>Fraction of sites clustered around the impact point (small shards near the hit).</summary>
        public const float ImpactClusterFraction = 0.6f;
        private const float MinSiteSeparation = 0.01f;

        /// <summary>
        /// Deterministically generates fracture sites. Clustered sites use a radial distribution whose
        /// radius shrinks as force grows, so harder hits produce finer shards around the impact.
        /// </summary>
        /// <param name="force01">Normalized impact force (0 = tap, 1 = boulder).</param>
        public static Vec2[] GenerateSites(ulong seed, Vec2 impactLocal, float halfWidth, float halfDepth, int count, float force01)
        {
            if (count < 1) count = 1;
            var rng = new DeterministicRandom(seed);
            var sites = new List<Vec2>(count);
            force01 = GcMath.Clamp01(force01);
            impactLocal = new Vec2(
                GcMath.Clamp(impactLocal.X, -halfWidth, halfWidth),
                GcMath.Clamp(impactLocal.Y, -halfDepth, halfDepth));

            int clustered = (int)Math.Round(count * ImpactClusterFraction);
            float clusterRadius = GcMath.Lerp(1.2f, 0.45f, force01) * Math.Min(halfWidth, halfDepth);

            int guard = count * 20;
            while (sites.Count < count && guard-- > 0)
            {
                Vec2 p;
                if (sites.Count < clustered)
                {
                    float angle = rng.NextFloat() * 6.2831853f;
                    // Squaring the radius sample concentrates sites near the impact point.
                    float r = rng.NextFloat();
                    r = r * r * clusterRadius;
                    p = impactLocal + new Vec2((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r);
                }
                else
                {
                    p = new Vec2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfDepth, halfDepth));
                }

                if (p.X <= -halfWidth || p.X >= halfWidth || p.Y <= -halfDepth || p.Y >= halfDepth) continue;
                if (IsTooClose(sites, p)) continue;
                sites.Add(p);
            }

            return sites.ToArray();
        }

        private static bool IsTooClose(List<Vec2> sites, Vec2 p)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                if ((sites[i] - p).LengthSquared < MinSiteSeparation * MinSiteSeparation) return true;
            }
            return false;
        }

        public static List<FractureCell> ComputeCells(Vec2[] sites, float halfWidth, float halfDepth)
        {
            var cells = new List<FractureCell>(sites.Length);
            var poly = new List<Vec2>(16);
            var scratch = new List<Vec2>(16);

            for (int i = 0; i < sites.Length; i++)
            {
                poly.Clear();
                poly.Add(new Vec2(-halfWidth, -halfDepth));
                poly.Add(new Vec2(halfWidth, -halfDepth));
                poly.Add(new Vec2(halfWidth, halfDepth));
                poly.Add(new Vec2(-halfWidth, halfDepth));

                Vec2 s = sites[i];
                for (int j = 0; j < sites.Length && poly.Count > 0; j++)
                {
                    if (i == j) continue;
                    Vec2 o = sites[j];
                    Vec2 mid = (s + o) * 0.5f;
                    Vec2 normal = o - s;
                    ClipHalfPlane(poly, scratch, mid, normal);
                    var tmp = poly; poly = scratch; scratch = tmp;
                }

                if (poly.Count < 3) continue;
                var polygon = poly.ToArray();
                float area = PolygonArea(polygon, out Vec2 centroid);
                if (area <= 1e-7f) continue;
                cells.Add(new FractureCell { Site = s, Polygon = polygon, Area = area, Centroid = centroid });
            }

            return cells;
        }

        /// <summary>Keeps the part of <paramref name="input"/> where dot(p - point, normal) &lt;= 0.</summary>
        private static void ClipHalfPlane(List<Vec2> input, List<Vec2> output, Vec2 point, Vec2 normal)
        {
            output.Clear();
            int n = input.Count;
            for (int k = 0; k < n; k++)
            {
                Vec2 a = input[k];
                Vec2 b = input[(k + 1) % n];
                float da = Vec2.Dot(a - point, normal);
                float db = Vec2.Dot(b - point, normal);
                bool aIn = da <= 0f;
                bool bIn = db <= 0f;

                if (aIn) output.Add(a);
                if (aIn != bIn)
                {
                    float t = da / (da - db);
                    output.Add(a + (b - a) * t);
                }
            }
        }

        /// <summary>Signed-area shoelace formula; returns absolute area and the polygon centroid.</summary>
        public static float PolygonArea(Vec2[] polygon, out Vec2 centroid)
        {
            double a = 0, cx = 0, cy = 0;
            for (int k = 0; k < polygon.Length; k++)
            {
                Vec2 p = polygon[k];
                Vec2 q = polygon[(k + 1) % polygon.Length];
                double cross = (double)p.X * q.Y - (double)q.X * p.Y;
                a += cross;
                cx += (p.X + q.X) * cross;
                cy += (p.Y + q.Y) * cross;
            }
            a *= 0.5;
            if (Math.Abs(a) < 1e-12)
            {
                centroid = polygon.Length > 0 ? polygon[0] : Vec2.Zero;
                return 0f;
            }
            centroid = new Vec2((float)(cx / (6 * a)), (float)(cy / (6 * a)));
            return (float)Math.Abs(a);
        }

        public static bool ContainsPoint(Vec2[] convexPolygon, Vec2 p, float epsilon = 1e-4f)
        {
            int n = convexPolygon.Length;
            float sign = 0f;
            for (int k = 0; k < n; k++)
            {
                Vec2 a = convexPolygon[k];
                Vec2 b = convexPolygon[(k + 1) % n];
                float c = Vec2.Cross(b - a, p - a);
                if (Math.Abs(c) <= epsilon) continue;
                if (sign == 0f) sign = Math.Sign(c);
                else if (Math.Sign(c) != sign) return false;
            }
            return true;
        }
    }

    /// <summary>Engine-agnostic mesh buffers; the Unity layer copies them into a UnityEngine.Mesh.</summary>
    public sealed class MeshData
    {
        public Vec3[] Vertices;
        public Vec3[] Normals;
        public Vec2[] Uvs;
        public int[] Triangles;
    }

    /// <summary>Extrudes a convex fracture cell into a closed prism (top cap, bottom cap, side walls).</summary>
    public static class PrismMeshBuilder
    {
        /// <param name="polygon">Convex CCW polygon in tile-local XZ.</param>
        /// <param name="pivot">Vertices are emitted relative to this point (use the cell centroid).</param>
        /// <param name="tileHalfExtent">Used to map planar UVs across the whole tile so shards line up.</param>
        public static MeshData Build(Vec2[] polygon, float thickness, Vec2 pivot, float tileHalfExtent)
        {
            int n = polygon.Length;
            float h = thickness * 0.5f;
            // top n + bottom n + sides 4 per edge
            var verts = new Vec3[n * 2 + n * 4];
            var normals = new Vec3[verts.Length];
            var uvs = new Vec2[verts.Length];
            var tris = new List<int>((n - 2) * 6 + n * 6);

            for (int k = 0; k < n; k++)
            {
                Vec2 p = polygon[k] - pivot;
                Vec2 uv = new Vec2((polygon[k].X / tileHalfExtent + 1f) * 0.5f, (polygon[k].Y / tileHalfExtent + 1f) * 0.5f);
                verts[k] = new Vec3(p.X, h, p.Y);
                normals[k] = Vec3.Up;
                uvs[k] = uv;
                verts[n + k] = new Vec3(p.X, -h, p.Y);
                normals[n + k] = -Vec3.Up;
                uvs[n + k] = uv;
            }

            // Caps: polygon is CCW in XZ when viewed from +Y, which Unity treats as clockwise-front for
            // the top face when winding (0, k+1, k). Bottom uses the reverse.
            for (int k = 1; k < n - 1; k++)
            {
                tris.Add(0); tris.Add(k + 1); tris.Add(k);
                tris.Add(n); tris.Add(n + k); tris.Add(n + k + 1);
            }

            int baseIdx = n * 2;
            for (int k = 0; k < n; k++)
            {
                Vec2 a = polygon[k] - pivot;
                Vec2 b = polygon[(k + 1) % n] - pivot;
                Vec2 edge = b - a;
                var outward = new Vec3(edge.Y, 0f, -edge.X).Normalized;
                int v = baseIdx + k * 4;
                verts[v] = new Vec3(a.X, h, a.Y);
                verts[v + 1] = new Vec3(b.X, h, b.Y);
                verts[v + 2] = new Vec3(b.X, -h, b.Y);
                verts[v + 3] = new Vec3(a.X, -h, a.Y);
                float len = edge.Length;
                uvs[v] = new Vec2(0f, 1f);
                uvs[v + 1] = new Vec2(len, 1f);
                uvs[v + 2] = new Vec2(len, 0f);
                uvs[v + 3] = new Vec2(0f, 0f);
                for (int q = 0; q < 4; q++) normals[v + q] = outward;
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }

            return new MeshData { Vertices = verts, Normals = normals, Uvs = uvs, Triangles = tris.ToArray() };
        }
    }
}
