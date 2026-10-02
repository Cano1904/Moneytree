# Graph Report - Moneytree  (2026-10-02)

## Corpus Check
- 171 files · ~584,738 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 22 file(s) not represented in the graph (top: .shader 9, .cginc 5, .asmdef 3)

## Summary
- 4954 nodes · 19130 edges · 177 communities (152 shown, 25 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 2451 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ab00a332`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Game
- PlanetLayout
- GameData
- CampaignBot
- UIRoot
- GameApp
- .HeightAt
- RePlanetSetup
- .Min
- GameClient
- SessionHub
- WorldView
- Arr
- .Get
- .Music
- .Clamp
- AudioManager
- Rect
- .Euler
- sky_preview.py
- GameAction
- ObjView
- Atmosphere
- .Next
- FxView
- system_collections_generic
- UIScreen
- V3
- .Max
- .Step
- RePlanet.Core
- LocalServerTransport
- .True
- Loc
- Color32
- InputMap
- manifest.json
- PlayerController
- .Get
- .Main
- AudioSource
- KeyCode
- Mesh
- RE:PLANET – Eine zweite Chance · Game-Design-Dokument
- RE:PLANET – Koop (1–4 Spieler)
- JObj
- TrashRenderer
- CityLife
- Vector4
- Vector2
- .MusicVolume
- Synth
- RenderTextureFormat
- SaveStore
- .Produce
- LocTests
- ParticleSystem
- RE:PLANET – Eine zweite Chance
- BitSet
- RE:PLANET – Wirtschaftstabellen
- .Range
- GroundMarks
- .Run
- RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise
- WindLook
- system_io
- RePlanetBuild.cs
- Material
- RE:PLANET – Trailer- und Clip-Ideen
- Camera
- .R
- Müll je Planet und Bereich
- Großprojekte
- PlanetSelectScene
- .Clamp
- UnityStub2.cs
- Cat
- .EnsureEffects
- RePlanet.Server.csproj
- SmokeTest.csproj
- RePlanet.Tests.csproj
- Core.csproj
- Editor.csproj
- Runtime.csproj
- DocGen.csproj
- fetch-unity-refs.sh
- Vector3
- PostFX
- TntView
- MapCamera
- Texture
- Narrator
- Session
- .Main
- .LifeChecks
- Component
- SurfaceLook
- MikoGestures
- .Get
- QualitySettings
- Renderer
- TcpClientTransport
- ReflectionProbe
- Matrix4x4
- Checks
- MainModule
- UnityStub.cs
- Object
- .BuildWindows
- TcpServerTransport
- SystemLanguage
- FeaturesView
- AudioManager
- .Shots
- AudioClip
- AudioReverbPreset
- MonoBehaviour
- TreasureView
- TextureFormat
- ToastKind
- EventType
- BlendMode
- Shader
- .UpdateRadio
- HideFlags
- ParticleSystemShapeType
- CompareFunction
- SystemInfo
- MathUtil.cs
- Input
- RE:PLANET – Sprechertext Intro und Abspann
- RE:PLANET – Sprechertext für ElevenLabs
- ParticleSystemRenderMode
- RenderQueue
- CameraRig
- Json
- LightShadowResolution
- ParticleSystemRenderSpace
- MaterialGlobalIlluminationFlags
- FullScreenMode
- .Rect
- ParticleSystemCurveMode
- Windows-Build automatisch auf GitHub
- Testanleitung für echte Spieltests (Leistung und Koop)
- MenuLogoClock
- Scene
- .Card
- EndingDirector
- CullMode
- Profile
- Harness.csproj
- unityengine
- unityengine_rendering
- EmissionModule
- RE:PLANET – Architektur
- .ParseMel
- ParticleSystemGradientMode
- GameStub.cs
- .Check
- DepthTextureMode
- LightType
- ParticleSystemSortMode
- 9. Aufträge und Nebeninhalte
- .ShotList
- LightmapBakeType
- RenderTextureReadWrite
- ScaleMode
- TreasureDef
- Interp
- AudioDataLoadState
- CameraClearFlags
- NetEventType
- Building
- AudioRolloffMode
- MonoOrStereoscopicEye

## God Nodes (most connected - your core abstractions)
1. `Vector3` - 395 edges
2. `UIRoot` - 249 edges
3. `WorldView` - 208 edges
4. `Material` - 198 edges
5. `Game` - 193 edges
6. `GameApp` - 176 edges
7. `JObj` - 145 edges
8. `RePlanet.Core` - 129 edges
9. `WorldState` - 129 edges
10. `IntroDirector` - 129 edges

## Surprising Connections (you probably didn't know these)
- `Lokalisierung (`Core/Loc`)` --references--> `GameData`  [INFERRED]
  docs/ARCHITEKTUR.md → RePlanet/Assets/RePlanet/Core/Data/GameData.cs
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

## Communities (177 total, 25 thin omitted)

### Community 0 - "Game"
Cohesion: 0.10
Nodes (12): Dictionary, List, ActResult, Drone, Game, Now, TimeScale, Dictionary (+4 more)

### Community 1 - "PlanetLayout"
Cohesion: 0.10
Nodes (16): Ctx, Dictionary, List, BaseLayout, FloorPatch, GateLayout, PlanetLayout, Id (+8 more)

### Community 2 - "GameData"
Cohesion: 0.06
Nodes (28): CultureInfo, Erweiterungspunkte, Dictionary, List, BuildingDef, CosmeticDef, GateDef, Grade (+20 more)

### Community 3 - "CampaignBot"
Cohesion: 0.10
Nodes (15): Stopwatch, BalanceRun, CampaignBot, L, PS, S, Shutdowns, T (+7 more)

### Community 4 - "UIRoot"
Cohesion: 0.04
Nodes (27): DateTime, MapMark, MapTex, ProfilerRecorder, Toast, List, UIRoot, List (+19 more)

### Community 5 - "GameApp"
Cohesion: 0.07
Nodes (16): UIState, BlocksGameplay, List, Type, GameApp, CoopActive, CoopOpen, I (+8 more)

### Community 6 - ".HeightAt"
Cohesion: 0.06
Nodes (28): Animal, Group, Flag, Puddle, Stamp, Wheel, List, LifeCommon (+20 more)

### Community 7 - "RePlanetSetup"
Cohesion: 0.12
Nodes (16): Changes, MatSpec, Action, Func, List, MenuItem, Changes, MatSpec (+8 more)

### Community 9 - "GameClient"
Cohesion: 0.08
Nodes (19): Action, Dictionary, GameClient, Me, PendingCount, RenderTime, IClientTransport, Connected (+11 more)

### Community 10 - "SessionHub"
Cohesion: 0.10
Nodes (10): 1. Überblick, HostServer, Online, Port, Action, Func, IEnumerable, SessionHub (+2 more)

### Community 11 - "WorldView"
Cohesion: 0.05
Nodes (52): Stationen je Planet und Schlaf (`Runtime/Render/WorldViewStations.cs`, `RobotSleep.cs`), Facade, Box, Look, Dictionary, ChunkBuilder, ChunkCount, VertexCount (+44 more)

### Community 12 - "Arr"
Cohesion: 0.13
Nodes (6): LineDef, Func, Arr, BarLen, Beat, Spec

### Community 13 - ".Get"
Cohesion: 0.04
Nodes (36): Erzähler im Spiel und Radio (`Core/Sim/Story.cs`), TNT und Schätze im Müll, Zusatzsysteme (Lieferlimit, Sturm abwarten, Helfer, Erfolge, Schnellreise, Ereignisse), Stems, MissionDef, Dictionary, List, List (+28 more)

### Community 14 - ".Music"
Cohesion: 0.24
Nodes (8): Arr, Chord, MusicSet, Dictionary, Chord, BassNote, Top, MusicSet

### Community 15 - ".Clamp"
Cohesion: 0.04
Nodes (28): Oberfläche: Spielmenü-Tablet und Hauptmenü (`Runtime/UI`), TabletRects, Dictionary, UISkin, Contrast, Dictionary, HashSet, FontRebuilds (+20 more)

### Community 16 - "AudioManager"
Cohesion: 0.05
Nodes (25): Job, LoopVoice, MusicClips, Dictionary, List, AudioManager, Dictionary, HashSet (+17 more)

### Community 17 - "Rect"
Cohesion: 0.14
Nodes (9): ScrollCtx, Rect, center, max, min, xMax, xMin, yMax (+1 more)

### Community 18 - ".Euler"
Cohesion: 0.04
Nodes (28): Agent, Car, Kind, Plant, Action, List, FloraRenderer, MaxShear (+20 more)

### Community 19 - "sky_preview.py"
Cohesion: 0.06
Nodes (61): glob, math, numpy, os, pil, re, subprocess, sys (+53 more)

### Community 20 - "GameAction"
Cohesion: 0.05
Nodes (38): GameAction, AltTool, Build, DiveDown, DiveUp, Emote, Interact, Inventory (+30 more)

### Community 21 - "ObjView"
Cohesion: 0.07
Nodes (16): HashSet, IEnumerable, ObjView, DynObj, Def, Dictionary, List, Treasures (+8 more)

### Community 22 - "Atmosphere"
Cohesion: 0.07
Nodes (21): Color32Key, IEquatable, LookInfo, PlanetSky, Dictionary, List, Atmosphere, CloudCover (+13 more)

### Community 23 - ".Next"
Cohesion: 0.22
Nodes (3): OnePole, PNoise, Svf

### Community 24 - "FxView"
Cohesion: 0.19
Nodes (8): Arc, Ghost, IEnumerator, List, Stack, FxView, Density, I

### Community 25 - "system_collections_generic"
Cohesion: 0.08
Nodes (9): UnityEngine, UnityEngine.Rendering, RePlanet, Terrain, BuildMode, PhotoMode, SurfKind, SleepSpots (+1 more)

### Community 26 - "UIScreen"
Cohesion: 0.11
Nodes (18): UIScreen, Coop, Credits, Ending, Intro, Loading, MainMenu, Map (+10 more)

### Community 27 - "V3"
Cohesion: 0.09
Nodes (9): Welt und Speicherung, V3, IsFinite, Length, LengthXZ, ShelterRoom, List, Test (+1 more)

### Community 28 - ".Max"
Cohesion: 0.04
Nodes (12): Drop, Engine, List, RuntimeInitializeOnLoadMethod, Drop, ShipArrival, CinematicActive, Density (+4 more)

### Community 29 - ".Step"
Cohesion: 0.20
Nodes (3): List, Hud, Exception

### Community 30 - "RePlanet.Core"
Cohesion: 0.10
Nodes (6): RePlanet.Core, system, system_diagnostics, system_linq, Probe, unity_profiling

### Community 31 - "LocalServerTransport"
Cohesion: 0.12
Nodes (9): ConcurrentQueue, Dictionary, List, LocalClientTransport, Connected, Error, Failed, LocalServerTransport (+1 more)

### Community 32 - ".True"
Cohesion: 0.07
Nodes (34): Ergebnis des Testlaufs, Exception, IDisposable, SaveCodec, Func, Test, EconomyTests, TestHelpers (+26 more)

### Community 33 - "Loc"
Cohesion: 0.06
Nodes (22): DataField, IDictionary, Dictionary, List, Regex, Loc, English, EnglishTable (+14 more)

### Community 34 - "Color32"
Cohesion: 0.11
Nodes (8): Func, Style, TerrainLook, Style, EmitParams, Exception, Color32, UnityException

### Community 35 - "InputMap"
Cohesion: 0.18
Nodes (3): Dictionary, InputMap, UsingPad

### Community 36 - "manifest.json"
Cohesion: 0.09
Nodes (21): com.unity.modules.animation, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem, com.unity.modules.physics, com.unity.modules.screencapture (+13 more)

### Community 37 - "PlayerController"
Cohesion: 0.08
Nodes (19): VehicleState, Def, TntFlight, Action, Interaction, Interaction, PlayerController, Env (+11 more)

### Community 38 - ".Get"
Cohesion: 0.05
Nodes (18): Knock, Billboard, ParticleSystemRenderer, GameObject, activeInHierarchy, gameObject, scene, transform (+10 more)

### Community 39 - ".Main"
Cohesion: 0.22
Nodes (5): Random, Ids, IDisposable, List, Program

### Community 40 - "AudioSource"
Cohesion: 0.15
Nodes (5): Cat, LoopVoice, Slot, Voice, AudioSource

### Community 41 - "KeyCode"
Cohesion: 0.02
Nodes (110): KeyCode, A, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5 (+102 more)

### Community 42 - "Mesh"
Cohesion: 0.06
Nodes (21): Call, List, Call, Graphics, MaterialPropertyBlock, Mesh, colors, colors32 (+13 more)

### Community 43 - "RE:PLANET – Eine zweite Chance · Game-Design-Dokument"
Cohesion: 0.12
Nodes (17): 10. Geschichte, 11. Koop, 12. Speichern, 13. Fotomodus, 14. Barrierefreiheit und Komfort, 15. Präsentation, 1. Vision, 2. Die Spielschleife (+9 more)

### Community 44 - "RE:PLANET – Koop (1–4 Spieler)"
Cohesion: 0.15
Nodes (12): 10. Fehlermeldungen und was sie bedeuten, 2. Koop starten, 4. Rechte und Vertrauensmodus, 5. Gemeinsam spielen – was sich im Koop ändert, 6. Späte Beitritte, Verlassen und Wiederverbinden, 7. Speichern im Koop, 8. Netzwerk einrichten: Ports, Firewall, Router, 9. Dedizierter Server (+4 more)

### Community 45 - "JObj"
Cohesion: 0.14
Nodes (4): Dictionary, List, JObj, FeatureToasts

### Community 46 - "TrashRenderer"
Cohesion: 0.16
Nodes (10): Batch, TrashType, MainMaterial, TotalUnits, Dictionary, HashSet, List, Batch (+2 more)

### Community 47 - "CityLife"
Cohesion: 0.08
Nodes (19): Flag, Holo, Dictionary, List, RuntimeInitializeOnLoadMethod, Car, CityLife, CarsAlive (+11 more)

### Community 48 - "Vector4"
Cohesion: 0.15
Nodes (4): Dictionary, Mats, List, Vector4

### Community 49 - "Vector2"
Cohesion: 0.07
Nodes (14): Dictionary, List, ActorsView, I, LocalRobot, List, Vector2, magnitude (+6 more)

### Community 50 - ".MusicVolume"
Cohesion: 0.20
Nodes (7): Ambience und Effekte, Architektur, Intro-Score (100 s), Musik-Engine, Prüfen ohne Unity, RE:PLANET – Klang und Musik, Stücke und Stimmungen

### Community 51 - "Synth"
Cohesion: 0.13
Nodes (4): Note, Synth, WindStyle, WindStyle

### Community 52 - "RenderTextureFormat"
Cohesion: 0.13
Nodes (14): RenderTextureFormat, ARGB1555, ARGB2101010, ARGB32, ARGB4444, ARGBHalf, Default, DefaultHDR (+6 more)

### Community 55 - "LocTests"
Cohesion: 0.18
Nodes (9): HashSet, KeyValuePair, List, Regex, LocTests, Src, Program, TestAttribute (+1 more)

### Community 56 - "ParticleSystem"
Cohesion: 0.04
Nodes (47): CollisionModule, ColorOverLifetimeModule, EmissionModule, ForceOverLifetimeModule, LimitVelocityOverLifetimeModule, MainModule, MinMaxCurve, NoiseModule (+39 more)

### Community 57 - "RE:PLANET – Eine zweite Chance"
Cohesion: 0.18
Nodes (11): Dokumentation, Koop (1–4 Spieler), Projekt öffnen und spielen, Projektstruktur, RE:PLANET – Eine zweite Chance, Speicherorte, Stand – was geprüft ist und was nicht, Steuerung (+3 more)

### Community 58 - "BitSet"
Cohesion: 0.24
Nodes (4): IEnumerable, BitSet, Capacity, Count

### Community 59 - "RE:PLANET – Wirtschaftstabellen"
Cohesion: 0.20
Nodes (10): Aufträge (Missionen), Fahrzeuge, Gebäude (Stützpunkt), Grundwerte, Materialien, RE:PLANET – Wirtschaftstabellen, Recyclingaufträge (Auftragstafel), Reparaturen, Ökologie, Unterschlupf (+2 more)

### Community 60 - ".Range"
Cohesion: 0.11
Nodes (14): Cell, IList, Rng, Dictionary, Func, List, Backdrop, LastDrawn (+6 more)

### Community 61 - "GroundMarks"
Cohesion: 0.09
Nodes (16): Puddle, Dictionary, List, RuntimeInitializeOnLoadMethod, GroundMarks, BaseLife, Capacity, DrawnStamps (+8 more)

### Community 63 - "RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise"
Cohesion: 0.25
Nodes (8): 1. Was im Projekt steckt und woher es kommt, 2. Unity, 3. Weitere Werkzeuge, 4. Namen und Marken, Figuren, Namen und Designs, „RE:PLANET“, RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise, Weitere Marken

### Community 64 - "WindLook"
Cohesion: 0.08
Nodes (19): Belebte Welt (Tiere, Spuren, Wind, Stadt erwacht 2.0, MIKOs Mimik), Entscheidungen, Erweiterungen (Radio, Stadtklänge, Controller-Bauansicht, Englisch, Erzähler, Leistungsanzeige), Grenzen (Stand dieser Umgebung), Nächste Schritte, Prüfwerkzeuge ohne Unity, RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte, Stand (+11 more)

### Community 65 - "system_io"
Cohesion: 0.07
Nodes (22): RePlanet.DocGen, RePlanet.Server, RePlanet.SmokeTest, EnergyInfo, AppMode, Ending, Intro, Loading (+14 more)

### Community 66 - "RePlanetBuild.cs"
Cohesion: 0.38
Nodes (5): RePlanet.EditorTools, unityeditor, unityeditor_build_reporting, unityeditor_scenemanagement, unityengine_scenemanagement

### Community 67 - "Material"
Cohesion: 0.06
Nodes (34): Ark, Bot, Crowd, Geo, Glow, GlowSet, Look, MeshData (+26 more)

### Community 68 - "RE:PLANET – Trailer- und Clip-Ideen"
Cohesion: 0.29
Nodes (7): 1. „Eine Welle, ein Haufen Schrott weniger“ – die Magnetwelle, 2. „Das ist zu groß für dich, MIKO“ – Kran und Transporter räumen ein Wrack, 3. „Die Stadt erwacht“ – ein Großprojekt wird fertig, 4. „Such dir ein Dach“ – Sandsturm und Nacht, 5. „Vorher – nachher“ im Fotomodus (plus Koop-Finale), Allgemeine Hinweise für den Dreh, RE:PLANET – Trailer- und Clip-Ideen

### Community 69 - "Camera"
Cohesion: 0.10
Nodes (12): MonoOrStereoscopicEye, StereoscopicEye, Camera, current, main, pixelHeight, pixelWidth, worldToCameraMatrix (+4 more)

### Community 71 - ".R"
Cohesion: 0.14
Nodes (9): Automatisierte Tests und Balancing, Balancing-Messwerte (nach Anpassung), Beobachtungen / offene Punkte, Durch Tests gefundene und behobene Fehler in der Spiellogik, Kampagnen-Bot (`dotnet run -c Release -- balance`), Nicht getestet, RE:PLANET – Testbericht, List (+1 more)

### Community 72 - "Müll je Planet und Bereich"
Cohesion: 0.33
Nodes (6): Alle Planeten, Müll je Planet und Bereich, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 73 - "Großprojekte"
Cohesion: 0.40
Nodes (5): Großprojekte, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 74 - "PlanetSelectScene"
Cohesion: 0.10
Nodes (11): Body, List, RuntimeInitializeOnLoadMethod, Body, PlanetSelectScene, Active, Descending, Focused (+3 more)

### Community 75 - ".Clamp"
Cohesion: 0.13
Nodes (7): VehicleDef, List, Motor, MotorEnv, Tmp, MoverState, M

### Community 76 - "UnityStub2.cs"
Cohesion: 0.05
Nodes (46): Attribute, AudioHighPassFilter, AudioLowPassFilter, AudioSettings, dspTime, outputSampleRate, DefaultExecutionOrderAttribute, ExecuteAlways (+38 more)

### Community 77 - "Cat"
Cohesion: 0.50
Nodes (4): Cat, Ambient, Sfx, Ui

### Community 78 - ".EnsureEffects"
Cohesion: 0.17
Nodes (10): AnimationCurve, length, Gradient, GradientAlphaKey, GradientColorKey, Keyframe, MinMaxGradient, ParticleSystemStopBehavior (+2 more)

### Community 87 - "Vector3"
Cohesion: 0.03
Nodes (41): IEnumerable, SkyBody, List, FreighterModel, Arc, Ghost, Engine, Flyer (+33 more)

### Community 88 - "PostFX"
Cohesion: 0.17
Nodes (5): PostFX, I, LastExposure, Running, RenderTexture

### Community 89 - "TntView"
Cohesion: 0.08
Nodes (20): Boom, Charge, Flyer, Knock, Dictionary, List, RuntimeInitializeOnLoadMethod, Charge (+12 more)

### Community 90 - "MapCamera"
Cohesion: 0.07
Nodes (17): List, MapCamera, Cam, Distance, Failed, Following, HasFrame, I (+9 more)

### Community 91 - "Texture"
Cohesion: 0.17
Nodes (10): LookInfo, Cubemap, Texture2DArray, Texture, height, width, TextureWrapMode, Mirror (+2 more)

### Community 92 - "Narrator"
Cohesion: 0.10
Nodes (12): Cue, 7. Untertitel, Pending, Dictionary, List, Cue, Narrator, GameLineActive (+4 more)

### Community 93 - "Session"
Cohesion: 0.15
Nodes (11): RateLimit, Dictionary, List, Queue, RateLimit, RidCache, Session, Closed (+3 more)

### Community 94 - ".Main"
Cohesion: 0.17
Nodes (6): List, Program, Tri, F, P, ColorUtility

### Community 96 - "Component"
Cohesion: 0.11
Nodes (6): Harness, Type, Component, IsDead, tag, transform

### Community 97 - "SurfaceLook"
Cohesion: 0.17
Nodes (4): Icon, SurfaceLook, Custom, DetailShadersAllowed

### Community 98 - "MikoGestures"
Cohesion: 0.15
Nodes (8): Dictionary, KeyValuePair, List, RuntimeInitializeOnLoadMethod, MikoGestures, Hops, I, Waves

### Community 100 - "QualitySettings"
Cohesion: 0.09
Nodes (21): AnisotropicFiltering, Disable, Enable, ForceEnable, ColorSpace, Gamma, Linear, Uninitialized (+13 more)

### Community 101 - "Renderer"
Cohesion: 0.07
Nodes (26): TrailRenderer, MotionVectorGenerationMode, Camera, ForceNoMotion, Object, Renderer, bounds, isVisible (+18 more)

### Community 102 - "TcpClientTransport"
Cohesion: 0.16
Nodes (12): BlockingCollection, 3. Ablauf einer Verbindung, Schutzmechanismen, NetworkStream, Conn, Framing, TcpClientTransport, Connected (+4 more)

### Community 103 - "ReflectionProbe"
Cohesion: 0.10
Nodes (17): ReflectionProbe, texture, ReflectionProbeClearFlags, Skybox, SolidColor, ReflectionProbeMode, Baked, Custom (+9 more)

### Community 104 - "Matrix4x4"
Cohesion: 0.10
Nodes (12): Darstellung, Dictionary, DecoGrid, InstanceBatch, Count, Matrix4x4, identity, inverse (+4 more)

### Community 105 - "Checks"
Cohesion: 0.13
Nodes (10): BindingFlags, Saved, Checks, Dictionary, HashSet, List, MethodInfo, Type (+2 more)

### Community 106 - "MainModule"
Cohesion: 0.10
Nodes (20): MinMaxGradient, ColorOverLifetimeModule, MainModule, ParticleSystemCullingMode, AlwaysSimulate, Automatic, Pause, PauseAndCatchup (+12 more)

### Community 107 - "UnityStub.cs"
Cohesion: 0.04
Nodes (47): Dictionary, Bounds, extents, max, min, FilterMode, Bilinear, Point (+39 more)

### Community 108 - "Object"
Cohesion: 0.10
Nodes (7): RuntimeInitializeOnLoadMethod, Action, HashSet, MethodInfo, Object, IsDead, World

### Community 109 - ".BuildWindows"
Cohesion: 0.15
Nodes (7): MenuItem, RePlanetBuild, Result, Result, TimeSpan, Debug, isDebugBuild

### Community 110 - "TcpServerTransport"
Cohesion: 0.18
Nodes (6): Conn, Thread, NetIds, TcpServerTransport, Port, TcpListener

### Community 111 - "SystemLanguage"
Cohesion: 0.12
Nodes (14): Application, platform, systemLanguage, RuntimePlatform, LinuxPlayer, OSXPlayer, WindowsPlayer, SystemLanguage (+6 more)

### Community 112 - "FeaturesView"
Cohesion: 0.13
Nodes (13): Beam, Capsule, Dictionary, List, Stack, Beam, Capsule, FeaturesView (+5 more)

### Community 113 - "AudioManager"
Cohesion: 0.13
Nodes (6): AudioManager, I, IntroReady, IntroTime, VoiceGain, VoiceParent

### Community 115 - "AudioClip"
Cohesion: 0.24
Nodes (4): Flackern im Hauptmenü, Spielmenü als Feldtablet, MusicClips, AudioClip, loadState

### Community 116 - "AudioReverbPreset"
Cohesion: 0.14
Nodes (14): AudioReverbPreset, Arena, Auditorium, Bathroom, Cave, Concerthall, Generic, Hangar (+6 more)

### Community 117 - "MonoBehaviour"
Cohesion: 0.14
Nodes (8): Floater, PostFX, I, Running, UIRoot, IEnumerator, Coroutine, MonoBehaviour

### Community 118 - "TreasureView"
Cohesion: 0.17
Nodes (8): Pop, List, RuntimeInitializeOnLoadMethod, Pop, TreasureView, Glints, I, Pops

### Community 119 - "TextureFormat"
Cohesion: 0.15
Nodes (13): TextureFormat, Alpha8, ARGB32, DXT1, DXT5, R16, R8, RFloat (+5 more)

### Community 120 - "ToastKind"
Cohesion: 0.29
Nodes (6): ToastKind, Error, Info, Story, Success, Warning

### Community 121 - "EventType"
Cohesion: 0.17
Nodes (11): Event, EventType, KeyDown, KeyUp, Layout, MouseDown, MouseDrag, MouseMove (+3 more)

### Community 122 - "BlendMode"
Cohesion: 0.17
Nodes (12): BlendMode, DstAlpha, DstColor, One, OneMinusDstAlpha, OneMinusDstColor, OneMinusSrcAlpha, OneMinusSrcColor (+4 more)

### Community 124 - ".UpdateRadio"
Cohesion: 0.25
Nodes (5): Dictionary, List, NarrationLine, RadioTrack, Story

### Community 125 - "HideFlags"
Cohesion: 0.20
Nodes (10): HideFlags, DontSave, DontSaveInBuild, DontSaveInEditor, DontUnloadUnusedAsset, HideAndDontSave, HideInHierarchy, HideInInspector (+2 more)

### Community 126 - "ParticleSystemShapeType"
Cohesion: 0.18
Nodes (11): ParticleSystemShapeType, Box, Circle, Cone, Donut, Edge, Hemisphere, Mesh (+3 more)

### Community 127 - "CompareFunction"
Cohesion: 0.20
Nodes (10): CompareFunction, Always, Disabled, Equal, Greater, GreaterEqual, Less, LessEqual (+2 more)

### Community 128 - "SystemInfo"
Cohesion: 0.20
Nodes (9): SystemInfo, deviceUniqueIdentifier, graphicsDeviceName, graphicsMemorySize, maxTextureSize, operatingSystem, supportsComputeShaders, supportsInstancing (+1 more)

### Community 130 - "Input"
Cohesion: 0.12
Nodes (7): Dictionary, HashSet, Input, anyKey, anyKeyDown, inputString, mouseScrollDelta

### Community 131 - "RE:PLANET – Sprechertext Intro und Abspann"
Cohesion: 0.25
Nodes (7): 1. Die Stimme, 2. Intro (100 Sekunden, 14 Zeilen), 3. Abspann (5 Zeilen, optional), 4. Aufnahme und Lieferung, 5. Selbst aufnehmen, 6. Mit einem Stimmdienst erzeugen, RE:PLANET – Sprechertext Intro und Abspann

### Community 132 - "RE:PLANET – Sprechertext für ElevenLabs"
Cohesion: 0.25
Nodes (7): 1. Stimme auswählen, 2. Einstellungen, 3. Intro (14 Zeilen), 4. Abspann (5 Zeilen), 5. Im Spiel (21 Zeilen, Erzähler Helmut), 6. Ins Spiel bringen, RE:PLANET – Sprechertext für ElevenLabs

### Community 133 - "ParticleSystemRenderMode"
Cohesion: 0.29
Nodes (7): ParticleSystemRenderMode, Billboard, HorizontalBillboard, Mesh, None, Stretch, VerticalBillboard

### Community 134 - "RenderQueue"
Cohesion: 0.29
Nodes (7): RenderQueue, AlphaTest, Background, Geometry, GeometryLast, Overlay, Transparent

### Community 135 - "CameraRig"
Cohesion: 0.13
Nodes (8): DecoGrid, List, CameraRig, Cam, I, Post, AudioListener, Resources

### Community 137 - "LightShadowResolution"
Cohesion: 0.33
Nodes (6): LightShadowResolution, FromQualitySettings, High, Low, Medium, VeryHigh

### Community 138 - "ParticleSystemRenderSpace"
Cohesion: 0.33
Nodes (6): ParticleSystemRenderSpace, Facing, Local, Velocity, View, World

### Community 139 - "MaterialGlobalIlluminationFlags"
Cohesion: 0.33
Nodes (6): MaterialGlobalIlluminationFlags, AnyEmissive, BakedEmissive, EmissiveIsBlack, None, RealtimeEmissive

### Community 140 - "FullScreenMode"
Cohesion: 0.22
Nodes (9): FullScreenMode, ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed, Resolution, Screen, currentResolution (+1 more)

### Community 141 - ".Rect"
Cohesion: 0.07
Nodes (11): Lokalisierung (`Core/Loc`), Action, Settings, PathFile, TntShop, Dictionary, List, Stack (+3 more)

### Community 142 - "ParticleSystemCurveMode"
Cohesion: 0.40
Nodes (5): ParticleSystemCurveMode, Constant, Curve, TwoConstants, TwoCurves

### Community 143 - "Windows-Build automatisch auf GitHub"
Cohesion: 0.50
Nodes (3): Build starten und herunterladen, Einmalige Einrichtung (im GitHub-Repository, nicht im Chat), Windows-Build automatisch auf GitHub

### Community 144 - "Testanleitung für echte Spieltests (Leistung und Koop)"
Cohesion: 0.50
Nodes (3): 1. Leistung messen, 2. Koop mit zwei PCs testen, Testanleitung für echte Spieltests (Leistung und Koop)

### Community 145 - "MenuLogoClock"
Cohesion: 0.33
Nodes (3): MenuLogoClock, Starts, T

### Community 146 - "Scene"
Cohesion: 0.50
Nodes (4): Scene, Delivery, Landing, None

### Community 147 - ".Card"
Cohesion: 0.33
Nodes (3): Dictionary, RuntimeInitializeOnLoadMethod, TreasureTab

### Community 148 - "EndingDirector"
Cohesion: 0.32
Nodes (3): Action, List, EndingDirector

### Community 149 - "CullMode"
Cohesion: 0.50
Nodes (4): CullMode, Back, Front, Off

### Community 150 - "Profile"
Cohesion: 0.29
Nodes (4): Dictionary, HashSet, Profile, PathFile

### Community 154 - "EmissionModule"
Cohesion: 0.53
Nodes (3): Burst, EmissionModule, burstCount

### Community 155 - "RE:PLANET – Architektur"
Cohesion: 0.33
Nodes (5): Datenfluss im Spiel, Eine Simulation – drei Betriebsarten, RE:PLANET – Architektur, Stadtklänge (`Runtime/Audio/AudioCity.cs`, Klänge in `Core/Audio/SynthCity.cs`), Überblick

### Community 156 - ".ParseMel"
Cohesion: 0.33
Nodes (5): MelNote, Note, List, LineDef, MelNote

### Community 157 - "ParticleSystemGradientMode"
Cohesion: 0.33
Nodes (6): ParticleSystemGradientMode, Color, Gradient, RandomColor, TwoColors, TwoGradients

### Community 159 - ".Check"
Cohesion: 0.33
Nodes (3): EmitParams, List, NanGuard

### Community 160 - "DepthTextureMode"
Cohesion: 0.40
Nodes (5): DepthTextureMode, Depth, DepthNormals, MotionVectors, None

### Community 161 - "LightType"
Cohesion: 0.40
Nodes (5): LightType, Area, Directional, Point, Spot

### Community 162 - "ParticleSystemSortMode"
Cohesion: 0.40
Nodes (5): ParticleSystemSortMode, Distance, None, OldestInFront, YoungestInFront

### Community 163 - "9. Aufträge und Nebeninhalte"
Cohesion: 0.33
Nodes (6): 9. Aufträge und Nebeninhalte, Helferroboter, Schnellreise über Lichtpunkte, Schätze im Müll (Vitrine), TNT: Müllberge sprengen (und Freunde durch die Luft werfen), Weltereignisse

### Community 164 - ".ShotList"
Cohesion: 0.33
Nodes (5): IntroTimeline, Shot, Shot, List, Shot

### Community 165 - "LightmapBakeType"
Cohesion: 0.50
Nodes (4): LightmapBakeType, Baked, Mixed, Realtime

### Community 166 - "RenderTextureReadWrite"
Cohesion: 0.50
Nodes (4): RenderTextureReadWrite, Default, Linear, sRGB

### Community 167 - "ScaleMode"
Cohesion: 0.50
Nodes (4): ScaleMode, ScaleAndCrop, ScaleToFit, StretchToFill

### Community 169 - "Interp"
Cohesion: 0.50
Nodes (4): List, Interp, Snap, Snap

### Community 170 - "AudioDataLoadState"
Cohesion: 0.40
Nodes (5): AudioDataLoadState, Failed, Loaded, Loading, Unloaded

### Community 171 - "CameraClearFlags"
Cohesion: 0.40
Nodes (5): CameraClearFlags, Depth, Nothing, Skybox, SolidColor

### Community 172 - "NetEventType"
Cohesion: 0.50
Nodes (4): NetEventType, Connect, Disconnect, Message

### Community 173 - "Building"
Cohesion: 0.50
Nodes (3): Building, Def, W

### Community 175 - "AudioRolloffMode"
Cohesion: 0.50
Nodes (4): AudioRolloffMode, Custom, Linear, Logarithmic

### Community 176 - "MonoOrStereoscopicEye"
Cohesion: 0.50
Nodes (4): MonoOrStereoscopicEye, Left, Mono, Right

## Knowledge Gaps
- **1024 isolated node(s):** `BassNote`, `Top`, `Note`, `Beat`, `BarLen` (+1019 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1440 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Vector3` connect `Vector3` to `Input`, `UIRoot`, `.HeightAt`, `CameraRig`, `.Min`, `WorldView`, `.Rect`, `.Clamp`, `AudioManager`, `.Euler`, `Atmosphere`, `FxView`, `system_collections_generic`, `V3`, `.Max`, `.Step`, `.Check`, `Color32`, `.ShotList`, `PlayerController`, `.Get`, `AudioSource`, `Mesh`, `JObj`, `.PadBuildCursor`, `CityLife`, `TrashRenderer`, `Vector2`, `.Range`, `GroundMarks`, `Material`, `Camera`, `PlanetSelectScene`, `PostFX`, `TntView`, `MapCamera`, `Texture`, `.Main`, `.LifeChecks`, `MikoGestures`, `.Get`, `QualitySettings`, `ReflectionProbe`, `Matrix4x4`, `Checks`, `UnityStub.cs`, `Object`, `.BuildWindows`, `FeaturesView`, `AudioManager`, `MonoBehaviour`, `TreasureView`, `ParticleSystemShapeType`?**
  _High betweenness centrality (0.102) - this node is a cross-community bridge._
- **Why does `UIRoot` connect `UIRoot` to `Game`, `system_io`, `Input`, `GameApp`, `.Min`, `.Rect`, `.Clamp`, `Rect`, `MenuLogoClock`, `GameAction`, `SaveStore`, `MonoBehaviour`, `system_collections_generic`, `UIScreen`, `RePlanet.Core`?**
  _High betweenness centrality (0.057) - this node is a cross-community bridge._
- **Why does `GameApp` connect `GameApp` to `Game`, `Input`, `UIRoot`, `.Min`, `GameClient`, `SessionHub`, `.Get`, `.Rect`, `.Clamp`, `Rect`, `Profile`, `.Max`, `PlayerController`, `JObj`, `SaveStore`, `system_io`, `PlanetSelectScene`, `PostFX`, `.LifeChecks`, `Checks`, `Object`, `.Shots`, `MonoBehaviour`?**
  _High betweenness centrality (0.056) - this node is a cross-community bridge._
- **Are the 28 inferred relationships involving `Vector3` (e.g. with `.EnsureWaterNormal()` and `.Animate()`) actually correct?**
  _`Vector3` has 28 INFERRED edges - model-reasoned connections that need verification._
- **What connects `BassNote`, `Top`, `Note` to the rest of the system?**
  _1024 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Game` be split into smaller, more focused modules?**
  _Cohesion score 0.10418508694370764 - nodes in this community are weakly interconnected._
- **Should `PlanetLayout` be split into smaller, more focused modules?**
  _Cohesion score 0.09871031746031746 - nodes in this community are weakly interconnected._