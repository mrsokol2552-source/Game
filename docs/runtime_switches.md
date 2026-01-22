# Runtime Switches

This file tracks the runtime and inspector parameters that actually change behavior during debugging, balancing, and profiling.
It is not a full dump of every serialized field. It is the practical subset.

See also:

- [debug_playbooks.md](./debug_playbooks.md)
- [code_id_index.md](./code_id_index.md)
- [code_map.md](./code_map.md)

Columns:

- `Parameter` - field name in code/inspector
- `Effect` - practical runtime effect
- `When to touch` - the situation where changing it makes sense
- `Risk` - what usually breaks when it is set badly
- `Where to read` - `CODE-ID` and internal sections

## ProceduralEnvironment: world generation and streaming

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `Enabled` | Enables the whole generator | If you need to disable auto world generation temporarily | Scene stays empty | `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`, `PENV-02` |
| `GenerateOnAwake` | Starts generation in `Awake` | If you need a manual start | Scene can stay empty until a manual call | `PENV-02` |
| `UseAsyncGeneration` | Slices generation across frames | If start-up freezes are too large | Longer first load | `PENV-02`, `PENV-07` |
| `GroundRowsPerFrame` | Terrain rows processed per frame | If start-up must be smoother | Too low = slow load, too high = stutter | `PENV-07` |
| `PropAttemptsPerFrame` | Prop placement attempts per frame | If props dominate start-up cost | Too low = sparse world, too high = frame spikes | `PENV-08`, `PENV-09` |
| `UseWorldStreaming` | Enables chunk streaming | For large maps | World may exist only around the camera | `PENV-03`, `PENV-05`, `PENV-10` |
| `StreamChunkSize` | Chunk size | For streaming tuning | Small = overhead, large = frame spikes | `PENV-05`, `PENV-06` |
| `StreamActiveRadius` | Radius of actively loaded chunks | If too little world is visible around the player | High RAM/CPU cost | `PENV-05` |
| `StreamPrefetchRadius` | Radius of preloaded chunks | If the world appears too late at the screen edge | Extra background load | `PENV-05` |
| `StreamUnloadRadius` | Radius where chunks start unloading | If memory or loaded-chunk count keeps rising | Too low = constant re-generation | `PENV-05`, `PENV-10` |
| `StreamMaxLoadedChunks` | Hard cap of loaded chunks | To control memory | Too low = holes, too high = RAM pressure | `PENV-05` |
| `StreamChunksPerFrame` | Chunks processed per frame | To balance FPS vs load speed | Direct frame spikes | `PENV-05` |
| `StreamFrameBudgetMs` | Streaming time budget per frame | If stable FPS matters more than load speed | Too low = streaming feels slow | `PENV-05` |
| `StreamTargetFps` | Target FPS for budget logic | For weaker PCs | Indirectly slows streaming | `PENV-05` |
| `StreamSkipIfOverBudget` | Skip streaming work if the frame is already overloaded | If camera movement destroys FPS | World arrives more slowly | `PENV-05` |
| `StreamKeepGeneratedChunks` | Keep already generated chunks | If re-bake must be avoided | High memory usage on big maps | `PENV-05`, `PENV-10` |
| `StreamBakeAllChunksOnIdle` | Gradually pre-bake chunks while the camera is idle | If the world should fill in over time | Hidden background load | `PENV-03`, `PENV-11` |
| `StreamPropsUseBackgroundGrid` | Place props/trees on background-grid instead of hex-grid | If visuals must lock to square background cells | Easy to break mask coordinates and cleanup | `PENV-09`, `PENV-10`, `PENV-18` |

