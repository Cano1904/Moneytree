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
| Hauptmenü-Schriften | `Resources/Fonts/`: „RePlanet Logo“ (aus **Orbitron** abgeleitet, feste Strichstärke 800, wegen des reservierten Schriftnamens umbenannt) und **Exo 2** (Medium, Bold) – beide SIL Open Font License 1.1, Lizenztexte liegen bei (`OFL-RePlanetLogo.txt`, `OFL-Exo2.txt`) |
| Erzählerstimme (`Resources/Voice/`) | mit ElevenLabs erzeugt (Stimme „Helmut“ aus der Voice Library). Nutzungsrechte richten sich nach dem ElevenLabs-Tarif: für eine kommerzielle Veröffentlichung ist ein bezahlter Tarif nötig; Bedingungen vorher prüfen |
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

### Figuren, Namen und Designs

Alle Figuren, Namen, Designs, Texte, Bilder und Musik von RE:PLANET sind eigene Schöpfungen des Projekts:

- Der Roboter **MIKO**: türkise Hülle, orange Akzente, breites leuchtendes Visier, drei Räder, faltbarer Arm und
  sichtbarer Rückenbehälter.
- Der Konzern **KONSUMA**, die Arche **HORIZONT**, das Rückkehrprogramm **ZWEITE CHANCE**.

### Weitere Marken

Erwähnte Produkt- und Firmennamen (Windows, Xbox, Unity, Tailscale, ZeroTier) sind Marken ihrer
jeweiligen Inhaber und werden nur beschreibend genannt.
