// Angebot fertigstellen: Bausteine wählen, Reihenfolge per Drag & Drop, Prüfungen, PDF, Kundenmail, Versand-Freigabe, Nachfassen
import { useEffect, useState } from 'react';
import { api, docUrl, download } from '../api.js';
import { Badge, Empty, Field, Modal, TextInput, copyText, useAction, useLoad, useToast } from './ui.jsx';
import { FOLLOWUP_RESULTS } from '../../../shared/status.js';
import { fmtDate, fmtDateTime, fmtEUR, fmtPct } from '../../../shared/format.js';

export default function OfferBuilder({ project, reload }) {
  const [lvId, setLvId] = useState(project.lvs.at(-1)?.id || null);
  const prev = useLoad(lvId ? `/projects/${project.id}/offer-preview?lv_id=${lvId}` : null, [lvId, project.updated_at]);
  const [parts, setParts] = useState([]);
  const [extras, setExtras] = useState([]);
  const [longText, setLongText] = useState(true);
  const [validDays, setValidDays] = useState('');
  const [dragIdx, setDragIdx] = useState(null);
  const [mailFor, setMailFor] = useState(null);
  const [run, busy] = useAction();
  useEffect(() => {
    const d = prev.data;
    if (!d) return;
    setParts([{ ...d.covers[0], key: 'cover' }, { ...d.lvVariants[0], key: 'lv' }]);
    setExtras(d.extras.map((e) => e.type));
  }, [prev.data]);
  if (!project.lvs.length) return <Empty>Für ein Angebot zuerst ein LV hochladen oder Material übernehmen.</Empty>;
  const d = prev.data;
  const keyOf = (x) => (x.type === 'document' ? `doc${x.document_id}` : x.type);
  const has = (x) => parts.some((p) => keyOf(p) === keyOf(x));
  const toggle = (x) => setParts(has(x) ? parts.filter((p) => keyOf(p) !== keyOf(x)) : [...parts, { ...x, key: keyOf(x) }]);
  const move = (from, to) => { const n = [...parts]; const [it] = n.splice(from, 1); n.splice(to, 0, it); setParts(n); };
  const errors = d?.checks.filter((c) => c.severity === 'error') || [];
  const create = async () => {
    if (errors.length && !confirm(`Achtung:\n${errors.map((e) => `• ${e.msg}`).join('\n')}\n\nTrotzdem Angebot erstellen?`)) return;
    const r = await run(() => api.post(`/projects/${project.id}/offers`, { lv_id: lvId, parts, extras, long_text: longText, valid_days: validDays || undefined }), (x) => `Angebot ${x.number} erstellt`);
    if (r) { reload(); window.open(docUrl(r.document_id), '_blank'); }
  };
  return (
    <div className="stack">
      <div className="grid g2">
        <div className="card stack">
          <h2>1. Bausteine wählen</h2>
          {project.lvs.length > 1 && <Field label="LV"><select value={lvId} onChange={(e) => setLvId(Number(e.target.value))}>{project.lvs.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}</select></Field>}
          {d ? <>
            <div><div className="muted small">Deckblatt</div>{d.covers.map((c) => <label key={keyOf(c)} className="row"><input type="checkbox" checked={has(c)} onChange={() => toggle(c)} /> {c.label}</label>)}</div>
            <div><div className="muted small">Leistungsverzeichnis</div>{d.lvVariants.map((c) => <label key={keyOf(c)} className="row"><input type="checkbox" checked={has(c)} onChange={() => toggle(c)} /> {c.label}</label>)}
              <label className="row small" style={{ marginLeft: 26 }}><input type="checkbox" checked={longText} onChange={(e) => setLongText(e.target.checked)} /> Langtexte im Angebots-LV</label></div>
            {d.attachments.length > 0 && <div><div className="muted small">Anhänge aus der Projektakte</div>{d.attachments.map((a) => { const x = { type: 'document', document_id: a.id, label: a.filename }; return <label key={a.id} className="row"><input type="checkbox" checked={has(x)} onChange={() => toggle(x)} /> {a.filename} <span className="muted small">({a.category})</span></label>; })}</div>}
            {d.extras.length > 0 && <div><div className="muted small">Zusätzliche Dateien (für Mail)</div>{d.extras.map((x) => <label key={x.type} className="row"><input type="checkbox" checked={extras.includes(x.type)} onChange={(e) => setExtras(e.target.checked ? [...extras, x.type] : extras.filter((t) => t !== x.type))} /> {x.label}</label>)}</div>}
            <Field label="Angebot gültig (Tage)"><input style={{ width: 120 }} value={validDays} onChange={(e) => setValidDays(e.target.value)} placeholder="Standard" /></Field>
          </> : <div className="muted">Lade …</div>}
        </div>
        <div className="card stack">
          <h2>2. Reihenfolge prüfen</h2>
          <div className="muted small">Per Drag & Drop sortieren – so wird das PDF zusammengesetzt.</div>
          <div>
            {parts.map((p, i) => (
              <div key={p.key} className={`dragitem ${dragIdx !== null && dragIdx !== i ? 'over' : ''}`} draggable onDragStart={() => setDragIdx(i)} onDragOver={(e) => e.preventDefault()} onDrop={() => { if (dragIdx !== null) move(dragIdx, i); setDragIdx(null); }} onDragEnd={() => setDragIdx(null)}>
                <span className="h">⠿</span><b>{i + 1}.</b><span className="grow">{p.label}</span>
                <button className="btn sm ghost" disabled={i === 0} onClick={() => move(i, i - 1)}>↑</button><button className="btn sm ghost" disabled={i === parts.length - 1} onClick={() => move(i, i + 1)}>↓</button><button className="btn sm ghost" onClick={() => toggle(p)}>✕</button>
              </div>
            ))}
            {!parts.length && <div className="muted">Keine Bausteine gewählt.</div>}
          </div>
          {d && <>
            <h3>3. Prüfung</h3>
            {d.checks.map((c) => <div key={c.msg} className={`flag ${c.severity === 'ok' ? 'info' : c.severity}`}>{c.severity === 'ok' ? '✅' : c.severity === 'error' ? '🔴' : '⚠'} {c.msg}</div>)}
            <div className="row between"><span>Summe netto <b>{fmtEUR(d.totals.net)}</b> · brutto {fmtEUR(d.totals.gross)} · Marge {fmtPct(d.totals.margin_pct)}</span></div>
            <button className="btn big primary" disabled={busy || !parts.length} onClick={create}><span className="ico">📄</span>{busy ? 'PDF wird erstellt …' : `PDF erstellen (Version ${d.nextVersion})`}</button>
          </>}
        </div>
      </div>
      <OffersList project={project} reload={reload} onMail={setMailFor} />
      {mailFor && <OfferMail offerId={mailFor} onClose={() => { setMailFor(null); reload(); }} />}
    </div>
  );
}

