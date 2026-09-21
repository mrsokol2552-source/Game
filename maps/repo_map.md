# Repo Map

Compact top-level map for fast routing. Use this before opening large docs.

## Primary roots

- [My project](../My%20project)
  - Unity RTS project root. Contains `Assets/`, `Packages/`, `ProjectSettings/`, Unity-generated caches, and editor-local files.
- [Fin_prog_project](../Fin_prog_project)
  - Separate financial monitoring Python prototype with its own [README.md](../Fin_prog_project/README.md), `docs/`, `src/`, `scripts/`, `config/`, `data/`, `tests/`, and `logs/`.
- [Vrem_2](../Vrem_2)
  - Separate Dungeon Master Game / OmniRoute engineering orchestrator project with its own [AGENTS.md](../Vrem_2/AGENTS.md), `dungeon_master_game/`, `provenance/`, and `portable/`.
- [Assets/Scripts](../My%20project/Assets/Scripts)
  - Project-owned runtime/editor code.
- [Assets/Tests](../My%20project/Assets/Tests)
  - PlayMode/EditMode tests, `SampleScene` boot/churn smoke coverage, explicit renderer diagnostics, and performance harnesses.
- [Assets/Scenes](../My%20project/Assets/Scenes)
  - Scene wiring, especially `SampleScene`.
- [docs](../docs)
  - Human-readable architecture, debug, and navigation docs.
- [maps](./)
  - Compact repo-level maps for fast agent/tool routing.
- [supplements](../supplements)
  - Add-on design material, audio integration references, and external content folders.
- [Sprites](../Sprites)
  - Project sprite assets outside the Unity project root.
- [Assets/SmallScaleInt](../My%20project/Assets/SmallScaleInt)
  - Purchased/third-party asset content used by world visuals and character generation.

## Project boundaries

- `My project/`, root `docs/`, root `maps/`, root `supplements/`, `Sprites/`, and `LOGS UNITY/` currently describe or support the Unity RTS project unless a document explicitly says otherwise.
- `Fin_prog_project/` is a separate project. Its own docs and scripts live under that folder and should not be mixed with Unity gameplay/runtime documentation.
- `Vrem_2/` is a separate standalone project with its own nested repository, orchestrator, and test suites. Its configs and tools must not be mixed with Unity or financial project folders.
- Root `scripts/` validate the Unity/game documentation and machine layer. `Fin_prog_project/scripts/` belongs to the financial monitoring project.
- Unity assets, package settings, and generated Unity project files must not be used as storage or configuration for `Fin_prog_project/` or `Vrem_2/`.

## Runtime code roots

- [Assets/Scripts/Domain](../My%20project/Assets/Scripts/Domain)
  - Data models and core gameplay rules.
- [Assets/Scripts/Application](../My%20project/Assets/Scripts/Application)
  - Use cases and state orchestration.
- [Assets/Scripts/Infrastructure](../My%20project/Assets/Scripts/Infrastructure)
  - Configs and persistence.
- [Assets/Scripts/Presentation](../My%20project/Assets/Scripts/Presentation)
  - Unity scene logic, pathfinding, generation, performance systems, UI, input, and view.
- [Assets/Editor](../My%20project/Assets/Editor)
  - Editor utilities and asset/character tools.

## Hot systems

