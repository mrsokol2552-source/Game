from __future__ import annotations

import json
import math
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def rows_from_table(table: dict[str, Any] | None) -> list[dict[str, Any]]:
    if not table:
        return []

    columns = table.get("columns") or []
    data = table.get("data") or []
    return [dict(zip(columns, row, strict=False)) for row in data]


def first_row(table: dict[str, Any] | None) -> dict[str, Any]:
    rows = rows_from_table(table)
    return rows[0] if rows else {}


def first_present_value(sources: list[dict[str, Any]], fields: list[str]) -> tuple[str | None, Any]:
    for field in fields:
        for source in sources:
            if field in source and source[field] is not None:
                return field, source[field]
    return None, None


def to_float(value: Any) -> float | None:
    if value is None:
        return None
    return float(value)


@dataclass(frozen=True)
class ResolvedSecurity:
    query: str
    secid: str
    shortname: str
    name: str
    type: str
    group: str
    primary_boardid: str
    is_traded: bool


class MoexIssClient:
    def __init__(self, base_url: str, timeout_seconds: int = 20, user_agent: str = "FinProgProject/0.1") -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout_seconds = timeout_seconds
        self.user_agent = user_agent

    def get_json(self, path: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        query_string = urlencode(params or {}, doseq=True)
        url = f"{self.base_url}/{path.lstrip('/')}"
        if query_string:
            url = f"{url}?{query_string}"

        request = Request(url, headers={"User-Agent": self.user_agent})
        with urlopen(request, timeout=self.timeout_seconds) as response:
            return json.load(response)

    def search_security(self, resolution: dict[str, Any]) -> ResolvedSecurity:
        query = resolution["query"]
        payload = self.get_json("securities.json", {"q": query, "iss.meta": "off"})
        candidates = rows_from_table(payload.get("securities"))

        if not candidates:
            raise ValueError(f"No search results returned for query '{query}'.")

        ranked = sorted(
            candidates,
            key=lambda item: self._candidate_score(item, resolution),
            reverse=True,
        )

        best = ranked[0]
        if self._candidate_score(best, resolution) <= 0:
            raise ValueError(f"No suitable search result found for query '{query}'.")

        return ResolvedSecurity(
            query=query,
            secid=str(best.get("secid") or ""),
            shortname=str(best.get("shortname") or ""),
            name=str(best.get("name") or ""),
            type=str(best.get("type") or ""),
            group=str(best.get("group") or ""),
            primary_boardid=str(best.get("primary_boardid") or ""),
            is_traded=bool(best.get("is_traded")),
        )

    def fetch_instrument_snapshot(self, instrument: dict[str, Any]) -> dict[str, Any]:
        resolution = instrument["resolution"]
        snapshot = instrument["snapshot"]
        pricing = instrument["pricing"]
        resolved = self.search_security(resolution)

        board = snapshot.get("board") or resolved.primary_boardid
        path = (
            f"engines/{snapshot['engine']}/markets/{snapshot['market']}"
            f"/boards/{board}/securities/{resolved.secid}.json"
        )
        payload = self.get_json(path, {"iss.meta": "off"})

        market_row = first_row(payload.get("marketdata"))
        security_row = first_row(payload.get("securities"))
        dataversion_row = first_row(payload.get("dataversion"))
        value_sources = [market_row, security_row]

        current_field, current_value = first_present_value(
            value_sources,
            pricing["current_price_priority"],
        )
        previous_field, previous_value = first_present_value(
            value_sources,
            pricing["previous_price_priority"],
        )

        current_price = to_float(current_value)
        previous_price = to_float(previous_value)
        delta_pct = None
        log_return = None

        if current_price is not None and previous_price not in (None, 0.0):
            delta_pct = ((current_price / previous_price) - 1.0) * 100.0
            log_return = math.log(current_price / previous_price)

        return {
            "instrument_id": instrument["id"],
            "label": instrument["label"],
            "kind": instrument.get("kind"),
            "fetched_at_utc": utc_now_iso(),
            "resolved_security": {
                "query": resolved.query,
                "secid": resolved.secid,
                "shortname": resolved.shortname,
                "name": resolved.name,
                "type": resolved.type,
                "group": resolved.group,
                "board": board,
                "engine": snapshot["engine"],
                "market": snapshot["market"],
                "is_traded": resolved.is_traded,
            },
            "price_snapshot": {
                "current_price": current_price,
                "current_price_field": current_field,
                "previous_price": previous_price,
                "previous_price_field": previous_field,
                "bid": to_float(market_row.get("BID")),
                "offer": to_float(market_row.get("OFFER")),
                "open": to_float(market_row.get("OPEN")),
                "high": to_float(market_row.get("HIGH")),
                "low": to_float(market_row.get("LOW")),
                "exchange_time": market_row.get("TIME") or market_row.get("UPDATETIME"),
                "system_time": market_row.get("SYSTIME"),
            },
            "analytics_stub": {
                "delta_pct": delta_pct,
                "log_return": log_return,
                "vol_short": None,
                "vol_long": None,
                "vol_ratio": None,
            },
            "marketdata": market_row,
            "security_info": security_row,
            "dataversion": dataversion_row,
        }

    def _candidate_score(self, candidate: dict[str, Any], resolution: dict[str, Any]) -> int:
        score = 0

        secid = str(candidate.get("secid") or "")
        shortname = str(candidate.get("shortname") or "")
        name = str(candidate.get("name") or "")
        candidate_type = str(candidate.get("type") or "")
        candidate_group = str(candidate.get("group") or "")
        board = str(candidate.get("primary_boardid") or "")
        is_traded = bool(candidate.get("is_traded"))
        query = str(resolution.get("query") or "")

        if resolution.get("only_traded") and not is_traded:
            return -1

        if query and (secid == query or shortname == query):
            score += 100
        if resolution.get("prefer_exact_secid") == secid:
            score += 200
        if resolution.get("prefer_exact_shortname") == shortname:
            score += 200
        if resolution.get("prefer_exact_name") == name:
            score += 200
        if resolution.get("type") == candidate_type:
            score += 40
        if resolution.get("group") == candidate_group:
            score += 40
        if resolution.get("primary_boardid") == board:
            score += 30
        if resolution.get("shortname_prefix") and shortname.startswith(resolution["shortname_prefix"]):
            score += 20
        if resolution.get("secid_prefix") and secid.startswith(resolution["secid_prefix"]):
            score += 20
        if is_traded:
            score += 10

        return score
