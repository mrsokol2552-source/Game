# AGENTS.md

Scope: `Fin_prog_project/`, the financial monitoring project.

Follow the root `AGENTS.md` first. These rules add financial-project-specific constraints.

## Project Identity

- This is a separate Python prototype for market monitoring, `evidence.json` generation, `codex exec` analysis, and future delivery automation.
- It is not part of the Unity RTS runtime.
- Project plans live in `docs/`, especially `docs/mvp_stack_risks_plan.md`, `docs/bottlenecks_and_solutions.md`, and `docs/input_assumptions_v1.md`.

## Runnable Scripts

Run these from `Fin_prog_project/` unless a task says otherwise:

- Fetch MOEX snapshot:
  `python .\scripts\fetch_moex_snapshot.py --config .\config\appsettings.example.json`
- Build evidence and prompt from the latest snapshot:
  `python .\scripts\build_evidence.py --config .\config\appsettings.example.json`
- Preflight Codex runner without executing analysis:
  `python .\scripts\run_codex_analysis.py --config .\config\appsettings.example.json --dry-run`

No build or packaging command is currently committed for this project.

There is no committed canonical test command yet. The docs mention `pytest` as planned, but the repository currently has no pytest config or real test files for this project.

## Sensitive Areas

- Do not commit real secrets, chat credentials, browser profiles, Codex auth material, or private API tokens.
- `config/appsettings.example.json` is a template. If a real `appsettings.json` is added later, treat it as local/private unless the user explicitly decides otherwise.
- `config/evidence.schema.json` is a contract for generated evidence payloads. Update code, samples, and docs together if the schema changes.
- `data/exports/`, `data/evidence/`, and `logs/` are generated/runtime artifact areas unless a task explicitly asks to update checked-in sample/latest artifacts.
- `__pycache__/` folders are generated and should not be edited.

## Boundaries

- Do not put financial monitoring code, market snapshots, prompt templates, or delivery logs under Unity folders.
- Do not use Unity root `docs/` as the canonical plan for this project; use `Fin_prog_project/docs/`.
- Do not change Unity `Packages/`, `ProjectSettings/`, or assets for financial-project tasks.

## Documentation Matrix

- `docs/mvp_stack_risks_plan.md`: MVP scope, implementation order, stack decisions, and next practical step.
- `docs/bottlenecks_and_solutions.md`: risks, bottlenecks, mitigations, and reliability decisions.
- `docs/input_assumptions_v1.md`: instruments, thresholds, delivery assumptions, comparison rules, and user-confirmed constraints.
- `maps/repo_map.md`: project-local structure and routing.
- `README.md`: runnable scripts, project layout, and operational rules.
- `config/evidence.schema.json`: evidence payload contract; update code, samples, and docs together when it changes.

Update the owning file in the same change whenever the project state, commands, schema, assumptions, risks, or next step changes. Do not leave financial-project decisions only in chat.

## Owner Input Required

These cannot be guaranteed without user confirmation or local credentials:

- Final MOEX instruments for Brent/gold proxies and any replacement tickers.
- Delivery channel, browser session, selectors, and credentials.
- Local `codex exec` authorization state and acceptable timeout/model behavior.
- Production alert thresholds, cooldown rules, and message format.
