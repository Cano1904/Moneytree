using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Bodenbewuchs: Gräser, Blumen und fremdartige Pflanzen je Planet. Wächst sichtbar mit der Ökologie des Bereichs
    /// (Begrünung als eigene Aktion – Reinigen allein lässt keine Wiesen entstehen). Zeichnung per GPU-Instancing.
    /// </summary>
    public class FloraRenderer : MonoBehaviour
    {
        class Plant { public Vector3 Pos; public float Rot, Scale, Threshold; public int Kind, Area; }
        class Kind { public Mesh Mesh; public Material Mat; public readonly List<Matrix4x4> M = new List<Matrix4x4>(); }

        string planet;
        readonly List<Plant> plants = new List<Plant>();
        readonly List<Kind> kinds = new List<Kind>();
        readonly List<List<Matrix4x4>> pool = new List<List<Matrix4x4>>();
        float timer;
        readonly float[] grow = new float[3];

        void Build(string p)
        {
            planet = p;
            plants.Clear(); kinds.Clear();
            var layout = WorldGen.Get(p);
            var def = GameData.Planets[p];
            // Pflanzenformen je Planet (Farben orientiert an lebendigen Fremdwelten)
            Color[] cols;
            switch (p)
            {
                case "pyra": cols = new[] { new Color(0.95f, 0.65f, 0.2f), new Color(0.85f, 0.3f, 0.2f), new Color(0.55f, 0.7f, 0.3f), new Color(1f, 0.85f, 0.35f) }; break;
                case "pelagia": cols = new[] { new Color(1f, 0.5f, 0.7f), new Color(0.3f, 0.8f, 0.7f), new Color(0.55f, 0.75f, 0.35f), new Color(0.95f, 0.9f, 0.6f) }; break;
                case "nivalis": cols = new[] { new Color(0.3f, 0.9f, 0.75f), new Color(0.55f, 0.6f, 1f), new Color(0.8f, 0.95f, 1f), new Color(0.4f, 1f, 0.6f) }; break;
                default: cols = new[] { new Color(0.45f, 0.72f, 0.3f), new Color(0.85f, 0.25f, 0.25f), new Color(0.95f, 0.85f, 0.3f), new Color(0.6f, 0.8f, 0.35f) }; break;
            }
            kinds.Add(new Kind { Mesh = MeshKit.Get("flora_tuft", b => { for (int i = 0; i < 5; i++) b.BoxRot(new Vector3(0, 0.25f, 0), new Vector3(0.04f, 0.5f, 0.25f), new Vector3(15 * (i % 2 == 0 ? 1 : -1), i * 36, 0)); }), Mat = Mats.Get(Mats.Opaque, cols[0]) });
            kinds.Add(new Kind { Mesh = MeshKit.Get("flora_flower", b => { b.Cylinder(Vector3.zero, 0.02f, 0.45f, 5); b.Sphere(new Vector3(0, 0.5f, 0), 0.12f, 6, 4); }), Mat = Mats.Get(p == "nivalis" ? Mats.Emissive : Mats.Opaque, cols[1], p == "nivalis" ? (Color?)(cols[1] * 0.8f) : null) });
            kinds.Add(new Kind { Mesh = MeshKit.Get("flora_bush", b => { b.Blob(Vector3.zero, 0.6f, 0.55f, 8, 3, 7, 0.3f); }), Mat = Mats.Get(Mats.Opaque, cols[2]) });
            kinds.Add(new Kind { Mesh = MeshKit.Get("flora_frond", b => { for (int i = 0; i < 6; i++) b.BoxRot(new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.03f, 0.18f), new Vector3(0, i * 60, 35)); }), Mat = Mats.Get(Mats.Opaque, cols[3]) });
            var rng = new Rng(def.Seed + 1234);
            // Bewuchs konzentriert sich um Pflanzstellen, Projektplätze und Lichtpunkte – dazu locker verstreut
            var anchors = new List<V3>();
            foreach (var e in layout.Eco) anchors.Add(e.Pos);
            foreach (var s in layout.ProjectSites) anchors.Add(s);
            foreach (var z in layout.Zones) anchors.Add(z.Center);
            int target = 2600;
            for (int i = 0; i < target * 3 && plants.Count < target; i++)
            {
                float x, z;
                if (i % 3 != 0)
                {
                    var a = anchors[rng.Range(0, anchors.Count)];
                    float ang = rng.Range(0, 6.283f), r = Mathf.Pow(rng.Next(), 0.6f) * 22f;
                    x = a.x + Mathf.Cos(ang) * r; z = a.z + Mathf.Sin(ang) * r;
                }
                else { x = rng.Range(-145f, 145f); z = rng.Range(-145f, 145f); }
                if (layout.Base.InBase(x, z) || layout.BlockedStatic(x, z, 0.6f)) continue;
                float h = Terrain.HeightAt(p, x, z);
                if (p == "pelagia" && h < 0.35f) continue;
                if (Mathf.Abs(x) < 8f) continue; // Straße frei lassen
                plants.Add(new Plant
                {
                    Pos = new Vector3(x, h, z), Rot = rng.Range(0, 360f), Scale = rng.Range(0.7f, 1.6f),
                    Threshold = rng.Next() * 0.95f, Kind = rng.Range(0, kinds.Count), Area = PlanetLayout.AreaOf(z)
                });
            }
        }

        void Update()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null || wv.World == null) return;
            if (planet != wv.Planet) Build(wv.Planet);
            var w = wv.World;
            var ps = w.Planet(planet);
            bool before = PhotoMode.Active && PhotoMode.ShowBefore;
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                timer = 0.5f;
                for (int a = 0; a < 3; a++)
                {
                    float eco = Rules.EcoFraction(w, ps, a);
                    bool project = ps.Projects[GameData.ProjectId(planet, a)].Done;
                    // Nach dem Projekt ein zaghafter Anfang, die volle Wiese erst mit der Ökologie
                    grow[a] = before ? 0f : Mathf.Clamp01(eco * 0.9f + (project ? 0.1f : 0f));
                }
                var cam = Camera.main;
                var cp = cam != null ? cam.transform.position : Vector3.zero;
                float far = 70f * (GameApp.I != null ? GameApp.I.Settings.ViewDistance : 1f);
                foreach (var k in kinds) k.M.Clear();
                float sway = Mathf.Sin(Time.time * 1.3f) * 4f;
                foreach (var p in plants)
                {
                    float g = grow[p.Area];
                    if (g <= p.Threshold) continue;
                    if ((p.Pos - cp).sqrMagnitude > far * far) continue;
                    float s = p.Scale * Mathf.Clamp01((g - p.Threshold) * 6f);
                    kinds[p.Kind].M.Add(Matrix4x4.TRS(p.Pos, Quaternion.Euler(sway * 0.5f, p.Rot, sway * 0.3f), Vector3.one * s));
                }
            }
            int idx = 0;
            foreach (var k in kinds)
                for (int i = 0; i < k.M.Count; i += 1023)
                {
                    if (idx >= pool.Count) pool.Add(new List<Matrix4x4>(1023));
                    var l = pool[idx++];
                    l.Clear();
                    int n = Mathf.Min(1023, k.M.Count - i);
                    for (int j = 0; j < n; j++) l.Add(k.M[i + j]);
                    if (SystemInfo.supportsInstancing) Graphics.DrawMeshInstanced(k.Mesh, 0, k.Mat, l, null, ShadowCastingMode.Off, true);
                }
        }
    }
}
