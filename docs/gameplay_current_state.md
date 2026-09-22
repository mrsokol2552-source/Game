# Current Game State (RTS 2D)

## Navigation helpers
- [code_map.md](./code_map.md) - architecture map and key system locations.
- [code_id_index.md](./code_id_index.md) - practical `CODE-ID` lookup and reading routes.
- [debug_playbooks.md](./debug_playbooks.md) - symptom -> where to look first.
- [runtime_switches.md](./runtime_switches.md) - high-value inspector/runtime tuning switches.
- [document_roles.md](./document_roles.md) - role boundaries between the main docs.
- [unity_csharp_performance_optimization_reference.md](./unity_csharp_performance_optimization_reference.md) - practical performance reference for Unity C# code.
- [deep-research-report.md](./deep-research-report.md) - agent-focused repository navigation reference and rationale.
- [code_index.json](./code_index.json) - machine-readable system index.
- [machine_layer_audit.md](./machine_layer_audit.md) - machine-layer ownership, invariants, and validation state.
- [runtime_config_export.json](./runtime_config_export.json) - generated scene/prefab serialized runtime-config snapshot.
- [unity_mcp_tools.md](./unity_mcp_tools.md) - available Unity MCP tools and recommended usage.
- [repo_map.md](../maps/repo_map.md) - compact top-level repo map.
- [repo_map.json](../maps/repo_map.json) - machine-readable repo map for tools/agents.
- [agent_comment_standard.md](./agent_comment_standard.md) - current file-header convention for hot code.
- [../supplements/README.md](../supplements/README.md) - add-on design and audio reference material.
- [../supplements/design/gdd_match3_survival_siege.md](../supplements/design/gdd_match3_survival_siege.md) - canonical game design document for the "Match-3 Colony Siege" pivot.
- [supplements_index.json](../supplements/supplements_index.json) - machine-readable supplement index.

## Current Engineering Direction

### Strategic Pivot: «Колония: Осада» (Match-3 Colony Survival)

The project has officially pivoted from the unconstrained procedural 2D RTS into a commercially focused WebGL hybrid: **«Колония: Осада» (Match-3 Colony Survival)**, fully specified in master GDD [../supplements/design/gdd_match3_survival_siege.md](../supplements/design/gdd_match3_survival_siege.md).

Key architectural tenets of the pivot:
- **Canonical Viewport**: Single 9:16 portrait layout across both mobile and desktop (top ~44–46% arena, bottom ~54–56% 7x7 board). On desktop, this vertical viewport is centered with letterbox statistics/backgrounds to ensure identical gameplay balance and muscle memory.
- **Pacing**: Move-limited Day preparation (15–25 moves) paired with a high-intensity, real-time 60–90 second Night siege (continuous swarm, 200–350 ms board animation budget with input buffering, combat simulation never locks).
- **Cognitive Clarity**: 5 persistent color families (Red = Firepower, Blue = Defense/Wall, Yellow = Tech/Stun, Green = Meds/Recruitment, Purple = Tactical/Sniper).
- **Decoupled Architecture**: Pure C# `Match3BoardModel` communicates via events to `Match3CombatBridge`, completely decoupled from underlying `UnitCombat` and `FlowFieldManager` implementations.
- **Stage 1 Minimal Viable Slice (MVS)**: 1 arena, 1 wall, 1 rifle squad, 5 token colors, 3 enemy archetypes (Walker, Runner, Brute), 3 power-up types, 1 90s Night + 1 short Day, early `IPlatformServices` contract for Yandex SDK. Target economics: ~30,000 ₽/mo (~1,500 DAU).

### Match-3 Development Roadmap & Current State

- **Stage 1: Match3BoardModel v0.1 Core Domain (COMPLETED & HARDENED)**:
  - Pure C# assembly `Match3.asmdef` with `noEngineReferences: true` (zero coupling to Unity Engine, MonoBehaviour, or RTS).
  - Deterministic PCG32 pseudo-random number generator (`Pcg32Match3Random`) with rejection sampling and verified golden vector for persistence (`pcg32-v1`).
  - Deep immutability for `BoardResolution`, `ResolutionStep`, and `MatchGroup` with `ReadOnlyCollection<T>`.
  - Non-mutating `HasAnyLegalMove()` preserving board state and PRNG state.
  - Bounded loops for `AutoShuffle()` and initial generation with deterministic emergency fallback.
  - Hard cascade limit `MaxCascadeDepth = 32` with deterministic stabilization and `MaxCascadeDepthExceeded` flag.
  - Coordinate convention: `(0, 0)` is bottom-left, X right, Y up, gravity downwards.
  - Tests: 30 unit, property, and fuzz tests all green in EditMode.
- **Stage 2: Match3BoardModel v0.2 Special Resolution (COMPLETED & FROZEN)**:
  - Precedence: `Intersection (Dynamite) > Line5Plus (Airstrike) > Line4 (Rocket) > Line3 (None)`. Exactly 1 special per merged group.
  - Deterministic anchor selection: Intersection point > Destination cell B > Source cell A > lowest Y > lowest X.
  - Pre-gravity chain reaction resolver: all triggered specials enqueued into `Queue<BoardPosition>`, visited set prevents re-triggering within the step, board mutated once.
  - Newly spawned specials at anchor participate in same-step chain reactions if struck by explosions.
  - Full special swap matrix verified:
    - Rocket + Rocket (cross)
    - Rocket + Dynamite (3 rows + 3 columns centered at destination)
    - Dynamite + Dynamite (5x5 Chebyshev distance <= 2)
    - Airstrike + Airstrike (full 49-cell board clear)
    - Airstrike + RocketHorizontal (transforms target color, detonates alternating rockets)
    - Airstrike + RocketVertical (transforms target color, detonates alternating rockets)
    - Airstrike + Dynamite (transforms target color, detonates all 3x3 dynamites)
    - Single special swap (destination activation with source cell survival)
  - Rich `ClearedTile` snapshots with first-cause-wins semantics: `ClearCause = первая детерминированная причина, добавившая позицию в ClearSet`, while `SpecialActivation` retains full geometric impact area.
  - Canonical ordering: every collection sorted by Y ascending then X ascending.
  - Authoritative Unity Test Runner batchmode run verified:
    - **Match-3 C# Unity EditMode Suite: 55 passed, 0 failed** (Total repository EditMode suite: 62 passed, 0 failed).
    - Fuzz invariants: 10,000 board generations + 500 special swap simulations pass with zero residual matches and valid legal moves.
  - Mathematical core frozen.
- **Stage 3: v0.3 Match3 Presentation (NEXT MILESTONE)**:
  - Architecture: `Match3InputController` -> `Match3BoardController` -> `Match3BoardModel` -> `BoardResolution` -> `Match3ResolutionPlayer` -> `Match3BoardView`.
  - Core principle: `BoardView` decides nothing and maintains zero game state; it purely receives and visualizes calculated `BoardResolution`.
  - Timing & responsivity:
    - `0 ms`: `TrySwap()` execution.
    - `<1 ms`: Complete deterministic logical resolution.
    - `0–60 ms`: Swap animation playback.
    - `60–130 ms`: Match & special activation explosions.
    - `130–220 ms`: Gravity drop & refill cascade.
    - `~220 ms`: Input unlocked (board visually reaches new domain state).
    - `220–350 ms`: Lingering VFX, particle trails, audio tails.
  - Development integrity guard: `AssertViewMatchesModel()` checking all 49 cells (`occupied`, `color`, `special`, `position`) against domain model snapshot after each completed `BoardResolution`.

### Legacy RTS Engine Maintenance Status
This is the preserved canonical technical reference for the earlier large rendering and world-generation work:

Before any shader migration, the project must first go through code cleanup and decomposition.

