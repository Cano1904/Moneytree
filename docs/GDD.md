# RE:PLANET – Eine zweite Chance · Game-Design-Dokument

Stand: aus dem aktuellen Code abgeleitet (`Core/Data/GameData.cs`, `Core/Sim/*`, `Core/World/*`, `Runtime/*`).
Genaue Zahlen (Preise, Kosten, Müllmengen) stehen in der automatisch erzeugten [WIRTSCHAFT.md](WIRTSCHAFT.md).

## 1. Vision

Ein ruhiges, warmes 3D-Aufbauspiel über Aufräumen, Reparieren und Wiederbeleben. Der kleine Recyclingroboter **MIKO**
verwandelt vermüllte, verlassene Welten Schritt für Schritt zurück in Orte, an denen wieder Licht brennt, Wasser fließt
und etwas wächst. Jede Handlung ist sichtbar: Müll verschwindet wirklich, Material landet wirklich im Lager, Straßen
leuchten wieder auf. Kein Kampf, kein Zeitdruck außer Nacht und Wetter – dafür das Gefühl, etwas Kaputtes heil zu machen.
Allein oder mit bis zu drei Freunden.

**Ton:** goldene Melancholie, die in Hoffnung umschlägt. Eigene Figuren, eigene Welt und eigene Namen
(siehe [LIZENZEN.md](LIZENZEN.md)).

## 2. Die Spielschleife

```
entdecken → sammeln (Greifarm, Sauger, Magnet, Schneidgerät, Kran …) → zum Stützpunkt bringen
   → einlagern → sortieren → zu Ballen pressen → verkaufen / Aufträge erfüllen
   → Upgrades, Fahrzeuge, Gebäude kaufen → schwierigeren Müll erreichen
   → Bereich zu 85 % reinigen → Großprojekt bauen → „die Stadt erwacht“ → Ökologie ansiedeln
   → nächster Bereich / nächster Planet
```

- **Wert steigt mit Verarbeitung:** unsortiert 0,5×, sortiert 1,0×, als Ballen 1,3× des Materialpreises.
  Einkaufen kostet 2× – Kauf-und-Verkauf-Schleifen lohnen sich nicht (per Test abgesichert).
- **Behälter als Engpass:** MIKO trägt einen sichtbaren Rückenbehälter (12 bis 65 Volumen, dazu Anhänger und Presse).
- **Energie als zweiter Engpass:** Werkzeuge, Sprinten und Fahren kosten Akku. Laden am Stützpunkt, beim Schlafen
  oder langsam im Stillstand bei Tageslicht. Leerer Akku ist keine Sackgasse: Der Greifarm funktioniert im Notbetrieb.
- **Gefahrstoffe** (Farbeimer, Batterien, Kältemittel, Fässer, Altöl) brauchen den Gefahrgutbehälter und werden an der
  Entsorgungsstation gegen einen Bonus abgegeben statt verkauft.

## 3. Planeten

Drei **Startplaneten** sind von Anfang an frei wählbar und jederzeit mit dem Transportschiff erreichbar. **NIVALIS** ist
das Finale: Es öffnet sich, wenn die Großprojekte der drei Startplaneten abgeschlossen sind und der Sprungantrieb
eingebaut ist.

| Planet | Untertitel | Stimmung | Besonderheit | Sturm / Unterschlupf |
| --- | --- | --- | --- | --- |
| **TERRA** | Die vergessene Erde | Goldenes Licht über verlassenen Hochhäusern, Einkaufsstraßen und Parks | Haushaltsmüll, Autowracks, Straßenlaternen reparieren | Staubsturm / Unterstand |
| **PYRA** | Die rostrote Industriewelt | Roter Wüstensand, Fabriken, Schrottschluchten | Schwerer Industrieschrott, häufige Sandstürme verschieben die **Dünen** und öffnen neue Wege | Sandsturm / Felsnische |
| **PELAGIA** | Der vermüllte Ozeanplanet | Türkise Lagunen, Inselstädte, schwimmende Müllinseln | **Schwimmen und Tauchen** (Tauchmodul), Ölteppiche (Filtermodul), Sammelboot | Seesturm / Bootshaus |
| **NIVALIS** | Die eingefrorene Zukunft | Eis, Forschungskuppeln, Raumhafen, Polarlicht | **Kälte** erhöht den Energieverbrauch (Isolation), **eingefrorene** Objekte (Wärmemodul) | Schneesturm / Iglu-Station |

