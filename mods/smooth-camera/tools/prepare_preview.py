"""Compose the generated illustration and a separate native-font title."""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Jump King"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    font_path = args.game_dir / "Content/font/ttf_pixolde_bold.ttf"
    source = root / "assets/workshop-preview-art-v1.png"
    output = root / "workshop-preview.png"
    with Image.open(source) as art:
        if art.width != art.height:
            raise ValueError("Expected square artwork; do not silently crop it")
        canvas = Image.new("RGBA", (256, 256), (2, 5, 13, 255))
        canvas.alpha_composite(art.convert("RGBA").resize((256, 256), Image.Resampling.NEAREST))

    font = ImageFont.truetype(str(font_path), 44)
    # Render text independently at the delivery resolution. A binary mask keeps
    # native pixel lettering crisp; the illustration never supplies any glyphs.
    for text, y in (("SMOOTH", 10), ("CAMERA", 36)):
        left, top, right, bottom = font.getbbox(text)
        mask = Image.new("L", (right - left, bottom - top))
        ImageDraw.Draw(mask).text((-left, -top), text, font=font, fill=255)
        mask = mask.point(lambda value: 255 if value >= 96 else 0)
        title = Image.new("RGBA", mask.size, (246, 222, 173, 255))
        title.putalpha(mask)
        canvas.alpha_composite(title, ((256 - title.width) // 2, y))

    preview = canvas.convert("RGB").quantize(
        colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    preview.save(output, optimize=True)
    with Image.open(output) as check:
        check.load()
        if check.size != (256, 256) or check.format != "PNG":
            raise ValueError("Preview format verification failed")
    if output.stat().st_size >= 34 * 1024:
        raise ValueError("Preview exceeds the project's 34 KiB budget")
    work = root.parents[1] / "build/smooth-camera/PREVIEW_WORK"
    work.mkdir(parents=True, exist_ok=True)
    preview.resize((1024, 1024), Image.Resampling.NEAREST).save(work / "workshop-preview-1024.png")
    preview.resize((128, 128), Image.Resampling.NEAREST).save(work / "workshop-preview-128.png")
    print(f"[OK] {output}: 256x256, {output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
