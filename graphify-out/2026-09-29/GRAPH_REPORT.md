# Graph Report - Moneytree  (2026-09-28)

## Corpus Check
- 87 files · ~228,057 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 8 file(s) not represented in the graph (top: .asmdef 3, .shader 2, (none) 1)

## Summary
- 2226 nodes · 7616 edges · 87 communities (70 shown, 17 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 893 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7ec54d1e`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Game
- .HeightAt
- GameData
- CampaignBot
- UIRoot
- GameApp
- WorldState
- RePlanetSetup
- .Col
- GameClient
- .True
- WorldView
- Arr
- PlayerController
- Synth
- UISkin
- AudioManager
- UINav
- MeshBuilder
- sky_preview.py
- GameAction
- V3
- Atmosphere
- .Next
- FxView
- system_collections_generic
- UIScreen
- RobotModel
- .BuildProps
- IntroDirector
- Transport.cs
- LocalServerTransport
- .Station
- RePlanet.Core
- .Get
- InputMap
- manifest.json
- .Rect
- ActorsView
- SessionHub
- .Play
- TcpClientTransport
- TcpServerTransport
- RE:PLANET – Eine zweite Chance · Game-Design-Dokument
- HostServer
- .L
- TrashRenderer
- .Sfx
- .SetEmission
- ObjView
- RE:PLANET – Klang und Musik
- .Line
- CameraRig
- SaveStore
- .Produce
- TestRunner.cs
- .Main
- RE:PLANET – Eine zweite Chance
- BitSet
- RE:PLANET – Wirtschaftstabellen
- .Range
- EndingDirector
- .Run
- RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise
- RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte
- GameApp.cs
- RePlanetBuild.cs
- RE:PLANET – Architektur
- RE:PLANET – Trailer- und Clip-Ideen
- .Awake
- Automatisierte Tests und Balancing
- Müll je Planet und Bereich
- Großprojekte
- Ids
- NetEventType
- Cat
- Loc.cs
- RePlanet.Server.csproj
- SmokeTest.csproj
- RePlanet.Tests.csproj
- Core.csproj
- Editor.csproj
- Runtime.csproj
- DocGen.csproj
- fetch-unity-refs.sh

## God Nodes (most connected - your core abstractions)
1. `UIRoot` - 156 edges
2. `Game` - 126 edges
3. `GameApp` - 119 edges
4. `JObj` - 106 edges
5. `WorldView` - 78 edges
6. `WorldState` - 77 edges
7. `AudioManager` - 77 edges
8. `Synth` - 76 edges
9. `PlayerData` - 74 edges
10. `V3` - 73 edges

## Surprising Connections (you probably didn't know these)
- `Welt und Speicherung` --references--> `WorldGen`  [INFERRED]
  docs/ARCHITEKTUR.md → RePlanet/Assets/RePlanet/Core/World/WorldGen.cs
- `Ergebnis des Testlaufs` --references--> `MotorEnv`  [INFERRED]
  docs/TESTBERICHT.md → RePlanet/Assets/RePlanet/Core/Motion/Motor.cs
- `3. Ablauf einer Verbindung` --references--> `Interp`  [INFERRED]
  docs/KOOP.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs
- `Ergebnis des Testlaufs` --references--> `GameClient`  [INFERRED]
  docs/TESTBERICHT.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs
- `Eine Simulation – drei Betriebsarten` --references--> `HostServer`  [INFERRED]
  docs/ARCHITEKTUR.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs

## Import Cycles
- None detected.

## Communities (87 total, 17 thin omitted)

### Community 0 - "Game"
Cohesion: 0.05
Nodes (35): Dictionary, Durch Tests gefundene und behobene Fehler in der Spiellogik, Kampagnen-Bot (`dotnet run -c Release -- balance`), RateLimit, Action, Dictionary, Queue, RateLimit (+27 more)

### Community 1 - ".HeightAt"
Cohesion: 0.06
Nodes (33): Ctx, Kind, Plant, List, Motor, MotorEnv, Tmp, MoverState (+25 more)

### Community 2 - "GameData"
Cohesion: 0.06
Nodes (32): CultureInfo, Erweiterungspunkte, Dictionary, List, BuildingDef, CosmeticDef, GateDef, Grade (+24 more)

### Community 3 - "CampaignBot"
Cohesion: 0.10
Nodes (15): Stopwatch, BalanceRun, CampaignBot, L, PS, S, Shutdowns, T (+7 more)

### Community 4 - "UIRoot"
Cohesion: 0.06
Nodes (27): KeyCode, MapMark, MapTex, UIRoot, List, Dictionary, List, Color (+19 more)

### Community 5 - "GameApp"
Cohesion: 0.06
Nodes (24): UIState, BlocksGameplay, Action, List, GameApp, CoopActive, CoopOpen, I (+16 more)

### Community 6 - "WorldState"
Cohesion: 0.08
Nodes (13): Stems, Dictionary, List, Rules, Dictionary, HashSet, PlanetState, StorageEntry (+5 more)

### Community 7 - "RePlanetSetup"
Cohesion: 0.07
Nodes (26): Changes, MaterialGlobalIlluminationFlags, MatSpec, MenuItem, RePlanetBuild, Result, Action, Color (+18 more)

### Community 8 - ".Col"
Cohesion: 0.14
Nodes (4): GUIStyle, Rect, GUIStyle, Texture

### Community 9 - "GameClient"
Cohesion: 0.06
Nodes (25): Action, Dictionary, List, GameClient, Me, PendingCount, RenderTime, Interp (+17 more)

### Community 10 - ".True"
Cohesion: 0.14
Nodes (14): Ergebnis des Testlaufs, Exception, Func, Test, EconomyTests, TestHelpers, Test, NetTests (+6 more)

### Community 11 - "WorldView"
Cohesion: 0.06
Nodes (28): Font, Noise, Color, Color32, Dictionary, GameObject, KeyValuePair, List (+20 more)

### Community 12 - "Arr"
Cohesion: 0.16
Nodes (4): LineDef, Arr, BarLen, Beat

### Community 13 - "PlayerController"
Cohesion: 0.11
Nodes (16): Interaction, VehicleState, Def, Action, GameObject, MeshFilter, MeshRenderer, Vector3 (+8 more)

### Community 14 - "Synth"
Cohesion: 0.13
Nodes (12): Arr, Chord, MusicSet, Dictionary, Chord, BassNote, Top, MusicSet (+4 more)

### Community 15 - "UISkin"
Cohesion: 0.14
Nodes (10): GUIContent, Color, Dictionary, GUIStyle, Rect, Texture2D, Vector2, UISkin (+2 more)

### Community 16 - "AudioManager"
Cohesion: 0.08
Nodes (18): AudioClip, Job, LoopVoice, MusicClips, Dictionary, HashSet, List, Queue (+10 more)

### Community 17 - "UINav"
Cohesion: 0.13
Nodes (11): Dictionary, GUIStyle, List, Rect, Stack, Vector2, Vector2Int, Vector3 (+3 more)

### Community 18 - "MeshBuilder"
Cohesion: 0.14
Nodes (15): Action, Color, Dictionary, List, Matrix4x4, Mesh, Vector2, Vector3 (+7 more)

### Community 19 - "sky_preview.py"
Cohesion: 0.11
Nodes (32): glob, math, numpy, os, pil, re, subprocess, sys (+24 more)

### Community 20 - "GameAction"
Cohesion: 0.06
Nodes (35): GameAction, AltTool, Build, DiveDown, DiveUp, Emote, Interact, Inventory (+27 more)

### Community 21 - "V3"
Cohesion: 0.08
Nodes (12): IDisposable, V3, IsFinite, Length, LengthXZ, Dictionary, BaseLayout, Func (+4 more)

### Community 22 - "Atmosphere"
Cohesion: 0.11
Nodes (22): Palette, PlanetSky, ReflectionProbe, Camera, Color, Dictionary, Light, List (+14 more)

### Community 23 - ".Next"
Cohesion: 0.23
Nodes (3): OnePole, PNoise, Svf

### Community 24 - "FxView"
Cohesion: 0.11
Nodes (21): Arc, Ghost, IEnumerator, Color, LineRenderer, List, Material, Mesh (+13 more)

### Community 25 - "system_collections_generic"
Cohesion: 0.18
Nodes (5): RePlanet, Terrain, system_collections_generic, unityengine, unityengine_rendering

### Community 26 - "UIScreen"
Cohesion: 0.07
Nodes (30): List, Vector3, BuildMode, Hud, PhotoMode, Toast, ToastKind, Error (+22 more)

### Community 27 - "RobotModel"
Cohesion: 0.17
Nodes (12): Color, Dictionary, GameObject, Light, Material, Mesh, MeshFilter, MeshRenderer (+4 more)

### Community 28 - ".BuildProps"
Cohesion: 0.13
Nodes (14): MonoBehaviour, Color, GameObject, Mesh, MeshFilter, MeshRenderer, ParticleSystem, ParticleSystemRenderer (+6 more)

### Community 29 - "IntroDirector"
Cohesion: 0.11
Nodes (13): CameraClearFlags, Action, Color, Dictionary, GameObject, List, Material, Mesh (+5 more)

### Community 30 - "Transport.cs"
Cohesion: 0.11
Nodes (15): RePlanet.DocGen, RePlanet.Server, RePlanet.SmokeTest, IntroTimeline, Shot, Shot, system_collections, system_collections_concurrent (+7 more)

### Community 31 - "LocalServerTransport"
Cohesion: 0.12
Nodes (9): ConcurrentQueue, Dictionary, List, LocalClientTransport, Connected, Error, Failed, LocalServerTransport (+1 more)

### Community 32 - ".Station"
Cohesion: 0.28
Nodes (7): Test, LogicTests, Game, Func, List, Test, WeatherTests

### Community 33 - "RePlanet.Core"
Cohesion: 0.18
Nodes (6): RePlanet.Core, EnergyInfo, ProjectState, system, system_diagnostics, system_linq

### Community 34 - ".Get"
Cohesion: 0.21
Nodes (8): GameObject, LineRenderer, GameObject, Material, MeshFilter, MeshRenderer, Transform, MultiBuilder

### Community 35 - "InputMap"
Cohesion: 0.14
Nodes (5): Dictionary, Vector2, Vector2Int, InputMap, UsingPad

### Community 36 - "manifest.json"
Cohesion: 0.09
Nodes (21): com.unity.modules.animation, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem, com.unity.modules.physics, com.unity.modules.screencapture (+13 more)

### Community 38 - "ActorsView"
Cohesion: 0.15
Nodes (12): Dictionary, List, Material, Mesh, MeshFilter, MeshRenderer, Quaternion, Transform (+4 more)

### Community 39 - "SessionHub"
Cohesion: 0.13
Nodes (6): Func, IEnumerable, List, SessionHub, Sessions, IServerTransport

### Community 40 - ".Play"
Cohesion: 0.19
Nodes (5): AudioSource, Cat, Vector3, LoopVoice, Voice

### Community 41 - "TcpClientTransport"
Cohesion: 0.16
Nodes (12): BlockingCollection, 3. Ablauf einer Verbindung, Schutzmechanismen, NetworkStream, Conn, Framing, TcpClientTransport, Connected (+4 more)

### Community 42 - "TcpServerTransport"
Cohesion: 0.18
Nodes (6): Conn, Thread, NetIds, TcpServerTransport, Port, TcpListener

### Community 43 - "RE:PLANET – Eine zweite Chance · Game-Design-Dokument"
Cohesion: 0.11
Nodes (18): 10. Geschichte, 11. Koop, 12. Speichern, 13. Fotomodus, 14. Barrierefreiheit und Komfort, 15. Präsentation, 1. Vision, 2. Die Spielschleife (+10 more)

### Community 44 - "HostServer"
Cohesion: 0.12
Nodes (16): 10. Fehlermeldungen und was sie bedeuten, 1. Überblick, 2. Koop starten, 4. Rechte und Vertrauensmodus, 5. Gemeinsam spielen – was sich im Koop ändert, 6. Späte Beitritte, Verlassen und Wiederverbinden, 7. Speichern im Koop, 8. Netzwerk einrichten: Ports, Firewall, Router (+8 more)

### Community 46 - "TrashRenderer"
Cohesion: 0.21
Nodes (10): Batch, Dictionary, List, Material, Matrix4x4, Mesh, Batch, TrashRenderer (+2 more)

### Community 48 - ".SetEmission"
Cohesion: 0.24
Nodes (7): Color, Dictionary, Material, Mats, Material, MeshFilter, MeshRenderer

### Community 49 - "ObjView"
Cohesion: 0.22
Nodes (4): HashSet, IEnumerable, ObjView, Vector3

### Community 50 - "RE:PLANET – Klang und Musik"
Cohesion: 0.15
Nodes (8): Ambience und Effekte, Architektur, Intro-Score (100 s), Musik-Engine, Prüfen ohne Unity, RE:PLANET – Klang und Musik, Stücke und Stimmungen, Job

### Community 51 - ".Line"
Cohesion: 0.21
Nodes (7): MelNote, Note, List, LineDef, MelNote, Spec, Spec

### Community 52 - "CameraRig"
Cohesion: 0.19
Nodes (6): RenderTexture, List, Vector3, CameraRig, Cam, I

### Community 54 - ".Produce"
Cohesion: 0.24
Nodes (3): Product, Hash, Product

### Community 55 - "TestRunner.cs"
Cohesion: 0.18
Nodes (6): Attribute, system_reflection, Probe, Program, TestAttribute, TestRunner

### Community 56 - ".Main"
Cohesion: 0.33
Nodes (3): IDisposable, List, Program

### Community 57 - "RE:PLANET – Eine zweite Chance"
Cohesion: 0.18
Nodes (11): Dokumentation, Koop (1–4 Spieler), Projekt öffnen und spielen, Projektstruktur, RE:PLANET – Eine zweite Chance, Speicherorte, Stand – was geprüft ist und was nicht, Steuerung (+3 more)

### Community 58 - "BitSet"
Cohesion: 0.27
Nodes (4): IEnumerable, BitSet, Capacity, Count

### Community 59 - "RE:PLANET – Wirtschaftstabellen"
Cohesion: 0.20
Nodes (10): Aufträge (Missionen), Fahrzeuge, Gebäude (Stützpunkt), Grundwerte, Materialien, RE:PLANET – Wirtschaftstabellen, Recyclingaufträge (Auftragstafel), Reparaturen, Ökologie, Unterschlupf (+2 more)

### Community 60 - ".Range"
Cohesion: 0.29
Nodes (5): IList, Rng, Light, ParticleSystem, ParticleSystemRenderer

### Community 61 - "EndingDirector"
Cohesion: 0.24
Nodes (4): Action, List, Transform, EndingDirector

### Community 63 - "RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise"
Cohesion: 0.25
Nodes (8): 1. Was im Projekt steckt und woher es kommt, 2. Unity, 3. Weitere Werkzeuge, 4. Namen und Marken, „RE:PLANET“, RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise, WALL·E, Weitere Marken

### Community 64 - "RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte"
Cohesion: 0.25
Nodes (7): Entscheidungen, Grenzen (Stand dieser Umgebung), Nächste Schritte, Prüfwerkzeuge ohne Unity, RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte, Stand, Umsetzung in 10 Punkten

### Community 65 - "GameApp.cs"
Cohesion: 0.25
Nodes (7): AppMode, Ending, Intro, Loading, Menu, PlanetSelect, Playing

### Community 66 - "RePlanetBuild.cs"
Cohesion: 0.38
Nodes (5): RePlanet.EditorTools, unityeditor, unityeditor_build_reporting, unityeditor_scenemanagement, unityengine_scenemanagement

### Community 67 - "RE:PLANET – Architektur"
Cohesion: 0.29
Nodes (6): Darstellung, Datenfluss im Spiel, Eine Simulation – drei Betriebsarten, RE:PLANET – Architektur, Welt und Speicherung, Überblick

### Community 68 - "RE:PLANET – Trailer- und Clip-Ideen"
Cohesion: 0.29
Nodes (7): 1. „Eine Welle, ein Haufen Schrott weniger“ – die Magnetwelle, 2. „Das ist zu groß für dich, MIKO“ – Kran und Transporter räumen ein Wrack, 3. „Die Stadt erwacht“ – ein Großprojekt wird fertig, 4. „Such dir ein Dach“ – Sandsturm und Nacht, 5. „Vorher – nachher“ im Fotomodus (plus Koop-Finale), Allgemeine Hinweise für den Dreh, RE:PLANET – Trailer- und Clip-Ideen

### Community 69 - ".Awake"
Cohesion: 0.47
Nodes (4): AudioListener, FlareLayer, Camera, Light

### Community 71 - "Automatisierte Tests und Balancing"
Cohesion: 0.33
Nodes (5): Automatisierte Tests und Balancing, Balancing-Messwerte (nach Anpassung), Beobachtungen / offene Punkte, Nicht getestet, RE:PLANET – Testbericht

### Community 72 - "Müll je Planet und Bereich"
Cohesion: 0.33
Nodes (6): Alle Planeten, Müll je Planet und Bereich, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 73 - "Großprojekte"
Cohesion: 0.40
Nodes (5): Großprojekte, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 76 - "NetEventType"
Cohesion: 0.50
Nodes (4): NetEventType, Connect, Disconnect, Message

### Community 77 - "Cat"
Cohesion: 0.50
Nodes (4): Cat, Ambient, Sfx, Ui

## Knowledge Gaps
- **300 isolated node(s):** `BassNote`, `Top`, `Note`, `Beat`, `BarLen` (+295 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 543 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `GameApp` connect `GameApp` to `Game`, `GameApp.cs`, `UIRoot`, `.Rect`, `WorldState`, `.Col`, `GameClient`, `HostServer`, `PlayerController`, `.L`, `UINav`, `CameraRig`, `SaveStore`, `.BuildProps`?**
  _High betweenness centrality (0.100) - this node is a cross-community bridge._
- **Why does `UIRoot` connect `UIRoot` to `Game`, `GameApp`, `.Rect`, `.Col`, `.L`, `UINav`, `GameAction`, `SaveStore`, `system_collections_generic`, `UIScreen`, `.BuildProps`?**
  _High betweenness centrality (0.100) - this node is a cross-community bridge._
- **Why does `WorldState` connect `WorldState` to `Game`, `.Station`, `RePlanet.Core`, `GameData`, `CampaignBot`, `GameApp`, `.Get`, `SessionHub`, `.Col`, `GameClient`, `.True`, `WorldView`, `PlayerController`, `ObjView`, `SaveStore`, `RobotModel`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Are the 12 inferred relationships involving `JObj` (e.g. with `.CoopHostSection()` and `.Main()`) actually correct?**
  _`JObj` has 12 INFERRED edges - model-reasoned connections that need verification._
- **What connects `BassNote`, `Top`, `Note` to the rest of the system?**
  _300 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Game` be split into smaller, more focused modules?**
  _Cohesion score 0.050243830353184575 - nodes in this community are weakly interconnected._
- **Should `.HeightAt` be split into smaller, more focused modules?**
  _Cohesion score 0.06030855539971949 - nodes in this community are weakly interconnected._