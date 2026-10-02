using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Schätze im Müll: welches (statische) Müllobjekt einen Schatz enthält, folgt deterministisch aus dem Weltsamen
    /// (<see cref="WorldState.TreasureSeed"/>) – Server und alle Clients rechnen dieselbe Zuordnung aus. Häufige Schätze liegen
    /// in den vorderen Bereichen, seltene weiter hinten oder in schwer zugänglichem Müll, legendäre im letzten Bereich in
    /// Müll, der ein Werkzeug verlangt (eingefroren, unter Wasser, zerlegen, starker Magnet).
    /// </summary>
    public static class Treasures
    {
        static readonly Dictionary<string, Dictionary<int, string>> cache = new Dictionary<string, Dictionary<int, string>>();
        static readonly object cacheLock = new object();

        /// <summary>Samen für neue Welten.</summary>
        public static int NewSeed()
        {
            int v = (int)(Hash.Fnv1a(Guid.NewGuid().ToString("N")) & 0x7FFFFFFF);
            return v == 0 ? 1 : v;
        }

        /// <summary>Samen für ältere Spielstände ohne Schätze: fest aus Weltname und Erstellzeit (bei jedem Laden gleich).</summary>
        public static int DeriveSeed(WorldState s)
        {
            int v = (int)(Hash.Fnv1a("RePlanetSchatz|" + s.WorldName + "|" + s.Created) & 0x7FFFFFFF);
            return v == 0 ? 1 : v;
        }

        /// <summary>true, wenn der Samen neu festgelegt wurde.</summary>
        public static bool EnsureSeed(WorldState s)
        {
            if (s.TreasureSeed != 0) return false;
            s.TreasureSeed = DeriveSeed(s);
            s.TreasureVersion++;
            return true;
        }

        static bool Eligible(TrashObj o)
        {
            var t = o.Def;
            return o.Gate < 0 && !t.Crane && !t.Oil;
        }

        /// <summary>Braucht ein Werkzeug oder Geduld (eingefroren, unter Wasser, zu zerlegen, starker Magnet, nicht greifbar).</summary>
        static bool Hard(TrashObj o)
        {
            var t = o.Def;
            return o.Frozen || o.Underwater || t.CutInto != null || !t.Grab || (t.Magnet && t.MinMagnet >= 2);
        }

        static bool Fits(TrashObj o, int rarity)
        {
            switch (rarity)
            {
                case 0: return o.Area <= 1;
                case 1: return o.Area >= 1 || Hard(o);
                default: return o.Area == 2 && Hard(o);
            }
        }

        /// <summary>Deterministisch gemischte Kandidaten (Objekt-IDs) für einen Schatz.</summary>
        public static List<int> Candidates(string planet, int seed, TreasureDef t)
        {
            var l = WorldGen.Get(planet);
            var list = new List<int>();
            foreach (var o in l.Trash) if (Eligible(o) && Fits(o, t.Rarity)) list.Add(o.Id);
            if (list.Count == 0) foreach (var o in l.Trash) if (Eligible(o) && o.Area == 2) list.Add(o.Id);
            if (list.Count == 0) foreach (var o in l.Trash) if (Eligible(o)) list.Add(o.Id);
            var r = new Rng(seed ^ (int)(Hash.Fnv1a(t.Id) & 0x7FFFFFFF));
            for (int i = list.Count - 1; i > 0; i--) { int j = r.Range(0, i + 1); int tmp = list[i]; list[i] = list[j]; list[j] = tmp; }
            return list;
        }

        /// <summary>Trägerobjekte eines Planeten: Objekt-ID → Schatz-ID (auch schon gefundene).</summary>
        public static Dictionary<int, string> Carriers(WorldState s, string planet)
        {
            string key = planet + "|" + s.TreasureSeed + "|" + s.TreasureVersion + "|" + MovedKey(s);
            lock (cacheLock)
            {
                Dictionary<int, string> map;
                if (cache.TryGetValue(key, out map)) return map;
                map = new Dictionary<int, string>();
                var defs = GameData.TreasuresOf(planet);
                var l = WorldGen.Get(planet);
                // umgezogene zuerst (ihr neuer Platz ist fest), dann die übrigen in Datenreihenfolge
                foreach (var t in defs)
                {
                    int sid;
                    if (s.TreasureMoved.TryGetValue(t.Id, out sid) && sid >= 0 && sid < l.Trash.Count && !map.ContainsKey(sid)) map[sid] = t.Id;
                }
                foreach (var t in defs)
                {
                    if (map.ContainsValue(t.Id)) continue;
                    foreach (var sid in Candidates(planet, s.TreasureSeed, t))
                        if (!map.ContainsKey(sid)) { map[sid] = t.Id; break; }
                }
                if (cache.Count > 64) cache.Clear();
                cache[key] = map;
                return map;
            }
        }

        static string MovedKey(WorldState s)
        {
            if (s.TreasureMoved.Count == 0) return "";
            var keys = new List<string>();
            foreach (var kv in s.TreasureMoved) keys.Add(kv.Key + "=" + kv.Value);
            keys.Sort(StringComparer.Ordinal);
            return string.Join(",", keys.ToArray());
        }

        /// <summary>Ungefundener Schatz in diesem statischen Objekt, sonst null.</summary>
        public static string In(WorldState s, string planet, int sid)
        {
            if (sid < 0 || s.TreasureSeed == 0) return null;
            string id;
            if (!Carriers(s, planet).TryGetValue(sid, out id)) return null;
            return s.TreasureFound.Contains(id) ? null : id;
        }

        /// <summary>Gefundene Schätze eines Planeten (null = alle Planeten).</summary>
        public static int FoundCount(WorldState s, string planet)
        {
            int n = 0;
            foreach (var id in s.TreasureFound)
            {
                TreasureDef t;
                if (GameData.TreasureById.TryGetValue(id, out t) && (planet == null || t.Planet == planet)) n++;
            }
            return n;
        }

        public static int Total(string planet)
        {
            if (planet == null) return GameData.Treasures.Count;
            int n = 0;
            foreach (var t in GameData.Treasures) if (t.Planet == planet) n++;
            return n;
        }

        /// <summary>
        /// Nach dem Laden: Liegt ein noch nicht gefundener Schatz in einem Objekt, das schon eingesammelt ist (Spielstand von vor den
        /// Schätzen), zieht er in das nächste noch liegende Kandidatenobjekt um. So bleibt jeder Satz vollständig findbar.
        /// </summary>
        public static bool Relocate(WorldState s)
        {
            bool changed = false;
            foreach (var ps in s.Planets.Values)
            {
                for (int round = 0; round < 8; round++)
                {
                    var map = Carriers(s, ps.Id);
                    string lost = null;
                    foreach (var kv in map)
                        if (!s.TreasureFound.Contains(kv.Value) && ps.Removed.Get(kv.Key)) { lost = kv.Value; break; }
                    if (lost == null) break;
                    var def = GameData.TreasureById[lost];
                    int to = -1;
                    foreach (var sid in Candidates(ps.Id, s.TreasureSeed, def))
                        if (!ps.Removed.Get(sid) && !map.ContainsKey(sid)) { to = sid; break; }
                    if (to < 0)
                    {
                        var l = WorldGen.Get(ps.Id);
                        foreach (var o in l.Trash)
                            if (Eligible(o) && !ps.Removed.Get(o.Id) && !map.ContainsKey(o.Id)) { to = o.Id; break; }
                    }
                    if (to < 0) break; // nichts mehr da – der Satz bleibt unvollständig (sehr alter, leer geräumter Stand)
                    s.TreasureMoved[lost] = to;
                    s.TreasureVersion++;
                    changed = true;
                }
            }
            return changed;
        }
    }

    public partial class Game
    {
        /// <summary>Spieler, dessen Aktion gerade verarbeitet wird (null im Zeittakt: Helfer, Drohnen).</summary>
        string actor;

        void InitTreasures()
        {
            bool changed = Treasures.EnsureSeed(S);
            if (Treasures.Relocate(S)) changed = true;
            if (changed) DW("treasure");
        }

        /// <summary>Statisches Objekt wurde entfernt: enthielt es einen Schatz, ist er gefunden (wandert in die Vitrine).</summary>
        void FindTreasure(PlanetState ps, TrashObj t)
        {
            var id = Treasures.In(S, ps.Id, t.Id);
            if (id == null) return;
            var def = GameData.TreasureById[id];
            S.TreasureFound.Add(id);
            DW("treasure");
            S.AddStat("treasures", 1); DW("stats");
            var f = new JObj().Set("k", "treasure").Set("id", id).Set("name", def.Name).Set("rarity", def.Rarity).Set("planet", def.Planet)
                .Set("pos", t.Pos.ToJson(1)).Set("n", Treasures.FoundCount(S, def.Planet)).Set("of", Treasures.Total(def.Planet));
            if (actor != null) { f["pid"] = actor; PlayerData p; if (S.Players.TryGetValue(actor, out p)) f["by"] = p.Name; }
            Fx(f);
            SaveReason = "Schatz gefunden";
        }
    }
}
