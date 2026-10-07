// LV-Analyse: Datei → strukturierte Positionen (KI wenn verfügbar, sonst Regeln) + Klassifizierung + Modus-Erkennung
import { PDFDocument } from 'pdf-lib';
import { detectKind, pdfLines, excelRows, docxContent } from './extract.js';
import { parseTextLv, parseTableLv, parseGaebXml, parseGaeb90 } from './lvParser.js';
import { aiJson, aiStatus, fileBlock, N, obj, arr } from './ai.js';
import { classify, detectMode, GROUP_NAMES } from '../../shared/groups.js';
import { normalizeUnit } from '../../shared/format.js';

const LV_SCHEMA = obj({
  mode_guess: obj({ mode: { type: 'string', enum: ['galabau', 'hochbau', 'unklar'] }, confidence: { type: 'number' }, reason: { type: 'string' } }),
  positions: arr(obj({
    oz: N('string'), title_path: N('string'), short_text: { type: 'string' }, long_text: { type: 'string' },
    qty: N('number'), unit: N('string'), pos_type: { type: 'string', enum: ['N', 'B', 'A', 'T'] },
    group: { anyOf: [{ type: 'string', enum: GROUP_NAMES }, { type: 'null' }] }, product_hint: N('string'),
    confidence: { type: 'number' }, issues: arr({ type: 'string' }),
  })),
});

const LV_INSTRUCTIONS = `Aufgabe: Lies ein Leistungsverzeichnis (LV) und gib jede Position strukturiert zurück.
- oz: Ordnungszahl/Positionsnummer exakt wie im Dokument (z. B. "01.02.0010"); Titel/Abschnitte NICHT als Position ausgeben, sondern als title_path ("01 Pflaster › 01.02 Bord").
- short_text: Kurztext (erste Zeile); long_text: vollständiger Langtext.
- qty/unit: Vordersatz exakt übernehmen (deutsche Zahl "1.234,500" = 1234.5). Fehlt die Menge: null + Hinweis in issues.
- pos_type: N=Normal, B=Bedarfs-/Eventualposition, A=Alternativ-/Wahlposition, T=reiner Text-/Hinweis ohne Preis.
- group: passende Warengruppe aus der Liste oder null, wenn nicht eindeutig.
- product_hint: kurzer Produktbegriff (z. B. "Rechteckpflaster 20/10/8 grau") oder null.
- confidence 0..1 für die Sicherheit der Erkennung; issues: konkrete Prüfhinweise (fehlende Mengen, widersprüchliche Maße, unklare Angaben, mögliches fehlendes Zubehör).
- Der Text kann mitten in einer Position beginnen/enden: unvollständige Positionen trotzdem ausgeben und in issues "unvollständig (Abschnittsgrenze)" vermerken.
- mode_guess: galabau = Tief-/Garten-/Landschaftsbau, hochbau = Hochbau.`;

function chunkText(lines, max = 18000) {
  const chunks = [];
  let cur = '', page = 0;
  for (const l of lines) {
    let add = '';
    if (l.page && l.page !== page) { page = l.page; add += `\n--- Seite ${page} ---\n`; }
    add += `${l.text}\n`;
    if (cur.length + add.length > max && cur) { chunks.push(cur); cur = ''; }
    cur += add;
  }
  if (cur.trim()) chunks.push(cur);
  return chunks;
}

async function pdfChunks(buf, pagesPer = 8) {
  const src = await PDFDocument.load(buf, { ignoreEncryption: true });
  const n = src.getPageCount();
  if (n <= pagesPer) return [buf];
  const out = [];
  for (let s = 0; s < n; s += pagesPer) {
    const d = await PDFDocument.create();
    const pages = await d.copyPages(src, Array.from({ length: Math.min(pagesPer, n - s) }, (_, i) => s + i));
    pages.forEach((p) => d.addPage(p));
    out.push(Buffer.from(await d.save()));
  }
  return out;
}

