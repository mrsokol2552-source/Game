using System;
using System.Collections.Generic;

namespace Game.Infrastructure.AI.Pathfinding
{
    // Hex pathfinder over odd-r offset grid storage.
    // Exposes the same IGridPathfinder interface: x -> col (q), y -> row (r).
    public class HexPathfinder : IGridPathfinder
    {
        private readonly int _width;
        private readonly int _height;
        private readonly Func<int, int, bool> _isWalkable; // (col,row) walkable

        public HexPathfinder(int width, int height, Func<int, int, bool> isWalkable)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Grid dimensions must be positive.");
            _width = width; _height = height; _isWalkable = isWalkable ?? throw new ArgumentNullException(nameof(isWalkable));
        }

        public static HexPathfinder FromWalkableMap(bool[,] map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            int h = map.GetLength(0);
            int w = map.GetLength(1);
            return new HexPathfinder(w, h, (q, r) => map[r, q]);
        }

        public bool IsReachable(int fromCol, int fromRow, int toCol, int toRow)
        {
            if (!InBounds(fromCol, fromRow) || !InBounds(toCol, toRow)) return false;
            if (!_isWalkable(fromCol, fromRow) || !_isWalkable(toCol, toRow)) return false;
            if (fromCol == toCol && fromRow == toRow) return true;

            var visited = new bool[_height, _width];
            var q = new Queue<(int c, int r)>();
            visited[fromRow, fromCol] = true; q.Enqueue((fromCol, fromRow));
            while (q.Count > 0)
            {
                var (c, r) = q.Dequeue();
                // iterate six neighbors via cube conversion
                var cx = OffsetOddR_ToCubeX(c, r);
                var cz = OffsetOddR_ToCubeZ(c, r);
                var cy = -cx - cz;
                foreach (var (dx, dy, dz) in CubeDirs)
                {
                    int nx = cx + dx, ny = cy + dy, nz = cz + dz;
                    int aq = nx; int ar = nz; // axial q,r
                    var (oc, orow) = AxialToOddR(aq, ar);
                    if (!InBounds(oc, orow)) continue;
                    if (visited[orow, oc]) continue;
                    if (!_isWalkable(oc, orow)) continue;
                    if (oc == toCol && orow == toRow) return true;
                    visited[orow, oc] = true; q.Enqueue((oc, orow));
                }
            }
            return false;
        }

        public bool FindPath(int fromCol, int fromRow, int toCol, int toRow, List<GridPoint> path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            path.Clear();
            if (!InBounds(fromCol, fromRow) || !InBounds(toCol, toRow)) return false;
            if (!_isWalkable(fromCol, fromRow) || !_isWalkable(toCol, toRow)) return false;
            if (fromCol == toCol && fromRow == toRow) { path.Add(new GridPoint(fromCol, fromRow)); return true; }

            const int INF = int.MaxValue / 4;
            var g = new int[_height, _width];
            var cameC = new int[_height, _width];
            var cameR = new int[_height, _width];
            var open = new List<(int c, int r, int f)>();
            var inOpen = new bool[_height, _width];
            var closed = new bool[_height, _width];
            for (int r = 0; r < _height; r++) for (int c = 0; c < _width; c++) { g[r, c] = INF; cameC[r, c] = -1; cameR[r, c] = -1; }

            g[fromRow, fromCol] = 0;
            open.Add((fromCol, fromRow, HexHeuristic(fromCol, fromRow, toCol, toRow)));
            inOpen[fromRow, fromCol] = true;

            while (open.Count > 0)
            {
                int bestIdx = 0; int bestF = open[0].f;
                for (int i = 1; i < open.Count; i++) if (open[i].f < bestF) { bestF = open[i].f; bestIdx = i; }
                var node = open[bestIdx]; open.RemoveAt(bestIdx); inOpen[node.r, node.c] = false;
                if (closed[node.r, node.c]) continue; closed[node.r, node.c] = true;
                if (node.c == toCol && node.r == toRow)
                {
                    // reconstruct into path
                    int cc = toCol, rr = toRow; var rev = new List<GridPoint>(32);
                    rev.Add(new GridPoint(cc, rr));
                    while (!(cc == fromCol && rr == fromRow))
                    {
                        int pc = cameC[rr, cc], pr = cameR[rr, cc]; if (pc < 0) break;
                        cc = pc; rr = pr; rev.Add(new GridPoint(cc, rr));
                    }
                    for (int i = rev.Count - 1; i >= 0; i--) path.Add(rev[i]);
                    return true;
                }

                // six neighbors via cube displacement
                var cx = OffsetOddR_ToCubeX(node.c, node.r);
                var cz = OffsetOddR_ToCubeZ(node.c, node.r);
                var cy = -cx - cz;
                foreach (var (dx, dy, dz) in CubeDirs)
                {
                    int nx = cx + dx, ny = cy + dy, nz = cz + dz;
                    int aq = nx; int ar = nz;
                    var (oc, orow) = AxialToOddR(aq, ar);
                    if (!InBounds(oc, orow) || closed[orow, oc] || !_isWalkable(oc, orow)) continue;
                    int tentative = g[node.r, node.c] + 1;
                    if (tentative < g[orow, oc])
                    {
                        g[orow, oc] = tentative;
                        cameC[orow, oc] = node.c;
                        cameR[orow, oc] = node.r;
                        int f = tentative + HexHeuristic(oc, orow, toCol, toRow);
                        if (!inOpen[orow, oc]) { open.Add((oc, orow, f)); inOpen[orow, oc] = true; }
                    }
                }
            }

            return false;
        }

        private static readonly (int dx, int dy, int dz)[] CubeDirs = new (int, int, int)[]
        {
            ( 1,-1, 0), ( 1, 0,-1), ( 0, 1,-1),
            (-1, 1, 0), (-1, 0, 1), ( 0,-1, 1)
        };

        private static (int q, int r) OddRToAxial(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return (q, r);
        }

        private static (int col, int row) AxialToOddR(int q, int r)
        {
            int col = q + (r - (r & 1)) / 2;
            int row = r;
            return (col, row);
        }

        private static int OffsetOddR_ToCubeX(int col, int row)
        {
            var (q, r) = OddRToAxial(col, row);
            return q; // x
        }
        private static int OffsetOddR_ToCubeZ(int col, int row)
        {
            var (q, r) = OddRToAxial(col, row);
            return r; // z
        }

        private int HexHeuristic(int c0, int r0, int c1, int r1)
        {
            // cube distance between two hexes
            var x0 = OffsetOddR_ToCubeX(c0, r0); var z0 = OffsetOddR_ToCubeZ(c0, r0); var y0 = -x0 - z0;
            var x1 = OffsetOddR_ToCubeX(c1, r1); var z1 = OffsetOddR_ToCubeZ(c1, r1); var y1 = -x1 - z1;
            int dx = Math.Abs(x0 - x1); int dy = Math.Abs(y0 - y1); int dz = Math.Abs(z0 - z1);
            return (dx + dy + dz) / 2;
        }

        private bool InBounds(int col, int row) => col >= 0 && row >= 0 && col < _width && row < _height;
    }
}

