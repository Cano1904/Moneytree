// Dashboard, Suche, Einstellungen, Benutzer, Produkte, Wissensdatenbank, Pläne, Demo-Daten, Backup
import { Router } from 'express';
import fs from 'node:fs';
import path from 'node:path';
import { db, all, get, insert, update, run, tx, audit, parseJsonCols, getSettings, setSettings, DATA_DIR } from '../db.js';
import { saveDocument, readDocument, uid, must, bad, sendFile, todayIso, touchProject, lanAddresses } from '../util.js';
import QRCode from 'qrcode';
import { upload, fixName } from './projects.js';
import { createLv } from './lv.js';
import { aiStatus, aiText, AiError } from '../services/ai.js';
import { analyzePlan } from '../services/planAi.js';
import { analyzeLv } from '../services/lvImport.js';
import { checkLv } from '../services/plausibility.js';
import { mimeFor } from '../services/extract.js';
import { STUCK_DAYS, FINAL_STATUS } from '../../shared/status.js';
import { classify } from '../../shared/groups.js';
import { slug } from '../../shared/format.js';

const r = Router();

// ---------- Dashboard ----------
r.get('/dashboard', (req, res) => {
  const today = todayIso();
  const statusCounts = Object.fromEntries(all('SELECT status, COUNT(*) n FROM projects GROUP BY status').map((x) => [x.status, x.n]));
  const active = all(`SELECT p.id, p.name, p.number, p.status, p.status_changed_at, p.updated_at, p.mode, c.name customer_name,
      CAST(julianday('now') - julianday(p.status_changed_at) AS REAL) days_in_status,
      (SELECT COUNT(*) FROM lv_positions x JOIN lvs l ON l.id = x.lv_id WHERE l.project_id = p.id AND x.pos_type != 'T' AND x.ek IS NULL) missing_prices,
      (SELECT COUNT(*) FROM lv_positions x JOIN lvs l ON l.id = x.lv_id WHERE l.project_id = p.id AND x.pos_type != 'T') positions
    FROM projects p LEFT JOIN customers c ON c.id = p.customer_id WHERE p.status NOT IN (${FINAL_STATUS.map(() => '?').join(',')}) ORDER BY p.updated_at DESC`, ...FINAL_STATUS);
  const stuck = active.filter((p) => p.days_in_status > (STUCK_DAYS[p.status] ?? 3)).sort((a, b) => b.days_in_status - a.days_in_status);
  const openRequests = all(`SELECT r.id, r.status, r.sent_at, r.channel, r.project_id, s.name supplier_name, s.phone supplier_phone, p.name project_name,
      CAST(julianday('now') - julianday(COALESCE(r.sent_at, r.created_at)) AS REAL) age_days,
      (SELECT COUNT(*) FROM supplier_request_items i JOIN lv_positions x ON x.id = i.position_id WHERE i.request_id = r.id AND x.ek IS NULL) missing
    FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id JOIN projects p ON p.id = r.project_id WHERE r.status IN ('vorbereitet','angefragt','ausstehend','rueckfrage','preis_erhalten') ORDER BY r.status = 'rueckfrage' DESC, age_days DESC`);
  const fu = (cond, ...p) => all(`SELECT f.*, p.name project_name, c.name customer_name, p.contact_phone, c.phone customer_phone FROM followups f JOIN projects p ON p.id = f.project_id LEFT JOIN customers c ON c.id = p.customer_id WHERE ${cond} ORDER BY f.due_date LIMIT 50`, ...p);
  const openOffers = get(`SELECT COUNT(*) n, COALESCE(SUM(o.total_net),0) value FROM offers o JOIN projects p ON p.id = o.project_id WHERE o.status = 'versendet' AND p.status NOT IN ('auftrag','verloren') AND o.version = (SELECT MAX(version) FROM offers x WHERE x.project_id = o.project_id)`);
  res.json({
    statusCounts, stuck: stuck.slice(0, 15), openRequests,
    followups: { today: fu("f.status = 'offen' AND f.due_date = ?", today), overdue: fu("f.status = 'offen' AND f.due_date < ?", today), done: get("SELECT COUNT(*) n FROM followups WHERE status = 'erledigt' AND done_at >= date('now','-30 day')").n },
    recent: active.slice(0, 8),
    missingPrices: active.filter((p) => p.missing_prices > 0).slice(0, 10),
    openOffers,
    won: get("SELECT COUNT(*) n FROM projects WHERE status = 'auftrag' AND status_changed_at >= date('now','-90 day')").n,
    lost: get("SELECT COUNT(*) n FROM projects WHERE status = 'verloren' AND status_changed_at >= date('now','-90 day')").n,
    ai: aiStatus(),
  });
});

