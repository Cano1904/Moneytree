// Regelbasierte LV-Erkennung (ohne KI): Text-LVs (PDF/Word/TXT), Tabellen (Excel/CSV/Word), GAEB DA XML, GAEB 90
import { XMLParser } from 'fast-xml-parser';
import { parseNum, normalizeUnit, UNIT_REGEX_SRC } from '../../shared/format.js';

const OZ_RE = /^\s*(?:Pos(?:ition)?\.?\s*(?:-?\s*Nr\.?)?\s*)?((?:\d{1,4}\.){1,5}\d{1,4}\.?|\d{1,6}\.?)(?=\s|$)\s*(.*)$/i;
const UNIT_LINE_RE = new RegExp(`^\\s*(${UNIT_REGEX_SRC})\\s*(?:EP\\b|[._]{3,}|$)`, 'i');
const UNIT_ONLY_RE = new RegExp(`^(${UNIT_REGEX_SRC})(\\s|$)`, 'i');
const QTY_RE = new RegExp(`(?:^|[\\s:])(\\d{1,3}(?:\\.\\d{3})+(?:,\\d{1,4})?|\\d+(?:,\\d{1,4})?)\\s*(${UNIT_REGEX_SRC})(?=$|[\\s.,;_)]|\\.{2,})`, 'gi');
const PLACEHOLDER_RE = /^[._\s]{4,}$|\.{4,}|_{4,}/;
const PRICE_WORD_RE = /\b(EP|GP|E\.?-?Preis|G\.?-?Preis|Einheitspreis|Gesamtpreis)\b/gi;

function posTypeFromText(t) {
  if (/bedarfsposition|eventualposition|nur auf (besondere )?anordnung|bedarfspos\b|\(bedarf\)|eventualpos/i.test(t)) return 'B';
  if (/alternativposition|wahlposition|alternativ zu pos|alternative zu|\(alternativ\)|alt\.-pos/i.test(t)) return 'A';
  return 'N';
}

/** Mengen-/Einheitskandidaten einer Zeile mit Bewertung. */
function qtyCandidates(text) {
  const out = [];
  for (const m of text.matchAll(QTY_RE)) {
    const after = text.slice(m.index + m[0].length);
    const before = text.slice(0, m.index);
    let score = 0;
    if (/^\s*([._]{3,}|EP|GP|$)/i.test(after)) score += 3;
    if (/,\d{3}$/.test(m[1])) score += 1;
    if (before.trim().length === 0) score += 2;
    if (/menge[:\s]*$/i.test(before)) score += 3;
    if (text.length < 45) score += 1;
    if (/\b(dicke|höhe|breite|länge|tiefe|stärke|abstand|ca\.|bis|je|pro|à|mind|mindestens|max)\s*$/i.test(before)) score -= 3;
    out.push({ qty: parseNum(m[1]), unit: normalizeUnit(m[2]), score, index: m.index, raw: m[0].trim() });
  }
  return out;
}

