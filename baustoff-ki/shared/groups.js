// Arbeitsmodi, Warengruppen und regelbasierte Klassifizierung (Fallback ohne KI)
import { slug } from './format.js';

export const MODES = {
  galabau: { label: 'Tief- & GaLa-Bau', short: 'GaLa' },
  hochbau: { label: 'Hochbau', short: 'Hochbau' },
};

// kw: Schlüsselwörter (klein, ohne Umlaut-Normalisierung, Teilwort-Treffer)
export const GROUPS = [
  { name: 'Pflaster', mode: 'galabau', kw: ['pflaster', 'verbundstein', 'rechteckpflaster', 'rasengitter', 'okopflaster', 'drainpflaster', 'sickerpflaster', 'pflasterstein', 'pflasterdecke', 'pflasterbelag'] },
  { name: 'Platten', mode: 'galabau', kw: ['gehwegplatte', 'betonplatte', 'natursteinplatte', 'feinsteinzeug', 'plattenbelag', 'keramikplatte', 'platten '] },
  { name: 'Bordsteine', mode: 'galabau', kw: ['bordstein', 'hochbord', 'tiefbord', 'rundbord', 'rasenbord', 'rasenkante', 'kantenstein', 'randstein', 'leistenstein', 'einfassung', 'bordanlage', 'flachbord'] },
  { name: 'Entwässerung', mode: 'galabau', kw: ['entwasserung', 'strassenablauf', 'hofablauf', 'sinkkasten', 'aufsatz', 'versickerung', 'rigole', 'sickerschacht', 'drainage', 'dranage', 'dranrohr', 'sickerrohr', 'regenwasser', 'zisterne'] },
  { name: 'Kanal/Tiefbau', mode: 'galabau', kw: ['kanal', 'leitungsgraben', 'hausanschluss', 'leitungszone', 'tiefbau', 'baugrube', 'grabenverbau'] },
  { name: 'Schächte', mode: 'galabau', kw: ['schacht', 'schachtring', 'schachthals', 'konus', 'schachtabdeckung', 'schachtunterteil', 'auflagering', 'ausgleichsring', 'kontrollschacht'] },
  { name: 'Rohre', mode: 'galabau', kw: ['rohr', 'kg 2000', 'kg2000', 'pp rohr', 'pvc', 'steinzeug', 'formstuck', 'abzweig', 'kgea', 'kgb ', 'kgm ', 'kgu ', 'muffe', 'rohrbogen', 'pe hd', 'pehd'] },
  { name: 'Rinnen', mode: 'galabau', kw: ['entwasserungsrinne', 'rinne', 'schlitzrinne', 'kastenrinne', 'muldenrinne', 'pendelrinne', 'rinnenstein', 'stirnwand', 'einlaufkasten', 'gitterrost', 'stegrost', 'maschenrost'] },
  { name: 'Schüttgüter', mode: 'both', kw: ['schotter', 'splitt', 'kies', 'sand', 'frostschutz', 'tragschicht', 'mineralgemisch', 'brechsand', 'oberboden', 'mutterboden', 'fullboden', 'recycling', 'rc material', 'schuttgut', 'bettung', 'brechkorn', 'gesteinskorn', 'mineralbeton'] },
  { name: 'Mauersysteme', mode: 'galabau', kw: ['mauerstein', 'gartenmauer', 'trockenmauer', 'palisade', 'gabione', 'blockstufe', 'mauerabdeckung', 'winkelstufe', 'pflanzring', 'boschungsstein', 'mauerscheibe', 'sichtschutzmauer'] },
  { name: 'Stützwinkel', mode: 'galabau', kw: ['stutzwinkel', 'winkelstutz', 'l stein', 'l-stein', 'winkelstein', 'stutzwand'] },
  { name: 'Zement/Mörtel', mode: 'both', kw: ['zement', 'mortel', 'trassmortel', 'drainmortel', 'haftschlamme', 'vergussmortel', 'fundamentbeton', 'ruckenstutze', 'magerbeton', 'beton c', 'erdfeucht'] },
  { name: 'Fugenmaterial', mode: 'galabau', kw: ['fugenmaterial', 'fugenmortel', 'fugensand', 'fugensplitt', 'fugenfuller', 'verfugung', 'pflasterfugenmortel', 'fugen mit'] },
  { name: 'Terrassen', mode: 'galabau', kw: ['terrasse', 'terrassenplatte', 'wpc', 'terrassendiele', 'unterkonstruktion', 'balkonbelag', 'balkon'] },
  { name: 'Stelzlager', mode: 'galabau', kw: ['stelzlager', 'plattenlager', 'terrassenlager', 'stellfuss', 'fugenkreuz', 'lagerpad', 'gummigranulat', 'stellfuß'] },
  { name: 'Sonderbauteile', mode: 'galabau', kw: ['sonderanfertigung', 'formstein', 'sonderformat', 'blockstufen', 'treppenanlage', 'podest', 'blindenleit', 'rippenplatte', 'noppenplatte', 'taktil', 'leitsystem'] },
  { name: 'Stadtmobiliar', mode: 'galabau', kw: ['sitzbank', 'parkbank', 'abfallbehalter', 'papierkorb', 'poller', 'pflanzkubel', 'baumscheibe', 'baumrost', 'pergola', 'spielgerat', 'stadtmobiliar'] },
  { name: 'Fahrradständer', mode: 'galabau', kw: ['fahrradstander', 'fahrradbugel', 'anlehnbugel', 'fahrradparker', 'radbugel', 'fahrradabstell'] },
  { name: 'Mauerwerk', mode: 'hochbau', kw: ['mauerwerk', 'ziegel', 'hlz', 'porenbeton', 'planstein', 'hochlochziegel', 'mauerziegel', 'innenwand', 'aussenwand', 'schornstein', 'mauern', 'mauerwerkswand'] },
  { name: 'Kalksandstein', mode: 'hochbau', kw: ['kalksandstein', 'ks plan', 'ks l', 'ks xl', 'ks r', 'kalksand', 'kimmstein', '12df', '2df', '3df', '8df', '16df', '20df', ' ks '] },
  { name: 'Beton', mode: 'hochbau', kw: ['c20 25', 'c25 30', 'c30 37', 'c35 45', 'transportbeton', 'ortbeton', 'stahlbeton', 'bodenplatte', 'streifenfundament', 'sichtbeton', 'betondecke', 'fertigteildecke', 'filigrandecke', 'beton'] },
  { name: 'Bewehrung', mode: 'hochbau', kw: ['bewehrung', 'betonstahlmatte', 'betonstahl', 'q188', 'q257', 'q335', 'q424', 'q524', 'r188', 'r257', 'r335', 'b500', 'stabstahl', 'bugel', 'bewehrungskorb', 'lagermatte', 'listenmatte'] },
  { name: 'Stahl', mode: 'hochbau', kw: ['stahltrager', 'heb ', 'hea ', 'ipe ', 'u profil', 'stahlprofil', 'winkelstahl', 'flachstahl', 'stahlbau', 'stahlstutze'] },
  { name: 'Dämmung', mode: 'hochbau', kw: ['dammung', 'dammplatte', 'xps', 'eps', 'perimeter', 'mineralwolle', 'glaswolle', 'steinwolle', 'pur ', 'pir ', 'wdvs', 'trittschall', 'warmedamm'] },
  { name: 'Baustoffe', mode: 'hochbau', kw: ['abdichtung', 'bitumen', 'dichtschlamme', 'putz', 'gipskarton', 'trockenbau', 'sperrbahn', 'schweissbahn', 'estrich', 'folie', 'kellerabdichtung'] },
  { name: 'Zubehör', mode: 'hochbau', kw: ['zubehor', 'maueranker', 'dubel', 'schalung', 'abstandhalter', 'fugenband', 'quellband', 'kleber', 'rodeldraht', 'sturz', 'luftschichtanker'] },
];

