// Mail-Vorlagen (Lieferantenanfrage, Angebotsmail) und EML-Erzeugung (öffnet sich in Outlook/Thunderbird als Entwurf)
import { fmtQty, fmtDate, fmtEUR } from '../../shared/format.js';

const greet = (name) => {
  if (!name) return 'Sehr geehrte Damen und Herren,';
  const n = name.trim();
  if (/^(frau)\s/i.test(n)) return `Sehr geehrte ${n},`;
  if (/^(herr)\s/i.test(n)) return `Sehr geehrter ${n},`;
  return `Guten Tag ${n},`;
};
const formatOf = (t) => (String(t || '').match(/(\d{2,3}\s*[/x×]\s*\d{1,3}(?:\s*[/x×]\s*\d{1,2}(?:,\d)?)?\s*(?:cm|mm)?)/i) || [])[1];
const excerpt = (t, n = 280) => { const s = String(t || '').replace(/\s+/g, ' ').trim(); return s.length > n ? `${s.slice(0, n)} …` : s; };

export function supplierRequestMail({ project, customer, supplier, contact, positions, desiredDate, answerBy, user, settings }) {
  const subject = `Preisanfrage: ${project.name}${project.number ? ` (${project.number})` : ''} – ${positions.length} Position${positions.length === 1 ? '' : 'en'}`;
  const lines = [
    greet(contact?.name),
    '',
    `für das Bauvorhaben „${project.name}“${project.site ? `, ${project.site}` : ''}${customer?.name ? ` (Kunde: ${customer.name})` : ''} bitten wir um Ihr Angebot für folgende Positionen:`,
    '',
  ];
  for (const p of positions) {
    lines.push(`▸ ${p.oz ? `Pos. ${p.oz} – ` : ''}${p.short_text}`);
    lines.push(`   Menge: ${p.qty !== null && p.qty !== undefined ? `${fmtQty(p.qty)} ${p.unit || ''}` : 'bitte Preis je Einheit'}${p.unit && p.qty === null ? ` (${p.unit})` : ''}`);
    const fmt = formatOf(`${p.short_text} ${p.long_text}`);
    if (fmt) lines.push(`   Format: ${fmt}`);
    if (p.long_text) lines.push(`   Anforderungen: ${excerpt(p.long_text)}`);
    if (p.pos_type === 'B') lines.push('   (Bedarfsposition)');
    if (p.pos_type === 'A') lines.push('   (Alternativposition)');
    lines.push('');
  }
  lines.push('Bitte nennen Sie uns:', '• Einzelpreis netto je Einheit', '• Frachtkosten frei Baustelle bzw. Mindermengenzuschläge', '• Lieferzeit ab Auftragseingang', '• Preisbindung / Gültigkeit Ihres Angebots', '• ggf. gleichwertige Alternativprodukte mit technischen Daten', '');
  if (desiredDate) lines.push(`Gewünschter Liefertermin: ${fmtDate(desiredDate)}`);
  if (answerBy) lines.push(`Für eine Rückmeldung bis zum ${fmtDate(answerBy)} wären wir Ihnen dankbar.`);
  lines.push('', 'Vielen Dank und freundliche Grüße', '', user?.name || '', settings.company_name || '', settings.company_phone ? `Tel. ${settings.company_phone}` : '', settings.company_email || '');
  return { subject, body: lines.filter((l, i, a) => !(l === '' && a[i - 1] === '')).join('\n').trim(), to: contact?.email || supplier?.email || '' };
}

export function offerMail({ project, customer, offer, totals, validUntil, user, settings }) {
  const subject = `Angebot ${offer.number || ''} – ${project.name}`.replace(/\s+/g, ' ');
  const body = [
    greet(project.contact_name || customer?.contact),
    '',
    `vielen Dank für Ihre Anfrage zum Bauvorhaben „${project.name}“. Anbei erhalten Sie unser Angebot ${offer.number || ''}.`.replace(/\s+\./, '.'),
    '',
    `Angebotssumme netto: ${fmtEUR(totals.net)}`,
    `zzgl. ${settings.vat_pct} % MwSt.: ${fmtEUR(totals.vat)}`,
    `Angebotssumme brutto: ${fmtEUR(totals.gross)}`,
    validUntil ? `\nDas Angebot ist gültig bis ${fmtDate(validUntil)}.` : '',
    '',
    'Für Rückfragen stehen wir Ihnen gerne zur Verfügung. Über eine Auftragserteilung würden wir uns sehr freuen.',
    '',
    settings.signature || 'Mit freundlichen Grüßen',
    user?.name || '',
    settings.company_name || '',
  ].join('\n').replace(/\n{3,}/g, '\n\n').trim();
  return { subject, body, to: project.contact_email || customer?.email || '' };
}

const b64 = (s) => Buffer.from(s, 'utf8').toString('base64');
const encHeader = (s) => (/^[\x20-\x7e]*$/.test(s) ? s : `=?UTF-8?B?${b64(s)}?=`);
const wrap = (s) => s.replace(/(.{76})/g, '$1\r\n');

/** EML-Datei mit X-Unsent: 1 – wird von Outlook als bearbeitbarer Entwurf geöffnet. Versand erfolgt immer durch den Benutzer. */
export function buildEml({ to = '', subject, body, attachments = [], from = '' }) {
  const boundary = `----=_Part_${Date.now().toString(36)}`;
  const head = [`To: ${to}`, from ? `From: ${from}` : null, `Subject: ${encHeader(subject)}`, 'X-Unsent: 1', 'MIME-Version: 1.0', `Date: ${new Date().toUTCString()}`].filter(Boolean);
  if (!attachments.length) return [...head, 'Content-Type: text/plain; charset=UTF-8', 'Content-Transfer-Encoding: base64', '', wrap(b64(body))].join('\r\n');
  const parts = [`--${boundary}`, 'Content-Type: text/plain; charset=UTF-8', 'Content-Transfer-Encoding: base64', '', wrap(b64(body))];
  for (const a of attachments) parts.push(`--${boundary}`, `Content-Type: ${a.mime}; name="${encHeader(a.filename)}"`, 'Content-Transfer-Encoding: base64', `Content-Disposition: attachment; filename="${encHeader(a.filename)}"`, '', wrap(Buffer.from(a.data).toString('base64')));
  parts.push(`--${boundary}--`);
  return [...head, `Content-Type: multipart/mixed; boundary="${boundary}"`, '', ...parts].join('\r\n');
}