## ProceduralEnvironment: far view and bake

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `UseFarViewBake` | Enables the far-view system | For high zoom-out views | Can conflict with streaming and hide the map | `PENV-11`, `PENV-12`, `PENV-13` |
| `UseFarViewChunkedBake` | Bakes far view in chunks | Almost always for large maps | Slower full readiness | `PENV-13` |
| `UseFarViewDirectChunkRender` | Draws chunks directly instead of a safer intermediate path | Only for targeted profiling | Higher risk of render bugs | `PENV-13` |
| `FarViewOrthoThreshold` | Absolute ortho size threshold | If threshold should not depend on camera settings | Easy to miss the right switch point | `PENV-11`, `CameraZoom2D` |
| `UseFarViewThresholdFromCameraZoom` | Derives threshold from `CameraZoom2D.MaxOrthoSize` | If threshold should scale with the camera setup | Wrong percentage makes far-view too early or too late | `PENV-11` |
| `FarViewOrthoThresholdPercent` | Threshold as a fraction of `MaxOrthoSize` | Main practical tuning knob | Too low = extra bake load, too high = low FPS before switch | `PENV-11` |
| `FarViewChunksPerFrame` | Far-view chunks baked per frame | To balance bake speed vs FPS | Strong direct cost during bake | `PENV-13` |
| `FarViewBakeFrameBudgetMs` | Time budget for bake work in a frame | If FPS during bake must stay stable | Bake can become too slow | `PENV-13` |
| `FarViewChunkPixels` | Chunk resolution | To trade quality against speed | Large = CPU/GPU spikes, small = overhead | `PENV-13` |
| `FarViewPixelsPerUnit` | Far-view texture density | If distant view looks blurry | Higher memory and bake time | `PENV-13` |
| `FarViewMaxTextureSize` | Upper bound for RT/texture size | To respect VRAM limits | Too high risks GPU memory pressure | `PENV-12`, `PENV-13` |
| `UseFarViewAsyncReadback` | Async readback of baked chunks | If sync readback kills FPS | Pending readbacks can stall | `PENV-13` |
| `FarViewMaxPendingReadbacks` | Maximum queued readbacks | If async readback stalls | Too low = slow, too high = memory/stability risk | `PENV-13` |
| `FarViewHideMapWhileBaking` | Hide the regular map during bake | If bake causes distracting popping | Harder to debug visuals during bake | `PENV-14` |
| `ShowFarViewBakeHUD` | Show bake HUD | Usually useful during tuning | UI noise if left on permanently | `PENV-14` |

## ProceduralEnvironment: ground conversion and background

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `ConvertGroundTilesRuntime` | Runtime convert isometric tiles to square tiles | If using the current ground pipeline | Turning it off breaks the background | `PENV-07`, `PENV-16` |
| `PreconvertGroundTilesRuntime` | Pre-build converted tile palette | If runtime conversion is too slow | Longer initialization | `PENV-07` |
| `UseGroundTileManualDiamond` | Uses manual diamond cutout | Main mode for the current tileset | Wrong points break all ground visuals | `PENV-07` |
| `GroundTileDiamondNormalized` | Interprets diamond points as normalized instead of pixel coordinates | Only when swapping sprite source conventions | Easy to shift the diamond incorrectly | `PENV-07` |
| `GroundTileDiamondYFromTop` | Interprets Y values from the top edge | When source coordinates use top-origin space | Wrong interpretation flips the mask logic | `PENV-07` |
| `GroundTileDiamondInsetPixels` | Pulls the diamond inward | If black or dirty edges remain | Too high eats useful texture | `PENV-07` |
| `GroundTileEdgeDilatePixels` | Expands the cleaned edge | If there are visible gaps between square tiles | Easy to get blur | `PENV-07` |
| `GroundTileEdgeTrimPixels` | Trims problematic edges | If thin dark pixels remain | Too much trim damages the pattern | `PENV-07` |
| `GroundTileEdgeBlackThreshold` | Filters dark edge pixels | For black dots or edge artifacts | Too high destroys detail | `PENV-07` |
| `GroundTileEdgeChromaThreshold` | Filters low-chroma edge artifacts | For thin dark seams | Too high quickly causes muddy tiles | `PENV-07` |
| `GroundTilePreservePattern` | Tries to preserve internal texture pattern | If the tile surface matters more than aggressive cleanup | Can block edge cleanup | `PENV-07` |
| `GroundTileNoTransform` | Keeps only the diamond cut without square transformation | For debugging the raw cutout | Background becomes incompatible with square layout | `PENV-07` |
| `UseBackgroundTilemap` | Uses a separate rect-grid background | Required for square-ground visuals over hex gameplay | Without it the system falls back toward hex visuals | `PENV-04`, `PENV-16` |
| `BackgroundUseConvertedTiles` | Uses converted tiles in the background | Normally should stay on | Otherwise square conversion loses its purpose | `PENV-07`, `PENV-16` |
| `BackgroundCellOverlapPixels` | Overlaps square cells on the background | If micro-gaps are visible | Too much overlap causes dirty seams and drift | `PENV-16` |

