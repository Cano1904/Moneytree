// SHORT LEGS – intro film: the backstory, played in-engine with captions (skippable).
(function (global) {
  "use strict";
  const R = global.R3;
  const I = {};
  const END = 48;
  const ease = (x) => (x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x));
  const lerp = (a, b, k) => a + (b - a) * k;
  const V = (a, b, k) => [lerp(a[0], b[0], k), lerp(a[1], b[1], k), lerp(a[2], b[2], k)];
  const seg = (t, t0, t1) => ease((t - t0) / (t1 - t0));

  const CAPTIONS = [
    { t0: 0.8, t1: 7.6, de: "Winter 1952. Hoch im verschneiten Norden steht das Herrenhaus der Familie Brumm.", en: "Winter, 1952. High in the snowy north stands the manor of the Brumm family." },
    { t0: 8.4, t1: 14.6, de: "Sein größter Schatz: der Money Tree – ein Bonsai aus purem Gold. Seit hundert Jahren bringt er der Familie Glück.", en: "Its greatest treasure: the Money Tree, a bonsai of solid gold. For a hundred years it has brought the family luck." },
    { t0: 15.2, t1: 22.8, chap: "23:07", de: "Um 23:07 Uhr gehen im ganzen Haus die Lichter aus. Als sie wieder angehen, ist der Money Tree verschwunden.", en: "At 11:07 pm every light in the house goes out. When they come back on, the Money Tree is gone." },
    { t0: 23.4, t1: 30.8, de: "Drei Gäste waren in dieser Nacht im Haus: Baron Brumm, Madame Velours und Kip, der Gärtner. Alle schwören, sie hätten nichts gesehen.", en: "Three guests were in the house that night: Baron Brumm, Madame Velours and Kip the gardener. All of them swear they saw nothing." },
    { t0: 31.4, t1: 33.6, speaker: "Butler Jeeves", de: "„Ich habe noch nie vom Tafelsilber genascht!“", en: "“I have never pinched the silverware!”" },
    { t0: 34.4, t1: 39.4, de: "Doch auf diesem Haus liegt ein alter Fluch: Lügen haben kurze Beine. Mit jeder Lüge schrumpfen sie um ein Viertel.", en: "But an old curse lies on this house: lies have short legs. With every lie, they shrink by a quarter." },
    { t0: 40.0, t1: 47.6, de: "Du bist der Detektiv. Finde heraus, wer lügt, und bring den Money Tree zurück, bevor die Nacht vorbei ist.", en: "You are the detective. Find out who is lying and bring back the Money Tree before the night is over." },
  ];

  let st = null;
  const $ = (id) => document.getElementById(id);

  I.running = () => !!st;

  I.play = function (opts) {
    R.setMoneyTree(true);
    const tree = R.moneyTree();
    const mk = (o) => R.char(o);
    const actors = {
      thief: mk({ coat: 0x000000, hat: 0x000000, silhouette: true, beard: true }),
      brumm: mk({ coat: 0x4c4133, hat: 0x3d5a8a, beard: true }),
      velours: mk({ coat: 0x6a2c4a, hat: 0x2b2b2b, beard: false }),
      kip: mk({ coat: 0x4f6b35, hat: 0x8a3b2a, beard: false, hair: 0x8a5a2a }),
      butler: mk({ coat: 0x1a1a1a, hat: 0x1a1a1a, hatStyle: "none", beard: false, hair: 0xd8d8d8, scarf: 0xf4f1ea }),
      detective: mk(opts.detective),
    };
    st = { t: 0, opts, actors, tree, treeHome: tree.position.clone(), cap: -1, flags: {}, butlerLeg: 1 };
    $("intro").hidden = false;
    $("introTitle").classList.remove("on");
    $("introTitle").querySelector("small").textContent = opts.lang === "de" ? "„Lügen haben kurze Beine.“" : "“Lies have short legs.”";
    $("introCap").textContent = ""; $("introChap").textContent = "";
    $("introSkip").textContent = opts.lang === "de" ? "Überspringen ▸▸" : "Skip ▸▸";
    $("introSkip").onclick = () => I.skip();
    $("introSkip").focus({ preventScroll: true });
  };

  I.skip = function () { if (st) finish(); };

  function finish() {
    const s = st; st = null;
    for (const a of Object.values(s.actors)) R.remove(a.root);
    s.tree.position.copy(s.treeHome);
    R.setMoneyTree(false);
    R.setLightLevel(1);
    $("intro").hidden = true;
    $("introTitle").classList.remove("on");
    if (s.opts.onDone) s.opts.onDone();
  }

  function pose(a, x, z, yaw, extra, dt) {
    a.setPose(Object.assign({ x, z, y: R.heightAt(x, z), yaw, dt, leg: 1, face: "calm" }, extra || {}));
  }

  I.update = function (dt) {
    if (!st) return false;
    st.t += dt;
    const t = st.t, A = st.actors, o = st.opts;

    // ── captions ──
    const ci = CAPTIONS.findIndex((c) => t >= c.t0 && t < c.t1);
    if (ci !== st.cap) {
      st.cap = ci;
      const c = CAPTIONS[ci];
      const cap = $("introCap");
      cap.classList.remove("on");
      if (c) {
        cap.innerHTML = (c.speaker ? `<b>${c.speaker}:</b> ` : "") + (o.lang === "de" ? c.de : c.en);
        requestAnimationFrame(() => cap.classList.add("on"));
        $("introChap").textContent = c.chap || "";
        if (c.speaker) o.voice(170, st.butlerLeg < 1 ? 1 : 0, c.de); else o.narrate(o.lang === "de" ? c.de : c.en);
      }
    }

    // ── one-shot events ──
    const once = (k, fn) => { if (!st.flags[k] && t >= parseFloat(k)) { st.flags[k] = true; fn(); } };
    once("8.6", () => o.chime());
    once("33.8", () => { o.boing(); o.alarm(); o.flash(); });
    once("43.6", () => $("introTitle").classList.add("on"));

    // ── lights: out at 23:07, back after the theft ──
    let level = 1;
    if (t > 15.4 && t < 23) level = t < 16.2 ? lerp(1, 0.08, (t - 15.4) / 0.8) : t > 22.2 ? lerp(0.08, 1, (t - 22.2) / 0.8) : 0.08;
    R.setLightLevel(level);

    // ── actors ──
    // thief: in through the study door, takes the tree, out again
    const door = [5.5, 8.6], ped = [3.35, 3.7];
    if (t > 16 && t < 22.4) {
      const going = t < 18.8, k = going ? seg(t, 16, 18.8) : seg(t, 19.6, 22.3);
      const p = going ? V([door[0], 0, door[1]], [ped[0], 0, ped[1]], k) : V([ped[0], 0, ped[1]], [door[0], 0, door[1] + 0.8], k);
      const moving = going ? k < 1 : t > 19.6;
      const yaw = going ? Math.atan2(ped[0] - door[0], ped[1] - door[1]) : t < 19.6 ? -2.4 : Math.atan2(door[0] - ped[0], door[1] - ped[1]);
      pose(A.thief, p[0], p[2], yaw, { moving, walk: t * 9, face: "panic" }, dt);
      if (t > 19.2) { st.tree.position.set(p[0] + Math.sin(yaw) * 0.45, R.heightAt(p[0], p[2]) + 1.0, p[2] + Math.cos(yaw) * 0.45); }
    } else pose(A.thief, -20, -20, 0, { visible: false }, dt);
    if (t >= 22.4 && !st.flags.gone) { st.flags.gone = true; R.setMoneyTree(false); }

    // suspects in the hall
    const line = [["brumm", 9], ["velours", 11.5], ["kip", 14], ["butler", 16.5]];
    for (const [id, x] of line) {
      const face = id === "brumm" && t > 26 ? "sweat" : id === "butler" && t > 33.8 ? "sweat" : "calm";
      const leg = id === "butler" ? st.butlerLeg : 1;
      pose(A[id], x, 11.6, Math.sin(t * 0.7 + x) * 0.15, { face, leg, visible: t < 39.5 }, dt);
    }
    if (t > 33.8 && st.butlerLeg > 0.75) st.butlerLeg = Math.max(0.75, 1 - (t - 33.8) / 0.5 * 0.25);
    if (t > 33.8 && t < 34.4) A.butler.setPose({ leg: 0.75 + Math.sin((t - 33.8) * 20) * 0.06 * (34.4 - t), face: "panic" });

    // detective walks in from the garden
    const dp = V([9, 0, 21.2], [9, 0, 14.6], seg(t, 39.5, 45.5));
    pose(A.detective, dp[0], dp[2], Math.PI, { moving: t > 39.5 && t < 45.5, walk: t * 9 }, dt);

    // ── camera shots ──
    let cp, lk, fade = true;
    if (t < 8) { const k = seg(t, 0, 8); cp = V([20, 26, 52], [20, 15, 37], k); lk = [20, 0, 11]; fade = false; }
    else if (t < 15) { const k = seg(t, 8, 15); cp = V([7.8, 2.8, 7.4], [5.2, 2.0, 5.8], k); lk = [2.9, 1.25, 3.0]; }
    else if (t < 23) { const k = seg(t, 15, 23); cp = V([7.6, 4.2, 6.6], [7.0, 3.6, 7.2], k); lk = [4.2, 0.8, 5.0]; }
    else if (t < 31) { const k = seg(t, 23, 31); cp = V([8.0, 2.5, 16.7], [16.5, 2.4, 16.7], k); lk = [lerp(9.5, 15.5, k), 1.15, 11.6]; }
    else if (t < 39.5) { const k = seg(t, 31, 39.5); cp = V([16.5, 2.0, 16.4], [16.5, 1.55, 15.3], k); lk = [16.5, lerp(1.25, 1.0, k), 11.6]; }
    else { const k = seg(t, 39.5, 46); cp = [9.5 + Math.sin(t * 0.3) * 0.4, lerp(2.8, 2.1, k), dp[2] - 3.9]; lk = [9, 1.35, dp[2]]; }
    R.setCamera(cp[0], cp[1], cp[2], lk[0], lk[1], lk[2]);
    R.render(dt, t, { x: lk[0], z: lk[2] }, { fadeWalls: fade });

    if (t >= END) finish();
    return true;
  };

  global.Intro = I;
})(window);
