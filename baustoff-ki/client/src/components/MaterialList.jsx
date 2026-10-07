// Gesammelte Materialliste (aus Rechnern/Plan) → als LV ins Projekt übernehmen
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api.js';
import { Badge, Empty, NumInput, useAction } from './ui.jsx';
import ProjectPicker from './ProjectPicker.jsx';
import { certaintyColor } from './CalcForm.jsx';

export default function MaterialList({ items, setItems, projectId, name = 'Materialermittlung', source = 'rechner' }) {
  const [pick, setPick] = useState(false);
  const [run, busy] = useAction();
  const nav = useNavigate();
  const toProject = async (pid) => {
    const r = await run(() => api.post(`/projects/${pid}/material-lv`, { name, source, items }), 'Als LV ins Projekt übernommen');
    if (r) nav(`/projekte/${pid}?tab=lv&lv=${r.lvId}`);
  };
  return (
    <div className="card">
      <div className="row between"><h2>Materialliste ({items.length})</h2>
        <div className="row">{items.length > 0 && <button className="btn ghost" onClick={() => setItems([])}>Leeren</button>}<button className="btn primary" disabled={!items.length || busy} onClick={() => (projectId ? toProject(projectId) : setPick(true))}>→ Als LV ins Projekt übernehmen</button></div></div>
      {items.length ? <table className="t"><thead><tr><th>Material</th><th className="num">Menge</th><th>Einheit</th><th>Grundlage</th><th>Sicherheit</th><th /></tr></thead><tbody>
        {items.map((it, i) => <tr key={i}><td>{it.material}<div className="muted small">{it.calc}</div></td><td className="num"><NumInput value={it.qty} onCommit={(v) => setItems(items.map((x, j) => (j === i ? { ...x, qty: v, certainty: 'manuell geändert' } : x)))} /></td><td>{it.unit}</td><td className="small">{it.basis}</td><td><Badge color={certaintyColor(it.certainty)}>{it.certainty}</Badge></td><td><button className="btn sm ghost" onClick={() => setItems(items.filter((_, j) => j !== i))}>✕</button></td></tr>)}
      </tbody></table> : <Empty>Ergebnisse aus Rechnern mit „＋ Zur Materialliste“ sammeln.</Empty>}
      {pick && <ProjectPicker title="In welches Projekt übernehmen?" onClose={() => setPick(false)} onPick={(p) => { setPick(false); toProject(p.id); }} />}
    </div>
  );
}
