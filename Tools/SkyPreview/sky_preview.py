#!/usr/bin/env python3
"""
Vorschau des Himmels-Shaders (Resources/RePlanetSky.shader) ohne Unity.
Die Shader-Mathematik und die Paletten aus Runtime/Render/Atmosphere.cs werden 1:1 in NumPy nachgerechnet.
Ergebnis: PNG-Vorschauen je Planet und Tageszeit. Das sind KEINE Spielszenen-Screenshots, sondern eine
Kontrolle von Farbverlauf, Wolken, Himmelskörpern, Sternen und Polarlicht.
Aufruf: python3 sky_preview.py <Ausgabeordner>
"""
import math, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.abspath(__file__))
ATMO = os.path.join(ROOT, "../../RePlanet/Assets/RePlanet/Runtime/Render/Atmosphere.cs")

def hexcol(h):
    h = int(h, 16)
    return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], np.float32) / 255.0

def lin(c):  # sRGB → linear (Unity konvertiert Farbeigenschaften im Linear-Farbraum)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

def srgb(c):
    c = np.clip(c, 0, None)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)

def parse_palettes():
    src = open(ATMO, encoding="utf-8").read()
    planets = {}
    for m in re.finditer(r'\{ "(\w+)", new PlanetSky \{(.*?)\} \},', src, re.S):
        name, body = m.group(1), m.group(2)
        p = {}
        for key in ("Day", "Dusk", "Night", "Storm"):
            mm = re.search(key + r" = Pal\(([^)]*)\)", body)
            a = [x.strip() for x in mm.group(1).split(",")]
            cols = [hexcol(x) for x in a[:10]]
            p[key] = dict(Zenith=cols[0], Horizon=cols[1], Haze=cols[2], Ground=cols[3], CloudLight=cols[4], CloudShadow=cols[5],
                          NebA=cols[6], NebB=cols[7], Sun=cols[8], Fog=cols[9], Cover=float(a[10].rstrip("f")), Nebula=float(a[11].rstrip("f")), HazeStrength=float(a[12].rstrip("f")))
        for key in ("P1", "P2"):
            mm = re.search(key + r" = Body\(new Vector3\(([^)]*)\), ([^)]*)\)", body)
            v = [float(x.strip().rstrip("f")) for x in mm.group(1).split(",")]
            a = [x.strip() for x in mm.group(2).split(",")]
            d = np.array(v, np.float32); d /= np.linalg.norm(d)
            p[key] = dict(Dir=d, Radius=math.radians(float(a[0].rstrip("f"))), A=hexcol(a[1]), B=hexcol(a[2]), Rim=hexcol(a[3]),
                          Bands=float(a[4].rstrip("f")), Seed=float(a[5].rstrip("f")), RimS=float(a[6].rstrip("f")))
        def num(key, default):
            mm = re.search(key + r" = (-?[\d.]+)f", body)
            return float(mm.group(1)) if mm else default
        p["SunAzimuth"] = num("SunAzimuth", 0); p["CloudScale"] = num("CloudScale", 0.9); p["CloudDensity"] = num("CloudDensity", 2.4); p["Aurora"] = num("Aurora", 0)
        p["Bank"] = num("Bank", 0.8); p["BankHeight"] = num("BankHeight", 1.0)
        p["Saturation"] = num("Saturation", 1.2); p["Contrast"] = num("Contrast", 1.08)
        for key, default in (("GradeShadow", "4D7390"), ("GradeHighlight", "FFD9A6")):
            mm = re.search(key + r" = Mats\.C\(0x([0-9A-Fa-f]+)\)", body)
            p[key] = hexcol(mm.group(1) if mm else default)
        planets[name] = p
    return planets

# ------------------------------------------------------------------ Shader-Funktionen (wie HLSL)
def frac(x): return x - np.floor(x)

def hash13(p):
    p = frac(p * 0.1031)
    d = p[..., 0] * (p[..., 2] + 31.32) + p[..., 1] * (p[..., 1] + 31.32) + p[..., 2] * (p[..., 0] + 31.32)
    p = p + d[..., None]
    return frac((p[..., 0] + p[..., 1]) * p[..., 2])

