using System;

namespace RePlanet.Core
{
    /// <summary>Englisch: TNT (kaufen, werfen, sprengen, Treffer) und Schätze im Müll (Vitrine, Fund-Meldungen, Erfolge).</summary>
    public static partial class Loc
    {
        static void FillEnglishFun(Action<string, string> e)
        {
            // ------------------------------------------------ Schätze: Daten
            e("häufig", "common"); e("selten", "rare"); e("legendär", "legendary");
            e("Zerlesenes Comicheft", "Dog-eared comic book");
            e("Heft 47 von „Nebelkatze“. Die letzte Seite fehlt – als hätte jemand nicht gewollt, dass es endet.", "Issue 47 of “Fog Cat”. The last page is missing – as if someone didn't want it to end.");
            e("Kleine Heldenfigur", "Little hero figure");
            e("Ein Plastikheld mit abgekautem Umhang. Er hat viele Welten gerettet, meistens auf dem Küchentisch.", "A plastic hero with a chewed cape. He saved many worlds, mostly on the kitchen table.");
            e("Postkarte vom Meer", "Postcard from the sea");
            e("„Wetter schön, Essen salzig, wir vermissen euch.“ Abgestempelt, aber nie angekommen.", "“Weather lovely, food salty, we miss you.” Stamped, but never delivered.");
            e("Altes Spielmodul", "Old game cartridge");
            e("Ein graues Steckmodul mit verblichenem Etikett. Irgendwo darin wartet ein Spielstand, den niemand mehr fortsetzt.", "A grey cartridge with a faded label. Somewhere inside waits a saved game nobody will continue.");
            e("Einäugiger Teddy", "One-eyed teddy bear");
            e("Ein Knopfauge fehlt, das andere schaut noch immer freundlich. Er wartet geduldig darauf, ins Bett gebracht zu werden.", "One button eye is missing, the other still looks kind. He waits patiently to be tucked into bed.");
            e("Schneekugel mit Häuschen", "Snow globe with a little house");
            e("Schüttelt man sie, schneit es über einem kleinen Haus mit gelbem Fenster. Drinnen brennt immer Licht.", "Shake it and snow falls over a little house with a yellow window. The light inside is always on.");
            e("Schallplatte „Sommerregen“", "Vinyl record “Summer Rain”");
            e("Die Hülle ist aufgequollen, die Rille glänzt noch. Wer sie auflegt, hört ein Lied über warme Pfützen.", "The sleeve is swollen, the groove still shines. Play it and you hear a song about warm puddles.");
            e("Spieluhr mit Tänzerin", "Music box with a dancer");
            e("Aufgezogen dreht sich die kleine Tänzerin noch immer. Die Melodie stockt an derselben Stelle – wie ein Atemholen.", "Wound up, the little dancer still turns. The melody pauses at the same spot – like taking a breath.");
            e("Blechbrotdose", "Tin lunch box");
            e("Innen ein Zettel: „Iss auch das Obst!“ Das Obst ist lange fort, der Zettel ist geblieben.", "Inside, a note: “Eat your fruit too!” The fruit is long gone, the note has stayed.");
            e("Silberner Schraubenschlüssel", "Silver spanner");
            e("Ein Geschenk zur Gesellenprüfung – nie benutzt, immer poliert. Er lag in einem Futteral aus Samt.", "A gift for passing the apprenticeship exam – never used, always polished. It lay in a velvet case.");
            e("Kleines Transistorradio", "Little transistor radio");
            e("Mit Klebeband an einen Spind geklebt. Die Skala steht auf einem Sender, auf dem niemand mehr sendet.", "Taped to a locker. The dial is set to a station nobody broadcasts on any more.");
            e("Schutzhelm mit Aufklebern", "Hard hat with stickers");
            e("Ein Blümchen, ein Lachgesicht, eine krakelige Sonne – aufgeklebt von einem Kind, damit Papa heil nach Hause kommt.", "A little flower, a smiley face, a scribbly sun – stuck on by a child so that Dad comes home safe.");
            e("Pokal „Beste Schicht des Jahres“", "Trophy “Best Shift of the Year”");
            e("Vergoldetes Blech auf einem Sockel aus Kunststein. Unten stehen neun Namen, einer ist mit einem Herz umkringelt.", "Gilded tin on an artificial stone base. Nine names on the bottom, one of them circled with a heart.");
            e("Zahnrad-Anhänger", "Cogwheel pendant");
            e("Aus einem Uhrwerkteil geschliffen und an ein Lederband geknotet. Schmuck von jemandem, der mit den Händen dachte.", "Ground from a clockwork part and knotted to a leather cord. Jewellery from someone who thought with their hands.");
            e("Taschenuhr des Vorarbeiters", "The foreman's pocket watch");
            e("Innen die Gravur „Für 30 Jahre Schicht“. Sie ist um 6:14 stehen geblieben, kurz vor Feierabend.", "Engraved inside: “For 30 years of shifts”. It stopped at 6:14, just before the end of the shift.");
            e("Perlmuttmuschel", "Mother-of-pearl shell");
            e("Innen schimmert sie wie ein Sonnenaufgang über dem Wasser – und dieses Schimmern ist echt.", "Inside it shimmers like a sunrise over the water – and this shimmer is real.");
            e("Rote Sandschaufel", "Red sand spade");
            e("Von einem Kind, das Burgen bauen wollte, höher als die Flut. Die Flut hat trotzdem gewonnen.", "From a child who wanted to build castles taller than the tide. The tide won anyway.");
            e("Kinder-Taucherbrille", "Child's swimming goggles");
            e("Das Gummiband riecht noch nach Sonnencreme. Unter Wasser sah die Welt damals bunt aus.", "The strap still smells of sun cream. Back then the world looked colourful under water.");
            e("Kleines Holzsegelboot", "Little wooden sailing boat");
            e("Das Segel ist aus einem alten Hemd geschnitten. Es ist mehr gesegelt als die meisten echten Schiffe.", "The sail is cut from an old shirt. It has sailed more than most real ships.");
            e("Flaschenpost", "Message in a bottle");
            e("Der Zettel ist fast unleserlich. Nur „… wenn du das findest, wink mal“ ist noch zu erkennen. MIKO winkt.", "The note is almost illegible. Only “… if you find this, give a wave” can still be read. MIKO waves.");
            e("Messingkompass", "Brass compass");
            e("Die Nadel zittert und findet dann doch nach Norden. Manche Dinge verlieren ihre Richtung nie.", "The needle trembles and then finds north after all. Some things never lose their way.");
            e("Leuchtturm-Spardose", "Lighthouse money box");
            e("Ein Leuchtturm aus Keramik mit Schlitz im Dach. Drinnen klimpert Kleingeld für eine Reise, die nie stattfand.", "A ceramic lighthouse with a slot in the roof. Inside jingles small change for a trip that never happened.");
            e("Perlenkette", "Pearl necklace");
            e("Kleine, ungleiche Perlen, einzeln gesammelt und aufgefädelt – eine für jeden Sommer am Meer.", "Small, uneven pearls, gathered one by one and strung – one for every summer by the sea.");
            e("Verbeulte Thermoskanne", "Dented thermos flask");
            e("Mit Namensschild „Labor 3“. Innen ist der Tee zu Eis geworden – mitsamt der Zitronenscheibe.", "With a name tag “Lab 3”. Inside, the tea has turned to ice – lemon slice and all.");
            e("Geschnitzter Springer", "Carved knight");
            e("Eine Schachfigur aus Treibholz. Das Brett fehlt, die Partie ist unentschieden geblieben.", "A chess piece made of driftwood. The board is missing, the game remained a draw.");
            e("Solar-Taschenrechner", "Solar calculator");
            e("Auf der Anzeige steht noch 0,7734. Dreht man ihn um, sagt er leise hallo.", "The display still reads 0.7734. Turn it upside down and it quietly says hello.");
            e("Foto vom Polarlicht", "Photo of the aurora");
            e("Ein Sofortbild, grün und verwackelt. Auf der Rückseite: „Unser erster Winter hier. Er war schön.“", "An instant photo, green and blurry. On the back: “Our first winter here. It was lovely.”");
            e("Forschertagebuch", "Research diary");
            e("Letzter Eintrag: „Messwerte stabil. Die Flechten wachsen wieder. Es ist nicht zu spät.“ Datiert auf den Tag des Abflugs.", "Last entry: “Readings stable. The lichens are growing again. It is not too late.” Dated the day of departure.");
            e("Plüschpinguin mit Mütze", "Plush penguin with a hat");
            e("Das Maskottchen der Station. Er saß auf dem Hauptrechner und hat, so hieß es, nie einen Absturz zugelassen.", "The station's mascot. He sat on the main computer and, so they said, never allowed a crash.");
            e("Kristall-Speicherwürfel", "Crystal memory cube");
            e("Ein Würfel aus Glas, in dem Licht wie Wasser steht. Darauf gespeichert: ein Lied, das die Kinder der Station gesungen haben.", "A glass cube in which light stands like water. Stored on it: a song the station's children sang.");
            e("Bernstein", "Amber"); e("Messing", "Brass"); e("Perlmutt", "Mother-of-pearl"); e("Polarblau", "Polar blue"); e("Schatzgold", "Treasure gold");
            e("Kramkiste", "Odds and Ends"); e("Finde alle Schätze im Müll von TERRA.", "Find all the treasures in TERRA's trash.");
            e("Werkbankfunde", "Workbench Finds"); e("Finde alle Schätze im Müll von PYRA.", "Find all the treasures in PYRA's trash.");
            e("Strandgut", "Beachcomber"); e("Finde alle Schätze im Müll von PELAGIA.", "Find all the treasures in PELAGIA's trash.");
            e("Eisarchiv", "Ice Archive"); e("Finde alle Schätze im Müll von NIVALIS.", "Find all the treasures in NIVALIS's trash.");
            e("Kurator der Vitrine", "Curator of the Display Case"); e("Finde alle 30 Schätze auf allen vier Planeten.", "Find all 30 treasures on all four planets.");
            // ------------------------------------------------ Schätze: Oberfläche und Meldungen
            e("Vitrine", "Display case"); e("Vitrine – {0} von {1} Schätzen gefunden", "Display case – {0} of {1} treasures found");
            e("Schätze stecken selten im Müll – wer genau hinsieht, sieht es aus der Nähe funkeln. Fundstücke sind unverkäuflich und kommen sofort hierher. Ein vollständiger Satz eines Planeten schaltet einen Erfolg mit Kosmetik frei.",
              "Treasures rarely hide in the trash – look closely and you'll see them sparkle from nearby. They can't be sold and come straight here. A complete set for a planet unlocks an achievement with a cosmetic.");
            e("{0} von {1} gefunden", "{0} of {1} found");
            e("Satz komplett ✓ – Erfolg „{0}“, Belohnung: {1}", "Set complete ✓ – achievement “{0}”, reward: {1}");
            e("Satzbelohnung: Erfolg „{0}“ und {1}", "Set reward: achievement “{0}” and {1}");
            e("Noch nicht gefunden. Irgendwo im Müll dieses Planeten funkelt es …", "Not found yet. Somewhere in this planet's trash, something sparkles …");
            e("Fund! {0} ({1}) – ab in die Vitrine. {2}/{3} auf diesem Planeten.", "Found! {0} ({1}) – off to the display case. {2}/{3} on this planet.");
            e("Fund! {0} hat „{1}“ ({2}) entdeckt – ab in die Vitrine.", "Found! {0} discovered “{1}” ({2}) – off to the display case.");
            e("Fund! Ein Helfer hat „{0}“ ({1}) entdeckt – ab in die Vitrine.", "Found! A helper discovered “{0}” ({1}) – off to the display case.");
            e("Schatz gefunden", "Treasure found");
            // ------------------------------------------------ TNT: Oberfläche
            e("TNT werfen (halten, loslassen)", "Throw TNT (hold, release)"); e("TNT-Ladung", "TNT charge"); e("dabei: {0}/{1}", "carried: {0}/{1}");
            e("Vorrat voll", "Fully stocked"); e("Es fehlen {0} Credits", "{0} credits missing"); e("Nur am Stützpunkt", "Only at the base");
            e("1 kaufen", "Buy 1"); e("Auffüllen", "Fill up");
            e("Sprengt große Müllberge in viele sammelbare Stücke (ein Teil verweht als Staub) – und wirft Mitspieler harmlos durch die Luft. Werfen: [{0}] halten, mit der Kamera zielen, loslassen. Nicht im Stützpunkt, im Wasser oder im Sturm.",
              "Blasts big trash mountains into many collectable pieces (some is lost as dust) – and harmlessly sends fellow players flying. Throw: hold [{0}], aim with the camera, release. Not at the base, in water or during a storm.");
            e("Abtauchen · an Land halten: TNT zielen, loslassen: werfen · im Menü: Zurück", "Dive · on land hold: aim TNT, release: throw · in menus: back");
            e("TNT trifft Mitspieler: Getroffene fliegen harmlos durch die Luft (aus = nur der Werfer selbst)", "TNT hits other players: they fly harmlessly through the air (off = only the thrower)");
            e("Wurfkraft · TNT ×{0}", "Throw power · TNT ×{0}"); e("Wurf abgebrochen.", "Throw cancelled.");
            e("{0}× TNT gekauft (−{1} Credits). Werfen: [{2}] halten, zielen, loslassen.", "Bought {0}× TNT (−{1} credits). Throw: hold [{2}], aim, release.");
            e("Die Ladung ist zischend im Wasser versunken.", "The charge sank into the water with a hiss.");
            e("Im Stützpunkt wird nicht gesprengt – die Zündschnur ist erloschen, die Ladung ist zurück im Behälter.", "No blasting at the base – the fuse went out and the charge is back in the container.");
            e("Müllberg ganz zerlegt! {0} Stücke liegen bereit, {1} sind als Staub verweht.", "Trash mountain completely blown apart! {0} pieces are ready, {1} blew away as dust.");
            e("Müllberg gesprengt ({2}/{3}) – {0} Stücke liegen bereit, {1} sind als Staub verweht.", "Trash mountain blasted ({2}/{3}) – {0} pieces are ready, {1} blew away as dust.");
            e("Autsch – zu nah an der eigenen Ladung!", "Ouch – too close to your own charge!");
            e("{0} hat dich mit TNT erwischt!", "{0} got you with TNT!"); e("Treffer! {0} fliegt eine Runde.", "Hit! {0} takes a little flight.");
            // ------------------------------------------------ TNT: Meldungen (Server)
            e("TNT gibt es in der Werkstatt am Stützpunkt.", "TNT is sold in the workshop at the base.");
            e("Mehr als {0} Ladungen kann MIKO nicht tragen.", "MIKO can't carry more than {0} charges.");
            e("Es fehlen {0} Credits für TNT.", "{0} credits missing for TNT.");
            e("Die nächste Ladung ist gleich bereit …", "The next charge is almost ready …"); e("Ungültige Wurfrichtung.", "Invalid throw direction.");
            e("MIKO ist noch ganz benommen …", "MIKO is still quite dazed …");
            e("Keine TNT-Ladung dabei – in der Werkstatt am Stützpunkt kaufen.", "No TNT charge on you – buy some in the workshop at the base.");
            e("Aus dem Fahrzeug wird nicht geworfen – erst aussteigen.", "No throwing from a vehicle – get out first.");
            e("Im Stützpunkt wird nicht gesprengt – geh ein Stück hinaus.", "No blasting at the base – walk out a little.");
            e("Im Wasser zündet keine Zündschnur.", "No fuse will light in the water.");
            e("Im Sturm bläst der Wind jede Zündschnur aus – erst den Sturm abwarten.", "In a storm the wind blows out every fuse – wait out the storm first.");
            e("Hier ist kein Müllberg.", "There is no trash mountain here."); e("Von diesem Müllberg ist nichts mehr übrig.", "Nothing is left of this trash mountain.");
            e("Dieser Müllberg treibt im Wasser – dort zündet keine Ladung.", "This trash mountain floats in the water – no charge will go off there.");
            e("Dieser Bereich ist noch versperrt.", "This area is still blocked.");
            e("Der Staub legt sich noch – in {0} s lässt sich der Müllberg wieder sprengen.", "The dust is still settling – the trash mountain can be blasted again in {0} s.");
        }
    }
}
