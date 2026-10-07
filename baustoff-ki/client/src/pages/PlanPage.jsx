// Plan analysieren: Planansicht mit Messwerkzeug (Kalibrierung, Strecke, Polylinie, Fläche, Zählen), KI-Analyse, Rechner, Materialliste
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import * as pdfjs from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import { api, docUrl } from '../api.js';
import { Badge, Dropzone, Empty, Field, Seg, Tabs, useAction, useLoad } from '../components/ui.jsx';
import ProjectPicker from '../components/ProjectPicker.jsx';
import CalcForm from '../components/CalcForm.jsx';
import MaterialList from '../components/MaterialList.jsx';
import { CALCULATORS, getCalculator } from '../../../shared/calculators.js';
import { fmtNum } from '../../../shared/format.js';

pdfjs.GlobalWorkerOptions.workerSrc = workerUrl;
const uid = () => Math.random().toString(36).slice(2, 10);
const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const polyLen = (pts) => pts.slice(1).reduce((s, p, i) => s + dist(pts[i], p), 0);
const polyArea = (pts) => Math.abs(pts.reduce((s, p, i) => { const q = pts[(i + 1) % pts.length]; return s + p.x * q.y - q.x * p.y; }, 0)) / 2;
const KIND_UNIT = { flaeche: 'm²', laenge: 'm', anzahl: 'St', hoehe: 'm', dicke: 'cm', volumen: 'm³', sonstiges: '' };

export default function PlanPage() {
  const { id } = useParams();
  return id ? <PlanWork id={id} /> : <PlanStart />;
}

function PlanStart() {
  const [project, setProject] = useState(null);
  const [pick, setPick] = useState(true);
  const [run, busy] = useAction();
  const nav = useNavigate();
  return (
    <div className="page">
      <h1>Plan analysieren</h1>
      <div className="muted" style={{ marginBottom: 12 }}>Bauplan hochladen → Maße per KI lesen oder selbst messen → Rechner → Materialliste → als LV ins Projekt.</div>
      {project ? (
        <div className="card">
          <div className="row between"><div>Projekt: <b>{project.name}</b></div><button className="btn sm" onClick={() => setPick(true)}>ändern</button></div>
          <div style={{ marginTop: 12 }}><Dropzone accept=".pdf,.png,.jpg,.jpeg,.webp" disabled={busy} onFiles={async (f) => { const r = await run(() => api.upload(`/projects/${project.id}/plans`, f[0])); if (r) nav(`/plan/${r.id}`); }} label={busy ? 'Lade hoch …' : 'Plan hier ablegen (PDF, PNG, JPG)'} hint="Grundriss, Detail-, Terrassen-, Balkon-, Bewehrungs- oder Lageplan" /></div>
        </div>
      ) : <Empty>Bitte Projekt wählen.</Empty>}
      {pick && <ProjectPicker title="Plan analysieren – Projekt wählen" onClose={() => setPick(false)} onPick={(p) => { setProject(p); setPick(false); }} />}
    </div>
  );
}

