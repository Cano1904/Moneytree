# RE:PLANET – Testbericht

## Automatisierte Tests und Balancing

Stand: Branch mit den Commits „Speichertests …“ bis „Balancing: erste Upgrades teurer …“ (September 2026).
Ausgeführt mit `cd Tests && dotnet run -c Release` (.NET 8, Linux-Container). Die Tests laufen gegen die
reine C#-Spiellogik (`RePlanet.Core`) **ohne Unity**. Darstellung, Eingabe, Audio, Shader und das Verhalten
im Unity-Editor/Player wurden **nicht** getestet – dafür steht in dieser Umgebung kein Unity zur Verfügung.
Zusätzlich kompiliert `Tools/CompileCheck/Runtime` die Unity-Schicht gegen den geänderten Core (0 Fehler).

### Ergebnis des Testlaufs

`Gesamt: 39 bestanden, 0 fehlgeschlagen` (Release, Gesamtdauer ≈ 4 s; der längste Test ist die komplette Kampagne mit ≈ 1 s).

| Bereich | Prüfung | Ergebnis |
|---|---|---|
| Wirtschaft (`EconomyTests`, 10) | Sammeln entfernt Objekt, keine Duplikation; voller Behälter/falsches Werkzeug/fehlendes Geld mit verständlicher Meldung; Verkauf genau einmal; nie negatives Guthaben; keine Kauf-/Verkauf- oder Bau-/Abriss-Gewinnschleife; Upgrades wirken; Magnetwelle begrenzt; Bauen prüft Kollision/Material; leerer Akku keine Sackgasse; Projektmaterial beschaffbar | BESTANDEN |
| Speichern (`SaveTests`, 8) | Rundlauf Encode→Decode ergibt identisches JSON (auch im 2. Durchlauf) | BESTANDEN |
| | Falsche Prüfsumme, veränderte Nutzdaten, abgeschnittene/leere Datei, neuere Version → verständlicher Fehler | BESTANDEN |
| | `SaveStore` (Temp-Ordner): Backup-Rotation, beschädigter Hauptstand → Backup wird geladen (mit Hinweis), gutes Backup wird nicht durch kaputten Stand ersetzt, beide kaputt → Fehlermeldung | BESTANDEN |
| | Migration v1 (`money`, ohne `stats`/`is`/`misc`) → aktuelles Format; `SaveCodec.Migrate` v2→v3 | BESTANDEN |
| | Entfernte statische und dynamische Objekte bleiben nach Speichern + Laden + Reise (`travel`) TERRA↔PYRA entfernt | BESTANDEN |
| | Erledigte Aufträge/Recyclingaufträge werden nach (mehrfachem) Laden nicht erneut vergütet | BESTANDEN |
| | Neustart über `SaveStore` + `HostServer` + lokalen `GameClient`: gleicher Planet, Position, Credits, Tag-Versatz, Tageszeit beim Client | BESTANDEN (nach Korrektur, s. u.) |
| | Gespeicherte Position hinter geschlossenem Tor / ungültig → Start am Stützpunkt | BESTANDEN |
| Netzwerk (`NetTests`, 10) | 4 Clients per echtem TCP (dedizierter `SessionHub`, Port 0), 5. wird mit „voll“ abgelehnt; falscher Code; Nachrücker nach Verlassen | BESTANDEN |
| | Zwei Clients (lokal + TCP) greifen gleichzeitig dasselbe Objekt (6×): genau einer gewinnt, Objekt genau einmal im Behälter, Replikate stimmen | BESTANDEN |
| | Doppelte Anfrage-ID (sofort und später wiederholt) → einmal vergütet, gespeichertes Ergebnis zurück; gleiche ID eines anderen Spielers unabhängig | BESTANDEN |
| | Gast: teure Käufe (≥ 800), Bau teurer Anlagen, Schiff, Abriss, Reisen abgelehnt; `trust` nur durch Host; mit Vertrauen Käufe/Abriss erlaubt, Reisen weiter nur Host | BESTANDEN |
| | Später Beitritt: Snapshot enthält entfernte Objekte, Lieferung, Lager, Credits, Behälter; Client-Spielzeit läuft danach mit | BESTANDEN (nach Korrektur, s. u.) |
| | Wiederverbinden nach Verbindungsabbruch mit Token: gleicher Spieler, Behälter und Position erhalten; ohne Token abgelehnt; Übernahme mit Token trennt alte Verbindung | BESTANDEN |
| | Host verlässt Sitzung: Host erhält `save` vor `ended`, Gäste (TCP + lokal) `hostleft`, Sitzung entfernt | BESTANDEN |
| | Dedizierter Server: Host-Abbruch → Hinweis + Sicherung, Rückkehr innerhalb der Frist, sonst Ende mit `hostleft` + Abschlusssicherung | BESTANDEN |
| | Manipulierte Nachrichten (unbekannte Aktion, gefälschter Patch/Welcome/Hello, negative Mengen, falsche Kostenfelder, kaputtes JSON, `reqsave`/`trust` als Gast) ändern nichts; Teleport, NaN, Unendlich, außerhalb der Welt → `corr` | BESTANDEN |
| | Geschwindigkeitsbetrug mit gefälschtem `dt` wird korrigiert; ehrliche, gebündelt ankommende Pakete nicht | BESTANDEN (nach Korrektur, s. u.) |
| Wetter (`WeatherTests`, 8) | Nacht nach `(0,8−0,3)·Tageslänge` auf allen Planeten, Zyklus, Dunkelheit | BESTANDEN |
| | Ohne Unterschlupf sinkt nachts die Energie (≈ 0,55/s), am Stützpunkt/im Fahrzeug nicht | BESTANDEN |
| | Akku leer nachts → Notabschaltung, Zähler, keine Bewegung/kein Schlaf, nach 6 s Abschleppen zum Ladeplatz mit 40 % Energie, solo ist danach Morgen, nichts verloren; im Koop läuft die Nacht weiter | BESTANDEN |
| | `sleep` tagsüber, draußen, im Fahrzeug abgelehnt; im Unterschlupf nachts → Morgen, voller Akku, Speicherpunkt | BESTANDEN |
| | Koop: Morgen erst, wenn alle (Online-)Spieler schlafen; Verlassen des Unterschlupfs weckt | BESTANDEN |
| | Notunterschlupf: nicht am Stützpunkt, 80 Credits, Mindestabstand 12 m (auch zu vorhandenen), max. 8, nicht im Wasser, schlafen darin, gespeichert | BESTANDEN |
| | Sturm PYRA: Warnung 30 s vorher, Sturm, Ende nach Dauer, Dünen-Set wechselt (auch `MotorEnv`), Schlafen überspringt Sturm; TERRA-Stürme ohne Dünenwechsel | BESTANDEN |
| Regression (`LogicTests`, 2) | Schrottlieferungen sind gemischt | BESTANDEN (nach Korrektur) |
| | Gepresste Ballen zählen für Projekte/Bauten | BESTANDEN (nach Korrektur) |
| Kampagne (`CampaignTests`, 1) | Bot spielt die ganze Kampagne TERRA → PYRA → PELAGIA → Sprungantrieb → NIVALIS bis `WorldState.CampaignDone`; alle 12 Projekte fertig, Reihenfolge, Kampagnen-Kosmetik; Echtzeitlimit 120 s | BESTANDEN |

