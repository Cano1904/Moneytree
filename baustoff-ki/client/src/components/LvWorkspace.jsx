// LV-Tabelle mit Kalkulation: Inline-Bearbeitung, Mehrfachauswahl, Aufschläge (global/Gruppe/Auswahl), Filter, Exporte
import { memo, useCallback, useMemo, useRef, useState } from 'react';
import { api, download, docUrl } from '../api.js';
import { Badge, Dot, Dropzone, Empty, Field, Modal, NumInput, useAction, useIsMobile, useLoad, useToast } from './ui.jsx';
import PositionDrawer from './PositionDrawer.jsx';
import RequestDialog from './RequestDialog.jsx';
import { fmtEUR, fmtNum, fmtPct, KNOWN_UNITS } from '../../../shared/format.js';
import { GROUP_NAMES } from '../../../shared/groups.js';
import { POS_TYPES } from '../../../shared/pricing.js';

export function LvUpload({ projectId, onDone }) {
  const [run, busy] = useAction();
  const [useAi, setUseAi] = useState(true);
  const ai = useLoad('/ai/status');
  const [result, setResult] = useState(null);
  const up = async (files) => {
    const r = await run(() => api.upload(`/projects/${projectId}/lvs/upload`, files[0], { useAi: String(useAi) }), (x) => `${x.count} Positionen erkannt`);
    if (r) { setResult(r); if (!r.info.warnings?.length) onDone(r); }
  };
  return (
    <div className="card">
      <h2>LV hochladen</h2>
      <Dropzone camera onFiles={up} disabled={busy} accept=".pdf,.xlsx,.xlsm,.csv,.docx,.x81,.x82,.x83,.x84,.x86,.d83,.p83,.d81,.xml,.png,.jpg,.jpeg,.txt"
        label={busy ? '⏳ Dokument wird gelesen und analysiert …' : 'LV-Datei hierher ziehen oder klicken'}
        hint="PDF · Excel/CSV · GAEB (X83/X84/D83) · Word · Bild/Scan — das Original wird unverändert in der Projektakte gespeichert" />
      <div className="row" style={{ marginTop: 10 }}>
        <label className="row"><input type="checkbox" checked={useAi && ai.data?.enabled} disabled={!ai.data?.enabled} onChange={(e) => setUseAi(e.target.checked)} /> KI-Analyse verwenden</label>
        {!ai.data?.enabled && <span className="muted small">KI nicht konfiguriert – Regel-Erkennung für PDF/Excel/GAEB/Word aktiv; Scans/Bilder benötigen KI oder manuelle Erfassung.</span>}
      </div>
      {result && (
        <div className="callout warn" style={{ marginTop: 12 }}>
          <b>{result.count} Positionen erkannt</b> ({result.info.method}).
          <ul>{result.info.warnings.map((w) => <li key={w}>{w}</li>)}</ul>
          <button className="btn primary" onClick={() => onDone(result)}>Zum LV →</button>
        </div>
      )}
    </div>
  );
}

const STATE_FILTERS = [['', 'Alle'], ['offen', '🟡 Preis fehlt'], ['pruefen', '🔴 Prüfen'], ['marge', '🔴 Marge'], ['angefragt', '🔵 angefragt'], ['kalkuliert', '🟢 kalkuliert']];

