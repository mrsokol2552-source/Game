# Code ID Index

This document is the practical lookup table for the `CODE-ID` comments added to the project.
Use it together with [code_map.md](./code_map.md).
If the task starts from a bug symptom, open [debug_playbooks.md](./debug_playbooks.md) first.
If the task starts from a tuning parameter, open [runtime_switches.md](./runtime_switches.md) first.
If the task starts from live Unity scene/editor state, open [unity_mcp_tools.md](./unity_mcp_tools.md) first.
If the task starts from repo layout or ownership, open [repo_map.md](../maps/repo_map.md) first.
If the task starts from documentation ownership or overlap cleanup, open [document_roles.md](./document_roles.md) first.
If the task starts from the supplements layer, open [supplements_index.json](../supplements/supplements_index.json) first.

## How To Use

- If the task is broad, start from a file-level `CODE-ID`.
- If the task is in a large runtime system, jump to the short internal section IDs.
- In discussion, prefer `ID + short description` instead of vague references like "that pathfinding part".

Examples:

- `PENV-05` = streaming scheduler in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- `PENV-02` = generation bootstrap and lifecycle in [ProceduralEnvironment.Lifecycle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs)
- `PENV-07` = streamed background chunk synthesis in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-14` = far-view HUD / loading overlay in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-20` = biome-aware placement batching and tile application in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- `PENV-22` = palette routing and post-placement obstacle rebake in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
- `UCOM-03` = main combat tick in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UVEW-03` = per-frame movement integration in [UnitView.Movement.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs)
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER` = shared path manager file in [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)
- `PMGR-03` = occupancy-aware path cache layer in [PathManager.Occupancy.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
- `PMGR-04` = path reuse and nearest-free helpers in [PathManager.Reuse.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
- `FFLD-04` = crowd cost map logic in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FFLD-05` = influence cost map logic in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FFLD-06` = coarse tile-graph construction in [FlowFieldManager.TileGraph.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
- `FFLD-07` = per-target flow-field storage and integration state in [FlowFieldManager.FieldState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
- `PQUE-02` = queue draining, job scheduling, and immediate fallback dispatch in [PathRequestQueue.Dispatch.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
- `PQUE-03` = async job completion, occupancy snapshots, and callback safety in [PathRequestQueue.Completion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)

## Reading Routes

### 1. World generation, streaming, far-view bake

Open in this order:

1. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMING](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
2. `PENV-03`, `PENV-04`, `PENV-05`
3. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMCHUNKS](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
4. `PENV-06`, `PENV-07`, `PENV-08`, `PENV-09`, `PENV-10`
5. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-LIFECYCLE](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs)
6. `PENV-02`
7. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
8. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDMASKS](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
9. `PENV-16`, `PENV-17`, `PENV-18`
10. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDPAYLOADCHUNKRENDERER](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs)
11. `PENV-44`
12. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BIOMEMASKCHUNKRENDERER](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BiomeMaskChunkRenderer.cs)
13. `PENV-42`
14. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-PLACEMENT](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
15. `PENV-19`, `PENV-20`, `PENV-21`
16. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-PALETTES](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
17. `PENV-22`, `PENV-23`, `PENV-24`
18. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEW](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
19. `PENV-11`, `PENV-12`, `PENV-13`, `PENV-14`, `PENV-15`
20. [SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
21. [SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-WALKABILITY](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs)
22. `HPFB-03`, `HPFB-04`, `HPFB-06`
23. [SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-GEOMETRY](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs)
24. `HPFB-05`
25. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALOBSTACLES](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralObstacles.cs)
26. [SCRIPTS-PRESENTATION-PATHFINDING-STATICOBSTACLEHASH](../My%20project/Assets/Scripts/Presentation/Pathfinding/StaticObstacleHash.cs)

### 2. Unit combat, chasing, squads, and movement

Open in this order:

1. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
2. `UCOM-01`, `UCOM-02`
3. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-UPDATELOOP](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
4. `UCOM-03`, `UCOM-11`, `UCOM-12`, `UCOM-13`
5. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-TARGETING](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
6. `UCOM-05`, `UCOM-06`, `UCOM-07`
7. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-FORMATIONFLOW](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)
8. `UCOM-04`, `UCOM-08`
9. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.cs)
10. `UCJS-01`, `UCJS-02`
11. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-BUFFERS](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs)
12. `UCJS-03`
11. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-JOB](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs)
12. `UCJS-04`
13. [SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
14. [SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-MEMBERSHIP](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs)
15. `ESQD-04`
16. [SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-TACTICS](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs)
17. `ESQD-05`
18. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
19. `FFLD-03`
20. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-TILEGRAPH](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
21. `FFLD-06`
22. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-FIELDSTATE](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
23. `FFLD-07`
24. [SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)
25. [SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-OCCUPANCY](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
26. `PMGR-03`
27. [SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-REUSE](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
28. `PMGR-04`
29. [SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)
30. [SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-DISPATCH](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
31. `PQUE-02`
32. [SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-COMPLETION](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)
33. `PQUE-03`
34. [SCRIPTS-PRESENTATION-PATHFINDING-UNITPATHFOLLOWER](../My%20project/Assets/Scripts/Presentation/Pathfinding/UnitPathFollower.cs)
35. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
36. `UVEW-01`, `UVEW-02`
37. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW-MOVEMENT](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs)
38. `UVEW-03`

### 3. Avoidance, overlap, and movement performance

Open in this order:

1. [SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.cs)
2. `LAVO-01`, `LAVO-02`
3. [SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-BUFFERS](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs)
4. `LAVO-03`
5. [SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-JOB](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs)
6. `LAVO-04`
7. [SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
8. `ORCA-01`, `ORCA-02`, `ORCA-03`
9. [SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-BUFFERS](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs)
10. `ORCA-05`
11. [SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-JOB](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs)
12. `ORCA-04`
13. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.cs)
14. `USOA-01`, `USOA-02`
15. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUILD](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs)
16. `USOA-03`
17. [SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUFFERS](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs)
18. `USOA-04`
19. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
20. `FFLD-03`
21. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-COSTMAPS](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
22. `FFLD-04`, `FFLD-05`
23. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-TILEGRAPH](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
24. `FFLD-06`
25. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-FIELDSTATE](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
26. `FFLD-07`
27. [SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
28. [SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-BUFFERS](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs)
29. `MJOB-03`, `MJOB-05`
30. [SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-JOBS](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs)
31. `MJOB-04`
32. [SCRIPTS-PRESENTATION-PERFORMANCE-OCCUPANCYHASH](../My%20project/Assets/Scripts/Presentation/Performance/OccupancyHash.cs)
33. [SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.cs)
34. `STUCK-01`, `STUCK-02`
35. [SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-RECOVERY](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs)
36. `STUCK-03`
37. [SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-STATE](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs)
38. `STUCK-04`
39. [SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)
40. `CROWD-01`, `CROWD-02`, `CROWD-03`
41. [SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-SEARCH](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs)
42. `CROWD-04`
43. [SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-THROTTLE](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs)
44. `CROWD-05`

### 4. Unit visuals, animation, sort order

Open in this order:

1. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
2. `UVEW-01`, `UVEW-02`
3. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW-MOVEMENT](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs)
4. `UVEW-03`
5. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW-RENDERING](../My%20project/Assets/Scripts/Presentation/View/UnitView.Rendering.cs)
6. `UVEW-04`
7. [SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.cs)
8. `USPA-01`
9. [SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-PLAYBACK](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Playback.cs)
10. `USPA-02`
11. [SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-COMBAT](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Combat.cs)
12. `USPA-03`
13. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-STATE](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.State.cs)
14. `UCOM-09`, `UCOM-10`
13. [SCRIPTS-PRESENTATION-VIEW-DIRECTIONALANIMATIONSET](../My%20project/Assets/Scripts/Presentation/View/DirectionalAnimationSet.cs)
14. [SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY](../My%20project/Assets/Scripts/Presentation/View/UnitSortByY.cs)
15. [SCRIPTS-PRESENTATION-VIEW-UNITHPOVERLAY](../My%20project/Assets/Scripts/Presentation/View/UnitHpOverlay.cs)

