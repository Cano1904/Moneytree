// Angebots-PDF: Deckblatt, neu gesetztes Angebots-LV, Preise ins Original-PDF eintragen, Zusammenfügen
import PDFKit from 'pdfkit';
import { PDFDocument, StandardFonts, rgb } from 'pdf-lib';
import { fmtNum, fmtQty, fmtDate, fmtEUR } from '../../shared/format.js';
import { calcPosition, calcTotals, countsInTotal } from '../../shared/pricing.js';

const toBuffer = (doc) => new Promise((resolve, reject) => {
  const chunks = [];
  doc.on('data', (c) => chunks.push(c));
  doc.on('end', () => resolve(Buffer.concat(chunks)));
  doc.on('error', reject);
  doc.end();
});
const safe = (s) => String(s ?? '').replace(/[^\x09\x0a\x0d\x20-\x7e\xa0-\xff€–—‘’“”„•…›‹™Ø²³×]/g, '?');

/** Standard-Deckblatt aus Firmendaten (wenn kein eigenes Deckblatt hinterlegt ist). */
export function generateCover({ project, customer, offer, totals, settings, user, validUntil }) {
  const doc = new PDFKit({ size: 'A4', margin: 60, info: { Title: `Angebot ${offer.number}` } });
  doc.font('Helvetica-Bold').fontSize(18).text(safe(settings.company_name), 60, 60);
  doc.font('Helvetica').fontSize(9).fillColor('#555').text(safe([settings.company_address, settings.company_phone && `Tel. ${settings.company_phone}`, settings.company_email, settings.company_web].filter(Boolean).join('\n')), 60, 86);
  doc.fillColor('#000').fontSize(10).text(safe([customer?.name, project.contact_name, customer?.address].filter(Boolean).join('\n')), 60, 200);
  doc.font('Helvetica-Bold').fontSize(28).text('Angebot', 60, 330);
  doc.font('Helvetica').fontSize(12);
  const rows = [['Angebots-Nr.', offer.number], ['Datum', fmtDate(offer.date || new Date())], ['Bauvorhaben', project.name], project.site && ['Baustelle', project.site], project.number && ['Projekt-Nr.', project.number], ['Ansprechpartner', user?.name], validUntil && ['Gültig bis', fmtDate(validUntil)]].filter(Boolean);
  let y = 380;
  for (const [k, v] of rows) { doc.fillColor('#555').text(k, 60, y, { width: 130 }); doc.fillColor('#000').text(safe(v || ''), 200, y, { width: 330 }); y += 20; }
  y += 20;
  doc.moveTo(60, y).lineTo(535, y).strokeColor('#bbb').stroke();
  y += 12;
  doc.font('Helvetica-Bold').text('Angebotssumme netto', 60, y).text(fmtEUR(totals.net), 300, y, { width: 235, align: 'right' });
  doc.font('Helvetica').text(`zzgl. ${settings.vat_pct} % MwSt.`, 60, y + 18).text(fmtEUR(totals.vat), 300, y + 18, { width: 235, align: 'right' });
  doc.font('Helvetica-Bold').text('Angebotssumme brutto', 60, y + 36).text(fmtEUR(totals.gross), 300, y + 36, { width: 235, align: 'right' });
  doc.font('Helvetica').fontSize(9).fillColor('#555').text(safe(settings.offer_terms || ''), 60, 740, { width: 475 });
  return toBuffer(doc);
}