export default function LvWorkspace({ lvId, onChange, onDeleted }) {
  const { data, setData, reload } = useLoad(`/lvs/${lvId}`, [lvId]);
  const suppliers = useLoad('/suppliers');
  const [sel, setSel] = useState(new Set());
  const [flash, setFlash] = useState(new Set());
  const [filter, setFilter] = useState({ q: '', group: '', state: '', supplier: '' });
  const [open, setOpen] = useState(null);
  const [reqFor, setReqFor] = useState(null);
  const [suggest, setSuggest] = useState(null);
  const [bulk, setBulk] = useState({ markup: '', supplier: '', group: '' });
  const [globalMk, setGlobalMk] = useState('');
  const [groupMk, setGroupMk] = useState({ group: '', value: '' });
  const [overwrite, setOverwrite] = useState(false);
  const lastClick = useRef(null);
  const [run, busy] = useAction();
  const toast = useToast();
  const isMobile = useIsMobile();

  const positions = data?.positions || [];
  const visible = useMemo(() => positions.filter((p) => {
    if (filter.q && !`${p.oz} ${p.short_text} ${p.long_text} ${p.group_name}`.toLowerCase().includes(filter.q.toLowerCase())) return false;
    if (filter.group && p.group_name !== filter.group) return false;
    if (filter.state && p.state.id !== filter.state) return false;
    if (filter.supplier && String(p.supplier_id || '') !== filter.supplier) return false;
    return true;
  }), [positions, filter]);
  const groupsInLv = useMemo(() => [...new Set(positions.map((p) => p.group_name).filter(Boolean))], [positions]);

  const doFlash = (ids) => { setFlash(new Set(ids)); setTimeout(() => setFlash(new Set()), 1700); };
  const refresh = async () => { await reload(); onChange?.(); };
  const patchPos = useCallback(async (id, body) => {
    const updated = await run(() => api.patch(`/positions/${id}`, body));
    if (updated) {
      setData((d) => ({ ...d, positions: d.positions.map((p) => (p.id === id ? { ...p, ...updated } : p)) }));
      reload();
      onChange?.();
    }
  }, [run, setData, reload, onChange]);

  const toggle = useCallback((id, e) => {
    setSel((s) => {
      const n = new Set(s);
      if (e?.shiftKey && lastClick.current !== null) {
        const ids = visible.map((p) => p.id);
        const [a, b] = [ids.indexOf(lastClick.current), ids.indexOf(id)].sort((x, y) => x - y);
        ids.slice(a, b + 1).forEach((x) => n.add(x));
      } else if (n.has(id)) n.delete(id); else n.add(id);
      lastClick.current = id;
      return n;
    });
  }, [visible]);

  if (!data) return <div className="muted">Lade LV …</div>;
  const { lv, totals, project } = data;
  const ids = [...sel];

  const applyBulk = async (set, label) => {
    const r = await run(() => api.post(`/lvs/${lvId}/bulk`, { ids, set }));
    if (r) { toast(`${r.changed.length} Positionen: ${label}`, 'ok'); doFlash(r.changed); refresh(); }
  };
  const applyMarkup = async (body) => {
    const r = await run(() => api.post(`/lvs/${lvId}/markup`, { ...body, overwriteManual: overwrite }));
    if (r) { toast(`Aufschlag ${body.markup_pct} %: ${r.changed.length} Positionen geändert${r.skipped ? ` · ${r.skipped} manuell gesetzte unverändert` : ''}`, 'ok', 7000); doFlash(r.changed); refresh(); }
  };
  const exp = (kind, ext) => run(() => download(`/lvs/${lvId}/export/${kind}`, `export.${ext}`));
  const idx = open ? visible.findIndex((p) => p.id === open) : -1;

  let lastTitle = null;
  return (
    <div className="stack">
      <div className="grid g4">
        <div className="card kpi"><div className="l">Angebot netto</div><div className="v">{fmtEUR(totals.net)}</div><div className="muted small">brutto {fmtEUR(totals.gross)}{totals.optional ? ` · Bedarf/Alt. ${fmtEUR(totals.optional)}` : ''}</div></div>
        <div className="card kpi"><div className="l">Einstand (EK inkl. Rabatt/Fracht)</div><div className="v">{fmtEUR(totals.einstand)}</div></div>
        <div className="card kpi"><div className="l">Deckungsbeitrag / Marge</div><div className="v" style={{ color: totals.margin_pct !== null && totals.margin_pct < data.minMargin ? 'var(--red)' : undefined }}>{fmtEUR(totals.db)} · {fmtPct(totals.margin_pct)}</div></div>
        <div className="card kpi"><div className="l">Preise vollständig</div><div className="v">{totals.priced}/{totals.count}</div><div className="muted small">{totals.missing ? `🟡 ${totals.missing} ohne Preis/Menge` : '🟢 vollständig'}{totals.belowMin ? ` · 🔴 ${totals.belowMin} unter Mindestmarge` : ''}</div></div>
      </div>

      <div className="card">
        <div className="row" style={{ gap: 14, alignItems: 'flex-end' }}>
          <Field label={`Globaler Aufschlag (aktuell ${fmtNum(lv.global_markup, 1)} %)`}><div className="row"><input style={{ width: 90 }} value={globalMk} onChange={(e) => setGlobalMk(e.target.value)} placeholder="%" inputMode="decimal" /><button className="btn primary" disabled={globalMk === '' || busy} onClick={() => applyMarkup({ scope: 'global', markup_pct: Number(globalMk.replace(',', '.')) })}>Gesamtes LV</button></div></Field>
          <Field label="Aufschlag je Warengruppe"><div className="row">
            <select style={{ width: 170 }} value={groupMk.group} onChange={(e) => setGroupMk({ ...groupMk, group: e.target.value })}><option value="">Gruppe …</option>{groupsInLv.map((g) => <option key={g}>{g}</option>)}</select>
            <input style={{ width: 80 }} value={groupMk.value} onChange={(e) => setGroupMk({ ...groupMk, value: e.target.value })} placeholder="%" inputMode="decimal" />
            <button className="btn" disabled={!groupMk.group || groupMk.value === ''} onClick={() => applyMarkup({ scope: 'gruppe', group_name: groupMk.group, markup_pct: Number(groupMk.value.replace(',', '.')) })}>Anwenden</button>
          </div></Field>
          <label className="row small"><input type="checkbox" checked={overwrite} onChange={(e) => setOverwrite(e.target.checked)} /> auch manuell gesetzte Aufschläge überschreiben</label>
          <Field label="Mindestmarge %" style={{ width: 120 }}><NumInput className="" value={lv.min_margin} onCommit={async (v) => { await run(() => api.patch(`/lvs/${lvId}`, { min_margin: v })); refresh(); }} /></Field>
        </div>
        {data.calculations?.filter((c) => c.scope === 'gruppe').length > 0 && <div className="muted small" style={{ marginTop: 8 }}>Gruppen-Aufschläge: {data.calculations.filter((c) => c.scope === 'gruppe').map((c) => `${c.group_name} ${fmtNum(c.markup_pct, 1)} %`).join(' · ')}</div>}
      </div>

      {(lv.issues || []).length > 0 && <div className="callout">{lv.issues.map((i) => <div key={i.key}>💡 {i.msg}</div>)}</div>}

      <div className="row between">
        <div className="row">
          <input style={{ width: 220 }} placeholder="Filtern …" value={filter.q} onChange={(e) => setFilter({ ...filter, q: e.target.value })} />
          <select style={{ width: 160 }} value={filter.group} onChange={(e) => setFilter({ ...filter, group: e.target.value })}><option value="">Alle Gruppen</option>{groupsInLv.map((g) => <option key={g}>{g}</option>)}</select>
          <select style={{ width: 150 }} value={filter.state} onChange={(e) => setFilter({ ...filter, state: e.target.value })}>{STATE_FILTERS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          <select style={{ width: 170 }} value={filter.supplier} onChange={(e) => setFilter({ ...filter, supplier: e.target.value })}><option value="">Alle Lieferanten</option>{(suppliers.data || []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
        </div>
        <div className="row">
          <button className="btn" onClick={async () => { const r = await run(() => api.post(`/lvs/${lvId}/suggest-suppliers`, {})); if (r) setSuggest(r); }}>🤖 Lieferanten vorschlagen</button>
          <button className="btn" onClick={async () => { const r = await run(() => api.post(`/lvs/${lvId}/check`), (x) => `Prüfung: ${x.flagged} Position(en) mit Hinweisen`); if (r) refresh(); }}>✔ Plausibilität prüfen</button>
          <button className="btn" onClick={async () => { const r = await run(() => api.post(`/lvs/${lvId}/positions`, { short_text: 'Neue Position' })); if (r) { await refresh(); setOpen(r.id); } }}>＋ Position</button>
          <details style={{ position: 'relative' }}>
            <summary className="btn">⬇ Export</summary>
            <div className="card" style={{ position: 'absolute', right: 0, zIndex: 10, width: 300 }}>
              <div className="stack">
                <button className="btn" onClick={() => exp('pdf', 'pdf')}>Angebots-LV (PDF)</button>
                {lv.format === 'pdf' && lv.meta?.hasText && <button className="btn" onClick={() => exp('overlay', 'pdf')}>Original-PDF mit Preisen</button>}
                <button className="btn" onClick={() => exp('xlsx', 'xlsx')}>{lv.format === 'excel' ? 'Original-Excel mit Preisen' : 'Excel (Angebot)'}</button>
                {lv.format === 'gaeb-xml' && <button className="btn" onClick={() => exp('gaeb', 'x84')}>GAEB X84 (Angebotsabgabe)</button>}
                <button className="btn" onClick={() => exp('kalkulation', 'xlsx')}>Interne Kalkulation (Excel)</button>
                {data.source && <a className="btn" href={docUrl(data.source.id)} target="_blank" rel="noreferrer">Original ansehen</a>}
                <button className="btn danger" onClick={async () => { if (confirm(`LV „${lv.name}“ löschen? Das Original bleibt in der Akte.`)) { await run(() => api.del(`/lvs/${lvId}`)); onDeleted?.(); } }}>LV löschen</button>
              </div>
            </div>
          </details>
        </div>
      </div>

      {sel.size > 0 && (
        <div className="card" style={{ background: 'var(--accent-2)', position: 'sticky', top: 60, zIndex: 5 }}>
          <div className="row">
            <b>{sel.size} ausgewählt</b>
            <input style={{ width: 80 }} placeholder="%" value={bulk.markup} onChange={(e) => setBulk({ ...bulk, markup: e.target.value })} inputMode="decimal" />
            <button className="btn" disabled={bulk.markup === ''} onClick={() => applyBulk({ markup_pct: Number(bulk.markup.replace(',', '.')) }, `Aufschlag → ${bulk.markup} %`)}>Aufschlag setzen</button>
            <select style={{ width: 180 }} value={bulk.supplier} onChange={(e) => setBulk({ ...bulk, supplier: e.target.value })}><option value="">Lieferant …</option>{(suppliers.data || []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
            <button className="btn" disabled={!bulk.supplier} onClick={() => applyBulk({ supplier_id: Number(bulk.supplier) }, 'Lieferant zugeordnet')}>Zuordnen</button>
            <select style={{ width: 160 }} value={bulk.group} onChange={(e) => setBulk({ ...bulk, group: e.target.value })}><option value="">Warengruppe …</option>{GROUP_NAMES.map((g) => <option key={g}>{g}</option>)}</select>
            <button className="btn" disabled={!bulk.group} onClick={() => applyBulk({ group_name: bulk.group }, `Gruppe → ${bulk.group}`)}>Setzen</button>
            <button className="btn primary" onClick={() => setReqFor(positions.filter((p) => sel.has(p.id)))}>✉ / 📞 Anfragen</button>
            <button className="btn ghost" onClick={() => setSel(new Set())}>Auswahl aufheben</button>
          </div>
        </div>
      )}

      {isMobile ? (
        <div>
          {visible.map((p) => {
            const title = p.title_path !== lastTitle && p.title_path ? <div key={`t${p.id}`} className="muted small" style={{ fontWeight: 700, margin: '12px 2px 6px' }}>{p.title_path}</div> : null;
            lastTitle = p.title_path;
            return [title, <MobileRow key={p.id} p={p} selected={sel.has(p.id)} toggle={toggle} onOpen={setOpen} patchPos={patchPos} />];
          })}
          {!visible.length && <Empty>{positions.length ? 'Keine Positionen für diesen Filter.' : 'Noch keine Positionen.'}</Empty>}
        </div>
      ) : (
      <div className="tablewrap">
        <table className="t">
          <thead><tr>
            <th><input type="checkbox" checked={visible.length > 0 && visible.every((p) => sel.has(p.id))} onChange={(e) => setSel(e.target.checked ? new Set(visible.map((p) => p.id)) : new Set())} title="Alle sichtbaren auswählen" /></th>
            <th>Pos.</th><th style={{ minWidth: 220 }}>Beschreibung</th><th className="num">Menge</th><th>Einh.</th><th>Produkt / Gruppe</th><th>Lieferant</th>
            <th className="num">EK</th><th className="num">Aufschl. %</th><th className="num">VK</th><th className="num">Gesamt</th><th>Status</th>
          </tr></thead>
          <tbody>
            {visible.map((p) => {
              const title = p.title_path !== lastTitle && p.title_path ? <tr key={`t${p.id}`} className="title"><td colSpan={12}>{p.title_path}</td></tr> : null;
              lastTitle = p.title_path;
              return [title, <Row key={p.id} p={p} selected={sel.has(p.id)} flash={flash.has(p.id)} toggle={toggle} onOpen={setOpen} patchPos={patchPos} suppliers={suppliers.data || []} />];
            })}
          </tbody>
        </table>
        {!visible.length && <Empty>{positions.length ? 'Keine Positionen für diesen Filter.' : 'Noch keine Positionen – über „＋ Position“ manuell erfassen.'}</Empty>}
      </div>
      )}
      <div className="muted small hide-mobile">Tipp: Zeile anklicken für Details · Shift+Klick für Bereichsauswahl · Werte werden beim Verlassen des Feldes automatisch gespeichert · Positionsart: {Object.entries(POS_TYPES).map(([k, v]) => `${k}=${v}`).join(', ')}</div>

      {open && <PositionDrawer id={open} projectId={project.id} mode={project.mode} suppliers={suppliers.data || []} onClose={() => setOpen(null)} onChanged={refresh}
        onPrev={idx > 0 ? () => setOpen(visible[idx - 1].id) : null} onNext={idx >= 0 && idx < visible.length - 1 ? () => setOpen(visible[idx + 1].id) : null} onRequest={(pos) => setReqFor([pos])} />}
      {reqFor && <RequestDialog projectId={project.id} positions={reqFor} suppliers={suppliers.data || []} onClose={() => { setReqFor(null); refresh(); }} />}
      {suggest && (
        <Modal title="🤖 Lieferantenvorschläge" onClose={() => setSuggest(null)} wide footer={<>
          <button className="btn" onClick={() => setSuggest(null)}>Schließen</button>
          <button className="btn primary" disabled={!suggest.suggestions.length} onClick={async () => { const r = await run(() => api.post(`/lvs/${lvId}/suggest-suppliers`, { apply: true }), 'Vorschläge übernommen'); if (r) { setSuggest(null); refresh(); } }}>Alle Erstvorschläge übernehmen</button>
        </>}>
          <p className="muted">Vorschläge basieren auf Warengruppen der Lieferanten, Preishistorie und Präferenz. Sie entscheiden – einzeln per Klick oder alle auf einmal.</p>
          {suggest.unassigned > 0 && <div className="callout warn">{suggest.unassigned} Position(en) ohne passenden Lieferanten – bitte Lieferanten-Warengruppen pflegen oder manuell zuordnen.</div>}
          <table className="t"><thead><tr><th>Pos.</th><th>Text</th><th>Gruppe</th><th>Vorschläge</th></tr></thead><tbody>
            {suggest.suggestions.map((s) => <tr key={s.position_id}><td>{s.oz}</td><td>{s.short_text}</td><td>{s.group_name}</td><td><div className="row">{s.options.map((o) => (
              <button key={o.supplier_id} className="btn sm" title={o.reasons.join(', ')} onClick={async () => { await patchPos(s.position_id, { supplier_id: o.supplier_id }); setSuggest({ ...suggest, suggestions: suggest.suggestions.filter((x) => x.position_id !== s.position_id) }); }}>{o.name} <span className="muted small">({o.reasons.join(', ')})</span></button>
            ))}</div></td></tr>)}
          </tbody></table>
        </Modal>
      )}
    </div>
  );
}

const Row = memo(function Row({ p, selected, flash, toggle, onOpen, patchPos, suppliers }) {
  const c = p.calc;
  const openFlags = (p.flags || []).filter((f) => !f.resolved && f.severity !== 'info');
  const text = p.pos_type === 'T';
  return (
    <tr className={`click ${selected ? 'sel' : ''} ${flash ? 'flash' : ''}`} onClick={() => onOpen(p.id)}>
      <td onClick={(e) => { e.stopPropagation(); toggle(p.id, e); }}><input type="checkbox" checked={selected} readOnly /></td>
      <td className="nowrap"><Dot color={p.state.color} title={p.state.label} /> {p.oz}</td>
      <td>
        <div style={{ fontWeight: 600 }}>{p.short_text}</div>
        <div className="muted small">{p.pos_type !== 'N' && <Badge color="gray">{POS_TYPES[p.pos_type]}</Badge>} {openFlags.length > 0 && <span style={{ color: 'var(--red)' }}>⚠ Prüfung erforderlich: {openFlags[0].msg}{openFlags.length > 1 ? ` (+${openFlags.length - 1})` : ''}</span>}{p.confidence !== null && p.confidence < 0.6 && <span> · Erkennung unsicher</span>}</div>
      </td>
      <td className="num" onClick={(e) => e.stopPropagation()}>{text ? '' : <NumInput value={p.qty} onCommit={(v) => patchPos(p.id, { qty: v })} />}</td>
      <td onClick={(e) => e.stopPropagation()}>{text ? '' : <select className="cell" style={{ minHeight: 30, padding: '2px 4px', width: 70 }} value={p.unit || ''} onChange={(e) => patchPos(p.id, { unit: e.target.value || null })}><option value="">?</option>{[...new Set([...KNOWN_UNITS, p.unit].filter(Boolean))].map((u) => <option key={u}>{u}</option>)}</select>}</td>
      <td className="small">{p.product_name || <span className="muted">{p.group_name || '–'}</span>}</td>
      <td onClick={(e) => e.stopPropagation()}>{text ? '' : <select className="cell" style={{ minHeight: 30, padding: '2px 4px', width: 130, textAlign: 'left' }} value={p.supplier_id || ''} onChange={(e) => patchPos(p.id, { supplier_id: e.target.value ? Number(e.target.value) : null })}><option value="">–</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>}</td>
      <td className="num" onClick={(e) => e.stopPropagation()}>{text ? '' : <NumInput value={p.ek} onCommit={(v) => patchPos(p.id, { ek: v })} placeholder="EK" title={p.price_source ? `Quelle: ${p.price_source}${p.discount_pct ? `, Rabatt ${p.discount_pct} %` : ''}${p.freight ? `, Fracht ${fmtNum(p.freight, 2)}/ME` : ''}` : ''} />}</td>
      <td className="num" onClick={(e) => e.stopPropagation()}>{text ? '' : <><NumInput value={p.markup_pct} onCommit={(v) => patchPos(p.id, { markup_pct: v })} /><div className="muted" style={{ fontSize: 10 }}>{p.markup_source}</div></>}</td>
      <td className="num">{c.vk !== null ? fmtNum(c.vk) : ''}{p.vk_override !== null && p.vk_override !== undefined && <div className="muted" style={{ fontSize: 10 }}>fix</div>}</td>
      <td className="num"><b>{c.total !== null ? fmtNum(c.total) : ''}</b>{c.margin_pct !== null && <div style={{ fontSize: 11, color: c.below_min ? 'var(--red)' : 'var(--muted)' }}>{fmtPct(c.margin_pct)}</div>}</td>
      <td><Badge color={p.state.color}>{p.state.label}</Badge></td>
    </tr>
  );
});

/** Handy-Ansicht einer Position: Karte mit den wichtigsten Feldern, Tippen öffnet die Details. */
const MobileRow = memo(function MobileRow({ p, selected, toggle, onOpen, patchPos }) {
  const c = p.calc;
  const openFlags = (p.flags || []).filter((f) => !f.resolved && f.severity !== 'info');
  return (
    <div className={`mcard ${selected ? 'sel' : ''}`}>
      <div className="row" style={{ flexWrap: 'nowrap', alignItems: 'flex-start' }} onClick={() => onOpen(p.id)}>
        <input type="checkbox" checked={selected} onClick={(e) => { e.stopPropagation(); toggle(p.id, e); }} readOnly style={{ marginTop: 2 }} />
        <div className="grow">
          <div className="muted small"><Dot color={p.state.color} /> {p.oz} {p.pos_type !== 'N' && `· ${POS_TYPES[p.pos_type]}`} · {p.supplier_name || p.group_name || '–'}</div>
          <div style={{ fontWeight: 600 }}>{p.short_text}</div>
          {openFlags.length > 0 && <div className="small" style={{ color: 'var(--red)' }}>⚠ {openFlags[0].msg}</div>}
        </div>
        <div className="right nowrap"><b>{c.total !== null ? fmtEUR(c.total) : '–'}</b><div className="muted small">{c.vk !== null ? `EP ${fmtNum(c.vk)}` : p.state.label}</div></div>
      </div>
      {p.pos_type !== 'T' && (
        <div className="mrow">
          <label>Menge {p.unit || '?'}<NumInput value={p.qty} onCommit={(v) => patchPos(p.id, { qty: v })} /></label>
          <label>EK €<NumInput value={p.ek} onCommit={(v) => patchPos(p.id, { ek: v })} placeholder="EK" /></label>
          <label>Aufschlag %<NumInput value={p.markup_pct} onCommit={(v) => patchPos(p.id, { markup_pct: v })} /></label>
        </div>
      )}
    </div>
  );
});
