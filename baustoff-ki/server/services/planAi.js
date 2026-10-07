// Plan-KI: liest Maße/Bauteile aus Plänen. Rechnen tun ausschließlich die deterministischen Rechner.
import { aiJson, fileBlock, N, obj, arr } from './ai.js';
import { CALCULATORS } from '../../shared/calculators.js';

const calcIds = CALCULATORS.map((c) => c.id);
const SCHEMA = obj({
  plan_type: { type: 'string' },
  scale: N('string'),
  mode_guess: { type: 'string', enum: ['galabau', 'hochbau', 'unklar'] },
  measurements: arr(obj({ label: { type: 'string' }, kind: { type: 'string', enum: ['flaeche', 'laenge', 'anzahl', 'hoehe', 'dicke', 'volumen', 'sonstiges'] }, value: N('number'), unit: N('string'), source: { type: 'string' }, confidence: { type: 'number' } })),
  components: arr(obj({ name: { type: 'string' }, details: { type: 'string' } })),
  suggestions: arr(obj({ calculator: { type: 'string', enum: calcIds }, reason: { type: 'string' }, inputs: arr(obj({ key: { type: 'string' }, value: N('number'), source: { type: 'string' } })) })),
  questions: arr({ type: 'string' }),
});

const calcHelp = CALCULATORS.map((c) => `- ${c.id} (${c.name}): ${c.fields.filter((f) => f.type !== 'rects' && f.type !== 'select' && f.type !== 'checkbox').map((f) => `${f.key}${f.unit ? ` [${f.unit}]` : ''}`).join(', ')}${c.id === 'terrasse' ? ', rect_w [m], rect_d [m] (rechteckige Fläche)' : ''}`).join('\n');

const INSTR = `Aufgabe: Analysiere einen Bauplan (Grundriss, Detail-, Terrassen-, Bewehrungs- oder Lageplan) für die Materialermittlung im Baustoffhandel.
- measurements: nur Werte, die im Plan ablesbar sind (Bemaßung, Beschriftung, Flächenangabe). source beschreibt genau wo (z. B. "Bemaßung unten 4,50 m").
- Werte, die nur geschätzt werden könnten: value = null und eine gezielte Rückfrage in questions.
- Keine Maße aus Pixeln schätzen. Maßstab nur angeben, wenn er im Plan steht.
- suggestions: welche Rechner sinnvoll sind und mit welchen abgelesenen Eingaben (key aus der Liste, value mit Einheit wie angegeben). Fehlende Eingaben weglassen.
- questions: konkrete Rückfragen, die für eine eindeutige Mengenermittlung fehlen (Format, Aufbauhöhe, Schichtdicken, Steinformat …).
Verfügbare Rechner und Eingaben:
${calcHelp}`;

export async function analyzePlan({ buf, mime, hint }) {
  const blk = fileBlock(buf, mime);
  if (!blk) throw new Error('Plananalyse per KI nur für PDF, PNG, JPG');
  return aiJson({ content: [blk, { type: 'text', text: `Plan analysieren.${hint ? ` Hinweis des Benutzers: ${hint}` : ''}` }], schema: SCHEMA, instructions: INSTR, maxTokens: 16000 });
}