// ---------- Globale Suche (natürliche Sprache, regelbasiert) ----------
const STOP = new Set('zeige zeig mir alle alles die der das den dem von vom mit fuer für und oder ein eine im in zu zum zur bitte suche finde welche welcher gib list liste'.split(' '));
r.get('/search', (req, res) => {
  const raw = String(req.query.q || '').trim();
  if (!raw) return res.json({ query: raw, sections: [] });
  const q = raw.toLowerCase();
  const take = (re) => { const m = raw.match(re); return m ? m[1].trim() : null; };
  const customer = take(/kunde[n]?\s+(.+?)(?=\s+(mit|von|für|lieferant|produkt)\b|$)/i);
  const supplier = take(/lieferant(?:en)?\s+(.+?)(?=\s+(mit|von|für|kunde|produkt)\b|$)/i);
  const product = take(/(?:produkt|artikel|objektpreise? für|preise? für|mit)\s+(.+?)(?=\s+(von|für|kunde|lieferant)\b|$)/i);
  const want = {
    lvs: /\blvs?\b|leistungsverzeichnis/.test(q), projects: /projekt/.test(q), prices: /objektpreis|preis|\bek\b/.test(q),
    quotes: /angebot/.test(q) && /lieferant/.test(q), offers: /angebot/.test(q) && !/lieferant/.test(q), requests: /anfrage/.test(q), open: /\boffen/.test(q),
  };
  const anyEntity = Object.entries(want).some(([k, v]) => v && k !== 'open');
  const words = slug(raw).split(' ').filter((w) => w.length > 1 && !STOP.has(w) && !/^(lvs?|projekte?|angebote?|objektpreise?|preise?|kunden?|lieferanten?|produkte?|offene?n?|anfragen?|ek|leistungsverzeichnis(se)?)$/.test(w));
  const term = product || (!customer && !supplier ? words.join(' ') : null);
  const like = (s) => `%${s}%`;
  const sections = [];
  const add = (key, title, rows) => { if (rows.length) sections.push({ key, title, rows }); };

  if (!anyEntity || want.projects || want.lvs || customer) {
    const w = [], p = [];
    if (customer) { w.push('c.name LIKE ?'); p.push(like(customer)); }
    if (term && !customer) { w.push(`(p.name LIKE ? OR p.number LIKE ? OR c.name LIKE ? OR EXISTS (SELECT 1 FROM lv_positions x JOIN lvs l ON l.id = x.lv_id WHERE l.project_id = p.id AND (x.short_text LIKE ? OR x.long_text LIKE ? OR x.group_name LIKE ?)))`); p.push(...Array(6).fill(like(term))); }
    else if (term && customer && product) { w.push('EXISTS (SELECT 1 FROM lv_positions x JOIN lvs l ON l.id = x.lv_id WHERE l.project_id = p.id AND (x.short_text LIKE ? OR x.group_name LIKE ?))'); p.push(like(term), like(term)); }
    if (want.open) w.push("p.status NOT IN ('auftrag','verloren')");
    if (w.length) add('projects', want.lvs ? 'LVs / Projekte' : 'Projekte', all(`SELECT p.id, p.name, p.number, p.status, c.name customer_name, (SELECT GROUP_CONCAT(name, ', ') FROM lvs WHERE project_id = p.id) lvs FROM projects p LEFT JOIN customers c ON c.id = p.customer_id WHERE ${w.join(' AND ')} ORDER BY p.updated_at DESC LIMIT 30`, ...p));
  }
  if (!anyEntity || want.offers) {
    const w = [], p = [];
    if (customer) { w.push('c.name LIKE ?'); p.push(like(customer)); }
    if (want.open) w.push("o.status = 'versendet' AND pj.status NOT IN ('auftrag','verloren')");
    if (term && !customer && !want.open) { w.push('(o.number LIKE ? OR pj.name LIKE ? OR c.name LIKE ?)'); p.push(like(term), like(term), like(term)); }
    if (w.length) add('offers', 'Angebote', all(`SELECT o.*, pj.name project_name, c.name customer_name FROM offers o JOIN projects pj ON pj.id = o.project_id LEFT JOIN customers c ON c.id = pj.customer_id WHERE ${w.join(' AND ')} ORDER BY o.id DESC LIMIT 30`, ...p));
  }
  if (want.quotes || supplier || (!anyEntity && term)) {
    const w = [], p = [];
    if (supplier) { w.push('s.name LIKE ?'); p.push(like(supplier)); }
    else if (term) { w.push('(s.name LIKE ? OR EXISTS (SELECT 1 FROM supplier_quote_items i WHERE i.quote_id = q.id AND i.text LIKE ?))'); p.push(like(term), like(term)); }
    if (w.length) add('quotes', 'Lieferantenangebote', all(`SELECT q.*, s.name supplier_name, pj.name project_name, (SELECT COUNT(*) FROM supplier_quote_items i WHERE i.quote_id = q.id) items FROM supplier_quotes q LEFT JOIN suppliers s ON s.id = q.supplier_id LEFT JOIN projects pj ON pj.id = q.project_id WHERE ${w.join(' AND ')} ORDER BY q.id DESC LIMIT 30`, ...p));
  }
  if (want.prices || (!anyEntity && term)) {
    const w = [], p = [];
    if (term) { w.push('(pr.text LIKE ? OR pd.name LIKE ? OR pr.group_name LIKE ?)'); p.push(like(term), like(term), like(term)); }
    if (supplier) { w.push('s.name LIKE ?'); p.push(like(supplier)); }
    if (w.length) add('prices', 'Objektpreise / Preishistorie', all(`SELECT pr.*, s.name supplier_name, pj.name project_name, pd.name product_name FROM prices pr LEFT JOIN suppliers s ON s.id = pr.supplier_id LEFT JOIN projects pj ON pj.id = pr.project_id LEFT JOIN products pd ON pd.id = pr.product_id WHERE ${w.join(' AND ')} ORDER BY pr.date DESC LIMIT 40`, ...p));
  }
  if (want.requests || (want.open && !anyEntity)) add('requests', 'Lieferantenanfragen', all(`SELECT r.*, s.name supplier_name, pj.name project_name FROM supplier_requests r JOIN suppliers s ON s.id = r.supplier_id JOIN projects pj ON pj.id = r.project_id WHERE r.status NOT IN ('vollstaendig') ${supplier ? 'AND s.name LIKE ?' : ''} ORDER BY r.id DESC LIMIT 30`, ...(supplier ? [like(supplier)] : [])));
  if (!anyEntity && term) {
    add('positions', 'LV-Positionen', all(`SELECT x.id, x.oz, x.short_text, x.qty, x.unit, x.ek, l.id lv_id, l.project_id, pj.name project_name FROM lv_positions x JOIN lvs l ON l.id = x.lv_id JOIN projects pj ON pj.id = l.project_id WHERE x.short_text LIKE ? OR x.long_text LIKE ? ORDER BY x.id DESC LIMIT 30`, like(term), like(term)));
    add('suppliers', 'Lieferanten', all('SELECT id, name, phone, email FROM suppliers WHERE name LIKE ? OR groups LIKE ? OR notes LIKE ? LIMIT 20', like(term), like(term), like(term)));
    add('customers', 'Kunden', all('SELECT id, name, contact, phone FROM customers WHERE name LIKE ? OR contact LIKE ? LIMIT 20', like(term), like(term)));
    add('products', 'Produkte', all('SELECT id, name, manufacturer, group_name FROM products WHERE name LIKE ? OR manufacturer LIKE ? OR article_no LIKE ? OR specs LIKE ? LIMIT 20', ...Array(4).fill(like(term))));
    add('knowledge', 'Wissensdatenbank', all('SELECT id, title, kind FROM knowledge WHERE title LIKE ? OR body LIKE ? OR tags LIKE ? LIMIT 20', ...Array(3).fill(like(term))));
    add('documents', 'Dokumente', all('SELECT d.id, d.filename, d.category, d.project_id, p.name project_name FROM documents d LEFT JOIN projects p ON p.id = d.project_id WHERE d.filename LIKE ? LIMIT 20', like(term)));
  }
  if (supplier && !sections.length) add('suppliers', 'Lieferanten', all('SELECT id, name, phone, email FROM suppliers WHERE name LIKE ? LIMIT 20', like(supplier)));
  res.json({ query: raw, interpreted: { customer, supplier, product: product || null, term, ...want }, sections });
});