async function mapLimit(items, limit, fn) {
  const res = new Array(items.length);
  let i = 0;
  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, async () => { while (i < items.length) { const k = i++; res[k] = await fn(items[k], k); } }));
  return res;
}

async function aiLv(contents) {
  const parts = await mapLimit(contents, 3, (content) => aiJson({ content, schema: LV_SCHEMA, instructions: LV_INSTRUCTIONS, maxTokens: 64000 }));
  const byOz = new Map();
  const list = [];
  const votes = { galabau: 0, hochbau: 0 };
  const reasons = [];
  for (const r of parts) {
    if (r.mode_guess?.mode && r.mode_guess.mode !== 'unklar') { votes[r.mode_guess.mode] += r.mode_guess.confidence || 0.5; reasons.push(r.mode_guess.reason); }
    for (const p of r.positions || []) {
      const key = p.oz || `#${list.length}`;
      const ex = byOz.get(key);
      if (ex) {
        // Position über Abschnittsgrenze: vollständigere Variante übernehmen
        if ((p.long_text || '').length > (ex.long_text || '').length) ex.long_text = p.long_text;
        if (ex.qty === null && p.qty !== null) { ex.qty = p.qty; ex.unit = p.unit; }
        ex.issues = [...new Set([...(ex.issues || []), ...(p.issues || [])])].filter((x) => !/abschnittsgrenze/i.test(x));
        continue;
      }
      byOz.set(key, p);
      list.push(p);
    }
  }
  const total = votes.galabau + votes.hochbau;
  const mode = total ? (votes.galabau >= votes.hochbau ? 'galabau' : 'hochbau') : null;
  return { positions: list, mode: mode ? { mode, confidence: Math.round((votes[mode] / total) * 100) / 100, reason: reasons[0] || '' } : null };
}

const fromAi = (p) => ({
  oz: p.oz, title_path: p.title_path, short_text: p.short_text || '', long_text: p.long_text || '', qty: p.qty, unit: normalizeUnit(p.unit),
  pos_type: p.pos_type || 'N', group_name: p.group, product_hint: p.product_hint, confidence: p.confidence ?? 0.7,
  flags: (p.issues || []).map((msg, i) => ({ key: `ai_${i}`, severity: 'warn', msg: `KI: ${msg}` })), layout: null,
});

/**
 * Analysiert eine LV-Datei. useAi: KI verwenden falls konfiguriert.
 * Rückgabe: { format, positions, meta, info:{method, warnings[]}, detected }
 */
