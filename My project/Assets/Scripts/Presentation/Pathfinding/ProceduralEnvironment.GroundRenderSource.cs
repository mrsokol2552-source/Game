/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderSource.cs
@module: presentation.pathfinding.worldgen.ground_render_source
@purpose: Caches full and streaming ground render payloads and exposes chunk-level extraction helpers so far-view and future world render backends can read terrain decisions without direct tilemap reads.
@entry: PENV-33, ProceduralEnvironment.CacheGroundRenderPayload, ProceduralEnvironment.CaptureStreamGroundRenderPayload, ProceduralEnvironment.TryExtractGroundRenderChunkSnapshots
@api: partial class implementation for ProceduralEnvironment
@deps: ground render payload, far-view source, tilemap chunk snapshot types, tilemap layout helpers
@data: cached ground decision arrays, streaming ground cells, world bounds, payload versioning, and chunk snapshot extraction
@perf: medium; caches full terrain decisions once per generation and reuses them across far-view chunk extraction
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and far-view/runtime generation smoke tests
@config: far-view ground/transition inclusion and terrain ruleset generation settings in ProceduralEnvironment
@assets: none directly; acts as a runtime data seam between terrain generation and render backends
@notes: keep this source renderer-agnostic so tilemaps, far-view, and future shader paths can share one ground payload; live tilemap reads belong only to explicit LegacyTilemap fallback helpers
*/

