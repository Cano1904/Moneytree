# RE:PLANET – Sprechertext für ElevenLabs

Fertig zum Kopieren: Jede Zeile ist **eine eigene Datei**. Text in ElevenLabs einfügen, erzeugen, herunterladen und
exakt so benennen wie angegeben (`intro_01.mp3` … `intro_14.mp3`, `ending_01.mp3` … `ending_05.mp3`,
`game_01.mp3` … `game_21.mp3`).
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

## 5. Im Spiel (21 Zeilen, Erzähler Helmut)

Kurze Sätze zu besonderen Momenten im Spiel – **dieselbe Stimme und Regie wie im Intro** (ruhig, warm, leicht
melancholisch, tiefer alter Erzähler). Jede Zeile läuft pro Spielstand nur einmal, die Musik wird dabei leicht abgesenkt.
Ohne Aufnahme erscheint der Satz als Untertitel. Abschaltbar in den Einstellungen („Audio → Erzähler im Spiel“).
Die Zeilen stehen im Code in `Core/Sim/Story.cs` (Untertitel) – bei Textänderungen beide Stellen anpassen.

| Datei | max. | Anlass | Text v3 (mit Tags, zum Kopieren) | Text v2 (ohne Tags) |
|---|---|---|---|---|
| `game_01` | 5,5 s | Erste Landung auf TERRA | `[calm] Die alte Erde. [pause] Sie hat lange auf jemanden gewartet, der bleibt.` | Die alte Erde. Sie hat lange auf jemanden gewartet, der bleibt. |
| `game_02` | 5,5 s | Erste Landung auf PYRA | `Pyra glühte einst vor Arbeit. [sad] Jetzt glüht nur noch der Sand.` | Pyra glühte einst vor Arbeit. Jetzt glüht nur noch der Sand. |
| `game_03` | 5,0 s | Erste Landung auf PELAGIA | `[softly] Pelagia. Ein Meer, das sich nach klarem Wasser sehnt.` | Pelagia. Ein Meer, das sich nach klarem Wasser sehnt. |
| `game_04` | 6,5 s | Erste Landung auf NIVALIS | `[quietly] Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen.` | Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen. |
| `game_05` | 5,0 s | Erstes Mal Müll ins Lager gebracht | `[warmly] Das erste Stück ist heimgebracht. So fängt jede Heimkehr an.` | Das erste Stück ist heimgebracht. So fängt jede Heimkehr an. |
| `game_06` | 4,5 s | Erster Verkauf | `Aus dem, was wir fortwarfen, [warmly] wird wieder etwas wert.` | Aus dem, was wir fortwarfen, wird wieder etwas wert. |
| `game_07` | 5,0 s | Erste Schrottlieferung per Frachter | `Von fern kommt ein Frachter. [warmly] Du bist nicht mehr ganz allein.` | Von fern kommt ein Frachter. Du bist nicht mehr ganz allein. |
| `game_08` | 5,0 s | Erster Lichtpunkt sauber | `[softly] Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran.` | Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran. |
| `game_09` | 6,5 s | Erster Bereich: Hauptmüll entfernt (85 %) | `Der größte Berg ist abgetragen. [in awe] Darunter liegt eine Straße, die man fast vergessen hatte.` | Der größte Berg ist abgetragen. Darunter liegt eine Straße, die man fast vergessen hatte. |
| `game_10` | 6,0 s | Erster Bereich zu 100 % gereinigt | `[quietly] Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen.` | Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen. |
| `game_11` | 5,0 s | Erstes Projekt fertig – die Stadt erwacht | `[in awe] Die Lichter gehen wieder an. [softly] Leise, eines nach dem anderen.` | Die Lichter gehen wieder an. Leise, eines nach dem anderen. |
| `game_12` | 5,5 s | Erster Planet komplett (Großprojekt) | `[moved] Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben.` | Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben. |
| `game_13` | 4,5 s | Erster Sturm (nicht PYRA) | `[calm] Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund.` | Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund. |
| `game_14` | 5,0 s | Erster Sandsturm auf PYRA | `Der Sand wandert wieder. [thoughtful] Morgen sehen die Wege anders aus.` | Der Sand wandert wieder. Morgen sehen die Wege anders aus. |
| `game_15` | 6,0 s | Erste Nacht | `[softly] Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können.` | Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können. |
| `game_16` | 5,5 s | Erster Morgen nach dem Schlafen | `[warmly] Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet.` | Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet. |
| `game_17` | 5,0 s | Erste Notabschaltung | `[gently] Manchmal geht einem die Kraft aus. Das ist keine Schande.` | Manchmal geht einem die Kraft aus. Das ist keine Schande. |
| `game_18` | 5,5 s | NIVALIS freigeschaltet | `[hopeful] Das Eis ruft. Auf Nivalis wartet das letzte Signal.` | Das Eis ruft. Auf Nivalis wartet das letzte Signal. |
| `game_19` | 5,0 s | Erster Mitspieler im Koop | `[warmly] Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter.` | Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter. |
| `game_20` | 5,0 s | Erstes Fundstück | `Ein Fundstück. [sad] Jemand hat es gewusst – und trotzdem nichts getan.` | Ein Fundstück. Jemand hat es gewusst – und trotzdem nichts getan. |
| `game_21` | 6,0 s | Erste Ökologie wiederhergestellt | `[in awe] Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet.` | Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet. |

Hinweise: Zwischen zwei Zeilen liegt mindestens eine knappe Sekunde; kommen zwei Anlässe gleichzeitig (z. B. Landung und
Mitspieler), laufen sie nacheinander. Die maximale Dauer ist großzügiger als im Intro – trotzdem nicht darüber gehen,
der Untertitel verschwindet danach.

## 6. Ins Spiel bringen

1. Dateien genau so benennen (`intro_01.mp3`, `game_01.mp3` usw.; `.wav` oder `.ogg` gehen auch).
2. In Unity in den Ordner **`Resources/Voice`** kopieren – er liegt neben `Resources/RePlanetSky.shader`
   (bei dir: `Assets/Settings/RePlanet/Resources/Voice/`, sonst `Assets/RePlanet/Resources/Voice/`).
3. Play drücken und „Neues Spiel“ starten: Die Zeilen laufen zeitgenau zur Musik, die Musik wird dabei leicht abgesenkt.
   Fehlt eine Datei, erscheint an dieser Stelle nur der Untertitel.

Alternativ: Dateien an Claude schicken – dann werden sie geprüft (Länge, Pegel, Stille am Anfang), bei Bedarf
angepasst und ins Projekt übernommen.