// ---------- Einstellungen & Benutzer ----------
const maskKey = (s) => ({ ...s, ai_api_key: s.ai_api_key ? `••••${String(s.ai_api_key).slice(-4)}` : '' });
r.get('/settings', (req, res) => res.json({ ...maskKey(getSettings()), ai: aiStatus() }));
r.put('/settings', (req, res) => {
  const b = { ...req.body };
  if (typeof b.ai_api_key === 'string' && b.ai_api_key.startsWith('••••')) delete b.ai_api_key;
  setSettings(b);
  audit({ userId: uid(req), entity: 'settings', action: 'geändert', details: { keys: Object.keys(b).filter((k) => k !== 'ai_api_key') } });
  res.json({ ...maskKey(getSettings()), ai: aiStatus() });
});
r.post('/settings/cover', upload.single('file'), (req, res) => {
  if (!req.file) throw bad('Keine Datei');
  const id = saveDocument({ buf: req.file.buffer, filename: fixName(req.file), mime: req.file.mimetype || mimeFor(fixName(req.file)), category: 'deckblatt', userId: uid(req) });
  setSettings({ cover_document_id: id });
  res.json({ cover_document_id: id });
});
r.get('/ai/status', (req, res) => res.json(aiStatus()));

/** iPhone-Einrichtung: Adressen im WLAN + QR-Code zum Abscannen mit der Kamera. */
r.get('/mobile-info', async (req, res) => {
  const host = req.get('host') || '';
  const port = host.split(':')[1] || (req.protocol === 'https' ? '443' : '80');
  const isLocal = /^(localhost|127\.|\[::1\])/.test(host);
  const urls = isLocal ? lanAddresses().map((ip) => `${req.protocol}://${ip}:${port}`) : [`${req.protocol}://${host}`];
  const qr = urls[0] ? await QRCode.toString(urls[0], { type: 'svg', margin: 1, width: 220 }) : null;
  res.json({ urls, qr, secure: req.protocol === 'https' });
});

