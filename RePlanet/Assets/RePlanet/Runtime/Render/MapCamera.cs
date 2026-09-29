using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// 3D-Karte: eine eigene Kamera zeichnet die tatsächliche Spielwelt schräg von oben (Perspektive, 50–72° Neigung)
    /// in eine RenderTexture, die die Oberfläche (UIRoot, Karte) bildschirmfüllend zeigt und mit Symbolen überlagert.
    /// <list type="bullet">
    /// <item>Rendert nur, solange die Oberfläche sie jedes Bild anfordert (<see cref="Request"/>); sonst ist die Kamera aus,
    /// die RenderTexture wird nach einigen Sekunden freigegeben.</item>
    /// <item>Kein Himmel (einfarbiger Hintergrund → keine Wolken), Wetterpartikel und Sterne sind während ihres Bildes
    /// ausgeblendet; eigener Kartennebel am Rand, feste „Kartensonne“ (auch nachts lesbar), größere Schattenweite.</item>
    /// <item>Höhenlinien, Bereichsgrenzen, Stützpunktfläche und Weltrand als dünne Bänder auf dem Gelände – nur für diese
    /// Kamera sichtbar (Renderer nur zwischen OnPreCull und OnPostRender eingeschaltet).</item>
    /// </list>
    /// Steuerung (Drehen, Neigen, Zoomen, Verschieben, Zentrieren) setzt die Oberfläche über die öffentlichen Methoden;
    /// die Kamera folgt weich. Schlägt die RenderTexture fehl, meldet <see cref="Failed"/> das – die Oberfläche zeigt dann die 2D-Karte.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public class MapCamera : MonoBehaviour
    {
        public static MapCamera I { get; private set; }

        public const float MinDistance = 28f, MaxDistance = 330f, MinPitch = 50f, MaxPitch = 72f, WorldHalf = 150f;
        /// <summary>Hintergrund- und Nebelfarbe der Karte (dunkles Petrol, passend zur Oberfläche).</summary>
        public static readonly Color Background = new Color(0.035f, 0.085f, 0.105f, 1f);

        public Camera Cam { get; private set; }
        public RenderTexture Texture { get; private set; }
        /// <summary>RenderTexture nicht verfügbar → die Oberfläche zeigt die 2D-Karte.</summary>
        public bool Failed { get; private set; }
        public bool Rendering { get { return Cam != null && Cam.enabled; } }
        /// <summary>Seit dem Öffnen (bzw. seit einer neuen RenderTexture) mindestens einmal gerendert.</summary>
        public bool HasFrame { get; private set; }
        /// <summary>Blickrichtung in Grad (0 = Norden, +z), Zielwerte – die Kamera gleitet weich dorthin.</summary>
        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; } = 60f;
        public float TargetDistance { get; private set; } = 170f;
        /// <summary>Aktuelle (geglättete) Werte.</summary>
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public float Distance { get { return dist; } }
        public Vector3 Pivot { get { return pivot; } }
        /// <summary>true, solange die Karte MIKO folgt (bis zum ersten Verschieben; <see cref="Recenter"/> schaltet es wieder ein).</summary>
        public bool Following { get { return follow; } }
        /// <summary>Zoomfaktor für die Anzeige (1× = ganze Welt).</summary>
        public float ZoomFactor { get { return MaxDistance / Mathf.Max(1f, TargetDistance); } }

        float yaw, pitch = 60f, dist = 170f;
        Vector3 pivot, pivotTarget;
        bool follow = true, open, snap;
        int requestFrame = -100, reqW = 1280, reqH = 720;
        float closedFor;

        // Nur für diese Kamera sichtbare Linien (Höhenlinien, Grenzen)
        GameObject overlayGo;
        MeshFilter overlayFilter;
        MeshRenderer overlayRenderer;
        Mesh overlayMesh;
        string overlayPlanet;
        float overlayWidth;
        Material overlayMat;

        // Während des eigenen Bildes geänderter Zustand (danach zurückgesetzt)
        readonly List<Renderer> hidden = new List<Renderer>();
        bool savedFog, inRender;
        FogMode savedFogMode;
        Color savedFogColor;
        float savedFogStart, savedFogEnd, savedFogDensity, savedShadowDistance;
        Light savedSun;
        Quaternion savedSunRot;
        Color savedSunColor;
        float savedSunIntensity;

        // ================================================================== Erzeugung
        /// <summary>Liefert die Kartenkamera (legt sie beim ersten Aufruf an).</summary>
        public static MapCamera Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("MapCamera");
            if (GameApp.I != null) go.transform.SetParent(GameApp.I.transform, false);
            return go.AddComponent<MapCamera>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            try
            {
                Cam = gameObject.AddComponent<Camera>();
                Cam.enabled = false;
                Cam.clearFlags = CameraClearFlags.SolidColor;
                Cam.backgroundColor = Background;
                Cam.fieldOfView = 38f;
                Cam.nearClipPlane = 1f;
                Cam.farClipPlane = 1500f;
                Cam.depth = -20f;           // vor der Hauptkamera
                Cam.allowHDR = false;
                Cam.allowMSAA = true;
                Cam.useOcclusionCulling = false;
            }
            catch (Exception e) { Failed = true; Debug.LogWarning("[Karte] Kamera nicht verfügbar: " + e.Message); }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            ReleaseTexture();
            if (overlayGo != null) Destroy(overlayGo);
            if (overlayMesh != null) Destroy(overlayMesh);
        }

        // ================================================================== Schnittstelle für die Oberfläche
        /// <summary>
        /// Jedes Bild aufrufen, solange die Karte zu sehen ist (aus Update, vor dem Rendern). w×h = Größe der Darstellung in
        /// Bildschirmpixeln. Liefert die RenderTexture (in diesem Bild gerendert) oder null (dann 2D-Karte zeigen).
        /// </summary>
        public RenderTexture Request(int w, int h)
        {
            if (Failed || Cam == null) return null;
            requestFrame = Time.frameCount;
            // Auflösung begrenzen (Karte, kein Spielbild): höchstens ~Full HD
            float s = Mathf.Min(1f, 1920f / Mathf.Max(1, w));
            reqW = Mathf.Clamp((int)(w * s), 64, 4096);
            reqH = Mathf.Clamp((int)(h * s), 64, 4096);
            if (!open) OpenMap();
            EnsureTexture();
            return Failed ? null : Texture;
        }

        public void Rotate(float degrees) { TargetYaw = Mathf.Repeat(TargetYaw + degrees, 360f); }
        public void Tilt(float degrees) { TargetPitch = Mathf.Clamp(TargetPitch + degrees, MinPitch, MaxPitch); }
        /// <summary>Zoom um einen Faktor (&lt; 1 = näher heran).</summary>
        public void Zoom(float factor) { TargetDistance = Mathf.Clamp(TargetDistance * factor, MinDistance, MaxDistance); }
        /// <summary>Verschieben in Blickrichtung der Karte: x = rechts, y = vorwärts (Meter).</summary>
        public void Pan(Vector2 metres)
        {
            if (metres.sqrMagnitude < 1e-8f) return;
            follow = false;
            var rot = Quaternion.Euler(0f, TargetYaw, 0f);
            var d = rot * new Vector3(metres.x, 0f, metres.y);
            pivotTarget.x = Mathf.Clamp(pivotTarget.x + d.x, -WorldHalf, WorldHalf);
            pivotTarget.z = Mathf.Clamp(pivotTarget.z + d.z, -WorldHalf, WorldHalf);
        }
        /// <summary>Zurück zu MIKO; die Karte folgt ihm wieder.</summary>
        public void Recenter() { follow = true; if (PlayerController.I != null) pivotTarget = PlayerController.I.RenderPos; }
        /// <summary>Nach Norden ausrichten.</summary>
        public void NorthUp() { TargetYaw = 0f; }

        /// <summary>Weltpunkt → Darstellung (0…1, y nach unten wie in der Oberfläche). false = hinter der Kamera.</summary>
        public bool Project(Vector3 world, out Vector2 uv)
        {
            uv = default(Vector2);
            if (Cam == null) return false;
            var v = Cam.WorldToViewportPoint(world);
            if (v.z < Cam.nearClipPlane) return false;
            uv = new Vector2(v.x, 1f - v.y);
            return true;
        }

        /// <summary>Geländehöhe (Wasseroberfläche, wo tiefer) des aktuellen Planeten.</summary>
        public static float Ground(string planet, float x, float z)
        {
            float h = Terrain.HeightAt(planet, x, z);
            float w = Terrain.WaterLevel(planet);
            return w > -50f && h < w ? w : h;
        }

        static string CurrentPlanet()
        {
            if (WorldView.I != null && !string.IsNullOrEmpty(WorldView.I.Planet)) return WorldView.I.Planet;
            var app = GameApp.I;
            return app != null && app.W != null ? app.W.CurrentPlanet : null;
        }

        // ================================================================== Ablauf
        void OpenMap()
        {
            open = true;
            Recenter();
            snap = true;
        }

        void CloseMap()
        {
            open = false;
            HasFrame = false;
            if (Cam != null) { Cam.enabled = false; Cam.targetTexture = null; }
            if (overlayRenderer != null) overlayRenderer.enabled = false;
        }

        void EnsureTexture()
        {
            if (Texture != null && Texture.width == reqW && Texture.height == reqH) return;
            try
            {
                if (Cam != null) Cam.targetTexture = null;
                ReleaseTexture();
                HasFrame = false;
                var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;
                Texture = new RenderTexture(reqW, reqH, 24, fmt)
                {
                    name = "Karte3D", antiAliasing = Mathf.Clamp(QualitySettings.antiAliasing, 1, 4), filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!Texture.Create()) throw new Exception("RenderTexture " + reqW + "×" + reqH + " konnte nicht erzeugt werden");
            }
            catch (Exception e)
            {
                Failed = true;
                ReleaseTexture();
                CloseMap();
                Debug.LogWarning("[Karte] 3D-Karte nicht verfügbar, 2D-Karte wird verwendet: " + e.Message);
            }
        }

        void ReleaseTexture()
        {
            if (Texture == null) return;
            if (Cam != null && Cam.targetTexture == Texture) Cam.targetTexture = null;
            Texture.Release();
            Destroy(Texture);
            Texture = null;
        }

        void LateUpdate()
        {
            var app = GameApp.I;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool want = !Failed && Time.frameCount - requestFrame <= 1 && app != null && app.InGame;
            if (!want)
            {
                if (open) CloseMap();
                if (Texture != null && (closedFor += dt) > 5f) ReleaseTexture();
                return;
            }
            closedFor = 0f;
            string planet = CurrentPlanet();
            if (planet == null) return;
            try
            {
                if (follow && PlayerController.I != null) pivotTarget = PlayerController.I.RenderPos;
                pivotTarget.x = Mathf.Clamp(pivotTarget.x, -WorldHalf, WorldHalf);
                pivotTarget.z = Mathf.Clamp(pivotTarget.z, -WorldHalf, WorldHalf);
                pivotTarget.y = Ground(planet, pivotTarget.x, pivotTarget.z);
                float k = snap ? 1f : 1f - Mathf.Exp(-9f * dt);
                snap = false;
                yaw = Mathf.LerpAngle(yaw, TargetYaw, k);
                pitch = Mathf.Lerp(pitch, TargetPitch, k);
                dist = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(1f, dist)), Mathf.Log(TargetDistance), k));
                pivot = Vector3.Lerp(pivot, pivotTarget, k);
                var rot = Quaternion.Euler(pitch, yaw, 0f);
                var pos = pivot - rot * Vector3.forward * dist;
                Cam.transform.SetPositionAndRotation(pos, rot);
                Cam.farClipPlane = dist * 3f + 500f;
                Cam.nearClipPlane = Mathf.Max(0.5f, dist * 0.02f);
                Cam.targetTexture = Texture;
                Cam.enabled = Texture != null;
                // Linienbreite passend zum Zoom (≈ 1,5 Bildpunkte bei Full HD); neu bauen, wenn sie deutlich abweicht
                float width = Mathf.Clamp(dist * 0.0055f, 0.12f, 2.2f);
                if (overlayPlanet != planet || overlayWidth <= 0f || Mathf.Abs(width / overlayWidth - 1f) > 0.35f) BuildOverlay(planet, width);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Failed = true;
                CloseMap();
            }
        }

        // ================================================================== Nur im Kartenbild
        void OnPreCull()
        {
            if (inRender) return;
            inRender = true;
            try
            {
                // Wetter, Sterne und andere Partikel direkt an der Atmosphäre/Kamera ausblenden (Kartenbild ohne Wetter)
                hidden.Clear();
                var atm = Atmosphere.I;
                if (atm != null)
                {
                    var root = atm.transform;
                    for (int i = 0; i < root.childCount; i++)
                    {
                        var ch = root.GetChild(i);
                        var pr = ch.GetComponent<ParticleSystemRenderer>();
                        if (pr != null && pr.enabled) { pr.enabled = false; hidden.Add(pr); }
                    }
                }
                if (overlayRenderer != null) overlayRenderer.enabled = true;
                savedShadowDistance = QualitySettings.shadowDistance;
                QualitySettings.shadowDistance = Mathf.Max(savedShadowDistance, dist * 2.2f);
                // Kartensonne: fest schräg von oben links (Relief lesbar, auch nachts)
                savedSun = atm != null ? atm.Sun : RenderSettings.sun;
                if (savedSun != null)
                {
                    savedSunRot = savedSun.transform.rotation; savedSunColor = savedSun.color; savedSunIntensity = savedSun.intensity;
                    savedSun.transform.rotation = Quaternion.Euler(52f, yaw - 35f, 0f);
                    savedSun.color = new Color(1f, 0.97f, 0.92f);
                    savedSun.intensity = Mathf.Max(1.05f, savedSunIntensity);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Karte] OnPreCull: " + e.Message); }
        }

        void OnPreRender()
        {
            try
            {
                savedFog = RenderSettings.fog; savedFogMode = RenderSettings.fogMode; savedFogColor = RenderSettings.fogColor;
                savedFogStart = RenderSettings.fogStartDistance; savedFogEnd = RenderSettings.fogEndDistance; savedFogDensity = RenderSettings.fogDensity;
                // Kartennebel: nah klar, zum Rand hin in die Hintergrundfarbe
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Background;
                RenderSettings.fogStartDistance = dist * 1.15f;
                RenderSettings.fogEndDistance = dist * 2.7f + 60f;
            }
            catch (Exception e) { Debug.LogWarning("[Karte] OnPreRender: " + e.Message); }
        }

        void OnPostRender()
        {
            Restore();
            if (Cam != null && Cam.targetTexture != null && Cam.targetTexture == Texture) HasFrame = true;
        }

        void OnDisable() { if (inRender) Restore(); }

        void Restore()
        {
            if (!inRender) return;
            inRender = false;
            try
            {
                RenderSettings.fog = savedFog; RenderSettings.fogMode = savedFogMode; RenderSettings.fogColor = savedFogColor;
                RenderSettings.fogStartDistance = savedFogStart; RenderSettings.fogEndDistance = savedFogEnd; RenderSettings.fogDensity = savedFogDensity;
                QualitySettings.shadowDistance = savedShadowDistance;
                if (savedSun != null)
                {
                    savedSun.transform.rotation = savedSunRot; savedSun.color = savedSunColor; savedSun.intensity = savedSunIntensity;
                }
                savedSun = null;
                foreach (var r in hidden) if (r != null) r.enabled = true;
                hidden.Clear();
                if (overlayRenderer != null) overlayRenderer.enabled = false;
            }
            catch (Exception e) { Debug.LogWarning("[Karte] Zurücksetzen: " + e.Message); }
        }

        // ================================================================== Höhenlinien und Grenzen
        static readonly Color ContourMinor = new Color(0.85f, 0.97f, 1f, 0.16f), ContourMajor = new Color(0.85f, 0.97f, 1f, 0.34f);
        static readonly Color Coast = new Color(0.55f, 0.92f, 1f, 0.6f), AreaLine = new Color(0.35f, 1f, 0.88f, 0.75f);
        static readonly Color BaseLine = new Color(0.3f, 0.95f, 0.85f, 0.6f), EdgeLine = new Color(1f, 1f, 1f, 0.28f);

        void BuildOverlay(string planet, float width)
        {
            overlayPlanet = planet;
            overlayWidth = width;
            var v = new List<Vector3>(8192); var c = new List<Color>(8192); var t = new List<int>(12288);
            const float step = 2.5f;
            int n = Mathf.RoundToInt(WorldHalf * 2f / step) + 1;
            var h = new float[n * n];
            float hmin = float.MaxValue, hmax = float.MinValue;
            float water = Terrain.WaterLevel(planet);
            bool hasWater = water > -50f;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float y = Terrain.HeightAt(planet, -WorldHalf + i * step, -WorldHalf + j * step);
                    h[j * n + i] = y;
                    if (y < hmin) hmin = y;
                    if (y > hmax) hmax = y;
                }
            float lo = hasWater ? Mathf.Max(hmin, water) : hmin;
            float range = hmax - lo;
            float lift = 0.35f;
            if (range > 1.2f)
            {
                float interval = NiceInterval(range / 12f);
                int k0 = Mathf.CeilToInt(lo / interval), k1 = Mathf.FloorToInt(hmax / interval);
                for (int k = k0; k <= k1; k++)
                {
                    float level = k * interval;
                    if (hasWater && level <= water + 0.05f) continue;
                    bool major = k % 5 == 0;
                    Contour(h, n, step, level, lift, major ? width * 1.5f : width, major ? ContourMajor : ContourMinor, v, c, t);
                }
            }
            if (hasWater && water > hmin && water < hmax) Contour(h, n, step, water + 0.02f, lift * 0.6f, width * 1.8f, Coast, v, c, t);
            // Bereichsgrenzen (z = ±50), Stützpunktfläche, Weltrand
            for (int g = 0; g < 2; g++) GroundLine(planet, new Vector2(-WorldHalf, g == 0 ? -50f : 50f), new Vector2(WorldHalf, g == 0 ? -50f : 50f), width * 2.6f, AreaLine, v, c, t);
            try
            {
                var bl = WorldGen.Get(planet).Base;
                GroundRect(planet, bl.MinX, bl.MinZ, bl.MaxX, bl.MaxZ, width * 2f, BaseLine, v, c, t);
            }
            catch (Exception) { }
            GroundRect(planet, -WorldHalf, -WorldHalf, WorldHalf, WorldHalf, width * 2f, EdgeLine, v, c, t);

            if (overlayGo == null)
            {
                overlayGo = new GameObject("MapOverlay");
                if (GameApp.I != null) overlayGo.transform.SetParent(GameApp.I.transform, false);
                overlayFilter = overlayGo.AddComponent<MeshFilter>();
                overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
                overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlayRenderer.receiveShadows = false;
                overlayRenderer.enabled = false;
                overlayMat = Mats.Get(Mats.Line, Color.white);
                overlayRenderer.sharedMaterial = overlayMat;
            }
            overlayGo.transform.position = Vector3.zero;
            overlayGo.transform.rotation = Quaternion.identity;
            if (overlayMesh == null) overlayMesh = new Mesh { name = "Kartenlinien" };
            overlayMesh.Clear();
            overlayMesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            overlayMesh.SetVertices(v);
            overlayMesh.SetColors(c);
            overlayMesh.SetTriangles(t, 0);
            overlayMesh.RecalculateBounds();
            overlayFilter.sharedMesh = overlayMesh;
        }

        static float NiceInterval(float raw)
        {
            foreach (var s in new[] { 0.5f, 1f, 2f, 2.5f, 4f, 5f, 10f, 20f }) if (s >= raw) return s;
            return 25f;
        }

        /// <summary>Höhenlinie (Marching Squares) als flache Bänder knapp über dem Gelände.</summary>
        static void Contour(float[] h, int n, float step, float level, float lift, float width, Color col, List<Vector3> v, List<Color> c, List<int> t)
        {
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    float a = h[j * n + i], b = h[j * n + i + 1], d = h[(j + 1) * n + i], e = h[(j + 1) * n + i + 1];
                    int code = (a > level ? 1 : 0) | (b > level ? 2 : 0) | (e > level ? 4 : 0) | (d > level ? 8 : 0);
                    if (code == 0 || code == 15) continue;
                    float x0 = -WorldHalf + i * step, z0 = -WorldHalf + j * step;
                    // Schnittpunkte auf den vier Kanten: unten (a–b), rechts (b–e), oben (d–e), links (a–d)
                    Vector3 pb = Lerp(x0, z0, x0 + step, z0, a, b, level), pr = Lerp(x0 + step, z0, x0 + step, z0 + step, b, e, level);
                    Vector3 pt = Lerp(x0, z0 + step, x0 + step, z0 + step, d, e, level), pl = Lerp(x0, z0, x0, z0 + step, a, d, level);
                    switch (code)
                    {
                        case 1: case 14: Seg(pl, pb, lift, width, col, v, c, t); break;
                        case 2: case 13: Seg(pb, pr, lift, width, col, v, c, t); break;
                        case 3: case 12: Seg(pl, pr, lift, width, col, v, c, t); break;
                        case 4: case 11: Seg(pr, pt, lift, width, col, v, c, t); break;
                        case 6: case 9: Seg(pb, pt, lift, width, col, v, c, t); break;
                        case 7: case 8: Seg(pl, pt, lift, width, col, v, c, t); break;
                        case 5: Seg(pl, pt, lift, width, col, v, c, t); Seg(pb, pr, lift, width, col, v, c, t); break;
                        case 10: Seg(pl, pb, lift, width, col, v, c, t); Seg(pr, pt, lift, width, col, v, c, t); break;
                    }
                }
        }

        static Vector3 Lerp(float x0, float z0, float x1, float z1, float h0, float h1, float level)
        {
            float f = Mathf.Abs(h1 - h0) < 1e-5f ? 0.5f : Mathf.Clamp01((level - h0) / (h1 - h0));
            return new Vector3(Mathf.Lerp(x0, x1, f), level, Mathf.Lerp(z0, z1, f));
        }

        /// <summary>Flaches Band von a nach b (Höhe = a.y/b.y + lift), Breite w.</summary>
        static void Seg(Vector3 a, Vector3 b, float lift, float w, Color col, List<Vector3> v, List<Color> c, List<int> t)
        {
            var d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var side = new Vector3(-d.z, 0f, d.x) / len * (w * 0.5f);
            // leicht verlängert, damit aneinanderstoßende Stücke ohne Lücke wirken
            var ext = d / len * (w * 0.35f);
            a -= ext; b += ext;
            int i = v.Count;
            v.Add(new Vector3(a.x - side.x, a.y + lift, a.z - side.z));
            v.Add(new Vector3(a.x + side.x, a.y + lift, a.z + side.z));
            v.Add(new Vector3(b.x + side.x, b.y + lift, b.z + side.z));
            v.Add(new Vector3(b.x - side.x, b.y + lift, b.z - side.z));
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>Linie auf dem Gelände von a nach b (x/z), alle 2,5 m der Höhe folgend.</summary>
        static void GroundLine(string planet, Vector2 a, Vector2 b, float w, Color col, List<Vector3> v, List<Color> c, List<int> t)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 2.5f));
            var prev = new Vector3(a.x, Ground(planet, a.x, a.y), a.y);
            for (int s = 1; s <= steps; s++)
            {
                var p2 = Vector2.Lerp(a, b, s / (float)steps);
                var cur = new Vector3(p2.x, Ground(planet, p2.x, p2.y), p2.y);
                Seg(prev, cur, 0.4f, w, col, v, c, t);
                prev = cur;
            }
        }

        static void GroundRect(string planet, float x0, float z0, float x1, float z1, float w, Color col, List<Vector3> v, List<Color> c, List<int> t)
        {
            GroundLine(planet, new Vector2(x0, z0), new Vector2(x1, z0), w, col, v, c, t);
            GroundLine(planet, new Vector2(x1, z0), new Vector2(x1, z1), w, col, v, c, t);
            GroundLine(planet, new Vector2(x1, z1), new Vector2(x0, z1), w, col, v, c, t);
            GroundLine(planet, new Vector2(x0, z1), new Vector2(x0, z0), w, col, v, c, t);
        }
    }
}
