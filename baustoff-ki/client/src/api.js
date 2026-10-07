// API-Client mit Benutzerkennung, Fehlerbehandlung und Speicherstatus (Autosave-Anzeige)
const listeners = new Set();
let pending = 0;
export const saveState = { status: 'idle', at: null };
const emit = () => listeners.forEach((l) => l({ ...saveState }));
export const onSaveState = (fn) => { listeners.add(fn); return () => listeners.delete(fn); };

export const getUserId = () => { try { return localStorage.getItem('userId') || ''; } catch { return ''; } };

async function request(method, url, body, { isForm = false, track = method !== 'GET' } = {}) {
  if (track) { pending++; saveState.status = 'saving'; emit(); }
  try {
    const res = await fetch(`/api${url}`, {
      method,
      headers: { ...(isForm || body === undefined ? {} : { 'Content-Type': 'application/json' }), 'x-user-id': getUserId() },
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    });
    const ct = res.headers.get('content-type') || '';
    const data = ct.includes('application/json') ? await res.json() : await res.text();
    if (!res.ok) throw new Error(data?.error || `Fehler ${res.status}`);
    if (track) { saveState.status = 'saved'; saveState.at = new Date(); }
    return data;
  } catch (e) {
    if (track) saveState.status = 'error';
    throw e;
  } finally {
    if (track) { pending--; if (pending > 0) saveState.status = 'saving'; emit(); }
  }
}

export const api = {
  get: (u) => request('GET', u),
  post: (u, b = {}) => request('POST', u, b),
  put: (u, b) => request('PUT', u, b),
  patch: (u, b) => request('PATCH', u, b),
  del: (u) => request('DELETE', u),
  upload: (u, files, fields = {}, name = 'file') => {
    const fd = new FormData();
    for (const [k, v] of Object.entries(fields)) if (v !== undefined && v !== null) fd.append(k, v);
    for (const f of Array.isArray(files) ? files : [files]) fd.append(name, f);
    return request('POST', u, fd, { isForm: true });
  },
};

/** Download über Fetch (mit Benutzer-Header), Dateiname aus Content-Disposition. */
export async function download(url, fallbackName = 'download') {
  const res = await fetch(`/api${url}`, { headers: { 'x-user-id': getUserId() } });
  if (!res.ok) { let msg = `Fehler ${res.status}`; try { msg = (await res.json()).error || msg; } catch { /* */ } throw new Error(msg); }
  const cd = res.headers.get('content-disposition') || '';
  const m = cd.match(/filename\*=UTF-8''([^;]+)/);
  const name = m ? decodeURIComponent(m[1]) : fallbackName;
  const blob = await res.blob();
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = name;
  document.body.appendChild(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
  return name;
}
export const docUrl = (id, inline = true) => `/api/documents/${id}/download${inline ? '?inline=1' : ''}`;
