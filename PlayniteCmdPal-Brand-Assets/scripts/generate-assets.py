#!/usr/bin/env python3
"""
PlayniteCmdPal brand asset generator.

Authoritative artwork is defined here as vector geometry (symbol) and as outlined
Inter SemiBold glyphs (wordmark). Everything else (SVG, PNG, ICO, previews, docs,
ZIP) is derived from it.

Requirements (pip):  cairosvg  Pillow  fonttools  uharfbuzz
Usage:               python scripts/generate-assets.py [--no-zip]
"""
import io
import math
import random
import struct
import sys
import zipfile
from pathlib import Path

import cairosvg
import uharfbuzz as hb
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent.parent
SVG_DIR = ROOT / "svg"
PNG_DIR = ROOT / "icons" / "png"
MSIX_DIR = PNG_DIR / "msix"
UNPLATED_DIR = PNG_DIR / "glyph"
ICO_DIR = ROOT / "icons" / "ico"
PRE_DIR = ROOT / "previews"
BRAND_DIR = ROOT / "brand"
FONT_DIR = ROOT / "fonts"
LIC_DIR = ROOT / "LICENSES"

# --------------------------------------------------------------------------- palette
COBALT = "#2F6BFF"        # Launch Cobalt
COBALT_DEEP = "#1E4FD6"   # Cobalt Deep
COBALT_LIGHT = "#5B8CFF"  # Cobalt Light (dark backgrounds)
TINT = "#B5CBFF"          # Index Tint
GRAPHITE = "#14181F"
SLATE = "#5B6475"
MIST = "#F3F5F9"
WHITE = "#FFFFFF"

PALETTE = [
    ("Launch Cobalt", COBALT, "Primary brand colour. Symbol plate on light backgrounds."),
    ("Cobalt Deep", COBALT_DEEP, "Pressed/hover state, text-safe accent on light backgrounds."),
    ("Cobalt Light", COBALT_LIGHT, "Symbol plate and accent text on dark backgrounds."),
    ("Index Tint", TINT, "Secondary grid cells inside the symbol, subtle fills."),
    ("Graphite", GRAPHITE, "Primary text on light, dark backgrounds, wide-tile background."),
    ("Slate", SLATE, "Secondary text, captions, guides."),
    ("Mist", MIST, "Light surface, page background."),
    ("White", WHITE, "Glyph colour on plate, text on dark."),
]

THEMES = {
    # tile, chevron, selected cell, other cells
    "light": dict(tile=COBALT, chev=WHITE, sel=WHITE, cell=TINT, text=GRAPHITE, accent=COBALT),
    "dark": dict(tile=COBALT_LIGHT, chev=WHITE, sel=WHITE, cell="#CFDDFF", text=WHITE, accent=COBALT_LIGHT),
}

# --------------------------------------------------------------------------- helpers
def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def lum(h):
    def f(c):
        c /= 255
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = hex_rgb(h)
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)


def contrast(a, b):
    la, lb = lum(a), lum(b)
    if la < lb:
        la, lb = lb, la
    return (la + 0.05) / (lb + 0.05)


def fmt(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


# --------------------------------------------------------------------------- symbol geometry
# Authoritative symbol: 256 x 256 canvas.
#   full  : plate 16..240 (r56), chevron prompt + 2x2 library grid (one cell = selected result)
#   small : plate 8..248  (r56), heavier chevron + single block cursor (16-32 px optical variant)
def symbol_parts(c, small=False, plate=True):
    out = []
    if small:
        if plate:
            out.append(f'<rect x="8" y="8" width="240" height="240" rx="56" fill="{c["tile"]}"/>')
        out.append(
            f'<g transform="translate(7 0)">'
            f'<path d="M62 74 L112 128 L62 182" fill="none" stroke="{c["chev"]}" stroke-width="38" '
            f'stroke-linecap="round" stroke-linejoin="round"/>'
            f'<rect x="146" y="90" width="56" height="76" rx="14" fill="{c["sel"]}"/>'
            f"</g>"
        )
    else:
        if plate:
            out.append(f'<rect x="16" y="16" width="224" height="224" rx="56" fill="{c["tile"]}"/>')
        out.append(
            f'<g transform="translate(7 0)">'
            f'<path d="M58 92 L96 128 L58 164" fill="none" stroke="{c["chev"]}" stroke-width="24" '
            f'stroke-linecap="round" stroke-linejoin="round"/>'
            f'<rect x="120" y="90" width="32" height="32" rx="8" fill="{c["cell"]}"/>'
            f'<rect x="164" y="90" width="32" height="32" rx="8" fill="{c["sel"]}"/>'
            f'<rect x="120" y="134" width="32" height="32" rx="8" fill="{c["cell"]}"/>'
            f'<rect x="164" y="134" width="32" height="32" rx="8" fill="{c["cell"]}"/>'
            f"</g>"
        )
    return "".join(out)


def _rr(x, y, w, h, r):
    f = fmt
    return (f"M{f(x + r)} {f(y)}H{f(x + w - r)}A{f(r)} {f(r)} 0 0 1 {f(x + w)} {f(y + r)}"
            f"V{f(y + h - r)}A{f(r)} {f(r)} 0 0 1 {f(x + w - r)} {f(y + h)}H{f(x + r)}"
            f"A{f(r)} {f(r)} 0 0 1 {f(x)} {f(y + h - r)}V{f(y + r)}A{f(r)} {f(r)} 0 0 1 {f(x + r)} {f(y)}Z")


def _chevron_outline(A, B, C, r):
    """Closed outline of a polyline A-B-C stroked with width 2r, round caps and round outer join."""
    def sub(p, q): return (p[0] - q[0], p[1] - q[1])
    def add(p, q): return (p[0] + q[0], p[1] + q[1])
    def mul(p, k): return (p[0] * k, p[1] * k)
    def norm(p):
        l = math.hypot(*p)
        return (p[0] / l, p[1] / l)
    def cross(a, b): return a[0] * b[1] - a[1] * b[0]
    def dot(a, b): return a[0] * b[0] + a[1] * b[1]
    u1, u2 = norm(sub(B, A)), norm(sub(C, B))
    n1, n2 = (-u1[1], u1[0]), (-u2[1], u2[0])
    o = 1 if dot(n1, u2) < 0 else -1
    i = -o
    P = lambda p, n, s: add(p, mul(n, s * r))
    p0, p1, p2, p3, p4, p5 = P(A, n1, o), P(B, n1, o), P(B, n2, o), P(C, n2, o), P(C, n2, i), P(A, n1, i)
    m = add(B, mul(add(n1, n2), i * r / (1 + dot(n1, n2))))
    sj = 1 if cross(mul(n1, o), mul(n2, o)) > 0 else 0
    sc = 1 if cross(mul(n2, o), u2) > 0 else 0
    sa = 1 if cross(mul(n1, i), mul(u1, -1)) > 0 else 0
    pt = lambda p: f"{fmt(p[0])} {fmt(p[1])}"
    arc = lambda sw, p: f"A{fmt(r)} {fmt(r)} 0 0 {sw} {pt(p)}"
    return f"M{pt(p0)}L{pt(p1)}{arc(sj, p2)}L{pt(p3)}{arc(sc, p4)}L{pt(m)}L{pt(p5)}{arc(sa, p0)}Z"


def symbol_mono_parts(color="#000000", uid=None):
    """Single-colour symbol as ONE even-odd compound path: plate with chevron and three cells knocked out.
    The fourth (selected) cell stays solid. No masks, no filters."""
    d = _rr(16, 16, 224, 224, 56)
    d += _chevron_outline((65, 92), (103, 128), (65, 164), 12)
    d += _rr(127, 90, 32, 32, 8) + _rr(127, 134, 32, 32, 8) + _rr(171, 134, 32, 32, 8)
    return f'<path fill-rule="evenodd" fill="{color}" d="{d}"/>'


def svg_doc(vb, body, title, desc=None, size=None):
    x, y, w, h = vb
    sw, sh = size if size else (w, h)
    d = f"<desc>{desc}</desc>" if desc else ""
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
        f'version="1.1" viewBox="{fmt(x)} {fmt(y)} {fmt(w)} {fmt(h)}" width="{fmt(sw)}" height="{fmt(sh)}" '
        f'role="img" aria-label="{title}">\n<title>{title}</title>{d}\n{body}\n</svg>\n'
    )


