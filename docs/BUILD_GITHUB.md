# Windows-Build automatisch auf GitHub

Die Datei `.github/workflows/replanet.yml` hat zwei Teile:

1. **Prüfen** – läuft bei jedem Push automatisch und braucht keine Lizenz: .NET-Tests, Kompilierprüfung der
   Unity-Skripte gegen Unity-Referenzen, Shader-Syntaxprüfung.
2. **Windows-Build** – baut mit Unity (über [GameCI](https://game.ci)) die fertige `RePlanet.exe` und stellt sie als
   ZIP zum Herunterladen bereit. Startet nur, wenn du es auslöst (oder bei einem Versions-Tag `v…`).

## Einmalige Einrichtung (im GitHub-Repository, nicht im Chat)

GitHub → Repository → **Settings → Secrets and variables → Actions**

| Art | Name | Inhalt |
|---|---|---|
| Secret | `UNITY_EMAIL` | E-Mail deines Unity-Kontos |
| Secret | `UNITY_PASSWORD` | Passwort deines Unity-Kontos |
| Secret | `UNITY_LICENSE` | Inhalt deiner Unity-Lizenzdatei (`.ulf`, Personal-Lizenz) – *oder* stattdessen `UNITY_SERIAL` bei Pro/Plus |
| Variable | `UNITY_VERSION` | deine Unity-Version, z. B. `6000.0.58f2` (steht im Unity Hub bzw. unten im Hauptmenü des Spiels) |

**Lizenzdatei (.ulf) für die kostenlose Personal-Lizenz:** Nach dem Aktivieren im Unity Hub liegt sie unter
Windows in `C:\ProgramData\Unity\Unity_lic.ulf`. Den kompletten Dateiinhalt als Secret `UNITY_LICENSE` einfügen.
Hilfe von GameCI: <https://game.ci/docs/github/activation>

**Hinweis Unity-Version:** GameCI braucht ein Docker-Image für genau diese Version. Gibt es für eine sehr neue Version
noch keins, eine verfügbare LTS-Version eintragen (Liste: <https://game.ci/docs/docker/versions>) und das Projekt
lokal einmal mit dieser Version öffnen.

## Build starten und herunterladen

1. GitHub → **Actions** → „RE:PLANET“ → **Run workflow** → Branch wählen → **Run**.
2. Nach dem Durchlauf (erster Build 20–40 Minuten, danach schneller dank Cache) unten auf der Seite unter
   **Artifacts** → `RePlanet-Windows` herunterladen, entpacken, `RePlanet.exe` starten.

**Kosten:** Bei öffentlichen Repositories sind GitHub Actions kostenlos; bei privaten gibt es ein monatliches
Freikontingent an Minuten.