/** Neu gesetztes Angebots-LV (Tabelle mit OZ, Text, Menge, EP, GP, Titelsummen, Zusammenstellung). */
export function generateLvPdf({ project, customer, offer, positions, settings, minMargin, longText = true }) {
  const doc = new PDFKit({ size: 'A4', margins: { top: 70, bottom: 60, left: 40, right: 40 }, bufferPages: true, info: { Title: `Angebot ${offer.number} – LV` } });
  const W = { oz: 55, text: 0, qty: 55, unit: 32, ep: 68, gp: 75 };
  W.text = 515 - W.oz - W.qty - W.unit - W.ep - W.gp;
  const X = { oz: 40 }; X.text = X.oz + W.oz; X.qty = X.text + W.text; X.unit = X.qty + W.qty; X.ep = X.unit + W.unit; X.gp = X.ep + W.ep;
  const bottom = 842 - 60;
  let y = 70;
  const header = () => {
    doc.font('Helvetica-Bold').fontSize(8).fillColor('#333');
    doc.text('OZ', X.oz, y).text('Leistungsbeschreibung', X.text, y).text('Menge', X.qty, y, { width: W.qty - 4, align: 'right' }).text('ME', X.unit, y).text('EP €', X.ep, y, { width: W.ep - 4, align: 'right' }).text('GP €', X.gp, y, { width: W.gp, align: 'right' });
    y += 12; doc.moveTo(40, y).lineTo(555, y).strokeColor('#999').lineWidth(0.5).stroke(); y += 5;
    doc.fillColor('#000');
  };
  const ensure = (h) => { if (y + h > bottom) { doc.addPage(); y = 70; header(); } };
  doc.font('Helvetica-Bold').fontSize(13).text(safe(`Angebot ${offer.number} – ${project.name}`), 40, y); y += 18;
  doc.font('Helvetica').fontSize(9).text(safe(`${customer?.name || ''}${project.site ? ` · ${project.site}` : ''} · ${fmtDate(offer.date || new Date())}`), 40, y); y += 14;
  if (settings.offer_intro) { doc.text(safe(settings.offer_intro), 40, y, { width: 515 }); y = doc.y + 10; }
  header();
  const titleSums = [];
  let curTitle = null, curSum = 0;
  const flushTitle = () => {
    if (curTitle === null) return;
    ensure(18);
    doc.font('Helvetica-Bold').fontSize(8.5).text(safe(`Summe ${curTitle}`), X.text, y, { width: W.text + W.qty + W.unit + W.ep - 6 }).text(fmtNum(curSum), X.gp, y, { width: W.gp, align: 'right' });
    y += 16; titleSums.push([curTitle, curSum]);
  };
  for (const p of positions) {
    const t = p.title_path || '';
    if (t !== (curTitle ?? '')) {
      flushTitle();
      curTitle = t; curSum = 0;
      if (t) { ensure(30); y += 4; doc.font('Helvetica-Bold').fontSize(10).text(safe(t), X.oz, y, { width: 515 }); y = doc.y + 6; }
    }
    const c = calcPosition(p, minMargin);
    doc.font('Helvetica-Bold').fontSize(8.5);
    const typeNote = p.pos_type === 'B' ? ' (Bedarfsposition – nur EP)' : p.pos_type === 'A' ? ' (Alternativposition – nur EP)' : '';
    const sh = doc.heightOfString(safe(p.short_text + typeNote), { width: W.text - 6 });
    doc.font('Helvetica').fontSize(7.5);
    const lt = longText && p.long_text ? safe(p.long_text) : '';
    const lh = lt ? doc.heightOfString(lt, { width: W.text - 6 }) + 2 : 0;
    ensure(Math.min(sh + lh + 8, 300));
    const y0 = y;
    doc.font('Helvetica').fontSize(8.5).text(safe(p.oz || ''), X.oz, y0, { width: W.oz - 4 });
    doc.font('Helvetica-Bold').text(safe(p.short_text + typeNote), X.text, y0, { width: W.text - 6 });
    if (p.pos_type !== 'T') {
      doc.font('Helvetica').text(p.qty !== null ? fmtQty(p.qty) : '', X.qty, y0, { width: W.qty - 4, align: 'right' }).text(safe(p.unit || ''), X.unit, y0);
      doc.text(c.vk !== null ? fmtNum(c.vk) : '–', X.ep, y0, { width: W.ep - 4, align: 'right' });
      doc.text(countsInTotal(p) ? (c.total !== null ? fmtNum(c.total) : '–') : 'n. i. S.', X.gp, y0, { width: W.gp, align: 'right' });
    }
    y = y0 + sh + 1;
    if (lt) { doc.font('Helvetica').fontSize(7.5).fillColor('#444').text(lt, X.text, y, { width: W.text - 6 }); doc.fillColor('#000'); y = doc.y + 1; }
    y += 6;
    if (countsInTotal(p) && c.total !== null) curSum += c.total;
  }
  flushTitle();
  const t = calcTotals(positions, { minMargin, vatPct: settings.vat_pct });
  ensure(40 + titleSums.length * 13 + 60);
  y += 8;
  doc.font('Helvetica-Bold').fontSize(11).text('Zusammenstellung', 40, y); y += 18;
  doc.font('Helvetica').fontSize(9);
  for (const [n, s] of titleSums.filter(([n]) => n)) { doc.text(safe(n), 40, y, { width: 380 }).text(fmtEUR(s), 420, y, { width: 135, align: 'right' }); y += 13; }
  y += 4; doc.moveTo(300, y).lineTo(555, y).stroke(); y += 6;
  doc.font('Helvetica-Bold').text('Summe netto', 300, y).text(fmtEUR(t.net), 420, y, { width: 135, align: 'right' }); y += 14;
  doc.font('Helvetica').text(`MwSt. ${settings.vat_pct} %`, 300, y).text(fmtEUR(t.vat), 420, y, { width: 135, align: 'right' }); y += 14;
  doc.font('Helvetica-Bold').text('Summe brutto', 300, y).text(fmtEUR(t.gross), 420, y, { width: 135, align: 'right' }); y += 20;
  if (t.optional) { doc.font('Helvetica').fontSize(8).text(`Bedarfs-/Alternativpositionen sind nicht in der Summe enthalten (Wert: ${fmtEUR(t.optional)}).`, 40, y, { width: 515 }); y = doc.y + 6; }
  if (t.missing) { doc.font('Helvetica-Bold').fontSize(8).fillColor('#b00').text(`${t.missing} Position(en) ohne Preis oder Menge.`, 40, y); doc.fillColor('#000'); }
  const range = doc.bufferedPageRange();
  for (let i = range.start; i < range.start + range.count; i++) {
    doc.switchToPage(i);
    doc.font('Helvetica').fontSize(7.5).fillColor('#666');
    doc.text(safe(`${settings.company_name} · Angebot ${offer.number}`), 40, 30, { width: 300, lineBreak: false });
    doc.text(`Seite ${i + 1} von ${range.count}`, 400, 842 - 40, { width: 155, align: 'right', lineBreak: false });
    doc.fillColor('#000');
  }
  return toBuffer(doc);
}

