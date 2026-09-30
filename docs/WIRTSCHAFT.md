# RE:PLANET – Wirtschaftstabellen

> Automatisch erzeugt aus `Core/Data/GameData.cs`, `Core/Sim/Rules.cs` und `Core/World/WorldGen.cs`
> mit `dotnet run --project Tools/DocGen` (aus dem Repository-Wurzelordner). **Nicht von Hand bearbeiten** –
> nach Balancing-Änderungen einfach neu erzeugen. Alle Preise in Credits, ohne Boni, sofern nicht anders angegeben.

Inhalt: [Grundwerte](#grundwerte) · [Materialien](#materialien) · [Upgrades](#upgrades-werkstatt) · [Fahrzeuge](#fahrzeuge) ·
[Gebäude](#gebäude-stützpunkt) · [Großprojekte](#großprojekte) · [Kleine Kosten](#reparaturen-ökologie-unterschlupf) ·
[Aufträge](#aufträge-missionen) · [Recyclingaufträge](#recyclingaufträge-auftragstafel) · [Lieferungen](#schrottlieferungen) ·
[Müll je Planet](#müll-je-planet-und-bereich)

## Grundwerte

| Wert | Einstellung |
| --- | ---: |
| Startguthaben | 60 Credits |
| Verkauf unsortiert (direkt aus dem Behälter oder Lager) | 0,5 × Materialpreis |
| Verkauf sortiert | 1 × Materialpreis |
| Verkauf als Ballen (10 sortierte Einheiten) | 1,3 × Materialpreis je Einheit |
| Einkauf beim Materialhändler | 2 × Materialpreis (aufgerundet) |
| Recyclingauftrag (Auftragstafel) | 1,2 × Materialpreis (aufgerundet) |
| Lagerkapazität am Stützpunkt (Grundwert) | 400 Einheiten |
| Grundenergie für Anlagen | 6 E |
| Bereich gilt als „Hauptmüll entfernt“ ab | 85 % (Objekte gewichtet nach Materialeinheiten, Sperren zählen nicht) |
| Koop: Gäste brauchen Host-Freigabe ab | 800 Credits (außer im Vertrauensmodus) |
| Spieler je Sitzung | 4 |
| Handelsposten auf PYRA (Projekt pyra_p1) | +15 % auf Verkäufe, solange man sich auf PYRA befindet |
| Recyclingwerk (Projekt pyra_p3) | +20 % auf Ballen-Verkäufe auf allen Planeten |
| Gefahrstoffe/Altöl | nicht verkäuflich; Entsorgungsstation zahlt einen Bonus je Einheit |

## Materialien

Form und Symbol unterscheiden die Materialien zusätzlich zur Farbe (Barrierefreiheit).

| Material | Symbol | Pressbar | Unsortiert/E | Sortiert/E | Ballen (10 E) | je E im Ballen | Einkauf/E | Entsorgung/E |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Papier | ▤ | ja | 1 | 3 | 39 | 3,9 | 6 | – |
| Glas | ◯ | nein | 2 | 4 | – | – | 8 | – |
| Kunststoff | △ | ja | 1 | 3 | 39 | 3,9 | 6 | – |
| Metall | ⬡ | ja | 3 | 6 | 78 | 7,8 | 12 | – |
| Stahl | ▬ | ja | 3 | 7 | 91 | 9,1 | 14 | – |
| Kupfer | ◎ | ja | 6 | 12 | 156 | 15,6 | 24 | – |
| Elektronik | ▦ | nein | 7 | 14 | – | – | 28 | – |
| Netze & Seile | # | ja | 2 | 4 | 52 | 5,2 | 8 | – |
| Akkus | ▮ | nein | 8 | 16 | – | – | 32 | – |
| Seltene Metalle | ◆ | nein | 15 | 30 | – | – | 60 | – |
| Gefahrstoffe | ⚠ | nein | – | – | – | – | – | 10 |
| Altöl | ● | nein | – | – | – | – | – | 6 |

Ballen presst die **Ballenpresse** (Gebäude) nur aus pressbaren Materialien. Die **Müllpresse** (Upgrade) halbiert dagegen
nur das Volumen im Behälter und ändert den Preis nicht.

## Upgrades (Werkstatt)

Upgrades gelten für das ganze Team (Koop) und alle Planeten. Stufe 0 ist der Startzustand, „Summe“ die kumulierten Kosten.

| Upgrade | Kategorie | Stufe | Kosten | Summe | Wert | Kaufbar |
| --- | --- | ---: | ---: | ---: | --- | --- |
| **Müllbehälter** | Behälter | 0 | Start | – | 12 Vol. | sofort |
|  |  | 1 | 180 | 180 | 20 Vol. |  |
|  |  | 2 | 320 | 500 | 30 Vol. |  |
|  |  | 3 | 900 | 1.400 | 45 Vol. |  |
|  |  | 4 | 2.200 | 3.600 | 65 Vol. |  |
| **Greifarm** | Werkzeug | 0 | Start | – | 3 kg | sofort |
|  |  | 1 | 250 | 250 | 6 kg |  |
|  |  | 2 | 900 | 1.150 | 12 kg |  |
| **Müllsauger** | Werkzeug | 0 | Start | – | nicht vorhanden | sofort |
|  |  | 1 | 200 | 200 | 3 Obj./s, 4 m |  |
|  |  | 2 | 600 | 800 | 5 Obj./s, 6 m |  |
|  |  | 3 | 1.600 | 2.400 | 8 Obj./s, 8 m |  |
| **Magnetarm** | Werkzeug | 0 | Start | – | nicht vorhanden | sofort |
|  |  | 1 | 420 | 420 | 7 m, 6 Teile |  |
|  |  | 2 | 1.400 | 1.820 | 11 m, 12 Teile |  |
|  |  | 3 | 3.400 | 5.220 | 15 m, 20 Teile |  |
| **Schneidgerät** | Werkzeug | 0 | Start | – | nicht vorhanden | sofort |
|  |  | 1 | 1.100 | 1.100 | Tempo 1,0× |  |
|  |  | 2 | 3.000 | 4.100 | Tempo 1,8× |  |
| **Müllpresse** | Werkzeug | 0 | Start | – | keine Presse | sofort |
|  |  | 1 | 1.300 | 1.300 | Volumen ×0,5 |  |
| **Akku** | Energie | 0 | Start | – | 100 E | sofort |
|  |  | 1 | 280 | 280 | 150 E |  |
|  |  | 2 | 1.000 | 1.280 | 220 E |  |
|  |  | 3 | 2.600 | 3.880 | 320 E |  |
| **Energieeffizienz** | Energie | 0 | Start | – | 100 % | sofort |
|  |  | 1 | 700 | 700 | 80 % |  |
|  |  | 2 | 2.000 | 2.700 | 65 % |  |
| **Gefahrgutbehälter** | Behälter | 0 | Start | – | keine | sofort |
|  |  | 1 | 400 | 400 | Klasse 1 |  |
|  |  | 2 | 1.500 | 1.900 | Klasse 1–2 |  |
| **Anhänger** | Behälter | 0 | Start | – | kein Anhänger | sofort |
|  |  | 1 | 1.600 | 1.600 | +25 Vol. |  |
| **Bio-Modul** | Werkzeug | 0 | Start | – | nicht vorhanden | sofort |
|  |  | 1 | 700 | 700 | vorhanden |  |
| **Wärmemodul** | Werkzeug | 0 | Start | – | nicht vorhanden | nach Freischaltung von NIVALIS |
|  |  | 1 | 2.400 | 2.400 | Tempo 1,0× |  |
|  |  | 2 | 5.200 | 7.600 | Tempo 2,0× |  |
| **Isolation** | Energie | 0 | Start | – | ×1,6 bei Kälte | nach Freischaltung von NIVALIS |
|  |  | 1 | 1.800 | 1.800 | ×1,1 bei Kälte |  |
| **Tauchmodul** | Werkzeug | 0 | Start | – | nur schwimmen | sofort (gedacht für PELAGIA) |
|  |  | 1 | 2.600 | 2.600 | tauchen bis 14 m |  |
| **Filtermodul** | Werkzeug | 0 | Start | – | nicht vorhanden | sofort (gedacht für PELAGIA) |
|  |  | 1 | 2.000 | 2.000 | Rate 1,0× |  |
|  |  | 2 | 4.200 | 6.200 | Rate 2,0× |  |
| **Drohnentechnik** | Automatisierung | 0 | Start | – | 35 m, 3 Vol. | sofort |
|  |  | 1 | 1.800 | 1.800 | 50 m, 5 Vol. |  |
|  |  | 2 | 4.500 | 6.300 | 70 m, 8 Vol. |  |

Alle Upgrades zusammen: **53.050 Credits**.

Beschreibungen:

- **Müllbehälter** (Kapazität): Mehr Volumen im sichtbaren Rückenbehälter.
- **Greifarm** (Tragkraft): Hebt einzelne Objekte. Stärke = maximale Masse.
- **Müllsauger** (Saugrate): Saugt leichte Objekte (bis 1 kg) im Kegel vor MIKO ein.
- **Magnetarm** (Wellen-Reichweite): Zieht Metall an. Aufladen + Loslassen = Magnetwelle.
- **Schneidgerät** (Schnitttempo): Zerlegt große Objekte und Netze in tragbare Teile.
- **Müllpresse** (Pressfaktor): Presst Papier, Kunststoff, Metall, Stahl, Kupfer und Netze im Behälter auf halbes Volumen.
- **Akku** (Kapazität): Mehr Energie für Werkzeuge und Sprint.
- **Energieeffizienz** (Verbrauch): Geräte verbrauchen weniger Energie.
- **Gefahrgutbehälter** (Gefahrenklasse): Erlaubt sicheres Sammeln gekennzeichneter Gefahrstoffe.
- **Anhänger** (Zusatzvolumen): MIKO zieht einen kleinen Anhänger mit zusätzlichem Volumen.
- **Bio-Modul** (Modul): Pflanzt Setzlinge, setzt Riffmodule und Flechtenkulturen (ökologische Aktionen).
- **Wärmemodul** (Tautempo): Taut eingefrorene Objekte auf. Nötig auf NIVALIS.
- **Isolation** (Kälteverbrauch): Halbiert den Kälteverbrauch auf NIVALIS.
- **Tauchmodul** (Tauchen): Druckfeste Hülle: MIKO kann unter Wasser tauchen und sammeln.
- **Filtermodul** (Filterrate): Reinigt Ölteppiche und belastetes Wasser.
- **Drohnentechnik** (Einsatzradius): Verbessert Sammeldrohnen der Drohnenhangars (Radius und Kapazität).

## Fahrzeuge

| Fahrzeug | Kosten | Tempo (m/s) | Ladung (Vol.) | Planet | Beschreibung |
| --- | ---: | ---: | ---: | --- | --- |
| Transportrover | 2.200 | 14 | 40 | alle | Schneller Transporter mit Ladefläche (40 Vol.) und Ansaugschacht. Trägt auch Wracks vom Kran. |
| Kranfahrzeug | 3.600 | 7 | – | alle | Hebt schwere Wracks an und setzt sie auf den Transportrover oder am Stützpunkt ab. |
| Sammelboot | 4.800 | 12 | 60 | PELAGIA | Fischt Treibgut beim Überfahren ein (60 Vol.). |

| Raumschiff | Kosten | Wirkung |
| --- | ---: | --- |
| Transportschiff (TERRA, PYRA, PELAGIA) | vorhanden | Reisen zwischen den drei Startplaneten |
| Sprungantrieb (NIVALIS) | 9.000 | Freischaltung von NIVALIS (zusätzlich alle drei Großprojekte nötig) |

## Gebäude (Stützpunkt)

Anlagen (Maschinen) arbeiten nur, wenn sie über Förderbänder mit der Stützpunktkante verbunden sind. Reicht die Energie nicht,
arbeiten alle Anlagen anteilig langsamer (Wirkungsgrad = Angebot / Bedarf).

| Gebäude | Kategorie | Größe | Credits | Material | Materialwert | Energie | Max. | Voraussetzung | Wirkung |
| --- | --- | --- | ---: | --- | ---: | --- | ---: | --- | --- |
| Förderband | Logistik | 1×1 | 15 | 2 Metall | 12 | −0,2 | – | – | Verbindet Anlagen mit dem Stützpunkt-Lager. Transportiert echtes Material. |
| Sortieranlage | Verarbeitung | 3×2 | 350 | 15 Metall | 90 | −3 | 3 | – | Sortiert unsortiertes Material (1,5 Einheiten/s bei voller Energie). |
| Ballenpresse | Verarbeitung | 2×2 | 550 | 25 Metall | 150 | −4 | 3 | – | Presst 10 sortierte Einheiten zu einem Ballen (alle 5 s). Ballen erzielen 30 % mehr. |
| Lagerhalle | Logistik | 2×2 | 180 | 10 Metall | 60 | – | 6 | – | +300 Lagerkapazität am Stützpunkt. |
| Solarfeld | Energie | 2×2 | 220 | 10 Glas, 3 Elektronik | 82 | +4 | 8 | – | +4 Energie für Anlagen. |
| Recycling-Generator | Energie | 2×2 | 700 | 20 Stahl, 10 Kupfer | 260 | +10 | 4 | Projekt „Energieversorgung“ | +10 Energie. Verfügbar nach Energieversorgung auf PYRA. |
| Drohnenhangar | Automatisierung | 2×2 | 1.100 | 20 Metall, 10 Elektronik | 260 | −3 | 2 | – | Zwei Sammeldrohnen holen leichte Objekte im Einsatzradius und bringen sie ins Lager. |
| Schnellladestation | Energie | 1×1 | 260 | 4 Kupfer, 4 Metall | 72 | −1 | 2 | – | Lädt MIKO am Stützpunkt dreimal so schnell. |
| Laterne | Deko | 1×1 | 20 | 1 Metall, 1 Glas | 10 | – | – | – | Warmes Licht für den Stützpunkt. |
| Parkbank | Deko | 1×1 | 15 | 1 Metall | 6 | – | – | – | Ein Platz zum Ausruhen – auch für Roboter. |
| Blumenbeet | Deko | 1×1 | 25 | 2 Kunststoff | 6 | – | – | – | Kleines Beet aus recyceltem Kunststoff. |

## Großprojekte

Je Bereich ein Projekt. Start nur am Projektplatz, wenn der Bereich zu mindestens 85 % gereinigt ist.
Material kommt aus dem Lager des jeweiligen Planeten. „Materialwert“ = Wert des Materials bei sortiertem Verkauf.

### TERRA – Die vergessene Erde

| Bereich | Projekt | Credits | Material | Materialwert | Bauzeit (s) | Energie + | Voraussetzung | Wirkung |
| --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- |
| Wohnviertel | Licht für das Wohnviertel | 250 | 30 Glas, 25 Metall, 6 Elektronik | 354 | 15 | – | – | Straßenbeleuchtung und Buslinie 7 wieder in Betrieb nehmen. |
| Einkaufszentrum | Wasserkreislauf reaktivieren | 700 | 50 Metall, 40 Kunststoff, 12 Elektronik, 20 Glas | 668 | 20 | 3 | „Licht für das Wohnviertel“ | Pumpen, Brunnen und Leitungen des Einkaufszentrums reparieren. |
| Botanischer Bezirk | **Zentrales Gewächshaus** (Großprojekt) | 1.600 | 70 Glas, 60 Metall, 24 Elektronik, 30 Kunststoff | 1.066 | 30 | 4 | „Wasserkreislauf reaktivieren“ | Das große Gewächshaus mit den versiegelten Samen wiederaufbauen. |
| **Summe** |  | **2.550** |  | **2.088** |  |  |  |  |

### PYRA – Die rostrote Industriewelt

| Bereich | Projekt | Credits | Material | Materialwert | Bauzeit (s) | Energie + | Voraussetzung | Wirkung |
| --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- |
| Schrottmarkt | Handelsposten reaktivieren | 350 | 35 Stahl, 10 Kupfer, 10 Metall | 425 | 16 | – | – | Der alte Schrottmarkt kauft wieder an: +15 % auf Verkäufe auf PYRA. |
| Fabrikgürtel | Energieversorgung | 1.000 | 70 Stahl, 30 Kupfer, 12 Elektronik | 1.018 | 22 | 15 | „Handelsposten reaktivieren“ | Windturbinen und Kupferleitungen: +15 Energie auf PYRA, Recycling-Generator baubar. |
| Gießerei | **Recyclingwerk** (Großprojekt) | 2.400 | 140 Stahl, 55 Kupfer, 28 Elektronik, 20 Metall | 2.152 | 30 | 10 | „Energieversorgung“ | Die Gießerei wird zum Recyclingwerk: +20 % auf Ballen überall. |
| **Summe** |  | **3.750** |  | **3.595** |  |  |  |  |

### PELAGIA – Der vermüllte Ozeanplanet

| Bereich | Projekt | Credits | Material | Materialwert | Bauzeit (s) | Energie + | Voraussetzung | Wirkung |
| --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- |
| Hafen | Hafenbecken und Kaimauer | 400 | 40 Kunststoff, 25 Stahl | 295 | 18 | – | – | Hafenbecken säubern und die Kaimauer abdichten. |
| Küstensiedlung | Filterstationen | 1.200 | 40 Netze & Seile, 20 Elektronik, 30 Stahl | 650 | 22 | 5 | „Hafenbecken und Kaimauer“ | Drei Filterstationen reinigen das Küstenwasser. |
| Lagune | **Wasserreinigung und Riff** (Großprojekt) | 2.800 | 70 Kunststoff, 55 Netze & Seile, 35 Elektronik, 45 Stahl | 1.235 | 32 | 8 | „Filterstationen“ | Die Lagune wird gereinigt, das Riff kann wieder wachsen. |
| **Summe** |  | **4.400** |  | **2.180** |  |  |  |  |

### NIVALIS – Die eingefrorene Zukunft

| Bereich | Projekt | Credits | Material | Materialwert | Bauzeit (s) | Energie + | Voraussetzung | Wirkung |
| --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- |
| Forschungsviertel | Laborheizung | 3.200 | 40 Elektronik, 30 Metall, 20 Akkus | 1.060 | 20 | – | – | Die Kuppellabore werden wieder warm. |
| Rechenzentrum | Rechenzentrum | 6.500 | 80 Elektronik, 18 Seltene Metalle, 40 Kupfer | 2.140 | 26 | 6 | „Laborheizung“ | Die Server des Programms ZWEITE CHANCE laufen wieder an. |
| Raumhafen | **Wärme- und Energienetz** (Großprojekt) | 11.000 | 60 Akkus, 36 Seltene Metalle, 80 Metall, 50 Kupfer | 3.120 | 36 | 12 | „Rechenzentrum“ | Das Netz verbindet den Raumhafen – und sendet das Signal an die Arche. |
| **Summe** |  | **20.700** |  | **6.320** |  |  |  |  |

NIVALIS wird freigeschaltet, wenn die Großprojekte auf TERRA, PYRA und PELAGIA abgeschlossen sind und der Sprungantrieb
(9.000 Credits) eingebaut ist. Die Kampagne endet mit allen vier Großprojekten.

## Reparaturen, Ökologie, Unterschlupf

| Planet | Reparatur (Material aus dem Lager) | Belohnung | Reparaturpunkte | Ökologische Aktion | Pflanzplätze |
| --- | --- | ---: | ---: | --- | ---: |
| TERRA | Straßenlaterne reparieren: 2 Metall, 1 Glas | 15 | 10 | Setzling pflanzen (15 Credits, Bio-Modul, nach dem Projekt des Bereichs) | 12 |
| PYRA | Förderknoten reparieren: 3 Stahl, 1 Kupfer | 15 | 10 | Staubbinder-Kaktus pflanzen (15 Credits, Bio-Modul, nach dem Projekt des Bereichs) | 12 |
| PELAGIA | Leuchtboje reparieren: 3 Kunststoff, 1 Elektronik | 15 | 10 | Riffmodul setzen (15 Credits, Bio-Modul, nach dem Projekt des Bereichs) | 12 |
| NIVALIS | Wärmeknoten reparieren: 2 Kupfer, 1 Elektronik | 15 | 10 | Flechtenkultur ansiedeln (15 Credits, Bio-Modul, nach dem Projekt des Bereichs) | 12 |

| Sonstiges | Wert |
| --- | --- |
| Notunterschlupf bauen | 80 Credits (höchstens 8 je Planet, nicht am Stützpunkt, nicht im Wasser) |
| Wachstum einer Pflanzung | 90 s Spielzeit |
| Kran: Grundzeit zum Anheben | 4 s × max(1, Masse/90 kg); jeder helfende Mitspieler +75 % Tempo |

## Aufträge (Missionen)

| Planet | Art | Auftrag | Ziel | Menge | Belohnung | Kosmetik | Voraussetzung |
| --- | --- | --- | --- | ---: | ---: | --- | --- |
| TERRA | Einführung | Aufwachen | Fahre ein Stück vom Stützpunkt weg. | 8 | – | – | – |
| TERRA | Einführung | Ein Dach über dem Kopf | Nachts und bei Stürmen brauchst du einen Unterschlupf. Finde einen (Symbol auf der Karte) oder baue einen Notunterschlupf. | 1 | 30 | – | Erster Verkauf |
| TERRA | Einführung | Erste Handgriffe | Sammle 5 Müllobjekte mit dem Greifarm auf. | 5 | 10 | – | Aufwachen |
| TERRA | Einführung | Der erste Lichtpunkt | Räume den Lichtpunkt westlich des Stützpunkts komplett auf – dann geht dort das Licht an. | 1 | 25 | – | Erste Handgriffe |
| TERRA | Einführung | Erster Verkauf | Bringe deine Ladung zum Stützpunkt und verkaufe sie am Verkaufsterminal. | 1 | 15 | – | Der erste Lichtpunkt |
| TERRA | Einführung | Besser werden | Kaufe in der Werkstatt dein erstes Upgrade. | 1 | 20 | Stern | Erster Verkauf |
| TERRA | Einführung | Sortieren lohnt sich | Lagere Müll ein und sortiere 10 Einheiten am Sortiertisch. | 10 | 25 | – | Besser werden |
| TERRA | Nebenauftrag | Busroute freilegen | Räume die Haltestelle der Linie 7 frei. | 1 | 60 | – | – |
| TERRA | Nebenauftrag | Scherben bringen Glück | Sammle 40 Einheiten Glas. | 40 | 80 | – | – |
| TERRA | Nebenauftrag | Es werde Licht | Repariere 5 Straßenlaternen. | 5 | 120 | Antenne | – |
| TERRA | Nebenauftrag | Werkstatt bergen | Räume die alte Werkstatt im Einkaufszentrum aus. | 1 | 150 | – | – |
| TERRA | Nebenauftrag | Farbeimer entsorgen | Entsorge 6 Einheiten Gefahrstoffe fachgerecht. | 6 | 90 | – | – |
| PYRA | Nebenauftrag | Kupferfieber | Sammle 60 Einheiten Kupfer. | 60 | 300 | – | – |
| PYRA | Nebenauftrag | Ballen für den Markt | Verkaufe 10 Ballen. | 10 | 400 | – | – |
| PYRA | Nebenauftrag | Das Band läuft | Repariere 6 Förderknoten. | 6 | 450 | Zahnrad | – |
| PYRA | Nebenauftrag | Gefahrgut sichern | Entsorge 30 Einheiten Gefahrstoffe. | 30 | 500 | – | – |
| PELAGIA | Nebenauftrag | Schwarzes Wasser | Beseitige 8 Ölteppiche. | 8 | 700 | – | – |
| PELAGIA | Nebenauftrag | Netze kappen | Sammle 80 Einheiten Netze. | 80 | 600 | – | – |
| PELAGIA | Nebenauftrag | Hafenbecken reinigen | Räume das innere Hafenbecken auf. | 1 | 500 | – | – |
| PELAGIA | Nebenauftrag | Leuchtbojen | Repariere 6 Leuchtbojen. | 6 | 650 | Muschel | – |
| NIVALIS | Nebenauftrag | Eingefrorene Akkus sichern | Taue 15 eingefrorene Objekte auf und berge sie. | 15 | 900 | – | – |
| NIVALIS | Nebenauftrag | Seltene Schätze | Sammle 30 Einheiten seltene Metalle. | 30 | 1.200 | – | – |
| NIVALIS | Nebenauftrag | Warme Knoten | Repariere 6 Wärmeknoten. | 6 | 1.000 | Strickmütze | – |
| NIVALIS | Nebenauftrag | Das Archiv | Finde alle Fundstücke auf NIVALIS. | 4 | 800 | – | – |

Summe aller Auftragsbelohnungen: **8.625 Credits**.

## Recyclingaufträge (Auftragstafel)

Endlos wiederholbar; verlangt sortierte Einheiten aus dem Lager. Die Aufträge laufen reihum (hier die ersten sechs je Planet).

| Planet | Nr. | Material | Menge | Belohnung | zum Vergleich: sortiert verkauft |
| --- | ---: | --- | ---: | ---: | ---: |
| TERRA | 1 | Glas | 20 | 96 | 80 |
|  | 2 | Metall | 30 | 217 | 180 |
|  | 3 | Papier | 40 | 144 | 120 |
|  | 4 | Kunststoff | 20 | 72 | 60 |
|  | 5 | Glas | 30 | 144 | 120 |
|  | 6 | Metall | 40 | 288 | 240 |
| PYRA | 1 | Stahl | 20 | 168 | 140 |
|  | 2 | Kupfer | 30 | 433 | 360 |
|  | 3 | Metall | 40 | 288 | 240 |
|  | 4 | Stahl | 20 | 168 | 140 |
|  | 5 | Kupfer | 30 | 433 | 360 |
|  | 6 | Metall | 40 | 288 | 240 |
| PELAGIA | 1 | Kunststoff | 20 | 72 | 60 |
|  | 2 | Netze & Seile | 30 | 144 | 120 |
|  | 3 | Elektronik | 40 | 672 | 560 |
|  | 4 | Kunststoff | 20 | 72 | 60 |
|  | 5 | Netze & Seile | 30 | 144 | 120 |
|  | 6 | Elektronik | 40 | 672 | 560 |
| NIVALIS | 1 | Elektronik | 20 | 336 | 280 |
|  | 2 | Akkus | 30 | 576 | 480 |
|  | 3 | Kupfer | 40 | 576 | 480 |
|  | 4 | Elektronik | 20 | 336 | 280 |
|  | 5 | Akkus | 30 | 576 | 480 |
|  | 6 | Kupfer | 40 | 576 | 480 |

## Schrottlieferungen

Am Stützpunkt bestellbar (eine Lieferung gleichzeitig), je 14 Teile aus der Liste des Planeten, reihum gewählt. Jede Bestellung kostet eine Liefergebühr; danach startet der nächste Frachter frühestens nach 60 s Spielzeit (je Planet).

| Planet | Mögliche Teile | Ø Wert je Teil (sortiert) | Ø Wert je Lieferung | Gebühr |
| --- | --- | ---: | ---: | ---: |
| TERRA | Zeitungsbündel, Karton, Glasflasche, Plastikflasche, Getränkedose, Toaster, Rostiges Gartengerät, Alter Fernseher | 12,4 | 173 | 15 |
| PYRA | Schraubenhaufen, Zahnrad, Kupferkabeltrommel, Maschinenteil, Rohrstück, Stahlstück | 24,8 | 348 | 30 |
| PELAGIA | Plastikkanister, Treibende Plastikflasche, Kaputte Boje, Netzstück, Elektroschrott, Anker mit Kette | 14,8 | 208 | 20 |
| NIVALIS | Industrie-Akku, Platinenstapel, Kabeltrommel, Solarpanel-Bruch, Drohnenwrack, Seltenmetall-Kern | 35,3 | 495 | 45 |

## Müll je Planet und Bereich

Ausgangszustand der deterministisch erzeugten Welten (`WorldGen.Get(planet)`). „Einheiten“ = Materialeinheiten nach dem
Einlagern, „Wert“ = Verkaufswert dieser Einheiten sortiert (ohne Boni), „Entsorgung“ = Bonus der Entsorgungsstation für
Gefahrstoffe/Altöl. Sperren (Zugänge zum nächsten Bereich) sind enthalten und in der Spalte „Sperre“ gezählt.
Zerlegen (Schneidgerät) ändert die Summe nicht wesentlich; Lieferungen und Zerlegeteile kommen im Spiel hinzu.

### TERRA – Die vergessene Erde

**Bereich 1: Wohnviertel** – Reihenhäuser, Spielplatz und die alte Buslinie 7.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Getränkedose | 30 | – | 1 | 30 | 180 | – | Greifarm, Sauger, Magnet |
| Einkaufswagen | 6 | 6 | 5 | 30 | 180 | – | Magnet |
| Alter Fernseher | 4 | – | 5 | 20 | 156 | – | Greifarm St. 1 |
| Toaster | 6 | – | 3 | 18 | 156 | – | Greifarm, Magnet |
| Karton | 20 | – | 2 | 40 | 120 | – | Greifarm |
| Glasflasche | 30 | – | 1 | 30 | 120 | – | Greifarm |
| Plastikflasche | 34 | – | 1 | 34 | 102 | – | Greifarm, Sauger |
| Zeitungsbündel | 34 | – | 1 | 34 | 102 | – | Greifarm, Sauger |
| Altbatterie | 6 | – | 1 | 6 | 96 | – | Greifarm, Gefahrgut Kl. 1 |
| Plastiktüte | 26 | – | 1 | 26 | 78 | – | Greifarm, Sauger |
| Kunststoff-Blumentopf | 10 | – | 1 | 10 | 30 | – | Greifarm, Sauger |
| Alter Farbeimer | 4 | – | 1 | 4 | 0 | 40 | Greifarm, Gefahrgut Kl. 1 |
| **Summe** | **210** | 6 |  | **282** | **1.320** | **40** |  |

**Bereich 2: Einkaufszentrum** – Die Konsum-Meile von KONSUMA mit ihren Parkdecks.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Autowrack | 4 | – | 22 | 88 | 588 | – | Kran, Schneidgerät |
| Mikrowelle | 10 | – | 7 | 70 | 560 | – | Greifarm St. 2, Schneidgerät |
| Kühlschrank | 6 | – | 11 | 66 | 408 | 60 | Schneidgerät |
| Umgestürzter Stadtbus | 1 | 1 | 62 | 62 | 400 | – | Kran |
| Alter Fernseher | 10 | – | 5 | 50 | 390 | – | Greifarm St. 1 |
| Toaster | 10 | – | 3 | 30 | 260 | – | Greifarm, Magnet |
| Einkaufswagen | 8 | – | 5 | 40 | 240 | – | Magnet |
| Getränkedose | 34 | – | 1 | 34 | 204 | – | Greifarm, Sauger, Magnet |
| Karton | 30 | – | 2 | 60 | 180 | – | Greifarm |
| Altbatterie | 10 | – | 1 | 10 | 160 | – | Greifarm, Gefahrgut Kl. 1 |
| Glasflasche | 34 | – | 1 | 34 | 136 | – | Greifarm |
| Plastikflasche | 36 | – | 1 | 36 | 108 | – | Greifarm, Sauger |
| Plastiktüte | 30 | – | 1 | 30 | 90 | – | Greifarm, Sauger |
| Zeitungsbündel | 24 | – | 1 | 24 | 72 | – | Greifarm, Sauger |
| Alter Farbeimer | 6 | – | 1 | 6 | 0 | 60 | Greifarm, Gefahrgut Kl. 1 |
| **Summe** | **253** | 1 |  | **640** | **3.796** | **120** |  |

**Bereich 3: Botanischer Bezirk** – Parks und das zentrale Gewächshaus.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Autowrack | 3 | – | 22 | 66 | 441 | – | Kran, Schneidgerät |
| Mikrowelle | 6 | – | 7 | 42 | 336 | – | Greifarm St. 2, Schneidgerät |
| Kühlschrank | 4 | – | 11 | 44 | 272 | 40 | Schneidgerät |
| Rostiges Gartengerät | 20 | – | 2 | 40 | 240 | – | Greifarm, Magnet |
| Alter Fernseher | 6 | – | 5 | 30 | 234 | – | Greifarm St. 1 |
| Getränkedose | 26 | – | 1 | 26 | 156 | – | Greifarm, Sauger, Magnet |
| Glasflasche | 36 | – | 1 | 36 | 144 | – | Greifarm |
| Karton | 16 | – | 2 | 32 | 96 | – | Greifarm |
| Kunststoff-Blumentopf | 30 | – | 1 | 30 | 90 | – | Greifarm, Sauger |
| Plastikflasche | 30 | – | 1 | 30 | 90 | – | Greifarm, Sauger |
| Plastiktüte | 20 | – | 1 | 20 | 60 | – | Greifarm, Sauger |
| Zeitungsbündel | 16 | – | 1 | 16 | 48 | – | Greifarm, Sauger |
| Alter Farbeimer | 8 | – | 1 | 8 | 0 | 80 | Greifarm, Gefahrgut Kl. 1 |
| **Summe** | **221** | – |  | **420** | **2.207** | **120** |  |

**TERRA gesamt**

| Bereich | Objekte | Einheiten | Wert (sortiert) | Entsorgung | Projektkosten (Credits) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Wohnviertel | 210 | 282 | 1.320 | 40 | 250 |
| Einkaufszentrum | 253 | 640 | 3.796 | 120 | 700 |
| Botanischer Bezirk | 221 | 420 | 2.207 | 120 | 1.600 |
| **Summe** | **684** | **1.342** | **7.323** | **280** | **2.550** |

**Materialbilanz TERRA** (Ausgangsmüll vs. Bedarf der drei Projekte und aller Reparaturen auf diesem Planeten)

| Material | im Müll | Projekte | Reparaturen | Rest |
| --- | ---: | ---: | ---: | ---: |
| Papier | 206 | – | – | 206 |
| Glas | 187 | 120 | 10 | 57 |
| Kunststoff | 265 | 70 | – | 195 |
| Metall | 386 | 135 | 20 | 231 |
| Stahl | 138 | – | – | 138 |
| Elektronik | 116 | 42 | – | 74 |
| Akkus | 16 | – | – | 16 |
| Gefahrstoffe | 28 | – | – | 28 |

### PYRA – Die rostrote Industriewelt

**Bereich 1: Schrottmarkt** – Der alte Umschlagplatz für Altmetall.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Stahlträger | 13 | 5 | 8 | 104 | 728 | – | Magnet St. 2, Schneidgerät |
| Kupferkabeltrommel | 20 | – | 3 | 60 | 720 | – | Greifarm |
| Motorblock | 5 | – | 14 | 70 | 540 | – | Schneidgerät |
| Maschinenteil | 12 | – | 5 | 60 | 528 | – | Greifarm St. 1, Magnet |
| Rohrstück | 20 | – | 3 | 60 | 420 | – | Greifarm, Magnet |
| Zahnrad | 26 | – | 2 | 52 | 364 | – | Greifarm, Magnet |
| Schraubenhaufen | 40 | – | 1 | 40 | 240 | – | Greifarm, Sauger, Magnet |
| Getränkedose | 20 | – | 1 | 20 | 120 | – | Greifarm, Sauger, Magnet |
| Gefahrstofffass (gekennzeichnet) | 6 | – | 3 | 18 | 0 | 180 | Greifarm St. 1, Gefahrgut Kl. 2 |
| **Summe** | **162** | 5 |  | **484** | **3.660** | **180** |  |

**Bereich 2: Fabrikgürtel** – Endlose Hallen und Förderanlagen.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Fahrzeugwrack | 4 | – | 32 | 128 | 1.008 | – | Kran, Schneidgerät |
| Kupferkabeltrommel | 26 | – | 3 | 78 | 936 | – | Greifarm |
| Maschinenteil | 20 | – | 5 | 100 | 880 | – | Greifarm St. 1, Magnet |
| Motorblock | 8 | – | 14 | 112 | 864 | – | Schneidgerät |
| Stahlträger | 14 | – | 8 | 112 | 784 | – | Magnet St. 2, Schneidgerät |
| Rohrstück | 26 | – | 3 | 78 | 546 | – | Greifarm, Magnet |
| Verkeiltes Fabrikfahrzeug | 1 | 1 | 64 | 64 | 526 | – | Kran |
| Zahnrad | 26 | – | 2 | 52 | 364 | – | Greifarm, Magnet |
| Schraubenhaufen | 36 | – | 1 | 36 | 216 | – | Greifarm, Sauger, Magnet |
| Gefahrstofffass (gekennzeichnet) | 10 | – | 3 | 30 | 0 | 300 | Greifarm St. 1, Gefahrgut Kl. 2 |
| **Summe** | **171** | 1 |  | **790** | **6.124** | **300** |  |

**Bereich 3: Gießerei** – Hochöfen, Schlackenfelder und das geplante Recyclingwerk.

| Müllart | Anzahl | Sperre | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Fahrzeugwrack | 5 | – | 32 | 160 | 1.260 | – | Kran, Schneidgerät |
| Motorblock | 10 | – | 14 | 140 | 1.080 | – | Schneidgerät |
| Kupferkabeltrommel | 30 | – | 3 | 90 | 1.080 | – | Greifarm |
| Stahlträger | 16 | – | 8 | 128 | 896 | – | Magnet St. 2, Schneidgerät |
| Maschinenteil | 20 | – | 5 | 100 | 880 | – | Greifarm St. 1, Magnet |
| Rohrstück | 24 | – | 3 | 72 | 504 | – | Greifarm, Magnet |
| Zahnrad | 24 | – | 2 | 48 | 336 | – | Greifarm, Magnet |
| Schraubenhaufen | 30 | – | 1 | 30 | 180 | – | Greifarm, Sauger, Magnet |
| Gefahrstofffass (gekennzeichnet) | 12 | – | 3 | 36 | 0 | 360 | Greifarm St. 1, Gefahrgut Kl. 2 |
| **Summe** | **171** | – |  | **804** | **6.216** | **360** |  |

**PYRA gesamt**

| Bereich | Objekte | Einheiten | Wert (sortiert) | Entsorgung | Projektkosten (Credits) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Schrottmarkt | 162 | 484 | 3.660 | 180 | 350 |
| Fabrikgürtel | 171 | 790 | 6.124 | 300 | 1.000 |
| Gießerei | 171 | 804 | 6.216 | 360 | 2.400 |
| **Summe** | **504** | **2.078** | **16.000** | **840** | **3.750** |

**Materialbilanz PYRA** (Ausgangsmüll vs. Bedarf der drei Projekte und aller Reparaturen auf diesem Planeten)

| Material | im Müll | Projekte | Reparaturen | Rest |
| --- | ---: | ---: | ---: | ---: |
| Glas | 18 | – | – | 18 |
| Metall | 282 | 30 | – | 252 |
| Stahl | 1.248 | 245 | 30 | 973 |
| Kupfer | 372 | 95 | 10 | 267 |
| Elektronik | 74 | 40 | – | 34 |
| Gefahrstoffe | 84 | – | – | 84 |

### PELAGIA – Der vermüllte Ozeanplanet

**Bereich 1: Hafen** – Kräne, Kaimauern und ein verstopftes Hafenbecken.

| Müllart | Anzahl | Sperre | unter Wasser | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Elektroschrott | 14 | – | 2 | 2 | 28 | 392 | – | Greifarm |
| Geisternetz | 12 | 4 | – | 6 | 72 | 288 | – | Schneidgerät |
| Anker mit Kette | 8 | – | 3 | 5 | 40 | 280 | – | Greifarm St. 2, Magnet |
| Schiffsteil | 8 | – | – | 4 | 32 | 224 | – | Greifarm St. 1, Magnet St. 2 |
| Plastikkanister | 24 | – | – | 2 | 48 | 144 | – | Greifarm |
| Treibende Plastikflasche | 40 | – | – | 1 | 40 | 120 | – | Greifarm, Sauger |
| Kaputte Boje | 10 | – | – | 3 | 30 | 90 | – | Greifarm |
| Treibende Tüte | 30 | – | – | 1 | 30 | 90 | – | Greifarm, Sauger |
| Glasflasche | 16 | – | – | 1 | 16 | 64 | – | Greifarm |
| Ölteppich | 6 | – | – | 4 | 24 | 0 | 144 | Filtermodul |
| **Summe** | **168** | 4 | 5 |  | **360** | **1.692** | **144** |  |

**Bereich 2: Küstensiedlung** – Stelzenhäuser auf kleinen Inseln.

| Müllart | Anzahl | Sperre | unter Wasser | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Elektroschrott | 22 | – | 2 | 2 | 44 | 616 | – | Greifarm |
| Schiffsteil | 12 | – | 3 | 4 | 48 | 336 | – | Greifarm St. 1, Magnet St. 2 |
| Geisternetz | 12 | – | – | 6 | 72 | 288 | – | Schneidgerät |
| Anker mit Kette | 6 | – | 1 | 5 | 30 | 210 | – | Greifarm St. 2, Magnet |
| Gesunkenes Wrackteil | 3 | 3 | 3 | 10 | 30 | 210 | – | Magnet St. 2 |
| Plastikkanister | 26 | – | – | 2 | 52 | 156 | – | Greifarm |
| Treibende Plastikflasche | 40 | – | – | 1 | 40 | 120 | – | Greifarm, Sauger |
| Kaputte Boje | 12 | – | – | 3 | 36 | 108 | – | Greifarm |
| Treibende Tüte | 34 | – | – | 1 | 34 | 102 | – | Greifarm, Sauger |
| Glasflasche | 14 | – | – | 1 | 14 | 56 | – | Greifarm |
| Ölteppich | 8 | – | – | 4 | 32 | 0 | 192 | Filtermodul |
| **Summe** | **189** | 3 | 9 |  | **432** | **2.202** | **192** |  |

**Bereich 3: Lagune** – Das tiefe Herz von Pelagia mit versunkenen Ruinen.

| Müllart | Anzahl | Sperre | unter Wasser | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Elektroschrott | 34 | – | 32 | 2 | 68 | 952 | – | Greifarm |
| Schiffsteil | 18 | – | 16 | 4 | 72 | 504 | – | Greifarm St. 1, Magnet St. 2 |
| Geisternetz | 14 | – | – | 6 | 84 | 336 | – | Schneidgerät |
| Anker mit Kette | 8 | – | 8 | 5 | 40 | 280 | – | Greifarm St. 2, Magnet |
| Plastikkanister | 20 | – | – | 2 | 40 | 120 | – | Greifarm |
| Treibende Plastikflasche | 36 | – | – | 1 | 36 | 108 | – | Greifarm, Sauger |
| Kaputte Boje | 10 | – | – | 3 | 30 | 90 | – | Greifarm |
| Treibende Tüte | 30 | – | – | 1 | 30 | 90 | – | Greifarm, Sauger |
| Ölteppich | 12 | – | – | 4 | 48 | 0 | 288 | Filtermodul |
| **Summe** | **182** | – | 56 |  | **448** | **2.480** | **288** |  |

**PELAGIA gesamt**

| Bereich | Objekte | Einheiten | Wert (sortiert) | Entsorgung | Projektkosten (Credits) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Hafen | 168 | 360 | 1.692 | 144 | 400 |
| Küstensiedlung | 189 | 432 | 2.202 | 192 | 1.200 |
| Lagune | 182 | 448 | 2.480 | 288 | 2.800 |
| **Summe** | **539** | **1.240** | **6.374** | **624** | **4.400** |

**Materialbilanz PELAGIA** (Ausgangsmüll vs. Bedarf der drei Projekte und aller Reparaturen auf diesem Planeten)

| Material | im Müll | Projekte | Reparaturen | Rest |
| --- | ---: | ---: | ---: | ---: |
| Glas | 30 | – | – | 30 |
| Kunststoff | 446 | 110 | 30 | 306 |
| Stahl | 292 | 100 | – | 192 |
| Elektronik | 140 | 55 | 10 | 75 |
| Netze & Seile | 228 | 95 | – | 133 |
| Altöl | 104 | – | – | 104 |

### NIVALIS – Die eingefrorene Zukunft

**Bereich 1: Forschungsviertel** – Labore unter Kuppeln aus Glas.

| Müllart | Anzahl | Sperre | gefroren | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Industrie-Akku | 30 | – | 18 | 2 | 60 | 960 | – | Greifarm, Gefahrgut Kl. 1 |
| Platinenstapel | 30 | – | 5 | 2 | 60 | 840 | – | Greifarm |
| Eingefrorene Maschine | 10 | 4 | 10 | 9 | 90 | 760 | – | Schneidgerät |
| Drohnenwrack | 18 | – | 2 | 3 | 54 | 612 | – | Greifarm, Magnet |
| Kabeltrommel | 16 | – | 5 | 3 | 48 | 576 | – | Greifarm St. 1 |
| Serverschrank | 4 | – | 2 | 12 | 48 | 528 | – | Schneidgerät |
| Seltenmetall-Kern | 8 | – | 2 | 2 | 16 | 480 | – | Greifarm |
| Solarpanel-Bruch | 16 | – | 5 | 3 | 48 | 352 | – | Greifarm |
| **Summe** | **132** | 4 | 49 |  | **424** | **5.108** | – |  |

**Bereich 2: Rechenzentrum** – Die Server, die das Programm ZWEITE CHANCE berechneten.

| Müllart | Anzahl | Sperre | gefroren | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Serverschrank | 12 | – | 4 | 12 | 144 | 1.584 | – | Schneidgerät |
| Platinenstapel | 40 | – | 12 | 2 | 80 | 1.120 | – | Greifarm |
| Industrie-Akku | 30 | – | 17 | 2 | 60 | 960 | – | Greifarm, Gefahrgut Kl. 1 |
| Kabeltrommel | 20 | – | 4 | 3 | 60 | 720 | – | Greifarm St. 1 |
| Seltenmetall-Kern | 12 | – | 2 | 2 | 24 | 720 | – | Greifarm |
| Eingefrorene Maschine | 8 | – | 8 | 9 | 72 | 608 | – | Schneidgerät |
| Abgestürztes Shuttle | 1 | 1 | – | 62 | 62 | 584 | – | Kran |
| Drohnenwrack | 16 | – | 5 | 3 | 48 | 544 | – | Greifarm, Magnet |
| Solarpanel-Bruch | 14 | – | 5 | 3 | 42 | 308 | – | Greifarm |
| **Summe** | **153** | 1 | 57 |  | **592** | **7.148** | – |  |

**Bereich 3: Raumhafen** – Von hier startete die letzte Arche.

| Müllart | Anzahl | Sperre | gefroren | E je Objekt | Einheiten | Wert | Entsorgung | Werkzeug |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Industrie-Akku | 36 | – | 23 | 2 | 72 | 1.152 | – | Greifarm, Gefahrgut Kl. 1 |
| Seltenmetall-Kern | 18 | – | 4 | 2 | 36 | 1.080 | – | Greifarm |
| Drohnenwrack | 24 | – | 6 | 3 | 72 | 816 | – | Greifarm, Magnet |
| Serverschrank | 6 | – | – | 12 | 72 | 792 | – | Schneidgerät |
| Eingefrorene Maschine | 10 | – | 10 | 9 | 90 | 760 | – | Schneidgerät |
| Platinenstapel | 26 | – | 8 | 2 | 52 | 728 | – | Greifarm |
| Kabeltrommel | 20 | – | 7 | 3 | 60 | 720 | – | Greifarm St. 1 |
| Solarpanel-Bruch | 24 | – | 11 | 3 | 72 | 528 | – | Greifarm |
| **Summe** | **164** | – | 69 |  | **526** | **6.576** | – |  |

**NIVALIS gesamt**

| Bereich | Objekte | Einheiten | Wert (sortiert) | Entsorgung | Projektkosten (Credits) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Forschungsviertel | 132 | 424 | 5.108 | – | 3.200 |
| Rechenzentrum | 153 | 592 | 7.148 | – | 6.500 |
| Raumhafen | 164 | 526 | 6.576 | – | 11.000 |
| **Summe** | **449** | **1.542** | **18.832** | – | **20.700** |

**Materialbilanz NIVALIS** (Ausgangsmüll vs. Bedarf der drei Projekte und aller Reparaturen auf diesem Planeten)

| Material | im Müll | Projekte | Reparaturen | Rest |
| --- | ---: | ---: | ---: | ---: |
| Glas | 114 | – | – | 114 |
| Metall | 354 | 110 | – | 244 |
| Kupfer | 240 | 90 | 20 | 130 |
| Elektronik | 560 | 120 | 10 | 430 |
| Akkus | 192 | 80 | – | 112 |
| Seltene Metalle | 82 | 54 | – | 28 |

### Alle Planeten

| Planet | Objekte | Einheiten | Wert (sortiert) | Entsorgung |
| --- | ---: | ---: | ---: | ---: |
| TERRA | 684 | 1.342 | 7.323 | 280 |
| PYRA | 504 | 2.078 | 16.000 | 840 |
| PELAGIA | 539 | 1.240 | 6.374 | 624 |
| NIVALIS | 449 | 1.542 | 18.832 | – |

