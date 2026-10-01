using System;
using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Kamera: Third-Person mit Kollision, Menü-Kamerafahrt, Bauansicht, Fotomodus (freie Kamera, echter PNG-Export,
    /// Hochformat 9:16), Zwischensequenzen (Übersteuerung) und Render-Skalierung.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public Camera Cam { get; private set; }
        /// <summary>Eigene Nachbearbeitung (Bloom, Tonemapping, Luftperspektive …) an der Hauptkamera.</summary>
        public PostFX Post { get; private set; }
        public float Yaw, Pitch = 18f, Distance = 6.5f;
        /// <summary>Zwischensequenz steuert die Kamera direkt.</summary>
        public bool Cinematic;
        float shake, menuT;
        // Nachführen hinter MIKO: Zeit seit der letzten eigenen Kameradrehung, letzte Zielposition
        float lookIdle = 10f;
        Vector3 lastTarget; bool hasLastTarget;
        Vector3 photoPos; float photoYaw, photoPitch; bool photoInit;
        RenderTexture scaled;
        readonly List<Box> tmp = new List<Box>();

        void Awake()
        {
            I = this;
            DisableSceneLeftovers();
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.nearClipPlane = 0.15f;
            Cam.farClipPlane = 800f;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.allowMSAA = true;
            Cam.allowHDR = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<FlareLayer>();
            Post = go.AddComponent<PostFX>();
            // Weitere Darstellungsbausteine gehören an das Hauptobjekt
            foreach (var t in new[] { typeof(Atmosphere), typeof(TrashRenderer), typeof(ActorsView), typeof(FxView), typeof(FloraRenderer), typeof(FeaturesView) })
                if (GetComponent(t) == null) gameObject.AddComponent(t);
        }

        /// <summary>Kameras, Listener und Richtungslichter aus einer Beispielszene würden doppelt rendern/beleuchten.</summary>
        void DisableSceneLeftovers()
        {
            try
            {
                foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c != null && c.gameObject.scene.IsValid() && c.hideFlags == HideFlags.None && c.transform.root != transform.root) c.gameObject.SetActive(false);
                foreach (var l in Resources.FindObjectsOfTypeAll<AudioListener>())
                    if (l != null && l.gameObject.scene.IsValid() && l.hideFlags == HideFlags.None && l.transform.root != transform.root) l.enabled = false;
                foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                    if (l != null && l.type == LightType.Directional && l.gameObject.scene.IsValid() && l.hideFlags == HideFlags.None && l.transform.root != transform.root) l.gameObject.SetActive(false);
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Szenenobjekte: " + e.Message); }
        }

        public void Shake(float amount)
        {
            if (GameApp.I != null && !GameApp.I.Settings.CameraShake) return;
            shake = Mathf.Max(shake, amount);
        }

        void LateUpdate()
        {
            var app = GameApp.I;
            if (app == null) return;
            Cam.fieldOfView = PhotoMode.Active ? PhotoMode.Fov : app.Settings.Fov;
            if (Post != null) Post.Configure(app.Settings.Quality, app.Settings.ReduceFlashing);
            Hud.CameraYaw = Yaw;
            if (Cinematic) { ApplyRenderScale(app); return; }
            if (app.Mode == AppMode.Menu || app.Mode == AppMode.PlanetSelect || (app.Mode == AppMode.Loading && app.W == null)) { MenuCamera(); ApplyRenderScale(app); return; }
            if (!app.InGame || PlayerController.I == null) { ApplyRenderScale(app); return; }

            if (PhotoMode.Active) { PhotoCamera(); ApplyRenderScale(app); HandleCapture(app); return; }
            photoInit = false;
            var target = PlayerController.I.RenderPos + Vector3.up * (PlayerController.I.InVehicle ? 2.4f : 1.4f);
            bool look = !UIState.BlocksGameplay && !BuildMode.Active;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (look)
            {
                var d = InputMap.Look(app.Settings.MouseSensitivity, app.Settings.PadSensitivity, app.Settings.InvertY);
                Yaw += d.x;
                Pitch = Mathf.Clamp(Pitch - d.y, -25f, 70f);
                float sc = InputMap.Scroll();
                if (Mathf.Abs(sc) > 0.001f) Distance = Mathf.Clamp(Distance - sc * 6f, 3f, PlayerController.I.InVehicle ? 18f : 12f);
                lookIdle = d.sqrMagnitude > 0.0004f ? 0f : lookIdle + dt;
            }
            FollowBehind(target, dt);
            if (PlayerController.I.InVehicle && Distance < 8f) Distance = Mathf.Lerp(Distance, 9f, Time.deltaTime * 2f);
            float dist = Distance;
            float pitch = Pitch;
            // Innenraum (Hangar, Laderaum): näher heran und flacher, damit die Kamera unter der Decke bleibt
            var room = BuildMode.Active ? null : IndoorRoom();
            indoorBlend = Mathf.MoveTowards(indoorBlend, room != null ? 1f : 0f, dt * 2.5f);
            if (indoorBlend > 0f && !BuildMode.Active)
            {
                dist = Mathf.Lerp(dist, Mathf.Min(dist, PlayerController.I.InVehicle ? 5.2f : 3.4f), indoorBlend);
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(pitch, -12f, 22f), indoorBlend);
            }
            // Schlafen: Blick auf MIKO am Ladeplatz, langsame Kreisfahrt und sanftes Atmen der Entfernung; beim Aufwachen
            // zurück zur Blickrichtung von vorher
            SleepCamera(app, ref target, ref dist, ref pitch, dt);
            if (BuildMode.Active) { pitch = 55f; dist = 26f; target = new Vector3(0, target.y, -104); }
            var rot = Quaternion.Euler(pitch, Yaw, 0);
            var wanted = target - rot * Vector3.forward * dist;
            wanted = Collide(target, wanted);
            if (room != null && room.Inner.Contains(wanted.x, wanted.z, 0f))
                wanted.y = Mathf.Min(wanted.y, room.Inner.Y0 + room.Inner.H - 0.4f); // nie in die Decke
            var pos = wanted;
            if (shake > 0)
            {
                shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.5f);
                pos += UnityEngine.Random.insideUnitSphere * shake * 0.35f;
            }
            Cam.transform.position = pos;
            var lookDir = target - pos;
            if (lookDir.sqrMagnitude > 1e-6f) Cam.transform.rotation = Quaternion.LookRotation(lookDir); // Kamera direkt am Blickpunkt: alte Richtung behalten
            ApplyRenderScale(app);
        }

        /// <summary>
        /// Schwenkt die Kamera langsam hinter MIKO, solange er vorwärts/schräg läuft und die Kamera eine Weile nicht selbst
        /// gedreht wurde – so bleibt sie hinter ihm, statt nach Kurven allmählich seitlich oder vor ihm zu stehen. Läuft MIKO
        /// seitwärts oder auf die Kamera zu (Steuerung relativ zur Kamera), bleibt sie stehen, sonst würde sie im Kreis drehen.
        /// Im Fahrzeug folgt sie zügiger (außer beim Rückwärtsfahren).
        /// </summary>
        void FollowBehind(Vector3 target, float dt)
        {
            var pc = PlayerController.I;
            if (pc == null || BuildMode.Active || dt <= 0f) { hasLastTarget = false; return; }
            var v = hasLastTarget ? (target - lastTarget) / dt : Vector3.zero;
            lastTarget = target; hasLastTarget = true;
            float speed = new Vector2(v.x, v.z).magnitude;
            if (speed > 40f) return; // Sprung (Teleport, Abschleppen, Planetenwechsel)
            bool vehicle = pc.InVehicle;
            if (lookIdle < (vehicle ? 0.8f : 1.5f) || speed < 1f) return;
            float robotYaw = pc.RenderYaw * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(Yaw, robotYaw);
            // Seitwärtslaufen (90°) dreht nicht mit – sonst liefe MIKO bei gehaltener Seitwärtstaste im Kreis
            if (Mathf.Abs(delta) > (vehicle ? 150f : 75f)) return;
            float rate = (vehicle ? 70f : 32f) * Mathf.Clamp01(speed / 5f);
            Yaw = Mathf.MoveTowardsAngle(Yaw, robotYaw, rate * dt);
        }

        float indoorBlend;
        float sleepCam, yawBeforeSleep;
        bool sleepOrbit;

        /// <summary>Kamera während des Schlafs (eigener Spieler): Ziel = MIKO am Ladeplatz, Kreisfahrt 5°/s, etwas näher und höher.</summary>
        void SleepCamera(GameApp app, ref Vector3 target, ref float dist, ref float pitch, float dt)
        {
            var me = app.Me;
            var av = ActorsView.I;
            if (me == null || app.Client == null || av == null) { sleepCam = 0f; sleepOrbit = false; return; }
            bool asleep = av.SleepingVisual(me) && !BuildMode.Active && !UIState.BlocksGameplay;
            if (asleep && !sleepOrbit) { sleepOrbit = true; yawBeforeSleep = Yaw; }
            sleepCam = Mathf.MoveTowards(sleepCam, asleep ? 1f : 0f, dt / (asleep ? 2.5f : 1.3f));
            float sb = av.SleepBlendOf(app.Client.Pid);
            var rob = av.RobotOf(app.Client.Pid);
            if (sb > 0f && rob != null) target = Vector3.Lerp(target, rob.transform.position + Vector3.up * 1.0f, M.Smooth(sb));
            if (sleepCam <= 0f) { sleepOrbit = false; return; }
            float k = M.Smooth(sleepCam);
            if (asleep) Yaw += 5f * dt * k;
            else if (sleepOrbit) Yaw = Mathf.MoveTowardsAngle(Yaw, yawBeforeSleep, dt * 90f);
            dist = Mathf.Lerp(dist, Mathf.Min(dist, 4.4f) + Mathf.Sin(Time.time * 0.3f) * 0.35f, k);
            pitch = Mathf.Lerp(pitch, 26f, k * 0.85f);
        }

        /// <summary>Schutzraum (Hangar, Laderaum), in dem MIKO gerade ist, oder null.</summary>
        static ShelterRoom IndoorRoom()
        {
            var wv = WorldView.I; var pc = PlayerController.I;
            if (wv == null || wv.Layout == null || pc == null) return null;
            var p = pc.RenderPos;
            return Rules.RoomAt(wv.Layout, new V3(p.x, p.y, p.z));
        }

        /// <summary>Kameraradius für die Kollision: Nahebene (0,15 m, Bildecken ≈ 0,18 m) plus Rand.</summary>
        const float CamRadius = 0.3f;
        /// <summary>Näher als so an MIKO heran nur, wenn es keine freie Stellung gibt.</summary>
        const float MinDist = 1.1f;

        /// <summary>
        /// Hält die Kamera aus Wänden, Gebäuden (auch selbst gebauten Anlagen), Müllbergen und dem Gelände.
        /// Liegt direkt hinter MIKO eine Wand, schaut die Kamera steiler von oben statt in die Wand zu rücken;
        /// nur wenn gar keine Stellung frei ist, rückt sie bis an MIKO heran – aber nie in ein Hindernis.
        /// </summary>
        Vector3 Collide(Vector3 from, Vector3 to)
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null) return to;
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return to;
            dir /= len;
            EnsureDeco(wv);
            // Steckt MIKOs Kopfpunkt selbst in Deko-Geometrie (z. B. unter einem Vordach), zählt sie in diesem Bild nicht
            decoActive = deco != null && deco.Root == wv.Root && !deco.Hit(from, DecoRadius);
            Vector3 pos;
            float free = FreeDistance(wv, from, dir, len, out pos);
            if (free >= Mathf.Min(len, MinDist)) return pos;
            // Alternative: gleiche Richtung, aber steiler von oben
            float yaw = Mathf.Atan2(-dir.x, -dir.z);
            float pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f));
            float bestFree = free; Vector3 bestPos = pos;
            for (float add = 12f; add <= 70f; add += 12f)
            {
                float p = Mathf.Min(pitch + add * Mathf.Deg2Rad, 80f * Mathf.Deg2Rad);
                var d2 = new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(p), Mathf.Sin(p), -Mathf.Cos(yaw) * Mathf.Cos(p));
                Vector3 p2;
                float f2 = FreeDistance(wv, from, d2, len, out p2);
                if (f2 >= Mathf.Min(len, MinDist)) return p2;
                if (f2 > bestFree + 0.05f) { bestFree = f2; bestPos = p2; }
            }
            return bestPos;
        }

        /// <summary>Wie weit die Kamera (als Kugel) von <paramref name="from"/> in Richtung <paramref name="dir"/> frei ist; Gelände/Wasser heben sie an.</summary>
        float FreeDistance(WorldView wv, Vector3 from, Vector3 dir, float len, out Vector3 pos)
        {
            const float step = 0.1f;
            pos = Lift(wv, from, from);
            float free = 0f;
            for (float t = step; t <= len + 1e-3f; t += step)
            {
                var p = Lift(wv, from + dir * Mathf.Min(t, len), from);
                if (Blocked(wv, p)) return free;
                free = Mathf.Min(t, len); pos = p;
            }
            return free;
        }

        /// <summary>Über Gelände und (wenn MIKO nicht taucht) über der Wasseroberfläche halten.</summary>
        static Vector3 Lift(WorldView wv, Vector3 p, Vector3 target)
        {
            float floor = wv.Layout != null ? wv.Layout.GroundAt(p.x, p.z) : Terrain.HeightAt(wv.Planet, p.x, p.z); // auch Rampe/Laderaumboden
            float water = Terrain.WaterLevel(wv.Planet);
            bool diving = water > -50f && target.y < water;
            if (water > -50f && !diving) floor = Mathf.Max(floor, water);
            if (p.y < floor + CamRadius) p.y = floor + CamRadius;
            return p;
        }

        bool Blocked(WorldView wv, Vector3 p)
        {
            var env = PlayerController.I != null ? PlayerController.I.Env : null;
            if (env != null && env.Planet == wv.Planet) env.Query(p.x, p.z, CamRadius + 0.05f, tmp);
            else wv.Layout.Query(p.x, p.z, CamRadius + 0.05f, tmp);
            foreach (var b in tmp)
            {
                if (b.Kind == "gateblock") continue; // unsichtbare Sperre vor dem Müllwall
                if (env != null && env.Planet == wv.Planet ? !env.Solid(b) : (!b.Solid || b.DuneSet >= 0 || b.Gate >= 0)) continue;
                if (b.Contains(p.x, p.z, CamRadius) && p.y > b.Y0 - CamRadius && p.y < b.Y0 + b.H + CamRadius) return true;
            }
            // Müllberge (schrumpfen mit der Reinigung)
            var w = wv.World;
            if (w != null && wv.Layout.Mounds.Count > 0 && w.Planets.ContainsKey(wv.Planet))
            {
                var ps = w.Planet(wv.Planet);
                foreach (var m in wv.Layout.Mounds)
                {
                    float s = PhotoMode.Active && PhotoMode.ShowBefore ? 1f : WorldView.MoundScale(ps, m.Area);
                    if (s < 0.06f) continue;
                    // Form wie MeshKit.Mound: Halbkugel mit Rauschen (±25 %) und Gerümpel auf der Oberfläche
                    float r = m.Radius * s * 1.25f, dx = p.x - m.Pos.x, dz = p.z - m.Pos.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 > (r + CamRadius) * (r + CamRadius)) continue;
                    float k = Mathf.Sqrt(Mathf.Clamp01(1f - d2 / (r * r)));
                    if (p.y < m.Pos.y + m.Height * s * 1.2f * k + CamRadius) return true;
                }
            }
            // Gezeichnete Deko, die über die Kollisionsboxen hinausragt (Balkone, Vordächer, Markisen, Stützpunkt, Schiff)
            return decoActive && deco.Hit(p, DecoRadius);
        }

        // ------------------------------------------------------------ Deko-Geometrie
        /// <summary>Abstand der Kamera zu gezeichneter Deko (Nahebene 0,15 m; Bildecken ≈ 0,18 m).</summary>
        const float DecoRadius = 0.22f;
        static readonly string[] DecoGroups = { "Buildings", "Base", "Backdrop", "TransportShip", "Unterschlupf" };
        DecoGrid deco, decoPending;
        bool decoActive;

        /// <summary>
        /// Baut das Dreiecksraster neu, sobald die Welt neu aufgebaut wurde (Planetenwechsel, Sitzungsstart). Die Meshdaten
        /// werden im Hauptthread gelesen, das Einsortieren läuft im Hintergrund (kein Ruckler); bis dahin gelten nur die Boxen.
        /// </summary>
        void EnsureDeco(WorldView wv)
        {
            var root = wv.Root;
            if (root == null) { deco = null; decoPending = null; return; }
            if (deco != null && deco.Root == root) return;
            if (decoPending != null && decoPending.Root == root)
            {
                if (decoPending.Ready) { deco = decoPending.Failed ? null : decoPending; if (decoPending.Failed) Debug.LogWarning("[Kamera] Deko-Raster: " + decoPending.Error); decoPending = null; }
                return;
            }
            var grid = new DecoGrid(root);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            try
            {
                foreach (Transform ch in root)
                {
                    bool use = false;
                    foreach (var g in DecoGroups) if (ch.gameObject.name.StartsWith(g, StringComparison.Ordinal)) { use = true; break; }
                    if (!use) continue;
                    foreach (var mf in ch.GetComponentsInChildren<MeshFilter>(false))
                        if (mf.sharedMesh != null) grid.Take(mf.sharedMesh, mf.transform.localToWorldMatrix);
                }
            }
            catch (Exception e) { Debug.LogException(e); return; }
            decoPending = grid;
            if (!System.Threading.ThreadPool.QueueUserWorkItem(_ => grid.BuildNow())) grid.BuildNow();
        }

        /// <summary>Dreiecke der Welt-Deko in einem 1-m-Raster (XZ) für schnelle Kugelprüfungen der Kamera.</summary>
        sealed class DecoGrid
        {
            public readonly Transform Root;
            public volatile bool Ready, Failed;
            public string Error;
            const float Cell = 1f, Pad = 0.3f, Range = 170f;
            const int MaxCells = 900; // riesige Flächen (Wände der Kollisionsboxen) decken die Boxen schon ab
            readonly List<float> t = new List<float>(); // je Dreieck: 9 Koordinaten + Min/Max-Y
            readonly Dictionary<int, List<int>> cells = new Dictionary<int, List<int>>();
            readonly List<Vector3[]> srcV = new List<Vector3[]>();
            readonly List<int[]> srcT = new List<int[]>();
            readonly List<Matrix4x4> srcM = new List<Matrix4x4>();
            public DecoGrid(Transform root) { Root = root; }
            static int Key(int x, int z) { return (x + 2048) * 4096 + (z + 2048); }

            /// <summary>Hauptthread: Meshdaten kopieren (Unity-API).</summary>
            public void Take(Mesh m, Matrix4x4 mx) { srcV.Add(m.vertices); srcT.Add(m.triangles); srcM.Add(mx); }

            /// <summary>Beliebiger Thread: einsortieren (nur eigene Daten und reine Mathematik).</summary>
            public void BuildNow()
            {
                try { for (int i = 0; i < srcV.Count; i++) Add(srcV[i], srcT[i], srcM[i]); }
                catch (Exception e) { Error = e.Message; Failed = true; }
                srcV.Clear(); srcT.Clear(); srcM.Clear();
                Ready = true;
            }

            void Add(Vector3[] v, int[] tri, Matrix4x4 mx)
            {
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = mx.MultiplyPoint3x4(v[i]);
                for (int k = 0; k + 2 < tri.Length; k += 3)
                {
                    Vector3 a = w[tri[k]], b = w[tri[k + 1]], c = w[tri[k + 2]];
                    float x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    float z0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), z1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    float y0 = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), y1 = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    if (x1 < -Range || x0 > Range || z1 < -Range || z0 > Range || y0 > 80f) continue;
                    int gx0 = Mathf.FloorToInt((Mathf.Max(x0, -Range) - Pad) / Cell), gx1 = Mathf.FloorToInt((Mathf.Min(x1, Range) + Pad) / Cell);
                    int gz0 = Mathf.FloorToInt((Mathf.Max(z0, -Range) - Pad) / Cell), gz1 = Mathf.FloorToInt((Mathf.Min(z1, Range) + Pad) / Cell);
                    if ((gx1 - gx0 + 1) * (gz1 - gz0 + 1) > MaxCells) continue;
                    int id = t.Count;
                    t.Add(a.x); t.Add(a.y); t.Add(a.z); t.Add(b.x); t.Add(b.y); t.Add(b.z); t.Add(c.x); t.Add(c.y); t.Add(c.z); t.Add(y0); t.Add(y1);
                    for (int gx = gx0; gx <= gx1; gx++)
                        for (int gz = gz0; gz <= gz1; gz++)
                        {
                            List<int> l;
                            int key = Key(gx, gz);
                            if (!cells.TryGetValue(key, out l)) { l = new List<int>(); cells[key] = l; }
                            l.Add(id);
                        }
                }
            }

            public bool Hit(Vector3 p, float r)
            {
                List<int> l;
                if (!cells.TryGetValue(Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell)), out l)) return false;
                float r2 = r * r;
                foreach (int i in l)
                {
                    if (p.y < t[i + 9] - r || p.y > t[i + 10] + r) continue;
                    var a = new Vector3(t[i], t[i + 1], t[i + 2]); var b = new Vector3(t[i + 3], t[i + 4], t[i + 5]); var c = new Vector3(t[i + 6], t[i + 7], t[i + 8]);
                    if ((ClosestOnTriangle(p, a, b, c) - p).sqrMagnitude < r2) return true;
                }
                return false;
            }

            /// <summary>Nächster Punkt auf einem Dreieck (Ericson, Real-Time Collision Detection 5.1.5).</summary>
            static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 ab = b - a, ac = c - a, ap = p - a;
                float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0f && d2 <= 0f) return a;
                Vector3 bp = p - b;
                float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3) return b;
                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
                Vector3 cp = p - c;
                float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0f && d5 <= d6) return c;
                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
                float va = d3 * d6 - d5 * d4;
                if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
                float denom = va + vb + vc;
                if (Mathf.Abs(denom) < 1e-12f) return a; // entartetes Dreieck
                float v = vb / denom, w2 = vc / denom;
                return a + ab * v + ac * w2;
            }
        }

        void MenuCamera()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null) return;
            menuT += Time.unscaledDeltaTime * 0.03f;
            var b = wv.Layout.Base;
            var center = new Vector3(b.Spawn.x + 3, b.Spawn.y + 1.2f, b.Spawn.z + 14);
            float a = Mathf.Sin(menuT) * 0.6f + Mathf.PI * 1.15f;
            var pos = center + new Vector3(Mathf.Cos(a) * 9f, 2.2f + Mathf.Sin(menuT * 1.7f) * 0.6f, Mathf.Sin(a) * 9f);
            Cam.transform.position = pos;
            Cam.transform.rotation = Quaternion.LookRotation((center + new Vector3(0, 6f, 0) + Vector3.forward * 30f) - pos);
            if (Atmosphere.I != null) { Atmosphere.I.ForcePhase = 0.72f; }
        }

        void Update()
        {
            if (Atmosphere.I != null && GameApp.I != null && GameApp.I.Mode != AppMode.Menu && GameApp.I.Mode != AppMode.PlanetSelect && !Cinematic)
                Atmosphere.I.ForcePhase = -1f;
        }

        // ------------------------------------------------------------ Fotomodus
        void PhotoCamera()
        {
            if (!photoInit)
            {
                photoInit = true;
                photoPos = Cam.transform.position;
                var e = Cam.transform.rotation.eulerAngles;
                photoYaw = e.y; photoPitch = e.x > 180 ? e.x - 360 : e.x;
            }
            if (!string.IsNullOrEmpty(PhotoMode.JumpToViewpoint) && WorldView.I != null && WorldView.I.Layout != null)
            {
                var s = WorldView.I.Layout.FindSpot(PhotoMode.JumpToViewpoint);
                if (s != null) { photoPos = new Vector3(s.Pos.x, s.Pos.y, s.Pos.z); photoYaw = s.Yaw * Mathf.Rad2Deg; photoPitch = 8f; }
                PhotoMode.JumpToViewpoint = null;
            }
            var app = GameApp.I;
            if (!UIRoot.WantsCursor)
            {
                var d = InputMap.Look(app.Settings.MouseSensitivity, app.Settings.PadSensitivity, app.Settings.InvertY);
                photoYaw += d.x; photoPitch = Mathf.Clamp(photoPitch - d.y, -85f, 85f);
            }
            var mv = InputMap.Move();
            float up = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            float speed = (InputMap.Held(GameAction.Sprint) ? 18f : 6f) * Time.unscaledDeltaTime;
            var rot = Quaternion.Euler(photoPitch, photoYaw, 0);
            photoPos += (rot * new Vector3(mv.x, 0, mv.y) + Vector3.up * up) * speed;
            // Fotokamera bleibt in der Nähe des Spielers (max. 60 m), damit sie nicht aus der Welt fliegt
            if (PlayerController.I != null)
            {
                var off = photoPos - PlayerController.I.RenderPos;
                if (off.magnitude > 60f) photoPos = PlayerController.I.RenderPos + off.normalized * 60f;
            }
            if (WorldView.I != null) photoPos.y = Mathf.Max(photoPos.y, Terrain.HeightAt(WorldView.I.Planet, photoPos.x, photoPos.z) + 0.3f);
            Cam.transform.position = photoPos;
            Cam.transform.rotation = rot * Quaternion.Euler(0, 0, PhotoMode.Roll);
        }

        void HandleCapture(GameApp app)
        {
            if (!PhotoMode.RequestCapture) return;
            PhotoMode.RequestCapture = false;
            try
            {
                int w = PhotoMode.Portrait ? 1080 : 1920, h = PhotoMode.Portrait ? 1920 : 1080;
                // HDR-Ziel, damit Bloom/Tonemapping wie im Spiel wirken; danach in ein 8-Bit-Ziel kopieren (ReadPixels-sicher)
                var hdrFmt = HdrFormat();
                var rtHdr = RenderTexture.GetTemporary(w, h, 24, hdrFmt, hdrFmt == RenderTextureFormat.ARGB32 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear, Mathf.Max(1, QualitySettings.antiAliasing));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = Cam.targetTexture;
                Cam.targetTexture = rtHdr;
                Cam.Render();
                Cam.targetTexture = prev;
                Graphics.Blit(rtHdr, rt);
                RenderTexture.ReleaseTemporary(rtHdr);
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                var png = tex.EncodeToPNG();
                Destroy(tex);
                Directory.CreateDirectory(app.PhotoDir);
                string planet = app.W != null ? app.W.CurrentPlanet : "welt";
                string file = Path.Combine(app.PhotoDir, "RePlanet_" + planet + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + (PhotoMode.ShowBefore ? "_vorher" : "") + (PhotoMode.Portrait ? "_hochformat" : "") + ".png");
                File.WriteAllBytes(file, png);
                PhotoMode.LastSavedPath = file;
                Hud.Show("Foto gespeichert: " + file, ToastKind.Success, 5f);
                AudioManager.Play("ui_click");
            }
            catch (Exception e)
            {
                Hud.Show("Foto konnte nicht gespeichert werden: " + e.Message, ToastKind.Error, 6f);
            }
        }

        // ------------------------------------------------------------ Render-Skalierung
        void ApplyRenderScale(GameApp app)
        {
            float s = app.Settings.RenderScale;
            if (s >= 0.99f || PhotoMode.RequestCapture)
            {
                if (Cam.targetTexture != null) { Cam.targetTexture = null; }
                if (scaled != null) { scaled.Release(); Destroy(scaled); scaled = null; }
                return;
            }
            int w = Mathf.Max(320, (int)(Screen.width * s)), h = Mathf.Max(180, (int)(Screen.height * s));
            if (scaled == null || scaled.width != w || scaled.height != h)
            {
                if (scaled != null) { Cam.targetTexture = null; scaled.Release(); Destroy(scaled); }
                scaled = new RenderTexture(w, h, 24, HdrFormat()) { filterMode = FilterMode.Bilinear, antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
                scaled.Create();
            }
            Cam.targetTexture = scaled;
        }

        /// <summary>HDR-Format für Renderziele (sonst gingen Bloom-Spitzen und Tonemapping bei Render-Skalierung verloren).</summary>
        static RenderTextureFormat HdrFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)) return RenderTextureFormat.DefaultHDR;
            return RenderTextureFormat.ARGB32;
        }

        void OnGUI()
        {
            if (scaled == null || Cam.targetTexture != scaled || Event.current.type != EventType.Repaint) return;
            GUI.depth = 1000; // unter der Oberfläche
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), scaled, ScaleMode.StretchToFill, false);
        }
    }
}
