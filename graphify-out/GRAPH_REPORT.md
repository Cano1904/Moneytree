# Graph Report - Moneytree  (2026-09-29)

## Corpus Check
- 112 files · ~368,898 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 22 file(s) not represented in the graph (top: .shader 9, .cginc 5, .asmdef 3)

## Summary
- 4037 nodes · 14746 edges · 154 communities (130 shown, 24 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 1743 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `cd748784`
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
- .Max
- GameClient
- .True
- WorldView
- Arr
- V3
- .Music
- UISkin
- AudioManager
- .Min
- Vector3
- sky_preview.py
- GameAction
- PlanetLayout
- Atmosphere
- .Pole
- FxView
- system_collections_generic
- UIScreen
- RobotModel
- .Clamp01
- .Step
- Transport.cs
- TcpServerTransport
- .NewGame
- RePlanet.Core
- .LateUpdate
- InputMap
- manifest.json
- .Rect
- GameObject
- SessionHub
- .LoopInternal
- KeyCode
- Mesh
- RE:PLANET – Eine zweite Chance · Game-Design-Dokument
- RE:PLANET – Koop (1–4 Spieler)
- JObj
- TrashRenderer
- .Sfx
- Material
- ObjView
- RE:PLANET – Klang und Musik
- Synth
- CameraRig
- .Neustart_setzt_an_derselben_Stelle_fort
- .Produce
- Checks.cs
- ParticleSystem
- RE:PLANET – Eine zweite Chance
- BitSet
- RE:PLANET – Wirtschaftstabellen
- .Euler
- EndingDirector
- .Run
- RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise
- RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte
- GameApp.cs
- RePlanetBuild.cs
- .Get
- RE:PLANET – Trailer- und Clip-Ideen
- Camera
- Automatisierte Tests und Balancing
- Müll je Planet und Bereich
- Großprojekte
- Texture2D
- .Clamp
- UnityStub2.cs
- Cat
- ShipArrival
- RePlanet.Server.csproj
- SmokeTest.csproj
- RePlanet.Tests.csproj
- Core.csproj
- Editor.csproj
- Runtime.csproj
- DocGen.csproj
- fetch-unity-refs.sh
- Transform
- .LogWarning
- IntroDirector
- MapCamera
- Mathf
- Narrator
- .MakeEngine
- Color
- .GameChecks
- Component
- .Abs
- Session
- .Decode
- QualitySettings
- Renderer
- Quaternion
- ReflectionProbe
- Matrix4x4
- Checks
- MainModule
- UnityStub.cs
- Object
- .BuildWindows
- LineRenderer
- SystemLanguage
- Font
- AudioManager
- .DrawMesh
- AudioClip
- AudioReverbPreset
- MonoBehaviour
- RenderSettings
- TextureFormat
- ToastKind
- EventType
- BlendMode
- MaterialPropertyBlock
- Json
- HideFlags
- ParticleSystemShapeType
- CompareFunction
- SystemInfo
- TextAnchor
- Input
- RE:PLANET – Sprechertext Intro und Abspann
- RE:PLANET – Sprechertext für ElevenLabs
- ParticleSystemRenderMode
- RenderQueue
- MeshTopology
- PrimitiveType
- LightShadowResolution
- ParticleSystemRenderSpace
- MaterialGlobalIlluminationFlags
- Resources
- ParticleSystemCurveMode
- Windows-Build automatisch auf GitHub
- Testanleitung für echte Spieltests (Leistung und Koop)
- .RenderLine
- Scene
- LightShadows
- .Stop
- CullMode
- 3. Planeten
- Harness.csproj
- unityengine
- unityengine_rendering

## God Nodes (most connected - your core abstractions)
1. `Vector3` - 324 edges
2. `UIRoot` - 189 edges
3. `Material` - 178 edges
4. `WorldView` - 176 edges
5. `GameApp` - 145 edges
6. `Game` - 132 edges
7. `IntroDirector` - 129 edges
8. `KeyCode` - 123 edges
9. `JObj` - 108 edges
10. `Transform` - 103 edges

## Surprising Connections (you probably didn't know these)
- `Ergebnis des Testlaufs` --references--> `MotorEnv`  [INFERRED]
  docs/TESTBERICHT.md → RePlanet/Assets/RePlanet/Core/Motion/Motor.cs
- `Ergebnis des Testlaufs` --references--> `GameClient`  [INFERRED]
  docs/TESTBERICHT.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs
- `Eine Simulation – drei Betriebsarten` --references--> `HostServer`  [INFERRED]
  docs/ARCHITEKTUR.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs
- `1. Überblick` --references--> `HostServer`  [INFERRED]
  docs/KOOP.md → RePlanet/Assets/RePlanet/Core/Net/Client.cs
- `Durch Tests gefundene und behobene Fehler in der Spiellogik` --references--> `Session`  [INFERRED]
  docs/TESTBERICHT.md → RePlanet/Assets/RePlanet/Core/Net/Session.cs

## Import Cycles
- None detected.

## Communities (154 total, 24 thin omitted)

### Community 0 - "Game"
Cohesion: 0.17
Nodes (5): Dictionary, ActResult, Game, Now, PlayerData

### Community 1 - ".HeightAt"
Cohesion: 0.13
Nodes (10): Ctx, Hash, Noise, FloorPatch, Prop, Dictionary, Func, List (+2 more)

### Community 2 - "GameData"
Cohesion: 0.06
Nodes (25): CultureInfo, Erweiterungspunkte, Dictionary, List, BuildingDef, CosmeticDef, GateDef, Grade (+17 more)

### Community 3 - "CampaignBot"
Cohesion: 0.10
Nodes (15): Stopwatch, BalanceRun, CampaignBot, L, PS, S, Shutdowns, T (+7 more)

### Community 4 - "UIRoot"
Cohesion: 0.07
Nodes (18): MapMark, MapTex, UIRoot, List, Dictionary, List, List, Map3DActive (+10 more)

### Community 5 - "GameApp"
Cohesion: 0.05
Nodes (18): Action, Type, GameApp, CoopActive, CoopOpen, I, InGame, IsGuest (+10 more)

### Community 6 - "WorldState"
Cohesion: 0.08
Nodes (17): MissionDef, Dictionary, HashSet, List, EnergyInfo, Rules, Dictionary, HashSet (+9 more)

### Community 7 - "RePlanetSetup"
Cohesion: 0.11
Nodes (16): Changes, MatSpec, Action, Func, List, MenuItem, Changes, MatSpec (+8 more)

### Community 9 - "GameClient"
Cohesion: 0.09
Nodes (19): Action, Dictionary, GameClient, Me, PendingCount, RenderTime, IClientTransport, Connected (+11 more)

### Community 10 - ".True"
Cohesion: 0.22
Nodes (6): Exception, Func, Test, NetTests, Assert, AssertException

### Community 11 - "WorldView"
Cohesion: 0.08
Nodes (23): MultiBuilder, Empty, VertexCount, Dictionary, KeyValuePair, List, Thread, WorldView (+15 more)

### Community 12 - "Arr"
Cohesion: 0.18
Nodes (3): Arr, BarLen, Beat

### Community 13 - "V3"
Cohesion: 0.06
Nodes (21): Welt und Speicherung, Interaction, VehicleState, Def, V3, IsFinite, Length, LengthXZ (+13 more)

### Community 14 - ".Music"
Cohesion: 0.26
Nodes (8): Arr, Chord, MusicSet, Dictionary, Chord, BassNote, Top, MusicSet

### Community 15 - "UISkin"
Cohesion: 0.11
Nodes (8): Dictionary, UISkin, Contrast, GUI, GUIContent, GUISkin, GUIStyle, RectOffset

### Community 16 - "AudioManager"
Cohesion: 0.07
Nodes (19): Job, LoopVoice, MusicClips, Dictionary, HashSet, List, Queue, Thread (+11 more)

### Community 17 - ".Min"
Cohesion: 0.09
Nodes (15): Dictionary, List, Stack, ScrollCtx, UINav, ScrollCtx, Rect, center (+7 more)

### Community 18 - "Vector3"
Cohesion: 0.05
Nodes (26): Body, Agent, HashSet, List, MeshBuilder, VertexCount, MeshKit, Cube (+18 more)

### Community 19 - "sky_preview.py"
Cohesion: 0.08
Nodes (43): glob, math, numpy, os, pil, re, subprocess, sys (+35 more)

### Community 20 - "GameAction"
Cohesion: 0.06
Nodes (35): GameAction, AltTool, Build, DiveDown, DiveUp, Emote, Interact, Inventory (+27 more)

### Community 21 - "PlanetLayout"
Cohesion: 0.13
Nodes (14): List, MotorEnv, Tmp, Dictionary, List, BaseLayout, GateLayout, Mound (+6 more)

### Community 22 - "Atmosphere"
Cohesion: 0.08
Nodes (19): Color32Key, IEquatable, LookInfo, PlanetSky, Dictionary, List, Atmosphere, Darkness (+11 more)

### Community 23 - ".Pole"
Cohesion: 0.26
Nodes (3): OnePole, PNoise, Svf

### Community 24 - "FxView"
Cohesion: 0.10
Nodes (16): Arc, Ghost, IEnumerator, List, Stack, FxView, Density, I (+8 more)

### Community 25 - "system_collections_generic"
Cohesion: 0.12
Nodes (8): UnityEngine, UnityEngine.Rendering, RePlanet, Terrain, BuildMode, PhotoMode, SurfKind, system_collections_generic

### Community 26 - "UIScreen"
Cohesion: 0.10
Nodes (20): UIScreen, Coop, Credits, Ending, Intro, Loading, MainMenu, Map (+12 more)

### Community 27 - "RobotModel"
Cohesion: 0.20
Nodes (4): Action, Dictionary, List, RobotModel

### Community 28 - ".Clamp01"
Cohesion: 0.19
Nodes (3): Space, Self, World

### Community 30 - "Transport.cs"
Cohesion: 0.10
Nodes (14): RePlanet.DocGen, RePlanet.Server, RePlanet.SmokeTest, IntroTimeline, Shot, Shot, system_collections_concurrent, system_globalization (+6 more)

### Community 31 - "TcpServerTransport"
Cohesion: 0.05
Nodes (34): BlockingCollection, ConcurrentQueue, Conn, Darstellung, Datenfluss im Spiel, Eine Simulation – drei Betriebsarten, RE:PLANET – Architektur, Überblick (+26 more)

### Community 32 - ".NewGame"
Cohesion: 0.17
Nodes (11): Func, Test, EconomyTests, TestHelpers, Test, LogicTests, Game, Func (+3 more)

### Community 33 - "RePlanet.Core"
Cohesion: 0.16
Nodes (7): RePlanet.Core, Dictionary, Loc, system, system_diagnostics, system_linq, Harness

### Community 34 - ".LateUpdate"
Cohesion: 0.16
Nodes (5): Func, Style, TerrainLook, Style, Vector4

### Community 35 - "InputMap"
Cohesion: 0.13
Nodes (3): Dictionary, InputMap, UsingPad

### Community 36 - "manifest.json"
Cohesion: 0.09
Nodes (21): com.unity.modules.animation, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem, com.unity.modules.physics, com.unity.modules.screencapture (+13 more)

### Community 38 - "GameObject"
Cohesion: 0.06
Nodes (18): Dictionary, List, ActorsView, I, LocalRobot, Arc, Billboard, ParticleSystemRenderer (+10 more)

### Community 39 - "SessionHub"
Cohesion: 0.09
Nodes (16): Ergebnis des Testlaufs, IDisposable, Random, HostServer, Online, Port, Action, Func (+8 more)

### Community 40 - ".LoopInternal"
Cohesion: 0.25
Nodes (3): Cat, LoopVoice, Voice

### Community 41 - "KeyCode"
Cohesion: 0.02
Nodes (111): KeyCode, A, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5 (+103 more)

### Community 42 - "Mesh"
Cohesion: 0.04
Nodes (35): Kind, Plant, Cell, Action, List, FloraRenderer, Kind, Plant (+27 more)

### Community 43 - "RE:PLANET – Eine zweite Chance · Game-Design-Dokument"
Cohesion: 0.13
Nodes (15): 10. Geschichte, 11. Koop, 12. Speichern, 13. Fotomodus, 14. Barrierefreiheit und Komfort, 15. Präsentation, 1. Vision, 2. Die Spielschleife (+7 more)

### Community 44 - "RE:PLANET – Koop (1–4 Spieler)"
Cohesion: 0.09
Nodes (20): 10. Fehlermeldungen und was sie bedeuten, 1. Überblick, 2. Koop starten, 3. Ablauf einer Verbindung, 4. Rechte und Vertrauensmodus, 5. Gemeinsam spielen – was sich im Koop ändert, 6. Späte Beitritte, Verlassen und Wiederverbinden, 7. Speichern im Koop (+12 more)

### Community 45 - "JObj"
Cohesion: 0.08
Nodes (9): Dictionary, Durch Tests gefundene und behobene Fehler in der Spiellogik, List, Building, Def, W, List, JObj (+1 more)

### Community 46 - "TrashRenderer"
Cohesion: 0.16
Nodes (10): Batch, TrashType, MainMaterial, TotalUnits, Dictionary, HashSet, List, Batch (+2 more)

### Community 48 - "Material"
Cohesion: 0.11
Nodes (6): Dictionary, Mats, Texture2D&gt;, TerrainLook, Material, shaderKeywords

### Community 49 - "ObjView"
Cohesion: 0.12
Nodes (10): List, HashSet, List, Drone, IEnumerable, ObjView, DynObj, Def (+2 more)

### Community 50 - "RE:PLANET – Klang und Musik"
Cohesion: 0.22
Nodes (8): Ambience und Effekte, Architektur, Intro-Score (100 s), Musik-Engine, Prüfen ohne Unity, RE:PLANET – Klang und Musik, Stems, Stücke und Stimmungen

### Community 51 - "Synth"
Cohesion: 0.11
Nodes (9): MelNote, Note, List, LineDef, MelNote, Note, Synth, WindStyle (+1 more)

### Community 52 - "CameraRig"
Cohesion: 0.06
Nodes (27): DecoGrid, Dictionary, List, CameraRig, Cam, I, Post, DecoGrid (+19 more)

### Community 53 - ".Neustart_setzt_an_derselben_Stelle_fort"
Cohesion: 0.19
Nodes (5): SaveStore, SlotInfo, IDisposable, List, Program

### Community 55 - "Checks.cs"
Cohesion: 0.15
Nodes (6): system_collections, system_reflection, Probe, Program, TestAttribute, TestRunner

### Community 56 - "ParticleSystem"
Cohesion: 0.04
Nodes (51): Burst, CollisionModule, ColorOverLifetimeModule, EmissionModule, ForceOverLifetimeModule, LimitVelocityOverLifetimeModule, MainModule, MinMaxCurve (+43 more)

### Community 57 - "RE:PLANET – Eine zweite Chance"
Cohesion: 0.18
Nodes (11): Dokumentation, Koop (1–4 Spieler), Projekt öffnen und spielen, Projektstruktur, RE:PLANET – Eine zweite Chance, Speicherorte, Stand – was geprüft ist und was nicht, Steuerung (+3 more)

### Community 58 - "BitSet"
Cohesion: 0.27
Nodes (4): IEnumerable, BitSet, Capacity, Count

### Community 59 - "RE:PLANET – Wirtschaftstabellen"
Cohesion: 0.20
Nodes (10): Aufträge (Missionen), Fahrzeuge, Gebäude (Stützpunkt), Grundwerte, Materialien, RE:PLANET – Wirtschaftstabellen, Recyclingaufträge (Auftragstafel), Reparaturen, Ökologie, Unterschlupf (+2 more)

### Community 60 - ".Euler"
Cohesion: 0.08
Nodes (16): Cell, Facade, IList, Rng, Box, Dictionary, Func, List (+8 more)

### Community 61 - "EndingDirector"
Cohesion: 0.22
Nodes (3): Action, List, EndingDirector

### Community 63 - "RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise"
Cohesion: 0.25
Nodes (8): 1. Was im Projekt steckt und woher es kommt, 2. Unity, 3. Weitere Werkzeuge, 4. Namen und Marken, Figuren, Namen und Designs, „RE:PLANET“, RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise, Weitere Marken

### Community 64 - "RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte"
Cohesion: 0.25
Nodes (7): Entscheidungen, Grenzen (Stand dieser Umgebung), Nächste Schritte, Prüfwerkzeuge ohne Unity, RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte, Stand, Umsetzung in 10 Punkten

### Community 65 - "GameApp.cs"
Cohesion: 0.25
Nodes (7): AppMode, Ending, Intro, Loading, Menu, PlanetSelect, Playing

### Community 66 - "RePlanetBuild.cs"
Cohesion: 0.38
Nodes (5): RePlanet.EditorTools, unityeditor, unityeditor_build_reporting, unityeditor_scenemanagement, unityengine_scenemanagement

### Community 67 - ".Get"
Cohesion: 0.14
Nodes (17): Geo, Glow, GlowSet, MeshData, Part, H, Func, KeyValuePair (+9 more)

### Community 68 - "RE:PLANET – Trailer- und Clip-Ideen"
Cohesion: 0.29
Nodes (7): 1. „Eine Welle, ein Haufen Schrott weniger“ – die Magnetwelle, 2. „Das ist zu groß für dich, MIKO“ – Kran und Transporter räumen ein Wrack, 3. „Die Stadt erwacht“ – ein Großprojekt wird fertig, 4. „Such dir ein Dach“ – Sandsturm und Nacht, 5. „Vorher – nachher“ im Fotomodus (plus Koop-Finale), Allgemeine Hinweise für den Dreh, RE:PLANET – Trailer- und Clip-Ideen

### Community 69 - "Camera"
Cohesion: 0.05
Nodes (32): StereoscopicEye, AudioHighPassFilter, AudioListener, AudioLowPassFilter, CameraClearFlags, Depth, Nothing, Skybox (+24 more)

### Community 71 - "Automatisierte Tests und Balancing"
Cohesion: 0.29
Nodes (6): Automatisierte Tests und Balancing, Balancing-Messwerte (nach Anpassung), Beobachtungen / offene Punkte, Kampagnen-Bot (`dotnet run -c Release -- balance`), Nicht getestet, RE:PLANET – Testbericht

### Community 72 - "Müll je Planet und Bereich"
Cohesion: 0.33
Nodes (6): Alle Planeten, Müll je Planet und Bereich, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 73 - "Großprojekte"
Cohesion: 0.40
Nodes (5): Großprojekte, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 74 - "Texture2D"
Cohesion: 0.07
Nodes (18): List, RuntimeInitializeOnLoadMethod, Body, PlanetSelectScene, Active, Descending, Focused, I (+10 more)

### Community 75 - ".Clamp"
Cohesion: 0.17
Nodes (4): VehicleDef, Motor, MoverState, M

### Community 76 - "UnityStub2.cs"
Cohesion: 0.05
Nodes (45): Attribute, AudioSettings, dspTime, outputSampleRate, DefaultExecutionOrderAttribute, ExecuteAlways, FullScreenMode, ExclusiveFullScreen (+37 more)

### Community 77 - "Cat"
Cohesion: 0.50
Nodes (4): Cat, Ambient, Sfx, Ui

### Community 78 - "ShipArrival"
Cohesion: 0.09
Nodes (9): Drop, Engine, List, RuntimeInitializeOnLoadMethod, Drop, ShipArrival, CinematicActive, Density (+1 more)

### Community 87 - "Transform"
Cohesion: 0.05
Nodes (25): IEnumerable, Glow, List, FreighterModel, Dictionary, ChunkBuilder, ChunkCount, VertexCount (+17 more)

### Community 88 - ".LogWarning"
Cohesion: 0.07
Nodes (16): LookInfo, PostFX, I, LastExposure, Running, Cubemap, Texture2DArray, Dictionary (+8 more)

### Community 89 - "IntroDirector"
Cohesion: 0.13
Nodes (10): Ark, Bot, Crowd, Look, Dictionary, HashSet, IntroDirector, Dt (+2 more)

### Community 90 - "MapCamera"
Cohesion: 0.08
Nodes (17): List, MapCamera, Cam, Distance, Failed, Following, HasFrame, I (+9 more)

### Community 91 - "Mathf"
Cohesion: 0.08
Nodes (7): MonoOrStereoscopicEye, Mathf, TextureWrapMode, Clamp, Mirror, MirrorOnce, Repeat

### Community 92 - "Narrator"
Cohesion: 0.10
Nodes (13): Cue, 7. Untertitel, Dictionary, Cue, Narrator, HasRecordings, Speaking, AudioReverbFilter (+5 more)

### Community 93 - ".MakeEngine"
Cohesion: 0.14
Nodes (15): Engine, AnimationCurve, length, Gradient, GradientAlphaKey, GradientColorKey, Keyframe, MinMaxCurve (+7 more)

### Community 94 - "Color"
Cohesion: 0.08
Nodes (18): P, Color, black, blue, clear, cyan, gamma, gray (+10 more)

### Community 96 - "Component"
Cohesion: 0.10
Nodes (8): Action, HashSet, Type, Component, IsDead, tag, transform, World

### Community 97 - ".Abs"
Cohesion: 0.19
Nodes (4): Icon, SurfaceLook, Custom, DetailShadersAllowed

### Community 98 - "Session"
Cohesion: 0.13
Nodes (11): RateLimit, Dictionary, List, Queue, RateLimit, RidCache, Session, Closed (+3 more)

### Community 99 - ".Decode"
Cohesion: 0.21
Nodes (6): SaveCodec, Test, SaveTests, Func, List, TestKit

### Community 100 - "QualitySettings"
Cohesion: 0.09
Nodes (21): AnisotropicFiltering, Disable, Enable, ForceEnable, ColorSpace, Gamma, Linear, Uninitialized (+13 more)

### Community 101 - "Renderer"
Cohesion: 0.09
Nodes (21): TrailRenderer, MotionVectorGenerationMode, Camera, ForceNoMotion, Object, Renderer, bounds, isVisible (+13 more)

### Community 102 - "Quaternion"
Cohesion: 0.13
Nodes (4): Quaternion, eulerAngles, identity, normalized

### Community 103 - "ReflectionProbe"
Cohesion: 0.10
Nodes (17): ReflectionProbe, texture, ReflectionProbeClearFlags, Skybox, SolidColor, ReflectionProbeMode, Baked, Custom (+9 more)

### Community 104 - "Matrix4x4"
Cohesion: 0.13
Nodes (9): Agent, Crowd, Matrix4x4, identity, inverse, lossyScale, rotation, transpose (+1 more)

### Community 105 - "Checks"
Cohesion: 0.18
Nodes (8): BindingFlags, Checks, Dictionary, Exception, HashSet, List, MethodInfo, Type

### Community 106 - "MainModule"
Cohesion: 0.10
Nodes (20): MinMaxGradient, ColorOverLifetimeModule, MainModule, ParticleSystemCullingMode, AlwaysSimulate, Automatic, Pause, PauseAndCatchup (+12 more)

### Community 107 - "UnityStub.cs"
Cohesion: 0.12
Nodes (16): Exception, FilterMode, Bilinear, Point, Trilinear, IndexFormat, UInt16, UInt32 (+8 more)

### Community 108 - "Object"
Cohesion: 0.12
Nodes (4): RuntimeInitializeOnLoadMethod, MethodInfo, Object, IsDead

### Community 109 - ".BuildWindows"
Cohesion: 0.24
Nodes (5): MenuItem, RePlanetBuild, Result, Result, TimeSpan

### Community 110 - "LineRenderer"
Cohesion: 0.13
Nodes (11): EmitParams, List, LineAlignment, TransformZ, View, LineRenderer, positionCount, LineTextureMode (+3 more)

### Community 111 - "SystemLanguage"
Cohesion: 0.12
Nodes (14): Application, platform, systemLanguage, RuntimePlatform, LinuxPlayer, OSXPlayer, WindowsPlayer, SystemLanguage (+6 more)

### Community 112 - "Font"
Cohesion: 0.14
Nodes (12): FontStyle, Bold, BoldAndItalic, Italic, Normal, CharacterInfo, Font, TextAlignment (+4 more)

### Community 113 - "AudioManager"
Cohesion: 0.13
Nodes (6): AudioManager, I, IntroReady, IntroTime, VoiceGain, VoiceParent

### Community 114 - ".DrawMesh"
Cohesion: 0.23
Nodes (8): Call, Call, Graphics, ShadowCastingMode, Off, On, ShadowsOnly, TwoSided

### Community 115 - "AudioClip"
Cohesion: 0.16
Nodes (7): AudioClip, loadState, AudioDataLoadState, Failed, Loaded, Loading, Unloaded

### Community 116 - "AudioReverbPreset"
Cohesion: 0.14
Nodes (14): AudioReverbPreset, Arena, Auditorium, Bathroom, Cave, Concerthall, Generic, Hangar (+6 more)

### Community 117 - "MonoBehaviour"
Cohesion: 0.18
Nodes (7): PostFX, I, Running, UIRoot, IEnumerator, Coroutine, MonoBehaviour

### Community 118 - "RenderSettings"
Cohesion: 0.15
Nodes (13): FogMode, Exponential, ExponentialSquared, Linear, AmbientMode, Custom, Flat, Skybox (+5 more)

### Community 119 - "TextureFormat"
Cohesion: 0.15
Nodes (13): TextureFormat, Alpha8, ARGB32, DXT1, DXT5, R16, R8, RFloat (+5 more)

### Community 120 - "ToastKind"
Cohesion: 0.18
Nodes (7): Toast, ToastKind, Error, Info, Story, Success, Warning

### Community 121 - "EventType"
Cohesion: 0.17
Nodes (11): Event, EventType, KeyDown, KeyUp, Layout, MouseDown, MouseDrag, MouseMove (+3 more)

### Community 122 - "BlendMode"
Cohesion: 0.17
Nodes (12): BlendMode, DstAlpha, DstColor, One, OneMinusDstAlpha, OneMinusDstColor, OneMinusSrcAlpha, OneMinusSrcColor (+4 more)

### Community 125 - "HideFlags"
Cohesion: 0.20
Nodes (10): HideFlags, DontSave, DontSaveInBuild, DontSaveInEditor, DontUnloadUnusedAsset, HideAndDontSave, HideInHierarchy, HideInInspector (+2 more)

### Community 126 - "ParticleSystemShapeType"
Cohesion: 0.20
Nodes (10): ParticleSystemShapeType, Box, Circle, Cone, Donut, Edge, Hemisphere, Mesh (+2 more)

### Community 127 - "CompareFunction"
Cohesion: 0.20
Nodes (10): CompareFunction, Always, Disabled, Equal, Greater, GreaterEqual, Less, LessEqual (+2 more)

### Community 128 - "SystemInfo"
Cohesion: 0.20
Nodes (9): SystemInfo, deviceUniqueIdentifier, graphicsDeviceName, graphicsMemorySize, maxTextureSize, operatingSystem, supportsComputeShaders, supportsInstancing (+1 more)

### Community 129 - "TextAnchor"
Cohesion: 0.20
Nodes (10): TextAnchor, LowerCenter, LowerLeft, LowerRight, MiddleCenter, MiddleLeft, MiddleRight, UpperCenter (+2 more)

### Community 130 - "Input"
Cohesion: 0.22
Nodes (7): Dictionary, HashSet, Input, anyKey, anyKeyDown, inputString, mouseScrollDelta

### Community 131 - "RE:PLANET – Sprechertext Intro und Abspann"
Cohesion: 0.25
Nodes (7): 1. Die Stimme, 2. Intro (100 Sekunden, 14 Zeilen), 3. Abspann (5 Zeilen, optional), 4. Aufnahme und Lieferung, 5. Selbst aufnehmen, 6. Mit einem Stimmdienst erzeugen, RE:PLANET – Sprechertext Intro und Abspann

### Community 132 - "RE:PLANET – Sprechertext für ElevenLabs"
Cohesion: 0.29
Nodes (6): 1. Stimme auswählen, 2. Einstellungen, 3. Intro (14 Zeilen), 4. Abspann (5 Zeilen), 5. Ins Spiel bringen, RE:PLANET – Sprechertext für ElevenLabs

### Community 133 - "ParticleSystemRenderMode"
Cohesion: 0.29
Nodes (7): ParticleSystemRenderMode, Billboard, HorizontalBillboard, Mesh, None, Stretch, VerticalBillboard

### Community 134 - "RenderQueue"
Cohesion: 0.29
Nodes (7): RenderQueue, AlphaTest, Background, Geometry, GeometryLast, Overlay, Transparent

### Community 135 - "MeshTopology"
Cohesion: 0.29
Nodes (6): MeshTopology, Lines, LineStrip, Points, Quads, Triangles

### Community 136 - "PrimitiveType"
Cohesion: 0.29
Nodes (7): PrimitiveType, Capsule, Cube, Cylinder, Plane, Quad, Sphere

### Community 137 - "LightShadowResolution"
Cohesion: 0.33
Nodes (6): LightShadowResolution, FromQualitySettings, High, Low, Medium, VeryHigh

### Community 138 - "ParticleSystemRenderSpace"
Cohesion: 0.33
Nodes (6): ParticleSystemRenderSpace, Facing, Local, Velocity, View, World

### Community 139 - "MaterialGlobalIlluminationFlags"
Cohesion: 0.33
Nodes (6): MaterialGlobalIlluminationFlags, AnyEmissive, BakedEmissive, EmissiveIsBlack, None, RealtimeEmissive

### Community 142 - "ParticleSystemCurveMode"
Cohesion: 0.40
Nodes (5): ParticleSystemCurveMode, Constant, Curve, TwoConstants, TwoCurves

### Community 143 - "Windows-Build automatisch auf GitHub"
Cohesion: 0.50
Nodes (3): Build starten und herunterladen, Einmalige Einrichtung (im GitHub-Repository, nicht im Chat), Windows-Build automatisch auf GitHub

### Community 144 - "Testanleitung für echte Spieltests (Leistung und Koop)"
Cohesion: 0.50
Nodes (3): 1. Leistung messen, 2. Koop mit zwei PCs testen, Testanleitung für echte Spieltests (Leistung und Koop)

### Community 146 - "Scene"
Cohesion: 0.50
Nodes (4): Scene, Delivery, Landing, None

### Community 147 - "LightShadows"
Cohesion: 0.50
Nodes (4): LightShadows, Hard, None, Soft

### Community 148 - ".Stop"
Cohesion: 0.50
Nodes (3): ParticleSystemStopBehavior, StopEmitting, StopEmittingAndClear

### Community 149 - "CullMode"
Cohesion: 0.50
Nodes (4): CullMode, Back, Front, Off

### Community 150 - "3. Planeten"
Cohesion: 0.67
Nodes (3): 3. Planeten, Bereiche, Wiederherstellungsstufen (je Bereich)

## Knowledge Gaps
- **930 isolated node(s):** `BassNote`, `Top`, `Note`, `Beat`, `BarLen` (+925 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1260 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Vector3` connect `Vector3` to `Input`, `UIRoot`, `.Max`, `WorldView`, `V3`, `.OnGUI`, `.Min`, `PlanetLayout`, `Atmosphere`, `FxView`, `RobotModel`, `.Clamp01`, `.Step`, `GameObject`, `.LoopInternal`, `Mesh`, `JObj`, `TrashRenderer`, `CameraRig`, `ParticleSystem`, `.Euler`, `.Get`, `Camera`, `Texture2D`, `ShipArrival`, `Transform`, `.LogWarning`, `IntroDirector`, `MapCamera`, `Mathf`, `.MakeEngine`, `QualitySettings`, `Quaternion`, `ReflectionProbe`, `Matrix4x4`, `Checks`, `UnityStub.cs`, `Object`, `LineRenderer`, `AudioManager`, `.DrawMesh`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **Why does `WorldView` connect `WorldView` to `.HeightAt`, `UIRoot`, `WorldState`, `.Max`, `V3`, `Vector3`, `PlanetLayout`, `system_collections_generic`, `GameObject`, `Mesh`, `JObj`, `TrashRenderer`, `Material`, `CameraRig`, `ParticleSystem`, `.Euler`, `.Get`, `Texture2D`, `Transform`, `.LogWarning`, `Color`, `Quaternion`, `Font`, `MonoBehaviour`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **Why does `Material` connect `Material` to `RePlanetSetup`, `WorldView`, `MaterialGlobalIlluminationFlags`, `Atmosphere`, `FxView`, `RobotModel`, `.LateUpdate`, `GameObject`, `Mesh`, `TrashRenderer`, `.Euler`, `.Get`, `Texture2D`, `ShipArrival`, `Transform`, `.LogWarning`, `IntroDirector`, `MapCamera`, `Color`, `Component`, `.Abs`, `Renderer`, `Matrix4x4`, `UnityStub.cs`, `Object`, `Font`, `.DrawMesh`, `RenderSettings`?**
  _High betweenness centrality (0.058) - this node is a cross-community bridge._
- **Are the 23 inferred relationships involving `Vector3` (e.g. with `.EnsureWaterNormal()` and `.Animate()`) actually correct?**
  _`Vector3` has 23 INFERRED edges - model-reasoned connections that need verification._
- **What connects `BassNote`, `Top`, `Note` to the rest of the system?**
  _930 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `.HeightAt` be split into smaller, more focused modules?**
  _Cohesion score 0.1262699564586357 - nodes in this community are weakly interconnected._
- **Should `GameData` be split into smaller, more focused modules?**
  _Cohesion score 0.06493506493506493 - nodes in this community are weakly interconnected._