**Warum dauert der Kampagnentest nur ≈ 1 s?** Er simuliert tatsächlich jede Spielsekunde: ~7,2 h Spielzeit =
~103 000 Aufrufe von `Game.Tick(0,25 s)` (je ≈ 3 µs), ~16 600 Aktionen über `Game.Apply` und jede Bewegung über
`Game.Move` mit der realen Höchstgeschwindigkeit. Es gibt keine Darstellung und kein Netzwerk. Der Test prüft das
ausdrücklich (Schrittzahl ≥ 95 % von Spielzeit/0,25 s, > 3 000 gesammelte Objekte, ≥ 3 Kranbergungen).

### Durch Tests gefundene und behobene Fehler in der Spiellogik

1. **Clients hatten immer Spielzeit 0** (`Game.PosPacket`, `GameClient.ApplyPos`): Die Serverzeit stand im
   Positionspaket unter `"t"`, das die Sitzung mit dem Nachrichtentyp `"pos"` überschreibt. Folge: Tag/Nacht,
   Wachstum und Interpolation liefen beim Client (auch solo) nicht. Jetzt unter `"st"`.
2. **Neustart setzte den Spieler immer an den Stützpunkt** (`Game.Join`): Gespeicherte Position wurde verworfen.
   Bekannte Spieler behalten jetzt ihre Position, sofern gültig und über offene Tore erreichbar (auch beim Wiederverbinden).
