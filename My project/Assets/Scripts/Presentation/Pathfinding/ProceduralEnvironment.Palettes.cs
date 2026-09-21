/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.cs
@module: presentation.pathfinding.worldgen.palettes
@purpose: Hosts palette resolution, tile lookup/exclusion helpers, biome-mask cleanup, and post-placement obstacle baking helpers for ProceduralEnvironment.
@entry: PENV-22, PENV-23, PENV-24, ProceduralEnvironment.ResolvePalettes, ProceduralEnvironment.SmoothMask
@api: partial class implementation for ProceduralEnvironment
@deps: worldgen palettes, background masks, blocking tilemaps, HexPathfindingBootstrap obstacle baking
@data: weighted palettes, accent subsets, temporary biome masks
@perf: medium; runs during generation and streaming prep but should stay allocation-aware
@thread: main thread only
@tests: indirect coverage via repo audits, Unity recompilation, and worldgen/streaming smoke tests
@config: ground/prop/blocking palettes, weighting keywords, tree rarity/accent knobs, water-mask smoothing knobs, direct walkable updates
@assets: GroundTiles, PropTiles, BlockingPropTiles, TreeTiles
@notes: keep palette routing, lookup mapping, exclusion, and mask cleanup together so future data-extraction work can swap render backends without changing biome/palette policy
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-PALETTES]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.Palettes.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private readonly struct TileScore
        {
            public readonly TileBase Tile;
            public readonly float Score;

            public TileScore(TileBase tile, float score)
            {
                Tile = tile;
                Score = score;
            }
        }

        // [PENV-22]
        // Post-placement obstacle rebake and palette routing between ground, props, and blockers.
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

            if (UseGroundSuffixFilter && groundPalette != null && groundPalette.Length > 0 && !string.IsNullOrEmpty(GroundSuffixFilter))
            {
                var filtered = new List<TileBase>(groundPalette.Length);
                for (int i = 0; i < groundPalette.Length; i++)
                {
                    var tile = groundPalette[i];
                    if (tile == null) continue;
                    if (tile.name.EndsWith(GroundSuffixFilter, StringComparison.OrdinalIgnoreCase))
                        filtered.Add(tile);
                }
                if (filtered.Count > 0)
                    groundPalette = filtered.ToArray();
            }

            if (AutoSplitBlockingByName
                && propPalette != null && propPalette.Length > 0
                && BlockingNameKeywords != null && BlockingNameKeywords.Length > 0)
            {
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

            propPalette = ApplyPropWeighting(propPalette);

            if (ConvertGroundTilesRuntime && !_skipRuntimeConversionInResolve)
                groundPalette = ConvertGroundPalette(groundPalette);

            groundPalette = FilterNullTiles(groundPalette);
            propPalette = FilterNullTiles(propPalette);
            blockingPalette = FilterNullTiles(blockingPalette);
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

        private static TileBase[] FilterNullTiles(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            var filtered = new List<TileBase>(palette.Length);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile != null)
                    filtered.Add(tile);
            }
            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<TileBase>();
        }

        private TileBase[] ExcludeTilesByName(TileBase[] tiles, string[] excludeKeywords)
        {
            if (tiles == null || tiles.Length == 0)
                return tiles;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return tiles;

            var filtered = new List<TileBase>(tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, excludeKeywords)) continue;
                filtered.Add(tile);
            }

            return filtered.Count > 0 ? filtered.ToArray() : null;
        }

        private TileBase[] ExcludeTilesByNameOrSprite(TileBase[] tiles, string[] excludeKeywords)
        {
            if (tiles == null || tiles.Length == 0)
                return tiles;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return tiles;

            var filtered = new List<TileBase>(tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                bool match = IsNameMatch(tile.name, excludeKeywords);
                if (!match)
                {
                    var sprite = ExtractTileSprite(tile);
                    if (sprite != null)
                        match = IsNameMatch(sprite.name, excludeKeywords);
                }
                if (match) continue;
                filtered.Add(tile);
            }

            return filtered.Count > 0 ? filtered.ToArray() : null;
        }

        private string[] ResolveWaterExcludeKeywords()
        {
            if (WaterTileExcludeKeywords != null && WaterTileExcludeKeywords.Length > 0)
                return WaterTileExcludeKeywords;
            return new[] { "Ground A3_", "Ground A11_", "Ground A12_" };
        }

        private Dictionary<TileBase, TileBase> BuildConvertedLookup(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return null;
            var converted = ConvertGroundPalette(palette);
            var lookup = new Dictionary<TileBase, TileBase>(palette.Length);
            for (int i = 0; i < palette.Length; i++)
                lookup[palette[i]] = converted[i];
            return lookup;
        }

        private static TileBase[] MapPalette(TileBase[] palette, Dictionary<TileBase, TileBase> lookup)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (lookup == null || lookup.Count == 0) return palette;
            var mapped = new TileBase[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile != null && lookup.TryGetValue(tile, out var converted) && converted != null)
                    mapped[i] = converted;
                else
                    mapped[i] = tile;
            }
            return mapped;
        }

        private TileBase MapBackgroundTile(TileBase tile, Dictionary<TileBase, TileBase> lookup)
        {
            return MapBackgroundTileCore(tile, lookup);
        }

        private static HashSet<TileBase> BuildTileSet(TileBase[] tiles)
        {
            if (tiles == null || tiles.Length == 0) return null;
            var set = new HashSet<TileBase>();
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile != null)
                    set.Add(tile);
            }
            return set.Count > 0 ? set : null;
        }

        private static TileBase[] CombineTiles(TileBase[] primary, TileBase[] secondary)
        {
            if ((primary == null || primary.Length == 0) && (secondary == null || secondary.Length == 0))
                return null;
            var list = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            if (primary != null)
            {
                for (int i = 0; i < primary.Length; i++)
                {
                    var tile = primary[i];
                    if (tile != null && seen.Add(tile))
                        list.Add(tile);
                }
            }
            if (secondary != null)
            {
                for (int i = 0; i < secondary.Length; i++)
                {
                    var tile = secondary[i];
                    if (tile != null && seen.Add(tile))
                        list.Add(tile);
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // [PENV-23]
        // Palette weighting, accent extraction, and prop/tree selection helpers.
        private TileBase[] ApplyPropWeighting(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (PropNoBoostKeywords == null || PropNoBoostKeywords.Length == 0) return palette;

            const int baseWeight = 4;
            const int boostedWeight = 9;
            var weighted = new List<TileBase>(palette.Length * boostedWeight);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                int weight = IsNameMatch(tile.name, PropNoBoostKeywords) ? baseWeight : boostedWeight;
                for (int w = 0; w < weight; w++)
                    weighted.Add(tile);
            }
            return weighted.Count > 0 ? weighted.ToArray() : palette;
        }

        private TileBase[] ApplyTreeWeighting(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            bool hasRare = TreeRareKeywords != null && TreeRareKeywords.Length > 0;
            bool hasVeryRare = TreeVeryRareKeywords != null && TreeVeryRareKeywords.Length > 0;
            bool hasAccent = TreeAccentExcludeFromBase
                && TreeAccentKeywords != null
                && TreeAccentKeywords.Length > 0;
            if (!hasRare && !hasVeryRare && !hasAccent) return palette;

            float rareWeight = Mathf.Clamp(TreeRareWeight, 0.05f, 1f);
            float veryRareWeight = Mathf.Clamp(TreeVeryRareWeight, 0.02f, 1f);
            if (rareWeight >= 0.99f && veryRareWeight >= 0.99f) return palette;

            const int baseWeight = 10;
            int rareCount = Mathf.Max(1, Mathf.RoundToInt(baseWeight * rareWeight));
            int veryRareCount = Mathf.Max(1, Mathf.RoundToInt(baseWeight * veryRareWeight));
            var weighted = new List<TileBase>(palette.Length * baseWeight);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                if (hasAccent && IsNameMatch(tile.name, TreeAccentKeywords))
                    continue;
                int weight = baseWeight;
                if (hasVeryRare && IsNameMatch(tile.name, TreeVeryRareKeywords))
                    weight = veryRareCount;
                else if (hasRare && IsNameMatch(tile.name, TreeRareKeywords))
                    weight = rareCount;
                for (int w = 0; w < weight; w++)
                    weighted.Add(tile);
            }
            return weighted.Count > 0 ? weighted.ToArray() : palette;
        }

        private TileBase[] ResolveTreeAccentTiles()
        {
            if (TreeAccentTiles != null && TreeAccentTiles.Length > 0)
                return TreeAccentTiles;
            if (TreeTiles == null || TreeTiles.Length == 0) return null;
            if (TreeAccentKeywords == null || TreeAccentKeywords.Length == 0) return null;
            var list = new List<TileBase>();
            for (int i = 0; i < TreeTiles.Length; i++)
            {
                var tile = TreeTiles[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, TreeAccentKeywords))
                    list.Add(tile);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private TileBase[] BuildPropBoostPalette(TileBase[] palette)
        {
            if (palette == null || palette.Length == 0) return palette;
            if (PropNoBoostKeywords == null || PropNoBoostKeywords.Length == 0) return palette;
            var filtered = new List<TileBase>(palette.Length);
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[i];
                if (tile == null) continue;
                if (IsNameMatch(tile.name, PropNoBoostKeywords)) continue;
                filtered.Add(tile);
            }
            return filtered.Count > 0 ? filtered.ToArray() : palette;
        }

        // [PENV-24]
        // Water/rock biome mask cleanup and smoothing helpers used before tile selection.
        private static int CountMaskNeighbors4(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null) return 0;
            int idx = (row * width) + col;
            int count = 0;
            if (col > 0 && mask[idx - 1]) count++;
            if (col < width - 1 && mask[idx + 1]) count++;
            if (row > 0 && mask[idx - width]) count++;
            if (row < height - 1 && mask[idx + width]) count++;
            return count;
        }

        private static int RemoveIsolatedMaskCells(bool[] mask, int width, int height)
        {
            if (mask == null) return 0;
            int size = width * height;
            var original = new bool[size];
            Array.Copy(mask, original, size);
            int removed = 0;
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    int idx = rowBase + col;
                    if (!original[idx]) continue;
                    if (CountMaskNeighbors4(original, width, height, col, row) == 0)
                    {
                        mask[idx] = false;
                        removed++;
                    }
                }
            }
            return removed;
        }

        private bool[] SmoothMask(bool[] mask, int width, int height)
        {
            if (!UseWaterMaskSmoothing || mask == null) return mask;
            int passes = Mathf.Max(0, WaterMaskSmoothPasses);
            if (passes == 0) return mask;
            int size = width * height;
            bool[] src = mask;
            bool[] dst = new bool[size];
            var offsets = WaterMaskSmoothIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        int count = 0;
                        for (int i = 0; i < offsets.Length; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            if (src[nIdx]) count++;
                        }

                        if (src[idx])
                            dst[idx] = count >= WaterMaskSmoothStayNeighbors;
                        else
                            dst[idx] = count >= WaterMaskSmoothFillNeighbors;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, mask))
                Array.Copy(src, mask, size);
            return mask;
        }
    }
}
