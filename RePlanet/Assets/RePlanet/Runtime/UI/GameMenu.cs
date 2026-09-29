using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Spielmenü (Tab) mit Reitern: Inventar, Aufträge, Karte, Werkstatt, Lager, Archiv, Roboter, Radio, Koop.
    /// Q/E bzw. LB/RB wechseln die Reiter. Käufe/Verkäufe laufen als Aktionen über den Server – der prüft
    /// Nähe zu Stationen und Berechtigungen; die UI zeigt die Gründe vorab an.
    /// </summary>
    public partial class UIRoot
    {
        static readonly string[] MenuTabs = { "inventory", "missions", "map", "workshop", "storage", "archive", "robot", "radio", "coop" };
        static readonly string[] MenuTabNames = { "Inventar", "Aufträge", "Karte", "Werkstatt", "Lager", "Archiv", "Roboter", "Radio", "Koop" };
        string menuTab = "inventory";
        string menuStation;
        int wsSub;
        string travelSel;
        string actMsg; bool actMsgErr; float actMsgUntil;
        string loreSel;
        readonly string[] cosmSel = new string[4];
        static readonly string[] CosmKinds = { "color", "accent", "sticker", "attach" };
        static readonly string[] CosmKindNames = { "Farbe", "Akzent", "Aufkleber", "Anbauteil" };
        readonly Dictionary<string, int> binGroups = new Dictionary<string, int>();
        readonly List<string> binKeys = new List<string>();

        void OnMenuOpened()
        {
            menuStation = UIState.Station;
            string tab = UIState.MenuTab;
            if (menuStation != null)
            {
                switch (menuStation)
                {
                    case "workshop": tab = "workshop"; wsSub = 0; break;
                    case "garage": tab = "workshop"; wsSub = 1; break;
                    case "ship": tab = "workshop"; wsSub = 2; break;
                    default: tab = "storage"; break;
                }
            }
            SetMenuTab(tab);
            actMsg = null;
            LoadCosmSel();
        }

        void SetMenuTab(string t)
        {
            if (System.Array.IndexOf(MenuTabs, t) < 0) t = "inventory";
            if (menuTab != t) { UINav.ResetFocus(); AudioManager.Ui("ui_click"); }
            menuTab = t;
            UIState.MenuTab = t;
            confirm = null;
        }

        void CycleMenuTab(int d)
        {
            int i = System.Array.IndexOf(MenuTabs, menuTab);
            if (i < 0) i = 0;
            SetMenuTab(MenuTabs[(i + d + MenuTabs.Length) % MenuTabs.Length]);
        }

        /// <summary>Aktion senden und Ergebnis im Menü anzeigen.</summary>
        void MenuAct(GameApp app, JObj a, string okText = null)
        {
            app.Act(a, r =>
            {
                if (r == null) return;
                if (r.Ok) { if (okText != null) { actMsg = okText; actMsgErr = false; actMsgUntil = Time.unscaledTime + 5f; } }
                else { actMsg = r.Err ?? "Nicht möglich."; actMsgErr = true; actMsgUntil = Time.unscaledTime + 6f; }
            });
        }

        void DrawGameMenu(GameApp app)
        {
            if (!app.InGame) { UIState.Open(UIScreen.MainMenu); return; }
            Dim(0.55f);
            var r = new Rect(24, 20, VW - 48, VH - 40);
            UISkin.PanelBox(r);
            // Reiterleiste
            string[] names = new string[MenuTabNames.Length];
            for (int i = 0; i < names.Length; i++) names[i] = L(MenuTabNames[i]);
            int cur = System.Array.IndexOf(MenuTabs, menuTab);
            float tabsX = r.x + 56, tabsW = r.width - 56 - 380;
            GUI.Label(new Rect(r.x + 16, r.y + 14, 36, 42), UISkin.Col(InputMap.UsingPad ? "LB" : "Q", UISkin.TextDim), UISkin.LabelCenter);
            GUI.Label(new Rect(tabsX + tabsW + 2, r.y + 14, 36, 42), UISkin.Col(InputMap.UsingPad ? "RB" : "E", UISkin.TextDim), UISkin.LabelCenter);
            int nt = UINav.Tabs(new Rect(tabsX, r.y + 14, tabsW, 42), cur, names);
            if (nt != cur) SetMenuTab(MenuTabs[nt]);
            var w = app.W;
            GUI.Label(new Rect(r.xMax - 330, r.y + 14, 180, 42), "<b>" + Num(w.Credits) + "</b> Cr", UISkin.LabelRight);
            if (UINav.Button(new Rect(r.xMax - 140, r.y + 16, 120, 38), "✕ " + L("Zurück"), true, UISkin.ButtonSmall)) Back();
            UISkin.Rect(new Rect(r.x + 16, r.y + 64, r.width - 32, 2), new Color(UISkin.Accent.r, UISkin.Accent.g, UISkin.Accent.b, 0.5f));
            var content = new Rect(r.x + 22, r.y + 76, r.width - 44, r.height - 90);
            if (actMsg != null && Time.unscaledTime < actMsgUntil)
            {
                var mr = new Rect(content.x, content.yMax - 40, content.width, 40);
                content.height -= 48;
                UISkin.RoundRect(mr, actMsgErr ? new Color(0.4f, 0.08f, 0.05f, 0.9f) : new Color(0.05f, 0.3f, 0.15f, 0.9f));
                GUI.Label(new Rect(mr.x + 16, mr.y, mr.width - 32, mr.height), (actMsgErr ? "⚠ " : "✓ ") + actMsg, UISkin.Label);
            }
            switch (menuTab)
            {
                case "inventory": TabInventory(app, content); break;
                case "missions": TabMissions(app, content); break;
                case "map":
                    {
                        float side = Mathf.Min(380f, content.width * 0.3f);
                        float ms = Mathf.Min(content.height, content.width - side - 30);
                        DrawMap(app, new Rect(content.x, content.y, ms, ms));
                        DrawMapSide(app, new Rect(content.x + ms + 30, content.y, content.width - ms - 30, content.height));
                        break;
                    }
                case "workshop": TabWorkshop(app, content); break;
                case "storage": TabStorage(app, content); break;
                case "archive": TabArchive(app, content); break;
                case "robot": TabRobot(app, content); break;
                case "radio": TabRadio(app, content); break;
                case "coop":
                    {
                        const int key = 408;
                        UINav.BeginScroll(key, content);
                        float y = DrawCoopContent(app, UINav.ScrollWidth(key, content), 0);
                        UINav.EndScroll(y + 10);
                        break;
                    }
            }
        }

        // ================================================================== Hilfen
        bool InBaseLocal(GameApp app)
        {
            var me = app.Me;
            if (me == null) return false;
            var b = WorldGen.Get(app.W.CurrentPlanet).Base;
            var p = me.Pos;
            return p.x >= b.MinX - 4 && p.x <= b.MaxX + 4 && p.z >= b.MinZ - 4 && p.z <= b.MaxZ + 4;
        }

        bool NearStationLocal(GameApp app, string station, float extra = 1f)
        {
            var me = app.Me;
            if (me == null) return false;
            var b = WorldGen.Get(app.W.CurrentPlanet).Base;
            V3 pos;
            if (!b.Stations.TryGetValue(station, out pos)) return false;
            return V3.DistXZ(me.Pos, pos) <= GameData.StationRange + extra;
        }

        bool GuestBlocked(GameApp app, long cost) { return !app.IsHost && !app.W.TrustGuests && cost >= GameData.GuestExpensiveThreshold; }

        static string StationName(string st)
        {
            switch (st)
            {
                case "sell": return "Verkaufsterminal";
                case "storage": return "Lager";
                case "sort": return "Sortiertisch";
                case "trader": return "Materialhändler";
                case "disposal": return "Entsorgungsstation";
                case "contracts": return "Auftragstafel";
                case "workshop": return "Werkstatt";
                case "garage": return "Garage";
                case "ship": return "Transportschiff";
            }
            return st;
        }

        float Section(string title, float x, float w, float y)
        {
            GUI.Label(new Rect(x, y, w, 32), title, UISkin.H3);
            UISkin.Rect(new Rect(x, y + 32, w, 1), new Color(1, 1, 1, 0.12f));
            return y + 40;
        }

        float Para(string text, float x, float w, float y, GUIStyle st = null)
        {
            st = st ?? UISkin.WrapSmall;
            float h = UISkin.TextHeight(st, text, w);
            GUI.Label(new Rect(x, y, w, h + 4), text, st);
            return y + h + 6;
        }

        // ================================================================== Inventar
        void TabInventory(GameApp app, Rect c)
        {
            var w = app.W; var me = app.Me;
            float lw = c.width * 0.58f;
            var left = new Rect(c.x, c.y, lw - 20, c.height);
            var right = new Rect(c.x + lw + 10, c.y, c.width - lw - 10, c.height);

            // Behälter
            float vol = Item.Volume(me.Bin), cap = Mathf.Max(1f, w.BinCapacity);
            float y = left.y;
            GUI.Label(new Rect(left.x, y, left.width, 34), "Behälter", UISkin.H3);
            GUI.Label(new Rect(left.x, y, left.width, 34), vol.ToString("0.#") + " / " + cap.ToString("0") + " Vol.", UISkin.LabelRight);
            y += 38;
            UISkin.Bar(new Rect(left.x, y, left.width, 14), vol / cap, vol >= cap - 0.01f ? UISkin.Warn : UISkin.Accent);
            y += 24;
            binGroups.Clear(); binKeys.Clear();
            foreach (var it in me.Bin)
            {
                string k = it.T + (it.P ? "|p" : "");
                int n;
                if (!binGroups.TryGetValue(k, out n)) binKeys.Add(k);
                binGroups[k] = n + 1;
            }
            const int key = 401;
            var view = new Rect(left.x, y, left.width, left.yMax - y - 40);
            UINav.BeginScroll(key, view);
            float sw = UINav.ScrollWidth(key, view);
            float cy = 0;
            if (binKeys.Count == 0)
            {
                GUI.Label(new Rect(0, 10, sw, 30), UISkin.Col("Der Behälter ist leer. Müll mit " + KeyHint(GameAction.UseTool) + " aufsammeln.", UISkin.TextDim), UISkin.Label);
                cy = 50;
            }
            foreach (var k in binKeys)
            {
                bool pressed = k.EndsWith("|p");
                string tid = pressed ? k.Substring(0, k.Length - 2) : k;
                TrashType t;
                if (!GameData.Trash.TryGetValue(tid, out t)) continue;
                int n = binGroups[k];
                var row = new Rect(0, cy, sw, 58);
                UISkin.RoundRect(row, new Color(1, 1, 1, 0.045f));
                GUI.Label(new Rect(12, cy + 4, sw * 0.5f, 26), "<b>" + n + "×</b> " + t.Name + (pressed ? UISkin.Col("  (gepresst)", UISkin.Teal) : "") + (t.Hazard > 0 ? UISkin.Col("  ⚠ Gefahrstoff", UISkin.Bad) : ""), UISkin.Label);
                float mx = 12;
                foreach (var kv in t.Yield)
                {
                    mx += UISkin.MaterialTag(new Rect(mx, cy + 30, 220, 24), kv.Key, " ×" + (kv.Value * n), UISkin.LabelTiny) + 16;
                }
                float iv = Mathf.Max(0.25f, t.Volume) * (pressed ? 0.5f : 1f) * n;
                GUI.Label(new Rect(sw - 150, cy + 4, 140, 26), iv.ToString("0.#") + " Vol.", UISkin.LabelRight);
                cy += 64;
            }
            UINav.EndScroll(cy);
            int val = Rules.BinValue(w, me.Bin);
            GUI.Label(new Rect(left.x, left.yMax - 34, left.width, 30), "Direktverkauf (unsortiert): ca. <b>" + Num(val) + "</b> Credits · Sortiert und als Ballen bringt es mehr.", UISkin.LabelSmall);

            // Rechts: Energie, Werkzeuge, Presse, Tipps
            float ry = right.y;
            ry = Section(L("Energie"), right.x, right.width, ry);
            float ef = me.Energy / Mathf.Max(1f, w.MaxEnergy);
            UISkin.Bar(new Rect(right.x, ry + 4, right.width - 120, 14), ef, ef < 0.1f ? UISkin.Bad : ef < 0.3f ? UISkin.Warn : UISkin.Teal);
            GUI.Label(new Rect(right.xMax - 110, ry - 4, 110, 30), me.Energy.ToString("0") + " / " + w.MaxEnergy.ToString("0"), UISkin.LabelRight);
            ry += 30;
            if (me.Energy <= 0.01f) ry = Para(UISkin.Col("Notbetrieb: langsam, keine Werkzeuge. Am Stützpunkt lädt der Akku.", UISkin.Bad), right.x, right.width, ry);
            ry += 6;
            ry = Section("Werkzeuge", right.x, right.width, ry);
            for (int i = 0; i < Rules.ToolIds.Length; i++)
            {
                string id = Rules.ToolIds[i];
                bool has = Rules.HasTool(w, id);
                UISkin.Tex(new Rect(right.x + 4, ry + 4, 22, 22), UISkin.Shape(ToolShapes[i]), has ? UISkin.Teal : new Color(1, 1, 1, 0.25f));
                UISkin.KeyCap(right.x + 34, ry + 4, InputMap.Label(ToolKeys[i]), 22);
                string lvl = "";
                TechDef td;
                if (GameData.Tech.TryGetValue(id, out td)) lvl = td.Levels[Mathf.Clamp(w.TechLevel(id), 0, td.MaxLevel)].Label;
                GUI.Label(new Rect(right.x + 70, ry, right.width - 70, 30), (has ? Rules.ToolName(id) : UISkin.Col(Rules.ToolName(id) + " – Werkstatt", UISkin.TextDim)) + (has && lvl.Length > 0 ? UISkin.Col("  · " + lvl, UISkin.TextDim) : ""), UISkin.LabelSmall);
                ry += 30;
            }
            ry += 8;
            bool press = w.TechLevel("press") > 0;
            if (UINav.Button(new Rect(right.x, ry, right.width, 44), "Behälter pressen " + KeyHint(GameAction.Press), press && me.Bin.Count > 0))
                MenuAct(app, new JObj().Set("a", "press"), "Gepresst – mehr Platz im Behälter.");
            ry += 50;
            if (!press) ry = Para("Die Müllpresse gibt es in der Werkstatt (halbiert das Volumen von Papier, Kunststoff, Metall, Stahl, Kupfer und Netzen).", right.x, right.width, ry);
            ry += 4;
            ry = Section("Wohin damit?", right.x, right.width, ry);
            ry = Para("• <b>Verkaufsterminal:</b> Behälter direkt verkaufen (unsortiert).\n• <b>Lager/Abladeplatz:</b> einlagern, dann sortieren (Sortiertisch oder Sortieranlage) und teurer verkaufen.\n• <b>Entsorgungsstation:</b> Gefahrstoffe fachgerecht abgeben – der Umweltfonds zahlt.\n• Alles im Reiter „" + L("Lager") + "“ – direkt an der jeweiligen Station.", right.x, right.width, ry);
        }

        // ================================================================== Aufträge
        void TabMissions(GameApp app, Rect c)
        {
            var w = app.W;
            float lw = c.width * 0.52f;
            var left = new Rect(c.x, c.y, lw - 16, c.height);
            var right = new Rect(c.x + lw + 8, c.y, c.width - lw - 8, c.height);

            // Links: Ziel + Aufträge
            const int keyL = 411;
            UINav.BeginScroll(keyL, left);
            float w1 = UINav.ScrollWidth(keyL, left);
            float y = 0;
            string obj = Objective(w);
            float oh = UISkin.TextHeight(UISkin.Wrap, obj, w1 - 40) + 50;
            var or = new Rect(0, y, w1, oh);
            UISkin.RoundRect(or, new Color(UISkin.Accent.r, UISkin.Accent.g, UISkin.Accent.b, 0.16f));
            GUI.Label(new Rect(16, y + 8, w1 - 32, 28), UISkin.Col("AKTUELLES ZIEL", UISkin.Accent), UISkin.LabelBold);
            GUI.Label(new Rect(16, y + 38, w1 - 32, oh - 40), obj, UISkin.Wrap);
            y += oh + 16;
            for (int pass = 0; pass < 3; pass++)
            {
                string head = pass == 0 ? "Aktive Aufträge" : pass == 1 ? "Erledigt" : "Noch gesperrt";
                bool any = false;
                foreach (var m in GameData.Missions)
                {
                    MissionState ms;
                    int status = w.Missions.TryGetValue(m.Id, out ms) ? ms.Status : 0;
                    int want = pass == 0 ? 1 : pass == 1 ? 2 : 0;
                    if (status != want) continue;
                    if (pass == 2 && m.Planet != w.CurrentPlanet) continue;
                    if (!any) { y = Section(head, 0, w1, y); any = true; }
                    y = MissionRow(w, m, ms, status, w1, y);
                }
            }
            UINav.EndScroll(y + 10);

            // Rechts: Bereiche + Großprojekte
            const int keyR = 412;
            UINav.BeginScroll(keyR, right);
            float w2 = UINav.ScrollWidth(keyR, right);
            float ry = 0;
            var ps = w.Cur;
            var pd = GameData.Planets[w.CurrentPlanet];
            ry = Section("Wiederherstellung " + pd.Name + " – " + (Rules.PlanetRestoration(w, ps) * 100).ToString("0") + " %", 0, w2, ry);
            for (int a = 0; a < 3; a++)
            {
                int st = Rules.AreaStage(w, ps, a);
                float cl = Rules.Cleanliness(ps, a);
                GUI.Label(new Rect(0, ry, w2 * 0.5f, 26), "<b>" + pd.AreaNames[a] + "</b>", UISkin.Label);
                GUI.Label(new Rect(w2 * 0.4f, ry, w2 * 0.6f, 26), UISkin.Col("Stufe " + st + "/4 · " + Rules.StageNames[Mathf.Clamp(st, 0, 4)], st >= 4 ? UISkin.Good : UISkin.TextDim), UISkin.LabelSmall);
                ry += 28;
                for (int s = 1; s <= 4; s++)
                {
                    var sr = new Rect((s - 1) * (w2 / 4f), ry, w2 / 4f - 6, 8);
                    UISkin.RoundRect(sr, st >= s ? (s == 4 ? UISkin.Good : UISkin.Teal) : new Color(1, 1, 1, 0.12f));
                }
                ry += 14;
                GUI.Label(new Rect(0, ry, w2, 24), "Sauberkeit " + (cl * 100).ToString("0") + " % (Ziel " + (GameData.AreaCleanThreshold * 100).ToString("0") + " %) · Ökologie " + (Rules.EcoFraction(w, ps, a) * 100).ToString("0") + " %", UISkin.LabelTiny);
                ry += 30;
            }
            ry += 6;
            ry = Section("Großprojekte", 0, w2, ry);
            foreach (var pid in GameData.PlanetOrder)
            {
                var ppd = GameData.Planets[pid];
                PlanetState pps;
                w.Planets.TryGetValue(pid, out pps);
                bool unlocked = w.Unlocked.Contains(pid);
                GUI.Label(new Rect(0, ry, w2, 30), UISkin.Col(ppd.Name, UISkin.FromRgb(ppd.Accent)) + UISkin.Col("  " + ppd.Subtitle + (unlocked ? "" : " · noch nicht erreichbar"), UISkin.TextDim), UISkin.LabelBold);
                ry += 32;
                for (int a = 0; a < 3; a++) ry = ProjectRow(w, GameData.Projects[GameData.ProjectId(pid, a)], pps, w2, ry);
                ry += 8;
            }
            UINav.EndScroll(ry + 10);
        }

        float MissionRow(WorldState w, MissionDef m, MissionState ms, int status, float width, float y)
        {
            string tag = m.Kind == "tutorial" ? "Einstieg" : "Nebenauftrag";
            PlanetDef mpd;
            GameData.Planets.TryGetValue(m.Planet ?? "", out mpd);
            float dh = UISkin.TextHeight(UISkin.WrapSmall, m.Desc, width - 24);
            float h = 36 + dh + (status == 1 && m.Target > 1 ? 24 : 0) + 30;
            var r = new Rect(0, y, width, h);
            UISkin.RoundRect(r, new Color(1, 1, 1, status == 1 ? 0.07f : 0.035f));
            if (status == 1) UISkin.RoundRect(new Rect(r.x + 3, r.y + 6, 5, r.height - 12), UISkin.Accent);
            string title = (status == 2 ? UISkin.Col("✓ ", UISkin.Good) : "") + "<b>" + m.Title + "</b>" + UISkin.Col("  · " + tag + (mpd != null ? " · " + mpd.Name : ""), UISkin.TextDim);
            GUI.Label(new Rect(14, y + 6, width - 28, 28), title, UISkin.Label);
            float yy = y + 34;
            GUI.Label(new Rect(14, yy, width - 24, dh + 4), m.Desc, UISkin.WrapSmall);
            yy += dh + 2;
            if (status == 1 && m.Target > 1)
            {
                long p = ms != null ? System.Math.Min(ms.Progress, m.Target) : 0;
                UISkin.Bar(new Rect(14, yy + 6, width - 140, 10), p / (float)m.Target, UISkin.Accent);
                GUI.Label(new Rect(width - 118, yy, 104, 22), p + "/" + m.Target, UISkin.LabelTiny);
                yy += 24;
            }
            string reward = (m.RewardCredits > 0 ? "Belohnung: " + Num(m.RewardCredits) + " Credits" : "");
            CosmeticDef cd;
            if (m.RewardCosmetic != null && GameData.Cosmetics.TryGetValue(m.RewardCosmetic, out cd)) reward += (reward.Length > 0 ? " + " : "Belohnung: ") + "Kosmetik „" + cd.Name + "“";
            if (status == 0 && m.Prereq != null)
            {
                foreach (var pm in GameData.Missions) if (pm.Id == m.Prereq) { reward += (reward.Length > 0 ? " · " : "") + "Nach „" + pm.Title + "“"; break; }
            }
            GUI.Label(new Rect(14, yy, width - 24, 26), UISkin.Col(reward, UISkin.Warn), UISkin.LabelTiny);
            return y + h + 8;
        }

        float ProjectRow(WorldState w, ProjectDef pr, PlanetState ps, float width, float y)
        {
            ProjectState st = null;
            if (ps != null) ps.Projects.TryGetValue(pr.Id, out st);
            bool done = st != null && st.Done, started = st != null && st.Started;
            string why = null;
            if (!done && !started && ps != null)
            {
                try { why = Rules.ProjectCheck(w, pr.Id); } catch (System.Exception) { why = null; }
            }
            float dh = UISkin.TextHeight(UISkin.WrapSmall, pr.Desc, width - 24);
            float h = 34 + dh + 32 + (why != null ? 26 : 0) + (started && !done ? 20 : 0) + 8;
            var r = new Rect(0, y, width, h);
            UISkin.RoundRect(r, new Color(1, 1, 1, done ? 0.03f : 0.06f));
            string status = done ? UISkin.Col("✓ abgeschlossen", UISkin.Good) : started ? UISkin.Col("im Bau " + (st.Progress * 100).ToString("0") + " %", UISkin.Accent) : UISkin.Col("offen", UISkin.TextDim);
            GUI.Label(new Rect(14, y + 5, width - 28, 28), "<b>" + pr.Name + "</b>" + (pr.Great ? UISkin.Col("  GROSSPROJEKT", UISkin.Story) : "") + "   " + status, UISkin.Label);
            float yy = y + 33;
            GUI.Label(new Rect(14, yy, width - 24, dh + 4), pr.Desc, UISkin.WrapSmall);
            yy += dh + 4;
            if (started && !done)
            {
                UISkin.Bar(new Rect(14, yy + 4, width - 28, 10), st.Progress, UISkin.Accent);
                yy += 20;
            }
            // Anforderungen
            float mx = 14;
            bool okC = w.Credits >= pr.Credits;
            string cr = Num(pr.Credits) + " Cr";
            GUI.Label(new Rect(mx, yy, 140, 26), UISkin.Col(cr, done || okC ? UISkin.Text : UISkin.Bad), UISkin.LabelTiny);
            mx += UISkin.TextWidth(UISkin.LabelTiny, cr) + 16;
            foreach (var kv in pr.Mats)
            {
                int have = ps != null ? ps.Available(kv.Key) : 0;
                UISkin.MaterialIcon(new Rect(mx, yy + 3, 18, 18), kv.Key);
                string t = (done ? "" : have + "/") + kv.Value;
                GUI.Label(new Rect(mx + 22, yy, 120, 26), UISkin.Col(t, done || have >= kv.Value ? UISkin.Text : UISkin.Bad), UISkin.LabelTiny);
                mx += 22 + UISkin.TextWidth(UISkin.LabelTiny, t) + 14;
            }
            yy += 30;
            if (why != null) GUI.Label(new Rect(14, yy, width - 28, 24), UISkin.Col(why, UISkin.Warn), UISkin.LabelTiny);
            else if (!done && !started && ps != null) GUI.Label(new Rect(14, yy, width - 28, 24), UISkin.Col("Bereit – am Projektplatz starten.", UISkin.Good), UISkin.LabelTiny);
            return y + h + 6;
        }

        // ================================================================== Werkstatt
        void TabWorkshop(GameApp app, Rect c)
        {
            string[] subs = { "Upgrades", "Fahrzeuge", "Transportschiff & " + L("Reisen") };
            wsSub = UINav.Tabs(new Rect(c.x, c.y, Mathf.Min(760f, c.width), 40), wsSub, subs);
            bool inBase = InBaseLocal(app);
            float y = c.y + 50;
            if (!inBase && wsSub < 2)
            {
                GUI.Label(new Rect(c.x, y, c.width, 28), UISkin.Col("Du bist unterwegs – kaufen geht nur am Stützpunkt (Werkstatt/Garage). Die Angebote kannst du hier ansehen.", UISkin.Warn), UISkin.LabelSmall);
                y += 32;
            }
            var area = new Rect(c.x, y, c.width, c.yMax - y);
            if (wsSub == 0) WorkshopUpgrades(app, area, inBase);
            else if (wsSub == 1) WorkshopVehicles(app, area, inBase);
            else WorkshopShip(app, area, inBase);
        }

        void WorkshopUpgrades(GameApp app, Rect area, bool inBase)
        {
            var w = app.W;
            const int key = 421;
            UINav.BeginScroll(key, area);
            float sw = UINav.ScrollWidth(key, area);
            float y = 0;
            var cats = new List<string>(6);
            foreach (var id in GameData.TechOrder) { var c = GameData.Tech[id].Category; if (!cats.Contains(c)) cats.Add(c); }
            foreach (var cat in cats)
            {
                y = Section(cat, 0, sw, y);
                foreach (var id in GameData.TechOrder)
                {
                    var t = GameData.Tech[id];
                    if (t.Category != cat) continue;
                    y = TechRow(app, w, t, sw, y, inBase);
                }
                y += 6;
            }
            UINav.EndScroll(y);
        }

        float TechRow(GameApp app, WorldState w, TechDef t, float width, float y, bool inBase)
        {
            int lvl = Mathf.Clamp(w.TechLevel(t.Id), 0, t.MaxLevel);
            bool maxed = lvl >= t.MaxLevel;
            int cost = maxed ? 0 : t.Levels[lvl + 1].Cost;
            string reason = null;
            if (maxed) reason = "Voll ausgebaut";
            else if (t.RequiresPlanet != null && !w.Unlocked.Contains(t.RequiresPlanet)) reason = "Erst verfügbar, wenn " + GameData.Planets[t.RequiresPlanet].Name + " erreichbar ist";
            else if (GuestBlocked(app, cost)) reason = "Nur der Host (ab " + Num(GameData.GuestExpensiveThreshold) + " Cr, Vertrauensmodus aus)";
            else if (w.Credits < cost) reason = "Es fehlen " + Num(cost - w.Credits) + " Credits";
            else if (!inBase) reason = "Nur am Stützpunkt";
            float textW = width - 300;
            float dh = UISkin.TextHeight(UISkin.WrapSmall, t.Desc, textW);
            float h = Mathf.Max(96f, 34 + dh + 30);
            var r = new Rect(0, y, width, h);
            UISkin.RoundRect(r, new Color(1, 1, 1, 0.05f));
            GUI.Label(new Rect(14, y + 6, textW, 28), "<b>" + t.Name + "</b>" + UISkin.Col("   Stufe " + lvl + "/" + t.MaxLevel, UISkin.TextDim), UISkin.Label);
            GUI.Label(new Rect(14, y + 34, textW, dh + 4), t.Desc, UISkin.WrapSmall);
            string eff = t.Effect + ": " + UISkin.Col(t.Levels[lvl].Label, UISkin.Text) + (maxed ? "" : "  →  " + UISkin.Col(t.Levels[lvl + 1].Label, UISkin.Good));
            GUI.Label(new Rect(14, y + 36 + dh, textW, 26), eff, UISkin.LabelSmall);
            // Kaufen
            float bx = width - 280;
            if (!maxed) GUI.Label(new Rect(bx, y + 6, 266, 28), "<b>" + Num(cost) + "</b> Credits", UISkin.LabelRight);
            if (UINav.Button(new Rect(bx, y + 38, 266, 40), maxed ? "Maximal" : L("Kaufen"), reason == null, reason == null ? UISkin.ButtonSel : UISkin.ButtonSmall))
                MenuAct(app, new JObj().Set("a", "buytech").Set("id", t.Id), t.Name + " verbessert.");
            if (reason != null && !maxed) GUI.Label(new Rect(bx - 60, y + 80, 326, 22), UISkin.Col(reason, UISkin.Warn), SmallRight());
            return y + h + 8;
        }

        void WorkshopVehicles(GameApp app, Rect area, bool inBase)
        {
            var w = app.W;
            const int key = 422;
            UINav.BeginScroll(key, area);
            float sw = UINav.ScrollWidth(key, area);
            float y = 0;
            y = Para("Fahrzeuge stehen nach dem Kauf in der Garage des Stützpunkts. Einsteigen mit " + KeyHint(GameAction.Vehicle) + ".", 0, sw, y);
            foreach (var v in GameData.Vehicles.Values)
            {
                bool owned = w.OwnedVehicles.Contains(v.Id);
                string reason = null;
                if (owned) reason = "Bereits in der Garage";
                else if (v.Planet != null && !w.Unlocked.Contains(v.Planet)) reason = "Erst auf " + GameData.Planets[v.Planet].Name + " nutzbar";
                else if (GuestBlocked(app, v.Cost)) reason = "Nur der Host (Vertrauensmodus aus)";
                else if (w.Credits < v.Cost) reason = "Es fehlen " + Num(v.Cost - w.Credits) + " Credits";
                else if (!inBase) reason = "Nur am Stützpunkt";
                float textW = sw - 300;
                float dh = UISkin.TextHeight(UISkin.WrapSmall, v.Desc, textW);
                float h = Mathf.Max(100f, 34 + dh + 32);
                var r = new Rect(0, y, sw, h);
                UISkin.RoundRect(r, new Color(1, 1, 1, 0.05f));
                UISkin.Tex(new Rect(14, y + 10, 28, 28), UISkin.Shape("truck"), owned ? UISkin.Good : UISkin.Accent);
                GUI.Label(new Rect(52, y + 8, textW - 40, 28), "<b>" + v.Name + "</b>" + (owned ? UISkin.Col("   ✓ vorhanden", UISkin.Good) : "") + (v.Planet != null ? UISkin.Col("   nur " + GameData.Planets[v.Planet].Name, UISkin.TextDim) : ""), UISkin.Label);
                GUI.Label(new Rect(14, y + 40, textW, dh + 4), v.Desc, UISkin.WrapSmall);
                GUI.Label(new Rect(14, y + 42 + dh, textW, 24), "Tempo " + v.Speed.ToString("0") + " m/s" + (v.Capacity > 0 ? " · Ladung " + v.Capacity.ToString("0") + " Vol." : "") + (v.Water ? " · fährt auf dem Wasser" : ""), UISkin.LabelTiny);
                float bx = sw - 280;
                if (!owned) GUI.Label(new Rect(bx, y + 6, 266, 28), "<b>" + Num(v.Cost) + "</b> Credits", UISkin.LabelRight);
                if (UINav.Button(new Rect(bx, y + 38, 266, 40), owned ? "In der Garage" : L("Kaufen"), reason == null, reason == null ? UISkin.ButtonSel : UISkin.ButtonSmall))
                    MenuAct(app, new JObj().Set("a", "buyveh").Set("id", v.Id), v.Name + " gekauft.");
                if (reason != null && !owned) GUI.Label(new Rect(bx - 60, y + 80, 326, 22), UISkin.Col(reason, UISkin.Warn), SmallRight());
                y += h + 8;
            }
            UINav.EndScroll(y);
        }

        void WorkshopShip(GameApp app, Rect area, bool inBase)
        {
            var w = app.W;
            const int key = 423;
            UINav.BeginScroll(key, area);
            float sw = UINav.ScrollWidth(key, area);
            float y = 0;
            y = Section("Transportschiff", 0, sw, y);
            int lvl = Mathf.Clamp(w.ShipLevel, 0, GameData.ShipLevelName.Length - 1);
            GUI.Label(new Rect(0, y, sw, 28), "Ausbau: <b>" + GameData.ShipLevelName[lvl] + "</b>", UISkin.Label);
            y += 32;
            if (lvl < GameData.ShipLevelCost.Length - 1)
            {
                int cost = GameData.ShipLevelCost[lvl + 1];
                string reason = null;
                if (GuestBlocked(app, cost)) reason = "Nur der Host (Vertrauensmodus aus)";
                else if (w.Credits < cost) reason = "Es fehlen " + Num(cost - w.Credits) + " Credits";
                else if (!inBase) reason = "Nur am Stützpunkt (Landeplatz)";
                GUI.Label(new Rect(0, y, sw - 300, 28), "Nächste Stufe: " + GameData.ShipLevelName[lvl + 1] + " – <b>" + Num(cost) + "</b> Credits", UISkin.Label);
                if (UINav.Button(new Rect(sw - 280, y - 4, 266, 40), "Einbauen", reason == null, reason == null ? UISkin.ButtonSel : UISkin.ButtonSmall))
                    MenuAct(app, new JObj().Set("a", "buyship"), "Sprungantrieb eingebaut.");
                y += 42;
                if (reason != null) { GUI.Label(new Rect(0, y, sw, 24), UISkin.Col(reason, UISkin.Warn), UISkin.LabelTiny); y += 26; }
            }
            else { GUI.Label(new Rect(0, y, sw, 26), UISkin.Col("Voll ausgebaut – alle Welten sind erreichbar, sobald sie freigeschaltet sind.", UISkin.Good), UISkin.LabelSmall); y += 30; }
            y += 10;
            y = Section(L("Reisen") + " – Sternenkarte", 0, sw, y);
            y = DrawStarMap(app, sw, y);
            UINav.EndScroll(y + 10);
        }

        /// <summary>Sternenkarte mit allen Planeten; Auswahl + Reisen. Lokale Koordinaten (im Scroll-Bereich).</summary>
        float DrawStarMap(GameApp app, float width, float y)
        {
            var w = app.W;
            var order = GameData.PlanetOrder;
            if (travelSel == null || !GameData.Planets.ContainsKey(travelSel)) travelSel = w.CurrentPlanet;
            float mapH = 230f;
            var mr = new Rect(0, y, width, mapH);
            if (Event.current.type == EventType.Repaint)
            {
                UISkin.RoundRect(mr, new Color(0.01f, 0.02f, 0.06f, 0.85f));
                // Sterne (deterministisch)
                for (int i = 0; i < 70; i++)
                {
                    float sx = Mathf.Repeat(i * 137.5f, 1f * width - 8) + 4, sy = Mathf.Repeat(i * 71.3f, mapH - 8) + 4;
                    float a = 0.2f + 0.5f * Mathf.Repeat(i * 0.618f, 1f);
                    UISkin.Rect(new Rect(sx, y + sy, 2, 2), new Color(1, 1, 1, a));
                }
            }
            int n = order.Count;
            float step = width / (n + 1);
            // Verbindungslinien
            for (int i = 0; i < n - 1; i++)
            {
                float x0 = step * (i + 1), x1 = step * (i + 2);
                float y0 = y + mapH * (i % 2 == 0 ? 0.42f : 0.58f), y1 = y + mapH * ((i + 1) % 2 == 0 ? 0.42f : 0.58f);
                for (int d = 0; d < 12; d++)
                {
                    float t = (d + 0.25f) / 12f;
                    UISkin.Rect(new Rect(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), 8, 2), new Color(1, 1, 1, 0.2f));
                }
            }
            for (int i = 0; i < n; i++)
            {
                var id = order[i];
                var pd = GameData.Planets[id];
                bool unlocked = w.Unlocked.Contains(id), current = id == w.CurrentPlanet;
                float cx = step * (i + 1), cy = y + mapH * (i % 2 == 0 ? 0.42f : 0.58f);
                float size = 64 + (pd.Order == 3 ? 10 : 0);
                var pr = new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size);
                bool focused;
                var hit = new Rect(cx - 90, y + 8, 180, mapH - 16);
                bool click = UINav.Area(hit, out focused);
                if (click) travelSel = id;
                bool sel = travelSel == id;
                if (Event.current.type == EventType.Repaint)
                {
                    if (sel) UISkin.Tex(new Rect(pr.x - 14, pr.y - 14, size + 28, size + 28), UISkin.Ring, UISkin.Accent);
                    UISkin.Tex(new Rect(pr.x - 6, pr.y - 6, size + 12, size + 12), UISkin.Circle, UISkin.FromRgb(pd.Accent, 0.25f));
                    UISkin.Tex(pr, UISkin.Circle, unlocked ? UISkin.FromRgb(pd.Ground) : new Color(0.3f, 0.3f, 0.33f));
                    UISkin.Tex(new Rect(pr.x + size * 0.15f, pr.y + size * 0.12f, size * 0.45f, size * 0.4f), UISkin.Circle, new Color(1, 1, 1, 0.15f));
                    if (!unlocked) UISkin.Tex(new Rect(cx - 12, cy - 12, 24, 24), UISkin.Shape("cross"), new Color(1, 1, 1, 0.6f));
                }
                GUI.Label(new Rect(cx - 100, cy + size * 0.5f + 6, 200, 26), UISkin.Col(pd.Name, unlocked ? UISkin.FromRgb(pd.Accent) : UISkin.TextDim), SmallCenter());
                string st = current ? "● Du bist hier" : unlocked ? "erreichbar" : "gesperrt";
                PlanetState pps;
                if (w.Planets.TryGetValue(id, out pps) && unlocked) st += " · " + (Rules.PlanetRestoration(w, pps) * 100).ToString("0") + " %";
                GUI.Label(new Rect(cx - 110, cy + size * 0.5f + 30, 220, 22), UISkin.Col(st, current ? UISkin.Good : UISkin.TextDim), SmallCenter());
            }
            y += mapH + 12;

            // Details zum gewählten Planeten
            var sp = GameData.Planets[travelSel];
            bool su = w.Unlocked.Contains(travelSel), scur = travelSel == w.CurrentPlanet;
            GUI.Label(new Rect(0, y, width, 32), UISkin.Col(sp.Name, UISkin.FromRgb(sp.Accent)) + " – " + sp.Subtitle, UISkin.H3);
            y += 36;
            y = Para(UISkin.Col(sp.Mood, UISkin.Warn), 0, width, y);
            y = Para(sp.Description, 0, width, y);
            if (!su) y = Para(UISkin.Col("Freischalten: " + sp.UnlockHint, UISkin.Story), 0, width, y);
            string reason = null;
            if (scur) reason = "Du bist bereits hier.";
            else if (!su) reason = sp.UnlockHint;
            else if (!app.IsHost) reason = "Nur der Host kann das Transportschiff starten.";
            else if (!NearStationLocal(app, "ship", 4f)) reason = "Zum Transportschiff am Landeplatz fahren.";
            if (UINav.Button(new Rect(0, y + 4, 340, 48), L("Reisen") + " nach " + sp.Name, reason == null, reason == null ? UISkin.ButtonSel : UISkin.ButtonSmall))
            {
                string target = travelSel;
                app.Act(new JObj().Set("a", "travel").Set("planet", target), r =>
                {
                    if (r == null) return;
                    if (r.Ok) { if (UIState.Screen == UIScreen.Menu || UIState.Screen == UIScreen.Travel) UIState.Open(UIScreen.None); }
                    else { actMsg = r.Err; actMsgErr = true; actMsgUntil = Time.unscaledTime + 6f; }
                });
            }
            if (reason != null) GUI.Label(new Rect(356, y + 4, width - 360, 48), UISkin.Col(reason, UISkin.Warn), UISkin.LabelSmall);
            y += 60;
            y = Para("Beim Reisen kommen alle Mitspieler mit. Lager, Gebäude und Fortschritt jedes Planeten bleiben erhalten.", 0, width, y);
            return y;
        }

        void DrawTravelScreen(GameApp app)
        {
            if (!app.InGame) { UIState.Open(UIScreen.MainMenu); return; }
            Dim(0.6f);
            var r = CenterRect(1200, 860);
            var inner = Window(r, L("Reisen"));
            const int key = 431;
            UINav.BeginScroll(key, inner);
            float y = DrawStarMap(app, UINav.ScrollWidth(key, inner), 0);
            UINav.EndScroll(y + 10);
            if (actMsg != null && Time.unscaledTime < actMsgUntil)
                GUI.Label(new Rect(r.x + 24, r.yMax - 40, r.width - 48, 30), UISkin.Col(actMsg, actMsgErr ? UISkin.Bad : UISkin.Good), UISkin.LabelSmall);
        }

        // ================================================================== Lager
        void TabStorage(GameApp app, Rect c)
        {
            var w = app.W; var me = app.Me;
            var ps = w.Cur;
            var en = Rules.Energy(ps);
            const int key = 441;
            UINav.BeginScroll(key, c);
            float sw = UINav.ScrollWidth(key, c);
            float y = 0;
            int used = ps.StorageUsed(), cap = ps.StorageCap();
            GUI.Label(new Rect(0, y, sw * 0.5f, 28), "Lager: <b>" + used + " / " + cap + "</b> Einheiten" + UISkin.Col("  (Ballen zählen 5)", UISkin.TextDim), UISkin.Label);
            Color ec = en.Efficiency >= 0.999f ? UISkin.Good : en.Efficiency >= 0.6f ? UISkin.Warn : UISkin.Bad;
            GUI.Label(new Rect(sw * 0.5f, y, sw * 0.5f, 28), "Energie: +" + en.Supply.ToString("0.#") + " / −" + en.Demand.ToString("0.#") + " → Anlagen " + UISkin.Col((en.Efficiency * 100).ToString("0") + " %", ec), UISkin.LabelRight);
            y += 30;
            UISkin.Bar(new Rect(0, y, sw, 10), used / (float)Mathf.Max(1, cap), used >= cap ? UISkin.Warn : UISkin.Teal);
            y += 18;
            string where = menuStation != null ? "Du stehst am <b>" + StationName(menuStation) + "</b>. " : "";
            y = Para(where + "Der Server prüft die Station: Verkaufen am Verkaufsterminal, Kaufen beim Materialhändler, Einlagern am Lager/Abladeplatz, Entsorgen an der Entsorgungsstation, Recyclingaufträge an der Auftragstafel.", 0, sw, y);
            y += 4;

            // ---------------------------------------------------- Behälter
            y = Section("Behälter (" + me.Bin.Count + " Teile, " + Item.Volume(me.Bin).ToString("0.#") + " Vol.)", 0, sw, y);
            float bw = (sw - 24) / 3f;
            bool nSell = NearStationLocal(app, "sell"), nStore = NearStationLocal(app, "storage", 1.5f) || NearDrop(app), nDisp = NearStationLocal(app, "disposal");
            int binVal = Rules.BinValue(w, me.Bin);
            if (UINav.Button(new Rect(0, y, bw, 46), "Behälter verkaufen (~" + Num(binVal) + " Cr)", me.Bin.Count > 0, nSell ? UISkin.ButtonSel : UISkin.Button))
                MenuAct(app, new JObj().Set("a", "sellbin"));
            if (UINav.Button(new Rect(bw + 12, y, bw, 46), L("Einlagern") + " (alles)", me.Bin.Count > 0, nStore ? UISkin.ButtonSel : UISkin.Button))
                MenuAct(app, new JObj().Set("a", "deposit"), "Eingelagert.");
            if (UINav.Button(new Rect((bw + 12) * 2, y, bw, 46), "Gefahrstoffe " + L("Entsorgen").ToLowerInvariant(), true, nDisp ? UISkin.ButtonSel : UISkin.Button))
                MenuAct(app, new JObj().Set("a", "dispose"));
            y += 50;
            GUI.Label(new Rect(0, y, bw, 22), StationHint(nSell, "sell"), UISkin.LabelTiny);
            GUI.Label(new Rect(bw + 12, y, bw, 22), StationHint(nStore, "storage"), UISkin.LabelTiny);
            GUI.Label(new Rect((bw + 12) * 2, y, bw, 22), StationHint(nDisp, "disposal"), UISkin.LabelTiny);
            y += 32;

            // ---------------------------------------------------- Materialtabelle
            y = Section("Material im Lager", 0, sw, y);
            bool nTrade = NearStationLocal(app, "trader");
            float colName = sw * 0.2f, colCnt = sw * 0.05f, colSell = sw * 0.12f, colBuy = sw * 0.1f;
            float x0 = 0, xU = colName, xS = xU + colCnt, xB = xS + colCnt, xSell = xB + colCnt + 10, xBuy = xSell + colSell * 3 + 20;
            GUI.Label(new Rect(x0 + 8, y, colName, 24), "Material", UISkin.LabelTiny);
            GUI.Label(new Rect(xU, y, colCnt, 24), "Unsort.", UISkin.LabelTiny);
            GUI.Label(new Rect(xS, y, colCnt, 24), "Sortiert", UISkin.LabelTiny);
            GUI.Label(new Rect(xB, y, colCnt, 24), "Ballen", UISkin.LabelTiny);
            GUI.Label(new Rect(xSell, y, colSell * 3, 24), L("Verkaufen") + " (alles je Stufe)" + (nSell ? "" : UISkin.Col(" – am Verkaufsterminal", UISkin.TextDim)), UISkin.LabelTiny);
            GUI.Label(new Rect(xBuy, y, colBuy * 2 + 10, 24), "Händler" + (nTrade ? "" : UISkin.Col(" – beim Materialhändler", UISkin.TextDim)), UISkin.LabelTiny);
            y += 26;
            int row = 0;
            foreach (var mat in GameData.MaterialOrder)
            {
                var md = GameData.Materials[mat];
                StorageEntry e;
                ps.Storage.TryGetValue(mat, out e);
                int U = e != null ? e.U : 0, S = e != null ? e.S : 0, B = e != null ? e.B : 0;
                if (U + S + B == 0 && !md.Buyable) continue;
                var rr = new Rect(0, y, sw, 44);
                if (row++ % 2 == 0) UISkin.RoundRect(rr, new Color(1, 1, 1, 0.04f));
                UISkin.MaterialTag(new Rect(x0 + 8, y + 8, colName - 10, 28), mat, null, UISkin.LabelSmall);
                GUI.Label(new Rect(xU, y, colCnt, 44), U.ToString(), U > 0 ? UISkin.Label : UISkin.LabelSmall);
                GUI.Label(new Rect(xS, y, colCnt, 44), S.ToString(), S > 0 ? UISkin.Label : UISkin.LabelSmall);
                GUI.Label(new Rect(xB, y, colCnt, 44), B.ToString(), B > 0 ? UISkin.Label : UISkin.LabelSmall);
                if (md.Price <= 0)
                {
                    GUI.Label(new Rect(xSell, y, colSell * 3, 44), UISkin.Col("→ nur Entsorgung (+" + md.DisposalBonus + " Cr/Einheit)", UISkin.Warn), UISkin.LabelSmall);
                }
                else
                {
                    for (int g = 0; g < 3; g++)
                    {
                        int have = g == 0 ? U : g == 1 ? S : B;
                        string gl = g == 0 ? "U" : g == 1 ? "S" : "B";
                        int val = have > 0 ? Rules.SellValue(w, mat, (Grade)g, have) : 0;
                        if (UINav.Button(new Rect(xSell + g * (colSell + 6), y + 4, colSell, 36), gl + " +" + Num(val), have > 0, UISkin.ButtonSmall))
                            MenuAct(app, new JObj().Set("a", "sell").Set("m", mat).Set("g", g).Set("n", 0));
                    }
                }
                if (md.Buyable)
                {
                    int p10 = GameData.BuyPrice(mat) * 10, p50 = GameData.BuyPrice(mat) * 50;
                    if (UINav.Button(new Rect(xBuy, y + 4, colBuy, 36), "+10 (" + Num(p10) + ")", w.Credits >= p10 && !GuestBlocked(app, p10), UISkin.ButtonSmall))
                        MenuAct(app, new JObj().Set("a", "buymat").Set("m", mat).Set("n", 10), "10 " + md.Name + " gekauft.");
                    if (UINav.Button(new Rect(xBuy + colBuy + 8, y + 4, colBuy, 36), "+50 (" + Num(p50) + ")", w.Credits >= p50 && !GuestBlocked(app, p50), UISkin.ButtonSmall))
                        MenuAct(app, new JObj().Set("a", "buymat").Set("m", mat).Set("n", 50), "50 " + md.Name + " gekauft.");
                }
                y += 46;
            }
            y = Para("U = unsortiert (50 % Wert), S = sortiert (100 %), B = Ballen aus 10 sortierten Einheiten (130 %). Sortieren: am Sortiertisch " + KeyHint(GameAction.Interact) + " halten oder mit einer Sortieranlage (Bauansicht).", 0, sw, y + 4);

            // ---------------------------------------------------- Aufträge & Lieferungen
            y += 6;
            y = Section("Recyclingauftrag & Lieferungen", 0, sw, y);
            string cm; int cn, creward;
            Rules.Contract(w.CurrentPlanet, ps.ContractIdx, out cm, out cn, out creward);
            StorageEntry ce;
            int haveS = ps.Storage.TryGetValue(cm, out ce) ? ce.S : 0;
            bool nContract = NearStationLocal(app, "contracts");
            float tagW = UISkin.MaterialTag(new Rect(0, y + 4, 280, 30), cm, null, UISkin.LabelBold);
            GUI.Label(new Rect(tagW + 10, y, sw - tagW - 340, 38), "Liefere <b>" + cn + "</b> sortierte Einheiten (vorhanden: " + UISkin.Col(haveS.ToString(), haveS >= cn ? UISkin.Good : UISkin.Bad) + ") → <b>" + Num(creward) + "</b> Credits", UISkin.Label);
            if (UINav.Button(new Rect(sw - 320, y, 320, 42), "Auftrag erfüllen", haveS >= cn, nContract ? UISkin.ButtonSel : UISkin.Button))
                MenuAct(app, new JObj().Set("a", "contract"), "Recyclingauftrag erfüllt.");
            y += 44;
            GUI.Label(new Rect(sw - 320, y, 320, 22), StationHint(nContract, "contracts"), UISkin.LabelTiny);
            y += 30;
            bool pending = false;
            foreach (var d in ps.Dyn.Values) if (d.Delivery) { pending = true; break; }
            GUI.Label(new Rect(0, y, sw - 340, 42), "Schrottlieferung: 14 Teile Müll landen am Abladeplatz – ideal zum Sortieren und für Aufträge.", UISkin.LabelSmall);
            if (UINav.Button(new Rect(sw - 320, y, 320, 42), pending ? "Lieferung liegt noch bereit" : "Lieferung bestellen", !pending && InBaseLocal(app), UISkin.Button))
                MenuAct(app, new JObj().Set("a", "delivery"), "Lieferung ist unterwegs zum Abladeplatz.");
            y += 48;
            if (!InBaseLocal(app)) { GUI.Label(new Rect(sw - 320, y - 4, 320, 22), UISkin.Col("Nur am Stützpunkt", UISkin.TextDim), UISkin.LabelTiny); y += 20; }
            UINav.EndScroll(y + 10);
        }

        bool NearDrop(GameApp app)
        {
            var me = app.Me;
            if (me == null) return false;
            var b = WorldGen.Get(app.W.CurrentPlanet).Base;
            return V3.DistXZ(me.Pos, b.DropZone) <= b.DropRadius + 1f;
        }

        string StationHint(bool near, string st)
        {
            return near ? UISkin.Col("✓ " + StationName(st) + " in Reichweite", UISkin.Good) : UISkin.Col("→ am " + StationName(st), UISkin.TextDim);
        }

        // ================================================================== Archiv
        void TabArchive(GameApp app, Rect c)
        {
            var w = app.W;
            float lw = Mathf.Min(460f, c.width * 0.38f);
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);
            int found = 0;
            foreach (var id in GameData.LoreOrder) if (w.Lore.Contains(id)) found++;
            const int key = 451;
            UINav.BeginScroll(key, left);
            float sw = UINav.ScrollWidth(key, left);
            float y = 0;
            GUI.Label(new Rect(0, y, sw, 30), "Fundstücke: <b>" + found + " / " + GameData.LoreOrder.Count + "</b>", UISkin.Label);
            y += 36;
            string lastPlanet = null;
            foreach (var id in GameData.LoreOrder)
            {
                var ld = GameData.Lore[id];
                if (ld.Planet != lastPlanet)
                {
                    lastPlanet = ld.Planet;
                    PlanetDef pd;
                    string pn = GameData.Planets.TryGetValue(ld.Planet, out pd) ? pd.Name : ld.Planet;
                    GUI.Label(new Rect(0, y + 4, sw, 28), UISkin.Col(pn, pd != null ? UISkin.FromRgb(pd.Accent) : UISkin.Accent), UISkin.LabelBold);
                    y += 34;
                }
                bool has = w.Lore.Contains(id);
                if (UINav.Button(new Rect(0, y, sw, 40), has ? ld.Title : "??? (noch nicht gefunden)", has, loreSel == id ? UISkin.ButtonSel : UISkin.ButtonSmall)) loreSel = id;
                y += 46;
            }
            UINav.EndScroll(y);

            UISkin.PanelBoxLight(right);
            LoreDef sel;
            if (loreSel != null && GameData.Lore.TryGetValue(loreSel, out sel) && w.Lore.Contains(loreSel))
            {
                PlanetDef pd;
                GameData.Planets.TryGetValue(sel.Planet, out pd);
                GUI.Label(new Rect(right.x + 30, right.y + 24, right.width - 60, 40), sel.Title, UISkin.H2);
                GUI.Label(new Rect(right.x + 30, right.y + 66, right.width - 60, 26), UISkin.Col(pd != null ? pd.Name + " – " + pd.Subtitle : "", UISkin.TextDim), UISkin.LabelSmall);
                UISkin.Rect(new Rect(right.x + 30, right.y + 100, right.width - 60, 1), new Color(1, 1, 1, 0.15f));
                GUI.Label(new Rect(right.x + 30, right.y + 116, right.width - 60, right.height - 140), sel.Text, UISkin.Wrap);
            }
            else
            {
                GUI.Label(new Rect(right.x + 30, right.y + 30, right.width - 60, 120), UISkin.Col("Wähle links ein Fundstück zum Lesen.\n\nFundstücke liegen verstreut auf den Planeten (Buch-Symbol auf der Karte). Mit " + KeyHint(GameAction.Interact) + " aufheben.", UISkin.TextDim), UISkin.Wrap);
            }
        }

        // ================================================================== Roboter
        void LoadCosmSel()
        {
            var p = GameApp.I != null ? GameApp.I.Profile : null;
            if (p == null) return;
            cosmSel[0] = p.Color; cosmSel[1] = p.Accent; cosmSel[2] = p.Sticker; cosmSel[3] = p.Attach;
        }

        bool CosmAvailable(GameApp app, CosmeticDef c)
        {
            return c.Default || (app.W != null && app.W.CosmeticUnlocks.Contains(c.Id)) || (app.Profile != null && app.Profile.Unlocks.Contains(c.Id));
        }

        static string StickerShape(string id)
        {
            switch (id)
            {
                case "stern": return "star";
                case "zahnrad": return "gear";
                case "welle": return "drop";
                case "flocke": return "sun";
                case "herz": return "dot";
                case "antenne": return "flag";
                case "blume": return "star";
                case "muschel": return "ring";
                case "muetze": return "house";
                case "faehnchen": return "flag";
            }
            return null;
        }

        void TabRobot(GameApp app, Rect c)
        {
            var w = app.W;
            float lw = c.width * 0.6f;
            var left = new Rect(c.x, c.y, lw - 20, c.height);
            var right = new Rect(c.x + lw, c.y, c.width - lw, c.height);
            const int key = 461;
            UINav.BeginScroll(key, left);
            float sw = UINav.ScrollWidth(key, left);
            float y = 0;
            // Vorschau
            float pvS = 170f;
            var pv = new Rect(0, y, pvS, pvS + 20);
            DrawRobotPreview(pv);
            float tx = pvS + 24;
            GUI.Label(new Rect(tx, y, sw - tx, 30), "Aussehen von MIKO", UISkin.H3);
            y = Para("Kosmetik ist rein optisch. Freigeschaltetes bleibt in deinem Profil – auch in anderen Welten und im Koop.", tx, sw - tx, y + 36);
            bool changed = cosmSel[0] != app.Profile.Color || cosmSel[1] != app.Profile.Accent || cosmSel[2] != app.Profile.Sticker || cosmSel[3] != app.Profile.Attach;
            if (UINav.Button(new Rect(tx, y + 6, 240, 44), "Übernehmen", changed, UISkin.ButtonSel))
            {
                var p = app.Profile;
                p.Color = cosmSel[0]; p.Accent = cosmSel[1]; p.Sticker = cosmSel[2]; p.Attach = cosmSel[3];
                p.Save();
                MenuAct(app, new JObj().Set("a", "cosm").Set("color", p.Color).Set("accent", p.Accent).Set("sticker", p.Sticker).Set("attach", p.Attach).Set("personal", true), "Neues Aussehen übernommen.");
            }
            if (UINav.Button(new Rect(tx + 252, y + 6, 200, 44), "Zurücksetzen", changed, UISkin.ButtonSmall)) LoadCosmSel();
            y = Mathf.Max(y + 60, pvS + 40);
            for (int k = 0; k < 4; k++)
            {
                y = Section(CosmKindNames[k], 0, sw, y);
                float bx = 0, bw = 200, bh = 44;
                foreach (var id in GameData.CosmeticOrder)
                {
                    var cd = GameData.Cosmetics[id];
                    if (cd.Kind != CosmKinds[k]) continue;
                    bool avail = CosmAvailable(app, cd);
                    if (bx + bw > sw) { bx = 0; y += bh + 8; }
                    var br = new Rect(bx, y, bw, bh);
                    bool sel = cosmSel[k] == id;
                    if (UINav.Button(br, "", avail, sel ? UISkin.ButtonSel : UISkin.ButtonSmall)) cosmSel[k] = id;
                    if (cd.Value != 0 || k < 2) UISkin.Tex(new Rect(br.x + 10, br.y + 11, 22, 22), UISkin.Shape(k >= 2 ? (StickerShape(id) ?? "dot") : "dot"), avail ? UISkin.FromRgb(cd.Value) : new Color(0.5f, 0.5f, 0.5f, 0.6f));
                    GUI.Label(new Rect(br.x + 40, br.y, br.width - 46, bh), avail ? cd.Name : UISkin.Col(cd.Hint, UISkin.TextDim), UISkin.LabelTiny);
                    bx += bw + 8;
                }
                y += bh + 14;
            }
            UINav.EndScroll(y);

            // Statistiken
            float ry = right.y;
            ry = Section("Statistik – " + w.WorldName, right.x, right.width, ry);
            StatLine(right, ref ry, "Spielzeit", FormatTime(w.PlayTime));
            StatLine(right, ref ry, "Wiederherstellung gesamt", (SaveCodec.TotalRestoration(w) * 100).ToString("0") + " %");
            StatLine(right, ref ry, "Credits verdient", Num(w.Stat("credEarned")));
            StatLine(right, ref ry, "Objekte gesammelt", Num(w.Stat("collected")));
            StatLine(right, ref ry, "Verkäufe", Num(w.Stat("sales")));
            StatLine(right, ref ry, "Einheiten sortiert", Num(w.Stat("sorted")));
            StatLine(right, ref ry, "Ballen verkauft", Num(w.Stat("balesSold")));
            StatLine(right, ref ry, "Gefahrstoffe entsorgt", Num(w.Stat("disposed")));
            StatLine(right, ref ry, "Objekte zerlegt", Num(w.Stat("dismantled")));
            StatLine(right, ref ry, "Aufgetaut", Num(w.Stat("thawed")));
            StatLine(right, ref ry, "Ölteppiche gereinigt", Num(w.Stat("oil")));
            StatLine(right, ref ry, "Upgrades gekauft", Num(w.Stat("techBought")));
            StatLine(right, ref ry, "Unterschlupf genutzt", Num(w.Stat("shelterVisits")));
            StatLine(right, ref ry, "Notabschaltungen", Num(w.Stat("shutdowns")));
            StatLine(right, ref ry, "Fundstücke", w.Lore.Count + " / " + GameData.LoreOrder.Count);
            StatLine(right, ref ry, "Planeten erreichbar", w.Unlocked.Count + " / " + GameData.PlanetOrder.Count);
            if (w.CampaignDone) StatLine(right, ref ry, "Kampagne", UISkin.Col("abgeschlossen ✓", UISkin.Good));
        }

        void StatLine(Rect r, ref float y, string label, string value)
        {
            if (y > r.yMax - 28) return;
            GUI.Label(new Rect(r.x + 4, y, r.width * 0.6f, 28), label, UISkin.LabelSmall);
            GUI.Label(new Rect(r.x + r.width * 0.55f, y, r.width * 0.45f - 6, 28), "<b>" + value + "</b>", UISkin.LabelRight);
            y += 30;
        }

        void DrawRobotPreview(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            UISkin.RoundRect(r, new Color(0, 0, 0, 0.3f));
            CosmeticDef body, acc, st, at;
            GameData.Cosmetics.TryGetValue(cosmSel[0] ?? "", out body);
            GameData.Cosmetics.TryGetValue(cosmSel[1] ?? "", out acc);
            GameData.Cosmetics.TryGetValue(cosmSel[2] ?? "", out st);
            GameData.Cosmetics.TryGetValue(cosmSel[3] ?? "", out at);
            Color bc = body != null ? UISkin.FromRgb(body.Value) : UISkin.Teal;
            Color ac = acc != null ? UISkin.FromRgb(acc.Value) : UISkin.Accent;
            float cx = r.center.x;
            // Ketten
            UISkin.RoundRect(new Rect(cx - 62, r.yMax - 46, 124, 30), new Color(0.15f, 0.15f, 0.17f));
            // Körper
            var br = new Rect(cx - 50, r.y + 70, 100, 80);
            UISkin.RoundRect(br, bc);
            UISkin.Rect(new Rect(br.x + 6, br.y + 52, br.width - 12, 8), ac);
            // Kopf mit Augen
            var hr = new Rect(cx - 40, r.y + 30, 80, 38);
            UISkin.RoundRect(hr, Color.Lerp(bc, Color.white, 0.25f));
            UISkin.Tex(new Rect(cx - 30, r.y + 36, 24, 24), UISkin.Circle, new Color(0.1f, 0.12f, 0.14f));
            UISkin.Tex(new Rect(cx + 6, r.y + 36, 24, 24), UISkin.Circle, new Color(0.1f, 0.12f, 0.14f));
            UISkin.Tex(new Rect(cx - 24, r.y + 42, 10, 10), UISkin.Circle, UISkin.Teal);
            UISkin.Tex(new Rect(cx + 12, r.y + 42, 10, 10), UISkin.Circle, UISkin.Teal);
            // Aufkleber
            if (st != null && st.Value != 0)
            {
                var sh = StickerShape(st.Id);
                if (sh != null) UISkin.Tex(new Rect(cx - 14, br.y + 14, 28, 28), UISkin.Shape(sh), UISkin.FromRgb(st.Value));
            }
            // Anbauteil
            if (at != null && at.Value != 0)
            {
                var sh = StickerShape(at.Id) ?? "dot";
                UISkin.Rect(new Rect(cx + 24, r.y + 10, 3, 22), new Color(0.6f, 0.6f, 0.6f));
                UISkin.Tex(new Rect(cx + 14, r.y, 24, 24), UISkin.Shape(sh), UISkin.FromRgb(at.Value));
            }
        }
    }
}
