"""Compose the Workshop artwork with a separate, native-font title."""

import argparse
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def compact_palette(source: Image.Image) -> Image.Image:
    """Reserve small saturated details instead of averaging them into stone."""
    rgb = source.convert("RGB")
    base = rgb.quantize(colors=48, method=Image.Quantize.MEDIANCUT)
    palette = base.getpalette()[:48 * 3]
    accents = []
    for red, green, blue in rgb.get_flattened_data():
        if (green > red + 40 and blue > red + 40) or (
            green > red + 70 and green > blue + 40
        ) or (blue > red + 70 and blue > green + 40) or (
            red > 160 and green < 80 and blue < 80
        ):
            accents.append((red, green, blue))
    if not accents:
        raise ValueError("Expected the King's cyan helmet and RGB editor axes")
    accent_strip = Image.new("RGB", (len(accents), 1))
    accent_strip.putdata(accents)
    accent_palette = accent_strip.quantize(colors=11, method=Image.Quantize.MEDIANCUT)
    palette.extend(accent_palette.getpalette()[:11 * 3])
    for color in ((0, 255, 0), (255, 0, 0), (0, 70, 255),
                  (255, 232, 177), (61, 42, 24)):
        palette.extend(color)
    palette.extend(palette[:3] * (256 - len(palette) // 3))
    palette_image = Image.new("P", (1, 1))
    palette_image.putpalette(palette)
    return rgb.quantize(palette=palette_image, dither=Image.Dither.NONE)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Jump King"))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    font_path = args.game_dir / "Content/font/ttf_litter_lover2_bold.ttf"
    with Image.open(root / "assets/workshop-preview-art-v2.png") as art:
        if art.width != art.height:
            raise ValueError("Expected square artwork; do not silently crop it")
        canvas = art.convert("RGBA").resize((256, 256), Image.Resampling.NEAREST)
        master = art.convert("RGBA").resize((1024, 1024), Image.Resampling.LANCZOS)
    title = Image.new("RGBA", (256, 256))

    # Match Mega Gameplay Expansion: cream native pixel type, brown offset
    # shadow, two centered lines. No generated glyphs or scene repainting.
    for text, size, y in (("MEGA MAPPING", 25, 18), ("EXPANSION", 31, 45)):
        font = ImageFont.truetype(str(font_path), size)
        left, top, right, bottom = font.getbbox(text)
        mask = Image.new("L", (right - left, bottom - top))
        ImageDraw.Draw(mask).text((-left, -top), text, font=font, fill=255)
        mask = mask.point(lambda value: 255 if value >= 128 else 0)
        if mask.width > 236 or y + mask.height > 72:
            raise ValueError("Title exceeds its reserved safe area")
        x = (256 - mask.width) // 2
        for color, offset in (((61, 42, 24, 255), 2), ((255, 232, 177, 255), 0)):
            layer = Image.new("RGBA", mask.size, color)
            layer.putalpha(mask)
            title.alpha_composite(layer, (x + offset, y + offset))

    canvas.alpha_composite(title)
    master.alpha_composite(title.resize((1024, 1024), Image.Resampling.NEAREST))
    master.convert("RGB").save(root / "assets/workshop-preview-master.png", optimize=True)

    output = args.output or root / "workshop-preview.png"
    preview = compact_palette(canvas)
    encoded = BytesIO()
    preview.save(encoded, format="PNG", optimize=True, compress_level=9)
    if len(encoded.getvalue()) >= 34_000:
        raise ValueError("Workshop preview must be strictly below 34,000 bytes")
    output.write_bytes(encoded.getvalue())
    with Image.open(output) as check:
        check.load()
        if check.size != (256, 256) or check.format != "PNG":
            raise ValueError("Preview format verification failed")
    work = root.parents[1] / "build/mega-mapping-expansion/PREVIEW_WORK"
    work.mkdir(parents=True, exist_ok=True)
    preview.resize((1024, 1024), Image.Resampling.NEAREST).save(work / "workshop-preview-1024.png")
    preview.resize((128, 128), Image.Resampling.NEAREST).save(work / "workshop-preview-128.png")
    print(f"[OK] {output}: 256x256, {output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
