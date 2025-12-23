\# Architecture of High-Performance Navigation and Targeting Systems in 2D RTS Strategies



\## 1. Introduction: Analysis of Current Architecture and Scalability Challenges



Developing real-time strategy (RTS) games is one of the most complex tasks in game systems engineering. Unlike shooters or RPGs, where the number of active agents rarely exceeds several dozen, RTS games require the simulation of hundreds or thousands of units. Each must make decisions, find paths, and interact with the environment within strict frame time limits (16.6 ms for 60 FPS).



Analysis of the provided codebase (UnitCombat.cs, PathManager.cs, and related components) reveals a classic hybrid Unity architecture. High-level logic runs on the main thread via MonoBehaviour, while resource-intensive tasks (targeting) are partially offloaded to the C# Job System. While the current implementation shows an understanding of basic optimizations like object pooling and component caching, it contains fundamental architectural limitations that hinder scaling to the level of genre benchmarks like \*StarCraft 2\* or \*Supreme Commander\*.

For a file-level map of the current implementation, see `docs/code_map.md`.



\### 1.1 Critical Analysis of UnitCombat.cs and UnitCombatJobScheduler.cs



In the current implementation, targeting is centralized through a jobified spatial hash. Every combat tick (default `CombatTickInterval` 0.04s + jitter), an agent resolves a target from the job scheduler (`\_jobNearest`), with forced squad targets as an override. If the scheduler is disabled, it falls back to `OccupancyHash` neighbor queries. The previous `FindNearestEnemy()` O(N^2) fallback is removed, eliminating the worst-case cost but making acquisition dependent on job cadence and hash coverage.



\*\*Status:\*\* The code now follows the CollectUnits -> BuildHash (Job) -> QueryHash (Job) -> CacheTarget (Main) chain using `NativeParallelMultiHashMap`. The remaining risk is latency gaps when the scheduler is disabled or hash rings are too small, so targets may appear only once squads assign a forced target or the next job tick completes.



\*\*Current pipeline:\*\* CollectUnits -> BuildHash (Job) -> QueryHash (Job) -> CacheTarget (Main).

\* \*\*CollectUnits\*\*: Gathers active agents into dense arrays.

\* \*\*BuildHash\*\*: Incrementally updates a uniform grid/Spatial Hash.

\* \*\*QueryHash (Job)\*\*: Searches for the nearest targets in neighboring buckets and writes the result to a NativeArray.

\* \*\*CacheTarget\*\*: In MonoBehaviour, only reads/updates the cache until its TTL expires, without performing synchronous searches.



\### 1.2 PathManager.cs and Memory Management



The centralized path builder returns a `List<Vector3>`. In .NET and Unity, memory management is critical. Even with list pooling, working with reference types and heap-allocated collections puts pressure on the Garbage Collector (GC). The absence of a limit on path requests (`MaxBuildsPerFrame = 0`) is a critical risk. In a scenario where a player selects 200 units and issues a move command, the system will attempt to calculate 200 A\* paths in a single frame, leading to a performance spike.


Current state notes: `PathRequestQueue` now limits throughput (`MaxPerFrame=32`, `MaxQueueSize=512`) and jobs are enabled by default. This reduces spikes when requests go through the queue. However, `PathManager.MaxBuildsPerFrame` still defaults to 0 (unbounded), so direct synchronous `BuildPath` bursts can still stall a frame.



\### 1.3 Reactive Collision Resolution in CrowdingResolver.cs



The `CrowdingResolver` component, operating at 0.12s intervals, tries to resolve unit overlaps by directly changing positions or assigning short paths. This is a \*\*reactive\*\* approach: the problem is addressed \*after\* it has occurred. Professional RTS engines use a \*\*predictive\*\* approach (RVO/ORCA or Steering Behaviors), where units adjust their velocity vectors \*before\* a collision. The current implementation will inevitably lead to visual artifacts like jittering or "teleportation" at high unit densities.


Current state notes: The resolver now runs in `LateUpdate`, groups units by hex cell, keeps `AllowStayCountPerCell`, and nudges extras with short `SetDestination` moves. It uses `MoveCooldown` to avoid ping-pong, adaptive throttling based on frame time, and a short no-enemy window to resolve stacks after combat ends. It intentionally avoids path builds to reduce cost.



---



## 1.4 Current Implementation Snapshot (Applied Changes)

The current project already applies several of the architectural recommendations in code:

- Jobified targeting: `UnitCombatJobScheduler` builds a spatial hash (`NativeParallelMultiHashMap`) and resolves nearest enemies in an `IJobParallelFor`.
- Target resolution: `UnitCombat` uses job results with TTL and forced squad targets; the O(N^2) fallback scan was removed.
- Squad coordination: `EnemySquadManager` groups enemies and can optionally drive player squads (`DrivePlayers=true`) with shared targets.
- Path job pipeline: `PathRequestQueue` schedules `HexPathfinderJob` when native walkable data is available and falls back to `PathManager.BuildPath` when needed.
- Occupancy fast path: `OccupancyHash` rebuilds each frame and provides fast occupancy checks for `PathManager`.
- Diagnostics: `PathProfiler` and optional reset/jitter logs are in place for chase and movement tuning.



