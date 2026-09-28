# RE:PLANET – Sprechertext Intro und Abspann

Die Zwischensequenzen haben einen Erzähler. Das Spiel erzeugt **keine** Stimme selbst: Eine gute Erzählerstimme –
tief, rau, menschlich – entsteht mit einem echten Sprecher oder einem hochwertigen Stimmdienst deiner Wahl.
Das Spiel spielt die Aufnahmen ab, sobald sie im Projekt liegen, zeitgenau zur Musik. Fehlen sie, laufen die
Sequenzen wie bisher mit Untertiteln.

Technik: `RePlanet/Assets/RePlanet/Runtime/Audio/Narrator.cs` (Startzeiten, Ducking, Kino-Klang),
Untertiteltexte in `Core/Audio/Synth.cs` → `IntroTimeline.Shots[].Lines` (müssen zum Gesprochenen passen).

---

## 1. Die Stimme

| | |
|---|---|
| **Typ** | Männlicher Bassbariton, älter (50–70), rau, belegt, leicht heiser – ein Mann, der viel gesehen hat. Keine Trailerstimme, kein Pathos. |
| **Haltung** | Ruhig, nah, erzählend. Müde Hoffnung: Am Anfang Trauer und leise Bitterkeit, ab MIKO Wärme, am Ende ein kleines Leuchten. |
| **Tempo** | Langsam, ca. 110–120 Wörter pro Minute. Lieber zu langsam als zu schnell – die maximale Dauer lässt Luft. |
| **Nähe** | Nah am Mikrofon (15–20 cm), fast geflüstert gesprochen, aber mit Stimme (kein Hauchen). Der Bass kommt aus der Nähe. |
| **Pausen** | `…` im Text = hörbare Pause (ca. 0,6–0,8 s). `/` = kurzes Absetzen (ca. 0,3 s). Atmer dürfen bleiben – sie machen die Stimme menschlich. |
| **Betonung** | Unterstrichene Wörter (hier **fett**) tragen den Satz. Satzenden fallen ab, nie „fragend“ hochziehen. |
| **Namen** | MIKO = „MI-ko“ (Betonung vorn, kurzes i). KONSUMA = „kon-SU-ma“. HORIZONT = „ho-ri-ZONT“. |

---

## 2. Intro (100 Sekunden, 14 Zeilen)

Die Startzeit ist die Sekunde ab Beginn des Intros; die Datei wird **genau dann** gestartet.
Deshalb: **keine Stille am Dateianfang** (höchstens 50 ms), sonst kommt die Zeile zu spät.
Die maximale Dauer (einschließlich Nachklang) nicht überschreiten – danach folgt Musik, ein Bildwechsel oder die nächste Zeile.

