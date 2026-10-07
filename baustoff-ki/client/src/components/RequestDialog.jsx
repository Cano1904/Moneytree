// "Wie möchtest du anfragen?" – Telefon oder Mail; Anfrage-Ansicht mit kopierbarer Mail bzw. Gesprächserfassung
import { useEffect, useMemo, useState } from 'react';
import { api, download } from '../api.js';
import { Badge, Field, Modal, NumInput, TextInput, copyText, useAction, useLoad, useToast } from './ui.jsx';
import { REQUEST_STATUS } from '../../../shared/status.js';
import { fmtQty } from '../../../shared/format.js';

export default function RequestDialog({ projectId, positions, suppliers, onClose }) {
  const assigned = [...new Set(positions.map((p) => p.supplier_id).filter(Boolean))];
  const [supplierId, setSupplierId] = useState(assigned.length === 1 ? assigned[0] : '');
  const [includeAll, setIncludeAll] = useState(true);
  const [requestId, setRequestId] = useState(null);
  const [channel, setChannel] = useState(null);
  const [run, busy] = useAction();
  const detail = useLoad(positions.length === 1 ? `/positions/${positions[0].id}/detail` : null);
  const sugg = detail.data?.suggestions || [];
  const start = async (ch) => {
    const r = await run(() => api.post(`/projects/${projectId}/requests`, { supplier_id: Number(supplierId), position_ids: positions.map((p) => p.id), channel: ch }));
    if (!r) return;
    if (includeAll) await api.post(`/projects/${projectId}/requests`, { supplier_id: Number(supplierId), all_assigned: true, channel: ch }).catch(() => {}); // keine weiteren offenen Positionen ist kein Fehler
    setChannel(ch); setRequestId(r.id);
  };
  if (requestId) return <Modal title={channel === 'telefon' ? '📞 Lieferant anrufen' : '✉ Anfrage-Mail'} onClose={onClose} wide><RequestView id={requestId} projectId={projectId} mode={channel} onDone={onClose} /></Modal>;
  return (
    <Modal title="Wie möchtest du anfragen?" onClose={onClose}>
      <div className="stack">
        <div className="muted">{positions.length} Position(en): {positions.slice(0, 4).map((p) => p.oz || p.short_text).join(', ')}{positions.length > 4 ? ' …' : ''}</div>
        <Field label="Lieferant">
          <select value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">Bitte wählen …</option>
            {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}{s.preferred ? ' ★' : ''}</option>)}
          </select>
        </Field>
        {sugg.length > 0 && <div className="row"><span className="muted small">Vorschläge:</span>{sugg.map((s) => <button key={s.supplier_id} className={`pill ${String(supplierId) === String(s.supplier_id) ? 'on' : ''}`} title={s.reasons.join(', ')} onClick={() => setSupplierId(s.supplier_id)}>{s.name}</button>)}</div>}
        <label className="row"><input type="checkbox" checked={includeAll} onChange={(e) => setIncludeAll(e.target.checked)} /> alle weiteren Positionen dieses Lieferanten ohne Preis in dieselbe Anfrage aufnehmen</label>
        <div className="grid g2">
          <button className="btn big" disabled={!supplierId || busy} onClick={() => start('telefon')}><span className="ico">📞</span>Lieferant anrufen</button>
          <button className="btn big primary" disabled={!supplierId || busy} onClick={() => start('mail')}><span className="ico">✉</span>Anfrage-Mail erstellen</button>
        </div>
      </div>
    </Modal>
  );
}

