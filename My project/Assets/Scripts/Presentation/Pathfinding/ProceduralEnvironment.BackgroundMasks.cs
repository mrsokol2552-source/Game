/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.cs
@module: presentation.pathfinding.worldgen.background_masks
@purpose: Holds background-grid sizing, streamed water/rock mask generation, land-distance fields, and background flag accessors for ProceduralEnvironment.
@entry: PENV-16, PENV-17, PENV-18, ProceduralEnvironment.TryConfigureBackground, ProceduralEnvironment.BuildStreamingBackgroundMasks
@api: partial class implementation for ProceduralEnvironment
@deps: Grid/Tilemap sizing, background mask arrays, HexPathfindingBootstrap world bounds, water/rock biome configuration
@data: background cell size, water masks, rock masks, land distance fields
@perf: medium; setup-time sizing plus streamed mask generation and frequent background-flag lookups
@thread: main thread only
@tests: indirect coverage via repo audits, Unity recompilation, and streaming/worldgen smoke tests
@config: background sizing, water/rock biome, lake/river thickness, and mask-related inspector fields in ProceduralEnvironment
@assets: background tilemap/grid, converted ground palette
@notes: keep coordinate conversion and mask access rules together so hex-grid vs background-grid logic stays consistent during refactors
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDMASKS]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundMasks.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        // [PENV-16]
        // Background-grid sizing derived from hex-world bounds and square tile dimensions.
        private bool TryConfigureBackground(int hexWidth, int hexHeight, TileBase[] palette, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (_backgroundGrid == null || palette == null || palette.Length == 0 || _hex == null) return false;
            Vector2 cellSize = ResolveBackgroundCellSize(palette);
            if (cellSize.x <= 0f || cellSize.y <= 0f) return false;

            ComputeHexWorldBounds(hexWidth, hexHeight, out var min, out var max);
            float worldW = max.x - min.x;
            float worldH = max.y - min.y;
            width = Mathf.Max(1, Mathf.CeilToInt(worldW / cellSize.x) + 2);
            height = Mathf.Max(1, Mathf.CeilToInt(worldH / cellSize.y) + 2);
            bool allowClamp = !UseWorldStreaming;
            if (allowClamp)
            {
                if (BackgroundMaxWidth > 0)
                    width = Mathf.Min(width, BackgroundMaxWidth);
                if (BackgroundMaxHeight > 0)
                    height = Mathf.Min(height, BackgroundMaxHeight);
                if (BackgroundMaxCells > 0)
                {
                    long cells = (long)width * height;
                    if (cells > BackgroundMaxCells)
                    {
                        float scale = Mathf.Sqrt(cells / (float)BackgroundMaxCells);
                        width = Mathf.Max(1, Mathf.FloorToInt(width / scale));
                        height = Mathf.Max(1, Mathf.FloorToInt(height / scale));
                    }
                }
            }

            int usableWidth = Mathf.Max(1, width - 2);
            int usableHeight = Mathf.Max(1, height - 2);
            cellSize = new Vector2(
                Mathf.Max(0.0001f, worldW / usableWidth),
                Mathf.Max(0.0001f, worldH / usableHeight));

            _backgroundGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            _backgroundGrid.cellSize = new Vector3(cellSize.x, cellSize.y, 0f);
            _backgroundGrid.transform.position = new Vector3(min.x + (cellSize.x * 0.5f), min.y + (cellSize.y * 0.5f), 0f);
            return true;
        }

        private Vector2 ResolveBackgroundCellSize(TileBase[] palette)
        {
            if (BackgroundCellSizeOverride != Vector2.zero)
                return BackgroundCellSizeOverride;
            if (palette == null || palette.Length == 0) return Vector2.one;
            var sprite = ExtractTileSprite(palette[0]);
            if (sprite == null) return Vector2.one;
            var size = sprite.bounds.size;
            float s = Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
            if (BackgroundCellOverlapPixels > 0f)
            {
                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                float overlapWorld = BackgroundCellOverlapPixels / ppu;
                s = Mathf.Max(0.001f, s - overlapWorld);
            }
            return new Vector2(s, s);
        }

        private void ComputeHexWorldBounds(int width, int height, out Vector2 min, out Vector2 max)
        {
            if (_hex == null || width <= 0 || height <= 0)
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return;
            }
            Vector3 p00 = _hex.GridToWorld(0, 0);
            Vector3 p10 = _hex.GridToWorld(width - 1, 0);
            Vector3 p01 = _hex.GridToWorld(0, height - 1);
            Vector3 p11 = _hex.GridToWorld(width - 1, height - 1);
            float minX = Mathf.Min(Mathf.Min(p00.x, p10.x), Mathf.Min(p01.x, p11.x));
            float maxX = Mathf.Max(Mathf.Max(p00.x, p10.x), Mathf.Max(p01.x, p11.x));
            float minY = Mathf.Min(Mathf.Min(p00.y, p10.y), Mathf.Min(p01.y, p11.y));
            float maxY = Mathf.Max(Mathf.Max(p00.y, p10.y), Mathf.Max(p01.y, p11.y));
            float hexW = Mathf.Sqrt(3f) * _hex.HexSize;
            float hexH = 2f * _hex.HexSize;
            min = new Vector2(minX - (hexW * 0.5f), minY - (hexH * 0.5f));
            max = new Vector2(maxX + (hexW * 0.5f), maxY + (hexH * 0.5f));
        }

        private static Vector2 RotateVector(Vector2 v, float radians)
        {
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                (v.x * cos) - (v.y * sin),
                (v.x * sin) + (v.y * cos));
        }

        private static void MarkDisk(bool[] mask, int width, int height, int cx, int cy, int radius, ref int count)
        {
            if (mask == null) return;
            int r = Mathf.Max(1, radius);
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= height) continue;
                int row = y * width;
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= width) continue;
                    if ((dx * dx) + (dy * dy) > r2) continue;
                    int idx = row + x;
                    if (mask[idx]) continue;
                    mask[idx] = true;
                    count++;
                }
            }
        }

        private bool[] BuildWaterMaskRect(int width, int height, int seed, out int waterCount)
        {
            waterCount = 0;
            if (width <= 0 || height <= 0) return null;

            int total = width * height;
            int target = Mathf.Clamp(Mathf.RoundToInt(total * Mathf.Clamp01(WaterCoverage)), 0, total);
            if (target <= 0) return new bool[total];

            var mask = new bool[total];
            var rng = new System.Random(seed ^ 0x5bd1e995);

            int rivers = Mathf.Max(1, RiverCount);
            int maxSteps = width + height;
            for (int r = 0; r < rivers; r++)
            {
                if (waterCount >= target) break;

                int edge = rng.Next(4);
                Vector2 pos;
                Vector2 dir;
                switch (edge)
                {
                    case 0:
                        pos = new Vector2(0, rng.Next(height));
                        dir = Vector2.right;
                        break;
                    case 1:
                        pos = new Vector2(width - 1, rng.Next(height));
                        dir = Vector2.left;
                        break;
                    case 2:
                        pos = new Vector2(rng.Next(width), 0);
                        dir = Vector2.up;
                        break;
                    default:
                        pos = new Vector2(rng.Next(width), height - 1);
                        dir = Vector2.down;
                        break;
                }

                int minWidth = Mathf.Max(1, Mathf.Min(RiverWidthMin, RiverWidthMax));
                int maxWidth = Mathf.Max(minWidth, Mathf.Max(RiverWidthMin, RiverWidthMax));
                int riverWidth = Mathf.Clamp(rng.Next(minWidth, maxWidth + 1), 1, Mathf.Max(width, height));
                float turnStrength = Mathf.Clamp(RiverTurnStrength, 0.01f, 1f);

                for (int step = 0; step < maxSteps; step++)
                {
                    int cx = Mathf.RoundToInt(pos.x);
                    int cy = Mathf.RoundToInt(pos.y);
                    MarkDisk(mask, width, height, cx, cy, riverWidth / 2, ref waterCount);
                    if (waterCount >= target) break;

                    float turn = ((float)rng.NextDouble() * 2f - 1f) * turnStrength;
                    dir = RotateVector(dir, turn);
                    dir = dir.normalized;
                    pos += dir;
                    if (pos.x < -1 || pos.y < -1 || pos.x > width || pos.y > height)
                        break;
                }
            }

            for (int attempt = 0; attempt < LakeAttempts && waterCount < target; attempt++)
            {
                int startX = rng.Next(width);
                int startY = rng.Next(height);
                int startIdx = (startY * width) + startX;
                if (mask[startIdx]) continue;

                int minLake = Mathf.Max(1, Mathf.Min(LakeMinSize, LakeMaxSize));
                int maxLake = Mathf.Max(minLake, Mathf.Max(LakeMinSize, LakeMaxSize));
                int lakeSize = Mathf.Clamp(rng.Next(minLake, maxLake + 1), 1, total);
                int lakeTarget = Mathf.Min(lakeSize, target - waterCount);
                if (lakeTarget <= 0) break;

                var frontier = new List<Vector2Int>(lakeTarget * 2);
                frontier.Add(new Vector2Int(startX, startY));
                mask[startIdx] = true;
                waterCount++;
                int lakePlaced = 1;

                int cursor = 0;
                while (lakePlaced < lakeTarget && waterCount < target && cursor < frontier.Count)
                {
                    var current = frontier[cursor++];
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                        int ny = current.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int idx = (ny * width) + nx;
                        if (mask[idx]) continue;
                        if (rng.NextDouble() < 0.55)
                        {
                            mask[idx] = true;
                            waterCount++;
                            lakePlaced++;
                            frontier.Add(new Vector2Int(nx, ny));
                            if (lakePlaced >= lakeTarget || waterCount >= target) break;
                        }
                    }
                }
            }

            if (waterCount < target)
            {
                var waterCells = new List<int>(Mathf.Max(16, waterCount));
                for (int i = 0; i < mask.Length; i++)
                {
                    if (mask[i])
                        waterCells.Add(i);
                }
                if (waterCells.Count == 0)
                {
                    int sx = rng.Next(width);
                    int sy = rng.Next(height);
                    int idx = (sy * width) + sx;
                    mask[idx] = true;
                    waterCount++;
                    waterCells.Add(idx);
                }

                int attempts = 0;
                int maxAttempts = total * 4;
                while (waterCount < target && attempts < maxAttempts)
                {
                    int baseIdx = waterCells[rng.Next(waterCells.Count)];
                    int bx = baseIdx % width;
                    int by = baseIdx / width;
                    int dir = rng.Next(4);
                    int nx = bx + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                    int ny = by + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        attempts++;
                        continue;
                    }
                    int nidx = (ny * width) + nx;
                    if (!mask[nidx])
                    {
                        mask[nidx] = true;
                        waterCount++;
                        waterCells.Add(nidx);
                    }
                    attempts++;
                }
            }

            RemoveIsolatedMaskCells(mask, width, height);
            SmoothMask(mask, width, height);
            return mask;
        }

        private bool[] BuildRockMaskFromWater(int width, int height, bool[] waterMask, int seed)
        {
            if (waterMask == null || width <= 0 || height <= 0) return null;
            int size = width * height;
            var rockMask = new bool[size];
            var dist = new int[size];
            for (int i = 0; i < size; i++) dist[i] = -1;

            var queue = new Queue<int>(size);
            for (int i = 0; i < size; i++)
            {
                if (!waterMask[i]) continue;
                dist[i] = 0;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % width;
                int y = idx / width;
                int d = dist[idx];

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nidx = (ny * width) + nx;
                    if (dist[nidx] >= 0) continue;
                    dist[nidx] = d + 1;
                    queue.Enqueue(nidx);
                }
            }

            int min = Mathf.Max(0, RockMinThickness);
            int max = Mathf.Max(min, RockMaxThickness);
            float scale = Mathf.Max(0.0001f, RockThicknessNoiseScale);
            float ox = (seed % 1000) * 0.01f;
            float oy = (seed % 1000) * 0.02f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width) + x;
                    int d = dist[idx];
                    if (d <= 0) continue;
                    float n = Mathf.PerlinNoise((x + ox) * scale, (y + oy) * scale);
                    float thickness = Mathf.Lerp(min, max, n);
                    if (d <= thickness)
                        rockMask[idx] = true;
                }
            }

            return rockMask;
        }

        // [PENV-17]
        // Rect-grid biome mask generation for streamed water, rock, and land-distance fields.
        private void BuildStreamingBackgroundMasks(int width, int height, int seed)
        {
            _backgroundMaskWidth = Mathf.Max(0, width);
            _backgroundMaskHeight = Mathf.Max(0, height);
            if (_backgroundMaskWidth <= 0 || _backgroundMaskHeight <= 0)
            {
                ClearStreamingBackgroundMasks();
                return;
            }

            _backgroundWaterMask = BuildWaterMaskRect(_backgroundMaskWidth, _backgroundMaskHeight, seed, out _);
            _backgroundRockMask = (_backgroundWaterMask != null)
                ? BuildRockMaskFromWater(_backgroundMaskWidth, _backgroundMaskHeight, _backgroundWaterMask, seed)
                : null;
            _backgroundLandDistance = BuildLandDistanceField(_backgroundMaskWidth, _backgroundMaskHeight, _backgroundWaterMask, _backgroundRockMask, out _backgroundLandMaxDistance);
            BumpBiomeMaskChunkRendererDataVersion();
        }

        private void ClearStreamingBackgroundMasks()
        {
            _backgroundWaterMask = null;
            _backgroundRockMask = null;
            _backgroundLandDistance = null;
            _backgroundLandMaxDistance = 0;
            _backgroundMaskWidth = 0;
            _backgroundMaskHeight = 0;
            BumpBiomeMaskChunkRendererDataVersion();
        }

        private static int[] BuildLandDistanceField(int width, int height, bool[] waterMask, bool[] rockMask, out int maxDistance)
        {
            maxDistance = 0;
            if (width <= 0 || height <= 0)
                return null;

            int size = width * height;
            var dist = new int[size];
            for (int i = 0; i < size; i++) dist[i] = -1;

            var queue = new Queue<int>(size);
            bool hasSeed = false;
            for (int i = 0; i < size; i++)
            {
                bool isWater = waterMask != null && waterMask[i];
                bool isRock = rockMask != null && rockMask[i];
                if (!isWater && !isRock) continue;
                dist[i] = 0;
                queue.Enqueue(i);
                hasSeed = true;
            }

            if (!hasSeed)
            {
                for (int i = 0; i < size; i++) dist[i] = 0;
                return dist;
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % width;
                int y = idx / width;
                int d = dist[idx];
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nidx = (ny * width) + nx;
                    if (dist[nidx] >= 0) continue;
                    dist[nidx] = d + 1;
                    queue.Enqueue(nidx);
                    bool isLand = (waterMask == null || !waterMask[nidx]) && (rockMask == null || !rockMask[nidx]);
                    if (isLand && dist[nidx] > maxDistance)
                        maxDistance = dist[nidx];
                }
            }

            return dist;
        }

        private static bool IsMaskInterior(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null || col <= 0 || row <= 0 || col >= width - 1 || row >= height - 1)
                return false;
            int idx = (row * width) + col;
            return mask[idx]
                && mask[idx - 1] && mask[idx + 1]
                && mask[idx - width] && mask[idx + width];
        }

        private static bool IsMaskHole(bool[] mask, int width, int height, int col, int row)
        {
            if (mask == null || col <= 0 || row <= 0 || col >= width - 1 || row >= height - 1)
                return false;
            int idx = (row * width) + col;
            return !mask[idx]
                && mask[idx - 1] && mask[idx + 1]
                && mask[idx - width] && mask[idx + width];
        }

        private static bool IsWaterCell(bool[] waterMask, int width, int height, int col, int row)
        {
            if (waterMask == null || col < 0 || row < 0 || col >= width || row >= height)
                return false;
            int idx = (row * width) + col;
            if (waterMask[idx]) return true;
            return IsMaskHole(waterMask, width, height, col, row);
        }

        private bool TryGetBackgroundCellIndex(Vector2Int cell, out int idx)
        {
            idx = 0;
            if (_backgroundGrid == null || _grid == null)
                return false;
            Vector3 world = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
            if (bgCell.x < 0 || bgCell.y < 0 || bgCell.x >= _backgroundMaskWidth || bgCell.y >= _backgroundMaskHeight)
                return false;
            idx = (bgCell.y * _backgroundMaskWidth) + bgCell.x;
            return true;
        }

        // [PENV-18]
        // Mapping helpers between gameplay cells, background cells, and cached biome masks.
        private bool TryGetBackgroundCellFlags(Vector2Int cell, out bool isWater, out bool isRock)
        {
            isWater = false;
            isRock = false;
            if (!TryGetBackgroundCellData(cell, out var data))
                return false;

            isWater = data.IsWater;
            isRock = data.IsRock;
            return true;
        }

        private bool TryGetBackgroundMaskFlags(int col, int row, out bool isWater, out bool isRock)
        {
            isWater = false;
            isRock = false;
            if (!TryGetBackgroundMaskData(col, row, out var data))
                return false;

            isWater = data.IsWater;
            isRock = data.IsRock;
            return true;
        }
    }
}
