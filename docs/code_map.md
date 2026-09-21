# Code Map (Unity RTS Prototype)

This document is a code-level map for quick navigation. It complements [gameplay_current_state.md](./gameplay_current_state.md) and focuses on where behavior lives in the codebase.
For direct `CODE-ID` lookup, use [code_id_index.md](./code_id_index.md).
For symptom-first debugging, use [debug_playbooks.md](./debug_playbooks.md).
For high-value runtime/inspector parameters, use [runtime_switches.md](./runtime_switches.md).
For document boundaries and anti-duplication rules, use [document_roles.md](./document_roles.md).
For machine-readable system lookup, use [code_index.json](./code_index.json).
For the machine-readable documentation-governance manifest, use [document_roles_index.json](./document_roles_index.json).
For formal machine-layer ownership, validation rules, and the audit command, use [machine_layer_audit.md](./machine_layer_audit.md).
For the executable machine-layer audit entrypoint, use [scripts/audit_machine_layer.py](../scripts/audit_machine_layer.py).
For the executable docs-architecture audit entrypoint, use [scripts/audit_docs_architecture.py](../scripts/audit_docs_architecture.py).
For the one-command repo validation pipeline, use [scripts/run_all_repo_audits.py](../scripts/run_all_repo_audits.py).
For exported scene/prefab inspector-visible runtime values, use [runtime_config_export.json](./runtime_config_export.json).
For regenerating that snapshot, use [scripts/export_runtime_config.py](../scripts/export_runtime_config.py) with [runtime_config_manifest.json](../scripts/runtime_config_manifest.json).
For navigation/audit fixtures and regression tests, use [scripts/tests](../scripts/tests).
For a practical performance reference, use [unity_csharp_performance_optimization_reference.md](./unity_csharp_performance_optimization_reference.md).
For available Unity MCP capabilities and when to use them, use [unity_mcp_tools.md](./unity_mcp_tools.md).
For the repo-navigation architecture reference behind the current documentation model, use [deep-research-report.md](./deep-research-report.md).
For a compact repo overview, use [repo_map.md](../maps/repo_map.md).
For a machine-readable top-level repo map, use [repo_map.json](../maps/repo_map.json).
For the agent header/comment convention, use [agent_comment_standard.md](./agent_comment_standard.md).
For non-runtime add-on material, use [supplements/README.md](../supplements/README.md).
For machine-readable supplement routing, use [supplements_index.json](../supplements/supplements_index.json).

## Code ID scheme

All project scripts now contain a file-level comment in this format:

- `// [CODE-ID: ...]`
- `// Logical block: ...`

These IDs are path-based and unique inside the project codebase. They are intended as stable anchors for discussion and future refactors.

Large/high-risk systems also contain shorter internal section IDs:

- `PENV-*` - `ProceduralEnvironment`
- `UCOM-*` - `UnitCombat`
- `UVEW-*` - `UnitView`
- `USPA-*` - `UnitSpriteAnimator`
- `FFLD-*` - `FlowFieldManager`
- `PMGR-*` - `PathManager`
- `ORCA-*` - `OrcaAvoidanceSystem`
- `USOA-*` - `UnitSoARegistry`
- `ESQD-*` - `EnemySquadManager`
- `HPFB-*` - `HexPathfindingBootstrap`
- `MJOB-*` - `MovementJobSystem`
- `LAVO-*` - `LocalAvoidanceSystem`
- `PQUE-*` - `PathRequestQueue`
- `CROWD-*` - `CrowdingResolver`
- `STUCK-*` - `StuckResolver`

