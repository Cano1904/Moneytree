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
9. **Erzählung:** Intro als Echtzeit-Zwischensequenz nach der Handlung von WALL·E mit eigenen Figuren und Namen
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

## Belebte Welt (Tiere, Spuren, Wind, Stadt erwacht 2.0, MIKOs Mimik)
- Neue Darstellungsbausteine ohne Änderung der Spielregeln: `Wildlife`/`AnimalMeshes`, `GroundMarks`, `WindLook`
  (+ Wolkenschatten im PostFX-Shader, Wiegen in `FloraRenderer`), `CityLife`, `MikoFace`/`MikoGestures`, gemeinsame
  Hilfen `LifeCommon`, Zugänge `WorldViewLife`. Kleine Eingriffe: `RobotModel` (partial + drei Aufrufe),
  `Atmosphere` (Wolkendecke/Sturmanteil/Sonnenhöhe öffentlich), `ActorsView.VehiclePose`, `GameApp.OnEmote`
  (Roboterlaut der Mitspieler wird jetzt dargestellt).
- Synchronität: alles aus repliziertem Zustand (Wiederherstellung, Wetter, Sturmzeit, Positionen, Akku, Schutz) und
  Serverereignissen; Heimatplätze/Straßenabschnitte/Fahnen deterministisch aus dem Layout. Bewegungen der Tiere und
  Autos laufen je Client und sind nur ähnlich, nicht gleich.
- Leistung: Instancing je Art/Teil (Tiere 7–17 Aufrufe, Stadt ≈ 35, Spuren ≤ 13), Zeichnen/Simulieren nur in
  Kameranähe, Mengen nach Qualitätsstufe (Tiere ×0,35…1, Spuren 300…2000, Straßenbahnen und Lichthöfe ab „Mittel“).
- Prüfumgebung: neue Prüfung `ChecksLife` je Planet (vollständige Wiederherstellung simuliert: Tierzahlen, keine Tiere in
  Wänden/unter dem Gelände/außerhalb des Wassers, Flucht, Stadtfahrzeuge 20 s lang nie in Gebäuden oder neben der
  Straße, Spuren, Pfützen, Wind, Gesten, Nacht, Sturm, Vorher-Ansicht).
- Offen/ungetestet in Unity: Aussehen und Größe der Tiere, Wolkenschatten-Stärke, Spurtransparenz auf dem
  Gelände-Shader (Z-Kämpfe?), Pfützen-Glanz, Fahrverhalten an Kreuzungen, echte Bildrate.

## Grenzen (Stand dieser Umgebung)
- Kein Unity-Editor, kein Unity-Laufzeittest, keine echten Spiel-Screenshots, keine gebaute .exe.
- Shader wurden nicht mit Unitys Compiler übersetzt (nur glslang-Prüfung).

## Nächste Schritte
- Projekt in Unity öffnen (Unity 6 LTS oder 2022.3 LTS), Konsole auf Fehler prüfen, Play-Test, Windows-Build erstellen.
- Die von Unity beim ersten Öffnen erzeugten `.meta`-Dateien und Resources-Materialien committen.
- Offene Designfragen: Schrottlieferungen kostenlos/unbegrenzt; Schlafen beendet Stürme auch tagsüber (siehe `docs/TESTBERICHT.md`).
- Echte Screenshots und Spieltests (Controller, Koop mit mehreren Rechnern, 60-FPS-Messung auf Referenzhardware).