## ProceduralEnvironment: water, rock, and biome generation

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `UseWaterBiome` | Enables water and rock masks | If rivers, lakes, and rocky coasts are needed | Without palette discipline this becomes noise | `PENV-17`, `PENV-18` |
| `WaterCoverage` | Base share of water in the world | Coarse water density control | High values turn the map into swamp | `PENV-17` |
| `RiverCount` | Number of rivers | If large linear water features are needed | Too many rivers fragment the map | `PENV-17` |
| `RiverWidthMin/Max` | River thickness | To calibrate major rivers | Too small = noisy, too large = lake-like | `PENV-17` |
| `LakeMinSize/LakeMaxSize` | Lake size range | To control lake macro-shape | Large lakes can eat too much land | `PENV-17` |
| `RockMinThickness/RockMaxThickness` | Rocky border thickness around water | If shorelines should be rocky | Too thick suppresses normal land | `PENV-17` |
| `UseWaterMaskSmoothing` | Smooths the water mask | If water looks pixelated | Too much smoothing removes small shapes | `PENV-17` |
| `UseWaterEdgeColorMatch` | Chooses edge tiles by side-color masks | Main shoreline quality control | Bad tile masks make selection unstable | `PENV-07`, `PENV-17` |
| `UseWaterAutoInteriorByColor` | Auto-detect fully-water interior tiles | If no manual interior set is maintained | Can misclassify edge tiles | `PENV-17` |
| `WaterTilesAllowRotation` | Allows rotating water tiles | Usually better left off | Easy to invert water-to-land direction | `PENV-07`, `PENV-17` |
| `RockTilesAllowRotation` | Allows rotating rock shoreline tiles | Only if the palette is well-structured | Same inversion risk | `PENV-07`, `PENV-17` |
| `UseWaterEdgeRefinement` | Post-pass to improve shoreline picks | If primary edge matching is still rough | Extra generation cost | `PENV-07`, `PENV-17` |
| `WaterEdgeMaskSamples` | Sample points per tile side | If edge matching is too coarse | More samples cost more | `PENV-17` |
| `WaterEdgeMaskRatioThreshold` | How much water on a side is needed to classify it as a water edge | For shoreline tuning | Wrong threshold breaks side classification | `PENV-17` |
| `WaterEdgeMaskMatchWeight` | Weight of edge mask matching | To strengthen valid shoreline picks | Too high reduces variety | `PENV-17` |
| `WaterEdgeSmoothnessWeight` | Weight of smooth shoreline continuity | If coastlines need to read as one line | Too high makes all shores look similar | `PENV-17` |

## ProceduralEnvironment: vegetation and blockers

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `PropCoverage` | Base density of normal props | Overall map detail density | Direct CPU cost and visual clutter | `PENV-08`, `PENV-09` |
| `PropMinHexDistance` | Spacing between props | If props clump too much | Too high creates dead empty zones | `PENV-08`, `PENV-09` |
| `TreeCoverage` | Base tree density | Overall forest density | Too many trees hurt FPS and readability | `PENV-08`, `PENV-09` |
| `TreeMinHexDistance` | Spacing between trees | If forests are too dense | Too high breaks forest continuity | `PENV-08`, `PENV-09` |
| `TreesBlockMovement` | Trees become obstacles | If trees should matter for navigation | Too much blocking hurts pathing | `PENV-08`, `PENV-09`, `HPFB-04` |
| `TreeGradientEdge` | Lowers density near forest edges | If forests should be dense inside and sparse outside | Too high shrinks forest cores | `PENV-08`, `PENV-09` |
| `TreeGradientPower` | Controls contrast of the forest gradient | If forest center/edge contrast should be stronger | Too high makes hard forest borders | `PENV-08`, `PENV-09` |
| `UseTreeBiomeNoise` | Separate biome field for trees | If tree biomes should be independent of land texture | If off, trees distribute more evenly | `PENV-08`, `PENV-09` |
| `TreeBiomeScale` | Scale of forest biomes | For larger or smaller forest regions | Too fine = patchiness | `PENV-08`, `PENV-09` |
| `TreeBiomeThreshold` | Threshold for entering tree biomes | Main control of forest coverage | Strong effect on the whole map | `PENV-08`, `PENV-09` |
| `TreeBiomeFeather` | Softness of forest biome borders | If forest edges look too cut out | Too soft spreads forests everywhere | `PENV-08`, `PENV-09` |
| `TreeAccentCoverage` | Share of rare/accent trees | If colored accent trees are needed | Easy to overdo and overwhelm green trees | `PENV-08`, `PENV-09` |
| `RockPropCoverage` | Density of props on rock only | If reeds/rock props are needed on rocky coast | With bad filtering they leak into water | `PENV-09`, `PENV-17` |
| `BlockingPropCoverage` | Density of blocking props | If more movement blockers are needed | Can badly harm walkability and flow fields | `PENV-08`, `PENV-09` |
| `UseDirectWalkableUpdates` | Update walkability directly instead of rebaking through physics | If collider-based rebake is too expensive | Visuals and blocked cells can diverge | `PENV-08`, `PENV-09`, `HPFB-04` |

