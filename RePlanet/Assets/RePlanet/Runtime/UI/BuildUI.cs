using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Bauansicht: Palette aller Bauwerke (Kosten, Energie, Anzahl, Sperrgrund), Platzierungsstatus, Umsetzen/Abreißen.
    /// Alle anklickbaren Elemente liegen im unteren Bildschirmstreifen (&lt; 22 % Höhe), den PlayerController
    /// beim Platzieren per Mausklick ausspart.
    /// </summary>
    public partial class UIRoot
    {
        int buildPage;
        int buildSel = -1;
        float buildScroll;

        /// <summary>Mausrad/Bild↑↓/LB/RB wechseln das Bauwerk (aus Update, nur in der Bauansicht).</summary>
        void UpdateBuildInput(GameApp app)
        {
            if (!BuildMode.Active || UIState.Screen != UIScreen.None || app.W == null) return;
            if (BuildMode.HoverBuildingId >= 0) buildSel = BuildMode.HoverBuildingId;
            if (Input.GetMouseButtonDown(1)) buildSel = -1;
            int d = 0;
            float sc = InputMap.Scroll();
            if (sc > 0.01f) d = -1; else if (sc < -0.01f) d = 1;
            if (Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.JoystickButton5)) d = 1;
            if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.JoystickButton4)) d = -1;
            if (d == 0) return;
            var order = GameData.BuildingOrder;
            int idx = BuildMode.Type != null ? order.IndexOf(BuildMode.Type) : -1;
            idx = idx < 0 ? (d > 0 ? 0 : order.Count - 1) : (idx + d + order.Count) % order.Count;
            BuildMode.Type = order[idx];
            BuildMode.MoveId = -1;
            int per = Mathf.Max(1, buildPerPage);
            buildPage = idx / per;
            AudioManager.Ui("ui_hover");
        }

        int buildPerPage = 6;

        void DrawBuild(GameApp app)
        {
            var w = app.W;
            if (w == null) return;
            var ps = w.Cur;
            if (buildSel >= 0 && ps.Buildings.Find(b => b.Id == buildSel) == null) buildSel = -1;

            // ---------------------------------------------------- Info oben links (nicht anklickbar)
            var en = Rules.Energy(ps);
            var ir = new Rect(20, 210, 420, 214);
            UISkin.PanelBox(ir);
            GUI.Label(new Rect(ir.x + 18, ir.y + 10, ir.width - 36, 32), UISkin.Col("BAUANSICHT", UISkin.Accent), UISkin.H3);
            Color ec = en.Efficiency >= 0.999f ? UISkin.Good : en.Efficiency >= 0.6f ? UISkin.Warn : UISkin.Bad;
            GUI.Label(new Rect(ir.x + 18, ir.y + 44, ir.width - 36, 26), "Energie: " + UISkin.Col("+" + en.Supply.ToString("0.#"), UISkin.Good) + " / " + UISkin.Col("−" + en.Demand.ToString("0.#"), UISkin.Warn)
                + "  → Anlagen " + UISkin.Col((en.Efficiency * 100).ToString("0") + " %", ec), UISkin.LabelSmall);
            GUI.Label(new Rect(ir.x + 18, ir.y + 70, ir.width - 36, 26), "Lager: " + ps.StorageUsed() + " / " + ps.StorageCap() + " Einheiten · " + Num(w.Credits) + " Credits", UISkin.LabelSmall);
            string status;
            if (BuildMode.MoveId >= 0) status = UISkin.Col("Umsetzen: neuen Platz wählen", UISkin.Accent);
            else if (string.IsNullOrEmpty(BuildMode.Type)) status = UISkin.Col("Bauwerk unten auswählen", UISkin.TextDim);
            else if (!BuildMode.HasCursor) status = UISkin.Col("Mauszeiger auf die Baufläche richten", UISkin.TextDim);
            else if (BuildMode.Valid) status = UISkin.Col("✓ Hier platzierbar", UISkin.Good);
            else status = UISkin.Col("✗ " + (BuildMode.Reason ?? "Nicht platzierbar"), UISkin.Bad);
            float sh = UISkin.TextHeight(UISkin.WrapSmall, status, ir.width - 36);
            GUI.Label(new Rect(ir.x + 18, ir.y + 100, ir.width - 36, sh + 4), status, UISkin.WrapSmall);
            string hints = "Linksklick/" + InputMap.Label(GameAction.Interact) + ": platzieren · " + InputMap.Label(GameAction.RotateBuild) + ": drehen\nRechtsklick: Auswahl aufheben · Mausrad/Bild↑↓: Bauwerk wechseln\n"
                + InputMap.Label(GameAction.Build) + "/Esc: Bauansicht verlassen";
            GUI.Label(new Rect(ir.x + 18, ir.y + 140, ir.width - 36, 70), UISkin.Col(hints, UISkin.TextDim), UISkin.WrapSmall);

            // ---------------------------------------------------- Unterer Streifen
            float bandH = Mathf.Floor(VH * 0.205f);
            var band = new Rect(10, VH - bandH, VW - 20, bandH - 8);
            UISkin.PanelBox(band);
            float pad = 10f;
            float leftW = Mathf.Min(300f, band.width * 0.24f);
            var left = new Rect(band.x + pad, band.y + pad, leftW, band.height - pad * 2);
            DrawBuildSelection(app, left);

            float exitW = 150f;
            var exitR = new Rect(band.xMax - pad - exitW, band.y + pad, exitW, left.height);
            if (GUI.Button(exitR, "Bauansicht\nverlassen", UISkin.ButtonSmall)) { AudioManager.Ui("ui_back"); ExitBuild(); return; }

            var pal = new Rect(left.xMax + pad * 2, band.y + pad, exitR.x - left.xMax - pad * 4, left.height);
            var order = GameData.BuildingOrder;
            float cardW = 196f;
            float navW = 36f;
            buildPerPage = Mathf.Max(1, Mathf.FloorToInt((pal.width - navW * 2 - 8) / (cardW + 8)));
            int pages = Mathf.Max(1, Mathf.CeilToInt(order.Count / (float)buildPerPage));
            buildPage = Mathf.Clamp(buildPage, 0, pages - 1);
            if (GUI.Button(new Rect(pal.x, pal.y, navW, pal.height), "‹", buildPage > 0 ? UISkin.ButtonSmall : UISkin.ButtonOff) && buildPage > 0) { buildPage--; AudioManager.Ui("ui_click"); }
            if (GUI.Button(new Rect(pal.xMax - navW, pal.y, navW, pal.height), "›", buildPage < pages - 1 ? UISkin.ButtonSmall : UISkin.ButtonOff) && buildPage < pages - 1) { buildPage++; AudioManager.Ui("ui_click"); }
            float cx = pal.x + navW + 8;
            for (int i = buildPage * buildPerPage; i < Mathf.Min(order.Count, (buildPage + 1) * buildPerPage); i++)
            {
                var def = GameData.Buildings[order[i]];
                var cr = new Rect(cx, pal.y, cardW, pal.height);
                DrawBuildCard(app, w, ps, def, cr);
                cx += cardW + 8;
            }
            if (pages > 1) GUI.Label(new Rect(pal.x, band.y - 26, pal.width, 24), UISkin.Col("Seite " + (buildPage + 1) + "/" + pages, UISkin.TextDim), UISkin.LabelTiny);
        }

        void DrawBuildCard(GameApp app, WorldState w, PlanetState ps, BuildingDef def, Rect r)
        {
            bool sel = BuildMode.Type == def.Id && BuildMode.MoveId < 0;
            string locked = null;
            if (def.RequiresPlanetProject != null)
            {
                ProjectDef pr;
                if (GameData.Projects.TryGetValue(def.RequiresPlanetProject, out pr) && !w.Planet(pr.Planet).Projects[pr.Id].Done) locked = "Nach „" + pr.Name + "“ (" + GameData.Planets[pr.Planet].Name + ")";
            }
            int count = Rules.CountOf(ps, def.Id);
            bool max = count >= def.MaxCount;
            bool hover = r.Contains(Event.current.mousePosition);
            UISkin.Sliced(r, sel ? UISkin.BtnSel : hover ? UISkin.BtnHover : UISkin.PanelLight);
            float y = r.y + 6;
            float lh = Mathf.Clamp((r.height - 12) / 5f, 18f, 26f);
            UISkin.Tex(new Rect(r.x + 8, y + 3, 16, 16), UISkin.Shape("square_full"), UISkin.FromRgb(def.Color != 0 ? def.Color : 0x888888u));
            GUI.Label(new Rect(r.x + 30, y, r.width - 36, lh), "<b>" + def.Name + "</b>", UISkin.LabelTiny);
            y += lh;
            if (locked != null)
            {
                GUI.Label(new Rect(r.x + 8, y, r.width - 16, r.yMax - y - 4), UISkin.Col("Gesperrt: " + locked, UISkin.Warn), UISkin.WrapSmall);
            }
            else
            {
                bool okC = w.Credits >= def.Cost;
                GUI.Label(new Rect(r.x + 8, y, r.width - 16, lh), UISkin.Col(def.Cost + " Credits", okC ? UISkin.Text : UISkin.Bad) + UISkin.Col("  · " + def.W + "×" + def.H, UISkin.TextDim), UISkin.LabelTiny);
                y += lh;
                float mx = r.x + 8;
                foreach (var kv in def.Mats)
                {
                    int have = ps.Available(kv.Key);
                    UISkin.MaterialIcon(new Rect(mx, y + 2, lh - 4, lh - 4), kv.Key);
                    string t = kv.Value.ToString();
                    GUI.Label(new Rect(mx + lh - 2, y, 50, lh), UISkin.Col(t, have >= kv.Value ? UISkin.Text : UISkin.Bad), UISkin.LabelTiny);
                    mx += lh + UISkin.TextWidth(UISkin.LabelTiny, t) + 8;
                    if (mx > r.xMax - 30) break;
                }
                y += lh;
                string e = def.EnergyGen > 0 ? UISkin.Col("Energie +" + def.EnergyGen.ToString("0.#"), UISkin.Good) : def.EnergyUse > 0 ? UISkin.Col("Energie −" + def.EnergyUse.ToString("0.#"), UISkin.Warn) : UISkin.Col("keine Energie", UISkin.TextDim);
                GUI.Label(new Rect(r.x + 8, y, r.width - 16, lh), e, UISkin.LabelTiny);
                y += lh;
                GUI.Label(new Rect(r.x + 8, y, r.width - 16, lh), UISkin.Col(count + "/" + def.MaxCount + " gebaut", max ? UISkin.Warn : UISkin.TextDim), UISkin.LabelTiny);
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (locked != null) { Hud.Show("Gesperrt: " + locked, ToastKind.Info, 3f); AudioManager.Ui("beep_error"); }
                else if (max) { Hud.Show("Maximal " + def.MaxCount + "× " + def.Name + " pro Stützpunkt.", ToastKind.Info, 3f); AudioManager.Ui("beep_error"); }
                else { BuildMode.Type = def.Id; BuildMode.MoveId = -1; AudioManager.Ui("ui_click"); }
            }
            if (hover && Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(def.Desc))
            {
                float tw = 420f;
                float th = UISkin.TextHeight(UISkin.WrapSmall, def.Desc, tw - 20) + 14;
                var tr = new Rect(Mathf.Min(r.x, VW - tw - 10), r.y - th - 36, tw, th);
                UISkin.RoundRect(tr, new Color(0, 0, 0, 0.88f));
                GUI.Label(new Rect(tr.x + 10, tr.y + 7, tw - 20, th), def.Desc, UISkin.WrapSmall);
            }
        }

        void DrawBuildSelection(GameApp app, Rect r)
        {
            var ps = app.W.Cur;
            UISkin.Sliced(r, UISkin.PanelDark);
            int id = BuildMode.MoveId >= 0 ? BuildMode.MoveId : buildSel;
            Building b = id >= 0 ? ps.Buildings.Find(x => x.Id == id) : null;
            if (b == null)
            {
                GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16), UISkin.Col("Gebäude mit der Maus berühren, um es umzusetzen oder abzureißen.", UISkin.TextDim), UISkin.WrapSmall);
                return;
            }
            var def = b.Def;
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 26), "<b>" + def.Name + "</b>", UISkin.LabelSmall);
            string st = def.Machine ? (b.Connected ? UISkin.Col("verbunden", UISkin.Good) : UISkin.Col("nicht verbunden – Förderband zum Lager legen", UISkin.Warn)) : UISkin.Col(def.Category, UISkin.TextDim);
            GUI.Label(new Rect(r.x + 12, r.y + 32, r.width - 24, 40), st, UISkin.WrapSmall);
            float bh = Mathf.Min(38f, (r.height - 84) * 0.5f);
            if (bh < 24) bh = 24;
            float by = r.yMax - bh * 2 - 12;
            bool moving = BuildMode.MoveId >= 0;
            if (GUI.Button(new Rect(r.x + 10, by, r.width - 20, bh), moving ? "Umsetzen abbrechen" : "Umsetzen", UISkin.ButtonSmall))
            {
                AudioManager.Ui("ui_click");
                BuildMode.MoveId = moving ? -1 : b.Id;
            }
            if (confirm == "demolish:" + b.Id)
            {
                float hw = (r.width - 26) * 0.5f;
                if (GUI.Button(new Rect(r.x + 10, by + bh + 6, hw, bh), "Ja, abreißen", UISkin.ButtonSel))
                {
                    AudioManager.Ui("ui_click");
                    confirm = null;
                    BuildMode.MoveId = b.Id;
                    BuildMode.RequestDemolish = true;
                    buildSel = -1;
                }
                if (GUI.Button(new Rect(r.x + 16 + hw, by + bh + 6, hw, bh), "Nein", UISkin.ButtonSmall)) { confirm = null; AudioManager.Ui("ui_back"); }
            }
            else
            {
                bool allowed = app.IsHost || app.W.TrustGuests;
                if (GUI.Button(new Rect(r.x + 10, by + bh + 6, r.width - 20, bh), allowed ? "Abreißen (50 % zurück)" : "Abreißen: nur Host", allowed ? UISkin.ButtonSmall : UISkin.ButtonOff))
                {
                    if (allowed) { AudioManager.Ui("ui_click"); confirm = "demolish:" + b.Id; }
                    else { Hud.Show("Abriss ist dem Host vorbehalten (Vertrauensmodus aus).", ToastKind.Info, 3f); AudioManager.Ui("beep_error"); }
                }
            }
        }
    }
}
