import { useState } from 'react';
import { api } from '../api.js';
import { Badge, Empty, Field, Modal, Tabs, useAction, useLoad } from '../components/ui.jsx';
import { GROUP_NAMES, MODES } from '../../../shared/groups.js';
import { fmtDate, fmtNum, KNOWN_UNITS } from '../../../shared/format.js';

const KINDS = { notiz: 'Notiz', alternative: 'Alternativprodukt', technik: 'Technische Daten', erfahrung: 'Erfahrungswert', schema: 'Kalkulationsschema', hersteller: 'Hersteller', objektpreis: 'Objektpreis' };

export default function Knowledge() {
  const [tab, setTab] = useState('fragen');
  return (
    <div className="page">
      <div className="head"><div><h1>Produkte & Wissensdatenbank</h1><div className="muted">Produkte, Hersteller, technische Daten, Alternativen, Erfahrungswerte und Kalkulationsschemata – durchsuchbar und per KI befragbar.</div></div></div>
      <Tabs tabs={[{ id: 'fragen', label: '💬 Fragen' }, { id: 'produkte', label: 'Produkte' }, { id: 'wissen', label: 'Wissenseinträge' }]} value={tab} onChange={setTab} />
      {tab === 'fragen' && <Ask />}
      {tab === 'produkte' && <Products />}
      {tab === 'wissen' && <Entries />}
    </div>
  );
}

