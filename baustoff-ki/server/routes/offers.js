// Angebotserstellung (Deckblatt + LV + Anhänge = PDF), Kundenmail/EML, Versand-Freigabe, Wiedervorlagen
import { Router } from 'express';
import { all, get, insert, update, run, tx, audit, parseJsonCols, getSettings } from '../db.js';
import { saveDocument, readDocument, uid, must, bad, advanceProject, lvSummary, sendFile, todayIso } from '../util.js';
import { generateCover, generateLvPdf, overlayOriginal, assemble } from '../services/offerPdf.js';
import { fillExcel, fillGaeb } from '../services/lvExport.js';
import { offerMail, buildEml } from '../services/mail.js';
import { mimeFor } from '../services/extract.js';

const r = Router();
const projectFull = (id) => must(get('SELECT p.*, c.name customer_name, c.address customer_address, c.contact customer_contact, c.email customer_email FROM projects p LEFT JOIN customers c ON c.id = p.customer_id WHERE p.id = ?', id), 'Projekt');
const customerOf = (p) => ({ name: p.customer_name, address: p.customer_address, contact: p.customer_contact, email: p.customer_email });

/** Vorschau: verfügbare Bausteine, Reihenfolge-Vorschlag und Prüfungen vor der PDF-Erstellung. */
r.get('/projects/:id/offer-preview', (req, res) => {
  const p = projectFull(req.params.id);
  const s = getSettings();
  const lvId = Number(req.query.lv_id) || get('SELECT id FROM lvs WHERE project_id = ? ORDER BY id DESC LIMIT 1', p.id)?.id;
  if (!lvId) throw bad('Projekt hat noch kein LV');
  const sum = lvSummary(lvId);
  const checks = [];
  if (sum.totals.missing) checks.push({ severity: 'error', msg: `${sum.totals.missing} Position(en) ohne Preis oder Menge` });
  if (sum.totals.belowMin) checks.push({ severity: 'warn', msg: `${sum.totals.belowMin} Position(en) unter Mindestmarge (${sum.minMargin} %)` });
  const unresolved = sum.positions.filter((x) => (x.flags || []).some((f) => !f.resolved && f.severity === 'error')).length;
  if (unresolved) checks.push({ severity: 'warn', msg: `${unresolved} Position(en) mit offenen Prüfhinweisen` });
  const sumCheck = Math.round(sum.positions.filter((x) => x.pos_type === 'N' && x.calc.total !== null).reduce((a, x) => a + x.calc.total, 0) * 100) / 100;
  checks.push(Math.abs(sumCheck - sum.totals.net) < 0.01 ? { severity: 'ok', msg: `Summenprüfung OK: ${sumCheck.toFixed(2)} €` } : { severity: 'error', msg: `Summenabweichung: ${sumCheck} ≠ ${sum.totals.net}` });
  const covers = [p.cover_document_id && { type: 'document', document_id: p.cover_document_id, label: 'Projekt-Deckblatt' }, s.cover_document_id && { type: 'document', document_id: s.cover_document_id, label: 'Standard-Deckblatt (Einstellungen)' }, { type: 'cover_generated', label: 'Automatisches Deckblatt (Firmendaten)' }].filter(Boolean);
  const lvVariants = [{ type: 'lv_generated', label: 'Angebots-LV (neu gesetzt, mit Titelsummen)' }];
  if (sum.lv.format === 'pdf' && sum.lv.meta?.hasText) lvVariants.unshift({ type: 'lv_overlay', label: 'Original-LV mit eingetragenen Preisen' });
  const attachments = all("SELECT id, filename, category, mime FROM documents WHERE project_id = ? AND category IN ('anhang','plan','sonstiges','korrespondenz','lieferantenangebot','deckblatt') AND (mime LIKE 'image/%' OR mime = 'application/pdf') ORDER BY created_at DESC", p.id);
  const extras = [sum.lv.format === 'excel' && { type: 'xlsx', label: 'Original-Excel mit Preisen (zusätzliche Datei)' }, sum.lv.format === 'gaeb-xml' && { type: 'gaeb', label: 'GAEB X84 Angebotsabgabe (zusätzliche Datei)' }].filter(Boolean);
  res.json({ lv: { id: sum.lv.id, name: sum.lv.name, format: sum.lv.format }, totals: sum.totals, checks, covers, lvVariants, attachments, extras, nextVersion: (get('SELECT MAX(version) v FROM offers WHERE project_id = ?', p.id).v || 0) + 1 });
});

