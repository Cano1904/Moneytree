// Erzeugt Beispiel-LVs (PDF, XLSX, GAEB X83) und ein Lieferantenangebot in ./samples – für Tests und zum Ausprobieren
import fs from 'node:fs';
import path from 'node:path';
import PDFDocument from 'pdfkit';
import ExcelJS from 'exceljs';

const OUT = path.resolve(import.meta.dirname, '..', 'samples');
fs.mkdirSync(OUT, { recursive: true });

export const SAMPLE = [
  { title: ['01', 'Pflasterarbeiten'] },
  { oz: '01.0010', short: 'Betonpflaster 20/10/8 cm grau', long: 'Rechteckpflaster aus Beton, Format 20/10/8 cm, Farbe grau,\nin Bettung aus Splitt 2/5 verlegen, Fugen mit Fugensand verfüllen.', qty: '245,500', unit: 'm2' },
  { oz: '01.0020', short: 'Bettung Splitt 2/5', long: 'Bettungsmaterial Splitt 2/5, Dicke 4 cm verdichtet.', qty: '245,500', unit: 'm2' },
  { oz: '01.0030', short: 'Tiefbordstein 100/25/8 cm', long: 'Tiefbord aus Beton, in Beton C12/15 mit Rückenstütze setzen.', qty: '86,000', unit: 'm' },
  { oz: '01.0040', short: 'Hochbordstein 100/30/15 cm', long: 'Hochbord H 15/30, gerade, grau.', qty: '42,000', unit: 'm' },
  { oz: '01.0050', short: 'Frostschutzschicht 0/32', long: 'Frostschutzmaterial 0/32, Schichtdicke 30 cm, liefern und einbauen.', qty: '74,000', unit: 'm3' },
  { title: ['02', 'Entwässerung'] },
  { oz: '02.0010', short: 'Entwässerungsrinne Klasse C250, NW 100', long: 'Rinne aus Polymerbeton mit Gussrost, Baulänge 1,0 m.', qty: '18,000', unit: 'm' },
  { oz: '02.0020', short: 'KG-Rohr DN 160, SN 8', long: 'KG 2000 Rohr DN 160 inkl. Muffen.', qty: '35,000', unit: 'm' },
  { oz: '02.0030', short: 'Straßenablauf 500/500', long: 'Bedarfsposition: Straßenablauf komplett mit Aufsatz D400.', qty: '2,000', unit: 'St' },
  { oz: '02.0040', short: 'Schachtring DN 1000', long: 'Schachtring aus Beton DN 1000, Höhe 500 mm.', qty: '', unit: 'St' },
  { title: ['03', 'Terrasse / Ausstattung'] },
  { oz: '03.0010', short: 'Terrassenplatte Feinsteinzeug 60/60/2 cm', long: 'Feinsteinzeug-Terrassenplatten 60/60/2 cm auf Stelzlagern verlegen.', qty: '38,400', unit: 'm2' },
  { oz: '03.0020', short: 'Fahrradbügel feuerverzinkt', long: 'Anlehnbügel aus Stahlrohr, zum Einbetonieren.', qty: '6,000', unit: 'St' },
  { oz: '03.0030', short: 'Betonpflaster 20/10/8 cm grau', long: 'wie Pos. 01.0010', qty: '245,500', unit: 'm2' },
];

function pdf() {
  const doc = new PDFDocument({ size: 'A4', margin: 50 });
  doc.pipe(fs.createWriteStream(path.join(OUT, 'Beispiel-LV_Aussenanlagen.pdf')));
  doc.font('Helvetica-Bold').fontSize(14).text('Leistungsverzeichnis – Außenanlagen Kita Sonnenschein', 50, 50);
  doc.font('Helvetica').fontSize(9).text('Bauherr: Gemeinde Musterstadt    Vergabe-Nr. 2026-117', 50, 72);
  let y = 100;
  for (const r of SAMPLE) {
    if (y > 740) { doc.addPage(); y = 50; }
    if (r.title) { doc.font('Helvetica-Bold').fontSize(10).text(`${r.title[0]}   ${r.title[1]}`, 50, y); y += 20; continue; }
    doc.font('Helvetica-Bold').fontSize(9).text(r.oz, 50, y).text(r.short, 110, y);
    y += 13;
    doc.font('Helvetica').fontSize(8.5);
    for (const l of r.long.split('\n')) { doc.text(l, 110, y, { width: 330 }); y += 11; }
    doc.fontSize(9).text(`${r.qty || ''}  ${r.unit}`, 110, y);
    doc.text('..............', 360, y).text('..............', 470, y);
    doc.fontSize(7).text('EP', 345, y + 1).text('GP', 455, y + 1);
    y += 22;
  }
  doc.end();
}

