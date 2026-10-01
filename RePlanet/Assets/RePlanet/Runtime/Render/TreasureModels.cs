using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Kleine prozedurale 3D-Modelle der Schätze (eine Form je <see cref="TreasureDef.Icon"/>, Höhe ≈ 0,4 m, Ursprung unten).
    /// Untermeshes: 0 = Farbe des Schatzes, 1 = dunkel, 2 = Gold/Metall, 3 = hell.
    /// </summary>
    public static class TreasureModels
    {
        public static readonly string[] Icons =
        {
            "comic", "figure", "card", "cartridge", "teddy", "globe", "vinyl", "musicbox", "lunchbox", "wrench", "radio", "helmet", "cup", "pendant", "watch",
            "shell", "spade", "goggles", "boat", "bottle", "compass", "lighthouse", "necklace", "thermos", "knight", "calculator", "photo", "book", "penguin", "crystal",
        };

        public static Mesh For(string icon)
        {
            return MeshKit.Get("treasure_" + icon, b => Build(b, icon));
        }

        public static void PrewarmAll() { foreach (var i in Icons) For(i); }

        public static Material[] MaterialsFor(TreasureDef t)
        {
            var c = Mats.C(t.Color);
            var main = t.Icon == "globe" || t.Icon == "bottle" || t.Icon == "crystal" ? Mats.Get(Mats.Fade, new Color(c.r, c.g, c.b, 0.6f), null, 0.95f) : Mats.Get(Mats.Opaque, c);
            return new[] { main, Mats.Get(Mats.Opaque, new Color(0.12f, 0.12f, 0.13f)), Mats.Get(Mats.Metal, new Color(0.85f, 0.68f, 0.3f)), Mats.Get(Mats.Opaque, new Color(0.93f, 0.91f, 0.86f)) };
        }

        public static void Draw(TreasureDef t, Matrix4x4 m, bool shadows = false)
        {
            var mesh = For(t.Icon);
            var mats = MaterialsFor(t);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetIndexCount(s) == 0) continue;
                Graphics.DrawMesh(mesh, m, mats[Mathf.Min(s, mats.Length - 1)], 0, null, s, null, shadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
            }
        }

        static void Build(MeshBuilder b, string icon)
        {
            switch (icon)
            {
                case "comic":
                    b.Sub = 3; b.BoxRot(new Vector3(0, 0.02f, 0), new Vector3(0.26f, 0.03f, 0.36f), new Vector3(0, 8, 0));
                    b.Sub = 0; b.BoxRot(new Vector3(0, 0.04f, 0), new Vector3(0.27f, 0.01f, 0.37f), new Vector3(0, 8, 0));
                    b.Sub = 1; b.Box(new Vector3(0.03f, 0.047f, 0.08f), new Vector3(0.12f, 0.005f, 0.1f));
                    break;
                case "figure":
                    b.Sub = 0; b.Box(new Vector3(0, 0.16f, 0), new Vector3(0.12f, 0.16f, 0.07f));
                    b.Box(new Vector3(-0.035f, 0.04f, 0), new Vector3(0.045f, 0.08f, 0.05f)); b.Box(new Vector3(0.035f, 0.04f, 0), new Vector3(0.045f, 0.08f, 0.05f));
                    b.Sub = 3; b.Sphere(new Vector3(0, 0.29f, 0), 0.05f, 10, 7);
                    b.Sub = 1; b.BoxRot(new Vector3(0, 0.16f, -0.05f), new Vector3(0.14f, 0.18f, 0.01f), new Vector3(8, 0, 0));
                    break;
                case "card":
                case "photo":
                    b.Sub = 3; b.BoxRot(new Vector3(0, 0.015f, 0), new Vector3(0.3f, 0.01f, 0.21f), new Vector3(0, -12, 0));
                    b.Sub = 0; b.BoxRot(new Vector3(icon == "photo" ? 0 : -0.04f, 0.022f, icon == "photo" ? 0.02f : 0), new Vector3(icon == "photo" ? 0.24f : 0.17f, 0.005f, icon == "photo" ? 0.14f : 0.17f), new Vector3(0, -12, 0));
                    break;
                case "cartridge":
                    b.Sub = 0; b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.24f, 0.05f));
                    b.Sub = 3; b.Box(new Vector3(0, 0.15f, -0.027f), new Vector3(0.16f, 0.12f, 0.005f));
                    b.Sub = 2; b.Box(new Vector3(0, 0.01f, 0), new Vector3(0.18f, 0.02f, 0.03f));
                    break;
                case "teddy":
                    b.Sub = 0; b.Sphere(new Vector3(0, 0.13f, 0), 0.12f, 12, 8, 1.1f); b.Sphere(new Vector3(0, 0.3f, 0), 0.09f, 12, 8);
                    b.Sphere(new Vector3(-0.07f, 0.38f, 0), 0.035f, 8, 6); b.Sphere(new Vector3(0.07f, 0.38f, 0), 0.035f, 8, 6);
                    b.Sphere(new Vector3(-0.12f, 0.17f, -0.02f), 0.045f, 8, 6); b.Sphere(new Vector3(0.12f, 0.17f, -0.02f), 0.045f, 8, 6);
                    b.Sub = 3; b.Sphere(new Vector3(0, 0.28f, -0.075f), 0.035f, 8, 6);
                    b.Sub = 1; b.Sphere(new Vector3(-0.035f, 0.32f, -0.075f), 0.012f, 6, 4);
                    break;
                case "globe":
                    b.Sub = 1; b.Cylinder(Vector3.zero, 0.12f, 0.08f, 16, true, 0.1f);
                    b.Sub = 3; b.Box(new Vector3(0, 0.15f, 0), new Vector3(0.07f, 0.06f, 0.06f));
                    b.Sub = 2; b.Box(new Vector3(0.012f, 0.155f, -0.031f), new Vector3(0.018f, 0.018f, 0.002f));
                    b.Sub = 0; b.Sphere(new Vector3(0, 0.19f, 0), 0.13f, 14, 10);
                    break;
                case "vinyl":
                    b.Sub = 1; b.Cylinder(Vector3.zero, 0.18f, 0.012f, 28, true);
                    b.Sub = 0; b.Cylinder(new Vector3(0, 0.012f, 0), 0.06f, 0.003f, 16, true);
                    break;
                case "musicbox":
                    b.Sub = 0; b.Box(new Vector3(0, 0.07f, 0), new Vector3(0.24f, 0.14f, 0.18f));
                    b.Sub = 2; b.Cylinder(new Vector3(0, 0.14f, 0), 0.05f, 0.015f, 14, true);
                    b.Sub = 3; b.Cylinder(new Vector3(0, 0.155f, 0), 0.008f, 0.12f, 6, true); b.Sphere(new Vector3(0, 0.29f, 0), 0.025f, 8, 6);
                    b.Cylinder(new Vector3(0, 0.2f, 0), 0.04f, 0.05f, 10, true, 0.005f);
                    break;
                case "lunchbox":
                    b.Sub = 0; b.Box(new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.16f, 0.12f));
                    b.Sub = 2; b.Tube(new Vector3(-0.06f, 0.16f, 0), new Vector3(0.06f, 0.16f, 0), 0.012f, 6, true);
                    b.Sub = 1; b.Box(new Vector3(0, 0.12f, -0.061f), new Vector3(0.03f, 0.03f, 0.005f));
                    break;
                case "wrench":
                    b.Sub = 2; b.BoxRot(new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.025f, 0.05f), new Vector3(0, 20, 0));
                    b.TorusRot(new Vector3(0.18f, 0.02f, -0.065f), new Vector3(90, 0, 0), 0.045f, 0.018f, 12, 6);
                    b.TorusRot(new Vector3(-0.18f, 0.02f, 0.065f), new Vector3(90, 0, 0), 0.04f, 0.016f, 12, 6);
                    break;
                case "radio":
                    b.Sub = 0; b.Box(new Vector3(0, 0.1f, 0), new Vector3(0.26f, 0.2f, 0.09f));
                    b.Sub = 3; b.Box(new Vector3(0.05f, 0.1f, -0.046f), new Vector3(0.12f, 0.12f, 0.004f));
                    b.Sub = 1; b.Cylinder(new Vector3(-0.08f, 0.08f, -0.05f), 0.025f, 0.01f, 10, true);
                    b.Sub = 2; b.Tube(new Vector3(0.1f, 0.2f, 0.02f), new Vector3(0.16f, 0.42f, 0.02f), 0.006f, 5, true);
                    break;
                case "helmet":
                    b.Sub = 0; b.Sphere(new Vector3(0, 0.05f, 0), 0.17f, 14, 8, 0.85f);
                    b.Cylinder(new Vector3(0, 0.03f, 0), 0.21f, 0.025f, 20, true);
                    b.Sub = 3; b.Sphere(new Vector3(0.08f, 0.16f, -0.1f), 0.03f, 8, 5, 0.3f);
                    break;
                case "cup":
                    b.Sub = 1; b.Box(new Vector3(0, 0.04f, 0), new Vector3(0.16f, 0.08f, 0.16f));
                    b.Sub = 2;
                    b.Lathe(new Vector3(0, 0.08f, 0), new[] { new Vector2(0.03f, 0f), new Vector2(0.02f, 0.06f), new Vector2(0.03f, 0.12f), new Vector2(0.1f, 0.2f), new Vector2(0.12f, 0.32f), new Vector2(0.11f, 0.33f), new Vector2(0f, 0.33f) }, 16);
                    b.TorusRot(new Vector3(-0.12f, 0.33f, 0), new Vector3(0, 0, 0), 0.05f, 0.012f, 10, 5);
                    b.TorusRot(new Vector3(0.12f, 0.33f, 0), new Vector3(0, 0, 0), 0.05f, 0.012f, 10, 5);
                    break;
                case "pendant":
                    b.Sub = 1; b.Torus(new Vector3(0, 0.01f, 0), 0.15f, 0.006f, 24, 4);
                    b.Sub = 2; b.Cylinder(new Vector3(0, 0, -0.15f), 0.06f, 0.015f, 12, true);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; b.Box(new Vector3(Mathf.Cos(a) * 0.07f, 0.008f, -0.15f + Mathf.Sin(a) * 0.07f), new Vector3(0.02f, 0.015f, 0.02f)); }
                    break;
                case "watch":
                    b.Sub = 2; b.CylinderZ(new Vector3(0, 0.1f, 0), 0.1f, 0.03f, 20);
                    b.Torus(new Vector3(0, 0.215f, 0), 0.02f, 0.006f, 10, 4);
                    b.Sub = 3; b.CylinderZ(new Vector3(0, 0.1f, -0.016f), 0.085f, 0.004f, 20);
                    b.Sub = 1; b.Box(new Vector3(0, 0.12f, -0.02f), new Vector3(0.006f, 0.05f, 0.003f)); b.Box(new Vector3(0.018f, 0.1f, -0.02f), new Vector3(0.04f, 0.005f, 0.003f));
                    break;
                case "shell":
                    b.Sub = 0;
                    for (int i = 0; i < 7; i++) b.BoxRot(new Vector3(0, 0.03f, 0.04f), new Vector3(0.05f, 0.04f, 0.2f), new Vector3(-10, -45 + i * 15, 0));
                    b.Sub = 3; b.Sphere(new Vector3(0, 0.025f, -0.06f), 0.04f, 8, 5, 0.6f);
                    break;
                case "spade":
                    b.Sub = 0; b.Box(new Vector3(0, 0.015f, 0.12f), new Vector3(0.14f, 0.03f, 0.16f));
                    b.Tube(new Vector3(0, 0.03f, 0.03f), new Vector3(0, 0.05f, -0.2f), 0.015f, 6, true);
                    b.BoxRot(new Vector3(0, 0.05f, -0.21f), new Vector3(0.08f, 0.025f, 0.03f), Vector3.zero);
                    break;
                case "goggles":
                    b.Sub = 0; b.CylinderZ(new Vector3(-0.06f, 0.06f, 0), 0.05f, 0.04f, 14); b.CylinderZ(new Vector3(0.06f, 0.06f, 0), 0.05f, 0.04f, 14);
                    b.Sub = 3; b.CylinderZ(new Vector3(-0.06f, 0.06f, -0.022f), 0.04f, 0.004f, 14); b.CylinderZ(new Vector3(0.06f, 0.06f, -0.022f), 0.04f, 0.004f, 14);
                    b.Sub = 1; b.Torus(new Vector3(0, 0.06f, 0.08f), 0.12f, 0.008f, 18, 4);
                    break;
                case "boat":
                    b.Sub = 0; b.Prism(new Vector3(0, 0.04f, 0), new Vector3(0.12f, 0.08f, 0.32f));
                    b.Sub = 1; b.Cylinder(new Vector3(0, 0.08f, 0), 0.008f, 0.3f, 6, true);
                    b.Sub = 3; b.TriFace(new Vector3(0, 0.12f, 0.01f), new Vector3(0, 0.37f, 0.01f), new Vector3(0, 0.12f, 0.14f), Vector3.right);
                    b.TriFace(new Vector3(0, 0.12f, 0.01f), new Vector3(0, 0.12f, 0.14f), new Vector3(0, 0.37f, 0.01f), Vector3.left);
                    break;
                case "bottle":
                    b.Sub = 0; b.Lathe(new Vector3(0, 0, 0), new[] { new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.075f, 0.2f), new Vector2(0.03f, 0.27f), new Vector2(0.025f, 0.33f), new Vector2(0f, 0.33f) }, 14);
                    b.Sub = 3; b.CylinderX(new Vector3(0, 0.1f, 0), 0.025f, 0.08f, 8);
                    b.Sub = 2; b.Cylinder(new Vector3(0, 0.33f, 0), 0.022f, 0.03f, 8, true);
                    break;
                case "compass":
                    b.Sub = 2; b.Cylinder(Vector3.zero, 0.12f, 0.04f, 22, true);
                    b.Sub = 3; b.Cylinder(new Vector3(0, 0.04f, 0), 0.1f, 0.003f, 22, true);
                    b.Sub = 0; b.BoxRot(new Vector3(0, 0.046f, 0), new Vector3(0.02f, 0.006f, 0.16f), new Vector3(0, 25, 0));
                    break;
                case "lighthouse":
                    b.Sub = 3; b.Cylinder(Vector3.zero, 0.09f, 0.3f, 14, true, 0.06f);
                    b.Sub = 0; b.Cylinder(new Vector3(0, 0.08f, 0), 0.084f, 0.06f, 14, false, 0.075f); b.Cylinder(new Vector3(0, 0.2f, 0), 0.071f, 0.05f, 14, false, 0.064f);
                    b.Sub = 1; b.Cylinder(new Vector3(0, 0.3f, 0), 0.07f, 0.06f, 12, true);
                    b.Sub = 0; b.Cylinder(new Vector3(0, 0.36f, 0), 0.08f, 0.05f, 12, true, 0f);
                    break;
                case "necklace":
                    b.Sub = 0;
                    for (int i = 0; i < 18; i++) { float a = i / 18f * Mathf.PI * 2f; b.Sphere(new Vector3(Mathf.Cos(a) * 0.14f, 0.02f, Mathf.Sin(a) * 0.17f), 0.02f + 0.005f * (i % 3), 8, 5); }
                    b.Sub = 2; b.Sphere(new Vector3(0, 0.02f, -0.19f), 0.03f, 8, 6);
                    break;
                case "thermos":
                    b.Sub = 0; b.Cylinder(Vector3.zero, 0.07f, 0.32f, 16, true);
                    b.Sub = 1; b.Cylinder(new Vector3(0, 0.32f, 0), 0.06f, 0.06f, 14, true);
                    b.Sub = 3; b.Box(new Vector3(0, 0.15f, -0.07f), new Vector3(0.06f, 0.04f, 0.005f));
                    break;
                case "knight":
                    b.Sub = 0; b.Cylinder(Vector3.zero, 0.08f, 0.05f, 14, true, 0.06f);
                    b.Cylinder(new Vector3(0, 0.05f, 0), 0.05f, 0.12f, 12, true, 0.045f);
                    b.BoxRot(new Vector3(0, 0.22f, -0.02f), new Vector3(0.06f, 0.14f, 0.09f), new Vector3(-20, 0, 0));
                    b.BoxRot(new Vector3(0, 0.25f, -0.08f), new Vector3(0.05f, 0.05f, 0.09f), new Vector3(10, 0, 0));
                    break;
                case "calculator":
                    b.Sub = 0; b.BoxRot(new Vector3(0, 0.015f, 0), new Vector3(0.18f, 0.03f, 0.26f), new Vector3(0, 10, 0));
                    b.Sub = 3; b.BoxRot(new Vector3(0.012f, 0.032f, -0.07f), new Vector3(0.14f, 0.005f, 0.06f), new Vector3(0, 10, 0));
                    b.Sub = 1;
                    for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++) b.Box(new Vector3(-0.045f + x * 0.045f + z * 0.008f, 0.033f, 0.0f + z * 0.045f - x * 0.008f), new Vector3(0.03f, 0.006f, 0.03f));
                    break;
                case "book":
                    b.Sub = 0; b.Box(new Vector3(0, 0.03f, 0), new Vector3(0.24f, 0.06f, 0.32f));
                    b.Sub = 3; b.Box(new Vector3(0.006f, 0.03f, 0), new Vector3(0.235f, 0.05f, 0.3f));
                    b.Sub = 2; b.Box(new Vector3(0.122f, 0.03f, 0), new Vector3(0.01f, 0.065f, 0.33f));
                    break;
                case "penguin":
                    b.Sub = 0; b.Sphere(new Vector3(0, 0.13f, 0), 0.11f, 12, 8, 1.25f); b.Sphere(new Vector3(0, 0.29f, 0), 0.075f, 12, 8);
                    b.Sub = 3; b.Sphere(new Vector3(0, 0.12f, -0.05f), 0.08f, 10, 7, 1.2f);
                    b.Sub = 2; b.Sphere(new Vector3(0, 0.28f, -0.075f), 0.02f, 6, 4);
                    b.Sub = 1; b.Cylinder(new Vector3(0, 0.33f, 0), 0.07f, 0.05f, 12, true, 0.02f);
                    break;
                case "crystal":
                    b.Sub = 1; b.Box(new Vector3(0, 0.02f, 0), new Vector3(0.2f, 0.04f, 0.2f));
                    b.Sub = 0; b.BoxRot(new Vector3(0, 0.15f, 0), new Vector3(0.15f, 0.15f, 0.15f), new Vector3(35, 45, 0));
                    break;
                default:
                    b.Sub = 0; b.Sphere(new Vector3(0, 0.12f, 0), 0.12f, 12, 8);
                    break;
            }
        }
    }
}
