using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Intro als Echtzeit-Zwischensequenz (~100 s), synchron zum Intro-Score.
    /// Die Handlung folgt der Grundidee von WALL·E – vermüllte Erde, ein Konsumkonzern, die Menschen fliehen auf Archen,
    /// die Roboter geben auf, nur einer arbeitet weiter und findet einen Keimling – mit eigenen Figuren, Namen und Bildern:
    /// Skyline mit Müllwürfel-Türmen, KONSUMA-Megastore, startende Archen, abschaltende Roboter, MIKOs Zuhause im rostigen
    /// Lieferwagen mit blauem Licht, der Keimling im Greifarm, MIKO auf dem Transportschiff vor dem Planeten.
    /// </summary>
    public class IntroDirector : MonoBehaviour
    {
        Transform stage;
        Action done;
        bool playing, freeRun;
        float t, skipHold, startDelay;
        readonly Dictionary<string, Transform> shots = new Dictionary<string, Transform>();
        readonly List<Material> robotEyes = new List<Material>();
        readonly List<Transform> arks = new List<Transform>();
        RobotModel miko, mikoShip, mikoClose;
        Transform van, sprout, cubeStack, pressCube, planetSphere, shipSpace;
        Material vanLight;
        CameraClearFlags oldClear;
        Color oldBg;
        bool oldFog;
        static readonly Vector3 Origin = new Vector3(3000, 0, 3000);

        void Start()
        {
            if (GameApp.I != null) GameApp.I.OnIntroRequested += Play;
        }

        public void Play(Action onDone)
        {
            done = onDone;
            Build();
            playing = true;
            t = 0; skipHold = 0; startDelay = 0; freeRun = false;
            if (CameraRig.I != null) CameraRig.I.Cinematic = true;
            var cam = Camera.main;
            if (cam != null) { oldClear = cam.clearFlags; oldBg = cam.backgroundColor; }
            oldFog = RenderSettings.fog;
            AudioManager.PlayIntro();
        }

        void Finish()
        {
            if (!playing) return;
            playing = false;
            AudioManager.StopIntro();
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = oldClear; cam.backgroundColor = oldBg; }
            RenderSettings.fog = oldFog;
            if (CameraRig.I != null) CameraRig.I.Cinematic = false;
            if (Atmosphere.I != null) { Atmosphere.I.ForcePhase = -1f; Atmosphere.I.ForcePlanet = null; Atmosphere.I.ForceStorm = -1f; }
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
            shots.Clear(); robotEyes.Clear(); arks.Clear();
            Hud.Subtitle = null;
            var d = done; done = null;
            d?.Invoke();
        }

        // ================================================================== Bühnenbau
        Transform Shot(string id, Vector3 offset)
        {
            var go = new GameObject("Shot_" + id).transform;
            go.SetParent(stage, false);
            go.localPosition = offset;
            go.gameObject.SetActive(false);
            shots[id] = go;
            return go;
        }

        static GameObject Mesh(Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var go = new GameObject("m");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            return go;
        }

        void Build()
        {
            if (stage != null) Destroy(stage.gameObject);
            stage = new GameObject("IntroStage").transform;
            stage.position = Origin;
            var rng = new Rng(2100);
            var dust = Mats.Get(Mats.Opaque, new Color(0.55f, 0.45f, 0.33f));
            var ground = Mats.Get(Mats.Opaque, new Color(0.52f, 0.42f, 0.3f));

            // 1) Skyline: Hochhäuser und Türme aus gepressten Müllwürfeln im orangefarbenen Dunst
            {
                var s = Shot("skyline", Vector3.zero);
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(900, 1, 900));
                for (int i = 0; i < 70; i++)
                {
                    float x = rng.Range(-260f, 260f), z = rng.Range(60f, 420f);
                    if (rng.Chance(0.55f))
                    {
                        var cm = Mats.Get(Mats.Opaque, new Color(0.5f + rng.Next() * 0.15f, 0.4f + rng.Next() * 0.08f, 0.28f));
                        float y = 0; int n = 10 + rng.Range(0, 20); float w0 = rng.Range(12f, 22f);
                        for (int k = 0; k < n; k++)
                        {
                            float w = Mathf.Lerp(w0, w0 * 0.45f, k / (float)n);
                            mb.For(cm).BoxRot(new Vector3(x + rng.Range(-1f, 1f), y + 2.5f, z + rng.Range(-1f, 1f)), new Vector3(w, 5f, w), new Vector3(0, rng.Range(-10f, 10f), 0));
                            y += 5f;
                        }
                    }
                    else
                    {
                        var bm = Mats.Get(Mats.Opaque, new Color(0.42f, 0.38f, 0.35f) * (0.8f + rng.Next() * 0.4f));
                        float h = rng.Range(40f, 150f), w = rng.Range(14f, 28f);
                        mb.For(bm).Box(new Vector3(x, h * 0.5f, z), new Vector3(w, h, w));
                        mb.For(bm).Box(new Vector3(x, h + 8f, z), new Vector3(w * 0.5f, 16f, w * 0.5f));
                    }
                }
                for (int i = 0; i < 40; i++)
                    mb.For(dust).Blob(new Vector3(rng.Range(-120f, 120f), 0, rng.Range(-40f, 60f)), rng.Range(4f, 12f), rng.Range(3f, 8f), 10, 4, i, 0.3f);
                mb.Build("Skyline", s);
                // Überführung mit Müllbrocken (Motiv aus dem Vordergrund)
                var road = new MultiBuilder();
                road.For(Mats.Get(Mats.Opaque, new Color(0.35f, 0.32f, 0.3f))).Box(new Vector3(0, 12, 40), new Vector3(120, 1.5f, 10));
                for (int i = 0; i < 5; i++) road.For(Mats.Get(Mats.Opaque, new Color(0.4f, 0.36f, 0.32f))).Box(new Vector3(-48 + i * 24, 6, 40), new Vector3(3, 12, 3));
                road.Build("Overpass", s);
            }

            // 2) KONSUMA-Megastore mit großer Werbetafel und leerem Parkplatz
            {
                var s = Shot("megastore", new Vector3(1000, 0, 0));
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(900, 1, 900));
                var wall = Mats.Get(Mats.Opaque, new Color(0.9f, 0.82f, 0.55f));
                var red = Mats.Get(Mats.Opaque, new Color(0.7f, 0.2f, 0.15f));
                mb.For(wall).Box(new Vector3(-40, 7, 120), new Vector3(160, 14, 40));
                mb.For(red).Box(new Vector3(-40, 15, 120), new Vector3(162, 3, 42));
                mb.For(red).Box(new Vector3(-40, 5, 99.8f), new Vector3(160, 1.2f, 0.4f));
                for (int i = 0; i < 12; i++) mb.For(Mats.Get(Mats.Opaque, new Color(0.25f, 0.25f, 0.28f))).Box(new Vector3(-110 + i * 13, 4, 99.7f), new Vector3(6, 5, 0.3f));
                // Laternenreihen auf dem Parkplatz
                for (int r = 0; r < 4; r++)
                    for (int i = 0; i < 12; i++)
                    {
                        var p = new Vector3(-100 + i * 18, 0, 20 + r * 18);
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.25f, 0.25f, 0.27f))).Cylinder(p, 0.2f, 8f, 6);
                        mb.For(Mats.Get(Mats.Opaque, new Color(0.25f, 0.25f, 0.27f))).Box(p + new Vector3(0.8f, 8f, 0), new Vector3(1.8f, 0.3f, 0.5f));
                    }
                // verlassene Einkaufswagen und Autowracks
                for (int i = 0; i < 30; i++)
                {
                    var p = new Vector3(rng.Range(-110f, 90f), 0, rng.Range(5f, 90f));
                    if (rng.Chance(0.5f)) { mb.M = Matrix4x4.TRS(p, Quaternion.Euler(0, rng.Range(0, 360), 0), Vector3.one); mb.For(Mats.Get(Mats.Metal, new Color(0.7f, 0.72f, 0.75f))).Box(new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.5f, 0.9f)); }
                    else { mb.M = Matrix4x4.TRS(p, Quaternion.Euler(0, rng.Range(0, 360), 0), Vector3.one); mb.For(Mats.Get(Mats.Opaque, new Color(0.5f, 0.32f, 0.22f))).Box(new Vector3(0, 0.6f, 0), new Vector3(1.8f, 1.2f, 4f)); }
                    mb.M = Matrix4x4.identity;
                }
                // Werbetafel auf zwei Masten
                var sign = new Vector3(70, 0, 60);
                mb.For(Mats.Get(Mats.Opaque, new Color(0.7f, 0.2f, 0.15f))).Box(sign + new Vector3(-8, 11, 0), new Vector3(1.2f, 22, 1.2f));
                mb.For(Mats.Get(Mats.Opaque, new Color(0.7f, 0.2f, 0.15f))).Box(sign + new Vector3(8, 11, 0), new Vector3(1.2f, 22, 1.2f));
                mb.For(Mats.Get(Mats.Opaque, new Color(0.85f, 0.8f, 0.65f))).Box(sign + new Vector3(0, 26, 0), new Vector3(34, 9, 1.2f));
                mb.For(red).Box(sign + new Vector3(0, 26, -0.7f), new Vector3(31, 6.5f, 0.3f));
                mb.Build("Megastore", s);
                var txt = WorldView.TextLabel(s, "KONSUMA  MEGASTORE", sign + new Vector3(0, 26.3f, -1f), 1.6f, new Color(1f, 0.95f, 0.85f));
                var txt2 = WorldView.TextLabel(s, "ALLES. SOFORT. IMMER NEU.", sign + new Vector3(0, 23.8f, -1f), 0.6f, new Color(1f, 0.85f, 0.4f));
                if (txt != null) txt.transform.localRotation = Quaternion.identity;
                if (txt2 != null) txt2.transform.localRotation = Quaternion.identity;
            }

            // 3) Archen starten
            {
                var s = Shot("arks", new Vector3(2000, 0, 0));
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(900, 1, 900));
                for (int i = 0; i < 25; i++) mb.For(dust).Blob(new Vector3(rng.Range(-150f, 150f), 0, rng.Range(-30f, 150f)), rng.Range(5f, 14f), rng.Range(3f, 9f), 10, 4, i + 50, 0.3f);
                mb.Build("Ground", s);
                for (int i = 0; i < 3; i++)
                {
                    var ark = new GameObject("Arche" + i).transform;
                    ark.SetParent(s, false);
                    ark.localPosition = new Vector3(-60 + i * 55, 20 + i * 8, 140 + i * 30);
                    var am = new MultiBuilder();
                    am.For(Mats.Get(Mats.Metal, new Color(0.92f, 0.93f, 0.95f))).CylinderZ(Vector3.zero, 9f, 90f, 16);
                    am.For(Mats.Get(Mats.Metal, new Color(0.92f, 0.93f, 0.95f))).Sphere(new Vector3(0, 0, 45), 9f, 16, 8);
                    am.For(Mats.Get(Mats.Emissive, new Color(0.5f, 0.8f, 1f), new Color(1.2f, 2f, 3f))).CylinderZ(new Vector3(0, 0, -47), 7f, 4f, 16);
                    for (int k = 0; k < 14; k++) am.For(Mats.Get(Mats.Emissive, new Color(1f, 0.9f, 0.6f), new Color(2f, 1.8f, 1.2f))).Box(new Vector3(0, 9.05f, -38 + k * 6), new Vector3(3f, 0.1f, 1.2f));
                    am.Build("Hull", ark);
                    ark.localRotation = Quaternion.Euler(-35, 10 - i * 10, 0);
                    arks.Add(ark);
                }
                // zurückgelassene Reinigungsroboter
                for (int i = 0; i < 12; i++)
                {
                    var r = RobotModel.Create(s, "Reiniger");
                    r.transform.localPosition = new Vector3(-30 + i * 5, 0, 0);
                    r.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    r.SetCosmetics("c_nachtblau", "a_weiss", "s_none", "x_none");
                }
            }

            // 4) Die Roboter schalten ab (Reihe grauer Reiniger, Augen erlöschen nacheinander)
            {
                var s = Shot("shutdown", new Vector3(3000, 0, 0));
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(600, 1, 600));
                for (int i = 0; i < 20; i++) mb.For(dust).Blob(new Vector3(rng.Range(-80f, 80f), 0, rng.Range(20f, 120f)), rng.Range(4f, 10f), rng.Range(3f, 7f), 10, 4, i + 90, 0.3f);
                mb.Build("Ground", s);
                for (int i = 0; i < 16; i++)
                {
                    var r = new GameObject("GrauerReiniger").transform;
                    r.SetParent(s, false);
                    r.localPosition = new Vector3(-22 + i * 3f, 0, 8 + (i % 2) * 1.5f);
                    var body = Mats.Get(Mats.Opaque, new Color(0.45f, 0.45f, 0.47f));
                    Mesh(r, MeshKit.Cube, body, new Vector3(0, 0.7f, 0), new Vector3(1f, 1f, 1f), Vector3.zero);
                    Mesh(r, MeshKit.Cube, body, new Vector3(0, 1.45f, 0), new Vector3(0.8f, 0.4f, 0.6f), Vector3.zero);
                    var eye = Mats.Unique(Mats.Emissive, new Color(0.3f, 0.9f, 1f));
                    Mats.SetEmission(eye, new Color(0.4f, 1.6f, 2f));
                    Mesh(r, MeshKit.Cube, eye, new Vector3(0, 1.45f, 0.31f), new Vector3(0.6f, 0.12f, 0.02f), Vector3.zero);
                    robotEyes.Add(eye);
                }
            }

            // 5) MIKOs Zuhause: rostiger Lieferwagen mit blauem Licht zwischen Schrott
            {
                var s = Shot("home", new Vector3(4000, 0, 0));
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400));
                for (int i = 0; i < 30; i++) mb.For(Mats.Get(Mats.Opaque, new Color(0.4f + rng.Next() * 0.2f, 0.33f, 0.25f))).BoxRot(new Vector3(rng.Range(-15f, 15f), rng.Range(0f, 1.5f), rng.Range(-6f, 14f)), Vector3.one * rng.Range(0.6f, 2f), new Vector3(rng.Range(0, 360), rng.Range(0, 360), rng.Range(0, 360)));
                mb.Build("Junk", s);
                var vm = new MultiBuilder();
                var rust = Mats.Get(Mats.Opaque, new Color(0.72f, 0.58f, 0.25f));
                var rust2 = Mats.Get(Mats.Opaque, new Color(0.5f, 0.3f, 0.18f));
                vm.For(rust).Box(new Vector3(0, 1.6f, 0), new Vector3(2.4f, 2.2f, 5.5f));
                vm.For(rust2).Box(new Vector3(0, 1.3f, 3.4f), new Vector3(2.3f, 1.6f, 1.6f));
                vm.For(Mats.Get(Mats.Opaque, new Color(0.15f, 0.15f, 0.17f))).Box(new Vector3(0, 1.8f, 4.25f), new Vector3(2.0f, 0.8f, 0.05f));
                for (int i = 0; i < 4; i++) vm.For(Mats.Get(Mats.Opaque, new Color(0.15f, 0.15f, 0.15f))).CylinderX(new Vector3(i % 2 == 0 ? 1.2f : -1.2f, 0.45f, i < 2 ? 2.8f : -1.8f), 0.45f, 0.3f, 10);
                vm.For(Mats.Get(Mats.Opaque, new Color(0.9f, 0.9f, 0.85f))).Sphere(new Vector3(0, 2.9f, 3.5f), 0.35f, 8, 6);
                van = vm.Build("Lieferwagen", s).transform;
                van.localRotation = Quaternion.Euler(0, -25, 2);
                vanLight = Mats.Unique(Mats.Emissive, new Color(0.3f, 0.5f, 1f));
                Mats.SetEmission(vanLight, new Color(0.4f, 0.8f, 3f));
                var glow = Mesh(van, MeshKit.Cube, vanLight, new Vector3(0, 1.6f, -2.76f), new Vector3(2.1f, 2f, 0.05f), Vector3.zero);
                var l = new GameObject("BlauLicht").AddComponent<Light>();
                l.transform.SetParent(van, false);
                l.transform.localPosition = new Vector3(0, 1.6f, -3.6f);
                l.type = LightType.Spot; l.spotAngle = 80; l.range = 18; l.intensity = 3f; l.color = new Color(0.35f, 0.55f, 1f);
                l.transform.localRotation = Quaternion.Euler(10, 180, 0);
                miko = RobotModel.Create(s, "MIKO_Intro");
                miko.transform.localPosition = new Vector3(0.5f, 0, -4.5f);
                cubeStack = new GameObject("Stapel").transform;
                cubeStack.SetParent(s, false);
                cubeStack.localPosition = new Vector3(-5, 0, -3);
                for (int i = 0; i < 6; i++) Mesh(cubeStack, MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.55f, 0.45f, 0.3f)), new Vector3((i % 2) * 0.1f, 0.5f + i, 0), Vector3.one, new Vector3(0, i * 9, 0));
                pressCube = Mesh(s, MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.6f, 0.48f, 0.3f)), new Vector3(0, 0.5f, -7f), Vector3.one * 1.5f, Vector3.zero).transform;
            }

            // 6) Der Keimling im Greifarm
            {
                var s = Shot("sprout", new Vector3(5000, 0, 0));
                var mb = new MultiBuilder();
                mb.For(ground).Box(new Vector3(0, -0.5f, 0), new Vector3(200, 1, 200));
                for (int i = 0; i < 20; i++) mb.For(Mats.Get(Mats.Opaque, new Color(0.45f, 0.35f, 0.25f))).BoxRot(new Vector3(rng.Range(-8f, 8f), rng.Range(0f, 1f), rng.Range(2f, 12f)), Vector3.one * rng.Range(0.4f, 1.6f), new Vector3(rng.Range(0, 360), rng.Range(0, 360), 0));
                mb.Build("Rubble", s);
                mikoClose = RobotModel.Create(s, "MIKO_Nah");
                mikoClose.transform.localPosition = new Vector3(0, 0, 0);
                sprout = new GameObject("Keimling").transform;
                sprout.SetParent(s, false);
                sprout.localPosition = new Vector3(0.62f, 1.05f, 1.15f);
                Mesh(sprout, MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(0.3f, 0.2f, 0.13f)), Vector3.zero, new Vector3(0.28f, 0.18f, 0.24f), Vector3.zero);
                var green = Mats.Get(Mats.Opaque, new Color(0.35f, 0.75f, 0.25f));
                Mesh(sprout, MeshKit.Cylinder, green, new Vector3(0, 0.16f, 0), new Vector3(0.015f, 0.26f, 0.015f), new Vector3(0, 0, 6));
                Mesh(sprout, MeshKit.Sphere, green, new Vector3(0.05f, 0.3f, 0), new Vector3(0.09f, 0.015f, 0.05f), new Vector3(0, 20, 25));
                Mesh(sprout, MeshKit.Sphere, green, new Vector3(-0.04f, 0.26f, 0.01f), new Vector3(0.08f, 0.015f, 0.045f), new Vector3(0, -30, -20));
            }

            // 7) Im All: MIKO auf dem Transportschiff vor dem Planeten
            {
                var s = Shot("ship", new Vector3(6000, 500, 0));
                planetSphere = Mesh(s, MeshKit.Sphere, Mats.Get(Mats.Opaque, new Color(0.62f, 0.48f, 0.3f)), new Vector3(-120, 20, 380), Vector3.one * 420f, Vector3.zero).transform;
                Mesh(s, MeshKit.Sphere, Mats.Get(Mats.ParticleAdd, new Color(1f, 0.75f, 0.4f, 0.25f)), new Vector3(-120, 20, 380), Vector3.one * 440f, Vector3.zero);
                shipSpace = new GameObject("Schiff").transform;
                shipSpace.SetParent(s, false);
                var sm = new MultiBuilder();
                var hull = Mats.Get(Mats.Metal, new Color(0.86f, 0.87f, 0.9f));
                sm.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(6f, 0.5f), new Vector2(7.5f, 3f), new Vector2(6f, 6f), new Vector2(2.5f, 7.5f), new Vector2(0, 8f) }, 24);
                for (int i = 0; i < 10; i++) sm.For(Mats.Get(Mats.Opaque, new Color(0.2f, 0.22f, 0.26f))).Box(new Vector3(-4 + i * 0.9f, 6.4f, 1.5f), new Vector3(0.5f, 0.3f, 3f));
                sm.For(Mats.Get(Mats.Emissive, new Color(0.4f, 0.8f, 1f), new Color(1f, 2f, 3f))).Cylinder(new Vector3(0, 2.5f, -7.2f), 1.6f, 1f, 16);
                sm.Build("Rumpf", shipSpace);
                shipSpace.localRotation = Quaternion.Euler(-80, 0, 0);
                mikoShip = RobotModel.Create(s, "MIKO_Schiff");
                // Sterne
                var st = new GameObject("Sterne");
                st.transform.SetParent(s, false);
                var ps = st.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = 1e5f; main.startSpeed = 0; main.maxParticles = 1500;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = ps.emission; em.enabled = false;
                st.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Get(Mats.ParticleAdd, Color.white);
                var parts = new ParticleSystem.Particle[1500];
                var r2 = new System.Random(7);
                for (int i = 0; i < parts.Length; i++)
                {
                    var d = new Vector3((float)r2.NextDouble() * 2 - 1, (float)r2.NextDouble() * 2 - 1, (float)r2.NextDouble() * 2 - 1).normalized;
                    parts[i].position = d * 700f;
                    parts[i].startSize = 0.8f + (float)r2.NextDouble() * 2.2f;
                    parts[i].startColor = Color.Lerp(new Color(0.8f, 0.85f, 1f), new Color(1f, 0.85f, 0.7f), (float)r2.NextDouble());
                    parts[i].remainingLifetime = 1e5f; parts[i].startLifetime = 1e5f;
                }
                ps.SetParticles(parts, parts.Length);
            }
        }

        // ================================================================== Ablauf
        void Update()
        {
            if (!playing) return;
            // Warten, bis der Score läuft (max. 10 s), dann synchron zur Musik. Kommt die Musik nicht rechtzeitig,
            // läuft die Sequenz ohne sie weiter – ein verspäteter Start würde sonst zeitversetzt spielen.
            double at = freeRun ? -1 : AudioManager.IntroTime;
            if (at >= 0) t = (float)at;
            else
            {
                startDelay += Time.unscaledDeltaTime;
                if (!freeRun && startDelay > 10f) { freeRun = true; AudioManager.StopIntro(); }
                if (freeRun) t += Time.unscaledDeltaTime;
            }
            if (t >= IntroTimeline.Total + 2f) { Finish(); return; }

            // Überspringen: Esc/Eingabe/Leertaste/A gedrückt halten
            bool hold = Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0) || Input.GetMouseButton(0);
            skipHold = hold ? skipHold + Time.unscaledDeltaTime : 0f;
            if (skipHold > 1.0f) { Finish(); return; }

            IntroTimeline.Shot cur = IntroTimeline.Shots[0];
            foreach (var s in IntroTimeline.Shots) if (t >= s.Start) cur = s;
            foreach (var kv in shots) kv.Value.gameObject.SetActive(kv.Key == cur.Id);
            float local = t - cur.Start, len = cur.End - cur.Start, k = Mathf.Clamp01(local / len);
            // Untertitel: erste Zeile in der ersten Hälfte, zweite in der zweiten
            if (cur.Lines.Length > 0) Hud.Say(cur.Lines[k < 0.5f || cur.Lines.Length < 2 ? 0 : 1], 0.3f);
            AnimateShot(cur.Id, local, k);
        }

        void Cam(Vector3 pos, Vector3 look, float fov = 50f, float roll = 0f)
        {
            var c = Camera.main;
            if (c == null) return;
            c.transform.position = stage.position + pos;
            c.transform.rotation = Quaternion.LookRotation((stage.position + look) - (stage.position + pos)) * Quaternion.Euler(0, 0, roll);
            c.fieldOfView = fov;
        }

        void Env(float phase, float storm, bool space)
        {
            var a = Atmosphere.I;
            if (a != null) { a.ForcePlanet = "terra"; a.ForcePhase = phase; a.ForceStorm = storm; }
            var c = Camera.main;
            if (c == null) return;
            if (space) { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.005f, 0.005f, 0.015f); RenderSettings.fog = false; }
            else { c.clearFlags = CameraClearFlags.Skybox; RenderSettings.fog = true; }
        }

        void AnimateShot(string id, float lt, float k)
        {
            float e = M.Smooth(k);
            switch (id)
            {
                case "skyline":
                    Env(0.66f, 0.35f, false);
                    RenderSettings.fogDensity = 0.012f;
                    Cam(new Vector3(-40 + e * 60, 26 - e * 8, -60 + e * 30), new Vector3(20 + e * 20, 40, 200), 48f);
                    break;
                case "megastore":
                    Env(0.68f, 0.3f, false);
                    RenderSettings.fogDensity = 0.009f;
                    Cam(new Vector3(1000 + 60 - e * 70, 9, -20), new Vector3(1000 + 10 - e * 40, 18, 110), 55f);
                    break;
                case "arks":
                    {
                        Env(0.7f, 0.1f, false);
                        RenderSettings.fogDensity = 0.004f;
                        for (int i = 0; i < arks.Count; i++)
                        {
                            float rise = Mathf.Max(0, lt - i * 2.5f);
                            arks[i].localPosition = new Vector3(-60 + i * 55, 20 + i * 8 + rise * rise * 0.8f, 140 + i * 30 + rise * 6f);
                        }
                        Cam(new Vector3(2000, 3, -25), new Vector3(2000, 30 + e * 140, 150), 60f);
                        break;
                    }
                case "shutdown":
                    {
                        Env(0.74f, 0.2f, false);
                        RenderSettings.fogDensity = 0.01f;
                        for (int i = 0; i < robotEyes.Count; i++)
                        {
                            float off = 1.5f + i * 0.7f;
                            Mats.SetEmission(robotEyes[i], lt < off ? new Color(0.4f, 1.6f, 2f) : lt < off + 0.3f ? new Color(0.4f, 1.6f, 2f) * 0.2f : Color.black);
                        }
                        Cam(new Vector3(3000 + 26 - e * 48, 1.6f, 3f), new Vector3(3000 - e * 30, 1.2f, 9), 42f);
                        break;
                    }
                case "home":
                    {
                        Env(0.27f + e * 0.05f, 0f, false);
                        RenderSettings.fogDensity = 0.006f;
                        // MIKO rollt aus dem Wagen, presst einen Würfel und stapelt ihn
                        float roll = Mathf.Clamp01(lt / 5f);
                        miko.transform.localPosition = Vector3.Lerp(new Vector3(0.5f, 0, -3.2f), new Vector3(0.5f, 0, -6.2f), M.Smooth(roll));
                        miko.transform.localRotation = Quaternion.Euler(0, 180 + Mathf.Sin(lt) * 8, 0);
                        bool press = lt > 6f && lt < 10f;
                        pressCube.localScale = Vector3.one * (lt < 7f ? 1.5f : Mathf.Lerp(1.5f, 0.9f, Mathf.Clamp01((lt - 7f) / 1.5f)));
                        if (lt > 10f) pressCube.localPosition = Vector3.Lerp(new Vector3(0, 0.45f, -7f), new Vector3(-5, 6.45f, -3f), M.Smooth(Mathf.Clamp01((lt - 10f) / 3f)));
                        else pressCube.localPosition = new Vector3(0, 0.45f, -7f);
                        miko.Animate(Time.deltaTime, roll < 1 ? 1.5f : 0f, 0.6f, "grab", press, false, false, false, 0.4f, Vector3.back);
                        Mats.SetEmission(vanLight, new Color(0.4f, 0.8f, 3f) * (0.8f + Mathf.Sin(lt * 2f) * 0.2f));
                        Cam(new Vector3(4000 + 7 - e * 3, 2.2f, -14 + e * 2), new Vector3(4000 + 0, 1.4f, -3), 45f);
                        break;
                    }
                case "sprout":
                    {
                        Env(0.3f, 0f, false);
                        RenderSettings.fogDensity = 0.004f;
                        mikoClose.transform.localRotation = Quaternion.Euler(0, 0, 0);
                        mikoClose.Animate(Time.deltaTime, 0, 0.3f, "grab", true, false, false, false, 0.2f, new Vector3(0.4f, 0, 1f));
                        if (lt > 7f) mikoClose.Emote("happy");
                        sprout.localRotation = Quaternion.Euler(0, lt * 8f, Mathf.Sin(lt * 1.5f) * 3f);
                        Cam(new Vector3(5000 + 1.8f - e * 0.4f, 1.25f, 2.3f - e * 0.3f), new Vector3(5000 + 0.62f, 1.15f, 1.15f), 32f - e * 6f);
                        break;
                    }
                case "ship":
                    {
                        Env(0.5f, 0f, true);
                        shipSpace.localPosition = new Vector3(0, 0, e * 40f);
                        mikoShip.transform.position = shipSpace.TransformPoint(new Vector3(0, 7.9f, 1.0f));
                        mikoShip.transform.rotation = shipSpace.rotation * Quaternion.Euler(-90, 0, 0);
                        mikoShip.Animate(Time.deltaTime, 0, 0.3f, "grab", true, false, false, false, 0.2f, Vector3.forward);
                        planetSphere.Rotate(0, 1.5f * Time.deltaTime, 0);
                        var shipPos = shipSpace.position - stage.position;
                        Cam(shipPos + new Vector3(10 - e * 3, 4, -12 + e * 2), shipPos + new Vector3(0, 2, 8), 55f, -8f);
                        break;
                    }
            }
        }

        void OnGUI()
        {
            if (!playing) return;
            GUI.depth = -500;
            // Kinobalken
            float bar = Screen.height * 0.1f;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
            // Überblendungen am Anfang/Ende jeder Einstellung
            foreach (var s in IntroTimeline.Shots)
            {
                if (t < s.Start || t >= s.End) continue;
                float fade = Mathf.Max(Mathf.Clamp01(1f - (t - s.Start) / 0.8f), Mathf.Clamp01(1f - (s.End - t) / 0.8f));
                if (s.Id == "ship" && t > s.End - 0.8f) fade = 0;
                if (fade > 0.01f) { GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); }
            }
            float scale = Screen.height / 1080f * (GameApp.I != null ? GameApp.I.Settings.TextScale : 1f);
            // Titel
            if (t > 93f)
            {
                float a = Mathf.Clamp01((t - 93f) / 2f);
                var ts = new GUIStyle(GUI.skin.label) { fontSize = (int)(110 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.color = new Color(0, 0, 0, a * 0.5f);
                GUI.Label(new Rect(4, Screen.height * 0.3f + 4, Screen.width, 160 * scale), "RE:PLANET", ts);
                GUI.color = new Color(0.55f, 1f, 0.95f, a);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 160 * scale), "RE:PLANET", ts);
                var ss = new GUIStyle(ts) { fontSize = (int)(44 * scale), fontStyle = FontStyle.Normal };
                GUI.color = new Color(1f, 0.75f, 0.45f, a);
                GUI.Label(new Rect(0, Screen.height * 0.3f + 150 * scale, Screen.width, 70 * scale), "Eine zweite Chance", ss);
            }
            // Untertitel (immer sichtbar in der Zwischensequenz, weil sie die Geschichte erzählen)
            if (!string.IsNullOrEmpty(Hud.Subtitle) && t < 93f)
            {
                var st = new GUIStyle(GUI.skin.label) { fontSize = (int)(34 * scale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                var r = new Rect(Screen.width * 0.1f, Screen.height - bar - 110 * scale, Screen.width * 0.8f, 100 * scale);
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), Hud.Subtitle, st);
                GUI.color = Color.white;
                GUI.Label(r, Hud.Subtitle, st);
            }
            // Musik wird beim allerersten Start noch erzeugt
            if (!freeRun && t <= 0f && startDelay > 0.6f)
            {
                var ls = new GUIStyle(GUI.skin.label) { fontSize = (int)(24 * scale), alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1, 1, 1, 0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f));
                GUI.Label(new Rect(0, Screen.height * 0.5f - 20 * scale, Screen.width, 40 * scale), "Musik wird vorbereitet …", ls);
            }
            // Überspringen-Hinweis
            var hs = new GUIStyle(GUI.skin.label) { fontSize = (int)(20 * scale), alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1, 1, 1, 0.6f + (skipHold > 0 ? 0.4f : 0));
            GUI.Label(new Rect(0, Screen.height - bar + 10 * scale, Screen.width - 30, 30 * scale), skipHold > 0 ? "Überspringen … " + (int)(skipHold * 100) + " %" : "Gedrückt halten zum Überspringen", hs);
            GUI.color = Color.white;
        }
    }
}
