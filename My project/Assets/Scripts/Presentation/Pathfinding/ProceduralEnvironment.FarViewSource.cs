/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewSource.cs
@module: presentation.pathfinding.worldgen.farview_source
@purpose: Builds a far-view bake source that combines live scene renderers with cached background/ground/placement render payloads so future renderers can read extracted data instead of tilemap state directly.
@entry: PENV-27, ProceduralEnvironment.TryBuildFarViewBakeSource
@api: partial class implementation for ProceduralEnvironment
@deps: far-view bake pipeline, background/ground/placement render payloads, cached background ruleset input
@data: far-view renderers, bake bounds, cached background/ground/placement decisions, background world bounds
@perf: medium; background payload is cached per generated world and reused across far-view bakes
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and far-view bake smoke tests
@config: far-view include-background settings and background render configuration in ProceduralEnvironment
@assets: none directly; this is a runtime source contract between worldgen and far-view rendering
@notes: keep this layer renderer-agnostic; live tilemap reads are isolated as legacy fallback when no payload source exists
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWSOURCE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewSource.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void BumpFarViewTileSnapshotVersion()
        {
            unchecked
            {
                _cachedFarViewTileSnapshotVersion++;
                if (_cachedFarViewTileSnapshotVersion == 0)
                    _cachedFarViewTileSnapshotVersion = 1;
            }
        }

        private void BumpCachedBackgroundFarViewPayloadVersion()
        {
            unchecked
            {
                _cachedBackgroundFarViewPayloadVersion++;
                if (_cachedBackgroundFarViewPayloadVersion == 0)
                    _cachedBackgroundFarViewPayloadVersion = 1;
            }
        }

        private bool HasCachedGroundRenderPayloadSource(bool includeGround, bool includeTransitions)
        {
            return (includeGround || includeTransitions)
                && _hasCachedGroundRenderPayload
                && _cachedGroundRenderPayload != null
                && _cachedGroundRenderWidth > 0
                && _cachedGroundRenderHeight > 0;
        }

        private bool HasGroundRenderPayloadSource(bool includeGround, bool includeTransitions)
        {
            return HasCachedGroundRenderPayloadSource(includeGround, includeTransitions)
                || HasStreamGroundRenderPayloadSource(includeGround, includeTransitions);
        }

        private bool HasStreamBackgroundRenderPayloadSource()
        {
            if (!_streamingActive || !FarViewIncludeBackground || !UseBackgroundTilemap)
                return false;

            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (state.Generated && state.BackgroundPayload != null && state.BackgroundCellCount > 0)
                    return true;
            }

            return false;
        }

        private bool HasCachedPlacementRenderPayloadOnly(bool includeProps, bool includeBlockers)
        {
            return (includeProps && _hasCachedPropPlacementPayload)
                || (includeBlockers && _hasCachedBlockerPlacementPayload);
        }

        private string ResolveFarViewBackgroundPayloadSourceLabel()
        {
            if (!FarViewIncludeBackground || !UseBackgroundTilemap)
                return "disabled";
            if (_hasCachedBackgroundFarViewPayload && _cachedBackgroundFarViewPayload != null && _cachedBackgroundFarViewPayload.Length > 0)
                return "cached-ready";
            if (_hasBackgroundRenderInput)
                return "cached-input";
            if (HasStreamBackgroundRenderPayloadSource())
                return "stream";
            return "none";
        }

        private string ResolveFarViewGroundPayloadSourceLabel()
        {
            bool includeGround = FarViewIncludeGround || FarViewIncludeTransitions;
            if (!includeGround)
                return "disabled";
            if (HasCachedGroundRenderPayloadSource(FarViewIncludeGround, FarViewIncludeTransitions))
                return "cached";
            if (HasStreamGroundRenderPayloadSource(FarViewIncludeGround, FarViewIncludeTransitions))
                return "stream";
            return "legacy-fallback";
        }

        private string ResolveFarViewPlacementPayloadSourceLabel()
        {
            bool includePlacement = FarViewIncludeProps || FarViewIncludeBlockers;
            if (!includePlacement)
                return "disabled";
            if (HasCachedPlacementRenderPayloadOnly(FarViewIncludeProps, FarViewIncludeBlockers))
                return "cached";
            if (HasStreamPlacementRenderPayloadSource(FarViewIncludeProps, FarViewIncludeBlockers))
                return "stream";
            return "legacy-fallback";
        }

        internal readonly struct FarViewPayloadSourceDiagnostics
        {
            public string BackgroundSource { get; }
            public string GroundSource { get; }
            public string PlacementSource { get; }

            public FarViewPayloadSourceDiagnostics(
                string backgroundSource,
                string groundSource,
                string placementSource)
            {
                BackgroundSource = backgroundSource;
                GroundSource = groundSource;
                PlacementSource = placementSource;
            }
        }

        internal FarViewPayloadSourceDiagnostics GetFarViewPayloadSourceDiagnostics()
        {
            return new FarViewPayloadSourceDiagnostics(
                ResolveFarViewBackgroundPayloadSourceLabel(),
                ResolveFarViewGroundPayloadSourceLabel(),
                ResolveFarViewPlacementPayloadSourceLabel());
        }

        private string BuildFarViewPayloadSmokeText(FarViewBakeSource source)
        {
            var diagnostics = GetFarViewPayloadSourceDiagnostics();
            int rendererCount = source != null && source.Renderers != null ? source.Renderers.Count : -1;
            string bounds = source != null && source.HasBounds
                ? $"{source.Bounds.center.x:0.##},{source.Bounds.center.y:0.##} / {source.Bounds.size.x:0.##}x{source.Bounds.size.y:0.##}"
                : "none";
            return "[ProceduralEnvironment] Far-view payload smoke: "
                + $"background={diagnostics.BackgroundSource}, "
                + $"ground={diagnostics.GroundSource}, "
                + $"placement={diagnostics.PlacementSource}, "
                + $"renderers={rendererCount}, "
                + $"bounds={bounds}";
        }

        [ContextMenu("Log Far View Payload Sources")]
        private void LogFarViewPayloadSources()
        {
            Debug.Log(BuildFarViewPayloadSmokeText(null));
        }

        private sealed class FarViewBakeSource
        {
            public List<Renderer> Renderers;
            public Bounds Bounds;
            public bool HasBounds;
            public BackgroundRenderCellDecision[] BackgroundPayload;
            public Bounds BackgroundBounds;
            public bool HasBackgroundPayload;
            public int BackgroundPayloadWidth;
            public int BackgroundPayloadHeight;
            public Vector2Int BackgroundPayloadOrigin;
            public int BackgroundPayloadVersion;
            public bool HasTileSnapshotSource;
        }

        private readonly struct FarViewBackgroundChunkSource
        {
            public readonly BackgroundRenderCellDecision[] Decisions;
            public readonly RectInt CellRect;
            public readonly Bounds WorldBounds;
            public readonly int PayloadVersion;

            public bool HasPayload => Decisions != null && Decisions.Length > 0;
            public int CellCount => Decisions != null ? Decisions.Length : 0;

            public FarViewBackgroundChunkSource(
                BackgroundRenderCellDecision[] decisions,
                RectInt cellRect,
                Bounds worldBounds,
                int payloadVersion)
            {
                Decisions = decisions;
                CellRect = cellRect;
                WorldBounds = worldBounds;
                PayloadVersion = payloadVersion;
            }
        }

        private readonly struct FarViewTileChunkSource
        {
            public readonly TilemapChunkSnapshot Ground;
            public readonly TilemapChunkSnapshot Transitions;
            public readonly TilemapChunkSnapshot Props;
            public readonly TilemapChunkSnapshot Blockers;

            public bool HasTiles =>
                (Ground != null && Ground.HasTiles)
                || (Transitions != null && Transitions.HasTiles)
                || (Props != null && Props.HasTiles)
                || (Blockers != null && Blockers.HasTiles);
            public int GroundCellCount => Ground != null ? Ground.NonEmptyCount : 0;
            public int TransitionCellCount => Transitions != null ? Transitions.NonEmptyCount : 0;
            public int PropCellCount => Props != null ? Props.NonEmptyCount : 0;
            public int BlockerCellCount => Blockers != null ? Blockers.NonEmptyCount : 0;

            public FarViewTileChunkSource(
                TilemapChunkSnapshot ground,
                TilemapChunkSnapshot transitions,
                TilemapChunkSnapshot props,
                TilemapChunkSnapshot blockers)
            {
                Ground = ground;
                Transitions = transitions;
                Props = props;
                Blockers = blockers;
            }
        }

        private void ClearBackgroundRenderInputCache()
        {
            BumpCachedBackgroundFarViewPayloadVersion();
            _hasBackgroundRenderInput = false;
            _cachedBackgroundRenderWidth = 0;
            _cachedBackgroundRenderHeight = 0;
            _cachedBackgroundRenderTileSeed = 0;
            _cachedBackgroundRenderLayers = null;
            _cachedBackgroundRenderLayerIndex = null;
            _cachedBackgroundRenderLookup = null;
            _cachedBackgroundFarViewPayload = null;
            _cachedBackgroundFarViewBounds = default;
            _hasCachedBackgroundFarViewPayload = false;
        }

        private void CacheBackgroundRenderInput(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            Dictionary<TileBase, TileBase> backgroundLookup)
        {
            BumpCachedBackgroundFarViewPayloadVersion();
            _hasBackgroundRenderInput = width > 0
                && height > 0
                && layers != null
                && layerIndex != null
                && layerIndex.Length == width * height;
            if (!_hasBackgroundRenderInput)
            {
                ClearBackgroundRenderInputCache();
                return;
            }

            _cachedBackgroundRenderWidth = width;
            _cachedBackgroundRenderHeight = height;
            _cachedBackgroundRenderTileSeed = tileSeed;
            _cachedBackgroundRenderLayers = layers;
            _cachedBackgroundRenderLayerIndex = layerIndex;
            _cachedBackgroundRenderLookup = backgroundLookup;
            _cachedBackgroundFarViewPayload = null;
            _cachedBackgroundFarViewBounds = default;
            _hasCachedBackgroundFarViewPayload = false;
        }

        private bool TryGetCachedBackgroundFarViewBounds(int width, int height, out Bounds bounds)
        {
            bounds = default;
            if (_background == null || width <= 0 || height <= 0)
                return false;

            Vector3 min = _background.CellToWorld(Vector3Int.zero);
            Vector3 max = _background.CellToWorld(new Vector3Int(width, height, 0));
            Vector3 size = max - min;
            if (size.x <= 0f || size.y <= 0f)
                return false;

            bounds = new Bounds(min + (size * 0.5f), size);
            return true;
        }

        private bool TryBuildCachedBackgroundFarViewPayload(
            out BackgroundRenderCellDecision[] payload,
            out Bounds bounds)
        {
            return TryBuildCachedBackgroundRenderPayload(requireFarViewIncludeBackground: true, out payload, out bounds);
        }

        private bool TryBuildCachedBackgroundRenderPayload(
            bool requireFarViewIncludeBackground,
            out BackgroundRenderCellDecision[] payload,
            out Bounds bounds)
        {
            payload = null;
            bounds = default;
            if (!_hasBackgroundRenderInput || !UseBackgroundTilemap)
                return false;
            if (requireFarViewIncludeBackground && !FarViewIncludeBackground)
                return false;

            if (_hasCachedBackgroundFarViewPayload
                && _cachedBackgroundFarViewPayload != null
                && _cachedBackgroundFarViewPayload.Length == _cachedBackgroundRenderWidth * _cachedBackgroundRenderHeight)
            {
                payload = _cachedBackgroundFarViewPayload;
                bounds = _cachedBackgroundFarViewBounds;
                return true;
            }

            var resources = BuildBackgroundRenderResources(
                _cachedBackgroundRenderWidth,
                _cachedBackgroundRenderHeight,
                _cachedBackgroundRenderLayers,
                _cachedBackgroundRenderTileSeed,
                _cachedBackgroundRenderLookup,
                allocateRefineBuffers: false);

            var decisions = new BackgroundRenderCellDecision[resources.Size];
            PopulateBackgroundRenderDecisionBlock(
                _cachedBackgroundRenderWidth,
                _cachedBackgroundRenderHeight,
                _cachedBackgroundRenderLayers,
                _cachedBackgroundRenderTileSeed,
                _cachedBackgroundRenderLayerIndex,
                resources,
                0,
                _cachedBackgroundRenderHeight,
                decisions);

            if (UseWaterEdgeRefinement && UseWaterEdgeColorMatch)
            {
                var tiles = new TileBase[resources.Size];
                Matrix4x4[] transforms = resources.UseTransform ? new Matrix4x4[resources.Size] : null;
                ApplyBackgroundRenderDecisionBlock(resources, decisions, tiles, transforms);
                RefineWaterEdgesRectCore(
                    _cachedBackgroundRenderWidth,
                    _cachedBackgroundRenderHeight,
                    resources.WaterMask,
                    resources.RockMask,
                    _cachedBackgroundRenderLayerIndex,
                    resources.LayerVariants,
                    resources.WaterVariants,
                    resources.WaterInteriorVariants,
                    resources.RockVariants,
                    resources.SharedVariants,
                    _cachedBackgroundRenderTileSeed,
                    tiles,
                    transforms,
                    resources.PlacedProfiles,
                    resources.PlacedValid,
                    resources.PlacedVariantIds);

                for (int i = 0; i < decisions.Length; i++)
                {
                    decisions[i].Tile = tiles[i];
                    if (transforms != null)
                        decisions[i].Transform = transforms[i];
                    if (resources.PlacedProfiles != null)
                        decisions[i].Profile = resources.PlacedProfiles[i];
                    if (resources.PlacedVariantIds != null)
                        decisions[i].VariantId = resources.PlacedVariantIds[i];
                }
            }

            if (!TryGetCachedBackgroundFarViewBounds(_cachedBackgroundRenderWidth, _cachedBackgroundRenderHeight, out bounds))
                return false;

            _cachedBackgroundFarViewPayload = decisions;
            _cachedBackgroundFarViewBounds = bounds;
            _hasCachedBackgroundFarViewPayload = true;
            payload = decisions;
            return true;
        }

        private bool TryBuildFarViewBackgroundPayload(
            out BackgroundRenderCellDecision[] payload,
            out Bounds bounds,
            out int width,
            out int height,
            out Vector2Int origin)
        {
            if (TryBuildCachedBackgroundFarViewPayload(out payload, out bounds))
            {
                width = _cachedBackgroundRenderWidth;
                height = _cachedBackgroundRenderHeight;
                origin = Vector2Int.zero;
                return true;
            }

            return TryBuildStreamBackgroundFarViewPayload(out payload, out bounds, out width, out height, out origin);
        }

        private bool TryBuildStreamBackgroundFarViewPayload(
            out BackgroundRenderCellDecision[] payload,
            out Bounds bounds,
            out int width,
            out int height,
            out Vector2Int origin)
        {
            payload = null;
            bounds = default;
            width = 0;
            height = 0;
            origin = default;
            if (!_streamingActive || !FarViewIncludeBackground || !UseBackgroundTilemap || _background == null || _backgroundGrid == null)
                return false;

            bool hasAny = false;
            int minCol = int.MaxValue;
            int minRow = int.MaxValue;
            int maxCol = int.MinValue;
            int maxRow = int.MinValue;
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.BackgroundPayload == null || state.BackgroundCellCount <= 0)
                    continue;

                for (int i = 0; i < state.BackgroundPayload.Length; i++)
                {
                    var decision = state.BackgroundPayload[i];
                    if (decision.Tile == null)
                        continue;

                    if (decision.Col < minCol) minCol = decision.Col;
                    if (decision.Row < minRow) minRow = decision.Row;
                    if (decision.Col > maxCol) maxCol = decision.Col;
                    if (decision.Row > maxRow) maxRow = decision.Row;
                    hasAny = true;
                }
            }

            if (!hasAny)
                return false;

            width = (maxCol - minCol) + 1;
            height = (maxRow - minRow) + 1;
            if (width <= 0 || height <= 0)
                return false;

            payload = new BackgroundRenderCellDecision[width * height];
            foreach (var kvp in _streamChunks)
            {
                var state = kvp.Value;
                if (!state.Generated || state.BackgroundPayload == null || state.BackgroundCellCount <= 0)
                    continue;

                for (int i = 0; i < state.BackgroundPayload.Length; i++)
                {
                    var decision = state.BackgroundPayload[i];
                    if (decision.Tile == null)
                        continue;

                    int localX = decision.Col - minCol;
                    int localY = decision.Row - minRow;
                    if (localX < 0 || localY < 0 || localX >= width || localY >= height)
                        continue;

                    payload[(localY * width) + localX] = decision;
                }
            }

            Vector3 worldMin = _background.CellToWorld(new Vector3Int(minCol, minRow, 0));
            Vector3 worldMax = _background.CellToWorld(new Vector3Int(maxCol + 1, maxRow + 1, 0));
            Vector3 size = worldMax - worldMin;
            if (size.x <= 0f || size.y <= 0f)
                return false;

            bounds = new Bounds(worldMin + (size * 0.5f), size);
            origin = new Vector2Int(minCol, minRow);
            return true;
        }

        private bool TryBuildFarViewBakeSource(out FarViewBakeSource source)
        {
            source = new FarViewBakeSource
            {
                Renderers = CollectFarViewRenderers()
            };

            if (UseFarViewChunkedBake && source.Renderers != null)
            {
                RemoveTilemapRenderer(_ground, source.Renderers, FarViewIncludeGround);
                RemoveTilemapRenderer(_transitions, source.Renderers, FarViewIncludeTransitions);
                RemoveTilemapRenderer(_props, source.Renderers, FarViewIncludeProps);
                RemoveTilemapRenderer(_blockers, source.Renderers, FarViewIncludeBlockers);
            }

            source.HasBounds = TryGetFarViewBounds(source.Renderers, out source.Bounds);
            source.HasTileSnapshotSource = TryBuildFarViewTileSnapshotBounds(out var tileSnapshotBounds);
            if (source.HasTileSnapshotSource)
            {
                if (!source.HasBounds)
                {
                    source.Bounds = tileSnapshotBounds;
                    source.HasBounds = true;
                }
                else
                {
                    source.Bounds.Encapsulate(tileSnapshotBounds);
                }
            }
            if (TryBuildFarViewBackgroundPayload(out var payload, out var payloadBounds, out var payloadWidth, out var payloadHeight, out var payloadOrigin))
            {
                source.BackgroundPayload = payload;
                source.BackgroundBounds = payloadBounds;
                source.HasBackgroundPayload = true;
                source.BackgroundPayloadWidth = payloadWidth;
                source.BackgroundPayloadHeight = payloadHeight;
                source.BackgroundPayloadOrigin = payloadOrigin;
                source.BackgroundPayloadVersion = _cachedBackgroundFarViewPayloadVersion;
                if (UseFarViewChunkedBake && _background != null)
                {
                    var backgroundRenderer = _background.GetComponent<Renderer>();
                    if (backgroundRenderer != null)
                        source.Renderers.Remove(backgroundRenderer);
                }
                if (!source.HasBounds)
                {
                    source.Bounds = payloadBounds;
                    source.HasBounds = true;
                }
                else
                {
                    source.Bounds.Encapsulate(payloadBounds);
                }
            }

            return source.HasBounds
                && ((source.Renderers != null && source.Renderers.Count > 0) || source.HasBackgroundPayload || source.HasTileSnapshotSource);
        }

        private bool TryBuildFarViewBackgroundChunkSource(
            FarViewBakeSource source,
            FarViewChunk reuseChunk,
            Bounds chunkBounds,
            out FarViewBackgroundChunkSource chunkSource)
        {
            chunkSource = default;
            if (source == null
                || !source.HasBackgroundPayload
                || source.BackgroundPayload == null
                || source.BackgroundPayload.Length == 0
                || _backgroundGrid == null
                || _background == null
                || source.BackgroundPayloadWidth <= 0
                || source.BackgroundPayloadHeight <= 0)
            {
                return false;
            }

            float minX = Mathf.Max(chunkBounds.min.x, source.BackgroundBounds.min.x);
            float minY = Mathf.Max(chunkBounds.min.y, source.BackgroundBounds.min.y);
            float maxX = Mathf.Min(chunkBounds.max.x, source.BackgroundBounds.max.x);
            float maxY = Mathf.Min(chunkBounds.max.y, source.BackgroundBounds.max.y);
            if (maxX <= minX || maxY <= minY)
                return false;

            Vector3 cellSize = _backgroundGrid.cellSize;
            float cellEpsilon = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 0.001f);
            Vector3Int minCell = _backgroundGrid.WorldToCell(new Vector3(minX + cellEpsilon, minY + cellEpsilon, 0f));
            Vector3Int maxCell = _backgroundGrid.WorldToCell(new Vector3(maxX - cellEpsilon, maxY - cellEpsilon, 0f));

            int payloadMinX = source.BackgroundPayloadOrigin.x;
            int payloadMinY = source.BackgroundPayloadOrigin.y;
            int payloadMaxX = payloadMinX + source.BackgroundPayloadWidth - 1;
            int payloadMaxY = payloadMinY + source.BackgroundPayloadHeight - 1;
            int clampedMinX = Mathf.Clamp(minCell.x, payloadMinX, payloadMaxX);
            int clampedMinY = Mathf.Clamp(minCell.y, payloadMinY, payloadMaxY);
            int clampedMaxX = Mathf.Clamp(maxCell.x, payloadMinX, payloadMaxX);
            int clampedMaxY = Mathf.Clamp(maxCell.y, payloadMinY, payloadMaxY);
            if (clampedMaxX < clampedMinX || clampedMaxY < clampedMinY)
                return false;

            int sliceWidth = clampedMaxX - clampedMinX + 1;
            int sliceHeight = clampedMaxY - clampedMinY + 1;
            var cellRect = new RectInt(clampedMinX, clampedMinY, sliceWidth, sliceHeight);

            Vector3 worldMin = _background.CellToWorld(new Vector3Int(clampedMinX, clampedMinY, 0));
            Vector3 worldMax = _background.CellToWorld(new Vector3Int(clampedMaxX + 1, clampedMaxY + 1, 0));
            Vector3 worldSize = worldMax - worldMin;
            if (worldSize.x <= 0f || worldSize.y <= 0f)
                return false;

            var worldBounds = new Bounds(worldMin + (worldSize * 0.5f), worldSize);
            var renderSource = reuseChunk != null ? reuseChunk.RenderSource : null;
            if (reuseChunk != null
                && renderSource != null
                && renderSource.BackgroundPayload != null
                && renderSource.BackgroundPayloadVersion == source.BackgroundPayloadVersion
                && renderSource.BackgroundCellRect.Equals(cellRect)
                && renderSource.BackgroundPayload.Length == sliceWidth * sliceHeight
                && AreFarViewBoundsApproximatelyEqual(renderSource.BackgroundBounds, worldBounds))
            {
                _farViewBackgroundSliceCacheHits++;
                chunkSource = new FarViewBackgroundChunkSource(
                    renderSource.BackgroundPayload,
                    cellRect,
                    worldBounds,
                    source.BackgroundPayloadVersion);
                return true;
            }

            _farViewBackgroundSliceCacheMisses++;
            var decisions = new BackgroundRenderCellDecision[sliceWidth * sliceHeight];
            int dst = 0;
            for (int row = clampedMinY; row <= clampedMaxY; row++)
            {
                int rowBase = (row - payloadMinY) * source.BackgroundPayloadWidth;
                for (int col = clampedMinX; col <= clampedMaxX; col++)
                    decisions[dst++] = source.BackgroundPayload[rowBase + (col - payloadMinX)];
            }

            chunkSource = new FarViewBackgroundChunkSource(
                decisions,
                cellRect,
                worldBounds,
                source.BackgroundPayloadVersion);
            return true;
        }

        private static void ApplyFarViewBackgroundChunkSource(FarViewChunk chunk, FarViewBackgroundChunkSource source)
        {
            if (chunk == null)
                return;

            var renderSource = chunk.RenderSource;
            renderSource.BackgroundPayload = source.Decisions;
            renderSource.BackgroundPayloadVersion = source.HasPayload ? source.PayloadVersion : 0;
            renderSource.BackgroundCellCount = source.CellCount;
            renderSource.BackgroundCellRect = source.CellRect;
            renderSource.BackgroundBounds = source.WorldBounds;
        }

        private bool TryBuildFarViewTileChunkSource(FarViewChunk reuseChunk, Bounds chunkBounds, out FarViewTileChunkSource source)
        {
            source = default;
            var renderSource = reuseChunk != null ? reuseChunk.RenderSource : null;
            if (reuseChunk != null
                && renderSource != null
                && renderSource.TileSnapshotResolved
                && renderSource.TileSnapshotVersion == _cachedFarViewTileSnapshotVersion
                && AreFarViewBoundsApproximatelyEqual(renderSource.TileSnapshotCaptureBounds, chunkBounds))
            {
                _farViewTileSliceCacheHits++;
                source = new FarViewTileChunkSource(
                    renderSource.GroundSnapshot,
                    renderSource.TransitionSnapshot,
                    renderSource.PropSnapshot,
                    renderSource.BlockerSnapshot);
                return source.HasTiles;
            }

            _farViewTileSliceCacheMisses++;
            TilemapChunkSnapshot ground = null;
            TilemapChunkSnapshot transitions = null;
            TilemapChunkSnapshot props = null;
            TilemapChunkSnapshot blockers = null;
            bool hasGround = false;
            bool hasTransitions = false;
            bool hasProps = false;
            bool hasBlockers = false;
            bool hasGroundPayload = HasGroundRenderPayloadSource(FarViewIncludeGround, FarViewIncludeTransitions);
            if (hasGroundPayload
                && TryExtractGroundRenderChunkSnapshots(chunkBounds, out var cachedGround, out var cachedTransitions))
            {
                ground = cachedGround;
                transitions = cachedTransitions;
                hasGround = FarViewIncludeGround && ground != null && ground.HasTiles;
                hasTransitions = FarViewIncludeTransitions && transitions != null && transitions.HasTiles;
            }

            if (!hasGroundPayload && !hasGround)
                hasGround = FarViewIncludeGround && TryExtractLegacyTilemapChunkSnapshot(_ground, chunkBounds, out ground);
            if (!hasGroundPayload && !hasTransitions)
                hasTransitions = FarViewIncludeTransitions && TryExtractLegacyTilemapChunkSnapshot(_transitions, chunkBounds, out transitions);
            bool hasCachedPlacementPayload = HasCachedPlacementRenderPayloadSource(FarViewIncludeProps, FarViewIncludeBlockers);
            if (hasCachedPlacementPayload
                && TryExtractPlacementRenderChunkSnapshots(chunkBounds, out var cachedProps, out var cachedBlockers))
            {
                props = cachedProps;
                blockers = cachedBlockers;
                hasProps = FarViewIncludeProps && props != null && props.HasTiles;
                hasBlockers = FarViewIncludeBlockers && blockers != null && blockers.HasTiles;
            }

            if (!hasCachedPlacementPayload)
            {
                hasProps = FarViewIncludeProps && TryExtractLegacyTilemapChunkSnapshot(_props, chunkBounds, out props);
                hasBlockers = FarViewIncludeBlockers && TryExtractLegacyTilemapChunkSnapshot(_blockers, chunkBounds, out blockers);
            }
            if (!hasGround && !hasTransitions && !hasProps && !hasBlockers)
                return false;

            source = new FarViewTileChunkSource(ground, transitions, props, blockers);
            return source.HasTiles;
        }

        private void ApplyFarViewTileChunkSource(FarViewChunk chunk, FarViewTileChunkSource source, Bounds chunkBounds)
        {
            if (chunk == null)
                return;

            var renderSource = chunk.RenderSource;
            renderSource.GroundSnapshot = source.Ground;
            renderSource.TransitionSnapshot = source.Transitions;
            renderSource.PropSnapshot = source.Props;
            renderSource.BlockerSnapshot = source.Blockers;
            renderSource.GroundCellCount = source.GroundCellCount;
            renderSource.TransitionCellCount = source.TransitionCellCount;
            renderSource.PropCellCount = source.PropCellCount;
            renderSource.BlockerCellCount = source.BlockerCellCount;
            renderSource.TileSnapshotResolved = true;
            renderSource.TileSnapshotVersion = _cachedFarViewTileSnapshotVersion;
            renderSource.TileSnapshotCaptureBounds = chunkBounds;
        }

        private static void RemoveTilemapRenderer(Tilemap map, List<Renderer> renderers, bool enabled)
        {
            if (!enabled || map == null || renderers == null || renderers.Count == 0)
                return;

            var renderer = map.GetComponent<Renderer>();
            if (renderer != null)
                renderers.Remove(renderer);
        }

        private bool TryBuildFarViewTileSnapshotBounds(out Bounds bounds)
        {
            bounds = default;
            if (!UseFarViewChunkedBake)
                return false;

            bool hasAny = false;
            if (HasGroundRenderPayloadSource(FarViewIncludeGround, FarViewIncludeTransitions)
                && TryGetGroundRenderPayloadBounds(FarViewIncludeGround, FarViewIncludeTransitions, out var cachedGroundBounds))
            {
                bounds = cachedGroundBounds;
                hasAny = true;
            }
            else
            {
                if (FarViewIncludeGround && TryGetLegacyTilemapUsedWorldBounds(_ground, out var groundBounds))
                {
                    bounds = groundBounds;
                    hasAny = true;
                }

                if (FarViewIncludeTransitions && TryGetLegacyTilemapUsedWorldBounds(_transitions, out var transitionBounds))
                {
                    if (!hasAny)
                    {
                        bounds = transitionBounds;
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(transitionBounds);
                    }
                }
            }

            bool hasCachedPlacementPayload = HasCachedPlacementRenderPayloadSource(FarViewIncludeProps, FarViewIncludeBlockers);
            if (hasCachedPlacementPayload
                && TryGetCachedPlacementRenderBounds(FarViewIncludeProps, FarViewIncludeBlockers, out var cachedPlacementBounds))
            {
                if (!hasAny)
                {
                    bounds = cachedPlacementBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(cachedPlacementBounds);
                }
            }

            if (!hasCachedPlacementPayload && FarViewIncludeProps && TryGetLegacyTilemapUsedWorldBounds(_props, out var propBounds))
            {
                if (!hasAny)
                {
                    bounds = propBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(propBounds);
                }
            }

            if (!hasCachedPlacementPayload && FarViewIncludeBlockers && TryGetLegacyTilemapUsedWorldBounds(_blockers, out var blockerBounds))
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
    }
}
