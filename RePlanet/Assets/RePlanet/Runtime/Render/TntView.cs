using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Darstellung von TNT (nur aus Serverereignissen und repliziertem Zustand): Zielvorschau (gepunktete Flugbahn, Landemarke),
    /// fliegendes und zischendes Sprengbündel (rote Stangen, Klebeband, Zeitzünder mit blinkender Anzeige, Funken an der
    /// Zündschnur, Countdown-Piepser), Explosion (Lichtblitz – gedämpft bei „weniger Lichtblitze“ –, Feuerball, Rauchsäule,
    /// Staubring, umherfliegender Schutt, Kamerawackeln gemäß Einstellung), Stücke des Müllbergs fliegen zu ihren Landestellen,
    /// getroffene Roboter fliegen im Bogen, überschlagen sich, haben Ruß auf dem Augen-Display und Sternchen um den Kopf.
    /// Tiere fliehen vor zischenden Ladungen und Explosionen.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class TntView : MonoBehaviour
    {
        public static TntView I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(TntView))) GameApp.Components.Add(typeof(TntView));
        }

        class Charge
        {
            public string Id, By;
            public List<Vector3> Path = new List<Vector3>();
            public Vector3 Land;
            public float Start, Flight, Fuse, NextBeep, NextSpark;
            public int Kind;
            public Quaternion Spin = Quaternion.identity;
        }

        class Boom
        {
            public Vector3 Pos;
            public float T;
            public Light Light;
            public bool Calm;
            public Material Fire, Ring;
        }

        class Flyer
        {
            public Mesh Mesh; public Material[] Mats; public string HideId;
            public Vector3 From, To; public float T, Dur, Height, Scale; public Quaternion Rot, SpinAxis; public bool Collectible;
        }

        class Knock
        {
            public string Pid;
            public Vector3 From, To;
            public float T, Air, Soot, Stun;
            public Vector3 Axis;
            public GameObject SootGo;
        }

        readonly Dictionary<string, Charge> charges = new Dictionary<string, Charge>();
        readonly List<Boom> booms = new List<Boom>();
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Dictionary<string, Knock> knocks = new Dictionary<string, Knock>();
        readonly List<V3> tmpPath = new List<V3>();
        string planet;
        int landSounds;
        Material redMat, darkMat, metalMat, displayOn, displayOff, dotMat, markGood, markBad, markGrey, starMat, sootMat, chunkMat;

        // ================================================================== Formen
        /// <summary>Sprengbündel: drei rote Stangen, schwarzes Klebeband, Zeitzünder (Untermesh 2 Gehäuse, 3 Anzeige), Zündschnur (1).</summary>
        public static Mesh BundleMesh
        {
            get
            {
                return MeshKit.Get("tnt_bundle", b =>
                {
                    b.Sub = 0;
                    var pts = new[] { new Vector3(-0.075f, 0, 0), new Vector3(0.075f, 0, 0), new Vector3(0, 0, 0.12f) };
                    foreach (var p in pts) b.Cylinder(p, 0.07f, 0.42f, 10, true);
                    b.Sub = 1;
                    b.Cylinder(new Vector3(0, 0.1f, 0.04f), 0.165f, 0.05f, 14, true);
                    b.Cylinder(new Vector3(0, 0.29f, 0.04f), 0.165f, 0.05f, 14, true);
                    // Zündschnur: zwei schräge Rohrstücke nach oben
                    b.Tube(new Vector3(0, 0.42f, 0.06f), new Vector3(0.05f, 0.55f, 0.02f), 0.012f, 5, true);
                    b.Tube(new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0.1f, 0.6f, -0.02f), 0.012f, 5, true);
                    b.Sub = 2;
                    b.Box(new Vector3(0, 0.2f, -0.1f), new Vector3(0.17f, 0.11f, 0.05f));
                    b.Sub = 3;
                    b.Box(new Vector3(0, 0.205f, -0.128f), new Vector3(0.12f, 0.055f, 0.008f));
                });
            }
        }

        /// <summary>Spitze der Zündschnur im Bündel-Raum (Funken).</summary>
        static readonly Vector3 FuseTip = new Vector3(0.1f, 0.6f, -0.02f);

        public static Mesh StarMesh
        {
            get
            {
                return MeshKit.Get("tnt_star", b =>
                {
                    // flacher Stern mit 5 Zacken (Vorder- und Rückseite)
                    for (int side = 0; side < 2; side++)
                    {
                        var n = side == 0 ? Vector3.back : Vector3.forward;
                        for (int i = 0; i < 5; i++)
                        {
                            float a0 = i * Mathf.PI * 0.4f, a1 = a0 + Mathf.PI * 0.2f, a2 = a0 - Mathf.PI * 0.2f;
                            var tip = new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0) * 0.5f;
                            var l = new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0) * 0.2f;
                            var r = new Vector3(Mathf.Sin(a2), Mathf.Cos(a2), 0) * 0.2f;
                            b.TriFace(Vector3.zero, l, tip, n);
                            b.TriFace(Vector3.zero, tip, r, n);
                        }
                    }
                });
            }
        }

        public static Mesh RingMesh { get { return MeshKit.Get("tnt_ring", b => b.Torus(Vector3.zero, 1f, 0.12f, 28, 6)); } }
        public static Mesh SootMesh { get { return MeshKit.Get("tnt_soot", b => b.Blob(Vector3.zero, 0.5f, 0.35f, 12, 4, 7, 0.35f)); } }

        /// <summary>Für die Prüfumgebung (Wicklung aller Formen).</summary>
        public static void PrewarmMeshes() { var a = BundleMesh; a = StarMesh; a = RingMesh; a = SootMesh; }

        // ================================================================== Lebenszyklus
        void Awake() { I = this; }

        void Start()
        {
            if (GameApp.I == null) return;
            GameApp.I.OnFx += OnFx;
            GameApp.I.OnPlanetChanged += p => ClearAll();
        }

        void OnDestroy() { if (GameApp.I != null) GameApp.I.OnFx -= OnFx; }

        void EnsureMats()
        {
            if (redMat != null) return;
            redMat = Mats.Get(Mats.Opaque, new Color(0.78f, 0.13f, 0.1f));
            darkMat = Mats.Get(Mats.Opaque, new Color(0.08f, 0.08f, 0.09f));
            metalMat = Mats.Get(Mats.Metal, new Color(0.55f, 0.57f, 0.6f));
            displayOn = Mats.Get(Mats.Opaque, new Color(1f, 0.15f, 0.1f), new Color(1f, 0.12f, 0.05f) * 2.5f);
            displayOff = Mats.Get(Mats.Opaque, new Color(0.18f, 0.05f, 0.04f));
            dotMat = Mats.Get(Mats.ParticleAdd, new Color(1f, 0.85f, 0.45f));
            markGood = Mats.Get(Mats.ParticleAdd, new Color(0.45f, 1f, 0.55f));
            markBad = Mats.Get(Mats.ParticleAdd, new Color(1f, 0.55f, 0.25f));
            markGrey = Mats.Get(Mats.ParticleAdd, new Color(0.45f, 0.65f, 1f));
            starMat = Mats.Get(Mats.Opaque, new Color(1f, 0.85f, 0.2f), new Color(1f, 0.8f, 0.2f) * 1.5f);
            sootMat = Mats.Get(Mats.Fade, new Color(0.04f, 0.035f, 0.03f, 0.88f));
            chunkMat = Mats.Get(Mats.Opaque, new Color(0.28f, 0.24f, 0.2f));
        }

        void ClearAll()
        {
            foreach (var c in charges.Values) AudioManager.Loop("tnt_" + c.Id, "tnt_fuse", false);
            charges.Clear();
            foreach (var b in booms) if (b.Light != null) Destroy(b.Light.gameObject);
            booms.Clear();
            flyers.Clear();
            foreach (var k in knocks.Values) if (k.SootGo != null) Destroy(k.SootGo);
            knocks.Clear();
            TrashRenderer.HiddenUntil.Clear();
        }

        static Vector3 V(List<object> a, Vector3 fallback)
        {
            if (a == null || a.Count < 3) return fallback;
            return new Vector3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0));
        }

        static Vector3 U(V3 v) { return new Vector3(v.x, v.y, v.z); }

        bool Calm { get { return GameApp.I != null && GameApp.I.Settings != null && GameApp.I.Settings.ReduceFlashing; } }

        string MyPid { get { return GameApp.I != null && GameApp.I.Client != null ? GameApp.I.Client.Pid : null; } }

        string NameOf(string pid)
        {
            var w = GameApp.I != null ? GameApp.I.W : null;
            PlayerData p;
            return w != null && pid != null && w.Players.TryGetValue(pid, out p) ? p.Name : "MIKO";
        }

        // ================================================================== Ereignisse
        void OnFx(JObj f)
        {
            switch (f.Str("k"))
            {
                case "tntthrow":
                    {
                        var w = GameApp.I.W;
                        if (w == null) break;
                        AddCharge(w, f.Str("id"), f.Str("pid"), V3.FromArr(f.Floats("from")), V3.FromArr(f.Floats("v")), V3.FromArr(f.Floats("pos")),
                            f.Float("fl"), f.Float("fu", GameData.TntFuse), 0f, f.Int("l"));
                        AudioManager.Play("tnt_throw", V(f.Arr("from"), Vector3.zero), 0.7f, Random.Range(0.95f, 1.08f));
                        var r = ActorsView.I != null ? ActorsView.I.RobotOf(f.Str("pid")) : null;
                        if (r != null && f.Str("pid") != MyPid) r.Grab();
                        break;
                    }
                case "tntboom": OnBoom(f); break;
                case "tntbuy":
                    if (f.Str("pid") == MyPid) Hud.Show(Loc.F("{0}× TNT gekauft (−{1} Credits). Werfen: [{2}] halten, zielen, loslassen.", f.Int("n"), f.Long("c"), InputMap.Label(GameAction.ThrowTnt)), ToastKind.Success, 5f);
                    break;
            }
        }

        void AddCharge(WorldState w, string id, string by, V3 from, V3 vel, V3 land, float flight, float fuse, float age, int kind)
        {
            if (id == null || charges.ContainsKey(id)) return;
            var c = new Charge { Id = id, By = by, Land = U(land), Flight = Mathf.Max(0.05f, flight), Fuse = fuse, Start = Time.time - age, Kind = kind };
            Rules.TntSimulate(w.Cur, from, vel, tmpPath);
            foreach (var p in tmpPath) c.Path.Add(U(p));
            if (c.Path.Count == 0) c.Path.Add(U(from));
            // Abweichung zur Serverlandung (andere Rechengenauigkeit) auf die letzten Punkte verteilen
            var err = c.Land - c.Path[c.Path.Count - 1];
            if (err.sqrMagnitude > 0.0001f)
                for (int i = 0; i < c.Path.Count; i++) c.Path[i] += err * (i / (float)Mathf.Max(1, c.Path.Count - 1));
            c.NextBeep = Mathf.Floor(c.Fuse - age);
            charges[id] = c;
        }

        void RemoveCharge(string id)
        {
            Charge c;
            if (id == null || !charges.TryGetValue(id, out c)) return;
            AudioManager.Loop("tnt_" + id, "tnt_fuse", false);
            charges.Remove(id);
        }

        void OnBoom(JObj f)
        {
            string id = f.Str("id");
            RemoveCharge(id);
            var pos = V(f.Arr("pos"), Vector3.zero);
            bool mine = f.Str("pid") == MyPid;
            string fizzle = f.Str("fizzle");
            if (fizzle != null)
            {
                if (fizzle == "water")
                {
                    Burst(pos, new Color(0.75f, 0.9f, 1f, 0.8f), 30, 3f, 0.25f, 1.2f, 0.8f, false);
                    AudioManager.Play("splash", pos, 0.6f, 1.2f);
                    if (mine) Hud.Show(Loc.T("Die Ladung ist zischend im Wasser versunken."), ToastKind.Info, 3.5f);
                }
                else
                {
                    Burst(pos + Vector3.up * 0.3f, new Color(0.6f, 0.6f, 0.6f, 0.6f), 14, 0.8f, 0.5f, 1.5f, -0.1f, false);
                    AudioManager.Play("beep_sad", pos, 0.5f);
                    if (mine) Hud.Show(Loc.T("Im Stützpunkt wird nicht gesprengt – die Zündschnur ist erloschen, die Ladung ist zurück im Behälter."), ToastKind.Info, 4.5f);
                }
                return;
            }
            Explosion(pos);
            // Müllberg
            var pieces = f.Arr("pieces");
            if (pieces != null)
            {
                var mpos = V(f.Arr("mpos"), pos);
                float lift = 2.5f;
                foreach (var o in pieces)
                {
                    var a = o as List<object>;
                    if (a == null || a.Count < 5) continue;
                    TrashType t;
                    string pid = a[0] as string, type = a[1] as string;
                    if (type == null || !GameData.Trash.TryGetValue(type, out t)) continue;
                    var to = new Vector3((float)Json.ToDouble(a[2], 0), (float)Json.ToDouble(a[3], 0), (float)Json.ToDouble(a[4], 0));
                    float dur = Random.Range(0.75f, 1.35f);
                    var fl = new Flyer
                    {
                        Mesh = TrashRenderer.MeshFor(t), Mats = TrashRenderer.MaterialsFor(t), HideId = pid, From = mpos + Vector3.up * lift + Random.insideUnitSphere * 1.2f,
                        To = to, Dur = dur, Height = Random.Range(3f, 7f), Scale = t.Size, Rot = Random.rotation, SpinAxis = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), 0), Collectible = true
                    };
                    flyers.Add(fl);
                    if (pid != null) TrashRenderer.HiddenUntil[pid] = Time.time + dur;
                }
                TrashRenderer.RefreshSoon();
                // Staubwolke über dem Berg
                for (int k = 0; k < 5; k++) Burst(mpos + Random.insideUnitSphere * 3f + Vector3.up * 2f, new Color(0.55f, 0.48f, 0.38f, 0.55f), 25, 2.5f, 1.6f, 4f, -0.04f, false);
                int stage = f.Int("stage"), stages = f.Int("stages");
                Hud.Show(Loc.F(stage >= stages ? "Müllberg ganz zerlegt! {0} Stücke liegen bereit, {1} sind als Staub verweht." : "Müllberg gesprengt ({2}/{3}) – {0} Stücke liegen bereit, {1} sind als Staub verweht.",
                    pieces.Count, f.Int("dust"), stage, stages), ToastKind.Success, 5f);
            }
            else if (mine && f.Str("why") != null) Hud.Show(Loc.T(f.Str("why")), ToastKind.Info, 4f);
            // Getroffene Roboter
            var hits = f.Arr("hits");
            if (hits != null)
                foreach (var o in hits)
                {
                    var a = o as List<object>;
                    if (a == null || a.Count < 8) continue;
                    string pid = a[0] as string;
                    var from = new Vector3((float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0), (float)Json.ToDouble(a[3], 0));
                    var to = new Vector3((float)Json.ToDouble(a[4], 0), (float)Json.ToDouble(a[5], 0), (float)Json.ToDouble(a[6], 0));
                    bool self = Json.ToDouble(a[7], 0) > 0;
                    StartKnock(pid, from, to);
                    if (pid == MyPid)
                    {
                        if (PlayerController.I != null) PlayerController.I.Knockback(from, to, GameData.TntKnockAir, GameData.TntStun);
                        Hud.Show(self ? Loc.T("Autsch – zu nah an der eigenen Ladung!") : Loc.F("{0} hat dich mit TNT erwischt!", NameOf(f.Str("pid"))), ToastKind.Warning, 3.5f);
                    }
                    else if (mine && !self) { Hud.Show(Loc.F("Treffer! {0} fliegt eine Runde.", NameOf(pid)), ToastKind.Success, 3.5f); AudioManager.Ui("beep_happy"); }
                }
        }

        static void Burst(Vector3 p, Color c, int n, float speed, float size, float life, float gravity, bool glow)
        {
            if (FxView.I != null) FxView.I.Burst(p, c, n, speed, size, life, gravity, glow);
        }

        void Explosion(Vector3 pos)
        {
            EnsureMats();
            bool calm = Calm;
            var b = new Boom { Pos = pos, Calm = calm, Fire = Mats.Unique(Mats.ParticleAdd, new Color(1f, 0.6f, 0.2f)), Ring = Mats.Unique(Mats.Fade, new Color(0.62f, 0.54f, 0.42f, 0.7f)) };
            var lg = new GameObject("TntBlitz");
            lg.transform.SetParent(transform, false);
            lg.transform.position = pos + Vector3.up * 1.5f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.3f);
            l.range = calm ? 14f : 26f;
            l.intensity = calm ? 1.6f : 7f;
            l.shadows = LightShadows.None;
            b.Light = l;
            booms.Add(b);
            // Funken, Feuer, Rauchsäule, Staubring, Schutt
            Burst(pos + Vector3.up * 0.8f, new Color(1f, 0.75f, 0.35f), 70, 9f, 0.18f, 0.8f, 0.6f, true);
            Burst(pos + Vector3.up * 1.2f, new Color(1f, 0.45f, 0.15f, 0.9f), 40, 4f, 1.2f, 0.7f, -0.2f, true);
            for (int k = 0; k < 6; k++)
                Burst(pos + Vector3.up * (1.5f + k * 1.6f) + Random.insideUnitSphere * 0.8f, new Color(0.22f, 0.21f, 0.2f, 0.7f), 18, 0.9f, 2.2f + k * 0.3f, 5f, -0.12f, false);
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                var p = pos + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * 2.5f;
                Burst(p, new Color(0.6f, 0.52f, 0.4f, 0.6f), 6, 3.5f, 1.1f, 2.5f, 0.15f, false);
            }
            for (int k = 0; k < 12; k++)
            {
                var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.4f;
                flyers.Add(new Flyer
                {
                    Mesh = MeshKit.Cube, Mats = new[] { chunkMat }, From = pos + Vector3.up * 0.5f, To = pos + new Vector3(dir.x, 0, dir.z) * Random.Range(4f, 10f),
                    Dur = Random.Range(0.6f, 1.1f), Height = Random.Range(2f, 6f), Scale = Random.Range(0.12f, 0.3f), Rot = Random.rotation, SpinAxis = Random.rotation
                });
            }
            AudioManager.Play("tnt_boom", pos, 1f, Random.Range(0.94f, 1.04f));
            Wildlife.Scare(pos, 4f, 8f);
            var cam = Camera.main;
            if (CameraRig.I != null && cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, pos);
                if (d < 45f) CameraRig.I.Shake(0.75f * (1f - d / 45f) + 0.05f);
            }
        }

        void StartKnock(string pid, Vector3 from, Vector3 to)
        {
            Knock k;
            if (knocks.TryGetValue(pid, out k) && k.SootGo != null) Destroy(k.SootGo);
            k = new Knock { Pid = pid, From = from, To = to, Air = GameData.TntKnockAir, Stun = GameData.TntStun, Soot = GameData.TntSootTime };
            var flat = to - from; flat.y = 0;
            k.Axis = flat.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            var r = ActorsView.I != null ? ActorsView.I.RobotOf(pid) : null;
            if (r != null)
            {
                var head = FindChild(r.transform, "head");
                EnsureMats();
                var go = new GameObject("TntRuss");
                go.transform.SetParent(head != null ? head : r.transform, false);
                go.transform.localPosition = head != null ? new Vector3(0, -0.01f, 0.2f) : new Vector3(0, 1.1f, 0.35f);
                go.transform.localScale = new Vector3(0.8f, 0.34f, 0.12f);
                go.AddComponent<MeshFilter>().sharedMesh = SootMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sootMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                k.SootGo = go;
            }
            knocks[pid] = k;
            AudioManager.Play("dizzy", to, 0.5f);
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t == null) return null;
            if (t.gameObject.name == name) return t;
            foreach (Transform c in t) { var f = FindChild(c, name); if (f != null) return f; }
            return null;
        }

        // ================================================================== Bild für Bild
        void Update()
        {
            var app = GameApp.I;
            var w = app != null ? app.W : null;
            if (w == null || !app.InGame) { if (charges.Count > 0 || flyers.Count > 0) ClearAll(); return; }
            if (planet != w.CurrentPlanet) { ClearAll(); planet = w.CurrentPlanet; }
            EnsureMats();
            float dt = Time.deltaTime;
            // spät beigetreten: scharfe Ladungen aus dem Zustand übernehmen
            foreach (var c in w.Cur.Tnt) if (!charges.ContainsKey(c.Id)) AddCharge(w, c.Id, c.By, c.From, c.Vel, c.Pos, c.Flight, c.Fuse, c.Age, c.Land);
            DrawCharges(dt);
            DrawBooms(dt);
            DrawFlyers(dt);
            DrawAim();
        }

        void DrawCharges(float dt)
        {
            var stale = (List<string>)null;
            foreach (var c in charges.Values)
            {
                float t = Time.time - c.Start;
                if (t > c.Fuse + 3f) { (stale ?? (stale = new List<string>())).Add(c.Id); continue; } // Explosion verpasst
                Vector3 pos; Quaternion rot;
                if (t < c.Flight)
                {
                    float fi = t / GameData.TntStep;
                    int i0 = Mathf.Clamp((int)fi, 0, c.Path.Count - 1), i1 = Mathf.Min(i0 + 1, c.Path.Count - 1);
                    pos = Vector3.Lerp(c.Path[i0], c.Path[i1], fi - (int)fi);
                    c.Spin = Quaternion.AngleAxis(540f * dt, new Vector3(0.7f, 0.2f, 0.6f)) * c.Spin;
                    rot = c.Spin;
                }
                else
                {
                    pos = c.Land;
                    rot = Quaternion.Euler(0, (c.Id.GetHashCode() % 360), 0);
                    if (c.Kind != 1 && c.Kind != 3)
                    {
                        AudioManager.Loop("tnt_" + c.Id, "tnt_fuse", true, pos, 0.55f);
                        if (Time.time >= c.NextSpark)
                        {
                            c.NextSpark = Time.time + 0.07f;
                            Burst(pos + rot * FuseTip, new Color(1f, 0.8f, 0.35f), 3, 1.6f, 0.06f, 0.35f, 0.5f, true);
                        }
                        Wildlife.Scare(pos, 1.6f, 0.5f);
                    }
                    float left = c.Fuse - t;
                    if (left <= c.NextBeep && left > 0f)
                    {
                        bool last = left <= 1.05f;
                        AudioManager.Play(last ? "tnt_beep_hi" : "tnt_beep", pos, 0.7f);
                        c.NextBeep = last ? left - 0.25f : Mathf.Floor(left - 0.001f);
                    }
                }
                var m = Matrix4x4.TRS(pos, rot, Vector3.one * 1.15f);
                var mesh = BundleMesh;
                bool blink = c.Fuse - t < 1.05f ? Mathf.Repeat(Time.time * 8f, 1f) < 0.5f : Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetIndexCount(s) == 0) continue;
                    var mat = s == 0 ? redMat : s == 1 ? darkMat : s == 2 ? metalMat : (blink ? displayOn : displayOff);
                    Graphics.DrawMesh(mesh, m, mat, 0, null, s, null, ShadowCastingMode.On, true);
                }
            }
            if (stale != null) foreach (var id in stale) RemoveCharge(id);
        }

        void DrawBooms(float dt)
        {
            for (int i = 0; i < booms.Count; i++)
            {
                var b = booms[i];
                b.T += dt;
                float t = b.T;
                if (b.Light != null)
                {
                    float fade = b.Calm ? Mathf.Clamp01(1f - t / 0.9f) : Mathf.Clamp01(1f - t / 0.45f);
                    b.Light.intensity = (b.Calm ? 1.6f : 7f) * fade * fade;
                    if (fade <= 0f) { Destroy(b.Light.gameObject); b.Light = null; }
                }
                // Feuerball: wächst schnell, verglimmt
                if (t < 0.6f)
                {
                    float k = t / 0.6f;
                    float s = Mathf.Lerp(1.2f, 6.5f, Mathf.Sqrt(k));
                    var c = Color.Lerp(new Color(1f, 0.75f, 0.35f), new Color(0.6f, 0.15f, 0.05f), k) * (1f - k) * (b.Calm ? 0.5f : 1f);
                    b.Fire.color = c;
                    Graphics.DrawMesh(MeshKit.Sphere, Matrix4x4.TRS(b.Pos + Vector3.up * (1f + k * 1.5f), Quaternion.identity, Vector3.one * s), b.Fire, 0, null, 0, null, ShadowCastingMode.Off, false);
                }
                // Staubring am Boden
                if (t < 1.6f)
                {
                    float k = t / 1.6f;
                    float r = Mathf.Lerp(1f, 11f, 1f - (1f - k) * (1f - k));
                    var c = b.Ring.color; c.a = 0.7f * (1f - k);
                    b.Ring.color = c;
                    Graphics.DrawMesh(RingMesh, Matrix4x4.TRS(b.Pos + Vector3.up * 0.35f, Quaternion.identity, new Vector3(r, 2.2f * (1f - k) + 0.3f, r)), b.Ring, 0, null, 0, null, ShadowCastingMode.Off, false);
                }
                if (t > 2f && b.Light == null) { booms.RemoveAt(i); i--; }
            }
        }

        void DrawFlyers(float dt)
        {
            bool refresh = false;
            for (int i = 0; i < flyers.Count; i++)
            {
                var f = flyers[i];
                f.T += dt;
                float k = Mathf.Clamp01(f.T / f.Dur);
                var p = Vector3.Lerp(f.From, f.To, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * f.Height;
                var rot = Quaternion.Slerp(f.Rot, Quaternion.identity, k * k) * Quaternion.AngleAxis((1f - k) * 900f, f.SpinAxis * Vector3.up);
                if (k >= 1f)
                {
                    if (f.Collectible)
                    {
                        if (f.HideId != null) TrashRenderer.HiddenUntil.Remove(f.HideId);
                        refresh = true;
                        Burst(f.To + Vector3.up * 0.2f, new Color(0.55f, 0.5f, 0.42f, 0.6f), 6, 1.4f, 0.5f, 0.9f, 0.2f, false);
                        if (landSounds < 4) { landSounds++; AudioManager.Play("debris_land", f.To, 0.45f, Random.Range(0.9f, 1.15f)); }
                    }
                    flyers.RemoveAt(i); i--;
                    continue;
                }
                var m = Matrix4x4.TRS(p, rot, Vector3.one * f.Scale);
                for (int s = 0; s < f.Mesh.subMeshCount; s++)
                {
                    if (f.Mesh.GetIndexCount(s) == 0) continue;
                    Graphics.DrawMesh(f.Mesh, m, f.Mats[Mathf.Min(s, f.Mats.Length - 1)], 0, null, s, null, ShadowCastingMode.Off, true);
                }
            }
            if (flyers.Count == 0) landSounds = 0;
            if (refresh) TrashRenderer.RefreshSoon();
        }

        /// <summary>Zielvorschau des eigenen Wurfs: gepunktete Bahn und Landemarke (grün = sprengt einen Müllberg, blau = erlischt).</summary>
        void DrawAim()
        {
            var pc = PlayerController.I;
            if (pc == null || !pc.TntAiming || pc.TntPreview.Count < 2) return;
            var path = pc.TntPreview;
            var dot = MeshKit.Sphere;
            for (int i = 2; i < path.Count; i += 3)
            {
                float s = 0.13f - 0.05f * (i / (float)path.Count);
                Graphics.DrawMesh(dot, Matrix4x4.TRS(U(path[i]), Quaternion.identity, Vector3.one * s), dotMat, 0, null, 0, null, ShadowCastingMode.Off, false);
            }
            var fl = pc.TntPreviewFlight;
            var w = GameApp.I.W;
            Material mm = markBad;
            if (fl.Land == 1 || fl.Land == 3) mm = markGrey;
            else if (w != null)
            {
                int mound = fl.Mound >= 0 ? fl.Mound : Rules.HeapAt(w.Cur, fl.Pos, GameData.TntHeapReach);
                if (mound >= 0 && Rules.MoundBlastCheck(w, w.Cur, mound) == null) mm = markGood;
            }
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 8f);
            Graphics.DrawMesh(RingMesh, Matrix4x4.TRS(U(fl.Pos) + Vector3.up * 0.15f, Quaternion.identity, new Vector3(1.1f * pulse, 0.6f, 1.1f * pulse)), mm, 0, null, 0, null, ShadowCastingMode.Off, false);
            Graphics.DrawMesh(RingMesh, Matrix4x4.TRS(U(fl.Pos) + Vector3.up * 0.15f, Quaternion.identity, new Vector3(GameData.TntKnockRadius, 0.25f, GameData.TntKnockRadius)), mm, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        // Getroffene Roboter: Bogen (Mitspieler), Überschlag, Sternchen, Ruß – nach ActorsView (DefaultExecutionOrder 1000)
        void LateUpdate()
        {
            if (knocks.Count == 0) return;
            float dt = Time.deltaTime;
            EnsureMats();
            List<string> done = null;
            foreach (var k in knocks.Values)
            {
                k.T += dt;
                var r = ActorsView.I != null ? ActorsView.I.RobotOf(k.Pid) : null;
                if (r != null && r.gameObject.activeInHierarchy)
                {
                    var tr = r.transform;
                    float a = k.T / k.Air;
                    if (a < 1f)
                    {
                        if (k.Pid != MyPid) tr.position = Vector3.Lerp(k.From, k.To, a) + Vector3.up * Mathf.Sin(a * Mathf.PI) * 2.4f;
                        var center = tr.position + Vector3.up * 0.7f;
                        var spin = Quaternion.AngleAxis(a * 720f, k.Axis);
                        tr.position = center + spin * (tr.position - center);
                        tr.rotation = spin * tr.rotation;
                    }
                    else if (k.T < k.Air + k.Stun)
                    {
                        // benommen: leichtes Taumeln, Sternchen kreisen
                        float wob = Mathf.Sin(k.T * 9f) * 8f;
                        tr.rotation = tr.rotation * Quaternion.Euler(wob * 0.5f, 0, wob);
                        for (int s = 0; s < 3; s++)
                        {
                            float ang = k.T * 4f + s * 2.094f;
                            var p = tr.position + Vector3.up * 1.75f + new Vector3(Mathf.Cos(ang), 0.08f * Mathf.Sin(ang * 2f), Mathf.Sin(ang)) * 0.45f;
                            var cam = Camera.main;
                            var q = cam != null ? Quaternion.LookRotation(p - cam.transform.position) : Quaternion.identity;
                            Graphics.DrawMesh(StarMesh, Matrix4x4.TRS(p, q * Quaternion.Euler(0, 0, k.T * 200f), Vector3.one * 0.22f), starMat, 0, null, 0, null, ShadowCastingMode.Off, false);
                        }
                    }
                }
                if (k.T > k.Soot)
                {
                    if (k.SootGo != null) Destroy(k.SootGo);
                    (done ?? (done = new List<string>())).Add(k.Pid);
                }
                else if (k.SootGo != null && k.T > k.Soot - 1f)
                    k.SootGo.transform.localScale *= Mathf.Max(0f, 1f - dt * 2f); // Ruß bröckelt ab
            }
            if (done != null) foreach (var p in done) knocks.Remove(p);
        }

        /// <summary>Prüfumgebung: Anzahl sichtbarer Ladungen, Explosionen, fliegender Teile und getroffener Roboter.</summary>
        public int Charges { get { return charges.Count; } }
        public int Booms { get { return booms.Count; } }
        public int Flyers { get { return flyers.Count; } }
        public int Knocks { get { return knocks.Count; } }
    }
}
