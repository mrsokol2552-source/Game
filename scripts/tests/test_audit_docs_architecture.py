import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AUDIT_SCRIPT = ROOT / "scripts" / "audit_docs_architecture.py"
BASE_FIXTURE = ROOT / "scripts" / "tests" / "fixtures" / "base_repo"


class AuditDocsArchitectureTests(unittest.TestCase):
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

    def test_missing_required_link_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            readme = repo_root / "README.md"
            readme.write_text("# Fixture Repo\n\n- [Code Map](./docs/code_map.md)\n", encoding="utf-8")
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("missing required markdown link", result.stdout)
        finally:
            temp_dir.cleanup()

    def test_untracked_markdown_doc_fails(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            extra = repo_root / "docs" / "extra.md"
            extra.write_text("# Extra\n", encoding="utf-8")
            result = self.run_audit(repo_root)
            self.assertNotEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Untracked markdown doc under governed roots", result.stdout)
        finally:
            temp_dir.cleanup()


if __name__ == "__main__":
    unittest.main()