## HexPathfindingBootstrap

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `Width`, `Height` | Hex-grid size | If the world size changes | Large values quickly hit RAM/CPU limits | `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP`, `HPFB-01` |
| `HexSize` | World size of one hex | If map scale changes | Breaks alignment between terrain, units, and pathing | `HPFB-01`, `HPFB-03` |
| `AutoClampSize` | Automatically clamps grid size | If the requested grid exceeds safe limits | Can silently shrink the effective world | `HPFB-01` |
| `MaxCells` | Cell limit used by clamping | For large-map tuning | Too high increases memory sharply | `HPFB-01` |
| `AutoBakeColliders` | Bakes obstacles from physics at startup | If collider-driven obstacles are required | Expensive startup and possible mismatch with direct updates | `HPFB-02` |
| `SampleRadius` | Radius used when sampling physics obstacles | If obstacle bake is wrong | Too high inflates blocked zones | `HPFB-02` |

## PathManager and PathRequestQueue

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `MaxBuildsPerFrame` | Sync path builds allowed per frame | If path building dominates CPU | Too low makes units slow to react | `PMGR-01` |
| `EnableGroupPathReuse` | Reuses paths for nearby groups | If many units move similarly | Bad reuse can produce suboptimal paths | `PMGR-01` |
| `GroupReuseFrames` | Lifetime of reusable paths | To tune reuse aggressiveness | Too low wastes reuse value | `PMGR-01` |
| `MaxPathNodes` | Hard path length cap | If long routes time out | Too low cuts off distant paths | `PMGR-01` |
| `FriendlyReserveSeconds` | Temporary reservation of friendly cells | If paths should avoid moving through crowds | Too high produces false blocking | `PMGR-01`, `PMGR-03` |
| `UseOccupancyHash` | Uses dynamic occupancy | Usually should stay on | If off, units route through moving crowds too easily | `PMGR-03` |
| `UseStaticObstacleHash` | Uses static obstacle hash | If blockers, rocks, or trees matter | If off, units route through hard obstacles | `PMGR-03` |
| `MaxPerFrame` | Path requests processed per frame | If the queue keeps growing | Too high hurts the frame | `PQUE-01`, `PQUE-02` |
| `UseJobs` | Uses jobified path builds | Usually for large scenes | If native data is suspect, fallback behavior becomes important | `PQUE-01`, `PQUE-02` |
| `MaxQueueSize` | Maximum path request queue size | If command spam overwhelms the system | Older requests will be dropped | `PQUE-01` |
| `ProcessSynchronouslyIfIdle` | Handles requests immediately when idle | For lower latency in quiet moments | Can create CPU spikes | `PQUE-01` |

