// Lieferanten, Ansprechpartner, Anfragen (Mail/Telefon), Lieferantenangebote, Preishistorie
import { Router } from 'express';
import { all, get, insert, update, run, tx, audit, parseJsonCols, getSettings } from '../db.js';
import { saveDocument, uid, must, bad, advanceProject, touchProject, checkPricesComplete, sendFile } from '../util.js';
import { upload, fixName } from './projects.js';
import { supplierRequestMail, buildEml } from '../services/mail.js';
import { analyzeQuote } from '../services/quoteImport.js';
import { mimeFor } from '../services/extract.js';
import { round } from '../../shared/format.js';

const r = Router();
const SUP_FIELDS = ['name', 'phone', 'email', 'address', 'groups', 'delivery_days', 'preferred', 'notes'];

// ---------- Lieferanten ----------
r.get('/suppliers', (req, res) => {
  const q = `%${req.query.q || ''}%`;
  const rows = all(`SELECT s.*, (SELECT COUNT(*) FROM supplier_requests r WHERE r.supplier_id = s.id AND r.status IN ('angefragt','ausstehend','rueckfrage')) open_requests,
      (SELECT COUNT(*) FROM supplier_quotes q WHERE q.supplier_id = s.id) quotes
    FROM suppliers s WHERE s.name LIKE ? OR s.groups LIKE ? OR s.notes LIKE ? ORDER BY s.preferred DESC, s.name`, q, q, q).map(parseJsonCols);
  const contacts = all('SELECT * FROM supplier_contacts');
  for (const s of rows) s.contacts = contacts.filter((c) => c.supplier_id === s.id);
  if (req.query.group) return res.json(rows.filter((s) => s.groups?.includes(req.query.group)));
  res.json(rows);
});
r.post('/suppliers', (req, res) => {
  if (!req.body.name?.trim()) throw bad('Firmenname fehlt');
  const id = insert('suppliers', { ...Object.fromEntries(SUP_FIELDS.map((k) => [k, req.body[k]])), groups: req.body.groups || [] });
  for (const c of req.body.contacts || []) if (c.name) insert('supplier_contacts', { supplier_id: id, name: c.name, role: c.role, phone: c.phone, email: c.email });
  audit({ userId: uid(req), entity: 'supplier', entityId: id, action: 'angelegt' });
  res.json({ id });
});
r.get('/suppliers/:id', (req, res) => {
  const s = must(parseJsonCols(get('SELECT * FROM suppliers WHERE id = ?', req.params.id)), 'Lieferant');
  s.contacts = all('SELECT * FROM supplier_contacts WHERE supplier_id = ?', s.id);
  s.quotes = all('SELECT q.*, p.name project_name, (SELECT COUNT(*) FROM supplier_quote_items i WHERE i.quote_id = q.id) items FROM supplier_quotes q LEFT JOIN projects p ON p.id = q.project_id WHERE q.supplier_id = ? ORDER BY q.id DESC', s.id);
  s.requests = all('SELECT r.*, p.name project_name FROM supplier_requests r JOIN projects p ON p.id = r.project_id WHERE r.supplier_id = ? ORDER BY r.id DESC', s.id);
  s.prices = all('SELECT pr.*, p.name project_name FROM prices pr LEFT JOIN projects p ON p.id = pr.project_id WHERE pr.supplier_id = ? ORDER BY pr.date DESC, pr.id DESC LIMIT 200', s.id);
  res.json(s);
});
r.patch('/suppliers/:id', (req, res) => {
  update('suppliers', req.params.id, req.body, SUP_FIELDS);
  if (Array.isArray(req.body.contacts)) tx(() => {
    const keep = req.body.contacts.filter((c) => c.id).map((c) => c.id);
    run(`DELETE FROM supplier_contacts WHERE supplier_id = ? ${keep.length ? `AND id NOT IN (${keep.map(Number).join(',')})` : ''}`, req.params.id);
    for (const c of req.body.contacts) {
      if (!c.name) continue;
      if (c.id) update('supplier_contacts', c.id, c, ['name', 'role', 'phone', 'email']);
      else insert('supplier_contacts', { supplier_id: Number(req.params.id), name: c.name, role: c.role, phone: c.phone, email: c.email });
    }
  });
  res.json({ ok: true });
});
r.delete('/suppliers/:id', (req, res) => { run('DELETE FROM suppliers WHERE id = ?', req.params.id); res.json({ ok: true }); });