/** Text-LV (Zeilen) → Positionen. lines: [{text, page?, y?, items?}] */
export function parseTextLv(lines, pages = []) {
  const blocks = [];
  let cur = null;
  for (const ln of lines) {
    const t = ln.text.replace(/\t/g, '   ');
    if (!t.trim()) continue;
    if (/^\s*(Seite|Blatt)\s+\d+|^\s*Übertrag|^\s*Summe\s|^\s*Zwischensumme/i.test(t)) continue;
    const m = t.match(OZ_RE);
    const rest = m?.[2]?.trim() || '';
    const shortNum = m && !m[1].includes('.') && m[1].replace(/\.$/, '').length < 3;
    // 1–2-stellige Nummern nur als Titel akzeptieren (kurzer Text, Großbuchstabe, keine Menge)
    const titleLike = shortNum && /^[A-ZÄÖÜ]/.test(rest) && rest.length < 70 && !qtyCandidates(t).length;
    const looksLikeOz = m && rest && (!shortNum || titleLike) && !/^\d+,\d/.test(rest) && !(UNIT_ONLY_RE.test(rest) && rest.length < 25);
    if (looksLikeOz) {
      cur = { oz: m[1].replace(/\.$/, ''), head: m[2].trim(), lines: [ln], depth: m[1].replace(/\.$/, '').split('.').length };
      blocks.push(cur);
    } else if (cur) {
      cur.lines.push(ln);
    }
  }
  // Mengen je Block bestimmen
  for (const b of blocks) {
    let best = null;
    b.lines.forEach((ln, li) => {
      for (const c of qtyCandidates(ln.text)) if (!best || c.score >= best.score) best = { ...c, li };
    });
    b.qty = best;
    if (!best) b.lines.forEach((ln, li) => {
      const mm = ln.text.match(UNIT_LINE_RE);
      if (mm && li > 0) b.unitOnly = { unit: normalizeUnit(mm[1]), li, score: 0, raw: mm[1] };
    });
  }
  const withQty = blocks.filter((b) => b.qty && b.qty.score > 0);
  const depthCount = {};
  for (const b of withQty) depthCount[b.depth] = (depthCount[b.depth] || 0) + 1;
  const posDepth = Number(Object.entries(depthCount).sort((a, b) => b[1] - a[1])[0]?.[0] || 0);
  const titles = {};
  const positions = [];
  for (const b of blocks) {
    const fullText = b.lines.map((l) => l.text).join('\n');
    if (b.depth < posDepth && !(b.qty && b.qty.score > 0)) { titles[b.oz] = b.head.replace(/\s{2,}.*$/, ''); continue; }
    const titlePath = Object.keys(titles).filter((oz) => b.oz.startsWith(`${oz}.`)).map((oz) => titles[oz]).join(' › ');
    const q = b.qty;
    const qLine = q?.li ?? b.unitOnly?.li;
    const clean = (s) => s.replace(QTY_RE, (mm) => (q && mm.trim() === q.raw ? ' ' : mm)).replace(/[._]{4,}/g, ' ').replace(PRICE_WORD_RE, ' ').replace(/\s{2,}/g, ' ').trim();
    const textLines = b.lines.map((l, i) => (i === 0 ? b.head : l.text)).map((s, i) => (i === qLine ? (q ? clean(s) : '') : s.replace(/[._]{4,}.*$/, '').trim())).filter(Boolean);
    const short = (textLines[0] || '').replace(/\s{3,}.*/, '');
    const long = textLines.slice(1).join('\n').trim();
    const flags = [];
    let qty = null, unit = null, confidence = 0.75;
    if (q && q.score > 0) { qty = q.qty; unit = q.unit; confidence = q.score >= 3 ? 0.85 : 0.65; }
    else if (q) { qty = q.qty; unit = q.unit; confidence = 0.35; flags.push({ key: 'qty_from_text', severity: 'warn', msg: `Menge nur im Fließtext gefunden („${q.raw}“) – bitte prüfen` }); }
    if (!q && b.unitOnly) unit = b.unitOnly.unit;
    let posType = posTypeFromText(fullText);
    if (!q && /hinweis|vorbemerkung|allgemein|zulage zu/i.test(fullText) && !/zulage/i.test(short)) posType = 'T';
    const layout = layoutFor(b, q || b.unitOnly, pages);
    positions.push({ oz: b.oz, title_path: titlePath || null, short_text: short, long_text: long, qty, unit, pos_type: posType, confidence, flags, layout });
  }
  return { positions, info: { method: 'heuristik', blocks: blocks.length, posDepth } };
}

/** Koordinaten für das Eintragen der Preise in das Original-PDF. */
function layoutFor(b, q, pages) {
  const ln = q ? b.lines[q.li] : b.lines[0];
  if (!ln || ln.page === undefined) return null;
  const cand = [ln, b.lines[(q?.li ?? 0) + 1]].filter(Boolean);
  let slots = [];
  for (const l of cand) {
    slots = (l.items || []).filter((it) => PLACEHOLDER_RE.test(it.str)).map((it) => ({ x: it.x, w: it.w, y: l.y, page: l.page }));
    if (slots.length) break;
  }
  const pg = pages[ln.page - 1] || { width: 595, height: 842 };
  const lay = { page: ln.page, y: ln.y, h: ln.h || 9, pageWidth: pg.width, qty0: q?.qty ?? null };
  if (slots.length >= 2) { lay.ep = slots.at(-2); lay.gp = slots.at(-1); lay.mode = 'platzhalter'; }
  else if (slots.length === 1) { lay.gp = slots[0]; lay.ep = { x: slots[0].x - 90, w: 80, y: slots[0].y, page: ln.page }; lay.mode = 'platzhalter'; }
  else { lay.ep = { x: pg.width - 150, w: 60, y: ln.y, page: ln.page }; lay.gp = { x: pg.width - 80, w: 60, y: ln.y, page: ln.page }; lay.mode = 'rand'; }
  return lay;
}

