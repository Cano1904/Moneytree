// LV: Upload/Analyse, Positionen, Kalkulation (global/Gruppe/Auswahl/Einzeln), Plausibilität, Varianten, Exporte
import { Router } from 'express';
import { all, get, insert, update, run, tx, audit, parseJsonCols, getSettings } from '../db.js';
import { saveDocument, readDocument, uid, must, bad, advanceProject, touchProject, lvSummary, loadPositions, checkPricesComplete, sendFile } from '../util.js';
import { upload, fixName } from './projects.js';
import { analyzeLv } from '../services/lvImport.js';
import { checkLv } from '../services/plausibility.js';
import { fillExcel, fillGaeb } from '../services/lvExport.js';
import { generateLvPdf, overlayOriginal } from '../services/offerPdf.js';
import { mimeFor } from '../services/extract.js';
import { rankVariants } from '../../shared/pricing.js';
import { textSimilarity, slug } from '../../shared/format.js';
import { classify } from '../../shared/groups.js';

const r = Router();
const POS_FIELDS = ['oz', 'title_path', 'short_text', 'long_text', 'qty', 'unit', 'pos_type', 'group_name', 'product_id', 'supplier_id', 'ek', 'discount_pct', 'freight', 'markup_pct', 'markup_source', 'vk_override', 'notes', 'sort'];

function runChecks(lvId) {
  const lv = get('SELECT l.id, p.mode FROM lvs l JOIN projects p ON p.id = l.project_id WHERE l.id = ?', lvId);
  const positions = all('SELECT * FROM lv_positions WHERE lv_id = ? ORDER BY sort', lvId).map(parseJsonCols);
  const res = checkLv(positions, { mode: lv.mode });
  tx(() => {
    for (const p of res.positions) run('UPDATE lv_positions SET flags = ? WHERE id = ?', JSON.stringify(p.flags), p.id);
    run('UPDATE lvs SET issues = ? WHERE id = ?', JSON.stringify(res.lvIssues), lvId);
  });
  return res;
}

function createLv({ projectId, name, format, parseMethod, parseInfo, sourceDocId, meta, positions, userId }) {
  const s = getSettings();
  return tx(() => {
    const lvId = insert('lvs', { project_id: projectId, name, format, parse_method: parseMethod, parse_info: parseInfo, source_document_id: sourceDocId, meta, global_markup: s.default_markup, min_margin: s.min_margin });
    positions.forEach((p, i) => insert('lv_positions', {
      lv_id: lvId, sort: (i + 1) * 10, oz: p.oz, title_path: p.title_path, short_text: p.short_text, long_text: p.long_text, qty: p.qty, unit: p.unit,
      pos_type: p.pos_type || 'N', group_name: p.group_name, confidence: p.confidence, flags: p.flags || [], layout: p.layout, notes: p.product_hint ? `KI-Produkthinweis: ${p.product_hint}` : p.notes,
      markup_pct: s.default_markup, markup_source: 'global',
    }));
    insert('calculations', { lv_id: lvId, scope: 'global', group_name: '', markup_pct: s.default_markup });
    audit({ userId, projectId, entity: 'lv', entityId: lvId, action: 'angelegt', details: { name, format, positions: positions.length, method: parseMethod } });
    return lvId;
  });
}
export { createLv };

r.post('/projects/:id/lvs/upload', upload.single('file'), async (req, res) => {
  const project = must(get('SELECT * FROM projects WHERE id = ?', req.params.id), 'Projekt');
  if (!req.file) throw bad('Keine Datei hochgeladen');
  const filename = fixName(req.file);
  const mime = req.file.mimetype && req.file.mimetype !== 'application/octet-stream' ? req.file.mimetype : mimeFor(filename);
  const docId = saveDocument({ buf: req.file.buffer, filename, mime, projectId: project.id, category: 'lv_original', userId: uid(req) });
  let result;
  try {
    result = await analyzeLv({ buf: req.file.buffer, filename, mime, mode: project.mode, useAi: req.body.useAi !== 'false' });
  } catch (e) {
    throw bad(`Analyse fehlgeschlagen: ${e.message}. Das Original ist in der Projektakte gespeichert.`);
  }
  const lvId = createLv({ projectId: project.id, name: req.body.name || filename.replace(/\.[^.]+$/, ''), format: result.format, parseMethod: result.info.method, parseInfo: result.info, sourceDocId: docId, meta: result.meta, positions: result.positions, userId: uid(req) });
  runChecks(lvId);
  if (result.detected) run('UPDATE projects SET detected_mode = ?, detected_confidence = ? WHERE id = ?', result.detected.mode, result.detected.confidence, project.id);
  if (result.positions.length) advanceProject(project.id, 'lv_analysiert', uid(req));
  touchProject(project.id);
  res.json({ lvId, count: result.positions.length, info: result.info, detected: result.detected, projectMode: project.mode });
});

