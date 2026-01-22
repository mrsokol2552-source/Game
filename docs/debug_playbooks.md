# Debug Playbooks

This file is the symptom-first entry layer for working with the project.
Use it together with:

- [code_map.md](./code_map.md) for architecture overview
- [code_id_index.md](./code_id_index.md) for direct `CODE-ID` and section lookup
- [runtime_switches.md](./runtime_switches.md) for inspector/runtime tuning

Each playbook follows the same format:

- `Symptom` - what is visible in-game or in the profiler
- `Open first` - the smallest set of files/sections to inspect
- `Check switches` - parameters most likely to change behavior
- `Common causes` - the failure modes that repeat most often
- `Fast path` - a short verification route without reading the whole class

## PB-01. Map does not fully load, blue void is visible

Symptom:

- part of the map stays blue;
- terrain appears only after moving or zooming the camera;
- objects can hang in empty space.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-03`, `PENV-05`, `PENV-06`, `PENV-10`, `PENV-16`, `PENV-17`, `PENV-18`
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP`
- `HPFB-01`, `HPFB-04`

Check switches:

- `UseWorldStreaming`
- `StreamChunkSize`
- `StreamActiveRadius`
- `StreamPrefetchRadius`
- `StreamUnloadRadius`
- `StreamMaxLoadedChunks`
- `StreamKeepGeneratedChunks`
- `StreamBakeAllChunksOnIdle`
- `BackgroundMaxCells`
- `AutoClampSize`, `MaxCells` in `HexPathfindingBootstrap`

Common causes:

- streaming only generates part of the world and unloads nearby chunks too aggressively;
- hex-grid and background-grid bounds diverge;
- props are placed/cleared in the wrong coordinate space;
- `HexPathfindingBootstrap` silently clamps the usable world size.

Fast path:

1. Read `PENV-03` for streaming startup.
2. Read `PENV-05` for chunk selection and queueing.
3. Read `PENV-10` for unload cleanup.
4. Read `PENV-16` and `PENV-18` for background bounds math.
5. Read `HPFB-01` and `HPFB-04` for grid size and native walkable updates.

## PB-02. Trees and props spawn on water, rock, or beyond visible land

Symptom:

- trees or props appear on water;
- normal land props appear on rock;
- terrain stops visually, but props continue beyond it.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-08`, `PENV-09`, `PENV-10`, `PENV-17`, `PENV-18`

Check switches:

- `StreamPropsUseBackgroundGrid`
- `StreamIncludeProps`, `StreamIncludeTrees`, `StreamIncludeBlockers`
- `PropCoverage`, `TreeCoverage`, `RockPropCoverage`
- `PropsBlockMovement`, `TreesBlockMovement`
- `AutoSplitGroundByName`, `AutoSplitBlockingByName`
- `UseWaterBiome`

Common causes:

- water/rock mask checks happen in hex coordinates while placement happens in background coordinates;
- chunk cleanup uses the wrong bounds;
- `RockPropTiles` is empty or polluted with land props;
- land placement does not filter `waterMask` or `rockMask`.

Fast path:

1. Read `PENV-09` first if placement uses background-grid.
2. Otherwise read `PENV-08`.
3. Then read `PENV-17` and `PENV-18` to verify mask-to-placement mapping.
4. Finally read `PENV-10` to verify unload cleanup.

## PB-03. Water and rock coastlines look noisy or broken

Symptom:

- shoreline is built from random water/rock tiles;
- interior water tiles are mixed with edge tiles;
- rivers and lakes exist but do not read visually.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-07`, `PENV-17`, `PENV-18`

Check switches:

- `UseWaterBiome`
- `WaterCoverage`
- `RiverCount`, `RiverWidthMin`, `RiverWidthMax`
- `LakeMinSize`, `LakeMaxSize`, `LakeAttempts`
- `RockMinThickness`, `RockMaxThickness`
- `UseWaterMaskSmoothing`
- `UseWaterEdgeColorMatch`
- `UseWaterAutoInteriorByColor`
- `UseWaterEdgeRefinement`
- `WaterTilesAllowRotation`, `WaterTilesAllowMirroring`
- `RockTilesAllowRotation`, `RockTilesAllowMirroring`