3. **Geschwindigkeitsbetrug über gefälschtes `dt`** (`Session`, Eingabe `in`): Das Client-`dt` wurde ungeprüft
   als Zeitbasis genommen (`dt: 1` → ~15 m je Paket, gemessen 112 m in 2 s). Jetzt höchstens die am Server
   vergangene Zeit (Guthaben bis 1 s für Netzschwankungen).
4. **Lieferungen waren sortenrein** (`ActDelivery`): Der Sortenindex hing an `S.NextDyn`, das beim Erzeugen
   hochzählt – jede TERRA-Lieferung bestand aus 14 gleichen Teilen (z. B. 14 Fernseher à 5 kg, mit dem Start-Greifarm
   nicht hebbar). Da eine neue Lieferung erst nach dem Abräumen möglich ist, führte das im Bot zu einer Sackgasse.
5. **Die Ballenpresse machte Projektmaterial unbrauchbar** (`PlanetState.Available`, `Game.ConsumeMaterials`):
   Ballen zählten nicht als verfügbar; gekauftes Metall war bis zum Projektplatz schon wieder gepresst. Ballen zählen
   jetzt mit und werden bei Bedarf aufgebrochen.

### Kampagnen-Bot (`dotnet run -c Release -- balance`)

Der frühere Bot brach in dieser Umgebung nach ~4 Spielminuten mit „Bewegung abgelehnt“ ab (auf dem Weg zum
Ladeplatz; er kannte Nacht, Sturm und Notabschaltung nicht – die genaue Ursache wurde nicht weiter untersucht). Überarbeitet: Unterschlupf aufsuchen oder Notunterschlupf bauen
und schlafen, Abschleppen abwarten, neue Planetenreihenfolge, Fortschrittszeile alle 10 Spielminuten, Spielzeit-
und Echtzeitlimit, Laufzeitprofil. **Laufzeit: 1,3 s** für die ganze Kampagne (Ziel < 3 min), inklusive Build ≈ 6 s.
Profil: `Game.Tick` 0,3 s, `BuildPatch` 0,2 s, Zielsuche 0,3 s, `Game.Apply` 0,2 s – kein Performance-Problem im Core.

Zwei Verhaltensprofile:
* **Bot (optimal):** reale Höchstgeschwindigkeit, Umweg ×1,3, keine Bedienzeiten.
* **Mensch-Modell (geschätzt, `balance mensch`):** 80 % der Höchstgeschwindigkeit, Umweg ×1,5, +2 s je Aufnahme,
  +6 s je Stationsbesuch. Das ist eine **Annahme**, keine Messung an Menschen; Lernzeit, Lesen von Texten, Erkunden,
  Pausen und Fehlentscheidungen sind nicht enthalten. Echte Spielzeiten liegen eher darüber.

### Balancing-Messwerte (nach Anpassung)

| Kennzahl | Ziel (Mensch) | Bot optimal | Mensch-Modell | Bewertung |
|---|---|---|---|---|
| Erster Verkauf | 3–5 min | 0,5 min | 1,1 min | Mit Lern-/Lesezeit plausibel im Ziel; nicht angepasst (Tutorial-Lichtpunkt mit 8 leichten Objekten) |
| Erstes Upgrade (Behälter) | 10–15 min | 4,5 min | 10,5 min | im Ziel (vorher 1,2 / 2,7 min) |
| Erste sichtbare Veränderung (Lichtpunkt „Spielplatz“) | < 2 min | 0,3 min | 0,7 min | im Ziel |
| Erster Bereich zu 85 % gereinigt | – | 15,5 min | 32,5 min | – |
| TERRA | – | 2,22 h | 4,29 h | längster Planet (Grundausstattung wird gekauft) |
| PYRA | – | 1,19 h | 2,17 h | |
| PELAGIA | – | 1,19 h | 2,27 h | |
| Sparen auf Sprungantrieb (9 000 Credits) | – | 0,6 h | 1,5 h | reine Geldphase |
| NIVALIS | – | 1,95 h | 3,52 h | |
| **Gesamt** | 8–12 h | **7,2 h** | **13,8 h** | Modell knapp über dem Richtwert, s. u. |
| Notabschaltungen | – | 0 | 0 | Bot sucht immer rechtzeitig Schutz |
| Schlafpausen (davon Sturm am Tag) | – | 121 (64) | 232 (122) | |
| Notunterschlüpfe gebaut | – | 14 | 19 | |
| Lieferungen / Erlös | – | 147 / 26 039 Cr | 144 / 26 438 Cr | ≈ 37 % aller Einnahmen |
| Insgesamt verdient | – | 69 968 Cr | 70 645 Cr | |

