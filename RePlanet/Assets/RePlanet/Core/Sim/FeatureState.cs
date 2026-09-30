using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Reparierter Helferroboter: sammelt langsam kleinen Müll im Umkreis seines Arbeitsorts (<see cref="Home"/>) und
    /// schickt die Ladung per Rohrpost-Kapsel ins Lager des Stützpunkts. Gespeichert werden Arbeitsort, Position und Ladung;
    /// Ziel, Zustand und „folgt Spieler“ sind Laufzeitwerte des Servers.
    /// </summary>
    public class HelperBot
    {
        public string Id;
        public V3 Home, Pos;
        public float Yaw;
        public List<Item> Load = new List<Item>();

        // Laufzeit (Server)
        /// <summary>0 = sucht, 1 = fährt zum Objekt, 2 = kehrt zum Arbeitsort zurück, 3 = verschickt die Ladung, 4 = folgt einem Spieler.</summary>
        public int State;
        public string Target;
        public float Timer;
        /// <summary>Spieler-ID, dem der Helfer gerade folgt (zum Umsetzen des Arbeitsorts), sonst null.</summary>
        public string Follow;

        public JObj ToJson()
        {
            var o = new JObj().Set("id", Id).Set("h", Home.ToJson(1)).Set("p", Pos.ToJson(2)).Set("y", Json.R(Yaw, 2)).Set("l", Item.ListToJson(Load));
            if (Follow != null) o["f"] = Follow;
            return o;
        }

        public static HelperBot FromJson(JObj o, PlanetLayout l)
        {
            if (o == null) return null;
            var id = o.Str("id");
            if (id == null || l == null || l.Bots.Find(s => s.Id == id) == null) return null;
            var b = new HelperBot { Id = id, Home = V3.FromArr(o.Floats("h")), Pos = V3.FromArr(o.Floats("p")), Yaw = o.Float("y"), Load = Item.ListFromJson(o.Arr("l")), Follow = o.Str("f") };
            if (!b.Home.IsFinite || !b.Pos.IsFinite) return null;
            return b;
        }
    }

    /// <summary>Erfolg: Zähler (Statistik oder abgeleiteter Wert) erreicht das Ziel → kosmetische Belohnung.</summary>
    public class AchievementDef
    {
        public string Id, Name, Desc, Counter, Reward;
        public long Target;
        /// <summary>Anzeige-Teiler (z. B. Zentimeter → Kilometer) und Einheit.</summary>
        public long Div = 1;
        public string Unit = "";
    }
}
