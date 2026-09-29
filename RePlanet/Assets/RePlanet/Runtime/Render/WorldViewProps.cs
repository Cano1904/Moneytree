using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;
using UnityEngine.Rendering;

namespace RePlanet
{
    public partial class WorldView
    {
        static readonly Dictionary<string, uint> StationColors = new Dictionary<string, uint>
        {
            { "sell", 0x4CAF6A }, { "workshop", 0xFF8C2E }, { "sort", 0x3FA7D6 }, { "trader", 0xB48CE0 },
            { "disposal", 0xE8493A }, { "contracts", 0xF2C14E }, { "storage", 0x2EC4B6 }, { "charge", 0x8FD14F },
        };

        public static readonly Dictionary<string, string> StationNames = new Dictionary<string, string>
        {
            { "sell", "Verkauf" }, { "workshop", "Werkstatt" }, { "sort", "Sortiertisch" }, { "trader", "Materialhändler" },
            { "disposal", "Entsorgung" }, { "contracts", "Auftragstafel" }, { "storage", "Lager" }, { "charge", "Ladeplatz" },
            { "ship", "Transportschiff" }, { "garage", "Garage" }, { "build", "Baufläche" },
        };

        GameObject Obj(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion rot, Transform parent = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ?? Root, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        Vector3 P(V3 v) { return new Vector3(v.x, v.y, v.z); }

        // ================================================================== Requisiten
        void BuildProps()
        {
            if (trimMat == null) InitBuildingMats();
            var cb = new ChunkBuilder(80f);
            treeDead = new ChunkBuilder(80f); treeAlive.Clear(); treeAliveThreshold.Clear();
            var dark = Mats.Get(Mats.Opaque, new Color(0.22f, 0.22f, 0.24f));
            var rust = Mats.Get(Mats.Opaque, new Color(0.5f, 0.3f, 0.2f));
            var rng = new Rng(Def.Seed + 3);
            foreach (var p in Layout.Props)
            {
                var pos = P(p.Pos);
                int area = Mathf.Clamp(p.Area, 0, 2);
                var mb = cb.At(pos.x, pos.z);
                mb.M = Matrix4x4.TRS(pos, Quaternion.Euler(0, p.Rot * Mathf.Rad2Deg, 0), Vector3.one * p.Scale);
                switch (p.Kind)
                {
                    case "lamp":
                    case "heatlamp":
                        if (StreetLamp(mb, rng, area, p.Kind == "heatlamp"))
                            lampPositions[area].Add(pos + Quaternion.Euler(0, p.Rot * Mathf.Rad2Deg, 0) * new Vector3(0, 5.1f, 1.27f));
                        break;
                    case "zonelamp":
                        {
                            var m = Mats.Unique(Mats.Emissive, new Color(0.9f, 0.85f, 0.7f));
                            Mats.SetEmission(m, Color.black);
                            zoneLampMats[p.Style] = m;
                            var go = new GameObject("ZoneLamp" + p.Style);
                            go.transform.SetParent(Root, false);
                            go.transform.localPosition = pos;
                            ZoneLampModel(go.transform, m);
                            zoneLamps[p.Style] = go;
                            break;
                        }
                    case "tree":
                    case "palm":
                    case "deadcactus":
                    case "icespike":
                        {
                            float threshold = 0.05f + rng.Next() * 0.9f;
                            int bucket = Mathf.Clamp((int)(threshold * 4f), 0, 3);
                            int key = area * 4 + bucket;
                            MultiBuilder alive;
                            if (!treeAlive.TryGetValue(key, out alive)) { alive = new MultiBuilder { UsePalette = true }; treeAlive[key] = alive; treeAliveThreshold[key] = (bucket + 0.5f) / 4f; }
                            var dead = treeDead.At(pos.x, pos.z);
                            dead.M = alive.M = mb.M;
                            BuildTree(p, rng, dead, alive);
                            break;
                        }
                    case "billboard":
                        Billboard(mb, rng);
                        BuildKonsumaSign(pos, p.Rot, p.Scale);
                        break;
                    case "busstop":
                        BusStop(mb, rng);
                        break;
                    case "fountain":
                        {
                            var stone = Mats.Get(Mats.Opaque, new Color(0.7f, 0.68f, 0.62f));
                            var stoneD = Mats.Get(Mats.Opaque, new Color(0.55f, 0.53f, 0.49f));
                            mb.For(stone).Cylinder(Vector3.zero, 3f, 0.6f, 24, true, 3f);
                            mb.For(stoneD).Torus(new Vector3(0, 0.6f, 0), 2.9f, 0.14f, 24, 4);
                            mb.For(Mats.Get(Mats.Water, new Color(0.3f, 0.6f, 0.75f, 0.6f))).Cylinder(new Vector3(0, 0.5f, 0), 2.7f, 0.05f, 24);
                            mb.For(stoneD).Cylinder(Vector3.zero, 0.45f, 1.5f, 12, true, 0.3f);
                            mb.For(stone).Cylinder(new Vector3(0, 1.5f, 0), 1.2f, 0.25f, 16, true, 1.3f);
                            mb.For(stone).Cylinder(Vector3.zero, 0.25f, 2.2f, 10);
                            mb.For(stone).Sphere(new Vector3(0, 2.25f, 0), 0.3f, 10, 6);
                            for (int k = 0; k < 5; k++) mb.For(Mats.Get(Mats.Opaque, new Color(0.45f, 0.35f, 0.22f))).Crumple(new Vector3(Mathf.Cos(k * 1.3f) * 1.8f, 0.52f, Mathf.Sin(k * 1.3f) * 1.8f), 0.18f, 0.25f, k, 0.3f, 6, 3);
                            fountains.Add(MakeFountain(pos + Vector3.up * 2.2f * p.Scale));
                            break;
                        }
                    case "pipe":
                        mb.For(rust).CylinderX(new Vector3(0, 0.8f, 0), 0.6f, 12f, 14);
                        for (int k = -1; k <= 1; k++)
                        {
                            mb.For(acMat).CylinderX(new Vector3(k * 5.8f, 0.8f, 0), 0.72f, 0.2f, 14);
                            mb.For(dark).Box(new Vector3(k * 4f, 0.2f, 0), new Vector3(0.4f, 0.4f, 1.5f));
                        }
                        break;
                    case "rustcar":
                        CarWreck(mb, rng, p.Style + rng.Range(0, 6));
                        break;
                    case "gantry":
                        Gantry(mb, rng);
                        break;
                    case "turbine":
                        {
                            TurbineTower(mb);
                            var rotor = new GameObject("Rotor");
                            rotor.transform.SetParent(Root, false);
                            rotor.transform.localPosition = pos + Vector3.up * 22.1f * p.Scale + Quaternion.Euler(0, p.Rot * Mathf.Rad2Deg, 0) * new Vector3(0, 0, 0.9f * p.Scale);
                            rotor.transform.localRotation = Quaternion.Euler(0, p.Rot * Mathf.Rad2Deg, 0);
                            var blades = new MeshBuilder();
                            blades.Sphere(Vector3.zero, 0.55f, 10, 6);
                            for (int i = 0; i < 3; i++)
                            {
                                var o = blades.M;
                                blades.M = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, i * 120), Vector3.one);
                                blades.BoxRot(new Vector3(0, 2.2f, 0), new Vector3(0.75f, 3.6f, 0.14f), new Vector3(0, 12, 0));
                                blades.BoxRot(new Vector3(0.05f, 6.2f, 0), new Vector3(0.45f, 4.6f, 0.1f), new Vector3(0, 18, 0));
                                blades.M = o;
                            }
                            var bgo = Obj("blades", blades.Build("blades"), Mats.Get(Mats.Opaque, new Color(0.9f, 0.9f, 0.88f)), Vector3.zero, Vector3.one * p.Scale, Quaternion.identity, rotor.transform);
                            spinnersPending.Add(new KeyValuePair<Transform, string>(bgo.transform, "pyra_p2"));
                            break;
                        }
                    case "chimney":
                        Chimney(mb);
                        break;
                    case "harborcrane":
                        HarborCrane(mb, rng);
                        break;
                    case "lighthouse":
                        Lighthouse(mb, 0);
                        lampPositions[0].Add(pos + Vector3.up * 19.3f);
                        break;
                    case "filterstation":
                        {
                            var off = new MultiBuilder { UsePalette = true }; var on = new MultiBuilder { UsePalette = true };
                            off.M = on.M = mb.M;
                            FilterStation(off, on);
                            projectSwitches.Add(new Switchable { Off = off.Build("FilterOff", Root), On = on.Build("FilterOn", Root), Project = "pelagia_p2" });
                            break;
                        }
                    case "reef":
                        {
                            var on = new MultiBuilder { UsePalette = true };
                            on.M = mb.M;
                            Reef(on, rng);
                            projectSwitches.Add(new Switchable { On = on.Build("Reef", Root), Project = "pelagia_p3" });
                            break;
                        }
                    case "buoy":
                        NavBuoy(mb, p.Style);
                        break;
                    case "radar":
                        {
                            RadarBase(mb);
                            var white = Mats.Get(Mats.Opaque, new Color(0.9f, 0.92f, 0.95f));
                            var dish = new GameObject("Dish");
                            dish.transform.SetParent(Root, false);
                            dish.transform.localPosition = pos + Vector3.up * 6.6f;
                            var db = new MeshBuilder();
                            db.Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(1.2f, 0.25f), new Vector2(2.5f, 0.8f), new Vector2(3.2f, 1.6f), new Vector2(3.25f, 1.7f) }, 20, true);
                            db.Cylinder(new Vector3(0, 0, 0), 0.08f, 2.6f, 6);
                            db.Sphere(new Vector3(0, 2.6f, 0), 0.2f, 8, 5);
                            Obj("dish", db.Build("dish"), white, Vector3.zero, Vector3.one, Quaternion.Euler(-60, 0, 0), dish.transform);
                            spinnersPending.Add(new KeyValuePair<Transform, string>(dish.transform, p.LitBy));
                            break;
                        }
                    case "tradepost":
                        {
                            var off = new MultiBuilder { UsePalette = true }; var on = new MultiBuilder { UsePalette = true };
                            off.M = on.M = mb.M;
                            off.For(rust).Box(new Vector3(0, 2, 0), new Vector3(8, 4, 5));
                            off.For(Mats.Get(Mats.Opaque, new Color(0.35f, 0.2f, 0.14f))).BoxRot(new Vector3(0, 4.2f, 0), new Vector3(8.6f, 0.2f, 5.6f), new Vector3(0, 0, 6));
                            off.For(woodMat).BoxRot(new Vector3(-1.5f, 1.5f, 2.55f), new Vector3(2.4f, 0.2f, 0.06f), new Vector3(0, 0, 20));
                            on.For(Mats.Get(Mats.Opaque, new Color(0.8f, 0.55f, 0.3f))).Box(new Vector3(0, 2, 0), new Vector3(8, 4, 5));
                            on.For(Mats.Get(Mats.Opaque, new Color(0.3f, 0.65f, 0.55f))).Box(new Vector3(0, 4.15f, 0), new Vector3(8.6f, 0.3f, 5.6f));
                            on.For(Mats.Get(Mats.Emissive, new Color(1f, 0.6f, 0.2f), new Color(2f, 1f, 0.3f))).Box(new Vector3(0, 4.6f, 2.6f), new Vector3(5, 1, 0.2f));
                            for (int k = 0; k < 3; k++) on.For(WindowMat(0)).Box(new Vector3(-2.5f + k * 2.5f, 2.2f, 2.52f), new Vector3(1.6f, 1.4f, 0.06f));
                            projectSwitches.Add(new Switchable { Off = off.Build("TradeOff", Root), On = on.Build("TradeOn", Root), Project = "pyra_p1" });
                            break;
                        }
                    case "recycler":
                        {
                            var on = new MultiBuilder { UsePalette = true };
                            on.M = mb.M;
                            on.For(Mats.Get(Mats.Opaque, new Color(0.3f, 0.65f, 0.55f))).Box(new Vector3(0, 6, -6), new Vector3(20, 3, 3));
                            on.For(Mats.Get(Mats.Emissive, new Color(0.3f, 1f, 0.6f), new Color(0.4f, 2f, 0.8f))).Box(new Vector3(0, 8, -4.4f), new Vector3(10, 1.2f, 0.2f));
                            for (int k = -2; k <= 2; k++) on.For(acMat).Box(new Vector3(k * 4f, 2.2f, -6f), new Vector3(0.5f, 4.4f, 0.5f));
                            for (float x = -9.5f; x < 10f; x += 1f) on.For(dark).CylinderZ(new Vector3(x, 7.55f, -6f), 0.1f, 2.8f, 6);
                            projectSwitches.Add(new Switchable { On = on.Build("Recycler", Root), Project = "pyra_p3" });
                            break;
                        }
                    case "skyline":
                    case "mesa":
                    case "farisland":
                    case "icepeak":
                        break; // BuildSkyline
                    default:
                        mb.For(dark).Box(new Vector3(0, 0.5f, 0), Vector3.one);
                        break;
                }
            }
            BuildStreetFurniture(cb);
            cb.Build("Props", Root, true);
            // Bäume: kahle Teile in Blöcken, lebendige Teile je Bereich und Wachstumsstufe zusammengefasst
            treeDead.Build("TreesDead", Root, true);
            foreach (var kv in treeAlive)
            {
                var go = kv.Value.BuildCombined("TreesAlive_" + kv.Key, Root, true);
                trees.Add(new KeyValuePair<Transform, float>(go.transform, treeAliveThreshold[kv.Key]));
                treeAreas.Add(kv.Key / 4);
            }
            foreach (var kv in spinnersPending) spinnerProjects[kv.Key] = kv.Value;
            spinnersPending.Clear();
        }

        readonly List<KeyValuePair<Transform, string>> spinnersPending = new List<KeyValuePair<Transform, string>>();
        readonly Dictionary<Transform, string> spinnerProjects = new Dictionary<Transform, string>();

        void LateUpdate()
        {
            if (World == null || Root == null) return;
            var ps = World.Planet(Planet);
            foreach (var kv in spinnerProjects)
            {
                if (kv.Key == null) continue;
                bool on = kv.Value == null || (ps.Projects.ContainsKey(kv.Value) && ps.Projects[kv.Value].Done);
                if (on) kv.Key.Rotate(0, 0, 50f * Time.deltaTime, Space.Self);
            }
        }

        ParticleSystem MakeFountain(Vector3 pos)
        {
            var go = new GameObject("Fountain");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.4f; main.startSpeed = 6f; main.startSize = 0.18f; main.gravityModifier = 1f;
            main.startColor = new Color(0.8f, 0.95f, 1f, 0.7f);
            main.maxParticles = 400;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.1f;
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var em = ps.emission; em.rateOverTime = 120f; em.enabled = false;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Get(Mats.Particle, new Color(0.85f, 0.95f, 1f, 0.8f));
            ps.Play();
            return ps;
        }

        /// <summary>Werbetafel des Konzerns KONSUMA aus Leuchtbuchstaben-Blöcken (eigene Marke, Intro-Motiv).</summary>
        void BuildKonsumaSign(Vector3 pos, float rot, float scale)
        {
            var go = new GameObject("KONSUMA");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = pos + Vector3.up * 7.2f * scale;
            go.transform.localRotation = Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0);
            go.transform.localScale = Vector3.one * scale;
            var red = Mats.Get(Mats.Opaque, new Color(0.8f, 0.2f, 0.18f));
            Obj("band", MeshKit.Cube, red, new Vector3(0, 0, 0.18f), new Vector3(7.4f, 1.6f, 0.05f), Quaternion.identity, go.transform);
            var tm = TextLabel(go.transform, "KONSUMA", new Vector3(0, 0, 0.22f), 0.28f, Color.white);
            if (tm != null) tm.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }

        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                return font;
            }
        }

        static Material textMat;
        /// <summary>Material für 3D-Schrift mit Tiefentest (eigener Shader); fällt auf das Schriftmaterial zurück.</summary>
        static Material TextMaterial(Font f)
        {
            if (textMat != null) return textMat;
            var sh = Resources.Load<Shader>("RePlanetText3D") ?? Shader.Find("RePlanet/Text3D");
            if (sh == null || !sh.isSupported) return f.material;
            textMat = new Material(sh) { name = "RP_Text3D", mainTexture = f.material.mainTexture };
            // Dynamische Schriften bauen ihre Textur bei neuen Zeichen neu auf
            Font.textureRebuilt += rebuilt => { if (rebuilt == font && textMat != null) textMat.mainTexture = rebuilt.material.mainTexture; };
            return textMat;
        }

        /// <summary>3D-Schriftzug (TextMesh); gibt null zurück, falls keine Schrift verfügbar ist.</summary>
        public static TextMesh TextLabel(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var f = DefaultFont;
            if (f == null) return null;
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = f;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(f);
            return tm;
        }

        /// <summary>Doppelseitiges, gebogenes Wedelblatt (Palmen, Farne).</summary>
        static void FrondLeaf(MeshBuilder b, Vector3 basePos, float yaw, float len, float width, float lift, float droop)
        {
            var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var side = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var p0 = basePos; var p1 = basePos + dir * len * 0.5f + Vector3.up * lift; var p2 = basePos + dir * len + Vector3.up * (lift - droop);
            float w0 = width * 0.3f, w1 = width, w2 = width * 0.12f;
            b.Face(p0 - side * w0, p1 - side * w1, p1 + side * w1, p0 + side * w0, Vector3.up);
            b.Face(p0 - side * w0, p0 + side * w0, p1 + side * w1, p1 - side * w1, Vector3.down);
            b.Face(p1 - side * w1, p2 - side * w2, p2 + side * w2, p1 + side * w1, Vector3.up);
            b.Face(p1 - side * w1, p1 + side * w1, p2 + side * w2, p2 - side * w2, Vector3.down);
        }

        /// <summary>
        /// Baum/Palme/Kaktus/Eisspitze: der kahle Teil ist immer sichtbar, der lebendige Teil (Krone, Wedel, Blüten,
        /// leuchtende Flechten) erscheint, sobald die Ökologie des Bereichs den Schwellwert des Baums erreicht.
        /// </summary>
        ChunkBuilder treeDead;
        readonly Dictionary<int, MultiBuilder> treeAlive = new Dictionary<int, MultiBuilder>();
        readonly Dictionary<int, float> treeAliveThreshold = new Dictionary<int, float>();

        void BuildTree(Prop p, Rng rng, MultiBuilder dead, MultiBuilder alive)
        {
            var trunk = Mats.Get(Mats.Opaque, new Color(0.35f, 0.26f, 0.18f));
            var trunkD = Mats.Get(Mats.Opaque, new Color(0.28f, 0.21f, 0.15f));
            switch (p.Kind)
            {
                case "tree":
                    {
                        dead.For(trunk).Tube(Vector3.zero, new Vector3(0.1f, 3.4f, 0), 0.24f, 8, false, 0.12f);
                        dead.For(trunkD).Cylinder(Vector3.zero, 0.34f, 0.3f, 8, false, 0.24f);
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i * 1.3f + rng.Range(0f, 0.5f), y = 2.0f + i * 0.35f;
                            var s0 = new Vector3(0.05f, y, 0);
                            var s1 = s0 + new Vector3(Mathf.Cos(a) * 1.3f, 0.9f, Mathf.Sin(a) * 1.3f);
                            dead.For(trunk).Tube(s0, s1, 0.08f, 5, false, 0.04f);
                            dead.For(trunk).Tube(s1, s1 + new Vector3(Mathf.Cos(a + 0.6f) * 0.5f, 0.5f, Mathf.Sin(a + 0.6f) * 0.5f), 0.035f, 4, false, 0.01f);
                        }
                        var leafA = Mats.Get(Mats.Opaque, new Color(0.28f + rng.Next() * 0.1f, 0.52f + rng.Next() * 0.15f, 0.24f));
                        var leafB = Mats.Get(Mats.Opaque, new Color(0.38f, 0.66f, 0.3f));
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 1.05f;
                            var c = new Vector3(Mathf.Cos(a) * (i == 0 ? 0f : 1.1f), 4.1f + (i % 3) * 0.45f + (i == 0 ? 0.8f : 0f), Mathf.Sin(a) * (i == 0 ? 0f : 1.1f));
                            alive.For(i % 2 == 0 ? leafA : leafB).Crumple(c, i == 0 ? 1.6f : 1.15f, 0.8f, i + (int)(p.Pos.x * 3), 0.2f, 9, 6);
                        }
                        var blossom = Mats.Get(Mats.Opaque, rng.Chance(0.5f) ? new Color(0.95f, 0.5f, 0.6f) : new Color(1f, 0.85f, 0.4f));
                        for (int i = 0; i < 9; i++)
                        {
                            float a = rng.Range(0f, 6.28f), r = rng.Range(1.2f, 1.9f);
                            alive.For(blossom).Sphere(new Vector3(Mathf.Cos(a) * r, rng.Range(3.9f, 5.8f), Mathf.Sin(a) * r), 0.16f, 6, 4);
                        }
                        break;
                    }
                case "palm":
                    {
                        var pts = new Vector3[6];
                        float lean = rng.Range(0.3f, 0.9f);
                        for (int i = 0; i < 6; i++) { float t = i / 5f; pts[i] = new Vector3(lean * t * t * 1.4f, t * 5.2f, 0); }
                        for (int i = 0; i < 5; i++)
                        {
                            dead.For(trunk).Tube(pts[i], pts[i + 1], Mathf.Lerp(0.26f, 0.17f, i / 5f), 7, false, Mathf.Lerp(0.26f, 0.17f, (i + 1) / 5f));
                            dead.For(trunkD).Torus(pts[i] + Vector3.up * 0.5f, Mathf.Lerp(0.26f, 0.17f, i / 5f) + 0.02f, 0.035f, 8, 3);
                        }
                        var top = pts[5];
                        var frondM = Mats.Get(Mats.Opaque, new Color(0.22f, 0.58f, 0.32f));
                        var frondM2 = Mats.Get(Mats.Opaque, new Color(0.3f, 0.66f, 0.36f));
                        for (int i = 0; i < 9; i++) FrondLeaf(alive.For(i % 2 == 0 ? frondM : frondM2), top, i * 40 + rng.Range(-8f, 8f), rng.Range(2.4f, 3.1f), 0.32f, 0.5f, 1.4f);
                        var nut = Mats.Get(Mats.Opaque, new Color(0.42f, 0.3f, 0.18f));
                        for (int i = 0; i < 3; i++) alive.For(nut).Sphere(top + new Vector3(Mathf.Cos(i * 2.1f) * 0.22f, -0.2f, Mathf.Sin(i * 2.1f) * 0.22f), 0.14f, 6, 4);
                        // trockene, hängende Wedel am toten Stamm
                        for (int i = 0; i < 4; i++) FrondLeaf(dead.For(Mats.Get(Mats.Opaque, new Color(0.52f, 0.42f, 0.26f))), top + Vector3.down * 0.2f, i * 90 + 30, 1.8f, 0.22f, -0.2f, 1.2f);
                        break;
                    }
                case "deadcactus":
                    {
                        var dry = Mats.Get(Mats.Opaque, new Color(0.45f, 0.35f, 0.25f));
                        dead.For(dry).Tube(Vector3.zero, new Vector3(0, 2.6f, 0), 0.26f, 8, true, 0.22f);
                        dead.For(dry).Tube(new Vector3(0.2f, 1.1f, 0), new Vector3(0.65f, 1.2f, 0), 0.14f, 6);
                        dead.For(dry).Tube(new Vector3(0.65f, 1.2f, 0), new Vector3(0.7f, 1.9f, 0), 0.14f, 6, true, 0.12f);
                        dead.For(dry).Tube(new Vector3(-0.2f, 1.5f, 0.05f), new Vector3(-0.55f, 1.45f, 0.1f), 0.12f, 6);
                        dead.For(dry).Tube(new Vector3(-0.55f, 1.45f, 0.1f), new Vector3(-0.62f, 1.9f, 0.1f), 0.12f, 6, true, 0.1f);
                        var green = Mats.Get(Mats.Opaque, new Color(0.32f, 0.6f, 0.34f));
                        alive.For(green).Tube(Vector3.zero, new Vector3(0, 2.7f, 0), 0.3f, 10, true, 0.26f);
                        alive.For(green).Tube(new Vector3(0.2f, 1.1f, 0), new Vector3(0.68f, 1.2f, 0), 0.18f, 8);
                        alive.For(green).Tube(new Vector3(0.68f, 1.2f, 0), new Vector3(0.74f, 2.05f, 0), 0.18f, 8, true, 0.15f);
                        alive.For(green).Tube(new Vector3(-0.2f, 1.5f, 0.05f), new Vector3(-0.58f, 1.45f, 0.1f), 0.16f, 8);
                        alive.For(green).Tube(new Vector3(-0.58f, 1.45f, 0.1f), new Vector3(-0.65f, 2.0f, 0.1f), 0.16f, 8, true, 0.13f);
                        var bloom = Mats.Get(Mats.Emissive, new Color(1f, 0.45f, 0.3f), new Color(1.2f, 0.4f, 0.2f));
                        foreach (var t in new[] { new Vector3(0, 2.72f, 0), new Vector3(0.74f, 2.07f, 0), new Vector3(-0.65f, 2.02f, 0.1f) })
                            for (int i = 0; i < 5; i++) alive.For(bloom).Sphere(t + new Vector3(Mathf.Cos(i * 1.26f) * 0.1f, 0.03f, Mathf.Sin(i * 1.26f) * 0.1f), 0.07f, 5, 3);
                        break;
                    }
                case "icespike":
                    {
                        var iceM = Mats.Get(Mats.Opaque, new Color(0.8f, 0.92f, 1f), null, 0.9f);
                        for (int i = 0; i < 4; i++)
                        {
                            float a = i * 1.6f, tilt = i == 0 ? 0 : 18f + i * 6f;
                            var o = dead.M;
                            dead.M = o * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * (i == 0 ? 0 : 0.5f), -0.2f, Mathf.Sin(a) * (i == 0 ? 0 : 0.5f)), Quaternion.Euler(Mathf.Sin(a) * tilt, 0, -Mathf.Cos(a) * tilt), Vector3.one);
                            dead.For(iceM).Cylinder(Vector3.zero, i == 0 ? 0.8f : 0.4f, i == 0 ? 4f : 2.2f - i * 0.2f, 6, true, 0f);
                            dead.M = o;
                        }
                        var lichen = Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 0.7f), new Color(0.2f, 0.8f, 0.6f));
                        alive.For(lichen).Crumple(new Vector3(0, 0.05f, 0), 1.2f, 0.18f, (int)p.Pos.x, 0.35f, 10, 3);
                        var flower = Mats.Get(Mats.Emissive, new Color(0.7f, 0.5f, 1f), new Color(1f, 0.6f, 1.6f));
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 1.05f + 0.3f;
                            var s0 = new Vector3(Mathf.Cos(a) * 1.0f, 0, Mathf.Sin(a) * 1.0f);
                            alive.For(lichen).Tube(s0, s0 + Vector3.up * 0.5f, 0.02f, 4);
                            alive.For(flower).Sphere(s0 + Vector3.up * 0.55f, 0.09f, 6, 4, 1.3f);
                        }
                        break;
                    }
            }
        }

        /// <summary>Gepflanzter Setzling je Planet (wächst mit der Zeit).</summary>
        void EcoPlant(MultiBuilder mb, Rng rng)
        {
            switch (Planet)
            {
                case "pelagia":
                    {
                        var cols = new[] { new Color(1f, 0.45f, 0.5f), new Color(1f, 0.7f, 0.3f), new Color(0.6f, 0.4f, 1f), new Color(0.3f, 0.9f, 0.7f) };
                        for (int i = 0; i < 7; i++)
                        {
                            var m = Mats.Get(Mats.Opaque, cols[i % 4]);
                            var b0 = new Vector3(Mathf.Cos(i * 0.9f) * 0.6f, 0, Mathf.Sin(i * 0.9f) * 0.6f);
                            var t0 = b0 + new Vector3(rng.Range(-0.2f, 0.2f), 1.0f + i * 0.12f, rng.Range(-0.2f, 0.2f));
                            mb.For(m).Tube(b0, t0, 0.11f, 6, false, 0.06f);
                            mb.For(m).Tube(Vector3.Lerp(b0, t0, 0.5f), Vector3.Lerp(b0, t0, 0.5f) + new Vector3(0.35f, 0.4f, 0.1f), 0.06f, 5, true, 0.03f);
                        }
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.95f, 0.5f, 0.7f))).BoxJ(new Vector3(-0.4f, 0.6f, 0.4f), new Vector3(1f, 1f, 0.05f), new Vector3(0, 40, 0), 0.12f, 3);
                        for (int i = 0; i < 8; i++) mb.For(Mats.Get(Mats.Emissive, new Color(0.4f, 1f, 0.95f), new Color(0.6f, 2f, 1.8f))).Sphere(new Vector3(Mathf.Cos(i * 0.8f) * 0.9f, 0.25f, Mathf.Sin(i * 0.8f) * 0.9f), 0.07f, 5, 3);
                        break;
                    }
                case "nivalis":
                    {
                        mb.For(Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 0.7f), new Color(0.3f, 1.2f, 0.9f))).Crumple(new Vector3(0, 0.05f, 0), 1.2f, 0.2f, 3, 0.35f, 10, 3);
                        var ice = Mats.Get(Mats.Emissive, new Color(0.65f, 0.85f, 1f), new Color(0.25f, 0.5f, 0.9f), 0.95f);
                        for (int i = 0; i < 5; i++)
                        {
                            var o = mb.M;
                            float a = i * 1.26f;
                            mb.M = o * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f), Quaternion.Euler(Mathf.Sin(a) * 20f, 0, -Mathf.Cos(a) * 20f), Vector3.one);
                            mb.For(ice).Cylinder(Vector3.zero, 0.12f, 0.7f + (i % 3) * 0.3f, 6, false);
                            mb.For(ice).Cylinder(new Vector3(0, 0.7f + (i % 3) * 0.3f, 0), 0.12f, 0.25f, 6, false, 0f);
                            mb.M = o;
                        }
                        for (int i = 0; i < 6; i++) mb.For(Mats.Get(Mats.Emissive, new Color(0.75f, 0.5f, 1f), new Color(1.2f, 0.8f, 2f))).Sphere(new Vector3(Mathf.Cos(i) * 0.9f, 0.4f, Mathf.Sin(i) * 0.9f), 0.08f, 5, 3, 1.3f);
                        break;
                    }
                default:
                    {
                        var trunk = Mats.Get(Mats.Opaque, new Color(0.35f, 0.26f, 0.18f));
                        var green = Mats.Get(Mats.Opaque, Planet == "pyra" ? new Color(0.35f, 0.6f, 0.3f) : new Color(0.3f, 0.62f, 0.25f));
                        var green2 = Mats.Get(Mats.Opaque, Planet == "pyra" ? new Color(0.5f, 0.65f, 0.3f) : new Color(0.42f, 0.7f, 0.32f));
                        mb.For(trunk).Tube(Vector3.zero, new Vector3(0, 2.8f, 0), 0.16f, 7, false, 0.09f);
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.5f, 0.4f, 0.28f))).Tube(new Vector3(0.6f, 0, 0), new Vector3(0.1f, 1.6f, 0), 0.03f, 4); // Stützpfahl
                        for (int i = 0; i < 4; i++) mb.For(i % 2 == 0 ? green : green2).Crumple(new Vector3(Mathf.Cos(i * 1.6f) * (i == 0 ? 0 : 0.7f), 3.2f + (i % 2) * 0.4f, Mathf.Sin(i * 1.6f) * (i == 0 ? 0 : 0.7f)), i == 0 ? 1.3f : 0.9f, 0.8f, i + 11, 0.2f, 8, 5);
                        var bl = Mats.Get(Mats.Opaque, new Color(1f, 0.6f, 0.75f));
                        for (int i = 0; i < 7; i++) mb.For(bl).Sphere(new Vector3(Mathf.Cos(i * 0.9f) * 1.1f, 3.3f + (i % 3) * 0.3f, Mathf.Sin(i * 0.9f) * 1.1f), 0.13f, 6, 4);
                        var fl = Mats.Get(Mats.Opaque, new Color(1f, 0.85f, 0.3f));
                        for (int i = 0; i < 6; i++) { var q = new Vector3(Mathf.Cos(i * 1.05f) * 0.9f, 0, Mathf.Sin(i * 1.05f) * 0.9f); mb.For(green).Tube(q, q + Vector3.up * 0.35f, 0.015f, 4); mb.For(fl).Sphere(q + Vector3.up * 0.38f, 0.06f, 5, 3); }
                        break;
                    }
            }
        }

        // ================================================================== Punkte in der Welt
        void BuildSpots()
        {
            var dark = Mats.Get(Mats.Opaque, new Color(0.25f, 0.25f, 0.27f));
            foreach (var s in Layout.Repairs)
            {
                var pos = P(s.Pos);
                var broken = new GameObject("RepairBroken"); broken.transform.SetParent(Root, false); broken.transform.localPosition = pos;
                var fixedGo = new GameObject("RepairFixed"); fixedGo.transform.SetParent(Root, false); fixedGo.transform.localPosition = pos;
                var bmb = new MultiBuilder { UsePalette = true };
                var fmb = new MultiBuilder { UsePalette = true };
                if (Planet == "pelagia")
                {
                    // kaputt: verrostete, halb gesunkene Boje mit abgeknicktem Mast; repariert: Seezeichen mit Licht
                    bmb.M = Matrix4x4.TRS(new Vector3(0, -0.3f, 0), Quaternion.Euler(24, s.Yaw * Mathf.Rad2Deg, 14), Vector3.one);
                    bmb.For(Mats.Get(Mats.Opaque, new Color(0.5f, 0.3f, 0.25f))).Cylinder(new Vector3(0, -0.5f, 0), 0.75f, 0.9f, 12, true, 0.6f);
                    bmb.For(Mats.Get(Mats.Opaque, new Color(0.35f, 0.2f, 0.15f))).Beam(new Vector3(0, 0.4f, 0), new Vector3(0.1f, 1.2f, 0), 0.08f);
                    bmb.For(Mats.Get(Mats.Opaque, new Color(0.35f, 0.2f, 0.15f))).Beam(new Vector3(0.1f, 1.2f, 0), new Vector3(0.7f, 1.0f, 0.2f), 0.08f);
                    bmb.For(glassDark).Sphere(new Vector3(0.75f, 0.95f, 0.2f), 0.12f, 6, 4);
                    NavBuoy(fmb, 1);
                }
                else
                {
                    var lit = Planet == "nivalis" ? new Color(1f, 0.5f, 0.2f) : Planet == "pyra" ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f);
                    var pole = Mats.Get(Mats.Opaque, new Color(0.24f, 0.26f, 0.27f));
                    var box = Mats.Get(Mats.Opaque, new Color(0.45f, 0.5f, 0.42f));
                    // kaputt: umgeknickter Mast, Leuchte am Boden, heraushängendes Kabel, offener Schaltkasten
                    bmb.For(plinthMat).Cylinder(Vector3.zero, 0.24f, 0.5f, 8, true, 0.2f);
                    bmb.For(pole).Tube(new Vector3(0, 0.5f, 0), new Vector3(0.1f, 1.9f, 0), 0.1f, 8);
                    bmb.For(pole).Tube(new Vector3(0.1f, 1.9f, 0), new Vector3(1.9f, 0.3f, 0.4f), 0.08f, 8);
                    bmb.For(glassDark).BoxRot(new Vector3(2.2f, 0.15f, 0.5f), new Vector3(0.45f, 0.2f, 0.7f), new Vector3(10, 40, 70));
                    bmb.For(darkMat).Tube(new Vector3(0.1f, 1.9f, 0), new Vector3(0.3f, 1.0f, -0.2f), 0.02f, 4);
                    bmb.For(darkMat).Tube(new Vector3(0.3f, 1.0f, -0.2f), new Vector3(0.5f, 0.05f, -0.4f), 0.02f, 4);
                    bmb.For(box).Box(new Vector3(0, 1.0f, -0.2f), new Vector3(0.4f, 0.55f, 0.22f));
                    bmb.For(box).BoxRot(new Vector3(0.28f, 1.0f, -0.42f), new Vector3(0.04f, 0.5f, 0.38f), new Vector3(0, 55, 0));
                    bmb.For(Mats.Get(Mats.Emissive, new Color(1f, 0.6f, 0.2f), new Color(2f, 1f, 0.3f))).Sphere(new Vector3(0.45f, 0.06f, -0.4f), 0.06f, 6, 4);
                    // repariert: gerader Mast mit leuchtendem Kopf, geschlossener Kasten mit grüner Lampe
                    fmb.For(plinthMat).Cylinder(Vector3.zero, 0.24f, 0.5f, 8, true, 0.2f);
                    fmb.For(pole).Cylinder(new Vector3(0, 0.5f, 0), 0.1f, 3.1f, 8, true, 0.07f);
                    fmb.For(pole).Cylinder(new Vector3(0, 3.55f, 0), 0.3f, 0.12f, 10, true, 0.36f);
                    fmb.For(Mats.Get(Mats.Emissive, lit, lit * 2.2f)).Sphere(new Vector3(0, 3.85f, 0), 0.3f, 10, 6);
                    fmb.For(pole).Cylinder(new Vector3(0, 4.1f, 0), 0.32f, 0.1f, 10, true, 0.05f);
                    fmb.For(box).Box(new Vector3(0, 1.0f, -0.2f), new Vector3(0.4f, 0.55f, 0.22f));
                    fmb.For(Mats.Get(Mats.Emissive, new Color(0.3f, 1f, 0.4f), new Color(0.4f, 2f, 0.6f))).Box(new Vector3(0, 1.15f, -0.32f), new Vector3(0.08f, 0.08f, 0.02f));
                }
                bmb.Build("RepairBrokenMesh", broken.transform, true);
                fmb.Build("RepairFixedMesh", fixedGo.transform, true);
                repairVisuals[s.Id] = new[] { broken, fixedGo };
            }
            foreach (var s in Layout.Eco)
            {
                var go = new GameObject("Eco_" + s.Id);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = P(s.Pos);
                // Pflanzstelle (Markierung) bleibt immer sichtbar
                Obj("bed", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.35f, 0.25f, 0.18f)), P(s.Pos) + Vector3.up * 0.02f, new Vector3(2.2f, 0.06f, 2.2f), Quaternion.identity, Root, false);
                var plantB = new MultiBuilder { UsePalette = true };
                EcoPlant(plantB, new Rng((int)Hash.Fnv1a(s.Id)));
                plantB.Build("Plant", go.transform, true);
                ecoVisuals[s.Id] = go.transform;
                go.SetActive(false);
            }
            foreach (var s in Layout.LoreSpots)
            {
                var go = new GameObject("Lore_" + s.Id);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = P(s.Pos);
                Obj("page", MeshKit.Cube, Mats.Get(Mats.Emissive, new Color(1f, 0.9f, 0.6f), new Color(1.6f, 1.3f, 0.6f)), new Vector3(0, 0.9f, 0), new Vector3(0.5f, 0.02f, 0.4f), Quaternion.Euler(20, 30, 10), go.transform, false);
                go.AddComponent<Floater>();
                loreVisuals[s.Id] = go;
            }
            foreach (var s in Layout.Shelters) BuildShelter(P(s.Pos), s.Yaw, false);
            for (int a = 0; a < 3; a++)
            {
                var site = P(Layout.ProjectSites[a]);
                var go = new GameObject("ProjectSite" + a);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = site;
                ProjectSiteModel(go.transform);
                projectSites[GameData.ProjectId(Planet, a)] = go.transform;
                // Projektzeichen (Stern) statt Schriftzug, dreht sich zur Kamera
                IconBillboard(go.transform, new Vector3(0, 6.2f, 0), 1.1f, SurfaceLook.Icon.Star);
            }
        }

        /// <summary>
        /// Lichtpunkt-Laterne (geht an, wenn die Zone aufgeräumt ist): Sockel mit Stufen, kannelierter Mast mit Zierringen,
        /// Laternenkopf mit Streben um die Leuchtkugel, Dach mit Spitze. Ein Mesh (Palette + Leuchtkugel).
        /// </summary>
        void ZoneLampModel(Transform parent, Material globe)
        {
            var zmb = new MultiBuilder { UsePalette = true, GroundY = 0f };
            var iron = Paint(new Color(0.16f, 0.2f, 0.2f), 0.5f);
            var brass = Steel(new Color(0.7f, 0.58f, 0.32f));
            zmb.For(Conc(new Color(0.5f, 0.49f, 0.46f))).BevelBox(new Vector3(0, 0.12f, 0), new Vector3(0.9f, 0.24f, 0.9f), 0.04f);
            zmb.For(iron).Cylinder(new Vector3(0, 0.24f, 0), 0.3f, 0.35f, 16, true, 0.2f);
            zmb.For(iron).Cylinder(new Vector3(0, 0.59f, 0), 0.12f, 2.6f, 12, true, 0.09f);
            for (int k = 0; k < 8; k++) { float a = k * 45f; zmb.For(iron).Box(Quaternion.Euler(0, a, 0) * new Vector3(0, 1.4f, 0.11f), new Vector3(0.03f, 1.5f, 0.03f)); }
            foreach (var y in new[] { 0.62f, 2.2f, 3.05f }) zmb.For(brass).Torus(new Vector3(0, y, 0), 0.13f, 0.035f, 14, 4);
            zmb.For(iron).Cylinder(new Vector3(0, 3.0f, 0), 0.32f, 0.08f, 12);
            for (int k = 0; k < 4; k++) { float a = k * 90f + 45f; zmb.For(iron).Beam(Quaternion.Euler(0, a, 0) * new Vector3(0, 3.05f, 0.3f), Quaternion.Euler(0, a, 0) * new Vector3(0, 3.78f, 0.3f), 0.03f); }
            zmb.For(globe).Sphere(new Vector3(0, 3.4f, 0), 0.34f, 16, 10);
            zmb.For(iron).Cylinder(new Vector3(0, 3.78f, 0), 0.42f, 0.18f, 12, true, 0.12f);
            zmb.For(brass).Sphere(new Vector3(0, 4.02f, 0), 0.07f, 8, 5);
            zmb.Build("ZoneLampMesh", parent, true);
        }

        /// <summary>Leuchtendes Piktogramm, das sich zur Kamera dreht (ersetzt schwebende Namensschilder).</summary>
        GameObject IconBillboard(Transform parent, Vector3 localPos, float size, int icon)
        {
            var b = new MeshBuilder();
            b.UVRect = SurfaceLook.IconRect(icon);
            float h = size * 0.5f;
            // Billboard dreht +Z von der Kamera weg: sichtbare Seite zeigt nach −Z, rechts = +X
            b.Quad(new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), Vector3.back);
            var go = Obj("Piktogramm", b.Build("icon"), SurfaceLook.SignMaterial(), localPos, Vector3.one, Quaternion.identity, parent, false);
            go.AddComponent<Billboard>();
            return go;
        }

        /// <summary>Projektplatz: gefastes Terminal mit Projekt-Piktogramm, Gerüst mit Streben, Bohlen und Absperrband.</summary>
        void ProjectSiteModel(Transform parent)
        {
            var pmb = new MultiBuilder { UsePalette = true, GroundY = 0f };
            var orange = Paint(new Color(0.9f, 0.62f, 0.15f), 0.45f);
            var dark = Paint(new Color(0.22f, 0.23f, 0.25f), 0.4f);
            pmb.For(Conc(new Color(0.5f, 0.49f, 0.47f))).BevelBox(new Vector3(0, 0.08f, 0), new Vector3(1.6f, 0.16f, 1.1f), 0.03f);
            pmb.For(dark).BevelBox(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.0f, 0.9f), 0.07f);
            pmb.For(Steel(new Color(0.3f, 0.31f, 0.33f))).BevelBoxRot(new Vector3(0, 1.62f, 0.44f), new Vector3(1.2f, 0.9f, 0.06f), new Vector3(-8, 0, 0), 0.02f);
            var o = pmb.M;
            pmb.M = Matrix4x4.TRS(new Vector3(0, 1.62f, 0.475f), Quaternion.Euler(-8, 0, 0), Vector3.one);
            var sb = pmb.For(SurfaceLook.SignMaterial());
            sb.UVRect = SurfaceLook.IconRect(SurfaceLook.Icon.Star);
            sb.Quad(new Vector3(0.4f, -0.4f, 0), new Vector3(-0.4f, -0.4f, 0), new Vector3(-0.4f, 0.4f, 0), new Vector3(0.4f, 0.4f, 0), Vector3.forward);
            pmb.M = o;
            HazardBand(pmb, new Vector3(0, 2.18f, 0.46f), 0, 1.4f, 0.12f, 0.02f);
            pmb.For(Glow(new Color(1f, 0.6f, 0.2f), 1.2f)).Box(new Vector3(0, 0.25f, 0.455f), new Vector3(1.2f, 0.04f, 0.02f));
            // Gerüst: vier Stützen mit Fußplatten, Kreuzstreben, Bohlen, Absperrband
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3);
                pmb.For(orange).Box(p + Vector3.up * 2.5f, new Vector3(0.14f, 5f, 0.14f));
                pmb.For(dark).Box(p + Vector3.up * 0.02f, new Vector3(0.4f, 0.04f, 0.4f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                pmb.For(orange).Beam(new Vector3(-3, 0.3f, s * 3), new Vector3(3, 4.7f, s * 3), 0.06f);
                pmb.For(orange).Beam(new Vector3(s * 3, 0.3f, -3), new Vector3(s * 3, 4.7f, 3), 0.06f);
                pmb.For(orange).Box(new Vector3(0, 4.9f, s * 3), new Vector3(6.1f, 0.1f, 0.1f));
                pmb.For(orange).Box(new Vector3(s * 3, 4.9f, 0), new Vector3(0.1f, 0.1f, 6.1f));
            }
            for (int k = 0; k < 5; k++) pmb.For(woodMat).Box(new Vector3(-2.4f + k * 1.2f, 4.98f, -3f), new Vector3(1.1f, 0.05f, 0.35f));
            for (int s = -1; s <= 1; s += 2)
            {
                pmb.For(Paint(new Color(0.95f, 0.3f, 0.2f), 0.4f)).Box(new Vector3(0, 0.9f, s * 3.05f), new Vector3(6f, 0.07f, 0.01f));
                pmb.For(Paint(new Color(0.95f, 0.3f, 0.2f), 0.4f)).Box(new Vector3(s * 3.05f, 0.9f, 0), new Vector3(0.01f, 0.07f, 6f));
            }
            pmb.Build("ProjectSiteMesh", parent, true);
        }

        /// <summary>Unterschlupf (planetentypisch) – auch für selbst gebaute Notunterschlüpfe.</summary>
        public GameObject BuildShelter(Vector3 pos, float yaw, bool emergency)
        {
            var mb = new MultiBuilder { UsePalette = true };
            mb.M = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), Vector3.one);
            var dark = Mats.Get(Mats.Opaque, new Color(0.25f, 0.25f, 0.27f));
            var warm = Mats.Get(Mats.Emissive, new Color(1f, 0.8f, 0.5f), new Color(1.2f, 0.8f, 0.4f));
            if (emergency)
            {
                var tent = Mats.Get(Mats.Opaque, new Color(1f, 0.55f, 0.2f));
                mb.For(tent).BoxRot(new Vector3(-0.9f, 1.1f, 0), new Vector3(0.1f, 2.5f, 3f), new Vector3(0, 0, -35));
                mb.For(tent).BoxRot(new Vector3(0.9f, 1.1f, 0), new Vector3(0.1f, 2.5f, 3f), new Vector3(0, 0, 35));
                mb.For(warm).Sphere(new Vector3(0, 0.3f, 0.2f), 0.15f, 6, 4);
            }
            else switch (Planet)
                {
                    case "pyra":
                        {
                            var rock = Mats.Get(Mats.Opaque, new Color(0.55f, 0.28f, 0.2f));
                            mb.For(rock).Blob(new Vector3(0, 0, -1.5f), 3.2f, 3.4f, 10, 4, (int)(pos.x * 3), 0.2f);
                            mb.For(rock).BoxRot(new Vector3(0, 3.1f, 0.6f), new Vector3(5f, 0.8f, 3f), new Vector3(-8, 0, 0));
                            break;
                        }
                    case "pelagia":
                        {
                            var wood = Mats.Get(Mats.Opaque, new Color(0.5f, 0.36f, 0.24f));
                            mb.For(wood).Box(new Vector3(0, 1.5f, -1.5f), new Vector3(4, 3, 0.2f));
                            mb.For(wood).Box(new Vector3(-2, 1.5f, 0), new Vector3(0.2f, 3, 3));
                            mb.For(wood).Box(new Vector3(2, 1.5f, 0), new Vector3(0.2f, 3, 3));
                            mb.For(Mats.Get(Mats.Opaque, new Color(0.3f, 0.5f, 0.6f))).BoxRot(new Vector3(0, 3.2f, 0), new Vector3(4.6f, 0.2f, 3.6f), new Vector3(-10, 0, 0));
                            break;
                        }
                    case "nivalis":
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.92f, 0.96f, 1f))).Sphere(Vector3.zero, 2.4f, 12, 6, 0.8f);
                        mb.For(dark).Box(new Vector3(0, 0.8f, 2.2f), new Vector3(1.4f, 1.6f, 0.6f));
                        break;
                    default:
                        mb.For(dark).Box(new Vector3(-1.8f, 1.3f, 0), new Vector3(0.12f, 2.6f, 0.12f));
                        mb.For(dark).Box(new Vector3(1.8f, 1.3f, 0), new Vector3(0.12f, 2.6f, 0.12f));
                        mb.For(Mats.Get(Mats.Fade, new Color(0.75f, 0.85f, 0.9f, 0.4f))).Box(new Vector3(0, 1.3f, -1f), new Vector3(3.6f, 2.4f, 0.05f));
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.3f, 0.55f, 0.5f))).Box(new Vector3(0, 2.7f, -0.2f), new Vector3(4f, 0.15f, 2.2f));
                        break;
                }
            mb.For(warm).Box(new Vector3(0, 2.4f, -0.8f), new Vector3(0.4f, 0.1f, 0.2f));
            var go = mb.Build(emergency ? "Notunterschlupf" : "Unterschlupf", Root, true);
            // Unterschlupf-Zeichen (Haus) statt Schriftzug
            IconBillboard(go.transform, pos + Vector3.up * 3.9f, emergency ? 0.6f : 0.75f, SurfaceLook.Icon.House);
            return go;
        }

        // ================================================================== Müllberge & Horizont
        void BuildMounds()
        {
            var baseCols = new[] { Planet == "pyra" ? new Color(0.45f, 0.3f, 0.22f) : Planet == "nivalis" ? new Color(0.6f, 0.65f, 0.7f) : new Color(0.45f, 0.42f, 0.36f), new Color(0.5f, 0.45f, 0.35f) };
            var junk = new[] { new Color(0.72f, 0.36f, 0.24f), new Color(0.3f, 0.5f, 0.66f), new Color(0.8f, 0.72f, 0.4f), new Color(0.42f, 0.55f, 0.36f) };
            for (int i = 0; i < Layout.Mounds.Count; i++)
            {
                var m = Layout.Mounds[i];
                var go = Obj("Mound", MeshKit.Mound(i % 7), Mats.Get(Mats.Opaque, baseCols[i % 2]), P(m.Pos), new Vector3(m.Radius, m.Height, m.Radius), Quaternion.Euler(0, i * 47, 0));
                go.GetComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    Mats.Get(Mats.Opaque, baseCols[i % 2]), Mats.Get(Mats.Opaque, junk[i % junk.Length]),
                    Mats.Get(Mats.Metal, new Color(0.5f, 0.48f, 0.45f)), Mats.Get(Mats.Opaque, new Color(0.14f, 0.14f, 0.15f)),
                };
                mounds.Add(go.transform);
            }
        }

        /// <summary>Horizont und dekorative Müllmassen (Hintergrund-Ring, fernes Gelände, Streumüll) – siehe <see cref="Backdrop"/>.</summary>
        void BuildSkyline()
        {
            backdrop = new Backdrop(Planet, Layout, Root) { RoofAt = RoofHeight };
            try { backdrop.Build(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        /// <summary>Menge des Streumülls eines Bereichs: voll im Ausgangszustand, 15 % Rest nach dem Hauptmüll, weg nach dem Projekt.</summary>
        public static float LitterFactor(PlanetState ps, int area)
        {
            float c = Rules.Cleanliness(ps, area);
            bool done = ps.Projects[GameData.ProjectId(ps.Id, area)].Done;
            return Mathf.Clamp01(1f - c / GameData.AreaCleanThreshold) * 0.85f + (done ? 0f : 0.15f);
        }
    }

    /// <summary>Dreht ein Objekt zur Kamera (Namensschilder).</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position);
        }
    }

    /// <summary>Sanftes Schweben und Drehen (Fundstücke, Hinweise).</summary>
    public class Floater : MonoBehaviour
    {
        Vector3 basePos;
        void Start() { basePos = transform.localPosition; }
        void Update()
        {
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
            transform.Rotate(0, 40f * Time.deltaTime, 0);
        }
    }
}
