// Material-Kalkulatoren. Jeder Rechner liefert nachvollziehbare Schritte, Annahmen und Ergebnisse.
// Ergebnisformat: { results:[{material, qty, unit, basis, certainty}], steps:[], assumptions:[], warnings:[] }
import { round, ceilTo, fmtNum } from './format.js';

const f = (n, d = 2) => fmtNum(n, d);
const num = (v, def = 0) => (v === '' || v === null || v === undefined || !Number.isFinite(Number(v)) ? def : Number(v));
// info = reine Kontrollgröße (wird nicht in die Materialliste übernommen)
const R = (material, qty, unit, basis, certainty = 'berechnet', group = null, info = false) => ({ material, qty, unit, basis, certainty, group, ...(info ? { info: true } : {}) });

function need(v, keys) {
  const missing = keys.filter(([k]) => !(Number(v[k]) > 0)).map(([, label]) => label);
  if (missing.length) throw new Error(`Nicht eindeutig bestimmbar – bitte angeben: ${missing.join(', ')}`);
}

// ---------- Geometrie für Terrassen/Stelzlager: Vereinigung achsparalleler Rechtecke ----------
export function unionGeometry(rects) {
  const rs = rects.filter((r) => r.w > 0 && r.d > 0).map((r) => ({ x0: num(r.x), y0: num(r.y), x1: num(r.x) + num(r.w), y1: num(r.y) + num(r.d) }));
  if (!rs.length) return { area: 0, perimeter: 0, bbox: null, rs };
  const xs = [...new Set(rs.flatMap((r) => [r.x0, r.x1]))].sort((a, b) => a - b);
  const ys = [...new Set(rs.flatMap((r) => [r.y0, r.y1]))].sort((a, b) => a - b);
  const cov = (i, j) => {
    if (i < 0 || j < 0 || i >= xs.length - 1 || j >= ys.length - 1) return false;
    const cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
    return rs.some((r) => cx > r.x0 && cx < r.x1 && cy > r.y0 && cy < r.y1);
  };
  let area = 0, perimeter = 0;
  for (let i = 0; i < xs.length - 1; i++) {
    for (let j = 0; j < ys.length - 1; j++) {
      if (!cov(i, j)) continue;
      const w = xs[i + 1] - xs[i], h = ys[j + 1] - ys[j];
      area += w * h;
      if (!cov(i - 1, j)) perimeter += h;
      if (!cov(i + 1, j)) perimeter += h;
      if (!cov(i, j - 1)) perimeter += w;
      if (!cov(i, j + 1)) perimeter += w;
    }
  }
  return { area, perimeter, bbox: { x0: xs[0], y0: ys[0], x1: xs.at(-1), y1: ys.at(-1) }, rs };
}

function overlapArea(a, rs) {
  // Rechtecke einer Vereinigung können sich überlappen → über Koordinatenkompression exakt rechnen
  const clip = rs.map((r) => ({ x0: Math.max(a.x0, r.x0), y0: Math.max(a.y0, r.y0), x1: Math.min(a.x1, r.x1), y1: Math.min(a.y1, r.y1) })).filter((r) => r.x1 > r.x0 && r.y1 > r.y0);
  if (!clip.length) return 0;
  if (clip.length === 1) return (clip[0].x1 - clip[0].x0) * (clip[0].y1 - clip[0].y0);
  return unionGeometry(clip.map((r) => ({ x: r.x0, y: r.y0, w: r.x1 - r.x0, d: r.y1 - r.y0 }))).area;
}

export const PEDESTAL_SYSTEMS = [
  { id: 'p10-20', label: 'Stelzlager 10–20 mm (Pad/Flachlager)', min: 10, max: 20 },
  { id: 'p20-35', label: 'Stelzlager 20–35 mm', min: 20, max: 35 },
  { id: 'p35-55', label: 'Stelzlager 35–55 mm', min: 35, max: 55 },
  { id: 'p40-70', label: 'Stelzlager 40–70 mm', min: 40, max: 70 },
  { id: 'p50-80', label: 'Stelzlager 50–80 mm', min: 50, max: 80 },
  { id: 'p60-100', label: 'Stelzlager 60–100 mm', min: 60, max: 100 },
  { id: 'p80-120', label: 'Stelzlager 80–120 mm', min: 80, max: 120 },
  { id: 'p120-220', label: 'Stelzlager 120–220 mm (mit Verlängerung)', min: 120, max: 220 },
  { id: 'p200-400', label: 'Stelzlager 200–400 mm (mit Verlängerung)', min: 200, max: 400 },
];