def noise3(x):
    i = np.floor(x); f = frac(x); f = f * f * (3 - 2 * f)
    def h(o): return hash13(i + np.array(o, np.float32))
    a = (h([0,0,0]) * (1 - f[...,0]) + h([1,0,0]) * f[...,0]) * (1 - f[...,1]) + (h([0,1,0]) * (1 - f[...,0]) + h([1,1,0]) * f[...,0]) * f[...,1]
    b = (h([0,0,1]) * (1 - f[...,0]) + h([1,0,1]) * f[...,0]) * (1 - f[...,1]) + (h([0,1,1]) * (1 - f[...,0]) + h([1,1,1]) * f[...,0]) * f[...,1]
    return a * (1 - f[...,2]) + b * f[...,2]

def fbm(p, octaves, mul, off):
    v = 0; a = 0.5
    for _ in range(octaves):
        v = v + a * noise3(p); p = p * mul + np.array(off, np.float32); a *= 0.5
    return v

def fbm5(p): return fbm(p, 5, 2.03, [1.7, 9.2, 3.1])
def fbm3(p): return fbm(p, 3, 2.07, [5.1, 1.3, 7.7])

def sat(x): return np.clip(x, 0, 1)
def smoothstep(a, b, x):
    t = sat((x - a) / (b - a)); return t * t * (3 - 2 * t)
def mix(a, b, t): return a + (b - a) * t

def body(d, col, B, sun, vis):
    pdir = B["Dir"]; r = B["Radius"]
    dd = np.clip(d @ pdir, -1, 1); ang = np.arccos(dd)
    outer = sat(1 - (ang - r) / (r * 0.35))
    halo = (ang > r)[..., None] * (lin(B["Rim"]) * (outer * outer * 0.45 * B["RimS"] * vis)[..., None])
    col = col + halo
    inside = ang <= r
    if not inside.any(): return col
    upv = np.array([0, 1, 0], np.float32) if abs(pdir[1]) < 0.98 else np.array([1, 0, 0], np.float32)
    right = np.cross(upv, pdir); right /= np.linalg.norm(right); up2 = np.cross(pdir, right)
    s = math.sin(r)
    u = (d @ right) / s; v = (d @ up2) / s
    r2 = sat(u * u + v * v); z = np.sqrt(1 - r2)
    n = right * u[..., None] + up2 * v[..., None] - pdir * z[..., None]
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
    pat = fbm5(n * 3.2 + B["Seed"])
    bands = np.sin(n[..., 1] * 18 + pat * 5) * 0.5 + 0.5
    A, Bc, Rim = lin(B["A"]), lin(B["B"]), lin(B["Rim"])
    surf = mix(A, Bc, sat(pat * 1.3 - 0.15)[..., None])
    surf = mix(surf, Bc * 0.8, (bands * B["Bands"])[..., None])
    crater = smoothstep(0.62, 0.7, fbm3(n * 9 + B["Seed"] * 3))
    surf = surf * (1 - crater * 0.25)[..., None]
    light = sat((n @ sun) * 1.1 + 0.05)
    lit = surf * (light * 1.05 + 0.04)[..., None]
    limb = (1 - z) ** 2.5
    lit = lit + Rim * (limb * B["RimS"] * (0.35 + light * 0.9))[..., None]
    edge = smoothstep(1.0, 0.96, np.sqrt(r2))
    mixed = lit + col * (1 - vis) * 0.9
    newc = mix(col, mixed, (edge * sat(vis + 0.25))[..., None])
    return np.where(inside[..., None], newc, col)

