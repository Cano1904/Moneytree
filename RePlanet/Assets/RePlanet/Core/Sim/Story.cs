using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Eine Erzählerzeile im Spiel (Helmut): Auslöser-Id, Aufnahme (Resources/Voice/&lt;File&gt;), Untertitel, maximale Dauer.</summary>
    public class NarrationLine
    {
        public string Id, File, Text;
        public float MaxDuration;
    }

    /// <summary>Ein Stück im Radio: Musikstück aus <see cref="Synth.Music"/> mit eigener Stem-Mischung.</summary>
    public class RadioTrack
    {
        public string Id, Title, Piece;
        /// <summary>Planet (für Sender und Freischaltung); null = allgemeines Stück.</summary>
        public string Planet;
        /// <summary>Lautstärke je Stem in der Reihenfolge von <see cref="Synth.StemNames"/> (pad, piano, bass, choir, brass, perc, wind).</summary>
        public float[] Mix;
        /// <summary>Wie wird es freigeschaltet (für die Anzeige, deutsch).</summary>
        public string UnlockHint;
    }

    /// <summary>
    /// Erzählerzeilen im Spiel und Radio-Stücke: Kataloge und Freischaltregeln.
    /// Der Server merkt sich, welche Zeilen schon liefen und welche Stücke frei sind (<see cref="WorldState.Narrated"/>,
    /// <see cref="WorldState.RadioUnlocked"/>, Speicherteil „story“) – jede Zeile läuft pro Spielstand nur einmal.
    /// </summary>
    public static class Story
    {
        public static readonly List<NarrationLine> Lines = new List<NarrationLine>();
        public static readonly Dictionary<string, NarrationLine> LineById = new Dictionary<string, NarrationLine>();
        public static readonly List<RadioTrack> Tracks = new List<RadioTrack>();
        public static readonly Dictionary<string, RadioTrack> TrackById = new Dictionary<string, RadioTrack>();

        static void L(string id, float max, string text)
        {
            var l = new NarrationLine { Id = id, File = "game_" + (Lines.Count + 1).ToString("00"), Text = text, MaxDuration = max };
            Lines.Add(l);
            LineById[id] = l;
        }

        static void R(string id, string title, string piece, string planet, string hint, params float[] mix)
        {
            var t = new RadioTrack { Id = id, Title = title, Piece = piece, Planet = planet, Mix = mix, UnlockHint = hint };
            Tracks.Add(t);
            TrackById[id] = t;
        }

        static Story()
        {
            // ------------------------------------------------ Erzählerzeilen (game_01 … game_21), Reihenfolge = Dateinummer
            L("land_terra", 5.5f, "Die alte Erde. Sie hat lange auf jemanden gewartet, der bleibt.");
            L("land_pyra", 5.5f, "Pyra glühte einst vor Arbeit. Jetzt glüht nur noch der Sand.");
            L("land_pelagia", 5.0f, "Pelagia. Ein Meer, das sich nach klarem Wasser sehnt.");
            L("land_nivalis", 6.5f, "Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen.");
            L("first_deposit", 5.0f, "Das erste Stück ist heimgebracht. So fängt jede Heimkehr an.");
            L("first_sale", 4.5f, "Aus dem, was wir fortwarfen, wird wieder etwas wert.");
            L("first_delivery", 5.0f, "Von fern kommt ein Frachter. Du bist nicht mehr ganz allein.");
            L("first_zone", 5.0f, "Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran.");
            L("first_areaclean", 6.5f, "Der größte Berg ist abgetragen. Darunter liegt eine Straße, die man fast vergessen hatte.");
            L("area_full", 6.0f, "Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen.");
            L("first_awaken", 5.0f, "Die Lichter gehen wieder an. Leise, eines nach dem anderen.");
            L("planet_complete", 5.5f, "Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben.");
            L("first_storm", 4.5f, "Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund.");
            L("first_sandstorm", 5.0f, "Der Sand wandert wieder. Morgen sehen die Wege anders aus.");
            L("first_night", 6.0f, "Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können.");
            L("first_morning", 5.5f, "Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet.");
            L("first_shutdown", 5.0f, "Manchmal geht einem die Kraft aus. Das ist keine Schande.");
            L("nivalis_unlocked", 5.5f, "Das Eis ruft. Auf Nivalis wartet das letzte Signal.");
            L("coop_join", 5.0f, "Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter.");
            L("first_lore", 5.0f, "Ein Fundstück. Jemand hat es gewusst – und trotzdem nichts getan.");
            L("first_eco", 6.0f, "Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet.");

            // ------------------------------------------------ Radio (Stem-Mischungen der vorhandenen Musikstücke)
            //                                                                           pad  piano bass choir brass perc wind
            R("theme", "Eine zweite Chance (Hauptthema)", "menu", null, "Von Anfang an im Radio.", 1f, 1f, 1f, 1f, 1f, 1f, 0.6f);
            var names = new Dictionary<string, string[]>
            {
                { "terra", new[] { "Goldene Straßen", "Buslinie 7", "Gewächshauslicht", "TERRA – Die Stadt erwacht" } },
                { "pyra", new[] { "Roter Sand", "Schrottmarkt", "Gießereifeuer", "PYRA – Glut und Stahl" } },
                { "pelagia", new[] { "Türkise Stille", "Küstenwind", "Lagunenlicht", "PELAGIA – Das Riff atmet" } },
                { "nivalis", new[] { "Polarlicht", "Rechenzentrum", "Raumhafen", "NIVALIS – Das Signal" } },
            };
            foreach (var kv in names)
            {
                string p = kv.Key;
                var n = kv.Value;
                R(p + "_0", n[0], p, p, "Ersten Bereich reinigen.", 1f, 1f, 0f, 0f, 0f, 0f, 0.7f);
                R(p + "_1", n[1], p, p, "Zweiten Bereich reinigen.", 0.9f, 1f, 1f, 0f, 0f, 0.8f, 0.4f);
                R(p + "_2", n[2], p, p, "Dritten Bereich reinigen.", 1f, 0.6f, 1f, 1f, 0.8f, 0.6f, 0.3f);
                R(p + "_full", n[3], p, p, "Großprojekt des Planeten abschließen.", 1f, 1f, 1f, 1f, 1f, 1f, 0.5f);
            }
            R("ending", "Heimkehr (Abspann)", "ending", null, "Die Arche HORIZONT zurückholen.", 1f, 1f, 1f, 1f, 1f, 1f, 0.5f);
        }

        /// <summary>Darf das Stück nach dem Spielstand frei sein?</summary>
        public static bool Eligible(WorldState s, RadioTrack t)
        {
            if (t.Id == "theme") return true;
            if (t.Id == "ending") return s.CampaignDone;
            if (t.Planet == null || !s.Planets.ContainsKey(t.Planet)) return false;
            var ps = s.Planets[t.Planet];
            if (t.Id.EndsWith("_full", StringComparison.Ordinal))
            {
                ProjectState st;
                return ps.Projects.TryGetValue(GameData.ProjectId(t.Planet, 2), out st) && st.Done;
            }
            int a = t.Id[t.Id.Length - 1] - '0';
            if (a < 0 || a > 2) return false;
            return Rules.Cleanliness(ps, a) >= GameData.AreaCleanThreshold;
        }

        /// <summary>Freigeschaltete Stücke in Katalogreihenfolge (planet = null → alle).</summary>
        public static List<RadioTrack> Unlocked(WorldState s, string planet = null)
        {
            var l = new List<RadioTrack>();
            foreach (var t in Tracks)
                if (s.RadioUnlocked.Contains(t.Id) && (planet == null || t.Planet == planet || t.Planet == null && t.Id == "theme")) l.Add(t);
            return l;
        }

        /// <summary>
        /// Alter Spielstand ohne „story“-Teil: Zeilen, deren Anlass offensichtlich schon vorbei ist, gelten als gehört –
        /// sonst würde z. B. „die erste Nacht“ nach 20 Stunden Spielzeit erzählt.
        /// </summary>
        public static void InferNarrated(WorldState s)
        {
            Action<string> mark = id => { if (LineById.ContainsKey(id)) s.Narrated.Add(id); };
            bool anyZone = false, anyClean = false, anyFull = false, anyProject = false, anyGreat = false, anyEco = false;
            foreach (var kv in s.Planets)
            {
                var ps = kv.Value;
                if (ps.Visited) mark("land_" + kv.Key);
                var l = WorldGen.Get(kv.Key);
                for (int z = 0; z < l.Zones.Count && !anyZone; z++) if (Rules.ZoneCleared(ps, z)) anyZone = true;
                for (int a = 0; a < 3; a++)
                {
                    float c = Rules.Cleanliness(ps, a);
                    if (c >= GameData.AreaCleanThreshold) anyClean = true;
                    if (c >= 0.999f) anyFull = true;
                    if (Rules.EcoFraction(s, ps, a) >= 0.999f) anyEco = true;
                }
                foreach (var pk in ps.Projects)
                    if (pk.Value.Done) { anyProject = true; if (pk.Key.EndsWith("_p3", StringComparison.Ordinal)) anyGreat = true; }
                if (ps.StormCount > 0) mark(kv.Key == "pyra" ? "first_sandstorm" : "first_storm");
            }
            if (s.Stat("collected") > 0) mark("first_deposit");
            if (s.Stat("sales") > 0) mark("first_sale");
            if (anyZone) mark("first_zone");
            if (anyClean) mark("first_areaclean");
            if (anyFull) mark("area_full");
            if (anyProject) mark("first_awaken");
            if (anyGreat) mark("planet_complete");
            if (anyEco) mark("first_eco");
            if (s.PlayTime > 900) { mark("first_night"); mark("first_morning"); }
            if (s.Stat("shutdowns") > 0) mark("first_shutdown");
            if (s.Unlocked.Contains("nivalis")) mark("nivalis_unlocked");
            if (s.Players.Count > 1) mark("coop_join");
            if (s.Lore.Count > 0) mark("first_lore");
        }
    }

    public partial class Game
    {
        /// <summary>Erzählerzeile auslösen – nur beim ersten Mal pro Spielstand (danach still).</summary>
        public bool Narrate(string id)
        {
            if (id == null || !Story.LineById.ContainsKey(id) || !S.Narrated.Add(id)) return false;
            DW("story");
            Fx(new JObj().Set("k", "narrate").Set("id", id));
            return true;
        }

        /// <summary>Gleicht die Radio-Freischaltungen mit dem Spielstand ab (announce = Meldung an die Clients).</summary>
        public int SyncRadio(bool announce)
        {
            int n = 0;
            foreach (var t in Story.Tracks)
            {
                if (S.RadioUnlocked.Contains(t.Id) || !Story.Eligible(S, t)) continue;
                S.RadioUnlocked.Add(t.Id);
                n++;
                if (announce) Fx(new JObj().Set("k", "radio").Set("id", t.Id).Set("name", t.Title));
            }
            if (n > 0) DW("story");
            return n;
        }

        /// <summary>Beim Laden: alte Spielstände ohne „story“-Teil nachziehen, Radio abgleichen.</summary>
        void InitStory()
        {
            if (!S.StoryLoaded) { Story.InferNarrated(S); S.StoryLoaded = true; }
            SyncRadio(false);
        }

        /// <summary>Reaktion auf eigene Spielereignisse: Erzählerzeilen und Radio-Freischaltungen.</summary>
        void OnStoryFx(JObj f)
        {
            string k = f.Str("k");
            switch (k)
            {
                case null:
                case "narrate":
                case "radio":
                    return;
                case "arrive": Narrate("land_" + f.Str("planet")); break;
                case "join":
                    {
                        Narrate("land_" + S.CurrentPlanet);
                        int online = 0;
                        foreach (var p in S.Players.Values) if (p.Online) online++;
                        if (online > 1) Narrate("coop_join");
                        break;
                    }
                case "deposit": Narrate("first_deposit"); break;
                case "sell": Narrate("first_sale"); break;
                case "delivery": Narrate("first_delivery"); break;
                case "zone": Narrate("first_zone"); break;
                case "areaclean": Narrate("first_areaclean"); SyncRadio(true); break;
                case "awaken": Narrate(f.Bool("great") ? "planet_complete" : "first_awaken"); SyncRadio(true); break;
                case "storm": if (f.Bool("on")) Narrate(S.CurrentPlanet == "pyra" ? "first_sandstorm" : "first_storm"); break;
                case "nightfall": Narrate("first_night"); break;
                case "morning": Narrate("first_morning"); break;
                case "shutdown": Narrate("first_shutdown"); break;
                case "unlock": if (f.Str("planet") == "nivalis") Narrate("nivalis_unlocked"); break;
                case "lore": Narrate("first_lore"); break;
                case "eco": Narrate("first_eco"); break;
                case "ending": SyncRadio(true); break;
            }
        }

        /// <summary>Bereich vollständig (100 %) gereinigt – aus RemoveObj.</summary>
        void CheckAreaFull(float before, float after)
        {
            if (before < 0.999f && after >= 0.999f) Narrate("area_full");
        }
    }
}