r.post('/projects/:id/lvs', (req, res) => {
  must(get('SELECT id FROM projects WHERE id = ?', req.params.id), 'Projekt');
  const lvId = createLv({ projectId: Number(req.params.id), name: req.body.name || 'Manuelles LV', format: req.body.format || 'manuell', parseMethod: 'manuell', parseInfo: {}, positions: req.body.positions || [], userId: uid(req) });
  if (req.body.positions?.length) runChecks(lvId);
  res.json({ lvId });
});

r.get('/lvs/:id', (req, res) => {
  const s = must(lvSummary(req.params.id), 'LV');
  s.calculations = all('SELECT * FROM calculations WHERE lv_id = ?', s.lv.id);
  s.project = get('SELECT id, name, mode, detected_mode, detected_confidence, status FROM projects WHERE id = ?', s.lv.project_id);
  s.source = s.lv.source_document_id ? get('SELECT id, filename, mime FROM documents WHERE id = ?', s.lv.source_document_id) : null;
  res.json(s);
});

r.patch('/lvs/:id', (req, res) => {
  update('lvs', req.params.id, req.body, ['name', 'min_margin']);
  res.json(get('SELECT * FROM lvs WHERE id = ?', req.params.id));
});
r.delete('/lvs/:id', (req, res) => {
  const lv = must(get('SELECT * FROM lvs WHERE id = ?', req.params.id), 'LV');
  run('DELETE FROM lvs WHERE id = ?', lv.id);
  audit({ userId: uid(req), projectId: lv.project_id, entity: 'lv', entityId: lv.id, action: 'gelöscht', details: { name: lv.name } });
  res.json({ ok: true });
});

// ---------- Positionen ----------
r.post('/lvs/:id/positions', (req, res) => {
  const lv = must(get('SELECT * FROM lvs WHERE id = ?', req.params.id), 'LV');
  const s = getSettings();
  const maxSort = get('SELECT MAX(sort) m FROM lv_positions WHERE lv_id = ?', lv.id).m || 0;
  const b = req.body;
  const group = b.group_name ?? classify(b.short_text || '', undefined, b.long_text || '').group;
  const id = insert('lv_positions', { lv_id: lv.id, sort: b.sort ?? maxSort + 10, oz: b.oz, title_path: b.title_path, short_text: b.short_text || 'Neue Position', long_text: b.long_text, qty: b.qty ?? null, unit: b.unit, pos_type: b.pos_type || 'N', group_name: group, markup_pct: lv.global_markup ?? s.default_markup, markup_source: 'global', flags: [] });
  audit({ userId: uid(req), projectId: lv.project_id, entity: 'position', entityId: id, action: 'angelegt' });
  runChecks(lv.id);
  res.json({ id });
});

r.patch('/positions/:id', (req, res) => {
  const p = must(get('SELECT p.*, l.project_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE p.id = ?', req.params.id), 'Position');
  const b = { ...req.body };
  if ('markup_pct' in b && b.markup_source === undefined) b.markup_source = 'manuell';
  if ('ek' in b && b.price_source === undefined) { b.price_source = b.ek === null ? null : 'manuell'; b.price_date = new Date().toISOString().slice(0, 10); }
  const changes = {};
  for (const k of [...POS_FIELDS, 'price_source', 'price_date']) if (k in b && String(b[k] ?? '') !== String(p[k] ?? '')) changes[k] = { from: p[k], to: b[k] };
  update('lv_positions', p.id, b, [...POS_FIELDS, 'price_source', 'price_date']);
  if (Object.keys(changes).length) audit({ userId: uid(req), projectId: p.project_id, entity: 'position', entityId: p.id, action: 'geändert', details: { oz: p.oz, ...changes } });
  if (['qty', 'unit', 'short_text', 'long_text', 'group_name', 'pos_type'].some((k) => k in changes)) runChecks(p.lv_id);
  if ('ek' in changes && b.ek !== null) {
    insert('prices', { product_id: b.product_id ?? p.product_id, supplier_id: b.supplier_id ?? p.supplier_id, project_id: p.project_id, position_id: p.id, text: p.short_text, group_name: p.group_name, price: b.ek, unit: p.unit, qty: p.qty, source: 'manuell' });
    checkPricesComplete(p.project_id, uid(req));
  }
  touchProject(p.project_id);
  const s = getSettings();
  const lv = get('SELECT min_margin FROM lvs WHERE id = ?', p.lv_id);
  res.json(loadPositions(p.lv_id, lv.min_margin ?? s.min_margin).find((x) => x.id === p.id));
});

