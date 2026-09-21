/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Ruleset.cs
@module: presentation.pathfinding.worldgen.ruleset
@purpose: Hosts terrain ruleset preparation, auto-ruleset construction, and ruleset tile collection outside the ProceduralEnvironment monolith.
@entry: PENV-40, ProceduralEnvironment.TryPrepareRuleset, ProceduralEnvironment.GetAutoTerrainRuleset
@api: partial class implementation for ProceduralEnvironment
@deps: HexTerrainRuleset, terrain layers, ground palette resolution, edge cache construction, and biome/shared tile filters
@data: prepared terrain layers, edge lookup tables, auto terrain ruleset cache, tile seed, noise offset, and ruleset tile subsets
@perf: generation-time setup; cache auto-generated rulesets by palette/config hash and keep per-run allocations scoped
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: terrain ruleset, auto terrain, water biome, shared ground tiles, random seed, and edge transition settings
@assets: source ground tiles and optional generated runtime HexTerrainRuleset
@notes: this file prepares renderer-agnostic terrain inputs consumed by TerrainData, GroundRender, and BackgroundRenderData
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-RULESET]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.Ruleset.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void GenerateTerrainFromRuleset(int width, int height, bool skipBase, TileBase[] groundPalette)
        {
            if (!TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset))
                return;

            int[] layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
            ApplyRulesetToGround(width, height, skipBase, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
        }

        private IEnumerator GenerateTerrainFromRulesetRoutine(int width, int height, bool skipBase, TileBase[] groundPalette)
        {
            if (!TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset))
                yield break;

            int[] layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
            yield return ApplyRulesetToGroundRoutine(width, height, skipBase, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
        }

        private bool TryPrepareRuleset(
            TileBase[] groundPalette,
            out HexTerrainRuleset ruleset,
            out List<TerrainLayer> layers,
            out Dictionary<int, TileBase>[] edgeLookup,
            out List<HexMaskTile>[] edgeByBits,
            out int tileSeed,
            out Vector2 noiseOffset)
        {
            ruleset = null;
            layers = null;
            edgeLookup = null;
            edgeByBits = null;
            tileSeed = 0;
            noiseOffset = Vector2.zero;

            if (!UseTerrainRuleset)
                return false;

            ruleset = TerrainRuleset != null ? TerrainRuleset : GetAutoTerrainRuleset(groundPalette);
            if (ruleset == null)
                return false;

            if (ruleset.Layers == null || ruleset.Layers.Count == 0)
                return false;

            layers = new List<TerrainLayer>(ruleset.Layers);
            layers.Sort((a, b) => a.MaxHeight.CompareTo(b.MaxHeight));

            var rand = ruleset.UseRandomSeed ? new System.Random() : new System.Random(ruleset.Seed);
            tileSeed = ruleset.UseRandomSeed ? rand.Next() : ruleset.Seed;
            if (ruleset.RandomizeNoiseOffset)
            {
                noiseOffset = new Vector2(
                    (float)rand.NextDouble() * 1000f,
                    (float)rand.NextDouble() * 1000f);
            }
            else
            {
                noiseOffset = ruleset.NoiseOffset;
            }

            edgeLookup = new Dictionary<int, TileBase>[layers.Count];
            edgeByBits = new List<HexMaskTile>[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                BuildEdgeCache(layers[i], out edgeLookup[i], out edgeByBits[i]);
            }

            return true;
        }

        private HexTerrainRuleset GetAutoTerrainRuleset(TileBase[] groundPalette)
        {
            if (!AutoRulesetFromGroundTiles) return null;
            if (groundPalette == null || groundPalette.Length == 0) return null;

            int hash = ComputeAutoTerrainRulesetHash(groundPalette);
            if (_autoTerrainRuleset != null && _autoTerrainRulesetHash == hash)
                return _autoTerrainRuleset;

            ClearAutoTerrainRuleset();

            var ruleset = ScriptableObject.CreateInstance<HexTerrainRuleset>();
            ruleset.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            ruleset.UseRandomSeed = UseRandomSeed;
            ruleset.Seed = Seed;
            ruleset.NoiseScale = AutoTerrainNoiseScale;
            ruleset.Octaves = AutoTerrainOctaves;
            ruleset.Persistence = AutoTerrainPersistence;
            ruleset.Lacunarity = AutoTerrainLacunarity;
            ruleset.NoiseOffset = AutoTerrainNoiseOffset;
            ruleset.RandomizeNoiseOffset = AutoTerrainRandomizeNoiseOffset;
            ruleset.PreferLowerNeighbors = AutoTerrainPreferLowerNeighbors;
            ruleset.TreatOutOfBoundsAsLower = AutoTerrainTreatOutOfBoundsAsLower;

            var palette = groundPalette;
            if (UseWaterBiome)
            {
                palette = ExcludeTilesByNameOrSprite(palette, WaterTileNameKeywords);
                palette = ExcludeTilesByNameOrSprite(palette, RockTileNameKeywords);
                if (palette == null || palette.Length == 0)
                    palette = groundPalette;
            }

            if (AutoTerrainGroupByPrefix)
            {
                var groups = new SortedDictionary<string, List<TileBase>>();
                for (int i = 0; i < palette.Length; i++)
                {
                    var tile = palette[i];
                    if (tile == null) continue;
                    var key = GetAutoTerrainGroupKey(tile);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<TileBase>();
                        groups[key] = list;
                    }
                    list.Add(tile);
                }

                int layerCount = Mathf.Max(1, groups.Count);
                ruleset.Layers = new List<TerrainLayer>(layerCount);
                int index = 0;
                foreach (var entry in groups)
                {
                    var tiles = entry.Value.ToArray();
                    index++;
                    var layer = new TerrainLayer
                    {
                        Id = $"Auto {entry.Key}",
                        MaxHeight = index / (float)layerCount,
                        BaseTiles = tiles
                    };
                    ruleset.Layers.Add(layer);
                }
            }
            else
            {
                int layerSize = Mathf.Max(1, AutoTerrainLayerSize);
                int layerCount = Mathf.Max(1, Mathf.CeilToInt(palette.Length / (float)layerSize));
                ruleset.Layers = new List<TerrainLayer>(layerCount);
                for (int i = 0; i < layerCount; i++)
                {
                    int start = i * layerSize;
                    int end = Mathf.Min(start + layerSize, palette.Length);
                    int count = Mathf.Max(0, end - start);
                    if (count <= 0) continue;
                    var tiles = new TileBase[count];
                    for (int t = 0; t < count; t++)
                        tiles[t] = palette[start + t];

                    var layer = new TerrainLayer
                    {
                        Id = $"Auto {i + 1}",
                        MaxHeight = (i + 1) / (float)layerCount,
                        BaseTiles = tiles
                    };
                    ruleset.Layers.Add(layer);
                }
            }

            _autoTerrainRuleset = ruleset;
            _autoTerrainRulesetHash = hash;
            return ruleset;
        }

        private int ComputeAutoTerrainRulesetHash(TileBase[] groundPalette)
        {
            unchecked
            {
                int h = 17;
                h = (h * 23) + (AutoRulesetFromGroundTiles ? 1 : 0);
                h = (h * 23) + AutoTerrainLayerSize;
                h = (h * 23) + (AutoTerrainGroupByPrefix ? 1 : 0);
                h = (h * 23) + AutoTerrainNoiseScale.GetHashCode();
                h = (h * 23) + AutoTerrainOctaves;
                h = (h * 23) + AutoTerrainPersistence.GetHashCode();
                h = (h * 23) + AutoTerrainLacunarity.GetHashCode();
                h = (h * 23) + AutoTerrainRandomizeNoiseOffset.GetHashCode();
                h = (h * 23) + AutoTerrainNoiseOffset.GetHashCode();
                h = (h * 23) + AutoTerrainPreferLowerNeighbors.GetHashCode();
                h = (h * 23) + AutoTerrainTreatOutOfBoundsAsLower.GetHashCode();
                h = (h * 23) + (UseWaterBiome ? 1 : 0);
                if (UseWaterBiome)
                {
                    h = (h * 23) + ComputeKeywordHash(WaterTileNameKeywords);
                    h = (h * 23) + ComputeKeywordHash(RockTileNameKeywords);
                }
                h = (h * 23) + UseRandomSeed.GetHashCode();
                h = (h * 23) + Seed;
                h = (h * 23) + groundPalette.Length;
                for (int i = 0; i < groundPalette.Length; i++)
                    h = (h * 23) + (groundPalette[i] == null ? 0 : groundPalette[i].GetInstanceID());
                return h;
            }
        }

        private static int ComputeKeywordHash(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0) return 0;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < keywords.Length; i++)
                {
                    var kw = keywords[i];
                    if (string.IsNullOrEmpty(kw))
                    {
                        h = (h * 23);
                        continue;
                    }
                    for (int c = 0; c < kw.Length; c++)
                        h = (h * 23) + kw[c];
                }
                return h;
            }
        }

        private static string GetAutoTerrainGroupKey(TileBase tile)
        {
            if (tile == null) return "_";
            string name = tile.name;
            if (string.IsNullOrEmpty(name))
            {
                var sprite = ExtractTileSprite(tile);
                if (sprite != null) name = sprite.name;
            }
            if (string.IsNullOrEmpty(name)) return "_";

            int idx = name.IndexOf("Ground ", StringComparison.OrdinalIgnoreCase);
            int start = idx >= 0 ? idx + 7 : 0;
            for (int i = start; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch))
                    return char.ToUpperInvariant(ch).ToString();
            }

            return char.ToUpperInvariant(name[0]).ToString();
        }

        private void ClearAutoTerrainRuleset()
        {
            if (_autoTerrainRuleset == null) return;
            if (UnityEngine.Application.isPlaying)
                Destroy(_autoTerrainRuleset);
            else
                DestroyImmediate(_autoTerrainRuleset);
            _autoTerrainRuleset = null;
            _autoTerrainRulesetHash = 0;
        }

        private static TileBase[] CollectUniqueLayerTiles(List<TerrainLayer> layers)
        {
            if (layers == null || layers.Count == 0) return null;
            var unique = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.BaseTiles == null) continue;
                for (int t = 0; t < layer.BaseTiles.Length; t++)
                {
                    var tile = layer.BaseTiles[t];
                    if (tile == null || !seen.Add(tile)) continue;
                    unique.Add(tile);
                }
            }
            return unique.ToArray();
        }

        private TileBase[] ResolveSharedGroundTiles(List<TerrainLayer> layers)
        {
            if (!UseSharedGroundTiles)
                return null;

            var shared = new List<TileBase>();
            var seen = new HashSet<TileBase>();

            if (SharedGroundTiles != null && SharedGroundTiles.Length > 0)
            {
                for (int i = 0; i < SharedGroundTiles.Length; i++)
                {
                    var tile = SharedGroundTiles[i];
                    if (tile != null && seen.Add(tile))
                        shared.Add(tile);
                }
            }

            if (SharedGroundTilesUseNameFilter && SharedGroundTileNameKeywords != null && SharedGroundTileNameKeywords.Length > 0)
            {
                var candidates = CollectUniqueLayerTiles(layers) ?? GroundTiles;
                if (candidates != null)
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var tile = candidates[i];
                        if (tile == null) continue;
                        if (!IsNameMatch(tile.name, SharedGroundTileNameKeywords)) continue;
                        if (seen.Add(tile))
                            shared.Add(tile);
                    }
                }
            }

            return shared.Count > 0 ? shared.ToArray() : null;
        }

        private TileBase[] ResolveBiomeTilesByName(List<TerrainLayer> layers, string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return null;

            var candidates = CollectUniqueLayerTiles(layers) ?? GroundTiles;
            if (candidates == null || candidates.Length == 0) return null;

            var result = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var tile = candidates[i];
                if (tile == null) continue;
                if (!IsNameMatch(tile.name, keywords)) continue;
                if (seen.Add(tile))
                    result.Add(tile);
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private TileBase[] ResolveBiomeTilesByName(TileBase[] candidates, string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return null;
            if (candidates == null || candidates.Length == 0)
                return null;

            var result = new List<TileBase>();
            var seen = new HashSet<TileBase>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var tile = candidates[i];
                if (tile == null) continue;
                if (!IsNameMatch(tile.name, keywords)) continue;
                if (seen.Add(tile))
                    result.Add(tile);
            }

            return result.Count > 0 ? result.ToArray() : null;
        }
    }
}
