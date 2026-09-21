# Agent Project Workflow Rules

This document consolidates the working rules for Codex/agent work in this repository. It does not replace [../AGENTS.md](../AGENTS.md) or [../My project/AGENTS.md](../My%20project/AGENTS.md); those files remain the binding instruction sources. Use this file as the compact project-specific checklist for planning, documentation, testing, and handoff.

## Scope

- The Unity RTS lives in [../My project](../My%20project). Root [../docs](../docs), [../maps](../maps), [../supplements](../supplements), root [../scripts](../scripts), [../Sprites](../Sprites), and [../LOGS UNITY](../LOGS%20UNITY) currently support the Unity RTS unless a file explicitly says otherwise.
- The financial monitoring prototype lives in [../Fin_prog_project](../Fin_prog_project). Do not mix its market data, configs, logs, or docs into Unity folders.
- Before editing inside a project folder, read the nearest instruction file. For Unity work, that means [../AGENTS.md](../AGENTS.md) and [../My project/AGENTS.md](../My%20project/AGENTS.md).

## Planning Rules

- Current Unity runtime state, completed work, owner targets, test trust, and next priorities belong in [gameplay_current_state.md](./gameplay_current_state.md).
- Navigation and movement improvement ideas belong in [navigation_optimization_ideas.md](./navigation_optimization_ideas.md), not in the current runtime plan unless promoted.
- Runtime switches, inspector defaults, serialized fields, and tuning risks belong in [runtime_switches.md](./runtime_switches.md).
- Future states/diplomacy/research expansion belongs in [../supplements/design/states_diplomacy_research_automation_plan.md](../supplements/design/states_diplomacy_research_automation_plan.md).
- When Unity work changes roadmap, completed status, readiness, test-trust level, next priority, or owner-input blockers, update [gameplay_current_state.md](./gameplay_current_state.md) in the same change.
- Do not leave important state only in chat. Durable decisions, completed status, measured baselines, blockers, and next steps must be promoted to the owning document.

## Documentation Rules

- One document must have one role. Follow [document_roles.md](./document_roles.md) before adding or expanding docs.
- If a new governed markdown file is added under [../README.md](../README.md), [../docs](../docs), [../maps](../maps), or [../supplements](../supplements), register it in [document_roles.md](./document_roles.md) and [document_roles_index.json](./document_roles_index.json).
- New or changed debugging workflow belongs in [debug_playbooks.md](./debug_playbooks.md).
- New, moved, renamed, or deleted hot code files, `CODE-ID`s, section IDs, or reading routes must update [code_id_index.md](./code_id_index.md), [code_map.md](./code_map.md), [code_index.json](./code_index.json), [../maps/repo_map.md](../maps/repo_map.md), and [../maps/repo_map.json](../maps/repo_map.json) as applicable.
- Root/project boundary, run/build/test command, dependency, or ownership changes should update [../README.md](../README.md), [../AGENTS.md](../AGENTS.md), the relevant nested project instruction file, and repo maps as applicable.
- Avoid broad duplicate docs. If an explanation starts repeating, move it into the owning document, index, generated snapshot, or script output and link to it.

## Coding Rules

- Prefer existing codebase patterns, helpers, ownership boundaries, and partial-class organization over new abstractions.
- Keep edits scoped to the requested behavior. Do not refactor unrelated areas just because they are nearby.
- Do not revert or overwrite unrelated local changes. The worktree may already contain user or prior-agent changes.
- Treat Unity dependency/project-setting files as sensitive: [../My project/Packages/manifest.json](../My%20project/Packages/manifest.json), [../My project/Packages/packages-lock.json](../My%20project/Packages/packages-lock.json), and [../My project/ProjectSettings](../My%20project/ProjectSettings).
- Preserve Unity `.meta` files when adding, moving, or deleting Unity assets.
- Do not manually edit Unity generated/local caches such as [../My project/Library](../My%20project/Library), [../My project/Temp](../My%20project/Temp), [../My project/Logs](../My%20project/Logs), [../My project/UserSettings](../My%20project/UserSettings), or generated solution/project files.

