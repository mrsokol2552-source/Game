/*
@file: My project/Assets/Scripts/Presentation/Performance/StuckResolver.cs
@module: presentation.performance.stuck_resolver
@purpose: Detects movers that fail to make progress and orchestrates recovery nudges or combat repaths.
@entry: StuckResolver.Update, STUCK-02
@api: scene MonoBehaviour singleton used by movement/combat path recovery
@deps: PathManager, UnitView, UnitCombat, UnitPathFollower
@data: per-unit movement progress windows, stale-state cleanup, throttled log counters
@perf: light-medium periodic scan over active units; bounded by Interval and UnitView.All
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual stuck-unit recovery verification
@config: Interval, WindowSeconds, MinTravelDistance, NudgeRadius, ResolveCooldown
@assets: none
@notes: must remain subordinate to flow-field and squad ownership; only FreeCombat units may be nudged
*/

using System.Collections.Generic;
using Game.Presentation.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PERFORMANCE-STUCKRESOLVER]
// Logical block: Scripts/Presentation/Performance/StuckResolver.

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Detects units that are moving but not making progress and nudges them.
    /// </summary>
    public partial class StuckResolver : MonoBehaviour
    {
        // [STUCK-01]
        // Resolver config, singleton ownership, per-unit progress windows, and throttled log state.
        public static StuckResolver Instance { get; private set; }

        [Tooltip("Enable stuck detection and resolution.")]
        public bool Enabled = true;
        [Tooltip("How often to sample unit progress (seconds).")]
        public float Interval = 0.2f;
        [Tooltip("Window size for accumulated movement (seconds).")]
        public float WindowSeconds = 0.8f;
        [Tooltip("Minimum distance that should be traveled during the window to be considered not stuck.")]
        public float MinTravelDistance = 0.12f;
        [Tooltip("Minimum current speed to consider a unit trying to move.")]
        public float MinSpeed = 0.05f;
        [Tooltip("Minimum distance to destination to avoid false positives when arriving.")]
        public float ArrivalSlack = 0.2f;
        [Tooltip("How many hexes to search for a nudge target.")]
        public int NudgeRadius = 2;
        [Tooltip("Cooldown between stuck resolutions per unit (seconds).")]
        public float ResolveCooldown = 1.2f;
        [Tooltip("Allow nudging units with manual paths.")]
        public bool AllowManualPathNudge = false;
        [Tooltip("Force combat repath when a combat unit is stuck.")]
        public bool ForceCombatRepath = true;
        [Tooltip("Log stuck events (throttled).")]
        public bool LogStuck = false;
        public int MaxLogsPerTick = 3;

        private float _timer;
        private readonly Dictionary<int, StuckState> _states = new Dictionary<int, StuckState>(256);
        private readonly List<int> _staleKeys = new List<int>(128);
        private int _logsThisTick;

        private struct StuckState
        {
            public Vector3 LastPos;
            public float WindowTime;
            public float Moved;
            public float LastResolveTime;
            public int LastSeenFrame;
        }

        // [STUCK-02]
        // Periodic progress sampling across active units, with squad/flow/manual guards and recovery dispatch.
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Enabled) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;
            _logsThisTick = 0;

            var pm = PathManager.Ensure();
            int frame = Time.frameCount;
            foreach (var uv in UnitView.All)
            {
                if (uv == null || !uv.isActiveAndEnabled) continue;
                var follower = uv.GetComponent<UnitPathFollower>();
                bool hasPath = follower != null && follower.HasPath;
                bool hasDest = uv.HasDestination;
                if (!hasPath && !hasDest)
                {
                    ResetState(uv, frame);
                    continue;
                }
                if (hasPath && follower != null && follower.Source == UnitPathFollower.PathSource.Manual && !AllowManualPathNudge)
                {
                    ResetState(uv, frame);
                    continue;
                }
                var uc = uv.GetComponent<UnitCombat>();
                if (uc != null)
                {
                    if (uc.IsUsingFlowField)
                    {
                        ResetState(uv, frame);
                        continue;
                    }
                    if (uc.IsInSquad && uc.CurrentSquadMode != UnitCombat.SquadMode.FreeCombat)
                    {
                        ResetState(uv, frame);
                        continue;
                    }
                }

                int id = uv.GetInstanceID();
                if (!_states.TryGetValue(id, out var state))
                {
                    state = new StuckState
                    {
                        LastPos = uv.transform.position,
                        WindowTime = 0f,
                        Moved = 0f,
                        LastResolveTime = -999f,
                        LastSeenFrame = frame
                    };
                }

                var pos = uv.transform.position;
                state.Moved += (pos - state.LastPos).magnitude;
                state.LastPos = pos;
                state.WindowTime += Interval;
                state.LastSeenFrame = frame;

                if (state.WindowTime >= WindowSeconds)
                {
                    bool nearArrival = false;
                    if (uv.TryGetDestination(out var dest))
                    {
                        float d = (dest - pos).magnitude;
                        var m = uv.GetMovementSettings();
                        float slack = Mathf.Max(ArrivalSlack, m.StopDistance * 2f);
                        if (d <= slack) nearArrival = true;
                    }

                    if (!nearArrival && uv.GetSpeed() >= MinSpeed && state.Moved < MinTravelDistance)
                    {
                        if (Time.time - state.LastResolveTime >= ResolveCooldown)
                        {
                            ResolveStuck(uv, pm, uc);
                            state.LastResolveTime = Time.time;
                        }
                    }
                    state.WindowTime = 0f;
                    state.Moved = 0f;
                }

                _states[id] = state;
            }

            CleanupStale(frame);
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("StuckResolver");
            go.AddComponent<StuckResolver>();
        }
    }
}
