using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// MIKO – die Heldenfigur: prozedural aufgebautes, animiertes Robotermodell. Türkise Schutzhülle mit gefasten,
    /// abgerundeten Kanten, Plattenfugen, Schrauben und Lüftungsgittern; orangefarbene Akzente (Stoßfänger, Radkästen,
    /// Kopfplatte); breites Glasvisier mit ausdrucksstarken Leuchtaugen samt Lidern (freudig, traurig, neugierig);
    /// drei robuste Räder mit Profil, Felgen und sichtbarer Federung; faltbarer Sammelarm mit Gelenken, Hydraulik und
    /// Kabeln; sichtbarer Müllbehälter mit Rippen und Füllung. Lack mit Kratzern, Kantenabrieb und Schmutz kommt aus
    /// dem Oberflächen-Shader. Upgrades (Behältergröße, Anbauteile) und Kosmetik (Farbe, Akzent, Aufkleber, Aufsatz)
    /// sind am Modell sichtbar; Scheinwerfer leuchten bei Dunkelheit. Stimmungen und Gesten (Winken, Freudensprung,
    /// Kopfneigen, Zittern, Gähnen): MikoFace.cs.
    /// </summary>
    public partial class RobotModel : MonoBehaviour
    {
        Transform root, body, head, visor, eyeL, eyeR, bin, binFill, arm1, arm2, tip, stickerQuad;
        readonly Transform[] wheels = new Transform[3];
        readonly Transform[] lidUp = new Transform[2], lidDown = new Transform[2];
        Material bodyMat, accentMat, eyeMat, visorMat, fillMat, stickerMat, lampMat;
        readonly Dictionary<string, GameObject> heads = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> extras = new Dictionary<string, GameObject>();
        readonly List<Mesh> ownMeshes = new List<Mesh>();
        GameObject cosAttach;
        string curAttach, curSticker, curColor, curAccent;
        public Light Headlight;
        Transform saw;

        float arm = 0f, armTarget, blinkT = 2f, blink, wheelRot, bob, emoteT;
        string emoteKind;
        Color eyeBase = new Color(0.35f, 0.95f, 1f);
        Color eyeColor;
        Vector3 lookLocal;
        float lidLower, lidRaise, lidTilt, lidAsym;

        static Material sDark, sSteel, sRubber, sChrome, sFrame;

        // Eigene 4×4-Farbpalette je Roboter: alle festen Farben und die Kosmetikfarben (Hülle, Akzent) teilen sich
        // ein mattes und ein metallisches Material – wenige Draw-Calls je Baugruppe. Alpha = Glätte.
        const int SBody = 0, SAccent = 1, SDark = 2, SRubber = 3, SRed = 4, SFill = 5, SFill0 = 6, SSteel = 12, SChrome = 13, SFrame = 14, SWhite = 15;
        static readonly Color[] SlotCol =
        {
            Mats.C(0x2EC4B6), Mats.C(0xFF8C2E), new Color(0.13f, 0.14f, 0.16f), new Color(0.07f, 0.07f, 0.075f),
            new Color(0.85f, 0.2f, 0.15f), new Color(0.55f, 0.5f, 0.42f), new Color(0.55f, 0.5f, 0.42f), new Color(0.35f, 0.5f, 0.62f),
            new Color(0.7f, 0.35f, 0.25f), new Color(0.45f, 0.55f, 0.35f), new Color(0.75f, 0.72f, 0.6f), new Color(0.5f, 0.5f, 0.5f),
            new Color(0.62f, 0.65f, 0.68f), new Color(0.8f, 0.82f, 0.85f), new Color(0.22f, 0.24f, 0.27f), new Color(0.9f, 0.9f, 0.88f),
        };
        static readonly float[] SlotGloss = { 0.55f, 0.5f, 0.4f, 0.3f, 0.35f, 0.25f, 0.3f, 0.45f, 0.3f, 0.3f, 0.3f, 0.3f, 0.6f, 0.85f, 0.45f, 0.4f };
        static readonly int[] SlotSurf =
        {
            SurfKind.Paint, SurfKind.Paint, SurfKind.Paint, SurfKind.Rubber, SurfKind.Rubber, SurfKind.Generic, SurfKind.Generic, SurfKind.Generic,
            SurfKind.Generic, SurfKind.Generic, SurfKind.Generic, SurfKind.Generic, SurfKind.Paint, SurfKind.Generic, SurfKind.Paint, SurfKind.Paint,
        };
        Texture2D palTex;
        Material palMat, palMetal;

        void InitPalette()
        {
            palTex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "MIKO_Palette" };
            for (int i = 0; i < 16; i++) SetSlot(i, SlotCol[i], false);
            palTex.Apply(false);
            palMat = SurfaceLook.Lit("MIKO_Lack", Color.white, SurfaceLook.Custom ? 1f : 0.5f, 0.05f, palTex);
            palMetal = SurfaceLook.Lit("MIKO_Metall", Color.white, SurfaceLook.Custom ? 1f : 0.6f, 0.8f, palTex);
        }

        void SetSlot(int slot, Color c, bool apply = true)
        {
            c.a = SlotGloss[slot];
            palTex.SetPixel(slot % 4, slot / 4, c);
            if (apply) palTex.Apply(false);
        }

        /// <summary>MeshBuilder für einen Platz (Farbe, Glätte, Oberflächenklasse) der Roboter-Palette.</summary>
        MeshBuilder P(MultiBuilder mb, int slot)
        {
            var b = mb.For(slot >= SSteel && slot != SWhite ? palMetal : palMat);
            b.FixedUV = new Vector2((slot % 4 + 0.5f) / 4f, (slot / 4 + 0.5f) / 4f);
            b.Surf = SlotSurf[slot];
            return b;
        }

        static Material Mat(Color c) { return Mats.Get(Mats.Opaque, c); }

        public static RobotModel Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var r = go.AddComponent<RobotModel>();
            r.Build();
            return r;
        }

        void OnDestroy()
        {
            foreach (var m in ownMeshes) if (m != null) Destroy(m);
            ownMeshes.Clear();
            if (palTex != null) Destroy(palTex);
            if (palMat != null) Destroy(palMat);
            if (palMetal != null) Destroy(palMetal);
        }

        Transform Part(Transform parent, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Vector3 euler, string n = "part", bool shadows = true)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        Transform Node(Transform parent, string n, Vector3 pos)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        /// <summary>
        /// Starre Baugruppe als ein Mesh mit einem Untermesh je Material. groundY: Bodenhöhe im lokalen Raum der
        /// Gruppe (Schmutz nach unten hin).
        /// </summary>
        Transform Group(Transform parent, string n, Vector3 pos, float groundY, System.Action<MultiBuilder> build, bool shadows = true)
        {
            var mb = new MultiBuilder { GroundY = groundY, GrimeHeight = 0.75f };
            build(mb);
            var go = mb.BuildCombined(n, parent, shadows);
            go.transform.localPosition = pos;
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) ownMeshes.Add(mf.sharedMesh);
            return go.transform;
        }

        static Material Lit(string n, Color c, float gloss, float metal, int surf)
        {
            var m = SurfaceLook.Lit(n, c, gloss, metal);
            Mats.SetSurf(m, surf);
            return m;
        }

        static void SharedMats()
        {
            if (sDark != null) return;
            // Feste Farben laufen über die Farbpalette (wenige Draw-Calls), nur Hülle/Akzent bleiben eigenständig (Kosmetik)
            sDark = Mats.Surface(SurfKind.Paint, new Color(0.13f, 0.14f, 0.16f), 0.4f);
            sSteel = Mats.Surface(SurfKind.Paint, new Color(0.62f, 0.65f, 0.68f), 0.6f, true);
            sRubber = Mats.Surface(SurfKind.Rubber, new Color(0.07f, 0.07f, 0.075f), 0.3f);
            sChrome = Mats.Surface(SurfKind.Generic, new Color(0.8f, 0.82f, 0.85f), 0.85f, true);
            sFrame = Mats.Surface(SurfKind.Paint, new Color(0.22f, 0.24f, 0.27f), 0.45f, true);
        }

        void Build()
        {
            SharedMats();
            InitPalette();
            bodyMat = Lit("MIKO_Huelle", Mats.C(0x2EC4B6), 0.55f, 0.05f, SurfKind.Paint);
            accentMat = Lit("MIKO_Akzent", Mats.C(0xFF8C2E), 0.5f, 0.05f, SurfKind.Paint);
            eyeMat = Mats.Unique(Mats.Emissive, eyeBase);
            Mats.SetEmission(eyeMat, eyeBase * 2.2f);
            visorMat = Mats.Unique(Mats.Emissive, new Color(0.03f, 0.05f, 0.07f));
            Mats.SetEmission(visorMat, new Color(0.01f, 0.07f, 0.1f));
            try { visorMat.SetFloat("_Glossiness", 0.96f); } catch { }
            lampMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.95f, 0.85f));
            Mats.SetEmission(lampMat, new Color(1f, 0.9f, 0.7f) * 0.2f);
            fillMat = Lit("MIKO_Fuellung", new Color(0.55f, 0.5f, 0.42f), 0.25f, 0f, SurfKind.Generic);
            var dark = sDark; var steel = sSteel; var rubber = sRubber;

            root = Node(transform, "root", Vector3.zero);
            BuildChassis(dark, steel, rubber);
            BuildBody(dark, steel, rubber);
            BuildHead(dark, steel, rubber);
            BuildBin(dark, steel);
            BuildArm(dark, steel, rubber);
            BuildHeads(dark, steel);
            BuildExtras(dark, steel);
        }

        // ------------------------------------------------------------------ Fahrwerk
        void BuildChassis(Material dark, Material steel, Material rubber)
        {
            Group(root, "chassis", Vector3.zero, 0f, mb =>
            {
                // Rahmen mit Unterfahrschutz, Achsträger hinten, Gabel vorn
                P(mb, SFrame).BevelBox(new Vector3(0, 0.3f, -0.05f), new Vector3(0.62f, 0.14f, 0.92f), 0.03f);
                P(mb, SSteel).BevelBox(new Vector3(0, 0.215f, 0.0f), new Vector3(0.5f, 0.03f, 0.75f), 0.01f);
                P(mb, SSteel).CylinderX(new Vector3(0, 0.28f, -0.32f), 0.045f, 0.84f, 10);
                foreach (var sx in new[] { -1f, 1f })
                {
                    // Schwinge und Achslager
                    P(mb, SDark).BevelBox(new Vector3(sx * 0.33f, 0.28f, -0.25f), new Vector3(0.05f, 0.1f, 0.3f), 0.015f);
                    P(mb, SSteel).CylinderX(new Vector3(sx * 0.37f, 0.28f, -0.32f), 0.07f, 0.06f, 12);
                    // Radkasten-Kotflügel (orange Bögen)
                    ArcBand(mb, SAccent, new Vector3(sx * 0.47f, 0.28f, -0.32f), 0.335f, 15f, 170f, 0.2f, 0.035f, 9);
                    // Federbein (Dämpfer) zwischen Rahmen und Karosserie
                    P(mb, SSteel).Tube(new Vector3(sx * 0.3f, 0.3f, -0.12f), new Vector3(sx * 0.3f, 0.47f, -0.12f), 0.018f, 6);
                    P(mb, SDark).Tube(new Vector3(sx * 0.3f, 0.3f, -0.12f), new Vector3(sx * 0.3f, 0.39f, -0.12f), 0.03f, 8);
                }
                // Gabel vorn mit Schutzblech und Nabe
                foreach (var sx in new[] { -1f, 1f })
                {
                    P(mb, SDark).BevelBoxRot(new Vector3(sx * 0.1f, 0.3f, 0.37f), new Vector3(0.035f, 0.26f, 0.08f), new Vector3(-20, 0, 0), 0.012f);
                    P(mb, SSteel).Tube(new Vector3(sx * 0.1f, 0.3f, 0.32f), new Vector3(sx * 0.1f, 0.44f, 0.28f), 0.016f, 6);
                }
                P(mb, SSteel).CylinderX(new Vector3(0, 0.22f, 0.45f), 0.03f, 0.24f, 8);
                ArcBand(mb, SAccent, new Vector3(0, 0.22f, 0.45f), 0.27f, 15f, 150f, 0.17f, 0.03f, 7);
                // Batteriekasten unten mit Kühlrippen und Kabel
                P(mb, SDark).BevelBox(new Vector3(0, 0.22f, -0.05f), new Vector3(0.36f, 0.08f, 0.4f), 0.015f);
                for (int k = 0; k < 5; k++) P(mb, SSteel).Box(new Vector3(-0.12f + k * 0.06f, 0.18f, -0.05f), new Vector3(0.015f, 0.02f, 0.38f));
                P(mb, SRubber).Tube(new Vector3(0.15f, 0.25f, 0.12f), new Vector3(0.24f, 0.36f, 0.25f), 0.014f, 5);
            });
            // Räder: Rundprofil-Reifen mit Stollen, Felge, Nabenkappe, Radmuttern
            wheels[0] = Node(root, "wheelL", new Vector3(-0.47f, 0.28f, -0.32f));
            wheels[1] = Node(root, "wheelR", new Vector3(0.47f, 0.28f, -0.32f));
            wheels[2] = Node(root, "wheelF", new Vector3(0, 0.22f, 0.45f));
            for (int i = 0; i < 3; i++)
            {
                float r = i < 2 ? 0.28f : 0.22f, w = i < 2 ? 0.17f : 0.14f;
                int side = i == 0 ? -1 : 1;
                Group(wheels[i], "wheel", Vector3.zero, float.NaN, mb => Wheel(mb, r, w, side, rubber));
            }
        }

        /// <summary>Gebogenes Band (Kotflügel) um die X-Achse: Bogen von a0 bis a1 (Grad, 0 = vorn, 90 = oben).</summary>
        void ArcBand(MultiBuilder mb, int slot, Vector3 c, float radius, float a0, float a1, float width, float thick, int seg)
        {
            for (int k = 0; k < seg; k++)
            {
                float t0 = Mathf.Lerp(a0, a1, k / (float)seg) * Mathf.Deg2Rad, t1 = Mathf.Lerp(a0, a1, (k + 1) / (float)seg) * Mathf.Deg2Rad;
                var p0 = c + new Vector3(0, Mathf.Sin(t0) * radius, Mathf.Cos(t0) * radius);
                var p1 = c + new Vector3(0, Mathf.Sin(t1) * radius, Mathf.Cos(t1) * radius);
                float am = (t0 + t1) * 0.5f;
                P(mb, slot).BevelBoxRot((p0 + p1) * 0.5f, new Vector3(width, thick, (p1 - p0).magnitude * 1.08f), new Vector3(90f - am * Mathf.Rad2Deg, 0, 0), thick * 0.35f);
            }
        }

        void Wheel(MultiBuilder mb, float r, float w, int side, Material rubber)
        {
            // Reifen um die X-Achse (Lathe läuft um Y → um 90° gedreht)
            var o = mb.M;
            mb.M = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one);
            float hw = w * 0.5f;
            P(mb, SRubber).Lathe(Vector3.zero, new[]
            {
                new Vector2(r * 0.66f, -hw), new Vector2(r * 0.86f, -hw - 0.004f), new Vector2(r * 0.97f, -hw + 0.012f), new Vector2(r * 0.995f, -hw * 0.5f),
                new Vector2(r * 0.995f, hw * 0.5f), new Vector2(r * 0.97f, hw - 0.012f), new Vector2(r * 0.86f, hw + 0.004f), new Vector2(r * 0.66f, hw),
            }, 28);
            mb.M = o;
            // Profilstollen (versetzt, Pfeilprofil)
            int n = 20;
            for (int k = 0; k < n; k++)
            {
                float a = k * 360f / n;
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = Quaternion.Euler(a + (s > 0 ? 360f / n * 0.5f : 0f), 0, 0) * new Vector3(s * w * 0.22f, 0, r + 0.008f);
                    P(mb, SRubber).BoxRot(p, new Vector3(w * 0.36f, 0.022f, 0.055f), new Vector3(a + (s > 0 ? 360f / n * 0.5f : 0f), s * 18f, 0));
                }
            }
            // Felge, Speichen, Nabenkappe, Radmuttern (außen)
            P(mb, SSteel).CylinderX(Vector3.zero, r * 0.66f, w * 0.86f, 20);
            P(mb, SDark).CylinderX(new Vector3(side * w * 0.44f, 0, 0), r * 0.5f, 0.012f, 20);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f;
                P(mb, SSteel).BoxRot(new Vector3(side * w * 0.45f, 0, 0) + Quaternion.Euler(a, 0, 0) * new Vector3(0, r * 0.3f, 0), new Vector3(0.02f, r * 0.42f, 0.035f), new Vector3(a, 0, 0));
            }
            P(mb, SAccent).CylinderX(new Vector3(side * w * 0.48f, 0, 0), r * 0.26f, 0.03f, 16);
            P(mb, SChrome).CylinderX(new Vector3(side * w * 0.5f, 0, 0), r * 0.1f, 0.03f, 10);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f + 36f;
                P(mb, SChrome).BevelBox(new Vector3(side * w * 0.5f, 0, 0) + Quaternion.Euler(a, 0, 0) * new Vector3(0, r * 0.18f, 0), new Vector3(0.02f, 0.018f, 0.018f), 0.004f);
            }
        }

        // ------------------------------------------------------------------ Karosserie
        void BuildBody(Material dark, Material steel, Material rubber)
        {
            // Karosserie (gefedert)
            body = Node(root, "body", new Vector3(0, 0.62f, 0));
            Group(body, "hull", Vector3.zero, -0.62f, mb =>
            {
                // Hauptschale mit gerundeten Kanten, schräger Bug, Unterboden
                P(mb, SBody).BevelBox(new Vector3(0, 0.02f, 0.0f), new Vector3(0.9f, 0.46f, 0.9f), 0.07f);
                P(mb, SBody).BevelBoxRot(new Vector3(0, -0.02f, 0.47f), new Vector3(0.84f, 0.4f, 0.14f), new Vector3(-12, 0, 0), 0.05f);
                P(mb, SDark).BevelBox(new Vector3(0, -0.2f, 0.02f), new Vector3(0.82f, 0.08f, 0.86f), 0.02f);
                // Plattenfugen und Schrauben an den Flanken
                foreach (var sx in new[] { -1f, 1f })
                {
                    float fx = sx * 0.452f;
                    P(mb, SDark).Box(new Vector3(fx, 0.02f, -0.18f), new Vector3(0.006f, 0.34f, 0.01f));
                    P(mb, SDark).Box(new Vector3(fx, 0.02f, 0.2f), new Vector3(0.006f, 0.34f, 0.01f));
                    P(mb, SDark).Box(new Vector3(fx, 0.16f, 0.01f), new Vector3(0.006f, 0.01f, 0.76f));
                    foreach (var z in new[] { -0.36f, -0.22f, 0.24f, 0.38f })
                        foreach (var y in new[] { -0.1f, 0.13f })
                            P(mb, SChrome).BevelBox(new Vector3(fx + sx * 0.004f, y, z), new Vector3(0.012f, 0.026f, 0.026f), 0.005f);
                    // Akzentstreifen mit Zierleiste
                    P(mb, SAccent).BevelBox(new Vector3(sx * 0.455f, 0.03f, 0.02f), new Vector3(0.025f, 0.075f, 0.78f), 0.01f);
                    // Lüftungsgitter
                    P(mb, SDark).BevelBox(new Vector3(sx * 0.455f, -0.08f, 0.0f), new Vector3(0.02f, 0.12f, 0.26f), 0.008f);
                    for (int k = 0; k < 5; k++) P(mb, SSteel).BoxRot(new Vector3(sx * 0.462f, -0.08f, -0.1f + k * 0.05f), new Vector3(0.012f, 0.1f, 0.012f), new Vector3(0, 0, sx * 20f));
                }
                // Stoßfänger mit Gummileiste, Abschlepphaken, Frontscheinwerfer
                P(mb, SAccent).BevelBox(new Vector3(0, -0.2f, 0.55f), new Vector3(0.88f, 0.11f, 0.1f), 0.035f);
                P(mb, SRubber).Box(new Vector3(0, -0.2f, 0.605f), new Vector3(0.7f, 0.035f, 0.02f));
                P(mb, SSteel).TorusRot(new Vector3(0.28f, -0.27f, 0.6f), new Vector3(0, 90, 0), 0.03f, 0.009f, 10, 4);
                foreach (var sx in new[] { -0.3f, 0.3f })
                {
                    P(mb, SFrame).BevelBoxRot(new Vector3(sx, 0.12f, 0.535f), new Vector3(0.17f, 0.05f, 0.05f), new Vector3(-12, 0, 0), 0.015f);
                    mb.For(lampMat).BoxRot(new Vector3(sx, 0.12f, 0.558f), new Vector3(0.14f, 0.025f, 0.01f), new Vector3(-12, 0, 0));
                }
                // Hals: Drehkranz, Faltenbalg, Kabelstränge zum Kopf
                P(mb, SDark).Cylinder(new Vector3(0, 0.25f, 0.12f), 0.2f, 0.03f, 20);
                P(mb, SSteel).Cylinder(new Vector3(0, 0.26f, 0.12f), 0.08f, 0.14f, 12);
                for (int k = 0; k < 3; k++) P(mb, SRubber).Torus(new Vector3(0, 0.29f + k * 0.035f, 0.12f), 0.095f, 0.018f, 14, 4);
                P(mb, SRubber).Tube(new Vector3(-0.14f, 0.24f, 0.0f), new Vector3(-0.12f, 0.34f, 0.08f), 0.014f, 5);
                P(mb, SRed).Tube(new Vector3(0.13f, 0.24f, 0.02f), new Vector3(0.11f, 0.34f, 0.1f), 0.011f, 5);
                // Schulterlager für den Sammelarm (rechts) mit Hydraulikleitung
                P(mb, SDark).BevelBox(new Vector3(0.44f, 0.12f, 0.25f), new Vector3(0.06f, 0.18f, 0.2f), 0.02f);
                P(mb, SRubber).Tube(new Vector3(0.43f, 0.2f, 0.12f), new Vector3(0.49f, 0.18f, 0.22f), 0.012f, 5);
                // Status-LEDs am Heck
                for (int k = 0; k < 3; k++)
                    mb.For(Mats.Get(Mats.Emissive, new Color(0.3f, 1f, 0.5f), new Color(0.4f, 1.6f, 0.6f))).Box(new Vector3(-0.2f + k * 0.05f, 0.12f, -0.455f), new Vector3(0.025f, 0.025f, 0.01f));
            });
        }

        // ------------------------------------------------------------------ Kopf mit Visier und Augen
        void BuildHead(Material dark, Material steel, Material rubber)
        {
            head = Node(body, "head", new Vector3(0, 0.46f, 0.18f));
            Group(head, "headShell", Vector3.zero, -1.08f, mb =>
            {
                // Schale: gerundet, hinten schmaler, Visierrahmen vorn
                P(mb, SBody).BevelBox(new Vector3(0, 0, -0.02f), new Vector3(0.84f, 0.36f, 0.38f), 0.09f);
                P(mb, SFrame).BevelBox(new Vector3(0, -0.01f, 0.16f), new Vector3(0.8f, 0.29f, 0.06f), 0.03f);
                // Kopfplatte (Akzent) mit Sensorleiste
                P(mb, SAccent).BevelBox(new Vector3(0, 0.185f, -0.02f), new Vector3(0.6f, 0.035f, 0.3f), 0.015f);
                P(mb, SDark).BevelBox(new Vector3(0, 0.205f, 0.06f), new Vector3(0.22f, 0.03f, 0.08f), 0.01f);
                mb.For(lampMat).Box(new Vector3(0, 0.205f, 0.101f), new Vector3(0.18f, 0.012f, 0.006f));
                // Seitliche Sensor-„Ohren“ mit Akzentring und Linse
                foreach (var sx in new[] { -1f, 1f })
                {
                    P(mb, SFrame).CylinderX(new Vector3(sx * 0.43f, 0, -0.02f), 0.1f, 0.04f, 18);
                    P(mb, SAccent).CylinderX(new Vector3(sx * 0.452f, 0, -0.02f), 0.075f, 0.012f, 18);
                    P(mb, SSteel).CylinderX(new Vector3(sx * 0.46f, 0, -0.02f), 0.04f, 0.012f, 12);
                    for (int k = 0; k < 3; k++) P(mb, SDark).Box(new Vector3(sx * 0.425f, -0.13f + k * 0.03f, -0.12f), new Vector3(0.012f, 0.012f, 0.08f));
                }
                // kurze Antenne mit Kugel
                P(mb, SSteel).Cylinder(new Vector3(-0.28f, 0.18f, -0.12f), 0.012f, 0.14f, 6);
                P(mb, SAccent).Sphere(new Vector3(-0.28f, 0.33f, -0.12f), 0.022f, 8, 5);
                // Schrauben am Visierrahmen
                foreach (var sx in new[] { -0.37f, 0.37f })
                    foreach (var sy in new[] { -0.12f, 0.1f })
                        P(mb, SChrome).BevelBox(new Vector3(sx, sy, 0.192f), new Vector3(0.022f, 0.022f, 0.01f), 0.004f);
            });
            // Glasvisier (leicht gewölbt wirkend durch Fasen), eigene Ebene
            visor = Group(head, "visor", Vector3.zero, float.NaN, mb => mb.For(visorMat).BevelBox(new Vector3(0, -0.01f, 0.19f), new Vector3(0.72f, 0.23f, 0.03f), 0.012f), false);
            eyeL = Part(head, MeshKit.Sphere, eyeMat, new Vector3(-0.17f, 0f, 0.225f), new Vector3(0.15f, 0.1f, 0.03f), Vector3.zero, "eyeL", false);
            eyeR = Part(head, MeshKit.Sphere, eyeMat, new Vector3(0.17f, 0f, 0.225f), new Vector3(0.15f, 0.1f, 0.03f), Vector3.zero, "eyeR", false);
            // Lider (Visierfarbe, vor den Augen): oben senken für traurig/müde, unten heben für freudig
            // Einheits-Lider: Drehpunkt an der Augenkante, Höhe über die Skalierung (0 = unsichtbar)
            var lidUpMesh = MeshKit.Get("mikoLidUp", b => b.Box(new Vector3(0, -0.5f, 0), new Vector3(0.2f, 1f, 0.008f)));
            var lidDnMesh = MeshKit.Get("mikoLidDn", b => b.Box(new Vector3(0, 0.5f, 0), new Vector3(0.2f, 1f, 0.008f)));
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.17f : 0.17f;
                lidUp[i] = Part(head, lidUpMesh, visorMat, new Vector3(x, 0.056f, 0.247f), new Vector3(1, 0.001f, 1), Vector3.zero, "lidUp", false);
                lidDown[i] = Part(head, lidDnMesh, visorMat, new Vector3(x, -0.056f, 0.247f), new Vector3(1, 0.001f, 1), Vector3.zero, "lidDown", false);
            }
            var hl = Node(head, "headlight", new Vector3(0, 0.12f, 0.2f));
            Headlight = hl.gameObject.AddComponent<Light>();
            Headlight.type = LightType.Spot;
            Headlight.spotAngle = 70f;
            Headlight.innerSpotAngle = 30f;
            Headlight.range = 22f;
            Headlight.intensity = 0f;
            Headlight.color = new Color(1f, 0.93f, 0.82f);
            Headlight.shadows = LightShadows.None;
            Headlight.renderMode = LightRenderMode.ForcePixel;
            hl.localRotation = Quaternion.Euler(14, 0, 0);
        }

        // ------------------------------------------------------------------ Rückenbehälter
        void BuildBin(Material dark, Material steel)
        {
            // Rückenbehälter (Größe wächst mit Upgrades)
            bin = Node(body, "bin", new Vector3(0, 0.36f, -0.36f));
            Group(bin, "binShell", Vector3.zero, -0.98f, mb =>
            {
                P(mb, SSteel).BevelBox(new Vector3(0, -0.02f, 0), new Vector3(0.78f, 0.04f, 0.5f), 0.01f);
                P(mb, SBody).BevelBox(new Vector3(-0.38f, 0.22f, 0), new Vector3(0.045f, 0.46f, 0.52f), 0.015f);
                P(mb, SBody).BevelBox(new Vector3(0.38f, 0.22f, 0), new Vector3(0.045f, 0.46f, 0.52f), 0.015f);
                P(mb, SBody).BevelBox(new Vector3(0, 0.22f, -0.25f), new Vector3(0.8f, 0.46f, 0.045f), 0.015f);
                P(mb, SBody).BevelBox(new Vector3(0, 0.17f, 0.25f), new Vector3(0.8f, 0.36f, 0.045f), 0.015f);
                // Rippen außen, Oberkante mit Akzentleiste, Griffe
                foreach (var sx in new[] { -1f, 1f })
                    for (int k = 0; k < 3; k++)
                        P(mb, SBody).BevelBox(new Vector3(sx * 0.405f, 0.22f, -0.16f + k * 0.16f), new Vector3(0.02f, 0.42f, 0.035f), 0.008f);
                for (int k = 0; k < 4; k++) P(mb, SBody).BevelBox(new Vector3(-0.27f + k * 0.18f, 0.22f, -0.275f), new Vector3(0.035f, 0.42f, 0.02f), 0.008f);
                P(mb, SAccent).BevelBox(new Vector3(0, 0.46f, -0.25f), new Vector3(0.84f, 0.04f, 0.07f), 0.012f);
                P(mb, SAccent).BevelBox(new Vector3(-0.38f, 0.46f, 0), new Vector3(0.07f, 0.04f, 0.56f), 0.012f);
                P(mb, SAccent).BevelBox(new Vector3(0.38f, 0.46f, 0), new Vector3(0.07f, 0.04f, 0.56f), 0.012f);
                foreach (var sx in new[] { -0.25f, 0.25f })
                {
                    P(mb, SSteel).Tube(new Vector3(sx - 0.06f, 0.38f, -0.29f), new Vector3(sx - 0.06f, 0.38f, -0.33f), 0.012f, 5);
                    P(mb, SSteel).Tube(new Vector3(sx + 0.06f, 0.38f, -0.29f), new Vector3(sx + 0.06f, 0.38f, -0.33f), 0.012f, 5);
                    P(mb, SSteel).Tube(new Vector3(sx - 0.06f, 0.38f, -0.33f), new Vector3(sx + 0.06f, 0.38f, -0.33f), 0.012f, 5);
                }
                // Recyclingzeichen als Prägung (drei Pfeile) auf der Rückwand
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 120f;
                    var c = new Vector3(0, 0.22f, -0.276f) + Quaternion.Euler(0, 0, a) * new Vector3(0, 0.07f, 0);
                    P(mb, SAccent).BoxRot(c, new Vector3(0.09f, 0.018f, 0.008f), new Vector3(0, 0, a));
                    P(mb, SAccent).BoxRot(c + Quaternion.Euler(0, 0, a) * new Vector3(0.045f, 0, 0), new Vector3(0.03f, 0.03f, 0.008f), new Vector3(0, 0, a + 45));
                }
                // Scharnier und nach hinten weggeklappter Deckel
                P(mb, SSteel).CylinderX(new Vector3(0, 0.47f, -0.28f), 0.018f, 0.7f, 8);
                P(mb, SBody).BevelBoxRot(new Vector3(0, 0.42f, -0.31f), new Vector3(0.72f, 0.018f, 0.12f), new Vector3(-80, 0, 0), 0.006f);
            });
            // Füllung aus Müllbrocken (wird mit dem Füllstand skaliert)
            binFill = Group(bin, "fill", new Vector3(0, 0.02f, 0), float.NaN, mb =>
            {
                var cols = new[] { new Color(0.55f, 0.5f, 0.42f), new Color(0.35f, 0.5f, 0.62f), new Color(0.7f, 0.35f, 0.25f), new Color(0.45f, 0.55f, 0.35f), new Color(0.75f, 0.72f, 0.6f) };
                P(mb, SFill).Box(new Vector3(0, 0.5f, 0), new Vector3(0.72f, 1f, 0.44f));
                for (int k = 0; k < 14; k++)
                {
                    float x = -0.28f + (k % 5) * 0.14f + MeshBuilder.Hash01(k, 1, 5) * 0.05f, z = -0.15f + (k / 5) * 0.14f;
                    P(mb, SFill0 + k % 5).Crumple(new Vector3(x, 0.98f, z), 0.07f + MeshBuilder.Hash01(k, 2, 5) * 0.04f, 0.9f, k, 0.3f, 6, 4);
                }
            }, false);
            stickerMat = Mats.Unique(Mats.UnlitTransparent, Color.white);
            stickerQuad = Part(bin, MeshKit.Quad, stickerMat, new Vector3(0.418f, 0.24f, 0), new Vector3(0.28f, 0.28f, 1), new Vector3(0, -90, 0), "sticker", false);
            stickerQuad.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Sammelarm
        void BuildArm(Material dark, Material steel, Material rubber)
        {
            // Faltbarer Sammelarm (rechts): Schultergelenk, Oberarm mit Hydraulikzylinder, Ellbogen, Unterarm, Handgelenk
            arm1 = Node(body, "arm1", new Vector3(0.5f, 0.12f, 0.25f));
            Group(arm1, "upper", Vector3.zero, float.NaN, mb =>
            {
                P(mb, SFrame).CylinderX(Vector3.zero, 0.085f, 0.1f, 16);
                P(mb, SAccent).CylinderX(new Vector3(0.055f, 0, 0), 0.055f, 0.02f, 16);
                P(mb, SChrome).CylinderX(new Vector3(0.068f, 0, 0), 0.02f, 0.012f, 8);
                P(mb, SBody).BevelBox(new Vector3(0, 0, 0.25f), new Vector3(0.1f, 0.11f, 0.42f), 0.03f);
                P(mb, SDark).Box(new Vector3(0.051f, 0, 0.25f), new Vector3(0.006f, 0.05f, 0.3f));
                // Hydraulik: Zylinder + blanke Kolbenstange unter dem Oberarm
                P(mb, SDark).Tube(new Vector3(0, -0.075f, 0.06f), new Vector3(0, -0.075f, 0.3f), 0.024f, 8, true);
                P(mb, SChrome).Tube(new Vector3(0, -0.075f, 0.3f), new Vector3(0, -0.07f, 0.46f), 0.012f, 6, true);
                P(mb, SRubber).Tube(new Vector3(-0.04f, 0.05f, 0.05f), new Vector3(-0.045f, 0.06f, 0.44f), 0.01f, 5);
            });
            arm2 = Node(arm1, "arm2", new Vector3(0, 0, 0.5f));
            Group(arm2, "fore", Vector3.zero, float.NaN, mb =>
            {
                P(mb, SFrame).CylinderX(Vector3.zero, 0.065f, 0.12f, 14);
                P(mb, SChrome).CylinderX(new Vector3(0.062f, 0, 0), 0.02f, 0.01f, 8);
                P(mb, SAccent).BevelBox(new Vector3(0, 0, 0.22f), new Vector3(0.085f, 0.085f, 0.38f), 0.025f);
                P(mb, SDark).Box(new Vector3(0, 0.044f, 0.22f), new Vector3(0.04f, 0.006f, 0.28f));
                P(mb, SFrame).Cylinder(new Vector3(0, 0, 0.42f), 0.055f, 0.04f, 14); // Handgelenk-Ring (Z-Achse egal: Ring um Y)
                P(mb, SSteel).CylinderZ(new Vector3(0, 0, 0.44f), 0.05f, 0.05f, 14);
                P(mb, SRubber).Tube(new Vector3(-0.045f, 0.03f, 0.02f), new Vector3(-0.045f, 0.03f, 0.4f), 0.009f, 5);
            });
            tip = Node(arm2, "tip", new Vector3(0, 0, 0.46f));
        }

        void BuildHeads(Material dark, Material steel)
        {
            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            // Greifklaue: drei Finger mit Gelenken
            var g = Node(tip, "head_grab", Vector3.zero).gameObject;
            Group(g.transform, "claw", Vector3.zero, float.NaN, mb =>
            {
                P(mb, SFrame).CylinderZ(new Vector3(0, 0, 0.02f), 0.05f, 0.04f, 12);
                for (int k = 0; k < 3; k++)
                {
                    var q = Quaternion.Euler(0, 0, k * 120f);
                    var a = q * new Vector3(0, 0.035f, 0.04f);
                    var b = q * new Vector3(0, 0.06f, 0.11f);
                    var c = q * new Vector3(0, 0.035f, 0.17f);
                    P(mb, SSteel).Tube(a, b, 0.012f, 6, true);
                    P(mb, SDark).Tube(b, c, 0.01f, 6, true, 0.006f);
                    P(mb, SAccent).Sphere(b, 0.016f, 6, 4);
                }
            });
            heads["grab"] = g;
            // Sauger-Düse
            var v = Node(tip, "head_vacuum", Vector3.zero).gameObject;
            Part(v.transform, cyl, steel, new Vector3(0, 0, 0.1f), new Vector3(0.12f, 0.2f, 0.12f), new Vector3(90, 0, 0), "nozzle");
            Part(v.transform, MeshKit.Get("mikoBellows", b => { for (int k = 0; k < 4; k++) b.Torus(new Vector3(0, -0.35f + k * 0.2f, 0), 0.5f, 0.12f, 12, 4); }), sRubber, new Vector3(0, 0, 0.08f), new Vector3(0.12f, 0.16f, 0.12f), new Vector3(90, 0, 0), "bellows");
            Part(v.transform, cyl, accentMat, new Vector3(0, 0, 0.21f), new Vector3(0.2f, 0.03f, 0.2f), new Vector3(90, 0, 0), "mouth");
            heads["vacuum"] = v;
            // Magnetscheibe
            var m = Node(tip, "head_magnet", Vector3.zero).gameObject;
            Part(m.transform, cyl, Mat(new Color(0.75f, 0.12f, 0.1f)), new Vector3(0, 0, 0.06f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90, 0, 0), "disc");
            Part(m.transform, cyl, steel, new Vector3(0, 0, 0.1f), new Vector3(0.22f, 0.02f, 0.22f), new Vector3(90, 0, 0), "face");
            Part(m.transform, MeshKit.Get("mikoMagRing", b => b.Torus(Vector3.zero, 0.5f, 0.06f, 20, 4)), Mats.Get(Mats.Emissive, new Color(0.4f, 0.7f, 1f), new Color(0.3f, 0.6f, 1.4f)), new Vector3(0, 0, 0.095f), new Vector3(0.3f, 0.3f, 0.3f), new Vector3(90, 0, 0), "coil", false);
            heads["magnet"] = m;
            // Kreissäge mit Schutzhaube
            var c2 = Node(tip, "head_cutter", Vector3.zero).gameObject;
            Part(c2.transform, cube, dark, new Vector3(0, 0, 0.05f), new Vector3(0.06f, 0.08f, 0.12f), Vector3.zero, "mount");
            saw = Part(c2.transform, MeshKit.Get("mikoSaw", b => { b.Cylinder(new Vector3(0, -0.5f, 0), 0.5f, 1f, 24); for (int k = 0; k < 16; k++) { float a = k * 22.5f * Mathf.Deg2Rad; b.BoxRot(new Vector3(Mathf.Cos(a) * 0.52f, 0, Mathf.Sin(a) * 0.52f), new Vector3(0.1f, 0.9f, 0.08f), new Vector3(0, -k * 22.5f + 30, 0)); } }), Mats.Get(Mats.Metal, new Color(0.85f, 0.85f, 0.88f)), new Vector3(0, 0.02f, 0.14f), new Vector3(0.28f, 0.01f, 0.28f), new Vector3(0, 0, 90), "blade");
            Part(c2.transform, sph, accentMat, new Vector3(0.02f, 0.06f, 0.12f), new Vector3(0.03f, 0.12f, 0.2f), Vector3.zero, "guard");
            heads["cutter"] = c2;
            // Wärmespirale
            var h = Node(tip, "head_heat", Vector3.zero).gameObject;
            var hm = Mats.Unique(Mats.Emissive, new Color(1f, 0.45f, 0.1f));
            Mats.SetEmission(hm, new Color(2f, 0.6f, 0.1f));
            for (int i = 0; i < 3; i++) Part(h.transform, MeshKit.Get("torusHeat", b => b.Torus(Vector3.zero, 0.07f, 0.015f, 12, 5)), hm, new Vector3(0, 0, 0.05f + i * 0.04f), Vector3.one, new Vector3(90, 0, 0), "coil", false);
            Part(h.transform, cyl, dark, new Vector3(0, 0, 0.02f), new Vector3(0.1f, 0.03f, 0.1f), new Vector3(90, 0, 0), "base");
            heads["heat"] = h;
            // Filtermodul
            var f = Node(tip, "head_filter", Vector3.zero).gameObject;
            Part(f.transform, cyl, Mat(new Color(0.2f, 0.45f, 0.7f)), new Vector3(0, 0, 0.1f), new Vector3(0.18f, 0.2f, 0.18f), new Vector3(90, 0, 0), "filter");
            Part(f.transform, MeshKit.Get("mikoFilterRings", b => { for (int k = 0; k < 3; k++) b.Torus(new Vector3(0, -0.3f + k * 0.3f, 0), 0.5f, 0.05f, 14, 3); }), steel, new Vector3(0, 0, 0.1f), new Vector3(0.19f, 0.2f, 0.19f), new Vector3(90, 0, 0), "rings");
            heads["filter"] = f;
            // Bio-Modul
            var s = Node(tip, "head_seeder", Vector3.zero).gameObject;
            Part(s.transform, sph, Mat(new Color(0.35f, 0.7f, 0.3f)), new Vector3(0, 0, 0.1f), new Vector3(0.18f, 0.18f, 0.22f), Vector3.zero, "pod");
            Part(s.transform, cyl, steel, new Vector3(0, 0, 0.2f), new Vector3(0.04f, 0.04f, 0.04f), new Vector3(90, 0, 0), "nozzle");
            heads["seeder"] = s;
            SetToolHead("grab");
        }

        void BuildExtras(Material dark, Material steel)
        {
            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            GameObject e;
            e = Node(bin, "press", Vector3.zero).gameObject;
            Part(e.transform, cyl, sChrome, new Vector3(-0.43f, 0.25f, 0), new Vector3(0.05f, 0.25f, 0.05f), Vector3.zero, "pistonL");
            Part(e.transform, cyl, sChrome, new Vector3(0.43f, 0.25f, 0), new Vector3(0.05f, 0.25f, 0.05f), Vector3.zero, "pistonR");
            Part(e.transform, cyl, dark, new Vector3(-0.43f, 0.12f, 0), new Vector3(0.08f, 0.14f, 0.08f), Vector3.zero, "sleeveL");
            Part(e.transform, cyl, dark, new Vector3(0.43f, 0.12f, 0), new Vector3(0.08f, 0.14f, 0.08f), Vector3.zero, "sleeveR");
            Part(e.transform, cube, accentMat, new Vector3(0, 0.52f, 0), new Vector3(0.9f, 0.05f, 0.1f), Vector3.zero, "pressBar");
            extras["press"] = e;
            e = Node(body, "dive", Vector3.zero).gameObject;
            Part(e.transform, cyl, Mat(new Color(0.95f, 0.8f, 0.2f)), new Vector3(-0.52f, 0.1f, -0.25f), new Vector3(0.16f, 0.25f, 0.16f), new Vector3(90, 0, 0), "tank");
            Part(e.transform, cyl, steel, new Vector3(-0.52f, 0.1f, -0.1f), new Vector3(0.06f, 0.04f, 0.06f), new Vector3(90, 0, 0), "valve");
            Part(e.transform, cube, dark, new Vector3(0, -0.3f, -0.6f), new Vector3(0.5f, 0.03f, 0.25f), new Vector3(10, 0, 0), "fin");
            extras["dive"] = e;
            e = Node(body, "insulation", Vector3.zero).gameObject;
            var wool = Mat(new Color(0.92f, 0.9f, 0.85f));
            for (int i = 0; i < 7; i++) Part(e.transform, sph, wool, new Vector3(-0.39f + i * 0.13f, 0.26f, 0.42f), Vector3.one * 0.16f, Vector3.zero, "puff");
            extras["insulation"] = e;
            e = Node(bin, "hazard", Vector3.zero).gameObject;
            var yellow = Mat(new Color(0.95f, 0.78f, 0.1f));
            for (int i = 0; i < 4; i++) Part(e.transform, cube, i % 2 == 0 ? yellow : dark, new Vector3(-0.3f + i * 0.2f, 0.4f, 0.28f), new Vector3(0.2f, 0.08f, 0.02f), Vector3.zero, "stripe");
            extras["hazard"] = e;
            e = Node(body, "filterpack", Vector3.zero).gameObject;
            Part(e.transform, cyl, Mat(new Color(0.2f, 0.45f, 0.7f)), new Vector3(0.52f, 0.05f, -0.3f), new Vector3(0.14f, 0.2f, 0.14f), Vector3.zero, "canister");
            extras["filter"] = e;
            e = Node(body, "seederpack", Vector3.zero).gameObject;
            Part(e.transform, sph, Mat(new Color(0.35f, 0.7f, 0.3f)), new Vector3(-0.5f, 0.2f, 0.25f), new Vector3(0.14f, 0.14f, 0.2f), Vector3.zero, "pod");
            extras["seeder"] = e;
            e = Node(body, "battery", Vector3.zero).gameObject;
            Part(e.transform, cube, Mat(new Color(0.55f, 0.82f, 0.3f)), new Vector3(0, 0.05f, -0.52f), new Vector3(0.4f, 0.18f, 0.06f), Vector3.zero, "cell");
            Part(e.transform, cube, Mats.Get(Mats.Emissive, new Color(0.5f, 1f, 0.4f), new Color(0.5f, 1.6f, 0.4f)), new Vector3(0, 0.05f, -0.556f), new Vector3(0.3f, 0.04f, 0.01f), Vector3.zero, "charge", false);
            extras["battery"] = e;
            foreach (var x in extras.Values) x.SetActive(false);
        }

        public void SetToolHead(string tool)
        {
            if (!heads.ContainsKey(tool)) tool = "grab";
            foreach (var kv in heads) kv.Value.SetActive(kv.Key == tool);
        }

        /// <summary>Upgrades sichtbar machen (Behältergröße, Anbauteile).</summary>
        public void SetTech(WorldState w)
        {
            if (w == null) return;
            float binScale = 1f + 0.1f * w.TechLevel("bin");
            bin.localScale = new Vector3(binScale, 1f + 0.08f * w.TechLevel("bin"), binScale);
            extras["press"].SetActive(w.TechLevel("press") > 0);
            extras["dive"].SetActive(w.TechLevel("dive") > 0);
            extras["insulation"].SetActive(w.TechLevel("insulation") > 0);
            extras["hazard"].SetActive(w.TechLevel("hazard") > 0);
            extras["filter"].SetActive(w.TechLevel("filter") > 0);
            extras["seeder"].SetActive(w.TechLevel("seeder") > 0);
            extras["battery"].SetActive(w.TechLevel("battery") > 0);
        }

        public void SetCosmetics(string color, string accent, string sticker, string attach)
        {
            CosmeticDef c;
            if (color != null && color != curColor && GameData.Cosmetics.TryGetValue(color, out c) && c.Kind == "color") { curColor = color; bodyMat.color = Mats.C(c.Value); SetSlot(SBody, Mats.C(c.Value)); }
            if (accent != null && accent != curAccent && GameData.Cosmetics.TryGetValue(accent, out c) && c.Kind == "accent") { curAccent = accent; accentMat.color = Mats.C(c.Value); SetSlot(SAccent, Mats.C(c.Value)); }
            if (sticker != curSticker)
            {
                curSticker = sticker;
                if (sticker != null && GameData.Cosmetics.TryGetValue(sticker, out c) && c.Kind == "sticker" && !c.Default)
                {
                    stickerMat.mainTexture = StickerTex(sticker, Mats.C(c.Value));
                    stickerQuad.gameObject.SetActive(true);
                }
                else stickerQuad.gameObject.SetActive(false);
            }
            if (attach != curAttach)
            {
                curAttach = attach;
                if (cosAttach != null) Destroy(cosAttach);
                cosAttach = null;
                if (attach != null && GameData.Cosmetics.TryGetValue(attach, out c) && c.Kind == "attach" && !c.Default) cosAttach = BuildAttach(attach, Mats.C(c.Value));
            }
        }

        GameObject BuildAttach(string id, Color col)
        {
            var n = Node(head, "attach_" + id, Vector3.zero);
            var m = Mat(col);
            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            switch (id)
            {
                case "antenne":
                    Part(n, cyl, Mat(new Color(0.3f, 0.3f, 0.3f)), new Vector3(0.25f, 0.4f, -0.05f), new Vector3(0.02f, 0.25f, 0.02f), Vector3.zero, "rod");
                    Part(n, sph, Mats.Get(Mats.Emissive, col, col * 1.5f), new Vector3(0.25f, 0.66f, -0.05f), Vector3.one * 0.08f, Vector3.zero, "tip");
                    break;
                case "faehnchen":
                    Part(n, cyl, Mat(new Color(0.3f, 0.3f, 0.3f)), new Vector3(-0.3f, 0.5f, -0.1f), new Vector3(0.02f, 0.35f, 0.02f), Vector3.zero, "pole");
                    Part(n, cube, m, new Vector3(-0.2f, 0.75f, -0.1f), new Vector3(0.2f, 0.12f, 0.01f), Vector3.zero, "flag");
                    break;
                case "muetze":
                    Part(n, sph, m, new Vector3(0, 0.2f, -0.02f), new Vector3(0.62f, 0.34f, 0.44f), Vector3.zero, "cap");
                    Part(n, sph, Mat(Color.white), new Vector3(0, 0.38f, -0.02f), Vector3.one * 0.13f, Vector3.zero, "bobble");
                    break;
                case "muschel":
                    Part(n, sph, m, new Vector3(0.36f, 0.12f, 0), new Vector3(0.18f, 0.16f, 0.08f), new Vector3(0, 0, 20), "shell");
                    break;
                case "blume":
                    Part(n, cyl, Mat(new Color(0.3f, 0.6f, 0.25f)), new Vector3(-0.28f, 0.3f, 0), new Vector3(0.02f, 0.14f, 0.02f), Vector3.zero, "stem");
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2;
                        Part(n, sph, m, new Vector3(-0.28f + Mathf.Cos(a) * 0.06f, 0.46f, Mathf.Sin(a) * 0.06f), Vector3.one * 0.07f, Vector3.zero, "petal");
                    }
                    Part(n, sph, Mat(new Color(1f, 0.85f, 0.2f)), new Vector3(-0.28f, 0.46f, 0), Vector3.one * 0.05f, Vector3.zero, "center");
                    break;
                case "rundumleuchte": // orangefarbene Warnleuchte auf dem Kopf
                    Part(n, cyl, Mat(new Color(0.2f, 0.2f, 0.22f)), new Vector3(0, 0.3f, -0.05f), new Vector3(0.14f, 0.04f, 0.14f), Vector3.zero, "base");
                    Part(n, sph, Mats.Get(Mats.Emissive, col, col * 1.6f), new Vector3(0, 0.38f, -0.05f), new Vector3(0.18f, 0.16f, 0.18f), Vector3.zero, "dome");
                    break;
                case "gluehbirne": // Glühbirne am Draht
                    Part(n, cyl, Mat(new Color(0.3f, 0.3f, 0.3f)), new Vector3(0.22f, 0.4f, -0.05f), new Vector3(0.015f, 0.2f, 0.015f), Vector3.zero, "wire");
                    Part(n, cyl, Mat(new Color(0.6f, 0.6f, 0.62f)), new Vector3(0.22f, 0.6f, -0.05f), new Vector3(0.05f, 0.04f, 0.05f), Vector3.zero, "socket");
                    Part(n, sph, Mats.Get(Mats.Emissive, col, col * 1.4f), new Vector3(0.22f, 0.7f, -0.05f), new Vector3(0.12f, 0.14f, 0.12f), Vector3.zero, "bulb");
                    break;
                case "propeller": // Propellermütze
                    Part(n, sph, m, new Vector3(0, 0.22f, -0.02f), new Vector3(0.56f, 0.28f, 0.42f), Vector3.zero, "cap");
                    Part(n, cyl, Mat(new Color(0.3f, 0.3f, 0.3f)), new Vector3(0, 0.4f, -0.02f), new Vector3(0.02f, 0.06f, 0.02f), Vector3.zero, "axle");
                    Part(n, cube, Mat(new Color(1f, 0.82f, 0.25f)), new Vector3(0, 0.47f, -0.02f), new Vector3(0.44f, 0.01f, 0.06f), new Vector3(0, 25, 0), "blade1");
                    Part(n, cube, Mat(new Color(0.9f, 0.3f, 0.3f)), new Vector3(0, 0.47f, -0.02f), new Vector3(0.44f, 0.01f, 0.06f), new Vector3(0, 115, 0), "blade2");
                    break;
            }
            return n.gameObject;
        }

        static readonly Dictionary<string, Texture2D> stickerTex = new Dictionary<string, Texture2D>();

        /// <summary>Aufkleber-Symbole als kleine prozedurale Texturen.</summary>
        static Texture2D StickerTex(string id, Color c)
        {
            Texture2D t;
            if (stickerTex.TryGetValue(id, out t)) return t;
            const int N = 64;
            t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                    bool inside = false;
                    switch (id)
                    {
                        case "stern": inside = r < 0.45f + 0.4f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f); break;
                        case "blatt": inside = Mathf.Abs(u) < 0.55f * (1 - v * v) && Mathf.Abs(v) < 0.9f; break;
                        case "welle": inside = Mathf.Abs(v - 0.25f * Mathf.Sin(u * 5f)) < 0.18f || Mathf.Abs(v + 0.4f - 0.25f * Mathf.Sin(u * 5f + 1)) < 0.12f; break;
                        case "flocke": inside = r < 0.9f && (Mathf.Abs(Mathf.Sin(a * 3f)) < 0.18f || r < 0.2f); break;
                        case "zahnrad": inside = (r < 0.6f + (Mathf.Cos(a * 8f) > 0.3f ? 0.25f : 0f)) && r > 0.25f; break;
                        case "herz": { float xx = u * 1.1f, yy = -v * 1.1f + 0.3f; inside = Mathf.Pow(xx * xx + yy * yy - 0.5f, 3f) - xx * xx * yy * yy * yy < 0; break; }
                        case "krone": inside = Mathf.Abs(u) < 0.75f && v > -0.55f && v < -0.1f || (Mathf.Abs(u) < 0.75f && v >= -0.1f && v < 0.6f - 0.9f * Mathf.Abs(Mathf.Sin(u * 4.7f))); break;
                        case "blitz": inside = Mathf.Abs(u - (v > 0 ? 0.35f * v : 0.35f * v) + (v > 0 ? 0.12f : -0.12f)) < 0.2f && Mathf.Abs(v) < 0.85f; break;
                        case "mond": inside = r < 0.8f && Mathf.Sqrt((u - 0.35f) * (u - 0.35f) + (v - 0.2f) * (v - 0.2f)) > 0.6f; break;
                        case "komet": inside = Mathf.Sqrt((u - 0.35f) * (u - 0.35f) + (v - 0.35f) * (v - 0.35f)) < 0.3f || (Mathf.Abs(u - v) < 0.18f * (1f + u + v) * 0.5f && u + v < 0.7f && u + v > -1.2f); break;
                        case "tropfen": inside = Mathf.Sqrt(u * u + (v + 0.25f) * (v + 0.25f)) < 0.5f || (v > -0.25f && v < 0.85f && Mathf.Abs(u) < 0.5f * (0.85f - v) / 1.1f); break;
                        default: inside = r < 0.8f; break;
                    }
                    bool border = !inside && r < 0.98f;
                    px[y * N + x] = inside ? c : border ? new Color(1, 1, 1, 0.9f) : new Color(0, 0, 0, 0);
                }
            t.SetPixels(px);
            t.Apply(false, true);
            stickerTex[id] = t;
            return t;
        }

        /// <summary>Kurze Gefühlsregung (Augenfarbe, Lider, Wackeln): "happy", "sad", "curious".</summary>
        public void Emote(string kind)
        {
            emoteT = 1.6f;
            emoteKind = kind;
            eyeColor = kind == "happy" ? new Color(0.4f, 1f, 0.55f) : kind == "sad" ? new Color(0.3f, 0.45f, 1f) : new Color(1f, 0.85f, 0.3f);
        }

        public void Grab() { armTarget = 1f; graspT = 0.45f; }
        float graspT;

        /// <summary>Animation pro Frame.</summary>
        public void Animate(float dt, float speed, float loadFrac, string tool, bool acting, bool swimming, bool sleeping, bool off, float night, Vector3 lookDirWorld)
        {
            // Räder
            wheelRot += speed * dt / 0.28f * Mathf.Rad2Deg;
            for (int i = 0; i < 3; i++) wheels[i].localRotation = Quaternion.Euler(wheelRot * (i == 2 ? 1.27f : 1f), 0, 0);
            // Federung: schwere Ladung drückt den Körper tiefer, Fahrt wippt leicht
            bob += dt * (4f + speed * 1.4f);
            float sag = -0.07f * loadFrac - (sleeping ? 0.12f : 0f);
            float wobble = Mathf.Sin(bob) * 0.012f * Mathf.Clamp01(speed / 6f) * (1f + loadFrac);
            body.localPosition = new Vector3(0, 0.62f + sag + wobble, 0);
            body.localRotation = Quaternion.Euler(-Mathf.Clamp(speed * 0.6f, 0, 4f) * (1 + loadFrac) + (sleeping ? 6 : 0), 0, Mathf.Sin(bob * 0.5f) * 0.6f * Mathf.Clamp01(speed / 6f));
            if (swimming) root.localPosition = new Vector3(0, Mathf.Sin(Time.time * 1.8f) * 0.06f, 0); else root.localPosition = Vector3.zero;
            // Behälter-Füllstand (Brocken-Mesh ist 1 m hoch, wird auf die Füllhöhe gestaucht)
            float fillH = Mathf.Max(0.02f, loadFrac * 0.44f);
            binFill.localScale = new Vector3(1f, fillH, 1f);
            binFill.localPosition = new Vector3(0, 0.0f, 0);
            binFill.gameObject.SetActive(loadFrac > 0.01f);
            // Arm: gefaltet ↔ ausgestreckt
            if (graspT > 0) graspT -= dt;
            float want = acting || graspT > 0 ? 1f : 0f;
            arm = Mathf.MoveTowards(arm, want, dt * 5f);
            arm1.localRotation = Quaternion.Euler(Mathf.Lerp(-70f, 28f, arm), Mathf.Lerp(-8f, -18f, arm), 0);
            arm2.localRotation = Quaternion.Euler(Mathf.Lerp(150f, 20f, arm), 0, 0);
            if (saw != null && tool == "cutter" && acting) saw.Rotate(0, 1200f * dt, 0, Space.Self);
            // Augen: blinzeln, schauen, Stimmung
            blinkT -= dt;
            if (blinkT <= 0) { blink = 0.15f; blinkT = Random.Range(2.5f, 6f); }
            if (blink > 0) blink -= dt;
            float eyeH = off ? 0.02f : sleeping ? 0.015f : blink > 0 ? 0.02f : 0.1f;
            var local = head.InverseTransformDirection(lookDirWorld);
            lookLocal = Vector3.Lerp(lookLocal, new Vector3(Mathf.Clamp(local.x, -1, 1) * 0.05f, Mathf.Clamp(local.y, -1, 1) * 0.03f, 0), dt * 6f);
            eyeL.localScale = new Vector3(0.15f, eyeH, 0.03f);
            eyeR.localScale = new Vector3(0.15f, eyeH, 0.03f);
            eyeL.localPosition = new Vector3(-0.17f + lookLocal.x, lookLocal.y, 0.225f);
            eyeR.localPosition = new Vector3(0.17f + lookLocal.x, lookLocal.y, 0.225f);
            if (emoteT > 0) emoteT -= dt;
            // Lider: Ausdruck je Stimmung, sonst leicht müde bei Nacht/Ladung
            float tLower = 0f, tRaise = 0f, tTilt = 0f, tAsym = 0f;
            if (emoteT > 0)
            {
                if (emoteKind == "happy") { tRaise = 0.55f; }
                else if (emoteKind == "sad") { tLower = 0.35f; tTilt = 18f; }
                else { tAsym = 0.4f; }
            }
            else if (sleeping) tLower = 0.9f;
            else { tLower = loadFrac * 0.12f; MoodLids(ref tLower, ref tRaise, ref tTilt, ref tAsym); }
            float k = Mathf.Clamp01(dt * 10f);
            lidLower = Mathf.Lerp(lidLower, tLower, k); lidRaise = Mathf.Lerp(lidRaise, tRaise, k);
            lidTilt = Mathf.Lerp(lidTilt, tTilt, k); lidAsym = Mathf.Lerp(lidAsym, tAsym, k);
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                var ep = i == 0 ? eyeL.localPosition : eyeR.localPosition;
                float lower = Mathf.Clamp01(lidLower + (i == 1 ? lidAsym : -lidAsym * 0.6f));
                lidUp[i].localPosition = new Vector3(ep.x, ep.y + 0.056f, 0.247f);
                lidUp[i].localScale = new Vector3(1f, Mathf.Max(0.001f, lower * 0.1f + Mathf.Abs(lidTilt) * 0.0015f), 1f);
                lidUp[i].localRotation = Quaternion.Euler(0, 0, -sx * lidTilt);
                lidDown[i].localPosition = new Vector3(ep.x, ep.y - 0.056f, 0.247f);
                lidDown[i].localScale = new Vector3(1f, Mathf.Max(0.001f, lidRaise * 0.1f), 1f);
                lidDown[i].localRotation = Quaternion.Euler(0, 0, sx * lidRaise * 10f);
                lidUp[i].gameObject.SetActive(lidUp[i].localScale.y > 0.002f);
                lidDown[i].gameObject.SetActive(lidDown[i].localScale.y > 0.002f);
            }
            var ec = off ? new Color(0.8f, 0.1f, 0.05f) * (Mathf.Sin(Time.time * 6f) > 0 ? 1f : 0.2f) : emoteT > 0 ? eyeColor : MoodEyeColor(dt);
            Mats.SetEmission(eyeMat, ec * (sleeping ? 0.4f : 2.2f * (off || emoteT > 0 ? 1f : MoodGlow())));
            head.localRotation = Quaternion.Euler(emoteT > 0 ? Mathf.Sin(emoteT * 18f) * 6f : 0, emoteT > 0 ? Mathf.Sin(emoteT * 11f) * 8f : 0, emoteT > 0 && emoteKind == "curious" ? 10f * Mathf.Min(1f, emoteT) : 0f);
            // Mimik und Gesten (Stimmung, Winken, Hüpfen, Kopfneigen, Zittern) – siehe MikoFace.cs
            ApplyFace(dt, speed, acting, swimming, sleeping, off);
            // Scheinwerfer in der Dunkelheit
            float lamp = off || sleeping ? 0f : night;
            if (Headlight != null) Headlight.intensity = Mathf.Lerp(0f, 2.4f, lamp);
            if (lampMat != null) Mats.SetEmission(lampMat, new Color(1f, 0.9f, 0.7f) * Mathf.Lerp(0.2f, 3.5f, lamp));
        }
    }
}