def symbol_svg(theme="light", small=False, mono=None, glyph_only=None):
    """glyph_only: None or dict of colours (no plate, cropped to a square around the glyph)."""
    if mono:
        return svg_doc((0, 0, 256, 256), symbol_mono_parts(mono), "PlayniteCmdPal symbol (monochrome)",
                       size=(256, 256))
    if glyph_only:
        body = symbol_parts(glyph_only, small=small, plate=False)
        return svg_doc((28, 28, 200, 200), body, "PlayniteCmdPal glyph", size=(256, 256))
    c = THEMES[theme]
    return svg_doc((0, 0, 256, 256), symbol_parts(c, small=small),
                   f"PlayniteCmdPal symbol ({theme})", size=(256, 256))


GLYPH_COBALT = dict(tile=None, chev=COBALT, sel=COBALT, cell="#8FAEFF")
GLYPH_WHITE = dict(tile=None, chev=WHITE, sel=WHITE, cell=TINT)

# --------------------------------------------------------------------------- wordmark (outlined Inter SemiBold)
WM_FONT = FONT_DIR / "Inter-SemiBold.ttf"
_tt = None
_gs = None


def _font():
    global _tt, _gs
    if _tt is None:
        _tt = TTFont(str(WM_FONT))
        _gs = _tt.getGlyphSet()
    return _tt, _gs


def shape_text(text, size, tracking_em=-0.012):
    data = WM_FONT.read_bytes()
    face = hb.Face(data)
    font = hb.Font(face)
    upem = face.upem
    buf = hb.Buffer()
    buf.add_str(text)
    buf.guess_segment_properties()
    hb.shape(font, buf, {"kern": True, "liga": False, "calt": False})
    tt, gs = _font()
    order = tt.getGlyphOrder()
    sc = size / upem
    x = 0.0
    glyphs = []
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        glyphs.append((order[info.codepoint], x + pos.x_offset * sc, info.cluster))
        x += pos.x_advance * sc + tracking_em * size
    cap = tt["OS/2"].sCapHeight * sc
    return glyphs, sc, cap


def glyph_path(glyphs, sc, ox, oy, lo=0, hi=10 ** 6):
    tt, gs = _font()
    d = []
    bp = BoundsPen(gs)
    for name, gx, cl in glyphs:
        if not (lo <= cl < hi):
            continue
        pen = SVGPathPen(gs, ntos=fmt)
        gs[name].draw(TransformPen(pen, (sc, 0, 0, -sc, ox + gx, oy)))
        d.append(pen.getCommands())
        gs[name].draw(TransformPen(bp, (sc, 0, 0, -sc, ox + gx, oy)))
    return "".join(d), bp.bounds


def wordmark_body(size, ox, oy, text_col, accent_col, uid):
    glyphs, sc, cap = shape_text("PlayniteCmdPal", size)
    d1, b1 = glyph_path(glyphs, sc, ox, oy, 0, 8)      # "Playnite"
    d2, b2 = glyph_path(glyphs, sc, ox, oy, 8, 99)     # "CmdPal"
    body = (
        f'<g id="{uid}-wordmark" aria-label="PlayniteCmdPal">'
        f'<path id="{uid}-playnite" d="{d1}" fill="{text_col}"/>'
        f'<path id="{uid}-cmdpal" d="{d2}" fill="{accent_col}"/></g>'
    )
    bounds = (min(b1[0], b2[0]), min(b1[1], b2[1]), max(b1[2], b2[2]), max(b1[3], b2[3]))
    return body, bounds, cap


def lockup_svg(theme="light", mono=None):
    H = 96
    k = H / 256
    size = 54
    if mono:
        text_col = accent_col = mono
    else:
        c = THEMES[theme]
        text_col, accent_col = c["text"], c["accent"]
    # measure
    _, b, cap = wordmark_body(size, 0, 0, text_col, accent_col, "m")
    baseline = 48 + cap / 2
    gap = 22
    ox = 90 + gap - b[0]
    body, b, cap = wordmark_body(size, ox, baseline, text_col, accent_col, "pcp")
    W = b[2] + 6
    if mono:
        sym = symbol_mono_parts(mono, "pcp-mono")
        defs = f'<defs><g id="pcp-symbol">{sym}</g></defs>'
    else:
        defs = f'<defs><g id="pcp-symbol">{symbol_parts(THEMES[theme])}</g></defs>'
    use = f'<use xlink:href="#pcp-symbol" transform="scale({fmt(k)})"/>'
    name = "monochrome" if mono else theme
    return svg_doc((0, 0, round(W, 2), H), defs + use + body,
                   f"PlayniteCmdPal horizontal wordmark ({name})", size=(round(W * 2, 2), H * 2))


def compact_svg(theme="light"):
    c = THEMES[theme]
    size = 56
    _, b, cap = wordmark_body(size, 0, 0, c["text"], c["accent"], "m")
    pad = 8
    ox, oy = pad - b[0], pad - b[1]
    body, b2, _ = wordmark_body(size, ox, oy, c["text"], c["accent"], "pcp")
    W, H = b2[2] + pad, b2[3] + pad
    return svg_doc((0, 0, round(W, 2), round(H, 2)), body, f"PlayniteCmdPal compact wordmark ({theme})",
                   size=(round(W * 2, 2), round(H * 2, 2)))


# --------------------------------------------------------------------------- raster
def render(svg_text, w, h=None):
    return cairosvg.svg2png(bytestring=svg_text.encode("utf-8"), output_width=int(w), output_height=int(h) if h else None)


def pil(svg_text, w, h=None):
    return Image.open(io.BytesIO(render(svg_text, w, h))).convert("RGBA")


SMALL_MAX = 32


def plated_svg(size, theme="light"):
    return symbol_svg(theme, small=size <= SMALL_MAX)


def glyph_svg(size, kind):
    return symbol_svg(small=size <= SMALL_MAX, glyph_only=GLYPH_COBALT if kind == "cobalt" else GLYPH_WHITE)


def wide_tile_png(w, h):
    """Wide 310x150 tile: full-bleed Graphite, plated symbol + light wordmark."""
    S = 4
    im = Image.new("RGBA", (w * S, h * S), GRAPHITE)
    sym_h = int(h * 0.52 * S)
    sym = pil(symbol_svg("dark"), sym_h)
    size = h * 0.15 * S
    glyphs, sc, cap = shape_text("PlayniteCmdPal", size)
    # build a tiny svg for the wordmark so it is rasterised from the same outlines
    d1, b1 = glyph_path(glyphs, sc, 0, 0, 0, 8)
    d2, b2 = glyph_path(glyphs, sc, 0, 0, 8, 99)
    tw = max(b1[2], b2[2]) - min(b1[0], b2[0])
    gap = h * 0.07 * S
    total = sym_h * 0.875 + gap + tw
    x0 = (w * S - total) / 2
    sym_x = x0 - sym_h * 0.0625
    im.alpha_composite(sym, (int(sym_x), int((h * S - sym_h) / 2)))
    tx = x0 + sym_h * 0.875 + gap - min(b1[0], b2[0])
    ty = h * S / 2 + cap / 2
    svg = svg_doc(
        (0, 0, w * S, h * S),
        f'<path transform="translate({fmt(tx)} {fmt(ty)})" d="{d1}" fill="#FFFFFF"/>'
        f'<path transform="translate({fmt(tx)} {fmt(ty)})" d="{d2}" fill="{COBALT_LIGHT}"/>',
        "wide tile text")
    txt = pil(svg, w * S, h * S)
    im.alpha_composite(txt)
    return im.resize((w, h), Image.LANCZOS)


