"""Validate the handbook and detached SDK; render an assembly-derived API index."""
from __future__ import annotations

import argparse
import html
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit


def prose(text: str) -> str:
    """Exclude fenced examples, whose sample links/headings are not document prose."""
    result, fence = [], None
    for line in text.splitlines():
        match = re.match(r"^\s*(`{3,}|~{3,})", line)
        if match:
            marker = match[1]
            if fence is None:
                fence = marker
            elif marker[0] == fence[0] and len(marker) >= len(fence):
                fence = None
            result.append("")
        else:
            result.append(line if fence is None else "")
    return "\n".join(result)


def anchors(text: str) -> set[str]:
    result, counts = set(), {}
    for heading in re.findall(r"^#{1,6}\s+(.+?)\s*#*\s*$", prose(text), re.M):
        heading = re.sub(r"`+([^`]+)`+", lambda m: html.escape(m[1]), heading)
        heading = re.sub(r"\[([^]]+)\]\([^)]*\)", r"\1", heading)
        heading = re.sub(r"<[^>]+>", "", heading)
        slug = re.sub(r"[^\w\- ]", "", html.unescape(heading).lower()).replace(" ", "-")
        count = counts.get(slug, 0)
        candidate = slug if count == 0 else f"{slug}-{count}"
        while candidate in result:
            count += 1
            candidate = f"{slug}-{count}"
        counts[slug] = count + 1
        result.add(candidate)
    result.update(re.findall(r'(?:id|name)=["\']([^"\']+)["\']', prose(text)))
    return result


def links(text: str):
    content = prose(text)
    # Inline links/images and reference definitions, with optional quoted titles.
    pattern = r"!?\[[^]\n]*\]\(\s*(<[^>]+>|[^\s)]+)(?:\s+\"[^\"]*\")?\s*\)"
    for match in re.finditer(pattern, content):
        yield match[1].strip("<>")
    for match in re.finditer(r"^\s*\[[^]]+\]:\s*(<[^>]+>|\S+)", content, re.M):
        yield match[1].strip("<>")


def check_links(root: Path, files: list[Path], portable: bool = False) -> list[str]:
    errors = []
    for file in files:
        for link in links(file.read_text(encoding="utf-8-sig")):
            parsed = urlsplit(link)
            if parsed.scheme or parsed.netloc:
                continue
            target = (file.parent / unquote(parsed.path)).resolve() if parsed.path else file.resolve()
            if portable and not target.is_relative_to(root.resolve()):
                errors.append(f"{file.relative_to(root)}: link escapes SDK: {link}")
                continue
            if not target.exists():
                errors.append(f"{file.relative_to(root)}: missing link: {link}")
            elif parsed.fragment and target.suffix.lower() == ".md":
                if unquote(parsed.fragment) not in anchors(target.read_text(encoding="utf-8-sig")):
                    errors.append(f"{file.relative_to(root)}: missing heading: {link}")
    return errors


def check_api_mentions(text: str, index: dict) -> list[str]:
    """Check SDK claims and named entry-point members against compiled metadata."""
    errors = []
    for type_name in ("JKRuntime.RuntimeApi", "JKRuntime.UI.UIApi"):
        definition = next((t for t in index["types"] if t["name"] == type_name), None)
        if definition is None:
            continue
        short = type_name.rsplit(".", 1)[1]
        for member in sorted(set(re.findall(r"\b" + short + r"\.([A-Z]\w*)\b", text))):
            if member not in definition["staticMembers"]:
                errors.append(f"Unknown public entry point: {short}.{member}")
    current = ".".join(index["runtimeVersion"].split(".")[:2])
    for version in re.findall(r"Current SDK:\s*\*\*([\d.]+)\*\*", text):
        if version != current:
            errors.append(f"Stale SDK version {version}; built SDK is {current}")
    return errors


