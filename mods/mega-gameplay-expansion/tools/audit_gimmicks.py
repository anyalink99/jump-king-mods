"""Read-only installed-content inventory for the gimmick browser design.

This tool never loads a DLL or calls a block factory. Colour candidates come
from the reviewed registry, not from executing downloaded code. Discovery is
not proof that a mechanic can be activated or that a factory accepts a map.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import struct
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[3]
KNOWN_GAME = "476f2033b8b614ec97b04311799b2b78239397a2fe8018c77bb45a1f946ffc88"
MAX_TEXTURE_BYTES = 64 * 1024 * 1024
NATIVE_LABELS = {
    (0, 0, 0): "Solid",
    (255, 0, 0): "Slope",
    (0, 255, 255): "Ice",
    (255, 255, 0): "Snow",
    (255, 106, 0): "Sand",
    (255, 255, 255): "No wind",
    (0, 170, 170): "Water",
    (182, 255, 0): "Quark",
    (128, 128, 128): "Fake / slope support",
}


def digest(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(chunk)
    return sha.hexdigest()


def read_exact(stream: io.BytesIO, count: int) -> bytes:
    data = stream.read(count)
    if len(data) != count:
        raise ValueError("Truncated texture")
    return data


def read_seven(stream: io.BytesIO) -> int:
    value = 0
    for shift in range(0, 35, 7):
        byte = read_exact(stream, 1)[0]
        if shift == 28 and byte > 15:
            raise ValueError("Invalid 7-bit integer")
        value |= (byte & 127) << shift
        if byte < 128:
            return value
    raise ValueError("Invalid 7-bit integer")


def decode_texture(data: bytes) -> tuple[int, int, bytes]:
    """Decode the uncompressed RGBA XNB format present in the audited install.

    Unsupported files are reported, never silently substituted with level.png:
    the PNG beside a compiled map may be an outdated authoring source.
    """
    if len(data) > MAX_TEXTURE_BYTES:
        raise ValueError("Texture exceeds the audit size limit")
    stream = io.BytesIO(data)
    if read_exact(stream, 4) != b"XNBw":
        raise ValueError("Expected Windows XNB")
    version, flags, length = struct.unpack("<BBI", read_exact(stream, 6))
    if version != 5 or flags & 0xC0:
        raise ValueError("Only uncompressed XNB5 is supported by this offline audit")
    if length != len(data):
        raise ValueError("XNB length mismatch")
    reader_count = read_seven(stream)
    if not 1 <= reader_count <= 64:
        raise ValueError("Invalid reader count")
    readers = []
    for _ in range(reader_count):
        length = read_seven(stream)
        if length > 4096:
            raise ValueError("Reader name too long")
        readers.append(read_exact(stream, length).decode("utf-8"))
        read_exact(stream, 4)
    if read_seven(stream) != 0:
        raise ValueError("Shared resources are unsupported")
    reader = read_seven(stream)
    if not 1 <= reader <= len(readers) or not readers[reader - 1].startswith(
        "Microsoft.Xna.Framework.Content.Texture2DReader"
    ):
        raise ValueError("Expected Texture2D reader")
    surface, width, height, mips, size = struct.unpack("<iiiii", read_exact(stream, 20))
    if surface != 0 or mips < 1:
        raise ValueError("Expected RGBA Color texture with mip data")
    if width <= 0 or height <= 0 or width * height * 4 != size:
        raise ValueError("Invalid texture dimensions/payload")
    return width, height, read_exact(stream, size)


def screen_colours(width: int, height: int, rgba: bytes) -> list[Counter]:
    """Use native LevelTexture.GetCoord column-major screen ordering.

    The native implementation divides by width/60 for both axes. Rectangular
    atlases with fewer vertical cells than that can address outside the image;
    report them instead of silently inventing a different ordering.
    """
    if width % 60 or height % 45 or len(rgba) != width * height * 4:
        raise ValueError("Collision atlas must contain complete 60x45 screens")
    columns, rows = width // 60, height // 45
    result = []
    for screen in range(columns * rows):
        left, top = screen // columns * 60, screen % columns * 45
        if left + 60 > width or top + 45 > height:
            raise ValueError("Atlas dimensions do not support native screen addressing")
        counts: Counter = Counter()
        for y in range(top, top + 45):
            for x in range(left, left + 60):
                offset = (y * width + x) * 4
                pixel = tuple(rgba[offset : offset + 4])
                if pixel[3]:
                    counts[pixel] += 1
        result.append(counts)
    return result


def locations(path: Path) -> list[dict]:
    if not path.exists():
        return []
    result = []
    for number, node in enumerate(ET.parse(path).getroot().findall("./locations/Location"), 1):
        start, end = int(node.findtext("start", "0")), int(node.findtext("end", "0"))
        if start < 1 or end < start:
            raise ValueError("Invalid region screen range")
        result.append(
            {
                "number": number,
                "name": node.findtext("name", ""),
                "first_screen": start,
                "last_screen": end,
            }
        )
    return result


def regions_for(screen: int, regions: list[dict]) -> list[int]:
    # Inclusive and possibly overlapping, matching LocationComp.
    return [r["number"] for r in regions if r["first_screen"] <= screen <= r["last_screen"]]


def inventory(game: Path, workshop: Path, catalog: dict) -> list[dict]:
    reviewed = {(a["workshop"], a["file"]): a["sha256"] for a in catalog["assemblies"]}
    result = []
    roots = [("local", game / "Content" / "JKMods")]
    if workshop.is_dir():
        roots.extend((p.name, p) for p in sorted(workshop.iterdir()) if p.is_dir())
    for owner, root in roots:
        if not root.is_dir():
            continue
        for file in sorted(root.rglob("*.dll")):
            # Library inventory is kept separate from mechanic evidence.
            sha = digest(file)
            expected = reviewed.get((owner, file.name))
            result.append(
                {
                    "owner": owner,
                    "file": file.name,
                    "path": str(file),
                    "sha256": sha,
                    "role": "dependency" if file.name == "0Harmony.dll" else "assembly",
                    "registry_status": "matched"
                    if sha == expected
                    else "changed"
                    if expected
                    else "not-in-snapshot",
                }
            )
    return result


def candidates(catalog: dict, installed: list[dict], game_hash: str) -> dict:
    by_code: dict = defaultdict(list)
    for factory in catalog["factories"]:
        owner = factory["owner"]
        if owner == "vanilla":
            status = "matched" if game_hash == KNOWN_GAME else "changed"
        else:
            # Embedded module names differ from the shell; the owner fingerprint
            # must be matched via the audited assembly inventory, not its name.
            audited = [
                a
                for a in catalog["assemblies"]
                if a["workshop"] == owner
                and (
                    a["file"][:-4] == factory.get("assembly")
                    or factory.get("assembly", "").endswith(".Module")
                )
            ]
            matches = [
                a
                for a in installed
                if a["owner"] == owner and any(a["file"] == b["file"] for b in audited)
            ]
            status = (
                "matched"
                if matches and all(a["registry_status"] == "matched" for a in matches)
                else "unverified"
            )
        labels = {tuple(label["rgb"]): label["name"] for label in factory.get("labels", [])}
        for red, green, first, last in factory["rgb_ranges"]:
            for blue in range(first, last + 1):
                rgb = (red, green, blue)
                label = labels.get(rgb, NATIVE_LABELS.get(rgb, "") if owner == "vanilla" else "")
                by_code[rgb + (255,)].append(
                    {
                        "owner": owner,
                        "factory": factory["factory"],
                        "label": label,
                        "evidence": status,
                    }
                )
    for item in catalog.get("reservations", []):
        by_code[tuple(item["rgb"]) + (255,)].append(
            {
                "owner": item["owner"],
                "factory": "source reservation",
                "label": item["name"],
                "evidence": "reservation-only",
            }
        )
    return by_code


def audit_map(root: Path, map_id: str, by_code: dict) -> dict:
    result = {"id": map_id, "root": str(root), "errors": []}
    settings = root / "level_settings.xml"
    if settings.exists():
        try:
            xml = ET.parse(settings).getroot()
            result["title"] = xml.findtext("./About/title") or root.name
            result["tags"] = [n.text or "" for n in xml.findall("./Tags/string")]
        except (ValueError, ET.ParseError, OSError) as error:
            result["errors"].append("level_settings: " + str(error))
    else:
        result.update(title="Jump King / New Babe+ / Ghost of the Babe", tags=[])
    try:
        result["regions"] = locations(root / "gui" / "location_settings.xml")
    except (ValueError, ET.ParseError, OSError) as error:
        result["regions"] = []
        result["errors"].append("locations: " + str(error))
    path = root / "level.xnb"
    try:
        if path.stat().st_size > MAX_TEXTURE_BYTES:
            raise ValueError("Texture exceeds the audit size limit")
        data = path.read_bytes()
        width, height, rgba = decode_texture(data)
        screens = screen_colours(width, height, rgba)
        result.update(
            texture_sha256=hashlib.sha256(data).hexdigest(),
            width=width,
            height=height,
            atlas_screen_capacity=len(screens),
        )
        occurrences: dict = defaultdict(list)
        for screen, counts in enumerate(screens, 1):
            for code, count in sorted(counts.items()):
                occurrences[code].append(
                    {
                        "screen": screen,
                        "regions": regions_for(screen, result["regions"]),
                        "pixels": count,
                    }
                )
        result["colours"] = [
            {
                "rgba": list(code),
                "candidates": by_code.get(code, []),
                "occurrences": occurrences[code],
            }
            for code in sorted(occurrences)
        ]
        result["nonempty_screens"] = sum(bool(s) for s in screens)
    except (ValueError, OSError, struct.error, UnicodeError) as error:
        result["errors"].append("level.xnb: " + str(error))
    return result


def audit(game: Path, workshop: Path, catalog_path: Path) -> dict:
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    installed = inventory(game, workshop, catalog)
    game_hash = digest(game / "JumpKing.exe")
    by_code = candidates(catalog, installed, game_hash)
    roots = [("native", game / "Content")]
    if workshop.is_dir():
        roots.extend(
            (p.parent.relative_to(workshop).as_posix(), p.parent)
            for p in sorted(workshop.rglob("level_settings.xml"))
        )
    maps = [audit_map(root, map_id, by_code) for map_id, root in roots]
    return {
        "schema": 1,
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "scope": "Offline files only; candidates are not effective factory ownership or activation support.",
        "game_sha256": game_hash,
        "registry_audit_date": catalog.get("auditDate"),
        "assemblies": installed,
        "maps": maps,
        "catalogue_colour_count": len(by_code),
        "overlapping_candidate_colours": [
            {"rgba": list(code), "candidates": entries}
            for code, entries in sorted(by_code.items())
            if len(entries) > 1
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--game-dir",
        type=Path,
        default=Path("C:/Program Files (x86)/Steam/steamapps/common/Jump King"),
    )
    parser.add_argument("--workshop-dir", type=Path)
    parser.add_argument(
        "--catalog", type=Path, default=REPOSITORY / "docs/modding/block-registry/catalog.json"
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=REPOSITORY / "build/mega-gameplay-expansion/research/installed-gimmicks.json",
    )
    args = parser.parse_args()
    workshop = args.workshop_dir or args.game_dir.parent.parent / "workshop/content/1061090"
    output = args.output.resolve()
    # This audit must not create or overwrite a file in an installed-content tree.
    for source in (args.game_dir.resolve(), workshop.resolve()):
        if output.is_relative_to(source):
            parser.error("Output must be outside the game and Workshop directories")
    if output == args.catalog.resolve():
        parser.error("Output must not replace the reviewed catalogue")
    report = audit(args.game_dir, workshop, args.catalog)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    mods = [a for a in report["assemblies"] if a["role"] == "assembly"]
    print(f"[OK] {len(mods)} assembly files; {len(report['maps'])} collision atlases")
    for item in report["maps"]:
        print(
            f"  {item['id']}: {item.get('title', '?')}; {len(item['regions'])} regions; "
            f"{len(item.get('colours', []))} nontransparent colours; errors={len(item['errors'])}"
        )
    print(f"[OUTPUT] {output}")
    return 1 if any(m["errors"] for m in report["maps"]) else 0


if __name__ == "__main__":
    raise SystemExit(main())