### 5. Bootstrap, input, UI, and scene wiring

Open in this order:

1. [SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs)
2. [SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-SETUP](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs)
3. [SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-ACTIONS](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs)
4. [SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-PERSISTENCE](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs)
5. [SCRIPTS-PRESENTATION-INPUT-INPUTCONTROLLER](../My%20project/Assets/Scripts/Presentation/Input/InputController.cs)
6. [SCRIPTS-PRESENTATION-INPUT-UNITSPAWNERCOMMANDER](../My%20project/Assets/Scripts/Presentation/Input/UnitSpawnerCommander.cs)
7. [SCRIPTS-PRESENTATION-UI-HUDCONTROLLER](../My%20project/Assets/Scripts/Presentation/UI/HudController.cs)
8. [SCRIPTS-PRESENTATION-UI-HUDCONTROLLER-SQUADS](../My%20project/Assets/Scripts/Presentation/UI/HudController.Squads.cs)
9. [SCRIPTS-PRESENTATION-UI-ACTIONSPANEL](../My%20project/Assets/Scripts/Presentation/UI/ActionsPanel.cs)
10. [SCRIPTS-PRESENTATION-UI-ACTIONSPANEL-SELFTEST](../My%20project/Assets/Scripts/Presentation/UI/ActionsPanel.SelfTest.cs)
11. [SCRIPTS-PRESENTATION-UI-RESEARCHPANEL](../My%20project/Assets/Scripts/Presentation/UI/ResearchPanel.cs)
12. [SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D](../My%20project/Assets/Scripts/Presentation/Camera/CameraZoom2D.cs)

