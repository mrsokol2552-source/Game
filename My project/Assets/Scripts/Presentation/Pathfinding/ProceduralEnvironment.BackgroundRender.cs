/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRender.cs
@module: presentation.pathfinding.worldgen.background_render
@purpose: Hosts background ruleset rendering, water-edge refinement, and background tile mapping outside the ProceduralEnvironment monolith.
@entry: PENV-25, ProceduralEnvironment.ApplyRulesetToBackground, ProceduralEnvironment.ApplyRulesetToBackgroundRoutine
@api: partial class implementation for ProceduralEnvironment
@deps: background render resources, terrain layers, runtime tile conversion, biome masks, tilemap writeback
@data: prepared background palettes, placed edge profiles, refinement buffers, tile transforms
@perf: hot path; keeps render loops localized so future shader/instancing work can swap writeback without re-reading monolith state
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: background rendering, water-edge refinement, shared tiles, and runtime ground conversion settings in ProceduralEnvironment
@assets: background tilemap tiles, runtime-converted ground tiles
@notes: keep tile-selection logic here and let future render backends consume extracted payloads from BackgroundRenderData instead of ProceduralEnvironment.cs
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDRENDER]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRender.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void RefineWaterEdgesRect(
            int width,
            int height,
            bool[] waterMask,
            bool[] rockMask,
            int[] layerIndex,
            TileVariant[][] layerVariants,
            TileVariant[] waterVariants,
            TileVariant[] waterInteriorVariants,
            TileVariant[] rockVariants,
            TileVariant[] sharedVariants,
            int tileSeed,
            TileBase[] tiles,
            Matrix4x4[] transforms,
            EdgeProfile[] placedProfiles,
            bool[] placedValid,
            int[] placedVariantIds)
        {
            RefineWaterEdgesRectCore(
                width,
                height,
                waterMask,
                rockMask,
                layerIndex,
                layerVariants,
                waterVariants,
                waterInteriorVariants,
                rockVariants,
                sharedVariants,
                tileSeed,
                tiles,
                transforms,
                placedProfiles,
                placedValid,
                placedVariantIds);
        }

        private float GetWaterMaskMismatchScoreAll(
            EdgeProfile profile,
            byte leftMask,
            bool hasLeftMask,
            byte rightMask,
            bool hasRightMask,
            byte bottomMask,
            bool hasBottomMask,
            byte topMask,
            bool hasTopMask)
        {
            return GetWaterMaskMismatchScoreAllCore(
                profile,
                leftMask,
                hasLeftMask,
                rightMask,
                hasRightMask,
                bottomMask,
                hasBottomMask,
                topMask,
                hasTopMask);
        }

        private void ApplyRulesetToBackground(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            Dictionary<TileBase, TileBase> backgroundLookup)
        {
            ApplyRulesetToBackgroundCore(width, height, layers, tileSeed, layerIndex, backgroundLookup);
        }

        private IEnumerator ApplyRulesetToBackgroundRoutine(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            int[] layerIndex,
            Dictionary<TileBase, TileBase> backgroundLookup)
        {
            return ApplyRulesetToBackgroundRoutineCore(width, height, layers, tileSeed, layerIndex, backgroundLookup);
        }

        private TileBase MapBackgroundTileCore(TileBase tile, Dictionary<TileBase, TileBase> lookup)
                {
                    if (tile == null) return null;
                    if (lookup != null && lookup.TryGetValue(tile, out var mapped) && mapped != null)
                        return mapped;
                    if (ConvertGroundTilesRuntime)
                        return GetOrCreateRuntimeGroundTile(tile);
                    return tile;
                }

        private void RefineWaterEdgesRectCore(
                    int width,
                    int height,
                    bool[] waterMask,
                    bool[] rockMask,
                    int[] layerIndex,
                    TileVariant[][] layerVariants,
                    TileVariant[] waterVariants,
                    TileVariant[] waterInteriorVariants,
                    TileVariant[] rockVariants,
                    TileVariant[] sharedVariants,
                    int tileSeed,
                    TileBase[] tiles,
                    Matrix4x4[] transforms,
                    EdgeProfile[] placedProfiles,
                    bool[] placedValid,
                    int[] placedVariantIds)
                {
                    if (!UseWaterEdgeRefinement || !UseWaterEdgeColorMatch) return;
                    if (waterMask == null || layerIndex == null || layerVariants == null) return;
                    if (placedProfiles == null || placedValid == null || placedVariantIds == null) return;
                    if (tiles == null || tiles.Length == 0) return;
                    int passes = Mathf.Clamp(WaterEdgeRefinePasses, 0, 4);
                    if (passes <= 0) return;
        
                    bool useSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f;
                    Matrix4x4 identity = Matrix4x4.identity;
        
                    for (int pass = 0; pass < passes; pass++)
                    {
                        for (int row = 0; row < height; row++)
                        {
                            for (int col = 0; col < width; col++)
                            {
                                int idx = (row * width) + col;
                                if (!placedValid[idx]) continue;
        
                                bool isWater = waterMask[idx];
                                bool isWaterHole = !isWater && IsMaskHole(waterMask, width, height, col, row);
                                if (isWaterHole) isWater = true;
                                bool isRock = !isWater && rockMask != null && rockMask[idx];
                                bool isWaterInterior = isWater && (isWaterHole || IsMaskInterior(waterMask, width, height, col, row));
        
                                bool leftWater = col > 0 && IsWaterCell(waterMask, width, height, col - 1, row);
                                bool rightWater = col < width - 1 && IsWaterCell(waterMask, width, height, col + 1, row);
                                bool bottomWater = row > 0 && IsWaterCell(waterMask, width, height, col, row - 1);
                                bool topWater = row < height - 1 && IsWaterCell(waterMask, width, height, col, row + 1);
                                bool hasWaterNeighbor = leftWater || rightWater || bottomWater || topWater;
                                bool hasLandNeighbor = !leftWater || !rightWater || !bottomWater || !topWater;
                                if (!hasWaterNeighbor) continue;
                                if (isWater && !hasLandNeighbor) continue;
        
                                int layerIdx = layerIndex[idx];
                                if (layerIdx < 0 || layerIdx >= layerVariants.Length) continue;
        
                                bool useSharedNow = !isWater && !isRock && useSharedTiles && sharedVariants != null && sharedVariants.Length > 0
                                    && ShouldUseSharedTiles(col, row, layerIdx, tileSeed, SharedGroundTileChance);
        
                                TileVariant[] variants;
                                if (isWater && waterVariants != null && waterVariants.Length > 0)
                                    variants = (isWaterInterior && waterInteriorVariants != null && waterInteriorVariants.Length > 0)
                                        ? waterInteriorVariants
                                        : waterVariants;
                                else if (isRock && rockVariants != null && rockVariants.Length > 0)
                                    variants = rockVariants;
                                else
                                    variants = useSharedNow && sharedVariants != null && sharedVariants.Length > 0
                                        ? sharedVariants
                                        : layerVariants[layerIdx];
                                if (variants == null || variants.Length == 0)
                                    continue;
        
                                bool requireLeftGreen = false;
                                bool requireBottomGreen = false;
                                int requireLeftWater = 0;
                                int requireBottomWater = 0;
                                int requireRightWater = 0;
                                int requireTopWater = 0;
                                byte leftMask = 0;
                                byte rightMask = 0;
                                byte bottomMask = 0;
                                byte topMask = 0;
                                bool hasLeftMask = false;
                                bool hasRightMask = false;
                                bool hasBottomMask = false;
                                bool hasTopMask = false;
                                int leftId = int.MinValue;
                                int bottomId = int.MinValue;
        
                                if (col > 0 && placedValid[idx - 1])
                                {
                                    var leftProfile = placedProfiles[idx - 1];
                                    requireLeftGreen = leftProfile.RightGreen;
                                    leftId = placedVariantIds[idx - 1];
                                    if (isWater != leftWater)
                                    {
                                        hasLeftMask = true;
                                        leftMask = leftProfile.RightWaterMask;
                                    }
                                }
                                if (col < width - 1 && placedValid[idx + 1])
                                {
                                    var rightProfile = placedProfiles[idx + 1];
                                    if (isWater != rightWater)
                                    {
                                        hasRightMask = true;
                                        rightMask = rightProfile.LeftWaterMask;
                                    }
                                }
                                if (row > 0 && placedValid[idx - width])
                                {
                                    var bottomProfile = placedProfiles[idx - width];
                                    requireBottomGreen = bottomProfile.TopGreen;
                                    bottomId = placedVariantIds[idx - width];
                                    if (isWater != bottomWater)
                                    {
                                        hasBottomMask = true;
                                        bottomMask = bottomProfile.TopWaterMask;
                                    }
                                }
                                if (row < height - 1 && placedValid[idx + width])
                                {
                                    var topProfile = placedProfiles[idx + width];
                                    if (isWater != topWater)
                                    {
                                        hasTopMask = true;
                                        topMask = topProfile.BottomWaterMask;
                                    }
                                }
        
                                if (col > 0) requireLeftWater = leftWater ? 1 : -1;
                                if (row > 0) requireBottomWater = bottomWater ? 1 : -1;
                                if (col < width - 1) requireRightWater = rightWater ? 1 : -1;
                                if (row < height - 1) requireTopWater = topWater ? 1 : -1;
        
                                bool enforceEdge = UseGroundTileEdgeColorMatch && !isWater && !isRock;
                                bool enforceAntiRepeat = UseGroundTileAntiRepeat;
        
                                float bestScore = float.MaxValue;
                                TileVariant best = default;
                                for (int i = 0; i < variants.Length; i++)
                                {
                                    var variant = variants[i];
                                    if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                        continue;
                                    if (enforceEdge)
                                    {
                                        if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                        if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                                    }
                                    if (enforceAntiRepeat)
                                    {
                                        if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                        if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                                    }
        
                                    float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                                    score += GetWaterMaskMismatchScoreAllCore(
                                        variant.Profile,
                                        leftMask,
                                        hasLeftMask,
                                        rightMask,
                                        hasRightMask,
                                        bottomMask,
                                        hasBottomMask,
                                        topMask,
                                        hasTopMask);
        
                                    if (score < bestScore)
                                    {
                                        bestScore = score;
                                        best = variant;
                                        if (bestScore <= 0f)
                                            break;
                                    }
                                }
        
                                if (best.Tile != null)
                                {
                                    float tolerance = Mathf.Clamp01(WaterEdgeMismatchTolerance);
                                    float threshold = bestScore + tolerance;
                                    int hash = (col * 73856093) ^ (row * 19349663) ^ tileSeed ^ (pass * 83492791);
                                    if (hash < 0) hash = -hash;
                                    int candidateCount = 0;
                                    for (int i = 0; i < variants.Length; i++)
                                    {
                                        var variant = variants[i];
                                        if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                            continue;
                                        if (enforceEdge)
                                        {
                                            if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                            if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                                        }
                                        if (enforceAntiRepeat)
                                        {
                                            if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                            if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                                        }
        
                                        float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                                        score += GetWaterMaskMismatchScoreAllCore(
                                            variant.Profile,
                                            leftMask,
                                            hasLeftMask,
                                            rightMask,
                                            hasRightMask,
                                            bottomMask,
                                            hasBottomMask,
                                            topMask,
                                            hasTopMask);
        
                                        if (score <= threshold)
                                            candidateCount++;
                                    }
        
                                    if (candidateCount > 0)
                                    {
                                        int pick = hash % candidateCount;
                                        for (int i = 0; i < variants.Length; i++)
                                        {
                                            var variant = variants[i];
                                            if (!MatchesWaterEdgeRequirement(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater))
                                                continue;
                                            if (enforceEdge)
                                            {
                                                if (requireLeftGreen && !variant.Profile.LeftGreen) continue;
                                                if (requireBottomGreen && !variant.Profile.BottomGreen) continue;
                                            }
                                            if (enforceAntiRepeat)
                                            {
                                                if (GroundTileAntiRepeatLeft && leftId != int.MinValue && variant.Id == leftId) continue;
                                                if (GroundTileAntiRepeatBottom && bottomId != int.MinValue && variant.Id == bottomId) continue;
                                            }
        
                                            float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater);
                                            score += GetWaterMaskMismatchScoreAllCore(
                                                variant.Profile,
                                                leftMask,
                                                hasLeftMask,
                                                rightMask,
                                                hasRightMask,
                                                bottomMask,
                                                hasBottomMask,
                                                topMask,
                                                hasTopMask);
        
                                            if (score > threshold) continue;
                                            if (pick-- == 0)
                                            {
                                                best = variant;
                                                break;
                                            }
                                        }
                                    }
        
                                    if (best.Tile != null && best.Id != placedVariantIds[idx])
                                    {
                                        tiles[idx] = best.Tile;
                                        if (transforms != null)
                                            transforms[idx] = best.Transform == default ? identity : best.Transform;
                                        placedProfiles[idx] = best.Profile;
                                        placedVariantIds[idx] = best.Id;
                                    }
                                }
                            }
                        }
                    }
                }

        private float GetWaterMaskMismatchScoreAllCore(
                    EdgeProfile profile,
                    byte leftMask,
                    bool hasLeftMask,
                    byte rightMask,
                    bool hasRightMask,
                    byte bottomMask,
                    bool hasBottomMask,
                    byte topMask,
                    bool hasTopMask)
                {
                    float weight = Mathf.Max(0f, WaterEdgeMaskMatchWeight);
                    if (weight <= 0f) return 0f;
                    int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
                    float score = 0f;
                    if (hasLeftMask)
                        score += CountMaskDifference(profile.LeftWaterMask, leftMask, samples) / (float)samples;
                    if (hasRightMask)
                        score += CountMaskDifference(profile.RightWaterMask, rightMask, samples) / (float)samples;
                    if (hasBottomMask)
                        score += CountMaskDifference(profile.BottomWaterMask, bottomMask, samples) / (float)samples;
                    if (hasTopMask)
                        score += CountMaskDifference(profile.TopWaterMask, topMask, samples) / (float)samples;
                    score *= weight;
        
                    float smoothWeight = Mathf.Max(0f, WaterEdgeSmoothnessWeight);
                    if (smoothWeight > 0f)
                    {
                        float denom = Mathf.Max(1f, samples - 1f);
                        if (hasLeftMask)
                            score += (profile.LeftWaterTransitions / denom) * smoothWeight;
                        if (hasRightMask)
                            score += (profile.RightWaterTransitions / denom) * smoothWeight;
                        if (hasBottomMask)
                            score += (profile.BottomWaterTransitions / denom) * smoothWeight;
                        if (hasTopMask)
                            score += (profile.TopWaterTransitions / denom) * smoothWeight;
                    }
                    return score;
                }

                private void ApplyRulesetToBackgroundCore(
                    int width,
                    int height,
                    List<TerrainLayer> layers,
                    int tileSeed,
                    int[] layerIndex,
                    Dictionary<TileBase, TileBase> backgroundLookup)
                {
                    if (_background == null || layers == null || layerIndex == null) return;
                    CacheBackgroundRenderInput(width, height, layers, tileSeed, layerIndex, backgroundLookup);
                    var resources = BuildBackgroundRenderResources(width, height, layers, tileSeed, backgroundLookup, allocateRefineBuffers: false);
                    var decisions = new BackgroundRenderCellDecision[resources.Size];
                    PopulateBackgroundRenderDecisionBlock(width, height, layers, tileSeed, layerIndex, resources, 0, height, decisions);

                    var tiles = new TileBase[resources.Size];
                    Matrix4x4[] transforms = resources.UseTransform ? new Matrix4x4[resources.Size] : null;
                    ApplyBackgroundRenderDecisionBlock(resources, decisions, tiles, transforms);

                    RefineWaterEdgesRectCore(
                        width,
                        height,
                        resources.WaterMask,
                        resources.RockMask,
                        layerIndex,
                        resources.LayerVariants,
                        resources.WaterVariants,
                        resources.WaterInteriorVariants,
                        resources.RockVariants,
                        resources.SharedVariants,
                        tileSeed,
                        tiles,
                        transforms,
                        resources.PlacedProfiles,
                        resources.PlacedValid,
                        resources.PlacedVariantIds);

                    WriteBackgroundTileBlock(width, 0, height, tiles, transforms);
                    CacheBackgroundBiomeMasks(width, height, resources.WaterMask, resources.RockMask);
                }

                private IEnumerator ApplyRulesetToBackgroundRoutineCore(
                    int width,
                    int height,
                    List<TerrainLayer> layers,
                    int tileSeed,
                    int[] layerIndex,
                    Dictionary<TileBase, TileBase> backgroundLookup)
                {
                    if (_background == null || layers == null || layerIndex == null) yield break;
                    CacheBackgroundRenderInput(width, height, layers, tileSeed, layerIndex, backgroundLookup);
                    int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                    var resources = BuildBackgroundRenderResources(width, height, layers, tileSeed, backgroundLookup, allocateRefineBuffers: true);

                    for (int row = 0; row < height; row += rowsPerFrame)
                    {
                        int rowCount = Mathf.Min(rowsPerFrame, height - row);
                        int blockSize = width * rowCount;
                        var decisions = new BackgroundRenderCellDecision[blockSize];
                        PopulateBackgroundRenderDecisionBlock(width, height, layers, tileSeed, layerIndex, resources, row, rowCount, decisions);

                        var tiles = new TileBase[blockSize];
                        Matrix4x4[] transforms = resources.UseTransform ? new Matrix4x4[blockSize] : null;
                        ApplyBackgroundRenderDecisionBlock(resources, decisions, tiles, transforms);
                        WriteBackgroundTileBlock(width, row, rowCount, tiles, transforms);
                        yield return null;
                    }

                    if (resources.RefineTiles != null)
                    {
                        RefineWaterEdgesRectCore(
                            width,
                            height,
                            resources.WaterMask,
                            resources.RockMask,
                            layerIndex,
                            resources.LayerVariants,
                            resources.WaterVariants,
                            resources.WaterInteriorVariants,
                            resources.RockVariants,
                            resources.SharedVariants,
                            tileSeed,
                            resources.RefineTiles,
                            resources.RefineTransforms,
                            resources.PlacedProfiles,
                            resources.PlacedValid,
                            resources.PlacedVariantIds);

                        WriteBackgroundTileBlock(width, 0, height, resources.RefineTiles, resources.RefineTransforms);
                    }

                    CacheBackgroundBiomeMasks(width, height, resources.WaterMask, resources.RockMask);
                }
    }
}
