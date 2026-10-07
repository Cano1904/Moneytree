# Baustoff-KI – LV- & Kalkulations-Arbeitsplatz für den Baustoffhandel

Web-App, die den Angebotsprozess im Baustoffhandel bündelt und weitgehend automatisiert:

**Kundenanfrage → LV/Plan → Materialermittlung → Lieferanten → Preise → Kalkulation → Angebot → PDF → Versand → Archiv → Nachfassen**

Standardmodus ist **Tief- & GaLa-Bau**, **Hochbau** ist als zweiter Modus vollständig integriert (Umschalter oben bzw. je Projekt). Die App erkennt den wahrscheinlichen Bereich eines LVs, überschreibt den gewählten Modus aber nie selbst.

## Schnellstart

Voraussetzung: **Node.js 22.13 oder neuer** (nutzt das eingebaute `node:sqlite`, keine Datenbank-Installation nötig).

```bash
cd baustoff-ki
npm install
npm run build      # Oberfläche bauen
npm start          # http://localhost:3000
```

Zum Ausprobieren: **Einstellungen → Demo-Daten laden** (Lieferanten, Produkte, Preishistorie, Wissen und ein Beispielprojekt mit analysiertem LV). Beispieldateien zum Hochladen liegen in `samples/` (PDF-LV, Excel-LV, GAEB X83, Lieferantenangebot).

