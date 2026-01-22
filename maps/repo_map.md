# Repo Map

Compact top-level map for fast routing. Use this before opening large docs.

## Primary roots

- [Assets/Scripts](../My%20project/Assets/Scripts)
  - Project-owned runtime/editor code.
- [Assets/Tests](../My%20project/Assets/Tests)
  - PlayMode/EditMode tests and performance harnesses.
- [Assets/Scenes](../My%20project/Assets/Scenes)
  - Scene wiring, especially `SampleScene`.
- [docs](../docs)
  - Human-readable architecture, debug, and navigation docs.
- [maps](./)
  - Compact repo-level maps for fast agent/tool routing.
- [Sprites](../Sprites)
  - Project sprite assets outside the Unity project root.
- [Assets/SmallScaleInt](../My%20project/Assets/SmallScaleInt)
  - Purchased/third-party asset content used by world visuals and character generation.

## Runtime code roots

- [Assets/Scripts/Domain](../My%20project/Assets/Scripts/Domain)
  - Data models and core gameplay rules.
- [Assets/Scripts/Application](../My%20project/Assets/Scripts/Application)
  - Use cases and state orchestration.
- [Assets/Scripts/Infrastructure](../My%20project/Assets/Scripts/Infrastructure)
  - Configs and persistence.
- [Assets/Scripts/Presentation](../My%20project/Assets/Scripts/Presentation)
  - Unity scene logic, pathfinding, generation, performance systems, UI, input, and view.
- [Assets/Scripts/Editor](../My%20project/Assets/Scripts/Editor)
  - Editor utilities and asset/character tools.

## Hot systems

- [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
  - World generation, background conversion, water/rock biomes, streaming, far-view bake.
- [HexPathfindingBootstrap.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/HexPathfindingBootstrap.cs)
  - Authoritative hex grid and walkability state.
- [PathManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs) / [PathRequestQueue.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs)
  - Path building, caching, and async/job submission.
- [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
  - Shared flow fields for squads and macro movement.
- [EnemySquadManager.cs](../My%20project/Assets/Scripts/Presentation/Performance/EnemySquadManager.cs)
  - Squad formation and macro combat state switching.
- [UnitCombat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
  - Per-unit targeting, attack timing, chase/repath behavior.
- [OrcaAvoidanceSystem.cs](../My%20project/Assets/Scripts/Presentation/Performance/OrcaAvoidanceSystem.cs)
  - Local avoidance.
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
- [code_index.json](../docs/code_index.json)
  - Machine-readable system index.
- [unity_mcp_tools.md](../docs/unity_mcp_tools.md)
  - Available Unity MCP tools and recommended usage.
- [agent_comment_standard.md](../docs/agent_comment_standard.md)
  - Header/comment convention for hot files.

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
