// Claude-Anbindung. Ohne API-Key arbeitet die App vollständig mit Heuristiken + Human-in-the-Loop weiter.
import Anthropic from '@anthropic-ai/sdk';
import { getSettings } from '../db.js';

let cached = { key: null, client: null };

export function aiStatus() {
  const s = getSettings();
  const key = process.env.ANTHROPIC_API_KEY || s.ai_api_key;
  return { enabled: Boolean(key) && s.ai_enabled !== false, model: process.env.ANTHROPIC_MODEL || s.ai_model || 'claude-opus-5-5', effort: s.ai_effort || 'medium', source: process.env.ANTHROPIC_API_KEY ? 'env' : s.ai_api_key ? 'einstellungen' : null };
}

function client() {
  const s = getSettings();
  const key = process.env.ANTHROPIC_API_KEY || s.ai_api_key;
  if (!key) return null;
  if (cached.key !== key) cached = { key, client: new Anthropic({ apiKey: key, maxRetries: 2 }) };
  return cached.client;
}

export class AiError extends Error {}

const SYSTEM_BASE = `Du bist ein Fachassistent für den deutschen Baustoffhandel (Tiefbau, Garten- und Landschaftsbau, Hochbau).
Grundregeln:
- Erfinde niemals Maße, Mengen, Produkte oder Preise. Was nicht eindeutig im Dokument steht, ist null bzw. wird als unsicher markiert.
- Übernimm Positionsnummern, Mengen und Einheiten exakt wie im Dokument.
- Markiere Unsicherheiten ausdrücklich in den dafür vorgesehenen Feldern.
- Antworte ausschließlich im geforderten JSON-Format.`;

/**
 * Strukturierte Abfrage. content: Array von Content-Blöcken (Text, Dokument, Bild).
 * Liefert das geparste JSON-Objekt oder wirft AiError.
 */
export async function aiJson({ content, schema, instructions, maxTokens = 32000 }) {
  const c = client();
  const st = aiStatus();
  if (!c || !st.enabled) throw new AiError('KI nicht konfiguriert (API-Key in Einstellungen hinterlegen)');
  const params = {
    model: st.model,
    max_tokens: maxTokens,
    system: `${SYSTEM_BASE}\n\n${instructions}`,
    messages: [{ role: 'user', content }],
    output_config: { effort: st.effort, format: { type: 'json_schema', schema } },
  };
  let msg;
  try {
    msg = await c.beta.messages.stream({ ...params, betas: ['server-side-fallback-2026-07-01'], fallbacks: 'default' }).finalMessage();
  } catch (e) {
    if (e instanceof Anthropic.BadRequestError && /fallback/i.test(e.message)) msg = await c.messages.stream(params).finalMessage();
    else if (e instanceof Anthropic.AuthenticationError) throw new AiError('KI-API-Key ungültig');
    else if (e instanceof Anthropic.RateLimitError) throw new AiError('KI-Ratenlimit erreicht – bitte später erneut versuchen');
    else if (e instanceof Anthropic.APIError) throw new AiError(`KI-Fehler: ${e.message}`);
    else throw new AiError(`KI nicht erreichbar: ${e.message}`);
  }
  if (msg.stop_reason === 'refusal') throw new AiError('KI hat die Anfrage abgelehnt');
  if (msg.stop_reason === 'max_tokens') throw new AiError('KI-Antwort zu lang (Dokument in kleinere Teile aufteilen)');
  const text = msg.content.filter((b) => b.type === 'text').map((b) => b.text).join('');
  try {
    return JSON.parse(text);
  } catch {
    throw new AiError('KI-Antwort war kein gültiges JSON');
  }
}

/** Freitext-Antwort (z. B. Wissensdatenbank-Fragen). */
export async function aiText({ prompt, instructions, maxTokens = 4000 }) {
  const c = client();
  const st = aiStatus();
  if (!c || !st.enabled) throw new AiError('KI nicht konfiguriert');
  try {
    const msg = await c.messages.create({ model: st.model, max_tokens: maxTokens, system: `${SYSTEM_BASE.replace('- Antworte ausschließlich im geforderten JSON-Format.', '')}\n${instructions}`, messages: [{ role: 'user', content: prompt }], output_config: { effort: 'low' } });
    if (msg.stop_reason === 'refusal') throw new AiError('KI hat die Anfrage abgelehnt');
    return msg.content.filter((b) => b.type === 'text').map((b) => b.text).join('');
  } catch (e) {
    if (e instanceof AiError) throw e;
    throw new AiError(`KI-Fehler: ${e.message}`);
  }
}

export const fileBlock = (buf, mime) => {
  const data = buf.toString('base64');
  if (mime === 'application/pdf') return { type: 'document', source: { type: 'base64', media_type: 'application/pdf', data } };
  if (/^image\/(png|jpeg|gif|webp)$/.test(mime)) return { type: 'image', source: { type: 'base64', media_type: mime, data } };
  return null;
};

// Nullable-Helfer für JSON-Schemas
export const N = (type) => ({ anyOf: [{ type }, { type: 'null' }] });
export const obj = (properties) => ({ type: 'object', properties, required: Object.keys(properties), additionalProperties: false });
export const arr = (items) => ({ type: 'array', items });
