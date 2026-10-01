using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Zeichnet alle Müllobjekte per GPU-Instancing (ein Draw-Call je Typ, Untermesh und 1023 Objekte).
    /// Die Formen haben bis zu fünf Untermeshes (Grundfarbe, dunkle Teile, Metall, Etiketten, Signalfarbe).
    /// Nahe Objekte werfen Schatten, entfernte werden per Sichtweite ausgeblendet. Keine GameObjects pro Objekt.
    /// </summary>
    public class TrashRenderer : MonoBehaviour
    {
        public static TrashRenderer I { get; private set; }

        class Batch
        {
            public Mesh Mesh;
            public Material[] Mats;
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

        /// <summary>
        /// Nur Darstellung: Lieferobjekte (<c>DynObj.Delivery</c>) bis zu diesem Zeitpunkt (<c>Time.time</c>) ausblenden – sie
        /// werden erst sichtbar, wenn der Frachter sie „ausgeladen“ hat (<see cref="ShipArrival"/>). Die Spiellogik bleibt unverändert.
        /// </summary>
        public static float HideDeliveriesUntil = -1f;
        /// <summary>Lieferobjekte (Dyn-Id), die trotz <see cref="HideDeliveriesUntil"/> schon sichtbar sind (bereits ausgeladen).</summary>
        public static readonly HashSet<string> RevealedDeliveries = new HashSet<string>();

        /// <summary>Beim nächsten Bild neu einsortieren (z. B. nach dem Ein-/Ausblenden von Lieferobjekten).</summary>
        public static void RefreshSoon() { if (I != null) I.timer = 0f; }

        static bool HiddenDelivery(ObjView o)
        {
            return o.D != null && o.D.Delivery && HideDeliveriesUntil > 0f && Time.time < HideDeliveriesUntil && !RevealedDeliveries.Contains(o.D.Id);
        }

        /// <summary>Nur Darstellung: dynamische Objekte (Dyn-Id) bis zu diesem Zeitpunkt ausblenden – z. B. Stücke eines gesprengten
        /// Müllbergs, die noch durch die Luft fliegen (<see cref="TntView"/>).</summary>
        public static readonly Dictionary<string, float> HiddenUntil = new Dictionary<string, float>();

        static bool HiddenFx(ObjView o)
        {
            float t;
            return o.D != null && HiddenUntil.Count > 0 && HiddenUntil.TryGetValue(o.D.Id, out t) && Time.time < t;
        }

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

        /// <summary>Material eines Nebenteils (Untermesh 1–4) – gemeinsam für alle Müllarten.</summary>
        public static Material SlotMaterial(int slot)
        {
            switch (slot)
            {
                case MeshKit.Dark: return Mats.Get(Mats.Opaque, new Color(0.12f, 0.12f, 0.13f), null, 0.35f);
                case MeshKit.Metal: return Mats.Get(Mats.Metal, new Color(0.66f, 0.68f, 0.7f));
                case MeshKit.Light: return Mats.Get(Mats.Opaque, new Color(0.9f, 0.88f, 0.8f));
                case MeshKit.Signal: return Mats.Get(Mats.Opaque, new Color(0.95f, 0.42f, 0.14f));
                default: return Mats.Get(Mats.Opaque, Color.gray);
            }
        }

        static readonly Dictionary<string, Material[]> matCache = new Dictionary<string, Material[]>();
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Mesh eines Müll-Typs: die Form aus MeshKit, deren schlichte deckende Teile (Grundfarbe, dunkle Teile, Etiketten,
        /// Signalfarbe) über die Farbpalette in einem Untermesh zusammengefasst sind. Metall- und Glasteile bleiben eigene
        /// Untermeshes. Dadurch braucht ein Typ meist nur ein bis zwei Draw-Calls statt bis zu fünf.
        /// </summary>
        public static Mesh MeshFor(TrashType t)
        {
            Mesh mesh;
            if (meshCache.TryGetValue(t.Id, out mesh) && mesh != null) return mesh;
            BuildTypeMesh(t);
            return meshCache[t.Id];
        }

        /// <summary>Materialien je Untermesh von <see cref="MeshFor"/>.</summary>
        public static Material[] MaterialsFor(TrashType t)
        {
            Material[] arr;
            if (matCache.TryGetValue(t.Id, out arr) && arr != null && arr.Length > 0 && arr[0] != null) return arr;
            BuildTypeMesh(t);
            return matCache[t.Id];
        }

        static void BuildTypeMesh(TrashType t)
        {
            var src = MeshKit.Trash(t.Shape);
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            src.GetVertices(verts); src.GetNormals(norms);
            var uvs = new List<Vector2>(new Vector2[verts.Count]);
            var groups = new List<KeyValuePair<Material, List<int>>>();
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                src.GetTriangles(tris, s);
                if (tris.Count == 0) continue;
                var m = s == 0 ? MaterialFor(t) : SlotMaterial(s);
                Material target; Vector2 uv;
                if (Palette.Route(m, out target, out uv)) foreach (var i in tris) uvs[i] = uv;
                else target = m;
                List<int> g = null;
                foreach (var kv in groups) if (kv.Key == target) g = kv.Value;
                if (g == null) { g = new List<int>(); groups.Add(new KeyValuePair<Material, List<int>>(target, g)); }
                g.AddRange(tris);
            }
            Palette.Flush();
            var mesh = new Mesh { name = "trashtype_" + t.Id };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = Mathf.Max(1, groups.Count);
            var mats = new Material[mesh.subMeshCount];
            for (int i = 0; i < groups.Count; i++) { mesh.SetTriangles(groups[i].Value, i, false); mats[i] = groups[i].Key; }
            if (groups.Count == 0) mats[0] = MaterialFor(t);
            mesh.RecalculateBounds();
            meshCache[t.Id] = mesh;
            matCache[t.Id] = mats;
        }

        /// <summary>Zeichnet ein einzelnes Müllobjekt mit allen Teilen (getragene Last, fliegende Objekte).</summary>
        public static void DrawTrash(TrashType t, Matrix4x4 m, Material overrideAll = null)
        {
            var mesh = MeshFor(t);
            var mats = MaterialsFor(t);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetIndexCount(s) == 0) continue;
                Graphics.DrawMesh(mesh, m, overrideAll ?? mats[Mathf.Min(s, mats.Length - 1)], 0, null, s, null, ShadowCastingMode.On, true);
            }
        }

        Batch Get(TrashType t)
        {
            Batch b;
            if (batches.TryGetValue(t.Id, out b)) return b;
            b = new Batch { Mesh = MeshFor(t), Mats = MaterialsFor(t) };
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
            if (ice == null) ice = new Batch { Mesh = MeshKit.Get("iceblock", bb => { bb.BoxJ(Vector3.zero, Vector3.one, Vector3.zero, 0.08f, 3); bb.BoxJ(new Vector3(0.2f, 0.45f, 0.1f), new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0, 30, 8), 0.06f, 4); }), Mats = new[] { Mats.Get(Mats.Fade, new Color(0.75f, 0.9f, 1f, 0.55f), null, 0.95f) } };
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
            else foreach (var o in Rules.All(ps)) { if (!HiddenDelivery(o) && !HiddenFx(o)) Add(o, cp, far, near, false); }
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
                DrawList(b.Mesh, b.Mats, b.Near, shadows ? ShadowCastingMode.On : ShadowCastingMode.Off);
                DrawList(b.Mesh, b.Mats, b.Far, ShadowCastingMode.Off);
            }
            if (ice != null) DrawList(ice.Mesh, ice.Mats, ice.Near, ShadowCastingMode.Off);
            ObjView h;
            if (HighlightKey != null && visible.TryGetValue(HighlightKey, out h))
            {
                var m = MatrixFor(h) * Matrix4x4.Scale(Vector3.one * 1.12f);
                var mesh = MeshFor(h.T);
                var hm = HighlightBlocked ? highlightBad : highlightMat;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    if (mesh.GetIndexCount(s) > 0) Graphics.DrawMesh(mesh, m, hm, 0, null, s, null, ShadowCastingMode.Off, false);
            }
        }

        void DrawList(Mesh mesh, Material[] mats, List<Matrix4x4> list, ShadowCastingMode sh)
        {
            if (list.Count == 0) return;
            int subs = mesh.subMeshCount;
            if (!SystemInfo.supportsInstancing)
            {
                foreach (var m in list)
                    for (int s = 0; s < subs; s++) Graphics.DrawMesh(mesh, m, mats[Mathf.Min(s, mats.Length - 1)], 0, null, s, null, sh, true);
                return;
            }
            for (int i = 0; i < list.Count; i += 1023)
            {
                if (chunkIdx >= chunkPool.Count) chunkPool.Add(new List<Matrix4x4>(1023));
                var chunk = chunkPool[chunkIdx++];
                chunk.Clear();
                int n = Mathf.Min(1023, list.Count - i);
                for (int k = 0; k < n; k++) chunk.Add(list[i + k]);
                for (int s = 0; s < subs; s++)
                {
                    if (mesh.GetIndexCount(s) == 0) continue;
                    Graphics.DrawMeshInstanced(mesh, s, mats[Mathf.Min(s, mats.Length - 1)], chunk, null, sh, true);
                }
            }
        }
    }
}
