# AGENTS.md

Scope: `My project/`, the Unity RTS project.

Follow the root `AGENTS.md` first. These rules add Unity-specific constraints.

## Project Identity

- This is the Unity RTS project, not the financial monitoring project.
- Unity version is `6000.4.1f1` according to `ProjectSettings/ProjectVersion.txt`.
- Main scene entrypoint is `Assets/Scenes/SampleScene.unity`.
- Runtime code is primarily under `Assets/Scripts/`.
- Tests are under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`.

## Run, Build, Verify

- Run locally by opening `My project/` in Unity Hub, opening `Assets/Scenes/SampleScene.unity`, and pressing Play.
- There is no checked-in command-line Unity build script. Do not invent a build command; use Unity Editor build flow only when explicitly requested.
- Verify C# changes with Unity script recompilation.
- Verify gameplay changes with Unity Test Runner EditMode/PlayMode tests under `Assets/Tests/`.
- After root docs/config snapshots change, run from repository root:
  `python .\scripts\run_all_repo_audits.py --with-full-export`

## Sensitive Areas

- Do not manually edit Unity-generated caches: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Obj/`, `Build*/`, generated `.csproj`, `.sln`, `.slnx`, `.user`, `.pidb`, `.svd`, `.pdb`, `.mdb`, `.opendb`, `.VC.db`.
- Treat `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/` as high-risk project settings. Change them only when the task explicitly requires it.
- Preserve `.meta` files when adding, moving, or deleting Unity assets.
- Avoid moving third-party or purchased assets unless the task is explicitly asset-organization work.

## Worldgen and Shader Migration

- Current canonical plan is in `../docs/gameplay_current_state.md`.
- Update `../docs/gameplay_current_state.md` in the same change whenever worldgen/render/shader work changes what is done or what should happen next.
- Do not migrate rendering directly to shaders while renderer inputs still depend on live tilemap reads.
- New render paths must consume extracted payloads/snapshots where possible: background, ground, biome-mask, placement, and far-view sources.
- Direct tilemap reads should remain isolated behind explicit `LegacyTilemap` fallback helpers.
- Experimental shader-facing renderers must stay off by default until parity and performance are measured.
- Gameplay, navigation, combat, save/load, and walkability data remain CPU-side unless a separate migration plan is written.

## Unity Documentation Matrix

- `../docs/gameplay_current_state.md`: current runtime state, completed refactor/shader steps, readiness, prioritized backlog, test-trust notes, and owner-input blockers.
- `../docs/runtime_switches.md`: high-value inspector/runtime switches, defaults, when to touch them, and risks.
- `../docs/code_id_index.md`: task-to-file routes, `CODE-ID` routes, and section IDs for hot systems.
- `../docs/code_map.md`: file ownership, architecture map, and runtime flow changes.
- `../maps/repo_map.md` and `../maps/repo_map.json`: top-level routing and hot-system entries.
- `../docs/navigation_optimization_ideas.md`: navigation/movement backlog only, not current runtime truth.
- `../supplements/design/states_diplomacy_research_automation_plan.md`: future design expansion only, not implementation status unless explicitly promoted.

Update the matrix entry in the same change when its owned topic changes. If unsure whether a Unity change affects docs, update `../docs/gameplay_current_state.md` with a short status note rather than leaving the state only in chat.

## Tests To Prefer

- For render/data extraction changes, add or update PlayMode tests around payload parity, chunk bounds, stream/non-stream behavior, and renderer lifecycle toggles.
- For combat/navigation changes, add small deterministic tests before relying on broad FPS stress scenarios.
- FPS stress tests are useful diagnostics, not a substitute for correctness checks.
