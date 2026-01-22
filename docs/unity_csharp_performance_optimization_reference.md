# Unity C# Performance Optimization Reference

This file is a working reference, not a research dump.

It is optimized for fast navigation and practical decisions during implementation, review, profiling, and debugging.

Primary goal:

- help the coding agent quickly choose the right optimization direction
- reduce time wasted on low-value micro-tweaks
- connect general Unity performance advice to this specific project

## How To Use This File

Start here only when the task is specifically about code/runtime performance.

If the task starts from a symptom:

- use [debug_playbooks.md](./debug_playbooks.md) first
- use [runtime_switches.md](./runtime_switches.md) for inspector/runtime knobs
- use [code_id_index.md](./code_id_index.md) to jump into the actual hot files

Use this file for:

- deciding optimization priority
- choosing between algorithm/data-structure changes vs micro-optimizations
- avoiding common Unity/C# performance traps
- reviewing a proposed optimization before implementing it

## Optimization Priority Order

Always optimize in this order:

1. Remove unnecessary work.
2. Improve algorithmic complexity and data layout.
3. Reduce repeated scene queries and repeated traversal.
4. Eliminate avoidable allocations and GC pressure.
5. Time-slice expensive work across frames.
6. Parallelize or use Jobs/Burst only when the previous steps are already solid.
7. Apply micro-optimizations only after profiling confirms they matter.

Rule:

- "shorter code" does not mean "faster code"
- "clever code" is usually worse than "less work"

## Baseline Profiling Workflow

Use this workflow before and after any optimization:

1. Reproduce the problem in the real target scene, not in an artificial mini scene unless the bug requires isolation.
2. Check Unity Stats for high-level symptom confirmation.
3. Check Unity Profiler:
   - CPU Usage
   - GC.Alloc
   - Rendering
   - Memory
4. If the bug is allocation-related, inspect `GC.Alloc` callsites first.
5. If the bug is frame-time related, identify the top hot functions by total time.
6. Fix the largest hot path first.
7. Re-profile immediately after the change.

Do not start with:

- Deep Profile on the whole game
- refactoring random utility code
- replacing syntax without evidence

## High-Value Rules

### 1. No avoidable allocations in hot paths

Target:

- ideally `0 B/frame` on stable simulation paths
- especially in `Update`, `LateUpdate`, `FixedUpdate`, combat loops, path loops, chunk generation loops

Avoid in hot code:

- creating new `List<>`, `Dictionary<>`, arrays, strings each frame
- LINQ chains in per-frame logic
- `yield return new WaitForSeconds(...)` inside repeating coroutines
- boxing through interfaces or generic misuse
- iterator/state-machine overhead in tight loops

Prefer:

- reusable buffers
- cached collections cleared and reused
- pooled temporary objects
- precomputed lookup tables

### 2. Cache expensive lookups

Avoid repeated:

- `GetComponent`
- `FindObjectOfType`
- hierarchy walks
- repeated path/target resolution from scratch

Prefer:

- cache in `Awake` / `Start`
- explicit registries
- hash maps / spatial hashes
- stable singleton/service references where appropriate

### 3. Replace repeated scans with indexed access

Bad pattern:

- nested loops over units
- repeated `List.Find`, `FirstOrDefault`, `Where(...).ToList()`

Prefer:

- `Dictionary<TKey, TValue>`
- `HashSet<T>`
- spatial hash
- occupancy grids
- squad/group caches
- flow fields instead of per-unit re-pathing where possible

### 4. Time-slice heavy work

If a task does not need to finish in one frame, distribute it:

- path building
- chunk generation
- prop placement
- far-view baking
- expensive refreshes and scans

Use:

- per-frame budgets
- item-per-frame caps
- dynamic limits when frame time rises

### 5. Optimize data flow, not just syntax

Often the best optimization is:

- computing something once and reusing it
- switching from per-unit logic to per-group logic
- moving from pull to push updates
- collapsing duplicated systems

### 6. Jobs/Burst are not first-line fixes

Use Jobs/Burst when:

- the operation is large-N
- data is regular
- main-thread structure is already clean
- you have eliminated obvious redundant work first

Do not use Jobs/Burst just to hide poor algorithmic choices.

## Unity-Specific C# Pitfalls

### LINQ in hot paths

Usually avoid LINQ in:

- `Update`
- combat loops
- chunk generation
- pathfinding support loops

LINQ may be acceptable in:

- editor code
- startup/bootstrap code
- infrequent tooling code

### Strings

Avoid:

- string concatenation in loops
- building debug text every frame if HUD is hidden

Prefer:

- `StringBuilder`
- cached labels updated only when values change
- throttled diagnostics

### Coroutines

Coroutines are fine for orchestration, but:

- cache repeated wait instructions
- do not spawn large numbers of short-lived coroutines unnecessarily
- do not confuse coroutine count with "free async work"

### FixedUpdate

Be careful:

- low FPS can cause multiple `FixedUpdate` calls per rendered frame
- expensive logic in `FixedUpdate` scales badly under stress

Put physics-only or fixed-step simulation there, not general gameplay logic by default.

### Async/Task

Use cautiously in Unity runtime logic.

Prefer:

- synchronous logic for frame-bound work
- coroutines for simple Unity-side orchestration
- jobs for heavy parallel data work

Do not add `async/await` to hot gameplay code without a clear reason and profiling evidence.

## Data Structure Heuristics

Use:

- `List<T>` for ordered dense collections
- `Dictionary<TKey, TValue>` for frequent keyed lookup
- `HashSet<T>` for frequent membership checks
- structs for small value-like data
- classes for identity-bearing, stateful objects

Be careful with:

- large structs copied frequently
- interface-based iteration in hot code
- collections recreated on every query

## Pooling Rules

Pool when objects are:

- spawned/despawned frequently
- simple to reset
- numerous enough to pressure GC or instantiation cost

Typical pool candidates:

- bullets
- temporary VFX
- damage popups
- reusable temp buffers

Typical non-pool candidates:

- rare persistent scene objects
- systems with complex hidden state that is expensive to reset correctly

## Rendering-Adjacent CPU Rules

Even when the problem looks "rendering-related", code often causes it indirectly.

Check:

- too many active objects
- too many transform updates
- too much per-frame sorting/filtering
- repeated culling logic on the CPU
- rebuilding data or visuals that did not actually change

## Project-Specific Hot Paths

These are the first files to inspect for performance work in this project.

### World generation, streaming, far-view

- `SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT`
- `PENV-03`
- `PENV-05`
- `PENV-07`
- `PENV-09`
- `PENV-13`

Typical issues:

- chunk generation doing too much in one frame
- props/trees generation pressure
- far-view bake queue pressure
- duplicate work between streaming and bake systems

### Hex grid and walkability

- `SCRIPTS-PRESENTATION-PATHFINDING-HEXPATHFINDINGBOOTSTRAP`
- `HPFB-01`
- `HPFB-03`
- `HPFB-04`

Typical issues:

- oversized grid allocations
- too-frequent walkability updates
- mismatch between world generation extents and grid extents

### Paths and path requests

- `SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER`
- `SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE`
- `PMGR-01`
- `PQUE-01`
- `PQUE-03`
- `PQUE-04`

Typical issues:

- too many path rebuilds
- queue thrash
- stale target churn
- path requests for units that no longer need them

### Flow fields and squads

- `SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER`
- `SCRIPTS-PRESENTATION-PERFORMANCE-ENEMYSQUADMANAGER`
- `FFLD-01`
- `FFLD-03`
- `FFLD-06`
- `ESQD-03`
- `ESQD-04`

Typical issues:

- fields rebuilt too often
- squads entering unstable state loops
- per-unit logic overriding macro flow too aggressively

### Unit combat

- `SCRIPTS-PRESENTATION-VIEW-UNITCOMBAT`
- `UCOM-03`
- `UCOM-06`
- `UCOM-08`

Typical issues:

- target scans
- repeated combat reset logic
- ORCA/path/attack systems fighting each other

### Local avoidance and movement