## Unity Rendering And Worldgen Rules

- Do not migrate production rendering directly to shaders while renderer inputs still depend on live tilemap reads.
- New render paths should consume extracted payloads and snapshots where possible: background, ground, biome-mask, placement, and far-view sources.
- Direct tilemap reads should stay isolated behind explicit `LegacyTilemap` fallback helpers.
- Experimental shader-facing validation renderers must stay disabled by default until parity and performance are measured.
- Gameplay, navigation, combat, save/load, and walkability data remain CPU-side unless a separate migration plan is written.

## Testing Contract

- Tests are written for Codex/agent work, not just for human QA. When a task touches a covered system, run the smallest relevant test or diagnostic route that can validate it.
- Unity `Gate` tests are normal regression checks. `SceneGate` tests verify real-scene wiring. `Diagnostic` tests are explicit/manual probes and should not silently become normal gates.
- For C# changes, refresh/import assets and run Unity script recompilation through the Unity MCP workflow documented in [unity_mcp_tools.md](./unity_mcp_tools.md).
- For root docs, maps, machine indexes, scripts, or runtime snapshots, run the root audit pipeline:

```powershell
python .\scripts\run_all_repo_audits.py --with-full-export
```

- For Python audit tests only, use:

```powershell
python -m unittest discover .\scripts\tests -v
```

- If Unity MCP `run_tests` returns `Connection failed`, do not call the test passed just from MCP. Check fresh Unity XML at the local Test Runner result path when available, use the relevant extractor when one exists, and report the MCP transport limitation separately.
- After a PlayMode connection failure, a following EditMode call can surface stale PlayMode result names or cleanup logs. Record that as observed PlayMode output only, then rerun intended EditMode verification before recording EditMode pass/fail.

## Current Agent-Facing Diagnostics

- `SampleScene` renderer/streaming diagnostics use `SampleSceneRendererDiagnosticsTests` and [../scripts/extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py). Use `--check-provisional-budget` when the run should fail on current hard provisional limits.
- Owner-target 100v100 combat pressure diagnostics use `FpsStressTests.OwnerTarget100v100_PathPressure_LogsCombatPressureProbe` and [../scripts/extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py). Use `--check-provisional-budget`.
- Current hard provisional combat/renderer limits include failed XML/test result, wrong required unit counts where applicable, missing core metrics, `maxFrameMs > 203`, and diagnostic duration over 60 seconds.
- For combat pressure, no path builds or no attack events are hard failures.
- The 144 FPS target is currently a warning through `avgFrameMs`, not a hard failure, until target-device data justifies a tighter budget.

## Owner Targets

- Target hardware: 8 GB RAM, 8 CPU cores around 3.6 GHz, and GPU not below AMD Radeon RX 7600 XT.
- FPS target: 144 FPS, about 6.94 ms average frame time. This is currently aspirational and reported as a warning in diagnostics.
- Temporary spike cap: 203 ms. This is a rough provisional hard cap, not the final quality goal.
- Loading/warmup target: 60 seconds provisional maximum.
- Map size path: current large working size is 4096x4096; intended release-scale target is 8192x8192.
- Unit-count target: 200 active combat units, split as 100 player units and 100 enemy units. This is not a final release cap.
- Visual target: prototype parity for now. Obvious holes, flicker, broken layer order, missing readable biome shapes, and unusable far-zoom output are regressions; exact pixel/color/edge parity can be tuned iteratively.

## Handoff Rules

- Final responses should state what changed, what was verified, what could not be verified, and where the durable state was recorded.
- If a result depends on stale XML, MCP transport fallback, or manual Unity UI evidence, say that explicitly.
- If a change reveals a new blocker or owner decision, record it in [gameplay_current_state.md](./gameplay_current_state.md) under the relevant owner-input or planning area.
