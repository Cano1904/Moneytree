using System;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Eigene Nachbearbeitung für die Built-in Render Pipeline (ohne Pakete) an der Hauptkamera:
    /// Luftperspektive (Höhennebel in Himmelsfarbe über die Tiefentextur), Umgebungsverdeckung (SSAO-Näherung),
    /// Konturlinien (Tiefe + Normalen), Bloom (Dual-Filter-Kette mit weichem Schwellwert), Sonnenstrahlen (Radialunschärfe
    /// vom Sonnenpunkt), Belichtung, Kontrast, ACES-Tonemapping, Split-Toning je Planet, Sättigung/Vibrance, Vignette,
    /// Filmkorn und (nur Ultra, sehr schwach) chromatische Aberration.
    /// Qualität 0 = aus (Unity-Nebel wie bisher), 1 = Grundstufe mit Konturen aus der Tiefe, 2 = zusätzlich Normalen-
    /// Konturen (DepthNormals), Umgebungsverdeckung (6 Abtastungen, halbe Auflösung), Sonnenstrahlen und leichtes Korn,
    /// 3 = alles (Verdeckung mit 10 Abtastungen).
    /// Belichtung (Kalibrierung siehe <see cref="Atmosphere"/>): ein Albedo-0,5-Grau in der Sonne landet nach ACES bei
    /// etwa 0,4 (linear), im Schatten bei 0,13 – Bildmittel um 0,18; ein weißes Teil in voller Sonne bei 0,7 (kein Clipping).
    /// Schlägt irgendetwas fehl, wird das Bild unverändert durchgereicht – nie ein schwarzer Bildschirm.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public static PostFX I { get; private set; }

        /// <summary>Nachbearbeitung läuft (Atmosphere überlässt dann Nebel und Fotomodus-Belichtung diesem Baustein).</summary>
        public static bool Running { get { return I != null && I.isActiveAndEnabled && I.mat != null && !I.failed; } }

        /// <summary>Zuletzt verwendete Belichtung (Anzeige/Fehlersuche).</summary>
        public static float LastExposure { get; private set; } = 1f;

        Camera cam;
        Material mat;
        bool failed;
        int quality = 2;
        bool reduceFlashing;
        RenderTextureFormat hdrFormat = RenderTextureFormat.DefaultHDR;
        readonly RenderTexture[] down = new RenderTexture[8];
        readonly RenderTexture[] up = new RenderTexture[8];
        readonly Vector3[] corners = new Vector3[4];

        // Nebelzustand dieses Bildes (Unity-Nebel wird während des Renderns ausgeschaltet und danach zurückgesetzt)
        bool fogSuppressed, fogThisFrame;
        Color fogColor;
        float fogDensity;

        static readonly int idBloomTex = Shader.PropertyToID("_BloomTex"), idShaftTex = Shader.PropertyToID("_ShaftTex"), idSkyCube = Shader.PropertyToID("_SkyCube"),
            idRayBL = Shader.PropertyToID("_RayBL"), idRayBR = Shader.PropertyToID("_RayBR"), idRayTL = Shader.PropertyToID("_RayTL"), idRayTR = Shader.PropertyToID("_RayTR"),
            idFogColor = Shader.PropertyToID("_FogColor"), idFogParams = Shader.PropertyToID("_FogParams"), idFogParams2 = Shader.PropertyToID("_FogParams2"),
            idSunDirW = Shader.PropertyToID("_SunDirW"), idSunColor = Shader.PropertyToID("_SunColor"), idSunScreen = Shader.PropertyToID("_SunScreen"),
            idShaftParams = Shader.PropertyToID("_ShaftParams"), idShaftColor = Shader.PropertyToID("_ShaftColor"), idBloom = Shader.PropertyToID("_Bloom"),
            idGrade = Shader.PropertyToID("_Grade"), idShadowTint = Shader.PropertyToID("_ShadowTint"), idHighlightTint = Shader.PropertyToID("_HighlightTint"),
            idFx = Shader.PropertyToID("_Fx"), idVignetteColor = Shader.PropertyToID("_VignetteColor"), idDepthOn = Shader.PropertyToID("_RP_DepthOn"),
            idViewInfo = Shader.PropertyToID("_ViewInfo"), idEdgeParams = Shader.PropertyToID("_EdgeParams"), idEdgeColor = Shader.PropertyToID("_EdgeColor"),
            idAOParams = Shader.PropertyToID("_AOParams"), idAOParams2 = Shader.PropertyToID("_AOParams2"), idAOTex = Shader.PropertyToID("_AOTex");

        const int PassPrefilter = 0, PassDown = 1, PassUp = 2, PassShaftMask = 3, PassShaftBlur = 4, PassComposite = 5, PassAO = 6;
        RenderTextureFormat aoFormat = RenderTextureFormat.Default;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            try
            {
                var sh = Resources.Load<Shader>("RePlanetPostFX") ?? Shader.Find("Hidden/RePlanet/PostFX");
                if (sh == null || !sh.isSupported)
                {
                    failed = true;
                    Debug.LogWarning("[RE:PLANET] Nachbearbeitung nicht verfügbar (Shader fehlt oder wird nicht unterstützt) – Bild ohne Effekte.");
                    return;
                }
                mat = new Material(sh) { name = "RePlanetPostFX", hideFlags = HideFlags.HideAndDontSave };
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)) hdrFormat = RenderTextureFormat.RGB111110Float;
                else if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)) hdrFormat = RenderTextureFormat.Default;
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)) aoFormat = RenderTextureFormat.R8;
                Configure(quality, false); // bis CameraRig die Einstellungen übergibt
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning("[RE:PLANET] Nachbearbeitung abgeschaltet: " + e.Message);
            }
        }

        /// <summary>Von CameraRig je Bild aufgerufen: Qualitätsstufe und Barrierefreiheit übernehmen.</summary>
        public void Configure(int q, bool calm)
        {
            quality = Mathf.Clamp(q, 0, 3);
            reduceFlashing = calm;
            bool want = quality >= 1 && !failed && mat != null;
            if (enabled != want) enabled = want;
            // Tiefentextur: für Luftperspektive, Konturen, Umgebungsverdeckung, Sonnenstrahlen und den Wasser-Shader
            if (want) cam.depthTextureMode |= DepthTextureMode.Depth;
            else cam.depthTextureMode &= ~DepthTextureMode.Depth;
            // Tiefe + Normalen (ein zusätzlicher Geometrie-Durchgang) erst ab „Hoch“: Normalen-Konturen und Verdeckung
            if (want && quality >= 2) cam.depthTextureMode |= DepthTextureMode.DepthNormals;
            else cam.depthTextureMode &= ~DepthTextureMode.DepthNormals;
            Shader.SetGlobalFloat(idDepthOn, want ? 1f : 0f);
        }

        void OnDisable()
        {
            if (fogSuppressed) { RenderSettings.fog = true; fogSuppressed = false; }
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (I == this) I = null;
        }

        void OnPreRender()
        {
            fogThisFrame = RenderSettings.fog;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            // nur wenn die Tiefentextur da ist – sonst bliebe das Bild ganz ohne Nebel
            if (Running && fogThisFrame && (cam.depthTextureMode & DepthTextureMode.Depth) != 0)
            {
                // Unity-Nebel aus, damit nicht doppelt vernebelt wird – die Luftperspektive übernimmt
                RenderSettings.fog = false;
                fogSuppressed = true;
            }
        }

        void OnPostRender()
        {
            if (fogSuppressed) { RenderSettings.fog = true; fogSuppressed = false; }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null || failed) { Graphics.Blit(src, dst); return; }
            try { Render(src, dst); }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning("[RE:PLANET] Nachbearbeitung abgeschaltet: " + e.Message);
                ReleaseAll();
                Graphics.Blit(src, dst);
            }
        }

        RenderTexture Get(int w, int h) { return Get(w, h, hdrFormat); }

        RenderTexture Get(int w, int h, RenderTextureFormat fmt)
        {
            var rt = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0, fmt, RenderTextureReadWrite.Linear);
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            return rt;
        }

        void ReleaseAll()
        {
            for (int i = 0; i < down.Length; i++)
            {
                if (down[i] != null) { RenderTexture.ReleaseTemporary(down[i]); down[i] = null; }
                if (up[i] != null) { RenderTexture.ReleaseTemporary(up[i]); up[i] = null; }
            }
        }

        void Render(RenderTexture src, RenderTexture dst)
        {
            var look = Atmosphere.I != null ? Atmosphere.I.Look : Atmosphere.DefaultLook;
            bool depthOn = (cam.depthTextureMode & DepthTextureMode.Depth) != 0;

            // ---------------- Sichtstrahlen der Bildecken (Welt, Sichttiefe 1) für die Tiefenrekonstruktion
            float far = cam.farClipPlane;
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), far, Camera.MonoOrStereoscopicEye.Mono, corners);
            var tr = cam.transform;
            mat.SetVector(idRayBL, tr.TransformVector(corners[0]) / far);
            mat.SetVector(idRayTL, tr.TransformVector(corners[1]) / far);
            mat.SetVector(idRayTR, tr.TransformVector(corners[2]) / far);
            mat.SetVector(idRayBR, tr.TransformVector(corners[3]) / far);

            // ---------------- Luftperspektive
            bool fog = fogThisFrame && depthOn;
            var sky = look.SkyCube;
            float skyShare = sky != null ? look.FogSky : 0f;
            if (sky != null) mat.SetTexture(idSkyCube, sky);
            mat.SetColor(idFogColor, new Color(fogColor.r, fogColor.g, fogColor.b, look.FogMax));
            mat.SetVector(idFogParams, new Vector4(fogDensity, look.FogFalloff, look.FogBase, fog ? 1f : 0f));
            mat.SetVector(idFogParams2, new Vector4(skyShare, 3f, look.FogLinear, depthOn ? 1f : 0f));
            mat.SetVector(idSunDirW, look.SunDir);
            mat.SetColor(idSunColor, new Color(look.SunColor.r, look.SunColor.g, look.SunColor.b, look.FogSunScatter));

            // ---------------- Farbkorrektur
            float brightness = GameApp.I != null && GameApp.I.Settings != null ? GameApp.I.Settings.Brightness : 1f;
            float baseExposure = look.AutoExposure ? Atmosphere.AutoExposureFor(brightness) * look.Exposure : look.Exposure;
            float exposure = baseExposure * (PhotoMode.Active ? PhotoMode.Exposure : 1f);
            LastExposure = exposure;
            mat.SetVector(idGrade, new Vector4(exposure, look.Contrast, look.Saturation, look.Vibrance));
            mat.SetColor(idShadowTint, new Color(look.ShadowTint.r, look.ShadowTint.g, look.ShadowTint.b, look.SplitAmount));
            mat.SetColor(idHighlightTint, new Color(look.HighlightTint.r, look.HighlightTint.g, look.HighlightTint.b, look.SplitAmount));
            mat.SetColor(idVignetteColor, look.VignetteColor);
            // Korn nur leicht (ab „Hoch“), chromatische Aberration nur auf „Ultra“ und kaum sichtbar (≈1 px in den Ecken)
            float grain = quality >= 2 ? 0.012f : 0f;
            float ca = quality >= 3 ? 0.0025f : 0f;
            float time = reduceFlashing ? 0.37f : Time.unscaledTime; // ruhiges Korn bei „Weniger Blitzeffekte“
            mat.SetVector(idFx, new Vector4(look.Vignette, grain, ca, time));

            // ---------------- Konturen und Umgebungsverdeckung
            bool normalsOn = (cam.depthTextureMode & DepthTextureMode.DepthNormals) != 0;
            float tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect0 = src.width / (float)Mathf.Max(1, src.height);
            mat.SetVector(idViewInfo, new Vector4(tanY * aspect0, tanY, normalsOn ? 1f : 0f, 0f));
            float edgeStrength = depthOn ? look.Outline * (quality >= 2 ? 1f : 0.8f) : 0f;
            float edgeStep = Mathf.Max(1f, src.height / 1080f);
            mat.SetVector(idEdgeParams, new Vector4(edgeStrength, edgeStep, look.OutlineFadeStart, look.OutlineFadeEnd));
            mat.SetColor(idEdgeColor, look.OutlineColor);
            bool aoOn = depthOn && normalsOn && quality >= 2 && look.AO > 0.01f;
            mat.SetVector(idAOParams, new Vector4(0.7f, look.AO, quality >= 3 ? 10f : 6f, 60f));
            mat.SetVector(idAOParams2, new Vector4(aoOn ? 1f : 0f, 0f, 0f, 0f));
            RenderTexture aoRT = null;
            if (aoOn)
            {
                aoRT = Get(src.width / 2, src.height / 2, aoFormat);
                Graphics.Blit(src, aoRT, mat, PassAO);
            }
            mat.SetTexture(idAOTex, aoRT != null ? (Texture)aoRT : Texture2D.whiteTexture);

            // ---------------- Bloom
            float bloomIntensity = look.Bloom * (reduceFlashing ? 0.6f : 1f);
            mat.SetVector(idBloom, new Vector4(look.BloomThreshold, look.BloomThreshold * 0.5f, bloomIntensity, reduceFlashing ? 6f : 20f));
            int levels = quality >= 3 ? 6 : quality == 2 ? 5 : 4;
            int w = src.width / 2, h = src.height / 2;
            int n = 0;
            for (int i = 0; i < levels; i++)
            {
                if (w < 4 || h < 4) break;
                down[i] = Get(w, h);
                if (i == 0) Graphics.Blit(src, down[0], mat, PassPrefilter);
                else Graphics.Blit(down[i - 1], down[i], mat, PassDown);
                n++;
                w /= 2; h /= 2;
            }
            RenderTexture bloom = n > 0 ? down[n - 1] : null;
            // Die Aufwärtskette summiert alle Stufen → Stärke auf die Stufenzahl normieren
            if (n > 0) mat.SetVector(idBloom, new Vector4(look.BloomThreshold, look.BloomThreshold * 0.5f, bloomIntensity * 3f / n, reduceFlashing ? 6f : 20f));
            for (int i = n - 2; i >= 0; i--)
            {
                up[i] = Get(down[i].width, down[i].height);
                mat.SetTexture(idBloomTex, down[i]);
                Graphics.Blit(bloom, up[i], mat, PassUp);
                bloom = up[i];
            }

            // ---------------- Sonnenstrahlen
            RenderTexture shafts = null, shaftTmp = null;
            float shaftStrength = 0f;
            if (quality >= 2 && look.ShaftStrength > 0.01f)
            {
                var sp = cam.WorldToViewportPoint(tr.position + look.SunDir * 1000f);
                if (sp.z > 0f)
                {
                    float outside = Mathf.Max(Mathf.Max(-sp.x, sp.x - 1f), Mathf.Max(-sp.y, sp.y - 1f));
                    shaftStrength = look.ShaftStrength * Mathf.Clamp01(1f - outside * 2.5f);
                    if (reduceFlashing) shaftStrength *= 0.7f;
                }
                if (shaftStrength > 0.01f)
                {
                    float aspect = src.width / (float)Mathf.Max(1, src.height);
                    mat.SetVector(idSunScreen, new Vector4(sp.x, sp.y, shaftStrength, aspect));
                    mat.SetColor(idShaftColor, look.SunColor);
                    int sw = Mathf.Max(8, src.width / 4), sh = Mathf.Max(8, src.height / 4);
                    shafts = Get(sw, sh);
                    shaftTmp = Get(sw, sh);
                    // Schwelle in belichteten Einheiten (die Maske liest das unbelichtete Bild)
                    float shaftThr = look.ShaftThreshold / Mathf.Max(0.05f, exposure);
                    mat.SetVector(idShaftParams, new Vector4(0f, shaftThr, 0.93f, 0.9f));
                    Graphics.Blit(src, shafts, mat, PassShaftMask);
                    int iterations = quality >= 3 ? 3 : 2;
                    float stepLen = 0.035f;
                    for (int k = 0; k < iterations; k++)
                    {
                        mat.SetVector(idShaftParams, new Vector4(stepLen, shaftThr, 0.93f, 0.9f));
                        Graphics.Blit(shafts, shaftTmp, mat, PassShaftBlur);
                        var t = shafts; shafts = shaftTmp; shaftTmp = t;
                        stepLen *= 2.6f;
                    }
                }
            }
            if (shaftStrength <= 0.01f) mat.SetVector(idSunScreen, new Vector4(0.5f, 0.5f, 0f, 1f));

            // ---------------- Zusammensetzen
            mat.SetTexture(idBloomTex, bloom != null ? (Texture)bloom : Texture2D.blackTexture);
            mat.SetTexture(idShaftTex, shafts != null ? (Texture)shafts : Texture2D.blackTexture);
            Graphics.Blit(src, dst, mat, PassComposite);

            if (aoRT != null) RenderTexture.ReleaseTemporary(aoRT);
            if (shafts != null) RenderTexture.ReleaseTemporary(shafts);
            if (shaftTmp != null) RenderTexture.ReleaseTemporary(shaftTmp);
            ReleaseAll();
        }
    }
}
