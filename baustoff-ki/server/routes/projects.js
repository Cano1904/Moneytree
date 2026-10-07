// Kunden, Projekte (digitale Projektakte), Dokumente, Notizen, Verlauf
import { Router } from 'express';
import multer from 'multer';
import { all, get, insert, update, run, audit, parseJsonCols, getSettings } from '../db.js';
import { saveDocument, readDocument, uid, must, bad, sendFile } from '../util.js';
import { mimeFor } from '../services/extract.js';
import { calcTotals } from '../../shared/pricing.js';
import { PROJECT_STATUS } from '../../shared/status.js';
import { loadPositions } from '../util.js';

export const upload = multer({ storage: multer.memoryStorage(), limits: { fileSize: 60 * 1024 * 1024 } });
export const fixName = (f) => Buffer.from(f.originalname, 'latin1').toString('utf8');
const r = Router();

// ---------- Kunden ----------
r.get('/customers', (req, res) => {
  const q = `%${req.query.q || ''}%`;
  res.json(all(`SELECT c.*, (SELECT COUNT(*) FROM projects p WHERE p.customer_id = c.id) projects FROM customers c WHERE c.name LIKE ? OR c.contact LIKE ? ORDER BY c.name LIMIT 200`, q, q));
});
r.post('/customers', (req, res) => {
  if (!req.body.name?.trim()) throw bad('Name fehlt');
  const id = insert('customers', { name: req.body.name.trim(), contact: req.body.contact, email: req.body.email, phone: req.body.phone, address: req.body.address, notes: req.body.notes });
  res.json(get('SELECT * FROM customers WHERE id = ?', id));
});
r.patch('/customers/:id', (req, res) => {
  update('customers', req.params.id, req.body, ['name', 'contact', 'email', 'phone', 'address', 'notes']);
  res.json(get('SELECT * FROM customers WHERE id = ?', req.params.id));
});

// ---------- Projekte ----------
function projectTotals(projectId) {
  const s = getSettings();
  const lvs = all('SELECT id, min_margin FROM lvs WHERE project_id = ?', projectId);
  let net = 0, missing = 0, count = 0, belowMin = 0;
  for (const lv of lvs) {
    const t = calcTotals(loadPositions(lv.id, lv.min_margin ?? s.min_margin), { minMargin: lv.min_margin ?? s.min_margin, vatPct: s.vat_pct });
    net += t.net; missing += t.missing; count += t.count; belowMin += t.belowMin;
  }
  return { net, missing, count, belowMin };
}

r.get('/projects', (req, res) => {
  const where = [], p = [];
  if (req.query.status) { where.push('p.status = ?'); p.push(req.query.status); }
  if (req.query.customer_id) { where.push('p.customer_id = ?'); p.push(req.query.customer_id); }
  if (req.query.q) { where.push('(p.name LIKE ? OR p.number LIKE ? OR c.name LIKE ?)'); p.push(...Array(3).fill(`%${req.query.q}%`)); }
  const rows = all(`SELECT p.*, c.name customer_name,
      (SELECT COUNT(*) FROM lv_positions x JOIN lvs l ON l.id = x.lv_id WHERE l.project_id = p.id AND x.pos_type != 'T' AND x.ek IS NULL) missing_prices,
      (SELECT MIN(due_date) FROM followups f WHERE f.project_id = p.id AND f.status = 'offen') next_followup
    FROM projects p LEFT JOIN customers c ON c.id = p.customer_id ${where.length ? `WHERE ${where.join(' AND ')}` : ''} ORDER BY p.updated_at DESC LIMIT ${Number(req.query.limit) || 300}`, ...p);
  if (req.query.totals) for (const row of rows) row.totals = projectTotals(row.id);
  res.json(rows);
});

r.post('/projects', (req, res) => {
  const b = req.body;
  if (!b.name?.trim()) throw bad('Projektname fehlt');
  let customerId = b.customer_id || null;
  if (!customerId && b.customer_name?.trim()) {
    customerId = get('SELECT id FROM customers WHERE name = ?', b.customer_name.trim())?.id || insert('customers', { name: b.customer_name.trim(), contact: b.contact_name, email: b.contact_email, phone: b.contact_phone });
  }
  const year = new Date().getFullYear();
  const seq = (get(`SELECT COUNT(*) n FROM projects WHERE number LIKE ?`, `P-${year}-%`).n || 0) + 1;
  const id = insert('projects', {
    number: b.number || `P-${year}-${String(seq).padStart(4, '0')}`, name: b.name.trim(), customer_id: customerId, contact_name: b.contact_name, contact_email: b.contact_email,
    contact_phone: b.contact_phone, site: b.site, mode: b.mode === 'hochbau' ? 'hochbau' : 'galabau', due_date: b.due_date, notes: b.notes, created_by: uid(req),
  });
  audit({ userId: uid(req), projectId: id, entity: 'project', entityId: id, action: 'angelegt' });
  res.json(get('SELECT * FROM projects WHERE id = ?', id));
});

