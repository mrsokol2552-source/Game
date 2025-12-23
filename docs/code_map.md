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
- Configs: `GameConfig`, `BuildingConfig`, `ResearchConfig`, `UnitConfig`.
- Persistence: `SaveSystem` (JSON to `Application.persistentDataPath/save.json`).

Presentation:
- Bootstrap: `CompositionRoot`.
- Input: `InputController`, `UnitSpawnerCommander`.
- UI: `HudController`, `ActionsPanel`, `ResearchPanel`.
- View: `UnitView`, `UnitCombat`, `UnitHpOverlay`, `MovementSettings`.
- Performance: `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `UnitVisualCulling`.
- Pathfinding: `PathManager`, `PathRequestQueue`, `HexPathfindingBootstrap`, `HexPathfinderJob`, `PathfindingBootstrap` (grid fallback), `CrowdingResolver`, `PathProfiler`, `PathDebugHUD`.

## Bootstrap and singletons

- `CompositionRoot` (`Presentation/Bootstrap/CompositionRoot.cs`):
  - Creates `GameStateService` and binds `SaveSystem` callbacks.
  - Optionally runs `StartNewGame` with `GameConfig.StartingResources`.
  - Ensures `CameraZoom2D`, `HexPathfindingBootstrap`, `ProceduralObstacles`, `UnitCombatJobScheduler`, `EnemySquadManager`, `OccupancyHash`, `PathRequestQueue`.
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
  - Clears expired forced targets and job targets.
  - `ResolveTarget`:
    - Uses job target (`UnitCombatJobScheduler`) if available.
    - Falls back to `OccupancyHash` only when the job scheduler is disabled.
    - Forced squad target is overridden if a local target is within `AttackRange * LocalThreatOverrideMultiplier`.
  - If target is outside `AttackRange`, computes desired position, snaps to hex center if moving into a new cell, and requests a path.
  - If in range, cancels combat path and attacks on cooldown.

### Path request pipeline
- `PathRequestQueue.Update`:
  - Schedules at most `MaxPerFrame` requests; uses jobs when `UseJobs=true`.
  - Builds an enemy-only occupancy hash for jobs.
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
     -> OccupancyHash (only if scheduler disabled)
     -> forced squad target (unless local threat overrides)
  -> If target:
     - out of range -> compute desired -> path request
     - in range -> cancel combat path + attack
  -> If no target -> cancel combat steering
```

### Jobified targeting (UnitCombatJobScheduler)
```
UnitCombatJobScheduler.Update
  -> GatherUnits
  -> FillArrays + Build hash buckets
  -> Schedule NearestEnemyJob
  -> Complete job (same frame)
  -> ApplyResults -> UnitCombat.SetJobNearest
```

### Path request pipeline (jobs)
```
PathRequestQueue.Update
  -> Dequeue request
  -> TryScheduleJob
     -> Get walkable native map from HexPathfindingBootstrap
     -> Build enemy-only occupancy hash
     -> Schedule HexPathfinderJob
  -> FinishJob
     -> Convert cells to world points (skip start cell)
     -> Fallback to PathManager.BuildPath if needed
     -> Callback with world path
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

- `PathManager.BuildPath`:
  - Chooses hex bootstrap if available, else grid fallback.
  - Occupancy: blocks enemies and optionally recent friendly cells (`FriendlyReserveSeconds`).
  - `EnableGroupPathReuse` caches paths by target cell for nearby allies.
  - Converts grid path to world points; skips start cell and smooths straight segments.

- `UnitPathFollower`:
  - Maintains a queue of world points and advances when within `WaypointEpsilon`.
  - Simplifies straight runs (`StraightDotThreshold`, `MinStraightRun`).
  - `Source` = `Manual` or `Combat` so combat logic avoids clobbering manual paths.

## Performance helpers

- `UnitCombatJobScheduler`:
  - Collects unit positions/factions into `NativeArray`.
  - Builds a spatial hash with `NativeParallelMultiHashMap`.
  - Runs `NearestEnemyJob` and writes indices back to units via `SetJobNearest`.

- `EnemySquadManager`:
  - Groups by radius and assigns a shared target for each squad.
  - Optional path sharing for leader and members; can also drive player units when `DrivePlayers=true`.

- `OccupancyHash`:
  - Rebuilt every frame; used for quick occupancy checks and nearest enemy lookup if job scheduler is disabled.

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