Reason:

- the current rendering/generation stack is still too entangled, especially in [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
- moving directly to shaders now would stack a new rendering architecture on top of already mixed responsibilities
- cleanup first will reduce migration risk, make profiling more trustworthy, and create proper seams for GPU-oriented rendering

Mandatory order for the next major technical phase:

1. Decompose large files and remove mixed responsibilities.
2. Apply the existing C#/Unity performance and maintainability rules from [unity_csharp_performance_optimization_reference.md](./unity_csharp_performance_optimization_reference.md) while refactoring.
3. Introduce a clean data extraction layer for generated world chunks.
4. Only after that, start the rendering migration in this order:
   - ground chunk shader renderer
   - water/rock mask shader
   - props/trees instancing
   - removal or radical simplification of the far-view bake pipeline
   - later LOD / indirect draws / deeper GPU culling

Important rule:

- shaders must replace the current rendering layer, not the generation logic itself
- gameplay/navigation/combat data stays CPU-side unless there is a separate, justified migration plan

Primary cleanup targets before shader work:

- [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
- [UnitCombat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
- [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
- [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs)

For `ProceduralEnvironment`, the intended future split should at minimum separate:

- generation data and biome masks
- background/ground rendering preparation
- props/trees placement
- streaming lifecycle and chunk ownership
- far-view bake and far-view HUD/debug

Current refactor progress:

- far-view HUD/debug helpers already live in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- far-view bake scheduling, render-object setup, and chunked bake coroutine now also live in [ProceduralEnvironment.FarView.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs)
- far-view chunk sync bake, chunk texture/sprite lifecycle, GPU readback polling, and bake counters now live in [ProceduralEnvironment.FarViewChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewChunks.cs)
- far-view render texture lifecycle, renderer collection, capture camera/bounds helpers, threshold checks, loading visibility, and baked/base-map state toggles now live in [ProceduralEnvironment.FarViewState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewState.cs)
- MonoBehaviour lifecycle, async/sync generation entrypoints, prepare/bootstrap, and grid/tilemap setup now live in [ProceduralEnvironment.Lifecycle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs)
- low-level tilemap creation, bulk ground fill, sorting-layer lookup, grid sizing, and obstacle collider/layer utilities now live in [ProceduralEnvironment.TilemapUtilities.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TilemapUtilities.cs)
- streaming bootstrap and scheduler helpers already live in [ProceduralEnvironment.Streaming.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs)
- streamed chunk generation, streamed background synthesis, props/trees placement, and chunk cleanup now live in [ProceduralEnvironment.StreamChunks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs)
- background-grid sizing, streamed biome masks, land-distance fields, and background-cell lookup helpers now live in [ProceduralEnvironment.BackgroundMasks.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs)
- biome-aware prop/tree placement, blocked-cell bookkeeping, and spatial placement helpers now live in [ProceduralEnvironment.Placement.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs)
- non-stream and generated streaming prop/blocker placement payload caching plus chunk snapshot extraction now live in [ProceduralEnvironment.PlacementRenderSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.PlacementRenderSource.cs), and far-view props/blockers treat committed/generated placement payloads as authoritative before falling back to live tilemap reads only when no placement payload source exists
- palette resolution, tile lookup/exclusion helpers, biome-mask cleanup, and post-placement obstacle baking helpers now live in [ProceduralEnvironment.Palettes.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs)
- chunk-scoped world-data extraction, typed background-biome reads, and render-independent streaming chunk snapshots now live in [ProceduralEnvironment.WorldData.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.WorldData.cs)
- typed far-view chunk snapshots for `ground`, `transitions`, `props`, and `blockers` tilemaps now also live in [ProceduralEnvironment.WorldData.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.WorldData.cs), carry extracted per-cell world bounds/tint plus an explicit `TilemapLayerSnapshotMetadata` copy of grid/tilemap/renderer metadata, and are routed through [ProceduralEnvironment.FarViewSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewSource.cs); direct live tilemap snapshot/bounds reads are explicitly isolated behind `LegacyTilemap` fallback helpers
- terrain ruleset preparation, auto-ruleset construction/cache/hash, and shared/biome tile collection now live in [ProceduralEnvironment.Ruleset.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Ruleset.cs)
- terrain layer-index generation, domain-warp/macro-biome sampling, smoothing, region cleanup, and layer quantization now live in [ProceduralEnvironment.TerrainData.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TerrainData.cs)
- tile-selection data primitives, edge cache construction, deterministic tile picking, terrain neighbor masks, and ground tile override type now live in [ProceduralEnvironment.TileData.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileData.cs)
- ground tile override application, tile variant construction, shared-tile gating, anti-repeat/water-edge constrained variant picking, and water mismatch scoring now live in [ProceduralEnvironment.TileSelection.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileSelection.cs)
- sprite-derived edge/water profile extraction, profile rotation/mirroring, water mask helpers, and water-interior scoring now live in [ProceduralEnvironment.TileProfile.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileProfile.cs)
- background-render setup extraction for sync/coroutine ruleset application now lives in [ProceduralEnvironment.BackgroundRenderData.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderData.cs)
- background ruleset application, tile selection loops, water-edge refinement, and tilemap writeback now live in [ProceduralEnvironment.BackgroundRender.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRender.cs)
- typed per-cell background render decisions, streaming background payload capture, and payload-to-writeback helpers now live in [ProceduralEnvironment.BackgroundRenderPayload.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderPayload.cs), and an off-by-default experimental chunked sprite-mesh renderer now mirrors cached and streamed background payloads through [ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs) plus the hidden sprite texture shader path
- ground ruleset orchestration and sync/coroutine writeback flow now live in [ProceduralEnvironment.GroundRender.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRender.cs), typed per-cell ground and transition render decisions plus shared writeback helpers now live in [ProceduralEnvironment.GroundRenderPayload.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderPayload.cs), world-tilemap payload replay/backend helpers now live in [ProceduralEnvironment.GroundRenderBackend.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderBackend.cs), staged payload storage, cached full-ground extraction, generated streaming ground-cell capture, and chunk snapshot routing now live in [ProceduralEnvironment.GroundRenderSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderSource.cs), sync `ApplyRulesetToGround*` now replay the committed cached payload through that backend after ruleset application, coroutine ground writes now replay row blocks from staged payload storage before the cache is committed, `far-view` ground/transition chunk sources now treat committed/generated ground payloads as authoritative, falling back to direct tilemap reads only when no ground payload source exists, and an off-by-default experimental chunked sprite-mesh renderer now mirrors both committed non-stream ground payloads and generated streaming ground cells through [ProceduralEnvironment.GroundPayloadChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundPayloadChunkRenderer.cs) plus [GroundPayloadSprite.shader](../My%20project/Assets/Shaders/GroundPayloadSprite.shader) as the first shader-facing validation path
- runtime ground conversion cache lifecycle, readable-texture caching, conversion hash routing, and palette entry points now live in [ProceduralEnvironment.GroundConversion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversion.cs), low-level packed-sprite/unskew/diamond raster helpers now live in [ProceduralEnvironment.GroundConversionRaster.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionRaster.cs), and square-sprite conversion entry point `TryCreateSquareSprite` now lives in [ProceduralEnvironment.GroundConversionSprite.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundConversionSprite.cs)
- far-view bake source assembly, cached/streaming background payload bridges, cached/streaming ground payload bridges, placement payload bridges, chunk-level background payload slices, payload-source HUD/log smoke diagnostics, and explicit routing from payload sources to legacy live-tilemap fallback now live in [ProceduralEnvironment.FarViewSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewSource.cs)
- sprite-sampled payload-driven far-view background raster, chunk-slice reuse, chunk-texture reuse, tile-snapshot slice reuse/versioning, cached GPU tile underlays for unchanged chunk payloads, payload-only chunk fast path for `background + ground + transitions + props + blockers`, mixed-chunk payload underlay composition, tile-data bounds for chunked tilemap layers, no-live-renderer short-circuit for tilemap-only chunked bakes, typed per-chunk far-view render sources, material-backed chunk compositing, the split between background-only raster logic and generic tile snapshot raster logic, and a GPU tile-snapshot backend via an isolated offscreen tilemap rig now live in [ProceduralEnvironment.FarViewBackgroundRaster.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewBackgroundRaster.cs), [ProceduralEnvironment.FarViewTileRaster.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileRaster.cs), [ProceduralEnvironment.FarViewTileGpu.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileGpu.cs), [ProceduralEnvironment.FarViewRenderSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewRenderSource.cs), [ProceduralEnvironment.FarViewSource.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewSource.cs), and [FarViewChunkComposite.shader](../My%20project/Assets/Shaders/FarViewChunkComposite.shader)
- extracted water/rock biome-mask chunk payloads plus the off-by-default vertex-color validation renderer now live in [ProceduralEnvironment.BiomeMaskChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BiomeMaskChunkRenderer.cs) and [BiomeMaskColor.shader](../My%20project/Assets/Shaders/BiomeMaskColor.shader); this consumes `BackgroundBiomeData`/mask extraction rather than live tilemap tile reads
- `UnitCombat` target arbitration, facing/crouch helpers, and faction override logic now live in [UnitCombat.Targeting.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.Targeting.cs)
- `UnitCombat` squad metadata, formation math, shared hex access, and flow-field steering now live in [UnitCombat.FormationFlow.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.FormationFlow.cs)
- `UnitCombat` combat tick orchestration, engage/no-target branching, and stall recovery now live in [UnitCombat.UpdateLoop.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.UpdateLoop.cs)
- `PathManager` occupancy-cache maintenance and occupied-path rejection now live in [PathManager.Occupancy.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Occupancy.cs)
- `PathManager` path reuse, nearest-free lookup, and cluster helpers now live in [PathManager.Reuse.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs)
- `PathRequestQueue` queue draining, job scheduling, and immediate fallback dispatch now live in [PathRequestQueue.Dispatch.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs)
- `PathRequestQueue` async job completion, occupancy snapshots, stats, and safe callback handling now live in [PathRequestQueue.Completion.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs)
- `FlowFieldManager` crowd and influence cost-map logic now live in [FlowFieldManager.CostMaps.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.CostMaps.cs)
- `FlowFieldManager` coarse tile-graph construction now lives in [FlowFieldManager.TileGraph.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.TileGraph.cs)
- `FlowFieldManager` per-target field storage, LoS state, and next-cell sampling now live in [FlowFieldManager.FieldState.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs)
- `LocalAvoidanceSystem` unit gathering, Native buffer growth/fill, and steering applyback now live in [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs)
- `LocalAvoidanceSystem` steering job and cell/hash helpers now live in [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs)
- both splits were verified by Unity script recompilation after `Assets/Refresh`, so this is now a real code decomposition step rather than doc-only bookkeeping

## Current State Snapshot and Prioritized Backlog

This section is the current planning snapshot for the Unity RTS project. It does not own `Fin_prog_project/`; that project keeps its plan in [Fin_prog_project/docs/mvp_stack_risks_plan.md](../Fin_prog_project/docs/mvp_stack_risks_plan.md).

Maintenance rule: this section is referenced by [AGENTS.md](../AGENTS.md) and must be updated in the same change whenever Unity roadmap, completed status, or next priority changes.

Last synchronized: 2026-06-28, after the owner retested the max-zoom static scene with far-view enabled and observed the stream reaching `810/810` chunks, the map disappearing, and FPS dropping to about `14`. `SampleScene` has been reverted to `UseFarViewBake=0`, the component default is now also off, and the scene's diagnostic far-view knobs were reduced to `UseFarViewDirectChunkRender=0`, `FarViewChunkPixels=512`, `FarViewPixelsPerUnit=16`, and `FarViewBakeFrameBudgetMs=0`; current far-view is a diagnostic/P0 investigation path only, not a production scene default. The earlier static max-zoom symptom of about `82` FPS with no units or activity remains tracked as a renderer/streaming workload issue rather than combat or pathing. `ProceduralEnvironment.LateUpdate()` no longer bypasses far-view activation checks while world streaming is active, but far-view baking waits until the required streaming queue is drained; far-view tile-underlay camera render targets now use a depth buffer for URP RenderGraph compatibility; chunked far-view activation now requires every expected content chunk to have a sprite before base tilemaps are disabled; and `ProceduralEnvironment` exposes `GetFarViewDiagnostics()` including expected/ready content chunk counts for tests. `SampleSceneRendererDiagnosticsTests` now includes an explicit `[SampleSceneZoomOutProbe]` max-zoom diagnostic, but the current MCP PlayMode bridge hit a Unity Test Runner lifecycle failure (`SaveModifiedSceneTask` / `ExitPlayModeTask`) before a trustworthy XML result was written, so the final verification for that new probe is still pending a clean PlayMode Test Runner session. Existing focused PlayMode coverage for stream background far-view payload assembly, generated stream ground/placement bounds, far-view tile snapshot bounds, payload-only far-view bake source assembly, validation renderer material setup, far-view chunk-source reuse/version/bounds invalidation, stale source cleanup, texture-cache reuse predicates, payload-only route predicates, and incomplete far-view activation guarding remains in place. `PathRequestQueue` no longer drains queued requests through synchronous fallback while a jobified path request is active, and completed path jobs now consume `MaxPerFrame` queue budget before another job is scheduled. The 100v100 combat-pressure diagnostic keeps `UseJobs=true` so it measures jobified queue pressure instead of forced sync fallback and logs per-frame activity buckets for no-activity, attack, path, and job frames. `UnitView.SetDestination` ignores exact duplicate destination commands before incrementing path-command diagnostics, `PathProfiler.CountCommand()` resets per-frame path-reset counters correctly on frame boundaries, `UnitCombat.CombatTickBudgetPerFrame` uses slot-based spreading so first-contact attack/reset bursts do not starve later-created units, `UnitCombat.ResolveTarget()` fast-paths valid preferred forced targets before job/hash/local target lookup, and combat ORCA friendly keep-alive scans now run only while ORCA is active.

Current readiness:

- The Unity gameplay code is now substantially decomposed around the previous large hotspots, especially `ProceduralEnvironment`, `UnitCombat`, `PathManager`, `PathRequestQueue`, `FlowFieldManager`, and the movement/performance systems.
- The world-generation/rendering stack is prepared for shader validation paths, not for a full production renderer swap yet.
- The cleanest seams today are the typed world/background/ground/placement/far-view payloads and the off-by-default validation renderers for background payloads, ground payloads, and biome masks.
- Completed validation renderers currently include [ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundPayloadChunkRenderer.cs), [ProceduralEnvironment.GroundPayloadChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundPayloadChunkRenderer.cs), and [ProceduralEnvironment.BiomeMaskChunkRenderer.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BiomeMaskChunkRenderer.cs); all are disabled by default.
- The main remaining risk before production shader migration is still data ownership: renderers must consume extracted payloads and snapshots, not live tilemap state, except through explicitly isolated `LegacyTilemap` fallback helpers.
- The current Unity tests are useful regression and smoke coverage, and now include cached/streamed validation renderer coverage for background payloads, ground payloads, and biome masks; all three validation renderers now check actual mesh data, cached background plus cached ground/transition renderer paths now verify separate renderer output per source texture inside a chunk, and cached plus streamed background/ground/biome-mask renderers now verify partial right/top edge chunk geometry when payload bounds are not divisible by chunk size. Background/ground texture-group checks now also verify the hidden sprite shader material and no-shadow renderer flags, while the biome-mask validation renderer checks its hidden runtime vertex-color shader material, sorting order, and no-shadow renderer flags. Streamed ground and biome-mask validation renderers now also honor `GroundPayloadChunkRendererChunkSize` / `BiomeMaskChunkRendererChunkSize` instead of emitting one mesh per stream state. Ground stream bounds now have focused PlayMode coverage for generated-only cells and null-tile filtering. Placement payload chunk snapshots now also have cached/streamed PlayMode coverage for bounds, tile/tint/transform metadata, and world-bounds clipping, and placement stream bounds now separately cover prop/blocker layer merging plus excluded-layer/null-tile/non-generated-chunk filtering. Far-view tile snapshot bounds now have coverage for both sides of the fallback boundary: generated stream ground/placement payload bounds suppress distant legacy ground/transition/prop/blocker tilemap contents when payload sources exist, while legacy tilemaps still define bounds when payload sources are missing. Far-view bake source assembly now has a payload-only stream coverage path that verifies background payload metadata, tile snapshot availability, combined payload bounds, and removal of included tilemap renderers under chunked bake. Stream background far-view payload assembly now has direct sparse generated-chunk merge coverage for payload origin, world bounds, and gaps. Far-view background chunk-source slicing now has direct payload slice coverage for cell rect, world bounds, payload version, sliced decisions, applied-slice reuse/rebuild behavior when version or capture bounds change, stale-slice clearing when a later chunk has no background source, background texture reuse predicate coverage, and background-only payload route coverage. Far-view tile chunk-source coverage now checks applied snapshot reuse when version and capture bounds match, rebuild behavior when the tile snapshot version or capture bounds change, stale snapshot clearing when a later chunk has no tile source, payload-first suppression of legacy transition/blocker snapshots when stream ground/placement payload sources exist, tile-underlay texture reuse predicate coverage, and tile-only payload route coverage. Renderer lifecycle coverage now checks ground missing-root recovery without a payload version bump plus toggle-driven cleanup for background, ground, and biome-mask renderer roots. `ProceduralEnvironment` exposes internal `GetStreamingDiagnostics()` and source-aware `GetValidationChunkRendererDiagnostics()` for tests, so scene smoke can verify streaming, renderer source availability, renderer startup, and disabled renderer cleanup without reflection. `SampleScene` boot now has smoke tests for required bootstrap/HUD/camera/pathfinding/environment/path-queue systems, startup errors, multi-step camera movement, streaming active bounds, tracked/generated chunks, create/reuse/unload counters, source-aware validation renderer startup/toggle behavior, bounded four-step renderer toggle churn, and disabled experimental renderer roots. The suite is still not a full representative model of the main game loop.
- Test audit result: the existing tests are suitable enough to use immediately as targeted agent checks, but not all are equally agent-friendly. The hardening pass added `Gate` / `SceneGate` / `Diagnostic` categories, made `FpsStressTests` explicit diagnostic-only coverage with deterministic random seeding, accurate method names, and a 100v100 owner-target `[CombatPressureProbe]`, added `Tests.EditMode.asmdef` so EditMode tests are discoverable, strengthened combat/scene cleanup, made UnitCombat reflection setup fail with clear messages, split the large renderer payload PlayMode suite into smaller partial files with shared helpers, replaced `UnitCombatStallTests` fixed waits with bounded health polling, expanded `SampleSceneBootSmokeTests` into multi-step camera movement plus streaming create/reuse/unload coverage and bounded source-aware validation renderer startup/toggle churn, moved the scene smoke's streaming/renderer assertions onto internal diagnostics APIs instead of private-field reflection, and added explicit `SampleSceneRendererDiagnosticsTests` baseline logging for renderer/streaming/frame/memory metrics plus external provisional budget checks in the XML extractors. `SampleSceneRendererDiagnosticsTests` also owns the explicit `[SampleSceneZoomOutProbe]` max-zoom far-view activation diagnostic. Remaining hardening work is narrower: reduce other fixed-time checks only where they become flaky or can preserve the same diagnostic value, keep explicit diagnostic FPS/renderer probes out of normal regression gates, add longer soak/performance probes only where they have a bounded diagnostic contract, and restore direct PlayMode Test Runner connectivity when MCP returns `Connection failed` or hits Unity Test Runner lifecycle errors.
- Latest extracted renderer diagnostic baseline, 2026-06-27: `SampleSceneRendererDiagnosticsTests` passed through Unity `TestResults.xml` after the PlayMode MCP response returned `Connection failed`; [extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py) reported `measurement=postWarmup`, `loadSettleFrames=3`, `warmupFrames=5`, `frames=12`, `seconds=0.438`, `avgFrameMs=36.506`, `maxFrameMs=88.682`, `generatedDelta=168`, `createdDelta=18`, `reusedDelta=18`, `unloadedDelta=21`, `finalTracked=9`, `backgroundPeakQuads=17712`, `biomePeakQuads=11442`, `unityAllocDeltaMb=2.147`, and `managedDeltaMb=144.879`. With `--check-provisional-budget`, the same XML passes hard provisional checks (`maxFrameMs=88.682 <= 203`, `test_duration_seconds=7.228806 <= 60`, `finalPending=0`, and renderer roots cleaned up) and emits one warning because `avgFrameMs=36.506` is above the 144 FPS target frame time of about `6.944` ms.
- Latest extracted combat-pressure diagnostic baseline, 2026-06-27: `FpsStressTests.OwnerTarget100v100_PathPressure_LogsCombatPressureProbe` passed through Unity `TestResults.xml` after MCP returned `Connection failed`; [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py) reported `measurement=ownerTarget100v100`, `pathPressure=True`, `playerUnits=100`, `enemyUnits=100`, `totalUnits=200`, `warmupFrames=12`, `frames=90`, `seconds=3.099`, `avgFps=29.038`, `avgFrameMs=34.437`, `maxFrameMs=88.514`, `attackEvents=288`, `playerAttackEvents=143`, `enemyAttackEvents=145`, `maxAttackEventsFrame=19`, `pathResets=203`, `maxPathResetsFrame=20`, `resetCombatInRange=186`, `resetFollowerCancel=17`, `totalPathBuilds=131`, `totalPathAccepts=47`, `totalPathRejects=84`, `maxPathBuildsFrame=2`, `totalPathCommands=227`, `maxPathCommandsFrame=41`, `totalPaths=47`, `totalJobScheduled=89`, `totalJobCompleted=89`, and `totalJobFallback=0`. The same probe also logged `noActivityFrames=0`, `pathActivityFrames=90`, `pathActivityAvgFrameMs=34.437`, `jobActivityFrames=89`, `jobActivityAvgFrameMs=34.810`, `attackActivityFrames=55`, and `attackActivityAvgFrameMs=30.444`. With `--check-provisional-budget`, the XML passes the provisional hard checks including `maxFrameMs <= 203`; it still emits the 144 FPS warning because `avgFrameMs=34.437` is above about `6.944` ms. Treat this as a bounded 100v100 path/combat pressure diagnostic: jobified path fallback spikes are gone, first-contact attack/reset bursts are slot-spread, and the remaining sustained average cost is continuous path/job activity across nearly every measured frame.

Priority P0 - do now:

- Continue hardening the current test harness for agent use while expanding scene-level coverage:
  - keep the split renderer payload PlayMode suite organized by source/snapshot/chunk-source/background/ground/biome-mask/lifecycle responsibility and add new renderer scenarios to the matching partial file;
  - continue improving time-based PlayMode tests by replacing fixed waits with bounded polling where practical; `UnitCombatStallTests` now polls for health changes, while longer diagnostic windows should keep their timing only when they measure behavior over time;
  - keep Unity-side performance assertions out of normal gates for now; use the XML extractor's provisional budget check when collecting renderer diagnostics.
- Do not re-enable `UseFarViewBake` in `SampleScene` by default until a clean `SampleSceneZoomOutProbe` run and a manual max-zoom retest prove that the far-view path keeps the map visible and improves FPS. The latest owner retest reached `810/810` streamed chunks, hid the map, and dropped to about `14` FPS with far-view enabled; a guard now blocks base-map hiding when expected far-view content chunks are missing sprites, but the next renderer work should still isolate the remaining far-view visibility/performance failure, then continue tile/prop draw-call reduction or a payload-backed shader path rather than combat optimization.
- Continue combat-pressure tuning from the current 100v100 diagnostic state: `PathRequestQueue` avoids synchronous fallback while a path job is active and counts job completion against `MaxPerFrame`, `UnitView.SetDestination` suppresses duplicate destination commands, `UnitCombat.CombatTickBudgetPerFrame` spreads first-contact combat ticks by slot, `PreferForcedTarget` avoids unnecessary target searches when a valid forced target should win, and ORCA friendly keep-alive scans are skipped while ORCA is inactive. The current diagnostic passes the provisional hard spike cap with `totalJobFallback=0`, but average frame time remains a warning and the new activity buckets show path activity on all 90 measured frames plus job activity on 89, so the next investigation should focus on reducing continuous path/job churn rather than broad fallback, first-contact burst, or simple cadence knob tuning.
- Continue strengthening payload parity and renderer validation tests for remaining far-view chunk sources. Background, ground, and biome-mask validation renderers now have isolated mesh-data checks for their current contracts, cached background and ground/transition validation renderers cover multi-texture grouping inside a chunk, cached and streamed validation renderers cover partial edge chunks, validation renderers cover runtime material/renderer setup for the checked hidden shader paths, placement chunk snapshots cover cached/stream extraction, generated stream ground/placement bounds cover generated-only/null-tile/excluded-layer behavior, far-view tile snapshot bounds cover both payload-first suppression of legacy tilemap fallback contents and the no-payload legacy fallback case, payload-only far-view bake source assembly covers background/tile payload metadata and renderer-list cleanup, tile chunk-source extraction covers payload-first suppression of legacy transition/blocker snapshots, stream background far-view payload assembly plus far-view background chunk slicing and tile chunk reuse/version/bounds/stale-source/texture-cache/payload-route invalidation have focused coverage, renderer lifecycle covers missing-root recovery, toggle cleanup, and biome-mask streaming source disappearance, and `SampleScene` has boot-smoke plus bounded renderer toggle churn coverage. Remaining renderer P0 work is visual/performance parity before default use; `SampleSceneRendererDiagnosticsTests` now provides a diagnostic baseline log source, and the XML extractor owns the current provisional hard/warning budget interpretation.
- Keep direct tilemap reads behind explicit `LegacyTilemap` fallback helpers only, and avoid adding new renderer logic that reads live tilemaps directly.
- Expand scene-level PlayMode coverage beyond boot only where the scenario has a bounded diagnostic contract. The current `SampleScene` gate now uses internal diagnostics instead of reflection, checks multi-step camera streaming create/reuse/unload counters, and exercises source-aware validation renderer startup/toggle churn under real scene conditions.
- Keep shader-facing validation renderers disabled by default until visual parity and performance are measured.

Priority P1 - next:

- Build the next ground/background shader input path on top of existing payloads, with tilemap parity checks before replacing any visible production path.
- Extend the water/rock mask validation renderer toward a real shader input path after payload parity is stable.
- Continue structured performance data capture for worldgen, streaming, far-view, renderer updates, and combat pressure instead of relying only on visual inspection. The current first-pass diagnostics are `[SampleSceneRendererProbe]` and `[CombatPressureProbe]`.

Priority P2 - after the payload-backed renderer is stable:

- Move props/trees toward instanced rendering that consumes placement payloads rather than tilemap state.
- Simplify or retire far-view bake stages that become redundant once payload-backed shader rendering covers the same zoom ranges.
- Add longer running PlayMode scenarios for streaming, camera movement, save/load, combat pressure, and renderer toggles.

Priority P3 - broader cleanup:

- Continue reducing root partial classes only when a split removes a real responsibility boundary, not just to reduce line count.
- Revisit remaining navigation and combat tuning debt in [navigation_optimization_ideas.md](./navigation_optimization_ideas.md).
- Keep [runtime_switches.md](./runtime_switches.md), [code_map.md](./code_map.md), and machine indexes synchronized after any new runtime surface or major file move.

Items that require direct owner input before they can be guaranteed:

- Visual acceptance criteria for shader parity: prototype-level parity for now; obvious holes, flicker, broken layer order, missing readable biome shapes, and unusable far-zoom output are regressions, while exact pixel/color/edge parity can be adjusted iteratively.
- Target hardware and performance budgets: primary target is 8 GB RAM, 8 CPU cores at about 3.6 GHz, and GPU not below AMD Radeon RX 7600 XT. The owner target is 144 FPS, which implies about 6.94 ms average frame time. The current 203 ms spike value is explicitly temporary and rough, used only as an initial diagnostic hard cap until real renderer/streaming data shows what tighter warning/fail thresholds should be.
- Map-size target: current large-map working size is 4096x4096, while the intended release-scale target is 8192x8192. Renderer/streaming diagnostics should keep this growth path in mind instead of optimizing only for the current scene size.
- Loading/warmup target: use 60 seconds as the provisional maximum for initial load/generation/warmup, then tighten or split it into more precise budgets after real measurements exist.
- Unit-count target: use 200 simultaneously active combat units as the provisional target, split as 100 player units and 100 enemy units. This is not a final release cap; `FpsStressTests.OwnerTarget100v100_PathPressure_LogsCombatPressureProbe` and [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py) now provide the first reusable diagnostic route for that target.
- Remaining target details still needed before final performance certification: separate budgets for normal camera movement, renderer toggle churn, combat pressure, save/load, and rare loading-only spikes.
- Final gameplay priorities outside the current RTS prototype loop, especially state/diplomacy/research expansion from [states_diplomacy_research_automation_plan.md](../supplements/design/states_diplomacy_research_automation_plan.md).
- Art direction and asset replacement rules for terrain, props, units, UI, music, and SFX.

Unity test trust level:

- Current EditMode coverage verifies selected pure logic, especially combat target priority.
- Current PlayMode coverage catches important regressions in combat/stall behavior, path resets, FPS stress scenarios, far-view/payload source behavior, validation renderer lifecycle, and `SampleScene` boot wiring.
- These tests are not yet enough to certify the main game as a whole because they do not fully cover complete save/load loops, long streaming sessions, shader parity, or target-device performance budgets.

Agent-facing test contract:

- Unity tests under [Assets/Tests](../My%20project/Assets/Tests) are durable tools for Codex/agent work, not optional human-only QA.
- When a task touches a covered system, agents should run the smallest relevant EditMode or PlayMode filter through Unity MCP/Test Runner when the editor connection is available.
- Test categories now exist for agent filtering: `Gate` for normal regression gates, `SceneGate` for real-scene wiring tests, and `Diagnostic` for explicit/manual diagnostic runs such as FPS stress.
- If `run_tests` is unavailable or returns a connection failure, check whether Unity wrote a fresh `AppData/LocalLow/DefaultCompany/My project/TestResults.xml` for the requested PlayMode filter. Treat the XML as the observed Test Runner result only when its timestamp, fixture/test names, counts, and Unity Console output match the attempted run; otherwise the result is `not run`, not `passed`. Agents should still run Unity script recompilation and repo audits where applicable, then report the MCP limitation explicitly.
- Current MCP observation: full EditMode run works and passes the `UnitCombatTargetingTests` gate, while filtered EditMode runs can report `0/0` despite discovering a test count; direct PlayMode filters can return `Connection failed`; on 2026-05-06, the Unity Test Runner UI manually passed `Tests.PlayMode.SampleSceneBootSmokeTests` while MCP `run_tests` for the same PlayMode filter still returned `Connection failed: Unknown error`, and the fresh Unity `TestResults.xml` also reported that fixture as 3/3 passed after the MCP transport failure. On 2026-06-27, after a full Unity restart, script recompilation passed with 0 warnings and the `Tests.PlayMode.ProceduralEnvironmentFarViewPayloadSourceTests` run wrote a fresh passed XML result with 46/46 tests even though MCP returned `Connection failed: Unknown error`; `Tests.PlayMode.UnitCombatStallTests` also wrote a passed 2/2 XML result after the same MCP response failure. The explicit `SampleSceneRendererDiagnosticsTests` and `FpsStressTests.OwnerTarget100v100_PathPressure_LogsCombatPressureProbe` runs also wrote passed XML results even when MCP returned a connection failure; extract them with [extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py) or [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py). This is currently tracked as an MCP bridge/transport limitation unless Unity Console shows a real fixture failure. Do not run Unity Test Runner jobs in parallel through MCP. After a PlayMode connection failure, the next EditMode call can surface the completed PlayMode result with stale PlayMode test names and cleanup logs, so record that separately as observed PlayMode output and rerun EditMode before recording its result.
- For renderer diagnostics, run [extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py) with `--check-provisional-budget` after the explicit PlayMode diagnostic. Hard failures currently cover failed XML/test result, `maxFrameMs > 203`, diagnostic duration over 60 seconds, nonzero `finalPending`, missing/invalid core metrics, and validation renderer roots left alive. The 144 FPS target is currently a warning through `avgFrameMs`, not a hard fail.
- For owner-target combat-pressure diagnostics, run [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py) with `--check-provisional-budget` after `FpsStressTests.OwnerTarget100v100_PathPressure_LogsCombatPressureProbe`. Hard failures currently cover failed XML/test result, wrong unit counts, no path builds, no attack events, `maxFrameMs > 203`, diagnostic duration over 60 seconds, and missing/invalid core metrics. The 144 FPS target is currently a warning through `avgFrameMs`, not a hard fail.
- New behavior, renderer seams, scene lifecycle risks, or regression-prone fixes should add or update focused tests in the same change when practical, so future agents can reuse them for verification.
- Prefer focused tests first, then broader scene-level tests only after the narrow contract passes or the failure requires real scene wiring.

Agent suitability audit, 2026-04-30:

| Test file | Current agent use | Hardening needed before heavier reliance |
| --- | --- | --- |
| [UnitCombatTargetingTests.cs](../My%20project/Assets/Tests/EditMode/UnitCombatTargetingTests.cs) | Good fast EditMode gate for target-priority logic; full EditMode run passes after adding [Tests.EditMode.asmdef](../My%20project/Assets/Tests/EditMode/Tests.EditMode.asmdef). | Keep reflection-member assertions current if targeting internals change. |
| [ProceduralEnvironmentFarViewPayloadSourceTests.cs](../My%20project/Assets/Tests/PlayMode/ProceduralEnvironmentFarViewPayloadSourceTests.cs) plus renderer partials in the same folder | High-value `Gate` PlayMode coverage is now agent-sized: payload-source diagnostics, placement snapshots, far-view chunk sources, background renderer, ground renderer, biome-mask renderer, lifecycle toggles, biome-mask streaming source cleanup, and shared helpers are split across focused partial files while preserving test method names. | Add new renderer cases to the matching partial file and run the narrowest reliable PlayMode filter when Test Runner connectivity is available; otherwise treat compilation as fallback verification, not a test pass. |
| [SampleSceneBootSmokeTests.cs](../My%20project/Assets/Tests/PlayMode/SampleSceneBootSmokeTests.cs) | Good scene-wiring smoke gate and best starting point for future scene-level coverage; now has `Gate` / `SceneGate` categories, safer log handling, required-system boot checks, multi-step camera movement, streaming-state assertions plus create/reuse/unload counters via `GetStreamingDiagnostics()`, and source-aware validation renderer startup/toggle plus bounded four-step churn cleanup checks via `GetValidationChunkRendererDiagnostics()`. | Keep this as a correctness gate; do not add budget assertions here. |
| [SampleSceneRendererDiagnosticsTests.cs](../My%20project/Assets/Tests/PlayMode/SampleSceneRendererDiagnosticsTests.cs) | Explicit `Diagnostic` baseline probe for real `SampleScene` streaming/validation-renderer churn; logs `[SampleSceneRendererProbe]` with frame, memory, streaming, and renderer object/quad counts. The Unity test itself has no performance assertions; the current XML extractor [extract_sample_scene_renderer_probe.py](../scripts/extract_sample_scene_renderer_probe.py) can apply the provisional owner budget with `--check-provisional-budget`. | Run manually or through a reliable PlayMode result path when collecting baselines; treat hard extractor failures as regressions and 144 FPS misses as warnings until the budget is tightened with target-device data. |
| [CombatPathResetTests.cs](../My%20project/Assets/Tests/PlayMode/CombatPathResetTests.cs) | Useful focused regression for combat chase reset/jitter behavior; now has `Gate` category and setup/teardown cleanup for units/path jobs/test hex bootstrap. | Replace the fixed chase window only if it becomes flaky. |
| [UnitCombatStallTests.cs](../My%20project/Assets/Tests/PlayMode/UnitCombatStallTests.cs) | Useful small PlayMode combat behavior gate; attack assertions now use bounded health polling instead of fixed `WaitForSeconds`, so successful behavior can end the test earlier. | Keep timeouts aligned with real combat cadence if attack cooldown, movement speed, or range defaults change. |
| [FpsStressTests.cs](../My%20project/Assets/Tests/PlayMode/FpsStressTests.cs) | Explicit `Diagnostic` benchmark log source with deterministic random seed, fixed 20v20 scenarios, a 10v10-to-20v20 sweep, and an owner-target 100v100 path/combat-pressure `[CombatPressureProbe]`; [extract_combat_pressure_probe.py](../scripts/extract_combat_pressure_probe.py) can apply the provisional budget outside the Unity test. | Keep this out of normal regression gates; treat hard extractor failures as regressions and 144 FPS misses as warnings until the budget is tightened with target-device data. |

Recommended universal test additions:

- Use `SampleSceneRendererDiagnosticsTests` to collect renderer/streaming baseline metrics, then run the extractor's provisional budget check before deciding whether a renderer change is acceptable for further work.
- Deterministic worldgen snapshot tests by seed and map size for biome masks, chunk bounds, placement counts, and blocked-cell counts.
- Render parity tests comparing tilemap-backed output, payload snapshots, and experimental shader-facing renderers for representative chunks.
- Streaming lifecycle tests that pan the camera across chunk boundaries and assert create/reuse/cleanup/version behavior.
- Save/load end-to-end tests that spawn units, move, fight, mutate resources/research, save, load, and compare state.
- Performance budget tests that emit structured data for FPS, frame spikes, path requests, chunk updates, allocations, renderer object counts, and owner-target 100v100 combat pressure.
- Long-run soak tests for several in-game minutes with combat, streaming, and renderer toggles enabled.

## Index / Search Hints
- Bootstrap & lifecycle: CompositionRoot, GameStateService, SaveSystem, camera setup.
- Domain & use cases: EconomyState/Manager, ResearchStore, StartNewGame, PlaceBuilding, StartResearch, CompleteResearch.
- Configs: GameConfig, BuildingConfig, ResearchConfig, UnitConfig, MovementSettings.
- Input & UI: InputController, UnitSpawnerCommander, HudController, ActionsPanel, ResearchPanel, camera controls.
- Units & movement: UnitView, UnitStats, UnitHpOverlay, UnitVisualCulling, UnitPathFollower.
- Pathfinding & navigation: PathManager, PathRequestQueue, HexPathfindingBootstrap, HexPathfinderJob, CrowdingResolver, ProceduralObstacles.
- Combat & targeting: UnitCombat (state + lifecycle), UnitCombat.UpdateLoop (combat tick, chase/repath, stall recovery), UnitCombat.State (health, forced targets, profile application), UnitCombatJobScheduler, EnemySquadManager, OccupancyHash.
- Persistence: SaveSystem bindings, what is persisted.
- Debug/perf: PathProfiler, PathDebugHUD, diagnostic toggles.
- Tests: [Assets/Tests](../My%20project/Assets/Tests) (EditMode + PlayMode).

## Layers and code map
- This doc describes runtime behavior; for file navigation see [code_map.md](./code_map.md).
- For exact `CODE-ID` and section lookup see [code_id_index.md](./code_id_index.md).
- For symptom-first bug routes see [debug_playbooks.md](./debug_playbooks.md).
- For important runtime/inspector knobs see [runtime_switches.md](./runtime_switches.md).
- Domain: pure data + rules (`Domain/*`).
- Application: use cases that orchestrate domain changes (`Application/*`).
- Infrastructure: ScriptableObjects and persistence (`Infrastructure/*`).
- Presentation: Unity MonoBehaviours (`Presentation/*`).

## Bootstrap and Lifecycle
- [CompositionRoot.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs)
  - Owns root lifecycle/state references and ticks `EconomyManager` every frame (`CROOT-01`).
- [CompositionRoot.Setup.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Setup.cs)
  - Creates `GameStateService`, auto-starts a new game, ensures camera/pathfinding/environment/performance singletons, disables legacy avoidance, and configures existing scene units (`CROOT-02`).
- [CompositionRoot.Actions.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Actions.cs)
  - Save/load wrappers plus build/research debug actions and status text (`CROOT-03`).
- [CompositionRoot.Persistence.cs](../My%20project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs)
  - SaveSystem capture/restore callbacks for units, faction visuals, overlays, culling, and sorting (`CROOT-04`).

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
  - `HudController.Squads` owns squad summary aggregation and bottom-strip squad selection UI.
- Dev panel (`ActionsPanel`):
  - Spawn units, add resources, attempt build, toggle research panel, clear save file.
  - `ActionsPanel.SelfTest` owns the deterministic save/load self-test: spawn units, save/load, validate data.
- Research panel (`ResearchPanel`):
  - Renders `ResearchConfig` items from `CompositionRoot.TestResearch`.
  - Buttons call `StartResearch` / `CompleteResearch` use cases.

## Units, Health, and Visuals
- `UnitStats`: `MaxHealth`, `Speed` (movement uses `MovementSettings`, not `UnitStats.Speed`).
- `UnitView`:
  - [UnitView.Movement.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Movement.cs) now owns per-frame movement integration, steering blend, and facing updates.
  - [UnitView.Rendering.cs](../My%20project/Assets/Scripts/Presentation/View/UnitView.Rendering.cs) now owns Y-sorting bootstrap and destination gizmo helpers.
- `UnitSpriteAnimator`:
  - [UnitSpriteAnimator.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.cs) now owns animator state, renderer wiring, and combat event subscription lifecycle.
  - [UnitSpriteAnimator.Playback.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Playback.cs) now owns per-frame directional playback and crouch/locomotion state transitions.
  - [UnitSpriteAnimator.Combat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitSpriteAnimator.Combat.cs) now owns attack/death animation triggers and external crouch requests.
  - Uses `MovementSettings.Default` if no asset assigned.
  - `SetDestination` ignores targets at the current position and exact duplicate destinations before incrementing `PathProfiler.CountCommand`; optional jitter logs when `LogJitteryCommands` and `EnableJitterLog` are true.
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
  - [UnitSoARegistry.Build.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Build.cs) now owns per-frame projection from active units into ORCA/combat snapshot arrays.
  - [UnitSoARegistry.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitSoARegistry.Buffers.cs) now owns NativeArray capacity growth, disposal, and shared cell projection helpers.
  - Also exposes combat snapshots for targeting systems when enabled.
- `CrowdingResolver`:
  - Runs in `LateUpdate` every `Interval`.
  - [CrowdingResolver.Search.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Search.cs) now owns free-cell search, reservation keys, and odd-r ring enumeration.
  - [CrowdingResolver.Throttle.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs) now owns adaptive throttling, effective search/group budgets, and diagnostic logging.
  - Groups units by hex cell; nudges units beyond `AllowStayCountPerCell`.
  - Uses adaptive throttling (`FrameTimeSoftLimit/HardLimit`) and population scaling.
  - Skips squad-controlled units and units currently following flow fields; no-ops while ORCA is active or legacy local avoidance is enabled.
  - Skips work when path builder budget is exhausted and uses `MoveCooldown` to prevent ping-pong.
  - Can resolve stacks for a limited window after enemies disappear (`ResolveWithoutEnemies`).
- `StuckResolver`:
  - [StuckResolver.Recovery.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs) now owns optional combat repath and nearest-free nudge recovery.
  - [StuckResolver.State.cs](../My%20project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs) now owns per-unit progress reset and stale-entry cleanup.
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
  - Processes up to `MaxPerFrame` request dispatch/completion steps; `MaxQueueSize` drops oldest.
  - Jobs: `UseJobs=true` schedules `HexPathfinderJob` when native walkable data exists; queued requests now wait while a job is active instead of falling through to synchronous processing in the same frame.
  - Completed jobs consume queue budget before another job is scheduled, so `MaxPerFrame=1` can split completion and next scheduling across frames.
  - Uses per-frame occupancy snapshots by faction for job scheduling and caches the hex bootstrap reference.
  - Fallback to sync `PathManager.BuildPath` when job scheduling fails, the job returns no path, or jobs are disabled.
  - `ProcessSynchronouslyIfIdle` can handle requests immediately when jobs are off.
- `HexPathfindingBootstrap`:
  - Odd-r hex grid, pointy-top. Defaults: `Width/Height=1024`, `HexSize=0.4`.
  - `AutoClampSize` limits grid if `Width*Height` exceeds `MaxCells`.
  - [HexPathfindingBootstrap.Walkability.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Walkability.cs) owns `BakeFromPhysics`, `BakeFromPhysicsRect`, `CaptureBlocked`/`RestoreBlocked`, and native walkable maintenance.
  - [HexPathfindingBootstrap.Geometry.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.Geometry.cs) owns world/grid conversion, gizmos, and `GridInfo`.
  - `BakeFromPhysics` uses `ObstacleMask` (or "Obstacles" layer) and `SampleRadius`.
  - `BakeFromPhysicsRect` / `BakeFromPhysicsRectCells` update only a grid region and patch native walkable data for that rect.
  - Maintains `NativeArray<byte>` walkable map; `UpdateNativeWalkable` completes jobs before rebuild.
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
  - Budgets: `CombatTickBudgetPerFrame` spreads combat ticks across slot-selected frames when active unit count exceeds the budget; `RepathBudgetPerFrame` is enforced; `TargetSearchBudgetPerFrame` is declared but not currently enforced.
  - Targeting pipeline:
    - Returns a valid forced squad target before job/hash/local lookup when `PreferForcedTarget` is enabled.
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
  - [EnemySquadManager.Membership.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Membership.cs) now owns recruitment, gather-radius growth, and squad-center maintenance.
  - [EnemySquadManager.Tactics.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.Tactics.cs) now owns mode hysteresis, forced-target TTL, and squad-anchor updates.
  - Dynamic gather radius grows by step every `GatherRadiusStepSeconds` until full or `MaxGatherRadiusHex`.
  - If still underfilled at max radius, squad sleeps and retries recruitment every `SleepRetrySeconds`.
  - Uses squad-to-squad distance in hexes to drive states (`Gathering`, `Marching`, `Ready`, `FreeCombat`, `Sleeping`) with hysteresis (`Ready/Combat` entry and exit distances).
  - Assigns forced squad targets by TTL; in `FreeCombat` targets are released only when a unit is close enough (world distance based on `AttackRange`, optional hex override).
  - Assigns per-unit formation slot indices for arrival offsets when enabled.
  - Computes a squad move anchor via flow fields; non-free-combat units follow formation slots around the anchor (macro->micro split).
- `UnitCombatJobScheduler`:
  - Jobified spatial hash (`NativeParallelMultiHashMap`) for nearest enemy search.
  - [UnitCombatJobScheduler.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Buffers.cs) now owns buffer growth, snapshot ingestion, hash-bucket construction, and applyback to `UnitCombat`.
  - [UnitCombatJobScheduler.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/UnitCombatJobScheduler.Job.cs) now owns the burst-compiled nearest-enemy search job.
  - `Interval`, `HashCellSize`, `HashRings` control cadence and coverage.
  - Uses double buffering: schedule in one tick, apply results on the next update.
  - Skips squad-controlled units (squad targeting handled by `EnemySquadManager` + `OccupancyHash`).
  - Can reuse `UnitSoARegistry` snapshots when enabled to reduce per-unit transform reads.
- `OccupancyHash`:
  - Rebuilt every frame; native hash for occupancy counts and a managed bucket map for nearest lookup.
- `MovementJobSystem`:
  - [MovementJobSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Buffers.cs) now owns unit gathering, buffer growth, and applyback to `UnitView`.
  - [MovementJobSystem.Jobs.cs](../My%20project/Assets/Scripts/Presentation/Performance/MovementJobSystem.Jobs.cs) now owns the parallel movement integration job.
- `OrcaAvoidanceSystem`:
  - ORCA/RVO local avoidance in jobs, using a spatial hash and per-unit velocity override.
  - [OrcaAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Buffers.cs) now owns unit gathering, Native buffer growth, snapshot ingestion, and applyback to `UnitView`.
  - [OrcaAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.Job.cs) now owns the burst-compiled ORCA solver and LP helper routines.
  - Supports cohesion toward friendly centroid to keep groups together.
  - Per-unit priority (`UnitView.OrcaPriority`) shifts avoidance responsibility; `MinResponsibility` clamps zero-weight agents.
  - Respects per-unit `UseOrcaVelocity` (units can opt out of overrides but remain avoidance obstacles).
- `LocalAvoidanceSystem`:
  - Legacy steering avoidance fallback (disabled by default when ORCA is enabled; no-ops while ORCA is active).
  - [LocalAvoidanceSystem.Buffers.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Buffers.cs) now owns unit gathering, Native buffer growth/fill, and steering applyback.
  - [LocalAvoidanceSystem.Job.cs](../My%20project/Assets/Scripts/Presentation/Performance/LocalAvoidanceSystem.Job.cs) now owns the steering job plus cell/hash helpers.

## Environment and Obstacles
- `ProceduralObstacles`:
  - Spawns rocks by `Count` or `CoveragePercent`.
  - `UseRandomSeed=true` uses time-based randomness; `false` uses `Seed`.
  - Ensures obstacle layer is included in `HexPathfindingBootstrap.ObstacleMask`.
  - Re-bakes walkability after spawn using a bounded rect when possible.
- `ProceduralEnvironment`:
  - Builds a ground tilemap layer and optional prop layer from `TileBase` arrays.
  - Aligns grid cell size to `HexPathfindingBootstrap.HexSize` when hex layout is used.
  - Supports blocking props (separate tile list) that can be baked into walkability.
  - Can auto-split blocking props by tile name keywords when enabled.
  - Supports async, chunked generation to avoid editor freezes on large maps.
  - Can update walkability directly for blocking tiles (no physics rebake) via `UseDirectWalkableUpdates`.
  - World streaming:
    - `UseWorldStreaming` streams chunks around the camera (active + prefetch radii).
    - `StreamFrameBudgetMs` + `StreamTargetFps` cap per-frame work.
    - `StreamKeepGeneratedChunks` keeps chunks and skips re-bake; `StreamBakeAllChunksOnIdle` fills the map when camera is idle.
    - Water/rock biomes in streaming are driven by the same water/rock masks as full generation (not by simple noise).
    - `StreamPropsUseBackgroundGrid` controls whether props/trees are placed on the background grid (rect) or hex grid (default: hex).
  - Background rect grid:
    - `UseBackgroundTilemap` renders square-converted ground tiles.
    - Background cell size is derived from converted tiles; overlap can be added via `BackgroundCellOverlapPixels`.
  - Far view bake:
    - `UseFarViewBake` can render the map to cached chunks for very far zooms, but it is currently off by default and off in `SampleScene` after the `810/810` chunk max-zoom regression where the map disappeared and FPS dropped to about `14`.
    - Chunked bake uses `FarViewChunkPixels`, `FarViewChunksPerFrame`; `SampleScene` keeps diagnostic values at `512` chunk pixels and `16` pixels per unit.
    - Far-view bake work must wait for the required streaming queue to drain, and chunked activation must have sprites for all expected content chunks before disabling base tilemaps; do not treat the current far-view path as production-safe until max-zoom visibility and FPS are fixed.

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
  - `ProceduralEnvironmentFarViewPayloadSourceTests` is a `Gate` suite for payload-source, chunk-snapshot, validation-renderer mesh, lifecycle, texture grouping, and edge-chunk contracts. Its methods are split across focused partial files under [Assets/Tests/PlayMode](../My%20project/Assets/Tests/PlayMode): payload source diagnostics, placement payload snapshots, far-view chunk sources, background payload renderer, ground payload renderer, biome-mask renderer, validation renderer lifecycle, and shared payload test helpers.
  - `SampleSceneBootSmokeTests` is a `Gate` / `SceneGate` suite for real `SampleScene` boot wiring, multi-step camera movement, streaming-state smoke checks plus create/reuse/unload counters through `GetStreamingDiagnostics()`, and source-aware validation renderer startup/toggle churn cleanup through `GetValidationChunkRendererDiagnostics()`.
  - `SampleSceneRendererDiagnosticsTests` is an explicit `Diagnostic` suite that logs `[SampleSceneRendererProbe]` baseline metrics for real-scene streaming and validation renderer churn plus `[SampleSceneZoomOutProbe]` for max-zoom far-view activation; do not treat it as a performance gate until owner budgets exist.
  - `CombatPathResetTests` is a `Gate` suite for destination reset rates during chase.
  - `UnitCombatStallTests` is a `Gate` suite for close-range attacks and chasing; it uses bounded health polling rather than fixed sleeps.
  - `FpsStressTests` is an explicit `Diagnostic` suite that logs average FPS per scenario (`[FpsStress]` line) plus owner-target 100v100 combat pressure (`[CombatPressureProbe]` line); do not treat it as a normal pass/fail performance gate.
- EditMode:
  - `UnitCombatTargetingTests` is a `Gate` suite for target resolution priority and is compiled by [Tests.EditMode.asmdef](../My%20project/Assets/Tests/EditMode/Tests.EditMode.asmdef).
