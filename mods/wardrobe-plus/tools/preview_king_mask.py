"""Export inspectable source/mask pairs from the hand-authored pixel ownership mask."""
import argparse, importlib.util, json, sys
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root/'examples'))
from king_mask import frame_mask, body_geometry
spec = importlib.util.spec_from_file_location('native', root/'examples/ashen-king/build_assets.py')
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)
parser = argparse.ArgumentParser()
parser.add_argument('--native', type=Path, required=True)
parser.add_argument('--layout', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
atlas = native.read_native_texture(args.native)
layout = json.loads(args.layout.read_text())
pages = []
protection = Image.new('RGBA',atlas.size)
geometry_atlas = Image.new('L',atlas.size)
for f in layout:
    src=atlas.crop((f['x'],f['y'],f['x']+f['width'],f['y']+f['height']))
    separate=frame_mask(f['group'],f['key'],src.size,'foreign')
    overlap=frame_mask(f['group'],f['key'],src.size,'occluded')
    for y in range(src.height):
        for x in range(src.width):
            if separate.getpixel((x,y)):protection.putpixel((f['x']+x,f['y']+y),(245,65,85,255))
            elif overlap.getpixel((x,y)):protection.putpixel((f['x']+x,f['y']+y),(60,145,255,255))
    geometry_atlas.paste(body_geometry(f['group'],f['key'],src),(f['x'],f['y']))
protection.save(args.output/'king-protection.png')
geometry_atlas.save(args.output/'king-body.png')

def sheet(frames,name):
    scale=5
    cell_w=max(f['width'] for f in frames)*scale
    cell_h=max(f['height'] for f in frames)*scale
    tile_w=cell_w*3+65;tile_h=cell_h+65
    image=Image.new('RGB',(tile_w*2,tile_h*((len(frames)+1)//2)+60),'#252c37')
    d=ImageDraw.Draw(image)
    d.text((12,10),'ORIGINAL / PROTECTION / BODY GEOMETRY',fill='white')
    d.text((12,28),'Red: separate object. Blue: object over the body. Untinted: visible King. Crowns and awarded cape are protected.',fill='white')
    for i,f in enumerate(frames):
        x,y=i%2*tile_w,i//2*tile_h+60
        box=(f['x'],f['y'],f['x']+f['width'],f['y']+f['height'])
        source=atlas.crop(box);mask=protection.crop(box)
        overlay=source.copy();overlay.alpha_composite(mask)
        body=Image.new('RGBA',source.size)
        geo=geometry_atlas.crop(box)
        for py in range(source.height):
            for px in range(source.width):
                if geo.getpixel((px,py)):
                    body.putpixel((px,py),(60,145,255,255) if mask.getpixel((px,py))[2]>200 else (225,232,237,255))
        d.text((x+10,y),f['group']+' / '+str(f['key']),fill='white')
        for col,picture in enumerate((source,overlay,body)):
            big=picture.resize((source.width*scale,source.height*scale),Image.Resampling.NEAREST)
            image.paste(big,(x+10+col*(cell_w+18),y+25),big)
    image.save(args.output/name);pages.append(name)
featured=[('BabeCouple',0),('NBPKing',1),('NBPKing',13),('OwlKing',3),('OwlKing',14),('OwlKing',2)]
sheet([next(f for f in layout if (f['group'],f['key'])==key) for key in featured],'overview.png')
for group in dict.fromkeys(f['group'] for f in layout):
    frames=sorted([f for f in layout if f['group']==group],key=lambda f:f['key'])
    for start in range(0,len(frames),6):sheet(frames[start:start+6],group+'-'+str(start//6+1)+'.png')
html='<html><meta charset="utf-8"><title>King mask review</title><style>body{background:#181c24;color:#eee;font:18px sans-serif}img{image-rendering:pixelated;max-width:100%}a{color:#9ef}section{margin:40px 0}</style><h1>King pixel ownership</h1><p>Original / protection / reconstructed body. Red: separate objects. Blue: foreign pixels over the King. The third view shows geometry used for contours and fluid volume. Click to inspect full-size pixels.</p>'
html+=''.join(f'<section><h2>{p[:-4]}</h2><a href="{p}"><img src="{p}"></a></section>' for p in pages)
(args.output/'index.html').write_text(html,encoding='utf-8')
