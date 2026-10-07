import { createContext, useContext, useEffect, useState } from 'react';
import { NavLink, Route, Routes, useNavigate } from 'react-router-dom';
import { onSaveState } from './api.js';
import { Seg, useLoad } from './components/ui.jsx';
import { MODES } from '../../shared/groups.js';
import Dashboard from './pages/Dashboard.jsx';
import Projects from './pages/Projects.jsx';
import Project from './pages/Project.jsx';
import Suppliers from './pages/Suppliers.jsx';
import Requests from './pages/Requests.jsx';
import Calculators from './pages/Calculators.jsx';
import PlanPage from './pages/PlanPage.jsx';
import Knowledge from './pages/Knowledge.jsx';
import Prices from './pages/Prices.jsx';
import FollowUps from './pages/FollowUps.jsx';
import Search from './pages/Search.jsx';
import Settings from './pages/Settings.jsx';

const AppCtx = createContext({});
export const useApp = () => useContext(AppCtx);
const ls = { get: (k, d) => { try { return localStorage.getItem(k) ?? d; } catch { return d; } }, set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* */ } } };

function SaveIndicator() {
  const [s, setS] = useState({ status: 'idle' });
  useEffect(() => onSaveState(setS), []);
  const txt = { idle: '', saving: '● speichert …', saved: `✓ gespeichert ${s.at ? s.at.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' }) : ''}`, error: '⚠ nicht gespeichert' }[s.status];
  return <div className="savestate" style={{ color: s.status === 'error' ? 'var(--red)' : undefined }}>{txt}</div>;
}

export default function App() {
  const [mode, setModeState] = useState(ls.get('mode', 'galabau'));
  const [theme, setTheme] = useState(ls.get('theme', 'light'));
  const [userId, setUserId] = useState(ls.get('userId', ''));
  const [q, setQ] = useState('');
  const nav = useNavigate();
  const users = useLoad('/users');
  const dash = useLoad('/dashboard');
  useEffect(() => { document.documentElement.dataset.theme = theme; ls.set('theme', theme); }, [theme]);
  useEffect(() => { if (!userId && users.data?.length) { setUserId(String(users.data[0].id)); ls.set('userId', String(users.data[0].id)); } }, [users.data, userId]);
  const setMode = (m) => { setModeState(m); ls.set('mode', m); };
  const counts = dash.data ? { req: dash.data.openRequests.filter((r) => ['angefragt', 'ausstehend', 'rueckfrage'].includes(r.status)).length, fu: dash.data.followups.today.length + dash.data.followups.overdue.length } : {};
  const ctx = { mode, setMode, userId, users: users.data || [], refreshCounts: dash.reload };
  const link = (to, ico, label, cnt) => <NavLink to={to} end={to === '/'}>{ico}<span>{label}</span>{cnt ? <b className="cnt">{cnt}</b> : null}</NavLink>;
  return (
    <AppCtx.Provider value={ctx}>
      <div className="app">
        <nav className="side">
          <div className="logo"><i>▲</i><b>Baustoff-KI</b></div>
          {link('/', '🏠', 'Dashboard')}
          {link('/projekte', '📁', 'Projekte & Akten')}
          {link('/anfragen', '✉️', 'Lieferantenanfragen', counts.req)}
          {link('/wiedervorlagen', '⏰', 'Wiedervorlagen', counts.fu)}
          <div className="sec">Werkzeuge</div>
          {link('/plan', '📐', 'Plan analysieren')}
          {link('/rechner', '🧮', 'Material-Rechner')}
          <div className="sec">Stammdaten</div>
          {link('/lieferanten', '🚚', 'Lieferanten')}
          {link('/wissen', '📚', 'Produkte & Wissen')}
          {link('/preise', '📈', 'Preishistorie')}
          {link('/einstellungen', '⚙️', 'Einstellungen')}
        </nav>
        <div className="main">
          <header className="top">
            <form className="search" onSubmit={(e) => { e.preventDefault(); if (q.trim()) nav(`/suche?q=${encodeURIComponent(q.trim())}`); }}>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="🔍 Suche: „alle LVs von Kunde Müller“, „Projekte mit Kalksandstein“, „Objektpreise für Pflaster“ …" />
            </form>
            <Seg options={Object.entries(MODES).map(([k, v]) => ({ value: k, label: v.short }))} value={mode} onChange={setMode} />
            <SaveIndicator />
            <select style={{ width: 150 }} value={userId} onChange={(e) => { setUserId(e.target.value); ls.set('userId', e.target.value); }} title="Benutzer">
              {(users.data || []).filter((u) => u.active).map((u) => <option key={u.id} value={u.id}>👤 {u.name}</option>)}
            </select>
            <button className="btn ghost" title="Hell/Dunkel" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}>{theme === 'dark' ? '☀️' : '🌙'}</button>
          </header>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/projekte" element={<Projects />} />
            <Route path="/projekte/:id" element={<Project />} />
            <Route path="/anfragen" element={<Requests />} />
            <Route path="/wiedervorlagen" element={<FollowUps />} />
            <Route path="/plan" element={<PlanPage />} />
            <Route path="/plan/:id" element={<PlanPage />} />
            <Route path="/rechner" element={<Calculators />} />
            <Route path="/lieferanten" element={<Suppliers />} />
            <Route path="/wissen" element={<Knowledge />} />
            <Route path="/preise" element={<Prices />} />
            <Route path="/suche" element={<Search />} />
            <Route path="/einstellungen" element={<Settings />} />
            <Route path="*" element={<div className="page"><h1>Seite nicht gefunden</h1></div>} />
          </Routes>
        </div>
      </div>
    </AppCtx.Provider>
  );
}

