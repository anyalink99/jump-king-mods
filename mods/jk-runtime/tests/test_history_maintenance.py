import gzip
import hashlib
import importlib.util
import json
import os
import subprocess
import tempfile
import time
import unittest
import zipfile
from pathlib import Path
from unittest import mock

SOURCE = Path(__file__).resolve().parents[1] / "build-support/history-maintenance.py"
SPEC = importlib.util.spec_from_file_location("history_maintenance", SOURCE)
history_module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(history_module)
History = history_module.History


class HistoryTests(unittest.TestCase):
    def setUp(self):
        workspace = Path(__file__).resolve().parents[3]
        scratch = workspace / "build/_work"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="history-test-", dir=scratch)
        self.root = Path(self.temporary.name)
        self.now = time.time()

    def tearDown(self):
        self.temporary.cleanup()

    def put(self, relative, data=b"data", old=True):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        if old:
            os.utime(path, (self.now - 172800, self.now - 172800))
        return path

    def backups(self, count=5):
        paths = []
        for index in range(count):
            prefix = f"_work/total-eclipse/install-{index:032x}"
            self.put(prefix + "/previous-map/level.xnb", b"compiled")
            self.put(prefix + "/previous-map/source/map.cs", f"source-{index}".encode())
            self.put(prefix + "/previous-map/source/provider.dll", b"authoring input")
            self.put(prefix + "/previous-map/Settings.json", b"same settings")
            self.put(prefix + "/previous-map/Saves/progress.sav", f"save-{index}".encode())
            self.put(prefix + "/previous-map/art.png", f"art-{index}".encode())
            self.put(prefix + "/.backup-complete", b"")
            path = self.root / prefix
            os.utime(path, (self.now - 10000 + index, self.now - 10000 + index))
            paths.append(path)
        return paths

    def history(self, **kwargs):
        return History(self.root, now=self.now, **kwargs)

    def test_two_backups_and_verified_unique_data_restore(self):
        paths = self.backups()
        h = self.history(apply=True)
        h.backups("TotalEclipse")
        self.assertEqual(sum(path.exists() for path in paths), 2)
        archive = h.store / "TotalEclipse.zip"
        with zipfile.ZipFile(archive) as saved:
            settings = hashlib.sha256(b"same settings").hexdigest()
            self.assertEqual(saved.namelist().count("objects/" + settings), 1)
        destination = self.root / "restored"
        count = history_module.restore(h, archive, destination)
        self.assertEqual(count, 18)
        for path in paths[:3]:
            restored = destination / path.relative_to(self.root) / "previous-map"
            index = int(path.name.split("-")[-1], 16)
            self.assertEqual((restored / "source/map.cs").read_bytes(), f"source-{index}".encode())
            self.assertEqual(
                (restored / "Saves/progress.sav").read_bytes(), f"save-{index}".encode()
            )
            self.assertEqual((restored / "source/provider.dll").read_bytes(), b"authoring input")
            self.assertFalse((restored / "level.xnb").exists())

    def test_preview_does_not_write_or_delete(self):
        paths = self.backups()
        h = self.history()
        h.backups("TotalEclipse")
        self.assertEqual(len(h.result["retired"]), 3)
        self.assertTrue(all(path.exists() for path in paths))
        self.assertFalse(h.store.exists())

    def test_metroidvania_keeps_two_rollbacks_and_unique_characters(self):
        paths = []
        for index in range(4):
            prefix = f"_work/metroidvania/install/20261026-120000-{index:032x}"
            self.put(prefix + "/content/Metroidvania.dll", b"compiled")
            self.put(prefix + "/content/Metroidvania.Character.xml", f"profile-{index}".encode())
            self.put(prefix + "/.backup-complete", b"")
            paths.append(self.root / prefix)
        h = self.history(apply=True)
        h.backups("Metroidvania")
        self.assertEqual(sum(path.exists() for path in paths), 2)
        with zipfile.ZipFile(h.store / "Metroidvania.zip") as saved:
            for index in range(2):
                key = hashlib.sha256(f"profile-{index}".encode()).hexdigest()
                self.assertEqual(saved.read("objects/" + key), f"profile-{index}".encode())

    def test_current_and_incomplete_backups_do_not_consume_two_valid_slots(self):
        paths = self.backups()
        (paths[0] / ".backup-incomplete").write_text("rollback still needs these files")
        h = self.history(apply=True, preserve=[paths[1]])
        h.backups("TotalEclipse")
        self.assertTrue(paths[0].exists())
        self.assertTrue(paths[1].exists())
        self.assertFalse(paths[2].exists())
        self.assertTrue(paths[3].exists())
        self.assertTrue(paths[4].exists())

    def test_archive_failure_leaves_originals(self):
        paths = self.backups()
        h = self.history(apply=True)
        with mock.patch.object(h, "unchanged", side_effect=RuntimeError("changed source")):
            with self.assertRaises(RuntimeError):
                h.backups("TotalEclipse")
        self.assertTrue(all(path.exists() for path in paths))
        self.assertFalse((h.store / "TotalEclipse.zip").exists())

    def test_corrupt_existing_object_cannot_authorize_deletion(self):
        paths = self.backups()
        h = self.history(apply=True)
        h.store.mkdir(parents=True)
        with zipfile.ZipFile(h.store / "TotalEclipse.zip", "w") as saved:
            saved.writestr(
                "objects/" + hashlib.sha256(b"same settings").hexdigest(), b"wrong bytes"
            )
        with self.assertRaises(RuntimeError):
            h.backups("TotalEclipse")
        self.assertTrue(all(path.exists() for path in paths))

    def test_new_file_prevents_retirement(self):
        paths = self.backups(1)
        h = self.history(apply=True)
        snapshot = h.files(paths[0])
        (paths[0] / "new-save.sav").write_bytes(b"new progress")
        with self.assertRaises(RuntimeError):
            h.remove_tree(paths[0], snapshot)
        self.assertTrue((paths[0] / "new-save.sav").exists())

    def test_log_compression_and_name_reuse_preserve_both_payloads(self):
        first = b"old log\n" * 10000
        path = self.put("_work/logs/trace.log.1", first)
        recent = self.put("_work/logs/recent.log", b"new log" * 1000, old=False)
        h = self.history(apply=True)
        h.logs()
        self.assertFalse(path.exists())
        self.assertEqual(gzip.decompress(path.with_name(path.name + ".gz").read_bytes()), first)
        self.assertTrue(recent.exists())
        second = b"different old log\n" * 10000
        self.put("_work/logs/trace.log.1", second)
        h.logs()
        contents = {gzip.decompress(p.read_bytes()) for p in path.parent.glob("*.gz")}
        self.assertEqual(contents, {first, second})

    def test_active_logs_and_backups_are_preserved(self):
        paths = self.backups()
        log = self.put("_work/active/trace.log", b"active log" * 1000)
        h = self.history(
            apply=True, processes=[str(paths[0] / "game.exe"), str(log.parent / "game.exe")]
        )
        h.backups("TotalEclipse")
        h.logs()
        self.assertTrue(paths[0].exists())
        self.assertTrue(log.exists())

    def test_legacy_archive_keeps_compiled_and_source_files(self):
        source = self.put(
            "_work/backups/_ARCHIVE/legacy-generated-workspace/old/level.xnb", b"old art bytes"
        )
        h = self.history(apply=True)
        h.backups()
        self.assertFalse(source.exists())
        destination = self.root / "restored"
        history_module.restore(h, h.store / "legacy-workspace.zip", destination)
        self.assertEqual(
            (destination / source.relative_to(self.root)).read_bytes(), b"old art bytes"
        )

    def test_boundary_and_restore_collisions(self):
        h = self.history(apply=True)
        with self.assertRaises(ValueError):
            h.checked(self.root.parent / "outside")
        self.backups()
        h.backups("TotalEclipse")
        archive = h.store / "TotalEclipse.zip"
        destination = self.root / "restored"
        history_module.restore(h, archive, destination)
        source = next(destination.rglob("map.cs"))
        source.write_bytes(b"new user edits")
        with self.assertRaises(FileExistsError):
            history_module.restore(h, archive, destination)
        self.assertEqual(source.read_bytes(), b"new user edits")

    def test_junction_is_rejected_before_archiving(self):
        paths = self.backups()
        target = self.root / "valuable"
        target.mkdir()
        (target / "only-save.sav").write_bytes(b"keep")
        link = paths[0] / "linked"
        if os.name == "nt":
            command = (
                "New-Item -ItemType Junction -Path '"
                + str(link).replace("'", "''")
                + "' -Target '"
                + str(target).replace("'", "''")
                + "' | Out-Null"
            )
            subprocess.run(
                ["powershell.exe", "-NoProfile", "-Command", command],
                check=True,
                capture_output=True,
            )
        else:
            link.symlink_to(target, target_is_directory=True)
        try:
            with self.assertRaises(ValueError):
                self.history(apply=True).backups("TotalEclipse")
            self.assertEqual((target / "only-save.sav").read_bytes(), b"keep")
            self.assertTrue(all(path.exists() for path in paths))
        finally:
            if os.name == "nt":
                os.rmdir(link)
            else:
                link.unlink()

    def test_runtime_fixture_backups_do_not_replace_real_history(self):
        for index in range(5):
            prefix = f"jk-runtime/_INTERNAL/release-backup-{index:032x}"
            self.put(prefix + "/JKRuntime.dll", b"old dll")
            target = str(self.root / "jk-runtime/_INTERNAL/installer-test-old/game/JKRuntime.dll")
            self.put(prefix + "/manifest.json", json.dumps({"Target": target}).encode())
            path = self.root / prefix
            os.utime(path, (self.now - 10000 + index, self.now - 10000 + index))
        h = self.history(apply=True)
        h.backups("Runtime")
        self.assertFalse(list((self.root / "jk-runtime/_INTERNAL").glob("release-backup-*")))

    def test_legacy_and_current_locations_share_one_component_limit(self):
        paths = []
        for index, parent in enumerate(
            ("stereo-madness/_INTERNAL", "_work/stereo-madness", "stereo-madness-mod/_INTERNAL")
        ):
            prefix = (
                f"{parent}/" + ("local-install-" if index < 2 else "old-backup-") + f"{index:032x}"
            )
            self.put(prefix + "/StereoMadness.dll", b"old module")
            self.put(prefix + "/Settings.xml", f"settings-{index}".encode())
            path = self.root / prefix
            os.utime(path, (self.now - 10000, self.now - 10000))
            paths.append(path)
        self.history(apply=True).backups("StereoMadness")
        self.assertEqual(sum(path.exists() for path in paths), 2)

    def test_empty_install_records_do_not_displace_real_backups(self):
        paths = self.backups(2)
        empty = self.put(
            "_work/total-eclipse/install-ffffffffffffffffffffffffffffffff/manifest.json", b"{}"
        ).parent
        os.utime(empty, (self.now - 9000, self.now - 9000))
        self.history(apply=True).backups("TotalEclipse")
        self.assertTrue(all(path.exists() for path in paths))
        self.assertFalse(empty.exists())


if __name__ == "__main__":
    unittest.main()