- [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
  - World generation, background conversion, water/rock biomes, per-chunk decoration placement, and far-view bake scheduling.
- [HudController.cs](../My%20project/Assets/Scripts/Presentation/UI/HudController.cs)
  - Main HUD shell; squad strip is now isolated in [HudController.Squads.cs](../My%20project/Assets/Scripts/Presentation/UI/HudController.Squads.cs).
- [ActionsPanel.cs](../My%20project/Assets/Scripts/Presentation/UI/ActionsPanel.cs)
  - Dev actions shell; save/load verification is now isolated in [ActionsPanel.SelfTest.cs](../My%20project/Assets/Scripts/Presentation/UI/ActionsPanel.SelfTest.cs).
- [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
  - Streaming bootstrap, queueing, chunk bounds, and camera-driven scheduler extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
  - Streamed chunk generation, background tile synthesis, prop/tree placement, and chunk cleanup extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.Lifecycle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs)
  - MonoBehaviour lifecycle, generation entrypoints, prepare/bootstrap, and grid/tilemap setup extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
  - Far-view bake scheduling, render objects, chunked bake flow, HUD, loading overlay, and debug bounds extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
  - Background-grid sizing, streamed water/rock masks, land-distance fields, and background-cell lookup helpers extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
  - Biome-aware prop/tree placement, blocked-cell bookkeeping, and spatial placement helpers extracted from `ProceduralEnvironment`.
- [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
  - Palette resolution, biome-mask cleanup, and post-placement obstacle baking helpers extracted from `ProceduralEnvironment`.
- [HexPathfindingBootstrap.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
  - Authoritative hex grid and walkability state.
- [HexPathfindingBootstrap.Walkability.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs)
  - Walkability mutation, collider rebake, persistence, and Native buffer maintenance extracted from `HexPathfindingBootstrap`.
- [HexPathfindingBootstrap.Geometry.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs)
  - World/grid conversion, gizmos, and geometry snapshots extracted from `HexPathfindingBootstrap`.
- [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs) / [PathRequestQueue.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)
  - Path building, caching, and async/job submission.
- [PathManager.Occupancy.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
  - Occupancy caches and occupied-path rejection extracted from `PathManager`.
- [PathManager.Reuse.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
  - Group-path reuse, nearest-free lookup, and cluster helpers extracted from `PathManager`.
- [PathRequestQueue.Dispatch.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
  - Queue draining, job scheduling, and immediate fallback dispatch extracted from `PathRequestQueue`.
- [PathRequestQueue.Completion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)
  - Async job completion, occupancy snapshots, stats, and safe callback/log handling extracted from `PathRequestQueue`.
- [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
  - Shared flow fields for squads and macro movement.
- [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
  - Crowd and influence cost-map maintenance extracted from `FlowFieldManager`.
- [FlowFieldManager.TileGraph.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
  - Coarse tile-graph construction and macro tile-path expansion extracted from `FlowFieldManager`.
- [FlowFieldManager.FieldState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
  - Per-target field storage, LoS state, and next-cell sampling extracted from `FlowFieldManager`.
- [CrowdingResolver.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)
  - Crowd-pressure relief root state, caches, and LateUpdate orchestration.
- [CrowdingResolver.Search.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs)
  - Free-cell search, reservation keys, and odd-r ring enumeration extracted from `CrowdingResolver`.
- [CrowdingResolver.Throttle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs)
  - Adaptive-throttling, effective work-budget, and diagnostic logging extracted from `CrowdingResolver`.
- [CompositionRoot.Setup.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs)
  - Bootstrap helpers, scene singleton wiring, and existing-unit setup extracted from `CompositionRoot`.
- [CompositionRoot.Actions.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs)
  - Save/load wrappers and build/research debug actions extracted from `CompositionRoot`.
- [CompositionRoot.Persistence.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs)
  - Unit snapshot capture/restore and faction visual helpers extracted from `CompositionRoot`.
- [EnemySquadManager.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
  - Squad formation and macro combat state switching.
- [EnemySquadManager.Membership.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs)
  - Squad composition, member recruitment, and center maintenance extracted from `EnemySquadManager`.
- [EnemySquadManager.Tactics.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs)
  - Squad mode hysteresis, target assignment, and flow-anchor logic extracted from `EnemySquadManager`.
- [UnitCombatJobScheduler.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.cs)
  - Nearest-target combat job scheduler core state, lifecycle, and scheduling loop.
- [UnitCombatJobScheduler.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs)
  - Native buffer growth, snapshot ingestion, hash fill, and applyback extracted from `UnitCombatJobScheduler`.
- [UnitCombatJobScheduler.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs)
  - Burst-compiled nearest-enemy job extracted from `UnitCombatJobScheduler`.
- [UnitCombat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
  - Per-unit combat state, tuning, and lifecycle wiring.
- [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
  - Combat tick orchestration, engage/no-target branching, chase/repath handling, and stall recovery extracted from `UnitCombat`.
- [UnitView.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
  - Per-unit movement state, destination/override API, and shared runtime flags.
- [UnitView.Movement.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs)
  - Per-frame movement integration, steering blend, and facing updates extracted from `UnitView`.
- [UnitView.Rendering.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Rendering.cs)
  - Y-sorting bootstrap and selection-gizmo helpers extracted from `UnitView`.
- [UnitSpriteAnimator.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.cs)
  - Directional sprite animation state, renderer binding, and combat event wiring.
- [UnitSpriteAnimator.Playback.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Playback.cs)
  - Per-frame directional playback and crouch/locomotion state machine extracted from `UnitSpriteAnimator`.
- [UnitSpriteAnimator.Combat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Combat.cs)
  - Attack/death animation triggers and external crouch requests extracted from `UnitSpriteAnimator`.
- [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
  - Target arbitration, faction overrides, facing, crouch logic, and repath helper extraction from `UnitCombat`.
- [UnitCombat.FormationFlow.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)
  - Squad metadata, formation slot math, shared hex access, and flow-field steering extraction from `UnitCombat`.
- [UnitCombat.State.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.State.cs)
  - Damage/health mutation, forced-target control, and profile application extracted from `UnitCombat`.
- [OrcaAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
  - Local avoidance scheduler, lifecycle, and main ORCA tick.
- [OrcaAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs)
  - Unit gathering, Native buffer growth, snapshot ingestion, and applyback extracted from `OrcaAvoidanceSystem`.
- [OrcaAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs)
  - Burst-compiled ORCA solver and linear-program helpers extracted from `OrcaAvoidanceSystem`.
- [UnitSoARegistry.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.cs)
  - Shared structure-of-arrays snapshot root for ORCA and combat systems.
- [UnitSoARegistry.Build.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs)
  - Per-frame snapshot projection extracted from `UnitSoARegistry`.
- [UnitSoARegistry.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs)
  - Native buffer ownership, capacity growth, and disposal extracted from `UnitSoARegistry`.
- [MovementJobSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
  - Movement job orchestration, lifecycle, and scheduling.
- [MovementJobSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs)
  - Unit gathering, Native buffer management, and applyback extracted from `MovementJobSystem`.
- [MovementJobSystem.Jobs.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs)
  - Parallel movement integration job extracted from `MovementJobSystem`.
- [LocalAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.cs)
  - Legacy local-avoidance scheduler, ORCA handoff, and job submission state.
- [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs)
  - Unit gathering, Native buffer growth/fill, and steering applyback extracted from `LocalAvoidanceSystem`.
- [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs)
  - Steering job and cell/hash helpers extracted from `LocalAvoidanceSystem`.
- [StuckResolver.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.cs)
  - Stuck-detection root state, singleton lifecycle, and periodic progress scan.
- [StuckResolver.Recovery.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs)
  - Optional combat repath and nearest-free nudge recovery extracted from `StuckResolver`.
- [StuckResolver.State.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs)
  - Per-unit progress reset and stale-entry cleanup extracted from `StuckResolver`.
- [CameraZoom2D.cs](../My%20project/Assets/Scripts/Presentation/Camera/CameraZoom2D.cs)
  - Camera zoom/pan, indirectly affects streaming pressure.

## Primary docs

- [code_map.md](../docs/code_map.md)
  - Full architecture/code map.
- [code_id_index.md](../docs/code_id_index.md)
  - Practical ID lookup and reading routes.
- [debug_playbooks.md](../docs/debug_playbooks.md)
  - Symptom-driven debugging routes.
- [runtime_switches.md](../docs/runtime_switches.md)
  - Important runtime and inspector switches.
- [unity_csharp_performance_optimization_reference.md](../docs/unity_csharp_performance_optimization_reference.md)
  - Practical performance reference for Unity C# runtime work.
- [deep-research-report.md](../docs/deep-research-report.md)
  - Agent-focused repository navigation architecture reference.
- [document_roles.md](../docs/document_roles.md)
  - Role boundaries that keep the docs from duplicating each other.
- [document_roles_index.json](../docs/document_roles_index.json)
  - Machine-readable manifest of governed docs, role IDs, and required links.
- [code_index.json](../docs/code_index.json)
  - Machine-readable system index.
- [machine_layer_audit.md](../docs/machine_layer_audit.md)
  - Formal audit and invariants for the machine-readable routing layer.
- [audit_docs_architecture.py](../scripts/audit_docs_architecture.py)
  - Executable audit for doc ownership, required links, and governed-doc coverage.
- [run_all_repo_audits.py](../scripts/run_all_repo_audits.py)
  - One-command pipeline for strict export refresh, both audits, and the Python regression suite.
- [extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py)
  - Extracts `[SampleSceneRendererProbe]` metrics from Unity `TestResults.xml` after explicit renderer diagnostic runs and can evaluate the provisional owner budget.
- [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py)
  - Extracts `[CombatPressureProbe]` 100v100 path/combat metrics from Unity `TestResults.xml` after explicit combat-pressure diagnostic runs and can evaluate the provisional owner budget.
- [runtime_config_export.json](../docs/runtime_config_export.json)
  - Generated scene/prefab runtime-config snapshot.
- [supplements_index.json](../supplements/supplements_index.json)
  - Machine-readable supplement index.
- [unity_mcp_tools.md](../docs/unity_mcp_tools.md)
  - Available Unity MCP tools and recommended usage.
- [agent_comment_standard.md](../docs/agent_comment_standard.md)
  - Header/comment convention for hot files.

## Supplements

- [supplements/README.md](../supplements/README.md)
  - Top-level supplement index.
- [supplements/supplements_index.json](../supplements/supplements_index.json)
  - Machine-readable supplement routing.
- [supplements/audio/README.md](../supplements/audio/README.md)
  - Audio integration references and source banks.
- [supplements/design/README.md](../supplements/design/README.md)
  - Future gameplay expansion design material.

## High-value scenes and assets

- [SampleScene.unity](../My%20project/Assets/Scenes/SampleScene.unity)
  - Main integration scene.
- [Assets/SmallScaleInt](../My%20project/Assets/SmallScaleInt)
  - Terrain, flora, trees, Character Creator - Modern assets.

## Reading shortcuts

- World generation / map streaming:
  - [repo_map.md](./repo_map.md) -> [code_map.md](../docs/code_map.md) -> [code_id_index.md](../docs/code_id_index.md) route 1
- Combat / squads / movement:
  - [repo_map.md](./repo_map.md) -> [code_id_index.md](../docs/code_id_index.md) route 2 or 3
- Runtime tuning:
  - [runtime_switches.md](../docs/runtime_switches.md)
- Bug-first triage:
  - [debug_playbooks.md](../docs/debug_playbooks.md)