export const GROUP_NAMES = GROUPS.map((g) => g.name);
export const groupsForMode = (mode) => GROUPS.filter((g) => g.mode === mode || g.mode === 'both').map((g) => g.name);
export const groupMode = (name) => { const m = GROUPS.find((g) => g.name === name)?.mode; return m && m !== 'both' ? m : null; };

/** Klassifiziert eine Position (Kurztext zählt dreifach) in eine Warengruppe. Liefert {group, mode, confidence} oder group=null. */
export function classify(text, preferredMode = 'galabau', longText = '') {
  const parts = [[` ${slug(text)} `, 3], [` ${slug(longText)} `, 1]];
  let best = null;
  for (const g of GROUPS) {
    let score = 0;
    for (const k of g.kw) {
      const kk = slug(k);
      if (!kk) continue;
      const needle = k.startsWith(' ') || k.endsWith(' ') ? ` ${kk} ` : kk;
      for (const [s, w] of parts) if (s.includes(needle)) score += w * (1 + kk.length / 8);
    }
    if (score > 0 && (g.mode === preferredMode || g.mode === 'both')) score *= 1.15;
    if (score > 0 && (!best || score > best.score)) best = { group: g.name, mode: g.mode, score };
  }
  if (!best) return { group: null, mode: null, confidence: 0 };
  return { group: best.group, mode: best.mode, confidence: Math.min(0.9, 0.4 + best.score / 12) };
}

