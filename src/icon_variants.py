# geo-11 made as large as the square allows, with the 3D glyph (stereo offset + lenticular stripes).
import os, sys
from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1]
S = 1024
NAVY = (22, 30, 70); CYAN = (70, 220, 255); RED = (255, 70, 90); BLUE = (70, 140, 255); WHITE = (245, 248, 255)

def font(size, bold=True):
    for name in (["segoeuib.ttf", "arialbd.ttf"] if bold else ["segoeui.ttf", "arial.ttf"]):
        p = os.path.join(os.environ["WINDIR"], "Fonts", name)
        if os.path.exists(p): return ImageFont.truetype(p, size)
    return ImageFont.load_default()

def fit_font(txt, width, start):
    size = start
    while size > 10:
        f = font(size)
        bb = ImageDraw.Draw(Image.new("RGBA", (S, S))).textbbox((0, 0), txt, font=f)
        if bb[2] - bb[0] <= width: return f
        size -= 8
    return font(size)

def base_with_stripes(y0, y1):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=S // 5, fill=255)
    im.paste(Image.new("RGBA", (S, S), NAVY), (0, 0), mask)
    stripes = Image.new("RGBA", (S, S), (0, 0, 0, 0)); sd_ = ImageDraw.Draw(stripes)
    for i in range(int(S * 0.08), int(S * 0.92), int(S * 0.08)):
        sd_.rectangle((i, y0, i + S * 0.028, y1), fill=(120, 200, 255, 38))
    stripes.putalpha(Image.composite(stripes.split()[3], Image.new("L", (S, S), 0), mask))
    im.alpha_composite(stripes)
    return im

def glyph3d(d, cx, cy, f, shift):
    d.text((cx - shift, cy), "3D", font=f, fill=RED, anchor="mm")
    d.text((cx + shift, cy), "3D", font=f, fill=BLUE, anchor="mm")
    d.text((cx, cy), "3D", font=f, fill=WHITE, anchor="mm")

# M: "geo-11" across the full width (cyan), 3D glyph below
def variant_m():
    im = base_with_stripes(S * 0.42, S * 0.92); d = ImageDraw.Draw(im)
    f = fit_font("geo-11", S * 0.90, int(S * 0.34))
    d.text((S / 2, S * 0.24), "geo-11", font=f, fill=CYAN, anchor="mm")
    glyph3d(d, S / 2, S * 0.66, font(int(S * 0.44)), S * 0.022)
    return im

# N: 3D glyph on top, "geo-11" across the full width inside a tall cyan bar
def variant_n():
    im = base_with_stripes(S * 0.08, S * 0.58); d = ImageDraw.Draw(im)
    glyph3d(d, S / 2, S * 0.33, font(int(S * 0.44)), S * 0.022)
    d.rounded_rectangle((S * 0.06, S * 0.62, S * 0.94, S * 0.92), radius=S * 0.06, fill=CYAN)
    f = fit_font("geo-11", S * 0.82, int(S * 0.30))
    d.text((S / 2, S * 0.77), "geo-11", font=f, fill=NAVY, anchor="mm")
    return im

# O: "geo-11" full width in white with the stereo offset (the name itself is the 3D glyph), small "3D" tag below
def variant_o():
    im = base_with_stripes(S * 0.14, S * 0.86); d = ImageDraw.Draw(im)
    f = fit_font("geo-11", S * 0.92, int(S * 0.36))
    d.text((S / 2 - S * 0.018, S * 0.44), "geo-11", font=f, fill=RED, anchor="mm")
    d.text((S / 2 + S * 0.018, S * 0.44), "geo-11", font=f, fill=BLUE, anchor="mm")
    d.text((S / 2, S * 0.44), "geo-11", font=f, fill=WHITE, anchor="mm")
    d.rounded_rectangle((S * 0.33, S * 0.66, S * 0.67, S * 0.86), radius=S * 0.05, fill=CYAN)
    d.text((S / 2, S * 0.76), "3D", font=font(int(S * 0.16)), fill=NAVY, anchor="mm")
    return im

variants = {"M_geo11_big_top": variant_m(), "N_3D_geo11_big_bar": variant_n(), "O_geo11_stereo_3Dtag": variant_o()}
sizes = [256, 48, 32, 16]; pad = 24
sheet = Image.new("RGBA", (pad + 256 + pad + sum(s + pad for s in sizes[1:]) + 40, pad + len(variants) * (256 + pad + 28)), (255, 255, 255, 255))
sd = ImageDraw.Draw(sheet); lf = font(22, bold=False); y = pad
for name, im in variants.items():
    im.save(os.path.join(OUT, "icon_" + name + ".png"))
    sd.text((pad, y - 2), name, font=lf, fill=(60, 60, 60))
    x = pad
    for s in sizes:
        small = im.resize((s, s), Image.LANCZOS)
        sheet.alpha_composite(small, (x, y + 28 + ((256 - s) // 2 if s != 256 else 0)))
        x += s + pad
    y += 256 + pad + 28
sheet.save(os.path.join(OUT, "icon_variants5_sheet.png"))
print("ok")
