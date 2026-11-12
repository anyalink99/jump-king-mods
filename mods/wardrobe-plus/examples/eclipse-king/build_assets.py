"""Build a native King reskin with a charge-driven solar visor.

Original artwork: Nexile. Requires the user's installed game atlas.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from king_mask import protect

from PIL import Image

# Share the native XNB reader with the bundled Ashen King authoring example
reader_spec = importlib.util.spec_from_file_location(
    'native_king_reader', Path(__file__).resolve().parents[1] / 'ashen-king/build_assets.py')
reader = importlib.util.module_from_spec(reader_spec)
sys.dont_write_bytecode = True
reader_spec.loader.exec_module(reader)

PALETTE = {
    # Obsidian plates: neutral near-black faces with narrow warm specular edges
    (0, 0, 0): (5, 5, 5),
    (18, 108, 184): (18, 18, 17), (18, 107, 184): (18, 18, 17),
    (139, 216, 236): (47, 46, 41), (215, 255, 246): (110, 103, 81),
    (255, 255, 255): (255, 234, 155),
    # Gold runs from nearly black recesses through amber to sharp pale highlights
    (97, 41, 19): (15, 13, 9), (133, 83, 39): (66, 43, 12),
    (213, 131, 59): (216, 141, 18), (229, 197, 104): (248, 185, 39),
    (89, 79, 31): (29, 23, 12), (174, 118, 26): (116, 65, 9),
    (226, 152, 22): (216, 132, 12), (255, 236, 50): (255, 211, 73),
    (108, 70, 37): (81, 48, 11), (189, 157, 65): (208, 136, 17),
    (69, 39, 27): (25, 20, 12), (193, 125, 66): (207, 124, 13),
    (32, 67, 98): (24, 24, 22), (158, 202, 214): (94, 87, 67),
    # the cape is charcoal instead of blue or purple
    (232, 39, 52): (46, 41, 33), (189, 0, 53): (25, 24, 21),
    (143, 24, 62): (18, 18, 17), (130, 18, 54): (14, 14, 13),
    (89, 11, 22): (8, 8, 8), (215, 48, 42): (55, 48, 35),
    (138, 24, 20): (25, 24, 21),
}

# Bounds of the original black visor opening, in each native 48 px pose
# Charge includes both bottom pixels of the T's stem. no helmet geometry moves
BEVEL_BOUNDS = {0: (22, 26, 26, 30), 1: (23, 25, 27, 29),
          2: (23, 26, 27, 30), 3: (23, 25, 27, 29),
          4: (21, 40, 26, 42), 5: (23, 20, 27, 25),
          6: (23, 22, 27, 26), 7: (24, 25, 28, 31),
          9: (22, 26, 26, 30), 10: (22, 28, 26, 32),
          11: (23, 25, 27, 29)}

# exact native opening pixels. White and blue bevels belong to the helmet,
# not to the opening. Tilted poses keep their asymmetric native geometry
VISOR_ROWS = {
    0: [(26,21,25),(27,22,25),(28,24,24),(29,24,24),(30,24,24)],
    1: [(25,22,26),(26,23,26),(27,25,25),(28,25,25),(29,25,25)],
    2: [(26,22,26),(27,23,26),(28,25,25),(29,25,25),(30,25,25)],
    3: [(25,22,26),(26,23,26),(27,25,25),(28,25,25),(29,25,25)],
    4: [(40,21,26),(41,23,24),(42,23,24)],
    5: [(20,22,26),(21,23,26),(22,25,25),(23,25,25),(24,25,25),(25,25,25)],
    6: [(22,22,26),(23,23,26),(24,25,25),(25,25,25),(26,25,25)],
    7: [(25,24,24),(26,25,26),(27,26,28),(28,25,26),(29,25,25),(30,25,25),(31,25,25)],
    8: [],  # the visor is hidden against the ground in the native splat pose
    9: [(26,21,25),(27,22,25),(28,24,24),(29,24,24),(30,24,24)],
    10: [(28,21,25),(29,22,25),(30,24,24),(31,24,24),(32,24,24)],
    11: [(25,22,26),(26,23,26),(27,25,25),(28,25,25),(29,25,25)],
    12: [(24,19,23),(25,18,19),(25,22,22),(26,18,18),(26,23,23)],
}

# each proposal changes material distribution as well as hue. Broad armor faces
# stay black, accents and highlights use distinct ramps instead of a flat tint
FINISHES = {
    'graphite': ((14,14,14),(43,43,42),(100,99,93),(183,181,166),(115,113,103),(66,65,60)),
    'platinum': ((13,14,14),(55,56,55),(163,166,161),(242,241,223),(216,219,213),(108,112,110)),
    'white-gold': ((17,15,12),(67,54,33),(186,159,99),(255,241,191),(225,203,143),(126,98,48)),
    'bronze': ((19,13,11),(76,37,22),(192,101,51),(248,186,122),(211,130,68),(118,58,31)),
    'garnet': ((18,10,11),(68,15,24),(157,28,48),(228,145,127),(183,43,59),(93,20,33)),
    'ivory': ((17,16,13),(55,51,42),(165,154,126),(250,240,207),(224,215,185),(100,91,69)),
}
ACTIVE_FINISH = 'ivory'


def finish_palette(name):
    palette = PALETTE.copy()
    if name == 'current':
        return palette
    shadow, mid, edge, glint, plate, bevel = FINISHES[name]
    for color in ((97,41,19),(69,39,27)):
        palette[color] = shadow
    for color in ((133,83,39),(108,70,37),(89,79,31)):
        palette[color] = mid
    for color in ((213,131,59),(174,118,26),(226,152,22),(193,125,66),(189,157,65)):
        palette[color] = edge
    for color in ((229,197,104),(255,236,50)):
        palette[color] = plate
    palette[(255,255,255)] = glint
    return palette


def recolor(source):
    output = source.copy()
    palette = finish_palette(ACTIVE_FINISH)
    output.putdata([(*palette.get(p[:3], p[:3]), p[3]) for p in source.getdata()])
    return output


def pose(source, key):
    output = recolor(source)
    # Opaque data mask: red=visor (255) or ray (128), green=reflection,
    # blue=charge fill threshold. Alpha stays opaque to keep data on upload
    mask = Image.new('RGBA', source.size, (0, 0, 0, 255))
    slit = [(x,y) for y,left,right in VISOR_ROWS[key] for x in range(left,right+1)]
    assert all(source.getpixel(p) == (0,0,0,255) for p in slit), f'Native visor changed in pose {key}'
    if not slit:
        return output, mask, (0, -22)
    x0, y0 = min(x for x,y in slit), min(y for x,y in slit)
    x1, y1 = max(x for x,y in slit), max(y for x,y in slit)
    # Gild only existing bevel pixels along the visor-side edge and chin
    # the broad helmet face stays black, so its gold rim reads as metal inlay
    bx0, by0, bx1, by1 = BEVEL_BOUNDS.get(key,(x0,y0,x1,y1))
    rim, chin = ((230,161,27),(166,96,12)) if ACTIVE_FINISH == 'current' else (FINISHES[ACTIVE_FINISH][4],FINISHES[ACTIVE_FINISH][5])
    for y in range(max(0, by0-4), min(47, by1+4)):
        for x in range(max(0, bx0-3), min(47, bx1+3)):
            if source.getpixel((x, y))[:3] != (139, 216, 236):
                continue
            right = source.getpixel((x+1, y))[:3]
            below = source.getpixel((x, y+1))[:3]
            if x >= bx1 and right == (18, 108, 184):
                output.putpixel((x, y), (*rim,255))
            elif y >= by1+1 and below == (0, 0, 0):
                output.putpixel((x, y), (*chin,255))
    if key == 4:
        assert len(slit) == 10, 'The complete charge visor must contain ten pixels'
    for x, y in slit:
        threshold = .12 + .5 * (y1-y) / max(1, y1-y0)
        threshold += .12 * abs(x-(x0+x1)/2) / max(1, (x1-x0)/2)
        mask.putpixel((x, y), (255, 0, round(threshold*255), 255))
        output.putpixel((x, y), (100, 43, 16, 255))
    for y in range(max(0, y0-2), min(48, y1+3)):
        for x in range(max(0, x0-2), min(48, x1+3)):
            if (x, y) in slit or source.getpixel((x, y))[3] == 0:
                continue
            distance = min(abs(x-sx)+abs(y-sy) for sx, sy in slit)
            if distance <= 2 and source.getpixel((x, y))[:3] != (0, 0, 0):
                mask.putpixel((x, y), (0, 130 if distance == 1 else 45, 0, 255))
    for x in (x0-4, x0-3, x1+3, x1+4):
        if 0 <= x < 48:
            mask.putpixel((x, y0), (128, 0, 0, 255))
    assert output.getchannel('A').tobytes() == source.getchannel('A').tobytes()
    return output, mask, ((x0+x1)/2-24, (y0+y1)/2-48)


def build(layout, output, native):
    if output.exists():
        raise ValueError('Use a new output directory; existing sources are preserved.')
    assets = output/'wardrobe'
    assets.mkdir(parents=True)
    original = reader.read_native_texture(native)
    regular = {f['key']: f for f in layout if f['group'] == 'Regular'}
    poses = {}
    for key, frame in regular.items():
        x, y, w, h = (frame[n] for n in ('x', 'y', 'width', 'height'))
        poses[key] = pose(original.crop((x, y, x+w, y+h)), key)
    states = [('idle', [0], .2), ('walk', [1, 3, 2, 3], .08),
              ('charge', [4], .2), ('rise', [5], .2), ('fall', [6], .2),
              ('land', [0], .06), ('splat', [8], .2),
              ('recover', [9, 11, 10, 11, 0], .09), ('lookUp', [12], .2)]
    atlas = Image.new('RGBA', (240, len(states)*48))
    mask = Image.new('RGBA', atlas.size, (0, 0, 0, 255))
    clips = []
    for row, (state, keys, duration) in enumerate(states):
        frames = []
        for col, key in enumerate(keys):
            sprite, data, head = poses[key]
            atlas.paste(sprite, (col*48, row*48))
            mask.paste(data, (col*48, row*48))
            frames.append(dict(x=col*48, y=row*48, width=48, height=48,
                               originX=24, originY=48, duration=([11/60,4/60,10/60,4/60][col] if state=='walk' else duration),
                               anchors=[dict(id='head', x=head[0], y=head[1]),
                                        dict(id='feet', x=0, y=0)]))
        clips.append(dict(state=state, texture='animations.png', transition=0,
                          loop=state not in ('land', 'splat', 'recover'), frames=frames))
        if state == 'walk':
            clips[-1]['nativeFrames'] = keys
    atlas.save(assets/'animations.png')
    mask.save(assets/'visor-mask.png')
    fallback = original.copy()
    for frame in layout:
        if frame['group'] in ('Regular','BabeCouple','Ending1Misc','NBPKing','OwlKing'):
            x, y, w, h = (frame[n] for n in ('x', 'y', 'width', 'height'))
            image = poses[frame['key']][0] if frame['group'] == 'Regular' else recolor(original.crop((x, y, x+w, y+h)))
            fallback.paste(image, (x, y))
    assert fallback.getchannel('A').tobytes() == original.getchannel('A').tobytes()
    fallback = protect(original, fallback, layout)
    fallback.save(output/'eclipse-king.png')
    particle = Image.new('RGBA', (3, 3))
    particle.putpixel((1, 1), (255, 231, 164, 255))
    particle.save(assets/'light.png')
    effects = [dict(id='release', kind='particles', texture='light.png', anchor='head',
                    count=4, speed=18, speedSpread=5, spread=180, gravity=0,
                    life=.16, lifeSpread=.03, size=1, endSize=.25,
                    color='#FFF4CBFF', endColor='#FFAA4400'),
               dict(id='solar-trace', kind='particles', texture='light.png', anchor='head',
                    count=0, rate=14, duration=.1, cooldown=.1, speed=0, speedSpread=0,
                    gravity=0, life=.18, lifeSpread=0, size=1, endSize=.25,
                    color='#FFCF77B0', endColor='#FFAA4400')]
    rules = [dict(id='release', trigger='jump', channels=[dict(channel='particles', mode='add', effects=['release'])]),
             dict(id='trace', trigger='rise', minCharge=.35, channels=[dict(channel='particles', mode='add', effects=['solar-trace'])])]
    manifest = dict(schema=1, id='eclipse-king', name='Eclipse King', version='1.0.5',
                    minimumWardrobe='2.0.6', defaultProfile='solar', effects=effects,
                    profiles=[dict(id='solar', name='Captive sun', rules=rules)],
                    materials=[dict(id='solar', kind='shader', shader='visor.mgfxo', mask='visor-mask.png')],
                    animations=[dict(id='king', item='NULL', material='solar', clips=clips)])
    (assets/'skin.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    (output/'set_settings.xml').write_text('<SetSettings><enabled>false</enabled><Reskins><Reskin><skin>NULL</skin><name>eclipse-king</name></Reskin></Reskins></SetSettings>\n', encoding='utf-8')
    preview = Image.new('RGB', (256, 256), '#0d101b')
    sprite = poses[0][0].resize((192, 192), Image.Resampling.NEAREST)
    preview.paste(sprite, (32, 22), sprite)
    preview.save(output/'workshop-preview.png')
    (output/'README.md').write_text((Path(__file__).parent/'README.md').read_text(encoding='utf-8'), encoding='utf-8')
    print(output)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--native', type=Path, required=True)
    parser.add_argument('--layout', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--finish', choices=['current', *FINISHES], default='ivory')
    args = parser.parse_args()
    ACTIVE_FINISH = args.finish
    build(json.loads(args.layout.read_text()), args.output, args.native)
