import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RUN_ALL_SCRIPT = ROOT / "scripts" / "run_all_repo_audits.py"
BASE_FIXTURE = ROOT / "scripts" / "tests" / "fixtures" / "base_repo"
SCRIPT_GUID = "abcdefabcdefabcdefabcdefabcdefab"


SCENE_TEXT = f"""--- !u!1 &100
GameObject:
  m_Name: Bootstrap
  m_Component:
  - component: {{fileID: 400}}
  - component: {{fileID: 401}}
  - component: {{fileID: 402}}
--- !u!4 &400
Transform:
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!20 &401
Camera:
  orthographic: 1
  orthographic size: 5
  near clip plane: 0.3
  far clip plane: 1000
  m_BackGroundColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_Depth: 0
--- !u!114 &402
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_EditorClassIdentifier: Assembly-CSharp::ExampleSystem
  Enabled: true
  Threshold: 5
"""


PREFAB_TEXT = f"""--- !u!1 &200
GameObject:
  m_Name: Unit
  m_Component:
  - component: {{fileID: 500}}
  - component: {{fileID: 501}}
--- !u!4 &500
Transform:
  m_LocalPosition: {{x: 1, y: 2, z: 0}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &501
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_EditorClassIdentifier: Assembly-CSharp::ExampleSystem
  PrefabValue: 7
"""


META_TEXT = f"""fileFormatVersion: 2
guid: {SCRIPT_GUID}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


class RunAllRepoAuditsTests(unittest.TestCase):
    def make_repo_copy(self) -> tuple[tempfile.TemporaryDirectory[str], Path]:
        temp_dir = tempfile.TemporaryDirectory()
        repo_root = Path(temp_dir.name) / "repo"
        shutil.copytree(BASE_FIXTURE, repo_root)
        self.augment_repo_for_export(repo_root)
        return temp_dir, repo_root

    def augment_repo_for_export(self, repo_root: Path) -> None:
        scene_path = repo_root / "My project" / "Assets" / "Scenes" / "FixtureScene.unity"
        prefab_path = repo_root / "My project" / "Assets" / "Prefabs" / "Unit.prefab"
        script_meta_path = repo_root / "My project" / "Assets" / "Scripts" / "ExampleSystem.cs.meta"
        manifest_path = repo_root / "scripts" / "runtime_config_manifest.json"

        scene_path.parent.mkdir(parents=True, exist_ok=True)
        prefab_path.parent.mkdir(parents=True, exist_ok=True)

        scene_path.write_text(SCENE_TEXT, encoding="utf-8")
        prefab_path.write_text(PREFAB_TEXT, encoding="utf-8")
        script_meta_path.write_text(META_TEXT, encoding="utf-8")

        manifest = {
            "version": 1,
            "output": "docs/runtime_config_export.json",
            "script_roots": ["My project/Assets/Scripts"],
            "scene_targets": [
                {
                    "path": "My project/Assets/Scenes/FixtureScene.unity",
                    "game_objects": ["Bootstrap"],
                    "include_component_types": ["MonoBehaviour", "Transform", "Camera"],
                }
            ],
            "prefab_targets": [
                {
                    "path": "My project/Assets/Prefabs/Unit.prefab",
                    "include_component_types": ["MonoBehaviour", "Transform"],
                }
            ],
        }
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    def run_all(self, repo_root: Path, *extra_args: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(RUN_ALL_SCRIPT), "--repo-root", str(repo_root), *extra_args],
            text=True,
            capture_output=True,
            check=False,
        )

    def test_run_all_passes_on_fixture_repo(self) -> None:
        temp_dir, repo_root = self.make_repo_copy()
        try:
            result = self.run_all(repo_root, "--skip-tests", "--with-full-export")
            self.assertEqual(result.returncode, 0, msg=result.stdout + "\n" + result.stderr)
            self.assertIn("Result: PASS", result.stdout)
            self.assertTrue((repo_root / "docs" / "runtime_config_export.json").exists())
            self.assertTrue((repo_root / "docs" / "runtime_config_export_full.json").exists())
        finally:
            temp_dir.cleanup()


if __name__ == "__main__":
    unittest.main()
