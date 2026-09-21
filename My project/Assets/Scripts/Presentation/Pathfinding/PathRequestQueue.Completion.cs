/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.cs
@module: presentation.pathfinding.jobs.completion
@purpose: Finalizes path jobs, maintains per-frame occupancy snapshots and counters, and safely invokes callbacks/logging once async work finishes.
@entry: PQUE-03
@api: internal partial of PathRequestQueue
@deps: HexPathfindingBootstrap, PathManager, PathProfiler, UnitCombat
@data: job statistics, occupied-cell hash maps, cached hex bootstrap reference
@perf: hotpath, completion and snapshot refresh cost directly impact async path throughput
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual path queue verification
@config: LogJobResults, MaxLogsPerFrame
@assets: none
@notes: this layer owns callback safety and should remain the only place that converts finished job results back into pooled world paths
*/

using System;
using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-COMPLETION]
// Logical block: Scripts/Presentation/Pathfinding/PathRequestQueue.Completion.

namespace Game.Presentation.Pathfinding
{
    public partial class PathRequestQueue
    {
        // [PQUE-03]
        // Completion path for async jobs, callbacks, and safety checks against destroyed owners.
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

            List<Vector3> worldPath = null;
            bool built = false;
            int maxNodes = pm.MaxPathNodes > 0 ? pm.MaxPathNodes : 2048;
            if (_jobPath.Length > 0)
            {
                worldPath = PathManager.RentWorldList();
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
                if (LogJobResults) Log($"Job OK len={pathLen} source={(_jobPath.Length > 0 ? "job" : "fallback")}", _jobReq.Unit, _jobReq.Target);
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
