// SQLite-Datenbank (node:sqlite, keine nativen Abhängigkeiten) inkl. Schema und Helfern
import { DatabaseSync } from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';

export const DATA_DIR = path.resolve(process.env.DATA_DIR || path.join(import.meta.dirname, '..', 'data'));
export const FILES_DIR = path.join(DATA_DIR, 'files');
fs.mkdirSync(FILES_DIR, { recursive: true });

export const db = new DatabaseSync(process.env.DB_FILE || path.join(DATA_DIR, 'baustoff-ki.sqlite'));
db.exec('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;');

const SCHEMA = `
CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY, name TEXT NOT NULL, email TEXT, role TEXT DEFAULT 'mitarbeiter', active INTEGER DEFAULT 1, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY, name TEXT NOT NULL, contact TEXT, email TEXT, phone TEXT, address TEXT, notes TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS projects (
  id INTEGER PRIMARY KEY, number TEXT, name TEXT NOT NULL, customer_id INTEGER REFERENCES customers(id) ON DELETE SET NULL,
  contact_name TEXT, contact_email TEXT, contact_phone TEXT, site TEXT,
  mode TEXT NOT NULL DEFAULT 'galabau', detected_mode TEXT, detected_confidence REAL,
  status TEXT NOT NULL DEFAULT 'neu', status_changed_at TEXT DEFAULT CURRENT_TIMESTAMP, due_date TEXT, notes TEXT,
  cover_document_id INTEGER, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS documents (
  id INTEGER PRIMARY KEY, project_id INTEGER REFERENCES projects(id) ON DELETE CASCADE, category TEXT NOT NULL, filename TEXT NOT NULL,
  mime TEXT, size INTEGER, sha256 TEXT, path TEXT NOT NULL, version INTEGER DEFAULT 1, note TEXT, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS lvs (
  id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, name TEXT NOT NULL,
  source_document_id INTEGER REFERENCES documents(id) ON DELETE SET NULL, format TEXT, parse_method TEXT, parse_info TEXT,
  meta TEXT DEFAULT '{}', issues TEXT DEFAULT '[]', global_markup REAL, min_margin REAL,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP, updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS lv_positions (
  id INTEGER PRIMARY KEY, lv_id INTEGER NOT NULL REFERENCES lvs(id) ON DELETE CASCADE, sort INTEGER DEFAULT 0, oz TEXT, title_path TEXT,
  short_text TEXT, long_text TEXT, qty REAL, unit TEXT, pos_type TEXT DEFAULT 'N', group_name TEXT,
  product_id INTEGER REFERENCES products(id) ON DELETE SET NULL, supplier_id INTEGER REFERENCES suppliers(id) ON DELETE SET NULL,
  ek REAL, discount_pct REAL DEFAULT 0, freight REAL DEFAULT 0, markup_pct REAL, markup_source TEXT DEFAULT 'global', vk_override REAL,
  price_source TEXT, price_date TEXT, quote_item_id INTEGER, confidence REAL, flags TEXT DEFAULT '[]', layout TEXT, notes TEXT,
  updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS ix_pos_lv ON lv_positions(lv_id, sort);
CREATE TABLE IF NOT EXISTS calculations (id INTEGER PRIMARY KEY, lv_id INTEGER NOT NULL REFERENCES lvs(id) ON DELETE CASCADE, scope TEXT NOT NULL, group_name TEXT, markup_pct REAL, updated_at TEXT DEFAULT CURRENT_TIMESTAMP, UNIQUE(lv_id, scope, group_name));
CREATE TABLE IF NOT EXISTS plans (
  id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, document_id INTEGER REFERENCES documents(id) ON DELETE SET NULL,
  name TEXT, kind TEXT, analysis TEXT DEFAULT '{}', measurements TEXT DEFAULT '[]', runs TEXT DEFAULT '[]', status TEXT DEFAULT 'neu',
  created_at TEXT DEFAULT CURRENT_TIMESTAMP, updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS products (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, manufacturer TEXT, group_name TEXT, mode TEXT, unit TEXT, format TEXT, article_no TEXT,
  specs TEXT, notes TEXT, alternatives TEXT DEFAULT '[]', created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS suppliers (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, phone TEXT, email TEXT, address TEXT, groups TEXT DEFAULT '[]', delivery_days INTEGER,
  preferred INTEGER DEFAULT 0, notes TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS supplier_contacts (id INTEGER PRIMARY KEY, supplier_id INTEGER NOT NULL REFERENCES suppliers(id) ON DELETE CASCADE, name TEXT NOT NULL, role TEXT, phone TEXT, email TEXT);
CREATE TABLE IF NOT EXISTS supplier_requests (
  id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, supplier_id INTEGER NOT NULL REFERENCES suppliers(id) ON DELETE CASCADE,
  contact_id INTEGER REFERENCES supplier_contacts(id) ON DELETE SET NULL, channel TEXT DEFAULT 'mail', status TEXT DEFAULT 'vorbereitet',
  subject TEXT, body TEXT, desired_date TEXT, sent_at TEXT, call_notes TEXT, call_result TEXT,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP, updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS supplier_request_items (id INTEGER PRIMARY KEY, request_id INTEGER NOT NULL REFERENCES supplier_requests(id) ON DELETE CASCADE, position_id INTEGER NOT NULL REFERENCES lv_positions(id) ON DELETE CASCADE, UNIQUE(request_id, position_id));
CREATE TABLE IF NOT EXISTS supplier_quotes (
  id INTEGER PRIMARY KEY, project_id INTEGER REFERENCES projects(id) ON DELETE CASCADE, supplier_id INTEGER REFERENCES suppliers(id) ON DELETE SET NULL,
  request_id INTEGER REFERENCES supplier_requests(id) ON DELETE SET NULL, document_id INTEGER REFERENCES documents(id) ON DELETE SET NULL,
  source TEXT DEFAULT 'angebot', quote_no TEXT, valid_until TEXT, freight_total REAL, discount_pct REAL, delivery_time TEXT, notes TEXT,
  created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS supplier_quote_items (
  id INTEGER PRIMARY KEY, quote_id INTEGER NOT NULL REFERENCES supplier_quotes(id) ON DELETE CASCADE, position_id INTEGER REFERENCES lv_positions(id) ON DELETE SET NULL,
  text TEXT, article_no TEXT, qty REAL, unit TEXT, price REAL, discount_pct REAL DEFAULT 0, freight REAL DEFAULT 0, factor REAL DEFAULT 1,
  delivery_days INTEGER, delivery_text TEXT);
CREATE TABLE IF NOT EXISTS prices (
  id INTEGER PRIMARY KEY, product_id INTEGER REFERENCES products(id) ON DELETE SET NULL, supplier_id INTEGER REFERENCES suppliers(id) ON DELETE SET NULL,
  project_id INTEGER REFERENCES projects(id) ON DELETE SET NULL, position_id INTEGER, text TEXT, group_name TEXT, price REAL NOT NULL, unit TEXT, qty REAL,
  date TEXT DEFAULT CURRENT_DATE, valid_until TEXT, source TEXT, quote_id INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS ix_prices_product ON prices(product_id);
CREATE TABLE IF NOT EXISTS offers (
  id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, lv_id INTEGER REFERENCES lvs(id) ON DELETE SET NULL,
  version INTEGER DEFAULT 1, document_id INTEGER REFERENCES documents(id) ON DELETE SET NULL, number TEXT, total_net REAL, total_gross REAL, margin_pct REAL,
  parts TEXT DEFAULT '[]', status TEXT DEFAULT 'erstellt', mail_subject TEXT, mail_body TEXT, sent_at TEXT, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS followups (
  id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, offer_id INTEGER REFERENCES offers(id) ON DELETE SET NULL,
  due_date TEXT NOT NULL, status TEXT DEFAULT 'offen', result TEXT, note TEXT, done_at TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS notes (id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE, kind TEXT DEFAULT 'notiz', text TEXT NOT NULL, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
CREATE TABLE IF NOT EXISTS audit_logs (id INTEGER PRIMARY KEY, ts TEXT DEFAULT CURRENT_TIMESTAMP, user_id INTEGER, project_id INTEGER, entity TEXT, entity_id INTEGER, action TEXT, details TEXT);
CREATE INDEX IF NOT EXISTS ix_audit_project ON audit_logs(project_id, ts);
CREATE TABLE IF NOT EXISTS knowledge (id INTEGER PRIMARY KEY, kind TEXT DEFAULT 'notiz', title TEXT NOT NULL, body TEXT, tags TEXT, product_id INTEGER REFERENCES products(id) ON DELETE SET NULL, supplier_id INTEGER REFERENCES suppliers(id) ON DELETE SET NULL, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, updated_at TEXT DEFAULT CURRENT_TIMESTAMP);
`;
db.exec(SCHEMA);