/** Terrassen-/Balkonplatten auf Stelzlagern inkl. 2D-Layout. */
export function terraceLayout(v) {
  const rects = (v.rects || []).map((r) => ({ x: num(r.x), y: num(r.y), w: num(r.w), d: num(r.d) }));
  const geo = unionGeometry(rects);
  if (!geo.area) throw new Error('Nicht eindeutig bestimmbar – bitte Fläche (mind. ein Rechteck mit Breite und Tiefe) angeben');
  need(v, [['plattenL', 'Plattenlänge'], ['plattenB', 'Plattenbreite']]);
  const L = num(v.plattenL) / 100, B = num(v.plattenB) / 100, fz = num(v.fuge, 4) / 1000;
  const px = L + fz, py = B + fz;
  const { x0, y0, x1, y1 } = geo.bbox;
  let ox = x0, oy = y0;
  if (v.ausrichtung === 'mitte') {
    ox = x0 + (((x1 - x0) % px) - px) / 2;
    oy = y0 + (((y1 - y0) % py) - py) / 2;
    if (ox > x0) ox -= px;
    if (oy > y0) oy -= py;
  }
  const iMax = Math.ceil((x1 - ox) / px), jMax = Math.ceil((y1 - oy) / py);
  if (iMax * jMax > 200000) throw new Error('Fläche/Format ergibt zu viele Platten für die Vorschau – bitte Eingaben prüfen');
  const cells = [];
  const covered = new Set();
  let full = 0, cut = 0, slivers = 0;
  for (let i = 0; i < iMax; i++) {
    for (let j = 0; j < jMax; j++) {
      const t = { x0: ox + i * px, y0: oy + j * py, x1: ox + i * px + L, y1: oy + j * py + B };
      const a = overlapArea(t, geo.rs);
      const c = a / (L * B);
      if (c < 0.001) continue;
      covered.add(`${i},${j}`);
      const type = c > 0.999 ? 'voll' : 'zuschnitt';
      if (type === 'voll') full++; else { cut++; if (c < 0.1) slivers++; }
      cells.push({ i, j, x: t.x0, y: t.y0, w: L, d: B, type, c: round(c, 3) });
    }
  }
  // Stelzlager an Fugenkreuzen: Knoten (i,j) liegt an der linken oberen Ecke von Zelle (i,j)
  const nodes = new Map();
  for (const key of covered) {
    const [i, j] = key.split(',').map(Number);
    for (const [di, dj] of [[0, 0], [1, 0], [0, 1], [1, 1]]) {
      const k = `${i + di},${j + dj}`;
      nodes.set(k, (nodes.get(k) || 0) + 1);
    }
  }
  const nodeList = [];
  let inner = 0, edge = 0, corner = 0;
  for (const [k, n] of nodes) {
    const [i, j] = k.split(',').map(Number);
    const type = n === 4 ? 'innen' : n === 1 ? 'ecke' : 'rand';
    if (type === 'innen') inner++; else if (type === 'ecke') corner++; else edge++;
    nodeList.push({ x: ox + i * px - fz / 2, y: oy + j * py - fz / 2, type });
  }
  const extraPerTile = num(v.zusatzlager, 0);
  const tiles = full + cut;
  const reserve = num(v.reserve, 3);
  const tilesOrder = ceilTo(tiles * (1 + reserve / 100));
  const extra = tiles * extraPerTile;
  const pedestals = inner + edge + corner + extra;
  const hMin = num(v.hoeheMin), hMax = num(v.hoeheMax);
  const warnings = [];
  const assumptions = [
    `Fugenbreite ${f(fz * 1000, 0)} mm, Raster ${v.ausrichtung === 'mitte' ? 'mittig ausgerichtet' : 'ab Ecke links oben'}`,
    'Jede angeschnittene Platte wird als eigene Platte gezählt (Reststücke werden nicht wiederverwendet)',
    'Stelzlager an jedem Fugenkreuz; Rand-/Eck-Stelzlager mit teilbarem Kopf bzw. Randlager einplanen',
  ];
  let system = PEDESTAL_SYSTEMS.find((s) => s.id === v.system);
  if (hMin > 0 && hMax > 0) {
    if (hMax < hMin) warnings.push('Aufbauhöhe max. ist kleiner als min. – bitte prüfen');
    const fits = PEDESTAL_SYSTEMS.filter((s) => s.min <= hMin && s.max >= hMax);
    if (system && !(system.min <= hMin && system.max >= hMax)) warnings.push(`Gewähltes System (${system.label}) deckt ${hMin}–${hMax} mm nicht vollständig ab${fits.length ? ` – passend wäre: ${fits[0].label}` : ' – Höhenbereich mit Verlängerungen/mehreren Systemen planen'}`);
    if (!system && fits.length) { system = fits[0]; assumptions.push(`System automatisch nach Aufbauhöhe gewählt: ${system.label}`); }
    if (!fits.length) warnings.push('Kein Einzel-System deckt den gesamten Höhenbereich ab – Stelzlager nach Höhenzonen aufteilen');
  } else {
    warnings.push('Aufbauhöhe nicht angegeben – Stelzlager-Höhenbereich nicht eindeutig bestimmbar');
  }
  if (slivers) warnings.push(`${slivers} sehr schmale Passstücke (< 10 % Plattenfläche) – Raster ggf. verschieben (Ausrichtung „mittig“)`);
  if ((L > 0.6 || B > 0.6) && !extraPerTile) warnings.push('Großformat > 60 cm: zusätzliches Mittel-Stelzlager je Platte prüfen');
  const results = [
    R('Belagsfläche (netto)', round(geo.area, 2), 'm²', 'Summe der Teilflächen (Vereinigung)', 'berechnet', 'Terrassen', true),
    R(`Terrassenplatten ${f(L * 100, 0)}×${f(B * 100, 0)} cm`, tilesOrder, 'St', `${full} volle + ${cut} Zuschnitte = ${tiles} St, + ${reserve} % Bruchreserve`, 'berechnet', 'Terrassen'),
    R(`${system ? system.label : 'Stelzlager (Höhe offen)'}`, pedestals, 'St', `Innen ${inner} + Rand ${edge} + Ecke ${corner}${extra ? ` + Zusatz ${extra}` : ''}`, system ? 'berechnet' : 'prüfen', 'Stelzlager'),
    R('davon Rand-/Eck-Stelzlager (Kopf teilbar)', edge + corner, 'St', `Rand ${edge} + Ecke ${corner}`, 'berechnet', 'Stelzlager', true),
    R('Randabschluss / Blende (Umfang)', round(geo.perimeter, 2), 'm', 'Außenumfang der Fläche', 'berechnet', 'Terrassen'),
  ];
  if (v.gefaelleausgleich) results.push(R('Gefälleausgleich / Ausgleichsscheiben', pedestals, 'St', '1 je Stelzlager', 'Annahme', 'Stelzlager'));
  if (v.schutzmatte) results.push(R('Schutz-/Drainagematte unter Stelzlagern', ceilTo(geo.area * 1.05, 0.5), 'm²', 'Fläche + 5 % Überlappung', 'Annahme', 'Stelzlager'));
  return {
    results,
    steps: [
      `Fläche = ${f(geo.area)} m², Umfang = ${f(geo.perimeter)} m`,
      `Raster = (${f(L * 100, 1)} + ${f(fz * 1000, 0)}/10) × (${f(B * 100, 1)} + ${f(fz * 1000, 0)}/10) cm`,
      `Platten: ${full} voll + ${cut} Zuschnitt = ${tiles}; × (1 + ${reserve} %) = ${tilesOrder} St`,
      `Stelzlager: Fugenkreuze ${inner + edge + corner} (innen ${inner}, Rand ${edge}, Ecke ${corner})${extra ? ` + ${extra} Zusatzlager` : ''} = ${pedestals} St`,
    ],
    assumptions,
    warnings,
    layout: { bbox: geo.bbox, rects: geo.rs, cells, nodes: nodeList },
  };
}

