using System.Collections.Generic;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Optik von MIKOs Feldtablet (Spielmenü) und Schrift-Vorbereitung gegen Flackern:
    /// Gehäuse, Bildschirm, Scanlinien, Rauschen, Glasreflex, Schrauben, Karten, App-Symbole und Stile.
    /// Alle Texturen entstehen einmal in <see cref="BuildTablet"/> (bei Moduswechsel neu) – keine Texturen pro Bild.
    /// </summary>
    public static partial class UISkin
    {
        // ------------------------------------------------------------ Tablet-Texturen
        /// <summary>Gehäuse (9-Slice, Rand <see cref="BodyBorder"/>), weicher Schatten, Bildschirm (Rand <see cref="ScreenBorder"/>).</summary>
        public static Texture2D TabletBody, TabletShadow, TabletScreen, TabletScan, TabletNoise, TabletGlass, TabletScrew, Card, CardHi;
        public const int BodyBorder = 30, ShadowBorder = 30, ScreenBorder = 18;
        /// <summary>Tablet-Stile: Reiterbeschriftung, Statusleiste, Abschnittsüberschrift, Gravur im Gehäuse.</summary>
        public static GUIStyle TabLabel, TabLabelSel, StatusText, StatusBold, SectionHead, BezelMark;

        /// <summary>
        /// Globale Deckkraft (0–1) für die Zeichenhilfen (Rect, Tex, Sliced, RoundRect, OutlineRect, Bar, MaterialIcon).
        /// Das Tablet blendet damit Inhalte beim Hochfahren und Reiterwechsel ein; außerhalb immer 1.
        /// </summary>
        public static float Fade = 1f;

        public static Color Faded(Color c) { if (Fade < 1f) c.a *= Fade; return c; }

        // ------------------------------------------------------------ Schriftatlas (Flackern)
        /// <summary>Neuaufbauten der dynamischen Schriftatlanten seit Start (Leistungsanzeige; jeder kann ein Bild mit falschen Glyphen zeigen).</summary>
        public static int FontRebuilds { get; private set; }
        static bool rebuildHooked;
        static readonly HashSet<long> prewarmed = new HashSet<long>();

        static void HookFontRebuilds()
        {
            if (rebuildHooked) return;
            rebuildHooked = true;
            Font.textureRebuilt += f => FontRebuilds++;
        }

        /// <summary>
        /// Fordert Zeichen einer Schrift in fester Größe vorab an (einmal je Schrift/Größe/Text). IMGUI fordert Glyphen erst
        /// beim Zeichnen an; läuft der Atlas dabei über, wird er mitten im Bild neu aufgebaut und bereits gezeichneter Text
        /// dieses Bildes zeigt kurz falsche Glyphen. Vorab angefordert passiert das höchstens beim ersten Aufruf.
        /// </summary>
        public static void Prewarm(Font f, string chars, int size, FontStyle style = FontStyle.Normal)
        {
            if (f == null || string.IsNullOrEmpty(chars) || size <= 0) return;
            long key = ((long)f.GetInstanceID() << 32) ^ ((long)size << 20) ^ ((long)style << 16) ^ (uint)chars.GetHashCode();
            if (!prewarmed.Add(key)) return;
            try { f.RequestCharactersInTexture(chars, size, style); }
            catch (System.Exception) { }
        }

        /// <summary>Alle Buchstaben, Ziffern und Satzzeichen der Menütexte (Deutsch/Englisch), für Prewarm.</summary>
        public const string MenuGlyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜabcdefghijklmnopqrstuvwxyzäöüß0123456789 :.,-–'’!?()/·";
        public const string MenuGlyphsUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜ0123456789 :.,-–'’!?()/·";

        // ------------------------------------------------------------ Aufbau
        static void BuildTablet(bool hc)
        {
            HookFontRebuilds();
            if (hc)
            {
                TabletBody = Rounded(Color.black, Color.white, 22, 3f, 64);
                TabletScreen = Rounded(Color.black, new Color(0.75f, 0.75f, 0.75f), 12, 2f, 48);
                Card = Rounded(Color.black, new Color(0.7f, 0.7f, 0.7f), 9, 1.5f);
                CardHi = Rounded(Hex(0x1A1A00), Hex(0xFFE000), 9, 2.5f);
            }
            else
            {
                // Gehäuse: Petrol, oben heller, unten dunkler (Verlauf in der Textur – 9-Slice streckt ihn stetig mit)
                TabletBody = Shaded(64, 22, Hex(0x1C525C), Hex(0x0B2A31), Hex(0x3FD6C8, 0.55f), 2f);
                TabletScreen = Shaded(48, 13, Hex(0x0A2A31, 0.97f), Hex(0x03100F, 0.98f), Hex(0x000000, 0.9f), 2.5f);
                Card = Rounded(Hex(0x0F3139, 0.8f), Hex(0x2EC4B6, 0.2f), 9, 1.2f);
                CardHi = Rounded(Hex(0x173F47, 0.88f), Hex(0xFF8C2E, 0.8f), 9, 1.6f);
            }
            TabletShadow = MakeSoftShadow();
            TabletScan = MakeScanlines();
            TabletNoise = MakeNoise();
            TabletGlass = MakeGlass();
            TabletScrew = MakeScrew();
            slicedB.Clear();

            Font bold = MenuFontBold, med = MenuFont;
            TabLabel = Style(FontTiny + 1, TextDim, TextAnchor.MiddleCenter, false);
            TabLabel.richText = false;
            if (bold != null) TabLabel.font = bold; else TabLabel.fontStyle = FontStyle.Bold;
            TabLabelSel = new GUIStyle(TabLabel);
            TabLabelSel.normal.textColor = Text;
            StatusText = Style(FontTiny + 1, TextDim, TextAnchor.MiddleLeft, false);
            if (med != null) StatusText.font = med;
            StatusBold = Style(FontTiny + 1, Text, TextAnchor.MiddleLeft, false);
            if (bold != null) StatusBold.font = bold; else StatusBold.fontStyle = FontStyle.Bold;
            SectionHead = Style(FontH3 - 2, hc ? Accent : Text, TextAnchor.MiddleLeft, false);
            if (bold != null) SectionHead.font = bold; else SectionHead.fontStyle = FontStyle.Bold;
            BezelMark = Style(FontTiny - 3, new Color(TextDim.r, TextDim.g, TextDim.b, 0.4f), TextAnchor.MiddleCenter, false);
            BezelMark.richText = false;
            if (bold != null) BezelMark.font = bold; else BezelMark.fontStyle = FontStyle.Bold;
        }

        /// <summary>Abgerundetes Rechteck mit senkrechtem Verlauf (oben → unten) und Kontur.</summary>
        static Texture2D Shaded(int size, int radius, Color top, Color bottom, Color border, float borderW)
        {
            var t = NewTex(size, size);
            var px = new Color[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                Color fill = Color.Lerp(bottom, top, (y + 0.5f) / size); // y = 0 unten
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, r, size - r), cy = Mathf.Clamp(fy, r, size - r);
                    float dist = r - Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float a = Mathf.Clamp01(dist + 0.5f);
                    Color c = Color.Lerp(fill, border, Mathf.Clamp01(borderW - dist + 0.5f));
                    // feine Lichtkante oben innen
                    if (y > size - radius && dist > borderW && dist < borderW + 1.5f) c = Color.Lerp(c, Color.white, 0.12f);
                    c.a *= a;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeSoftShadow()
        {
            const int s = 64; const float inner = 30f, fall = 22f;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, inner, s - inner), cy = Mathf.Clamp(fy, inner, s - inner);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy)); // 0 innen
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - (inner - fall)) / fall));
                    px[y * s + x] = new Color(0f, 0f, 0f, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Scanlinien: 1×4 Texel, weiches Profil (kein Moiré bei gebrochener Oberflächenskalierung), kachelbar.</summary>
        static Texture2D MakeScanlines()
        {
            var t = NewTex(1, 4);
            t.wrapMode = TextureWrapMode.Repeat;
            t.SetPixels(new[] { new Color(0, 0, 0, 0.15f), new Color(0, 0, 0, 0.6f), new Color(0, 0, 0, 1f), new Color(0, 0, 0, 0.6f) });
            t.Apply();
            return t;
        }

        /// <summary>Feines, festes Rauschen (deterministisch, kachelbar) – bewegt sich nicht, flackert also nicht.</summary>
        static Texture2D MakeNoise()
        {
            const int s = 128;
            var t = NewTex(s, s);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Point;
            var px = new Color[s * s];
            uint h = 0x9E3779B9u;
            for (int i = 0; i < px.Length; i++)
            {
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                float n = (h & 0xFFFF) / 65535f;
                px[i] = new Color(1f, 1f, 1f, n);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Glasreflex: schräges Lichtband oben links plus schwacher Schimmer in der oberen Ecke.</summary>
        static Texture2D MakeGlass()
        {
            const int s = 128;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s; // v = 1 oben
                    float d = (u + (1f - v)) * 0.5f;           // 0 = Ecke oben links
                    float band = Mathf.Exp(-Mathf.Pow((d - 0.27f) / 0.07f, 2f)) * 0.75f;
                    float band2 = Mathf.Exp(-Mathf.Pow((d - 0.37f) / 0.025f, 2f)) * 0.35f;
                    float corner = (1f - Mathf.SmoothStep(0f, 0.3f, d)) * 0.45f;
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band + band2 + corner));
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Kreuzschlitz-Schraube (Metall, leicht schattiert).</summary>
        static Texture2D MakeScrew()
        {
            const int s = 32;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            float c = s * 0.5f, r = s * 0.5f - 1.5f;
            float ca = Mathf.Cos(0.35f), sa = Mathf.Sin(0.35f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    float light = 0.72f + 0.28f * Mathf.Clamp((dx * -0.5f + dy * 0.7f) / r, -1f, 1f);
                    Color col = new Color(0.62f, 0.7f, 0.71f) * light;
                    if (d > r - 2.2f) col *= 0.55f; // dunkler Rand
                    float rx = dx * ca - dy * sa, ry = dx * sa + dy * ca;
                    bool slot = (Mathf.Abs(rx) < 1.3f || Mathf.Abs(ry) < 1.3f) && d < r * 0.62f;
                    if (slot) col = new Color(0.12f, 0.16f, 0.17f);
                    col.a = a;
                    px[y * s + x] = col;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------ Zeichnen
        static readonly Dictionary<Texture2D, GUIStyle> slicedB = new Dictionary<Texture2D, GUIStyle>();

        /// <summary>9-Slice mit eigener Randbreite (für große Radien: Gehäuse, Bildschirm, Schatten).</summary>
        public static void SlicedB(Rect r, Texture2D t, int border, Color tint)
        {
            if (Event.current.type != EventType.Repaint || t == null) return;
            GUIStyle st;
            if (!slicedB.TryGetValue(t, out st) || st == null)
            {
                st = new GUIStyle { border = new RectOffset(border, border, border, border) };
                st.normal.background = t;
                slicedB[t] = st;
            }
            var old = GUI.color;
            GUI.color = Faded(tint);
            st.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Kachelnde Textur (Scanlinien, Rauschen) mit Texelgröße tile (virtuelle Pixel).</summary>
        public static void Tiled(Rect r, Texture2D t, float tileW, float tileH, Color tint)
        {
            if (Event.current.type != EventType.Repaint || t == null) return;
            var old = GUI.color;
            GUI.color = Faded(tint);
            GUI.DrawTextureWithTexCoords(r, t, new Rect(0f, 0f, r.width / Mathf.Max(1f, tileW), r.height / Mathf.Max(1f, tileH)), true);
            GUI.color = old;
        }

        // ------------------------------------------------------------ App-Symbole (weiß, eingefärbt beim Zeichnen)
        static bool TabletShapeHit(string shape, float u, float v, out bool hit)
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            float r = Mathf.Sqrt(u * u + v * v);
            switch (shape)
            {
                case "bag": // Rucksack
                    {
                        bool body = au < 0.62f && v > -0.85f && v < 0.36f && !(au > 0.42f && v < -0.65f && (au - 0.42f) * (au - 0.42f) + (v + 0.65f) * (v + 0.65f) > 0.04f);
                        bool seam = au < 0.5f && Mathf.Abs(v - 0.02f) < 0.05f;
                        bool pocketInner = au < 0.27f && v > -0.59f && v < -0.27f;
                        bool pocketOuter = au < 0.34f && v > -0.66f && v < -0.2f;
                        float hr = Mathf.Sqrt(u * u + (v - 0.36f) * (v - 0.36f));
                        bool handle = v >= 0.3f && hr < 0.38f && hr > 0.22f;
                        hit = (body && !seam && !(pocketOuter && !pocketInner)) || handle;
                        return true;
                    }
                case "list": // Klemmbrett mit Häkchen-Zeilen
                    {
                        bool board = au < 0.66f && av < 0.86f && !(au < 0.5f && v > -0.72f && v < 0.64f);
                        bool clip = au < 0.26f && v > 0.62f && v < 0.95f;
                        bool lines = false;
                        for (int i = 0; i < 3; i++)
                        {
                            float ly = 0.34f - i * 0.36f;
                            if (Mathf.Abs(v - ly) < 0.065f && u > -0.12f && u < 0.36f) lines = true;
                            if (Mathf.Sqrt((u + 0.29f) * (u + 0.29f) + (v - ly) * (v - ly)) < 0.1f) lines = true;
                        }
                        hit = board || clip || lines;
                        return true;
                    }
                case "pin": // Kartennadel
                    {
                        float cr = Mathf.Sqrt(u * u + (v - 0.28f) * (v - 0.28f));
                        bool head = cr < 0.52f && cr > 0.22f;
                        bool tip = v < 0.28f && v > -0.9f && au < (v + 0.9f) * 0.44f && cr > 0.22f;
                        hit = head || tip;
                        return true;
                    }
                case "crate": // Lagerkiste mit Latte
                    {
                        bool box = au < 0.8f && av < 0.66f;
                        bool inner = au < 0.64f && av < 0.5f;
                        bool diag = inner && Mathf.Abs(u * 0.78f - v) < 0.1f;
                        hit = (box && !inner) || diag;
                        return true;
                    }
                case "trophy": // Pokal
                    {
                        bool bowl = v > -0.05f && v < 0.82f && au < 0.24f + 0.34f * Mathf.Sqrt(Mathf.Clamp01((v + 0.05f) / 0.87f));
                        float hx = au - 0.56f, hy = v - 0.45f;
                        float hd = Mathf.Sqrt(hx * hx + hy * hy);
                        bool handle = au > 0.5f && hd < 0.24f && hd > 0.12f;
                        bool stem = au < 0.09f && v > -0.5f && v <= -0.05f;
                        bool foot = au < 0.42f && v > -0.82f && v < -0.52f;
                        hit = bowl || handle || stem || foot;
                        return true;
                    }
                case "robot": // MIKO-Kopf
                    {
                        bool head = au < 0.66f && v > -0.42f && v < 0.42f && !(au > 0.5f && av > 0.28f && Mathf.Sqrt((au - 0.5f) * (au - 0.5f) + (av - 0.28f) * (av - 0.28f)) > 0.16f);
                        float ex = au - 0.28f, ey = v - 0.02f;
                        bool eye = ex * ex + ey * ey < 0.15f * 0.15f;
                        bool mouth = au < 0.2f && Mathf.Abs(v + 0.25f) < 0.045f;
                        bool antenna = au < 0.055f && v >= 0.42f && v < 0.7f;
                        bool ball = Mathf.Sqrt(u * u + (v - 0.78f) * (v - 0.78f)) < 0.12f;
                        bool ears = au > 0.66f && au < 0.8f && av < 0.18f;
                        bool neck = au < 0.34f && v < -0.5f && v > -0.86f;
                        hit = (head && !eye && !mouth) || antenna || ball || ears || neck;
                        return true;
                    }
                case "radio":
                    {
                        bool body = au < 0.82f && v > -0.72f && v < 0.3f;
                        bool bodyIn = au < 0.68f && v > -0.58f && v < 0.16f;
                        float sx = u + 0.3f, sy = v + 0.21f;
                        float sd = Mathf.Sqrt(sx * sx + sy * sy);
                        bool speaker = sd < 0.25f && (sd > 0.16f || sd < 0.07f);
                        bool dial = u > 0.14f && u < 0.56f && (Mathf.Abs(v - 0.0f) < 0.045f || Mathf.Abs(v + 0.2f) < 0.045f || Mathf.Abs(v + 0.4f) < 0.045f);
                        // Antenne: Linie von (0,35 | 0,3) nach (0,72 | 0,9)
                        float ax = u - 0.35f, ay = v - 0.3f;
                        float along = (ax * 0.37f + ay * 0.6f) / 0.4933f;
                        float across = Mathf.Abs(ax * 0.6f - ay * 0.37f) / 0.705f;
                        bool antenna = along > 0f && along < 0.7f && across < 0.05f;
                        hit = (body && !bodyIn) || speaker || dial || antenna;
                        return true;
                    }
                case "people": // zwei Personen (Koop)
                    {
                        bool h1 = Mathf.Sqrt((u + 0.3f) * (u + 0.3f) + (v - 0.32f) * (v - 0.32f)) < 0.22f;
                        float b1 = Mathf.Sqrt((u + 0.3f) * (u + 0.3f) + (v + 0.52f) * (v + 0.52f));
                        bool s1 = v > -0.6f && v < -0.02f && b1 < 0.46f;
                        bool h2 = Mathf.Sqrt((u - 0.34f) * (u - 0.34f) + (v - 0.2f) * (v - 0.2f)) < 0.19f;
                        float b2 = Mathf.Sqrt((u - 0.34f) * (u - 0.34f) + (v + 0.6f) * (v + 0.6f));
                        bool s2 = v > -0.68f && v < -0.12f && b2 < 0.42f && u > 0.02f;
                        hit = h1 || s1 || h2 || (s2 && !(b1 < 0.54f && u < 0.16f));
                        return true;
                    }
                case "coin":
                    hit = r < 0.82f && !(r < 0.6f && r > 0.48f);
                    return true;
                case "signal": // vier aufsteigende Balken
                    {
                        hit = false;
                        for (int i = 0; i < 4; i++)
                        {
                            float x0 = -0.84f + i * 0.44f;
                            if (u > x0 && u < x0 + 0.3f && v > -0.8f && v < -0.8f + 0.4f * (i + 1)) hit = true;
                        }
                        return true;
                    }
            }
            hit = false;
            return false;
        }
    }
}
