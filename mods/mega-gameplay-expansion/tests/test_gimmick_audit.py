"""Offline discovery regressions; no installed game or third-party execution."""

import importlib.util
import json
import struct
import tempfile
import unittest
from pathlib import Path

TOOL = Path(__file__).resolve().parents[1] / "tools/audit_gimmicks.py"
SPEC = importlib.util.spec_from_file_location("audit_gimmicks", TOOL)
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


def seven(value):
    data = bytearray()
    while value > 127:
        data.append((value & 127) | 128)
        value >>= 7
    data.append(value)
    return bytes(data)


def texture(width=60, height=45, pixels=None):
    pixels = pixels if pixels is not None else bytes(width * height * 4)
    reader = b"Microsoft.Xna.Framework.Content.Texture2DReader, MonoGame.Framework"
    body = b"\x01" + seven(len(reader)) + reader + bytes(4) + b"\x00\x01"
    body += struct.pack("<iiiii", 0, width, height, 1, len(pixels)) + pixels
    return b"XNBw\x05\x00" + struct.pack("<I", len(body) + 10) + body


class GimmickAuditTests(unittest.TestCase):
    def test_xnb_decodes_compiled_collision_pixels(self):
        pixels = bytes((4, 8, 12, 255)) * (60 * 45)
        self.assertEqual((60, 45, pixels), audit.decode_texture(texture(pixels=pixels)))

    def test_corrupt_and_compressed_textures_are_reported(self):
        valid = texture()
        for data in (
            valid[:-1],
            valid[:5] + b"\x80" + valid[6:],
            valid[:5] + b"\x40" + valid[6:],
            b"PNG" + valid[3:],
        ):
            with self.subTest(data=data[:10]), self.assertRaises(ValueError):
                audit.decode_texture(data)

    def test_native_column_major_screens_and_alpha(self):
        width, height = 120, 90
        pixels = bytearray(width * height * 4)
        for x, y, code in ((0, 0, 1), (0, 45, 2), (60, 0, 3), (60, 45, 4)):
            offset = (y * width + x) * 4
            pixels[offset : offset + 4] = bytes((code, 0, 0, 255))
        pixels[4:8] = bytes((255, 1, 2, 0))
        pixels[8:12] = bytes((99, 1, 2, 128))
        screens = audit.screen_colours(width, height, bytes(pixels))
        for index, counts in enumerate(screens, 1):
            self.assertEqual(1, counts[(index, 0, 0, 255)])
            self.assertNotIn((255, 1, 2, 0), counts)
        self.assertIn((99, 1, 2, 128), screens[0])

    def test_invalid_atlas_does_not_invent_screen_numbers(self):
        for width, height in ((61, 45), (120, 45), (60, 90)):
            with self.subTest(width=width, height=height), self.assertRaises(ValueError):
                audit.screen_colours(width, height, bytes(width * height * 4))

    def test_regions_preserve_order_overlaps_and_unassigned_screens(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "locations.xml"
            path.write_text(
                """<LocationSettings><locations>
                <Location><name>High</name><start>9</start><end>12</end></Location>
                <Location><name>Low</name><start>1</start><end>9</end></Location>
                </locations></LocationSettings>""",
                encoding="utf-8",
            )
            regions = audit.locations(path)
        self.assertEqual([1, 2], audit.regions_for(9, regions))
        self.assertEqual([2], audit.regions_for(1, regions))
        self.assertEqual([], audit.regions_for(13, regions))

    def test_stale_fingerprint_and_collision_are_not_effective_ownership(self):
        catalog = {
            "assemblies": [{"workshop": "1", "file": "Example.dll", "sha256": "expected"}],
            "factories": [
                {
                    "owner": "1",
                    "assembly": "Example",
                    "factory": "Example.Factory",
                    "rgb_ranges": [[1, 2, 3, 4]],
                    "labels": [],
                },
                {
                    "owner": "vanilla",
                    "factory": "Native",
                    "rgb_ranges": [[1, 2, 3, 3]],
                    "labels": [],
                },
            ],
            "reservations": [],
        }
        entries = audit.candidates(
            catalog,
            [{"owner": "1", "file": "Example.dll", "registry_status": "changed"}],
            audit.KNOWN_GAME,
        )
        self.assertEqual(2, len(entries[(1, 2, 3, 255)]))
        self.assertEqual("unverified", entries[(1, 2, 3, 255)][0]["evidence"])
        self.assertEqual("matched", entries[(1, 2, 3, 255)][1]["evidence"])
        self.assertNotIn((1, 2, 3, 128), entries)

    def test_compiled_texture_wins_and_scan_never_executes_dll(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            game, workshop = root / "game", root / "workshop"
            content, map_root = game / "Content", workshop / "123"
            content.mkdir(parents=True)
            map_root.mkdir(parents=True)
            (game / "JumpKing.exe").write_bytes(b"not an executable")
            (content / "level.xnb").write_bytes(texture())
            (map_root / "Example.dll").write_bytes(b"not a DLL")
            (map_root / "level_settings.xml").write_text(
                "<LevelSettings><About><title>Test map</title></About></LevelSettings>",
                encoding="utf-8",
            )
            (map_root / "level.png").write_bytes(b"invalid outdated PNG must never be read")
            (map_root / "level.xnb").write_bytes(texture(pixels=bytes((1, 2, 3, 255)) * 2700))
            catalog_path = root / "catalog.json"
            catalog_path.write_text(
                json.dumps({"assemblies": [], "factories": []}), encoding="utf-8"
            )
            before = {p: p.read_bytes() for p in root.rglob("*") if p.is_file()}
            report = audit.audit(game, workshop, catalog_path)
            self.assertEqual(2, len(report["maps"]))
            self.assertEqual([1, 2, 3, 255], report["maps"][1]["colours"][0]["rgba"])
            self.assertEqual([], report["maps"][1]["colours"][0]["candidates"])
            self.assertEqual([], report["maps"][1]["regions"])
            self.assertEqual(before, {p: p.read_bytes() for p in root.rglob("*") if p.is_file()})

    def test_unreadable_map_remains_in_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "level_settings.xml").write_text("<bad", encoding="utf-8")
            result = audit.audit_map(root, "missing-texture", {})
            self.assertEqual("missing-texture", result["id"])
            self.assertEqual(2, len(result["errors"]))
            self.assertNotIn("colours", result)


if __name__ == "__main__":
    unittest.main()
