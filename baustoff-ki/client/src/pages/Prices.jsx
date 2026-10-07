import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { Empty, Field, Modal, useAction, useLoad } from '../components/ui.jsx';
import { fmtDate, fmtNum, KNOWN_UNITS } from '../../../shared/format.js';
import { GROUP_NAMES } from '../../../shared/groups.js';

export default function Prices() {
  const [q, setQ] = useState('');
  const [sup, setSup] = useState('');
  const { data, reload } = useLoad(`/prices?q=${encodeURIComponent(q)}${sup ? `&supplier_id=${sup}` : ''}`, [q, sup]);
  const suppliers = useLoad('/suppliers');
  const [add, setAdd] = useState(null);
  const [run] = useAction();
  // Preisentwicklung je Text gruppiert (min/max/letzter)
  const groups = Object.values((data || []).reduce((m, p) => { const k = `${p.product_name || p.text}|${p.unit}`; (m[k] ||= { name: p.product_name || p.text, unit: p.unit, list: [] }).list.push(p); return m; }, {}));
  return (
    <div className="page">
      <div className="head"><div><h1>Preishistorie & Objektpreise</h1><div className="muted">Jeder übernommene EK wird gespeichert – mit Datum, Lieferant, Projekt und Menge.</div></div><button className="btn primary" onClick={() => setAdd({ text: '', price: '', unit: 'm²', supplier_id: '', group_name: '', date: new Date().toISOString().slice(0, 10), valid_until: '' })}>＋ Preis erfassen</button></div>
      <div className="row" style={{ marginBottom: 12 }}>
        <input style={{ maxWidth: 360 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Produkt / Text / Warengruppe" />
        <select style={{ maxWidth: 240 }} value={sup} onChange={(e) => setSup(e.target.value)}><option value="">Alle Lieferanten</option>{(suppliers.data || []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
      </div>
      {q && groups.length > 0 && <div className="grid g3" style={{ marginBottom: 12 }}>{groups.slice(0, 6).map((g) => {
        const ps = g.list.map((x) => x.price);
        return <div key={g.name + g.unit} className="card kpi"><div className="l">{g.name}</div><div className="v">{fmtNum(g.list[0].price)} €<span className="small muted">/{g.unit}</span></div><div className="muted small">letzter EK {fmtDate(g.list[0].date)} · min {fmtNum(Math.min(...ps))} · max {fmtNum(Math.max(...ps))} · {ps.length} Preise</div></div>;
      })}</div>}
      <div className="card" style={{ padding: 0 }}>
        {data?.length ? <table className="t"><thead><tr><th>Datum</th><th>Lieferant</th><th>Produkt / Text</th><th className="num">EK</th><th>Projekt</th><th className="num">Menge</th><th>gültig bis</th><th>Quelle</th><th /></tr></thead><tbody>
          {data.map((p) => <tr key={p.id}><td>{fmtDate(p.date)}</td><td>{p.supplier_name}</td><td>{p.product_name || p.text}<div className="muted small">{p.group_name}</div></td><td className="num"><b>{fmtNum(p.price)} €</b>/{p.unit}</td><td>{p.project_id ? <Link to={`/projekte/${p.project_id}`}>{p.project_name}</Link> : ''}</td><td className="num">{p.qty ?? ''}</td><td>{fmtDate(p.valid_until)}</td><td>{p.source}</td><td><button className="btn sm ghost" onClick={async () => { if (confirm('Preis löschen?')) { await run(() => api.del(`/prices/${p.id}`)); reload(); } }}>✕</button></td></tr>)}
        </tbody></table> : <Empty>{data ? 'Keine Preise gefunden.' : 'Lade …'}</Empty>}
      </div>
      {add && <Modal title="Preis erfassen" onClose={() => setAdd(null)} footer={<button className="btn primary" disabled={!add.text || add.price === ''} onClick={async () => { const r = await run(() => api.post('/prices', { ...add, price: Number(String(add.price).replace(',', '.')), supplier_id: add.supplier_id || null }), 'Preis gespeichert'); if (r) { setAdd(null); reload(); } }}>Speichern</button>}>
        <div className="grid g2">
          <Field label="Produkt / Text"><input value={add.text} onChange={(e) => setAdd({ ...add, text: e.target.value })} /></Field>
          <Field label="Lieferant"><select value={add.supplier_id} onChange={(e) => setAdd({ ...add, supplier_id: e.target.value })}><option value="">–</option>{(suppliers.data || []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
          <Field label="EK €"><input value={add.price} onChange={(e) => setAdd({ ...add, price: e.target.value })} inputMode="decimal" /></Field>
          <Field label="Einheit"><select value={add.unit} onChange={(e) => setAdd({ ...add, unit: e.target.value })}>{KNOWN_UNITS.map((u) => <option key={u}>{u}</option>)}</select></Field>
          <Field label="Warengruppe"><select value={add.group_name} onChange={(e) => setAdd({ ...add, group_name: e.target.value })}><option value="">–</option>{GROUP_NAMES.map((g) => <option key={g}>{g}</option>)}</select></Field>
          <Field label="Datum"><input type="date" value={add.date} onChange={(e) => setAdd({ ...add, date: e.target.value })} /></Field>
          <Field label="Preisbindung bis"><input type="date" value={add.valid_until} onChange={(e) => setAdd({ ...add, valid_until: e.target.value })} /></Field>
        </div>
      </Modal>}
    </div>
  );
}
