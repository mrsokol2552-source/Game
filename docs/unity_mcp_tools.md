# Unity MCP Tools

This document tracks the Unity MCP tools available in this workspace and how they should be used during day-to-day work on the project.

See also:

- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)
- [runtime_switches.md](./runtime_switches.md)
- [repo_map.md](../maps/repo_map.md)

## Autonomy

Per current user instruction, Unity MCP tools may be used without asking for per-action approval.

Use them pragmatically:

- inspect scene state before changing code blindly
- inspect logs/tests before guessing
- prefer scene edits through Unity MCP over fragile scene-file patching when possible
- prefer batch operations for repetitive scene changes

## Available callable Unity MCP tools

### Scene management

- `get_scene_info`
- `create_scene`
- `load_scene`
- `save_scene`
- `delete_scene`
- `unload_scene`

Use for:

- confirming which scene is active
- saving scene changes after MCP edits
- loading test/setup scenes without touching files manually

### Hierarchy and GameObjects

- `get_gameobject`
- `select_gameobject`
- `update_gameobject`
- `delete_gameobject`
- `duplicate_gameobject`
- `reparent_gameobject`

Use for:

- inspecting runtime/editor hierarchy state
- changing tags, layers, active state, names
- fixing parenting issues in scene setup

### Transform operations

- `move_gameobject`
- `rotate_gameobject`
- `scale_gameobject`
- `set_transform`

Use for:

- camera setup
- correcting misplaced spawned helpers
- bulk scene adjustments when exact transforms matter

### Components

- `update_component`

Use for:

- changing inspector-backed fields on existing scene objects
- adding a missing component if absent

### Assets, prefabs, packages

- `add_asset_to_scene`
- `create_prefab`
- `add_package`

Use for:

- testing prefabs in-scene
- adding Unity packages
- creating a prefab scaffold from generated objects/components

### Materials

- `create_material`
- `assign_material`
- `get_material_info`
- `modify_material`

Use for:

- quick rendering/debug material setup
- assigning generated or temporary materials to scene objects

### Editor and orchestration

- `execute_menu_item`
- `batch_execute`
- `recompile_scripts`

Use for:

- triggering Unity menu commands
- grouping many scene edits into one transaction
- forcing recompilation after codegen or editor setup changes

### Diagnostics and testing

- `get_console_logs`
- `run_tests`
- `send_console_log`

Use for:

- checking current Unity errors/warnings before changing code
- running EditMode/PlayMode tests from the editor side
- writing explicit breadcrumbs to the Unity console during debugging

## Available Unity MCP resources

- `unity://menu-items`
- `unity://scenes_hierarchy`
- `unity://packages`
- `unity://assets`
- `unity://tests/EditMode`
- `unity://tests/PlayMode`
- `unity://tests/`
- `unity://logs/...`

Use these resources when a structured read is enough and no mutation is needed.

## Recommended usage in this project

### For scene/setup bugs

Open in this order:

1. `get_scene_info`
2. `unity://scenes_hierarchy`
3. `get_gameobject`
4. `get_console_logs`

Typical targets:

- `SampleScene`
- `ProceduralEnvironment`
- `HexPathfinding`
- `PathManager (Auto)`
- camera objects

### For world generation / streaming debugging

Use:

- `get_console_logs`
- `get_gameobject` on `ProceduralEnvironment`
- `get_gameobject` on `HexPathfinding`
- `get_scene_info`

Then route into:

- [debug_playbooks.md](./debug_playbooks.md)
- [runtime_switches.md](./runtime_switches.md)
- [code_id_index.md](./code_id_index.md) route 1

### For UI and hierarchy issues

Use:

- `unity://scenes_hierarchy`
- `get_gameobject`
- `update_component`
- `set_transform`

### For animation/prefab iteration

Use:

- `add_asset_to_scene`
- `create_prefab`
- `update_component`
- `save_scene`

## Preferred workflow rules

- Prefer `get_console_logs(includeStackTrace=false)` first for broad diagnosis.
- Prefer `batch_execute` when making multiple scene edits.
- Prefer Unity MCP scene edits over manual `.unity` patching unless file-level changes are unavoidable.
- If a bug may be scene-state-specific, inspect with Unity MCP before changing runtime code.
- If code and scene disagree, trust live Unity state first and reconcile code second.

## Current project note

For this repository, Unity MCP is especially useful for:

- `ProceduralEnvironment` and world-streaming setup
- camera positioning / zoom thresholds
- auto-created helper objects
- verifying that generated scene objects actually exist and are configured as expected
- checking whether a problem is in code, inspector state, or active scene state
