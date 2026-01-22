# Code ID Index

This document is the practical lookup table for the `CODE-ID` comments added to the project.
Use it together with [code_map.md](./code_map.md).
If the task starts from a bug symptom, open [debug_playbooks.md](./debug_playbooks.md) first.
If the task starts from a tuning parameter, open [runtime_switches.md](./runtime_switches.md) first.
If the task starts from live Unity scene/editor state, open [unity_mcp_tools.md](./unity_mcp_tools.md) first.
If the task starts from repo layout or ownership, open [repo_map.md](../maps/repo_map.md) first.

## How To Use

- If the task is broad, start from a file-level `CODE-ID`.
- If the task is in a large runtime system, jump to the short internal section IDs.
- In discussion, prefer `ID + short description` instead of vague references like "that pathfinding part".

Examples:

- `PENV-05` = streaming scheduler in [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
- `UCOM-03` = main combat tick in [UnitCombat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER` = shared path manager file in [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)

## Reading Routes

### 1. World generation, streaming, far-view bake

Open in this order:

1. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
2. `PENV-03`, `PENV-05`, `PENV-06`, `PENV-07`, `PENV-09`, `PENV-13`
3. [SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
4. `HPFB-03`, `HPFB-04`
5. [SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALOBSTACLES](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralObstacles.cs)
6. [SCRIPTS-PRESENTATION-PATHFINDING-STATICOBSTACLEHASH](../My%20project/Assets/Scripts/Presentation/Pathfinding/StaticObstacleHash.cs)

### 2. Unit combat, chasing, squads, and movement

Open in this order:

1. [SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
2. `UCOM-03`, `UCOM-06`, `UCOM-08`
3. [SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
4. `ESQD-04`, `ESQD-05`
5. [SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
6. `FFLD-03`, `FFLD-06`, `FFLD-07`
7. [SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)
8. [SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)
9. [SCRIPTS-PRESENTATION-PATHFINDING-UNITPATHFOLLOWER](../My%20project/Assets/Scripts/Presentation/Pathfinding/UnitPathFollower.cs)
10. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)

### 3. Avoidance, overlap, and movement performance

Open in this order:

1. [SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
2. `ORCA-03`, `ORCA-04`
3. [SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.cs)
4. `MJOB-03`, `MJOB-04`
5. [SCRIPTS-PRESENTATION-PERFORMANCE-OCCUPANCYHASH](../My%20project/Assets/Scripts/Presentation/Performance/OccupancyHash.cs)
6. [SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.cs)
7. [SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.cs)

### 4. Unit visuals, animation, sort order

Open in this order:

1. [SCRIPTS-PRESENTATION-VIEW-UNITVIEW](../My%20project/Assets/Scripts/Presentation/View/UnitView.cs)
2. [SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.cs)
3. [SCRIPTS-PRESENTATION-VIEW-DIRECTIONALANIMATIONSET](../My%20project/Assets/Scripts/Presentation/View/DirectionalAnimationSet.cs)
4. [SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY](../My%20project/Assets/Scripts/Presentation/View/UnitSortByY.cs)
5. [SCRIPTS-PRESENTATION-VIEW-UNITHPOVERLAY](../My%20project/Assets/Scripts/Presentation/View/UnitHpOverlay.cs)

### 5. Bootstrap, input, UI, and scene wiring

Open in this order:

1. [SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs)
2. [SCRIPTS-PRESENTATION-INPUT-INPUTCONTROLLER](../My%20project/Assets/Scripts/Presentation/Input/InputController.cs)
3. [SCRIPTS-PRESENTATION-INPUT-UNITSPAWNERCOMMANDER](../My%20project/Assets/Scripts/Presentation/Input/UnitSpawnerCommander.cs)
4. [SCRIPTS-PRESENTATION-UI-HUDCONTROLLER](../My%20project/Assets/Scripts/Presentation/UI/HudController.cs)
5. [SCRIPTS-PRESENTATION-UI-ACTIONSPANEL](../My%20project/Assets/Scripts/Presentation/UI/ActionsPanel.cs)
6. [SCRIPTS-PRESENTATION-UI-RESEARCHPANEL](../My%20project/Assets/Scripts/Presentation/UI/ResearchPanel.cs)
7. [SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D](../My%20project/Assets/Scripts/Presentation/Camera/CameraZoom2D.cs)

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
2. [EDITOR-CHARACTERCREATORSIMPLECONFIGEDITOR](../My%20project/Assets/Scripts/Editor/CharacterCreatorSimpleConfigEditor.cs)
3. [EDITOR-CHARACTERCREATORSCENEBUILDER](../My%20project/Assets/Scripts/Editor/CharacterCreatorSceneBuilder.cs)
4. [EDITOR-CHARACTERCREATORUNITBUILDER](../My%20project/Assets/Scripts/Editor/CharacterCreatorUnitBuilder.cs)

## Section IDs For Large Systems

### ProceduralEnvironment

- `PENV-01` - inspector config and runtime state
- `PENV-02` - Unity bootstrap
- `PENV-03` - streaming startup
- `PENV-04` - tilemap parenting between grids
- `PENV-05` - streaming scheduler
- `PENV-06` - per-chunk generation entry point
- `PENV-07` - background chunk tile synthesis
- `PENV-08` - hex-grid props/trees placement
- `PENV-09` - background-grid props/trees placement
- `PENV-10` - chunk unload and cleanup
- `PENV-11` - delayed far-view bake start
- `PENV-12` - far-view render objects
- `PENV-13` - chunked far-view bake
- `PENV-14` - far-view HUD / loading overlay
- `PENV-15` - bake bounds debug rendering
- `PENV-16` - background grid sizing
- `PENV-17` - streaming biome masks
- `PENV-18` - background mask coordinate helpers

### UnitCombat

- `UCOM-01` - combat state and tuning
- `UCOM-02` - unit registration / cache init
- `UCOM-03` - main combat update loop
- `UCOM-04` - squad metadata assignment
- `UCOM-05` - local nearest-enemy search
- `UCOM-06` - target arbitration logic
- `UCOM-07` - faction/profile overrides
- `UCOM-08` - flow-field steering path

### Other Large Systems

- `FFLD-*` - shared macro navigation via flow fields
- `PMGR-*` - path caching, reuse, occupancy-aware path builds
- `ORCA-*` - local avoidance job pipeline
- `ESQD-*` - squad grouping and squad-state transitions
- `HPFB-*` - hex grid bootstrap and walkability storage
- `MJOB-*` - unit movement job batching
- `PQUE-*` - async path request queue

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
- `SCRIPTS-PRESENTATION-CAMERA-CAMERAZOOM2D` - camera zoom and pan logic
- `SCRIPTS-PRESENTATION-INPUT-INPUTCONTROLLER` - runtime player input router
- `SCRIPTS-PRESENTATION-INPUT-UNITSPAWNERCOMMANDER` - unit spawning and move commands
- `SCRIPTS-PRESENTATION-UI-HUDCONTROLLER` - top-left HUD and resources panel
- `SCRIPTS-PRESENTATION-UI-ACTIONSPANEL` - squad panel and action buttons
- `SCRIPTS-PRESENTATION-UI-RESEARCHPANEL` - research selection UI

### Presentation / View

- `SCRIPTS-PRESENTATION-VIEW-UNITVIEW` - core unit movement/view component
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT` - combat brain and chase/attack behavior
- `SCRIPTS-PRESENTATION-VIEW-UNITSPRITEANIMATOR` - directional sprite animation state machine
- `SCRIPTS-PRESENTATION-VIEW-DIRECTIONALANIMATIONSET` - animation frames container
- `SCRIPTS-PRESENTATION-VIEW-MOVEMENTSETTINGS` - movement defaults/tuning container
- `SCRIPTS-PRESENTATION-VIEW-UNITHPOVERLAY` - health bar visuals
- `SCRIPTS-PRESENTATION-VIEW-UNITSORTBYY` - Y-based sorting helper

### Presentation / Pathfinding

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT` - map generation, streaming, biomes, far-view bake
- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALOBSTACLES` - obstacle auto-placement and collider generation
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER` - high-level path planning and cache reuse
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE` - async path job queue
- `SCRIPTS-PRESENTATION-PATHFINDING-UNITPATHFOLLOWER` - runtime path execution for a unit
- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER` - shared macro navigation fields
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP` - hex grid and walkability data source
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDERJOB` - jobified hex path build
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHFINDINGBOOTSTRAP` - non-hex/grid pathfinding bootstrap
- `SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER` - crowd pressure and local spacing helper
- `SCRIPTS-PRESENTATION-PATHFINDING-COVERSLOTHASH` - cached cover-slot spatial index
- `SCRIPTS-PRESENTATION-PATHFINDING-STATICOBSTACLEHASH` - cached static obstacle hash
- `SCRIPTS-PRESENTATION-PATHFINDING-ROCKOBSTACLEMETA` - metadata for rock blockers
- `SCRIPTS-PRESENTATION-PATHFINDING-HEXTERRAINRULESET` - terrain ruleset asset/runtime types
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHPROFILER` - pathfinding diagnostics aggregation
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHDEBUGHUD` - pathfinding diagnostics HUD

### Presentation / Performance

- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM` - ORCA avoidance pipeline
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM` - movement batching and movement jobs
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER` - squad grouping and anchor management
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITCOMBATJOBSCHEDULER` - nearest-target combat job scheduler
- `SCRIPTS-PRESENTATION-PERFORMANCE-UNITSOAREGISTRY` - SoA snapshot of active units
- `SCRIPTS-PRESENTATION-PERFORMANCE-OCCUPANCYHASH` - dynamic nearest-enemy occupancy queries
- `SCRIPTS-PRESENTATION-PERFORMANCE-LOCALAVOIDANCESYSTEM` - legacy local avoidance system
- `SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER` - stuck detection / recovery
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
- `TESTS-PLAYMODE-FPSSTRESSTESTS` - performance stress harness
- `TESTS-PLAYMODE-UNITCOMBATSTALLTESTS` - combat stall regression tests

## Practical Rule

When working in a large area, reference both levels:

- file ID for the module
- short section ID for the exact subsystem inside it

Example:

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT + PENV-09`
- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT + UCOM-03`
