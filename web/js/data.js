// SHORT LEGS – map, texts (DE/EN) and case content.
(function (global) {
  "use strict";
  const { claim, ANY } = SL;

  // ───────────── Texts ─────────────
  const STR = {
    de: {
      story: "STORY-MODUS", intro: "INTRO-FILM", ctrlCam: "Kamera drehen", lobby: "MULTIPLAYER-LOBBY", lab: "SHRINK-LABOR", archive: "BEWEIS-ARCHIV", settings: "EINSTELLUNGEN", quit: "BEENDEN",
      quitTitle: "Spiel beenden?", quitText: "Im Browser kann sich das Spiel nicht selbst schließen. Schließe einfach den Tab.", back: "Zurück", yes: "Ja", no: "Nein",
      paused: "ERMITTLUNG PAUSIERT", resume: "FORTSETZEN", notebook: "NOTIZBUCH ANSEHEN", options: "OPTIONEN", abandon: "FALL AUFGEBEN",
      abandonQ: "Fall wirklich aufgeben? Der Fortschritt dieses Falls geht verloren.",
      lobbyTitle: "Multiplayer-Lobby", lobbyNote: "Online-Partien mit echten Mitspielern und Sprachchat laufen im Unity-Build. Hier spielst du dieselben Regeln gegen Bots: einer von ihnen ist der Lügner.",
      maxLies: "Max. Lügen bis zur Niederlage", bots: "Mitspieler (Bots)", timer: "Rundenzeit", map: "Tatort", voice: "Proximity-Voice", start: "FALL STARTEN", code: "Lobby-Code",
      mapManor: "Das Große Herrenhaus", mapYacht: "Die Gesunkene Yacht (gesperrt)", voiceOnly: "nur Online-Version",
      gfx: "Grafik & Audio", art: "Stilfilter", artComic: "Vibrant Comic", artNoir: "Classic Noir", res: "Render-Auflösung", full: "Vollbild",
      master: "Gesamt", music: "Musik", voiceVol: "Stimmen (VOIP)", sfx: "Mechanik-SFX", access: "Barrierefreiheit & Sprache",
      tts: "Text-to-Speech", stt: "Speech-to-Text", subs: "Untertitel", subSize: "Untertitelgröße", contrast: "Hoher Kontrast", flash: "Blitzen reduzieren", lang: "Sprache",
      on: "An", off: "Aus", apply: "Übernehmen",
      controls: "Steuerung", ctrlMove: "Bewegen", ctrlLook: "Umsehen / Hinweise prüfen", ctrlUse: "Interagieren / Beweis vorlegen", ctrlNote: "Notizbuch", ctrlPtt: "Push-to-Talk", ctrlPause: "Pause", ctrlSprint: "Sprinten", ctrlJump: "Springen", ctrlMeet: "Notfall-Treffen",
      clues: "Hinweise", statements: "Aussagen", legs: "Beinstatus", profiles: "Profile", role: "Rolle", investigator: "ERMITTLER", noClues: "Noch keine Hinweise.", noStatements: "Noch keine Aussagen.",
      interact: "E: untersuchen", talk: "E: befragen", search: "E: durchsuchen",
      present: "Beweis vorlegen", leave: "Gehen", pickStatement: "Aussage", pickClue: "Beweis", noEvidence: "Du hast noch keine Beweise.",
      lie: "LÜGE ERKANNT", shrunk: (n, p) => `${n}s Beine schrumpfen auf ${p} %`, framed: "(reingelegt!)",
      found: (c) => `Hinweis gefunden: ${c}`, wrong: (n) => `Einspruch abgelehnt. Noch ${n} Fehlversuche.`, notLie: "Das widerlegt diese Aussage nicht.",
      meeting: "TREFFEN", finalMeeting: "LETZTES TREFFEN", vote: "Wen beschuldigst du?", skip: "Enthalten", accuse: "Beschuldigen",
      noMajority: "Keine Mehrheit. Die Ermittlung geht weiter.", insufficient: (n) => `${n} kommt frei: zu wenig Beweise (3 echte Hinweise nötig).`,
      framedOut: (n) => `${n} war unschuldig und scheidet aus.`, caught: (n) => `${n} war der Lügner!`,
      win: "FALL GELÖST", lose: "FALL VERLOREN", timeUp: "Die Zeit ist abgelaufen. Der Lügner ist entkommen.", liarExposed: (n) => `${n} hat so oft gelogen, dass keine Beine mehr übrig sind.`,
      wiped: "Zu viele Unschuldige sind ausgeschieden. Der Lügner gewinnt.", replay: "Lügen-Replay", menu: "HAUPTMENÜ", again: "NOCHMAL",
      investigate: "ERMITTELN", alibis: "ALIBIS", lightsOut: "LICHT AUS", clueCount: "HINWEISE", time: "ZEIT", mistakes: "FEHLVERSUCHE",
      labHelp: "L: Lüge · R: Reset · Teste Treppe (Keller), Hecke (Garten), Sprinten und Springen.", labTitle: "Shrink-Labor",
      archiveTitle: "Beweis-Archiv", hats: "Hüte", cases: "Fälle", lore: "Gefundene Hinweise", equip: "Aufsetzen", equipped: "Aufgesetzt", locked: "Gesperrt",
      emergency: "M: Notfall-Treffen", meetingCalled: "Notfall-Treffen einberufen!", saysAt: "sagt", unlocked: (h) => `Freigeschaltet: ${h}`,
      noSprint: "kein Sprint", noStairs: "keine Treppen", crawl: "krabbelt", fine: "ok", ghost: "ausgeschieden",
      stairsBlocked: "Mit so kurzen Beinen kommst du die Treppe nicht mehr hoch oder runter.", hedgeBlocked: "Die Hecke ist zu hoch. Springen (Leertaste)?",
      shoe: "Schuhgröße", coat: "Mantel", wasAt: "Alibi 23:00",
    },
    en: {
      story: "STORY MODE", intro: "INTRO FILM", ctrlCam: "Rotate camera", lobby: "MULTIPLAYER LOBBY", lab: "SHRINK LAB", archive: "EVIDENCE ARCHIVE", settings: "SETTINGS", quit: "QUIT",
      quitTitle: "Quit the game?", quitText: "A browser game cannot close itself. Just close the tab.", back: "Back", yes: "Yes", no: "No",
      paused: "INVESTIGATION PAUSED", resume: "RESUME", notebook: "REVIEW NOTEBOOK", options: "OPTIONS", abandon: "ABANDON CASE",
      abandonQ: "Abandon this case? Progress in this case will be lost.",
      lobbyTitle: "Multiplayer Lobby", lobbyNote: "Online matches with real players and voice chat run in the Unity build. Here you play the same rules against bots: one of them is the Liar.",
      maxLies: "Max lies before defeat", bots: "Players (bots)", timer: "Match time", map: "Crime scene", voice: "Proximity voice", start: "START CASE", code: "Lobby code",
      mapManor: "The Grand Manor", mapYacht: "The Sunken Yacht (locked)", voiceOnly: "online version only",
      gfx: "Graphics & Audio", art: "Art style filter", artComic: "Vibrant Comic", artNoir: "Classic Noir", res: "Render resolution", full: "Fullscreen",
      master: "Master", music: "Music", voiceVol: "Voice (VOIP)", sfx: "Mechanic SFX", access: "Accessibility & Language",
      tts: "Text-to-speech", stt: "Speech-to-text", subs: "Subtitles", subSize: "Subtitle size", contrast: "High contrast", flash: "Reduce flashing", lang: "Language",
      on: "On", off: "Off", apply: "Apply",
      controls: "Controls", ctrlMove: "Move", ctrlLook: "Look / inspect clues", ctrlUse: "Interact / present evidence", ctrlNote: "Notebook", ctrlPtt: "Push-to-talk", ctrlPause: "Pause", ctrlSprint: "Sprint", ctrlJump: "Jump", ctrlMeet: "Emergency meeting",
      clues: "Clues", statements: "Statements", legs: "Leg status", profiles: "Profiles", role: "Role", investigator: "INVESTIGATOR", noClues: "No clues yet.", noStatements: "No statements yet.",
      interact: "E: inspect", talk: "E: question", search: "E: search",
      present: "Present evidence", leave: "Leave", pickStatement: "Statement", pickClue: "Evidence", noEvidence: "You have no evidence yet.",
      lie: "LIE DETECTED", shrunk: (n, p) => `${n}'s legs shrink to ${p}%`, framed: "(framed!)",
      found: (c) => `Clue found: ${c}`, wrong: (n) => `Objection overruled. ${n} mistakes left.`, notLie: "That doesn't contradict this statement.",
      meeting: "MEETING", finalMeeting: "FINAL MEETING", vote: "Who do you accuse?", skip: "Skip", accuse: "Accuse",
      noMajority: "No majority. The investigation continues.", insufficient: (n) => `${n} walks free: not enough evidence (3 genuine clues needed).`,
      framedOut: (n) => `${n} was innocent and is out.`, caught: (n) => `${n} was the Liar!`,
      win: "CASE SOLVED", lose: "CASE LOST", timeUp: "Time is up. The Liar got away.", liarExposed: (n) => `${n} lied until there were no legs left.`,
      wiped: "Too many innocents are out. The Liar wins.", replay: "Lie replay", menu: "MAIN MENU", again: "PLAY AGAIN",
      investigate: "INVESTIGATE", alibis: "ALIBIS", lightsOut: "LIGHTS OUT", clueCount: "CLUES", time: "TIME", mistakes: "MISTAKES",
      labHelp: "L: lie · R: reset · Try the stairs (cellar), the hedge (garden), sprinting and jumping.", labTitle: "Shrink Lab",
      archiveTitle: "Evidence Archive", hats: "Hats", cases: "Cases", lore: "Clues found", equip: "Wear", equipped: "Wearing", locked: "Locked",
      emergency: "M: emergency meeting", meetingCalled: "Emergency meeting called!", saysAt: "says", unlocked: (h) => `Unlocked: ${h}`,
      noSprint: "no sprint", noStairs: "no stairs", crawl: "crawling", fine: "fine", ghost: "out",
      stairsBlocked: "Your legs are too short for the stairs now.", hedgeBlocked: "The hedge is too high. Jump (Space)?",
      shoe: "Shoe size", coat: "Coat", wasAt: "Alibi 23:00",
    },
  };

  // ───────────── Map: The Grand Manor (40×26 tiles) ─────────────
  const W = 40, H = 26;
  const ROOMS = {
    study:      { x: 1,  y: 1,  w: 10, h: 7, de: "Arbeitszimmer", en: "Study",      floor: "#6b4a32" },
    library:    { x: 12, y: 1,  w: 12, h: 7, de: "Bibliothek",    en: "Library",    floor: "#5a3f33" },
    bedroom:    { x: 25, y: 1,  w: 7,  h: 7, de: "Schlafzimmer",  en: "Bedroom",    floor: "#6a5160" },
    kitchen:    { x: 33, y: 1,  w: 6,  h: 7, de: "Küche",         en: "Kitchen",    floor: "#b8b0a0", checker: true },
    hall:       { x: 1,  y: 9,  w: 26, h: 8, de: "Große Halle",   en: "Great Hall", floor: "#8a6a48" },
    greenhouse: { x: 28, y: 9,  w: 11, h: 8, de: "Gewächshaus",   en: "Greenhouse", floor: "#5f7a58" },
    garden:     { x: 1,  y: 18, w: 16, h: 7, de: "Garten",        en: "Garden",     floor: "#dfe9ee", snow: true },
    cellar:     { x: 18, y: 19, w: 9,  h: 6, de: "Keller",        en: "Cellar",     floor: "#3c3f45" },
    shed:       { x: 28, y: 18, w: 11, h: 7, de: "Schuppen",      en: "Shed",       floor: "#7a6a52" },
  };
  const DOORS = [[5, 8], [17, 8], [18, 8], [26, 8], [35, 8], [32, 4], [11, 4], [27, 12], [27, 13], [8, 17], [9, 17], [33, 17]];
  const STAIRS = []; for (let x = 21; x <= 23; x++) for (let y = 17; y <= 18; y++) STAIRS.push([x, y]);
  const HEDGE = []; for (let y = 18; y <= 22; y++) HEDGE.push([11, y]);

  function buildMap() {
    const tiles = [], region = [];
    for (let y = 0; y < H; y++) { tiles.push(Array(W).fill("#")); region.push(Array(W).fill(null)); }
    for (const [id, r] of Object.entries(ROOMS))
      for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) { tiles[y][x] = "."; region[y][x] = id; }
    const near = (x, y) => { for (const [dx, dy] of [[0, 1], [0, -1], [1, 0], [-1, 0]]) { const g = region[y + dy] && region[y + dy][x + dx]; if (g) return g; } return null; };
    for (const [x, y] of DOORS) { tiles[y][x] = "d"; region[y][x] = near(x, y); }
    for (const [x, y] of STAIRS) { tiles[y][x] = "S"; region[y][x] = "cellar"; }
    for (const [x, y] of HEDGE) tiles[y][x] = "h";
    return { W, H, tiles, region };
  }

  // ───────────── Characters ─────────────
  const CAST = [
    { id: "brumm",  name: "Baron Brumm",     coat: "#4c4133", hat: "#3d5a8a", beard: true,  shoe: 46, coatName: { de: "braun", en: "brown" },  voice: 150 },
    { id: "velours",name: "Madame Velours",  coat: "#6a2c4a", hat: "#2b2b2b", beard: false, shoe: 38, coatName: { de: "weinrot", en: "wine red" }, voice: 260 },
    { id: "kip",    name: "Kip",             coat: "#4f6b35", hat: "#8a3b2a", beard: false, shoe: 42, coatName: { de: "grün", en: "green" },  voice: 210 },
    { id: "fenn",   name: "Lady Fenn",       coat: "#2f5b6b", hat: "#c9a227", beard: false, shoe: 39, coatName: { de: "petrol", en: "teal" },  voice: 280 },
    { id: "oddby",  name: "Dr. Oddby",       coat: "#3a3f47", hat: "#1f262e", beard: true,  shoe: 44, coatName: { de: "grau", en: "grey" },   voice: 170 },
    { id: "kruste", name: "Oberst Kruste",   coat: "#7a5a2a", hat: "#4e2a30", beard: true,  shoe: 47, coatName: { de: "ocker", en: "ochre" }, voice: 130 },
    { id: "mimi",   name: "Tante Mimi",      coat: "#8a6a9a", hat: "#e8e1d0", beard: false, shoe: 37, coatName: { de: "lila", en: "lilac" },  voice: 300 },
    { id: "pips",   name: "Mr. Pips",        coat: "#b3262b", hat: "#2d3d4d", beard: false, shoe: 43, coatName: { de: "rot", en: "red" },    voice: 230 },
  ];
  const PLAYER = { id: "you", coat: "#8b7a5a", hat: "#2a2a2a", beard: false, shoe: 44, voice: 190, detective: true };

  const HATS = [
    { id: "fedora",  de: "Detektiv-Fedora", en: "Detective fedora", color: "#2a2a2a", free: true },
    { id: "beanie",  de: "Brumms Mütze",    en: "Brumm's beanie",   color: "#3d5a8a" },
    { id: "tophat",  de: "Zylinder des Lügners", en: "The Liar's top hat", color: "#111111" },
    { id: "redcap",  de: "Rote Ermittlermütze", en: "Red investigator cap", color: "#ff2d55" },
  ];

  const T = (o, lang) => (o && typeof o === "object" && (o.de || o.en)) ? (o[lang] || o.de) : o;

  // ───────────── Story Mode: Case 1 "The Missing Money Tree" ─────────────
  function storyCase() {
    const c = (id, room, tile, name, desc, facts, isTrue = true) => ({ id, room, tile, name, desc, isTrue, facts: facts.map((f) => ({ fact: f, weight: 1 })) });
    return {
      title: { de: "Fall 1: Der verschwundene Money Tree", en: "Case 1: The Missing Money Tree" },
      briefing: {
        de: "Mitternacht, Schneesturm. Der goldene Bonsai des Hauses, der „Money Tree“, ist aus dem Arbeitszimmer verschwunden. Drei Verdächtige warten in der Großen Halle. Finde Hinweise, befrage alle und lege Beweise gegen ihre Aussagen vor. Lügen haben kurze Beine.",
        en: "Midnight, snowstorm. The house's golden bonsai, the “Money Tree”, is gone from the study. Three suspects wait in the Great Hall. Find clues, question everyone and present evidence against their statements. Lies have short legs.",
      },
      autoResolve: false,
      wrongAllowed: 3,
      culprit: "brumm",
      liesUntilConfession: 3,
      suspects: [
        { id: "brumm", tile: [8, 12], lines: [
          { de: "Um 23 Uhr war ich im Schlafzimmer und habe tief geschlafen.", en: "At 11 pm I was in the bedroom, fast asleep.", claim: claim("brumm", "wasIn", "bedroom", 23) },
          { de: "Den Money Tree habe ich nie angefasst!", en: "I never touched the Money Tree!", claim: claim("brumm", "touched", "moneytree", ANY, true) },
          { de: "In den Keller gehe ich nie. Viel zu staubig.", en: "I never go to the cellar. Far too dusty.", claim: claim("brumm", "wasIn", "cellar", ANY, true) },
        ], confession: { de: "Schon gut, SCHON GUT! Ich hab ihn genommen! Er liegt im Keller hinter den Weinfässern!", en: "Fine, FINE! I took it! It's in the cellar behind the wine barrels!" } },
        { id: "velours", tile: [14, 11], lines: [
          { de: "Um 23 Uhr war ich in der Küche und habe aufgeräumt.", en: "At 11 pm I was in the kitchen, tidying up.", claim: claim("velours", "wasIn", "kitchen", 23) },
          { de: "Vom Kuchen habe ich nicht genascht. Kein Krümel!", en: "I didn't touch the cake. Not a crumb!", claim: claim("velours", "touched", "cake", ANY, true) },
        ] },
        { id: "kip", tile: [20, 13], lines: [
          { de: "Um 23 Uhr war ich im Gewächshaus. Die Orchideen frieren sonst.", en: "At 11 pm I was in the greenhouse. The orchids freeze otherwise.", claim: claim("kip", "wasIn", "greenhouse", 23) },
          { de: "Den Money Tree fasse ich nicht an. Der gehört dem Baron.", en: "I don't touch the Money Tree. It belongs to the Baron.", claim: claim("kip", "touched", "moneytree", ANY, true) },
        ] },
      ],
      clues: [
        c("coat", "bedroom", [29, 3], { de: "Brumms nasser Mantel", en: "Brumm's wet coat" }, { de: "Klatschnass vom Schnee. Wer um 23 Uhr im Bett lag, war nicht draußen im Garten.", en: "Soaked with snow. Nobody asleep in bed at 11 pm was out in the garden." }, [claim("brumm", "wasIn", "garden", 23)]),
        c("golddust", "library", [21, 5], { de: "Handschuh mit Goldstaub", en: "Glove with gold dust" }, { de: "Ein großer brauner Handschuh, voller Goldstaub vom Money Tree. Die Initialen: „B. B.“", en: "A large brown glove covered in gold dust from the Money Tree. Initials: “B. B.”" }, [claim("brumm", "touched", "moneytree", 23)]),
        c("boot", "cellar", [24, 22], { de: "Stiefelabdruck Größe 46", en: "Size 46 boot print" }, { de: "Frisch im Kellerstaub, kurz nach Mitternacht. Nur einer im Haus trägt Größe 46.", en: "Fresh in the cellar dust, just after midnight. Only one person in the house wears size 46." }, [claim("brumm", "wasIn", "cellar", 0)]),
        c("crumbs", "kitchen", [36, 5], { de: "Kuchenkrümel auf der Schürze", en: "Cake crumbs on an apron" }, { de: "Madame Velours' Schürze, voller Schokoladenkrümel.", en: "Madame Velours' apron, full of chocolate crumbs." }, [claim("velours", "touched", "cake", 23)], false),
        c("glove", "shed", [34, 21], { de: "Grüner Gartenhandschuh", en: "Green garden glove" }, { de: "Liegt auffällig mitten im Schuppen. Fast zu auffällig.", en: "Lying right in the middle of the shed. Almost too obvious." }, [], false),
        c("watch", "study", [4, 3], { de: "Zerbrochene Uhr: 23:07", en: "Broken watch: 23:07" }, { de: "Neben dem leeren Podest. Der Diebstahl geschah um 23:07.", en: "Next to the empty pedestal. The theft happened at 23:07." }, [], false),
        c("teacup", "greenhouse", [31, 11], { de: "Leere Teetasse", en: "Empty teacup" }, { de: "Noch warm. Kip trinkt hier nachts Tee.", en: "Still warm. Kip drinks tea here at night." }, [], false),
      ],
      reward: "beanie",
    };
  }

  // ───────────── Party Mode (vs bots): random Liar ─────────────
  function partyCase(opts, rand) {
    const pick = (a) => a[Math.floor(rand() * a.length)];
    const shuffle = (a) => { a = a.slice(); for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rand() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };
    const bots = shuffle(CAST).slice(0, opts.bots);
    const liar = pick(bots);
    const innocents = bots.filter((b) => b !== liar);
    const whiteLiar = pick(innocents);
    const framedOne = pick(innocents.filter((b) => b !== whiteLiar)) || whiteLiar;
    const alibiRooms = ["library", "bedroom", "kitchen", "hall", "greenhouse", "shed"];
    const trueRoom = {}; for (const b of innocents) trueRoom[b.id] = pick(alibiRooms);
    const liarFake = pick(alibiRooms);
    const floorIn = (room) => { const r = ROOMS[room]; return [r.x + 1 + Math.floor(rand() * (r.w - 2)), r.y + 1 + Math.floor(rand() * (r.h - 2))]; };
    const c = (id, room, name, desc, facts, isTrue = true) => ({ id, room, tile: floorIn(room), name, desc, isTrue, facts: facts.map((f) => ({ fact: f, weight: 1 })) });

    const roomName = (id) => ({ de: ROOMS[id].de, en: ROOMS[id].en });
    const alibi = (b, room) => ({ de: `Um 23 Uhr war ich im Raum „${ROOMS[room].de}“.`, en: `At 11 pm I was in the ${ROOMS[room].en.toLowerCase()}.`, claim: claim(b.id, "wasIn", room, 23) });

    const clues = [
      c("boot", "study", { de: `Stiefelabdruck Größe ${liar.shoe}`, en: `Size ${liar.shoe} boot print` }, { de: `Neben dem leeren Podest im Arbeitszimmer, 23 Uhr. Vergleiche die Schuhgrößen im Notizbuch.`, en: `Next to the empty pedestal in the study, 11 pm. Compare shoe sizes in the notebook.` }, [claim(liar.id, "wasIn", "study", 23)]),
      c("fiber", pick(["study", "library"]), { de: `Wollfaser, ${liar.coatName.de}`, en: `Wool fiber, ${liar.coatName.en}` }, { de: `Hing am Blumentopf des Money Tree. Wer trägt einen ${liar.coatName.de}en Mantel?`, en: `Caught on the Money Tree's pot. Who wears a ${liar.coatName.en} coat?` }, [claim(liar.id, "touched", "moneytree", 23)]),
      c("key", "cellar", { de: "Kellerschlüssel mit Namensschild", en: "Cellar key with a name tag" }, { de: `Auf dem Schild steht „${liar.name}“. Der Money Tree lag direkt daneben.`, en: `The tag reads “${liar.name}”. The Money Tree was right next to it.` }, [claim(liar.id, "had", "cellarkey", ANY)]),
      c("crumbs", "kitchen", { de: `Kuchenkrümel, Schuhabdruck Größe ${whiteLiar.shoe}`, en: `Cake crumbs, size ${whiteLiar.shoe} shoe print` }, { de: "Jemand hat nachts vom Kuchen genascht. Mit dem Diebstahl hat das nichts zu tun.", en: "Someone snacked on the cake at night. Nothing to do with the theft." }, [claim(whiteLiar.id, "touched", "cake", ANY)], false),
      c("tea", pick(["greenhouse", "library"]), { de: "Leere Teetasse", en: "Empty teacup" }, { de: "Noch lauwarm.", en: "Still lukewarm." }, [], false),
      c("paper", "hall", { de: "Zerknitterte Zeitung", en: "Crumpled newspaper" }, { de: "Schlagzeile: „Goldener Bonsai Millionen wert“.", en: "Headline: “Golden bonsai worth millions”." }, [], false),
      c("cat", "bedroom", { de: "Katzenhaare", en: "Cat hair" }, { de: "Das Haus hat keine Katze. Seltsam, aber ohne Bezug.", en: "The house has no cat. Odd, but unrelated." }, [], false),
      c("butler", pick(["library", "greenhouse", "bedroom"]), { de: "Notiz des Butlers", en: "The butler's note" }, { de: "„Der Detektiv stand um 23 Uhr in der Großen Halle am Kamin.“ Wer über das eigene Alibi lügt, fliegt hiermit auf.", en: "“The detective stood by the fireplace in the Great Hall at 11 pm.” Anyone who lied about their own alibi is caught by this." }, [claim("you", "wasIn", "hall", 23)], false),
      c("postcard", "garden", { de: "Alte Postkarte", en: "Old postcard" }, { de: "„Grüße aus Baden-Baden.“ Durchgeweicht.", en: "“Greetings from Baden-Baden.” Soaked." }, [], false),
    ];
    const planted = {
      id: "planted", room: "hall", tile: floorIn("hall"), isTrue: true, planted: true, plantedBy: liar.id,
      name: { de: `Goldstaub-Spur, Schuhgröße ${framedOne.shoe}`, en: `Gold dust trail, shoe size ${framedOne.shoe}` },
      desc: { de: "Eine Spur aus Goldstaub vom Arbeitszimmer in die Halle. Sieht eindeutig aus. Vielleicht zu eindeutig.", en: "A trail of gold dust from the study into the hall. Looks conclusive. Maybe too conclusive." },
      facts: [{ fact: claim(framedOne.id, "wasIn", "study", 23), weight: 1 }],
    };

    const statements = { opening: [], meeting1: [], final: [] };
    for (const b of bots) statements.opening.push({ speaker: b.id, line: b === liar ? alibi(b, liarFake) : alibi(b, trueRoom[b.id]) });
    statements.meeting1.push({ speaker: liar.id, line: { de: "Den Money Tree habe ich nie angefasst. Ehrenwort.", en: "I never touched the Money Tree. Word of honor.", claim: claim(liar.id, "touched", "moneytree", ANY, true) } });
    statements.meeting1.push({ speaker: whiteLiar.id, line: { de: "Und vom Kuchen habe ich auch nicht genascht!", en: "And I didn't touch the cake either!", claim: claim(whiteLiar.id, "touched", "cake", ANY, true) } });
    for (const b of innocents.filter((x) => x !== whiteLiar).slice(0, 2))
      statements.meeting1.push({ speaker: b.id, line: { de: "Ich habe nichts Verdächtiges gesehen.", en: "I didn't see anything suspicious." } });
    statements.final.push({ speaker: liar.id, line: { de: "Einen Kellerschlüssel hatte ich nie!", en: "I never had a cellar key!", claim: claim(liar.id, "had", "cellarkey", ANY, true) } });
    statements.final.push({ speaker: pick(innocents).id, line: { de: "Einer von uns lügt. Seht euch die Beine an!", en: "One of us is lying. Look at the legs!" } });

    return {
      title: { de: "Das Große Herrenhaus: Der verschwundene Money Tree", en: "The Grand Manor: The Missing Money Tree" },
      briefing: {
        de: `Einer der ${bots.length} Gäste ist der Lügner. Finde 3 echte Hinweise und beschuldige ihn bei einem Treffen, bevor die Zeit abläuft. Jede aufgedeckte Lüge kostet 25 % Bein. Du warst um 23 Uhr in der Großen Halle.`,
        en: `One of the ${bots.length} guests is the Liar. Find 3 genuine clues and accuse them at a meeting before time runs out. Every exposed lie costs 25% leg. You were in the Great Hall at 11 pm.`,
      },
      autoResolve: true,
      bots, liar: liar.id, framed: framedOne.id, whiteLiar: whiteLiar.id, trueRoom, clues, planted, statements,
      roomName,
    };
  }

  global.SL = Object.assign(global.SL || {}, { STR, ROOMS, buildMap, CAST, PLAYER, HATS, T, storyCase, partyCase });
})(window);