r.get('/users', (req, res) => res.json(all('SELECT * FROM users ORDER BY active DESC, name')));
r.post('/users', (req, res) => { if (!req.body.name?.trim()) throw bad('Name fehlt'); res.json({ id: insert('users', { name: req.body.name.trim(), email: req.body.email, role: req.body.role || 'mitarbeiter' }) }); });
r.patch('/users/:id', (req, res) => { update('users', req.params.id, req.body, ['name', 'email', 'role', 'active']); res.json({ ok: true }); });

// ---------- Produkte ----------
r.get('/products', (req, res) => {
  const q = `%${req.query.q || ''}%`;
  const rows = all(`SELECT pd.*, (SELECT price FROM prices WHERE product_id = pd.id ORDER BY date DESC, id DESC LIMIT 1) last_price,
      (SELECT date FROM prices WHERE product_id = pd.id ORDER BY date DESC, id DESC LIMIT 1) last_price_date
    FROM products pd WHERE (pd.name LIKE ? OR pd.manufacturer LIKE ? OR pd.article_no LIKE ? OR pd.group_name LIKE ?) ${req.query.group ? 'AND pd.group_name = ?' : ''} ORDER BY pd.name LIMIT 500`, q, q, q, q, ...(req.query.group ? [req.query.group] : [])).map(parseJsonCols);
  res.json(rows);
});
r.post('/products', (req, res) => {
  const b = req.body;
  if (!b.name?.trim()) throw bad('Produktname fehlt');
  const id = insert('products', { name: b.name.trim(), manufacturer: b.manufacturer, group_name: b.group_name || classify(b.name).group, mode: b.mode, unit: b.unit, format: b.format, article_no: b.article_no, specs: b.specs, notes: b.notes, alternatives: b.alternatives || [] });
  if (b.position_id) run('UPDATE lv_positions SET product_id = ? WHERE id = ?', id, b.position_id);
  res.json({ id });
});
r.get('/products/:id', (req, res) => {
  const p = must(parseJsonCols(get('SELECT * FROM products WHERE id = ?', req.params.id)), 'Produkt');
  p.prices = all('SELECT pr.*, s.name supplier_name, pj.name project_name FROM prices pr LEFT JOIN suppliers s ON s.id = pr.supplier_id LEFT JOIN projects pj ON pj.id = pr.project_id WHERE pr.product_id = ? ORDER BY pr.date DESC, pr.id DESC', p.id);
  p.alternativeProducts = (p.alternatives || []).length ? all(`SELECT id, name, manufacturer FROM products WHERE id IN (${p.alternatives.map(Number).join(',')})`) : [];
  p.usedAsAlternativeBy = all('SELECT id, name, alternatives FROM products WHERE id != ?', p.id).map(parseJsonCols).filter((x) => (x.alternatives || []).includes(p.id)).map(({ id, name }) => ({ id, name }));
  p.knowledge = all('SELECT * FROM knowledge WHERE product_id = ?', p.id);
  res.json(p);
});
r.patch('/products/:id', (req, res) => { update('products', req.params.id, req.body, ['name', 'manufacturer', 'group_name', 'mode', 'unit', 'format', 'article_no', 'specs', 'notes', 'alternatives']); res.json({ ok: true }); });
r.delete('/products/:id', (req, res) => { run('DELETE FROM products WHERE id = ?', req.params.id); res.json({ ok: true }); });

