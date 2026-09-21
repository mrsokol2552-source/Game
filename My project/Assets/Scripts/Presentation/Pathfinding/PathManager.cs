/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathManager.cs
@module: presentation.pathfinding.paths
@purpose: Builds, caches, and reuses per-unit world paths on the hex grid, including occupancy-aware replans.
@entry: PathManager.BuildPath, PathManager.Update, PMGR-03
@api: shared MonoBehaviour singleton used by combat and path followers
@deps: HexPathfindingBootstrap, OccupancyHash, StaticObstacleHash, UnitCombat
@data: cached group paths, node pools, occupancy snapshots, diagnostics counters
@perf: hotpath, path allocation and reuse directly affect frame time under combat load
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual chase/path verification
@config: inspector path reuse and occupancy settings, docs/runtime_switches.md
@assets: none directly
@notes: friendly reservation and group reuse are behavior-critical and can cause stale-path bugs if tuned too aggressively
*/

using System.Collections.Generic;
using Game.Infrastructure.AI.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER]
// Logical block: Scripts/Presentation/Pathfinding/PathManager.

namespace Game.Presentation.Pathfinding
{
    // Caches per-unit grid paths and performs suffix replan on new targets
    public partial class PathManager : MonoBehaviour
    {
        // [PMGR-01]
        // Shared pathfinding state, caches, object pools, and diagnostics counters.
        [Header("Replan Settings")]
        [Tooltip("Tail window (in nodes) that is allowed to change when retargeting")] public int TailWindow = 6;
        [Tooltip("Keep this many nodes ahead of current position before replan")] public int StableAhead = 4;
        [Header("Perf")]
        [Tooltip("Max path builds allowed per frame (0 or less = unlimited).")]
        public int MaxBuildsPerFrame = 0;
        [Tooltip("When true, reuse last built path to same target cell for nearby allies in the same cluster (best effort).")]
        public bool EnableGroupPathReuse = false;
        [Tooltip("Max squared distance between unit and cached start to allow reuse.")]
        public float GroupReuseMaxStartDist2 = 1.5f * 1.5f;
        [Tooltip("How many frames a cached group path stays valid.")]
        public int GroupReuseFrames = 20;
        [Header("Safety")]
        [Tooltip("Hard cap on path nodes to avoid runaway allocations (0 = unlimited).")]
        public int MaxPathNodes = 2048;
        [Header("Diagnostics")]
        [Tooltip("If true, logs reasons for failed path builds (throttled per frame).")]
        public bool LogBuildFailures = false;
        [Tooltip("Max failure logs per frame to avoid spamming the console.")]
        public int MaxFailureLogsPerFrame = 3;

        private readonly Dictionary<int, CachedPath> _cachedGroupPaths = new Dictionary<int, CachedPath>(32);
        private struct CachedPath
        {
            public Vector2Int StartCell;
            public Vector2Int TargetCell;
            public List<Vector3> WorldPath;
            public int Frame;
        }
        [Tooltip("How long (seconds) a friendly-occupied cell stays reserved for pathing purposes.")]
        public float FriendlyReserveSeconds = 0.25f;
        [Header("Occupancy")]
        [Tooltip("Use OccupancyHash (if available) for fast occupied checks.")]
        public bool UseOccupancyHash = true;
        [Tooltip("Use StaticObstacleHash (if available) for blocked-cell checks.")]
        public bool UseStaticObstacleHash = true;

        private readonly HashSet<int> _hexFittedOnce = new HashSet<int>();
        private readonly HashSet<int> _gridFittedOnce = new HashSet<int>();
        private int _builtThisFrame;
        private int _lastFrame;
        private readonly Dictionary<int, float> _friendRecentPlayers = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _friendRecentEnemies = new Dictionary<int, float>();
        private static PathManager _instance;
        private HexPathfindingBootstrap _hexCached;
        private PathfindingBootstrap _gridCached;
        // Pools to avoid per-call allocations
        private static readonly Stack<List<GridPoint>> _gridPool = new Stack<List<GridPoint>>();
        private static readonly Stack<List<Vector3>> _worldPool = new Stack<List<Vector3>>();
        private static readonly Stack<HashSet<int>> _hashPool = new Stack<HashSet<int>>();
        private Game.Presentation.Performance.OccupancyHash _occ;
        private StaticObstacleHash _staticHash;
        private int _logFrame = -1;
        private int _logsThisFrame;
        private int _occupiedCacheFrame = -1;
        private int _occupiedCacheGridId;
        private readonly HashSet<int> _occupiedAllCache = new HashSet<int>();
        private readonly HashSet<int> _occupiedPlayersCache = new HashSet<int>();
        private readonly HashSet<int> _occupiedEnemiesCache = new HashSet<int>();
        private readonly HashSet<int> _occupiedAllWithRecentPlayersCache = new HashSet<int>();
        private readonly HashSet<int> _occupiedAllWithRecentEnemiesCache = new HashSet<int>();
        private readonly List<int> _staleRecentKeys = new List<int>(128);

