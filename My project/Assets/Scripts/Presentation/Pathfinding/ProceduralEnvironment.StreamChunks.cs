/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.cs
@module: presentation.pathfinding.worldgen.stream_chunks
@purpose: Holds per-chunk streaming generation, background fill, streamed props/trees placement, biome helpers, and chunk cleanup for ProceduralEnvironment.
@entry: PENV-06, PENV-07, PENV-08, PENV-09, PENV-10, ProceduralEnvironment.GenerateStreamChunk
@api: partial class implementation for ProceduralEnvironment
@deps: Tilemap/Grid, HexPathfindingBootstrap, background biome masks, streaming palettes and variants
@data: StreamChunkState, biome masks, tile variant caches, blocked-cell tracking
@perf: hotpath; chunk generation and cleanup run repeatedly during streaming
@thread: main thread only
@tests: indirect coverage via repo audits, Unity recompilation, and streaming smoke tests
@config: streaming, water/rock biome, prop/tree density, runtime tile conversion settings in ProceduralEnvironment
@assets: GroundTiles, Water/Rock variants, PropTiles, RockPropTiles, TreeTiles
@notes: keep chunk generation/cleanup separate from streaming orchestration and far-view helpers while decomposing ProceduralEnvironment
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMCHUNKS]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.StreamChunks.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        // [PENV-06]
        // Per-chunk generation entry point for ground/background tiles plus streamed decorations.
        private void GenerateStreamChunk(Vector2Int coord)
        {
            if (!_streamChunks.TryGetValue(coord, out var state))
                return;

            var hexBounds = state.HexBounds;
            if (hexBounds.size.x <= 0 || hexBounds.size.y <= 0)
                return;

            var rng = new System.Random(HashChunkSeed(coord));
            StreamChunkWorldData chunkData = null;
            if (UseBackgroundTilemap && _background != null)
            {
                var bgBounds = state.BackgroundBounds;
                if (bgBounds.size.x > 0 && bgBounds.size.y > 0)
                {
                    chunkData = ExtractStreamChunkWorldData(state);
                    FillStreamBackgroundChunk(ref state, rng, chunkData);
                }
            }
            else if (_ground != null)
            {
                var tiles = new TileBase[hexBounds.size.x * hexBounds.size.y];
                int idx = 0;
                for (int y = 0; y < hexBounds.size.y; y++)
                {
                    int row = hexBounds.yMin + y;
                    for (int x = 0; x < hexBounds.size.x; x++)
                    {
                        int col = hexBounds.xMin + x;
                        var biome = GetStreamBiome(col, row);
                        var palette = ResolveStreamPalette(biome);
                        tiles[idx++] = PickTile(rng, palette);
                    }
                }
                _ground.SetTilesBlock(hexBounds, tiles);
                CaptureStreamGroundRenderPayload(ref state, hexBounds, tiles);
            }

            if (StreamIncludeProps || StreamIncludeTrees)
            {
                state.PropPlacements = new List<Placement>();
                state.BlockerPlacements = new List<Placement>();
                bool useBackgroundForProps = StreamPropsUseBackgroundGrid
                    && UseBackgroundTilemap
                    && _background != null
                    && _backgroundGrid != null;
                if (useBackgroundForProps)
                {
                    if (chunkData == null)
                        chunkData = ExtractStreamChunkWorldData(state);
                    PlaceStreamPropsAndTreesBackground(state, rng, chunkData);
                }
                else
                    PlaceStreamPropsAndTrees(state, rng);
            }

            state.Generated = true;
            state.LastTouchedFrame = Time.frameCount;
            BumpFarViewTileSnapshotVersion();
            BumpCachedBackgroundFarViewPayloadVersion();
            BumpGroundPayloadChunkRendererStreamVersion();
            BumpBiomeMaskChunkRendererDataVersion();
            _streamChunks[coord] = state;
        }

        // [PENV-07]
        // Rect-grid background synthesis with biome masks, edge matching, anti-repeat,
        // and transform selection for square ground tiles.
        private void FillStreamBackgroundChunk(ref StreamChunkState state, System.Random rng, StreamChunkWorldData chunkData)
        {
            if (_background == null) return;
            var bgBounds = state.BackgroundBounds;
            if (bgBounds.size.x <= 0 || bgBounds.size.y <= 0) return;

            int width = bgBounds.size.x;
            int height = bgBounds.size.y;
            var tiles = new TileBase[width * height];

            bool useVariants = UseGroundTileRandomRotation
                || GroundTileMirrorX
                || GroundTileMirrorY
                || UseGroundTileEdgeColorMatch
                || UseWaterEdgeColorMatch
                || UseGroundTileAntiRepeat;
            bool useTransform = UseGroundTileRandomRotation || GroundTileMirrorX || GroundTileMirrorY;
            EdgeProfile[] placedProfiles = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch) ? new EdgeProfile[tiles.Length] : null;
            bool[] placedValid = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new bool[tiles.Length] : null;
            int[] placedVariantIds = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new int[tiles.Length] : null;
            Matrix4x4[] transforms = useTransform ? new Matrix4x4[tiles.Length] : null;

            bool useWaterMask = UseWaterBiome && chunkData != null && chunkData.HasBackgroundCells;
            bool useSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f && _streamSharedVariants != null && _streamSharedVariants.Length > 0;

            for (int y = 0; y < height; y++)
            {
                int row = bgBounds.yMin + y;
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int col = bgBounds.xMin + x;
                    int idx = rowBase + x;

                    var biomeData = ResolveBackgroundBiomeData(chunkData, col, row);
                    bool isWater = useWaterMask && biomeData.IsWater;
                    bool isRock = useWaterMask && biomeData.IsRock;
                    bool isWaterInterior = useWaterMask && biomeData.IsWaterInterior;

                    if (!useVariants)
                    {
                        TileBase[] palette;
                        if (isWater && _streamWaterTiles != null && _streamWaterTiles.Length > 0)
                            palette = (isWaterInterior && _streamWaterInteriorTiles != null && _streamWaterInteriorTiles.Length > 0)
                                ? _streamWaterInteriorTiles
                                : _streamWaterTiles;
                        else if (isRock && _streamRockTiles != null && _streamRockTiles.Length > 0)
                            palette = _streamRockTiles;
                        else
                            palette = _streamLandTiles;

                        if (palette == null || palette.Length == 0)
                            continue;

                        var picked = PickTileDeterministic(palette, col, row, _streamSeed);
                        if (!isWater && !isRock && (_streamWaterSet != null || _streamRockSet != null))
                        {
                            bool disallowed = (_streamWaterSet != null && _streamWaterSet.Contains(picked))
                                || (_streamRockSet != null && _streamRockSet.Contains(picked));
                            if (disallowed && _streamLandTiles != null && _streamLandTiles.Length > 0)
                                picked = PickTileDeterministic(_streamLandTiles, col, row, _streamSeed);
                        }
                        if (!isWater && !isRock && IsTileExcludedByNameOrSprite(picked, _streamWaterExclude))
                        {
                            if (_streamLandTiles != null && _streamLandTiles.Length > 0)
                                picked = PickTileDeterministic(_streamLandTiles, col, row, _streamSeed);
                            else
                                picked = PickTileDeterministicExcluding(palette, col, row, _streamSeed, _streamWaterExclude);
                        }
                        tiles[idx] = picked;
                        continue;
                    }

                    TileVariant[] variants;
                    if (isWater && _streamWaterVariants != null && _streamWaterVariants.Length > 0)
                        variants = (isWaterInterior && _streamWaterInteriorVariants != null && _streamWaterInteriorVariants.Length > 0)
                            ? _streamWaterInteriorVariants
                            : _streamWaterVariants;
                    else if (isRock && _streamRockVariants != null && _streamRockVariants.Length > 0)
                        variants = _streamRockVariants;
                    else
                        variants = useSharedTiles ? _streamSharedVariants : _streamLandVariants;

                    if (variants == null || variants.Length == 0)
                        continue;

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

                    if (placedValid != null)
                    {
                        if (x > 0)
                        {
                            int leftIdx = idx - 1;
                            if (placedValid[leftIdx])
                            {
                                if (placedProfiles != null && placedProfiles[leftIdx].RightGreen)
                                    requireLeftGreen = true;
                                if (placedProfiles != null)
                                {
                                    hasLeftWaterMask = true;
                                    leftWaterMask = placedProfiles[leftIdx].RightWaterMask;
                                }
                                if (placedVariantIds != null)
                                    leftId = placedVariantIds[leftIdx];
                            }
                        }
                        if (y > 0)
                        {
                            int bottomIdx = idx - width;
                            if (placedValid[bottomIdx])
                            {
                                if (placedProfiles != null && placedProfiles[bottomIdx].TopGreen)
                                    requireBottomGreen = true;
                                if (placedProfiles != null)
                                {
                                    hasBottomWaterMask = true;
                                    bottomWaterMask = placedProfiles[bottomIdx].TopWaterMask;
                                }
                                if (placedVariantIds != null)
                                    bottomId = placedVariantIds[bottomIdx];
                            }
                        }
                    }

                    bool enforceEdge = UseGroundTileEdgeColorMatch && !isWater && !isRock;
                    bool enforceWaterEdge = false;
                    if (UseWaterEdgeColorMatch && useWaterMask)
                    {
                        var leftBiome = ResolveBackgroundBiomeData(chunkData, col - 1, row);
                        var bottomBiome = ResolveBackgroundBiomeData(chunkData, col, row - 1);
                        var rightBiome = ResolveBackgroundBiomeData(chunkData, col + 1, row);
                        var topBiome = ResolveBackgroundBiomeData(chunkData, col, row + 1);
                        bool leftWater = leftBiome.IsWater;
                        bool bottomWater = bottomBiome.IsWater;
                        bool rightWater = rightBiome.IsWater;
                        bool topWater = topBiome.IsWater;
                        bool hasWaterNeighbor = leftWater || bottomWater || rightWater || topWater;

                        enforceWaterEdge = isWater || isRock || hasWaterNeighbor;
                        if (enforceWaterEdge)
                        {
                            if (col > 0)
                                requireLeftWater = leftWater ? 1 : -1;
                            if (row > 0)
                                requireBottomWater = bottomWater ? 1 : -1;
                            if (col < _backgroundMaskWidth - 1)
                                requireRightWater = rightWater ? 1 : -1;
                            if (row < _backgroundMaskHeight - 1)
                                requireTopWater = topWater ? 1 : -1;
                        }
                    }

                    var variant = PickVariantWithConstraints(
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
                        _streamSeed);

                    if (!isWater && !isRock && IsTileExcludedByNameOrSprite(variant.Tile, _streamWaterExclude))
                    {
                        if (_streamLandVariants != null && _streamLandVariants.Length > 0)
                        {
                            variant = PickVariantWithConstraints(
                                _streamLandVariants,
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
                                _streamSeed);
                        }
                        else
                        {
                            variant = PickVariantDeterministicExcluding(variants, col, row, _streamSeed, _streamWaterExclude);
                        }
                    }

                    tiles[idx] = variant.Tile;
                    if (transforms != null)
                        transforms[idx] = variant.Transform;
                    if (placedValid != null)
                    {
                        if (placedProfiles != null)
                            placedProfiles[idx] = variant.Profile;
                        placedValid[idx] = variant.Tile != null;
                        if (placedVariantIds != null)
                            placedVariantIds[idx] = variant.Id;
                    }

                    if (!isWater && !isRock && variant.Tile != null && (_streamWaterSet != null || _streamRockSet != null))
                    {
                        bool disallowed = (_streamWaterSet != null && _streamWaterSet.Contains(variant.Tile))
                            || (_streamRockSet != null && _streamRockSet.Contains(variant.Tile));
                        if (disallowed && _streamLandVariants != null && _streamLandVariants.Length > 0)
                        {
                            var landVariant = PickVariantWithConstraints(
                                _streamLandVariants,
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
                                _streamSeed);
                            tiles[idx] = landVariant.Tile;
                            if (transforms != null)
                                transforms[idx] = landVariant.Transform;
                            if (placedValid != null)
                            {
                                if (placedProfiles != null)
                                    placedProfiles[idx] = landVariant.Profile;
                                placedValid[idx] = landVariant.Tile != null;
                                if (placedVariantIds != null)
                                    placedVariantIds[idx] = landVariant.Id;
                            }
                        }
                    }
                }
            }

            _background.SetTilesBlock(bgBounds, tiles);
            if (transforms != null)
            {
                for (int y = 0; y < height; y++)
                {
                    int row = bgBounds.yMin + y;
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int col = bgBounds.xMin + x;
                        var transform = transforms[rowBase + x];
                        _background.SetTransformMatrix(new Vector3Int(col, row, 0), transform);
                    }
                }
            }

            CaptureStreamBackgroundRenderPayload(ref state, bgBounds, tiles, transforms, chunkData);
        }

        // [PENV-08]
        // Hex-grid tree/prop placement with biome-aware filtering for land, water, and rock cells.
        private void PlaceStreamPropsAndTrees(StreamChunkState state, System.Random rng)
        {
            var hexBounds = state.HexBounds;
            if (hexBounds.size.x <= 0 || hexBounds.size.y <= 0) return;

            Tilemap propMap = _props;
            Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
            if (treeMap == null && propMap == null) return;

            double propChance = Mathf.Clamp01(PropCoverage);
            double rockPropChance = Mathf.Clamp01(RockPropCoverage);
            double treeChance = Mathf.Clamp01(TreeCoverage);
            double accentChance = Mathf.Clamp01(TreeAccentCoverage);

            for (int y = 0; y < hexBounds.size.y; y++)
            {
                int row = hexBounds.yMin + y;
                for (int x = 0; x < hexBounds.size.x; x++)
                {
                    int col = hexBounds.xMin + x;
                    if (UseBackgroundTilemap && _backgroundGrid != null)
                    {
                        if (TryGetBackgroundCellFlags(new Vector2Int(col, row), out bool isWater, out bool isRock))
                        {
                            if (isWater)
                                continue;
                            if (isRock)
                            {
                                if (StreamIncludeProps && propMap != null && RockPropTiles != null && RockPropTiles.Length > 0 && rockPropChance > 0.0)
                                {
                                    if (rng.NextDouble() < rockPropChance)
                                    {
                                        var rockTile = PickTile(rng, RockPropTiles);
                                        if (rockTile != null)
                                        {
                                            propMap.SetTile(new Vector3Int(col, row, 0), rockTile);
                                            AddStreamPlacement(state, propMap, new Vector2Int(col, row), rockTile);
                                        }
                                    }
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var biome = GetStreamBiome(col, row);
                        if (biome == StreamBiomeType.Water)
                            continue;
                        if (biome == StreamBiomeType.Rock)
                        {
                            if (StreamIncludeProps && propMap != null && RockPropTiles != null && RockPropTiles.Length > 0 && rockPropChance > 0.0)
                            {
                                if (rng.NextDouble() < rockPropChance)
                                {
                                    var rockTile = PickTile(rng, RockPropTiles);
                                    if (rockTile != null)
                                    {
                                        propMap.SetTile(new Vector3Int(col, row, 0), rockTile);
                                        AddStreamPlacement(state, propMap, new Vector2Int(col, row), rockTile);
                                    }
                                }
                            }
                            continue;
                        }
                    }

                    var cell = new Vector3Int(col, row, 0);
                    if (StreamIncludeTrees && treeMap != null && _streamTreeTiles != null && _streamTreeTiles.Length > 0)
                    {
                        float treeWeight = GetTreePlacementWeight(new Vector2Int(col, row));
                        if (treeWeight > 0f && rng.NextDouble() < treeChance * treeWeight)
                        {
                            var tile = PickTile(rng, _streamTreeTiles);
                            if (tile != null)
                            {
                                treeMap.SetTile(cell, tile);
                                AddStreamPlacement(state, treeMap, new Vector2Int(col, row), tile);
                                if (UseDirectWalkableUpdates && TreesBlockMovement && state.BlockedCells != null)
                                {
                                    _hex.SetWalkable(col, row, false);
                                    state.BlockedCells.Add(new Vector2Int(col, row));
                                }
                            }
                            continue;
                        }
                        if (_streamTreeAccentTiles != null && _streamTreeAccentTiles.Length > 0 && treeWeight > 0f && rng.NextDouble() < accentChance * treeWeight)
                        {
                            var tile = PickTile(rng, _streamTreeAccentTiles);
                            if (tile != null)
                            {
                                treeMap.SetTile(cell, tile);
                                AddStreamPlacement(state, treeMap, new Vector2Int(col, row), tile);
                                if (UseDirectWalkableUpdates && TreesBlockMovement && state.BlockedCells != null)
                                {
                                    _hex.SetWalkable(col, row, false);
                                    state.BlockedCells.Add(new Vector2Int(col, row));
                                }
                            }
                            continue;
                        }
                    }

                    if (StreamIncludeProps && propMap != null && _streamPropTiles != null && _streamPropTiles.Length > 0)
                    {
                        if (rng.NextDouble() < propChance)
                        {
                            var tile = PickTile(rng, _streamPropTiles);
                            if (tile != null)
                            {
                                propMap.SetTile(cell, tile);
                                AddStreamPlacement(state, propMap, new Vector2Int(col, row), tile);
                                if (UseDirectWalkableUpdates && PropsBlockMovement && state.BlockedCells != null)
                                {
                                    _hex.SetWalkable(col, row, false);
                                    state.BlockedCells.Add(new Vector2Int(col, row));
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddStreamPlacement(StreamChunkState state, Tilemap map, Vector2Int cell, TileBase tile)
        {
            if (tile == null)
                return;
            if (map == _blockers)
                state.BlockerPlacements?.Add(new Placement { Cell = cell, Tile = tile });
            else
                state.PropPlacements?.Add(new Placement { Cell = cell, Tile = tile });
        }

        // [PENV-09]
        // Background-grid tree/prop placement using extracted biome data so render/placement logic does not read raw masks directly.
        private void PlaceStreamPropsAndTreesBackground(StreamChunkState state, System.Random rng, StreamChunkWorldData chunkData)
        {
            if (_background == null) return;
            var bgBounds = state.BackgroundBounds;
            if (bgBounds.size.x <= 0 || bgBounds.size.y <= 0) return;

            Tilemap propMap = _props;
            Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
            if (treeMap == null && propMap == null) return;

            double propChance = Mathf.Clamp01(PropCoverage);
            double rockPropChance = Mathf.Clamp01(RockPropCoverage);
            double treeChance = Mathf.Clamp01(TreeCoverage);
            double accentChance = Mathf.Clamp01(TreeAccentCoverage);

            for (int y = 0; y < bgBounds.size.y; y++)
            {
                int row = bgBounds.yMin + y;
                for (int x = 0; x < bgBounds.size.x; x++)
                {
                    int col = bgBounds.xMin + x;
                    if (UseBackgroundTilemap && _backgroundGrid != null)
                    {
                        var biomeData = ResolveBackgroundBiomeData(chunkData, col, row);
                        if (biomeData.InBounds)
                        {
                            if (biomeData.IsWater)
                                continue;
                            if (biomeData.IsRock)
                            {
                                if (StreamIncludeProps && propMap != null && RockPropTiles != null && RockPropTiles.Length > 0 && rockPropChance > 0.0)
                                {
                                    if (rng.NextDouble() < rockPropChance)
                                    {
                                        var rockTile = PickTile(rng, RockPropTiles);
                                        if (rockTile != null)
                                        {
                                            propMap.SetTile(new Vector3Int(col, row, 0), rockTile);
                                            AddStreamPlacement(state, propMap, new Vector2Int(col, row), rockTile);
                                        }
                                    }
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var biome = GetStreamBiome(col, row);
                        if (biome == StreamBiomeType.Water)
                            continue;
                        if (biome == StreamBiomeType.Rock)
                        {
                            if (StreamIncludeProps && propMap != null && RockPropTiles != null && RockPropTiles.Length > 0 && rockPropChance > 0.0)
                            {
                                if (rng.NextDouble() < rockPropChance)
                                {
                                    var rockTile = PickTile(rng, RockPropTiles);
                                    if (rockTile != null)
                                    {
                                        propMap.SetTile(new Vector3Int(col, row, 0), rockTile);
                                        AddStreamPlacement(state, propMap, new Vector2Int(col, row), rockTile);
                                    }
                                }
                            }
                            continue;
                        }
                    }

                    var cell = new Vector3Int(col, row, 0);
                    bool placed = false;
                    if (StreamIncludeTrees && treeMap != null && _streamTreeTiles != null && _streamTreeTiles.Length > 0)
                    {
                        var world = _background.GetCellCenterWorld(cell);
                        var hexCell = _hex != null ? _hex.WorldToGrid(world) : new Vector2Int(col, row);
                        float treeWeight = GetTreePlacementWeight(hexCell);
                        if (treeWeight > 0f && rng.NextDouble() < treeChance * treeWeight)
                        {
                            var tile = PickTile(rng, _streamTreeTiles);
                            if (tile != null)
                            {
                                treeMap.SetTile(cell, tile);
                                AddStreamPlacement(state, treeMap, new Vector2Int(col, row), tile);
                                placed = true;
                            }
                        }
                        else if (_streamTreeAccentTiles != null && _streamTreeAccentTiles.Length > 0 && treeWeight > 0f && rng.NextDouble() < accentChance * treeWeight)
                        {
                            var tile = PickTile(rng, _streamTreeAccentTiles);
                            if (tile != null)
                            {
                                treeMap.SetTile(cell, tile);
                                AddStreamPlacement(state, treeMap, new Vector2Int(col, row), tile);
                                placed = true;
                            }
                        }
                    }

                    if (!placed && StreamIncludeProps && propMap != null && _streamPropTiles != null && _streamPropTiles.Length > 0)
                    {
                        if (rng.NextDouble() < propChance)
                        {
                            var tile = PickTile(rng, _streamPropTiles);
                            if (tile != null)
                            {
                                propMap.SetTile(cell, tile);
                                AddStreamPlacement(state, propMap, new Vector2Int(col, row), tile);
                                placed = true;
                            }
                        }
                    }

                    if (UseDirectWalkableUpdates && placed)
                    {
                        if (_hex != null)
                        {
                            var world = _background.GetCellCenterWorld(cell);
                            var hexCell = _hex.WorldToGrid(world);
                            _hex.SetWalkable(hexCell.x, hexCell.y, false);
                            if (state.BlockedCells != null)
                                state.BlockedCells.Add(hexCell);
                        }
                    }
                }
            }
        }

        // [PENV-10]
        // Chunk eviction and cleanup, including tilemap clearing and walkability restoration.
        private void UnloadDistantChunks(Vector2Int center)
        {
            if (StreamKeepGeneratedChunks || StreamBakeAllChunks)
                return;
            int baseRadius = Mathf.Max(StreamActiveRadius, StreamPrefetchRadius);
            int unloadRadius = Mathf.Max(StreamUnloadRadius, baseRadius + 1);
            if (unloadRadius <= 0) return;
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _streamChunks)
            {
                var coord = kvp.Key;
                int dist = Mathf.Max(Mathf.Abs(coord.x - center.x), Mathf.Abs(coord.y - center.y));
                if (dist <= unloadRadius) continue;
                ClearStreamChunk(kvp.Value);
                toRemove.Add(coord);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                _streamChunks.Remove(toRemove[i]);
                _streamUnloadedChunkTotal++;
            }
        }

        private void UnloadDistantChunks(Vector2Int minChunk, Vector2Int maxChunk)
        {
            if (StreamKeepGeneratedChunks || StreamBakeAllChunks)
                return;
            int pad = Mathf.Max(0, StreamUnloadRadius);
            int minX = minChunk.x - pad;
            int minY = minChunk.y - pad;
            int maxX = maxChunk.x + pad;
            int maxY = maxChunk.y + pad;
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _streamChunks)
            {
                var coord = kvp.Key;
                if (coord.x >= minX && coord.x <= maxX && coord.y >= minY && coord.y <= maxY)
                    continue;
                ClearStreamChunk(kvp.Value);
                toRemove.Add(coord);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                _streamChunks.Remove(toRemove[i]);
                _streamUnloadedChunkTotal++;
            }
        }

        private void EnforceStreamCacheLimit()
        {
            if (StreamKeepGeneratedChunks || StreamBakeAllChunks)
                return;
            int limit = Mathf.Max(0, StreamMaxLoadedChunks);
            if (_streamRequiredChunkCount > 0 && limit > 0 && limit < _streamRequiredChunkCount)
                limit = _streamRequiredChunkCount;
            if (limit == 0 || _streamChunks.Count <= limit) return;
            int removeCount = _streamChunks.Count - limit;
            for (int i = 0; i < removeCount; i++)
            {
                Vector2Int oldestKey = default;
                int oldestFrame = int.MaxValue;
                bool found = false;
                foreach (var kvp in _streamChunks)
                {
                    if (kvp.Value.LastTouchedFrame < oldestFrame)
                    {
                        oldestFrame = kvp.Value.LastTouchedFrame;
                        oldestKey = kvp.Key;
                        found = true;
                    }
                }
                if (!found) break;
                ClearStreamChunk(_streamChunks[oldestKey]);
                _streamChunks.Remove(oldestKey);
                _streamUnloadedChunkTotal++;
            }
        }

        private void ClearStreamChunk(StreamChunkState state)
        {
            if (state.Generated)
            {
                BumpFarViewTileSnapshotVersion();
                BumpCachedBackgroundFarViewPayloadVersion();
                BumpGroundPayloadChunkRendererStreamVersion();
                BumpBiomeMaskChunkRendererDataVersion();
            }

            if (state.HexBounds.size.x > 0 && state.HexBounds.size.y > 0)
            {
                ClearTilemapBounds(_ground, state.HexBounds);
                if (!UseBackgroundTilemap || !StreamPropsUseBackgroundGrid || _backgroundGrid == null)
                {
                    ClearTilemapBounds(_props, state.HexBounds);
                    ClearTilemapBounds(_blockers, state.HexBounds);
                    ClearTilemapBounds(_transitions, state.HexBounds);
                }
            }
            if (state.BackgroundBounds.size.x > 0 && state.BackgroundBounds.size.y > 0)
            {
                ClearTilemapBounds(_background, state.BackgroundBounds);
                if (UseBackgroundTilemap && StreamPropsUseBackgroundGrid && _backgroundGrid != null)
                {
                    ClearTilemapBounds(_props, state.BackgroundBounds);
                    ClearTilemapBounds(_blockers, state.BackgroundBounds);
                    ClearTilemapBounds(_transitions, state.BackgroundBounds);
                }
            }
            if (UseDirectWalkableUpdates && state.BlockedCells != null)
            {
                for (int i = 0; i < state.BlockedCells.Count; i++)
                {
                    var cell = state.BlockedCells[i];
                    _hex.SetWalkable(cell.x, cell.y, true);
                }
            }
        }

        private static void ClearTilemapBounds(Tilemap map, BoundsInt bounds)
        {
            if (map == null) return;
            int size = Mathf.Max(0, bounds.size.x * bounds.size.y);
            if (size == 0) return;
            var empty = new TileBase[size];
            map.SetTilesBlock(bounds, empty);
        }

        private int HashChunkSeed(Vector2Int coord)
        {
            unchecked
            {
                int h = _streamSeed;
                h = (h * 397) ^ coord.x;
                h = (h * 397) ^ coord.y;
                return h;
            }
        }

        private enum StreamBiomeType
        {
            Land,
            Water,
            Rock
        }

        private StreamBiomeType GetStreamBiome(int col, int row)
        {
            if (UseWaterBiome && HasBackgroundBiomeMaskData())
            {
                if (TryGetBackgroundMaskData(col, row, out var maskData))
                {
                    if (maskData.IsWater) return StreamBiomeType.Water;
                    if (maskData.IsRock) return StreamBiomeType.Rock;
                }
                else if (TryGetBackgroundCellData(new Vector2Int(col, row), out var cellData))
                {
                    if (cellData.IsWater) return StreamBiomeType.Water;
                    if (cellData.IsRock) return StreamBiomeType.Rock;
                }
            }

            float wScale = StreamWaterNoiseScale > 0f ? StreamWaterNoiseScale : StreamBiomeNoiseScale;
            float rScale = StreamRockNoiseScale > 0f ? StreamRockNoiseScale : StreamBiomeNoiseScale;
            float waterThreshold = StreamWaterThreshold > 0f ? StreamWaterThreshold : WaterCoverage;
            float rockThreshold = StreamRockThreshold > 0f ? StreamRockThreshold : (WaterCoverage * 0.5f);

            float water = FractalNoise((col + _streamWaterOffset.x) * wScale, (row + _streamWaterOffset.y) * wScale,
                Mathf.Max(1, AutoTerrainOctaves), Mathf.Clamp01(AutoTerrainPersistence), Mathf.Max(0.01f, AutoTerrainLacunarity));
            if (UseWaterBiome && _streamWaterTiles != null && _streamWaterTiles.Length > 0 && water < waterThreshold)
                return StreamBiomeType.Water;

            float rock = FractalNoise((col + _streamRockOffset.x) * rScale, (row + _streamRockOffset.y) * rScale,
                Mathf.Max(1, AutoTerrainOctaves), Mathf.Clamp01(AutoTerrainPersistence), Mathf.Max(0.01f, AutoTerrainLacunarity));
            if (UseWaterBiome && _streamRockTiles != null && _streamRockTiles.Length > 0 && rock < rockThreshold)
                return StreamBiomeType.Rock;

            return StreamBiomeType.Land;
        }

        private TileBase[] ResolveStreamPalette(StreamBiomeType biome)
        {
            switch (biome)
            {
                case StreamBiomeType.Water:
                    return _streamWaterTiles ?? _streamLandTiles;
                case StreamBiomeType.Rock:
                    return _streamRockTiles ?? _streamLandTiles;
                default:
                    return _streamLandTiles;
            }
        }

        private static TileBase[] ExcludeTiles(TileBase[] baseTiles, TileBase[] a, TileBase[] b)
        {
            if (baseTiles == null) return null;
            if ((a == null || a.Length == 0) && (b == null || b.Length == 0)) return baseTiles;
            var exclude = new HashSet<TileBase>();
            if (a != null)
            {
                for (int i = 0; i < a.Length; i++) if (a[i] != null) exclude.Add(a[i]);
            }
            if (b != null)
            {
                for (int i = 0; i < b.Length; i++) if (b[i] != null) exclude.Add(b[i]);
            }
            var list = new List<TileBase>();
            for (int i = 0; i < baseTiles.Length; i++)
            {
                var tile = baseTiles[i];
                if (tile != null && !exclude.Contains(tile))
                    list.Add(tile);
            }
            return list.ToArray();
        }
    }
}