r.delete('/positions/:id', (req, res) => {
  const p = must(get('SELECT p.*, l.project_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE p.id = ?', req.params.id), 'Position');
  run('DELETE FROM lv_positions WHERE id = ?', p.id);
  audit({ userId: uid(req), projectId: p.project_id, entity: 'position', entityId: p.id, action: 'gelöscht', details: { oz: p.oz, text: p.short_text } });
  runChecks(p.lv_id);
  res.json({ ok: true });
});

r.post('/positions/:id/flags/:key', (req, res) => {
  const p = must(parseJsonCols(get('SELECT p.*, l.project_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE p.id = ?', req.params.id)), 'Position');
  const flags = (p.flags || []).map((f) => (f.key === req.params.key ? { ...f, resolved: req.body.resolved !== false } : f));
  run('UPDATE lv_positions SET flags = ? WHERE id = ?', JSON.stringify(flags), p.id);
  audit({ userId: uid(req), projectId: p.project_id, entity: 'position', entityId: p.id, action: req.body.resolved !== false ? 'Hinweis geprüft' : 'Hinweis wieder offen', details: { oz: p.oz, key: req.params.key } });
  res.json({ flags });
});

/** Massenänderung (Mehrfachauswahl): Aufschlag, Lieferant, Warengruppe, Produkt, Rabatt, Fracht. */
r.post('/lvs/:id/bulk', (req, res) => {
  const lv = must(get('SELECT * FROM lvs WHERE id = ?', req.params.id), 'LV');
  const ids = (req.body.ids || []).map(Number).filter(Boolean);
  if (!ids.length) throw bad('Keine Positionen ausgewählt');
  const set = {};
  for (const k of ['markup_pct', 'supplier_id', 'group_name', 'product_id', 'discount_pct', 'freight', 'pos_type']) if (k in (req.body.set || {})) set[k] = req.body.set[k];
  if (!Object.keys(set).length) throw bad('Keine Änderung angegeben');
  if ('markup_pct' in set) set.markup_source = 'manuell';
  const before = all(`SELECT id, oz, ${Object.keys(set).join(', ')} FROM lv_positions WHERE lv_id = ? AND id IN (${ids.map(() => '?').join(',')})`, lv.id, ...ids);
  tx(() => { for (const p of before) update('lv_positions', p.id, set); });
  const summary = Object.keys(set).filter((k) => k !== 'markup_source').map((k) => {
    const olds = [...new Set(before.map((p) => p[k]))];
    return { field: k, from: olds.length === 1 ? olds[0] : 'verschieden', to: set[k] };
  });
  audit({ userId: uid(req), projectId: lv.project_id, entity: 'lv', entityId: lv.id, action: 'Massenänderung', details: { count: before.length, summary, oz: before.map((p) => p.oz).slice(0, 50) } });
  if ('group_name' in set) runChecks(lv.id);
  res.json({ changed: before.map((p) => p.id), summary });
});

