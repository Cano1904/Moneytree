using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>HUD im Spiel: Status, Werkzeugleiste, Ziel, Hinweise, Wetter/Uhr, Warnungen, Overlays, Meldungen.</summary>
    public partial class UIRoot
    {
        string objectiveCache;
        float objectiveAt;
        readonly List<PlayerData> hudPlayers = new List<PlayerData>(4);

        static readonly string[] ToolShort = { "Greifarm", "Sauger", "Magnet", "Schneider", "Wärme", "Filter", "Bio" };
        static readonly string[] ToolShapes = { "plus", "drop", "ring", "cross", "sun", "grid", "leaf" };
        static readonly GameAction[] ToolKeys = { GameAction.Tool1, GameAction.Tool2, GameAction.Tool3, GameAction.Tool4, GameAction.Tool5, GameAction.Tool6, GameAction.Tool7 };

        string Objective(WorldState w)
        {
            if (objectiveCache == null || Time.unscaledTime >= objectiveAt)
            {
                objectiveAt = Time.unscaledTime + 0.5f;
                try { objectiveCache = Rules.CurrentObjective(w); } catch (System.Exception e) { objectiveCache = "–"; Debug.LogWarning(e.Message); }
            }
            return objectiveCache;
        }

        void DrawHud(GameApp app)
        {
            var w = app.W;
            var me = app.Me;
            if (w == null || me == null) return;
            var pd = GameData.Planets[w.CurrentPlanet];
            var s = app.Settings;
            bool ev = Event.current.type == EventType.Repaint;

            // ---------------------------------------------------- Warnrand bei Nacht/Sturm ohne Schutz
            if (me.Exposed && ev && me.TowTimer <= 0)
            {
                float pulse = s.ReduceFlashing ? 0.55f : 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                var c = UISkin.Bad; c.a = pulse;
                UISkin.Tex(new Rect(0, 0, VW, VH), UISkin.EdgeGlow, c);
            }

            // ---------------------------------------------------- Oben links: Planet, Credits, Energie, Ladung
            var tl = new Rect(20, 18, 400, 176);
            UISkin.PanelBox(tl);
            int area = PlanetLayout.AreaOf(me.Pos.z);
            var accent = UISkin.FromRgb(pd.Accent);
            GUI.Label(new Rect(tl.x + 18, tl.y + 8, tl.width - 36, 30), UISkin.Col(pd.Name, accent) + UISkin.Col("  ·  " + pd.AreaNames[Mathf.Clamp(area, 0, 2)], UISkin.Text), UISkin.LabelBold);
            UISkin.Tex(new Rect(tl.x + 18, tl.y + 46, 22, 22), UISkin.Shape("dot"), UISkin.Accent);
            UISkin.Tex(new Rect(tl.x + 23, tl.y + 51, 12, 12), UISkin.Shape("ring"), new Color(1, 1, 1, 0.8f));
            GUI.Label(new Rect(tl.x + 48, tl.y + 40, 300, 34), "<b>" + Num(w.Credits) + "</b> " + L("Credits"), UISkin.Label);
            // Energie
            float maxE = Mathf.Max(1f, w.MaxEnergy);
            float ef = Mathf.Clamp01(me.Energy / maxE);
            Color ec = ef < 0.1f ? UISkin.Bad : ef < 0.3f ? UISkin.Warn : UISkin.Teal;
            GUI.Label(new Rect(tl.x + 18, tl.y + 78, 110, 26), L("Energie"), UISkin.LabelSmall);
            UISkin.Bar(new Rect(tl.x + 120, tl.y + 84, tl.width - 230, 14), ef, ec);
            GUI.Label(new Rect(tl.xMax - 104, tl.y + 78, 90, 26), me.Energy.ToString("0") + "/" + maxE.ToString("0"), UISkin.LabelSmall);
            // Ladung
            float vol = Item.Volume(me.Bin), cap = Mathf.Max(1f, w.BinCapacity);
            float lf = Mathf.Clamp01(vol / cap);
            GUI.Label(new Rect(tl.x + 18, tl.y + 106, 110, 26), L("Ladung"), UISkin.LabelSmall);
            UISkin.Bar(new Rect(tl.x + 120, tl.y + 112, tl.width - 230, 14), lf, lf >= 0.99f ? UISkin.Warn : UISkin.Accent);
            GUI.Label(new Rect(tl.xMax - 104, tl.y + 106, 90, 26), vol.ToString("0.#") + "/" + cap.ToString("0"), UISkin.LabelSmall);
            string status;
            if (me.Energy <= 0.01f) status = UISkin.Col("⚠ NOTBETRIEB – langsam, Werkzeuge aus. Zum Stützpunkt laden.", (s.ReduceFlashing || Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f) ? UISkin.Bad : UISkin.Warn);
            else if (lf >= 0.99f) status = UISkin.Col("Behälter voll – einlagern, verkaufen oder pressen " + KeyHint(GameAction.Press), UISkin.Warn);
            else if (Hud.Diving) status = UISkin.Col("Tauchen", UISkin.Teal) + "  " + KeyHint(GameAction.DiveUp) + " auf · " + KeyHint(GameAction.DiveDown) + " ab";
            else if (Hud.Swimming) status = UISkin.Col("Schwimmen", UISkin.Teal) + (w.TechLevel("dive") > 0 ? "  " + KeyHint(GameAction.DiveDown) + " abtauchen" : "");
            else status = UISkin.Col(KeyHint(GameAction.Menu) + " Menü  " + KeyHint(GameAction.Map) + " Karte  " + KeyHint(GameAction.Build) + " Bauen", UISkin.TextDim);
            GUI.Label(new Rect(tl.x + 18, tl.y + 136, tl.width - 30, 30), status, UISkin.LabelTiny);

            // ---------------------------------------------------- Oben Mitte: aktuelles Ziel
            string obj = Objective(w);
            float ow = Mathf.Min(700f, VW - 900f);
            if (ow < 380f) ow = Mathf.Min(560f, VW - 480f);
            if (ow > 200f && !string.IsNullOrEmpty(obj))
            {
                float oh = UISkin.TextHeight(UISkin.WrapSmall, obj, ow - 110) + 18;
                var orr = new Rect((VW - ow) * 0.5f, 18, ow, Mathf.Max(44, oh));
                UISkin.PanelBox(orr);
                GUI.Label(new Rect(orr.x + 16, orr.y + 9, 90, 26), UISkin.Col(L("Ziel").ToUpperInvariant(), UISkin.Accent), UISkin.LabelBold);
                GUI.Label(new Rect(orr.x + 96, orr.y + 9, ow - 110, oh), obj, UISkin.WrapSmall);
            }

            // ---------------------------------------------------- Oben rechts: Uhr, Wetter, Mitspieler, FPS
            DrawClockWeather(app, w, pd, s);

            // ---------------------------------------------------- Unterschlupf-Warnung mit Richtungspfeil
            if (me.Exposed && me.TowTimer <= 0 && !me.Sleeping) DrawShelterWarning(app, w, me, pd);

            // ---------------------------------------------------- Unten: Werkzeugleiste und Hinweise
            float barY = VH - 96f;
            if (!Hud.InVehicle) DrawToolbar(app, w, me, barY);
            else DrawVehicleHud(app, w, me, barY);
            DrawPrompts(app, barY - 12f);

            // Untertitel
            if (s.Subtitles && !string.IsNullOrEmpty(Hud.Subtitle) && Time.unscaledTime < Hud.SubtitleUntil)
            {
                string sub = (string.IsNullOrEmpty(Hud.SubtitleSpeaker) ? "" : UISkin.Col(Hud.SubtitleSpeaker + ": ", UISkin.Accent)) + Hud.Subtitle;
                float sw = Mathf.Min(1100f, VW - 80f);
                float sh = UISkin.TextHeight(UISkin.WrapCenter, sub, sw - 40) + 18;
                var sr = new Rect((VW - sw) * 0.5f, VH * 0.62f, sw, sh);
                UISkin.RoundRect(sr, new Color(0, 0, 0, UISkin.Contrast ? 0.95f : 0.6f));
                GUI.Label(new Rect(sr.x + 20, sr.y + 9, sw - 40, sh), sub, UISkin.WrapCenter);
            }

            // Gespeichert-Anzeige
            if (app.savedFlash > 0)
            {
                var c = UISkin.Good; c.a = Mathf.Clamp01(app.savedFlash);
                var r = new Rect(VW - 210, VH - 56, 190, 36);
                UISkin.RoundRect(r, new Color(0, 0, 0, 0.5f * c.a));
                GUI.Label(r, UISkin.Col("✓ Gespeichert", c), UISkin.LabelCenter);
            }

            // ---------------------------------------------------- Vollbild-Overlays
            if (me.TowTimer > 0) DrawTowOverlay(me);
            else if (me.Sleeping) DrawSleepOverlay(app, w);
        }

        void DrawClockWeather(GameApp app, WorldState w, PlanetDef pd, Settings s)
        {
            float phase = Rules.DayPhase(w, w.CurrentPlanet);
            bool night = Rules.IsNight(phase);
            int mins = (int)(phase * 24f * 60f);
            string clock = (mins / 60).ToString("00") + ":" + (mins % 60).ToString("00");
            var ps = w.Cur;
            float dx, dz;
            float wind = Rules.Wind(w, w.CurrentPlanet, out dx, out dz);
            float h = 108f;
            var r = new Rect(VW - 300, 18, 280, h);
            UISkin.PanelBox(r);
            UISkin.Tex(new Rect(r.x + 16, r.y + 12, 34, 34), UISkin.Shape(night ? "moon" : "sun"), night ? new Color(0.75f, 0.82f, 1f) : UISkin.Warn);
            GUI.Label(new Rect(r.x + 60, r.y + 8, 120, 40), "<b>" + clock + "</b>", UISkin.H2);
            GUI.Label(new Rect(r.x + 160, r.y + 12, 110, 34), night ? "Nacht" : phase < 0.3f ? "Morgen" : phase > 0.72f ? "Abend" : "Tag", UISkin.LabelSmall);
            // Wind: Pfeil (relativ zur Kamera) + Stärke
            float windDeg = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg - Hud.CameraYaw;
            DrawRotated(new Rect(r.x + 20, r.y + 58, 26, 26), UISkin.Arrow, windDeg, UISkin.Teal);
            string ws = "Wind " + (wind * 100).ToString("0") + " %";
            Color wc = UISkin.TextDim;
            if (ps.StormActive) { ws = pd.StormName.ToUpperInvariant() + "!"; wc = UISkin.Bad; }
            else if (ps.StormWarn) { ws = "⚠ " + pd.StormName + " zieht auf"; wc = UISkin.Warn; }
            GUI.Label(new Rect(r.x + 60, r.y + 54, r.width - 70, 34), UISkin.Col(ws, wc), UISkin.LabelSmall);
            float y = r.yMax + 8;

            // Mitspieler (klein)
            hudPlayers.Clear();
            foreach (var p in w.Players.Values) if (p.Online) hudPlayers.Add(p);
            if (hudPlayers.Count > 1)
            {
                var pr = new Rect(r.x, y, r.width, 14 + hudPlayers.Count * 26);
                UISkin.PanelBox(pr);
                float py = pr.y + 7;
                foreach (var p in hudPlayers)
                {
                    CosmeticDef cd;
                    Color col = GameData.Cosmetics.TryGetValue(p.Color ?? "", out cd) ? UISkin.FromRgb(cd.Value) : UISkin.Teal;
                    UISkin.Tex(new Rect(pr.x + 14, py + 5, 14, 14), UISkin.Shape("dot"), col);
                    string st = p.TowTimer > 0 ? UISkin.Col(" (abgeschleppt)", UISkin.Bad) : p.Sleeping ? UISkin.Col(" (schläft)", UISkin.TextDim) : p.Exposed ? UISkin.Col(" (ungeschützt)", UISkin.Warn) : "";
                    GUI.Label(new Rect(pr.x + 36, py, pr.width - 44, 24), p.Name + st, UISkin.LabelTiny);
                    py += 26;
                }
                y = pr.yMax + 8;
            }
            if (s.ShowFps)
            {
                GUI.Label(new Rect(r.x, y, r.width - 10, 24), UISkin.Col(fps.ToString("0") + " FPS", fps < 30 ? UISkin.Warn : UISkin.Good), UISkin.LabelRight);
                y += 26;
            }
            toastTop = y + 6;
        }

        float toastTop = 140f;

        void DrawShelterWarning(GameApp app, WorldState w, PlayerData me, PlanetDef pd)
        {
            bool storm = w.Cur.StormActive;
            string title = storm ? pd.StormName + "! Schnell in einen Unterschlupf" : "Nacht: Suche einen " + pd.ShelterName;
            float bw = Mathf.Min(620f, VW - 200f);
            var r = new Rect((VW - bw) * 0.5f, 110, bw, 118);
            UISkin.PanelBox(r);
            UISkin.RoundRect(new Rect(r.x + 3, r.y + 3, r.width - 6, 38), new Color(UISkin.Bad.r, UISkin.Bad.g, UISkin.Bad.b, 0.35f));
            GUI.Label(new Rect(r.x, r.y + 5, r.width, 34), "⚠ " + title, UISkin.LabelCenter);
            if (Hud.ShelterDist >= 0)
            {
                var sp = Hud.ShelterPos;
                var mp = PlayerController.I != null ? PlayerController.I.RenderPos : new Vector3(me.Pos.x, me.Pos.y, me.Pos.z);
                float ang = Mathf.Atan2(sp.x - mp.x, sp.z - mp.z) * Mathf.Rad2Deg - Hud.CameraYaw;
                DrawRotated(new Rect(r.x + 26, r.y + 52, 52, 52), UISkin.Arrow, ang, UISkin.Accent);
                GUI.Label(new Rect(r.x + 96, r.y + 46, r.width - 110, 30), "Nächster Unterschlupf: <b>" + Hud.ShelterDist.ToString("0") + " m</b>", UISkin.Label);
            }
            GUI.Label(new Rect(r.x + 96, r.y + 76, r.width - 110, 30),
                KeyHint(GameAction.Shelter) + " Notunterschlupf bauen (" + Rules.ShelterCost + " Credits)  ·  " + KeyHint(GameAction.Sleep) + " dort schlafen", UISkin.LabelSmall);
        }

        void DrawToolbar(GameApp app, WorldState w, PlayerData me, float y)
        {
            int n = Rules.ToolIds.Length;
            float sw = 92f, gap = 6f;
            float total = n * sw + (n - 1) * gap;
            float x0 = (VW - total) * 0.5f;
            string active = PlayerController.I != null ? PlayerController.I.Tool : me.Tool;
            for (int i = 0; i < n; i++)
            {
                string id = Rules.ToolIds[i];
                bool has = Rules.HasTool(w, id);
                bool act = id == active;
                var r = new Rect(x0 + i * (sw + gap), y, sw, 78);
                UISkin.Sliced(r, act ? UISkin.BtnSel : has ? UISkin.Panel : UISkin.PanelDark);
                if (act) UISkin.OutlineRect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UISkin.Accent);
                Color ic = !has ? new Color(1, 1, 1, 0.22f) : act ? Color.white : UISkin.Teal;
                UISkin.Tex(new Rect(r.center.x - 15, r.y + 12, 30, 30), UISkin.Shape(ToolShapes[i]), ic);
                GUI.Label(new Rect(r.x, r.y + 46, r.width, 26), has ? ToolShort[i] : UISkin.Col(ToolShort[i], UISkin.TextDim * new Color(1, 1, 1, 0.6f)), SmallCenter());
                UISkin.KeyCap(r.x + 5, r.y + 5, InputMap.Label(ToolKeys[i]), 20);
            }
            // Magnet-Aufladung über der Leiste
            if (Hud.MagnetCharge >= 0f)
            {
                var mr = new Rect((VW - 300) * 0.5f, y - 30, 300, 14);
                UISkin.Bar(mr, Hud.MagnetCharge, Color.Lerp(UISkin.Teal, UISkin.Accent, Hud.MagnetCharge));
                GUI.Label(new Rect(mr.x - 150, mr.y - 6, 140, 26), "Magnetwelle", SmallRight());
            }
        }

        static GUIStyle smallCenter, smallRight;
        static GUIStyle SmallCenter()
        {
            if (smallCenter == null || smallCenter.normal.textColor != UISkin.Text) { smallCenter = new GUIStyle(UISkin.LabelTiny) { alignment = TextAnchor.MiddleCenter }; smallCenter.normal.textColor = UISkin.Text; }
            return smallCenter;
        }
        static GUIStyle SmallRight()
        {
            if (smallRight == null || smallRight.normal.textColor != UISkin.Text) { smallRight = new GUIStyle(UISkin.LabelSmall) { alignment = TextAnchor.MiddleRight }; smallRight.normal.textColor = UISkin.Text; }
            return smallRight;
        }

        void DrawVehicleHud(GameApp app, WorldState w, PlayerData me, float y)
        {
            VehicleState v = null;
            if (Hud.VehicleId != null) w.Cur.Vehicles.TryGetValue(Hud.VehicleId, out v);
            if (v == null) return;
            var def = v.Def;
            float bw = 460f;
            var r = new Rect((VW - bw) * 0.5f, y - 6, bw, 84);
            UISkin.PanelBox(r);
            UISkin.Tex(new Rect(r.x + 16, r.y + 14, 34, 34), UISkin.Shape("truck"), UISkin.Accent);
            GUI.Label(new Rect(r.x + 62, r.y + 8, bw - 80, 30), "<b>" + def.Name + "</b>   " + UISkin.Col(KeyHint(GameAction.Vehicle) + " aussteigen", UISkin.TextDim), UISkin.Label);
            if (def.Capacity > 0)
            {
                float vol = Item.Volume(v.Cargo);
                float f = Mathf.Clamp01(vol / def.Capacity);
                UISkin.Bar(new Rect(r.x + 62, r.y + 46, bw - 190, 14), f, f >= 0.99f ? UISkin.Warn : UISkin.Accent);
                GUI.Label(new Rect(r.xMax - 118, r.y + 40, 104, 26), "Ladung " + vol.ToString("0") + "/" + def.Capacity.ToString("0"), UISkin.LabelTiny);
            }
            else if (v.Carry != null)
                GUI.Label(new Rect(r.x + 62, r.y + 40, bw - 80, 26), UISkin.Col("Wrack am Haken – zum Rover oder Stützpunkt bringen", UISkin.Warn), UISkin.LabelSmall);
            else
                GUI.Label(new Rect(r.x + 62, r.y + 40, bw - 80, 26), "Kran bereit – an ein Wrack heranfahren", UISkin.LabelSmall);
            if (Hud.VehicleStuck)
            {
                var sr = new Rect((VW - 460) * 0.5f, r.y - 48, 460, 40);
                UISkin.RoundRect(sr, new Color(UISkin.Warn.r * 0.4f, UISkin.Warn.g * 0.3f, 0, 0.85f));
                GUI.Label(sr, "Festgefahren?  " + KeyHint(GameAction.VehicleReset) + " Fahrzeug zurücksetzen", UISkin.LabelCenter);
            }
        }

        void DrawPrompts(GameApp app, float bottom)
        {
            float y = bottom;
            float pw = Mathf.Min(760f, VW - 80f);
            if (Hud.Progress >= 0f)
            {
                y -= 44;
                var r = new Rect((VW - 420) * 0.5f, y, 420, 38);
                UISkin.RoundRect(r, new Color(0, 0, 0, 0.55f));
                UISkin.Bar(new Rect(r.x + 12, r.y + 24, r.width - 24, 8), Hud.Progress, UISkin.Accent);
                GUI.Label(new Rect(r.x, r.y + 1, r.width, 22), (Hud.ProgressLabel ?? "") + "  " + (Hud.Progress * 100).ToString("0") + " %", SmallCenter());
            }
            if (!string.IsNullOrEmpty(Hud.Blocked))
            {
                string t = "⚠ " + Hud.Blocked;
                float th = UISkin.TextHeight(UISkin.WrapCenter, t, pw - 40) + 16;
                y -= th + 6;
                var r = new Rect((VW - pw) * 0.5f, y, pw, th);
                UISkin.RoundRect(r, UISkin.Contrast ? Color.black : new Color(0.35f, 0.06f, 0.04f, 0.85f));
                UISkin.OutlineRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), UISkin.Bad);
                GUI.Label(new Rect(r.x + 20, r.y + 8, pw - 40, th), UISkin.Col(t, UISkin.Contrast ? UISkin.Warn : Color.white), UISkin.WrapCenter);
            }
            if (!string.IsNullOrEmpty(Hud.Prompt))
            {
                float th = UISkin.TextHeight(UISkin.WrapCenter, Hud.Prompt, pw - 40) + 14;
                y -= th + 6;
                var r = new Rect((VW - pw) * 0.5f, y, pw, th);
                UISkin.RoundRect(r, new Color(0.02f, 0.08f, 0.1f, UISkin.Contrast ? 0.95f : 0.72f));
                GUI.Label(new Rect(r.x + 20, r.y + 7, pw - 40, th), Hud.Prompt, UISkin.WrapCenter);
            }
        }

        void DrawTowOverlay(PlayerData me)
        {
            if (Event.current.type == EventType.Repaint) UISkin.Rect(new Rect(0, 0, VW, VH), new Color(0.05f, 0.0f, 0.0f, 0.55f));
            var r = CenterRect(760, 170);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x, r.y + 18, r.width, 44), UISkin.Col("NOTABSCHALTUNG", UISkin.Bad), UISkin.H1);
            GUI.Label(new Rect(r.x, r.y + 72, r.width, 30), "Eine Abschleppdrohne bringt MIKO zum Stützpunkt …", UISkin.LabelCenter);
            GUI.Label(new Rect(r.x, r.y + 110, r.width, 30), "Ankunft in etwa " + Mathf.CeilToInt(me.TowTimer) + " s. Die Ladung bleibt erhalten.", UISkin.LabelCenter);
        }

        void DrawSleepOverlay(GameApp app, WorldState w)
        {
            if (Event.current.type == EventType.Repaint) UISkin.Rect(new Rect(0, 0, VW, VH), new Color(0.0f, 0.02f, 0.06f, 0.68f));
            int online = 0, sleeping = 0;
            foreach (var p in w.Players.Values) if (p.Online) { online++; if (p.Sleeping) sleeping++; }
            var r = CenterRect(700, 180);
            float z = Mathf.Repeat(Time.unscaledTime * 0.8f, 3f);
            string zzz = z < 1 ? "Zzz" : z < 2 ? "Zzz …" : "Zzz … …";
            GUI.Label(new Rect(r.x, r.y, r.width, 60), UISkin.Col(zzz, UISkin.Story), UISkin.H1);
            string t = online > 1 && sleeping < online ? "Warte auf Mitspieler (" + sleeping + "/" + online + " schlafen) …" : "MIKO lädt und schläft bis zum Morgen …";
            GUI.Label(new Rect(r.x, r.y + 70, r.width, 30), t, UISkin.LabelCenter);
            GUI.Label(new Rect(r.x, r.y + 110, r.width, 30), UISkin.Col(KeyHint(GameAction.Sleep) + " aufwachen", UISkin.TextDim), UISkin.LabelCenter);
        }

        /// <summary>Zeichnet eine Textur um ihren Mittelpunkt gedreht (Grad, im Uhrzeigersinn, 0 = oben).</summary>
        void DrawRotated(Rect r, Texture tex, float deg, Color c)
        {
            if (Event.current.type != EventType.Repaint || tex == null) return;
            var old = GUI.matrix;
            var p = new Vector3(r.center.x, r.center.y, 0);
            GUI.matrix = old * Matrix4x4.TRS(p, Quaternion.Euler(0, 0, deg), Vector3.one) * Matrix4x4.TRS(-p, Quaternion.identity, Vector3.one);
            var oc = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = oc;
            GUI.matrix = old;
        }

        // ================================================================== Meldungen (Toasts)
        void DrawToasts(GameApp app)
        {
            var list = Hud.Toasts;
            if (list.Count == 0) return;
            float now = Time.unscaledTime;
            float w = Mathf.Min(460f, VW * 0.4f);
            float y = UIState.Screen == UIScreen.None && app.InGame ? toastTop : 20f;
            float x = VW - w - 20;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                float age = now - t.Created;
                if (age > t.Duration || age < 0) continue;
                float a = Mathf.Clamp01((t.Duration - age) / 0.5f) * Mathf.Clamp01(age / 0.15f + 0.2f);
                float th = UISkin.TextHeight(UISkin.Toast, t.Text, w - 44) + 18;
                var r = new Rect(x, y, w, th);
                var oc = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                UISkin.PanelBox(r);
                var kc = UISkin.ToastColor(t.Kind);
                UISkin.RoundRect(new Rect(r.x + 5, r.y + 6, 6, r.height - 12), kc);
                GUI.Label(new Rect(r.x + 24, r.y + 9, w - 40, th), t.Text, UISkin.Toast);
                GUI.color = oc;
                y += th + 8;
                if (y > VH * 0.7f) break;
            }
        }

        /// <summary>Abgelaufene Meldungen entfernen (nicht während des Zeichnens).</summary>
        void PruneToasts()
        {
            float now = Time.unscaledTime;
            for (int i = Hud.Toasts.Count - 1; i >= 0; i--)
            {
                var t = Hud.Toasts[i];
                if (now - t.Created > t.Duration + 0.5f) Hud.Toasts.RemoveAt(i);
            }
        }
    }
}
