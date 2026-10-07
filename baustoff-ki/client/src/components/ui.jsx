// Wiederverwendbare UI-Bausteine
import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { api } from '../api.js';

// ---------- Toasts ----------
const ToastCtx = createContext(() => {});
export function ToastProvider({ children }) {
  const [items, setItems] = useState([]);
  const push = useCallback((msg, kind = 'info', ms = 4500) => {
    const id = Math.random();
    setItems((x) => [...x, { id, msg, kind }]);
    setTimeout(() => setItems((x) => x.filter((t) => t.id !== id)), ms);
  }, []);
  return (
    <ToastCtx.Provider value={push}>
      {children}
      <div className="toasts">{items.map((t) => <div key={t.id} className={`toast ${t.kind}`}>{t.msg}</div>)}</div>
    </ToastCtx.Provider>
  );
}
export const useToast = () => useContext(ToastCtx);

/** Lädt Daten und bietet reload(). Fehler werden als Toast gezeigt. */
export function useLoad(url, deps = []) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(Boolean(url));
  const toast = useToast();
  const seq = useRef(0);
  const reload = useCallback(async () => {
    if (!url) return null;
    const my = ++seq.current;
    setLoading(true);
    try {
      const d = await api.get(url);
      if (my === seq.current) { setData(d); setError(null); }
      return d;
    } catch (e) {
      if (my === seq.current) { setError(e.message); toast(e.message, 'err'); }
      return null;
    } finally { if (my === seq.current) setLoading(false); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url, ...deps]);
  useEffect(() => { reload(); }, [reload]);
  return { data, setData, error, loading, reload };
}

/** Führt eine Aktion aus, zeigt Erfolg/Fehler. */
export function useAction() {
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  const run = useCallback(async (fn, okMsg) => {
    setBusy(true);
    try {
      const r = await fn();
      if (okMsg) toast(typeof okMsg === 'function' ? okMsg(r) : okMsg, 'ok');
      return r;
    } catch (e) {
      toast(e.message, 'err', 7000);
      return undefined;
    } finally { setBusy(false); }
  }, [toast]);
  return [run, busy];
}

export function Modal({ title, onClose, children, footer, wide }) {
  useEffect(() => {
    const h = (e) => e.key === 'Escape' && onClose?.();
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onClose]);
  return (
    <div className="modal-bg" onMouseDown={(e) => e.target === e.currentTarget && onClose?.()}>
      <div className={`modal ${wide ? 'wide' : ''}`} role="dialog" aria-modal="true">
        <div className="mh"><h2 style={{ margin: 0 }}>{title}</h2><button className="btn ghost" onClick={onClose} aria-label="Schließen">✕</button></div>
        <div className="mb">{children}</div>
        {footer && <div className="mf">{footer}</div>}
      </div>
    </div>
  );
}

export function Dropzone({ onFiles, accept, multiple = false, label = 'Datei hierher ziehen oder klicken', hint, disabled }) {
  const [over, setOver] = useState(false);
  const ref = useRef();
  return (
    <div className={`drop ${over ? 'over' : ''}`} onClick={() => !disabled && ref.current.click()}
      onDragOver={(e) => { e.preventDefault(); setOver(true); }} onDragLeave={() => setOver(false)}
      onDrop={(e) => { e.preventDefault(); setOver(false); if (!disabled && e.dataTransfer.files.length) onFiles([...e.dataTransfer.files]); }}>
      <div style={{ fontSize: 28 }}>📄</div>
      <div style={{ fontWeight: 600 }}>{label}</div>
      {hint && <div className="muted small">{hint}</div>}
      <input ref={ref} type="file" hidden accept={accept} multiple={multiple} onChange={(e) => { if (e.target.files.length) onFiles([...e.target.files]); e.target.value = ''; }} />
    </div>
  );
}

export const Badge = ({ color = 'gray', children, title }) => <span className={`badge ${color}`} title={title}>{children}</span>;
export const Dot = ({ color = 'gray', title }) => <span className={`dot ${color}`} title={title} />;

export function Field({ label, children, style }) {
  return <label className="f" style={style}><span>{label}</span>{children}</label>;
}

export function Tabs({ tabs, value, onChange }) {
  return <div className="tabs">{tabs.map((t) => <button key={t.id} className={value === t.id ? 'on' : ''} onClick={() => onChange(t.id)}>{t.label}{t.count ? <span className="badge gray" style={{ marginLeft: 6 }}>{t.count}</span> : null}</button>)}</div>;
}

export function Seg({ options, value, onChange }) {
  return <div className="seg">{options.map((o) => <button key={o.value} className={value === o.value ? 'on' : ''} onClick={() => onChange(o.value)} type="button">{o.label}</button>)}</div>;
}

/** Zahlenfeld, das erst bei Blur/Enter speichert (Autosave ohne Zwischenstände). */
export function NumInput({ value, onCommit, className = 'cell', placeholder, step = 'any', disabled, title }) {
  const show = (x) => (x === null || x === undefined ? '' : String(x).replace('.', ','));
  const [v, setV] = useState(show(value));
  useEffect(() => { setV(show(value)); }, [value]);
  const commit = () => {
    const s = String(v).replace(',', '.').trim();
    const n = s === '' ? null : Number(s);
    if (s !== '' && !Number.isFinite(n)) { setV(show(value)); return; }
    if (n !== (value ?? null)) onCommit(n);
  };
  return <input className={className} inputMode="decimal" value={v} placeholder={placeholder} step={step} disabled={disabled} title={title}
    onChange={(e) => setV(e.target.value)} onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); if (e.key === 'Escape') { setV(show(value)); } }} onClick={(e) => e.stopPropagation()} />;
}
export function TextInput({ value, onCommit, multiline, ...rest }) {
  const [v, setV] = useState(value ?? '');
  useEffect(() => { setV(value ?? ''); }, [value]);
  const P = multiline ? 'textarea' : 'input';
  return <P value={v} onChange={(e) => setV(e.target.value)} onBlur={() => v !== (value ?? '') && onCommit(v)} onKeyDown={(e) => { if (!multiline && e.key === 'Enter') e.currentTarget.blur(); }} {...rest} />;
}

export async function copyText(text) {
  try { await navigator.clipboard.writeText(text); return true; } catch {
    const ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select();
    const ok = document.execCommand('copy'); ta.remove(); return ok;
  }
}

export const Empty = ({ children }) => <div className="empty">{children}</div>;