Credits-Verlauf (Bot optimal, Kontostand/insgesamt verdient): 0:10 141/261 · 0:30 445/1 723 · 1:00 20/5 208 ·
2:00 618/11 266 · 3:00 160/17 818 · 4:00 1 433/26 011 · 5:00 5 315/35 373 · 6:00 320/47 600 · 7:11 70/69 968.
Der Kontostand bleibt fast durchgehend niedrig: Einnahmen fließen sofort in Werkzeuge, Anlagen und Projekte.

**Anpassung (gemessen, `Core/Data/GameData.cs`):** Behälter Stufe 1 90 → 180 Credits, Müllsauger Stufe 1 110 → 200.
Begründung: Startkapital 60 + Tutorial-Belohnungen (10 + 25) reichten schon **vor** dem ersten Verkauf für das erste
Upgrade (Modell 2,7 min statt 10–15 min). Zwischenwerte gemessen: 200/220 → Modell 19,4 min (zu spät, der erste
Belohnungsschub reicht nicht mehr), 150/170 → 4,7 min (zu früh), 180/200 → 10,5 min.
Verworfen: Sprungantrieb 9 000 → 6 000 (Gesamtzeit nur 13,8 → 13,7 h, das Geld fehlt dann auf NIVALIS) – zurückgenommen.

**Gesamtzeit:** Das Mensch-Modell liegt mit 13,8 h etwas über 8–12 h, der optimale Bot mit 7,2 h darunter. Wegen
der großen Unsicherheit des Modells (und weil echte Spieler zusätzlich Lernzeit haben) wurde hier nicht weiter
gekürzt; ein Spieltest mit Menschen sollte entscheiden. Größte Zeitblöcke im Modell: TERRA (4,3 h) und NIVALIS (3,5 h).

### Beobachtungen / offene Punkte

* **Lieferungen sind kostenlos und unbegrenzt** (Auftragstafel). Sie liefern ≈ 37 % aller Einnahmen des Bots, der sie
  nur nutzt, wenn in der Nähe nichts mehr liegt. Ohne Kosten/Abklingzeit sind sie eine beliebig wiederholbare
  Geldquelle – Designentscheidung offen (nicht geändert).
* Eine Lieferung kann Teile enthalten, die das aktuelle Werkzeug nicht heben kann (TERRA: Fernseher 5 kg bei 3 kg
  Greifarm); solange sie liegen, ist keine neue Lieferung möglich. Mit dem Fix (gemischte Lieferungen) keine Sackgasse mehr.
* Die Bewegungsprüfung erlaubt zusätzlich 1,2 m Kulanz **je Paket**; bei maximaler Nachrichtenrate (150/s) bliebe
  damit ein Geschwindigkeitsvorteil möglich. Nicht behoben (größerer Umbau der Prüfung nötig).
* Aktionen (z. B. Greifen) sind während der Notabschaltung (Abschleppen, 6 s) serverseitig nicht gesperrt;
  nur Bewegung und Schlafen. Geringe Auswirkung, nicht geändert.
* Schlafen beendet einen Sturm sofort, auch tagsüber (vom Bot 64×/Kampagne genutzt) – so implementiert, als
  Designfrage notiert.
* Der Bot bewegt sich geradlinig ohne Kollision mit Gebäuden/Dünen (Umwegfaktor statt Wegfindung); Boot und Rover
  nutzt er nicht, Ökologie (Pflanzen) ist für die Kampagne nicht nötig und wird nicht gespielt.

### Nicht getestet

* Alles in Unity: Darstellung, Shader, Kamera, Eingabe/Controller, UI (IMGUI), Audio-Ausgabe, Performance im Player,
  Build für Zielplattformen – NICHT GETESTET (kein Unity in dieser Umgebung).
* Netzwerk über echte Netze (Latenz, Paketverlust, NAT/Firewall, mehrere Rechner) – NICHT GETESTET; nur Loopback-TCP.
* Menschliche Spielzeiten – NICHT GEMESSEN, nur über das oben beschriebene Modell geschätzt.
