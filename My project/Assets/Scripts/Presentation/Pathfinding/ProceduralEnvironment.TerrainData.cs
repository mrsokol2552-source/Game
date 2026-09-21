/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.TerrainData.cs
@module: presentation.pathfinding.worldgen.terrain_data
@purpose: Builds terrain layer-index data from rulesets, noise, smoothing, and region cleanup outside the ProceduralEnvironment monolith.
@entry: PENV-39, ProceduralEnvironment.BuildLayerIndex, ProceduralEnvironment.BuildLayerIndexRect
@api: partial class implementation for ProceduralEnvironment
@deps: terrain layers, terrain ruleset, noise configuration, layer smoothing, and region cleanup settings
@data: per-cell terrain layer indices for hex and rectangular render/writeback paths
@perf: hot during terrain generation; keep allocation bounded to layer arrays and cleanup queues
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and terrain generation smoke tests
@config: terrain noise, macro biome, domain warp, layer quantization, smoothing, and cleanup settings
@assets: none directly; this layer produces data consumed by tile selection and render payload builders
@notes: keep terrain data preparation renderer-agnostic so tilemap, far-view, and future shader paths consume the same layer-index source
*/

using System;
using System.Collections.Generic;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-TERRAINDATA]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.TerrainData.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private int[] BuildLayerIndex(int width, int height, List<TerrainLayer> layers, Vector2 noiseOffset, HexTerrainRuleset ruleset)
        {
            int size = width * height;
            var layerIndex = new int[size];

            int layerCount = layers?.Count ?? 0;
            float scale = Mathf.Max(0.0001f, ruleset.NoiseScale);
            int octaves = Mathf.Max(1, ruleset.Octaves);
            float persistence = Mathf.Clamp01(ruleset.Persistence);
            float lacunarity = Mathf.Max(0.01f, ruleset.Lacunarity);
            float warpScale = Mathf.Max(0.0001f, DomainWarpScale);
            float warpStrength = Mathf.Max(0f, DomainWarpStrength);
            int warpOctaves = Mathf.Max(1, DomainWarpOctaves);
            float warpPersistence = Mathf.Clamp01(DomainWarpPersistence);
            float warpLacunarity = Mathf.Max(0.01f, DomainWarpLacunarity);
            float macroScale = Mathf.Max(0.0001f, MacroBiomeScale);
            int macroOctaves = Mathf.Max(1, MacroBiomeOctaves);
            float macroPersistence = Mathf.Clamp01(MacroBiomePersistence);
            float macroLacunarity = Mathf.Max(0.01f, MacroBiomeLacunarity);
            float macroBlend = Mathf.Clamp01(MacroBiomeBlend);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int q = col - (row - (row & 1)) / 2;
                    int r = row;
                    const float sqrt3Over2 = 0.8660254f;
                    float wx = q + (r * 0.5f);
                    float wy = r * sqrt3Over2;
                    if (UseNoiseDomainWarp)
                    {
                        float warpX = FractalNoise((wx + noiseOffset.x + 127.1f) * warpScale, (wy + noiseOffset.y + 311.7f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        float warpY = FractalNoise((wx + noiseOffset.x + 269.5f) * warpScale, (wy + noiseOffset.y + 183.3f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        warpX = (warpX - 0.5f) * 2f;
                        warpY = (warpY - 0.5f) * 2f;
                        wx += warpX * warpStrength;
                        wy += warpY * warpStrength;
                    }

                    float nx = (wx + noiseOffset.x) * scale;
                    float ny = (wy + noiseOffset.y) * scale;
                    float h = FractalNoise(nx, ny, octaves, persistence, lacunarity);
                    if (UseMacroBiomeNoise)
                    {
                        float mx = (wx + noiseOffset.x) * macroScale;
                        float my = (wy + noiseOffset.y) * macroScale;
                        float macro = FractalNoise(mx, my, macroOctaves, macroPersistence, macroLacunarity);
                        macro = AdjustContrast(macro, MacroBiomeContrast);
                        h = Mathf.Lerp(h, macro, macroBlend);
                    }
                    h = ApplyLayerQuantization(h, col, row, layerCount, ruleset.Seed);
                    int idx = (row * width) + col;
                    layerIndex[idx] = FindLayerIndex(h, layers);
                }
            }

            var smoothed = ApplyLayerSmoothingHex(layerIndex, width, height, layers);
            return ApplyLayerRegionCleanupHex(smoothed, width, height, layers);
        }

        private int[] BuildLayerIndexRect(int width, int height, List<TerrainLayer> layers, Vector2 noiseOffset, Vector2 cellSize, HexTerrainRuleset ruleset)
        {
            int size = width * height;
            var layerIndex = new int[size];

            int layerCount = layers?.Count ?? 0;
            float scale = Mathf.Max(0.0001f, ruleset.NoiseScale);
            int octaves = Mathf.Max(1, ruleset.Octaves);
            float persistence = Mathf.Clamp01(ruleset.Persistence);
            float lacunarity = Mathf.Max(0.01f, ruleset.Lacunarity);
            float stepX = Mathf.Max(0.0001f, cellSize.x);
            float stepY = Mathf.Max(0.0001f, cellSize.y);
            float warpScale = Mathf.Max(0.0001f, DomainWarpScale);
            float warpStrength = Mathf.Max(0f, DomainWarpStrength);
            int warpOctaves = Mathf.Max(1, DomainWarpOctaves);
            float warpPersistence = Mathf.Clamp01(DomainWarpPersistence);
            float warpLacunarity = Mathf.Max(0.01f, DomainWarpLacunarity);
            float macroScale = Mathf.Max(0.0001f, MacroBiomeScale);
            int macroOctaves = Mathf.Max(1, MacroBiomeOctaves);
            float macroPersistence = Mathf.Clamp01(MacroBiomePersistence);
            float macroLacunarity = Mathf.Max(0.01f, MacroBiomeLacunarity);
            float macroBlend = Mathf.Clamp01(MacroBiomeBlend);

            for (int row = 0; row < height; row++)
            {
                float wy = row * stepY;
                for (int col = 0; col < width; col++)
                {
                    float wx = col * stepX;
                    if (UseNoiseDomainWarp)
                    {
                        float warpX = FractalNoise((wx + noiseOffset.x + 127.1f) * warpScale, (wy + noiseOffset.y + 311.7f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        float warpY = FractalNoise((wx + noiseOffset.x + 269.5f) * warpScale, (wy + noiseOffset.y + 183.3f) * warpScale, warpOctaves, warpPersistence, warpLacunarity);
                        warpX = (warpX - 0.5f) * 2f;
                        warpY = (warpY - 0.5f) * 2f;
                        wx += warpX * warpStrength;
                        wy += warpY * warpStrength;
                    }
                    float nx = (wx + noiseOffset.x) * scale;
                    float ny = (wy + noiseOffset.y) * scale;
                    float h = FractalNoise(nx, ny, octaves, persistence, lacunarity);
                    if (UseMacroBiomeNoise)
                    {
                        float mx = (wx + noiseOffset.x) * macroScale;
                        float my = (wy + noiseOffset.y) * macroScale;
                        float macro = FractalNoise(mx, my, macroOctaves, macroPersistence, macroLacunarity);
                        macro = AdjustContrast(macro, MacroBiomeContrast);
                        h = Mathf.Lerp(h, macro, macroBlend);
                    }
                    h = ApplyLayerQuantization(h, col, row, layerCount, ruleset.Seed);
                    int idx = (row * width) + col;
                    layerIndex[idx] = FindLayerIndex(h, layers);
                }
            }

            var smoothed = ApplyLayerSmoothingRect(layerIndex, width, height, layers);
            return ApplyLayerRegionCleanupRect(smoothed, width, height, layers);
        }

        private int[] ApplyLayerSmoothingHex(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerSmoothing || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerSmoothingPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;

            int[] src = layerIndex;
            int[] dst = new int[src.Length];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    var offsets = (row & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        Array.Clear(counts, 0, layerCount);
                        int idx = rowBase + col;
                        int current = src[idx];
                        counts[current]++;
                        int neighborCount = 1;

                        for (int i = 0; i < 6; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            counts[src[nIdx]]++;
                            neighborCount++;
                        }

                        int bestLayer = current;
                        int bestCount = counts[current];
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (counts[i] > bestCount)
                            {
                                bestLayer = i;
                                bestCount = counts[i];
                            }
                        }

                        int required = Mathf.Max(2, Mathf.CeilToInt(neighborCount * Mathf.Clamp01(LayerSmoothingMajority)));
                        dst[idx] = (bestLayer != current && bestCount >= required) ? bestLayer : current;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, layerIndex))
                Array.Copy(src, layerIndex, layerIndex.Length);
            return layerIndex;
        }

        private int[] ApplyLayerSmoothingRect(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerSmoothing || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerSmoothingPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;

            var offsets = LayerSmoothingIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;
            int[] src = layerIndex;
            int[] dst = new int[src.Length];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        Array.Clear(counts, 0, layerCount);
                        int idx = rowBase + col;
                        int current = src[idx];
                        counts[current]++;
                        int neighborCount = 1;

                        for (int i = 0; i < offsets.Length; i++)
                        {
                            int nc = col + offsets[i].x;
                            int nr = row + offsets[i].y;
                            if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                            int nIdx = (nr * width) + nc;
                            counts[src[nIdx]]++;
                            neighborCount++;
                        }

                        int bestLayer = current;
                        int bestCount = counts[current];
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (counts[i] > bestCount)
                            {
                                bestLayer = i;
                                bestCount = counts[i];
                            }
                        }

                        int required = Mathf.Max(2, Mathf.CeilToInt(neighborCount * Mathf.Clamp01(LayerSmoothingMajority)));
                        dst[idx] = (bestLayer != current && bestCount >= required) ? bestLayer : current;
                    }
                }

                var swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, layerIndex))
                Array.Copy(src, layerIndex, layerIndex.Length);
            return layerIndex;
        }

        private int[] ApplyLayerRegionCleanupHex(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerRegionCleanup || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerCleanupPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;
            int size = width * height;
            int[] queue = new int[size];
            int[] counts = new int[layerCount];

            for (int pass = 0; pass < passes; pass++)
            {
                var visited = new bool[size];
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        if (visited[idx]) continue;
                        int layer = layerIndex[idx];
                        int head = 0;
                        int tail = 0;
                        queue[tail++] = idx;
                        visited[idx] = true;
                        int regionCount = 0;
                        Array.Clear(counts, 0, layerCount);

                        while (head < tail)
                        {
                            int current = queue[head++];
                            regionCount++;
                            int c = current % width;
                            int r = current / width;
                            var localOffsets = (r & 1) == 0 ? EvenRowNeighborOffsets : OddRowNeighborOffsets;
                            for (int i = 0; i < 6; i++)
                            {
                                int nc = c + localOffsets[i].x;
                                int nr = r + localOffsets[i].y;
                                if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                                int nIdx = (nr * width) + nc;
                                int nLayer = layerIndex[nIdx];
                                if (nLayer == layer)
                                {
                                    if (!visited[nIdx])
                                    {
                                        visited[nIdx] = true;
                                        queue[tail++] = nIdx;
                                    }
                                }
                                else
                                {
                                    if (nLayer >= 0 && nLayer < layerCount)
                                        counts[nLayer]++;
                                }
                            }
                        }

                        if (regionCount < LayerMinRegionSize)
                        {
                            int bestLayer = layer;
                            int bestCount = -1;
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (counts[i] > bestCount)
                                {
                                    bestLayer = i;
                                    bestCount = counts[i];
                                }
                            }
                            if (bestLayer != layer && bestCount > 0)
                            {
                                for (int i = 0; i < tail; i++)
                                    layerIndex[queue[i]] = bestLayer;
                            }
                        }
                    }
                }
            }

            return layerIndex;
        }

        private int[] ApplyLayerRegionCleanupRect(int[] layerIndex, int width, int height, List<TerrainLayer> layers)
        {
            if (!UseLayerRegionCleanup || layerIndex == null) return layerIndex;
            int passes = Mathf.Max(0, LayerCleanupPasses);
            int layerCount = layers?.Count ?? 0;
            if (passes == 0 || layerCount <= 1) return layerIndex;
            int size = width * height;
            int[] queue = new int[size];
            int[] counts = new int[layerCount];
            var offsets = LayerCleanupIncludeDiagonals ? RectNeighborOffsets8 : RectNeighborOffsets4;

            for (int pass = 0; pass < passes; pass++)
            {
                var visited = new bool[size];
                for (int row = 0; row < height; row++)
                {
                    int rowBase = row * width;
                    for (int col = 0; col < width; col++)
                    {
                        int idx = rowBase + col;
                        if (visited[idx]) continue;
                        int layer = layerIndex[idx];
                        int head = 0;
                        int tail = 0;
                        queue[tail++] = idx;
                        visited[idx] = true;
                        int regionCount = 0;
                        Array.Clear(counts, 0, layerCount);

                        while (head < tail)
                        {
                            int current = queue[head++];
                            regionCount++;
                            int c = current % width;
                            int r = current / width;
                            for (int i = 0; i < offsets.Length; i++)
                            {
                                int nc = c + offsets[i].x;
                                int nr = r + offsets[i].y;
                                if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                                int nIdx = (nr * width) + nc;
                                int nLayer = layerIndex[nIdx];
                                if (nLayer == layer)
                                {
                                    if (!visited[nIdx])
                                    {
                                        visited[nIdx] = true;
                                        queue[tail++] = nIdx;
                                    }
                                }
                                else
                                {
                                    if (nLayer >= 0 && nLayer < layerCount)
                                        counts[nLayer]++;
                                }
                            }
                        }

                        if (regionCount < LayerMinRegionSize)
                        {
                            int bestLayer = layer;
                            int bestCount = -1;
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (counts[i] > bestCount)
                                {
                                    bestLayer = i;
                                    bestCount = counts[i];
                                }
                            }
                            if (bestLayer != layer && bestCount > 0)
                            {
                                for (int i = 0; i < tail; i++)
                                    layerIndex[queue[i]] = bestLayer;
                            }
                        }
                    }
                }
            }

            return layerIndex;
        }

        private static float FractalNoise(float x, float y, int octaves, float persistence, float lacunarity)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sx = x * frequency;
                float sy = y * frequency;
                float perlin = Mathf.PerlinNoise(sx, sy) * 2f - 1f;
                noiseHeight += perlin * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            if (maxValue <= 0f) return 0f;
            float normalized = (noiseHeight / maxValue + 1f) * 0.5f;
            return Mathf.Clamp01(normalized);
        }

        private static float AdjustContrast(float value, float contrast)
        {
            if (contrast <= 0f) return Mathf.Clamp01(value);
            return Mathf.Clamp01(0.5f + (value - 0.5f) * contrast);
        }

        private float ApplyLayerQuantization(float value, int col, int row, int layerCount, int seed)
        {
            if (!UseLayerQuantization || layerCount <= 1) return Mathf.Clamp01(value);
            float step = 1f / layerCount;
            int hash = (col * 73856093) ^ (row * 19349663) ^ seed;
            if (hash < 0) hash = -hash;
            float jitter = (LayerQuantizationJitter > 0f)
                ? ((hash % 1000) / 999f - 0.5f) * 2f * LayerQuantizationJitter
                : 0f;
            float quantized = Mathf.Round(value / step) * step + jitter * step;
            return Mathf.Clamp01(quantized);
        }

        private static int FindLayerIndex(float heightValue, List<TerrainLayer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (heightValue <= layers[i].MaxHeight)
                    return i;
            }
            return layers.Count - 1;
        }
    }
}
