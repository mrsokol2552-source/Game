from __future__ import annotations

from typing import Any


def build_evidence_payload(
    snapshot_payload: dict[str, Any],
    settings: dict[str, Any],
    snapshot_path: str,
    config_name: str,
) -> dict[str, Any]:
    threshold = float(settings["triggers"]["price_move_threshold_pct"])
    poll_interval = int(settings["app"]["poll_interval_minutes"])

    triggered = [
        result
        for result in snapshot_payload.get("results", [])
        if result.get("analytics_stub", {}).get("delta_pct") is not None
        and abs(float(result["analytics_stub"]["delta_pct"])) >= threshold
    ]

    if triggered:
        trigger_type = "price_move"
        primary_trigger = triggered[0]["instrument_id"]
    else:
        trigger_type = "manual"
        primary_trigger = "manual"

    timestamp_token = (
        str(snapshot_payload["generated_at_utc"])
        .replace("-", "")
        .replace(":", "")
        .replace(".", "")
    )
    event_id = f"{timestamp_token}-{trigger_type}-{primary_trigger}"

    instruments = []
    for result in snapshot_payload.get("results", []):
        instruments.append(
            {
                "id": result["instrument_id"],
                "label": result["label"],
                "resolved_security": {
                    "query": result["resolved_security"]["query"],
                    "secid": result["resolved_security"]["secid"],
                    "shortname": result["resolved_security"]["shortname"],
                    "name": result["resolved_security"]["name"],
                    "type": result["resolved_security"]["type"],
                    "group": result["resolved_security"]["group"],
                    "board": result["resolved_security"]["board"],
                    "engine": result["resolved_security"]["engine"],
                    "market": result["resolved_security"]["market"],
                },
                "price_snapshot": result["price_snapshot"],
                "analytics": result["analytics_stub"],
            }
        )

    return {
        "schema_version": "1.0",
        "generated_at_utc": snapshot_payload["generated_at_utc"],
        "source": {
            "provider": snapshot_payload["provider"],
            "config_name": config_name,
            "snapshot_path": snapshot_path,
        },
        "event": {
            "id": event_id,
            "trigger_type": trigger_type,
            "comparison_mode": settings["triggers"]["comparison_mode"],
            "poll_interval_minutes": poll_interval,
            "cooldown_applied": False,
            "thresholds": {
                "price_move_pct": threshold,
                "volatility_ratio": float(settings["triggers"]["volatility_ratio_threshold"]),
            },
        },
        "instruments": instruments,
        "model": {
            "kind": "not_computed_yet",
            "horizon_minutes": poll_interval,
            "confidence_label": "low",
        },
        "codex": {
            "prompt_template": settings["codex"]["prompt_template_path"],
        },
    }


def build_instrument_snapshot_lines(evidence_payload: dict[str, Any]) -> tuple[str, str]:
    triggered_lines: list[str] = []
    snapshot_lines: list[str] = []
    threshold = evidence_payload["event"]["thresholds"]["price_move_pct"]

    for instrument in evidence_payload["instruments"]:
        delta = instrument["analytics"]["delta_pct"]
        current_price = instrument["price_snapshot"]["current_price"]
        price_field = instrument["price_snapshot"]["current_price_field"]
        secid = instrument["resolved_security"]["secid"]

        snapshot_lines.append(
            f"- {instrument['label']} [{secid}] price={current_price} "
            f"field={price_field} delta_pct={delta}"
        )

        if delta is not None and abs(float(delta)) >= float(threshold):
            triggered_lines.append(f"- {instrument['label']} delta_pct={delta}")

    if not triggered_lines:
        triggered_lines.append("- No threshold trigger fired. Treat this as a manual evidence build.")

    return "\n".join(triggered_lines), "\n".join(snapshot_lines)
