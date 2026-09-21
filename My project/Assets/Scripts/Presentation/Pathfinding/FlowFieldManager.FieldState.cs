/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.cs
@module: presentation.pathfinding.flowfields.fieldstate
@purpose: Holds per-target flow-field integration state, tile-gating state, line-of-sight flags, and next-cell sampling logic.
@entry: FFLD-07
@api: internal nested storage used by FlowFieldManager cache
@deps: HexPathfindingBootstrap, crowd/influence cost info, tile graph activation state
@data: integration array, frontier queue, deferred queue, active tiles, LoS flags
@perf: hotpath, incremental BFS and per-request next-step sampling
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual squad movement verification
@config: Uses FlowFieldManager runtime settings passed into TryGetNextCell and Process
@assets: none
@notes: This layer is intentionally isolated so the future data-extraction phase can replace storage/solver internals without touching higher-level cache orchestration.
*/

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER-FIELDSTATE]
// Logical block: Scripts/Presentation/Pathfinding/FlowFieldManager.FieldState.

namespace Game.Presentation.Pathfinding
{
    public partial class FlowFieldManager
    {
        // [FFLD-07]
        // Per-target flow-field storage and the incremental integration build state.
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
            public bool UseTileGating;
            public int TileSize;
            public int TileCols;
            public int TileRows;
            public bool[] ActiveTiles;
            public byte[] LoSFlags;

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