---



\## 2. Global Navigation: From A\* to Flow Fields



The A\* algorithm is the gold standard for a single agent's pathfinding. However, in an RTS where commands are given to groups, using A\* for each unit individually is an inefficient use of CPU resources.



\### 2.1 Concept and Advantages of Flow Fields



Flow Fields (or Vector Fields) represent a paradigm shift: instead of calculating a path for an agent, we calculate a navigation map for the entire game world surface relative to a specific target.



1\.  \*\*Cost Field\*\*: A static grid storing the traversal cost of each cell.

2\.  \*\*Integration Field\*\*: Using Dijkstra's algorithm starting from the goal, we "fill" the map with total path costs.

3\.  \*\*Flow Field\*\*: For each cell, a vector is calculated pointing to the neighbor with the lowest integration value.



\*\*Key Advantage:\*\* The complexity depends on the map size and the number of targets, but is completely independent of the number of units. For 10,000 units moving to the same point, navigation becomes a simple vector read (an $O(1)$ operation).



\### 2.2 The Eikonal Equation and Smoothing



To avoid "robotic" 45 and 90-degree movement, the \*\*Eikonal equation\*\* ($\\|\\nabla u(x)\\| = F(x)$) allows for high-accuracy approximation of arrival time, creating gradients that aren't strictly tied to cell centers. In Unity, this is implemented using finite differences to find the integration field's density gradients.



---



\## 3. Spatial Hashing for Targeting



`UnitCombat.cs` no longer falls back to `FindNearestEnemy()`. The nearest target is provided by the job scheduler (or `OccupancyHash` if the scheduler is disabled), with forced squad targets as an override. This removes the $O(N^2)$ worst-case, but it also means target acquisition depends on the scheduler interval, hash cell size, and ring count.



\### 3.1 Spatial Hashing vs. Quadtrees



\* \*\*Quadtrees\*\*: Moving a unit requires deleting and re-inserting it, leading to rebalancing and pointer chasing, which is cache-unfriendly.

\* \*\*Spatial Hashing\*\*: Breaks the world into uniform buckets. A unit's coordinate is mathematically converted into an array index: $Index = (Floor(x/CellSize) \\cdot Prime1 + Floor(y/CellSize) \\cdot Prime2) \\% ArraySize$. Updating is an $O(1)$ operation.



---



\## 4. Local Avoidance and Physics



\* \*\*RVO2 (Reciprocal Velocity Obstacles)\*\*: The standard for crowd navigation. It calculates a "velocity cone" that would lead to a collision and chooses a new velocity outside that cone.

\* \*\*"Soft" Collisions (StarCraft 2 Approach)\*\*: Uses \*\*Separation Force\*\* where units have a repulsion radius. Under pressure, they can "squish" slightly, which looks organic for groups like Zerg.



---



\## 5. Practical Recommendations for Refactoring



\### 5.1 Transformation of PathManager.cs

\* \*\*Native Collections\*\*: Use `NativeList<float2>` and pool these buffers.

\* \*\*Time-slicing\*\*: Limit path tasks to a specific budget (e.g., 2 ms per frame).



\### 5.2 Optimization of UnitCombat.cs

\* \*\*Asynchronous Processing\*\*: Use the `BuildHash` -> `QueryHash` -> `CacheTarget` chain.

\* \*\*Double Buffering\*\*: Schedule jobs in Frame $N$ and complete them in Frame $N+1$ to maximize parallelism.



---



\## 6. Comparison Table



| Subsystem | Current Implementation | Recommended Architecture | Expected Gain |

| :--- | :--- | :--- | :--- |

| \*\*Group Pathfinding\*\* | Individual A\* ($O(U \\cdot N)$) | \*\*Flow Field\*\* ($O(N)$) | 10x-100x CPU reduction |

| \*\*Targeting\*\* | Job + spatial hash + forced targets (OccupancyHash fallback only if job disabled) | \*\*Spatial Hashing\*\* | $O(1)$ neighbor access |

| \*\*Avoidance\*\* | Reactive Resolver | \*\*RVO2 (Burst)\*\* | Smooth movement |

| \*\*Memory\*\* | Managed Objects | \*\*Unmanaged Structs\*\* | No GC pauses |



\## 7. Conclusion



To achieve the performance required for thousands of units, the architecture must shift from "unit as an object" to "unit as an index in a data array". Transitioning to Flow Fields, Spatial Hashing, and Burst-compiled Steering Behaviors is essential for modern 2D RTS performance.

