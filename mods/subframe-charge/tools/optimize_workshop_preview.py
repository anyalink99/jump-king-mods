from __future__ import annotations

import argparse
import os
import tempfile
from pathlib import Path

from PIL import Image


RESERVED_COLORS = (
    (2, 5, 13),       # shared background
    (255, 232, 177),  # shared title foreground
    (61, 42, 24),     # shared title shadow
)


def quantize_with_reserved_colors(
    source: Image.Image,
    adaptive_colors: int,
) -> Image.Image:
    rgb = source.convert("RGB")
    indexed = rgb.quantize(
        colors=adaptive_colors,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    )

    palette = indexed.getpalette()
    if palette is None:
        raise RuntimeError("Pillow did not create an indexed palette")
    palette.extend([0] * (768 - len(palette)))
    for offset, color in enumerate(RESERVED_COLORS):
        palette_index = adaptive_colors + offset
        palette[palette_index * 3 : palette_index * 3 + 3] = color
    indexed.putpalette(palette)

    source_pixels = list(rgb.get_flattened_data())
    indexed_pixels = list(indexed.get_flattened_data())
    reserved_indices = {
        color: adaptive_colors + offset
        for offset, color in enumerate(RESERVED_COLORS)
    }
    for index, color in enumerate(source_pixels):
        reserved_index = reserved_indices.get(color)
        if reserved_index is not None:
            indexed_pixels[index] = reserved_index
    indexed.putdata(indexed_pixels)
    return indexed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--max-bytes", type=int, default=34 * 1024)
    args = parser.parse_args()

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(args.source) as source:
        with tempfile.TemporaryDirectory(
            prefix="subframe-preview-",
            dir=args.output.parent,
        ) as temporary_directory:
            selected: Path | None = None
            selected_colors = 0
            for adaptive_colors in (253, 221, 189, 157, 125, 93, 61):
                candidate = Path(temporary_directory) / (
                    f"candidate-{adaptive_colors + len(RESERVED_COLORS)}.png"
                )
                quantized = quantize_with_reserved_colors(
                    source,
                    adaptive_colors,
                )
                quantized.save(candidate, optimize=True, compress_level=9)
                quantized.close()
                if candidate.stat().st_size < args.max_bytes:
                    selected = candidate
                    selected_colors = adaptive_colors + len(RESERVED_COLORS)
                    break

            if selected is None:
                raise RuntimeError(
                    f"Could not fit preview below {args.max_bytes} bytes"
                )
            os.replace(selected, args.output)

    print(
        f"[OK] Optimized Workshop preview: {args.output} "
        f"({args.output.stat().st_size} bytes, {selected_colors} colors)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
