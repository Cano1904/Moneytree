using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Bodenbewuchs: Gräser, Blumen und fremdartige Pflanzen je Planet (leuchtende Sporenkapseln, Riesenpilze,
    /// Anemonen, Eiskristalle …). Wächst sichtbar mit der Ökologie des Bereichs (Begrünung als eigene Aktion –
    /// Reinigen allein lässt keine Wiesen entstehen); zäher Pionier-Bewuchs ist von Anfang an spärlich da.
    /// Zeichnung per GPU-Instancing mit vorab angelegten Puffern (keine Allokation pro Bild).
    /// Wind: Jede Pflanze wiegt sich mit Windstärke und -richtung (Scherung der Instanzmatrix auf der CPU – kein
    /// eigener Shader nötig, funktioniert mit jedem Material); Böen laufen als Welle über die Wiesen.
    /// </summary>
    public class FloraRenderer : MonoBehaviour
    {
        class Plant { public Vector3 Pos; public float Rot, Scale, Threshold, Q; public int Kind, Area; }
        class Kind
        {
            public Mesh Mesh; public Material[] Mats; public bool Big, Pioneer; public float Weight, SMin, SMax;
            /// <summary>Nur für die Hügel-Ausstattung (Büschel, Steine auf Wiesenhängen); Still = wiegt nicht im Wind.</summary>
            public bool Hill, Still;
            public readonly List<Matrix4x4[]> Buf = new List<Matrix4x4[]>();
            /// <summary>Ruhelage (ohne Wind) und Wiegen je Instanz: cos/sin der Phase, Ausschlag.</summary>
            public readonly List<Matrix4x4[]> Base = new List<Matrix4x4[]>();
            public readonly List<Vector3[]> Wave = new List<Vector3[]>();
            public int Count;
        }

        string planet;
        readonly List<Plant> plants = new List<Plant>();
        readonly List<Kind> kinds = new List<Kind>();
        float timer;
        readonly float[] grow = new float[3];
        readonly List<Box> tmp = new List<Box>();

        // ------------------------------------------------------------------ Materialien und Formen
        static Material O(Color c, float gloss = -1f) { return Mats.Get(Mats.Opaque, c, null, gloss); }
        static Material G(Color c, float k) { return Mats.Get(Mats.Emissive, c, c * k); }

        Kind Add(string mesh, System.Action<MeshBuilder> build, Material[] mats, float weight, float sMin, float sMax, bool big = false, bool pioneer = false)
        {
            var k = new Kind { Mesh = MeshKit.Get("flora_" + mesh, build), Mats = mats, Weight = weight, SMin = sMin, SMax = sMax, Big = big, Pioneer = pioneer };
            kinds.Add(k);
            return k;
        }

        /// <summary>Doppelseitiges Blatt (zwei Dreiecke) von der Basis zur Spitze.</summary>
        static void Blade(MeshBuilder b, Vector3 basePos, Vector3 tip, float width)
        {
            var d = tip - basePos;
            var side = Vector3.Cross(d, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            var n = Vector3.Cross(side, d).normalized;
            var a = basePos - side * width * 0.5f; var c = basePos + side * width * 0.5f;
            b.TriFace(a, c, tip, n);
            b.TriFace(a, c, tip, -n);
        }

        /// <summary>Gebogenes, doppelseitiges Wedelblatt aus zwei Vierecken.</summary>
        static void Frond(MeshBuilder b, Vector3 basePos, float yaw, float len, float width, float lift, float droop)
        {
            var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var side = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var p0 = basePos; var p1 = basePos + dir * len * 0.5f + Vector3.up * lift; var p2 = basePos + dir * len + Vector3.up * (lift - droop);
            float w0 = width * 0.3f, w1 = width, w2 = width * 0.15f;
            b.Face(p0 - side * w0, p1 - side * w1, p1 + side * w1, p0 + side * w0, Vector3.up);
            b.Face(p0 - side * w0, p0 + side * w0, p1 + side * w1, p1 - side * w1, Vector3.down);
            b.Face(p1 - side * w1, p2 - side * w2, p2 + side * w2, p1 + side * w1, Vector3.up);
            b.Face(p1 - side * w1, p1 + side * w1, p2 + side * w2, p2 - side * w2, Vector3.down);
        }

        static void GrassTuft(MeshBuilder b, int blades, float h, int seed)
        {
            for (int i = 0; i < blades; i++)
            {
                float a = MeshBuilder.Hash01(i, 1, seed) * 6.283f, r = MeshBuilder.Hash01(i, 2, seed) * 0.12f;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                float hh = h * (0.6f + MeshBuilder.Hash01(i, 3, seed) * 0.6f);
                var tip = p + new Vector3(Mathf.Cos(a) * hh * 0.35f, hh, Mathf.Sin(a) * hh * 0.35f);
                Blade(b, p, tip, 0.06f + h * 0.04f);
            }
        }

        static void Mushroom(MeshBuilder b, float h, float capR, int seed)
        {
            b.Sub = 1;
            b.Cylinder(Vector3.zero, capR * 0.22f, h, 8, false, capR * 0.14f);
            b.Torus(new Vector3(0, h * 0.62f, 0), capR * 0.2f, capR * 0.04f, 8, 3);
            b.Sub = 0;
            b.Lathe(new Vector3(0, h * 0.92f, 0), new[] { new Vector2(0, 0), new Vector2(capR, 0.02f), new Vector2(capR * 1.02f, capR * 0.12f), new Vector2(capR * 0.8f, capR * 0.45f), new Vector2(capR * 0.4f, capR * 0.65f), new Vector2(0, capR * 0.7f) }, 12);
            b.Sub = 2;
            b.Disc(new Vector3(0, h * 0.92f + 0.01f, 0), capR * 0.95f, 12, false);
            for (int i = 0; i < 5; i++)
            {
                float a = MeshBuilder.Hash01(i, 5, seed) * 6.283f, r = 0.3f + MeshBuilder.Hash01(i, 6, seed) * 0.5f;
                float y = h * 0.92f + capR * 0.7f * (1f - r * r) * 0.95f;
                b.Sphere(new Vector3(Mathf.Cos(a) * r * capR * 0.9f, y, Mathf.Sin(a) * r * capR * 0.9f), capR * 0.09f, 5, 3, 0.5f);
            }
            b.Sub = 0;
        }

        void Build(string p)
        {
            planet = p;
            plants.Clear(); kinds.Clear();
            var layout = WorldGen.Get(p);
            var def = GameData.Planets[p];
            switch (p)
            {
                case "pyra":
                    Add("drygrass_p", b => GrassTuft(b, 6, 0.4f, 11), new[] { O(new Color(0.62f, 0.44f, 0.25f)) }, 3f, 0.7f, 1.3f, false, true);
                    Add("deadbush", b => { for (int i = 0; i < 6; i++) b.Tube(Vector3.zero, new Vector3(Mathf.Cos(i) * 0.4f, 0.5f + (i % 3) * 0.15f, Mathf.Sin(i) * 0.4f), 0.02f, 3, false, 0.005f); }, new[] { O(new Color(0.4f, 0.3f, 0.22f)) }, 1f, 0.7f, 1.4f, false, true);
                    Add("goldgrass", b => GrassTuft(b, 8, 0.55f, 12), new[] { O(new Color(0.9f, 0.7f, 0.3f)) }, 4f, 0.8f, 1.5f);
                    Add("succulent", b => { for (int i = 0; i < 9; i++) b.BoxRot(new Vector3(Mathf.Cos(i * 0.7f) * 0.15f, 0.15f, Mathf.Sin(i * 0.7f) * 0.15f), new Vector3(0.1f, 0.4f, 0.05f), new Vector3(-30 - (i % 3) * 12, -i * 40, 0)); b.Sub = 2; b.Sphere(new Vector3(0, 0.3f, 0), 0.07f, 6, 4); b.Sub = 0; },
                        new[] { O(new Color(0.35f, 0.62f, 0.5f)), O(Color.gray), G(new Color(1f, 0.4f, 0.6f), 1.5f) }, 2f, 0.8f, 1.6f);
                    Add("sporepod", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0.05f, 0.7f, 0), 0.03f, 4); b.Tube(new Vector3(0.1f, 0, 0.05f), new Vector3(0.2f, 0.45f, 0.08f), 0.025f, 4); b.Sub = 2; b.Sphere(new Vector3(0.05f, 0.78f, 0), 0.1f, 7, 5, 1.3f); b.Sphere(new Vector3(0.2f, 0.5f, 0.08f), 0.07f, 6, 4, 1.3f); b.Sub = 0; b.Crumple(Vector3.zero, 0.15f, 0.4f, 3, 0.2f, 6, 3); },
                        new[] { O(new Color(0.5f, 0.35f, 0.25f)), O(new Color(0.55f, 0.4f, 0.3f)), G(new Color(1f, 0.55f, 0.15f), 2.2f) }, 2f, 0.8f, 1.5f);
                    Add("fanplant", b => { for (int i = 0; i < 7; i++) Blade(b, new Vector3((i - 3) * 0.04f, 0, 0), new Vector3((i - 3) * 0.22f, 0.9f - Mathf.Abs(i - 3) * 0.1f, 0.05f), 0.12f); }, new[] { O(new Color(0.85f, 0.3f, 0.22f)) }, 1.5f, 0.7f, 1.4f);
                    Add("flower_y", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0, 0.45f, 0), 0.015f, 4); b.Sub = 0; for (int i = 0; i < 5; i++) Blade(b, new Vector3(0, 0.45f, 0), new Vector3(Mathf.Cos(i * 1.257f) * 0.14f, 0.5f, Mathf.Sin(i * 1.257f) * 0.14f), 0.08f); b.Sub = 2; b.Sphere(new Vector3(0, 0.46f, 0), 0.035f, 5, 3); b.Sub = 0; },
                        new[] { O(new Color(1f, 0.82f, 0.3f)), O(new Color(0.4f, 0.55f, 0.3f)), O(new Color(0.6f, 0.3f, 0.15f)) }, 2f, 0.8f, 1.3f);
                    Add("redshroom", b => Mushroom(b, 2.6f, 1.4f, 7), new[] { O(new Color(0.85f, 0.25f, 0.18f)), O(new Color(0.9f, 0.85f, 0.72f)), G(new Color(1f, 0.85f, 0.4f), 1.2f) }, 0.35f, 0.8f, 1.8f, true);
                    break;
                case "pelagia":
                    Add("seagrass_d", b => GrassTuft(b, 6, 0.5f, 21), new[] { O(new Color(0.5f, 0.55f, 0.35f)) }, 3f, 0.7f, 1.3f, false, true);
                    Add("grass_p", b => GrassTuft(b, 9, 0.5f, 22), new[] { O(new Color(0.4f, 0.7f, 0.4f)) }, 4f, 0.8f, 1.5f);
                    Add("frond", b => { for (int i = 0; i < 6; i++) Frond(b, new Vector3(0, 0.05f, 0), i * 60 + 15, 0.8f, 0.12f, 0.35f, 0.3f); }, new[] { O(new Color(0.3f, 0.75f, 0.65f)) }, 2f, 0.8f, 1.6f);
                    Add("anemone", b => { b.Sub = 1; b.Cylinder(Vector3.zero, 0.12f, 0.25f, 8, false, 0.18f); b.Sub = 0; for (int i = 0; i < 10; i++) { float a = i * 0.628f; b.Tube(new Vector3(Mathf.Cos(a) * 0.12f, 0.25f, Mathf.Sin(a) * 0.12f), new Vector3(Mathf.Cos(a) * 0.32f, 0.55f + (i % 3) * 0.06f, Mathf.Sin(a) * 0.32f), 0.025f, 4, false, 0.01f); } b.Sub = 2; for (int i = 0; i < 10; i++) { float a = i * 0.628f; b.Sphere(new Vector3(Mathf.Cos(a) * 0.32f, 0.56f + (i % 3) * 0.06f, Mathf.Sin(a) * 0.32f), 0.035f, 5, 3); } b.Sub = 0; },
                        new[] { O(new Color(0.95f, 0.45f, 0.65f)), O(new Color(0.8f, 0.5f, 0.6f)), G(new Color(0.4f, 1f, 0.95f), 2.4f) }, 1.5f, 0.8f, 1.5f);
                    Add("bigflower", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0.1f, 1.4f, 0), 0.05f, 5, false, 0.035f); b.Sub = 0; for (int i = 0; i < 6; i++) Frond(b, new Vector3(0.1f, 1.4f, 0), i * 60, 0.55f, 0.18f, 0.2f, 0.25f); b.Sub = 2; b.Sphere(new Vector3(0.1f, 1.45f, 0), 0.12f, 7, 5); b.Sub = 0; },
                        new[] { O(new Color(1f, 0.55f, 0.6f)), O(new Color(0.35f, 0.6f, 0.4f)), G(new Color(1f, 0.9f, 0.4f), 1.8f) }, 0.8f, 0.8f, 1.5f);
                    Add("bush_p", b => { b.Crumple(new Vector3(0, 0.25f, 0), 0.5f, 0.7f, 8, 0.25f, 8, 5); b.Sub = 2; for (int i = 0; i < 6; i++) b.Sphere(new Vector3(Mathf.Cos(i) * 0.42f, 0.3f + (i % 2) * 0.15f, Mathf.Sin(i) * 0.42f), 0.05f, 5, 3); b.Sub = 0; },
                        new[] { O(new Color(0.3f, 0.6f, 0.4f)), O(Color.gray), O(new Color(0.95f, 0.4f, 0.3f)) }, 1.2f, 0.8f, 1.6f);
                    Add("tealshroom", b => Mushroom(b, 2.2f, 1.2f, 9), new[] { O(new Color(0.25f, 0.65f, 0.7f)), O(new Color(0.9f, 0.9f, 0.85f)), G(new Color(0.4f, 1f, 0.9f), 1.6f) }, 0.25f, 0.8f, 1.6f, true);
                    break;
                case "nivalis":
                    Add("lichen_d", b => b.Crumple(Vector3.zero, 0.35f, 0.15f, 5, 0.35f, 7, 3), new[] { O(new Color(0.55f, 0.62f, 0.58f)) }, 3f, 0.7f, 1.5f, false, true);
                    Add("lichen_g", b => { b.Crumple(Vector3.zero, 0.45f, 0.14f, 6, 0.35f, 8, 3); b.Sub = 2; for (int i = 0; i < 5; i++) b.Sphere(new Vector3(Mathf.Cos(i * 1.3f) * 0.25f, 0.06f, Mathf.Sin(i * 1.3f) * 0.25f), 0.04f, 5, 3); b.Sub = 0; },
                        new[] { G(new Color(0.3f, 0.9f, 0.75f), 0.8f), O(Color.gray), G(new Color(0.6f, 1f, 1f), 2.4f) }, 4f, 0.8f, 1.6f);
                    Add("crystal", b =>
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i * 1.26f, lean = 12f + (i % 3) * 10f;
                            var o = b.M;
                            b.M = o * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.08f, 0, Mathf.Sin(a) * 0.08f), Quaternion.Euler(Mathf.Sin(a) * lean, 0, -Mathf.Cos(a) * lean), Vector3.one);
                            float h = 0.35f + (i % 3) * 0.2f, r = 0.06f + (i % 2) * 0.03f;
                            b.Cylinder(Vector3.zero, r, h, 6, false);
                            b.Cylinder(new Vector3(0, h, 0), r, r * 2f, 6, false, 0f);
                            b.M = o;
                        }
                    }, new[] { Mats.Get(Mats.Emissive, new Color(0.65f, 0.85f, 1f), new Color(0.25f, 0.5f, 0.9f), 0.95f) }, 2f, 0.7f, 1.8f);
                    Add("frostfern", b => { for (int i = 0; i < 7; i++) Frond(b, Vector3.zero, i * 51, 0.6f, 0.1f, 0.4f, 0.2f); }, new[] { O(new Color(0.7f, 0.85f, 0.95f), 0.6f) }, 2f, 0.8f, 1.5f);
                    Add("crystalflower", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0, 0.5f, 0), 0.015f, 4); b.Sub = 0; for (int i = 0; i < 6; i++) Blade(b, new Vector3(0, 0.5f, 0), new Vector3(Mathf.Cos(i * 1.047f) * 0.16f, 0.62f, Mathf.Sin(i * 1.047f) * 0.16f), 0.07f); b.Sub = 0; },
                        new[] { G(new Color(0.75f, 0.5f, 1f), 1.6f), O(new Color(0.5f, 0.6f, 0.7f)) }, 1.5f, 0.8f, 1.4f);
                    Add("glowshroom", b => Mushroom(b, 1.8f, 1.0f, 13), new[] { O(new Color(0.25f, 0.4f, 0.8f)), O(new Color(0.85f, 0.9f, 0.95f)), G(new Color(0.4f, 0.8f, 1f), 2.4f) }, 0.3f, 0.8f, 2.0f, true);
                    break;
                default:
                    Add("drygrass_t", b => GrassTuft(b, 6, 0.4f, 31), new[] { O(new Color(0.55f, 0.5f, 0.32f)) }, 3f, 0.7f, 1.3f, false, true);
                    Add("weed", b => { for (int i = 0; i < 5; i++) Frond(b, Vector3.zero, i * 72, 0.3f, 0.06f, 0.12f, 0.05f); }, new[] { O(new Color(0.42f, 0.5f, 0.3f)) }, 1.5f, 0.8f, 1.4f, false, true);
                    Add("grass", b => GrassTuft(b, 9, 0.5f, 32), new[] { O(new Color(0.42f, 0.7f, 0.3f)) }, 4f, 0.8f, 1.5f);
                    Add("grass2", b => GrassTuft(b, 7, 0.7f, 33), new[] { O(new Color(0.55f, 0.78f, 0.35f)) }, 2f, 0.8f, 1.4f);
                    Add("flower_r", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0.02f, 0.45f, 0), 0.015f, 4); b.Sub = 0; for (int i = 0; i < 5; i++) Blade(b, new Vector3(0.02f, 0.45f, 0), new Vector3(0.02f + Mathf.Cos(i * 1.257f) * 0.13f, 0.52f, Mathf.Sin(i * 1.257f) * 0.13f), 0.08f); b.Sub = 2; b.Sphere(new Vector3(0.02f, 0.47f, 0), 0.035f, 5, 3); b.Sub = 0; },
                        new[] { O(new Color(0.9f, 0.25f, 0.25f)), O(new Color(0.35f, 0.55f, 0.28f)), O(new Color(1f, 0.85f, 0.3f)) }, 1.5f, 0.8f, 1.3f);
                    Add("flower_y2", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0, 0.35f, 0), 0.015f, 4); b.Sub = 0; for (int i = 0; i < 7; i++) Blade(b, new Vector3(0, 0.35f, 0), new Vector3(Mathf.Cos(i * 0.9f) * 0.1f, 0.4f, Mathf.Sin(i * 0.9f) * 0.1f), 0.05f); b.Sub = 2; b.Sphere(new Vector3(0, 0.37f, 0), 0.03f, 5, 3); b.Sub = 0; },
                        new[] { O(new Color(0.98f, 0.9f, 0.4f)), O(new Color(0.35f, 0.55f, 0.28f)), O(new Color(0.55f, 0.3f, 0.15f)) }, 1.5f, 0.8f, 1.3f);
                    Add("glowflower", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0.03f, 0.6f, 0), 0.018f, 4); b.Sub = 0; for (int i = 0; i < 4; i++) Frond(b, Vector3.zero, i * 90 + 20, 0.25f, 0.05f, 0.08f, 0.02f); b.Sub = 2; b.Sphere(new Vector3(0.03f, 0.64f, 0), 0.07f, 6, 4, 1.4f); b.Sub = 0; },
                        new[] { O(new Color(0.3f, 0.55f, 0.3f)), O(new Color(0.3f, 0.5f, 0.3f)), G(new Color(0.45f, 0.75f, 1f), 2.2f) }, 0.8f, 0.8f, 1.4f);
                    Add("bush", b => { b.Crumple(new Vector3(0, 0.3f, 0), 0.55f, 0.75f, 7, 0.25f, 8, 5); b.Crumple(new Vector3(0.35f, 0.2f, 0.1f), 0.35f, 0.8f, 8, 0.25f, 7, 4); b.Sub = 2; for (int i = 0; i < 7; i++) b.Sphere(new Vector3(Mathf.Cos(i) * 0.45f, 0.35f + (i % 2) * 0.2f, Mathf.Sin(i) * 0.45f), 0.05f, 5, 3); b.Sub = 0; },
                        new[] { O(new Color(0.3f, 0.55f, 0.25f)), O(Color.gray), O(new Color(0.9f, 0.3f, 0.35f)) }, 1.2f, 0.8f, 1.6f);
                    Add("fern", b => { for (int i = 0; i < 7; i++) Frond(b, Vector3.zero, i * 51, 0.7f, 0.1f, 0.4f, 0.25f); }, new[] { O(new Color(0.35f, 0.62f, 0.3f)) }, 1.5f, 0.8f, 1.5f);
                    Add("bigflower_t", b => { b.Sub = 1; b.Tube(Vector3.zero, new Vector3(0.1f, 1.6f, 0), 0.05f, 5, false, 0.035f); b.Sub = 0; for (int i = 0; i < 7; i++) Frond(b, new Vector3(0.1f, 1.6f, 0), i * 51, 0.6f, 0.18f, 0.22f, 0.3f); b.Sub = 2; b.Sphere(new Vector3(0.1f, 1.65f, 0), 0.13f, 7, 5); b.Sub = 0; },
                        new[] { O(new Color(0.95f, 0.45f, 0.7f)), O(new Color(0.35f, 0.55f, 0.3f)), G(new Color(1f, 0.85f, 0.4f), 1.6f) }, 0.3f, 0.9f, 1.6f, true);
                    break;
            }
            AddHillKinds(p);
            var rng = new Rng(def.Seed + 1234);
            float wSum = 0, wPio = 0;
            foreach (var k in kinds) { if (k.Hill) continue; if (k.Pioneer) wPio += k.Weight; else wSum += k.Weight; }
            // Bewuchs konzentriert sich um Pflanzstellen, Projektplätze und Lichtpunkte – dazu locker verstreut
            var anchors = new List<V3>();
            foreach (var e in layout.Eco) anchors.Add(e.Pos);
            foreach (var s in layout.ProjectSites) anchors.Add(s);
            foreach (var z in layout.Zones) anchors.Add(z.Center);
            int target = 7500, pioneers = 1400;
            for (int i = 0; i < (target + pioneers) * 4 && plants.Count < target + pioneers; i++)
            {
                bool pio = plants.Count >= target || (i % 6 == 5);
                float x, z;
                if (!pio && i % 3 != 0)
                {
                    var a = anchors[rng.Range(0, anchors.Count)];
                    float ang = rng.Range(0, 6.283f), r = Mathf.Pow(rng.Next(), 0.6f) * 24f;
                    x = a.x + Mathf.Cos(ang) * r; z = a.z + Mathf.Sin(ang) * r;
                }
                else { x = rng.Range(-146f, 146f); z = rng.Range(-146f, 146f); }
                // Kind wählen (gewichtet innerhalb der Gruppe)
                float pick = rng.Next() * (pio ? wPio : wSum);
                int kind = -1;
                for (int k = 0; k < kinds.Count; k++)
                {
                    if (kinds[k].Pioneer != pio || kinds[k].Hill) continue;
                    pick -= kinds[k].Weight;
                    if (pick <= 0) { kind = k; break; }
                }
                if (kind < 0) continue;
                var kd = kinds[kind];
                if (!Free(layout, x, z, kd.Big ? 1.6f : 0.5f)) continue;
                float h = Terrain.HeightAt(p, x, z);
                if (p == "pelagia" && h < 0.35f) continue;
                plants.Add(new Plant
                {
                    Pos = new Vector3(x, h - 0.02f, z), Rot = rng.Range(0, 360f), Scale = rng.Range(kd.SMin, kd.SMax),
                    Threshold = pio ? -1f : rng.Next() * 0.95f, Q = rng.Next(), Kind = kind, Area = PlanetLayout.AreaOf(z)
                });
            }
            PlaceHillDressing(p, layout, def.Seed + 4321);
        }

        /// <summary>
        /// Hügel-Ausstattung (PELAGIA-Inseln, TERRA-Parks): Grasbüschel in zwei Tönen und flache Steine auf Wiesenhängen –
        /// von Anfang an da (gehört zum Gelände, nicht zur Begrünung), damit grüne Hänge nicht einfarbig wirken.
        /// </summary>
        void AddHillKinds(string p)
        {
            if (p != "pelagia" && p != "terra") return;
            var rockM = O(p == "pelagia" ? new Color(0.62f, 0.58f, 0.56f) : new Color(0.55f, 0.53f, 0.5f), 0.15f);
            var rockD = O(p == "pelagia" ? new Color(0.46f, 0.44f, 0.46f) : new Color(0.42f, 0.4f, 0.38f), 0.1f);
            var tuftA = O(p == "pelagia" ? new Color(0.36f, 0.58f, 0.3f) : new Color(0.4f, 0.55f, 0.28f));
            var tuftB = O(p == "pelagia" ? new Color(0.62f, 0.66f, 0.36f) : new Color(0.6f, 0.58f, 0.32f));
            var k1 = Add("hilltuft_a", b => GrassTuft(b, 9, 0.42f, 31), new[] { tuftA }, 1f, 0.8f, 1.5f, false, true); k1.Hill = true;
            var k2 = Add("hilltuft_b", b => GrassTuft(b, 6, 0.3f, 37), new[] { tuftB }, 1f, 0.7f, 1.3f, false, true); k2.Hill = true;
            var k3 = Add("hillrock", b => { b.Crumple(new Vector3(0, 0.02f, 0), 0.32f, 0.45f, 5, 0.25f, 7, 4); b.Sub = 1; b.Crumple(new Vector3(0.36f, 0f, 0.12f), 0.16f, 0.5f, 9, 0.3f, 6, 3); b.Sub = 0; },
                new[] { rockM, rockD }, 1f, 0.6f, 1.6f, false, true); k3.Hill = true; k3.Still = true;
        }

        void PlaceHillDressing(string p, PlanetLayout layout, int seed)
        {
            int ta = -1, tb = -1, rk = -1;
            for (int k = 0; k < kinds.Count; k++) { if (!kinds[k].Hill) continue; if (kinds[k].Still) rk = k; else if (ta < 0) ta = k; else tb = k; }
            if (ta < 0 || tb < 0 || rk < 0) return;
            var rng = new Rng(seed);
            int placed = 0, want = p == "pelagia" ? 2600 : 900;
            for (int i = 0; i < want * 8 && placed < want; i++)
            {
                float x = rng.Range(-146f, 146f), z = rng.Range(-146f, 146f);
                float h = Terrain.HeightAt(p, x, z);
                if (p == "pelagia" && h < 0.7f) continue;
                if (p == "terra" && z < 52f) continue; // TERRA: nur die Parkhügel im Norden
                float hx = Terrain.HeightAt(p, x + 1f, z) - h, hz = Terrain.HeightAt(p, x, z + 1f) - h;
                float slope = Mathf.Sqrt(hx * hx + hz * hz);
                if (slope < 0.06f && rng.Next() > 0.25f) continue; // bevorzugt an Hängen
                if (slope > 0.9f) continue;
                bool rock = rng.Next() < (0.18f + slope * 0.35f);
                int kind = rock ? rk : (rng.Next() < 0.6f ? ta : tb);
                if (!Free(layout, x, z, rock ? 0.8f : 0.4f)) continue;
                var kd = kinds[kind];
                plants.Add(new Plant { Pos = new Vector3(x, h - (rock ? 0.08f : 0.02f), z), Rot = rng.Range(0, 360f), Scale = rng.Range(kd.SMin, kd.SMax), Threshold = -1f, Q = rng.Next(), Kind = kind, Area = PlanetLayout.AreaOf(z) });
                placed++;
                // Büschel stehen in kleinen Gruppen
                if (!rock)
                    for (int c = 0; c < 2; c++)
                    {
                        float cx = x + rng.Range(-0.9f, 0.9f), cz = z + rng.Range(-0.9f, 0.9f);
                        if (!Free(layout, cx, cz, 0.4f)) continue;
                        plants.Add(new Plant { Pos = new Vector3(cx, Terrain.HeightAt(p, cx, cz) - 0.02f, cz), Rot = rng.Range(0, 360f), Scale = rng.Range(kd.SMin, kd.SMax) * 0.85f, Threshold = -1f, Q = rng.Next(), Kind = kind, Area = PlanetLayout.AreaOf(cz) });
                    }
            }
        }

        bool Free(PlanetLayout layout, float x, float z, float margin)
        {
            if (Mathf.Abs(x) > 147f || Mathf.Abs(z) > 147f) return false;
            if (layout.Base.InBase(x, z)) return false;
            // Straßen freihalten (Rinnstein darf bewachsen sein)
            foreach (var r in layout.Roads)
            {
                float hw = r[4] * 0.5f - 0.6f;
                if (Mathf.Abs(r[0] - r[2]) < 0.01f) { if (z >= Mathf.Min(r[1], r[3]) && z <= Mathf.Max(r[1], r[3]) && Mathf.Abs(x - r[0]) < hw) return false; }
                else if (x >= Mathf.Min(r[0], r[2]) && x <= Mathf.Max(r[0], r[2]) && Mathf.Abs(z - r[1]) < hw) return false;
            }
            layout.Query(x, z, margin + 1f, tmp);
            foreach (var b in tmp) if (b.Solid && b.Gate < 0 && b.DuneSet < 0 && b.Contains(x, z, margin)) return false;
            return true;
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
                timer = 0.4f;
                for (int a = 0; a < 3; a++)
                {
                    float eco = Rules.EcoFraction(w, ps, a);
                    bool project = ps.Projects[GameData.ProjectId(planet, a)].Done;
                    // Nach dem Projekt ein zaghafter Anfang, die volle Wiese erst mit der Ökologie
                    grow[a] = before ? 0f : Mathf.Clamp01(eco * 0.9f + (project ? 0.1f : 0f));
                }
                Rebuild();
            }
            bool shadows = GameApp.I == null || GameApp.I.Settings.Shadows >= 2;
            if (!SystemInfo.supportsInstancing) return;
            ApplyWind(w);
            foreach (var k in kinds)
            {
                int n = k.Count;
                if (n == 0) continue;
                int subs = k.Mesh.subMeshCount;
                for (int c = 0; c * 1023 < n; c++)
                {
                    int cnt = Mathf.Min(1023, n - c * 1023);
                    for (int s = 0; s < subs; s++)
                        Graphics.DrawMeshInstanced(k.Mesh, s, k.Mats[Mathf.Min(s, k.Mats.Length - 1)], k.Buf[c], cnt, null, k.Big && shadows && s < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off, true, 0, null);
                }
            }
        }

        void Rebuild()
        {
            var cam = Camera.main;
            var cp = cam != null ? cam.transform.position : Vector3.zero;
            var st = GameApp.I != null ? GameApp.I.Settings : null;
            float view = st != null ? st.ViewDistance : 1f;
            int q = st != null ? st.Quality : 2;
            float qScale = q <= 0 ? 0.3f : q == 1 ? 0.55f : q == 2 ? 0.8f : 1f;
            float far = 75f * view, farBig = 150f * view;
            foreach (var k in kinds) k.Count = 0;
            foreach (var p in plants)
            {
                if (p.Q > qScale) continue;
                var k = kinds[p.Kind];
                float g = k.Pioneer ? 1f : grow[p.Area];
                if (g <= p.Threshold) continue;
                float d2 = (p.Pos - cp).sqrMagnitude;
                float lim = k.Big ? farBig : far;
                if (d2 > lim * lim) continue;
                // in der Ferne nur jede zweite kleine Pflanze
                if (!k.Big && d2 > far * far * 0.36f && (p.Q * 1000f) % 2f > 1f) continue;
                float s = p.Scale * (k.Pioneer ? 1f : Mathf.Clamp01((g - p.Threshold) * 6f));
                int i = k.Count++;
                int c = i / 1023;
                while (k.Buf.Count <= c) { k.Buf.Add(new Matrix4x4[1023]); k.Base.Add(new Matrix4x4[1023]); k.Wave.Add(new Vector3[1023]); }
                var mx = Matrix4x4.TRS(p.Pos, Quaternion.Euler(0, p.Rot, 0), Vector3.one * s);
                k.Buf[c][i % 1023] = mx;
                k.Base[c][i % 1023] = mx;
                // Phase aus der Lage: Böen laufen als Welle über die Wiese; hohe, weiche Pflanzen schwingen stärker
                float ph = p.Pos.x * 0.21f + p.Pos.z * 0.17f + p.Q * 2.5f;
                float amp = k.Still ? 0f : k.Big ? 0.012f : k.Pioneer ? 0.09f : 0.13f;
                k.Wave[c][i % 1023] = new Vector3(Mathf.Cos(ph), Mathf.Sin(ph), amp * (0.8f + p.Q * 0.4f));
            }
        }

        /// <summary>Letzte Windwerte (Prüfumgebung/Anzeige): Stärke 0..1 und größter Ausschlag (Scherung).</summary>
        public float WindStrength { get; private set; }
        public float MaxShear { get; private set; }

        /// <summary>
        /// Wiegen im Wind: Scherung der Instanzmatrix um den Fußpunkt (x' = x + sx·y, z' = z + sz·y). Bei reiner
        /// Drehung um die Hochachse genügen zwei Multiplikationen je Instanz, die Schwingung kommt ohne Winkelfunktion
        /// je Instanz aus (sin(t + φ) = sin t · cos φ + cos t · sin φ).
        /// </summary>
        void ApplyWind(WorldState w)
        {
            float dx = 1f, dz = 0.3f, wind = 0.2f;
            if (w != null && planet != null && w.Planets.ContainsKey(planet)) wind = Rules.Wind(w, planet, out dx, out dz);
            WindStrength = wind;
            float t = Time.time;
            float f = 1.5f + wind * 2.4f;
            float S = Mathf.Sin(t * f), C = Mathf.Cos(t * f);
            float lean = wind * wind * 0.9f, osc = 0.25f + wind * 0.95f;
            float maxShear = 0f;
            foreach (var k in kinds)
            {
                int n = k.Count;
                for (int c = 0; c * 1023 < n; c++)
                {
                    int cnt = Mathf.Min(1023, n - c * 1023);
                    var src = k.Base[c]; var dst = k.Buf[c]; var wv = k.Wave[c];
                    for (int i = 0; i < cnt; i++)
                    {
                        var a = wv[i];
                        float sh = a.z * (lean + (S * a.x + C * a.y) * osc);
                        var m = src[i];
                        float up = m.m11;
                        m.m01 += dx * sh * up;
                        m.m21 += dz * sh * up;
                        dst[i] = m;
                        if (sh > maxShear) maxShear = sh;
                    }
                }
            }
            MaxShear = maxShear;
        }
    }
}
