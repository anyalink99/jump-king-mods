"""Validate a portable release without starting the editor or contacting Steam."""
import argparse
import hashlib
from pathlib import Path, PurePosixPath
import re
import struct
import xml.etree.ElementTree as ET
import zipfile

MANIFEST = "package-manifest.xml"
MAX_EXPANDED = 256 * 1024 * 1024


def safe_name(name):
    if not name or len(name) > 200 or any(ord(c) < 32 or c in '\\:*?"<>|' for c in name):
        raise ValueError("Unsafe package path: " + name)
    for part in name.split("/"):
        if part in ("", ".", "..") or part.endswith((" ", ".")) or re.match(r"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", part, re.I):
            raise ValueError("Unsafe package path: " + name)
    if name.lower() in ("verified.xml", "worldsmith-path.txt", "game-path.txt"):
        raise ValueError("Local settings aren't release files: " + name)


def write_manifest(folder, version):
    if not re.fullmatch(r"\d{1,5}\.\d{1,5}\.\d{1,5}(?:-preview\.[1-9]\d{0,5})?", version):
        raise ValueError("Invalid package version")
    root = ET.Element("Package", version="1", release=version)
    for path in sorted(folder.rglob("*")):
        if path.is_symlink():
            raise ValueError("Linked package input: " + str(path))
        if not path.is_file() or path == folder / MANIFEST:
            continue
        name = path.relative_to(folder).as_posix()
        safe_name(name)
        data = path.read_bytes()
        ET.SubElement(root, "File", name=name, size=str(len(data)), sha256=hashlib.sha256(data).hexdigest())
    ET.indent(root)
    ET.ElementTree(root).write(folder / MANIFEST, encoding="utf-8", xml_declaration=True)


def verify(path):
    with zipfile.ZipFile(path) as archive:
        files = {item.filename.replace("\\", "/"): item for item in archive.infolist() if not item.is_dir()}
        required = {
            "WorldsmithExtension.exe", "WorldsmithExtension.Worker.exe",
            "WorldsmithExtension.Engine.dll", "WorldsmithExtension.LegacyCollision.dll", "WorldsmithExtension.exe.config",
            "WorldsmithExtension.Worker.exe.config", "0Harmony.dll",
            "mapping/SceneCacheCompiler.exe", "mapping/MegaMappingApi.dll", "mapping/JKRuntime.dll",
            "README.md", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md",
            "mapping/THIRD_PARTY_NOTICES.md", "docs/index.md", "docs/development.md",
            "assets/platforms/default.png",
        }
        required.add(MANIFEST)
        missing = required - files.keys()
        if missing:
            raise ValueError("Missing portable dependencies: " + ", ".join(sorted(missing)))
        names = [i.filename.rstrip("/").lower() for i in archive.infolist()]
        if len(names) != len(set(names)) or len(names) > 8192:
            raise ValueError("Duplicate archive entries")
        manifest_data = archive.read(files[MANIFEST])
        if len(manifest_data) > 1024 * 1024 or b"<!DOCTYPE" in manifest_data.upper() or b"<!ENTITY" in manifest_data.upper():
            raise ValueError("Unsafe package manifest")
        root = ET.fromstring(manifest_data)
        if root.tag != "Package" or root.get("version") != "1" or not re.fullmatch(r"\d{1,5}\.\d{1,5}\.\d{1,5}(?:-preview\.[1-9]\d{0,5})?", root.get("release", "")):
            raise ValueError("Unsupported package manifest")
        expected = {}
        for entry in root:
            name = entry.get("name", "")
            safe_name(name)
            if entry.tag != "File" or len(entry) or name.lower() == MANIFEST or name.lower() in {n.lower() for n in expected} or len(expected) >= 4096 or not re.fullmatch(r"[a-f0-9]{64}", entry.get("sha256", "")):
                raise ValueError("Invalid package manifest entry: " + name)
            size = int(entry.get("size", "-1"))
            if size < 0 or size > MAX_EXPANDED:
                raise ValueError("Invalid package file size: " + name)
            expected[name] = entry
        if set(files) != set(expected) | {MANIFEST}:
            raise ValueError("Archive files don't match the package manifest")
        if sum(i.file_size for i in files.values()) > MAX_EXPANDED:
            raise ValueError("Package exceeds the size limit")
        for item in archive.infolist():
            safe_name(item.filename[:-1] if item.is_dir() else item.filename)
            if item.external_attr >> 16 & 0xF000 == 0xA000 or item.external_attr & 0x400:
                raise ValueError("Linked archive entry: " + item.filename)
            if item.is_dir() and not any(n.startswith(item.filename) for n in expected):
                raise ValueError("Unexpected package folder: " + item.filename)
        for name in files:
            parts = PurePosixPath(name)
            if parts.is_absolute() or ".." in parts.parts or ":" in name:
                raise ValueError("Unsafe archive path: " + name)
            data = archive.read(files[name])
            if name in required and not data:
                raise ValueError("Empty release file: " + name)
            if name != MANIFEST and (len(data) != int(expected[name].get("size")) or hashlib.sha256(data).hexdigest() != expected[name].get("sha256")):
                raise ValueError("Package file checksum mismatch: " + name)
        for name, subsystem in (("WorldsmithExtension.exe", 2), ("WorldsmithExtension.Worker.exe", 3)):
            data = archive.read(files[name])
            pe = struct.unpack_from("<I", data, 0x3C)[0]
            if data[:2] != b"MZ" or data[pe:pe + 4] != b"PE\0\0":
                raise ValueError("Invalid executable: " + name)
            if struct.unpack_from("<H", data, pe + 4)[0] != 0x8664:
                raise ValueError("Executable must target Windows x64: " + name)
            if struct.unpack_from("<H", data, pe + 24 + 68)[0] != subsystem:
                raise ValueError("Incorrect GUI/worker subsystem: " + name)
        if archive.testzip() is not None:
            raise ValueError("Corrupt archive payload")
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, nargs="?")
    parser.add_argument("--write-manifest", type=Path)
    parser.add_argument("--version")
    args = parser.parse_args()
    if args.write_manifest:
        if not args.version:
            parser.error("--version is required with --write-manifest")
        write_manifest(args.write_manifest, args.version)
        print("[OK] Package manifest: " + str(args.write_manifest / MANIFEST))
        raise SystemExit(0)
    if not args.archive:
        parser.error("archive is required")
    digest = verify(args.archive)
    print(f"[OK] Portable package and dependencies: {args.archive.name}")
    print(f"SHA256 {digest}")
