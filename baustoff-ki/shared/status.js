// Status-Definitionen (Projekt-Pipeline, Lieferantenanfragen, Positionen)
export const PROJECT_STATUS = [
  { id: 'neu', label: 'Neu', color: 'blue' },
  { id: 'lv_analysiert', label: 'LV analysiert', color: 'blue' },
  { id: 'anfrage_laeuft', label: 'Anfrage läuft', color: 'yellow' },
  { id: 'preise_vollstaendig', label: 'Preise vollständig', color: 'blue' },
  { id: 'kalkulation', label: 'Kalkulation', color: 'blue' },
  { id: 'pruefung', label: 'Prüfung', color: 'yellow' },
  { id: 'angebot_fertig', label: 'Angebot fertig', color: 'green' },
  { id: 'versendet', label: 'Versendet', color: 'green' },
  { id: 'nachfassen', label: 'Nachfassen', color: 'yellow' },
  { id: 'auftrag', label: 'Auftrag', color: 'green' },
  { id: 'verloren', label: 'Verloren', color: 'red' },
];
export const STATUS_ORDER = Object.fromEntries(PROJECT_STATUS.map((s, i) => [s.id, i]));
export const statusLabel = (id) => PROJECT_STATUS.find((s) => s.id === id)?.label || id;
export const FINAL_STATUS = ['auftrag', 'verloren'];
// Nach so vielen Tagen ohne Statuswechsel gilt ein Projekt als "hängt"
export const STUCK_DAYS = { neu: 1, lv_analysiert: 1, anfrage_laeuft: 3, preise_vollstaendig: 1, kalkulation: 2, pruefung: 1, angebot_fertig: 1, versendet: 10, nachfassen: 3 };

export const REQUEST_STATUS = [
  { id: 'vorbereitet', label: 'Anfrage vorbereitet', color: 'blue' },
  { id: 'angefragt', label: 'angefragt', color: 'yellow' },
  { id: 'ausstehend', label: 'Antwort ausstehend', color: 'yellow' },
  { id: 'preis_erhalten', label: 'Preis erhalten', color: 'green' },
  { id: 'rueckfrage', label: 'Rückfrage erforderlich', color: 'red' },
  { id: 'vollstaendig', label: 'vollständig', color: 'green' },
];
export const requestStatusLabel = (id) => REQUEST_STATUS.find((s) => s.id === id)?.label || 'noch nicht angefragt';

export const FOLLOWUP_RESULTS = [
  { id: 'auftrag', label: 'Auftrag erhalten' },
  { id: 'offen', label: 'noch offen' },
  { id: 'verloren', label: 'verloren' },
  { id: 'erneut', label: 'erneut kontaktieren' },
];

export const DOC_CATEGORIES = {
  lv_original: 'Original-LV', plan: 'Plan', anfrage: 'Lieferantenanfrage', lieferantenangebot: 'Lieferantenangebot', kalkulation: 'Kalkulation',
  deckblatt: 'Deckblatt', angebot: 'Angebot', anhang: 'Anhang', korrespondenz: 'Korrespondenz', sonstiges: 'Sonstiges',
};

/** Abgeleiteter Positionsstatus für die Ampel (wird nicht gespeichert). */
export function positionState(p, calc, requested) {
  if (p.pos_type === 'T') return { id: 'text', label: 'Text', color: 'gray' };
  const openIssues = (p.flags || []).filter((f) => !f.resolved && f.severity !== 'info');
  if (openIssues.length) return { id: 'pruefen', label: 'Prüfung erforderlich', color: 'red' };
  if (calc?.below_min) return { id: 'marge', label: 'Marge unter Mindestwert', color: 'red' };
  if (calc?.vk !== null && calc?.vk !== undefined) return { id: 'kalkuliert', label: 'kalkuliert', color: 'green' };
  if (p.ek !== null && p.ek !== undefined) return { id: 'preis', label: 'Preis da, Aufschlag fehlt', color: 'blue' };
  if (requested) return { id: 'angefragt', label: 'angefragt', color: 'blue' };
  return { id: 'offen', label: 'Preis fehlt', color: 'yellow' };
}
