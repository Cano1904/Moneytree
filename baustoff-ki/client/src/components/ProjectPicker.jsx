// Projekt wählen oder in einem Schritt neu anlegen (Kunde wird bei Bedarf mit angelegt)
import { useState } from 'react';
import { api } from '../api.js';
import { Field, Modal, Seg, useAction, useLoad } from './ui.jsx';
import { useApp } from '../App.jsx';
import { MODES } from '../../../shared/groups.js';

export function NewProjectForm({ onCreated, submitLabel = 'Projekt anlegen' }) {
  const { mode } = useApp();
  const [f, setF] = useState({ name: '', customer_name: '', contact_name: '', contact_email: '', contact_phone: '', site: '', mode });
  const customers = useLoad('/customers');
  const [run, busy] = useAction();
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value });
  const submit = async (e) => {
    e.preventDefault();
    const p = await run(() => api.post('/projects', f), 'Projekt angelegt');
    if (p) onCreated(p);
  };
  return (
    <form onSubmit={submit} className="stack">
      <div className="grid g2">
        <Field label="Projekt / Bauvorhaben *"><input autoFocus required value={f.name} onChange={set('name')} placeholder="z. B. Außenanlagen Kita Sonnenschein" /></Field>
        <Field label="Kunde"><input list="cust-list" value={f.customer_name} onChange={set('customer_name')} placeholder="Name oder neu eingeben" /></Field>
        <datalist id="cust-list">{(customers.data || []).map((c) => <option key={c.id} value={c.name} />)}</datalist>
        <Field label="Ansprechpartner"><input value={f.contact_name} onChange={set('contact_name')} placeholder="Herr/Frau …" /></Field>
        <Field label="E-Mail"><input type="email" value={f.contact_email} onChange={set('contact_email')} /></Field>
        <Field label="Telefon"><input value={f.contact_phone} onChange={set('contact_phone')} /></Field>
        <Field label="Baustelle"><input value={f.site} onChange={set('site')} /></Field>
      </div>
      <div className="row between">
        <Seg options={Object.entries(MODES).map(([k, v]) => ({ value: k, label: v.label }))} value={f.mode} onChange={(m) => setF({ ...f, mode: m })} />
        <button className="btn primary" disabled={busy}>{submitLabel}</button>
      </div>
    </form>
  );
}

export default function ProjectPicker({ title, onPick, onClose }) {
  const [tab, setTab] = useState('new');
  const [q, setQ] = useState('');
  const list = useLoad(`/projects?limit=50&q=${encodeURIComponent(q)}`, [q]);
  return (
    <Modal title={title} onClose={onClose}>
      <div className="row" style={{ marginBottom: 14 }}>
        <Seg options={[{ value: 'new', label: '＋ Neues Projekt' }, { value: 'existing', label: 'Bestehendes Projekt' }]} value={tab} onChange={setTab} />
      </div>
      {tab === 'new' ? <NewProjectForm onCreated={onPick} submitLabel="Anlegen & weiter" /> : (
        <div className="stack">
          <input autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder="Projekt, Nummer oder Kunde suchen" />
          <div className="list" style={{ maxHeight: 360, overflow: 'auto' }}>
            {(list.data || []).map((p) => (
              <div key={p.id} className="row between" style={{ cursor: 'pointer' }} onClick={() => onPick(p)}>
                <div><b>{p.name}</b><div className="muted small">{p.number} · {p.customer_name || 'ohne Kunde'}</div></div>
                <button className="btn sm">Wählen</button>
              </div>
            ))}
            {list.data?.length === 0 && <div className="muted">Keine Projekte gefunden.</div>}
          </div>
        </div>
      )}
    </Modal>
  );
}