// ---------- Helfer ----------
export const all = (sql, ...p) => db.prepare(sql).all(...p).map(plain);
export const get = (sql, ...p) => { const r = db.prepare(sql).get(...p); return r ? plain(r) : null; };
export const run = (sql, ...p) => db.prepare(sql).run(...p);
function plain(r) { return { ...r }; }

export function tx(fn) {
  db.exec('BEGIN');
  try { const r = fn(); db.exec('COMMIT'); return r; } catch (e) { db.exec('ROLLBACK'); throw e; }
}

const JSON_COLS = new Set(['meta', 'issues', 'flags', 'layout', 'analysis', 'measurements', 'runs', 'groups', 'alternatives', 'parts', 'details', 'parse_info']);
export const parseJsonCols = (row) => {
  if (!row) return row;
  for (const k of Object.keys(row)) if (JSON_COLS.has(k) && typeof row[k] === 'string') { try { row[k] = JSON.parse(row[k]); } catch { /* bleibt String */ } }
  return row;
};
const enc = (k, v) => (JSON_COLS.has(k) && v !== null && typeof v === 'object' ? JSON.stringify(v) : v === undefined ? null : typeof v === 'boolean' ? (v ? 1 : 0) : v);

export function insert(table, obj) {
  const keys = Object.keys(obj).filter((k) => obj[k] !== undefined);
  const r = run(`INSERT INTO ${table} (${keys.join(',')}) VALUES (${keys.map(() => '?').join(',')})`, ...keys.map((k) => enc(k, obj[k])));
  return Number(r.lastInsertRowid);
}
export function update(table, id, obj, allowed) {
  const keys = Object.keys(obj).filter((k) => obj[k] !== undefined && (!allowed || allowed.includes(k)));
  if (!keys.length) return 0;
  const touch = ['projects', 'lvs', 'lv_positions', 'supplier_requests', 'plans', 'knowledge'].includes(table) ? ', updated_at = CURRENT_TIMESTAMP' : '';
  return run(`UPDATE ${table} SET ${keys.map((k) => `${k} = ?`).join(', ')}${touch} WHERE id = ?`, ...keys.map((k) => enc(k, obj[k])), id).changes;
}

