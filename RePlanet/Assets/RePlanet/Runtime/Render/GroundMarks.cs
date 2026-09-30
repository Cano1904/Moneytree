using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Spuren am Boden: <b>Reifenspuren</b> hinter MIKO (drei Räder) und den Fahrzeugen (Rover: Reifen, Kran: Ketten)
    /// auf Sand, Schnee und Erde – nicht auf Straßen, Plätzen und im Stützpunkt. Sie verblassen mit der Zeit, Wind und
    /// Stürme verwehen sie schneller (PYRA-Sand, NIVALIS-Schnee). <b>Staubwolken</b> steigen auf, wenn MIKO oder ein
    /// Fahrzeug schnell über PYRA oder TERRA fährt (auf NIVALIS stiebt Schnee). <b>Pfützen</b> bilden sich im Regen
    /// (Seesturm auf PELAGIA) auf flachen Stellen und trocknen danach langsam ein – kleine zuerst.
    /// Alles folgt dem replizierten Zustand (Positionen, Sturm, Sturmzeit), sodass Mitspieler dieselben Spuren und
    /// Pfützen sehen. Zeichnung per Instancing mit wenigen Transparenz-Stufen (Verblassen ohne eigenen Shader).
    /// </summary>
    public class GroundMarks : MonoBehaviour
    {
        public static GroundMarks I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(GroundMarks))) GameApp.Components.Add(typeof(GroundMarks));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<GroundMarks>() == null) GameApp.I.gameObject.AddComponent<GroundMarks>();
        }

        struct Stamp
        {
            public Vector3 Pos; public Quaternion Rot; public float Len, Width, Age, Life; public int Kind; public bool Live;
        }

        class Wheel { public Vector3 Last; public bool Has; public float LastSeen; }

        const int Buckets = 5;
        static readonly float[] BucketAlpha = { 0.6f, 0.48f, 0.36f, 0.24f, 0.12f };

        Stamp[] stamps = new Stamp[0];
        int head;
        readonly Dictionary<string, Wheel> wheels = new Dictionary<string, Wheel>();
        readonly Dictionary<string, Vector3> lastPos = new Dictionary<string, Vector3>();
        readonly Dictionary<string, float> dustTimer = new Dictionary<string, float>();
        InstanceBatch[,] trackBatches;
        Material[,] trackMats;
        Texture2D tyreTex, chainTex;
        string planet;
        PlanetLayout layout;
        float water;

        // Pfützen
        class Puddle { public Vector3 Pos; public float Radius, Rot, DryAt; public int Shape; }
        readonly List<Puddle> puddles = new List<Puddle>();
        InstanceBatch[] puddleBatches;
        Material puddleMat;
        float wet;

        /// <summary>Statistik: lebende Spurstücke, gezeichnete Spurstücke, sichtbare Pfützen, Staubstöße insgesamt.</summary>
        public int LiveStamps { get; private set; }
        public int DrawnStamps { get; private set; }
        public int VisiblePuddles { get; private set; }
        public int PuddleSpots { get { return puddles.Count; } }
        public float Wetness { get { return wet; } }
        public long DustPuffs { get; private set; }
        public int Capacity { get { return stamps.Length; } }

        void Awake()
        {
            I = this;
            tyreTex = TreadTexture(false);
            chainTex = TreadTexture(true);
            var quad = QuadMesh();
            trackMats = new Material[2, Buckets];
            trackBatches = new InstanceBatch[2, Buckets];
            for (int k = 0; k < 2; k++)
                for (int b = 0; b < Buckets; b++)
                {
                    var m = Mats.Unique(Mats.Fade, new Color(0.3f, 0.25f, 0.2f, BucketAlpha[b]));
                    m.mainTexture = k == 0 ? tyreTex : chainTex;
                    m.renderQueue = 2990; // vor anderem Transparenten (Wasser, Partikel)
                    trackMats[k, b] = m;
                    trackBatches[k, b] = new InstanceBatch("spur" + k + "_" + b, quad, new[] { m });
                }
            puddleMat = Mats.Unique(Mats.Water, new Color(0.3f, 0.36f, 0.42f, 0.62f));
            if (puddleMat.HasProperty("_Glossiness")) puddleMat.SetFloat("_Glossiness", 0.97f);
            puddleBatches = new InstanceBatch[3];
            for (int s = 0; s < 3; s++) puddleBatches[s] = new InstanceBatch("pfuetze" + s, PuddleMesh(s), new[] { puddleMat });
        }

        void OnDestroy()
        {
            if (tyreTex != null) Destroy(tyreTex);
            if (chainTex != null) Destroy(chainTex);
            if (I == this) I = null;
        }

        // ================================================================== Formen und Texturen
        /// <summary>Flaches Einheitsviereck in der XZ-Ebene (U entlang der Fahrtrichtung Z).</summary>
        public static Mesh QuadMesh()
        {
            return MeshKit.Get("spur_viereck", b => b.Quad(new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f), Vector3.up));
        }

        /// <summary>Unregelmäßige, flache Pfützenform (Einheitsradius) als Fächer.</summary>
        public static Mesh PuddleMesh(int shape)
        {
            return MeshKit.Get("pfuetze_" + shape, b =>
            {
                const int seg = 14;
                var pts = new Vector3[seg];
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float r = 0.78f + 0.22f * MeshBuilder.Hash01(i, shape, 41) + 0.12f * Mathf.Sin(a * (2 + shape));
                    pts[i] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                }
                for (int i = 0; i < seg; i++) b.TriFace(Vector3.zero, pts[i], pts[(i + 1) % seg], Vector3.up);
            });
        }

        /// <summary>Profil als Transparenz: Reifen mit Pfeilprofil, Kette mit Querstegen; Ränder weich.</summary>
        static Texture2D TreadTexture(bool chain)
        {
            const int W = 64, H = 32; // U = Fahrtrichtung, V = quer
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = chain ? "RP_Kettenspur" : "RP_Reifenspur", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = (y + 0.5f) / H;
                    float across = Mathf.Abs(v - 0.5f) * 2f;
                    float edge = Mathf.Clamp01((1f - across) * 4f);
                    float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 10f);
                    float groove;
                    if (chain) groove = Mathf.Repeat(u * 8f, 1f) < 0.45f ? 1f : 0.45f;
                    else groove = Mathf.Repeat(u * 6f + across * 0.9f, 1f) < 0.5f ? 1f : 0.5f;
                    float a = edge * (0.55f + 0.45f * groove) * Mathf.Lerp(0.6f, 1f, ends);
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(0.7f, 1f, groove) * 255f);
                    px[y * W + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        // ================================================================== Planet
        void Build(string p)
        {
            planet = p;
            layout = WorldGen.Get(p);
            water = Terrain.WaterLevel(p);
            for (int i = 0; i < stamps.Length; i++) stamps[i].Live = false;
            wheels.Clear(); lastPos.Clear(); dustTimer.Clear();
            Color c = p == "pyra" ? new Color(0.42f, 0.2f, 0.12f) : p == "nivalis" ? new Color(0.5f, 0.58f, 0.74f) : p == "pelagia" ? new Color(0.42f, 0.36f, 0.26f) : new Color(0.24f, 0.19f, 0.14f);
            for (int k = 0; k < 2; k++) for (int b = 0; b < Buckets; b++) trackMats[k, b].color = new Color(c.r, c.g, c.b, BucketAlpha[b]);
            BuildPuddles();
            wet = 0f;
        }

        public static bool RainPlanet(string p) { return p == "pelagia"; }

        /// <summary>Pfützenplätze (deterministisch): flache Stellen auf Straßen, Plätzen und Wegen, nicht in Gebäuden.</summary>
        void BuildPuddles()
        {
            puddles.Clear();
            if (!RainPlanet(planet)) return;
            var tmp = new List<Box>();
            var rng = new Rng(GameData.Planets[planet].Seed + 555);
            for (int t = 0; t < 900 && puddles.Count < 70; t++)
            {
                float x = rng.Range(-140f, 140f), z = rng.Range(-146f, 140f);
                float r = rng.Range(0.7f, 2.6f);
                // bevorzugt auf Straßen (Senken im Belag), sonst freie flache Stellen
                if (t % 3 != 0 && !LifeCommon.OnRoad(layout, x, z, -r)) continue;
                float h0 = Terrain.HeightAt(planet, x, z);
                if (water > -50f && h0 < water + 0.5f) continue;
                if (LifeCommon.Solid(layout, x, z, r + 0.4f, tmp)) continue;
                if (layout.GroundAt(x, z) > h0 + 0.05f) continue;
                bool flat = true;
                for (int k = 0; k < 8 && flat; k++)
                {
                    float a = k * 0.785f;
                    if (Mathf.Abs(Terrain.HeightAt(planet, x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r) - h0) > 0.045f) flat = false;
                }
                if (!flat) continue;
                bool overlap = false;
                foreach (var q in puddles) if ((new Vector2(q.Pos.x - x, q.Pos.z - z)).magnitude < q.Radius + r + 0.8f) { overlap = true; break; }
                if (overlap) continue;
                puddles.Add(new Puddle { Pos = new Vector3(x, h0 + 0.035f, z), Radius = r, Rot = rng.Range(0f, 360f), DryAt = rng.Range(0.1f, 0.9f) * (1.2f - r / 2.6f * 0.4f), Shape = rng.Range(0, 3) });
            }
        }

        // ================================================================== Aktualisierung
        void Update()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null || string.IsNullOrEmpty(wv.Planet)) return;
            if (planet != wv.Planet) Build(wv.Planet);
            int want = LifeCommon.Quality <= 0 ? 300 : LifeCommon.Quality == 1 ? 700 : LifeCommon.Quality == 2 ? 1200 : 2000;
            if (stamps.Length != want) { stamps = new Stamp[want]; head = 0; }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            var w = wv.World;
            PlanetState ps = null;
            if (w != null) w.Planets.TryGetValue(planet, out ps);
            float wind = 0.2f, dx, dz;
            if (w != null && ps != null) wind = Rules.Wind(w, planet, out dx, out dz);
            bool storm = ps != null && ps.StormActive;
            AgeStamps(dt, wind, storm);
            if (GameApp.I != null && GameApp.I.InGame && w != null) EmitFromMovers(w, dt);
            UpdateWetness(ps, w, dt);
            Draw();
        }

        void AgeStamps(float dt, float wind, bool storm)
        {
            // Sand und Schnee werden verweht; im Sturm verschwinden Spuren binnen Sekunden
            float drift = planet == "pyra" || planet == "nivalis" ? 1f + wind * 1.5f : 1f + wind * 0.4f;
            float rate = drift * (storm ? 6f : 1f);
            int live = 0;
            for (int i = 0; i < stamps.Length; i++)
            {
                if (!stamps[i].Live) continue;
                stamps[i].Age += dt * rate;
                if (stamps[i].Age >= stamps[i].Life) { stamps[i].Live = false; continue; }
                live++;
            }
            LiveStamps = live;
        }

        float BaseLife { get { return planet == "nivalis" ? 150f : planet == "pyra" ? 70f : planet == "pelagia" ? 60f : 110f; } }

        /// <summary>Weicher Boden, auf dem Spuren bleiben (kein Belag, kein Stützpunkt, kein Laderaum, nicht unter Wasser).</summary>
        public bool SoftGround(float x, float z)
        {
            if (layout == null || !LifeCommon.InWorld(x, z, 2f) || layout.Base.InBase(x, z)) return false;
            if (LifeCommon.OnRoad(layout, x, z, 0.3f)) return false;
            float h = Terrain.HeightAt(planet, x, z);
            if (layout.GroundAt(x, z) > h + 0.05f) return false;
            if (water > -50f && (h < water + 0.02f || (planet == "pelagia" && h > water + 1.8f))) return false;
            return true;
        }

        void EmitFromMovers(WorldState w, float dt)
        {
            var av = ActorsView.I;
            if (av == null) return;
            float now = Time.time;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online || p.TowTimer > 0) continue;
                if (p.Vehicle == null)
                {
                    var r = av.RobotOf(p.Id);
                    if (r == null || !r.gameObject.activeInHierarchy) continue;
                    var tr = r.transform;
                    var pos = tr.position;
                    float h = Terrain.HeightAt(planet, pos.x, pos.z);
                    if (Mathf.Abs(pos.y - h) > 0.35f) { ForgetMover(p.Id); continue; } // schwimmt, auf Rampe, angehoben
                    float yaw = tr.eulerAngles.y;
                    Track(p.Id + ":l", pos, yaw, new Vector3(-0.47f, 0, -0.32f), 0.17f, 0, now);
                    Track(p.Id + ":r", pos, yaw, new Vector3(0.47f, 0, -0.32f), 0.17f, 0, now);
                    Track(p.Id + ":f", pos, yaw, new Vector3(0f, 0, 0.45f), 0.13f, 0, now);
                    Dust(p.Id, pos, yaw, -0.5f, 5f, dt, 0.6f);
                }
            }
            foreach (var v in w.Cur.Vehicles.Values)
            {
                if (v.Id == "boat") continue;
                Vector3 pos; float yaw;
                if (!av.VehiclePose(v.Id, out pos, out yaw)) continue;
                if (v.Id == "crane")
                {
                    Track(v.Id + ":l", pos, yaw, new Vector3(-1.3f, 0, -2.0f), 0.9f, 1, now);
                    Track(v.Id + ":r", pos, yaw, new Vector3(1.3f, 0, -2.0f), 0.9f, 1, now);
                    Dust(v.Id, pos, yaw, -2.3f, 3.5f, dt, 1.4f);
                }
                else
                {
                    Track(v.Id + ":l", pos, yaw, new Vector3(-1.15f, 0, -1.4f), 0.42f, 0, now);
                    Track(v.Id + ":r", pos, yaw, new Vector3(1.15f, 0, -1.4f), 0.42f, 0, now);
                    Dust(v.Id, pos, yaw, -2.2f, 5f, dt, 1.3f);
                }
            }
            // Einträge verschwundener Fahrer/Fahrzeuge aufräumen
            if (wheels.Count > 64)
            {
                var old = new List<string>();
                foreach (var kv in wheels) if (now - kv.Value.LastSeen > 30f) old.Add(kv.Key);
                foreach (var k in old) wheels.Remove(k);
            }
        }

        void ForgetMover(string id)
        {
            Wheel wl;
            foreach (var s in new[] { ":l", ":r", ":f" }) if (wheels.TryGetValue(id + s, out wl)) wl.Has = false;
        }

        /// <summary>Spurstück hinter einem Rad: alle ~0,45 m vom letzten Abdruck bis zur aktuellen Radposition.</summary>
        void Track(string key, Vector3 pos, float yawDeg, Vector3 local, float width, int kind, float now)
        {
            var wp = pos + Quaternion.Euler(0, yawDeg, 0) * local;
            Wheel wl;
            if (!wheels.TryGetValue(key, out wl)) { wl = new Wheel(); wheels[key] = wl; }
            wl.LastSeen = now;
            if (!wl.Has) { wl.Last = wp; wl.Has = true; return; }
            var d = wp - wl.Last; d.y = 0;
            float len = d.magnitude;
            if (len < 0.45f) return;
            if (len > 3f || !LifeCommon.Finite(wp)) { wl.Last = wp; return; } // Sprung (Zurücksetzen, Reise)
            var mid = (wp + wl.Last) * 0.5f;
            wl.Last = wp;
            if (!SoftGround(mid.x, mid.z)) return;
            float h = Terrain.HeightAt(planet, mid.x, mid.z);
            var n = LifeCommon.TerrainNormal(planet, mid.x, mid.z, 0.5f);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, yaw, 0);
            stamps[head] = new Stamp { Pos = new Vector3(mid.x, h + 0.045f, mid.z), Rot = rot, Len = len + 0.06f, Width = width, Age = 0f, Life = BaseLife, Kind = kind, Live = true };
            head = (head + 1) % stamps.Length;
        }

        /// <summary>Staubwolke hinter schnell fahrenden Robotern/Fahrzeugen (PYRA, TERRA; auf NIVALIS Schneestaub).</summary>
        void Dust(string id, Vector3 pos, float yawDeg, float back, float minSpeed, float dt, float size)
        {
            Vector3 prev;
            bool had = lastPos.TryGetValue(id, out prev);
            lastPos[id] = pos;
            if (!had || dt <= 0f) return;
            var d = pos - prev; d.y = 0;
            float speed = d.magnitude / dt;
            if (speed < minSpeed || speed > 40f) return;
            if (planet != "pyra" && planet != "terra" && planet != "nivalis") return;
            float timer;
            dustTimer.TryGetValue(id, out timer);
            timer -= dt;
            if (timer > 0f) { dustTimer[id] = timer; return; }
            float k = Mathf.Clamp01((speed - minSpeed) / (minSpeed * 1.5f));
            dustTimer[id] = Mathf.Lerp(0.16f, 0.06f, k);
            var at = pos + Quaternion.Euler(0, yawDeg, 0) * new Vector3(0, 0, back);
            if (!SoftGround(at.x, at.z) || FxView.I == null) return;
            Color c = planet == "pyra" ? new Color(0.78f, 0.45f, 0.3f, 0.5f) : planet == "nivalis" ? new Color(0.95f, 0.97f, 1f, 0.6f) : new Color(0.72f, 0.64f, 0.5f, 0.42f);
            FxView.I.Burst(at + Vector3.up * 0.25f, c, 2 + Mathf.RoundToInt(k * 3f), 0.9f + k, size * (0.7f + k * 0.5f), 1.3f + k * 0.8f, -0.03f);
            DustPuffs++;
        }

        /// <summary>
        /// Nässe 0..1 aus dem replizierten Wetter: steigt im Regen (Seesturm), sinkt danach mit der Zeit seit dem Sturm
        /// (Sturmzeit ist synchron – Mitspieler sehen dieselben Pfützen). Nachts trocknet es langsamer.
        /// </summary>
        void UpdateWetness(PlanetState ps, WorldState w, float dt)
        {
            float target = 0f;
            if (ps != null && RainPlanet(planet) && !LifeCommon.BeforeView)
            {
                if (ps.StormActive) target = Mathf.Clamp01(0.25f + ps.StormTimer / 35f);
                else if (ps.StormCount > 0)
                {
                    bool night = w != null && Rules.IsNight(w, planet);
                    float dryTime = night ? 380f : 240f;
                    target = 1f - Mathf.Clamp01(ps.StormTimer / dryTime);
                }
            }
            wet = Mathf.MoveTowards(wet, target, dt * 0.08f);
        }

        // ================================================================== Zeichnen
        void Draw()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cp = cam.transform.position; var cf = cam.transform.forward;
            float view = 60f * LifeCommon.ViewScale;
            for (int k = 0; k < 2; k++) for (int b = 0; b < Buckets; b++) trackBatches[k, b].Clear();
            int drawn = 0;
            for (int i = 0; i < stamps.Length; i++)
            {
                if (!stamps[i].Live) continue;
                var d = stamps[i].Pos - cp;
                float d2 = d.sqrMagnitude;
                if (d2 > view * view) continue;
                if (d2 > 36f && Vector3.Dot(d, cf) < -0.3f * Mathf.Sqrt(d2)) continue;
                int b = Mathf.Clamp((int)(stamps[i].Age / stamps[i].Life * Buckets), 0, Buckets - 1);
                trackBatches[stamps[i].Kind, b].Add(Matrix4x4.TRS(stamps[i].Pos, stamps[i].Rot, new Vector3(stamps[i].Width, 1f, stamps[i].Len)));
                drawn++;
            }
            DrawnStamps = drawn;
            for (int k = 0; k < 2; k++) for (int b = 0; b < Buckets; b++) trackBatches[k, b].Draw(false);

            // Pfützen: kleine trocknen zuerst (Schwelle je Pfütze), Deckkraft folgt der Nässe
            int vis = 0;
            foreach (var pb in puddleBatches) pb.Clear();
            if (wet > 0.01f)
            {
                var c = puddleMat.color; c.a = Mathf.Lerp(0.35f, 0.68f, wet); puddleMat.color = c;
                float pv = 90f * LifeCommon.ViewScale;
                foreach (var q in puddles)
                {
                    float s = LifeCommon.Smooth(q.DryAt * 0.8f, q.DryAt * 0.8f + 0.25f, wet);
                    if (s < 0.05f) continue;
                    if ((q.Pos - cp).sqrMagnitude > pv * pv) continue;
                    puddleBatches[q.Shape].Add(Matrix4x4.TRS(q.Pos, Quaternion.Euler(0, q.Rot, 0), new Vector3(q.Radius * s, 1f, q.Radius * s * 0.85f)));
                    vis++;
                }
                foreach (var pb in puddleBatches) pb.Draw(false);
            }
            VisiblePuddles = vis;
        }

        /// <summary>Prüfumgebung: alle lebenden Spurstücke endlich, auf dem Boden (±0,3 m) und nicht auf Straßen.</summary>
        public int Validate(List<string> problems)
        {
            int bad = 0;
            for (int i = 0; i < stamps.Length; i++)
            {
                if (!stamps[i].Live) continue;
                var p = stamps[i].Pos;
                if (!LifeCommon.Finite(p)) { bad++; if (problems != null && problems.Count < 6) problems.Add("Spur mit NaN-Position"); continue; }
                float h = Terrain.HeightAt(planet, p.x, p.z);
                if (Mathf.Abs(p.y - h) > 0.3f) { bad++; if (problems != null && problems.Count < 6) problems.Add("Spur schwebt/steckt bei " + p + " (Boden " + h.ToString("0.00") + ")"); }
            }
            return bad;
        }

        /// <summary>Prüfumgebung: eine Spur zwischen zwei Punkten erzwingen (wie ein Rad, das von a nach b rollt).</summary>
        public void TestTrack(Vector3 a, Vector3 b)
        {
            var k = "test";
            wheels.Remove(k);
            Track(k, a, 0f, Vector3.zero, 0.3f, 0, Time.time);
            Track(k, b, 0f, Vector3.zero, 0.3f, 0, Time.time);
        }
    }
}