        public static bool BuildBudgetExhausted => _instance != null && _instance.IsBuildBudgetExhaustedInternal();

        public static PathManager Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("PathManager (Auto)");
                _instance = go.AddComponent<PathManager>();
                if (go.GetComponent<CrowdingResolver>() == null)
                    go.AddComponent<CrowdingResolver>();
            }
            return _instance;
        }

        public bool BuildPath(UnitView unit, Vector3 worldTarget, bool allowDiag, bool smooth, bool autoFit, out List<Vector3> worldPoints, bool blockFriendlies = true)
        {
            worldPoints = null;
            if (unit == null) return false;
            int maxNodes = MaxPathNodes > 0 ? MaxPathNodes : 2048;
            ThrottleReset();
            if (MaxBuildsPerFrame > 0 && _builtThisFrame >= MaxBuildsPerFrame)
            {
                PathProfiler.CountBuild(false);
                LogFailure("Budget exceeded", unit, worldTarget);
                return false;
            }
            // Prefer hex bootstrap if present; fallback to square grid (cached)
            CacheBootstraps();
            var hex = _hexCached;
            var grid = (hex == null) ? _gridCached : null;
            IGridPathfinder pf = null;
            System.Func<Vector3, Vector2Int> worldToCell = null;
            System.Func<int, int, Vector3> cellToWorld = null;
            if (hex != null)
            {
                // Fit hex grid to camera only once per bootstrap to avoid drifting origins between commands
                if (autoFit)
                {
                    int hid = hex.GetInstanceID();
                    if (!_hexFittedOnce.Contains(hid))
                    {
                        hex.FitToCamera();
                        _hexFittedOnce.Add(hid);
                    }
                }
                pf = hex.Pathfinder;
                worldToCell = hex.WorldToGrid;
                cellToWorld = hex.GridToWorld;
            }
            else if (grid != null)
            {
                grid.SetAllowDiagonals(allowDiag);
                grid.AutoFitToCamera = autoFit;
                if (autoFit)
                {
                    int gid = grid.GetInstanceID();
                    if (!_gridFittedOnce.Contains(gid))
                    {
                        grid.FitToCamera();
                        _gridFittedOnce.Add(gid);
                    }
                }
                pf = grid.Pathfinder;
                worldToCell = grid.WorldToGrid;
                cellToWorld = grid.GridToWorld;
            }
            if (pf == null || worldToCell == null || cellToWorld == null)
            {
                LogFailure("No pathfinder/bootstrap", unit, worldTarget);
                return false;
            }

            var from = worldToCell(unit.transform.position);
            var to = worldToCell(worldTarget);
            // Bounds check for hex/grid; if outside, log and bail to avoid repeated empty paths
            if (hex != null)
            {
                if (from.x < 0 || from.y < 0 || from.x >= hex.Width || from.y >= hex.Height ||
                    to.x < 0 || to.y < 0 || to.x >= hex.Width || to.y >= hex.Height)
                {
                    LogFailure($"Out of bounds (from {from} to {to}, grid {hex.Width}x{hex.Height})", unit, worldTarget);
                    return false;
                }
            }
            else if (grid != null)
            {
                if (from.x < 0 || from.y < 0 || from.x >= grid.Width || from.y >= grid.Height ||
                    to.x < 0 || to.y < 0 || to.x >= grid.Width || to.y >= grid.Height)
                {
                    LogFailure($"Out of bounds (from {from} to {to}, grid {grid.Width}x{grid.Height})", unit, worldTarget);
                    return false;
                }
            }

            // Try reuse cached path for nearby allies heading to the same target cell in the same frame
            if (EnableGroupPathReuse && TryReuseGroupPath(unit, from, to, out worldPoints))
            {
                PathProfiler.CountBuild(true);
                return true;
            }

            // Block enemies and (recently) friendly cells to reduce stacking
            var occupied = BuildOccupied(worldToCell, unit, enemiesOnly: !blockFriendlies, FriendlyReserveSeconds);

            var newGrid = RentGridList();
            // Always compute a fresh path from current cell to target to avoid queuing legacy segments
            pf.FindPath(from.x, from.y, to.x, to.y, newGrid);

            if (newGrid != null && newGrid.Count > maxNodes)
            {
                ReturnGridList(newGrid);
                ReleaseOccupied(occupied);
                if (worldPoints != null) ReturnWorldList(worldPoints);
                PathProfiler.CountBuild(false);
                LogFailure($"Path too long ({newGrid.Count} > {maxNodes})", unit, worldTarget);
                return false;
            }

            if (newGrid != null) PathProfiler.CountPath(newGrid.Count);
            if (newGrid == null || newGrid.Count == 0)
            {
                if (newGrid != null) ReturnGridList(newGrid);
                ReleaseOccupied(occupied);
                LogFailure("Path empty/null", unit, worldTarget);
                return false;
            }
            // Do not accept a path that traverses an occupied cell (except current cell)
            if (PathHitsOccupied(newGrid, occupied, from))
            {
                ReturnGridList(newGrid);
                ReleaseOccupied(occupied);
                if (worldPoints != null) ReturnWorldList(worldPoints);
                PathProfiler.CountBuild(false);
                LogFailure("Path hits occupied", unit, worldTarget);
                return false;
            }
            // Build world points starting from nearest forward index; do not clamp to previous path indices
            int closestIdx = ClosestIndex(newGrid, from.x, from.y);
            int startIdx = closestIdx;
            // Do not force prior steps; start is chosen based solely on closest current cell in the fresh path.
            // Never allow start before closest current cell index
            startIdx = Mathf.Max(startIdx, closestIdx);
            startIdx = Mathf.Clamp(startIdx, 0, newGrid.Count - 1);
            worldPoints = RentWorldList();
            if (smooth)
            {
                SmoothToWorld(cellToWorld, unit.transform.position, worldTarget, newGrid, startIdx, worldPoints);
            }
            else
            {
                // Skip the center of the current cell to avoid snapping back to it
                int ptStart = Mathf.Min(startIdx + 1, newGrid.Count - 1);
                for (int i = ptStart; i < newGrid.Count; i++)
                {
                    var gp = newGrid[i];
                    worldPoints.Add(cellToWorld(gp.X, gp.Y));
                }
            }
            ReturnGridList(newGrid);
            ReleaseOccupied(occupied);
            if (worldPoints.Count == 0)
            {
                ReturnWorldList(worldPoints);
                PathProfiler.CountBuild(false);
                LogFailure("World path empty after conversion", unit, worldTarget);
                return false;
            }
            PathProfiler.CountPathLength(worldPoints.Count);
            PathProfiler.CountBuild(true);
            // Cache for reuse within this frame
            if (EnableGroupPathReuse)
                CacheGroupPath(from, to, worldPoints);

            return true;
        }

        public static void ReleaseOnFail(List<Vector3> worldPoints)
        {
            if (worldPoints != null)
                ReturnWorldList(worldPoints);
        }

        // [PMGR-02]
        // Global path-build throttling and queue budget helpers.
        private void ThrottleReset()
        {
            int frame = Time.frameCount;
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
                _builtThisFrame = 0;
                CleanupCachedPaths(frame);
            }
            if (MaxBuildsPerFrame > 0)
                _builtThisFrame++;
        }

        private bool IsBuildBudgetExhaustedInternal()
        {
            if (MaxBuildsPerFrame <= 0) return false;
            if (Time.frameCount != _lastFrame) return false;
            return _builtThisFrame >= MaxBuildsPerFrame;
        }

        private static int ClosestIndex(List<GridPoint> path, int gx, int gy)
        {
            int best = 0; int bestD = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                int dx = path[i].X - gx; if (dx < 0) dx = -dx;
                int dy = path[i].Y - gy; if (dy < 0) dy = -dy;
                int d = dx + dy;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static int IndexOf(List<GridPoint> path, GridPoint gp)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].X == gp.X && path[i].Y == gp.Y) return i;
            }
            return -1;
        }

        private static void SmoothToWorld(System.Func<int,int,Vector3> cellToWorld, Vector3 unitPos, Vector3 finalWorld, List<GridPoint> path, int startIdx, List<Vector3> outPoints)
        {
            int count = path.Count;
            if (count == 0 || startIdx >= count) return;
            int maxPoints = _instance != null && _instance.MaxPathNodes > 0 ? _instance.MaxPathNodes : 2048;
            int endExclusive = Mathf.Min(count, startIdx + maxPoints);
            int needed = endExclusive - startIdx + 1;
            if (outPoints.Capacity < needed)
            {
                int newCap = needed;
                if (newCap > maxPoints + 8) newCap = maxPoints + 8; // safety cap
                outPoints.Capacity = newCap;
            }

            // If there's only one cell in the path, go directly to final world target
            if (endExclusive - startIdx == 1)
            {
                outPoints.Add(finalWorld);
                return;
            }

            // Start from the next cell center to avoid returning to the center of current cell
            int prevX = path[startIdx + 1].X, prevY = path[startIdx + 1].Y;
            int dirX = 0, dirY = 0;
            var first = cellToWorld(prevX, prevY);
            outPoints.Add(first);

            for (int i = startIdx + 2; i < endExclusive; i++)
            {
                int sx = path[i].X - prevX;
                int sy = path[i].Y - prevY;
                int ndx = sx == 0 ? 0 : (sx > 0 ? 1 : -1);
                int ndy = sy == 0 ? 0 : (sy > 0 ? 1 : -1);
                if (i == startIdx + 2)
                {
                    dirX = ndx; dirY = ndy;
                }
                else if (ndx != dirX || ndy != dirY)
                {
                    outPoints.Add(cellToWorld(prevX, prevY));
                    dirX = ndx; dirY = ndy;
                }
                prevX = path[i].X; prevY = path[i].Y;
            }
            outPoints.Add(cellToWorld(path[endExclusive - 1].X, path[endExclusive - 1].Y));
        }

        // [PMGR-05]
        // Bootstrap discovery and pooled collection helpers for path-building resources.
        private void CacheBootstraps()
        {
            if (_hexCached == null)
                _hexCached = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_gridCached == null && _hexCached == null)
                _gridCached = UnityEngine.Object.FindAnyObjectByType<PathfindingBootstrap>();
            if (_occ == null && UseOccupancyHash)
                _occ = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Performance.OccupancyHash>();
            if (_staticHash == null && UseStaticObstacleHash)
                _staticHash = UnityEngine.Object.FindAnyObjectByType<StaticObstacleHash>();
        }

        private static List<GridPoint> RentGridList()
        {
            return _gridPool.Count > 0 ? _gridPool.Pop() : new List<GridPoint>(128);
        }

        private static void ReturnGridList(List<GridPoint> list)
        {
            if (list == null) return;
            list.Clear();
            _gridPool.Push(list);
        }

        public static List<Vector3> RentWorldList()
        {
            return _worldPool.Count > 0 ? _worldPool.Pop() : new List<Vector3>(128);
        }

        public static void ReturnWorldList(List<Vector3> list)
        {
            if (list == null) return;
            list.Clear();
            _worldPool.Push(list);
        }

        private static HashSet<int> RentHashSet()
        {
            return _hashPool.Count > 0 ? _hashPool.Pop() : new HashSet<int>();
        }

        private static void ReturnHashSet(HashSet<int> set)
        {
            if (set == null) return;
            set.Clear();
            _hashPool.Push(set);
        }

        private void LogFailure(string reason, UnitView unit, Vector3 target)
        {
            if (!LogBuildFailures) return;
            int frame = Time.frameCount;
            if (frame != _logFrame)
            {
                _logFrame = frame;
                _logsThisFrame = 0;
            }
            if (MaxFailureLogsPerFrame > 0 && _logsThisFrame >= MaxFailureLogsPerFrame) return;
            _logsThisFrame++;
            string unitName = unit != null ? unit.name : "null";
            Debug.LogWarning($"[PathManager] Fail: {reason} | unit={unitName} pos={unit?.transform.position ?? Vector3.zero} target={target} frame={frame}");
        }
    }
}

