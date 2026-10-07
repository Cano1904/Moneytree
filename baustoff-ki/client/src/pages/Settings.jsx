import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, docUrl, download } from '../api.js';
import { Badge, Dropzone, Field, useAction, useLoad } from '../components/ui.jsx';
import { useApp } from '../App.jsx';

export default function Settings() {
  const { data, reload } = useLoad('/settings');
  const users = useLoad('/users');
  const [s, setS] = useState(null);
  const [newUser, setNewUser] = useState('');
  const [run, busy] = useAction();
  const nav = useNavigate();
  const { refreshCounts } = useApp();
  useEffect(() => { if (data) setS(data); }, [data]);
  if (!s) return <div className="page muted">Lade …</div>;
  const set = (k) => (e) => setS({ ...s, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value });
  const save = async () => {
    const { ai, ...rest } = s;
    for (const k of ['default_markup', 'min_margin', 'vat_pct', 'followup_days', 'offer_validity_days']) rest[k] = Number(String(rest[k]).replace(',', '.'));
    const r = await run(() => api.put('/settings', rest), 'Einstellungen gespeichert');
    if (r) { setS(r); reload(); }
  };
  const F = ({ k, label, type = 'text' }) => <Field label={label}><input type={type} value={s[k] ?? ''} onChange={set(k)} /></Field>;
  return (
    <div className="page">
      <div className="head"><h1>Einstellungen</h1><button className="btn primary" disabled={busy} onClick={save}>Speichern</button></div>
      <div className="grid g2">
        <div className="card stack">
          <h2>Firma (Deckblatt & Mails)</h2>
          {F({ k: 'company_name', label: 'Firmenname' })}
          <Field label="Adresse"><textarea value={s.company_address || ''} onChange={set('company_address')} /></Field>
          <div className="grid g3">{F({ k: 'company_phone', label: 'Telefon' })}{F({ k: 'company_email', label: 'E-Mail' })}{F({ k: 'company_web', label: 'Web' })}</div>
          <Field label="Einleitung Angebots-LV"><textarea value={s.offer_intro || ''} onChange={set('offer_intro')} /></Field>
          <Field label="Angebotsbedingungen (Fußzeile Deckblatt)"><textarea value={s.offer_terms || ''} onChange={set('offer_terms')} /></Field>
          <Field label="Signatur Angebotsmail"><textarea value={s.signature || ''} onChange={set('signature')} /></Field>
        </div>
        <div className="stack">
          <MobileSetup />
          <div className="card stack">
            <h2>Kalkulation & Ablauf</h2>
            <div className="grid g3">
              {F({ k: 'default_markup', label: 'Standard-Aufschlag %' })}{F({ k: 'min_margin', label: 'Mindestmarge %' })}{F({ k: 'vat_pct', label: 'MwSt. %' })}
              {F({ k: 'followup_days', label: 'Nachfassen nach Tagen' })}{F({ k: 'offer_validity_days', label: 'Angebot gültig (Tage)' })}
              <Field label="Standardmodus"><select value={s.default_mode} onChange={set('default_mode')}><option value="galabau">Tief- & GaLa-Bau</option><option value="hochbau">Hochbau</option></select></Field>
            </div>
          </div>
          <div className="card stack">
            <h2>Standard-Deckblatt</h2>
            <div className="muted small">Wird jedem Angebot vorangestellt (PDF oder Bild). Projektbezogene Deckblätter gehen in der Projektakte. Ohne Datei wird ein Deckblatt aus den Firmendaten erzeugt.</div>
            {s.cover_document_id ? <div className="row"><Badge color="green">hinterlegt</Badge><a href={docUrl(s.cover_document_id)} target="_blank" rel="noreferrer">ansehen</a><button className="btn sm" onClick={async () => { await run(() => api.put('/settings', { cover_document_id: null })); reload(); }}>entfernen</button></div> : null}
            <Dropzone accept=".pdf,.png,.jpg,.jpeg" onFiles={async (f) => { await run(() => api.upload('/settings/cover', f[0]), 'Deckblatt gespeichert'); reload(); }} label="Deckblatt hochladen" />
          </div>
          <div className="card stack">
            <h2>KI {s.ai?.enabled ? <Badge color="green">aktiv</Badge> : <Badge color="yellow">aus</Badge>}</h2>
            <div className="muted small">Mit Claude-API-Key werden LVs, Scans, Lieferantenangebote und Pläne per KI gelesen. Ohne Key arbeitet die App mit Regel-Erkennung und manuellen Schritten. KI-Ergebnisse werden immer markiert und vom Benutzer bestätigt.</div>
            {s.ai?.source === 'env' ? <div className="callout">API-Key über Umgebungsvariable ANTHROPIC_API_KEY gesetzt.</div> : F({ k: 'ai_api_key', label: 'Anthropic API-Key', type: 'password' })}
            <div className="grid g2">
              <Field label="Modell"><input value={s.ai_model || ''} onChange={set('ai_model')} /></Field>
              <Field label="Gründlichkeit"><select value={s.ai_effort} onChange={set('ai_effort')}><option value="low">schnell</option><option value="medium">ausgewogen</option><option value="high">gründlich</option></select></Field>
            </div>
            <label className="row"><input type="checkbox" checked={s.ai_enabled !== false} onChange={set('ai_enabled')} /> KI verwenden</label>
          </div>
          <div className="card stack">
            <h2>Benutzer</h2>
            <div className="list">{(users.data || []).map((u) => <div key={u.id} className="row between"><span>{u.name} <span className="muted small">{u.role}</span></span><button className="btn sm" onClick={async () => { await run(() => api.patch(`/users/${u.id}`, { active: u.active ? 0 : 1 })); users.reload(); }}>{u.active ? 'deaktivieren' : 'aktivieren'}</button></div>)}</div>
            <div className="row"><input style={{ maxWidth: 240 }} value={newUser} onChange={(e) => setNewUser(e.target.value)} placeholder="Name" /><button className="btn" disabled={!newUser.trim()} onClick={async () => { await run(() => api.post('/users', { name: newUser })); setNewUser(''); users.reload(); }}>＋ Benutzer</button></div>
          </div>
          <div className="card stack">
            <h2>Daten</h2>
            <div className="row">
              <button className="btn" onClick={() => run(() => download('/backup', 'backup.sqlite'), 'Backup heruntergeladen')}>⬇ Datenbank-Backup</button>
              <button className="btn" onClick={async () => { const r = await run(() => api.post('/demo'), 'Demo-Daten geladen'); if (r) { refreshCounts?.(); nav(`/projekte/${r.project_id}?tab=lv`); } }}>Demo-Daten laden</button>
            </div>
            <div className="muted small">Demo: 6 Lieferanten, Produkte, Preishistorie, Wissen und ein Beispielprojekt mit analysiertem LV (alle als „(Demo)“ gekennzeichnet).</div>
          </div>
        </div>
      </div>
    </div>
  );
}