/** Schätzt den Arbeitsbereich (GaLa/Hochbau) anhand vieler Positionstexte. Überschreibt nie den Benutzermodus. */
export function detectMode(texts) {
  const sc = { galabau: 0, hochbau: 0 };
  for (const t of texts) {
    const c = classify(t, null);
    if (c.mode && c.mode !== 'both') sc[c.mode] += c.confidence;
  }
  const total = sc.galabau + sc.hochbau;
  if (!total) return { mode: null, confidence: 0, scores: sc };
  const mode = sc.galabau >= sc.hochbau ? 'galabau' : 'hochbau';
  return { mode, confidence: Math.round((sc[mode] / total) * 100) / 100, scores: sc };
}

// Typisches Zubehör: wenn Gruppe A vorkommt, aber keine der Gruppen/Begriffe B, Hinweis erzeugen
export const ACCESSORY_RULES = [
  { if: 'Pflaster', needAny: ['Schüttgüter', 'Fugenmaterial'], msg: 'Pflaster ohne Bettungs- oder Fugenmaterial-Position' },
  { if: 'Platten', needAny: ['Schüttgüter', 'Fugenmaterial', 'Stelzlager', 'Zement/Mörtel'], msg: 'Platten ohne Bettung/Stelzlager/Fugenmaterial' },
  { if: 'Terrassen', needAny: ['Stelzlager', 'Schüttgüter', 'Fugenmaterial'], msg: 'Terrassenbelag ohne Unterbau (Stelzlager/Bettung)' },
  { if: 'Bordsteine', needAny: ['Zement/Mörtel', 'Beton'], msg: 'Bordsteine ohne Beton für Fundament/Rückenstütze' },
  { if: 'Rinnen', needText: ['stirnwand', 'einlaufkasten'], msg: 'Rinnen ohne Stirnwände/Einlaufkasten' },
  { if: 'Rohre', needText: ['bogen', 'abzweig', 'formst'], msg: 'Rohre ohne Formteile (Bögen/Abzweige)' },
  { if: 'Schächte', needText: ['abdeckung', 'schachtabdeckung'], msg: 'Schacht ohne Schachtabdeckung' },
  { if: 'Kalksandstein', needAny: ['Zement/Mörtel'], needText: ['mortel', 'dunnbett', 'kleber'], msg: 'Kalksandstein ohne Mörtel/Dünnbettmörtel' },
  { if: 'Mauerwerk', needText: ['mortel', 'dunnbett', 'kleber'], msg: 'Mauerwerk ohne Mörtel' },
  { if: 'Beton', needAny: ['Bewehrung'], msg: 'Beton ohne Bewehrungsposition (falls Stahlbeton)' },
  { if: 'Bewehrung', needText: ['abstandhalter'], msg: 'Bewehrung ohne Abstandhalter' },
];
