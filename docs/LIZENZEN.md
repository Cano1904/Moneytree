# RE:PLANET – Herkunft der Inhalte, Lizenzen und Markenhinweise

Kurzfassung: **Alle Grafiken, Sounds und Musik von RE:PLANET werden im Projekt selbst erzeugt.** Es werden keine
Fremd-Assets, Asset-Store-Pakete, Schriftdateien, Sample-Bibliotheken oder Bilder Dritter verwendet.
Dieses Dokument ist eine sachliche Bestandsaufnahme und keine Rechtsberatung.

## 1. Was im Projekt steckt und woher es kommt

| Inhalt | Herkunft |
| --- | --- |
| 3D-Modelle (MIKO, Fahrzeuge, Gebäude, Müll, Gelände, Pflanzen) | zur Laufzeit per Code erzeugt (`Runtime/Render/MeshKit.cs`, `RobotModel.cs`, `WorldView*.cs`, `ActorsView.cs`, `FloraRenderer.cs`, `Core/World/*`) |
| Texturen (Gelände, Wasser-Normalmap, Aufkleber) | zur Laufzeit per Code berechnet; die kleine Normalmap `RP_WaterNormal.png` erzeugt das Editor-Setup ebenfalls per Code |
| Himmel | eigener Shader `Resources/RePlanetSky.shader` |
| Materialien | Unitys eingebaute Shader (Standard, Particles/Standard Unlit, Unlit/*, Sprites/Default, Skybox/Procedural) mit vom Setup erzeugten Material-Vorlagen |
| Soundeffekte, Umgebungsgeräusche, Musik, Intro-Score | prozedurale Klangsynthese in C# (`Core/Audio/Synth.cs`) |
| Oberfläche | Unity IMGUI mit Unitys Standardschrift (Teil der Engine) |
| Texte, Geschichte, Namen | selbst geschrieben |
| Konzeptkunst in `docs/concept/` | handgeschriebene SVG-Dateien, als „KONZEPTKUNST – keine Spielszene“ beschriftet |

## 2. Unity

- RE:PLANET ist ein Unity-Projekt. **Die Unity-Engine ist nicht Teil dieses Repositorys**; sie wird vom Nutzer installiert.
- Für Nutzung, Builds und eine eventuelle Veröffentlichung gelten die **Unity-Nutzungsbedingungen und die Lizenz des
  jeweiligen Nutzers** (z. B. Unity Personal oder Pro inklusive der dort geregelten Umsatz- bzw. Förderungsgrenzen und
  Vorgaben zum Startbildschirm). Diese Bedingungen bitte vor einer Veröffentlichung selbst prüfen.
- Ein Windows-Build enthält Laufzeitbestandteile von Unity (z. B. `UnityPlayer.dll`, Mono); deren Weitergabe ist durch die
  Unity-Lizenz geregelt.
- `Tools/CompileCheck/fetch-unity-refs.sh` lädt öffentlich über NuGet verfügbare **Referenz-Assemblies** (nur
  Schnittstellen) herunter, um die Skripte ohne installierten Editor zu kompilieren. Diese Dateien werden nicht ins
  Repository übernommen und nicht mit dem Spiel ausgeliefert.

## 3. Weitere Werkzeuge

- **.NET 8 SDK** (Tests, dedizierter Server, Generatoren) – vom Nutzer installiert, MIT-lizenzierte Laufzeit von Microsoft.
  Der dedizierte Server nutzt ausschließlich die .NET-Standardbibliothek; es gibt keine NuGet-Abhängigkeiten.
- **Python 3** (Himmelsvorschau `Tools/SkyPreview`) – nur Entwicklungswerkzeug, nicht Teil des Spiels.

## 4. Namen und Marken

### „RE:PLANET“

Der Titel wurde **nicht** auf Marken-, Firmen- oder Domainkonflikte geprüft. Vor einer kommerziellen Veröffentlichung
(Store-Seite, Verkauf, Vermarktung) bitte eine Namensrecherche durchführen, z. B. in den Registern des DPMA, des EUIPO
(„eSearch plus“/TMview) und der WIPO sowie in den großen Spiele-Stores, und bei Bedarf rechtlich beraten lassen.
Gleiches gilt für die Namen MIKO, KONSUMA, HORIZONT und „Programm ZWEITE CHANCE“.

### WALL·E

**WALL·E ist ein Film und eine Marke von Disney/Pixar.** RE:PLANET ist davon lediglich in der Grundstimmung
inspiriert (vermüllte Erde, Menschen verlassen den Planeten, ein einzelner Roboter räumt weiter auf und findet einen
Keimling). Übernommen wurden **keine** Figuren, Namen, Designs, Dialoge, Musik, Bilder oder Filmausschnitte:

- Der Roboter heißt **MIKO** und hat ein eigenes Design: türkise Hülle, orange Akzente, breites leuchtendes Visier,
  drei Räder, faltbarer Arm und sichtbarer Rückenbehälter (keine Kettenlaufwerke, keine Fernglas-Augen, kein Würfelkörper).
- Der Konzern heißt **KONSUMA**, die Arche **HORIZONT**, das Rückkehrprogramm **ZWEITE CHANCE**.
- Es gibt keine Nachbildung von Filmszenen, Filmfiguren oder bekannten Filmmotiven über die allgemeine Handlungsidee hinaus.

In Beschreibungen des Spiels sollte WALL·E höchstens als Inspiration erwähnt und nicht zur Vermarktung genutzt werden
(keine Formulierungen wie „das WALL·E-Spiel“, keine Filmbilder).

### Weitere Marken

Erwähnte Produkt- und Firmennamen (Windows, Xbox, Unity, Tailscale, ZeroTier, Disney, Pixar) sind Marken ihrer
jeweiligen Inhaber und werden nur beschreibend genannt.