function MobileSetup() {
  const { data } = useLoad('/mobile-info');
  return (
    <div className="card stack">
      <h2>📱 iPhone-App einrichten</h2>
      <div className="row" style={{ alignItems: 'flex-start', gap: 16 }}>
        {data?.qr && <div style={{ background: '#fff', padding: 6, borderRadius: 8, width: 150 }} dangerouslySetInnerHTML={{ __html: data.qr }} />}
        <ol className="small grow" style={{ margin: 0, paddingLeft: 18 }}>
          <li>iPhone im selben WLAN wie dieser Rechner.</li>
          <li>QR-Code mit der Kamera scannen oder in Safari öffnen:<br />{(data?.urls || []).map((u) => <b key={u} style={{ display: 'block' }}>{u}</b>)}{data && !data.urls.length && <span className="muted">keine Netzwerkadresse gefunden</span>}</li>
          <li>In Safari <b>Teilen</b> (□↑) → <b>„Zum Home-Bildschirm“</b>.</li>
          <li>Baustoff-KI startet dann wie eine App – mit Kamera-Upload für LVs, Lieferantenangebote und Pläne.</li>
        </ol>
      </div>
      {data && !data.secure && <div className="muted small">Tipp: Für unterwegs (außerhalb des WLANs) den Server über HTTPS erreichbar machen (z. B. Reverse-Proxy oder VPN) und <code>APP_PASSWORD</code> setzen.</div>}
    </div>
  );
}
