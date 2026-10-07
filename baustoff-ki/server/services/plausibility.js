// Plausibilitätsprüfung: markiert Unsicherheiten, entscheidet aber nie selbst (Human-in-the-Loop)
import { KNOWN_UNITS, slug } from '../../shared/format.js';
import { ACCESSORY_RULES, groupMode } from '../../shared/groups.js';

const EXPECTED_UNITS = {
  Pflaster: ['m²', 'St', 't'], Platten: ['m²', 'St'], Bordsteine: ['m', 'St'], Rohre: ['m', 'St'], Rinnen: ['m', 'St'], Schächte: ['St'],
  'Schüttgüter': ['t', 'm³', 'm²'], Stelzlager: ['St', 'm²'], Terrassen: ['m²', 'St', 'm'], Fugenmaterial: ['kg', 't', 'Sack', 'm²'],
  Bewehrung: ['kg', 't', 'St', 'm²', 'm'], Beton: ['m³', 'm²'], Kalksandstein: ['m²', 'St', 'm³', 'm'], Mauerwerk: ['m²', 'St', 'm³', 'm'],
  'Dämmung': ['m²', 'm³', 'm'], Fahrradständer: ['St'], Stadtmobiliar: ['St'],
};

// Nur dreiteilige Formate (L/B/H) vergleichen – zweiteilige Angaben wie C12/15 oder H 15/30 sind mehrdeutig
const formats = (t) => [...String(t || '').matchAll(/(?<![\w/])(\d{2,3})\s*[/x×]\s*(\d{1,3})\s*[/x×]\s*(\d{1,2}(?:,\d)?)(?![\w/])/gi)].map((m) => m.slice(1, 4).join('/'));

/** Prüft alle Positionen. Bereits als erledigt markierte Hinweise bleiben erledigt. Liefert {positions:[{id, flags}], lvIssues}. */
export function checkLv(positions, { mode = 'galabau' } = {}) {
  const out = [];
  const dupKey = new Map();
  for (const p of positions) {
    if (p.pos_type === 'T') continue;
    const k = `${slug(p.short_text)}|${p.qty}|${p.unit}`;
    if (p.short_text && p.pos_type === 'N') dupKey.set(k, [...(dupKey.get(k) || []), p.oz || p.id]);
  }
  for (const p of positions) {
    const prev = Array.isArray(p.flags) ? p.flags : [];
    const keep = prev.filter((f) => ['qty_from_text', 'qty_unreadable', 'gaeb90'].includes(f.key) || f.key.startsWith('ai_'));
    const flags = [...keep];
    const add = (key, severity, msg, suggestion) => { if (!flags.some((f) => f.key === key)) flags.push({ key, severity, msg, ...(suggestion ? { suggestion } : {}) }); };
    if (p.pos_type !== 'T') {
      if (p.qty === null || p.qty === undefined) add('qty_missing', 'error', 'Menge fehlt – nicht eindeutig bestimmbar', 'Menge aus LV/Plan ergänzen oder beim Kunden erfragen');
      else if (Number(p.qty) === 0) add('qty_zero', 'warn', 'Menge ist 0');
      else if (Number(p.qty) > 100000) add('qty_outlier', 'warn', 'Ungewöhnlich große Menge – Tausendertrennzeichen/Komma prüfen');
      if (p.unit === 'St' && p.qty && !Number.isInteger(Number(p.qty))) add('qty_fraction', 'warn', 'Stückzahl mit Nachkommastellen');
      if (!p.unit) add('unit_missing', 'error', 'Einheit fehlt');
      else if (!KNOWN_UNITS.includes(p.unit)) add('unit_unknown', 'warn', `Unbekannte Einheit „${p.unit}“`);
      if (p.group_name && p.unit && EXPECTED_UNITS[p.group_name] && !EXPECTED_UNITS[p.group_name].includes(p.unit)) add('unit_group', 'warn', `Ungewöhnliche Einheit „${p.unit}“ für ${p.group_name}`, 'Umrechnung (z. B. m² ↔ St, m³ ↔ t) beim Lieferanten klären');
      if (!p.group_name) add('group_unknown', 'warn', 'Produkt/Warengruppe nicht erkannt', 'Warengruppe oder Produkt manuell zuordnen');
      else if (groupMode(p.group_name) && groupMode(p.group_name) !== mode) add('mode_other', 'info', `Gehört eher zum Bereich ${groupMode(p.group_name) === 'hochbau' ? 'Hochbau' : 'Tief-/GaLa-Bau'}`);
      if (!p.short_text || p.short_text.trim().length < 4) add('text_short', 'warn', 'Positionstext unvollständig');
      if (/\bwie (vor|pos)/i.test(`${p.short_text} ${p.long_text}`)) add('ref_text', 'info', 'Verweis „wie vor/wie Pos.“ – Bezugsposition prüfen');
      const fs = formats(p.short_text), fl = formats(p.long_text);
      if (fs.length && fl.length && !fl.includes(fs[0]) && !fs.includes(fl[0])) add('format_conflict', 'warn', `Widersprüchliche Maße: Kurztext ${fs[0]} / Langtext ${fl[0]}`);
      const k = `${slug(p.short_text)}|${p.qty}|${p.unit}`;
      if (p.pos_type === 'N' && (dupKey.get(k) || []).length > 1) add('duplicate', 'warn', `Mögliche Doppelposition (${dupKey.get(k).filter((x) => x !== (p.oz || p.id)).join(', ')})`);
      if (p.group_name === 'Pflaster') {
        const h = String(`${p.short_text} ${p.long_text}`).match(/\/(\d{1,2})\s*cm/);
        if (h && (Number(h[1]) < 4 || Number(h[1]) > 18)) add('format_unusual', 'info', `Ungewöhnliche Pflasterdicke ${h[1]} cm`);
      }
    }
    for (const f of flags) { const old = prev.find((x) => x.key === f.key); if (old?.resolved) f.resolved = true; }
    out.push({ id: p.id, flags });
  }
  const groups = new Set(positions.map((p) => p.group_name).filter(Boolean));
  const allText = slug(positions.map((p) => `${p.short_text} ${p.long_text}`).join(' '));
  const lvIssues = [];
  for (const r of ACCESSORY_RULES) {
    if (!groups.has(r.if)) continue;
    const okGroup = (r.needAny || []).some((g) => groups.has(g));
    const okText = (r.needText || []).some((t) => allText.includes(t));
    if (!okGroup && !okText) lvIssues.push({ key: `acc_${slug(r.if)}`, severity: 'info', msg: `Fehlendes Zubehör? ${r.msg}` });
  }
  return { positions: out, lvIssues };
}