// ---------- Wissensdatenbank ----------
r.get('/knowledge', (req, res) => {
  const q = `%${req.query.q || ''}%`;
  res.json(all('SELECT k.*, p.name product_name, s.name supplier_name FROM knowledge k LEFT JOIN products p ON p.id = k.product_id LEFT JOIN suppliers s ON s.id = k.supplier_id WHERE k.title LIKE ? OR k.body LIKE ? OR k.tags LIKE ? ORDER BY k.updated_at DESC LIMIT 300', q, q, q));
});
r.post('/knowledge', (req, res) => {
  if (!req.body.title?.trim()) throw bad('Titel fehlt');
  res.json({ id: insert('knowledge', { kind: req.body.kind || 'notiz', title: req.body.title.trim(), body: req.body.body, tags: req.body.tags, product_id: req.body.product_id || null, supplier_id: req.body.supplier_id || null, created_by: uid(req) }) });
});
r.patch('/knowledge/:id', (req, res) => { update('knowledge', req.params.id, req.body, ['kind', 'title', 'body', 'tags', 'product_id', 'supplier_id']); res.json({ ok: true }); });
r.delete('/knowledge/:id', (req, res) => { run('DELETE FROM knowledge WHERE id = ?', req.params.id); res.json({ ok: true }); });

/** Frage an die Wissensbasis: Suche über Produkte/Wissen/Preise, optional KI-Antwort nur auf Basis der Treffer. */
r.post('/knowledge/ask', async (req, res) => {
  const question = String(req.body.question || '').trim();
  if (!question) throw bad('Frage fehlt');
  const words = slug(question).split(' ').filter((w) => w.length > 3 && !STOP.has(w) && !['welche', 'haben', 'alternative', 'alternativen', 'gibt', 'produkt', 'unsere', 'wir'].includes(w));
  const like = words.map((w) => `%${w}%`);
  const cond = (cols) => (words.length ? words.map(() => `(${cols.map((c) => `${c} LIKE ?`).join(' OR ')})`).join(' OR ') : '0');
  const products = words.length ? all(`SELECT * FROM products WHERE ${cond(['name', 'manufacturer', 'specs', 'notes', 'group_name'])} LIMIT 15`, ...like.flatMap((l) => [l, l, l, l, l])).map(parseJsonCols) : [];
  for (const p of products) p.alternativeProducts = (p.alternatives || []).length ? all(`SELECT id, name, manufacturer FROM products WHERE id IN (${p.alternatives.map(Number).join(',')})`) : [];
  const knowledge = words.length ? all(`SELECT * FROM knowledge WHERE ${cond(['title', 'body', 'tags'])} LIMIT 15`, ...like.flatMap((l) => [l, l, l])) : [];
  const prices = words.length ? all(`SELECT pr.*, s.name supplier_name FROM prices pr LEFT JOIN suppliers s ON s.id = pr.supplier_id WHERE ${cond(['pr.text'])} ORDER BY pr.date DESC LIMIT 10`, ...like) : [];
  let answer = null, aiError = null;
  if (aiStatus().enabled && (products.length || knowledge.length || prices.length)) {
    const ctx = [
      ...products.map((p) => `PRODUKT #${p.id}: ${p.name} | Hersteller ${p.manufacturer || '-'} | Gruppe ${p.group_name || '-'} | Format ${p.format || '-'} | ${p.specs || ''} | Alternativen: ${p.alternativeProducts.map((a) => a.name).join(', ') || '-'} | Notiz: ${p.notes || ''}`),
      ...knowledge.map((k) => `WISSEN #${k.id} (${k.kind}): ${k.title} – ${k.body || ''}`),
      ...prices.map((p) => `PREIS: ${p.text} ${p.price} €/${p.unit || '?'} bei ${p.supplier_name || '?'} am ${p.date}`),
    ].join('\n');
    try {
      answer = await aiText({ prompt: `Frage: ${question}\n\nInterne Datenbasis:\n${ctx}`, instructions: 'Beantworte die Frage ausschließlich anhand der internen Datenbasis, kurz und fachlich. Nenne die Quellen (#ID). Wenn die Daten die Frage nicht beantworten, sage das klar.' });
    } catch (e) { aiError = e instanceof AiError ? e.message : 'KI-Fehler'; }
  }
  res.json({ products, knowledge, prices, answer, aiError, words });
});

