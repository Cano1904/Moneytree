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
            var cb = new ChunkBuilder(60f);
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
                            Obj("pole", MeshKit.Cylinder, dark, new Vector3(0, 1.6f, 0), new Vector3(0.18f, 3.2f, 0.18f), Quaternion.identity, go.transform);
                            Obj("globe", MeshKit.Sphere, m, new Vector3(0, 3.4f, 0), Vector3.one * 0.7f, Quaternion.identity, go.transform, false);
                            Obj("cap", MeshKit.Cylinder, dark, new Vector3(0, 3.8f, 0), new Vector3(0.5f, 0.12f, 0.5f), Quaternion.identity, go.transform);
                            Obj("base", MeshKit.Cylinder, dark, new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.3f, 0.5f), Quaternion.identity, go.transform);
                            zoneLamps[p.Style] = go;
                            break;
                        }
                    case "tree":
                    case "palm":
                    case "deadcactus":
                    case "icespike":
                        BuildTree(p, rng);
                        break;
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
                            var off = new MultiBuilder(); var on = new MultiBuilder();
                            off.M = on.M = mb.M;
                            FilterStation(off, on);
                            projectSwitches.Add(new Switchable { Off = off.Build("FilterOff", Root), On = on.Build("FilterOn", Root), Project = "pelagia_p2" });
                            break;
                        }
                    case "reef":
                        {
                            var on = new MultiBuilder();
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
                            db.Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(1.2f, 0.25f), new Vector2(2.5f, 0.8f), new Vector2(3.2f, 1.6f), new Vector2(3.25f, 1.7f) }, 20);
                            db.Cylinder(new Vector3(0, 0, 0), 0.08f, 2.6f, 6);
                            db.Sphere(new Vector3(0, 2.6f, 0), 0.2f, 8, 5);
                            Obj("dish", db.Build("dish"), white, Vector3.zero, Vector3.one, Quaternion.Euler(-60, 0, 0), dish.transform);
                            spinnersPending.Add(new KeyValuePair<Transform, string>(dish.transform, p.LitBy));
                            break;
                        }
                    case "tradepost":
                        {
                            var off = new MultiBuilder(); var on = new MultiBuilder();
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
                            var on = new MultiBuilder();
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

        void BuildTree(Prop p, Rng rng)
        {
            var go = new GameObject(p.Kind);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = P(p.Pos);
            go.transform.localRotation = Quaternion.Euler(0, p.Rot * Mathf.Rad2Deg, 0);
            go.transform.localScale = Vector3.one * p.Scale;
            var dead = new GameObject("dead"); dead.transform.SetParent(go.transform, false);
            var alive = new GameObject("alive"); alive.transform.SetParent(go.transform, false);
            var trunk = Mats.Get(Mats.Opaque, new Color(0.35f, 0.26f, 0.18f));
            switch (p.Kind)
            {
                case "tree":
                    Obj("trunk", MeshKit.Cylinder, trunk, new Vector3(0, 1.8f, 0), new Vector3(0.35f, 3.6f, 0.35f), Quaternion.identity, dead.transform);
                    Obj("branch", MeshKit.Cube, trunk, new Vector3(0.4f, 3.2f, 0), new Vector3(1.2f, 0.12f, 0.12f), Quaternion.Euler(0, 0, 35), dead.transform);
                    Obj("crown", MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(0.3f + rng.Next() * 0.1f, 0.55f + rng.Next() * 0.15f, 0.25f)), new Vector3(0, 4.2f, 0), new Vector3(3.4f, 3f, 3.4f), Quaternion.identity, alive.transform);
                    Obj("crown2", MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(0.9f, 0.4f, 0.45f)), new Vector3(0.9f, 5f, 0.4f), new Vector3(1.4f, 1.2f, 1.4f), Quaternion.identity, alive.transform);
                    break;
                case "palm":
                    Obj("trunk", MeshKit.Cylinder, trunk, new Vector3(0.3f, 2.5f, 0), new Vector3(0.3f, 5f, 0.3f), Quaternion.Euler(0, 0, -8), dead.transform);
                    for (int i = 0; i < 6; i++)
                        Obj("leaf", MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.2f, 0.55f, 0.3f)), new Vector3(0.7f, 4.9f, 0), new Vector3(2.8f, 0.08f, 0.7f), Quaternion.Euler(0, i * 60, -25), alive.transform);
                    break;
                case "deadcactus":
                    Obj("trunk", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.45f, 0.35f, 0.25f)), new Vector3(0, 1.2f, 0), new Vector3(0.5f, 2.4f, 0.5f), Quaternion.identity, dead.transform);
                    Obj("arm", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.45f, 0.35f, 0.25f)), new Vector3(0.45f, 1.5f, 0), new Vector3(0.3f, 1.0f, 0.3f), Quaternion.identity, dead.transform);
                    Obj("green", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.35f, 0.6f, 0.3f)), new Vector3(0, 1.3f, 0), new Vector3(0.55f, 2.6f, 0.55f), Quaternion.identity, alive.transform);
                    Obj("bloom", MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(1f, 0.45f, 0.3f)), new Vector3(0, 2.7f, 0), Vector3.one * 0.5f, Quaternion.identity, alive.transform);
                    break;
                case "icespike":
                    Obj("spike", MeshKit.Get("spike", b => b.Cylinder(Vector3.zero, 0.8f, 4f, 6, true, 0f)), Mats.Get(Mats.Opaque, new Color(0.8f, 0.92f, 1f), null, 0.9f), Vector3.zero, Vector3.one, Quaternion.Euler(rng.Range(-10f, 10f), 0, rng.Range(-10f, 10f)), dead.transform);
                    Obj("lichen", MeshKit.Sphere, Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 0.7f), new Color(0.2f, 0.8f, 0.6f)), new Vector3(0, 0.2f, 0), new Vector3(2f, 0.4f, 2f), Quaternion.identity, alive.transform);
                    break;
            }
            // Bäume bleiben kahl, bis die Ökologie des Bereichs wächst (je Baum eigener Schwellwert)
            float threshold = 0.05f + rng.Next() * 0.9f;
            trees.Add(new KeyValuePair<Transform, float>(alive.transform, threshold));
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
                if (Planet == "pelagia")
                {
                    Obj("buoy", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.5f, 0.3f, 0.25f)), new Vector3(0, -0.2f, 0), new Vector3(1.2f, 1.2f, 1.2f), Quaternion.Euler(20, 0, 15), broken.transform);
                    Obj("buoy", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.95f, 0.8f, 0.2f)), new Vector3(0, 0.2f, 0), new Vector3(1.2f, 1.6f, 1.2f), Quaternion.identity, fixedGo.transform);
                    Obj("light", MeshKit.Sphere, Mats.Get(Mats.Emissive, new Color(1f, 0.4f, 0.2f), new Color(2f, 0.6f, 0.2f)), new Vector3(0, 1.2f, 0), Vector3.one * 0.5f, Quaternion.identity, fixedGo.transform, false);
                }
                else
                {
                    Obj("pole", MeshKit.Cylinder, dark, new Vector3(0, 1.4f, 0), new Vector3(0.18f, 2.8f, 0.18f), Quaternion.Euler(0, 0, 25), broken.transform);
                    Obj("spark", MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.5f, 0.35f, 0.25f)), new Vector3(0.9f, 0.3f, 0), new Vector3(0.6f, 0.3f, 0.4f), Quaternion.identity, broken.transform);
                    Obj("pole", MeshKit.Cylinder, dark, new Vector3(0, 1.8f, 0), new Vector3(0.18f, 3.6f, 0.18f), Quaternion.identity, fixedGo.transform);
                    var lit = Planet == "nivalis" ? new Color(1f, 0.5f, 0.2f) : Planet == "pyra" ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f);
                    Obj("head", MeshKit.Sphere, Mats.Get(Mats.Emissive, lit, lit * 2.2f), new Vector3(0, 3.8f, 0), Vector3.one * 0.6f, Quaternion.identity, fixedGo.transform, false);
                }
                repairVisuals[s.Id] = new[] { broken, fixedGo };
            }
            foreach (var s in Layout.Eco)
            {
                var go = new GameObject("Eco_" + s.Id);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = P(s.Pos);
                // Pflanzstelle (Markierung) bleibt immer sichtbar
                Obj("bed", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.35f, 0.25f, 0.18f)), P(s.Pos) + Vector3.up * 0.02f, new Vector3(2.2f, 0.06f, 2.2f), Quaternion.identity, Root, false);
                var plant = new GameObject("Plant"); plant.transform.SetParent(go.transform, false);
                if (Planet == "pelagia")
                {
                    for (int i = 0; i < 5; i++) Obj("coral", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new[] { new Color(1f, 0.45f, 0.5f), new Color(1f, 0.7f, 0.3f), new Color(0.6f, 0.4f, 1f) }[i % 3]), new Vector3((i - 2) * 0.5f, 0.6f, (i % 2) * 0.4f), new Vector3(0.3f, 1.2f + i * 0.2f, 0.3f), Quaternion.Euler(i * 7, 0, i * 5), plant.transform);
                }
                else if (Planet == "nivalis")
                {
                    Obj("lichen", MeshKit.Sphere, Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 0.7f), new Color(0.3f, 1.2f, 0.9f)), new Vector3(0, 0.2f, 0), new Vector3(2.4f, 0.5f, 2.4f), Quaternion.identity, plant.transform, false);
                }
                else
                {
                    var green = Mats.Get(Mats.Opaque, Planet == "pyra" ? new Color(0.35f, 0.6f, 0.3f) : new Color(0.3f, 0.62f, 0.25f));
                    Obj("trunk", MeshKit.Cylinder, Mats.Get(Mats.Opaque, new Color(0.35f, 0.26f, 0.18f)), new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3f, 0.3f), Quaternion.identity, plant.transform);
                    Obj("crown", MeshKit.Sphere, green, new Vector3(0, 3.6f, 0), new Vector3(3f, 2.6f, 3f), Quaternion.identity, plant.transform);
                    Obj("flower", MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(1f, 0.6f, 0.75f)), new Vector3(0.8f, 4.3f, 0.5f), Vector3.one * 0.6f, Quaternion.identity, plant.transform);
                }
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
                Obj("terminal", MeshKit.Cube, dark, new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.2f, 0.9f), Quaternion.identity, go.transform);
                Obj("screen", MeshKit.Cube, Mats.Get(Mats.Emissive, new Color(1f, 0.6f, 0.2f), new Color(1.8f, 0.9f, 0.3f)), new Vector3(0, 1.6f, 0.46f), new Vector3(1.1f, 0.8f, 0.04f), Quaternion.identity, go.transform, false);
                for (int i = 0; i < 4; i++) Obj("scaffold", MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.85f, 0.6f, 0.15f)), new Vector3(i % 2 == 0 ? -3 : 3, 2.5f, i < 2 ? -3 : 3), new Vector3(0.2f, 5f, 0.2f), Quaternion.identity, go.transform);
                projectSites[GameData.ProjectId(Planet, a)] = go.transform;
                var tm = TextLabel(go.transform, GameData.Projects[GameData.ProjectId(Planet, a)].Name, new Vector3(0, 6.2f, 0), 0.36f, new Color(1f, 0.85f, 0.5f));
                if (tm != null) tm.gameObject.AddComponent<Billboard>();
            }
        }

        /// <summary>Unterschlupf (planetentypisch) – auch für selbst gebaute Notunterschlüpfe.</summary>
        public GameObject BuildShelter(Vector3 pos, float yaw, bool emergency)
        {
            var mb = new MultiBuilder();
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
            var tm = TextLabel(go.transform, emergency ? "Notunterschlupf" : Def.ShelterName, pos + Vector3.up * 3.9f, 0.24f, new Color(0.8f, 1f, 0.9f));
            if (tm != null) tm.gameObject.AddComponent<Billboard>();
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
            backdrop = new Backdrop(Planet, Layout, Root);
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
