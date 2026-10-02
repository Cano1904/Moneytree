# RE:PLANET – Architektur

## Überblick

```
RePlanet/Assets/RePlanet/
├── Core/        Reine Spiellogik (C#, ohne UnityEngine – asmdef mit noEngineReferences)
│   ├── Data/      Datentabellen: Materialien, Müllarten, Werkzeuge, Fahrzeuge, Gebäude, Planeten, Projekte, Aufträge, Fundstücke, Kosmetik
│   ├── World/     Deterministische Weltgenerierung (Terrain-Höhenfunktion, Planeten-Layouts)
│   ├── Sim/       Serverautoritative Simulation (State, Rules, Game, GameActions)
│   ├── Save/      Versioniertes Speicherformat mit Prüfsumme, Backup, Migration
│   ├── Net/       Sitzungen (Session, SessionHub), Transporte (lokal, TCP), Client-Replik (GameClient), Host (HostServer)
│   ├── Motion/    Bewegungsphysik (Roboter, Schwimmen/Tauchen, Fahrzeuge, Kollision)
│   ├── Audio/     Prozedurale Klangsynthese (Effekte, Musik-Stems, Intro-Score, Radio-Kennungen, Stadtklänge) + Intro-Zeitplan
│   └── Loc/       Lokalisierung: Deutsch = Schlüssel, Englisch in LocEn*.cs (Oberfläche, Daten, Meldungsvorlagen)
├── Runtime/     Unity-Schicht (MonoBehaviours, alles wird zur Laufzeit erzeugt – keine Prefabs nötig)
│   ├── Game/      GameApp (Ablaufsteuerung), Settings/Profile, InputMap, Bridge (HUD-/UI-Zustand),
│   │              CameraRig, PlayerController, IntroDirector, EndingDirector
│   ├── Render/    WorldView (+Props), Atmosphere (Himmel/Licht/Wetter), TrashRenderer (Instancing),
│   │              ActorsView (Roboter, Fahrzeuge, Drohnen, Anlagen), FxView (Effekte), FloraRenderer,
│   │              RobotModel (MIKO), MeshKit (prozedurale Formen), Mats (Materialien),
│   │              ShipArrival (Containerfrachter bei Lieferungen, Landeanflug; Geometrie: FreighterModel),
│   │              PlanetSelectScene (Planetenwahl als Weltall-Szene mit Anflug),
│   │              MapCamera (3D-Karte: eigene Kamera schräg von oben in eine RenderTexture, nur bei offener Karte)
│   ├── Audio/     AudioManager (Clips aus Core/Audio, Musikschichten, 3D-Effekte, Ambience), AudioRadio (Radio),
│   │              AudioCity (Stadtklänge), Narrator (Erzähler: Intro, Abspann, Zeilen im Spiel)
│   └── UI/        UIRoot (IMGUI: Hauptmenü, Einstellungen, HUD, Spielmenü, Karte (3D mit Symbolen, 2D-Rückfall), Bau-/Fotomodus);
│                  HudHints: ruhiges HUD je Einstellung „Hinweise“ (Aus / Minimal / Ausführlich);
│                  RadioUI (Spielmenü-Reiter Radio), PerfOverlay (Leistungsanzeige, F3), BuildUI (Bauansicht mit Controller)
├── Editor/      Projekt-Setup (Material-Vorlagen, Szene, Build-Einstellungen) und Build-Menü
└── Resources/   RePlanetSky.shader (eigener Himmel) + vom Setup erzeugte Material-Vorlagen
```

## Eine Simulation – drei Betriebsarten

Alle Zustandsänderungen passieren in `Core/Sim/Game` (Methode `Apply` für Spieleraktionen, `Tick` für Zeit).
Clients schicken nur **Absichten** (Positionsmeldungen und Aktionen mit eindeutiger Anfrage-ID) und erhalten
**Patches** (geänderte Zustandsteile) sowie schnelle **Positionspakete** (15×/s).

| Betriebsart | Wo läuft die Simulation? | Transport |
|---|---|---|
| Solo | im Spielprozess (`HostServer`) | `LocalServerTransport` (gleiche Nachrichten wie im Netz) |
| Koop (Host) | im Spielprozess des Hosts | lokal + `TcpServerTransport` (Port einstellbar, Standard 7777) |
| Dedizierter Server | `Server/` (.NET-Konsole) | `TcpServerTransport`, beliebig viele Sitzungen per Code |

