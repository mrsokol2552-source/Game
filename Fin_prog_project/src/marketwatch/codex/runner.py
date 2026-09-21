from __future__ import annotations

import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class CodexPreflight:
    executable: str
    version: str


@dataclass(frozen=True)
class CodexRunResult:
    command: list[str]
    stdout: str
    stderr: str
    exit_code: int


def codex_preflight() -> CodexPreflight:
    executable = shutil.which("codex")
    if not executable:
        raise RuntimeError("codex executable was not found in PATH.")

    version = subprocess.run(
        [executable, "--version"],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout.strip()

    return CodexPreflight(executable=executable, version=version)


def build_exec_command(working_directory: Path, prompt_text: str) -> list[str]:
    return ["codex", "exec", "--cd", str(working_directory), prompt_text]


def run_codex_exec(working_directory: Path, prompt_text: str, timeout_seconds: int) -> CodexRunResult:
    command = build_exec_command(working_directory=working_directory, prompt_text=prompt_text)
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=timeout_seconds,
    )
    return CodexRunResult(
        command=command,
        stdout=completed.stdout,
        stderr=completed.stderr,
        exit_code=completed.returncode,
    )