def render_api(source: Path, sdk: Path, index: dict) -> int:
    routes = json.loads((source / "docs/api-guides.json").read_text(encoding="utf-8-sig"))
    missing = sorted({t["ns"] for t in index["types"]} - routes.keys())
    if missing:
        raise ValueError("Public namespaces need guide routes: " + ", ".join(missing))
    lines = ["# Public API signature reference", "", f"Assembly: `{index['assembly']}`.", "",
             "Generated from the built DLL. Public/protected declared members are listed;",
             "inherited members belong to their declaring type. Property accessors show",
             "their individual visibility. This is reflection notation, not a compilable",
             "source file. Available type summaries come from JKRuntime.xml; IDE XML",
             "also contains member documentation. Follow each semantic guide for lifetimes,",
             "threading, side effects and failure behavior.", "", "## Feature flags", "",
             "These are API availability flags, not active providers or device health.",
             "Use the exact spelling below. Runtime flags and UI page-tools/pointer-drag",
             "flags are case-sensitive; other UI flags compare case-insensitively.", ""]
    for file, service in [("src/Runtime/RuntimeApi.cs", "RuntimeApi"), ("src/UI/UIApi.cs", "UIApi")]:
        text = (source / file).read_text(encoding="utf-8-sig")
        match = re.search(r"public static bool Supports\(string \w+\)\s*\{([^}]+)\}", text)
        if not match:
            raise ValueError(f"Review feature extraction after Supports implementation change: {file}")
        flags = sorted(set(re.findall(r'"([a-z][a-z0-9-]+)"', match[1])))
        if not flags:
            raise ValueError(f"No feature strings extracted: {file}")
        lines += [f"### {service}.Supports", "", *[f"- `{f}`" for f in flags], ""]
    for ns in sorted(routes):
        group = [t for t in index["types"] if t["ns"] == ns]
        if not group:
            raise ValueError(f"Stale public namespace guide route: {ns}")
        lines += [f"## {ns}", "", f"[Semantic guide]({routes[ns]})", ""]
        lines += [f"- [`{t['name']}`](#{next(iter(anchors('### `' + t['name'] + '`')))})" for t in group]
        lines += [""]
        for t in group:
            lines += [f"### `{t['name']}`", ""]
            if t["summary"]:
                lines += [t["summary"], ""]
            bases = ([t["baseType"]] if t["baseType"] else []) + t["interfaces"]
            lines += ["```text", f"{t['kind']} {t['name']}{t['constraints']}"]
            if bases:
                lines += ["Base/interfaces: " + ", ".join(bases)]
            lines += t["members"] + ["```", ""]
    (sdk / "PublicApi.md").write_text("\n".join(lines), encoding="utf-8")
    return len(index["types"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--sdk", type=Path, required=True)
    parser.add_argument("--api-index", type=Path, required=True)
    args = parser.parse_args()
    source, sdk = args.source.resolve(), args.sdk.resolve()
    index = json.loads(args.api_index.read_text(encoding="utf-8-sig"))
    count = render_api(source, sdk, index)
    source_files = list(source.glob("*.md")) + list((source / "docs").rglob("*.md"))
    sdk_files = list(sdk.glob("*.md")) + list((sdk / "docs").rglob("*.md"))
    errors = check_links(source, source_files) + check_links(sdk, sdk_files, portable=True)
    for file in source_files:
        if "CHANGELOG" not in file.name:
            errors += [f"{file}: {error}" for error in check_api_mentions(file.read_text(encoding="utf-8-sig"), index)]
    template = sdk / "ModEntry.cs"
    if not template.is_file() or template.read_bytes() != (source / "examples/UiModExample.cs").read_bytes():
        errors.append("SDK entry point must match the compiled canonical UI example")
    shared = list((source / "docs").rglob("*.md")) + [source / "CHANGELOG.md"]
    for origin in shared:
        exported = sdk / origin.relative_to(source)
        if not exported.is_file() or exported.read_bytes() != origin.read_bytes():
            errors.append(f"Handbook missing or differs in SDK: {origin.relative_to(source)}")
    for origin in (source / "examples").rglob("*"):
        if origin.is_file():
            exported = sdk / "examples" / origin.relative_to(source / "examples")
            if not exported.is_file() or exported.read_bytes() != origin.read_bytes():
                errors.append(f"Example missing or differs in SDK: {origin.name}")
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"[OK] Documentation: {len(source_files)} source / {len(sdk_files)} SDK pages; {count} public types routed; examples exported")


if __name__ == "__main__":
    main()