/** Aufschlag global oder je Warengruppe. Manuelle Einzelwerte bleiben, außer overwriteManual. */
r.post('/lvs/:id/markup', (req, res) => {
  const lv = must(get('SELECT * FROM lvs WHERE id = ?', req.params.id), 'LV');
  const { scope = 'global', group_name: group, markup_pct: m, overwriteManual = false } = req.body;
  if (m === null || m === undefined || !Number.isFinite(Number(m))) throw bad('Aufschlag fehlt');
  const where = ['lv_id = ?'], params = [lv.id];
  if (scope === 'gruppe') { if (!group) throw bad('Warengruppe fehlt'); where.push('group_name = ?'); params.push(group); }
  if (!overwriteManual) where.push(scope === 'gruppe' ? "markup_source IN ('global','gruppe')" : "markup_source = 'global'");
  const affected = all(`SELECT id, markup_pct FROM lv_positions WHERE ${where.join(' AND ')} AND (markup_pct IS NULL OR markup_pct != ?)`, ...params, Number(m));
  tx(() => {
    run(`UPDATE lv_positions SET markup_pct = ?, markup_source = ?, updated_at = CURRENT_TIMESTAMP WHERE ${where.join(' AND ')}`, Number(m), scope === 'gruppe' ? 'gruppe' : 'global', ...params);
    if (scope === 'global') run('UPDATE lvs SET global_markup = ? WHERE id = ?', Number(m), lv.id);
    run(`INSERT INTO calculations (lv_id, scope, group_name, markup_pct) VALUES (?,?,?,?) ON CONFLICT(lv_id, scope, group_name) DO UPDATE SET markup_pct = excluded.markup_pct, updated_at = CURRENT_TIMESTAMP`, lv.id, scope, scope === 'gruppe' ? group : '', Number(m));
  });
  const skipped = scope === 'global' && !overwriteManual ? get("SELECT COUNT(*) n FROM lv_positions WHERE lv_id = ? AND markup_source != 'global'", lv.id).n : 0;
  audit({ userId: uid(req), projectId: lv.project_id, entity: 'lv', entityId: lv.id, action: scope === 'gruppe' ? `Aufschlag Gruppe ${group}` : 'Aufschlag global', details: { markup_pct: Number(m), changed: affected.length, skipped, overwriteManual } });
  advanceProject(lv.project_id, 'kalkulation', uid(req));
  res.json({ changed: affected.map((a) => a.id), skipped });
});

r.post('/lvs/:id/check', (req, res) => {
  must(get('SELECT id FROM lvs WHERE id = ?', req.params.id), 'LV');
  const out = runChecks(req.params.id);
  res.json({ issues: out.lvIssues, flagged: out.positions.filter((p) => p.flags.some((f) => !f.resolved && f.severity !== 'info')).length });
});

r.post('/lvs/:id/resort', (req, res) => {
  const ids = req.body.ids || [];
  tx(() => ids.forEach((id, i) => run('UPDATE lv_positions SET sort = ? WHERE id = ? AND lv_id = ?', (i + 1) * 10, id, req.params.id)));
  res.json({ ok: true });
});

// ---------- Lieferantenvorschläge & Details ----------
export function suggestSuppliers(pos, suppliers) {
  const hist = all('SELECT supplier_id, price, date, unit FROM prices WHERE supplier_id IS NOT NULL AND (product_id = ? OR group_name = ?) ORDER BY date DESC LIMIT 200', pos.product_id ?? -1, pos.group_name ?? '');
  return suppliers.map((s) => {
    const groups = Array.isArray(s.groups) ? s.groups : [];
    const reasons = [];
    let score = 0;
    if (pos.group_name && groups.includes(pos.group_name)) { score += 3; reasons.push(`führt ${pos.group_name}`); }
    if (s.preferred) { score += 1; reasons.push('bevorzugter Lieferant'); }
    const h = hist.filter((x) => x.supplier_id === s.id);
    if (h.length) { score += 1.5; reasons.push(`${h.length} frühere Preise`); }
    const textHit = textSimilarity(pos.short_text, `${s.notes || ''} ${groups.join(' ')}`);
    if (textHit > 0.3) { score += 1; reasons.push('passende Notizen'); }
    return { supplier_id: s.id, name: s.name, score, reasons, delivery_days: s.delivery_days };
  }).filter((x) => x.score > 0).sort((a, b) => b.score - a.score).slice(0, 4);
}

r.post('/lvs/:id/suggest-suppliers', (req, res) => {
  const lv = must(get('SELECT * FROM lvs WHERE id = ?', req.params.id), 'LV');
  const suppliers = all('SELECT * FROM suppliers').map(parseJsonCols);
  const positions = all("SELECT * FROM lv_positions WHERE lv_id = ? AND pos_type != 'T' AND supplier_id IS NULL", lv.id);
  const suggestions = positions.map((p) => ({ position_id: p.id, oz: p.oz, short_text: p.short_text, group_name: p.group_name, options: suggestSuppliers(p, suppliers) })).filter((x) => x.options.length);
  if (req.body.apply) {
    tx(() => { for (const s of suggestions) run('UPDATE lv_positions SET supplier_id = ? WHERE id = ?', s.options[0].supplier_id, s.position_id); });
    audit({ userId: uid(req), projectId: lv.project_id, entity: 'lv', entityId: lv.id, action: 'Lieferanten zugeordnet (Vorschlag übernommen)', details: { count: suggestions.length } });
  }
  res.json({ suggestions, unassigned: positions.length - suggestions.length });
});

