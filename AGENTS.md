# AGENTS.md

Scope: the whole repository. More specific rules in nested `AGENTS.md` files apply inside their folders.

## Chosen Structure

- Root `AGENTS.md` owns repository-wide boundaries, shared safety rules, and shared verification.
- `My project/AGENTS.md` owns Unity RTS project rules.
- `Fin_prog_project/AGENTS.md` owns financial monitoring project rules.
- `Vrem_2/AGENTS.md` and `Vrem_2/dungeon_master_game/AGENTS.md` own the OmniRoute-backed engineering orchestrator and Dungeon Master Game project rules.

This structure matches the current tree: separate projects live in one repository workspace, while root docs and scripts mostly support the Unity RTS.

## Project Boundaries

- `My project/` is the Unity RTS project. Its owned tracked project data is under `Assets/`, `Packages/`, and `ProjectSettings/`.
- `Fin_prog_project/` is the financial monitoring prototype. Its owned project data is under its own `docs/`, `maps/`, `scripts/`, `data/`, `config/`, `src/`, `tests/`, `logs/`, and `supplements/`.
- `Vrem_2/` is the Dungeon Master Game / OmniRoute engineering orchestrator project. Its owned project data is under its own nested repository (`.git/`, `.agents/`, `dungeon_master_game/`, `provenance/`, `portable/`).
- Root `docs/`, `maps/`, `supplements/`, `Sprites/`, `LOGS UNITY/`, and root `scripts/` currently belong to the Unity RTS documentation/tooling layer unless a file explicitly states otherwise.
- Do not mix Unity runtime settings, assets, generated files, or project configuration with `Fin_prog_project/` or `Vrem_2/`.
- Do not mix `Fin_prog_project` market data, prompts, logs, or configs into Unity or `Vrem_2` folders.
- Do not mix `Vrem_2` orchestrator configs, missions, provenance runs, OmniRoute data, or python environments into Unity or financial folders.

## Shared Rules

- Inspect the nearest `AGENTS.md` before editing inside a project folder.
- Do not revert or overwrite unrelated local changes.
- Use repository evidence: existing scripts, configs, docs, and project files. Do not invent commands or processes as if they already exist.
- Keep generated artifacts out of manual edits unless the task is explicitly to regenerate them.
- Treat documentation updates as part of the same task, not follow-up cleanup, whenever code or project state changes the source of truth.
- Do not leave important state only in chat. Promote durable decisions, completed status, and next steps into the owning documentation file.
- Treat dependency and project-setting files as sensitive: Unity `Packages/manifest.json`, Unity `Packages/packages-lock.json`, Unity `ProjectSettings/`, and financial project config/schema files.
- If documentation under root `README.md`, `docs/`, `maps/`, or `supplements/` changes, run the root audit pipeline from the repository root:
  `python .\scripts\run_all_repo_audits.py --with-full-export`

## Documentation Freshness Contract

Before the final response for any non-trivial change, check whether the change affects durable project knowledge. If yes, update the owning document in the same change.

Update triggers:

- Roadmap, completed status, next priority, readiness, or test-trust changes: update the owning plan file listed below.
- New or changed Unity inspector/runtime switches, defaults, serialized fields, or tuning risks: update `docs/runtime_switches.md`; if root runtime snapshots are affected, run `python .\scripts\run_all_repo_audits.py --with-full-export`.
- New, moved, renamed, or deleted hot code files, `CODE-ID`s, section IDs, or reading routes: update `docs/code_id_index.md`, `docs/code_map.md`, `docs/code_index.json`, `maps/repo_map.md`, and `maps/repo_map.json` as applicable.
- New or changed debugging workflow: update `docs/debug_playbooks.md`.
- New governed markdown file under `README.md`, `docs/`, `maps/`, or `supplements/`: update `docs/document_roles.md` and `docs/document_roles_index.json`.
- Root/project boundary, run/build/test command, dependency, or ownership change: update `README.md`, this `AGENTS.md`, the relevant nested `AGENTS.md`, and repo maps as applicable.
- Financial project MVP order, assumptions, risk, schema, script, or delivery-process change: update the matching `Fin_prog_project/docs/` file and `Fin_prog_project/README.md` if commands changed.

If a documentation update cannot be made because the needed information depends on the user, record that uncertainty in the owning plan's owner-input/blocker section or explicitly state the gap in the final response.

## Current Plan Files

Unity RTS:

- `docs/gameplay_current_state.md` - current Unity runtime state, shader/worldgen order, and prioritized technical backlog.
- `docs/navigation_optimization_ideas.md` - navigation and movement improvement backlog.
- `docs/runtime_switches.md` - high-value inspector/runtime switches and tuning risks.
- `supplements/design/states_diplomacy_research_automation_plan.md` - future states/diplomacy/research expansion plan.

