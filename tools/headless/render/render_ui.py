#!/usr/bin/env python3
"""Turns the UI dumps (Sim --render) into PNG pictures.

usage: render_ui.py <folder with .json dumps> [out folder]

What it draws: every rectangle where the game's layout code puts it, with its colour and text. Pictures (sprites) are plain boxes, the
font is a similar one (Liberation Sans, like Unity's default), and the world behind the UI is a flat green.
"""
import json, os, re, sys
from PIL import Image, ImageDraw, ImageFont

ASSETS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../Assets/_Project'))

FONT = '/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf'
BOLD = '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'
BACKGROUND = (92, 140, 70, 255)

# --- The project's pictures: "guid:fileID" -> the part of the texture file that sprite is -----------------------------------------------

_textures = None   # guid -> (path, meta text)
_sprite_cache = {}

def _scan():
    global _textures
    _textures = {}
    for root, _, files in os.walk(os.path.join(ASSETS, 'Art')):
        for name in files:
            if name.endswith('.meta') and name[:-5].lower().endswith(('.png', '.jpg', '.jpeg')):
                meta = open(os.path.join(root, name), encoding='utf8').read()
                guid = re.search(r'(?m)^guid: ([0-9a-f]{32})', meta)
                if guid:
                    _textures[guid.group(1)] = (os.path.join(root, name[:-5]), meta)

def sprite(reference):
    """The picture (RGBA) of a sprite reference, or None when it cannot be found."""
    if not reference:
        return None
    if reference in _sprite_cache:
        return _sprite_cache[reference]
    if _textures is None:
        _scan()
    guid, _, file_id = reference.partition(':')
    result = None
    if guid in _textures:
        path, meta = _textures[guid]
        image = Image.open(path).convert('RGBA')
        crop = None
        for block in re.split(r'(?m)^    - serializedVersion: 2\n', meta)[1:]:
            ident = re.search(r'internalID: (-?\d+)', block)
            rect = re.search(r'x: (\d+)\n\s+y: (\d+)\n\s+width: (\d+)\n\s+height: (\d+)', block)
            if ident and rect and ident.group(1) == file_id:
                x, y, w, h = map(int, rect.groups())
                crop = (x, image.height - (y + h), x + w, image.height - y)   # unity's y goes up
                break
        result = image.crop(crop) if crop else image
    _sprite_cache[reference] = result
    return result

def tint(image, rgba):
    r, g, b, a = [c / 255 for c in rgba]
    channels = image.split()
    return Image.merge('RGBA', (channels[0].point(lambda v: v * r), channels[1].point(lambda v: v * g), channels[2].point(lambda v: v * b), channels[3].point(lambda v: v * a)))

def fit(image, w, h, keep_aspect):
    if not keep_aspect:
        return image.resize((max(1, round(w)), max(1, round(h))), Image.LANCZOS), 0, 0
    scale = min(w / image.width, h / image.height)
    nw, nh = max(1, round(image.width * scale)), max(1, round(image.height * scale))
    return image.resize((nw, nh), Image.LANCZOS), (w - nw) / 2, (h - nh) / 2

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

GRASS = '__grass__'

def grass_picture():
    for name in ('Tiles/GrassBackground.png',):
        path = os.path.join(ASSETS, 'Art/Sprites', name)
        if os.path.exists(path):
            return Image.open(path).convert('RGBA')
    return None

def draw_world(img, data):
    """The ground, the Map's fog, roads, buildings and characters behind the UI (cells are one world unit)."""
    cam = data.get('camera')
    items = data.get('world') or []
    if not cam or not items:
        return
    W, H = img.size
    cx, cy, ortho = cam
    ppu = H / (2 * ortho)
    def screen(x, y):
        return W / 2 + (x - cx) * ppu, H / 2 - (y - cy) * ppu
    grass = grass_picture()
    tile = grass.resize((max(1, round(ppu)), max(1, round(ppu))), Image.NEAREST) if grass else None
    for it in items:
        if it['kind'] != 'ground':
            continue
        sx, sy = screen(it['x'] - 0.5, it['y'] + 0.5)
        if tile:
            img.paste(tile, (round(sx), round(sy)))
        if it.get('road'):
            road = sprite(it['road'])
            if road:
                img.alpha_composite(road.resize((max(1, round(ppu)), max(1, round(ppu))), Image.LANCZOS), (round(sx), round(sy)))
    for it in items:
        if it['kind'] == 'overlay':
            sx, sy = screen(it['x'] - 0.5, it['y'] + 0.5)
            layer = Image.new('RGBA', (max(1, round(ppu)), max(1, round(ppu))), tuple(it['rgba']))
            img.alpha_composite(layer, (round(sx), round(sy)))
    import base64
    pictures = {it['id']: Image.frombytes('RGBA', (it['size'], it['size']), base64.b64decode(it['rgba'])).transpose(Image.FLIP_TOP_BOTTOM) for it in items if it['kind'] == 'decorSprite'}
    movers = [it for it in items if it['kind'] in ('building', 'character', 'decor')]
    for it in sorted(movers, key=lambda i: -i['sortY']):      # further up the screen first, so lower things stand in front
        if it['kind'] == 'decor':
            base = pictures[it['id']]
            size = max(1, round(ppu * it['scale']))
            pic = base.resize((size, size), Image.NEAREST)
            sx, sy = screen(it['x'], it['y'])               # the picture stands on its bottom centre
            paste_clipped(img, pic, round(sx - size / 2), round(sy - size))
            continue
        picture = sprite(it.get('ref'))
        if it['kind'] == 'building':
            w, h = it['w'] * ppu, it['h'] * ppu
            if picture is None:
                picture = Image.new('RGBA', (10, 10), (200, 200, 200, 255))
            pic, ox, oy = fit(picture, w, h, False)
            sx, sy = screen(it['x'] - it['w'] / 2, it['y'] + it['h'] / 2)
        else:
            h = it['h'] * ppu
            if picture is None:
                picture = Image.new('RGBA', (10, 20), (200, 80, 80, 255))
            w = h * picture.width / picture.height
            pic, ox, oy = fit(picture, w, h, True)
            sx, sy = screen(it['x'], it['y'])
            sx -= w / 2; sy -= h / 2
        img.alpha_composite(pic, (max(0, round(sx + ox)), max(0, round(sy + oy)))) if sx + ox >= 0 and sy + oy >= 0 else paste_clipped(img, pic, round(sx + ox), round(sy + oy))

def paste_clipped(img, pic, x, y):
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    layer.paste(pic, (x, y))
    img.alpha_composite(layer)

def render(path, out):
    data = json.load(open(path))
    W, H = data['width'], data['height']
    img = Image.new('RGBA', (W, H), BACKGROUND)
    draw_world(img, data)
    for item in data['items']:
        x, y, w, h = item['x'], item['y'], item['w'], item['h']
        top = H - (y + h)                       # unity's y goes up
        box = (x, top, w, h)
        clip = None
        if 'clip' in item:
            cx, cy, cw, ch = item['clip']
            cw, ch = max(0, cw), max(0, ch)
            clip = (cx, H - (cy + ch), cx + cw, H - cy)
        if 'image' in item and w > 0 and h > 0:
            im = item['image']
            layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
            d = ImageDraw.Draw(layer)
            rgba = tuple(im['rgba'])
            picture = sprite(im.get('ref'))
            if picture is not None:
                pic, ox, oy = fit(tint(picture, rgba), w, h, im.get('fit', False))
                if clip:
                    pass
                layer.paste(pic, (round(x + ox), round(top + oy)))
            elif im['sprite'] and im['sliced']:
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