## FlowFieldManager

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `Enabled` | Enables flow field navigation | For squad/macro navigation | If off, load shifts back to per-unit pathing | `FFLD-01` |
| `CellsPerFrame` | Cell-processing budget | If fields build too slowly | Too high creates spikes | `FFLD-01`, `FFLD-07` |
| `MaxFields` | Maximum active fields | If many squads/targets coexist | More fields means more memory and updates | `FFLD-01` |
| `FieldTtl` | Lifetime of a field | If fields rebuild too often | Too high can keep stale fields alive | `FFLD-01` |
| `UseTiledFields` | Coarse tile gating | Usually useful for large maps | Bad coarse routing can cause odd detours | `FFLD-03`, `FFLD-07` |
| `TileSize` | Coarse tile size | To balance quality and speed | Too large makes gating rough | `FFLD-03` |
| `UseCrowdCosts` | Adds density costs | If squads push into each other too hard | Extra cost to rebuild crowd maps | `FFLD-04` |
| `UseDeterministicDirections` | Makes downhill direction choice more stable | If route shape should be stable | Less variation in movement | `FFLD-05` |
| `UseVectorSampling` | Smooth vector-like stepping | If flow movement looks too grid-like | Can create odd micro motion near obstacles | `FFLD-06` |
| `UseInfluenceCosts` | Adds threat/influence costs | If squads should avoid dangerous zones | Extra influence rebuild cost | `FFLD-04` |
| `UseLoSSmoothing` | Uses line-of-sight smoothing | If flow routes zigzag too much | Additional line checks | `FFLD-06` |

## EnemySquadManager

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `MaxSquadSize` | Maximum units per squad | To shape combat structure | Very large squads reduce tactical flexibility | `ESQD-01`, `ESQD-04` |
| `DrivePlayers` | Applies squad logic to player units too | If both factions should share the same squad pipeline | If off, player units fall back toward per-unit logic | `ESQD-01` |
| `InitialGatherRadiusHex` | Initial squad recruitment radius | If squads gather too slowly | Too high creates sloppy long-distance recruitment | `ESQD-01` |
| `MaxGatherRadiusHex` | Maximum recruitment radius | If sparse units fail to form squads | Too high turns squads into map-wide vacuum cleaners | `ESQD-01` |
| `GatherRadiusStepSeconds` | How quickly the recruitment radius grows | If formation is too slow | Fast growth hurts locality | `ESQD-01` |
| `SleepRetrySeconds` | Retry interval for sleeping underfilled squads | If squads stay underfilled too long | Too frequent retries waste work | `ESQD-01` |
| `ReadyDistanceHex` / `CombatDistanceHex` | Entry thresholds for `Ready` and `FreeCombat` | If squads break into combat too early or too late | Wrong threshold pair ruins combat rhythm | `ESQD-04` |
| `ReadyExitDistanceHex` / `CombatExitDistanceHex` | Exit hysteresis thresholds | If squads flicker between states | Too little hysteresis causes state jitter | `ESQD-04` |
| `UseSquadFlow` | Uses flow fields for squad movement | If macro squad movement matters | If off, pressure shifts to per-unit steering | `ESQD-05` |
| `SquadFlowMinDistance` | Distance where squad flow takes over | If macro-to-micro transition feels wrong | Strong influence on transition timing | `ESQD-05` |

## UnitCombat

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `UseFactionOverrides` | Uses different values for player and enemy | If the two sides should behave asymmetrically | Easy to forget the base values | `UCOM-01`, `UCOM-07` |
| `PlayerAttackRange`, `EnemyAttackRange` | Faction-specific attack ranges | For rifle vs melee tuning | Over-separating them distorts combat balance | `UCOM-01`, `UCOM-03` |
| `UseFlowFields` | Lets combat movement use flow fields | If long-range chase should be group-driven | If off, repath load goes up | `UCOM-01`, `UCOM-08` |
| `FlowFieldMinDistance` | Minimum distance before flow mode is used | If flow activates too early or too late | Changes macro/micro movement quality | `UCOM-08` |
| `RepathInterval*` | Per-unit repath timing | If units stall or thrash | High repath frequency increases load | `UCOM-03` |
| `InstantRepathOnTargetCellChange` | Repath immediately when the target cell changes | If targets move rapidly | More path load | `UCOM-03` |
| `TargetRefreshInterval` | How often the unit reevaluates targets | If units react too slowly | Too fast = CPU, too slow = sluggishness | `UCOM-03`, `UCOM-06` |
| `JobTargetTtl` | Lifetime of a job-scheduler target | If units stick to stale targets | Low TTL forces more local searches | `UCOM-06` |
| `LostTargetGraceSeconds` | Keeps combat intent briefly after losing target | If units freeze the moment a target dies | Too high makes them drift into empty space | `UCOM-03` |
| `DisableOrcaWhenInRange` | Disables ORCA near attack range | If ranged firing lines misbehave | Can reintroduce overlap near targets | `UCOM-03` |
| `FriendlySeparationRadius` | Friendly spacing during combat | If friendlies overlap and flicker | Too high breaks formations | `UCOM-03` |
| `UseCrouchWhenBlocked` | Makes front units crouch when a friendly is behind them | For layered firing visuals | Bad conditions produce odd posture changes | `UCOM-03` |
| `UseFormationOffsets` | Uses arrival offsets near targets | If squads compress into a blob | Can conflict with tight terrain | `UCOM-03` |

