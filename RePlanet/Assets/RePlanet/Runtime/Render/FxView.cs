using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Sichtbare Rückmeldungen: Objekte fliegen in den Behälter, Magnetwelle mit Bogenblitzen, Funken beim Schneiden,
    /// Dampf beim Tauen, „Stadt erwacht“-Feuerwerk, Staub beim Bauen, Glitzern bei Lichtpunkten.
    /// </summary>
    public class FxView : MonoBehaviour
    {
        public static FxView I { get; private set; }

        class Ghost
        {
            public Mesh Mesh; public Material Mat; public TrashType Type;
            public Vector3 From; public string TargetPid; public Vector3 TargetFallback;
            public float T, Delay, Duration, Scale; public Quaternion Rot;
        }

        class Arc { public LineRenderer L; public float Life; public Vector3 A, B; }

        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Arc> arcs = new List<Arc>();
        readonly Stack<LineRenderer> arcPool = new Stack<LineRenderer>();
        ParticleSystem burst, burstAdd;
        Transform ring; float ringT = -1;

        void Awake()
        {
            I = this;
            burst = MakeSystem("BurstFx", false);
            burstAdd = MakeSystem("GlowFx", true);
            var r = new GameObject("MagnetRing");
            r.transform.SetParent(transform, false);
            r.AddComponent<MeshFilter>().sharedMesh = MeshKit.Get("ringFx", b => b.Torus(Vector3.zero, 1f, 0.05f, 32, 4));
            r.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.Fade, new Color(0.4f, 0.8f, 1f, 0.6f));
            ring = r.transform;
            r.SetActive(false);
        }

        void Start() { if (GameApp.I != null) GameApp.I.OnFx += OnFx; }

        ParticleSystem MakeSystem(string name, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1f; main.startSpeed = 3f; main.startSize = 0.2f;
            var em = ps.emission; em.rateOverTime = 0;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Get(additive ? Mats.ParticleAdd : Mats.Particle, Color.white);
            ps.Play();
            return ps;
        }

        float Density { get { var s = GameApp.I != null ? GameApp.I.Settings.Particles : 2; return s == 0 ? 0.3f : s == 1 ? 0.65f : 1f; } }

        /// <summary>Partikelstoß: Position, Farbe, Anzahl, Geschwindigkeit, Größe, Lebensdauer, Schwerkraft.</summary>
        public void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity, bool glow = false)
        {
            var ps = glow ? burstAdd : burst;
            var ep = new ParticleSystem.EmitParams();
            int n = Mathf.Max(1, Mathf.RoundToInt(count * Density));
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.3f;
                var v = Random.insideUnitSphere * speed; v.y = Mathf.Abs(v.y) * 1.3f + speed * 0.3f;
                ep.velocity = v + Vector3.down * 0; ep.startColor = color; ep.startSize = size * Random.Range(0.6f, 1.3f); ep.startLifetime = life * Random.Range(0.7f, 1.2f);
                ps.Emit(ep, 1);
            }
            var main = ps.main; main.gravityModifier = gravity;
        }

        static Vector3 V(List<object> a, Vector3 fallback)
        {
            if (a == null || a.Count < 3) return fallback;
            return new Vector3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0));
        }

        Vector3 RobotBin(string pid, Vector3 fallback)
        {
            var r = ActorsView.I != null ? ActorsView.I.RobotOf(pid) : null;
            if (r == null || !r.gameObject.activeInHierarchy) return fallback;
            return r.transform.position + Vector3.up * 1.2f - r.transform.forward * 0.35f;
        }

        void AddGhost(string type, Vector3 from, string pid, float delay, float duration)
        {
            TrashType t;
            if (type == null || !GameData.Trash.TryGetValue(type, out t)) return;
            ghosts.Add(new Ghost
            {
                Mesh = MeshKit.Trash(t.Shape), Mat = TrashRenderer.MaterialFor(t), Type = t, From = from, TargetPid = pid, TargetFallback = from + Vector3.up,
                Delay = delay, Duration = duration, Scale = Mathf.Min(1f, t.Size), Rot = Quaternion.Euler(0, Random.Range(0, 360), 0)
            });
        }

        void OnFx(JObj f)
        {
            var me = GameApp.I.Me;
            var mePos = me != null ? new Vector3(me.Pos.x, me.Pos.y, me.Pos.z) : Vector3.zero;
            var pos = V(f.Arr("pos"), mePos);
            string pid = f.Str("pid");
            switch (f.Str("k"))
            {
                case "collect":
                    {
                        string tool = f.Str("tool");
                        AddGhost(f.Str("t"), pos, tool == "rover" || tool == "boat" ? null : pid, 0f, tool == "vacuum" ? 0.25f : 0.4f);
                        Burst(pos + Vector3.up * 0.2f, new Color(0.9f, 0.85f, 0.7f, 0.8f), 6, 1.2f, 0.18f, 0.5f, 0.3f);
                        if (pid == GameApp.I.Client.Pid && tool == "grab" && ActorsView.I != null && ActorsView.I.LocalRobot != null) ActorsView.I.LocalRobot.Grab();
                        break;
                    }
                case "magnetwave":
                    {
                        var chain = f.Arr("chain");
                        var origin = RobotBin(pid, mePos) + Vector3.up * 0.2f;
                        if (chain != null)
                            for (int i = 0; i < chain.Count; i++)
                            {
                                var c = chain[i] as List<object>;
                                if (c == null || c.Count < 5) continue;
                                var p = new Vector3((float)Json.ToDouble(c[2], 0), (float)Json.ToDouble(c[3], 0), (float)Json.ToDouble(c[4], 0));
                                AddGhost(c[1] as string, p, pid, i * 0.06f, 0.55f);
                                AddArc(origin, p + Vector3.up * 0.3f, 0.35f + i * 0.06f);
                                Burst(p + Vector3.up * 0.2f, new Color(0.5f, 0.85f, 1f), 5, 2f, 0.12f, 0.4f, 0f, true);
                            }
                        ring.gameObject.SetActive(true);
                        ring.position = origin + Vector3.down * 0.8f;
                        ringT = 0f; ringMax = Mathf.Max(3f, f.Float("r", 7f));
                        if (GameApp.I.Settings.CameraShake && pid == GameApp.I.Client.Pid && CameraRig.I != null) CameraRig.I.Shake(0.25f);
                        break;
                    }
                case "cut":
                    Burst(pos + Vector3.up * 0.6f, new Color(1f, 0.75f, 0.3f), 40, 5f, 0.08f, 0.6f, 1.5f, true);
                    Burst(pos + Vector3.up * 0.4f, new Color(0.5f, 0.5f, 0.5f, 0.6f), 16, 1.5f, 0.5f, 1.2f, -0.1f);
                    break;
                case "thawed":
                    Burst(pos + Vector3.up * 0.8f, new Color(0.9f, 0.95f, 1f, 0.6f), 30, 1.2f, 0.7f, 1.8f, -0.25f);
                    AudioManager.Play("steam", pos);
                    break;
                case "oilclean":
                    Burst(pos + Vector3.up * 0.2f, new Color(0.2f, 0.25f, 0.3f, 0.7f), 24, 2f, 0.3f, 1f, 0.5f);
                    break;
                case "zone":
                    {
                        var wv = WorldView.I;
                        int z = f.Int("z");
                        if (wv != null && wv.Layout != null && z >= 0 && z < wv.Layout.Zones.Count)
                        {
                            var c = wv.Layout.Zones[z].Center;
                            var zc = new Vector3(c.x, c.y, c.z);
                            Burst(zc + Vector3.up * 3.4f, new Color(1f, 0.85f, 0.4f), 60, 3f, 0.15f, 1.6f, 0.2f, true);
                            for (int i = 0; i < 14; i++) Burst(zc + Random.insideUnitSphere * wv.Layout.Zones[z].Radius, new Color(0.5f + Random.value * 0.5f, 0.9f, 0.5f), 3, 1f, 0.25f, 2f, -0.1f, true);
                        }
                        break;
                    }
                case "gate":
                    {
                        int g = f.Int("g");
                        var gp = new Vector3(0, WorldView.I != null && WorldView.I.Layout != null ? WorldView.I.Layout.Base.Center.y : 0, g == 0 ? -50 : 50);
                        Burst(gp + Vector3.up, new Color(0.7f, 0.65f, 0.55f, 0.6f), 80, 6f, 1.2f, 2.5f, -0.05f);
                        break;
                    }
                case "awaken":
                    {
                        var wv = WorldView.I;
                        ProjectDef pd;
                        if (wv != null && GameData.Projects.TryGetValue(f.Str("project") ?? "", out pd) && pd.Planet == wv.Planet)
                        {
                            var site = wv.ProjectSite(pd.Area);
                            var sp = new Vector3(site.x, site.y, site.z);
                            StartCoroutine(Fireworks(sp, f.Bool("great") ? 14 : 6));
                            if (CameraRig.I != null) CameraRig.I.Shake(0.3f);
                        }
                        break;
                    }
                case "build":
                case "demolish":
                case "movebuild":
                    Burst(mePos + Vector3.forward * 3f, new Color(0.7f, 0.65f, 0.55f, 0.6f), 30, 2.5f, 0.7f, 1.2f, 0.2f);
                    break;
                case "repaired":
                    Burst(pos + Vector3.up * 3f, new Color(1f, 0.85f, 0.4f), 40, 3f, 0.1f, 0.8f, 1f, true);
                    break;
                case "plant":
                case "grown":
                    {
                        var wv = WorldView.I;
                        Vector3 p = pos;
                        if (f.Str("s") != null && wv != null && wv.Layout != null) { var s = wv.Layout.FindSpot(f.Str("s")); if (s != null) p = new Vector3(s.Pos.x, s.Pos.y, s.Pos.z); }
                        Burst(p + Vector3.up * 0.8f, new Color(0.5f, 1f, 0.5f), 30, 1.5f, 0.15f, 1.5f, -0.2f, true);
                        break;
                    }
                case "lore":
                    Burst(mePos + Vector3.up * 1.5f, new Color(1f, 0.9f, 0.5f), 30, 1.5f, 0.12f, 1.2f, -0.2f, true);
                    break;
                case "lifted":
                case "dropped":
                case "wreckdone":
                    Burst(mePos + Vector3.forward * 4f, new Color(0.6f, 0.55f, 0.5f, 0.6f), 50, 4f, 1f, 1.8f, 0.1f);
                    if (CameraRig.I != null) CameraRig.I.Shake(0.2f);
                    break;
                case "sleep":
                    if (pid == GameApp.I.Client.Pid) Burst(mePos + Vector3.up * 1.8f, new Color(0.7f, 0.8f, 1f), 6, 0.4f, 0.3f, 2.5f, -0.1f, true);
                    break;
                case "towed":
                    Burst(pos + Vector3.up, new Color(1f, 0.6f, 0.2f), 20, 2f, 0.2f, 1f, 0.3f, true);
                    break;
            }
        }

        float ringMax = 7f;

        System.Collections.IEnumerator Fireworks(Vector3 at, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var p = at + new Vector3(Random.Range(-12f, 12f), Random.Range(12f, 22f), Random.Range(-12f, 12f));
                var c = Color.HSVToRGB(Random.value, 0.6f, 1f);
                Burst(p, c, 80, 6f, 0.2f, 1.8f, 0.4f, true);
                AudioManager.Play("coin", p, 0.4f, Random.Range(0.8f, 1.3f));
                yield return new WaitForSeconds(Random.Range(0.25f, 0.6f));
            }
        }

        void AddArc(Vector3 a, Vector3 b, float life)
        {
            LineRenderer l = arcPool.Count > 0 ? arcPool.Pop() : null;
            if (l == null)
            {
                var go = new GameObject("Arc");
                go.transform.SetParent(transform, false);
                l = go.AddComponent<LineRenderer>();
                l.sharedMaterial = Mats.Get(Mats.ParticleAdd, new Color(0.5f, 0.85f, 1f));
                l.positionCount = 8;
                l.useWorldSpace = true;
                l.startWidth = 0.08f; l.endWidth = 0.02f;
                l.shadowCastingMode = ShadowCastingMode.Off;
            }
            l.gameObject.SetActive(true);
            arcs.Add(new Arc { L = l, Life = life, A = a, B = b });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                if (g.Delay > 0) { g.Delay -= dt; TrashRenderer.DrawTrash(g.Type, Matrix4x4.TRS(g.From, g.Rot, Vector3.one * g.Scale)); continue; }
                g.T += dt / g.Duration;
                var target = g.TargetPid != null ? RobotBin(g.TargetPid, g.TargetFallback) : g.TargetFallback;
                float t = Mathf.Clamp01(g.T);
                var p = Vector3.Lerp(g.From, target, t * t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                float s = g.Scale * Mathf.Lerp(1f, 0.25f, t);
                TrashRenderer.DrawTrash(g.Type, Matrix4x4.TRS(p, g.Rot * Quaternion.Euler(t * 360, t * 180, 0), Vector3.one * s));
                if (g.T >= 1f) ghosts.RemoveAt(i);
            }
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                var a = arcs[i];
                a.Life -= dt;
                if (a.Life <= 0) { a.L.gameObject.SetActive(false); arcPool.Push(a.L); arcs.RemoveAt(i); continue; }
                for (int k = 0; k < 8; k++)
                {
                    float t = k / 7f;
                    var p = Vector3.Lerp(a.A, a.B, t) + (k > 0 && k < 7 ? Random.insideUnitSphere * 0.25f : Vector3.zero) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                    a.L.SetPosition(k, p);
                }
            }
            if (ringT >= 0)
            {
                ringT += dt * 1.6f;
                float s = Mathf.Lerp(0.5f, ringMax, ringT);
                ring.localScale = new Vector3(s, 1, s);
                if (ringT >= 1f) { ringT = -1; ring.gameObject.SetActive(false); }
            }
        }
    }
}