// ---------- Pläne / Materialermittlung ----------
r.post('/projects/:id/plans', upload.single('file'), (req, res) => {
  must(get('SELECT id FROM projects WHERE id = ?', req.params.id), 'Projekt');
  if (!req.file) throw bad('Keine Datei');
  const filename = fixName(req.file);
  const mime = req.file.mimetype && req.file.mimetype !== 'application/octet-stream' ? req.file.mimetype : mimeFor(filename);
  if (!/^(application\/pdf|image\/(png|jpeg|webp|gif))$/.test(mime)) throw bad('Pläne bitte als PDF, PNG oder JPG hochladen');
  const docId = saveDocument({ buf: req.file.buffer, filename, mime, projectId: Number(req.params.id), category: 'plan', userId: uid(req) });
  const id = insert('plans', { project_id: req.params.id, document_id: docId, name: req.body.name || filename, kind: req.body.kind || 'Plan' });
  touchProject(req.params.id);
  res.json({ id });
});
r.get('/plans/:id', (req, res) => {
  const p = must(parseJsonCols(get('SELECT pl.*, d.filename, d.mime, pj.name project_name, pj.mode FROM plans pl LEFT JOIN documents d ON d.id = pl.document_id JOIN projects pj ON pj.id = pl.project_id WHERE pl.id = ?', req.params.id)), 'Plan');
  res.json(p);
});
r.patch('/plans/:id', (req, res) => {
  update('plans', req.params.id, req.body, ['name', 'kind', 'measurements', 'runs', 'status']);
  res.json(parseJsonCols(get('SELECT * FROM plans WHERE id = ?', req.params.id)));
});
r.delete('/plans/:id', (req, res) => { run('DELETE FROM plans WHERE id = ?', req.params.id); res.json({ ok: true }); });
r.post('/plans/:id/analyze', async (req, res) => {
  const pl = must(parseJsonCols(get('SELECT * FROM plans WHERE id = ?', req.params.id)), 'Plan');
  if (!aiStatus().enabled) throw bad('KI nicht konfiguriert – Maße bitte mit dem Messwerkzeug oder manuell erfassen');
  const { doc, buf } = readDocument(pl.document_id);
  let a;
  try { a = await analyzePlan({ buf, mime: doc.mime, hint: req.body.hint }); } catch (e) { throw bad(e.message); }
  const ms = [...(pl.measurements || []).filter((m) => m.source_type !== 'ki'), ...a.measurements.map((m, i) => ({ id: `ki${Date.now()}${i}`, ...m, source_type: 'ki', confirmed: false }))];
  update('plans', pl.id, { analysis: a, measurements: ms, status: 'analysiert' });
  audit({ userId: uid(req), projectId: pl.project_id, entity: 'plan', entityId: pl.id, action: 'KI-Plananalyse', details: { measurements: a.measurements.length, questions: a.questions.length } });
  res.json(parseJsonCols(get('SELECT * FROM plans WHERE id = ?', pl.id)));
});

/** Materialliste (aus Rechnern/Plan) als LV in das Projekt übernehmen. */
r.post('/projects/:id/material-lv', (req, res) => {
  const project = must(get('SELECT * FROM projects WHERE id = ?', req.params.id), 'Projekt');
  const items = (req.body.items || []).filter((i) => i.material && Number(i.qty) > 0);
  if (!items.length) throw bad('Keine Materialien');
  const positions = items.map((i, n) => ({ oz: String((n + 1) * 10).padStart(4, '0'), short_text: i.material, long_text: `Ermittelt: ${i.basis || ''}${i.certainty && i.certainty !== 'berechnet' ? ` (${i.certainty})` : ''}`, qty: Number(i.qty), unit: i.unit, pos_type: 'N', group_name: i.group || classify(i.material, project.mode).group, confidence: i.certainty === 'berechnet' ? 0.9 : 0.6, flags: i.certainty && i.certainty !== 'berechnet' ? [{ key: 'calc_assumption', severity: 'warn', msg: `Menge beruht auf Annahme (${i.certainty}) – bitte bestätigen` }] : [] }));
  const lvId = createLv({ projectId: project.id, name: req.body.name || 'Materialermittlung', format: 'material', parseMethod: 'rechner', parseInfo: { source: req.body.source || 'rechner' }, positions, userId: uid(req) });
  const posRows = all('SELECT * FROM lv_positions WHERE lv_id = ?', lvId).map(parseJsonCols);
  const chk = checkLv(posRows, { mode: project.mode });
  tx(() => { for (const p of chk.positions) run('UPDATE lv_positions SET flags = ? WHERE id = ?', JSON.stringify(p.flags), p.id); run('UPDATE lvs SET issues = ? WHERE id = ?', JSON.stringify(chk.lvIssues), lvId); });
  touchProject(project.id);
  res.json({ lvId });
});

