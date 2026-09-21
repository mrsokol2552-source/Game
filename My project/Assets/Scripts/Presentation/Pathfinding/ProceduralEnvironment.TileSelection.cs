/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileSelection.cs
@module: presentation.pathfinding.worldgen.tile_selection
@purpose: Hosts ground tile override application, tile variant construction, and constrained variant picking outside the ProceduralEnvironment monolith.
@entry: PENV-42, ProceduralEnvironment.BuildTileVariants, ProceduralEnvironment.PickVariantWithConstraints
@api: partial class implementation for ProceduralEnvironment
@deps: tile data primitives, edge profiles, tile sprite extraction, and water/green edge profile scoring
@data: generated tile variants, rotation/mirror transforms, deterministic variant picks, shared-tile gates, and mismatch scores
@perf: hot during background/streaming tile selection; deterministic hashes avoid per-cell RNG allocation
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: ground tile rotation/mirroring, shared-tile chance, edge color matching, water edge matching, anti-repeat, and ground tile overrides
@assets: source tile assets and runtime-converted ground tiles
@notes: this layer consumes profile data but does not own raw sprite raster/profile extraction
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-TILESELECTION]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileSelection.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void ApplyGroundTileOverrides(
            string spriteName,
            ref float insetPixels,
            ref int edgeTrimPixels,
            ref float edgeBlackThreshold,
            ref float edgeChromaThreshold,
            ref float extraTopInsetPixels,
            ref float extraRightInsetPixels,
            ref float extraBottomInsetPixels,
            ref float extraLeftInsetPixels)
        {
            if (GroundTileOverrides == null || GroundTileOverrides.Length == 0 || string.IsNullOrEmpty(spriteName))
                return;

            for (int i = 0; i < GroundTileOverrides.Length; i++)
            {
                var ov = GroundTileOverrides[i];
                if (ov == null || string.IsNullOrEmpty(ov.NameContains)) continue;
                if (spriteName.IndexOf(ov.NameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                insetPixels = Mathf.Max(0f, insetPixels + ov.ExtraInsetPixels);
                edgeTrimPixels = Mathf.Max(0, edgeTrimPixels + ov.ExtraEdgeTrimPixels);
                if (ov.OverrideEdgeBlackThreshold)
                    edgeBlackThreshold = Mathf.Clamp01(ov.EdgeBlackThresholdOverride);
                if (ov.OverrideEdgeChromaThreshold)
                    edgeChromaThreshold = ov.EdgeChromaThresholdOverride;
                extraTopInsetPixels = Mathf.Max(0f, extraTopInsetPixels + ov.ExtraTopInsetPixels);
                extraRightInsetPixels = Mathf.Max(0f, extraRightInsetPixels + ov.ExtraRightInsetPixels);
                extraBottomInsetPixels = Mathf.Max(0f, extraBottomInsetPixels + ov.ExtraBottomInsetPixels);
                extraLeftInsetPixels = Mathf.Max(0f, extraLeftInsetPixels + ov.ExtraLeftInsetPixels);
            }
        }

        private static bool ShouldUseSharedTiles(int col, int row, int layerIdx, int seed, float chance)
        {
            if (chance <= 0f) return false;
            unchecked
            {
                uint hash = (uint)(col * 73856093) ^ (uint)(row * 19349663) ^ (uint)(layerIdx * 83492791) ^ (uint)seed;
                float v = (hash & 0xFFFFFF) / 16777215f;
                return v < chance;
            }
        }

        private int[] GetRotationSteps()
        {
            if (!UseGroundTileRandomRotation)
                return new[] { 0 };

            var steps = new List<int>(4);
            if (GroundTileRotationInclude0) steps.Add(0);
            if (GroundTileRotationInclude90) steps.Add(1);
            if (GroundTileRotationInclude180) steps.Add(2);
            if (GroundTileRotationInclude270) steps.Add(3);
            if (steps.Count == 0)
                steps.Add(0);
            return steps.ToArray();
        }

        private (bool MirrorX, bool MirrorY)[] GetMirrorOptions()
        {
            if (!GroundTileMirrorX && !GroundTileMirrorY)
                return new[] { (false, false) };

            var options = new List<(bool, bool)>(4) { (false, false) };
            if (GroundTileMirrorX)
                options.Add((true, false));
            if (GroundTileMirrorY)
                options.Add((false, true));
            if (GroundTileMirrorX && GroundTileMirrorY)
                options.Add((true, true));
            return options.ToArray();
        }

        private TileVariant[] BuildTileVariants(TileBase[] tiles)
        {
            bool allowRotation = UseGroundTileRandomRotation;
            bool allowMirroring = GroundTileMirrorX || GroundTileMirrorY;
            return BuildTileVariants(tiles, allowRotation, allowMirroring);
        }

        private TileVariant[] BuildTileVariants(TileBase[] tiles, bool allowRotation, bool allowMirroring)
        {
            if (tiles == null || tiles.Length == 0)
                return Array.Empty<TileVariant>();

            int[] steps = allowRotation ? GetRotationSteps() : new[] { 0 };
            var mirrorOptions = allowMirroring ? GetMirrorOptions() : new[] { (false, false) };
            var variants = new List<TileVariant>(tiles.Length * steps.Length * mirrorOptions.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                var baseProfile = GetEdgeProfile(tile);
                var unique = new HashSet<MatrixKey>();
                for (int m = 0; m < mirrorOptions.Length; m++)
                {
                    var (mirrorX, mirrorY) = mirrorOptions[m];
                    var mirroredProfile = baseProfile;
                    if (mirrorX)
                        mirroredProfile = MirrorProfileX(mirroredProfile);
                    if (mirrorY)
                        mirroredProfile = MirrorProfileY(mirroredProfile);

                    for (int s = 0; s < steps.Length; s++)
                    {
                        int step = steps[s];
                        float angle = step * 90f;
                        var scale = new Vector3(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f, 1f);
                        var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), scale);
                        var key = MatrixKey.From(transform);
                        if (!unique.Add(key)) continue;

                        var profile = RotateProfileCCW(mirroredProfile, step);
                        int id = ((tile.GetInstanceID() * 397) ^ key.GetHashCode());
                        if (id == int.MinValue)
                            id = int.MaxValue;
                        variants.Add(new TileVariant
                        {
                            Tile = tile,
                            Transform = transform,
                            Profile = profile,
                            Id = id
                        });
                    }
                }
            }
            return variants.ToArray();
        }

        private static TileBase PickLayerBaseTile(TerrainLayer layer, int col, int row, int seed)
        {
            if (layer == null || layer.BaseTiles == null || layer.BaseTiles.Length == 0)
                return null;
            return PickTileDeterministic(layer.BaseTiles, col, row, seed);
        }

        private TileVariant PickVariantDeterministic(TileVariant[] variants, int col, int row, int seed)
        {
            if (variants == null || variants.Length == 0)
                return default;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            return variants[hash % variants.Length];
        }

        private TileVariant PickVariantDeterministicExcluding(
            TileVariant[] variants,
            int col,
            int row,
            int seed,
            string[] excludeKeywords)
        {
            if (variants == null || variants.Length == 0)
                return default;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return PickVariantDeterministic(variants, col, row, seed);
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            int start = hash % variants.Length;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[(start + i) % variants.Length];
                if (variant.Tile == null) continue;
                if (!IsTileExcludedByNameOrSprite(variant.Tile, excludeKeywords))
                    return variant;
            }
            return variants[start];
        }

        private TileVariant PickVariantWithConstraints(
            TileVariant[] variants,
            bool requireLeftGreen,
            bool requireBottomGreen,
            int requireLeftWater,
            int requireBottomWater,
            int requireRightWater,
            int requireTopWater,
            bool enforceEdge,
            bool enforceWaterEdge,
            bool enforceAntiRepeat,
            int leftId,
            int bottomId,
            byte leftWaterMask,
            bool hasLeftWaterMask,
            byte bottomWaterMask,
            bool hasBottomWaterMask,
            int col,
            int row,
            int seed)
        {
            if (variants == null || variants.Length == 0)
                return default;

            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            bool needEdge = enforceEdge && (requireLeftGreen || requireBottomGreen);
            bool needWaterEdge = enforceWaterEdge && (requireLeftWater != 0 || requireBottomWater != 0 || requireRightWater != 0 || requireTopWater != 0);
            bool needWaterEdgeLeftBottom = enforceWaterEdge && (requireLeftWater != 0 || requireBottomWater != 0);
            bool needAnti = enforceAntiRepeat && ((GroundTileAntiRepeatLeft && leftId != int.MinValue) || (GroundTileAntiRepeatBottom && bottomId != int.MinValue));

            var variant = TryPickVariant(variants, hash, needEdge, needWaterEdge, needAnti, requireLeftGreen, requireBottomGreen, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
            if (variant.Tile != null) return variant;

            if (needAnti)
            {
                variant = TryPickVariant(variants, hash, needEdge, needWaterEdge, false, requireLeftGreen, requireBottomGreen, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needWaterEdge)
            {
                variant = TryPickVariant(variants, hash, false, needWaterEdge, false, false, false, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needWaterEdge && (requireRightWater != 0 || requireTopWater != 0))
            {
                variant = TryPickVariant(variants, hash, false, needWaterEdgeLeftBottom, false, false, false, requireLeftWater, requireBottomWater, 0, 0, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            if (needEdge)
            {
                variant = TryPickVariant(variants, hash, needEdge, false, false, requireLeftGreen, requireBottomGreen, 0, 0, 0, 0, leftId, bottomId, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                if (variant.Tile != null) return variant;
            }

            return PickVariantDeterministic(variants, col, row, seed);
        }

        private TileVariant TryPickVariant(
            TileVariant[] variants,
            int hash,
            bool enforceEdge,
            bool enforceWaterEdge,
            bool enforceAntiRepeat,
            bool requireLeftGreen,
            bool requireBottomGreen,
            int requireLeftWater,
            int requireBottomWater,
            int requireRightWater,
            int requireTopWater,
            int leftId,
            int bottomId,
            byte leftWaterMask,
            bool hasLeftWaterMask,
            byte bottomWaterMask,
            bool hasBottomWaterMask)
        {
            if (enforceWaterEdge)
            {
                float bestScore = float.MaxValue;
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

                    float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                        + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                    if (score < bestScore)
                        bestScore = score;
                }

                if (bestScore < float.MaxValue)
                {
                    float tolerance = Mathf.Clamp01(WaterEdgeMismatchTolerance);
                    float threshold = bestScore + tolerance;
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

                        float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                            + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                        if (score <= threshold)
                            candidateCount++;
                    }

                    if (candidateCount > 0)
                    {
                        int candidatePick = hash % candidateCount;
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

                            float score = GetWaterMismatchScore(variant.Profile, requireLeftWater, requireBottomWater, requireRightWater, requireTopWater)
                                + GetWaterMaskMismatchScore(variant.Profile, leftWaterMask, hasLeftWaterMask, bottomWaterMask, hasBottomWaterMask);
                            if (score > threshold) continue;
                            if (candidatePick-- == 0) return variant;
                        }
                    }
                }

                return default;
            }

            int matchCount = 0;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
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
                matchCount++;
            }

            if (matchCount == 0)
                return default;

            int pick = hash % matchCount;
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
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
                if (pick-- == 0) return variant;
            }

            return default;
        }

        private float GetWaterMismatchScore(EdgeProfile profile, int requireLeftWater, int requireBottomWater, int requireRightWater, int requireTopWater)
        {
            float score = 0f;
            if (requireLeftWater != 0)
                score += requireLeftWater > 0 ? (1f - profile.LeftWaterRatio) : profile.LeftWaterRatio;
            if (requireBottomWater != 0)
                score += requireBottomWater > 0 ? (1f - profile.BottomWaterRatio) : profile.BottomWaterRatio;
            if (requireRightWater != 0)
                score += requireRightWater > 0 ? (1f - profile.RightWaterRatio) : profile.RightWaterRatio;
            if (requireTopWater != 0)
                score += requireTopWater > 0 ? (1f - profile.TopWaterRatio) : profile.TopWaterRatio;
            return score;
        }

        private bool MatchesWaterEdgeRequirement(EdgeProfile profile, int requireLeftWater, int requireBottomWater, int requireRightWater, int requireTopWater)
        {
            float waterMin = Mathf.Clamp01(WaterEdgeBlueRatio);
            float landMax = Mathf.Clamp01(WaterEdgeLandMaxRatio);
            if (landMax > waterMin) landMax = waterMin;

            if (requireLeftWater > 0 && profile.LeftWaterRatio < waterMin) return false;
            if (requireLeftWater < 0 && profile.LeftWaterRatio > landMax) return false;
            if (requireBottomWater > 0 && profile.BottomWaterRatio < waterMin) return false;
            if (requireBottomWater < 0 && profile.BottomWaterRatio > landMax) return false;
            if (requireRightWater > 0 && profile.RightWaterRatio < waterMin) return false;
            if (requireRightWater < 0 && profile.RightWaterRatio > landMax) return false;
            if (requireTopWater > 0 && profile.TopWaterRatio < waterMin) return false;
            if (requireTopWater < 0 && profile.TopWaterRatio > landMax) return false;
            return true;
        }

        private float GetWaterMaskMismatchScore(EdgeProfile profile, byte leftMask, bool hasLeftMask, byte bottomMask, bool hasBottomMask)
        {
            float weight = Mathf.Max(0f, WaterEdgeMaskMatchWeight);
            if (weight <= 0f) return 0f;
            int samples = Mathf.Clamp(profile.WaterMaskSamples, 1, 8);
            float score = 0f;
            if (hasLeftMask)
                score += CountMaskDifference(profile.LeftWaterMask, leftMask, samples) / (float)samples;
            if (hasBottomMask)
                score += CountMaskDifference(profile.BottomWaterMask, bottomMask, samples) / (float)samples;
            score *= weight;

            float smoothWeight = Mathf.Max(0f, WaterEdgeSmoothnessWeight);
            if (smoothWeight > 0f)
            {
                float denom = Mathf.Max(1f, samples - 1f);
                if (hasLeftMask)
                    score += (profile.LeftWaterTransitions / denom) * smoothWeight;
                if (hasBottomMask)
                    score += (profile.BottomWaterTransitions / denom) * smoothWeight;
            }
            return score;
        }
    }
}
