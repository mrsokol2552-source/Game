# 2D RTS Prototype (Unity)

Small 2D RTS sandbox built in Unity with hex-grid navigation, unit combat, economy/research, and save/load.

## Requirements

- Unity 6000.2.7f2 (Unity 6.2)
- Input System package enabled (legacy input also supported)

## Project layout

- `My project/` - Unity project root
- `docs/` - gameplay state and architecture notes
- `Sprites/` - external sprite packs (see license files inside)
- `LOGS UNITY/` - editor/runtime logs (optional)

## Quick start

1. Open `My project/` in Unity Hub (use 6000.2.7f2).
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

- `docs/gameplay_current_state.md` for the detailed systems overview.
- `docs/navigation_optimization_ideas.md` for consolidated navigation/avoidance ideas.
- `docs/code_map.md` for a file-by-file map and runtime flows.
- Unity Test Runner: EditMode and PlayMode tests under `Assets/Tests`.

## Performance tests

- PlayMode `FpsStressTests` logs average FPS per scenario; `AlliesVs200_Dist200` runs a sweep (10..20 units per side), `AlliesVs200_Dist50/100` run fixed counts.