r.get('/positions/:id/detail', (req, res) => {
  const p = must(parseJsonCols(get('SELECT p.*, l.project_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE p.id = ?', req.params.id)), 'Position');
  const suppliers = all('SELECT * FROM suppliers').map(parseJsonCols);
  const preferred = suppliers.filter((s) => s.preferred).map((s) => s.id);
  const variants = all(`SELECT qi.*, q.supplier_id, q.valid_until, q.source, q.created_at quote_date, q.quote_no, s.name supplier_name FROM supplier_quote_items qi JOIN supplier_quotes q ON q.id = qi.quote_id LEFT JOIN suppliers s ON s.id = q.supplier_id WHERE qi.position_id = ? ORDER BY qi.id DESC`, p.id)
    .map((v) => ({ ...v, price: v.price === null ? null : v.price * (v.factor || 1) }));
  // Preishistorie: gleiches Produkt oder ähnlicher Text
  const key = slug(p.short_text).split(' ').filter((t) => t.length > 3).slice(0, 3);
  const candidates = all(`SELECT pr.*, s.name supplier_name, pj.name project_name FROM prices pr LEFT JOIN suppliers s ON s.id = pr.supplier_id LEFT JOIN projects pj ON pj.id = pr.project_id
    WHERE (pr.position_id IS NULL OR pr.position_id != ?) AND (pr.product_id = ? ${key.map(() => 'OR pr.text LIKE ?').join(' ')}) ORDER BY pr.date DESC, pr.id DESC LIMIT 60`, p.id, p.product_id ?? -1, ...key.map((k) => `%${k}%`));
  const history = candidates.map((h) => ({ ...h, sim: h.product_id && h.product_id === p.product_id ? 1 : textSimilarity(h.text, p.short_text) })).filter((h) => h.sim >= 0.45).slice(0, 15);
  const requests = all('SELECT r.id, r.status, r.channel, r.sent_at, s.name supplier_name FROM supplier_request_items i JOIN supplier_requests r ON r.id = i.request_id JOIN suppliers s ON s.id = r.supplier_id WHERE i.position_id = ?', p.id);
  res.json({ position: p, variants: rankVariants(variants, { preferredSupplierIds: preferred }), history, requests, suggestions: suggestSuppliers(p, suppliers) });
});

r.post('/positions/:id/select-variant', (req, res) => {
  const p = must(get('SELECT p.*, l.project_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE p.id = ?', req.params.id), 'Position');
  const v = must(get('SELECT qi.*, q.supplier_id, q.source FROM supplier_quote_items qi JOIN supplier_quotes q ON q.id = qi.quote_id WHERE qi.id = ?', req.body.quote_item_id), 'Variante');
  update('lv_positions', p.id, { ek: v.price * (v.factor || 1), discount_pct: v.discount_pct || 0, freight: v.freight || 0, supplier_id: v.supplier_id, quote_item_id: v.id, price_source: v.source || 'angebot', price_date: new Date().toISOString().slice(0, 10) });
  audit({ userId: uid(req), projectId: p.project_id, entity: 'position', entityId: p.id, action: 'Variante gewählt', details: { oz: p.oz, from: { ek: p.ek, supplier_id: p.supplier_id }, to: { ek: v.price * (v.factor || 1), supplier_id: v.supplier_id } } });
  checkPricesComplete(p.project_id, uid(req));
  res.json({ ok: true });
});