            private static readonly int[][] DirectionPriority =
            {
                new[] { 0, 1, 5, 2, 4, 3 },
                new[] { 1, 2, 0, 3, 5, 4 },
                new[] { 2, 3, 1, 4, 0, 5 },
                new[] { 3, 4, 2, 5, 1, 0 },
                new[] { 4, 5, 3, 0, 2, 1 },
                new[] { 5, 0, 4, 1, 3, 2 }
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
                UseTileGating = false;
                TileSize = 0;
                TileCols = 0;
                TileRows = 0;
                if (ActiveTiles != null)
                    Array.Clear(ActiveTiles, 0, ActiveTiles.Length);
                if (LoSFlags == null || LoSFlags.Length != size)
                    LoSFlags = new byte[size];
                else
                    Array.Clear(LoSFlags, 0, LoSFlags.Length);

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

            public void RegisterTiles(IList<int> tiles, int tileCols, int tileRows, int tileSize)
            {
                if (tiles == null || tiles.Count == 0) return;
                int count = tileCols * tileRows;
                if (count <= 0) return;
                if (ActiveTiles == null || ActiveTiles.Length != count)
                    ActiveTiles = new bool[count];
                TileCols = tileCols;
                TileRows = tileRows;
                TileSize = Mathf.Max(1, tileSize);
                UseTileGating = true;
                for (int i = 0; i < tiles.Count; i++)
                {
                    int t = tiles[i];
                    if (t < 0 || t >= count) continue;
                    ActiveTiles[t] = true;
                }
            }

            public void DisableTileGating()
            {
                UseTileGating = false;
            }

            private bool IsTileAllowed(int col, int row)
            {
                if (!UseTileGating) return true;
                if (TileSize <= 0 || ActiveTiles == null || TileCols <= 0 || TileRows <= 0)
                    return true;
                int tx = col / TileSize;
                int ty = row / TileSize;
                if (tx < 0 || ty < 0 || tx >= TileCols || ty >= TileRows)
                    return false;
                int idx = ty * TileCols + tx;
                if (idx < 0 || idx >= ActiveTiles.Length) return false;
                return ActiveTiles[idx];
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
                    if (!IsTileAllowed(col, row))
                    {
                        processed++;
                        continue;
                    }
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
                        if (!IsTileAllowed(nc, nr)) continue;
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

            public bool TryGetNextCell(
                Vector2Int fromCell,
                HexPathfindingBootstrap hex,
                bool useDeterministic,
                bool useLoS,
                int loSRange,
                int loSMinImprovement,
                CrowdCostInfo crowd,
                InfluenceCostInfo influence,
                bool computeVector,
                out Vector2Int nextCell,
                out Vector3 flowDir,
                out bool flowDirIsLoS)
            {
                nextCell = fromCell;
                flowDir = Vector3.zero;
                flowDirIsLoS = false;
                if (Integration == null) return false;
                if (fromCell.x < 0 || fromCell.y < 0 || fromCell.x >= Width || fromCell.y >= Height) return false;
                if (!IsTileAllowed(fromCell.x, fromCell.y)) return false;

                int idx = fromCell.y * Width + fromCell.x;
                int fromCost = Integration[idx];
                if (fromCost == int.MaxValue) return false;

                var offs = (fromCell.y & 1) == 0 ? EvenOffsets : OddOffsets;
                int[] dirOrder = null;
                Vector3 fromWorld = default;
                Vector3 flowSum = Vector3.zero;
                float flowWeight = 0f;
                bool useVector = computeVector && hex != null;
                if (useVector)
                {
                    fromWorld = hex.GridToWorld(fromCell.x, fromCell.y);
                }
                if (useDeterministic)
                {
                    int primary = GetPreferredDirection(fromCell, offs);
                    dirOrder = DirectionPriority[primary];
                }
                Vector2Int bestCell = fromCell;
                int bestScore = int.MaxValue;
                bool found = false;
                for (int i = 0; i < 6; i++)
                {
                    int dir = useDeterministic ? dirOrder[i] : i;
                    int nc = fromCell.x + offs[dir].x;
                    int nr = fromCell.y + offs[dir].y;
                    if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                    if (!IsTileAllowed(nc, nr)) continue;
                    int nIdx = nr * Width + nc;
                    int cost = Integration[nIdx];
                    if (cost == int.MaxValue) continue;
                    bool downhill = cost < fromCost;
                    int detourAllowance = 0;
                    if (crowd.Enabled) detourAllowance = Mathf.Max(detourAllowance, crowd.DetourAllowance);
                    if (influence.Enabled) detourAllowance = Mathf.Max(detourAllowance, influence.DetourAllowance);
                    if (!downhill && detourAllowance > 0)
                    {
                        if (cost > fromCost + detourAllowance)
                            continue;
                    }
                    else if (!downhill)
                    {
                        continue;
                    }

                    int penalty = 0;
                    if (crowd.Enabled) penalty += GetCrowdPenalty(nc, nr, crowd);
                    if (influence.Enabled) penalty += GetInfluencePenalty(nc, nr, influence);
                    int score = cost + penalty;
                    if (score < bestScore)
                    {
                        bestCell = new Vector2Int(nc, nr);
                        bestScore = score;
                        found = true;
                    }
                    if (useVector)
                    {
                        int improvement = fromCost - score;
                        if (improvement > 0)
                        {
                            var neighborWorld = hex.GridToWorld(nc, nr);
                            flowSum += (neighborWorld - fromWorld) * improvement;
                            flowWeight += improvement;
                        }
                    }
                }

                if (!found || bestCell == fromCell) return false;

                if (useLoS && hex != null && loSRange > 1)
                {
                    int distToTarget = HexDistance(fromCell, TargetCell);
                    if (distToTarget > 1)
                    {
                        int maxStep = Mathf.Min(loSRange, distToTarget);
                        int minImprovement = Mathf.Max(1, loSMinImprovement);
                        var walkable = hex.GetWalkableNative();
                        bool hasLoSToTarget = false;
                        bool loSKnown = TryGetLoSToTarget(idx, fromCell, hex, walkable, out hasLoSToTarget);
                        Vector2Int lastCandidate = fromCell;
                        for (int step = maxStep; step >= 2; step--)
                        {
                            var candidate = StepToward(fromCell, TargetCell, step, distToTarget);
                            if (candidate == fromCell || candidate == lastCandidate) continue;
                            lastCandidate = candidate;
                            if (candidate.x < 0 || candidate.y < 0 || candidate.x >= Width || candidate.y >= Height) continue;
                            if (!IsTileAllowed(candidate.x, candidate.y)) continue;
                            int cIdx = candidate.y * Width + candidate.x;
                            int cCost = Integration[cIdx];
                            if (cCost == int.MaxValue) continue;
                            int cPenalty = 0;
                            if (crowd.Enabled) cPenalty += GetCrowdPenalty(candidate.x, candidate.y, crowd);
                            if (influence.Enabled) cPenalty += GetInfluencePenalty(candidate.x, candidate.y, influence);
                            int cScore = cCost + cPenalty;
                            if (cScore > fromCost - minImprovement) continue;
                            if (cScore >= bestScore) continue;
                            if (loSKnown && hasLoSToTarget)
                            {
                                nextCell = candidate;
                                if (useVector)
                                {
                                    var candidateWorld = hex.GridToWorld(candidate.x, candidate.y);
                                    flowDir = candidateWorld - fromWorld;
                                    flowDirIsLoS = true;
                                }
                                return true;
                            }
                            if (HasLineOfSight(hex, fromCell, candidate, walkable))
                            {
                                nextCell = candidate;
                                if (useVector)
                                {
                                    var candidateWorld = hex.GridToWorld(candidate.x, candidate.y);
                                    flowDir = candidateWorld - fromWorld;
                                    flowDirIsLoS = true;
                                }
                                return true;
                            }
                        }
                    }
                }

                nextCell = bestCell;
                if (useVector && flowWeight > 0f)
                    flowDir = flowSum / flowWeight;
                return true;
            }

            private int GetPreferredDirection(Vector2Int fromCell, Vector2Int[] offsets)
            {
                int bestDir = 0;
                int bestDist = int.MaxValue;
                for (int i = 0; i < 6; i++)
                {
                    int nc = fromCell.x + offsets[i].x;
                    int nr = fromCell.y + offsets[i].y;
                    if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                    int dist = HexDistance(new Vector2Int(nc, nr), TargetCell);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestDir = i;
                    }
                }
                return bestDir;
            }

            private static int GetCrowdPenalty(int col, int row, CrowdCostInfo crowd)
            {
                if (!crowd.Enabled || crowd.Count == null || crowd.Stamp == null) return 0;
                if (col < 0 || row < 0 || col >= crowd.Width || row >= crowd.Height) return 0;
                int idx = row * crowd.Width + col;
                if (idx < 0 || idx >= crowd.Count.Length) return 0;
                if (crowd.Stamp[idx] != crowd.StampValue) return 0;
                int count = crowd.Count[idx];
                if (count < crowd.MinUnits) return 0;
                int penalty = (count - crowd.MinUnits + 1) * crowd.CostPerUnit;
                if (crowd.MaxCost > 0 && penalty > crowd.MaxCost)
                    penalty = crowd.MaxCost;
                return penalty;
            }

            private static int GetInfluencePenalty(int col, int row, InfluenceCostInfo influence)
            {
                if (!influence.Enabled || influence.Values == null || influence.Stamp == null) return 0;
                if (col < 0 || row < 0 || col >= influence.Width || row >= influence.Height) return 0;
                int idx = row * influence.Width + col;
                if (idx < 0 || idx >= influence.Values.Length) return 0;
                if (influence.Stamp[idx] != influence.StampValue) return 0;
                int penalty = influence.Values[idx];
                if (influence.MaxCost > 0 && penalty > influence.MaxCost)
                    penalty = influence.MaxCost;
                return penalty;
            }

            private bool TryGetLoSToTarget(int idx, Vector2Int cell, HexPathfindingBootstrap hex, NativeArray<byte> walkable, out bool hasLoS)
            {
                hasLoS = false;
                if (LoSFlags == null || idx < 0 || idx >= LoSFlags.Length) return false;
                byte flag = LoSFlags[idx];
                if (flag == 1)
                {
                    hasLoS = true;
                    return true;
                }
                if (flag == 2)
                {
                    hasLoS = false;
                    return true;
                }
                if (hex == null || !walkable.IsCreated) return false;
                hasLoS = HasLineOfSight(hex, cell, TargetCell, walkable);
                LoSFlags[idx] = hasLoS ? (byte)1 : (byte)2;
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
