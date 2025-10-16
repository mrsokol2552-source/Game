# Sprint Plan (2 x 2 weeks)

Sources: NEAR_TERM.md, Master Plan (Unified), DECISIONS.md. No dates — only tasks, dependencies, and DoD.

## Guiding Decisions

- Architecture: 4 layers (Domain/Application/Infrastructure/Presentation); Unity API only in Presentation/Infrastructure; asmdef per layer; manual DI.
- Time/Loop: tick via `Time.deltaTime`; pause-safe; no day-based estimates.
- Persistence: JSON (System.Text.Json); single SaveDto with versioning; scope grows from Economy to Units/Build/Research in Sprint 2.
- UI: target uGUI; OnGUI allowed for prototype only; event-driven updates from domain state.
- Data: ScriptableObjects for configs (resources/units/buildings/research) with `ResourceAmount[]` costs.
- Scope: performance, balancing, and content scale are out for these two sprints.

## Sprint 1 — Foundations

- Project skeleton [Area: Arch]
  - Create Unity layered structure (Domain/Application/Infrastructure/Presentation) with asmdef per layer.
  - Stub ScriptableObjects for resources/units/config.
  - DoD: project opens; asmdefs compile; empty scene runs; layers match STANDARDS.md.
- Core domain: Economy basics [Area: Economy]
  - Define ResourceType enum and ResourceAmount struct.
  - EconomyState with stocks; EconomyManager tick API (no balancing yet).
  - DoD: add/remove resources; tick updates counters; UI notified via events or polling; basic playmode smoke test.
- Save/Load scaffolding [Area: Persistence]
  - Define SaveGame/LoadGame use-cases, SaveSystem with JSON and DTOs.
  - DoD: EconomyState round-trips in editor; SaveDto contains version.
- UI/HUD skeleton [Area: UI]
  - HUDController shows resource counters; InputController captures basic actions.
  - DoD: playmode displays counters from EconomyState; no GC spikes on updates.
- Build/Research data models [Area: Build/Research]
  - ScriptableObject stubs for buildings and research entries with cost fields.
  - DoD: assets can be created; visible in editor; linked to HUD display-only.
- CI shell (optional) [Area: CI]
  - Repo structure + placeholder pipeline config (manual for now).
  - DoD: README with local build/test instructions.

## Sprint 2 — Systems

- Units & movement scaffolding [Area: Units]
  - UnitStats (speed, health); UnitView placeholder; GridPathfinder interface.
  - DoD: spawn unit prefab; click-to-move demo stub (no final pathfinding yet).
- Combat basics [Area: Combat]
  - DamageType enum; CombatSimulator API with ResolveAttack stub.
  - DoD: method contracts exist; trivial damage verified in editor.
- Construction loop (minimal) [Area: Build]
  - Building placement API (no visuals), cost check against EconomyState.
  - DoD: failing/succeeding placement changes stocks accordingly.
- Research unlocks (data-driven) [Area: Research]
  - Research entry state (Locked/Queued/Done); unlock effects placeholder.
  - DoD: can mark research done and read its effects data.
- Persistence integration [Area: Persistence]
  - Include units/buildings/research states in SaveSystem.
  - DoD: full round-trip of core game state in editor.
- UX polish (minimal) [Area: UI]
  - Basic panels: resources, actions; simple input hints.
  - DoD: UI responds to state changes without GC spikes.

## Dependencies

- Sprint 1 “Project skeleton” precedes all other work.
- EconomyState precedes UI counters and construction costs.
- SaveSystem depends on domain DTOs; start with Economy in Sprint 1.
- Pathfinding implementation can be deferred; interfaces land in Sprint 2.

## Tickets

- See TICKETS.md for detailed Sprint 1/2 breakdown (IDs, DoD, dependencies).

## Notes

- Balancing/content/performance are out of scope here; goal is a minimal skeleton and end-to-end data flow.
- Conflicting or unclear items are tracked in OPEN_QUESTIONS.md and accepted decisions in DECISIONS.md.