// ---------- Anfragen ----------
function requestPositions(id) {
  return all(`SELECT p.*, l.name lv_name FROM supplier_request_items i JOIN lv_positions p ON p.id = i.position_id JOIN lvs l ON l.id = p.lv_id WHERE i.request_id = ? ORDER BY l.id, p.sort`, id).map(parseJsonCols);
}
function buildMail(reqRow, user) {
  const project = get('SELECT * FROM projects WHERE id = ?', reqRow.project_id);
  const customer = project.customer_id ? get('SELECT * FROM customers WHERE id = ?', project.customer_id) : null;
  const supplier = get('SELECT * FROM suppliers WHERE id = ?', reqRow.supplier_id);
  const contact = reqRow.contact_id ? get('SELECT * FROM supplier_contacts WHERE id = ?', reqRow.contact_id) : get('SELECT * FROM supplier_contacts WHERE supplier_id = ? LIMIT 1', reqRow.supplier_id);
  const answerBy = new Date(); answerBy.setDate(answerBy.getDate() + 3);
  return supplierRequestMail({ project, customer, supplier, contact, positions: requestPositions(reqRow.id), desiredDate: reqRow.desired_date, answerBy, user, settings: getSettings() });
}
const userOf = (req) => (uid(req) ? get('SELECT * FROM users WHERE id = ?', uid(req)) : null);

r.get('/requests', (req, res) => {
  const where = [], p = [];
  if (req.query.status) { where.push(`r.status IN (${req.query.status.split(',').map(() => '?').join(',')})`); p.push(...req.query.status.split(',')); }
  if (req.query.project_id) { where.push('r.project_id = ?'); p.push(req.query.project_id); }
  if (req.query.supplier_id) { where.push('r.supplier_id = ?'); p.push(req.query.supplier_id); }
  res.json(all(`SELECT r.*, s.name supplier_name, s.phone supplier_phone, pj.name project_name, pj.number project_number,
      (SELECT COUNT(*) FROM supplier_request_items i WHERE i.request_id = r.id) items,
      (SELECT COUNT(*) FROM supplier_request_items i JOIN lv_positions x ON x.id = i.position_id WHERE i.request_id = r.id AND x.ek IS NULL) missing
    FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id JOIN projects pj ON pj.id = r.project_id ${where.length ? `WHERE ${where.join(' AND ')}` : ''} ORDER BY r.updated_at DESC`, ...p));
});

function createOrExtendRequest({ projectId, supplierId, positionIds, channel = 'mail', contactId = null, desiredDate = null, userId, user }) {
  return tx(() => {
    let reqRow = get("SELECT * FROM supplier_requests WHERE project_id = ? AND supplier_id = ? AND status = 'vorbereitet' ORDER BY id DESC LIMIT 1", projectId, supplierId);
    if (!reqRow) {
      const id = insert('supplier_requests', { project_id: projectId, supplier_id: supplierId, channel, contact_id: contactId, desired_date: desiredDate, status: 'vorbereitet' });
      reqRow = get('SELECT * FROM supplier_requests WHERE id = ?', id);
      audit({ userId, projectId, entity: 'request', entityId: id, action: 'Anfrage vorbereitet', details: { supplier_id: supplierId, positions: positionIds.length } });
    }
    for (const pid of positionIds) run('INSERT OR IGNORE INTO supplier_request_items (request_id, position_id) VALUES (?, ?)', reqRow.id, pid);
    run('UPDATE lv_positions SET supplier_id = ? WHERE supplier_id IS NULL AND id IN (SELECT position_id FROM supplier_request_items WHERE request_id = ?)', supplierId, reqRow.id);
    const mail = buildMail(reqRow, user);
    update('supplier_requests', reqRow.id, { subject: mail.subject, body: mail.body, channel, contact_id: contactId ?? reqRow.contact_id });
    return reqRow.id;
  });
}

