"""Reproduce the former UIApi+ Workshop card with the JK Runtime title."""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


MOD_ROOT = Path(__file__).resolve().parents[1]
INK = (7, 9, 11, 255)
TEXT = (246, 222, 173, 255)
TITLE_LINES = ("JK", "Runtime")


def gui_frame(canvas: Image.Image, atlas: Image.Image) -> None:
    x, y, width, height = 12, 48, 232, 160
    canvas.paste((0, 0, 0, 255), (x + 8, y + 8, x + width - 8, y + height - 8))
    for col in range(3):
        for row in range(3):
            source = atlas.crop((col * 16, row * 16, col * 16 + 16, row * 16 + 16))
            dx = (x, x + 16, x + width - 16)[col]
            dy = (y, y + 16, y + height - 16)[row]
            dw = (16, width - 32, 16)[col]
            dh = (16, height - 32, 16)[row]
            canvas.alpha_composite(source.resize((dw, dh), Image.Resampling.NEAREST), (dx, dy))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=Path(r"C:\Program Files (x86)\Steam\steamapps\common\Jump King"))
    parser.add_argument("--output", type=Path, default=MOD_ROOT / "workshop-preview.png")
    args = parser.parse_args()
    title = ImageFont.truetype(str(args.game_dir / "Content/font/ttf_litter_lover2_bold.ttf"), 44)
    canvas = Image.new("RGBA", (256, 256), INK)
    with Image.open(MOD_ROOT / "assets/workshop-frame.png") as frame:
        gui_frame(canvas, frame.convert("RGBA"))
    draw = ImageDraw.Draw(canvas)
    draw.fontmode = "1"
    boxes = [draw.textbbox((0, 0), line, font=title) for line in TITLE_LINES]
    gap = 14
    height = sum(b - t for _, t, _, b in boxes) + gap * (len(boxes) - 1)
    y = 64 + (128 - height) // 2
    for line, (left, top, right, bottom) in zip(TITLE_LINES, boxes):
        width = right - left
        if width > 200 or height > 128:
            raise ValueError("Title exceeds the original card's safe area")
        x = 28 + (200 - width) // 2 - left
        draw.text((x, y - top), line, font=title, fill=TEXT)
        y += bottom - top + gap
    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(args.output, optimize=True)
    if args.output.stat().st_size >= 34 * 1024:
        raise ValueError("Workshop preview exceeds the 34 KiB budget")
    print(f"[OK] {args.output}: 256x256, {args.output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
