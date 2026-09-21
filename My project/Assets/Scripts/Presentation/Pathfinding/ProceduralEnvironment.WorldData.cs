/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.WorldData.cs
@module: presentation.pathfinding.worldgen.world_data
@purpose: Extracts typed chunk/background biome snapshots from ProceduralEnvironment so renderers and future shader backends can consume world data without reading raw mask arrays directly.
@entry: PENV-22, PENV-23, ProceduralEnvironment.ExtractStreamChunkWorldData
@api: partial class implementation for ProceduralEnvironment
@deps: background biome masks, streaming chunk state, placement queries
@data: extracted background biome cells, land-distance values, per-chunk world-data snapshots
@perf: medium; extraction is chunk-scoped and reused to avoid repeated raw-mask branching during rendering
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and streaming smoke tests
@config: background mask generation and streaming chunk configuration in ProceduralEnvironment
@assets: none directly; this is a data seam between generation state and render backends
@notes: keep this layer renderer-agnostic so future shader/instancing work can consume chunk data without re-reading ProceduralEnvironment internals
*/

using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-WORLDDATA]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.WorldData.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private readonly struct BackgroundBiomeData
        {
            public readonly int Col;
            public readonly int Row;
            public readonly bool InBounds;
            public readonly bool IsWater;
            public readonly bool IsWaterHole;
            public readonly bool IsWaterInterior;
            public readonly bool IsRock;
            public readonly int LandDistance;

            public bool IsLand => !IsWater && !IsRock;

            public BackgroundBiomeData(
                int col,
                int row,
                bool inBounds,
                bool isWater,
                bool isWaterHole,
                bool isWaterInterior,
                bool isRock,
                int landDistance)
            {
                Col = col;
                Row = row;
                InBounds = inBounds;
                IsWater = isWater;
                IsWaterHole = isWaterHole;
                IsWaterInterior = isWaterInterior;
                IsRock = isRock;
                LandDistance = landDistance;
            }
        }

        private readonly struct TilemapChunkCellData
        {
            public readonly int Col;
            public readonly int Row;
            public readonly TileBase Tile;
            public readonly Matrix4x4 Transform;
            public readonly Vector3 WorldMin;
            public readonly Vector3 WorldMax;
            public readonly Color32 Tint;

            public bool HasTile => Tile != null;

            public TilemapChunkCellData(int col, int row, TileBase tile, Matrix4x4 transform)
                : this(
                    col,
                    row,
                    tile,
                    transform,
                    Vector3.zero,
                    Vector3.zero,
                    new Color32(255, 255, 255, 255))
            {
            }

            public TilemapChunkCellData(
                int col,
                int row,
                TileBase tile,
                Matrix4x4 transform,
                Vector3 worldMin,
                Vector3 worldMax,
                Color32 tint)
            {
                Col = col;
                Row = row;
                Tile = tile;
                Transform = transform;
                WorldMin = worldMin;
                WorldMax = worldMax;
                Tint = tint;
            }
        }

        private readonly struct TilemapLayerSnapshotMetadata
        {
            public readonly string TilemapName;
            public readonly bool HasGridConfig;
            public readonly Vector3 GridPosition;
            public readonly Quaternion GridRotation;
            public readonly Vector3 GridScale;
            public readonly GridLayout.CellLayout GridCellLayout;
            public readonly Vector3 GridCellSize;
            public readonly Vector3 GridCellGap;
            public readonly GridLayout.CellSwizzle GridCellSwizzle;
            public readonly bool HasTilemapTransform;
            public readonly Vector3 TilemapLocalPosition;
            public readonly Quaternion TilemapLocalRotation;
            public readonly Vector3 TilemapLocalScale;
            public readonly Vector3 TilemapWorldPosition;
            public readonly Quaternion TilemapWorldRotation;
            public readonly Vector3 TilemapWorldScale;
            public readonly Tilemap.Orientation TilemapOrientation;
            public readonly Matrix4x4 TilemapOrientationMatrix;
            public readonly Vector3 TilemapAnchor;
            public readonly Color TilemapColor;
            public readonly bool HasRendererConfig;
            public readonly int SortingLayerId;
            public readonly int SortingOrder;
            public readonly Material SharedMaterial;

            public TilemapLayerSnapshotMetadata(Tilemap tilemap)
            {
                TilemapName = tilemap != null ? tilemap.name : string.Empty;
                var grid = tilemap != null ? tilemap.layoutGrid : null;
                HasGridConfig = grid != null;
                if (grid != null)
                {
                    GridPosition = grid.transform.position;
                    GridRotation = grid.transform.rotation;
                    GridScale = grid.transform.lossyScale;
                    GridCellLayout = grid.cellLayout;
                    GridCellSize = grid.cellSize;
                    GridCellGap = grid.cellGap;
                    GridCellSwizzle = grid.cellSwizzle;
                }
                else
                {
                    GridPosition = Vector3.zero;
                    GridRotation = Quaternion.identity;
                    GridScale = Vector3.one;
                    GridCellLayout = GridLayout.CellLayout.Rectangle;
                    GridCellSize = Vector3.one;
                    GridCellGap = Vector3.zero;
                    GridCellSwizzle = GridLayout.CellSwizzle.XYZ;
                }

                HasTilemapTransform = tilemap != null;
                if (tilemap != null)
                {
                    TilemapLocalPosition = tilemap.transform.localPosition;
                    TilemapLocalRotation = tilemap.transform.localRotation;
                    TilemapLocalScale = tilemap.transform.localScale;
                    TilemapWorldPosition = tilemap.transform.position;
                    TilemapWorldRotation = tilemap.transform.rotation;
                    TilemapWorldScale = tilemap.transform.lossyScale;
                    TilemapOrientation = tilemap.orientation;
                    TilemapOrientationMatrix = tilemap.orientationMatrix;
                    TilemapAnchor = tilemap.tileAnchor;
                    TilemapColor = tilemap.color;
                    var renderer = tilemap.GetComponent<TilemapRenderer>();
                    HasRendererConfig = renderer != null;
                    SortingLayerId = renderer != null ? renderer.sortingLayerID : 0;
                    SortingOrder = renderer != null ? renderer.sortingOrder : 0;
                    SharedMaterial = renderer != null ? renderer.sharedMaterial : null;
                }
                else
                {
                    TilemapLocalPosition = Vector3.zero;
                    TilemapLocalRotation = Quaternion.identity;
                    TilemapLocalScale = Vector3.one;
                    TilemapWorldPosition = Vector3.zero;
                    TilemapWorldRotation = Quaternion.identity;
                    TilemapWorldScale = Vector3.one;
                    TilemapOrientation = Tilemap.Orientation.XY;
                    TilemapOrientationMatrix = Matrix4x4.identity;
                    TilemapAnchor = Vector3.zero;
                    TilemapColor = Color.white;
                    HasRendererConfig = false;
                    SortingLayerId = 0;
                    SortingOrder = 0;
                    SharedMaterial = null;
                }
            }
        }

        private sealed class TilemapChunkSnapshot
        {
            private readonly TilemapChunkCellData[] _cells;

            public readonly string TilemapName;
            public readonly BoundsInt CellBounds;
            public readonly int NonEmptyCount;
            public readonly bool HasGridConfig;
            public readonly Vector3 GridPosition;
            public readonly Quaternion GridRotation;
            public readonly Vector3 GridScale;
            public readonly GridLayout.CellLayout GridCellLayout;
            public readonly Vector3 GridCellSize;
            public readonly Vector3 GridCellGap;
            public readonly GridLayout.CellSwizzle GridCellSwizzle;
            public readonly bool HasTilemapTransform;
            public readonly Vector3 TilemapLocalPosition;
            public readonly Quaternion TilemapLocalRotation;
            public readonly Vector3 TilemapLocalScale;
            public readonly Vector3 TilemapWorldPosition;
            public readonly Quaternion TilemapWorldRotation;
            public readonly Vector3 TilemapWorldScale;
            public readonly Tilemap.Orientation TilemapOrientation;
            public readonly Matrix4x4 TilemapOrientationMatrix;
            public readonly Vector3 TilemapAnchor;
            public readonly Color TilemapColor;
            public readonly bool HasRendererConfig;
            public readonly int SortingLayerId;
            public readonly int SortingOrder;
            public readonly Material SharedMaterial;

            public bool HasTiles => NonEmptyCount > 0;

            public TilemapChunkSnapshot(
                TilemapLayerSnapshotMetadata metadata,
                BoundsInt cellBounds,
                TilemapChunkCellData[] cells,
                int nonEmptyCount)
            {
                TilemapName = metadata.TilemapName;
                CellBounds = cellBounds;
                _cells = cells;
                NonEmptyCount = nonEmptyCount;
                HasGridConfig = metadata.HasGridConfig;
                GridPosition = metadata.GridPosition;
                GridRotation = metadata.GridRotation;
                GridScale = metadata.GridScale;
                GridCellLayout = metadata.GridCellLayout;
                GridCellSize = metadata.GridCellSize;
                GridCellGap = metadata.GridCellGap;
                GridCellSwizzle = metadata.GridCellSwizzle;
                HasTilemapTransform = metadata.HasTilemapTransform;
                TilemapLocalPosition = metadata.TilemapLocalPosition;
                TilemapLocalRotation = metadata.TilemapLocalRotation;
                TilemapLocalScale = metadata.TilemapLocalScale;
                TilemapWorldPosition = metadata.TilemapWorldPosition;
                TilemapWorldRotation = metadata.TilemapWorldRotation;
                TilemapWorldScale = metadata.TilemapWorldScale;
                TilemapOrientation = metadata.TilemapOrientation;
                TilemapOrientationMatrix = metadata.TilemapOrientationMatrix;
                TilemapAnchor = metadata.TilemapAnchor;
                TilemapColor = metadata.TilemapColor;
                HasRendererConfig = metadata.HasRendererConfig;
                SortingLayerId = metadata.SortingLayerId;
                SortingOrder = metadata.SortingOrder;
                SharedMaterial = metadata.SharedMaterial;
            }

            public bool TryGetCell(int col, int row, out TilemapChunkCellData cell)
            {
                cell = default;
                if (_cells == null || _cells.Length == 0)
                    return false;

                int localX = col - CellBounds.xMin;
                int localY = row - CellBounds.yMin;
                int width = CellBounds.size.x;
                int height = CellBounds.size.y;
                if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                    return false;

                int idx = (localY * width) + localX;
                if (idx < 0 || idx >= _cells.Length)
                    return false;

                cell = _cells[idx];
                return true;
            }
        }

        private sealed class StreamChunkWorldData
        {
            private readonly BackgroundBiomeData[] _backgroundCells;

            public readonly Vector2Int Coord;
            public readonly BoundsInt HexBounds;
            public readonly BoundsInt BackgroundBounds;

            public bool HasBackgroundCells => _backgroundCells != null && _backgroundCells.Length > 0;

            public StreamChunkWorldData(
                Vector2Int coord,
                BoundsInt hexBounds,
                BoundsInt backgroundBounds,
                BackgroundBiomeData[] backgroundCells)
            {
                Coord = coord;
                HexBounds = hexBounds;
                BackgroundBounds = backgroundBounds;
                _backgroundCells = backgroundCells;
            }

            public bool TryGetBackgroundCell(int col, int row, out BackgroundBiomeData data)
            {
                data = default;
                if (_backgroundCells == null || _backgroundCells.Length == 0)
                    return false;

                int localX = col - BackgroundBounds.xMin;
                int localY = row - BackgroundBounds.yMin;
                int width = BackgroundBounds.size.x;
                int height = BackgroundBounds.size.y;
                if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                    return false;

                int idx = (localY * width) + localX;
                if (idx < 0 || idx >= _backgroundCells.Length)
                    return false;

                data = _backgroundCells[idx];
                return true;
            }
        }

        // [PENV-22]
        // Chunk-scoped world-data extraction used as a clean seam between generation data and render/writeback code.
        private StreamChunkWorldData ExtractStreamChunkWorldData(StreamChunkState state)
        {
            BackgroundBiomeData[] backgroundCells = null;
            var bgBounds = state.BackgroundBounds;
            if (HasBackgroundBiomeMaskData() && bgBounds.size.x > 0 && bgBounds.size.y > 0)
                backgroundCells = ExtractBackgroundChunkCells(bgBounds);

            return new StreamChunkWorldData(
                state.Coord,
                state.HexBounds,
                state.BackgroundBounds,
                backgroundCells);
        }

        private BackgroundBiomeData[] ExtractBackgroundChunkCells(BoundsInt bounds)
        {
            int width = bounds.size.x;
            int height = bounds.size.y;
            if (width <= 0 || height <= 0)
                return Array.Empty<BackgroundBiomeData>();

            var cells = new BackgroundBiomeData[width * height];
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                int row = bounds.yMin + y;
                for (int x = 0; x < width; x++)
                {
                    int col = bounds.xMin + x;
                    cells[idx++] = ExtractBackgroundMaskData(col, row);
                }
            }

            return cells;
        }

        // [PENV-23]
        // Centralized background-mask extraction so gameplay and rendering code can read typed biome data instead of raw arrays.
        private bool HasBackgroundBiomeMaskData()
        {
            return _backgroundWaterMask != null && _backgroundMaskWidth > 0 && _backgroundMaskHeight > 0;
        }

        private BackgroundBiomeData ExtractBackgroundMaskData(int col, int row)
        {
            if (!HasBackgroundBiomeMaskData() || col < 0 || row < 0 || col >= _backgroundMaskWidth || row >= _backgroundMaskHeight)
            {
                return new BackgroundBiomeData(col, row, false, false, false, false, false, -1);
            }

            int idx = (row * _backgroundMaskWidth) + col;
            bool isWater = _backgroundWaterMask[idx];
            bool isWaterHole = !isWater && IsMaskHole(_backgroundWaterMask, _backgroundMaskWidth, _backgroundMaskHeight, col, row);
            if (isWaterHole)
                isWater = true;

            bool isRock = !isWater
                && _backgroundRockMask != null
                && _backgroundRockMask.Length == _backgroundWaterMask.Length
                && _backgroundRockMask[idx];
            bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(_backgroundWaterMask, _backgroundMaskWidth, _backgroundMaskHeight, col, row));

            int landDistance = -1;
            if (_backgroundLandDistance != null && _backgroundLandDistance.Length == _backgroundMaskWidth * _backgroundMaskHeight)
                landDistance = _backgroundLandDistance[idx];

            return new BackgroundBiomeData(col, row, true, isWater, isWaterHole, isWaterInterior, isRock, landDistance);
        }

        private BackgroundBiomeData ResolveBackgroundBiomeData(StreamChunkWorldData chunkData, int col, int row)
        {
            if (chunkData != null && chunkData.TryGetBackgroundCell(col, row, out var data))
                return data;

            return ExtractBackgroundMaskData(col, row);
        }

        private bool TryGetBackgroundMaskData(int col, int row, out BackgroundBiomeData data)
        {
            data = ExtractBackgroundMaskData(col, row);
            return data.InBounds;
        }

        private bool TryGetBackgroundCellData(Vector2Int cell, out BackgroundBiomeData data)
        {
            data = default;
            if (!HasBackgroundBiomeMaskData())
                return false;
            if (!TryGetBackgroundCellIndex(cell, out int idx))
                return false;

            int col = idx % _backgroundMaskWidth;
            int row = idx / _backgroundMaskWidth;
            data = ExtractBackgroundMaskData(col, row);
            return data.InBounds;
        }

        private static BoundsInt ClampBoundsInt(BoundsInt bounds, BoundsInt clamp)
        {
            int minX = Mathf.Max(bounds.xMin, clamp.xMin);
            int minY = Mathf.Max(bounds.yMin, clamp.yMin);
            int maxX = Mathf.Min(bounds.xMax, clamp.xMax);
            int maxY = Mathf.Min(bounds.yMax, clamp.yMax);
            if (maxX <= minX || maxY <= minY)
                return default;

            return new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
        }

        // Legacy live-tilemap fallback for scenes that were not generated through payload caches.
        private bool TryGetLegacyTilemapChunkCellBounds(Tilemap map, Bounds worldBounds, out BoundsInt bounds)
        {
            bounds = default;
            if (map == null)
                return false;

            var grid = map.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            float epsilon = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 0.001f);
            Vector3Int minCell = map.WorldToCell(new Vector3(worldBounds.min.x + epsilon, worldBounds.min.y + epsilon, 0f));
            Vector3Int maxCell = map.WorldToCell(new Vector3(worldBounds.max.x - epsilon, worldBounds.max.y - epsilon, 0f));

            int minX = Mathf.Min(minCell.x, maxCell.x);
            int minY = Mathf.Min(minCell.y, maxCell.y);
            int maxX = Mathf.Max(minCell.x, maxCell.x);
            int maxY = Mathf.Max(minCell.y, maxCell.y);
            var requested = new BoundsInt(minX, minY, 0, (maxX - minX) + 1, (maxY - minY) + 1, 1);
            bounds = ClampBoundsInt(requested, map.cellBounds);
            return bounds.size.x > 0 && bounds.size.y > 0;
        }

        // Legacy live-tilemap fallback for scenes that were not generated through payload caches.
        private bool TryGetLegacyTilemapUsedWorldBounds(Tilemap map, out Bounds bounds)
        {
            bounds = default;
            if (map == null)
                return false;

            BoundsInt cellBounds = map.cellBounds;
            if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
                return false;

            var tiles = map.GetTilesBlock(cellBounds);
            if (tiles == null || tiles.Length == 0)
                return false;

            int width = cellBounds.size.x;
            int height = cellBounds.size.y;
            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;

            for (int y = 0; y < height; y++)
            {
                int row = cellBounds.yMin + y;
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    var tile = tiles[rowBase + x];
                    if (tile == null)
                        continue;

                    int col = cellBounds.xMin + x;
                    if (!hasAny)
                    {
                        minCol = maxCol = col;
                        minRow = maxRow = row;
                        hasAny = true;
                        continue;
                    }

                    if (col < minCol) minCol = col;
                    if (row < minRow) minRow = row;
                    if (col > maxCol) maxCol = col;
                    if (row > maxRow) maxRow = row;
                }
            }

            if (!hasAny)
                return false;

            Vector3 min = map.CellToWorld(new Vector3Int(minCol, minRow, 0));
            Vector3 max = map.CellToWorld(new Vector3Int(maxCol + 1, maxRow + 1, 0));
            Vector3 size = max - min;
            if (size.x <= 0f || size.y <= 0f)
                return false;

            bounds = new Bounds(min + (size * 0.5f), size);
            return true;
        }

        private static TilemapChunkCellData BuildTilemapChunkCellData(
            Tilemap map,
            int col,
            int row,
            TileBase tile,
            Matrix4x4 transform)
        {
            Vector3 worldMin = Vector3.zero;
            Vector3 worldMax = Vector3.zero;
            Color32 tint = new Color32(255, 255, 255, 255);
            if (map != null)
            {
                var cellPosition = new Vector3Int(col, row, 0);
                Vector3 worldA = map.CellToWorld(cellPosition);
                Vector3 worldB = map.CellToWorld(new Vector3Int(col + 1, row + 1, 0));
                worldMin = new Vector3(Mathf.Min(worldA.x, worldB.x), Mathf.Min(worldA.y, worldB.y), Mathf.Min(worldA.z, worldB.z));
                worldMax = new Vector3(Mathf.Max(worldA.x, worldB.x), Mathf.Max(worldA.y, worldB.y), Mathf.Max(worldA.z, worldB.z));
                if (Mathf.Abs(worldMax.x - worldMin.x) <= 0.0001f || Mathf.Abs(worldMax.y - worldMin.y) <= 0.0001f)
                {
                    Vector3 cellSize = map.layoutGrid != null ? map.layoutGrid.cellSize : Vector3.one;
                    worldMax = new Vector3(
                        worldMin.x + Mathf.Max(0.0001f, Mathf.Abs(cellSize.x)),
                        worldMin.y + Mathf.Max(0.0001f, Mathf.Abs(cellSize.y)),
                        worldMax.z);
                }

                if (tile != null)
                    tint = ResolveFarViewTilemapTint(map, cellPosition, tile);
            }

            return new TilemapChunkCellData(col, row, tile, transform, worldMin, worldMax, tint);
        }

        private static TilemapChunkSnapshot BuildTilemapChunkSnapshot(
            Tilemap map,
            BoundsInt bounds,
            TilemapChunkCellData[] cells,
            int nonEmptyCount)
        {
            if (map == null || cells == null || nonEmptyCount <= 0)
                return null;

            var metadata = new TilemapLayerSnapshotMetadata(map);
            return new TilemapChunkSnapshot(metadata, bounds, cells, nonEmptyCount);
        }

        // Legacy live-tilemap fallback for scenes that were not generated through payload caches.
        private bool TryExtractLegacyTilemapChunkSnapshot(Tilemap map, Bounds worldBounds, out TilemapChunkSnapshot snapshot)
        {
            snapshot = null;
            if (map == null || !TryGetLegacyTilemapChunkCellBounds(map, worldBounds, out var bounds))
                return false;

            var tiles = map.GetTilesBlock(bounds);
            if (tiles == null || tiles.Length == 0)
                return false;

            var cells = new TilemapChunkCellData[tiles.Length];
            int nonEmptyCount = 0;
            int width = bounds.size.x;
            int height = bounds.size.y;
            for (int y = 0; y < height; y++)
            {
                int row = bounds.yMin + y;
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int col = bounds.xMin + x;
                    int idx = rowBase + x;
                    var tile = tiles[idx];
                    Matrix4x4 transform = Matrix4x4.identity;
                    if (tile != null)
                    {
                        nonEmptyCount++;
                        transform = map.GetTransformMatrix(new Vector3Int(col, row, 0));
                        if (transform == default || transform == Matrix4x4.identity)
                            transform = GetTileTransform(tile);
                    }

                    cells[idx] = BuildTilemapChunkCellData(map, col, row, tile, transform);
                }
            }

            if (nonEmptyCount <= 0)
                return false;

            snapshot = BuildTilemapChunkSnapshot(map, bounds, cells, nonEmptyCount);
            return true;
        }
    }
}
