# RE:PLANET – Klang und Musik

Alle Klänge von RE:PLANET werden zur Laufzeit **berechnet** – es gibt keine Audiodateien, keine Samples und keine
Fremdinhalte. Die Synthese liegt als reines C# in `Core/Audio/Synth.cs` (ohne UnityEngine), die Wiedergabe in
`Runtime/Audio/AudioManager.cs`.

**Lizenz:** vollständig eigene, prozedural erzeugte Inhalte (Code und daraus berechnete Klänge gehören zum Projekt).

## Architektur

```
Synth (Core, threadsicher)                          AudioManager (Unity, Hauptthread + 2 Hintergrund-Threads)
├── Sfx(id)          → float[] 44,1 kHz             ├── Warteschlange mit Prioritäten, Disk-Cache (PCM16, versioniert)
├── Music(id)        → MusicSet: 7 Stems, 22,05 kHz ├── 2 Musik-Slots × 7 AudioSources (PlayScheduled, Überblendung)
├── IntroScore()     → 100 s + 4 s Ausklang         ├── Intro-/Abspann-Quelle (vorgemerkter Start, DSP-genaue IntroTime)
└── IntroTimeline    → Zeitplan der Zwischensequenz ├── 24 Effektquellen (3D, logarithmisch 3–60 m) + Dauerklänge
                                                    └── Ambience (Wind, Sturm, Wasser, Nacht) und GameApp.OnFx
```

* **Erzeugung:** zuerst die kurzen Effekte, dann Menümusik, Intro, danach die übrigen Stücke und Ambience-Loops
  (nur in den Disk-Cache, geladen wird erst bei Bedarf). Ruft die Zwischensequenz `PlayIntro()` auf, bevor der Score
  fertig ist, wird er vorgezogen und startet automatisch; `IntroTime` bleibt bis zum tatsächlichen Start −1.
* **Cache:** `persistentDataPath/audiocache/v<Hash>/`. Der Hash hängt an `Synth.Version` – nach jeder hörbaren Änderung
  an der Synthese diese Konstante erhöhen, alte Cache-Ordner werden beim Start gelöscht.
* **Speicher:** höchstens drei Musik-Sätze gleichzeitig; Ambience-Loops nur für den aktuellen Planeten.
* **Lautstärken:** `Settings.MasterVolume` (AudioListener), `MusicVolume`, `SfxVolume`, `AmbientVolume`, `UiVolume`;
  bei `MuteWhenUnfocused` wird ohne Fokus weich stummgeschaltet (Wiedergabe läuft weiter, damit Sync erhalten bleibt).

## Musik-Engine

Jedes Stück ist ein Arrangement aus Abschnitten (Tempo, Taktart, ein Akkord je Takt) mit einem **Spannungsbogen**
(Intensität je Takt). Daraus entstehen:

* **Legato-Stimmen** (Streicher, Chor, Blech, Flageolett, Sub): verstimmte PolyBLEP-Sägezähne mit Glide, verzögertem
  Vibrato, dynamikabhängigem Filter (lauter = heller), Formantfiltern beim Chor („a“, „o“, „u“) und Sättigung beim Blech.
  Automatische Stimmführung für vierstimmige Sätze.
* **Einzelklänge**: Klavier (inharmonische Teiltöne, zwei Saiten, zweistufiger Ausklang), Spiccato- und
  Pizzicato-Streicher (Karplus-Strong), Taiko, Pauke (Wirbel), Amboss/Maschinenklappern, Sub-„Boom“, Crash,
  Rückwärts-Becken, Riser und „Braams“ (verzerrte tiefe Blechbläser-Cluster).
* **Hall** (Freeverb-Struktur) je Stem, bei Loops zweifach durchlaufen, damit der Hallanfang nahtlos ist.
* **Nahtlose Loops:** Ausklänge werden zyklisch an den Anfang addiert, Legato-Stimmen und Wind mit Vorlauf berechnet
  und mit gleicher Leistung überblendet gefaltet.
* **Pegel:** alle Stems gemeinsam normalisiert (Summe höchstens 0,89) – das Mischverhältnis bleibt erhalten, kein Clipping.

### Stems

| Stem | Inhalt | Im Spiel (Wiederherstellung `Rules.PlanetRestoration`) |
|---|---|---|
| `pad` | Streicherfläche, hohe Violinen, Flageoletts | immer |
| `piano` | Klavierthemen, gebrochene Akkorde, tiefe Akzente | ab 0,1 |
| `bass` | Celli-Ostinato (Spiccato/Pizzicato), Kontrabass/Sub, Wellen-Swells | ab 0,2 |
| `choir` | Chor „a“/„o“, Männerchor (PYRA) | ab 0,4 |
| `brass` | Hörner (Akkorde, Thema), Posaunen, Braams, Walgesang (PELAGIA) | ab 0,6 |
| `perc` | Taiko, Pauke, Ambosse, Riser, Einschläge, Violinen-Spiccato im Höhepunkt | gedämpft, ab 0,3 voll |
| `wind` | musikalischer Wind (planetentypisch) | immer, folgt `Rules.Wind` |