function OffersList({ project, reload, onMail }) {
  const [run] = useAction();
  const [fuNote, setFuNote] = useState('');
  const openFu = project.followups.filter((f) => f.status === 'offen');
  return (
    <div className="grid g2">
      <div className="card">
        <h2>Angebote & Versionen</h2>
        {project.offers.length ? <table className="t"><thead><tr><th>Nr.</th><th className="num">netto</th><th>Status</th><th>Erstellt</th><th /></tr></thead><tbody>
          {project.offers.map((o) => <tr key={o.id}><td><b>{o.number}</b><div className="muted small">{(o.parts?.order || []).join(' + ')}</div></td><td className="num">{fmtEUR(o.total_net)}</td>
            <td>{o.status === 'versendet' ? <Badge color="green">versendet {fmtDate(o.sent_at)}</Badge> : <Badge color="blue">erstellt</Badge>}</td><td className="small">{fmtDateTime(o.created_at)}</td>
            <td className="nowrap"><a className="btn sm" href={docUrl(o.document_id)} target="_blank" rel="noreferrer">PDF</a> <button className="btn sm primary" onClick={() => onMail(o.id)}>✉ Kundenmail</button></td></tr>)}
        </tbody></table> : <Empty>Noch kein Angebot erstellt.</Empty>}
      </div>
      <div className="card">
        <h2>Nachfassen</h2>
        {openFu.length ? openFu.map((f) => (
          <div key={f.id} className="stack" style={{ borderBottom: '1px solid var(--line)', paddingBottom: 10 }}>
            <div className="row between"><b>{f.note}</b><Badge color={f.due_date < new Date().toISOString().slice(0, 10) ? 'red' : 'yellow'}>fällig {fmtDate(f.due_date)}</Badge></div>
            <input value={fuNote} onChange={(e) => setFuNote(e.target.value)} placeholder="Ergebnis / Notiz zum Gespräch" />
            <div className="row">{FOLLOWUP_RESULTS.map((r) => <button key={r.id} className={`btn sm ${r.id === 'auftrag' ? 'success' : r.id === 'verloren' ? 'danger' : ''}`} onClick={async () => { await run(() => api.patch(`/followups/${f.id}`, { result: r.id, note: fuNote }), `Ergebnis: ${r.label}`); setFuNote(''); reload(); }}>{r.label}</button>)}</div>
          </div>
        )) : <div className="muted">Keine offene Wiedervorlage. Wird beim Versand automatisch angelegt.</div>}
        <FollowupAdd projectId={project.id} reload={reload} />
        {project.followups.filter((f) => f.status === 'erledigt').map((f) => <div key={f.id} className="muted small">✓ {fmtDate(f.done_at)}: {FOLLOWUP_RESULTS.find((r) => r.id === f.result)?.label} {f.note ? `– ${f.note}` : ''}</div>)}
      </div>
    </div>
  );
}

