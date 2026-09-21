/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs
@module: presentation.pathfinding.flowfields
@purpose: Builds and caches shared flow fields for squads and clustered movement toward common goals.
@entry: FlowFieldManager.Update, FFLD-03
@api: shared MonoBehaviour singleton queried by combat/squad systems
@deps: HexPathfindingBootstrap, UnitCombat, crowd/discomfort data, Unity Random
@data: cached flow fields, integration costs, tile activation masks, vector samples
@perf: hotpath, time-sliced BFS and cache management
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual squad movement verification
@config: inspector flow settings, docs/runtime_switches.md
@assets: none directly; consumes live unit and grid state
@notes: tiled fields, crowd costs, and deterministic direction settings are layered optimizations and should be tuned together
*/

using System;
using System.Collections.Generic;
using Game.Presentation.View;
using Game.Domain.Units;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-FLOWFIELDMANAGER]
// Logical block: Scripts/Presentation/Pathfinding/FlowFieldManager.

namespace Game.Presentation.Pathfinding
{
    /// <summary>
    /// Maintains flow fields for shared targets. Uses time-sliced BFS on the hex grid.
    /// </summary>
    public partial class FlowFieldManager : MonoBehaviour
    {
        // [FFLD-01]
        // Flow-field configuration, caches, graph buffers, and shared cost maps.
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

        // [FFLD-02]
        // Lifecycle setup and buffer initialization.
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

        // [FFLD-03]
        // Periodic refresh of crowd maps, influence maps, and cached field requests.
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

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("FlowFieldManager");
            go.AddComponent<FlowFieldManager>();
        }

    }
}