Nachts (`Rules.Darkness`) werden Chor, Blech und Klavier zurückgenommen; bei Sturm (`StormActive`, Vorwarnung
`StormWarn`) steigen Schlagwerk, Blech und Wind. Wenige aktive Schichten werden um bis zu +4,6 dB ausgeglichen.
Planetenwechsel und Menü ↔ Spiel werden in 2–2,5 s überblendet.

### Stücke und Stimmungen

| ID | Tonart / Tempo | Stimmung |
|---|---|---|
| `menu` | d-Moll → D-Dur, 84 BPM, 12 Takte | episches Hauptthema, Sechzehntel-Ostinato, Höhepunkt über VI–VII–I |
| `terra` | A-Dur mit Moll-Einfärbung, 72 BPM | goldene Melancholie → Hoffnung: Klavierthema, warme Streicher, Horn, Staubwind |
| `pyra` | g-Moll/dorisch, 100 BPM, 16 Takte | industriell-dramatisch: stampfende Taiko, Ambosse, Posaunen, Männerchor, heulender Wüstenwind |
| `pelagia` | H-lydisch, 3/4, 66 BPM | weit und atmend: zwei Wellenbögen, Chor, Wellen-Swells, walartige Gleitklänge, Seewind |
| `nivalis` | f-Moll, 64 BPM | eisig und erhaben: Flageoletts, kalter Chor, spärliches Klavier, Blechchoral, Schneesturm |
| `ending` | D-Dur, 76 BPM, 16 Takte (50 s) | triumphal-hoffnungsvoll: das Hauptthema in Dur |

Glocken, Celesta und FM-Glocken kommen in der Musik nicht mehr vor (nur noch in kurzen Oberflächen-Signalen wie Münzen).

### Intro-Score (100 s)

| Zeit | Bild | Musik |
|---|---|---|
| 0–14 | Skyline | Bordun, hohe Flageoletts, einsames Klavier, Wind |
| 14–30 | Megastore | mechanisches Sechzehntel-Ostinato, Ticken, Posaunenakzente, Steigerung, Riser |
| 30–46 | Archen starten | Braam, Chor, Hörner (tragisches Thema), volle Taiko, letzter Einschlag bei 43,3 s |
| 46–60 | Roboter schalten ab | Leere: fallende Klaviertöne, Hauch von Flageolett, Wind |
| 60–75 | MIKO erwacht | warmes A-Dur, Pizzicato „Würfel für Würfel“, Klavierthema von TERRA |
| 75–88 | Keimling | Staunen (Tremolo, Chor), dann Paukenwirbel, Trommelsteigerung, Riser |
| 88–100 | Schiff/Titel | Hauptthema in D-Dur mit Blech, Chor, Taiko; Schlussakkord mit großem Einschlag |

## Ambience und Effekte

* Wind-Loops je Planet (`wind_terra`, `wind_pyra`, `wind_pelagia`, `wind_nivalis`) und Sturm-Loops (`storm_*`), je 10 s:
  böiges gefiltertes Rauschen mit Heulresonanzen, zeitweisem Pfeifen, Grollen, Zischen und Sandkörnern bzw. Graupel;
  Stürme zusätzlich mit Regen/Gischt (PELAGIA), Blechschlagen (PYRA) oder Trümmerklappern (TERRA).
  Die Windlautstärke folgt `Rules.Wind`, die Stereo-Position der Windrichtung relativ zur Kamera; im Unterschlupf gedämpft.
* `water_loop` (Brandung, Schwappen) auf Wasserplaneten abhängig von der Kamerahöhe, `night_ambience`
  (Grillen, fernes Knarzen) nachts, `thunder` beim Sturmbeginn/-warnung (Blitzdonner selbst löst `Atmosphere` aus).
* Spielereignisse (`GameApp.OnFx`) werden auf passende Effekte abgebildet, z. B. Sammeln nach Material
  (Papier, Glas, Kunststoff/Netz, Metall), Magnetwelle, Schneiden, Pressen, Verkauf, Bau, Reparatur, Pflanzen,
  Fundstücke, Aufträge, Sturm, Tag/Nacht, Notabschaltung, Kran. Gleichzeitige Doppelauslösungen werden zusammengefasst.

## Prüfen ohne Unity

```
cd Tests && dotnet build -c Release -nologo -v q
dotnet bin/Release/net8.0/RePlanet.Tests.dll audio <ordner>          # alles
AUDIOPROBE="music terra pyra" dotnet bin/Release/net8.0/RePlanet.Tests.dll audio <ordner>   # Auswahl
```

Schreibt alle Effekte, jeden Musik-Stem einzeln, den Mix jedes Stücks (zweimal hintereinander, um die Naht zu hören)
und `intro.wav`, dazu `report.txt` mit Erzeugungszeiten, Spitzenpegeln, NaN-Prüfung, Nahtprüfung und RMS-Verlauf je 4 s.
