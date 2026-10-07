// Kalkulationslogik: EK → Einstand → VK → Gesamt. Einzige Quelle der Wahrheit für Server und Client.
import { round } from './format.js';

// Positionsarten: N = Normalposition, B = Bedarfs-/Eventualposition, A = Alternativposition, T = Texthinweis, P = Pauschal
export const POS_TYPES = { N: 'Normal', B: 'Bedarf', A: 'Alternativ', T: 'Text' };
export const countsInTotal = (p) => !p.pos_type || p.pos_type === 'N';
export const isPriceable = (p) => p.pos_type !== 'T';

/** Berechnet alle Preisfelder einer Position. Fehlende Werte bleiben null (keine erfundenen Preise). */
export function calcPosition(p, minMargin = null) {
  const ek = p.ek === null || p.ek === undefined || p.ek === '' ? null : Number(p.ek);
  const qty = p.qty === null || p.qty === undefined ? null : Number(p.qty);
  const disc = Number(p.discount_pct) || 0;
  const freight = Number(p.freight) || 0;
  const markup = p.markup_pct === null || p.markup_pct === undefined || p.markup_pct === '' ? null : Number(p.markup_pct);
  const r = { einstand: null, vk: null, total: null, db: null, db_total: null, margin_pct: null, below_min: false, ek_total: null };
  if (ek === null) return r;
  r.einstand = round(ek * (1 - disc / 100) + freight, 4);
  if (markup !== null) {
    r.vk = p.vk_override !== null && p.vk_override !== undefined && p.vk_override !== '' ? round(Number(p.vk_override)) : round(r.einstand * (1 + markup / 100));
    r.db = round(r.vk - r.einstand, 4);
    r.margin_pct = r.vk > 0 ? round((r.db / r.vk) * 100, 2) : null;
    if (qty !== null) {
      r.total = round(r.vk * qty);
      r.db_total = round(r.db * qty);
    }
    if (minMargin !== null && minMargin !== undefined && r.margin_pct !== null && r.margin_pct < Number(minMargin)) r.below_min = true;
  }
  if (qty !== null) r.ek_total = round(r.einstand * qty);
  return r;
}

/** Summen über ein LV. Nur Normalpositionen zählen in die Angebotssumme. */
export function calcTotals(positions, { minMargin = null, vatPct = 19 } = {}) {
  const t = { einstand: 0, net: 0, db: 0, count: 0, priced: 0, missing: 0, belowMin: 0, margin_pct: null, vat: 0, gross: 0, optional: 0 };
  for (const p of positions) {
    if (!isPriceable(p)) continue;
    const c = calcPosition(p, minMargin);
    if (!countsInTotal(p)) { if (c.total !== null) t.optional += c.total; continue; }
    t.count++;
    if (c.total === null) { t.missing++; continue; }
    t.priced++;
    t.net += c.total;
    t.einstand += c.ek_total ?? 0;
    t.db += c.db_total ?? 0;
    if (c.below_min) t.belowMin++;
  }
  t.net = round(t.net);
  t.einstand = round(t.einstand);
  t.db = round(t.db);
  t.optional = round(t.optional);
  t.margin_pct = t.net > 0 ? round((t.db / t.net) * 100, 2) : null;
  t.vat = round(t.net * (vatPct / 100));
  t.gross = round(t.net + t.vat);
  return t;
}

/** Auswertung von Varianten (Lieferantenpreisen) für eine Position. */
export function rankVariants(variants, { preferredSupplierIds = [] } = {}) {
  const v = variants.map((x) => {
    const einstand = x.price === null ? null : round(x.price * (1 - (Number(x.discount_pct) || 0) / 100) + (Number(x.freight) || 0), 4);
    return { ...x, einstand };
  });
  const valid = v.filter((x) => x.einstand !== null);
  const min = (arr, f) => arr.reduce((a, b) => (a === null || f(b) < f(a) ? b : a), null);
  const cheapestEk = min(valid, (x) => x.price);
  const bestTotal = min(valid, (x) => x.einstand);
  const withDays = v.filter((x) => Number.isFinite(x.delivery_days));
  const fastest = min(withDays, (x) => x.delivery_days);
  return v.map((x) => ({
    ...x,
    tags: [
      x === cheapestEk && 'günstigster EK',
      x === bestTotal && 'bester Einstand',
      x === fastest && 'schnellste Lieferung',
      preferredSupplierIds.includes(x.supplier_id) && 'bevorzugt',
    ].filter(Boolean),
  }));
}
