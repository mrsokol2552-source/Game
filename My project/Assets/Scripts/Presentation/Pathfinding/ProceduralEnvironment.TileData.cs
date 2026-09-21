/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileData.cs
@module: presentation.pathfinding.worldgen.tile_data
@purpose: Hosts tile-selection data types, deterministic picking helpers, neighbor masks, and edge-cache primitives outside the ProceduralEnvironment monolith.
@entry: PENV-41, ProceduralEnvironment.BuildEdgeCache, ProceduralEnvironment.BuildNeighborMask
@api: partial class implementation for ProceduralEnvironment
@deps: TerrainLayer, HexMaskTile, HexTerrainRuleset, tile palette filters, and tile variant/profile consumers
@data: edge profiles, tile variants, ground-tile overrides, matrix identity keys, neighbor offsets, and deterministic tile picks
@perf: hot in terrain render payload creation and streaming chunk tile selection; helpers avoid per-cell dynamic lookup setup
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: ground tile variant, transition mask, shared-tile, anti-repeat, and edge-matching settings
@assets: source tile assets and optional terrain edge tiles
@notes: keep low-level tile data primitives separate from profile scoring so future shader/render backends can reuse the same selection data
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-TILEDATA]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.TileData.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private struct EdgeProfile
        {
            public bool TopGreen;
            public bool RightGreen;
            public bool BottomGreen;
            public bool LeftGreen;
            public bool TopWater;
            public bool RightWater;
            public bool BottomWater;
            public bool LeftWater;
            public float TopWaterRatio;
            public float RightWaterRatio;
            public float BottomWaterRatio;
            public float LeftWaterRatio;
            public byte TopWaterMask;
            public byte RightWaterMask;
            public byte BottomWaterMask;
            public byte LeftWaterMask;
            public byte WaterMaskSamples;
            public byte TopWaterTransitions;
            public byte RightWaterTransitions;
            public byte BottomWaterTransitions;
            public byte LeftWaterTransitions;
        }

        private struct TileVariant
        {
            public TileBase Tile;
            public Matrix4x4 Transform;
            public EdgeProfile Profile;
            public int Id;
        }

        [Serializable]
        public class GroundTileOverride
        {
            public string NameContains;
            public float ExtraInsetPixels = 0f;
            public int ExtraEdgeTrimPixels = 0;
            public bool OverrideEdgeBlackThreshold = false;
            public float EdgeBlackThresholdOverride = 0f;
            public bool OverrideEdgeChromaThreshold = false;
            public float EdgeChromaThresholdOverride = 0f;
            public float ExtraTopInsetPixels = 0f;
            public float ExtraRightInsetPixels = 0f;
            public float ExtraBottomInsetPixels = 0f;
            public float ExtraLeftInsetPixels = 0f;
        }

        private readonly struct MatrixKey : IEquatable<MatrixKey>
        {
            public readonly int M00;
            public readonly int M01;
            public readonly int M10;
            public readonly int M11;

            public MatrixKey(int m00, int m01, int m10, int m11)
            {
                M00 = m00;
                M01 = m01;
                M10 = m10;
                M11 = m11;
            }

            public static MatrixKey From(Matrix4x4 m)
            {
                return new MatrixKey(
                    Mathf.RoundToInt(m.m00 * 1000f),
                    Mathf.RoundToInt(m.m01 * 1000f),
                    Mathf.RoundToInt(m.m10 * 1000f),
                    Mathf.RoundToInt(m.m11 * 1000f));
            }

            public bool Equals(MatrixKey other)
            {
                return M00 == other.M00 && M01 == other.M01 && M10 == other.M10 && M11 == other.M11;
            }

            public override bool Equals(object obj)
            {
                return obj is MatrixKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = (h * 23) + M00;
                    h = (h * 23) + M01;
                    h = (h * 23) + M10;
                    h = (h * 23) + M11;
                    return h;
                }
            }
        }

        // Mask bit order: 0=E, 1=NE, 2=NW, 3=W, 4=SW, 5=SE (odd-r layout).
        private static readonly Vector2Int[] EvenRowNeighborOffsets =
        {
            new Vector2Int(1, 0),   // E
            new Vector2Int(0, -1),  // NE
            new Vector2Int(-1, -1), // NW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(-1, 1),  // SW
            new Vector2Int(0, 1),   // SE
        };

        private static readonly Vector2Int[] OddRowNeighborOffsets =
        {
            new Vector2Int(1, 0),   // E
            new Vector2Int(1, -1),  // NE
            new Vector2Int(0, -1),  // NW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(0, 1),   // SW
            new Vector2Int(1, 1),   // SE
        };

        private static readonly Vector2Int[] RectNeighborOffsets4 =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private static readonly Vector2Int[] RectNeighborOffsets8 =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
        };

        private TileVariant[] ExcludeVariantsByNameOrSprite(TileVariant[] variants, string[] excludeKeywords)
        {
            if (variants == null || variants.Length == 0)
                return variants;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return variants;

            var filtered = new List<TileVariant>(variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                var tile = variant.Tile;
                if (tile == null) continue;
                bool match = IsNameMatch(tile.name, excludeKeywords);
                if (!match)
                {
                    var sprite = ExtractTileSprite(tile);
                    if (sprite != null)
                        match = IsNameMatch(sprite.name, excludeKeywords);
                }
                if (match) continue;
                filtered.Add(variant);
            }

            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<TileVariant>();
        }

        private static bool IsTileExcludedByNameOrSprite(TileBase tile, string[] excludeKeywords)
        {
            if (tile == null || excludeKeywords == null || excludeKeywords.Length == 0)
                return false;
            if (IsNameMatch(tile.name, excludeKeywords))
                return true;
            var sprite = ExtractTileSprite(tile);
            return sprite != null && IsNameMatch(sprite.name, excludeKeywords);
        }

        private static void BuildEdgeCache(
            TerrainLayer layer,
            out Dictionary<int, TileBase> edgeLookup,
            out List<HexMaskTile> edgeByBits)
        {
            edgeLookup = null;
            edgeByBits = null;
            if (layer == null || layer.EdgeTiles == null || layer.EdgeTiles.Count == 0)
                return;

            edgeLookup = new Dictionary<int, TileBase>();
            edgeByBits = new List<HexMaskTile>(layer.EdgeTiles.Count);
            for (int i = 0; i < layer.EdgeTiles.Count; i++)
            {
                var entry = layer.EdgeTiles[i];
                if (entry == null || entry.Tile == null) continue;
                int mask = Mathf.Clamp(entry.Mask, 0, 63);
                if (!edgeLookup.ContainsKey(mask))
                    edgeLookup.Add(mask, entry.Tile);
                edgeByBits.Add(entry);
            }
            edgeByBits.Sort((a, b) => CountBits(b.Mask).CompareTo(CountBits(a.Mask)));
        }

        private static TileBase PickEdgeTile(
            int mask,
            TerrainLayer layer,
            Dictionary<int, TileBase> edgeLookup,
            List<HexMaskTile> edgeByBits)
        {
            if (edgeLookup != null && edgeLookup.TryGetValue(mask, out var exact) && exact != null)
                return exact;

            if (edgeByBits != null)
            {
                for (int i = 0; i < edgeByBits.Count; i++)
                {
                    var entry = edgeByBits[i];
                    if (entry == null || entry.Tile == null) continue;
                    if ((mask & entry.Mask) == entry.Mask)
                        return entry.Tile;
                }
            }

            return layer != null ? layer.DefaultEdgeTile : null;
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                if ((mask & (1 << i)) != 0)
                    count++;
            }
            return count;
        }

        private static TileBase PickTileDeterministic(TileBase[] palette, int col, int row, int seed)
        {
            if (palette == null || palette.Length == 0) return null;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            return palette[hash % palette.Length];
        }

        private static TileBase PickTileDeterministicExcluding(TileBase[] palette, int col, int row, int seed, string[] excludeKeywords)
        {
            if (palette == null || palette.Length == 0) return null;
            if (excludeKeywords == null || excludeKeywords.Length == 0)
                return PickTileDeterministic(palette, col, row, seed);
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            int start = hash % palette.Length;
            for (int i = 0; i < palette.Length; i++)
            {
                var tile = palette[(start + i) % palette.Length];
                if (!IsTileExcludedByNameOrSprite(tile, excludeKeywords))
                    return tile;
            }
            return palette[start];
        }

        private static int BuildNeighborMask(
            int col,
            int row,
            int width,
            int height,
            int layerIdx,
            int[] layerIndex,
            HexTerrainRuleset ruleset)
        {
            var offsets = (row & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
            int mask = 0;
            for (int i = 0; i < 6; i++)
            {
                int nc = col + offsets[i].x;
                int nr = row + offsets[i].y;
                if (nc < 0 || nr < 0 || nc >= width || nr >= height)
                {
                    if (ruleset.TreatOutOfBoundsAsLower)
                        mask |= (1 << i);
                    continue;
                }

                int nIdx = (nr * width) + nc;
                int neighborLayer = layerIndex[nIdx];
                bool diff = ruleset.PreferLowerNeighbors ? neighborLayer < layerIdx : neighborLayer != layerIdx;
                if (diff)
                    mask |= (1 << i);
            }
            return mask;
        }
    }
}
