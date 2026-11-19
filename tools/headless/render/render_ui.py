#!/usr/bin/env python3
"""Turns the UI dumps (Sim --render) into PNG pictures.

usage: render_ui.py <folder with .json dumps> [out folder]

What it draws: every rectangle where the game's layout code puts it, with its colour and text. Pictures (sprites) are plain boxes, the
font is a similar one (Liberation Sans, like Unity's default), and the world behind the UI is a flat green.
"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

FONT = '/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf'
BOLD = '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'
BACKGROUND = (92, 140, 70, 255)

_fonts = {}
def font(size, bold=False):
    key = (round(size), bold)
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(BOLD if bold else FONT, max(6, round(size)))
    return _fonts[key]

def wrap(draw, text, fnt, width):
    lines = []
    for paragraph in text.split('\n'):
        words = paragraph.split(' ')
        line = ''
        for word in words:
            trial = (line + ' ' + word).strip()
            if draw.textlength(trial, font=fnt) <= width or not line:
                line = trial
            else:
                lines.append(line); line = word
        lines.append(line)
    return lines

def draw_text(img, item, box, clip):
    t = item['text']
    x, y, w, h = box
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    size = t['size']
    if t['auto']:
        size = t['max']
        while size > t['min']:
            lines = wrap(d, t['text'], font(size, t['bold']), w)
            if len(lines) * size * 1.2 <= h and max(d.textlength(l, font=font(size, t['bold'])) for l in lines) <= w:
                break
            size -= 1
    fnt = font(size, t['bold'])
    lines = wrap(d, t['text'], fnt, w)
    line_h = size * 1.2
    total = line_h * len(lines)
    align = t['align']
    v, hh = align & ~7 & 0xFF00 | 0, align & 7
    vert = (align >> 8) & 0xF
    if vert & 1: top = y + h - total          # (image y is up in unity; here box is already top-left based)
    # TMP vertical: 256 top, 512 middle, 1024 bottom
    vflag = align & 0xFF00
    if vflag & 0x100: ty = y
    elif vflag & 0x400: ty = y + h - total
    else: ty = y + (h - total) / 2
    for i, line in enumerate(lines):
        lw = d.textlength(line, font=fnt)
        if hh & 2: tx = x + (w - lw) / 2
        elif hh & 4: tx = x + w - lw
        else: tx = x
        d.text((tx, ty + i * line_h), line, font=fnt, fill=tuple(t['rgba']))
    if clip:
        mask = Image.new('L', img.size, 0)
        ImageDraw.Draw(mask).rectangle(clip, fill=255)
        layer.putalpha(Image.composite(layer.getchannel('A'), Image.new('L', img.size, 0), mask))
    img.alpha_composite(layer)

def render(path, out):
    data = json.load(open(path))
    W, H = data['width'], data['height']
    img = Image.new('RGBA', (W, H), BACKGROUND)
    for item in data['items']:
        x, y, w, h = item['x'], item['y'], item['w'], item['h']
        top = H - (y + h)                       # unity's y goes up
        box = (x, top, w, h)
        clip = None
        if 'clip' in item:
            cx, cy, cw, ch = item['clip']
            clip = (cx, H - (cy + ch), cx + cw, H - cy)
        if 'image' in item and w > 0 and h > 0:
            im = item['image']
            layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
            d = ImageDraw.Draw(layer)
            rgba = tuple(im['rgba'])
            if im['sprite'] and im['sliced']:
                d.rounded_rectangle((x, top, x + w, top + h), radius=min(10, h / 4, w / 4), fill=rgba)
            else:
                d.rectangle((x, top, x + w, top + h), fill=rgba)
            if im['button']:
                d.rounded_rectangle((x, top, x + w, top + h), radius=min(10, h / 4, w / 4), outline=(60, 60, 60, 255), width=1)
            if clip:
                mask = Image.new('L', img.size, 0)
                ImageDraw.Draw(mask).rectangle(clip, fill=255)
                layer.putalpha(Image.composite(layer.getchannel('A'), Image.new('L', img.size, 0), mask))
            img.alpha_composite(layer)
        if 'text' in item and w > 0 and h > 0:
            draw_text(img, item, box, clip)
    img.convert('RGB').save(out)

def main():
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else src
    os.makedirs(dst, exist_ok=True)
    for name in sorted(os.listdir(src)):
        if name.endswith('.json') and name != 'scene_ui.json':
            out = os.path.join(dst, name[:-5] + '.png')
            render(os.path.join(src, name), out)
            print('wrote', out)

if __name__ == '__main__':
    main()