function PlanWork({ id }) {
  const { data: plan, setData } = useLoad(`/plans/${id}`, [id]);
  const [tab, setTab] = useState('ki');
  const [calc, setCalc] = useState({ id: null, initial: {}, note: '' });
  const [checked, setChecked] = useState(false);
  const [hint, setHint] = useState('');
  const [answers, setAnswers] = useState({});
  const [run, busy] = useAction();
  const ai = useLoad('/ai/status');
  if (!plan) return <div className="page muted">Lade …</div>;
  const ms = plan.measurements || [];
  const items = plan.runs || [];
  const save = async (patch) => { setData({ ...plan, ...patch }); await run(() => api.patch(`/plans/${id}`, patch)); };
  const addMeasure = (m) => save({ measurements: [...ms, ...[].concat(m).map((x) => ({ id: uid(), confirmed: true, ...x }))] });
  const a = plan.analysis || {};
  const openSuggestion = (s) => {
    const initial = {};
    for (const inp of s.inputs || []) if (inp.value !== null) initial[inp.key] = inp.value;
    if (s.calculator === 'terrasse' && initial.rect_w && initial.rect_d) { initial.rects = [{ x: 0, y: 0, w: initial.rect_w, d: initial.rect_d }]; }
    delete initial.rect_w; delete initial.rect_d;
    setCalc({ id: s.calculator, initial, note: 'KI-Vorschlag', fromAi: true });
    setChecked(false);
    setTab('rechner');
  };
  const useMeasure = (m) => {
    const c = calc.id ? getCalculator(calc.id) : null;
    const key = c?.fields.find((f) => (m.kind === 'flaeche' && f.key === 'flaeche') || (m.kind === 'laenge' && ['laenge', 'wandL'].includes(f.key)))?.key;
    if (!c || !key) return alert('Bitte zuerst einen passenden Rechner wählen (z. B. Pflaster für Flächen, Bordsteine/Rohre/Rinnen für Längen).');
    setCalc({ ...calc, initial: { ...calc.initial, [key]: m.value }, note: `Maß „${m.label}“`, fromAi: calc.fromAi || m.source_type === 'ki' });
  };
  return (
    <div className="page">
      <div className="head">
        <div><div className="muted small"><Link to={`/projekte/${plan.project_id}?tab=plaene`}>{plan.project_name}</Link> › Plan</div><h1>📐 {plan.name}</h1></div>
        <div className="row">{plan.document_id && <a className="btn" href={docUrl(plan.document_id)} target="_blank" rel="noreferrer">Original öffnen</a>}</div>
      </div>
      <div className="grid" style={{ gridTemplateColumns: 'minmax(0, 1.3fr) minmax(380px, 1fr)', alignItems: 'start' }}>
        <PlanViewer docId={plan.document_id} mime={plan.mime} measurements={ms} onAdd={addMeasure} onCalibrate={(c) => save({ measurements: [...ms.filter((m) => m.kind !== 'kalibrierung'), { id: 'calib', kind: 'kalibrierung', label: 'Kalibrierung', ...c }] })} />
        <div className="stack">
          <Tabs tabs={[{ id: 'ki', label: '🤖 KI-Analyse' }, { id: 'masse', label: 'Maße', count: ms.filter((m) => m.kind !== 'kalibrierung').length }, { id: 'rechner', label: 'Berechnung' }, { id: 'liste', label: 'Materialliste', count: items.length }]} value={tab} onChange={setTab} />
          {tab === 'ki' && (
            <div className="card stack">
              {!ai.data?.enabled && <div className="callout warn">KI ist nicht konfiguriert. Maße mit dem Messwerkzeug (links) ermitteln oder unter „Maße“ manuell erfassen.</div>}
              <Field label="Hinweis für die KI (optional)"><input value={hint} onChange={(e) => setHint(e.target.value)} placeholder="z. B. „Terrasse mit Platten 60×60 auf Stelzlagern“" /></Field>
              <button className="btn primary" disabled={busy || !ai.data?.enabled} onClick={async () => { const r = await run(() => api.post(`/plans/${id}/analyze`, { hint }), 'Plan analysiert – Werte bitte prüfen'); if (r) setData(r); }}>{busy ? '⏳ Analysiere …' : 'Plan per KI analysieren'}</button>
              {a.plan_type && <>
                <div><b>{a.plan_type}</b>{a.scale ? ` · Maßstab ${a.scale}` : ' · Maßstab nicht angegeben'}</div>
                {a.questions?.length > 0 && <div className="callout warn"><b>Rückfragen (nicht eindeutig bestimmbar):</b>{a.questions.map((q, i) => <div key={q} className="row" style={{ marginTop: 6 }}><span className="grow">❓ {q}</span><input style={{ width: 110 }} placeholder="Wert" value={answers[i] || ''} onChange={(e) => setAnswers({ ...answers, [i]: e.target.value })} /><button className="btn sm" disabled={!answers[i]} onClick={() => { addMeasure({ label: q.slice(0, 80), kind: 'sonstiges', value: Number(String(answers[i]).replace(',', '.')) || null, unit: '', source: `Antwort Benutzer: ${answers[i]}`, source_type: 'manuell' }); setAnswers({ ...answers, [i]: '' }); }}>übernehmen</button></div>)}</div>}
                {a.components?.length > 0 && <div><div className="muted small">Erkannte Bauteile</div><ul className="small">{a.components.map((c) => <li key={c.name}><b>{c.name}</b>: {c.details}</li>)}</ul></div>}
                {a.suggestions?.length > 0 && <div><div className="muted small">Vorgeschlagene Berechnungen</div>{a.suggestions.map((s, i) => <div key={i} className="row between" style={{ marginTop: 6 }}><span>{getCalculator(s.calculator)?.name}: <span className="muted small">{s.reason}</span></span><button className="btn sm" onClick={() => openSuggestion(s)}>Rechner öffnen</button></div>)}</div>}
              </>}
            </div>
          )}
          {tab === 'masse' && <Measures ms={ms} save={save} useMeasure={useMeasure} addMeasure={addMeasure} />}
          {tab === 'rechner' && (
            <div className="card stack">
              <Field label="Rechner"><select value={calc.id || ''} onChange={(e) => setCalc({ id: e.target.value, initial: {}, note: '' })}><option value="">Bitte wählen …</option>{CALCULATORS.map((c) => <option key={c.id} value={c.id}>{c.icon} {c.name}</option>)}</select></Field>
              {calc.id && <>
                {calc.fromAi && <label className="row callout warn"><input type="checkbox" checked={checked} onChange={(e) => setChecked(e.target.checked)} /> Ich habe die übernommenen KI-Werte mit dem Plan abgeglichen.</label>}
                <CalcForm calcId={calc.id} initial={calc.initial} sourceNote={calc.note} onAdd={(res, values) => {
                  const cert = calc.fromAi && !checked ? 'prüfen' : null;
                  save({ runs: [...items, ...res.results.filter((r) => !r.info && r.qty > 0).map((r) => ({ ...r, certainty: cert || r.certainty, calc: getCalculator(calc.id).name, inputs: values }))] });
                  setTab('liste');
                }} />
              </>}
            </div>
          )}
          {tab === 'liste' && <MaterialList items={items} setItems={(it) => save({ runs: it })} projectId={plan.project_id} name={`Material aus Plan ${plan.name}`} source="plan" />}
        </div>
      </div>
    </div>
  );
}