### Bereiche

Jeder Planet hat drei Bereiche, getrennt durch **Sperren**, die ein bestimmtes Werkzeug verlangen:

| Planet | Bereiche | Sperre 1 → 2 | Sperre 2 → 3 |
| --- | --- | --- | --- |
| TERRA | Wohnviertel · Einkaufszentrum · Botanischer Bezirk | Barrikade aus 6 Einkaufswagen (Magnetarm) | umgestürzter Stadtbus (Kran + Transport) |
| PYRA | Schrottmarkt · Fabrikgürtel · Gießerei | 5 verkeilte Stahlträger (Magnet Stufe 2 oder Schneidgerät) | verkeiltes Fabrikfahrzeug (Kran + Transporter) |
| PELAGIA | Hafen · Küstensiedlung · Lagune | 4 Geisternetze (Schneidgerät) | 3 gesunkene Wrackteile (Tauchen + Magnet Stufe 2) |
| NIVALIS | Forschungsviertel · Rechenzentrum · Raumhafen | 4 eingefrorene Maschinen (auftauen, dann zerlegen) | abgestürztes Shuttle (Kran + Transporter) |

Dazu je Planet: 9 **Lichtpunkte** (kleine Zonen, die komplett geräumt „aufleuchten“), 10 Reparaturpunkte, 12 Plätze für
ökologische Aktionen, 4 Fundstücke, 9 vorhandene Unterschlüpfe und Aussichtspunkte für den Fotomodus.

### Wiederherstellungsstufen (je Bereich)

| Stufe | Name | Bedingung |
| --- | --- | --- |
| 0 | Zugang versperrt | Sperre zum Bereich noch nicht entfernt |
| 1 | Zugang frei | Sperre entfernt |
| 2 | Hauptmüll entfernt | mindestens 85 % des Mülls (gewichtet nach Materialeinheiten) entfernt |
| 3 | Infrastruktur repariert | Projekt des Bereichs abgeschlossen |
| 4 | Ökologie wiederhergestellt | alle ökologischen Plätze des Bereichs bepflanzt und ausgewachsen |

Der Wiederherstellungsgrad eines Planeten setzt sich je Bereich aus Sauberkeit (40 %), Projekt (35 %) und Ökologie (25 %) zusammen.

## 4. Werkzeuge und Module

Alle Upgrades werden in der Werkstatt gekauft und gelten für das ganze Team.

| Werkzeug / Modul | Funktion |
| --- | --- |
| **Greifarm** | Hebt einzelne Objekte bis zur Tragkraft (3 → 6 → 12 kg). Immer vorhanden, funktioniert auch bei leerem Akku. |
| **Müllsauger** | Saugt leichte Objekte (bis 1 kg) im Kegel vor MIKO ein (3–8 Objekte/s, 4–8 m). |
| **Magnetarm** | Zieht Metall an. **Magnetwelle:** aufladen und loslassen – holt alle erreichbaren Metallteile im vorderen Halbkreis auf einmal (7/11/15 m, 6/12/20 Teile; weniger bei kurzer Ladung). Kupfer ist nicht magnetisch. |
| **Schneidgerät** | Zerlegt Großes (Kühlschränke, Motorblöcke, Wracks, Netze, Serverschränke) in tragbare Teile. |
| **Müllpresse** | Presst pressbares Material im Behälter auf das halbe Volumen. |
| **Wärmemodul** | Taut eingefrorene Objekte auf (NIVALIS). |
| **Filtermodul** | Reinigt Ölteppiche (PELAGIA). |
| **Bio-Modul** | Ökologische Aktionen: Setzlinge, Staubbinder-Kakteen, Riffmodule, Flechtenkulturen. |
| **Tauchmodul** | Druckfeste Hülle: tauchen und unter Wasser sammeln (PELAGIA). |
| **Gefahrgutbehälter** | Erlaubt Gefahrstoffe der Klasse 1 bzw. 1–2. |
| Behälter, Anhänger, Akku, Energieeffizienz, Isolation, Drohnentechnik | Kapazität, Reichweite und Verbrauch |

Jede Ablehnung erklärt sich selbst („Zu schwer für den Greifarm (8 kg > 6 kg). Schneidgerät zerlegt es.“).

## 5. Fahrzeuge

