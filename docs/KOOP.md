# RE:PLANET – Koop (1–4 Spieler)

Dieses Dokument beschreibt, wie der Koop-Modus technisch funktioniert und wie man ihn einrichtet.
Alle Angaben sind aus dem Code belegt; die Fundstellen stehen jeweils dabei.

## 1. Überblick

- **Serverautoritativ:** Die Welt wird ausschließlich in einer **Sitzung** (`Core/Net/Session.cs`) verändert.
  Clients schicken nur *Absichten* (Positionsmeldungen und Aktionen), der Server prüft sie mit denselben Regeln
  (`Core/Sim/Game.cs`, `GameActions.cs`, `Rules.cs`) und verteilt das Ergebnis.
- **Ein Codepfad für alles:** Auch das Solo-Spiel läuft über eine Sitzung – nur mit einer prozessinternen Verbindung
  (`LocalServerTransport`). Öffnet der Host die Welt, kommt ein TCP-Port hinzu (`TcpServerTransport`).
  Der dedizierte Server (`Server/`) nutzt exakt dieselbe Sitzungslogik.
- **1–4 Spieler** je Sitzung (`GameData.MaxPlayers = 4`).
- **Gemeinsam:** Credits (eine Kasse), Upgrades (Team-Forschung), Lager, Gebäude, Projekte, Aufträge.
  **Persönlich:** Behälterinhalt, Energie, Position und die Kosmetik des eigenen Roboters.

| Betriebsart | Wo läuft die Simulation? | Verbindung |
| --- | --- | --- |
| Solo | im Spiel (`HostServer`) | lokal im Prozess |
| Koop mit Host | im Spiel des Hosts | lokal + TCP (Standard-Port 7777) |
| Dedizierter Server | `Server/` (.NET 8, ohne Unity) | TCP, beliebig viele Sitzungen |

## 2. Koop starten

### Als Host (im Spiel)

1. Solo-Spiel laden oder neu starten.
2. **Pause › Koop › „Welt für Mitspieler öffnen“.** Das Spiel öffnet den TCP-Port aus den Einstellungen
   (`Settings.CoopPort`, Standard **7777**) und zeigt Einladungen an – eine je IPv4-Adresse des Rechners
   (`HostServer.LocalAddresses()`), jeweils im Format **`Adresse:Port/CODE`**.
3. Einladung an die Mitspieler schicken. Die Welt kann jederzeit wieder geschlossen werden
   („Koop geschlossen – die Welt ist wieder privat.“).

### Als Mitspieler

Im Koop-Menü **Beitreten** wählen und die Einladung einfügen. Erlaubte Formen (`HostServer.ParseInvite`):

```
192.168.0.10:7777/K7M2QX      Adresse, Port und Code
192.168.0.10/K7M2QX           ohne Port → 7777
meinpc.example.org:7777/K7M2QX
replanet://192.168.0.10:7777/K7M2QX
```

Der Code darf auch separat eingegeben werden; Groß-/Kleinschreibung ist egal.

### Sitzungscode