const stoneField = { key: 'ksFormat', label: 'Steinformat', type: 'select', default: '12DF', options: [
  { value: 'NF', label: 'NF 240×115×71' }, { value: '2DF', label: '2DF 240×115×113' }, { value: '3DF', label: '3DF 240×175×113' },
  { value: '12DF', label: 'Planstein 12DF 498×175×248' }, { value: '16DF', label: 'Planstein 16DF 498×240×248' },
  { value: 'custom', label: 'Benutzerdefiniert' },
] };
const STONES = { NF: [240, 115, 71], '2DF': [240, 115, 113], '3DF': [240, 175, 113], '12DF': [498, 175, 248], '16DF': [498, 240, 248] };

function paving(v, kind) {
  need(v, [['flaeche', 'Fläche'], ['laenge', 'Steinlänge'], ['breite', 'Steinbreite']]);
  const A = num(v.flaeche), L = num(v.laenge) / 100, B = num(v.breite) / 100, fz = num(v.fuge) / 1000;
  const vs = num(v.verschnitt) / 100;
  const perM2 = num(v.steineProM2) > 0 ? num(v.steineProM2) : 1 / ((L + fz) * (B + fz));
  const Av = A * (1 + vs);
  const steine = ceilTo(Av * perM2);
  const h = num(v.hoehe) / 100, rho = num(v.dichteFuge, 1.6) * 1000;
  const fugeKg = fz > 0 && h > 0 ? A * ((L + B) / (L * B)) * fz * h * rho * (1 + num(v.fugenverlust, 10) / 100) : 0;
  const dB = num(v.bettung) / 100;
  const bettM3 = A * dB * num(v.verdichtung, 1.25);
  const bettT = bettM3 * num(v.dichteBettung, 1.5);
  const g = kind === 'platten' ? 'Platten' : 'Pflaster';
  const results = [
    R(`${g} ${f(L * 100, 0)}×${f(B * 100, 0)} cm (Fläche inkl. Verschnitt)`, round(ceilTo(Av, 0.01), 2), 'm²', `${f(A)} m² × (1 + ${f(vs * 100, 1)} %)`, 'berechnet', g),
    R(`${g} – Stückzahl`, steine, 'St', `${f(perM2, 2)} St/m² × ${f(Av)} m²${num(v.steineProM2) > 0 ? ' (St/m² laut Eingabe)' : ''}`, num(v.steineProM2) > 0 ? 'berechnet' : 'Annahme', g, true),
  ];
  if (dB > 0) results.push(R('Bettungsmaterial (z. B. Splitt 2/5)', round(ceilTo(bettM3, 0.1), 1), 'm³', `${f(A)} m² × ${f(dB * 100, 1)} cm × Verdichtung ${f(num(v.verdichtung, 1.25))}`, 'berechnet', 'Schüttgüter', true),
    R('Bettungsmaterial Gewicht', round(ceilTo(bettT, 0.1), 1), 't', `${f(bettM3)} m³ × ${f(num(v.dichteBettung, 1.5))} t/m³`, 'Annahme', 'Schüttgüter'));
  if (fugeKg > 0) results.push(R('Fugenmaterial', Math.ceil(fugeKg), 'kg', `A × (L+B)/(L×B) × Fuge × Tiefe × ρ + ${num(v.fugenverlust, 10)} % Verlust`, 'berechnet', 'Fugenmaterial'));
  return {
    results,
    steps: [
      `Steine/m² = 1 / ((${f(L, 3)} + ${f(fz, 3)}) × (${f(B, 3)} + ${f(fz, 3)})) = ${f(perM2, 2)}`,
      `Fläche inkl. Verschnitt = ${f(A)} × ${f(1 + vs, 3)} = ${f(Av)} m²`,
      fugeKg ? `Fugenmaterial = ${f(A)} × ${f((L + B) / (L * B), 3)} × ${f(fz, 3)} × ${f(h, 3)} × ${f(rho, 0)} kg/m³ × ${f(1 + num(v.fugenverlust, 10) / 100, 2)} = ${f(fugeKg, 0)} kg` : 'Fugenmaterial: keine Fugen-/Steinhöhe angegeben',
    ],
    assumptions: ['Formate ohne Verbund-/Formsteinanteil; bei Verbundpflaster St/m² laut Hersteller eintragen', `Schüttdichte Bettung ${f(num(v.dichteBettung, 1.5))} t/m³, Fugenmaterial ${f(rho / 1000)} t/m³`],
    warnings: [],
  };
}

