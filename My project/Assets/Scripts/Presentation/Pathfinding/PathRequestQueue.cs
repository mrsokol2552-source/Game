/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.cs
@module: presentation.pathfinding.jobs
@purpose: Queues asynchronous path requests, schedules jobified hex path builds, and falls back to synchronous path generation when needed.
@entry: PathRequestQueue.Enqueue, PQUE-01, PQUE-04
@api: shared MonoBehaviour singleton for async path submission
@deps: HexPathfindingBootstrap, HexPathfinderJob, UnitCombat, Unity Jobs/Collections
@data: request queue, Native job buffers, occupied-cell snapshots, job counters
@perf: hotpath, queue depth and job fallback behavior are major combat scaling factors
@thread: main thread scheduler + worker jobs
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual path queue verification
@config: MaxPerFrame, UseJobs, MaxQueueSize, docs/runtime_switches.md
@assets: none directly
@notes: destroyed request owners must be filtered before scheduling and before callbacks to avoid MissingReferenceException
*/

using System;
using System.Collections.Generic;
using Game.Presentation.View;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE]
// Logical block: Scripts/Presentation/Pathfinding/PathRequestQueue.

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Queue for path requests. Uses jobified hex pathfinder when available; falls back to sync BuildPath.
    /// </summary>
    public partial class PathRequestQueue : MonoBehaviour
    {
        // [PQUE-01]
        // Async path-request queue, job bookkeeping, and fallback counters.
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

        // [PQUE-04]
        // Immutable request payload captured at queue time for async path builds.
        private struct Request
        {
            public UnitView Unit;
            public Vector3 Target;
            public bool AllowDiag;
            public bool Smooth;
            public Action<bool, List<Vector3>> Callback;
        }
    }
}

