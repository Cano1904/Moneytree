using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Prozedurale Low-Poly-Formen der zurückkehrenden Tiere (Ursprung am Boden bzw. Körpermitte, Blick nach +Z).
    /// Untermeshes: 0 = Fell/Gefieder/Schuppen, 1 = helle Partie (Bauch, Brust, Schwanzspitze), 2 = dunkle Details
    /// (Augen, Nase, Schnabel, Beine). Bewegliche Teile (Flügel, Schwanz, Schwanzflosse) sind eigene Meshes mit dem
    /// Gelenk im Ursprung – die Tierdarstellung dreht sie je Instanz.
    /// </summary>
    public static class AnimalMeshes
    {
        /// <summary>Ellipsoid (Kugel mit Größe je Achse) um c.</summary>
        static void Ell(MeshBuilder b, Vector3 c, Vector3 size, int seg = 8, int rings = 6)
        {
            var o = b.M;
            b.M = o * Matrix4x4.TRS(c, Quaternion.identity, size);
            b.Sphere(Vector3.zero, 0.5f, seg, rings);
            b.M = o;
        }

        static void EllRot(MeshBuilder b, Vector3 c, Vector3 size, Vector3 euler, int seg = 8, int rings = 6)
        {
            var o = b.M;
            b.M = o * Matrix4x4.TRS(c, Quaternion.Euler(euler), size);
            b.Sphere(Vector3.zero, 0.5f, seg, rings);
            b.M = o;
        }

        /// <summary>Doppelseitiges Dreieck.</summary>
        static void Tri2(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
            b.TriFace(a, c, d, n);
            b.TriFace(a, c, d, -n);
        }

        /// <summary>Doppelseitiges Viereck a-b-c-d.</summary>
        static void Quad2(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e)
        {
            var n = Vector3.Cross(c - a, d - a) + Vector3.Cross(d - a, e - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
            b.Face(a, c, d, e, n);
            b.Face(a, c, d, e, -n);
        }

        // ------------------------------------------------------------------ Bodentiere
        /// <summary>Hase / Kaninchen (≈ 0,45 m).</summary>
        public static Mesh Rabbit()
        {
            return MeshKit.Get("tier_hase", b =>
            {
                b.Sub = 0;
                Ell(b, new Vector3(0, 0.2f, -0.02f), new Vector3(0.24f, 0.22f, 0.34f));
                Ell(b, new Vector3(0, 0.19f, -0.11f), new Vector3(0.27f, 0.25f, 0.22f));
                Ell(b, new Vector3(0, 0.31f, 0.16f), new Vector3(0.16f, 0.15f, 0.18f));
                b.BoxRot(new Vector3(-0.035f, 0.45f, 0.12f), new Vector3(0.045f, 0.19f, 0.06f), new Vector3(-20, 0, -8));
                b.BoxRot(new Vector3(0.035f, 0.45f, 0.12f), new Vector3(0.045f, 0.19f, 0.06f), new Vector3(-20, 0, 8));
                foreach (var sx in new[] { -1f, 1f })
                {
                    Ell(b, new Vector3(sx * 0.09f, 0.11f, -0.1f), new Vector3(0.08f, 0.14f, 0.2f), 6, 4);
                    b.Box(new Vector3(sx * 0.09f, 0.02f, -0.04f), new Vector3(0.06f, 0.035f, 0.16f));
                    b.Box(new Vector3(sx * 0.05f, 0.07f, 0.1f), new Vector3(0.04f, 0.14f, 0.045f));
                }
                b.Sub = 1;
                b.Sphere(new Vector3(0, 0.24f, -0.25f), 0.055f, 6, 4);
                b.Sphere(new Vector3(0, 0.285f, 0.235f), 0.045f, 6, 4);
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f }) b.Sphere(new Vector3(sx * 0.055f, 0.34f, 0.22f), 0.02f, 5, 3);
                b.Sphere(new Vector3(0, 0.305f, 0.265f), 0.016f, 4, 3);
                b.Sub = 0;
            });
        }

        /// <summary>Fuchs (≈ 0,8 m ohne Schwanz); Schwanz: <see cref="FoxTail"/>, Gelenk bei <see cref="FoxTailPivot"/>.</summary>
        public static Mesh Fox()
        {
            return MeshKit.Get("tier_fuchs", b =>
            {
                b.Sub = 0;
                Ell(b, new Vector3(0, 0.37f, -0.02f), new Vector3(0.22f, 0.22f, 0.56f));
                Ell(b, new Vector3(0, 0.5f, 0.3f), new Vector3(0.18f, 0.16f, 0.19f));
                b.Tube(new Vector3(0, 0.48f, 0.36f), new Vector3(0, 0.45f, 0.5f), 0.06f, 6, true, 0.022f);
                foreach (var sx in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(sx * 0.05f, 0.56f, 0.28f), new Vector3(sx * 0.075f, 0.68f, 0.25f), 0.038f, 5, true, 0.004f);
                    b.Tube(new Vector3(sx * 0.07f, 0.38f, 0.19f), new Vector3(sx * 0.07f, 0.17f, 0.2f), 0.036f, 5);
                    b.Tube(new Vector3(sx * 0.075f, 0.38f, -0.2f), new Vector3(sx * 0.08f, 0.17f, -0.22f), 0.04f, 5);
                }
                b.Sub = 1;
                Ell(b, new Vector3(0, 0.33f, 0.19f), new Vector3(0.16f, 0.2f, 0.2f), 6, 5);
                Ell(b, new Vector3(0, 0.45f, 0.4f), new Vector3(0.1f, 0.06f, 0.14f), 6, 4);
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(sx * 0.07f, 0.19f, 0.2f), new Vector3(sx * 0.07f, 0.0f, 0.22f), 0.026f, 5);
                    b.Tube(new Vector3(sx * 0.08f, 0.19f, -0.22f), new Vector3(sx * 0.085f, 0.0f, -0.24f), 0.028f, 5);
                    b.Sphere(new Vector3(sx * 0.055f, 0.54f, 0.37f), 0.018f, 5, 3);
                }
                b.Sphere(new Vector3(0, 0.455f, 0.505f), 0.022f, 5, 3);
                b.Sub = 0;
            });
        }

        public static readonly Vector3 FoxTailPivot = new Vector3(0, 0.42f, -0.27f);

        public static Mesh FoxTail()
        {
            return MeshKit.Get("tier_fuchs_schwanz", b =>
            {
                b.Sub = 0;
                EllRot(b, new Vector3(0, -0.07f, -0.2f), new Vector3(0.13f, 0.13f, 0.44f), new Vector3(-18, 0, 0), 7, 5);
                b.Sub = 1;
                b.Sphere(new Vector3(0, -0.13f, -0.41f), 0.06f, 6, 4);
                b.Sub = 0;
            });
        }

        /// <summary>Echse (≈ 0,5 m mit Schwanz).</summary>
        public static Mesh Lizard()
        {
            return MeshKit.Get("tier_echse", b =>
            {
                b.Sub = 0;
                Ell(b, new Vector3(0, 0.055f, 0f), new Vector3(0.1f, 0.07f, 0.28f), 7, 5);
                Ell(b, new Vector3(0, 0.065f, 0.17f), new Vector3(0.08f, 0.06f, 0.13f), 6, 4);
                b.Tube(new Vector3(0, 0.05f, -0.12f), new Vector3(0, 0.025f, -0.42f), 0.032f, 5, true, 0.004f);
                foreach (var sx in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(sx * 0.035f, 0.05f, 0.08f), new Vector3(sx * 0.11f, 0.005f, 0.11f), 0.014f, 4);
                    b.Tube(new Vector3(sx * 0.035f, 0.05f, -0.08f), new Vector3(sx * 0.11f, 0.005f, -0.1f), 0.015f, 4);
                }
                b.Sub = 1;
                Ell(b, new Vector3(0, 0.083f, -0.01f), new Vector3(0.05f, 0.03f, 0.24f), 6, 3);
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f }) b.Sphere(new Vector3(sx * 0.03f, 0.085f, 0.2f), 0.012f, 4, 3);
                b.Sub = 0;
            });
        }

        /// <summary>Krabbe (≈ 0,35 m breit), läuft seitwärts.</summary>
        public static Mesh Crab()
        {
            return MeshKit.Get("tier_krabbe", b =>
            {
                b.Sub = 0;
                Ell(b, new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.1f, 0.22f), 8, 4);
                foreach (var sx in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(sx * 0.1f, 0.1f, 0.06f), new Vector3(sx * 0.17f, 0.11f, 0.13f), 0.02f, 4);
                    Ell(b, new Vector3(sx * 0.19f, 0.11f, 0.16f), new Vector3(0.09f, 0.07f, 0.12f), 6, 4);
                    for (int k = 0; k < 3; k++)
                    {
                        float z = -0.06f + k * 0.055f;
                        b.Tube(new Vector3(sx * 0.12f, 0.1f, z), new Vector3(sx * 0.22f, 0.12f, z * 1.2f), 0.013f, 4);
                        b.Tube(new Vector3(sx * 0.22f, 0.12f, z * 1.2f), new Vector3(sx * 0.27f, 0.0f, z * 1.35f), 0.011f, 4);
                    }
                    b.Tube(new Vector3(sx * 0.04f, 0.13f, 0.08f), new Vector3(sx * 0.05f, 0.19f, 0.1f), 0.01f, 4);
                }
                b.Sub = 1;
                Ell(b, new Vector3(0, 0.07f, 0.0f), new Vector3(0.24f, 0.05f, 0.17f), 6, 3);
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f }) b.Sphere(new Vector3(sx * 0.05f, 0.2f, 0.1f), 0.018f, 5, 3);
                b.Sub = 0;
            });
        }

        /// <summary>Pinguinartiger Vogel (≈ 0,7 m hoch), watschelt.</summary>
        public static Mesh Penguin()
        {
            return MeshKit.Get("tier_pinguin", b =>
            {
                b.Sub = 0;
                Ell(b, new Vector3(0, 0.34f, 0), new Vector3(0.3f, 0.58f, 0.28f), 9, 7);
                b.Sphere(new Vector3(0, 0.66f, 0.02f), 0.12f, 8, 6);
                foreach (var sx in new[] { -1f, 1f })
                    b.BoxRot(new Vector3(sx * 0.16f, 0.38f, -0.01f), new Vector3(0.03f, 0.27f, 0.1f), new Vector3(0, 0, sx * 14));
                b.Sub = 1;
                Ell(b, new Vector3(0, 0.3f, 0.05f), new Vector3(0.24f, 0.46f, 0.22f), 8, 6);
                foreach (var sx in new[] { -1f, 1f }) b.Sphere(new Vector3(sx * 0.055f, 0.68f, 0.1f), 0.04f, 5, 3);
                b.Sub = 2;
                b.Tube(new Vector3(0, 0.65f, 0.12f), new Vector3(0, 0.625f, 0.22f), 0.03f, 5, true, 0.005f);
                foreach (var sx in new[] { -1f, 1f }) b.Box(new Vector3(sx * 0.06f, 0.015f, 0.06f), new Vector3(0.07f, 0.03f, 0.11f));
                b.Sub = 0;
            });
        }

        // ------------------------------------------------------------------ Vögel
        /// <summary>Vogelkörper (Länge ≈ 1, wird je Art skaliert). Füße bei y = −<see cref="BirdFoot"/>.</summary>
        public static Mesh Bird()
        {
            return MeshKit.Get("tier_vogel", b =>
            {
                b.Sub = 0;
                Ell(b, Vector3.zero, new Vector3(0.32f, 0.3f, 0.78f), 8, 6);
                b.Sphere(new Vector3(0, 0.12f, 0.36f), 0.15f, 8, 6);
                b.BoxRot(new Vector3(0, 0.02f, -0.48f), new Vector3(0.24f, 0.03f, 0.3f), new Vector3(-8, 0, 0));
                b.Sub = 1;
                Ell(b, new Vector3(0, -0.07f, 0.06f), new Vector3(0.26f, 0.22f, 0.55f), 7, 5);
                b.Sub = 2;
                b.Tube(new Vector3(0, 0.1f, 0.47f), new Vector3(0, 0.08f, 0.62f), 0.045f, 5, true, 0.005f);
                foreach (var sx in new[] { -1f, 1f })
                {
                    b.Sphere(new Vector3(sx * 0.075f, 0.16f, 0.44f), 0.026f, 5, 3);
                    b.Tube(new Vector3(sx * 0.05f, -0.1f, 0.0f), new Vector3(sx * 0.05f, -BirdFoot, 0.03f), 0.016f, 4);
                }
                b.Sub = 0;
            });
        }

        /// <summary>Abstand Körpermitte → Füße (Einheitsvogel).</summary>
        public const float BirdFoot = 0.3f;
        /// <summary>Schultergelenk rechts (links gespiegelt) im Körperraum des Einheitsvogels.</summary>
        public static readonly Vector3 WingPivot = new Vector3(0.12f, 0.06f, 0.06f);

        /// <summary>Flügel (rechts: +X, links: −X), Gelenk im Ursprung; Untermesh 1 = Flügelspitze.</summary>
        public static Mesh Wing(bool right)
        {
            return MeshKit.Get(right ? "tier_fluegel_r" : "tier_fluegel_l", b =>
            {
                float s = right ? 1f : -1f;
                var r0 = new Vector3(0, 0, 0.17f); var r1 = new Vector3(0, 0, -0.2f);
                var m0 = new Vector3(s * 0.5f, 0.03f, 0.12f); var m1 = new Vector3(s * 0.5f, 0.03f, -0.2f);
                var t0 = new Vector3(s * 0.8f, 0.01f, 0.02f); var t1 = new Vector3(s * 0.78f, 0.01f, -0.2f);
                var tip = new Vector3(s * 1.0f, 0f, -0.16f);
                b.Sub = 0;
                Quad2(b, r0, m0, m1, r1);
                b.Sub = 1;
                Quad2(b, m0, t0, t1, m1);
                Tri2(b, t0, tip, t1);
                b.Sub = 0;
            });
        }

        // ------------------------------------------------------------------ Fische
        /// <summary>Fischkörper (Länge ≈ 1, je Art skaliert); Schwanzflosse: <see cref="FishTail"/> bei <see cref="FishTailPivot"/>.</summary>
        public static Mesh Fish()
        {
            return MeshKit.Get("tier_fisch", b =>
            {
                b.Sub = 0;
                Ell(b, Vector3.zero, new Vector3(0.18f, 0.34f, 0.8f), 8, 6);
                Tri2(b, new Vector3(0, 0.15f, 0.12f), new Vector3(0, 0.3f, -0.1f), new Vector3(0, 0.14f, -0.22f));
                b.Sub = 1;
                Ell(b, new Vector3(0, -0.07f, 0.02f), new Vector3(0.14f, 0.2f, 0.62f), 7, 5);
                b.Sub = 2;
                foreach (var sx in new[] { -1f, 1f }) b.Sphere(new Vector3(sx * 0.07f, 0.05f, 0.27f), 0.032f, 5, 3);
                b.Sub = 0;
            });
        }

        public static readonly Vector3 FishTailPivot = new Vector3(0, 0, -0.37f);

        public static Mesh FishTail()
        {
            return MeshKit.Get("tier_fisch_schwanz", b =>
            {
                b.Sub = 0;
                Tri2(b, Vector3.zero, new Vector3(0, 0.2f, -0.3f), new Vector3(0, 0.02f, -0.2f));
                Tri2(b, Vector3.zero, new Vector3(0, -0.02f, -0.2f), new Vector3(0, -0.2f, -0.3f));
            });
        }

        /// <summary>Alle Formen anlegen (Prüfumgebung: Wicklungsprüfung aller Meshes).</summary>
        public static void PrewarmAll()
        {
            Rabbit(); Fox(); FoxTail(); Lizard(); Crab(); Penguin(); Bird(); Wing(true); Wing(false); Fish(); FishTail();
        }
    }
}
