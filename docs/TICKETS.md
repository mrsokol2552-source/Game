# Tickets (Sprint 1 & 2)

Sources: SPRINT_PLAN.md, DECISIONS.md. No dates; each ticket has ID, area, priority, size, status, DoD, and dependencies.

Legend: Priority P1вЂ“P3; Size S/M/L; Status todo/in-progress/done.

## Sprint 1 вЂ” Foundations

- ID: S1-01 — Project skeleton (asmdef + folders)
  - Area: Arch | Priority: P1 | Size: M | Status: done
  - Desc: Create layered structure Domain/Application/Infrastructure/Presentation; add asmdef per layer.
  - DoD: Project opens; compiles; scene runs; layers match STANDARDS.md.
  - Links: README.md:1
  - Depends: вЂ”

- ID: S1-02 вЂ” Economy types (ResourceType/ResourceAmount)
  - Area: Economy | Priority: P1 | Size: S | Status: done
  - DoD: Types exist and used in EconomyState; unit or playmode smoke test.
  - Links: My project/Assets/Scripts/Domain/Economy/ResourceType.cs:1, My project/Assets/Scripts/Domain/Economy/ResourceAmount.cs:1
  - Depends: S1-01 — Project skeleton (asmdef + folders)
  - Area: Arch | Priority: P1 | Size: M | Status: done
  - Desc: Implement add/remove/set and change notifications (event or observer) for UI.
  - DoD: Subscribers receive updates on stock changes; no GC spikes on frequent updates.
  - Links: My project/Assets/Scripts/Domain/Economy/EconomyState.cs:1
  - Depends: S1-02

- ID: S1-04 — EconomyManager Tick API
  - Area: Economy | Priority: P2 | Size: S | Status: done
  - DoD: Tick(float dt) advances economy counters; safe under pause (dt=0) path.
  - Links: My project/Assets/Scripts/Domain/Economy/EconomyManager.cs:1
  - Depends: S1-03 — EconomyState with stocks + events
  - Area: Economy | Priority: P1 | Size: M | Status: done
  - DoD: Service constructs economy and exposes references; start-new-game use case seeds state.
  - Links: My project/Assets/Scripts/Application/Services/GameStateService.cs:1, My project/Assets/Scripts/Application/UseCases/StartNewGame.cs:1
  - Depends: S1-03 — EconomyState with stocks + events
  - Area: Economy | Priority: P1 | Size: M | Status: done
  - DoD: SaveDto with version; serialize/deserialize EconomyState; round-trip works.
  - Links: My project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs:1, UnityProject/Assets/Scripts/Application/UseCases/{SaveGame.cs,LoadGame.cs}:1
  - Depends: S1-05 — GameStateService composition
  - Area: Arch | Priority: P1 | Size: S | Status: done
  - DoD: Hotkey adds materials (+10) and UI updates accordingly.
  - Links: My project/Assets/Scripts/Presentation/Input/InputController.cs:1
  - Depends: S1-07 — HUDController counters (prototype)
  - Area: UI | Priority: P1 | Size: S | Status: done
  - DoD: Create ResourceConfig/UnitConfig/GameConfig assets; editable in editor; no runtime logic.
  - Links: UnityProject/Assets/ScriptableObjects/Configs/*.cs:1
  - Depends: S1-01 — Project skeleton (asmdef + folders)
  - Area: Arch | Priority: P1 | Size: M | Status: done
  - DoD: Spawn prefab with UnitView; serialized UnitStats; click-to-spawn stub in scene.
  - Links: My project/Assets/Scripts/Domain/Units/UnitStats.cs:1, My project/Assets/Scripts/Presentation/View/UnitView.cs:1
  - Depends: S1-10 — Editor scene bootstrap
  - Area: Arch | Priority: P1 | Size: S | Status: done
  - DoD: Click sets destination; UnitView teleports/moves linearly; will be replaced by pathfinding later.
  - Links: My project/Assets/Scripts/Presentation/View/UnitView.cs:1
  - Depends: S2-02

- ID: S2-04 вЂ” Combat basics
  - Area: Combat | Priority: P2 | Size: S | Status: in-progress
  - DoD: DamageType + CombatSimulator.ResolveAttack(); trivial damage application verified.
  - Links: My project/Assets/Scripts/Domain/Combat/{DamageType.cs,CombatSimulator.cs}:1
  - Depends: S2-02

- ID: S2-05 — Building placement API
  - Area: Build | Priority: P1 | Size: M | Status: done
  - DoD: Validate cost vs EconomyState; success/failure updates stocks; no visuals required.
  - Links: My project/Assets/Scripts/Domain/Build/BuildingService.cs:1, My project/Assets/Scripts/Domain/Build/BuildResult.cs:1, My project/Assets/Scripts/Application/UseCases/PlaceBuilding.cs:1, My project/Assets/Scripts/Infrastructure/Configs/BuildingConfig.cs:1, My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs:69
  - Depends: S1-03, S1-05, S1-09 - Config ScriptableObjects (resources/units/game)

- ID: S2-06 — Research state & data
  - Area: Research | Priority: P2 | Size: M | Status: in-progress
  - DoD: Research entry state (Locked/Queued/Done); effects table placeholder; data-driven from SO.
  - Links: My project/Assets/Scripts/Domain/Research/{ResearchStatus.cs,ResearchStore.cs}:1, My project/Assets/Scripts/Infrastructure/Configs/ResearchConfig.cs:1, My project/Assets/Scripts/Application/UseCases/{StartResearch.cs,CompleteResearch.cs}:1
  - Depends: S1-09
- ID: S2-07 вЂ” SaveSystem integration (units/buildings/research)
  - Area: Persistence | Priority: P1 | Size: M | Status: todo
  - DoD: Save/Load covers core game state beyond economy; round-trip works.
  - Links: My project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs:1
  - Depends: S2-02, S2-05, S2-06

- ID: S2-08 вЂ” UI panels (resources/actions)
  - Area: UI | Priority: P2 | Size: M | Status: todo
  - DoD: Simple panels reflect state changes; minimal input hints; no GC spikes.
  - Links: My project/Assets/Scripts/Presentation/UI/HudController.cs:1
  - Depends: S1-07 — HUDController counters (prototype)
  - Area: UI | Priority: P1 | Size: S | Status: done
  - DoD: Select grid resolution, neighbor policy, and cost heuristics; document in DECISIONS.md.
  - Links: docs/DECISIONS.md:1
  - Depends: S2-01

- ID: S2-10 вЂ” Persistence tests (manual)
  - Area: CI | Priority: P3 | Size: S | Status: todo
  - DoD: Scripted steps in README to verify save/load of all systems.
  - Links: README.md:1
  - Depends: S2-07

- ID: S2-09 — Pathfinding planning
  - Area: AI/Path | Priority: P3 | Size: S | Status: done
  - DoD: Select grid size, neighbor policy, base costs, and heuristic; decisions recorded in DECISIONS.md.
  - Links: docs/DECISIONS.md:1
  - Depends: S2-01
## Notes

- Keep Domain/Application free from Unity API; use adapters in Infrastructure/Presentation.
- Prefer event-driven UI updates; accept polling as temporary solution in prototype.
- Defer Addressables and full CI until content scale requires them.






