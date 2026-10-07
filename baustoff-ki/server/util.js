// Gemeinsame Server-Helfer: Dokumentablage, Projektstatus, Positionsaufbereitung
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import os from 'node:os';
import { FILES_DIR, get, all, insert, run, audit, parseJsonCols, getSettings } from './db.js';
import { STATUS_ORDER, FINAL_STATUS, positionState } from '../shared/status.js';
import { calcPosition, calcTotals } from '../shared/pricing.js';

export class HttpError extends Error { constructor(status, msg) { super(msg); this.status = status; } }
export const bad = (msg) => new HttpError(400, msg);
export const notFound = (what = 'Eintrag') => new HttpError(404, `${what} nicht gefunden`);
export const uid = (req) => Number(req.get('x-user-id')) || null;
export const must = (row, what) => { if (!row) throw notFound(what); return row; };

/** Datei unverändert ablegen (Original bleibt immer erhalten) und als Dokument registrieren. */
export function saveDocument({ buf, filename, mime, projectId = null, category = 'sonstiges', note = null, userId = null }) {
  const sha = crypto.createHash('sha256').update(buf).digest('hex');
  const dir = path.join(FILES_DIR, new Date().toISOString().slice(0, 7));
  fs.mkdirSync(dir, { recursive: true });
  const ext = path.extname(filename).toLowerCase().replace(/[^.a-z0-9]/g, '');
  const rel = path.relative(FILES_DIR, path.join(dir, `${crypto.randomUUID()}${ext}`));
  fs.writeFileSync(path.join(FILES_DIR, rel), buf, { flag: 'wx' });
  const version = (get('SELECT MAX(version) v FROM documents WHERE project_id IS ? AND category = ? AND filename = ?', projectId, category, filename)?.v || 0) + 1;
  const id = insert('documents', { project_id: projectId, category, filename, mime, size: buf.length, sha256: sha, path: rel, version, note, created_by: userId });
  audit({ userId, projectId, entity: 'document', entityId: id, action: 'hochgeladen', details: { filename, category, version } });
  return id;
}
export function readDocument(id) {
  const d = must(get('SELECT * FROM documents WHERE id = ?', id), 'Dokument');
  return { doc: d, buf: fs.readFileSync(path.join(FILES_DIR, d.path)) };
}

/** Projektstatus nur vorwärts bewegen (manuelle Änderungen gehen über PATCH). */
export function advanceProject(projectId, status, userId = null) {
  const p = get('SELECT status FROM projects WHERE id = ?', projectId);
  if (!p || FINAL_STATUS.includes(p.status) || STATUS_ORDER[status] <= STATUS_ORDER[p.status]) return false;
  run('UPDATE projects SET status = ?, status_changed_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE id = ?', status, projectId);
  audit({ userId, projectId, entity: 'project', entityId: projectId, action: 'status', details: { from: p.status, to: status, auto: true } });
  return true;
}
export const touchProject = (id) => run('UPDATE projects SET updated_at = CURRENT_TIMESTAMP WHERE id = ?', id);

/** Positionen eines LV inkl. Kalkulation, Ampelstatus, Lieferant/Produkt. */
export function loadPositions(lvId, minMargin) {
  const rows = all(`SELECT p.*, s.name supplier_name, pr.name product_name,
      (SELECT r.status FROM supplier_request_items ri JOIN supplier_requests r ON r.id = ri.request_id WHERE ri.position_id = p.id ORDER BY r.id DESC LIMIT 1) request_status
    FROM lv_positions p LEFT JOIN suppliers s ON s.id = p.supplier_id LEFT JOIN products pr ON pr.id = p.product_id
    WHERE p.lv_id = ? ORDER BY p.sort, p.id`, lvId).map(parseJsonCols);
  for (const p of rows) {
    p.calc = calcPosition(p, minMargin);
    p.state = positionState(p, p.calc, p.request_status && !['vorbereitet'].includes(p.request_status));
  }
  return rows;
}

export function lvSummary(lvId) {
  const lv = parseJsonCols(get('SELECT * FROM lvs WHERE id = ?', lvId));
  if (!lv) return null;
  const s = getSettings();
  const minMargin = lv.min_margin ?? s.min_margin;
  const positions = loadPositions(lvId, minMargin);
  return { lv, positions, totals: calcTotals(positions, { minMargin, vatPct: s.vat_pct }), minMargin };
}

/** Prüft, ob alle preisrelevanten Positionen einen EK haben → Status "Preise vollständig". */
export function checkPricesComplete(projectId, userId) {
  const missing = get(`SELECT COUNT(*) n FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE l.project_id = ? AND p.pos_type != 'T' AND p.ek IS NULL`, projectId).n;
  const total = get(`SELECT COUNT(*) n FROM lv_positions p JOIN lvs l ON l.id = p.lv_id WHERE l.project_id = ? AND p.pos_type != 'T'`, projectId).n;
  if (total && !missing) advanceProject(projectId, 'preise_vollstaendig', userId);
  return { missing, total };
}

export const sendFile = (res, buf, filename, mime) => {
  res.setHeader('Content-Type', mime || 'application/octet-stream');
  res.setHeader('Content-Disposition', `attachment; filename*=UTF-8''${encodeURIComponent(filename)}`);
  res.send(buf);
};
export const todayIso = (offsetDays = 0) => { const d = new Date(); d.setDate(d.getDate() + offsetDays); return d.toISOString().slice(0, 10); };

/** IPv4-Adressen im lokalen Netz – damit das iPhone den Server im WLAN findet. */
export const lanAddresses = () => Object.values(os.networkInterfaces()).flat().filter((n) => n && n.family === 'IPv4' && !n.internal).map((n) => n.address);