/** Anfrage anzeigen/bearbeiten: Mail (kopieren/öffnen/EML) oder Telefonat (Notiz, Ergebnis, Preise). */
export function RequestView({ id, projectId, mode: initialMode, onDone }) {
  const { data: r, reload } = useLoad(`/requests/${id}`, [id]);
  const [mode, setMode] = useState(initialMode);
  const [run, busy] = useAction();
  const toast = useToast();
  const [prices, setPrices] = useState({});
  const [call, setCall] = useState({ notes: '', result: 'preis_erhalten', valid_until: '', delivery: '' });
  useEffect(() => { if (r && !mode) setMode(r.channel); }, [r, mode]);
  const fullText = useMemo(() => (r ? `Betreff: ${r.subject}\n\n${r.body}` : ''), [r]);
  if (!r) return <div className="muted">Lade …</div>;
  const patch = async (b) => { await run(() => api.patch(`/requests/${id}`, b)); reload(); };
  const mailto = `mailto:${encodeURIComponent(r.to)}?subject=${encodeURIComponent(r.subject)}&body=${encodeURIComponent(r.body)}`;
  const savePhone = async () => {
    const items = r.positions.filter((p) => prices[p.id]?.price !== undefined && prices[p.id]?.price !== null).map((p) => ({ position_id: p.id, text: p.short_text, qty: p.qty, unit: p.unit, price: prices[p.id].price, discount_pct: prices[p.id].discount ?? 0, delivery_text: call.delivery, apply: true }));
    await run(async () => {
      await api.patch(`/requests/${id}`, { call_notes: call.notes || `Telefonat geführt (${items.length} Preise)`, call_result: call.result, status: call.result, channel: 'telefon' });
      if (items.length) await api.post(`/projects/${projectId}/quotes`, { supplier_id: r.supplier_id, request_id: id, source: 'telefon', valid_until: call.valid_until || null, delivery_time: call.delivery, items });
    }, `Gespräch gespeichert${items.length ? ` · ${items.length} Preise übernommen` : ''}`);
    onDone?.();
  };
  return (
    <div className="stack">
      <div className="row between">
        <div><b>{r.supplier_name}</b> · {r.project_name}<div className="muted small">{r.contact ? `${r.contact.name}${r.contact.role ? ` (${r.contact.role})` : ''}` : 'kein Ansprechpartner hinterlegt'}</div></div>
        <div className="row">
          <select style={{ width: 210 }} value={r.status} onChange={(e) => patch({ status: e.target.value })}>{REQUEST_STATUS.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</select>
          <div className="seg"><button className={mode === 'mail' ? 'on' : ''} onClick={() => setMode('mail')}>✉ Mail</button><button className={mode === 'telefon' ? 'on' : ''} onClick={() => setMode('telefon')}>📞 Telefon</button></div>
        </div>
      </div>
      {r.contacts.length > 1 && <Field label="Ansprechpartner"><select value={r.contact_id || r.contact?.id || ''} onChange={async (e) => { await patch({ contact_id: Number(e.target.value) }); await run(() => api.post(`/requests/${id}/regenerate`)); reload(); }}>{r.contacts.map((c) => <option key={c.id} value={c.id}>{c.name} {c.email ? `<${c.email}>` : ''}</option>)}</select></Field>}

      {mode === 'mail' ? (
        <>
          <Field label="An"><input value={r.to} readOnly placeholder="Keine E-Mail hinterlegt – bitte beim Lieferanten ergänzen" /></Field>
          <Field label="Betreff"><TextInput value={r.subject} onCommit={(v) => patch({ subject: v })} /></Field>
          <Field label="Text (bearbeitbar, wird automatisch gespeichert)"><TextInput multiline rows={16} value={r.body} onCommit={(v) => patch({ body: v })} /></Field>
          <div className="row">
            <button className="btn primary" onClick={async () => { if (await copyText(fullText)) toast('Mail in Zwischenablage kopiert', 'ok'); }}>📋 Mail kopieren</button>
            <button className="btn" onClick={async () => { if (await copyText(r.body)) toast('Text kopiert', 'ok'); }}>Nur Text kopieren</button>
            <a className="btn" href={mailto}>✉ Im Mailprogramm öffnen</a>
            <button className="btn" onClick={() => run(() => download(`/requests/${id}/eml`, 'Anfrage.eml'), 'Entwurf (.eml) heruntergeladen – in Outlook öffnen und senden')}>⬇ Als Outlook-Entwurf (.eml)</button>
            <button className="btn" onClick={() => run(() => api.post(`/requests/${id}/regenerate`)).then(reload)}>↻ Neu generieren</button>
          </div>
          <div className="row between" style={{ borderTop: '1px solid var(--line)', paddingTop: 12 }}>
            <span className="muted small">Die Mail wird von Ihnen versendet. Danach als angefragt markieren.</span>
            <button className="btn success" disabled={busy} onClick={async () => { await patch({ status: 'angefragt', channel: 'mail' }); toast('Als angefragt markiert', 'ok'); onDone?.(); }}>✓ Versendet – als angefragt markieren</button>
          </div>
        </>
      ) : (
        <>
          <div className="callout"><div style={{ fontSize: 20, fontWeight: 700 }}>☎ {r.contact?.phone ? <a href={`tel:${r.contact.phone}`}>{r.contact.phone}</a> : r.supplier_phone ? <a href={`tel:${r.supplier_phone}`}>{r.supplier_phone}</a> : 'keine Nummer hinterlegt'}</div>{r.contact && <div>{r.contact.name}{r.contact.role ? ` – ${r.contact.role}` : ''}</div>}</div>
          <table className="t"><thead><tr><th>Pos.</th><th>Benötigt</th><th className="num">Menge</th><th className="num">Preis €/ME</th><th className="num">Rabatt %</th></tr></thead><tbody>
            {r.positions.map((p) => <tr key={p.id}><td>{p.oz}</td><td>{p.short_text}<div className="muted small">{(p.long_text || '').slice(0, 140)}</div></td><td className="num">{fmtQty(p.qty)} {p.unit}</td>
              <td className="num"><NumInput value={prices[p.id]?.price ?? p.ek ?? null} onCommit={(v) => setPrices({ ...prices, [p.id]: { ...prices[p.id], price: v } })} /></td>
              <td className="num"><NumInput value={prices[p.id]?.discount ?? null} onCommit={(v) => setPrices({ ...prices, [p.id]: { ...prices[p.id], discount: v } })} /></td></tr>)}
          </tbody></table>
          <div className="grid g3">
            <Field label="Ergebnis"><select value={call.result} onChange={(e) => setCall({ ...call, result: e.target.value })}><option value="preis_erhalten">Preis erhalten</option><option value="ausstehend">Rückruf / Angebot folgt</option><option value="rueckfrage">Rückfrage erforderlich</option><option value="vollstaendig">vollständig</option></select></Field>
            <Field label="Lieferzeit"><input value={call.delivery} onChange={(e) => setCall({ ...call, delivery: e.target.value })} placeholder="z. B. 2 Wochen" /></Field>
            <Field label="Preisbindung bis"><input type="date" value={call.valid_until} onChange={(e) => setCall({ ...call, valid_until: e.target.value })} /></Field>
          </div>
          <Field label="Gesprächsnotiz"><textarea value={call.notes} onChange={(e) => setCall({ ...call, notes: e.target.value })} placeholder="Was wurde besprochen? Fracht, Alternativen, Verfügbarkeit …" /></Field>
          {r.call_notes && <div className="muted small">Letzte Notiz: {r.call_notes}</div>}
          <div className="row" style={{ justifyContent: 'flex-end' }}><button className="btn primary" disabled={busy} onClick={savePhone}>Gespräch & Preise speichern</button></div>
        </>
      )}
      <details><summary>Positionen in dieser Anfrage ({r.positions.length})</summary>
        <div className="list">{r.positions.map((p) => <div key={p.id} className="row between"><span>{p.oz} {p.short_text}</span><span className="row">{p.ek !== null ? <Badge color="green">EK {p.ek}</Badge> : <Badge color="yellow">offen</Badge>}<button className="btn sm ghost" onClick={async () => { await run(() => api.del(`/requests/${id}/items/${p.id}`)); reload(); }}>✕</button></span></div>)}</div>
      </details>
    </div>
  );
}
