"""Reskin the installed original King while preserving native pixel geometry.

Usage: python build_assets.py --layout native-layout.json --output NEW_SOURCE
Requires the installed game atlas. Original King artwork is by Nexile; sounds are synthesized.
"""
from __future__ import annotations

import argparse
import json
import math
import random
import struct
import wave
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from king_mask import protect

from PIL import Image, ImageDraw

INK = '#100f19'
SHADOW = '#252330'
IRON = '#464450'
EDGE = '#77707a'
ASH = '#b8a2a0'
EMBER = '#ea5a25'
GOLD = '#ffc05a'
WHITE = '#ffe2a1'


NATIVE = None
REGULAR = {}


def read_native_texture(path: Path) -> Image.Image:
    """Read the game's uncompressed Color XNB without redistributing its source."""
    import io
    stream = io.BytesIO(path.read_bytes())
    if stream.read(6) != b'XNBw\x05\x00':
        raise ValueError('Expected a Windows uncompressed XNB v5 texture')
    size = struct.unpack('<I', stream.read(4))[0]
    if size != len(stream.getbuffer()):
        raise ValueError('Invalid XNB size')
    def integer():
        result = shift = 0
        while True:
            value = stream.read(1)[0]
            result |= (value & 127) << shift
            if value < 128:
                return result
            shift += 7
            if shift > 28:
                raise ValueError('Invalid XNB integer')
    readers = []
    for _ in range(integer()):
        readers.append(stream.read(integer()).decode())
        stream.read(4)
    if integer() != 0 or not readers[integer()-1].startswith('Microsoft.Xna.Framework.Content.Texture2DReader'):
        raise ValueError('Expected an unshared Texture2D')
    fmt, width, height, levels, count = struct.unpack('<iiiii', stream.read(20))
    if fmt != 0 or levels < 1 or count != width*height*4:
        raise ValueError('Expected Color texture pixels')
    return Image.frombytes('RGBA', (width, height), stream.read(count))


def recolor(image: Image.Image, phase: float = 0, charge: bool = False) -> Image.Image:
    # keep every source alpha value, contour, joint, face and equipment landmark
    colors = {
        (0, 0, 0): (15, 13, 18),
        (18, 108, 184): (65, 57, 61), (18, 107, 184): (65, 57, 61),
        (139, 216, 236): (149, 136, 126), (215, 255, 246): (222, 208, 183),
        (255, 255, 255): (249, 232, 192),
        (97, 41, 19): (43, 27, 27), (133, 83, 39): (80, 46, 36),
        (213, 131, 59): (155, 81, 42),
        (226, 152, 22): (224, 78, 27), (255, 236, 50): (255, 173, 66),
        (174, 118, 26): (173, 61, 31), (89, 79, 31): (72, 37, 30),
    }
    output = image.copy(); pixels = output.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if not a:
                continue
            color = colors.get((r, g, b), (r, g, b))
            # Ember fissures occupy existing bright armor pixels, never new silhouette pixels
            fissure = (r, g, b) in ((18,108,184),(139,216,236)) and ((x*7+y*11)%43 in (0,1))
            if fissure:
                heat = .5 + .5*math.sin(phase*math.tau + y*.45)
                color = (255, round(80+100*heat+(25 if charge else 0)), round(22+28*heat))
            pixels[x, y] = (*color, a)
    return output


def king(state: str, phase: float) -> Image.Image:
    poses = {'idle':[0], 'walk':[1,3,2,3], 'charge':[4], 'rise':[5],
             'fall':[6], 'land':[0], 'splat':[8],
             'recover':[9,11,10,11,0], 'lookUp':[12]}
    keys = poses[state]; key = keys[min(len(keys)-1,int(phase*len(keys)))]
    f = REGULAR[key]
    return recolor(NATIVE.crop((f['x'],f['y'],f['x']+f['width'],f['y']+f['height'])), phase, state=='charge')