r.post('/projects/:id/requests', (req, res) => {
  must(get('SELECT id FROM projects WHERE id = ?', req.params.id), 'Projekt');
  const b = req.body;
  if (!b.supplier_id) throw bad('Lieferant fehlt');
  let ids = (b.position_ids || []).map(Number);
  if (!ids.length && b.all_assigned) ids = all("SELECT p.id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE l.project_id = ? AND p.supplier_id = ? AND p.pos_type != 'T' AND p.ek IS NULL", req.params.id, b.supplier_id).map((x) => x.id);
  if (!ids.length && b.all_assigned) return res.json({ id: null, positions: 0 });
  if (!ids.length) throw bad('Keine Positionen für die Anfrage');
  const id = createOrExtendRequest({ projectId: Number(req.params.id), supplierId: b.supplier_id, positionIds: ids, channel: b.channel, contactId: b.contact_id, desiredDate: b.desired_date, userId: uid(req), user: userOf(req) });
  res.json({ id });
});

/** Alle zugeordneten, noch nicht angefragten Positionen je Lieferant zu Anfragen bündeln. */
r.post('/projects/:id/requests/prepare-all', (req, res) => {
  const rows = all(`SELECT p.id, p.supplier_id FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE l.project_id = ? AND p.supplier_id IS NOT NULL AND p.pos_type != 'T' AND p.ek IS NULL
    AND p.id NOT IN (SELECT position_id FROM supplier_request_items)`, req.params.id);
  const bySup = new Map();
  for (const x of rows) bySup.set(x.supplier_id, [...(bySup.get(x.supplier_id) || []), x.id]);
  const created = [...bySup].map(([sid, ids]) => createOrExtendRequest({ projectId: Number(req.params.id), supplierId: sid, positionIds: ids, userId: uid(req), user: userOf(req) }));
  res.json({ requests: created, positions: rows.length });
});

r.get('/requests/:id', (req, res) => {
  const q = must(get('SELECT r.*, s.name supplier_name, s.phone supplier_phone, s.email supplier_email, p.name project_name FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id JOIN projects p ON p.id = r.project_id WHERE r.id = ?', req.params.id), 'Anfrage');
  q.positions = requestPositions(q.id);
  q.contacts = all('SELECT * FROM supplier_contacts WHERE supplier_id = ?', q.supplier_id);
  q.contact = q.contact_id ? q.contacts.find((c) => c.id === q.contact_id) : q.contacts[0] || null;
  q.to = q.contact?.email || q.supplier_email || '';
  res.json(q);
});

r.patch('/requests/:id', (req, res) => {
  const q = must(get('SELECT * FROM supplier_requests WHERE id = ?', req.params.id), 'Anfrage');
  const b = { ...req.body };
  if (b.status === 'angefragt' && !q.sent_at) b.sent_at = new Date().toISOString();
  update('supplier_requests', q.id, b, ['status', 'subject', 'body', 'channel', 'contact_id', 'desired_date', 'call_notes', 'call_result', 'sent_at']);
  if (b.status && b.status !== q.status) {
    audit({ userId: uid(req), projectId: q.project_id, entity: 'request', entityId: q.id, action: `Anfrage: ${b.status}`, details: { from: q.status } });
    if (['angefragt', 'ausstehend'].includes(b.status)) advanceProject(q.project_id, 'anfrage_laeuft', uid(req));
  }
  if (b.call_notes && b.call_notes !== q.call_notes) insert('notes', { project_id: q.project_id, kind: 'telefon', text: `Telefonat ${get('SELECT name FROM suppliers WHERE id = ?', q.supplier_id).name}: ${b.call_notes}${b.call_result ? `\nErgebnis: ${b.call_result}` : ''}`, created_by: uid(req) });
  touchProject(q.project_id);
  res.json(get('SELECT * FROM supplier_requests WHERE id = ?', q.id));
});

