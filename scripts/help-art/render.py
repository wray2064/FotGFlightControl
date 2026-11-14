"""Renders the Flight Control wheel, manual power and shield allocation art for the help pages.

The drawing calls come from the real Lua (zzz_fotg_wheel-sct.lua) run with a recording graphics layer
(extract.cjs); this script replays them at high resolution, so the pictures are exactly what the code draws.

  python render.py --metrics font-metrics.json        # glyph widths, needed by extract.cjs
  node extract.cjs <path-to-fengari> draw-calls.json font-metrics.json
  python render.py draw-calls.json <output-folder>

Needs Pillow and the Segoe UI Bold font (Windows). Output is transparent PNG (full quality) and WebP.
"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

FONT_FILE = r'C:\Windows\Fonts\segoeuib.ttf'
FONT_PX = 18          # glyph size in game pixels at 1080p; the game's text line height is HEIGHT
HEIGHT = 19
FINAL = 2             # final pixels per game pixel
SS = 2                # supersampling on top of that
K = FINAL * SS

def metrics(path):
    f = ImageFont.truetype(FONT_FILE, FONT_PX * 20)
    widths = {str(b): f.getlength(chr(b)) / 20 for b in range(32, 127)}
    json.dump({'px': FONT_PX, 'height': HEIGHT, 'widths': widths}, open(path, 'w'))
    print('wrote', path)

def render(scene, out_dir):
    name, calls = scene['name'], scene['calls']
    cw, ch = scene['canvas']
    cx, cy = scene['center']
    W, H = int(cw * K), int(ch * K)
    ox, oy = cx - cw / 2, cy - ch / 2
    font = ImageFont.truetype(FONT_FILE, int(FONT_PX * K))
    base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    X = lambda v: (v - ox) * K
    Y = lambda v: (v - oy) * K

    def translucent(shape):
        layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        shape(ImageDraw.Draw(layer))
        return Image.alpha_composite(base, layer)

    for c in calls:
        c = list(c)
        for i in range(len(c) - 4, len(c)): c[i] = int(round(c[i]))     # the colour is always the last four values
        kind = c[0]
        if kind == 'c':
            _, r, x, y, filled, R, G, B, A = c
            box = [X(x - r), Y(y - r), X(x + r), Y(y + r)]
            if filled:
                if A < 255: base = translucent(lambda dd: dd.ellipse(box, fill=(R, G, B, A)))
                else: d.ellipse(box, fill=(R, G, B, A))
            else:
                d.ellipse(box, outline=(R, G, B, A), width=int(1.5 * K))
        elif kind == 'l':
            _, x1, y1, x2, y2, R, G, B, A = c
            long_line = ((x2 - x1) ** 2 + (y2 - y1) ** 2) ** 0.5 > 20      # spokes; arcs are drawn as short pieces
            # Arc strokes are five 1 px lines side by side in the game, so they must overlap here.
            w = int(1.5 * K) if long_line else int(1.4 * K)
            d.line([X(x1), Y(y1), X(x2), Y(y2)], fill=(R, G, B, A), width=w)
        elif kind == 'r':
            _, x1, y1, x2, y2, filled, R, G, B, A = c
            box = [X(x1), Y(y1), X(x2), Y(y2)]
            if filled:
                if A < 255: base = translucent(lambda dd: dd.rectangle(box, fill=(R, G, B, A)))
                else: d.rectangle(box, fill=(R, G, B, A))
            else:
                d.rectangle(box, outline=(R, G, B, A), width=int(1.4 * K))
        elif kind == 's':
            _, text, x, y, R, G, B, A = c
            d.text((X(x), Y(y)), text, font=font, fill=(R, G, B, A), anchor='la')
        d = ImageDraw.Draw(base)

    final = base.resize((int(cw * FINAL), int(ch * FINAL)), Image.LANCZOS)
    os.makedirs(out_dir, exist_ok=True)
    final.save(os.path.join(out_dir, name + '.png'), optimize=True)
    final.save(os.path.join(out_dir, name + '.webp'), quality=92, method=6)
    return final

if __name__ == '__main__':
    if sys.argv[1] == '--metrics':
        metrics(sys.argv[2])
    else:
        data = json.load(open(sys.argv[1]))
        for scene in data['scenes']:
            im = render(scene, sys.argv[2])
            print(scene['name'], im.size)