def render(planet, P, phase, storm, W=960, H=540, yaw_deg=None, pitch_deg=8, t=40.0):
    # Palette wie Atmosphere.LateUpdate
    elev = math.sin((phase - 0.25) * math.pi * 2)
    dusk = min(max(1 - abs(elev) * 3.2, 0), 1)
    dist = abs(phase - 0.5) * 2
    darkt = min(max((dist - 0.5) / 0.16, 0), 1); dark = darkt * darkt * (3 - 2 * darkt)
    def lerpP(a, b, k):
        return {key: (a[key] + (b[key] - a[key]) * k) for key in a}
    pal = lerpP(P["Day"], P["Dusk"], dusk); pal = lerpP(pal, P["Night"], dark); pal = lerpP(pal, P["Storm"], storm * (1 - dark * 0.6))
    elev_deg = -12 + (elev + 1) * 0.5 * 74
    az = math.radians(P["SunAzimuth"]); el = math.radians(max(elev_deg, 3))
    # Quaternion.Euler(el, az, 0) * back
    sun = np.array([-math.sin(az) * math.cos(el), math.sin(el), -math.cos(az) * math.cos(el)], np.float32)
    if yaw_deg is None: yaw_deg = math.degrees(math.atan2(sun[0], sun[2])) + 25
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    fov = math.radians(70)
    xs = (np.arange(W) + 0.5) / W * 2 - 1; ys = 1 - (np.arange(H) + 0.5) / H * 2
    X, Y = np.meshgrid(xs * math.tan(fov / 2) * W / H, ys * math.tan(fov / 2))
    d = np.stack([X, Y, np.ones_like(X)], -1).astype(np.float32)
    cp, sp = math.cos(pitch), math.sin(pitch)
    d = np.stack([d[..., 0], d[..., 1] * cp + d[..., 2] * sp, -d[..., 1] * sp + d[..., 2] * cp], -1)
    cy, sy = math.cos(yaw), math.sin(yaw)
    d = np.stack([d[..., 0] * cy + d[..., 2] * sy, d[..., 1], -d[..., 0] * sy + d[..., 2] * cy], -1)
    d /= np.linalg.norm(d, axis=-1, keepdims=True)
    h = d[..., 1]
    Z, Ho, Hz, G = lin(pal["Zenith"]), lin(pal["Horizon"]), lin(pal["Haze"]), lin(pal["Ground"])
    up = sat(h)
    col = mix(Ho, Z, np.power(up, 0.38)[..., None])
    col = np.where((h < 0)[..., None], mix(Ho, G, sat(-h * 5)[..., None]), col)
    sunI = 3 * (1 - storm * 0.8) if elev > -0.05 else 0
    sunGlow = (1 + dusk * 0.8) * (1 - storm * 0.7) if elev > -0.05 else 0
    sdot = d @ sun
    mie = sat(sdot) ** 5 * (0.35 + 0.65 * (1 - up)) + sat(sdot) ** 40 * 0.6
    col = col + lin(pal["Sun"]) * (1 - dark * 0.9) * (mie * 0.35 * sunGlow)[..., None]
    neb =fbm5(d * 2.3 + np.array([0, t * 0.003, 0], np.float32))
    neb2 = fbm3(d * 5.1 + neb[..., None] * 1.8)
    nebCol = mix(lin(pal["NebB"]), lin(pal["NebA"]), sat(neb2 * 1.6 - 0.3)[..., None])
    col = col + nebCol * (sat(neb * 1.8 - 0.55) * pal["Nebula"] * (1 - storm) * sat(h * 3 + 0.2))[..., None]
    stars = dark * (1 - storm * 0.9)
    if stars > 0.001:
        spp = d * 220; cell = np.floor(spp); rnd = hash13(cell); f = frac(spp) - 0.5
        star = (rnd > 0.9965) * sat(1 - np.linalg.norm(f, axis=-1) * 3.2)
        tw = 0.6 + 0.4 * np.sin(t * 3 + rnd * 80)
        sc = mix(np.array([0.75, 0.85, 1.0], np.float32), np.array([1, 0.85, 0.7], np.float32), hash13(cell + 7)[..., None])
        col = col + sc * (star * tw * 2.2 * stars * sat(h * 6 + 0.3))[..., None]
        dust = sat(fbm3(d * 14) * 1.35 - 0.4) ** 3
        col = col + np.array([0.6, 0.65, 0.9], np.float32) * (dust * 0.25 * stars)[..., None]
    aur = P["Aurora"] * (0.3 + 0.7 * dark) * (1 - storm)
    if aur > 0.001:
        wave = fbm3(np.stack([d[..., 0] * 2.2, d[..., 2] * 2.2, np.full_like(h, t * 0.04)], -1)) * 3
        center = 0.28 + 0.08 * np.sin(d[..., 0] * 3 + wave + t * 0.15)
        curtain = np.where(h < center, np.exp(-((h - center) * 16) ** 2), np.exp(-((h - center) * 3.5) ** 2))
        rays = (0.5 + 0.5 * np.sin(d[..., 0] * 70 + d[..., 2] * 31 + wave * 7 + t * 0.6)) ** 3
        rays = rays * 0.75 + 0.25 * fbm3(np.stack([d[..., 0] * 20, h * 3, np.full_like(h, t * 0.1)], -1))
        ac = mix(lin(np.array([0.2, 1, 0.6], np.float32)), lin(np.array([0.6, 0.3, 1], np.float32)), sat((h - center) * 4)[..., None])
        col = col + ac * (curtain * rays * aur * 0.9 * (h > 0))[..., None]
    vis = (0.85 + 0.15 * dark) * (1 - storm * 0.85)
    col = body(d, col, P["P1"], sun, vis)
    col = body(d, col, P["P2"], sun, vis)
    sd = d @ sun; cosS = math.cos(0.03 + dusk * 0.015)
    disc = smoothstep(cosS, cosS + (1 - cosS) * 0.25, sd)
    glow = sat(sd) ** 10 * 0.45 + sat(sd) ** 90 * 0.8
    col = col + lin(pal["Sun"]) * (1 - dark * 0.9) * ((glow + disc * sunI) * (h > -0.04))[..., None]
    # Wolken (gekrümmte Schale + Selbstverschattung, wie im Shader)
    def cloudField(p):
        q = fbm3(p * 0.45)
        c = fbm5(p * 1.1 + np.stack([q * 2.2, q * 1.4, q], -1))
        billow = 1 - np.abs(noise3(p * 2.6) * 2 - 1)
        return c * 0.8 + billow * 0.25 + q * 0.2
    R, Hc = 8.0, 1.0
    hh = np.maximum(h, 0)
    tt = -R * hh + np.sqrt(R * R * hh * hh + 2 * R * Hc + Hc * Hc)
    p = d * (tt * P["CloudScale"])[..., None]
    wdir = np.array([1, 0.3], np.float32); wdir /= np.linalg.norm(wdir)
    p = p + np.array([wdir[0], 0, wdir[1]], np.float32) * t * (0.02 + storm * 0.08) + np.array([0, t * 0.004, 0], np.float32)
    c = cloudField(p)
    cdens = P["CloudDensity"] + storm * 1.5
    dens = sat((c - (1 - pal["Cover"]) * 0.85) * cdens) * sat(h * 14 + 0.15)
    toSun = np.array([sun[0], 0, sun[2]], np.float32); toSun /= np.linalg.norm(toSun) + 1e-4
    cs = cloudField(p + toSun * 0.35)
    lightAmt = sat(0.5 + (c - cs) * 4.0)
    towards = sat(sd * 0.5 + 0.5) ** 3
    shade = sat(c * 1.3 - 0.2)
    thick = sat((c - (1 - pal["Cover"]) * 0.85) * cdens * 0.5)
    cc = mix(lin(pal["CloudShadow"]), lin(pal["CloudLight"]), sat(lightAmt * 0.8 + shade * 0.25 - thick * 0.35 + 0.15)[..., None])
    sunC = lin(pal["Sun"]) * (1 - dark * 0.9)
    cc = cc + sunC * (towards * 0.3 * lightAmt)[..., None] + sunC * (sat(1 - dens) ** 2 * towards * 0.45)[..., None]
    cm = h > -0.02
    col = np.where(cm[..., None], mix(col, cc, (dens * 0.9)[..., None]), col)
    # Wolkenbank am Horizont
    bankS = P["Bank"] * (1 - 0.3 * dark) * (1 + storm * 0.25)
    bankH = P["BankHeight"] * (1 + storm * 0.4)
    if bankS > 0.001:
        az = np.stack([d[..., 0], d[..., 2]], -1); az /= np.linalg.norm(az, axis=-1, keepdims=True) + 1e-4
        cspeed = 0.02 + storm * 0.08
        drift = wdir * t * cspeed * 0.15
        top = 0.05 + 0.2 * bankH * sat(fbm3(np.stack([az[..., 0] * 2.3 + drift[0], np.full_like(h, 0.7), az[..., 1] * 2.3 + drift[1]], -1)) * 2 - 0.45)
        hn = sat(h / top)
        pb = np.stack([az[..., 0] * 5 + drift[0], h * 16, az[..., 1] * 5 + drift[1]], -1) * P["CloudScale"]
        n = fbm5(pb)
        shape = n + (1 - hn) * 0.42 - hn * hn * 0.25
        bd = sat((shape - 0.62) * 5) * sat(1 - hn * 0.98) * sat(h * 60 + 1) * bankS
        n2 = fbm3(pb + np.array([0, 0.9, 0], np.float32))
        bl = sat(0.35 + (n - n2) * 2.5 + hn * 0.55)
        tsb = sat(sd * 0.5 + 0.5) ** 4
        bc = mix(lin(pal["CloudShadow"]), lin(pal["CloudLight"]), bl[..., None])
        bc = bc + sunC * (tsb * (0.25 + bl * 0.45))[..., None] + sunC * (sat(1 - bd) ** 3 * tsb * 0.6)[..., None]
        bc = mix(bc, Hz, (sat(0.45 - h * 2) * pal["HazeStrength"])[..., None])
        bm = (h > -0.03) & (h < 0.3)
        col = np.where(bm[..., None], mix(col, bc, (sat(bd) * 0.95)[..., None]), col)
    haze = np.exp(-np.abs(h) * 14) * pal["HazeStrength"]
    col = mix(col, Hz, sat(haze)[..., None])
    # Vordergrund: Hügelsilhouette im Nebel (nur zur Einordnung)
    xs_ang = np.arctan2(d[..., 0], d[..., 2])
    ridge = -0.02 + 0.035 * np.sin(xs_ang * 5 + 1) + 0.02 * np.sin(xs_ang * 13 + 2) + 0.01 * np.sin(xs_ang * 31)
    fog = lin(mix(pal["Fog"], pal["Haze"], 0.35))
    land = mix(G * 0.6, fog, 0.55)
    col = np.where((h < ridge)[..., None], land, col)
    img = (srgb(col) * 255).clip(0, 255).astype(np.uint8)
    graded = (srgb(post_grade(col, P, pal, dark, dusk, storm)) * 255).clip(0, 255).astype(np.uint8)
    return Image.fromarray(img, "RGB"), Image.fromarray(graded, "RGB")

