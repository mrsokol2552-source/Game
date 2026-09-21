#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROBE_PREFIX = "[SampleSceneRendererProbe]"
DEFAULT_FIXTURE = "Tests.PlayMode.SampleSceneRendererDiagnosticsTests"
DEFAULT_METHOD = "SampleSceneRendererStreamingProbe_LogsBaselineMetrics"
KEY_VALUE_RE = re.compile(r"(?P<key>[A-Za-z][A-Za-z0-9]*)=(?P<value>\S+)")
PROVISIONAL_TARGET_FPS = 144.0
PROVISIONAL_TARGET_AVG_FRAME_MS = 1000.0 / PROVISIONAL_TARGET_FPS
PROVISIONAL_MAX_FRAME_MS = 203.0
PROVISIONAL_MAX_DURATION_SECONDS = 60.0


@dataclass(frozen=True)
class ProbeResult:
    xml_path: Path
    xml_mtime_utc: str
    test_run_result: str
    fixture: str
    method: str
    test_result: str
    test_duration_seconds: float | None
    probe_line: str
    metrics: dict[str, Any]


@dataclass(frozen=True)
class BudgetEvaluation:
    status: str
    hard_failures: list[str]
    warnings: list[str]
    limits: dict[str, Any]


def default_results_path() -> Path:
    root = os.environ.get("USERPROFILE")
    base = Path(root) if root else Path.home()
    return base / "AppData" / "LocalLow" / "DefaultCompany" / "My project" / "TestResults.xml"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract the latest SampleScene renderer diagnostic probe from Unity TestResults.xml."
    )
    parser.add_argument(
        "--xml",
        type=Path,
        default=default_results_path(),
        help="Unity TestResults.xml path. Defaults to the local Unity Test Runner output path.",
    )
    parser.add_argument(
        "--fixture",
        default=DEFAULT_FIXTURE,
        help="Expected test fixture fullname.",
    )
    parser.add_argument(
        "--method",
        default=DEFAULT_METHOD,
        help="Expected test method name.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit a JSON object instead of a compact text summary.",
    )
    parser.add_argument(
        "--max-age-minutes",
        type=float,
        default=None,
        help="Fail if the XML file is older than this many minutes.",
    )
    parser.add_argument(
        "--check-provisional-budget",
        action="store_true",
        help="Evaluate the current provisional owner budget and fail only on hard blockers.",
    )
    return parser.parse_args()


def parse_metric_value(value: str) -> Any:
    if value == "True":
        return True
    if value == "False":
        return False

    normalized = value.replace(",", ".")
    try:
        if "." not in normalized:
            return int(normalized)
    except ValueError:
        pass

    try:
        return float(normalized)
    except ValueError:
        return value


def parse_probe_line(line: str) -> dict[str, Any]:
    metrics: dict[str, Any] = {}
    for match in KEY_VALUE_RE.finditer(line):
        metrics[match.group("key")] = parse_metric_value(match.group("value"))
    return metrics


def extract_probe_result(xml_path: Path, fixture: str, method: str) -> ProbeResult:
    if not xml_path.exists():
        raise FileNotFoundError(f"Unity TestResults.xml not found: {xml_path}")

    tree = ET.parse(xml_path)
    root = tree.getroot()
    test_run_result = root.attrib.get("result", "")
    selected_case = None
    for test_case in root.iter("test-case"):
        if test_case.attrib.get("classname") != fixture:
            continue
        if test_case.attrib.get("methodname") != method:
            continue
        selected_case = test_case

    if selected_case is None:
        raise ValueError(f"Did not find test case {fixture}.{method} in {xml_path}")

    output = selected_case.findtext("output") or ""
    probe_line = ""
    for line in output.splitlines():
        if PROBE_PREFIX in line:
            probe_line = line.strip()

    if not probe_line:
        raise ValueError(f"Did not find {PROBE_PREFIX} output in {fixture}.{method}")

    try:
        duration = float((selected_case.attrib.get("duration") or "").replace(",", "."))
    except ValueError:
        duration = None

    mtime = datetime.fromtimestamp(xml_path.stat().st_mtime, timezone.utc)
    return ProbeResult(
        xml_path=xml_path,
        xml_mtime_utc=mtime.isoformat(timespec="seconds"),
        test_run_result=test_run_result,
        fixture=fixture,
        method=method,
        test_result=selected_case.attrib.get("result", ""),
        test_duration_seconds=duration,
        probe_line=probe_line,
        metrics=parse_probe_line(probe_line),
    )


def result_to_dict(result: ProbeResult) -> dict[str, Any]:
    return {
        "xml_path": str(result.xml_path),
        "xml_mtime_utc": result.xml_mtime_utc,
        "test_run_result": result.test_run_result,
        "fixture": result.fixture,
        "method": result.method,
        "test_result": result.test_result,
        "test_duration_seconds": result.test_duration_seconds,
        "probe_line": result.probe_line,
        "metrics": result.metrics,
    }


def budget_to_dict(evaluation: BudgetEvaluation) -> dict[str, Any]:
    return {
        "status": evaluation.status,
        "hard_failures": evaluation.hard_failures,
        "warnings": evaluation.warnings,
        "limits": evaluation.limits,
    }


