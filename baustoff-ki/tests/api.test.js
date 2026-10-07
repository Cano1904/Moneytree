// End-to-End über die REST-API: LV-Upload → Lieferanten → Anfrage → Angebotsimport → Kalkulation → Angebots-PDF → Versand → Nachfassen
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'bki-'));
process.env.DATA_DIR = tmp;
delete process.env.ANTHROPIC_API_KEY;
const { app } = await import('../server/index.js');
const SAMPLES = path.resolve(import.meta.dirname, '..', 'samples');
let server, base;

before(async () => { server = app.listen(0); await new Promise((r) => server.once('listening', r)); base = `http://127.0.0.1:${server.address().port}/api`; });
after(() => { server.close(); fs.rmSync(tmp, { recursive: true, force: true }); });

const call = async (method, url, body) => {
  const res = await fetch(base + url, { method, headers: body instanceof FormData || !body ? {} : { 'content-type': 'application/json' }, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined });
  const ct = res.headers.get('content-type') || '';
  const data = ct.includes('json') ? await res.json() : Buffer.from(await res.arrayBuffer());
  if (!res.ok) throw new Error(`${method} ${url} → ${res.status} ${data.error || ''}`);
  return data;
};
const upload = (url, file, fields = {}) => {
  const fd = new FormData();
  fd.append('file', new Blob([fs.readFileSync(path.join(SAMPLES, file))]), file);
  for (const [k, v] of Object.entries(fields)) fd.append(k, v);
  return call('POST', url, fd);
};

