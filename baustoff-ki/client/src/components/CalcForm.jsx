// Generisches Rechner-Formular: Eingaben → deterministische Berechnung mit Formeln, Annahmen und Sicherheit
import { useEffect, useMemo, useState } from 'react';
import { Badge, Field } from './ui.jsx';
import { getCalculator, runCalculator } from '../../../shared/calculators.js';
import { fmtNum, fmtQty } from '../../../shared/format.js';

export const certaintyColor = (c) => (c === 'berechnet' ? 'green' : c === 'prüfen' ? 'red' : 'yellow');

export default function CalcForm({ calcId, initial = {}, sourceNote, onAdd }) {
  const calc = getCalculator(calcId);
  const defaults = useMemo(() => Object.fromEntries(calc.fields.map((f) => [f.key, f.default])), [calc]);
  const [v, setV] = useState({ ...defaults, ...initial });
  useEffect(() => { setV({ ...defaults, ...initial }); }, [calcId, JSON.stringify(initial)]); // eslint-disable-line react-hooks/exhaustive-deps
  const res = useMemo(() => runCalculator(calcId, v), [calcId, v]);
  const set = (k, val) => setV((x) => ({ ...x, [k]: val }));
  return (
    <div className="stack">
      <div className="grid g3">
        {calc.fields.map((f) => {
          if (f.type === 'rects') return <RectEditor key={f.key} value={v[f.key] || []} onChange={(r) => set(f.key, r)} />;
          if (f.type === 'select') return <Field key={f.key} label={f.label}><select value={v[f.key] ?? ''} onChange={(e) => set(f.key, e.target.value)}>{f.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></Field>;
          if (f.type === 'checkbox') return <label key={f.key} className="row" style={{ alignSelf: 'end' }}><input type="checkbox" checked={Boolean(v[f.key])} onChange={(e) => set(f.key, e.target.checked)} /> {f.label}</label>;
          if (f.type === 'text') return <Field key={f.key} label={f.label}><input value={v[f.key] ?? ''} onChange={(e) => set(f.key, e.target.value)} /></Field>;
          return <Field key={f.key} label={`${f.label}${f.unit ? ` [${f.unit}]` : ''}`}><input inputMode="decimal" value={v[f.key] ?? ''} title={f.help} placeholder={f.help || ''} onChange={(e) => set(f.key, e.target.value.replace(',', '.'))} style={initial[f.key] !== undefined ? { borderColor: 'var(--blue)' } : undefined} /></Field>;
        })}
      </div>
      {Object.keys(initial).length > 0 && <div className="muted small">Blau umrandet: aus Plan/Messung übernommen{sourceNote ? ` (${sourceNote})` : ''} – bitte prüfen.</div>}
      {res.error ? <div className="callout err">⚠ {res.error}</div> : (
        <>
          {res.layout && <TerracePreview layout={res.layout} />}
          <table className="t"><thead><tr><th>Material</th><th className="num">Menge</th><th>Einheit</th><th>Grundlage</th><th>Sicherheit</th></tr></thead><tbody>
            {res.results.map((r) => <tr key={r.material} style={r.info ? { opacity: 0.65 } : undefined}><td>{r.info ? <>{r.material} <span className="muted small">(Kontrollwert)</span></> : <b>{r.material}</b>}</td><td className="num">{fmtQty(r.qty)}</td><td>{r.unit}</td><td className="small">{r.basis}</td><td><Badge color={certaintyColor(r.certainty)}>{r.certainty}</Badge></td></tr>)}
          </tbody></table>
          {res.warnings.map((w) => <div key={w} className="flag warn">⚠ {w}</div>)}
          <details open><summary>Rechenweg & Annahmen</summary>
            <ul className="small">{res.steps.map((s) => <li key={s}>{s}</li>)}</ul>
            {res.assumptions.length > 0 && <><div className="muted small">Annahmen:</div><ul className="small">{res.assumptions.map((s) => <li key={s}>{s}</li>)}</ul></>}
          </details>
          {onAdd && <div className="row" style={{ justifyContent: 'flex-end' }}><button className="btn primary" onClick={() => onAdd(res, v)}>＋ Zur Materialliste</button></div>}
        </>
      )}
    </div>
  );
}

function RectEditor({ value, onChange }) {
  const upd = (i, k, val) => onChange(value.map((r, j) => (j === i ? { ...r, [k]: val.replace(',', '.') } : r)));
  return (
    <div style={{ gridColumn: '1 / -1' }}>
      <div className="muted small">Fläche aus Rechtecken (m) – L-/U-Formen durch mehrere Rechtecke mit Versatz x/y</div>
      {value.map((r, i) => (
        <div key={i} className="row" style={{ marginTop: 4 }}>
          <span className="small muted">#{i + 1}</span>
          {[['x', 'x'], ['y', 'y'], ['w', 'Breite'], ['d', 'Tiefe']].map(([k, l]) => <label key={k} className="row small">{l}<input style={{ width: 80 }} inputMode="decimal" value={r[k]} onChange={(e) => upd(i, k, e.target.value)} /></label>)}
          {value.length > 1 && <button className="btn sm ghost" onClick={() => onChange(value.filter((_, j) => j !== i))}>✕</button>}
        </div>
      ))}
      <button className="btn sm" style={{ marginTop: 6 }} onClick={() => { const last = value.at(-1) || { x: 0, y: 0, w: 0, d: 0 }; onChange([...value, { x: 0, y: Number(last.y) + Number(last.d), w: 2, d: 2 }]); }}>＋ Rechteck</button>
    </div>
  );
}

/** 2D-Vorschau: Platten (voll/Zuschnitt) und Stelzlager (innen/Rand/Ecke). */
export function TerracePreview({ layout }) {
  const { bbox, cells, nodes, rects } = layout;
  const W = bbox.x1 - bbox.x0, H = bbox.y1 - bbox.y0;
  const pad = 0.3;
  return (
    <div className="card" style={{ padding: 8 }}>
      <svg viewBox={`${bbox.x0 - pad} ${bbox.y0 - pad} ${W + 2 * pad} ${H + 2 * pad}`} style={{ width: '100%', maxHeight: 360, background: 'var(--panel2)' }}>
        {cells.map((c) => <rect key={`${c.i},${c.j}`} x={c.x} y={c.y} width={c.w} height={c.d} fill={c.type === 'voll' ? '#d9c8a9' : '#f2b880'} stroke="#8a7a5c" strokeWidth={0.01} />)}
        {rects.map((r, i) => <rect key={i} x={r.x0} y={r.y0} width={r.x1 - r.x0} height={r.y1 - r.y0} fill="none" stroke="#c4320a" strokeWidth={0.03} />)}
        {nodes.map((n, i) => <circle key={i} cx={n.x} cy={n.y} r={0.045} fill={n.type === 'innen' ? '#1d5bd8' : n.type === 'rand' ? '#12805c' : '#c4320a'} />)}
      </svg>
      <div className="row small muted"><span>■ <span style={{ color: '#b99f6e' }}>volle Platte</span></span><span>■ <span style={{ color: '#f2b880' }}>Zuschnitt</span></span><span style={{ color: '#1d5bd8' }}>● innen</span><span style={{ color: '#12805c' }}>● Rand</span><span style={{ color: '#c4320a' }}>● Ecke</span><span>Maße {fmtNum(W)} × {fmtNum(H)} m</span></div>
    </div>
  );
}
