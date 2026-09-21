from __future__ import annotations

from typing import Any

from marketwatch.evidence import build_instrument_snapshot_lines


def render_prompt(template_text: str, evidence_payload: dict[str, Any]) -> str:
    triggered_lines, snapshot_lines = build_instrument_snapshot_lines(evidence_payload)
    model = evidence_payload.get("model", {})

    return template_text.format(
        event_id=evidence_payload["event"]["id"],
        trigger_type=evidence_payload["event"]["trigger_type"],
        comparison_mode=evidence_payload["event"]["comparison_mode"],
        poll_interval_minutes=evidence_payload["event"]["poll_interval_minutes"],
        triggered_instruments=triggered_lines,
        instrument_snapshot=snapshot_lines,
        model_kind=model.get("kind", "unknown"),
        model_horizon=model.get("horizon_minutes", "unknown"),
        confidence_label=model.get("confidence_label", "unknown"),
    )