Common causes:

- water/rock tiles leak into the normal land palette;
- edge refinement does not match the actual tile edge masks;
- rotation or mirroring inverts shoreline direction;
- exclusion keywords do not remove bad candidate tiles.

Fast path:

1. Read `PENV-17` for water/rock palette and masks.
2. Read `PENV-07` for background tile synthesis.
3. Read `PENV-18` for coordinate helpers.
4. If only the coast is broken, inspect edge color matching first.

## PB-04. Far view bake does not start, stalls, or hides the map

Symptom:

- bake HUD does not move;
- the map disappears after bake completes;
- bake is too slow or freezes the scene;
- the map disappears in chunks at high zoom-out.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-11`, `PENV-12`, `PENV-13`, `PENV-14`, `PENV-15`
- `SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D`

Check switches:

- `UseFarViewBake`
- `UseFarViewChunkedBake`
- `UseFarViewDirectChunkRender`
- `FarViewAlwaysActive`
- `FarViewBakeOnStart`
- `FarViewOrthoThreshold`
- `UseFarViewThresholdFromCameraZoom`
- `FarViewOrthoThresholdPercent`
- `FarViewChunkPixels`
- `FarViewChunksPerFrame`
- `FarViewBakeFrameBudgetMs`
- `UseFarViewAsyncReadback`
- `FarViewMaxPendingReadbacks`
- `FarViewIncludeBackground/Ground/Props/Blockers`
- `ShowFarViewBakeHUD`, `FarViewHideMapWhileBaking`

Common causes:

- far view is disabled while world streaming is active;
- bake camera, mesh, or render textures are not fully initialized;
- regular renderers are disabled after bake, but far view never becomes visible;
- chunk size or texture size is too large and readback stalls the frame.

Fast path:

1. Read `PENV-11` for activation rules.
2. Read `PENV-12` for far-view object setup.
3. Read `PENV-13` for the chunk bake loop and readbacks.
4. Read `PENV-14` for HUD and loading overlay behavior.

## PB-05. FPS drops while moving the camera

Symptom:

- FPS is stable while idle but drops during camera movement;
- the biggest drop happens near the visible edge of the world;
- profiler points to `ProceduralEnvironment`, obstacle rebuilds, or hash rebuilds.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-03`, `PENV-05`, `PENV-06`, `PENV-10`, `PENV-13`
- `SCRIPTS-PRESENTATION-PATHFINDING-STATICOBSTACLEHASH`
- `SCRIPTS-PRESENTATION-PERFORMANCE-OCCUPANCYHASH`

Check switches:

- `StreamChunksPerFrame`
- `StreamFrameBudgetMs`
- `StreamTargetFps`
- `StreamSkipIfOverBudget`
- `StreamPrefetchRadius`
- `StreamBakeAllChunksOnIdle`
- `FarViewChunksPerFrame`
- `FarViewBakeFrameBudgetMs`

Common causes:

- chunks are generated faster than the scene can absorb them;
- already processed chunks are re-entering expensive passes;
- props, trees, and blockers are generated together without budget separation.

## PB-06. Units ignore enemies, stall, or join combat too late

Symptom:

- units stand still until direct contact;
- some members of a squad do not join the fight;
- units lose targets and freeze;
- long-range behavior is worse than close-range behavior.

Open first:

- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT`
- `UCOM-03`, `UCOM-05`, `UCOM-06`, `UCOM-08`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER`
- `ESQD-04`, `ESQD-05`
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER`
- `FFLD-03`, `FFLD-06`, `FFLD-07`

Check switches:

- `UseFlowFields`
- `FlowFieldMinDistance`
- `TargetRefreshInterval`
- `JobTargetTtl`
- `LostTargetGraceSeconds`
- `UseBehaviorProfile`
- `UseAggroRange`, `AggroRange`
- `UseLeash`, `LeashRange`
- `MaxSquadSize`
- `ReadyDistanceHex`, `CombatDistanceHex`
- `UseSquadFlow`, `SquadFlowMinDistance`

Common causes:

- squad state keeps the unit in `Ready` or `Marching`, while flow field output is weak;
- job scheduler provides a stale target and local override never wins;
- the unit stops using individual paths too early;
- behavior profile limits aggro or leash more than expected.

## PB-07. Units overlap, flicker, or stand inside each other

Symptom:

- multiple units occupy nearly the same place;
- sprites flicker because they overlap;
- ORCA is enabled but visible separation is still weak.

Open first:

- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT`
- `UCOM-03`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM`
- `ORCA-03`, `ORCA-04`
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM`
- `MJOB-03`, `MJOB-04`
- `SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER`

Check switches:

- `DisableOrcaWhenInRange`
- `FriendlySeparationRadius`
- `CellSize`, `NeighborDist`, `MaxNeighbors`
- `AgentRadius`
- `TimeHorizon`
- `UseCohesion`, `CohesionWeight`
- `MinResponsibility`

Common causes:

- agent radius is too small relative to the sprite footprint;
- ORCA is disabled too early near attack range;
- movement jobs apply ORCA velocity, but direct steering partially overwrites it;
- cohesion is stronger than separation.

## PB-08. Pathfinding times out, queue grows, units do not receive real paths

Symptom:

- `PathRequestQueue` keeps growing;
- jobs rarely finish or fall back too often;
- units receive a destination but not a real path.

Open first:

- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE`
- `PQUE-01`, `PQUE-02`, `PQUE-03`
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER`
- `PMGR-01`, `PMGR-03`, `PMGR-04`
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP`
- `HPFB-03`, `HPFB-04`

Check switches:

- `MaxPerFrame`
- `UseJobs`
- `MaxQueueSize`
- `ProcessSynchronouslyIfIdle`
- `MaxBuildsPerFrame`
- `MaxPathNodes`
- `FriendlyReserveSeconds`
- `UseOccupancyHash`
- `UseStaticObstacleHash`

Common causes:

- native walkable data is stale or invalid after obstacle updates;
- jobs are enabled but the pipeline constantly falls back to sync path builds;
- occupancy or static hashes block too aggressively;
- combat repath budget produces too many requests per frame.

## PB-09. Unit sprites or animations are missing or wrong

Symptom:

- a unit exists but only the HP bar is visible;
- walk/attack states do not switch;
- weapon sprite is missing;
- units are too small or sort under the map.

Open first:

- `SCRIPTS-PRESENTATION-VIEW-UNITVIEW`
- `SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR`
- `SCRIPTS-PRESENTATION-VIEW-DIRECTIONALANIMATIONSET`
- `SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY`
- `SCRIPTS-TOOLS-CHARACTERCREATORSIMPLECONFIG`

## PB-10. HUD, input, or scene wiring behaves incorrectly

Symptom:

- pan or zoom stops working;
- HUD overlaps itself;
- save/load or UI buttons disappear;
- helper objects are not created in scene.

Open first:

- `SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT`
- `SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D`
- `SCRIPTS-PRESENTATION-INPUT-INPUTCONTROLLER`
- `SCRIPTS-PRESENTATION-INPUT-UNITSPAWNERCOMMANDER`
- `SCRIPTS-PRESENTATION-UI-HUDCONTROLLER`
- `SCRIPTS-PRESENTATION-UI-ACTIONSPANEL`

## PB-11. What to open first for a new task

If the task is about:

- world, water, trees, or streaming -> start with `PENV-*`
- combat, target choice, or reaction speed -> start with `UCOM-*`
- squads -> `ESQD-*`
- long-range navigation -> `FFLD-*`
- local avoidance and overlap -> `ORCA-*` and `MJOB-*`
- path build and fallback -> `PQUE-*`, `PMGR-*`, `HPFB-*`
- scene wiring, UI, or input -> `CompositionRoot`, `InputController`, `HudController`

Practical rule:

1. Start with the relevant playbook.
2. Jump to [code_id_index.md](./code_id_index.md).
3. Only then open the target file by `CODE-ID`.
