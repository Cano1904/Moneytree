import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { Badge, Empty, Field, Modal, useAction, useLoad } from '../components/ui.jsx';
import { GROUPS, MODES } from '../../../shared/groups.js';
import { fmtDate, fmtNum } from '../../../shared/format.js';

const blank = { name: '', phone: '', email: '', address: '', groups: [], delivery_days: '', preferred: 0, notes: '', contacts: [] };

export default function Suppliers() {
  const [q, setQ] = useState('');
  const [group, setGroup] = useState('');
  const { data, reload } = useLoad(`/suppliers?q=${encodeURIComponent(q)}${group ? `&group=${encodeURIComponent(group)}` : ''}`, [q, group]);
  const [edit, setEdit] = useState(null);
  const [detail, setDetail] = useState(null);
  return (
    <div className="page">
      <div className="head"><div><h1>Lieferanten</h1><div className="muted">Kontakte, Produktgruppen, Lieferzeiten, Preise und frühere Angebote.</div></div><button className="btn primary" onClick={() => setEdit(blank)}>＋ Lieferant</button></div>
      <div className="row" style={{ marginBottom: 12 }}>
        <input style={{ maxWidth: 320 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Suchen …" />
        <select style={{ maxWidth: 220 }} value={group} onChange={(e) => setGroup(e.target.value)}><option value="">Alle Warengruppen</option>{GROUPS.map((g) => <option key={g.name}>{g.name}</option>)}</select>
      </div>
      <div className="card" style={{ padding: 0 }}>
        {data?.length ? <table className="t"><thead><tr><th>Firma</th><th>Kontakt</th><th>Warengruppen</th><th className="num">Lieferzeit</th><th className="num">offene Anfragen</th><th className="num">Angebote</th><th /></tr></thead><tbody>
          {data.map((s) => <tr key={s.id} className="click" onClick={() => setDetail(s.id)}>
            <td><b>{s.name}</b> {s.preferred ? <Badge color="green">★ bevorzugt</Badge> : null}</td>
            <td className="small">{s.phone && <div>☎ {s.phone}</div>}{s.email && <div>✉ {s.email}</div>}{s.contacts.map((c) => <div key={c.id} className="muted">{c.name}{c.role ? ` (${c.role})` : ''}</div>)}</td>
            <td className="small">{(s.groups || []).join(', ')}</td>
            <td className="num">{s.delivery_days ? `${s.delivery_days} T.` : '–'}</td><td className="num">{s.open_requests || '–'}</td><td className="num">{s.quotes || '–'}</td>
            <td><button className="btn sm" onClick={(e) => { e.stopPropagation(); setEdit({ ...s }); }}>Bearbeiten</button></td>
          </tr>)}
        </tbody></table> : <Empty>{data ? 'Keine Lieferanten. Legen Sie Lieferanten mit Warengruppen an – dann schlägt die App sie automatisch vor.' : 'Lade …'}</Empty>}
      </div>
      {edit && <SupplierEdit s={edit} onClose={() => { setEdit(null); reload(); }} />}
      {detail && <SupplierDetail id={detail} onClose={() => setDetail(null)} onEdit={(s) => { setDetail(null); setEdit(s); }} />}
    </div>
  );
}

function SupplierEdit({ s: init, onClose }) {
  const [s, setS] = useState({ ...init, contacts: init.contacts || [] });
  const [run, busy] = useAction();
  const set = (k) => (e) => setS({ ...s, [k]: e.target.value });
  const toggleGroup = (g) => setS({ ...s, groups: s.groups.includes(g) ? s.groups.filter((x) => x !== g) : [...s.groups, g] });
  const save = async () => {
    const body = { ...s, delivery_days: s.delivery_days === '' ? null : Number(s.delivery_days), preferred: s.preferred ? 1 : 0 };
    const r = await run(() => (s.id ? api.patch(`/suppliers/${s.id}`, body) : api.post('/suppliers', body)), 'Lieferant gespeichert');
    if (r) onClose();
  };
  return (
    <Modal title={s.id ? s.name : 'Neuer Lieferant'} onClose={onClose} wide footer={<>{s.id && <button className="btn danger" onClick={async () => { if (confirm('Lieferant löschen?')) { await run(() => api.del(`/suppliers/${s.id}`)); onClose(); } }}>Löschen</button>}<span className="grow" /><button className="btn" onClick={onClose}>Abbrechen</button><button className="btn primary" disabled={busy || !s.name} onClick={save}>Speichern</button></>}>
      <div className="stack">
        <div className="grid g3">
          <Field label="Firma *"><input value={s.name} onChange={set('name')} /></Field>
          <Field label="Telefon"><input value={s.phone || ''} onChange={set('phone')} /></Field>
          <Field label="E-Mail (Anfragen)"><input value={s.email || ''} onChange={set('email')} /></Field>
          <Field label="Adresse"><input value={s.address || ''} onChange={set('address')} /></Field>
          <Field label="Lieferzeit (Tage)"><input value={s.delivery_days ?? ''} onChange={set('delivery_days')} /></Field>
          <label className="row" style={{ alignSelf: 'end' }}><input type="checkbox" checked={Boolean(s.preferred)} onChange={(e) => setS({ ...s, preferred: e.target.checked ? 1 : 0 })} /> bevorzugter Lieferant</label>
        </div>
        <div><div className="muted small">Produktgruppen (für automatische Lieferantenvorschläge)</div>
          {Object.entries(MODES).map(([m, ml]) => <div key={m} className="pill-input" style={{ marginTop: 6 }}><span className="muted small" style={{ width: 90 }}>{ml.short}:</span>{GROUPS.filter((g) => g.mode === m || (g.mode === 'both' && m === 'galabau')).map((g) => <span key={g.name} className={`pill ${s.groups.includes(g.name) ? 'on' : ''}`} onClick={() => toggleGroup(g.name)}>{g.name}</span>)}</div>)}
        </div>
        <Field label="Notizen (Konditionen, Frachtregeln, Besonderheiten)"><textarea value={s.notes || ''} onChange={set('notes')} /></Field>
        <div>
          <div className="row between"><h3>Ansprechpartner</h3><button className="btn sm" onClick={() => setS({ ...s, contacts: [...s.contacts, { name: '', role: '', phone: '', email: '' }] })}>＋ Kontakt</button></div>
          {s.contacts.map((c, i) => <div key={c.id || `n${i}`} className="grid g4" style={{ marginBottom: 6 }}>
            {['name', 'role', 'phone', 'email'].map((k) => <input key={k} placeholder={{ name: 'Name (z. B. Frau Petersen)', role: 'Funktion', phone: 'Telefon', email: 'E-Mail' }[k]} value={c[k] || ''} onChange={(e) => setS({ ...s, contacts: s.contacts.map((x, j) => (j === i ? { ...x, [k]: e.target.value } : x)) })} />)}
          </div>)}
        </div>
      </div>
    </Modal>
  );
}

function SupplierDetail({ id, onClose, onEdit }) {
  const { data: s } = useLoad(`/suppliers/${id}`, [id]);
  if (!s) return null;
  return (
    <Modal title={s.name} wide onClose={onClose} footer={<button className="btn" onClick={() => onEdit(s)}>Bearbeiten</button>}>
      <div className="grid g2">
        <div><h3>Kontakt</h3><div>{s.phone && <>☎ <a href={`tel:${s.phone}`}>{s.phone}</a><br /></>}{s.email && <>✉ <a href={`mailto:${s.email}`}>{s.email}</a></>}</div>
          {s.contacts.map((c) => <div key={c.id} className="muted">{c.name} {c.role && `(${c.role})`} {c.phone} {c.email}</div>)}
          <p>{s.notes}</p></div>
        <div><h3>Anfragen</h3>{s.requests.slice(0, 10).map((r) => <div key={r.id} className="small"><Link to={`/projekte/${r.project_id}?tab=anfragen`}>{r.project_name}</Link> · {r.status} · {fmtDate(r.sent_at || r.created_at)}</div>)}</div>
      </div>
      <h3 style={{ marginTop: 14 }}>Frühere Angebote</h3>
      {s.quotes.length ? <table className="t"><thead><tr><th>Datum</th><th>Projekt</th><th>Nr.</th><th>Positionen</th><th>gültig bis</th></tr></thead><tbody>{s.quotes.map((q) => <tr key={q.id}><td>{fmtDate(q.created_at)}</td><td>{q.project_name}</td><td>{q.quote_no}</td><td>{q.items}</td><td>{fmtDate(q.valid_until)}</td></tr>)}</tbody></table> : <div className="muted">–</div>}
      <h3 style={{ marginTop: 14 }}>Einkaufspreise</h3>
      {s.prices.length ? <table className="t"><thead><tr><th>Datum</th><th>Produkt</th><th className="num">EK</th><th>Projekt</th><th>Quelle</th></tr></thead><tbody>{s.prices.map((p) => <tr key={p.id}><td>{fmtDate(p.date)}</td><td>{p.text}</td><td className="num">{fmtNum(p.price)} €/{p.unit}</td><td>{p.project_name}</td><td>{p.source}</td></tr>)}</tbody></table> : <div className="muted">–</div>}
    </Modal>
  );
}
