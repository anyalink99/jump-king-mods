"""Check mod guide placement, entry points and local Markdown links."""
from __future__ import annotations

import argparse
from pathlib import Path
import runpy

REPO = Path(__file__).resolve().parents[1]
MARKDOWN = runpy.run_path(str(REPO / "mods/jk-runtime/tools/check_docs.py"))
ROOT_DOCUMENTS = {"README.md", "CHANGELOG.md", "WORKSHOP.md", "LICENSE.md",
                  "CREDITS.md", "THIRD_PARTY_NOTICES.md", "AGENTS.md"}


def check_mod(root: Path) -> list[str]:
    errors = []
    root = root.resolve()
    guides = sorted((root / "docs").rglob("*.md"))
    documents = sorted(p for p in root.glob("*.md")
                       if p.name not in {"WORKSHOP.md", "AGENTS.md"})
    # Workshop copy uses Steam BBCode, not Markdown; [i](max) is not a link.
    for path in documents:
        if path.name not in ROOT_DOCUMENTS:
            errors.append(f"{path.name}: technical guides belong under docs/")
    if guides:
        index = root / "docs/index.md"
        readme = root / "README.md"
        if not index.is_file():
            errors.append("docs/index.md: missing guide entry point")
        if not readme.is_file():
            errors.append("README.md: missing mod entry point")
        else:
            targets = {link.split('#', 1)[0] for link in
                       MARKDOWN["links"](readme.read_text(encoding="utf-8-sig"))}
            if "docs/index.md" not in targets:
                errors.append("README.md: link to docs/index.md is missing")
    errors += MARKDOWN["check_links"](root, documents + guides)
    return errors


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mod", action="append", help="Mod directory name; repeat to select several")
    args = parser.parse_args()
    roots = [REPO / "mods" / name for name in args.mod] if args.mod else sorted((REPO / "mods").iterdir())
    errors, checked = [], 0
    for root in roots:
        if not root.is_dir() or not root.resolve().is_relative_to(REPO / "mods"):
            if args.mod:
                errors.append(f"Unknown mod directory: {root.name}")
            continue
        checked += 1
        errors += [f"{root.name}: {error}" for error in check_mod(root)]
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"[OK] Documentation layout and links: {checked} mod directories")


if __name__ == "__main__":
    main()
