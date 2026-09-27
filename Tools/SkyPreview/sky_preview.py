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
    col = mix(Ho, Z, np.power(up, 0.5)[..., None])
    col = np.where((h < 0)[..., None], mix(Ho, G, sat(-h * 5)[..., None]), col)
    neb = fbm5(d * 2.3 + np.array([0, t * 0.003, 0], np.float32))
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
    aur = P["Aurora"] * dark * (1 - storm)
    if aur > 0.001:
        wave = fbm3(np.stack([d[..., 0] * 2.2, d[..., 2] * 2.2, np.full_like(h, t * 0.04)], -1)) * 3
        center = 0.28 + 0.08 * np.sin(d[..., 0] * 3 + wave + t * 0.15)
        curtain = np.where(h < center, np.exp(-((h - center) * 16) ** 2), np.exp(-((h - center) * 3.5) ** 2))
        rays = (0.5 + 0.5 * np.sin(d[..., 0] * 70 + d[..., 2] * 31 + wave * 7 + t * 0.6)) ** 3
        rays = rays * 0.75 + 0.25 * fbm3(np.stack([d[..., 0] * 20, h * 3, np.full_like(h, t * 0.1)], -1))
        ac = mix(lin(np.array([0.2, 1, 0.6], np.float32)), lin(np.array([0.6, 0.3, 1], np.float32)), sat((h - center) * 4)[..., None])
        col = col + ac * (curtain * rays * aur * 0.9 * (h > 0))[..., None]
    vis = (0.55 + 0.45 * dark) * (1 - storm * 0.85)
    col = body(d, col, P["P1"], sun, vis)
    col = body(d, col, P["P2"], sun, vis)
    sd = d @ sun; cosS = math.cos(0.03 + dusk * 0.015)
    disc = smoothstep(cosS, cosS + (1 - cosS) * 0.25, sd)
    glow = sat(sd) ** 10 * 0.45 + sat(sd) ** 90 * 0.8
    sunI = 3 * (1 - storm * 0.8) if elev > -0.05 else 0
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
    dens = sat((c - (1 - pal["Cover"]) * 0.85) * (P["CloudDensity"] + storm * 1.5)) * sat(h * 8 + 0.08)
    toSun = np.array([sun[0], 0, sun[2]], np.float32); toSun /= np.linalg.norm(toSun) + 1e-4
    cs = cloudField(p + toSun * 0.35)
    lightAmt = sat(0.55 + (c - cs) * 3.2)
    towards = sat(sd * 0.5 + 0.5) ** 3
    shade = sat(c * 1.3 - 0.2)
    cc = mix(lin(pal["CloudShadow"]), lin(pal["CloudLight"]), sat(lightAmt * 0.75 + shade * 0.35)[..., None])
    sunC = lin(pal["Sun"]) * (1 - dark * 0.9)
    cc = cc + sunC * (towards * 0.3 * lightAmt)[..., None] + sunC * (sat(1 - dens) ** 2 * towards * 0.45)[..., None]
    cm = h > -0.02
    col = np.where(cm[..., None], mix(col, cc, (dens * 0.96)[..., None]), col)
    haze = np.exp(-np.abs(h) * 9) * pal["HazeStrength"]
    col = mix(col, Hz, sat(haze)[..., None])
    # Vordergrund: Hügelsilhouette im Nebel (nur zur Einordnung)
    xs_ang = np.arctan2(d[..., 0], d[..., 2])
    ridge = -0.02 + 0.035 * np.sin(xs_ang * 5 + 1) + 0.02 * np.sin(xs_ang * 13 + 2) + 0.01 * np.sin(xs_ang * 31)
    fog = lin(mix(pal["Fog"], pal["Haze"], 0.35))
    land = mix(G * 0.6, fog, 0.55)
    col = np.where((h < ridge)[..., None], land, col)
    img = (srgb(col) * 255).clip(0, 255).astype(np.uint8)
    return Image.fromarray(img, "RGB")

def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "sky_preview"
    os.makedirs(out, exist_ok=True)
    pals = parse_palettes()
    shots = [("tag", 0.42, 0.0), ("daemmerung", 0.74, 0.0), ("nacht", 0.93, 0.0), ("sturm", 0.45, 1.0)]
    tiles = []
    for planet in ("terra", "pyra", "pelagia", "nivalis"):
        row = []
        for name, phase, storm in shots:
            img = render(planet, pals[planet], phase, storm, 640, 360)
            img.save(os.path.join(out, f"himmel_{planet}_{name}.png"))
            row.append(img)
            print(planet, name, "ok", flush=True)
        tiles.append(row)
    # Übersicht
    W, H = 640, 360
    sheet = Image.new("RGB", (W * 4, H * 4 + 40), (10, 10, 14))
    dr = ImageDraw.Draw(sheet)
    for r, row in enumerate(tiles):
        for c, img in enumerate(row):
            sheet.paste(img, (c * W, 40 + r * H))
            dr.text((c * W + 10, 40 + r * H + 8), f"{['TERRA','PYRA','PELAGIA','NIVALIS'][r]} – {shots[c][0]}", fill=(255, 255, 255))
    dr.text((10, 12), "Himmels-Shader-Vorschau (NumPy-Nachrechnung, keine Spielszene)", fill=(255, 220, 160))
    sheet.save(os.path.join(out, "himmel_uebersicht.png"))

if __name__ == "__main__":
    main()
