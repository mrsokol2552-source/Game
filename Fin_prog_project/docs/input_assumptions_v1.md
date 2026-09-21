# Input Assumptions V1

## Status

This document fixes the first implementation assumptions for the MVP as of April 15, 2026.

## Fixed For V1

### Polling And Trigger Base

- Poll interval: `30` minutes.
- Primary comparison mode for the `±1%` trigger: `previous_poll`.
- Cooldown will be applied per instrument and added later in the analytics layer.

### Data Provider

- Primary source: MOEX ISS JSON endpoints.
- Delayed or partially limited market data is acceptable for MVP.
- Every downstream message must indicate that the source is MOEX ISS and may be delayed.

### Instruments In Scope

- `usd_rub_tom`
  - Search alias: `USDRUB_TOM`
  - Verified live trading `secid` on April 15, 2026: `USD000UTSTOM`
  - Board: `CETS`
  - Engine/market: `currency/selt`
- `cny_rub_tom`
  - Search alias: `CNYRUB_TOM`
  - Verified `secid`: `CNYRUB_TOM`
  - Board: `CETS`
  - Engine/market: `currency/selt`
- `gold_rub_tom`
  - Search alias: `GLDRUB_TOM`
  - Verified `secid`: `GLDRUB_TOM`
  - Board: `CETS`
  - Engine/market: `currency/selt`
  - This is the local gold-metal proxy for v1, not a USD/oz benchmark.
- `brent_front`
  - Resolver alias: `BR`
  - Market family: MOEX Brent futures on `RFUD`
  - The exact contract must be resolved dynamically from search results because the front month rolls.

### Price Selection Priority

- FX and gold-metal instruments:
  - Current price: `LAST -> MARKETPRICE -> WAPRICE -> OPEN`
  - Previous price: `PREVPRICE -> PREVWAPRICE`
- Brent futures:
  - Current price: `LAST -> SETTLEPRICE -> OPEN`
  - Previous price: `PREVPRICE -> PREVSETTLEPRICE`

### Analytics Boundary

- The algorithm produces the numbers.
- Codex produces the text interpretation.
- Codex input must be built strictly from `evidence.json` plus a fixed prompt template.

### Delivery Boundary

- Primary delivery target for MVP: Yandex Messenger web automation through Playwright.
- Operational fallback for the first dry-runs: local file output and logs.
- A future official API adapter must remain possible without changing the upstream analytics pipeline.

## Open Decisions

- Confirm whether the production gold instrument must remain `GLDRUB_TOM` or switch to a USD-denominated benchmark proxy.
- Confirm the exact rollover rule for Brent front-month selection.
- Confirm whether the long-term reserve delivery channel should be an official bot/API path or a second file-based sink.

## Rule

If any of these assumptions changes, update this document first and only then change config or code.