r.post('/projects/:id/offers', async (req, res) => {
  const p = projectFull(req.params.id);
  const s = getSettings();
  const b = req.body;
  const sum = must(lvSummary(b.lv_id), 'LV');
  if (sum.lv.project_id !== p.id) throw bad('LV gehört nicht zum Projekt');
  if (!b.parts?.length) throw bad('Keine Bestandteile ausgewählt');
  const version = (get('SELECT MAX(version) v FROM offers WHERE project_id = ?', p.id).v || 0) + 1;
  const offer = { number: `${p.number || `A-${p.id}`}-${String(version).padStart(2, '0')}`, date: new Date() };
  const validUntil = todayIso(Number(b.valid_days ?? s.offer_validity_days) || 30);
  const user = uid(req) ? get('SELECT * FROM users WHERE id = ?', uid(req)) : null;
  const parts = [];
  const log = [];
  for (const part of b.parts) {
    if (part.type === 'cover_generated') { parts.push({ buf: await generateCover({ project: p, customer: customerOf(p), offer, totals: sum.totals, settings: s, user, validUntil }), mime: 'application/pdf', name: 'Deckblatt' }); log.push('Deckblatt (automatisch)'); }
    else if (part.type === 'lv_generated') { parts.push({ buf: await generateLvPdf({ project: p, customer: customerOf(p), offer, positions: sum.positions, settings: s, minMargin: sum.minMargin, longText: b.long_text !== false }), mime: 'application/pdf', name: 'Angebots-LV' }); log.push('Angebots-LV'); }
    else if (part.type === 'lv_overlay') {
      const { buf } = readDocument(sum.lv.source_document_id);
      const o = await overlayOriginal({ originalBuf: buf, positions: sum.positions, settings: s, minMargin: sum.minMargin, offer });
      parts.push({ buf: o.buf, mime: 'application/pdf', name: 'Original-LV' });
      log.push(`Original-LV mit Preisen (${o.placed} eingetragen${o.skipped ? `, ${o.skipped} nicht platzierbar` : ''})`);
    } else if (part.type === 'document') {
      const { doc, buf } = readDocument(part.document_id);
      parts.push({ buf, mime: doc.mime || mimeFor(doc.filename), name: doc.filename });
      log.push(doc.filename);
    }
  }
  let pdf;
  try { pdf = await assemble(parts); } catch (e) { throw bad(e.message); }
  const fileBase = `Angebot_${offer.number}`;
  const docId = saveDocument({ buf: pdf, filename: `${fileBase}.pdf`, mime: 'application/pdf', projectId: p.id, category: 'angebot', note: log.join(' + '), userId: uid(req) });
  const extraDocs = [];
  if (b.extras?.includes('xlsx') && sum.lv.format === 'excel') extraDocs.push(saveDocument({ buf: await fillExcel({ originalBuf: readDocument(sum.lv.source_document_id).buf, meta: sum.lv.meta, positions: sum.positions, minMargin: sum.minMargin }), filename: `${fileBase}.xlsx`, mime: mimeFor('a.xlsx'), projectId: p.id, category: 'angebot', userId: uid(req) }));
  if (b.extras?.includes('gaeb') && sum.lv.format === 'gaeb-xml') extraDocs.push(saveDocument({ buf: Buffer.from(fillGaeb({ originalXml: readDocument(sum.lv.source_document_id).buf.toString('utf8'), positions: sum.positions, minMargin: sum.minMargin }).xml), filename: `${fileBase}.X84`, mime: 'application/xml', projectId: p.id, category: 'angebot', userId: uid(req) }));
  const mail = offerMail({ project: p, customer: customerOf(p), offer, totals: sum.totals, validUntil, user, settings: s });
  const id = insert('offers', { project_id: p.id, lv_id: sum.lv.id, version, document_id: docId, number: offer.number, total_net: sum.totals.net, total_gross: sum.totals.gross, margin_pct: sum.totals.margin_pct, parts: { order: log, extraDocs, validUntil }, mail_subject: mail.subject, mail_body: mail.body, created_by: uid(req) });
  advanceProject(p.id, 'angebot_fertig', uid(req));
  audit({ userId: uid(req), projectId: p.id, entity: 'offer', entityId: id, action: 'Angebot erstellt', details: { number: offer.number, net: sum.totals.net, parts: log } });
  res.json({ id, document_id: docId, number: offer.number, extraDocs });
});

r.get('/offers/:id', (req, res) => {
  const o = must(parseJsonCols(get('SELECT * FROM offers WHERE id = ?', req.params.id)), 'Angebot');
  const p = projectFull(o.project_id);
  o.to = p.contact_email || p.customer_email || '';
  res.json(o);
});
r.patch('/offers/:id', (req, res) => { update('offers', req.params.id, req.body, ['mail_subject', 'mail_body']); res.json({ ok: true }); });