## ORCA and movement jobs

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `Enabled` | Enables ORCA | If strong local avoidance is required | If off, overlap comes back | `ORCA-01`, `ORCA-03` |
| `CellSize` | Spatial hash cell size | If dense crowds avoid poorly | Too small or too large weakens neighbor quality | `ORCA-01` |
| `NeighborDist` | Neighbor search radius | If avoidance feels weak | Larger radius costs more | `ORCA-01` |
| `MaxNeighbors` | Maximum considered neighbors | If dense clusters still interpenetrate | Low values miss important agents | `ORCA-01` |
| `AgentRadius` | Effective agent size | Main anti-overlap control | Too high creates visible force fields | `ORCA-01` |
| `TimeHorizon` | Collision prediction horizon | If avoidance reacts too late | High values make agents overly cautious | `ORCA-01` |
| `UseCohesion` | Pulls friendlies toward their local center | If groups spread too much | Can increase overlap if too strong | `ORCA-01`, `ORCA-03` |
| `AvoidEnemies` | Includes enemies in avoidance | If opposing groups should flow around each other | Can reduce aggression for melee swarms | `ORCA-01` |
| `UseSoARegistry` | Uses snapshot registry for inputs | If transform reads are too expensive | If snapshots are wrong, ORCA sees garbage | `ORCA-01`, `ORCA-02` |
| `BatchSize` | Job batch size | Profiling-only tuning | Wrong values can hurt scheduling | `ORCA-02` |
| `Interval` | How often ORCA updates | If quality vs cost must be balanced | Long intervals make response late | `ORCA-01` |

## CameraZoom2D

| Parameter | Effect | When to touch | Risk | Where to read |
|---|---|---|---|---|
| `MinOrthoSize`, `MaxOrthoSize` | Zoom bounds | If the world scale changes | Affects far-view threshold percentage | `SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D` |
| `OrthoStep` | Scroll step size | If zoom feels too coarse or too slow | Mostly UX, not architecture | `CameraZoom2D` |
| `PanSpeed` | WASD/MMB movement speed | If camera movement is too slow or too fast | Large worlds usually need zoom-scaled pan | `CameraZoom2D` |
| `PanZoomScale` | How much pan speed scales with zoom | If the camera crawls at high zoom-out | Too high causes jumpy camera movement | `CameraZoom2D` |

## Highest-value switches for day-to-day work

These are the first knobs to consider before touching deeper code:

1. `UseWorldStreaming`
2. `StreamChunkSize`
3. `StreamActiveRadius`
4. `StreamPrefetchRadius`
5. `StreamFrameBudgetMs`
6. `UseFarViewBake`
7. `FarViewOrthoThresholdPercent`
8. `FarViewChunksPerFrame`
9. `UseWaterBiome`
10. `UseWaterEdgeColorMatch`
11. `TreeCoverage`
12. `RockPropCoverage`
13. `UseFlowFields`
14. `TargetRefreshInterval`
15. `FriendlySeparationRadius`
16. `AgentRadius`
17. `MaxPerFrame` in `PathRequestQueue`
18. `MaxBuildsPerFrame` in `PathManager`

Practical rule:

- if the problem is visual and world-related -> start in `ProceduralEnvironment`
- if it is combat or reaction related -> start in `UnitCombat` and `EnemySquadManager`
- if it is overlap related -> start in `ORCA` and `MovementJobSystem`
- if it is route related -> start in `PathRequestQueue`, `PathManager`, and `HexPathfindingBootstrap`
