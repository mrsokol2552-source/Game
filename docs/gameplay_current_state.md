# Current Game State (RTS 2D)

## Index / Search Hints
- Bootstrap & lifecycle: CompositionRoot, GameStateService, SaveSystem, camera setup.
- Domain & use cases: EconomyState/Manager, ResearchStore, StartNewGame, PlaceBuilding, StartResearch, CompleteResearch.
- Configs: GameConfig, BuildingConfig, ResearchConfig, UnitConfig, MovementSettings.
- Input & UI: InputController, UnitSpawnerCommander, HudController, ActionsPanel, ResearchPanel, camera controls.
- Units & movement: UnitView, UnitStats, UnitHpOverlay, UnitVisualCulling, UnitPathFollower.
- Pathfinding & navigation: PathManager, PathRequestQueue, HexPathfindingBootstrap, HexPathfinderJob, CrowdingResolver, ProceduralObstacles.
- Combat & targeting: UnitCombat (steering, repath timers, budgets), UnitCombatJobScheduler, EnemySquadManager, OccupancyHash.
- Persistence: SaveSystem bindings, what is persisted.
- Debug/perf: PathProfiler, PathDebugHUD, diagnostic toggles.
- Tests: Assets/Tests (EditMode + PlayMode).

## Layers and code map
- This doc describes runtime behavior; for file navigation see `docs/code_map.md`.
- Domain: pure data + rules (`Domain/*`).
- Application: use cases that orchestrate domain changes (`Application/*`).
- Infrastructure: ScriptableObjects and persistence (`Infrastructure/*`).
- Presentation: Unity MonoBehaviours (`Presentation/*`).