Dadurch verhält sich das Solo-Spiel exakt wie der Koop – es gibt keinen zweiten Codepfad.

## Datenfluss im Spiel

```
Eingabe (InputMap) ─► PlayerController ─► Motor (lokale Vorhersage) ─► GameClient.SendInput ─► Session ─► Game.Move (Prüfung)
                                     └──► GameClient.Act(Aktion) ────────────────────────────► Session ─► Game.Apply
Game ─► BuildPatch/PosPacket ─► GameClient (Replik WorldState) ─► WorldView / ActorsView / TrashRenderer / UIRoot / AudioManager
```

* **Autorität:** Credits, Inventare, Objekte, Käufe, Projekte, Missionen ändern sich nur auf dem Server.
* **Idempotenz:** Jede Aktion hat eine Anfrage-ID; Wiederholungen liefern das gespeicherte Ergebnis ohne erneute Verarbeitung.
* **Gleichzeitigkeit:** Die Sitzung verarbeitet Nachrichten nacheinander – greifen zwei Spieler dasselbe Objekt, gewinnt genau einer.
* **Bewegung:** Clients bewegen sich vorhergesagt; der Server prüft Geschwindigkeit und Grenzen und korrigiert (`corr`).

## Welt und Speicherung

Die Planeten werden aus festen Seeds erzeugt (`WorldGen`). Gespeichert werden nur Abweichungen:
entfernte Objekte (Bitset), aufgetaute Objekte, neue dynamische Objekte (Zerlegeteile, Lieferungen, versetzte Wracks),
Lager, Gebäude, Projekte, Reparaturen, Begrünung, Fahrzeuge, Wetter, Notunterschlüpfe, Missionen, Statistiken.
Der Fotomodus kann deshalb jederzeit den **Ausgangszustand** (Vorher-Ansicht) zeigen.

**Befahrbare Innenräume:** `BaseLayout.Rooms` beschreibt Hangar und Schiffsladeraum (`ShelterRoom`: Innenfläche,
Eingang, Schlafplatz, Vorplatz). `Rules.ShelterKind` liefert dafür 3 (Hangar) bzw. 4 (Schiff); `Rules.Indoors` sperrt dort
Sammelaktionen serverseitig. Rampe und Laderaumboden sind begehbare Böden (`FloorPatch`, `PlanetLayout.GroundAt`), die
der Motor, die Kamera und der Kampagnen-Bot gleichermaßen nutzen. Tor und Rampe sind reine Darstellung (`WorldViewBase`).

## Stationen je Planet und Schlaf (`Runtime/Render/WorldViewStations.cs`, `RobotSleep.cs`)

* **Gleiche Spielfläche, eigene Architektur:** Hangar, Stationen, Garage und Wege bleiben wie in `WorldGen.BuildBase`
  (Kollision, `ShelterRoom`, Tests unverändert). `WorldViewBase.BuildBase` wählt je Planet den Stil: TERRA Stadtdepot
  (`CoreBuilding` + `TerraDepotExtras`), PYRA `PyraBunker`, PELAGIA `PelagiaPier`, NIVALIS `NivalisDome`; dazu
  `HangarStyleDetails` (Innenraum), `StationGarage`, `StationPad` (Platte unter jeder Station) und das Tor je Stil
  (Rolltor, Stahl-Hubtor, Holz-Rolltor, Schleuse mit zwei Schiebehälften; Öffnen weiterhin über `hangarOpen`).
* **Zusätzliche Kollision (nur ergänzt):** `WorldGen.StationExtras` – PYRA Felsflanke links der Halle, PELAGIA Hafenbecken des
  Bootshauses (Art `stationdeco`, beide in einer Sackgasse ohne Stationen/Fahrzeugplätze).
* **Leistung:** alles in einem Paletten-Mesh (39–47 Draws je Station wie vorher); Feindetail (Schrauben, Fugen, Nieten) in
  „BaseDetail“, ab 75 m Kameraabstand ausgeblendet. Lichter: Wandleuchten in Planetenfarbe, Leuchtröhren (TERRA), Glut und
  Rauch (PYRA, `FxView.Burst` nur in Kameranähe), Leuchtfeuer mit drehendem Strahl (PELAGIA), Heizstrahler/Warnfeuer (NIVALIS).
