using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Eigene IMGUI-Optik aus prozeduralen Texturen: abgerundete, halbtransparente Glas-Panels in Petrol/Türkis,
    /// orange Akzente. Hoher-Kontrast-Modus mit schwarzem Grund, weißer Schrift und gelben Rahmen.
    /// Texturen und Stile werden einmal erzeugt und nur bei Moduswechsel neu gebaut.
    /// </summary>
    public static partial class UISkin
    {
        // ------------------------------------------------------------ Farben
        public static Color Text, TextDim, Accent, Teal, Good, Warn, Bad, Story, PanelCol, FocusCol, BarBack;
        public static bool Contrast { get; private set; }

        // ------------------------------------------------------------ Texturen
        public static Texture2D White, Panel, PanelLight, PanelDark, Btn, BtnHover, BtnActive, BtnOff, BtnSel, Outline, Round, Circle, Ring, Vignette, Arrow, EdgeGlow, BarTex;
        static readonly Dictionary<string, Texture2D> shapes = new Dictionary<string, Texture2D>();

        // ------------------------------------------------------------ Stile
        public static GUIStyle Label, LabelSmall, LabelTiny, LabelBold, LabelCenter, LabelRight, H1, H2, H3, Title, Subtitle, Button, ButtonSmall, ButtonSel, ButtonOff, Tab, TabSel,
            PanelStyle, PanelLightStyle, Field, Wrap, WrapSmall, WrapCenter, Toast, Mono, Key;

        // ------------------------------------------------------------ Hauptmenü-Schriften (Resources/Fonts, SIL Open Font License)
        /// <summary>Logo-Schrift „RePlanet Logo“ (aus Orbitron abgeleitet) und Exo 2 für Menü-Knöpfe; null = Unity-Standardschrift.</summary>
        public static Font LogoFont, MenuFont, MenuFontBold;
        public static GUIStyle Logo, Tagline, MenuButton, MenuButtonSel, MenuButtonOff;
        static bool fontsLoaded;
        static readonly Dictionary<char, string> charStr = new Dictionary<char, string>();

        static bool built;
        static bool builtContrast;

        public const int FontNormal = 20, FontSmall = 17, FontTiny = 14, FontH3 = 22, FontH2 = 26, FontH1 = 34;

        public static void Ensure(bool highContrast)
        {
            if (built && builtContrast == highContrast && White != null) return;
            Build(highContrast);
        }

        static Color Hex(uint rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }

        public static Color FromRgb(uint rgb, float a = 1f) { return Hex(rgb, a); }

        static void Build(bool hc)
        {
            built = true; builtContrast = hc; Contrast = hc;
            if (hc)
            {
                Text = Color.white; TextDim = new Color(0.9f, 0.9f, 0.9f); Accent = Hex(0xFFE000); Teal = Hex(0x00FFFF);
                Good = Hex(0x66FF66); Warn = Hex(0xFFE000); Bad = Hex(0xFF5050); Story = Hex(0xFF9CFF);
                PanelCol = new Color(0, 0, 0, 0.96f); FocusCol = Hex(0xFFE000); BarBack = new Color(0.25f, 0.25f, 0.25f, 1f);
            }
            else
            {
                Text = Hex(0xEAF6F4); TextDim = Hex(0xA3C7C3); Accent = Hex(0xFF8C2E); Teal = Hex(0x2EC4B6);
                Good = Hex(0x6FE3A1); Warn = Hex(0xFFC15A); Bad = Hex(0xFF6A5C); Story = Hex(0xC9A7FF);
                PanelCol = Hex(0x0C262C, 0.86f); FocusCol = Hex(0xFF8C2E); BarBack = Hex(0x082026, 0.85f);
            }

            White = Solid(Color.white);
            BarTex = Rounded(Color.white, Color.clear, 6, 0f, 16);
            if (hc)
            {
                Panel = Rounded(new Color(0, 0, 0, 0.96f), Color.white, 10, 2f);
                PanelLight = Rounded(new Color(0.08f, 0.08f, 0.08f, 0.97f), new Color(0.85f, 0.85f, 0.85f), 10, 2f);
                PanelDark = Rounded(new Color(0, 0, 0, 0.98f), new Color(0.6f, 0.6f, 0.6f), 10, 1.5f);
                Btn = Rounded(new Color(0.05f, 0.05f, 0.05f, 1f), Color.white, 8, 2f);
                BtnHover = Rounded(new Color(0.2f, 0.2f, 0.2f, 1f), Hex(0xFFE000), 8, 3f);
                BtnActive = Rounded(Hex(0x333300), Hex(0xFFE000), 8, 3f);
                BtnOff = Rounded(new Color(0.1f, 0.1f, 0.1f, 1f), new Color(0.5f, 0.5f, 0.5f), 8, 2f);
                BtnSel = Rounded(Hex(0x3A3A00), Hex(0xFFE000), 8, 3f);
                Outline = Rounded(Color.clear, Hex(0xFFE000), 10, 4f);
            }
            else
            {
                Panel = Rounded(Hex(0x0B2429, 0.86f), Hex(0x2EC4B6, 0.35f), 12, 1.5f);
                PanelLight = Rounded(Hex(0x15414A, 0.82f), Hex(0x2EC4B6, 0.28f), 10, 1.2f);
                PanelDark = Rounded(Hex(0x061519, 0.9f), Hex(0x2EC4B6, 0.2f), 10, 1.2f);
                Btn = Rounded(Hex(0x174650, 0.94f), Hex(0x2EC4B6, 0.45f), 9, 1.5f);
                BtnHover = Rounded(Hex(0x21616D, 0.97f), Hex(0x6FF0E4, 0.85f), 9, 2f);
                BtnActive = Rounded(Hex(0x2A8C87, 1f), Hex(0xFF8C2E, 1f), 9, 2f);
                BtnOff = Rounded(Hex(0x14292D, 0.8f), Hex(0x3C5A5E, 0.5f), 9, 1.2f);
                BtnSel = Rounded(Hex(0x5A3514, 0.95f), Hex(0xFF8C2E, 1f), 9, 2f);
                Outline = Rounded(Color.clear, Hex(0xFF8C2E), 11, 3f);
            }
            Round = Rounded(Color.white, Color.clear, 10, 0f);
            Circle = MakeCircle(64, false);
            Ring = MakeCircle(64, true);
            Vignette = MakeVignette();
            EdgeGlow = MakeEdgeGlow();
            Arrow = MakeArrow();
            shapes.Clear();
            slicedStyles.Clear();
            barStyle = outlineStyle = roundStyle = null;

            Label = Style(FontNormal, Text, TextAnchor.MiddleLeft, false);
            LabelSmall = Style(FontSmall, TextDim, TextAnchor.MiddleLeft, false);
            LabelTiny = Style(FontTiny, TextDim, TextAnchor.MiddleLeft, false);
            LabelBold = Style(FontNormal, Text, TextAnchor.MiddleLeft, false); LabelBold.fontStyle = FontStyle.Bold;
            LabelCenter = Style(FontNormal, Text, TextAnchor.MiddleCenter, false);
            LabelRight = Style(FontNormal, Text, TextAnchor.MiddleRight, false);
            H3 = Style(FontH3, Accent, TextAnchor.MiddleLeft, false); H3.fontStyle = FontStyle.Bold;
            H2 = Style(FontH2, Text, TextAnchor.MiddleLeft, false); H2.fontStyle = FontStyle.Bold;
            H1 = Style(FontH1, Text, TextAnchor.MiddleCenter, false); H1.fontStyle = FontStyle.Bold;
            Title = Style(110, Text, TextAnchor.MiddleCenter, false); Title.fontStyle = FontStyle.Bold;
            Subtitle = Style(34, Accent, TextAnchor.MiddleCenter, false); Subtitle.fontStyle = FontStyle.Italic;
            LoadFonts();
            Logo = Style(110, Text, TextAnchor.MiddleLeft, false);
            Logo.richText = false;
            if (LogoFont != null) Logo.font = LogoFont; else Logo.fontStyle = FontStyle.Bold;
            Tagline = Style(20, TextDim, TextAnchor.MiddleLeft, false);
            Tagline.richText = false;
            if (MenuFont != null) Tagline.font = MenuFont;
            Wrap = Style(FontNormal, Text, TextAnchor.UpperLeft, true);
            WrapSmall = Style(FontSmall, TextDim, TextAnchor.UpperLeft, true);
            WrapCenter = Style(FontNormal, Text, TextAnchor.UpperCenter, true);
            Toast = Style(FontSmall + 1, Text, TextAnchor.MiddleLeft, true);
            Mono = Style(FontSmall, Text, TextAnchor.MiddleLeft, false);
            Key = Style(FontTiny, hc ? Color.black : Hex(0x0B2429), TextAnchor.MiddleCenter, false);
            Key.fontStyle = FontStyle.Bold;
            Key.normal.background = Rounded(hc ? Color.white : Hex(0xEAF6F4, 0.92f), Color.clear, 5, 0f, 16);
            Key.border = new RectOffset(6, 6, 6, 6);
            Key.padding = new RectOffset(5, 5, 1, 1);

            Button = ButtonStyle(Btn, BtnHover, BtnActive, FontNormal);
            ButtonSmall = ButtonStyle(Btn, BtnHover, BtnActive, FontSmall);
            ButtonSel = ButtonStyle(BtnSel, BtnSel, BtnActive, FontNormal);
            ButtonOff = ButtonStyle(BtnOff, BtnOff, BtnOff, FontNormal);
            ButtonOff.normal.textColor = ButtonOff.hover.textColor = ButtonOff.active.textColor = new Color(TextDim.r, TextDim.g, TextDim.b, 0.75f);
            // Hauptmenü-Knöpfe: kantiger, dunkles Glas mit feiner Türkis-Kontur (Hoher Kontrast: normale Knopf-Texturen)
            if (hc)
            {
                MenuButton = ButtonStyle(Btn, BtnHover, BtnActive, FontNormal + 2);
                MenuButtonSel = ButtonStyle(BtnSel, BtnSel, BtnActive, FontNormal + 2);
                MenuButtonOff = ButtonStyle(BtnOff, BtnOff, BtnOff, FontNormal + 2);
            }
            else
            {
                MenuButton = ButtonStyle(Rounded(Hex(0x06181D, 0.66f), Hex(0x2EC4B6, 0.32f), 4, 1.2f), Rounded(Hex(0x0E3A43, 0.9f), Hex(0x6FF0E4, 0.95f), 4, 1.6f),
                    Rounded(Hex(0x1F7470, 0.96f), Hex(0xFF8C2E, 1f), 4, 2f), FontNormal + 2);
                MenuButtonSel = ButtonStyle(Rounded(Hex(0x4A2A10, 0.92f), Hex(0xFF8C2E, 1f), 4, 1.8f), Rounded(Hex(0x6A3C16, 0.96f), Hex(0xFFB070, 1f), 4, 2f),
                    Rounded(Hex(0x8A4E1C, 1f), Hex(0xFFD0A0, 1f), 4, 2f), FontNormal + 2);
                MenuButtonOff = ButtonStyle(Rounded(Hex(0x06161A, 0.45f), Hex(0x3C5A5E, 0.3f), 4, 1f), Rounded(Hex(0x06161A, 0.45f), Hex(0x3C5A5E, 0.3f), 4, 1f),
                    Rounded(Hex(0x06161A, 0.45f), Hex(0x3C5A5E, 0.3f), 4, 1f), FontNormal + 2);
                MenuButtonOff.normal.textColor = MenuButtonOff.hover.textColor = new Color(TextDim.r, TextDim.g, TextDim.b, 0.45f);
            }
            foreach (var mb in new[] { MenuButton, MenuButtonSel, MenuButtonOff })
            {
                mb.border = new RectOffset(6, 6, 6, 6);
                if (MenuFontBold != null) mb.font = MenuFontBold; else mb.fontStyle = FontStyle.Bold;
            }

            Tab = ButtonStyle(PanelDark, BtnHover, BtnActive, FontSmall + 1);
            TabSel = ButtonStyle(BtnSel, BtnSel, BtnActive, FontSmall + 1);
            TabSel.fontStyle = FontStyle.Bold;

            PanelStyle = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
            PanelStyle.normal.background = Panel;
            PanelLightStyle = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
            PanelLightStyle.normal.background = PanelLight;

            Field = new GUIStyle(GUI.skin != null ? GUI.skin.textField : new GUIStyle());
            Field.fontSize = FontNormal;
            Field.normal.background = PanelDark; Field.hover.background = PanelLight; Field.focused.background = BtnHover; Field.active.background = BtnHover;
            Field.onNormal.background = PanelDark; Field.onFocused.background = BtnHover;
            Field.normal.textColor = Text; Field.hover.textColor = Text; Field.focused.textColor = Text; Field.active.textColor = Text;
            Field.border = new RectOffset(10, 10, 10, 10);
            Field.padding = new RectOffset(10, 10, 6, 6);
            Field.alignment = TextAnchor.MiddleLeft;
            Field.clipping = TextClipping.Clip;
            BuildTablet(hc);
        }

        static void LoadFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            LogoFont = Resources.Load<Font>("Fonts/RePlanetLogo-ExtraBold");
            MenuFont = Resources.Load<Font>("Fonts/Exo2-Medium");
            MenuFontBold = Resources.Load<Font>("Fonts/Exo2-Bold");
            if (LogoFont == null || MenuFont == null || MenuFontBold == null)
                Debug.Log("[RE:PLANET] Menü-Schriften nicht gefunden (Resources/Fonts) – Standardschrift wird benutzt.");
        }

        /// <summary>Einzelnes Zeichen als (zwischengespeicherter) String, für gesperrten Text ohne Speicher-Müll.</summary>
        public static string Chr(char c)
        {
            string s;
            if (!charStr.TryGetValue(c, out s)) { s = c.ToString(); charStr[c] = s; }
            return s;
        }

        /// <summary>Breite eines Texts mit Sperrung (IMGUI kennt keine Laufweite, daher Zeichen für Zeichen).</summary>
        public static float TrackedWidth(GUIStyle st, string text, float spacing)
        {
            float w = 0f;
            for (int i = 0; i < text.Length; i++) w += st.CalcSize(Tmp(Chr(text[i]))).x + (i < text.Length - 1 ? spacing : 0f);
            return w;
        }

        /// <summary>Gesperrten Text zeichnen (links ab x); gibt die Breite zurück.</summary>
        public static float Tracked(float x, float y, float h, string text, GUIStyle st, float spacing, Color c)
        {
            var old = st.normal.textColor;
            st.normal.textColor = c;
            float x0 = x;
            for (int i = 0; i < text.Length; i++)
            {
                string ch = Chr(text[i]);
                float cw = st.CalcSize(Tmp(ch)).x;
                if (text[i] != ' ') GUI.Label(new Rect(x, y, cw + 4f, h), ch, st);
                x += cw + spacing;
            }
            st.normal.textColor = old;
            return x - x0 - spacing;
        }

        static GUIStyle Style(int size, Color c, TextAnchor a, bool wrap)
        {
            var s = new GUIStyle();
            s.fontSize = size;
            s.normal.textColor = c;
            s.alignment = a;
            s.wordWrap = wrap;
            s.richText = true;
            s.clipping = wrap ? TextClipping.Clip : TextClipping.Overflow;
            return s;
        }

        static GUIStyle ButtonStyle(Texture2D n, Texture2D h, Texture2D a, int size)
        {
            var s = new GUIStyle();
            s.fontSize = size;
            s.alignment = TextAnchor.MiddleCenter;
            s.richText = true;
            s.wordWrap = false;
            s.clipping = TextClipping.Clip;
            s.border = new RectOffset(11, 11, 11, 11);
            s.padding = new RectOffset(10, 10, 4, 4);
            s.normal.background = n; s.normal.textColor = Text;
            s.hover.background = h; s.hover.textColor = Contrast ? Accent : Color.white;
            s.active.background = a; s.active.textColor = Color.white;
            s.focused.background = h; s.focused.textColor = Text;
            return s;
        }

        // ------------------------------------------------------------ Textur-Erzeugung
        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Texture2D Solid(Color c)
        {
            var t = NewTex(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        /// <summary>Abgerundetes Rechteck (9-Slice-fähig) mit weichem Rand und optionaler Kontur.</summary>
        public static Texture2D Rounded(Color fill, Color border, int radius, float borderW, int size = 32)
        {
            var t = NewTex(size, size);
            var px = new Color[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, r, size - r), cy = Mathf.Clamp(fy, r, size - r);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float dist = r - d; // >0 innen
                    float a = Mathf.Clamp01(dist + 0.5f);
                    Color c = fill;
                    if (borderW > 0f)
                    {
                        float bw = Mathf.Clamp01(borderW - dist + 0.5f);
                        c = Color.Lerp(fill, border, bw);
                    }
                    c.a *= a;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeCircle(int size, bool ring)
        {
            var t = NewTex(size, size);
            var px = new Color[size * size];
            float c = size * 0.5f, r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ring) a *= Mathf.Clamp01(d - (r - size * 0.12f) + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeVignette()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d)) * 0.75f;
                    px[y * s + x] = new Color(0.01f, 0.05f, 0.07f, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D MakeEdgeGlow()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = Mathf.Min(x + 0.5f, s - x - 0.5f) / (s * 0.5f), v = Mathf.Min(y + 0.5f, s - y - 0.5f) / (s * 0.5f);
                    float e = Mathf.Min(u, v);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - e * 3.2f), 2f);
                    px[y * s + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Pfeil nach oben (weiß), für Richtungsanzeigen und Kartenmarker.</summary>
        static Texture2D MakeArrow()
        {
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            // Dreieck-Pfeil: Spitze oben, Kerbe unten
            Vector2 a = new Vector2(32, 60), b = new Vector2(8, 6), c = new Vector2(56, 6), n = new Vector2(32, 20);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float alpha = 0f;
                    for (int ss = 0; ss < 4; ss++)
                    {
                        var q = p + new Vector2((ss & 1) * 0.5f - 0.25f, (ss >> 1) * 0.5f - 0.25f);
                        bool inTri = InTri(q, a, b, n) || InTri(q, a, n, c);
                        if (inTri) alpha += 0.25f;
                    }
                    px[y * s + x] = new Color(1, 1, 1, alpha);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) { return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y); }

        /// <summary>Form-Symbol als Textur (weiß, wird eingefärbt). Deckt alle Material- und Kartenformen ab.</summary>
        public static Texture2D Shape(string shape)
        {
            if (shape == null) shape = "circle";
            Texture2D t;
            if (shapes.TryGetValue(shape, out t) && t != null) return t;
            const int s = 48;
            t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float a = 0f;
                    for (int ss = 0; ss < 4; ss++)
                    {
                        float u = (x + 0.25f + (ss & 1) * 0.5f) / s * 2f - 1f;
                        float v = (y + 0.25f + (ss >> 1) * 0.5f) / s * 2f - 1f; // v nach oben
                        if (ShapeHit(shape, u, v)) a += 0.25f;
                    }
                    px[y * s + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            t.Apply();
            shapes[shape] = t;
            return t;
        }

        static bool ShapeHit(string shape, float u, float v)
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            float r = Mathf.Sqrt(u * u + v * v);
            switch (shape)
            {
                case "square": return au < 0.75f && av < 0.75f && (au > 0.57f || av > 0.57f || Mathf.Abs(v - 0.2f) < 0.08f || Mathf.Abs(v + 0.2f) < 0.08f);
                case "circle": return r < 0.82f && r > 0.5f;
                case "triangle":
                    {
                        bool outer = v > -0.7f && av <= 1 && au < (0.85f - v) * 0.55f && v < 0.85f;
                        bool inner = v > -0.42f && au < (0.45f - v) * 0.55f && v < 0.45f;
                        return outer && !inner;
                    }
                case "hexagon":
                    {
                        float hx = Mathf.Max(au * 0.866f + av * 0.5f, av);
                        return hx < 0.82f && hx > 0.52f;
                    }
                case "bar": return au < 0.85f && av < 0.28f;
                case "ring": return (r < 0.85f && r > 0.62f) || r < 0.3f;
                case "chip": return (au < 0.52f && av < 0.52f) || (au < 0.85f && av < 0.4f && au > 0.52f && Mathf.Repeat(v * 4f + 10f, 1f) < 0.5f) || (av < 0.85f && au < 0.4f && av > 0.52f && Mathf.Repeat(u * 4f + 10f, 1f) < 0.5f);
                case "grid": return au < 0.85f && av < 0.85f && (Mathf.Abs(au - 0.36f) < 0.12f || Mathf.Abs(av - 0.36f) < 0.12f);
                case "battery": return (au < 0.45f && v < 0.7f && v > -0.85f && !(au < 0.28f && v < 0.52f && v > -0.1f)) || (au < 0.2f && v >= 0.7f && v < 0.88f);
                case "diamond": return au + av < 0.9f;
                case "hazard":
                    {
                        bool tri = v > -0.72f && au < (0.88f - v) * 0.6f && v < 0.88f;
                        bool bang = (au < 0.09f && v > -0.25f && v < 0.45f) || (au < 0.1f && v > -0.55f && v < -0.37f);
                        return tri && !bang;
                    }
                case "drop":
                    {
                        if (v < 0.05f) return r < 0.62f || Mathf.Sqrt(u * u + (v + 0.1f) * (v + 0.1f)) < 0.62f;
                        return au < (0.9f - v) * 0.62f && v < 0.9f;
                    }
                // Kartenformen
                case "house": return (av < 0.9f && v < 0.1f && v > -0.75f && au < 0.62f && !(au < 0.2f && v < -0.25f)) || (v >= 0.1f && au < (0.9f - v) * 1.0f && v < 0.9f);
                case "star":
                    {
                        float ang = Mathf.Atan2(v, u);
                        float k = Mathf.Cos(5f * (ang - Mathf.PI / 2f));
                        float rr = 0.45f + 0.4f * Mathf.Max(0, k);
                        return r < rr;
                    }
                case "wrench": return (au < 0.14f && av < 0.75f) || (Mathf.Sqrt(u * u + (v - 0.6f) * (v - 0.6f)) < 0.34f && !(au < 0.12f && v > 0.5f));
                case "leaf": return Mathf.Sqrt((u - 0.35f) * (u - 0.35f) + v * v) < 0.75f && Mathf.Sqrt((u + 0.35f) * (u + 0.35f) + v * v) < 0.75f;
                case "book": return au < 0.8f && av < 0.62f && !(au < 0.06f) && !(av < 0.5f && au > 0.12f && au < 0.7f && Mathf.Repeat(v * 5f, 1f) < 0.3f);
                case "eye": return (Mathf.Sqrt(u * u + (v - 0.6f) * (v - 0.6f)) < 1f && Mathf.Sqrt(u * u + (v + 0.6f) * (v + 0.6f)) < 1f) && !(r < 0.42f && r > 0.26f);
                case "flag": return (u > -0.7f && u < -0.55f && av < 0.85f) || (u >= -0.55f && u < 0.7f && v > 0.1f && v < 0.8f);
                case "plus": return (au < 0.2f && av < 0.8f) || (av < 0.2f && au < 0.8f);
                case "dot": return r < 0.8f;
                case "cross": return (Mathf.Abs(u - v) < 0.25f || Mathf.Abs(u + v) < 0.25f) && au < 0.8f && av < 0.8f;
                case "sun":
                    {
                        if (r < 0.42f) return true;
                        float ang = Mathf.Atan2(v, u);
                        return r < 0.85f && r > 0.55f && Mathf.Cos(8f * ang) > 0.6f;
                    }
                case "moon": return r < 0.8f && Mathf.Sqrt((u - 0.35f) * (u - 0.35f) + (v - 0.25f) * (v - 0.25f)) > 0.62f;
                case "gear":
                    {
                        float ang = Mathf.Atan2(v, u);
                        float rr = 0.62f + (Mathf.Cos(8f * ang) > 0.3f ? 0.22f : 0f);
                        return r < rr && r > 0.26f;
                    }
                case "truck": return (u > -0.85f && u < 0.35f && v > -0.4f && v < 0.45f) || (u >= 0.35f && u < 0.85f && v > -0.4f && v < 0.15f) || Mathf.Sqrt((u + 0.5f) * (u + 0.5f) + (v + 0.55f) * (v + 0.55f)) < 0.2f || Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v + 0.55f) * (v + 0.55f)) < 0.2f;
                case "square_full": return au < 0.8f && av < 0.8f;
            }
            bool extra;
            if (TabletShapeHit(shape, u, v, out extra)) return extra; // Tablet-Symbole (UISkinTablet.cs)
            return r < 0.8f;
        }

        // ------------------------------------------------------------ Zeichnen
        public static void Rect(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = Faded(c);
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Tex(Rect r, Texture t, Color c)
        {
            if (Event.current.type != EventType.Repaint || t == null) return;
            var old = GUI.color;
            GUI.color = Faded(c);
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        public static void PanelBox(Rect r) { if (Event.current.type == EventType.Repaint) PanelStyle.Draw(r, false, false, false, false); }
        public static void PanelBoxLight(Rect r) { if (Event.current.type == EventType.Repaint) PanelLightStyle.Draw(r, false, false, false, false); }

        static GUIStyle barStyle, outlineStyle, roundStyle;

        static GUIStyle SlicedStyle(ref GUIStyle st, Texture2D t, int border)
        {
            if (st == null || st.normal.background != t) { st = new GUIStyle { border = new RectOffset(border, border, border, border) }; st.normal.background = t; }
            return st;
        }

        static readonly Dictionary<Texture2D, GUIStyle> slicedStyles = new Dictionary<Texture2D, GUIStyle>();

        /// <summary>Zeichnet eine 9-Slice-Textur (Panel/Knopf-Hintergrund) in einem Rechteck.</summary>
        public static void Sliced(Rect r, Texture2D t) { Sliced(r, t, Color.white); }

        public static void Sliced(Rect r, Texture2D t, Color tint)
        {
            if (Event.current.type != EventType.Repaint || t == null) return;
            GUIStyle st;
            if (!slicedStyles.TryGetValue(t, out st) || st == null)
            {
                st = new GUIStyle { border = new RectOffset(11, 11, 11, 11) };
                st.normal.background = t;
                slicedStyles[t] = st;
            }
            var old = GUI.color;
            GUI.color = Faded(tint);
            st.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Abgerundete, eingefärbte Fläche (9-Slice).</summary>
        public static void RoundRect(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = Faded(c);
            SlicedStyle(ref roundStyle, Round, 10).Draw(r, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Fokusrahmen / Kontur.</summary>
        public static void OutlineRect(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = Faded(c);
            SlicedStyle(ref outlineStyle, Outline, 12).Draw(r, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Fortschrittsbalken mit abgerundeten Enden.</summary>
        public static void Bar(Rect r, float f, Color fill)
        {
            if (Event.current.type != EventType.Repaint) return;
            f = Mathf.Clamp01(f);
            var st = SlicedStyle(ref barStyle, BarTex, 7);
            var old = GUI.color;
            GUI.color = Faded(BarBack);
            st.Draw(r, false, false, false, false);
            if (f > 0.001f)
            {
                GUI.color = Faded(fill);
                float w = r.width * f;
                if (w >= r.height) st.Draw(new Rect(r.x, r.y, w, r.height), false, false, false, false);
                else GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, Mathf.Max(0, w - 2), r.height - 4), White);
            }
            GUI.color = old;
            if (Contrast) OutlineRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(1, 1, 1, 0.8f));
        }

        /// <summary>Material-Kennung: Form-Symbol in Materialfarbe (dunkel umrandet) – nie nur Farbe.</summary>
        public static void MaterialIcon(Rect r, string mat)
        {
            MaterialDef md;
            if (mat == null || !GameData.Materials.TryGetValue(mat, out md)) return;
            if (Event.current.type != EventType.Repaint) return;
            var sh = Shape(md.Shape);
            var old = GUI.color;
            GUI.color = Faded(new Color(0, 0, 0, 0.7f));
            GUI.DrawTexture(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), sh, ScaleMode.ScaleToFit, true);
            GUI.color = Faded(Hex(md.Color));
            if (md.Color == 0x2E2A26) GUI.color = Faded(Hex(0x8A8078)); // Altöl auf dunklem Grund sichtbar machen
            GUI.DrawTexture(r, sh, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        /// <summary>Materialsymbol + Symbolzeichen + Name, z. B. „◯ Glas“. Liefert die verbrauchte Breite.</summary>
        public static float MaterialTag(Rect r, string mat, string suffix = null, GUIStyle st = null)
        {
            MaterialDef md;
            if (mat == null || !GameData.Materials.TryGetValue(mat, out md)) return 0;
            float s = Mathf.Min(r.height, 26f);
            MaterialIcon(new Rect(r.x, r.y + (r.height - s) * 0.5f, s, s), mat);
            var text = md.Symbol + " " + md.Name + (suffix ?? "");
            st = st ?? Label;
            GUI.Label(new Rect(r.x + s + 6, r.y, r.width - s - 6, r.height), text, st);
            return s + 6 + st.CalcSize(Tmp(text)).x;
        }

        static readonly GUIContent tmp = new GUIContent();
        public static GUIContent Tmp(string s) { tmp.text = s; tmp.image = null; tmp.tooltip = null; return tmp; }

        public static float TextWidth(GUIStyle st, string s) { return st.CalcSize(Tmp(s)).x; }
        public static float TextHeight(GUIStyle st, string s, float width) { return st.CalcHeight(Tmp(s), width); }

        /// <summary>Tastensymbol als kleine helle Kappe, z. B. [E].</summary>
        public static float KeyCap(float x, float y, string key, float h = 24f)
        {
            float w = Mathf.Max(h, Key.CalcSize(Tmp(key)).x + 8);
            GUI.Label(new Rect(x, y, w, h), key, Key);
            return w;
        }

        public static string Hexs(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

        /// <summary>Text mit Farbe als Rich-Text.</summary>
        public static string Col(string text, Color c) { return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>"; }

        public static Color ToastColor(ToastKind k)
        {
            switch (k)
            {
                case ToastKind.Success: return Good;
                case ToastKind.Warning: return Warn;
                case ToastKind.Error: return Bad;
                case ToastKind.Story: return Story;
            }
            return Teal;
        }

        /// <summary>Schatten-Text für gute Lesbarkeit über der 3D-Welt.</summary>
        public static void Shadow(Rect r, string text, GUIStyle st)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var c = st.normal.textColor;
                st.normal.textColor = new Color(0, 0, 0, Contrast ? 1f : 0.75f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), StripColor(text), st);
                st.normal.textColor = c;
            }
            GUI.Label(r, text, st);
        }

        static string StripColor(string s)
        {
            if (s == null || s.IndexOf('<') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (var ch in s)
            {
                if (ch == '<') { tag = true; continue; }
                if (ch == '>') { tag = false; continue; }
                if (!tag) sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