def lum(c): return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722

def post_grade(col, P, pal, dark, dusk, storm):
    """Farbkorrektur wie Resources/RePlanetPostFX.shader (Pass Zusammensetzen) mit den Werten aus Atmosphere.UpdateLook
    (ohne Bloom, Sonnenstrahlen, Korn – nur Belichtung, Kontrast, ACES, Split-Toning, Sättigung/Vibrance, Vignette)."""
    # Belichtung wie Atmosphere.AutoExposureFor (Näherung: Tag 0,48, Sturm heller angepasst, Nacht 1,35) mal
    # Himmelsbelichtung (Atmosphere.SkyExposureDay 1,3 am Tag → 1,0 nachts)
    auto = min(0.48 * (1 + 0.6 * storm), 1.35)
    exposure = (auto + (1.35 - auto) * dark) * (1.3 + (1.0 - 1.3) * dark)
    contrast = P["Contrast"] + (1.05 - P["Contrast"]) * storm * 0.7
    satur = (P["Saturation"] + (1.08 - P["Saturation"]) * storm * 0.5) * (1 + (1.02 - 1) * dark)
    split = (0.18 + 0.04 * dark) * (1 - storm * 0.3)
    st = lin(mix(P["GradeShadow"], pal["Zenith"], dark * 0.5))
    ht = lin(mix(P["GradeHighlight"], pal["Sun"], dusk * 0.5))
    c = np.maximum(col * exposure, 0)
    c = 0.18 * np.power(c / 0.18 + 1e-5, contrast)
    c = np.clip(c * (2.51 * c + 0.03) / (c * (2.43 * c + 0.59) + 0.14), 0, 1)
    l = lum(c); hl = smoothstep(0.08, 0.75, l)[..., None]
    tS = 1 + (st / max(lum(st), 0.01) - 1) * split; tH = 1 + (ht / max(lum(ht), 0.01) - 1) * split
    c = c * (tS + (tH - tS) * hl)
    l = lum(c)[..., None]; mx = c.max(-1, keepdims=True); mn = c.min(-1, keepdims=True)
    s = satur * (1 + 0.45 * (1 - (mx - mn) / np.maximum(mx, 1e-4)))
    c = np.maximum(l + (c - l) * s, 0)
    H, W = c.shape[:2]; y, x = np.mgrid[0:H, 0:W]; u = x / W - 0.5; v = y / H - 0.5
    vg = np.clip((2 * (u * u + v * v)) ** 1.25 * 0.35, 0, 1)[..., None]
    vc = lin(mix(np.array([0.2, 0.18, 0.26], np.float32), P["GradeShadow"] * 0.5, 0.5))
    return c + (c * vc - c) * vg

