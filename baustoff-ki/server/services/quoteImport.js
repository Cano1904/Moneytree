// Lieferantenangebote auslesen (KI oder Regeln) und Positionen zuordnen – Übernahme erst nach Bestätigung
import { detectKind, pdfLines, excelRows, docxContent } from './extract.js';
import { aiJson, aiStatus, fileBlock, N, obj, arr } from './ai.js';
import { parseNum, normalizeUnit, textSimilarity, UNIT_REGEX_SRC } from '../../shared/format.js';

const SCHEMA = obj({
  supplier_name: N('string'), quote_no: N('string'), valid_until: N('string'), freight_total: N('number'), discount_pct: N('number'), delivery_text: N('string'),
  items: arr(obj({ text: { type: 'string' }, article_no: N('string'), qty: N('number'), unit: N('string'), price: N('number'), discount_pct: N('number'), delivery_text: N('string'), matched_oz: N('string'), notes: N('string') })),
});
const INSTR = `Aufgabe: Lies ein Lieferantenangebot aus und extrahiere je Artikelzeile Text, Artikelnummer, Menge, Einheit, Einzelpreis netto je Einheit (price), Rabatt %, Lieferzeit.
- price ist immer der Preis pro Einheit (nicht Gesamtpreis). Wenn nur ein Gesamtpreis vorhanden ist: price = null und Hinweis in notes.
- valid_until als ISO-Datum (YYYY-MM-DD) wenn "gültig bis"/"Preisbindung" angegeben, sonst null.
- freight_total: pauschale Fracht in Euro, falls angegeben.
- matched_oz: OZ der angefragten LV-Position, die dieser Zeile entspricht (aus der mitgelieferten Liste), sonst null. Nur zuordnen wenn fachlich eindeutig.
- Nichts erfinden: Fehlende Werte = null.`;

const PRICE_RE = /(\d{1,3}(?:\.\d{3})*,\d{2,3}|\d+,\d{2,3})\s*(?:€|EUR)?/g;
const QTY_UNIT_RE = new RegExp(`(\\d{1,3}(?:\\.\\d{3})*(?:,\\d{1,3})?|\\d+(?:,\\d{1,3})?)\\s*(${UNIT_REGEX_SRC})(?=$|[\\s.,;/)])`, 'i');

function headerParse(lines) {
  const all = lines.join('\n');
  const meta = { supplier_name: null, quote_no: null, valid_until: null, freight_total: null, discount_pct: null, delivery_text: null };
  const d = all.match(/(?:gültig bis|preisbindung bis|preise gültig bis|bindefrist)[:\s]*(\d{1,2})\.(\d{1,2})\.(\d{2,4})/i);
  if (d) meta.valid_until = `${d[3].length === 2 ? `20${d[3]}` : d[3]}-${d[2].padStart(2, '0')}-${d[1].padStart(2, '0')}`;
  const fr = all.match(/fracht[^\d\n]{0,30}(\d{1,3}(?:\.\d{3})*,\d{2}|\d+)\s*(?:€|EUR)?/i);
  if (fr && !/frei|inklusive|inkl/i.test(fr[0])) meta.freight_total = parseNum(fr[1]);
  const lz = all.match(/lieferzeit[:\s]*([^\n]{2,40})/i);
  if (lz) meta.delivery_text = lz[1].replace(/\s{2,}.*/, '').trim();
  const rb = all.match(/rabatt[:\s]*(\d+(?:,\d+)?)\s*%/i);
  if (rb) meta.discount_pct = parseNum(rb[1]);
  const no = all.match(/angebot(?:s)?(?:-?\s?nr\.?|nummer)[:\s]*([A-Z0-9-/]+)/i);
  if (no) meta.quote_no = no[1];
  return meta;
}

function lineItems(lines) {
  const items = [];
  for (const l of lines) {
    if (/fracht|gesamt|summe|mwst|netto|brutto|übertrag|zwischensumme/i.test(l)) continue;
    const qu = l.match(QTY_UNIT_RE) || l.match(new RegExp(`(\\d+(?:,\\d+)?)\\s{1,}(${UNIT_REGEX_SRC})\\s`, 'i'));
    const qStart = qu ? l.indexOf(qu[0]) : -1;
    const qEnd = qu ? qStart + qu[0].length : -1;
    // Preise, die Teil der Mengenangabe sind, ausschließen
    const prices = [...l.matchAll(PRICE_RE)].filter((m) => !(qu && m.index >= qStart && m.index < qEnd));
    if (!prices.length) continue;
    const text = l.slice(0, Math.min(qu ? qStart : Infinity, prices[0].index)).replace(/\s{2,}/g, ' ').trim();
    if (text.length < 3) continue;
    const qty = qu ? parseNum(qu[1]) : null;
    let price = parseNum(prices[0][1]), notes = null;
    if (prices.length >= 2) {
      const a = parseNum(prices[0][1]), b = parseNum(prices.at(-1)[1]);
      if (!(qty && Math.abs(a * qty - b) <= Math.max(0.05, b * 0.01))) notes = 'Mehrere Preise in der Zeile – Einzelpreis prüfen';
    }
    items.push({ text, article_no: null, qty, unit: qu ? normalizeUnit(qu[2]) : null, price, discount_pct: null, delivery_text: null, matched_oz: null, notes });
  }
  return items;
}

