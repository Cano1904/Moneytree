# RE:PLANET – Eine zweite Chance

Ein 3D-Recycling-Aufbauspiel für 1–4 Spieler. Du bist **MIKO**, ein kleiner Recyclingroboter, der als Einziger
nicht aufgegeben hat: Er räumt vermüllte Planeten auf, sortiert und presst Material, verkauft es, rüstet sich auf,
baut einen Stützpunkt aus und bringt mit Großprojekten Licht, Wasser und Leben zurück – bis das Signal an die
Arche HORIZONT gesendet werden kann. Drei Startplaneten sind frei wählbar (TERRA, PYRA, PELAGIA), NIVALIS ist das Finale.

Das Spiel ist mit **Unity** (Built-in Render Pipeline) umgesetzt. Grafik, Sounds und Musik werden vollständig
prozedural im Projekt erzeugt – es gibt keine Fremd-Assets.

> Spielidee, Planeten und Ablauf: [docs/GDD.md](docs/GDD.md) · Technik: [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md)

---

## Stand – was geprüft ist und was nicht

| Bereich | Wie geprüft | Ergebnis |
| --- | --- | --- |
| Spiellogik, Wirtschaft, Speichern, Tag/Nacht/Wetter, komplette Solo-Kampagne per Bot (reines C#, `Core/`) | 40 automatisierte .NET-Tests in `Tests/` (`cd Tests && dotnet run`), Details in `docs/TESTBERICHT.md` | 40/40 bestanden |
| Koop-Logik (4 TCP-Clients, gleichzeitiges Greifen, doppelte Aktionen, Gastrechte, später Beitritt, Wiederverbinden, Host-Verlassen, Betrugsversuche) | `Tests/NetTests.cs` über Loopback-TCP | bestanden (kein Test über echte Internetverbindungen) |
| Koop-Netzwerk über TCP (Sitzung erstellen, Beitritt per Code, Sichtbarkeit, Speichern auf dem Server) | Rauchtest `Server/SmokeTest` gegen den dedizierten Server | läuft ohne Unity |
| Unity-Skripte (`Runtime/`, `Editor/`) | kompiliert gegen Unity-Referenz-Assemblies (`Tools/CompileCheck`) | kompiliert fehlerfrei |
| Spiel in Unity (Darstellung, Eingabe, Klang, Oberfläche) | **nicht getestet** | Unity war in der Entwicklungsumgebung nicht verfügbar |
| Windows-Anwendung (.exe) | **nicht im Repository** | muss in Unity über das Menü **RE:PLANET › Windows-Build erstellen (64 Bit)** erzeugt werden |

Ehrlich gesagt: Es hat noch niemand das Spiel in Unity laufen sehen. Die Unity-Schicht ist gegen die
Schnittstellen der Engine kompiliert, aber Laufzeitfehler, Darstellungs- oder Balancing-Probleme in Unity sind möglich.
Bitte Auffälligkeiten mit der Datei `Player.log` bzw. dem Editor-Log melden (siehe [Speicherorte](#speicherorte)).

---

## Voraussetzungen

- **Unity Hub** und eine dieser Unity-Versionen (empfohlen):
  - **Unity 6 LTS** (6000.0.x) oder
  - **Unity 2022.3 LTS**
- Beim Installieren das Modul **„Windows Build Support (Mono)“** auswählen (unter Windows ist es beim Editor
  meist schon dabei; unter macOS/Linux muss es für Windows-Builds nachinstalliert werden).
- Keine weiteren Pakete: `RePlanet/Packages/manifest.json` enthält nur eingebaute Unity-Module. Kein Input-System-Paket,
  keine URP/HDRP – das Spiel nutzt die Built-in Render Pipeline und den klassischen Input Manager.
- Optional für Tests, Server und Werkzeuge: **.NET 8 SDK**.

## Projekt öffnen und spielen

1. Unity Hub › **Add** › **Add project from disk** › den Ordner **`RePlanet/`** in diesem Repository wählen
   (nicht die Repository-Wurzel).
2. Das Repository enthält bewusst keine `ProjectVersion.txt`. Der Hub fragt deshalb nach der Editor-Version –
   eine installierte 6000.0.x- oder 2022.3.x-Version wählen und bestätigen.
3. Beim ersten Öffnen läuft das Setup automatisch (Konsole: Meldungen mit `[RE:PLANET]`). Es legt an bzw. stellt ein:
   - Material-Vorlagen in `Assets/RePlanet/Resources/` (damit die benötigten Shader-Varianten im Build landen),
   - die leere Startszene `Assets/RePlanet/Scenes/Main.unity` und trägt sie in die Build-Liste ein,
   - Grafik-Einstellungen (zusätzlich eingebundene Shader, Nebel-Varianten),
   - Player-Einstellungen (Name „RE PLANET“, Firma „RE PLANET Projekt“, Linear-Farbraum, Vollbild-Fenster,
     „Run In Background“, klassischer Input Manager).

   Fordert Unity wegen des Eingabesystems zu einem Neustart auf: bestätigen. Das Setup lässt sich jederzeit über
   **RE:PLANET › Projekt einrichten** erneut ausführen; es überschreibt nichts unnötig.
4. **Play** drücken. Kamera, Licht, Welt und Oberfläche erzeugt das Spiel zur Laufzeit selbst – die Szene darf leer sein.

## Windows-Build erstellen

- Menü **RE:PLANET › Windows-Build erstellen (64 Bit)**. Vorher läuft automatisch das Setup.
  Ergebnis: `RePlanet/Builds/Windows/RePlanet.exe`. Ein Dialog zeigt Erfolg, Größe und Dauer bzw. die ersten Fehler.
- **Zum Weitergeben den ganzen Ordner `Builds/Windows/` verteilen** (z. B. als ZIP) – die `.exe` allein startet nicht.
- Kommandozeile (z. B. für automatische Builds; Pfad zur Unity.exe anpassen):

  ```
  "C:\Program Files\Unity\Hub\Editor\<Version>\Editor\Unity.exe" -batchmode -quit -projectPath RePlanet ^
      -executeMethod RePlanet.EditorTools.RePlanetBuild.BuildWindowsCI -logFile build.log
  ```

  Bei einem Fehlschlag endet Unity mit Exitcode 1. Optional `-rpOutput <Pfad\RePlanet.exe>` für einen anderen Zielort.
- Die erzeugte `.exe` ist nicht signiert; Windows SmartScreen fragt beim ersten Start eventuell nach („Weitere Informationen › Trotzdem ausführen“).

## Steuerung

Alle Tastatur- und Mausbelegungen sind im Spiel unter Einstellungen › Steuerung frei änderbar (Quelle: `Runtime/Game/InputMap.cs`).

| Aktion | Tastatur / Maus | Controller (Xbox-Layout, XInput) |
| --- | --- | --- |
| Bewegen | W A S D | linker Stick |
| Kamera | Maus | rechter Stick |
| Kamera-Abstand | Mausrad | – |
| Sprinten | Umschalt | L3 (linken Stick drücken) |
| Interagieren | E | A |
| Werkzeug benutzen | linke Maustaste | RT |
| Magnet aufladen / Zweitfunktion | rechte Maustaste | LT |
| Werkzeug direkt wählen | 1 Greifarm · 2 Müllsauger · 3 Magnetarm · 4 Schneidgerät · 5 Wärmemodul · 6 Filtermodul · 7 Bio-Modul | – |
| Vorheriges / nächstes Werkzeug | frei belegbar (standardmäßig keine Taste) | LB / RB |
| Pressen | R | X |
| Ein-/Aussteigen (Fahrzeug) | F | Y |
| Fahrzeug zurücksetzen | X | – |
| Auftauchen / Abtauchen (PELAGIA) | Leertaste / C (oder Strg) | A / B |
| Schlafen (nachts oder im Sturm, im Unterschlupf) | Z | Steuerkreuz ↓ |
| Notunterschlupf bauen | N | – |
| Roboterlaut (Emote) | G | Steuerkreuz ← |
| Spielmenü | Tab | Steuerkreuz ↑ |
| Bauansicht | B | Steuerkreuz → |
| Bauwerk drehen (in der Bauansicht) | R | – |
| Karte | M | Back / View |
| Aufträge | J | – |
| Inventar | I | – |
| Fotomodus | P (Kamera auf/ab: E / Q) | R3 (rechten Stick drücken) |
| Schnellspeichern | F5 | – |
| Pause | Esc | Start / Menu |
| Menüs | Pfeiltasten, Eingabe, Esc, Q/E für Reiter | Steuerkreuz/linker Stick, A, B, LB/RB |
| Intro/Abspann überspringen | Esc, Eingabe oder Leertaste gedrückt halten | A gedrückt halten |

Controller-Achsen sind in `RePlanet/ProjectSettings/InputManager.asset` als `RP_LX … RP_DY` für XInput unter Windows
eingetragen. Hinweis: Das Spiel wertet Tasten nach ihrer **Beschriftung** aus (auf deutschen Tastaturen ist „Z“ die Taste neben „T“).

## Koop (1–4 Spieler)

Ausführlich: [docs/KOOP.md](docs/KOOP.md). Kurzfassung:

- **Host:** im Spiel **Pause › Koop › „Welt für Mitspieler öffnen“**. Das Spiel zeigt eine Einladung im Format
  **`IP:Port/CODE`** (z. B. `192.168.0.10:7777/K7M2QX`). Diese an die Mitspieler schicken.
- **Mitspieler:** im Koop-Menü **Beitreten** wählen und die Einladung einfügen.
- **Netzwerk:** Der Host braucht einen erreichbaren **TCP-Port 7777** (in den Einstellungen änderbar):
  - im **LAN** reicht die Freigabe in der Windows-Firewall (beim ersten Öffnen fragt Windows nach – „Private Netzwerke“ erlauben);
  - übers **Internet** den Port im Router auf den Host-PC **weiterleiten**, oder
  - ein virtuelles LAN nutzen, z. B. **Tailscale** oder **ZeroTier** (dann die dort angezeigte IP in der Einladung verwenden).
- **Dedizierter Server** (ohne Unity, .NET 8): `dotnet run --project Server -- --port 7777`
  (Optionen: `--help`). Er öffnet eine dauerhafte Welt und zeigt die Einladung im Log an.
- Es werden **keine externen, kostenpflichtigen Dienste** benötigt – weder Accounts noch Relay-Server.

## Speicherorte

Alle Daten liegen in Unitys `Application.persistentDataPath`, unter Windows:

```
%USERPROFILE%\AppData\LocalLow\RE PLANET Projekt\RE PLANET\
├── saves\          Spielstände: auto.rpsave, slot1.rpsave … slot3.rpsave (+ jeweils .bak.rpsave als Sicherung)
├── Fotos\          Fotomodus: RePlanet_<planet>_<datum>[_vorher][_hochformat].png
├── settings.json   Einstellungen (Grafik, Audio, Steuerung, Barrierefreiheit, Koop-Port)
├── profile.json    Spielerprofil (Spieler-ID, persönliche Kosmetik, Wiederverbindungs-Codes)
└── Player.log      Unity-Protokoll des Spiels (für Fehlermeldungen)
```

Der Ordner gilt für das gebaute Spiel und für den Play-Modus im Editor gleichermaßen (gleicher Firmen- und Produktname).
Im Editor öffnet **RE:PLANET › Spielstandordner öffnen** den Ordner direkt. Spielstände sind Textdateien mit Prüfsumme;
ein beschädigter Stand wird automatisch aus der `.bak`-Sicherung geladen. Der dedizierte Server speichert in seinem eigenen
Ordner (`--saves`, Standard `./server-saves/<Sitzungscode>.rpsave`).

## Tests und Werkzeuge (ohne Unity, .NET 8 SDK)

```bash
cd Tests && dotnet run                          # automatisierte Tests (Ergebnis zusätzlich in test-results.txt)
cd Tests && dotnet run -c Release -- balance    # Kampagnen-Bot für Balancing-Messungen

# Unity-Skripte gegen Referenz-Assemblies kompilieren (einmalig: Tools/CompileCheck/fetch-unity-refs.sh)
cd Tools/CompileCheck/Core    && dotnet build
cd Tools/CompileCheck/Runtime && dotnet build
cd Tools/CompileCheck/Editor  && dotnet build

# Dedizierter Server + Rauchtest (zweites Terminal)
dotnet run --project Server -- --port 7777 --saves ./server-saves
dotnet run --project Server/SmokeTest -- --host 127.0.0.1 --port 7777 --saves ./server-saves

dotnet run --project Tools/DocGen                # erzeugt docs/WIRTSCHAFT.md aus den Spieldaten neu
python3 Tools/SkyPreview/sky_preview.py <ordner> # Vorschaubilder des Himmels-Shaders ohne Unity
```

## Projektstruktur

```
RePlanet/                     Unity-Projekt (diesen Ordner im Unity Hub öffnen)
├── Assets/RePlanet/
│   ├── Core/                 reine Spiellogik ohne UnityEngine: Daten, Welt, Simulation, Speichern, Netzwerk, Klangsynthese
│   ├── Runtime/              Unity-Schicht: Ablauf, Eingabe, Kamera, Darstellung, Audio, Oberfläche
│   ├── Editor/               Projekt-Setup und Build-Menü (RePlanetSetup.cs, RePlanetBuild.cs)
│   └── Resources/            eigener Himmels-Shader + vom Setup erzeugte Material-Vorlagen
├── Packages/manifest.json    nur eingebaute Unity-Module
└── ProjectSettings/          InputManager.asset (Controller-Achsen); fehlende Einstellungen erzeugt Unity selbst
Server/                       dedizierter Koop-Server (.NET 8) und Rauchtest-Client (Server/SmokeTest)
Tests/                        automatisierte Tests der Spiellogik (.NET 8)
Tools/CompileCheck/           Kompilierprüfung der Unity-Skripte ohne Unity
Tools/DocGen/                 Generator für docs/WIRTSCHAFT.md
Tools/SkyPreview/             Himmelsvorschau in Python
docs/                         Dokumentation und Konzeptkunst
```

## Dokumentation

- [docs/GDD.md](docs/GDD.md) – Game-Design-Dokument (Spielschleife, Planeten, Werkzeuge, Geschichte …)
- [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md) – technischer Aufbau
- [docs/KOOP.md](docs/KOOP.md) – Koop, Ports, Firewall, dedizierter Server, Fehlermeldungen
- [docs/WIRTSCHAFT.md](docs/WIRTSCHAFT.md) – automatisch erzeugte Preis- und Balancing-Tabellen
- [docs/LIZENZEN.md](docs/LIZENZEN.md) – Herkunft aller Inhalte, Marken- und Namenshinweise
- [docs/CLIP_IDEEN.md](docs/CLIP_IDEEN.md) – Trailer- und Clip-Ideen aus echten Spielmechaniken
- [docs/concept/](docs/concept/) – Konzeptkunst (keine Spielszenen)
