import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, docUrl } from '../api.js';
import { Badge, Dropzone, Empty, Field, Seg, Tabs, TextInput, useAction, useLoad } from '../components/ui.jsx';
import LvWorkspace, { LvUpload } from '../components/LvWorkspace.jsx';
import RequestsPanel from '../components/RequestsPanel.jsx';
import OfferBuilder from '../components/OfferBuilder.jsx';
import { PROJECT_STATUS, DOC_CATEGORIES, statusLabel } from '../../../shared/status.js';
import { MODES } from '../../../shared/groups.js';
import { fmtDate, fmtDateTime, fmtEUR } from '../../../shared/format.js';
import { useApp } from '../App.jsx';

export default function Project() {
  const { id } = useParams();
  const [sp, setSp] = useSearchParams();
  const tab = sp.get('tab') || 'uebersicht';
  const setTab = (t) => setSp({ tab: t });
  const { data: p, reload } = useLoad(`/projects/${id}`, [id]);
  const [run] = useAction();
  const nav = useNavigate();
  const { refreshCounts } = useApp();
  const patch = async (b) => { await run(() => api.patch(`/projects/${id}`, b)); reload(); refreshCounts?.(); };
  if (!p) return <div className="page muted">Lade …</div>;
  const missing = p.lvs.reduce((s, l) => s + l.totals.missing, 0);
  const net = p.lvs.reduce((s, l) => s + l.totals.net, 0);
  const tabs = [
    { id: 'uebersicht', label: 'Übersicht' },
    { id: 'lv', label: 'LV & Kalkulation', count: p.lvs.length },
    { id: 'anfragen', label: 'Lieferantenanfragen', count: p.requests.length },
    { id: 'plaene', label: 'Pläne & Material', count: p.plans.length },
    { id: 'angebot', label: 'Angebot & Versand', count: p.offers.length },
    { id: 'akte', label: 'Projektakte', count: p.documents.length },
    { id: 'verlauf', label: 'Notizen & Verlauf' },
  ];
  const showDetect = p.detected_mode && p.detected_mode !== p.mode && p.detected_confidence >= 0.6;
  return (
    <div className="page">
      <div className="head">
        <div>
          <div className="muted small"><Link to="/projekte">Projekte</Link> › {p.number}</div>
          <h1>{p.name}</h1>
          <div className="muted">{p.customer_name || 'ohne Kunde'}{p.contact_name ? ` · ${p.contact_name}` : ''}{p.site ? ` · ${p.site}` : ''}</div>
        </div>
        <div className="row">
          <Seg options={Object.entries(MODES).map(([k, v]) => ({ value: k, label: v.label }))} value={p.mode} onChange={(m) => patch({ mode: m })} />
          <select style={{ width: 200 }} value={p.status} onChange={(e) => patch({ status: e.target.value })}>{PROJECT_STATUS.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</select>
        </div>
      </div>
      {showDetect && (
        <div className="callout warn" style={{ marginBottom: 12 }}>
          🤖 Das LV wirkt eher wie <b>{MODES[p.detected_mode].label}</b> ({Math.round(p.detected_confidence * 100)} % Sicherheit). Ihr gewählter Modus bleibt <b>{MODES[p.mode].label}</b>.{' '}
          <button className="btn sm" onClick={() => patch({ mode: p.detected_mode })}>Modus wechseln</button>
        </div>
      )}
      <div className="grid g4" style={{ marginBottom: 14 }}>
        <div className="card kpi"><div className="l">Status</div><div className="v" style={{ fontSize: 18 }}><Badge color={PROJECT_STATUS.find((s) => s.id === p.status)?.color}>{statusLabel(p.status)}</Badge></div></div>
        <div className="card kpi"><div className="l">Angebotssumme netto</div><div className="v">{fmtEUR(net)}</div></div>
        <div className="card kpi"><div className="l">Offen (Preis oder Menge fehlt)</div><div className="v" style={{ color: missing ? 'var(--yellow)' : 'var(--green)' }}>{missing}</div></div>
        <div className="card kpi"><div className="l">Angebote / Versionen</div><div className="v">{p.offers.length}</div></div>
      </div>
      <Tabs tabs={tabs} value={tab} onChange={setTab} />
      {tab === 'uebersicht' && <Overview p={p} patch={patch} setTab={setTab} />}
      {tab === 'lv' && <LvTab p={p} reloadProject={reload} upload={sp.get('upload') === '1'} lvParam={sp.get('lv')} />}
      {tab === 'anfragen' && <RequestsPanel projectId={p.id} onChange={reload} />}
      {tab === 'plaene' && <PlansTab p={p} reload={reload} autoUpload={sp.get('upload') === '1'} nav={nav} />}
      {tab === 'angebot' && <OfferBuilder project={p} reload={reload} />}
      {tab === 'akte' && <Akte p={p} reload={reload} />}
      {tab === 'verlauf' && <History p={p} reload={reload} />}
    </div>
  );
}

function Overview({ p, patch, setTab }) {
  const steps = [
    ['LV / Plan erfasst', p.lvs.length > 0, 'lv'],
    ['Lieferanten zugeordnet & angefragt', p.requests.some((r) => r.status !== 'vorbereitet'), 'anfragen'],
    ['Preise vollständig', p.lvs.length > 0 && p.lvs.every((l) => !l.totals.missing), 'lv'],
    ['Kalkuliert', ['kalkulation', 'pruefung', 'angebot_fertig', 'versendet', 'nachfassen', 'auftrag'].includes(p.status), 'lv'],
    ['Angebot erstellt', p.offers.length > 0, 'angebot'],
    ['Versendet', p.offers.some((o) => o.status === 'versendet'), 'angebot'],
    ['Nachgefasst / Ergebnis', ['auftrag', 'verloren'].includes(p.status), 'angebot'],
  ];
  const F = ({ k, label, type }) => <Field label={label}><TextInput type={type} value={p[k] || ''} onCommit={(v) => patch({ [k]: v })} /></Field>;
  return (
    <div className="grid g2">
      <div className="card">
        <h2>Projektdaten <span className="muted small">(Änderungen werden automatisch gespeichert)</span></h2>
        <div className="grid g2">
          {F({ k: 'name', label: 'Projekt / Bauvorhaben' })}
          <Field label="Kunde"><TextInput value={p.customer_name || ''} onCommit={(v) => patch({ customer_name: v })} /></Field>
          {F({ k: 'contact_name', label: 'Ansprechpartner' })}
          {F({ k: 'contact_email', label: 'E-Mail', type: 'email' })}
          {F({ k: 'contact_phone', label: 'Telefon' })}
          {F({ k: 'site', label: 'Baustelle' })}
          {F({ k: 'due_date', label: 'Abgabetermin', type: 'date' })}
          {F({ k: 'number', label: 'Projektnummer' })}
        </div>
        <Field label="Notizen" style={{ marginTop: 10 }}><TextInput multiline value={p.notes || ''} onCommit={(v) => patch({ notes: v })} /></Field>
      </div>
      <div className="card">
        <h2>Ablauf</h2>
        <div className="list">{steps.map(([l, done, t], i) => (
          <div key={l} className="row between"><span>{done ? '🟢' : '⚪'} {i + 1}. {l}</span>{!done && <button className="btn sm" onClick={() => setTab(t)}>Öffnen →</button>}</div>
        ))}</div>
        {p.due_date && <div className="callout" style={{ marginTop: 10 }}>Abgabe: <b>{fmtDate(p.due_date)}</b></div>}
      </div>
    </div>
  );
}

function LvTab({ p, reloadProject, upload, lvParam }) {
  const [lvId, setLvId] = useState(Number(lvParam) || p.lvs.at(-1)?.id || null);
  const [showUpload, setShowUpload] = useState(upload || !p.lvs.length);
  useEffect(() => { if (!lvId && p.lvs.length) setLvId(p.lvs.at(-1).id); }, [p.lvs, lvId]);
  const [run] = useAction();
  return (
    <div className="stack">
      <div className="row">
        {p.lvs.map((l) => <button key={l.id} className={`btn ${l.id === lvId && !showUpload ? 'primary' : ''}`} onClick={() => { setLvId(l.id); setShowUpload(false); }}>{l.name} <span className="small">({l.totals.count})</span></button>)}
        <button className="btn" onClick={() => setShowUpload(!showUpload)}>＋ LV hochladen</button>
        <button className="btn ghost" onClick={async () => { const r = await run(() => api.post(`/projects/${p.id}/lvs`, { name: 'Manuelles LV' })); if (r) { await reloadProject(); setLvId(r.lvId); setShowUpload(false); } }}>＋ Leeres LV</button>
      </div>
      {showUpload && <LvUpload projectId={p.id} onDone={async (r) => { await reloadProject(); setLvId(r.lvId); setShowUpload(false); }} />}
      {!showUpload && lvId && <LvWorkspace key={lvId} lvId={lvId} onChange={reloadProject} onDeleted={async () => { setLvId(null); await reloadProject(); }} />}
    </div>
  );
}

function PlansTab({ p, reload, autoUpload, nav }) {
  const [run, busy] = useAction();
  const up = async (files) => {
    const r = await run(() => api.upload(`/projects/${p.id}/plans`, files[0]), 'Plan gespeichert');
    if (r) nav(`/plan/${r.id}`);
  };
  return (
    <div className="grid g2">
      <div className="card">
        <h2>Plan hochladen & analysieren</h2>
        <Dropzone camera onFiles={up} accept=".pdf,.png,.jpg,.jpeg,.webp" label={busy ? 'Lade hoch …' : autoUpload ? '👉 Bauplan hier ablegen' : 'Bauplan, Grundriss, Detail-, Terrassen- oder Bewehrungsplan'} hint="PDF, PNG, JPG – Maße werden per KI gelesen oder mit dem Messwerkzeug ermittelt" />
      </div>
      <div className="card">
        <h2>Pläne im Projekt</h2>
        {p.plans.length ? <div className="list">{p.plans.map((pl) => <div key={pl.id} className="row between"><Link to={`/plan/${pl.id}`}>📐 {pl.name}</Link><Badge color={pl.status === 'analysiert' ? 'green' : 'gray'}>{pl.status}</Badge></div>)}</div> : <Empty>Noch keine Pläne.</Empty>}
        <hr /><Link to={`/rechner?project=${p.id}`}>🧮 Material-Rechner für dieses Projekt öffnen →</Link>
      </div>
    </div>
  );
}

function Akte({ p, reload }) {
  const [cat, setCat] = useState('anhang');
  const [run, busy] = useAction();
  const groups = Object.keys(DOC_CATEGORIES).map((k) => [k, p.documents.filter((d) => d.category === k)]).filter(([, l]) => l.length);
  return (
    <div className="grid g2">
      <div className="card">
        <h2>Dokumente hinzufügen</h2>
        <div className="row" style={{ marginBottom: 10 }}>
          <select value={cat} onChange={(e) => setCat(e.target.value)} style={{ maxWidth: 260 }}>{Object.entries(DOC_CATEGORIES).filter(([k]) => !['lv_original', 'angebot'].includes(k)).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
        </div>
        <Dropzone camera multiple onFiles={async (files) => { await run(() => api.upload(`/projects/${p.id}/documents`, files, { category: cat }, 'files'), `${files.length} Datei(en) abgelegt`); reload(); }} label={busy ? 'Lade hoch …' : 'Dateien hierher ziehen'} hint="Datenblätter, Korrespondenz, Deckblatt, Fotos – alles in der Akte" />
        <div className="callout" style={{ marginTop: 12 }}>
          <b>Deckblatt fürs Angebot:</b> Datei mit Kategorie „Deckblatt“ hochladen und hier als Projekt-Deckblatt festlegen – kein Ausdrucken & Einscannen mehr.
          <div style={{ marginTop: 8 }}>
            <select value={p.cover_document_id || ''} onChange={async (e) => { await run(() => api.patch(`/projects/${p.id}`, { cover_document_id: e.target.value ? Number(e.target.value) : null }), 'Deckblatt festgelegt'); reload(); }}>
              <option value="">– Standard aus Einstellungen –</option>
              {p.documents.filter((d) => d.category === 'deckblatt' || /pdf|image/.test(d.mime)).map((d) => <option key={d.id} value={d.id}>{d.filename}</option>)}
            </select>
          </div>
        </div>
      </div>
      <div className="card">
        <h2>Digitale Projektakte</h2>
        {groups.length ? groups.map(([k, list]) => (
          <div key={k} style={{ marginBottom: 12 }}>
            <h3>{DOC_CATEGORIES[k]} <span className="muted small">({list.length})</span></h3>
            <div className="list">{list.map((d) => (
              <div key={d.id} className="row between">
                <div><a href={docUrl(d.id)} target="_blank" rel="noreferrer">{d.filename}</a>{d.version > 1 && <Badge color="blue">v{d.version}</Badge>}<div className="muted small">{fmtDateTime(d.created_at)} · {Math.ceil(d.size / 1024)} KB{d.note ? ` · ${d.note}` : ''}</div></div>
                <div className="row">
                  <a className="btn sm" href={docUrl(d.id, false)}>⬇</a>
                  {!['lv_original', 'angebot', 'lieferantenangebot'].includes(d.category) && <button className="btn sm danger" onClick={async () => { if (confirm(`„${d.filename}“ aus der Akte entfernen?`)) { await run(() => api.del(`/documents/${d.id}`)); reload(); } }}>✕</button>}
                </div>
              </div>
            ))}</div>
          </div>
        )) : <Empty>Akte ist leer.</Empty>}
        <div className="muted small">Originale, Angebote und Lieferantenangebote sind schreibgeschützt archiviert.</div>
      </div>
    </div>
  );
}

const FIELD = { ek: 'EK', qty: 'Menge', unit: 'Einheit', markup_pct: 'Aufschlag %', markup_source: 'Aufschlagsquelle', supplier_id: 'Lieferant', discount_pct: 'Rabatt %', freight: 'Fracht', short_text: 'Kurztext', long_text: 'Langtext', group_name: 'Warengruppe', product_id: 'Produkt', pos_type: 'Art', vk_override: 'VK fix' };

function History({ p, reload }) {
  const audit = useLoad(`/projects/${p.id}/audit`, [p.updated_at]);
  const [text, setText] = useState('');
  const [kind, setKind] = useState('notiz');
  const [run] = useAction();
  const describe = (a) => {
    const d = a.details || {};
    if (a.action === 'status') return `Status: ${statusLabel(d.from)} → ${statusLabel(d.to)}${d.auto ? ' (automatisch)' : ''}`;
    if (a.action === 'geändert' && d) return `Pos. ${d.oz || ''}: ${Object.entries(d).filter(([k]) => !['oz', 'price_source', 'price_date'].includes(k)).map(([k, v]) => `${FIELD[k] || k} ${v?.from ?? '–'} → ${v?.to ?? '–'}`).join(', ')}`;
    if (a.action === 'Massenänderung') return `${d.count} Positionen: ${(d.summary || []).map((s) => `${FIELD[s.field] || s.field} ${s.from ?? '–'} → ${s.to}`).join(', ')}`;
    if (d.markup_pct !== undefined) return `${a.action}: ${d.markup_pct} % (${d.changed} geändert${d.skipped ? `, ${d.skipped} manuelle unverändert` : ''})`;
    return `${a.action}${d.filename ? `: ${d.filename}` : ''}${d.oz ? ` (Pos. ${d.oz})` : ''}${d.number ? ` ${d.number}` : ''}`;
  };
  return (
    <div className="grid g2">
      <div className="card">
        <h2>Notizen & Korrespondenz</h2>
        <div className="stack">
          <div className="row"><Seg options={[{ value: 'notiz', label: 'Notiz' }, { value: 'telefon', label: '📞 Telefonat' }, { value: 'mail', label: '✉ Mail' }]} value={kind} onChange={setKind} /></div>
          <textarea value={text} onChange={(e) => setText(e.target.value)} placeholder="Notiz, Gesprächsinhalt, Absprache …" />
          <button className="btn primary" disabled={!text.trim()} onClick={async () => { await run(() => api.post(`/projects/${p.id}/notes`, { text, kind })); setText(''); reload(); }}>Speichern</button>
        </div>
        <hr />
        <div className="list">{p.notes.map((n) => (
          <div key={n.id}><div className="row between"><Badge color={n.kind === 'telefon' ? 'blue' : n.kind === 'mail' ? 'green' : 'gray'}>{n.kind}</Badge><span className="muted small">{fmtDateTime(n.created_at)} · {n.user_name || ''} <button className="btn ghost sm" onClick={async () => { await run(() => api.del(`/notes/${n.id}`)); reload(); }}>✕</button></span></div><div style={{ whiteSpace: 'pre-wrap', marginTop: 4 }}>{n.text}</div></div>
        ))}</div>
      </div>
      <div className="card">
        <h2>Verlauf (Audit-Log)</h2>
        <div className="list" style={{ maxHeight: 600, overflow: 'auto' }}>{(audit.data || []).map((a) => (
          <div key={a.id}><div className="muted small">{fmtDateTime(a.ts)} · {a.user_name || 'System'}</div><div>{describe(a)}</div></div>
        ))}</div>
      </div>
    </div>
  );
}
