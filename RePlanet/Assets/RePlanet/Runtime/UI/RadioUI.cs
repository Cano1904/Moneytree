using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Spielmenü-Reiter „Radio“: an/aus, Sender, laufendes Stück, alle Stücke (freie abspielbar, gesperrte mit Hinweis).</summary>
    public partial class UIRoot
    {
        /// <summary>Radio-Taste (Standard T) im Spiel.</summary>
        void ToggleRadio()
        {
            AudioManager.RadioToggle();
            if (AudioManager.RadioOn) Hud.Show(Loc.F("Radio an: {0}", AudioManager.StationName(AudioManager.RadioStation)), ToastKind.Info, 2.5f);
            else Hud.Show(L("Radio aus – die Planetenmusik kehrt zurück."), ToastKind.Info, 2.5f);
            AudioManager.Ui("ui_click");
        }

        void TabRadio(GameApp app, Rect c)
        {
            var w = app.W;
            float lw = Mathf.Min(520f, c.width * 0.42f);
            var left = new Rect(c.x, c.y, lw, c.height);
            var right = new Rect(c.x + lw + 20, c.y, c.width - lw - 20, c.height);

            // ------------------------------------------------ Links: Gerät
            UISkin.PanelBoxLight(left);
            float x = left.x + 24, y = left.y + 20, bw = left.width - 48;
            GUI.Label(new Rect(x, y, bw, 40), L("Radio"), UISkin.H2);
            y += 50;
            bool on = AudioManager.RadioOn;
            if (UINav.Button(new Rect(x, y, bw, 50), (on ? L("Radio ausschalten") : L("Radio einschalten")) + "  " + KeyHint(GameAction.Radio), true, on ? UISkin.ButtonSel : UISkin.Button))
                ToggleRadio();
            y += 64;
            var cur = AudioManager.RadioCurrent;
            string nowText;
            if (!on) nowText = UISkin.Col(L("Aus. Im Spiel läuft die Planetenmusik, die mit der Wiederherstellung wächst."), UISkin.TextDim);
            else if (cur == null) nowText = UISkin.Col(L("Kein Stück verfügbar."), UISkin.TextDim);
            else if (AudioManager.RadioAnnouncing) nowText = UISkin.Col(L("Ansage …"), UISkin.Accent) + "\n<b>" + L(cur.Title) + "</b>";
            else nowText = UISkin.Col(L("Es läuft:"), UISkin.TextDim) + "\n<b>" + L(cur.Title) + "</b>";
            float nh = UISkin.TextHeight(UISkin.Wrap, nowText, bw);
            GUI.Label(new Rect(x, y, bw, nh + 4), nowText, UISkin.Wrap);
            y += nh + 10;
            float prog = AudioManager.RadioProgress;
            if (prog >= 0f) { UISkin.Bar(new Rect(x, y, bw, 10), prog, UISkin.Teal); y += 22; }
            if (on && UINav.Button(new Rect(x, y, bw, 42), L("Nächstes Stück") + " ›", cur != null, UISkin.ButtonSmall)) AudioManager.RadioNext();
            if (on) y += 56;

            GUI.Label(new Rect(x, y, bw, 30), UISkin.Col(L("Sender").ToUpperInvariant(), UISkin.Accent), UISkin.LabelBold);
            y += 36;
            var stations = AudioManager.RadioStations(w);
            string st = AudioManager.RadioStation;
            foreach (var s in stations)
            {
                bool sel = s == st;
                if (UINav.Button(new Rect(x, y, bw, 42), AudioManager.StationName(s), true, sel ? UISkin.ButtonSel : UISkin.ButtonSmall))
                    AudioManager.RadioSetStation(s);
                y += 48;
            }
            y += 6;
            float hh = left.yMax - y - 16;
            if (hh > 40)
                GUI.Label(new Rect(x, y, bw, hh), UISkin.Col(L("Neue Stücke kommen mit dem Fortschritt dazu: für jeden gereinigten Bereich und jedes Großprojekt. Lautstärke = Musik."), UISkin.TextDim), UISkin.WrapSmall);

            // ------------------------------------------------ Rechts: Stücke
            const int key = 470;
            UINav.BeginScroll(key, right);
            float sw = UINav.ScrollWidth(key, right);
            float ry = 0;
            int free = 0;
            foreach (var t in Story.Tracks) if (w.RadioUnlocked.Contains(t.Id)) free++;
            GUI.Label(new Rect(0, ry, sw, 30), Loc.F("Stücke: <b>{0} / {1}</b>", free, Story.Tracks.Count), UISkin.Label);
            ry += 36;
            string lastGroup = "-";
            foreach (var t in Story.Tracks)
            {
                string group = t.Planet;
                if (group != lastGroup)
                {
                    lastGroup = group;
                    PlanetDef pd = null;
                    string gname = group != null && GameData.Planets.TryGetValue(group, out pd) ? pd.Name : L("Allgemein");
                    GUI.Label(new Rect(0, ry + 4, sw, 28), UISkin.Col(gname, pd != null ? UISkin.FromRgb(pd.Accent) : UISkin.Accent), UISkin.LabelBold);
                    ry += 34;
                }
                bool has = w.RadioUnlocked.Contains(t.Id);
                bool playing = cur == t;
                string label = has ? (playing ? "♪ " : "") + L(t.Title) : "○ " + L(t.UnlockHint);
                if (UINav.Button(new Rect(0, ry, sw, 40), label, has, playing ? UISkin.ButtonSel : UISkin.ButtonSmall) && has)
                    AudioManager.RadioPlay(t.Id);
                ry += 46;
            }
            UINav.EndScroll(ry);
        }
    }
}