export const CALCULATORS = [
  {
    id: 'terrasse', name: 'Terrassen-/Balkonplatten auf Stelzlagern', modes: ['galabau', 'hochbau'], icon: '🟫',
    fields: [
      { key: 'rects', label: 'Fläche (Rechtecke, m)', type: 'rects', default: [{ x: 0, y: 0, w: 5, d: 4 }] },
      { key: 'plattenL', label: 'Plattenlänge', unit: 'cm', default: 60 },
      { key: 'plattenB', label: 'Plattenbreite', unit: 'cm', default: 60 },
      { key: 'fuge', label: 'Fugenbreite', unit: 'mm', default: 4 },
      { key: 'ausrichtung', label: 'Raster', type: 'select', default: 'start', options: [{ value: 'start', label: 'ab Ecke' }, { value: 'mitte', label: 'mittig (gleiche Randstücke)' }] },
      { key: 'reserve', label: 'Verschnitt/Bruchreserve', unit: '%', default: 3 },
      { key: 'hoeheMin', label: 'Aufbauhöhe min.', unit: 'mm', default: '' },
      { key: 'hoeheMax', label: 'Aufbauhöhe max.', unit: 'mm', default: '' },
      { key: 'system', label: 'Stelzlagersystem', type: 'select', default: '', options: [{ value: '', label: 'automatisch nach Höhe' }, ...PEDESTAL_SYSTEMS.map((s) => ({ value: s.id, label: s.label }))] },
      { key: 'zusatzlager', label: 'Zusatz-Stelzlager je Platte', unit: 'St', default: 0, help: 'z. B. 1 Mittellager bei Großformaten' },
      { key: 'gefaelleausgleich', label: 'Gefälleausgleich', type: 'checkbox', default: false },
      { key: 'schutzmatte', label: 'Schutzmatte', type: 'checkbox', default: false },
    ],
    compute: terraceLayout,
  },
  {
    id: 'pflaster', name: 'Pflaster', modes: ['galabau'], icon: '🧱',
    fields: [
      { key: 'flaeche', label: 'Fläche', unit: 'm²', default: '' },
      { key: 'laenge', label: 'Steinlänge', unit: 'cm', default: 20 },
      { key: 'breite', label: 'Steinbreite', unit: 'cm', default: 10 },
      { key: 'hoehe', label: 'Steinhöhe', unit: 'cm', default: 8 },
      { key: 'fuge', label: 'Fugenbreite', unit: 'mm', default: 3 },
      { key: 'steineProM2', label: 'St/m² laut Hersteller (optional)', unit: 'St', default: '' },
      { key: 'verschnitt', label: 'Verschnitt', unit: '%', default: 5 },
      { key: 'bettung', label: 'Bettungsdicke', unit: 'cm', default: 4 },
      { key: 'verdichtung', label: 'Verdichtungsfaktor Bettung', default: 1.25 },
      { key: 'dichteBettung', label: 'Schüttdichte Bettung', unit: 't/m³', default: 1.5 },
      { key: 'dichteFuge', label: 'Dichte Fugenmaterial', unit: 't/m³', default: 1.6 },
      { key: 'fugenverlust', label: 'Verlust Fugenmaterial', unit: '%', default: 10 },
    ],
    compute: (v) => paving(v, 'pflaster'),
  },
  {
    id: 'platten', name: 'Platten / Terrassenplatten im Bett', modes: ['galabau', 'hochbau'], icon: '⬜',
    fields: [
      { key: 'flaeche', label: 'Fläche', unit: 'm²', default: '' },
      { key: 'laenge', label: 'Plattenlänge', unit: 'cm', default: 60 },
      { key: 'breite', label: 'Plattenbreite', unit: 'cm', default: 40 },
      { key: 'hoehe', label: 'Plattendicke', unit: 'cm', default: 4 },
      { key: 'fuge', label: 'Fugenbreite', unit: 'mm', default: 4 },
      { key: 'steineProM2', label: 'St/m² (optional)', unit: 'St', default: '' },
      { key: 'verschnitt', label: 'Verschnitt', unit: '%', default: 5 },
      { key: 'bettung', label: 'Bettungsdicke', unit: 'cm', default: 4 },
      { key: 'verdichtung', label: 'Verdichtungsfaktor Bettung', default: 1.25 },
      { key: 'dichteBettung', label: 'Schüttdichte Bettung', unit: 't/m³', default: 1.5 },
      { key: 'dichteFuge', label: 'Dichte Fugenmaterial', unit: 't/m³', default: 1.6 },
      { key: 'fugenverlust', label: 'Verlust Fugenmaterial', unit: '%', default: 10 },
    ],
    compute: (v) => paving(v, 'platten'),
  },
  {
    id: 'bordsteine', name: 'Bordsteine inkl. Fundament', modes: ['galabau'], icon: '▭',
    fields: [
      { key: 'laenge', label: 'Gesamtlänge', unit: 'm', default: '' },
      { key: 'steinlaenge', label: 'Steinlänge', unit: 'cm', default: 100 },
      { key: 'verschnitt', label: 'Verschnitt', unit: '%', default: 3 },
      { key: 'fundB', label: 'Fundament Breite', unit: 'cm', default: 30 },
      { key: 'fundH', label: 'Fundament Höhe', unit: 'cm', default: 20 },
      { key: 'rueckB', label: 'Rückenstütze Breite', unit: 'cm', default: 15 },
      { key: 'rueckH', label: 'Rückenstütze Höhe', unit: 'cm', default: 15 },
      { key: 'rueckForm', label: 'Rückenstütze Form', type: 'select', default: 'dreieck', options: [{ value: 'dreieck', label: 'Dreieck/Keil' }, { value: 'rechteck', label: 'Rechteck' }] },
      { key: 'betonVerlust', label: 'Betonverlust', unit: '%', default: 5 },
    ],
    compute(v) {
      need(v, [['laenge', 'Gesamtlänge'], ['steinlaenge', 'Steinlänge']]);
      const L = num(v.laenge), sl = num(v.steinlaenge) / 100;
      const st = ceilTo((L * (1 + num(v.verschnitt) / 100)) / sl);
      const fq = (num(v.fundB) / 100) * (num(v.fundH) / 100);
      const rq = (num(v.rueckB) / 100) * (num(v.rueckH) / 100) * (v.rueckForm === 'rechteck' ? 1 : 0.5);
      const beton = L * (fq + rq) * (1 + num(v.betonVerlust, 5) / 100);
      return {
        results: [R(`Bordsteine (${f(sl * 100, 0)} cm)`, st, 'St', `${f(L)} m × (1 + ${num(v.verschnitt)} %) / ${f(sl)} m`, 'berechnet', 'Bordsteine'),
          R('Beton Fundament + Rückenstütze (erdfeucht, z. B. C12/15)', round(ceilTo(beton, 0.1), 1), 'm³', `${f(L)} m × (${f(fq, 4)} + ${f(rq, 4)}) m² × ${f(1 + num(v.betonVerlust, 5) / 100, 2)}`, 'berechnet', 'Zement/Mörtel')],
        steps: [`Querschnitt Fundament ${f(fq, 4)} m², Rückenstütze ${f(rq, 4)} m²`, `Beton = ${f(beton, 3)} m³`],
        assumptions: ['Bögen/Formsteine nicht berücksichtigt – separat erfassen', 'Querschnitt laut Eingabe (DIN 18318 / Herstellerangabe prüfen)'],
        warnings: [],
      };
    },
  },
  {
    id: 'fugenmaterial', name: 'Fugenmaterial', modes: ['galabau'], icon: '〰',
    fields: [
      { key: 'flaeche', label: 'Fläche', unit: 'm²', default: '' },
      { key: 'laenge', label: 'Steinlänge', unit: 'cm', default: 20 },
      { key: 'breite', label: 'Steinbreite', unit: 'cm', default: 10 },
      { key: 'tiefe', label: 'Fugentiefe (Steinhöhe)', unit: 'cm', default: 8 },
      { key: 'fuge', label: 'Fugenbreite', unit: 'mm', default: 5 },
      { key: 'dichte', label: 'Dichte', unit: 't/m³', default: 1.6 },
      { key: 'verlust', label: 'Verlust', unit: '%', default: 10 },
      { key: 'sack', label: 'Gebinde', unit: 'kg', default: 25 },
    ],
    compute(v) {
      need(v, [['flaeche', 'Fläche'], ['laenge', 'Steinlänge'], ['breite', 'Steinbreite'], ['tiefe', 'Fugentiefe'], ['fuge', 'Fugenbreite']]);
      const L = num(v.laenge) / 100, B = num(v.breite) / 100;
      const perM2 = ((L + B) / (L * B)) * (num(v.fuge) / 1000) * (num(v.tiefe) / 100) * num(v.dichte, 1.6) * 1000;
      const kg = perM2 * num(v.flaeche) * (1 + num(v.verlust) / 100);
      return {
        results: [R('Fugenmaterial', Math.ceil(kg), 'kg', `${f(perM2, 2)} kg/m² × ${f(num(v.flaeche))} m² + ${num(v.verlust)} %`, 'berechnet', 'Fugenmaterial', true),
          R(`Gebinde à ${num(v.sack, 25)} kg`, ceilTo(kg / num(v.sack, 25)), 'Sack', `${f(kg, 0)} kg / ${num(v.sack, 25)} kg`, 'berechnet', 'Fugenmaterial')],
        steps: [`Verbrauch = (L+B)/(L×B) × Fugenbreite × Fugentiefe × ρ = ${f(perM2, 2)} kg/m²`],
        assumptions: ['Herstellerverbrauch hat Vorrang (Kornform/Verdichtung)'], warnings: [],
      };
    },
  },
  {
    id: 'schuettgut', name: 'Schotter / Frostschutz / Splitt / Bettung', modes: ['galabau', 'hochbau'], icon: '⛰',
    fields: [
      { key: 'material', label: 'Material', type: 'select', default: 'fss', options: [
        { value: 'fss', label: 'Frostschutz 0/32 (ρ 2,0 / k 1,30)' }, { value: 'schotter', label: 'Schottertragschicht 0/45 (ρ 2,1 / k 1,30)' },
        { value: 'splitt', label: 'Splitt 2/5 Bettung (ρ 1,5 / k 1,25)' }, { value: 'sand', label: 'Sand 0/2 (ρ 1,6 / k 1,20)' }, { value: 'mutterboden', label: 'Oberboden (ρ 1,5 / k 1,25)' }] },
      { key: 'flaeche', label: 'Fläche', unit: 'm²', default: '' },
      { key: 'dicke', label: 'Schichtdicke (verdichtet)', unit: 'cm', default: 30 },
      { key: 'k', label: 'Verdichtungsfaktor (leer = Materialwert)', default: '' },
      { key: 'rho', label: 'Schüttdichte lose (leer = Materialwert)', unit: 't/m³', default: '' },
    ],
    compute(v) {
      need(v, [['flaeche', 'Fläche'], ['dicke', 'Schichtdicke']]);
      const M = { fss: ['Frostschutzmaterial 0/32', 2.0, 1.3], schotter: ['Schotter 0/45', 2.1, 1.3], splitt: ['Splitt 2/5', 1.5, 1.25], sand: ['Sand 0/2', 1.6, 1.2], mutterboden: ['Oberboden', 1.5, 1.25] }[v.material] || ['Schüttgut', 1.8, 1.25];
      const k = num(v.k) > 0 ? num(v.k) : M[2], rho = num(v.rho);
      const Vc = num(v.flaeche) * num(v.dicke) / 100;
      const Vl = Vc * k;
      const t = rho > 0 ? Vl * rho : Vc * M[1];
      return {
        results: [R(`${M[0]} – Einbauvolumen verdichtet`, round(Vc, 2), 'm³', `${f(num(v.flaeche))} m² × ${f(num(v.dicke) / 100, 3)} m`, 'berechnet', 'Schüttgüter', true),
          R(`${M[0]} – Liefermenge lose`, round(ceilTo(Vl, 0.5), 1), 'm³', `× Verdichtungsfaktor ${f(k)}`, 'Annahme', 'Schüttgüter', true),
          R(`${M[0]} – Tonnage`, round(ceilTo(t, 0.5), 1), 't', rho > 0 ? `${f(Vl)} m³ lose × ${f(rho)} t/m³` : `${f(Vc)} m³ verdichtet × ${f(M[1])} t/m³ (Einbaudichte)`, 'Annahme', 'Schüttgüter')],
        steps: [`V verdichtet = ${f(Vc, 3)} m³`, `V lose = ${f(Vc, 3)} × ${f(k)} = ${f(Vl, 3)} m³`],
        assumptions: ['Dichten/Verdichtungsfaktoren sind Erfahrungswerte – Lieferantenangabe hat Vorrang'], warnings: [],
      };
    },
  },
  {
    id: 'beton', name: 'Beton', modes: ['hochbau', 'galabau'], icon: '🧊',
    fields: [
      { key: 'laenge', label: 'Länge', unit: 'm', default: '' },
      { key: 'breite', label: 'Breite', unit: 'm', default: '' },
      { key: 'hoehe', label: 'Höhe/Dicke', unit: 'm', default: '' },
      { key: 'anzahl', label: 'Anzahl Bauteile', unit: 'St', default: 1 },
      { key: 'verlust', label: 'Verlust/Überprofil', unit: '%', default: 5 },
      { key: 'sackErgiebigkeit', label: 'Sackbeton Ergiebigkeit', unit: 'l/Sack', default: 19, help: 'z. B. 40-kg-Sack ≈ 19 l' },
      { key: 'zementGehalt', label: 'Zementgehalt Eigenmischung', unit: 'kg/m³', default: 300 },
    ],
    compute(v) {
      need(v, [['laenge', 'Länge'], ['breite', 'Breite'], ['hoehe', 'Höhe/Dicke']]);
      const V = num(v.laenge) * num(v.breite) * num(v.hoehe) * Math.max(1, num(v.anzahl, 1)) * (1 + num(v.verlust) / 100);
      return {
        results: [R('Transportbeton', round(ceilTo(V, 0.25), 2), 'm³', `${f(num(v.laenge))} × ${f(num(v.breite))} × ${f(num(v.hoehe))} m × ${num(v.anzahl, 1)} + ${num(v.verlust)} %`, 'berechnet', 'Beton'),
          R('alternativ Sackbeton', ceilTo((V * 1000) / num(v.sackErgiebigkeit, 19)), 'Sack', `${f(V * 1000, 0)} l / ${num(v.sackErgiebigkeit, 19)} l`, 'Annahme', 'Zement/Mörtel', true),
          R('alternativ Zement (Eigenmischung)', ceilTo((V * num(v.zementGehalt, 300)) / 25), 'Sack', `${f(V, 2)} m³ × ${num(v.zementGehalt, 300)} kg/m³ / 25 kg`, 'Annahme', 'Zement/Mörtel', true)],
        steps: [`V = ${f(V, 3)} m³`], assumptions: ['Betonfestigkeitsklasse laut Statik/LV'], warnings: [],
      };
    },
  },
  {
    id: 'mauerwerk', name: 'Mauersteine / Kalksandstein + Mörtel', modes: ['hochbau', 'galabau'], icon: '🧱',
    fields: [
      { key: 'wandL', label: 'Wandlänge', unit: 'm', default: '' },
      { key: 'wandH', label: 'Wandhöhe', unit: 'm', default: '' },
      { key: 'oeffnungen', label: 'Öffnungen abziehen', unit: 'm²', default: 0 },
      stoneField,
      { key: 'sL', label: 'Stein L (nur benutzerdef.)', unit: 'mm', default: '' },
      { key: 'sB', label: 'Stein B = Wanddicke', unit: 'mm', default: '' },
      { key: 'sH', label: 'Stein H', unit: 'mm', default: '' },
      { key: 'stossfuge', label: 'Stoßfuge', unit: 'mm', default: 2 },
      { key: 'lagerfuge', label: 'Lagerfuge', unit: 'mm', default: 2, help: 'Normalmörtel 10–12 mm, Dünnbett 1–3 mm' },
      { key: 'verschnitt', label: 'Verschnitt', unit: '%', default: 5 },
      { key: 'ergiebigkeit', label: 'Mörtel-Ergiebigkeit', unit: 'l/Sack', default: 15 },
    ],
    compute(v) {
      need(v, [['wandL', 'Wandlänge'], ['wandH', 'Wandhöhe']]);
      const dims = v.ksFormat === 'custom' ? [num(v.sL), num(v.sB), num(v.sH)] : STONES[v.ksFormat];
      if (!dims || dims.some((d) => !(d > 0))) throw new Error('Nicht eindeutig bestimmbar – Steinmaße (L/B/H) angeben');
      const [l, b, h] = dims.map((d) => d / 1000);
      const fs = num(v.stossfuge) / 1000, fl = num(v.lagerfuge) / 1000;
      const A = Math.max(0, num(v.wandL) * num(v.wandH) - num(v.oeffnungen));
      const perM2 = 1 / ((l + fs) * (h + fl));
      const n = ceilTo(A * perM2 * (1 + num(v.verschnitt) / 100));
      const Vwall = A * b;
      const mortarL = Math.max(0, (Vwall - A * perM2 * l * b * h) * 1000) * 1.1;
      const name = v.ksFormat === 'custom' ? `Mauerstein ${dims.join('×')} mm` : `Kalksandstein ${v.ksFormat} (${dims.join('×')} mm)`;
      return {
        results: [R('Wandfläche netto', round(A, 2), 'm²', `${f(num(v.wandL))} × ${f(num(v.wandH))} − ${f(num(v.oeffnungen))}`, 'berechnet', 'Mauerwerk', true),
          R(name, n, 'St', `${f(perM2, 2)} St/m² × ${f(A)} m² + ${num(v.verschnitt)} %`, 'berechnet', 'Kalksandstein'),
          R('Mauermörtel', ceilTo(mortarL / num(v.ergiebigkeit, 15)), 'Sack', `${f(mortarL, 0)} l (Fugenvolumen + 10 %) / ${num(v.ergiebigkeit, 15)} l`, 'Annahme', 'Zement/Mörtel'),
          R('Mauerwerksvolumen', round(Vwall, 2), 'm³', `${f(A)} m² × ${f(b, 3)} m`, 'berechnet', 'Mauerwerk', true)],
        steps: [`Steine/m² = 1/((${l}+${fs})×(${h}+${fl})) = ${f(perM2, 2)}`, `Mörtel = Wandvolumen − Steinvolumen = ${f(mortarL / 1.1, 0)} l`],
        assumptions: ['Lochanteil der Steine nicht berücksichtigt', 'Kimmschicht/Ergänzungssteine nicht gesondert ausgewiesen'],
        warnings: num(v.lagerfuge) > 4 && ['12DF', '16DF'].includes(v.ksFormat) ? ['Plansteine werden üblicherweise im Dünnbett (1–3 mm) vermauert'] : [],
      };
    },
  },
  {
    id: 'rinnen', name: 'Entwässerungsrinnen', modes: ['galabau'], icon: '⎍',
    fields: [
      { key: 'laenge', label: 'Rinnenlänge gesamt', unit: 'm', default: '' },
      { key: 'element', label: 'Elementlänge', unit: 'm', default: 1 },
      { key: 'straenge', label: 'Anzahl Rinnenstränge', unit: 'St', default: 1 },
      { key: 'einlauf', label: 'Einlaufkästen', unit: 'St', default: 1 },
      { key: 'fundB', label: 'Betonbettung Breite', unit: 'cm', default: 40 },
      { key: 'fundH', label: 'Betonbettung Höhe (inkl. Seitenstütze, gemittelt)', unit: 'cm', default: 30 },
      { key: 'rinnenB', label: 'Rinne Außenbreite', unit: 'cm', default: 15 },
      { key: 'rinnenH', label: 'Rinne Bauhöhe', unit: 'cm', default: 15 },
    ],
    compute(v) {
      need(v, [['laenge', 'Rinnenlänge'], ['element', 'Elementlänge']]);
      const el = ceilTo(num(v.laenge) / num(v.element));
      const beton = num(v.laenge) * Math.max(0, (num(v.fundB) / 100) * (num(v.fundH) / 100) - (num(v.rinnenB) / 100) * (num(v.rinnenH) / 100));
      return {
        results: [R(`Rinnenelemente ${f(num(v.element))} m`, el, 'St', `${f(num(v.laenge))} m / ${f(num(v.element))} m`, 'berechnet', 'Rinnen'),
          R('Abdeckungen/Roste', el, 'St', '1 je Element (falls nicht integriert)', 'Annahme', 'Rinnen'),
          R('Stirnwände (Anfang/Ende)', 2 * Math.max(1, num(v.straenge, 1)), 'St', '2 je Strang', 'berechnet', 'Rinnen'),
          R('Einlaufkasten', num(v.einlauf), 'St', 'laut Eingabe', 'berechnet', 'Rinnen'),
          R('Beton Rinnenbettung', round(ceilTo(beton * 1.05, 0.1), 1), 'm³', `(Bettungsquerschnitt − Rinnenquerschnitt) × Länge + 5 %`, 'berechnet', 'Zement/Mörtel')],
        steps: [`Elemente = ⌈${f(num(v.laenge))} / ${f(num(v.element))}⌉ = ${el}`], assumptions: ['Einbau nach Herstellerangabe/Belastungsklasse prüfen'], warnings: [],
      };
    },
  },
  {
    id: 'rohre', name: 'Rohre (KG/PP) inkl. Leitungszone', modes: ['galabau'], icon: '⭕',
    fields: [
      { key: 'laenge', label: 'Leitungslänge', unit: 'm', default: '' },
      { key: 'rohrlaenge', label: 'Rohrlänge', unit: 'm', type: 'select', default: '5', options: ['0.5', '1', '2', '3', '5'].map((x) => ({ value: x, label: `${x.replace('.', ',')} m` })) },
      { key: 'da', label: 'Rohr-Außendurchmesser', unit: 'mm', default: 160 },
      { key: 'boegen', label: 'Bögen', unit: 'St', default: 0 },
      { key: 'abzweige', label: 'Abzweige', unit: 'St', default: 0 },
      { key: 'graben', label: 'Grabenbreite', unit: 'm', default: 0.6 },
      { key: 'bettung', label: 'Bettung unter Rohr', unit: 'cm', default: 10 },
      { key: 'abdeckung', label: 'Abdeckung über Scheitel', unit: 'cm', default: 30 },
      { key: 'dichte', label: 'Dichte Leitungszonenmaterial', unit: 't/m³', default: 1.8 },
    ],
    compute(v) {
      need(v, [['laenge', 'Leitungslänge'], ['rohrlaenge', 'Rohrlänge'], ['da', 'Durchmesser']]);
      const L = num(v.laenge), da = num(v.da) / 1000;
      const rohre = ceilTo(L / num(v.rohrlaenge));
      const zone = L * (num(v.graben) * (num(v.bettung) / 100 + da + num(v.abdeckung) / 100) - Math.PI * da * da / 4);
      return {
        results: [R(`Rohre DA ${num(v.da)} – ${f(num(v.rohrlaenge), 1)} m`, rohre, 'St', `⌈${f(L)} / ${f(num(v.rohrlaenge), 1)}⌉`, 'berechnet', 'Rohre'),
          R('Bögen', num(v.boegen), 'St', 'laut Eingabe', 'berechnet', 'Rohre'),
          R('Abzweige', num(v.abzweige), 'St', 'laut Eingabe', 'berechnet', 'Rohre'),
          R('Leitungszonenmaterial (Sand/Splitt)', round(ceilTo(zone, 0.5), 1), 'm³', `L × (b × (Bettung + DA + Abdeckung) − Rohrquerschnitt)`, 'berechnet', 'Schüttgüter', true),
          R('Leitungszonenmaterial Gewicht', round(ceilTo(zone * num(v.dichte, 1.8), 0.5), 1), 't', `× ${f(num(v.dichte, 1.8))} t/m³`, 'Annahme', 'Schüttgüter')],
        steps: [`Leitungszone = ${f(zone, 3)} m³`], assumptions: ['Maße der Leitungszone nach DIN EN 1610 / LV prüfen'], warnings: [],
      };
    },
  },
  {
    id: 'matten', name: 'Bewehrung – Betonstahlmatten', modes: ['hochbau'], icon: '#',
    fields: [
      { key: 'flaeche', label: 'Bewehrte Fläche', unit: 'm²', default: '' },
      { key: 'lagen', label: 'Lagen', unit: 'St', default: 1 },
      { key: 'mattenL', label: 'Mattenlänge', unit: 'm', default: 6 },
      { key: 'mattenB', label: 'Mattenbreite', unit: 'm', default: 2.3 },
      { key: 'ueber', label: 'Übergreifung', unit: 'cm', default: 30 },
      { key: 'kgm2', label: 'Mattengewicht', unit: 'kg/m²', default: '', help: 'laut Mattenliste des Herstellers' },
      { key: 'typ', label: 'Mattentyp (Text)', type: 'text', default: 'Q188A' },
    ],
    compute(v) {
      need(v, [['flaeche', 'Fläche'], ['mattenL', 'Mattenlänge'], ['mattenB', 'Mattenbreite']]);
      const ue = num(v.ueber) / 100;
      const eff = (num(v.mattenL) - ue) * (num(v.mattenB) - ue);
      if (eff <= 0) throw new Error('Übergreifung größer als Matte – Eingaben prüfen');
      const n = ceilTo((num(v.flaeche) * Math.max(1, num(v.lagen, 1))) / eff);
      const res = [R(`Betonstahlmatte ${v.typ || ''} ${f(num(v.mattenL))}×${f(num(v.mattenB))} m`, n, 'St', `${f(num(v.flaeche))} m² × ${num(v.lagen, 1)} Lage(n) / ${f(eff)} m² Nutzfläche`, 'berechnet', 'Bewehrung')];
      const warnings = [];
      if (num(v.kgm2) > 0) res.push(R('Mattengewicht', round(n * num(v.mattenL) * num(v.mattenB) * num(v.kgm2), 0), 'kg', `${n} × ${f(num(v.mattenL) * num(v.mattenB))} m² × ${f(num(v.kgm2))} kg/m²`, 'berechnet', 'Bewehrung', true));
      else warnings.push('Mattengewicht nicht angegeben – Tonnage nicht bestimmbar (kg/m² aus Mattenliste eintragen)');
      return { results: res, steps: [`Nutzfläche je Matte = ${f(eff)} m²`], assumptions: ['Randverluste/Zuschnitt nicht gesondert berücksichtigt'], warnings };
    },
  },
  {
    id: 'stabstahl', name: 'Bewehrung – Stabstahl B500B', modes: ['hochbau'], icon: '⟋',
    fields: [
      { key: 'd', label: 'Durchmesser', unit: 'mm', type: 'select', default: '12', options: ['6', '8', '10', '12', '14', '16', '20', '25', '28'].map((x) => ({ value: x, label: `Ø ${x}` })) },
      { key: 'anzahl', label: 'Anzahl Stäbe', unit: 'St', default: '' },
      { key: 'laenge', label: 'Länge je Stab', unit: 'm', default: '' },
      { key: 'zuschlag', label: 'Zuschlag Übergreifung/Verschnitt', unit: '%', default: 8 },
    ],
    compute(v) {
      need(v, [['d', 'Durchmesser'], ['anzahl', 'Anzahl'], ['laenge', 'Länge']]);
      const d = num(v.d), kgm = 0.00617 * d * d;
      const L = num(v.anzahl) * num(v.laenge) * (1 + num(v.zuschlag) / 100);
      return {
        results: [R(`Betonstahl B500B Ø ${d}`, round(L, 1), 'm', `${num(v.anzahl)} × ${f(num(v.laenge))} m + ${num(v.zuschlag)} %`, 'berechnet', 'Bewehrung', true),
          R(`Betonstahl B500B Ø ${d} – Gewicht`, round(L * kgm, 1), 'kg', `${f(L, 1)} m × ${f(kgm, 3)} kg/m (0,00617 × d²)`, 'berechnet', 'Bewehrung')],
        steps: [`Metergewicht = 0,00617 × ${d}² = ${f(kgm, 3)} kg/m`], assumptions: [], warnings: [],
      };
    },
  },
  {
    id: 'moertel', name: 'Mörtel (Volumen)', modes: ['hochbau', 'galabau'], icon: '🪣',
    fields: [
      { key: 'flaeche', label: 'Fläche', unit: 'm²', default: '' },
      { key: 'dicke', label: 'Schichtdicke', unit: 'mm', default: 20 },
      { key: 'verlust', label: 'Verlust', unit: '%', default: 10 },
      { key: 'ergiebigkeit', label: 'Ergiebigkeit', unit: 'l/Sack', default: 15 },
    ],
    compute(v) {
      need(v, [['flaeche', 'Fläche'], ['dicke', 'Schichtdicke']]);
      const l = num(v.flaeche) * num(v.dicke) * (1 + num(v.verlust) / 100);
      return {
        results: [R('Mörtel', ceilTo(l / num(v.ergiebigkeit, 15)), 'Sack', `${f(num(v.flaeche))} m² × ${num(v.dicke)} mm = ${f(l, 0)} l / ${num(v.ergiebigkeit, 15)} l`, 'berechnet', 'Zement/Mörtel')],
        steps: [`1 m² × 1 mm = 1 l → ${f(l, 0)} l`], assumptions: ['Ergiebigkeit laut Sackaufdruck prüfen'], warnings: [],
      };
    },
  },
];

export const getCalculator = (id) => CALCULATORS.find((c) => c.id === id);

/** Führt einen Rechner sicher aus und liefert entweder Ergebnis oder Fehlermeldung (keine erfundenen Werte). */
export function runCalculator(id, values) {
  const c = getCalculator(id);
  if (!c) return { error: `Unbekannter Rechner: ${id}` };
  const v = {};
  for (const fd of c.fields) v[fd.key] = values?.[fd.key] ?? fd.default;
  try {
    return { ...c.compute(v), inputs: v };
  } catch (e) {
    return { error: e.message, inputs: v };
  }
}
