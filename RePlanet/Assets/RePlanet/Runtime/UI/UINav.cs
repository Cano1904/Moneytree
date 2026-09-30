using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Fokus-System für IMGUI: Jedes bedienbare Element meldet sich pro Durchlauf in Zeichenreihenfolge an.
    /// Tastatur/Controller bewegen den Fokus räumlich (Pfeile, Steuerkreuz, Stick), Bestätigen löst aus,
    /// Links/Rechts verstellt Regler und Auswahlfelder. Maus funktioniert wie gewohnt und setzt den Fokus beim Überfahren.
    /// </summary>
    public static class UINav
    {
        const byte KButton = 0, KAdjust = 1, KText = 2;

        /// <summary>Index des fokussierten Elements (Zeichenreihenfolge).</summary>
        public static int Focus;
        /// <summary>true, solange zuletzt Tastatur/Controller benutzt wurde → Fokusrahmen sichtbar.</summary>
        public static bool KeyboardMode;
        /// <summary>Ein Textfeld hat den Tastaturfokus (Hotkeys pausieren).</summary>
        public static bool Editing;
        /// <summary>Fokus hat sich geändert (für Vorschau-Aktionen wie die Planetenwahl).</summary>
        public static int FocusChangedFrame;

        static int count;
        static readonly List<Rect> rects = new List<Rect>(64), prevRects = new List<Rect>(64);
        static readonly List<byte> kinds = new List<byte>(64), prevKinds = new List<byte>(64);
        static int pendingActivate = -1, pendingAdjust, pendingFocusText = -1;
        static bool navMoved, mouseMoved, stopEditing;
        static Vector3 lastMouse;
        static int lastHoverSoundFrame;

        // Scroll-Bereiche: Position und Inhaltshöhe je Schlüssel
        static readonly Dictionary<int, Vector2> scrolls = new Dictionary<int, Vector2>();
        static readonly Dictionary<int, float> contentH = new Dictionary<int, float>();
        struct ScrollCtx { public int Key; public Rect View; public Vector2 Offset; }
        static readonly Stack<ScrollCtx> scrollStack = new Stack<ScrollCtx>();
        static Vector2 offset;

        public static void ResetFocus()
        {
            Focus = 0; pendingActivate = -1; pendingAdjust = 0; navMoved = false; pendingFocusText = -1;
            stopEditing = Editing;
            FocusChangedFrame = Time.frameCount;
        }

        public static void ResetScroll(int key) { scrolls.Remove(key); }

        // ------------------------------------------------------------ Durchläufe
        public static void BeginPass()
        {
            count = 0;
            offset = Vector2.zero;
            scrollStack.Clear();
            if (Event.current.type == EventType.Repaint) { rects.Clear(); kinds.Clear(); }
        }

        public static void EndPass()
        {
            var ev = Event.current.type;
            if (ev == EventType.Repaint)
            {
                prevRects.Clear(); prevRects.AddRange(rects);
                prevKinds.Clear(); prevKinds.AddRange(kinds);
                if (prevRects.Count > 0 && Focus >= prevRects.Count) Focus = prevRects.Count - 1;
                if (Focus < 0) Focus = 0;
                mouseMoved = false;
                navMoved = false;
                // Nicht verbrauchte Eingaben (z. B. Fokus auf nicht mehr vorhandenem Element) verfallen
                pendingActivate = -1;
                pendingAdjust = 0;
            }
            if (stopEditing)
            {
                stopEditing = false;
                GUIUtility.keyboardControl = 0;
            }
            // Nur benannte Textfelder zählen als „Bearbeiten“ (Schieberegler o. Ä. können ebenfalls den Tastaturfokus halten)
            Editing = GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
        }

        /// <summary>Verarbeitet Menü-Eingaben (einmal pro Frame aus Update). active = ein Menü ist offen.</summary>
        public static void Tick(bool active)
        {
            var m = Input.mousePosition;
            if ((m - lastMouse).sqrMagnitude > 4f) { mouseMoved = true; KeyboardMode = false; }
            lastMouse = m;
            if (Input.GetMouseButtonDown(0)) KeyboardMode = false;
            var nav = InputMap.Nav(); // immer abfragen (Wiederholungszustand)
            if (!active) { pendingActivate = -1; pendingAdjust = 0; return; }
            if (Editing)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)
                    || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton1))
                    stopEditing = true;
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) { stopEditing = true; Move(nav); }
                return;
            }
            if (nav != Vector2Int.zero)
            {
                KeyboardMode = true;
                byte k = Focus >= 0 && Focus < prevKinds.Count ? prevKinds[Focus] : KButton;
                if (k == KAdjust && nav.x != 0 && nav.y == 0) pendingAdjust = nav.x;
                else Move(nav);
            }
            if (InputMap.NavConfirm())
            {
                KeyboardMode = true;
                if (Focus >= 0 && Focus < prevRects.Count) pendingActivate = Focus;
            }
        }

        public static void CancelPending() { pendingActivate = -1; pendingAdjust = 0; }

        static void Move(Vector2Int nav)
        {
            int n = prevRects.Count;
            if (n == 0) return;
            if (Focus < 0 || Focus >= n) { SetFocus(0); return; }
            var from = prevRects[Focus];
            var fc = from.center;
            // GUI-Koordinaten: y wächst nach unten
            var dir = new Vector2(nav.x, -nav.y);
            if (dir.x != 0 && dir.y != 0) dir.y = 0;
            int best = -1; float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (i == Focus) continue;
                var r = prevRects[i];
                var c = r.center;
                float along, across;
                if (dir.x != 0)
                {
                    along = (c.x - fc.x) * dir.x;
                    // Überlappung in y bevorzugen
                    float overlap = Mathf.Min(r.yMax, from.yMax) - Mathf.Max(r.yMin, from.yMin);
                    across = overlap > 0 ? 0 : Mathf.Abs(c.y - fc.y);
                    if (along <= 2f) continue;
                    if ((dir.x > 0 && r.xMin < from.xMax - 4f && r.center.x <= fc.x + 2f) || (dir.x < 0 && r.xMax > from.xMin + 4f && r.center.x >= fc.x - 2f)) continue;
                }
                else
                {
                    along = (c.y - fc.y) * dir.y;
                    float overlap = Mathf.Min(r.xMax, from.xMax) - Mathf.Max(r.xMin, from.xMin);
                    across = overlap > 0 ? 0 : Mathf.Abs(c.x - fc.x);
                    if (along <= 2f) continue;
                }
                float score = along + across * 3f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best < 0 && dir.y != 0)
            {
                // Umlauf: oben ↔ unten
                best = dir.y > 0 ? 0 : n - 1;
                if (best == Focus) return;
            }
            if (best >= 0) SetFocus(best);
        }

        static void SetFocus(int i)
        {
            if (i == Focus) return;
            Focus = i;
            navMoved = true;
            FocusChangedFrame = Time.frameCount;
            HoverSound();
        }

        static void HoverSound()
        {
            if (Time.frameCount - lastHoverSoundFrame < 4) return;
            lastHoverSoundFrame = Time.frameCount;
            AudioManager.Ui("ui_hover");
        }

        // ------------------------------------------------------------ Anmeldung
        /// <summary>Meldet ein Element an. Liefert seinen Index; focused = hat den Fokus.</summary>
        static int Register(Rect r, byte kind, out bool focused)
        {
            int id = count++;
            var ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                rects.Add(new Rect(r.x + offset.x, r.y + offset.y, r.width, r.height));
                kinds.Add(kind);
                if (mouseMoved && r.Contains(ev.mousePosition) && InsideScrollView(ev.mousePosition) && Focus != id)
                {
                    Focus = id;
                    FocusChangedFrame = Time.frameCount;
                    HoverSound();
                }
                if (navMoved && id == Focus) EnsureVisible(r);
            }
            focused = id == Focus;
            return id;
        }

        static bool InsideScrollView(Vector2 localMouse)
        {
            if (scrollStack.Count == 0) return true;
            var ctx = scrollStack.Peek();
            var abs = localMouse + offset;
            return ctx.View.Contains(abs);
        }

        static void EnsureVisible(Rect r)
        {
            if (scrollStack.Count == 0) return;
            var ctx = scrollStack.Peek();
            Vector2 s;
            scrolls.TryGetValue(ctx.Key, out s);
            float top = r.y - 8f, bottom = r.yMax + 8f;
            if (top < s.y) s.y = Mathf.Max(0, top);
            else if (bottom > s.y + ctx.View.height) s.y = bottom - ctx.View.height;
            scrolls[ctx.Key] = s;
        }

        static bool Activated(int id)
        {
            if (pendingActivate == id)
            {
                pendingActivate = -1;
                return true;
            }
            return false;
        }

        static int Adjust(bool focused)
        {
            if (!focused || pendingAdjust == 0) return 0;
            int a = pendingAdjust;
            pendingAdjust = 0;
            return a;
        }

        static void DrawFocus(Rect r, bool focused)
        {
            if (!focused || !KeyboardMode || Event.current.type != EventType.Repaint) return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            var c = UISkin.FocusCol; c.a = pulse;
            UISkin.OutlineRect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), c);
        }

        // ------------------------------------------------------------ Scroll-Bereiche
        public static void BeginScroll(int key, Rect view)
        {
            Vector2 s; scrolls.TryGetValue(key, out s);
            float h; contentH.TryGetValue(key, out h);
            float maxY = Mathf.Max(0, h - view.height);
            s.y = Mathf.Clamp(s.y, 0, maxY);
            bool bar = h > view.height + 1f;
            var content = new Rect(0, 0, view.width - (bar ? 18f : 0f), Mathf.Max(h, view.height));
            s = GUI.BeginScrollView(view, s, content, false, false, GUIStyle.none, bar ? GUI.skin.verticalScrollbar : GUIStyle.none);
            scrolls[key] = s;
            scrollStack.Push(new ScrollCtx { Key = key, View = new Rect(view.x + offset.x, view.y + offset.y, view.width, view.height), Offset = offset });
            offset = new Vector2(offset.x + view.x - s.x, offset.y + view.y - s.y);
        }

        /// <summary>Beendet den Scroll-Bereich; usedHeight = tatsächliche Inhaltshöhe (gilt ab dem nächsten Frame).</summary>
        public static void EndScroll(float usedHeight)
        {
            GUI.EndScrollView(true);
            if (scrollStack.Count == 0) return;
            var ctx = scrollStack.Pop();
            offset = ctx.Offset;
            if (Event.current.type == EventType.Repaint) contentH[ctx.Key] = usedHeight;
        }

        /// <summary>Schließt offene Scroll-Bereiche nach einem Fehler, damit der GUI-Clip-Stapel stimmt.</summary>
        public static void AbortScrolls()
        {
            while (scrollStack.Count > 0)
            {
                var ctx = scrollStack.Pop();
                offset = ctx.Offset;
                try { GUI.EndScrollView(false); } catch (System.Exception) { }
            }
        }

        /// <summary>Innenbreite eines Scroll-Bereichs (abzüglich Leiste, falls nötig).</summary>
        public static float ScrollWidth(int key, Rect view)
        {
            float h; contentH.TryGetValue(key, out h);
            return view.width - (h > view.height + 1f ? 20f : 0f);
        }

        // ------------------------------------------------------------ Bedienelemente
        public static bool Button(Rect r, string text, bool enabled = true, GUIStyle st = null)
        {
            text = Loc.T(text); // Beschriftungen dürfen deutsch übergeben werden (Sprache wird hier angewandt)
            bool focused;
            int id = Register(r, KButton, out focused);
            st = st ?? UISkin.Button;
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(r, text, enabled ? st : UISkin.ButtonOff);
            GUI.enabled = old;
            if (enabled && focused && KeyboardMode && Event.current.type == EventType.Repaint)
                st.Draw(r, UISkin.Tmp(text), true, false, false, false);
            if (Activated(id) && enabled) clicked = true;
            DrawFocus(r, focused);
            if (clicked) AudioManager.Ui("ui_click");
            return clicked && enabled;
        }

        /// <summary>Unsichtbare Klickfläche (für Karten, Listenzeilen); der Aufrufer zeichnet selbst.</summary>
        public static bool Area(Rect r, out bool focused, bool enabled = true)
        {
            int id = Register(r, KButton, out focused);
            bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (Activated(id) && enabled) clicked = true;
            DrawFocus(r, focused);
            if (clicked) AudioManager.Ui("ui_click");
            return clicked;
        }

        public static bool IsHover(Rect r) { return r.Contains(Event.current.mousePosition); }

        public static bool Toggle(Rect r, bool value, string label)
        {
            bool focused;
            int id = Register(r, KAdjust, out focused);
            bool hover = r.Contains(Event.current.mousePosition) || (focused && KeyboardMode);
            if (Event.current.type == EventType.Repaint)
            {
                if (hover) UISkin.RoundRect(r, new Color(1, 1, 1, 0.06f));
                var box = new Rect(r.x + 6, r.y + (r.height - 26) * 0.5f, 26, 26);
                UISkin.Sliced(box, value ? UISkin.BtnSel : UISkin.PanelDark);
                if (value) GUI.Label(box, "✓", UISkin.LabelCenter);
                GUI.Label(new Rect(box.xMax + 12, r.y, r.width - 50, r.height), Loc.T(label), UISkin.Label);
                GUI.Label(new Rect(r.x, r.y, r.width - 10, r.height), value ? Loc.T("An") : Loc.T("Aus"), value ? UISkin.LabelRight : DimRight());
            }
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (Activated(id)) clicked = true;
            if (Adjust(focused) != 0) clicked = true;
            DrawFocus(r, focused);
            if (clicked) { AudioManager.Ui("ui_click"); return !value; }
            return value;
        }

        static GUIStyle dimRight;
        static GUIStyle DimRight()
        {
            if (dimRight == null || dimRight.fontSize != UISkin.LabelRight.fontSize) { dimRight = new GUIStyle(UISkin.LabelRight); }
            dimRight.normal.textColor = UISkin.TextDim;
            return dimRight;
        }

        /// <summary>Regler: Beschriftung links, Schieber, Wert rechts. Links/Rechts ändert um step.</summary>
        public static float Slider(Rect r, string label, float value, float min, float max, float step, string valueText)
        {
            bool focused;
            Register(r, KAdjust, out focused);
            bool hover = r.Contains(Event.current.mousePosition) || (focused && KeyboardMode);
            if (hover) UISkin.RoundRect(r, new Color(1, 1, 1, 0.06f));
            float lw = r.width * 0.42f, vw = 90f;
            GUI.Label(new Rect(r.x + 10, r.y, lw - 10, r.height), Loc.T(label), UISkin.Label);
            var sr = new Rect(r.x + lw, r.y + r.height * 0.5f - 9, r.width - lw - vw - 10, 18);
            float f = Mathf.InverseLerp(min, max, value);
            UISkin.Bar(new Rect(sr.x, sr.y + 4, sr.width, 10), f, UISkin.Teal);
            if (Event.current.type == EventType.Repaint)
            {
                var knob = new Rect(sr.x + sr.width * f - 10, sr.y - 2, 20, 22);
                UISkin.Tex(knob, UISkin.Circle, focused && KeyboardMode ? UISkin.FocusCol : UISkin.Text);
            }
            float nv = GUI.HorizontalSlider(sr, value, min, max, GUIStyle.none, GUIStyle.none);
            if (Mathf.Abs(nv - value) > 1e-5f)
            {
                value = step > 0 ? Mathf.Round(nv / step) * step : nv;
                value = Mathf.Clamp(value, min, max);
            }
            GUI.Label(new Rect(r.xMax - vw, r.y, vw - 10, r.height), valueText ?? value.ToString("0.00"), UISkin.LabelRight);
            int a = Adjust(focused);
            if (a != 0) { value = Mathf.Clamp(value + a * step, min, max); AudioManager.Ui("ui_hover"); }
            DrawFocus(r, focused);
            return value;
        }

        /// <summary>Auswahlfeld „‹ Wert ›“ mit Beschriftung. Links/Rechts oder Klick auf die Pfeile wechselt.</summary>
        public static int Choice(Rect r, string label, int index, string[] options)
        {
            if (options == null || options.Length == 0) return index;
            bool focused;
            int id = Register(r, KAdjust, out focused);
            bool hover = r.Contains(Event.current.mousePosition) || (focused && KeyboardMode);
            if (hover) UISkin.RoundRect(r, new Color(1, 1, 1, 0.06f));
            float lw = label != null ? r.width * 0.42f : 0f;
            if (label != null) GUI.Label(new Rect(r.x + 10, r.y, lw - 10, r.height), Loc.T(label), UISkin.Label);
            var cr = new Rect(r.x + lw, r.y + 3, r.width - lw - 6, r.height - 6);
            UISkin.Sliced(cr, UISkin.PanelDark);
            int n = options.Length;
            index = Mathf.Clamp(index, 0, n - 1);
            GUI.Label(cr, Loc.T(options[index]), UISkin.LabelCenter);
            var lb = new Rect(cr.x, cr.y, 40, cr.height);
            var rb = new Rect(cr.xMax - 40, cr.y, 40, cr.height);
            GUI.Label(lb, "‹", UISkin.LabelCenter);
            GUI.Label(rb, "›", UISkin.LabelCenter);
            int d = 0;
            bool bl = GUI.Button(lb, GUIContent.none, GUIStyle.none);
            bool br = GUI.Button(rb, GUIContent.none, GUIStyle.none);
            bool bc = GUI.Button(new Rect(lb.xMax, cr.y, cr.width - 80, cr.height), GUIContent.none, GUIStyle.none);
            if (bl) d = -1; else if (br || bc) d = 1;
            if (Activated(id)) d = 1;
            int a = Adjust(focused);
            if (a != 0) d = a;
            DrawFocus(r, focused);
            if (d != 0) { AudioManager.Ui("ui_click"); index = (index + d + n) % n; }
            return index;
        }

        public static string TextField(Rect r, string value, int maxLength, string controlName)
        {
            bool focused;
            int id = Register(r, KText, out focused);
            if (Activated(id)) pendingFocusText = id;
            GUI.SetNextControlName(controlName);
            string nv = GUI.TextField(r, value ?? "", maxLength, UISkin.Field);
            if (pendingFocusText == id && Event.current.type == EventType.Repaint)
            {
                pendingFocusText = -1;
                GUI.FocusControl(controlName);
            }
            DrawFocus(r, focused);
            return nv;
        }

        /// <summary>Reiterleiste; liefert den neuen Index.</summary>
        public static int Tabs(Rect r, int current, string[] names, bool registerNav = true)
        {
            int n = names.Length;
            float gap = 6f;
            float w = (r.width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var tr = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                var st = i == current ? UISkin.TabSel : UISkin.Tab;
                if (registerNav)
                {
                    if (Button(tr, names[i], true, st)) current = i;
                }
                else if (GUI.Button(tr, Loc.T(names[i]), st)) { current = i; AudioManager.Ui("ui_click"); }
            }
            return current;
        }
    }
}
