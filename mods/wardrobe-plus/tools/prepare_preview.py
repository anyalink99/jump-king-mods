"""Prepare the approved artwork for Workshop without regenerating it."""

from io import BytesIO
from pathlib import Path

from PIL import Image


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    source = root / "assets" / "workshop-preview-master.png"
    output = root / "workshop-preview-256.png"
    with Image.open(source) as master:
        if master.width != master.height:
            raise ValueError("Expected square artwork; do not silently crop it")
        small = master.convert("RGB").resize((256, 256), Image.Resampling.NEAREST)
    # Generated near-black backgrounds contain thousands of imperceptible noise
    # variations. Flatten only those cool dark pixels, preserving colored artwork.
    small.putdata([
        (2, 5, 13) if max(color) <= 18 and color[2] >= color[1] >= color[0] else color
        for color in small.get_flattened_data()
    ])
    indexed = small.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    errors = [
        sum(abs(a - b) for a, b in zip(original, result)) / 3
        for original, result in zip(small.get_flattened_data(), indexed.convert("RGB").get_flattened_data())
        if max(original) > 32
    ]
    if errors and sum(errors) / len(errors) > 3:
        raise ValueError("Palette conversion changes artwork colors too much")
    buffer = BytesIO()
    indexed.save(buffer, format="PNG", optimize=True, compress_level=9)
    payload = buffer.getvalue()
    if len(payload) >= 34 * 1024:
        raise ValueError("Preview exceeds the 34 KiB budget; do not reduce the palette silently")
    with Image.open(BytesIO(payload)) as check:
        check.load()
        if check.format != "PNG" or check.size != (256, 256) or check.mode != "P":
            raise ValueError("Workshop preview format validation failed")
    temporary = output.with_suffix(".pending.png")
    temporary.write_bytes(payload)
    temporary.replace(output)
    print(f"[OK] {output}: 256x256, indexed PNG, 256 palette colors, {len(payload)} bytes")


if __name__ == "__main__":
    main()
