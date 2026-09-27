# RE:PLANET – Fortschritt, Entscheidungen, nächste Schritte

## Umsetzung in 10 Punkten
1. **Engine-Entscheidung:** Unreal Engine 5 ist in der verfügbaren Build-Umgebung (Linux-Container ohne GPU, ohne Epic-Launcher/Editor) nicht ausführbar. Alternative: **Three.js (WebGL 2) + Node.js**. Begründung: echtes 3D, lauffähig und testbar in dieser Umgebung, als PWA installierbar, gleiche Spiellogik in Browser (Solo) und Server (Koop).
2. **Eine Simulation, zwei Hosts:** `shared/` enthält die komplette, deterministische Spiellogik (Müll, Werkzeuge, Wirtschaft, Bauen, Missionen, Speichern). Solo läuft sie im Browser, im Koop serverautoritativ auf dem Node-Server.
3. **Datengetrieben:** Müllarten, Materialien, Preise, Werkzeuge, Fahrzeuge, Gebäude, Planeten und Aufträge liegen als Tabellen in `shared/data/`.
4. **Vier Planeten** (Terra, Pyra, Pelagia, Nivalis) mit je drei Bereichen, eigener Geometrie, eigenen Mechaniken (Reparatur / Sandsturm / Schwimmen + Tauchen / Eis auftauen), Sounds und Musikskalen.
5. **Kernschleife:** entdecken → sammeln (Greifarm, Sauger, Magnet, Schneider, Kran) → sortieren → pressen → verkaufen/verwerten → Upgrades → Großprojekte → Stadt erwacht.
6. **Online-Koop 1–4 Spieler:** WebSocket-Sitzungen mit Sitzungscode, Lobby, Einladungslink, spätem Beitritt, Reconnect-Token, Host-Verlassen mit Sicherung, Rechten für teure Käufe/Abriss, Idempotenz gegen Doppelverarbeitung.
7. **Speichern:** versioniert, Prüfsumme, Backup-Rotation, manuelle Slots, Export/Import, entfernte Objekte als Bitset gegenüber deterministischem Ausgangszustand.
8. **Erzählung:** Intro als Echtzeit-Zwischensequenz, angelehnt an die Handlung von WALL·E (vermüllte Erde, Konzern, Menschen fliehen auf Archen, ein einzelner Roboter arbeitet weiter, findet einen Keimling). Eigene Figuren und Namen, keine Filmfiguren.
9. **Zugänglichkeit/Komfort:** Tastenbelegung, Controller, getrennte Lautstärken, Untertitel, UI-Skalierung, Kamerawackeln aus, Halten/Umschalten, Form+Symbol je Material, Fotomodus mit Vorher/Nachher und Hochformat.
10. **Nachweis:** automatisierte Tests (Wirtschaft, Speichern, Netzwerk, komplette Solo-Kampagne per Bot), Playwright-Durchlauf mit echten Screenshots, Testbericht mit bestanden/fehlgeschlagen/nicht getestet.

## Entscheidungen
- Keine Build-Pipeline nötig: native ES-Module + Import-Map; `three` wird vom Server aus `node_modules` ausgeliefert.
- Audio vollständig prozedural mit WebAudio erzeugt (keine Fremd-Assets, keine Lizenzfragen).
- Grafik aus prozeduraler Geometrie (keine Fremd-Assets).
- Werkzeug-Upgrades sind Team-Forschung (gelten für alle Roboter der Sitzung), Kosmetik ist persönlich.
- Credits sind eine gemeinsame Kasse; Lager/Materialien gehören zum jeweiligen Planeten-Stützpunkt.

## Stand
Siehe `docs/TESTBERICHT.md` für den geprüften Stand.
