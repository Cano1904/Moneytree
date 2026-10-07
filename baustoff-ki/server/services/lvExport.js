// Preise in Original-Formate zurückschreiben: Excel (Originaldatei) und GAEB X84
import ExcelJS from 'exceljs';
import { calcPosition, calcTotals, countsInTotal } from '../../shared/pricing.js';

/** Trägt EP/GP in die Original-Excel ein (Layout bleibt erhalten). Fehlende Preisspalten werden angehängt. */
export async function fillExcel({ originalBuf, meta, positions, minMargin }) {
  const wb = new ExcelJS.Workbook();
  await wb.xlsx.load(originalBuf);
  const ws = wb.getWorksheet(meta.sheet) || wb.worksheets[0];
  const cols = { ...meta.columns };
  const last = Math.max(ws.columnCount, ...Object.values(cols).map((c) => c + 1));
  if (cols.ep === undefined) { cols.ep = last; ws.getRow(meta.headerRow).getCell(cols.ep + 1).value = 'EP'; }
  if (cols.gp === undefined) { cols.gp = Math.max(last, cols.ep + 1); ws.getRow(meta.headerRow).getCell(cols.gp + 1).value = 'GP'; }
  let lastRow = meta.headerRow;
  for (const p of positions) {
    const r = p.layout?.row;
    if (!r || p.pos_type === 'T') continue;
    lastRow = Math.max(lastRow, r);
    const c = calcPosition(p, minMargin);
    const row = ws.getRow(r);
    const epCell = row.getCell(cols.ep + 1);
    epCell.value = c.vk;
    epCell.numFmt = '#,##0.00';
    const gpCell = row.getCell(cols.gp + 1);
    if (countsInTotal(p) && c.vk !== null && cols.qty !== undefined) {
      const qtyRef = ws.getRow(r).getCell(cols.qty + 1).address;
      gpCell.value = { formula: `ROUND(${qtyRef}*${epCell.address},2)`, result: c.total };
    } else gpCell.value = countsInTotal(p) ? c.total : 'n. i. S.';
    gpCell.numFmt = '#,##0.00';
  }
  const t = calcTotals(positions, { minMargin });
  const sumRow = ws.getRow(lastRow + 2);
  sumRow.getCell(Math.max(1, cols.ep)).value = 'Summe netto';
  sumRow.getCell(cols.gp + 1).value = t.net;
  sumRow.getCell(cols.gp + 1).numFmt = '#,##0.00';
  sumRow.font = { bold: true };
  return Buffer.from(await wb.xlsx.writeBuffer());
}

/** Erzeugt GAEB X84 (Angebotsabgabe) aus der Original-X83: UP/IT je Item einsetzen, DP auf 84 setzen. */
export function fillGaeb({ originalXml, positions, minMargin }) {
  const byIndex = new Map(positions.filter((p) => p.layout?.gaebIndex !== undefined).map((p) => [p.layout.gaebIndex, p]));
  let idx = 0, filled = 0;
  let xml = originalXml.replace(/<Item\b[^>]*>[\s\S]*?<\/Item>/g, (item) => {
    const p = byIndex.get(idx++);
    if (!p) return item;
    const c = calcPosition(p, minMargin);
    if (c.vk === null) return item;
    let out = item.replace(/<UP>[\s\S]*?<\/UP>/g, '').replace(/<IT>[\s\S]*?<\/IT>/g, '');
    const ins = `<UP>${c.vk.toFixed(2)}</UP>${countsInTotal(p) && c.total !== null ? `<IT>${c.total.toFixed(2)}</IT>` : ''}`;
    out = /<\/QU>/.test(out) ? out.replace(/<\/QU>/, `</QU>${ins}`) : out.replace(/<Description>/, `${ins}<Description>`);
    filled++;
    return out;
  });
  xml = xml.replace(/<DP>\s*8[1-3]\s*<\/DP>/, '<DP>84</DP>').replace(/DA8[1-3]/g, 'DA84');
  return { xml, filled };
}
