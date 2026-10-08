"""Render per-item ending ownership sheets from the reviewed source masks."""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
from build_item_masks import native, ROOT


def mask_image(frame):
    image = Image.new('RGBA', (frame['width'], frame['height']))
    draw = ImageDraw.Draw(image)
    for channel, color in [('foreign', (245, 65, 85, 160)),
                           ('occluded', (60, 145, 255, 255)), ('rows', (80, 235, 140, 255))]:
        for y, left, right in frame[channel]:
            draw.line((left, y, right, y), fill=color)
    return image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--king', type=Path, required=True)
    parser.add_argument('--layout', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    layout = json.loads(args.layout.read_text(encoding='utf-8'))
    items = json.loads((ROOT / 'assets/item-masks.json').read_text(encoding='utf-8'))['items']
    textures = {s.findtext('item'): s.findtext('texture') for s in ET.parse(args.king / 'skin_settings.xml').findall('./skins/Skin')}
    base = native.read_native_texture(args.king / 'base.xnb')
    pages = []
    featured = []
    for item in items:
        atlas = native.read_native_texture(args.king / (textures[item['item']] + '.xnb'))
        frames = []
        masks = {(f['group'], f['key']): f for f in item['frames']}
        protection = Image.new('RGBA', atlas.size)
        for f in layout:
            m = masks[f['group'], f['key']]
            box = (f['x'], f['y'], f['x'] + f['width'], f['y'] + f['height'])
            picture = mask_image(m)
            protection.paste(picture, (f['x'], f['y']))
            if f['group'] == 'Regular' or not m['rows']:
                continue
            scene = base.crop(box); scene.alpha_composite(atlas.crop(box))
            overlay = scene.copy(); overlay.alpha_composite(picture)
            geometry = picture.copy()
            for y, left, right in m['foreign']:
                ImageDraw.Draw(geometry).line((left, y, right, y), fill=(0, 0, 0, 0))
            entry = (item['item'] + ' / ' + f['group'] + ' / ' + str(f['key']), scene, overlay, geometry)
            frames.append(entry)
            if (item['item'], f['group'], f['key']) in {
                ('CapeOwl', 'OwlKing', 14), ('GiantBoots', 'BabeCouple', 0),
                ('CrownOwl', 'OwlKing', 1), ('Tunic', 'NBPKing', 7),
                ('YellowShoes', 'NBPKing', 7), ('SnakeRing', 'NBPKing', 13)}:
                featured.append(entry)
        protection.save(args.output / (item['item'] + '-mask.png'))
        for offset in range(0, len(frames), 6):
            name = item['item'] + '-' + str(offset // 6 + 1) + '.png'
            sheet(frames[offset:offset + 6], args.output / name)
            pages.append(name)
    sheet(featured, args.output / 'overview.png')
    html = '<html><meta charset="utf-8"><title>Item ownership masks</title><style>body{background:#181c24;color:#eee;font:18px sans-serif}img{image-rendering:pixelated;max-width:100%}a{color:#9ef}</style>'
    html += '<h1>Ending item masks</h1><p>Native composite / ownership / item geometry. Green: material applies. Red: protected other artwork. Blue: protected overlap contributing to the hidden item contour. Empty masks on NPC-only poses are omitted from sheets; full atlases are included separately.</p>'
    html += '<p>' + ' | '.join('<a href="' + item['item'] + '-mask.png">' + item['item'] + ' atlas</a>' for item in items) + '</p>'
    html += ''.join('<h2>' + name[:-4] + '</h2><a href="' + name + '"><img src="' + name + '"></a>' for name in ['overview.png'] + pages)
    (args.output / 'index.html').write_text(html, encoding='utf-8')
    print('Review:', args.output / 'index.html', '-', len(pages), 'sheets')


def sheet(entries, path):
    scale, cell_w, cell_h = 4, 810, 445
    image = Image.new('RGB', (cell_w * 2, cell_h * ((len(entries) + 1) // 2) + 48), '#252c37')
    draw = ImageDraw.Draw(image)
    draw.text((12, 8), 'NATIVE COMPOSITE / OWNERSHIP / ITEM GEOMETRY', fill='white')
    draw.text((12, 25), 'Green: item. Red: protected other artwork. Blue: protected overlap with confirmed item geometry.', fill='white')
    for index, (title, *pictures) in enumerate(entries):
        x, y = index % 2 * cell_w, index // 2 * cell_h + 48
        draw.text((x + 10, y), title, fill='white')
        for col, picture in enumerate(pictures):
            big = picture.resize((picture.width * scale, picture.height * scale), Image.Resampling.NEAREST)
            image.paste(big, (x + 10 + col * 266, y + 24), big)
    image.save(path)


if __name__ == '__main__':
    main()
