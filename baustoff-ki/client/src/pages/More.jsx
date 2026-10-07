// Handy: "Mehr"-Menü mit allen weiteren Bereichen, Modus, Benutzer und Darstellung
import { Link } from 'react-router-dom';
import { Field, Seg } from '../components/ui.jsx';
import { useApp } from '../App.jsx';
import { MODES } from '../../../shared/groups.js';

const LINKS = [
  ['/plan', '📐', 'Plan analysieren', 'Foto/PDF vom Plan, messen, Material'],
  ['/rechner', '🧮', 'Material-Rechner', 'Pflaster, Terrasse, Schüttgut, Beton …'],
  ['/lieferanten', '🚚', 'Lieferanten', 'Anrufen, Kontakte, Preise'],
  ['/wissen', '📚', 'Produkte & Wissen', 'Alternativen, Erfahrungswerte'],
  ['/preise', '📈', 'Preishistorie', 'Letzte EKs & Objektpreise'],
  ['/einstellungen', '⚙️', 'Einstellungen', 'Firma, Kalkulation, KI, Benutzer'],
];

export const isStandalone = () => window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;

export default function More() {
  const { mode, setMode, userId, setUserId, users, theme, setTheme } = useApp();
  return (
    <div className="page stack">
      <h1>Mehr</h1>
      <div className="card" style={{ padding: 0 }}>
        {LINKS.map(([to, ico, t, s]) => (
          <Link key={to} to={to} className="menuitem"><span className="ico">{ico}</span><span className="grow"><b>{t}</b><div className="muted small">{s}</div></span><span className="muted">›</span></Link>
        ))}
      </div>
      <div className="card stack">
        <Field label="Arbeitsmodus"><Seg options={Object.entries(MODES).map(([k, v]) => ({ value: k, label: v.label }))} value={mode} onChange={setMode} /></Field>
        <Field label="Benutzer"><select value={userId} onChange={(e) => setUserId(e.target.value)}>{users.filter((u) => u.active).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>
        <Field label="Darstellung"><Seg options={[{ value: 'light', label: '☀️ Hell' }, { value: 'dark', label: '🌙 Dunkel' }]} value={theme} onChange={setTheme} /></Field>
      </div>
      {!isStandalone() && (
        <div className="callout">
          <b>📱 Als App auf dem iPhone installieren</b>
          <ol className="small" style={{ margin: '6px 0 0', paddingLeft: 18 }}>
            <li>Diese Seite in <b>Safari</b> öffnen</li>
            <li>Unten auf <b>Teilen</b> (□↑) tippen</li>
            <li><b>„Zum Home-Bildschirm“</b> wählen → „Hinzufügen“</li>
          </ol>
          <div className="small muted" style={{ marginTop: 6 }}>Danach startet Baustoff-KI wie eine App im Vollbild.</div>
        </div>
      )}
    </div>
  );
}
