"""Validate explicit ownership classes and protected native fallback pixels."""
import argparse, importlib.util, json, sys
from pathlib import Path

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'examples'))
from king_mask import frame_mask, body_geometry
spec=importlib.util.spec_from_file_location('native',root/'examples/ashen-king/build_assets.py')
native=importlib.util.module_from_spec(spec);spec.loader.exec_module(native)
parser=argparse.ArgumentParser()
parser.add_argument('--native',type=Path,required=True)
parser.add_argument('--layout',type=Path,required=True)
parser.add_argument('--reskin',type=Path,required=True)
args=parser.parse_args()
from PIL import Image
source=native.read_native_texture(args.native)
reskin=Image.open(args.reskin).convert('RGBA')
assert source.size==reskin.size
frames=json.loads(args.layout.read_text())
document=json.loads((root/'assets/king-mask.json').read_text())
assert document['schema']==2
assert {(f['group'],f['key']) for f in document['frames']}=={(f['group'],f['key']) for f in frames}
protected=overlaps=0
for f in frames:
    visible=frame_mask(f['group'],f['key'],(f['width'],f['height']))
    overlap=frame_mask(f['group'],f['key'],visible.size,'occluded')
    separate=frame_mask(f['group'],f['key'],visible.size,'foreign')
    for y in range(f['height']):
        for x in range(f['width']):
            p=(f['x']+x,f['y']+y)
            kinds=sum(bool(m.getpixel((x,y))) for m in (visible,overlap,separate))
            assert kinds<=1, (f['group'],f['key'],x,y,'overlapping classes')
            if source.getpixel(p)[3]:assert kinds==1,(f['group'],f['key'],x,y,'unclassified pixel')
            if not visible.getpixel((x,y)):
                assert reskin.getpixel(p)==source.getpixel(p),(f['group'],f['key'],x,y,'foreign pixel changed')
                protected+=bool(source.getpixel(p)[3]);overlaps+=bool(overlap.getpixel((x,y)))
assert protected>1000 and overlaps>100
print(f'[OK] Two-class mask: {len(frames)} frames, {protected} protected pixels, {overlaps} occluding pixels; fallback matches native artwork outside the King.')
