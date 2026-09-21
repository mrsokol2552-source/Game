/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.cs
@module: presentation.pathfinding.worldgen.placement
@purpose: Hosts biome-aware prop/tree placement, blocked-cell bookkeeping, and spatial placement helpers for ProceduralEnvironment.
@entry: PENV-19, PENV-20, PENV-21, ProceduralEnvironment.BuildPlacements, ProceduralEnvironment.PlaceProps, ProceduralEnvironment.PlaceTrees
@api: partial class implementation for ProceduralEnvironment
@deps: HexPathfindingBootstrap, Tilemap/Grid, background biome masks, worldgen palettes
@data: placement candidates, blocked-cell sets, background biome counts, spatial hash buckets
@perf: hot during generation and streaming; placement loops must stay allocation-aware
@thread: main thread only
@tests: indirect coverage via repo audits, Unity recompilation, and worldgen/streaming smoke tests
@config: prop/tree coverage, min hex distance, tree biome noise, rock-prop coverage, streaming placement grid settings
@assets: prop palettes, tree palettes, rock-prop palettes, background masks
@notes: keep biome-aware placement and blocked-cell bookkeeping isolated so future world-data extraction can replace Tilemap writes without touching generation math
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-PLACEMENT]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.Placement.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private struct Placement
        {
            public Vector2Int Cell;
            public TileBase Tile;
        }

        private enum PropBiomeFilter
        {
            Any,
            LandOnly,
            RockOnly
        }

        private struct Axial
        {
            public int q;
            public int r;
        }

        private sealed class SpatialHash
        {
            private readonly int _cellSize;
            private readonly Dictionary<long, List<Vector2Int>> _buckets = new Dictionary<long, List<Vector2Int>>(1024);

            public SpatialHash(int cellSize)
            {
                _cellSize = Mathf.Max(1, cellSize);
            }

            public void Add(Vector2Int cell)
            {
                Vector2Int bucket = GetBucket(cell);
                long key = Key(bucket.x, bucket.y);
                if (!_buckets.TryGetValue(key, out var list))
                {
                    list = new List<Vector2Int>(8);
                    _buckets[key] = list;
                }

                list.Add(cell);
            }

            public bool IsFarEnough(Vector2Int cell, int minDist)
            {
                if (minDist <= 0) return true;

                int radius = Mathf.Max(1, Mathf.CeilToInt(minDist / (float)_cellSize));
                Vector2Int bucket = GetBucket(cell);
                for (int by = -radius; by <= radius; by++)
                {
                    for (int bx = -radius; bx <= radius; bx++)
                    {
                        long key = Key(bucket.x + bx, bucket.y + by);
                        if (!_buckets.TryGetValue(key, out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (HexDistance(cell, list[i]) < minDist)
                                return false;
                        }
                    }
                }

                return true;
            }

            private Vector2Int GetBucket(Vector2Int cell)
            {
                return new Vector2Int(cell.x / _cellSize, cell.y / _cellSize);
            }

            private static long Key(int x, int y)
            {
                unchecked
                {
                    return ((long)x << 32) ^ (uint)y;
                }
            }
        }

        private sealed class BlockBounds
        {
            public int MinCol;
            public int MinRow;
            public int MaxCol;
            public int MaxRow;

            public bool HasAny => MaxCol >= 0 && MaxRow >= 0;

            public void Reset(int width, int height)
            {
                MinCol = width;
                MinRow = height;
                MaxCol = -1;
                MaxRow = -1;
            }

            public void Include(int col, int row)
            {
                if (col < MinCol) MinCol = col;
                if (row < MinRow) MinRow = row;
                if (col > MaxCol) MaxCol = col;
                if (row > MaxRow) MaxRow = row;
            }
        }

        // [PENV-19]
        // Candidate caches, biome-aware placement filters, and density helpers for props and trees.
        private float GetTreePlacementWeight(Vector2Int cell)
        {
            return GetTreeDensity(cell) * GetTreeBiomeWeight(cell);
        }

        private void BuildPropCandidateCaches(int width, int height, out List<Vector2Int> landCells, out List<Vector2Int> rockCells, out List<Vector2Int> anyCells)
        {
            int total = width * height;
            landCells = new List<Vector2Int>(Mathf.Max(16, total / 2));
            rockCells = new List<Vector2Int>(Mathf.Max(8, total / 8));
            anyCells = new List<Vector2Int>(Mathf.Max(16, total));
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var cell = new Vector2Int(col, row);
                    anyCells.Add(cell);
                    if (TryGetBackgroundCellFlags(cell, out bool isWater, out bool isRock))
                    {
                        if (isWater) continue;
                        if (isRock) rockCells.Add(cell);
                        else landCells.Add(cell);
                    }
                    else
                    {
                        landCells.Add(cell);
                    }
                }
            }
        }

        private static void ShuffleList<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private bool IsAllowedPropCell(Vector2Int cell, PropBiomeFilter filter)
        {
            if (filter == PropBiomeFilter.Any) return true;
            if (TryGetBackgroundCellFlags(cell, out bool isWater, out bool isRock))
            {
                if (isWater) return false;
                if (filter == PropBiomeFilter.RockOnly) return isRock;
                return !isRock;
            }

            bool hasBiome = TryGetBiomeCountsAtCell(cell, out int waterCount, out int rockCount);
            if (!hasBiome)
                return filter != PropBiomeFilter.RockOnly;
            if (filter == PropBiomeFilter.RockOnly)
                return rockCount > 0 && rockCount >= waterCount;
            return waterCount == 0 && rockCount == 0;
        }

        private bool TryGetBiomeFlagsAtCell(Vector2Int cell, out bool isWater, out bool isRock)
        {
            isWater = false;
            isRock = false;
            if (!TryGetBiomeCountsAtCell(cell, out int waterCount, out int rockCount))
                return false;

            if (waterCount > 0 && waterCount >= rockCount)
            {
                isWater = true;
                return true;
            }

            if (rockCount > 0)
            {
                isRock = true;
                return true;
            }

            return true;
        }

        private void CacheBackgroundBiomeMasks(int width, int height, bool[] waterMask, bool[] rockMask)
        {
            if (!UseBackgroundTilemap || _background == null)
            {
                _backgroundWaterMask = null;
                _backgroundRockMask = null;
                _backgroundMaskWidth = 0;
                _backgroundMaskHeight = 0;
                _backgroundLandDistance = null;
                _backgroundLandMaxDistance = 0;
                BumpBiomeMaskChunkRendererDataVersion();
                return;
            }

            _backgroundMaskWidth = width;
            _backgroundMaskHeight = height;
            _backgroundWaterMask = waterMask;
            _backgroundRockMask = rockMask;
            _backgroundLandDistance = BuildLandDistanceField(width, height, waterMask, rockMask, out _backgroundLandMaxDistance);
            BumpBiomeMaskChunkRendererDataVersion();
        }

        private bool TryGetBiomeCountsAtCell(Vector2Int cell, out int waterCount, out int rockCount)
        {
            waterCount = 0;
            rockCount = 0;
            bool usedMask = TrySampleBackgroundBiomeCounts(cell, out waterCount, out rockCount);
            if (usedMask && (waterCount > 0 || rockCount > 0))
                return true;

            TileBase tile = null;
            if (UseBackgroundTilemap && _background != null && _backgroundGrid != null && _grid != null)
            {
                Vector3 world = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
                tile = _background.GetTile(bgCell);
            }
            else if (_ground != null)
            {
                tile = _ground.GetTile(new Vector3Int(cell.x, cell.y, 0));
            }

            if (tile == null) return false;
            string tileName = tile.name;
            string spriteName = null;
            if (tile is Tile tileAsset && tileAsset.sprite != null)
                spriteName = tileAsset.sprite.name;
            bool matchesWater = IsNameMatch(tileName, WaterTileNameKeywords)
                || IsNameMatch(tileName, WaterInteriorTileNameKeywords)
                || (!string.IsNullOrEmpty(spriteName)
                    && (IsNameMatch(spriteName, WaterTileNameKeywords)
                        || IsNameMatch(spriteName, WaterInteriorTileNameKeywords)));
            bool matchesRock = IsNameMatch(tileName, RockTileNameKeywords)
                || (!string.IsNullOrEmpty(spriteName) && IsNameMatch(spriteName, RockTileNameKeywords));
            if (matchesWater) waterCount = 1;
            if (matchesRock) rockCount = 1;
            return true;
        }

        private bool TrySampleBackgroundBiomeCounts(Vector2Int cell, out int waterCount, out int rockCount)
        {
            waterCount = 0;
            rockCount = 0;
            if (!UseBackgroundTilemap || _backgroundGrid == null || _grid == null)
                return false;
            if (_backgroundWaterMask == null || _backgroundMaskWidth <= 0 || _backgroundMaskHeight <= 0)
                return false;

            Vector3 center = _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            float cellSizeX = Mathf.Abs(_grid.cellSize.x);
            float cellSizeY = Mathf.Abs(_grid.cellSize.y);
            int sampleCount = 0;
            for (int i = 0; i < BiomeSampleOffsets.Length; i++)
            {
                var offset = BiomeSampleOffsets[i];
                Vector3 world = center + new Vector3(offset.x * cellSizeX, offset.y * cellSizeY, 0f);
                Vector3Int bgCell = _backgroundGrid.WorldToCell(world);
                int sx = bgCell.x;
                int sy = bgCell.y;
                if (!TryGetBackgroundMaskData(sx, sy, out var data))
                    continue;
                sampleCount++;
                if (data.IsWater)
                {
                    waterCount++;
                    continue;
                }

                if (data.IsRock)
                {
                    rockCount++;
                }
            }

            return sampleCount > 0;
        }

        private float GetTreeDensity(Vector2Int cell)
        {
            if (_backgroundLandDistance == null || _backgroundLandDistance.Length == 0 || _backgroundLandMaxDistance <= 0)
                return 1f;
            if (!TryGetBackgroundCellData(cell, out var data))
                return 1f;
            int dist = data.LandDistance;
            float t = _backgroundLandMaxDistance > 0 ? Mathf.Clamp01(dist / (float)_backgroundLandMaxDistance) : 1f;
            float shaped = Mathf.Pow(t, TreeGradientPower);
            return Mathf.Lerp(TreeGradientEdge, 1f, shaped);
        }

        private float GetTreeBiomeWeight(Vector2Int cell)
        {
            if (!UseTreeBiomeNoise || TreeBiomeScale <= 0f)
                return 1f;
            float scale = Mathf.Max(0.0001f, TreeBiomeScale);
            int octaves = Mathf.Max(1, TreeBiomeOctaves);
            float persistence = Mathf.Clamp01(TreeBiomePersistence);
            float lacunarity = Mathf.Max(0.01f, TreeBiomeLacunarity);
            float threshold = Mathf.Clamp01(TreeBiomeThreshold);
            float feather = Mathf.Clamp01(TreeBiomeFeather);

            int q = cell.x - (cell.y - (cell.y & 1)) / 2;
            int r = cell.y;
            const float sqrt3Over2 = 0.8660254f;
            float wx = q + (r * 0.5f);
            float wy = r * sqrt3Over2;

            float nx = (wx + _treeNoiseOffset.x) * scale;
            float ny = (wy + _treeNoiseOffset.y) * scale;
            float v = FractalNoise(nx, ny, octaves, persistence, lacunarity);
            v = AdjustContrast(v, TreeBiomeContrast);

            if (feather <= 0f)
                return v >= threshold ? 1f : 0f;

            float t0 = threshold - feather;
            float t1 = threshold + feather;
            return Mathf.Clamp01((v - t0) / Mathf.Max(0.0001f, t1 - t0));
        }

        // [PENV-20]
        // Placement building, batching, and tile application for props and trees.
        private void BuildPlacements(
            List<Placement> output,
            List<Vector2Int> candidates,
            int targetCount,
            int minHexDistance,
            System.Random rng,
            TileBase[] palette,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            SpatialHash hash,
            Func<Vector2Int, float> weightFunc,
            bool markBlocked,
            BlockBounds bounds)
        {
            if (targetCount <= 0 || palette == null || palette.Length == 0 || candidates == null || candidates.Count == 0)
                return;

            ShuffleList(candidates, rng);
            for (int i = 0; i < candidates.Count && output.Count < targetCount; i++)
            {
                var cell = candidates[i];
                if (occupiedSet.Contains(cell)) continue;
                if (minHexDistance > 0 && hash != null && !hash.IsFarEnough(cell, minHexDistance)) continue;
                float weight = weightFunc != null ? weightFunc(cell) : 1f;
                if (weight <= 0f) continue;
                if (weight < 1f && rng.NextDouble() > weight) continue;

                var tile = PickTile(rng, palette);
                output.Add(new Placement { Cell = cell, Tile = tile });
                occupied.Add(cell);
                occupiedSet.Add(cell);
                hash?.Add(cell);
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
            }
        }

        private IEnumerator BuildPlacementsBatched(
            List<Placement> output,
            List<Vector2Int> candidates,
            int targetCount,
            int minHexDistance,
            System.Random rng,
            TileBase[] palette,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            SpatialHash hash,
            Func<Vector2Int, float> weightFunc,
            bool markBlocked,
            BlockBounds bounds)
        {
            if (targetCount <= 0 || palette == null || palette.Length == 0 || candidates == null || candidates.Count == 0)
                yield break;

            ShuffleList(candidates, rng);
            int perFrame = Mathf.Max(1, PropCandidatesPerFrame);
            int processed = 0;
            for (int i = 0; i < candidates.Count && output.Count < targetCount; i++)
            {
                var cell = candidates[i];
                if (occupiedSet.Contains(cell)) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                if (minHexDistance > 0 && hash != null && !hash.IsFarEnough(cell, minHexDistance)) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                float weight = weightFunc != null ? weightFunc(cell) : 1f;
                if (weight <= 0f) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }
                if (weight < 1f && rng.NextDouble() > weight) { processed++; if (processed >= perFrame) { processed = 0; yield return null; } continue; }

                var tile = PickTile(rng, palette);
                output.Add(new Placement { Cell = cell, Tile = tile });
                occupied.Add(cell);
                occupiedSet.Add(cell);
                hash?.Add(cell);
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);

                processed++;
                if (processed >= perFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        private static void ApplyPlacements(Tilemap map, List<Placement> placements)
        {
            if (map == null || placements == null || placements.Count == 0) return;
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                map.SetTile(new Vector3Int(placement.Cell.x, placement.Cell.y, 0), placement.Tile);
            }
        }

        private void PlaceProps(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            PropBiomeFilter biomeFilter,
            List<Placement> output = null)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) return;
            int attempts = 0;
            int placed = 0;
            while (placed < targetCount && attempts < targetCount * 50)
            {
                attempts++;
                if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                    continue;
                if (!IsAllowedPropCell(cell, biomeFilter))
                    continue;

                occupied.Add(cell);
                occupiedSet.Add(cell);
                var tile = PickTile(rng, palette);
                map.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
                output?.Add(new Placement { Cell = cell, Tile = tile });
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
                placed++;
            }
        }

        private IEnumerator PlacePropsBatched(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            PropBiomeFilter biomeFilter,
            List<Placement> output = null)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) yield break;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 50;
            int attemptsPerFrame = Mathf.Max(1, PropAttemptsPerFrame);
            while (placed < targetCount && attempts < maxAttempts)
            {
                int frameAttempts = attemptsPerFrame;
                for (int i = 0; i < frameAttempts && placed < targetCount && attempts < maxAttempts; i++)
                {
                    attempts++;
                    if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                        continue;
                    if (!IsAllowedPropCell(cell, biomeFilter))
                        continue;

                    occupied.Add(cell);
                    occupiedSet.Add(cell);
                    var tile = PickTile(rng, palette);
                    map.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
                    output?.Add(new Placement { Cell = cell, Tile = tile });
                    bounds?.Include(cell.x, cell.y);
                    if (markBlocked)
                        MarkBlockedCell(cell);
                    placed++;
                }

                yield return null;
            }
        }

        private void PlaceTrees(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            List<Placement> output = null)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) return;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 200;
            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;
                if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                    continue;
                if (!IsAllowedPropCell(cell, PropBiomeFilter.LandOnly))
                    continue;
                float density = GetTreeDensity(cell);
                float biomeWeight = GetTreeBiomeWeight(cell);
                if (rng.NextDouble() > density * biomeWeight)
                    continue;

                occupied.Add(cell);
                occupiedSet.Add(cell);
                var tile = PickTile(rng, palette);
                map.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
                output?.Add(new Placement { Cell = cell, Tile = tile });
                bounds?.Include(cell.x, cell.y);
                if (markBlocked)
                    MarkBlockedCell(cell);
                placed++;
            }
        }

        private IEnumerator PlaceTreesBatched(
            Tilemap map,
            int width,
            int height,
            System.Random rng,
            TileBase[] palette,
            int targetCount,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            BlockBounds bounds,
            bool markBlocked,
            List<Placement> output = null)
        {
            if (map == null || palette == null || palette.Length == 0 || targetCount <= 0) yield break;
            int attempts = 0;
            int placed = 0;
            int maxAttempts = targetCount * 200;
            int attemptsPerFrame = Mathf.Max(1, PropAttemptsPerFrame);
            while (placed < targetCount && attempts < maxAttempts)
            {
                int frameAttempts = attemptsPerFrame;
                for (int i = 0; i < frameAttempts && placed < targetCount && attempts < maxAttempts; i++)
                {
                    attempts++;
                    if (!TryPickCell(width, height, rng, minHexDistance, occupied, occupiedSet, out var cell))
                        continue;
                    if (!IsAllowedPropCell(cell, PropBiomeFilter.LandOnly))
                        continue;
                    float density = GetTreeDensity(cell);
                    float biomeWeight = GetTreeBiomeWeight(cell);
                    if (rng.NextDouble() > density * biomeWeight)
                        continue;

                    occupied.Add(cell);
                    occupiedSet.Add(cell);
                    var tile = PickTile(rng, palette);
                    map.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
                    output?.Add(new Placement { Cell = cell, Tile = tile });
                    bounds?.Include(cell.x, cell.y);
                    if (markBlocked)
                        MarkBlockedCell(cell);
                    placed++;
                }

                yield return null;
            }
        }

        private static bool TryPickCell(
            int width,
            int height,
            System.Random rng,
            int minHexDistance,
            List<Vector2Int> occupied,
            HashSet<Vector2Int> occupiedSet,
            out Vector2Int cell)
        {
            int col = rng.Next(0, width);
            int row = rng.Next(0, height);
            cell = new Vector2Int(col, row);
            if (occupiedSet.Contains(cell)) return false;
            if (minHexDistance > 0)
            {
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (HexDistance(cell, occupied[i]) < minHexDistance)
                        return false;
                }
            }

            return true;
        }

        // [PENV-21]
        // Blocked-cell bookkeeping, spatial hashing, and hex distance helpers for placement.
        private void MarkBlockedCell(Vector2Int cell)
        {
            if (_hex == null) return;
            if (_directBlockedSet.Contains(cell)) return;
            _directBlockedSet.Add(cell);
            _directBlockedCells.Add(cell);
            _hex.SetWalkable(cell.x, cell.y, false);
        }

        private void ClearDirectBlocks()
        {
            if (_hex == null || _directBlockedCells.Count == 0) return;
            for (int i = 0; i < _directBlockedCells.Count; i++)
            {
                var cell = _directBlockedCells[i];
                _hex.SetWalkable(cell.x, cell.y, true);
            }

            _directBlockedCells.Clear();
            _directBlockedSet.Clear();
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            Axial aa = OddRToAxial(a.x, a.y);
            Axial bb = OddRToAxial(b.x, b.y);
            int dx = aa.q - bb.q; if (dx < 0) dx = -dx;
            int dz = aa.r - bb.r; if (dz < 0) dz = -dz;
            int dy = -(aa.q + aa.r) - (-(bb.q + bb.r));
            if (dy < 0) dy = -dy;
            return (dx + dy + dz) / 2;
        }

        private static Axial OddRToAxial(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new Axial { q = q, r = r };
        }
    }
}