// ---------- Demo-Daten & Backup ----------
r.post('/demo', async (req, res) => {
  if (get("SELECT id FROM suppliers WHERE name LIKE '%(Demo)%'")) throw bad('Demo-Daten sind bereits geladen');
  const sup = (name, groups, extra = {}) => insert('suppliers', { name: `${name} (Demo)`, groups, ...extra });
  const s1 = sup('Betonwerk Nord GmbH', ['Pflaster', 'Platten', 'Bordsteine', 'Mauersysteme', 'Stützwinkel'], { phone: '04101 123450', email: 'angebote@betonwerk-nord.example', delivery_days: 10, preferred: 1, notes: 'Pflaster/Bord ab Werk, Fracht ab 25 t frei' });
  const s2 = sup('Rohr & Rinne Handel KG', ['Rohre', 'Rinnen', 'Schächte', 'Entwässerung', 'Kanal/Tiefbau'], { phone: '040 998877', email: 'vertrieb@rohr-rinne.example', delivery_days: 5 });
  const s3 = sup('Kieswerk Süd', ['Schüttgüter', 'Zement/Mörtel', 'Fugenmaterial'], { phone: '07141 55555', email: 'dispo@kieswerk-sued.example', delivery_days: 2 });
  const s4 = sup('Terrassenwelt Großhandel', ['Terrassen', 'Stelzlager', 'Platten'], { phone: '0221 4433', email: 'info@terrassenwelt.example', delivery_days: 7 });
  const s5 = sup('Baustoff Union Hochbau', ['Kalksandstein', 'Mauerwerk', 'Beton', 'Bewehrung', 'Stahl', 'Dämmung', 'Baustoffe', 'Zubehör'], { phone: '0711 22334', email: 'hochbau@baustoff-union.example', delivery_days: 7 });
  const s6 = sup('Stadtraum Ausstattung', ['Stadtmobiliar', 'Fahrradständer', 'Sonderbauteile'], { phone: '030 776655', email: 'objekt@stadtraum.example', delivery_days: 21 });
  for (const [sid, n, role, mail] of [[s1, 'Frau Petersen', 'Innendienst', 'petersen@betonwerk-nord.example'], [s2, 'Herr Yilmaz', 'Verkauf', 'yilmaz@rohr-rinne.example'], [s3, 'Herr Schäfer', 'Disposition', null], [s4, 'Frau Lange', 'Objektberatung', 'lange@terrassenwelt.example'], [s5, 'Herr Wagner', 'Außendienst', 'wagner@baustoff-union.example'], [s6, 'Frau Koch', 'Projekte', 'koch@stadtraum.example']]) insert('supplier_contacts', { supplier_id: sid, name: n, role, email: mail });
  const prod = (o) => insert('products', o);
  const p1 = prod({ name: 'Rechteckpflaster 20/10/8 grau', manufacturer: 'Betonwerk Nord', group_name: 'Pflaster', unit: 'm²', format: '20/10/8', specs: 'DIN EN 1338, ca. 50 St/m²' });
  const p2 = prod({ name: 'Verbundpflaster Doppel-T 8 cm grau', manufacturer: 'Betonwerk Nord', group_name: 'Pflaster', unit: 'm²', format: '20/16,5/8', specs: 'ca. 36 St/m²', alternatives: [p1] });
  run('UPDATE products SET alternatives = ? WHERE id = ?', JSON.stringify([p2]), p1);
  const p3 = prod({ name: 'Tiefbordstein 100/25/8 grau', manufacturer: 'Betonwerk Nord', group_name: 'Bordsteine', unit: 'm', format: '100/25/8' });
  const p4 = prod({ name: 'KG 2000 Rohr DN 160 SN 12, 5 m', manufacturer: 'Rohrwerk', group_name: 'Rohre', unit: 'm', format: 'DN 160' });
  const p5 = prod({ name: 'Polymerbetonrinne NW 100 C250 mit Gussrost', manufacturer: 'Rinnenbau', group_name: 'Rinnen', unit: 'm', format: 'NW 100' });
  const p6 = prod({ name: 'Stelzlager 35–55 mm mit Fugenkreuz 4 mm', manufacturer: 'Terrassenwelt', group_name: 'Stelzlager', unit: 'St' });
  const p7 = prod({ name: 'KS-Planstein 12DF SFK 20', manufacturer: 'KS-Werk', group_name: 'Kalksandstein', unit: 'm²', format: '498/175/248' });
  const p8 = prod({ name: 'Frostschutzmaterial 0/32', manufacturer: 'Kieswerk Süd', group_name: 'Schüttgüter', unit: 't' });
  const price = (product, supplier, text, pr, unit, date) => insert('prices', { product_id: product, supplier_id: supplier, text, price: pr, unit, date, source: 'angebot', group_name: get('SELECT group_name FROM products WHERE id = ?', product).group_name });
  price(p1, s1, 'Rechteckpflaster 20/10/8 grau', 12.4, 'm²', '2026-09-14'); price(p1, s1, 'Rechteckpflaster 20/10/8 grau', 11.9, 'm²', '2026-03-02');
  price(p3, s1, 'Tiefbordstein 100/25/8 grau', 6.1, 'm', '2026-06-20'); price(p4, s2, 'KG-Rohr DN 160 SN 12', 9.8, 'm', '2026-08-11');
  price(p5, s2, 'Entwässerungsrinne NW 100 C250', 48.5, 'm', '2026-07-01'); price(p6, s4, 'Stelzlager 35-55 mm', 2.35, 'St', '2026-05-18');
  price(p7, s5, 'KS-Planstein 12DF', 24.9, 'm²', '2026-09-01'); price(p8, s3, 'Frostschutz 0/32', 14.5, 't', '2026-09-20');
  insert('knowledge', { kind: 'alternative', title: 'Alternative zu Rechteckpflaster 20/10/8', body: 'Bei Lieferengpass: Verbundpflaster Doppel-T 8 cm (Betonwerk Nord) – Kunde muss Verlegemuster freigeben. Preis ca. 5 % günstiger.', tags: 'pflaster, alternative', product_id: p1 });
  insert('knowledge', { kind: 'erfahrung', title: 'Stelzlager bei Großformat 80/40', body: 'Ab Plattenlänge > 60 cm zusätzliches Mittellager je Platte einplanen (Durchbiegung). Randlager mit teilbarem Kopf bestellen.', tags: 'stelzlager, terrasse, großformat' });
  insert('knowledge', { kind: 'schema', title: 'Kalkulationsschema Schüttgüter', body: 'Schüttgüter: Aufschlag 12 %, Fracht je Tour separat ausweisen (Sattelzug 25 t).', tags: 'kalkulation, schüttgut' });
  const cust = insert('customers', { name: 'Gemeinde Musterstadt (Demo)', contact: 'Herr Bauer', email: 'bauamt@musterstadt.example', phone: '01234 5678' });
  const pid = insert('projects', { number: `P-${new Date().getFullYear()}-DEMO`, name: 'Außenanlagen Kita Sonnenschein (Demo)', customer_id: cust, contact_name: 'Herr Bauer', contact_email: 'bauamt@musterstadt.example', site: 'Lindenweg 3, Musterstadt', mode: 'galabau', created_by: uid(req) });
  const sample = path.resolve(import.meta.dirname, '..', '..', 'samples', 'Beispiel-LV_Aussenanlagen.pdf');
  if (fs.existsSync(sample)) {
    const buf = fs.readFileSync(sample);
    const docId = saveDocument({ buf, filename: 'Beispiel-LV_Aussenanlagen.pdf', mime: 'application/pdf', projectId: pid, category: 'lv_original', userId: uid(req) });
    const a = await analyzeLv({ buf, filename: 'x.pdf', mime: 'application/pdf', mode: 'galabau', useAi: false });
    const lvId = createLv({ projectId: pid, name: 'LV Außenanlagen', format: a.format, parseMethod: a.info.method, parseInfo: a.info, sourceDocId: docId, meta: a.meta, positions: a.positions, userId: uid(req) });
    const rows = all('SELECT * FROM lv_positions WHERE lv_id = ?', lvId).map(parseJsonCols);
    const chk = checkLv(rows, { mode: 'galabau' });
    tx(() => { for (const p of chk.positions) run('UPDATE lv_positions SET flags = ? WHERE id = ?', JSON.stringify(p.flags), p.id); run('UPDATE lvs SET issues = ? WHERE id = ?', JSON.stringify(chk.lvIssues), lvId); });
    run("UPDATE projects SET status = 'lv_analysiert' WHERE id = ?", pid);
  }
  audit({ userId: uid(req), entity: 'demo', action: 'Demo-Daten geladen' });
  res.json({ ok: true, project_id: pid });
});

r.get('/backup', (req, res) => {
  const file = path.join(DATA_DIR, `backup-${Date.now()}.sqlite`);
  db.exec(`VACUUM INTO '${file.replace(/'/g, "''")}'`);
  const buf = fs.readFileSync(file);
  fs.unlinkSync(file);
  sendFile(res, buf, `baustoff-ki-backup-${todayIso()}.sqlite`, 'application/x-sqlite3');
});

export default r;