- 6 Zeichen aus `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (ohne die verwechselbaren I, O, 0, 1) – `Ids.Code(6)`.
- Der Code ist die „Tür“ zur Sitzung: Ohne passenden Code wird eine Verbindung abgelehnt
  („Sitzung „…“ wurde nicht gefunden. Code prüfen.“). Einladungen daher nur an Mitspieler weitergeben.
- Auf dem dedizierten Server kann der Code der dauerhaften Welt selbst gewählt werden (`--code`, 4–12 Zeichen A–Z, 0–9).

## 3. Ablauf einer Verbindung

1. Der Client baut eine TCP-Verbindung auf (Zeitlimit 6 s, `TcpClientTransport.Connect`) und sendet `hello`
   mit Protokollversion, Spieler-ID (aus `profile.json`), Name, Code, Kosmetik und ggf. Wiederverbindungs-Token.
2. Verbindungen ohne `hello` werden nach **15 s** getrennt (`SessionHub.Update`).
3. Der Server antwortet mit `welcome` und dem **vollständigen Weltzustand** (Snapshot). Dadurch sind
   **späte Beitritte** jederzeit möglich – auch mitten in einem Projekt oder Sturm.
4. Danach laufen:
   - **Patches** mit geänderten Zustandsteilen (bis zu 30×/s, nur wenn sich etwas geändert hat),
   - **Positionspakete** aller Spieler, Fahrzeuge und Drohnen (15×/s); entfernte Objekte werden mit ~120 ms Verzögerung
     interpoliert (`Interp`),
   - **Aktionen** mit eindeutiger Anfrage-ID und Antwort (`act`/`res`),
   - **Ping** alle 2 s (Anzeige der Latenz).

Die Übertragung ist ein einfacher Rahmen aus 4 Byte Länge (Big-Endian) + UTF-8-JSON (`Framing`, max. 8 MB je Nachricht).
Protokollversion: `Session.ProtocolVersion = 1` – Host und Gäste müssen dieselbe Spielversion nutzen.

### Schutzmechanismen

- **Idempotenz:** Jede Aktion trägt eine Anfrage-ID. Kommt dieselbe ID erneut an, schickt der Server das gespeicherte
  Ergebnis zurück, ohne die Aktion noch einmal auszuführen (letzte 512 IDs je Spieler). Nichts wird doppelt verkauft oder gekauft.
- **Reihenfolge:** Die Sitzung verarbeitet Nachrichten nacheinander. Greifen zwei Spieler gleichzeitig nach demselben
  Objekt, bekommt es genau einer; der andere erhält eine verständliche Meldung.
- **Bewegungsprüfung:** Positionen außerhalb der Welt oder zu schnelle Sprünge werden abgelehnt und der Client korrigiert (`corr`).
- **Ratenbegrenzung:** höchstens 150 Nachrichten pro Sekunde und Verbindung.
- **Hängende Clients:** Läuft der Sendepuffer einer Verbindung voll (4096 Nachrichten), wird sie getrennt, statt den
  Speicher des Servers zu füllen.

## 4. Rechte und Vertrauensmodus

Aus `Core/Sim/GameActions.cs`:

| Aktion | Host | Gast |
| --- | --- | --- |
| Sammeln, Zerlegen, Verkaufen, Einlagern, Sortieren, Entsorgen, Reparieren, Pflanzen, Fahrzeuge fahren | ja | ja |
| Käufe (Upgrades, Fahrzeuge, Material, Gebäude, Projekte, Sprungantrieb) **unter 800 Credits** | ja | ja |
| Käufe **ab 800 Credits** | ja | nur im Vertrauensmodus |
| Gebäude abreißen | ja | nur im Vertrauensmodus |
| Transportschiff starten (Planetenwechsel) | ja | nein |
| Vertrauensmodus ein-/ausschalten | ja | nein |
| Speichern | ja (Spielstand liegt beim Host) | nein |

Verweigerte Aktionen melden: „Teure Käufe, Abriss und Reisen sind dem Host vorbehalten (Host kann den Vertrauensmodus aktivieren).“
Der Vertrauensmodus (`TrustGuests`) wird mit der Welt gespeichert.

## 5. Gemeinsam spielen – was sich im Koop ändert

- **Keine Pause:** Im Solo-Spiel steht die Welt still, solange ein Menü offen ist. Im Koop läuft sie weiter.
- **Schlafen:** Die Nacht (bzw. ein Sturm) wird erst übersprungen, wenn **alle** verbundenen Spieler geschützt schlafen
  („Warte auf Mitspieler (1/2 schlafen) …“).
- **Notabschaltung:** Leert sich der Akku ungeschützt in Nacht oder Sturm, schleppt eine Drohne den Roboter zum Stützpunkt.
  Nur allein im Spiel vergeht dabei die Nacht; im Koop läuft die Zeit für alle normal weiter.
- **Wracks gemeinsam anheben:** Mitspieler in der Nähe eines Wracks können „helfen“ – jeder Helfer beschleunigt das Anheben
  mit dem Kran um 75 %.
- **Fahrzeuge:** Jedes Fahrzeug hat genau einen Fahrer („Das Fahrzeug wird bereits gesteuert.“).
- **Planetenwechsel** nimmt alle Spieler mit; Fahrzeuge werden geparkt, getragene Wracks abgesetzt.

## 6. Späte Beitritte, Verlassen und Wiederverbinden

- **Beitreten:** jederzeit, solange weniger als 4 Spieler online sind („Die Sitzung ist voll (4/4).“).
  Die Spielerdaten (Behälter, Energie) hängen an der Spieler-ID aus `profile.json` und bleiben in der Welt erhalten –
  wer später wieder beitritt, hat seinen Behälterinhalt noch und startet am Stützpunkt.
- **Wiederverbindungs-Token:** Beim ersten Beitritt vergibt der Server ein Token je Spieler und Sitzung; das Spiel merkt es
  sich in `profile.json` (unter dem Sitzungscode). Ist die alte Verbindung nach einem Netzabbruch serverseitig noch offen,
  ersetzt eine neue Verbindung mit gültigem Token die alte. Ohne Token wird ein zweiter Beitritt mit derselben Spieler-ID
  abgelehnt („Dieses Spielerprofil ist bereits in der Sitzung.“). Ist die alte Verbindung schon geschlossen, genügt ein
  normaler neuer Beitritt.
- **Gast verlässt die Sitzung:** Sein Roboter verschwindet für die anderen; die Welt läuft weiter.
- **Host verlässt die Sitzung (im Spiel):** Der Stand wird beim Host gespeichert, danach endet die Sitzung für alle.
  Gäste sehen: „Der Host hat die Sitzung beendet. Der Spielstand wurde beim Host gesichert.“ – und landen im Hauptmenü.
  Das gilt auch, wenn das Spiel des Hosts abstürzt oder die Verbindung abreißt: Ohne Host keine Welt.
- **Host-Verbindung bricht ab (dedizierter Server):** Der Server sichert die Welt, wartet bis zu **30 s** auf die Rückkehr
  des Hosts („Verbindung zum Host unterbrochen – warte bis zu 30 Sekunden …“) und beendet sonst die Sitzung
  („Der Host ist nicht zurückgekehrt. Die Welt wurde gesichert.“). Verlässt der Host die Sitzung regulär, endet sie sofort.

## 7. Speichern im Koop

- Die Welt gehört dem **Host**. Der Server schickt ihm bei wichtigen Ereignissen den aktuellen Stand, und das Spiel des Hosts
  schreibt ihn in den aktiven Spielstand: Projekt gestartet/abgeschlossen, Bereich gereinigt, Zugang freigelegt,
  Planetenwechsel, nach dem Schlafen, Ökologie abgeschlossen, Kampagnenende und automatisch alle 2 Minuten laufender Spielzeit.
- Gäste können nicht speichern („Die Welt gehört dem Host – nur er kann speichern.“).
- Der **dedizierte Server** speichert jede Sitzung selbst als `<saves>/<CODE>.rpsave` (plus `.bak.rpsave`) – bei denselben
  Ereignissen, wenn der Host die Sitzung verlässt und beim Beenden des Servers.

## 8. Netzwerk einrichten: Ports, Firewall, Router

- **Protokoll/Port:** TCP, Standard **7777**, im Spiel unter Einstellungen änderbar (Host) bzw. `--port` (Server).
  Gäste brauchen keinen offenen Port.
- **Nur IPv4:** Der Host lauscht auf allen IPv4-Adressen (`IPAddress.Any`). Anschlüsse ohne eigene öffentliche IPv4-Adresse
  (z. B. DS-Lite/CGNAT bei manchen Kabel- und Glasfaseranbietern) können keine Verbindungen aus dem Internet annehmen –
  dann ein virtuelles LAN oder einen Server mit öffentlicher IPv4 verwenden.
- **Windows-Firewall:** Beim ersten Öffnen der Welt fragt Windows, ob RE:PLANET im Netzwerk kommunizieren darf.
  Für LAN-Spiele „Private Netzwerke“ erlauben. Wurde versehentlich abgelehnt: *Windows-Sicherheit › Firewall- und
  Netzwerkschutz › Zugriff von App durch Firewall zulassen* › RE:PLANET erlauben (oder eine eingehende Regel für TCP 7777 anlegen).
- **Router (Internet):** Eine Portweiterleitung **TCP 7777 → lokale IP des Host-PCs** einrichten. In der Einladung dann die
  öffentliche IP-Adresse (oder einen DynDNS-Namen) statt der lokalen Adresse verwenden, z. B. `203.0.113.5:7777/K7M2QX`.
- **Virtuelles LAN (ohne Router-Einstellungen):** **Tailscale** oder **ZeroTier** auf allen Rechnern installieren und
  dasselbe Netz beitreten. Die Einladung verwendet dann die dort angezeigte Adresse des Hosts (Tailscale z. B. `100.x.y.z`).
  Häufig erscheint diese Adresse schon in der Einladungsliste des Spiels, weil es alle IPv4-Adressen des Rechners anzeigt.
- Es werden **keine externen kostenpflichtigen Dienste** benötigt; RE:PLANET nutzt keine Accounts, keine Lobby-Server und
  keine Relays.

## 9. Dedizierter Server

Reines .NET 8, kein Unity nötig – läuft auf Windows, Linux und macOS (z. B. auf einem kleinen Heimserver oder vServer).

```bash
dotnet run --project Server -- --port 7777 --saves ./server-saves
```

| Option | Bedeutung |
| --- | --- |
| `--port <Zahl>` | TCP-Port (Standard 7777) |
| `--saves <Ordner>` | Ordner für Spielstände (Standard `./server-saves`) |
| `--code <CODE>` | Code der dauerhaften Welt; ohne Angabe wird der Code des zuletzt gespeicherten Stands übernommen, sonst ein neuer Zufallscode erzeugt |
| `--world <Name>` | Name einer neu angelegten Welt |
| `--planet terra\|pyra\|pelagia` | Startplanet einer neuen Welt |
| `--no-world` | keine dauerhafte Welt öffnen (nur Sitzungen, die Clients selbst erstellen) |
| `--help` | Hilfe |

Verhalten:

- Beim Start öffnet der Server eine **dauerhafte Welt** (aus `<saves>/<CODE>.rpsave` oder neu) und schreibt die Einladungen
  ins Log: `Einladung (LAN/VPN): 192.168.0.20:7777/K7M2QX`. Mitspieler treten wie bei einem Host über **Beitreten** bei.
- **Wer zuerst beitritt, wird Host** der Sitzung (Rechte siehe Abschnitt 4). Verlässt der Host die Sitzung, wird sie gesichert
  und beendet; der Server öffnet die dauerhafte Welt sofort wieder aus dem Spielstand (die übrigen Spieler müssen erneut beitreten).
- Die Simulation ruht, solange niemand verbunden ist.
- Clients können über das Protokoll zusätzlich eigene Sitzungen anlegen (`hello` mit `create`); das nutzt derzeit nur der
  Rauchtest – das Spiel selbst tritt Sitzungen per Code bei.
- **Beenden mit Strg+C** (oder SIGTERM): Alle Welten werden gespeichert, die Spieler erhalten
  „Der Server wurde beendet. Die Welt wurde auf dem Server gesichert.“
- Ist ein vorhandener Spielstand nicht lesbar, öffnet der Server die Welt **nicht** (damit nichts überschrieben wird) und meldet das im Log.

Rauchtest gegen einen laufenden Server:

```bash
dotnet run --project Server/SmokeTest -- --host 127.0.0.1 --port 7777 --saves ./server-saves --code <CODE>
```

Er prüft mit echten TCP-Verbindungen: Sitzung erstellen, Beitritt per Code, gegenseitige Sichtbarkeit, Emote, eine Aktion
mit Serverantwort, Ablehnung eines falschen Codes, Speichern auf dem Server (gültige Datei), Verlassen von Gast und Host
und – mit `--code` – den Beitritt zweier Spieler zur dauerhaften Welt.

## 10. Fehlermeldungen und was sie bedeuten

| Meldung | Ursache / Abhilfe |
| --- | --- |
| „Keine Antwort von *Adresse:Port* (Zeitüberschreitung). Prüfe Adresse, Firewall und Portweiterleitung.“ | Host nicht erreichbar: falsche IP, Firewall blockiert, keine Portweiterleitung, CGNAT. |
| „Verbindung abgelehnt: Auf *Adresse:Port* läuft keine RE:PLANET-Sitzung.“ | Rechner erreichbar, aber der Port ist nicht offen – hat der Host die Welt geöffnet? Stimmt der Port? |
| „Adresse „…“ wurde nicht gefunden.“ | Rechnername/DNS-Name unbekannt – IP-Adresse verwenden. |
| „Adresse ungültig. Format: 192.168.0.10:7777/ABC123“ | Einladung falsch eingefügt. |
| „Bitte den Sitzungscode angeben.“ | Einladung ohne `/CODE` und kein separater Code. |
| „Sitzung „…“ wurde nicht gefunden. Code prüfen.“ | Falscher Code, oder die Sitzung ist inzwischen beendet. |
| „Spielversion passt nicht zum Host (Protokoll x statt y).“ | Host und Gast nutzen unterschiedliche Spielversionen. |
| „Die Sitzung ist voll (4/4).“ | Bereits vier Spieler online. |
| „Dieses Spielerprofil ist bereits in der Sitzung.“ | Dieselbe Spieler-ID ist noch verbunden (z. B. zweites Spiel mit derselben `profile.json`). |
| „Ungültiges Spielerprofil.“ | `profile.json` ohne gültige Spieler-ID – Datei löschen, das Spiel legt eine neue an. |
| „Port 7777 ist bereits belegt. Anderen Port in den Einstellungen wählen.“ | Ein anderes Programm (oder ein zweites RE:PLANET) nutzt den Port. |
| „Zeitüberschreitung – keine Antwort vom Host.“ | Innerhalb von 20 s kam keine Begrüßung vom Host zurück (Host reagiert nicht oder Verbindung hängt). |
| „Verbindung zum Host wurde getrennt.“ / „Verbindung verloren: …“ | Netzabbruch während des Spiels. |
| „Der Host hat die Sitzung beendet. Der Spielstand wurde beim Host gesichert.“ | Host hat die Sitzung verlassen oder das Spiel beendet. |
| „Verbindung zum Host unterbrochen – warte bis zu 30 Sekunden …“ | Nur dedizierter Server: Host-Verbindung weg, Server wartet. |
| „Der Host ist nicht zurückgekehrt. Die Welt wurde gesichert.“ | Nur dedizierter Server: Host kam nicht innerhalb von 30 s zurück. |
| „Teure Käufe, Abriss und Reisen sind dem Host vorbehalten (Host kann den Vertrauensmodus aktivieren).“ | Rechte, siehe Abschnitt 4. |
| „Spielstand wurde abgelehnt: …“ | Beim Erstellen einer Server-Sitzung mitgeschickter Spielstand ist beschädigt oder zu neu. |
