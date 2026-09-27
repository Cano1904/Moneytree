using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Zeichnet alle Müllobjekte per GPU-Instancing (ein Draw-Call je Typ und 1023 Objekte).
    /// Nahe Objekte werfen Schatten, entfernte werden per Sichtweite ausgeblendet. Keine GameObjects pro Objekt.
    /// </summary>
    public class TrashRenderer : MonoBehaviour
    {
        public static TrashRenderer I { get; private set; }

        class Batch
        {
            public Mesh Mesh;
            public Material Mat;
            public readonly List<Matrix4x4> Near = new List<Matrix4x4>();
            public readonly List<Matrix4x4> Far = new List<Matrix4x4>();
        }

        readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        Batch ice;
        // Je Draw-Call eine eigene Liste, damit keine Daten eines noch nicht gerenderten Aufrufs überschrieben werden
        readonly List<List<Matrix4x4>> chunkPool = new List<List<Matrix4x4>>();
        int chunkIdx;
        float timer;
        Material highlightMat, highlightBad;
        /// <summary>Schlüssel des vom Spieler anvisierten Objekts (Hervorhebung).</summary>
        public string HighlightKey;
        public bool HighlightBlocked;
        readonly Dictionary<string, ObjView> visible = new Dictionary<string, ObjView>();

        void Awake()
        {
            I = this;
            highlightMat = Mats.Unique(Mats.Fade, new Color(0.3f, 1f, 0.9f, 0.28f));
            highlightBad = Mats.Unique(Mats.Fade, new Color(1f, 0.45f, 0.2f, 0.28f));
        }

        public static Material MaterialFor(TrashType t)
        {
            var c = Mats.C(t.Color);
            if (t.Oil) return Mats.Get(Mats.Water, new Color(0.05f, 0.04f, 0.03f, 0.85f));
            if (t.Shape == "bottle" || t.Shape == "shards") return Mats.Get(Mats.Fade, new Color(c.r, c.g, c.b, 0.75f), null, 0.9f);
            if (t.Magnet || t.Shape == "can" || t.Shape == "sheetmetal") return Mats.Get(Mats.Metal, c);
            return Mats.Get(Mats.Opaque, c);
        }

        Batch Get(TrashType t)
        {
            Batch b;
            if (batches.TryGetValue(t.Id, out b)) return b;
            b = new Batch { Mesh = MeshKit.Trash(t.Shape), Mat = MaterialFor(t) };
            batches[t.Id] = b;
            return b;
        }

        public static Matrix4x4 MatrixFor(ObjView o)
        {
            float s = o.T.Oil || o.T.Crane ? o.Scale : o.Scale * o.T.Size;
            return Matrix4x4.TRS(new Vector3(o.Pos.x, o.Pos.y, o.Pos.z), Quaternion.Euler(0, o.Rot * Mathf.Rad2Deg, 0), Vector3.one * s);
        }

        /// <summary>Sichtbares Objekt nach Schlüssel (für Zielauswahl ohne erneute Suche).</summary>
        public bool TryGetVisible(string key, out ObjView o) { return visible.TryGetValue(key, out o); }

        void Update()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null || wv.World == null) return;
            timer -= Time.deltaTime;
            if (timer <= 0) { timer = 0.2f; Rebuild(wv); }
            Draw();
        }

        void Rebuild(WorldView wv)
        {
            foreach (var b in batches.Values) { b.Near.Clear(); b.Far.Clear(); }
            if (ice == null) ice = new Batch { Mesh = MeshKit.Cube, Mat = Mats.Get(Mats.Fade, new Color(0.75f, 0.9f, 1f, 0.55f), null, 0.95f) };
            ice.Near.Clear(); ice.Far.Clear();
            visible.Clear();
            var cam = Camera.main;
            if (cam == null) return;
            var cp = cam.transform.position;
            float view = GameApp.I != null ? GameApp.I.Settings.ViewDistance : 1f;
            float far = 150f * view, near = 40f * view;
            var ps = wv.World.Planet(wv.Planet);
            bool before = PhotoMode.Active && PhotoMode.ShowBefore;
            if (before)
            {
                foreach (var t in wv.Layout.Trash) Add(Rules.FromStatic(ps, t), cp, far, near, true);
            }
            else foreach (var o in Rules.All(ps)) Add(o, cp, far, near, false);
        }

        void Add(ObjView o, Vector3 cp, float far, float near, bool before)
        {
            float dx = o.Pos.x - cp.x, dz = o.Pos.z - cp.z;
            float d2 = dx * dx + dz * dz;
            float big = o.T.Crane ? 2f : 1f;
            if (d2 > far * far * big) return;
            var b = Get(o.T);
            var m = MatrixFor(o);
            bool isNear = d2 < near * near;
            (isNear ? b.Near : b.Far).Add(m);
            if (!before) visible[o.Key] = o;
            if (o.Frozen && !before)
            {
                float r = Rules.ObjRadius(o.T) * 2.2f + 0.4f;
                ice.Near.Add(Matrix4x4.TRS(new Vector3(o.Pos.x, o.Pos.y + r * 0.4f, o.Pos.z), Quaternion.Euler(0, o.Rot * 57f, 0), new Vector3(r, r * 0.8f, r)));
            }
        }

        void Draw()
        {
            bool shadows = GameApp.I == null || GameApp.I.Settings.Shadows > 0;
            chunkIdx = 0;
            foreach (var b in batches.Values)
            {
                DrawList(b.Mesh, b.Mat, b.Near, shadows ? ShadowCastingMode.On : ShadowCastingMode.Off);
                DrawList(b.Mesh, b.Mat, b.Far, ShadowCastingMode.Off);
            }
            if (ice != null) DrawList(ice.Mesh, ice.Mat, ice.Near, ShadowCastingMode.Off);
            ObjView h;
            if (HighlightKey != null && visible.TryGetValue(HighlightKey, out h))
            {
                var m = MatrixFor(h) * Matrix4x4.Scale(Vector3.one * 1.12f);
                Graphics.DrawMesh(MeshKit.Trash(h.T.Shape), m, HighlightBlocked ? highlightBad : highlightMat, 0, null, 0, null, ShadowCastingMode.Off, false);
            }
        }

        void DrawList(Mesh mesh, Material mat, List<Matrix4x4> list, ShadowCastingMode sh)
        {
            if (list.Count == 0) return;
            if (!SystemInfo.supportsInstancing)
            {
                foreach (var m in list) Graphics.DrawMesh(mesh, m, mat, 0, null, 0, null, sh, true);
                return;
            }
            for (int i = 0; i < list.Count; i += 1023)
            {
                if (chunkIdx >= chunkPool.Count) chunkPool.Add(new List<Matrix4x4>(1023));
                var chunk = chunkPool[chunkIdx++];
                chunk.Clear();
                int n = Mathf.Min(1023, list.Count - i);
                for (int k = 0; k < n; k++) chunk.Add(list[i + k]);
                Graphics.DrawMeshInstanced(mesh, 0, mat, chunk, null, sh, true);
            }
        }
    }
}