r.get('/offers/:id/eml', (req, res) => {
  const o = must(parseJsonCols(get('SELECT * FROM offers WHERE id = ?', req.params.id)), 'Angebot');
  const p = projectFull(o.project_id);
  const atts = [o.document_id, ...(o.parts?.extraDocs || [])].filter(Boolean).map((id) => { const { doc, buf } = readDocument(id); return { filename: doc.filename, mime: doc.mime, data: buf }; });
  const eml = buildEml({ to: req.query.to || p.contact_email || p.customer_email || '', subject: o.mail_subject, body: o.mail_body, attachments: atts, from: getSettings().company_email });
  sendFile(res, Buffer.from(eml), `Angebot_${o.number}.eml`, 'message/rfc822');
});

/** Benutzer bestätigt den Versand (Mail wurde von ihm verschickt) → Status + Wiedervorlage. */
r.post('/offers/:id/sent', (req, res) => {
  const o = must(get('SELECT * FROM offers WHERE id = ?', req.params.id), 'Angebot');
  const s = getSettings();
  const days = Number(req.body.followup_days ?? s.followup_days) || 7;
  tx(() => {
    update('offers', o.id, { status: 'versendet', sent_at: new Date().toISOString(), mail_subject: req.body.subject ?? o.mail_subject, mail_body: req.body.body ?? o.mail_body });
    insert('followups', { project_id: o.project_id, offer_id: o.id, due_date: todayIso(days), note: `Nachfassen Angebot ${o.number}` });
    insert('notes', { project_id: o.project_id, kind: 'mail', text: `Angebot ${o.number} versendet${req.body.to ? ` an ${req.body.to}` : ''}.\nBetreff: ${req.body.subject ?? o.mail_subject}\n\n${req.body.body ?? o.mail_body}`, created_by: uid(req) });
  });
  advanceProject(o.project_id, 'versendet', uid(req));
  audit({ userId: uid(req), projectId: o.project_id, entity: 'offer', entityId: o.id, action: 'Angebot versendet', details: { followup_in_days: days } });
  res.json({ ok: true });
});

// ---------- Wiedervorlagen ----------
r.get('/followups', (req, res) => {
  const today = todayIso();
  const scope = req.query.scope || 'open';
  const cond = { today: "f.status = 'offen' AND f.due_date = ?", overdue: "f.status = 'offen' AND f.due_date < ?", open: "f.status = 'offen' AND ? = ?", done: "f.status = 'erledigt' AND ? = ?" }[scope] || "f.status = 'offen'";
  const params = scope === 'open' || scope === 'done' ? [today, today] : [today];
  res.json(all(`SELECT f.*, p.name project_name, p.number project_number, p.contact_name, p.contact_phone, c.name customer_name, c.phone customer_phone, o.number offer_number, o.total_net
    FROM followups f JOIN projects p ON p.id = f.project_id LEFT JOIN customers c ON c.id = p.customer_id LEFT JOIN offers o ON o.id = f.offer_id WHERE ${cond} ORDER BY f.due_date ${scope === 'done' ? 'DESC' : 'ASC'} LIMIT 300`, ...params));
});
r.post('/projects/:id/followups', (req, res) => {
  if (!req.body.due_date) throw bad('Datum fehlt');
  const id = insert('followups', { project_id: req.params.id, offer_id: req.body.offer_id || null, due_date: req.body.due_date, note: req.body.note });
  res.json({ id });
});
r.patch('/followups/:id', (req, res) => {
  const f = must(get('SELECT * FROM followups WHERE id = ?', req.params.id), 'Wiedervorlage');
  const { result, note, due_date: due } = req.body;
  const s = getSettings();
  tx(() => {
    if (result) {
      update('followups', f.id, { status: 'erledigt', result, note: note ?? f.note, done_at: new Date().toISOString() });
      const statusMap = { auftrag: 'auftrag', verloren: 'verloren', offen: 'nachfassen', erneut: 'nachfassen' };
      run('UPDATE projects SET status = ?, status_changed_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE id = ?', statusMap[result], f.project_id);
      if (result === 'erneut' || result === 'offen') insert('followups', { project_id: f.project_id, offer_id: f.offer_id, due_date: due || todayIso(Number(s.followup_days) || 7), note: note || f.note });
      if (note) insert('notes', { project_id: f.project_id, kind: 'telefon', text: `Nachfassen: ${note}`, created_by: uid(req) });
      audit({ userId: uid(req), projectId: f.project_id, entity: 'followup', entityId: f.id, action: `Nachfassen: ${result}`, details: { note } });
    } else update('followups', f.id, req.body, ['due_date', 'note']);
  });
  res.json({ ok: true });
});
r.delete('/followups/:id', (req, res) => { run('DELETE FROM followups WHERE id = ?', req.params.id); res.json({ ok: true }); });

export default r;