/** Preise in das Original-PDF eintragen (an Platzhaltern bzw. am rechten Rand) + Zusammenstellungsseite. */
export async function overlayOriginal({ originalBuf, positions, settings, minMargin, offer }) {
  const pdf = await PDFDocument.load(originalBuf, { ignoreEncryption: true });
  const font = await pdf.embedFont(StandardFonts.Helvetica);
  const bold = await pdf.embedFont(StandardFonts.HelveticaBold);
  const pages = pdf.getPages();
  let placed = 0, skipped = 0;
  const qtyChanged = [];
  const drawRight = (page, txt, slot, f = font, size = 8.5) => {
    const w = f.widthOfTextAtSize(txt, size);
    const right = slot.x + slot.w;
    page.drawRectangle({ x: slot.x - 1, y: slot.y - 2.5, width: slot.w + 2, height: 11, color: rgb(1, 1, 1) });
    page.drawText(txt, { x: right - w, y: slot.y, size, font: f, color: rgb(0.05, 0.1, 0.45) });
  };
  for (const p of positions) {
    const L = p.layout;
    const c = calcPosition(p, minMargin);
    if (!L?.ep || !L?.gp || c.vk === null || p.pos_type === 'T') { if (p.pos_type !== 'T') skipped++; continue; }
    const page = pages[(L.page || 1) - 1];
    if (!page) { skipped++; continue; }
    if ('qty0' in L && (L.qty0 ?? null) !== (p.qty ?? null)) qtyChanged.push(`${p.oz || ''}: ${L.qty0 === null ? 'ohne Menge' : fmtQty(L.qty0)} → ${fmtQty(p.qty)} ${p.unit || ''}`);
    drawRight(page, fmtNum(c.vk), L.ep);
    drawRight(page, countsInTotal(p) ? (c.total !== null ? fmtNum(c.total) : '') : 'n. i. S.', L.gp, bold);
    placed++;
  }
  const t = calcTotals(positions, { minMargin, vatPct: settings.vat_pct });
  const pg = pdf.addPage([595.28, 841.89]);
  let y = 780;
  const line = (a, b, f = font, size = 10) => { pg.drawText(a, { x: 50, y, size, font: f }); if (b) pg.drawText(b, { x: 545 - f.widthOfTextAtSize(b, size), y, size, font: f }); y -= size + 8; };
  line(`Zusammenstellung – Angebot ${offer.number}`, null, bold, 14);
  y -= 6;
  const titles = new Map();
  for (const p of positions) { const c = calcPosition(p, minMargin); if (countsInTotal(p) && c.total !== null && p.pos_type !== 'T') titles.set(p.title_path || 'Ohne Titel', (titles.get(p.title_path || 'Ohne Titel') || 0) + c.total); }
  for (const [n, s] of titles) line(safe(n).slice(0, 80).replace(/[^\x20-\x7e\xa0-\xff€–›]/g, '?'), fmtEUR(s));
  y -= 6;
  line('Summe netto', fmtEUR(t.net), bold);
  line(`MwSt. ${settings.vat_pct} %`, fmtEUR(t.vat));
  line('Summe brutto', fmtEUR(t.gross), bold);
  if (t.missing) line(`${t.missing} Position(en) ohne Preis oder Menge`, null, bold, 9);
  if (qtyChanged.length) { y -= 4; line('Abweichende Mengen gegenüber dem Original-LV (GP auf Basis der geänderten Menge):', null, bold, 8); for (const t of qtyChanged) line(safe(t).replace(/[^\x20-\x7e\xa0-\xff€–›→]/g, '?').replace('→', '->'), null, font, 8); }
  if (skipped) line(`Hinweis: ${skipped} Preis(e) konnten nicht ins Original eingetragen werden – siehe Angebots-LV.`, null, font, 8);
  return { buf: Buffer.from(await pdf.save()), placed, skipped };
}

/** Teile (PDF oder Bild) zu einer Angebots-PDF zusammenfügen. */
export async function assemble(parts) {
  const out = await PDFDocument.create();
  for (const part of parts) {
    if (part.mime === 'application/pdf') {
      const src = await PDFDocument.load(part.buf, { ignoreEncryption: true });
      const pages = await out.copyPages(src, src.getPageIndices());
      pages.forEach((p) => out.addPage(p));
    } else if (/^image\/(png|jpe?g)$/.test(part.mime)) {
      const img = part.mime === 'image/png' ? await out.embedPng(part.buf) : await out.embedJpg(part.buf);
      const page = out.addPage([595.28, 841.89]);
      const s = Math.min(555 / img.width, 802 / img.height);
      page.drawImage(img, { x: (595.28 - img.width * s) / 2, y: (841.89 - img.height * s) / 2, width: img.width * s, height: img.height * s });
    } else {
      throw new Error(`Anhang „${part.name}“ kann nicht ins PDF übernommen werden (nur PDF, PNG, JPG)`);
    }
  }
  return Buffer.from(await out.save());
}
