#!/usr/bin/env python3
"""Statische VORSCHAU des Spielmenü-Tablets (MIKOs Feldtablet) ohne Unity.

Rechnet die Aufteilung aus Runtime/UI/TabletMenu.cs (TabletLayout) für 1920x1080 nach und zeichnet Gehäuse,
Bildschirm, Statusleiste, App-Reiterleiste und einen Beispielinhalt (Reiter „Aufträge“) mit denselben Farben,
denselben App-Symbolen (Port von UISkinTablet.TabletShapeHit) und den Schriften aus Resources/Fonts
(Fließtext: Liberation Sans als Ersatz für Unitys Standardschrift). Kein Spielbild – nur eine Entwurfsansicht.

Aufruf: python3 Tools/TabletPreview/tablet_preview.py [ausgabe.png]
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONTS = os.path.join(ROOT, "RePlanet", "Assets", "RePlanet", "Resources", "Fonts")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "vorschau", "tablet_vorschau.png")

S = 2                      # Überabtastung
VW, VH = 1920, 1080


def hexc(v, a=1.0):
    return ((v >> 16) & 255, (v >> 8) & 255, v & 255, int(round(a * 255)))


TEXT, DIM, ACCENT, TEAL = hexc(0xEAF6F4), hexc(0xA3C7C3), hexc(0xFF8C2E), hexc(0x2EC4B6)
GOOD, WARN, BAD, STORY = hexc(0x6FE3A1), hexc(0xFFC15A), hexc(0xFF6A5C), hexc(0xC9A7FF)


def font(name, size):
    path = os.path.join(FONTS, name)
    if not os.path.exists(path):
        path = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
    return ImageFont.truetype(path, int(size * S))


def sans(size, bold=False):
    p = "/usr/share/fonts/truetype/liberation/LiberationSans-%s.ttf" % ("Bold" if bold else "Regular")
    if not os.path.exists(p):
        p = "/usr/share/fonts/truetype/dejavu/DejaVuSans%s.ttf" % ("-Bold" if bold else "")
    return ImageFont.truetype(p, int(size * S))


EXO_B = lambda s: font("Exo2-Bold.ttf", s)
EXO_M = lambda s: font("Exo2-Medium.ttf", s)

img = Image.new("RGBA", (VW * S, VH * S), (0, 0, 0, 255))


def layer():
    return Image.new("RGBA", img.size, (0, 0, 0, 0))


def comp(l):
    global img
    img = Image.alpha_composite(img, l)


def R(x, y, w, h):
    return [x * S, y * S, (x + w) * S, (y + h) * S]


def rrect(x, y, w, h, r, fill, outline=None, width=0):
    l = layer()
    ImageDraw.Draw(l).rounded_rectangle(R(x, y, w, h), radius=r * S, fill=fill, outline=outline, width=int(width * S))
    comp(l)


def rect(x, y, w, h, fill):
    l = layer()
    ImageDraw.Draw(l).rectangle(R(x, y, w, h), fill=fill)
    comp(l)


def vgrad_rrect(x, y, w, h, r, top, bottom, outline=None, width=0):
    W, H = int(w * S), int(h * S)
    t = np.linspace(0, 1, H)[:, None, None]
    a = np.array(top, float)[None, None, :]
    b = np.array(bottom, float)[None, None, :]
    g = (a * (1 - t) + b * t).repeat(W, axis=1).astype(np.uint8)
    gi = Image.fromarray(g, "RGBA")
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=r * S, fill=255)
    l = layer()
    gi.putalpha(Image.fromarray((np.array(gi)[:, :, 3] * (np.array(mask) / 255.0)).astype(np.uint8)))
    l.paste(gi, (int(x * S), int(y * S)), gi)
    comp(l)
    if outline:
        rrect(x, y, w, h, r, None, outline, width)


def text(x, y, s, f, col, anchor="lm"):
    l = layer()
    ImageDraw.Draw(l).text((x * S, y * S), s, font=f, fill=col, anchor=anchor)
    comp(l)


def text_w(s, f):
    return f.getlength(s) / S


def wrap(s, f, width):
    words, lines, cur = s.split(" "), [], ""
    for w_ in words:
        t = (cur + " " + w_).strip()
        if text_w(t, f) <= width:
            cur = t
        else:
            lines.append(cur)
            cur = w_
    if cur:
        lines.append(cur)
    return lines


def circle(cx, cy, r, fill):
    l = layer()
    ImageDraw.Draw(l).ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=fill)
    comp(l)


# ------------------------------------------------------------------ App-Symbole (Port von UISkin.ShapeHit/TabletShapeHit)
def shape_mask(shape, n):
    g = (np.arange(n) + 0.5) / n * 2 - 1
    u = g[None, :].repeat(n, 0)
    v = -g[:, None].repeat(n, 1)          # v nach oben
    au, av, r = abs(u), abs(v), np.sqrt(u * u + v * v)
    if shape == "bag":
        body = (au < .62) & (v > -.85) & (v < .36) & ~((au > .42) & (v < -.65) & ((au - .42) ** 2 + (v + .65) ** 2 > .04))
        seam = (au < .5) & (abs(v - .02) < .05)
        pin_ = (au < .27) & (v > -.59) & (v < -.27)
        pout = (au < .34) & (v > -.66) & (v < -.2)
        hr = np.sqrt(u * u + (v - .36) ** 2)
        handle = (v >= .3) & (hr < .38) & (hr > .22)
        return (body & ~seam & ~(pout & ~pin_)) | handle
    if shape == "list":
        board = (au < .66) & (av < .86) & ~((au < .5) & (v > -.72) & (v < .64))
        clip = (au < .26) & (v > .62) & (v < .95)
        lines = np.zeros_like(u, bool)
        for i in range(3):
            ly = .34 - i * .36
            lines |= (abs(v - ly) < .065) & (u > -.12) & (u < .36)
            lines |= np.sqrt((u + .29) ** 2 + (v - ly) ** 2) < .1
        return board | clip | lines
    if shape == "pin":
        cr = np.sqrt(u * u + (v - .28) ** 2)
        return ((cr < .52) & (cr > .22)) | ((v < .28) & (v > -.9) & (au < (v + .9) * .44) & (cr > .22))
    if shape == "wrench":
        return ((au < .14) & (av < .75)) | ((np.sqrt(u * u + (v - .6) ** 2) < .34) & ~((au < .12) & (v > .5)))
    if shape == "crate":
        box = (au < .8) & (av < .66)
        inner = (au < .64) & (av < .5)
        return (box & ~inner) | (inner & (abs(u * .78 - v) < .1))
    if shape == "book":
        return (au < .8) & (av < .62) & ~(au < .06) & ~((av < .5) & (au > .12) & (au < .7) & (np.mod(v * 5, 1) < .3))
    if shape == "trophy":
        bowl = (v > -.05) & (v < .82) & (au < .24 + .34 * np.sqrt(np.clip((v + .05) / .87, 0, 1)))
        hd = np.sqrt((au - .56) ** 2 + (v - .45) ** 2)
        return bowl | ((au > .5) & (hd < .24) & (hd > .12)) | ((au < .09) & (v > -.5) & (v <= -.05)) | ((au < .42) & (v > -.82) & (v < -.52))
    if shape == "robot":
        corner = (au > .5) & (av > .28) & (np.sqrt((au - .5) ** 2 + (av - .28) ** 2) > .16)
        head = (au < .66) & (v > -.42) & (v < .42) & ~corner
        eye = (au - .28) ** 2 + (v - .02) ** 2 < .15 ** 2
        mouth = (au < .2) & (abs(v + .25) < .045)
        return (head & ~eye & ~mouth) | ((au < .055) & (v >= .42) & (v < .7)) | (np.sqrt(u * u + (v - .78) ** 2) < .12) \
            | ((au > .66) & (au < .8) & (av < .18)) | ((au < .34) & (v < -.5) & (v > -.86))
    if shape == "radio":
        body = (au < .82) & (v > -.72) & (v < .3)
        inn = (au < .68) & (v > -.58) & (v < .16)
        sd = np.sqrt((u + .3) ** 2 + (v + .21) ** 2)
        spk = (sd < .25) & ((sd > .16) | (sd < .07))
        dial = (u > .14) & (u < .56) & ((abs(v) < .045) | (abs(v + .2) < .045) | (abs(v + .4) < .045))
        ax, ay = u - .35, v - .3
        along = (ax * .37 + ay * .6) / .4933
        across = abs(ax * .6 - ay * .37) / .705
        return (body & ~inn) | spk | dial | ((along > 0) & (along < .7) & (across < .05))
    if shape == "people":
        h1 = np.sqrt((u + .3) ** 2 + (v - .32) ** 2) < .22
        b1 = np.sqrt((u + .3) ** 2 + (v + .52) ** 2)
        s1 = (v > -.6) & (v < -.02) & (b1 < .46)
        h2 = np.sqrt((u - .34) ** 2 + (v - .2) ** 2) < .19
        b2 = np.sqrt((u - .34) ** 2 + (v + .6) ** 2)
        s2 = (v > -.68) & (v < -.12) & (b2 < .42) & (u > .02)
        return h1 | s1 | h2 | (s2 & ~((b1 < .54) & (u < .16)))
    if shape == "coin":
        return (r < .82) & ~((r < .6) & (r > .48))
    if shape == "signal":
        m = np.zeros_like(u, bool)
        for i in range(4):
            x0 = -.84 + i * .44
            m |= (u > x0) & (u < x0 + .3) & (v > -.8) & (v < -.8 + .4 * (i + 1))
        return m
    if shape == "sun":
        ang = np.arctan2(v, u)
        return (r < .42) | ((r < .85) & (r > .55) & (np.cos(8 * ang) > .6))
    if shape == "cross":
        return ((abs(u - v) < .25) | (abs(u + v) < .25)) & (au < .8) & (av < .8)
    if shape == "flag":
        return ((u > -.7) & (u < -.55) & (av < .85)) | ((u >= -.55) & (u < .7) & (v > .1) & (v < .8))
    if shape == "circle":
        return (r < .82) & (r > .5)
    if shape == "hexagon":
        hx = np.maximum(au * .866 + av * .5, av)
        return (hx < .82) & (hx > .52)
    if shape == "triangle":
        outer = (v > -.7) & (au < (.85 - v) * .55) & (v < .85)
        inner = (v > -.42) & (au < (.45 - v) * .55) & (v < .45)
        return outer & ~inner
    if shape == "ring":
        return ((r < .85) & (r > .62)) | (r < .3)
    return r < .8


def icon(shape, x, y, size, col):
    n = max(8, int(size * S))
    m = shape_mask(shape, n * 4).reshape(n, 4, n, 4).mean(axis=(1, 3))
    a = (m * col[3]).astype(np.uint8)
    im = Image.new("RGBA", (n, n), col[:3] + (0,))
    im.putalpha(Image.fromarray(a))
    l = layer()
    l.paste(im, (int(x * S), int(y * S)), im)
    comp(l)


# ------------------------------------------------------------------ Hintergrund: abgedunkelte Spielwelt (angedeutet)
bg = np.zeros((VH * S, VW * S, 4), np.uint8)
yy = np.linspace(0, 1, VH * S)[:, None]
sky = np.array([40, 92, 104]) * (1 - yy) + np.array([120, 110, 80]) * yy
bg[:, :, :3] = np.broadcast_to(np.clip(sky, 0, 255)[:, None, :], (VH * S, VW * S, 3)).astype(np.uint8)
bg[:, :, 3] = 255
img = Image.fromarray(bg, "RGBA")
d = ImageDraw.Draw(img)
for i in range(14):                                   # Silhouetten (Stadt, Hügel)
    x = (i * 157) % VW
    hgt = 120 + (i * 73) % 220
    d.rectangle([x * S, (VH * 0.55 - hgt) * S, (x + 90) * S, VH * S], fill=(30, 48, 52, 255))
d.rectangle([0, int(VH * 0.62 * S), VW * S, VH * S], fill=(58, 70, 54, 255))
img = img.filter(ImageFilter.GaussianBlur(10 * S))
rect(0, 0, VW, VH, (3, 10, 13, int(0.55 * 255)))      # Dim(0.55)

# ------------------------------------------------------------------ Aufteilung wie TabletLayout()
m = min(max(VH * 0.02, 8), 24)
B = min(max(VH * 0.034, 20), 40)
SIDE = B * 1.2
h = VH - 2 * m
w = min(VW - 2 * m, h * 2.2)
OX, OY, OW, OH = (VW - w) / 2, m, w, h
SX, SY, SW, SH = OX + SIDE, OY + B, OW - 2 * SIDE, OH - 2 * B
pad = min(max(B * 0.5, 12), 18)
sh_ = min(max(VH * 0.03, 26), 32)
ST = (SX + pad, SY + 6, SW - 2 * pad, sh_)
th = min(max(VH * 0.064, 52), 70)
TB = (SX + pad, ST[1] + ST[3] + 6, SW - 2 * pad, th)
cy0 = TB[1] + TB[3] + 14
CT = (SX + pad + 6, cy0, SW - 2 * pad - 12, SY + SH - cy0 - pad)

# ------------------------------------------------------------------ Gehäuse
sh_l = layer()
ImageDraw.Draw(sh_l).rounded_rectangle(R(OX - 4, OY + 10, OW + 8, OH + 8), radius=30 * S, fill=(0, 0, 0, 170))
comp(sh_l.filter(ImageFilter.GaussianBlur(14 * S)))
vgrad_rrect(OX, OY, OW, OH, 22, hexc(0x1C525C), hexc(0x0B2A31), hexc(0x3FD6C8, 0.55), 2)
rrect(OX + 24, OY + 3, OW - 48, 1.5, 1, (255, 255, 255, 30))
bump = (205, 112, 40, 255)
k, ln = B * 0.55, B * 3.0
for c in range(4):
    right, bottom = c & 1, c >= 2
    x0 = OX + OW - ln + 3 if right else OX - 3
    y0 = OY + OH - k + 3 if bottom else OY - 3
    xv = OX + OW - k + 3 if right else OX - 3
    yv = OY + OH - ln + 3 if bottom else OY - 3
    rrect(x0, y0, ln, k, 7, bump)
    rrect(xv, yv, k, ln, 7, bump)
    rect(x0 + 8, y0 + 2, ln - 16, 1.5, (255, 190, 140, 120))
gy = OY + OH / 2 - B * 1.2
for i in range(6):
    yy_ = gy + i * B * 0.48
    for gx in (OX + SIDE * 0.28, OX + OW - SIDE * 0.72):
        rect(gx, yy_, SIDE * 0.44, 2, (0, 0, 0, 90))
        rect(gx, yy_ + 2, SIDE * 0.44, 1, (255, 255, 255, 18))
ss = max(9, B * 0.36)
for sx_, sy_ in ((OX + SIDE * .8, OY + B * .75), (OX + OW - SIDE * .8, OY + B * .75), (OX + SIDE * .8, OY + OH - B * .75), (OX + OW - SIDE * .8, OY + OH - B * .75)):
    circle(sx_, sy_, ss / 2, (95, 108, 110, 255))
    circle(sx_ - 0.6, sy_ - 0.6, ss / 2 - 2, (150, 168, 170, 255))
    l = layer(); dd = ImageDraw.Draw(l)
    a = 0.35
    for ang in (a, a + math.pi / 2):
        dx, dy = math.cos(ang) * ss * 0.3, math.sin(ang) * ss * 0.3
        dd.line([(sx_ - dx) * S, (sy_ - dy) * S, (sx_ + dx) * S, (sy_ + dy) * S], fill=(30, 40, 42, 255), width=int(2.4 * S))
    comp(l)
cs = max(8, B * 0.36); ccx, ccy = OX + OW / 2, OY + B / 2
circle(ccx, ccy, cs * 0.8, (0, 0, 0, 115)); circle(ccx, ccy, cs / 2, (5, 13, 18, 255))
l = layer(); ImageDraw.Draw(l).ellipse([(ccx - cs / 2) * S, (ccy - cs / 2) * S, (ccx + cs / 2) * S, (ccy + cs / 2) * S], outline=TEAL[:3] + (115,), width=int(1.5 * S)); comp(l)
circle(ccx - cs * 0.12, ccy - cs * 0.18, cs * 0.1, (255, 255, 255, 140))
lx, ly, ls = OX + OW - SIDE - 12, OY + B / 2, max(6, B * 0.2)
for i, lc in enumerate((GOOD, (77, 102, 107, 255), GOOD)):
    x = lx - i * ls * 2.6
    if i != 1:
        circle(x, ly, ls * 1.3, lc[:3] + (56,))
    circle(x, ly, ls / 2, lc)
for i in range(7):
    rrect(OX + B * 3 + 12 + i * 7, OY + OH - B / 2 - B * .18, 3, B * .36, 1.5, (0, 0, 0, 100))
mark = "MIKO · FELDTABLET"
f_mark = EXO_B(11)
text(OX + OW / 2, OY + OH - B / 2, mark, f_mark, DIM[:3] + (102,), "mm")

# ------------------------------------------------------------------ Bildschirm
vgrad_rrect(SX, SY, SW, SH, 13, hexc(0x0A2A31, 0.97), hexc(0x03100F, 0.98), (0, 0, 0, 230), 2.5)
glow = layer()
ImageDraw.Draw(glow).rounded_rectangle(R(SX + 3, SY + 3, SW - 6, SH - 6), radius=12 * S, outline=TEAL[:3] + (70,), width=int(18 * S))
comp(glow.filter(ImageFilter.GaussianBlur(18 * S)))

# Statusleiste
rrect(*ST, 9, (0, 0, 0, 51))
scy = ST[1] + ST[3] / 2
x = ST[0] + 10
icon("robot", x, scy - 9, 18, TEAL); x += 24
f_sb, f_st = EXO_B(15), EXO_M(15)
text(x, scy, "MIKO-OS", f_sb, TEXT); x += text_w("MIKO-OS", f_sb) + 14
circle(x + 5, scy, 5, hexc(0x3FA7D6)); x += 16
text(x, scy, "PELAGIA", f_sb, hexc(0x3FA7D6))
clock = "17:24"
cw = text_w(clock, f_sb)
cx = ST[0] + ST[2] / 2 - (cw + 24) / 2
icon("sun", cx, scy - 9, 18, WARN)
text(cx + 24, scy, clock, f_sb, TEXT)
xr = ST[0] + ST[2] - 10
bx = xr - 29
rrect(bx, scy - 6.5, 26, 13, 3, TEXT[:3] + (204,)); rrect(bx + 1.5, scy - 5, 23, 10, 2, (5, 20, 25, 255))
rect(bx + 3, scy - 3.5, 20 * 0.84, 7, GOOD); rect(bx + 26, scy - 3, 3, 6, TEXT[:3] + (204,))
t_ = "84 %"; xr = bx - 6 - text_w(t_, f_sb); text(xr, scy, t_, f_sb, TEXT); xr -= 22
t_ = "1.295 Cr"; xr -= text_w(t_, f_sb); text(xr, scy, t_, f_sb, TEXT); icon("coin", xr - 24, scy - 9, 18, WARN); xr -= 24 + 22
t_ = "Solo"; xr -= text_w(t_, f_st); text(xr, scy, t_, f_st, DIM); icon("signal", xr - 24, scy - 9, 18, DIM[:3] + (128,))

# Reiterleiste
f_key = sans(14, True)
def keycap(x, y, s):
    wk = max(26, text_w(s, f_key) + 10)
    rrect(x, y, wk, 26, 5, (234, 246, 244, 235))
    text(x + wk / 2, y + 13, s, f_key, (11, 36, 41, 255), "mm")
    return wk
kw = 34
closeW = min(max(TB[3] * 1.5, 78), 104)
bar = (TB[0] + kw + 8, TB[1], TB[2] - 2 * (kw + 8) - closeW - 10, TB[3])
keycap(TB[0] + (kw - 26) / 2, TB[1] + TB[3] / 2 - 13, "Q")
keycap(bar[0] + bar[2] + 8 + (kw - 26) / 2, TB[1] + TB[3] / 2 - 13, "E")
rrect(*bar, 10, (0, 0, 0, 66))
tabs = [("bag", "Inventar"), ("list", "Aufträge"), ("pin", "Karte"), ("wrench", "Werkstatt"), ("crate", "Lager"),
        ("book", "Archiv"), ("trophy", "Erfolge"), ("robot", "Roboter"), ("radio", "Radio"), ("people", "Koop")]
cur = 1
n = len(tabs); gap = 4
tw = (bar[2] - 8 - gap * (n - 1)) / n
tht = bar[3] - 8
f_tab = EXO_B(15)
for i, (ic, name) in enumerate(tabs):
    tx, ty = bar[0] + 4 + i * (tw + gap), bar[1] + 4
    sel = i == cur
    if sel:
        rrect(tx, ty, tw, tht, 9, hexc(0x5A3514, 0.95), ACCENT, 2)
        rrect(tx + tw / 2 - tw * .2, ty + tht - 3.5, tw * .4, 2.5, 1.2, ACCENT)
    if i == 4:  # Maus darüber
        rrect(tx, ty, tw, tht, 9, (255, 255, 255, 18))
    isz = min(tht * 0.44, 28)
    iy = ty + tht * 0.1
    icon(ic, tx + tw / 2 - isz / 2, iy, isz, ACCENT if sel else (TEXT if i == 4 else DIM))
    text(tx + tw / 2, iy + isz + (tht - (iy - ty) - isz - 6) / 2, name, f_tab, TEXT if sel else DIM, "mm")
crx = TB[0] + TB[2] - closeW
rrect(crx, TB[1] + 4, closeW, TB[3] - 8, 9, (0, 0, 0, 66))
cis = min((TB[3] - 8) * 0.36, 20)
icon("cross", crx + closeW / 2 - cis / 2, TB[1] + 4 + (TB[3] - 8) * .16, cis, DIM)
text(crx + closeW / 2, TB[1] + 4 + (TB[3] - 8) * .16 + cis + 1 + ((TB[3] - 8) * .84 - cis - 2) / 2, "Zurück", f_tab, DIM, "mm")

# ------------------------------------------------------------------ Inhalt: Reiter „Aufträge“
f_l, f_lb, f_s, f_t = sans(20), sans(20, True), sans(17), sans(14)
f_sec = EXO_B(20)
cx0, cy_, cwid, chgt = CT
lw = cwid * 0.52
left_w, right_x, right_w = lw - 16 - 20, cx0 + lw + 8, cwid - lw - 8 - 20

def card(x, y, w_, h_, hi=False):
    if hi:
        rrect(x, y, w_, h_, 9, hexc(0x173F47, 0.88), hexc(0xFF8C2E, 0.8), 1.6)
    else:
        rrect(x, y, w_, h_, 9, hexc(0x0F3139, 0.8), hexc(0x2EC4B6, 0.2), 1.2)

def section(title, x, w_, y):
    rrect(x, y + 8, 4, 17, 2, ACCENT)
    text(x + 12, y + 16, title, f_sec, TEXT)
    rect(x, y + 33, w_, 1, TEAL[:3] + (71,))
    return y + 40

def scrollbar(x, y, h_, frac):
    rrect(x, y, 8, h_, 4, (255, 255, 255, 20)); rrect(x, y, 8, h_ * frac, 4, (255, 255, 255, 70))

# links: Ziel + Aufträge
y = cy_
obj = "Bringe deine Ladung zum Stützpunkt und verkaufe sie am Verkaufsterminal."
oh = 34 * len(wrap(obj, f_l, left_w - 40)) + 50
card(cx0, y, left_w, oh, True)
rrect(cx0, y, left_w, oh, 9, ACCENT[:3] + (26,))
icon("flag", cx0 + 16, y + 12, 20, ACCENT)
text(cx0 + 44, y + 22, "AKTUELLES ZIEL", EXO_B(15), ACCENT)
for j, line in enumerate(wrap(obj, f_l, left_w - 32)):
    text(cx0 + 16, y + 52 + j * 26, line, f_l, TEXT)
y += oh + 16
y = section("Aktive Aufträge", cx0, left_w, y)
missions = [("Erster Verkauf", "Einstieg · TERRA", "Bringe deine Ladung zum Stützpunkt und verkaufe sie am Verkaufsterminal.", None, "Belohnung: 15 Credits"),
            ("Kupferfieber", "Nebenauftrag · PYRA", "Sammle 60 Einheiten Kupfer.", (0, 60), "Belohnung: 300 Credits"),
            ("Ballen für den Markt", "Nebenauftrag · PYRA", "Verkaufe 10 Ballen.", (3, 10), "Belohnung: 400 Credits"),
            ("Das Band läuft", "Nebenauftrag · PYRA", "Repariere 6 Förderknoten.", (2, 6), "Belohnung: 450 Credits + Kosmetik „Zahnrad“"),
            ("Gefahrgut sichern", "Nebenauftrag · PYRA", "Entsorge 30 Einheiten Gefahrstoffe.", (0, 30), "Belohnung: 500 Credits"),
            ("Schwarzes Wasser", "Nebenauftrag · PELAGIA", "Beseitige 8 Ölteppiche.", (1, 8), "Belohnung: 700 Credits")]
for title, tag, desc, prog, reward in missions:
    hh = 36 + 22 + (24 if prog else 0) + 30
    if y + hh > cy_ + chgt:
        break
    card(cx0, y, left_w, hh)
    rrect(cx0 + 3, y + 6, 5, hh - 12, 2.5, ACCENT)
    text(cx0 + 14, y + 20, title, f_lb, TEXT)
    text(cx0 + 22 + text_w(title, f_lb), y + 20, "· " + tag, f_l, DIM)
    text(cx0 + 14, y + 46, desc, f_s, TEXT)
    yy_ = y + 58
    if prog:
        rrect(cx0 + 14, yy_ + 6, left_w - 140, 10, 5, (8, 32, 38, 217))
        if prog[0]:
            rrect(cx0 + 14, yy_ + 6, (left_w - 140) * prog[0] / prog[1], 10, 5, ACCENT)
        text(cx0 + left_w - 118, yy_ + 11, "%d/%d" % prog, f_t, DIM)
        yy_ += 24
    text(cx0 + 14, yy_ + 13, reward, f_t, WARN)
    y += hh + 8
scrollbar(cx0 + left_w + 6, cy_, chgt, 0.42)

# rechts: Wiederherstellung + Großprojekte
y = cy_
y = section("Wiederherstellung PELAGIA – 1 %", right_x, right_w, y)
for name, st, cl in (("Hafen", 1, 4), ("Küstensiedlung", 0, 0), ("Lagune", 0, 0)):
    text(right_x, y + 13, name, f_lb, TEXT)
    text(right_x + right_w * 0.4, y + 13, "Stufe %d/4 · %s" % (st, "Zugang frei" if st else "Zugang versperrt"), f_s, DIM)
    y += 28
    for s_ in range(4):
        rrect(right_x + s_ * right_w / 4, y, right_w / 4 - 6, 8, 4, TEAL if st > s_ else (255, 255, 255, 31))
    y += 14
    text(right_x, y + 12, "Sauberkeit %d %% (Ziel 85 %%) · Ökologie 0 %%" % cl, f_t, DIM)
    y += 30
y += 6
y = section("Großprojekte", right_x, right_w, y)
text(right_x, y + 15, "TERRA", f_lb, hexc(0x8FD14F)); text(right_x + text_w("TERRA ", f_lb) + 6, y + 15, "Die vergessene Erde", f_lb, DIM)
y += 32
projects = [("Licht für das Wohnviertel", False, "Straßenbeleuchtung und Buslinie 7 wieder in Betrieb nehmen.", "250 Cr", [("circle", hexc(0x7FD6C9), "0/30"), ("hexagon", hexc(0x9AA5B1), "0/25")], "Erst den Hauptmüll entfernen: 0 % von 85 %."),
            ("Wasserkreislauf reaktivieren", False, "Pumpen, Brunnen und Leitungen des Einkaufszentrums reparieren.", "700 Cr", [("circle", hexc(0x7FD6C9), "0/50"), ("triangle", hexc(0xF2C14E), "0/40")], "Zuerst „Licht für das Wohnviertel“ abschließen."),
            ("Zentrales Gewächshaus", True, "Das große Gewächshaus mit den versiegelten Samen wiederaufbauen.", "1.600 Cr", [("circle", hexc(0x7FD6C9), "0/70"), ("hexagon", hexc(0x9AA5B1), "0/60"), ("triangle", hexc(0xF2C14E), "0/30")], "Zuerst „Wasserkreislauf reaktivieren“ abschließen.")]
for name, great, desc, cr, mats, why in projects:
    hh = 34 + 22 + 32 + 26 + 8
    if y + hh > cy_ + chgt:
        break
    card(right_x, y, right_w, hh)
    text(right_x + 14, y + 19, name, f_lb, TEXT)
    xx = right_x + 22 + text_w(name, f_lb)
    if great:
        text(xx, y + 19, "GROSSPROJEKT", f_l, STORY); xx += text_w("GROSSPROJEKT ", f_l) + 8
    text(xx, y + 19, "offen", f_l, DIM)
    text(right_x + 14, y + 44, desc, f_s, TEXT)
    yy_ = y + 33 + 22 + 4
    text(right_x + 14, yy_ + 13, cr, f_t, BAD)
    mx = right_x + 14 + text_w(cr, f_t) + 16
    for shp, col, t_ in mats:
        icon(shp, mx, yy_ + 4, 18, col)
        text(mx + 22, yy_ + 13, t_, f_t, BAD)
        mx += 22 + text_w(t_, f_t) + 14
    text(right_x + 14, yy_ + 30 + 12, why, f_t, WARN)
    y += hh + 6
scrollbar(right_x + right_w + 6, cy_, chgt, 0.3)

# ------------------------------------------------------------------ Bildschirm-Überzug: Scanlinien, Rauschen, Glas
ov = np.zeros((int(SH * S), int(SW * S), 4), np.uint8)
rows = (np.arange(ov.shape[0]) / S) % 4
prof = np.interp(rows, [0, 1, 2, 3, 4], [0.15, 0.6, 1.0, 0.6, 0.15])
ov[:, :, 3] = (prof[:, None] * 0.05 * 255).astype(np.uint8)
ovi = Image.fromarray(ov, "RGBA")
l = layer(); l.paste(ovi, (int(SX * S), int(SY * S)), ovi); comp(l)
rng = np.random.default_rng(7)
nz = rng.random((int(SH / 1.5) + 1, int(SW / 1.5) + 1))
nzi = Image.fromarray((nz * 255).astype(np.uint8)).resize((int(SW * S), int(SH * S)), Image.NEAREST)
noise = Image.new("RGBA", nzi.size, (255, 255, 255, 0)); noise.putalpha(nzi.point(lambda p: int(p * 0.022)))
l = layer(); l.paste(noise, (int(SX * S), int(SY * S)), noise); comp(l)
gw, gh = int(SW * 0.78 * S), int(SH * 0.85 * S)
uu = np.linspace(0, 1, gw)[None, :]; vv = np.linspace(1, 0, gh)[:, None]
dd_ = (uu + (1 - vv)) * 0.5
ga = np.exp(-((dd_ - .27) / .07) ** 2) * .75 + np.exp(-((dd_ - .37) / .025) ** 2) * .35 + (1 - np.clip(dd_ / .3, 0, 1) ** 2 * (3 - 2 * np.clip(dd_ / .3, 0, 1))) * .45
glass = Image.new("RGBA", (gw, gh), (255, 255, 255, 0)); glass.putalpha(Image.fromarray((np.clip(ga, 0, 1) * 0.05 * 255).astype(np.uint8)))
l = layer(); l.paste(glass, (int((SX + 3) * S), int((SY + 3) * S)), glass); comp(l)

# ------------------------------------------------------------------ Kennzeichnung als Vorschau
f_pv = sans(15, True)
lbl = "VORSCHAU (Python/PIL, kein Spielbild) – Spielmenü als MIKOs Feldtablet, 1920×1080"
rrect(VW / 2 - text_w(lbl, f_pv) / 2 - 12, 2, text_w(lbl, f_pv) + 24, 17, 4, (0, 0, 0, 170))
text(VW / 2, 10.5, lbl, f_pv, WARN, "mm")

out = img.resize((VW, VH), Image.LANCZOS).convert("RGB")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
out.save(OUT, optimize=True)
print("geschrieben:", OUT)