function Measures({ ms, save, useMeasure, addMeasure }) {
  const [m, setM] = useState({ label: '', kind: 'flaeche', value: '' });
  const list = ms.filter((x) => x.kind !== 'kalibrierung');
  const upd = (mid, patch) => save({ measurements: ms.map((x) => (x.id === mid ? { ...x, ...patch } : x)) });
  return (
    <div className="card stack">
      {list.length ? <table className="t"><thead><tr><th>✓</th><th>Maß</th><th className="num">Wert</th><th>Quelle</th><th /></tr></thead><tbody>
        {list.map((x) => <tr key={x.id}>
          <td><input type="checkbox" checked={Boolean(x.confirmed)} onChange={(e) => upd(x.id, { confirmed: e.target.checked })} title="vom Benutzer bestätigt" /></td>
          <td>{x.label}<div className="small">{x.source_type === 'ki' ? <Badge color={x.confirmed ? 'green' : 'yellow'}>KI {Math.round((x.confidence || 0) * 100)} %</Badge> : <Badge color="blue">{x.source_type}</Badge>}</div></td>
          <td className="num"><input className="cell" value={x.value ?? ''} onChange={(e) => upd(x.id, { value: e.target.value === '' ? null : Number(e.target.value.replace(',', '.')), confirmed: true })} /> {x.unit}</td>
          <td className="small">{x.value === null ? <span style={{ color: 'var(--red)' }}>nicht eindeutig bestimmbar</span> : x.source}</td>
          <td className="nowrap"><button className="btn sm" disabled={x.value === null} onClick={() => useMeasure(x)}>→ Rechner</button> <button className="btn sm ghost" onClick={() => save({ measurements: ms.filter((y) => y.id !== x.id) })}>✕</button></td>
        </tr>)}
      </tbody></table> : <Empty>Noch keine Maße. Links im Plan messen oder unten eintragen.</Empty>}
      <div className="row">
        <input style={{ width: 160 }} placeholder="Bezeichnung" value={m.label} onChange={(e) => setM({ ...m, label: e.target.value })} />
        <select style={{ width: 120 }} value={m.kind} onChange={(e) => setM({ ...m, kind: e.target.value })}>{Object.keys(KIND_UNIT).map((k) => <option key={k} value={k}>{k}</option>)}</select>
        <input style={{ width: 90 }} placeholder="Wert" value={m.value} onChange={(e) => setM({ ...m, value: e.target.value })} />
        <button className="btn" disabled={!m.label || m.value === ''} onClick={() => { addMeasure({ label: m.label, kind: m.kind, value: Number(m.value.replace(',', '.')), unit: KIND_UNIT[m.kind], source: 'manuell eingegeben', source_type: 'manuell' }); setM({ label: '', kind: m.kind, value: '' }); }}>＋ Maß</button>
      </div>
    </div>
  );
}

