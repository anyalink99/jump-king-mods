"""Keep two rollback copies and archive data before retiring older history."""

from __future__ import annotations

import argparse
import contextlib
import gzip
import hashlib
import json
import os
import re
import shutil
import stat
import sys
import time
import uuid
import zipfile
from pathlib import Path

COMPILED = {".dll", ".exe", ".pdb", ".xnb", ".jkmod", ".netmodule", ".obj", ".pyc"}
GUID = r"[0-9a-f]{32}"
COMPONENTS = (
    "All",
    "Runtime",
    "Mapping",
    "Worldsmith",
    "Wardrobe",
    "Multiplayer",
    "StereoMadness",
    "TotalEclipse",
    "Prism",
    "Metroidvania",
    "ScreenSolver",
)


def digest(stream):
    value = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        value.update(chunk)
    return value.hexdigest()


def file_digest(path):
    with path.open("rb") as stream:
        return digest(stream)


class History:
    def __init__(self, root, *, apply=False, processes=(), preserve=(), now=None, minimum_age=3600):
        self.root = Path(os.path.abspath(root))
        self.apply = apply
        self.processes = [str(value).replace("/", "\\").casefold() for value in processes]
        self.preserve = [Path(os.path.abspath(value)) for value in preserve]
        self.now = time.time() if now is None else now
        self.minimum_age = minimum_age
        self.store = self.root / "_work" / "preserved-history"
        self.result = {
            "retired": [],
            "kept": {},
            "compressed_logs": 0,
            "log_bytes_saved": 0,
            "skipped": [],
            "links_skipped": [],
            "archives": [],
        }
        self.checked(self.root, allow_root=True)

    def checked(self, path, *, allow_root=False):
        path = Path(os.path.abspath(path))
        if path == self.root:
            if not allow_root:
                raise ValueError("Refusing to change the build root")
        elif self.root not in path.parents:
            raise ValueError(f"History path escaped build: {path}")
        # resolve nothing through a junction, including one above build/
        for ancestor in (path, *path.parents):
            if ancestor.exists() or ancestor.is_symlink():
                attrs = ancestor.lstat()
                if stat.S_ISLNK(attrs.st_mode) or getattr(attrs, "st_file_attributes", 0) & 1024:
                    raise ValueError(f"Linked history path: {ancestor}")
        return path

    def relative(self, path):
        return path.relative_to(self.root).as_posix()

    def busy(self, path):
        normalized = str(path).replace("/", "\\").casefold()
        return any(normalized in process for process in self.processes) or any(
            path == value or path in value.parents for value in self.preserve
        )

    def files(self, root):
        root = self.checked(root)
        found = []
        for directory, children, names in os.walk(root, followlinks=False):
            children.sort()
            for name in children:
                self.checked(Path(directory) / name)
            for name in sorted(names):
                path = self.checked(Path(directory) / name)
                metadata = path.stat()
                found.append((path, metadata.st_size, metadata.st_mtime_ns))
        return found

    def unchanged(self, snapshot):
        for path, size, modified in snapshot:
            self.checked(path)
            metadata = path.stat()
            if (metadata.st_size, metadata.st_mtime_ns) != (size, modified):
                raise RuntimeError(f"History changed while archiving: {path}")

    def has_payload(self, root):
        for directory, children, names in os.walk(self.checked(root), followlinks=False):
            for name in children:
                entry = Path(directory) / name
                attrs = entry.lstat()
                if stat.S_ISLNK(attrs.st_mode) or getattr(attrs, "st_file_attributes", 0) & 1024:
                    raise ValueError(f"Linked history path: {entry}")
            for name in names:
                if name not in ("manifest.json", "receipt.json", ".backup-complete"):
                    self.checked(Path(directory) / name)
                    return True
        return False

    def remove_tree(self, root, snapshot):
        self.checked(root)
        self.unchanged(snapshot)
        # don't delete a file that appeared after the preservation pass
        if {p for p, _, _ in self.files(root)} != {p for p, _, _ in snapshot}:
            raise RuntimeError(f"History contents changed: {root}")
        for path, _, _ in snapshot:
            self.checked(path)
            if not os.access(path, os.W_OK):
                path.chmod(path.stat().st_mode | stat.S_IWRITE)
            path.unlink()
        for directory, children, _ in os.walk(root, topdown=False, followlinks=False):
            for child in children:
                self.checked(Path(directory) / child).rmdir()
        self.checked(root).rmdir()

    def preserve_trees(self, component, roots, *, full=False):
        if not roots:
            return
        snapshots = [(path, self.files(path)) for path in roots]
        if not self.apply:
            self.result["retired"].extend(self.relative(path) for path in roots)
            return
        archive = self.checked(self.store / f"{component}.zip")
        archive.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.checked(archive.with_name(archive.name + f".{uuid.uuid4().hex}.tmp"))
        if archive.exists():
            shutil.copy2(archive, temporary)
        references = set()
        try:
            with zipfile.ZipFile(
                temporary,
                "a",
                compression=zipfile.ZIP_DEFLATED,
                compresslevel=6,
                allowZip64=True,
                strict_timestamps=False,
            ) as output:
                present = set(output.namelist())
                for root, snapshot in snapshots:
                    manifest = {
                        "source": self.relative(root),
                        "full": full,
                        "files": [],
                        "omitted_compiled_files": [],
                    }
                    for path, size, modified in snapshot:
                        relative = path.relative_to(root).as_posix()
                        user_data = any(
                            part.casefold()
                            in (
                                "saves",
                                "savesperma",
                                "settings",
                                "source",
                                "src",
                                "editable_source",
                            )
                            for part in path.relative_to(root).parts
                        )
                        if not full and not user_data and path.suffix.casefold() in COMPILED:
                            manifest["omitted_compiled_files"].append(
                                {"path": relative, "bytes": size}
                            )
                            continue
                        value = file_digest(path)
                        entry = f"objects/{value}"
                        if entry not in present:
                            output.write(path, entry)
                            present.add(entry)
                        references.add(value)
                        manifest["files"].append(
                            {"path": relative, "sha256": value, "bytes": size, "mtime_ns": modified}
                        )
                    payload = json.dumps(manifest, ensure_ascii=False, sort_keys=True).encode(
                        "utf-8"
                    )
                    entry = "manifests/" + hashlib.sha256(payload).hexdigest() + ".json"
                    if entry not in present:
                        output.writestr(entry, payload)
                        present.add(entry)
            # read the stored bytes back before anything is removed
            with zipfile.ZipFile(temporary) as saved:
                for value in references:
                    with saved.open(f"objects/{value}") as stream:
                        if digest(stream) != value:
                            raise RuntimeError(f"Archive verification failed: {archive}")
            for _, snapshot in snapshots:
                self.unchanged(snapshot)
            with temporary.open("r+b") as stream:
                os.fsync(stream.fileno())
            self.checked(archive)
            os.replace(temporary, archive)
            for root, snapshot in snapshots:
                self.remove_tree(root, snapshot)
                self.result["retired"].append(self.relative(root))
            self.result["archives"].append(self.relative(archive))
        finally:
            if temporary.exists():
                self.checked(temporary).unlink()

    def series(self, component, parents, pattern, *, keep=2):
        candidates = []
        for parent in parents:
            parent = self.checked(self.root / parent)
            if not parent.exists():
                continue
            for item in parent.iterdir():
                if re.fullmatch(pattern, item.name) and item.is_dir():
                    self.checked(item)
                    candidates.append(item)
        # a backup's creation date survives later inspection of its contents
        candidates.sort(
            key=lambda p: (
                getattr(p.stat(), "st_birthtime", p.stat().st_ctime)
                if os.name == "nt"
                else p.stat().st_mtime,
                p.name,
            ),
            reverse=True,
        )
        if component == "Runtime":
            fixtures = []
            for path in candidates:
                manifest = path / "manifest.json"
                if not manifest.is_file():
                    continue
                records = json.loads(manifest.read_text(encoding="utf-8-sig"))
                records = records if isinstance(records, list) else [records]
                targets = [str(record.get("Target", "")).replace("\\", "/") for record in records]
                if targets and all(
                    "/build/" in target and "/installer-test-" in target for target in targets
                ):
                    if not self.busy(path) and self.now - path.stat().st_mtime >= self.minimum_age:
                        fixtures.append(path)
            self.preserve_trees("Runtime-test-fixtures", fixtures)
            candidates = [path for path in candidates if path not in fixtures]
        empty = []
        for path in candidates:
            if self.busy(path) or (path / ".backup-incomplete").exists():
                continue
            if (
                self.now - path.stat().st_mtime < self.minimum_age
                and not (path / ".backup-complete").exists()
            ):
                continue
            if not self.has_payload(path):
                empty.append(path)
        self.preserve_trees(component + "-empty", empty)
        candidates = [path for path in candidates if path not in empty]
        keepers, retired = [], []
        ordinary = 0
        for path in candidates:
            complete = (path / ".backup-complete").is_file()
            protected = self.busy(path) or (path / ".backup-incomplete").exists()
            recent = self.now - path.stat().st_mtime < self.minimum_age and not complete
            if protected or recent:
                keepers.append(path)
            elif ordinary < keep:
                keepers.append(path)
                ordinary += 1
            else:
                retired.append(path)
        self.result["kept"].setdefault(component, []).extend(
            self.relative(path) for path in keepers
        )
        self.preserve_trees(component, retired)

    def backups(self, component="All", override=None):
        definitions = [
            ("Runtime", ["jk-runtime/_INTERNAL"], rf"release-backup-{GUID}"),
            (
                "StereoMadness",
                ["stereo-madness/_INTERNAL", "_work/stereo-madness"],
                rf"local-install-{GUID}",
            ),
            ("TotalEclipse", ["_work/total-eclipse"], rf"install-{GUID}"),
            (
                "Multiplayer",
                ["_work/multiplayer-expansion/install-backups"],
                rf"\d{{8}}-\d{{6}}(?:-{GUID})?",
            ),
            ("Prism", ["_work/prism/install"], rf"\d{{8}}-\d{{6}}-{GUID}"),
            ("Metroidvania", ["_work/metroidvania/install"], rf"\d{{8}}-\d{{6}}-{GUID}"),
            ("ScreenSolver", ["screen-solver"], rf"install-backup-{GUID}"),
            (
                "Mapping",
                ["_work/backups/local-mod-backups"],
                rf"mega-mapping-install-\d{{8}}-\d{{6}}(?:-{GUID})?",
            ),
        ]
        for name, parents, pattern in definitions:
            if component not in ("All", name):
                continue
            if override is not None:
                parents = [self.relative(self.checked(override))]
            self.series(name, parents, pattern)
        if override is not None:
            return
        # older feature-specific backups share one limit per mod
        mapping = {
            "jk-runtime": "Runtime",
            "wardrobe-plus": "Wardrobe",
            "mega-mapping-expansion": "Mapping",
            "multiplayer-expansion": "Multiplayer",
            "stereo-madness": "StereoMadness",
            "stereo-madness-mod": "StereoMadness",
            "total-eclipse": "TotalEclipse",
            "hammer-king": "more-items",
        }
        for mod in self.root.iterdir():
            if not mod.is_dir() or mod.name.startswith("_"):
                continue
            name = mapping.get(mod.name, mod.name)
            if component not in ("All", name):
                continue
            self.series(
                name + "-feature-backups",
                [mod.name + "/_INTERNAL"],
                rf"(?!release-backup-|local-install-)(?:.*backup.*|before-.*|previous-release)-{GUID}",
            )
        # old feature backups and installer backups still belong to the same mod
        owners = {}
        for category, paths in self.result["kept"].items():
            owner = category.removesuffix("-feature-backups")
            owners.setdefault(owner, set()).update(self.root / path for path in paths)
        for owner, paths in owners.items():
            ordered = sorted(paths, key=lambda p: (p.stat().st_ctime_ns, p.name), reverse=True)
            ordinary, retired = 0, []
            for path in ordered:
                if self.busy(path) or (path / ".backup-incomplete").exists():
                    continue
                if (
                    self.now - path.stat().st_mtime < self.minimum_age
                    and not (path / ".backup-complete").exists()
                ):
                    continue
                if ordinary < 2:
                    ordinary += 1
                else:
                    retired.append(path)
            self.preserve_trees(owner, retired)
            removed = {self.relative(path) for path in retired}
            for category, kept in self.result["kept"].items():
                self.result["kept"][category] = [path for path in kept if path not in removed]
        if component in ("All", "Mapping"):
            parent = self.root / "mega-mapping-expansion/_INTERNAL"
            if parent.exists():
                for kind, filename in [
                    ("packages", "MegaMappingExpansion.dll"),
                    ("kits", "README.md"),
                ]:
                    found = [
                        p
                        for p in parent.iterdir()
                        if re.fullmatch(rf"previous-output-{GUID}", p.name)
                        and (p / filename).is_file()
                        and (kind == "packages" or not (p / "MegaMappingExpansion.dll").is_file())
                    ]
                    found.sort(key=lambda p: (p.stat().st_ctime_ns, p.name), reverse=True)
                    retired = [
                        p
                        for p in found[2:]
                        if not self.busy(p) and self.now - p.stat().st_mtime >= self.minimum_age
                    ]
                    self.preserve_trees("Mapping-" + kind, retired)
        if component in ("All", "Worldsmith"):
            for prefix in ("previous", "previous-release"):
                self.series(
                    "Worldsmith-" + prefix, ["_work/worldsmith-extension"], rf"{prefix}-{GUID}"
                )
        if component == "All":
            legacy = self.root / "_work/backups/_ARCHIVE/legacy-generated-workspace"
            if legacy.exists() and not self.busy(legacy):
                self.preserve_trees("legacy-workspace", [legacy], full=True)

    def logs(self, *, age=86400):
        work = self.checked(self.root / "_work")
        if not work.exists():
            return
        for directory, children, names in os.walk(work, followlinks=False):
            safe = []
            for child in children:
                path = Path(directory) / child
                if path == self.store:
                    continue
                try:
                    attrs = path.lstat()
                    if (
                        stat.S_ISLNK(attrs.st_mode)
                        or getattr(attrs, "st_file_attributes", 0) & 1024
                    ):
                        raise ValueError(f"Linked history path: {path}")
                    safe.append(child)
                except ValueError:
                    self.result["links_skipped"].append(self.relative(path))
            children[:] = safe
            for name in names:
                if not (
                    re.search(r"(?i)\.log(?:\.\d+)?$", name)
                    or name.endswith((".etl", ".etl.xml"))
                    or name in ("verification.xml", "clr.xml")
                ):
                    continue
                source = self.checked(Path(directory) / name)
                metadata = source.stat()
                if (
                    metadata.st_size < 1024
                    or self.now - metadata.st_mtime < age
                    or self.busy(source.parent)
                ):
                    continue
                destination = self.checked(source.with_name(source.name + ".gz"))
                if not self.apply:
                    self.result["compressed_logs"] += 1
                    continue
                self.compress_log(source, destination, metadata)

    def compress_log(self, source, destination, metadata):
        temporary = self.checked(
            destination.with_name(destination.name + f".{uuid.uuid4().hex}.tmp")
        )
        try:
            expected = file_digest(source)
            if destination.exists():
                with gzip.open(destination, "rb") as stream:
                    if digest(stream) != expected:
                        destination = self.checked(
                            source.with_name(source.name + f".{expected[:16]}.gz")
                        )
            if not destination.exists():
                with source.open("rb") as input_stream, temporary.open("xb") as output_stream:
                    with gzip.GzipFile(
                        filename=source.name,
                        fileobj=output_stream,
                        mode="wb",
                        mtime=int(metadata.st_mtime),
                    ) as compressed:
                        shutil.copyfileobj(input_stream, compressed, 1024 * 1024)
                    output_stream.flush()
                    os.fsync(output_stream.fileno())
                with gzip.open(temporary, "rb") as stream:
                    if digest(stream) != expected:
                        raise RuntimeError(f"Compressed log failed verification: {source}")
                self.unchanged([(source, metadata.st_size, metadata.st_mtime_ns)])
                os.replace(temporary, destination)
                os.utime(destination, ns=(metadata.st_atime_ns, metadata.st_mtime_ns))
            else:
                with gzip.open(destination, "rb") as stream:
                    if digest(stream) != expected:
                        raise RuntimeError(f"Existing log archive differs: {destination}")
            self.unchanged([(source, metadata.st_size, metadata.st_mtime_ns)])
            self.checked(source).unlink()
            self.result["compressed_logs"] += 1
            self.result["log_bytes_saved"] += metadata.st_size - destination.stat().st_size
        except (OSError, RuntimeError) as error:
            self.result["skipped"].append(f"{self.relative(source)}: {error}")
        finally:
            if temporary.exists():
                self.checked(temporary).unlink()


