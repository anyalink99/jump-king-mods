import runpy
import tempfile
from pathlib import Path
import unittest

CHECK = runpy.run_path(str(Path(__file__).resolve().parents[1] / "check-mod-docs.py"))["check_mod"]


class ModDocumentationTests(unittest.TestCase):
    def test_portable_guide_links_and_root_entry(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "docs").mkdir()
            (root / "README.md").write_text("# Mod\n[Guides](docs/index.md)\n", encoding="utf-8")
            (root / "docs/index.md").write_text("# Guides\n[API](api.md#calls)\n", encoding="utf-8")
            (root / "docs/api.md").write_text("# API\n## Calls\n[Home](../README.md)\n", encoding="utf-8")
            (root / "WORKSHOP.md").write_text("[i](max)[/i]", encoding="utf-8")
            self.assertEqual(CHECK(root), [])
            (root / "docs/api.md").rename(root / "API.md")
            errors = CHECK(root)
            self.assertTrue(any("technical guides belong" in error for error in errors))
            self.assertTrue(any("missing link" in error for error in errors))

    def test_small_mod_needs_no_empty_handbook(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "README.md").write_text("# Mod\n", encoding="utf-8")
            self.assertEqual(CHECK(root), [])
            (root / "docs").mkdir()
            (root / "docs/api.md").write_text("# API\n", encoding="utf-8")
            errors = CHECK(root)
            self.assertTrue(any("missing guide entry point" in error for error in errors))
            self.assertTrue(any("link to docs/index.md" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
