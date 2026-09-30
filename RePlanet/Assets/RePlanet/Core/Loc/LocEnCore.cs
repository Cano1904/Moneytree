using System;

namespace RePlanet.Core
{
    /// <summary>
    /// Englisch: Meldungen, die Server und Spiel zur Laufzeit auf Deutsch zusammensetzen (Ablehnungsgründe, Ziele, Ereignismeldungen,
    /// Netzwerk- und Speicherfehler). Vorlagen mit {0} … werden beim Anzeigen erkannt (<see cref="T"/>).
    /// </summary>
    public static partial class Loc
    {
        static void FillEnglishCore(Action<string, string> e)
        {
            // ------------------------------------------------ Regeln: Sammeln, Zerlegen
            e("Nichts in Reichweite.", "Nothing in range."); e("Wird gerade transportiert.", "Is being transported right now.");
            e("{0}: viel zu schwer – nur mit dem Kranfahrzeug bewegbar oder mit dem Schneidgerät zerlegbar.", "{0}: far too heavy – can only be moved with the crane vehicle or cut up with the cutter.");
            e("{0}: viel zu schwer – nur mit dem Kranfahrzeug bewegbar.", "{0}: far too heavy – can only be moved with the crane vehicle.");
            e("Ölteppich: mit dem Filtermodul reinigen.", "Oil slick: clean it with the filter module."); e("Eingefroren – zuerst mit dem Wärmemodul auftauen.", "Frozen – thaw it with the heat module first.");
            e("Liegt unter Wasser – abtauchen (Strg/C bzw. LB).", "Lies underwater – dive (Ctrl/C or LB)."); e("Liegt unter Wasser – Tauchmodul nötig (Werkstatt).", "Lies underwater – diving module needed (workshop).");
            e("Gefahrstoff Klasse {0}: Gefahrgutbehälter Stufe {1} nötig.", "Hazardous waste class {0}: hazmat container level {1} needed.");
            e("Zu sperrig für den Greifarm – Magnetarm (Stufe {0}) nutzen.", "Too bulky for the gripper arm – use the magnet arm (level {0}).");
            e("Zu groß – mit dem Schneidgerät zerlegen.", "Too big – cut it up with the cutter."); e("Mit dem Greifarm nicht greifbar.", "Cannot be grabbed with the gripper arm.");
            e("Zu schwer für den Greifarm ({0} kg > {1} kg). Schneidgerät zerlegt es.", "Too heavy for the gripper arm ({0} kg > {1} kg). The cutter can cut it up.");
            e("Zu schwer für den Greifarm ({0} kg > {1} kg). Der Magnetarm schafft es.", "Too heavy for the gripper arm ({0} kg > {1} kg). The magnet arm can do it.");
            e("Zu schwer für den Greifarm ({0} kg > {1} kg). Greifarm verbessern.", "Too heavy for the gripper arm ({0} kg > {1} kg). Upgrade the gripper arm.");
            e("Kein Müllsauger vorhanden (Werkstatt).", "No trash vacuum yet (workshop)."); e("Zu schwer oder sperrig für den Sauger.", "Too heavy or bulky for the vacuum.");
            e("Kein Magnetarm vorhanden (Werkstatt).", "No magnet arm yet (workshop)."); e("Nicht magnetisch (Kupfer ist nicht magnetisch).", "Not magnetic (copper is not magnetic).");
            e("Nicht magnetisch.", "Not magnetic."); e("Magnet zu schwach – Stufe {0} nötig.", "Magnet too weak – level {0} needed."); e("Dieses Werkzeug sammelt nicht.", "This tool does not collect.");
            e("Behälter voll ({0}/{1}) – zum Stützpunkt bringen oder pressen (Taste R).", "Container full ({0}/{1}) – bring it to the base or compact it (key R).");
            e("Behälter voll ({0}/{1}) – zum Stützpunkt bringen.", "Container full ({0}/{1}) – bring it to the base.");
            e("Nichts zum Zerlegen in Reichweite.", "Nothing to cut up in range."); e("Kein Schneidgerät vorhanden (Werkstatt).", "No cutter yet (workshop).");
            e("Kann nicht zerlegt werden.", "Cannot be cut up."); e("Schneidgerät Stufe {0} nötig.", "Cutter level {0} needed."); e("Eingefroren – zuerst auftauen.", "Frozen – thaw it first.");
            e("Im Unterschlupf – hier drinnen wird nicht gesammelt.", "In the shelter – no collecting indoors.");
            // ------------------------------------------------ Regeln: Bauen, Projekte, Unterschlupf
            e("Unbekanntes Bauwerk.", "Unknown building."); e("Erst nach dem Projekt „{0}“ verfügbar.", "Only available after the project “{0}”."); e("Außerhalb der Baufläche.", "Outside the build area.");
            e("Kollision mit {0}.", "Collides with {0}."); e("Unbekanntes Projekt.", "Unknown project."); e("Bereits abgeschlossen.", "Already completed."); e("Wird gerade gebaut.", "Under construction.");
            e("Zuerst „{0}“ abschließen.", "Complete “{0}” first."); e("Der Zugang zum Bereich ist noch versperrt.", "Access to the area is still blocked.");
            e("Erst den Hauptmüll entfernen: {0} % von {1} %.", "Remove the main trash first: {0} % of {1} %."); e("Es fehlen: {0}.", "Missing: {0}."); e("{0} Credits", "{0} credits");
            e("Am Stützpunkt gibt es schon ein Dach.", "The base already has a roof."); e("Höchstens {0} Notunterschlüpfe pro Planet.", "At most {0} emergency shelters per planet.");
            e("Nicht im Wasser baubar.", "Cannot be built in water."); e("Kein Platz – freie Fläche suchen.", "No room – look for a clear spot."); e("Hier in der Nähe gibt es schon einen Unterschlupf.", "There is already a shelter nearby.");
            // ------------------------------------------------ Ziele
            e("{0} ({1}/{2})", "{0} ({1}/{2})"); e("{0} aufräumen: {1} % / {2} %", "{0}: clean-up {1} % / {2} %"); e("„{0}“ wird gebaut … {1} %", "“{0}” is being built … {1} %");
            e("Projekt „{0}“ starten – {1}", "Start project “{0}” – {1}"); e("Projekt „{0}“ starten (am Projektplatz).", "Start project “{0}” (at the project site).");
            e("Mit dem Transportschiff nach {0} reisen: {1}.", "Travel to {0} with the transport ship: {1}.");
            e("Finale: Transportschiff aufrüsten – {0} ({1} Credits) für {2}.", "Finale: upgrade the transport ship – {0} ({1} credits) for {2}.");
            e("Großprojekt auf {0} abschließen: „{1}“.", "Complete the grand project on {0}: “{1}”."); e("Ökologie in {0}: {1} (Bio-Modul).", "Ecology in {0}: {1} (bio module).");
            e("Freies Spiel: Recyclingaufträge, Lieferungen und die letzten Ecken aufräumen.", "Free play: recycling orders, deliveries and cleaning up the last corners.");
            // ------------------------------------------------ Simulation
            e("Akku leer – Notbetrieb. Fahre zum Ladeplatz am Stützpunkt (nur der Greifarm funktioniert).", "Battery empty – emergency mode. Drive to the charging spot at the base (only the gripper arm works).");
            e("Zugang freigelegt", "Access cleared"); e("Bereich gereinigt", "Area cleaned"); e("Nach dem Schlafen", "After sleeping"); e("Ökologie abgeschlossen", "Ecology completed");
            e("Projekt abgeschlossen", "Project completed"); e("Projekt gestartet", "Project started"); e("Planetenwechsel", "Planet change"); e("Unbekannter Planet im Spielstand", "Unknown planet in the save");
            // ------------------------------------------------ Aktionen (GameActions)
            e("Teure Käufe, Abriss und Reisen sind dem Host vorbehalten (Host kann den Vertrauensmodus aktivieren).", "Expensive purchases, demolition and travel are reserved for the host (the host can enable trust mode).");
            e("Ungültige Aktion.", "Invalid action."); e("Spieler nicht verbunden.", "Player not connected."); e("MIKO ist abgeschaltet – die Abschleppdrohne ist unterwegs.", "MIKO is shut down – the tow drone is on its way.");
            e("Nur der Host kann das ändern.", "Only the host can change that."); e("Interner Fehler: {0}", "Internal error: {0}"); e("Unbekannte Aktion: {0}", "Unknown action: {0}");
            e("Im Fahrzeug nicht möglich.", "Not possible in a vehicle."); e("Schon eingesammelt.", "Already collected."); e("Zu weit entfernt.", "Too far away."); e("Magnet lädt noch …", "Magnet still charging …");
            e("Kein Metall in Reichweite.", "No metal in range."); e("Liegt unter Wasser – abtauchen.", "Lies underwater – dive."); e("Kein Wärmemodul vorhanden (Werkstatt, ab NIVALIS).", "No heat module yet (workshop, from NIVALIS).");
            e("Kein Filtermodul vorhanden (Werkstatt, ab PELAGIA).", "No filter module yet (workshop, from PELAGIA)."); e("Kein Ölteppich in Reichweite.", "No oil slick in range.");
            e("Behälter voll – das Altöl braucht Platz im Filtertank.", "Container full – the waste oil needs room in the filter tank."); e("Keine Müllpresse vorhanden (Werkstatt).", "No trash compactor yet (workshop).");
            e("Nichts Pressbares im Behälter (Papier, Kunststoff, Metall, Stahl, Kupfer, Netze).", "Nothing compactable in the container (paper, plastic, metal, steel, copper, nets).");
            e("Zum Lager des Stützpunkts fahren.", "Drive to the base storage."); e("Behälter ist leer.", "Container is empty."); e("Lager voll ({0}/{1}) – verkaufen oder Lagerhalle bauen.", "Storage full ({0}/{1}) – sell or build a warehouse.");
            e("Zum Verkaufsterminal fahren.", "Drive to the sales terminal."); e("Gefahrstoffe bitte bei der Entsorgung abgeben.", "Please hand in hazardous waste at the disposal station.");
            e("Unbekanntes Material.", "Unknown material."); e("Gefahrstoffe werden bei der Entsorgung abgegeben, nicht verkauft.", "Hazardous waste is handed in at the disposal station, not sold.");
            e("Nicht genug {0} im Lager.", "Not enough {0} in storage."); e("Zum Sortiertisch fahren.", "Drive to the sorting table."); e("Nichts Unsortiertes im Lager – zuerst Müll einlagern.", "Nothing unsorted in storage – store some trash first.");
            e("Zur Entsorgungsstation fahren.", "Drive to the disposal station."); e("Keine Gefahrstoffe dabei oder im Lager.", "No hazardous waste carried or in storage.");
            e("Upgrades gibt es in der Werkstatt am Stützpunkt.", "Upgrades are available in the workshop at the base."); e("Unbekanntes Upgrade.", "Unknown upgrade."); e("Bereits voll ausgebaut.", "Already fully upgraded.");
            e("Erst verfügbar, wenn {0} erreichbar ist.", "Only available once {0} is reachable."); e("Es fehlen {0} Credits.", "{0} credits missing.");
            e("Fahrzeuge gibt es in der Garage am Stützpunkt.", "Vehicles are available in the garage at the base."); e("Unbekanntes Fahrzeug.", "Unknown vehicle."); e("Bereits vorhanden.", "Already owned.");
            e("Erst auf {0} nutzbar.", "Only usable on {0}."); e("Am Landeplatz des Stützpunkts aufrüsten.", "Upgrade at the base landing pad."); e("Das Transportschiff ist voll ausgebaut.", "The transport ship is fully upgraded.");
            e("Zum Materialhändler fahren.", "Drive to the material trader."); e("Dieses Material wird nicht gehandelt.", "This material is not traded."); e("Lager voll.", "Storage full.");
            e("Nur am Stützpunkt baubar.", "Can only be built at the base."); e("Maximal {0}× {1} pro Stützpunkt.", "At most {0}× {1} per base."); e("Nur am Stützpunkt möglich.", "Only possible at the base.");
            e("Bauwerk nicht gefunden.", "Building not found."); e("Unbekannter Reparaturpunkt.", "Unknown repair spot."); e("Bereits repariert.", "Already repaired.");
            e("Für die Reparatur fehlen im Lager: {0}.", "Missing in storage for the repair: {0}."); e("Bio-Modul nötig (Werkstatt).", "Bio module needed (workshop)."); e("Unbekannter Pflanzplatz.", "Unknown planting spot.");
            e("Hier wächst schon etwas.", "Something is already growing here."); e("Erst die Infrastruktur wiederherstellen (Projekt „{0}“) – dann folgt die Begrünung.", "Restore the infrastructure first (project “{0}”) – then the greening follows.");
            e("Es fehlen {0} Credits für das Saatgut.", "{0} credits missing for the seeds."); e("Unbekanntes Fundstück.", "Unknown find."); e("Bereits gefunden.", "Already found.");
            e("Unbekannter Aussichtspunkt.", "Unknown viewpoint."); e("Zum Projektplatz fahren.", "Drive to the project site."); e("Fahrzeug nicht vorhanden.", "Vehicle not available.");
            e("Du sitzt schon in einem Fahrzeug.", "You are already in a vehicle."); e("Das Fahrzeug wird bereits gesteuert.", "The vehicle is already being driven."); e("Das Fahrzeug wird gerade gesteuert.", "The vehicle is being driven right now.");
            e("Kein Transportfahrzeug in der Nähe.", "No transport vehicle nearby."); e("Zu weit vom Fahrzeug entfernt.", "Too far from the vehicle."); e("Ladefläche voll.", "Cargo bed full.");
            e("Du sitzt in keinem Fahrzeug.", "You are not in a vehicle."); e("Zum Abladeplatz am Stützpunkt fahren.", "Drive to the drop-off at the base."); e("Lager voll – verkaufen oder Lagerhalle bauen.", "Storage full – sell or build a warehouse.");
            e("Nichts geladen.", "Nothing loaded."); e("Nur mit dem Transportrover.", "Only with the transport rover."); e("{0} passt nicht in den Ansaugschacht.", "{0} does not fit into the intake chute.");
            e("Gefahrgutbehälter nötig.", "Hazmat container needed."); e("Ladefläche voll – zum Stützpunkt fahren.", "Cargo bed full – drive to the base."); e("Nur mit dem Sammelboot.", "Only with the collector boat.");
            e("Netz voll – im Hafen abladen.", "Net full – unload in the harbour."); e("Hier gibt es nichts anzuheben.", "There is nothing to lift here."); e("Näher heranfahren.", "Drive closer.");
            e("Dafür brauchst du das Kranfahrzeug.", "You need the crane vehicle for that."); e("Der Kran trägt bereits eine Last.", "The crane is already carrying a load."); e("Kein Wrack in Reichweite.", "No wreck in range.");
            e("Das ist zu klein für den Kran – mit dem Greifarm sammeln.", "That is too small for the crane – collect it with the gripper arm."); e("Näher an das Wrack fahren.", "Drive closer to the wreck.");
            e("Der Kran trägt nichts.", "The crane is not carrying anything."); e("Last verloren.", "Load lost."); e("Unbekanntes Ziel.", "Unknown destination.");
            e("Lieferungen werden am Stützpunkt bestellt.", "Deliveries are ordered at the base."); e("Die letzte Lieferung liegt noch am Abladeplatz.", "The last delivery is still at the drop-off.");
            e("Zur Auftragstafel fahren.", "Drive to the mission board."); e("Es fehlen {0} sortierte Einheiten {1}.", "{0} sorted units of {1} missing."); e("Unbekannte Kosmetik.", "Unknown cosmetic.");
            e("Noch nicht freigeschaltet: {0}", "Not unlocked yet: {0}"); e("MIKO ist abgeschaltet.", "MIKO is shut down."); e("Zum Schlafen erst aussteigen.", "Get out of the vehicle to sleep.");
            e("Hier ist kein Unterschlupf. Suche einen {0}, fahre zum Stützpunkt oder baue einen Notunterschlupf ({1} Credits).", "There is no shelter here. Look for a {0}, drive to the base or build an emergency shelter ({1} credits).");
            e("MIKO ist nicht müde – Schlafen geht nachts oder während eines Sturms.", "MIKO is not tired – sleeping works at night or during a storm.");
            e("Es fehlen {0} Credits für den Notunterschlupf.", "{0} credits missing for the emergency shelter."); e("Nicht möglich.", "Not possible.");
            // ------------------------------------------------ Netzwerk, Sitzung
            e("Ungültige Nachrichtengröße", "Invalid message size"); e("Lokale Sitzung beendet.", "Local session ended."); e("Port {0} ist bereits belegt. Anderen Port in den Einstellungen wählen.", "Port {0} is already in use. Choose another port in the settings.");
            e("Server konnte nicht gestartet werden: {0}", "Server could not be started: {0}"); e("Keine Antwort von {0} (Zeitüberschreitung). Prüfe Adresse, Firewall und Portweiterleitung.", "No answer from {0} (timeout). Check address, firewall and port forwarding.");
            e("Verbindung zum Host wurde getrennt.", "The connection to the host was closed."); e("Verbindung abgelehnt: Auf {0} läuft keine RE:PLANET-Sitzung.", "Connection refused: no RE:PLANET session is running on {0}.");
            e("Adresse „{0}“ wurde nicht gefunden.", "Address “{0}” was not found."); e("Netzwerkfehler: {0}", "Network error: {0}"); e("Verbindung verloren: {0}", "Connection lost: {0}"); e("Senden fehlgeschlagen: {0}", "Sending failed: {0}");
            e("Die Sitzung wurde beendet.", "The session has ended."); e("Spielversion passt nicht zum Host (Protokoll {0} statt {1}).", "Game version does not match the host (protocol {0} instead of {1}).");
            e("Ungültiges Spielerprofil.", "Invalid player profile."); e("Dieses Spielerprofil ist bereits in der Sitzung.", "This player profile is already in the session."); e("Die Sitzung ist voll ({0}/{1}).", "The session is full ({0}/{1}).");
            e("Host verlässt die Sitzung", "Host is leaving the session"); e("Der Host hat die Sitzung beendet. Der Spielstand wurde auf dem Server gesichert.", "The host has ended the session. The save was stored on the server.");
            e("Der Host hat die Sitzung beendet. Der Spielstand wurde beim Host gesichert.", "The host has ended the session. The save was stored by the host.");
            e("Host-Verbindung verloren", "Host connection lost"); e("Verbindung zum Host unterbrochen – warte bis zu {0} Sekunden …", "Connection to the host interrupted – waiting up to {0} seconds …");
            e("Host nicht zurückgekehrt", "Host did not return"); e("Der Host ist nicht zurückgekehrt. Die Welt wurde gesichert.", "The host did not return. The world has been saved.");
            e("Unbekanntes Protokoll.", "Unknown protocol."); e("Dieser Host erstellt keine neuen Sitzungen.", "This host does not create new sessions."); e("Spielstand wurde abgelehnt: {0}", "Save was rejected: {0}");
            e("Koop-Welt", "Co-op world"); e("Sitzung „{0}“ wurde nicht gefunden. Code prüfen.", "Session “{0}” was not found. Check the code."); e("Verbindung verloren.", "Connection lost.");
            e("Der Host hat die Sitzung beendet.", "The host has ended the session.");
            // ------------------------------------------------ Speichern
            e("Datei ist beschädigt (kein gültiges JSON).", "The file is damaged (not valid JSON)."); e("Kein RE:PLANET-Spielstand.", "Not a RE:PLANET save."); e("Spielstand ist leer.", "The save is empty.");
            e("Spielstand ist beschädigt (kein gültiges JSON).", "The save is damaged (not valid JSON)."); e("Spielstand stammt aus einer neueren Spielversion ({0}).", "The save comes from a newer game version ({0}).");
            e("Spielstand enthält keine Daten.", "The save contains no data."); e("Prüfsumme stimmt nicht – der Spielstand wurde beschädigt.", "Checksum mismatch – the save has been damaged."); e("Weltdaten sind beschädigt.", "World data is damaged.");
            e("Weltdaten konnten nicht gelesen werden: {0}", "World data could not be read: {0}"); e("Schreibprüfung fehlgeschlagen: {0}", "Write check failed: {0}"); e("Speichern fehlgeschlagen: {0}", "Saving failed: {0}");
            e("Kein Spielstand vorhanden.", "No save available."); e("Hauptspielstand unbrauchbar ({0}) – Backup wurde geladen.", "Main save unusable ({0}) – the backup was loaded.");
            e("{0} Backup ebenfalls unbrauchbar: {1}", "{0} Backup unusable as well: {1}"); e("Nur Backup vorhanden", "Only backup available");
            // ------------------------------------------------ Spiel (GameApp): Abläufe und Ereignismeldungen
            e("Spielstand nicht ladbar", "Save could not be loaded"); e("Unbekannter Fehler.", "Unknown error."); e("Welt wird geladen …", "Loading world …");
            e("Nur der Host kann eine Sitzung öffnen.", "Only the host can open a session."); e("Koop geschlossen – die Welt ist wieder privat.", "Co-op closed – the world is private again.");
            e("Adresse ungültig. Format: 192.168.0.10:7777/ABC123", "Invalid address. Format: 192.168.0.10:7777/ABC123"); e("Bitte den Sitzungscode angeben.", "Please enter the session code.");
            e("Verbindung nicht möglich: {0}", "Cannot connect: {0}"); e("Verbinde mit {0} …", "Connecting to {0} …"); e("Der Host hat die Sitzung beendet. Die Welt wurde beim Host gesichert.", "The host has ended the session. The world was saved by the host.");
            e("Die Welt gehört dem Host – nur der Host kann speichern.", "The world belongs to the host – only the host can save."); e("Gespeichert ({0}).", "Saved ({0}).");
            e("Zeitüberschreitung – keine Antwort vom Host.", "Timeout – no answer from the host."); e("Verbindung fehlgeschlagen", "Connection failed"); e("Sitzung beendet", "Session ended");
            e("Der Host hat das Spiel beendet. Die Welt wurde beim Host gesichert.", "The host has quit the game. The world was saved by the host.");
            e("✦ Lichtpunkt „{0}“ ist wieder sauber!", "✦ Light point “{0}” is clean again!"); e("Der Weg nach „{0}“ ist frei!", "The way to “{0}” is clear!");
            e("{0}: Hauptmüll entfernt! Jetzt das Projekt am Projektplatz starten.", "{0}: main trash removed! Now start the project at the project site."); e("Bau gestartet: „{0}“.", "Construction started: “{0}”.");
            e("GROSSPROJEKT abgeschlossen: „{0}“ – die Umgebung erwacht!", "GRAND PROJECT completed: “{0}” – the surroundings awaken!"); e("Projekt abgeschlossen: „{0}“ – die Umgebung erwacht!", "Project completed: “{0}” – the surroundings awaken!");
            e("{0} ist jetzt erreichbar!", "{0} is now reachable!"); e("Auftrag erledigt: {0} (+{1} Credits)", "Mission completed: {0} (+{1} credits)"); e("Auftrag erledigt: {0}", "Mission completed: {0}");
            e("Neuer Auftrag: {0}", "New mission: {0}"); e("Kosmetik freigeschaltet: {0}", "Cosmetic unlocked: {0}"); e("{0} zieht auf! Suche einen Unterschlupf.", "{0} is coming! Look for a shelter.");
            e("Ein Sturm", "A storm"); e("{0}! Ohne Unterschlupf leert sich der Akku schnell.", "{0}! Without shelter the battery drains quickly.");
            e("Der {0} ist vorbei – die Dünen haben sich verschoben, neue Wege sind frei.", "{0} has passed – the dunes have shifted, new paths are open."); e("Der {0} ist vorbei.", "{0} has passed.");
            e("Die Nacht bricht herein. Suche einen Unterschlupf und schlafe [{0}].", "Night is falling. Find a shelter and sleep [{0}]."); e("Ein neuer Morgen bricht an.", "A new morning dawns.");
            e("Ausgeschlafen! Ein neuer Morgen – Akku voll.", "Well rested! A new morning – battery full."); e("Notabschaltung! Eine Abschleppdrohne bringt MIKO zum Stützpunkt …", "Emergency shutdown! A tow drone is bringing MIKO to the base …");
            e("{0} hatte eine Notabschaltung und wird abgeschleppt.", "{0} had an emergency shutdown and is being towed."); e("Am Stützpunkt angekommen. Der Akku lädt.", "Arrived at the base. The battery is charging.");
            e("Warte auf Mitspieler ({0}/{1} schlafen) …", "Waiting for other players ({0}/{1} asleep) …"); e("Notunterschlupf gebaut – hier kannst du schlafen.", "Emergency shelter built – you can sleep here.");
            e("Fundstück entdeckt: {0} (Archiv)", "Find discovered: {0} (archive)"); e("+{0} Credits", "+{0} credits"); e("Gefahrstoffe fachgerecht entsorgt: +{0} Credits", "Hazardous waste properly disposed of: +{0} credits");
            e("Recyclingauftrag erfüllt: +{0} Credits", "Recycling order fulfilled: +{0} credits"); e("Eingebaut: {0} – {1}", "Installed: {0} – {1}"); e("Neues Fahrzeug in der Garage: {0}", "New vehicle in the garage: {0}");
            e("Sprungantrieb eingebaut!", "Jump drive installed!"); e("Gebaut: {0}", "Built: {0}"); e("{0} ist der Sitzung beigetreten.", "{0} joined the session."); e("{0} hat die Sitzung verlassen.", "{0} left the session.");
            e("Schrottlieferung am Abladeplatz eingetroffen ({0} Teile).", "Scrap delivery arrived at the drop-off ({0} items)."); e("Wrack verwertet: +{0} Einheiten im Lager.", "Wreck recycled: +{0} units in storage.");
            e("Repariert! (+15 Credits)", "Repaired! (+15 credits)"); e("{0}: Ökologie wiederhergestellt – hier lebt es wieder!", "{0}: ecology restored – life has returned!");
            e("Wrack angehoben – mit {0} Helfern!", "Wreck lifted – with {0} helpers!"); e("Wrack angehoben. Auf den Transporter setzen oder zum Stützpunkt fahren.", "Wreck lifted. Put it on the transporter or drive to the base.");
            e("Wrack liegt auf dem Transportrover – ab zum Stützpunkt!", "The wreck is on the transport rover – off to the base!");
            e("Freies Spiel: Die Welten gehören jetzt dir. Recyclingaufträge und Lieferungen bleiben verfügbar.", "Free play: the worlds are yours now. Recycling orders and deliveries remain available.");
            e("Foto gespeichert: {0}", "Photo saved: {0}"); e("Foto konnte nicht gespeichert werden: {0}", "Photo could not be saved: {0}"); e("Welt konnte nicht vollständig aufgebaut werden: {0}", "The world could not be built completely: {0}");
            // Kurzformen im ruhigen HUD (HudHints)
            e("Auftrag erfüllt  +{0}", "Order fulfilled  +{0}"); e("✦ {0} sauber", "✦ {0} clean"); e("Weg frei: {0}", "Way clear: {0}"); e("{0}: Hauptmüll entfernt", "{0}: main trash removed");
            e("Projekt fertig: „{0}“", "Project done: “{0}”"); e("✓ {0}  +{1}", "✓ {0}  +{1}"); e("✓ {0}", "✓ {0}"); e("Neu: {0}", "New: {0}"); e("⚠ {0} zieht auf", "⚠ {0} is coming"); e("⚠ {0}!", "⚠ {0}!");
            e("{0} vorbei – neue Wege frei", "{0} over – new paths open"); e("{0} vorbei", "{0} over"); e("Nacht – Unterschlupf suchen", "Night – find a shelter"); e("Guten Morgen – Akku voll", "Good morning – battery full");
            e("Notabschaltung!", "Emergency shutdown!"); e("{0} wird abgeschleppt", "{0} is being towed"); e("Am Stützpunkt – Akku lädt", "At the base – battery charging"); e("Unterschlupf gebaut", "Shelter built");
            e("Fundstück: {0}", "Find: {0}"); e("Eingebaut: {0}", "Installed: {0}"); e("Neu in der Garage: {0}", "New in the garage: {0}"); e("{0} ist da", "{0} is here"); e("{0} ist gegangen", "{0} has left");
            e("Lieferung: {0}", "Delivery: {0}"); e("{0} Teile", "{0} items"); e("Lieferung ist da", "Delivery has arrived"); e("Wrack verwertet  +{0}", "Wreck recycled  +{0}"); e("Repariert  +{0}", "Repaired  +{0}");
            e("{0}: Leben kehrt zurück", "{0}: life returns"); e("Wrack angehoben – mit {0} Helfern", "Wreck lifted – with {0} helpers"); e("Wrack angehoben", "Wreck lifted"); e("Wrack auf dem Rover", "Wreck on the rover");
            e("Aussicht gemerkt", "View saved"); e("Koop geschlossen", "Co-op closed"); e("Koop geöffnet", "Co-op opened");
            // Häufige gekürzte Formen (erster Satz / vor „ – “)
            e("Die Nacht bricht herein", "Night is falling"); e("Notunterschlupf gebaut", "Emergency shelter built"); e("Am Stützpunkt angekommen", "Arrived at the base");
        }
    }
}
