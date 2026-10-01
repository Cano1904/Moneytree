using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Werkstatt-Zeile „TNT-Ladung“: Preis je Planet, Vorrat (höchstens <see cref="GameData.TntMaxCarry"/>), Kaufen (1 bzw. auffüllen)
    /// und eine kurze Erklärung zum Werfen. Eigene Klasse – das Spielmenü hängt sie nur unter die Upgrades.
    /// </summary>
    public static class TntShop
    {
        static string L(string de) { return Loc.T(de); }

        public static float Row(GameApp app, float width, float y, bool inBase)
        {
            var w = app.W; var me = app.Me;
            if (w == null || me == null) return y;
            int price = GameData.TntPrice(w.CurrentPlanet);
            int room = GameData.TntMaxCarry - me.Tnt;
            string reason = null;
            if (room <= 0) reason = L("Vorrat voll");
            else if (w.Credits < price) reason = Loc.F("Es fehlen {0} Credits", price - w.Credits);
            else if (!inBase) reason = L("Nur am Stützpunkt");
            string desc = Loc.F("Sprengt große Müllberge in viele sammelbare Stücke (ein Teil verweht als Staub) – und wirft Mitspieler harmlos durch die Luft. Werfen: [{0}] halten, mit der Kamera zielen, loslassen. Nicht im Stützpunkt, im Wasser oder im Sturm.", InputMap.Label(GameAction.ThrowTnt));
            float textW = width - 300;
            float dh = UISkin.TextHeight(UISkin.WrapSmall, desc, textW);
            float h = Mathf.Max(96f, 34 + dh + 30);
            var r = new Rect(0, y, width, h);
            UISkin.RoundRect(r, new Color(1f, 0.3f, 0.2f, 0.07f));
            GUI.Label(new Rect(14, y + 6, textW, 28), "<b>" + L("TNT-Ladung") + "</b>" + UISkin.Col("   " + Loc.F("dabei: {0}/{1}", me.Tnt, GameData.TntMaxCarry), UISkin.TextDim), UISkin.Label);
            GUI.Label(new Rect(14, y + 34, textW, dh + 4), desc, UISkin.WrapSmall);
            float bx = width - 280;
            GUI.Label(new Rect(bx, y + 6, 266, 28), "<b>" + price + L("</b> Credits"), UISkin.LabelRight);
            if (UINav.Button(new Rect(bx, y + 38, 128, 40), L("1 kaufen"), reason == null, reason == null ? UISkin.ButtonSel : UISkin.ButtonSmall))
                app.Act(new JObj().Set("a", "buytnt").Set("n", 1));
            bool fill = reason == null && room > 1 && w.Credits >= (long)price * room;
            if (UINav.Button(new Rect(bx + 138, y + 38, 128, 40), L("Auffüllen"), fill, fill ? UISkin.ButtonSel : UISkin.ButtonSmall))
                app.Act(new JObj().Set("a", "buytnt").Set("n", room));
            if (reason != null) GUI.Label(new Rect(bx - 60, y + 80, 326, 22), UISkin.Col(reason, UISkin.Warn), UISkin.LabelRight);
            return y + h + 8;
        }
    }
}