Entwicklung mit Hot-Reload: `npm run dev` (API auf :3000, Oberfläche auf http://localhost:5173). Tests: `npm test`.

| Umgebungsvariable | Bedeutung |
|---|---|
| `PORT` | Port (Standard 3000) |
| `DATA_DIR` | Ablage für Datenbank und Dokumente (Standard `./data`) |
| `ANTHROPIC_API_KEY` | Claude-API-Key (alternativ in den Einstellungen hinterlegen) |
| `ANTHROPIC_MODEL` | Modell (Standard `claude-opus-5-5`) |
| `APP_PASSWORD` | Optionaler Zugangsschutz (HTTP-Basic-Auth, Benutzername beliebig) |

## KI & Human-in-the-Loop

* **Mit API-Key** liest Claude LVs (auch Scans/Fotos), Lieferantenangebote und Pläne. Ergebnisse kommen als strukturiertes JSON (Structured Outputs), jede Position trägt eine Erkennungssicherheit und konkrete Prüfhinweise.
* **Ohne API-Key** arbeitet die App vollständig weiter: Regel-Erkennung für PDF-, Excel-, Word- und GAEB-LVs sowie Lieferantenangebote; Scans/Bilder werden manuell erfasst, Pläne mit dem Messwerkzeug vermessen.
* Die KI **erfindet nichts**: fehlende Mengen bleiben leer und werden als „⚠ Prüfung erforderlich“ markiert. Mengen aus Plänen rechnet nie die KI, sondern immer die deterministischen Rechner – mit Formel, Annahmen und Sicherheitsstatus.
* Preise werden nie unbemerkt geändert: jede Änderung (auch Massenänderungen und Aufschlagsläufe) steht im Verlauf, geänderte Zeilen werden hervorgehoben, manuell gesetzte Aufschläge bleiben bei globalen Änderungen erhalten.
* Mails werden **nie automatisch versendet**: Die App erstellt kopierbaren Text, `mailto:`-Links und Outlook-Entwürfe (`.eml` inkl. PDF-Anhang); der Versand wird erst nach Benutzerbestätigung als erledigt markiert.

## Funktionen

**LV-Upload & Analyse** – PDF (Text oder Scan), Excel/CSV, GAEB DA XML (X81–X86), GAEB 90 (eingeschränkt), Word, Bilder. Erkennt Ordnungszahlen, Titel, Kurz-/Langtexte, Mengen, Einheiten, Bedarfs-/Alternativpositionen und Warengruppen. Das Original wird unverändert archiviert.

**Plausibilitätsprüfung** – fehlende Mengen/Einheiten, unbekannte oder untypische Einheiten, widersprüchliche Formate (Kurz- vs. Langtext), Doppelpositionen, „wie vor“-Verweise, nicht erkannte Produkte, fehlendes Zubehör (z. B. Pflaster ohne Bettung, Rinne ohne Stirnwände). Jeder Hinweis lässt sich als „geprüft“ abhaken.

**Lieferanten & Anfragen** – Lieferantendatenbank mit Ansprechpartnern, Warengruppen, Lieferzeiten, Preisen und früheren Angeboten. Automatische Lieferantenvorschläge je Position. Klick auf eine Position → „Wie möchtest du anfragen?“ → 📞 Anruf (Nummer, Positionen, Notiz, Ergebnis, Preise) oder ✉ Anfrage-Mail. Positionen desselben Lieferanten werden automatisch zu einer Anfrage gebündelt („Alle Anfragen vorbereiten“). Status je Anfrage und Übersicht „Welche Preise fehlen noch?“.

**Lieferantenangebote** – Upload (PDF, Excel, Word, Foto) → Artikel, Preis, Einheit, Fracht, Rabatt, Lieferzeit, Preisbindung werden erkannt und LV-Positionen zugeordnet → Bestätigungstabelle mit Umrechnungsfaktor und Frachtverteilung nach Warenwert → Übernahme.

**Kalkulation** – EK, Rabatt, Fracht, Einstand, Aufschlag, VK, DB, Positionssumme, Gesamtmarge. Aufschlag global, je Warengruppe, für markierte Positionen (Mehrfachauswahl, Shift-Klick) oder einzeln; Mindestmarge mit Warnung. Variantenvergleich (günstigster EK, bester Einstand, schnellste Lieferung, bevorzugter Lieferant). Preishistorie mit „Letzter EK: 12,40 €/m² – 14.09.2026“.

**Angebot** – Deckblatt (projektbezogen, Standard aus den Einstellungen oder automatisch erzeugt) + LV + Anhänge, Reihenfolge per Drag & Drop, Summenprüfung, PDF in einem Schritt – kein Ausdrucken und Einscannen mehr. Bei PDF-LVs werden die Preise **in das Original-LV eingetragen**; bei Excel-LVs wird die Original-Excel befüllt, bei GAEB eine **X84** erzeugt. Zusätzlich ein neu gesetztes Angebots-LV mit Titelsummen.

**Versand & Nachfassen** – Angebotsmail mit PDF als Outlook-Entwurf, Versandfreigabe, automatische Wiedervorlage (z. B. 7 Tage), Ergebnis Auftrag / offen / verloren / erneut kontaktieren.

**Plananalyse & Material** – Plan-Viewer mit Messwerkzeug (Kalibrieren an einer Bemaßung, Strecke, Polylinie, Fläche, Zählen), KI-Plananalyse mit Rückfragen, Rechner für Terrassen/Balkone auf Stelzlagern (inkl. 2D-Vorschau, Rand-/Eckbedarf), Pflaster, Platten, Bordsteine, Fugenmaterial, Schüttgüter, Beton, Mauerwerk/Kalksandstein, Rinnen, Rohre, Bewehrung, Mörtel. Materialliste → als LV ins Projekt übernehmen.

**Digitale Projektakte** – alle Dokumente mit Kategorie und Version (Originale, Angebote und Lieferantenangebote schreibgeschützt), Notizen/Telefonate/Mails, vollständiger Verlauf (Audit-Log).

**Dashboard, Suche, Wissen** – Statuspipeline mit „Hier hängt Arbeit“, offene Anfragen, fällige Wiedervorlagen, fehlende Preise. Globale Suche in natürlicher Sprache („alle LVs von Kunde Müller“, „Projekte mit Kalksandstein“, „Angebote von Lieferant X“, „Objektpreise für Produkt Y“, „offene Angebote“). Wissensdatenbank für Produkte, Alternativen, Erfahrungswerte und Kalkulationsschemata – optional per KI befragbar (Antwort nur aus internen Daten).

## Architektur

```
baustoff-ki/
  shared/        Kalkulation, Rechner, Warengruppen, Status, Formatierung (Server + Browser)
  server/        Express-API, SQLite (node:sqlite), Dokumentablage
    services/    Extraktion (pdf.js, ExcelJS, mammoth), LV-Parser, GAEB, KI (Claude), Plausibilität,
                 Angebotsimport, PDF (pdfkit/pdf-lib), Excel/GAEB-Export, Mail/EML
    routes/      projects, lv, suppliers, offers, misc
  client/        React-Oberfläche (Vite)
  tests/         Unit- und End-to-End-Tests (node:test)
  samples/       Beispieldateien (Erzeugung: npm run samples)
```

Datenmodell: users, customers, projects, lvs, lv_positions, calculations, plans, products, suppliers, supplier_contacts, supplier_requests (+ items), supplier_quotes (+ items), prices, offers, documents, followups, notes, audit_logs, knowledge, settings.

Sicherung: **Einstellungen → Datenbank-Backup**; Dokumente liegen unter `DATA_DIR/files`.

## Grenzen (bewusst als Human-in-the-Loop gelöst)

* Gescannte LVs/Angebote und Pläne benötigen für die automatische Erkennung einen KI-Key; ohne Key: manuelle Erfassung bzw. Messwerkzeug.
* Preise ins Original eintragen funktioniert bei text-basierten PDFs (an Platzhaltern wie `.......` bzw. am rechten Rand); abweichende Mengen werden auf der Zusammenstellungsseite ausgewiesen. Bei Scans wird das neu gesetzte Angebots-LV verwendet.
* GAEB 90 wird nur eingeschränkt gelesen (Warnhinweis je Position); GAEB DA XML wird vollständig unterstützt.
* Mehrbenutzer ohne Rechteverwaltung (Benutzerauswahl für Verlauf/Signatur); für den Netzbetrieb `APP_PASSWORD` setzen.
