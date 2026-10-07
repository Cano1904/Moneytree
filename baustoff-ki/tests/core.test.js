// Unit-Tests: Kalkulation, Rechner, Parser, Plausibilität, Exporte, Angebotsimport
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { calcPosition, calcTotals, rankVariants } from '../shared/pricing.js';
import { runCalculator, unionGeometry } from '../shared/calculators.js';
import { parseNum, normalizeUnit, fmtQty } from '../shared/format.js';
import { classify, detectMode } from '../shared/groups.js';
import { parseTextLv, parseTableLv, parseGaebXml } from '../server/services/lvParser.js';
import { pdfLines, excelRows } from '../server/services/extract.js';
import { checkLv } from '../server/services/plausibility.js';
import { fillGaeb, fillExcel } from '../server/services/lvExport.js';
import { analyzeQuote } from '../server/services/quoteImport.js';
import { buildEml } from '../server/services/mail.js';

const SAMPLES = path.resolve(import.meta.dirname, '..', 'samples');
const sample = (f) => fs.readFileSync(path.join(SAMPLES, f));

test('Zahlen & Einheiten', () => {
  assert.equal(parseNum('1.234,50'), 1234.5);
  assert.equal(parseNum('245,500'), 245.5);
  assert.equal(parseNum('12.000'), 12000);
  assert.equal(parseNum('abc'), null);
  assert.equal(normalizeUnit('qm'), 'm²');
  assert.equal(normalizeUnit('Stck'), 'St');
  assert.equal(normalizeUnit('lfm'), 'm');
  assert.equal(fmtQty(245.5), '245,5');
});

test('Positionskalkulation: Einstand, VK, Marge, Mindestmarge', () => {
  const c = calcPosition({ ek: 10, discount_pct: 10, freight: 1, markup_pct: 10, qty: 100 }, 12);
  assert.equal(c.einstand, 10);
  assert.equal(c.vk, 11);
  assert.equal(c.total, 1100);
  assert.equal(c.db_total, 100);
  assert.equal(c.below_min, true);
  assert.equal(calcPosition({ ek: null, markup_pct: 8, qty: 5 }).vk, null, 'ohne EK kein erfundener Preis');
});

test('Summen: Bedarfs-, Alternativ- und Textpositionen zählen nicht', () => {
  const t = calcTotals([
    { ek: 10, markup_pct: 0, qty: 1, pos_type: 'N' },
    { ek: 10, markup_pct: 0, qty: 1, pos_type: 'B' },
    { ek: 10, markup_pct: 0, qty: 1, pos_type: 'A' },
    { pos_type: 'T' },
    { ek: null, markup_pct: 0, qty: 1, pos_type: 'N' },
  ], { vatPct: 19 });
  assert.equal(t.net, 10);
  assert.equal(t.optional, 20);
  assert.equal(t.missing, 1);
  assert.equal(t.gross, 11.9);
});

test('Variantenvergleich markiert günstigsten EK und schnellste Lieferung', () => {
  const v = rankVariants([{ supplier_id: 1, price: 10, freight: 2, delivery_days: 10 }, { supplier_id: 2, price: 11, freight: 0, delivery_days: 3 }], { preferredSupplierIds: [1] });
  assert.deepEqual(v[0].tags, ['günstigster EK', 'bevorzugt']);
  assert.deepEqual(v[1].tags, ['bester Einstand', 'schnellste Lieferung']);
});

test('Terrassenrechner: 1,2 × 1,2 m mit 60×60 ohne Fuge = 4 Platten, 9 Stelzlager', () => {
  const r = runCalculator('terrasse', { rects: [{ x: 0, y: 0, w: 1.2, d: 1.2 }], plattenL: 60, plattenB: 60, fuge: 0, reserve: 0, hoeheMin: 40, hoeheMax: 60 });
  assert.equal(r.error, undefined);
  const get = (re) => r.results.find((x) => re.test(x.material));
  assert.equal(get(/Terrassenplatten/).qty, 4);
  assert.equal(get(/^Stelzlager/).qty, 9);
  assert.equal(get(/Rand-\/Eck/).qty, 8);
  assert.equal(get(/Belagsfläche/).qty, 1.44);
});

test('Geometrie: L-Form Fläche und Umfang', () => {
  const g = unionGeometry([{ x: 0, y: 0, w: 4, d: 3 }, { x: 0, y: 3, w: 2, d: 2 }]);
  assert.equal(g.area, 16);
  assert.equal(g.perimeter, 18);
});

test('Rechner raten nicht: fehlende Eingaben → Fehlermeldung', () => {
  assert.match(runCalculator('pflaster', { flaeche: '' }).error, /Nicht eindeutig bestimmbar/);
  assert.match(runCalculator('terrasse', { rects: [] }).error, /Nicht eindeutig bestimmbar/);
});

test('Pflasterrechner: 20×10 cm, 3 mm Fuge', () => {
  const r = runCalculator('pflaster', { flaeche: 100, laenge: 20, breite: 10, fuge: 3, verschnitt: 0, hoehe: 8 });
  const st = r.results.find((x) => /Stückzahl/.test(x.material));
  assert.equal(st.qty, Math.ceil(100 / (0.203 * 0.103)));
});

test('Klassifizierung & Modus-Erkennung', () => {
  assert.equal(classify('Betonpflaster 20/10/8 cm', 'galabau', 'in Bettung aus Splitt verlegen').group, 'Pflaster');
  assert.equal(classify('KS-Planstein 12DF').group, 'Kalksandstein');
  assert.equal(classify('Entwässerungsrinne NW 100').group, 'Rinnen');
  assert.equal(detectMode(['Transportbeton C25/30', 'Betonstahlmatte Q188A', 'Kalksandstein 2DF']).mode, 'hochbau');
});