/** Planansicht mit Messwerkzeug. Koordinaten in Bildpixeln; Kalibrierung über bekannte Strecke. */
function PlanViewer({ docId, mime, measurements, onAdd, onCalibrate }) {
  const canvasRef = useRef();
  const wrapRef = useRef();
  const [size, setSize] = useState(null);
  const [imgSrc, setImgSrc] = useState(null);
  const [pageNo, setPageNo] = useState(1);
  const [pages, setPages] = useState(1);
  const [zoom, setZoom] = useState(1);
  const [tool, setTool] = useState('none');
  const [pts, setPts] = useState([]);
  const [err, setErr] = useState(null);
  const calib = measurements.find((m) => m.kind === 'kalibrierung' && m.page === pageNo) || measurements.find((m) => m.kind === 'kalibrierung');
  const pxPerM = calib?.value || null;
  const isPdf = mime === 'application/pdf';
  useEffect(() => {
    let cancel = false;
    setErr(null);
    if (!docId) return undefined;
    if (!isPdf) { setImgSrc(docUrl(docId)); return undefined; }
    (async () => {
      try {
        const doc = await pdfjs.getDocument(docUrl(docId)).promise;
        if (cancel) return;
        setPages(doc.numPages);
        const page = await doc.getPage(pageNo);
        const vp = page.getViewport({ scale: 2 });
        const c = canvasRef.current;
        c.width = vp.width; c.height = vp.height;
        await page.render({ canvasContext: c.getContext('2d'), viewport: vp }).promise;
        if (!cancel) { setSize({ w: vp.width, h: vp.height }); setZoom(Math.min(1, (wrapRef.current?.clientWidth || 800) / vp.width)); }
      } catch (e) { if (!cancel) setErr(`Plan konnte nicht dargestellt werden: ${e.message}`); }
    })();
    return () => { cancel = true; };
  }, [docId, isPdf, pageNo]);
  const pos = (e) => { const r = e.currentTarget.getBoundingClientRect(); return { x: ((e.clientX - r.left) / r.width) * size.w, y: ((e.clientY - r.top) / r.height) * size.h }; };
  const finish = (points = pts) => {
    const cal = `Kalibrierung ${fmtNum(calib?.real || 0)} m`;
    if (tool === 'kalibrieren' && points.length === 2) {
      const real = Number(String(prompt('Wie lang ist diese Strecke in der Realität (Meter)?') || '').replace(',', '.'));
      if (real > 0) onCalibrate({ value: dist(points[0], points[1]) / real, real, unit: 'px/m', page: pageNo, source: `${fmtNum(dist(points[0], points[1]), 0)} px = ${real} m`, source_type: 'messung', geom: { type: 'linie', pts: points, page: pageNo } });
    } else if (tool === 'strecke' && points.length === 2) {
      const v = dist(points[0], points[1]) / pxPerM;
      const label = prompt(`Strecke: ${fmtNum(v)} m – Bezeichnung?`, 'Strecke');
      if (label !== null) onAdd({ label, kind: 'laenge', value: Math.round(v * 100) / 100, unit: 'm', source: `Messung im Plan (${cal})`, source_type: 'messung', geom: { type: 'linie', pts: points, page: pageNo } });
    } else if (tool === 'linie' && points.length >= 2) {
      const v = polyLen(points) / pxPerM;
      const label = prompt(`Länge: ${fmtNum(v)} m – Bezeichnung?`, 'Länge');
      if (label !== null) onAdd({ label, kind: 'laenge', value: Math.round(v * 100) / 100, unit: 'm', source: `Polylinie ${points.length} Punkte (${cal})`, source_type: 'messung', geom: { type: 'linie', pts: points, page: pageNo } });
    } else if (tool === 'flaeche' && points.length >= 3) {
      const v = polyArea(points) / (pxPerM * pxPerM);
      const per = (polyLen(points) + dist(points.at(-1), points[0])) / pxPerM;
      const label = prompt(`Fläche: ${fmtNum(v)} m² (Umfang ${fmtNum(per)} m) – Bezeichnung?`, 'Fläche');
      if (label !== null) {
        onAdd([
          { label, kind: 'flaeche', value: Math.round(v * 100) / 100, unit: 'm²', source: `Polygon ${points.length} Punkte (${cal})`, source_type: 'messung', geom: { type: 'flaeche', pts: points, page: pageNo } },
          { label: `${label} – Umfang`, kind: 'laenge', value: Math.round(per * 100) / 100, unit: 'm', source: `Umfang Polygon (${cal})`, source_type: 'messung' },
        ]);
      }
    } else if (tool === 'zaehlen' && points.length) {
      const label = prompt(`${points.length} gezählt – Bezeichnung?`, 'Anzahl');
      if (label !== null) onAdd({ label, kind: 'anzahl', value: points.length, unit: 'St', source: 'Gezählt im Plan', source_type: 'messung', geom: { type: 'punkte', pts: points, page: pageNo } });
    }
    setPts([]);
  };
  const click = (e) => {
    if (tool === 'none' || !size) return;
    const p = pos(e);
    const n = [...pts, p];
    if ((tool === 'kalibrieren' || tool === 'strecke') && n.length === 2) { setPts(n); setTimeout(() => finish(n), 30); return; }
    setPts(n);
  };
  const needCal = ['strecke', 'linie', 'flaeche'].includes(tool) && !pxPerM;
  const shapes = useMemo(() => measurements.filter((m) => m.geom && (m.geom.page || 1) === pageNo), [measurements, pageNo]);
  const sw = size ? Math.max(2, size.w / 600) : 2;
  return (
    <div className="card stack">
      <div className="row">
        <Seg value={tool} onChange={(t) => { setTool(t); setPts([]); }} options={[{ value: 'none', label: '✋' }, { value: 'kalibrieren', label: '📏 Kalibrieren' }, { value: 'strecke', label: 'Strecke' }, { value: 'linie', label: 'Polylinie' }, { value: 'flaeche', label: 'Fläche' }, { value: 'zaehlen', label: 'Zählen' }]} />
        {['linie', 'flaeche', 'zaehlen'].includes(tool) && <button className="btn sm primary" disabled={!pts.length} onClick={() => finish()}>✓ Fertig ({pts.length})</button>}
        {pts.length > 0 && <button className="btn sm" onClick={() => setPts(pts.slice(0, -1))}>↶</button>}
        <span className="grow" />
        <button className="btn sm" onClick={() => setZoom(zoom / 1.25)}>−</button><span className="small">{Math.round(zoom * 100)} %</span><button className="btn sm" onClick={() => setZoom(zoom * 1.25)}>＋</button>
        {pages > 1 && <select style={{ width: 110 }} value={pageNo} onChange={(e) => setPageNo(Number(e.target.value))}>{Array.from({ length: pages }, (_, i) => <option key={i} value={i + 1}>Seite {i + 1}</option>)}</select>}
      </div>
      <div className="small">{pxPerM ? <Badge color="green">kalibriert: {calib.source}</Badge> : <Badge color="yellow">nicht kalibriert – zuerst bekannte Strecke (Bemaßung) mit „Kalibrieren“ anklicken</Badge>}{needCal && <span style={{ color: 'var(--red)' }}> · Messen erst nach Kalibrierung möglich</span>}</div>
      {err && <div className="callout err">{err}</div>}
      <div className="planview" ref={wrapRef}>
        <div style={{ position: 'relative', width: size ? size.w * zoom : 'auto' }}>
          {isPdf ? <canvas ref={canvasRef} style={{ width: size ? size.w * zoom : undefined }} /> : imgSrc && <img src={imgSrc} alt="Plan" style={{ width: size ? size.w * zoom : undefined }} onLoad={(e) => { const w = e.currentTarget.naturalWidth, h = e.currentTarget.naturalHeight; setSize({ w, h }); setZoom(Math.min(1, (wrapRef.current?.clientWidth || 800) / w)); }} />}
          {size && (
            <svg viewBox={`0 0 ${size.w} ${size.h}`} style={{ width: size.w * zoom, height: size.h * zoom, cursor: tool === 'none' || needCal ? 'default' : 'crosshair' }} onClick={needCal ? undefined : click}>
              {shapes.map((m) => m.geom.type === 'flaeche'
                ? <polygon key={m.id} points={m.geom.pts.map((p) => `${p.x},${p.y}`).join(' ')} fill="rgba(232,89,12,.15)" stroke="#e8590c" strokeWidth={sw} />
                : m.geom.type === 'punkte' ? m.geom.pts.map((p, i) => <circle key={`${m.id}${i}`} cx={p.x} cy={p.y} r={sw * 3} fill="#1d5bd8" />)
                  : <polyline key={m.id} points={m.geom.pts.map((p) => `${p.x},${p.y}`).join(' ')} fill="none" stroke={m.kind === 'kalibrierung' ? '#12805c' : '#1d5bd8'} strokeWidth={sw} strokeDasharray={m.kind === 'kalibrierung' ? `${sw * 4} ${sw * 2}` : undefined} />)}
              {pts.length > 0 && (tool === 'flaeche' ? <polygon points={pts.map((p) => `${p.x},${p.y}`).join(' ')} fill="rgba(196,50,10,.2)" stroke="#c4320a" strokeWidth={sw} /> : tool !== 'zaehlen' && <polyline points={pts.map((p) => `${p.x},${p.y}`).join(' ')} fill="none" stroke="#c4320a" strokeWidth={sw} />)}
              {pts.map((p, i) => <circle key={i} cx={p.x} cy={p.y} r={sw * 2.5} fill="#c4320a" />)}
            </svg>
          )}
        </div>
      </div>
    </div>
  );
}
