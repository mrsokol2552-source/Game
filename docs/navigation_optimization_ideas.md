# Navigation Optimization Ideas (Consolidated)

This file consolidates the practical, high-impact ideas from the two removed architecture docs. It focuses on changes that align with the current Unity RTS codebase and the goal of scaling to large unit counts.

## Implemented (baseline to keep)
- FlowFieldManager with time-sliced BFS, per-target caching, distance-limited expansion, and optional LoS smoothing.
- Tiled flow fields: coarse tile graph with active tile gating along the coarse path.
- Deterministic flow selection: neighbor order biased toward target direction to reduce oscillation.
- Flow vector sampling: weighted neighbor directions with sub-cell step for smoother motion.
- Influence costs: per-faction threat maps (enemy units) bias flow steps away from danger.
- LoS flags in integration field: cached line-of-sight to target avoids repeated checks during smoothing.
- Squad flow + formation targets: squad center advances via flow fields; units follow formation slots around the moving anchor.
- Group-level fields instead of per-unit path requests: squads rely on flow fields/direct steering, avoiding per-unit path builds.
- Dual hash: static obstacle hash + dynamic unit occupancy for faster blocked/occupied queries.
- Pre-baked cover slots + spatial hash: cover slots around static obstacles are cached for fast queries.
- Jobified movement (MovementJobSystem) with ORCA velocity overrides.
- ORCA/RVO avoidance with cohesion bias and combat gating near attack range.
- Squad system (group-centric combat) with dynamic gather radius and state machine.
- Jobified targeting (spatial hash) with OccupancyHash fallback.
- StuckResolver for low-progress movers.
- Dirty-rectangle obstacle rebake: partial physics rebake and partial native walkable updates.
- Discomfort/crowd costs: per-cell crowd penalties bias flow steps away from dense clusters.
- ORCA priority classes: per-unit priority reduces avoidance responsibility.
- Formation offsets on arrival: per-unit formation slots near the target reduce stacking.
- ORCA->Movement double-buffering: velocity overrides can be reused for one frame to decouple job timing.
- Job pipeline coordinator: ORCA + Movement are driven in a fixed order by a central coordinator.
- UnitSoARegistry: centralized SoA snapshot for ORCA inputs to reduce redundant per-unit collection.
- Targeting job can reuse UnitSoARegistry snapshots to reduce per-unit transform reads.
- Data-driven combat profiles: `UnitCombatProfile` centralizes behavior tuning for ECS-friendly configs.
- Data-driven behavior profiles: `UnitBehaviorProfile` centralizes aggro/leash/hold tuning.

## Long-term / expensive changes
- Data-oriented unit storage (SoA/ECS) for movement, targeting, and avoidance.
- ECS-friendly behavior trees / data-driven logic if micro-tactics scale up.
