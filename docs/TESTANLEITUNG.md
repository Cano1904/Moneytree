# Testanleitung für echte Spieltests (Leistung und Koop)

Diese beiden Prüfungen gehen nur auf echten PCs. Bitte die Ergebnisse (Screenshots, Zahlen, Fehlermeldungen aus der
Unity-Console bzw. `Player.log`) an Claude schicken.

## 1. Leistung messen

**Vorbereitung**
- In Unity: Play drücken, oder die gebaute `RePlanet.exe` starten (aussagekräftiger, weil der Editor selbst Leistung kostet).
- Einstellungen → Grafik → **FPS anzeigen** einschalten. Qualitätsstufe notieren.
- Grafikkarte, Prozessor und Bildschirmauflösung notieren.

**Messpunkte** (jeweils ca. 20 Sekunden beobachten, niedrigsten und typischen FPS-Wert notieren)

| # | Stelle | Was passiert dort |
|---|---|---|
| 1 | Intro, Megastore (ca. 0:15–0:30) | sehr viele Objekte |
| 2 | Intro, Archen-Start (ca. 0:30–0:46) | Partikel, Licht |
| 3 | Planetenwahl im Weltall | Planeten, Nebel |
| 4 | TERRA, Wohnviertel am Tag, Blick in eine lange Straße | viele Gebäude, Hintergrund-Müll |
| 5 | TERRA nachts am Stützpunkt | Lampenlichter |
| 6 | PYRA im Sandsturm | Partikel, Nebel |
| 7 | PELAGIA am Hafen, Blick aufs Wasser | Wasser-Shader, Spiegelung |
| 8 | Schrottlieferung (Frachter landet) | Frachter, Staub |

**Wenn es ruckelt:** Qualitätsstufe eine Stufe tiefer testen und notieren, welche Stufe flüssig läuft.
Ziel: stabile 60 FPS bei 1080p auf einer Mittelklasse-Grafikkarte.

**Log-Datei der gebauten Anwendung (Windows):**
`%USERPROFILE%\AppData\LocalLow\RE PLANET Projekt\RE PLANET\Player.log`

## 2. Koop mit zwei PCs testen

**Im selben Heimnetz (am einfachsten)**
1. PC A (Host): Spiel starten → Spielstand laden → Pause → **Koop** → „Welt für Mitspieler öffnen“.
   Die angezeigte Einladung (z. B. `192.168.0.23:7777/ABC123`) notieren.
2. Windows fragt ggf. nach der Firewall: **Zugriff erlauben** (privates Netzwerk).
3. PC B (Gast): Hauptmenü → **Koop** → Einladung eintragen → Beitreten.

**Über das Internet**
- Einfachster Weg: auf beiden PCs **Tailscale** oder **ZeroTier** installieren (kostenlos), dann wie im Heimnetz mit
  der dort angezeigten Adresse beitreten.
- Oder am Router des Hosts den **TCP-Port 7777** an den Host-PC weiterleiten und die öffentliche IP-Adresse verwenden.

**Bitte prüfen und notieren**

| # | Test | Erwartung |
|---|---|---|
| 1 | Beitritt | Gast sieht die Welt des Hosts, beide sehen sich gegenseitig |
| 2 | Gast sieht das Intro | beim ersten Beitritt läuft das Intro, danach ins Spiel |
| 3 | Beide greifen gleichzeitig dasselbe Objekt | nur einer bekommt es |
| 4 | Verkaufen / Credits | gemeinsame Kasse, beide sehen denselben Stand |
| 5 | Gast will teures Upgrade (≥ 800) kaufen | abgelehnt, außer Host schaltet Vertrauensmodus ein |
| 6 | Gast trennt die Verbindung und tritt neu bei | gleicher Roboter, Behälter-Inhalt noch da |
| 7 | Host beendet das Spiel | Gast bekommt Meldung „Host hat die Sitzung beendet“, Host hat gespeichert |
| 8 | Nacht: beide schlafen im Unterschlupf | Morgen erst, wenn beide schlafen |
| 9 | Schrottlieferung | beide sehen den Frachter landen |
| 10 | Ping (Koop-Bildschirm) | Wert notieren |

Fehler bitte mit Screenshot und dem Text aus der Console bzw. `Player.log` schicken.