// ---------- Einstellungen ----------
export const DEFAULT_SETTINGS = {
  company_name: 'Mein Baustoffhandel GmbH', company_address: 'Musterstraße 1\n12345 Musterstadt', company_phone: '', company_email: '', company_web: '',
  default_mode: 'galabau', default_markup: 8, min_margin: 5, vat_pct: 19, followup_days: 7, offer_validity_days: 30,
  offer_intro: 'Sehr geehrte Damen und Herren,\n\nvielen Dank für Ihre Anfrage. Gerne bieten wir Ihnen wie folgt an:',
  offer_terms: 'Preise zzgl. gesetzlicher MwSt. Lieferung frei Baustelle, sofern nicht anders angegeben. Es gelten unsere AGB.',
  signature: 'Mit freundlichen Grüßen\n\nIhr Verkaufsteam', cover_document_id: null,
  ai_api_key: '', ai_model: 'claude-opus-5-5', ai_effort: 'medium', ai_enabled: true,
};
export function getSettings() {
  const s = { ...DEFAULT_SETTINGS };
  for (const r of all('SELECT key, value FROM settings')) { try { s[r.key] = JSON.parse(r.value); } catch { s[r.key] = r.value; } }
  return s;
}
export function setSettings(obj) {
  tx(() => { for (const [k, v] of Object.entries(obj)) if (k in DEFAULT_SETTINGS) run('INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value', k, JSON.stringify(v)); });
}

// ---------- Audit ----------
export function audit({ userId = null, projectId = null, entity, entityId = null, action, details = null }) {
  run('INSERT INTO audit_logs (user_id, project_id, entity, entity_id, action, details) VALUES (?,?,?,?,?,?)', userId, projectId, entity, entityId, action, details ? JSON.stringify(details) : null);
}

// Erstbenutzer
if (!get('SELECT id FROM users LIMIT 1')) insert('users', { name: 'Admin', role: 'admin' });
