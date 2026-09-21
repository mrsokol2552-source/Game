import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXTRACT_SCRIPT = ROOT / "scripts" / "extract_combat_pressure_probe.py"


VALID_XML = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="1" passed="1" failed="0">
  <test-suite type="TestFixture" name="FpsStressTests">
    <test-case
      classname="Tests.PlayMode.FpsStressTests"
      methodname="OwnerTarget100v100_PathPressure_LogsCombatPressureProbe"
      result="Passed"
      duration="12,500000">
      <output><![CDATA[[CombatPressureProbe] measurement=ownerTarget100v100 playerUnits=100 enemyUnits=100 totalUnits=200 distance=12,000 pathPressure=True warmupFrames=3 frames=90 seconds=1,500 avgFps=60,000 avgFrameMs=16,667 maxFrameMs=50,000 attackEvents=12 playerAttackEvents=7 enemyAttackEvents=5 playerAlive=100 enemyAlive=100 pathResets=1 maxPathResetsFrame=1 totalPathBuilds=18 totalPathAccepts=10 totalPathRejects=8 maxPathBuildsFrame=3 totalPathCommands=44 maxPathCommandsFrame=4 totalPaths=11 maxPathsFrame=5 totalPathLength=120 maxPathLengthFrame=20 totalJobScheduled=0 totalJobCompleted=0 totalJobFallback=18 totalJitter=0 maxJitterFrame=0 totalCrowdMoves=6 maxCrowdMovesFrame=6
]]></output>
    </test-case>
  </test-suite>
</test-run>
"""


MISSING_PROBE_XML = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="1" passed="1" failed="0">
  <test-case
    classname="Tests.PlayMode.FpsStressTests"
    methodname="OwnerTarget100v100_PathPressure_LogsCombatPressureProbe"
    result="Passed"
    duration="1.0">
    <output><![CDATA[No probe here]]></output>
  </test-case>
</test-run>
"""


class ExtractCombatPressureProbeTests(unittest.TestCase):
    def write_xml(self, text: str) -> tuple[tempfile.TemporaryDirectory[str], Path]:
        temp_dir = tempfile.TemporaryDirectory()
        path = Path(temp_dir.name) / "TestResults.xml"
        path.write_text(text, encoding="utf-8")
        return temp_dir, path

    def test_extracts_probe_metrics_as_json(self):
        temp_dir, path = self.write_xml(VALID_XML)
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [sys.executable, str(EXTRACT_SCRIPT), "--xml", str(path), "--json"],
            cwd=ROOT,
            text=True,
            capture_output=True,
            check=True,
        )
        payload = json.loads(result.stdout)

        self.assertEqual(payload["test_result"], "Passed")
        self.assertEqual(payload["metrics"]["measurement"], "ownerTarget100v100")
        self.assertEqual(payload["metrics"]["playerUnits"], 100)
        self.assertEqual(payload["metrics"]["enemyUnits"], 100)
        self.assertEqual(payload["metrics"]["totalUnits"], 200)
        self.assertEqual(payload["metrics"]["pathPressure"], True)
        self.assertEqual(payload["metrics"]["frames"], 90)
        self.assertAlmostEqual(payload["metrics"]["avgFrameMs"], 16.667)
        self.assertAlmostEqual(payload["metrics"]["maxFrameMs"], 50.0)
        self.assertEqual(payload["metrics"]["attackEvents"], 12)
        self.assertEqual(payload["metrics"]["totalPathBuilds"], 18)
        self.assertEqual(payload["metrics"]["maxPathCommandsFrame"], 4)

    def test_missing_probe_line_fails(self):
        temp_dir, path = self.write_xml(MISSING_PROBE_XML)
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [sys.executable, str(EXTRACT_SCRIPT), "--xml", str(path)],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Did not find [CombatPressureProbe]", result.stderr)

    def test_provisional_budget_passes_with_target_fps_warning(self):
        temp_dir, path = self.write_xml(VALID_XML)
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [
                sys.executable,
                str(EXTRACT_SCRIPT),
                "--xml",
                str(path),
                "--json",
                "--check-provisional-budget",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        payload = json.loads(result.stdout)
        budget = payload["provisional_budget"]

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(budget["status"], "passed")
        self.assertEqual(budget["hard_failures"], [])
        self.assertTrue(any("144 FPS" in warning for warning in budget["warnings"]))
        self.assertEqual(budget["limits"]["expected_total_units"], 200)

    def test_provisional_budget_fails_when_spike_exceeds_hard_cap(self):
        temp_dir, path = self.write_xml(
            VALID_XML.replace("maxFrameMs=50,000", "maxFrameMs=204,001")
        )
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [
                sys.executable,
                str(EXTRACT_SCRIPT),
                "--xml",
                str(path),
                "--json",
                "--check-provisional-budget",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        payload = json.loads(result.stdout)
        budget = payload["provisional_budget"]

        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(budget["status"], "failed")
        self.assertTrue(any("maxFrameMs=204.001" in failure for failure in budget["hard_failures"]))

    def test_provisional_budget_fails_when_unit_count_is_not_owner_target(self):
        temp_dir, path = self.write_xml(
            VALID_XML.replace("playerUnits=100", "playerUnits=99").replace("totalUnits=200", "totalUnits=199")
        )
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [
                sys.executable,
                str(EXTRACT_SCRIPT),
                "--xml",
                str(path),
                "--json",
                "--check-provisional-budget",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        payload = json.loads(result.stdout)
        budget = payload["provisional_budget"]

        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(any("playerUnits=99" in failure for failure in budget["hard_failures"]))
        self.assertTrue(any("totalUnits=199" in failure for failure in budget["hard_failures"]))

    def test_provisional_budget_fails_without_path_or_attack_activity(self):
        temp_dir, path = self.write_xml(
            VALID_XML
            .replace("totalPathBuilds=18", "totalPathBuilds=0")
            .replace("attackEvents=12", "attackEvents=0")
        )
        self.addCleanup(temp_dir.cleanup)

        result = subprocess.run(
            [
                sys.executable,
                str(EXTRACT_SCRIPT),
                "--xml",
                str(path),
                "--json",
                "--check-provisional-budget",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        payload = json.loads(result.stdout)
        budget = payload["provisional_budget"]

        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(any("totalPathBuilds=0" in failure for failure in budget["hard_failures"]))
        self.assertTrue(any("attackEvents=0" in failure for failure in budget["hard_failures"]))


if __name__ == "__main__":
    unittest.main()
