// Zahlen-, Einheiten- und Formatierungshelfer (Server + Client)

/** Deutsche Zahl ("1.234,56", "12,5", "1234.5") in Number umwandeln. Gibt null bei Unlesbarem zurück. */
export function parseNum(v) {
  if (v === null || v === undefined || v === '') return null;
  if (typeof v === 'number') return Number.isFinite(v) ? v : null;
  let s = String(v).trim().replace(/\s|€|EUR/gi, '');
  if (!s) return null;
  if (s.includes(',')) s = s.replace(/\./g, '').replace(',', '.');
  else if (/^-?\d{1,3}(\.\d{3})+$/.test(s)) s = s.replace(/\./g, '');
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

export const round = (n, d = 2) => (n === null || n === undefined || !Number.isFinite(n) ? null : Math.round((n + Number.EPSILON) * 10 ** d) / 10 ** d);
export const ceilTo = (n, step = 1) => Math.ceil(n / step - 1e-9) * step;

const nf = (d) => new Intl.NumberFormat('de-DE', { minimumFractionDigits: d, maximumFractionDigits: d });
const nfCache = {};
export function fmtNum(n, d = 2) {
  if (n === null || n === undefined || !Number.isFinite(Number(n))) return '';
  return (nfCache[d] ||= nf(d)).format(Number(n));
}
export const fmtEUR = (n) => (n === null || n === undefined || !Number.isFinite(Number(n)) ? '–' : `${fmtNum(n, 2)} €`);
export const fmtPct = (n, d = 1) => (n === null || n === undefined || !Number.isFinite(Number(n)) ? '–' : `${fmtNum(n, d)} %`);
const qtyFmt = new Intl.NumberFormat('de-DE', { minimumFractionDigits: 0, maximumFractionDigits: 3 });
export function fmtQty(n) {
  if (n === null || n === undefined || n === '' || !Number.isFinite(Number(n))) return '';
  return qtyFmt.format(Number(n));
}
export function fmtDate(d) {
  if (!d) return '';
  const dt = new Date(d);
  return Number.isNaN(dt.getTime()) ? String(d) : dt.toLocaleDateString('de-DE', { day: '2-digit', month: '2-digit', year: 'numeric' });
}
export function fmtDateTime(d) {
  if (!d) return '';
  const dt = new Date(d);
  return Number.isNaN(dt.getTime()) ? String(d) : dt.toLocaleString('de-DE', { dateStyle: 'short', timeStyle: 'short' });
}

const UNIT_MAP = [
  [/^(m2|m²|qm|m\^2)$/i, 'm²'],
  [/^(m3|m³|cbm|m\^3)$/i, 'm³'],
  [/^(st|stk|stck|stück|stueck|stk\.|st\.)$/i, 'St'],
  [/^(m|lfm|lfdm|lm|lfd\.?\s?m|meter)$/i, 'm'],
  [/^(t|to|tonne|tonnen)$/i, 't'],
  [/^(kg)$/i, 'kg'],
  [/^(psch|pschl|pausch|pauschal|pa|ps)$/i, 'psch'],
  [/^(h|std|stunde|stunden)$/i, 'h'],
  [/^(l|ltr|liter)$/i, 'l'],
  [/^(sack|sck|sa)$/i, 'Sack'],
  [/^(pal|palette)$/i, 'Pal'],
  [/^(satz|set)$/i, 'Satz'],
  [/^(paar|pr)$/i, 'Paar'],
];
export const KNOWN_UNITS = ['m²', 'm³', 'm', 'St', 't', 'kg', 'psch', 'h', 'l', 'Sack', 'Pal', 'Satz', 'Paar'];

/** Einheit vereinheitlichen; unbekannte Einheiten bleiben unverändert (werden nicht erfunden). */
export function normalizeUnit(u) {
  if (!u) return null;
  const s = String(u).trim().replace(/\.$/, '');
  for (const [re, val] of UNIT_MAP) if (re.test(s)) return val;
  return s || null;
}
export const UNIT_REGEX_SRC = 'm²|m2|m³|m3|qm|cbm|lfm|lfdm|lfd\\.?\\s?m|stück|stck|stk|st|psch|pschl|pauschal|t|to|kg|h|std|l|sack|pal|satz|paar|m';

export function slug(s) {
  return String(s || '').toLowerCase().normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/ß/g, 'ss').replace(/[^a-z0-9]+/g, ' ').trim();
}

/** Ähnlichkeit zweier Texte (Token-Überlappung, 0..1) – für Zuordnung von Angebotszeilen zu LV-Positionen. */
export function textSimilarity(a, b) {
  const tok = (s) => new Set(slug(s).split(' ').filter((t) => t.length > 1));
  const A = tok(a), B = tok(b);
  if (!A.size || !B.size) return 0;
  let inter = 0;
  for (const t of A) if (B.has(t)) inter++;
  return inter / Math.min(A.size, B.size) * (2 * inter / (A.size + B.size)) ** 0.5;
}
