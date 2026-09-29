"""Shared, explicitly authored native King pixel mask (no runtime segmentation)."""
import json
from pathlib import Path
from PIL import Image, ImageDraw


def frame_mask(group, key, size, channel="rows"):
    path = Path(__file__).resolve().parents[1] / 'assets/king-mask.json'
    entries = json.loads(path.read_text())['frames']
    mask = Image.new('L', size)
    draw = ImageDraw.Draw(mask)
    entry = next((f for f in entries if f['group'] == group and f['key'] == key), None)
    if entry:
        assert (entry['width'], entry['height']) == size
        for y, left, right in entry[channel]:
            draw.line((left, y, right, y), fill=255)
    return mask


def protect(original, changed, layout):
    mask = Image.new('L', original.size)
    for f in layout:
        mask.paste(frame_mask(f['group'], f['key'], (f['width'], f['height'])), (f['x'], f['y']))
    return Image.composite(changed, original, mask)


def body_geometry(group, key, source):
    visible = frame_mask(group, key, source.size)
    hidden = frame_mask(group, key, source.size, 'occluded')
    result = Image.new('L', source.size)
    for y in range(source.height):
        for x in range(source.width):
            if hidden.getpixel((x,y)) or (visible.getpixel((x,y)) and source.getpixel((x,y))[3]):
                result.putpixel((x,y),255)
    return result
