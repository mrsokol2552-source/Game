# Repository Workspace

This repository currently contains three separate projects:

- `My project/` - a 2D RTS Unity game with hex-grid navigation, unit combat, economy/research, save/load, procedural world generation, and shader-migration work.
- `Fin_prog_project/` - a separate financial monitoring prototype for market snapshots, `evidence.json`, `codex exec` analysis, and future delivery automation. See [Fin_prog_project/README.md](./Fin_prog_project/README.md).
- `Vrem_2/` - a separate Dungeon Master Game / OmniRoute engineering orchestrator project with its own model routing, missions, and provenance tracking. See [Vrem_2/AGENTS.md](./Vrem_2/AGENTS.md).

The root `docs/`, `maps/`, `supplements/`, `Sprites/`, `LOGS UNITY/`, and root audit scripts primarily document and support the Unity RTS unless a file explicitly says otherwise.

## Unity Game Requirements

- Unity 6000.4.1f1 (see `My project/ProjectSettings/ProjectVersion.txt`)
- Input System package enabled (legacy input also supported)

## Project layout

- `My project/` - Unity project root
- `Fin_prog_project/` - separate financial monitoring project root
- `Vrem_2/` - separate Dungeon Master Game / OmniRoute orchestrator project root
- `docs/` - gameplay state and architecture notes
- `maps/` - compact repo-level routing maps and machine layer entrypoints
- `supplements/` - translated add-on design/audio references plus machine supplement index
- `Sprites/` - external sprite packs (see license files inside)
- `LOGS UNITY/` - editor/runtime logs (optional)

## Quick start

1. Open `My project/` in Unity Hub (use 6000.4.1f1).
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

SampleScene already includes `Bootstrap` (CompositionRoot), `HudController`, `InputController`, `ActionsPanel`, and
`ResearchPanel`. If anything is missing, use the menu items in `Tools/RTS/Setup`.

Config assets live in `Assets/ScriptableObjects/Configs` and can be created via `Assets > Create > Configs`:

- `Game Config` (starting resources)
- `Building Config` (test build costs)
- `Research Config` (test research list)

## Controls

- Camera: mouse wheel or +/- to zoom; MMB drag or WASD to pan.
- Spawn/move: LMB spawns a player unit; RMB sets destination for the last spawned unit.
- Hotkeys: `M` +10 Materials, `F` +5 Food, `B` test build, `R` start first research, `C` complete first research,
  `E` spawn enemy at cursor.

## UI and dev tools

- HUD shows resource stocks and Save/Load buttons.
- Dev panel (ActionsPanel) can spawn units, add resources, run build/research helpers, clear the save file, and run a
  quick save/load self-test.
- Research panel lists items from `TestResearch` and lets you start/complete them.

## Save/Load

- File path: `Application.persistentDataPath/save.json`.
- Saves: resources, units (position/destination/faction/HP), research status, and blocked hex cells.

## Tech notes

- Hex grid pathfinding with jobified A* (`HexPathfindingBootstrap`, `PathRequestQueue`), using enemy-only occupancy during jobs.
- Unit combat/targeting with spatial-hash job scheduler (`UnitCombatJobScheduler`), squad targeting (`EnemySquadManager` can also drive player units), and optional OccupancyHash fallback when the scheduler is disabled.
- Layered architecture: Domain / Application / Infrastructure / Presentation.

## Quick code map

