import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AUDIT_SCRIPT = ROOT / "scripts" / "audit_machine_layer.py"
BASE_FIXTURE = ROOT / "scripts" / "tests" / "fixtures" / "base_repo"


class AuditMachineLayerTests(unittest.TestCase):
    maxDiff = None

    def make_repo_copy(self) -> tuple[tempfile.TemporaryDirectory[str], Path]:
        temp_dir = tempfile.TemporaryDirectory()
        repo_root = Path(temp_dir.name) / "repo"
        shutil.copytree(BASE_FIXTURE, repo_root)
        return temp_dir, repo_root

    def run_audit(self, repo_root: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(AUDIT_SCRIPT), "--repo-root", str(repo_root)],
            text=True,
            capture_output=True,
            check=False,
        )

    def test_pass_fixture_repo(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            result = self.run_audit(repo_root)
            self.assertEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Result: PASS", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_duplicate_code_id_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            duplicate = repo_root / "My project" / "Assets" / "Scripts" / "DuplicateSystem.cs"
            duplicate.write_text(
                "/*\n@file: My project/Assets/Scripts/DuplicateSystem.cs\n*/\n"
                "// [CODE-ID: SCRIPTS-EXAMPLE-SYSTEM]\n"
                "namespace FixtureRepo { // [EXMP-02]\npublic sealed class DuplicateSystem {} }\n",
                encoding="utf-8",
            )
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Duplicate CODE-ID", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_plain_file_reference_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            code_map = repo_root / "docs" / "code_map.md"
            code_map.write_text(code_map.read_text(encoding="utf-8") + "\nSee docs/runtime_config_export.json.\n", encoding="utf-8")
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Plain file reference without Markdown link", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_broken_anchor_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            readme = repo_root / "README.md"
            readme.write_text(readme.read_text(encoding="utf-8") + "\n- [Broken](./docs/code_map.md#missing-anchor)\n", encoding="utf-8")
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Broken markdown anchor", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_runtime_config_sync_failure_is_detected(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            export_path = repo_root / "docs" / "runtime_config_export.json"
            export = export_path.read_text(encoding="utf-8")
            export = export.replace("\"scene_targets\": []", "\"scene_targets\": [{\"path\": \"My project/Assets/Scenes/MissingScene.unity\", \"missing_game_objects\": [], \"game_objects\": []}]")
            export_path.write_text(export, encoding="utf-8")
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("runtime config sync", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_tilemap_block_read_outside_legacy_helper_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            reader = repo_root / "My project" / "Assets" / "Scripts" / "TilemapReader.cs"
            reader.write_text(
                "/*\n@file: My project/Assets/Scripts/TilemapReader.cs\n*/\n"
                "// [CODE-ID: SCRIPTS-TILEMAP-READER]\n"
                "namespace FixtureRepo\n"
                "{\n"
                "    public sealed class TilemapReader\n"
                "    {\n"
                "        public object[] Read(UnityEngine.Tilemaps.Tilemap map, UnityEngine.BoundsInt bounds)\n"
                "        {\n"
                "            return map.GetTilesBlock(bounds);\n"
                "        }\n"
                "    }\n"
                "}\n",
                encoding="utf-8",
            )
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Tilemap block read outside LegacyTilemap extraction helper", result.stdout)
        finally:
            temp_dir.cleanup()


if __name__ == "__main__":
    unittest.main()
