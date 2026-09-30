using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// 3D-Karte (Taste M): die echte Welt schräg von oben (<see cref="MapCamera"/>, bildschirmfüllend), darüber auf den
    /// Bildschirm projizierte Symbole mit leichtem Glühen und Standlinie, Bereichsnamen mit Fortschritt, Kompass, halbtransparente
    /// Seitenleiste (Wiederherstellung, Legende, Zoom) und Steuerhinweise. Drehen: Q/E, Maus ziehen, rechter Stick;
    /// Zoom: Mausrad, +/−, LB/RB; Verschieben: WASD, linker Stick, Mitte/Shift + Maus ziehen; C/R3: zu MIKO; N: Norden oben.
    /// Ohne RenderTexture (oder auf Wunsch) bleibt die 2D-Karte.
    /// </summary>
    public partial class UIRoot
    {
        /// <summary>Spieler hat die 2D-Karte gewählt (Umschalter in der Seitenleiste).</summary>
        bool map2D;
        bool mapDragging, mapPanning;
        Texture2D mapGlow;

        bool Map3DAvailable { get { return MapCamera.I == null || !MapCamera.I.Failed; } }
        bool Map3DActive { get { return UIState.Screen == UIScreen.Map && !map2D && MapCamera.I != null && !MapCamera.I.Failed && MapCamera.I.Texture != null; } }

        // ================================================================== Update: Anfordern und Tasten/Sticks
        void UpdateMap3D(GameApp app)
        {
            if (UIState.Screen != UIScreen.Map || app == null || !app.InGame || map2D) { mapDragging = mapPanning = false; return; }
            var mc = MapCamera.Ensure();
            if (mc == null || mc.Failed) return;
            if (mc.Request(Screen.width, Screen.height) == null) return;
            if (capturing != null || UINav.Editing) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float rot = 0f;
            if (Input.GetKey(KeyCode.Q)) rot -= 1f;
            if (Input.GetKey(KeyCode.E)) rot += 1f;
            float rx = InputMap.Axis("RP_RX"), ry = -InputMap.Axis("RP_RY");
            if (Mathf.Abs(rx) > InputMap.PadDeadzone) rot += rx;
            if (Mathf.Abs(rot) > 0.01f) mc.Rotate(rot * 100f * dt);
            if (Mathf.Abs(ry) > InputMap.PadDeadzone) mc.Tilt(-ry * 40f * dt);
            var mv = InputMap.Move();
            if (mv.sqrMagnitude > 0.01f) mc.Pan(mv * (mc.Distance * 0.9f + 20f) * dt);
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.JoystickButton9)) { mc.Recenter(); AudioManager.Ui("ui_hover"); }
            if (Input.GetKeyDown(KeyCode.N)) { mc.NorthUp(); AudioManager.Ui("ui_hover"); }
            if (Input.GetKey(KeyCode.PageUp)) mc.Zoom(Mathf.Pow(0.4f, dt));
            if (Input.GetKey(KeyCode.PageDown)) mc.Zoom(Mathf.Pow(2.5f, dt));
        }

        // ================================================================== Zeichnen
        void DrawMapScreen(GameApp app)
        {
            if (!app.InGame) { UIState.Open(UIScreen.MainMenu); return; }
            var mc = MapCamera.I;
            if (!map2D && mc != null && !mc.Failed && mc.Texture != null)
            {
                if (mc.HasFrame) { DrawMapScreen3D(app, mc); return; }
                // erstes Kartenbild steht noch aus (ein Bild): ruhiger Hintergrund statt kurz aufblitzender 2D-Karte
                if (Event.current.type == EventType.Repaint) UISkin.Rect(new Rect(0, 0, VW, VH), MapCamera.Background);
                return;
            }
            DrawMapScreen2D(app);
        }

        void DrawMapScreen2D(GameApp app)
        {
            Dim(0.6f);
            var r = new Rect(30, 30, VW - 60, VH - 60);
            var inner = Window(r, L("Karte") + "   " + UISkin.Col(KeyHint(GameAction.Map) + L(" schließen"), UISkin.TextDim));
            float side = Mathf.Min(380f, inner.width * 0.3f);
            float ms = Mathf.Min(inner.height, inner.width - side - 30);
            DrawMap(app, new Rect(inner.x, inner.y, ms, ms));
            DrawMapSide(app, new Rect(inner.x + ms + 30, inner.y, inner.width - ms - 30, inner.height), Map3DAvailable ? 1 : 0);
        }

        Rect map3DArea;

        void DrawMapScreen3D(GameApp app, MapCamera mc)
        {
            var w = app.W;
            var me = app.Me;
            string planet = w.CurrentPlanet;
            var l = WorldGen.Get(planet);
            var ps = w.Cur;
            var pd = GameData.Planets[planet];
            var e = Event.current;
            bool rep = e.type == EventType.Repaint;
            if (rep) marks.Clear();
            EnsureMapGlow();

            float panelW = Mathf.Min(400f, VW * 0.3f);
            var panel = new Rect(VW - panelW - 24f, 24f, panelW, VH - 48f);
            map3DArea = new Rect(0, 0, panel.x - 12f, VH);
            var full = new Rect(0, 0, VW, VH);

            // Weltbild, Nebel am Rand, Vignette
            if (rep)
            {
                GUI.DrawTexture(full, mc.Texture, ScaleMode.StretchToFill, false);
                var bg = MapCamera.Background;
                UISkin.Tex(full, UISkin.EdgeGlow, new Color(bg.r, bg.g, bg.b, 0.95f));
                UISkin.Tex(full, UISkin.Vignette, new Color(1f, 1f, 1f, 0.55f));
            }

            // Maus: ziehen = drehen/neigen, Mitte oder Shift + ziehen = verschieben, Rad = Zoom
            bool overMap = map3DArea.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && overMap) { mc.Zoom(e.delta.y > 0 ? 1.15f : 1f / 1.15f); e.Use(); }
            else if (e.type == EventType.MouseDown && overMap)
            {
                if (e.button == 2 || (e.button == 0 && e.shift)) mapPanning = true; else if (e.button <= 1) mapDragging = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && (mapDragging || mapPanning))
            {
                if (mapPanning) mc.Pan(new Vector2(-e.delta.x, e.delta.y) * mc.Distance * 0.0022f);
                else { mc.Rotate(e.delta.x * 0.3f); mc.Tilt(e.delta.y * 0.18f); }
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { mapDragging = mapPanning = false; }

            float isz = Mathf.Clamp(170f / Mathf.Max(1f, mc.Distance), 0.8f, 1.3f);

            // Bereichsnamen mit Fortschritt (auf Höhe von MIKO bzw. der Kartenmitte)
            for (int a = 0; a < 3; a++)
            {
                float az = a == 0 ? -100f : a == 1 ? 0f : 100f;
                float ax = Mathf.Clamp(mc.Pivot.x, -110f, 110f);
                Vector2 p;
                if (!Proj(mc, new Vector3(ax, MapCamera.Ground(planet, ax, az) + 4f, az), out p) || !map3DArea.Contains(p)) continue;
                if (!rep) continue;
                int stage = Rules.AreaStage(w, ps, a);
                float clean = Rules.Cleanliness(ps, a);
                string t = "<b>" + pd.AreaNames[a] + "</b>  " + (clean * 100).ToString("0") + " %";
                float tw = UISkin.TextWidth(UISkin.LabelSmall, t) + 28f;
                var cr = new Rect(p.x - tw * 0.5f, p.y - 22f, tw, 44f);
                UISkin.RoundRect(cr, new Color(0.01f, 0.05f, 0.07f, 0.62f));
                GUI.Label(new Rect(cr.x + 14, cr.y + 2, tw - 20, 26), UISkin.Col(t, stage >= 4 ? UISkin.Good : UISkin.Text), UISkin.LabelSmall);
                UISkin.Bar(new Rect(cr.x + 14, cr.y + 30, tw - 28, 6), clean, stage >= 2 ? UISkin.Good : UISkin.Teal);
            }

            // Versperrte Durchgänge
            for (int g = 0; g < 2; g++)
            {
                var gl = l.Gates[g];
                if (Rules.GateOpen(ps, g) || gl == null || gl.Blocker == null) continue;
                var bb = gl.Blocker;
                Mark3(mc, planet, bb.Cx, bb.Cz, "cross", UISkin.Bad, 24 * isz, L("Versperrt: ") + gl.Hint);
            }

            // Stützpunkt, Schiff, Projektplätze
            var bl = l.Base;
            Mark3(mc, planet, bl.Center.x, bl.Center.z, "house", UISkin.Teal, 28 * isz, L("Stützpunkt (Lager, Verkauf, Werkstatt, Schiff)"));
            Mark3(mc, planet, bl.ShipPad.x, bl.ShipPad.z, "flag", UISkin.Accent, 20 * isz, L("Transportschiff (Reisen)"));
            for (int a = 0; a < 3; a++)
            {
                var site = l.ProjectSites != null && l.ProjectSites.Length > a ? l.ProjectSites[a] : Terrain.ProjectSite(planet, a);
                var pid = GameData.ProjectId(planet, a);
                var pst = ps.Projects[pid];
                var pdef = GameData.Projects[pid];
                Color c = pst.Done ? UISkin.Good : pst.Started ? UISkin.Accent : UISkin.Warn;
                string st = pst.Done ? L("fertig") : pst.Started ? L("im Bau ") + (pst.Progress * 100).ToString("0") + " %" : L("offen");
                Mark3(mc, planet, site.x, site.z, "star", c, 28 * isz, L("Projektplatz: ") + pdef.Name + " (" + st + ")");
            }
            // Lichtpunkte, Reparatur, Öko, Fundstücke, Aussicht, Unterschlüpfe
            foreach (var z in l.Zones)
            {
                bool clean = Rules.ZoneCleared(ps, z.Index);
                Mark3(mc, planet, z.Center.x, z.Center.z, clean ? "dot" : "ring", clean ? UISkin.Good : UISkin.Warn, (clean ? 15 : 19) * isz, L("Lichtpunkt „") + L(z.Name) + L("“") + (clean ? L(" – leuchtet (Schnellreise-Ziel)") : L(" – verschmutzt")));
            }
            foreach (var s in l.Repairs)
            {
                bool done = ps.Repaired.Contains(s.Id);
                Mark3(mc, planet, s.Pos.x, s.Pos.z, "wrench", done ? UISkin.Good : UISkin.Accent, 17 * isz, L(s.Name) + (done ? L(" – repariert") : L(" – defekt")));
            }
            foreach (var s in l.Eco)
            {
                float gr = Rules.EcoGrowth(w, ps, s.Id);
                bool planted = ps.Eco.ContainsKey(s.Id);
                Color c = gr >= 0.999f ? UISkin.Good : planted ? new Color(0.55f, 0.85f, 0.45f) : new Color(0.7f, 0.75f, 0.65f, 0.8f);
                Mark3(mc, planet, s.Pos.x, s.Pos.z, "leaf", c, 16 * isz, L(s.Name) + (gr >= 0.999f ? L(" – gewachsen") : planted ? L(" – wächst ") + (gr * 100).ToString("0") + " %" : L(" – noch leer")));
            }
            foreach (var s in l.LoreSpots)
            {
                bool found = w.Lore.Contains(s.Id);
                Mark3(mc, planet, s.Pos.x, s.Pos.z, "book", found ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : UISkin.Story, 17 * isz, found ? L("Fundstück (gefunden): ") + L(s.Name) : L("Fundstück – noch nicht entdeckt"));
            }
            foreach (var s in l.Viewpoints)
            {
                bool seen = ps.Views.Contains(s.Id);
                Mark3(mc, planet, s.Pos.x, s.Pos.z, "eye", seen ? UISkin.Good : UISkin.Teal, 17 * isz, L(s.Name) + (seen ? L(" – gemerkt (Fotomodus)") : ""));
            }
            foreach (var s in l.Shelters) Mark3(mc, planet, s.Pos.x, s.Pos.z, "house", UISkin.Story, 18 * isz, pd.ShelterName);
            foreach (var s in ps.Shelters) Mark3(mc, planet, s.x, s.z, "house", new Color(0.6f, 0.95f, 1f), 16 * isz, L("Notunterschlupf (selbst gebaut)"));
            FeatureMarks3D(app, mc, isz);
            // Fahrzeuge
            foreach (var v in ps.Vehicles.Values)
            {
                if (!GameData.Vehicles.ContainsKey(v.Id)) continue;
                Mark3(mc, planet, v.Pos.x, v.Pos.z, "truck", UISkin.Accent, 21 * isz, v.Def.Name + (v.Driver != null ? L(" (besetzt)") : ""));
            }

            // Mitspieler und MIKO (Pfeil in Blickrichtung)
            string myId = app.Client != null ? app.Client.Pid : null;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online || p.Id == myId) continue;
                CosmeticDef cd;
                Color col = GameData.Cosmetics.TryGetValue(p.Color ?? "", out cd) ? UISkin.FromRgb(cd.Value) : UISkin.Teal;
                var pos = new Vector3(p.Pos.x, MapCamera.Ground(planet, p.Pos.x, p.Pos.z) + 1.5f, p.Pos.z);
                DrawPlayerArrow(mc, pos, new Vector3(Mathf.Sin(p.Yaw), 0f, Mathf.Cos(p.Yaw)), col, 24f, p.Name, false);
            }
            if (me != null)
            {
                Vector3 myPos = PlayerController.I != null ? PlayerController.I.RenderPos : new Vector3(me.Pos.x, me.Pos.y, me.Pos.z);
                float yawR = Hud.CameraYaw * Mathf.Deg2Rad;
                DrawPlayerArrow(mc, myPos + Vector3.up * 1.5f, new Vector3(Mathf.Sin(yawR), 0f, Mathf.Cos(yawR)), UISkin.Accent, 32f, L("Du (") + (me.Name ?? "MIKO") + ")", true);
            }

            // Kompass (oben rechts über der Karte)
            DrawCompass(mc, new Vector2(map3DArea.xMax - 64f, 74f));

            // Titel (oben links) und Steuerung (unten links)
            if (rep)
            {
                string title = "<b>" + L("Karte") + "</b>  " + UISkin.Col(pd.Name, UISkin.FromRgb(pd.Accent)) + UISkin.Col("  ·  " + pd.Subtitle, UISkin.TextDim);
                float tw = UISkin.TextWidth(UISkin.Label, title) + 40f;
                var tr = new Rect(24f, 24f, Mathf.Min(tw, map3DArea.width - 150f), 50f);
                UISkin.RoundRect(tr, new Color(0.01f, 0.05f, 0.07f, 0.66f));
                UISkin.Rect(new Rect(tr.x + 12, tr.yMax - 6, 46, 3), UISkin.Accent);
                GUI.Label(new Rect(tr.x + 18, tr.y + 6, tr.width - 24, 34), title, UISkin.Label);
                string hint = InputMap.UsingPad
                    ? L("Rechter Stick: drehen/neigen  ·  LB/RB: Zoom  ·  Linker Stick: verschieben  ·  R3: zu MIKO  ·  B/Back: schließen")
                    : L("Q/E oder Maus ziehen: drehen  ·  Mausrad, +/−: Zoom  ·  ") + MoveKeysLabel() + L(": verschieben  ·  C: zu MIKO  ·  N: Norden  ·  ") + InputMap.Label(GameAction.Map) + L("/Esc: schließen");
                float hw = Mathf.Min(UISkin.TextWidth(UISkin.LabelTiny, hint) + 32f, map3DArea.width - 48f);
                var hr = new Rect(24f, VH - 24f - 38f, hw, 38f);
                UISkin.RoundRect(hr, new Color(0.01f, 0.05f, 0.07f, 0.66f));
                GUI.Label(new Rect(hr.x + 16, hr.y + 7, hr.width - 24, 26), UISkin.Col(hint, UISkin.TextDim), UISkin.LabelTiny);
                if (!mc.Following)
                    GUI.Label(new Rect(hr.x + 4, hr.y - 30, 400, 26), UISkin.Col(L("Karte verschoben – ") + (InputMap.UsingPad ? "R3" : "C") + L(": zurück zu MIKO"), UISkin.Teal), UISkin.LabelTiny);
            }

            // Seitenleiste (halbtransparent)
            if (rep)
            {
                UISkin.RoundRect(panel, new Color(0.012f, 0.045f, 0.06f, UISkin.Contrast ? 0.94f : 0.74f));
                UISkin.OutlineRect(panel, new Color(UISkin.Teal.r, UISkin.Teal.g, UISkin.Teal.b, 0.25f));
            }
            DrawMapSide(app, new Rect(panel.x + 22, panel.y + 18, panel.width - 44, panel.height - 32), 2);

            DrawMarkTooltip(map3DArea);
        }

        string MoveKeysLabel()
        {
            return InputMap.Label(GameAction.MoveForward) + InputMap.Label(GameAction.MoveLeft) + InputMap.Label(GameAction.MoveBack) + InputMap.Label(GameAction.MoveRight);
        }

        bool Proj(MapCamera mc, Vector3 world, out Vector2 gui)
        {
            Vector2 uv;
            if (!mc.Project(world, out uv)) { gui = default(Vector2); return false; }
            gui = new Vector2(uv.x * VW, uv.y * VH);
            return true;
        }

        /// <summary>Symbol über einem Weltpunkt: Standlinie zum Boden, weiches Glühen, Schatten, Form; merkt sich den Tooltip.</summary>
        void Mark3(MapCamera mc, string planet, float x, float z, string shape, Color c, float size, string text)
        {
            float g = MapCamera.Ground(planet, x, z);
            float lift = 2.2f + mc.Distance * 0.025f;
            Vector2 top, bottom;
            if (!Proj(mc, new Vector3(x, g + lift, z), out top)) return;
            if (!map3DArea.Contains(top)) return;
            if (Event.current.type != EventType.Repaint) return;
            // weiter entfernte Symbole blassen im Kartennebel aus
            float cd = Vector3.Distance(mc.Cam.transform.position, new Vector3(x, g, z));
            float fade = 1f - Mathf.InverseLerp(mc.Distance * 1.7f, mc.Distance * 2.8f, cd);
            if (fade <= 0.02f) return;
            if (Proj(mc, new Vector3(x, g, z), out bottom)) DrawGuiLine(bottom, top, new Color(c.r, c.g, c.b, 0.55f * fade), 2f);
            if (mapGlow != null) UISkin.Tex(new Rect(top.x - size * 1.2f, top.y - size * 1.2f, size * 2.4f, size * 2.4f), mapGlow, new Color(c.r, c.g, c.b, 0.5f * fade));
            var sh = UISkin.Shape(shape);
            UISkin.Tex(new Rect(top.x - size * 0.5f + 1.5f, top.y - size * 0.5f + 1.5f, size, size), sh, new Color(0, 0, 0, 0.7f * fade));
            UISkin.Tex(new Rect(top.x - size * 0.5f, top.y - size * 0.5f, size, size), sh, new Color(c.r, c.g, c.b, c.a * fade));
            if (text != null && fade > 0.4f) marks.Add(new MapMark { P = top, Text = text });
        }

        void DrawPlayerArrow(MapCamera mc, Vector3 pos, Vector3 dir, Color col, float size, string label, bool me)
        {
            Vector2 p, q;
            if (!Proj(mc, pos, out p) || !map3DArea.Contains(p)) return;
            float ang = 0f;
            if (Proj(mc, pos + dir * 5f, out q) && (q - p).sqrMagnitude > 1e-3f) ang = Mathf.Atan2(q.x - p.x, -(q.y - p.y)) * Mathf.Rad2Deg;
            bool rep = Event.current.type == EventType.Repaint;
            if (rep)
            {
                if (mapGlow != null) UISkin.Tex(new Rect(p.x - size * 1.3f, p.y - size * 1.3f, size * 2.6f, size * 2.6f), mapGlow, new Color(col.r, col.g, col.b, 0.55f));
                if (me)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    float rs = size * (1.35f + 0.25f * pulse);
                    UISkin.Tex(new Rect(p.x - rs * 0.5f, p.y - rs * 0.5f, rs, rs), UISkin.Ring, new Color(1, 1, 1, 0.3f + 0.35f * (1f - pulse)));
                }
            }
            DrawRotated(new Rect(p.x - size * 0.5f - 2f, p.y - size * 0.5f - 2f, size + 4f, size + 4f), UISkin.Arrow, ang, new Color(1, 1, 1, 0.95f));
            DrawRotated(new Rect(p.x - size * 0.5f + 2f, p.y - size * 0.5f + 2f, size - 4f, size - 4f), UISkin.Arrow, ang, col);
            if (rep)
            {
                if (!me) GUI.Label(new Rect(p.x + size * 0.6f, p.y - 11, 180, 22), label, UISkin.LabelTiny);
                marks.Add(new MapMark { P = p, Text = label });
            }
        }

        void DrawCompass(MapCamera mc, Vector2 c)
        {
            if (Event.current.type != EventType.Repaint) return;
            const float R = 34f;
            UISkin.Tex(new Rect(c.x - R, c.y - R, R * 2, R * 2), UISkin.Circle, new Color(0.01f, 0.05f, 0.07f, 0.66f));
            UISkin.Tex(new Rect(c.x - R, c.y - R, R * 2, R * 2), UISkin.Ring, new Color(UISkin.Teal.r, UISkin.Teal.g, UISkin.Teal.b, 0.45f));
            // Richtung Norden auf dem Bildschirm (über die Projektion, stimmt auch bei Neigung)
            Vector2 a, b;
            float ang = -mc.Yaw;
            if (Proj(mc, mc.Pivot, out a) && Proj(mc, mc.Pivot + new Vector3(0, 0, 10f), out b) && (b - a).sqrMagnitude > 1e-3f)
                ang = Mathf.Atan2(b.x - a.x, -(b.y - a.y)) * Mathf.Rad2Deg;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, c);
            UISkin.Tex(new Rect(c.x - 9, c.y - R + 5, 18, 22), UISkin.Arrow, UISkin.Accent);
            UISkin.Tex(new Rect(c.x - 7, c.y + R - 25, 14, 18), UISkin.Arrow, new Color(1, 1, 1, 0.25f));
            GUI.matrix = old;
            float rad = ang * Mathf.Deg2Rad;
            var np = c + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * (R + 14f);
            GUI.Label(new Rect(np.x - 15, np.y - 13, 30, 26), UISkin.Col("<b>N</b>", UISkin.Accent), UISkin.LabelCenter);
        }

        /// <summary>Linie in Oberflächenkoordinaten (gedrehtes, dünnes Rechteck).</summary>
        void DrawGuiLine(Vector2 a, Vector2 b, Color c, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            UISkin.Rect(new Rect(a.x, a.y - width * 0.5f, len, width), c);
            GUI.matrix = old;
        }

        void EnsureMapGlow()
        {
            if (mapGlow != null) return;
            const int s = 64;
            mapGlow = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                    float a = (1f - d) * (1f - d);
                    px[y * s + x] = new Color(1, 1, 1, a);
                }
            mapGlow.SetPixels(px);
            mapGlow.Apply();
        }

        /// <summary>Tooltip zum nächstgelegenen Symbol unter der Maus (2D- und 3D-Karte).</summary>
        void DrawMarkTooltip(Rect area)
        {
            var e = Event.current;
            if (e.type != EventType.Repaint || !area.Contains(e.mousePosition)) return;
            float best = 18f * 18f; int bi = -1;
            for (int i = 0; i < marks.Count; i++)
            {
                float d = (marks[i].P - e.mousePosition).sqrMagnitude;
                if (d < best) { best = d; bi = i; }
            }
            if (bi < 0) return;
            string t = marks[bi].Text;
            float tw = Mathf.Min(420f, UISkin.TextWidth(UISkin.LabelSmall, t) + 24);
            float th = UISkin.TextHeight(UISkin.WrapSmall, t, tw - 20) + 12;
            var tr = new Rect(e.mousePosition.x + 16, e.mousePosition.y + 12, tw, th);
            if (tr.xMax > VW - 10) tr.x = e.mousePosition.x - tw - 12;
            if (tr.yMax > VH - 10) tr.y = e.mousePosition.y - th - 12;
            UISkin.RoundRect(tr, new Color(0, 0, 0, 0.88f));
            GUI.Label(new Rect(tr.x + 10, tr.y + 6, tw - 20, th), t, UISkin.WrapSmall);
        }
    }
}
