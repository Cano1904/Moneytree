using System;

namespace RePlanet.Core
{
    /// <summary>Englisch: Lieferungen (Gebühr, Abklingzeit), Sturm abwarten, Helferroboter, Erfolge, Schnellreise, Weltereignisse.</summary>
    public static partial class Loc
    {
        static void FillEnglishFeatures(Action<string, string> e)
        {
            // ------------------------------------------------ Daten: Ereignisfunde, Kosmetik, Erfolge
            e("Meteoritensplitter", "Meteorite fragment"); e("Frisch vom Himmel gefallen – noch warm und voller seltener Metalle.", "Freshly fallen from the sky – still warm and full of rare metals.");
            e("Versorgungskiste", "Supply crate"); e("Ein alter Abwurfbehälter der Arche-Flotte, randvoll mit Ersatzteilen.", "An old drop container of the ark fleet, packed full of spare parts.");
            e("Minzgrün", "Mint green"); e("Graphit", "Graphite"); e("Lavendel", "Lavender"); e("Perlweiß", "Pearl white"); e("Retro-Rost", "Retro rust");
            e("Limette", "Lime"); e("Himmelblau", "Sky blue"); e("Gold", "Gold"); e("Marineblau", "Navy blue"); e("Krone", "Crown"); e("Blatt", "Leaf"); e("Blitz", "Lightning");
            e("Mond", "Moon"); e("Komet", "Comet"); e("Tropfen", "Drop"); e("Rundumleuchte", "Beacon light"); e("Glühbirne", "Light bulb"); e("Propellermütze", "Propeller cap");
            e("Erfolg „{0}“", "Achievement “{0}”"); e(" km", " km");
            e("Erste Ernte", "First Harvest"); e("Sammle 100 Objekte.", "Collect 100 objects.");
            e("Fleißiger Sammler", "Busy Collector"); e("Sammle 1 000 Objekte.", "Collect 1,000 objects.");
            e("Müllmeister", "Trash Master"); e("Sammle 4 000 Objekte.", "Collect 4,000 objects.");
            e("Langstrecke", "Long Haul"); e("Lege 20 km zu Fuß zurück.", "Travel 20 km on foot.");
            e("Marktschreier", "Market Crier"); e("Verkaufe 100-mal am Verkaufsterminal.", "Sell 100 times at the sales terminal.");
            e("Ballenkönig", "Bale King"); e("Verkaufe 40 Ballen.", "Sell 40 bales.");
            e("Ordnung muss sein", "Everything in Its Place"); e("Sortiere 1 000 Einheiten.", "Sort 1,000 units.");
            e("Saubere Sache", "Clean Job"); e("Entsorge 60 Einheiten Gefahrstoffe fachgerecht.", "Properly dispose of 60 units of hazardous waste.");
            e("Schrauber", "Tinkerer"); e("Zerlege 30 große Objekte mit dem Schneidgerät.", "Cut up 30 large objects with the cutter.");
            e("Kranführer", "Crane Operator"); e("Hebe 6 Wracks mit dem Kran an.", "Lift 6 wrecks with the crane.");
            e("Lichtbringer", "Lightbringer"); e("Repariere 20 Laternen, Knoten oder Bojen.", "Repair 20 lamps, nodes or buoys.");
            e("Chronist", "Chronicler"); e("Finde alle 16 Fundstücke.", "Find all 16 finds.");
            e("Grüner Daumen", "Green Thumb"); e("Belebe 20 ökologische Plätze.", "Bring 20 ecological spots to life.");
            e("Sturmerprobt", "Storm-Tested"); e("Warte 5 Stürme im Unterschlupf ab.", "Wait out 5 storms in a shelter.");
            e("Nachtruhe", "Good Night's Rest"); e("Schlafe 15 Nächte durch.", "Sleep through 15 nights.");
            e("Neue Freunde", "New Friends"); e("Repariere einen Helferroboter.", "Repair a helper robot.");
            e("Roboterfamilie", "Robot Family"); e("Repariere 6 Helferroboter.", "Repair 6 helper robots.");
            e("Sternschnuppe", "Shooting Star"); e("Birg 15 Ereignisfunde (Meteoriten, Versorgungskisten, Deponien).", "Recover 15 event finds (meteorites, supply crates, dumps).");
            e("Lichtnetz", "Light Network"); e("Reise 10-mal schnell zwischen Lichtpunkten.", "Fast-travel 10 times between light points.");
            e("Stammkunde", "Regular Customer"); e("Bestelle 10 Schrottlieferungen.", "Order 10 scrap deliveries.");
            e("Alter Sammelroboter", "Old collector robot"); e("Verrosteter Helfer", "Rusty helper"); e("Stiller Sortierroboter", "Silent sorting robot");
            // ------------------------------------------------ Oberfläche
            e("Erfolge", "Achievements"); e("Erfolge – {0} von {1} erreicht", "Achievements – {0} of {1} unlocked");
            e("Erfolge schalten Kosmetik für MIKO frei (Farben, Akzente, Aufkleber, Anbauteile) – ohne Spielvorteil. Anlegen im Reiter „Roboter“.",
              "Achievements unlock cosmetics for MIKO (colours, accents, stickers, attachments) – no gameplay advantage. Equip them in the “Robot” tab.");
            e("erreicht ✓", "unlocked ✓"); e("✕ Schnellreise schließen", "✕ Close fast travel"); e("✦ Schnellreise (Lichtnetz)", "✦ Fast travel (light network)"); e("Schnellreise", "Fast travel");
            e("Du stehst am Stützpunkt. Ziel wählen:", "You are at the base. Choose a destination:"); e("Du stehst am Lichtpunkt „{0}“. Ziel wählen:", "You are at the light point “{0}”. Choose a destination:");
            e("Start nur an einem leuchtenden Lichtpunkt oder am Stützpunkt. Nicht im Sturm, nicht im Fahrzeug, Behälter höchstens zu einem Viertel voll.",
              "Departure only from a lit light point or the base. Not during a storm, not in a vehicle, container at most a quarter full.");
            e("  · {0} Energie", "  · {0} energy"); e("  · leuchtet noch nicht", "  · not lit yet"); e("Helferroboter (sammelt im Umkreis {0} m, {1}/{2})", "Helper robot (collects within {0} m, {1}/{2})");
            e("{0} – defekt, reparierbar ({1})", "{0} – broken, repairable ({1})"); e("Meteoritensplitter (wertvoll)", "Meteorite fragment (valuable)"); e("Freigelegte Deponie", "Uncovered dump");
            e("{0} abwarten …", "Waiting out the {0} …"); e("Warte auf Mitspieler ({0}/{1}) – erst wenn alle abwarten, läuft die Zeit schneller.", "Waiting for other players ({0}/{1}) – time only speeds up once everyone is waiting.");
            e("Die Zeit läuft ×{0} schneller. Noch etwa {1} s Sturm.", "Time runs ×{0} faster. About {1} s of storm left."); e(" aufstehen", " get up");
            e("Nächste in {0}", "Next in {0}"); e("Lieferung bestellen ({0} Cr)", "Order delivery ({0} cr)"); e(" – leuchtet (Schnellreise-Ziel)", " – lit (fast travel destination)");
            e("neutral", "neutral"); e("fröhlich", "happy"); e("neugierig", "curious"); e("müde", "tired"); e("ängstlich", "anxious"); e("schläfrig", "sleepy"); e("frierend", "freezing");
            e("Sturm abwarten", "Wait out the storm"); e("Team", "Team"); e("Team: {0}", "Team: {0}");
            // ------------------------------------------------ Interaktionen mit Helfern
            e("[{0}] Helfer hier arbeiten lassen (Umkreis {1} m)", "[{0}] Let the helper work here (radius {1} m)"); e("Helfer absetzen", "Drop off helper");
            e("Helfer folgt dir – {0}", "Helper is following you – {0}"); e("Helfer folgt", "Helper following");
            e("[{0}] Helfer mitnehmen (neuen Arbeitsort wählen) · sammelt {1}/{2}", "[{0}] Take the helper along (choose a new workplace) · collected {1}/{2}"); e("Helfer mitnehmen", "Take helper along");
            e("{0}: Für die Reparatur fehlen: {1}. ({2})", "{0}: missing for the repair: {1}. ({2})"); e("{0}: Dieser Helfer läuft bereits. ({1})", "{0}: this helper is already running. ({1})");
            e("Helfer: Material fehlt", "Helper: material missing"); e("[{0} halten] {1} reparieren ({2})", "[{0} hold] Repair {1} ({2})"); e("Helfer reparieren", "Repair helper");
            // ------------------------------------------------ Meldungen (Server)
            e("Es fehlen {0} Credits für die Liefergebühr.", "{0} credits missing for the delivery fee."); e("Es fehlen {0} Credits für die Liefergebühr ({1} Credits).", "{0} credits missing for the delivery fee ({1} credits).");
            e("Der nächste Frachter startet in {0}.", "The next freighter departs in {0}.");
            e("MIKO ist nicht müde – Schlafen geht nur nachts. Einen Sturm wartest du im Unterschlupf ab.", "MIKO is not tired – sleeping only works at night. You wait out a storm in a shelter.");
            e("Unbekanntes Reiseziel.", "Unknown destination."); e("Der Lichtpunkt „{0}“ leuchtet noch nicht – erst komplett aufräumen.", "The light point “{0}” is not lit yet – clean it up completely first.");
            e("Erst aussteigen – Fahrzeuge reisen nicht über das Lichtnetz.", "Get out first – vehicles don't travel through the light network.");
            e("Im Sturm ist das Lichtnetz gestört – erst den Sturm abwarten.", "The light network is disrupted in the storm – wait out the storm first.");
            e("Schnellreise startet an einem leuchtenden Lichtpunkt oder am Stützpunkt.", "Fast travel starts at a lit light point or at the base."); e("Du bist schon dort.", "You are already there.");
            e("Behälter zu voll ({0}/{1}) – das Lichtnetz überträgt höchstens ein Viertel Ladung. Erst einlagern.", "Container too full ({0}/{1}) – the light network carries at most a quarter load. Store things first.");
            e("Zu wenig Energie für die Reise ({0} nötig).", "Not enough energy for the trip ({0} needed)."); e("Unbekannter Roboter.", "Unknown robot."); e("Dieser Helfer läuft bereits.", "This helper is already running.");
            e("Für die Reparatur fehlen: {0}.", "Missing for the repair: {0}."); e("Am Stützpunkt gibt es nichts zu sammeln – such einen Platz draußen.", "There is nothing to collect at the base – find a spot outside.");
            e("Drinnen gibt es nichts zu sammeln.", "There is nothing to collect indoors."); e("Helfer arbeiten nur an Land.", "Helpers only work on land.");
            e("Zum Abwarten erst aussteigen.", "Get out of the vehicle to wait."); e("Gerade tobt kein Sturm.", "There is no storm raging right now.");
            e("Hier ist kein Unterschlupf. Suche einen {0} oder baue einen Notunterschlupf ({1} Credits).", "There is no shelter here. Look for a {0} or build an emergency shelter ({1} credits).");
            e("Das Lichtnetz lädt noch …", "The light network is still charging …"); e("Helfer repariert", "Helper repaired"); e("Hier ist kein Helfer.", "There is no helper here.");
            // ------------------------------------------------ Ereignismeldungen
            e("Ausgeschlafen – aber der Sturm tobt noch. MIKO wartet ihn im Unterschlupf ab.", "Well rested – but the storm is still raging. MIKO waits it out in the shelter.");
            e("Schrottlieferung am Abladeplatz eingetroffen ({0} Teile, Gebühr {1} Credits).", "Scrap delivery arrived at the drop-off ({0} items, fee {1} credits)."); e("Gebühr {0} Credits", "fee {0} credits");
            e("★ Erfolg: {0} – Belohnung: {1} (Roboter-Reiter)", "★ Achievement: {0} – reward: {1} (Robot tab)");
            e("Meteoritenschauer über „{0}“! {1} wertvolle Splitter sind niedergegangen (Stern-Symbol auf der Karte).", "Meteor shower over “{0}”! {1} valuable fragments have come down (star symbol on the map).");
            e("Versorgungsabwurf über „{0}“ – die Kiste steckt voller Ersatzteile (Rauchzeichen, Karte).", "Supply drop over “{0}” – the crate is full of spare parts (smoke signal, map).");
            e("Der Sturm hat in „{0}“ eine verschüttete Deponie freigelegt ({1} Teile).", "The storm has uncovered a buried dump in “{0}” ({1} items).");
            e("{0} läuft wieder! Er sammelt kleinen Müll im Umkreis und schickt ihn ins Lager.", "{0} is running again! It collects small trash nearby and sends it to storage.");
            e("Der Helfer folgt dir. An einer guten Stelle [{0}] – dort arbeitet er weiter.", "The helper is following you. At a good spot press [{0}] – it will work there.");
            e("Neuer Arbeitsort für den Helfer.", "New workplace for the helper."); e("Schnellreise nach „{0}“ (−{1} Energie).", "Fast travel to “{0}” (−{1} energy).");
            e("Warte auf Mitspieler ({0}/{1} warten den Sturm ab) …", "Waiting for other players ({0}/{1} waiting out the storm) …");
        }
    }
}