| Fahrzeug | Rolle |
| --- | --- |
| **Transportrover** | schneller Transporter mit 40 Volumen Ladefläche und Ansaugschacht; trägt auch Wracks vom Kran |
| **Kranfahrzeug** | hebt schwere Wracks (Autos, Busse, Fabrikfahrzeuge, Shuttle) an und setzt sie auf den Rover oder am Stützpunkt ab, wo sie verwertet werden; Mitspieler können beim Anheben helfen |
| **Sammelboot** | nur PELAGIA: fischt Treibgut beim Überfahren ein (60 Volumen) |

Fahrzeuge lassen sich jederzeit zurücksetzen (getragene Last bleibt am alten Ort liegen). Das Transportschiff verbindet
die Planeten; der **Sprungantrieb** ist die teuerste Einzelinvestition und Voraussetzung für NIVALIS.

## 6. Stützpunkt und Basisbau

Jeder Planet hat einen Stützpunkt mit Lager, Ladeplatz, Verkaufsterminal, Werkstatt, Sortiertisch, Materialhändler,
Entsorgungsstation, Auftragstafel, Garage, Abladeplatz für Lieferungen und Landeplatz des Transportschiffs. In der **Bauansicht** werden Gebäude auf einem Raster platziert,
gedreht, verschoben oder abgerissen:

- **Förderbänder** verbinden Anlagen mit dem Stützpunkt – nur verbundene Anlagen arbeiten.
- **Sortieranlage** und **Ballenpresse** automatisieren die Verarbeitung im Lager.
- **Lagerhallen** erhöhen die Lagerkapazität, **Solarfelder** und **Recycling-Generatoren** liefern Energie;
  bei Energiemangel arbeiten alle Anlagen anteilig langsamer.
- **Drohnenhangars** schicken zwei Sammeldrohnen los, die leichte Objekte im Umkreis ins Lager bringen.
- **Schnellladestation** (dreifaches Ladetempo), dazu Deko: Laternen, Parkbänke, Blumenbeete.

Das Lager gehört zum jeweiligen Planeten; Credits sind eine gemeinsame Kasse.

## 7. Großprojekte und „Die Stadt erwacht“

Pro Bereich ein Projekt (Credits + Material aus dem Planetenlager), gebaut am Projektplatz. Das dritte Projekt jedes
Planeten ist ein **Großprojekt**:

- TERRA: Licht für das Wohnviertel → Wasserkreislauf → **Zentrales Gewächshaus**
- PYRA: Handelsposten (+15 % auf Verkäufe auf PYRA) → Energieversorgung → **Recyclingwerk** (+20 % auf Ballen überall)
- PELAGIA: Hafenbecken und Kaimauer → Filterstationen → **Wasserreinigung und Riff**
- NIVALIS: Laborheizung → Rechenzentrum → **Wärme- und Energienetz** (sendet das Signal an die Arche)

Ist ein Projekt fertig, **erwacht** der Bereich: Fenster und Laternen schalten sich nacheinander ein, Brunnen laufen,
über dem Projektplatz steigt ein Feuerwerk auf. Projekte bringen zusätzliche Energie, schalten Gebäude frei und machen
die ökologischen Aktionen des Bereichs möglich. Großprojekte schalten Kosmetik frei.

## 8. Tag und Nacht, Wind und Stürme

- **Tageslänge** je Planet 12–16 Minuten Spielzeit (NIVALIS 720 s, PYRA 780 s, TERRA 840 s, PELAGIA 960 s).
  Etwa 40 % davon sind Nacht.
- **Wind** weht ständig mit Böen, nachts stärker; **Stürme** kommen regelmäßig (je nach Planet alle 5,5–8 Minuten,
  70–85 s lang) und werden 30 s vorher angekündigt.
- **Ungeschützt** in Nacht oder Sturm verliert MIKO Energie (nachts 0,55, im Sturm 0,9 Energie pro Sekunde, beides addiert sich).
  Schutz bieten der Stützpunkt, vorhandene Unterschlüpfe im Gelände (Symbol auf der Karte) und selbst gebaute
  **Notunterschlüpfe** (80 Credits, höchstens 8 pro Planet). Im Fahrzeug ist man ebenfalls geschützt.
- **Hineinfahren:** In den **Hangar** des Hauptquartiers (Rolltor öffnet sich bei Annäherung, nachts und bei Sturm
  schon ab 13 m) und in den **Laderaum des Transportschiffs** (Heckrampe) kann MIKO zu Fuß oder mit dem Rover hineinfahren.
  Drinnen ist man vor Nacht und Sturm geschützt, kann schlafen und im Hangar laden – gesammelt wird drinnen nichts.
  Wer nicht hineinfahren will, muss nicht: die übrigen Unterschlüpfe gelten weiter.