def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "sky_preview"
    os.makedirs(out, exist_ok=True)
    pals = parse_palettes()
    shots = [("tag", 0.42, 0.0), ("daemmerung", 0.74, 0.0), ("nacht", 0.93, 0.0), ("sturm", 0.45, 1.0)]
    # Blickwinkel: Standard leicht nach oben; mit „--spielblick“ wie die Spielkamera (flach, Horizont im oberen Drittel)
    pitch = -6 if "--spielblick" in sys.argv else 8
    size = (480, 270) if "--klein" in sys.argv else (640, 360)
    tiles, tiles_post = [], []
    for planet in ("terra", "pyra", "pelagia", "nivalis"):
        row, row_post = [], []
        for name, phase, storm in shots:
            img, img_post = render(planet, pals[planet], phase, storm, size[0], size[1], pitch_deg=pitch)
            img.save(os.path.join(out, f"himmel_{planet}_{name}.png"))
            img_post.save(os.path.join(out, f"himmel_{planet}_{name}_post.png"))
            row.append(img); row_post.append(img_post)
            print(planet, name, "ok", flush=True)
        tiles.append(row); tiles_post.append(row_post)
    # Übersicht (roh und mit Nachbearbeitung)
    W, H = size
    for rows, fname, title in ((tiles, "himmel_uebersicht.png", "Himmels-Shader-Vorschau (NumPy-Nachrechnung, keine Spielszene)"),
                               (tiles_post, "himmel_uebersicht_post.png", "Himmel + Farbkorrektur der Nachbearbeitung (ACES, Split-Toning, Vibrance; ohne Bloom)")):
        sheet = Image.new("RGB", (W * 4, H * 4 + 40), (10, 10, 14))
        dr = ImageDraw.Draw(sheet)
        for r, row in enumerate(rows):
            for c, img in enumerate(row):
                sheet.paste(img, (c * W, 40 + r * H))
                dr.text((c * W + 10, 40 + r * H + 8), f"{['TERRA','PYRA','PELAGIA','NIVALIS'][r]} - {shots[c][0]}", fill=(255, 255, 255))
        dr.text((10, 12), title, fill=(255, 220, 160))
        sheet.save(os.path.join(out, fname))

if __name__ == "__main__":
    main()
