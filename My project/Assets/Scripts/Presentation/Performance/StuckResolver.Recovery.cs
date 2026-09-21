/*
@file: My project/Assets/Scripts/Presentation/Performance/StuckResolver.Recovery.cs
@module: presentation.performance.stuck_resolver
@purpose: Extracted stuck-unit recovery actions for nudge-based movement rescue and optional combat repath.
@entry: STUCK-03
@api: StuckResolver partial recovery helpers
@deps: PathManager, UnitView, UnitCombat
@data: nudge targets and throttled warning logs
@perf: lightweight; executes only when the main stuck scan detects a stalled mover
@thread: main thread only
@tests: manual stuck-unit recovery verification
@config: ForceCombatRepath, NudgeRadius, LogStuck, MaxLogsPerTick
@assets: none
@notes: keeps recovery side effects isolated from progress sampling so future movement policies can replace this layer cleanly
*/

using Game.Presentation.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-RECOVERY]
// Logical block: StuckResolver recovery extraction.

namespace Game.Presentation.Performance
{
    public partial class StuckResolver
    {
        // [STUCK-03]
        // Recovery actions: optional combat repath followed by nearest-free nudge and throttled diagnostics.
        private void ResolveStuck(UnitView uv, PathManager pm, UnitCombat uc)
        {
            if (uv == null || pm == null) return;
            if (ForceCombatRepath && uc != null)
            {
                uc.ForceRepath();
            }

            if (pm.TryFindNearestFreeWorld(uv.transform.position, uv, NudgeRadius, out var free))
            {
                uv.SetDestination(free);
                if (LogStuck && (MaxLogsPerTick <= 0 || _logsThisTick < MaxLogsPerTick))
                {
                    _logsThisTick++;
                    Debug.LogWarning($"[StuckResolver] nudged {uv.name} to {free} (combat={uc != null})");
                }
            }
        }
    }
}
