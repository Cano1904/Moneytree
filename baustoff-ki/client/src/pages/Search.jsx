import { Link, useSearchParams } from 'react-router-dom';
import { docUrl } from '../api.js';
import { Badge, Empty, useLoad } from '../components/ui.jsx';
import { statusLabel, requestStatusLabel } from '../../../shared/status.js';
import { fmtDate, fmtEUR, fmtNum, fmtQty } from '../../../shared/format.js';

const render = {
  projects: (r) => <><Link to={`/projekte/${r.id}`}><b>{r.name}</b></Link> <span className="muted">{r.number} · {r.customer_name || ''}</span> <Badge color="gray">{statusLabel(r.status)}</Badge>{r.lvs && <div className="muted small">LVs: {r.lvs}</div>}</>,
  offers: (r) => <><Link to={`/projekte/${r.project_id}?tab=angebot`}><b>Angebot {r.number}</b></Link> · {r.project_name} · {r.customer_name} · {fmtEUR(r.total_net)} <Badge color={r.status === 'versendet' ? 'green' : 'blue'}>{r.status}</Badge></>,
  quotes: (r) => <><b>{r.supplier_name}</b> · {r.project_name} · {r.items} Positionen · {fmtDate(r.created_at)} {r.document_id && <a href={docUrl(r.document_id)} target="_blank" rel="noreferrer">Dokument</a>}</>,
  prices: (r) => <>{fmtDate(r.date)} · <b>{r.product_name || r.text}</b>: {fmtNum(r.price)} €/{r.unit} · {r.supplier_name || '?'} {r.project_name && <>· <Link to={`/projekte/${r.project_id}`}>{r.project_name}</Link></>}</>,
  requests: (r) => <><Link to={`/projekte/${r.project_id}?tab=anfragen`}>{r.supplier_name}</Link> · {r.project_name} <Badge color="yellow">{requestStatusLabel(r.status)}</Badge></>,
  positions: (r) => <><Link to={`/projekte/${r.project_id}?tab=lv&lv=${r.lv_id}`}>{r.oz} {r.short_text}</Link> · {fmtQty(r.qty)} {r.unit} · {r.project_name}</>,
  suppliers: (r) => <><Link to="/lieferanten">{r.name}</Link> {r.phone} {r.email}</>,
  customers: (r) => <><Link to={`/suche?q=${encodeURIComponent(`Projekte von Kunde ${r.name}`)}`}>{r.name}</Link> <span className="muted">{r.contact} {r.phone}</span></>,
  products: (r) => <><Link to="/wissen">{r.name}</Link> <span className="muted">{r.manufacturer} · {r.group_name}</span></>,
  knowledge: (r) => <><Link to="/wissen">{r.title}</Link> <Badge color="gray">{r.kind}</Badge></>,
  documents: (r) => <><a href={docUrl(r.id)} target="_blank" rel="noreferrer">{r.filename}</a> · {r.category} {r.project_name && <>· <Link to={`/projekte/${r.project_id}?tab=akte`}>{r.project_name}</Link></>}</>,
};

export default function Search() {
  const [sp] = useSearchParams();
  const q = sp.get('q') || '';
  const { data } = useLoad(`/search?q=${encodeURIComponent(q)}`, [q]);
  const i = data?.interpreted;
  return (
    <div className="page">
      <h1>Suche: „{q}“</h1>
      {i && <div className="muted" style={{ marginBottom: 12 }}>Verstanden: {[i.customer && `Kunde „${i.customer}“`, i.supplier && `Lieferant „${i.supplier}“`, i.product && `Produkt „${i.product}“`, !i.product && i.term && `Begriff „${i.term}“`, i.open && 'nur offene'].filter(Boolean).join(' · ') || 'Freitext'}</div>}
      {data?.sections.length ? data.sections.map((s) => (
        <div key={s.key} className="card"><h2>{s.title} <span className="muted small">({s.rows.length})</span></h2><div className="list">{s.rows.map((r) => <div key={`${s.key}${r.id}`}>{render[s.key](r)}</div>)}</div></div>
      )) : <Empty>{data ? 'Keine Treffer. Beispiele: „alle LVs von Kunde Müller“, „Projekte mit Kalksandstein“, „Angebote von Lieferant Betonwerk“, „Objektpreise für Pflaster“, „offene Angebote“.' : 'Suche …'}</Empty>}
    </div>
  );
}