@contextlib.contextmanager
def workspace_lock(history):
    path = history.checked(history.store / ".lock")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a+b") as stream:
        if stream.tell() == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield


def restore(history, archive, destination):
    archive = history.checked(archive)
    destination = history.checked(destination)
    count = 0
    with zipfile.ZipFile(archive) as saved:
        for entry in saved.namelist():
            if not entry.startswith("manifests/"):
                continue
            manifest = json.loads(saved.read(entry))
            for record in manifest["files"]:
                relative = Path(manifest["source"]) / record["path"]
                target = history.checked(destination / relative)
                if destination not in target.parents:
                    raise ValueError("Archive restore path escaped its destination")
                if target.exists():
                    if file_digest(target) != record["sha256"]:
                        raise FileExistsError(f"Restore would overwrite different data: {target}")
                    continue
                with saved.open("objects/" + record["sha256"]) as stream:
                    if digest(stream) != record["sha256"]:
                        raise RuntimeError("Recovery object failed verification")
                target.parent.mkdir(parents=True, exist_ok=True)
                with (
                    saved.open("objects/" + record["sha256"]) as source,
                    target.open("xb") as output,
                ):
                    shutil.copyfileobj(source, output)
                os.utime(target, ns=(record["mtime_ns"], record["mtime_ns"]))
                count += 1
    return count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--build-root", type=Path, default=Path(os.path.abspath(__file__)).parents[3] / "build"
    )
    parser.add_argument("--component", choices=COMPONENTS, default="All")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--logs", action="store_true")
    parser.add_argument("--logs-only", action="store_true")
    parser.add_argument("--minimum-age-hours", type=float, default=1)
    parser.add_argument("--backup-directory", type=Path)
    parser.add_argument("--preserve", action="append", default=[])
    parser.add_argument("--restore", type=Path)
    parser.add_argument("--destination", type=Path)
    args = parser.parse_args()
    # keep subst drive spelling aligned with the calling build scripts
    boundary = Path(os.path.abspath(__file__)).parents[3] / "build"
    chosen = Path(os.path.abspath(args.build_root))
    if chosen != boundary and boundary not in chosen.parents:
        raise ValueError("Build root must stay inside this workspace build directory")
    incoming = sys.stdin.read() if not sys.stdin.isatty() else ""
    state = json.loads(incoming) if incoming.strip() else {}
    history = History(
        chosen,
        apply=args.apply,
        processes=state.get("processes", []),
        preserve=args.preserve,
        minimum_age=args.minimum_age_hours * 3600,
    )
    if args.restore:
        if not args.destination:
            parser.error("--restore requires --destination")
        print(json.dumps({"restored": restore(history, args.restore, args.destination)}))
        return
    manager = workspace_lock(history) if args.apply else contextlib.nullcontext()
    try:
        with manager:
            if not args.logs_only:
                history.backups(args.component, args.backup_directory)
            if args.logs or args.logs_only:
                history.logs()
    except OSError as error:
        if getattr(error, "winerror", None) in (32, 33) or error.errno in (11, 13):
            history.result["skipped"].append(f"History is in use: {error}")
        else:
            raise
    print(json.dumps(history.result, ensure_ascii=False))


if __name__ == "__main__":
    main()
