// SHORT LEGS – 3D renderer (Three.js): the manor, detailed toon characters, clues, lights, menu room, portraits.
(function (global) {
  "use strict";
  const THREE = global.THREE;
  const R = {};
  const PX = 1 / 32;               // logic pixels → world units (1 tile = 1 unit)
  const CELLAR_Y = -1.4;
  const WALL_TOP = 2.4, WALL_BOTTOM = -1.6;

  let renderer, scene, camera, menuScene, menuCam, pRenderer, pScene, pCam;
  let MAP = null, ROOMS = null;
  const walls = [];
  const pointLights = [];
  let hemi, moon, flashlight, fire, fireLight, snow, moneyTree, pedestalSpot;
  const dyn = new Set();            // per-match objects (characters, clue markers)
  const cam = { yaw: 0, pitch: 0.95, dist: 8.5, tx: 0, ty: 0, tz: 0, ready: false };
  let lightLevel = 1;

  // ───────────── materials & geometry helpers ─────────────
  const gradient = (() => {
    const t = new THREE.DataTexture(new Uint8Array([70, 150, 255]), 3, 1, THREE.RedFormat);
    t.minFilter = t.magFilter = THREE.NearestFilter; t.needsUpdate = true; return t;
  })();
  const matCache = new Map();
  const toon = (color, extra) => {
    const key = "t" + color + (extra ? JSON.stringify(extra) : "");
    if (!extra && matCache.has(key)) return matCache.get(key);
    const m = new THREE.MeshToonMaterial(Object.assign({ color, gradientMap: gradient }, extra || {}));
    if (!extra) matCache.set(key, m);
    return m;
  };
  const std = (color, rough = 0.85, extra) => new THREE.MeshStandardMaterial(Object.assign({ color, roughness: rough, metalness: 0 }, extra || {}));
  const OUTLINE = new THREE.MeshBasicMaterial({ color: 0x14100e, side: THREE.BackSide });
  const G = {
    sphere: new THREE.SphereGeometry(1, 20, 14),
    sphereLo: new THREE.SphereGeometry(1, 10, 8),
    box: new THREE.BoxGeometry(1, 1, 1),
    cyl: new THREE.CylinderGeometry(1, 1, 1, 18),
    cylLo: new THREE.CylinderGeometry(1, 1, 1, 10),
    cone: new THREE.ConeGeometry(1, 1, 12),
  };
  function mesh(geo, mat, x = 0, y = 0, z = 0, sx = 1, sy = 1, sz = 1, parent) {
    const m = new THREE.Mesh(geo, mat);
    m.position.set(x, y, z); m.scale.set(sx, sy, sz);
    m.castShadow = true; m.receiveShadow = true;
    if (parent) parent.add(m);
    return m;
  }
  const box = (w, h, d, mat, x, y, z, parent) => mesh(G.box, mat, x, y + h / 2, z, w, h, d, parent);
  const cyl = (r, h, mat, x, y, z, parent, lo) => mesh(lo ? G.cylLo : G.cyl, mat, x, y + h / 2, z, r, h, r, parent);
  function outline(m, k = 1.07) {
    const o = new THREE.Mesh(m.geometry, OUTLINE);
    o.scale.setScalar(k); o.castShadow = false; o.receiveShadow = false; o.userData.outline = true;
    m.add(o); return m;
  }

  // ───────────── procedural textures ─────────────
  function canvasTex(w, h, draw, repeat) {
    const c = document.createElement("canvas"); c.width = w; c.height = h;
    draw(c.getContext("2d"), w, h);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
    if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; }
    return t;
  }
  const shade = (hex, f) => {
    const n = parseInt(hex.slice(1), 16), r = Math.min(255, (n >> 16 & 255) * f) | 0, g = Math.min(255, (n >> 8 & 255) * f) | 0, b = Math.min(255, (n & 255) * f) | 0;
    return `rgb(${r},${g},${b})`;
  };
  let seed = 7; const srnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);

  const TEX = {
    planks: (base) => canvasTex(256, 256, (g, w, h) => {
      for (let row = 0; row < 8; row++) {
        let x = -srnd() * 120;
        while (x < w) {
          const len = 90 + srnd() * 110;
          g.fillStyle = shade(base, 0.82 + srnd() * 0.3); g.fillRect(x, row * 32, len, 32);
          g.strokeStyle = "rgba(0,0,0,.25)"; g.lineWidth = 2; g.strokeRect(x, row * 32, len, 32);
          g.strokeStyle = "rgba(0,0,0,.08)"; g.lineWidth = 1;
          for (let k = 0; k < 4; k++) { g.beginPath(); const y = row * 32 + 5 + srnd() * 22; g.moveTo(x, y); g.bezierCurveTo(x + len * 0.3, y + 3, x + len * 0.6, y - 3, x + len, y); g.stroke(); }
          x += len;
        }
      }
    }, true),
    checker: () => canvasTex(128, 128, (g) => { for (let y = 0; y < 2; y++) for (let x = 0; x < 2; x++) { g.fillStyle = (x + y) % 2 ? "#2e3238" : "#e6e1d6"; g.fillRect(x * 64, y * 64, 64, 64); } g.strokeStyle = "rgba(0,0,0,.15)"; g.strokeRect(0, 0, 128, 128); }, true),
    snow: () => canvasTex(256, 256, (g, w, h) => {
      g.fillStyle = "#e9f1f5"; g.fillRect(0, 0, w, h);
      for (let i = 0; i < 260; i++) { g.fillStyle = `rgba(${140 + srnd() * 40},${170 + srnd() * 40},${200 + srnd() * 30},${0.08 + srnd() * 0.12})`; g.beginPath(); g.ellipse(srnd() * w, srnd() * h, 4 + srnd() * 18, 2 + srnd() * 8, srnd() * 3, 0, Math.PI * 2); g.fill(); }
      for (let i = 0; i < 200; i++) { g.fillStyle = "rgba(255,255,255,.9)"; g.fillRect(srnd() * w, srnd() * h, 1.5, 1.5); }
    }, true),
    stone: (base) => canvasTex(256, 256, (g, w, h) => {
      g.fillStyle = shade(base, 0.6); g.fillRect(0, 0, w, h);
      for (let row = 0; row < 6; row++) for (let col = 0; col < 5; col++) {
        const x = col * 52 + (row % 2) * 26 - 10, y = row * 43;
        g.fillStyle = shade(base, 0.8 + srnd() * 0.35); g.beginPath(); g.roundRect ? g.roundRect(x + 3, y + 3, 46, 37, 6) : g.rect(x + 3, y + 3, 46, 37); g.fill();
      }
    }, true),
    terracotta: () => canvasTex(128, 128, (g) => { for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) { g.fillStyle = shade("#b0643c", 0.85 + srnd() * 0.25); g.fillRect(x * 32 + 1, y * 32 + 1, 30, 30); } }, true),
    rug: () => canvasTex(512, 256, (g, w, h) => {
      g.fillStyle = "#7a1f25"; g.fillRect(0, 0, w, h);
      g.strokeStyle = "#c9a227"; g.lineWidth = 8; g.strokeRect(14, 14, w - 28, h - 28);
      g.lineWidth = 3; g.strokeRect(30, 30, w - 60, h - 60);
      g.fillStyle = "rgba(201,162,39,.55)";
      for (let i = 0; i < 9; i++) { const cx = 60 + i * 49; g.beginPath(); g.moveTo(cx, h / 2 - 34); g.lineTo(cx + 18, h / 2); g.lineTo(cx, h / 2 + 34); g.lineTo(cx - 18, h / 2); g.closePath(); g.fill(); }
      g.fillStyle = "#1f3b5a"; g.beginPath(); g.ellipse(w / 2, h / 2, 60, 40, 0, 0, Math.PI * 2); g.fill();
    }),
    wall: (paper, wains) => canvasTex(128, 512, (g, w, h) => {
      // v runs from WALL_BOTTOM (-1.6) at the bottom to WALL_TOP (2.4) at the top: 4 units → 128 px per unit.
      const U = h / (WALL_TOP - WALL_BOTTOM), yOf = (v) => h - (v - WALL_BOTTOM) * U;
      g.fillStyle = "#4a4d52"; g.fillRect(0, yOf(0), w, h - yOf(0));                          // cellar stone (below floor)
      for (let r = 0; r < 6; r++) { g.strokeStyle = "rgba(0,0,0,.35)"; g.beginPath(); g.moveTo(0, yOf(-r * 0.27)); g.lineTo(w, yOf(-r * 0.27)); g.stroke(); }
      g.fillStyle = paper; g.fillRect(0, 0, w, yOf(0.95));                                    // wallpaper
      g.fillStyle = "rgba(255,255,255,.07)"; for (let x = 0; x < w; x += 32) g.fillRect(x, 0, 12, yOf(0.95));
      g.fillStyle = "rgba(255,230,160,.12)"; for (let y = 20; y < yOf(0.95); y += 44) for (let x = 16; x < w; x += 32) { g.beginPath(); g.arc(x + ((y / 44) % 2) * 16, y, 4, 0, Math.PI * 2); g.fill(); }
      g.fillStyle = wains; g.fillRect(0, yOf(0.95), w, yOf(0) - yOf(0.95));                  // wainscot
      g.strokeStyle = "rgba(0,0,0,.3)"; g.lineWidth = 2; for (let x = 6; x < w; x += 42) g.strokeRect(x, yOf(0.85), 34, yOf(0.1) - yOf(0.85));
      g.fillStyle = "#d9c79a"; g.fillRect(0, yOf(0.98), w, 4);                                // chair rail
      g.fillStyle = "#2a1d17"; g.fillRect(0, yOf(0.08), w, 10);                               // skirting
      g.fillStyle = "#e8dcc0"; g.fillRect(0, 0, w, 8);                                        // crown moulding
    }),
  };

  // ───────────── init & sizing ─────────────
  R.init = function (canvas) {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0b1420);
    scene.fog = new THREE.FogExp2(0x0b1420, 0.018);
    camera = new THREE.PerspectiveCamera(48, 1, 0.1, 200);
    buildMenuScene();
  };
  R.resize = function (w, h, dpr) {
    if (!renderer) return;
    renderer.setPixelRatio(dpr); renderer.setSize(w, h, false);
    camera.aspect = w / h; camera.updateProjectionMatrix();
    menuCam.aspect = w / h; menuCam.updateProjectionMatrix();
  };
  R.cam = cam;
  R.PX = PX;

  // ───────────── world ─────────────
  R.heightAt = function (ux, uz) {
    if (!MAP) return 0;
    const tx = Math.floor(ux), tz = Math.floor(uz);
    const t = (MAP.tiles[tz] || [])[tx];
    if (t === "S") return CELLAR_Y * Math.min(1, Math.max(0, (uz - 17) / 2));
    return (MAP.region[tz] || [])[tx] === "cellar" ? CELLAR_Y : 0;
  };

  const WALLSTYLE = {
    study: ["#2f4a3a", "#4a2e1f"], library: ["#5a2b2b", "#3a2418"], bedroom: ["#6a5a7a", "#4a3a44"], kitchen: ["#d9d2b8", "#7a8a8a"],
    hall: ["#7a5a2a", "#3a2418"], greenhouse: ["#6f8a6a", "#5a4a3a"], cellar: ["#4a4d52", "#3a3d42"], shed: ["#6a5a44", "#4a3a2a"], garden: ["#8a8f96", "#6a6f76"],
  };

  R.buildWorld = function (map, rooms) {
    MAP = map; ROOMS = rooms;
    const world = new THREE.Group(); scene.add(world);

    // Lights
    hemi = new THREE.HemisphereLight(0x9cb8d4, 0x2a2018, 1.1); world.add(hemi);
    moon = new THREE.DirectionalLight(0xc8dcff, 1.6);
    moon.castShadow = true; moon.shadow.mapSize.set(2048, 2048);
    Object.assign(moon.shadow.camera, { left: -16, right: 16, top: 16, bottom: -16, near: 1, far: 70 });
    moon.shadow.bias = -0.0008; moon.shadow.normalBias = 0.02;
    scene.add(moon, moon.target);

    // Exterior: snowy ground, forest, moon, stars
    const snowTex = TEX.snow(); snowTex.repeat.set(40, 40);
    // Ground around the manor (four slabs, so the cellar below floor level stays open)
    const groundM = std(0xffffff, 1, { map: snowTex });
    for (const [x0, z0, x1, z1] of [[-100, -100, 140, 0], [-100, map.H, 140, 120], [-100, 0, 0, map.H], [map.W, 0, 140, map.H]]) {
      const gp = new THREE.Mesh(new THREE.PlaneGeometry(x1 - x0, z1 - z0), groundM);
      gp.rotation.x = -Math.PI / 2; gp.position.set((x0 + x1) / 2, -0.03, (z0 + z1) / 2); gp.receiveShadow = true; world.add(gp);
    }
    const trunkM = toon(0x4a3222), pineM = toon(0x1f3d2e), snowM = toon(0xf2f7fa);
    const tree = (x, z, s) => {
      const g = new THREE.Group(); g.position.set(x, 0, z); g.scale.setScalar(s);
      cyl(0.18, 0.8, trunkM, 0, 0, 0, g, true);
      for (let i = 0; i < 3; i++) { mesh(G.cone, pineM, 0, 1.1 + i * 0.75, 0, 1.3 - i * 0.3, 1.4, 1.3 - i * 0.3, g); mesh(G.cone, snowM, 0, 1.45 + i * 0.75, 0, 0.8 - i * 0.2, 0.55, 0.8 - i * 0.2, g); }
      world.add(g);
    };
    seed = 99;
    for (let i = 0; i < 70; i++) {
      const a = srnd() * Math.PI * 2, r = 30 + srnd() * 30;
      tree(20 + Math.cos(a) * r, 13 + Math.sin(a) * r * 0.8, 1.2 + srnd() * 1.4);
    }
    const moonMesh = new THREE.Mesh(G.sphere, new THREE.MeshBasicMaterial({ color: 0xf4f1dc, fog: false }));
    moonMesh.position.set(-40, 55, -60); moonMesh.scale.setScalar(4); world.add(moonMesh);
    const starGeo = new THREE.BufferGeometry(), sp = [];
    for (let i = 0; i < 600; i++) { const a = srnd() * Math.PI * 2, e = 0.15 + srnd() * 1.2, r = 150; sp.push(20 + Math.cos(a) * Math.cos(e) * r, Math.sin(e) * r, 13 + Math.sin(a) * Math.cos(e) * r); }
    starGeo.setAttribute("position", new THREE.Float32BufferAttribute(sp, 3));
    world.add(new THREE.Points(starGeo, new THREE.PointsMaterial({ color: 0xffffff, size: 0.6, fog: false })));

    // Floors per room
    const floorTex = {
      study: () => TEX.planks("#6b4a32"), library: () => TEX.planks("#5a3a26"), bedroom: () => TEX.planks("#7a5a44"), hall: () => TEX.planks("#8a6a48"),
      kitchen: TEX.checker, greenhouse: TEX.terracotta, garden: TEX.snow, cellar: () => TEX.stone("#6a6d72"), shed: () => TEX.planks("#7a6a52"),
    };
    for (const [id, r] of Object.entries(rooms)) {
      const tex = (floorTex[id] || (() => TEX.planks("#8a6a48")))();
      tex.repeat.set(r.w / (id === "kitchen" ? 2 : 3), r.h / (id === "kitchen" ? 2 : 3));
      const y = id === "cellar" ? CELLAR_Y : 0;
      const f = new THREE.Mesh(new THREE.BoxGeometry(r.w, 0.1, r.h), std(0xffffff, id === "kitchen" ? 0.5 : 0.8, { map: tex }));
      f.position.set(r.x + r.w / 2, y - 0.05, r.y + r.h / 2); f.receiveShadow = true; world.add(f);
    }
    // Door thresholds
    const thrM = std(0x5a3a22, 0.6);
    for (let z = 0; z < map.H; z++) for (let x = 0; x < map.W; x++) if (map.tiles[z][x] === "d") { const d = box(1, 0.06, 1, thrM, x + 0.5, -0.03, z + 0.5, world); d.castShadow = false; }

    // Stairs down to the cellar (6 steps over 2 tiles)
    const stepM = std(0x6a5440, 0.7), stepEdge = std(0x3a2a1c, 0.7);
    for (let i = 0; i < 6; i++) {
      const top = CELLAR_Y * ((i + 0.5) / 6), d = 2 / 6;
      const s = box(3, top - WALL_BOTTOM, d, stepM, 22.5, WALL_BOTTOM, 17 + d * (i + 0.5), world);
      box(3, 0.04, 0.05, stepEdge, 22.5, top - 0.02, 17 + d * i + 0.03, world);
      s.receiveShadow = true;
    }
    // Hedges
    const hedgeM = toon(0x2f5a2a), hedgeSnow = toon(0xf4f8fb);
    for (let z = 0; z < map.H; z++) for (let x = 0; x < map.W; x++) if (map.tiles[z][x] === "h") {
      box(0.9, 0.95, 0.9, hedgeM, x + 0.5, 0, z + 0.5, world);
      for (let k = 0; k < 3; k++) mesh(G.sphereLo, hedgeM, x + 0.2 + k * 0.3, 0.95, z + 0.5, 0.22, 0.2, 0.4, world);
      box(0.92, 0.1, 0.92, hedgeSnow, x + 0.5, 1.02, z + 0.5, world);
    }

    // Walls (one mesh per wall tile so the camera can fade the ones in the way)
    const neighborRegion = (x, z) => {
      for (const [dx, dz] of [[0, 1], [0, -1], [1, 0], [-1, 0], [1, 1], [-1, 1], [1, -1], [-1, -1]]) { const r = (map.region[z + dz] || [])[x + dx]; if (r && map.tiles[z + dz][x + dx] !== "S") return r; }
      return null;
    };
    const wallMats = {}, capM = std(0x241c18, 0.9), capFade = std(0x241c18, 0.9, { transparent: true, opacity: 0.12, depthWrite: false });
    const styleMats = (id) => {
      if (wallMats[id]) return wallMats[id];
      const [paper, wains] = WALLSTYLE[id] || WALLSTYLE.hall;
      const side = std(0xffffff, 0.85, { map: TEX.wall(paper, wains) });
      const fade = side.clone(); fade.transparent = true; fade.opacity = 0.12; fade.depthWrite = false;
      return (wallMats[id] = { solid: [side, side, capM, capM, side, side], fade: [fade, fade, capFade, capFade, fade, fade] });
    };
    const gardenWallM = [std(0x8a8f96, 0.95, { map: TEX.stone("#9aa0a8") })];
    for (let z = 0; z < map.H; z++) for (let x = 0; x < map.W; x++) {
      if (map.tiles[z][x] !== "#") continue;
      const reg = neighborRegion(x, z);
      if (!reg) continue;
      const onlyGarden = [[0, 1], [0, -1], [1, 0], [-1, 0]].every(([dx, dz]) => { const r = (map.region[z + dz] || [])[x + dx]; return !r || r === "garden"; }) && reg === "garden";
      if (onlyGarden) {
        const w = box(1, 1.0, 1, gardenWallM[0], x + 0.5, 0, z + 0.5, world);
        box(1.04, 0.12, 1.04, hedgeSnow, x + 0.5, 1.0, z + 0.5, world);
        w.userData.low = true;
        continue;
      }
      const mats = styleMats(reg);
      const w = new THREE.Mesh(G.box, mats.solid);
      w.scale.set(1, WALL_TOP - WALL_BOTTOM, 1);
      w.position.set(x + 0.5, (WALL_TOP + WALL_BOTTOM) / 2, z + 0.5);
      w.castShadow = true; w.receiveShadow = true;
      w.userData = { mats, faded: false, cx: x + 0.5, cz: z + 0.5 };
      world.add(w); walls.push(w);
      // Windows on the outer walls
      const edge = x === 0 || z === 0 || x === map.W - 1;
      if (edge && (x + z) % 3 === 0 && reg !== "cellar") addWindow(world, x, z, map);
    }

    buildFurniture(world);
    buildSnow(world);
    flashlight = new THREE.SpotLight(0xfff2d0, 0, 14, 0.55, 0.5, 1.2);
    scene.add(flashlight, flashlight.target);
  };

  function addWindow(world, x, z, map) {
    const glow = new THREE.MeshBasicMaterial({ color: 0xffc46b }), night = std(0x223a55, 0.2, { emissive: 0x1a3048, emissiveIntensity: 0.6 }), frameM = std(0xe8dcc0, 0.6);
    const [nx, nz] = x === 0 ? [-1, 0] : x === map.W - 1 ? [1, 0] : [0, -1];
    for (const sgn of [1, -1]) {
      const g = new THREE.Group();
      g.position.set(x + 0.5 + nx * 0.505 * sgn, 1.35, z + 0.5 + nz * 0.505 * sgn);
      g.rotation.y = Math.atan2(nx * sgn, nz * sgn);
      g.add(new THREE.Mesh(new THREE.PlaneGeometry(0.62, 0.9), sgn === 1 ? glow : night));
      for (const [w, h, py] of [[0.7, 0.06, 0.47], [0.7, 0.06, -0.47], [0.7, 0.04, 0]]) { const b = new THREE.Mesh(G.box, frameM); b.scale.set(w, h, 0.03); b.position.set(0, py, 0.01); g.add(b); }
      for (const px of [-0.34, 0.34, 0]) { const b = new THREE.Mesh(G.box, frameM); b.scale.set(px ? 0.06 : 0.04, 0.98, 0.03); b.position.set(px, 0, 0.01); g.add(b); }
      if (sgn === -1) { const sill = new THREE.Mesh(G.box, frameM); sill.scale.set(0.8, 0.05, 0.12); sill.position.set(0, -0.5, 0.05); g.add(sill); }
      world.add(g);
    }
  }

  function light(color, intensity, dist, x, y, z, world) {
    const l = new THREE.PointLight(color, intensity, dist, 2);
    l.position.set(x, y, z); l.userData.base = intensity;
    world.add(l); pointLights.push(l); return l;
  }

  function buildFurniture(world) {
    const wood = std(0x5a3a22, 0.7), darkWood = std(0x3a2418, 0.7), brass = std(0xc9a227, 0.35, { metalness: 0.6 }), marble = std(0xe8e4dc, 0.3);
    const fabricRed = toon(0x7a2b2b), fabricGreen = toon(0x2f4a3a), white = toon(0xf4f1ea), black = std(0x1a1a1a, 0.6);
    const legs4 = (w, d, h, x, z, g) => { for (const [a, b] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) cyl(0.04, h, darkWood, x + a * (w / 2 - 0.08), 0, z + b * (d / 2 - 0.08), g, true); };
    const books = (x0, z0, width, shelves, rot, g) => {
      const n = Math.floor(width / 0.09) * shelves;
      const im = new THREE.InstancedMesh(G.box, std(0xffffff, 0.8), n), m4 = new THREE.Matrix4(), col = new THREE.Color();
      const palette = [0x7a2b2b, 0x2d4a6a, 0x6b5a2a, 0x3f6a36, 0x5a3a5a, 0xc9a227, 0x2a2a2a];
      let i = 0;
      for (let s = 0; s < shelves; s++) for (let k = 0; k < Math.floor(width / 0.09); k++) {
        const h = 0.26 + srnd() * 0.12;
        const pos = new THREE.Vector3(-width / 2 + 0.05 + k * 0.09, 0.3 + s * 0.45 + h / 2, 0);
        pos.applyAxisAngle(new THREE.Vector3(0, 1, 0), rot); pos.add(new THREE.Vector3(x0, 0, z0));
        m4.compose(pos, new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), rot), new THREE.Vector3(0.07, h, 0.24));
        im.setMatrixAt(i, m4); im.setColorAt(i, col.setHex(palette[Math.floor(srnd() * palette.length)])); i++;
      }
      im.castShadow = true; im.frustumCulled = false; g.add(im);
    };
    const shelf = (x, z, w, rot, g) => {
      const s = new THREE.Group(); s.position.set(x, 0, z); s.rotation.y = rot;
      box(w, 2.1, 0.34, darkWood, 0, 0, 0, s);
      for (let k = 0; k < 4; k++) box(w - 0.06, 0.04, 0.3, wood, 0, 0.28 + k * 0.45, 0.03, s);
      g.add(s); books(x, z + 0.04 * Math.cos(rot), w - 0.1, 4, rot, g);
    };
    seed = 42;

    // ── Study
    box(3.2, 0.08, 1.1, wood, 7.8, 0.78, 2.0, world); legs4(3.2, 1.1, 0.78, 7.8, 2.0, world);
    box(0.9, 0.5, 0.6, darkWood, 6.8, 0, 2.0, world); box(0.9, 0.5, 0.6, darkWood, 8.8, 0, 2.0, world);
    box(0.6, 0.03, 0.4, white, 7.4, 0.86, 2.0, world);
    cyl(0.05, 0.4, brass, 8.8, 0.86, 1.8, world); const shade1 = mesh(G.cone, toon(0x2f6a3a), 8.8, 1.3, 1.8, 0.2, 0.18, 0.2, world); shade1.rotation.x = Math.PI;
    light(0xffc07a, 6, 6, 8.8, 1.2, 1.9, world);
    box(0.55, 0.9, 0.55, fabricRed, 7.8, 0, 3.0, world); box(0.55, 0.7, 0.1, fabricRed, 7.8, 0.9, 3.25, world);
    shelf(2.6, 1.2, 2.6, 0, world);
    // pedestal (empty during the case, the Money Tree stands here in the intro)
    cyl(0.32, 0.08, marble, 2.8, 0, 3.0, world); cyl(0.22, 0.9, marble, 2.8, 0.08, 3.0, world); cyl(0.34, 0.08, marble, 2.8, 0.98, 3.0, world);
    moneyTree = new THREE.Group(); moneyTree.position.set(2.8, 1.06, 3.0);
    cyl(0.18, 0.2, std(0x8a3b2a, 0.6), 0, 0, 0, moneyTree); cyl(0.2, 0.03, std(0x6a2a1a, 0.6), 0, 0.2, 0, moneyTree);
    const gold = std(0xf2c84b, 0.25, { metalness: 0.9, emissive: 0x8a6010, emissiveIntensity: 0.6 });
    const trunk = cyl(0.04, 0.45, std(0x6b4a2b, 0.8), 0, 0.2, 0, moneyTree); trunk.rotation.z = 0.2;
    for (let i = 0; i < 16; i++) {
      const a = srnd() * Math.PI * 2, r = 0.08 + srnd() * 0.2;
      const coin = mesh(G.cyl, gold, Math.cos(a) * r, 0.55 + srnd() * 0.25, Math.sin(a) * r, 0.07, 0.012, 0.07, moneyTree);
      coin.rotation.set(srnd() * 1.5, srnd() * 3, srnd() * 1.5);
    }
    moneyTree.visible = false; world.add(moneyTree);
    pedestalSpot = new THREE.SpotLight(0xffe2a0, 0, 6, 0.35, 0.6, 1.5); pedestalSpot.position.set(2.8, 3.2, 3.0); pedestalSpot.target.position.set(2.8, 1, 3.0);
    world.add(pedestalSpot, pedestalSpot.target);
    // globe
    cyl(0.03, 0.8, brass, 9.6, 0, 6.8, world); mesh(G.sphere, toon(0x2d6a8a), 9.6, 0.95, 6.8, 0.2, 0.2, 0.2, world);
    const rugS = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 2.2), std(0xffffff, 0.95, { map: TEX.rug() })); rugS.rotation.x = -Math.PI / 2; rugS.position.set(6, 0.012, 5.5); rugS.receiveShadow = true; world.add(rugS);

    // ── Library
    for (let i = 0; i < 4; i++) shelf(13.6 + i * 2.8, 1.2, 2.5, 0, world);
    shelf(23.3, 4.5, 2.6, -Math.PI / 2, world);
    box(0.8, 0.45, 0.8, fabricGreen, 16, 0, 5.2, world); box(0.8, 0.7, 0.18, fabricGreen, 16, 0.45, 5.55, world);
    box(0.8, 0.45, 0.8, fabricGreen, 19, 0, 5.2, world); box(0.8, 0.7, 0.18, fabricGreen, 19, 0.45, 5.55, world);
    cyl(0.4, 0.05, wood, 17.5, 0.55, 5.2, world); cyl(0.05, 0.55, darkWood, 17.5, 0, 5.2, world);
    cyl(0.07, 0.07, white, 17.4, 0.6, 5.1, world);
    light(0xffc890, 6, 6, 17.5, 1.6, 5, world);

    // ── Bedroom
    box(1.8, 0.45, 2.6, darkWood, 27.2, 0, 3.0, world); box(1.7, 0.2, 2.5, white, 27.2, 0.45, 3.0, world);
    box(1.72, 0.12, 1.6, toon(0x6a2c4a), 27.2, 0.62, 3.5, world); box(0.7, 0.14, 0.4, white, 26.8, 0.66, 2.0, world); box(0.7, 0.14, 0.4, white, 27.6, 0.66, 2.0, world);
    box(1.8, 1.1, 0.12, darkWood, 27.2, 0, 1.68, world);
    box(0.5, 0.55, 0.45, wood, 28.6, 0, 1.9, world); cyl(0.05, 0.25, brass, 28.6, 0.55, 1.9, world); mesh(G.cone, toon(0xf0dcb0), 28.6, 0.9, 1.9, 0.18, 0.2, 0.18, world);
    light(0xffc07a, 5, 5, 28.6, 1.1, 2.0, world);
    box(1.4, 2.0, 0.6, darkWood, 30.6, 0, 1.4, world); box(0.03, 1.7, 0.02, brass, 30.6, 0.15, 1.72, world);

    // ── Kitchen
    const counterM = std(0x8a9aa0, 0.5), topM = std(0xd8d4cc, 0.3);
    box(5.6, 0.9, 0.7, counterM, 36, 0, 1.4, world); box(5.7, 0.06, 0.75, topM, 36, 0.9, 1.4, world);
    box(1.0, 0.92, 0.72, black, 34.2, 0, 1.4, world); for (const bx of [33.95, 34.45]) cyl(0.12, 0.02, std(0xaa2222, 0.5, { emissive: 0x551010 }), bx, 0.93, 1.4, world);
    box(0.7, 0.1, 0.5, std(0xcfd8dc, 0.2, { metalness: 0.7 }), 37.2, 0.9, 1.4, world);
    box(1.6, 0.06, 1.0, wood, 35.5, 0.75, 4.2, world); legs4(1.6, 1.0, 0.75, 35.5, 4.2, world);
    cyl(0.28, 0.18, toon(0x6b3f2a), 35.5, 0.81, 4.2, world); cyl(0.29, 0.05, white, 35.5, 0.99, 4.2, world); mesh(G.sphereLo, toon(0xcc1133), 35.5, 1.07, 4.2, 0.05, 0.05, 0.05, world);
    const slice = box(0.2, 0.2, 0.12, toon(0x6b3f2a), 35.95, 0.81, 4.45, world); slice.rotation.y = 0.6;
    light(0xfff0d0, 7, 7, 36, 2.1, 3.5, world);

    // ── Great Hall
    const rug = new THREE.Mesh(new THREE.PlaneGeometry(19, 4.2), std(0xffffff, 0.95, { map: TEX.rug() })); rug.rotation.x = -Math.PI / 2; rug.position.set(13.5, 0.012, 13); rug.receiveShadow = true; world.add(rug);
    const stoneM = std(0x8a8580, 0.9, { map: TEX.stone("#9a948e") });
    box(3.2, 1.5, 0.7, stoneM, 13.5, 0, 9.35, world); box(3.6, 0.15, 0.85, marble, 13.5, 1.5, 9.4, world);
    box(1.6, 0.9, 0.3, std(0x0a0a0a, 1), 13.5, 0, 9.6, world);
    fire = new THREE.Group(); fire.position.set(13.5, 0.05, 9.8);
    for (let i = 0; i < 3; i++) mesh(G.cyl, std(0x3a2418), -0.3 + i * 0.3, 0.06, 0, 0.06, 0.6, 0.06, fire).rotation.z = Math.PI / 2;
    const flameM = new THREE.MeshBasicMaterial({ color: 0xff9a3c, transparent: true, opacity: 0.9 }), flameM2 = new THREE.MeshBasicMaterial({ color: 0xffe07a, transparent: true, opacity: 0.9 });
    for (let i = 0; i < 5; i++) { const f = mesh(G.cone, i % 2 ? flameM2 : flameM, -0.35 + i * 0.18, 0.3, 0, 0.12, 0.45, 0.12, fire); f.castShadow = false; f.userData.flame = i; }
    world.add(fire);
    fireLight = light(0xff8a3c, 22, 10, 13.5, 0.7, 10.2, world);
    // chandelier
    const ch = new THREE.Group(); ch.position.set(13.5, 2.95, 13); ch.scale.setScalar(0.75);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.6, 0.03, 8, 32), brass); ring.rotation.x = Math.PI / 2; ch.add(ring);
    for (let i = 0; i < 8; i++) { const a = i / 8 * Math.PI * 2; mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0xffe8b0 }), Math.cos(a) * 0.6, 0.08, Math.sin(a) * 0.6, 0.06, 0.09, 0.06, ch).castShadow = false; }
    world.add(ch); light(0xffd9a0, 12, 12, 13.5, 2.8, 13, world);
    // sofas, clock, coat rack, paintings, plants
    const sofa = (x, z, rot) => { const g = new THREE.Group(); g.position.set(x, 0, z); g.rotation.y = rot; box(2.0, 0.45, 0.8, fabricGreen, 0, 0, 0, g); box(2.0, 0.55, 0.2, fabricGreen, 0, 0.45, -0.32, g); box(0.2, 0.3, 0.8, fabricGreen, -0.95, 0.45, 0, g); box(0.2, 0.3, 0.8, fabricGreen, 0.95, 0.45, 0, g); world.add(g); };
    sofa(4.5, 10.2, 0); sofa(22.5, 10.2, 0);
    box(0.6, 2.2, 0.4, darkWood, 25.9, 0, 9.5, world); cyl(0.2, 0.02, std(0xf4f1ea, 0.4), 25.9, 1.75, 9.71, world).rotation.x = Math.PI / 2;
    cyl(0.04, 1.8, darkWood, 1.6, 0, 15.5, world); for (let i = 0; i < 3; i++) mesh(G.sphereLo, toon([0x4c4133, 0x2f5b6b, 0x7a5a2a][i]), 1.6 + Math.cos(i * 2) * 0.2, 1.5, 15.5 + Math.sin(i * 2) * 0.2, 0.18, 0.3, 0.12, world);
    const painting = (x, color) => { box(1.2, 0.9, 0.06, brass, x, 1.3, 9.03, world); box(1.05, 0.75, 0.02, toon(color), x, 1.375, 9.07, world); mesh(G.sphereLo, toon(0xf2b38f), x, 1.8, 9.09, 0.16, 0.18, 0.02, world); };
    painting(6, 0x2d4a6a); painting(9, 0x5a2b2b); painting(18, 0x3f5a36); painting(21, 0x4a3a5a);
    const plant = (x, z) => { cyl(0.22, 0.4, std(0x8a3b2a, 0.7), x, 0, z, world); for (let i = 0; i < 5; i++) mesh(G.sphereLo, toon(0x3f6a36), x + (srnd() - 0.5) * 0.35, 0.6 + srnd() * 0.4, z + (srnd() - 0.5) * 0.35, 0.25, 0.25, 0.25, world); };
    plant(1.6, 9.6); plant(26.3, 16.3); plant(1.6, 16.3);

    // ── Greenhouse
    for (let i = 0; i < 3; i++) {
      box(2.4, 0.5, 1.0, wood, 30.5 + i * 3, 0, 10.2, world); box(2.3, 0.08, 0.9, std(0x3a2a1c, 1), 30.5 + i * 3, 0.5, 10.2, world);
      for (let k = 0; k < 5; k++) { mesh(G.sphereLo, toon(0x3f7a3a), 29.6 + i * 3 + k * 0.45, 0.75, 10.2 + (srnd() - 0.5) * 0.4, 0.2, 0.25, 0.2, world); mesh(G.sphereLo, toon([0xff6f91, 0xffd166, 0xc77dff][k % 3]), 29.6 + i * 3 + k * 0.45, 1.0, 10.2, 0.07, 0.07, 0.07, world); }
    }
    box(1.0, 0.05, 1.0, wood, 31, 0.7, 12.5, world); legs4(1, 1, 0.7, 31, 12.5, world); cyl(0.06, 0.08, white, 31, 0.75, 12.5, world);
    for (let i = 0; i < 4; i++) plant(29 + i * 3, 16.2);
    light(0xd6ffd0, 6, 9, 33.5, 2.2, 13, world);

    // ── Garden: snowman, lamppost, bench, pines
    const sm = new THREE.Group(); sm.position.set(4, 0, 21.5);
    mesh(G.sphere, snowM(), 0, 0.4, 0, 0.45, 0.42, 0.45, sm); mesh(G.sphere, snowM(), 0, 1.0, 0, 0.33, 0.32, 0.33, sm); mesh(G.sphere, snowM(), 0, 1.45, 0, 0.23, 0.23, 0.23, sm);
    const carrot = mesh(G.cone, toon(0xff8a2a), 0, 1.45, 0.3, 0.05, 0.25, 0.05, sm); carrot.rotation.x = Math.PI / 2;
    for (const ex of [-0.08, 0.08]) mesh(G.sphereLo, black, ex, 1.52, 0.2, 0.03, 0.03, 0.03, sm);
    cyl(0.18, 0.03, black, 0, 1.63, 0, sm); cyl(0.12, 0.22, black, 0, 1.65, 0, sm);
    const scarf = new THREE.Mesh(new THREE.TorusGeometry(0.26, 0.06, 8, 20), toon(0xff2d55)); scarf.rotation.x = Math.PI / 2; scarf.position.y = 1.22; sm.add(scarf);
    world.add(sm);
    cyl(0.05, 2.4, black, 14.5, 0, 19.2, world); box(0.3, 0.35, 0.3, new THREE.MeshBasicMaterial({ color: 0xffd08a }), 14.5, 2.4, 19.2, world);
    light(0xffd08a, 12, 9, 14.5, 2.3, 19.4, world);
    box(1.6, 0.08, 0.5, wood, 7, 0.45, 23.8, world); legs4(1.6, 0.5, 0.45, 7, 23.8, world); box(1.6, 0.06, 0.52, toon(0xf4f8fb), 7, 0.53, 23.8, world);
    function snowM() { return toon(0xf2f7fa); }
    for (const [x, z, s] of [[2, 19, 0.7], [15.5, 23.8, 0.8], [13.5, 24, 0.6]]) {
      const g = new THREE.Group(); g.position.set(x, 0, z); g.scale.setScalar(s);
      cyl(0.15, 0.6, toon(0x4a3222), 0, 0, 0, g, true);
      for (let i = 0; i < 3; i++) { mesh(G.cone, toon(0x1f3d2e), 0, 0.9 + i * 0.6, 0, 0.9 - i * 0.22, 1.1, 0.9 - i * 0.22, g); mesh(G.cone, toon(0xf2f7fa), 0, 1.2 + i * 0.6, 0, 0.55 - i * 0.15, 0.45, 0.55 - i * 0.15, g); }
      world.add(g);
    }

    // ── Cellar
    const barrelM = std(0x6a4426, 0.8), bandM = std(0x2a2a2a, 0.5, { metalness: 0.5 });
    for (let i = 0; i < 5; i++) {
      const b = new THREE.Group(); b.position.set(19.2 + i * 1.1, CELLAR_Y + 0.42, 24.2); b.rotation.x = Math.PI / 2;
      mesh(G.cyl, barrelM, 0, 0, 0, 0.42, 0.9, 0.42, b); for (const y of [-0.3, 0.3]) mesh(G.cyl, bandM, 0, y, 0, 0.43, 0.05, 0.43, b);
      world.add(b);
    }
    for (let i = 0; i < 3; i++) box(0.7, 0.6, 0.7, wood, 25.8, CELLAR_Y + i * 0.6 * (i < 2 ? 1 : 0), 19.8 + (i === 2 ? 0.8 : 0), world);
    box(0.08, 1.6, 2.4, darkWood, 18.2, CELLAR_Y, 21.5, world);
    for (let r = 0; r < 4; r++) for (let k = 0; k < 6; k++) { const bt = cyl(0.05, 0.3, std(0x1f3d2e, 0.3), 18.35, CELLAR_Y + 0.25 + r * 0.35, 20.5 + k * 0.35, world, true); bt.rotation.z = Math.PI / 2; }
    cyl(0.01, 0.6, black, 22.5, 1.0 + CELLAR_Y + 0.8, 22, world, true);
    mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0xffc070 }), 22.5, CELLAR_Y + 1.75, 22, 0.08, 0.1, 0.08, world).castShadow = false;
    light(0xffb060, 8, 7, 22.5, CELLAR_Y + 1.6, 22, world);

    // ── Shed
    box(3.5, 0.9, 0.8, wood, 30.5, 0, 18.6, world); box(3.6, 0.08, 0.9, darkWood, 30.5, 0.9, 18.6, world);
    for (let i = 0; i < 4; i++) { const tl = box(0.05, 1.2, 0.05, darkWood, 36.5 + i * 0.25, 0, 18.3, world); tl.rotation.z = 0.15; }
    box(0.25, 0.3, 0.05, std(0x8a8f96, 0.4, { metalness: 0.7 }), 36.5, 0.05, 18.3, world);
    for (let i = 0; i < 3; i++) mesh(G.sphereLo, toon(0xc9b28a), 37.8, 0.3, 21 + i * 0.7, 0.35, 0.3, 0.3, world);
    const wb = new THREE.Group(); wb.position.set(32.5, 0, 23); box(1.0, 0.35, 0.6, std(0x3f6a36, 0.6), 0, 0.3, 0, wb); cyl(0.18, 0.06, black, 0.6, 0.12, 0, wb).rotation.x = Math.PI / 2; world.add(wb);
    light(0xffa040, 7, 7, 30.5, 1.6, 19.2, world);
  }

  function buildSnow(world) {
    const n = 1400, pos = new Float32Array(n * 3), spd = new Float32Array(n);
    const respawn = (i, top) => {
      let x, z, tries = 0;
      do { x = -12 + Math.random() * 64; z = -12 + Math.random() * 50; tries++; }
      while (tries < 30 && MAP && x > -1 && x < MAP.W + 1 && z > -1 && z < MAP.H + 1 && !(x > 1.5 && x < 16.5 && z > 18.5 && z < 24.5));
      pos[i * 3] = x; pos[i * 3 + 1] = top ? 9 + Math.random() * 3 : Math.random() * 12; pos[i * 3 + 2] = z; spd[i] = 0.6 + Math.random() * 0.8;
    };
    for (let i = 0; i < n; i++) respawn(i, false);
    const geo = new THREE.BufferGeometry(); geo.setAttribute("position", new THREE.BufferAttribute(pos, 3));
    snow = new THREE.Points(geo, new THREE.PointsMaterial({ color: 0xffffff, size: 0.07, transparent: true, opacity: 0.85, depthWrite: false }));
    snow.userData = { pos, spd, respawn, n };
    world.add(snow);
  }

  // ───────────── characters ─────────────
  const L0 = 0.62;
  R.makeChar = function (o) {
    const root = new THREE.Group();
    const sil = !!o.silhouette;
    const M = (c) => (sil ? toon(0x0c0c0e) : toon(c));
    const coatM = M(o.coat), skinM = M(0xf2b38f), trouserM = M(o.trousers || 0x3a3f47), bootM = M(0x231a15), hatM = M(o.hat), beardM = M(0x2a1d17), hairM = M(o.hair || 0x5a3a22);
    const eyeM = sil ? new THREE.MeshBasicMaterial({ color: 0xfff2b0 }) : toon(0xffffff), pupilM = new THREE.MeshBasicMaterial({ color: 0x111111 });

    const legs = [];
    for (const side of [-1, 1]) {
      const pivot = new THREE.Group(); pivot.position.set(side * 0.14, L0, 0);
      outline(mesh(G.cyl, trouserM, 0, -L0 / 2, 0, 0.085, L0, 0.085, pivot), 1.14);
      const foot = new THREE.Group(); foot.position.y = -L0; pivot.add(foot);
      outline(mesh(G.sphere, bootM, 0, 0.05, 0.05, 0.12, 0.08, 0.19, foot), 1.12);
      root.add(pivot); legs.push({ pivot, foot });
    }
    const upper = new THREE.Group(); upper.position.y = L0; root.add(upper);
    const body = new THREE.Group(); upper.add(body);
    outline(mesh(G.cyl, coatM, 0, 0.14, 0, 0.36, 0.3, 0.3, body), 1.05);                              // coat hem
    const torso = outline(mesh(new THREE.CapsuleGeometry(0.31, 0.34, 6, 16), coatM, 0, 0.47, 0, 1, 1, 0.82, body), 1.05);
    torso.userData.torso = true;
    if (!sil) {
      for (let i = 0; i < 3; i++) mesh(G.sphereLo, toon(o.detective ? 0x2a1d17 : 0xc9a227), 0, 0.28 + i * 0.17, 0.26, 0.03, 0.03, 0.02, body);
      if (o.detective) { mesh(G.cyl, toon(0x4a3a28), 0, 0.33, 0, 0.325, 0.06, 0.27, body); mesh(G.box, toon(0xc9a227), 0, 0.33, 0.27, 0.07, 0.05, 0.02, body); }
      const collar = new THREE.Mesh(new THREE.TorusGeometry(0.2, 0.07, 8, 20), toon(o.scarf || (o.detective ? 0x7a2b2b : 0x2a2a2a)));
      collar.rotation.x = Math.PI / 2; collar.position.y = 0.84; body.add(collar);
    }
    const arms = [];
    for (const side of [-1, 1]) {
      const a = new THREE.Group(); a.position.set(side * 0.37, 0.72, 0); a.rotation.z = side * 0.2;
      outline(mesh(new THREE.CapsuleGeometry(0.085, 0.3, 4, 10), coatM, 0, -0.2, 0, 1, 1, 1, a), 1.1);
      outline(mesh(G.sphere, skinM, 0, -0.42, 0.02, 0.1, 0.1, 0.1, a), 1.1);
      body.add(a); arms.push(a);
    }
    const head = new THREE.Group(); head.position.y = 1.22; body.add(head);
    outline(mesh(G.sphere, skinM, 0, 0, 0, 0.36, 0.36, 0.36, head), 1.04);
    for (const s of [-1, 1]) mesh(G.sphereLo, skinM, s * 0.35, -0.02, 0, 0.07, 0.1, 0.06, head);
    mesh(G.sphere, M(0xe39a78), 0, -0.03, 0.36, 0.085, 0.075, 0.09, head);                           // nose
    const eyes = [], pupils = [];
    for (const s of [-1, 1]) {
      const e = outline(mesh(G.sphere, eyeM, s * 0.13, 0.08, 0.27, 0.11, 0.12, 0.08, head), 1.12); eyes.push(e);
      if (!sil) pupils.push(mesh(G.sphereLo, pupilM, s * 0.125, 0.08, 0.35, 0.03, 0.03, 0.015, head));
    }
    const brows = [];
    if (!sil) for (const s of [-1, 1]) { const b = mesh(G.box, M(0x2a1d17), s * 0.13, 0.24, 0.31, 0.17, 0.035, 0.04, head); b.rotation.z = -s * 0.12; brows.push(b); }
    const mouth = sil ? null : mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0x3b1410 }), 0, -0.17, 0.32, 0.07, 0.025, 0.02, head);
    if (o.beard) {
      const beard = new THREE.Mesh(new THREE.SphereGeometry(0.37, 20, 12, 0, Math.PI * 2, Math.PI * 0.42, Math.PI * 0.58), beardM);
      beard.scale.set(1.02, 1.25, 0.98); beard.position.set(0, -0.02, 0.03); beard.castShadow = true; head.add(beard); outline(beard, 1.04);
      for (const s of [-1, 1]) { const m = mesh(G.sphere, beardM, s * 0.07, -0.1, 0.34, 0.09, 0.04, 0.05, head); m.rotation.z = s * 0.3; }
      if (mouth) mouth.position.set(0, -0.2, 0.36);
    } else {
      outline(mesh(G.sphere, hairM, 0, 0.05, -0.06, 0.385, 0.38, 0.36, head), 1.03);
    }
    const sweat = [];
    if (!sil) for (const [x, y] of [[0.3, 0.2], [-0.32, 0.1]]) { const d = mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0x9fd3ef }), x, y, 0.2, 0.035, 0.055, 0.035, head); d.visible = false; sweat.push(d); }
    // hat
    const hat = new THREE.Group(); hat.position.y = 0.2; head.add(hat);
    const style = o.hatStyle || "beanie";
    if (style === "fedora") {
      outline(mesh(G.cyl, hatM, 0, 0.0, 0, 0.56, 0.035, 0.56, hat), 1.03);
      outline(mesh(G.cyl, hatM, 0, 0.16, 0, 0.3, 0.3, 0.3, hat), 1.04);
      mesh(G.cyl, M(0x7a2b2b), 0, 0.05, 0, 0.305, 0.07, 0.305, hat);
      hat.rotation.z = -0.08;
    } else if (style === "tophat") {
      outline(mesh(G.cyl, hatM, 0, 0.0, 0, 0.5, 0.035, 0.5, hat), 1.03);
      outline(mesh(G.cyl, hatM, 0, 0.3, 0, 0.29, 0.58, 0.29, hat), 1.03);
      mesh(G.cyl, M(0xff2d55), 0, 0.06, 0, 0.295, 0.08, 0.295, hat);
    } else if (style !== "none") {
      const dome = new THREE.Mesh(new THREE.SphereGeometry(0.39, 20, 12, 0, Math.PI * 2, 0, Math.PI / 2), hatM); dome.position.y = -0.08; dome.castShadow = true; hat.add(dome); outline(dome, 1.03);
      const band = new THREE.Mesh(new THREE.TorusGeometry(0.37, 0.07, 8, 24), hatM); band.rotation.x = Math.PI / 2; band.position.y = -0.06; hat.add(band);
      outline(mesh(G.sphereLo, M(0xf4f1ea), 0, 0.36, 0, 0.09, 0.09, 0.09, hat), 1.1);
    }
    root.traverse((n) => { if (n.isMesh && !n.userData.outline) { n.castShadow = true; } });

    const h = {
      root, legs, upper, body, arms, head, eyes, pupils, brows, sweat, mouth, phase: 0, yaw: 0, ghost: false,
      setPose(p) {
        const s = Math.max(0.06, p.leg == null ? 1 : p.leg);
        for (const { pivot, foot } of legs) { pivot.position.y = L0 * s; pivot.scale.y = s; foot.scale.y = 1 / s; }
        const swing = p.moving ? Math.sin(p.walk || 0) : 0;
        const crawl = p.crawl && p.moving;
        legs[0].pivot.rotation.x = swing * 0.6; legs[1].pivot.rotation.x = -swing * 0.6;
        upper.position.y = L0 * s + (p.moving ? Math.abs(Math.cos(p.walk || 0)) * 0.035 : 0);
        body.rotation.x = crawl ? 0.55 : p.moving ? 0.06 : 0;
        arms[0].rotation.x = crawl ? -1.3 + swing * 0.3 : -swing * 0.5; arms[1].rotation.x = crawl ? -1.3 - swing * 0.3 : swing * 0.5;
        const face = p.face || "calm", panic = face === "panic";
        for (const pu of pupils) pu.scale.set(panic ? 0.018 : 0.03, panic ? 0.018 : 0.03, 0.015);
        for (const e of eyes) e.scale.set(panic ? 0.125 : 0.11, panic ? 0.14 : 0.12, 0.08);
        brows.forEach((b, i) => { b.rotation.z = (i ? -1 : 1) * (panic ? -0.35 : face === "sweat" ? -0.15 : 0.12); b.position.y = panic ? 0.27 : 0.24; });
        for (const d of sweat) d.visible = face !== "calm";
        if (mouth) mouth.scale.set(panic ? 0.06 : 0.07, panic ? 0.05 : 0.025, 0.02);
        if (p.yaw != null) {
          let d = p.yaw - h.yaw; while (d > Math.PI) d -= Math.PI * 2; while (d < -Math.PI) d += Math.PI * 2;
          h.yaw += d * Math.min(1, (p.dt || 0.016) * 10);
          root.rotation.y = h.yaw;
        }
        if (p.x != null) root.position.set(p.x, p.y || 0, p.z);
        root.visible = p.visible !== false;
        if (p.ghost != null && p.ghost !== h.ghost) {
          h.ghost = p.ghost;
          root.traverse((n) => {
            if (!n.isMesh) return;
            if (n.userData.outline) { n.visible = !p.ghost; return; }
            n.material.transparent = p.ghost; n.material.opacity = p.ghost ? 0.3 : 1; n.castShadow = !p.ghost;
          });
        }
      },
    };
    return h;
  };
  // Materials are shared through the cache, so ghosting would fade everyone; give each character its own copies.
  R.char = function (o) {
    const h = R.makeChar(o);
    h.root.traverse((n) => { if (n.isMesh && !n.userData.outline) n.material = n.material.clone(); });
    scene.add(h.root); dyn.add(h.root);
    return h;
  };

  // ───────────── clue markers ─────────────
  let tentTex = new Map();
  function tentTexture(n) {
    if (tentTex.has(n)) return tentTex.get(n);
    const t = canvasTex(128, 128, (g) => { g.fillStyle = "#ffd21a"; g.fillRect(0, 0, 128, 128); g.fillStyle = "#111"; g.font = "bold 84px sans-serif"; g.textAlign = "center"; g.textBaseline = "middle"; g.fillText(String(n), 64, 70); });
    tentTex.set(n, t); return t;
  }
  R.clue = function (ux, uz) {
    const g = new THREE.Group(); const y = R.heightAt(ux, uz);
    g.position.set(ux, y, uz);
    const ringM = new THREE.MeshBasicMaterial({ color: 0xffd678, transparent: true, opacity: 0.7, depthWrite: false });
    const ring = new THREE.Mesh(new THREE.RingGeometry(0.3, 0.4, 32), ringM); ring.rotation.x = -Math.PI / 2; ring.position.y = 0.02; g.add(ring);
    const glass = new THREE.Group(); glass.position.y = 0.9; g.add(glass);
    const brassM = std(0xc9a227, 0.3, { metalness: 0.8, emissive: 0x5a4010, emissiveIntensity: 0.8 });
    glass.add(new THREE.Mesh(new THREE.TorusGeometry(0.16, 0.035, 8, 24), brassM));
    const lens = new THREE.Mesh(new THREE.CircleGeometry(0.15, 24), new THREE.MeshBasicMaterial({ color: 0xc9e8ff, transparent: true, opacity: 0.35, side: THREE.DoubleSide })); glass.add(lens);
    const handle = mesh(G.cylLo, std(0x5a3a22, 0.6), 0, -0.3, 0, 0.03, 0.26, 0.03, glass); handle.castShadow = false;
    scene.add(g); dyn.add(g);
    return {
      g,
      tick(t) { glass.rotation.y = t * 1.6; glass.position.y = 0.9 + Math.sin(t * 2.4) * 0.08; ringM.opacity = 0.45 + Math.sin(t * 4) * 0.25; },
      found(n) {
        g.remove(ring); g.remove(glass);
        const tm = std(0xffffff, 0.6, { map: tentTexture(n), side: THREE.DoubleSide });
        for (const s of [-1, 1]) { const p = new THREE.Mesh(new THREE.PlaneGeometry(0.28, 0.26), tm); p.position.set(0, 0.11, s * 0.06); p.rotation.x = s * 0.45; p.rotation.y = s === 1 ? 0 : Math.PI; p.castShadow = true; g.add(p); }
      },
    };
  };

  R.remove = function (obj) { scene.remove(obj); dyn.delete(obj); };
  R.clearDynamic = function () { for (const o of dyn) scene.remove(o); dyn.clear(); };
  R.setMoneyTree = (on) => { if (moneyTree) moneyTree.visible = on; if (pedestalSpot) pedestalSpot.intensity = on ? 25 : 0; };
  R.setLightLevel = (f) => { lightLevel = f; };
  R.moneyTree = () => moneyTree;

  // ───────────── per-frame game render ─────────────
  const tmpA = new THREE.Vector3(), tmpB = new THREE.Vector3();
  R.updateCamera = function (dt, tx, ty, tz) {
    const k = cam.ready ? Math.min(1, dt * 6) : 1;
    cam.tx += (tx - cam.tx) * k; cam.ty += (ty - cam.ty) * k; cam.tz += (tz - cam.tz) * k; cam.ready = true;
    const cp = Math.cos(cam.pitch), spp = Math.sin(cam.pitch);
    camera.position.set(cam.tx + Math.sin(cam.yaw) * cp * cam.dist, cam.ty + spp * cam.dist, cam.tz + Math.cos(cam.yaw) * cp * cam.dist);
    camera.lookAt(cam.tx, cam.ty, cam.tz);
  };
  R.setCamera = function (px, py, pz, lx, ly, lz) { camera.position.set(px, py, pz); camera.lookAt(lx, ly, lz); cam.ready = false; };

  function fadeWalls(focusX, focusZ) {
    const cx = camera.position.x, cz = camera.position.z;
    const dx = focusX - cx, dz = focusZ - cz, len2 = dx * dx + dz * dz;
    for (const w of walls) {
      const { cx: wx, cz: wz } = w.userData;
      let fade = false;
      if (Math.abs(wx - focusX) < 10 && Math.abs(wz - focusZ) < 10) {
        const t = ((wx - cx) * dx + (wz - cz) * dz) / len2;
        if (t > 0 && t < 1.02) {
          const px = cx + dx * t, pz = cz + dz * t;
          const lateral = (wx - px) ** 2 + (wz - pz) ** 2;
          fade = (t > 0.35 && lateral < 4.5 * 4.5) || lateral < 1.4 * 1.4 || (Math.hypot(wx - focusX, wz - focusZ) < 1.6 && t > 0.6);
        }
      }
      if (fade !== w.userData.faded) { w.userData.faded = fade; w.material = fade ? w.userData.mats.fade : w.userData.mats.solid; w.castShadow = !fade; }
    }
  }

  R.render = function (dt, t, focus, opts) {
    opts = opts || {};
    // lights
    const lo = opts.lightsOut ? 0.12 : 1;
    const f = lo * lightLevel;
    hemi.intensity = 1.1 * f; moon.intensity = 1.6 * f;
    for (const l of pointLights) l.intensity = l.userData.base * (opts.lightsOut ? 0.05 : lightLevel);
    if (fireLight) fireLight.intensity = fireLight.userData.base * (0.8 + Math.sin(t * 13) * 0.1 + Math.sin(t * 7.3) * 0.1) * (opts.lightsOut ? 0.25 : lightLevel);
    if (fire) fire.children.forEach((c) => { if (c.userData.flame != null) { c.scale.y = 0.4 + Math.sin(t * 11 + c.userData.flame * 1.7) * 0.12; c.scale.x = c.scale.z = 0.11 + Math.sin(t * 9 + c.userData.flame) * 0.02; } });
    if (moneyTree && moneyTree.visible) moneyTree.rotation.y = t * 0.4;
    // moonlight shadow follows the focus
    moon.position.set(focus.x - 10, 20, focus.z + 8); moon.target.position.set(focus.x, 0, focus.z); moon.target.updateMatrixWorld();
    // flashlight during lights-out
    if (opts.flashFrom) {
      flashlight.intensity = opts.lightsOut ? 40 : 0;
      flashlight.position.set(opts.flashFrom.x, opts.flashFrom.y + 1.4, opts.flashFrom.z);
      flashlight.target.position.set(opts.flashFrom.x + Math.sin(opts.flashFrom.yaw) * 3, 0, opts.flashFrom.z + Math.cos(opts.flashFrom.yaw) * 3);
      flashlight.target.updateMatrixWorld();
    }
    // snow
    if (snow) {
      const { pos, spd, respawn, n } = snow.userData;
      for (let i = 0; i < n; i++) {
        pos[i * 3 + 1] -= spd[i] * dt;
        if (pos[i * 3 + 1] < 0) respawn(i, true);
      }
      snow.geometry.attributes.position.needsUpdate = true;
    }
    if (opts.fadeWalls !== false) fadeWalls(focus.x, focus.z); else for (const w of walls) if (w.userData.faded) { w.userData.faded = false; w.material = w.userData.mats.solid; }
    renderer.render(scene, camera);
  };

  R.project = function (x, y, z, w, h) {
    tmpA.set(x, y, z).project(camera);
    if (tmpA.z > 1) return null;
    return { x: (tmpA.x * 0.5 + 0.5) * w, y: (-tmpA.y * 0.5 + 0.5) * h };
  };
  R.cameraYaw = () => cam.yaw;

  // ───────────── menu scene: interrogation room ─────────────
  let menuChar, menuRed, menuRedBulb;
  function buildMenuScene() {
    menuScene = new THREE.Scene();
    menuScene.background = new THREE.Color(0x08090b);
    menuScene.fog = new THREE.Fog(0x08090b, 8, 18);
    menuCam = new THREE.PerspectiveCamera(40, 1, 0.1, 50);
    menuCam.position.set(-0.6, 1.9, 7.2); menuCam.lookAt(-0.6, 1.2, 0);
    menuScene.add(new THREE.HemisphereLight(0x6a7a8a, 0x100c0a, 0.35));
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(30, 30), std(0x2a2622, 0.9, { map: (() => { const t = TEX.planks("#3a3028"); t.repeat.set(6, 6); return t; })() }));
    floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; menuScene.add(floor);
    const wallM = std(0x2a2e33, 0.95);
    const back = new THREE.Mesh(new THREE.PlaneGeometry(30, 8), wallM); back.position.set(0, 4, -3); back.receiveShadow = true; menuScene.add(back);
    // one-way mirror
    const mirror = new THREE.Mesh(new THREE.PlaneGeometry(3, 1.4), std(0x1a2430, 0.1, { metalness: 0.6 })); mirror.position.set(-2.4, 2.1, -2.98); menuScene.add(mirror);
    // table + chairs + lie detector
    const tableM = std(0x3a2a1c, 0.6);
    box(2.4, 0.08, 1.2, tableM, 1.5, 0.82, 0.6, menuScene);
    for (const [a, b] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) cyl(0.04, 0.82, tableM, 1.5 + a * 1.1, 0, 0.6 + b * 0.5, menuScene, true);
    box(0.7, 0.3, 0.45, std(0x2f3a33, 0.5), 2.2, 0.9, 0.6, menuScene);
    menuRedBulb = mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0x551020 }), 2.35, 1.27, 0.6, 0.07, 0.07, 0.07, menuScene);
    const paper = box(0.8, 0.02, 0.2, toon(0xefe6d2), 1.9, 0.905, 0.62, menuScene); paper.rotation.y = 0.2;
    menuRed = new THREE.PointLight(0xff2d55, 0, 8, 2); menuRed.position.set(2.35, 1.4, 0.8); menuScene.add(menuRed);
    // hanging lamp + spotlight
    cyl(0.01, 2.2, std(0x111111), 1.5, 3.4, 0.6, menuScene, true);
    const shadeM = mesh(G.cone, std(0x1a1a1a, 0.5), 1.5, 3.3, 0.6, 0.35, 0.3, 0.35, menuScene); shadeM.castShadow = false;
    mesh(G.sphereLo, new THREE.MeshBasicMaterial({ color: 0xfff0c0 }), 1.5, 3.12, 0.6, 0.08, 0.06, 0.08, menuScene).castShadow = false;
    const spot = new THREE.SpotLight(0xffc07a, 60, 12, 0.6, 0.45, 1.4); spot.position.set(1.5, 3.1, 0.6); spot.target.position.set(1.5, 0, 0.6);
    spot.castShadow = true; spot.shadow.mapSize.set(1024, 1024); menuScene.add(spot, spot.target);
    const coneM = new THREE.MeshBasicMaterial({ color: 0xffc07a, transparent: true, opacity: 0.06, depthWrite: false, side: THREE.DoubleSide });
    const beam = new THREE.Mesh(new THREE.ConeGeometry(1.9, 3.1, 32, 1, true), coneM); beam.position.set(1.5, 1.55, 0.6); menuScene.add(beam);
    // chair + silhouette suspect behind the table
    box(0.6, 0.5, 0.6, std(0x1a1a1a), 1.5, 0, -0.35, menuScene); box(0.6, 0.9, 0.08, std(0x1a1a1a), 1.5, 0.5, -0.66, menuScene);
    menuChar = R.makeChar({ coat: 0x000000, hat: 0x000000, silhouette: true, beard: true });
    menuChar.root.position.set(1.5, 0, -0.1); menuChar.root.scale.setScalar(1.25);
    menuScene.add(menuChar.root);
  }
  R.renderMenu = function (dt, t, leg, flash) {
    menuChar.setPose({ leg, face: "panic" });
    menuChar.head.rotation.y = Math.sin(t * 0.6) * 0.25;
    menuRed.intensity = flash * 30;
    menuRedBulb.material.color.setHex(flash > 0.05 ? 0xff2d55 : 0x551020);
    menuCam.position.x = -0.6 + Math.sin(t * 0.15) * 0.25;
    menuCam.lookAt(-0.6, 1.2, 0);
    renderer.render(menuScene, menuCam);
  };

  // ───────────── portraits (second renderer, copied into 2D canvases) ─────────────
  const portraitCache = new Map();
  R.portrait = function (target, key, o, pose) {
    if (!pRenderer) {
      pRenderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
      pRenderer.outputColorSpace = THREE.SRGBColorSpace; pRenderer.toneMapping = THREE.ACESFilmicToneMapping;
      pRenderer.setPixelRatio(1);
      pScene = new THREE.Scene();
      pScene.add(new THREE.HemisphereLight(0xcfe0f0, 0x3a2a20, 1.4));
      const key1 = new THREE.DirectionalLight(0xffd6a0, 2.2); key1.position.set(2, 3, 4); pScene.add(key1);
      const rim = new THREE.DirectionalLight(0x88b0ff, 1.2); rim.position.set(-3, 2, -2); pScene.add(rim);
      pCam = new THREE.PerspectiveCamera(30, 1, 0.1, 20);
    }
    let h = portraitCache.get(key);
    if (!h) { h = R.makeChar(o); portraitCache.set(key, h); }
    pScene.children.filter((c) => c.userData.portrait).forEach((c) => pScene.remove(c));
    h.root.userData.portrait = true; pScene.add(h.root);
    h.root.rotation.y = pose.yaw || 0.35; h.yaw = h.root.rotation.y;
    h.setPose(Object.assign({ moving: false }, pose, { yaw: null, x: null }));
    const w = target.width, hh = target.height;
    pRenderer.setSize(w, hh, false);
    pCam.aspect = w / hh; pCam.updateProjectionMatrix();
    const focusY = pose.focusY != null ? pose.focusY : 1.35;
    pCam.position.set(0, focusY + 0.25, pose.dist || 5.2); pCam.lookAt(0, focusY, 0);
    pRenderer.render(pScene, pCam);
    const g = target.getContext("2d");
    g.clearRect(0, 0, w, hh); g.drawImage(pRenderer.domElement, 0, 0, w, hh);
  };

  global.R3 = R;
})(window);
