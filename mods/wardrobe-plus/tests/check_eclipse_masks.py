"""Check authored visor masks against the installed native atlas, including bevel exclusion."""
import argparse
import importlib.util
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('eclipse', Path(__file__).resolve().parents[1]/'examples/eclipse-king/build_assets.py')
eclipse = importlib.util.module_from_spec(spec)
spec.loader.exec_module(eclipse)


def check(native, layout, proof):
    atlas = eclipse.reader.read_native_texture(native)
    frames = {f['key']: f for f in json.loads(layout.read_text()) if f['group'] == 'Regular'}
    # reviewed interior regions exclude the neck and silhouette. compare every
    # native black pixel inside them, not a copy of the authored mask's run list
    regions = {0:(20,25,27,31),1:(21,24,28,30),2:(21,25,28,31),3:(21,24,28,30),
               4:(21,40,27,43),5:(21,19,28,26),6:(21,21,28,27),7:(23,24,30,32),
               9:(20,25,27,31),10:(20,27,27,33),11:(21,24,28,30),12:(16,24,25,27)}
    sheet = Image.new('RGB',(7*192,2*256),'#303033')
    draw = ImageDraw.Draw(sheet)
    names = ['IDLE','WALK 1','WALK 2','SMEAR','CHARGE','RISE','FALL','BOUNCE','SPLAT','RECOVER 1','RECOVER 2','RECOVER SMEAR','LOOK UP']
    for key, frame in frames.items():
        x,y,w,h = [frame[n] for n in ('x','y','width','height')]
        source = atlas.crop((x,y,x+w,y+h))
        skin, mask, _ = eclipse.pose(source,key)
        actual = {(px,py) for py in range(h) for px in range(w) if mask.getpixel((px,py))[0] == 255}
        if key == 8:
            expected = set()
        else:
            x0,y0,x1,y1 = regions[key]
            expected = {(px,py) for py in range(y0,y1) for px in range(x0,x1) if source.getpixel((px,py)) == (0,0,0,255)}
        assert actual == expected, f'Pose {key}: missing={expected-actual}, outside={actual-expected}'
        assert skin.getchannel('A').tobytes() == source.getchannel('A').tobytes()
        # Diagnostic overlay marks only the opening on original artwork
        marked = source.copy()
        for point in actual:
            marked.putpixel(point,(255,45,160,255))
        tile = marked.resize((192,192),Image.Resampling.NEAREST)
        tx,ty = key%7*192,key//7*256
        sheet.paste(tile,(tx,ty+20),tile)
        draw.text((tx+8,ty+220),names[key],fill='white')
        draw.text((tx+8,ty+235),f'{len(actual)} opening pixels',fill='#ff83c5')
    sheet.save(proof)
    print('[OK] Exact visor coverage and native alpha in all 13 poses; hidden splat visor excluded')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--native',type=Path,required=True)
    parser.add_argument('--layout',type=Path,required=True)
    parser.add_argument('--proof',type=Path,required=True)
    args = parser.parse_args()
    check(args.native,args.layout,args.proof)
