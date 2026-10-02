using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Geworfene TNT-Ladung: Abwurf, Anfangsgeschwindigkeit und die vom Server berechnete Landestelle. Die Zündschnur brennt
    /// ab dem Wurf; ist sie abgebrannt, explodiert die Ladung an der Landestelle (<see cref="Game"/>, GameTnt.cs).
    /// Gespeichert (Teil „tnt“ des Planeten), damit auch spät beitretende Mitspieler die zischende Ladung sehen.
    /// </summary>
    public class TntCharge
    {
        public string Id, By;
        public V3 From, Vel, Pos;
        /// <summary>Flugzeit bis zur Ruhe (s), gesamte Zündschnur (s), seit dem Wurf verstrichen (s).</summary>
        public float Flight, Fuse, Age;
        /// <summary>Getroffener Müllberg (Index in <see cref="PlanetLayout.Mounds"/>) oder -1.</summary>
        public int Mound = -1;
        /// <summary>Landung: 0 Boden, 1 Wasser (erlischt), 2 Müllberg, 3 Stützpunkt (erlischt).</summary>
        public int Land;

        public float Left { get { return Math.Max(0f, Fuse - Age); } }

        public JObj ToJson()
        {
            return new JObj().Set("id", Id).Set("by", By).Set("f", From.ToJson(2)).Set("v", Vel.ToJson(3)).Set("p", Pos.ToJson(2))
                .Set("fl", Json.R(Flight, 3)).Set("fu", Json.R(Fuse, 3)).Set("age", Json.R(Age, 2)).Set("m", Mound).Set("l", Land);
        }

        public static TntCharge FromJson(JObj o)
        {
            if (o == null || o.Str("id") == null) return null;
            var c = new TntCharge
            {
                Id = o.Str("id"), By = o.Str("by"), From = V3.FromArr(o.Floats("f")), Vel = V3.FromArr(o.Floats("v")), Pos = V3.FromArr(o.Floats("p")),
                Flight = Math.Max(0f, o.Float("fl")), Fuse = Math.Max(0f, o.Float("fu", GameData.TntFuse)), Age = Math.Max(0f, o.Float("age")),
                Mound = o.Int("m", -1), Land = M.Clamp(o.Int("l"), 0, 3)
            };
            if (!c.Pos.IsFinite || !c.From.IsFinite || !c.Vel.IsFinite) return null;
            return c;
        }
    }

    public partial class PlanetState
    {
        /// <summary>Scharfe (geworfene, noch nicht explodierte) TNT-Ladungen auf diesem Planeten.</summary>
        public List<TntCharge> Tnt = new List<TntCharge>();
        /// <summary>Sprengungen je Müllberg (Index → Anzahl) und Zeitpunkt, ab dem er wieder gesprengt werden kann.</summary>
        public Dictionary<int, int> MoundBlasts = new Dictionary<int, int>();
        public Dictionary<int, double> MoundCool = new Dictionary<int, double>();
        /// <summary>Eingesammelte Stücke gesprengter Müllberge je Bereich (Materialeinheiten) – zählen zum Hauptmüll (gedeckelt).</summary>
        public float[] HeapWeight = new float[3];

        public int Blasts(int mound) { int n; return MoundBlasts.TryGetValue(mound, out n) ? n : 0; }

        JObj TntToJson()
        {
            var o = new JObj();
            var q = new List<object>();
            foreach (var c in Tnt) q.Add(c.ToJson());
            o["q"] = q;
            var b = new JObj();
            foreach (var kv in MoundBlasts) if (kv.Value > 0) b[kv.Key.ToString()] = kv.Value;
            o["b"] = b;
            var cd = new JObj();
            foreach (var kv in MoundCool) cd[kv.Key.ToString()] = Math.Round(kv.Value, 1);
            o["cd"] = cd;
            o["hw"] = Json.Arr(Json.R(HeapWeight[0], 1), Json.R(HeapWeight[1], 1), Json.R(HeapWeight[2], 1));
            return o;
        }

        void TntFromJson(JObj o)
        {
            Tnt.Clear(); MoundBlasts.Clear(); MoundCool.Clear();
            HeapWeight = new float[3];
            if (o == null) return;
            var mounds = WorldGen.Get(Id).Mounds;
            foreach (var x in o.Arr("q") ?? new List<object>()) { var c = TntCharge.FromJson(x as JObj); if (c != null) Tnt.Add(c); }
            var b = o.Obj("b");
            if (b != null)
                foreach (var kv in b)
                {
                    int i;
                    if (!int.TryParse(kv.Key, out i) || i < 0 || i >= mounds.Count) continue;
                    int n = M.Clamp((int)Json.ToDouble(kv.Value, 0), 0, GameData.MoundStages(mounds[i]));
                    if (n > 0) MoundBlasts[i] = n;
                }
            var cd = o.Obj("cd");
            if (cd != null)
                foreach (var kv in cd)
                {
                    int i;
                    if (int.TryParse(kv.Key, out i) && i >= 0 && i < mounds.Count) MoundCool[i] = Json.ToDouble(kv.Value, 0);
                }
            var hw = o.Floats("hw");
            if (hw != null) for (int a = 0; a < 3 && a < hw.Length; a++) HeapWeight[a] = Math.Max(0f, M.Finite(hw[a]) ? hw[a] : 0f);
        }
    }

    public partial class WorldState
    {
        /// <summary>Host-Einstellung „TNT trifft Mitspieler“ (gespeichert mit der Welt). Aus: nur der Werfer selbst fliegt.</summary>
        public bool TntHitsPlayers = true;
        /// <summary>Weltsamen für die Verteilung der Schätze (0 = noch nicht festgelegt, siehe <see cref="Treasures.EnsureSeed"/>).</summary>
        public int TreasureSeed;
        /// <summary>Gefundene Schätze (IDs aus <see cref="GameData.Treasures"/>).</summary>
        public HashSet<string> TreasureFound = new HashSet<string>();
        /// <summary>Umgezogene Schätze (Träger war beim Laden eines älteren Stands schon eingesammelt): Schatz → Objekt-ID.</summary>
        public Dictionary<string, int> TreasureMoved = new Dictionary<string, int>();
        /// <summary>Zählt Änderungen an <see cref="TreasureMoved"/> (Zwischenspeicher der Trägerliste).</summary>
        public int TreasureVersion;

        JObj TreasureToJson()
        {
            var mv = new JObj();
            foreach (var kv in TreasureMoved) mv[kv.Key] = kv.Value;
            return new JObj().Set("seed", TreasureSeed).Set("found", new List<object>(TreasureFound)).Set("moved", mv);
        }

        void TreasureFromJson(JObj o)
        {
            TreasureFound.Clear(); TreasureMoved.Clear();
            TreasureVersion++;
            if (o == null) return;
            TreasureSeed = o.Int("seed");
            foreach (var id in o.Strs("found")) if (GameData.TreasureById.ContainsKey(id)) TreasureFound.Add(id);
            var mv = o.Obj("moved");
            if (mv != null) foreach (var kv in mv) if (GameData.TreasureById.ContainsKey(kv.Key)) TreasureMoved[kv.Key] = (int)Json.ToDouble(kv.Value, -1);
        }
    }
}
