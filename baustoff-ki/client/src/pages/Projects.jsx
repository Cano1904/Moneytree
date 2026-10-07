import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Badge, Empty, Modal, useLoad } from '../components/ui.jsx';
import { NewProjectForm } from '../components/ProjectPicker.jsx';
import { PROJECT_STATUS, statusLabel } from '../../../shared/status.js';
import { fmtDate, fmtEUR } from '../../../shared/format.js';

export default function Projects() {
  const [sp, setSp] = useSearchParams();
  const [q, setQ] = useState('');
  const status = sp.get('status') || '';
  const { data } = useLoad(`/projects?totals=1&q=${encodeURIComponent(q)}${status ? `&status=${status}` : ''}`, [q, status]);
  const [creating, setCreating] = useState(false);
  const nav = useNavigate();
  return (
    <div className="page">
      <div className="head">
        <div><h1>Projekte & digitale Akten</h1><div className="muted">Jedes Projekt ist eine vollständige Akte: LV, Pläne, Anfragen, Angebote, Korrespondenz, Versionen.</div></div>
        <button className="btn primary" onClick={() => setCreating(true)}>＋ Neues Projekt</button>
      </div>
      <div className="row" style={{ marginBottom: 12 }}>
        <input style={{ maxWidth: 360 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Suchen: Projekt, Nummer, Kunde" />
        <select style={{ maxWidth: 220 }} value={status} onChange={(e) => setSp(e.target.value ? { status: e.target.value } : {})}>
          <option value="">Alle Status</option>
          {PROJECT_STATUS.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
        </select>
      </div>
      <div className="card" style={{ padding: 0 }}>
        {data?.length ? (
          <table className="t"><thead><tr><th>Projekt</th><th>Kunde</th><th>Status</th><th className="num">Angebotssumme netto</th><th className="num">fehlende Preise</th><th>Wiedervorlage</th><th>Geändert</th></tr></thead>
            <tbody>{data.map((p) => (
              <tr key={p.id} className="click" onClick={() => nav(`/projekte/${p.id}`)}>
                <td><b>{p.name}</b><div className="muted small">{p.number} · {p.mode === 'hochbau' ? 'Hochbau' : 'Tief-/GaLa-Bau'}</div></td>
                <td>{p.customer_name || '–'}</td>
                <td><Badge color={PROJECT_STATUS.find((s) => s.id === p.status)?.color}>{statusLabel(p.status)}</Badge></td>
                <td className="num">{p.totals?.count ? fmtEUR(p.totals.net) : '–'}</td>
                <td className="num">{p.missing_prices ? <Badge color="yellow">{p.missing_prices}</Badge> : '–'}</td>
                <td>{p.next_followup ? fmtDate(p.next_followup) : ''}</td>
                <td className="muted small">{fmtDate(p.updated_at)}</td>
              </tr>
            ))}</tbody></table>
        ) : <Empty>{data ? 'Keine Projekte gefunden.' : 'Lade …'}</Empty>}
      </div>
      {creating && <Modal title="Neues Projekt / Kundenanfrage" onClose={() => setCreating(false)}><NewProjectForm onCreated={(p) => nav(`/projekte/${p.id}`)} /></Modal>}
    </div>
  );
}
