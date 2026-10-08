"""Validate a portable release without starting the editor or contacting Steam."""
import argparse
import hashlib
from pathlib import Path, PurePosixPath
import struct
import zipfile


def verify(path):
    with zipfile.ZipFile(path) as archive:
        files = {item.filename.replace("\\", "/"): item for item in archive.infolist() if not item.is_dir()}
        required = {
            "WorldsmithExtension.exe", "WorldsmithExtension.Worker.exe",
            "WorldsmithExtension.Engine.dll", "WorldsmithExtension.exe.config",
            "WorldsmithExtension.Worker.exe.config", "0Harmony.dll",
            "mapping/SceneCacheCompiler.exe", "mapping/MegaMappingApi.dll", "mapping/JKRuntime.dll",
            "README.md", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md",
            "mapping/THIRD_PARTY_NOTICES.md", "docs/index.md", "docs/development.md",
        }
        missing = required - files.keys()
        if missing:
            raise ValueError("Missing portable dependencies: " + ", ".join(sorted(missing)))
        if len(files) != len([i for i in archive.infolist() if not i.is_dir()]):
            raise ValueError("Duplicate archive entries")
        for name in files:
            parts = PurePosixPath(name)
            if parts.is_absolute() or ".." in parts.parts or ":" in name:
                raise ValueError("Unsafe archive path: " + name)
            if name not in required and not (name.startswith("docs/") and name.endswith(".md")):
                raise ValueError("Unexpected release file: " + name)
            data = archive.read(files[name])
            if not data:
                raise ValueError("Empty release file: " + name)
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
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    digest = verify(args.archive)
    print(f"[OK] Portable package and dependencies: {args.archive.name}")
    print(f"SHA256 {digest}")
