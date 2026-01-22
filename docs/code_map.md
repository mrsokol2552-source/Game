# Code Map (Unity RTS Prototype)

This document is a code-level map for quick navigation. It complements `docs/gameplay_current_state.md` and focuses on where behavior lives in the codebase.

## Repository layout

- `My project/Assets/Scripts/Domain` - core data and rules (economy, research, unit stats).
- `My project/Assets/Scripts/Application` - use cases that orchestrate domain actions.
- `My project/Assets/Scripts/Infrastructure` - configs (ScriptableObjects) and persistence.
- `My project/Assets/Scripts/Presentation` - Unity MonoBehaviours for input, UI, view, pathfinding, performance.
- `My project/Assets/Tests` - EditMode/PlayMode tests and perf stress harness.
- `docs/` - system notes and architecture references.

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
- View: `UnitView`, `UnitCombat`, `UnitHpOverlay`, `MovementSettings`.
- Performance: `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `UnitVisualCulling`, `MovementJobSystem`, `OrcaAvoidanceSystem`, `UnitSoARegistry`, `LocalAvoidanceSystem` (legacy), `StuckResolver`.
- Performance: `JobPipelineCoordinator` (fixed update order for ORCA + Movement).
- Pathfinding: `PathManager`, `PathRequestQueue`, `HexPathfindingBootstrap`, `HexPathfinderJob`, `PathfindingBootstrap` (grid fallback), `FlowFieldManager`, `CrowdingResolver`, `ProceduralEnvironment`, `PathProfiler`, `PathDebugHUD`.
- Pathfinding: `StaticObstacleHash` (blocked-cell hash for fast static queries), `CoverSlotHash` (pre-baked cover slots).

## ProceduralEnvironment ground conversion preset (isometric -> square)

The current default preset is tuned for `Zombie Rural - HD Isometric Tileset` ground tiles (128x256). Defaults live in `My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs` and are mirrored in `My project/Assets/Scenes/SampleScene.unity`.

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

## Bootstrap and singletons

- `CompositionRoot` (`Presentation/Bootstrap/CompositionRoot.cs`):
  - Creates `GameStateService` and binds `SaveSystem` callbacks.
  - Optionally runs `StartNewGame` with `GameConfig.StartingResources`.
- Ensures `CameraZoom2D`, `HexPathfindingBootstrap`, `ProceduralObstacles`, `ProceduralEnvironment`, `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `StaticObstacleHash`, `CoverSlotHash`, `PathRequestQueue`, `FlowFieldManager`, `MovementJobSystem`, `OrcaAvoidanceSystem`, `StuckResolver`.
  - Disables `LocalAvoidanceSystem` when ORCA is enabled.
  - Applies `UnitVisualCulling` and sorting layer/order to existing units.
  - `Save()` and `Load()` wrap `SaveGame`/`LoadGame` use cases.

- `PathManager.Ensure()` and `PathRequestQueue.Ensure()` create global instances if missing.

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
- `UnitCombat.Update` (gated by `CombatTickInterval + CombatTickJitter`):
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
UnitCombat.Update (combat tick)
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
  - Collects unit positions/factions into `NativeArray`.
  - Builds a spatial hash with `NativeParallelMultiHashMap`.
  - Schedules `NearestEnemyJob` and applies results on the next update tick.
  - Skips squad-controlled units (squad targeting handled elsewhere).
  - Can reuse `UnitSoARegistry` snapshots when enabled to reduce per-unit transform reads.

- `EnemySquadManager`:
  - Forms squads for both factions (default size 12), with dynamic gather radius.
  - Uses squad-to-squad distance (hexes) to drive states with hysteresis.
  - Assigns forced targets via TTL; releases targets in `FreeCombat` only when close enough.
  - Assigns per-unit formation indices for arrival offsets when enabled.
  - Computes a flow-based squad move anchor; non-free-combat units follow formation slots around it.

- `OccupancyHash`:
  - Rebuilt every frame; used for quick occupancy checks and nearest enemy lookup when no job target is available.
- `MovementJobSystem`:
  - Jobified movement update for `UnitView` (enabled by default).
  - ORCA overrides can be reused for a short frame window to decouple job timing.
- `JobPipelineCoordinator`:
  - Drives ORCA + Movement in a fixed order and disables their internal Update loops when enabled.
- `UnitSoARegistry`:
  - Builds a centralized SoA snapshot for ORCA inputs to reduce redundant per-unit collection.
  - Also exposes combat snapshots for targeting systems when enabled.
- `OrcaAvoidanceSystem`:
  - ORCA/RVO avoidance with spatial hash; feeds velocity overrides into movement jobs.
  - Optional cohesion bias toward friendly centroid.
  - Respects `UnitView.UseOrcaVelocity` (units can opt out of overrides but remain obstacles).
  - Per-unit priority (`UnitView.OrcaPriority`) reduces avoidance responsibility (see `MinResponsibility`).
- `CrowdingResolver`:
  - LateUpdate stack resolver for non-squad, non-flow-field units; no-ops while ORCA or legacy local avoidance is active.
- `StuckResolver`:
  - Detects stuck movers and nudges them; can force combat repath.
  - Skips squad-controlled units outside `FreeCombat` and units currently following flow fields.

- `UnitVisualCulling`:
  - Disables `SpriteRenderer`, `Animator`, `UnitHpOverlay` when far from camera or outside frustum.

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
  - `FpsStressTests` logs `[FpsStress]` average FPS for staged unit counts.
  - `CombatPathResetTests` checks path reset rates during chase.
  - `UnitCombatStallTests` checks in-range attacks and chase behavior.
- EditMode:
  - `UnitCombatTargetingTests` validates target resolution priority.

## Not currently wired

- `UnitConfig` exists but is not referenced by runtime code.
- `UnitStats.Speed` is not used by `UnitView` (movement uses `MovementSettings`).