- **Schlafen** geht nachts oder im Sturm in einem Unterschlupf. Schlafen alle verbundenen Spieler, wird die Nacht
  übersprungen bzw. der Sturm beendet – Akku voll, Spielstand gesichert.
- **Notabschaltung:** Ist der Akku ungeschützt leer, schaltet MIKO ab; nach wenigen Sekunden bringt eine Abschleppdrohne
  ihn mit 40 % Akku zum Ladeplatz. Allein im Spiel vergeht dabei die Nacht. Keine Strafe außer Zeit und Weg.
- Auf **PYRA** verschieben Sandstürme die Dünen: Nach jedem Sturm sind andere Wege offen.

## 9. Aufträge und Nebeninhalte

- **Einführung** auf TERRA (7 Schritte): Aufwachen, erste Handgriffe, erster Lichtpunkt, erster Verkauf, Unterschlupf,
  erstes Upgrade, Sortieren.
- **Nebenaufträge** je Planet (Material sammeln, Ballen verkaufen, Reparaturen, Gefahrstoffe entsorgen, Ölteppiche,
  Auftauen, Archiv) mit Credits und teils Kosmetik als Belohnung.
- **Recyclingaufträge** an der Auftragstafel (endlos, 20 % über dem Verkaufspreis) und kostenlose **Schrottlieferungen**
  am Abladeplatz sorgen dafür, dass Projektmaterial nie ausgeht.
- **Fundstücke** (16 Stück, 4 je Planet) erzählen in kurzen Texten, wie es zur Vermüllung kam – im Archiv nachlesbar.
- **Kosmetik** ohne Spielvorteil: Farben, Akzentfarben, Aufkleber und Anbauteile (z. B. Antenne, Blume, Strickmütze).

## 10. Geschichte

**Intro (ca. 100 s Echtzeit-Zwischensequenz, überspringbar).** Die Handlung:

1. *Es gab einmal eine Welt, die alles hatte. Und alles, was sie hatte, warf sie fort.* – Skyline aus Müllwürfel-Türmen.
2. *KONSUMA versprach uns das Glück: „Alles. Sofort. Immer neu.“ … bis der Müll unsere Städte überragte.* – Megastore.
3. *Dann bauten wir Archen. „Nur für fünf Jahre“, sagten sie. Zurück blieben die Maschinen – auf vier Welten.* – Start der Archen.
4. *Aus fünf Jahren wurden fünfzig. Eine Maschine nach der anderen verstummte.* – Roboterreihen schalten ab.
5. *Nur eine nicht. MIKO. Jeden Morgen. Würfel für Würfel.* – MIKOs Zuhause in einem rostigen Lieferwagen.
6. *Bis MIKO etwas fand, das längst verloren war: einen Keimling.*
7. *Ein altes Signal erwachte: PROGRAMM ZWEITE CHANCE. Wenn das Leben zurückkehrt … kehren auch wir zurück.*

Erzählerstimme: optional über Sprachaufnahmen in `Resources/Voice/` (Skript, Zeiten und Regie: `docs/SPRECHERTEXT.md`);
ohne Aufnahmen laufen Untertitel.

**Im Spiel** erzählen die Fundstücke vom Konsumrausch (KONSUMA-Werbetafeln, Schichtpläne mit „Recycling-Anteil 0 %“),
vom Aufbruch zur **Arche HORIZONT** und von Menschen, die es besser wussten. Auf NIVALIS stehen die Server des
**Programms ZWEITE CHANCE**: Bedingung für die Rückkehr ist die Wiederherstellung der vier Welten.

**Ende:** Mit dem vierten Großprojekt ist die Bedingung erfüllt – die Arche HORIZONT kehrt zurück („Danke, MIKO.“).
Danach geht das Spiel als **freies Spiel** weiter; Aufträge und Lieferungen bleiben verfügbar.

## 11. Koop

1–4 Spieler in einer gemeinsamen, serverautoritativen Welt: Der Host öffnet seine Welt per Einladung
(`IP:Port/CODE`), späte Beitritte sind jederzeit möglich, alternativ läuft ein dedizierter .NET-Server.
Gemeinsame Kasse, gemeinsame Upgrades, gemeinsames Anheben von Wracks, gemeinsames Schlafen; teure Käufe, Abriss und
Reisen sind dem Host vorbehalten, sofern er den Vertrauensmodus nicht einschaltet. Details: [KOOP.md](KOOP.md).