def png_bytes(im):
    b = io.BytesIO()
    im.save(b, "PNG", optimize=True)
    return b.getvalue()


def ico_bytes(entries):
    """entries: list of PIL RGBA images. BMP/DIB for < 256 px, PNG for 256 px."""
    blobs = []
    for im in entries:
        w, h = im.size
        if w >= 256:
            blobs.append((w, h, png_bytes(im)))
            continue
        pix = im.transpose(Image.FLIP_TOP_BOTTOM).tobytes("raw", "BGRA")
        mask_row = ((w + 31) // 32) * 4
        hdr = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(pix) + mask_row * h, 0, 0, 0, 0)
        blobs.append((w, h, hdr + pix + b"\x00" * (mask_row * h)))
    n = len(blobs)
    out = struct.pack("<HHH", 0, 1, n)
    off = 6 + 16 * n
    body = b""
    for w, h, data in blobs:
        out += struct.pack("<BBBBHHII", 0 if w >= 256 else w, 0 if h >= 256 else h, 0, 0, 1, 32,
                           len(data), off + len(body))
        body += data
    return out + body


# --------------------------------------------------------------------------- preview canvas
_fcache = {}


def font_obj(name, px):
    key = (name, px)
    if key not in _fcache:
        _fcache[key] = ImageFont.truetype(str(FONT_DIR / f"Inter-{name}.ttf"), px)
    return _fcache[key]


class Canvas:
    def __init__(self, w, h, bg, S=2):
        self.S, self.w, self.h = S, w, h
        self.im = Image.new("RGBA", (w * S, h * S), bg)
        self.d = ImageDraw.Draw(self.im)

    def rect(self, x, y, w, h, fill=None, r=0, outline=None, width=1):
        S = self.S
        self.d.rounded_rectangle([x * S, y * S, (x + w) * S - 1, (y + h) * S - 1], radius=r * S, fill=fill,
                                 outline=outline, width=max(1, int(width * S)) if outline else 0)

    def text(self, x, y, t, size, weight="Regular", fill="#000", anchor="la"):
        self.d.text((x * self.S, y * self.S), t, font=font_obj(weight, int(size * self.S)), fill=fill,
                    anchor=anchor)

    def tw(self, t, size, weight="Regular"):
        return font_obj(weight, int(size * self.S)).getlength(t) / self.S

    def line(self, pts, fill, width=1):
        self.d.line([(px * self.S, py * self.S) for px, py in pts], fill=fill, width=max(1, int(width * self.S)))

    def poly(self, pts, fill):
        self.d.polygon([(px * self.S, py * self.S) for px, py in pts], fill=fill)

    def ellipse(self, x, y, w, h, fill=None, outline=None, width=1):
        S = self.S
        self.d.ellipse([x * S, y * S, (x + w) * S, (y + h) * S], fill=fill, outline=outline,
                       width=int(width * S))

    def paste(self, im, x, y, w=None, h=None, resample=Image.LANCZOS):
        """Paste a PIL image. Without w the image is pasted pixel-exact; with w/h (logical units) it is resized."""
        S = self.S
        if w:
            im = im.resize((int(w * S), int(h * S) if h else int(w * S)), resample)
        self.im.paste(im, (int(x * S), int(y * S)), im)

    def psvg(self, svg_text, w, x, y, h=None):
        """Rasterise SVG directly at canvas resolution (w, h in logical units) and paste."""
        S = self.S
        im = pil(svg_text, int(round(w * S)), int(round(h * S)) if h else None)
        self.im.paste(im, (int(round(x * S)), int(round(y * S))), im)

    def shadow(self, x, y, w, h, r, blur=14, off=(0, 10), alpha=90):
        S = self.S
        layer = Image.new("RGBA", self.im.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rounded_rectangle(
            [(x + off[0]) * S, (y + off[1]) * S, (x + off[0] + w) * S, (y + off[1] + h) * S],
            radius=r * S, fill=(0, 0, 0, alpha))
        self.im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur * S)))

    def save(self, path, flatten=True):
        out = self.im.resize((self.w, self.h), Image.LANCZOS)
        if flatten:
            out = out.convert("RGB")
        path.parent.mkdir(parents=True, exist_ok=True)
        out.save(path, "PNG", optimize=True)


def svg_im(svg, w, h=None):
    return pil(svg, int(w), int(h) if h else None)


# --------------------------------------------------------------------------- generate SVG + raster assets
def build_svgs():
    files = {
        "logo-symbol.svg": symbol_svg("light"),
        "logo-symbol-light.svg": symbol_svg("light"),
        "logo-symbol-dark.svg": symbol_svg("dark"),
        "logo-symbol-small.svg": symbol_svg("light", small=True),
        "logo-symbol-small-dark.svg": symbol_svg("dark", small=True),
        "logo-glyph-cobalt.svg": symbol_svg(glyph_only=GLYPH_COBALT),
        "logo-glyph-white.svg": symbol_svg(glyph_only=GLYPH_WHITE),
        "logo-monochrome.svg": symbol_svg(mono="#000000"),
        "wordmark-horizontal.svg": lockup_svg("light"),
        "wordmark-horizontal-light.svg": lockup_svg("light"),
        "wordmark-horizontal-dark.svg": lockup_svg("dark"),
        "wordmark-horizontal-monochrome.svg": lockup_svg(mono="#000000"),
        "wordmark-compact.svg": compact_svg("light"),
        "wordmark-compact-light.svg": compact_svg("light"),
        "wordmark-compact-dark.svg": compact_svg("dark"),
    }
    for name, text in files.items():
        write(SVG_DIR / name, text)
    return files


SIZES = [16, 24, 32, 40, 48, 64, 128, 150, 256, 300, 310, 400, 512]


def build_png_ico():
    PNG_DIR.mkdir(parents=True, exist_ok=True)
    MSIX_DIR.mkdir(parents=True, exist_ok=True)
    UNPLATED_DIR.mkdir(parents=True, exist_ok=True)
    ICO_DIR.mkdir(parents=True, exist_ok=True)
    cache = {}

    def plated(s):
        if s not in cache:
            cache[s] = pil(plated_svg(s), s)
        return cache[s]

    for s in SIZES:
        plated(s).save(PNG_DIR / f"app-icon-{s}.png", optimize=True)
        pil(glyph_svg(s, "cobalt"), s).save(UNPLATED_DIR / f"glyph-cobalt-{s}.png", optimize=True)
        pil(glyph_svg(s, "white"), s).save(UNPLATED_DIR / f"glyph-white-{s}.png", optimize=True)
    # dark-theme plated variants
    for s in (16, 24, 32, 48, 64, 128, 256, 512):
        pil(symbol_svg("dark", small=s <= SMALL_MAX), s).save(PNG_DIR / f"app-icon-dark-{s}.png", optimize=True)

    # --- MSIX-style asset set (names follow standard Windows asset conventions; NOT matched to a manifest)
    def save(name, im):
        im.save(MSIX_DIR / name, optimize=True)

    for scale, px in ((100, 44), (200, 88), (400, 176)):
        save(f"Square44x44Logo.scale-{scale}.png", plated(px) if px in cache else pil(plated_svg(px), px))
    for t in (16, 24, 32, 48, 256):
        save(f"Square44x44Logo.targetsize-{t}.png", plated(t) if t in cache else pil(plated_svg(t), t))
        save(f"Square44x44Logo.targetsize-{t}_altform-unplated.png", pil(glyph_svg(t, "white"), t))
        save(f"Square44x44Logo.targetsize-{t}_altform-lightunplated.png", pil(glyph_svg(t, "cobalt"), t))
    for scale, px in ((100, 150), (200, 300), (400, 600)):
        save(f"Square150x150Logo.scale-{scale}.png", pil(plated_svg(px), px))
    for scale, px in ((100, 50), (200, 100), (400, 200)):
        save(f"StoreLogo.scale-{scale}.png", pil(plated_svg(px), px))
    save("Square310x310Logo.scale-100.png", pil(plated_svg(310), 310))
    for scale, (w, h) in ((100, (310, 150)), (200, (620, 300)), (400, (1240, 600))):
        save(f"Wide310x150Logo.scale-{scale}.png", wide_tile_png(w, h))
    wide_tile_png(310, 150).save(PNG_DIR / "wide-tile-310x150.png", optimize=True)
    pil(plated_svg(50), 50).save(PNG_DIR / "store-logo-50.png", optimize=True)
    pil(plated_svg(44), 44).save(PNG_DIR / "square-44x44-logo.png", optimize=True)
    pil(plated_svg(150), 150).save(PNG_DIR / "square-150x150-logo.png", optimize=True)

    # --- ICO
    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    write_bin(ICO_DIR / "PlayniteCmdPal.ico", ico_bytes([plated(s) for s in ico_sizes]))
    write_bin(ICO_DIR / "PlayniteCmdPal-dark.ico",
              ico_bytes([pil(symbol_svg("dark", small=s <= SMALL_MAX), s) for s in ico_sizes]))
    write_bin(ICO_DIR / "PlayniteCmdPal-small.ico", ico_bytes([plated(s) for s in (16, 24, 32, 48)]))