async function xlsx() {
  const wb = new ExcelJS.Workbook();
  const ws = wb.addWorksheet('LV');
  ws.addRow(['Leistungsverzeichnis Wohnanlage Lindenhof – Rohbau']);
  ws.addRow([]);
  ws.addRow(['Pos.', 'Kurztext', 'Langtext', 'Menge', 'Einheit', 'EP', 'GP']);
  const rows = [
    ['1', 'Mauerwerk', '', '', '', '', ''],
    ['1.10', 'KS-Planstein 12DF 175 mm', 'Kalksandstein-Planstein KS R(P) 12DF, SFK 20, Wanddicke 17,5 cm, im Dünnbett vermauern', 412.5, 'm2', '', ''],
    ['1.20', 'Dünnbettmörtel', 'Dünnbettmörtel für KS-Plansteine, 25 kg Sack', 60, 'Sack', '', ''],
    ['1.30', 'Kimmsteine KS 11,5 cm', 'Kimmstein für erste Lage', 120, 'm', '', ''],
    ['2', 'Beton / Bewehrung', '', '', '', '', ''],
    ['2.10', 'Transportbeton C25/30 XC2', 'Bodenplatte, Konsistenz F3', 86.4, 'm3', '', ''],
    ['2.20', 'Betonstahlmatte Q188A', 'Lagermatte 6,00 x 2,30 m', 2.85, 't', '', ''],
    ['2.30', 'Betonstabstahl B500B Ø 12', 'geschnitten und gebogen', 1.4, 't', '', ''],
    ['2.40', 'Abstandhalter', '', '', 'St', '', ''],
    ['3', 'Dämmung', '', '', '', '', ''],
    ['3.10', 'Perimeterdämmung XPS 120 mm', 'unter Bodenplatte, WLS 035', 210, 'm2', '', ''],
  ];
  for (const r of rows) ws.addRow(r);
  ws.columns.forEach((c, i) => { c.width = [8, 36, 50, 10, 8, 12, 14][i]; });
  ws.getRow(3).font = { bold: true };
  await wb.xlsx.writeFile(path.join(OUT, 'Beispiel-LV_Rohbau.xlsx'));
}

function gaeb() {
  const items = SAMPLE.filter((r) => !r.title);
  const ctg = (no, label, list) => `<BoQCtgy RNoPart="${no}"><LblTx><p><span>${label}</span></p></LblTx><BoQBody><Itemlist>${list.map((r) => `
<Item RNoPart="${r.oz.split('.')[1]}">${r.long.startsWith('Bedarfs') ? '<Provis>WithoutTotal</Provis>' : ''}${r.qty ? `<Qty>${r.qty.replace(',', '.')}</Qty>` : ''}<QU>${r.unit}</QU><Description><CompleteText><DetailTxt><Text><p><span>${r.long.replace(/\n/g, '</span></p><p><span>')}</span></p></Text></DetailTxt><OutlineText><OutlTxt><TextOutlTxt><p><span>${r.short}</span></p></TextOutlTxt></OutlTxt></OutlineText></CompleteText></Description></Item>`).join('')}
</Itemlist></BoQBody></BoQCtgy>`;
  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<GAEB xmlns="http://www.gaeb.de/GAEB_DA_XML/DA83/3.2">
<GAEBInfo><Version>3.2</Version><ProgSystem>Beispiel</ProgSystem></GAEBInfo>
<PrjInfo><NamePrj>Außenanlagen Kita Sonnenschein</NamePrj></PrjInfo>
<Award><DP>83</DP><BoQ><BoQInfo><Name>Außenanlagen</Name></BoQInfo><BoQBody>
${ctg('01', 'Pflasterarbeiten', items.filter((r) => r.oz.startsWith('01')))}
${ctg('02', 'Entwässerung', items.filter((r) => r.oz.startsWith('02')))}
${ctg('03', 'Terrasse / Ausstattung', items.filter((r) => r.oz.startsWith('03')))}
</BoQBody></BoQ></Award></GAEB>`;
  fs.writeFileSync(path.join(OUT, 'Beispiel-LV_Aussenanlagen.x83'), xml);
}

function quote() {
  const doc = new PDFDocument({ size: 'A4', margin: 50 });
  doc.pipe(fs.createWriteStream(path.join(OUT, 'Beispiel-Lieferantenangebot.pdf')));
  doc.font('Helvetica-Bold').fontSize(13).text('Betonwerk Nord GmbH – Angebot Nr. A-55123', 50, 50);
  doc.font('Helvetica').fontSize(9).text('Bauvorhaben: Kita Sonnenschein    Preise gültig bis 31.12.2026    Lieferzeit: 2 Wochen', 50, 72);
  const rows = [
    ['Rechteckpflaster 20/10/8 grau', '245,50', 'm2', '11,85'],
    ['Tiefbordstein 100/25/8 grau', '86,00', 'm', '6,40'],
    ['Hochbordstein H 15/30 100 cm', '42,00', 'm', '13,90'],
  ];
  let y = 110;
  doc.font('Helvetica-Bold').text('Artikel', 50, y).text('Menge', 300, y).text('ME', 360, y).text('Preis/ME', 420, y);
  doc.font('Helvetica');
  for (const r of rows) { y += 16; doc.text(r[0], 50, y).text(r[1], 300, y).text(r[2], 360, y).text(`${r[3]} €`, 420, y); }
  y += 30;
  doc.text('Fracht pauschal: 180,00 €', 50, y);
  doc.end();
}

await xlsx();
pdf();
gaeb();
quote();
console.log('Beispieldateien erzeugt in', OUT);
