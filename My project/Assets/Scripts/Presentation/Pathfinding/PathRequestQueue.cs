using System;
using System.Collections.Generic;
using Game.Presentation.View;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Queue for path requests. Uses jobified hex pathfinder when available; falls back to sync BuildPath.
    /// </summary>
    public class PathRequestQueue : MonoBehaviour
    {
        public static PathRequestQueue Instance { get; private set; }

        [Tooltip("How many path requests to process per frame (0 = unlimited).")]
        public int MaxPerFrame = 32;
        [Tooltip("If true, when queue is idle the request is processed immediately in Enqueue.")]
        public bool ProcessSynchronouslyIfIdle = false;
        [Tooltip("Use HexPathfinderJob if hex grid data is available.")]
        public bool UseJobs = true; // Burst pathfinder job enabled
        [Tooltip("Optional hard cap on queued requests; oldest are dropped if exceeded (0 = no cap).")]
        public int MaxQueueSize = 512;
        [Header("Diagnostics")]
        [Tooltip("Log job/fallback results (throttled per frame).")]
        public bool LogJobResults = false;
        public int MaxLogsPerFrame = 3;

        private readonly Queue<Request> _queue = new Queue<Request>(128);
        private NativeList<int2> _jobPath;
        private JobHandle _jobHandle;
        private bool _jobActive;
        private Request _jobReq;
        private NativeHashMap<int, byte> _occupiedPlayers;
        private NativeHashMap<int, byte> _occupiedEnemies;
        private int _occupiedFrame = -1;
        private HexPathfindingBootstrap _hexCached;
        private static int _jobFrame = -1;
        private static int _jobScheduledThisFrame;
        private static int _jobCompletedThisFrame;
        private static int _jobFallbackThisFrame;
        private int _logFrame = -1;
        private int _logsThisFrame;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("PathRequestQueue");
            Instance = go.AddComponent<PathRequestQueue>();
            DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            if (_jobActive)
            {
                _jobHandle.Complete();
                _jobActive = false;
            }
            if (_jobPath.IsCreated) { _jobPath.Dispose(); _jobPath = default; }
            if (_occupiedPlayers.IsCreated) { _occupiedPlayers.Dispose(); _occupiedPlayers = default; }
            if (_occupiedEnemies.IsCreated) { _occupiedEnemies.Dispose(); _occupiedEnemies = default; }
        }

        public void CompleteActiveJobAndClear()
        {
            if (_jobActive)
            {
                _jobHandle.Complete();
                _jobActive = false;
            }
            _queue.Clear();
            if (_jobPath.IsCreated) _jobPath.Clear();
            if (_occupiedPlayers.IsCreated) _occupiedPlayers.Clear();
            if (_occupiedEnemies.IsCreated) _occupiedEnemies.Clear();
            _occupiedFrame = -1;
        }

        private void Update()
        {
            TouchJobFrame();
            int budget = MaxPerFrame <= 0 ? int.MaxValue : MaxPerFrame;

            while (budget-- > 0)
            {
                if (_jobActive && _jobHandle.IsCompleted)
                {
                    _jobHandle.Complete();
                    TouchJobFrame();
                    FinishJob();
                    _jobActive = false;
                }

                if (_queue.Count == 0) break;

                // If job system is free and enabled, try to schedule the next request
                if (!_jobActive && UseJobs)
                {
                    var peekReq = _queue.Peek();
                    // Check validity before scheduling to avoid blocking the job slot with invalid reqs
                    if (peekReq.Unit == null || !peekReq.Unit.isActiveAndEnabled)
                    {
                        if (_queue.Count > 0) _queue.Dequeue();
                        peekReq.Callback?.Invoke(false, null);
                        continue;
                    }

                    if (TryScheduleJob(peekReq))
                    {
                        if (_queue.Count > 0) _queue.Dequeue(); // Successfully moved to job
                        continue;
                    }
                    // If scheduling failed (e.g. specific condition), fall through to immediate
                }

                // If job is busy OR UseJobs is false OR Schedule failed -> Process Immediate
                if (_queue.Count > 0)
                {
                    var req = _queue.Dequeue();
                    ProcessImmediate(req);
                }
            }
        }

        /// <summary>
        /// Request a path; callback receives (success, worldPath). Path is returned to pool after callback.
        /// </summary>
        public void Enqueue(UnitView unit, Vector3 target, bool allowDiag, bool smooth, Action<bool, List<Vector3>> onDone)
        {
            var req = new Request
            {
                Unit = unit,
                Target = target,
                AllowDiag = allowDiag,
                Smooth = smooth,
                Callback = onDone
            };

            // Optional cap to avoid unbounded growth
            if (MaxQueueSize > 0 && _queue.Count >= MaxQueueSize)
            {
                // drop oldest
                _queue.Dequeue();
            }

            // If queue idle and sync allowed, process immediately; otherwise enqueue
            if (ProcessSynchronouslyIfIdle && _queue.Count == 0 && !UseJobs)
                ProcessImmediate(req);
            else
                _queue.Enqueue(req);
        }

        /// <summary>
        /// Overload with defaults: allowDiag/smooth = true.
        /// </summary>
        public void Enqueue(UnitView unit, Vector3 target, Action<bool, List<Vector3>> onDone)
        {
            Enqueue(unit, target, allowDiag: true, smooth: true, onDone);
        }

        private void ProcessImmediate(Request req)
        {
            TouchJobFrame();
            if (req.Unit == null || !req.Unit.isActiveAndEnabled)
            {
                req.Callback?.Invoke(false, null);
                return;
            }
            var pm = PathManager.Ensure();
            List<Vector3> path = null;
            bool ok = pm.BuildPath(req.Unit, req.Target, req.AllowDiag, req.Smooth, autoFit: false, out path, blockFriendlies: false);
            req.Callback?.Invoke(ok, path);
            if (path != null)
                PathManager.ReturnWorldList(path);
            _jobFallbackThisFrame++;
            if (LogJobResults) Log($"ProcessImmediate {(ok ? "OK" : "FAIL")} len={(path != null ? path.Count : 0)} unit={req.Unit?.name}", req.Unit, req.Target);
        }

        private bool TryScheduleJob(Request req)
        {
            if (!UseJobs) return false;
            if (_jobActive) return false;
            TouchJobFrame();
            if (req.Unit == null || !req.Unit.isActiveAndEnabled) return false;
            var pm = PathManager.Ensure();
            var hex = GetHex();
            if (pm == null || hex == null) return false;

            var walkable = hex.GetWalkableNative();
            if (!walkable.IsCreated) return false;

            var startV2 = hex.WorldToGrid(req.Unit.transform.position);
            var goalV2 = hex.WorldToGrid(req.Target);
            int2 start = new int2(startV2.x, startV2.y);
            int2 goal = new int2(goalV2.x, goalV2.y);

            if (!_jobPath.IsCreated) _jobPath = new NativeList<int2>(Allocator.Persistent);
            else _jobPath.Clear();

            EnsureOccupancySnapshot(hex);
            var selfCombat = req.Unit != null ? req.Unit.GetComponent<Game.Presentation.View.UnitCombat>() : null;
            var selfFaction = selfCombat != null ? (Game.Domain.Units.Faction?)selfCombat.Faction : null;
            NativeHashMap<int, byte> occupied = default;
            if (selfFaction.HasValue)
            {
                if (selfFaction.Value == Game.Domain.Units.Faction.Player)
                    occupied = _occupiedEnemies;
                else if (selfFaction.Value == Game.Domain.Units.Faction.Enemy)
                    occupied = _occupiedPlayers;
            }

            var job = new HexPathfinderJob
            {
                Walkable = walkable,
                Width = hex.Width,
                Height = hex.Height,
                StartCol = start.x,
                StartRow = start.y,
                GoalCol = goal.x,
                GoalRow = goal.y,
                Occupied = occupied,
                MaxNodes = pm.MaxPathNodes > 0 ? pm.MaxPathNodes : 2048,
                Result = _jobPath
            };
            if (LogJobResults) Log($"Job scheduled start={start} goal={goal} grid={hex.Width}x{hex.Height}", req.Unit, req.Target);
            _jobHandle = job.Schedule();
            _jobActive = true;
            _jobReq = req;
            _jobScheduledThisFrame++;
            return true;
        }

        private void FinishJob()
        {
            var pm = PathManager.Ensure();
            var hex = GetHex();
            if (pm == null || hex == null)
            {
                PathProfiler.CountBuild(false);
                _jobReq.Callback?.Invoke(false, null);
                return;
            }

            // try build from job result; fallback to sync BuildPath if job returned empty
            List<Vector3> worldPath = null;
            bool built = false;
            int maxNodes = pm.MaxPathNodes > 0 ? pm.MaxPathNodes : 2048;
            if (_jobPath.Length > 0)
            {
                worldPath = PathManager.RentWorldList();
                // Skip the starting cell to avoid snapping back to the current hex center.
                int limit = Mathf.Min(_jobPath.Length, maxNodes);
                int startIdx = 1;
                if (_jobReq.Unit != null)
                {
                    var currentCell = hex.WorldToGrid(_jobReq.Unit.transform.position);
                    while (startIdx < limit)
                    {
                        var cell = _jobPath[startIdx];
                        if (cell.x == currentCell.x && cell.y == currentCell.y)
                            startIdx++;
                        else
                            break;
                    }
                }
                for (int i = startIdx; i < limit; i++)
                {
                    var cell = _jobPath[i];
                    worldPath.Add(hex.GridToWorld(cell.x, cell.y));
                }
                if (worldPath.Count == 0)
                    worldPath.Add(_jobReq.Target);
                built = worldPath.Count > 0;
            }
            if (!built)
            {
                built = pm.BuildPath(_jobReq.Unit, _jobReq.Target, _jobReq.AllowDiag, _jobReq.Smooth, autoFit: false, out worldPath, blockFriendlies: false);
            }

            int pathLen = worldPath != null ? worldPath.Count : 0;
            if (built && worldPath != null && worldPath.Count > 0)
            {
                if (LogJobResults) Log($"Job OK len={pathLen} source={( _jobPath.Length > 0 ? "job" : "fallback")}", _jobReq.Unit, _jobReq.Target);
                _jobReq.Callback?.Invoke(true, worldPath);
                PathProfiler.CountBuild(true);
                PathProfiler.CountPath(pathLen);
                PathProfiler.CountPathLength(pathLen);
            }
            else
            {
                if (LogJobResults) Log($"Job FAIL (empty path) jobPathLen={_jobPath.Length} len={pathLen}", _jobReq.Unit, _jobReq.Target);
                _jobReq.Callback?.Invoke(false, worldPath);
                PathProfiler.CountBuild(false);
            }
            _jobCompletedThisFrame++;
        }

        private static void TouchJobFrame()
        {
            int f = Time.frameCount;
            if (f == _jobFrame) return;
            _jobFrame = f;
            _jobScheduledThisFrame = 0;
            _jobCompletedThisFrame = 0;
            _jobFallbackThisFrame = 0;
        }

        public struct JobStats
        {
            public int Scheduled;
            public int Completed;
            public int Fallback;
        }

        public static JobStats CollectJobStatsAndReset()
        {
            TouchJobFrame();
            var s = new JobStats
            {
                Scheduled = _jobScheduledThisFrame,
                Completed = _jobCompletedThisFrame,
                Fallback = _jobFallbackThisFrame
            };
            _jobScheduledThisFrame = 0;
            _jobCompletedThisFrame = 0;
            _jobFallbackThisFrame = 0;
            return s;
        }

        private HexPathfindingBootstrap GetHex()
        {
            if (_hexCached == null || !_hexCached.isActiveAndEnabled)
                _hexCached = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            return _hexCached;
        }

        private void EnsureOccupancySnapshot(HexPathfindingBootstrap hex)
        {
            if (hex == null) return;
            if (_jobActive) return;
            int frame = Time.frameCount;
            if (frame == _occupiedFrame && _occupiedPlayers.IsCreated && _occupiedEnemies.IsCreated) return;

            int needed = Mathf.Max(64, UnitCombat.All.Count * 2);
            EnsureMapCapacity(ref _occupiedPlayers, needed);
            EnsureMapCapacity(ref _occupiedEnemies, needed);
            _occupiedPlayers.Clear();
            _occupiedEnemies.Clear();

            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                var cell = hex.WorldToGrid(uc.transform.position);
                int key = (cell.y << 16) ^ (cell.x & 0xFFFF);
                if (uc.Faction == Game.Domain.Units.Faction.Player)
                    _occupiedPlayers.TryAdd(key, 1);
                else if (uc.Faction == Game.Domain.Units.Faction.Enemy)
                    _occupiedEnemies.TryAdd(key, 1);
            }

            _occupiedFrame = frame;
        }

        private static void EnsureMapCapacity(ref NativeHashMap<int, byte> map, int capacity)
        {
            if (!map.IsCreated)
                map = new NativeHashMap<int, byte>(capacity, Allocator.Persistent);
            else if (map.Capacity < capacity)
                map.Capacity = Mathf.Max(map.Capacity * 2, capacity);
        }

        private struct Request
        {
            public UnitView Unit;
            public Vector3 Target;
            public bool AllowDiag;
            public bool Smooth;
            public Action<bool, List<Vector3>> Callback;
        }

        private void Log(string message, UnitView unit, Vector3 target)
        {
            int frame = Time.frameCount;
            if (frame != _logFrame)
            {
                _logFrame = frame;
                _logsThisFrame = 0;
            }
            if (MaxLogsPerFrame > 0 && _logsThisFrame >= MaxLogsPerFrame) return;
            _logsThisFrame++;
            Debug.Log($"[PathRequestQueue] {message} frame={frame} unit={unit?.name} target={target}");
        }
    }
}


