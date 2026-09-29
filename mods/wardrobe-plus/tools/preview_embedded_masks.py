"""Review embedded reward masks alongside original/Glass graphics-test exports."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
GROUPS = ['Regular', 'Babe', 'BabeCouple', 'Ending1Misc', 'NBPKing', 'NBPBabe',
          'OwlKing', 'OwlBabe', 'OwlGargoyle', 'OwlBird']
ITEMS = {'Crown': 0, 'CrownNBP': 3, 'CapeOwl': 13}


def straight_alpha(path):
    image = Image.open(path).convert('RGBA')
    image.putdata([(min(255, round(r * 255 / a)), min(255, round(g * 255 / a)),
                    min(255, round(b * 255 / a)), a) if a else (0, 0, 0, 0)
                   for r, g, b, a in image.get_flattened_data()])
    return image


def sheet(entries, path):
    scale, cell_w, cell_h = 4, 810, 425
    image = Image.new('RGB', (cell_w * 2, cell_h * ((len(entries) + 1) // 2) + 42), '#252c37')
    draw = ImageDraw.Draw(image)
    draw.text((12, 8), 'ORIGINAL / REWARD OWNERSHIP / GLASS (GAME MATERIAL BAKER)', fill='white')
    draw.text((12, 23), 'Green: reward pixels. Blue: occluded reward geometry. Untinted: protected actor artwork.', fill='white')
    for i, (title, pictures) in enumerate(entries):
        x, y = i % 2 * cell_w, i // 2 * cell_h + 42
        draw.text((x + 10, y), title, fill='white')
        for col, picture in enumerate(pictures):
            picture = picture.resize((picture.width * scale, picture.height * scale), Image.Resampling.NEAREST)
            image.paste(picture, (x + 10 + col * 266, y + 24), picture)
    image.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rendered', type=Path, required=True, help='Wardrobe graphics test artifact directory')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    document = json.loads((ROOT / 'assets/embedded-masks.json').read_text(encoding='utf-8'))
    pages, featured = [], []
    for item in document['items']:
        entries = []
        for frame in item['frames']:
            name = str(ITEMS[item['item']]) + '-' + str(GROUPS.index(frame['group'])) + '-' + str(frame['key'])
            original = straight_alpha(args.rendered / 'embedded-items' / (name + '-original.png'))
            changed = straight_alpha(args.rendered / 'embedded-items' / (name + '-glass.png'))
            overlay = original.copy(); draw = ImageDraw.Draw(overlay)
            for channel, color in [('rows', '#50eb8c'), ('occluded', '#3c91ff')]:
                for y, left, right in frame[channel]:
                    draw.line((left, y, right, y), fill=color)
            entry = (item['item'] + ' / ' + frame['group'] + ' / ' + str(frame['key']), [original, overlay, changed])
            entries.append(entry)
            if (frame['group'], frame['key']) in [('Ending1Misc', 2), ('BabeCouple', 10),
                                                  ('NBPBabe', 0), ('NBPBabe', 25), ('OwlBird', 0)]:
                featured.append(entry)
        for offset in range(0, len(entries), 6):
            name = item['item'] + '-' + str(offset // 6 + 1) + '.png'
            sheet(entries[offset:offset + 6], args.output / name); pages.append(name)
    vessel = [straight_alpha(args.rendered / ('vessel-crown-' + suffix + '.png')) for suffix in ['original', 'glass']]
    vessel_mask = vessel[0].copy(); draw = ImageDraw.Draw(vessel_mask)
    frame = next(f for item in document['items'] if item['item'] == 'Crown'
                 for f in item['frames'] if f['group'] == 'BabeCouple' and f['key'] == 10)
    for y, left, right in frame['rows']:
        draw.line((left, y, right, y), fill='#50eb8c')
    featured.append(('Vessel King / first ending / Original and Glass crown', [vessel[0], vessel_mask, vessel[1]]))
    sheet(featured, args.output / 'overview.png')
    html = '<html><meta charset="utf-8"><title>Ending rewards</title><style>body{background:#181c24;color:#eee;font:18px sans-serif}img{image-rendering:pixelated;max-width:100%}a{color:#9ef}</style>'
    html += '<h1>Reward ownership throughout ending scenes</h1><p>Original / mask / Glass. The replacement NBP crown and NPC features stay protected. Rendered by the game material pipeline; enlarged without smoothing.</p>'
    html += ''.join('<h2>' + p[:-4] + '</h2><a href="' + p + '"><img src="' + p + '"></a>' for p in ['overview.png'] + pages)
    (args.output / 'index.html').write_text(html, encoding='utf-8')
    print(args.output / 'index.html')


if __name__ == '__main__':
    main()
