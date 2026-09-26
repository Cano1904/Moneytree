using System;
using System.Collections.Generic;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Neon IMGUI widget kit. Layout is authored on a 1920×1080 virtual canvas and scaled to the real
    /// resolution. Buttons implement the spec'd interaction: hover → SFX_Glass_Tink_High, 1.05× scale,
    /// brighter glow; click → SFX_Glass_Crack_Sharp + white screen flash.
    /// </summary>
    public static class Ui
    {
        public const float RefHeight = 1080f;
        public static readonly Color Cyan = new Color(0f, 1f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.25f, 0.6f);
        public static readonly Color Green = new Color(0.3f, 1f, 0.45f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.3f);
        public static readonly Color Panel = new Color(0.02f, 0.06f, 0.09f, 0.82f);

        private static readonly Dictionary<string, float> HoverAnim = new Dictionary<string, float>();
        private static readonly HashSet<string> Hovered = new HashSet<string>();
        private static Texture2D _white;
        private static Font _font;
        private static GUIStyle _label, _button, _field, _invisible;
        private static string _openDropdown;
        private static Action _deferred;
        private static float _flash;

        public static float Scale { get; private set; } = 1f;
        public static float Width { get; private set; } = 1920f;
        public static float Height => RefHeight;
        public static bool PointerOverDropdown { get; private set; }

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        /// <summary>Call at the top of OnGUI.</summary>
        public static void Begin()
        {
            if (_font == null)
            {
                // Bahnschrift ships with Windows 10+: a clean DIN-style "futuristic minimalist" face.
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Helvetica Neue", "Arial" }, 32);
                _label = new GUIStyle { font = _font, richText = false, wordWrap = false, alignment = TextAnchor.MiddleLeft };
                _label.normal.textColor = Color.white;
                _button = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                _field = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = 26, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4) };
                _field.normal.background = White;
                _field.focused.background = White;
                _field.normal.textColor = Color.white;
                _field.focused.textColor = Color.white;
                _invisible = new GUIStyle();
            }
            Scale = Screen.height / RefHeight;
            Width = Screen.width / Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            _deferred = null;
            if (Event.current.type == EventType.Repaint) Hovered.Clear();
        }

        /// <summary>Call at the end of OnGUI: draws open dropdowns and the click flash on top.</summary>
        public static void End()
        {
            PointerOverDropdown = false;
            _deferred?.Invoke();
            if (_flash > 0f)
            {
                Rect(new Rect(0, 0, Width, Height), new Color(1f, 1f, 1f, _flash * 0.55f));
                if (Event.current.type == EventType.Repaint) _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 6f);
            }
        }

        public static void Flash() => _flash = 1f;

        public static void Rect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float t = 2f)
        {
            Rect(new Rect(r.x, r.y, r.width, t), c);
            Rect(new Rect(r.x, r.yMax - t, r.width, t), c);
            Rect(new Rect(r.x, r.y, t, r.height), c);
            Rect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        public static void PanelBox(Rect r, string title = null)
        {
            Rect(r, Panel);
            Frame(r, new Color(0f, 1f, 1f, 0.35f), 1.5f);
            if (!string.IsNullOrEmpty(title)) Label(new Rect(r.x + 18, r.y + 10, r.width - 36, 36), title, 24, Cyan);
        }

        public static void Label(Rect r, string text, int size = 24, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, bool glow = false)
        {
            _label.fontSize = size;
            _label.alignment = align;
            Color c = color ?? Color.white;
            if (glow)
            {
                // Fake bloom: soft offset copies behind the text.
                _label.normal.textColor = new Color(c.r, c.g, c.b, c.a * 0.18f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    GUI.Label(new Rect(r.x + Mathf.Cos(a) * size * 0.08f, r.y + Mathf.Sin(a) * size * 0.08f, r.width, r.height), text, _label);
                }
            }
            _label.normal.textColor = c;
            GUI.Label(r, text, _label);
        }

        public static bool NeonButton(string id, Rect r, string text, int size = 28, bool focused = false, bool enabled = true, Color? accent = null)
        {
            Color acc = accent ?? Cyan;
            bool mouseOver = enabled && r.Contains(Event.current.mousePosition) && _openDropdown == null;
            bool hover = mouseOver || (focused && enabled);
            TrackHover(id, hover);

            float anim = HoverAnim.TryGetValue(id, out float v) ? v : 0f;
            if (Event.current.type == EventType.Repaint)
            {
                anim = Mathf.MoveTowards(anim, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                HoverAnim[id] = anim;
            }

            float scale = 1f + 0.05f * anim;
            Matrix4x4 old = GUI.matrix;
            Vector2 c = r.center;
            GUI.matrix = old * Matrix4x4.TRS(new Vector3(c.x, c.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f)) * Matrix4x4.TRS(new Vector3(-c.x, -c.y, 0f), Quaternion.identity, Vector3.one);

            float glow = enabled ? 0.10f + 0.30f * anim : 0.04f;
            Rect(r, new Color(acc.r * 0.25f, acc.g * 0.25f, acc.b * 0.25f, 0.55f + 0.2f * anim));
            Rect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(acc.r, acc.g, acc.b, glow * 0.35f));
            Frame(r, new Color(acc.r, acc.g, acc.b, enabled ? 0.45f + 0.55f * anim : 0.2f), 2f);
            _button.fontSize = size;
            _button.normal.textColor = enabled ? Color.Lerp(new Color(0.8f, 0.95f, 1f), Color.white, anim) : new Color(1f, 1f, 1f, 0.3f);
            GUI.Label(r, text, _button);

            bool clicked = enabled && GUI.Button(r, GUIContent.none, _invisible) && _openDropdown == null;
            GUI.matrix = old;
            if (clicked) Click();
            return clicked;
        }

        public static void Click()
        {
            AudioService.Instance?.Play(AudioService.CrackSharp, 0.7f);
            Flash();
        }

        private static void TrackHover(string id, bool hover)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (hover)
            {
                if (!HoverAnim.ContainsKey(id + "#h") || HoverAnim[id + "#h"] < 0.5f)
                    AudioService.Instance?.Play(AudioService.TinkHigh, 0.35f, UnityEngine.Random.Range(0.95f, 1.1f));
                HoverAnim[id + "#h"] = 1f;
                Hovered.Add(id);
            }
            else HoverAnim[id + "#h"] = 0f;
        }

        public static bool Toggle(string id, Rect r, string label, bool value, bool enabled = true)
        {
            Label(new Rect(r.x, r.y, r.width - 90, r.height), label, 22, enabled ? Color.white : new Color(1, 1, 1, 0.4f));
            var box = new Rect(r.xMax - 80, r.y + (r.height - 34) / 2, 80, 34);
            if (NeonButton(id, box, value ? "ON" : "OFF", 20, false, enabled, value ? Green : new Color(0.6f, 0.6f, 0.7f))) value = !value;
            return value;
        }

        public static float Slider(string id, Rect r, string label, float value, float min, float max, string valueText, bool enabled = true)
        {
            Label(new Rect(r.x, r.y, r.width * 0.42f, r.height), label, 22, enabled ? Color.white : new Color(1, 1, 1, 0.4f));
            var track = new Rect(r.x + r.width * 0.44f, r.y + r.height / 2 - 3, r.width * 0.42f, 6);
            Rect(track, new Color(1f, 1f, 1f, 0.15f));
            float t = Mathf.InverseLerp(min, max, value);
            Rect(new Rect(track.x, track.y, track.width * t, track.height), enabled ? Cyan : new Color(0.4f, 0.6f, 0.6f));
            var knob = new Rect(track.x + track.width * t - 9, track.y - 9, 18, 24);
            Rect(knob, enabled ? Color.white : Color.gray);
            Label(new Rect(r.xMax - r.width * 0.12f, r.y, r.width * 0.12f, r.height), valueText, 20, Cyan, TextAnchor.MiddleRight);

            if (!enabled || _openDropdown != null) return value;
            var hit = new Rect(track.x - 10, r.y, track.width + 20, r.height);
            Event e = Event.current;
            int control = GUIUtility.GetControlID(id.GetHashCode(), FocusType.Passive, hit);
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (hit.Contains(e.mousePosition)) { GUIUtility.hotControl = control; e.Use(); goto case EventType.MouseDrag; }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == control)
                    {
                        value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                        GUI.changed = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == control) { GUIUtility.hotControl = 0; e.Use(); AudioService.Instance?.Play(AudioService.TinkHigh, 0.3f); }
                    break;
            }
            return value;
        }

        /// <summary>Real dropdown: the option list is drawn last (on top of everything) while open.</summary>
        public static int Dropdown(string id, Rect r, string label, string[] options, int selected, bool enabled = true)
        {
            Label(new Rect(r.x, r.y, r.width * 0.42f, r.height), label, 22, enabled ? Color.white : new Color(1, 1, 1, 0.4f));
            var box = new Rect(r.x + r.width * 0.44f, r.y + 4, r.width * 0.56f, r.height - 8);
            string current = selected >= 0 && selected < options.Length ? options[selected] : "—";
            bool open = _openDropdown == id;
            if (open)
            {
                Rect(box, new Color(0f, 0.3f, 0.35f, 0.9f));
                Frame(box, Cyan, 2f);
                Label(new Rect(box.x + 12, box.y, box.width - 40, box.height), current + "  ▴", 22, Color.white);
            }
            else if (NeonButton(id, box, current + "  ▾", 22, false, enabled))
            {
                _openDropdown = id;
            }

            if (open)
            {
                int result = selected;
                Rect list = new Rect(box.x, box.yMax + 2, box.width, options.Length * 40f);
                if (list.yMax > Height - 10) list.y = box.y - list.height - 2;
                _deferred += () =>
                {
                    Rect(list, new Color(0.01f, 0.04f, 0.06f, 0.97f));
                    Frame(list, Cyan, 2f);
                    PointerOverDropdown = list.Contains(Event.current.mousePosition);
                    for (int i = 0; i < options.Length; i++)
                    {
                        var item = new Rect(list.x, list.y + i * 40f, list.width, 40f);
                        bool hover = item.Contains(Event.current.mousePosition);
                        if (hover || i == selected) Rect(item, new Color(0f, 1f, 1f, hover ? 0.25f : 0.1f));
                        Label(new Rect(item.x + 14, item.y, item.width - 20, item.height), options[i], 22, Color.white);
                        if (Event.current.type == EventType.MouseDown && hover)
                        {
                            _pendingSelection = i;
                            _pendingId = id;
                            _openDropdown = null;
                            Click();
                            Event.current.Use();
                        }
                    }
                    if (Event.current.type == EventType.MouseDown && !list.Contains(Event.current.mousePosition))
                    {
                        _openDropdown = null;
                        Event.current.Use();
                    }
                };
                return result;
            }

            if (_pendingId == id)
            {
                _pendingId = null;
                return Mathf.Clamp(_pendingSelection, 0, options.Length - 1);
            }
            return selected;
        }

        private static int _pendingSelection;
        private static string _pendingId;

        public static void CloseDropdowns() => _openDropdown = null;

        public static string TextField(string id, Rect r, string value, int maxLength)
        {
            GUI.SetNextControlName(id);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0.15f, 0.2f, 0.9f);
            string v = GUI.TextField(r, value ?? string.Empty, maxLength, _field);
            GUI.backgroundColor = old;
            Frame(r, new Color(0f, 1f, 1f, GUI.GetNameOfFocusedControl() == id ? 0.9f : 0.35f), 2f);
            return v;
        }

        public static void Bar(Rect r, float fraction, Color fill, Color? back = null)
        {
            Rect(r, back ?? new Color(1f, 1f, 1f, 0.12f));
            Rect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), fill);
        }

        /// <summary>Vertical gamepad/keyboard focus for simple menus.</summary>
        public static int NavigateFocus(int focus, int count, InputService input)
        {
            int dir = input.MenuNavigate();
            if (dir == 0) return focus;
            AudioService.Instance?.Play(AudioService.TinkHigh, 0.35f);
            return (focus + dir + count) % count;
        }

        public static float Pulse(float speed = 2f, float min = 0.6f) => min + (1f - min) * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed));
    }
}
