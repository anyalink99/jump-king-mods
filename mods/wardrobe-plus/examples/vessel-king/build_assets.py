"""Derive a hollow glass King from the installed native silhouette."""
from __future__ import annotations
import argparse
import importlib.util
import json
import sys
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from king_mask import protect, frame_mask, body_geometry
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('native_reader', Path(__file__).resolve().parents[1]/'ashen-king/build_assets.py')
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)


def vessel(source, geometry=None):
    width, height = source.size
    occupied = {(x,y) for y in range(height) for x in range(width) if (geometry.getpixel((x,y)) if geometry is not None else source.getpixel((x,y))[3])}
    output = Image.new('RGBA', source.size)
    mask = Image.new('RGBA', source.size, (0,0,0,255))
    if not occupied:
        return output, mask
    boundary = {p for p in occupied if any((p[0]+dx,p[1]+dy) not in occupied for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)))}
    interior = occupied-boundary
    xmin, xmax = min(x for x,y in occupied), max(x for x,y in occupied)
    # use cumulative interior area so each pose keeps the same fractional water volume
    # even when crouching changes the width and height
    below = 0
    for y in range(height-1,-1,-1):
        row = sum((x,y) in interior for x in range(width))
        depth = (below+row*.5)/max(1,len(interior))
        for x in range(width):
            if (x,y) not in occupied:
                continue
            if (x,y) in boundary:
                color = (192,243,238,245) if (x,y-1) not in occupied else (53,115,134,230)
                if (x-1,y) not in occupied:
                    color = (113,211,219,240)
                output.putpixel((x,y),color)
            else:
                original = source.getpixel((x,y))[:3]
                if original == (255,255,255):
                    color = (223,255,247,185)
                elif original == (0,0,0):
                    color = (31,75,91,130)
                else:
                    color = (106,184,197,48)
                output.putpixel((x,y),color)
                mask.putpixel((x,y),(255,round((x-xmin)/max(1,xmax-xmin)*255),round(depth*255),255))
        below += row
    assert {p for p in occupied if output.getpixel(p)[3]} == occupied
    return output, mask


def static_water(glass, mask):
    output = glass.copy()
    for y in range(glass.height):
        for x in range(glass.width):
            r,_,depth,_ = mask.getpixel((x,y))
            if r and depth/255 < .72:
                output.putpixel((x,y),(40,145,184,225) if depth/255 < .66 else (153,245,234,235))
    return output


def build(native, layout, output):
    if output.exists():
        raise ValueError('Use a new output directory.')
    assets = output/'wardrobe'
    assets.mkdir(parents=True)
    original = reader.read_native_texture(native)
    frames = json.loads(layout.read_text())
    regular = {f['key']:f for f in frames if f['group']=='Regular'}
    poses = {key:vessel(original.crop((f['x'],f['y'],f['x']+f['width'],f['y']+f['height']))) for key,f in regular.items()}
    states = [('idle',[0]),('walk',[1,3,2,3]),('charge',[4]),('rise',[5]),
              ('fall',[6,7]),('land',[0]),('splat',[8]),('recover',[9,11,10,11,0]),('lookUp',[12])]
    atlas = Image.new('RGBA',(240,48*len(states)))
    masks = Image.new('RGBA',atlas.size,(0,0,0,255))
    clips = []
    for row,(state,keys) in enumerate(states):
        authored = []
        for col,key in enumerate(keys):
            glass,mask = poses[key]
            atlas.paste(glass,(col*48,row*48));masks.paste(mask,(col*48,row*48))
            duration = [11/60,4/60,10/60,4/60][col] if state=='walk' else 30 if state=='fall' else .09
            authored.append(dict(x=col*48,y=row*48,width=48,height=48,originX=24,originY=48,duration=duration,
                                 anchors=[dict(id='feet',x=0,y=0),dict(id='body',x=0,y=-8)]))
        clip = dict(state=state,texture='animations.png',loop=state in ('idle','walk','charge','rise','lookUp'),transition=0,frames=authored)
        if state in ('walk','fall','recover'):
            clip['nativeFrames'] = keys
        clips.append(clip)
    atlas.save(assets/'animations.png');masks.save(assets/'water-mask.png')
    fallback = original.copy()
    for frame in frames:
        if frame['group'] in ('Regular','BabeCouple','Ending1Misc','NBPKing','OwlKing'):
            x,y,w,h = [frame[n] for n in ('x','y','width','height')]
            source = original.crop((x,y,x+w,y+h))
            owned = Image.composite(source, Image.new('RGBA',source.size), frame_mask(frame['group'],frame['key'],source.size))
            glass,mask = poses[frame['key']] if frame['group']=='Regular' else vessel(owned, body_geometry(frame['group'],frame['key'],source))
            fallback.paste(static_water(glass,mask),(x,y))
    fallback = protect(original, fallback, frames)
    fallback.save(output/'vessel-king.png')
    drop = Image.new('RGBA',(3,5));draw = ImageDraw.Draw(drop)
    draw.line((1,0,1,4),fill='#5ed6ec',width=1);draw.rectangle((0,2,2,3),fill='#36a5dc');draw.point((1,1),fill='#e0ffff')
    drop.save(assets/'drop.png')
    effects = [dict(id='spill',kind='particles',texture='drop.png',anchor='body',count=64,maxAlive=128,liquidScale=True,
                    collision='water',speed=85,speedSpread=40,angle=-90,spread=165,gravity=310,life=4,lifeSpread=.6,
                    size=.65,endSize=.3,color='#FFFFFFFF',endColor='#70CFEF00',cooldown=.25)]
    rules = [dict(id='splat-spill',trigger='splat',channels=[dict(channel='particles',mode='add',effects=['spill'])])]
    manifest = dict(schema=1,id='vessel-king',name='Vessel King',version='1.0.1',minimumWardrobe='2.0.8',defaultProfile='water',
                    effects=effects,profiles=[dict(id='water',name='Spilled water',rules=rules)],
                    materials=[dict(id='water',kind='shader',shader='visor.mgfxo',mask='water-mask.png')],
                    animations=[dict(id='king',item='NULL',material='water',clips=clips)])
    (assets/'skin.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    (output/'set_settings.xml').write_text('<SetSettings><enabled>false</enabled><Reskins><Reskin><skin>NULL</skin><name>vessel-king</name></Reskin></Reskins></SetSettings>\n',encoding='utf-8')
    # the packaging step renders the Workshop preview through the compiled shader
    (output/'README.md').write_text((Path(__file__).parent/'README.md').read_text(encoding='utf-8'),encoding='utf-8')
    print(output)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--native',type=Path,required=True);parser.add_argument('--layout',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();build(args.native,args.layout,args.output)
