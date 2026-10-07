import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Badge, Dot, Empty, useLoad } from '../components/ui.jsx';
import ProjectPicker from '../components/ProjectPicker.jsx';
import { PROJECT_STATUS, statusLabel, requestStatusLabel } from '../../../shared/status.js';
import { fmtEUR, fmtDate } from '../../../shared/format.js';

const days = (d) => `${Math.floor(d)} ${Math.floor(d) === 1 ? 'Tag' : 'Tage'}`;

export default function Dashboard() {
  const { data, reload } = useLoad('/dashboard');
  const nav = useNavigate();
  const [picker, setPicker] = useState(null);
  const actions = [
    ['📝', 'Neue Anfrage', 'Kunde/Projekt anlegen', () => setPicker({ title: 'Neue Kundenanfrage', go: (p) => `/projekte/${p.id}` })],
    ['📄', 'Neues LV', 'PDF, Excel, GAEB, Word, Scan', () => setPicker({ title: 'LV hochladen – Projekt wählen', go: (p) => `/projekte/${p.id}?tab=lv&upload=1` })],
    ['📐', 'Plan analysieren', 'Maße & Materialliste', () => setPicker({ title: 'Plan analysieren – Projekt wählen', go: (p) => `/projekte/${p.id}?tab=plaene&upload=1` })],
    ['🧮', 'Kalkulation', 'Material-Rechner', () => nav('/rechner')],
    ['🚚', 'Lieferanten', 'Kontakte & Preise', () => nav('/lieferanten')],
    ['✉️', 'Offene Anfragen', 'Welche Preise fehlen?', () => nav('/anfragen')],
    ['📑', 'Angebote', 'offen / versendet', () => nav('/suche?q=offene%20Angebote')],
    ['🗂️', 'Projektakten', 'Digitale Ordner', () => nav('/projekte')],
    ['⏰', 'Wiedervorlagen', 'Heute nachfassen', () => nav('/wiedervorlagen')],
  ];
  if (!data) return <div className="page muted">Lade …</div>;
  const empty = !Object.keys(data.statusCounts).length;
  return (
    <div className="page">
      <div className="head">
        <div><h1>Guten Tag 👋</h1><div className="muted">Kundenanfrage → LV/Plan → Lieferanten → Preise → Kalkulation → Angebot → Versand → Nachfassen</div></div>
        <div className="row">{data.ai.enabled ? <Badge color="green">KI aktiv · {data.ai.model}</Badge> : <Link to="/einstellungen"><Badge color="yellow">KI aus – Regel-Modus (in Einstellungen aktivieren)</Badge></Link>}</div>
      </div>
      <div className="grid g3 actions" style={{ marginBottom: 14 }}>
        {actions.map(([ico, t, s, fn]) => <button key={t} className="btn big" onClick={fn}><span className="ico">{ico}</span><span style={{ textAlign: 'left' }}>{t}<div className="muted small sub" style={{ fontWeight: 400 }}>{s}</div></span></button>)}
      </div>
      {empty && <div className="callout" style={{ marginBottom: 14 }}>Noch keine Projekte. Starten Sie mit <b>Neues LV</b> – oder laden Sie unter <Link to="/einstellungen">Einstellungen</Link> die Demo-Daten zum Ausprobieren.</div>}
      <div className="card">
        <h2>Statusübersicht</h2>
        <div className="pipeline">
          {PROJECT_STATUS.map((s) => <Link key={s.id} to={`/projekte?status=${s.id}`}><div className="n">{data.statusCounts[s.id] || 0}</div><div className="s"><Dot color={s.color} /> {s.label}</div></Link>)}
        </div>
      </div>
      <div className="grid g2" style={{ marginTop: 14 }}>
        <div className="card">
          <h2>🔴 Hier hängt Arbeit</h2>
          {data.stuck.length ? <div className="list">{data.stuck.map((p) => (
            <div key={p.id} className="row between"><div><Link to={`/projekte/${p.id}`}><b>{p.name}</b></Link><div className="muted small">{p.customer_name} · seit {days(p.days_in_status)} in „{statusLabel(p.status)}“{p.missing_prices ? ` · ${p.missing_prices} Preise fehlen` : ''}</div></div><Badge color="red">{days(p.days_in_status)}</Badge></div>
          ))}</div> : <Empty>✅ Nichts blockiert.</Empty>}
        </div>
        <div className="card">
          <h2>⏰ Nachfassen</h2>
          <div className="row" style={{ marginBottom: 8 }}><Badge color="red">{data.followups.overdue.length} überfällig</Badge><Badge color="yellow">{data.followups.today.length} heute</Badge><Badge color="green">{data.followups.done} erledigt (30 T.)</Badge></div>
          {[...data.followups.overdue, ...data.followups.today].length ? <div className="list">{[...data.followups.overdue, ...data.followups.today].map((f) => (
            <div key={f.id} className="row between"><div><Link to={`/projekte/${f.project_id}`}><b>{f.project_name}</b></Link><div className="muted small">{f.customer_name} · {f.note} {f.contact_phone || f.customer_phone ? `· ☎ ${f.contact_phone || f.customer_phone}` : ''}</div></div><Badge color={f.due_date < new Date().toISOString().slice(0, 10) ? 'red' : 'yellow'}>{fmtDate(f.due_date)}</Badge></div>
          ))}</div> : <Empty>Keine fälligen Wiedervorlagen.</Empty>}
          <Link to="/wiedervorlagen">Alle Wiedervorlagen →</Link>
        </div>
        <div className="card">
          <h2>✉️ Offene Lieferantenanfragen</h2>
          {data.openRequests.length ? <table className="t"><thead><tr><th>Lieferant</th><th>Projekt</th><th>Status</th><th className="num">fehlt</th><th className="num">Alter</th></tr></thead><tbody>
            {data.openRequests.slice(0, 12).map((r) => <tr key={r.id} className="click" onClick={() => nav(`/projekte/${r.project_id}?tab=anfragen`)}><td>{r.supplier_name}{r.supplier_phone ? <div className="muted small">☎ {r.supplier_phone}</div> : null}</td><td>{r.project_name}</td><td><Badge color={r.status === 'rueckfrage' ? 'red' : r.status === 'vorbereitet' ? 'blue' : 'yellow'}>{requestStatusLabel(r.status)}</Badge></td><td className="num">{r.missing}</td><td className="num">{days(r.age_days)}</td></tr>)}
          </tbody></table> : <Empty>Keine offenen Anfragen.</Empty>}
        </div>
        <div className="card">
          <h2>💶 Fehlende Preise & offene Angebote</h2>
          <div className="row" style={{ marginBottom: 8 }}><Badge color="blue">{data.openOffers.n} Angebote offen · {fmtEUR(data.openOffers.value)}</Badge><Badge color="green">{data.won} Aufträge (90 T.)</Badge><Badge color="red">{data.lost} verloren</Badge></div>
          {data.missingPrices.length ? <div className="list">{data.missingPrices.map((p) => (
            <div key={p.id} className="row between"><Link to={`/projekte/${p.id}?tab=lv`}>{p.name}</Link><Badge color="yellow">{p.missing_prices} / {p.positions} Preise fehlen</Badge></div>
          ))}</div> : <Empty>Alle Preise vollständig.</Empty>}
        </div>
      </div>
      <div className="card" style={{ marginTop: 14 }}>
        <div className="row between"><h2>Zuletzt bearbeitet</h2><button className="btn sm" onClick={reload}>↻ Aktualisieren</button></div>
        <table className="t"><thead><tr><th>Projekt</th><th>Kunde</th><th>Modus</th><th>Status</th><th className="num">Preise</th></tr></thead><tbody>
          {data.recent.map((p) => <tr key={p.id} className="click" onClick={() => nav(`/projekte/${p.id}`)}><td><b>{p.name}</b><div className="muted small">{p.number}</div></td><td>{p.customer_name}</td><td>{p.mode === 'hochbau' ? 'Hochbau' : 'GaLa'}</td><td><Badge color={PROJECT_STATUS.find((s) => s.id === p.status)?.color}>{statusLabel(p.status)}</Badge></td><td className="num">{p.positions ? `${p.positions - p.missing_prices}/${p.positions}` : '–'}</td></tr>)}
        </tbody></table>
      </div>
      {picker && <ProjectPicker title={picker.title} onClose={() => setPicker(null)} onPick={(p) => nav(picker.go(p))} />}
    </div>
  );
}
