using System.Collections.Generic;
using Game.Infrastructure.AI.Pathfinding;
using Game.Presentation.View;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    // Caches per-unit grid paths and performs suffix replan on new targets
    public class PathManager : MonoBehaviour
    {
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

        private readonly HashSet<int> _hexFittedOnce = new HashSet<int>();
        private readonly HashSet<int> _gridFittedOnce = new HashSet<int>();
        private int _builtThisFrame;
        private int _lastFrame;
        private readonly Dictionary<int, float> _friendRecent = new Dictionary<int, float>();
        private static PathManager _instance;
        private HexPathfindingBootstrap _hexCached;
        private PathfindingBootstrap _gridCached;
        // Pools to avoid per-call allocations
        private static readonly Stack<List<GridPoint>> _gridPool = new Stack<List<GridPoint>>();
        private static readonly Stack<List<Vector3>> _worldPool = new Stack<List<Vector3>>();
        private static readonly Stack<HashSet<int>> _hashPool = new Stack<HashSet<int>>();
        private Game.Presentation.Performance.OccupancyHash _occ;
        private int _logFrame = -1;
        private int _logsThisFrame;

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
                ReturnHashSet(occupied);
                if (worldPoints != null) ReturnWorldList(worldPoints);
                PathProfiler.CountBuild(false);
                LogFailure($"Path too long ({newGrid.Count} > {maxNodes})", unit, worldTarget);
                return false;
            }

            if (newGrid != null) PathProfiler.CountPath(newGrid.Count);
            if (newGrid == null || newGrid.Count == 0)
            {
                LogFailure("Path empty/null", unit, worldTarget);
                return false;
            }
            // Do not accept a path that traverses an occupied cell (except current cell)
            if (PathHitsOccupied(newGrid, occupied, from))
            {
                ReturnGridList(newGrid);
                ReturnHashSet(occupied);
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
            ReturnHashSet(occupied);
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

        private HashSet<int> BuildOccupied(System.Func<Vector3, Vector2Int> worldToCell, UnitView self, bool enemiesOnly, float friendTtl = 0f)
        {
            var set = RentHashSet();
            Game.Domain.Units.Faction? selfFaction = null;
            if (self != null) selfFaction = self.GetComponent<Game.Presentation.View.UnitCombat>()?.Faction;

            // Fast path: use occupancy hash if available and not enemiesOnly
            if (UseOccupancyHash && _occ != null && !enemiesOnly)
            {
                foreach (var uc in Game.Presentation.View.UnitCombat.All)
                {
                    if (uc == null) continue;
                    if (self != null && uc.gameObject == self.gameObject) continue;
                    var cell = worldToCell(uc.transform.position);
                    set.Add(Key(cell.x, cell.y));
                }
                return set;
            }

            foreach (var uc in Game.Presentation.View.UnitCombat.All)
            {
                if (uc == null) continue;
                if (self != null && uc.gameObject == self.gameObject) continue;
                if (enemiesOnly)
                {
                    var ucFaction = uc.Faction;
                    if (selfFaction.HasValue && ucFaction == selfFaction.Value)
                        continue;
                }
                else
                {
                    // For friendlies, apply TTL so paths can pass through if cell was vacated recently
                    if (selfFaction.HasValue && uc.Faction == selfFaction.Value && friendTtl > 0f)
                    {
                        var cellTmp = worldToCell(uc.transform.position);
                        int k = Key(cellTmp.x, cellTmp.y);
                        _friendRecent[k] = Time.time;
                    }
                }
                var cell = worldToCell(uc.transform.position);
                set.Add(Key(cell.x, cell.y));
            }

            if (!enemiesOnly && friendTtl > 0f)
            {
                float now = Time.time;
                foreach (var kv in _friendRecent)
                {
                    if (now - kv.Value <= friendTtl)
                        set.Add(kv.Key);
                }
            }
            return set;
        }

        private bool PathHitsOccupied(List<GridPoint> path, HashSet<int> occupied, Vector2Int from)
        {
            if (occupied == null || occupied.Count == 0) return false;
            for (int i = 0; i < path.Count; i++)
            {
                var gp = path[i];
                if (gp.X == from.x && gp.Y == from.y) continue;
                if (occupied.Contains(Key(gp.X, gp.Y))) return true;
            }
            return false;
        }

        private bool TryReuseGroupPath(UnitView unit, Vector2Int from, Vector2Int to, out List<Vector3> worldPath)
        {
            worldPath = null;
            int key = Key(to.x, to.y);
            if (!_cachedGroupPaths.TryGetValue(key, out var cached)) return false;
            // keep cached for a few frames
            if (Time.frameCount - cached.Frame > GroupReuseFrames) return false;
            if (cached.WorldPath == null || cached.WorldPath.Count == 0) return false;
            // ensure same target cell
            if (cached.TargetCell != to) return false;
            // allow reuse only if unit is near cached start to avoid absurd detours
            if ((from - cached.StartCell).sqrMagnitude > GroupReuseMaxStartDist2) return false;
            // avoid reusing if path starts behind the unit (rough check)
            if (cached.WorldPath.Count > 0)
            {
                Vector3 worldStart = cached.WorldPath[0];
                Vector3 worldFrom = _hexCached != null ? _hexCached.GridToWorld(from.x, from.y) : new Vector3(from.x, from.y, 0f);
                var dir = worldStart - worldFrom;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    // reject if first step is more than ~135 degrees backward
                    Vector3 forward = (cached.WorldPath[cached.WorldPath.Count - 1] - worldFrom).normalized;
                    float dot = Vector3.Dot(dir.normalized, forward);
                    if (dot < -0.7f) return false;
                }
            }
            // copy cached path to new list to avoid shared mutation
            worldPath = RentWorldList();
            worldPath.AddRange(cached.WorldPath);
            return true;
        }

        private void CacheGroupPath(Vector2Int from, Vector2Int to, List<Vector3> worldPath)
        {
            if (worldPath == null || worldPath.Count == 0) return;
            int key = Key(to.x, to.y);
            if (_cachedGroupPaths.TryGetValue(key, out var existing))
            {
                if (existing.WorldPath != null)
                    ReturnWorldList(existing.WorldPath);
            }
            var copy = RentWorldList();
            copy.AddRange(worldPath);
            _cachedGroupPaths[key] = new CachedPath
            {
                StartCell = from,
                TargetCell = to,
                WorldPath = copy,
                Frame = Time.frameCount
            };
        }

        private void CleanupCachedPaths(int frame)
        {
            if (_cachedGroupPaths.Count == 0) return;
            _tmpKeys.Clear();
            foreach (var kv in _cachedGroupPaths)
            {
                if (frame - kv.Value.Frame > GroupReuseFrames)
                    _tmpKeys.Add(kv.Key);
            }
            for (int i = 0; i < _tmpKeys.Count; i++)
            {
                var key = _tmpKeys[i];
                if (_cachedGroupPaths.TryGetValue(key, out var cached))
                {
                    if (cached.WorldPath != null)
                        ReturnWorldList(cached.WorldPath);
                    _cachedGroupPaths.Remove(key);
                }
            }
        }

        private readonly List<int> _tmpKeys = new List<int>(16);

        private int Key(int col, int row) => (row << 16) ^ (col & 0xFFFF);

        public bool IsWorldOccupied(Vector3 world, UnitView except = null, bool enemiesOnly = false)
        {
            CacheBootstraps();
            if (UseOccupancyHash && _occ != null)
                return _occ.IsOccupied(world, except, enemiesOnly);
            var hex = _hexCached;
            if (hex == null) return false;
            var cell = hex.WorldToGrid(world);
            return IsCellOccupied(cell, except, enemiesOnly);
        }

        public bool IsCellOccupied(Vector2Int cell, UnitView except = null, bool enemiesOnly = false)
        {
            Game.Domain.Units.Faction? selfFaction = null;
            if (except != null)
                selfFaction = except.GetComponent<Game.Presentation.View.UnitCombat>()?.Faction;
            CacheBootstraps();
            var hex = _hexCached;
            foreach (var uc in Game.Presentation.View.UnitCombat.All)
            {
                if (uc == null) continue;
                if (except != null && uc.gameObject == except.gameObject) continue;
                if (enemiesOnly && selfFaction.HasValue && uc.Faction == selfFaction.Value) continue;
                if (hex == null) continue;
                var posCell = hex.WorldToGrid(uc.transform.position);
                if (posCell == cell) return true;
            }
            return false;
        }

        public bool TryFindNearestFreeWorld(Vector3 desiredWorld, UnitView self, int maxRadius, out Vector3 freeWorld)
        {
            freeWorld = desiredWorld;
            CacheBootstraps();
            var hex = _hexCached;
            if (hex == null) return false;
            var start = hex.WorldToGrid(desiredWorld);
            if (IsCellFree(hex, start, self))
            {
                freeWorld = hex.GridToWorld(start.x, start.y);
                return true;
            }

            for (int radius = 1; radius <= Mathf.Max(1, maxRadius); radius++)
            {
                foreach (var cell in HexRing(start, radius))
                {
                    if (cell.x < 0 || cell.y < 0 || cell.x >= hex.Width || cell.y >= hex.Height) continue;
                    if (!IsCellFree(hex, cell, self)) continue;
                    freeWorld = hex.GridToWorld(cell.x, cell.y);
                    return true;
                }
            }
            return false;
        }

        private bool IsCellFree(HexPathfindingBootstrap hex, Vector2Int cell, UnitView self)
        {
            if (hex == null) return false;
            if (!hex.IsWalkable(cell.x, cell.y)) return false;
            return !IsCellOccupied(cell, self);
        }

        public int ClusterDistance(Vector3 aWorld, Vector3 bWorld, int clusterSize)
        {
            CacheBootstraps();
            var hex = _hexCached;
            if (hex == null || clusterSize <= 0) return 0;
            var ca = hex.WorldToGrid(aWorld);
            var cb = hex.WorldToGrid(bWorld);
            var ac = new Vector2Int(ca.x / clusterSize, ca.y / clusterSize);
            var bc = new Vector2Int(cb.x / clusterSize, cb.y / clusterSize);
            return Mathf.Abs(ac.x - bc.x) + Mathf.Abs(ac.y - bc.y);
        }

        public Vector2Int CellToCluster(Vector2Int cell, int clusterSize)
        {
            if (clusterSize <= 0) return Vector2Int.zero;
            return new Vector2Int(cell.x / clusterSize, cell.y / clusterSize);
        }

        /// <summary>
        /// If target is outside current cluster, pick a walkable boundary cell of current cluster toward the target cluster.
        /// </summary>
        public bool TryGetClusterEdgeTarget(Vector3 fromWorld, Vector3 toWorld, int clusterSize, UnitView self, out Vector3 edgeWorld)
        {
            edgeWorld = toWorld;
            CacheBootstraps();
            var hex = _hexCached;
            if (hex == null || clusterSize <= 0) return false;
            var fromCell = hex.WorldToGrid(fromWorld);
            var toCell = hex.WorldToGrid(toWorld);
            var fromCluster = CellToCluster(fromCell, clusterSize);
            var toCluster = CellToCluster(toCell, clusterSize);
            if (fromCluster == toCluster) return false;

            int minCol = fromCluster.x * clusterSize;
            int maxCol = Mathf.Min(hex.Width - 1, minCol + clusterSize - 1);
            int minRow = fromCluster.y * clusterSize;
            int maxRow = Mathf.Min(hex.Height - 1, minRow + clusterSize - 1);

            // Collect boundary cells
            var candidates = new List<Vector2Int>();
            for (int r = minRow; r <= maxRow; r++)
            {
                candidates.Add(new Vector2Int(minCol, r));
                candidates.Add(new Vector2Int(maxCol, r));
            }
            for (int c = minCol; c <= maxCol; c++)
            {
                candidates.Add(new Vector2Int(c, minRow));
                candidates.Add(new Vector2Int(c, maxRow));
            }

            // Choose closest to target cell
            Vector2Int bestCell = fromCell;
            int bestDist = int.MaxValue;
            foreach (var c in candidates)
            {
                if (c.x < 0 || c.y < 0 || c.x >= hex.Width || c.y >= hex.Height) continue;
                if (!hex.IsWalkable(c.x, c.y)) continue;
                if (IsCellOccupied(c, self, enemiesOnly: false)) continue;
                int d = Mathf.Abs(c.x - toCell.x) + Mathf.Abs(c.y - toCell.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCell = c;
                }
            }

            if (bestDist == int.MaxValue) return false;
            edgeWorld = hex.GridToWorld(bestCell.x, bestCell.y);
            return true;
        }

        private IEnumerable<Vector2Int> HexRing(Vector2Int center, int radius)
        {
            // axial directions
            var dirs = new (int q, int r)[] { (1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1) };
            // convert center (odd-r) to axial
            int cq = center.x - (center.y - (center.y & 1)) / 2;
            int cr = center.y;
            int aq = cq + dirs[4].q * radius;
            int ar = cr + dirs[4].r * radius;
            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    var dir = dirs[side];
                    aq += dir.q;
                    ar += dir.r;
                    // axial to odd-r
                    int col = aq + (ar - (ar & 1)) / 2;
                    int row = ar;
                    yield return new Vector2Int(col, row);
                }
            }
        }

        private void CacheBootstraps()
        {
            if (_hexCached == null)
                _hexCached = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_gridCached == null && _hexCached == null)
                _gridCached = UnityEngine.Object.FindAnyObjectByType<PathfindingBootstrap>();
            if (_occ == null && UseOccupancyHash)
                _occ = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Performance.OccupancyHash>();
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