Use these IDs when referencing a logical block instead of describing it indirectly.
During the ongoing decomposition of the world-generation stack:
- `PENV-03`..`PENV-05` live in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- `PENV-06`..`PENV-10` live in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-11`..`PENV-15` live in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-16`..`PENV-18` live in [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
- `PENV-19`..`PENV-21` live in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- `PENV-22`..`PENV-24` live in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
- `PENV-02` now lives in [ProceduralEnvironment.Lifecycle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs)
- `UCOM-03` now lives in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UCOM-04` and `UCOM-08` now live in [UnitCombat.FormationFlow.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)
- `UCOM-05`..`UCOM-07` now live in [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
- `UCOM-09` and `UCOM-10` now live in [UnitCombat.State.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.State.cs)
- `PMGR-03` now lives in [PathManager.Occupancy.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
- `PMGR-04` now lives in [PathManager.Reuse.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
- `PQUE-02` now lives in [PathRequestQueue.Dispatch.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
- `PQUE-03` now lives in [PathRequestQueue.Completion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)
- `FFLD-04` and `FFLD-05` now live in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FFLD-06` now lives in [FlowFieldManager.TileGraph.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
- `FFLD-07` now lives in [FlowFieldManager.FieldState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
- `ESQD-04` now lives in [EnemySquadManager.Membership.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs)
- `ESQD-05` now lives in [EnemySquadManager.Tactics.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs)
- `MJOB-05` now lives in [MovementJobSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs)
- `MJOB-04` now lives in [MovementJobSystem.Jobs.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs)
- `LAVO-03` now lives in [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs)
- `LAVO-04` now lives in [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs)
- `USPA-02` now lives in [UnitSpriteAnimator.Playback.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Playback.cs)
- `USPA-03` now lives in [UnitSpriteAnimator.Combat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Combat.cs)
- `CROWD-04` now lives in [CrowdingResolver.Search.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs)
- `CROWD-05` now lives in [CrowdingResolver.Throttle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs)
- `STUCK-03` now lives in [StuckResolver.Recovery.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs)
- `STUCK-04` now lives in [StuckResolver.State.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs)
- the remaining `PENV-*` sections still live in [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)

## Repository layout

- [Assets/Scripts/Domain](../My%20project/Assets/Scripts/Domain) - core data and rules (economy, research, unit stats).
- [Assets/Scripts/Application](../My%20project/Assets/Scripts/Application) - use cases that orchestrate domain actions.
- [Assets/Scripts/Infrastructure](../My%20project/Assets/Scripts/Infrastructure) - configs (ScriptableObjects) and persistence.
- [Assets/Scripts/Presentation](../My%20project/Assets/Scripts/Presentation) - Unity MonoBehaviours for input, UI, view, pathfinding, performance.
- [Assets/Tests](../My%20project/Assets/Tests) - EditMode/PlayMode tests and perf stress harness.
- [docs](./) - system notes and architecture references.
- [supplements](../supplements) - add-on design material, audio integration references, and source libraries.

## Layer map (Domain / Application / Infrastructure / Presentation)

Domain:
- Economy: `EconomyState`, `EconomyManager`, `ResourceType`, `ResourceAmount`.
- Build: `BuildingService`, `BuildResult`.
- Research: `ResearchStore`, `ResearchStatus`, `ResearchStartResult`.
- Units: `UnitStats` (data only; movement uses `MovementSettings`).

Application:
- `GameStateService`: holds `EconomyState`, `EconomyManager`, `ResearchStore`.
- Use cases: `StartNewGame`, `PlaceBuilding`, `StartResearch`, `CompleteResearch`, `SaveGame`, `LoadGame`.

Infrastructure:
- Configs: `GameConfig`, `BuildingConfig`, `ResearchConfig`, `UnitConfig`, `UnitCombatProfile`, `UnitBehaviorProfile`.
- Persistence: `SaveSystem` (JSON to `Application.persistentDataPath/save.json`).

Presentation:
- Bootstrap: `CompositionRoot`.
- Input: `InputController`, `UnitSpawnerCommander`.
- UI: `HudController`, `ActionsPanel`, `ResearchPanel`.
- UI: `HudController.Squads` holds squad summary aggregation and selection UI extracted from `HudController`.
- UI: `ActionsPanel.SelfTest` holds save/load self-test scaffolding extracted from `ActionsPanel`.
- View: `UnitView`, `UnitCombat`, `UnitHpOverlay`, `MovementSettings`.
- View: `UnitView.Movement` holds per-frame movement integration, steering blend, and facing logic extracted from `UnitView`.
- View: `UnitView.Rendering` holds Y-sorting bootstrap and selection gizmos extracted from `UnitView`.
- View: `UnitSpriteAnimator.Playback` holds per-frame directional playback and crouch/locomotion state changes extracted from `UnitSpriteAnimator`.
- View: `UnitSpriteAnimator.Combat` holds attack/death animation triggers and crouch requests extracted from `UnitSpriteAnimator`.
- View: `UnitCombat.UpdateLoop` holds the combat tick orchestration, engage/no-target branches, and stall recovery extracted from `UnitCombat`.
- View: `UnitCombat.Targeting` holds target arbitration, faction overrides, facing, crouch, and repath helper logic extracted from `UnitCombat`.
- View: `UnitCombat.FormationFlow` holds squad metadata, formation offset math, shared hex access, and flow-field steering extracted from `UnitCombat`.
- Performance: `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `UnitVisualCulling`, `MovementJobSystem`, `OrcaAvoidanceSystem`, `UnitSoARegistry`, `LocalAvoidanceSystem` (legacy), `StuckResolver`.
- Performance: `UnitCombatJobScheduler.Buffers` holds Native buffer growth, snapshot ingestion, and applyback extracted from `UnitCombatJobScheduler`.
- Performance: `UnitCombatJobScheduler.Job` holds the parallel nearest-enemy job extracted from `UnitCombatJobScheduler`.
- Performance: `OrcaAvoidanceSystem.Buffers` holds unit gathering, Native buffer growth, snapshot ingestion, and applyback extracted from `OrcaAvoidanceSystem`.
- Performance: `OrcaAvoidanceSystem.Job` holds the burst-compiled ORCA solver extracted from `OrcaAvoidanceSystem`.
- Performance: `UnitSoARegistry.Build` holds per-frame SoA snapshot projection extracted from `UnitSoARegistry`.
- Performance: `UnitSoARegistry.Buffers` holds Native buffer ownership and capacity helpers extracted from `UnitSoARegistry`.
- Performance: `LocalAvoidanceSystem.Buffers` holds unit gathering, Native buffer growth/fill, and steering applyback extracted from `LocalAvoidanceSystem`.
- Performance: `LocalAvoidanceSystem.Job` holds the legacy steering job and spatial-hash helpers extracted from `LocalAvoidanceSystem`.
- Performance: `JobPipelineCoordinator` (fixed update order for ORCA + Movement).
- Performance: `StuckResolver.Recovery` holds nudge/repath recovery actions extracted from `StuckResolver`.
- Performance: `StuckResolver.State` holds per-unit progress reset and stale-entry cleanup extracted from `StuckResolver`.
- Pathfinding: `PathManager`, `PathRequestQueue`, `HexPathfindingBootstrap`, `HexPathfinderJob`, `PathfindingBootstrap` (grid fallback), `FlowFieldManager`, `CrowdingResolver`, `ProceduralEnvironment`, `PathProfiler`, `PathDebugHUD`.
- Pathfinding: `PathManager.Occupancy` holds occupancy caches and occupied-path rejection extracted from `PathManager`.
- Pathfinding: `PathManager.Reuse` holds reuse, nearest-free lookup, and cluster helper logic extracted from `PathManager`.
- Pathfinding: `PathRequestQueue.Dispatch` holds queue draining, job scheduling, and immediate fallback dispatch extracted from `PathRequestQueue`.
- Pathfinding: `PathRequestQueue.Completion` holds job completion, occupancy snapshots, stats, and safe callback/log handling extracted from `PathRequestQueue`.
- Pathfinding: `FlowFieldManager.CostMaps` holds crowd and influence cost-map maintenance extracted from `FlowFieldManager`.
- Pathfinding: `FlowFieldManager.TileGraph` holds coarse tile-graph construction and expansion extracted from `FlowFieldManager`.
- Pathfinding: `FlowFieldManager.FieldState` holds per-target flow-field storage, LoS state, and next-cell sampling extracted from `FlowFieldManager`.
- Pathfinding: `CrowdingResolver.Search` holds free-cell search, reservation keys, and odd-r ring enumeration extracted from `CrowdingResolver`.
- Pathfinding: `CrowdingResolver.Throttle` holds adaptive-throttling, effective-work-budget, and diagnostic logging extracted from `CrowdingResolver`.
- Pathfinding: `ProceduralEnvironment.Streaming` holds streaming bootstrap, queueing, chunk bounds, and scheduler helpers extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.StreamChunks` holds per-chunk background synthesis, streamed prop/tree placement, and chunk cleanup extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.Lifecycle` holds MonoBehaviour lifecycle, async/sync generation entrypoints, prepare/bootstrap, and grid/tilemap setup extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.FarView` holds far-view bake scheduling, render-object maintenance, chunked bake flow, HUD, and debug helpers extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.BackgroundMasks` holds background-grid sizing, streamed water/rock mask generation, land-distance fields, and background-cell lookup helpers extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.Placement` holds biome-aware prop/tree placement, blocked-cell bookkeeping, and spatial placement helpers extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `ProceduralEnvironment.Palettes` holds palette resolution, biome-mask cleanup, and post-placement obstacle baking helpers extracted from the main `ProceduralEnvironment` file.
- Pathfinding: `StaticObstacleHash` (blocked-cell hash for fast static queries), `CoverSlotHash` (pre-baked cover slots).

## ProceduralEnvironment ground conversion preset (isometric -> square)

The current default preset is tuned for `Zombie Rural - HD Isometric Tileset` ground tiles (128x256). Defaults live in [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs) and are mirrored in [SampleScene.unity](../My%20project/Assets/Scenes/SampleScene.unity).

Key settings:
- Manual diamond cutout: `UseGroundTileManualDiamond=true`, `GroundTileDiamondNormalized=false`, `GroundTileDiamondYFromTop=true`.
- Diamond points (pixels): top `(63.5,175)`, right `(127,207.5)`, bottom `(63.5,240)`, left `(0,208.5)`.
- Edge cleanup: `GroundTileDiamondInsetPixels=3`, `GroundTileMaskOutsideDiamond=true`, `GroundTileEdgeDilatePixels=2`, `GroundTileEdgeTrimPixels=1`, `GroundTileEdgeBlackThreshold=0.09`, `GroundTileEdgeChromaThreshold=0.09`.
- Sampling: `GroundTileAlphaThreshold=0.2`, `GroundTileFilterMode=Point`, `UseGroundTileAutoCrop=false`.
- Packed sprites: if a sprite is atlas-rotated (`sprite.packed`), it is unrotated before the manual diamond cut.
- Background: `UseBackgroundTilemap=true`, `BackgroundCellOverlapPixels=0`.

If you switch to another tileset or sprite size, update the diamond points and (optionally) the inset/edge thresholds.

Palette filters and biomes (SampleScene defaults):
- `UseGroundSuffixFilter=false` (keep all orientation variants; when enabled it filters `GroundTiles` by suffix and can hide `_E/_S/_W` variants).
- `AutoSplitGroundByName=true` with `PropNameKeywords=flora` and `BlockingNameKeywords=tree, rock, boulder, stone, cliff, pine`.
- `UseWaterBiome=true` with `WaterTileNameKeywords=Ground A2_..A14_`, `RockTileNameKeywords=Ground E2_..E10_` (names must exist in the current `GroundTiles` set).
- `WaterInteriorTileNameKeywords` controls which tiles are allowed in fully-surrounded water; `UseWaterAutoInteriorByColor` can auto-detect interior water tiles by blue-dominant edges and a clean interior region (defaults: `WaterInteriorBlueRatio=0.9`, `WaterInteriorSampleInsetPixels=4`, `WaterInteriorFallbackCount=1`, `WaterEdgeBlueRatio=0.8`, `WaterEdgeBlueDominance=0.08`, `WaterEdgeBlueMin=0.2`, `WaterEdgeSampleInsetPixels=1`, `WaterEdgeSampleBandPixels=3`, `WaterEdgeMismatchTolerance=0.1`, `WaterEdgeMaskSamples=8`, `WaterEdgeMaskRatioThreshold=0.45`, `WaterEdgeMaskMatchWeight=0.8`, `WaterEdgeSmoothnessWeight=0.6`, `WaterTileExcludeKeywords=Ground A3_, Ground A11_, Ground A12_`).
- When `UseWaterBiome=true`, water/rock tiles are removed from normal land selection; if the water mask doesn't build, you'll see only land tiles.
- Water generation removes isolated single water cells (4-neighbor check) and converts land “holes” fully surrounded by water to water.
- Water edge matching: `UseWaterEdgeColorMatch=true` enforces blue-dominant edges for any tile adjacent to water (water/rock/land), requiring water edges where the mask neighbor is water and non-water edges elsewhere; fallback uses water edge ratios and edge masks when strict matches fail.
- Water edge refinement: `UseWaterEdgeRefinement=true` runs a post-pass over the background to re-pick variants near water using full 4-neighbor edge masks (`WaterEdgeRefinePasses=1`, `WaterEdgeSmoothnessWeight=0.6`).
- Water mask smoothing: `UseWaterMaskSmoothing=true` applies cellular smoothing over the water mask (`WaterMaskSmoothPasses=2`, `WaterMaskSmoothFillNeighbors=5`, `WaterMaskSmoothStayNeighbors=4`, `WaterMaskSmoothIncludeDiagonals=true`) to reduce jagged shorelines.
- Layer smoothing: `UseLayerSmoothing=true` applies majority smoothing over biome indices (`LayerSmoothingPasses=1`, `LayerSmoothingMajority=0.55`, `LayerSmoothingIncludeDiagonals=false`) to reduce speckle noise globally.
- Layer cleanup: `UseLayerRegionCleanup=true` merges tiny biome islands into neighboring majority regions (`LayerMinRegionSize=20`, `LayerCleanupPasses=1`, `LayerCleanupIncludeDiagonals=false`).
- Noise warp: `UseNoiseDomainWarp=true` distorts the biome noise field to break grid-like patterns (`DomainWarpScale=0.02`, `DomainWarpStrength=0.6`, `DomainWarpOctaves=2`, `DomainWarpPersistence=0.5`, `DomainWarpLacunarity=2`).
- Layer quantization: `UseLayerQuantization=true` snaps noise into clearer biome bands with small jitter (`LayerQuantizationJitter=0.12`).
- Macro biomes: `UseMacroBiomeNoise=true` blends in a very low-frequency noise to produce large contiguous regions (`MacroBiomeScale=0.004`, `MacroBiomeBlend=0.85`, `MacroBiomeContrast=1.2`, `MacroBiomeOctaves=1`).

## ProceduralEnvironment: world streaming + far view

Streaming (for very large maps):
- `UseWorldStreaming=true` enables chunked generation around the camera.
- Chunk controls: `StreamChunkSize`, `StreamActiveRadius`, `StreamPrefetchRadius`, `StreamUnloadRadius`, `StreamMaxLoadedChunks`.
- Performance controls: `StreamChunksPerFrame`, `StreamFrameBudgetMs`, `StreamTargetFps`, `StreamSkipIfOverBudget`.
- Cache policy: `StreamKeepGeneratedChunks` keeps chunks forever (no re-bake/unload); `StreamBakeAllChunksOnIdle` fills the map when camera is idle (`StreamIdleSeconds`, `StreamIdleMoveEpsilon`).
- Water/rock biomes in streaming use the same **water mask + rock mask** as non-streaming generation (no simple noise fallback).
- Edge matching in streaming respects `UseWaterEdgeColorMatch` and related mask/ratio settings.
- Props/trees placement grid: `StreamPropsUseBackgroundGrid` (default `false`).
  - `false`: props/trees are placed in the hex grid (most consistent with unit positions).
  - `true`: props/trees placed in the background rect grid (use only if you want them locked to the square background).

Far view bake:
- `UseFarViewBake=true` renders the map to a cached texture for very far zooms.
- Chunked bake: `UseFarViewChunkedBake=true`, `FarViewChunksPerFrame`, `FarViewChunkPixels`, `FarViewMaxTextureSize`.
- Readback control: `UseFarViewAsyncReadback`, `FarViewReadbacksPerFrame`, `FarViewMaxPendingReadbacks`, `FarViewReadbackTimeout`.
- UI: `ShowFarViewBakeHUD`, `FarViewHideMapWhileBaking`.
- Important: Far view is **disabled while streaming is active** (streaming takes priority).

## Bootstrap and singletons

- [CompositionRoot.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs):
  - Owns the root lifecycle (`CROOT-01`), inspector references, and economy tick.
- [CompositionRoot.Setup.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs):
  - Creates `GameStateService`, auto-starts a new game, and wires scene singletons (`CROOT-02`).
  - Ensures `CameraZoom2D`, `HexPathfindingBootstrap`, `ProceduralObstacles`, `ProceduralEnvironment`, `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `StaticObstacleHash`, `CoverSlotHash`, `PathRequestQueue`, `FlowFieldManager`, `MovementJobSystem`, `OrcaAvoidanceSystem`, `StuckResolver`.
  - Disables `LocalAvoidanceSystem` when ORCA is enabled.
  - Applies `UnitVisualCulling` and sorting layer/order to existing units.
- [CompositionRoot.Actions.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs):
  - `Save()` and `Load()` wrap `SaveGame`/`LoadGame` use cases.
  - Exposes test build/research actions and `LastStatusMessage` (`CROOT-03`).
- [CompositionRoot.Persistence.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs):
  - Captures unit snapshots and restores faction visuals, overlays, culling, and sorting on load (`CROOT-04`).

- [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs) `Ensure()` and [PathRequestQueue.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs) `Ensure()` create global instances if missing.

## Core runtime flows (step-by-step)

### Spawn player unit (LMB)
- `UnitSpawnerCommander.Update`:
  - LMB -> `SnapToHex` -> `Instantiate(UnitPrefab)` -> add `UnitCombat`, `UnitHpOverlay`, `UnitVisualCulling`.
  - Sets `Faction.Player`, colors and sprite from `CompositionRoot` if assigned.
  - Saves as `lastUnit` for RMB commands.

### Spawn enemy at cursor (E hotkey)
- `InputController.TrySpawnEnemyAtCursor`:
  - Picks world point under cursor, snaps to hex.
  - Instantiates prefab, adds/gets `UnitCombat`, sets `Faction.Enemy`.
  - Applies red tint and enemy sprite.

### Command movement (RMB)
- `UnitSpawnerCommander`:
  - RMB is coalesced by `RmbCoalesceSeconds`, then `TrySetPath`.
  - `PathRequestQueue.Enqueue` runs job or sync build.
  - On success -> `UnitPathFollower.SetWorldPath`, then `UnitCombat.NotifyManualMove`.
  - On failure -> if target is walkable and not occupied, set a direct `UnitView.SetDestination`.

### Combat tick and targeting
- [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs) `UnitCombat.Update` (gated by `CombatTickInterval + CombatTickJitter`):
  - Optional `UnitCombatProfile` applies data-driven settings on enable.
  - Optional `UnitBehaviorProfile` applies hold/aggro/leash rules and target preference.
  - Clears expired forced targets and job targets.
  - `ResolveTarget`:
    - Uses job target (`UnitCombatJobScheduler`) if available.
    - Falls back to `OccupancyHash` when no job target is available.
    - Forced squad target is overridden if a local target is within `AttackRange * LocalThreatOverrideMultiplier`.
  - If target is outside `AttackRange`, computes desired position, snaps to hex center if moving into a new cell:
    - Non-squad units: may request a path via `PathRequestQueue`.
    - Squad units (including `FreeCombat`): use flow fields or direct destination steering (no per-unit path builds).
    - Optional formation offsets near target for squad units.
  - If in range, cancels combat path and attacks on cooldown.
  - Flow fields can be used for far-distance chasing (`UseFlowFields`) to avoid frequent path builds.

### Path request pipeline
- `PathRequestQueue.Update`:
  - Schedules at most `MaxPerFrame` requests; uses jobs when `UseJobs=true`.
  - Uses a per-frame occupancy snapshot by faction for job scheduling.
  - Converts job path to world points, skipping the start cell to avoid snapping back.
  - Falls back to `PathManager.BuildPath` if the job yields no path.

### Save / Load
- `SaveSystem.SaveDefault`:
  - Saves stocks, unit snapshots (position, dest, faction, hp), research, and blocked cells.
- `SaveSystem.LoadDefault`:
  - Restores stocks, units, research, and obstacles.
  - `CompositionRoot.RestoreUnitsEx` re-instantiates units with correct faction, sprites, HP, and destination.

### Self-test (ActionsPanel)
- `ActionsPanel.SelfTestRoutine`:
  - Disables combat, spawns a deterministic set of units, optionally starts research.
  - Save -> Load -> validates position, destination, faction, HP, and overlays.

## Runtime flow diagrams (ASCII)

These are compact flow sketches to make it easy to follow runtime execution without opening the code.

### Spawn unit (LMB)
```
InputController (LMB) -> UnitSpawnerCommander.Update
  -> SnapToHex
  -> Instantiate(UnitPrefab)
  -> Add/Get UnitCombat + UnitHpOverlay + UnitVisualCulling
  -> Apply faction visuals + sorting
  -> lastUnit = spawned unit
```

### Spawn enemy (hotkey E)
```
InputController.Update (E) -> TrySpawnEnemyAtCursor
  -> ScreenToWorld -> SnapToHex
  -> Instantiate(prefab)
  -> Add/Get UnitCombat (Faction.Enemy)
  -> Apply enemy visuals + sorting
```

### Manual move (RMB)
```
UnitSpawnerCommander.Update (RMB)
  -> EnqueueRmb -> ApplyRmbAfterCoalesce
    -> TrySetPath
      -> PathRequestQueue.Enqueue
        -> (job or sync) BuildPath
        -> onDone:
           - if ok: UnitPathFollower.SetWorldPath
           - else: UnitView.SetDestination (if walkable)
        -> UnitCombat.NotifyManualMove
```

### Combat tick (per UnitCombat)
```
UnitCombat.Update in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs) (combat tick)
  -> ResolveTarget
     -> job target (UnitCombatJobScheduler)
     -> OccupancyHash (fallback if no job target)
     -> forced squad target (unless local threat overrides)
  -> If target:
     - out of range -> compute desired
         - FreeCombat/None -> path request
         - Squad modes -> flow field or direct destination
     - in range -> cancel combat path + attack
  -> If no target -> cancel combat steering
```

### Jobified targeting (UnitCombatJobScheduler)
```
UnitCombatJobScheduler.Update
  -> GatherUnits (non-squad units only)
  -> FillArrays + Build hash buckets
  -> Schedule NearestEnemyJob (frame N)
  -> ApplyResults on next update when the job completes
```

### Path request pipeline (jobs)
```
PathRequestQueue.Update
  -> Dequeue request
  -> TryScheduleJob
     -> Get walkable native map from HexPathfindingBootstrap
     -> Use per-frame occupancy snapshot by faction
     -> Schedule HexPathfinderJob
  -> FinishJob
     -> Convert cells to world points (skip start cell)
     -> Fallback to PathManager.BuildPath if needed
     -> Callback with world path
```

### Movement update (jobs + ORCA)
```
OrcaAvoidanceSystem.Update
  -> Build spatial hash + ORCA constraints (jobs)
  -> Output velocity overrides
MovementJobSystem.Update
  -> Apply ORCA velocity overrides (accel/decel-limited)
  -> Fallback to steering or direct-to-destination
  -> Apply facing
```

### Squad control (group-centric combat)
```
EnemySquadManager.Update
  -> Build/refresh squads (size up to 12)
  -> Grow gather radius until filled or max, then sleep/retry
  -> Compute squad-to-squad distance in hexes
  -> State machine: Gathering/Marching/Ready/FreeCombat
  -> Assign forced targets (TTL) until FreeCombat release distance
```

### Save / Load
```
HudController -> Save button
  -> CompositionRoot.Save -> SaveGame -> SaveSystem.SaveDefault
    -> Capture units + research + obstacles -> save.json

HudController -> Load button
  -> CompositionRoot.Load -> LoadGame -> SaveSystem.LoadDefault
    -> Restore stocks + units + research + obstacles
```

## Pathfinding and navigation details

- `HexPathfindingBootstrap`:
  - Odd-r offset grid; pointy-top hex math.
  - [HexPathfindingBootstrap.Walkability.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs) owns walkability mutation, physics rebake, persistence, and Native mirror updates.
  - [HexPathfindingBootstrap.Geometry.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs) owns world/grid conversion, gizmos, and geometry snapshots.
  - `BakeFromPhysics` uses `Physics2D.OverlapCircle` with `ObstacleMask` or "Obstacles" layer.
  - Maintains `NativeArray<byte>` walkable map for jobs.
  - Calls `PathRequestQueue.CompleteActiveJobAndClear` before rebuilding or disposing native arrays.
  - Supports partial rebakes via `BakeFromPhysicsRect` / `BakeFromPhysicsRectCells`.

- `PathManager.BuildPath`:
  - Chooses hex bootstrap if available, else grid fallback.
  - Occupancy: blocks enemies and optionally recent friendly cells (`FriendlyReserveSeconds`).
  - Uses `StaticObstacleHash` (static blocks) + `OccupancyHash` (dynamic units) when enabled.
  - Per-frame occupied caches are reused across sync calls (including recent-friendly TTL by faction).
  - `EnableGroupPathReuse` caches paths by target cell for nearby allies.
  - Converts grid path to world points; skips start cell and smooths straight segments.

- `UnitPathFollower`:
  - Maintains a queue of world points and advances when within `WaypointEpsilon`.
  - Simplifies straight runs (`StraightDotThreshold`, `MinStraightRun`).
  - `Source` = `Manual` or `Combat` so combat logic avoids clobbering manual paths.
- `FlowFieldManager`:
  - Time-sliced flow fields (BFS) on hex grid with TTL/LRU eviction.
  - Field expansion is capped by farthest requesting unit (distance limit + padding).
  - Optional tiled mode restricts expansion to a coarse tile path (`TileSize` + `TilePadding`).
  - Optional crowd cost mode biases next-step selection away from dense clusters.
  - Optional deterministic direction bias keeps flow steps aligned with the target direction.
  - Optional vector sampling blends downhill neighbors to smooth movement.
  - Optional influence costs use per-faction threat maps (enemy units) to steer around danger.
  - Optional LoS flags cache per-cell visibility to the target to reduce repeated line checks.
- `CoverSlotHash`:
  - Pre-bakes cover slots around blocked cells and stores them in a spatial hash for fast lookup.
- `ProceduralEnvironment`:
  - Builds tilemap ground/prop layers from `TileBase` palettes.
  - Supports blocking props that can be baked into walkability when enabled.
  - Can auto-split blocking props by tile name keywords when enabled.
  - Supports async, chunked generation to avoid editor freezes on large maps.
  - Can update walkability directly for blocking tiles to avoid physics rebake.

## Performance helpers

- `UnitCombatJobScheduler`:
  - [UnitCombatJobScheduler.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs) owns Native buffer growth, snapshot fill, hash-bucket construction, and applyback to `UnitCombat`.
  - [UnitCombatJobScheduler.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs) owns the burst-compiled nearest-enemy search job.
  - Collects unit positions/factions into `NativeArray`.
  - Builds a spatial hash with `NativeParallelMultiHashMap`.
  - Schedules `NearestEnemyJob` and applies results on the next update tick.
  - Skips squad-controlled units (squad targeting handled elsewhere).
  - Can reuse `UnitSoARegistry` snapshots when enabled to reduce per-unit transform reads.

- `EnemySquadManager`:
  - Forms squads for both factions (default size 12), with dynamic gather radius.
  - [EnemySquadManager.Membership.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs) owns squad composition, recruitment, and center updates.
  - [EnemySquadManager.Tactics.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs) owns mode hysteresis, target assignment, and flow-anchor selection.
  - Uses squad-to-squad distance (hexes) to drive states with hysteresis.
  - Assigns forced targets via TTL; releases targets in `FreeCombat` only when close enough.
  - Assigns per-unit formation indices for arrival offsets when enabled.
  - Computes a flow-based squad move anchor; non-free-combat units follow formation slots around it.

- `OccupancyHash`:
  - Rebuilt every frame; used for quick occupancy checks and nearest enemy lookup when no job target is available.
- `MovementJobSystem`:
  - Jobified movement update for `UnitView` (enabled by default).
  - ORCA overrides can be reused for a short frame window to decouple job timing.
- `LocalAvoidanceSystem`:
  - Legacy lightweight steering avoidance fallback built on a spatial hash and simple neighbor repulsion.
  - [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs) owns unit gathering, Native buffer growth/fill, and steering applyback.
  - [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs) owns the steering job plus cell/hash helpers.
  - CompositionRoot keeps it alive for fallback coverage, but disables it whenever ORCA is active.
- `JobPipelineCoordinator`:
  - Drives ORCA + Movement in a fixed order and disables their internal Update loops when enabled.
- `UnitSoARegistry`:
  - Builds a centralized SoA snapshot for ORCA inputs to reduce redundant per-unit collection.
  - [UnitSoARegistry.Build.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs) owns per-frame projection from active units into ORCA/combat snapshot arrays.
  - [UnitSoARegistry.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs) owns NativeArray capacity growth, disposal, and shared cell projection helpers.
  - Also exposes combat snapshots for targeting systems when enabled.
- `OrcaAvoidanceSystem`:
  - ORCA/RVO avoidance with spatial hash; feeds velocity overrides into movement jobs.
  - [OrcaAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs) owns unit gathering, Native buffer growth, snapshot ingestion, and applyback to `UnitView`.
  - [OrcaAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs) owns the burst-compiled ORCA solver and LP helper routines.
  - Optional cohesion bias toward friendly centroid.
  - Respects `UnitView.UseOrcaVelocity` (units can opt out of overrides but remain obstacles).
  - Per-unit priority (`UnitView.OrcaPriority`) reduces avoidance responsibility (see `MinResponsibility`).
- `CrowdingResolver`:
  - LateUpdate stack resolver for non-squad, non-flow-field units; no-ops while ORCA or legacy local avoidance is active.
  - [CrowdingResolver.Search.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs) owns free-cell search, reservation keys, and odd-r ring enumeration.
  - [CrowdingResolver.Throttle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs) owns adaptive throttling, effective search/group budgets, and diagnostic logging.
- `StuckResolver`:
  - Detects stuck movers and nudges them; can force combat repath.
  - [StuckResolver.Recovery.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs) owns optional combat repath and nearest-free nudge recovery.
  - [StuckResolver.State.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs) owns per-unit progress reset and stale-state cleanup.
  - Skips squad-controlled units outside `FreeCombat` and units currently following flow fields.

- `UnitVisualCulling`:
  - Disables `SpriteRenderer`, `Animator`, `UnitHpOverlay` when far from camera or outside frustum.
- `StaticObstacleHash` / `CoverSlotHash`:
  - Have guards for huge grids/streaming. When map size is too large or streaming is enabled, they skip heavy rebuilds to avoid RAM spikes and frame stalls.

## Diagnostics and toggles

- `PathProfiler`:
  - Tracks builds, rejects, max nodes, path resets, commands, jitter, crowd moves.
  - Optional anomaly log when thresholds are exceeded.
- `PathDebugHUD`:
  - On-screen snapshot of `PathProfiler` stats.
- `UnitCombat.LogCombatResets` and `UnitView.EnableJitterLog`:
  - Opt-in diagnostic logs for combat resets and destination jitter.
- `UnitCombat.DisableCombat`:
  - Global switch to freeze combat logic (used in tests and self-test).

## Tests

- PlayMode:
  - `SampleSceneBootSmokeTests` loads `SampleScene`, checks required bootstrap/HUD/camera/pathfinding/environment/path-queue systems, captures startup errors, and unloads into a cleanup scene.
  - `SampleSceneRendererDiagnosticsTests` is an explicit diagnostic-only `SampleScene` probe that logs `[SampleSceneRendererProbe]` streaming, renderer, frame, and memory baseline metrics; the companion extractor can apply the provisional owner budget outside the Unity test.
  - `FpsStressTests` logs `[FpsStress]` average FPS for staged unit counts and `[CombatPressureProbe]` for the provisional 100v100 owner target with path and combat activity; the companion extractor can apply the provisional owner budget outside the Unity test.
  - `CombatPathResetTests` checks path reset rates during chase.
  - `UnitCombatStallTests` checks in-range attacks and chase behavior.
- EditMode:
  - `UnitCombatTargetingTests` validates target resolution priority.

## Not currently wired

- `UnitConfig` exists but is not referenced by runtime code.
- `UnitStats.Speed` is not used by `UnitView` (movement uses `MovementSettings`).