function FollowupAdd({ projectId, reload }) {
  const [date, setDate] = useState('');
  const [run] = useAction();
  return <div className="row" style={{ marginTop: 10 }}><input type="date" style={{ width: 170 }} value={date} onChange={(e) => setDate(e.target.value)} /><button className="btn sm" disabled={!date} onClick={async () => { await run(() => api.post(`/projects/${projectId}/followups`, { due_date: date, note: 'Wiedervorlage' }), 'Wiedervorlage gesetzt'); setDate(''); reload(); }}>＋ Wiedervorlage</button></div>;
}

function OfferMail({ offerId, onClose }) {
  const { data: o, reload } = useLoad(`/offers/${offerId}`, [offerId]);
  const [to, setTo] = useState('');
  const [days, setDays] = useState(7);
  const [run, busy] = useAction();
  const toast = useToast();
  useEffect(() => { if (o) setTo(o.to); }, [o]);
  if (!o) return null;
  const patch = async (b) => { await run(() => api.patch(`/offers/${offerId}`, b)); reload(); };
  return (
    <Modal title={`✉ Angebotsmail · ${o.number}`} wide onClose={onClose} footer={<>
      <span className="muted small grow">Versand erfolgt durch Sie im Mailprogramm. Erst danach freigeben.</span>
      <label className="row small">Nachfassen in <input style={{ width: 60 }} value={days} onChange={(e) => setDays(e.target.value)} /> Tagen</label>
      <button className="btn success" disabled={busy || o.status === 'versendet'} onClick={async () => { if (!confirm(`Bestätigen: Angebot ${o.number} wurde an ${to || '(ohne Empfänger)'} versendet?`)) return; const r = await run(() => api.post(`/offers/${offerId}/sent`, { followup_days: Number(days), to, subject: o.mail_subject, body: o.mail_body }), 'Als versendet markiert · Wiedervorlage angelegt'); if (r) onClose(); }}>{o.status === 'versendet' ? '✓ bereits versendet' : '✓ Versand freigeben & bestätigen'}</button>
    </>}>
      <div className="stack">
        <Field label="An"><input value={to} onChange={(e) => setTo(e.target.value)} placeholder="E-Mail des Kunden" /></Field>
        <Field label="Betreff"><TextInput value={o.mail_subject} onCommit={(v) => patch({ mail_subject: v })} /></Field>
        <Field label="Text"><TextInput multiline rows={14} value={o.mail_body} onCommit={(v) => patch({ mail_body: v })} /></Field>
        <div className="row">
          <button className="btn primary" onClick={() => run(() => download(`/offers/${offerId}/eml?to=${encodeURIComponent(to)}`, 'Angebot.eml'), 'Outlook-Entwurf mit PDF-Anhang heruntergeladen')}>⬇ Mail-Entwurf mit PDF (.eml)</button>
          <button className="btn" onClick={async () => { if (await copyText(`${o.mail_subject}\n\n${o.mail_body}`)) toast('Mail kopiert', 'ok'); }}>📋 Kopieren</button>
          <a className="btn" href={`mailto:${encodeURIComponent(to)}?subject=${encodeURIComponent(o.mail_subject)}&body=${encodeURIComponent(o.mail_body)}`}>✉ Mailprogramm (ohne Anhang)</a>
          <a className="btn" href={docUrl(o.document_id, false)}>⬇ PDF</a>
        </div>
      </div>
    </Modal>
  );
}