* **Schlaf:** `ActorsView.SleepingVisual` = Server-`Sleeping` oder 6,5 s nach dem Serverereignis „sleep“ (allein wird die
  Nacht sofort übersprungen – die Szene läuft dann in den Morgen). MIKO fährt auf den Ladeplatz (`SleepSpots`: Ladering im
  Hangar, Laderaum, Ladefläche; mehrere Spieler nebeneinander), `RobotModel.ApplySleep` legt Haltung, geschlossene Augen,
  Atmen, Ladekabel, Ladeleuchte und Z an; Aufwachen mit Strecken und Piepser. Alles aus repliziertem Zustand/Ereignis →
  beim Mitspieler gleich. `CameraRig.SleepCamera`: langsame Kreisfahrt, danach zurück zur alten Blickrichtung. Das
  Kabinenlicht im Hangar dimmt, solange jemand darin schläft.
* **Prüfumgebung:** `dotnet run -- shots <Ordner> [Filter]` zeichnet Bilder aus dem laufenden Spiel (Software-Renderer, keine
  Unity-Aufnahmen); `RP_SHOTS` eigene Aufnahmen, `RP_VIEWS`/`RP_PICK` für die statische Ansicht, `RP_SCHATTEN=1` Gebäudeschatten.

## Zusatzsysteme (Lieferlimit, Sturm abwarten, Helfer, Erfolge, Schnellreise, Ereignisse)

Alles serverautoritativ in `Core/Sim/GameFeatures.cs` (Aktionen `wait`, `fasttravel`, `botfix`, `botfollow`, `botstay`;
`TickFeatures` aus `Game.Tick`), Regeln für Server und Anzeige in `Core/Sim/FeatureRules.cs`, Daten und Balancing-Werte in
`Core/Data/GameDataFeatures.cs`, Zustandsklassen (`HelperBot`, `AchievementDef`) in `Core/Sim/FeatureState.cs`.

* **Speicherung (abwärtskompatibel, Format bleibt v3):** neue Planetenteile `bots` (reparierte Helfer: Arbeitsort,
  Position, Ladung) und `ev` (nächstes Ereignis, Zähler), `misc.dn` (Lieferabklingzeit), `DynObj.ev` (Ereignisfund),
  Weltteil `ach` (Erfolge). Fehlen sie in alten Ständen, gelten Standardwerte; bereits erfüllte Erfolge werden beim Laden
  still nachgetragen.
* **Zeitraffer:** `Game.TimeScale` ist 4, solange alle verbundenen Spieler einen Sturm geschützt abwarten;
  `Session.Update` rechnet damit Echtzeit in Spielzeit um. Stürme laufen dabei unverändert ab.
* **Helferroboter:** Fundorte `PlanetLayout.Bots` (erzeugt am Ende von `WorldGen.Generate` mit eigenem Zufallsgenerator,
  damit alle übrigen Objekt-IDs gleich bleiben). Positionen im Positionspaket (`"b"` → `GameClient.Bots`).
* **Schnellreise:** `pinnedPos` in `Game` lehnt Positionsmeldungen fern der neuen Stelle ab (Korrektur an den Client), bis der
  Client die Serverposition übernommen hat.
* **Darstellung/Oberfläche:** `Runtime/Render/FeaturesView.cs` (Helfer als kleine MIKO-Modelle, Leuchtspuren, Rohrpost-Kapseln,
  Lichtsäulen, Rauchzeichen), `Runtime/UI/FeaturesUI.cs` (Reiter „Erfolge“, Schnellreise-Liste in der Kartenseitenleiste,
  Kartensymbole, Abwarten-Anzeige), `Runtime/Game/FeatureToasts.cs` (Hinweise), `Runtime/Game/PlayerControllerFeatures.cs`
  (Interaktion mit Helfern).

## Darstellung

* **Himmel:** eigener Shader (`Resources/RePlanetSky.shader`) mit Farbverläufen, Dunstband, Sonne, animierten Wolken
  auf einer gekrümmten Wolkenschicht mit Selbstverschattung, Nebel, Sternen, zwei Himmelskörpern mit Atmosphärensaum
  und Polarlicht. `Atmosphere` mischt je Planet die Paletten für Tag, Dämmerung, Nacht und Sturm.
  Vorschau ohne Unity: `python3 Tools/SkyPreview/sky_preview.py <ordner>` (rechnet die Shader-Mathematik nach).
