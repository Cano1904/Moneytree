# Graph Report - Moneytree  (2026-09-30)

## Corpus Check
- 145 files · ~435,330 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 22 file(s) not represented in the graph (top: .shader 9, .cginc 5, .asmdef 3)

## Summary
- 4601 nodes · 17293 edges · 168 communities (144 shown, 24 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 2170 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `79db7d5a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Game
- WorldGen
- GameData
- CampaignBot
- UIRoot
- GameApp
- Wildlife
- RePlanetSetup
- .Min
- Program
- NetRig
- WorldView
- Arr
- WorldState
- .Music
- UISkin
- AudioManager
- .L
- .Sin
- sky_preview.py
- GameAction
- PlanetLayout
- Atmosphere
- .Pole
- FxView
- RePlanet.Core
- UIScreen
- RobotModel
- .LogException
- system
- Transport.cs
- .True
- Loc
- Texture2D
- InputMap
- manifest.json
- .Clamp
- GameObject
- .Main
- AudioSource
- KeyCode
- Mesh
- RE:PLANET – Eine zweite Chance · Game-Design-Dokument
- RE:PLANET – Koop (1–4 Spieler)
- JObj
- TrashRenderer
- CityLife
- Material
- Vector2
- .Darkness
- Synth
- RenderTextureFormat
- SaveStore
- .Produce
- LocTests
- ParticleSystem
- RE:PLANET – Eine zweite Chance
- BitSet
- RE:PLANET – Wirtschaftstabellen
- Backdrop
- GroundMarks
- .Run
- RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise
- WindLook
- GameApp.cs
- RePlanetBuild.cs
- .Max
- RE:PLANET – Trailer- und Clip-Ideen
- Camera
- Automatisierte Tests und Balancing
- Müll je Planet und Bereich
- Großprojekte
- Color
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
- Vector3
- PostFX
- MapCamera
- Mathf
- Narrator
- AnimationCurve
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
- Font
- AudioManager
- LifeCommon
- AudioClip
- AudioReverbPreset
- MonoBehaviour
- AmbientMode
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
- .HeightAt
- Input
- RE:PLANET – Sprechertext Intro und Abspann
- RE:PLANET – Sprechertext für ElevenLabs
- ParticleSystemRenderMode
- RenderQueue
- CameraRig
- PrimitiveType
- LightShadowResolution
- ParticleSystemRenderSpace
- MaterialGlobalIlluminationFlags
- FullScreenMode
- UINav
- ParticleSystemCurveMode
- Windows-Build automatisch auf GitHub
- Testanleitung für echte Spieltests (Leistung und Koop)
- .RenderLine
- Scene
- LightShadows
- CullMode
- InstanceBatch
- Harness.csproj
- unityengine
- unityengine_rendering
- EmissionModule
- RE:PLANET – Architektur
- .ParseMel
- ParticleSystemGradientMode
- .SaveLife
- DepthTextureMode
- LightType
- ParticleSystemSortMode
- 9. Aufträge und Nebeninhalte
- FogMode
- LightmapBakeType
- RenderTextureReadWrite
- ScaleMode

## God Nodes (most connected - your core abstractions)
1. `Vector3` - 372 edges
2. `UIRoot` - 223 edges
3. `WorldView` - 185 edges
4. `Material` - 185 edges
5. `Game` - 170 edges
6. `GameApp` - 161 edges
7. `IntroDirector` - 129 edges
8. `KeyCode` - 123 edges
9. `JObj` - 122 edges
10. `AudioManager` - 109 edges

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

## Communities (168 total, 24 thin omitted)

### Community 0 - "Game"
Cohesion: 0.13
Nodes (6): Dictionary, ActResult, Game, Now, TimeScale, PlayerData

### Community 1 - "WorldGen"
Cohesion: 0.18
Nodes (6): Ctx, Dictionary, Func, List, Ctx, WorldGen

### Community 2 - "GameData"
Cohesion: 0.06
Nodes (27): CultureInfo, Erweiterungspunkte, Dictionary, List, BuildingDef, CosmeticDef, GateDef, Grade (+19 more)

### Community 3 - "CampaignBot"
Cohesion: 0.08
Nodes (15): Stopwatch, BalanceRun, CampaignBot, L, PS, S, Shutdowns, T (+7 more)

### Community 4 - "UIRoot"
Cohesion: 0.05
Nodes (19): MapMark, MapTex, ProfilerRecorder, List, UIRoot, List, Dictionary, List (+11 more)

### Community 5 - "GameApp"
Cohesion: 0.06
Nodes (24): List, Hud, Toast, UIState, BlocksGameplay, Action, List, Type (+16 more)

### Community 6 - "Wildlife"
Cohesion: 0.10
Nodes (15): Animal, Group, Dictionary, List, RuntimeInitializeOnLoadMethod, Animal, Group, Wildlife (+7 more)

### Community 7 - "RePlanetSetup"
Cohesion: 0.11
Nodes (16): Changes, MatSpec, Action, Func, List, MenuItem, Changes, MatSpec (+8 more)

### Community 9 - "Program"
Cohesion: 0.27
Nodes (4): Func, IEnumerable, List, Program

### Community 10 - "NetRig"
Cohesion: 0.09
Nodes (17): IDisposable, HostServer, Online, Port, Func, List, Test, NetRig (+9 more)

### Community 11 - "WorldView"
Cohesion: 0.06
Nodes (35): Facade, IList, Rng, Box, Dictionary, ChunkBuilder, ChunkCount, VertexCount (+27 more)

### Community 12 - "Arr"
Cohesion: 0.15
Nodes (4): Arr, BarLen, Beat, Spec

### Community 13 - "WorldState"
Cohesion: 0.03
Nodes (60): Erzähler im Spiel und Radio (`Core/Sim/Story.cs`), Welt und Speicherung, Zusatzsysteme (Lieferlimit, Sturm abwarten, Helfer, Erfolge, Schnellreise, Ereignisse), MissionDef, Dictionary, List, List, Rules (+52 more)

### Community 14 - ".Music"
Cohesion: 0.21
Nodes (8): Arr, Chord, MusicSet, Dictionary, Chord, BassNote, Top, MusicSet

### Community 15 - "UISkin"
Cohesion: 0.13
Nodes (6): Dictionary, UISkin, Contrast, GUIContent, GUIStyle, RectOffset

### Community 16 - "AudioManager"
Cohesion: 0.05
Nodes (25): Job, LoopVoice, MusicClips, Dictionary, List, AudioManager, Dictionary, HashSet (+17 more)

### Community 17 - ".L"
Cohesion: 0.12
Nodes (9): Lokalisierung (`Core/Loc`), Rect, center, max, min, xMax, xMin, yMax (+1 more)

### Community 18 - ".Sin"
Cohesion: 0.06
Nodes (20): Car, Kind, Plant, Action, List, FloraRenderer, MaxShear, WindStrength (+12 more)

### Community 19 - "sky_preview.py"
Cohesion: 0.08
Nodes (43): glob, math, numpy, os, pil, re, subprocess, sys (+35 more)

### Community 20 - "GameAction"
Cohesion: 0.05
Nodes (37): GameAction, AltTool, Build, DiveDown, DiveUp, Emote, Interact, Inventory (+29 more)

### Community 21 - "PlanetLayout"
Cohesion: 0.15
Nodes (13): Dictionary, List, BaseLayout, FloorPatch, GateLayout, Mound, PlanetLayout, Id (+5 more)

### Community 22 - "Atmosphere"
Cohesion: 0.09
Nodes (15): LookInfo, PlanetSky, Dictionary, List, Atmosphere, CloudCover, Darkness, I (+7 more)

### Community 23 - ".Pole"
Cohesion: 0.22
Nodes (3): OnePole, PNoise, Svf

### Community 24 - "FxView"
Cohesion: 0.09
Nodes (16): Arc, Darstellung, Ghost, IEnumerator, List, Stack, FxView, Density (+8 more)

### Community 25 - "RePlanet.Core"
Cohesion: 0.11
Nodes (12): RePlanet.Core, UnityEngine, UnityEngine.Rendering, RePlanet, Terrain, BuildMode, PhotoMode, FeatureToasts (+4 more)

### Community 26 - "UIScreen"
Cohesion: 0.11
Nodes (18): UIScreen, Coop, Credits, Ending, Intro, Loading, MainMenu, Map (+10 more)

### Community 27 - "RobotModel"
Cohesion: 0.16
Nodes (7): RobotModel, CurrentGesture, GestureCount, Mood, Action, Dictionary, List

### Community 29 - ".LogException"
Cohesion: 0.12
Nodes (6): Action, List, EndingDirector, Exception, Exception, UnityException

### Community 30 - "system"
Cohesion: 0.06
Nodes (18): RePlanet.DocGen, RePlanet.Server, RePlanet.SmokeTest, IntroTimeline, Shot, Shot, system, system_collections (+10 more)

### Community 31 - "Transport.cs"
Cohesion: 0.07
Nodes (22): ConcurrentQueue, Dictionary, List, IClientTransport, Connected, Error, Failed, IServerTransport (+14 more)

### Community 32 - ".True"
Cohesion: 0.09
Nodes (24): Ergebnis des Testlaufs, Exception, Func, Test, EconomyTests, TestHelpers, Func, List (+16 more)

### Community 33 - "Loc"
Cohesion: 0.07
Nodes (19): DataField, IDictionary, Dictionary, List, Regex, Loc, English, EnglishTable (+11 more)

### Community 34 - "Texture2D"
Cohesion: 0.11
Nodes (11): Style, TerrainLook, Style, GUIStyleState, Color32, Texture2D, blackTexture, format (+3 more)

### Community 35 - "InputMap"
Cohesion: 0.15
Nodes (3): Dictionary, InputMap, UsingPad

### Community 36 - "manifest.json"
Cohesion: 0.09
Nodes (21): com.unity.modules.animation, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem, com.unity.modules.physics, com.unity.modules.screencapture (+13 more)

### Community 37 - ".Clamp"
Cohesion: 0.09
Nodes (4): PlanetDef, MapMark, GUI, GUISkin

### Community 38 - "GameObject"
Cohesion: 0.05
Nodes (19): Particle, Dictionary, List, ActorsView, I, LocalRobot, Particle, ParticleSystemRenderer (+11 more)

### Community 39 - ".Main"
Cohesion: 0.20
Nodes (5): Random, Ids, IDisposable, List, Program

### Community 40 - "AudioSource"
Cohesion: 0.10
Nodes (9): Cat, LoopVoice, Slot, Voice, AudioRolloffMode, Custom, Linear, Logarithmic (+1 more)

### Community 41 - "KeyCode"
Cohesion: 0.02
Nodes (111): KeyCode, A, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5 (+103 more)

### Community 42 - "Mesh"
Cohesion: 0.09
Nodes (17): List, Mesh, colors, colors32, normals, subMeshCount, tangents, triangles (+9 more)

### Community 43 - "RE:PLANET – Eine zweite Chance · Game-Design-Dokument"
Cohesion: 0.12
Nodes (17): 10. Geschichte, 11. Koop, 12. Speichern, 13. Fotomodus, 14. Barrierefreiheit und Komfort, 15. Präsentation, 1. Vision, 2. Die Spielschleife (+9 more)

### Community 44 - "RE:PLANET – Koop (1–4 Spieler)"
Cohesion: 0.14
Nodes (13): 10. Fehlermeldungen und was sie bedeuten, 1. Überblick, 2. Koop starten, 4. Rechte und Vertrauensmodus, 5. Gemeinsam spielen – was sich im Koop ändert, 6. Späte Beitritte, Verlassen und Wiederverbinden, 7. Speichern im Koop, 8. Netzwerk einrichten: Ports, Firewall, Router (+5 more)

### Community 45 - "JObj"
Cohesion: 0.04
Nodes (34): Dictionary, Durch Tests gefundene und behobene Fehler in der Spiellogik, RateLimit, Action, Dictionary, List, GameClient, Me (+26 more)

### Community 46 - "TrashRenderer"
Cohesion: 0.15
Nodes (11): Batch, TrashType, MainMaterial, TotalUnits, Ghost, Dictionary, HashSet, List (+3 more)

### Community 47 - "CityLife"
Cohesion: 0.08
Nodes (19): Flag, Holo, Dictionary, List, RuntimeInitializeOnLoadMethod, Car, CityLife, CarsAlive (+11 more)

### Community 48 - "Material"
Cohesion: 0.09
Nodes (8): Dictionary, Mats, Func, Texture2D&gt;, TerrainLook, Material, shaderKeywords, Vector4

### Community 49 - "Vector2"
Cohesion: 0.07
Nodes (14): Color32Key, IEquatable, KeyValuePair, Color32Key, Palette, Matte, Vector2, magnitude (+6 more)

### Community 50 - ".Darkness"
Cohesion: 0.20
Nodes (8): Ambience und Effekte, Architektur, Intro-Score (100 s), Musik-Engine, Prüfen ohne Unity, RE:PLANET – Klang und Musik, Stems, Stücke und Stimmungen

### Community 51 - "Synth"
Cohesion: 0.12
Nodes (4): Note, Synth, WindStyle, WindStyle

### Community 52 - "RenderTextureFormat"
Cohesion: 0.13
Nodes (14): RenderTextureFormat, ARGB1555, ARGB2101010, ARGB32, ARGB4444, ARGBHalf, Default, DefaultHDR (+6 more)

### Community 55 - "LocTests"
Cohesion: 0.13
Nodes (11): SortedSet, HashSet, KeyValuePair, List, Regex, Test, LocTests, Src (+3 more)

### Community 56 - "ParticleSystem"
Cohesion: 0.04
Nodes (47): CollisionModule, ColorOverLifetimeModule, EmissionModule, EmitParams, ForceOverLifetimeModule, LimitVelocityOverLifetimeModule, MainModule, MinMaxCurve (+39 more)

### Community 57 - "RE:PLANET – Eine zweite Chance"
Cohesion: 0.18
Nodes (11): Dokumentation, Koop (1–4 Spieler), Projekt öffnen und spielen, Projektstruktur, RE:PLANET – Eine zweite Chance, Speicherorte, Stand – was geprüft ist und was nicht, Steuerung (+3 more)

### Community 58 - "BitSet"
Cohesion: 0.27
Nodes (4): IEnumerable, BitSet, Capacity, Count

### Community 59 - "RE:PLANET – Wirtschaftstabellen"
Cohesion: 0.20
Nodes (10): Aufträge (Missionen), Fahrzeuge, Gebäude (Stützpunkt), Grundwerte, Materialien, RE:PLANET – Wirtschaftstabellen, Recyclingaufträge (Auftragstafel), Reparaturen, Ökologie, Unterschlupf (+2 more)

### Community 60 - "Backdrop"
Cohesion: 0.11
Nodes (12): Cell, Dictionary, Func, List, Backdrop, LastDrawn, RingVertices, TotalInstances (+4 more)

### Community 61 - "GroundMarks"
Cohesion: 0.08
Nodes (17): Puddle, Dictionary, List, RuntimeInitializeOnLoadMethod, GroundMarks, BaseLife, Capacity, DrawnStamps (+9 more)

### Community 63 - "RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise"
Cohesion: 0.25
Nodes (8): 1. Was im Projekt steckt und woher es kommt, 2. Unity, 3. Weitere Werkzeuge, 4. Namen und Marken, Figuren, Namen und Designs, „RE:PLANET“, RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise, Weitere Marken

### Community 64 - "WindLook"
Cohesion: 0.08
Nodes (18): Belebte Welt (Tiere, Spuren, Wind, Stadt erwacht 2.0, MIKOs Mimik), Entscheidungen, Erweiterungen (Radio, Stadtklänge, Controller-Bauansicht, Englisch, Erzähler, Leistungsanzeige), Grenzen (Stand dieser Umgebung), Nächste Schritte, Prüfwerkzeuge ohne Unity, RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte, Stand (+10 more)

### Community 65 - "GameApp.cs"
Cohesion: 0.25
Nodes (7): AppMode, Ending, Intro, Loading, Menu, PlanetSelect, Playing

### Community 66 - "RePlanetBuild.cs"
Cohesion: 0.38
Nodes (5): RePlanet.EditorTools, unityeditor, unityeditor_build_reporting, unityeditor_scenemanagement, unityengine_scenemanagement

### Community 67 - ".Max"
Cohesion: 0.09
Nodes (32): Agent, Ark, Bot, Crowd, Geo, Glow, GlowSet, Look (+24 more)

### Community 68 - "RE:PLANET – Trailer- und Clip-Ideen"
Cohesion: 0.29
Nodes (7): 1. „Eine Welle, ein Haufen Schrott weniger“ – die Magnetwelle, 2. „Das ist zu groß für dich, MIKO“ – Kran und Transporter räumen ein Wrack, 3. „Die Stadt erwacht“ – ein Großprojekt wird fertig, 4. „Such dir ein Dach“ – Sandsturm und Nacht, 5. „Vorher – nachher“ im Fotomodus (plus Koop-Finale), Allgemeine Hinweise für den Dreh, RE:PLANET – Trailer- und Clip-Ideen

### Community 69 - "Camera"
Cohesion: 0.07
Nodes (21): MonoOrStereoscopicEye, StereoscopicEye, CameraClearFlags, Depth, Nothing, Skybox, SolidColor, Camera (+13 more)

### Community 71 - "Automatisierte Tests und Balancing"
Cohesion: 0.29
Nodes (6): Automatisierte Tests und Balancing, Balancing-Messwerte (nach Anpassung), Beobachtungen / offene Punkte, Kampagnen-Bot (`dotnet run -c Release -- balance`), Nicht getestet, RE:PLANET – Testbericht

### Community 72 - "Müll je Planet und Bereich"
Cohesion: 0.33
Nodes (6): Alle Planeten, Müll je Planet und Bereich, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 73 - "Großprojekte"
Cohesion: 0.40
Nodes (5): Großprojekte, NIVALIS – Die eingefrorene Zukunft, PELAGIA – Der vermüllte Ozeanplanet, PYRA – Die rostrote Industriewelt, TERRA – Die vergessene Erde

### Community 74 - "Color"
Cohesion: 0.06
Nodes (27): Look, List, RuntimeInitializeOnLoadMethod, Body, PlanetSelectScene, Active, Descending, Focused (+19 more)

### Community 75 - ".Clamp"
Cohesion: 0.14
Nodes (6): List, Motor, MotorEnv, Tmp, MoverState, M

### Community 76 - "UnityStub2.cs"
Cohesion: 0.06
Nodes (39): Attribute, List, AudioHighPassFilter, AudioLowPassFilter, AudioSettings, dspTime, outputSampleRate, Cubemap (+31 more)

### Community 77 - "Cat"
Cohesion: 0.50
Nodes (4): Cat, Ambient, Sfx, Ui

### Community 78 - "ShipArrival"
Cohesion: 0.07
Nodes (15): Body, Drop, Engine, List, RuntimeInitializeOnLoadMethod, Drop, Engine, ShipArrival (+7 more)

### Community 87 - "Vector3"
Cohesion: 0.02
Nodes (55): Beam, Capsule, IEnumerable, SkyBody, Flag, Dictionary, List, Stack (+47 more)

### Community 88 - "PostFX"
Cohesion: 0.14
Nodes (5): PostFX, I, LastExposure, Running, RenderTexture

### Community 90 - "MapCamera"
Cohesion: 0.08
Nodes (17): List, MapCamera, Cam, Distance, Failed, Following, HasFrame, I (+9 more)

### Community 91 - "Mathf"
Cohesion: 0.08
Nodes (11): LookInfo, RenderSettings, Mathf, Texture, height, width, TextureWrapMode, Clamp (+3 more)

### Community 92 - "Narrator"
Cohesion: 0.12
Nodes (12): Cue, 7. Untertitel, Pending, Dictionary, List, Cue, Narrator, GameLineActive (+4 more)

### Community 93 - "AnimationCurve"
Cohesion: 0.39
Nodes (3): AnimationCurve, length, Keyframe

### Community 94 - ".Main"
Cohesion: 0.23
Nodes (5): List, Program, Tri, P, ColorUtility

### Community 96 - "Component"
Cohesion: 0.09
Nodes (7): Action, HashSet, Component, IsDead, tag, transform, World

### Community 97 - "SurfaceLook"
Cohesion: 0.23
Nodes (4): Icon, SurfaceLook, Custom, DetailShadersAllowed

### Community 98 - "MikoGestures"
Cohesion: 0.15
Nodes (8): Dictionary, KeyValuePair, List, RuntimeInitializeOnLoadMethod, MikoGestures, Hops, I, Waves

### Community 100 - "QualitySettings"
Cohesion: 0.09
Nodes (21): AnisotropicFiltering, Disable, Enable, ForceEnable, ColorSpace, Gamma, Linear, Uninitialized (+13 more)

### Community 101 - "Renderer"
Cohesion: 0.07
Nodes (22): TrailRenderer, MaterialPropertyBlock, MotionVectorGenerationMode, Camera, ForceNoMotion, Object, Renderer, bounds (+14 more)

### Community 102 - "TcpClientTransport"
Cohesion: 0.15
Nodes (12): BlockingCollection, 3. Ablauf einer Verbindung, Schutzmechanismen, NetworkStream, Conn, Framing, TcpClientTransport, Connected (+4 more)

### Community 103 - "ReflectionProbe"
Cohesion: 0.11
Nodes (17): ReflectionProbe, texture, ReflectionProbeClearFlags, Skybox, SolidColor, ReflectionProbeMode, Baked, Custom (+9 more)

### Community 104 - "Matrix4x4"
Cohesion: 0.09
Nodes (17): Call, Dictionary, DecoGrid, Call, Graphics, Matrix4x4, identity, inverse (+9 more)

### Community 105 - "Checks"
Cohesion: 0.13
Nodes (8): BindingFlags, Checks, Dictionary, Exception, HashSet, List, MethodInfo, Type

### Community 106 - "MainModule"
Cohesion: 0.10
Nodes (20): MinMaxGradient, ColorOverLifetimeModule, MainModule, ParticleSystemCullingMode, AlwaysSimulate, Automatic, Pause, PauseAndCatchup (+12 more)

### Community 107 - "UnityStub.cs"
Cohesion: 0.07
Nodes (24): Bounds, extents, max, min, FilterMode, Bilinear, Point, Trilinear (+16 more)

### Community 108 - "Object"
Cohesion: 0.10
Nodes (5): RuntimeInitializeOnLoadMethod, MethodInfo, Object, IsDead, Resources

### Community 109 - ".BuildWindows"
Cohesion: 0.16
Nodes (7): MenuItem, RePlanetBuild, Result, Result, TimeSpan, Debug, isDebugBuild

### Community 110 - "TcpServerTransport"
Cohesion: 0.18
Nodes (5): Conn, Thread, TcpServerTransport, Port, TcpListener

### Community 111 - "SystemLanguage"
Cohesion: 0.12
Nodes (14): Application, platform, systemLanguage, RuntimePlatform, LinuxPlayer, OSXPlayer, WindowsPlayer, SystemLanguage (+6 more)

### Community 112 - "Font"
Cohesion: 0.08
Nodes (22): FontStyle, Bold, BoldAndItalic, Italic, Normal, CharacterInfo, Font, TextAlignment (+14 more)

### Community 113 - "AudioManager"
Cohesion: 0.13
Nodes (6): AudioManager, I, IntroReady, IntroTime, VoiceGain, VoiceParent

### Community 114 - "LifeCommon"
Cohesion: 0.14
Nodes (7): List, LifeCommon, BeforeView, Quality, QualityScale, SmallShadows, ViewScale

### Community 115 - "AudioClip"
Cohesion: 0.16
Nodes (8): MusicClips, AudioClip, loadState, AudioDataLoadState, Failed, Loaded, Loading, Unloaded

### Community 116 - "AudioReverbPreset"
Cohesion: 0.14
Nodes (14): AudioReverbPreset, Arena, Auditorium, Bathroom, Cave, Concerthall, Generic, Hangar (+6 more)

### Community 117 - "MonoBehaviour"
Cohesion: 0.14
Nodes (8): Floater, PostFX, I, Running, UIRoot, IEnumerator, Coroutine, MonoBehaviour

### Community 118 - "AmbientMode"
Cohesion: 0.40
Nodes (5): AmbientMode, Custom, Flat, Skybox, Trilight

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

### Community 123 - "Shader"
Cohesion: 0.19
Nodes (3): Dictionary, Shader, isSupported

### Community 124 - ".UpdateRadio"
Cohesion: 0.27
Nodes (5): Dictionary, List, NarrationLine, RadioTrack, Story

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

### Community 130 - "Input"
Cohesion: 0.17
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
Cohesion: 0.22
Nodes (7): DecoGrid, List, CameraRig, Cam, I, Post, AudioListener

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

### Community 140 - "FullScreenMode"
Cohesion: 0.22
Nodes (9): FullScreenMode, ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed, Resolution, Screen, currentResolution (+1 more)

### Community 141 - "UINav"
Cohesion: 0.09
Nodes (7): Dictionary, List, Stack, ScrollCtx, UINav, ScrollCtx, Vector2Int

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

### Community 149 - "CullMode"
Cohesion: 0.50
Nodes (4): CullMode, Back, Front, Off

### Community 150 - "InstanceBatch"
Cohesion: 0.29
Nodes (3): InstanceBatch, Count, Species

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

### Community 159 - ".SaveLife"
Cohesion: 0.40
Nodes (3): Saved, Dictionary, Saved

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
Cohesion: 0.50
Nodes (4): 9. Aufträge und Nebeninhalte, Helferroboter, Schnellreise über Lichtpunkte, Weltereignisse

### Community 164 - "FogMode"
Cohesion: 0.50
Nodes (4): FogMode, Exponential, ExponentialSquared, Linear

### Community 165 - "LightmapBakeType"
Cohesion: 0.50
Nodes (4): LightmapBakeType, Baked, Mixed, Realtime

### Community 166 - "RenderTextureReadWrite"
Cohesion: 0.50
Nodes (4): RenderTextureReadWrite, Default, Linear, sRGB

### Community 167 - "ScaleMode"
Cohesion: 0.50
Nodes (4): ScaleMode, ScaleAndCrop, ScaleToFit, StretchToFill

## Knowledge Gaps
- **996 isolated node(s):** `BassNote`, `Top`, `Note`, `Beat`, `BarLen` (+991 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1378 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Vector3` connect `Vector3` to `Input`, `UIRoot`, `GameApp`, `Wildlife`, `CameraRig`, `.Min`, `WorldView`, `WorldState`, `UINav`, `AudioManager`, `.Sin`, `Atmosphere`, `InstanceBatch`, `FxView`, `RePlanet.Core`, `RobotModel`, `.Clamp01`, `Texture2D`, `InputMap`, `.Clamp`, `GameObject`, `AudioSource`, `Mesh`, `JObj`, `TrashRenderer`, `CityLife`, `Backdrop`, `GroundMarks`, `WindLook`, `.Max`, `Camera`, `Color`, `ShipArrival`, `PostFX`, `MapCamera`, `Mathf`, `.Main`, `.LifeChecks`, `MikoGestures`, `.Get`, `QualitySettings`, `ReflectionProbe`, `Matrix4x4`, `Checks`, `UnityStub.cs`, `Object`, `.BuildWindows`, `AudioManager`, `LifeCommon`, `MonoBehaviour`?**
  _High betweenness centrality (0.167) - this node is a cross-community bridge._
- **Why does `WorldView` connect `WorldView` to `.HeightAt`, `CampaignBot`, `WorldState`, `.Sin`, `PlanetLayout`, `RePlanet.Core`, `.Clamp01`, `Texture2D`, `.Clamp`, `GameObject`, `JObj`, `TrashRenderer`, `Material`, `ParticleSystem`, `Backdrop`, `.Max`, `Color`, `ShipArrival`, `Vector3`, `.Main`, `Matrix4x4`, `Font`, `MonoBehaviour`, `Shader`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **Why does `RE:PLANET – Architektur` connect `RE:PLANET – Architektur` to `FxView`, `.L`, `GameData`, `WorldState`?**
  _High betweenness centrality (0.055) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `Vector3` (e.g. with `.EnsureWaterNormal()` and `.Animate()`) actually correct?**
  _`Vector3` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `BassNote`, `Top`, `Note` to the rest of the system?**
  _996 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Game` be split into smaller, more focused modules?**
  _Cohesion score 0.125642490005711 - nodes in this community are weakly interconnected._
- **Should `GameData` be split into smaller, more focused modules?**
  _Cohesion score 0.061419753086419754 - nodes in this community are weakly interconnected._