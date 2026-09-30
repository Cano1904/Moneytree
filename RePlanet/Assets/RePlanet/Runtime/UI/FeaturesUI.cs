using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Oberfläche der Zusatzsysteme: Reiter „Erfolge“ im Spielmenü, Schnellreise-Liste in der Kartenseitenleiste (2D und 3D),
    /// Kartensymbole für Helferroboter, Ereignisfunde und Reiseziele sowie die Anzeige beim Abwarten eines Sturms.
    /// </summary>
    public partial class UIRoot
    {
        bool fastTravelOpen;
        string fastTravelMsg; bool fastTravelErr; float fastTravelMsgUntil;

        // ================================================================== Erfolge
        void TabAchievements(GameApp app, Rect c)
        {
            var w = app.W;
            const int key = 471;
            UINav.BeginScroll(key, c);
            float sw = UINav.ScrollWidth(key, c);
            float y = 0;
            int done = 0;
            foreach (var a in GameData.Achievements) if (w.Achievements.Contains(a.Id)) done++;
            y = Section(Loc.F("Erfolge – {0} von {1} erreicht", done, GameData.Achievements.Count), 0, sw, y);
            y = Para(L("Erfolge schalten Kosmetik für MIKO frei (Farben, Akzente, Aufkleber, Anbauteile) – ohne Spielvorteil. Anlegen im Reiter „Roboter“."), 0, sw, y);
            y += 6;
            float colW = sw >= 900f ? (sw - 16f) * 0.5f : sw;
            int col = 0; float rowY = y;
            // Erreichte zuerst, dann nach Fortschritt
            var list = new List<AchievementDef>(GameData.Achievements);
            list.Sort((x, z) => Rules.AchievementProgress(w, z).CompareTo(Rules.AchievementProgress(w, x)));
            foreach (var a in list)
            {
                float x = col * (colW + 16f);
                AchievementRow(w, a, new Rect(x, rowY, colW, 84f));
                col++;
                if (col * (colW + 16f) + colW > sw + 1f) { col = 0; rowY += 92f; }
            }
            if (col > 0) rowY += 92f;
            UINav.EndScroll(rowY + 10);
        }

        void AchievementRow(WorldState w, AchievementDef a, Rect r)
        {
            bool got = w.Achievements.Contains(a.Id);
            float prog = Rules.AchievementProgress(w, a);
            if (Event.current.type == EventType.Repaint)
                UISkin.RoundRect(r, got ? new Color(UISkin.Good.r * 0.3f, UISkin.Good.g * 0.3f, UISkin.Good.b * 0.3f, 0.55f) : new Color(0, 0, 0, 0.28f));
            UISkin.Tex(new Rect(r.x + 12, r.y + 14, 30, 30), UISkin.Shape(got ? "star" : "ring"), got ? UISkin.Warn : new Color(1, 1, 1, 0.35f));
            GUI.Label(new Rect(r.x + 52, r.y + 6, r.width - 230, 28), "<b>" + a.Name + "</b>", UISkin.Label);
            GUI.Label(new Rect(r.x + 52, r.y + 32, r.width - 230, 24), UISkin.Col(a.Desc, UISkin.TextDim), UISkin.LabelTiny);
            long cur = Rules.AchievementCounter(w, a);
            long div = System.Math.Max(1, a.Div);
            string num = got ? L("erreicht ✓") : System.Math.Min(cur / div, a.Target / div).ToString("#,0") + " / " + (a.Target / div).ToString("#,0") + a.Unit;
            UISkin.Bar(new Rect(r.x + 52, r.y + 62, r.width - 230, 8), prog, got ? UISkin.Good : UISkin.Teal);
            GUI.Label(new Rect(r.xMax - 172, r.y + 54, 160, 24), UISkin.Col(num, got ? UISkin.Good : UISkin.Text), UISkin.LabelTiny);
            // Belohnung
            CosmeticDef cd;
            if (GameData.Cosmetics.TryGetValue(a.Reward, out cd))
            {
                string shape = cd.Kind == "color" || cd.Kind == "accent" ? "dot" : cd.Kind == "sticker" ? "star" : "flag";
                UISkin.Tex(new Rect(r.xMax - 170, r.y + 14, 22, 22), UISkin.Shape(shape), UISkin.FromRgb(cd.Value));
                string kind = L(cd.Kind == "color" ? "Farbe" : cd.Kind == "accent" ? "Akzent" : cd.Kind == "sticker" ? "Aufkleber" : "Anbauteil");
                GUI.Label(new Rect(r.xMax - 142, r.y + 6, 136, 22), UISkin.Col(kind, UISkin.TextDim), UISkin.LabelTiny);
                GUI.Label(new Rect(r.xMax - 142, r.y + 24, 136, 26), cd.Name, UISkin.LabelTiny);
            }
        }

        // ================================================================== Schnellreise (Kartenseitenleiste)
        /// <summary>Schaltfläche in der Kartenseitenleiste; true = Liste offen (ersetzt dann die Legende).</summary>
        bool FastTravelButton(GameApp app, Rect r)
        {
            var me = app.Me;
            string label = L(fastTravelOpen ? "✕ Schnellreise schließen" : "✦ Schnellreise (Lichtnetz)");
            if (UINav.Button(r, label, me != null, fastTravelOpen ? UISkin.ButtonSel : UISkin.ButtonSmall)) { fastTravelOpen = !fastTravelOpen; AudioManager.Ui("ui_click"); }
            return fastTravelOpen;
        }

        /// <summary>Liste der Reiseziele (Stützpunkt + Lichtpunkte) mit Energie und Begründung; gibt die genutzte Höhe zurück.</summary>
        float DrawFastTravelList(GameApp app, Rect r)
        {
            var w = app.W; var me = app.Me;
            if (w == null || me == null) return r.y;
            var ps = w.Cur;
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 28), L("Schnellreise"), UISkin.H3);
            y += 30;
            var here = Rules.TravelPointAt(ps, me.Pos);
            string intro = here != null
                ? (here.Zone < 0 ? L("Du stehst am Stützpunkt. Ziel wählen:") : Loc.F("Du stehst am Lichtpunkt „{0}“. Ziel wählen:", L(here.Name)))
                : L("Start nur an einem leuchtenden Lichtpunkt oder am Stützpunkt. Nicht im Sturm, nicht im Fahrzeug, Behälter höchstens zu einem Viertel voll.");
            y = Para(intro, r.x, r.width, y, UISkin.WrapSmall);
            foreach (var t in Rules.TravelPoints(ps))
            {
                if (y > r.yMax - 46) break;
                float cost; V3 dest;
                string why = Rules.FastTravelCheck(w, ps, me, t.Zone, out cost, out dest);
                bool ok = why == null;
                string label = (t.Lit ? "✦ " : "○ ") + L(t.Name) + (t.Lit ? Loc.F("  · {0} Energie", cost.ToString("0")) : L("  · leuchtet noch nicht"));
                if (UINav.Button(new Rect(r.x, y, r.width, 38), label, ok, ok ? UISkin.ButtonSmall : UISkin.ButtonSmall))
                {
                    int zone = t.Zone;
                    app.Act(new JObj().Set("a", "fasttravel").Set("z", zone), res =>
                    {
                        if (res == null) return;
                        if (res.Ok) { fastTravelOpen = false; UIState.Open(UIScreen.None); }
                        else { fastTravelMsg = res.Err; fastTravelErr = true; fastTravelMsgUntil = Time.unscaledTime + 5f; }
                    });
                }
                if (!ok && t.Lit && UINav.IsHover(new Rect(r.x, y, r.width, 38))) { fastTravelMsg = L(why); fastTravelErr = true; fastTravelMsgUntil = Time.unscaledTime + 0.2f; }
                y += 42;
            }
            if (fastTravelMsg != null && Time.unscaledTime < fastTravelMsgUntil && y < r.yMax - 30)
                y = Para(UISkin.Col(L(fastTravelMsg), fastTravelErr ? UISkin.Warn : UISkin.Good), r.x, r.width, y + 4, UISkin.WrapSmall);
            return y;
        }

        // ================================================================== Kartensymbole
        /// <summary>Helferroboter, Ereignisfunde und Reiseziele auf der 2D-Karte.</summary>
        void FeatureMarks2D(GameApp app)
        {
            var w = app.W; var ps = w.Cur;
            var l = WorldGen.Get(w.CurrentPlanet);
            foreach (var s in l.Bots)
            {
                HelperBot b;
                if (ps.Bots.TryGetValue(s.Id, out b)) Mark(W2M(b.Pos.x, b.Pos.z), "gear", UISkin.Teal, 16, Loc.F("Helferroboter (sammelt im Umkreis {0} m, {1}/{2})", GameData.HelperRadius.ToString("0"), b.Load.Count, GameData.HelperLoad));
                else Mark(W2M(s.Pos.x, s.Pos.z), "gear", new Color(0.65f, 0.4f, 0.25f), 15, Loc.F("{0} – defekt, reparierbar ({1})", L(s.Name), L(Rules.HelperCostText(w.CurrentPlanet))));
            }
            foreach (var d in ps.Dyn.Values)
                if (d.Ev > 0 && d.CarriedBy == null) Mark(W2M(d.Pos.x, d.Pos.z), "star", UISkin.Warn, 13, EventName(d));
        }

        /// <summary>Dasselbe auf der 3D-Karte.</summary>
        void FeatureMarks3D(GameApp app, MapCamera mc, float isz)
        {
            var w = app.W; var ps = w.Cur;
            string planet = w.CurrentPlanet;
            var l = WorldGen.Get(planet);
            foreach (var s in l.Bots)
            {
                HelperBot b;
                if (ps.Bots.TryGetValue(s.Id, out b)) Mark3(mc, planet, b.Pos.x, b.Pos.z, "gear", UISkin.Teal, 18 * isz, Loc.F("Helferroboter (sammelt im Umkreis {0} m, {1}/{2})", GameData.HelperRadius.ToString("0"), b.Load.Count, GameData.HelperLoad));
                else Mark3(mc, planet, s.Pos.x, s.Pos.z, "gear", new Color(0.65f, 0.4f, 0.25f), 17 * isz, Loc.F("{0} – defekt, reparierbar ({1})", L(s.Name), L(Rules.HelperCostText(planet))));
            }
            foreach (var d in ps.Dyn.Values)
                if (d.Ev > 0 && d.CarriedBy == null) Mark3(mc, planet, d.Pos.x, d.Pos.z, "star", UISkin.Warn, 15 * isz, EventName(d));
        }

        static string EventName(DynObj d)
        {
            return L(d.Ev == 1 ? "Meteoritensplitter (wertvoll)" : d.Ev == 2 ? "Versorgungskiste" : "Freigelegte Deponie");
        }

        // ================================================================== Sturm abwarten
        void DrawWaitOverlay(GameApp app, WorldState w)
        {
            int online = 0, resting = 0;
            foreach (var p in w.Players.Values) if (p.Online) { online++; if (p.Waiting || p.Sleeping) resting++; }
            var pd = GameData.Planets[w.CurrentPlanet];
            float bw = Mathf.Min(720f, VW - 80f);
            var r = new Rect((VW - bw) * 0.5f, VH * 0.2f, bw, 110);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 36), UISkin.Col(Loc.F("{0} abwarten …", pd.StormName), UISkin.Story), UISkin.LabelCenter);
            float left = Mathf.Max(0f, pd.StormDuration - w.Cur.StormTimer);
            string t = online > 1 && resting < online
                ? Loc.F("Warte auf Mitspieler ({0}/{1}) – erst wenn alle abwarten, läuft die Zeit schneller.", resting, online)
                : Loc.F("Die Zeit läuft ×{0} schneller. Noch etwa {1} s Sturm.", Game.WaitTimeScale.ToString("0"), Mathf.CeilToInt(left));
            GUI.Label(new Rect(r.x + 20, r.y + 46, r.width - 40, 28), t, UISkin.LabelCenter);
            GUI.Label(new Rect(r.x, r.y + 76, r.width, 26), UISkin.Col(KeyHint(GameAction.Sleep) + L(" aufstehen"), UISkin.TextDim), UISkin.LabelCenter);
        }
    }
}
