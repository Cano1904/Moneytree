import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { Badge, Empty, useAction, useLoad } from '../components/ui.jsx';
import { FOLLOWUP_RESULTS } from '../../../shared/status.js';
import { fmtDate, fmtEUR } from '../../../shared/format.js';
import { useApp } from '../App.jsx';

export default function FollowUps() {
  const [scope, setScope] = useState('open');
  const { data, reload } = useLoad(`/followups?scope=${scope}`, [scope]);
  const [notes, setNotes] = useState({});
  const [dates, setDates] = useState({});
  const [run] = useAction();
  const { refreshCounts } = useApp();
  const today = new Date().toISOString().slice(0, 10);
  const done = async (f, result) => { await run(() => api.patch(`/followups/${f.id}`, { result, note: notes[f.id], due_date: dates[f.id] }), FOLLOWUP_RESULTS.find((r) => r.id === result).label); reload(); refreshCounts?.(); };
  return (
    <div className="page">
      <div className="head"><div><h1>Wiedervorlagen / Nachfassen</h1><div className="muted">Nach Angebotsversand automatisch angelegt. Ergebnis erfassen – Projektstatus wird gesetzt.</div></div></div>
      <div className="row" style={{ marginBottom: 12 }}>{[['today', 'Heute'], ['overdue', 'Überfällig'], ['open', 'Alle offenen'], ['done', 'Erledigt']].map(([v, l]) => <button key={v} className={`pill ${scope === v ? 'on' : ''}`} onClick={() => setScope(v)}>{l}</button>)}</div>
      {data?.length ? <div className="grid g2">{data.map((f) => (
        <div key={f.id} className="card stack">
          <div className="row between">
            <div><Link to={`/projekte/${f.project_id}?tab=angebot`}><b>{f.project_name}</b></Link><div className="muted small">{f.customer_name} {f.offer_number ? `· Angebot ${f.offer_number} · ${fmtEUR(f.total_net)}` : ''}</div></div>
            {f.status === 'offen' ? <Badge color={f.due_date < today ? 'red' : f.due_date === today ? 'yellow' : 'blue'}>{f.due_date < today ? 'überfällig ' : ''}{fmtDate(f.due_date)}</Badge> : <Badge color="green">{FOLLOWUP_RESULTS.find((r) => r.id === f.result)?.label}</Badge>}
          </div>
          <div>{f.note} {(f.contact_phone || f.customer_phone) && <>· ☎ <a href={`tel:${f.contact_phone || f.customer_phone}`}>{f.contact_phone || f.customer_phone}</a></>} {f.contact_name ? `(${f.contact_name})` : ''}</div>
          {f.status === 'offen' && <>
            <input placeholder="Gesprächsnotiz / Ergebnis" value={notes[f.id] || ''} onChange={(e) => setNotes({ ...notes, [f.id]: e.target.value })} />
            <div className="row"><span className="muted small">Neue Wiedervorlage (bei „noch offen“/„erneut“):</span><input type="date" style={{ width: 170 }} value={dates[f.id] || ''} onChange={(e) => setDates({ ...dates, [f.id]: e.target.value })} /></div>
            <div className="row">{FOLLOWUP_RESULTS.map((r) => <button key={r.id} className={`btn ${r.id === 'auftrag' ? 'success' : r.id === 'verloren' ? 'danger' : ''}`} onClick={() => done(f, r.id)}>{r.label}</button>)}</div>
          </>}
        </div>
      ))}</div> : <Empty>{data ? 'Keine Wiedervorlagen.' : 'Lade …'}</Empty>}
    </div>
  );
}