r.post('/requests/:id/regenerate', (req, res) => {
  const q = must(get('SELECT * FROM supplier_requests WHERE id = ?', req.params.id), 'Anfrage');
  const mail = buildMail(q, userOf(req));
  update('supplier_requests', q.id, { subject: mail.subject, body: mail.body });
  res.json(mail);
});
r.post('/requests/:id/items', (req, res) => {
  const q = must(get('SELECT * FROM supplier_requests WHERE id = ?', req.params.id), 'Anfrage');
  for (const pid of req.body.position_ids || []) run('INSERT OR IGNORE INTO supplier_request_items (request_id, position_id) VALUES (?, ?)', q.id, pid);
  const mail = buildMail(q, userOf(req));
  update('supplier_requests', q.id, { subject: mail.subject, body: mail.body });
  res.json({ ok: true });
});
r.delete('/requests/:id/items/:pid', (req, res) => {
  run('DELETE FROM supplier_request_items WHERE request_id = ? AND position_id = ?', req.params.id, req.params.pid);
  const q = get('SELECT * FROM supplier_requests WHERE id = ?', req.params.id);
  if (q) { const mail = buildMail(q, userOf(req)); update('supplier_requests', q.id, { subject: mail.subject, body: mail.body }); }
  res.json({ ok: true });
});
r.delete('/requests/:id', (req, res) => {
  const q = must(get('SELECT * FROM supplier_requests WHERE id = ?', req.params.id), 'Anfrage');
  run('DELETE FROM supplier_requests WHERE id = ?', q.id);
  audit({ userId: uid(req), projectId: q.project_id, entity: 'request', entityId: q.id, action: 'Anfrage gelöscht' });
  res.json({ ok: true });
});
r.get('/requests/:id/eml', (req, res) => {
  const q = must(get('SELECT r.*, s.email supplier_email FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id WHERE r.id = ?', req.params.id), 'Anfrage');
  const c = q.contact_id ? get('SELECT email FROM supplier_contacts WHERE id = ?', q.contact_id) : get('SELECT email FROM supplier_contacts WHERE supplier_id = ? AND email IS NOT NULL LIMIT 1', q.supplier_id);
  const eml = buildEml({ to: c?.email || q.supplier_email || '', subject: q.subject, body: q.body, from: getSettings().company_email });
  // Anfrage als Korrespondenz archivieren
  saveDocument({ buf: Buffer.from(eml), filename: `Anfrage_${q.id}.eml`, mime: 'message/rfc822', projectId: q.project_id, category: 'anfrage', userId: uid(req) });
  sendFile(res, Buffer.from(eml), `Anfrage_${q.id}.eml`, 'message/rfc822');
});