// ---------- Tabellen (Excel/CSV/Word) ----------
const HEAD = {
  oz: /^(pos(ition)?\.?(-?\s?nr\.?)?|oz|ordnungszahl|nr\.?|lfd\.?\s?nr\.?)$/i,
  text: /(kurztext|beschreibung|bezeichnung|leistung|artikel|^text$|positionstext)/i,
  long: /langtext/i,
  qty: /^(menge|anzahl|mng\.?|qty|lv-menge|vordersatz)/i,
  unit: /^(einheit|me|eh|einh\.?|mengeneinheit|me\.)$/i,
  ep: /(^ep\b|einheitspreis|e\.?-?preis|preis\s*\/\s*einh|^preis$)/i,
  gp: /(^gp\b|gesamtpreis|g\.?-?preis|^gesamt|^betrag)/i,
};
export function findHeader(rows) {
  let best = null;
  for (const r of rows.slice(0, 40)) {
    const map = {};
    r.cells.forEach((c, i) => {
      const s = String(c ?? '').trim();
      if (!s || s.length > 40) return;
      for (const [k, re] of Object.entries(HEAD)) if (map[k] === undefined && re.test(s)) { map[k] = i; break; }
    });
    const score = Object.keys(map).length + (map.text !== undefined ? 1 : 0) + (map.qty !== undefined ? 1 : 0);
    if ((map.text !== undefined || map.qty !== undefined) && score >= 3 && (!best || score > best.score)) best = { row: r.row, map, score };
  }
  return best;
}

export function parseTableLv(sheets) {
  for (const sh of sheets) {
    const hdr = findHeader(sh.rows);
    if (!hdr) continue;
    const { map } = hdr;
    const positions = [];
    let title = null;
    const val = (cells, k) => (map[k] === undefined ? '' : cells[map[k]] ?? '');
    for (const r of sh.rows.filter((x) => x.row > hdr.row)) {
      const oz = String(val(r.cells, 'oz')).trim();
      const text = String(val(r.cells, 'text')).trim();
      const long = String(val(r.cells, 'long')).trim();
      const qtyRaw = val(r.cells, 'qty');
      const qty = parseNum(qtyRaw);
      const unit = normalizeUnit(String(val(r.cells, 'unit')).trim());
      if (!oz && !text && qty === null) continue;
      if (/^(summe|gesamt|zwischensumme|übertrag)/i.test(text) || /^(summe|gesamt)/i.test(oz)) continue;
      if (qty === null && !unit) {
        if (!oz && positions.length) { positions.at(-1).long_text = [positions.at(-1).long_text, text].filter(Boolean).join('\n'); continue; }
        if (oz) { title = text; continue; }
      }
      const flags = [];
      if (qtyRaw !== '' && qty === null) flags.push({ key: 'qty_unreadable', severity: 'error', msg: `Menge „${qtyRaw}“ nicht lesbar` });
      positions.push({ oz: oz || null, title_path: title, short_text: text.split('\n')[0], long_text: [text.split('\n').slice(1).join('\n'), long].filter(Boolean).join('\n'), qty, unit, pos_type: posTypeFromText(`${text} ${long}`), confidence: 0.9, flags, layout: { sheet: sh.name, row: r.row } });
    }
    if (positions.length) return { positions, meta: { sheet: sh.name, headerRow: hdr.row, columns: map }, info: { method: 'tabelle', sheet: sh.name } };
  }
  return null;
}

