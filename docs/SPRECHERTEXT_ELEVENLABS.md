# RE:PLANET – Sprechertext für ElevenLabs

Fertig zum Kopieren: Jede Zeile ist **eine eigene Datei**. Text in ElevenLabs einfügen, erzeugen, herunterladen und
exakt so benennen wie angegeben (`intro_01.mp3` … `intro_14.mp3`, `ending_01.mp3` … `ending_05.mp3`).
Ausführliche Regie und Szenenbeschreibung: `docs/SPRECHERTEXT.md`.

## 1. Stimme auswählen

In ElevenLabs unter **Voices → Voice Library** suchen, Sprache **Deutsch**, Geschlecht männlich, Alter „alt“ bzw.
„mittel“. Gute Suchbegriffe:

- `deep narrator`, `storyteller`, `gravelly`, `raspy`, `documentary`, `movie trailer`, `old man`
- Deutsch: `Erzähler`, `tief`, `rau`, `Hörbuch`

Worauf achten: tiefe, raue, ruhige Stimme; müde, aber warm – kein Werbesprecher, kein Trailer-Gebrüll.
Vorher mit Zeile `intro_01` testen und zwei, drei Stimmen vergleichen. **Nutzungsrechte:** Stimmen aus der Voice Library
sind für die Nutzung in eigenen Projekten gedacht; für eine kommerzielle Veröffentlichung einen bezahlten Tarif wählen
und die Lizenzbedingungen der Stimme prüfen. Keine Stimme einer realen Person ohne deren Zustimmung klonen.

## 2. Einstellungen

| Einstellung | Wert |
|---|---|
| Modell | **Eleven v3** (am ausdrucksstärksten, versteht die Tags in eckigen Klammern) – alternativ **Eleven Multilingual v2** (dann die Tags weglassen, Spalte „Text v2“ nehmen) |
| Stabilität | 40–50 % (v3: „Natural“) – niedriger = lebendiger, höher = gleichmäßiger |
| Ähnlichkeit (Similarity) | 75 % |
| Stil (Style Exaggeration) | 15–30 % |
| Speaker Boost | an |
| Geschwindigkeit | 0,90–0,95 (langsam, aber jede Zeile muss in ihre **maximale Dauer** passen) |
| Ausgabeformat | WAV (PCM, 44,1 oder 48 kHz), sonst MP3 192 kbit/s |

Tipps:
- Pro Zeile 2–3 Varianten erzeugen und die beste nehmen.
- Ist eine Aufnahme **länger als die maximale Dauer**, Geschwindigkeit leicht erhöhen oder neu erzeugen.
- Stille am Anfang der Datei entfernen (im Spiel ist der Startzeitpunkt fest).
- Kino-Klang (Tiefen, Hall) fügt das Spiel selbst hinzu – in ElevenLabs keinen Hall/Effekt verwenden.

## 3. Intro (14 Zeilen)

| Datei | max. | Text v3 (mit Tags, zum Kopieren) | Text v2 (ohne Tags) |
|---|---|---|---|
| `intro_01` | 5,0 s | `[calm] Es gab einmal eine Welt, die alles hatte.` | Es gab einmal eine Welt, die alles hatte. |
| `intro_02` | 5,5 s | `Und alles, was sie hatte … [pause] warf sie fort.` | Und alles, was sie hatte … warf sie fort. |
| `intro_03` | 6,5 s | `Konsuma versprach uns das Glück. [ironic] Alles. Sofort. Immer neu.` | Konsuma versprach uns das Glück. … Alles. Sofort. Immer neu. |
| `intro_04` | 6,0 s | `Wir kauften und kauften … [slowly] bis der Müll unsere Städte überragte.` | Wir kauften und kauften … bis der Müll unsere Städte überragte. |
| `intro_05` | 6,0 s | `Dann bauten wir Archen. [skeptical] „Nur für fünf Jahre“, sagten sie.` | Dann bauten wir Archen. „Nur für fünf Jahre“, sagten sie. |
| `intro_06` | 6,5 s | `[sad] Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen.` | Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen. |
| `intro_07` | 5,0 s | `[quietly] Aus fünf Jahren … wurden fünfzig.` | Aus fünf Jahren … wurden fünfzig. |
| `intro_08` | 5,5 s | `Eine Maschine nach der anderen … [whispers] verstummte.` | Eine Maschine nach der anderen … verstummte. |
| `intro_09` | 5,5 s | `Nur eine nicht. [warmly] Eine kleine, sture Maschine.` | Nur eine nicht. … Eine kleine, sture Maschine. |
| `intro_10` | 6,0 s | `[warmly] Miko. Jeden Morgen. Würfel für Würfel.` | Miko. Jeden Morgen. Würfel für Würfel. |
| `intro_11` | 5,5 s | `Bis Miko eines Tages etwas fand, das längst verloren war.` | Bis Miko eines Tages etwas fand, das längst verloren war. |
| `intro_12` | 4,5 s | `[in awe] Einen Keimling. Klein. Grün. Lebendig.` | Einen Keimling. … Klein. Grün. Lebendig. |
| `intro_13` | 4,0 s | `[firmly] Ein altes Signal erwachte: Programm Zweite Chance.` | Ein altes Signal erwachte: Programm Zweite Chance. |
| `intro_14` | 5,5 s | `[hopeful] Wenn das Leben zurückkehrt … kehren auch wir zurück.` | Wenn das Leben zurückkehrt … kehren auch wir zurück. |

## 4. Abspann (5 Zeilen)

| Datei | max. | Text v3 | Text v2 |
|---|---|---|---|
| `ending_01` | 5,0 s | `[softly] Und eines Abends leuchteten neue Lichter am Himmel.` | Und eines Abends leuchteten neue Lichter am Himmel. |
| `ending_02` | 4,5 s | `[relieved] Die Arche Horizont … kam nach Hause.` | Die Arche Horizont … kam nach Hause. |
| `ending_03` | 5,0 s | `Vier Welten. [proudly] Vier zweite Chancen.` | Vier Welten. Vier zweite Chancen. |
| `ending_04` | 3,5 s | `[moved] Danke, Miko.` | Danke, Miko. |
| `ending_05` | 6,5 s | `Was wir fortgeworfen hatten … [softly] hast du uns zurückgegeben.` | Was wir fortgeworfen hatten … hast du uns zurückgegeben. |

Hinweise zur Aussprache: „Konsuma“, „Miko“ und „Horizont“ sind hier bewusst klein geschrieben, damit die Stimme sie als
Wort und nicht als Abkürzung liest. „…“ erzeugt eine kurze Pause.

## 5. Ins Spiel bringen

1. Dateien genau so benennen (`intro_01.mp3` usw.; `.wav` oder `.ogg` gehen auch).
2. In Unity in den Ordner **`Resources/Voice`** kopieren – er liegt neben `Resources/RePlanetSky.shader`
   (bei dir: `Assets/Settings/RePlanet/Resources/Voice/`, sonst `Assets/RePlanet/Resources/Voice/`).
3. Play drücken und „Neues Spiel“ starten: Die Zeilen laufen zeitgenau zur Musik, die Musik wird dabei leicht abgesenkt.
   Fehlt eine Datei, erscheint an dieser Stelle nur der Untertitel.

Alternativ: Dateien an Claude schicken – dann werden sie geprüft (Länge, Pegel, Stille am Anfang), bei Bedarf
angepasst und ins Projekt übernommen.