- Bootstrap: `CompositionRoot` auto-adds `CameraZoom2D`, `HexPathfindingBootstrap`, `ProceduralObstacles`, `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, and `PathRequestQueue`.
- Input/UI: `InputController`, `UnitSpawnerCommander`, `HudController`, `ActionsPanel`, `ResearchPanel`.
- Units/combat: `UnitView`, `UnitCombat`, `UnitPathFollower`, `UnitHpOverlay`, `UnitVisualCulling`.
- Pathing: `PathManager`, `PathRequestQueue`, `HexPathfindingBootstrap`, `CrowdingResolver`.
- Persistence/tests: `SaveSystem`, `SaveGame`/`LoadGame`, PlayMode tests (see `Assets/Tests`).

## Docs and tests

- [docs/gameplay_current_state.md](./docs/gameplay_current_state.md) for the detailed systems overview.
- [docs/agent_project_workflow_rules.md](./docs/agent_project_workflow_rules.md) for the consolidated Codex/agent project working rules.
- [docs/navigation_optimization_ideas.md](./docs/navigation_optimization_ideas.md) for consolidated navigation/avoidance ideas.
- [docs/code_map.md](./docs/code_map.md) for a file-by-file map and runtime flows.
- [maps/repo_map.md](./maps/repo_map.md) / [maps/repo_map.json](./maps/repo_map.json) for fast repo routing.
- [docs/document_roles_index.json](./docs/document_roles_index.json) for the machine-readable manifest of governed documentation roles.
- [supplements/supplements_index.json](./supplements/supplements_index.json) for machine-readable routing into the supplements layer.
- [docs/runtime_config_export.json](./docs/runtime_config_export.json) for the generated scene/prefab runtime-config snapshot.
- [scripts/export_runtime_config.py](./scripts/export_runtime_config.py) with [scripts/runtime_config_manifest.json](./scripts/runtime_config_manifest.json) to regenerate that snapshot after inspector/scene/prefab changes.
- [scripts/audit_docs_architecture.py](./scripts/audit_docs_architecture.py) to validate document ownership, required cross-links, and coverage of governed markdown files.
- [scripts/audit_machine_layer.py](./scripts/audit_machine_layer.py) to validate the machine/document routing layer after structure changes.
- [scripts/run_all_repo_audits.py](./scripts/run_all_repo_audits.py) to run the full repo audit pipeline in one command.
- [scripts/extract_sample_scene_renderer_probe.py](./scripts/extract_sample_scene_renderer_probe.py) to extract `[SampleSceneRendererProbe]` baseline metrics from Unity `TestResults.xml` and evaluate the provisional owner budget.
- [scripts/extract_combat_pressure_probe.py](./scripts/extract_combat_pressure_probe.py) to extract `[CombatPressureProbe]` 100v100 metrics from Unity `TestResults.xml` and evaluate the provisional owner budget.
- [docs/machine_layer_audit.md](./docs/machine_layer_audit.md) for machine-layer ownership, validation scope, and the maintenance rule.
- Runtime config export modes:
  - `python ./scripts/export_runtime_config.py --strict`
  - `python ./scripts/export_runtime_config.py --full --output ./docs/runtime_config_export_full.json`
- Audit / test commands:
  - `python ./scripts/audit_machine_layer.py`
  - `python ./scripts/audit_docs_architecture.py`
  - `python ./scripts/extract_sample_scene_renderer_probe.py --json`
  - `python ./scripts/extract_sample_scene_renderer_probe.py --json --check-provisional-budget`
  - `python ./scripts/extract_combat_pressure_probe.py --json --check-provisional-budget`
  - `python -m unittest discover ./scripts/tests -v`
  - `python ./scripts/run_all_repo_audits.py --with-full-export`
- Unity Test Runner: EditMode and PlayMode tests under `Assets/Tests`.

## Documentation rules

These rules are mandatory for all future documentation work:

1. One document = one role.
2. Do not create broad duplicate documents that overlap existing ownership.
3. If an explanation starts repeating, move it into the owning index, script, or canonical document and link to it instead of copying it.
4. Prefer machine-readable indexes or scripts over repeated manual lists when the same structure must stay in sync.
5. When in doubt, follow [docs/document_roles.md](./docs/document_roles.md) and extend the existing layer instead of creating a parallel one.

## Performance tests

- PlayMode `FpsStressTests` logs average FPS per scenario; `AlliesVs20Enemies_Dist50` and `AlliesVs20Enemies_Dist100` run fixed 20v20 diagnostics, `AlliesVsEnemiesSweep_Dist200` runs a sweep from 10v10 to 20v20, and `OwnerTarget100v100_PathPressure_LogsCombatPressureProbe` logs the provisional 200-unit owner target with path and combat activity as `[CombatPressureProbe]`.
