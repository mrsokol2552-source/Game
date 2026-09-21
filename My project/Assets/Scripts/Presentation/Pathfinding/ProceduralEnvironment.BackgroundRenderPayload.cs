/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderPayload.cs
@module: presentation.pathfinding.worldgen.background_render_payload
@purpose: Builds typed per-cell background render decisions so tilemap writeback, far-view, and future shader backends can consume the same payload.
@entry: PENV-26, ProceduralEnvironment.BuildBackgroundRenderDecision, ProceduralEnvironment.PopulateBackgroundRenderDecisionBlock
@api: partial class implementation for ProceduralEnvironment
@deps: background render resources, terrain layers, biome masks, tile-variant selection, tilemap writeback
@data: per-cell biome kind, shared-palette usage, resolved tile/transform decisions, and placement cache updates
@perf: hot path; centralizes cell-decision logic to eliminate duplicated sync/coroutine render selection code
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: background rendering, shared tiles, water-edge matching, and anti-repeat settings in ProceduralEnvironment
@assets: background tilemap tiles and runtime-converted variants
@notes: keep this payload renderer-agnostic so tilemaps, far-view, and shader/instancing paths can share one decision layer
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDRENDERPAYLOAD]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderPayload.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private enum BackgroundRenderBiomeKind
        {
            Land,
            Water,
            Rock
        }

        private struct BackgroundRenderCellDecision
        {
            public int GlobalIndex;
            public int Col;
            public int Row;
            public int LayerIndex;
            public BackgroundRenderBiomeKind BiomeKind;
            public bool IsWaterInterior;
            public bool UsedSharedPalette;
            public bool UsesVariantPlacement;
            public TileBase Tile;
            public Matrix4x4 Transform;
            public EdgeProfile Profile;
            public int VariantId;
        }

        private static BackgroundRenderBiomeKind ResolveBackgroundBiomeKind(bool isWater, bool isRock)
        {
            if (isWater) return BackgroundRenderBiomeKind.Water;
            if (isRock) return BackgroundRenderBiomeKind.Rock;
            return BackgroundRenderBiomeKind.Land;
        }

        private BackgroundRenderCellDecision BuildBackgroundRenderDecision(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            BackgroundRenderResources resources,
            int col,
            int row,
            int globalIdx)
        {
            int layerIdx = layerIndex[globalIdx];
            var decision = new BackgroundRenderCellDecision
            {
                GlobalIndex = globalIdx,
                Col = col,
                Row = row,
                LayerIndex = layerIdx,
                BiomeKind = BackgroundRenderBiomeKind.Land,
                Transform = Matrix4x4.identity,
                VariantId = int.MinValue
            };

            if (layerIdx < 0 || layerIdx >= layers.Count)
                return decision;

            bool isWater = resources.UseWaterBiome && resources.WaterMask != null && resources.WaterMask[globalIdx];
            bool isWaterHole = !isWater && resources.UseWaterBiome && IsMaskHole(resources.WaterMask, width, height, col, row);
            if (isWaterHole)
                isWater = true;

            bool isRock = !isWater && resources.RockMask != null && resources.RockMask[globalIdx];
            bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(resources.WaterMask, width, height, col, row));
            bool useSharedNow = !isWater && !isRock
                && resources.UseSharedTiles
                && resources.SharedTiles != null
                && resources.SharedTiles.Length > 0
                && ShouldUseSharedTiles(col, row, layerIdx, tileSeed, SharedGroundTileChance);

            decision.BiomeKind = ResolveBackgroundBiomeKind(isWater, isRock);
            decision.IsWaterInterior = isWaterInterior;
            decision.UsedSharedPalette = useSharedNow;

            if (!resources.UseVariants)
            {
                TileBase[] palette;
                if (isWater && resources.WaterTiles != null && resources.WaterTiles.Length > 0)
                {
                    palette = (isWaterInterior && resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0)
                        ? resources.WaterInteriorTiles
                        : resources.WaterTiles;
                }
                else if (isRock && resources.RockTiles != null && resources.RockTiles.Length > 0)
                {
                    palette = resources.RockTiles;
                }
                else
                {
                    palette = useSharedNow ? resources.SharedTiles : resources.LayerTiles[layerIdx];
                }

                if (palette == null || palette.Length == 0)
                    return decision;

                var picked = PickTileDeterministic(palette, col, row, tileSeed);
                if (!isWater && !isRock && (resources.WaterSet != null || resources.RockSet != null))
                {
                    bool disallowed = (resources.WaterSet != null && resources.WaterSet.Contains(picked))
                        || (resources.RockSet != null && resources.RockSet.Contains(picked));
                    if (disallowed && resources.LandTiles != null && resources.LandTiles.Length > 0)
                        picked = PickTileDeterministic(resources.LandTiles, col, row, tileSeed);
                }

                if (!isWater && !isRock && IsTileExcludedByNameOrSprite(picked, resources.WaterExclude))
                {
                    if (resources.LandTiles != null && resources.LandTiles.Length > 0)
                        picked = PickTileDeterministic(resources.LandTiles, col, row, tileSeed);
                    else
                        picked = PickTileDeterministicExcluding(palette, col, row, tileSeed, resources.WaterExclude);
                }

                decision.Tile = picked;
                return decision;
            }

            TileVariant[] variants;
            if (isWater && resources.WaterVariants != null && resources.WaterVariants.Length > 0)
            {
                variants = (isWaterInterior && resources.WaterInteriorVariants != null && resources.WaterInteriorVariants.Length > 0)
                    ? resources.WaterInteriorVariants
                    : resources.WaterVariants;
            }
            else if (isRock && resources.RockVariants != null && resources.RockVariants.Length > 0)
            {
                variants = resources.RockVariants;
            }
            else
            {
                variants = useSharedNow && resources.SharedVariants != null && resources.SharedVariants.Length > 0
                    ? resources.SharedVariants
                    : resources.LayerVariants[layerIdx];
            }

            if (variants == null || variants.Length == 0)
                return decision;

            decision.UsesVariantPlacement = true;

            bool requireLeftGreen = false;
            bool requireBottomGreen = false;
            int requireLeftWater = 0;
            int requireBottomWater = 0;
            int requireRightWater = 0;
            int requireTopWater = 0;
            byte leftWaterMask = 0;
            byte bottomWaterMask = 0;
            bool hasLeftWaterMask = false;
            bool hasBottomWaterMask = false;
            int leftId = int.MinValue;
            int bottomId = int.MinValue;

            if (resources.PlacedValid != null)
            {
                if (col > 0)
                {
                    int leftIdx = globalIdx - 1;
                    if (resources.PlacedValid[leftIdx])
                    {
                        if (resources.PlacedProfiles != null && resources.PlacedProfiles[leftIdx].RightGreen)
                            requireLeftGreen = true;
                        if (resources.PlacedProfiles != null)
                        {
                            hasLeftWaterMask = true;
                            leftWaterMask = resources.PlacedProfiles[leftIdx].RightWaterMask;
                        }
                        if (resources.PlacedVariantIds != null)
                            leftId = resources.PlacedVariantIds[leftIdx];
                    }
                }

                if (row > 0)
                {
                    int bottomIdx = globalIdx - width;
                    if (resources.PlacedValid[bottomIdx])
                    {
                        if (resources.PlacedProfiles != null && resources.PlacedProfiles[bottomIdx].TopGreen)
                            requireBottomGreen = true;
                        if (resources.PlacedProfiles != null)
                        {
                            hasBottomWaterMask = true;
                            bottomWaterMask = resources.PlacedProfiles[bottomIdx].TopWaterMask;
                        }
                        if (resources.PlacedVariantIds != null)
                            bottomId = resources.PlacedVariantIds[bottomIdx];
                    }
                }
            }

            bool enforceEdge = UseGroundTileEdgeColorMatch && !isWater && !isRock;
            bool enforceWaterEdge = false;
            if (UseWaterEdgeColorMatch && resources.WaterMask != null)
            {
                bool leftWater = col > 0 && IsWaterCell(resources.WaterMask, width, height, col - 1, row);
                bool bottomWater = row > 0 && IsWaterCell(resources.WaterMask, width, height, col, row - 1);
                bool rightWater = col < width - 1 && IsWaterCell(resources.WaterMask, width, height, col + 1, row);
                bool topWater = row < height - 1 && IsWaterCell(resources.WaterMask, width, height, col, row + 1);
                bool hasWaterNeighbor = leftWater || bottomWater || rightWater || topWater;

                enforceWaterEdge = isWater || isRock || hasWaterNeighbor;
                if (enforceWaterEdge)
                {
                    if (col > 0)
                        requireLeftWater = leftWater ? 1 : -1;
                    if (row > 0)
                        requireBottomWater = bottomWater ? 1 : -1;
                    if (col < width - 1)
                        requireRightWater = rightWater ? 1 : -1;
                    if (row < height - 1)
                        requireTopWater = topWater ? 1 : -1;
                }
            }

            TileVariant variant = PickVariantWithConstraints(
                variants,
                requireLeftGreen,
                requireBottomGreen,
                requireLeftWater,
                requireBottomWater,
                requireRightWater,
                requireTopWater,
                enforceEdge,
                enforceWaterEdge,
                UseGroundTileAntiRepeat,
                leftId,
                bottomId,
                leftWaterMask,
                hasLeftWaterMask,
                bottomWaterMask,
                hasBottomWaterMask,
                col,
                row,
                tileSeed);

            if (variant.Tile == null && useSharedNow)
            {
                var fallback = resources.LayerVariants[layerIdx];
                if (fallback != null && fallback.Length > 0)
                {
                    variant = PickVariantWithConstraints(
                        fallback,
                        requireLeftGreen,
                        requireBottomGreen,
                        requireLeftWater,
                        requireBottomWater,
                        requireRightWater,
                        requireTopWater,
                        enforceEdge,
                        enforceWaterEdge,
                        UseGroundTileAntiRepeat,
                        leftId,
                        bottomId,
                        leftWaterMask,
                        hasLeftWaterMask,
                        bottomWaterMask,
                        hasBottomWaterMask,
                        col,
                        row,
                        tileSeed);
                }
            }

            if (!isWater && !isRock && IsTileExcludedByNameOrSprite(variant.Tile, resources.WaterExclude))
            {
                if (resources.LandVariants != null && resources.LandVariants.Length > 0)
                {
                    variant = PickVariantWithConstraints(
                        resources.LandVariants,
                        requireLeftGreen,
                        requireBottomGreen,
                        requireLeftWater,
                        requireBottomWater,
                        requireRightWater,
                        requireTopWater,
                        enforceEdge,
                        enforceWaterEdge,
                        UseGroundTileAntiRepeat,
                        leftId,
                        bottomId,
                        leftWaterMask,
                        hasLeftWaterMask,
                        bottomWaterMask,
                        hasBottomWaterMask,
                        col,
                        row,
                        tileSeed);
                }
                else
                {
                    variant = PickVariantDeterministicExcluding(variants, col, row, tileSeed, resources.WaterExclude);
                }
            }

            if (!isWater && !isRock && variant.Tile != null && (resources.WaterSet != null || resources.RockSet != null))
            {
                bool disallowed = (resources.WaterSet != null && resources.WaterSet.Contains(variant.Tile))
                    || (resources.RockSet != null && resources.RockSet.Contains(variant.Tile));
                if (disallowed && resources.LandVariants != null && resources.LandVariants.Length > 0)
                {
                    variant = PickVariantWithConstraints(
                        resources.LandVariants,
                        requireLeftGreen,
                        requireBottomGreen,
                        requireLeftWater,
                        requireBottomWater,
                        requireRightWater,
                        requireTopWater,
                        enforceEdge,
                        enforceWaterEdge,
                        UseGroundTileAntiRepeat,
                        leftId,
                        bottomId,
                        leftWaterMask,
                        hasLeftWaterMask,
                        bottomWaterMask,
                        hasBottomWaterMask,
                        col,
                        row,
                        tileSeed);
                }
            }

            decision.Tile = variant.Tile;
            decision.Transform = variant.Transform;
            decision.Profile = variant.Profile;
            decision.VariantId = variant.Id;
            return decision;
        }

        private void CacheBackgroundRenderPlacement(BackgroundRenderResources resources, BackgroundRenderCellDecision decision)
        {
            if (!decision.UsesVariantPlacement || resources.PlacedValid == null)
                return;

            if (resources.PlacedProfiles != null)
                resources.PlacedProfiles[decision.GlobalIndex] = decision.Profile;
            resources.PlacedValid[decision.GlobalIndex] = decision.Tile != null;
            if (resources.PlacedVariantIds != null)
                resources.PlacedVariantIds[decision.GlobalIndex] = decision.VariantId;
        }

        private void PopulateBackgroundRenderDecisionBlock(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            BackgroundRenderResources resources,
            int startRow,
            int rowCount,
            BackgroundRenderCellDecision[] decisions)
        {
            int blockIndex = 0;
            for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
            {
                int row = startRow + rowOffset;
                for (int col = 0; col < width; col++)
                {
                    int globalIdx = (row * width) + col;
                    var decision = BuildBackgroundRenderDecision(width, height, layers, tileSeed, layerIndex, resources, col, row, globalIdx);
                    decisions[blockIndex++] = decision;
                    CacheBackgroundRenderPlacement(resources, decision);
                }
            }
        }

        private void ApplyBackgroundRenderDecisionBlock(
            BackgroundRenderResources resources,
            BackgroundRenderCellDecision[] decisions,
            TileBase[] tiles,
            Matrix4x4[] transforms)
        {
            for (int i = 0; i < decisions.Length; i++)
            {
                var decision = decisions[i];
                tiles[i] = decision.Tile;
                if (transforms != null)
                    transforms[i] = decision.Transform;
                if (resources.RefineTiles != null)
                    resources.RefineTiles[decision.GlobalIndex] = decision.Tile;
                if (resources.RefineTransforms != null)
                    resources.RefineTransforms[decision.GlobalIndex] = decision.Transform;
            }
        }

        private void CaptureStreamBackgroundRenderPayload(
            ref StreamChunkState state,
            BoundsInt bounds,
            TileBase[] tiles,
            Matrix4x4[] transforms,
            StreamChunkWorldData chunkData)
        {
            state.BackgroundPayload = null;
            state.BackgroundCellCount = 0;

            if (_background == null
                || tiles == null
                || bounds.size.x <= 0
                || bounds.size.y <= 0
                || tiles.Length != bounds.size.x * bounds.size.y)
            {
                return;
            }

            var decisions = new BackgroundRenderCellDecision[tiles.Length];
            int nonEmptyCount = 0;
            int idx = 0;
            for (int y = 0; y < bounds.size.y; y++)
            {
                int row = bounds.yMin + y;
                for (int x = 0; x < bounds.size.x; x++)
                {
                    int col = bounds.xMin + x;
                    var tile = tiles[idx];
                    var transform = transforms != null ? transforms[idx] : Matrix4x4.identity;
                    var biomeData = ResolveBackgroundBiomeData(chunkData, col, row);
                    decisions[idx] = new BackgroundRenderCellDecision
                    {
                        GlobalIndex = ResolveStreamBackgroundGlobalIndex(col, row, idx),
                        Col = col,
                        Row = row,
                        LayerIndex = -1,
                        BiomeKind = ResolveBackgroundBiomeKind(biomeData.IsWater, biomeData.IsRock),
                        IsWaterInterior = biomeData.IsWaterInterior,
                        UsesVariantPlacement = transforms != null && transform != default && transform != Matrix4x4.identity,
                        Tile = tile,
                        Transform = transform == default ? Matrix4x4.identity : transform,
                        VariantId = int.MinValue
                    };
                    if (tile != null)
                        nonEmptyCount++;

                    idx++;
                }
            }

            if (nonEmptyCount <= 0)
                return;

            state.BackgroundPayload = decisions;
            state.BackgroundCellCount = nonEmptyCount;
        }

        private int ResolveStreamBackgroundGlobalIndex(int col, int row, int fallbackIndex)
        {
            if (_backgroundMaskWidth > 0 && _backgroundMaskHeight > 0 && col >= 0 && row >= 0 && col < _backgroundMaskWidth && row < _backgroundMaskHeight)
                return (row * _backgroundMaskWidth) + col;

            return fallbackIndex;
        }

        private void WriteBackgroundTileBlock(
            int width,
            int startRow,
            int rowCount,
            TileBase[] tiles,
            Matrix4x4[] transforms)
        {
            var bounds = new BoundsInt(0, startRow, 0, width, rowCount, 1);
            _background.SetTilesBlock(bounds, tiles);
            if (transforms == null)
                return;

            Matrix4x4 identity = Matrix4x4.identity;
            for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
            {
                int row = startRow + rowOffset;
                int rowBase = rowOffset * width;
                for (int col = 0; col < width; col++)
                {
                    int blockIdx = rowBase + col;
                    if (tiles[blockIdx] == null) continue;
                    var transform = transforms[blockIdx];
                    if (transform == default || transform == identity)
                        transform = GetTileTransform(tiles[blockIdx]);
                    _background.SetTransformMatrix(new Vector3Int(col, row, 0), transform);
                }
            }
        }
    }
}
