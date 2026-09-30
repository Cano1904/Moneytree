using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Darstellung der Zusatzsysteme: defekte und reparierte Helferroboter (MIKO-Bauweise, kleiner, eigene Lackierung),
    /// Rohrpost-Kapseln zum Lager, Meteoritenschauer mit Leuchtspuren, Versorgungsabwurf mit Rauchzeichen, Staubwolke über
    /// freigelegten Deponien und Lichtsäulen bei der Schnellreise. Alles aus Serverereignissen (fx) und dem replizierten Zustand.
    /// </summary>
    public class FeaturesView : MonoBehaviour
    {
        public static FeaturesView I { get; private set; }

        class Streak
        {
            public LineRenderer L; public Vector3 From, To; public float T, Delay, Duration; public string Type; public Color Col; public bool Slow;
        }

        class Capsule { public Vector3 From, To; public float T, Duration; }
        class Beam { public LineRenderer L; public float Life, Max; }

        readonly Dictionary<string, RobotModel> bots = new Dictionary<string, RobotModel>();
        readonly Dictionary<string, Vector3> botRender = new Dictionary<string, Vector3>();
        readonly List<Streak> streaks = new List<Streak>();
        readonly List<Capsule> capsules = new List<Capsule>();
        readonly List<Beam> beams = new List<Beam>();
        readonly Stack<LineRenderer> linePool = new Stack<LineRenderer>();
        Transform root;
        string planet;
        float smokeTimer;

        void Awake() { I = this; root = new GameObject("Features").transform; root.SetParent(transform, false); }

        void Start() { if (GameApp.I != null) GameApp.I.OnFx += OnFx; }

        void OnDestroy() { if (GameApp.I != null) GameApp.I.OnFx -= OnFx; }

        static Vector3 V(List<object> a, Vector3 fallback)
        {
            if (a == null || a.Count < 3) return fallback;
            return new Vector3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0));
        }

        LineRenderer Line(Color c, float w0, float w1)
        {
            LineRenderer l = linePool.Count > 0 ? linePool.Pop() : null;
            if (l == null)
            {
                var go = new GameObject("FeatureLine");
                go.transform.SetParent(root, false);
                l = go.AddComponent<LineRenderer>();
                l.positionCount = 2;
                l.useWorldSpace = true;
                l.shadowCastingMode = ShadowCastingMode.Off;
            }
            l.sharedMaterial = Mats.Get(Mats.ParticleAdd, c);
            l.startWidth = w0; l.endWidth = w1;
            l.gameObject.SetActive(true);
            return l;
        }

        void Release(LineRenderer l) { if (l == null) return; l.gameObject.SetActive(false); linePool.Push(l); }

        static void Burst(Vector3 p, Color c, int n, float speed, float size, float life, float gravity, bool glow)
        {
            if (FxView.I != null) FxView.I.Burst(p, c, n, speed, size, life, gravity, glow);
        }

        void OnFx(JObj f)
        {
            var me = GameApp.I != null ? GameApp.I.Me : null;
            var mePos = me != null ? new Vector3(me.Pos.x, me.Pos.y, me.Pos.z) : Vector3.zero;
            var pos = V(f.Arr("pos"), mePos);
            switch (f.Str("k"))
            {
                case "meteor":
                    {
                        var pts = f.Arr("pts");
                        int i = 0;
                        var dir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                        if (pts != null)
                            foreach (var o in pts)
                            {
                                var to = V(o as List<object>, pos);
                                var from = to + dir * 70f + Vector3.up * 110f + Random.insideUnitSphere * 8f;
                                streaks.Add(new Streak { L = Line(new Color(1f, 0.75f, 0.45f), 0.9f, 0.05f), From = from, To = to, Delay = i * Random.Range(0.25f, 0.6f), Duration = 1.3f, Type = "meteorit", Col = new Color(1f, 0.6f, 0.3f) });
                                i++;
                            }
                        AudioManager.Play("whoosh", pos, 0.6f, 0.6f);
                        break;
                    }
                case "supply":
                    {
                        var pts = f.Arr("pts");
                        if (pts != null)
                            foreach (var o in pts)
                            {
                                var to = V(o as List<object>, pos);
                                streaks.Add(new Streak { L = Line(new Color(1f, 0.55f, 0.2f), 0.25f, 0.02f), From = to + Vector3.up * 90f, To = to, Duration = 6f, Type = "versorgungskiste", Col = new Color(1f, 0.5f, 0.15f), Slow = true });
                            }
                        AudioManager.Play("whoosh", pos, 0.4f, 0.8f);
                        break;
                    }
                case "dump":
                    for (int k = 0; k < 6; k++) Burst(pos + Random.insideUnitSphere * 5f + Vector3.up, new Color(0.62f, 0.55f, 0.45f, 0.6f), 30, 3f, 1.4f, 3f, -0.05f, false);
                    break;
                case "fasttravel":
                    {
                        var from = V(f.Arr("from"), pos);
                        AddBeam(from, new Color(0.4f, 1f, 0.9f));
                        AddBeam(pos, new Color(0.4f, 1f, 0.9f));
                        Burst(from + Vector3.up, new Color(0.5f, 1f, 0.9f), 50, 3f, 0.15f, 1.2f, -0.3f, true);
                        Burst(pos + Vector3.up, new Color(0.5f, 1f, 0.9f), 60, 3f, 0.15f, 1.4f, -0.3f, true);
                        AudioManager.Play("beep_happy", pos, 0.5f, 1.3f);
                        break;
                    }
                case "botfixed":
                    Burst(pos + Vector3.up * 1f, new Color(1f, 0.85f, 0.4f), 50, 3f, 0.12f, 1f, 0.6f, true);
                    AudioManager.Play("beep_happy", pos, 0.6f, 0.9f);
                    break;
                case "botpick":
                    Burst(pos + Vector3.up * 0.4f, new Color(0.7f, 0.95f, 1f), 8, 1f, 0.1f, 0.6f, 0.2f, true);
                    break;
                case "botsend":
                    {
                        var to = V(f.Arr("to"), pos);
                        capsules.Add(new Capsule { From = pos + Vector3.up * 1.2f, To = to + Vector3.up * 1.5f, Duration = Mathf.Clamp(Vector3.Distance(pos, to) / 40f, 1.5f, 6f) });
                        Burst(pos + Vector3.up, new Color(0.9f, 0.9f, 1f, 0.7f), 20, 2f, 0.4f, 0.8f, 0.3f, false);
                        break;
                    }
            }
        }

        void AddBeam(Vector3 at, Color c)
        {
            var l = Line(c, 1.2f, 0.2f);
            l.SetPosition(0, at);
            l.SetPosition(1, at + Vector3.up * 40f);
            beams.Add(new Beam { L = l, Life = 1.4f, Max = 1.4f });
        }

        void ClearAll()
        {
            foreach (var r in bots.Values) if (r != null) Destroy(r.gameObject);
            bots.Clear(); botRender.Clear();
            foreach (var s in streaks) Release(s.L);
            streaks.Clear(); capsules.Clear();
            foreach (var b in beams) Release(b.L);
            beams.Clear();
        }

        void Update()
        {
            var app = GameApp.I;
            var wv = WorldView.I;
            if (app == null || wv == null || wv.Layout == null || app.W == null || !app.InGame) { if (bots.Count > 0) ClearAll(); return; }
            var w = app.W;
            if (planet != w.CurrentPlanet) { ClearAll(); planet = w.CurrentPlanet; }
            float dt = Time.deltaTime;
            UpdateBots(app, w, dt);
            UpdateStreaks(dt);
            UpdateCapsules(dt);
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                b.Life -= dt;
                if (b.Life <= 0) { Release(b.L); beams.RemoveAt(i); continue; }
                float k = b.Life / b.Max;
                b.L.startWidth = 1.2f * k; b.L.endWidth = 0.2f * k;
            }
            // Ereignisfunde in der Nähe: Rauchzeichen über Versorgungskisten, Glimmen der Meteoritensplitter
            smokeTimer -= dt;
            if (smokeTimer <= 0 && Camera.main != null)
            {
                smokeTimer = 0.6f;
                var cam = Camera.main.transform.position;
                int n = 0;
                foreach (var d in w.Cur.Dyn.Values)
                {
                    if (d.Ev <= 0 || d.CarriedBy != null || n > 12) continue;
                    var p = new Vector3(d.Pos.x, d.Pos.y, d.Pos.z);
                    if ((p - cam).sqrMagnitude > 140f * 140f) continue;
                    n++;
                    if (d.Ev == 2) Burst(p + Vector3.up * 1.2f, new Color(1f, 0.45f, 0.15f, 0.55f), 4, 0.6f, 1.1f, 3.5f, -0.12f, false);
                    else if (d.Ev == 1) Burst(p + Vector3.up * 0.4f, new Color(0.75f, 0.55f, 1f), 2, 0.3f, 0.12f, 0.8f, -0.1f, true);
                }
            }
        }

        // ------------------------------------------------------------ Helferroboter
        void UpdateBots(GameApp app, WorldState w, float dt)
        {
            var l = WorldGen.Get(w.CurrentPlanet);
            var client = app.Client;
            float dark = Atmosphere.I != null ? Atmosphere.I.Darkness : 0f;
            foreach (var s in l.Bots)
            {
                HelperBot hb;
                bool fixedBot = w.Cur.Bots.TryGetValue(s.Id, out hb);
                RobotModel r;
                if (!bots.TryGetValue(s.Id, out r) || r == null)
                {
                    r = RobotModel.Create(root, "Helfer_" + s.Id);
                    if (r.Headlight != null) r.Headlight.enabled = false;
                    r.transform.localScale = Vector3.one * 0.78f;
                    bots[s.Id] = r;
                }
                if (!fixedBot)
                {
                    // Defekt: rostig, leicht umgekippt, Augen aus
                    r.SetCosmetics("c_rost", "a_kupfer", "s_none", "x_none");
                    r.transform.position = new Vector3(s.Pos.x, s.Pos.y - 0.05f, s.Pos.z);
                    r.transform.rotation = Quaternion.Euler(8f, s.Yaw * Mathf.Rad2Deg, 14f);
                    r.Animate(dt, 0f, 0.1f, "grab", false, false, false, true, dark, r.transform.forward);
                    botRender.Remove(s.Id);
                    continue;
                }
                r.SetCosmetics("c_perlweiss", "a_himmel", "zahnrad", "antenne");
                float[] sp;
                Vector3 target = new Vector3(hb.Pos.x, hb.Pos.y, hb.Pos.z);
                float yaw = hb.Yaw;
                if (client != null && client.Bots.TryGetValue(s.Id, out sp)) { target = new Vector3(sp[0], sp[1], sp[2]); yaw = sp[3]; }
                Vector3 cur;
                if (!botRender.TryGetValue(s.Id, out cur) || (cur - target).sqrMagnitude > 400f) cur = target;
                var next = Vector3.Lerp(cur, target, Mathf.Clamp01(dt * 8f));
                float speed = new Vector2(next.x - cur.x, next.z - cur.z).magnitude / Mathf.Max(dt, 0.001f);
                botRender[s.Id] = next;
                r.transform.position = next;
                r.transform.rotation = Quaternion.Slerp(r.transform.rotation, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0), Mathf.Clamp01(dt * 6f));
                float load = Mathf.Clamp01(hb.Load.Count / (float)GameData.HelperLoad);
                bool storm = w.Cur.StormActive && hb.Follow == null;
                r.Animate(dt, speed, load, "grab", false, false, storm, false, dark, r.transform.forward);
            }
        }

        // ------------------------------------------------------------ Leuchtspuren, Kapseln
        void UpdateStreaks(float dt)
        {
            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                var s = streaks[i];
                if (s.Delay > 0) { s.Delay -= dt; s.L.SetPosition(0, s.From); s.L.SetPosition(1, s.From); continue; }
                s.T += dt / s.Duration;
                float t = Mathf.Clamp01(s.T);
                float e = s.Slow ? t : t * t;
                var head = Vector3.Lerp(s.From, s.To, e);
                var tail = Vector3.Lerp(s.From, s.To, Mathf.Max(0f, e - (s.Slow ? 0.25f : 0.35f)));
                s.L.SetPosition(0, tail); s.L.SetPosition(1, head);
                TrashRenderer.DrawTrash(GameData.Trash[s.Type], Matrix4x4.TRS(head, Quaternion.Euler(t * 400f, t * 170f, 0), Vector3.one * (s.Slow ? 1f : 0.8f)));
                if (!s.Slow && Random.value < 0.5f) Burst(head, s.Col, 2, 0.5f, 0.35f, 0.5f, 0f, true);
                if (s.T >= 1f)
                {
                    Burst(s.To + Vector3.up * 0.3f, s.Slow ? new Color(0.65f, 0.6f, 0.5f, 0.6f) : s.Col, s.Slow ? 20 : 40, s.Slow ? 1.5f : 4f, s.Slow ? 0.8f : 0.2f, 1.2f, 0.5f, !s.Slow);
                    if (!s.Slow)
                    {
                        Burst(s.To + Vector3.up * 0.3f, new Color(0.5f, 0.45f, 0.4f, 0.6f), 20, 2f, 1f, 1.6f, -0.05f, false);
                        if (CameraRig.I != null && Camera.main != null && (Camera.main.transform.position - s.To).sqrMagnitude < 60f * 60f) CameraRig.I.Shake(0.12f);
                        AudioManager.Play("metal", s.To, 0.6f, 0.55f);
                    }
                    Release(s.L);
                    streaks.RemoveAt(i);
                }
            }
        }

        void UpdateCapsules(float dt)
        {
            for (int i = capsules.Count - 1; i >= 0; i--)
            {
                var c = capsules[i];
                c.T += dt / c.Duration;
                float t = Mathf.Clamp01(c.T);
                var p = Vector3.Lerp(c.From, c.To, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (8f + Vector3.Distance(c.From, c.To) * 0.12f);
                TrashRenderer.DrawTrash(GameData.Trash["kanister"], Matrix4x4.TRS(p, Quaternion.Euler(0, t * 720f, 90f), Vector3.one * 0.5f));
                if (Random.value < 0.6f) Burst(p, new Color(0.6f, 0.95f, 1f), 1, 0.2f, 0.25f, 0.5f, 0f, true);
                if (c.T >= 1f) { Burst(c.To, new Color(0.6f, 0.95f, 1f), 16, 2f, 0.12f, 0.6f, 0.3f, true); capsules.RemoveAt(i); }
            }
        }
    }
}
