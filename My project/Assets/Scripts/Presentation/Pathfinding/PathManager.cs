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

        // Note: caching/progress disabled to avoid sticky anchors under rapid re-targeting
        private readonly Dictionary<int, List<GridPoint>> _cached = new Dictionary<int, List<GridPoint>>();
        private readonly HashSet<int> _hexFittedOnce = new HashSet<int>();
        private readonly HashSet<int> _gridFittedOnce = new HashSet<int>();

        public static PathManager Ensure()
        {
            var pm = FindObjectOfType<PathManager>();
            if (pm == null)
            {
                var go = new GameObject("PathManager (Auto)");
                pm = go.AddComponent<PathManager>();
            }
            return pm;
        }

        public bool BuildPath(UnitView unit, Vector3 worldTarget, bool allowDiag, bool smooth, bool autoFit, out List<Vector3> worldPoints)
        {
            worldPoints = null;
            if (unit == null) return false;
            // Prefer hex bootstrap if present; fallback to square grid
            var hex = FindObjectOfType<HexPathfindingBootstrap>();
            var grid = (hex == null) ? FindObjectOfType<PathfindingBootstrap>() : null;
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
            if (pf == null || worldToCell == null || cellToWorld == null) return false;

            var from = worldToCell(unit.transform.position);
            var to = worldToCell(worldTarget);

            var key = unit.GetInstanceID();

            var newGrid = new List<GridPoint>(64);
            // Always compute a fresh path from current cell to target to avoid queuing legacy segments
            pf.FindPath(from.x, from.y, to.x, to.y, newGrid);

            if (newGrid == null || newGrid.Count == 0) return false;
            _cached[key] = newGrid;

            // Build world points starting from nearest forward index; do not clamp to previous path indices
            int closestIdx = ClosestIndex(newGrid, from.x, from.y);
            int startIdx = closestIdx;
            // Do not force prior steps; start is chosen based solely on closest current cell in the fresh path.
            // Never allow start before closest current cell index
            startIdx = Mathf.Max(startIdx, closestIdx);
            startIdx = Mathf.Clamp(startIdx, 0, newGrid.Count - 1);
            worldPoints = new List<Vector3>(newGrid.Count - startIdx);
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
            return worldPoints.Count > 0;
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

            // If there's only one cell in the path, go directly to final world target
            if (count - startIdx == 1)
            {
                outPoints.Add(finalWorld);
                return;
            }

            // Start from the next cell center to avoid returning to the center of current cell
            int prevX = path[startIdx + 1].X, prevY = path[startIdx + 1].Y;
            int dirX = 0, dirY = 0;
            var first = cellToWorld(prevX, prevY);
            outPoints.Add(first);

            for (int i = startIdx + 2; i < count; i++)
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
            outPoints.Add(cellToWorld(path[count - 1].X, path[count - 1].Y));
        }
    }
}