// ---------- GAEB DA XML (X81/X83/X84/X86) ----------
const ARR = new Set(['BoQCtgy', 'Item', 'Remark', 'p', 'span', 'TextComplement', 'MarkupItem']);
export function parseGaebXml(xml) {
  const parser = new XMLParser({ ignoreAttributes: false, attributeNamePrefix: '@_', removeNSPrefix: true, parseTagValue: false, trimValues: false, isArray: (name) => ARR.has(name) });
  const doc = parser.parse(xml);
  const gaeb = doc.GAEB;
  if (!gaeb) throw new Error('Keine GAEB-DA-XML-Datei (Wurzelelement GAEB fehlt)');
  const award = gaeb.Award || gaeb.Tender || {};
  const boq = award.BoQ;
  if (!boq) throw new Error('GAEB-Datei enthält kein Leistungsverzeichnis (BoQ)');
  const dp = String(award.DP ?? gaeb.Award?.DP ?? '').trim();
  const positions = [];
  let itemIndex = 0;
  const txt = (node) => {
    if (node === null || node === undefined) return '';
    if (typeof node === 'string' || typeof node === 'number') return String(node);
    if (Array.isArray(node)) return node.map(txt).join(node.length > 1 ? '\n' : '');
    let s = '';
    for (const [k, v] of Object.entries(node)) {
      if (k.startsWith('@_')) continue;
      if (k === '#text') s += v;
      else if (k === 'br') s += '\n';
      else s += (k === 'p' ? '\n' : '') + txt(v);
    }
    return s;
  };
  const walk = (body, ozParts, titles) => {
    for (const c of body?.BoQCtgy || []) {
      const label = txt(c.LblTx).replace(/\s+/g, ' ').trim();
      walk(c.BoQBody, [...ozParts, c['@_RNoPart']], [...titles, label]);
    }
    const list = body?.Itemlist;
    for (const it of list?.Item || []) {
      const idx = itemIndex++;
      const desc = it.Description || {};
      const outline = txt(desc.CompleteText?.OutlineText ?? desc.OutlineText).replace(/\s+/g, ' ').trim();
      const detail = txt(desc.CompleteText?.DetailTxt ?? desc.DetailTxt).replace(/\n{2,}/g, '\n').trim();
      const qtyStr = String(it.Qty ?? '').trim();
      const qty = qtyStr === '' ? null : Number.isFinite(Number(qtyStr)) ? Number(qtyStr) : parseNum(qtyStr);
      const provis = String(it.Provis ?? '').trim();
      const alnSer = Number(it.ALNSerNo ?? 0);
      const posType = provis ? 'B' : alnSer > 0 ? 'A' : 'N';
      const flags = [];
      if (it.Qty === undefined && !it.LumpSumItem) flags.push({ key: 'qty_missing', severity: 'error', msg: 'Keine Menge in GAEB-Position' });
      positions.push({ oz: [...ozParts, it['@_RNoPart']].filter(Boolean).join('.'), title_path: titles.filter(Boolean).join(' › ') || null, short_text: outline || detail.split('\n')[0] || '', long_text: detail, qty: it.LumpSumItem === 'Yes' ? 1 : qty, unit: normalizeUnit(String(it.QU ?? (it.LumpSumItem === 'Yes' ? 'psch' : '')).trim()), pos_type: posType, confidence: 0.95, flags, layout: { gaebIndex: idx } });
    }
  };
  walk(boq.BoQBody, [], []);
  const prj = gaeb.PrjInfo || {};
  return { positions, meta: { dp, project: txt(prj.NamePrj || prj.LblPrj).trim(), boqName: txt(boq.BoQInfo?.Name || boq.BoQInfo?.LblBoQ).trim() }, info: { method: 'gaeb-xml', dp } };
}

// ---------- GAEB 90 (D83/P83) – Best-Effort ----------
export function parseGaeb90(text) {
  const positions = [];
  let cur = null;
  for (const raw of text.split(/\r?\n/)) {
    const rec = raw.slice(0, 2);
    if (rec === '21') {
      const oz = raw.slice(2, 11).trim().replace(/\s+/g, '.');
      const qtyRaw = raw.slice(23, 34).trim();
      const qty = /^\d+$/.test(qtyRaw) ? Number(qtyRaw) / 1000 : parseNum(qtyRaw);
      cur = { oz, title_path: null, short_text: '', long_text: '', qty, unit: normalizeUnit(raw.slice(34, 38).trim()), pos_type: 'N', confidence: 0.5, flags: [{ key: 'gaeb90', severity: 'warn', msg: 'GAEB 90 nur eingeschränkt unterstützt – Menge/Einheit prüfen' }], layout: null };
      positions.push(cur);
    } else if (rec === '25' && cur) cur.short_text = (cur.short_text + ' ' + raw.slice(2, 72).trim()).trim();
    else if (rec === '26' && cur) cur.long_text = (cur.long_text + '\n' + raw.slice(5, 72).trimEnd()).trim();
  }
  return { positions, info: { method: 'gaeb90' } };
}
