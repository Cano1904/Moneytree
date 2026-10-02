using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Spielmenü-Reiter „Vitrine“: alle Schätze je Planet (gefundene mit Symbol in ihrer Farbe, Name, Seltenheit und Text,
    /// fehlende als Silhouette mit „???“), Fortschritt und Belohnung je Satz. Rein datengetrieben aus
    /// <see cref="GameData.Treasures"/> – eigene Klasse, damit die Menügestaltung sie nur einhängen muss.
    /// </summary>
    public static class TreasureTab
    {
        public const string TabId = "treasures";
        public const string TabName = "Vitrine";
        const int ScrollKey = 481;

        static string L(string de) { return Loc.T(de); }

        /// <summary>Reiter im Tablet anmelden (hinter „Archiv“).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            UIRoot.RegisterMenuTab(TabId, TabName, "diamond", (ui, app, c) => Draw(app, c), "archive");
        }

        static readonly Color[] RarityCol = { new Color(0.82f, 0.84f, 0.86f), new Color(0.45f, 0.7f, 1f), new Color(1f, 0.78f, 0.3f) };

        public static void Draw(GameApp app, Rect c)
        {
            var w = app.W;
            if (w == null) return;
            UINav.BeginScroll(ScrollKey, c);
            float sw = UINav.ScrollWidth(ScrollKey, c);
            float y = 0;
            int all = Treasures.FoundCount(w, null);
            GUI.Label(new Rect(0, y, sw, 36), "<b>" + Loc.F("Vitrine – {0} von {1} Schätzen gefunden", all, GameData.Treasures.Count) + "</b>", UISkin.Label);
            y += 38;
            string intro = L("Schätze stecken selten im Müll – wer genau hinsieht, sieht es aus der Nähe funkeln. Fundstücke sind unverkäuflich und kommen sofort hierher. Ein vollständiger Satz eines Planeten schaltet einen Erfolg mit Kosmetik frei.");
            float ih = UISkin.TextHeight(UISkin.WrapSmall, intro, sw);
            GUI.Label(new Rect(0, y, sw, ih + 4), UISkin.Col(intro, UISkin.TextDim), UISkin.WrapSmall);
            y += ih + 12;
            float cardW = sw >= 1100f ? (sw - 3 * 14f) / 4f : sw >= 760f ? (sw - 2 * 14f) / 3f : sw >= 480f ? (sw - 14f) / 2f : sw;
            foreach (var pl in GameData.PlanetOrder)
            {
                var list = GameData.TreasuresOf(pl);
                int found = Treasures.FoundCount(w, pl);
                var pd = GameData.Planets[pl];
                UISkin.Rect(new Rect(0, y + 34, sw, 2), new Color(UISkin.Accent.r, UISkin.Accent.g, UISkin.Accent.b, 0.4f));
                GUI.Label(new Rect(0, y, sw * 0.6f, 34), "<b>" + pd.Name + "</b>" + UISkin.Col("  " + Loc.F("{0} von {1} gefunden", found, list.Count), found >= list.Count ? UISkin.Good : UISkin.TextDim), UISkin.Label);
                var ach = GameData.AchievementById(GameData.TreasureAchievement(pl));
                if (ach != null)
                {
                    CosmeticDef cd;
                    string reward = GameData.Cosmetics.TryGetValue(ach.Reward, out cd) ? cd.Name : ach.Reward;
                    bool got = w.Achievements.Contains(ach.Id);
                    GUI.Label(new Rect(sw * 0.4f, y + 4, sw * 0.6f, 28), UISkin.Col(Loc.F(got ? "Satz komplett ✓ – Erfolg „{0}“, Belohnung: {1}" : "Satzbelohnung: Erfolg „{0}“ und {1}", ach.Name, reward), got ? UISkin.Good : UISkin.TextDim), UISkin.LabelRight);
                }
                y += 44;
                int col = 0; float rowY = y, rowH = 0;
                foreach (var t in list)
                {
                    float h = Card(w, t, new Rect(col * (cardW + 14f), rowY, cardW, 0));
                    rowH = Mathf.Max(rowH, h);
                    col++;
                    if ((col + 1) * (cardW + 14f) - 14f > sw + 1f) { col = 0; rowY += rowH + 12f; rowH = 0; }
                }
                if (col > 0) rowY += rowH + 12f;
                y = rowY + 10f;
            }
            UINav.EndScroll(y + 10);
        }

        /// <summary>Zeichnet eine Karte (Höhe aus dem Inhalt) und gibt die Höhe zurück.</summary>
        static float Card(WorldState w, TreasureDef t, Rect r)
        {
            bool got = w.TreasureFound.Contains(t.Id);
            string desc = got ? Loc.T(t.Desc) : L("Noch nicht gefunden. Irgendwo im Müll dieses Planeten funkelt es …");
            float textW = r.width - 98f;
            float dh = UISkin.TextHeight(UISkin.WrapSmall, desc, textW);
            float h = Mathf.Max(96f, 58f + dh + 10f);
            r.height = h;
            if (Event.current.type == EventType.Repaint)
                UISkin.RoundRect(r, got ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.28f));
            var icon = new Rect(r.x + 12f, r.y + 14f, 70f, 70f);
            if (got) UISkin.RoundRect(new Rect(icon.x - 4, icon.y - 4, icon.width + 8, icon.height + 8), new Color(RarityCol[t.Rarity].r, RarityCol[t.Rarity].g, RarityCol[t.Rarity].b, 0.16f));
            UISkin.Tex(icon, Icon(t.Icon), got ? UISkin.FromRgb(t.Color) : new Color(0f, 0f, 0f, 0.6f));
            string rar = Loc.T(GameData.RarityNames[Mathf.Clamp(t.Rarity, 0, 2)]);
            GUI.Label(new Rect(r.x + 92f, r.y + 6f, textW, 28f), got ? "<b>" + Loc.T(t.Name) + "</b>" : "<b>???</b>", UISkin.Label);
            GUI.Label(new Rect(r.x + 92f, r.y + 32f, textW, 22f), UISkin.Col(rar, RarityCol[t.Rarity]), UISkin.LabelTiny);
            GUI.Label(new Rect(r.x + 92f, r.y + 54f, textW, dh + 4f), UISkin.Col(desc, got ? UISkin.Text : UISkin.TextDim), UISkin.WrapSmall);
            return h;
        }

        // ================================================================== Symbole
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Weißes Symbol (Maske) je Formfamilie, eingefärbt beim Zeichnen: gefunden in der Farbe des Schatzes, fehlend als dunkle Silhouette.
        /// Formen: c = Kreis (x y r), o = Ring (x y r Dicke), r = Rechteck (x y halbe Breite halbe Höhe), t = Dreieck, Koordinaten −1…1.
        /// </summary>
        static readonly Dictionary<string, string> Spec = new Dictionary<string, string>
        {
            { "comic", "r 0 0 0.6 0.8;r 0.15 -0.35 0.3 0.25" },
            { "figure", "c 0 -0.62 0.24;r 0 0 0.3 0.38;r -0.15 0.6 0.12 0.28;r 0.15 0.6 0.12 0.28;r -0.42 -0.05 0.1 0.3;r 0.42 -0.05 0.1 0.3" },
            { "card", "r 0 0 0.85 0.55" },
            { "photo", "r 0 0 0.7 0.8" },
            { "cartridge", "r 0 -0.1 0.7 0.75;r 0 0.75 0.55 0.12" },
            { "teddy", "c 0 0.3 0.5;c 0 -0.4 0.36;c -0.32 -0.72 0.14;c 0.32 -0.72 0.14;c -0.52 0.2 0.18;c 0.52 0.2 0.18" },
            { "globe", "c 0 -0.15 0.65;r 0 0.68 0.6 0.2" },
            { "vinyl", "o 0 0 0.85 0.62;c 0 0 0.12" },
            { "musicbox", "r 0 0.4 0.8 0.45;r 0 -0.15 0.06 0.25;c 0 -0.55 0.18" },
            { "lunchbox", "r 0 0.15 0.85 0.6;o 0 -0.45 0.3 0.08" },
            { "wrench", "t -0.75 0.6 0.55 -0.6 0.7 -0.45;o -0.62 0.55 0.24 0.12;o 0.62 -0.55 0.24 0.12" },
            { "radio", "r 0 0.2 0.85 0.6;t 0.4 -0.4 0.48 -0.4 0.8 -0.95" },
            { "helmet", "c 0 0.25 0.7;r 0 0.35 0.95 0.1" },
            { "cup", "t -0.6 -0.7 0.6 -0.7 0 0.2;r 0 0.45 0.12 0.3;r 0 0.8 0.45 0.12;o -0.65 -0.45 0.22 0.08;o 0.65 -0.45 0.22 0.08" },
            { "pendant", "o 0 -0.2 0.8 0.04;o 0 0.45 0.4 0.18;c 0 0.45 0.12" },
            { "watch", "o 0 0.15 0.7 0.14;c 0 0.15 0.08;r 0 -0.7 0.12 0.12;o 0 -0.88 0.12 0.05;r 0.02 -0.05 0.04 0.22" },
            { "shell", "t 0 0.7 -0.85 -0.45 0.85 -0.45;c 0 -0.45 0.85" },
            { "spade", "r 0 -0.45 0.42 0.42;r 0 0.35 0.08 0.45;r 0 0.82 0.25 0.1" },
            { "goggles", "o -0.42 0 0.36 0.12;o 0.42 0 0.36 0.12;r 0 0 0.12 0.06" },
            { "boat", "t -0.8 0.45 0.8 0.45 0.5 0.8;t -0.6 0.45 0.05 -0.9 0.05 0.45;r 0.08 -0.2 0.03 0.65" },
            { "bottle", "r 0 0.3 0.35 0.6;r 0 -0.55 0.14 0.3;r 0 -0.88 0.17 0.08" },
            { "compass", "o 0 0 0.85 0.12;t 0 -0.6 -0.15 0 0.15 0;t 0 0.6 -0.15 0 0.15 0" },
            { "lighthouse", "t -0.4 0.9 0.4 0.9 0 -0.4;r 0 -0.55 0.25 0.15;t -0.3 -0.7 0.3 -0.7 0 -0.95" },
            { "necklace", "o 0 -0.1 0.75 0.1;c 0 0.7 0.18" },
            { "thermos", "r 0 0.15 0.4 0.8;r 0 -0.75 0.3 0.12" },
            { "knight", "r 0 0.75 0.5 0.15;r 0 0.35 0.3 0.3;t -0.35 0.1 0.4 -0.75 0.4 0.1;t -0.6 -0.2 -0.2 -0.2 0.1 -0.6" },
            { "calculator", "r 0 0 0.6 0.85;r 0 -0.5 0.45 0.18" },
            { "book", "r 0 0 0.7 0.85;r -0.62 0 0.08 0.85" },
            { "penguin", "c 0 0.25 0.55;c 0 -0.5 0.35;t -0.15 -0.5 0.15 -0.5 0 -0.3" },
            { "crystal", "t 0 -0.9 -0.65 0 0.65 0;t 0 0.9 -0.65 0 0.65 0" },
        };

        public static Texture2D Icon(string icon)
        {
            Texture2D tex;
            if (icon != null && icons.TryGetValue(icon, out tex) && tex != null) return tex;
            string spec;
            if (icon == null || !Spec.TryGetValue(icon, out spec)) spec = "c 0 0 0.7";
            const int N = 64;
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Schatz_" + icon };
            var parts = spec.Split(';');
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        float u = ((x + (s % 2) * 0.5f + 0.25f) / N) * 2f - 1f, v = 1f - ((y + (s / 2) * 0.5f + 0.25f) / N) * 2f; // Textur-Zeile 0 = unten, Spezifikation: oben = negativ
                        if (Inside(parts, u, v)) hits++;
                    }
                    px[y * N + x] = new Color(1f, 1f, 1f, hits / 4f);
                }
            tex.SetPixels(px);
            tex.Apply(false);
            icons[icon ?? ""] = tex;
            return tex;
        }

        static bool Inside(string[] parts, float u, float v)
        {
            foreach (var p in parts)
            {
                var a = p.Trim().Split(' ');
                if (a.Length < 4) continue;
                float f1 = F(a, 1), f2 = F(a, 2), f3 = F(a, 3);
                switch (a[0])
                {
                    case "c": if ((u - f1) * (u - f1) + (v - f2) * (v - f2) <= f3 * f3) return true; break;
                    case "o":
                        {
                            float d = Mathf.Sqrt((u - f1) * (u - f1) + (v - f2) * (v - f2));
                            if (Mathf.Abs(d - f3) <= F(a, 4)) return true;
                            break;
                        }
                    case "r": if (Mathf.Abs(u - f1) <= f3 && Mathf.Abs(v - f2) <= F(a, 4)) return true; break;
                    case "t":
                        {
                            float x1 = f1, y1 = f2, x2 = f3, y2 = F(a, 4), x3 = F(a, 5), y3 = F(a, 6);
                            float d1 = (u - x2) * (y1 - y2) - (x1 - x2) * (v - y2), d2 = (u - x3) * (y2 - y3) - (x2 - x3) * (v - y3), d3 = (u - x1) * (y3 - y1) - (x3 - x1) * (v - y1);
                            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                            if (!(neg && pos)) return true;
                            break;
                        }
                }
            }
            return false;
        }

        static float F(string[] a, int i)
        {
            float f;
            return i < a.Length && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 0f;
        }
    }
}