### 6. Save/load and game state

Open in this order:

1. [SCRIPTS-APPLICATION-SERVICES-GAMESTATESERVICE](../My%20project/Assets/Scripts/Application/Services/GameStateService.cs)
2. [SCRIPTS-APPLICATION-USECASES-STARTNEWGAME](../My%20project/Assets/Scripts/Application/UseCases/StartNewGame.cs)
3. [SCRIPTS-APPLICATION-USECASES-SAVEGAME](../My%20project/Assets/Scripts/Application/UseCases/SaveGame.cs)
4. [SCRIPTS-APPLICATION-USECASES-LOADGAME](../My%20project/Assets/Scripts/Application/UseCases/LoadGame.cs)
5. [SCRIPTS-INFRASTRUCTURE-PERSISTENCE-SAVESYSTEM](../My%20project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs)

### 7. Character Creator pipeline

Open in this order:

1. [SCRIPTS-TOOLS-CHARACTERCREATORSIMPLECONFIG](../My%20project/Assets/Scripts/Tools/CharacterCreatorSimpleConfig.cs)
2. [EDITOR-CHARACTERCREATORSIMPLECONFIGEDITOR](../My%20project/Assets/Editor/CharacterCreatorSimpleConfigEditor.cs)
3. [EDITOR-CHARACTERCREATORSCENEBUILDER](../My%20project/Assets/Editor/CharacterCreatorSceneBuilder.cs)
4. [EDITOR-CHARACTERCREATORUNITBUILDER](../My%20project/Assets/Editor/CharacterCreatorUnitBuilder.cs)

## Section IDs For Large Systems

### ProceduralEnvironment

