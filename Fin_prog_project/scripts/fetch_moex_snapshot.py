from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"

if str(SRC_ROOT) not in sys.path:
    sys.path.insert(0, str(SRC_ROOT))

from marketwatch.sources.moex_iss import MoexIssClient, utc_now_iso


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Fetch a MOEX ISS dry-run snapshot for configured instruments.")
    parser.add_argument(
        "--config",
        default=str(ROOT / "config" / "appsettings.example.json"),
        help="Path to the JSON config file.",
    )
    parser.add_argument(
        "--output",
        default=None,
        help="Override output path for the generated snapshot JSON.",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    config_path = Path(args.config).resolve()
    settings = load_json(config_path)

    client = MoexIssClient(
        base_url=settings["source"]["base_url"],
        timeout_seconds=int(settings["source"]["timeout_seconds"]),
        user_agent=settings["source"]["user_agent"],
    )

    results = []
    errors = []
    for instrument in settings["instruments"]:
        if not instrument.get("enabled", True):
            continue

        try:
            result = client.fetch_instrument_snapshot(instrument)
            results.append(result)
            price = result["price_snapshot"]["current_price"]
            delta = result["analytics_stub"]["delta_pct"]
            print(
                f"{instrument['id']}: {result['resolved_security']['secid']} "
                f"price={price} delta_pct={None if delta is None else round(delta, 4)}"
            )
        except Exception as exc:  # noqa: BLE001
            errors.append({"instrument_id": instrument["id"], "error": str(exc)})
            print(f"{instrument['id']}: ERROR {exc}", file=sys.stderr)

    snapshot_payload = {
        "generated_at_utc": utc_now_iso(),
        "provider": settings["source"]["provider"],
        "config_path": str(config_path),
        "results": results,
        "errors": errors,
    }

    output_path = Path(args.output).resolve() if args.output else (ROOT / settings["app"]["output_snapshot_path"]).resolve()
    write_json(output_path, snapshot_payload)
    print(f"Snapshot saved to {output_path}")

    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