| Datei | Start | max. Dauer | Bild | Text | Regie |
|---|---|---|---|---|---|
| `intro_01` | 0:01,5 | 5,0 s | Skyline aus Müllwürfel-Türmen im goldenen Gegenlicht | Es gab einmal eine Welt, / die **alles** hatte. | Wie der Anfang eines Märchens, das nicht gut ausgeht. Ruhig, fast zärtlich. |
| `intro_02` | 0:07,5 | 5,5 s | Kamera gleitet über die Müllhalden | Und alles, was sie hatte … warf sie **fort**. | Nach „hatte“ Pause. „fort“ leise, endgültig, fallend. |
| `intro_03` | 0:15,5 | 6,5 s | KONSUMA-Megastore, Leuchtreklame, Menschenmassen | KONSUMA versprach uns das **Glück**. … Alles. / Sofort. / Immer neu. | Der Slogan mit bitterer Ironie, jedes Wort einzeln abgesetzt, wie ein Echo aus der Werbung. |
| `intro_04` | 0:23,0 | 6,0 s | Kamera steigt auf: Müllberge überragen die Stadt | Wir kauften und kauften … bis der Müll unsere Städte **überragte**. | „kauften und kauften“ fließend, fast mechanisch; nach der Pause langsamer. |
| `intro_05` | 0:31,5 | 6,0 s | Die Archen starten (laute Musik) | Dann bauten wir **Archen**. … „Nur für fünf Jahre“, / sagten sie. | Etwas mehr Stimme (die Musik ist hier voll), aber nicht schreien. „sagten sie“ skeptisch. |
| `intro_06` | 0:38,5 | 6,5 s | Zurückgelassene Roboter sehen den Lichtspuren nach | Zurück blieben die **Maschinen**. / Auf vier Welten. / Um aufzuräumen. | Drei kurze Sätze, jeder mit Gewicht. Leichte Traurigkeit. |
| `intro_07` | 0:47,5 | 5,0 s | Stille. Reihen kleiner Roboter im Wind | Aus fünf Jahren … wurden **fünfzig**. | Sehr leise, viel Raum. „fünfzig“ fast geflüstert. |
| `intro_08` | 0:53,5 | 5,5 s | Die Augen der Roboter erlöschen nacheinander | Eine Maschine nach der anderen … **verstummte**. | Langsam. Das letzte Wort verklingen lassen. |
| `intro_09` | 1:01,5 | 5,5 s | Lieferwagen innen, Lichterkette, MIKO erwacht | Nur **eine** nicht. … Eine kleine, / sture Maschine. | Hier kippt der Ton: ein Hauch von Lächeln in der Stimme, Zuneigung. |
| `intro_10` | 1:08,0 | 6,0 s | MIKO presst Würfel und stapelt sie | **MIKO**. / Jeden Morgen. / Würfel für Würfel. | Warm, mit Rhythmus – wie ein Arbeitslied. „Würfel für Würfel“ ruhig und stolz. |
| `intro_11` | 1:16,5 | 5,5 s | MIKO in den Trümmern, ein Lichtstrahl | Bis MIKO eines Tages etwas fand, / das längst **verloren** war. | Erzählend, neugierig, ein kleines Anhalten vor „verloren“. |
| `intro_12` | 1:23,0 | 4,5 s | Nahaufnahme: der Keimling | Einen **Keimling**. … Klein. / Grün. / Lebendig. | Staunen. Sehr leise, fast ungläubig. „Lebendig“ mit Wärme – der emotionale Kern. |
| `intro_13` | 1:28,8 | 4,0 s | Das Schiff im All, Musik auf dem Höhepunkt | Ein altes Signal erwachte: / **Programm Zweite Chance**. | Mit Kraft, aber gefasst. „Programm Zweite Chance“ als Titel, deutlich getrennt. |
| `intro_14` | 1:34,0 | 5,5 s | Titel RE:PLANET über dem Planeten | Wenn das Leben zurückkehrt … kehren auch wir **zurück**. | Das Versprechen. Ruhig, hoffnungsvoll, am Ende leise lächelnd. |

Gesamt: ca. 58 Sekunden Sprache in 100 Sekunden – die Musik behält ihre Momente (Archenstart, Titel).

---

## 3. Abspann (5 Zeilen, optional)

Der Abspann läuft 52 Sekunden. Die Zeilen passen zu den Texttafeln. Fehlen sie, zeigt der Abspann nur seine Tafeln.

| Datei | Start | max. Dauer | Tafel | Text | Regie |
|---|---|---|---|---|---|
| `ending_01` | 0:01,5 | 5,0 s | Lichter sinken vom Himmel | Und eines Abends leuchteten **neue Lichter** am Himmel. | Leise, erstaunt – wie am Anfang des Intros, nur heller. |
| `ending_02` | 0:07,0 | 4,5 s | „Die Arche HORIZONT kehrt zurück.“ | Die Arche HORIZONT … kam nach **Hause**. | Erleichtert. „nach Hause“ warm. |
| `ending_03` | 0:13,0 | 5,0 s | Rückblick auf die vier Welten | Vier Welten. / Vier **zweite Chancen**. | Stolz, ruhig. |
| `ending_04` | 0:37,0 | 3,5 s | „Danke, MIKO.“ | Danke, **MIKO**. | Ganz schlicht, persönlich, fast gerührt. |
| `ending_05` | 0:41,5 | 6,5 s | Mitwirkende | Was wir fortgeworfen hatten … hast du uns **zurückgegeben**. | Der letzte Satz des Spiels. Langsam, mit Pause, am Ende leise. |

---

## 4. Aufnahme und Lieferung

**Format:** WAV, **48 kHz, mono**, 24 Bit (16 Bit geht auch). Eine Datei pro Zeile, Dateiname exakt wie oben
(`intro_01.wav` … `intro_14.wav`, `ending_01.wav` … `ending_05.wav`). OGG oder MP3 funktionieren ebenfalls.

**Schnitt:**
- Dateianfang: Sprache beginnt nach höchstens 50 ms (Atmer vor dem Satz dürfen drinbleiben, wenn sie zur Zeile gehören).
- Dateiende: 200–400 ms Ausklang, dann weich ausblenden. Maximale Dauer aus der Tabelle einhalten.
- Keine Musik, kein Hall, keine Effekte – das Spiel legt einen dezenten Kino-Klang darüber (abschaltbar, siehe unten).

**Pegel:**
- Lautheit je Datei etwa **−20 LUFS** (±2), Spitzen höchstens **−1 dBFS** (True Peak).
- Alle Zeilen gleich laut – der Erzähler soll nicht zwischen den Sätzen springen.
- Leichte Rauschminderung ist in Ordnung; nicht überkomprimieren (die Rauheit der Stimme soll bleiben).

