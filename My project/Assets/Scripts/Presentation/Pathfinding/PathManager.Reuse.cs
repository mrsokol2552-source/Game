/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/PathManager.Reuse.cs
@module: presentation.pathfinding.paths.reuse
@purpose: Handles group-path reuse, occupied/free-cell queries, and cluster edge helper logic for PathManager.
@entry: PMGR-04, PathManager.TryReuseGroupPath, PathManager.TryFindNearestFreeWorld
@api: partial of PathManager used internally by combat/path layers and cluster-stepping callers
@deps: HexPathfindingBootstrap, OccupancyHash, StaticObstacleHash, UnitCombat
@data: cached group path reuse map, temp key list
@perf: hotpath, reuse decisions and occupancy checks directly affect combat movement cost
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual combat/path verification
@config: EnableGroupPathReuse, GroupReuseFrames, GroupReuseMaxStartDist2
@assets: none directly
@notes: cluster-edge and nearest-free helpers are behavior-facing and should stay deterministic across refactors
*/

using System.Collections.Generic;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PATHMANAGER-REUSE]
// Logical block: Scripts/Presentation/Pathfinding/PathManager.Reuse.

namespace Game.Presentation.Pathfinding
{
    public partial class PathManager
    {
        private readonly List<int> _tmpKeys = new List<int>(16);

        // [PMGR-04]
        // Group-path reuse and cache lookup to reduce repeated per-unit path builds.
        private bool TryReuseGroupPath(UnitView unit, Vector2Int from, Vector2Int to, out List<Vector3> worldPath)
        {
            worldPath = null;
            int key = Key(to.x, to.y);
            if (!_cachedGroupPaths.TryGetValue(key, out var cached)) return false;
            if (Time.frameCount - cached.Frame > GroupReuseFrames) return false;
            if (cached.WorldPath == null || cached.WorldPath.Count == 0) return false;
            if (cached.TargetCell != to) return false;
            if ((from - cached.StartCell).sqrMagnitude > GroupReuseMaxStartDist2) return false;
            if (cached.WorldPath.Count > 0)
            {
                Vector3 worldStart = cached.WorldPath[0];
                Vector3 worldFrom = _hexCached != null ? _hexCached.GridToWorld(from.x, from.y) : new Vector3(from.x, from.y, 0f);
                var dir = worldStart - worldFrom;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Vector3 forward = (cached.WorldPath[cached.WorldPath.Count - 1] - worldFrom).normalized;
                    float dot = Vector3.Dot(dir.normalized, forward);
                    if (dot < -0.7f) return false;
                }
            }

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

        private int Key(int col, int row) => (row << 16) ^ (col & 0xFFFF);

        public bool IsWorldOccupied(Vector3 world, UnitView except = null, bool enemiesOnly = false)
        {
            CacheBootstraps();
            if (UseStaticObstacleHash && _staticHash != null && _staticHash.IsBlockedWorld(world))
                return true;
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
            if (UseStaticObstacleHash && _staticHash != null && _staticHash.IsBlockedCell(cell))
                return true;

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
            var dirs = new (int q, int r)[] { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };
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
                    int col = aq + (ar - (ar & 1)) / 2;
                    int row = ar;
                    yield return new Vector2Int(col, row);
                }
            }
        }
    }
}
