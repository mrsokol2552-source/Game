using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Maintains flow fields for shared targets. Uses time-sliced BFS on the hex grid.
    /// </summary>
    public class FlowFieldManager : MonoBehaviour
    {
        public static FlowFieldManager Instance { get; private set; }

        [Tooltip("Enable flow field generation and queries.")]
        public bool Enabled = true;
        [Tooltip("Max cells processed per frame across all fields.")]
        public int CellsPerFrame = 8000;
        [Tooltip("Max cached fields to keep alive (LRU eviction).")]
        public int MaxFields = 16;
        [Tooltip("Seconds after last use to evict a completed field.")]
        public float FieldTtl = 2.5f;
        [Tooltip("Quantize target cell to reduce unique fields (1 = no quantization).")]
        public int TargetCellStride = 1;
        [Tooltip("Extra cells to expand beyond farthest requester to reduce frequent expansions.")]
        public int DistancePadding = 12;
        [Tooltip("Optional hard cap on max flow field radius in cells (0 = unlimited).")]
        public int MaxDistanceCells = 0;
        [Header("LoS Smoothing")]
        [Tooltip("Use line-of-sight smoothing to skip zigzags on clear paths.")]
        public bool UseLoSSmoothing = true;
        [Tooltip("Max cells to look ahead along the line to the target.")]
        public int LoSMaxRange = 4;
        [Tooltip("Minimum cost improvement to accept a smoothed jump.")]
        public int LoSMinImprovement = 1;

        private readonly Dictionary<int, FlowField> _fields = new Dictionary<int, FlowField>(32);
        private readonly List<int> _staleKeys = new List<int>(16);
        private HexPathfindingBootstrap _hex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Enabled) return;
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_hex == null) return;

            int budget = Mathf.Max(0, CellsPerFrame);
            _staleKeys.Clear();
            foreach (var kv in _fields)
            {
                var field = kv.Value;
                if (field.IsComplete && FieldTtl > 0f && (Time.time - field.LastUsedTime) > FieldTtl)
                {
                    _staleKeys.Add(kv.Key);
                    continue;
                }
                if (!field.IsComplete && budget > 0)
                {
                    budget -= field.Process(_hex, budget);
                }
            }

            for (int i = 0; i < _staleKeys.Count; i++)
                _fields.Remove(_staleKeys[i]);
        }

        public bool TryGetNextPoint(Vector3 fromWorld, Vector3 targetWorld, out Vector3 nextWorld)
        {
            nextWorld = default;
            if (!Enabled) return false;
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
            if (_hex == null) return false;

            var targetCell = QuantizeCell(_hex.WorldToGrid(targetWorld));
            var field = GetField(targetCell);
            if (field == null) return false;
            field.LastUsedTime = Time.time;

            var fromCell = _hex.WorldToGrid(fromWorld);
            int limit = ComputeRequestLimit(fromCell, targetCell);
            field.RegisterRequest(limit);
            if (!field.TryGetNextCell(fromCell, _hex, UseLoSSmoothing, LoSMaxRange, LoSMinImprovement, out var nextCell))
                return false;

            nextWorld = _hex.GridToWorld(nextCell.x, nextCell.y);
            return true;
        }

        private FlowField GetField(Vector2Int targetCell)
        {
            int key = Key(targetCell);
            if (!_fields.TryGetValue(key, out var field))
            {
                EvictIfNeeded();
                field = new FlowField();
                _fields[key] = field;
            }
            int walkableVersion = _hex != null ? _hex.WalkableVersion : 0;
            if (!field.Matches(targetCell, _hex, walkableVersion))
            {
                field.Reset(_hex, targetCell, walkableVersion);
            }
            return field;
        }

        private void EvictIfNeeded()
        {
            if (MaxFields <= 0 || _fields.Count < MaxFields) return;
            int oldestKey = 0;
            float oldestTime = float.MaxValue;
            foreach (var kv in _fields)
            {
                if (kv.Value.LastUsedTime < oldestTime)
                {
                    oldestTime = kv.Value.LastUsedTime;
                    oldestKey = kv.Key;
                }
            }
            if (_fields.Count > 0)
                _fields.Remove(oldestKey);
        }

        private Vector2Int QuantizeCell(Vector2Int cell)
        {
            int stride = Mathf.Max(1, TargetCellStride);
            if (stride <= 1) return cell;
            int qx = (cell.x / stride) * stride + (stride / 2);
            int qy = (cell.y / stride) * stride + (stride / 2);
            if (_hex != null)
            {
                qx = Mathf.Clamp(qx, 0, _hex.Width - 1);
                qy = Mathf.Clamp(qy, 0, _hex.Height - 1);
            }
            return new Vector2Int(qx, qy);
        }

        private static int Key(Vector2Int cell) => (cell.y << 16) ^ (cell.x & 0xFFFF);

        private int ComputeRequestLimit(Vector2Int fromCell, Vector2Int targetCell)
        {
            int dist = HexDistance(fromCell, targetCell);
            int limit = dist + Mathf.Max(0, DistancePadding);
            if (limit < 1) limit = 1;
            if (MaxDistanceCells > 0)
                limit = Mathf.Min(limit, MaxDistanceCells);
            return limit;
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            int aq = a.x - (a.y - (a.y & 1)) / 2;
            int ar = a.y;
            int bq = b.x - (b.y - (b.y & 1)) / 2;
            int br = b.y;
            int dq = aq - bq;
            int dr = ar - br;
            int ds = (aq + ar) - (bq + br);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("FlowFieldManager");
            go.AddComponent<FlowFieldManager>();
        }

        private class FlowField
        {
            public Vector2Int TargetCell;
            public int WalkableVersion;
            public int Width;
            public int Height;
            public int[] Integration;
            public bool IsComplete;
            public float LastUsedTime;
            public int MaxDistanceLimit;

            private readonly Queue<int> _frontier = new Queue<int>(256);
            private readonly List<int> _deferred = new List<int>(256);

            private static readonly Vector2Int[] EvenOffsets =
            {
                new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, -1),
                new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
            };

            private static readonly Vector2Int[] OddOffsets =
            {
                new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
                new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
            };

            public bool Matches(Vector2Int targetCell, HexPathfindingBootstrap hex, int walkableVersion)
            {
                if (hex == null) return false;
                if (Width != hex.Width || Height != hex.Height) return false;
                if (WalkableVersion != walkableVersion) return false;
                return TargetCell == targetCell;
            }

            public void Reset(HexPathfindingBootstrap hex, Vector2Int targetCell, int walkableVersion)
            {
                if (hex == null) return;
                TargetCell = targetCell;
                WalkableVersion = walkableVersion;
                Width = hex.Width;
                Height = hex.Height;
                MaxDistanceLimit = 0;
                _deferred.Clear();
                int size = Width * Height;
                if (Integration == null || Integration.Length != size)
                    Integration = new int[size];
                Array.Fill(Integration, int.MaxValue);
                _frontier.Clear();

                int tIdx = targetCell.y * Width + targetCell.x;
                if (IsWalkable(hex, tIdx))
                {
                    Integration[tIdx] = 0;
                    _frontier.Enqueue(tIdx);
                    IsComplete = false;
                }
                else
                {
                    IsComplete = true;
                }
            }

            public void RegisterRequest(int limit)
            {
                if (limit <= 0) return;
                if (limit > MaxDistanceLimit)
                {
                    MaxDistanceLimit = limit;
                    if (_deferred.Count > 0)
                    {
                        for (int i = 0; i < _deferred.Count; i++)
                            _frontier.Enqueue(_deferred[i]);
                        _deferred.Clear();
                    }
                    if (IsComplete) IsComplete = false;
                }
            }

            public int Process(HexPathfindingBootstrap hex, int budget)
            {
                if (IsComplete || hex == null || budget <= 0) return 0;
                var walkable = hex.GetWalkableNative();
                int processed = 0;
                int max = Width * Height;
                if (!walkable.IsCreated || walkable.Length != max)
                    return 0;
                int limit = MaxDistanceLimit > 0 ? MaxDistanceLimit : int.MaxValue;

                while (_frontier.Count > 0 && processed < budget)
                {
                    int idx = _frontier.Dequeue();
                    int col = idx % Width;
                    int row = idx / Width;
                    int baseCost = Integration[idx];
                    if (baseCost >= limit)
                    {
                        if (MaxDistanceLimit > 0)
                            _deferred.Add(idx);
                        processed++;
                        continue;
                    }

                    var offs = (row & 1) == 0 ? EvenOffsets : OddOffsets;
                    for (int i = 0; i < 6; i++)
                    {
                        int nc = col + offs[i].x;
                        int nr = row + offs[i].y;
                        if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                        int nIdx = nr * Width + nc;
                        if (!IsWalkable(hex, nIdx, walkable)) continue;
                        int nextCost = baseCost + 1;
                        if (Integration[nIdx] > nextCost)
                        {
                            Integration[nIdx] = nextCost;
                            _frontier.Enqueue(nIdx);
                        }
                    }
                    processed++;
                }

                if (_frontier.Count == 0)
                    IsComplete = true;
                return processed;
            }

            public bool TryGetNextCell(Vector2Int fromCell, HexPathfindingBootstrap hex, bool useLoS, int loSRange, int loSMinImprovement, out Vector2Int nextCell)
            {
                nextCell = fromCell;
                if (Integration == null) return false;
                if (fromCell.x < 0 || fromCell.y < 0 || fromCell.x >= Width || fromCell.y >= Height) return false;

                int idx = fromCell.y * Width + fromCell.x;
                int fromCost = Integration[idx];
                if (fromCost == int.MaxValue) return false;

                var offs = (fromCell.y & 1) == 0 ? EvenOffsets : OddOffsets;
                Vector2Int bestCell = fromCell;
                int bestCost = fromCost;
                for (int i = 0; i < 6; i++)
                {
                    int nc = fromCell.x + offs[i].x;
                    int nr = fromCell.y + offs[i].y;
                    if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                    int nIdx = nr * Width + nc;
                    int cost = Integration[nIdx];
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestCell = new Vector2Int(nc, nr);
                    }
                }

                if (bestCell == fromCell) return false;

                if (useLoS && hex != null && loSRange > 1)
                {
                    int distToTarget = HexDistance(fromCell, TargetCell);
                    if (distToTarget > 1)
                    {
                        int maxStep = Mathf.Min(loSRange, distToTarget);
                        int minImprovement = Mathf.Max(1, loSMinImprovement);
                        var walkable = hex.GetWalkableNative();
                        Vector2Int lastCandidate = fromCell;
                        for (int step = maxStep; step >= 2; step--)
                        {
                            var candidate = StepToward(fromCell, TargetCell, step, distToTarget);
                            if (candidate == fromCell || candidate == lastCandidate) continue;
                            lastCandidate = candidate;
                            if (candidate.x < 0 || candidate.y < 0 || candidate.x >= Width || candidate.y >= Height) continue;
                            int cIdx = candidate.y * Width + candidate.x;
                            int cCost = Integration[cIdx];
                            if (cCost == int.MaxValue) continue;
                            if (cCost > fromCost - minImprovement) continue;
                            if (HasLineOfSight(hex, fromCell, candidate, walkable))
                            {
                                nextCell = candidate;
                                return true;
                            }
                        }
                    }
                }

                nextCell = bestCell;
                return true;
            }

            private static bool IsWalkable(HexPathfindingBootstrap hex, int idx)
            {
                var walkable = hex.GetWalkableNative();
                if (!walkable.IsCreated) return false;
                if (idx < 0 || idx >= walkable.Length) return false;
                return walkable[idx] != 0;
            }

            private static bool IsWalkable(HexPathfindingBootstrap hex, int idx, NativeArray<byte> walkable)
            {
                if (!walkable.IsCreated) return false;
                if (idx < 0 || idx >= walkable.Length) return false;
                return walkable[idx] != 0;
            }

            private static bool IsWalkable(HexPathfindingBootstrap hex, Vector2Int cell, NativeArray<byte> walkable)
            {
                if (hex == null || !walkable.IsCreated) return false;
                if (cell.x < 0 || cell.y < 0 || cell.x >= hex.Width || cell.y >= hex.Height) return false;
                int idx = cell.y * hex.Width + cell.x;
                if (idx < 0 || idx >= walkable.Length) return false;
                return walkable[idx] != 0;
            }

            private static bool HasLineOfSight(HexPathfindingBootstrap hex, Vector2Int from, Vector2Int to, NativeArray<byte> walkable)
            {
                if (hex == null) return false;
                int n = HexDistance(from, to);
                if (n <= 1) return true;
                for (int i = 1; i <= n; i++)
                {
                    float t = n > 0 ? (float)i / n : 0f;
                    var cell = CubeToOddR(CubeRound(CubeLerp(OddRToCube(from), OddRToCube(to), t)));
                    if (!IsWalkable(hex, cell, walkable))
                        return false;
                }
                return true;
            }

            private static Vector3Int OddRToCube(Vector2Int cell)
            {
                int x = cell.x - (cell.y - (cell.y & 1)) / 2;
                int z = cell.y;
                int y = -x - z;
                return new Vector3Int(x, y, z);
            }

            private static Vector2Int CubeToOddR(Vector3Int cube)
            {
                int col = cube.x + (cube.z - (cube.z & 1)) / 2;
                int row = cube.z;
                return new Vector2Int(col, row);
            }

            private static Vector3 CubeLerp(Vector3Int a, Vector3Int b, float t)
            {
                return new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t));
            }

            private static Vector3Int CubeRound(Vector3 cube)
            {
                int rx = Mathf.RoundToInt(cube.x);
                int ry = Mathf.RoundToInt(cube.y);
                int rz = Mathf.RoundToInt(cube.z);
                float dx = Mathf.Abs(rx - cube.x);
                float dy = Mathf.Abs(ry - cube.y);
                float dz = Mathf.Abs(rz - cube.z);
                if (dx > dy && dx > dz) rx = -ry - rz;
                else if (dy > dz) ry = -rx - rz;
                else rz = -rx - ry;
                return new Vector3Int(rx, ry, rz);
            }

            private static Vector2Int StepToward(Vector2Int from, Vector2Int to, int step, int total)
            {
                if (total <= 0) return from;
                int clampedStep = Mathf.Clamp(step, 0, total);
                float t = total > 0 ? (float)clampedStep / total : 0f;
                var cube = CubeRound(CubeLerp(OddRToCube(from), OddRToCube(to), t));
                return CubeToOddR(cube);
            }

            private static int HexDistance(Vector2Int a, Vector2Int b)
            {
                int aq = a.x - (a.y - (a.y & 1)) / 2;
                int ar = a.y;
                int bq = b.x - (b.y - (b.y & 1)) / 2;
                int br = b.y;
                int dq = aq - bq;
                int dr = ar - br;
                int ds = (aq + ar) - (bq + br);
                return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
            }
        }
    }
}