using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-GROUNDRENDERSOURCE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.GroundRenderSource.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private bool HasGroundRenderPayloadStorage =>
            _cachedGroundRenderPayload != null
            && _cachedGroundRenderWidth > 0
            && _cachedGroundRenderHeight > 0;

        private void BumpCachedGroundFarViewPayloadVersion()
        {
            unchecked
            {
                _cachedGroundFarViewPayloadVersion++;
                if (_cachedGroundFarViewPayloadVersion == 0)
                    _cachedGroundFarViewPayloadVersion = 1;
            }
        }

        private void ClearGroundRenderPayloadCache()
        {
            BumpCachedGroundFarViewPayloadVersion();
            _cachedGroundRenderPayload = null;
            _cachedGroundRenderBounds = default;
            _hasCachedGroundRenderPayload = false;
            _cachedGroundRenderWidth = 0;
            _cachedGroundRenderHeight = 0;
            _cachedGroundRenderUsesTransition = false;
            ReleaseGroundPayloadChunkRenderer();
        }

        private bool TryPrepareGroundRenderPayloadStorage(
            int width,
            int height,
            bool useTransition)
        {
            BumpCachedGroundFarViewPayloadVersion();
            if (_ground == null
                || width <= 0
                || height <= 0)
            {
                ClearGroundRenderPayloadCache();
                return false;
            }

            Vector3 min = _ground.CellToWorld(Vector3Int.zero);
            Vector3 max = _ground.CellToWorld(new Vector3Int(width, height, 0));
            Vector3 size = max - min;
            if (size.x <= 0f || size.y <= 0f)
            {
                ClearGroundRenderPayloadCache();
                return false;
            }

            int cellCount = width * height;
            if (_cachedGroundRenderPayload == null || _cachedGroundRenderPayload.Length != cellCount)
                _cachedGroundRenderPayload = new GroundRenderCellDecision[cellCount];

            _cachedGroundRenderBounds = new Bounds(min + (size * 0.5f), size);
            _hasCachedGroundRenderPayload = false;
            _cachedGroundRenderWidth = width;
            _cachedGroundRenderHeight = height;
            _cachedGroundRenderUsesTransition = useTransition;
            ReleaseGroundPayloadChunkRenderer();
            return true;
        }

        private bool TryStageGroundRenderDecisionBlockInPayloadStorage(
            int startRow,
            int rowCount,
            GroundRenderCellDecision[] decisions)
        {
            if (!HasGroundRenderPayloadStorage
                || decisions == null
                || startRow < 0
                || rowCount <= 0
                || startRow + rowCount > _cachedGroundRenderHeight
                || decisions.Length != _cachedGroundRenderWidth * rowCount)
            {
                return false;
            }

            System.Array.Copy(
                decisions,
                0,
                _cachedGroundRenderPayload,
                startRow * _cachedGroundRenderWidth,
                decisions.Length);
            return true;
        }

        private void CommitGroundRenderPayloadStorage()
        {
            _hasCachedGroundRenderPayload = HasGroundRenderPayloadStorage;
            RefreshGroundPayloadChunkRenderer();
        }

        private void CacheGroundRenderPayload(
            int width,
            int height,
            bool useTransition,
            GroundRenderCellDecision[] decisions)
        {
            if (decisions == null
                || decisions.Length != width * height
                || !TryPrepareGroundRenderPayloadStorage(width, height, useTransition)
                || !TryStageGroundRenderDecisionBlockInPayloadStorage(0, height, decisions))
            {
                ClearGroundRenderPayloadCache();
                return;
            }

            CommitGroundRenderPayloadStorage();
        }

        private bool TryGetCachedGroundRenderBounds(out Bounds bounds)
        {
            bounds = default;
            if (!_hasCachedGroundRenderPayload
                || _cachedGroundRenderPayload == null
                || _cachedGroundRenderWidth <= 0
                || _cachedGroundRenderHeight <= 0)
            {
                return false;
            }

            bounds = _cachedGroundRenderBounds;
            return bounds.size.x > 0f && bounds.size.y > 0f;
        }

        private bool HasStreamGroundRenderPayloadSource(bool includeGround, bool includeTransitions)
        {
            if (!_streamingActive || !includeGround)
                return false;

            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (state.Generated && state.GroundCells != null && state.GroundCellCount > 0)
                    return true;
            }

            return false;
        }

        private bool TryGetGroundRenderPayloadBounds(bool includeGround, bool includeTransitions, out Bounds bounds)
        {
            if (TryGetCachedGroundRenderBounds(out bounds))
                return true;

            return TryGetStreamGroundRenderBounds(includeGround, includeTransitions, out bounds);
        }

        private bool TryGetStreamGroundRenderBounds(bool includeGround, bool includeTransitions, out Bounds bounds)
        {
            bounds = default;
            if (!_streamingActive || !includeGround)
                return false;

            bool hasAny = false;
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.GroundCells == null || state.GroundCellCount <= 0)
                    continue;

                for (int i = 0; i < state.GroundCells.Length; i++)
                {
                    var cell = state.GroundCells[i];
                    if (!cell.HasTile)
                        continue;

                    EncapsulateCellWorldBounds(cell, ref bounds, ref hasAny);
                }
            }

            return hasAny;
        }

        private static void EncapsulateCellWorldBounds(TilemapChunkCellData cell, ref Bounds bounds, ref bool hasAny)
        {
            Vector3 size = cell.WorldMax - cell.WorldMin;
            if (size.x <= 0f || size.y <= 0f)
                return;

            var cellBounds = new Bounds(cell.WorldMin + (size * 0.5f), size);
            if (!hasAny)
            {
                bounds = cellBounds;
                hasAny = true;
                return;
            }

            bounds.Encapsulate(cellBounds);
        }

        private void CaptureStreamGroundRenderPayload(ref StreamChunkState state, BoundsInt bounds, TileBase[] tiles)
        {
            state.GroundPayloadBounds = default;
            state.GroundCells = null;
            state.GroundCellCount = 0;

            if (_ground == null
                || tiles == null
                || bounds.size.x <= 0
                || bounds.size.y <= 0
                || tiles.Length != bounds.size.x * bounds.size.y)
            {
                return;
            }

            var cells = new TilemapChunkCellData[tiles.Length];
            int nonEmptyCount = 0;
            int idx = 0;
            for (int y = 0; y < bounds.size.y; y++)
            {
                int row = bounds.yMin + y;
                for (int x = 0; x < bounds.size.x; x++)
                {
                    int col = bounds.xMin + x;
                    var tile = tiles[idx];
                    Matrix4x4 transform = tile != null ? GetTileTransform(tile) : Matrix4x4.identity;
                    if (tile != null)
                        nonEmptyCount++;

                    cells[idx] = BuildTilemapChunkCellData(_ground, col, row, tile, transform);
                    idx++;
                }
            }

            if (nonEmptyCount <= 0)
                return;

            state.GroundPayloadBounds = bounds;
            state.GroundCells = cells;
            state.GroundCellCount = nonEmptyCount;
        }

        private bool TryGetGroundRenderChunkCellBounds(Bounds worldBounds, out BoundsInt bounds)
        {
            bounds = default;
            var referenceMap = _ground != null ? _ground : _transitions;
            if (referenceMap == null || !_hasCachedGroundRenderPayload)
                return false;

            var grid = referenceMap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            float epsilon = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 0.001f);
            Vector3Int minCell = referenceMap.WorldToCell(new Vector3(worldBounds.min.x + epsilon, worldBounds.min.y + epsilon, 0f));
            Vector3Int maxCell = referenceMap.WorldToCell(new Vector3(worldBounds.max.x - epsilon, worldBounds.max.y - epsilon, 0f));

            int minX = Mathf.Min(minCell.x, maxCell.x);
            int minY = Mathf.Min(minCell.y, maxCell.y);
            int maxX = Mathf.Max(minCell.x, maxCell.x);
            int maxY = Mathf.Max(minCell.y, maxCell.y);
            var requested = new BoundsInt(minX, minY, 0, (maxX - minX) + 1, (maxY - minY) + 1, 1);
            var clamp = new BoundsInt(0, 0, 0, _cachedGroundRenderWidth, _cachedGroundRenderHeight, 1);
            bounds = ClampBoundsInt(requested, clamp);
            return bounds.size.x > 0 && bounds.size.y > 0;
        }

        private static TilemapChunkSnapshot BuildGroundRenderChunkSnapshot(
            Tilemap map,
            BoundsInt bounds,
            TilemapChunkCellData[] cells,
            int nonEmptyCount)
        {
            if (map == null || cells == null || nonEmptyCount <= 0)
                return null;

            return BuildTilemapChunkSnapshot(map, bounds, cells, nonEmptyCount);
        }

        private bool TryExtractCachedGroundRenderChunkSnapshots(
            Bounds worldBounds,
            out TilemapChunkSnapshot ground,
            out TilemapChunkSnapshot transitions)
        {
            ground = null;
            transitions = null;
            if (!_hasCachedGroundRenderPayload
                || _cachedGroundRenderPayload == null
                || _cachedGroundRenderWidth <= 0
                || _cachedGroundRenderHeight <= 0
                || !TryGetGroundRenderChunkCellBounds(worldBounds, out var bounds))
            {
                return false;
            }

            bool needGround = FarViewIncludeGround && _ground != null;
            bool needTransitions = FarViewIncludeTransitions && _transitions != null && _cachedGroundRenderUsesTransition;
            if (!needGround && !needTransitions)
                return false;

            int width = bounds.size.x;
            int height = bounds.size.y;
            int cellCount = width * height;
            TilemapChunkCellData[] groundCells = needGround ? new TilemapChunkCellData[cellCount] : null;
            TilemapChunkCellData[] transitionCells = needTransitions ? new TilemapChunkCellData[cellCount] : null;
            int groundNonEmptyCount = 0;
            int transitionNonEmptyCount = 0;

            int dst = 0;
            for (int row = bounds.yMin; row < bounds.yMax; row++)
            {
                int rowBase = row * _cachedGroundRenderWidth;
                for (int col = bounds.xMin; col < bounds.xMax; col++)
                {
                    var decision = _cachedGroundRenderPayload[rowBase + col];
                    if (needGround)
                    {
                        var tile = decision.GroundTile;
                        var transform = tile != null ? GetTileTransform(tile) : Matrix4x4.identity;
                        if (tile != null)
                            groundNonEmptyCount++;
                        groundCells[dst] = BuildTilemapChunkCellData(_ground, col, row, tile, transform);
                    }

                    if (needTransitions)
                    {
                        var tile = decision.TransitionTile;
                        var transform = tile != null ? GetTileTransform(tile) : Matrix4x4.identity;
                        if (tile != null)
                            transitionNonEmptyCount++;
                        transitionCells[dst] = BuildTilemapChunkCellData(_transitions, col, row, tile, transform);
                    }

                    dst++;
                }
            }

            ground = BuildGroundRenderChunkSnapshot(_ground, bounds, groundCells, groundNonEmptyCount);
            transitions = BuildGroundRenderChunkSnapshot(_transitions, bounds, transitionCells, transitionNonEmptyCount);
            return ground != null || transitions != null;
        }

        private bool TryExtractGroundRenderChunkSnapshots(
            Bounds worldBounds,
            out TilemapChunkSnapshot ground,
            out TilemapChunkSnapshot transitions)
        {
            if (_hasCachedGroundRenderPayload)
                return TryExtractCachedGroundRenderChunkSnapshots(worldBounds, out ground, out transitions);

            return TryExtractStreamGroundRenderChunkSnapshots(worldBounds, out ground, out transitions);
        }

        private bool TryExtractStreamGroundRenderChunkSnapshots(
            Bounds worldBounds,
            out TilemapChunkSnapshot ground,
            out TilemapChunkSnapshot transitions)
        {
            transitions = null;
            ground = null;
            if (!_streamingActive || !FarViewIncludeGround)
                return false;

            return TryExtractStreamGroundRenderChunkSnapshot(_ground, worldBounds, out ground);
        }

        private bool TryExtractStreamGroundRenderChunkSnapshot(
            Tilemap map,
            Bounds worldBounds,
            out TilemapChunkSnapshot snapshot)
        {
            snapshot = null;
            if (!_streamingActive || map == null)
                return false;

            var extracted = new System.Collections.Generic.List<TilemapChunkCellData>();
            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.GroundCells == null || state.GroundCellCount <= 0)
                    continue;

                for (int i = 0; i < state.GroundCells.Length; i++)
                {
                    var cell = state.GroundCells[i];
                    if (!IntersectsWorldBounds2D(cell, worldBounds))
                        continue;

                    extracted.Add(cell);
                    if (cell.Col < minCol) minCol = cell.Col;
                    if (cell.Row < minRow) minRow = cell.Row;
                    if (cell.Col > maxCol) maxCol = cell.Col;
                    if (cell.Row > maxRow) maxRow = cell.Row;
                    hasAny = true;
                }
            }

            if (!hasAny)
                return false;

            var bounds = new BoundsInt(minCol, minRow, 0, (maxCol - minCol) + 1, (maxRow - minRow) + 1, 1);
            int width = bounds.size.x;
            int height = bounds.size.y;
            var cells = new TilemapChunkCellData[width * height];
            int nonEmptyCount = 0;
            for (int i = 0; i < extracted.Count; i++)
            {
                var cell = extracted[i];
                int localX = cell.Col - bounds.xMin;
                int localY = cell.Row - bounds.yMin;
                if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                    continue;

                cells[(localY * width) + localX] = cell;
                nonEmptyCount++;
            }

            snapshot = BuildGroundRenderChunkSnapshot(map, bounds, cells, nonEmptyCount);
            return snapshot != null;
        }
    }
}