def audio(path: Path, kind: str, length: float) -> None:
    rate = 22050
    rng = random.Random(127 + len(kind))
    samples = []
    low = 0.0
    for i in range(int(rate * length)):
        t = i / rate
        p = t / length
        noise = rng.uniform(-1, 1)
        low = .85 * low + .15 * noise
        if kind == 'charge':
            envelope = math.sin(math.pi * p) ** .4
            value = (.3 * low + .16 * math.sin(math.tau * (65 * t + 75 * t*t))) * envelope
        elif kind == 'jump':
            value = (.3 * noise * math.exp(-9*p) + .25 * math.sin(math.tau * (240*t - 130*t*t))) * math.sin(math.pi*p)**.5
        elif kind == 'land':
            value = (.5 * low + .2 * math.sin(math.tau * 72*t) + .08 * math.sin(math.tau * 713*t)) * math.exp(-8*p) * min(1,t/.003)
        else:
            value = (noise-low) * .16 * math.sin(math.pi*p)**.7
        samples.append(round(max(-.85, min(.85, value)) * 32767))
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(rate)
        stream.writeframes(struct.pack('<' + 'h'*len(samples), *samples))


def build(layout: list[dict], output: Path, native: Path) -> None:
    global NATIVE, REGULAR
    NATIVE = read_native_texture(native)
    REGULAR = {f["key"]:f for f in layout if f["group"]=="Regular"}
    if output.exists():
        raise ValueError('Use a new output directory; existing author sources are preserved.')
    assets = output / 'wardrobe'; assets.mkdir(parents=True)
    states = [('idle', 6, .16), ('walk', 8, .08), ('charge', 6, .07), ('rise', 4, .08),
              ('fall', 4, .1), ('land', 5, .035), ('splat', 4, .12), ('recover', 6, .09), ('lookUp', 4, .14)]
    atlas = Image.new('RGBA', (384, len(states)*48))
    clips = []
    for row, (state, count, duration) in enumerate(states):
        frames = []
        for frame in range(count):
            atlas.alpha_composite(king(state, frame/count), (frame*48, row*48))
            frames.append(dict(x=frame*48, y=row*48, width=48, height=48, duration=duration, originX=24, originY=48,
                               anchors=[dict(id='head', x=0, y=-30+(5 if state=='charge' else 0)), dict(id='back', x=-7, y=-23), dict(id='feet', x=0, y=0)]))
        clips.append(dict(state=state, texture='animations.png', loop=state not in ('land','splat','recover'), transition=0, frames=frames))
    atlas.save(assets/'animations.png')
    # only the original charge-pose visor pixels receive heat, all other poses
    # and the native silhouette remain unchanged. Charge is supplied by Wardrobe+.
    visor = Image.new('RGBA', atlas.size)
    for frame in range(6):
        for x, y in [(x, 40) for x in range(21, 27)] + [(x, y) for y in (41, 42) for x in (23, 24)]:
            point = (frame*48+x, 2*48+y)
            assert atlas.getpixel(point) == (15, 13, 18, 255)
            visor.putpixel(point, (255, 255, 255, 255))
    visor.save(assets/'visor-mask.png')
    fallback = NATIVE.copy()
    for f in layout:
        if f['group'] in ('Regular','BabeCouple','Ending1Misc','NBPKing','OwlKing'):
            box=(f['x'],f['y'],f['x']+f['width'],f['y']+f['height'])
            fallback.paste(recolor(NATIVE.crop(box)), box[:2])
    assert fallback.getchannel('A').tobytes()==NATIVE.getchannel('A').tobytes()
    fallback = protect(NATIVE, fallback, layout)
    fallback.save(output/'ashen-king.png')
    particle = Image.new('RGBA',(32,8)); d = ImageDraw.Draw(particle)
    for i, color in enumerate((WHITE,GOLD,EMBER,'#a13732')):
        d.polygon([(i*8+4,1),(i*8+6,4),(i*8+4,6),(i*8+2,4)],fill=color)
        d.point((i*8+4,3), fill=WHITE)
    particle.save(assets/'embers.png')
    ribbon = Image.new('RGBA',(16,16)); d=ImageDraw.Draw(ribbon)
    d.polygon([(5,0),(10,0),(12,12),(9,15),(7,12),(4,15),(3,9)],fill=INK)
    d.line([(6,1),(6,8),(8,12)],fill='#824348');d.line([(9,5),(10,11)],fill=EMBER)
    ribbon.save(assets/'ribbon.png')
    for kind, length in [('charge',.65)]:
        audio(assets/(kind+'.wav'),kind,length)
    def particles(id, **values):
        return dict(id=id,kind='particles',texture='embers.png',columns=4,life=.5,lifeSpread=.2,size=.5,endSize=0,color='#FFFFFFFF',endColor='#FF553300',**values)
    effects = [particles('jump-embers',count=12,speed=45,speedSpread=16,gravity=100),
               particles('land-embers',count=18,speed=65,speedSpread=25,angle=-90,spread=160,gravity=140),
               particles('idle-embers',count=0,rate=3,duration=.4,speed=8,speedSpread=4,gravity=-4,anchor='back',cooldown=.25),
               particles('air-trail',count=0,rate=14,duration=.15,speed=4,speedSpread=3,gravity=10,cooldown=.1),
               dict(id='steam',kind='particles',count=10,speed=15,speedSpread=8,gravity=-8,life=.5,size=2,endSize=5,color='#D6CDD078',endColor='#D6CDD000'),
               dict(id='charge-sound',kind='sound',sounds=['charge.wav'],volume=.25,loop=True,stopOn='chargeEnd')]
    def channel(name,mode,*effects): return dict(channel=name,mode=mode,effects=list(effects))
    rules = [dict(id='jump',trigger='jump',channels=[channel('particles','replace','jump-embers')]),
             dict(id='snow-jump',trigger='jump',surface='snow',priority=10,channels=[channel('particles','replace','jump-embers')]),
             dict(id='water-jump',trigger='jump',surface='water',priority=20,channels=[channel('particles','keep'),channel('particles','add')])]
    # each rule has one decision per channel, additions compose across ordered rules
    rules[-1]['channels']=[channel('particles','add','steam')]
    rules[-1]['channels'][0]['mode']='keep';rules[-1]['channels'][0]['effects']=[]
    rules.extend([dict(id='water-steam',trigger='jump',surface='water',priority=21,channels=[channel('particles','add','steam')]),
                  dict(id='charge',trigger='chargeStart',channels=[channel('surfaceSound','add','charge-sound')]),
                  dict(id='idle',trigger='idle',channels=[channel('particles','add','idle-embers')]),
                  dict(id='rise',trigger='rise',channels=[channel('particles','add','air-trail')]),
                  dict(id='fall',trigger='fall',channels=[channel('particles','add','air-trail')]),
                  dict(id='landing',trigger='land',channels=[channel('particles','add','land-embers')]),
                  dict(id='snow-land',trigger='land',surface='snow',priority=10,channels=[channel('particles','keep')]),
                  dict(id='snow-ash',trigger='land',surface='snow',priority=11,channels=[channel('particles','add','idle-embers')]),
                  dict(id='water-land',trigger='land',surface='water',priority=20,channels=[channel('particles','replace','steam')]),
                  dict(id='splat',trigger='splat',channels=[channel('particles','add','land-embers')])])
    manifest = dict(schema=1,id='ashen-king',name='Ashen King',version='1.0.6',minimumWardrobe='2.0.0',defaultProfile='embers',effects=effects,
                    profiles=[dict(id='embers',name='Ash and embers',rules=rules)],
                    materials=[dict(id='embers',kind='shader',shader='visor.mgfxo',mask='visor-mask.png')],
                    animations=[dict(id='king',item='NULL',material='embers',clips=clips)])
    (assets/'skin.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    (output/'set_settings.xml').write_text('<SetSettings><enabled>false</enabled><Reskins><Reskin><skin>NULL</skin><name>ashen-king</name></Reskin></Reskins></SetSettings>\n',encoding='utf-8')
    preview = Image.new('RGB',(256,256),'#15121e');preview.paste(king('idle',0).resize((192,192),Image.Resampling.NEAREST),(32,26),king('idle',0).resize((192,192),Image.Resampling.NEAREST))
    preview.save(output/'workshop-preview.png')
    (output/'README.md').write_text('# Ashen King\n\nA reskin of the original King: native silhouette and poses, soot-dark armor, ember seams and synthesized sound.\nThe helmet slit heats from dark to incandescent with jump charge. Takeoff, landing and splat use native sounds on every surface, including heavy boots. Landing retains the standing pose; the package adds no screen shake.\nRequires Wardrobe+ 2.0 for animation and effects; the native atlas works independently.\nSelect Ashen King in Collections, or choose its embers profile on another outfit.\n\nOriginal King artwork: Nexile / Jump King. This derivative reskin preserves the original atlas geometry. Palette, ember variations and synthesized sounds are reproducible with build_assets.py and an installed copy of the game. No original game audio is bundled.\n',encoding='utf-8')
    print(output)


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--native',type=Path,required=True);parser.add_argument('--layout',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();build(json.loads(args.layout.read_text()),args.output,args.native)
