using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Raylib_cs;

namespace Glasscore.Desktop
{
    /// <summary>
    /// Immediate-mode neon UI on a 1920×1080 virtual canvas (scaled to the window). Buttons follow the
    /// spec: hover → tink + 1.05× scale + stronger glow; click → sharp crack + white screen flash.
    /// </summary>
    public static class Ui
    {
        public const float RefHeight = 1080f;
        public static float Scale { get; private set; } = 1f;
        public static float Width { get; private set; } = 1920f;
        public static float Height => RefHeight;
        public static Vector2 Mouse { get; private set; }
        public static bool Clicked { get; private set; }
        public static bool MouseDown { get; private set; }
        public static string Focus;           // text field with keyboard focus
        public static bool Modal;             // disables widgets beneath an overlay

        private static Font _font;
        private static bool _hasFont;
        private static readonly Dictionary<string, float> Anim = new Dictionary<string, float>();
        private static readonly HashSet<string> WasHover = new HashSet<string>();
        private static readonly HashSet<string> HoverThisFrame = new HashSet<string>();
        private static float _flash;
        private static string _dragging;

        public static void LoadFont()
        {
            string[] candidates =
            {
                @"C:\Windows\Fonts\bahnschrift.ttf", @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            };
            var codepoints = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).Concat(new[] { 0x2022, 0x2026, 0x2014, 0x00B7 }).ToArray();
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;
                _font = Raylib.LoadFontEx(path, 96, codepoints, codepoints.Length);
                if (_font.GlyphCount > 0)
                {
                    Raylib.SetTextureFilter(_font.Texture, TextureFilter.Bilinear);
                    _hasFont = true;
                    return;
                }
            }
            _font = Raylib.GetFontDefault();
        }

        public static void BeginFrame()
        {
            Scale = Raylib.GetScreenHeight() / RefHeight;
            Width = Raylib.GetScreenWidth() / Scale;
            Vector2 m = Raylib.GetMousePosition();
            Mouse = new Vector2(m.X / Scale, m.Y / Scale);
            Clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
            MouseDown = Raylib.IsMouseButtonDown(MouseButton.Left);
            if (!MouseDown) _dragging = null;
            HoverThisFrame.Clear();
        }

        public static void EndFrame()
        {
            WasHover.RemoveWhere(id => !HoverThisFrame.Contains(id));
            if (_flash > 0f)
            {
                Rect(0, 0, Width, Height, new Color(255, 255, 255, (int)(_flash * 140)));
                _flash = Math.Max(0f, _flash - Raylib.GetFrameTime() * 6f);
            }
        }

        public static void Flash() => _flash = 1f;

        public static void ClickFeedback()
        {
            Sfx.Play(Sfx.CrackSharp, 0.7f);
            Flash();
        }

        // ── primitives (virtual coordinates) ──

        public static Rectangle R(float x, float y, float w, float h) => new Rectangle(x * Scale, y * Scale, w * Scale, h * Scale);

        public static void Rect(float x, float y, float w, float h, Color c) => Raylib.DrawRectangleRec(R(x, y, w, h), c);

        public static void Frame(float x, float y, float w, float h, Color c, float t = 2f) =>
            Raylib.DrawRectangleLinesEx(R(x, y, w, h), Math.Max(1f, t * Scale), c);

        public static void Panel(float x, float y, float w, float h, string title = null)
        {
            Rect(x, y, w, h, Palette.Panel);
            Frame(x, y, w, h, new Color(0, 255, 255, 90), 1.5f);
            if (title != null) Text(title, x + 18, y + 12, 26, Palette.Cyan);
        }

        public static Vector2 Measure(string text, float size)
        {
            Vector2 v = Raylib.MeasureTextEx(_font, text ?? "", size * Scale, Spacing(size));
            return new Vector2(v.X / Scale, v.Y / Scale);
        }

        private static float Spacing(float size) => _hasFont ? size * Scale * 0.02f : size * Scale * 0.1f;

        public static void Text(string text, float x, float y, float size, Color c, bool glow = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var pos = new Vector2(x * Scale, y * Scale);
            if (glow)
            {
                var g = new Color(c.R, c.G, c.B, (int)(c.A * 0.16f));
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * MathF.PI * 2f;
                    Raylib.DrawTextEx(_font, text, pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * size * 0.07f * Scale, size * Scale, Spacing(size), g);
                }
            }
            Raylib.DrawTextEx(_font, text, pos, size * Scale, Spacing(size), c);
        }

        /// <param name="align">0 = left, 1 = centre, 2 = right (within width w), vertically centred in h.</param>
        public static void TextIn(string text, float x, float y, float w, float h, float size, Color c, int align = 0, bool glow = false)
        {
            Vector2 m = Measure(text, size);
            float tx = align == 0 ? x : align == 1 ? x + (w - m.X) / 2 : x + w - m.X;
            Text(text, tx, y + (h - m.Y) / 2, size, c, glow);
        }

        public static bool Hover(float x, float y, float w, float h) =>
            !Modal && Mouse.X >= x && Mouse.X <= x + w && Mouse.Y >= y && Mouse.Y <= y + h;

        // ── widgets ──

        public static bool Button(string id, float x, float y, float w, float h, string label, float size = 28, bool focused = false, bool enabled = true, Color? accent = null)
        {
            Color acc = accent ?? Palette.Cyan;
            bool hover = enabled && (Hover(x, y, w, h) || focused);
            if (hover)
            {
                HoverThisFrame.Add(id);
                if (!WasHover.Contains(id))
                {
                    WasHover.Add(id);
                    Sfx.Play(Sfx.TinkHigh, 0.35f, 0.95f + (float)Random.Shared.NextDouble() * 0.15f);
                }
            }
            float a = Anim.TryGetValue(id, out float v) ? v : 0f;
            a = MoveTowards(a, hover ? 1f : 0f, Raylib.GetFrameTime() * 8f);
            Anim[id] = a;

            float s = 1f + 0.05f * a;
            float cx = x + w / 2, cy = y + h / 2;
            float sw = w * s, sh = h * s, sx = cx - sw / 2, sy = cy - sh / 2;

            Rect(sx - 4, sy - 4, sw + 8, sh + 8, Palette.WithAlpha(acc, enabled ? (0.04f + 0.12f * a) : 0.02f));
            Rect(sx, sy, sw, sh, new Color((int)(acc.R * 0.22f), (int)(acc.G * 0.22f), (int)(acc.B * 0.22f), (int)(150 + 50 * a)));
            Frame(sx, sy, sw, sh, Palette.WithAlpha(acc, enabled ? 0.45f + 0.55f * a : 0.2f), 2f);
            Color tc = enabled ? new Color(210 + (int)(45 * a), 240 + (int)(15 * a), 255, 255) : new Color(255, 255, 255, 80);
            TextIn(label, sx, sy, sw, sh, size * s, tc, 1);

            bool clicked = enabled && !Modal && Clicked && Hover(x, y, w, h);
            if (clicked) ClickFeedback();
            return clicked;
        }

        public static bool Toggle(string id, float x, float y, float w, float h, string label, bool value, bool enabled = true)
        {
            TextIn(label, x, y, w - 100, h, 22, enabled ? Color.White : new Color(255, 255, 255, 100));
            if (Button(id, x + w - 90, y + (h - 38) / 2, 90, 38, value ? "ON" : "OFF", 20, false, enabled, value ? Palette.Green : new Color(150, 150, 180, 255)))
                value = !value;
            return value;
        }

        /// <summary>Cycling selector "◂ value ▸" used for dropdown-style choices.</summary>
        public static int Selector(string id, float x, float y, float w, float h, string label, string[] options, int index, bool enabled = true)
        {
            TextIn(label, x, y, w * 0.42f, h, 22, enabled ? Color.White : new Color(255, 255, 255, 100));
            float bx = x + w * 0.44f, bw = w * 0.56f;
            if (Button(id + ".l", bx, y + 4, 48, h - 8, "<", 24, false, enabled)) index = (index - 1 + options.Length) % options.Length;
            Rect(bx + 54, y + 4, bw - 108, h - 8, new Color(0, 40, 50, 200));
            TextIn(options[Math.Clamp(index, 0, options.Length - 1)], bx + 54, y + 4, bw - 108, h - 8, 22, enabled ? Color.White : new Color(255, 255, 255, 100), 1);
            if (Button(id + ".r", bx + bw - 48, y + 4, 48, h - 8, ">", 24, false, enabled)) index = (index + 1) % options.Length;
            return index;
        }

        public static float Slider(string id, float x, float y, float w, float h, string label, float value, float min, float max, string valueText, bool enabled = true)
        {
            TextIn(label, x, y, w * 0.42f, h, 22, enabled ? Color.White : new Color(255, 255, 255, 100));
            float tx = x + w * 0.44f, tw = w * 0.42f, ty = y + h / 2 - 3;
            Rect(tx, ty, tw, 6, new Color(255, 255, 255, 40));
            float t = Math.Clamp((value - min) / (max - min), 0f, 1f);
            Rect(tx, ty, tw * t, 6, enabled ? Palette.Cyan : new Color(100, 150, 150, 255));
            Rect(tx + tw * t - 9, ty - 9, 18, 24, enabled ? Color.White : Color.Gray);
            TextIn(valueText, x + w - w * 0.12f, y, w * 0.12f, h, 20, Palette.Cyan, 2);
            if (enabled && !Modal)
            {
                if (Clicked && Hover(tx - 10, y, tw + 20, h)) _dragging = id;
                if (_dragging == id)
                {
                    value = min + (max - min) * Math.Clamp((Mouse.X - tx) / tw, 0f, 1f);
                    if (!MouseDown) Sfx.Play(Sfx.TinkHigh, 0.3f);
                }
            }
            return value;
        }

        public static string TextField(string id, float x, float y, float w, float h, string value, int maxLength, float size = 26)
        {
            bool focused = Focus == id;
            if (Clicked && !Modal) { if (Hover(x, y, w, h)) Focus = id; else if (focused) Focus = null; }
            focused = Focus == id;
            Rect(x, y, w, h, new Color(0, 38, 51, 230));
            Frame(x, y, w, h, Palette.WithAlpha(Palette.Cyan, focused ? 0.9f : 0.35f), 2f);
            if (focused)
            {
                int ch;
                while ((ch = Raylib.GetCharPressed()) > 0)
                    if (ch >= 32 && value.Length < maxLength) value += char.ConvertFromUtf32(ch);
                if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && value.Length > 0)
                    value = value.Substring(0, value.Length - 1);
                if ((Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl)) && Raylib.IsKeyPressed(KeyboardKey.V))
                {
                    string clip = Raylib.GetClipboardText_();
                    if (!string.IsNullOrEmpty(clip)) value = (value + clip.Trim()).Substring(0, Math.Min(maxLength, value.Length + clip.Trim().Length));
                }
            }
            string shown = value + (focused && (int)(Raylib.GetTime() * 2) % 2 == 0 ? "|" : "");
            TextIn(shown, x + 12, y, w - 24, h, size, Color.White);
            return value;
        }

        public static void Bar(float x, float y, float w, float h, float fraction, Color fill, Color? back = null)
        {
            Rect(x, y, w, h, back ?? new Color(255, 255, 255, 30));
            Rect(x, y, w * Math.Clamp(fraction, 0f, 1f), h, fill);
        }

        public static float Pulse(float speed = 2f, float min = 0.6f) => min + (1f - min) * (0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * speed));

        private static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
    }
}
