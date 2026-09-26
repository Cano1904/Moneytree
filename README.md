# GLASSCORE

> *„Wer im Glashaus sitzt, sollte nicht mit Steinen werfen.“*

Physikbasierter Online-Multiplayer-Shooter (2–8 Spieler) auf schwebenden Glasplatten. Jeder
Schuss stößt dich zurück (drittes Newtonsches Gesetz) und drückt auf das Glas unter dir. Wer
leichtsinnig schießt, zerbricht sein eigenes Glashaus.

* **Unity 6 (C#)**, alles per Code erzeugt: eine leere Szene, keine Assets, keine Pakete von Drittanbietern
* **Autoritativer Server** mit Client-Prediction, Server-Reconciliation, Interpolation und Lag-Compensation
* **Deterministischer Voronoi-Glasbruch**: Der Server sendet nur `RPC_ShatterTile(tileID, impactPoint, force)`
* 3 Glasarten, 4 Stein-Waffen, Saugstiefel-Anker, 3 Arenen, Fracture-Cascade-Endphase
* Hauptmenü, Lobby mit Code, Quick Match (SBMM), Einstellungen, Tastenbelegung, Gamepad,
  Pause ohne Zeitstopp, Proximity-Voice-Chat, Highlight-Clip-Export
* Eigenständiger **Dedicated Server** (.NET 8) und **36 automatische Tests** (inklusive echtem Netzwerk-Match über Loopback)

Der komplette technische Entwurf (alle 8 Abschnitte der Spezifikation plus Timeline) steht in
[`docs/GLASSCORE_BLUEPRINT.md`](docs/GLASSCORE_BLUEPRINT.md).

---

## Schnellstart: Spiel bauen, dann PC automatisch ausschalten

**Voraussetzungen (einmalig):**
1. [Unity Hub](https://unity.com/download) installieren, anmelden und die kostenlose Personal-Lizenz aktivieren.
2. Im Hub **Unity 6 (6000.x LTS)** mit dem Modul **„Windows Build Support (Mono)“** installieren.
3. Optional das [.NET 8 SDK](https://dotnet.microsoft.com/download) für Tests und den Dedicated Server.
4. Optional [ffmpeg](https://ffmpeg.org/) im PATH, damit Clips automatisch als MP4 exportiert werden.

**Bauen und danach herunterfahren:**

```
scripts\build_and_shutdown.bat
```

Das Skript
1. führt die Tests aus (wenn .NET installiert ist),
2. baut den Dedicated Server nach `Build\Server\GlasscoreServer.exe`,
3. baut das Spiel mit Unity im Batch-Modus nach `Build\Windows\GLASSCORE.exe`
   (der erste Import dauert einige Minuten),
4. schreibt `Build\BUILD_REPORT.txt` und legt eine Kopie auf den Desktop,
5. fährt den PC **erst danach** herunter. Du hast 120 Sekunden Vorlauf und kannst mit `shutdown /a` abbrechen.

Optionen:

| Aufruf | Wirkung |
|---|---|
| `scripts\build_only.bat` | nur bauen, PC bleibt an |
| `build_and_shutdown.bat -ShutdownOnlyOnSuccess` | PC bleibt an, wenn der Build fehlschlägt |
| `build_and_shutdown.bat -ShutdownDelaySeconds 600` | 10 Minuten Vorlauf vor dem Ausschalten |
| `build_and_shutdown.bat -UnityPath "D:\Unity\6000.0.40f1\Editor\Unity.exe"` | bestimmten Unity-Editor verwenden |

Alternativ im Editor: Projektordner `Glasscore/` im Unity Hub öffnen, dann **GLASSCORE → Setup Project**,
die Szene `Assets/Scenes/Glasscore.unity` öffnen und auf **Play** drücken.

---

## Spielen

* **Im LAN:** Ein Spieler wählt **CREATE LOBBY** und gibt den 6-stelligen Code weiter. Die anderen wählen
  **JOIN LOBBY** und geben den Code ein. Alle drücken **READY**, der Host drückt **START GAME**.
  Die Windows-Firewall-Abfrage beim ersten Start mit „Zulassen“ bestätigen.
* **Quick Match** sucht offene öffentliche Lobbys passend zu deinem Rating. Findet es nichts, eröffnet es
  nach 15 Sekunden selbst eine Lobby, der andere Suchende beitreten.
* **Über das Internet:** `scripts\run_dedicated_server.bat` auf einem Rechner mit freigegebenem Port
  **27015 TCP+UDP** starten. Die Spieler tragen die Server-Adresse unter *Settings → Online* ein oder
  geben bei JOIN LOBBY direkt `adresse:27015` ein.
* Zwei Spiel-Fenster auf einem PC zum Testen sind möglich (der zweite Host nutzt automatisch Port 27016).

| Aktion | Tastatur & Maus | Gamepad |
|---|---|---|
| Bewegen / Zielen | WASD / Maus | linker / rechter Stick |
| Springen (beschädigt zerbrechliches Glas um 10) | Leertaste | A / Kreuz |
| Feuern (mit Rückstoß) | Linksklick | RT / R2 |
| Saugstiefel-Anker (1,5 s, doppelte Last) | Rechtsklick | LT / L2 |
| Waffe wechseln | Q / E / Mausrad | LB / RB |
| Push-to-Talk / Mikro stumm | V / M | – / Steuerkreuz unten |
| Pause (Match läuft weiter) | Esc | START / OPTIONS |

---

## Projektstruktur

```
Glasscore/                       Unity-Projekt
  Assets/Scripts/Simulation/     Spielregeln, Voronoi, Bewegung, Match-Ablauf (engine-unabhängig)
  Assets/Scripts/Net/            TCP/UDP-Netcode, Lobby-Suche, GameServer, GameClient
  Assets/Scripts/Client/         Rendering, UI, Audio-Synthese, Voice-Chat, Clips
  Assets/Scripts/Editor/         Build-Automatisierung
  Assets/Resources/Shaders/      Glas-, Neon-, Lit-, Himmel- und Blur-Shader
  ProjectSettings/InputManager.asset   Gamepad-Achsen
server/Glasscore.Server/         Headless Dedicated Server (.NET 8)
tests/Glasscore.Tests/           36 xUnit-Tests
tests/UnityCompileCheck/         kompiliert alle Unity-Skripte gegen Unity-Referenzbibliotheken
scripts/                         build_and_shutdown.ps1/.bat, build_only.bat, run_dedicated_server.bat
docs/GLASSCORE_BLUEPRINT.md      technischer Entwurf
```

Entwicklung ohne Unity:

```
dotnet test tests/Glasscore.Tests            # Regeln, Netcode, Loopback-Match
dotnet build tests/UnityCompileCheck         # Unity-API-Check aller Skripte
dotnet run --project server/Glasscore.Server -- --port 27015
```
