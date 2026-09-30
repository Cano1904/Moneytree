using System;

namespace RePlanet.Core
{
    /// <summary>Englisch: Datentabellen (Materialien, Müll, Upgrades, Fahrzeuge, Gebäude, Planeten, Projekte, Aufträge, Fundstücke, Kosmetik).</summary>
    public static partial class Loc
    {
        static void FillEnglishData(Action<string, string> e)
        {
            // ------------------------------------------------ Materialien
            e("Papier", "Paper"); e("Glas", "Glass"); e("Kunststoff", "Plastic"); e("Metall", "Metal"); e("Stahl", "Steel"); e("Kupfer", "Copper");
            e("Elektronik", "Electronics"); e("Netze & Seile", "Nets & ropes"); e("Akkus", "Batteries"); e("Seltene Metalle", "Rare metals");
            e("Gefahrstoffe", "Hazardous waste"); e("Altöl", "Waste oil");
            // ------------------------------------------------ Müll
            e("Zeitungsbündel", "Bundle of newspapers"); e("Karton", "Cardboard box"); e("Glasflasche", "Glass bottle"); e("Plastikflasche", "Plastic bottle");
            e("Plastiktüte", "Plastic bag"); e("Getränkedose", "Drink can"); e("Toaster", "Toaster"); e("Mikrowelle", "Microwave"); e("Kühlschrank", "Fridge");
            e("Einkaufswagen", "Shopping cart"); e("Autowrack", "Car wreck"); e("Umgestürzter Stadtbus", "Overturned city bus"); e("Alter Farbeimer", "Old paint bucket");
            e("Altbatterie", "Used battery"); e("Rostiges Gartengerät", "Rusty garden tool"); e("Kunststoff-Blumentopf", "Plastic flower pot"); e("Alter Fernseher", "Old TV");
            e("Blechteil", "Sheet metal"); e("Platine", "Circuit board"); e("Glasscherben", "Broken glass"); e("Kältemittelbehälter", "Refrigerant canister");
            e("Stahlstück", "Piece of steel"); e("Netzstück", "Piece of net"); e("Kupferkabel-Stück", "Piece of copper cable"); e("Akkuzelle", "Battery cell");
            e("Seltenmetall-Kern", "Rare-metal core"); e("Schraubenhaufen", "Pile of screws"); e("Stahlträger", "Steel girder"); e("Kupferkabeltrommel", "Copper cable drum");
            e("Zahnrad", "Gear"); e("Maschinenteil", "Machine part"); e("Motorblock", "Engine block"); e("Fahrzeugwrack", "Vehicle wreck");
            e("Gefahrstofffass (gekennzeichnet)", "Hazmat barrel (labelled)"); e("Rohrstück", "Piece of pipe"); e("Verkeiltes Fabrikfahrzeug", "Jammed factory vehicle");
            e("Plastikkanister", "Plastic canister"); e("Geisternetz", "Ghost net"); e("Elektroschrott", "E-waste"); e("Schiffsteil", "Ship part"); e("Kaputte Boje", "Broken buoy");
            e("Ölteppich", "Oil slick"); e("Treibende Plastikflasche", "Floating plastic bottle"); e("Treibende Tüte", "Floating bag"); e("Gesunkenes Wrackteil", "Sunken wreck part");
            e("Anker mit Kette", "Anchor with chain"); e("Industrie-Akku", "Industrial battery"); e("Platinenstapel", "Stack of circuit boards"); e("Serverschrank", "Server rack");
            e("Eingefrorene Maschine", "Frozen machine"); e("Kabeltrommel", "Cable drum"); e("Solarpanel-Bruch", "Broken solar panel"); e("Abgestürztes Shuttle", "Crashed shuttle");
            e("Drohnenwrack", "Drone wreck");
            // ------------------------------------------------ Upgrades
            e("Müllbehälter", "Trash container"); e("Mehr Volumen im sichtbaren Rückenbehälter.", "More volume in the visible back container."); e("Kapazität", "Capacity");
            e("12 Vol.", "12 vol."); e("20 Vol.", "20 vol."); e("30 Vol.", "30 vol."); e("45 Vol.", "45 vol."); e("65 Vol.", "65 vol.");
            e("Hebt einzelne Objekte. Stärke = maximale Masse.", "Lifts single objects. Strength = maximum mass."); e("Werkzeug", "Tool"); e("Tragkraft", "Lifting power");
            e("3 kg", "3 kg"); e("6 kg", "6 kg"); e("12 kg", "12 kg");
            e("Saugt leichte Objekte (bis 1 kg) im Kegel vor MIKO ein.", "Sucks up light objects (up to 1 kg) in a cone in front of MIKO."); e("Saugrate", "Suction rate");
            e("nicht vorhanden", "not installed"); e("3 Obj./s, 4 m", "3 obj./s, 4 m"); e("5 Obj./s, 6 m", "5 obj./s, 6 m"); e("8 Obj./s, 8 m", "8 obj./s, 8 m");
            e("Zieht Metall an. Aufladen + Loslassen = Magnetwelle.", "Attracts metal. Charge + release = magnet wave."); e("Wellen-Reichweite", "Wave range");
            e("7 m, 6 Teile", "7 m, 6 parts"); e("11 m, 12 Teile", "11 m, 12 parts"); e("15 m, 20 Teile", "15 m, 20 parts");
            e("Zerlegt große Objekte und Netze in tragbare Teile.", "Cuts large objects and nets into portable pieces."); e("Schnitttempo", "Cutting speed");
            e("Tempo 1,0×", "Speed 1.0×"); e("Tempo 1,8×", "Speed 1.8×"); e("Müllpresse", "Trash compactor");
            e("Presst Papier, Kunststoff, Metall, Stahl, Kupfer und Netze im Behälter auf halbes Volumen.", "Compacts paper, plastic, metal, steel, copper and nets in the container to half their volume.");
            e("Pressfaktor", "Compaction"); e("keine Presse", "no compactor"); e("Volumen ×0,5", "Volume ×0.5"); e("Akku", "Battery");
            e("Mehr Energie für Werkzeuge und Sprint.", "More energy for tools and sprinting."); e("Energieeffizienz", "Energy efficiency");
            e("Geräte verbrauchen weniger Energie.", "Devices use less energy."); e("Verbrauch", "Consumption"); e("Gefahrgutbehälter", "Hazmat container");
            e("Erlaubt sicheres Sammeln gekennzeichneter Gefahrstoffe.", "Allows safe collection of labelled hazardous waste."); e("Gefahrenklasse", "Hazard class");
            e("keine", "none"); e("Klasse 1", "Class 1"); e("Klasse 1–2", "Class 1–2"); e("Anhänger", "Trailer");
            e("MIKO zieht einen kleinen Anhänger mit zusätzlichem Volumen.", "MIKO pulls a small trailer with extra volume."); e("Zusatzvolumen", "Extra volume");
            e("kein Anhänger", "no trailer"); e("+25 Vol.", "+25 vol.");
            e("Pflanzt Setzlinge, setzt Riffmodule und Flechtenkulturen (ökologische Aktionen).", "Plants seedlings, places reef modules and lichen cultures (ecological actions).");
            e("Modul", "Module"); e("vorhanden", "installed");
            e("Taut eingefrorene Objekte auf. Nötig auf NIVALIS.", "Thaws frozen objects. Needed on NIVALIS."); e("Tautempo", "Thawing speed"); e("Tempo 2,0×", "Speed 2.0×");
            e("Isolation", "Insulation"); e("Halbiert den Kälteverbrauch auf NIVALIS.", "Halves the cold drain on NIVALIS."); e("Kälteverbrauch", "Cold drain");
            e("×1,6 bei Kälte", "×1.6 in the cold"); e("×1,1 bei Kälte", "×1.1 in the cold"); e("Tauchmodul", "Diving module");
            e("Druckfeste Hülle: MIKO kann unter Wasser tauchen und sammeln.", "Pressure-proof hull: MIKO can dive and collect underwater.");
            e("nur schwimmen", "swimming only"); e("tauchen bis 14 m", "diving down to 14 m");
            e("Reinigt Ölteppiche und belastetes Wasser.", "Cleans oil slicks and polluted water."); e("Filterrate", "Filter rate"); e("Rate 1,0×", "Rate 1.0×"); e("Rate 2,0×", "Rate 2.0×");
            e("Drohnentechnik", "Drone technology"); e("Verbessert Sammeldrohnen der Drohnenhangars (Radius und Kapazität).", "Improves the collector drones of drone hangars (radius and capacity).");
            e("Automatisierung", "Automation"); e("Einsatzradius", "Operating radius"); e("35 m, 3 Vol.", "35 m, 3 vol."); e("50 m, 5 Vol.", "50 m, 5 vol."); e("70 m, 8 Vol.", "70 m, 8 vol.");
            e("Greifarm", "Gripper arm"); e("Müllsauger", "Trash vacuum"); e("Magnetarm", "Magnet arm"); e("Schneidgerät", "Cutter"); e("Wärmemodul", "Heat module");
            e("Filtermodul", "Filter module"); e("Bio-Modul", "Bio module"); e("Behälter", "Container");
            // ------------------------------------------------ Fahrzeuge
            e("Transportrover", "Transport rover"); e("Schneller Transporter mit Ladefläche (40 Vol.) und Ansaugschacht. Trägt auch Wracks vom Kran.", "Fast transporter with a cargo bed (40 vol.) and an intake chute. Also carries wrecks from the crane.");
            e("Kranfahrzeug", "Crane vehicle"); e("Hebt schwere Wracks an und setzt sie auf den Transportrover oder am Stützpunkt ab.", "Lifts heavy wrecks and sets them down on the transport rover or at the base.");
            e("Sammelboot", "Collector boat"); e("Fischt Treibgut beim Überfahren ein (60 Vol.).", "Scoops up flotsam as it drives over it (60 vol.).");
            // ------------------------------------------------ Gebäude
            e("Förderband", "Conveyor belt"); e("Verbindet Anlagen mit dem Stützpunkt-Lager. Transportiert echtes Material.", "Connects machines to the base storage. Carries real material.");
            e("Logistik", "Logistics"); e("Sortieranlage", "Sorting plant"); e("Sortiert unsortiertes Material (1,5 Einheiten/s bei voller Energie).", "Sorts unsorted material (1.5 units/s at full power).");
            e("Verarbeitung", "Processing"); e("Ballenpresse", "Baler");
            e("Presst 10 sortierte Einheiten zu einem Ballen (alle 5 s). Ballen erzielen 30 % mehr.", "Presses 10 sorted units into a bale (every 5 s). Bales sell for 30 % more.");
            e("Lagerhalle", "Warehouse"); e("+300 Lagerkapazität am Stützpunkt.", "+300 storage capacity at the base."); e("Solarfeld", "Solar field"); e("+4 Energie für Anlagen.", "+4 energy for machines.");
            e("Recycling-Generator", "Recycling generator"); e("+10 Energie. Verfügbar nach Energieversorgung auf PYRA.", "+10 energy. Available after Power Supply on PYRA.");
            e("Drohnenhangar", "Drone hangar"); e("Zwei Sammeldrohnen holen leichte Objekte im Einsatzradius und bringen sie ins Lager.", "Two collector drones fetch light objects within their radius and bring them to storage.");
            e("Schnellladestation", "Fast charging station"); e("Lädt MIKO am Stützpunkt dreimal so schnell.", "Charges MIKO three times as fast at the base.");
            e("Laterne", "Lantern"); e("Warmes Licht für den Stützpunkt.", "Warm light for the base."); e("Deko", "Decoration"); e("Parkbank", "Park bench");
            e("Ein Platz zum Ausruhen – auch für Roboter.", "A place to rest – robots too."); e("Blumenbeet", "Flower bed"); e("Kleines Beet aus recyceltem Kunststoff.", "Small bed made of recycled plastic.");
            e("Energie", "Energy");
            // ------------------------------------------------ Planeten
            e("Die vergessene Erde", "The forgotten Earth"); e("Goldenes Licht über verlassenen Hochhäusern, Einkaufsstraßen und überwucherten Parks.", "Golden light over abandoned high-rises, shopping streets and overgrown parks.");
            e("Von Anfang an verfügbar.", "Available from the start."); e("Setzling pflanzen", "Plant a seedling"); e("Straßenlaterne reparieren", "Repair street lamp");
            e("Goldene Melancholie: verlassene Straßen im Abendlicht, warme Hoffnung.", "Golden melancholy: deserted streets in the evening light, warm hope.");
            e("Staubsturm", "Dust storm"); e("Unterstand", "Shelter"); e("Wohnviertel", "Residential quarter"); e("Einkaufszentrum", "Shopping centre"); e("Botanischer Bezirk", "Botanical district");
            e("Reihenhäuser, Spielplatz und die alte Buslinie 7.", "Terraced houses, a playground and the old bus line 7."); e("Die Konsum-Meile von KONSUMA mit ihren Parkdecks.", "KONSUMA's shopping mile with its car parks.");
            e("Parks und das zentrale Gewächshaus.", "Parks and the central greenhouse.");
            e("Eine Barrikade aus Einkaufswagen blockiert die Straße. Ein Magnetarm zieht sie heraus.", "A barricade of shopping carts blocks the street. A magnet arm pulls them out.");
            e("Ein umgestürzter Bus blockiert den Weg. Hebe ihn mit dem Kran an und transportiere ihn ab.", "An overturned bus blocks the way. Lift it with the crane and haul it away.");
            e("Die rostrote Industriewelt", "The rust-red industrial world"); e("Roter Wüstensand, verlassene Fabriken, Schrottschluchten und gigantische Förderanlagen.", "Red desert sand, abandoned factories, scrap canyons and gigantic conveyor systems.");
            e("Staubbinder-Kaktus pflanzen", "Plant a dust-binding cactus"); e("Förderknoten reparieren", "Repair conveyor node");
            e("Rau und dramatisch: Hitze, Rost, heulender Wüstenwind und stampfende Maschinen.", "Rough and dramatic: heat, rust, howling desert wind and pounding machines.");
            e("Sandsturm", "Sandstorm"); e("Felsnische", "Rock niche"); e("Schrottmarkt", "Scrap market"); e("Fabrikgürtel", "Factory belt"); e("Gießerei", "Foundry");
            e("Der alte Umschlagplatz für Altmetall.", "The old trading yard for scrap metal."); e("Endlose Hallen und Förderanlagen.", "Endless halls and conveyor systems.");
            e("Hochöfen, Schlackenfelder und das geplante Recyclingwerk.", "Blast furnaces, slag fields and the planned recycling plant.");
            e("Verkeilte Stahlträger. Starker Magnet (Stufe 2) oder Schneidgerät nötig.", "Jammed steel girders. Strong magnet (level 2) or cutter needed.");
            e("Ein verkeiltes Fabrikfahrzeug versperrt das Tor zur Gießerei. Kran und Transporter!", "A jammed factory vehicle blocks the gate to the foundry. Crane and transporter!");
            e("Der vermüllte Ozeanplanet", "The littered ocean planet"); e("Türkisfarbene Lagunen, Inselstädte, überflutete Straßen und schwimmende Müllinseln.", "Turquoise lagoons, island towns, flooded streets and floating garbage islands.");
            e("Riffmodul setzen", "Place a reef module"); e("Leuchtboje reparieren", "Repair light buoy");
            e("Weit und atmend: Meeresrauschen, Möwenwind, schwebende Weite – und dunkle Tiefe.", "Wide and breathing: the sound of the sea, gull wind, floating vastness – and dark depths.");
            e("Seesturm", "Sea storm"); e("Bootshaus", "Boathouse"); e("Hafen", "Harbour"); e("Küstensiedlung", "Coastal settlement"); e("Lagune", "Lagoon");
            e("Kräne, Kaimauern und ein verstopftes Hafenbecken.", "Cranes, quay walls and a clogged harbour basin."); e("Stelzenhäuser auf kleinen Inseln.", "Stilt houses on small islands.");
            e("Das tiefe Herz von Pelagia mit versunkenen Ruinen.", "The deep heart of Pelagia with sunken ruins.");
            e("Riesige Geisternetze sperren die Durchfahrt. Das Schneidgerät zerteilt sie.", "Huge ghost nets block the passage. The cutter cuts them apart.");
            e("Gesunkene Wrackteile blockieren den Kanal. Tauchen und mit starkem Magnet (Stufe 2) bergen.", "Sunken wreck parts block the canal. Dive and recover them with a strong magnet (level 2).");
            e("Die eingefrorene Zukunft", "The frozen future"); e("Blaue Eislandschaften, stillgelegte Forschungsstädte, Raumhäfen und Polarlichter.", "Blue ice landscapes, shut-down research towns, spaceports and auroras.");
            e("Finale: Großprojekte auf TERRA, PYRA und PELAGIA abschließen und den Sprungantrieb einbauen.", "Finale: complete the grand projects on TERRA, PYRA and PELAGIA and install the jump drive.");
            e("Flechtenkultur ansiedeln", "Settle a lichen culture"); e("Wärmeknoten reparieren", "Repair heat node");
            e("Eisig und erhaben: Polarlicht, klirrende Stille, Schneestürme – das Finale.", "Icy and sublime: aurora, crisp silence, snowstorms – the finale.");
            e("Schneesturm", "Snowstorm"); e("Iglu-Station", "Igloo station"); e("Forschungsviertel", "Research quarter"); e("Rechenzentrum", "Data centre"); e("Raumhafen", "Spaceport");
            e("Labore unter Kuppeln aus Glas.", "Laboratories under glass domes."); e("Die Server, die das Programm ZWEITE CHANCE berechneten.", "The servers that computed the SECOND CHANCE programme.");
            e("Von hier startete die letzte Arche.", "The last ark launched from here.");
            e("Eingefrorene Maschinen blockieren die Straße. Erst auftauen (Wärmemodul), dann zerlegen.", "Frozen machines block the street. Thaw them first (heat module), then cut them up.");
            e("Ein abgestürztes Shuttle versperrt den Raumhafen. Kran und Transporter!", "A crashed shuttle blocks the spaceport. Crane and transporter!");
            e("Sturm", "Storm"); e("Unterschlupf", "Shelter");
            // ------------------------------------------------ Projekte
            e("Licht für das Wohnviertel", "Light for the residential quarter"); e("Straßenbeleuchtung und Buslinie 7 wieder in Betrieb nehmen.", "Bring the street lighting and bus line 7 back into service.");
            e("Wasserkreislauf reaktivieren", "Reactivate the water cycle"); e("Pumpen, Brunnen und Leitungen des Einkaufszentrums reparieren.", "Repair the pumps, fountains and pipes of the shopping centre.");
            e("Zentrales Gewächshaus", "Central greenhouse"); e("Das große Gewächshaus mit den versiegelten Samen wiederaufbauen. GROSSPROJEKT", "Rebuild the great greenhouse with the sealed seeds. GRAND PROJECT");
            e("Handelsposten reaktivieren", "Reactivate the trading post"); e("Der alte Schrottmarkt kauft wieder an: +15 % auf Verkäufe auf PYRA.", "The old scrap market buys again: +15 % on sales on PYRA.");
            e("Energieversorgung", "Power supply"); e("Windturbinen und Kupferleitungen: +15 Energie auf PYRA, Recycling-Generator baubar.", "Wind turbines and copper lines: +15 energy on PYRA, recycling generator can be built.");
            e("Recyclingwerk", "Recycling plant"); e("Die Gießerei wird zum Recyclingwerk: +20 % auf Ballen überall. GROSSPROJEKT", "The foundry becomes a recycling plant: +20 % on bales everywhere. GRAND PROJECT");
            e("Hafenbecken und Kaimauer", "Harbour basin and quay wall"); e("Hafenbecken säubern und die Kaimauer abdichten.", "Clean the harbour basin and seal the quay wall.");
            e("Filterstationen", "Filter stations"); e("Drei Filterstationen reinigen das Küstenwasser.", "Three filter stations clean the coastal water.");
            e("Wasserreinigung und Riff", "Water purification and reef"); e("Die Lagune wird gereinigt, das Riff kann wieder wachsen. GROSSPROJEKT", "The lagoon is cleaned, the reef can grow again. GRAND PROJECT");
            e("Laborheizung", "Laboratory heating"); e("Die Kuppellabore werden wieder warm.", "The dome labs become warm again.");
            e("Die Server des Programms ZWEITE CHANCE laufen wieder an.", "The servers of the SECOND CHANCE programme start up again.");
            e("Wärme- und Energienetz", "Heat and power grid"); e("Das Netz verbindet den Raumhafen – und sendet das Signal an die Arche. GROSSPROJEKT", "The grid connects the spaceport – and sends the signal to the ark. GRAND PROJECT");
            // ------------------------------------------------ Aufträge
            e("Aufwachen", "Waking up"); e("Fahre ein Stück vom Stützpunkt weg.", "Drive a little way away from the base.");
            e("Ein Dach über dem Kopf", "A roof over your head"); e("Nachts und bei Stürmen brauchst du einen Unterschlupf. Finde einen (Symbol auf der Karte) oder baue einen Notunterschlupf.", "At night and in storms you need shelter. Find one (symbol on the map) or build an emergency shelter.");
            e("Erste Handgriffe", "First steps"); e("Sammle 5 Müllobjekte mit dem Greifarm auf.", "Pick up 5 pieces of trash with the gripper arm.");
            e("Der erste Lichtpunkt", "The first light point"); e("Räume den Lichtpunkt westlich des Stützpunkts komplett auf – dann geht dort das Licht an.", "Clear the light point west of the base completely – then the lights come on there.");
            e("Erster Verkauf", "First sale"); e("Bringe deine Ladung zum Stützpunkt und verkaufe sie am Verkaufsterminal.", "Bring your load to the base and sell it at the sales terminal.");
            e("Besser werden", "Getting better"); e("Kaufe in der Werkstatt dein erstes Upgrade.", "Buy your first upgrade in the workshop.");
            e("Sortieren lohnt sich", "Sorting pays off"); e("Lagere Müll ein und sortiere 10 Einheiten am Sortiertisch.", "Store trash and sort 10 units at the sorting table.");
            e("Busroute freilegen", "Clear the bus route"); e("Räume die Haltestelle der Linie 7 frei.", "Clear the line 7 bus stop.");
            e("Scherben bringen Glück", "Shards bring luck"); e("Sammle 40 Einheiten Glas.", "Collect 40 units of glass."); e("Es werde Licht", "Let there be light");
            e("Repariere 5 Straßenlaternen.", "Repair 5 street lamps."); e("Werkstatt bergen", "Recover the workshop"); e("Räume die alte Werkstatt im Einkaufszentrum aus.", "Clear out the old workshop in the shopping centre.");
            e("Farbeimer entsorgen", "Dispose of paint buckets"); e("Entsorge 6 Einheiten Gefahrstoffe fachgerecht.", "Properly dispose of 6 units of hazardous waste.");
            e("Kupferfieber", "Copper fever"); e("Sammle 60 Einheiten Kupfer.", "Collect 60 units of copper."); e("Ballen für den Markt", "Bales for the market"); e("Verkaufe 10 Ballen.", "Sell 10 bales.");
            e("Das Band läuft", "The belt is running"); e("Repariere 6 Förderknoten.", "Repair 6 conveyor nodes."); e("Gefahrgut sichern", "Secure hazardous goods");
            e("Entsorge 30 Einheiten Gefahrstoffe.", "Dispose of 30 units of hazardous waste."); e("Schwarzes Wasser", "Black water"); e("Beseitige 8 Ölteppiche.", "Remove 8 oil slicks.");
            e("Netze kappen", "Cut the nets"); e("Sammle 80 Einheiten Netze.", "Collect 80 units of nets."); e("Hafenbecken reinigen", "Clean the harbour basin");
            e("Räume das innere Hafenbecken auf.", "Clean up the inner harbour basin."); e("Leuchtbojen", "Light buoys"); e("Repariere 6 Leuchtbojen.", "Repair 6 light buoys.");
            e("Eingefrorene Akkus sichern", "Secure frozen batteries"); e("Taue 15 eingefrorene Objekte auf und berge sie.", "Thaw and recover 15 frozen objects.");
            e("Seltene Schätze", "Rare treasures"); e("Sammle 30 Einheiten seltene Metalle.", "Collect 30 units of rare metals."); e("Warme Knoten", "Warm nodes");
            e("Repariere 6 Wärmeknoten.", "Repair 6 heat nodes."); e("Das Archiv", "The archive"); e("Finde alle Fundstücke auf NIVALIS.", "Find all finds on NIVALIS.");
            // ------------------------------------------------ Fundstücke
            e("KONSUMA-Werbetafel", "KONSUMA billboard");
            e("„Warum reparieren? NEU ist besser! KONSUMA liefert alles in 12 Minuten. Alles. Sofort. Immer neu.“ – Darunter hat jemand mit Kreide geschrieben: „Und wohin mit dem Alten?“",
              "“Why repair? NEW is better! KONSUMA delivers everything in 12 minutes. Everything. Now. Always new.” – Underneath, someone has written in chalk: “And where does the old stuff go?”");
            e("Notiz einer Busfahrerin", "A bus driver's note");
            e("Letzte Fahrt der Linie 7. Die Straßen sind zu, der Bus bleibt hier. Morgen bringen uns die Shuttles zur Arche HORIZONT. „Nur fünf Jahre“, sagen sie. Ich lasse den Schlüssel stecken. Für wen auch immer.",
              "Last run of line 7. The streets are blocked, the bus stays here. Tomorrow the shuttles take us to the ark HORIZON. “Only five years,” they say. I'm leaving the key in. For whoever.");
            e("Kinderzeichnung", "Child's drawing"); e("Ein krakeliger Baum mit einer Sonne. Darunter: „Wenn wir zurückkommen, ist er riesengroß. – Lina, 7 Jahre“", "A scribbled tree with a sun. Underneath: “When we come back, it will be huge. – Lina, age 7”");
            e("Tagebuch der Gärtnerin", "The gardener's diary");
            e("Ich habe die letzten Samen im zentralen Gewächshaus versiegelt. Tomaten, Linden, Mohn. Irgendwer wird sie finden. Vielleicht kein Mensch. Das wäre auch in Ordnung.",
              "I sealed the last seeds in the central greenhouse. Tomatoes, lindens, poppies. Someone will find them. Maybe not a human. That would be all right too.");
            e("Schichtplan der Gießerei", "Foundry shift schedule");
            e("Quartalsziel: +18 %. Nächstes Quartal: +22 %. Unter der Tabelle, klein gedruckt: „Recycling-Anteil: 0 %. Begründung: nicht wirtschaftlich.“",
              "Quarterly target: +18 %. Next quarter: +22 %. Below the table, in small print: “Recycling share: 0 %. Reason: not economical.”");
            e("Warnschild am Tor", "Warning sign at the gate"); e("GEFAHRSTOFFLAGER VOLL. Neue Fässer bitte VOR dem Tor abstellen. – Die Verwaltung", "HAZMAT STORE FULL. Please leave new barrels OUTSIDE the gate. – The management");
            e("Sprachmemo eines Ingenieurs", "An engineer's voice memo");
            e("„Ich hatte die Pläne für ein Recyclingwerk fertig. Alles durchgerechnet. Der Vorstand hat gelacht. Jetzt stehen wir am Raumhafen, und die Berge sind höher als die Hochöfen.“",
              "“I had the plans for a recycling plant ready. All calculated. The board laughed. Now we're standing at the spaceport, and the heaps are higher than the blast furnaces.”");
            e("Sandverwehte Postkarte", "Sand-blown postcard"); e("„Grüße von PYRA! Die Kakteenfelder blühen gerade, rot und orange, so weit man sehen kann.“ – Das Datum ist fünfzig Jahre alt.",
              "“Greetings from PYRA! The cactus fields are in bloom right now, red and orange as far as the eye can see.” – The date is fifty years old.");
            e("Hafenlogbuch", "Harbour log"); e("Tag 212: Wieder drei Netze über Bord. Kosten für die Entsorgung an Land: zu hoch. Das Meer ist groß, sagt der Kapitän.",
              "Day 212: Three more nets overboard. Cost of disposal on land: too high. The sea is big, says the captain.");
            e("Flaschenpost", "Message in a bottle");
            e("„An wen auch immer: Wir gehen heute an Bord. Ich lasse diese Flasche hier, damit das Meer wenigstens eine gute Nachricht trägt: Wir haben es verstanden. Zu spät, aber verstanden.“",
              "“To whoever: We're going on board today. I'm leaving this bottle here so that the sea carries at least one piece of good news: We understood. Too late, but we understood.”");
            e("Forschungsboje B-7", "Research buoy B-7"); e("Messreihe Riff Süd: Lebende Korallen 71 % … 44 % … 12 % … 0 %. Letzter Eintrag: „Die Filterstationen hätten gereicht.“",
              "Measurement series, south reef: living corals 71 % … 44 % … 12 % … 0 %. Last entry: “The filter stations would have been enough.”");
            e("Kinderbuch „Der kleine Wal“", "Children's book “The Little Whale”");
            e("„… und als das Wasser wieder klar war, kam der kleine Wal zurück in die Lagune und sang das schönste Lied, das die Insel je gehört hatte.“",
              "“… and when the water was clear again, the little whale came back to the lagoon and sang the most beautiful song the island had ever heard.”");
            e("Serverprotokoll: ZWEITE CHANCE", "Server log: SECOND CHANCE");
            e("PROGRAMM ZWEITE CHANCE. Bedingung: Wiederherstellung der vier Kolonialwelten. Sensor: Lebenszeichen, Wasserqualität, Energienetz. Aktion: Signal an Arche-Flotte. Rückkehr freigegeben.",
              "PROGRAMME SECOND CHANCE. Condition: restoration of the four colony worlds. Sensor: signs of life, water quality, power grid. Action: signal to the ark fleet. Return approved.");
            e("Anzeigetafel Raumhafen", "Spaceport departure board"); e("ARCHE HORIZONT – ABFLUG 06:40 – GATE 3 – PÜNKTLICH. Darunter blinkt seit Jahrzehnten: „Rückflug: unbekannt.“",
              "ARK HORIZON – DEPARTURE 06:40 – GATE 3 – ON TIME. Below it, blinking for decades: “Return flight: unknown.”");
            e("Brief der Direktorin", "The director's letter");
            e("„Wir haben die Roboter zurückgelassen, weil wir uns selbst nicht zugetraut haben, aufzuräumen. Ich hoffe, einer von ihnen gibt nicht auf. Wenn du das liest, kleiner Freund: danke.“",
              "“We left the robots behind because we didn't trust ourselves to clean up. I hope one of them doesn't give up. If you're reading this, little friend: thank you.”");
            e("Tauwasser-Messung", "Meltwater measurement");
            e("Eisdicke rückläufig seit Wiederinbetriebnahme der Wärmeknoten. Erste Flechten auf der Südseite der Kuppel B. Ein grüner Punkt in all dem Blau.",
              "Ice thickness decreasing since the heat nodes were restarted. First lichens on the south side of dome B. A green dot in all that blue.");
            // ------------------------------------------------ Kosmetik
            e("Türkis (Original)", "Turquoise (original)"); e("Standard", "Default"); e("Salbeigrün", "Sage green"); e("Großprojekt auf TERRA", "Grand project on TERRA");
            e("Wüstensand", "Desert sand"); e("Großprojekt auf PYRA", "Grand project on PYRA"); e("Koralle", "Coral"); e("Großprojekt auf PELAGIA", "Grand project on PELAGIA");
            e("Nachtblau", "Midnight blue"); e("Großprojekt auf NIVALIS", "Grand project on NIVALIS"); e("Sonnengelb", "Sun yellow"); e("Kampagne abgeschlossen", "Campaign completed");
            e("Orange (Original)", "Orange (original)"); e("Weiß", "White"); e("Tutorial abgeschlossen", "Tutorial completed"); e("Magenta", "Magenta");
            e("Alle Fundstücke auf TERRA", "All finds on TERRA"); e("Kein Aufkleber", "No sticker"); e("Stern", "Star"); e("Erstes Upgrade", "First upgrade");
            e("Auftrag „Das Band läuft“", "Mission “The belt is running”"); e("Welle", "Wave"); e("Schneeflocke", "Snowflake"); e("Herz", "Heart"); e("Kein Anbauteil", "No attachment");
            e("Antenne", "Antenna"); e("Auftrag „Es werde Licht“", "Mission “Let there be light”"); e("Blume", "Flower"); e("Muschel", "Shell"); e("Auftrag „Leuchtbojen“", "Mission “Light buoys”");
            e("Strickmütze", "Knitted hat"); e("Auftrag „Warme Knoten“", "Mission “Warm nodes”"); e("Fähnchen", "Little flag");
            // ------------------------------------------------ Stufen, Schiff
            e("Transportschiff (TERRA, PYRA, PELAGIA)", "Transport ship (TERRA, PYRA, PELAGIA)"); e("Sprungantrieb (NIVALIS)", "Jump drive (NIVALIS)");
            e("Zugang versperrt", "Access blocked"); e("Zugang frei", "Access open"); e("Hauptmüll entfernt", "Main trash removed"); e("Infrastruktur repariert", "Infrastructure repaired");
            e("Ökologie wiederhergestellt", "Ecology restored");
        }
    }
}
