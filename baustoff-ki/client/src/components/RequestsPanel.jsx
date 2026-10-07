// Anfrage-Management je Projekt: fehlende Preise, Anfragen je Lieferant, Lieferantenangebote importieren
import { useEffect, useState } from 'react';
import { api, docUrl } from '../api.js';
import { Badge, Dropzone, Empty, Field, Modal, NumInput, useAction, useLoad } from './ui.jsx';
import { RequestView } from './RequestDialog.jsx';
import { REQUEST_STATUS, requestStatusLabel } from '../../../shared/status.js';
import { fmtDate, fmtNum, fmtQty } from '../../../shared/format.js';

export default function RequestsPanel({ projectId, onChange }) {
  const reqs = useLoad(`/requests?project_id=${projectId}`, [projectId]);
  const proj = useLoad(`/projects/${projectId}`, [projectId]);
  const suppliers = useLoad('/suppliers');
  const [lvData, setLvData] = useState(null);
  const [openReq, setOpenReq] = useState(null);
  const [quoteFor, setQuoteFor] = useState(null);
  const [run, busy] = useAction();
  const lvIds = proj.data?.lvs.map((l) => l.id).join(',');
  useEffect(() => {
    if (!lvIds) return;
    Promise.all(lvIds.split(',').map((i) => api.get(`/lvs/${i}`))).then((all) => setLvData(all.flatMap((x) => x.positions))).catch(() => {});
  }, [lvIds, reqs.data]);
  const refresh = () => { reqs.reload(); proj.reload(); onChange?.(); };
  const missing = (lvData || []).filter((p) => p.pos_type !== 'T' && p.ek === null);
  const bySupplier = missing.reduce((m, p) => { const k = p.supplier_name || '— ohne Lieferant —'; (m[k] ||= []).push(p); return m; }, {});
  return (
    <div className="stack">
      <div className="grid g2">
        <div className="card">
          <div className="row between"><h2>Welche Preise fehlen noch?</h2><Badge color={missing.length ? 'yellow' : 'green'}>{missing.length} offen</Badge></div>
          {missing.length ? Object.entries(bySupplier).map(([s, list]) => (
            <details key={s} open={Object.keys(bySupplier).length < 4}><summary><b>{s}</b> · {list.length} Position(en) · {list.filter((p) => p.state.id === 'angefragt').length} angefragt</summary>
              <div className="list small">{list.map((p) => <div key={p.id}>{p.oz} {p.short_text} – {fmtQty(p.qty)} {p.unit} {p.state.id === 'angefragt' && <Badge color="blue">angefragt</Badge>}</div>)}</div>
            </details>
          )) : <Empty>🟢 Alle Preise vorhanden.</Empty>}
          {bySupplier['— ohne Lieferant —'] && <div className="callout warn" style={{ marginTop: 8 }}>Positionen ohne Lieferant im Reiter „LV & Kalkulation“ zuordnen (oder „🤖 Lieferanten vorschlagen“).</div>}
        </div>
        <div className="card stack">
          <h2>Aktionen</h2>
          <button className="btn big primary" disabled={busy} onClick={async () => { const r = await run(() => api.post(`/projects/${projectId}/requests/prepare-all`), (x) => `${x.requests.length} Anfrage(n) für ${x.positions} Positionen vorbereitet`); if (r) refresh(); }}><span className="ico">⚡</span>Alle Anfragen vorbereiten<div className="small" style={{ fontWeight: 400 }}>Positionen je Lieferant automatisch bündeln</div></button>
          <button className="btn big" onClick={() => setQuoteFor({})}><span className="ico">📥</span>Lieferantenangebot hochladen<div className="small muted" style={{ fontWeight: 400 }}>Preise automatisch erkennen und bestätigen</div></button>
        </div>
      </div>
      <div className="card" style={{ padding: 0 }}>
        {reqs.data?.length ? (
          <table className="t"><thead><tr><th>Lieferant</th><th>Kanal</th><th>Status</th><th className="num">Positionen</th><th className="num">Preis fehlt</th><th>Angefragt</th><th /></tr></thead><tbody>
            {reqs.data.map((r) => (
              <tr key={r.id}>
                <td><b>{r.supplier_name}</b>{r.supplier_phone && <div className="muted small">☎ {r.supplier_phone}</div>}</td>
                <td>{r.channel === 'telefon' ? '📞 Telefon' : '✉ Mail'}</td>
                <td><select style={{ minHeight: 32, padding: 4 }} value={r.status} onChange={async (e) => { await run(() => api.patch(`/requests/${r.id}`, { status: e.target.value })); refresh(); }}>{REQUEST_STATUS.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</select></td>
                <td className="num">{r.items}</td>
                <td className="num">{r.missing ? <Badge color="yellow">{r.missing}</Badge> : <Badge color="green">0</Badge>}</td>
                <td>{r.sent_at ? fmtDate(r.sent_at) : '–'}</td>
                <td className="nowrap"><button className="btn sm" onClick={() => setOpenReq(r)}>Öffnen</button> <button className="btn sm" onClick={() => setQuoteFor({ request_id: r.id, supplier_id: r.supplier_id })}>📥 Preise</button> <button className="btn sm ghost danger" onClick={async () => { if (confirm('Anfrage löschen?')) { await run(() => api.del(`/requests/${r.id}`)); refresh(); } }}>✕</button></td>
              </tr>
            ))}
          </tbody></table>
        ) : <Empty>Noch keine Anfragen. Positionen im LV auswählen → „Anfragen“, oder „Alle Anfragen vorbereiten“.</Empty>}
      </div>
      {proj.data?.quotes?.length > 0 && (
        <div className="card">
          <h2>Erhaltene Lieferantenangebote</h2>
          <table className="t"><thead><tr><th>Lieferant</th><th>Quelle</th><th>Nr.</th><th>gültig bis</th><th>Lieferzeit</th><th>Datum</th><th /></tr></thead><tbody>
            {proj.data.quotes.map((q) => <tr key={q.id}><td>{q.supplier_name}</td><td>{q.source}</td><td>{q.quote_no}</td><td>{fmtDate(q.valid_until)}</td><td>{q.delivery_time}</td><td>{fmtDate(q.created_at)}</td><td>{q.document_id && <a href={docUrl(q.document_id)} target="_blank" rel="noreferrer">Dokument</a>}</td></tr>)}
          </tbody></table>
        </div>
      )}
      {openReq && <Modal title={`Anfrage · ${openReq.supplier_name}`} wide onClose={() => { setOpenReq(null); refresh(); }}><RequestView id={openReq.id} projectId={projectId} onDone={() => { setOpenReq(null); refresh(); }} /></Modal>}
      {quoteFor && <QuoteImport projectId={projectId} preset={quoteFor} suppliers={suppliers.data || []} onClose={() => { setQuoteFor(null); refresh(); }} />}
    </div>
  );
}

/** Lieferantenangebot einlesen → Vorschlag prüfen → bestätigen (nichts wird ohne Bestätigung übernommen). */
export function QuoteImport({ projectId, preset, suppliers, onClose }) {
  const [supplierId, setSupplierId] = useState(preset.supplier_id || '');
  const [draft, setDraft] = useState(null);
  const [run, busy] = useAction();
  const analyze = async (files) => {
    const r = await run(() => api.upload(`/projects/${projectId}/quotes/analyze`, files[0], { request_id: preset.request_id, supplier_id: supplierId }));
    if (r) {
      if (!supplierId && r.supplier_id) setSupplierId(r.supplier_id);
      const posById = new Map(r.positions.map((p) => [p.id, p]));
      setDraft({ ...r, distribute_freight: Boolean(r.freight_total), items: r.items.map((i) => ({ ...i, apply: Boolean(i.position_id) && i.price !== null && posById.get(i.position_id)?.ek === null })) });
    }
  };
  const setItem = (k, patch) => setDraft({ ...draft, items: draft.items.map((x, i) => (i === k ? { ...x, ...patch } : x)) });
  const save = async () => {
    const r = await run(() => api.post(`/projects/${projectId}/quotes`, { supplier_id: Number(supplierId), request_id: preset.request_id, document_id: draft.document_id, quote_no: draft.quote_no, valid_until: draft.valid_until, freight_total: draft.freight_total, distribute_freight: draft.distribute_freight, discount_pct: draft.discount_pct, delivery_time: draft.delivery_text, items: draft.items }), (x) => `${x.applied} Preise übernommen`);
    if (r) onClose();
  };
  const posById = draft ? new Map(draft.positions.map((p) => [p.id, p])) : new Map();
  return (
    <Modal title="Lieferantenangebot übernehmen" wide onClose={onClose} footer={draft && <><button className="btn" onClick={onClose}>Abbrechen</button><button className="btn primary" disabled={busy || !supplierId || !draft.items.some((i) => i.price !== null)} onClick={save}>✓ Bestätigen & übernehmen ({draft.items.filter((i) => i.apply).length} in LV)</button></>}>
      <div className="stack">
        <Field label="Lieferant"><select value={supplierId} onChange={(e) => setSupplierId(e.target.value)}><option value="">Bitte wählen …</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
        {!draft && <Dropzone onFiles={analyze} disabled={busy} accept=".pdf,.xlsx,.csv,.docx,.png,.jpg,.jpeg,.txt" label={busy ? '⏳ Angebot wird ausgelesen …' : 'Angebot (PDF, Excel, Word, Foto) hier ablegen'} hint="Artikel, Preis, Einheit, Fracht, Rabatt, Lieferzeit und Preisbindung werden erkannt – Sie bestätigen vor der Übernahme." />}
        {draft && (
          <>
            {draft.warnings?.map((w) => <div key={w} className="callout warn">{w}</div>)}
            <div className="muted small">Erkannt per {draft.method === 'ki' ? 'KI' : 'Regeln'} · bitte Zuordnung, Preise und Einheiten prüfen. Umrechnungsfaktor: Preis × Faktor = EK je LV-Einheit (z. B. Stückpreis × St/m²).</div>
            <div className="grid g4">
              <Field label="Angebots-Nr."><input value={draft.quote_no || ''} onChange={(e) => setDraft({ ...draft, quote_no: e.target.value })} /></Field>
              <Field label="Preisbindung bis"><input type="date" value={draft.valid_until || ''} onChange={(e) => setDraft({ ...draft, valid_until: e.target.value })} /></Field>
              <Field label="Lieferzeit"><input value={draft.delivery_text || ''} onChange={(e) => setDraft({ ...draft, delivery_text: e.target.value })} /></Field>
              <Field label="Rabatt % (gesamt)"><NumInput className="" value={draft.discount_pct} onCommit={(v) => setDraft({ ...draft, discount_pct: v })} /></Field>
              <Field label="Fracht pauschal €"><NumInput className="" value={draft.freight_total} onCommit={(v) => setDraft({ ...draft, freight_total: v })} /></Field>
              <label className="row" style={{ alignSelf: 'end' }}><input type="checkbox" checked={draft.distribute_freight} onChange={(e) => setDraft({ ...draft, distribute_freight: e.target.checked })} /> Fracht nach Warenwert auf Positionen verteilen</label>
            </div>
            <table className="t"><thead><tr><th>Übern.</th><th>Angebotszeile</th><th className="num">Menge</th><th>ME</th><th className="num">Preis €/ME</th><th className="num">Faktor</th><th>→ LV-Position</th><th>aktueller EK</th></tr></thead><tbody>
              {draft.items.map((it, k) => {
                const pos = posById.get(it.position_id);
                return (
                  <tr key={k} className={it.apply ? 'sel' : ''}>
                    <td><input type="checkbox" checked={it.apply} disabled={!it.position_id} onChange={(e) => setItem(k, { apply: e.target.checked })} /></td>
                    <td>{it.text}{it.notes && <div className="small" style={{ color: 'var(--yellow)' }}>⚠ {it.notes}</div>}{it.unit_mismatch && <div className="small" style={{ color: 'var(--red)' }}>⚠ Einheit {it.unit} ≠ LV {pos?.unit} – Faktor prüfen</div>}</td>
                    <td className="num">{fmtQty(it.qty)}</td><td>{it.unit}</td>
                    <td className="num"><NumInput value={it.price} onCommit={(v) => setItem(k, { price: v })} /></td>
                    <td className="num"><NumInput value={it.factor} onCommit={(v) => setItem(k, { factor: v ?? 1 })} /></td>
                    <td><select style={{ minHeight: 32, padding: 4 }} value={it.position_id || ''} onChange={(e) => setItem(k, { position_id: e.target.value ? Number(e.target.value) : null, apply: Boolean(e.target.value) })}><option value="">– nicht zuordnen –</option>{draft.positions.map((p) => <option key={p.id} value={p.id}>{p.oz} {p.short_text.slice(0, 50)}</option>)}</select>{it.position_id && <div className="muted small">Treffer {Math.round((it.match_score || 0) * 100)} %</div>}</td>
                    <td>{pos?.ek !== null && pos?.ek !== undefined ? `${fmtNum(pos.ek)} €` : '–'}</td>
                  </tr>
                );
              })}
            </tbody></table>
            <button className="btn sm" onClick={() => setDraft({ ...draft, items: [...draft.items, { text: 'Manuelle Zeile', price: null, factor: 1, apply: false, position_id: null }] })}>＋ Zeile</button>
          </>
        )}
      </div>
    </Modal>
  );
}