r.get('/projects/:id', (req, res) => {
  const p = must(get('SELECT p.*, c.name customer_name, c.email customer_email, c.phone customer_phone, c.address customer_address, c.contact customer_contact FROM projects p LEFT JOIN customers c ON c.id = p.customer_id WHERE p.id = ?', req.params.id), 'Projekt');
  const s = getSettings();
  p.lvs = all('SELECT id, name, format, parse_method, source_document_id, global_markup, min_margin, created_at FROM lvs WHERE project_id = ? ORDER BY id', p.id)
    .map((lv) => ({ ...lv, totals: calcTotals(loadPositions(lv.id, lv.min_margin ?? s.min_margin), { minMargin: lv.min_margin ?? s.min_margin, vatPct: s.vat_pct }) }));
  p.documents = all('SELECT id, category, filename, mime, size, version, note, created_at FROM documents WHERE project_id = ? ORDER BY created_at DESC, id DESC', p.id);
  p.notes = all('SELECT n.*, u.name user_name FROM notes n LEFT JOIN users u ON u.id = n.created_by WHERE project_id = ? ORDER BY n.created_at DESC, n.id DESC', p.id);
  p.requests = all(`SELECT r.*, s.name supplier_name, (SELECT COUNT(*) FROM supplier_request_items i WHERE i.request_id = r.id) items FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id WHERE r.project_id = ? ORDER BY r.id`, p.id);
  p.offers = all('SELECT * FROM offers WHERE project_id = ? ORDER BY version DESC', p.id).map(parseJsonCols);
  p.followups = all('SELECT * FROM followups WHERE project_id = ? ORDER BY due_date DESC', p.id);
  p.plans = all('SELECT id, name, kind, status, document_id, created_at FROM plans WHERE project_id = ? ORDER BY id DESC', p.id);
  p.quotes = all('SELECT q.*, s.name supplier_name FROM supplier_quotes q LEFT JOIN suppliers s ON s.id = q.supplier_id WHERE q.project_id = ? ORDER BY q.id DESC', p.id);
  res.json(p);
});

r.patch('/projects/:id', (req, res) => {
  const p = must(get('SELECT * FROM projects WHERE id = ?', req.params.id), 'Projekt');
  const b = { ...req.body };
  if (b.status && !PROJECT_STATUS.some((s) => s.id === b.status)) throw bad('Unbekannter Status');
  if (b.customer_name !== undefined && !b.customer_id) {
    b.customer_id = b.customer_name.trim() ? get('SELECT id FROM customers WHERE name = ?', b.customer_name.trim())?.id || insert('customers', { name: b.customer_name.trim() }) : null;
  }
  update('projects', p.id, b, ['name', 'number', 'customer_id', 'contact_name', 'contact_email', 'contact_phone', 'site', 'mode', 'status', 'due_date', 'notes', 'cover_document_id']);
  if (b.status && b.status !== p.status) {
    run('UPDATE projects SET status_changed_at = CURRENT_TIMESTAMP WHERE id = ?', p.id);
    audit({ userId: uid(req), projectId: p.id, entity: 'project', entityId: p.id, action: 'status', details: { from: p.status, to: b.status } });
  }
  if (b.mode && b.mode !== p.mode) audit({ userId: uid(req), projectId: p.id, entity: 'project', entityId: p.id, action: 'modus', details: { from: p.mode, to: b.mode } });
  res.json(get('SELECT * FROM projects WHERE id = ?', p.id));
});

r.delete('/projects/:id', (req, res) => {
  run('DELETE FROM projects WHERE id = ?', req.params.id);
  audit({ userId: uid(req), entity: 'project', entityId: Number(req.params.id), action: 'gelöscht' });
  res.json({ ok: true });
});

r.get('/projects/:id/audit', (req, res) => {
  res.json(all('SELECT a.*, u.name user_name FROM audit_logs a LEFT JOIN users u ON u.id = a.user_id WHERE a.project_id = ? ORDER BY a.id DESC LIMIT 500', req.params.id).map(parseJsonCols));
});

// ---------- Notizen ----------
r.post('/projects/:id/notes', (req, res) => {
  if (!req.body.text?.trim()) throw bad('Text fehlt');
  const id = insert('notes', { project_id: req.params.id, kind: req.body.kind || 'notiz', text: req.body.text.trim(), created_by: uid(req) });
  res.json(get('SELECT * FROM notes WHERE id = ?', id));
});
r.delete('/notes/:id', (req, res) => { run('DELETE FROM notes WHERE id = ?', req.params.id); res.json({ ok: true }); });

// ---------- Dokumente ----------
r.post('/projects/:id/documents', upload.array('files', 20), (req, res) => {
  must(get('SELECT id FROM projects WHERE id = ?', req.params.id), 'Projekt');
  if (!req.files?.length) throw bad('Keine Datei');
  const ids = req.files.map((f) => saveDocument({ buf: f.buffer, filename: fixName(f), mime: f.mimetype || mimeFor(fixName(f)), projectId: Number(req.params.id), category: req.body.category || 'sonstiges', note: req.body.note, userId: uid(req) }));
  res.json(all(`SELECT * FROM documents WHERE id IN (${ids.join(',')})`));
});
r.get('/documents/:id/download', (req, res) => {
  const { doc, buf } = readDocument(req.params.id);
  if (req.query.inline) {
    res.setHeader('Content-Type', doc.mime || mimeFor(doc.filename));
    res.setHeader('Content-Disposition', `inline; filename*=UTF-8''${encodeURIComponent(doc.filename)}`);
    return res.send(buf);
  }
  sendFile(res, buf, doc.filename, doc.mime);
});
r.patch('/documents/:id', (req, res) => { update('documents', req.params.id, req.body, ['category', 'note']); res.json(get('SELECT * FROM documents WHERE id = ?', req.params.id)); });
r.delete('/documents/:id', (req, res) => {
  const d = must(get('SELECT * FROM documents WHERE id = ?', req.params.id), 'Dokument');
  if (['lv_original', 'angebot', 'lieferantenangebot'].includes(d.category)) throw bad('Originale, Angebote und Lieferantenangebote werden revisionssicher archiviert und können nicht gelöscht werden');
  run('DELETE FROM documents WHERE id = ?', d.id);
  audit({ userId: uid(req), projectId: d.project_id, entity: 'document', entityId: d.id, action: 'gelöscht', details: { filename: d.filename } });
  res.json({ ok: true });
});

export default r;
