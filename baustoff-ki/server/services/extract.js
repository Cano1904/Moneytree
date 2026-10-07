// Rohdaten aus Dateien gewinnen: PDF (Text + Koordinaten), Excel, Word, GAEB, Bilder
import path from 'node:path';
import ExcelJS from 'exceljs';
import mammoth from 'mammoth';
import { getDocument, GlobalWorkerOptions } from 'pdfjs-dist/legacy/build/pdf.mjs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
GlobalWorkerOptions.workerSrc = require.resolve('pdfjs-dist/legacy/build/pdf.worker.mjs');

export function detectKind(filename, mime = '') {
  const ext = path.extname(filename).toLowerCase();
  if (ext === '.pdf' || mime === 'application/pdf') return 'pdf';
  if (['.xlsx', '.xlsm', '.xls', '.csv'].includes(ext)) return ext === '.csv' ? 'csv' : 'excel';
  if (['.docx'].includes(ext)) return 'docx';
  if (/^\.(x|d|p)8[0-9]$/.test(ext) || ext === '.x8x' || ext === '.gaeb') return /^\.x/.test(ext) || ext === '.gaeb' ? 'gaeb-xml' : 'gaeb90';
  if (['.png', '.jpg', '.jpeg', '.webp', '.gif'].includes(ext) || /^image\//.test(mime)) return 'image';
  if (ext === '.xml') return 'gaeb-xml';
  if (ext === '.txt') return 'text';
  return 'unknown';
}

export const mimeFor = (filename) => ({
  '.pdf': 'application/pdf', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp', '.gif': 'image/gif',
  '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  '.csv': 'text/csv', '.txt': 'text/plain', '.xml': 'application/xml', '.eml': 'message/rfc822',
}[path.extname(filename).toLowerCase()] || 'application/octet-stream');

/** PDF → Zeilen mit Seiten-/Koordinateninfo (für Overlay der Preise ins Original). */
export async function pdfLines(buf) {
  const doc = await getDocument({ data: new Uint8Array(buf), useSystemFonts: true, isEvalSupported: false, verbosity: 0 }).promise;
  const lines = [];
  const pages = [];
  for (let p = 1; p <= doc.numPages; p++) {
    const page = await doc.getPage(p);
    const vp = page.getViewport({ scale: 1 });
    pages.push({ width: vp.width, height: vp.height });
    const tc = await page.getTextContent();
    const items = tc.items.filter((it) => it.str !== undefined && it.str.trim() !== '').map((it) => ({ str: it.str, x: it.transform[4], y: it.transform[5], w: it.width, h: Math.abs(it.transform[3]) || it.height || 10 }));
    items.sort((a, b) => b.y - a.y || a.x - b.x);
    const rows = [];
    for (const it of items) {
      const row = rows.find((r) => Math.abs(r.y - it.y) <= Math.max(2, it.h * 0.35));
      if (row) row.items.push(it); else rows.push({ y: it.y, items: [it] });
    }
    rows.sort((a, b) => b.y - a.y);
    for (const r of rows) {
      r.items.sort((a, b) => a.x - b.x);
      let text = '';
      let lastEnd = null;
      for (const it of r.items) {
        if (lastEnd !== null) text += it.x - lastEnd > 12 ? '   ' : it.x - lastEnd > 1.5 ? ' ' : '';
        text += it.str;
        lastEnd = it.x + it.w;
      }
      lines.push({ page: p, y: r.y, h: Math.max(...r.items.map((i) => i.h)), text: text.replace(/\s+$/, ''), items: r.items });
    }
  }
  await (doc.destroy?.() ?? doc.cleanup?.());
  return { lines, pages, numPages: pages.length, hasText: lines.some((l) => l.text.replace(/[\s._-]/g, '').length > 3) };
}

/** Excel → Tabellenblätter mit Zeilen (Zellwerte als String/Number). */
export async function excelRows(buf, kind = 'excel') {
  const wb = new ExcelJS.Workbook();
  if (kind === 'csv') {
    const text = buf.toString('utf8');
    const sep = (text.split('\n')[0].match(/;/g) || []).length >= (text.split('\n')[0].match(/,/g) || []).length ? ';' : ',';
    const rows = text.split(/\r?\n/).map((l) => l.split(sep).map((c) => c.replace(/^"|"$/g, '').trim()));
    return [{ name: 'CSV', rows: rows.map((cells, i) => ({ row: i + 1, cells })) }];
  }
  await wb.xlsx.load(buf);
  return wb.worksheets.map((ws) => {
    const rows = [];
    ws.eachRow({ includeEmpty: false }, (row, rn) => {
      const cells = [];
      row.eachCell({ includeEmpty: true }, (cell, cn) => { cells[cn - 1] = cellValue(cell.value); });
      rows.push({ row: rn, cells: Array.from(cells, (c) => (c === undefined ? '' : c)) });
    });
    return { name: ws.name, rows };
  });
}
function cellValue(v) {
  if (v === null || v === undefined) return '';
  if (typeof v === 'object') {
    if (v.richText) return v.richText.map((t) => t.text).join('');
    if ('result' in v) return v.result ?? '';
    if (v.text) return v.text;
    if (v instanceof Date) return v.toISOString().slice(0, 10);
    return '';
  }
  return v;
}

/** Word → Tabellen (falls vorhanden) und Rohtext. */
export async function docxContent(buf) {
  const { value: html } = await mammoth.convertToHtml({ buffer: buf });
  const tables = [];
  for (const t of html.match(/<table[\s\S]*?<\/table>/g) || []) {
    const rows = (t.match(/<tr[\s\S]*?<\/tr>/g) || []).map((tr, i) => ({ row: i + 1, cells: (tr.match(/<t[dh][\s\S]*?<\/t[dh]>/g) || []).map((td) => decode(td.replace(/<\/p>/g, '\n').replace(/<[^>]+>/g, '')).trim()) }));
    tables.push({ name: `Tabelle ${tables.length + 1}`, rows });
  }
  const { value: text } = await mammoth.extractRawText({ buffer: buf });
  return { tables, text };
}
const decode = (s) => s.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&nbsp;/g, ' ');