test('PDF-LV: Positionen, Titel, Mengen, Platzhalter-Koordinaten', async () => {
  const pl = await pdfLines(sample('Beispiel-LV_Aussenanlagen.pdf'));
  const r = parseTextLv(pl.lines, pl.pages);
  assert.equal(r.positions.length, 12);
  const p = r.positions.find((x) => x.oz === '02.0010');
  assert.equal(p.qty, 18);
  assert.equal(p.unit, 'm');
  assert.equal(p.title_path, 'Entwässerung');
  assert.equal(p.layout.mode, 'platzhalter');
  const schacht = r.positions.find((x) => x.oz === '02.0040');
  assert.equal(schacht.qty, null, 'fehlende Menge wird nicht erfunden');
  assert.equal(schacht.unit, 'St');
  assert.equal(r.positions.find((x) => x.oz === '02.0030').pos_type, 'B');
});

test('Excel-LV und GAEB-XML', async () => {
  const x = parseTableLv(await excelRows(sample('Beispiel-LV_Rohbau.xlsx')));
  assert.equal(x.positions.length, 8);
  assert.equal(x.positions[0].qty, 412.5);
  assert.deepEqual(x.meta.columns, { oz: 0, text: 1, long: 2, qty: 3, unit: 4, ep: 5, gp: 6 });
  const g = parseGaebXml(sample('Beispiel-LV_Aussenanlagen.x83').toString('utf8'));
  assert.equal(g.positions.length, 12);
  assert.equal(g.positions[0].oz, '01.0010');
  assert.equal(g.positions[0].qty, 245.5);
  assert.equal(g.meta.dp, '83');
});

test('Plausibilität: fehlende Menge, Doppelposition, geprüfte Hinweise bleiben geprüft', () => {
  const pos = [
    { id: 1, oz: '1', short_text: 'Pflaster grau', long_text: '', qty: 10, unit: 'm²', pos_type: 'N', group_name: 'Pflaster' },
    { id: 2, oz: '2', short_text: 'Pflaster grau', long_text: '', qty: 10, unit: 'm²', pos_type: 'N', group_name: 'Pflaster', flags: [{ key: 'duplicate', resolved: true }] },
    { id: 3, oz: '3', short_text: 'Schachtring', long_text: '', qty: null, unit: 'St', pos_type: 'N', group_name: 'Schächte' },
  ];
  const r = checkLv(pos, { mode: 'galabau' });
  assert.ok(r.positions[0].flags.some((f) => f.key === 'duplicate' && !f.resolved));
  assert.ok(r.positions[1].flags.some((f) => f.key === 'duplicate' && f.resolved));
  assert.ok(r.positions[2].flags.some((f) => f.key === 'qty_missing'));
  assert.ok(r.lvIssues.some((i) => /Pflaster/.test(i.msg)));
});

test('GAEB X84 und Excel-Rückschreiben', async () => {
  const g = parseGaebXml(sample('Beispiel-LV_Aussenanlagen.x83').toString('utf8'));
  const priced = g.positions.map((p) => ({ ...p, ek: 10, markup_pct: 10 }));
  const { xml, filled } = fillGaeb({ originalXml: sample('Beispiel-LV_Aussenanlagen.x83').toString('utf8'), positions: priced });
  assert.equal(filled, 12);
  assert.match(xml, /<DP>84<\/DP>/);
  assert.match(xml, /<UP>11\.00<\/UP><IT>2700\.50<\/IT>/);
  assert.equal(parseGaebXml(xml).positions.length, 12);
  const x = parseTableLv(await excelRows(sample('Beispiel-LV_Rohbau.xlsx')));
  const buf = await fillExcel({ originalBuf: sample('Beispiel-LV_Rohbau.xlsx'), meta: x.meta, positions: x.positions.map((p) => ({ ...p, ek: 10, markup_pct: 0 })) });
  const rows = (await excelRows(buf))[0].rows;
  assert.equal(rows.find((r) => r.row === 5).cells[5], 10);
});

test('Lieferantenangebot (PDF) wird regelbasiert gelesen und zugeordnet', async () => {
  const r = await analyzeQuote({ buf: sample('Beispiel-Lieferantenangebot.pdf'), filename: 'a.pdf', mime: 'application/pdf', useAi: false, positions: [{ id: 7, oz: '01.0010', short_text: 'Betonpflaster 20/10/8 cm grau', qty: 245.5, unit: 'm²' }, { id: 8, oz: '01.0030', short_text: 'Tiefbordstein 100/25/8 cm', qty: 86, unit: 'm' }] });
  assert.equal(r.items.length, 3);
  assert.equal(r.items[0].price, 11.85);
  assert.equal(r.items[0].position_id, 7);
  assert.equal(r.items[1].position_id, 8);
  assert.equal(r.freight_total, 180);
  assert.equal(r.valid_until, '2026-12-31');
});

test('EML-Entwurf mit Anhang', () => {
  const eml = buildEml({ to: 'a@b.de', subject: 'Angebot Ä', body: 'Hallo', attachments: [{ filename: 'x.pdf', mime: 'application/pdf', data: Buffer.from('%PDF') }] });
  assert.match(eml, /X-Unsent: 1/);
  assert.match(eml, /=\?UTF-8\?B\?/);
  assert.match(eml, /Content-Disposition: attachment; filename="x.pdf"/);
});
