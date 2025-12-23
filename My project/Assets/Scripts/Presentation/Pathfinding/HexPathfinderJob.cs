using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Simple A* for hex grid using NativeArray walkable and NativeHashMap occupied. Runs as a job.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    public struct HexPathfinderJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Walkable; // 0 = blocked, 1 = walkable
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public int StartCol;
        [ReadOnly] public int StartRow;
        [ReadOnly] public int GoalCol;
        [ReadOnly] public int GoalRow;
        [ReadOnly] public NativeHashMap<int, byte> Occupied; // hash(cell) -> 1
        [ReadOnly] public int MaxNodes; // safety cap
        public NativeList<int2> Result; // path cells (col,row)

        public void Execute()
        {
            Result.Clear();
            if (!InBounds(StartCol, StartRow) || !InBounds(GoalCol, GoalRow)) return;
            int capacity = Mathf.Min(MaxNodes > 0 ? MaxNodes : 2048, Width * Height);
            var openSet = new NativeMinHeap(capacity, Allocator.Temp);
            var cameFrom = new NativeHashMap<int, int>(capacity, Allocator.Temp);
            var gScore = new NativeHashMap<int, int>(capacity, Allocator.Temp);
            var fScore = new NativeHashMap<int, int>(capacity, Allocator.Temp);

            int startKey = Key(StartCol, StartRow);
            gScore[startKey] = 0;
            fScore[startKey] = Heuristic(StartCol, StartRow, GoalCol, GoalRow);
            openSet.Insert(fScore[startKey], startKey);

            int nodes = 0;
            while (openSet.Count > 0 && (MaxNodes <= 0 || nodes < MaxNodes))
            {
                nodes++;
                int currentKey = openSet.ExtractMin();
                int cCol, cRow;
                Unkey(currentKey, out cCol, out cRow);
                if (cCol == GoalCol && cRow == GoalRow)
                {
                    Reconstruct(cameFrom, currentKey, Result);
                    openSet.Dispose();
                    cameFrom.Dispose();
                    gScore.Dispose();
                    fScore.Dispose();
                    return;
                }

                for (int i = 0; i < 6; i++)
                {
                    Neighbor(cCol, cRow, i, out var nCol, out var nRow);
                    if (!InBounds(nCol, nRow)) continue;
                    if (!IsWalkable(nCol, nRow)) continue;
                    int nKey = Key(nCol, nRow);
                    if (Occupied.IsCreated && Occupied.ContainsKey(nKey)) continue;

                    int tentativeG = gScore.ContainsKey(currentKey) ? gScore[currentKey] + 1 : int.MaxValue;
                    if (!gScore.ContainsKey(nKey) || tentativeG < gScore[nKey])
                    {
                        cameFrom[nKey] = currentKey;
                        gScore[nKey] = tentativeG;
                        int f = tentativeG + Heuristic(nCol, nRow, GoalCol, GoalRow);
                        fScore[nKey] = f;
                        openSet.Insert(f, nKey);
                    }
                }
            }

            openSet.Dispose();
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();
        }

        private bool InBounds(int col, int row) => col >= 0 && row >= 0 && col < Width && row < Height;

        private bool IsWalkable(int col, int row)
        {
            int idx = row * Width + col;
            if (idx < 0 || idx >= Walkable.Length) return false;
            return Walkable[idx] != 0;
        }

        private static int Heuristic(int c, int r, int gc, int gr)
        {
            int dq = c - gc;
            int dr = r - gr;
            return Mathf.Abs(dq) + Mathf.Abs(dr);
        }

        private static int Key(int col, int row) => (row << 16) ^ (col & 0xFFFF);
        private static void Unkey(int key, out int col, out int row)
        {
            row = key >> 16;
            col = key & 0xFFFF;
        }

        // odd-r neighbors
        private void Neighbor(int col, int row, int dir, out int ncol, out int nrow)
        {
            // dirs: E, NE, NW, W, SW, SE (clockwise)
            int2 offs = ((row & 1) == 0) ? EvenOffsets[dir] : OddOffsets[dir];
            ncol = col + offs.x;
            nrow = row + offs.y;
        }

        private static readonly int2[] EvenOffsets = new int2[]
        {
            new int2(1,0), new int2(0,-1), new int2(-1,-1),
            new int2(-1,0), new int2(-1,1), new int2(0,1)
        };

        private static readonly int2[] OddOffsets = new int2[]
        {
            new int2(1,0), new int2(1,-1), new int2(0,-1),
            new int2(-1,0), new int2(0,1), new int2(1,1)
        };

        private struct HeapNode
        {
            public int Key;
            public int Value;
        }

        private struct NativeMinHeap
        {
            private NativeList<int> _keys;
            private NativeList<int> _vals;

            public int Count => _keys.IsCreated ? _keys.Length : 0;

            public NativeMinHeap(int capacity, Allocator alloc)
            {
                _keys = new NativeList<int>(capacity, alloc);
                _vals = new NativeList<int>(capacity, alloc);
            }

            public void Insert(int key, int val)
            {
                _keys.Add(key);
                _vals.Add(val);
                HeapifyUp(_keys.Length - 1);
            }

            public int ExtractMin()
            {
                int minVal = _vals[0];
                int lastIdx = _keys.Length - 1;
                _keys[0] = _keys[lastIdx];
                _vals[0] = _vals[lastIdx];
                _keys.RemoveAt(lastIdx);
                _vals.RemoveAt(lastIdx);
                HeapifyDown(0);
                return minVal;
            }

            public void Dispose()
            {
                if (_keys.IsCreated) _keys.Dispose();
                if (_vals.IsCreated) _vals.Dispose();
            }

            private void HeapifyUp(int idx)
            {
                while (idx > 0)
                {
                    int parent = (idx - 1) >> 1;
                    if (_keys[parent] <= _keys[idx]) break;
                    Swap(parent, idx);
                    idx = parent;
                }
            }

            private void HeapifyDown(int idx)
            {
                int count = _keys.Length;
                while (true)
                {
                    int left = (idx << 1) + 1;
                    int right = left + 1;
                    int smallest = idx;
                    if (left < count && _keys[left] < _keys[smallest]) smallest = left;
                    if (right < count && _keys[right] < _keys[smallest]) smallest = right;
                    if (smallest == idx) break;
                    Swap(idx, smallest);
                    idx = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                int k = _keys[a]; _keys[a] = _keys[b]; _keys[b] = k;
                int v = _vals[a]; _vals[a] = _vals[b]; _vals[b] = v;
            }
        }

        private void Reconstruct(NativeHashMap<int, int> cameFrom, int currentKey, NativeList<int2> outPath)
        {
            outPath.Clear();
            int safety = 0;
            while (safety++ < MaxNodes && cameFrom.ContainsKey(currentKey))
            {
                int col, row;
                Unkey(currentKey, out col, out row);
                outPath.Add(new int2(col, row));
                currentKey = cameFrom[currentKey];
            }
            // add start
            int sc, sr;
            Unkey(currentKey, out sc, out sr);
            outPath.Add(new int2(sc, sr));
            // reverse
            for (int i = 0, j = outPath.Length - 1; i < j; i++, j--)
            {
                var tmp = outPath[i];
                outPath[i] = outPath[j];
                outPath[j] = tmp;
            }
        }
    }
}