## Bootstrap and Lifecycle
- `CompositionRoot` (`Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs`)
  - Creates `GameStateService` and binds SaveSystem capture/restore callbacks.
  - Auto-starts a new game when `AutoStart=true`, using `GameConfig.StartingResources` or zeroing stocks.
  - Ensures helpers: `CameraZoom2D`, `HexPathfindingBootstrap` ("HexPathfinding (Auto)"), `ProceduralObstacles`, `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `StaticObstacleHash`, `CoverSlotHash`, `PathRequestQueue`, `FlowFieldManager`, `MovementJobSystem`, `OrcaAvoidanceSystem`, `StuckResolver`.
  - Disables `LocalAvoidanceSystem` when ORCA is enabled (legacy steering).
  - Applies `UnitVisualCulling` and sorting layer/order to existing `UnitView` objects.
  - Ticks `EconomyManager` every frame (currently no passive income).
  - Exposes UI helpers: `AttemptPlaceTestBuilding`, `AttemptStartTestResearch`, `AttemptCompleteTestResearch`, status in `LastStatusMessage`.

## Domain and Application
- `GameStateService`:
  - Holds `EconomyState`, `EconomyManager`, `ResearchStore`.
- Use cases:
  - `StartNewGame` resets stocks and optionally applies `GameConfig.StartingResources`.
  - `PlaceBuilding` delegates to `BuildingService.TryPlace`.
  - `StartResearch` charges cost via `BuildingService` and sets status to `Queued`.
  - `CompleteResearch` promotes `Queued` to `Done`.
  - `SaveGame` and `LoadGame` wrap `SaveSystem`.

## Configs and ScriptableObjects
- `GameConfig`: `StartingResources`.
- `BuildingConfig`: `Id`, `Cost`.
- `ResearchConfig`: `ResearchDef` entries (`Id`, `Cost`).
- `UnitConfig`: `Id`, `Speed`, `MaxHealth`, `Cost` (not wired to runtime yet).
- `UnitBehaviorProfile`: hold/aggro/leash rules and forced-target preference.
- `MovementSettings`: movement tuning for `UnitView` (`MaxSpeed`, accel/decel, slowdown, stop distance, rotate to velocity).

## Input, Camera, and UI
- Camera (`CameraZoom2D`):
  - Zoom: mouse wheel or `+/-`, clamped `MinOrthoSize..MaxOrthoSize`, smooth lerp.
  - Pan: MMB drag or WASD; speed scales with ortho size (`PanZoomScale`).
  - Blocks zoom/drag when pointer is over HUD; WASD still works.
- Hotkeys (`InputController`):
  - `M` +10 Materials, `F` +5 Food, `B` attempt test build, `R` start first research, `C` complete first research, `E` spawn enemy at cursor (snapped to hex).
  - Uses Input System if enabled, falls back to legacy input otherwise.
- Spawning and commands (`UnitSpawnerCommander`):
  - LMB: spawn player unit at nearest hex center using `UnitPrefab` or `CompositionRoot.DefaultUnitPrefab`.
  - RMB: coalesced by `RmbCoalesceSeconds`, queued via `PathRequestQueue`.
  - On spawn: ensures `UnitCombat`, `UnitHpOverlay`, `UnitVisualCulling`; applies faction tint/sprite and sorting.
  - On manual move: `UnitCombat.NotifyManualMove` clears combat steering so manual commands persist.
- HUD (`HudController`):
  - Shows unit count, resource stocks, Save/Load buttons, toggle Research panel, toggle Dev panel.
  - Keeps a list of UI rectangles for pointer blocking (`IsPointerOverHud`).
- Dev panel (`ActionsPanel`):
  - Spawn units, add resources, attempt build, toggle research panel, clear save file.
  - Save/Load self-test: spawn deterministic units, save/load, validate data.
- Research panel (`ResearchPanel`):
  - Renders `ResearchConfig` items from `CompositionRoot.TestResearch`.
  - Buttons call `StartResearch` / `CompleteResearch` use cases.

## Units, Health, and Visuals
- `UnitStats`: `MaxHealth`, `Speed` (movement uses `MovementSettings`, not `UnitStats.Speed`).
- `UnitView`:
  - Uses `MovementSettings.Default` if no asset assigned.
  - `SetDestination` increments `PathProfiler.CountCommand`; optional jitter logs when `LogJitteryCommands` and `EnableJitterLog` are true.
  - `ClearDestination` increments `PathProfiler.CountPathReset`.
  - Movement: smooth accel/decel, snap within `StopDistance`, optional rotate-to-velocity, sprite mirror on X.
  - Job movement: skips `Update` when `MovementJobSystem` is active; supports ORCA velocity override and steering input.
  - Exposes last direction, speed, and silent destination clear for job systems.
- `UnitHpOverlay`:
  - OnGUI health bar above units; draws only if HP < max.
- `UnitVisualCulling`:
  - Every `CheckInterval`, toggles `SpriteRenderer`, `Animator`, `UnitHpOverlay` when far/out of frustum.
  - Logic continues to run; only visuals are disabled.

## Movement, Path Following, and Crowding
- `UnitPathFollower`:
  - Maintains a queue of waypoints and advances when within `WaypointEpsilon`.
  - Simplifies straight segments (`StraightDotThreshold`, `MinStraightRun`).
  - `Source` flag (`Manual`, `Combat`) to prevent combat from overriding manual paths.
  - `Cancel` clears points/destination and counts a reset if there was a path but no destination.
- `MovementJobSystem`:
  - Jobified movement update for all `UnitView` with `UseMovementJobs=true`.
  - Applies ORCA velocity overrides, then falls back to steering or direct-to-destination motion.
  - Updates facing based on the resulting direction.
  - ORCA overrides can be reused for a short frame window to decouple job timing.
- `JobPipelineCoordinator`:
  - Drives ORCA + Movement in a fixed order and disables their internal Update loops when enabled.
- `UnitSoARegistry`:
  - Builds a centralized SoA snapshot for ORCA inputs and is driven by the job coordinator.
  - Also exposes combat snapshots for targeting systems when enabled.
- `CrowdingResolver`:
  - Runs in `LateUpdate` every `Interval`.
  - Groups units by hex cell; nudges units beyond `AllowStayCountPerCell`.
  - Uses adaptive throttling (`FrameTimeSoftLimit/HardLimit`) and population scaling.
  - Skips squad-controlled units and units currently following flow fields; no-ops while ORCA is active or legacy local avoidance is enabled.
  - Skips work when path builder budget is exhausted and uses `MoveCooldown` to prevent ping-pong.
  - Can resolve stacks for a limited window after enemies disappear (`ResolveWithoutEnemies`).
- `StuckResolver`:
  - Tracks movement progress over a time window; if moving but not progressing, nudges to nearby free hex.
  - Can force a combat repath on stuck units; respects a per-unit cooldown.
  - Ignores squad-controlled units outside `FreeCombat` and units currently following flow fields.

## Pathfinding and Navigation
- `PathManager` (singleton):
  - Prefers `HexPathfindingBootstrap`, falls back to `PathfindingBootstrap`.
  - Budget: `MaxBuildsPerFrame` (0 = unlimited), `MaxPathNodes`.
  - Occupancy: enemies always block; friendlies are reserved for `FriendlyReserveSeconds`.
  - Uses `StaticObstacleHash` (blocked cells) plus `OccupancyHash` (dynamic units) for fast occupied checks.
  - Per-frame occupied caches are reused across synchronous `BuildPath` calls (including recent-friendly TTL by faction).
  - Optional `EnableGroupPathReuse` with `GroupReuseMaxStartDist2` and `GroupReuseFrames`.
  - Uses pools for grid points, world points, and hash sets.
- `PathRequestQueue`:
  - Processes up to `MaxPerFrame` requests; `MaxQueueSize` drops oldest.
  - Jobs: `UseJobs=true` schedules `HexPathfinderJob` when native walkable data exists.
  - Uses per-frame occupancy snapshots by faction for job scheduling and caches the hex bootstrap reference.
  - Fallback to sync `PathManager.BuildPath` when job fails or is disabled.
  - `ProcessSynchronouslyIfIdle` can handle requests immediately when jobs are off.
- `HexPathfindingBootstrap`:
  - Odd-r hex grid, pointy-top. Defaults: `Width/Height=1024`, `HexSize=0.4`.
  - `AutoClampSize` limits grid if `Width*Height` exceeds `MaxCells`.
  - `BakeFromPhysics` uses `ObstacleMask` (or "Obstacles" layer) and `SampleRadius`.
  - `BakeFromPhysicsRect` / `BakeFromPhysicsRectCells` update only a grid region and patch native walkable data for that rect.
  - Maintains `NativeArray<byte>` walkable map; `UpdateNativeWalkable` completes jobs before rebuild.
  - `CaptureBlocked`/`RestoreBlocked` for persistence.
- `HexPathfinderJob`:
  - A* on hex grid using native walkable map and enemy occupancy hash.
  - Returns `int2` cell path; `PathRequestQueue` converts to world points and skips the start cell.
- `FlowFieldManager`:
  - Time-sliced BFS on hex grid with per-target caching.
  - Fields expand only to the farthest requesting unit (distance limit + padding), not the full map.
  - Quantizes target cells to reduce field count; evicts fields by TTL/LRU.
  - Optional line-of-sight smoothing to skip zigzags on clear paths (`UseLoSSmoothing`, `LoSMaxRange`, `LoSMinImprovement`).
  - Tiled mode (`UseTiledFields`) builds a coarse tile graph (`TileSize`) and limits expansion to tiles along the coarse path plus `TilePadding`.
  - If a tile path cannot be found, it falls back to ungated expansion for that field.
  - Crowd cost mode (`UseCrowdCosts`) builds a per-hex occupancy map each frame and biases flow steps away from dense clusters.
  - Deterministic flow selection (`UseDeterministicDirections`) biases neighbor order toward the target direction for more stable results.
  - Vector sampling (`UseVectorSampling`) blends downhill neighbors and steps a fraction of a hex for smoother motion.
  - Influence cost mode (`UseInfluenceCosts`) builds per-faction threat maps from enemy units and biases flow steps away from danger.
  - LoS flags cache per-cell visibility to the target and reduce repeated line checks during smoothing.
- `PathfindingBootstrap` (square grid fallback):
  - Uses `CellSize`, `AllowDiagonals`, `AutoFitToCamera`. `SmoothWorldPath` is present but not wired.
- `StaticObstacleHash`:
  - Caches blocked hex cells into a static hash and rebuilds on walkable version changes.
- `CoverSlotHash`:
  - Pre-bakes cover slots around blocked cells and stores them in a spatial hash for fast lookup.
- `HexPathfinderJobBurst.md` documents job usage and expectations.

## Combat and Targeting
- `UnitCombat`:
  - Static `UnitCombat.All` holds all active combat units.
  - Global switch `DisableCombat` freezes combat logic (used by tests/self-test).
  - Supports `UnitCombatProfile` to apply data-driven combat settings at spawn time.
  - Supports `UnitBehaviorProfile` to apply hold/aggro/leash rules and forced-target preference.
  - Squad membership: units carry `SquadId` and `SquadMode` (`None`, `Gathering`, `Marching`, `Ready`, `FreeCombat`, `Sleeping`).
  - Squad gating: individual combat path builds are allowed only for non-squad units; squads use flow fields or direct destination steering.
  - Squad units now skip per-unit combat path requests entirely; flow fields and direct steering handle movement.
  - Timers: `CombatTickInterval` + `CombatTickJitter`, `TargetRefreshInterval`, `JobTargetTtl`, `Repath*` timers.
  - `LostTargetGraceSeconds` keeps combat steering briefly after losing a target; cancels early if moving away from the last target position.
  - Budgets: `RepathBudgetPerFrame` enforced; `TargetSearchBudgetPerFrame` declared but not currently enforced.
  - Targeting pipeline:
    - Uses job scheduler target (`SetJobNearest`) when available.
    - Falls back to `OccupancyHash` when no job target is available (even if the scheduler is enabled).
    - Forced squad target (`AssignSquadTarget`) is overridden if a local target is within `AttackRange * LocalThreatOverrideMultiplier`.
    - No O(n^2) fallback; if nothing resolved, the unit idles until next refresh.
  - Enemy presence check uses cached faction counts to avoid scanning all units each tick.
  - Movement steering:
    - Desired point at stop distance; optional perpendicular jitter to avoid stacking.
    - Optional formation offsets near target for squad units (per-unit slot index).
    - Snaps to hex center only when moving into a different cell.
    - Cluster stepping (`UseClusterStepping`) uses `PathManager.TryGetClusterEdgeTarget`.
    - Flow fields (`UseFlowFields`) can advance toward target using `FlowFieldManager` when far away; can be forced when squad mode disallows individual paths.
    - ORCA velocity override can be disabled near attack range (`DisableOrcaWhenInRange`).
  - Requests paths via `PathRequestQueue`; stale callbacks are ignored via request id.
  - In-range behavior:
    - Cancels combat path, clears destination, attacks on cooldown.
  - No-enemy behavior:
    - Cancels combat-driven movement and pending paths; leaves manual paths intact.
  - Diagnostics:
    - `LogCombatResets` logs destination/path resets with per-frame throttle.
  - Notes:
    - `EngageStopMultiplier` is exposed but not referenced by current logic.

## Enemies, Squads, and Background Jobs
- `EnemySquadManager`:
  - Forms squads up to `MaxSquadSize` for both factions (`DrivePlayers=true`).
  - Dynamic gather radius grows by step every `GatherRadiusStepSeconds` until full or `MaxGatherRadiusHex`.
  - If still underfilled at max radius, squad sleeps and retries recruitment every `SleepRetrySeconds`.
  - Uses squad-to-squad distance in hexes to drive states (`Gathering`, `Marching`, `Ready`, `FreeCombat`, `Sleeping`) with hysteresis (`Ready/Combat` entry and exit distances).
  - Assigns forced squad targets by TTL; in `FreeCombat` targets are released only when a unit is close enough (world distance based on `AttackRange`, optional hex override).
  - Assigns per-unit formation slot indices for arrival offsets when enabled.
  - Computes a squad move anchor via flow fields; non-free-combat units follow formation slots around the anchor (macro->micro split).
- `UnitCombatJobScheduler`:
  - Jobified spatial hash (`NativeParallelMultiHashMap`) for nearest enemy search.
  - `Interval`, `HashCellSize`, `HashRings` control cadence and coverage.
  - Uses double buffering: schedule in one tick, apply results on the next update.
  - Skips squad-controlled units (squad targeting handled by `EnemySquadManager` + `OccupancyHash`).
  - Can reuse `UnitSoARegistry` snapshots when enabled to reduce per-unit transform reads.
- `OccupancyHash`:
  - Rebuilt every frame; native hash for occupancy counts and a managed bucket map for nearest lookup.
- `OrcaAvoidanceSystem`:
  - ORCA/RVO local avoidance in jobs, using a spatial hash and per-unit velocity override.
  - Supports cohesion toward friendly centroid to keep groups together.
  - Per-unit priority (`UnitView.OrcaPriority`) shifts avoidance responsibility; `MinResponsibility` clamps zero-weight agents.
  - Respects per-unit `UseOrcaVelocity` (units can opt out of overrides but remain avoidance obstacles).
- `LocalAvoidanceSystem`:
  - Legacy steering avoidance (disabled by default when ORCA is enabled; no-ops while ORCA is active).

## Environment and Obstacles
- `ProceduralObstacles`:
  - Spawns rocks by `Count` or `CoveragePercent`.
  - `UseRandomSeed=true` uses time-based randomness; `false` uses `Seed`.
  - Ensures obstacle layer is included in `HexPathfindingBootstrap.ObstacleMask`.
  - Re-bakes walkability after spawn using a bounded rect when possible.

## Save / Load
- `SaveSystem`:
  - JSON schema version 1.
  - Saves stocks, unit snapshots (position, destination, faction, HP), research status map, blocked cells.
  - Restores units via `CompositionRoot.RestoreUnitsEx` with sprites, faction tint, overlays, and culling.

## Debugging, Profiling, Diagnostics
- `PathProfiler`:
  - Tracks builds/accepts/rejects, max nodes, path lengths, commands, jitter, crowd moves, resets.
  - Optional anomaly log with thresholds (`AnomalyFrameTimeMs`, `AnomalyJitter`, etc).
- `PathDebugHUD`: OnGUI overlay for path stats.
- `PathRequestQueue.LogJobResults` and `PathManager.LogBuildFailures` for verbose tracing.
- `UnitView.EnableJitterLog` and `UnitCombat.LogCombatResets` for movement diagnostics.

## Tests and Benchmarks
- PlayMode:
  - `FpsStressTests` logs average FPS per scenario (`[FpsStress]` line).
  - `CombatPathResetTests` tracks destination reset rates during chase.
  - `UnitCombatStallTests` checks close-range attacks and chasing.
- EditMode:
  - `UnitCombatTargetingTests` verifies target resolution priority.
