# RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte

Dieses Dokument hält den Arbeitsstand fest (Masterprompt, Abschnitt 1: „Bewahre bei langen Aufgaben Fortschritt,
Entscheidungen und nächste Schritte schriftlich im Projekt“). Geprüfte Ergebnisse stehen in `docs/TESTBERICHT.md`.

## Umsetzung in 10 Punkten
1. **Engine:** Unity (auf Wunsch des Auftraggebers) mit C#, Built-in Render Pipeline und klassischem Input Manager.
   Unity ist in der Entwicklungsumgebung nicht installierbar (Netzwerkrichtlinie blockiert die Unity-Server, keine Lizenz).
   Deshalb: vollständiges Unity-Projekt unter `RePlanet/`, die Windows-Anwendung erzeugt der Nutzer im Editor per Menü
   „RE:PLANET/Windows-Build erstellen (64 Bit)“.
2. **Eine Simulation für alles:** `Core/` ist reines C# ohne UnityEngine (serverautoritativ). Solo, Koop-Host und
   dedizierter Server nutzen denselben Code; dadurch ist die Spiellogik mit .NET ohne Unity testbar.
3. **Datengetrieben:** Materialien, Müllarten, Werkzeuge, Fahrzeuge, Gebäude, Planeten, Projekte, Aufträge, Fundstücke,
   Kosmetik als Tabellen in `Core/Data/GameData.cs`.
4. **Vier Planeten:** TERRA, PYRA und PELAGIA sind **ab Spielbeginn frei wählbar** und bereisbar; NIVALIS ist das
   Finale (Sprungantrieb + die drei letzten Großprojekte). Jeder Planet hat eigene Geometrie, Mechanik
   (Reparatur / Sandstürme mit wandernden Dünen / Schwimmen und Tauchen / Eis auftauen), Stimmung, Himmel und Musik.
5. **Tag/Nacht und Wetter:** Tageszyklus, Wind und Stürme (mit Vorwarnung) auf allen Planeten. Nachts oder im Sturm
   braucht MIKO einen Unterschlupf; Schlafen überspringt Nacht/Sturm. Bei leerem Akku Notabschaltung und Abschleppen
   zum Stützpunkt (nur Zeitverlust). Notunterschlüpfe sind baubar.
6. **Online-Koop 1–4:** TCP mit Sitzungscode und Einladung `IP:Port/CODE`, später Beitritt, Wiederverbinden per Token,
   Host-Verlassen mit Sicherung, Gastrechte + Vertrauensmodus, Idempotenz über Anfrage-IDs, dedizierter Server.
7. **Speichern:** versioniert, Prüfsumme, Backup-Rotation, Slots (auto + 3), Export/Import, Migration; gespeichert werden
   nur Abweichungen vom deterministisch erzeugten Ausgangszustand.
8. **Darstellung:** alles prozedural zur Laufzeit (keine Prefabs, keine Fremd-Assets): eigener Himmels-Shader
   (Wolkenmassen, große Himmelskörper mit Lichtsaum, Sterne, Polarlicht), Müll per GPU-Instancing, „Stadt erwacht“,
   nachwachsende Vegetation, Wellenwasser, animierter MIKO mit sichtbaren Upgrades.
9. **Erzählung:** Intro als Echtzeit-Zwischensequenz mit eigenen Figuren und Namen
   (Konzern KONSUMA, Arche HORIZONT, Programm ZWEITE CHANCE, Roboter MIKO); Abspann mit Rückblick aus echten Spielwerten.
10. **Audio:** Musik, Wind, Ambience und Effekte werden prozedural synthetisiert (keine Lizenzfragen); Musik je Planet
    mit eigener Stimmung, Schichten wachsen mit dem Wiederherstellungsgrad.

## Entscheidungen
- Werkzeug-Upgrades sind Team-Forschung (gelten für alle Roboter der Sitzung), Kosmetik ist persönlich.
- Credits sind eine gemeinsame Kasse; Lager/Materialien gehören zum jeweiligen Planeten-Stützpunkt.
- Unterschlupf-Mechanik in der mittleren Härte „Notabschaltung“ (Entscheidung des Auftraggebers): kein Verlust von
  Gegenständen, nur Zeitverlust.
- Drei Startplaneten frei wählbar (Entscheidung des Auftraggebers), NIVALIS als gemeinsames Finale.
- Der Himmel orientiert sich an den gelieferten Referenzbildern (farbige Wolkenmassen, große Planeten/Monde).

## Prüfwerkzeuge ohne Unity
- `Tools/CompileCheck/{Core,Runtime,Editor}`: kompiliert die Unity-Skripte mit dotnet gegen UnityEngine-/UnityEditor-
  Referenz-DLLs (`Tools/CompileCheck/fetch-unity-refs.sh`).
- `Tools/ShaderCheck/check_shaders.py`: Syntax-/Typprüfung der eigenen Shader mit glslang (HLSL). Ersetzt nicht den
  Unity-Shader-Compiler.
- `Tools/SkyPreview/sky_preview.py`: rechnet die Himmels-Shader-Mathematik in NumPy nach (Vorschaubilder, keine Spielszenen).
- `Tools/RenderHarness`: baut die Welt aller Planeten mit dem echten Render-Code gegen einen UnityEngine-Ersatz,
  zählt Draws/Ecken/Instanzen und prüft die Dreieckswicklung (`cd Tools/RenderHarness && dotnet run`).