function Ask() {
  const [q, setQ] = useState('');
  const [res, setRes] = useState(null);
  const [run, busy] = useAction();
  return (
    <div className="stack">
      <form className="row" onSubmit={async (e) => { e.preventDefault(); const r = await run(() => api.post('/knowledge/ask', { question: q })); if (r) setRes(r); }}>
        <input className="grow" style={{ width: 'auto' }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="z. B. „Welche Alternative haben wir zu Rechteckpflaster 20/10/8?“" />
        <button className="btn primary" disabled={busy || !q.trim()}>{busy ? 'Suche …' : 'Fragen'}</button>
      </form>
      {res && <>
        {res.answer && <div className="card"><h3>🤖 Antwort (nur aus interner Datenbasis)</h3><div style={{ whiteSpace: 'pre-wrap' }}>{res.answer}</div></div>}
        {res.aiError && <div className="callout warn">{res.aiError}</div>}
        {!res.products.length && !res.knowledge.length && !res.prices.length && <div className="callout warn">Keine passenden Einträge in der Wissensbasis. Legen Sie Produkte/Wissen an, damit die Suche Antworten findet.</div>}
        {res.products.length > 0 && <div className="card"><h3>Produkte</h3>{res.products.map((p) => <div key={p.id} style={{ marginBottom: 8 }}><b>{p.name}</b> <span className="muted">{p.manufacturer} · {p.group_name} {p.format}</span>{p.alternativeProducts.length > 0 && <div>↔ Alternativen: {p.alternativeProducts.map((a) => <Badge key={a.id} color="blue">{a.name}</Badge>)}</div>}{p.notes && <div className="muted small">{p.notes}</div>}</div>)}</div>}
        {res.knowledge.length > 0 && <div className="card"><h3>Wissen</h3>{res.knowledge.map((k) => <div key={k.id} style={{ marginBottom: 8 }}><Badge color="gray">{KINDS[k.kind] || k.kind}</Badge> <b>{k.title}</b><div style={{ whiteSpace: 'pre-wrap' }}>{k.body}</div></div>)}</div>}
        {res.prices.length > 0 && <div className="card"><h3>Preise</h3>{res.prices.map((p) => <div key={p.id}>{fmtDate(p.date)} · {p.text}: <b>{fmtNum(p.price)} €/{p.unit}</b> · {p.supplier_name}</div>)}</div>}
      </>}
    </div>
  );
}

function Products() {
  const [q, setQ] = useState('');
  const { data, reload } = useLoad(`/products?q=${encodeURIComponent(q)}`, [q]);
  const [edit, setEdit] = useState(null);
  const [run] = useAction();
  const save = async () => {
    const r = await run(() => (edit.id ? api.patch(`/products/${edit.id}`, edit) : api.post('/products', edit)), 'Produkt gespeichert');
    if (r) { setEdit(null); reload(); }
  };
  return (
    <div className="stack">
      <div className="row between"><input style={{ maxWidth: 360 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Produkt, Hersteller, Art.-Nr." /><button className="btn primary" onClick={() => setEdit({ name: '', manufacturer: '', group_name: '', unit: 'm²', format: '', article_no: '', specs: '', notes: '', alternatives: [] })}>＋ Produkt</button></div>
      <div className="card" style={{ padding: 0 }}>
        {data?.length ? <table className="t"><thead><tr><th>Produkt</th><th>Hersteller</th><th>Gruppe</th><th>Format</th><th className="num">Letzter EK</th><th>Alternativen</th></tr></thead><tbody>
          {data.map((p) => <tr key={p.id} className="click" onClick={() => setEdit({ ...p })}><td><b>{p.name}</b><div className="muted small">{p.article_no}</div></td><td>{p.manufacturer}</td><td>{p.group_name}</td><td>{p.format}</td><td className="num">{p.last_price !== null ? `${fmtNum(p.last_price)} € · ${fmtDate(p.last_price_date)}` : '–'}</td><td>{(p.alternatives || []).length || ''}</td></tr>)}
        </tbody></table> : <Empty>{data ? 'Keine Produkte.' : 'Lade …'}</Empty>}
      </div>
      {edit && <Modal title={edit.id ? edit.name : 'Neues Produkt'} onClose={() => setEdit(null)} wide footer={<>{edit.id && <button className="btn danger" onClick={async () => { if (confirm('Produkt löschen?')) { await run(() => api.del(`/products/${edit.id}`)); setEdit(null); reload(); } }}>Löschen</button>}<span className="grow" /><button className="btn primary" disabled={!edit.name} onClick={save}>Speichern</button></>}>
        <div className="grid g3">
          {[['name', 'Bezeichnung *'], ['manufacturer', 'Hersteller'], ['article_no', 'Artikel-Nr.'], ['format', 'Format']].map(([k, l]) => <Field key={k} label={l}><input value={edit[k] || ''} onChange={(e) => setEdit({ ...edit, [k]: e.target.value })} /></Field>)}
          <Field label="Warengruppe"><select value={edit.group_name || ''} onChange={(e) => setEdit({ ...edit, group_name: e.target.value })}><option value="">–</option>{GROUP_NAMES.map((g) => <option key={g}>{g}</option>)}</select></Field>
          <Field label="Einheit"><select value={edit.unit || ''} onChange={(e) => setEdit({ ...edit, unit: e.target.value })}>{KNOWN_UNITS.map((u) => <option key={u}>{u}</option>)}</select></Field>
          <Field label="Bereich"><select value={edit.mode || ''} onChange={(e) => setEdit({ ...edit, mode: e.target.value })}><option value="">beide</option>{Object.entries(MODES).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}</select></Field>
        </div>
        <Field label="Technische Daten" style={{ marginTop: 10 }}><textarea value={edit.specs || ''} onChange={(e) => setEdit({ ...edit, specs: e.target.value })} /></Field>
        <Field label="Interne Notizen" style={{ marginTop: 10 }}><textarea value={edit.notes || ''} onChange={(e) => setEdit({ ...edit, notes: e.target.value })} /></Field>
        <div style={{ marginTop: 10 }}><div className="muted small">Alternativprodukte</div><div className="pill-input">{(data || []).filter((x) => x.id !== edit.id).map((x) => <span key={x.id} className={`pill ${(edit.alternatives || []).includes(x.id) ? 'on' : ''}`} onClick={() => setEdit({ ...edit, alternatives: (edit.alternatives || []).includes(x.id) ? edit.alternatives.filter((a) => a !== x.id) : [...(edit.alternatives || []), x.id] })}>{x.name}</span>)}</div></div>
      </Modal>}
    </div>
  );
}

function Entries() {
  const [q, setQ] = useState('');
  const { data, reload } = useLoad(`/knowledge?q=${encodeURIComponent(q)}`, [q]);
  const [edit, setEdit] = useState(null);
  const [run] = useAction();
  return (
    <div className="stack">
      <div className="row between"><input style={{ maxWidth: 360 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Suchen …" /><button className="btn primary" onClick={() => setEdit({ kind: 'notiz', title: '', body: '', tags: '' })}>＋ Eintrag</button></div>
      <div className="grid g2">{(data || []).map((k) => <div key={k.id} className="card" style={{ cursor: 'pointer' }} onClick={() => setEdit({ ...k })}><Badge color="gray">{KINDS[k.kind] || k.kind}</Badge> <b>{k.title}</b><div style={{ whiteSpace: 'pre-wrap', marginTop: 6 }}>{k.body}</div><div className="muted small">{k.tags}{k.product_name ? ` · ${k.product_name}` : ''}</div></div>)}</div>
      {data?.length === 0 && <Empty>Keine Einträge.</Empty>}
      {edit && <Modal title={edit.id ? 'Eintrag bearbeiten' : 'Neuer Wissenseintrag'} onClose={() => setEdit(null)} footer={<>{edit.id && <button className="btn danger" onClick={async () => { await run(() => api.del(`/knowledge/${edit.id}`)); setEdit(null); reload(); }}>Löschen</button>}<span className="grow" /><button className="btn primary" disabled={!edit.title} onClick={async () => { const r = await run(() => (edit.id ? api.patch(`/knowledge/${edit.id}`, edit) : api.post('/knowledge', edit)), 'Gespeichert'); if (r) { setEdit(null); reload(); } }}>Speichern</button></>}>
        <div className="stack">
          <div className="grid g2"><Field label="Art"><select value={edit.kind} onChange={(e) => setEdit({ ...edit, kind: e.target.value })}>{Object.entries(KINDS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field><Field label="Titel"><input value={edit.title} onChange={(e) => setEdit({ ...edit, title: e.target.value })} /></Field></div>
          <Field label="Inhalt"><textarea rows={8} value={edit.body || ''} onChange={(e) => setEdit({ ...edit, body: e.target.value })} /></Field>
          <Field label="Schlagworte"><input value={edit.tags || ''} onChange={(e) => setEdit({ ...edit, tags: e.target.value })} placeholder="pflaster, alternative, …" /></Field>
        </div>
      </Modal>}
    </div>
  );
}
