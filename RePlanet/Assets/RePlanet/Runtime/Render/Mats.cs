using System.Collections.Generic;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Material-Verwaltung. Vorlagen liegen als Assets in Resources/ (vom Editor-Setup angelegt, damit Shader-Varianten
    /// im Build enthalten sind) und werden je Farbe geklont und zwischengespeichert. Fällt auf Shader.Find zurück.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> templates = new Dictionary<string, Material>();
        static readonly Dictionary<(string, Color, Color, bool, float), Material> cache = new Dictionary<(string, Color, Color, bool, float), Material>();

        public const string Opaque = "RP_Opaque", Metal = "RP_Metal", Emissive = "RP_Emissive", Fade = "RP_Fade", Water = "RP_Water",
            Particle = "RP_Particle", ParticleAdd = "RP_ParticleAdd", Sky = "RP_Sky", Unlit = "RP_Unlit", UnlitTex = "RP_UnlitTex",
            UnlitTransparent = "RP_UnlitTransparent", Line = "RP_Line";

        public static Color C(uint hex, float a = 1f)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, a);
        }

        public static Material Template(string name)
        {
            Material m;
            if (templates.TryGetValue(name, out m) && m != null) return m;
            m = Resources.Load<Material>(name);
            if (m == null) m = Fallback(name);
            templates[name] = m;
            return m;
        }

        static Material Fallback(string name)
        {
            Shader s = null;
            switch (name)
            {
                case Particle: case ParticleAdd: s = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended"); break;
                case Sky: s = Shader.Find("Skybox/Procedural"); break;
                case Unlit: s = Shader.Find("Unlit/Color"); break;
                case UnlitTex: s = Shader.Find("Unlit/Texture"); break;
                case UnlitTransparent: s = Shader.Find("Unlit/Transparent"); break;
                case Line: s = Shader.Find("Sprites/Default"); break;
                default: s = Shader.Find("Standard"); break;
            }
            if (s == null) s = Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader");
            var m = new Material(s) { name = name + "_Fallback" };
            try
            {
                switch (name)
                {
                    case Metal: m.SetFloat("_Glossiness", 0.55f); m.SetFloat("_Metallic", 0.6f); break;
                    case Emissive: m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); break;
                    case Fade: SetupTransparent(m, 2); break;
                    case Water: SetupTransparent(m, 3); m.SetFloat("_Glossiness", 0.95f); break;
                    case Particle: SetupParticle(m, false); break;
                    case ParticleAdd: SetupParticle(m, true); break;
                    default: if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.2f); break;
                }
                m.enableInstancing = true;
            }
            catch { }
            return m;
        }

        static void SetupTransparent(Material m, int mode)
        {
            m.SetFloat("_Mode", mode);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", mode == 3 ? 1 : 5);
            m.SetInt("_DstBlend", 10);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            if (mode == 3) { m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.DisableKeyword("_ALPHABLEND_ON"); }
            else { m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
            m.renderQueue = 3000;
        }

        static void SetupParticle(Material m, bool additive)
        {
            if (!m.HasProperty("_Mode")) return;
            m.SetFloat("_Mode", additive ? 4 : 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", 5);
            m.SetInt("_DstBlend", additive ? 1 : 10);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }

        /// <summary>Material der Vorlage mit Farbe (und optional Leuchtfarbe). Wird zwischengespeichert.</summary>
        public static Material Get(string template, Color color, Color? emission = null, float gloss = -1f)
        {
            var key = (template, color, emission ?? Color.clear, emission.HasValue, gloss);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Template(template));
            m.name = template + "_" + ColorUtility.ToHtmlStringRGBA(color);
            m.color = color;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", color);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            if (gloss >= 0 && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        public static Material Get(string template, uint hex) { return Get(template, C(hex)); }

        /// <summary>Eigenständiges (nicht geteiltes) Material, z. B. für animierte Leuchtstärke.</summary>
        public static Material Unique(string template, Color color)
        {
            var m = new Material(Template(template)) { color = color };
            m.enableInstancing = true;
            return m;
        }

        public static void SetEmission(Material m, Color c)
        {
            if (m == null || !m.HasProperty("_EmissionColor")) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
        }
    }
}
