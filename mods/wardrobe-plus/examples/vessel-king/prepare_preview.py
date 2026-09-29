"""Fit the selected pixel artwork to Workshop's preview budget without repainting it."""
from io import BytesIO
from pathlib import Path
from PIL import Image


def prepare(source_path: Path, output: Path) -> None:
    with Image.open(source_path) as image:
        source = image.convert('RGBA')
    # keep the full composition and aspect ratio
    # extend only the outer rows/columns to make it square before nearest-neighbour scaling
    side = max(source.size)
    left, top = (side-source.width)//2, (side-source.height)//2
    square = Image.new('RGBA', (side, side))
    for y in range(side):
        for x in range(side):
            square.putpixel((x,y), source.getpixel((min(source.width-1,max(0,x-left)),
                                                   min(source.height-1,max(0,y-top)))))
    resized = square.resize((256,256), Image.Resampling.NEAREST)
    indexed = resized.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    error = sum(sum(abs(original[i]*original[3]/255-converted[i]*converted[3]/255) for i in range(3))
                + abs(original[3]-converted[3])
                for original,converted in zip(resized.get_flattened_data(),indexed.convert('RGBA').get_flattened_data()))/(256*256*4)
    if error > 3:
        raise ValueError('Palette conversion changes the artwork too much')
    if [a>0 for a in resized.getchannel('A').get_flattened_data()] != [a>0 for a in indexed.convert('RGBA').getchannel('A').get_flattened_data()]:
        raise ValueError('Palette conversion changes the native silhouette')
    stream = BytesIO()
    indexed.save(stream,format='PNG',optimize=True,compress_level=9)
    payload = stream.getvalue()
    if len(payload) >= 34*1024:
        raise ValueError('Workshop preview exceeds the 34 KiB safety budget')
    output.write_bytes(payload)
    print(f'[OK] Preview: 256x256, indexed PNG, {len(payload)} bytes; mean channel error {error:.4f}')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,default=Path(__file__).with_name('workshop-preview.png'))
    args=parser.parse_args()
    prepare(args.source,args.output)