export async function analyzeLv({ buf, filename, mime, mode = 'galabau', useAi = true }) {
  const format = detectKind(filename, mime);
  const ai = useAi && aiStatus().enabled;
  const warnings = [];
  let res = null;
  let aiMode = null;

  if (format === 'gaeb-xml') {
    const head = buf.subarray(0, 200).toString('latin1');
    res = parseGaebXml(buf.toString(/encoding="(iso-8859-1|windows-1252)"/i.test(head) ? 'latin1' : 'utf8'));
  } else if (format === 'gaeb90') {
    res = parseGaeb90(buf.toString('latin1'));
    warnings.push('GAEB 90 wird nur eingeschränkt unterstützt – wenn möglich als GAEB DA XML (X83) exportieren.');
  } else if (format === 'excel' || format === 'csv') {
    const sheets = await excelRows(buf, format);
    res = parseTableLv(sheets);
    if (!res) {
      warnings.push('Keine Kopfzeile (Pos./Text/Menge/Einheit) gefunden – Inhalte als Text analysiert.');
      res = parseTextLv(sheets.flatMap((s) => s.rows.map((r) => ({ text: r.cells.filter((c) => c !== '').join('   ') }))));
    }
  } else if (format === 'docx') {
    const { tables, text } = await docxContent(buf);
    res = tables.length ? parseTableLv(tables) : null;
    if (!res) {
      const lines = text.split('\n').map((t) => ({ text: t }));
      res = ai ? null : parseTextLv(lines);
      if (ai) { const r = await aiLv(chunkText(lines).map((c) => [{ type: 'text', text: `LV-Text:\n${c}` }])); aiMode = r.mode; res = { positions: r.positions.map(fromAi), info: { method: 'ki' } }; }
    }
  } else if (format === 'pdf') {
    const pl = await pdfLines(buf);
    const heur = pl.hasText ? parseTextLv(pl.lines, pl.pages) : { positions: [], info: { method: 'heuristik' } };
    if (ai) {
      const contents = pl.hasText
        ? chunkText(pl.lines).map((c) => [{ type: 'text', text: `LV-Text (aus PDF extrahiert, Spalten durch mehrere Leerzeichen getrennt):\n${c}` }])
        : (await pdfChunks(buf)).map((b) => [fileBlock(b, 'application/pdf'), { type: 'text', text: 'Lies dieses gescannte LV vollständig aus.' }]);
      const r = await aiLv(contents);
      aiMode = r.mode;
      const hBy = new Map(heur.positions.filter((p) => p.oz).map((p) => [p.oz, p]));
      const positions = r.positions.map(fromAi).map((p) => {
        const h = p.oz ? hBy.get(p.oz) : null;
        if (h) {
          p.layout = h.layout;
          if (h.qty !== null && p.qty !== null && Math.abs(h.qty - p.qty) > 1e-6) p.flags.push({ key: 'ai_qty_conflict', severity: 'warn', msg: `Menge uneindeutig: KI ${p.qty} / Textanalyse ${h.qty} – bitte prüfen` });
        }
        return p;
      });
      res = { positions, info: { method: pl.hasText ? 'ki+text' : 'ki-scan' } };
      if (!pl.hasText) warnings.push('Gescanntes PDF: Positionen per KI aus dem Bild gelesen – Mengen bitte besonders sorgfältig prüfen. Preise können nicht ins Original eingetragen werden.');
    } else {
      res = heur;
      if (!pl.hasText) warnings.push('PDF enthält keinen Text (Scan). Ohne KI ist keine automatische Erkennung möglich – Positionen bitte manuell erfassen oder KI aktivieren.');
    }
    res.meta = { ...(res.meta || {}), pages: pl.pages, hasText: pl.hasText };
  } else if (format === 'image') {
    if (ai && fileBlock(buf, mime)) {
      const r = await aiLv([[fileBlock(buf, mime), { type: 'text', text: 'Lies dieses LV (Foto/Scan) vollständig aus.' }]]);
      aiMode = r.mode;
      res = { positions: r.positions.map(fromAi), info: { method: 'ki-bild' } };
      warnings.push('Bild per KI gelesen – Mengen bitte besonders sorgfältig prüfen.');
    } else {
      res = { positions: [], info: { method: 'manuell' } };
      warnings.push('Bild/Scan: automatische Erkennung benötigt KI – Positionen bitte manuell erfassen.');
    }
  } else if (format === 'text') {
    res = parseTextLv(buf.toString('utf8').split(/\r?\n/).map((t) => ({ text: t })));
  } else {
    throw new Error('Dateiformat nicht unterstützt (PDF, XLSX, CSV, DOCX, GAEB X8x/D8x, PNG/JPG)');
  }

  for (const p of res.positions) {
    if (!p.group_name) {
      const c = classify(p.short_text, mode, p.long_text);
      p.group_name = c.group;
      if (c.group) p.confidence = Math.min(p.confidence ?? 0.8, Math.max(c.confidence, 0.5));
    }
  }
  const heurMode = detectMode(res.positions.map((p) => `${p.short_text} ${p.long_text}`));
  const detected = aiMode || (heurMode.mode ? { mode: heurMode.mode, confidence: heurMode.confidence, reason: 'Auswertung der Warengruppen' } : null);
  if (!res.positions.length && !warnings.length) warnings.push('Keine Positionen erkannt – Dokumentaufbau prüfen oder Positionen manuell erfassen.');
  return { format, positions: res.positions, meta: res.meta || {}, info: { ...(res.info || {}), method: res.info?.method, warnings }, detected };
}
