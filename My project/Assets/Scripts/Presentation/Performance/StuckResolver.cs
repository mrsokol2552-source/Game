using System.Collections.Generic;
using Game.Presentation.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

namespace Game.Presentation.Performance
{
    /// <summary>
    /// Detects units that are moving but not making progress and nudges them.
    /// </summary>
    public class StuckResolver : MonoBehaviour
    {
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

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("StuckResolver");
            go.AddComponent<StuckResolver>();
        }
    }
}
