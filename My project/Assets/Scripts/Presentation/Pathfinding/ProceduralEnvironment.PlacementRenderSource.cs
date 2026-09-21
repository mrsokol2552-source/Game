/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.PlacementRenderSource.cs
@module: presentation.pathfinding.worldgen.placement_render_source
@purpose: Caches prop/blocker placement payloads and exposes chunk-level snapshots so far-view can consume placement data without direct tilemap reads.
@entry: PENV-40, ProceduralEnvironment.CachePropPlacementRenderPayload, ProceduralEnvironment.TryExtractPlacementRenderChunkSnapshots
@api: partial class implementation for ProceduralEnvironment
@deps: placement generation, tilemap chunk snapshot types, far-view source
@data: cached prop and blocker placement cells, placement bounds, payload commitment flags
@perf: medium; builds once per non-stream generation and slices per far-view chunk
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and far-view generation smoke tests
@config: far-view props/blockers inclusion and prop/tree/blocker generation settings in ProceduralEnvironment
@assets: prop, tree, rock-prop, and blocker tiles
@notes: streaming captures generated placement lists; live tilemap reads remain only as explicit far-view legacy fallback when no placement payload source exists
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-PLACEMENTRENDERSOURCE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.PlacementRenderSource.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void ClearPlacementRenderPayloadCache()
        {
            BumpFarViewTileSnapshotVersion();
            _cachedPropPlacementPayload = null;
            _cachedPropPlacementBounds = default;
            _hasCachedPropPlacementPayload = false;
            _cachedBlockerPlacementPayload = null;
            _cachedBlockerPlacementBounds = default;
            _hasCachedBlockerPlacementPayload = false;
        }

        private bool HasCachedPlacementRenderPayloadSource(bool includeProps, bool includeBlockers)
        {
            return (includeProps && _hasCachedPropPlacementPayload)
                || (includeBlockers && _hasCachedBlockerPlacementPayload)
                || HasStreamPlacementRenderPayloadSource(includeProps, includeBlockers);
        }

        private void CachePropPlacementRenderPayload(params List<Placement>[] placementGroups)
        {
            CachePlacementRenderPayload(
                _props,
                placementGroups,
                out _cachedPropPlacementPayload,
                out _cachedPropPlacementBounds,
                out _hasCachedPropPlacementPayload);
        }

        private void CacheBlockerPlacementRenderPayload(params List<Placement>[] placementGroups)
        {
            CachePlacementRenderPayload(
                _blockers,
                placementGroups,
                out _cachedBlockerPlacementPayload,
                out _cachedBlockerPlacementBounds,
                out _hasCachedBlockerPlacementPayload);
        }

        private void CachePlacementRenderPayloads(
            List<Placement> blockingPlacements,
            List<Placement> treePlacements,
            List<Placement> treeAccentPlacements,
            List<Placement> rockPlacements,
            List<Placement> propPlacements,
            List<Placement> boostPlacements)
        {
            if (TreesBlockMovement)
            {
                CacheBlockerPlacementRenderPayload(blockingPlacements, treePlacements, treeAccentPlacements);
                CachePropPlacementRenderPayload(rockPlacements, propPlacements, boostPlacements);
            }
            else
            {
                CacheBlockerPlacementRenderPayload(blockingPlacements);
                CachePropPlacementRenderPayload(treePlacements, treeAccentPlacements, rockPlacements, propPlacements, boostPlacements);
            }
        }

        private void CachePlacementRenderPayload(
            Tilemap map,
            List<Placement>[] placementGroups,
            out TilemapChunkCellData[] payload,
            out BoundsInt bounds,
            out bool hasPayload)
        {
            BumpFarViewTileSnapshotVersion();
            payload = null;
            bounds = default;
            hasPayload = true;

            if (map == null || placementGroups == null || placementGroups.Length == 0)
                return;

            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;
            for (int g = 0; g < placementGroups.Length; g++)
            {
                var placements = placementGroups[g];
                if (placements == null) continue;
                for (int i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement.Tile == null)
                        continue;

                    int col = placement.Cell.x;
                    int row = placement.Cell.y;
                    if (col < minCol) minCol = col;
                    if (row < minRow) minRow = row;
                    if (col > maxCol) maxCol = col;
                    if (row > maxRow) maxRow = row;
                    hasAny = true;
                }
            }

            if (!hasAny)
                return;

            bounds = new BoundsInt(minCol, minRow, 0, (maxCol - minCol) + 1, (maxRow - minRow) + 1, 1);
            int width = bounds.size.x;
            int height = bounds.size.y;
            payload = new TilemapChunkCellData[width * height];
            for (int g = 0; g < placementGroups.Length; g++)
            {
                var placements = placementGroups[g];
                if (placements == null) continue;
                for (int i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement.Tile == null)
                        continue;

                    int localX = placement.Cell.x - bounds.xMin;
                    int localY = placement.Cell.y - bounds.yMin;
                    if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                        continue;

                    int idx = (localY * width) + localX;
                    var transform = GetTileTransform(placement.Tile);
                    payload[idx] = BuildTilemapChunkCellData(map, placement.Cell.x, placement.Cell.y, placement.Tile, transform);
                }
            }
        }

        private bool TryGetCachedPlacementRenderBounds(bool includeProps, bool includeBlockers, out Bounds bounds)
        {
            bounds = default;
            bool hasAny = false;
            if (includeProps && TryGetCachedPlacementRenderLayerBounds(_props, _hasCachedPropPlacementPayload, _cachedPropPlacementBounds, out var propBounds))
            {
                bounds = propBounds;
                hasAny = true;
            }

            if (includeBlockers && TryGetCachedPlacementRenderLayerBounds(_blockers, _hasCachedBlockerPlacementPayload, _cachedBlockerPlacementBounds, out var blockerBounds))
            {
                if (!hasAny)
                {
                    bounds = blockerBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(blockerBounds);
                }
            }

            if (hasAny)
                return true;

            return TryGetStreamPlacementRenderBounds(includeProps, includeBlockers, out bounds);
        }

        private static bool TryGetCachedPlacementRenderLayerBounds(
            Tilemap map,
            bool hasPayload,
            BoundsInt cellBounds,
            out Bounds bounds)
        {
            bounds = default;
            if (!hasPayload || map == null || cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
                return false;

            Vector3 min = map.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
            Vector3 max = map.CellToWorld(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));
            Vector3 size = max - min;
            if (size.x <= 0f || size.y <= 0f)
                return false;

            bounds = new Bounds(min + (size * 0.5f), size);
            return true;
        }

        private bool TryExtractPlacementRenderChunkSnapshots(
            Bounds worldBounds,
            out TilemapChunkSnapshot props,
            out TilemapChunkSnapshot blockers)
        {
            props = null;
            blockers = null;

            bool hasNonStreamPayload = (FarViewIncludeProps && _hasCachedPropPlacementPayload)
                || (FarViewIncludeBlockers && _hasCachedBlockerPlacementPayload);
            if (!hasNonStreamPayload && HasStreamPlacementRenderPayloadSource(FarViewIncludeProps, FarViewIncludeBlockers))
                return TryExtractStreamPlacementRenderChunkSnapshots(worldBounds, out props, out blockers);

            bool hasProps = TryExtractPlacementRenderChunkSnapshot(
                _props,
                FarViewIncludeProps && _hasCachedPropPlacementPayload,
                _cachedPropPlacementBounds,
                _cachedPropPlacementPayload,
                worldBounds,
                out props);
            bool hasBlockers = TryExtractPlacementRenderChunkSnapshot(
                _blockers,
                FarViewIncludeBlockers && _hasCachedBlockerPlacementPayload,
                _cachedBlockerPlacementBounds,
                _cachedBlockerPlacementPayload,
                worldBounds,
                out blockers);

            return hasProps || hasBlockers;
        }

        private bool HasStreamPlacementRenderPayloadSource(bool includeProps, bool includeBlockers)
        {
            if (!_streamingActive)
                return false;

            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated)
                    continue;
                if (includeProps && state.PropPlacements != null)
                    return true;
                if (includeBlockers && state.BlockerPlacements != null)
                    return true;
            }

            return false;
        }

        private bool TryGetStreamPlacementRenderBounds(bool includeProps, bool includeBlockers, out Bounds bounds)
        {
            bounds = default;
            bool hasAny = false;
            if (includeProps && TryGetStreamPlacementRenderLayerBounds(_props, propsLayer: true, out var propBounds))
            {
                bounds = propBounds;
                hasAny = true;
            }

            if (includeBlockers && TryGetStreamPlacementRenderLayerBounds(_blockers, propsLayer: false, out var blockerBounds))
            {
                if (!hasAny)
                {
                    bounds = blockerBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(blockerBounds);
                }
            }

            return hasAny;
        }

        private bool TryGetStreamPlacementRenderLayerBounds(Tilemap map, bool propsLayer, out Bounds bounds)
        {
            bounds = default;
            if (!_streamingActive || map == null)
                return false;

            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated)
                    continue;

                var placements = propsLayer ? state.PropPlacements : state.BlockerPlacements;
                if (placements == null)
                    continue;

                for (int i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement.Tile == null)
                        continue;

                    int col = placement.Cell.x;
                    int row = placement.Cell.y;
                    if (col < minCol) minCol = col;
                    if (row < minRow) minRow = row;
                    if (col > maxCol) maxCol = col;
                    if (row > maxRow) maxRow = row;
                    hasAny = true;
                }
            }

            if (!hasAny)
                return false;

            var cellBounds = new BoundsInt(minCol, minRow, 0, (maxCol - minCol) + 1, (maxRow - minRow) + 1, 1);
            return TryGetCachedPlacementRenderLayerBounds(map, true, cellBounds, out bounds);
        }

        private bool TryExtractStreamPlacementRenderChunkSnapshots(
            Bounds worldBounds,
            out TilemapChunkSnapshot props,
            out TilemapChunkSnapshot blockers)
        {
            props = null;
            blockers = null;
            bool hasProps = FarViewIncludeProps && TryExtractStreamPlacementRenderChunkSnapshot(_props, propsLayer: true, worldBounds, out props);
            bool hasBlockers = FarViewIncludeBlockers && TryExtractStreamPlacementRenderChunkSnapshot(_blockers, propsLayer: false, worldBounds, out blockers);
            return hasProps || hasBlockers;
        }

        private bool TryExtractStreamPlacementRenderChunkSnapshot(
            Tilemap map,
            bool propsLayer,
            Bounds worldBounds,
            out TilemapChunkSnapshot snapshot)
        {
            snapshot = null;
            if (!_streamingActive || map == null)
                return false;

            var extracted = new List<TilemapChunkCellData>();
            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated)
                    continue;

                var placements = propsLayer ? state.PropPlacements : state.BlockerPlacements;
                if (placements == null)
                    continue;

                for (int i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement.Tile == null)
                        continue;

                    var transform = GetTileTransform(placement.Tile);
                    var cell = BuildTilemapChunkCellData(map, placement.Cell.x, placement.Cell.y, placement.Tile, transform);
                    if (!IntersectsWorldBounds2D(cell, worldBounds))
                        continue;

                    extracted.Add(cell);
                    int col = placement.Cell.x;
                    int row = placement.Cell.y;
                    if (col < minCol) minCol = col;
                    if (row < minRow) minRow = row;
                    if (col > maxCol) maxCol = col;
                    if (row > maxRow) maxRow = row;
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

            if (nonEmptyCount <= 0)
                return false;

            snapshot = BuildTilemapChunkSnapshot(map, bounds, cells, nonEmptyCount);
            return true;
        }

        private static bool IntersectsWorldBounds2D(TilemapChunkCellData cell, Bounds bounds)
        {
            if (!cell.HasTile)
                return false;
            return cell.WorldMax.x > bounds.min.x
                && cell.WorldMin.x < bounds.max.x
                && cell.WorldMax.y > bounds.min.y
                && cell.WorldMin.y < bounds.max.y;
        }

        private bool TryExtractPlacementRenderChunkSnapshot(
            Tilemap map,
            bool hasPayload,
            BoundsInt payloadBounds,
            TilemapChunkCellData[] payload,
            Bounds worldBounds,
            out TilemapChunkSnapshot snapshot)
        {
            snapshot = null;
            if (!hasPayload
                || map == null
                || payload == null
                || payload.Length == 0
                || payloadBounds.size.x <= 0
                || payloadBounds.size.y <= 0
                || !TryGetPlacementRenderChunkCellBounds(map, payloadBounds, worldBounds, out var bounds))
            {
                return false;
            }

            int width = bounds.size.x;
            int height = bounds.size.y;
            var cells = new TilemapChunkCellData[width * height];
            int nonEmptyCount = 0;
            int dst = 0;
            for (int row = bounds.yMin; row < bounds.yMax; row++)
            {
                int srcRow = (row - payloadBounds.yMin) * payloadBounds.size.x;
                for (int col = bounds.xMin; col < bounds.xMax; col++)
                {
                    int src = srcRow + (col - payloadBounds.xMin);
                    var cell = payload[src];
                    if (cell.HasTile)
                        nonEmptyCount++;
                    cells[dst++] = cell;
                }
            }

            if (nonEmptyCount <= 0)
                return false;

            snapshot = BuildTilemapChunkSnapshot(map, bounds, cells, nonEmptyCount);
            return true;
        }

        private bool TryGetPlacementRenderChunkCellBounds(
            Tilemap map,
            BoundsInt payloadBounds,
            Bounds worldBounds,
            out BoundsInt bounds)
        {
            bounds = default;
            if (map == null || payloadBounds.size.x <= 0 || payloadBounds.size.y <= 0)
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
            bounds = ClampBoundsInt(requested, payloadBounds);
            return bounds.size.x > 0 && bounds.size.y > 0;
        }
    }
}