// ---------- Lieferantenangebote ----------
r.post('/projects/:id/quotes/analyze', upload.single('file'), async (req, res) => {
  must(get('SELECT id FROM projects WHERE id = ?', req.params.id), 'Projekt');
  if (!req.file) throw bad('Keine Datei');
  const filename = fixName(req.file);
  const mime = req.file.mimetype && req.file.mimetype !== 'application/octet-stream' ? req.file.mimetype : mimeFor(filename);
  const docId = saveDocument({ buf: req.file.buffer, filename, mime, projectId: Number(req.params.id), category: 'lieferantenangebot', userId: uid(req) });
  const positions = req.body.request_id
    ? requestPositions(req.body.request_id)
    : all("SELECT p.* FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE l.project_id = ? AND p.pos_type != 'T'", req.params.id);
  let draft;
  try {
    draft = await analyzeQuote({ buf: req.file.buffer, filename, mime, positions, useAi: req.body.useAi !== 'false' });
  } catch (e) {
    draft = { items: [], warnings: [`Automatische Auswertung fehlgeschlagen (${e.message}) – Preise bitte manuell erfassen.`], method: 'manuell' };
  }
  if (!req.body.supplier_id && draft.supplier_name) {
    const s = get('SELECT id FROM suppliers WHERE name LIKE ?', `%${draft.supplier_name.split(/\s+/)[0]}%`);
    if (s) draft.supplier_id = s.id;
  }
  res.json({ ...draft, document_id: docId, positions: positions.map((p) => ({ id: p.id, oz: p.oz, short_text: p.short_text, qty: p.qty, unit: p.unit, ek: p.ek })) });
});

/** Bestätigte Preise übernehmen (aus Angebot oder Telefonat). */
r.post('/projects/:id/quotes', (req, res) => {
  const projectId = Number(req.params.id);
  const b = req.body;
  if (!b.supplier_id) throw bad('Lieferant fehlt');
  const items = (b.items || []).filter((i) => i.price !== null && i.price !== undefined && i.price !== '');
  if (!items.length) throw bad('Keine Preise zum Übernehmen');
  const result = tx(() => {
    const qid = insert('supplier_quotes', { project_id: projectId, supplier_id: b.supplier_id, request_id: b.request_id || null, document_id: b.document_id || null, source: b.source || 'angebot', quote_no: b.quote_no, valid_until: b.valid_until || null, freight_total: b.freight_total || null, discount_pct: b.discount_pct || null, delivery_time: b.delivery_time, notes: b.notes, created_by: uid(req) });
    // Pauschalfracht nach Warenwert auf übernommene Positionen verteilen
    const applied = items.filter((i) => i.apply && i.position_id);
    const posById = new Map(applied.map((i) => [i.position_id, get('SELECT * FROM lv_positions WHERE id = ?', i.position_id)]));
    const value = applied.reduce((s, i) => s + Number(i.price) * (Number(i.factor) || 1) * (posById.get(i.position_id)?.qty || 0), 0);
    let changed = 0;
    for (const i of items) {
      const factor = Number(i.factor) || 1;
      const pos = i.position_id ? posById.get(i.position_id) || get('SELECT * FROM lv_positions WHERE id = ?', i.position_id) : null;
      let freight = Number(i.freight) || 0;
      if (b.distribute_freight && b.freight_total && i.apply && pos?.qty && value > 0) freight = round((Number(b.freight_total) * (Number(i.price) * factor * pos.qty / value)) / pos.qty, 4);
      const disc = i.discount_pct ?? b.discount_pct ?? 0;
      const qiId = insert('supplier_quote_items', { quote_id: qid, position_id: i.position_id || null, text: i.text, article_no: i.article_no, qty: i.qty, unit: i.unit, price: Number(i.price), discount_pct: disc, freight, factor, delivery_days: i.delivery_days ?? null, delivery_text: i.delivery_text || b.delivery_time });
      insert('prices', { product_id: pos?.product_id, supplier_id: b.supplier_id, project_id: projectId, position_id: pos?.id, text: pos && i.text && i.text !== pos.short_text ? `${pos.short_text} | ${i.text}` : pos?.short_text || i.text, group_name: pos?.group_name, price: Number(i.price) * factor, unit: pos?.unit || i.unit, qty: pos?.qty ?? i.qty, valid_until: b.valid_until || null, source: b.source || 'angebot', quote_id: qid });
      if (i.apply && pos) {
        update('lv_positions', pos.id, { ek: round(Number(i.price) * factor, 4), discount_pct: disc, freight, supplier_id: b.supplier_id, quote_item_id: qiId, price_source: b.source || 'angebot', price_date: new Date().toISOString().slice(0, 10) });
        audit({ userId: uid(req), projectId, entity: 'position', entityId: pos.id, action: 'Preis übernommen', details: { oz: pos.oz, from: pos.ek, to: round(Number(i.price) * factor, 4), quote: qid } });
        changed++;
      }
    }
    if (b.request_id) {
      const missing = get('SELECT COUNT(*) n FROM supplier_request_items i JOIN lv_positions p ON p.id = i.position_id WHERE i.request_id = ? AND p.ek IS NULL', b.request_id).n;
      update('supplier_requests', b.request_id, { status: missing ? 'preis_erhalten' : 'vollstaendig' });
    }
    audit({ userId: uid(req), projectId, entity: 'quote', entityId: qid, action: b.source === 'telefon' ? 'Telefonpreise erfasst' : 'Lieferantenangebot übernommen', details: { items: items.length, applied: changed } });
    return { id: qid, applied: changed };
  });
  checkPricesComplete(projectId, uid(req));
  touchProject(projectId);
  res.json(result);
});