Maintenance rule: update `docs/gameplay_current_state.md` in the same change whenever Unity work changes the current roadmap, completed status, or next priority.

Financial monitoring:

- `Fin_prog_project/docs/mvp_stack_risks_plan.md` - MVP scope, stack, risks, and implementation order.
- `Fin_prog_project/docs/bottlenecks_and_solutions.md` - bottlenecks and mitigations.
- `Fin_prog_project/docs/input_assumptions_v1.md` - first-version input assumptions.

Vrem_2 / OmniRoute orchestrator:

- `Vrem_2/dungeon_master_game/docs/operations/omniroute-runtime.md` - OmniRoute writer runtime and model routing operations.
- `Vrem_2/dungeon_master_game/docs/operations/gemini38-migration-20260918.md` - Gemini 3.8 primary migration status and verified behavior.
- `Vrem_2/dungeon_master_game/docs/operations/orchestrator-portability-and-bottlenecks.md` - Engineering orchestrator efficiency bottlenecks, coupling audit, and cross-project adoption roadmap.

## Shared Verification

- Root docs/machine layer: `python .\scripts\run_all_repo_audits.py --with-full-export`
- Root Python audit tests only: `python -m unittest discover .\scripts\tests -v`
- Unity compile/tests are not represented by a checked-in shell script. Use Unity Editor/Test Runner or the configured Unity MCP workflow when available.
- Financial project currently has runnable scripts but no committed canonical test command.
- Vrem_2 / OmniRoute orchestrator: `powershell.exe -ExecutionPolicy Bypass -File tools\check.ps1 -Level fast` inside `Vrem_2/dungeon_master_game/`.

## Generated and Local Files

- Unity generated/local: `My project/Library/`, `My project/Temp/`, `My project/Logs/`, `My project/UserSettings/`, `My project/Obj/`, `My project/Build*/`, Unity-generated `.csproj`, `.sln`, `.slnx`, `.user`, `.pidb`, `.svd`, `.pdb`, `.mdb`, `.opendb`, `.VC.db`.
- Financial generated/local: `Fin_prog_project/**/__pycache__/`, `Fin_prog_project/logs/`, generated snapshots under `Fin_prog_project/data/exports/` and `Fin_prog_project/data/evidence/` unless a task explicitly asks to update sample or latest data.
- Vrem_2 generated/local: `Vrem_2/dungeon_master_game/.venv/`, `Vrem_2/dungeon_master_game/.ruff_cache/`, `Vrem_2/dungeon_master_game/**/__pycache__/`, `Vrem_2/dungeon_master_game/artifacts/runs/`, `%LOCALAPPDATA%/Vrem/`.
- Root logs and local exports: `LOGS UNITY/`, `unity-editmode-test.log`, `unity-playmode-test.log`, ad hoc spreadsheet drafts, and other local diagnostic files are not project architecture.

## Agent Guidelines

- **Strict Orchestrator Delegation Policy (Zero Manual Coding by Primary Assistant)**:
  Antigravity / the primary assistant is strictly prohibited from directly modifying, refactoring, implementing, or editing application or domain code files in Vrem / Dungeon Master Game when the OmniRoute-backed engineering orchestrator is alive and operational.
  Direct manual coding by the primary assistant is an emergency fallback permitted ONLY if a catastrophic bootstrap defect demonstrably prevents the orchestrator itself from executing.
  In all normal operations:
  1. The assistant acts strictly as the **Supervisor / Dispatcher**.
  2. The assistant formulates an `EngineeringMissionV1` (`mission.json`), clearly defining the objective, explicit acceptance criteria, bounded scope hints, and test commands.
  3. The assistant dispatches the mission to the orchestrator:
     `.\.venv\Scripts\python.exe tools\orchestrator.py mission <mission.json> --auto`
  4. The assistant consumes only the compact handoff (`supervisor_summary.json`), verification verdict, and provenance receipt.
  5. The assistant must NOT waste its own context or user subscription manually inspecting files, writing patches, or running iterative pytest loops that belong to the autonomous orchestrator.

- **Bounded Improvement Capture**: After completing a task, briefly record only directly observed, actionable follow-up ideas in the owning backlog or documentation when they are useful. Do not continue implementation, refactoring, architecture work, or prompt optimization after the requested outcome and acceptance criteria are satisfied unless the extra work is required to correct a concrete blocker or regression, or the user explicitly authorizes it. Optional ideas do not reopen a completed task, and having no follow-up idea is not a blocker to stopping.

