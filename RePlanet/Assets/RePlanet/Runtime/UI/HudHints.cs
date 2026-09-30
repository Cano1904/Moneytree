using System;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Ruhiges HUD (Einstellung „Hinweise“ = Aus oder Minimal): schlanker Status, einblendendes Ziel, kleine Werkzeugleiste,
    /// Interaktionshinweise als Tastensymbol + ein Wort direkt am Objekt, gekürzte und zusammengefasste Meldungen.
    /// Bei „Ausführlich“ zeichnet <c>HudView</c> die bisherige, ausführliche Darstellung.
    /// </summary>
    public partial class UIRoot
    {
        // ---- Ziel
        string objShown;
        float objUntil, objLastHudTime = -10f;
        // ---- Werkzeugleiste
        string toolLast;
        float toolActiveAt = -100f;

        int HintLevel(GameApp app) { return app != null && app.Settings != null ? Mathf.Clamp(app.Settings.Hints, 0, 2) : Settings.HintsMinimal; }

        /// <summary>Ziel erneut kurz einblenden (z. B. nach dem Schließen des Menüs, neuem Auftrag).</summary>
        public void ShowObjective(float seconds = 6f) { objUntil = Mathf.Max(objUntil, Time.unscaledTime + seconds); }

        /// <summary>Kurzbezeichnung einer Taste für kleine Tastensymbole.</summary>
        static string ShortKey(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            switch (label)
            {
                case "Linke Maustaste": return "LMT";
                case "Rechte Maustaste": return "RMT";
                case "Mittlere Maustaste": return "MMT";
                case "Leertaste": return L("Leer");
                case "Umschalt": return "⇧";
                case "Steuerkreuz ↑": return "✚↑";
                case "Steuerkreuz ↓": return "✚↓";
                case "Steuerkreuz →": return "✚→";
                case "Steuerkreuz ←": return "✚←";
                // Englische Tastennamen (Sprache Englisch)
                case "Left Mouse": return "LMB";
                case "Right Mouse": return "RMB";
                case "Middle Mouse": return "MMB";
                case "Space": return "Space";
                case "Shift": return "⇧";
                case "D-pad ↑": return "✚↑";
                case "D-pad ↓": return "✚↓";
                case "D-pad →": return "✚→";
                case "D-pad ←": return "✚←";
            }
            return label.Length > 6 ? label.Substring(0, 5) + "." : label;
        }

        /// <summary>Weltpunkt → HUD-Koordinaten (virtuelle 1080p-Fläche). false, wenn hinter der Kamera oder außerhalb.</summary>
        bool WorldToHud(Vector3 world, out Vector2 p)
        {
            p = Vector2.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.5f) return false;
            p = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return p.x > -40 && p.x < VW + 40 && p.y > -40 && p.y < VH + 40;
        }

        // ================================================================== Status oben links (schlank)
        void DrawStatusCompact(GameApp app, WorldState w, PlayerData me, PlanetDef pd, Settings s, int level)
        {
            int area = PlanetLayout.AreaOf(me.Pos.z);
            var accent = UISkin.FromRgb(pd.Accent);
            float maxE = Mathf.Max(1f, w.MaxEnergy);
            float ef = Mathf.Clamp01(me.Energy / maxE);
            float vol = Item.Volume(me.Bin), cap = Mathf.Max(1f, w.BinCapacity);
            float lf = Mathf.Clamp01(vol / cap);

            string alert = null; Color alertCol = UISkin.Warn;
            if (me.Energy <= 0.01f) { alert = L("⚠ Notbetrieb – zum Stützpunkt"); alertCol = (s.ReduceFlashing || Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f) ? UISkin.Bad : UISkin.Warn; }
            else if (lf >= 0.99f) alert = L("Behälter voll") + (level > 0 ? "  [" + ShortKey(InputMap.Label(GameAction.Press)) + L("] pressen") : "");
            else if (level > 0 && Hud.Diving) { alert = L("Tauchen  [") + ShortKey(InputMap.Label(GameAction.DiveUp)) + "] ↑  [" + ShortKey(InputMap.Label(GameAction.DiveDown)) + "] ↓"; alertCol = UISkin.Teal; }
            else if (level > 0 && Hud.Swimming && w.TechLevel("dive") > 0) { alert = "[" + ShortKey(InputMap.Label(GameAction.DiveDown)) + L("] abtauchen"); alertCol = UISkin.Teal; }

            var r = new Rect(16, 14, 262, alert != null ? 106 : 82);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x + 14, r.y + 5, r.width - 24, 22), UISkin.Col(pd.Name, accent) + UISkin.Col("  ·  " + pd.AreaNames[Mathf.Clamp(area, 0, 2)], UISkin.TextDim), UISkin.LabelTiny);
            // Credits
            UISkin.Tex(new Rect(r.x + 14, r.y + 31, 18, 18), UISkin.Shape("dot"), UISkin.Accent);
            UISkin.Tex(new Rect(r.x + 18, r.y + 35, 10, 10), UISkin.Shape("ring"), new Color(1, 1, 1, 0.8f));
            GUI.Label(new Rect(r.x + 38, r.y + 26, 200, 28), "<b>" + Num(w.Credits) + "</b>", UISkin.Label);
            // Energie und Ladung als Symbole mit schmalen Balken
            Color ec = ef < 0.1f ? UISkin.Bad : ef < 0.3f ? UISkin.Warn : UISkin.Teal;
            float by = r.y + 58;
            UISkin.Tex(new Rect(r.x + 14, by, 16, 16), UISkin.Shape("battery"), ec);
            UISkin.Bar(new Rect(r.x + 34, by + 5, 82, 7), ef, ec);
            UISkin.Tex(new Rect(r.x + 134, by, 16, 16), UISkin.Shape("square"), lf >= 0.99f ? UISkin.Warn : UISkin.Accent);
            UISkin.Bar(new Rect(r.x + 154, by + 5, 82, 7), lf, lf >= 0.99f ? UISkin.Warn : UISkin.Accent);
            if (alert != null) GUI.Label(new Rect(r.x + 14, r.y + 78, r.width - 20, 24), UISkin.Col(alert, alertCol), UISkin.LabelTiny);
        }

        // ================================================================== Ziel (einblendend, kompakt)
        void DrawObjectiveCompact(GameApp app, WorldState w, int level)
        {
            string obj = L(Objective(w));
            float now = Time.unscaledTime;
            // Ziel nach Menü/Karte (HUD war nicht sichtbar) oder bei Änderung wieder zeigen
            if (now - objLastHudTime > 0.4f) ShowObjective(level == Settings.HintsOff ? 4f : 6f);
            objLastHudTime = now;
            if (obj != objShown)
            {
                bool first = objShown == null;
                objShown = obj;
                if (!string.IsNullOrEmpty(obj)) ShowObjective(first ? 8f : 9f);
            }
            if (string.IsNullOrEmpty(obj)) return;
            float a = Mathf.Clamp01((objUntil - now) / 0.6f);
            float ow = Mathf.Min(620f, VW - 700f);
            if (ow < 360f) ow = Mathf.Min(520f, VW - 460f);
            if (ow < 200f) return;
            if (a > 0.01f)
            {
                var oc = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                float th = UISkin.TextHeight(UISkin.WrapSmall, obj, ow - 74);
                var r = new Rect((VW - ow) * 0.5f, 14, ow, Mathf.Max(34f, th + 12f));
                UISkin.PanelBox(r);
                UISkin.Tex(new Rect(r.x + 14, r.y + 10, 14, 14), UISkin.Shape("diamond"), UISkin.Accent);
                GUI.Label(new Rect(r.x + 40, r.y + 6, ow - 52, th + 4), UISkin.Col(obj, UISkin.Text), UISkin.WrapSmall);
                GUI.color = oc;
            }
            else if (level != Settings.HintsOff)
            {
                // unaufdringliches Merkzeichen: das Ziel ist mit [J] abrufbar
                string k = ShortKey(InputMap.Label(GameAction.Missions));
                var r = new Rect(VW * 0.5f - 44, 14, 88, 26);
                UISkin.RoundRect(r, new Color(0, 0, 0, UISkin.Contrast ? 0.9f : 0.35f));
                UISkin.Tex(new Rect(r.x + 10, r.y + 7, 12, 12), UISkin.Shape("diamond"), UISkin.Accent * new Color(1, 1, 1, 0.8f));
                GUI.Label(new Rect(r.x + 26, r.y, 34, 26), UISkin.Col(L("Ziel"), UISkin.TextDim), UISkin.LabelTiny);
                UISkin.KeyCap(r.x + 60, r.y + 4, k, 18);
            }
        }

        // ================================================================== Werkzeugleiste (klein, blendet aus)
        void DrawToolbarCompact(GameApp app, WorldState w, PlayerData me, float y)
        {
            string active = PlayerController.I != null ? PlayerController.I.Tool : me.Tool;
            float now = Time.unscaledTime;
            if (active != toolLast) { toolLast = active; toolActiveAt = now; }
            if (InputMap.Down(GameAction.ToolNext) || InputMap.Down(GameAction.ToolPrev)) toolActiveAt = now;
            foreach (var k in ToolKeys) if (InputMap.Down(k)) toolActiveAt = now;
            float a = Mathf.Clamp01(1f - (now - toolActiveAt - 3.5f) / 0.6f);
            int n = Rules.ToolIds.Length;
            int ai = Array.IndexOf(Rules.ToolIds, active);
            if (a > 0.01f)
            {
                float sw = 54f, gap = 4f, h = 50f;
                float total = n * sw + (n - 1) * gap;
                float x0 = (VW - total) * 0.5f;
                float y0 = y + 78f - h;
                var oc = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                for (int i = 0; i < n; i++)
                {
                    string id = Rules.ToolIds[i];
                    bool has = Rules.HasTool(w, id);
                    bool act = id == active;
                    var r = new Rect(x0 + i * (sw + gap), y0, sw, h);
                    UISkin.Sliced(r, act ? UISkin.BtnSel : has ? UISkin.Panel : UISkin.PanelDark);
                    if (act) UISkin.OutlineRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UISkin.Accent);
                    Color ic = !has ? new Color(1, 1, 1, 0.2f) : act ? Color.white : UISkin.Teal;
                    UISkin.Tex(new Rect(r.center.x - 11, r.y + 16, 22, 22), UISkin.Shape(ToolShapes[i]), ic);
                    UISkin.KeyCap(r.x + 3, r.y + 3, ShortKey(InputMap.Label(ToolKeys[i])), 16);
                }
                if (ai >= 0) GUI.Label(new Rect(x0, y0 - 26, total, 24), L(ToolShort[ai]), SmallCenter());
                GUI.color = oc;
            }
            if (a < 0.99f && ai >= 0)
            {
                // Ausgeblendet: nur das aktive Werkzeug als kleines Symbol
                var oc = GUI.color;
                GUI.color = new Color(1, 1, 1, 1f - a);
                var r = new Rect(VW * 0.5f - 20, y + 78f - 40f, 40, 40);
                UISkin.RoundRect(r, new Color(0, 0, 0, UISkin.Contrast ? 0.9f : 0.4f));
                UISkin.Tex(new Rect(r.x + 9, r.y + 9, 22, 22), UISkin.Shape(ToolShapes[ai]), UISkin.Teal);
                GUI.color = oc;
            }
            if (Hud.MagnetCharge >= 0f)
            {
                var mr = new Rect((VW - 240) * 0.5f, y - 6, 240, 10);
                UISkin.Bar(mr, Hud.MagnetCharge, Color.Lerp(UISkin.Teal, UISkin.Accent, Hud.MagnetCharge));
            }
        }

        // ================================================================== Interaktion: Tastensymbol + Wort am Objekt
        void DrawPromptsCompact(GameApp app, float bottom, int level)
        {
            if (Hud.Progress >= 0f)
            {
                var r = new Rect((VW - 300) * 0.5f, bottom - 72, 300, 30);
                UISkin.RoundRect(r, new Color(0, 0, 0, 0.5f));
                UISkin.Bar(new Rect(r.x + 10, r.y + 20, r.width - 20, 6), Hud.Progress, UISkin.Accent);
                GUI.Label(new Rect(r.x, r.y, r.width, 20), (Hud.ProgressLabel ?? "") + "  " + (Hud.Progress * 100).ToString("0") + " %", SmallCenter());
            }
            if (level == Settings.HintsOff) return;
            Vector2 anchor = new Vector2(VW * 0.5f, bottom - 110f);
            bool anchored = false;
            string word = Hud.PromptWord;
            string key = Hud.PromptKey;
            if (word == null && key == null && !string.IsNullOrEmpty(Hud.Prompt)) word = Hud.Prompt;
            if (word != null || key != null)
            {
                Vector2 p;
                if (Hud.PromptAt.HasValue && WorldToHud(Hud.PromptAt.Value, out p)) { anchor = p; anchored = true; }
                anchor = DrawChip(anchor, key != null ? ShortKey(key) : null, word, Hud.PromptHold, key == null, false);
            }
            string bl = L(Hud.BlockedShort ?? Hud.Blocked);
            if (!string.IsNullOrEmpty(bl))
            {
                Vector2 bp = anchor + new Vector2(0f, anchored || word != null || key != null ? 30f : 0f);
                Vector2 p;
                if (word == null && key == null && Hud.BlockedAt.HasValue && WorldToHud(Hud.BlockedAt.Value, out p)) bp = p;
                DrawChip(bp, null, "⚠ " + bl, false, false, true);
            }
        }

        /// <summary>Kleines Hinweisschild, zentriert über dem Punkt. Liefert den Mittelpunkt (für ein zweites Schild darunter).</summary>
        Vector2 DrawChip(Vector2 at, string key, string word, bool hold, bool dim, bool bad)
        {
            var st = UISkin.LabelSmall;
            float ww = string.IsNullOrEmpty(word) ? 0f : UISkin.TextWidth(st, word);
            float kw = 0f;
            if (key != null) kw = Mathf.Max(24f, UISkin.TextWidth(UISkin.Key, key) + 8f) + (hold ? 16f : 0f) + (ww > 0 ? 8f : 0f);
            float w = kw + ww + 20f, h = 30f;
            float x = Mathf.Clamp(at.x - w * 0.5f, 10f, VW - w - 10f);
            float y = Mathf.Clamp(at.y - h * 0.5f, 60f, VH - 150f);
            var r = new Rect(x, y, w, h);
            UISkin.RoundRect(r, bad ? new Color(0.35f, 0.06f, 0.04f, UISkin.Contrast ? 1f : 0.78f) : new Color(0.02f, 0.07f, 0.09f, UISkin.Contrast ? 0.95f : dim ? 0.45f : 0.68f));
            float cx = r.x + 10f;
            if (key != null)
            {
                float k = UISkin.KeyCap(cx, r.y + 4, key, 22);
                cx += k;
                if (hold) { UISkin.Tex(new Rect(cx + 2, r.y + 9, 12, 12), UISkin.Shape("ring"), UISkin.Accent); cx += 16f; }
                cx += ww > 0 ? 8f : 0f;
            }
            if (ww > 0) GUI.Label(new Rect(cx, r.y, ww + 4, h), bad ? UISkin.Col(word, UISkin.Contrast ? UISkin.Warn : Color.white) : dim ? UISkin.Col(word, UISkin.TextDim) : word, st);
            return new Vector2(r.center.x, r.center.y);
        }

        // ================================================================== Unterschlupf-Warnung (kompakt)
        void DrawShelterCompact(GameApp app, WorldState w, PlayerData me, PlanetDef pd, int level)
        {
            bool storm = w.Cur.StormActive;
            string title = storm ? pd.StormName + "!" : L("Nacht");
            string dist = Hud.ShelterDist >= 0 ? L("  Unterschlupf ") + Hud.ShelterDist.ToString("0") + " m" : "";
            float bw = 420f;
            var r = new Rect((VW - bw) * 0.5f, 70, bw, level > 0 ? 64 : 38);
            UISkin.RoundRect(r, new Color(UISkin.Bad.r * 0.35f, UISkin.Bad.g * 0.1f, 0.02f, UISkin.Contrast ? 1f : 0.72f));
            if (Hud.ShelterDist >= 0)
            {
                var sp = Hud.ShelterPos;
                var mp = PlayerController.I != null ? PlayerController.I.RenderPos : new Vector3(me.Pos.x, me.Pos.y, me.Pos.z);
                float ang = Mathf.Atan2(sp.x - mp.x, sp.z - mp.z) * Mathf.Rad2Deg - Hud.CameraYaw;
                DrawRotated(new Rect(r.x + 12, r.y + 6, 26, 26), UISkin.Arrow, ang, UISkin.Accent);
            }
            GUI.Label(new Rect(r.x + 46, r.y + 4, bw - 56, 30), "⚠ <b>" + title + "</b>" + UISkin.Col(dist, UISkin.Text), UISkin.LabelSmall);
            if (level > 0)
            {
                float x = r.x + 46;
                x += UISkin.KeyCap(x, r.y + 36, ShortKey(InputMap.Label(GameAction.Shelter)), 20) + 6;
                GUI.Label(new Rect(x, r.y + 33, 180, 26), UISkin.Col(L("Notunterschlupf ") + Rules.ShelterCost, UISkin.TextDim), UISkin.LabelTiny);
                x += 176;
                x += UISkin.KeyCap(x, r.y + 36, ShortKey(InputMap.Label(GameAction.Sleep)), 20) + 6;
                GUI.Label(new Rect(x, r.y + 33, 120, 26), UISkin.Col(L("Schlafen"), UISkin.TextDim), UISkin.LabelTiny);
            }
        }

        // ================================================================== Meldungen kürzen und zusammenfassen
        /// <summary>
        /// Wertet neue Meldungen nach der Hinweisstufe aus (kürzen, unterdrücken, gleichartige zusammenfassen).
        /// Meldungen und Kurzformen sind deutsch (Kanon, wie vom Server); übersetzt wird erst beim Zeichnen (Loc-Vorlagen).
        /// </summary>
        void ProcessToasts(GameApp app)
        {
            var list = Hud.Toasts;
            if (list.Count == 0) return;
            int level = HintLevel(app);
            float now = Time.unscaledTime;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.Seen) continue;
                t.Seen = true;
                if (level == Settings.HintsFull) { t.Shown = null; continue; }
                string key; long val;
                string sh = ShortToast(t, level, out key, out val);
                if (sh == null) { t.Hidden = true; continue; }
                t.Shown = sh;
                t.MergeKey = key;
                t.Sum = val;
                float cap = t.Kind == ToastKind.Story ? 5.5f : t.Kind == ToastKind.Warning || t.Kind == ToastKind.Error ? 4f : 2.8f;
                t.Duration = Mathf.Min(t.Duration, cap);
                // Gleichartige, noch sichtbare Meldung aktualisieren statt eine neue zu zeigen
                for (int j = 0; j < list.Count; j++)
                {
                    var o = list[j];
                    if (o == t || o.Hidden || !o.Seen || now - o.Created > o.Duration) continue;
                    if (key != null && o.MergeKey == key)
                    {
                        o.Sum += val; o.Count++;
                        o.Shown = key == "credits" ? "+" + Num(o.Sum) + " Credits" : o.Shown;
                        o.Created = now; o.Duration = Mathf.Max(o.Duration, t.Duration);
                        t.Hidden = true;
                        break;
                    }
                    if (key == null && o.Shown == sh)
                    {
                        o.Count++; o.Created = now;
                        t.Hidden = true;
                        break;
                    }
                }
                if (!t.Hidden && sh.StartsWith("Neu: ", StringComparison.Ordinal) && t.Text.StartsWith("Neuer Auftrag", StringComparison.Ordinal)) ShowObjective(7f);
            }
        }

        static string Quoted(string s)
        {
            int a = s.IndexOf('„'), b = a >= 0 ? s.IndexOf('“', a + 1) : -1;
            return a >= 0 && b > a ? s.Substring(a + 1, b - a - 1) : s;
        }

        /// <summary>Erste Zahl hinter einem „+“ (z. B. „… +25 Credits“).</summary>
        static long PlusNumber(string s)
        {
            int i = s.IndexOf('+');
            if (i < 0) return 0;
            long v = 0; bool any = false;
            for (int k = i + 1; k < s.Length; k++)
            {
                char c = s[k];
                if (c >= '0' && c <= '9') { v = v * 10 + (c - '0'); any = true; }
                else if ((c == '.' || c == ' ' || c == ' ' || c == ' ') && any) continue;
                else break;
            }
            return v;
        }

        /// <summary>Kurzform einer Meldung (null = in dieser Stufe nicht zeigen). key/val: Zusammenfassen gleichartiger Meldungen.</summary>
        static string ShortToast(Toast t, int level, out string key, out long val)
        {
            key = null; val = 0;
            string s = (t.Text ?? "").Trim();
            string r = null;
            bool hide = false;
            if (s.StartsWith("+") && s.EndsWith(" Credits")) { val = PlusNumber(s); key = "credits"; r = "+" + Num(val) + " Credits"; }
            else if (s.StartsWith("Gefahrstoffe fachgerecht entsorgt")) { val = PlusNumber(s); key = "credits"; r = "+" + Num(val) + " Credits"; }
            else if (s.StartsWith("Recyclingauftrag erfüllt")) r = "Auftrag erfüllt  +" + Num(PlusNumber(s));
            else if (s.StartsWith("✦ Lichtpunkt „")) r = "✦ " + Quoted(s) + " sauber";
            else if (s.StartsWith("Der Weg nach „")) r = "Weg frei: " + Quoted(s);
            else if (s.Contains(": Hauptmüll entfernt")) r = s.Substring(0, s.IndexOf(": Hauptmüll", StringComparison.Ordinal)) + ": Hauptmüll entfernt";
            else if (s.StartsWith("GROSSPROJEKT abgeschlossen") || s.StartsWith("Projekt abgeschlossen")) r = "Projekt fertig: „" + Quoted(s) + "“";
            else if (s.StartsWith("Auftrag erledigt: "))
            {
                string body = s.Substring(18);
                int p = body.LastIndexOf(" (+", StringComparison.Ordinal);
                r = "✓ " + (p > 0 ? body.Substring(0, p) + "  +" + Num(PlusNumber(body.Substring(p))) : body);
            }
            else if (s.StartsWith("Neuer Auftrag: ")) r = "Neu: " + s.Substring(15);
            else if (s.StartsWith("Kosmetik freigeschaltet: ")) r = "Neu: " + s.Substring(25);
            else if (s.Contains(" zieht auf!")) r = "⚠ " + s.Substring(0, s.IndexOf(" zieht auf!", StringComparison.Ordinal)) + " zieht auf";
            else if (s.Contains("! Ohne Unterschlupf")) r = "⚠ " + s.Substring(0, s.IndexOf("! Ohne Unterschlupf", StringComparison.Ordinal)) + "!";
            else if (s.StartsWith("Der ") && s.Contains(" ist vorbei")) r = s.Substring(4, s.IndexOf(" ist vorbei", StringComparison.Ordinal) - 4) + " vorbei" + (s.Contains("neue Wege") ? " – neue Wege frei" : "");
            else if (s.StartsWith("Die Nacht bricht herein")) r = "Nacht – Unterschlupf suchen";
            else if (s.StartsWith("Ein neuer Morgen bricht an")) hide = true;
            else if (s.StartsWith("Ausgeschlafen!")) r = "Guten Morgen – Akku voll";
            else if (s.StartsWith("Notabschaltung!")) r = "Notabschaltung!";
            else if (s.Contains(" hatte eine Notabschaltung")) r = s.Substring(0, s.IndexOf(" hatte eine", StringComparison.Ordinal)) + " wird abgeschleppt";
            else if (s.StartsWith("Am Stützpunkt angekommen")) r = "Am Stützpunkt – Akku lädt";
            else if (s.StartsWith("Warte auf Mitspieler (")) hide = true; // die Schlafanzeige zeigt es
            else if (s.StartsWith("Notunterschlupf gebaut")) r = "Unterschlupf gebaut";
            else if (s.StartsWith("Fundstück entdeckt: ")) r = "Fundstück: " + s.Substring(20).Replace(" (Archiv)", "");
            else if (s.StartsWith("Eingebaut: ")) { int p = s.IndexOf(" – ", StringComparison.Ordinal); r = p > 0 ? s.Substring(0, p) : s; }
            else if (s.StartsWith("Neues Fahrzeug in der Garage: ")) r = "Neu in der Garage: " + s.Substring(30);
            else if (s.EndsWith(" ist der Sitzung beigetreten.")) r = s.Substring(0, s.Length - 29) + " ist da";
            else if (s.EndsWith(" hat die Sitzung verlassen.")) r = s.Substring(0, s.Length - 27) + " ist gegangen";
            else if (s.StartsWith("Schrottlieferung")) { int a = s.IndexOf('('); r = a > 0 ? "Lieferung: " + s.Substring(a + 1).TrimEnd('.', ')') : "Lieferung ist da"; }
            else if (s.StartsWith("Wrack verwertet")) r = "Wrack verwertet  +" + PlusNumber(s);
            else if (s.StartsWith("Repariert!")) r = "Repariert  +" + PlusNumber(s);
            else if (s.Contains(": Ökologie wiederhergestellt")) r = s.Substring(0, s.IndexOf(": Ökologie", StringComparison.Ordinal)) + ": Leben kehrt zurück";
            else if (s.StartsWith("Wrack angehoben")) r = s.StartsWith("Wrack angehoben – mit") ? s.TrimEnd('!') : "Wrack angehoben";
            else if (s.StartsWith("Wrack liegt auf dem Transportrover")) r = "Wrack auf dem Rover";
            else if (s.StartsWith("Aussichtspunkt gemerkt") || s.StartsWith("Aussichtspunkt gespeichert")) r = "Aussicht gemerkt";
            else if (s.StartsWith("Gespeichert (")) hide = true; // „✓ Gespeichert“ unten rechts genügt
            else if (s.StartsWith("Koop geschlossen")) r = "Koop geschlossen";
            else if (s.StartsWith("Koop geöffnet")) r = "Koop geöffnet";
            if (hide) return null;
            if (level == Settings.HintsOff && (t.Kind == ToastKind.Info || t.Kind == ToastKind.Success)) return null;
            if (r == null) r = GenericShort(s, t.Kind);
            return r;
        }

        /// <summary>Allgemeine Kürzung: Nebensatz nach „ – “ weg, bei langen Texten nur der erste Satz.</summary>
        static string GenericShort(string s, ToastKind kind)
        {
            if (kind == ToastKind.Story && s.Length <= 110) return s;
            int d = s.IndexOf(" – ", StringComparison.Ordinal);
            if (d > 8 && kind != ToastKind.Story) s = s.Substring(0, d);
            if (s.Length > 70)
            {
                int e = s.IndexOfAny(new[] { '.', '!', '?' }, 12);
                if (e > 0 && e < s.Length - 1) s = s.Substring(0, e + (s[e] == '.' ? 0 : 1));
            }
            return s.TrimEnd('.', ' ');
        }

        // ================================================================== Kamerafahrt
        void DrawCinematicHint(GameApp app)
        {
            if (HintLevel(app) == Settings.HintsOff) return;
            var r = new Rect(VW - 250, VH - 44, 230, 28);
            GUI.Label(r, UISkin.Col(L("Beliebige Taste: weiter"), new Color(1, 1, 1, 0.55f)), SmallRight());
        }
    }
}
