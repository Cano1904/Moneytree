using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// MIKOs Augen-Display und Gesten: dauerhafte Stimmungen (fröhlich, neugierig, müde bei leerem Akku, ängstlich im
    /// Sturm, schläfrig bei Nacht, frierend auf NIVALIS) verändern Augenform, Lider und Augenfarbe; kurze Gesten
    /// (Winken, Freudensprung, Kopfneigen im Leerlauf, Zittern in der Kälte, Gähnen) bewegen Arm, Kopf und Körper.
    /// Die Stimmung setzt <see cref="MikoGestures"/> aus dem replizierten Zustand; ohne Stimmung (Intro, Menü) bleibt
    /// alles wie bisher.
    /// </summary>
    public partial class RobotModel
    {
        public const int MoodNeutral = 0, MoodHappy = 1, MoodCurious = 2, MoodTired = 3, MoodScared = 4, MoodSleepy = 5, MoodCold = 6;
        public static readonly string[] MoodNames = { "neutral", "fröhlich", "neugierig", "müde", "ängstlich", "schläfrig", "frierend" };

        /// <summary>Leerlauf-Gesten (Kopfneigen, Umschauen, Zittern, Gähnen) selbständig abspielen – nur für Spielerfiguren.</summary>
        public bool AutoGestures;
        /// <summary>Aktuelle Stimmung (siehe Mood*-Konstanten).</summary>
        public int Mood { get; private set; }
        /// <summary>Laufende Geste ("wave", "hop", "cheer", "tilt", "shiver", "yawn") oder null.</summary>
        public string CurrentGesture { get { return gestT > 0f ? gestKind : null; } }
        /// <summary>Zähler abgespielter Gesten (Prüfumgebung).</summary>
        public int GestureCount { get; private set; }

        Vector3? lookAt;
        string gestKind;
        float gestT, gestDur, idleT, autoT = 4f, moodK;
        Color moodCol = new Color(0.35f, 0.95f, 1f);
        Vector3 gestTarget;

        /// <summary>Stimmung und optionalen Blickpunkt (Tier, Mitspieler) für die nächsten Bilder setzen.</summary>
        public void SetMood(int mood, Vector3? look)
        {
            Mood = Mathf.Clamp(mood, 0, 6);
            lookAt = look;
        }

        /// <summary>Kurze Geste abspielen (eine laufende gleichartige wird nicht neu gestartet).</summary>
        public void Gesture(string kind, Vector3? toward = null)
        {
            if (string.IsNullOrEmpty(kind)) return;
            if (gestT > 0f && gestKind == kind) return;
            // Winken und Freude unterbrechen Leerlauf-Gesten, nicht umgekehrt
            if (gestT > 0f && (kind == "tilt" || kind == "shiver" || kind == "yawn") && (gestKind == "wave" || gestKind == "hop" || gestKind == "cheer")) return;
            gestKind = kind;
            gestDur = kind == "wave" ? 2.4f : kind == "hop" ? 0.8f : kind == "cheer" ? 1.5f : kind == "tilt" ? 1.8f : kind == "shiver" ? 1.3f : kind == "yawn" ? 1.8f : 1f;
            gestT = gestDur;
            if (toward.HasValue) gestTarget = toward.Value; else gestTarget = transform.position + transform.forward;
            GestureCount++;
            if (kind == "hop" || kind == "cheer") Emote("happy");
        }

        void MoodLids(ref float lower, ref float raise, ref float tilt, ref float asym)
        {
            switch (Mood)
            {
                case MoodHappy: raise = Mathf.Max(raise, 0.35f); break;
                case MoodCurious: asym = 0.3f; break;
                case MoodTired: lower = Mathf.Max(lower, 0.48f + 0.06f * Mathf.Sin(Time.time * 0.9f)); tilt = 8f; break;
                case MoodScared: lower = 0f; raise = 0f; tilt = -10f; break;
                case MoodSleepy: lower = Mathf.Max(lower, 0.58f + 0.12f * Mathf.Sin(Time.time * 0.5f)); break;
                case MoodCold: lower = Mathf.Max(lower, 0.22f); raise = 0.15f; break;
            }
            if (gestT > 0f)
            {
                if (gestKind == "hop" || gestKind == "cheer" || gestKind == "wave") { raise = 0.5f; lower = 0f; tilt = 0f; }
                else if (gestKind == "yawn") { lower = 0.85f; raise = 0.3f; }
            }
        }

        Color MoodEyeColor(float dt)
        {
            Color target;
            switch (Mood)
            {
                case MoodHappy: target = new Color(0.45f, 1f, 0.75f); break;
                case MoodCurious: target = new Color(1f, 0.9f, 0.45f); break;
                case MoodTired: target = new Color(1f, 0.6f, 0.2f); break;
                case MoodScared: target = new Color(0.75f, 0.88f, 1f); break;
                case MoodSleepy: target = new Color(0.6f, 0.62f, 1f); break;
                case MoodCold: target = new Color(0.62f, 0.92f, 1f); break;
                default: target = eyeBase; break;
            }
            moodCol = Color.Lerp(moodCol, target, Mathf.Clamp01(dt * 3f));
            return moodCol;
        }

        float MoodGlow()
        {
            // müde: Akku-Puls, schläfrig: gedimmt
            if (Mood == MoodTired) return 0.55f + 0.25f * Mathf.Sin(Time.time * 2.4f);
            if (Mood == MoodSleepy) return 0.7f;
            if (Mood == MoodScared) return 1.15f;
            return 1f;
        }

        /// <summary>Nach der Grundanimation: Augenform, Blickrichtung, Gesten auf Kopf, Arm und Körper legen.</summary>
        void ApplyFace(float dt, float speed, bool acting, bool swimming, bool sleeping, bool off)
        {
            if (off || sleeping) { gestT = 0f; idleT = 0f; return; }
            // --- Augenform je Stimmung (Blinzeln bleibt erhalten: skaliert wird relativ)
            float wx = 1f, hy = 1f;
            switch (Mood)
            {
                case MoodScared: wx = 0.72f; hy = 1.25f + 0.08f * Mathf.Sin(Time.time * 31f); break;
                case MoodTired: wx = 1.05f; hy = 0.8f; break;
                case MoodSleepy: wx = 0.95f; hy = 0.85f; break;
                case MoodCurious: wx = 1f; hy = 1.1f; break;
                case MoodHappy: wx = 1.08f; hy = 0.95f; break;
            }
            moodK = Mathf.MoveTowards(moodK, 1f, dt * 2f);
            var sl = eyeL.localScale; var sr = eyeR.localScale;
            eyeL.localScale = new Vector3(sl.x * wx, sl.y * hy * (Mood == MoodCurious ? 0.85f : 1f), sl.z);
            eyeR.localScale = new Vector3(sr.x * wx, sr.y * hy * (Mood == MoodCurious ? 1.15f : 1f), sr.z);

            // --- Leerlauf-Gesten
            idleT = speed < 0.15f && !acting ? idleT + dt : 0f;
            if (AutoGestures && gestT <= 0f)
            {
                autoT -= dt;
                if (autoT <= 0f)
                {
                    autoT = 4f + Random.value * 5f;
                    if (Mood == MoodCold && !swimming) Gesture("shiver");
                    else if (Mood == MoodSleepy && Random.value < 0.5f) Gesture("yawn");
                    else if (idleT > 5f) Gesture("tilt");
                }
            }

            // --- Blick zum Ziel (Tier, Mitspieler): Kopf dreht sich, höchstens ±35°
            float lookYaw = 0f, lookPitch = 0f;
            if (lookAt.HasValue && body != null)
            {
                var d = body.InverseTransformPoint(lookAt.Value) - head.localPosition;
                if (d.sqrMagnitude > 0.04f)
                {
                    lookYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -35f, 35f);
                    lookPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -20f, 15f);
                }
            }
            float scaredGlance = Mood == MoodScared ? Mathf.Sin(Time.time * 2.3f) * 22f * Mathf.Clamp01(Mathf.Sin(Time.time * 0.9f) * 3f) : 0f;
            float tiredDroop = Mood == MoodTired ? 9f : Mood == MoodSleepy ? 6f : 0f;
            curLookYaw = Mathf.Lerp(curLookYaw, lookYaw + scaredGlance, Mathf.Clamp01(dt * 4f));
            curLookPitch = Mathf.Lerp(curLookPitch, lookPitch + tiredDroop, Mathf.Clamp01(dt * 3f));
            float hx = curLookPitch, hyw = curLookYaw, hz = 0f;
            var rootOff = Vector3.zero;
            var bodyRot = Quaternion.identity;

            // --- laufende Geste
            if (gestT > 0f)
            {
                gestT -= dt;
                float u = 1f - Mathf.Clamp01(gestT / gestDur); // 0 → 1
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                switch (gestKind)
                {
                    case "wave":
                        if (!acting)
                        {
                            float up = Mathf.Clamp01(Mathf.Min(u * 5f, (1f - u) * 5f));
                            arm1.localRotation = Quaternion.Slerp(arm1.localRotation, Quaternion.Euler(-112f, 38f, 0f), up);
                            arm2.localRotation = Quaternion.Slerp(arm2.localRotation, Quaternion.Euler(20f, Mathf.Sin(u * 28f) * 38f, 0f), up);
                        }
                        hz += 10f * env;
                        {
                            var d = body.InverseTransformPoint(gestTarget) - head.localPosition;
                            if (d.sqrMagnitude > 0.04f) hyw = Mathf.Lerp(hyw, Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -40f, 40f), env);
                        }
                        break;
                    case "hop":
                    case "cheer":
                        {
                            int hops = gestKind == "cheer" ? 2 : 1;
                            float ph = Mathf.Repeat(u * hops, 1f);
                            rootOff.y = Mathf.Sin(ph * Mathf.PI) * (swimming ? 0.12f : 0.32f);
                            bodyRot = Quaternion.Euler(-Mathf.Sin(ph * Mathf.PI * 2f) * 6f, 0, 0);
                            hx -= 12f * env;
                            if (gestKind == "cheer" && !acting)
                            {
                                arm1.localRotation = Quaternion.Slerp(arm1.localRotation, Quaternion.Euler(-120f, 20f, 0f), env);
                                arm2.localRotation = Quaternion.Slerp(arm2.localRotation, Quaternion.Euler(10f, 0f, 0f), env);
                            }
                            break;
                        }
                    case "tilt":
                        hz += 16f * env * (autoT > 6f ? 1f : -1f);
                        hyw += Mathf.Sin(u * Mathf.PI * 2f) * 20f;
                        break;
                    case "shiver":
                        bodyRot = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 55f) * 2.8f * env);
                        hz += Mathf.Sin(Time.time * 47f) * 2.5f * env;
                        break;
                    case "yawn":
                        hx -= 16f * env;
                        break;
                }
                if (gestT <= 0f) gestT = 0f;
            }
            head.localRotation = head.localRotation * Quaternion.Euler(hx, hyw, hz);
            if (rootOff.y > 0f) root.localPosition += rootOff;
            if (bodyRot != Quaternion.identity) body.localRotation = body.localRotation * bodyRot;
        }

        float curLookYaw, curLookPitch;
    }

    /// <summary>
    /// Steuert MIKOs Mimik und Gesten aller Spielerfiguren aus dem replizierten Zustand – damit sehen Mitspieler
    /// dieselben Regungen: Akku (müde), Sturm und Schutz (ängstlich), Tageszeit (schläfrig), NIVALIS ohne Unterschlupf
    /// (frierend), Tiere in der Nähe (neugierig), gut wiederhergestellte Umgebung (fröhlich). Ereignisse vom Server
    /// lösen Gesten aus: Abgabe, Verkauf, Lieferung, Lichtpunkt/Bereich/Projekt geschafft (Freudensprung), Roboterlaut
    /// eines Spielers (auch beim Mitspieler sichtbar), Mitspieler kommt näher (beide winken).
    /// </summary>
    public class MikoGestures : MonoBehaviour
    {
        public static MikoGestures I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(MikoGestures))) GameApp.Components.Add(typeof(MikoGestures));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            if (GameApp.I != null && GameApp.I.GetComponent<MikoGestures>() == null) GameApp.I.gameObject.AddComponent<MikoGestures>();
        }

        readonly Dictionary<string, float> pairDist = new Dictionary<string, float>();
        readonly Dictionary<string, float> pairCooldown = new Dictionary<string, float>();
        readonly List<KeyValuePair<string, RobotModel>> live = new List<KeyValuePair<string, RobotModel>>();
        bool hooked;
        float moodTimer;

        /// <summary>Statistik: Winken ausgelöst, Freudensprünge ausgelöst.</summary>
        public int Waves { get; private set; }
        public int Hops { get; private set; }

        void Awake() { I = this; }

        void Start() { Hook(); }

        void Hook()
        {
            if (hooked || GameApp.I == null) return;
            GameApp.I.OnFx += OnFx;
            GameApp.I.OnEmote += OnEmote;
            hooked = true;
        }

        void OnDestroy()
        {
            if (hooked && GameApp.I != null) { GameApp.I.OnFx -= OnFx; GameApp.I.OnEmote -= OnEmote; }
            if (I == this) I = null;
        }

        static RobotModel Robot(string pid) { return ActorsView.I != null ? ActorsView.I.RobotOf(pid) : null; }

        void All(string gesture)
        {
            var w = GameApp.I != null ? GameApp.I.W : null;
            if (w == null) return;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online || p.Sleeping || p.TowTimer > 0) continue;
                var r = Robot(p.Id);
                if (r != null && r.gameObject.activeInHierarchy) { r.Gesture(gesture); if (gesture == "hop" || gesture == "cheer") Hops++; }
            }
        }

        void Near(Vector3 at, float radius, string gesture)
        {
            var w = GameApp.I != null ? GameApp.I.W : null;
            if (w == null) return;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online) continue;
                var r = Robot(p.Id);
                if (r != null && r.gameObject.activeInHierarchy && (r.transform.position - at).sqrMagnitude < radius * radius) { r.Gesture(gesture); Hops++; }
            }
        }

        void One(string pid, string gesture)
        {
            var r = Robot(pid);
            if (r == null || !r.gameObject.activeInHierarchy) return;
            r.Gesture(gesture);
            if (gesture == "hop" || gesture == "cheer") Hops++;
        }

        void OnFx(JObj f)
        {
            string pid = f.Str("pid");
            switch (f.Str("k"))
            {
                case "deposit": if (f.Int("n") > 0) One(pid, "hop"); break;
                case "sell": case "dispose": case "upgrade": One(pid, "hop"); break;
                case "delivery": case "contract": All("hop"); break;
                case "zone": case "areaclean": case "gate": case "eco": case "awaken": All("cheer"); break;
                case "morning": All("hop"); break;
                case "lore": { var r = Robot(pid); if (r != null) r.Emote("curious"); break; }
                case "shutdown": { var r = Robot(pid); if (r != null) r.Emote("sad"); break; }
                case "repaired":
                case "plant":
                    {
                        var a = f.Arr("pos");
                        if (a != null && a.Count >= 3) Near(new Vector3((float)Json.ToDouble(a[0], 0), (float)Json.ToDouble(a[1], 0), (float)Json.ToDouble(a[2], 0)), 14f, "hop");
                        break;
                    }
            }
        }

        void OnEmote(string pid, string e)
        {
            var r = Robot(pid);
            if (r == null) return;
            bool me = GameApp.I != null && GameApp.I.Client != null && pid == GameApp.I.Client.Pid;
            if (!me) r.Emote(e == "sad" || e == "curious" ? e : "happy"); // der eigene Roboter hat schon reagiert
            r.Gesture("hop");
            Hops++;
        }

        void Update()
        {
            Hook();
            var app = GameApp.I;
            var av = ActorsView.I;
            if (app == null || av == null || !app.InGame || app.W == null) return;
            var w = app.W;
            var ps = w.Cur;
            float dt = Time.deltaTime;
            live.Clear();
            foreach (var p in w.Players.Values)
            {
                if (!p.Online) continue;
                var r = av.RobotOf(p.Id);
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                r.AutoGestures = true;
                live.Add(new KeyValuePair<string, RobotModel>(p.Id, r));
            }
            moodTimer -= dt;
            if (moodTimer <= 0f)
            {
                moodTimer = 0.25f;
                float dark = Rules.Darkness(Rules.DayPhase(w, w.CurrentPlanet));
                float maxE = Mathf.Max(1f, w.MaxEnergy);
                bool insulated = w.TechLevel("insulation") > 0;
                foreach (var kv in live)
                {
                    PlayerData p;
                    if (!w.Players.TryGetValue(kv.Key, out p)) continue;
                    var r = kv.Value;
                    var pos = r.transform.position;
                    Vector3? look = null;
                    int mood = RobotModel.MoodNeutral;
                    Vector3 animal = Vector3.zero;
                    bool nearAnimal = Wildlife.I != null && Wildlife.I.NearestAnimal(pos, 7f, out animal);
                    if (nearAnimal) look = animal + Vector3.up * 0.2f;
                    else
                    {
                        // Mitspieler in der Nähe anschauen
                        float best = 64f;
                        foreach (var o in live)
                        {
                            if (o.Key == kv.Key) continue;
                            float d2 = (o.Value.transform.position - pos).sqrMagnitude;
                            if (d2 < best) { best = d2; look = o.Value.transform.position + Vector3.up * 1.1f; }
                        }
                    }
                    int area = PlanetLayout.AreaOf(pos.z);
                    if (ps.StormActive && p.Exposed) mood = RobotModel.MoodScared;
                    else if (p.Energy / maxE < 0.22f) mood = RobotModel.MoodTired;
                    else if (w.CurrentPlanet == "nivalis" && p.Exposed && (!insulated || dark > 0.5f || ps.StormWarn)) mood = RobotModel.MoodCold;
                    else if (dark > 0.65f) mood = RobotModel.MoodSleepy;
                    else if (nearAnimal) mood = RobotModel.MoodCurious;
                    else if (LifeCommon.AreaRestoration(w, ps, area) > 0.65f) mood = RobotModel.MoodHappy;
                    r.SetMood(mood, look);
                }
            }
            // Mitspieler kommen näher → beide winken (einmal je Annäherung, 20 s Pause je Paar)
            float now = Time.time;
            for (int i = 0; i < live.Count; i++)
                for (int k = i + 1; k < live.Count; k++)
                {
                    var a = live[i]; var b = live[k];
                    string key = string.CompareOrdinal(a.Key, b.Key) < 0 ? a.Key + "|" + b.Key : b.Key + "|" + a.Key;
                    float d = Vector3.Distance(a.Value.transform.position, b.Value.transform.position);
                    float prev;
                    bool had = pairDist.TryGetValue(key, out prev);
                    pairDist[key] = d;
                    if (!had) continue;
                    float cool;
                    pairCooldown.TryGetValue(key, out cool);
                    if (prev > 7f && d <= 6f && now >= cool)
                    {
                        a.Value.Gesture("wave", b.Value.transform.position + Vector3.up);
                        b.Value.Gesture("wave", a.Value.transform.position + Vector3.up);
                        pairCooldown[key] = now + 20f;
                        Waves++;
                    }
                }
        }
    }
}
