using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Gemeinsame Hilfen der „belebten Welt“ (Tiere, Spuren, Stadtleben, Wind, MIKOs Mimik): Wiederherstellungsgrad je
    /// Bereich, Qualitätsstufe, Bodenart, Kollisionstest und ein Instanz-Stapel für <see cref="Graphics.DrawMeshInstanced(Mesh,int,Material,Matrix4x4[],int,MaterialPropertyBlock,ShadowCastingMode,bool,int,Camera)"/>.
    /// Alles ist reine Darstellung: gelesen wird nur der replizierte Zustand, geschrieben wird nichts.
    /// </summary>
    public static class LifeCommon
    {
        /// <summary>
        /// Wiederherstellungsgrad eines Bereichs 0..1 – dieselbe Gewichtung wie der Planetenwert
        /// (Sauberkeit 40 %, Projekt 35 %, Ökologie 25 %). Fotomodus „Vorher“ = 0.
        /// </summary>
        public static float AreaRestoration(WorldState w, PlanetState ps, int area)
        {
            if (w == null || ps == null || BeforeView) return 0f;
            string pid = GameData.ProjectId(ps.Id, area);
            ProjectState pst;
            bool done = ps.Projects.TryGetValue(pid, out pst) && pst.Done;
            float r = Rules.Cleanliness(ps, area) * 0.4f + (done ? 0.35f : 0f) + Rules.EcoFraction(w, ps, area) * 0.25f;
            return Mathf.Clamp01(r);
        }

        /// <summary>Projekt des Bereichs fertig („Stadt erwacht“)?</summary>
        public static bool AreaAwake(PlanetState ps, int area)
        {
            if (ps == null || BeforeView) return false;
            ProjectState pst;
            return ps.Projects.TryGetValue(GameData.ProjectId(ps.Id, area), out pst) && pst.Done;
        }

        /// <summary>Fotomodus zeigt den Ausgangszustand.</summary>
        public static bool BeforeView { get { return PhotoMode.Active && PhotoMode.ShowBefore; } }

        /// <summary>Qualitätsstufe 0..3 aus den Einstellungen (ohne Spiel: Hoch).</summary>
        public static int Quality { get { return GameApp.I != null && GameApp.I.Settings != null ? Mathf.Clamp(GameApp.I.Settings.Quality, 0, 3) : 2; } }

        /// <summary>Mengenfaktor je Qualitätsstufe (Niedrig 0,35 … Ultra 1).</summary>
        public static float QualityScale { get { int q = Quality; return q <= 0 ? 0.35f : q == 1 ? 0.6f : q == 2 ? 0.85f : 1f; } }

        /// <summary>Sichtweiten-Faktor aus den Einstellungen (0,5 … 1,5).</summary>
        public static float ViewScale { get { return GameApp.I != null && GameApp.I.Settings != null ? Mathf.Clamp(GameApp.I.Settings.ViewDistance, 0.5f, 1.5f) : 1f; } }

        /// <summary>Schatten für kleine bewegte Objekte (erst ab Schattenstufe „Mittel“).</summary>
        public static bool SmallShadows { get { return GameApp.I == null || GameApp.I.Settings == null || GameApp.I.Settings.Shadows >= 2; } }

        public static float Hash01(int a, int b, int c) { return MeshBuilder.Hash01(a, b, c); }

        /// <summary>Liegt der Punkt auf einer Straße (Fahrbahn, optional mit Rand)?</summary>
        public static bool OnRoad(PlanetLayout l, float x, float z, float margin = 0f)
        {
            foreach (var r in l.Roads)
            {
                float hw = r[4] * 0.5f + margin;
                if (Mathf.Abs(r[0] - r[2]) < 0.01f)
                {
                    if (z >= Mathf.Min(r[1], r[3]) && z <= Mathf.Max(r[1], r[3]) && Mathf.Abs(x - r[0]) < hw) return true;
                }
                else if (x >= Mathf.Min(r[0], r[2]) && x <= Mathf.Max(r[0], r[2]) && Mathf.Abs(z - r[1]) < hw) return true;
            }
            return false;
        }

        /// <summary>Steckt der Punkt (mit Rand) in einer festen Box des Layouts (Gebäude, Mauern, Tore, Dünen)?</summary>
        public static bool Solid(PlanetLayout l, float x, float z, float margin, List<Box> tmp)
        {
            l.Query(x, z, margin + 1f, tmp);
            foreach (var b in tmp) if (b.Solid && b.Contains(x, z, margin)) return true;
            return false;
        }

        /// <summary>Oberkante der höchsten festen Box an der Stelle (NaN, wenn keine).</summary>
        public static float SolidTop(PlanetLayout l, float x, float z, List<Box> tmp)
        {
            l.Query(x, z, 1f, tmp);
            float top = float.NaN;
            foreach (var b in tmp) if (b.Solid && b.Contains(x, z, 0f)) top = float.IsNaN(top) ? b.Y0 + b.H : Mathf.Max(top, b.Y0 + b.H);
            return top;
        }

        /// <summary>Geländenormale aus Nachbarhöhen.</summary>
        public static Vector3 TerrainNormal(string planet, float x, float z, float d = 0.6f)
        {
            float hx = Terrain.HeightAt(planet, x + d, z) - Terrain.HeightAt(planet, x - d, z);
            float hz = Terrain.HeightAt(planet, x, z + d) - Terrain.HeightAt(planet, x, z - d);
            var n = new Vector3(-hx, 2f * d, -hz);
            float m = n.magnitude;
            return m > 1e-5f ? n / m : Vector3.up;
        }

        /// <summary>Innerhalb der begehbaren Welt (mit Abstand zum Rand)?</summary>
        public static bool InWorld(float x, float z, float margin = 3f)
        {
            return Mathf.Abs(x) < PlanetLayout.Half - margin && Mathf.Abs(z) < PlanetLayout.Half - margin;
        }

        /// <summary>Endliche Zahlen (kein NaN/Unendlich) in allen Komponenten?</summary>
        public static bool Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        /// <summary>Weicher Übergang 0..1 zwischen a und b.</summary>
        public static float Smooth(float a, float b, float v) { return M.Smooth(M.InvLerp(a, b, v)); }

        /// <summary>Winkel (Grad) mit kürzestem Weg annähern.</summary>
        public static float TurnTowards(float cur, float target, float maxDeg) { return Mathf.MoveTowardsAngle(cur, target, maxDeg); }

        /// <summary>Gier-Winkel (Grad) einer Richtung in der Ebene; bei Nullvektor der bisherige Wert.</summary>
        public static float YawOf(float dx, float dz, float fallback)
        {
            return dx * dx + dz * dz < 1e-8f ? fallback : Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Stapel gleichartiger Instanzen (ein Mesh, ein Material je Untermesh) mit vorab angelegten Puffern in
    /// 1023er-Blöcken – keine Allokation pro Bild. Ohne GPU-Instancing wird jede Instanz einzeln gezeichnet.
    /// </summary>
    public sealed class InstanceBatch
    {
        public readonly Mesh Mesh;
        public readonly Material[] Mats;
        public readonly string Name;
        readonly List<Matrix4x4[]> buf = new List<Matrix4x4[]>();
        public int Count { get; private set; }
        /// <summary>Summe aller gezeichneten Instanzen (Statistik).</summary>
        public static long TotalDrawn;
        public static long TotalCalls;

        public InstanceBatch(string name, Mesh mesh, Material[] mats)
        {
            Name = name; Mesh = mesh; Mats = mats;
        }

        public void Clear() { Count = 0; }

        public void Add(Matrix4x4 m)
        {
            int i = Count++;
            int c = i / 1023;
            while (buf.Count <= c) buf.Add(new Matrix4x4[1023]);
            buf[c][i % 1023] = m;
        }

        public Matrix4x4 Get(int i) { return buf[i / 1023][i % 1023]; }

        public void Draw(bool shadows, int maxShadowSub = 99)
        {
            if (Count == 0 || Mesh == null || Mats == null || Mats.Length == 0) return;
            int subs = Mesh.subMeshCount;
            bool inst = SystemInfo.supportsInstancing;
            for (int c = 0; c * 1023 < Count; c++)
            {
                int cnt = Mathf.Min(1023, Count - c * 1023);
                for (int s = 0; s < subs; s++)
                {
                    var mat = Mats[Mathf.Min(s, Mats.Length - 1)];
                    if (mat == null) continue;
                    var sh = shadows && s <= maxShadowSub ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    if (inst) Graphics.DrawMeshInstanced(Mesh, s, mat, buf[c], cnt, null, sh, true, 0, null);
                    else for (int k = 0; k < cnt; k++) Graphics.DrawMesh(Mesh, buf[c][k], mat, 0, null, s, null, sh, true);
                    TotalCalls++;
                }
                TotalDrawn += cnt;
            }
        }
    }
}
