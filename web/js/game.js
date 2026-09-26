// SHORT LEGS – browser game: rendering, input, audio, menus and match flow.
(function () {
  "use strict";
  const { ShrinkMatrix: SM, DeceptionEngine, STR, ROOMS, buildMap, CAST, PLAYER, HATS, T, storyCase, partyCase, claim } = SL;

  const TILE = 32;
  const LIE = "#ff2d55";
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const rnd = Math.random;

  // ═════════════════════════ Settings & archive (per-browser) ═════════════════════════
  const DEFAULTS = { lang: "de", art: "comic", res: "auto", master: 0.8, music: 0.45, voice: 0.8, sfx: 0.8, tts: false, subs: true, subSize: 2, contrast: true, flash: false, hat: "fedora" };
  const store = {
    get(k, d) { try { const v = localStorage.getItem(k); return v ? Object.assign({}, d, JSON.parse(v)) : Object.assign({}, d); } catch (e) { return Object.assign({}, d); } },
    set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* storage blocked: keep in memory */ } },
  };
  let S = store.get("shortlegs.settings", DEFAULTS);
  let saved = Object.assign({}, S);
  const archive = store.get("shortlegs.archive", { hats: ["fedora"], cases: [], lore: [] });
  const saveArchive = () => store.set("shortlegs.archive", archive);
  const unlock = (list, id) => { if (!archive[list].includes(id)) { archive[list].push(id); saveArchive(); return true; } return false; };

  const t = (k, ...a) => { const v = (STR[S.lang] || STR.de)[k]; return typeof v === "function" ? v(...a) : v; };
  const tx = (o) => T(o, S.lang);

  // Noir palette: desaturate every color except lie red.
  const noirCache = new Map();
  function P(hex) {
    if (S.art !== "noir" || hex === LIE) return hex;
    let v = noirCache.get(hex);
    if (!v) {
      const n = parseInt(hex.slice(1), 16);
      const l = clamp(Math.round((0.3 * (n >> 16 & 255) + 0.59 * (n >> 8 & 255) + 0.11 * (n & 255) - 128) * 1.25 + 128), 0, 255);
      v = `rgb(${l},${l},${l})`;
      noirCache.set(hex, v);
    }
    return v;
  }

  // ═════════════════════════ Audio (WebAudio, starts on first interaction) ═════════════════════════
  const Sound = {
    ctx: null, bus: {},
    init() {
      if (this.ctx) return;
      try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const master = ctx.createGain(); master.connect(ctx.destination);
        this.bus = { master, music: ctx.createGain(), voice: ctx.createGain(), sfx: ctx.createGain() };
        for (const k of ["music", "voice", "sfx"]) this.bus[k].connect(master);
        this.ctx = ctx; this.apply(); this.startMusic();
      } catch (e) { this.ctx = null; }
    },
    apply() {
      if (!this.ctx) return;
      this.bus.master.gain.value = S.master; this.bus.music.gain.value = S.music * 0.35;
      this.bus.voice.gain.value = S.voice * 0.5; this.bus.sfx.gain.value = S.sfx * 0.6;
    },
    tone(bus, type, f0, f1, dur, vol = 0.4, delay = 0) {
      if (!this.ctx) return;
      const c = this.ctx, t0 = c.currentTime + delay, o = c.createOscillator(), g = c.createGain();
      o.type = type; o.frequency.setValueAtTime(f0, t0);
      if (f1) o.frequency.exponentialRampToValueAtTime(f1, t0 + dur);
      g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(vol, t0 + 0.012); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
      o.connect(g).connect(this.bus[bus]); o.start(t0); o.stop(t0 + dur + 0.02);
    },
    // Character "voice": blips, pitched +15% per lie.
    voice(base, lies, text) {
      const n = clamp(Math.round(String(text).split(/\s+/).length * 0.8), 2, 14);
      const p = SM.voicePitch(lies);
      for (let i = 0; i < n; i++) this.tone("voice", "square", base * p * (0.9 + rnd() * 0.25), 0, 0.07, 0.18, i * 0.085);
    },
    boing() { this.tone("sfx", "sine", 620, 110, 0.32, 0.5); this.tone("sfx", "sine", 140, 320, 0.18, 0.35, 0.3); },
    alarm() { for (let i = 0; i < 3; i++) { this.tone("sfx", "square", 880, 0, 0.1, 0.18, i * 0.2); this.tone("sfx", "square", 660, 0, 0.1, 0.18, i * 0.2 + 0.1); } },
    chime() { this.tone("sfx", "triangle", 784, 0, 0.25, 0.35); this.tone("sfx", "triangle", 1175, 0, 0.35, 0.3, 0.1); },
    hop() { this.tone("sfx", "sine", 300, 600, 0.15, 0.25); },
    click() { this.tone("sfx", "triangle", 520, 0, 0.05, 0.15); },
    thud() { this.tone("sfx", "sine", 120, 60, 0.2, 0.4); },
    startMusic() {
      // Slow noir walking bass, A minor.
      const notes = [110, 130.8, 146.8, 164.8, 110, 98, 103.8, 82.4];
      let i = 0;
      const step = () => {
        if (!this.ctx) return;
        const n = notes[i++ % notes.length];
        this.tone("music", "triangle", n, 0, 0.55, 0.5);
        if (i % 4 === 1) this.tone("music", "sine", n * 4, 0, 1.2, 0.08);
      };
      setInterval(step, 620);
    },
  };

  function speakTTS(text, lies) {
    if (!S.tts || !("speechSynthesis" in window)) return;
    try {
      const u = new SpeechSynthesisUtterance(text);
      u.lang = S.lang === "de" ? "de-DE" : "en-US";
      u.pitch = clamp(SM.voicePitch(lies || 0), 0, 2);
      window.speechSynthesis.speak(u);
    } catch (e) { /* no speech available */ }
  }

  // ═════════════════════════ Canvas & drawing ═════════════════════════
  const cv = $("cv"), ctx = cv.getContext("2d");
  let VW = 0, VH = 0, DPR = 1;
  function resize() {
    const cap = S.res === "auto" ? 2 : Number(S.res);
    DPR = Math.min(window.devicePixelRatio || 1, cap);
    VW = window.innerWidth; VH = window.innerHeight;
    cv.width = Math.round(VW * DPR); cv.height = Math.round(VH * DPR);
  }
  window.addEventListener("resize", resize);

  function rr(c, x, y, w, h, r) {
    c.beginPath();
    if (c.roundRect) c.roundRect(x, y, w, h, r); else c.rect(x, y, w, h);
  }
  function ell(c, x, y, rx, ry) { c.beginPath(); c.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2); }

  // One chunky suspect (art direction §0), feet at (x, y). leg = leg scale 0..1.
  function drawChar(c, o) {
    const s = o.s, leg = Math.max(0.06, o.leg), L = 78 * leg, hip = -8 - L;
    const f = (col) => o.fill || P(col);
    c.save();
    c.translate(o.x, o.y);
    c.scale(s, s);
    c.globalAlpha = o.alpha == null ? 1 : o.alpha;
    if (!o.fill) { c.fillStyle = "rgba(0,0,0,.28)"; ell(c, 0, -2, 38, 10); c.fill(); }
    c.translate(0, -(o.lift || 0));

    const st = o.stride || 0;
    for (const [lx, a] of [[-13, st], [13, -st]]) {
      const th = a * Math.PI / 180;
      c.save(); c.translate(lx, hip + 4); c.rotate(th);
      c.fillStyle = f("#3a3f47"); rr(c, -7, 0, 14, L, 6); c.fill();
      c.restore();
      const fx = lx - Math.sin(th) * L, fy = hip + 4 + Math.cos(th) * L;
      c.fillStyle = f("#231a15"); ell(c, fx + 3, fy - 3, 13, 8); c.fill();
    }
    // coat + arms
    c.fillStyle = f(o.coat); rr(c, -36, hip - 78, 72, 88, 24); c.fill();
    if (!o.fill) { c.fillStyle = "rgba(0,0,0,.18)"; rr(c, -3, hip - 70, 6, 74, 3); c.fill(); }
    for (const [sx, a] of [[-37, 10], [37, -10]]) {
      c.save(); c.translate(sx, hip - 70); c.rotate(a * Math.PI / 180);
      c.fillStyle = f(o.coat); rr(c, -9, 0, 18, 60, 9); c.fill(); c.restore();
    }
    if (!o.back) { c.fillStyle = f("#f2b38f"); ell(c, -44, hip - 8, 10, 10); c.fill(); ell(c, 44, hip - 8, 10, 10); c.fill(); }

    // head
    const hy = hip - 110;
    c.fillStyle = f("#f2b38f"); ell(c, 0, hy, 36, 36); c.fill();
    if (o.back) {
      c.fillStyle = f(o.beard ? "#2a1d17" : "#5a3a22"); ell(c, 0, hy + 6, 34, 28); c.fill();
    } else if (!o.fill) {
      if (o.beard) {
        c.fillStyle = P("#2a1d17"); c.beginPath();
        c.moveTo(-34, hy + 2); c.quadraticCurveTo(-38, hy + 52, 0, hy + 60); c.quadraticCurveTo(38, hy + 52, 34, hy + 2);
        c.quadraticCurveTo(20, hy + 22, 0, hy + 20); c.quadraticCurveTo(-20, hy + 22, -34, hy + 2); c.fill();
      } else {
        c.fillStyle = P("#5a3a22"); ell(c, -30, hy + 4, 9, 22); c.fill(); ell(c, 30, hy + 4, 9, 22); c.fill();
      }
      const panic = o.face === "panic", er = panic ? 12 : 10, pr = panic ? 1.8 : 3;
      c.lineWidth = 1.5; c.strokeStyle = "#1a1a1a";
      for (const ex of [-13, 13]) { c.fillStyle = "#ffffff"; ell(c, ex, hy - 6, er, er); c.fill(); c.stroke(); c.fillStyle = "#111111"; ell(c, ex * 0.93, hy - 5, pr, pr); c.fill(); }
      c.strokeStyle = P("#2a1d17"); c.lineWidth = 4; c.lineCap = "round";
      c.beginPath(); c.moveTo(-24, hy - 21 - (panic ? 4 : 0)); c.lineTo(-5, hy - 18); c.moveTo(24, hy - 21 - (panic ? 4 : 0)); c.lineTo(5, hy - 18); c.stroke();
      c.fillStyle = P("#d98e6b"); ell(c, 0, hy + 8, 8, 7); c.fill();
      c.fillStyle = "#3b1410"; ell(c, 0, hy + 23, panic ? 6 : 8, panic ? 5 : 2.5); c.fill();
      if (o.face !== "calm") {
        c.fillStyle = "#9fd3ef";
        ell(c, 32, hy - 18, 3.5, 6); c.fill(); ell(c, -33, hy - 8, 3, 5); c.fill();
      }
    }
    drawHat(c, o.hatStyle || "beanie", o.fill || P(o.hat), hy);
    c.restore();
  }

  function drawHat(c, style, col, hy) {
    c.fillStyle = col;
    if (style === "fedora") {
      ell(c, 0, hy - 28, 52, 10); c.fill();
      rr(c, -28, hy - 66, 56, 40, 12); c.fill();
      c.fillStyle = "#7a2b2b"; c.fillRect(-28, hy - 40, 56, 8);
    } else if (style === "tophat") {
      ell(c, 0, hy - 28, 48, 9); c.fill();
      c.fillRect(-26, hy - 98, 52, 70);
      c.fillStyle = LIE; c.fillRect(-26, hy - 42, 52, 8);
    } else {
      c.beginPath(); c.moveTo(-35, hy - 22); c.quadraticCurveTo(-35, hy - 60, 0, hy - 62); c.quadraticCurveTo(35, hy - 60, 35, hy - 22); c.closePath(); c.fill();
      rr(c, -38, hy - 34, 76, 13, 6.5); c.fill();
      c.fillStyle = "rgba(255,255,255,.18)"; ell(c, 0, hy - 64, 8, 8); c.fill();
    }
  }

  const faceFor = (lies) => (lies >= 3 ? "panic" : lies >= 1 ? "sweat" : "calm");
  const hatStyleFor = (id) => (id === "fedora" ? "fedora" : id === "tophat" ? "tophat" : "beanie");
  const hatColorFor = (id) => (HATS.find((h) => h.id === id) || HATS[0]).color;

  // ═════════════════════════ Input ═════════════════════════
  const keys = new Set(), hit = new Set();
  const touch = { x: 0, y: 0, sprint: false };
  window.addEventListener("keydown", (e) => {
    Sound.init();
    if (["Tab", " ", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(e.key) && screen === "game" && !anyModal()) e.preventDefault();
    if (e.key === "Tab" && screen === "game") e.preventDefault();
    if (!keys.has(e.code)) hit.add(e.code);
    keys.add(e.code);
    onKey(e);
  });
  window.addEventListener("keyup", (e) => keys.delete(e.code));
  window.addEventListener("blur", () => keys.clear());
  window.addEventListener("pointerdown", () => Sound.init(), { once: false });

  const pads = { prev: [] };
  function pollPad() {
    let gp = null;
    try { gp = navigator.getGamepads ? [...navigator.getGamepads()].find((g) => g) : null; } catch (e) { gp = null; }
    if (!gp) return null;
    const b = gp.buttons.map((x) => x.pressed);
    const was = pads.prev; pads.prev = b;
    const down = (i) => b[i] && !was[i];
    if (down(9)) virtualKey("Escape");
    if (down(8)) virtualKey("Tab");
    if (down(0)) virtualKey("KeyE");
    if (down(1)) hit.add("Space");
    if (down(12)) virtualKey("ArrowUp");
    if (down(13)) virtualKey("ArrowDown");
    return { x: Math.abs(gp.axes[0]) > 0.2 ? gp.axes[0] : 0, y: Math.abs(gp.axes[1]) > 0.2 ? gp.axes[1] : 0, sprint: !!b[10] };
  }
  function virtualKey(code) {
    const key = { Escape: "Escape", Tab: "Tab", KeyE: "e", ArrowUp: "ArrowUp", ArrowDown: "ArrowDown" }[code] || code;
    hit.add(code);
    onKey({ code, key, preventDefault() {}, synthetic: true });
  }

  function moveInput(pad) {
    let x = 0, y = 0;
    if (keys.has("KeyA") || keys.has("ArrowLeft")) x -= 1;
    if (keys.has("KeyD") || keys.has("ArrowRight")) x += 1;
    if (keys.has("KeyW") || keys.has("ArrowUp")) y -= 1;
    if (keys.has("KeyS") || keys.has("ArrowDown")) y += 1;
    if (pad) { x += pad.x; y += pad.y; }
    x += touch.x; y += touch.y;
    const m = Math.hypot(x, y);
    if (m > 1) { x /= m; y /= m; }
    const sprint = keys.has("ShiftLeft") || keys.has("ShiftRight") || (pad && pad.sprint) || touch.sprint;
    return { x, y, sprint };
  }

  // Touch: joystick + buttons
  let showTouch = () => {};
  (function setupTouch() {
    const coarse = matchMedia("(pointer: coarse)").matches;
    if (!coarse) return;
    const stick = $("stick"), knob = stick.querySelector("i");
    let id = null;
    const upd = (e) => {
      const r = stick.getBoundingClientRect();
      let dx = e.clientX - (r.left + r.width / 2), dy = e.clientY - (r.top + r.height / 2);
      const m = Math.hypot(dx, dy), max = r.width / 2;
      if (m > max) { dx *= max / m; dy *= max / m; }
      knob.style.transform = `translate(${dx}px, ${dy}px)`;
      touch.x = dx / max; touch.y = dy / max;
    };
    stick.addEventListener("pointerdown", (e) => { id = e.pointerId; stick.setPointerCapture(id); upd(e); });
    stick.addEventListener("pointermove", (e) => { if (e.pointerId === id) upd(e); });
    const end = () => { id = null; touch.x = touch.y = 0; knob.style.transform = ""; };
    stick.addEventListener("pointerup", end); stick.addEventListener("pointercancel", end);
    for (const b of $("tbtns").querySelectorAll("button")) {
      b.addEventListener("pointerdown", (e) => {
        e.preventDefault(); Sound.init();
        const k = b.dataset.k;
        if (k === "sprint") { touch.sprint = !touch.sprint; b.classList.toggle("on", touch.sprint); }
        else if (k === "jump") hit.add("Space");
        else if (k === "use") virtualKey("KeyE");
        else if (k === "note") virtualKey("Tab");
        else if (k === "meet") virtualKey("KeyM");
        else if (k === "pause") virtualKey("Escape");
      });
    }
    showTouch = (on) => { $("touch").hidden = !on; };
  })();

  // ═════════════════════════ UI helpers ═════════════════════════
  let screen = "menu";
  const SCREENS = ["menu", "lobby", "settings", "archive"];
  const MODALS = ["quit", "pause", "notebook", "dialog", "wheel", "vote", "card", "end"];
  const anyModal = () => MODALS.some((m) => !$("m-" + m).hidden);
  function openModal(id) { $("m-" + id).hidden = false; focusFirst($("m-" + id)); }
  function closeModal(id) { $("m-" + id).hidden = true; }
  function focusFirst(root) { const b = root.querySelector("button:not([disabled]), select, input"); if (b) b.focus({ preventScroll: true }); }

  function show(name) {
    screen = name;
    for (const s of SCREENS) $("scr-" + s).hidden = s !== name;
    $("hud").hidden = name !== "game";
    showTouch(name === "game");
    if (name !== "game") $("subs").hidden = true;
    if (SCREENS.includes(name)) { renderScreen(name); focusFirst($("scr-" + name)); }
  }
  function renderScreen(name) {
    if (name === "menu") renderMenu();
    if (name === "lobby") renderLobby();
    if (name === "settings") renderSettings();
    if (name === "archive") renderArchive();
  }

  function toast(msg, red) {
    const d = document.createElement("div");
    d.textContent = msg; if (red) d.className = "red";
    $("toast").appendChild(d);
    setTimeout(() => d.remove(), 4300);
  }

  function flash() {
    const f = $("flash");
    f.className = S.flash ? "border" : "";
    requestAnimationFrame(() => { f.classList.add("on"); setTimeout(() => f.classList.remove("on"), 90); });
  }

  // Arrow-key / D-pad navigation between buttons of the active screen or modal.
  function navFocus(dir) {
    const root = MODALS.map((m) => $("m-" + m)).filter((m) => !m.hidden).pop() || (SCREENS.includes(screen) ? $("scr-" + screen) : null);
    if (!root) return false;
    const items = [...root.querySelectorAll("button:not([disabled]), select, input")].filter((el) => el.offsetParent !== null);
    if (!items.length) return false;
    const i = items.indexOf(document.activeElement);
    items[(i + dir + items.length) % items.length].focus();
    return true;
  }

  // ═════════════════════════ Menu screens ═════════════════════════
  function renderMenu() {
    const items = [["story", () => startGame("story")], ["lobby", () => show("lobby")], ["lab", () => startGame("lab")], ["archive", () => show("archive")], ["settings", () => show("settings")], ["quit", () => { $("quitBox").innerHTML = `<h2>${t("quitTitle")}</h2><p class="note">${t("quitText")}</p><div class="actions"><button class="btn" id="qBack">${t("back")}</button></div>`; $("qBack").onclick = () => closeModal("quit"); openModal("quit"); }]];
    const nav = $("mainNav");
    nav.innerHTML = "";
    for (const [k, fn] of items) {
      const b = document.createElement("button");
      b.textContent = `[${t(k)}]`;
      b.onclick = () => { Sound.init(); Sound.click(); fn(); };
      nav.appendChild(b);
    }
    $("footL").textContent = S.lang === "de" ? "Pfeiltasten + Enter oder Maus · Gamepad unterstützt" : "Arrow keys + Enter or mouse · gamepad supported";
    document.querySelector(".proverb").textContent = S.lang === "de" ? "„Lügen haben kurze Beine.“" : "“Lies have short legs.”";
  }

  const lobby = { maxLies: 4, bots: 5, minutes: 6 };
  function renderLobby() {
    const code = (Math.random().toString(36).slice(2, 8) + "XXXXXX").slice(0, 6).toUpperCase();
    const seg = (key, vals, fmt = (v) => v) => `<div class="seg" data-key="${key}">${vals.map((v) => `<button data-v="${v}" aria-pressed="${lobby[key] === v}">${fmt(v)}</button>`).join("")}</div>`;
    $("scr-lobby").innerHTML = `<div class="sheet">
      <h2>${t("lobbyTitle")}</h2>
      <p class="note">${t("lobbyNote")}</p>
      <div class="group">
        <div class="row"><span class="lbl">${t("code")}</span><span class="chip" style="justify-self:start">${code}</span></div>
        <div class="row"><span class="lbl">${t("map")}</span><div class="seg"><button aria-pressed="true">${t("mapManor")}</button><button disabled>${t("mapYacht")}</button></div></div>
        <div class="row"><span class="lbl">${t("maxLies")}</span>${seg("maxLies", [1, 2, 3, 4])}</div>
        <div class="row"><span class="lbl">${t("bots")}</span>${seg("bots", [3, 4, 5, 6, 7])}</div>
        <div class="row"><span class="lbl">${t("timer")}</span>${seg("minutes", [4, 6, 8, 10], (v) => v + " min")}</div>
        <div class="row"><span class="lbl">${t("voice")}</span><small>${t("voiceOnly")}</small></div>
      </div>
      <div class="actions"><button class="btn primary" id="lbStart">${t("start")}</button><button class="btn" id="lbBack">${t("back")}</button></div>
    </div>`;
    for (const g of $("scr-lobby").querySelectorAll(".seg[data-key]")) {
      g.addEventListener("click", (e) => {
        const b = e.target.closest("button"); if (!b) return;
        lobby[g.dataset.key] = Number(b.dataset.v); Sound.click();
        for (const x of g.children) x.setAttribute("aria-pressed", x === b);
      });
    }
    $("lbStart").onclick = () => startGame("party", Object.assign({}, lobby));
    $("lbBack").onclick = () => show("menu");
  }

  function renderSettings() {
    const seg = (key, vals) => `<div class="seg" data-key="${key}">${vals.map(([v, label]) => `<button data-v="${v}" aria-pressed="${String(S[key]) === String(v)}">${label}</button>`).join("")}</div>`;
    const slider = (key, label) => `<div class="row"><label for="set-${key}">${label}</label><input id="set-${key}" type="range" min="0" max="1" step="0.05" value="${S[key]}" data-key="${key}"></div>`;
    const onoff = (key) => seg(key, [[true, t("on")], [false, t("off")]]);
    const k = (...ks) => ks.map((x) => `<span class="kbd">${x}</span>`).join("");
    $("scr-settings").innerHTML = `<div class="sheet">
      <h2>${t("settings")}</h2>
      <div class="group"><h3>${t("gfx")}</h3>
        <div class="row"><span class="lbl">${t("art")}</span>${seg("art", [["comic", t("artComic")], ["noir", t("artNoir")]])}</div>
        <div class="row"><label for="set-res">${t("res")}</label><select id="set-res" data-key="res">
          ${[["auto", "Auto"], ["1", "720p"], ["1.5", "1080p"], ["2", "4K"]].map(([v, l]) => `<option value="${v}" ${String(S.res) === v ? "selected" : ""}>${l}</option>`).join("")}</select></div>
        <div class="row"><span class="lbl">${t("full")}</span><div class="seg"><button id="set-full">${t("full")}</button></div></div>
        ${slider("master", t("master"))}${slider("music", t("music"))}${slider("voice", t("voiceVol"))}${slider("sfx", t("sfx"))}
      </div>
      <div class="group"><h3>${t("access")}</h3>
        <div class="row"><span class="lbl">${t("tts")}</span>${onoff("tts")}</div>
        <div class="row"><span class="lbl">${t("stt")}</span><small>${t("voiceOnly")}</small></div>
        <div class="row"><span class="lbl">${t("subs")}</span>${onoff("subs")}</div>
        <div class="row"><span class="lbl">${t("subSize")}</span>${seg("subSize", [[0, "S"], [1, "M"], [2, "L"], [3, "XL"]])}</div>
        <div class="row"><span class="lbl">${t("contrast")}</span>${onoff("contrast")}</div>
        <div class="row"><span class="lbl">${t("flash")}</span>${onoff("flash")}</div>
        <div class="row"><span class="lbl">${t("lang")}</span>${seg("lang", [["de", "Deutsch"], ["en", "English"]])}</div>
      </div>
      <div class="group"><h3>${t("controls")}</h3>
        <table class="keys"><tbody>
          <tr><td>${k("W", "A", "S", "D")} · L-Stick</td><td>${t("ctrlMove")}</td></tr>
          <tr><td>${k("Shift")} · L3</td><td>${t("ctrlSprint")}</td></tr>
          <tr><td>${k("Space")} · B</td><td>${t("ctrlJump")}</td></tr>
          <tr><td>${k("E")} / ${k("Klick")} · A</td><td>${t("ctrlUse")}</td></tr>
          <tr><td>${k("Tab")} · View · D-Pad ↑↓</td><td>${t("ctrlNote")}</td></tr>
          <tr><td>${k("M")}</td><td>${t("ctrlMeet")}</td></tr>
          <tr><td>${k("V")}</td><td>${t("ctrlPtt")} <small>(${t("voiceOnly")})</small></td></tr>
          <tr><td>${k("Esc")} · Start</td><td>${t("ctrlPause")}</td></tr>
        </tbody></table>
      </div>
      <div class="actions"><button class="btn primary" id="setApply">${t("apply")}</button><button class="btn" id="setBack">${t("back")}</button></div>
    </div>`;
    const root = $("scr-settings");
    for (const g of root.querySelectorAll(".seg[data-key]")) {
      g.addEventListener("click", (e) => {
        const b = e.target.closest("button"); if (!b) return;
        const key = g.dataset.key; let v = b.dataset.v;
        if (v === "true" || v === "false") v = v === "true"; else if (!isNaN(Number(v)) && key === "subSize") v = Number(v);
        S[key] = v; applySettings(); Sound.click();
        if (key === "lang") { renderSettings(); focusFirst(root); } else for (const x of g.children) x.setAttribute("aria-pressed", x === b);
      });
    }
    for (const r of root.querySelectorAll("input[type=range]")) r.addEventListener("input", () => { S[r.dataset.key] = Number(r.value); applySettings(); });
    $("set-res").addEventListener("change", (e) => { S.res = e.target.value; applySettings(); });
    $("set-full").onclick = () => {
      try { if (document.fullscreenElement) document.exitFullscreen(); else document.documentElement.requestFullscreen().catch(() => toast(S.lang === "de" ? "Vollbild ist hier nicht verfügbar." : "Fullscreen isn't available here.")); }
      catch (e) { toast(S.lang === "de" ? "Vollbild ist hier nicht verfügbar." : "Fullscreen isn't available here."); }
    };
    $("setApply").onclick = () => { saved = Object.assign({}, S); store.set("shortlegs.settings", S); toast(S.lang === "de" ? "Gespeichert." : "Saved."); settingsBack(); };
    $("setBack").onclick = () => { S = Object.assign({}, saved); applySettings(); settingsBack(); };
  }
  let settingsReturn = "menu";
  function settingsBack() {
    if (settingsReturn === "pause") { show("game"); settingsReturn = "menu"; openPause(); }
    else show("menu");
  }

  function applySettings() {
    Sound.apply(); resize(); noirCache.clear();
    document.documentElement.style.setProperty("--sub-size", [16, 20, 24, 30][S.subSize] + "px");
    $("subs").classList.toggle("hc", !!S.contrast);
    document.documentElement.lang = S.lang;
  }

  function renderArchive() {
    const hatCards = HATS.map((h) => {
      const own = archive.hats.includes(h.id) || h.free;
      const worn = S.hat === h.id;
      return `<div class="hat ${own ? "" : "locked"}"><canvas width="192" height="192" data-hat="${h.id}"></canvas><span>${esc(tx(h))}</span>
        <button class="btn" data-wear="${h.id}" ${own && !worn ? "" : "disabled"}>${worn ? t("equipped") : own ? t("equip") : t("locked")}</button></div>`;
    }).join("");
    const cases = archive.cases.length ? archive.cases.map((c) => `<li>${esc(c)}</li>`).join("") : `<li class="note">—</li>`;
    const lore = archive.lore.length ? archive.lore.map((c) => `<li>${esc(c)}</li>`).join("") : `<li class="note">—</li>`;
    $("scr-archive").innerHTML = `<div class="sheet">
      <h2>${t("archiveTitle")}</h2>
      <div class="group"><h3>${t("hats")}</h3><div class="hats">${hatCards}</div></div>
      <div class="group nb"><section><h3>${t("cases")}</h3><ul>${cases}</ul></section><section><h3>${t("lore")}</h3><ul>${lore}</ul></section></div>
      <div class="actions"><button class="btn" id="arBack">${t("back")}</button></div>
    </div>`;
    for (const c of $("scr-archive").querySelectorAll("canvas[data-hat]")) {
      const g = c.getContext("2d");
      drawChar(g, { x: 96, y: 330, s: 1.05, leg: 1, coat: PLAYER.coat, hat: hatColorFor(c.dataset.hat), hatStyle: hatStyleFor(c.dataset.hat), face: "calm" });
    }
    for (const b of $("scr-archive").querySelectorAll("[data-wear]")) b.onclick = () => { S.hat = b.dataset.wear; saved.hat = S.hat; store.set("shortlegs.settings", Object.assign({}, saved)); renderArchive(); };
    $("arBack").onclick = () => show("menu");
  }

  // ═════════════════════════ The game ═════════════════════════
  let G = null;
  const MAP = buildMap();

  function tileAt(x, y) { const tx = Math.floor(x / TILE), ty = Math.floor(y / TILE); return (MAP.tiles[ty] || [])[tx] || "#"; }
  function regionAt(x, y) { const tx = Math.floor(x / TILE), ty = Math.floor(y / TILE); return (MAP.region[ty] || [])[tx] || null; }
  function passableTile(ch, tx, ty, jumping) {
    const tl = (MAP.tiles[ty] || [])[tx];
    if (!tl || tl === "#") return false;
    if (tl === "h") return !!jumping;
    if (tl === "S") return SM.canStepUp(ch.lies);
    return true;
  }
  const center = (tx, ty) => ({ x: tx * TILE + TILE / 2, y: ty * TILE + TILE / 2 });

  function bfs(ch, goal) {
    const sx = Math.floor(ch.x / TILE), sy = Math.floor(ch.y / TILE);
    const key = (x, y) => y * MAP.W + x, prev = new Map([[key(sx, sy), -1]]), q = [[sx, sy]];
    while (q.length) {
      const [x, y] = q.shift();
      if (x === goal[0] && y === goal[1]) {
        const path = []; let k = key(x, y);
        while (k !== -1 && k !== key(sx, sy)) { path.unshift([k % MAP.W, Math.floor(k / MAP.W)]); k = prev.get(k); }
        return path;
      }
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy, nk = key(nx, ny);
        if (prev.has(nk) || !passableTile(ch, nx, ny, false)) continue;
        prev.set(nk, key(x, y)); q.push([nx, ny]);
      }
    }
    return null;
  }

  function randomTileIn(room) {
    const r = ROOMS[room];
    for (let i = 0; i < 30; i++) {
      const x = r.x + Math.floor(rnd() * r.w), y = r.y + Math.floor(rnd() * r.h);
      if (MAP.tiles[y][x] === ".") return [x, y];
    }
    return [r.x + 1, r.y + 1];
  }

  function makeChar(base, tile, isPlayer) {
    const p = center(tile[0], tile[1]);
    return Object.assign({}, base, {
      x: p.x, y: p.y, lies: 0, legShown: 1, legFrom: 1, legTo: 1, legT: 1, ghost: false, isPlayer: !!isPlayer,
      path: null, goal: null, hold: rnd() * 2, walkT: 0, moving: false, back: false, jumpT: 0, speedBase: isPlayer ? 132 : 66,
      spoken: false, lines: [],
    });
  }

  function startGame(mode, opts) {
    Sound.init();
    closeAllModals();
    $("toast").innerHTML = ""; $("cluecard").hidden = true;
    const g = G = {
      mode, opts: opts || {}, time: 0, elapsed: 0, over: false, chars: [], clues: [], notebook: { clues: [], shrinks: [] },
      subs: [], subT: 0, curSub: null, phase: "play", wrong: 0, meetingsLeft: 1, lightsOut: 0, hintT: 0, events: {},
      maxLies: 4, lieReplay: [],
    };
    const player = makeChar(Object.assign({}, PLAYER, { name: S.lang === "de" ? "Du" : "You", hat: hatColorFor(S.hat), hatStyle: hatStyleFor(S.hat) }), [13, 15], true);
    g.player = player;
    g.chars.push(player);

    if (mode === "story") {
      const cs = g.case = storyCase();
      g.engine = new DeceptionEngine(cs.clues.map((c) => Object.assign({}, c)), false);
      for (const s of cs.suspects) {
        const base = CAST.find((c) => c.id === s.id);
        const ch = makeChar(base, s.tile, false);
        ch.lines = s.lines; ch.confession = s.confession; ch.home = "hall";
        g.chars.push(ch);
      }
      g.clues = cs.clues.map((c) => ({ id: c.id, tile: c.tile, name: c.name, desc: c.desc, found: false }));
      g.phase = "intro";
      showCard(tx(cs.title), tx(cs.briefing), () => { g.phase = "play"; say(null, S.lang === "de" ? "Befrage die drei Verdächtigen in der Großen Halle (E)." : "Question the three suspects in the Great Hall (E)."); });
    } else if (mode === "party") {
      g.maxLies = opts.maxLies;
      const seed = Math.floor(Math.random() * 2 ** 31);
      let st = seed; const rand = () => ((st = (st * 1103515245 + 12345) % 2147483648) / 2147483648);
      const pc = g.case = partyCase({ bots: opts.bots }, rand);
      g.seed = seed;
      g.engine = new DeceptionEngine(pc.clues.map((c) => Object.assign({}, c)), true);
      pc.bots.forEach((b, i) => { const ch = makeChar(b, [3 + i * 3, 11 + (i % 2) * 2], false); ch.home = null; g.chars.push(ch); });
      const used = new Set();
      g.clues = pc.clues.map((c) => {
        let [x, y] = c.tile;
        while (used.has(x + "," + y)) { const nt = randomTileIn(c.room); x = nt[0]; y = nt[1]; }
        used.add(x + "," + y);
        return { id: c.id, tile: [x, y], name: c.name, desc: c.desc, found: false };
      });
      g.totalTime = opts.minutes * 60;
      g.timeLeft = g.totalTime;
      g.meeting1At = g.totalTime * 0.4;
      g.finalAt = g.totalTime - Math.min(60, g.totalTime * 0.2);
      g.plantAt = g.totalTime * 0.5;
      g.lightsAt = g.totalTime * 0.65;
      g.phase = "intro";
      showCard(tx(pc.title), tx(pc.briefing), openAlibiWheel);
    } else {
      g.phase = "play";
      g.maxLies = 99;
      say(null, t("labHelp"));
    }
    show("game");
  }
  function closeAllModals() { for (const m of MODALS) closeModal(m); }

  const byId = (id) => G.chars.find((c) => c.id === id);
  const aliveChars = () => G.chars.filter((c) => !c.ghost);
  const nameOf = (id) => { const c = byId(id); return c ? c.name : id; };
  const roomLabel = (id) => (ROOMS[id] ? ROOMS[id][S.lang] : id);

  // ── Subtitles / voices ──
  function say(speaker, text, onShow) { G.subs.push({ speaker, text, onShow }); }
  function tickSubs(dt) {
    if (G.curSub) {
      G.subT -= dt;
      if (G.subT > 0) return;
      G.curSub = null; $("subs").hidden = true;
    }
    const next = G.subs.shift();
    if (!next) return;
    G.curSub = next;
    const words = String(next.text).split(/\s+/).length;
    G.subT = clamp(1.4 + words * 0.3, 2.2, 6);
    if (next.onShow) next.onShow();
    const ch = next.speaker ? byId(next.speaker) : null;
    if (S.subs) {
      $("subs").innerHTML = `<span>${ch ? `<b>${esc(ch.name)}:</b> ` : ""}${esc(next.text)}</span>`;
      $("subs").hidden = false;
    }
    if (ch) Sound.voice(ch.voice, ch.lies, next.text);
    speakTTS((ch ? ch.name + ": " : "") + next.text, ch ? ch.lies : 0);
  }

  // ── Lies & shrinking ──
  function applyLie(det) {
    const ch = byId(det.speaker);
    if (!ch || G.over) return;
    ch.lies += 1;
    ch.legFrom = ch.legShown; ch.legTo = SM.legScale(ch.lies); ch.legT = 0;
    Sound.boing(); Sound.alarm(); flash();
    const pct = Math.round(SM.legScale(ch.lies) * 100);
    toast(`${t("lie")} — ${t("shrunk", ch.name, pct)}${det.record && det.record.framed ? " " + t("framed") : ""}`, true);
    G.notebook.shrinks.push({ id: ch.id, lies: ch.lies, framed: !!(det.record && det.record.framed) });
    if (det.record) G.lieReplay.push({ speaker: ch.id, text: det.record.statement.text, clue: det.clueId, framed: det.record.framed });

    if (G.mode === "story") {
      const cs = G.case;
      if (ch.id === cs.culprit && ch.lies === 2) {
        ch.flee = true; ch.path = null; ch.goal = [22, 16]; ch.hold = 0.4;
        say(ch.id, S.lang === "de" ? "Ich… ich muss dringend in den Keller!" : "I… I urgently need to go to the cellar!");
        say(null, S.lang === "de" ? "Brumm flieht zur Kellertreppe, aber mit halben Beinen kommt er die Stufen nicht mehr hinunter!" : "Brumm runs for the cellar stairs, but with half his legs he can't get down the steps!");
      }
      if (ch.id === cs.culprit && ch.lies >= cs.liesUntilConfession) {
        say(ch.id, tx(ch.confession), null);
        G.pendingEnd = { win: true, reason: t("caught", ch.name) };
      }
      return;
    }
    if (G.mode === "party" && SM.isExposed(ch.lies, G.maxLies)) {
      if (ch.id === G.case.liar) { endGame(true, t("liarExposed", ch.name)); unlock("hats", "tophat"); }
      else if (ch.isPlayer) endGame(false, S.lang === "de" ? "Du bist über deine eigenen Lügen gestolpert." : "You tripped over your own lies.");
      else { ch.ghost = true; checkWipe(); }
    }
  }

  function discoverClue(clue, finder) {
    if (clue.found) return;
    clue.found = true;
    const exposed = G.engine.discover(clue.id);
    Sound.chime();
    G.notebook.clues.push(clue.id);
    unlock("lore", tx(clue.name));
    if (finder && !finder.isPlayer) toast((S.lang === "de" ? `${finder.name} hat etwas gefunden: ` : `${finder.name} found something: `) + tx(clue.name));
    else showClueCard(clue);
    for (const d of exposed) applyLie(d);
  }

  function showClueCard(clue) {
    const card = $("cluecard");
    card.innerHTML = `<span class="stamp">${S.lang === "de" ? "BEWEISSTÜCK" : "EXHIBIT"} · ${esc(roomLabel(regionAt(center(...clue.tile).x, center(...clue.tile).y)))}</span><b>${esc(tx(clue.name))}</b><p>${esc(tx(clue.desc))}</p>`;
    card.hidden = false;
    clearTimeout(showClueCard.t);
    showClueCard.t = setTimeout(() => { card.hidden = true; }, 7000);
    toast(t("found", tx(clue.name)));
  }

  function submitStatement(ch, line) {
    const text = tx(line);
    if (!line.claim) return null;
    const res = G.engine.submit({ speaker: ch.id, claim: line.claim, text });
    ch.statements = ch.statements || [];
    ch.statements.push(res.record.index);
    if (res.detection) applyLie(res.detection);
    if (G.talking === ch && !$("m-dialog").hidden) renderDialog();
    return res.record;
  }

  // ── Story: interrogation & presenting evidence ──
  function openDialog(ch) {
    G.talking = ch;
    if (G.mode === "story" && !ch.spoken) {
      ch.spoken = true;
      for (const l of ch.lines) say(ch.id, tx(l), () => submitStatement(ch, l));
    }
    renderDialog();
    openModal("dialog");
  }
  const dlgSel = { st: 0, cl: 0 };
  function renderDialog() {
    const ch = G.talking; if (!ch) return;
    const recs = (ch.statements || []).map((i) => G.engine.log[i]);
    const found = G.clues.filter((c) => c.found);
    const lines = recs.length
      ? recs.map((r) => `<li class="${r.exposed ? "x" + (r.framed ? " fr" : "") : ""}">${esc(r.statement.text)}</li>`).join("")
      : `<li class="note">${G.mode === "story" ? (S.lang === "de" ? "Hört zu…" : "Listening…") : t("noStatements")}</li>`;
    const canPresent = G.mode === "story" && recs.length > 0;
    dlgSel.st = clamp(dlgSel.st, 0, Math.max(0, recs.length - 1));
    dlgSel.cl = clamp(dlgSel.cl, 0, Math.max(0, found.length - 1));
    const profile = ch.isPlayer ? "" : `${t("shoe")}: ${ch.shoe} · ${t("coat")}: ${esc(tx(ch.coatName))}<br>${t("legs")}: ${Math.round(SM.legScale(ch.lies) * 100)} %${ch.ghost ? " · " + t("ghost") : ""}`;
    const wheel = !canPresent ? "" : found.length === 0 ? `<p class="note">${t("noEvidence")}</p>` : `
      <div class="wheel">
        <div class="ring"><span class="k">${t("pickStatement")}</span><button id="stPrev" aria-label="prev">‹</button><span class="v" id="stVal">${esc(recs[dlgSel.st].statement.text)}</span><button id="stNext" aria-label="next">›</button></div>
        <div class="ring"><span class="k">${t("pickClue")}</span><button id="clPrev" aria-label="prev">‹</button><span class="v" id="clVal">${esc(tx(found[dlgSel.cl].name))}</span><button id="clNext" aria-label="next">›</button></div>
        <div class="actions"><button class="btn primary" id="dlgPresent">${t("present")}</button></div>
      </div>`;
    $("dlgBox").innerHTML = `<div class="dlg"><canvas id="portrait" width="360" height="440"></canvas>
      <div class="stack"><h2>${esc(ch.name)}</h2><div class="profile">${profile}</div><ul class="lines">${lines}</ul></div></div>
      ${wheel}
      <div class="actions"><button class="btn" id="dlgLeave">${t("leave")}</button>${G.mode === "story" && G.mistakesShown !== false ? `<span class="chip" style="align-self:center">${t("mistakes")} ${G.wrong}/${G.case.wrongAllowed}</span>` : ""}</div>`;
    drawPortrait($("portrait"), ch);
    $("dlgLeave").onclick = () => { closeModal("dialog"); G.talking = null; };
    if (canPresent && found.length) {
      const n = recs.length, m = found.length;
      $("stPrev").onclick = () => { dlgSel.st = (dlgSel.st - 1 + n) % n; renderDialog(); };
      $("stNext").onclick = () => { dlgSel.st = (dlgSel.st + 1) % n; renderDialog(); };
      $("clPrev").onclick = () => { dlgSel.cl = (dlgSel.cl - 1 + m) % m; renderDialog(); };
      $("clNext").onclick = () => { dlgSel.cl = (dlgSel.cl + 1) % m; renderDialog(); };
      $("dlgPresent").onclick = () => presentEvidence(recs[dlgSel.st], found[dlgSel.cl]);
    }
  }
  function drawPortrait(c, ch) {
    const g = c.getContext("2d");
    g.clearRect(0, 0, c.width, c.height);
    drawChar(g, { x: 180, y: 420, s: 1.12, leg: ch.legShown, coat: ch.coat, hat: ch.hat, hatStyle: ch.hatStyle, beard: ch.beard, face: faceFor(ch.lies) });
  }

  function presentEvidence(rec, clue) {
    if (rec.exposed) { toast(S.lang === "de" ? "Diese Aussage ist schon widerlegt." : "That statement is already disproved."); return; }
    const det = G.engine.present(rec.index, clue.id);
    if (det) {
      const ch = byId(det.speaker);
      say(ch.id, S.lang === "de" ? ["Äh… das… das kann ich erklären!", "Wie bitte?! Das ist… unmöglich!", "N-nein… das… ähm…"][Math.min(2, ch.lies)] : ["Uh… I… I can explain!", "Excuse me?! That's… impossible!", "N-no… that… um…"][Math.min(2, ch.lies)]);
      applyLie(det);
    } else {
      G.wrong += 1;
      Sound.thud();
      const left = G.case.wrongAllowed - G.wrong;
      toast(`${t("notLie")} ${t("wrong", Math.max(0, left))}`, true);
      if (left <= 0) endGame(false, S.lang === "de" ? "Zu viele falsche Anschuldigungen. Der Fall wird dir entzogen." : "Too many false accusations. You're taken off the case.");
    }
    renderDialog();
  }

  // ── Party: alibi wheel, meetings, votes ──
  const wheelSel = { room: 0 };
  const alibiRooms = ["hall", "library", "bedroom", "kitchen", "greenhouse", "study", "shed", "garden"];
  function openAlibiWheel() {
    const render = () => {
      const room = alibiRooms[wheelSel.room];
      $("wheelBox").innerHTML = `<h2>${S.lang === "de" ? "Dein Alibi" : "Your alibi"}</h2>
        <p class="note">${S.lang === "de" ? "Evidence Binding Wheel: Baue deine Aussage. Die Wahrheit: Du warst in der Großen Halle. Lügen ist erlaubt, aber Lügen haben kurze Beine." : "Evidence Binding Wheel: build your statement. The truth: you were in the Great Hall. You may lie, but lies have short legs."}</p>
        <div class="wheel">
          <div class="ring"><span class="k">${S.lang === "de" ? "Subjekt" : "Subject"}</span><span></span><span class="v">${S.lang === "de" ? "Ich" : "I"}</span><span></span></div>
          <div class="ring"><span class="k">${S.lang === "de" ? "Aussage" : "Claim"}</span><span></span><span class="v">${S.lang === "de" ? "war im Raum" : "was in the"}</span><span></span></div>
          <div class="ring"><span class="k">${S.lang === "de" ? "Ort" : "Place"}</span><button id="wPrev" aria-label="prev">‹</button><span class="v">${esc(roomLabel(room))}</span><button id="wNext" aria-label="next">›</button></div>
          <div class="ring"><span class="k">${S.lang === "de" ? "Zeit" : "Time"}</span><span></span><span class="v">23:00</span><span></span></div>
        </div>
        <p class="preview">„${S.lang === "de" ? `Ich war um 23 Uhr im Raum ${roomLabel(room)}.` : `I was in the ${roomLabel(room).toLowerCase()} at 11 pm.`}“</p>
        <div class="actions"><button class="btn primary" id="wSay">${S.lang === "de" ? "Aussagen" : "State it"}</button></div>`;
      const n = alibiRooms.length;
      $("wPrev").onclick = () => { wheelSel.room = (wheelSel.room - 1 + n) % n; render(); };
      $("wNext").onclick = () => { wheelSel.room = (wheelSel.room + 1) % n; render(); };
      $("wSay").onclick = () => {
        closeModal("wheel");
        const room = alibiRooms[wheelSel.room];
        const line = { de: `Ich war um 23 Uhr im Raum „${ROOMS[room].de}“.`, en: `I was in the ${ROOMS[room].en.toLowerCase()} at 11 pm.`, claim: claim("you", "wasIn", room, 23) };
        G.phase = "alibis";
        say("you", tx(line), () => submitStatement(G.player, line));
        for (const s of G.case.statements.opening) say(s.speaker, tx(s.line), () => submitStatement(byId(s.speaker), s.line));
        say(null, S.lang === "de" ? "Die Ermittlung beginnt. Finde 3 echte Hinweise!" : "The investigation begins. Find 3 genuine clues!", () => { G.phase = "play"; });
      };
      focusFirst($("m-wheel"));
    };
    render();
    openModal("wheel");
  }

  function startMeeting(kind) {
    if (G.over || G.phase !== "play") return;
    G.phase = "meeting";
    G.meetingKind = kind;
    closeModal("dialog"); closeModal("notebook");
    let seat = 0;
    for (const ch of G.chars) {
      if (ch.ghost) continue;
      const p = center(4 + (seat % 10) * 2, 11 + Math.floor(seat / 10) * 2); seat++;
      ch.x = p.x; ch.y = p.y; ch.path = null; ch.hold = 1;
    }
    Sound.alarm();
    const title = kind === "final" ? t("finalMeeting") : t("meeting");
    say(null, `${title}! ${S.lang === "de" ? "Alle in die Große Halle." : "Everyone to the Great Hall."}`);
    const list = kind === "final" ? G.case.statements.final : kind === "m1" ? G.case.statements.meeting1 : [];
    for (const s of list) {
      const ch = byId(s.speaker);
      if (!ch || ch.ghost) continue;
      say(s.speaker, tx(s.line), () => submitStatement(ch, s.line));
    }
    say(null, t("vote"), () => { if (!G.over) openVote(); });
  }

  function openVote() {
    G.phase = "vote";
    const cands = aliveChars().filter((c) => !c.isPlayer);
    $("voteBox").innerHTML = `<h2>${t("vote")}</h2>
      <p class="note">${t("clueCount")}: ${G.engine.genuineCount()}/3 · ${S.lang === "de" ? "Mehrheit der Lebenden nötig." : "Majority of the living required."}</p>
      <div class="stack">${cands.map((c) => `<button class="btn" data-vote="${c.id}">${esc(c.name)} · ${"▮".repeat(Math.max(0, 4 - c.lies))}${"▯".repeat(Math.min(4, c.lies))}</button>`).join("")}
      <button class="btn" data-vote="">${t("skip")}</button></div>`;
    for (const b of $("voteBox").querySelectorAll("[data-vote]")) b.onclick = () => { closeModal("vote"); resolveVote(b.dataset.vote || null); };
    openModal("vote");
  }

  function botVote(bot) {
    const others = aliveChars().filter((c) => c !== bot);
    let top = Math.max(...others.map((c) => c.lies));
    if (top <= 0) return null;
    let pool = others.filter((c) => c.lies === top);
    if (bot.id === G.case.liar) {
      pool = others.filter((c) => c.id !== bot.id && c.lies === top);
      if (!pool.length) pool = others;
    }
    return pool[Math.floor(rnd() * pool.length)].id;
  }

  function resolveVote(playerVote) {
    const voters = aliveChars();
    const tally = new Map();
    const add = (id) => { if (id) tally.set(id, (tally.get(id) || 0) + 1); };
    add(playerVote);
    for (const b of voters) if (!b.isPlayer) add(botVote(b));
    let target = null;
    for (const [id, n] of tally) if (n * 2 > voters.length) target = id;
    const ch = target && byId(target);
    if (!ch) say(null, t("noMajority"));
    else if (ch.id === G.case.liar) {
      if (G.engine.genuineCount() >= 3) { say(null, t("caught", ch.name)); endGame(true, t("caught", ch.name)); return; }
      say(null, t("insufficient", ch.name));
    } else {
      ch.ghost = true;
      say(null, t("framedOut", ch.name));
      if (ch.isPlayer) { endGame(false, S.lang === "de" ? "Die anderen haben dich rausgewählt." : "The others voted you out."); return; }
      if (checkWipe()) return;
    }
    G.phase = "play";
  }

  function checkWipe() {
    const innocentsAlive = aliveChars().filter((c) => c.id !== G.case.liar).length;
    if (innocentsAlive <= 1) { endGame(false, t("wiped")); return true; }
    return false;
  }

  function endGame(win, reason) {
    if (G.over) return;
    G.over = true;
    G.phase = "over";
    closeAllModals();
    const unlocks = [];
    if (win && G.mode === "story") { if (unlock("hats", G.case.reward)) unlocks.push(tx(HATS.find((h) => h.id === G.case.reward))); unlock("cases", tx(G.case.title)); }
    if (win && G.mode === "party") { if (unlock("hats", "redcap")) unlocks.push(tx(HATS.find((h) => h.id === "redcap"))); unlock("cases", tx(G.case.title)); }
    const liar = G.mode === "party" ? byId(G.case.liar) : null;
    const replay = G.lieReplay.length
      ? G.lieReplay.map((r) => `<li>${esc(nameOf(r.speaker))}: „${esc(r.text)}“ <small>${r.clue ? "↯ " + esc(tx((G.clues.find((c) => c.id === r.clue) || {}).name || r.clue)) : ""} ${r.framed ? "· " + t("framed") : ""}</small></li>`).join("")
      : `<li class="note">—</li>`;
    setTimeout(() => {
      $("endBox").innerHTML = `<h2 style="color:${win ? "var(--ok)" : "var(--lie)"}">${win ? t("win") : t("lose")}</h2>
        <p>${esc(reason)}</p>
        ${liar ? `<p class="note">${S.lang === "de" ? "Der Lügner war" : "The Liar was"} <b>${esc(liar.name)}</b>.${G.case.framed ? ` ${S.lang === "de" ? "Reingelegt werden sollte" : "The frame-up targeted"} ${esc(nameOf(G.case.framed))}.` : ""}</p>` : ""}
        ${unlocks.map((u) => `<p class="chip" style="justify-self:start">${esc(t("unlocked", u))}</p>`).join("")}
        <div class="nb replay"><section><h3>${t("replay")}</h3><ul>${replay}</ul></section></div>
        <div class="actions"><button class="btn primary" id="endAgain">${t("again")}</button><button class="btn" id="endMenu">${t("menu")}</button></div>`;
      $("endAgain").onclick = () => startGame(G.mode, G.opts);
      $("endMenu").onclick = () => { closeAllModals(); G = null; show("menu"); };
      openModal("end");
    }, 1400);
  }

  function showCard(title, text, onGo) {
    $("cardBox").innerHTML = `<h2>${esc(title)}</h2><p>${esc(text)}</p><div class="actions"><button class="btn primary" id="cardGo">${S.lang === "de" ? "Los geht's" : "Let's go"}</button></div>`;
    $("cardGo").onclick = () => { closeModal("card"); onGo && onGo(); };
    openModal("card");
  }

  // ── Pause & notebook ──
  function openPause() {
    $("pauseBox").innerHTML = `<h2>${t("paused")}</h2>
      ${G && G.mode === "party" ? `<p class="note">${S.lang === "de" ? "Wie im Multiplayer läuft die Zeit weiter." : "Like in multiplayer, the clock keeps running."}</p>` : ""}
      <div class="stack"><button class="btn" id="pRes">[${t("resume")}]</button><button class="btn" id="pNb">[${t("notebook")}]</button><button class="btn" id="pOpt">[${t("options")}]</button><button class="btn" id="pAb">[${t("abandon")}]</button></div>`;
    $("pRes").onclick = () => closeModal("pause");
    $("pNb").onclick = () => { closeModal("pause"); openNotebook(true); };
    $("pOpt").onclick = () => { closeModal("pause"); settingsReturn = "pause"; screen = "settings"; $("scr-settings").hidden = false; $("hud").hidden = true; renderSettings(); focusFirst($("scr-settings")); };
    $("pAb").onclick = () => {
      $("pauseBox").innerHTML = `<h2>${t("abandon")}</h2><p class="note">${t("abandonQ")}</p><div class="actions"><button class="btn primary" id="abYes">${t("yes")}</button><button class="btn" id="abNo">${t("no")}</button></div>`;
      $("abYes").onclick = () => { closeAllModals(); G = null; show("menu"); };
      $("abNo").onclick = openPause;
      focusFirst($("m-pause"));
    };
    openModal("pause");
  }

  let nbFromPause = false;
  function openNotebook(fromPause) {
    nbFromPause = !!fromPause;
    const found = G.clues.filter((c) => c.found);
    const clues = found.length ? found.map((c) => `<li><b>${esc(tx(c.name))}</b><small>${esc(tx(c.desc))}</small></li>`).join("") : `<li class="note">${t("noClues")}</li>`;
    const sts = G.engine ? G.engine.log.map((r) => {
      const tag = r.exposed ? `<span class="tag ${r.framed ? "fake" : "x"}">${r.framed ? (S.lang === "de" ? "REINGELEGT?" : "FRAMED?") : (S.lang === "de" ? "WIDERLEGT" : "DISPROVED")}</span>` : `<span class="tag open">${S.lang === "de" ? "OFFEN" : "OPEN"}</span>`;
      return `<li>#${r.index + 1} <b>${esc(nameOf(r.statement.speaker))}</b>${tag}<small>„${esc(r.statement.text)}“</small></li>`;
    }).join("") : "";
    const legs = G.chars.map((c) => {
      const on = Math.round(SM.legScale(c.lies) * 4);
      const st = c.ghost ? t("ghost") : SM.mustCrawl(c.lies) ? t("crawl") : !SM.canStepUp(c.lies) ? t("noStairs") : !SM.canSprint(c.lies) ? t("noSprint") : t("fine");
      return `<div class="legrow"><span>${esc(c.name)} <small>${c.lies} · ${st}</small></span><span class="bar">${[0, 1, 2, 3].map((i) => `<i class="${i < on ? "" : "off"}"></i>`).join("")}</span></div>`;
    }).join("");
    const profiles = G.chars.filter((c) => !c.isPlayer).map((c) => `<li><b>${esc(c.name)}</b><small>${t("shoe")} ${c.shoe} · ${t("coat")} ${esc(tx(c.coatName))}</small></li>`).join("");
    const role = G.mode === "party" ? `<p class="chip" style="justify-self:start">${t("role")}: ${t("investigator")}</p>` : "";
    $("nbBox").innerHTML = `<h2>${S.lang === "de" ? "Notizbuch" : "Notebook"}</h2>${role}
      <div class="nb">
        <section><h3>${t("clues")} (${found.length})</h3><ul>${clues}</ul></section>
        <section><h3>${t("statements")}</h3><ul>${sts || `<li class="note">${t("noStatements")}</li>`}</ul></section>
        <section><h3>${t("legs")}</h3>${legs}</section>
        <section><h3>${t("profiles")}</h3><ul>${profiles}</ul></section>
      </div>
      <div class="actions"><button class="btn" id="nbClose">${t("back")}</button></div>`;
    $("nbClose").onclick = closeNotebook;
    openModal("notebook");
  }
  function closeNotebook() { closeModal("notebook"); if (nbFromPause) openPause(); }

  function onKey(e) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      if (screen !== "game" || anyModal()) { if (navFocus(e.key === "ArrowDown" ? 1 : -1)) e.preventDefault && e.preventDefault(); return; }
    }
    if ((e.key === "Enter" || e.code === "KeyE") && e.synthetic && document.activeElement && document.activeElement.tagName === "BUTTON" && (screen !== "game" || anyModal())) { document.activeElement.click(); return; }
    if (e.key === "Escape") {
      if (!$("m-notebook").hidden) return closeNotebook();
      if (!$("m-dialog").hidden) { closeModal("dialog"); if (G) G.talking = null; return; }
      if (!$("m-quit").hidden) return closeModal("quit");
      if (!$("m-pause").hidden) return closeModal("pause");
      if (screen === "settings") return $("setBack").click();
      if (screen === "lobby" || screen === "archive") return show("menu");
      if (screen === "game" && G && !G.over && !anyModal()) return openPause();
      return;
    }
    if (screen !== "game" || !G) return;
    if (e.key === "Tab") {
      if (!$("m-notebook").hidden) closeNotebook();
      else if (!anyModal() && G.phase !== "intro") openNotebook(false);
      return;
    }
    if (anyModal()) return;
    if (e.code === "KeyM" && G.mode === "party" && G.phase === "play" && G.meetingsLeft > 0) { G.meetingsLeft--; toast(t("meetingCalled")); startMeeting("emergency"); }
    if (G.mode === "lab" && e.code === "KeyL") applyLie({ speaker: "you", record: null });
    if (G.mode === "lab" && e.code === "KeyR") { const p = G.player; p.lies = 0; p.legFrom = p.legShown; p.legTo = 1; p.legT = 0; }
  }

  // ═════════════════════════ Update ═════════════════════════
  const BOX = { w: 9, h: 5 };
  function blockedAt(ch, x, y, jumping) {
    for (const [dx, dy] of [[-BOX.w, -BOX.h], [BOX.w, -BOX.h], [-BOX.w, BOX.h], [BOX.w, BOX.h]]) {
      const tx = Math.floor((x + dx) / TILE), ty = Math.floor((y + dy) / TILE);
      if (!passableTile(ch, tx, ty, jumping)) return (MAP.tiles[ty] || [])[tx] || "#";
    }
    return null;
  }
  function moveChar(ch, vx, vy, dt, jumping) {
    let blocked = null;
    const nx = ch.x + vx * dt, ny = ch.y + vy * dt;
    const bx = blockedAt(ch, nx, ch.y, jumping); if (!bx) ch.x = nx; else blocked = bx;
    const by = blockedAt(ch, ch.x, ny, jumping); if (!by) ch.y = ny; else blocked = blocked || by;
    return blocked;
  }

  function updatePlayer(dt, pad) {
    const p = G.player;
    if (p.ghost) return;
    const inp = moveInput(pad);
    const frozenInput = anyModal() || G.phase === "meeting" || G.phase === "vote" || G.phase === "intro";
    const lies = p.lies;
    if (hit.has("Space") && !frozenInput) {
      if (SM.canJump(lies) && p.jumpT <= 0) { p.jumpT = 0.55; Sound.hop(); }
      else if (!SM.canJump(lies)) hint(S.lang === "de" ? "Ab 3 Lügen kannst du nicht mehr springen." : "At 3 lies you can't jump any more.");
    }
    p.jumpT = Math.max(0, p.jumpT - dt);
    const sprint = inp.sprint && SM.canSprint(lies);
    if (inp.sprint && !SM.canSprint(lies) && (inp.x || inp.y)) hint(S.lang === "de" ? "Mit einer Lüge auf dem Konto: kein Sprint." : "With a lie on record: no sprinting.");
    const speed = SM.speed(p.speedBase, lies) * (sprint ? 1.6 : 1);
    const vx = frozenInput ? 0 : inp.x * speed, vy = frozenInput ? 0 : inp.y * speed;
    p.moving = Math.abs(vx) + Math.abs(vy) > 1;
    if (p.moving) { p.walkT += dt * (sprint ? 1.6 : 1); p.back = vy < -Math.abs(vx) * 0.5; }
    const blocked = p.moving ? moveChar(p, vx, vy, dt, p.jumpT > 0) : null;
    if (blocked === "S") hint(t("stairsBlocked"));
    if (blocked === "h") hint(SM.canJump(lies) ? t("hedgeBlocked") : (S.lang === "de" ? "Die Hecke ist zu hoch, und springen kannst du nicht mehr." : "The hedge is too high, and you can't jump any more."));
  }

  function hint(msg) {
    if (G.hintT > 0 && G.lastHint === msg) return;
    G.hintT = 3; G.lastHint = msg; toast(msg);
  }

  function updateNPC(ch, dt) {
    if (ch.ghost || ch.isPlayer) return;
    if (G.phase === "meeting" || G.phase === "vote" || G.phase === "intro") { ch.moving = false; return; }
    if (ch.hold > 0) { ch.hold -= dt; ch.moving = false; return; }
    if (!ch.path || !ch.path.length) {
      let goal = ch.goal;
      if (!goal) {
        if (G.mode === "story") goal = ch.flee ? [22, 16] : randomTileIn("hall");
        else { const rooms = ["hall", "library", "bedroom", "kitchen", "greenhouse", "study", "shed", "garden"]; if (rnd() < 0.15) rooms.push("cellar"); goal = randomTileIn(rooms[Math.floor(rnd() * rooms.length)]); }
      }
      ch.goal = null;
      const path = bfs(ch, goal);
      if (!path || !path.length) { ch.hold = 1 + rnd() * 2; ch.moving = false; if (ch.flee) ch.stuck = true; return; }
      ch.path = path;
    }
    const [nx, ny] = ch.path[0];
    if (!passableTile(ch, nx, ny, false)) { ch.path = null; ch.hold = 0.8; return; }
    const tgt = center(nx, ny), dx = tgt.x - ch.x, dy = tgt.y - ch.y, d = Math.hypot(dx, dy);
    const sp = SM.speed(ch.speedBase, ch.lies);
    if (sp <= 0) { ch.moving = false; return; }
    if (d < 2) { ch.path.shift(); if (!ch.path.length) ch.hold = ch.flee ? 999 : 1 + rnd() * 3.5; return; }
    ch.x += (dx / d) * sp * dt; ch.y += (dy / d) * sp * dt;
    ch.moving = true; ch.walkT += dt; ch.back = dy < -Math.abs(dx) * 0.5;

    // Investigator bots search clue spots they walk over (after the first quarter of the match).
    if (G.mode === "party" && ch.id !== G.case.liar && G.elapsed > G.totalTime * 0.25) {
      const tx = Math.floor(ch.x / TILE), ty = Math.floor(ch.y / TILE);
      const clue = G.clues.find((c) => !c.found && c.tile[0] === tx && c.tile[1] === ty);
      if (clue && rnd() < dt * 0.9) discoverClue(clue, ch);
    }
  }

  function nearestInteractable() {
    const p = G.player;
    let best = null, bd = 1e9;
    for (const c of G.clues) {
      if (c.found) continue;
      const q = center(c.tile[0], c.tile[1]), d = Math.hypot(q.x - p.x, q.y - p.y);
      if (d < 38 && d < bd) { bd = d; best = { kind: "clue", obj: c }; }
    }
    for (const ch of G.chars) {
      if (ch.isPlayer || ch.ghost) continue;
      const d = Math.hypot(ch.x - p.x, ch.y - p.y);
      if (d < 46 && d < bd) { bd = d; best = { kind: "char", obj: ch }; }
    }
    return best;
  }

  function update(dt, pad) {
    const g = G;
    g.hintT = Math.max(0, g.hintT - dt);
    const frozen = g.over || g.phase === "intro" || (g.mode !== "party" && anyModal());
    tickSubs(dt);
    if (!frozen) {
      g.time += dt;
      if (g.mode === "party" && g.phase !== "intro" && g.phase !== "alibis") {
        g.elapsed += dt;
        g.timeLeft = Math.max(0, g.totalTime - g.elapsed);
        if (!g.events.m1 && g.elapsed >= g.meeting1At && g.phase === "play") { g.events.m1 = true; startMeeting("m1"); }
        if (!g.events.final && g.elapsed >= g.finalAt && g.phase === "play") { g.events.final = true; startMeeting("final"); }
        if (!g.events.plant && g.elapsed >= g.plantAt && !byId(g.case.liar).ghost) {
          g.events.plant = true;
          const pl = g.case.planted, tile = g.clues.some((c) => c.tile[0] === pl.tile[0] && c.tile[1] === pl.tile[1]) ? randomTileIn("hall") : pl.tile;
          g.engine.register(Object.assign({}, pl));
          g.clues.push({ id: pl.id, tile, name: pl.name, desc: pl.desc, found: false });
        }
        if (!g.events.lights && g.elapsed >= g.lightsAt && g.phase === "play") { g.events.lights = true; g.lightsOut = 15; toast(t("lightsOut"), true); }
        if (g.timeLeft <= 0 && g.phase === "play") endGame(false, t("timeUp"));
      }
      g.lightsOut = Math.max(0, g.lightsOut - dt);
      updatePlayer(dt, pad);
      for (const ch of g.chars) updateNPC(ch, dt);
    }
    if (g.pendingEnd && !g.subs.length && !g.curSub) { const e = g.pendingEnd; g.pendingEnd = null; endGame(e.win, e.reason); }
    // Leg tween ("boing", 0.6 s, overshoot) runs even while frozen so shrinks are visible in dialogs.
    for (const ch of g.chars) {
      if (ch.legT < 1) {
        ch.legT = Math.min(1, ch.legT + dt / 0.6);
        const k = ch.legT - 1, s = 1.9, e = k * k * ((s + 1) * k + s) + 1;
        ch.legShown = ch.legFrom + (ch.legTo - ch.legFrom) * e;
        if (G.talking === ch && !$("m-dialog").hidden) drawPortrait($("portrait"), ch);
      }
    }
    const near = !anyModal() && g.phase === "play" ? nearestInteractable() : null;
    const pr = $("prompt");
    pr.hidden = !near;
    if (near) pr.textContent = near.kind === "clue" ? t("search") : `${t("talk")} · ${near.obj.name}`;
    if (hit.has("KeyE") && near && !anyModal()) {
      if (near.kind === "clue") discoverClue(near.obj, g.player); else openDialog(near.obj);
    }
    renderHud();
  }

  function renderHud() {
    const g = G, L = [], R = [];
    if (g.mode === "party") {
      const m = Math.floor(g.timeLeft / 60), s = Math.floor(g.timeLeft % 60);
      const phase = g.phase === "meeting" || g.phase === "vote" ? (g.meetingKind === "final" ? t("finalMeeting") : t("meeting")) : g.phase === "alibis" ? t("alibis") : g.lightsOut > 0 ? t("lightsOut") : t("investigate");
      L.push(`<span class="chip"><small>${t("time")}</small>${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}</span>`, `<span class="chip"><small>PHASE</small>${phase}</span>`);
      R.push(`<span class="chip"><small>${t("clueCount")}</small>${g.engine.genuineCount()}/3</span>`);
      if (g.meetingsLeft > 0) R.push(`<span class="chip"><small>${t("ctrlMeet")}</small>M</span>`);
    } else if (g.mode === "story") {
      L.push(`<span class="chip"><small>${S.lang === "de" ? "FALL 1" : "CASE 1"}</small>Money Tree</span>`);
      R.push(`<span class="chip"><small>${t("clues")}</small>${g.clues.filter((c) => c.found).length}/${g.clues.length}</span>`, `<span class="chip"><small>${t("mistakes")}</small>${g.wrong}/${g.case.wrongAllowed}</span>`);
    } else {
      const lies = g.player.lies;
      L.push(`<span class="chip"><small>${t("labTitle")}</small>L / R</span>`);
      R.push(`<span class="chip"><small>SPEED</small>${Math.round(SM.legScale(lies) * 100)} %</span>`, `<span class="chip"><small>PITCH</small>${SM.voicePitch(lies).toFixed(2)}×</span>`);
    }
    const hl = L.join(""), hr = R.join("");
    if ($("hudL").innerHTML !== hl) $("hudL").innerHTML = hl;
    if ($("hudR").innerHTML !== hr) $("hudR").innerHTML = hr;
    const p = g.player, on = Math.round(SM.legScale(p.lies) * 4);
    const flags = [SM.canSprint(p.lies) ? "SPRINT ✓" : "SPRINT ✗", SM.canStepUp(p.lies) ? (S.lang === "de" ? "TREPPE ✓" : "STAIRS ✓") : (S.lang === "de" ? "TREPPE ✗" : "STAIRS ✗"), SM.canJump(p.lies) ? (S.lang === "de" ? "SPRUNG ✓" : "JUMP ✓") : (S.lang === "de" ? "SPRUNG ✗" : "JUMP ✗")].join(" · ");
    const lg = `<small>${t("legs")} · ${p.lies} ${S.lang === "de" ? "Lügen" : "lies"}</small><div class="bar">${[0, 1, 2, 3].map((i) => `<i class="${i < on ? "" : "off"}"></i>`).join("")}</div><small style="margin-top:4px">${flags}</small>`;
    if ($("legs").innerHTML !== lg) $("legs").innerHTML = lg;
  }

  // ═════════════════════════ Render ═════════════════════════
  const cam = { x: 0, y: 0, z: 1.6 };

  function drawWorld(dtVisual) {
    const g = G, p = g.player;
    cam.z = clamp(Math.min(VW / (19 * TILE), VH / (12 * TILE)), 0.9, 2.2);
    const vw = VW / cam.z, vh = VH / cam.z;
    const tx = clamp(p.x - vw / 2, -TILE * 2, MAP.W * TILE - vw + TILE * 2), ty = clamp(p.y - vh / 2 - 20, -TILE * 2, MAP.H * TILE - vh + TILE * 2);
    cam.x += (tx - cam.x) * Math.min(1, dtVisual * 8); cam.y += (ty - cam.y) * Math.min(1, dtVisual * 8);
    ctx.setTransform(DPR * cam.z, 0, 0, DPR * cam.z, -cam.x * DPR * cam.z, -cam.y * DPR * cam.z);
    ctx.fillStyle = "#07080a"; ctx.fillRect(cam.x - 10, cam.y - 10, vw + 20, vh + 20);

    const x0 = Math.max(0, Math.floor(cam.x / TILE)), y0 = Math.max(0, Math.floor(cam.y / TILE));
    const x1 = Math.min(MAP.W - 1, Math.ceil((cam.x + vw) / TILE)), y1 = Math.min(MAP.H - 1, Math.ceil((cam.y + vh) / TILE));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) drawTile(x, y);
    drawDecor();

    // clues
    for (const c of g.clues) {
      const q = center(c.tile[0], c.tile[1]);
      if (c.found) { ctx.fillStyle = "rgba(255,255,255,.12)"; ell(ctx, q.x, q.y + 6, 8, 4); ctx.fill(); continue; }
      const pulse = 0.6 + Math.sin(g.time * 4 + c.tile[0]) * 0.25;
      ctx.fillStyle = `rgba(255,214,120,${0.25 * pulse})`; ell(ctx, q.x, q.y + 4, 14, 7); ctx.fill();
      ctx.strokeStyle = P("#ffd678"); ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(q.x - 2, q.y - 4, 5, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(q.x + 2, q.y); ctx.lineTo(q.x + 7, q.y + 5); ctx.stroke();
    }

    // characters, y-sorted
    const list = g.chars.slice().sort((a, b) => a.y - b.y);
    for (const ch of list) {
      const stride = ch.moving ? Math.sin(ch.walkT * 12) * 22 : 0;
      const lift = ch.jumpT > 0 ? Math.sin((1 - ch.jumpT / 0.55) * Math.PI) * 60 : 0;
      drawChar(ctx, { x: ch.x, y: ch.y + 6, s: 0.2, leg: ch.legShown, coat: ch.coat, hat: ch.hat, hatStyle: ch.hatStyle, beard: ch.beard, face: faceFor(ch.lies), back: ch.back, stride, lift, alpha: ch.ghost ? 0.3 : 1 });
      if (!ch.isPlayer && !ch.ghost && Math.hypot(ch.x - p.x, ch.y - p.y) < 120) {
        ctx.font = "700 8px Figtree, system-ui, sans-serif"; ctx.textAlign = "center";
        const label = ch.name + (ch.lies ? " " + "▼".repeat(ch.lies) : "");
        const w = ctx.measureText(label).width + 8, top = ch.y + 6 - 0.2 * (8 + 78 * ch.legShown + 172) - 14;
        ctx.fillStyle = "rgba(8,9,11,.8)"; rr(ctx, ch.x - w / 2, top - 8, w, 11, 3); ctx.fill();
        ctx.fillStyle = ch.lies ? LIE : "#ffffff"; ctx.fillText(label, ch.x, top);
      }
    }

    // warm lamp glows
    ctx.globalCompositeOperation = "lighter";
    for (const [lx, ly, r, a] of [[13.5 * TILE, 9.6 * TILE, 150, 0.22], [3 * TILE, 2 * TILE, 90, 0.18], [22 * TILE, 21 * TILE, 110, 0.18], [36 * TILE, 3 * TILE, 80, 0.12]]) {
      const gr = ctx.createRadialGradient(lx, ly, 4, lx, ly, r);
      gr.addColorStop(0, S.art === "noir" ? `rgba(255,255,255,${a})` : `rgba(255,154,60,${a})`); gr.addColorStop(1, "rgba(0,0,0,0)");
      ctx.fillStyle = gr; ctx.fillRect(lx - r, ly - r, r * 2, r * 2);
    }
    ctx.globalCompositeOperation = "source-over";

    // falling snow over the garden
    ctx.fillStyle = "rgba(255,255,255,.7)";
    for (let i = 0; i < 40; i++) {
      const sx = ((i * 97) % (16 * TILE)) + TILE, sy = (((i * 53) + g.time * 30 * (1 + (i % 3) * 0.3)) % (7 * TILE)) + 18 * TILE;
      ell(ctx, sx + Math.sin(g.time + i) * 4, sy, 1.3, 1.3); ctx.fill();
    }

    ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
    if (g.lightsOut > 0) {
      const sx = (p.x - cam.x) * cam.z, sy = (p.y - cam.y) * cam.z;
      const gr = ctx.createRadialGradient(sx, sy, 30, sx, sy, 170);
      gr.addColorStop(0, "rgba(0,0,0,0)"); gr.addColorStop(1, "rgba(0,0,0,.94)");
      ctx.fillStyle = gr; ctx.fillRect(0, 0, VW, VH);
    }
    // vignette
    const vg = ctx.createRadialGradient(VW / 2, VH / 2, Math.min(VW, VH) * 0.35, VW / 2, VH / 2, Math.max(VW, VH) * 0.75);
    vg.addColorStop(0, "rgba(0,0,0,0)"); vg.addColorStop(1, anyModal() ? "rgba(0,0,0,.55)" : "rgba(0,0,0,.35)");
    ctx.fillStyle = vg; ctx.fillRect(0, 0, VW, VH);
  }

  function drawTile(x, y) {
    const tl = MAP.tiles[y][x], reg = MAP.region[y][x], X = x * TILE, Y = y * TILE;
    if (tl === "#") {
      const below = (MAP.tiles[y + 1] || [])[x];
      ctx.fillStyle = P("#1c1f24"); ctx.fillRect(X, Y, TILE, TILE);
      if (below && below !== "#") { ctx.fillStyle = P("#3a3430"); ctx.fillRect(X, Y + TILE * 0.35, TILE, TILE * 0.65); ctx.fillStyle = P("#4a423c"); ctx.fillRect(X, Y + TILE * 0.35, TILE, 3); }
      return;
    }
    const room = ROOMS[reg] || ROOMS.hall;
    let col = room.floor;
    if (room.checker && (x + y) % 2) col = "#8e877a";
    ctx.fillStyle = P(col); ctx.fillRect(X, Y, TILE, TILE);
    if (!room.snow && !room.checker && reg !== "cellar") { ctx.fillStyle = "rgba(0,0,0,.08)"; ctx.fillRect(X, Y + TILE - 2, TILE, 2); if (reg !== "greenhouse") ctx.fillRect(X + ((y % 2) ? 12 : 26), Y, 1.5, TILE); }
    if (room.snow) { ctx.fillStyle = "rgba(111,147,166,.18)"; ell(ctx, X + ((x * 7) % 20) + 6, Y + ((y * 11) % 18) + 7, 5, 2); ctx.fill(); }
    if (tl === "d") { ctx.fillStyle = P("#5a3a22"); ctx.fillRect(X + 2, Y + 2, TILE - 4, TILE - 4); ctx.fillStyle = "rgba(255,255,255,.06)"; ctx.fillRect(X + 2, Y + 2, TILE - 4, 3); }
    if (tl === "S") {
      ctx.fillStyle = P("#4a3a2c"); ctx.fillRect(X, Y, TILE, TILE);
      for (let i = 0; i < 4; i++) { ctx.fillStyle = i % 2 ? P("#6a5440") : P("#574634"); ctx.fillRect(X, Y + i * 8, TILE, 8); }
      ctx.fillStyle = "rgba(0,0,0,.25)"; ctx.fillRect(X, Y + TILE - 3, TILE, 3);
    }
    if (tl === "h") {
      ctx.fillStyle = P("#2f4a2a"); ctx.fillRect(X + 2, Y + 4, TILE - 4, TILE - 6);
      ctx.fillStyle = P("#3f6a36"); for (const [a, b] of [[8, 10], [20, 9], [14, 18], [24, 20]]) { ell(ctx, X + a, Y + b, 8, 7); ctx.fill(); }
      ctx.fillStyle = "rgba(255,255,255,.75)"; ell(ctx, X + 12, Y + 6, 7, 3); ctx.fill(); ell(ctx, X + 23, Y + 7, 5, 2.5); ctx.fill();
    }
  }

  function box(x, y, w, h, col, top) { ctx.fillStyle = P(col); ctx.fillRect(x, y, w, h); if (top) { ctx.fillStyle = P(top); ctx.fillRect(x, y, w, Math.min(6, h)); } }
  function drawDecor() {
    const T_ = TILE;
    // study: desk, empty pedestal
    box(6 * T_, 1.2 * T_, 3.5 * T_, 1.1 * T_, "#3b2616", "#553621");
    box(2.2 * T_, 1.6 * T_, 1.2 * T_, 1.2 * T_, "#8a8f96", "#b5bac0");
    ctx.fillStyle = "rgba(255,45,85,.55)"; ctx.font = "700 9px JetBrains Mono, monospace"; ctx.textAlign = "center"; ctx.fillText("?", 2.8 * T_, 2.4 * T_);
    // library shelves
    for (let i = 0; i < 5; i++) { box((12.3 + i * 2.3) * T_, 1.05 * T_, 2 * T_, 0.8 * T_, "#3a2418", "#4a2e1f"); for (let b = 0; b < 7; b++) { ctx.fillStyle = P(["#7a2b2b", "#2d4a6a", "#6b5a2a", "#3f6a36"][b % 4]); ctx.fillRect((12.4 + i * 2.3) * T_ + b * 8.5, 1.15 * T_, 6, 0.55 * T_); } }
    // bedroom bed
    box(26 * T_, 1.4 * T_, 2.4 * T_, 3 * T_, "#e8e1d0", "#ffffff"); box(26 * T_, 3.2 * T_, 2.4 * T_, 1.2 * T_, "#6a2c4a");
    // kitchen counters + cake
    box(33.2 * T_, 1.1 * T_, 5.6 * T_, 0.9 * T_, "#9aa3aa", "#c4ccd2"); ctx.fillStyle = P("#6b3f2a"); ell(ctx, 35 * T_, 1.5 * T_, 10, 7); ctx.fill(); ctx.fillStyle = "#ffffff"; ell(ctx, 35 * T_, 1.4 * T_, 9, 4); ctx.fill();
    // hall: rug, fireplace, coat rack
    box(4 * T_, 11 * T_, 19 * T_, 4 * T_, "#7a2b2b"); ctx.strokeStyle = P("#c9a227"); ctx.lineWidth = 2; ctx.strokeRect(4.3 * T_, 11.3 * T_, 18.4 * T_, 3.4 * T_);
    box(12 * T_, 9.05 * T_, 3 * T_, 0.9 * T_, "#2a2a2a", "#555555"); ctx.fillStyle = P("#ff9a3c"); ell(ctx, 13.5 * T_, 9.6 * T_, 16 + Math.sin(G.time * 9) * 2, 7); ctx.fill();
    // greenhouse plants
    for (let i = 0; i < 6; i++) { ctx.fillStyle = P("#3f6a36"); ell(ctx, (29.5 + i * 1.6) * T_, 9.9 * T_, 13, 11); ctx.fill(); ctx.fillStyle = P("#7a4a2a"); ctx.fillRect((29.5 + i * 1.6) * T_ - 6, 10.2 * T_, 12, 8); }
    // cellar barrels
    for (let i = 0; i < 4; i++) { ctx.fillStyle = P("#5a3a22"); ell(ctx, (19.5 + i * 1.2) * T_, 23.8 * T_, 15, 12); ctx.fill(); ctx.strokeStyle = P("#2a1d17"); ctx.lineWidth = 2; ctx.stroke(); }
    // shed workbench
    box(29 * T_, 18.2 * T_, 5 * T_, 0.9 * T_, "#5a3a22", "#7a5a3a"); box(36 * T_, 18.2 * T_, 2.5 * T_, 2.2 * T_, "#3a3f47", "#4a5058");
    // room labels
    ctx.font = "700 9px JetBrains Mono, monospace"; ctx.textAlign = "left";
    for (const [id, r] of Object.entries(ROOMS)) { ctx.fillStyle = r.snow || r.checker ? "rgba(0,0,0,.45)" : "rgba(255,255,255,.35)"; ctx.fillText(r[S.lang].toUpperCase(), r.x * T_ + 6, (r.y + r.h) * T_ - 6); }
  }

  // Main menu backdrop: interrogation room, silhouette's legs shrink on every lie-detector flash.
  const menuFx = { t: 0, step: 0, next: 2.5, flash: 0, leg: 1, from: 1, to: 1, k: 1 };
  function drawMenu(dt) {
    ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
    const reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
    menuFx.t += dt;
    if (!reduce && menuFx.t > menuFx.next) {
      menuFx.t = 0; menuFx.next = 4 + rnd() * 3;
      menuFx.step = (menuFx.step + 1) % 4;
      menuFx.from = menuFx.leg; menuFx.to = [1, 0.75, 0.5, 0.25][menuFx.step]; menuFx.k = 0;
      menuFx.flash = S.flash ? 0.3 : 1;
      if (Sound.ctx && screen === "menu") Sound.tone("sfx", "square", 660 * SM.voicePitch(menuFx.step), 0, 0.12, 0.08);
      const ll = $("logoLegs"); if (ll) ll.style.transform = `scaleY(${Math.max(0.45, menuFx.to)})`;
    }
    if (menuFx.k < 1) { menuFx.k = Math.min(1, menuFx.k + dt / 0.6); const k = menuFx.k - 1, e = k * k * (2.9 * k + 1.9) + 1; menuFx.leg = menuFx.from + (menuFx.to - menuFx.from) * e; }
    menuFx.flash = Math.max(0, menuFx.flash - dt * 2.2);

    const bg = ctx.createRadialGradient(VW * 0.68, VH * 0.35, 20, VW * 0.68, VH * 0.35, Math.max(VW, VH) * 0.8);
    bg.addColorStop(0, "#23262b"); bg.addColorStop(1, "#08090b");
    ctx.fillStyle = bg; ctx.fillRect(0, 0, VW, VH);
    const floorY = VH * 0.86;
    ctx.fillStyle = "#101216"; ctx.fillRect(0, floorY, VW, VH - floorY);
    // lamp cone
    const lx = VW * 0.68;
    const cone = ctx.createLinearGradient(0, 0, 0, floorY);
    cone.addColorStop(0, S.art === "noir" ? "rgba(255,255,255,.18)" : "rgba(255,154,60,.22)"); cone.addColorStop(1, "rgba(255,154,60,0)");
    ctx.fillStyle = cone; ctx.beginPath(); ctx.moveTo(lx - 20, VH * 0.12); ctx.lineTo(lx + 20, VH * 0.12); ctx.lineTo(lx + VW * 0.2, floorY); ctx.lineTo(lx - VW * 0.2, floorY); ctx.fill();
    ctx.strokeStyle = "#000"; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(lx, 0); ctx.lineTo(lx, VH * 0.1); ctx.stroke();
    ctx.fillStyle = "#2a2a2a"; ctx.beginPath(); ctx.moveTo(lx - 28, VH * 0.13); ctx.quadraticCurveTo(lx, VH * 0.06, lx + 28, VH * 0.13); ctx.fill();
    // lie detector light
    ctx.fillStyle = `rgba(255,45,85,${0.25 + menuFx.flash * 0.75})`; ell(ctx, VW * 0.9, VH * 0.2, 12, 12); ctx.fill();
    if (menuFx.flash > 0) { const fl = ctx.createRadialGradient(VW * 0.9, VH * 0.2, 5, VW * 0.9, VH * 0.2, VW * 0.5); fl.addColorStop(0, `rgba(255,45,85,${0.45 * menuFx.flash})`); fl.addColorStop(1, "rgba(255,45,85,0)"); ctx.fillStyle = fl; ctx.fillRect(0, 0, VW, VH); }
    // chair + silhouette
    const s = clamp(VH / 620, 0.55, 1.4);
    ctx.fillStyle = "#16181b"; ctx.fillRect(lx - 55 * s, floorY - 110 * s, 10 * s, 110 * s); ctx.fillRect(lx + 45 * s, floorY - 110 * s, 10 * s, 110 * s);
    drawChar(ctx, { x: lx, y: floorY, s, leg: menuFx.leg, coat: "#000000", hat: "#000000", fill: "#040405" });
  }

  // ═════════════════════════ Main loop ═════════════════════════
  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    const pad = pollPad();
    if (screen === "game" && G) {
      update(dt, pad);
      drawWorld(dt);
    } else {
      drawMenu(dt);
    }
    hit.clear();
    requestAnimationFrame(frame);
  }

  // Clicking the world = interact (spec: LEFT CLICK interacts).
  cv.addEventListener("click", () => { if (screen === "game" && G && !anyModal()) hit.add("KeyE"); });

  resize();
  applySettings();
  show("menu");
  requestAnimationFrame(frame);
})();
