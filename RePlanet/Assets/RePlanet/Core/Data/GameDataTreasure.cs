using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Seltener Fund im Müll (Schatz/Trophäe): wandert beim Finden in die Vitrine, ist unverkäuflich.</summary>
    public class TreasureDef
    {
        public string Id, Planet, Name, Desc;
        /// <summary>0 = häufig, 1 = selten, 2 = legendär.</summary>
        public int Rarity;
        /// <summary>Formfamilie für Symbol (Vitrine) und kleines 3D-Modell (Fund-Anzeige).</summary>
        public string Icon;
        public uint Color;
    }

    /// <summary>
    /// Schätze im Müll: 30 Einzelstücke (TERRA Haushalt/Spielzeug, PYRA Arbeit/Industrie, PELAGIA Meer/Strand,
    /// NIVALIS Forschung/Technik). Welches Müllobjekt einen Schatz enthält, bestimmt der Weltsamen (<see cref="Treasures"/>).
    /// Ein vollständiger Planetensatz schaltet über einen Erfolg ein kosmetisches Teil frei.
    /// </summary>
    public static partial class GameData
    {
        public static readonly List<TreasureDef> Treasures = new List<TreasureDef>();
        public static readonly Dictionary<string, TreasureDef> TreasureById = new Dictionary<string, TreasureDef>();
        public static readonly string[] RarityNames = { "häufig", "selten", "legendär" };

        static void Tr(string id, string planet, int rarity, string icon, uint color, string name, string desc)
        {
            var t = new TreasureDef { Id = id, Planet = planet, Rarity = rarity, Icon = icon, Color = color, Name = name, Desc = desc };
            Treasures.Add(t);
            TreasureById[id] = t;
        }

        public static List<TreasureDef> TreasuresOf(string planet)
        {
            var l = new List<TreasureDef>();
            foreach (var t in Treasures) if (t.Planet == planet) l.Add(t);
            return l;
        }

        /// <summary>Erfolg für den vollständigen Satz eines Planeten (bzw. aller Planeten).</summary>
        public static string TreasureAchievement(string planet) { return planet == null ? "ach_schatz_alle" : "ach_schatz_" + planet; }

        static void DefineTreasures()
        {
            // ---------------------------------------------------------------- TERRA – Haushalt und Spielzeug
            Tr("tr_comic", "terra", 0, "comic", 0xE5533D, "Zerlesenes Comicheft",
                "Heft 47 von „Nebelkatze“. Die letzte Seite fehlt – als hätte jemand nicht gewollt, dass es endet.");
            Tr("tr_figur", "terra", 0, "figure", 0x3F7FD9, "Kleine Heldenfigur",
                "Ein Plastikheld mit abgekautem Umhang. Er hat viele Welten gerettet, meistens auf dem Küchentisch.");
            Tr("tr_postkarte", "terra", 0, "card", 0x6CC4F0, "Postkarte vom Meer",
                "„Wetter schön, Essen salzig, wir vermissen euch.“ Abgestempelt, aber nie angekommen.");
            Tr("tr_spielmodul", "terra", 0, "cartridge", 0x8A8F98, "Altes Spielmodul",
                "Ein graues Steckmodul mit verblichenem Etikett. Irgendwo darin wartet ein Spielstand, den niemand mehr fortsetzt.");
            Tr("tr_teddy", "terra", 1, "teddy", 0xB07A4A, "Einäugiger Teddy",
                "Ein Knopfauge fehlt, das andere schaut noch immer freundlich. Er wartet geduldig darauf, ins Bett gebracht zu werden.");
            Tr("tr_schneekugel", "terra", 1, "globe", 0xCFE8F5, "Schneekugel mit Häuschen",
                "Schüttelt man sie, schneit es über einem kleinen Haus mit gelbem Fenster. Drinnen brennt immer Licht.");
            Tr("tr_schallplatte", "terra", 1, "vinyl", 0x1E1E22, "Schallplatte „Sommerregen“",
                "Die Hülle ist aufgequollen, die Rille glänzt noch. Wer sie auflegt, hört ein Lied über warme Pfützen.");
            Tr("tr_spieluhr", "terra", 2, "musicbox", 0xE8B4C8, "Spieluhr mit Tänzerin",
                "Aufgezogen dreht sich die kleine Tänzerin noch immer. Die Melodie stockt an derselben Stelle – wie ein Atemholen.");
            // ---------------------------------------------------------------- PYRA – Arbeit und Industrie
            Tr("tr_brotdose", "pyra", 0, "lunchbox", 0x5E8C6A, "Blechbrotdose",
                "Innen ein Zettel: „Iss auch das Obst!“ Das Obst ist lange fort, der Zettel ist geblieben.");
            Tr("tr_schluessel", "pyra", 0, "wrench", 0xC9CED3, "Silberner Schraubenschlüssel",
                "Ein Geschenk zur Gesellenprüfung – nie benutzt, immer poliert. Er lag in einem Futteral aus Samt.");
            Tr("tr_radio", "pyra", 0, "radio", 0xD9A566, "Kleines Transistorradio",
                "Mit Klebeband an einen Spind geklebt. Die Skala steht auf einem Sender, auf dem niemand mehr sendet.");
            Tr("tr_helm", "pyra", 1, "helmet", 0xF2C14E, "Schutzhelm mit Aufklebern",
                "Ein Blümchen, ein Lachgesicht, eine krakelige Sonne – aufgeklebt von einem Kind, damit Papa heil nach Hause kommt.");
            Tr("tr_pokal", "pyra", 1, "cup", 0xE8C04A, "Pokal „Beste Schicht des Jahres“",
                "Vergoldetes Blech auf einem Sockel aus Kunststein. Unten stehen neun Namen, einer ist mit einem Herz umkringelt.");
            Tr("tr_anhaenger", "pyra", 1, "pendant", 0xC87B4A, "Zahnrad-Anhänger",
                "Aus einem Uhrwerkteil geschliffen und an ein Lederband geknotet. Schmuck von jemandem, der mit den Händen dachte.");
            Tr("tr_taschenuhr", "pyra", 2, "watch", 0xD4AF37, "Taschenuhr des Vorarbeiters",
                "Innen die Gravur „Für 30 Jahre Schicht“. Sie ist um 6:14 stehen geblieben, kurz vor Feierabend.");
            // ---------------------------------------------------------------- PELAGIA – Meer und Strand
            Tr("tr_muschel", "pelagia", 0, "shell", 0xF3D9C8, "Perlmuttmuschel",
                "Innen schimmert sie wie ein Sonnenaufgang über dem Wasser – und dieses Schimmern ist echt.");
            Tr("tr_schaufel", "pelagia", 0, "spade", 0xE24A3B, "Rote Sandschaufel",
                "Von einem Kind, das Burgen bauen wollte, höher als die Flut. Die Flut hat trotzdem gewonnen.");
            Tr("tr_taucherbrille", "pelagia", 0, "goggles", 0x3FA7D6, "Kinder-Taucherbrille",
                "Das Gummiband riecht noch nach Sonnencreme. Unter Wasser sah die Welt damals bunt aus.");
            Tr("tr_segelboot", "pelagia", 0, "boat", 0xA0714F, "Kleines Holzsegelboot",
                "Das Segel ist aus einem alten Hemd geschnitten. Es ist mehr gesegelt als die meisten echten Schiffe.");
            Tr("tr_flaschenpost", "pelagia", 1, "bottle", 0x7FD6C9, "Flaschenpost",
                "Der Zettel ist fast unleserlich. Nur „… wenn du das findest, wink mal“ ist noch zu erkennen. MIKO winkt.");
            Tr("tr_kompass", "pelagia", 1, "compass", 0xC8A04A, "Messingkompass",
                "Die Nadel zittert und findet dann doch nach Norden. Manche Dinge verlieren ihre Richtung nie.");
            Tr("tr_leuchtturm", "pelagia", 1, "lighthouse", 0xE85D3A, "Leuchtturm-Spardose",
                "Ein Leuchtturm aus Keramik mit Schlitz im Dach. Drinnen klimpert Kleingeld für eine Reise, die nie stattfand.");
            Tr("tr_perlenkette", "pelagia", 2, "necklace", 0xF4EFE6, "Perlenkette",
                "Kleine, ungleiche Perlen, einzeln gesammelt und aufgefädelt – eine für jeden Sommer am Meer.");
            // ---------------------------------------------------------------- NIVALIS – Forschung und Technik
            Tr("tr_thermoskanne", "nivalis", 0, "thermos", 0x9AA5B1, "Verbeulte Thermoskanne",
                "Mit Namensschild „Labor 3“. Innen ist der Tee zu Eis geworden – mitsamt der Zitronenscheibe.");
            Tr("tr_schachfigur", "nivalis", 0, "knight", 0x8C6B4A, "Geschnitzter Springer",
                "Eine Schachfigur aus Treibholz. Das Brett fehlt, die Partie ist unentschieden geblieben.");
            Tr("tr_rechner", "nivalis", 0, "calculator", 0x4A4F57, "Solar-Taschenrechner",
                "Auf der Anzeige steht noch 0,7734. Dreht man ihn um, sagt er leise hallo.");
            Tr("tr_polarfoto", "nivalis", 0, "photo", 0x3FE0A0, "Foto vom Polarlicht",
                "Ein Sofortbild, grün und verwackelt. Auf der Rückseite: „Unser erster Winter hier. Er war schön.“");
            Tr("tr_tagebuch", "nivalis", 1, "book", 0x2B3A67, "Forschertagebuch",
                "Letzter Eintrag: „Messwerte stabil. Die Flechten wachsen wieder. Es ist nicht zu spät.“ Datiert auf den Tag des Abflugs.");
            Tr("tr_pinguin", "nivalis", 1, "penguin", 0x2C3440, "Plüschpinguin mit Mütze",
                "Das Maskottchen der Station. Er saß auf dem Hauptrechner und hat, so hieß es, nie einen Absturz zugelassen.");
            Tr("tr_kristall", "nivalis", 2, "crystal", 0x9FE3FF, "Kristall-Speicherwürfel",
                "Ein Würfel aus Glas, in dem Licht wie Wasser steht. Darauf gespeichert: ein Lied, das die Kinder der Station gesungen haben.");

            // Belohnungen: ein kosmetisches Teil je vollständigem Planetensatz, eins für alle 30 (Hinweis setzt Ach)
            Co("a_bernstein", "accent", "Bernstein", 0xE0A040, "");
            Co("c_messing", "color", "Messing", 0xB8963E, "");
            Co("c_perlmutt", "color", "Perlmutt", 0xDCE6EA, "");
            Co("a_polarblau", "accent", "Polarblau", 0x7FC8F0, "");
            Co("c_schatzgold", "color", "Schatzgold", 0xE3B23C, "");
            Ach("ach_schatz_terra", "Kramkiste", "Finde alle Schätze im Müll von TERRA.", "treasure:terra", 8, "a_bernstein");
            Ach("ach_schatz_pyra", "Werkbankfunde", "Finde alle Schätze im Müll von PYRA.", "treasure:pyra", 7, "c_messing");
            Ach("ach_schatz_pelagia", "Strandgut", "Finde alle Schätze im Müll von PELAGIA.", "treasure:pelagia", 8, "c_perlmutt");
            Ach("ach_schatz_nivalis", "Eisarchiv", "Finde alle Schätze im Müll von NIVALIS.", "treasure:nivalis", 7, "a_polarblau");
            Ach("ach_schatz_alle", "Kurator der Vitrine", "Finde alle 30 Schätze auf allen vier Planeten.", "treasure", 30, "c_schatzgold");
        }
    }
}