test('kompletter Angebotsprozess', async () => {
  const s1 = await call('POST', '/suppliers', { name: 'Betonwerk', groups: ['Pflaster', 'Bordsteine'], email: 'a@b.de', preferred: 1 });
  await call('POST', '/suppliers', { name: 'Rohrhandel', groups: ['Rohre', 'Rinnen', 'Schächte', 'Entwässerung'] });
  await call('POST', '/suppliers', { name: 'Kieswerk', groups: ['Schüttgüter'] });
  const p = await call('POST', '/projects', { name: 'Testprojekt', customer_name: 'Kunde Müller', contact_email: 'k@m.de' });

  const up = await upload(`/projects/${p.id}/lvs/upload`, 'Beispiel-LV_Aussenanlagen.pdf', { useAi: 'false' });
  assert.equal(up.count, 12);
  assert.equal(up.detected.mode, 'galabau');
  const proj = await call('GET', `/projects/${p.id}`);
  assert.equal(proj.status, 'lv_analysiert');
  assert.equal(proj.documents[0].category, 'lv_original');

  const sug = await call('POST', `/lvs/${up.lvId}/suggest-suppliers`, { apply: true });
  assert.ok(sug.suggestions.length >= 8);
  const prep = await call('POST', `/projects/${p.id}/requests/prepare-all`);
  assert.ok(prep.requests.length >= 2);
  const req = await call('GET', `/requests/${prep.requests[0]}`);
  assert.match(req.body, /Bitte nennen Sie uns/);
  await call('PATCH', `/requests/${req.id}`, { status: 'angefragt' });
  assert.equal((await call('GET', `/projects/${p.id}`)).status, 'anfrage_laeuft');

  const draft = await upload(`/projects/${p.id}/quotes/analyze`, 'Beispiel-Lieferantenangebot.pdf', { request_id: String(req.id), useAi: 'false' });
  assert.equal(draft.items.length, 3);
  const q = await call('POST', `/projects/${p.id}/quotes`, { supplier_id: s1.id, request_id: req.id, document_id: draft.document_id, freight_total: draft.freight_total, distribute_freight: true, items: draft.items.map((i) => ({ ...i, apply: true })) });
  assert.equal(q.applied, 3);

  let lv = await call('GET', `/lvs/${up.lvId}`);
  for (const pos of lv.positions.filter((x) => x.ek === null)) await call('PATCH', `/positions/${pos.id}`, { ek: 5, qty: pos.qty ?? 2 });
  const mk = await call('POST', `/lvs/${up.lvId}/markup`, { markup_pct: 12 });
  assert.equal(mk.changed.length, 12);
  const ids = lv.positions.slice(0, 3).map((x) => x.id);
  const bulk = await call('POST', `/lvs/${up.lvId}/bulk`, { ids, set: { markup_pct: 20 } });
  assert.deepEqual(bulk.changed.sort(), ids.sort());
  const mk2 = await call('POST', `/lvs/${up.lvId}/markup`, { markup_pct: 9 });
  assert.equal(mk2.skipped, 3, 'manuell gesetzte Aufschläge bleiben unverändert');
  lv = await call('GET', `/lvs/${up.lvId}`);
  assert.equal(lv.totals.missing, 0);
  assert.equal(lv.positions.find((x) => x.id === ids[0]).markup_pct, 20);

  const prev = await call('GET', `/projects/${p.id}/offer-preview?lv_id=${up.lvId}`);
  assert.ok(prev.lvVariants.some((v) => v.type === 'lv_overlay'));
  const off = await call('POST', `/projects/${p.id}/offers`, { lv_id: up.lvId, parts: [{ type: 'cover_generated' }, { type: 'lv_overlay' }, { type: 'lv_generated' }] });
  const pdf = await call('GET', `/documents/${off.document_id}/download`);
  assert.equal(pdf.subarray(0, 4).toString(), '%PDF');
  const eml = (await call('GET', `/offers/${off.id}/eml`)).toString();
  assert.match(eml, /application\/pdf/);
  await call('POST', `/offers/${off.id}/sent`, { followup_days: 7 });
  const fus = await call('GET', '/followups?scope=open');
  assert.equal(fus.length, 1);
  await call('PATCH', `/followups/${fus[0].id}`, { result: 'auftrag', note: 'Zuschlag erhalten' });
  assert.equal((await call('GET', `/projects/${p.id}`)).status, 'auftrag');

  const search = await call('GET', `/search?q=${encodeURIComponent('alle LVs von Kunde Müller')}`);
  assert.equal(search.sections[0].rows[0].id, p.id);
  const prices = await call('GET', '/search?q=Objektpreise%20f%C3%BCr%20Rechteckpflaster');
  assert.ok(prices.sections.some((s) => s.key === 'prices' && s.rows.length));
  const audit = await call('GET', `/projects/${p.id}/audit`);
  assert.ok(audit.some((a) => a.action === 'Massenänderung'));
});

test('Excel/GAEB-Upload, Exporte und geschützte Originale', async () => {
  const p = await call('POST', '/projects', { name: 'Rohbau', mode: 'hochbau' });
  const x = await upload(`/projects/${p.id}/lvs/upload`, 'Beispiel-LV_Rohbau.xlsx');
  assert.equal(x.count, 8);
  const g = await upload(`/projects/${p.id}/lvs/upload`, 'Beispiel-LV_Aussenanlagen.x83');
  assert.equal(g.detected.mode, 'galabau');
  const xlsx = await call('GET', `/lvs/${x.lvId}/export/xlsx`);
  assert.equal(xlsx.subarray(0, 2).toString(), 'PK');
  const x84 = (await call('GET', `/lvs/${g.lvId}/export/gaeb`)).toString();
  assert.match(x84, /<DP>84<\/DP>/);
  const docs = (await call('GET', `/projects/${p.id}`)).documents;
  await assert.rejects(call('DELETE', `/documents/${docs[0].id}`), /400/);
  const mat = await call('POST', `/projects/${p.id}/material-lv`, { items: [{ material: 'Kalksandstein 12DF', qty: 210, unit: 'St', basis: 'Rechner', certainty: 'berechnet' }] });
  const lv = await call('GET', `/lvs/${mat.lvId}`);
  assert.equal(lv.positions[0].group_name, 'Kalksandstein');
});
