using System;

namespace RePlanet.Core
{
    /// <summary>Englisch: Oberfläche (Menüs, Einstellungen, HUD, Karte, Bau- und Fotomodus, Koop, Radio, Intro/Abspann, Erzähler).</summary>
    public static partial class Loc
    {
        static void FillEnglishUi(Action<string, string> e)
        {
            // ------------------------------------------------ Allgemein
            e("An", "On"); e("Aus", "Off"); e("Ja", "Yes"); e("Nein", "No"); e("OK", "OK"); e("Abbrechen", "Cancel"); e("Kopieren", "Copy"); e("Einfügen", "Paste");
            e("Pause", "Pause"); e("Hauptmenü", "Main Menu"); e("Laden …", "Loading …"); e("Hinweis", "Notice"); e("Back", "Back"); e("Start", "Start");
            e(" (Esc)", " (Esc)"); e(" Std. ", " h "); e(" Min.", " min"); e("Credits", "Credits"); e(" Credits", " credits"); e(" Cr", " cr"); e("</b> Cr", "</b> cr");
            e("</b> Credits", "</b> credits"); e(" Vol.", " vol."); e(" Vol.)", " vol.)"); e("Zzz", "Zzz"); e("Zzz …", "Zzz …"); e("Zzz … …", "Zzz … …");
            e("Anzeigefehler: ", "Display error: "); e("Neue Welt", "New World"); e("Welt", "World"); e("Jemand", "Someone");
            // ------------------------------------------------ Hauptmenü, Neues Spiel, Planetenwahl, Spielstände
            e("Eine zweite Chance", "A Second Chance"); e("Meine Welt", "My World"); e("Ja, beenden", "Yes, quit"); e("Version ", "Version ");
            e("Steuerkreuz/Stick · A: Bestätigen · B: Zurück", "D-pad/stick · A: confirm · B: back"); e("Pfeiltasten/Maus · Eingabe: Bestätigen · Esc: Zurück", "Arrow keys/mouse · Enter: confirm · Esc: back");
            e("Name der Welt", "World name"); e("Speicherplatz", "Save slot"); e("⚠ Überschreibt den vorhandenen Stand: ", "⚠ Overwrites the existing save: ");
            e(" · gespeichert ", " · saved "); e("Dieser Speicherplatz ist frei.", "This save slot is free."); e("Los geht's!", "Let's go!");
            e("Beliebige Taste: überspringen", "Any key: skip"); e("Beliebige Taste: weiter", "Any key: continue"); e("Wohin fliegt MIKO zuerst?", "Where does MIKO fly first?");
            e("Startplanet wählen – die anderen erreichst du später mit dem Transportschiff.", "Choose a starting planet – you can reach the others later with the transport ship.");
            e("später", "later"); e("Das Finale – wird später erreichbar.", "The finale – becomes reachable later."); e("Hier landen ›", "Land here ›"); e("Noch gesperrt", "Still locked");
            e("Wähle den Startplaneten – die anderen erreichst du später mit dem Transportschiff.", "Choose the starting planet – you can reach the others later with the transport ship.");
            e("NIVALIS – das Finale – wird später erreichbar.", "NIVALIS – the finale – becomes reachable later."); e("Auswählen", "Select");
            e("Ordner: ", "Folder: "); e("Als Gast kannst du nicht speichern – die Welt gehört dem Host. Laden beendet die Koop-Sitzung.", "As a guest you cannot save – the world belongs to the host. Loading ends the co-op session.");
            e("Aktueller Speicherplatz: ", "Current save slot: "); e(". Beim Laden eines anderen Stands wird die laufende Welt vorher gespeichert.", ". Loading another save stores the running world first.");
            e("  (aktuell)", "  (current)"); e("(leer)", "(empty)"); e("Spielzeit ", "Play time "); e(" Credits · Wiederherstellung ", " credits · Restoration ");
            e(" · Kampagne abgeschlossen", " · Campaign completed"); e("Gespeichert: ", "Saved: "); e("   · Backup vorhanden", "   · Backup available"); e("Wirklich löschen?", "Really delete?");
            e("Ja, löschen", "Yes, delete"); e(" gelöscht.", " deleted."); e("Löschen fehlgeschlagen.", "Deleting failed."); e("Laufende Welt speichern und wechseln?", "Save the running world and switch?");
            e("Sitzung verlassen und laden?", "Leave the session and load?"); e("Ja, laden", "Yes, load"); e("Exportieren", "Export"); e("Exportiert nach: ", "Exported to: ");
            e("Export fehlgeschlagen: ", "Export failed: "); e("Hier speichern", "Save here"); e("Gespeichert in ", "Saved in "); e("Löschen", "Delete");
            e("Koop: das Spiel läuft weiter", "Co-op: the game keeps running"); e("Die Welt steht still.", "The world is paused."); e("Zurück ins Hauptmenü? Die Welt wird gespeichert", "Back to the main menu? The world will be saved");
            e(" und die Mitspieler werden getrennt.", " and the other players will be disconnected."); e("Die Koop-Sitzung verlassen?", "Leave the co-op session?");
            e("Spiel beenden? Die Welt wird vorher gespeichert.", "Quit the game? The world will be saved first."); e("Spiel beenden und die Sitzung verlassen?", "Quit the game and leave the session?");
            e("Ja, zum Hauptmenü", "Yes, to the main menu"); e(" – nur Host", " – host only"); e("MIKO fährt die Systeme hoch …", "MIKO is powering up its systems …");
            e("Automatisch", "Automatic"); e("Spielstand 1", "Save 1"); e("Spielstand 2", "Save 2"); e("Spielstand 3", "Save 3"); e("Neues Spiel", "New Game");
            e("Fortsetzen", "Continue"); e("Koop", "Co-op"); e("Spielstände", "Saves"); e("Einstellungen", "Settings"); e("Beenden", "Quit"); e("Zurück", "Back");
            e("Speichern", "Save"); e("Laden", "Load");
            // ------------------------------------------------ Einstellungen
            e("Grafik", "Graphics"); e("Audio", "Audio"); e("Steuerung", "Controls"); e("Barrierefreiheit", "Accessibility"); e("Sonstiges", "Other");
            e("Bildschirm (", "Screen ("); e("Abgebrochen.", "Cancelled."); e("“ ist jetzt nicht belegt.", "” is now unbound."); e("Qualität", "Quality");
            e("Die Qualitätsstufe setzt Schatten, Kantenglättung, Sichtweite, Partikel und Render-Skalierung. Einzelwerte lassen sich danach anpassen.",
              "The quality level sets shadows, anti-aliasing, view distance, particles and render scale. Individual values can be adjusted afterwards.");
            e("Anzeige", "Display"); e("Fenstermodus", "Window mode"); e("Auflösung", "Resolution"); e("Übernehmen", "Apply"); e("Anzeige übernommen.", "Display applied.");
            e("Noch nicht übernommen", "Not applied yet"); e("Im Editor ohne Wirkung", "No effect in the editor"); e("Unbegrenzt", "Unlimited"); e("Bildrate begrenzen", "Frame rate limit");
            e("Hinweis: Die Begrenzung wirkt nur bei ausgeschaltetem VSync.", "Note: the limit only applies with VSync off."); e("Bildqualität", "Image quality"); e("Schatten", "Shadows");
            e("Kantenglättung", "Anti-aliasing"); e("Sichtweite", "View distance"); e("Render-Skalierung", "Render scale"); e("Partikel", "Particles"); e("Helligkeit", "Brightness");
            e("Sichtfeld", "Field of view"); e("Leistungsanzeige", "Performance overlay");
            e("Oben links: Bildrate (aktuell, Minimum und Mittel der letzten 5 Sekunden), Bildzeit, Draw-Calls, Qualitätsstufe und Auflösung.",
              "Top left: frame rate (current, minimum and average of the last 5 seconds), frame time, draw calls, quality level and resolution.");
            e("Detail-Shader (experimentell, wirkt nach Neustart)", "Detail shaders (experimental, takes effect after restart)");
            e("Gesamtlautstärke", "Master volume"); e("Musik", "Music"); e("Effekte", "Effects"); e("Umgebung", "Ambience"); e("Oberfläche", "Interface");
            e("Stimmen & Roboterlaute", "Voices & robot sounds"); e("Stumm, wenn das Spiel nicht im Vordergrund ist", "Mute when the game is in the background");
            e("Erzähler im Spiel", "In-game narrator");
            e("Der Erzähler spricht zu besonderen Momenten einen kurzen Satz – jeder nur einmal pro Spielstand. Ohne Aufnahme erscheint der Satz als Untertitel.",
              "At special moments the narrator speaks a short sentence – each only once per save. Without a recording the sentence appears as a subtitle.");
            e("Mausempfindlichkeit", "Mouse sensitivity"); e("Controller-Empfindlichkeit", "Controller sensitivity"); e("Y-Achse umkehren", "Invert Y axis");
            e("Werkzeuge halten statt umschalten", "Hold tools instead of toggling"); e("Werkzeug wirkt, solange die Taste gehalten wird.", "The tool works while the button is held.");
            e("Einmal drücken startet das Werkzeug, erneut drücken stoppt es (schont die Hände).", "Press once to start the tool, press again to stop it (easier on the hands).");
            e("Tasten belegen", "Key bindings"); e(" (Tastatur & Maus)", " (keyboard & mouse)"); e("Neue Taste für „", "Press a new key for “"); e("“ drücken … (Esc bricht ab)", "” … (Esc cancels)");
            e("Standard wiederherstellen", "Restore defaults"); e("Standardbelegung wiederhergestellt.", "Default bindings restored.");
            e("„Bauwerk drehen“ darf dieselbe Taste wie eine andere Aktion nutzen – es wirkt nur in der Bauansicht. Menüs lassen sich immer mit Pfeiltasten, Eingabe und Esc bedienen; im Spielmenü wechseln Q/E die Reiter.",
              "“Rotate building” may share a key with another action – it only works in build view. Menus can always be used with the arrow keys, Enter and Esc; in the game menu Q/E switch tabs.");
            e("Controller (feste Belegung)", "Controller (fixed layout)");
            e("Controller werden über Unitys klassischen Input Manager (XInput) gelesen. Tastatur/Maus und Controller können jederzeit gewechselt werden – Hinweise im Spiel passen sich an.",
              "Controllers are read via Unity's classic Input Manager (XInput). You can switch between keyboard/mouse and controller at any time – the in-game hints adapt.");
            e("Hinweise", "Hints"); e("Keine Tasten- und Tipp-Hinweise; nur Warnungen und wichtige Meldungen.", "No key or tip hints; only warnings and important messages.");
            e("Ruhiges HUD: Tastensymbol und ein Wort direkt am Objekt, kurze Meldungen. Das Ziel blendet sich aus – mit [", "Calm HUD: key symbol and one word right at the object, short messages. The objective fades out – with [");
            e("] oder bei einem neuen Ziel kommt es zurück.", "] or with a new objective it comes back."); e("Alle Hinweise als ausführlicher Text (Tastenhilfe, Ziel dauerhaft, vollständige Meldungen).", "All hints as detailed text (key help, permanent objective, full messages).");
            e("Untertitel", "Subtitles"); e("Textgröße", "Text size"); e("Kamerawackeln", "Camera shake"); e("Hoher Kontrast", "High contrast"); e("Blitze und Lichtblitze reduzieren", "Reduce flashes and lightning");
            e("Materialien werden immer mit Form-Symbol und Farbe gezeigt (z. B. ◯ Glas, △ Kunststoff, ⬡ Metall), damit sie auch ohne Farbsehen unterscheidbar sind. Hoher Kontrast nutzt schwarze Flächen, weiße Schrift und gelbe Fokusrahmen.",
              "Materials are always shown with a shape symbol and a colour (e.g. ◯ glass, △ plastic, ⬡ metal), so they can be told apart without colour vision. High contrast uses black panels, white text and yellow focus frames.");
            e("Sprache", "Language"); e(" / Language", " / Sprache"); e("Spielername", "Player name"); e("Der Name ist für Mitspieler sichtbar. Er gilt ab der nächsten Sitzung.", "The name is visible to other players. It applies from the next session.");
            e("Koop-Port (TCP)", "Co-op port (TCP)"); e("Aktiv: ", "Active: "); e("Bitte 1024–65535 eingeben", "Please enter 1024–65535");
            e("Für Spiele über das Internet muss dieser TCP-Port am Router des Hosts weitergeleitet werden (oder ein VPN wie Tailscale/ZeroTier genutzt werden).",
              "For games over the internet this TCP port must be forwarded on the host's router (or use a VPN such as Tailscale/ZeroTier).");
            e("Ordner", "Folders"); e("Fotos", "Photos"); e("Pfad kopieren", "Copy path"); e("Pfad kopiert.", "Path copied.");
            e("Minimal", "Minimal"); e("Ausführlich", "Detailed"); e("Niedrig", "Low"); e("Mittel", "Medium"); e("Hoch", "High"); e("Ultra", "Ultra");
            e("Vollbild (randlos)", "Fullscreen (borderless)"); e("Exklusives Vollbild", "Exclusive fullscreen"); e("Fenster", "Windowed"); e("Wenig", "Few"); e("Viel", "Many");
            e("Linker Stick", "Left stick"); e("Bewegen / im Menü: Auswahl", "Move / in menus: select"); e("Rechter Stick", "Right stick"); e("Kamera", "Camera");
            e("Interagieren · Auftauchen · im Menü: Bestätigen", "Interact · surface · in menus: confirm"); e("Abtauchen · im Menü: Zurück", "Dive · in menus: back");
            e("Ein-/Aussteigen · Fotomodus: Panel ein/aus", "Enter/exit vehicle · photo mode: panel on/off"); e("Werkzeug wechseln · im Menü: Reiter wechseln", "Switch tool · in menus: switch tab");
            e("L3 (Stick drücken)", "L3 (press stick)"); e("R3 (Stick drücken)", "R3 (press stick)"); e("Steuerkreuz ↑", "D-pad ↑"); e("Steuerkreuz →", "D-pad →"); e("Steuerkreuz ↓", "D-pad ↓"); e("Steuerkreuz ←", "D-pad ←");
            e("A platzieren · Y drehen · X umsetzen · Back abreißen · LB/RB Bauwerk · LT/RT Kategorie · B abbrechen", "A place · Y rotate · X move · Back demolish · LB/RB building · LT/RT category · B cancel");
            e("Linke Maustaste", "Left Mouse"); e("Rechte Maustaste", "Right Mouse"); e("Mittlere Maustaste", "Middle Mouse"); e("Umschalt", "Shift"); e("Strg", "Ctrl"); e("Leertaste", "Space"); e("Eingabe", "Enter");
            // Aktionen (Tastenbelegung)
            e("Vorwärts", "Forward"); e("Rückwärts", "Backward"); e("Links", "Left"); e("Rechts", "Right"); e("Sprinten", "Sprint"); e("Interagieren", "Interact");
            e("Werkzeug benutzen", "Use tool"); e("Magnet aufladen / Zweitfunktion", "Charge magnet / secondary"); e("Nächstes Werkzeug", "Next tool"); e("Vorheriges Werkzeug", "Previous tool");
            e("Pressen", "Compact"); e("Spielmenü", "Game menu"); e("Karte", "Map"); e("Bauansicht", "Build view"); e("Fotomodus", "Photo mode"); e("Auftauchen", "Surface"); e("Abtauchen", "Dive");
            e("Ein-/Aussteigen", "Enter/exit vehicle"); e("Fahrzeug zurücksetzen", "Reset vehicle"); e("Roboterlaut", "Robot sound"); e("Schlafen", "Sleep");
            e("Notunterschlupf bauen", "Build emergency shelter"); e("Aufträge", "Missions"); e("Inventar", "Inventory"); e("Schnellspeichern", "Quick save");
            e("Bauwerk drehen", "Rotate building"); e("Radio an/aus", "Radio on/off");
            // ------------------------------------------------ Spielmenü
            e("Archiv", "Archive"); e("Roboter", "Robot"); e("Radio", "Radio"); e("Werkstatt", "Workshop"); e("Lager", "Storage");
            e("Farbe", "Colour"); e("Akzent", "Accent"); e("Aufkleber", "Sticker"); e("Anbauteil", "Attachment");
            e("Verkaufsterminal", "Sales terminal"); e("Sortiertisch", "Sorting table"); e("Materialhändler", "Material trader"); e("Entsorgungsstation", "Disposal station");
            e("Auftragstafel", "Mission board"); e("Garage", "Garage"); e("Transportschiff", "Transport ship");
            e("Der Behälter ist leer. Müll mit ", "The container is empty. Press "); e(" aufsammeln.", " to collect trash.");
            e("  (gepresst)", "  (compacted)"); e("  ⚠ Gefahrstoff", "  ⚠ Hazardous"); e("Direktverkauf (unsortiert): ca. <b>", "Direct sale (unsorted): approx. <b>");
            e("</b> Credits · Sortiert und als Ballen bringt es mehr.", "</b> credits · Sorted and baled it earns more.");
            e("Notbetrieb: langsam, keine Werkzeuge. Am Stützpunkt lädt der Akku.", "Emergency mode: slow, no tools. The battery charges at the base.");
            e("Werkzeuge", "Tools"); e(" – Werkstatt", " – workshop"); e("Behälter pressen ", "Compact container "); e("Gepresst – mehr Platz im Behälter.", "Compacted – more room in the container.");
            e("Die Müllpresse gibt es in der Werkstatt (halbiert das Volumen von Papier, Kunststoff, Metall, Stahl, Kupfer und Netzen).",
              "The trash compactor is available in the workshop (halves the volume of paper, plastic, metal, steel, copper and nets).");
            e("Wohin damit?", "Where does it go?");
            e("• <b>Verkaufsterminal:</b> Behälter direkt verkaufen (unsortiert).\n• <b>Lager/Abladeplatz:</b> einlagern, dann sortieren (Sortiertisch oder Sortieranlage) und teurer verkaufen.\n• <b>Entsorgungsstation:</b> Gefahrstoffe fachgerecht abgeben – der Umweltfonds zahlt.\n• Alles im Reiter „",
              "• <b>Sales terminal:</b> sell the container directly (unsorted).\n• <b>Storage/drop-off:</b> store, then sort (sorting table or sorting plant) and sell for more.\n• <b>Disposal station:</b> hand in hazardous waste properly – the environmental fund pays.\n• Everything in the “");
            e("“ – direkt an der jeweiligen Station.", "” tab – right at the respective station.");
            e("Aktive Aufträge", "Active missions"); e("Erledigt", "Completed"); e("Wiederherstellung ", "Restoration "); e("Stufe ", "Level "); e("Sauberkeit ", "Cleanliness ");
            e(" % (Ziel ", " % (target "); e(" %) · Ökologie ", " %) · Ecology "); e("Großprojekte", "Grand projects"); e(" · noch nicht erreichbar", " · not reachable yet");
            e("Einstieg", "Introduction"); e("Nebenauftrag", "Side mission"); e("Belohnung: ", "Reward: "); e("Kosmetik „", "Cosmetic “"); e("✓ abgeschlossen", "✓ completed");
            e("im Bau ", "under construction "); e("Bereit – am Projektplatz starten.", "Ready – start at the project site."); e("Upgrades", "Upgrades"); e("Fahrzeuge", "Vehicles");
            e("Transportschiff & ", "Transport ship & "); e("Du bist unterwegs – kaufen geht nur am Stützpunkt (Werkstatt/Garage). Die Angebote kannst du hier ansehen.",
              "You are out in the field – buying only works at the base (workshop/garage). You can browse the offers here.");
            e("Voll ausgebaut", "Fully upgraded"); e("Erst verfügbar, wenn ", "Only available once "); e(" erreichbar ist", " is reachable"); e("Nur der Host (ab ", "Host only (from ");
            e(" Cr, Vertrauensmodus aus)", " cr, trust mode off)"); e("Es fehlen ", "Missing "); e("Nur am Stützpunkt", "Only at the base"); e("   Stufe ", "   Level ");
            e("Maximal", "Maximum"); e("Kaufen", "Buy"); e(" verbessert.", " upgraded."); e("Fahrzeuge stehen nach dem Kauf in der Garage des Stützpunkts. Einsteigen mit ", "After purchase, vehicles wait in the base garage. Get in with ");
            e("Bereits in der Garage", "Already in the garage"); e("Erst auf ", "Only on "); e(" nutzbar", " usable"); e("Nur der Host (Vertrauensmodus aus)", "Host only (trust mode off)");
            e("   ✓ vorhanden", "   ✓ owned"); e("   nur ", "   only "); e("Tempo ", "Speed "); e(" · Ladung ", " · Load "); e(" · fährt auf dem Wasser", " · drives on water");
            e("In der Garage", "In the garage"); e(" gekauft.", " bought."); e("Ausbau: <b>", "Upgrade: <b>"); e("Nur am Stützpunkt (Landeplatz)", "Only at the base (landing pad)");
            e("Nächste Stufe: ", "Next level: "); e("Einbauen", "Install"); e("Sprungantrieb eingebaut.", "Jump drive installed.");
            e("Voll ausgebaut – alle Welten sind erreichbar, sobald sie freigeschaltet sind.", "Fully upgraded – all worlds are reachable as soon as they are unlocked.");
            e(" – Sternenkarte", " – star map"); e("● Du bist hier", "● You are here"); e("erreichbar", "reachable"); e("gesperrt", "locked"); e("Freischalten: ", "Unlock: ");
            e("Du bist bereits hier.", "You are already here."); e("Nur der Host kann das Transportschiff starten.", "Only the host can launch the transport ship.");
            e("Zum Transportschiff am Landeplatz fahren.", "Drive to the transport ship on the landing pad."); e(" nach ", " to ");
            e("Beim Reisen kommen alle Mitspieler mit. Lager, Gebäude und Fortschritt jedes Planeten bleiben erhalten.", "All players come along when travelling. Storage, buildings and progress of every planet are kept.");
            e("Lager: <b>", "Storage: <b>"); e("</b> Einheiten", "</b> units"); e("  (Ballen zählen 5)", "  (bales count 5)"); e("Energie: +", "Energy: +"); e(" → Anlagen ", " → machines ");
            e("Du stehst am <b>", "You are at the <b>");
            e("Der Server prüft die Station: Verkaufen am Verkaufsterminal, Kaufen beim Materialhändler, Einlagern am Lager/Abladeplatz, Entsorgen an der Entsorgungsstation, Recyclingaufträge an der Auftragstafel.",
              "The server checks the station: sell at the sales terminal, buy from the material trader, store at the storage/drop-off, dispose at the disposal station, recycling orders at the mission board.");
            e("Behälter (", "Container ("); e(" Teile, ", " items, "); e("Behälter verkaufen (~", "Sell container (~"); e(" Cr)", " cr)"); e(" (alles)", " (all)"); e("Eingelagert.", "Stored.");
            e("Gefahrstoffe ", "Hazardous waste "); e("Entsorgen", "Dispose"); e("Material im Lager", "Material in storage"); e("Material", "Material"); e("Unsort.", "Unsort.");
            e("Sortiert", "Sorted"); e("Ballen", "Bales"); e("Verkaufen", "Sell"); e(" (alles je Stufe)", " (all per grade)"); e(" – am Verkaufsterminal", " – at the sales terminal");
            e("Händler", "Trader"); e(" – beim Materialhändler", " – at the material trader"); e("→ nur Entsorgung (+", "→ disposal only (+"); e(" Cr/Einheit)", " cr/unit)");
            e("U = unsortiert (50 % Wert), S = sortiert (100 %), B = Ballen aus 10 sortierten Einheiten (130 %). Sortieren: am Sortiertisch ",
              "U = unsorted (50 % value), S = sorted (100 %), B = bales of 10 sorted units (130 %). Sorting: at the sorting table ");
            e(" halten oder mit einer Sortieranlage (Bauansicht).", " hold, or with a sorting plant (build view)."); e("Recyclingauftrag & Lieferungen", "Recycling order & deliveries");
            e("Liefere <b>", "Deliver <b>"); e("</b> sortierte Einheiten (vorhanden: ", "</b> sorted units (available: "); e("Auftrag erfüllen", "Fulfil order"); e("Recyclingauftrag erfüllt.", "Recycling order fulfilled.");
            e("Schrottlieferung: 14 Teile Müll landen am Abladeplatz – ideal zum Sortieren und für Aufträge.", "Scrap delivery: 14 pieces of trash land at the drop-off – ideal for sorting and orders.");
            e("Lieferung liegt noch bereit", "Delivery still waiting"); e("Lieferung bestellen", "Order delivery"); e("Lieferung ist unterwegs zum Abladeplatz.", "The delivery is on its way to the drop-off.");
            e(" in Reichweite", " in range"); e("→ am ", "→ at the "); e("Fundstücke: <b>", "Finds: <b>"); e("??? (noch nicht gefunden)", "??? (not found yet)");
            e("Wähle links ein Fundstück zum Lesen.\n\nFundstücke liegen verstreut auf den Planeten (Buch-Symbol auf der Karte). Mit ", "Choose a find on the left to read it.\n\nFinds are scattered across the planets (book symbol on the map). Press ");
            e(" aufheben.", " to pick them up."); e("Aussehen von MIKO", "MIKO's look"); e("Kosmetik ist rein optisch. Freigeschaltetes bleibt in deinem Profil – auch in anderen Welten und im Koop.",
              "Cosmetics are purely visual. Unlocks stay in your profile – in other worlds and in co-op too.");
            e("Neues Aussehen übernommen.", "New look applied."); e("Zurücksetzen", "Reset"); e("Statistik – ", "Statistics – "); e("Spielzeit", "Play time"); e("Wiederherstellung gesamt", "Total restoration");
            e("Credits verdient", "Credits earned"); e("Objekte gesammelt", "Objects collected"); e("Verkäufe", "Sales"); e("Einheiten sortiert", "Units sorted"); e("Ballen verkauft", "Bales sold");
            e("Gefahrstoffe entsorgt", "Hazardous waste disposed"); e("Objekte zerlegt", "Objects dismantled"); e("Aufgetaut", "Thawed"); e("Ölteppiche gereinigt", "Oil slicks cleaned");
            e("Upgrades gekauft", "Upgrades bought"); e("Unterschlupf genutzt", "Shelter used"); e("Notabschaltungen", "Emergency shutdowns"); e("Fundstücke", "Finds");
            e("Planeten erreichbar", "Planets reachable"); e("Kampagne", "Campaign"); e("abgeschlossen ✓", "completed ✓");
            // ------------------------------------------------ HUD
            e("Leer", "Empty"); e("⚠ Notbetrieb – zum Stützpunkt", "⚠ Emergency mode – to the base"); e("Behälter voll", "Container full"); e("] pressen", "] compact");
            e("Tauchen  [", "Diving  ["); e("] abtauchen", "] dive"); e("Ziel", "Objective"); e("Nacht", "Night"); e("  Unterschlupf ", "  Shelter "); e("Notunterschlupf ", "Emergency shelter ");
            e("Energie", "Energy"); e("Ladung", "Load"); e("⚠ NOTBETRIEB – langsam, Werkzeuge aus. Zum Stützpunkt laden.", "⚠ EMERGENCY MODE – slow, tools off. Charge at the base.");
            e("Behälter voll – einlagern, verkaufen oder pressen ", "Container full – store, sell or compact "); e("Tauchen", "Diving"); e(" auf · ", " up · "); e(" ab", " down");
            e("Schwimmen", "Swimming"); e(" abtauchen", " dive"); e(" Menü  ", " Menu  "); e(" Karte  ", " Map  "); e(" Bauen", " Build"); e("✓ Gespeichert", "✓ Saved");
            e("Morgen", "Morning"); e("Abend", "Evening"); e("Tag", "Day"); e("Wind ", "Wind "); e(" zieht auf", " is coming"); e(" (abgeschleppt)", " (being towed)"); e(" (ungeschützt)", " (exposed)");
            e("! Schnell in einen Unterschlupf", "! Get into a shelter quickly"); e("Nacht: Suche einen ", "Night: look for a "); e("Nächster Unterschlupf: <b>", "Nearest shelter: <b>");
            e(" Notunterschlupf bauen (", " Build emergency shelter ("); e(" Credits)  ·  ", " credits)  ·  "); e(" dort schlafen", " sleep there"); e(" aussteigen", " get out");
            e("Ladung ", "Load "); e("Wrack am Haken – zum Rover oder Stützpunkt bringen", "Wreck on the hook – bring it to the rover or the base"); e("Wrack am Haken", "Wreck on the hook");
            e("Kran bereit – an ein Wrack heranfahren", "Crane ready – drive up to a wreck"); e("Kran bereit", "Crane ready"); e("Festgefahren?  ", "Stuck?  "); e(" Fahrzeug zurücksetzen", " reset vehicle");
            e("Festgefahren?  [", "Stuck?  ["); e("Eine Abschleppdrohne bringt MIKO zum Stützpunkt …", "A tow drone is bringing MIKO to the base …"); e("Ankunft in etwa ", "Arrival in about ");
            e(" s. Die Ladung bleibt erhalten.", " s. The load is kept."); e("Warte auf Mitspieler (", "Waiting for other players ("); e(" schlafen) …", " asleep) …");
            e("MIKO lädt und schläft bis zum Morgen …", "MIKO is charging and sleeping until morning …"); e(" aufwachen", " wake up");
            e("Sauger", "Vacuum"); e("Magnet", "Magnet"); e("Schneider", "Cutter"); e("Wärme", "Heat"); e("Filter", "Filter"); e("Bio", "Bio"); e("Magnetwelle", "Magnet wave"); e("Leer", "Empty");
            // ------------------------------------------------ Karte
            e("Karte wird erstellt …", "Creating map …"); e("Versperrt: ", "Blocked: "); e("Stützpunkt (Lager, Verkauf, Werkstatt, Schiff)", "Base (storage, sales, workshop, ship)");
            e("Transportschiff (Reisen)", "Transport ship (travel)"); e("fertig", "done"); e("offen", "open"); e("Projektplatz: ", "Project site: "); e("Lichtpunkt „", "Light point “");
            e(" – sauber", " – clean"); e(" – verschmutzt", " – dirty"); e(" – repariert", " – repaired"); e(" – defekt", " – broken"); e(" – gewachsen", " – grown"); e(" – wächst ", " – growing ");
            e(" – noch leer", " – still empty"); e("Fundstück (gefunden): ", "Find (found): "); e("Fundstück – noch nicht entdeckt", "Find – not discovered yet"); e(" – gemerkt (Fotomodus)", " – saved (photo mode)");
            e("Notunterschlupf (selbst gebaut)", "Emergency shelter (self-built)"); e(" (besetzt)", " (occupied)"); e("Du (", "You ("); e("Wiederherstellung: ", "Restoration: "); e("Legende", "Legend");
            e("Du (Blickrichtung) · Mitspieler in ihrer Farbe", "You (view direction) · other players in their colour"); e("Bereichsgrenze", "Area boundary"); e("Höhenlinien · Stützpunktfläche", "Contour lines · base area");
            e("Zoom ", "Zoom "); e("2D-Karte zeigen", "Show 2D map"); e("3D-Karte zeigen", "Show 3D map"); e("LB/RB: Zoom", "LB/RB: zoom"); e("Mausrad oder +/−: Zoom", "Mouse wheel or +/−: zoom");
            e(" schließen", " close"); e("Rechter Stick: drehen/neigen  ·  LB/RB: Zoom  ·  Linker Stick: verschieben  ·  R3: zu MIKO  ·  B/Back: schließen", "Right stick: rotate/tilt  ·  LB/RB: zoom  ·  Left stick: pan  ·  R3: to MIKO  ·  B/Back: close");
            e("Q/E oder Maus ziehen: drehen  ·  Mausrad, +/−: Zoom  ·  ", "Q/E or drag mouse: rotate  ·  mouse wheel, +/−: zoom  ·  "); e(": verschieben  ·  C: zu MIKO  ·  N: Norden  ·  ", ": pan  ·  C: to MIKO  ·  N: north  ·  ");
            e("/Esc: schließen", "/Esc: close"); e("Karte verschoben – ", "Map moved – "); e(": zurück zu MIKO", ": back to MIKO");
            e("Stützpunkt", "Base"); e("Projektplatz", "Project site"); e("Lichtpunkt (verschmutzt)", "Light point (dirty)"); e("Lichtpunkt (sauber)", "Light point (clean)"); e("Notunterschlupf", "Emergency shelter");
            e("Reparatur (grün = erledigt)", "Repair (green = done)"); e("Öko-Platz (grün = gewachsen)", "Eco spot (green = grown)"); e("Aussichtspunkt", "Viewpoint"); e("Versperrter Durchgang", "Blocked passage");
            e("Fahrzeug", "Vehicle"); e("Fundstück", "Find");
            // ------------------------------------------------ Koop
            e(" – gemeinsam aufräumen (1–4 Spieler)", " – clean up together (1–4 players)"); e("Sitzung beitreten", "Join session"); e("Adresse oder Einladung (z. B. 192.168.0.10:7777/ABC123)", "Address or invite (e.g. 192.168.0.10:7777/ABC123)");
            e("Sitzungscode", "Session code"); e(" (optional, falls nicht in der Einladung)", " (optional, if not in the invite)"); e("Verbinden", "Connect"); e("So klappt die Verbindung", "How to connect");
            e("• <b>Im selben Netzwerk (LAN/WLAN):</b> Die Einladung des Hosts einfach einfügen.\n", "• <b>On the same network (LAN/Wi-Fi):</b> simply paste the host's invite.\n");
            e("• <b>Über das Internet:</b> Der Host leitet am Router den TCP-Port (Standard 7777) an seinen PC weiter und teilt seine öffentliche IP.\n", "• <b>Over the internet:</b> the host forwards the TCP port (default 7777) on the router to their PC and shares their public IP.\n");
            e("• <b>Ohne Portweiterleitung:</b> Ein VPN wie Tailscale oder ZeroTier – dann die VPN-Adresse des Hosts nutzen.\n", "• <b>Without port forwarding:</b> a VPN such as Tailscale or ZeroTier – then use the host's VPN address.\n");
            e("• <b>Dedizierter Server:</b> Adresse und Code des Servers eingeben; die Welt läuft dort weiter, auch wenn der Host offline ist.", "• <b>Dedicated server:</b> enter the server's address and code; the world keeps running there even when the host is offline.");
            e("Sitzung erstellen", "Host session"); e("Lade einen Spielstand – danach wird die Welt automatisch für Mitspieler geöffnet (Port ", "Load a save – the world is then opened automatically for other players (port ");
            e(", änderbar unter Einstellungen → Sonstiges). Die Einladung erscheint anschließend hier.", ", changeable under Settings → Other). The invite then appears here.");
            e("Laden & hosten", "Load & host"); e("Noch kein Spielstand vorhanden – starte zuerst ein neues Spiel.", "No save yet – start a new game first.");
            e("Deine Welt ist privat. Öffne sie, damit bis zu ", "Your world is private. Open it so that up to "); e(" Freunde beitreten können. Die Simulation läuft bei dir – du speicherst für alle.", " friends can join. The simulation runs on your machine – you save for everyone.");
            e("Welt für Mitspieler öffnen", "Open world for other players"); e("TCP-Port ", "TCP port "); e(" (Einstellungen → Sonstiges)", " (Settings → Other)"); e("● Koop ist offen", "● Co-op is open");
            e("  –  Port ", "  –  port "); e(" · Code ", " · code "); e("Einladungen (je nach Netzwerk die passende Adresse teilen):", "Invites (share the address that fits the network):"); e("Einladung kopiert: ", "Invite copied: ");
            e("Gleiches Netzwerk: lokale Adresse (192.168… / 10…). Internet: öffentliche IP des Routers mit weitergeleitetem TCP-Port ", "Same network: local address (192.168… / 10…). Internet: public IP of the router with forwarded TCP port ");
            e("“. Mit Tailscale/ZeroTier: die VPN-Adresse (100… bzw. je nach Netz).", "”. With Tailscale/ZeroTier: the VPN address (100… or depending on the network).");
            e("Alle Mitspieler werden getrennt. Sicher?", "All other players will be disconnected. Are you sure?"); e("Ja, schließen", "Yes, close"); e("Koop schließen", "Close co-op");
            e("Vertrauensmodus: Gäste dürfen teure Käufe (ab ", "Trust mode: guests may make expensive purchases (from "); e(" Credits), Abriss und Reisen auslösen", " credits), demolish and travel");
            e("● Verbunden", "● Connected"); e(" mit der Welt „", " to the world “"); e("“ von ", "” of "); e(" (dedizierter Server)", " (dedicated server)"); e("Ping: ", "Ping: "); e(" ms", " ms");
            e("   ·   Code ", "   ·   code "); e("Die Welt gehört dem Host: Er speichert für alle. ", "The world belongs to the host: they save for everyone. ");
            e("Vertrauensmodus ist an – du darfst auch teure Käufe tätigen.", "Trust mode is on – you may make expensive purchases too."); e("Teure Käufe (ab ", "Expensive purchases (from ");
            e(" Credits), Abriss und Reisen sind dem Host vorbehalten.", " credits), demolition and travel are reserved for the host."); e("Sitzung wirklich verlassen?", "Really leave the session?");
            e("Ja, verlassen", "Yes, leave"); e("Sitzung verlassen", "Leave session"); e("Spieler", "Players"); e("  (Host)", "  (host)"); e("  (du)", "  (you)"); e("offline", "offline");
            e("wird abgeschleppt", "being towed"); e("schläft", "asleep"); e("fährt ", "drives "); e(" (schläft)", " (asleep)");
            e("Koop geöffnet – Einladung teilen!", "Co-op opened – share the invite!"); e("Koop konnte nicht geöffnet werden: ", "Co-op could not be opened: ");
            // ------------------------------------------------ Bauansicht
            e("Zum Bauen erst aussteigen.", "Get out of the vehicle to build."); e("Bauen geht nur am Stützpunkt.", "Building only works at the base."); e("Bauansicht nur am Stützpunkt.", "Build view only at the base.");
            e("Energie: ", "Energy: "); e("  → Anlagen ", "  → machines "); e("Lager: ", "Storage: "); e(" Einheiten · ", " units · ");
            e("Abreißen? Back erneut drücken (B: abbrechen)", "Demolish? Press Back again (B: cancel)"); e("Umsetzen: neuen Platz wählen", "Move: choose a new spot");
            e("Bauwerk mit LB/RB wählen (LT/RT: Kategorie)", "Choose a building with LB/RB (LT/RT: category)"); e("Bauwerk unten auswählen", "Choose a building below");
            e("Mit dem linken Stick auf die Baufläche zielen", "Aim at the build area with the left stick"); e("Mauszeiger auf die Baufläche richten", "Point the mouse at the build area");
            e("✓ Hier platzierbar", "✓ Can be placed here"); e("Nicht platzierbar", "Cannot be placed"); e("platzieren", "place"); e("drehen", "rotate"); e("L-Stick", "L-stick"); e("zielen", "aim");
            e("Bauwerk", "Building"); e("Kategorie", "Category"); e("umsetzen", "move"); e("abreißen", "demolish"); e("abbrechen", "cancel"); e("Linksklick", "Left click");
            e("Rechtsklick: Auswahl aufheben · Mausrad/Bild↑↓: Bauwerk wechseln · 1–6: Kategorie", "Right click: deselect · mouse wheel/PgUp/PgDn: switch building · 1–6: category");
            e("/Esc: ", "/Esc: "); e("Bauansicht verlassen", "Leave build view"); e("Bauansicht\nverlassen", "Leave\nbuild view"); e("Seite {0}/{1}", "Page {0}/{1}");
            e("Nach „", "After “"); e("Gesperrt: ", "Locked: "); e("Energie +", "Energy +"); e("Energie −", "Energy −"); e("keine Energie", "no energy"); e(" gebaut", " built");
            e(" pro Stützpunkt.", " per base."); e("Alle", "All"); e("Cursor auf ein Gebäude richten: X umsetzen, Back abreißen.", "Point the cursor at a building: X move, Back demolish.");
            e("Gebäude mit der Maus berühren, um es umzusetzen oder abzureißen.", "Hover over a building with the mouse to move or demolish it.");
            e("Cursor auf ein Gebäude richten, um es umzusetzen.", "Point the cursor at a building to move it."); e("Cursor auf ein Gebäude richten, um es abzureißen.", "Point the cursor at a building to demolish it.");
            e("Abriss ist dem Host vorbehalten (Vertrauensmodus aus).", "Demolition is reserved for the host (trust mode off).");
            e("verbunden", "connected"); e("nicht verbunden – Förderband zum Lager legen", "not connected – lay a conveyor to the storage"); e("Umsetzen abbrechen", "Cancel move"); e("Umsetzen", "Move");
            e("Ja, abreißen", "Yes, demolish"); e("Abreißen (50 % zurück)", "Demolish (50 % back)"); e("Abreißen: nur Host", "Demolish: host only"); e("Maximal ", "At most "); e("× pro Stützpunkt.", "× per base.");
            e("Es fehlen: ", "Missing: ");
            // ------------------------------------------------ Fotomodus
            e("Leertaste/F12", "Space/F12"); e("[H] Panel  ·  [", "[H] panel  ·  ["); e("] Foto  ·  ", "] photo  ·  "); e("/Esc Beenden", "/Esc exit"); e("[Y] Panel  ·  [A] Foto  ·  [B] Beenden", "[Y] panel  ·  [A] photo  ·  [B] exit");
            e("[Y] Panel ausblenden – dann frei umsehen", "[Y] hide panel – then look around freely"); e("[H] Panel ausblenden – dann mit der Maus umsehen", "[H] hide panel – then look around with the mouse");
            e("● Foto aufnehmen", "● Take photo"); e("HUD ausblenden", "Hide HUD"); e("Neigung", "Tilt"); e("Belichtung", "Exposure"); e("Hochformat 9:16", "Portrait 9:16");
            e("Vorher-Ansicht (Ausgangszustand)", "Before view (initial state)"); e("Werte zurücksetzen", "Reset values"); e("Zuletzt gespeichert:", "Last saved:");
            e("Aussichtspunkte", "Viewpoints"); e(" (noch nicht besucht)", " (not visited yet)"); e("Aussichtspunkte im Gelände besuchen und mit ", "Visit viewpoints in the terrain and save them with ");
            e(" merken – dann hierher springen.", " – then jump here."); e("Linker Stick: bewegen · Rechter Stick: umsehen (Panel aus) · L3: schneller · A: Foto · B: beenden", "Left stick: move · right stick: look around (panel off) · L3: faster · A: photo · B: exit");
            e("WASD: bewegen · Maus: umsehen (Panel aus) · Q/E: runter/hoch · Umschalt: schneller · Leertaste/F12: Foto · ", "WASD: move · mouse: look around (panel off) · Q/E: down/up · Shift: faster · Space/F12: photo · ");
            e("/Esc: beenden", "/Esc: exit"); e("Fotos landen in: ", "Photos are saved to: "); e("Fotomodus beenden", "Exit photo mode");
            // ------------------------------------------------ Interaktionen (PlayerController)
            e(" ist noch nicht vorhanden – in der Werkstatt kaufen.", " is not available yet – buy it in the workshop."); e("Wärmemodul nötig.", "Heat module needed.");
            e("Nicht eingefroren.", "Not frozen."); e("Kein Ölteppich.", "No oil slick."); e("] Aufheben: ", "] Pick up: "); e("Aufheben", "Pick up");
            e(" halten] Magnetwelle aufladen, loslassen zum Auslösen", " hold] charge magnet wave, release to trigger"); e("Zerlegen", "Cut up"); e("Auftauen", "Thaw"); e("Filtern", "Filter");
            e(" halten] ", " hold] "); e(" (15 Credits)", " (15 credits)"); e("Pflanzen", "Plant"); e("Bio-Modul: Pflanzstelle suchen (Karte)", "Bio module: find a planting spot (map)");
            e("Pflanzstelle suchen", "Find planting spot"); e("] Abladen", "] Unload"); e("Abladen", "Unload"); e("Im Unterschlupf – Ansaugen erst draußen (", "In the shelter – suction only outside (");
            e("Im Unterschlupf", "In the shelter"); e(" halten] Ansaugen (", " hold] suck up ("); e("Ansaugen ", "Suction "); e("] Im Hafen abladen", "] Unload in the harbour");
            e("Über Treibgut fahren (", "Drive over flotsam ("); e("Treibgut ", "Flotsam "); e("Auf den Transportrover setzen", "Put it on the transport rover"); e("Am Stützpunkt verwerten", "Recycle at the base");
            e("Absetzen", "Set down"); e("Auf Rover", "On rover"); e("Verwerten", "Recycle"); e("An ein großes Wrack heranfahren", "Drive up to a large wreck"); e("Wrack suchen", "Find a wreck");
            e(" halten] Anheben: ", " hold] Lift: "); e(" (Mitspieler können helfen)", " (other players can help)"); e("Anheben", "Lift"); e(" Helfer)", " helpers)");
            e("Einlagern (", "Store ("); e("Einlagern", "Store"); e("Lager: Behälter leer", "Storage: container empty"); e("Behälter leer", "Container empty"); e(" halten] Sortieren", " hold] Sort");
            e("Sortieren", "Sort"); e("Gefahrstoffe entsorgen", "Dispose of hazardous waste"); e("Aufträge & Lieferungen", "Missions & deliveries"); e("Reisen (Transportschiff)", "Travel (transport ship)");
            e("Reisen", "Travel"); e("Lädt ", "Charging "); e("Akku voll", "Battery full"); e("] Bauansicht", "] Build view"); e("Bauen", "Build");
            e("] Auf die Ladefläche umladen · [", "] Move to the cargo bed · ["); e("] Einsteigen", "] Get in"); e("Umladen", "Transfer"); e("] Einsteigen: ", "] Get in: "); e("Einsteigen", "Get in");
            e("] Projekt starten: „", "] Start project: “"); e(" Credits + Material)", " credits + material)"); e("Projekt starten", "Start project"); e(" aus dem Lager)", " from storage)");
            e("Reparieren", "Repair"); e("] Fundstück aufheben", "] Pick up find"); e(" merken (Fotomodus)", " save (photo mode)"); e("Aussicht merken", "Save view");
            e(" halten] Beim Anheben helfen (Kran nötig)", " hold] Help lift (crane needed)"); e("Mitheben", "Help lift"); e("] Schlafen", "] Sleep"); e("Unterschlupf", "Shelter");
            e("Aussichtspunkt gemerkt – im Fotomodus [", "Viewpoint saved – in photo mode ["); e("] anspringbar.", "] you can jump to it."); e("Kein Fahrzeug in der Nähe.", "No vehicle nearby.");
            e("Zum Stützpunkt zurückkehren", "Return to base");
            e("Unterschlupf: über die Rampe ins Transportschiff fahren – geschützt vor dem Sturm", "Shelter: drive up the ramp into the transport ship – protected from the storm");
            e("Unterschlupf: über die Rampe ins Transportschiff fahren – geschützt vor der Nacht", "Shelter: drive up the ramp into the transport ship – protected from the night");
            e("Unterschlupf: durchs Rolltor in den Hangar fahren – geschützt vor dem Sturm", "Shelter: drive through the roller door into the hangar – protected from the storm");
            e("Unterschlupf: durchs Rolltor in den Hangar fahren – geschützt vor der Nacht", "Shelter: drive through the roller door into the hangar – protected from the night");
            e("Im Unterschlupf – der Kran arbeitet nur draußen", "In the shelter – the crane only works outside");
            // ------------------------------------------------ Intro, Abspann
            e("Intro wird vorbereitet …", "Preparing intro …"); e("Musik wird vorbereitet …", "Preparing music …"); e("Überspringen … {0} %", "Skipping … {0} %");
            e("Gedrückt halten zum Überspringen", "Hold to skip"); e("PROGRAMM ZWEITE CHANCE – BEDINGUNG ERFÜLLT", "PROGRAMME SECOND CHANCE – CONDITION MET");
            e("Die Arche HORIZONT kehrt zurück.", "The ark HORIZON returns."); e("Wiederhergestellt: {0} %", "Restored: {0} %"); e("Danke, MIKO.", "Thank you, MIKO.");
            e("Objekte gesammelt: {0}   ·   Credits verdient: {1}   ·   Spielzeit: {2} h", "Objects collected: {0}   ·   Credits earned: {1}   ·   Play time: {2} h");
            e("RE:PLANET – Eine zweite Chance", "RE:PLANET – A Second Chance"); e("Idee und Auftrag: das RE:PLANET-Team", "Idea and commission: the RE:PLANET team");
            e("Spielentwurf, Programmierung, Grafik und Musik: prozedural erzeugt im Projekt", "Game design, programming, graphics and music: procedurally generated in the project");
            e("Die Welten gehören jetzt dir. Freies Spiel beginnt …", "The worlds are yours now. Free play begins …");
            e("Es gab einmal eine Welt, die alles hatte.", "Once there was a world that had everything."); e("Und alles, was sie hatte, warf sie fort.", "And everything it had, it threw away.");
            e("KONSUMA versprach uns das Glück. „Alles. Sofort. Immer neu.“", "KONSUMA promised us happiness. “Everything. Now. Always new.”");
            e("Wir kauften und kauften … bis der Müll unsere Städte überragte.", "We bought and bought … until the trash towered over our cities.");
            e("Dann bauten wir Archen. „Nur für fünf Jahre“, sagten sie.", "Then we built arks. “Only for five years,” they said.");
            e("Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen.", "The machines stayed behind. On four worlds. To clean up.");
            e("Aus fünf Jahren wurden fünfzig.", "Five years became fifty."); e("Eine Maschine nach der anderen … verstummte.", "One machine after another … fell silent.");
            e("Nur eine nicht. Eine kleine, sture Maschine.", "All but one. A small, stubborn machine."); e("MIKO. Jeden Morgen. Würfel für Würfel.", "MIKO. Every morning. Cube by cube.");
            e("Bis MIKO eines Tages etwas fand, das längst verloren war.", "Until one day MIKO found something that had long been lost."); e("Einen Keimling. Klein. Grün. Lebendig.", "A seedling. Small. Green. Alive.");
            e("Ein altes Signal erwachte: PROGRAMM ZWEITE CHANCE.", "An old signal awoke: PROGRAMME SECOND CHANCE."); e("Wenn das Leben zurückkehrt … kehren auch wir zurück.", "When life returns … we will return too.");
            e("Und eines Abends leuchteten neue Lichter am Himmel.", "And one evening, new lights shone in the sky."); e("Die Arche HORIZONT kam nach Hause.", "The ark HORIZON came home.");
            e("Vier Welten. Vier zweite Chancen.", "Four worlds. Four second chances."); e("Was wir fortgeworfen hatten … hast du uns zurückgegeben.", "What we had thrown away … you gave back to us.");
            // ------------------------------------------------ Erzählerzeilen im Spiel (Untertitel)
            e("Die alte Erde. Sie hat lange auf jemanden gewartet, der bleibt.", "The old Earth. It has waited a long time for someone who stays.");
            e("Pyra glühte einst vor Arbeit. Jetzt glüht nur noch der Sand.", "Pyra once glowed with work. Now only the sand glows.");
            e("Pelagia. Ein Meer, das sich nach klarem Wasser sehnt.", "Pelagia. A sea that longs for clear water.");
            e("Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen.", "Nivalis. Beneath the ice sleep the servers that promised our return.");
            e("Das erste Stück ist heimgebracht. So fängt jede Heimkehr an.", "The first piece has been brought home. That is how every homecoming begins.");
            e("Aus dem, was wir fortwarfen, wird wieder etwas wert.", "What we threw away is worth something again.");
            e("Von fern kommt ein Frachter. Du bist nicht mehr ganz allein.", "A freighter comes from afar. You are not quite alone anymore.");
            e("Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran.", "A small square, clean again. The light remembers.");
            e("Der größte Berg ist abgetragen. Darunter liegt eine Straße, die man fast vergessen hatte.", "The biggest heap is gone. Beneath it lies a street that was almost forgotten.");
            e("Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen.", "Not a single piece left. This is how it looked before we threw everything away.");
            e("Die Lichter gehen wieder an. Leise, eines nach dem anderen.", "The lights come on again. Quietly, one after another.");
            e("Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben.", "This world breathes again. You gave it its second chance.");
            e("Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund.", "A storm is coming. Find yourself a roof, little friend.");
            e("Der Sand wandert wieder. Morgen sehen die Wege anders aus.", "The sand is moving again. Tomorrow the paths will look different.");
            e("Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können.", "The first night. Even machines need a place where they can wait.");
            e("Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet.", "A new morning. The work is patient – it has waited for you.");
            e("Manchmal geht einem die Kraft aus. Das ist keine Schande.", "Sometimes you run out of strength. There is no shame in that.");
            e("Das Eis ruft. Auf Nivalis wartet das letzte Signal.", "The ice is calling. On Nivalis the last signal is waiting.");
            e("Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter.", "You are not alone anymore. Every load is lighter when carried together.");
            e("Ein Fundstück. Jemand hat es gewusst – und trotzdem nichts getan.", "A find. Someone knew – and still did nothing.");
            e("Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet.", "Something is growing here again. All by itself, as if it had only been waiting for room.");
            // ------------------------------------------------ Radio
            e("Radio an: {0}", "Radio on: {0}"); e("Radio aus – die Planetenmusik kehrt zurück.", "Radio off – the planet music returns."); e("Radio ausschalten", "Turn radio off"); e("Radio einschalten", "Turn radio on");
            e("Aus. Im Spiel läuft die Planetenmusik, die mit der Wiederherstellung wächst.", "Off. In the game the planet music plays, growing with the restoration.");
            e("Kein Stück verfügbar.", "No track available."); e("Ansage …", "Station ident …"); e("Es läuft:", "Now playing:"); e("Nächstes Stück", "Next track"); e("Sender", "Station");
            e("Neue Stücke kommen mit dem Fortschritt dazu: für jeden gereinigten Bereich und jedes Großprojekt. Lautstärke = Musik.", "New tracks are added with progress: for every cleaned area and every grand project. Volume = music.");
            e("Stücke: <b>{0} / {1}</b>", "Tracks: <b>{0} / {1}</b>"); e("Allgemein", "General"); e("Radio Zweite Chance", "Radio Second Chance");
            e("Neues Stück im Radio: „{0}“ [{1}]", "New track on the radio: “{0}” [{1}]");
            e("Eine zweite Chance (Hauptthema)", "A Second Chance (main theme)"); e("Von Anfang an im Radio.", "On the radio from the start."); e("Goldene Straßen", "Golden Streets");
            e("Ersten Bereich reinigen.", "Clean the first area."); e("Buslinie 7", "Bus Line 7"); e("Zweiten Bereich reinigen.", "Clean the second area."); e("Gewächshauslicht", "Greenhouse Light");
            e("Dritten Bereich reinigen.", "Clean the third area."); e("TERRA – Die Stadt erwacht", "TERRA – The City Awakens"); e("Großprojekt des Planeten abschließen.", "Complete the planet's grand project.");
            e("Roter Sand", "Red Sand"); e("Gießereifeuer", "Foundry Fire"); e("PYRA – Glut und Stahl", "PYRA – Embers and Steel"); e("Türkise Stille", "Turquoise Silence");
            e("Küstenwind", "Coastal Wind"); e("Lagunenlicht", "Lagoon Light"); e("PELAGIA – Das Riff atmet", "PELAGIA – The Reef Breathes"); e("Polarlicht", "Aurora");
            e("NIVALIS – Das Signal", "NIVALIS – The Signal"); e("Heimkehr (Abspann)", "Homecoming (end credits)"); e("Die Arche HORIZONT zurückholen.", "Bring the ark HORIZON back.");
            // ------------------------------------------------ Leistungsanzeige
            e("min {0} · Ø {1} (5 s)", "min {0} · avg {1} (5 s)"); e("Bildzeit {0} ms · Spitze {1} ms", "Frame time {0} ms · peak {1} ms");
            e("Draw-Calls {0} · Batches {1} · SetPass {2} · Dreiecke {3}", "Draw calls {0} · batches {1} · SetPass {2} · triangles {3}");
            e("Qualität {0} (Unity: {1}) · {2}×{3} · Render {4} % · Speicher {5} MB", "Quality {0} (Unity: {1}) · {2}×{3} · render {4} % · memory {5} MB");
            // ------------------------------------------------ Weltgenerierung: Lichtpunkte, Orte
            e("Spielplatz", "Playground"); e("Haltestelle Linie 7", "Line 7 bus stop"); e("Vorgarten", "Front garden"); e("Parkdeck", "Car park"); e("Brunnenplatz", "Fountain square");
            e("Alte Werkstatt", "Old workshop"); e("Rosengarten", "Rose garden"); e("Teichufer", "Pond shore"); e("Gewächshaus-Vorplatz", "Greenhouse forecourt");
            e("Aussichtspunkt Stützpunkt", "Viewpoint base"); e("Aussichtspunkt {0}", "Viewpoint {0}"); e("Marktstand", "Market stall"); e("Schrottwaage", "Scrap scales"); e("Schrottgasse", "Scrap alley");
            e("Kantine", "Canteen"); e("Montagehalle", "Assembly hall"); e("Lokschuppen", "Engine shed"); e("Schlackenfeld", "Slag field"); e("Kühlturm", "Cooling tower"); e("Werkstor", "Factory gate");
            e("Fischmarkt", "Fish market"); e("Inneres Hafenbecken", "Inner harbour basin"); e("Bootsschuppen", "Boat shed"); e("Stelzendorf", "Stilt village"); e("Inselschule", "Island school");
            e("Muschelbucht", "Shell bay"); e("Riffkante", "Reef edge"); e("Leuchtturm", "Lighthouse"); e("Wrackbucht", "Wreck bay"); e("Laborhof", "Lab courtyard"); e("Kuppel B", "Dome B");
            e("Messstation", "Measuring station"); e("Kühlhalle", "Cold store"); e("Serverhof", "Server yard"); e("Notstromraum", "Emergency power room"); e("Startrampe 3", "Launch pad 3");
            e("Hangar", "Hangar"); e("Leitstand", "Control room");
        }
    }
}
