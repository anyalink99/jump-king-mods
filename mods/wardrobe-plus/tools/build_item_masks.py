"""Prepare native item ownership masks for visual review; never run in the game.

Ending coverage comes from each separate native item atlas. Hidden pixels are
accepted only when every visible pixel matches a translated regular pose and
all matching poses agree that an existing foreground pixel covers the item.
No palette classification, dilation or guessed silhouette is used.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("native", ROOT / "examples/ashen-king/build_assets.py")
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


def runs(points, width, height):
    result = []
    for y in range(height):
        x = 0
        while x < width:
            if (x, y) not in points:
                x += 1
                continue
            left = x
            while x + 1 < width and (x + 1, y) in points:
                x += 1
            result.append([y, left, x])
            x += 1
    return result


def pixels(atlas, frame):
    return {(x, y): atlas.getpixel((frame['x'] + x, frame['y'] + y))
            for y in range(frame['height']) for x in range(frame['width'])
            if atlas.getpixel((frame['x'] + x, frame['y'] + y))[3]}


def hidden_pixels(visible, foreground, templates):
    if len(visible) < 4:
        return set()
    anchor = next(iter(visible))
    candidates = []
    for template in templates:
        if len(template) <= len(visible):
            continue
        for position, color in template.items():
            if color != visible[anchor]:
                continue
            dx, dy = anchor[0] - position[0], anchor[1] - position[1]
            shifted = {(x + dx, y + dy): c for (x, y), c in template.items()}
            if not all(shifted.get(p) == c for p, c in visible.items()):
                continue
            extra = set(shifted) - set(visible)
            if extra and extra <= foreground:
                candidates.append(extra)
    return set.intersection(*candidates) if candidates else set()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--king', type=Path, required=True)
    parser.add_argument('--layout', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    layout = json.loads(args.layout.read_text(encoding='utf-8'))
    body = json.loads((ROOT / 'assets/king-mask.json').read_text(encoding='utf-8'))
    body_frames = {(f['group'], f['key']): f for f in body['frames']}
    base = native.read_native_texture(args.king / 'base.xnb')
    items = []
    for skin in ET.parse(args.king / 'skin_settings.xml').findall('./skins/Skin'):
        item, texture = skin.findtext('item'), skin.findtext('texture')
        atlas = native.read_native_texture(args.king / (texture + '.xnb'))
        templates = [pixels(atlas, f) for f in layout if f['group'] == 'Regular']
        frames = []
        for f in layout:
            w, h = f['width'], f['height']
            visible = pixels(atlas, f)
            background = set(pixels(base, f))
            hidden = set()
            if f['group'] == 'Regular':
                # keep arbitrary Workshop silhouettes in ordinary gameplay
                allowed = {(x, y) for y in range(h) for x in range(w)}
            elif f['group'] in ('BabeCouple', 'Ending1Misc', 'NBPKing', 'OwlKing'):
                allowed = set(visible)
                body_frame = body_frames[f['group'], f['key']]
                foreground = {(x, y) for y, left, right in body_frame['occluded']
                              for x in range(left, right + 1)}
                # the cape is also behind the King's body, unlike boots/tunics
                if item == 'CapeOwl':
                    foreground = background - allowed
                hidden = hidden_pixels(visible, foreground, templates)
            else:
                # NPC-only groups don't acquire ownership from stray atlas ink
                allowed = set()
            foreign = (background | set(visible)) - allowed - hidden
            frames.append(dict(group=f['group'], key=f['key'], width=w, height=h,
                               rows=runs(allowed, w, h), occluded=runs(hidden, w, h),
                               foreign=runs(foreign, w, h)))
        items.append(dict(item=item, frames=frames))
    document = dict(schema=1, description='Native item ownership; regular poses allow custom silhouettes. '
                    'Ending rows follow the separate item atlas; hidden coverage requires exact pose evidence. '
                    'Review changes before replacing this asset.', items=items)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, separators=(',', ':')) + '\n', encoding='utf-8')
    print('Prepared masks for', len(items), 'items:', args.output)


if __name__ == '__main__':
    main()
