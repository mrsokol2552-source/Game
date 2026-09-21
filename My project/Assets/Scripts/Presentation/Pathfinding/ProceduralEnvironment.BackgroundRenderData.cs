/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderData.cs
@module: presentation.pathfinding.worldgen.background_render_data
@purpose: Builds reusable background-render resources for sync and chunked ruleset application so render loops consume prepared data instead of duplicating setup logic.
@entry: PENV-24, ProceduralEnvironment.BuildBackgroundRenderResources
@api: partial class implementation for ProceduralEnvironment
@deps: terrain layers, runtime tile conversion, biome masks, shared-ground settings
@data: prepared layer tiles/variants, water/rock/land palettes, masks, anti-repeat buffers, optional refine buffers
@perf: medium; runs once per background ruleset pass and reduces duplicated setup before large render loops
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and world-generation smoke tests
@config: background rendering, water biome, shared tiles, and runtime conversion settings in ProceduralEnvironment
@assets: mapped background tiles and runtime-converted tile assets
@notes: keep setup data extraction here so future shader/instancing backends can consume the same prepared payloads as tilemap rendering
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-BACKGROUNDRENDERDATA]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.BackgroundRenderData.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private sealed class BackgroundRenderResources
        {
            public int Size;
            public bool UseVariants;
            public bool UseTransform;
            public bool UseWaterBiome;
            public bool UseSharedTiles;
            public string[] WaterExclude;
            public bool[] WaterMask;
            public bool[] RockMask;
            public TileVariant[][] LayerVariants;
            public TileBase[][] LayerTiles;
            public TileVariant[] SharedVariants;
            public TileBase[] SharedTiles;
            public TileVariant[] WaterVariants;
            public TileVariant[] RockVariants;
            public TileBase[] WaterTiles;
            public TileBase[] RockTiles;
            public TileVariant[] WaterInteriorVariants;
            public TileBase[] WaterInteriorTiles;
            public TileVariant[] LandVariants;
            public TileBase[] LandTiles;
            public HashSet<TileBase> WaterSet;
            public HashSet<TileBase> RockSet;
            public EdgeProfile[] PlacedProfiles;
            public bool[] PlacedValid;
            public int[] PlacedVariantIds;
            public TileBase[] RefineTiles;
            public Matrix4x4[] RefineTransforms;
        }

        // [PENV-24]
        // Shared setup for background rendering paths so sync and coroutine variants consume the same prepared render data.
        private BackgroundRenderResources BuildBackgroundRenderResources(
            int width,
            int height,
            List<TerrainLayer> layers,
            int tileSeed,
            Dictionary<TileBase, TileBase> backgroundLookup,
            bool allocateRefineBuffers)
        {
            var resources = new BackgroundRenderResources
            {
                Size = width * height
            };

            resources.UseVariants = UseGroundTileRandomRotation
                || GroundTileMirrorX
                || GroundTileMirrorY
                || UseGroundTileEdgeColorMatch
                || UseWaterEdgeColorMatch
                || UseGroundTileAntiRepeat;
            resources.UseTransform = UseGroundTileRandomRotation || GroundTileMirrorX || GroundTileMirrorY;
            resources.PlacedProfiles = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch) ? new EdgeProfile[resources.Size] : null;
            resources.PlacedValid = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new bool[resources.Size] : null;
            resources.PlacedVariantIds = (UseGroundTileEdgeColorMatch || UseWaterEdgeColorMatch || UseGroundTileAntiRepeat) ? new int[resources.Size] : null;
            bool refineEdges = allocateRefineBuffers && UseWaterEdgeRefinement && UseWaterEdgeColorMatch;
            resources.RefineTiles = refineEdges ? new TileBase[resources.Size] : null;
            resources.RefineTransforms = (refineEdges && resources.UseTransform) ? new Matrix4x4[resources.Size] : null;

            resources.LayerVariants = resources.UseVariants ? new TileVariant[layers.Count][] : null;
            resources.LayerTiles = resources.UseVariants ? null : new TileBase[layers.Count][];
            resources.UseSharedTiles = UseSharedGroundTiles && SharedGroundTileChance > 0f;
            resources.UseWaterBiome = UseWaterBiome;
            resources.WaterExclude = resources.UseWaterBiome ? ResolveWaterExcludeKeywords() : null;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.BaseTiles == null || layer.BaseTiles.Length == 0)
                {
                    if (resources.LayerVariants != null) resources.LayerVariants[i] = Array.Empty<TileVariant>();
                    if (resources.LayerTiles != null) resources.LayerTiles[i] = Array.Empty<TileBase>();
                    continue;
                }

                var baseTiles = layer.BaseTiles;
                var mappedTiles = new TileBase[baseTiles.Length];
                for (int t = 0; t < baseTiles.Length; t++)
                    mappedTiles[t] = MapBackgroundTile(baseTiles[t], backgroundLookup);

                if (resources.UseVariants)
                    resources.LayerVariants[i] = BuildTileVariants(mappedTiles);
                else
                    resources.LayerTiles[i] = mappedTiles;
            }

            if (resources.UseWaterBiome)
            {
                var biomeCandidates = (AutoTerrainGroupByPrefix && GroundTiles != null && GroundTiles.Length > 0)
                    ? GroundTiles
                    : CollectUniqueLayerTiles(layers);
                resources.WaterTiles = ResolveBiomeTilesByName(biomeCandidates, WaterTileNameKeywords);
                if (resources.WaterTiles != null && resources.WaterTiles.Length > 0)
                    resources.WaterTiles = ExcludeTilesByNameOrSprite(resources.WaterTiles, resources.WaterExclude);
                resources.RockTiles = ResolveBiomeTilesByName(biomeCandidates, RockTileNameKeywords);
                resources.WaterInteriorTiles = ResolveBiomeTilesByName(biomeCandidates, WaterInteriorTileNameKeywords);
                if (resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0)
                    resources.WaterInteriorTiles = ExcludeTilesByNameOrSprite(resources.WaterInteriorTiles, resources.WaterExclude);

                if (resources.WaterTiles != null && resources.WaterTiles.Length > 0)
                {
                    for (int t = 0; t < resources.WaterTiles.Length; t++)
                        resources.WaterTiles[t] = MapBackgroundTile(resources.WaterTiles[t], backgroundLookup);
                    resources.WaterTiles = ExcludeTilesByNameOrSprite(resources.WaterTiles, resources.WaterExclude);
                }

                if (resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0)
                {
                    for (int t = 0; t < resources.WaterInteriorTiles.Length; t++)
                        resources.WaterInteriorTiles[t] = MapBackgroundTile(resources.WaterInteriorTiles[t], backgroundLookup);
                    resources.WaterInteriorTiles = ExcludeTilesByNameOrSprite(resources.WaterInteriorTiles, resources.WaterExclude);
                }

                if (UseWaterAutoInteriorByColor)
                {
                    var allWaterTiles = CombineTiles(resources.WaterTiles, resources.WaterInteriorTiles);
                    if (allWaterTiles != null && allWaterTiles.Length > 0)
                    {
                        var autoInterior = new List<TileBase>();
                        for (int t = 0; t < allWaterTiles.Length; t++)
                        {
                            var tile = allWaterTiles[t];
                            if (tile != null && IsWaterInteriorTile(tile))
                                autoInterior.Add(tile);
                        }

                        if (autoInterior.Count == 0 && WaterInteriorFallbackCount > 0)
                        {
                            var scored = new List<TileScore>(allWaterTiles.Length);
                            for (int t = 0; t < allWaterTiles.Length; t++)
                            {
                                var tile = allWaterTiles[t];
                                if (tile == null) continue;
                                scored.Add(new TileScore(tile, GetWaterInteriorScore(tile)));
                            }
                            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
                            int take = Mathf.Clamp(WaterInteriorFallbackCount, 0, scored.Count);
                            for (int i = 0; i < take; i++)
                            {
                                if (scored[i].Tile != null)
                                    autoInterior.Add(scored[i].Tile);
                            }
                        }

                        if (autoInterior.Count > 0)
                            resources.WaterInteriorTiles = CombineTiles(resources.WaterInteriorTiles, autoInterior.ToArray());
                    }
                }

                if (resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0 && resources.WaterTiles != null && resources.WaterTiles.Length > 0)
                {
                    var interiorSet = new HashSet<TileBase>(resources.WaterInteriorTiles);
                    var filtered = new List<TileBase>(resources.WaterTiles.Length);
                    for (int t = 0; t < resources.WaterTiles.Length; t++)
                    {
                        var tile = resources.WaterTiles[t];
                        if (tile != null && !interiorSet.Contains(tile))
                            filtered.Add(tile);
                    }
                    resources.WaterTiles = filtered.Count > 0 ? filtered.ToArray() : null;
                }

                if (resources.UseVariants)
                {
                    if (resources.WaterTiles != null && resources.WaterTiles.Length > 0)
                    {
                        resources.WaterVariants = BuildTileVariants(
                            resources.WaterTiles,
                            UseGroundTileRandomRotation && WaterTilesAllowRotation,
                            (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        resources.WaterVariants = ExcludeVariantsByNameOrSprite(resources.WaterVariants, resources.WaterExclude);
                    }

                    if (resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0)
                    {
                        resources.WaterInteriorVariants = BuildTileVariants(
                            resources.WaterInteriorTiles,
                            UseGroundTileRandomRotation && WaterTilesAllowRotation,
                            (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                        resources.WaterInteriorVariants = ExcludeVariantsByNameOrSprite(resources.WaterInteriorVariants, resources.WaterExclude);
                    }
                }

                if ((resources.WaterTiles != null && resources.WaterTiles.Length > 0) || (resources.WaterInteriorTiles != null && resources.WaterInteriorTiles.Length > 0))
                {
                    resources.WaterMask = BuildWaterMaskRect(width, height, tileSeed, out _);
                    var waterAllTiles = CombineTiles(resources.WaterTiles, resources.WaterInteriorTiles);
                    resources.WaterSet = BuildTileSet(waterAllTiles);
                    if (resources.WaterTiles == null || resources.WaterTiles.Length == 0)
                    {
                        resources.WaterTiles = resources.WaterInteriorTiles;
                        resources.WaterVariants = resources.WaterInteriorVariants;
                    }
                }

                if (resources.RockTiles != null && resources.RockTiles.Length > 0 && resources.WaterMask != null)
                {
                    for (int t = 0; t < resources.RockTiles.Length; t++)
                        resources.RockTiles[t] = MapBackgroundTile(resources.RockTiles[t], backgroundLookup);
                    if (resources.UseVariants)
                    {
                        resources.RockVariants = BuildTileVariants(
                            resources.RockTiles,
                            UseGroundTileRandomRotation && RockTilesAllowRotation,
                            (GroundTileMirrorX || GroundTileMirrorY) && RockTilesAllowMirroring);
                    }
                    resources.RockMask = BuildRockMaskFromWater(width, height, resources.WaterMask, tileSeed);
                    resources.RockSet = BuildTileSet(resources.RockTiles);
                }

                if (resources.WaterMask == null)
                    resources.UseWaterBiome = false;
            }

            if (resources.UseWaterBiome)
            {
                var allTiles = CollectUniqueLayerTiles(layers) ?? GroundTiles;
                if (allTiles != null && allTiles.Length > 0)
                {
                    var unique = new HashSet<TileBase>();
                    var list = new List<TileBase>();
                    for (int i = 0; i < allTiles.Length; i++)
                    {
                        var mapped = MapBackgroundTile(allTiles[i], backgroundLookup);
                        if (mapped == null) continue;
                        if (resources.WaterSet != null && resources.WaterSet.Contains(mapped)) continue;
                        if (resources.RockSet != null && resources.RockSet.Contains(mapped)) continue;
                        if (unique.Add(mapped))
                            list.Add(mapped);
                    }
                    resources.LandTiles = list.Count > 0 ? list.ToArray() : null;
                    if (resources.WaterExclude != null && resources.WaterExclude.Length > 0)
                        resources.LandTiles = ExcludeTilesByNameOrSprite(resources.LandTiles, resources.WaterExclude);
                    if (resources.UseVariants && resources.LandTiles != null)
                        resources.LandVariants = BuildTileVariants(resources.LandTiles);
                }
            }

            if (resources.UseSharedTiles)
            {
                resources.SharedTiles = ResolveSharedGroundTiles(layers);
                if (resources.SharedTiles == null || resources.SharedTiles.Length == 0)
                {
                    resources.UseSharedTiles = false;
                }
                else
                {
                    for (int t = 0; t < resources.SharedTiles.Length; t++)
                    {
                        var tile = resources.SharedTiles[t];
                        if (tile != null && backgroundLookup != null && backgroundLookup.TryGetValue(tile, out var mapped) && mapped != null)
                            resources.SharedTiles[t] = mapped;
                        else if (ConvertGroundTilesRuntime)
                            resources.SharedTiles[t] = GetOrCreateRuntimeGroundTile(tile);
                    }
                    if (resources.UseVariants)
                        resources.SharedVariants = BuildTileVariants(resources.SharedTiles);
                }
            }

            return resources;
        }
    }
}