def is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def evaluate_provisional_budget(result: ProbeResult) -> BudgetEvaluation:
    metrics = result.metrics
    hard_failures: list[str] = []
    warnings: list[str] = []
    limits: dict[str, Any] = {
        "target_hardware": "8GB RAM, 8 CPU cores around 3.6GHz, GPU >= AMD Radeon RX 7600 XT",
        "target_fps": PROVISIONAL_TARGET_FPS,
        "target_avg_frame_ms": round(PROVISIONAL_TARGET_AVG_FRAME_MS, 3),
        "max_frame_ms": PROVISIONAL_MAX_FRAME_MS,
        "max_duration_seconds": PROVISIONAL_MAX_DURATION_SECONDS,
        "required_final_pending": 0,
        "required_final_renderer_roots": False,
    }

    if result.test_run_result != "Passed":
        hard_failures.append(f"test_run_result={result.test_run_result!r}, expected 'Passed'")
    if result.test_result != "Passed":
        hard_failures.append(f"test_result={result.test_result!r}, expected 'Passed'")

    duration = result.test_duration_seconds
    if duration is None:
        hard_failures.append("test_duration_seconds is missing")
    elif duration > PROVISIONAL_MAX_DURATION_SECONDS:
        hard_failures.append(
            f"test_duration_seconds={duration:.3f} exceeds {PROVISIONAL_MAX_DURATION_SECONDS:.3f}"
        )

    frames = metrics.get("frames")
    if not is_number(frames):
        hard_failures.append("frames metric is missing or not numeric")
    elif frames <= 0:
        hard_failures.append(f"frames={frames} must be greater than zero")

    max_frame_ms = metrics.get("maxFrameMs")
    if not is_number(max_frame_ms):
        hard_failures.append("maxFrameMs metric is missing or not numeric")
    elif max_frame_ms > PROVISIONAL_MAX_FRAME_MS:
        hard_failures.append(
            f"maxFrameMs={max_frame_ms:.3f} exceeds provisional hard cap {PROVISIONAL_MAX_FRAME_MS:.3f}"
        )

    avg_frame_ms = metrics.get("avgFrameMs")
    if not is_number(avg_frame_ms):
        hard_failures.append("avgFrameMs metric is missing or not numeric")
    elif avg_frame_ms > PROVISIONAL_TARGET_AVG_FRAME_MS:
        warnings.append(
            f"avgFrameMs={avg_frame_ms:.3f} is above 144 FPS target frame time "
            f"{PROVISIONAL_TARGET_AVG_FRAME_MS:.3f}"
        )

    final_pending = metrics.get("finalPending")
    if not is_number(final_pending):
        hard_failures.append("finalPending metric is missing or not numeric")
    elif final_pending != 0:
        hard_failures.append(f"finalPending={final_pending}, expected 0")

    for root_key in ("finalBackgroundRoot", "finalGroundRoot", "finalBiomeRoot"):
        root_value = metrics.get(root_key)
        if root_value is not False:
            hard_failures.append(f"{root_key}={root_value!r}, expected False")

    status = "failed" if hard_failures else "passed"
    return BudgetEvaluation(status, hard_failures, warnings, limits)


def format_summary(result: ProbeResult) -> str:
    metrics = result.metrics
    fields = [
        f"result={result.test_result}",
        f"xml_mtime_utc={result.xml_mtime_utc}",
        f"frames={metrics.get('frames')}",
        f"avgFrameMs={metrics.get('avgFrameMs')}",
        f"maxFrameMs={metrics.get('maxFrameMs')}",
        f"generatedDelta={metrics.get('generatedDelta')}",
        f"createdDelta={metrics.get('createdDelta')}",
        f"reusedDelta={metrics.get('reusedDelta')}",
        f"unloadedDelta={metrics.get('unloadedDelta')}",
        f"backgroundPeakQuads={metrics.get('backgroundPeakQuads')}",
        f"biomePeakQuads={metrics.get('biomePeakQuads')}",
        f"unityAllocDeltaMb={metrics.get('unityAllocDeltaMb')}",
        f"managedDeltaMb={metrics.get('managedDeltaMb')}",
    ]
    return "SampleSceneRendererProbe " + " ".join(fields)


def format_budget_summary(evaluation: BudgetEvaluation) -> str:
    fields = [
        f"status={evaluation.status}",
        f"hardFailures={len(evaluation.hard_failures)}",
        f"warnings={len(evaluation.warnings)}",
        f"targetAvgFrameMs={evaluation.limits['target_avg_frame_ms']}",
        f"maxFrameMs={evaluation.limits['max_frame_ms']}",
        f"maxDurationSeconds={evaluation.limits['max_duration_seconds']}",
    ]
    lines = ["ProvisionalBudget " + " ".join(fields)]
    lines.extend(f"HARD_FAIL: {failure}" for failure in evaluation.hard_failures)
    lines.extend(f"WARN: {warning}" for warning in evaluation.warnings)
    return "\n".join(lines)


def check_age(xml_path: Path, max_age_minutes: float | None) -> None:
    if max_age_minutes is None:
        return

    age_seconds = datetime.now(timezone.utc).timestamp() - xml_path.stat().st_mtime
    if age_seconds > max_age_minutes * 60:
        raise ValueError(
            f"Unity TestResults.xml is older than {max_age_minutes:g} minutes: {xml_path}"
        )


def main() -> int:
    args = parse_args()
    try:
        check_age(args.xml, args.max_age_minutes)
        result = extract_probe_result(args.xml, args.fixture, args.method)
    except (OSError, ET.ParseError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    budget_evaluation = None
    if args.check_provisional_budget:
        budget_evaluation = evaluate_provisional_budget(result)

    if args.json:
        payload = result_to_dict(result)
        if budget_evaluation is not None:
            payload["provisional_budget"] = budget_to_dict(budget_evaluation)
        print(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True))
    else:
        print(format_summary(result))
        if budget_evaluation is not None:
            print(format_budget_summary(budget_evaluation))
        print(result.probe_line)
    return 1 if budget_evaluation is not None and budget_evaluation.hard_failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