## 12. Speichern

- Vier Spielstände (automatisch + 3 manuelle) und Schnellspeichern; Spielstanddateien lassen sich exportieren und importieren.
- Versioniertes Textformat mit Prüfsumme; ältere Stände werden migriert; beim Schreiben wird probegelesen und der vorherige
  gültige Stand als Sicherung behalten.
- Die Welten entstehen deterministisch aus festen Seeds; gespeichert werden nur Abweichungen (entfernte Objekte als Bitset,
  neue Objekte, Lager, Gebäude, Projekte, Begrünung, Wetter …). Deshalb kann der Fotomodus jederzeit den Ausgangszustand zeigen.
- Automatisches Speichern bei wichtigen Ereignissen und alle zwei Minuten Spielzeit.

## 13. Fotomodus

Freie Kamera in der Nähe des Roboters, Sichtfeld, Neigung, Belichtung, HUD aus, **Vorher-Ansicht** (Ausgangszustand mit
allem Müll), **Hochformat 9:16** für kurze Videos, Sprung zu Aussichtspunkten. Fotos werden als PNG (1920×1080 bzw.
1080×1920) gespeichert.

## 14. Barrierefreiheit und Komfort

- Frei belegbare Tasten, Controller-Unterstützung (XInput), Halten **oder** Umschalten für Werkzeug-Aktionen.
- Materialien unterscheiden sich durch **Form und Symbol**, nicht nur durch Farbe.
- Untertitel, Textgröße (0,8–1,6), Kamerawackeln abschaltbar, Option für weniger Lichtblitze, Option für hohen Kontrast.
- Getrennte Lautstärken (Gesamt, Musik, Effekte, Umgebung, Oberfläche, Stimme), Stummschalten im Hintergrund.
- Maus- und Controller-Empfindlichkeit, Y-Achse umkehrbar, Sichtfeld, Helligkeit, Render-Skalierung, Qualitätsstufen.
- Solo-Pause bei offenen Menüs; verständliche Begründung bei jeder abgelehnten Aktion; Notabschaltung statt Game Over.
- Sprache: Deutsch und Englisch (vollständig; Einstellungen › Sonstiges). Beim ersten Start Englisch, wenn das System englisch ist.
- Bauansicht vollständig mit dem Controller: Cursor mit dem linken Stick, A platzieren, Y drehen, LB/RB Bauwerk, LT/RT Kategorie,
  X umsetzen, Back (zweimal) abreißen, B abbrechen; Tastensymbole passen sich dem Gerät an.
- Leistungsanzeige (F3): Bildrate aktuell/Minimum der letzten 5 s, Bildzeit, Draw-Calls, Qualitätsstufe, Auflösung.

## 15. Präsentation

- Grafik vollständig prozedural: Gelände als Höhenfeld mit Verschmutzung, die beim Aufräumen abnimmt, und Bodenbewuchs,
  der mit der Ökologie wächst; Müll per GPU-Instancing; eigener Himmels-Shader mit Wolken, Sternen, Himmelskörpern und
  Polarlicht; Wetterpartikel; animiertes Wasser.
- Audio vollständig prozedural: Effekte, planetenspezifische Musik (eigene Tonleitern je Planet), Intro-Score.
- **Stadtklänge:** Mit der Wiederherstellung kehren Vögel (auf PELAGIA Möwen), Blätterrauschen, plätschernde Brunnen und – wenn
  die Stadt erwacht – fernes Stadtleben mit Verkehr, Straßenbahn und Stimmen zurück.
- **Radio (Taste T, Spielmenü › Radio):** Stücke aus der Spielmusik in eigenen Fassungen; neue Stücke für jeden gereinigten
  Bereich und jedes Großprojekt (gespeichert im Spielstand), kurze synthetische Senderkennungen zwischen den Stücken,
  ein Sender für alles und einer je Planet.
- **Erzähler im Spiel:** 21 kurze Sätze des Erzählers zu besonderen Momenten (erste Landung, erste Nacht, erster Sturm,
  Planet vollendet, Mitspieler …), jeder nur einmal pro Spielstand; abschaltbar („Erzähler im Spiel“).