// ---------- Exporte ----------
r.get('/lvs/:id/export/:kind', async (req, res) => {
  const s = getSettings();
  const sum = must(lvSummary(req.params.id), 'LV');
  const { lv, positions, minMargin } = sum;
  const project = get('SELECT p.*, c.name customer_name FROM projects p LEFT JOIN customers c ON c.id = p.customer_id WHERE p.id = ?', lv.project_id);
  const base = `${project.number || 'Projekt'}_${lv.name}`.replace(/[^\w\-äöüÄÖÜß ]+/g, '_');
  const kind = req.params.kind;
  if (kind === 'xlsx') {
    if (lv.format === 'excel' && lv.source_document_id && lv.meta?.columns) {
      const { buf } = readDocument(lv.source_document_id);
      return sendFile(res, await fillExcel({ originalBuf: buf, meta: lv.meta, positions, minMargin }), `${base}_Angebot.xlsx`, mimeFor('x.xlsx'));
    }
    const ExcelJS = (await import('exceljs')).default;
    const wb = new ExcelJS.Workbook();
    const ws = wb.addWorksheet('Angebot');
    ws.addRow(['OZ', 'Kurztext', 'Menge', 'Einheit', 'EP', 'GP', 'Art']).font = { bold: true };
    for (const p of positions) ws.addRow([p.oz, p.short_text, p.qty, p.unit, p.calc.vk, p.pos_type === 'N' ? p.calc.total : null, p.pos_type]);
    ws.addRow([]); ws.addRow(['', 'Summe netto', '', '', '', sum.totals.net]).font = { bold: true };
    ws.columns.forEach((c, i) => { c.width = [12, 60, 10, 8, 12, 14, 6][i]; if (i >= 4 && i <= 5) c.numFmt = '#,##0.00'; });
    return sendFile(res, Buffer.from(await wb.xlsx.writeBuffer()), `${base}_Angebot.xlsx`, mimeFor('x.xlsx'));
  }
  if (kind === 'kalkulation') {
    const ExcelJS = (await import('exceljs')).default;
    const wb = new ExcelJS.Workbook();
    const ws = wb.addWorksheet('Kalkulation');
    ws.addRow(['OZ', 'Kurztext', 'Gruppe', 'Lieferant', 'Menge', 'ME', 'EK', 'Rabatt %', 'Fracht/ME', 'Einstand', 'Aufschlag %', 'VK', 'GP', 'DB gesamt', 'Marge %', 'Preisquelle']).font = { bold: true };
    for (const p of positions) ws.addRow([p.oz, p.short_text, p.group_name, p.supplier_name, p.qty, p.unit, p.ek, p.discount_pct, p.freight, p.calc.einstand, p.markup_pct, p.calc.vk, p.calc.total, p.calc.db_total, p.calc.margin_pct, p.price_source]);
    ws.addRow([]); ws.addRow(['', 'Summe', '', '', '', '', '', '', '', sum.totals.einstand, '', '', sum.totals.net, sum.totals.db, sum.totals.margin_pct]).font = { bold: true };
    ws.columns.forEach((c, i) => { c.width = i === 1 ? 50 : 12; });
    return sendFile(res, Buffer.from(await wb.xlsx.writeBuffer()), `${base}_Kalkulation_intern.xlsx`, mimeFor('x.xlsx'));
  }
  if (kind === 'gaeb') {
    if (lv.format !== 'gaeb-xml' || !lv.source_document_id) throw bad('GAEB-Export nur für LVs, die als GAEB-XML importiert wurden');
    const { buf } = readDocument(lv.source_document_id);
    const { xml } = fillGaeb({ originalXml: buf.toString('utf8'), positions, minMargin });
    return sendFile(res, Buffer.from(xml, 'utf8'), `${base}.X84`, 'application/xml');
  }
  const offer = { number: req.query.number || `${project.number}-ENTWURF`, date: new Date() };
  if (kind === 'pdf') {
    const buf = await generateLvPdf({ project, customer: { name: project.customer_name }, offer, positions, settings: s, minMargin, longText: req.query.long !== '0' });
    return sendFile(res, buf, `${base}_Angebots-LV.pdf`, 'application/pdf');
  }
  if (kind === 'overlay') {
    if (lv.format !== 'pdf' || !lv.source_document_id || !lv.meta?.hasText) throw bad('Preise ins Original eintragen ist nur bei text-basierten PDF-LVs möglich');
    const { buf } = readDocument(lv.source_document_id);
    const out = await overlayOriginal({ originalBuf: buf, positions, settings: s, minMargin, offer });
    return sendFile(res, out.buf, `${base}_Original_mit_Preisen.pdf`, 'application/pdf');
  }
  throw bad('Unbekannter Export');
});

export default r;
