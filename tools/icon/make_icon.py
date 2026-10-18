"""Builds src\\app.ico: Geo Gaming Hub's icon, the green rounded square with a "G" that the window header also shows
(Theme.cs Brand). Every size is drawn on its own (8x supersampled) so the small ones stay sharp; sizes below 256 are
32-bit DIB entries, 256 is PNG.

usage: python make_icon.py [preview.png]
"""
import io
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'src', 'app.ico')
FONT = r'C:\Windows\Fonts\segoeuib.ttf'
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

TOP, BOTTOM = (0x4B, 0xE0, 0x9A), (0x2C, 0xB8, 0x72)   # Theme.Accent (#3DD68C) as a soft vertical gradient
EDGE = (0x1E, 0x8F, 0x57)
INK = (0x0F, 0x2A, 0x1B)                                 # Theme.OnAccent
SS = 8


def draw(size):
    s = size * SS
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    m = max(1, round(size * 0.06)) * SS if size >= 24 else SS // 2
    box = (m, m, s - m - 1, s - m - 1)
    r = int((box[2] - box[0]) * 0.22)

    mask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, r, fill=255)
    grad = Image.new('RGBA', (s, s))
    gd = ImageDraw.Draw(grad)
    for y in range(s):
        t = min(1.0, max(0.0, (y - box[1]) / max(1, box[3] - box[1])))
        gd.line([(0, y), (s, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)) + (255,))
    img.paste(grad, (0, 0), mask)
    if size >= 32:
        ImageDraw.Draw(img).rounded_rectangle(box, r, outline=EDGE + (255,), width=max(SS, s // 64))

    # the letter, centred on its ink box; from 32 px a faint offset copy behind it hints at the second eye (stereo 3D)
    inner = box[2] - box[0]
    font = ImageFont.truetype(FONT, int(inner * 0.78))
    l, t, rr, b = font.getbbox('G')
    x = (s - (rr - l)) / 2 - l
    y = (s - (b - t)) / 2 - t
    d = ImageDraw.Draw(img)
    if size >= 32:
        off = inner * 0.055
        ghost = Image.new('RGBA', (s, s), (0, 0, 0, 0))
        ImageDraw.Draw(ghost).text((x - off, y), 'G', font=font, fill=(255, 255, 255, 110))
        img = Image.alpha_composite(img, Image.composite(ghost, Image.new('RGBA', (s, s), (0, 0, 0, 0)), mask))
        d = ImageDraw.Draw(img)
        x += off * 0.35
    d.text((x, y), 'G', font=font, fill=INK + (255,))
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
        for n, im in images:
            for row in (0, 1):
                sheet.alpha_composite(im, (x, 16 + row * (256 + 32) + (256 - n)))
            x += n + 16
        sheet.save(sys.argv[1])


if __name__ == '__main__':
    main()