- `Tests/`: .NET-Tests für Wirtschaft, Speichern, Netzwerk, Wetter und einen Kampagnen-Bot.

## Stand
Alle Bereiche umgesetzt (Spiellogik, Koop, Speichern, Darstellung, Oberfläche, Audio, Editor-Setup, Server, Doku,
Konzeptkunst). Prüfungen: 40/40 .NET-Tests, Kompilierprüfungen Core/Runtime/Editor ohne Fehler und Warnungen,
Shader-Prüfung OK, Server-Rauchtest 17/17.

## Zusatzsysteme (Lieferlimit, Sturm abwarten, Helferroboter, Erfolge, Schnellreise, Weltereignisse)
- **Lieferungen:** Gebühr je Planet (15/30/20/45 Credits ≈ 9 % des sortierten Werts) und 60 s Abklingzeit je Planet.
- **Sturm/Schlafen:** Schlafen überspringt nur die Nacht. Stürme werden im Unterschlupf abgewartet; warten alle, läuft die
  Zeit ×4 (Sturm bleibt, dauert seine volle Zeit).
- **Helferroboter:** 3 defekte Helfer je Planet, reparierbar (Credits + Material), sammeln langsam im Umkreis von 14 m und
  schicken die Ladung per Rohrpost-Kapsel ins Lager; mitnehmen und neu absetzen möglich.
- **Erfolge:** 20 Erfolge aus der Statistik, je ein neues kosmetisches Teil als Belohnung; Reiter „Erfolge“, Hinweis beim Erreichen.
- **Schnellreise:** zwischen leuchtenden Lichtpunkten und dem Stützpunkt über die Karte (2D/3D); nicht im Sturm, nicht im
  Fahrzeug, höchstens ein Viertel Ladung; kostet 4 + 0,04 Energie je Meter.
- **Weltereignisse:** Meteoritenschauer, Versorgungsabwurf, freigelegte Deponie nach Stürmen – mit Hinweis, Kartensymbol und Effekten.
- **Balancing (Kampagnen-Bot, Spielzeit):** vorher 7,41 h (Bot) / 14,39 h (Mensch-Modell); nachher 9,83 h / 17,24 h.
  Davon Stürme abwarten 95 bzw. 162 min Spielzeit, die dank Zeitraffer nur ≈ 24 bzw. 41 min Echtzeit kosten →
  geschätzte Echtzeit 8,64 h / 15,21 h. Lieferungen: 151 → 126 (Bot), Abklingzeit-Wartezeit 38 min.
- Nicht in Unity getestet: Darstellung (Helfermodelle, Leuchtspuren, Kapseln), Oberfläche (Reiter, Schnellreise-Liste),
  Zeitraffer-Gefühl beim Abwarten, Koop mit mehreren Rechnern.

## Erweiterungen (Radio, Stadtklänge, Controller-Bauansicht, Englisch, Erzähler, Leistungsanzeige)
- **Radio:** `Core/Sim/Story.cs` (Stücke, Freischaltregeln), `Runtime/Audio/AudioRadio.cs`, `Runtime/UI/RadioUI.cs`; Kennungen in
  `Core/Audio/SynthCity.cs`. Freischaltungen im Speicherteil `story`, alte Stände werden abgeleitet (Test).
- **Stadtklänge:** `Runtime/Audio/AudioCity.cs` + Synth-Schichten `city_*` (Loops, nahtlos, Audio-Probe ohne NaN).
- **Controller in der Bauansicht:** Cursor per Stick, alle Aktionen am Pad, Kategorien (LT/RT bzw. 1–6), Tastensymbole.
- **Englisch:** vollständige Tabelle (`Core/Loc/LocEn*.cs`), Vorlagen für Server-Meldungen, Datentabellen übersetzbar, Test auf
  Vollständigkeit und Platzhalter.
- **Erzähler im Spiel:** 21 Zeilen (`game_01 … game_21`), Texte in `docs/SPRECHERTEXT_ELEVENLABS.md` Abschnitt 5 und
  `Tools/Voice/generate_elevenlabs.py` (`--game`). Aufnahmen fehlen noch → vorerst Untertitel.
- **Leistungsanzeige:** `Runtime/UI/PerfOverlay.cs` (F3, Einstellung Grafik › Leistungsanzeige).
- Nicht in Unity getestet: Klangbild von Radio/Stadtklängen, Controller-Bedienung der Bauansicht, Textlängen auf Englisch,
  ProfilerRecorder-Zähler im Release-Build (zeigen sonst „–“).

## Grenzen (Stand dieser Umgebung)
- Kein Unity-Editor, kein Unity-Laufzeittest, keine echten Spiel-Screenshots, keine gebaute .exe.
- Shader wurden nicht mit Unitys Compiler übersetzt (nur glslang-Prüfung).

## Nächste Schritte
- Projekt in Unity öffnen (Unity 6 LTS oder 2022.3 LTS), Konsole auf Fehler prüfen, Play-Test, Windows-Build erstellen.
- Die von Unity beim ersten Öffnen erzeugten `.meta`-Dateien und Resources-Materialien committen.
- Erledigt (siehe unten): Schrottlieferungen haben Gebühr + Abklingzeit; Stürme lassen sich nicht mehr verschlafen.
- Echte Screenshots und Spieltests (Controller, Koop mit mehreren Rechnern, 60-FPS-Messung auf Referenzhardware).
