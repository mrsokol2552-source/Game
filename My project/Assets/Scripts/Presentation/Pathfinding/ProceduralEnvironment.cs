using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Presentation.Pathfinding
{
    public class ProceduralEnvironment : MonoBehaviour
    {
        [Header("General")]
        public bool Enabled = true;
        public bool GenerateOnAwake = true;
        public bool ClearBeforeGenerate = true;
        [Header("Async Generation")]
        public bool UseAsyncGeneration = true;
        public int GroundRowsPerFrame = 16;
        public int PropAttemptsPerFrame = 200;

        [Header("Grid")]
        public GridLayout.CellLayout CellLayout = GridLayout.CellLayout.Hexagon;
        public Vector3 CellSizeOverride = Vector3.zero;
        public string GridObjectName = "EnvironmentGrid (Auto)";
        public string GroundTilemapName = "Ground";
        public string PropTilemapName = "Props";
        public string BlockerTilemapName = "Blockers";
        public string SortingLayerName = "";
        public int GroundSortingOrder = -10;
        public int PropSortingOrder = -5;
        public int BlockerSortingOrder = -4;

        [Header("Ground")]
        public bool FillGround = true;
        public TileBase[] GroundTiles;
        [Header("Ground Variants")]
        public bool UseGroundNoise = true;
        [Range(0.001f, 1f)]
        public float GroundNoiseScale = 0.04f;
        public Vector2 GroundNoiseOffset = Vector2.zero;
        public bool RandomizeGroundNoiseOffset = true;
        [Tooltip("Optional grouping of ground tiles to keep biomes together. 0 = disabled.")]
        public int GroundGroupSize = 0;

        [Header("Props")]
        [Range(0f, 1f)]
        public float PropCoverage = 0.02f;
        public int PropCount = 0;
        public int PropMinHexDistance = 0;
        public TileBase[] PropTiles;
        public bool PropsBlockMovement = false;

        [Header("Blocking Props")]
        [Range(0f, 1f)]
        public float BlockingPropCoverage = 0f;
        public int BlockingPropCount = 0;
        public int BlockingMinHexDistance = 1;
        public TileBase[] BlockingPropTiles;
        public bool BlockingPropsBlockMovement = true;
        [Tooltip("Update walkable grid directly for blocking tiles (avoids physics rebake).")]
        public bool UseDirectWalkableUpdates = true;
        [Tooltip("Create colliders for blocking tilemaps (optional).")]
        public bool BuildBlockingColliders = false;
        [Header("Auto Split")]
        [Tooltip("If enabled, tiles in GroundTiles whose name matches PropNameKeywords are treated as props.")]
        public bool AutoSplitGroundByName = false;
        public string[] PropNameKeywords = new[] { "flora" };
        [Tooltip("If enabled, tiles in PropTiles whose name matches BlockingNameKeywords are treated as blockers.")]
        public bool AutoSplitBlockingByName = false;
        public string[] BlockingNameKeywords = new[] { "tree", "rock", "boulder", "stone", "cliff", "pine" };

        public string ObstacleLayerName = "Obstacles";

        [Header("Random")]
        public bool UseRandomSeed = true;
        public int Seed = 12345;

        private HexPathfindingBootstrap _hex;
        private Grid _grid;
        private Tilemap _ground;
        private Tilemap _props;
        private Tilemap _blockers;
        private Coroutine _generateRoutine;
        private readonly BlockBounds _blockBounds = new BlockBounds();
        private readonly List<Vector2Int> _directBlockedCells = new List<Vector2Int>(1024);
        private readonly HashSet<Vector2Int> _directBlockedSet = new HashSet<Vector2Int>();
        private Vector2 _groundNoiseOffset;

        private void Awake()
        {
            if (!GenerateOnAwake) return;
            Generate();
        }

        [ContextMenu("Generate Environment")]
        public void Generate()
        {
            if (_generateRoutine != null)
            {
                StopCoroutine(_generateRoutine);
                _generateRoutine = null;
            }
            if (!Enabled) return;
            if (UseAsyncGeneration && UnityEngine.Application.isPlaying)
                _generateRoutine = StartCoroutine(GenerateRoutine());
            else
                GenerateImmediate();
        }

        private IEnumerator GenerateRoutine()
        {
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
            {
                _generateRoutine = null;
                yield break;
            }

            int total = width * height;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;
            if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                for (int row = 0; row < height; row += rowsPerFrame)
                {
                    int rowCount = Mathf.Min(rowsPerFrame, height - row);
                    FillGroundTilesBlock(_ground, width, row, rowCount, rng, groundPalette);
                    yield return null;
                }
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
            {
                yield return PlacePropsBatched(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers);
            }

            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
            {
                yield return PlacePropsBatched(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps);
            }

            if (BuildBlockingColliders)
            {
                if (blockingTargetCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockingTargetCount, propTargetCount, bounds);
            _generateRoutine = null;
        }

        private void GenerateImmediate()
        {
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
                return;

            int total = width * height;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;
            if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                FillGroundTiles(_ground, width, height, rng, groundPalette);
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
            {
                PlaceProps(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers);
            }

            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
            {
                PlaceProps(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps);
            }

            if (BuildBlockingColliders)
            {
                if (blockingTargetCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockingTargetCount, propTargetCount, bounds);
        }

        private bool PrepareGeneration(out System.Random rng, out int width, out int height, out List<Vector2Int> occupied,
            out HashSet<Vector2Int> occupiedSet, out BlockBounds bounds)
        {
            rng = UseRandomSeed ? new System.Random() : new System.Random(Seed);
            width = 0;
            height = 0;
            occupied = new List<Vector2Int>(256);
            occupiedSet = new HashSet<Vector2Int>();
            bounds = _blockBounds;

            if (!Enabled) return false;
            EnsureRefs();
            if (_hex == null) return false;

            EnsureGrid();
            EnsureTilemaps();

            if (ClearBeforeGenerate)
            {
                _ground?.ClearAllTiles();
                _props?.ClearAllTiles();
                _blockers?.ClearAllTiles();
                if (UseDirectWalkableUpdates)
                    ClearDirectBlocks();
            }

            width = Mathf.Max(0, _hex.Width);
            height = Mathf.Max(0, _hex.Height);
            if (width == 0 || height == 0) return false;

            if (UseGroundNoise)
            {
                if (RandomizeGroundNoiseOffset)
                {
                    _groundNoiseOffset = new Vector2(
                        (float)rng.NextDouble() * 1000f,
                        (float)rng.NextDouble() * 1000f);
                }
                else
                {
                    _groundNoiseOffset = GroundNoiseOffset;
                }
            }

            bounds.Reset(width, height);
            return true;
        }

        private void EnsureRefs()
        {
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
        }

        private void EnsureGrid()
        {
            if (_grid != null) return;
            var existing = GameObject.Find(GridObjectName);
            if (existing != null)
                _grid = existing.GetComponent<Grid>();
            if (_grid == null)
            {
                var go = existing ?? new GameObject(GridObjectName);
                _grid = go.GetComponent<Grid>();
                if (_grid == null) _grid = go.AddComponent<Grid>();
            }

            _grid.cellLayout = CellLayout;
            _grid.cellSize = ResolveCellSize();
            _grid.transform.position = new Vector3(_hex.Origin.x, _hex.Origin.y, 0f);
        }

        private void EnsureTilemaps()
        {
            _ground = FindOrCreateTilemap(_grid.transform, GroundTilemapName, GroundSortingOrder);
            _props = FindOrCreateTilemap(_grid.transform, PropTilemapName, PropSortingOrder);
            _blockers = FindOrCreateTilemap(_grid.transform, BlockerTilemapName, BlockerSortingOrder);
        }

        private Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            var child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null)
                go.transform.SetParent(parent, false);
            var tm = go.GetComponent<Tilemap>();
            if (tm == null) tm = go.AddComponent<Tilemap>();
            var tr = go.GetComponent<TilemapRenderer>();
            if (tr == null) tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                tr.sortingLayerName = SortingLayerName;
            return tm;
        }

        private void FillGroundTiles(Tilemap map, int width, int height, System.Random rng, TileBase[] palette)
        {
            int size = width * height;
            var tiles = new TileBase[size];
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    tiles[rowBase + col] = PickGroundTile(col, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, 0, 0, width, height, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private void FillGroundTilesBlock(Tilemap map, int width, int startRow, int rowCount, System.Random rng, TileBase[] palette)
        {
            int size = width * rowCount;
            var tiles = new TileBase[size];
            for (int r = 0; r < rowCount; r++)
            {
                int rowBase = r * width;
                int row = startRow + r;
                for (int c = 0; c < width; c++)
                {
                    tiles[rowBase + c] = PickGroundTile(c, row, rng, palette);
                }
            }
            var bounds = new BoundsInt(0, startRow, 0, width, rowCount, 1);
            map.SetTilesBlock(bounds, tiles);
        }

        private void EnsurePropCollider(Tilemap map)
        {
            if (map == null) return;
            var rb = map.GetComponent<Rigidbody2D>();
            if (rb == null) rb = map.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var collider = map.GetComponent<TilemapCollider2D>();
            if (collider == null) collider = map.gameObject.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = map.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        private Vector3 ResolveCellSize()
        {
            if (CellSizeOverride != Vector3.zero)
                return CellSizeOverride;
            if (_hex == null) return Vector3.one;
            float size = Mathf.Max(0.001f, _hex.HexSize);
            if (CellLayout == GridLayout.CellLayout.Hexagon)
                return new Vector3(Mathf.Sqrt(3f) * size, 2f * size, 0f);
            return new Vector3(size, size, 0f);
        }

        private int ResolveObstacleLayer()
        {
            int layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer < 0 && _hex != null)
                layer = FirstLayerFromMask(_hex.ObstacleMask);
            if (layer < 0) layer = 0;
            return layer;
        }

        private void ApplyObstacleLayer(Tilemap map)
        {
            if (map == null) return;
            int layer = ResolveObstacleLayer();
            if (layer >= 0)
                map.gameObject.layer = layer;
            if (_hex != null && layer >= 0)
            {
                var m = _hex.ObstacleMask;
                m.value |= (1 << layer);
                _hex.ObstacleMask = m;
            }
        }

        private static TileBase PickTile(System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            return palette[rng.Next(palette.Length)];
        }

        private TileBase PickGroundTile(int col, int row, System.Random rng, TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            if (!UseGroundNoise || GroundNoiseScale <= 0f)
                return PickTile(rng, palette);

            float scale = Mathf.Max(0.0001f, GroundNoiseScale);
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            const float sqrt3Over2 = 0.8660254f;
            float wx = q + (r * 0.5f);
            float wy = r * sqrt3Over2;
            float nx = (wx + _groundNoiseOffset.x) * scale;
            float ny = (wy + _groundNoiseOffset.y) * scale;
            float v = Mathf.Clamp01(Mathf.PerlinNoise(nx, ny));

            if (GroundGroupSize > 0)
            {
                int groupSize = Mathf.Max(1, GroundGroupSize);
                int groupCount = (palette.Length + groupSize - 1) / groupSize;
                int groupIndex = Mathf.Clamp(Mathf.FloorToInt(v * groupCount), 0, groupCount - 1);
                int groupStart = groupIndex * groupSize;
                int groupEnd = Mathf.Min(groupStart + groupSize, palette.Length);
                return palette[rng.Next(groupStart, groupEnd)];
            }

            int idx = Mathf.Clamp(Mathf.FloorToInt(v * palette.Length), 0, palette.Length - 1);
            return palette[idx];
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var l in SortingLayer.layers)
            {
                if (l.name == name) return true;
            }
            return false;
        }

        private static int FirstLayerFromMask(LayerMask mask)
        {
            int m = mask.value;
            if (m == 0) return -1;
            for (int i = 0; i < 32; i++)
            {
                if (((m >> i) & 1) != 0) return i;
            }
            return -1;
        }

        private void BakeBlockingIfNeeded(int blockingTargetCount, int propTargetCount, BlockBounds bounds)
        {
            if (UseDirectWalkableUpdates) return;
            bool hasBlockingFromProps = PropsBlockMovement && propTargetCount > 0 && _props != null;
            if (hasBlockingFromProps)
            {
                EnsurePropCollider(_props);
                int layer = ResolveObstacleLayer();
                if (layer >= 0)
                    _props.gameObject.layer = layer;
                if (_hex != null)
                {
                    var m = _hex.ObstacleMask;
                    if (layer >= 0) m.value |= (1 << layer);
                    _hex.ObstacleMask = m;
                }
            }

            bool hasBlockingFromBlockers = BlockingPropsBlockMovement && blockingTargetCount > 0 && _blockers != null;
            if (hasBlockingFromBlockers)
            {
                EnsurePropCollider(_blockers);
                int layer = ResolveObstacleLayer();
                if (layer >= 0)
                    _blockers.gameObject.layer = layer;
                if (_hex != null)
                {
                    var m = _hex.ObstacleMask;
                    if (layer >= 0) m.value |= (1 << layer);
                    _hex.ObstacleMask = m;
                }
            }

            if ((hasBlockingFromProps || hasBlockingFromBlockers) && _hex != null)
            {
                if (bounds != null && bounds.HasAny)
                    _hex.BakeFromPhysicsRectCells(bounds.MinCol, bounds.MinRow, bounds.MaxCol, bounds.MaxRow, paddingCells: 1);
                else
                    _hex.BakeFromPhysics();
            }
        }

        private void ResolvePalettes(out TileBase[] groundPalette, out TileBase[] propPalette, out TileBase[] blockingPalette)
        {
            groundPalette = GroundTiles;
            propPalette = PropTiles;
            blockingPalette = BlockingPropTiles;
            if (AutoSplitGroundByName && GroundTiles != null && GroundTiles.Length > 0
                && PropNameKeywords != null && PropNameKeywords.Length > 0)
            {
                var grounds = new List<TileBase>(GroundTiles.Length);
                var splitProps = new List<TileBase>((PropTiles?.Length ?? 0) + GroundTiles.Length);
                if (PropTiles != null && PropTiles.Length > 0)
                    splitProps.AddRange(PropTiles);

                for (int i = 0; i < GroundTiles.Length; i++)
                {
                    var tile = GroundTiles[i];
                    if (tile == null) continue;
                    if (IsNameMatch(tile.name, PropNameKeywords))
                        splitProps.Add(tile);
                    else
                        grounds.Add(tile);
                }

                groundPalette = grounds.ToArray();
                propPalette = splitProps.ToArray();
            }

            if (!AutoSplitBlockingByName) return;
            if (propPalette == null || propPalette.Length == 0) return;
            if (BlockingNameKeywords == null || BlockingNameKeywords.Length == 0) return;

            var props = new List<TileBase>(propPalette.Length);
            var blockers = new List<TileBase>(propPalette.Length / 2);
            for (int i = 0; i < propPalette.Length; i++)
            {
                var tile = propPalette[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, BlockingNameKeywords))
                    blockers.Add(tile);
                else
                    props.Add(tile);
            }

            if (BlockingPropTiles != null && BlockingPropTiles.Length > 0)
                blockers.AddRange(BlockingPropTiles);

            propPalette = props.ToArray();
            blockingPalette = blockers.ToArray();
        }

        private static bool IsNameMatch(string tileName, string[] keywords)
        {
            if (string.IsNullOrEmpty(tileName) || keywords == null || keywords.Length == 0) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                var kw = keywords[i];
                if (string.IsNullOrEmpty(kw)) continue;
                if (tileName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void PlaceProps(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) return;
            int attempts = 0;
            int placed = 0;
            while (placed < targetCount && attempts < targetCount * 50)
            {
                attempts++;
                if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                    continue;

                occupied.Add(cell);
                occupiedSet.Add(cell);
                map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
                placed++;
            }
        }

        private IEnumerator PlacePropsBatched(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) yield break;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 50;
            int attemptsPerFrame = Mathf.Max(1, PropAttemptsPerFrame);
            while (placed < targetCount && attempts < maxAttempts)
            {
                int frameAttempts = attemptsPerFrame;
                for (int i = 0; i < frameAttempts && placed < targetCount && attempts < maxAttempts; i++)
                {
                    attempts++;
                    if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                        continue;

                    occupied.Add(cell);
                    occupiedSet.Add(cell);
                    map.SetTile(new Vector3Int(cell.x, cell.y, 0), PickTile(rng, palette));
                    bounds?.Include(cell.x, cell.y);
                    if (markBlocked)
                        MarkBlockedCell(cell);
                    placed++;
                }
                yield return null;
            }
        }

        private static bool TryPickCell(int width, int height, System.Random rng, int minHexDistance,
            List<Vector2Int> occupied, HashSet<Vector2Int> occupiedSet, out Vector2Int cell)
        {
            int col = rng.Next(0, width);
            int row = rng.Next(0, height);
            cell = new Vector2Int(col, row);
            if (occupiedSet.Contains(cell)) return false;
            if (minHexDistance > 0)
            {
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (HexDistance(cell, occupied[i]) < minHexDistance)
                        return false;
                }
            }
            return true;
        }

        private void MarkBlockedCell(Vector2Int cell)
        {
            if (_hex == null) return;
            if (_directBlockedSet.Contains(cell)) return;
            _directBlockedSet.Add(cell);
            _directBlockedCells.Add(cell);
            _hex.SetWalkable(cell.x, cell.y, false);
        }

        private void ClearDirectBlocks()
        {
            if (_hex == null || _directBlockedCells.Count == 0) return;
            for (int i = 0; i < _directBlockedCells.Count; i++)
            {
                var cell = _directBlockedCells[i];
                _hex.SetWalkable(cell.x, cell.y, true);
            }
            _directBlockedCells.Clear();
            _directBlockedSet.Clear();
        }

        private sealed class BlockBounds
        {
            public int MinCol;
            public int MinRow;
            public int MaxCol;
            public int MaxRow;

            public bool HasAny => MaxCol >= 0 && MaxRow >= 0;

            public void Reset(int width, int height)
            {
                MinCol = width;
                MinRow = height;
                MaxCol = -1;
                MaxRow = -1;
            }

            public void Include(int col, int row)
            {
                if (col < MinCol) MinCol = col;
                if (row < MinRow) MinRow = row;
                if (col > MaxCol) MaxCol = col;
                if (row > MaxRow) MaxRow = row;
            }
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            Axial aa = OddRToAxial(a.x, a.y);
            Axial bb = OddRToAxial(b.x, b.y);
            int dx = aa.q - bb.q; if (dx < 0) dx = -dx;
            int dz = aa.r - bb.r; if (dz < 0) dz = -dz;
            int dy = -(aa.q + aa.r) - (-(bb.q + bb.r));
            if (dy < 0) dy = -dy;
            return (dx + dy + dz) / 2;
        }

        private struct Axial { public int q; public int r; }
        private static Axial OddRToAxial(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new Axial { q = q, r = r };
        }
    }
}