def write_bin(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


# --------------------------------------------------------------------------- previews
def checker(size, a="#E9ECF2", b="#FFFFFF", cell=8):
    im = Image.new("RGBA", (size, size), b)
    d = ImageDraw.Draw(im)
    for yy in range(0, size, cell):
        for xx in range(0, size, cell):
            if (xx // cell + yy // cell) % 2:
                d.rectangle([xx, yy, xx + cell - 1, yy + cell - 1], fill=a)
    return im


def build_app_icon_preview():
    W, H = 2000, 1480
    cv = Canvas(W, H, MIST, S=1)
    cv.text(60, 40, "PlayniteCmdPal  /  App icon preview", 34, "SemiBold", GRAPHITE)
    cv.text(60, 88, "Row 1: actual pixel size. Row 2: 16, 24, 32 px magnified 6x (nearest neighbour) for inspection.",
            18, "Regular", SLATE)
    sizes = [16, 24, 32, 48, 64, 128, 256, 512]
    for i, (bg, fg, sub, theme, border) in enumerate(((WHITE, GRAPHITE, SLATE, "light", "#D7DCE6"),
                                                       (GRAPHITE, WHITE, "#9AA3B5", "dark", "#2A303B"))):
        py = 140 + i * 650
        cv.rect(40, py, W - 80, 620, bg, 18, border, 2)
        cv.text(70, py + 22, "Light background" if i == 0 else "Dark background", 22, "SemiBold", fg)
        x = 80
        for s in sizes:
            sc = 0.5 if s == 512 else 1
            ds = int(s * sc)
            im = pil(symbol_svg(theme, small=s <= SMALL_MAX), ds)
            cv.paste(im, x, py + 70 + (256 - ds) if ds < 256 else py + 70)
            cv.text(x, py + 70 + 262, f"{s}", 15, "Medium", sub)
            x += ds + 44
        x = 80
        for s in (16, 24, 32):
            im = pil(symbol_svg(theme, small=True), s).resize((s * 6, s * 6), Image.NEAREST)
            cv.paste(im, x, py + 350)
            cv.text(x, py + 350 + s * 6 + 8, f"{s} px x6", 15, "Medium", sub)
            x += s * 6 + 60
        # small-size variant vs full detail at 32/48 for comparison
        x += 40
        for s, small in ((32, True), (32, False), (48, False)):
            im = pil(symbol_svg(theme, small=small), s).resize((s * 4, s * 4), Image.NEAREST)
            cv.paste(im, x, py + 350)
            cv.text(x, py + 350 + s * 4 + 8, f"{s} px {'small' if small else 'full'} x4", 15, "Medium", sub)
            x += s * 4 + 50
    cv.save(PRE_DIR / "app-icon-preview.png")


def build_brand_sheet():
    W, H = 2400, 1960
    cv = Canvas(W, H, MIST)
    P = 60
    cv.psvg(lockup_svg("light"), 520, P, 40)
    cv.text(W - P, 56, "Brand sheet  /  v1.0", 22, "SemiBold", GRAPHITE, "ra")
    cv.text(W - P, 90, "Microsoft PowerToys Command Palette extension for Playnite", 18, "Regular", SLATE, "ra")

    def panel(x, y, w, h, bg, title, tc=SLATE, border="#D7DCE6"):
        cv.rect(x, y, w, h, bg, 16, border, 2)
        cv.text(x + 24, y + 18, title, 16, "SemiBold", tc)

    # row 1: lockups
    y = 170
    panel(P, y, 1130, 290, WHITE, "PRIMARY LOCKUP  /  LIGHT BACKGROUND")
    cv.psvg(lockup_svg("light"), 760, P + 185, y + 100)
    panel(P + 1170, y, 1110, 290, GRAPHITE, "LOCKUP  /  DARK BACKGROUND", "#9AA3B5", "#2A303B")
    cv.psvg(lockup_svg("dark"), 760, P + 1170 + 175, y + 100)

    # row 2: symbol family + compact wordmark + mono
    y = 490
    panel(P, y, 1130, 380, WHITE, "SYMBOL FAMILY")
    x = P + 40
    items = [(symbol_svg("light"), 200, "Light"), (symbol_svg("dark"), 200, "Dark"),
             (symbol_svg(mono="#000000"), 200, "Monochrome"), (symbol_svg(small=True), 200, "Small (16-32 px)")]
    for svg, s, lab in items:
        bg = GRAPHITE if lab == "Dark" else MIST
        cv.rect(x - 6, y + 70, s + 12, s + 12, bg, 12)
        cv.psvg(svg, s, x, y + 76)
        cv.text(x + s / 2, y + 76 + s + 22, lab, 15, "Medium", SLATE, "ma")
        x += s + 40
    panel(P + 1170, y, 540, 380, WHITE, "COMPACT WORDMARK")
    cv.psvg(compact_svg("light"), 460, P + 1170 + 40, y + 90)
    cv.rect(P + 1170 + 24, y + 200, 492, 150, GRAPHITE, 12)
    cv.psvg(compact_svg("dark"), 460, P + 1170 + 40, y + 250)
    panel(P + 1170 + 570, y, 540, 380, WHITE, "MONOCHROME LOCKUP")
    cv.psvg(lockup_svg(mono="#000000"), 440, P + 1170 + 570 + 50, y + 100)
    cv.rect(P + 1170 + 570 + 24, y + 200, 492, 150, COBALT, 12)
    cv.psvg(lockup_svg(mono="#FFFFFF"), 440, P + 1170 + 570 + 50, y + 245)

    # row 3: palette
    y = 900
    cv.text(P, y, "COLOUR", 16, "SemiBold", SLATE)
    sw = (W - 2 * P - 7 * 20) / 8
    for i, (name, hx, _) in enumerate(PALETTE):
        x = P + i * (sw + 20)
        cv.rect(x, y + 32, sw, 120, hx, 14, "#D7DCE6" if hx in (WHITE, MIST) else None, 2)
        r, g, b = hex_rgb(hx)
        cv.text(x, y + 164, name, 18, "SemiBold", GRAPHITE)
        cv.text(x, y + 190, hx, 16, "Medium", SLATE)
        cv.text(x, y + 212, f"RGB {r}, {g}, {b}", 14, "Regular", SLATE)

    # row 4: size ladder
    y = 1150
    cv.text(P, y, "SIZE LADDER  /  ACTUAL PIXELS", 16, "SemiBold", SLATE)
    cv.rect(P, y + 32, 1130, 260, WHITE, 16, "#D7DCE6", 2)
    cv.rect(P + 1170, y + 32, 1110, 260, GRAPHITE, 16, "#2A303B", 2)
    for off, theme in ((0, "light"), (1170, "dark")):
        x = P + 36 + off
        for s in (16, 24, 32, 48, 64, 128):
            cv.psvg(symbol_svg(theme, small=s <= SMALL_MAX), s, x, y + 52 + (128 - s) // 2)
            cv.text(x + s / 2, y + 52 + 128 + 24, str(s), 14, "Medium", SLATE if off == 0 else "#9AA3B5", "ma")
            x += s + 46 if s < 64 else s + 40
        if off == 0:
            pass

    # row 5: clear space, min size, typography
    y = 1470
    panel(P, y, 560, 420, WHITE, "CLEAR SPACE  /  x = 1/4 SYMBOL HEIGHT")
    sx, sy, ss = P + 130, y + 110, 200
    k = ss / 224
    ox = sx - 16 * k
    oy = sy - 16 * k
    cx = 56 * k
    cv.rect(sx - cx, sy - cx, ss + 2 * cx, ss + 2 * cx, None, 0, "#E5484D", 2)
    cv.psvg(symbol_svg("light"), int(256 * k), ox, oy)
    for (ax, ay, aw, ah) in ((sx - cx, sy - cx, cx, cx), (sx + ss, sy + ss, cx, cx)):
        cv.rect(ax, ay, aw, ah, "#FBD5D6", 0)
        cv.text(ax + aw / 2, ay + ah / 2, "x", 20, "SemiBold", "#E5484D", "mm")
    cv.text(P + 24, y + 392 - 20, "Keep this margin free of text, edges and other marks.", 14, "Regular", SLATE)

    panel(P + 600, y, 560, 420, WHITE, "MINIMUM SIZES")
    cv.text(P + 624, y + 70, "Symbol", 16, "SemiBold", GRAPHITE)
    cv.psvg(symbol_svg(small=True), 16, P + 624, y + 100)
    cv.text(P + 650, y + 100, "16 px  (small variant)   /   48 px full variant for detail", 14, "Regular", SLATE)
    cv.text(P + 624, y + 160, "Horizontal lockup", 16, "SemiBold", GRAPHITE)
    cv.psvg(lockup_svg("light"), 120, P + 624, y + 192)
    cv.text(P + 624, y + 250, "120 px wide  /  24 px symbol height", 14, "Regular", SLATE)
    cv.text(P + 624, y + 290, "Compact wordmark", 16, "SemiBold", GRAPHITE)
    cv.psvg(compact_svg("light"), 110, P + 624, y + 322)
    cv.text(P + 624, y + 360, "110 px wide", 14, "Regular", SLATE)

    panel(P + 1200, y, 1080, 420, WHITE, "TYPOGRAPHY")
    cv.text(P + 1224, y + 62, "PlayniteCmdPal", 66, "SemiBold", GRAPHITE)
    cv.text(P + 1224, y + 150, "Inter SemiBold  /  wordmark, outlined to vector paths  /  SIL OFL 1.1", 16, "Medium", SLATE)
    cv.text(P + 1224, y + 196, "Search your Playnite library", 30, "SemiBold", GRAPHITE)
    cv.text(P + 1224, y + 240, "Launch games from Command Palette without leaving the keyboard.", 20, "Regular", SLATE)
    cv.text(P + 1224, y + 290, "UI text in product: Segoe UI Variable (system font on Windows 11).", 16, "Medium", SLATE)
    cv.text(P + 1224, y + 320, "Docs and marketing: Inter Regular / Medium / SemiBold.", 16, "Medium", SLATE)
    cv.text(P + 1224, y + 362, "Do not set the wordmark in any other typeface.", 16, "Regular", SLATE)
    cv.save(PRE_DIR / "brand-sheet.png")


def build_misuse_sheet():
    W, H = 2000, 1360
    cv = Canvas(W, H, MIST)
    cv.text(60, 40, "PlayniteCmdPal  /  Logo misuse", 34, "SemiBold", GRAPHITE)
    cv.text(60, 88, "None of these treatments are permitted.", 18, "Regular", SLATE)
    random.seed(7)
    R = 2  # sample images are built at 2x pixel size (canvas scale)
    base = pil(symbol_svg("light"), 256 * R)
    cases = []
    cases.append(("Do not stretch or squash", lambda: base.resize((330 * R, 170 * R), Image.LANCZOS), MIST))
    red = pil(symbol_svg("light").replace(COBALT, "#E8452C"), 256 * R)
    cases.append(("Do not recolour outside the palette", lambda: red, MIST))
    cases.append(("Do not rotate or skew", lambda: base.rotate(22, expand=True, resample=Image.BICUBIC), MIST))

    def shadow():
        sh = Image.new("RGBA", (330 * R, 330 * R), (0, 0, 0, 0))
        blob = Image.new("RGBA", base.size, (0, 0, 0, 190))
        blob.putalpha(base.getchannel("A").point(lambda v: int(v * 0.75)))
        sh.paste(blob, (44 * R, 62 * R), blob)
        sh = sh.filter(ImageFilter.GaussianBlur(14 * R))
        sh.alpha_composite(base, (36 * R, 36 * R))
        return sh
    cases.append(("Do not add shadows, glows or bevels", shadow, MIST))
    cases.append(("Do not place on low-contrast backgrounds", lambda: base, "#3A74FF"))
    cases.append(("Do not place on busy imagery", lambda: base, "BUSY"))

    def wrongtype():
        im = Image.new("RGBA", (640 * R, 120 * R), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        serif = Path("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf")
        f = ImageFont.truetype(str(serif), 50 * R) if serif.exists() else font_obj("Regular", 50 * R)
        d.text((0, 20 * R), "playnite-cmd-pal", font=f, fill=GRAPHITE)
        return im
    cases.append(("Do not retype or change the wordmark", wrongtype, WHITE))

    def outline():
        im = Image.new("RGBA", (256 * R, 256 * R), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([16 * R, 16 * R, 240 * R, 240 * R], 56 * R, outline=COBALT, width=10 * R)
        d.line([(65 * R, 92 * R), (103 * R, 128 * R), (65 * R, 164 * R)], fill=COBALT, width=10 * R, joint="curve")
        return im
    cases.append(("Do not outline or rebuild the symbol", outline, MIST))

    def addgame():
        im = base.copy()
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([150 * R, 150 * R, 250 * R, 215 * R], 14 * R, fill="#202020")
        d.ellipse([172 * R, 160 * R, 188 * R, 176 * R], fill="#FF3B3B")
        d.ellipse([205 * R, 160 * R, 221 * R, 176 * R], fill="#3BFF6F")
        d.rectangle([166 * R, 188 * R, 232 * R, 200 * R], fill="#888888")
        return im
    cases.append(("Do not add controllers or game motifs", addgame, MIST))

    cols, cw, ch = 3, 600, 350
    for i, (label, fn, bgc) in enumerate(cases):
        cx = 60 + (i % cols) * (cw + 40)
        cy = 140 + (i // cols) * (ch + 40)
        cv.rect(cx, cy, cw, ch, WHITE, 16, "#D7DCE6", 2)
        inner = (cx + 20, cy + 20, cw - 40, 270)
        if bgc == "BUSY":
            cv.rect(*inner, "#888888", 10)
            for _ in range(46):
                bw, bh = random.randint(20, 90), random.randint(8, 50)
                x = inner[0] + random.random() * (inner[2] - bw)
                y = inner[1] + random.random() * (inner[3] - bh)
                cv.rect(x, y, bw, bh, random.choice(
                    ["#FF6B3D", "#3DDCFF", "#7C3DFF", "#FFD23D", "#1D1D1D", "#EAEAEA"]), 4)
        else:
            cv.rect(*inner, bgc, 10)
        im = fn()
        lw, lh = im.width / R, im.height / R
        sc = min(200 / lw, 200 / lh, 1.0)
        if label.startswith("Do not retype"):
            sc = min(380 / lw, 1.0)
        pw, ph = lw * sc, lh * sc
        cv.paste(im, inner[0] + (inner[2] - pw) / 2, inner[1] + (inner[3] - ph) / 2, pw, ph)
        bx, by = cx + cw - 52, cy + 28
        cv.ellipse(bx, by, 32, 32, "#E5484D")
        cv.line([(bx + 10, by + 10), (bx + 22, by + 22)], WHITE, 3)
        cv.line([(bx + 22, by + 10), (bx + 10, by + 22)], WHITE, 3)
        cv.text(cx + 24, cy + 308, label, 17, "SemiBold", GRAPHITE)
    cv.save(PRE_DIR / "logo-misuse.png")


def game_art(cv, x, y, s, kind):
    pal = {
        0: ("#2B3A67", "#8FB4FF"), 1: ("#5A2E5A", "#FFB86B"), 2: ("#1F4D46", "#9BE8C7"), 3: ("#4A3B2A", "#F2D388"),
        4: ("#3A2F63", "#B9A6FF"),
    }[kind % 5]
    cv.rect(x, y, s, s, pal[0], s * 0.2)
    c = pal[1]
    m = s / 2
    if kind % 5 == 0:
        cv.ellipse(x + s * .28, y + s * .28, s * .44, s * .44, None, c, max(2, s * .07))
    elif kind % 5 == 1:
        cv.poly([(x + m, y + s * .24), (x + s * .76, y + s * .74), (x + s * .24, y + s * .74)], c)
    elif kind % 5 == 2:
        cv.poly([(x + m, y + s * .22), (x + s * .78, y + m), (x + m, y + s * .78), (x + s * .22, y + m)], c)
    elif kind % 5 == 3:
        cv.rect(x + s * .27, y + s * .27, s * .46, s * .46, c, s * .06)
    else:
        cv.rect(x + s * .24, y + s * .44, s * .52, s * .12, c, s * .05)
        cv.rect(x + s * .44, y + s * .24, s * .12, s * .52, c, s * .05)


def wallpaper(w, h, c1, c2):
    g = Image.linear_gradient("L").resize((w, h)).rotate(35, resample=Image.BICUBIC, expand=False)
    g = g.resize((w, h))
    return ImageOps.colorize(g, c1, c2).convert("RGBA")


def palette_window(cv, x, y, w, h, dark, query, rows, selected, header, show_home=False):
    bg = "#272A30" if dark else "#F7F8FA"
    fg = "#FFFFFF" if dark else "#14181F"
    sub = "#A5ADBB" if dark else "#5B6475"
    line = "#383C45" if dark else "#DFE3EA"
    selbg = "#363C4A" if dark else "#E4EBFB"
    cv.shadow(x, y, w, h, 14)
    cv.rect(x, y, w, h, bg, 14, "#3F444F" if dark else "#CBD1DB", 1)
    # search row
    cv.rect(x + 20, y + 18, w - 40, 52, "#1E2127" if dark else "#FFFFFF", 8, line, 1)
    cv.psvg(symbol_svg("dark" if dark else "light", small=True), 32, x + 30, y + 28)
    if query:
        cv.text(x + 78, y + 44, query, 19, "Regular", fg, "lm")
        cv.rect(x + 78 + cv.tw(query, 19) + 3, y + 32, 2, 24, COBALT_LIGHT if dark else COBALT)
    else:
        cv.text(x + 78, y + 44, "Search for apps, commands and more...", 19, "Regular", sub, "lm")
    cv.line([(x, y + 90), (x + w, y + 90)], line, 1)
    cv.text(x + 28, y + 106, header, 13, "SemiBold", sub)
    ry = y + 134
    for i, (title, subt, tag, art) in enumerate(rows):
        rh = 64
        if i == selected:
            cv.rect(x + 14, ry, w - 28, rh, selbg, 8)
            cv.rect(x + 14, ry + 16, 4, rh - 32, COBALT_LIGHT if dark else COBALT, 2)
        if art == "sym":
            cv.psvg(symbol_svg("dark" if dark else "light"), 44, x + 32, ry + 10)
        elif isinstance(art, int):
            game_art(cv, x + 32, ry + 10, 44, art)
        else:
            cv.rect(x + 32, ry + 10, 44, 44, "#3A3F4B" if dark else "#E2E6EE", 10)
            cv.text(x + 54, ry + 33, art, 18, "SemiBold", sub, "mm")
        cv.text(x + 92, ry + 12, title, 18, "SemiBold", fg)
        cv.text(x + 92, ry + 37, subt, 14, "Regular", sub)
        if tag:
            tw_ = cv.tw(tag, 13, "Medium") + 20
            cv.rect(x + w - 34 - tw_, ry + 20, tw_, 24, "#3B4150" if dark else "#E3E7EF", 12)
            cv.text(x + w - 34 - tw_ / 2, ry + 32, tag, 13, "Medium", sub, "mm")
        ry += rh + 4
    # bottom bar
    cv.line([(x, y + h - 52), (x + w, y + h - 52)], line, 1)
    cv.text(x + 28, y + h - 26, "Launch", 14, "SemiBold", fg, "lm")
    kx = x + 28 + cv.tw("Launch", 14, "SemiBold") + 10
    cv.rect(kx, y + h - 38, 46, 24, "#3B4150" if dark else "#E3E7EF", 6)
    cv.text(kx + 23, y + h - 26, "Enter", 12, "Medium", sub, "mm")
    cv.text(x + w - 28, y + h - 26, "Illustrative mock-up", 12, "Regular", sub, "rm")


def build_mockup():
    W, H = 2000, 660
    cv = Canvas(W, H, "#000000")
    cv.paste(wallpaper(1000, H, "#101A3A", "#3B63D8"), 0, 0, 1000, H)
    cv.paste(wallpaper(1000, H, "#DCE6FB", "#8DB0F2"), 1000, 0, 1000, H)
    rows_dark = [
        ("Orbital Drift", "Playnite  /  Last played 2 days ago", "Playnite", 0),
        ("Orbit Keeper", "Playnite  /  Last played 3 weeks ago", "Playnite", 3),
        ("Lantern Orbit", "Playnite  /  Never played", "Playnite", 2),
        ("Night Courier", "Playnite  /  Last played yesterday", "Playnite", 4),
    ]
    palette_window(cv, 70, 60, 860, 470, True, "orb", rows_dark, 0, "PLAYNITECMDPAL  /  GAMES")
    rows_light = [
        ("PlayniteCmdPal", "Search and launch games from your Playnite library", "Extension", "sym"),
        ("Windows Settings", "Open Windows Settings", "Command", "S"),
        ("Run command", "Run a command or open a file", "Command", ">"),
        ("Calculator", "Perform a quick calculation", "Command", "="),
    ]
    palette_window(cv, 1070, 60, 860, 470, False, "", rows_light, 0, "EXTENSIONS")
    cv.text(70, 570, "Dark: extension results with the symbol in the search field (32 px).", 16, "Medium", "#FFFFFF")
    cv.text(1070, 570, "Light: extension entry using the full 44 px symbol.", 16, "Medium", "#14181F")
    cv.text(W - 40, 625, "Presentation asset only. Game titles are fictional placeholders.", 13, "Regular",
            "#14181F", "ra")
    cv.save(PRE_DIR / "command-palette-mockup.png")


def build_qc_sheet():
    """Small-size and header-size QC render (kept in package for reference)."""
    W, H = 1700, 940
    cv = Canvas(W, H, WHITE, S=1)
    cv.rect(0, H // 2, W, H // 2, GRAPHITE)
    for row, theme in ((0, "light"), (1, "dark")):
        y0 = row * (H // 2)
        fg = GRAPHITE if row == 0 else WHITE
        sub = SLATE if row == 0 else "#9AA3B5"
        cv.text(30, y0 + 16, f"QC  /  {theme} background  /  16 to 512 px (512 shown at 50 %)", 18, "SemiBold", fg)
        x = 30
        for s in (16, 24, 32, 48, 64, 128, 256, 512):
            ds = s if s < 512 else 256
            cv.psvg(symbol_svg(theme, small=s <= SMALL_MAX), ds, x, y0 + 60 + (256 - ds))
            cv.text(x, y0 + 60 + 262, str(s), 14, "Medium", sub)
            x += ds + 28
        x = 30
        cv.text(30, y0 + 355, "Lockup at header heights 20, 24, 32, 40 px", 14, "Medium", sub)
        for hh in (20, 24, 32, 40):
            w_ = int(hh * lockup_aspect())
            cv.psvg(lockup_svg(theme), w_, x, y0 + 385)
            x += w_ + 50
        cv.psvg(compact_svg(theme), 220, 900, y0 + 385)
    cv.save(PRE_DIR / "qc-small-sizes.png")


def lockup_aspect():
    import re
    m = re.search(r'viewBox="0 0 ([\d.]+) ([\d.]+)"', lockup_svg("light"))
    return float(m.group(1)) / float(m.group(2))


# --------------------------------------------------------------------------- docs
def contrast_rows():
    pairs = [
        ("White on Launch Cobalt (symbol glyph on plate)", WHITE, COBALT),
        ("Launch Cobalt on White", COBALT, WHITE),
        ("Cobalt Deep on White (text-safe accent)", COBALT_DEEP, WHITE),
        ("Graphite on Mist", GRAPHITE, MIST),
        ("Slate on White", SLATE, WHITE),
        ("Cobalt Light on Graphite (dark theme accent)", COBALT_LIGHT, GRAPHITE),
        ("White on Graphite", WHITE, GRAPHITE),
        ("Launch Cobalt on Graphite", COBALT, GRAPHITE),
    ]
    out = []
    for name, a, b in pairs:
        r = contrast(a, b)
        grade = "AAA" if r >= 7 else "AA" if r >= 4.5 else "AA large / graphics (3:1)" if r >= 3 else "fails 3:1"
        out.append(f"| {name} | `{a}` on `{b}` | {r:.2f}:1 | {grade} |")
    return "\n".join(out)


def write_docs():
    pal_rows = "\n".join(
        f"| {n} | `{h}` | {hex_rgb(h)[0]}, {hex_rgb(h)[1]}, {hex_rgb(h)[2]} | {u} |" for n, h, u in PALETTE)
    write(BRAND_DIR / "colors.md", f"""# PlayniteCmdPal colour palette

| Name | HEX | RGB | Use |
|---|---|---|---|
{pal_rows}

## Theme mapping

| Element | Light background | Dark background |
|---|---|---|
| Symbol plate | `{COBALT}` | `{COBALT_LIGHT}` |
| Symbol chevron and selected cell | `#FFFFFF` | `#FFFFFF` |
| Symbol secondary cells | `{TINT}` | `#CFDDFF` |
| Wordmark "Playnite" | `{GRAPHITE}` | `#FFFFFF` |
| Wordmark "CmdPal" | `{COBALT}` | `{COBALT_LIGHT}` |
| Wide-tile background | n/a | `{GRAPHITE}` |

## Contrast (WCAG 2.x, computed by `scripts/generate-assets.py`)

| Pair | Colours | Ratio | Result |
|---|---|---|---|
{contrast_rows()}

Notes:
- Launch Cobalt carries the white glyph at roughly the 4.5:1 threshold. It is a graphic, not body text; use Cobalt Deep for text-size accent colour on light surfaces.
- The wordmark accent "CmdPal" switches to Cobalt Light on dark backgrounds to keep contrast above 4.5:1.
""")
    write(BRAND_DIR / "typography.md", """# PlayniteCmdPal typography

## Wordmark
- Typeface: **Inter SemiBold** (Inter 4.0, static TTF `fonts/Inter-SemiBold.ttf`).
- Licence: SIL Open Font License 1.1 (`LICENSES/Inter-OFL-1.1.txt`). Embedding, outlining and redistribution are permitted.
- The wordmark in every SVG is **outlined to vector paths**. No font is required to render it.
- Spelling is exactly `PlayniteCmdPal`. No spaces, hyphens or case changes.
- Tracking: -1.2 % (-0.012 em). Kerning: on (OpenType `kern`). Ligatures and contextual alternates: off.
- Two-tone treatment: "Playnite" in the text colour, "CmdPal" in the accent colour. Monochrome version uses one colour.
- Regenerate with `scripts/generate-assets.py`. Do not retype the wordmark.

## Product UI
- Use the Windows system font **Segoe UI Variable** inside the Command Palette extension and any Windows UI. It ships with Windows 11 and is **not** redistributed in this package.
- Fallback stack: `"Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif`.

## Documentation and marketing
- **Inter** Regular / Medium / SemiBold (included in `fonts/`).

## Scale (documentation, web)

| Role | Size / line | Weight |
|---|---|---|
| Display | 40 / 48 px | SemiBold |
| Title | 28 / 36 px | SemiBold |
| Subtitle | 20 / 28 px | SemiBold |
| Body | 16 / 24 px | Regular |
| Caption | 13 / 18 px | Medium |
""")
    write(BRAND_DIR / "brand-guidelines.md", """# PlayniteCmdPal brand guidelines

## 1. Concept
PlayniteCmdPal finds a game and launches it from the keyboard. The symbol joins two ideas in one rounded Windows-style plate:

1. **A command prompt chevron** (`>`): invoking a command. It also reads as a play marker, which is the only gaming reference.
2. **A 2 x 2 library grid**: the result list. One cell is brighter than the others: the selected result, ready to launch.

At 16 to 32 px the grid collapses to a single solid block, a text cursor beside the chevron (`>|`). The shape language is one stroke weight, one corner radius family and one colour.

No controller, magnifying glass, sparkle, gradient or mascot is used.

## 2. Construction
Authoritative geometry (256 x 256 canvas, full variant):

| Element | Geometry |
|---|---|
| Plate | 224 x 224 at (16, 16), corner radius 56 |
| Chevron | polyline (58,92) (96,128) (58,164), stroke 24, round caps and joins |
| Grid cells | 32 x 32, radius 8, gap 12, origin (120,90) |
| Selected cell | top-right cell, white; others Index Tint |
| Optical offset | glyph group translated +7 px on x to centre its visual mass |

Small variant (16 to 32 px): plate 240 x 240 at (8, 8), chevron stroke 38, one block 56 x 76 at (146,90), radius 14. Heavier weight, no tiny gaps.

## 3. Variants

| File | Use |
|---|---|
| `logo-symbol.svg`, `logo-symbol-light.svg` | Light backgrounds (default) |
| `logo-symbol-dark.svg` | Dark backgrounds |
| `logo-symbol-small*.svg` | 16 to 32 px rendering |
| `logo-monochrome.svg` | One-colour print, stamps, watermarks. Plate with glyph knocked out |
| `logo-glyph-cobalt.svg`, `logo-glyph-white.svg` | Plate-less glyph (unplated icons, tray) |
| `wordmark-horizontal*.svg` | Symbol + wordmark lockup |
| `wordmark-compact*.svg` | Wordmark only, tight crop, for headers and footers |

`-light` means "for use on light backgrounds". `-dark` means "for use on dark backgrounds".

## 4. Clear space
Keep a margin of **x** on all sides, where x = 1/4 of the symbol plate height (equal to the plate corner radius). In the horizontal lockup, x is measured from the plate.

## 5. Minimum size

| Asset | Minimum |
|---|---|
| Symbol, small variant | 16 px |
| Symbol, full variant | 40 px |
| Horizontal lockup | 120 px wide (24 px symbol height) |
| Compact wordmark | 110 px wide |
| Print symbol | 8 mm |

## 6. Light and dark
- Light backgrounds (white, Mist, light Mica): use the `light` files.
- Dark backgrounds (Graphite, dark Mica): use the `dark` files. The plate lightens to Cobalt Light so edge contrast stays above 3:1.
- Placing the light symbol on a similar blue is not allowed. Use the monochrome white version on Launch Cobalt.

## 7. Misuse
See `previews/logo-misuse.png`. Do not: stretch, recolour outside the palette, rotate, add shadows or glows, place on low-contrast or busy backgrounds, retype the wordmark, outline or rebuild the symbol, add controllers or other game motifs.

## 8. Relationship to other marks
PlayniteCmdPal is an independent extension. The symbol is original and does not reproduce or imitate Microsoft PowerToys, Windows or Playnite marks. Use the product name in text to refer to those products; do not use their logos inside this identity.
""")
    write(LIC_DIR / "THIRD-PARTY-ASSETS.md", """# Third-party assets

## Inter (typeface)
- Files: `fonts/Inter-Regular.ttf`, `fonts/Inter-Medium.ttf`, `fonts/Inter-SemiBold.ttf`
- Source: https://github.com/rsms/inter (release v4.0)
- Copyright (c) 2016 The Inter Project Authors
- Licence: SIL Open Font License 1.1. Full text: `LICENSES/Inter-OFL-1.1.txt`
- Use here: wordmark outlines (converted to vector paths), preview text.
- Restrictions: the font files may not be sold on their own and must keep the OFL text and copyright notice when redistributed. Converting glyphs to outlines for a logo is permitted.

## Segoe UI Variable
- Not included. Referenced in documentation only as the Windows system UI font. Licensed by Microsoft with Windows; do not redistribute.

## Python tooling (not redistributed in this package)
- cairosvg (LGPL-3.0), Pillow (HPND), fontTools (MIT), uharfbuzz (Apache-2.0). Used only by `scripts/generate-assets.py`.

## Everything else
- The symbol, lockups, icons and previews are original work for PlayniteCmdPal. Game titles and art in the mock-up are fictional placeholders.
""")


def inventory():
    rows = []
    for p in sorted(ROOT.rglob("*")):
        if p.is_file() and "__pycache__" not in p.parts and p.suffix != ".zip":
            rel = p.relative_to(ROOT).as_posix()
            rows.append(f"- `{rel}`" + (f"  ({p.stat().st_size:,} bytes)" if rel != "README.md" else ""))
    return "\n".join(rows)


def write_readme():
    body = f"""# PlayniteCmdPal brand assets

Brand identity and production assets for **PlayniteCmdPal**, a Microsoft PowerToys Command Palette extension that searches a Playnite game library and launches games.

Vector source: `svg/`. Rasters, ICO and previews are generated from it by `scripts/generate-assets.py`.

## Design summary
Rounded Windows-style plate in Launch Cobalt. Inside: a command-prompt chevron (also a play marker) and a 2 x 2 library grid with one highlighted cell (the selected result). Below 32 px the grid becomes a single block cursor for legibility. Wordmark: Inter SemiBold, outlined, two-tone.

## Folder map

| Path | Content |
|---|---|
| `svg/` | Authoritative vector logos (symbol, lockups, compact wordmark, monochrome, glyph-only, small-size variant) |
| `icons/png/app-icon-<size>.png` | Plated app icon at 16, 24, 32, 40, 48, 64, 128, 150, 256, 300, 310, 400, 512 px. 16 to 32 px use the small optical variant |
| `icons/png/app-icon-dark-<size>.png` | Dark-background plate variant |
| `icons/png/glyph/` | Transparent, plate-less glyphs (`glyph-cobalt-*`, `glyph-white-*`) |
| `icons/png/msix/` | Windows asset set using standard Windows naming (Square44x44Logo, Square150x150Logo, StoreLogo, Wide310x150Logo, targetsize, unplated) |
| `icons/png/` (named files) | `square-44x44-logo.png`, `square-150x150-logo.png`, `store-logo-50.png`, `wide-tile-310x150.png` |
| `icons/ico/PlayniteCmdPal.ico` | Multi-resolution icon: 16, 24, 32, 48, 64, 128 (BMP/DIB) and 256 (PNG) |
| `icons/ico/PlayniteCmdPal-dark.ico` | Same sizes, dark-background plate |
| `icons/ico/PlayniteCmdPal-small.ico` | 16, 24, 32, 48 only |
| `previews/` | `brand-sheet.png`, `app-icon-preview.png`, `command-palette-mockup.png`, `logo-misuse.png`, `qc-small-sizes.png` |
| `brand/` | `brand-guidelines.md`, `colors.md`, `typography.md` |
| `fonts/` | Inter Regular, Medium, SemiBold (OFL) used by the generator |
| `scripts/generate-assets.py` | Rebuilds every file in this package |
| `LICENSES/` | Third-party licence notes and Inter OFL text |

## SVG usage
- `-light` = for light backgrounds. `-dark` = for dark backgrounds. Unsuffixed files equal the light version.
- All SVGs: explicit `viewBox`, no raster images, no external references, no filters, transparent background. Wordmarks are outlined paths. The monochrome symbol is a single even-odd compound path (no masks); edit its `fill` to recolour.

## Windows / MSIX usage
- This package was produced **without access to the application repository or its `Package.appxmanifest`**. File names in `icons/png/msix/` follow Microsoft's standard asset naming conventions; they have **not** been checked against your manifest or project file. Map them to the manifest `Logo`, `Square44x44Logo`, `Square150x150Logo`, `Wide310x150Logo` and `StoreLogo` entries yourself.
- Square tiles carry the rounded plate with transparent corners. Set the manifest tile `BackgroundColor` to `transparent` to avoid a plate inside a plate.
- `Wide310x150Logo*` is full-bleed Graphite (`#14181F`) with the lockup. Use it only if the manifest declares a wide tile.
- `*_altform-unplated` files are white glyphs, `*_altform-lightunplated` are Cobalt glyphs, both without plate.
- No application source or manifest file was modified.

## Regenerate
```
pip install cairosvg Pillow fonttools uharfbuzz
python scripts/generate-assets.py
```
Output is deterministic apart from PNG compression metadata.

## Licensing
Original artwork. Typeface Inter under SIL OFL 1.1. See `LICENSES/THIRD-PARTY-ASSETS.md`.

## Known limitations
- Rasterisation uses cairosvg (no manual pixel hinting). The small variant compensates with heavier strokes; verify at 100 % scaling on a real display.
- Wordmark is outlined, so text is not editable in the SVG. Edit and rerun the generator to change it.
- Mock-ups use fictional game titles and a drawn approximation of the Command Palette UI, not a screenshot.
- Segoe UI Variable is referenced, not bundled.

## File inventory
{inventory()}
"""
    write(ROOT / "README.md", body)


# --------------------------------------------------------------------------- validate + zip
def validate():
    import xml.etree.ElementTree as ET
    problems = []
    for p in sorted(SVG_DIR.glob("*.svg")):
        try:
            root = ET.parse(p).getroot()
        except ET.ParseError as e:
            problems.append(f"{p.name}: XML error {e}")
            continue
        txt = p.read_text(encoding="utf-8")
        if "viewBox" not in root.attrib:
            problems.append(f"{p.name}: missing viewBox")
        for bad in ("<image", "http://www.w3.org/2000/svg#", "<filter", "data:image", "<script"):
            if bad in txt:
                problems.append(f"{p.name}: contains {bad}")
        import re
        for href in re.findall(r'href="([^"]+)"', txt):
            if not href.startswith("#"):
                problems.append(f"{p.name}: external href {href}")
        if p.stat().st_size < 200:
            problems.append(f"{p.name}: suspiciously small")
    for p in ROOT.rglob("*"):
        if p.is_file() and p.stat().st_size == 0:
            problems.append(f"{p.relative_to(ROOT)}: empty file")
    return problems


def make_zip():
    out = ROOT.parent / "PlayniteCmdPal-Brand-Assets.zip"
    if out.exists():
        out.unlink()
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for p in sorted(ROOT.rglob("*")):
            if p.is_file() and "__pycache__" not in p.parts:
                z.write(p, Path(ROOT.name) / p.relative_to(ROOT))
    return out


def main():
    build_svgs()
    build_png_ico()
    build_app_icon_preview()
    build_brand_sheet()
    build_misuse_sheet()
    build_mockup()
    build_qc_sheet()
    write_docs()
    write_readme()
    probs = validate()
    if probs:
        print("VALIDATION PROBLEMS:")
        print("\n".join(probs))
        sys.exit(1)
    print("validation ok")
    if "--no-zip" not in sys.argv:
        print(make_zip())


if __name__ == "__main__":
    main()