**Wohin kopieren:** in den Ordner `Resources/Voice` des Spiels – er liegt **neben** `Resources/RePlanetSky.shader`:
- im Repository: `RePlanet/Assets/RePlanet/Resources/Voice/`
- bei einem Projekt, in das die Spieldateien anderswo eingebunden sind (z. B. `Assets/Settings/RePlanet/`):
  `Assets/Settings/RePlanet/Resources/Voice/`

Unity importiert die Dateien automatisch. Empfohlene Importeinstellungen (Inspector, Datei auswählen):
*Force To Mono* an, *Load Type* „Decompress On Load“, *Preload Audio Data* an, *Compression Format* „Vorbis“, Qualität 80–100.
Danach im Spiel: Hauptmenü → Intro ansehen. Lautstärke: Einstellungen → Audio → „Stimmen & Roboterlaute“.

**Kino-Klang:** `Narrator.CinemaSound` (Standard: an) – +4 dB Tiefen bei 140 Hz, etwas weniger Schärfe über 7,5 kHz,
sanfte Kompression, sehr leichte Sättigung und ein kleiner Raum. Klingt die fertige Aufnahme schon „fertig“ (Studio-Master),
kann er abgeschaltet werden (`Narrator.CinemaSound = false`).

**Musik:** Während eine Zeile läuft, senkt das Spiel die Musik weich um etwa 7 dB ab und hebt sie danach langsam wieder an.

---

## 5. Selbst aufnehmen

Eine eigene Aufnahme klingt fast immer menschlicher als jede Synthese – auch mit einfachen Mitteln:

1. **Raum:** klein und weich (Kleiderschrank, Decken, Sofa, Vorhänge). Kein Badezimmer, keine kahlen Wände.
2. **Mikrofon:** dynamisches Sprachmikrofon oder Großmembran-Kondensator, nah (15–20 cm), leicht seitlich am Mund vorbei,
   mit Poppschutz. Die Nähe (Nahbesprechungseffekt) macht die Stimme tiefer und wärmer.
3. **Programm:** z. B. Audacity (kostenlos): Projekt-Rate 48000 Hz, Kanäle Mono. Pegel so einstellen, dass laute Stellen bei etwa −10 dBFS liegen.
4. **Stimme:** vorher 10 Minuten warm sprechen, lauwarmes Wasser, kein Milchprodukt. Morgens klingt die Stimme tiefer und rauer.
   Leise sprechen, aber mit Körper – nicht flüstern. Jede Zeile 3–5 Mal aufnehmen, die ruhigste nehmen.
5. **Nachbearbeitung:** Anfang/Ende schneiden, leichte Rauschminderung, Lautheit normalisieren (−20 LUFS), als WAV 48 kHz mono exportieren.

## 6. Mit einem Stimmdienst erzeugen

Wer einen Sprachsynthese- oder Stimmdienst nutzt (Text-to-Speech, Voice-Design), sollte:

- eine **tiefe, ältere, raue Erzählerstimme** wählen, deutschsprachig und muttersprachlich klingend;
- **jede Zeile einzeln** erzeugen und Tempo/Stabilität so einstellen, dass die Stimme ruhig, aber nicht monoton klingt
  (zu hohe „Stabilität“ klingt maschinell, zu niedrige wackelt);
- Pausen über Satzzeichen oder die Pausen-Funktion des Dienstes setzen (z. B. SSML `<break time="700ms"/>`, falls unterstützt);
- mehrere Varianten erzeugen und die natürlichste auswählen; unnatürliche Betonungen durch Umschreiben (Kommas, Punkte) korrigieren;
- die Datei wie oben schneiden, Lautheit angleichen und als WAV 48 kHz mono speichern.

**Rechte:** Vor der Veröffentlichung prüfen, dass die Lizenz des Dienstes bzw. die Vereinbarung mit dem Sprecher die
**kommerzielle Nutzung in einem Spiel** erlaubt. Keine Stimme einer realen Person nachbilden (Klonen), ohne deren
ausdrückliche, schriftliche Zustimmung. Die Nennung des Sprechers bzw. Dienstes gehört in die Mitwirkenden.

---

## 7. Untertitel

Die Untertitel zeigen genau den gesprochenen Text. Sie stehen in `IntroTimeline.Shots[].Lines` (`Core/Audio/Synth.cs`)
und für den Abspann in `Narrator.EndingCues()`. Wer den Sprechertext ändert, ändert beide Stellen und diese Datei.
Mit Aufnahme erscheint ein Untertitel, solange die Zeile klingt; ohne Aufnahme für die maximale Dauer der Zeile.
