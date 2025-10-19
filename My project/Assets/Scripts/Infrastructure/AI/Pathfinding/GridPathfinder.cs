using System;
using System.Collections.Generic;

namespace Game.Infrastructure.AI.Pathfinding
{
    // Simple grid-based reachability checker (4-connected). Pure C# (no Unity deps).
    // Full A* path output is deferred; this sprint provides IsReachable for plumbing/tests.
    public class GridPathfinder : IGridPathfinder
    {
        private readonly int _width;
        private readonly int _height;
        private readonly Func<int, int, bool> _isWalkable; // returns true if cell (x,y) is walkable
        private readonly bool _allowDiag;

        public GridPathfinder(int width, int height, Func<int, int, bool> isWalkable, bool allowDiagonals = false)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Grid dimensions must be positive.");
            _width = width;
            _height = height;
            _isWalkable = isWalkable ?? throw new ArgumentNullException(nameof(isWalkable));
            _allowDiag = allowDiagonals;
        }

        // Convenience factory from bool[,] where true means walkable
        public static GridPathfinder FromWalkableMap(bool[,] map, bool allowDiagonals = false)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            int h = map.GetLength(0);
            int w = map.GetLength(1);
            return new GridPathfinder(w, h, (x, y) => map[y, x], allowDiagonals);
        }

        public bool IsReachable(int fromX, int fromY, int toX, int toY)
        {
            if (!InBounds(fromX, fromY) || !InBounds(toX, toY)) return false;
            if (!_isWalkable(fromX, fromY) || !_isWalkable(toX, toY)) return false;
            if (fromX == toX && fromY == toY) return true;

            var visited = new bool[_height, _width];
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((fromX, fromY));
            visited[fromY, fromX] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                // Right
                int nx = x + 1, ny = y;
                if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                // Left
                nx = x - 1; ny = y;
                if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                // Up
                nx = x; ny = y + 1;
                if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                // Down
                nx = x; ny = y - 1;
                if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                if (_allowDiag)
                {
                    // Diagonals
                    nx = x + 1; ny = y + 1; if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                    nx = x - 1; ny = y + 1; if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                    nx = x + 1; ny = y - 1; if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                    nx = x - 1; ny = y - 1; if (InBounds(nx, ny) && !visited[ny, nx] && _isWalkable(nx, ny)) { if (nx == toX && ny == toY) return true; visited[ny, nx] = true; queue.Enqueue((nx, ny)); }
                }
            }
            return false;
        }

        public bool FindPath(int fromX, int fromY, int toX, int toY, List<GridPoint> path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            path.Clear();
            if (!InBounds(fromX, fromY) || !InBounds(toX, toY)) return false;
            if (!_isWalkable(fromX, fromY) || !_isWalkable(toX, toY)) return false;
            if (fromX == toX && fromY == toY) { path.Add(new GridPoint(fromX, fromY)); return true; }

            int w = _width, h = _height;
            const int INF = int.MaxValue / 4;
            var g = new int[h, w];
            var cameX = new int[h, w];
            var cameY = new int[h, w];
            var open = new List<(int x, int y, int f)>();
            var inOpen = new bool[h, w];
            var closed = new bool[h, w];

            for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++) { g[yy, xx] = INF; cameX[yy, xx] = -1; cameY[yy, xx] = -1; }
            g[fromY, fromX] = 0;
            open.Add((fromX, fromY, Heuristic(fromX, fromY, toX, toY)));
            inOpen[fromY, fromX] = true;

            while (open.Count > 0)
            {
                // Extract best f
                int bestIdx = 0; int bestF = open[0].f;
                for (int i = 1; i < open.Count; i++) if (open[i].f < bestF) { bestF = open[i].f; bestIdx = i; }
                var node = open[bestIdx];
                open.RemoveAt(bestIdx);
                inOpen[node.y, node.x] = false;

                if (closed[node.y, node.x]) continue;
                closed[node.y, node.x] = true;

                if (node.x == toX && node.y == toY)
                {
                    // reconstruct
                    int cx = toX, cy = toY;
                    var rev = new List<GridPoint>(32);
                    rev.Add(new GridPoint(cx, cy));
                    while (!(cx == fromX && cy == fromY))
                    {
                        int px = cameX[cy, cx];
                        int py = cameY[cy, cx];
                        if (px < 0) break; // safety
                        cx = px; cy = py; rev.Add(new GridPoint(cx, cy));
                    }
                    // reverse into path
                    for (int i = rev.Count - 1; i >= 0; i--) path.Add(rev[i]);
                    return true;
                }

                // neighbors 4-connected
                ExpandNeighbor(node.x + 1, node.y, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                ExpandNeighbor(node.x - 1, node.y, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                ExpandNeighbor(node.x, node.y + 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                ExpandNeighbor(node.x, node.y - 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                if (_allowDiag)
                {
                    ExpandNeighbor(node.x + 1, node.y + 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                    ExpandNeighbor(node.x - 1, node.y + 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                    ExpandNeighbor(node.x + 1, node.y - 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                    ExpandNeighbor(node.x - 1, node.y - 1, node.x, node.y, toX, toY, g, cameX, cameY, open, inOpen, closed);
                }
            }

            return false;
        }

        private void ExpandNeighbor(int nx, int ny, int px, int py, int tx, int ty,
            int[,] g, int[,] cameX, int[,] cameY,
            List<(int x, int y, int f)> open, bool[,] inOpen, bool[,] closed)
        {
            if (!InBounds(nx, ny) || closed[ny, nx] || !_isWalkable(nx, ny)) return;
            int tentative = g[py, px] + 1;
            if (tentative < g[ny, nx])
            {
                g[ny, nx] = tentative;
                cameX[ny, nx] = px;
                cameY[ny, nx] = py;
                int f = tentative + Heuristic(nx, ny, tx, ty);
                if (!inOpen[ny, nx]) { open.Add((nx, ny, f)); inOpen[ny, nx] = true; }
            }
        }

        private static int Heuristic(int x, int y, int tx, int ty)
        {
            int dx = x - tx; if (dx < 0) dx = -dx;
            int dy = y - ty; if (dy < 0) dy = -dy;
            return dx + dy; // Manhattan
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
    }
}
