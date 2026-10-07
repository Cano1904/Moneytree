import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Badge, Empty, Modal, useLoad } from '../components/ui.jsx';
import { RequestView } from '../components/RequestDialog.jsx';
import { REQUEST_STATUS, requestStatusLabel } from '../../../shared/status.js';
import { fmtDate } from '../../../shared/format.js';

const FILTERS = [['vorbereitet,angefragt,ausstehend,rueckfrage,preis_erhalten', 'Offen'], ['angefragt,ausstehend', 'Antwort ausstehend'], ['rueckfrage', 'Rückfrage'], ['vorbereitet', 'Vorbereitet'], ['vollstaendig', 'Vollständig'], ['', 'Alle']];

export default function Requests() {
  const [status, setStatus] = useState(FILTERS[0][0]);
  const { data, reload } = useLoad(`/requests${status ? `?status=${status}` : ''}`, [status]);
  const [open, setOpen] = useState(null);
  const color = (s) => REQUEST_STATUS.find((x) => x.id === s)?.color || 'gray';
  return (
    <div className="page">
      <div className="head"><div><h1>Lieferantenanfragen</h1><div className="muted">Alle Anfragen über alle Projekte – wer muss noch antworten, wo fehlen Preise?</div></div></div>
      <div className="row" style={{ marginBottom: 12 }}>{FILTERS.map(([v, l]) => <button key={l} className={`pill ${status === v ? 'on' : ''}`} onClick={() => setStatus(v)}>{l}</button>)}</div>
      <div className="card" style={{ padding: 0 }}>
        {data?.length ? <table className="t"><thead><tr><th>Lieferant</th><th>Projekt</th><th>Kanal</th><th>Status</th><th className="num">Pos.</th><th className="num">Preis fehlt</th><th>Angefragt</th><th /></tr></thead><tbody>
          {data.map((r) => <tr key={r.id}>
            <td><b>{r.supplier_name}</b>{r.supplier_phone && <div className="muted small">☎ <a href={`tel:${r.supplier_phone}`}>{r.supplier_phone}</a></div>}</td>
            <td><Link to={`/projekte/${r.project_id}?tab=anfragen`}>{r.project_name}</Link><div className="muted small">{r.project_number}</div></td>
            <td>{r.channel === 'telefon' ? '📞' : '✉'}</td>
            <td><Badge color={color(r.status)}>{requestStatusLabel(r.status)}</Badge></td>
            <td className="num">{r.items}</td><td className="num">{r.missing ? <Badge color="yellow">{r.missing}</Badge> : '0'}</td>
            <td>{fmtDate(r.sent_at)}</td>
            <td><button className="btn sm" onClick={() => setOpen(r)}>Öffnen</button></td>
          </tr>)}
        </tbody></table> : <Empty>{data ? 'Keine Anfragen in dieser Ansicht.' : 'Lade …'}</Empty>}
      </div>
      {open && <Modal title={`Anfrage · ${open.supplier_name}`} wide onClose={() => { setOpen(null); reload(); }}><RequestView id={open.id} projectId={open.project_id} onDone={() => { setOpen(null); reload(); }} /></Modal>}
    </div>
  );
}
