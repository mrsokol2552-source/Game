using System;
using System.Collections.Generic;
using Game.Presentation.View;
using Game.Domain.Units;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

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
        [Header("Tiled Flow Fields")]
        [Tooltip("Restrict flow field generation to active tiles along coarse paths.")]
        public bool UseTiledFields = true;
        [Tooltip("Tile size in cells for the coarse flow graph.")]
        public int TileSize = 32;
        [Tooltip("Extra tiles to include around the coarse path.")]
        public int TilePadding = 1;
        [Header("Crowd Cost (Discomfort)")]
        [Tooltip("Use per-cell crowd penalties when selecting next flow step.")]
        public bool UseCrowdCosts = true;
        [Tooltip("Units in a cell before penalties start.")]
        public int CrowdMinUnits = 2;
        [Tooltip("Penalty added per extra unit in a cell.")]
        public int CrowdCostPerUnit = 2;
        [Tooltip("Clamp crowd penalty (0 = unlimited).")]
        public int CrowdCostMax = 8;
        [Tooltip("Allow stepping to slightly higher integration cost to avoid crowds.")]
        public int CrowdDetourAllowance = 1;
        [Header("Deterministic Flow")]
        [Tooltip("Bias flow selection toward the target direction for stable results.")]
        public bool UseDeterministicDirections = true;
        [Header("Vector Sampling")]
        [Tooltip("Use flow vector sampling to smooth motion and reduce grid jaggies.")]
        public bool UseVectorSampling = true;
        [Tooltip("Step size as a fraction of neighbor distance (0..1).")]
        public float VectorStepFraction = 0.85f;
        [Header("Influence Cost")]
        [Tooltip("Apply influence costs from nearby enemy units to steer around threats.")]
        public bool UseInfluenceCosts = true;
        [Tooltip("Influence radius in hex cells around each enemy unit.")]
        public int InfluenceRadiusHex = 6;
        [Tooltip("Cost added per influence weight unit.")]
        public int InfluenceCostPerUnit = 2;
        [Tooltip("Clamp influence penalty per cell (0 = unlimited).")]
        public int InfluenceCostMax = 12;
        [Tooltip("Allow stepping to slightly higher integration cost to avoid influence.")]
        public int InfluenceDetourAllowance = 2;
        [Tooltip("How often to rebuild influence map (seconds).")]
        public float InfluenceUpdateInterval = 0.2f;
        [Tooltip("Random jitter added to influence update interval.")]
        public float InfluenceUpdateJitter = 0.05f;
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
        private int[] _crowdCount;
        private int[] _crowdStamp;
        private int _crowdStampValue;
        private int[] _influenceForPlayer;
        private int[] _influenceForEnemy;
        private int[] _influenceForPlayerStamp;
        private int[] _influenceForEnemyStamp;
        private int _influenceStampValue;
        private float _influenceTimer;
        private readonly List<AxialOffset> _influenceOffsets = new List<AxialOffset>(128);
        private int _influenceRadiusCached = -1;
        private int _tileCols;
        private int _tileRows;
        private int _tileCount;
        private int _tileGraphWalkableVersion = -1;
        private int _tileGraphTileSize = -1;
        private int _tileGraphWidth = -1;
        private int _tileGraphHeight = -1;
        private List<int>[] _tileNeighbors;
        private bool[] _tileHasWalkable;
        private int[] _tilePrev;
        private int[] _tileVisit;
        private int _tileVisitStamp;
        private int[] _tileQueue;
        private int[] _tilePadVisit;
        private int[] _tilePadDist;
        private int _tilePadStamp;
        private readonly List<int> _tilePath = new List<int>(64);
        private readonly List<int> _tilePathExpanded = new List<int>(128);

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

        private struct CrowdCostInfo
        {
            public bool Enabled;
            public int[] Count;
            public int[] Stamp;
            public int StampValue;
            public int Width;
            public int Height;
            public int MinUnits;
            public int CostPerUnit;
            public int MaxCost;
            public int DetourAllowance;
        }

        private struct InfluenceCostInfo
        {
            public bool Enabled;
            public int[] Values;
            public int[] Stamp;
            public int StampValue;
            public int Width;
            public int Height;
            public int MaxCost;
            public int DetourAllowance;
        }

        private struct AxialOffset
        {
            public int q;
            public int r;
            public int weight;
            public AxialOffset(int q, int r, int weight)
            {
                this.q = q;
                this.r = r;
                this.weight = weight;
            }
        }

        private struct Axial
        {
            public int q;
            public int r;
            public Axial(int q, int r)
            {
                this.q = q;
                this.r = r;
            }
        }

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

            if (UseCrowdCosts)
            {
                UpdateCrowdMap();
            }
            if (UseInfluenceCosts)
            {
                _influenceTimer -= Time.deltaTime;
                if (_influenceTimer <= 0f)
                {
                    UpdateInfluenceMap();
                    float interval = Mathf.Max(0f, InfluenceUpdateInterval);
                    _influenceTimer = interval + Random.Range(0f, Mathf.Max(0f, InfluenceUpdateJitter));
                }
            }

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

        public bool TryGetNextPoint(Vector3 fromWorld, Vector3 targetWorld, Faction faction, out Vector3 nextWorld)
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
            var crowdInfo = BuildCrowdInfo();
            var influenceInfo = BuildInfluenceInfo(faction);
            if (!UseTiledFields)
            {
                field.DisableTileGating();
            }
            else if (EnsureTileGraph())
            {
                if (TryGetTilePath(fromCell, targetCell, _tilePath))
                {
                    ExpandTilePath(_tilePath, Mathf.Max(0, TilePadding), _tilePathExpanded);
                    field.RegisterTiles(_tilePathExpanded, _tileCols, _tileRows, Mathf.Max(1, TileSize));
                }
                else
                {
                    field.DisableTileGating();
                }
            }
            else
            {
                field.DisableTileGating();
            }
            int limit = ComputeRequestLimit(fromCell, targetCell);
            field.RegisterRequest(limit);
            if (!field.TryGetNextCell(fromCell, _hex, UseDeterministicDirections, UseLoSSmoothing, LoSMaxRange, LoSMinImprovement, crowdInfo, influenceInfo, UseVectorSampling, out var nextCell, out var flowDir, out var flowDirIsLoS))
                return false;

            if (UseVectorSampling && flowDir.sqrMagnitude > 0.0001f)
            {
                Vector3 candidate;
                if (flowDirIsLoS)
                {
                    candidate = fromWorld + flowDir;
                }
                else
                {
                    float step = ComputeVectorStepDistance();
                    if (step <= 0f)
                        candidate = Vector3.zero;
                    else
                        candidate = fromWorld + flowDir.normalized * step;
                }
                if (candidate != Vector3.zero && _hex.IsWalkableWorld(candidate))
                {
                    nextWorld = candidate;
                    return true;
                }
            }

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

        private void UpdateCrowdMap()
        {
            if (_hex == null) return;
            if (!UseCrowdCosts) return;
            int size = _hex.Width * _hex.Height;
            if (size <= 0) return;
            if (_crowdCount == null || _crowdCount.Length != size)
            {
                _crowdCount = new int[size];
                _crowdStamp = new int[size];
                _crowdStampValue = 1;
            }

            int stamp = ++_crowdStampValue;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_crowdStamp, 0, _crowdStamp.Length);
                _crowdStampValue = 1;
                stamp = 1;
            }

            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                var cell = _hex.WorldToGrid(uc.transform.position);
                if (cell.x < 0 || cell.y < 0 || cell.x >= _hex.Width || cell.y >= _hex.Height) continue;
                int idx = cell.y * _hex.Width + cell.x;
                if (_crowdStamp[idx] != stamp)
                {
                    _crowdStamp[idx] = stamp;
                    _crowdCount[idx] = 1;
                }
                else
                {
                    _crowdCount[idx]++;
                }
            }

            _crowdStampValue = stamp;
        }

        private CrowdCostInfo BuildCrowdInfo()
        {
            if (!UseCrowdCosts || _hex == null || _crowdCount == null || _crowdStamp == null)
            {
                return new CrowdCostInfo { Enabled = false };
            }
            return new CrowdCostInfo
            {
                Enabled = UseCrowdCosts,
                Count = _crowdCount,
                Stamp = _crowdStamp,
                StampValue = _crowdStampValue,
                Width = _hex.Width,
                Height = _hex.Height,
                MinUnits = Mathf.Max(0, CrowdMinUnits),
                CostPerUnit = Mathf.Max(0, CrowdCostPerUnit),
                MaxCost = CrowdCostMax,
                DetourAllowance = Mathf.Max(0, CrowdDetourAllowance)
            };
        }

        private InfluenceCostInfo BuildInfluenceInfo(Faction faction)
        {
            if (!UseInfluenceCosts || _hex == null)
                return new InfluenceCostInfo { Enabled = false };
            if (_influenceForPlayer == null || _influenceForEnemy == null ||
                _influenceForPlayerStamp == null || _influenceForEnemyStamp == null)
                return new InfluenceCostInfo { Enabled = false };

            int[] values;
            int[] stamp;
            switch (faction)
            {
                case Faction.Player:
                    values = _influenceForPlayer;
                    stamp = _influenceForPlayerStamp;
                    break;
                case Faction.Enemy:
                    values = _influenceForEnemy;
                    stamp = _influenceForEnemyStamp;
                    break;
                default:
                    return new InfluenceCostInfo { Enabled = false };
            }

            return new InfluenceCostInfo
            {
                Enabled = true,
                Values = values,
                Stamp = stamp,
                StampValue = _influenceStampValue,
                Width = _hex.Width,
                Height = _hex.Height,
                MaxCost = InfluenceCostMax,
                DetourAllowance = Mathf.Max(0, InfluenceDetourAllowance)
            };
        }

        private void UpdateInfluenceMap()
        {
            if (_hex == null) return;
            if (!UseInfluenceCosts) return;
            int size = _hex.Width * _hex.Height;
            if (size <= 0) return;
            if (_influenceForPlayer == null || _influenceForPlayer.Length != size)
            {
                _influenceForPlayer = new int[size];
                _influenceForEnemy = new int[size];
                _influenceForPlayerStamp = new int[size];
                _influenceForEnemyStamp = new int[size];
                _influenceStampValue = 1;
            }

            int stamp = ++_influenceStampValue;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_influenceForPlayerStamp, 0, _influenceForPlayerStamp.Length);
                Array.Clear(_influenceForEnemyStamp, 0, _influenceForEnemyStamp.Length);
                _influenceStampValue = 1;
                stamp = 1;
            }

            int radius = Mathf.Max(0, InfluenceRadiusHex);
            int costPerUnit = Mathf.Max(0, InfluenceCostPerUnit);
            if (radius == 0 || costPerUnit == 0)
            {
                _influenceStampValue = stamp;
                return;
            }

            EnsureInfluenceOffsets();

            int width = _hex.Width;
            int height = _hex.Height;
            foreach (var uc in UnitCombat.All)
            {
                if (uc == null || !uc.isActiveAndEnabled) continue;
                int[] values;
                int[] stamps;
                if (uc.Faction == Faction.Player)
                {
                    values = _influenceForEnemy;
                    stamps = _influenceForEnemyStamp;
                }
                else if (uc.Faction == Faction.Enemy)
                {
                    values = _influenceForPlayer;
                    stamps = _influenceForPlayerStamp;
                }
                else
                {
                    continue;
                }

                var cell = _hex.WorldToGrid(uc.transform.position);
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height) continue;
                var axial = OddRToAxial(cell);
                for (int i = 0; i < _influenceOffsets.Count; i++)
                {
                    var off = _influenceOffsets[i];
                    var targetAxial = new Axial(axial.q + off.q, axial.r + off.r);
                    var targetCell = AxialToOddR(targetAxial);
                    if (targetCell.x < 0 || targetCell.y < 0 || targetCell.x >= width || targetCell.y >= height)
                        continue;
                    int idx = targetCell.y * width + targetCell.x;
                    int add = off.weight * costPerUnit;
                    if (stamps[idx] != stamp)
                    {
                        stamps[idx] = stamp;
                        values[idx] = add;
                    }
                    else
                    {
                        values[idx] += add;
                    }
                    if (InfluenceCostMax > 0 && values[idx] > InfluenceCostMax)
                        values[idx] = InfluenceCostMax;
                }
            }

            _influenceStampValue = stamp;
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

        private float ComputeVectorStepDistance()
        {
            if (_hex == null) return 0f;
            float neighbor = Mathf.Sqrt(3f) * _hex.HexSize;
            float frac = Mathf.Clamp01(VectorStepFraction);
            return neighbor * frac;
        }

        private void EnsureInfluenceOffsets()
        {
            int radius = Mathf.Max(0, InfluenceRadiusHex);
            if (radius == _influenceRadiusCached && _influenceOffsets.Count > 0)
                return;
            _influenceOffsets.Clear();
            _influenceRadiusCached = radius;
            if (radius <= 0) return;

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    int dist = AxialDistance(q, r);
                    int weight = radius - dist + 1;
                    if (weight <= 0) continue;
                    _influenceOffsets.Add(new AxialOffset(q, r, weight));
                }
            }
        }

        private static Axial OddRToAxial(Vector2Int cell)
        {
            int q = cell.x - (cell.y - (cell.y & 1)) / 2;
            int r = cell.y;
            return new Axial(q, r);
        }

        private static Vector2Int AxialToOddR(Axial axial)
        {
            int col = axial.q + (axial.r - (axial.r & 1)) / 2;
            int row = axial.r;
            return new Vector2Int(col, row);
        }

        private static int AxialDistance(int q, int r)
        {
            int s = -q - r;
            return (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("FlowFieldManager");
            go.AddComponent<FlowFieldManager>();
        }

        private bool EnsureTileGraph()
        {
            if (!UseTiledFields) return false;
            if (_hex == null) return false;
            int tileSize = Mathf.Max(1, TileSize);
            if (_tileNeighbors == null ||
                _tileGraphWalkableVersion != _hex.WalkableVersion ||
                _tileGraphTileSize != tileSize ||
                _tileGraphWidth != _hex.Width ||
                _tileGraphHeight != _hex.Height)
            {
                BuildTileGraph(tileSize);
            }
            return _tileNeighbors != null && _tileNeighbors.Length == _tileCount && _tileCount > 0;
        }

        private void BuildTileGraph(int tileSize)
        {
            if (_hex == null) return;
            _tileGraphWalkableVersion = _hex.WalkableVersion;
            _tileGraphTileSize = tileSize;
            _tileGraphWidth = _hex.Width;
            _tileGraphHeight = _hex.Height;
            _tileCols = Mathf.CeilToInt(_hex.Width / (float)tileSize);
            _tileRows = Mathf.CeilToInt(_hex.Height / (float)tileSize);
            _tileCount = Mathf.Max(1, _tileCols * _tileRows);
            _tileNeighbors = new List<int>[_tileCount];
            _tileHasWalkable = new bool[_tileCount];
            for (int i = 0; i < _tileCount; i++)
                _tileNeighbors[i] = new List<int>(6);

            EnsureTileBuffers();

            var walkable = _hex.GetWalkableNative();
            int width = _hex.Width;
            int height = _hex.Height;
            int max = width * height;
            if (!walkable.IsCreated || walkable.Length != max)
            {
                for (int i = 0; i < _tileCount; i++)
                    _tileHasWalkable[i] = true;
                return;
            }

            for (int row = 0; row < height; row++)
            {
                var offs = (row & 1) == 0 ? EvenOffsets : OddOffsets;
                for (int col = 0; col < width; col++)
                {
                    int idx = row * width + col;
                    if (walkable[idx] == 0) continue;
                    int tile = TileIndex(col, row, tileSize);
                    if (tile < 0 || tile >= _tileCount) continue;
                    _tileHasWalkable[tile] = true;

                    for (int i = 0; i < 6; i++)
                    {
                        int nc = col + offs[i].x;
                        int nr = row + offs[i].y;
                        if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                        int nIdx = nr * width + nc;
                        if (walkable[nIdx] == 0) continue;
                        int nTile = TileIndex(nc, nr, tileSize);
                        if (nTile == tile || nTile < 0 || nTile >= _tileCount) continue;
                        AddNeighbor(tile, nTile);
                        AddNeighbor(nTile, tile);
                    }
                }
            }
        }

        private void EnsureTileBuffers()
        {
            if (_tileCount <= 0) return;
            if (_tilePrev == null || _tilePrev.Length != _tileCount)
            {
                _tilePrev = new int[_tileCount];
                _tileVisit = new int[_tileCount];
                _tileQueue = new int[_tileCount];
                _tilePadVisit = new int[_tileCount];
                _tilePadDist = new int[_tileCount];
            }
        }

        private void AddNeighbor(int tile, int neighbor)
        {
            var list = _tileNeighbors[tile];
            if (list == null) return;
            if (!list.Contains(neighbor))
                list.Add(neighbor);
        }

        private bool TryGetTilePath(Vector2Int fromCell, Vector2Int targetCell, List<int> path)
        {
            path.Clear();
            if (_tileNeighbors == null || _tileCount <= 0) return false;
            int tileSize = Mathf.Max(1, TileSize);
            int fromTile = TileIndex(fromCell.x, fromCell.y, tileSize);
            int targetTile = TileIndex(targetCell.x, targetCell.y, tileSize);
            if (fromTile < 0 || targetTile < 0 || fromTile >= _tileCount || targetTile >= _tileCount)
                return false;
            if (_tileHasWalkable != null)
            {
                if (!_tileHasWalkable[fromTile] || !_tileHasWalkable[targetTile])
                    return false;
            }
            if (fromTile == targetTile)
            {
                path.Add(fromTile);
                return true;
            }

            EnsureTileBuffers();
            int stamp = ++_tileVisitStamp;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_tileVisit, 0, _tileVisit.Length);
                _tileVisitStamp = 1;
                stamp = 1;
            }

            int head = 0;
            int tail = 0;
            _tileQueue[tail++] = fromTile;
            _tileVisit[fromTile] = stamp;
            _tilePrev[fromTile] = -1;

            bool found = false;
            while (head < tail)
            {
                int tile = _tileQueue[head++];
                if (tile == targetTile)
                {
                    found = true;
                    break;
                }
                var neighbors = _tileNeighbors[tile];
                if (neighbors == null) continue;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int n = neighbors[i];
                    if (_tileHasWalkable != null && !_tileHasWalkable[n]) continue;
                    if (_tileVisit[n] == stamp) continue;
                    _tileVisit[n] = stamp;
                    _tilePrev[n] = tile;
                    _tileQueue[tail++] = n;
                }
            }

            if (!found) return false;
            int cur = targetTile;
            while (cur != -1)
            {
                path.Add(cur);
                cur = _tilePrev[cur];
            }
            path.Reverse();
            return true;
        }

        private void ExpandTilePath(List<int> path, int padding, List<int> output)
        {
            output.Clear();
            if (path == null || path.Count == 0) return;
            if (padding <= 0 || _tileNeighbors == null)
            {
                output.AddRange(path);
                return;
            }

            EnsureTileBuffers();
            int stamp = ++_tilePadStamp;
            if (stamp == int.MaxValue)
            {
                Array.Clear(_tilePadVisit, 0, _tilePadVisit.Length);
                _tilePadStamp = 1;
                stamp = 1;
            }

            int head = 0;
            int tail = 0;
            for (int i = 0; i < path.Count; i++)
            {
                int tile = path[i];
                if (tile < 0 || tile >= _tileCount) continue;
                if (_tilePadVisit[tile] == stamp) continue;
                _tilePadVisit[tile] = stamp;
                _tilePadDist[tile] = 0;
                _tileQueue[tail++] = tile;
                output.Add(tile);
            }

            while (head < tail)
            {
                int tile = _tileQueue[head++];
                int dist = _tilePadDist[tile];
                if (dist >= padding) continue;
                var neighbors = _tileNeighbors[tile];
                if (neighbors == null) continue;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int n = neighbors[i];
                    if (_tilePadVisit[n] == stamp) continue;
                    _tilePadVisit[n] = stamp;
                    _tilePadDist[n] = dist + 1;
                    _tileQueue[tail++] = n;
                    output.Add(n);
                }
            }
        }

        private int TileIndex(int col, int row, int tileSize)
        {
            int tx = col / tileSize;
            int ty = row / tileSize;
            return ty * _tileCols + tx;
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
                int bestCost = fromCost;
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
                        bestCost = cost;
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
