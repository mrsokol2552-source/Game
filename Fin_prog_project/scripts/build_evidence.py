from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))

from marketwatch.codex.prompt_builder import render_prompt
from marketwatch.evidence import build_evidence_payload


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Build an evidence.json payload and prompt from the latest market snapshot.")
    parser.add_argument(
        "--config",
        default=str(ROOT / "config" / "appsettings.example.json"),
        help="Path to the JSON config file.",
    )
    parser.add_argument(
        "--snapshot",
        default=str(ROOT / "data" / "exports" / "latest_market_snapshot.json"),
        help="Path to the snapshot JSON produced by fetch_moex_snapshot.py.",
    )
    parser.add_argument(
        "--output",
        default=str(ROOT / "data" / "evidence" / "latest_evidence.json"),
        help="Path to write evidence.json.",
    )
    parser.add_argument(
        "--prompt-output",
        default=str(ROOT / "data" / "evidence" / "prompts" / "latest_prompt.txt"),
        help="Path to write the rendered prompt.",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    config_path = Path(args.config).resolve()
    snapshot_path = Path(args.snapshot).resolve()
    output_path = Path(args.output).resolve()
    prompt_output_path = Path(args.prompt_output).resolve()

    settings = load_json(config_path)
    snapshot_payload = load_json(snapshot_path)

    evidence_payload = build_evidence_payload(
        snapshot_payload=snapshot_payload,
        settings=settings,
        snapshot_path=str(snapshot_path),
        config_name=config_path.name,
    )
    write_json(output_path, evidence_payload)

    template_path = (ROOT / settings["codex"]["prompt_template_path"]).resolve()
    template_text = template_path.read_text(encoding="utf-8")
    prompt_text = render_prompt(template_text, evidence_payload)
    write_text(prompt_output_path, prompt_text)

    print(f"Evidence saved to {output_path}")
    print(f"Prompt saved to {prompt_output_path}")
    print(f"Trigger type: {evidence_payload['event']['trigger_type']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
