using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// 2D-Karte des aktuellen Planeten. Der Hintergrund (Gelände, Wasser, Straßen, Gebäude) wird einmal pro Planet
    /// über mehrere Frames erzeugt; Marker (Stützpunkt, Projekte, Lichtpunkte, Unterschlüpfe, Spieler …) werden live gezeichnet.
    /// </summary>
    public partial class UIRoot
    {
        const int MapRes = 256;
        const float MapHalf = 150f;

        class MapTex
        {
            public string Planet;
            public Texture2D Tex;
            public float[] H;
            public int Row;
            public bool Done;
        }

        readonly Dictionary<string, MapTex> maps = new Dictionary<string, MapTex>();
        float mapZoom = 1f;
        static readonly float[] ZoomLevels = { 1f, 1.5f, 2f, 3f, 4f };

        struct MapMark { public Vector2 P; public string Text; }
        readonly List<MapMark> marks = new List<MapMark>(128);

        // ================================================================== Erzeugung
        void UpdateMapBuild(GameApp app)
        {
            if (app == null || !app.InGame) return;
            var planet = app.W.CurrentPlanet;
            MapTex m;
            if (!maps.TryGetValue(planet, out m))
            {
                m = new MapTex { Planet = planet, H = new float[MapRes * MapRes] };
                maps[planet] = m;
            }
            if (m.Done) return;
            // Schneller, solange die Karte offen ist
            bool visible = UIState.Screen == UIScreen.Map || (UIState.Screen == UIScreen.Menu && menuTab == "map");
            int rows = visible ? 64 : 12;
            for (int i = 0; i < rows && m.Row < MapRes; i++, m.Row++)
            {
                int z = m.Row;
                float wz = -MapHalf + (z + 0.5f) / MapRes * MapHalf * 2f;
                for (int x = 0; x < MapRes; x++)
                {
                    float wx = -MapHalf + (x + 0.5f) / MapRes * MapHalf * 2f;
                    m.H[z * MapRes + x] = Terrain.HeightAt(planet, wx, wz);
                }
            }
            if (m.Row >= MapRes) FinishMap(m);
        }

        void FinishMap(MapTex m)
        {
            var pd = GameData.Planets[m.Planet];
            var l = WorldGen.Get(m.Planet);
            var px = new Color32[MapRes * MapRes];
            float water = Terrain.WaterLevel(m.Planet);
            Color g1 = UISkin.FromRgb(pd.Ground), g2 = UISkin.FromRgb(pd.Ground2);
            Color shallow = new Color(0.30f, 0.62f, 0.72f), deep = new Color(0.06f, 0.20f, 0.33f);
            float hmax = 0.01f;
            foreach (var h in m.H) if (h > hmax) hmax = h;
            for (int z = 0; z < MapRes; z++)
                for (int x = 0; x < MapRes; x++)
                {
                    float h = m.H[z * MapRes + x];
                    Color c;
                    if (h < water)
                    {
                        c = Color.Lerp(shallow, deep, Mathf.Clamp01((water - h) / 8f));
                    }
                    else
                    {
                        float t = Mathf.Clamp01(h / Mathf.Max(3f, hmax));
                        c = Color.Lerp(g1, g2, 0.35f + 0.5f * t);
                        float hl = m.H[z * MapRes + Mathf.Max(0, x - 1)], hr = m.H[z * MapRes + Mathf.Min(MapRes - 1, x + 1)];
                        float hd = m.H[Mathf.Max(0, z - 1) * MapRes + x], hu = m.H[Mathf.Min(MapRes - 1, z + 1) * MapRes + x];
                        float shade = ((hl - hr) + (hu - hd)) * 0.18f;
                        c *= Mathf.Clamp(1f + shade, 0.6f, 1.35f);
                        c *= 0.82f;
                    }
                    c.a = 1f;
                    px[z * MapRes + x] = c;
                }
            // Straßen
            var road = new Color(0.36f, 0.37f, 0.40f, 1f);
            foreach (var r in l.Roads)
            {
                if (r == null || r.Length < 5) continue;
                FillRect(px, Mathf.Min(r[0], r[2]) - r[4] * 0.5f, Mathf.Min(r[1], r[3]) - r[4] * 0.5f, Mathf.Max(r[0], r[2]) + r[4] * 0.5f, Mathf.Max(r[1], r[3]) + r[4] * 0.5f, road, 0.75f);
            }
            // Gebäude, Mauern
            foreach (var b in l.Colliders)
            {
                if (b.Gate >= 0 || b.DuneSet >= 0 || b.Kind == "gateblock") continue;
                if (Mathf.Abs(b.Cx) > MapHalf + 2 || Mathf.Abs(b.Cz) > MapHalf + 2) continue;
                Color c = b.Color != 0 ? UISkin.FromRgb(b.Color) : new Color(0.5f, 0.5f, 0.52f);
                c = Color.Lerp(c, new Color(0.12f, 0.12f, 0.14f), b.Kind == "areawall" ? 0.35f : 0.2f);
                FillRect(px, b.Cx - b.Hx, b.Cz - b.Hz, b.Cx + b.Hx, b.Cz + b.Hz, c, 1f);
            }
            // Stützpunkt-Fläche
            var bl = l.Base;
            FillRect(px, bl.MinX, bl.MinZ, bl.MaxX, bl.MaxZ, new Color(0.18f, 0.77f, 0.71f), 0.18f);
            if (m.Tex == null)
            {
                m.Tex = new Texture2D(MapRes, MapRes, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            m.Tex.SetPixels32(px);
            m.Tex.Apply();
            m.Done = true;
            m.H = null;
        }

        static void FillRect(Color32[] px, float x0, float z0, float x1, float z1, Color c, float alpha)
        {
            int ix0 = Mathf.Clamp(Mathf.FloorToInt((x0 + MapHalf) / (MapHalf * 2) * MapRes), 0, MapRes - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt((x1 + MapHalf) / (MapHalf * 2) * MapRes) - 1, 0, MapRes - 1);
            int iz0 = Mathf.Clamp(Mathf.FloorToInt((z0 + MapHalf) / (MapHalf * 2) * MapRes), 0, MapRes - 1);
            int iz1 = Mathf.Clamp(Mathf.CeilToInt((z1 + MapHalf) / (MapHalf * 2) * MapRes) - 1, 0, MapRes - 1);
            for (int z = iz0; z <= iz1; z++)
                for (int x = ix0; x <= ix1; x++)
                {
                    int i = z * MapRes + x;
                    Color o = px[i];
                    px[i] = Color.Lerp(o, c, alpha);
                }
        }

        void ZoomMap(int d)
        {
            if (Map3DActive) { MapCamera.I.Zoom(d > 0 ? 0.78f : 1f / 0.78f); AudioManager.Ui("ui_hover"); return; }
            int idx = 0;
            for (int i = 0; i < ZoomLevels.Length; i++) if (Mathf.Abs(ZoomLevels[i] - mapZoom) < 0.01f) idx = i;
            idx = Mathf.Clamp(idx + d, 0, ZoomLevels.Length - 1);
            if (Mathf.Abs(ZoomLevels[idx] - mapZoom) > 0.01f) AudioManager.Ui("ui_hover");
            mapZoom = ZoomLevels[idx];
        }

        // ================================================================== Zeichnen
        Rect mapRect;
        float mapCx, mapCz, mapHs;

        Vector2 W2M(float x, float z)
        {
            return new Vector2(mapRect.x + (x - (mapCx - mapHs)) / (mapHs * 2f) * mapRect.width,
                               mapRect.yMax - (z - (mapCz - mapHs)) / (mapHs * 2f) * mapRect.height);
        }

        bool InMap(Vector2 p) { return p.x >= mapRect.x - 2 && p.x <= mapRect.xMax + 2 && p.y >= mapRect.y - 2 && p.y <= mapRect.yMax + 2; }

        void Mark(Vector2 p, string shape, Color c, float size, string text)
        {
            if (!InMap(p)) return;
            if (Event.current.type == EventType.Repaint)
            {
                var sh = UISkin.Shape(shape);
                UISkin.Tex(new Rect(p.x - size * 0.5f + 1.5f, p.y - size * 0.5f + 1.5f, size, size), sh, new Color(0, 0, 0, 0.75f));
                UISkin.Tex(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), sh, c);
                if (text != null) marks.Add(new MapMark { P = p, Text = text });
            }
        }

        /// <summary>Zeichnet die Karte in r (quadratisch empfohlen).</summary>
        void DrawMap(GameApp app, Rect r)
        {
            var w = app.W;
            var me = app.Me;
            if (w == null) return;
            string planet = w.CurrentPlanet;
            var l = WorldGen.Get(planet);
            var ps = w.Cur;
            var pd = GameData.Planets[planet];
            bool rep = Event.current.type == EventType.Repaint;
            if (rep) marks.Clear();

            // Mausrad-Zoom
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
            {
                ZoomMap(e.delta.y < 0 ? 1 : -1);
                e.Use();
            }

            Vector3 myPos = PlayerController.I != null ? PlayerController.I.RenderPos : (me != null ? new Vector3(me.Pos.x, me.Pos.y, me.Pos.z) : Vector3.zero);
            mapRect = r;
            mapHs = MapHalf / Mathf.Max(1f, mapZoom);
            mapCx = Mathf.Clamp(mapZoom > 1.01f ? myPos.x : 0f, -MapHalf + mapHs, MapHalf - mapHs);
            mapCz = Mathf.Clamp(mapZoom > 1.01f ? myPos.z : 0f, -MapHalf + mapHs, MapHalf - mapHs);

            UISkin.Sliced(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), UISkin.PanelDark);
            MapTex mt;
            maps.TryGetValue(planet, out mt);
            if (mt == null || !mt.Done || mt.Tex == null)
            {
                float prog = mt != null ? mt.Row / (float)MapRes : 0f;
                GUI.Label(new Rect(r.x, r.center.y - 40, r.width, 30), "Karte wird erstellt …", UISkin.LabelCenter);
                UISkin.Bar(new Rect(r.x + r.width * 0.2f, r.center.y, r.width * 0.6f, 12), prog, UISkin.Teal);
                return;
            }
            if (rep)
            {
                float uw = mapHs * 2f / (MapHalf * 2f);
                var uv = new Rect((mapCx - mapHs + MapHalf) / (MapHalf * 2f), (mapCz - mapHs + MapHalf) / (MapHalf * 2f), uw, uw);
                GUI.DrawTextureWithTexCoords(r, mt.Tex, uv, false);
            }

            // Dünen (wechseln mit Sandstürmen)
            if (pd.Storms && rep)
            {
                int dune = ps.StormCount % 2;
                foreach (var b in l.Colliders)
                    if (b.DuneSet == dune) MapBox(b.Cx - b.Hx, b.Cz - b.Hz, b.Cx + b.Hx, b.Cz + b.Hz, new Color(0.85f, 0.6f, 0.35f, 0.8f));
            }

            // Bereichsgrenzen + Namen
            for (int g = 0; g < 2; g++)
            {
                float gz = g == 0 ? -50f : 50f;
                var a = W2M(-MapHalf, gz); var b2 = W2M(MapHalf, gz);
                if (rep && a.y > r.y && a.y < r.yMax) UISkin.Rect(new Rect(Mathf.Max(r.x, a.x), a.y - 1, Mathf.Min(r.xMax, b2.x) - Mathf.Max(r.x, a.x), 2), new Color(1, 1, 1, 0.35f));
                bool open = Rules.GateOpen(ps, g);
                var gl = l.Gates[g];
                if (!open && gl != null && gl.Blocker != null)
                {
                    var bb = gl.Blocker;
                    if (rep) MapBox(bb.Cx - bb.Hx, bb.Cz - bb.Hz, bb.Cx + bb.Hx, bb.Cz + bb.Hz, new Color(UISkin.Bad.r, UISkin.Bad.g, UISkin.Bad.b, 0.9f));
                    Mark(W2M(bb.Cx, bb.Cz), "cross", UISkin.Bad, 22, "Versperrt: " + gl.Hint);
                }
            }
            for (int a = 0; a < 3; a++)
            {
                float az = a == 0 ? -100f : a == 1 ? 0f : 100f;
                var p = W2M(-MapHalf + 6, az + 44f);
                if (p.y > r.y + 4 && p.y < r.yMax - 24 && rep)
                {
                    int stage = Rules.AreaStage(w, ps, a);
                    string t = pd.AreaNames[a] + " · " + (Rules.Cleanliness(ps, a) * 100).ToString("0") + " %";
                    var lr = new Rect(Mathf.Max(r.x + 6, p.x), p.y, 320, 22);
                    UISkin.RoundRect(new Rect(lr.x - 4, lr.y, UISkin.TextWidth(UISkin.LabelTiny, t) + 10, 22), new Color(0, 0, 0, 0.55f));
                    GUI.Label(lr, UISkin.Col(t, stage >= 4 ? UISkin.Good : UISkin.Text), UISkin.LabelTiny);
                }
            }

            // Stützpunkt und Gebäude
            var bl = l.Base;
            if (rep)
                foreach (var b in ps.Buildings)
                {
                    var c0 = bl.CellCenter(b.Gx, b.Gz, b.W, b.H);
                    float hx = b.W * bl.Cell * 0.5f, hz = b.H * bl.Cell * 0.5f;
                    MapBox(c0.x - hx, c0.z - hz, c0.x + hx, c0.z + hz, UISkin.FromRgb(b.Def.Color != 0 ? b.Def.Color : 0x888888u, 0.95f));
                }
            Mark(W2M(bl.Center.x, bl.Center.z), "house", UISkin.Teal, 26, "Stützpunkt (Lager, Verkauf, Werkstatt, Schiff)");
            Mark(W2M(bl.ShipPad.x, bl.ShipPad.z), "flag", UISkin.Accent, 18, "Transportschiff (Reisen)");

            // Projektplätze
            for (int a = 0; a < 3; a++)
            {
                var site = l.ProjectSites != null && l.ProjectSites.Length > a ? l.ProjectSites[a] : Terrain.ProjectSite(planet, a);
                var pid = GameData.ProjectId(planet, a);
                var pst = ps.Projects[pid];
                var pdef = GameData.Projects[pid];
                Color c = pst.Done ? UISkin.Good : pst.Started ? UISkin.Accent : UISkin.Warn;
                string st = pst.Done ? "fertig" : pst.Started ? "im Bau " + (pst.Progress * 100).ToString("0") + " %" : "offen";
                Mark(W2M(site.x, site.z), "star", c, 26, "Projektplatz: " + pdef.Name + " (" + st + ")");
            }

            // Lichtpunkte (Zonen)
            foreach (var z in l.Zones)
            {
                bool clean = Rules.ZoneCleared(ps, z.Index);
                Mark(W2M(z.Center.x, z.Center.z), clean ? "dot" : "ring", clean ? UISkin.Good : UISkin.Warn, clean ? 14 : 18, "Lichtpunkt „" + z.Name + "“" + (clean ? " – sauber" : " – verschmutzt"));
            }
            // Reparaturpunkte
            foreach (var s in l.Repairs)
            {
                bool done = ps.Repaired.Contains(s.Id);
                Mark(W2M(s.Pos.x, s.Pos.z), "wrench", done ? UISkin.Good : UISkin.Accent, 16, s.Name + (done ? " – repariert" : " – defekt"));
            }
            // Öko-Plätze
            foreach (var s in l.Eco)
            {
                float gr = Rules.EcoGrowth(w, ps, s.Id);
                bool planted = ps.Eco.ContainsKey(s.Id);
                Color c = gr >= 0.999f ? UISkin.Good : planted ? new Color(0.55f, 0.85f, 0.45f) : new Color(0.7f, 0.75f, 0.65f, 0.75f);
                Mark(W2M(s.Pos.x, s.Pos.z), "leaf", c, 15, s.Name + (gr >= 0.999f ? " – gewachsen" : planted ? " – wächst " + (gr * 100).ToString("0") + " %" : " – noch leer"));
            }
            // Fundstücke
            foreach (var s in l.LoreSpots)
            {
                bool found = w.Lore.Contains(s.Id);
                Mark(W2M(s.Pos.x, s.Pos.z), "book", found ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : UISkin.Story, 16, found ? "Fundstück (gefunden): " + s.Name : "Fundstück – noch nicht entdeckt");
            }
            // Aussichtspunkte
            foreach (var s in l.Viewpoints)
            {
                bool seen = ps.Views.Contains(s.Id);
                Mark(W2M(s.Pos.x, s.Pos.z), "eye", seen ? UISkin.Good : UISkin.Teal, 16, s.Name + (seen ? " – gemerkt (Fotomodus)" : ""));
            }
            // Unterschlüpfe
            foreach (var s in l.Shelters) Mark(W2M(s.Pos.x, s.Pos.z), "house", UISkin.Story, 17, pd.ShelterName);
            foreach (var s in ps.Shelters) Mark(W2M(s.x, s.z), "house", new Color(0.6f, 0.95f, 1f), 15, "Notunterschlupf (selbst gebaut)");

            // Fahrzeuge
            foreach (var v in ps.Vehicles.Values)
            {
                if (!GameData.Vehicles.ContainsKey(v.Id)) continue;
                Mark(W2M(v.Pos.x, v.Pos.z), "truck", UISkin.Accent, 20, v.Def.Name + (v.Driver != null ? " (besetzt)" : ""));
            }

            // Spieler
            string myId = app.Client != null ? app.Client.Pid : null;
            foreach (var p in w.Players.Values)
            {
                if (!p.Online || p.Id == myId) continue;
                var mp = W2M(p.Pos.x, p.Pos.z);
                if (!InMap(mp)) continue;
                CosmeticDef cd;
                Color col = GameData.Cosmetics.TryGetValue(p.Color ?? "", out cd) ? UISkin.FromRgb(cd.Value) : UISkin.Teal;
                DrawRotated(new Rect(mp.x - 11, mp.y - 11, 22, 22), UISkin.Arrow, p.Yaw * Mathf.Rad2Deg, col);
                if (rep)
                {
                    GUI.Label(new Rect(mp.x + 12, mp.y - 11, 160, 22), p.Name, UISkin.LabelTiny);
                    marks.Add(new MapMark { P = mp, Text = p.Name });
                }
            }
            if (me != null)
            {
                var mp = W2M(myPos.x, myPos.z);
                if (InMap(mp))
                {
                    if (rep)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                        UISkin.Tex(new Rect(mp.x - 20, mp.y - 20, 40, 40), UISkin.Ring, new Color(1, 1, 1, 0.25f + 0.35f * pulse));
                    }
                    DrawRotated(new Rect(mp.x - 15, mp.y - 15, 30, 30), UISkin.Arrow, Hud.CameraYaw, Color.white);
                    DrawRotated(new Rect(mp.x - 11, mp.y - 11, 22, 22), UISkin.Arrow, Hud.CameraYaw, UISkin.Accent);
                    if (rep) marks.Add(new MapMark { P = mp, Text = "Du (" + (me.Name ?? "MIKO") + ")" });
                }
            }

            // Kompass
            if (rep)
            {
                GUI.Label(new Rect(r.xMax - 40, r.y + 6, 30, 26), UISkin.Col("N", UISkin.Accent), UISkin.LabelCenter);
                UISkin.Tex(new Rect(r.xMax - 33, r.y + 30, 16, 16), UISkin.Arrow, UISkin.Accent);
            }

            DrawMarkTooltip(r);
        }

        void MapBox(float x0, float z0, float x1, float z1, Color c)
        {
            var a = W2M(x0, z1); var b = W2M(x1, z0);
            var rr = Rect.MinMaxRect(Mathf.Max(a.x, mapRect.x), Mathf.Max(a.y, mapRect.y), Mathf.Min(b.x, mapRect.xMax), Mathf.Min(b.y, mapRect.yMax));
            if (rr.width <= 0 || rr.height <= 0) return;
            UISkin.Rect(rr, c);
        }

        static readonly string[] LegendShapes = { "house", "star", "ring", "dot", "house", "house", "wrench", "leaf", "book", "eye", "cross", "truck", "flag" };
        static readonly string[] LegendTexts = { "Stützpunkt", "Projektplatz", "Lichtpunkt (verschmutzt)", "Lichtpunkt (sauber)", "Unterschlupf", "Notunterschlupf", "Reparatur (grün = erledigt)", "Öko-Platz (grün = gewachsen)", "Fundstück", "Aussichtspunkt", "Versperrter Durchgang", "Fahrzeug", "Transportschiff" };

        Color LegendColor(int i)
        {
            switch (i)
            {
                case 0: return UISkin.Teal;
                case 1: return UISkin.Warn;
                case 2: return UISkin.Warn;
                case 3: return UISkin.Good;
                case 4: return UISkin.Story;
                case 5: return new Color(0.6f, 0.95f, 1f);
                case 6: return UISkin.Accent;
                case 7: return new Color(0.55f, 0.85f, 0.45f);
                case 8: return UISkin.Story;
                case 9: return UISkin.Teal;
                case 10: return UISkin.Bad;
                case 11: return UISkin.Accent;
                default: return UISkin.Accent;
            }
        }

        /// <summary>
        /// Seitenleiste: Wiederherstellung je Bereich, Legende, Zoom. mode: 0 = Spielmenü (2D), 1 = Kartenbildschirm 2D
        /// (mit Umschalter zur 3D-Karte), 2 = Kartenbildschirm 3D (Zoom der Kartenkamera, Umschalter zur 2D-Karte).
        /// </summary>
        float DrawMapSide(GameApp app, Rect r, int mode = 0)
        {
            var w = app.W;
            var ps = w.Cur;
            var pd = GameData.Planets[w.CurrentPlanet];
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 34), UISkin.Col(pd.Name, UISkin.FromRgb(pd.Accent)) + " – " + pd.Subtitle, UISkin.LabelBold);
            y += 34;
            GUI.Label(new Rect(r.x, y, r.width, 26), "Wiederherstellung: " + (Rules.PlanetRestoration(w, ps) * 100).ToString("0") + " %", UISkin.LabelSmall);
            y += 30;
            for (int a = 0; a < 3; a++)
            {
                int stage = Rules.AreaStage(w, ps, a);
                GUI.Label(new Rect(r.x, y, r.width, 24), "<b>" + pd.AreaNames[a] + "</b>", UISkin.LabelSmall);
                y += 24;
                GUI.Label(new Rect(r.x + 10, y, r.width - 10, 22), "Stufe " + stage + "/4: " + Rules.StageNames[Mathf.Clamp(stage, 0, 4)], UISkin.LabelTiny);
                y += 22;
                UISkin.Bar(new Rect(r.x + 10, y + 3, r.width - 90, 10), Rules.Cleanliness(ps, a), stage >= 2 ? UISkin.Good : UISkin.Teal);
                GUI.Label(new Rect(r.xMax - 74, y - 3, 70, 22), (Rules.Cleanliness(ps, a) * 100).ToString("0") + " %", UISkin.LabelTiny);
                y += 22;
            }
            y += 8;
            GUI.Label(new Rect(r.x, y, r.width, 28), "Legende", UISkin.H3);
            y += 30;
            float colW = r.width;
            for (int i = 0; i < LegendShapes.Length; i++)
            {
                if (y > r.yMax - 110) break;
                UISkin.Tex(new Rect(r.x + 4, y + 3, 18, 18), UISkin.Shape(LegendShapes[i]), LegendColor(i));
                GUI.Label(new Rect(r.x + 30, y, colW - 30, 24), LegendTexts[i], UISkin.LabelTiny);
                y += 24;
            }
            float legendEnd = mode == 0 ? r.yMax - 100 : r.yMax - 150;
            if (y < legendEnd)
            {
                DrawRotated(new Rect(r.x + 4, y + 3, 18, 18), UISkin.Arrow, 0, UISkin.Accent);
                GUI.Label(new Rect(r.x + 30, y, colW - 30, 24), "Du (Blickrichtung) · Mitspieler in ihrer Farbe", UISkin.LabelTiny);
                y += 24;
            }
            if (mode == 2)
            {
                // Linien der 3D-Karte
                if (y < legendEnd) { LegendLine(new Rect(r.x + 2, y, colW, 24), new Color(0.35f, 1f, 0.88f, 0.9f), 3f, "Bereichsgrenze"); y += 24; }
                if (y < legendEnd) { LegendLine(new Rect(r.x + 2, y, colW, 24), new Color(0.85f, 0.97f, 1f, 0.6f), 1.5f, "Höhenlinien · Stützpunktfläche"); y += 24; }
            }
            y += 4;
            // Zoom und Ansicht
            float zy = r.yMax - (mode == 0 ? 64 : 112);
            bool three = mode == 2 && MapCamera.I != null;
            float zf = three ? MapCamera.I.ZoomFactor : mapZoom;
            GUI.Label(new Rect(r.x, zy, 120, 40), "Zoom " + zf.ToString("0.#") + "×", UISkin.LabelSmall);
            bool canOut = three ? MapCamera.I.TargetDistance < MapCamera.MaxDistance - 0.5f : mapZoom > 1.01f;
            bool canIn = three ? MapCamera.I.TargetDistance > MapCamera.MinDistance + 0.5f : mapZoom < 3.99f;
            if (UINav.Button(new Rect(r.x + 120, zy, 60, 40), "−", canOut, UISkin.ButtonSmall)) ZoomMap(-1);
            if (UINav.Button(new Rect(r.x + 186, zy, 60, 40), "+", canIn, UISkin.ButtonSmall)) ZoomMap(1);
            if (mode >= 1)
            {
                if (UINav.Button(new Rect(r.x, zy + 48, Mathf.Min(r.width, 246), 40), mode == 2 ? "2D-Karte zeigen" : "3D-Karte zeigen", true, UISkin.ButtonSmall))
                {
                    map2D = mode == 2;
                    mapDragging = mapPanning = false;
                }
            }
            else GUI.Label(new Rect(r.x, zy + 42, r.width, 22), InputMap.UsingPad ? "LB/RB: Zoom" : "Mausrad oder +/−: Zoom", UISkin.LabelTiny);
            return y;
        }

        void LegendLine(Rect r, Color c, float thick, string text)
        {
            if (Event.current.type == EventType.Repaint) UISkin.Rect(new Rect(r.x + 2, r.y + 12 - thick * 0.5f, 20, thick), c);
            GUI.Label(new Rect(r.x + 28, r.y, r.width - 30, 24), text, UISkin.LabelTiny);
        }
    }
}
