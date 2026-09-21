#!/usr/bin/env python3
from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_ROOT = Path(__file__).resolve().parent


@dataclass
class StepResult:
    name: str
    returncode: int
    stdout: str
    stderr: str

    @property
    def success(self) -> bool:
        return self.returncode == 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the full repository audit pipeline: runtime config export, audits, and tests."
    )
    parser.add_argument(
        "--repo-root",
        default=str(DEFAULT_REPO_ROOT),
        help="Repository root to audit. Defaults to the current project root.",
    )
    parser.add_argument(
        "--skip-export",
        action="store_true",
        help="Skip the strict runtime-config export refresh step.",
    )
    parser.add_argument(
        "--with-full-export",
        action="store_true",
        help="Also generate the non-canonical full runtime-config snapshot.",
    )
    parser.add_argument(
        "--full-export-output",
        default="docs/runtime_config_export_full.json",
        help="Output path for the optional full export, relative to repo root unless absolute.",
    )
    parser.add_argument(
        "--skip-machine-audit",
        action="store_true",
        help="Skip the machine-layer audit.",
    )
    parser.add_argument(
        "--skip-docs-audit",
        action="store_true",
        help="Skip the docs-architecture audit.",
    )
    parser.add_argument(
        "--skip-tests",
        action="store_true",
        help="Skip the Python test suite.",
    )
    parser.add_argument(
        "--tests-path",
        default="scripts/tests",
        help="Path to the unittest discovery root, relative to repo root unless absolute.",
    )
    return parser.parse_args()


def run_step(name: str, command: list[str], cwd: Path) -> StepResult:
    result = subprocess.run(
        command,
        cwd=cwd,
        text=True,
        capture_output=True,
        check=False,
    )
    return StepResult(
        name=name,
        returncode=result.returncode,
        stdout=result.stdout,
        stderr=result.stderr,
    )


def render_step(result: StepResult) -> str:
    lines = [f"== {result.name} =="]
    lines.append(f"Command status: {'PASS' if result.success else 'FAIL'} ({result.returncode})")
    if result.stdout.strip():
        lines.append("[stdout]")
        lines.append(result.stdout.rstrip())
    if result.stderr.strip():
        lines.append("[stderr]")
        lines.append(result.stderr.rstrip())
    return "\n".join(lines)


def render_summary(results: list[StepResult]) -> str:
    lines = ["", "== Summary =="]
    for result in results:
        lines.append(f"- {result.name}: {'PASS' if result.success else 'FAIL'}")
    failures = [result for result in results if not result.success]
    lines.append("")
    lines.append(f"Steps: {len(results)}")
    lines.append(f"Failures: {len(failures)}")
    lines.append("Result: PASS" if not failures else "Result: FAIL")
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    full_export_output = Path(args.full_export_output)
    if not full_export_output.is_absolute():
        full_export_output = (repo_root / full_export_output).resolve()

    tests_path = Path(args.tests_path)
    if not tests_path.is_absolute():
        tests_path = (repo_root / tests_path).resolve()

    results: list[StepResult] = []

    if not args.skip_export:
        results.append(
            run_step(
                "export_runtime_config_strict",
                [
                    sys.executable,
                    str(SCRIPTS_ROOT / "export_runtime_config.py"),
                    "--repo-root",
                    str(repo_root),
                    "--strict",
                ],
                cwd=repo_root,
            )
        )

    if args.with_full_export:
        results.append(
            run_step(
                "export_runtime_config_full",
                [
                    sys.executable,
                    str(SCRIPTS_ROOT / "export_runtime_config.py"),
                    "--repo-root",
                    str(repo_root),
                    "--full",
                    "--output",
                    str(full_export_output),
                ],
                cwd=repo_root,
            )
        )

    if not args.skip_machine_audit:
        results.append(
            run_step(
                "audit_machine_layer",
                [
                    sys.executable,
                    str(SCRIPTS_ROOT / "audit_machine_layer.py"),
                    "--repo-root",
                    str(repo_root),
                ],
                cwd=repo_root,
            )
        )

    if not args.skip_docs_audit:
        results.append(
            run_step(
                "audit_docs_architecture",
                [
                    sys.executable,
                    str(SCRIPTS_ROOT / "audit_docs_architecture.py"),
                    "--repo-root",
                    str(repo_root),
                ],
                cwd=repo_root,
            )
        )

    if not args.skip_tests:
        if not tests_path.exists():
            results.append(
                StepResult(
                    name="python_unittests",
                    returncode=1,
                    stdout="",
                    stderr=f"Tests path does not exist: {tests_path}",
                )
            )
        else:
            results.append(
                run_step(
                    "python_unittests",
                    [
                        sys.executable,
                        "-m",
                        "unittest",
                        "discover",
                        str(tests_path),
                        "-v",
                    ],
                    cwd=repo_root,
                )
            )

    for result in results:
        print(render_step(result))
        print("")

    print(render_summary(results))
    return 0 if all(result.success for result in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
