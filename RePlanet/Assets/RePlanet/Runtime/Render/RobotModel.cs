using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// MIKO: prozedural aufgebautes, animiertes Robotermodell (türkise Hülle, orange Akzente, breites Visier,
    /// drei Räder, faltbarer Sammelarm, sichtbarer Rückenbehälter). Upgrades und Kosmetik sind am Modell sichtbar.
    /// </summary>
    public class RobotModel : MonoBehaviour
    {
        Transform root, body, head, visor, eyeL, eyeR, bin, binFill, arm1, arm2, tip, stickerQuad;
        readonly Transform[] wheels = new Transform[3];
        Material bodyMat, accentMat, eyeMat, visorMat, fillMat, stickerMat;
        readonly Dictionary<string, GameObject> heads = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> extras = new Dictionary<string, GameObject>();
        GameObject cosAttach;
        string curAttach, curSticker;
        public Light Headlight;
        Transform saw;

        float arm = 0f, armTarget, blinkT = 2f, blink, wheelRot, bob, emoteT;
        Color eyeBase = new Color(0.35f, 0.95f, 1f);
        Color eyeColor;
        Vector3 lookLocal;

        static Material Mat(Color c) { return Mats.Get(Mats.Opaque, c); }

        public static RobotModel Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var r = go.AddComponent<RobotModel>();
            r.Build();
            return r;
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

        void Build()
        {
            bodyMat = Mats.Unique(Mats.Opaque, Mats.C(0x2EC4B6));
            bodyMat.SetFloat("_Glossiness", 0.45f);
            accentMat = Mats.Unique(Mats.Opaque, Mats.C(0xFF8C2E));
            var dark = Mat(new Color(0.16f, 0.17f, 0.19f));
            var steel = Mats.Get(Mats.Metal, new Color(0.62f, 0.65f, 0.68f));
            eyeMat = Mats.Unique(Mats.Emissive, eyeBase);
            Mats.SetEmission(eyeMat, eyeBase * 2.2f);
            visorMat = Mats.Unique(Mats.Emissive, new Color(0.05f, 0.08f, 0.1f));
            Mats.SetEmission(visorMat, new Color(0.02f, 0.12f, 0.16f));
            visorMat.SetFloat("_Glossiness", 0.95f);
            fillMat = Mats.Unique(Mats.Opaque, new Color(0.55f, 0.5f, 0.42f));

            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            root = Node(transform, "root", Vector3.zero);
            // Fahrwerk mit drei Rädern
            Part(root, cube, dark, new Vector3(0, 0.3f, -0.05f), new Vector3(0.8f, 0.18f, 0.95f), Vector3.zero, "chassis");
            wheels[0] = Node(root, "wheelL", new Vector3(-0.47f, 0.28f, -0.32f));
            wheels[1] = Node(root, "wheelR", new Vector3(0.47f, 0.28f, -0.32f));
            wheels[2] = Node(root, "wheelF", new Vector3(0, 0.22f, 0.45f));
            for (int i = 0; i < 3; i++)
            {
                float r = i < 2 ? 0.28f : 0.22f;
                var tire = Part(wheels[i], cyl, dark, Vector3.zero, new Vector3(r * 2, i < 2 ? 0.16f : 0.14f, r * 2), new Vector3(0, 0, 90), "tire");
                Part(wheels[i], cyl, accentMat, Vector3.zero, new Vector3(r * 1.0f, i < 2 ? 0.18f : 0.16f, r * 1.0f), new Vector3(0, 0, 90), "hub");
                Part(wheels[i], cube, steel, new Vector3(0, r * 0.55f, 0), new Vector3(i < 2 ? 0.19f : 0.17f, 0.07f, 0.07f), Vector3.zero, "spoke");
            }
            Part(root, cube, dark, new Vector3(0, 0.3f, 0.3f), new Vector3(0.1f, 0.1f, 0.3f), Vector3.zero, "fork");

            // Körper (gefedert)
            body = Node(root, "body", new Vector3(0, 0.62f, 0));
            Part(body, cube, bodyMat, new Vector3(0, 0, 0.02f), new Vector3(0.9f, 0.5f, 0.95f), Vector3.zero, "hull");
            Part(body, cube, bodyMat, new Vector3(0, -0.02f, 0.5f), new Vector3(0.82f, 0.4f, 0.12f), new Vector3(-12, 0, 0), "nose");
            Part(body, cube, accentMat, new Vector3(0, -0.22f, 0.54f), new Vector3(0.86f, 0.1f, 0.1f), Vector3.zero, "bumper");
            Part(body, cube, accentMat, new Vector3(-0.455f, 0.02f, 0.02f), new Vector3(0.02f, 0.08f, 0.8f), Vector3.zero, "stripeL");
            Part(body, cube, accentMat, new Vector3(0.455f, 0.02f, 0.02f), new Vector3(0.02f, 0.08f, 0.8f), Vector3.zero, "stripeR");
            Part(body, cube, dark, new Vector3(0, 0.26f, 0.05f), new Vector3(0.5f, 0.05f, 0.5f), Vector3.zero, "neck");

            // Kopf mit breitem Visier
            head = Node(body, "head", new Vector3(0, 0.46f, 0.18f));
            Part(head, cube, bodyMat, Vector3.zero, new Vector3(0.84f, 0.36f, 0.42f), Vector3.zero, "headShell");
            Part(head, cube, accentMat, new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.04f, 0.3f), Vector3.zero, "headTop");
            visor = Part(head, cube, visorMat, new Vector3(0, -0.01f, 0.2f), new Vector3(0.74f, 0.24f, 0.04f), Vector3.zero, "visor", false);
            eyeL = Part(head, sph, eyeMat, new Vector3(-0.17f, 0f, 0.225f), new Vector3(0.15f, 0.1f, 0.03f), Vector3.zero, "eyeL", false);
            eyeR = Part(head, sph, eyeMat, new Vector3(0.17f, 0f, 0.225f), new Vector3(0.15f, 0.1f, 0.03f), Vector3.zero, "eyeR", false);
            var hl = Node(head, "headlight", new Vector3(0, 0.05f, 0.25f));
            Headlight = hl.gameObject.AddComponent<Light>();
            Headlight.type = LightType.Spot;
            Headlight.spotAngle = 70f;
            Headlight.range = 22f;
            Headlight.intensity = 0f;
            Headlight.color = new Color(1f, 0.93f, 0.8f);
            Headlight.shadows = LightShadows.None;
            hl.localRotation = Quaternion.Euler(12, 0, 0);

            // Rückenbehälter (Größe wächst mit Upgrades)
            bin = Node(body, "bin", new Vector3(0, 0.36f, -0.36f));
            Part(bin, cube, steel, new Vector3(0, -0.02f, 0), new Vector3(0.78f, 0.04f, 0.5f), Vector3.zero, "binFloor");
            Part(bin, cube, bodyMat, new Vector3(-0.38f, 0.22f, 0), new Vector3(0.04f, 0.46f, 0.52f), Vector3.zero, "binL");
            Part(bin, cube, bodyMat, new Vector3(0.38f, 0.22f, 0), new Vector3(0.04f, 0.46f, 0.52f), Vector3.zero, "binR");
            Part(bin, cube, bodyMat, new Vector3(0, 0.22f, -0.25f), new Vector3(0.8f, 0.46f, 0.04f), Vector3.zero, "binB");
            Part(bin, cube, bodyMat, new Vector3(0, 0.22f, 0.25f), new Vector3(0.8f, 0.46f, 0.04f), Vector3.zero, "binF");
            Part(bin, cube, accentMat, new Vector3(0, 0.46f, -0.25f), new Vector3(0.82f, 0.04f, 0.06f), Vector3.zero, "binRim");
            binFill = Part(bin, cube, fillMat, new Vector3(0, 0.02f, 0), new Vector3(0.72f, 0.02f, 0.44f), Vector3.zero, "fill");
            stickerMat = Mats.Unique(Mats.UnlitTransparent, Color.white);
            stickerQuad = Part(bin, MeshKit.Quad, stickerMat, new Vector3(0.405f, 0.24f, 0), new Vector3(0.3f, 0.3f, 1), new Vector3(0, -90, 0), "sticker", false);
            stickerQuad.gameObject.SetActive(false);

            // Faltbarer Sammelarm (rechts)
            arm1 = Node(body, "arm1", new Vector3(0.5f, 0.12f, 0.25f));
            Part(arm1, sph, dark, Vector3.zero, Vector3.one * 0.16f, Vector3.zero, "shoulder");
            Part(arm1, cube, bodyMat, new Vector3(0, 0, 0.25f), new Vector3(0.1f, 0.1f, 0.5f), Vector3.zero, "upper");
            arm2 = Node(arm1, "arm2", new Vector3(0, 0, 0.5f));
            Part(arm2, sph, dark, Vector3.zero, Vector3.one * 0.12f, Vector3.zero, "elbow");
            Part(arm2, cube, accentMat, new Vector3(0, 0, 0.22f), new Vector3(0.08f, 0.08f, 0.44f), Vector3.zero, "fore");
            tip = Node(arm2, "tip", new Vector3(0, 0, 0.46f));
            BuildHeads(dark, steel);
            BuildExtras(dark, steel);
        }

        void BuildHeads(Material dark, Material steel)
        {
            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            // Greifklaue
            var g = Node(tip, "head_grab", Vector3.zero).gameObject;
            Part(g.transform, cube, dark, new Vector3(0.05f, 0, 0.08f), new Vector3(0.03f, 0.06f, 0.16f), new Vector3(0, 18, 0), "fingerA");
            Part(g.transform, cube, dark, new Vector3(-0.05f, 0, 0.08f), new Vector3(0.03f, 0.06f, 0.16f), new Vector3(0, -18, 0), "fingerB");
            heads["grab"] = g;
            // Sauger-Düse
            var v = Node(tip, "head_vacuum", Vector3.zero).gameObject;
            Part(v.transform, cyl, steel, new Vector3(0, 0, 0.1f), new Vector3(0.12f, 0.2f, 0.12f), new Vector3(90, 0, 0), "nozzle");
            Part(v.transform, cyl, accentMat, new Vector3(0, 0, 0.21f), new Vector3(0.2f, 0.03f, 0.2f), new Vector3(90, 0, 0), "mouth");
            heads["vacuum"] = v;
            // Magnetscheibe
            var m = Node(tip, "head_magnet", Vector3.zero).gameObject;
            Part(m.transform, cyl, Mat(new Color(0.75f, 0.12f, 0.1f)), new Vector3(0, 0, 0.06f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(90, 0, 0), "disc");
            Part(m.transform, cyl, steel, new Vector3(0, 0, 0.1f), new Vector3(0.22f, 0.02f, 0.22f), new Vector3(90, 0, 0), "face");
            heads["magnet"] = m;
            // Kreissäge
            var c = Node(tip, "head_cutter", Vector3.zero).gameObject;
            Part(c.transform, cube, dark, new Vector3(0, 0, 0.05f), new Vector3(0.06f, 0.08f, 0.12f), Vector3.zero, "mount");
            saw = Part(c.transform, cyl, Mats.Get(Mats.Metal, new Color(0.85f, 0.85f, 0.88f)), new Vector3(0, 0.02f, 0.14f), new Vector3(0.28f, 0.01f, 0.28f), new Vector3(0, 0, 90), "blade");
            heads["cutter"] = c;
            // Wärmespirale
            var h = Node(tip, "head_heat", Vector3.zero).gameObject;
            var hm = Mats.Unique(Mats.Emissive, new Color(1f, 0.45f, 0.1f));
            Mats.SetEmission(hm, new Color(2f, 0.6f, 0.1f));
            for (int i = 0; i < 3; i++) Part(h.transform, MeshKit.Get("torusHeat", b => b.Torus(Vector3.zero, 0.07f, 0.015f, 12, 5)), hm, new Vector3(0, 0, 0.05f + i * 0.04f), Vector3.one, new Vector3(90, 0, 0), "coil", false);
            heads["heat"] = h;
            // Filtermodul
            var f = Node(tip, "head_filter", Vector3.zero).gameObject;
            Part(f.transform, cyl, Mat(new Color(0.2f, 0.45f, 0.7f)), new Vector3(0, 0, 0.1f), new Vector3(0.18f, 0.2f, 0.18f), new Vector3(90, 0, 0), "filter");
            heads["filter"] = f;
            // Bio-Modul
            var s = Node(tip, "head_seeder", Vector3.zero).gameObject;
            Part(s.transform, sph, Mat(new Color(0.35f, 0.7f, 0.3f)), new Vector3(0, 0, 0.1f), new Vector3(0.18f, 0.18f, 0.22f), Vector3.zero, "pod");
            heads["seeder"] = s;
            SetToolHead("grab");
        }

        void BuildExtras(Material dark, Material steel)
        {
            var cube = MeshKit.Cube; var cyl = MeshKit.Cylinder; var sph = MeshKit.Sphere;
            GameObject e;
            e = Node(bin, "press", Vector3.zero).gameObject;
            Part(e.transform, cyl, steel, new Vector3(-0.43f, 0.25f, 0), new Vector3(0.07f, 0.25f, 0.07f), Vector3.zero, "pistonL");
            Part(e.transform, cyl, steel, new Vector3(0.43f, 0.25f, 0), new Vector3(0.07f, 0.25f, 0.07f), Vector3.zero, "pistonR");
            Part(e.transform, cube, accentMat, new Vector3(0, 0.52f, 0), new Vector3(0.9f, 0.05f, 0.1f), Vector3.zero, "pressBar");
            extras["press"] = e;
            e = Node(body, "dive", Vector3.zero).gameObject;
            Part(e.transform, cyl, Mat(new Color(0.95f, 0.8f, 0.2f)), new Vector3(-0.52f, 0.1f, -0.25f), new Vector3(0.16f, 0.25f, 0.16f), new Vector3(90, 0, 0), "tank");
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
            if (color != null && GameData.Cosmetics.TryGetValue(color, out c) && c.Kind == "color") bodyMat.color = Mats.C(c.Value);
            if (accent != null && GameData.Cosmetics.TryGetValue(accent, out c) && c.Kind == "accent") accentMat.color = Mats.C(c.Value);
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

        /// <summary>Kurze Gefühlsregung (Augenfarbe/Wackeln): "happy", "sad", "curious".</summary>
        public void Emote(string kind)
        {
            emoteT = 1.2f;
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
            // Behälter-Füllstand
            binFill.localScale = new Vector3(0.72f, Mathf.Max(0.02f, loadFrac * 0.44f), 0.44f);
            binFill.localPosition = new Vector3(0, 0.01f + loadFrac * 0.22f, 0);
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
            var ec = off ? new Color(0.8f, 0.1f, 0.05f) * (Mathf.Sin(Time.time * 6f) > 0 ? 1f : 0.2f) : emoteT > 0 ? eyeColor : eyeBase;
            Mats.SetEmission(eyeMat, ec * (sleeping ? 0.4f : 2.2f));
            head.localRotation = Quaternion.Euler(emoteT > 0 ? Mathf.Sin(emoteT * 18f) * 6f : 0, emoteT > 0 ? Mathf.Sin(emoteT * 11f) * 8f : 0, 0);
            // Scheinwerfer in der Dunkelheit
            if (Headlight != null) Headlight.intensity = off || sleeping ? 0f : Mathf.Lerp(0f, 2.2f, night);
        }
    }
}
