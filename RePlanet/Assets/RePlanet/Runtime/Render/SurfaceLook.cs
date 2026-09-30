using System;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Oberflächen statt Flachfarben: eigener Oberflächen-Shader (Resources/RePlanetSurface.shader) mit prozeduraler,
    /// kachelbarer Detailtextur (Flecken, Körnung, Risse/Kratzer, Rost-/Schmutzmasken), Fenster-Shader
    /// (Resources/RePlanetWindow.shader: Rahmen, Sprossen, Spiegelung, nachts teils beleuchtete Innenräume) und ein
    /// Piktogramm-Atlas für Schilder (Zahnrad, Kiste, Münze, Blitz, Gefahrenraute, Pfeile, Schiff …) statt Schrift.
    /// Alle Texturen entstehen einmal beim ersten Gebrauch und werden zwischengespeichert. Fehlen die Shader, werden sie
    /// nicht unterstützt oder ist der Oberflächen-Shader abgeschaltet (Standard, siehe <see cref="DetailShadersAllowed"/>),
    /// gibt es Standard-Materialien mit Farbpalette.
    /// </summary>
    public static class SurfaceLook
    {
        static Shader surface, window;
        static bool loaded;
        static Texture2D detail, icons;
        static Material signMat;

        /// <summary>
        /// Übersteuerung für Prüfumgebungen (null = Einstellung <see cref="Settings.DetailShaders"/> gilt). Muss vor dem
        /// ersten Material gesetzt werden – die Entscheidung fällt einmal beim ersten Gebrauch.
        /// </summary>
        public static bool? ForceDetailShaders;

        /// <summary>
        /// Darf der eigene Oberflächen-Shader benutzt werden? Standard: nein (<see cref="Settings.DetailShadersDefault"/>).
        /// Hintergrund: In Unity erschienen alle Paletten-Teile (Gebäude, MIKO) mit ihm reinweiß; der Standard-Shader mit
        /// Farbpalette ist der geprüfte, verlässliche Weg. Einschalten über die Einstellung „Detail-Shader“.
        /// </summary>
        public static bool DetailShadersAllowed
        {
            get
            {
                if (ForceDetailShaders.HasValue) return ForceDetailShaders.Value;
                try
                {
                    var app = GameApp.I;
                    if (app != null && app.Settings != null) return app.Settings.DetailShaders;
                }
                catch { }
                return Settings.DetailShadersDefault;
            }
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            if (DetailShadersAllowed) surface = Mats.CustomShader("RePlanetSurface", "RePlanet/Surface");
            window = Mats.CustomShader("RePlanetWindow", "RePlanet/Window");
        }

        /// <summary>Ist der Oberflächen-Shader aktiv (Einstellung an und vom System unterstützt)?</summary>
        public static bool Custom { get { Load(); return surface != null; } }

        /// <summary>
        /// Deckendes Material mit Oberflächen-Detail (Klasse je Ecke aus UV-Kanal 1). <paramref name="main"/> ist die
        /// Grundfarbtextur (Farbpalette) oder null (dann nur <paramref name="c"/>). Rückfall: Standard-Material.
        /// </summary>
        public static Material Lit(string name, Color c, float gloss, float metallic, Texture main = null, int forceClass = -1)
        {
            Load();
            if (surface != null)
            {
                try
                {
                    var m = new Material(surface) { name = name };
                    m.color = c;
                    if (main != null) m.mainTexture = main;
                    m.SetTexture("_DetailTex", DetailTex());
                    m.SetFloat("_Glossiness", gloss);
                    m.SetFloat("_Metallic", metallic);
                    m.SetFloat("_SurfClass", forceClass);
                    m.enableInstancing = true;
                    return m;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Oberflächen-Shader: " + e.Message); }
            }
            var f = Mats.Unique(metallic > 0.01f ? Mats.Metal : Mats.Opaque, c);
            f.name = name;
            if (main != null) f.mainTexture = main;
            try { f.SetFloat("_Glossiness", gloss); if (metallic > 0.01f) f.SetFloat("_Metallic", metallic); } catch { }
            return f;
        }

        /// <summary>Leuchttextur (je Ecke über die Paletten-UV) für das Leucht-Paletten-Material.</summary>
        public static void SetGlow(Material m, Texture glowTex, float scale)
        {
            if (m == null) return;
            try { m.SetTexture("_GlowTex", glowTex); m.SetFloat("_GlowScale", scale); } catch { }
        }

        /// <summary>
        /// Fenster: Glas mit Rahmen und Sprossen, Himmelsspiegelung; die Leuchtfarbe (_EmissionColor, von WorldView je
        /// Bereich gesetzt) schaltet einen Teil der Innenräume warm ein (einzelne flackern selten).
        /// </summary>
        public static Material Window(Color glass)
        {
            Load();
            if (window != null)
            {
                try
                {
                    var m = new Material(window) { name = "RP_Window" };
                    m.color = glass;
                    m.SetTexture("_DetailTex", DetailTex());
                    m.SetColor("_EmissionColor", Color.black);
                    m.enableInstancing = true;
                    return m;
                }
                catch (Exception e) { Debug.LogWarning("[RE:PLANET] Fenster-Shader: " + e.Message); }
            }
            var f = Mats.Unique(Mats.Emissive, glass);
            try { f.SetFloat("_Glossiness", 0.9f); } catch { }
            Mats.SetEmission(f, Color.black);
            return f;
        }

        // ================================================================== Piktogramme
        /// <summary>Symbole des Piktogramm-Atlas (4 × 4 Zellen).</summary>
        public static class Icon
        {
            public const int Recycle = 0, Crate = 1, Gear = 2, Coin = 3, Bolt = 4, Hazard = 5, Sort = 6, Ship = 7,
                Bag = 8, Board = 9, Rover = 10, House = 11, Star = 12, Arrow = 13, Drop = 14, Leaf = 15;
        }

        /// <summary>UV-Rechteck (Skalierung, Versatz) einer Atlas-Zelle – für <see cref="MeshBuilder.UVRect"/>.</summary>
        public static Vector4 IconRect(int icon)
        {
            icon = Mathf.Clamp(icon, 0, 15);
            const float pad = 0.25f / 128f;
            return new Vector4(0.25f - pad * 2f, 0.25f - pad * 2f, (icon % 4) * 0.25f + pad, (icon / 4) * 0.25f + pad);
        }

        /// <summary>Leuchtendes Schildmaterial mit dem Piktogramm-Atlas (Grundfarbe und Leuchten aus derselben Textur).</summary>
        public static Material SignMaterial()
        {
            if (signMat != null) return signMat;
            signMat = Mats.Unique(Mats.Emissive, Color.white);
            signMat.name = "RP_Piktogramme";
            try
            {
                signMat.mainTexture = IconAtlas();
                signMat.SetTexture("_EmissionMap", IconAtlas());
                signMat.SetFloat("_Glossiness", 0.55f);
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Piktogramme: " + e.Message); }
            Mats.SetEmission(signMat, new Color(1f, 1f, 1f) * 1.15f);
            return signMat;
        }

        static Material paintedMat;

        /// <summary>Nicht leuchtende Variante (aufgemalte, verblasste Ladenzeichen an Fassaden).</summary>
        public static Material PaintedIconMaterial()
        {
            if (paintedMat != null) return paintedMat;
            paintedMat = Mats.Unique(Mats.Opaque, new Color(0.85f, 0.83f, 0.8f));
            paintedMat.name = "RP_Piktogramme_Farbe";
            try { paintedMat.mainTexture = IconAtlas(); paintedMat.SetFloat("_Glossiness", 0.25f); }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Piktogramme: " + e.Message); }
            return paintedMat;
        }

        // ------------------------------------------------------------------ 2D-Abstandsfunktionen
        static float Len(float x, float y) { return Mathf.Sqrt(x * x + y * y); }
        static float Circle(float x, float y, float cx, float cy, float r) { return Len(x - cx, y - cy) - r; }
        static float Ring(float x, float y, float cx, float cy, float r, float w) { return Mathf.Abs(Len(x - cx, y - cy) - r) - w; }

        static float RBox(float x, float y, float cx, float cy, float hx, float hy, float round, float rotDeg = 0f)
        {
            x -= cx; y -= cy;
            if (rotDeg != 0f)
            {
                float a = -rotDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float rx = x * ca - y * sa, ry = x * sa + y * ca; x = rx; y = ry;
            }
            float qx = Mathf.Abs(x) - hx + round, qy = Mathf.Abs(y) - hy + round;
            return Len(Mathf.Max(qx, 0), Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
        }

        static float Seg(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            return Len(px - dx * h, py - dy * h) - w;
        }

        static float Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            // vorzeichenbehafteter Abstand zu einem Dreieck (Kanten-Halbebenen, außen näherungsweise euklidisch)
            float d = Mathf.Min(Mathf.Min(SegD(x, y, ax, ay, bx, by), SegD(x, y, bx, by, cx, cy)), SegD(x, y, cx, cy, ax, ay));
            float s1 = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
            float s2 = (cx - bx) * (y - by) - (cy - by) * (x - bx);
            float s3 = (ax - cx) * (y - cy) - (ay - cy) * (x - cx);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        static float SegD(float x, float y, float ax, float ay, float bx, float by) { return Seg(x, y, ax, ay, bx, by, 0f); }

        static float Arrow(float x, float y, float ax, float ay, float bx, float by, float w, float head)
        {
            float dx = bx - ax, dy = by - ay, l = Mathf.Max(1e-4f, Len(dx, dy));
            dx /= l; dy /= l;
            float nx = -dy, ny = dx;
            float sx = bx - dx * head, sy = by - dy * head;
            float d = Seg(x, y, ax, ay, sx + dx * 0.02f, sy + dy * 0.02f, w);
            return Mathf.Min(d, Tri(x, y, bx, by, sx + nx * head * 0.75f, sy + ny * head * 0.75f, sx - nx * head * 0.75f, sy - ny * head * 0.75f));
        }

        static float Star(float x, float y, float r, float inner)
        {
            float best = 1e9f;
            bool inside = false;
            var px = new float[10]; var py = new float[10];
            for (int k = 0; k < 10; k++)
            {
                float a = Mathf.PI * 0.5f + k * Mathf.PI / 5f, rr = k % 2 == 0 ? r : r * inner;
                px[k] = Mathf.Cos(a) * rr; py[k] = Mathf.Sin(a) * rr;
            }
            for (int k = 0, j = 9; k < 10; j = k++)
            {
                best = Mathf.Min(best, SegD(x, y, px[j], py[j], px[k], py[k]));
                if (((py[k] > y) != (py[j] > y)) && (x < (px[j] - px[k]) * (y - py[k]) / (py[j] - py[k]) + px[k])) inside = !inside;
            }
            return inside ? -best : best;
        }

        /// <summary>Abstandsfunktion des Symbols (x, y in −1..1, y nach oben; negativ = innen).</summary>
        static float IconSdf(int icon, float x, float y)
        {
            switch (icon)
            {
                case Icon.Recycle:
                    {
                        float d = 1e9f;
                        for (int k = 0; k < 3; k++)
                        {
                            float a0 = k * 120f + 20f, a1 = a0 + 78f;
                            // Bogen: Ring, auf den Winkelbereich beschränkt
                            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                            float rel = Mathf.Repeat(ang - a0, 360f);
                            float arc = rel <= a1 - a0 ? Ring(x, y, 0, 0, 0.56f, 0.1f) : 1e9f;
                            d = Mathf.Min(d, arc);
                            float e = a1 * Mathf.Deg2Rad, ex = Mathf.Cos(e) * 0.56f, ey = Mathf.Sin(e) * 0.56f;
                            float tx = -Mathf.Sin(e), ty = Mathf.Cos(e), nx = Mathf.Cos(e), ny = Mathf.Sin(e);
                            d = Mathf.Min(d, Tri(x, y, ex + tx * 0.3f, ey + ty * 0.3f, ex + nx * 0.26f, ey + ny * 0.26f, ex - nx * 0.26f, ey - ny * 0.26f));
                        }
                        return d;
                    }
                case Icon.Crate:
                    {
                        float d = 1e9f;
                        var vx = new float[6]; var vy = new float[6];
                        for (int k = 0; k < 6; k++) { float a = (30f + k * 60f) * Mathf.Deg2Rad; vx[k] = Mathf.Cos(a) * 0.74f; vy[k] = Mathf.Sin(a) * 0.74f; }
                        for (int k = 0; k < 6; k++) d = Mathf.Min(d, Seg(x, y, vx[k], vy[k], vx[(k + 1) % 6], vy[(k + 1) % 6], 0.075f));
                        d = Mathf.Min(d, Seg(x, y, 0, 0, vx[0], vy[0], 0.07f));
                        d = Mathf.Min(d, Seg(x, y, 0, 0, vx[2], vy[2], 0.07f));
                        d = Mathf.Min(d, Seg(x, y, 0, 0, 0, -0.74f, 0.07f));
                        return d;
                    }
                case Icon.Gear:
                    {
                        float d = Circle(x, y, 0, 0, 0.5f);
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k * 45f, ar = a * Mathf.Deg2Rad;
                            d = Mathf.Min(d, RBox(x, y, Mathf.Cos(ar) * 0.6f, Mathf.Sin(ar) * 0.6f, 0.16f, 0.12f, 0.03f, a));
                        }
                        return Mathf.Max(d, -Circle(x, y, 0, 0, 0.21f));
                    }
                case Icon.Coin:
                    {
                        float d = Circle(x, y, 0, 0, 0.72f);
                        d = Mathf.Max(d, -Ring(x, y, 0, 0, 0.55f, 0.035f));
                        float mark = Mathf.Min(Ring(x, y, 0, 0, 0.3f, 0.07f), 1e9f);
                        // „C“ – rechts offen
                        if (x > 0.12f && Mathf.Abs(y) < 0.2f) mark = 1e9f;
                        mark = Mathf.Min(mark, Seg(x, y, 0, -0.45f, 0, 0.45f, 0.05f));
                        return Mathf.Max(d, -mark);
                    }
                case Icon.Bolt:
                    return Mathf.Min(Tri(x, y, 0.22f, 0.9f, -0.46f, -0.08f, 0.12f, -0.08f), Tri(x, y, -0.12f, 0.1f, 0.46f, 0.1f, -0.22f, -0.9f));
                case Icon.Hazard:
                    {
                        float d = Mathf.Abs(RBox(x, y, 0, 0, 0.58f, 0.58f, 0.06f, 45f)) - 0.075f;
                        d = Mathf.Min(d, Seg(x, y, 0, 0.36f, 0, -0.06f, 0.085f));
                        return Mathf.Min(d, Circle(x, y, 0, -0.3f, 0.09f));
                    }
                case Icon.Sort:
                    {
                        float d = Seg(x, y, 0, -0.8f, 0, -0.15f, 0.08f);
                        d = Mathf.Min(d, Arrow(x, y, 0, -0.15f, -0.55f, 0.55f, 0.075f, 0.28f));
                        d = Mathf.Min(d, Arrow(x, y, 0, -0.15f, 0, 0.8f, 0.075f, 0.28f));
                        return Mathf.Min(d, Arrow(x, y, 0, -0.15f, 0.55f, 0.55f, 0.075f, 0.28f));
                    }
                case Icon.Ship:
                    {
                        float d = RBox(x, y, 0, -0.02f, 0.22f, 0.45f, 0.2f);
                        d = Mathf.Min(d, Tri(x, y, -0.21f, 0.3f, 0.21f, 0.3f, 0, 0.88f));
                        d = Mathf.Min(d, Tri(x, y, -0.2f, 0.0f, -0.2f, -0.45f, -0.55f, -0.62f));
                        d = Mathf.Min(d, Tri(x, y, 0.2f, 0.0f, 0.2f, -0.45f, 0.55f, -0.62f));
                        d = Mathf.Max(d, -Circle(x, y, 0, 0.14f, 0.1f));
                        return Mathf.Min(d, Tri(x, y, -0.13f, -0.55f, 0.13f, -0.55f, 0, -0.9f));
                    }
                case Icon.Bag:
                    {
                        float d = RBox(x, y, 0, -0.2f, 0.56f, 0.5f, 0.1f);
                        float handle = Mathf.Max(Ring(x, y, 0, 0.3f, 0.27f, 0.065f), 0.3f - y);
                        d = Mathf.Min(d, handle);
                        return Mathf.Max(d, -Circle(x, y, 0, -0.12f, 0.12f));
                    }
                case Icon.Board:
                    {
                        float d = Mathf.Abs(RBox(x, y, 0, -0.06f, 0.5f, 0.7f, 0.08f)) - 0.07f;
                        d = Mathf.Min(d, RBox(x, y, 0, 0.66f, 0.24f, 0.12f, 0.04f));
                        for (int k = 0; k < 3; k++) d = Mathf.Min(d, Seg(x, y, -0.26f, 0.28f - k * 0.27f, 0.26f, 0.28f - k * 0.27f, 0.05f));
                        return d;
                    }
                case Icon.Rover:
                    {
                        float d = RBox(x, y, 0, -0.04f, 0.72f, 0.22f, 0.07f);
                        d = Mathf.Min(d, RBox(x, y, -0.16f, 0.3f, 0.36f, 0.2f, 0.08f));
                        d = Mathf.Max(d, -RBox(x, y, -0.16f, 0.32f, 0.25f, 0.1f, 0.04f));
                        foreach (var wx in new[] { -0.42f, 0.42f })
                        {
                            d = Mathf.Max(d, -Circle(x, y, wx, -0.32f, 0.24f));
                            d = Mathf.Min(d, Ring(x, y, wx, -0.32f, 0.12f, 0.07f));
                        }
                        return d;
                    }
                case Icon.House:
                    {
                        float d = Tri(x, y, -0.8f, 0.08f, 0.8f, 0.08f, 0, 0.8f);
                        d = Mathf.Min(d, RBox(x, y, 0, -0.32f, 0.54f, 0.44f, 0.02f));
                        return Mathf.Max(d, -RBox(x, y, 0, -0.48f, 0.15f, 0.3f, 0.04f));
                    }
                case Icon.Star:
                    return Star(x, y, 0.82f, 0.42f);
                case Icon.Arrow:
                    return Arrow(x, y, -0.7f, 0, 0.75f, 0, 0.13f, 0.45f);
                case Icon.Drop:
                    return Mathf.Min(Circle(x, y, 0, -0.2f, 0.46f), Tri(x, y, -0.4f, -0.02f, 0.4f, -0.02f, 0, 0.85f));
                default:
                    {
                        // Blatt: Linse aus zwei Kreisen, schräg, mit Stiel
                        float c = 0.7071f, rx = (x + y) * c, ry = (y - x) * c;
                        float d = Mathf.Max(Circle(rx, ry, 0, -0.42f, 0.78f), Circle(rx, ry, 0, 0.42f, 0.78f));
                        d = Mathf.Max(d, -Seg(rx, ry, -0.5f, 0, 0.5f, 0, 0.03f));
                        return Mathf.Min(d, Seg(x, y, -0.3f, -0.3f, -0.7f, -0.7f, 0.05f));
                    }
            }
        }

        /// <summary>Farbe (Symbol, Rand) je Piktogramm.</summary>
        static readonly uint[] IconCol = { 0x2EE0CC, 0x2EC4B6, 0xFF9A3C, 0xF2C14E, 0x9BE05A, 0xFF5A48, 0x4FB4F0, 0xE8F0FF, 0xC89CF0, 0xF2C14E, 0xFF9A3C, 0x9BE0C8, 0xFFD060, 0xFFFFFF, 0x4FC8F0, 0x7AD66A };

        /// <summary>Piktogramm-Atlas: 4 × 4 Zellen à 128 px, dunkles Schildfeld mit farbigem Rand und leuchtendem Symbol.</summary>
        public static Texture2D IconAtlas()
        {
            if (icons != null) return icons;
            const int N = 512, C = 128;
            var px = new Color32[N * N];
            var bg = new Color(0.07f, 0.08f, 0.1f);
            for (int icon = 0; icon < 16; icon++)
            {
                var col = Mats.C(IconCol[icon]);
                int ox = (icon % 4) * C, oy = (icon / 4) * C;
                float pix = 2f / C;
                for (int y = 0; y < C; y++)
                    for (int x = 0; x < C; x++)
                    {
                        float u = (x + 0.5f) / C * 2f - 1f, v = (y + 0.5f) / C * 2f - 1f;
                        // Schildfeld mit Rand und leichtem Verlauf
                        float frame = RBox(u, v, 0, 0, 0.96f, 0.96f, 0.14f);
                        float rim = Mathf.Clamp01(0.5f - (Mathf.Abs(frame + 0.05f) - 0.035f) / pix);
                        var c = bg * (1.1f - v * 0.15f);
                        c = Color.Lerp(c, col * 0.75f, rim);
                        // Symbol mit schmalem dunklem Hof (bessere Lesbarkeit) und hellem Kern
                        float d = IconSdf(icon, u * 1.18f, v * 1.18f) / 1.18f;
                        float halo = Mathf.Clamp01(0.5f - (d - 0.05f) / pix);
                        float fill = Mathf.Clamp01(0.5f - d / pix);
                        c = Color.Lerp(c, bg * 0.6f, halo * 0.6f);
                        var ic = Color.Lerp(col, Color.white, Mathf.Clamp01(-d * 3f) * 0.35f);
                        c = Color.Lerp(c, ic, fill);
                        if (frame > 0) c = bg * 0.5f;
                        px[(oy + y) * N + ox + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
                    }
            }
            icons = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "RP_Piktogramme", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            icons.SetPixels32(px);
            icons.Apply(true);
            return icons;
        }

        // ================================================================== Detailtextur
        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float H01(int x, int y, int seed) { return (Hash(x, y, seed) & 0xFFFFFF) / 16777215f; }

        static float Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            return H01(x, y, seed);
        }

        static float Periodic(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0, y0, period, seed), b = Lattice(x0 + 1, y0, period, seed);
            float c = Lattice(x0, y0 + 1, period, seed), d = Lattice(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int per = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Periodic(u, v, per, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                per *= 2;
            }
            return sum / norm;
        }

        /// <summary>Abstand zur nächsten Zellgrenze eines kachelbaren Voronoi-Musters (F2 − F1) – für Risse.</summary>
        static float VoronoiEdge(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 1e9f, f2 = 1e9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    float px = gx + Lattice(gx, gy, period, seed) * 0.9f + 0.05f, py = gy + Lattice(gx, gy, period, seed + 5) * 0.9f + 0.05f;
                    float d = Len(px - x, py - y);
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
            return (f2 - f1) / period;
        }

        static byte B(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255); }

        /// <summary>
        /// Kachelbare Detailtextur (256²): R = Flecken (fbm), G = feine Körnung/Poren, B = Linien (1 = frei, 0 = Riss/Kratzer),
        /// A = Masken (Rost, Wasserflecken, Farbflecken; grob, kontrastreich).
        /// </summary>
        public static Texture2D DetailTex()
        {
            if (detail != null) return detail;
            const int N = 256;
            var lines = new float[N * N];
            for (int i = 0; i < lines.Length; i++) lines[i] = 1f;
            // Risse: verwirbeltes Voronoi-Netz, stellenweise unterbrochen
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float wu = u + (Fbm(u, v, 4, 3, 301) - 0.5f) * 0.08f, wv = v + (Fbm(u, v, 4, 3, 307) - 0.5f) * 0.08f;
                    float e = VoronoiEdge(wu, wv, 5, 311);
                    float present = Mathf.Clamp01((Fbm(u, v, 3, 3, 317) - 0.42f) * 6f);
                    float crack = Mathf.Clamp01(1f - e / 0.006f) * present;
                    lines[y * N + x] = 1f - crack * 0.95f;
                }
            // Kratzer: kurze, gerade Linien in Vorzugsrichtungen (Nahtstellen der Kachel werden umlaufend gezeichnet)
            var rnd = new System.Random(4242);
            for (int k = 0; k < 90; k++)
            {
                float ax = (float)rnd.NextDouble() * N, ay = (float)rnd.NextDouble() * N;
                float ang = (float)(rnd.NextDouble() < 0.6 ? rnd.NextDouble() * 0.5 - 0.25 : rnd.NextDouble() * Math.PI);
                float len = 6f + (float)rnd.NextDouble() * 38f, strength = 0.35f + (float)rnd.NextDouble() * 0.5f;
                float bx = ax + Mathf.Cos(ang) * len, by = ay + Mathf.Sin(ang) * len;
                int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx)) - 2, x1 = Mathf.CeilToInt(Mathf.Max(ax, bx)) + 2;
                int y0 = Mathf.FloorToInt(Mathf.Min(ay, by)) - 2, y1 = Mathf.CeilToInt(Mathf.Max(ay, by)) + 2;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Seg(x + 0.5f, y + 0.5f, ax, ay, bx, by, 0f);
                        float a = Mathf.Clamp01(1.2f - d) * strength;
                        if (a <= 0) continue;
                        int ix = ((x % N) + N) % N, iy = ((y % N) + N) % N;
                        lines[iy * N + ix] = Mathf.Min(lines[iy * N + ix], 1f - a);
                    }
            }
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float r = (Fbm(u, v, 4, 5, 11) - 0.5f) * 2.0f + 0.5f;
                    float g = (Fbm(u, v, 32, 2, 23) - 0.5f) * 1.6f + 0.5f + (H01(x, y, 29) - 0.5f) * 0.35f;
                    float a = (Fbm(u, v, 3, 4, 53) - 0.5f) * 2.4f + 0.5f;
                    px[y * N + x] = new Color32(B(r), B(g), B(lines[y * N + x]), B(a));
                }
            detail = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "RP_Oberflaeche", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            detail.SetPixels32(px);
            detail.Apply(true, false);
            return detail;
        }
    }
}