- `PENV-01` - inspector config and runtime state
- `PENV-02` - Unity bootstrap and generation entrypoints
- `PENV-03` - streaming startup in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- `PENV-04` - tilemap parenting between grids in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- `PENV-05` - streaming scheduler in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- `PENV-06` - per-chunk generation entry point in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-07` - background chunk tile synthesis in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-08` - hex-grid props/trees placement in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-09` - background-grid props/trees placement in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-10` - chunk unload and cleanup in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- `PENV-11` - delayed far-view bake start in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-12` - far-view render objects in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-13` - chunked far-view bake in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-14` - far-view HUD / loading overlay in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-15` - bake bounds debug rendering in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- `PENV-16` - background grid sizing in [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
- `PENV-17` - streaming biome masks in [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
- `PENV-18` - background mask coordinate helpers in [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
- `PENV-19` - placement candidate caches, biome-aware filters, and density helpers in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- `PENV-20` - placement building, batching, and tile application in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- `PENV-21` - blocked-cell bookkeeping, spatial hash, and hex distance helpers in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- `PENV-22` - post-placement obstacle rebake and palette routing in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
- `PENV-23` - palette weighting, accent extraction, and selection helpers in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
- `PENV-24` - water/rock biome mask cleanup and smoothing in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)

### UnitCombat

- `UCOM-01` - combat state and tuning
- `UCOM-02` - unit registration / cache init
- `UCOM-03` - main combat update loop in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UCOM-11` - tick gate, timer maintenance, and pending-path normalization in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UCOM-12` - active target engagement, chase/repath, and in-range attack handling in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UCOM-13` - no-target grace branch and stall recovery in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `UCOM-04` - squad metadata assignment in [UnitCombat.FormationFlow.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)
- `UCOM-05` - local nearest-enemy search in [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
- `UCOM-06` - target arbitration logic in [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
- `UCOM-07` - faction/profile overrides in [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
- `UCOM-08` - flow-field steering path in [UnitCombat.FormationFlow.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)

### UnitView

- `UVEW-01` - per-unit state, movement/avoidance/sorting config, and lifecycle membership in [UnitView.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
- `UVEW-02` - destination, steering, velocity-override, and state accessors in [UnitView.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
- `UVEW-03` - per-frame movement integration, steering blend, arrival handling, and facing updates in [UnitView.Movement.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs)
- `UVEW-04` - LateUpdate Y-sorting, CompositionRoot sorting bootstrap, and selection gizmos in [UnitView.Rendering.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Rendering.cs)

### EnemySquadManager

- `ESQD-01` - squad registry, thresholds, and cached squad state in [EnemySquadManager.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
- `ESQD-02` - singleton/bootstrap lifecycle in [EnemySquadManager.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
- `ESQD-03` - main squad orchestration tick in [EnemySquadManager.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
- `ESQD-04` - squad composition, recruitment, and center maintenance in [EnemySquadManager.Membership.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs)
- `ESQD-05` - squad mode hysteresis, target assignment, and flow-anchor updates in [EnemySquadManager.Tactics.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs)

### MovementJobSystem

- `MJOB-01` - movement batch state and scheduling config in [MovementJobSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
- `MJOB-02` - lifecycle setup and persistent buffer ownership in [MovementJobSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
- `MJOB-03` - main movement scheduling tick in [MovementJobSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
- `MJOB-04` - parallel movement integration and destination advance logic in [MovementJobSystem.Jobs.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs)
- `MJOB-05` - unit gathering, Native buffer growth, input fill, and applyback in [MovementJobSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs)

### OrcaAvoidanceSystem

- `ORCA-01` - ORCA config, spatial buffers, and shared NativeCollections in [OrcaAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
- `ORCA-02` - singleton lifecycle and buffer ownership in [OrcaAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
- `ORCA-03` - frame update, agent gather/snapshot selection, and job dispatch in [OrcaAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
- `ORCA-04` - burst-compiled ORCA solver and linear-program helpers in [OrcaAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs)
- `ORCA-05` - unit gathering, Native buffer growth, snapshot ingestion, and applyback in [OrcaAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs)

### UnitSoARegistry

- `USOA-01` - singleton state, inspector toggles, SoA buffers, and snapshot payload structs in [UnitSoARegistry.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.cs)
- `USOA-02` - lifecycle setup, external tick control, and snapshot accessors in [UnitSoARegistry.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.cs)
- `USOA-03` - per-frame SoA snapshot projection from active units in [UnitSoARegistry.Build.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs)
- `USOA-04` - NativeArray capacity growth, disposal, and shared cell projection helpers in [UnitSoARegistry.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs)

### UnitCombatJobScheduler

- `UCJS-01` - scheduler config, double-buffered unit lists, and Native job-buffer ownership in [UnitCombatJobScheduler.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.cs)
- `UCJS-02` - lifecycle setup and per-interval nearest-enemy job scheduling in [UnitCombatJobScheduler.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.cs)
- `UCJS-03` - Native buffer growth, hash fill, snapshot ingestion, and applyback in [UnitCombatJobScheduler.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs)
- `UCJS-04` - burst-compiled nearest-enemy search inside spatial-hash neighborhoods in [UnitCombatJobScheduler.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs)

### PathManager

- `PMGR-01` - build settings, reuse caches, pools, and diagnostics in [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)
- `PMGR-02` - build throttling and per-frame budget checks in [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)
- `PMGR-03` - occupancy-cache maintenance and occupied-path rejection in [PathManager.Occupancy.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
- `PMGR-04` - group-path reuse, nearest-free lookup, and cluster helpers in [PathManager.Reuse.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
- `PMGR-05` - bootstrap discovery, pools, and failure logging in [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)

### FlowFieldManager

- `FFLD-01` - flow-field config, caches, and graph buffers in [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
- `FFLD-02` - lifecycle setup in [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
- `FFLD-03` - per-frame field refresh and request updates in [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
- `FFLD-04` - crowd cost-map sampling in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FFLD-05` - tactical influence-map sampling in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FFLD-06` - coarse tile-graph construction in [FlowFieldManager.TileGraph.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
- `FFLD-07` - per-target flow-field storage and integration state in [FlowFieldManager.FieldState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)

### PathRequestQueue

- `PQUE-01` - async queue state, job buffers, and lifecycle guards in [PathRequestQueue.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)
- `PQUE-02` - queue draining, job scheduling, and immediate fallback dispatch in [PathRequestQueue.Dispatch.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
- `PQUE-03` - async job completion, occupancy snapshots, and callback safety in [PathRequestQueue.Completion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)
- `PQUE-04` - immutable request payload captured at queue time in [PathRequestQueue.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)

### CrowdingResolver

- `CROWD-01` - resolver config, cached dependencies, per-cell groups, and adaptive-throttle state in [CrowdingResolver.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)
- `CROWD-02` - LateUpdate crowd-resolution tick, grouping, and short nudge issue path in [CrowdingResolver.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)
- `CROWD-03` - dependency cache setup, pooled-list cleanup, and move-timestamp maintenance in [CrowdingResolver.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)
- `CROWD-04` - free-cell search, reservation-key helpers, and odd-r ring enumeration in [CrowdingResolver.Search.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs)
- `CROWD-05` - adaptive throttling, effective radius/group budgets, and diagnostic logging in [CrowdingResolver.Throttle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs)

### StuckResolver

- `STUCK-01` - resolver config, singleton ownership, per-unit progress windows, and throttled log state in [StuckResolver.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.cs)
- `STUCK-02` - periodic progress sampling, guards, and recovery dispatch in [StuckResolver.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.cs)
- `STUCK-03` - optional combat repath and nearest-free nudge recovery in [StuckResolver.Recovery.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs)
- `STUCK-04` - per-unit progress reset and stale-entry cleanup in [StuckResolver.State.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs)

### LocalAvoidanceSystem

- `LAVO-01` - legacy local-avoidance config, singleton ownership, and double-buffered runtime state in [LocalAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.cs)
- `LAVO-02` - scheduler tick, ORCA handoff, job completion, and next avoidance-job submission in [LocalAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.cs)
- `LAVO-03` - unit gathering, Native buffer growth/fill, and steering applyback in [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs)
- `LAVO-04` - steering job plus cell/hash helpers in [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs)

### CompositionRoot

- `CROOT-01` - core lifecycle, shared inspector references, and root service ownership in [CompositionRoot.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs)
- `CROOT-02` - bootstrap helpers, singleton wiring, and existing-scene unit setup in [CompositionRoot.Setup.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs)
- `CROOT-03` - save/load wrappers, status text, and debug test actions in [CompositionRoot.Actions.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs)
- `CROOT-04` - save/restore callbacks, restored-unit visuals, and sorting helpers in [CompositionRoot.Persistence.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs)

### HexPathfindingBootstrap

- `HPFB-01` - hex-grid dimensions, walkability storage, and NativeArray mirrors in [HexPathfindingBootstrap.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
- `HPFB-02` - initial grid sizing and one-time bootstrap initialization in [HexPathfindingBootstrap.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
- `HPFB-03` - lazy initialization and reallocation guards in [HexPathfindingBootstrap.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
- `HPFB-04` - walkability mutation, collider baking, persistence, and dirty-rectangle updates in [HexPathfindingBootstrap.Walkability.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs)
- `HPFB-05` - world/grid conversion, gizmos, and geometry snapshots in [HexPathfindingBootstrap.Geometry.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs)
- `HPFB-06` - Native buffer cleanup and job-safe synchronization helpers in [HexPathfindingBootstrap.Walkability.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs)

### Other Large Systems

- `FFLD-*` - shared macro navigation via flow fields
- `PMGR-*` - path caching, reuse, occupancy-aware path builds
- `ORCA-*` - local avoidance job pipeline
- `USOA-*` - shared structure-of-arrays snapshot pipeline
- `ESQD-*` - squad grouping and squad-state transitions
- `HPFB-*` - hex grid bootstrap and walkability storage
- `MJOB-*` - unit movement job batching
- `LAVO-*` - legacy local-avoidance scheduling, buffers, and steering job
- `PQUE-*` - async path request queue
- `CROWD-*` - crowd-pressure relief, free-cell search, and adaptive throttling
- `STUCK-*` - stuck detection, recovery nudges, and stale-state cleanup

## File Index By Layer

### Application

- `SCRIPTS-APPLICATION-SERVICES-GAMESTATESERVICE` - runtime container for economy and research state
- `SCRIPTS-APPLICATION-SERVICES-ISAVESYSTEM` - save/load interface abstraction
- `SCRIPTS-APPLICATION-USECASES-STARTNEWGAME` - initializes a fresh game state
- `SCRIPTS-APPLICATION-USECASES-SAVEGAME` - serializes current state through persistence
- `SCRIPTS-APPLICATION-USECASES-LOADGAME` - restores saved state into runtime services
- `SCRIPTS-APPLICATION-USECASES-PLACEBUILDING` - building placement orchestration
- `SCRIPTS-APPLICATION-USECASES-STARTRESEARCH` - research start orchestration
- `SCRIPTS-APPLICATION-USECASES-COMPLETERESEARCH` - research completion orchestration

### Domain

- `SCRIPTS-DOMAIN-BUILD-BUILDINGSERVICE` - domain rules for building placement/build result
- `SCRIPTS-DOMAIN-BUILD-BUILDRESULT` - result payload for building operations
- `SCRIPTS-DOMAIN-COMBAT-COMBATSIMULATOR` - pure combat resolution helper
- `SCRIPTS-DOMAIN-COMBAT-DAMAGETYPE` - combat damage classification
- `SCRIPTS-DOMAIN-ECONOMY-ECONOMYMANAGER` - economy mutations and resource operations
- `SCRIPTS-DOMAIN-ECONOMY-ECONOMYSTATE` - stored economy values
- `SCRIPTS-DOMAIN-ECONOMY-RESOURCEAMOUNT` - typed resource amount value object
- `SCRIPTS-DOMAIN-ECONOMY-RESOURCETYPE` - resource enum
- `SCRIPTS-DOMAIN-RESEARCH-RESEARCHSTORE` - research progression state
- `SCRIPTS-DOMAIN-RESEARCH-RESEARCHSTATUS` - research lifecycle enum
- `SCRIPTS-DOMAIN-RESEARCH-RESEARCHSTARTRESULT` - result payload for starting research
- `SCRIPTS-DOMAIN-UNITS-FACTION` - unit side enum
- `SCRIPTS-DOMAIN-UNITS-UNITSTATS` - unit data payload

### Infrastructure

- `SCRIPTS-INFRASTRUCTURE-AI-PATHFINDING-IGRIDPATHFINDER` - grid pathfinder abstraction
- `SCRIPTS-INFRASTRUCTURE-AI-PATHFINDING-GRIDPATHFINDER` - rectangular-grid pathfinding implementation
- `SCRIPTS-INFRASTRUCTURE-AI-PATHFINDING-HEXPATHFINDER` - hex-grid pathfinding implementation
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-GAMECONFIG` - global startup config asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-BUILDINGCONFIG` - building config asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-RESEARCHCONFIG` - research config asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-RESOURCECONFIG` - resource config asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-UNITCONFIG` - unit config asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-UNITCOMBATPROFILE` - combat tuning profile asset
- `SCRIPTS-INFRASTRUCTURE-CONFIGS-UNITBEHAVIORPROFILE` - behavior tuning profile asset
- `SCRIPTS-INFRASTRUCTURE-PERSISTENCE-SAVESYSTEM` - JSON persistence backend

### Presentation / Bootstrap / Camera / Input / UI

- `SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT` - scene bootstrap and singleton setup
- `SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-SETUP` - extracted bootstrap helpers, auto-created scene systems, and existing-unit setup
- `SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-ACTIONS` - save/load wrappers, status text, and debug test actions extracted from `CompositionRoot`
- `SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-PERSISTENCE` - unit snapshot capture/restore and faction visual helpers extracted from `CompositionRoot`
- `SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D` - camera zoom and pan logic
- `SCRIPTS-PRESENTATION-INPUT-INPUTCONTROLLER` - runtime player input router
- `SCRIPTS-PRESENTATION-INPUT-UNITSPAWNERCOMMANDER` - unit spawning and move commands
- `SCRIPTS-PRESENTATION-UI-HUDCONTROLLER` - top-left HUD and resources panel
- `SCRIPTS-PRESENTATION-UI-HUDCONTROLLER-SQUADS` - squad summary aggregation and squad-selection strip extracted from `HudController`
- `SCRIPTS-PRESENTATION-UI-ACTIONSPANEL` - dev actions and spawn/resource controls
- `SCRIPTS-PRESENTATION-UI-ACTIONSPANEL-SELFTEST` - deterministic save/load self-test extracted from `ActionsPanel`
- `SCRIPTS-PRESENTATION-UI-RESEARCHPANEL` - research selection UI

### Presentation / View

- `SCRIPTS-PRESENTATION-VIEW-UNITVIEW` - core unit movement/view component
- `SCRIPTS-PRESENTATION-VIEW-UNITVIEW-MOVEMENT` - per-frame movement integration and facing extracted from `UnitView`
- `SCRIPTS-PRESENTATION-VIEW-UNITVIEW-RENDERING` - Y-sorting and gizmo helpers extracted from `UnitView`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT` - combat state, tuning, and lifecycle wiring
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-UPDATELOOP` - combat tick orchestration, engage/no-target branching, and stall recovery extracted from `UnitCombat`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-TARGETING` - target arbitration, faction overrides, facing, crouch, and repath helper extraction from `UnitCombat`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-FORMATIONFLOW` - squad metadata, formation math, shared hex access, and flow-field steering extraction from `UnitCombat`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-STATE` - damage/health mutation, forced-target control, and profile application extracted from `UnitCombat`
- `SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR` - directional sprite animation state machine
- `SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-PLAYBACK` - per-frame directional animation playback
- `SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR-COMBAT` - combat-driven attack/death animation hooks
- `USPA-01` - animator state, renderer wiring, and event subscription lifecycle
- `USPA-02` - per-frame playback and locomotion/crouch state machine
- `USPA-03` - attack/death animation triggers and external crouch requests
- `SCRIPTS-PRESENTATION-VIEW-DIRECTIONALANIMATIONSET` - animation frames container
- `SCRIPTS-PRESENTATION-VIEW-MOVEMENTSETTINGS` - movement defaults/tuning container
- `SCRIPTS-PRESENTATION-VIEW-UNITHPOVERLAY` - health bar visuals
- `SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY` - Y-based sorting helper

### Presentation / Pathfinding

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT` - map generation, streaming, biomes, far-view bake
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMING` - streaming bootstrap, chunk queueing, and camera-driven scheduler extraction from `ProceduralEnvironment`
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMCHUNKS` - per-chunk generation, streamed props placement, biome-aware background fill, and chunk cleanup extraction from `ProceduralEnvironment`
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-LIFECYCLE` - MonoBehaviour lifecycle, generation entrypoints, prep/bootstrap, and grid/tilemap setup extracted from `ProceduralEnvironment`
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEW` - far-view bake scheduling, render objects, chunked bake pipeline, HUD, loading overlay, bounds debug, and renderer-state helpers
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDMASKS` - background-grid sizing, water/rock masks, land-distance fields, and background-cell mask access extracted from `ProceduralEnvironment`
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALOBSTACLES` - obstacle auto-placement and collider generation
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER` - high-level path planning and cache reuse
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-OCCUPANCY` - occupancy caches and occupied-path rejection extracted from `PathManager`
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-REUSE` - path reuse, nearest-free lookup, and cluster helpers extracted from `PathManager`
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE` - async path job queue
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-DISPATCH` - queue draining, job scheduling, and immediate fallback dispatch extracted from `PathRequestQueue`
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-COMPLETION` - async job completion, occupancy snapshots, stats, and safe callback/log handling extracted from `PathRequestQueue`
- `SCRIPTS-PRESENTATION-PATHFINDING-UNITPATHFOLLOWER` - runtime path execution for a unit
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER` - shared macro navigation fields
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-COSTMAPS` - crowd and influence cost-map maintenance extracted from `FlowFieldManager`
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-TILEGRAPH` - coarse tile-graph construction and tile-path expansion extracted from `FlowFieldManager`
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-FIELDSTATE` - per-target field storage, LoS state, and next-cell sampling extracted from `FlowFieldManager`
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP` - hex grid and walkability data source
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-WALKABILITY` - walkability mutation, collider rebake, persistence, and Native buffer maintenance extracted from `HexPathfindingBootstrap`
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP-GEOMETRY` - world/grid conversion, gizmos, and grid geometry helpers extracted from `HexPathfindingBootstrap`
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDERJOB` - jobified hex path build
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHFINDINGBOOTSTRAP` - non-hex/grid pathfinding bootstrap
- `SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER` - crowd pressure and local spacing helper
- `SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-SEARCH` - free-cell search, reservation keys, and ring enumeration extracted from `CrowdingResolver`
- `SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-THROTTLE` - adaptive-throttling, effective work-budget, and diagnostic logging extracted from `CrowdingResolver`
- `SCRIPTS-PRESENTATION-PATHFINDING-COVERSLOTHASH` - cached cover-slot spatial index
- `SCRIPTS-PRESENTATION-PATHFINDING-STATICOBSTACLEHASH` - cached static obstacle hash
- `SCRIPTS-PRESENTATION-PATHFINDING-ROCKOBSTACLEMETA` - metadata for rock blockers
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXTERRAINRULESET` - terrain ruleset asset/runtime types
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHPROFILER` - pathfinding diagnostics aggregation
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHDEBUGHUD` - pathfinding diagnostics HUD

### Presentation / Performance

- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM` - ORCA avoidance pipeline
- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-BUFFERS` - unit gathering, Native buffer growth, snapshot ingestion, and applyback extracted from `OrcaAvoidanceSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM-JOB` - burst-compiled ORCA solver extracted from `OrcaAvoidanceSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY` - shared structure-of-arrays snapshot root for ORCA and combat systems
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUILD` - per-frame snapshot build extracted from `UnitSoARegistry`
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY-BUFFERS` - Native buffer ownership and capacity helpers extracted from `UnitSoARegistry`
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM` - movement batching and movement jobs
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-BUFFERS` - unit gathering, Native buffer management, and applyback extracted from `MovementJobSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM-JOBS` - parallel movement integration extracted from `MovementJobSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER` - squad grouping and anchor management
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-MEMBERSHIP` - squad composition, member recruitment, and center maintenance extracted from `EnemySquadManager`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER-TACTICS` - squad state transitions, target assignment, and flow-anchor logic extracted from `EnemySquadManager`
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER` - nearest-target combat job scheduler
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-BUFFERS` - Native buffer growth, snapshot ingestion, and applyback extracted from `UnitCombatJobScheduler`
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER-JOB` - burst-compiled nearest-enemy search job extracted from `UnitCombatJobScheduler`
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY` - SoA snapshot of active units
- `SCRIPTS-PRESENTATION-PERFORMANCE-OCCUPANCYHASH` - dynamic nearest-enemy occupancy queries
- `SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM` - legacy local avoidance system
- `SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-BUFFERS` - unit gathering, Native buffer growth/fill, and steering applyback extracted from `LocalAvoidanceSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM-JOB` - steering job and cell/hash helpers extracted from `LocalAvoidanceSystem`
- `SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER` - stuck detection / recovery
- `SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-RECOVERY` - optional combat repath and nearest-free nudge helpers extracted from `StuckResolver`
- `SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-STATE` - progress reset and stale-entry cleanup extracted from `StuckResolver`
- `SCRIPTS-PRESENTATION-PERFORMANCE-JOBPIPELINECOORDINATOR` - update ordering for runtime jobs
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITVISUALCULLING` - sprite visibility culling

### Tools / Editor

- `SCRIPTS-TOOLS-CHARACTERCREATORSIMPLECONFIG` - simplified Character Creator runtime config bridge
- `EDITOR-CHARACTERCREATORSCENEBUILDER` - builds the simplified Character Creator scene
- `EDITOR-CHARACTERCREATORSIMPLECONFIGEDITOR` - inspector buttons for the simple config
- `EDITOR-CHARACTERCREATORUNITBUILDER` - generates animation sets from Character Creator output
- `EDITOR-SPRITEIMPORTTOOLS` - sprite import utility from repo folders
- `EDITOR-RTSSETUP` - one-click RTS setup utilities and prefab/config creation

### Tests

- `TESTS-EDITMODE-UNITCOMBATTARGETINGTESTS` - target selection logic tests
- `TESTS-PLAYMODE-COMBATPATHRESETTESTS` - combat path reset regression tests
- `TESTS-PLAYMODE-FPSSTRESSTESTS` - performance stress harness, including `[CombatPressureProbe]` 100v100 owner-target diagnostics
- `TESTS-PLAYMODE-SAMPLESCENEBOOTSMOKETESTS` - `SampleScene` boot smoke test for required runtime systems
- `TESTS-PLAYMODE-SAMPLESCENERENDERERDIAGNOSTICSTESTS` - explicit `SampleScene` renderer/streaming baseline diagnostic that logs `[SampleSceneRendererProbe]` for extractor-based budget checks
- `TESTS-PLAYMODE-UNITCOMBATSTALLTESTS` - combat stall regression tests

## Practical Rule

When working in a large area, reference both levels:

- file ID for the module
- short section ID for the exact subsystem inside it

Example:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT + PENV-09`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT-UPDATELOOP + UCOM-03`