- `SCRIPTS-PRESENTATION-PERFORMANCE-ORCAAVOIDANCESYSTEM`
- `SCRIPTS-PRESENTATION-PERFORMANCE-MOVEMENTJOBSYSTEM`
- `ORCA-03`
- `ORCA-04`

Typical issues:

- too many neighbors
- large avoidance radius
- unnecessary work for units that should ignore ORCA in current state

## Practical Review Checklist

When reviewing a performance change, ask:

1. Does this remove work, or just move it around?
2. Does this improve asymptotic cost?
3. Does this reduce allocations?
4. Does this reduce scene queries or repeated traversal?
5. Does this make scheduling/frame budgets more predictable?
6. Does this create stale-cache or correctness risk?
7. Does this fight another optimization already present in the project?

If the answer is only:

- "it looks cleaner"
- "it uses newer syntax"
- "it should be faster"

then it is not enough.

## Performance Patterns To Prefer

Prefer:

- cached buffers
- explicit budgets
- coarse-to-fine processing
- batch updates
- shared results for groups
- stable ownership of data
- incremental rebuilds instead of full rebuilds

Avoid:

- global full refreshes every frame
- hidden allocations
- repeated conversions between coordinate spaces
- multiple systems solving the same problem in parallel
- unnecessary polling

## When Micro-Optimizations Are Worth It

Micro-optimizations are worth it only if all of these are true:

- the code is already on a proven hot path
- larger architectural wins are exhausted or already implemented
- the micro-change is measurable
- the readability loss is acceptable

Typical examples:

- replacing allocation-heavy helper patterns in a tight loop
- converting a hot query to non-alloc buffer fill
- avoiding boxing or iterator overhead in very hot code

## Measurement Notes

Trust, in order:

1. Unity Profiler on representative scene/runtime
2. `GC.Alloc` traces
3. frame time under stress
4. memory snapshots for leaks and growth
5. external benchmark harnesses only for isolated pure-C# utility code

Be careful with:

- editor-only profiling conclusions
- Deep Profile as the only evidence
- benchmarks that do not resemble runtime usage

## Anti-Patterns

Red flags:

- allocating a collection inside `Update`
- calling `GetComponent` repeatedly in per-frame code
- LINQ over large collections every frame
- rebuilding strings for hidden UI
- pathfinding and steering both recomputing the same intent
- multiple systems maintaining partially duplicated occupancy state
- using async abstractions where a simple budgeted queue would do

## Decision Table

| Symptom | First suspect | First action |
|---|---|---|
| GC spikes | temp allocations in hot loops | inspect `GC.Alloc`, remove per-frame allocations |
| Large frame spikes during generation | chunk work too large | reduce per-frame budget, cache results, split phases |
| Combat scales badly with more units | target scans / path churn / ORCA cost | inspect `UnitCombat`, `PathRequestQueue`, `EnemySquadManager`, `OrcaAvoidanceSystem` |
| Camera movement causes hitches | streaming/bake work | inspect `ProceduralEnvironment` scheduling and chunk radius |
| Many units overlap or jitter | multiple movement systems conflicting | inspect ORCA vs squad/flow/path ownership |
| Memory grows too much on large maps | oversized grids / chunk retention / bake textures | inspect `HexPathfindingBootstrap`, chunk caching, far-view settings |

## Working Rule For This Project

For this codebase specifically:

- prefer fixing ownership and duplicate-work issues before touching syntax
- prefer worldgen/streaming scheduling fixes before low-level rendering guesses
- prefer squad/group solutions before per-unit path churn
- prefer non-alloc and cache-first patterns in combat/movement code
- prefer incremental and bounded work over full-scene refreshes

## Related Project Docs

- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)
- [debug_playbooks.md](./debug_playbooks.md)
- [runtime_switches.md](./runtime_switches.md)
- [unity_mcp_tools.md](./unity_mcp_tools.md)

## Status

This file is intentionally edited into a practical reference.

If new optimization knowledge is added later, keep it:

- short
- actionable
- tied to profiling decisions
- tied to project hot paths when possible

Do not turn it back into a long unreadable research archive.
