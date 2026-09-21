from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))

from marketwatch.codex.runner import build_exec_command, codex_preflight, run_codex_exec


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def utc_token() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Preflight and optionally run codex exec against the rendered prompt.")
    parser.add_argument(
        "--config",
        default=str(ROOT / "config" / "appsettings.example.json"),
        help="Path to the JSON config file.",
    )
    parser.add_argument(
        "--prompt",
        default=str(ROOT / "data" / "evidence" / "prompts" / "latest_prompt.txt"),
        help="Path to the prompt text file.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only print the command and write preflight metadata without executing codex exec.",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    config_path = Path(args.config).resolve()
    prompt_path = Path(args.prompt).resolve()
    settings = load_json(config_path)
    prompt_text = prompt_path.read_text(encoding="utf-8")

    preflight = codex_preflight()
    working_directory = (ROOT / settings["codex"]["working_directory"]).resolve()
    command = build_exec_command(working_directory=working_directory, prompt_text=prompt_text)

    log_dir = (ROOT / "logs" / "codex").resolve()
    token = utc_token()
    preflight_path = log_dir / f"{token}.preflight.txt"

    preflight_text = "\n".join(
        [
            f"codex_path={preflight.executable}",
            f"codex_version={preflight.version}",
            f"working_directory={working_directory}",
            f"prompt_path={prompt_path}",
            f"command={' '.join(command[:4])} <prompt>",
        ]
    )
    write_text(preflight_path, preflight_text)

    if args.dry_run:
        print(f"Preflight saved to {preflight_path}")
        print(f"Command: {' '.join(command[:4])} <prompt>")
        return 0

    result = run_codex_exec(
        working_directory=working_directory,
        prompt_text=prompt_text,
        timeout_seconds=int(settings["codex"]["timeout_seconds"]),
    )

    stdout_path = log_dir / f"{token}.stdout.txt"
    stderr_path = log_dir / f"{token}.stderr.txt"
    write_text(stdout_path, result.stdout)
    write_text(stderr_path, result.stderr)

    print(f"Preflight saved to {preflight_path}")
    print(f"stdout saved to {stdout_path}")
    print(f"stderr saved to {stderr_path}")
    print(f"Exit code: {result.exit_code}")

    return result.exit_code


if __name__ == "__main__":
    raise SystemExit(main())
