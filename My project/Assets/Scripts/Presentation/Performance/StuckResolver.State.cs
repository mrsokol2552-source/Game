/*
@file: My project/Assets/Scripts/Presentation/Performance/StuckResolver.State.cs
@module: presentation.performance.stuck_resolver
@purpose: Extracted state-reset and stale-entry cleanup helpers for StuckResolver.
@entry: STUCK-04
@api: StuckResolver partial state-maintenance helpers
@deps: UnitView
@data: per-unit progress windows and stale-key scratch buffers
@perf: lightweight; called from the periodic stuck scan
@thread: main thread only
@tests: manual stuck-unit recovery verification
@config: ResolveCooldown
@assets: none
@notes: isolates transient dictionary maintenance from the detection loop for easier future replacement or jobification
*/

using Game.Presentation.View;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER-STATE]
// Logical block: StuckResolver state-maintenance extraction.

namespace Game.Presentation.Performance
{
    public partial class StuckResolver
    {
        // [STUCK-04]
        // Per-unit progress reset and stale-entry cleanup for the rolling stuck-detection state table.
        private void ResetState(UnitView uv, int frame)
        {
            if (uv == null) return;
            int id = uv.GetInstanceID();
            if (_states.TryGetValue(id, out var state))
            {
                state.LastPos = uv.transform.position;
                state.WindowTime = 0f;
                state.Moved = 0f;
                state.LastSeenFrame = frame;
                _states[id] = state;
            }
        }

        private void CleanupStale(int frame)
        {
            if (_states.Count == 0) return;
            _staleKeys.Clear();
            foreach (var kv in _states)
            {
                if (frame - kv.Value.LastSeenFrame > 60)
                    _staleKeys.Add(kv.Key);
            }

            for (int i = 0; i < _staleKeys.Count; i++)
                _states.Remove(_staleKeys[i]);

            _staleKeys.Clear();
        }
    }
}