* **Gelände:** Höhenfeld-Mesh mit prozeduraler Textur; Verschmutzung nimmt mit der Reinigung ab, Begrünung wächst.
* **Müll:** alle Objekte per GPU-Instancing (`TrashRenderer`), Sichtweiten-Culling, Schatten nur in der Nähe.
* **Stadt erwacht:** Nach einem Projekt schalten sich Fenster und Laternen des Bereichs nacheinander ein,
  Brunnen laufen, Feuerwerk über dem Projektplatz.
* **Belebte Welt** (reine Darstellung, liest nur den replizierten Zustand, Core-Regeln unverändert; Grundlage:
  `LifeCommon` – Wiederherstellungsgrad je Bereich = Sauberkeit 40 % + Projekt 35 % + Ökologie 25 %, Qualitätsfaktor,
  Kollisionstest, `InstanceBatch` für Instancing in 1023er-Blöcken):
  * `Wildlife` + `AnimalMeshes`: Tiere kehren mit dem Wiederherstellungsgrad zurück (TERRA Spatzen, Hasen, Füchse ·
    PYRA Echsen, Falken, Sandfinken · PELAGIA Möwen, Krabben, zwei Fischarten · NIVALIS Pinguinvögel, Polarfüchse,
    Schneeammern). Heimatplätze deterministisch aus dem Planeten-Seed, Anzahl aus Zustand und Qualität → Mitspieler sehen
    dieselben Tiere an denselben Orten. Umherstreifen, Schwärme (Boids, Kreisen, Landen auf Dächern/Boden), Flucht vor
    Robotern und Fahrzeugen, nachts/im Sturm verstecken. Simulation/Zeichnung nur in Kameranähe.
  * `GroundMarks`: Reifen-/Kettenspuren auf Sand, Schnee, Erde (Ringpuffer 300–2000 Stücke je Qualität, verblassen in
    5 Transparenzstufen, Wind/Sturm verwehen), Staubwolken bei schneller Fahrt (über `FxView.Burst`), Pfützen nach
    Regen (PELAGIA; Nässe aus Sturmzustand und Sturmzeit – synchron für alle).
  * `WindLook`: Wolkenschatten (globale Shader-Werte `_RP_CloudShadow*`, ausgewertet im Zusammensetzen-Pass von
    `RePlanetPostFX.shader`; ohne Nachbearbeitung kein Effekt = sicherer Rückfall) und Wiegen der Ökologie-Pflanzen.
    `FloraRenderer` schert die Instanzmatrizen je Bild mit Windstärke/-richtung (CPU, kein Shader nötig).
  * `CityLife` („Stadt erwacht 2.0“): Anteil beleuchteter Fenster (`_LitShare` des Fenster-Shaders) und Brunnenstärke
    je Wiederherstellung, Elektroautos und (TERRA) Straßenbahnen auf vorab berechneten freien Straßenabschnitten
    (halten vor Robotern, Fahrzeugen, Autos, liegendem großem Müll; wenden), wehende Fahnen, Hologramme, Lichthöfe
    um Laternen. `WorldViewLife` stellt dafür schmale Zugänge auf WorldView bereit.
  * `MikoFace` (Teil von `RobotModel`) + `MikoGestures`: Stimmungen (fröhlich, neugierig, müde, ängstlich, schläfrig,
    frierend) verändern Augenform, Lider, Augenfarbe und Blick; Gesten (Winken, Freudensprung, Kopfneigen, Zittern,
    Gähnen) aus Serverereignissen (`OnFx`), dem verteilten Roboterlaut (`GameApp.OnEmote`) und Annäherung von
    Mitspielern – dadurch auch beim Mitspieler sichtbar.

## Erzähler im Spiel und Radio (`Core/Sim/Story.cs`)

* **Erzählerzeilen** (`Story.Lines`, 21 Stück, Dateien `Resources/Voice/game_01 … game_21`): Der Server löst sie aus den eigenen
  Ereignissen aus (`Game.OnStoryFx` hängt an `Game.Fx`: erste Landung je Planet, erste Einlagerung, erster Verkauf, erste
  Lieferung, erster Lichtpunkt, erster Bereich bei 85 % und bei 100 %, erstes Projekt, erster Planet komplett, erster Sturm,
  erster Sandsturm, erste Nacht, erster Morgen, erste Notabschaltung, NIVALIS frei, erster Mitspieler, erstes Fundstück, erste
  Ökologie). Jede Zeile nur einmal pro Spielstand (`WorldState.Narrated`), Clients bekommen das Ereignis `narrate` und spielen
  die Aufnahme über `Narrator.PlayGameLine` (Warteschlange, Musik wird abgesenkt; fehlt die Datei, nur Untertitel).
  Einstellung „Erzähler im Spiel“.
