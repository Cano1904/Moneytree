import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import CalcForm from '../components/CalcForm.jsx';
import MaterialList from '../components/MaterialList.jsx';
import { CALCULATORS } from '../../../shared/calculators.js';
import { useApp } from '../App.jsx';

const LS_KEY = 'materialList';
const load = () => { try { return JSON.parse(localStorage.getItem(LS_KEY) || '[]'); } catch { return []; } };

export default function Calculators() {
  const { mode } = useApp();
  const [sp] = useSearchParams();
  const list = [...CALCULATORS].sort((a, b) => Number(b.modes[0] === mode) - Number(a.modes[0] === mode));
  const [calcId, setCalcId] = useState(list[0].id);
  const [items, setItems] = useState(load);
  useEffect(() => { try { localStorage.setItem(LS_KEY, JSON.stringify(items)); } catch { /* */ } }, [items]);
  const add = (res) => setItems([...items, ...res.results.filter((r) => !r.info && r.qty > 0).map((r) => ({ ...r, calc: CALCULATORS.find((c) => c.id === calcId).name }))]);
  return (
    <div className="page">
      <div className="head"><div><h1>Material-Rechner</h1><div className="muted">Einfache Eingaben, nachvollziehbare Formeln. Fehlende Angaben werden nicht geraten.</div></div></div>
      <div className="grid calc-layout">
        <select className="show-mobile" value={calcId} onChange={(e) => setCalcId(e.target.value)}>{list.map((c) => <option key={c.id} value={c.id}>{c.icon} {c.name}</option>)}</select>
        <div className="card hide-mobile" style={{ padding: 8 }}>
          {list.map((c) => <button key={c.id} className={`btn ${c.id === calcId ? 'primary' : 'ghost'}`} style={{ width: '100%', justifyContent: 'flex-start', marginBottom: 2, whiteSpace: 'normal', textAlign: 'left' }} onClick={() => setCalcId(c.id)}>{c.icon} {c.name}{!c.modes.includes(mode) && <span className="small" style={{ marginLeft: 'auto', opacity: 0.7 }}>{c.modes[0] === 'hochbau' ? 'HB' : 'GaLa'}</span>}</button>)}
        </div>
        <div className="stack">
          <div className="card"><h2>{CALCULATORS.find((c) => c.id === calcId).name}</h2><CalcForm calcId={calcId} onAdd={add} /></div>
          <MaterialList items={items} setItems={setItems} projectId={sp.get('project')} />
        </div>
      </div>
    </div>
  );
}
