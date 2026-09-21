/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.cs
@module: presentation.pathfinding.jobs.dispatch
@purpose: Schedules queued path requests, dispatches jobified hex builds, and falls back to synchronous path generation when immediate processing is required.
@entry: PQUE-02
@api: internal partial of PathRequestQueue
@deps: HexPathfindingBootstrap, HexPathfinderJob, PathManager, UnitCombat
@data: request queue, Native job path buffer, occupied-cell snapshots
@perf: hotpath, queue draining and job scheduling directly affect combat frame time
@thread: main thread scheduler + worker jobs
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual path queue verification
@config: MaxPerFrame, UseJobs, MaxQueueSize, ProcessSynchronouslyIfIdle
@assets: none
@notes: keep destroyed owners filtered before scheduling to avoid wasting the only active job slot
*/

using System.Collections.Generic;
using Game.Presentation.View;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHREQUESTQUEUE-DISPATCH]
// Logical block: Scripts/Presentation/Pathfinding/PathRequestQueue.Dispatch.

namespace Game.Presentation.Pathfinding
{
    public partial class PathRequestQueue
    {
        // [PQUE-02]
        // Job lifecycle update: schedule new path work and finish completed requests.
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

                    if (MaxPerFrame > 0 && budget <= 0)
                        break;
                }

                if (_queue.Count == 0) break;

                if (!_jobActive && UseJobs)
                {
                    var peekReq = _queue.Peek();
                    if (peekReq.Unit == null || !peekReq.Unit.isActiveAndEnabled)
                    {
                        if (_queue.Count > 0) _queue.Dequeue();
                        peekReq.Callback?.Invoke(false, null);
                        continue;
                    }

                    if (TryScheduleJob(peekReq))
                    {
                        if (_queue.Count > 0) _queue.Dequeue();
                        continue;
                    }
                }

                if (_jobActive && UseJobs)
                    break;

                if (_queue.Count > 0)
                {
                    var req = _queue.Dequeue();
                    ProcessImmediate(req);
                }
            }
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
    }
}
