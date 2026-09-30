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
│   ├── Audio/     Prozedurale Klangsynthese (Effekte, Musik-Stems, Intro-Score) + Intro-Zeitplan
│   └── Loc/       Lokalisierung (Deutsch = Schlüssel, weitere Sprachen als Tabellen)
├── Runtime/     Unity-Schicht (MonoBehaviours, alles wird zur Laufzeit erzeugt – keine Prefabs nötig)
│   ├── Game/      GameApp (Ablaufsteuerung), Settings/Profile, InputMap, Bridge (HUD-/UI-Zustand),
│   │              CameraRig, PlayerController, IntroDirector, EndingDirector
│   ├── Render/    WorldView (+Props), Atmosphere (Himmel/Licht/Wetter), TrashRenderer (Instancing),
│   │              ActorsView (Roboter, Fahrzeuge, Drohnen, Anlagen), FxView (Effekte), FloraRenderer,
│   │              RobotModel (MIKO), MeshKit (prozedurale Formen), Mats (Materialien),
│   │              ShipArrival (Containerfrachter bei Lieferungen, Landeanflug; Geometrie: FreighterModel),
│   │              PlanetSelectScene (Planetenwahl als Weltall-Szene mit Anflug),
│   │              MapCamera (3D-Karte: eigene Kamera schräg von oben in eine RenderTexture, nur bei offener Karte)
│   ├── Audio/     AudioManager (Clips aus Core/Audio, Musikschichten, 3D-Effekte, Ambience)
│   └── UI/        UIRoot (IMGUI: Hauptmenü, Einstellungen, HUD, Spielmenü, Karte (3D mit Symbolen, 2D-Rückfall), Bau-/Fotomodus);
│                  HudHints: ruhiges HUD je Einstellung „Hinweise“ (Aus / Minimal / Ausführlich)
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

## Darstellung

* **Himmel:** eigener Shader (`Resources/RePlanetSky.shader`) mit Farbverläufen, Dunstband, Sonne, animierten Wolken
  auf einer gekrümmten Wolkenschicht mit Selbstverschattung, Nebel, Sternen, zwei Himmelskörpern mit Atmosphärensaum
  und Polarlicht. `Atmosphere` mischt je Planet die Paletten für Tag, Dämmerung, Nacht und Sturm.
  Vorschau ohne Unity: `python3 Tools/SkyPreview/sky_preview.py <ordner>` (rechnet die Shader-Mathematik nach).
* **Gelände:** Höhenfeld-Mesh mit prozeduraler Textur; Verschmutzung nimmt mit der Reinigung ab, Begrünung wächst.
* **Müll:** alle Objekte per GPU-Instancing (`TrashRenderer`), Sichtweiten-Culling, Schatten nur in der Nähe.
* **Stadt erwacht:** Nach einem Projekt schalten sich Fenster und Laternen des Bereichs nacheinander ein,
  Brunnen laufen, Feuerwerk über dem Projektplatz.

## Erweiterungspunkte

* Neue Müllart: `GameData.DefineTrash` + Form in `MeshKit.Trash`.
* Neues Gebäude: `GameData.DefineBuildings` + Modell in `ActorsView.BuildMachine`.
* Neue Sprache: Tabelle in `Core/Loc/Loc.cs`.
* Balancing: Werte in `Core/Data/GameData.cs`; Wirkung mit dem Kampagnen-Bot messen (`cd Tests && dotnet run -c Release -- balance`).