r.get('/quotes', (req, res) => {
  const where = [], p = [];
  if (req.query.supplier_id) { where.push('q.supplier_id = ?'); p.push(req.query.supplier_id); }
  if (req.query.project_id) { where.push('q.project_id = ?'); p.push(req.query.project_id); }
  res.json(all(`SELECT q.*, s.name supplier_name, pj.name project_name FROM supplier_quotes q LEFT JOIN suppliers s ON s.id = q.supplier_id LEFT JOIN projects pj ON pj.id = q.project_id ${where.length ? `WHERE ${where.join(' AND ')}` : ''} ORDER BY q.id DESC LIMIT 300`, ...p));
});
r.get('/quotes/:id', (req, res) => {
  const q = must(get('SELECT q.*, s.name supplier_name FROM supplier_quotes q LEFT JOIN suppliers s ON s.id = q.supplier_id WHERE q.id = ?', req.params.id), 'Angebot');
  q.items = all('SELECT i.*, p.oz, p.short_text position_text FROM supplier_quote_items i LEFT JOIN lv_positions p ON p.id = i.position_id WHERE i.quote_id = ?', q.id);
  res.json(q);
});

// ---------- Preise / Objektpreise ----------
r.get('/prices', (req, res) => {
  const where = [], p = [];
  if (req.query.q) { where.push('(pr.text LIKE ? OR pd.name LIKE ? OR pr.group_name LIKE ?)'); p.push(...Array(3).fill(`%${req.query.q}%`)); }
  for (const k of ['product_id', 'supplier_id', 'project_id']) if (req.query[k]) { where.push(`pr.${k} = ?`); p.push(req.query[k]); }
  res.json(all(`SELECT pr.*, s.name supplier_name, pj.name project_name, pd.name product_name FROM prices pr LEFT JOIN suppliers s ON s.id = pr.supplier_id
    LEFT JOIN projects pj ON pj.id = pr.project_id LEFT JOIN products pd ON pd.id = pr.product_id ${where.length ? `WHERE ${where.join(' AND ')}` : ''} ORDER BY pr.date DESC, pr.id DESC LIMIT 500`, ...p));
});
r.post('/prices', (req, res) => {
  const b = req.body;
  if (!(Number(b.price) >= 0) || (!b.text && !b.product_id)) throw bad('Preis und Produkt/Text erforderlich');
  const id = insert('prices', { product_id: b.product_id || null, supplier_id: b.supplier_id || null, project_id: b.project_id || null, text: b.text || get('SELECT name FROM products WHERE id = ?', b.product_id)?.name, group_name: b.group_name, price: Number(b.price), unit: b.unit, qty: b.qty, date: b.date || undefined, valid_until: b.valid_until, source: b.source || 'manuell' });
  res.json({ id });
});
r.delete('/prices/:id', (req, res) => { run('DELETE FROM prices WHERE id = ?', req.params.id); res.json({ ok: true }); });

export default r;
