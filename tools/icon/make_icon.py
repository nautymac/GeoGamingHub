"""Builds src\\app.ico: Geo Gaming Hub's icon "SBS split screen" - a monitor split into left / right halves (side by
side 3D) with a cube standing across the split, on a charcoal rounded square. Every size is drawn on its own
(8x supersampled) so the small ones stay sharp; sizes below 256 are 32-bit DIB entries, 256 is PNG.

usage: python make_icon.py [preview.png]
"""
import io
import math
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'src', 'app.ico')
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SS = 8

BG_TOP, BG_BOT, BG_EDGE = (0x4C, 0x4B, 0x52), (0x2B, 0x2A, 0x30), (0x5C, 0x5B, 0x63)
BEZEL, STAND = (0xD2, 0xE2, 0xE8), (0xB9, 0xCC, 0xD4)
SCREEN_L, SCREEN_R = (0x63, 0xA8, 0x9C), (0x55, 0x76, 0xC8)   # teal (left eye) -> blue (right eye)
CUBE_TOP, CUBE_LEFT, CUBE_RIGHT = (0xEC, 0xF7, 0xFA), (0x7E, 0xC6, 0xB2), (0x5A, 0x8C, 0xD4)


def gradient(s, box, c0, c1, horizontal):
    g = Image.new('RGBA', (s, s))
    d = ImageDraw.Draw(g)
    a, b = (box[0], box[2]) if horizontal else (box[1], box[3])
    for i in range(s):
        t = min(1.0, max(0.0, (i - a) / max(1, b - a)))
        col = tuple(int(x + (y - x) * t) for x, y in zip(c0, c1)) + (255,)
        d.line([(i, 0), (i, s)] if horizontal else [(0, i), (s, i)], fill=col)
    return g


def draw(size):
    s = size * SS
    small = size < 32
    m = (0.5 if small else max(1, round(size * 0.04))) * SS
    box = (m, m, s - m - 1, s - m - 1)
    W = box[2] - box[0]
    X = lambda f: box[0] + f * W
    Y = lambda f: box[1] + f * W

    # charcoal rounded square
    mask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, int(W * 0.235), fill=255)
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    img.paste(gradient(s, box, BG_TOP, BG_BOT, False), (0, 0), mask)
    if not small:
        ImageDraw.Draw(img).rounded_rectangle(box, int(W * 0.235), outline=BG_EDGE + (255,), width=max(SS, s // 96))

    # monitor + stand on their own layer, then the split cut through it
    mon = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    md = ImageDraw.Draw(mon)
    t = W * (0.05 if small else 0.036)
    outer = (X(0.10), Y(0.215), X(0.90), Y(0.785))
    md.rounded_rectangle(outer, int(W * 0.04), fill=BEZEL + (255,))
    screen = (outer[0] + t, outer[1] + t, outer[2] - t, outer[3] - t)
    smask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(smask).rounded_rectangle(screen, int(W * 0.015), fill=255)
    mon.paste(gradient(s, screen, SCREEN_L, SCREEN_R, True), (0, 0), smask)
    if small:
        md.rectangle((X(0.40), Y(0.785), X(0.60), Y(0.86)), fill=STAND + (255,))
        md.rectangle((X(0.24), Y(0.86), X(0.76), Y(0.92)), fill=STAND + (255,))
    else:
        md.rectangle((X(0.42), Y(0.785), X(0.58), Y(0.845)), fill=STAND + (255,))
        md.rounded_rectangle((X(0.28), Y(0.84), X(0.72), Y(0.875)), int(W * 0.017), fill=BEZEL + (255,))
    gap = W * (0.045 if small else 0.03)
    cut = Image.new('L', (s, s), 255)
    ImageDraw.Draw(cut).rectangle((s / 2 - gap / 2, 0, s / 2 + gap / 2, s), fill=0)
    mon.putalpha(Image.composite(mon.getchannel('A'), Image.new('L', (s, s), 0), cut))
    img = Image.alpha_composite(img, mon)

    # cube across the split, with a soft shadow on the screen
    cx, cy, R = X(0.5), Y(0.485), W * (0.19 if small else 0.165)
    c30 = math.cos(math.radians(30))
    top, ur, lr = (cx, cy - R), (cx + R * c30, cy - R / 2), (cx + R * c30, cy + R / 2)
    bot, ll, ul, c = (cx, cy + R), (cx - R * c30, cy + R / 2), (cx - R * c30, cy - R / 2), (cx, cy)
    if size >= 48:
        sh = Image.new('RGBA', (s, s), (0, 0, 0, 0))
        ImageDraw.Draw(sh).polygon([(p[0] + R * 0.12, p[1] + R * 0.18) for p in (top, ur, lr, bot, ll, ul)], fill=(10, 20, 40, 120))
        img = Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(R * 0.12)))
    d = ImageDraw.Draw(img)
    d.polygon([top, ur, c, ul], fill=CUBE_TOP + (255,))
    d.polygon([ul, c, bot, ll], fill=CUBE_LEFT + (255,))
    d.polygon([c, ur, lr, bot], fill=CUBE_RIGHT + (255,))
    return img.resize((size, size), Image.LANCZOS)


def dib(img):
    w, h = img.size
    px = img.tobytes('raw', 'BGRA')
    rows = [px[i * w * 4:(i + 1) * w * 4] for i in range(h)]
    mask_row = ((w + 31) // 32) * 4
    data = b''.join(reversed(rows)) + b'\0' * (mask_row * h)   # bottom-up colour rows, empty AND mask (alpha is used)
    return struct.pack('<IiiHHIIiiII', 40, w, h * 2, 1, 32, 0, len(data), 0, 0, 0, 0) + data


def main():
    images = [(n, draw(n)) for n in SIZES]
    blobs = []
    for n, im in images:
        if n >= 256:
            b = io.BytesIO(); im.save(b, 'PNG'); blobs.append(b.getvalue())
        else:
            blobs.append(dib(im))
    head = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b''
    for (n, _), blob in zip(images, blobs):
        entries += struct.pack('<BBBBHHII', n % 256, n % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    with open(OUT, 'wb') as f:
        f.write(head + entries + b''.join(blobs))
    print('wrote', os.path.normpath(OUT), [n for n, _ in images])
    if len(sys.argv) > 1:   # preview: every size on dark and light backgrounds
        w = sum(n + 16 for n in SIZES) + 16
        sheet = Image.new('RGBA', (w, 2 * (256 + 32)), (0x25, 0x2A, 0x31, 255))
        ImageDraw.Draw(sheet).rectangle((0, 256 + 32, w, 2 * (256 + 32)), fill=(0xF3, 0xF3, 0xF3, 255))
        x = 16
        for n, im in reversed(images):
            for row in (0, 1):
                sheet.alpha_composite(im, (x, 16 + row * (256 + 32) + (256 - n)))
            x += n + 16
        sheet.save(sys.argv[1])


if __name__ == '__main__':
    main()
