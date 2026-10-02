using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Schätze im Müll (Darstellung): Müllobjekte mit einem noch nicht gefundenen Schatz funkeln aus der Nähe (bis 14 m um
    /// MIKO) – die Zuordnung rechnet jeder Client aus dem Weltsamen selbst (<see cref="Treasures"/>), Mitspieler sehen also
    /// dieselben Funkelstellen. Beim Fund steigt das kleine Modell des Schatzes funkelnd auf, dazu Klang und Hinweis „Fund!“.
    /// </summary>
    public class TreasureView : MonoBehaviour
    {
        public static TreasureView I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(TreasureView))) GameApp.Components.Add(typeof(TreasureView));
        }

        public const float GlintRange = 14f;

        class Pop { public TreasureDef Def; public Vector3 Pos; public float T; }

        readonly List<Vector3> glints = new List<Vector3>();
        readonly List<Pop> pops = new List<Pop>();
        float scan, sparkle;
        Material glintMat;

        /// <summary>Prüfumgebung: Zahl gerade funkelnder Objekte und laufender Fund-Anzeigen.</summary>
        public int Glints { get { return glints.Count; } }
        public int Pops { get { return pops.Count; } }

        void Awake() { I = this; }
        void Start() { if (GameApp.I != null) GameApp.I.OnFx += OnFx; }
        void OnDestroy() { if (GameApp.I != null) GameApp.I.OnFx -= OnFx; }

        void OnFx(JObj f)
        {
            if (f.Str("k") != "treasure") return;
            TreasureDef def;
            if (!GameData.TreasureById.TryGetValue(f.Str("id") ?? "", out def)) return;
            var a = f.Floats("pos");
            var pos = a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : (PlayerController.I != null ? PlayerController.I.RenderPos : Vector3.zero);
            pops.Add(new Pop { Def = def, Pos = pos });
            if (FxView.I != null)
            {
                FxView.I.Burst(pos + Vector3.up * 0.8f, new Color(1f, 0.92f, 0.55f), 50, 3f, 0.14f, 1.2f, -0.2f, true);
                FxView.I.Burst(pos + Vector3.up * 0.8f, Mats.C(def.Color), 20, 2f, 0.12f, 1f, -0.1f, true);
            }
            AudioManager.Play("treasure", pos, 0.9f);
            bool me = GameApp.I.Client != null && f.Str("pid") == GameApp.I.Client.Pid;
            string rarity = Loc.T(GameData.RarityNames[Mathf.Clamp(def.Rarity, 0, 2)]);
            string name = Loc.T(def.Name);
            if (me) Hud.Show(Loc.F("Fund! {0} ({1}) – ab in die Vitrine. {2}/{3} auf diesem Planeten.", name, rarity, f.Int("n"), f.Int("of")), ToastKind.Story, 6f);
            else if (f.Str("by") != null) Hud.Show(Loc.F("Fund! {0} hat „{1}“ ({2}) entdeckt – ab in die Vitrine.", f.Str("by"), name, rarity), ToastKind.Story, 6f);
            else Hud.Show(Loc.F("Fund! Ein Helfer hat „{0}“ ({1}) entdeckt – ab in die Vitrine.", name, rarity), ToastKind.Story, 6f);
        }

        void Update()
        {
            var app = GameApp.I;
            var w = app != null ? app.W : null;
            if (w == null || !app.InGame || PlayerController.I == null) { glints.Clear(); pops.Clear(); return; }
            if (glintMat == null) glintMat = Mats.Get(Mats.Opaque, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.9f, 0.55f) * 2.2f);
            float dt = Time.deltaTime;
            var me = PlayerController.I.RenderPos;
            scan -= dt;
            if (scan <= 0f)
            {
                scan = 0.4f;
                glints.Clear();
                if (!(PhotoMode.Active && PhotoMode.ShowBefore))
                {
                    var ps = w.Cur;
                    var l = WorldGen.Get(ps.Id);
                    foreach (var kv in Treasures.Carriers(w, ps.Id))
                    {
                        if (w.TreasureFound.Contains(kv.Value) || ps.Removed.Get(kv.Key) || kv.Key >= l.Trash.Count) continue;
                        var t = l.Trash[kv.Key];
                        var p = new Vector3(t.Pos.x, t.Pos.y, t.Pos.z);
                        if ((p - me).sqrMagnitude > GlintRange * GlintRange) continue;
                        glints.Add(p + Vector3.up * (Rules.ObjRadius(t.Def) * 1.2f + 0.35f));
                    }
                }
            }
            var cam = Camera.main;
            sparkle -= dt;
            bool burst = sparkle <= 0f;
            if (burst) sparkle = 0.6f;
            for (int i = 0; i < glints.Count; i++)
            {
                var p = glints[i];
                float d = (p - me).magnitude;
                float fade = Mathf.Clamp01((GlintRange - d) / 4f);
                float tw = 0.5f + 0.5f * Mathf.Sin(Time.time * 5.3f + i * 1.7f);
                float s = (0.12f + 0.22f * tw * tw) * fade;
                if (s < 0.02f) continue;
                var q = cam != null ? Quaternion.LookRotation(p - cam.transform.position) : Quaternion.identity;
                Graphics.DrawMesh(TntView.StarMesh, Matrix4x4.TRS(p, q * Quaternion.Euler(0, 0, Time.time * 40f + i * 30f), Vector3.one * s), glintMat, 0, null, 0, null, ShadowCastingMode.Off, false);
                if (burst && FxView.I != null && d < GlintRange * 0.8f) FxView.I.Burst(p, new Color(1f, 0.95f, 0.6f), 2, 0.4f, 0.06f, 0.6f, -0.1f, true);
            }
            // Fund-Anzeige: Modell steigt funkelnd auf und dreht sich
            for (int i = 0; i < pops.Count; i++)
            {
                var pp = pops[i];
                pp.T += dt;
                if (pp.T > 3f) { pops.RemoveAt(i); i--; continue; }
                float k = Mathf.Clamp01(pp.T / 0.8f);
                float scale = 1.6f * Mathf.SmoothStep(0f, 1f, k) * (pp.T > 2.6f ? (3f - pp.T) / 0.4f : 1f);
                var at = pp.Pos + Vector3.up * (0.6f + 1.4f * k);
                TreasureModels.Draw(pp.Def, Matrix4x4.TRS(at, Quaternion.Euler(-15f, pp.T * 140f, 0), Vector3.one * scale));
            }
        }
    }
}
