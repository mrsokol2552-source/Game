import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXTRACT_SCRIPT = ROOT / "scripts" / "extract_sample_scene_renderer_probe.py"


VALID_XML = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="1" passed="1" failed="0">
  <test-suite type="TestFixture" name="SampleSceneRendererDiagnosticsTests">
    <test-case
      classname="Tests.PlayMode.SampleSceneRendererDiagnosticsTests"
      methodname="SampleSceneRendererStreamingProbe_LogsBaselineMetrics"
      result="Passed"
      duration="6,458298">
      <output><![CDATA[[SampleSceneRendererProbe] measurement=postWarmup loadSettleFrames=3 warmupFrames=5 frames=17 seconds=1,904 avgFrameMs=111,986 maxFrameMs=1561,902 generatedDelta=488 createdDelta=18 reusedDelta=30 unloadedDelta=471 finalBackgroundRoot=False finalGroundRoot=False finalBiomeRoot=False unityAllocDeltaMb=8,029 managedDeltaMb=101,938
]]></output>
    </test-case>
  </test-suite>
</test-run>
"""


BUDGET_PASS_XML = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="1" passed="1" failed="0">
  <test-suite type="TestFixture" name="SampleSceneRendererDiagnosticsTests">
    <test-case
      classname="Tests.PlayMode.SampleSceneRendererDiagnosticsTests"
      methodname="SampleSceneRendererStreamingProbe_LogsBaselineMetrics"
      result="Passed"
      duration="6.458298">
      <output><![CDATA[[SampleSceneRendererProbe] measurement=postWarmup loadSettleFrames=3 warmupFrames=5 frames=12 seconds=0.439 avgFrameMs=36.615 maxFrameMs=84.588 generatedDelta=168 createdDelta=18 reusedDelta=18 unloadedDelta=21 finalTracked=9 finalGeneratedActive=9 finalPending=0 finalRequired=9 backgroundPeakChunks=360 backgroundPeakQuads=17712 groundPeakChunks=0 groundPeakQuads=0 biomePeakChunks=0 biomePeakQuads=0 finalBackgroundRoot=False finalGroundRoot=False finalBiomeRoot=False unityAllocDeltaMb=2.525 managedDeltaMb=34.496
]]></output>
    </test-case>
  </test-suite>
</test-run>
"""


MISSING_PROBE_XML = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="1" passed="1" failed="0">
  <test-case
    classname="Tests.PlayMode.SampleSceneRendererDiagnosticsTests"
    methodname="SampleSceneRendererStreamingProbe_LogsBaselineMetrics"
    result="Passed"
    duration="1.0">
    <output><![CDATA[No probe here]]></output>
  </test-case>
</test-run>
"""


class ExtractSampleSceneRendererProbeTests(unittest.TestCase):
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
        self.assertEqual(payload["metrics"]["measurement"], "postWarmup")
        self.assertEqual(payload["metrics"]["loadSettleFrames"], 3)
        self.assertEqual(payload["metrics"]["warmupFrames"], 5)
        self.assertEqual(payload["metrics"]["frames"], 17)
        self.assertAlmostEqual(payload["metrics"]["avgFrameMs"], 111.986)
        self.assertAlmostEqual(payload["metrics"]["maxFrameMs"], 1561.902)
        self.assertEqual(payload["metrics"]["finalBackgroundRoot"], False)
        self.assertAlmostEqual(payload["metrics"]["managedDeltaMb"], 101.938)

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
        self.assertIn("Did not find [SampleSceneRendererProbe]", result.stderr)

    def test_provisional_budget_passes_with_target_fps_warning(self):
        temp_dir, path = self.write_xml(BUDGET_PASS_XML)
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
        self.assertAlmostEqual(budget["limits"]["target_avg_frame_ms"], 6.944)
        self.assertEqual(budget["limits"]["max_frame_ms"], 203.0)

    def test_provisional_budget_fails_when_spike_exceeds_hard_cap(self):
        temp_dir, path = self.write_xml(
            BUDGET_PASS_XML.replace("maxFrameMs=84.588", "maxFrameMs=204.001")
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


if __name__ == "__main__":
    unittest.main()
