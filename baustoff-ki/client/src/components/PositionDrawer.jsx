// Positionsdetails: Prüfhinweise, Texte, Produkt, Preis/Kalkulation, Varianten, Preishistorie, Anfragen
import { useEffect, useState } from 'react';
import { api } from '../api.js';
import { Badge, Field, NumInput, TextInput, useAction, useLoad } from './ui.jsx';
import { calcPosition, POS_TYPES } from '../../../shared/pricing.js';
import { fmtDate, fmtEUR, fmtNum, fmtPct, KNOWN_UNITS } from '../../../shared/format.js';
import { GROUP_NAMES, groupsForMode } from '../../../shared/groups.js';
import { requestStatusLabel } from '../../../shared/status.js';

export default function PositionDrawer({ id, mode, suppliers, onClose, onChanged, onPrev, onNext, onRequest }) {
  const { data, reload } = useLoad(`/positions/${id}/detail`, [id]);
  const [run] = useAction();
  const lv = useLoad(data ? `/lvs/${data.position.lv_id}` : null, [data?.position.lv_id]);
  const products = useLoad('/products');
  useEffect(() => {
    const h = (e) => {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)) return;
      if (e.key === 'Escape') onClose();
      if ((e.key === 'ArrowDown' || e.key === 'j') && onNext) onNext();
      if ((e.key === 'ArrowUp' || e.key === 'k') && onPrev) onPrev();
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onClose, onNext, onPrev]);
  if (!data) return <div className="drawer"><div className="db muted">Lade …</div></div>;
  const p = data.position;
  const minMargin = lv.data?.minMargin ?? null;
  const c = calcPosition(p, minMargin);
  const save = async (body) => { const r = await run(() => api.patch(`/positions/${p.id}`, body)); if (r) { await reload(); onChanged?.(); } };
  const lastPrice = data.history[0];
  const groups = [...new Set([...groupsForMode(mode), ...GROUP_NAMES])];
  return (
    <div className="drawer">
      <div className="dh">
        <div className="grow">
          <div className="muted small">Pos. {p.oz || '–'} {p.title_path ? `· ${p.title_path}` : ''}</div>
          <h2 style={{ margin: 0 }}>{p.short_text}</h2>
        </div>
        <button className="btn sm" disabled={!onPrev} onClick={onPrev} title="Vorherige (↑)">↑</button>
        <button className="btn sm" disabled={!onNext} onClick={onNext} title="Nächste (↓)">↓</button>
        <button className="btn sm ghost" onClick={onClose}>✕</button>
      </div>
      <div className="db stack">
        {(p.flags || []).length > 0 && (
          <div>
            <h3>Prüfhinweise</h3>
            {p.flags.map((f) => (
              <div key={f.key} className={`flag ${f.resolved ? 'done' : f.severity}`}>
                <span>{f.severity === 'info' ? 'ℹ' : '⚠'}</span>
                <div className="grow">{f.msg}{f.suggestion && <div className="muted small">Vorschlag: {f.suggestion}</div>}</div>
                <button className="btn sm" onClick={async () => { await run(() => api.post(`/positions/${p.id}/flags/${f.key}`, { resolved: !f.resolved })); reload(); onChanged?.(); }}>{f.resolved ? 'wieder öffnen' : '✓ geprüft'}</button>
              </div>
            ))}
          </div>
        )}

        <div className="card">
          <h3>Preis & Kalkulation</h3>
          {lastPrice && <div className="callout" style={{ marginBottom: 10 }}>📈 Letzter EK: <b>{fmtEUR(lastPrice.price)}/{lastPrice.unit || p.unit || 'ME'}</b> – {fmtDate(lastPrice.date)} · {lastPrice.supplier_name || '?'}{lastPrice.project_name ? ` · ${lastPrice.project_name}` : ''} <button className="btn sm" onClick={() => save({ ek: lastPrice.price, supplier_id: lastPrice.supplier_id || p.supplier_id, price_source: 'historie' })}>übernehmen</button></div>}
          <div className="grid g3">
            <Field label="Lieferant"><select value={p.supplier_id || ''} onChange={(e) => save({ supplier_id: e.target.value ? Number(e.target.value) : null })}><option value="">–</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
            <Field label="EK je Einheit €"><NumInput className="" value={p.ek} onCommit={(v) => save({ ek: v })} /></Field>
            <Field label="Rabatt %"><NumInput className="" value={p.discount_pct} onCommit={(v) => save({ discount_pct: v ?? 0 })} /></Field>
            <Field label="Fracht je Einheit €"><NumInput className="" value={p.freight} onCommit={(v) => save({ freight: v ?? 0 })} /></Field>
            <Field label={`Aufschlag % (${p.markup_source})`}><NumInput className="" value={p.markup_pct} onCommit={(v) => save({ markup_pct: v })} /></Field>
            <Field label="VK fix (optional)"><NumInput className="" value={p.vk_override} onCommit={(v) => save({ vk_override: v })} placeholder="berechnet" /></Field>
          </div>
          <table className="t" style={{ marginTop: 10 }}><tbody>
            <tr><td>Einstandspreis</td><td className="num">{c.einstand !== null ? fmtNum(c.einstand, 2) : '–'} €</td><td>Verkaufspreis (EP)</td><td className="num"><b>{c.vk !== null ? fmtEUR(c.vk) : '–'}</b></td></tr>
            <tr><td>Deckungsbeitrag / ME</td><td className="num">{c.db !== null ? fmtNum(c.db, 2) : '–'} €</td><td>Marge</td><td className="num" style={{ color: c.below_min ? 'var(--red)' : undefined }}>{fmtPct(c.margin_pct)}{c.below_min ? ' ⚠ unter Mindestwert' : ''}</td></tr>
            <tr><td>Positionssumme</td><td className="num"><b>{fmtEUR(c.total)}</b></td><td>DB gesamt</td><td className="num">{fmtEUR(c.db_total)}</td></tr>
          </tbody></table>
          {p.price_source && <div className="muted small">Preisquelle: {p.price_source}{p.price_date ? ` · ${fmtDate(p.price_date)}` : ''}</div>}
        </div>

        <div className="card">
          <div className="row between"><h3>Anfragen</h3><button className="btn primary" onClick={() => onRequest(p)}>📞 / ✉ Lieferant anfragen</button></div>
          {data.requests.length ? data.requests.map((r) => <div key={r.id} className="row between"><span>{r.supplier_name} · {r.channel === 'telefon' ? '📞' : '✉'}</span><Badge color={['preis_erhalten', 'vollstaendig'].includes(r.status) ? 'green' : r.status === 'rueckfrage' ? 'red' : 'yellow'}>{requestStatusLabel(r.status)}</Badge></div>) : <div className="muted small">Noch nicht angefragt.</div>}
          {data.suggestions.length > 0 && <div style={{ marginTop: 8 }}><div className="muted small">Vorgeschlagene Lieferanten:</div><div className="row">{data.suggestions.map((s) => <button key={s.supplier_id} className={`pill ${p.supplier_id === s.supplier_id ? 'on' : ''}`} title={s.reasons.join(', ')} onClick={() => save({ supplier_id: s.supplier_id })}>{s.name}</button>)}</div></div>}
        </div>

        {data.variants.length > 0 && (
          <div className="card">
            <h3>Varianten / Lieferantenpreise</h3>
            <table className="t"><thead><tr><th>Lieferant</th><th className="num">EK</th><th className="num">Einstand</th><th>Lieferzeit</th><th>Merkmale</th><th /></tr></thead><tbody>
              {data.variants.map((v) => <tr key={v.id} className={p.quote_item_id === v.id ? 'sel' : ''}><td>{v.supplier_name}<div className="muted small">{v.source === 'telefon' ? 'Telefon' : `Angebot ${v.quote_no || ''}`} · {fmtDate(v.quote_date)}{v.valid_until ? ` · gültig bis ${fmtDate(v.valid_until)}` : ''}</div></td><td className="num">{fmtNum(v.price, 2)}</td><td className="num">{fmtNum(v.einstand, 2)}</td><td>{v.delivery_text || (v.delivery_days ? `${v.delivery_days} T.` : '–')}</td><td>{v.tags.map((t) => <Badge key={t} color="green">{t}</Badge>)}</td>
                <td>{p.quote_item_id === v.id ? <Badge color="blue">aktiv</Badge> : <button className="btn sm" onClick={async () => { await run(() => api.post(`/positions/${p.id}/select-variant`, { quote_item_id: v.id }), 'Variante übernommen'); reload(); onChanged?.(); }}>Wählen</button>}</td></tr>)}
            </tbody></table>
          </div>
        )}

        <div className="card">
          <h3>Position</h3>
          <div className="grid g3">
            <Field label="OZ"><TextInput value={p.oz || ''} onCommit={(v) => save({ oz: v })} /></Field>
            <Field label="Menge"><NumInput className="" value={p.qty} onCommit={(v) => save({ qty: v })} /></Field>
            <Field label="Einheit"><select value={p.unit || ''} onChange={(e) => save({ unit: e.target.value || null })}><option value="">?</option>{[...new Set([...KNOWN_UNITS, p.unit].filter(Boolean))].map((u) => <option key={u}>{u}</option>)}</select></Field>
            <Field label="Positionsart"><select value={p.pos_type} onChange={(e) => save({ pos_type: e.target.value })}>{Object.entries(POS_TYPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
            <Field label="Warengruppe"><select value={p.group_name || ''} onChange={(e) => save({ group_name: e.target.value || null })}><option value="">nicht erkannt</option>{groups.map((g) => <option key={g}>{g}</option>)}</select></Field>
            <Field label="Produkt">
              <select value={p.product_id || ''} onChange={(e) => save({ product_id: e.target.value ? Number(e.target.value) : null })}><option value="">–</option>{(products.data || []).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select>
            </Field>
          </div>
          {!p.product_id && <button className="btn sm" style={{ marginTop: 8 }} onClick={async () => { await run(() => api.post('/products', { name: p.short_text, group_name: p.group_name, unit: p.unit, mode, position_id: p.id }), 'Produkt angelegt und zugeordnet'); reload(); products.reload(); onChanged?.(); }}>＋ Produkt aus Positionstext anlegen</button>}
          <Field label="Kurztext" style={{ marginTop: 10 }}><TextInput value={p.short_text || ''} onCommit={(v) => save({ short_text: v })} /></Field>
          <Field label="Langtext" style={{ marginTop: 10 }}><TextInput multiline rows={6} value={p.long_text || ''} onCommit={(v) => save({ long_text: v })} /></Field>
          <Field label="Interne Notiz" style={{ marginTop: 10 }}><TextInput value={p.notes || ''} onCommit={(v) => save({ notes: v })} /></Field>
          <div className="row between" style={{ marginTop: 10 }}>
            <span className="muted small">Erkennungssicherheit: {p.confidence !== null ? `${Math.round(p.confidence * 100)} %` : '–'}</span>
            <button className="btn sm danger" onClick={async () => { if (confirm('Position löschen?')) { await run(() => api.del(`/positions/${p.id}`)); onChanged?.(); onClose(); } }}>Position löschen</button>
          </div>
        </div>

        {data.history.length > 0 && (
          <div className="card">
            <h3>Preishistorie / Objektpreise</h3>
            <table className="t"><thead><tr><th>Datum</th><th>Lieferant</th><th>Produkt/Text</th><th className="num">EK</th><th>Projekt</th></tr></thead><tbody>
              {data.history.map((h) => <tr key={h.id}><td>{fmtDate(h.date)}</td><td>{h.supplier_name}</td><td>{h.text}</td><td className="num">{fmtNum(h.price, 2)} €/{h.unit}</td><td>{h.project_name}</td></tr>)}
            </tbody></table>
          </div>
        )}
      </div>
    </div>
  );
}