function tableItems(sheets) {
  for (const sh of sheets) {
    for (const hr of sh.rows.slice(0, 30)) {
      const idx = {};
      hr.cells.forEach((c, i) => {
        const s = String(c).toLowerCase();
        if (idx.text === undefined && /(artikel|bezeichnung|beschreibung|text|produkt)/.test(s) && !/nr/.test(s)) idx.text = i;
        else if (idx.art === undefined && /(art\.?-?nr|artikelnummer|artikel-nr)/.test(s)) idx.art = i;
        else if (idx.qty === undefined && /^(menge|anzahl)/.test(s)) idx.qty = i;
        else if (idx.unit === undefined && /^(einheit|me|eh)$/.test(s.trim())) idx.unit = i;
        else if (idx.price === undefined && /(einzelpreis|ep|preis\s*\/|preis je|^preis|netto)/.test(s) && !/gesamt/.test(s)) idx.price = i;
        else if (idx.disc === undefined && /rabatt/.test(s)) idx.disc = i;
      });
      if (idx.text === undefined || idx.price === undefined) continue;
      const items = sh.rows.filter((r) => r.row > hr.row).map((r) => ({
        text: String(r.cells[idx.text] ?? '').trim(), article_no: idx.art !== undefined ? String(r.cells[idx.art] ?? '') || null : null,
        qty: idx.qty !== undefined ? parseNum(r.cells[idx.qty]) : null, unit: idx.unit !== undefined ? normalizeUnit(String(r.cells[idx.unit] ?? '')) : null,
        price: parseNum(r.cells[idx.price]), discount_pct: idx.disc !== undefined ? parseNum(r.cells[idx.disc]) : null, delivery_text: null, matched_oz: null, notes: null,
      })).filter((x) => x.text && x.price !== null);
      if (items.length) return items;
    }
  }
  return null;
}

/** positions: Kandidaten [{id, oz, short_text, long_text, qty, unit}] zum Zuordnen. */
export async function analyzeQuote({ buf, filename, mime, positions = [], useAi = true }) {
  const kind = detectKind(filename, mime);
  const ai = useAi && aiStatus().enabled;
  let draft = null, method = 'heuristik';
  const warnings = [];
  let lines = [];
  if (kind === 'excel' || kind === 'csv') {
    const sheets = await excelRows(buf, kind);
    const items = tableItems(sheets);
    lines = sheets.flatMap((s) => s.rows.map((r) => r.cells.join('   ')));
    if (items) draft = { ...headerParse(lines), items };
  } else if (kind === 'pdf') {
    const pl = await pdfLines(buf);
    lines = pl.lines.map((l) => l.text);
    if (!pl.hasText && !ai) warnings.push('Gescanntes Angebot: ohne KI keine automatische Erkennung – Preise bitte manuell erfassen.');
  } else if (kind === 'docx') {
    lines = (await docxContent(buf)).text.split('\n');
  } else if (kind === 'text') lines = buf.toString('utf8').split(/\r?\n/);
  else if (kind !== 'image') throw new Error('Format für Lieferantenangebote nicht unterstützt');

  if (ai && !draft) {
    const list = positions.map((p) => `${p.oz || p.id}: ${p.short_text} – ${p.qty ?? '?'} ${p.unit ?? ''}`).join('\n');
    const blk = kind === 'pdf' || kind === 'image' ? fileBlock(buf, mime || (kind === 'pdf' ? 'application/pdf' : 'image/png')) : null;
    const content = [...(blk ? [blk] : [{ type: 'text', text: `Angebotstext:\n${lines.join('\n').slice(0, 60000)}` }]), { type: 'text', text: `Angefragte LV-Positionen:\n${list || '(keine)'}` }];
    draft = await aiJson({ content, schema: SCHEMA, instructions: INSTR, maxTokens: 32000 });
    method = 'ki';
  }
  if (!draft) draft = { ...headerParse(lines), items: lineItems(lines) };

  // Zuordnung zu Positionen
  const byOz = new Map(positions.map((p) => [String(p.oz || p.id), p]));
  for (const it of draft.items) {
    let pos = it.matched_oz ? byOz.get(String(it.matched_oz)) : null;
    let score = pos ? 0.9 : 0;
    if (!pos) {
      for (const p of positions) {
        const s = textSimilarity(it.text, `${p.short_text} ${p.long_text || ''}`) + (it.qty && p.qty && Math.abs(it.qty - p.qty) < 1e-6 ? 0.15 : 0);
        if (s > score) { score = s; pos = p; }
      }
      if (score < 0.3) pos = null;
    }
    it.position_id = pos?.id ?? null;
    it.match_score = Math.round(Math.min(1, score) * 100) / 100;
    it.unit = normalizeUnit(it.unit);
    it.unit_mismatch = Boolean(pos && it.unit && pos.unit && it.unit !== pos.unit);
    it.factor = 1;
  }
  if (!draft.items.length) warnings.push('Keine Preiszeilen erkannt – Preise bitte manuell eintragen.');
  return { ...draft, method, warnings };
}