* **Radio** (`Story.Tracks`): Stücke sind die vorhandenen Musikstücke mit eigener Stem-Mischung; freigeschaltet je gereinigtem
  Bereich (85 %), je Großprojekt und nach dem Abspann (`WorldState.RadioUnlocked`, Ereignis `radio`). `AudioRadio` ersetzt im
  Spiel die Planetenmusik, spielt zwischen den Stücken synthetische Senderkennungen (`Synth.RadioJingles`), Sender „Radio
  Zweite Chance“ (alle) und je Planet. Taste T, Spielmenü-Reiter „Radio“.
* **Speicherteil `story`** (Radio + erzählte Zeilen): Fehlt er (ältere Spielstände), leitet `Game.InitStory` die Freischaltungen
  aus dem Fortschritt ab und markiert offensichtlich vergangene Anlässe als erzählt (`Story.InferNarrated`).

## Stadtklänge (`Runtime/Audio/AudioCity.cs`, Klänge in `Core/Audio/SynthCity.cs`)

Schichten wachsen mit der Wiederherstellung des Bereichs, in dem MIKO steht: Singvögel (TERRA, schwächer PYRA) bzw. Möwen
(PELAGIA) mit Sauberkeit und Ökologie, Blätterrauschen mit Begrünung und Wind, räumliche Brunnen-Quellen an den Brunnen
(sobald ihr Projekt fertig ist), fernes Stadtleben (Verkehr, Straßenbahn, Stimmen) mit der Zahl fertiger Projekte. Nacht,
Sturm und Unterschlupf dämpfen; Lautstärke = Einstellung „Umgebung“.

## Lokalisierung (`Core/Loc`)

* Deutsch ist Schlüssel und Kanon: Core und Server erzeugen immer deutsche Texte. Übersetzt wird bei der Anzeige
  (`Loc.T`): exakte Einträge, sonst **Vorlagen** mit Platzhaltern (`„{0}“ ist jetzt erreichbar!`), deren eingesetzte Werte
  ebenfalls übersetzt werden; zusätzlich Zerlegung von Aufzählungen, „Menge Name“ und „Name: Grund“. So werden auch Meldungen
  eines deutschen Hosts für einen englischen Gast übersetzt.
* Datentabellen (`GameData`) werden im Spielprozess mit `Loc.ApplyToData()` an Ort und Stelle übersetzt (Originale gemerkt).
* Oberfläche: `L("…")`/`Loc.T("…")` an Literalen, `UINav`-Bedienelemente übersetzen Beschriftungen selbst,
  Meldungen/Ziele/Gründe werden beim Zeichnen übersetzt (die HUD-Kurzformen arbeiten auf dem deutschen Kanon).
* Test `LocTests`: jeder deutsche Anzeigetext hat eine englische Übersetzung, Platzhalter und Leerzeichen stimmen.

## Erweiterungspunkte

* Neue Müllart: `GameData.DefineTrash` + Form in `MeshKit.Trash`.
* Neues Gebäude: `GameData.DefineBuildings` + Modell in `ActorsView.BuildMachine`.
* Neue Sprache: Kennung in `Loc.Languages`/`LanguageNames`, Tabelle wie `LocEn*.cs`; `LocTests` zeigt fehlende Einträge (`dotnet run -- locmissing`).
* Neue Erzählerzeile: `Story.L(...)` + Auslöser in `Game.OnStoryFx`, Text in `docs/SPRECHERTEXT_ELEVENLABS.md` und `Tools/Voice/generate_elevenlabs.py`.
* Neues Radio-Stück: `Story.R(...)` (Musikstück + Stem-Mischung) + Regel in `Story.Eligible`.
* Balancing: Werte in `Core/Data/GameData.cs`; Wirkung mit dem Kampagnen-Bot messen (`cd Tests && dotnet run -c Release -- balance`).
