/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.cs
@module: presentation.pathfinding.worldgen.lifecycle
@purpose: Hosts ProceduralEnvironment lifecycle bootstrap, generation entrypoints, and grid/tilemap preparation helpers.
@entry: PENV-02, ProceduralEnvironment.Generate, ProceduralEnvironment.PrepareGeneration
@api: internal partial of ProceduralEnvironment
@deps: HexPathfindingBootstrap, Tilemap/Grid, CameraZoom2D, streaming/far-view partials
@data: generation coroutine state, tilemap references, occupied-cell bootstrap sets
@perf: startup-sensitive, keep orchestration-only
@thread: main thread orchestration
@tests: scripts/run_all_repo_audits.py, Unity script recompile, manual scene generation verification
@config: PENV-01 inspector fields, docs/runtime_switches.md
@notes: lifecycle/bootstrap is separated from heavy generation math so future shader migration can replace render backends without reopening startup flow
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-LIFECYCLE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.Lifecycle.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment : MonoBehaviour
    {
        // [PENV-02]
        // Unity lifecycle bootstrap for generation and far-view HUD startup.
        private void Awake()
        {
            if (!GenerateOnAwake) return;
            Generate();
        }

        private void Start()
        {
            if (!UnityEngine.Application.isPlaying) return;
            if (ShowFarViewBakeHUD)
            {
                EnsureFarViewHud();
                if (_farViewHudRoot != null)
                    _farViewHudRoot.SetActive(true);
                UpdateFarViewHud();
            }
        }

        private void OnDestroy()
        {
            ReleaseFarViewBackgroundCompositeMaterial();
            ReleaseFarViewTileRasterRig();
            ReleaseBackgroundPayloadChunkRendererResources();
            ReleaseGroundPayloadChunkRendererResources();
            ReleaseBiomeMaskChunkRendererResources();
            ClearRuntimeGroundConversion();
            ClearReadableTextureCache();
            ClearAutoTerrainRuleset();
            ReleaseFarViewTexture();
            if (_farViewBakeRoutine != null)
            {
                StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = null;
            }
        }

        [ContextMenu("Generate Environment")]
        public void Generate()
        {
            if (_generateRoutine != null)
            {
                StopCoroutine(_generateRoutine);
                _generateRoutine = null;
            }
            BumpFarViewTileSnapshotVersion();
            ClearGroundRenderPayloadCache();
            ClearPlacementRenderPayloadCache();
            _waterInteriorCache.Clear();
            _edgeProfileCache.Clear();
            _farViewBakeQueuedOnStart = false;
            if (!Enabled) return;
            if (UseWorldStreaming && UnityEngine.Application.isPlaying)
            {
                StartStreaming();
                return;
            }
            if (UseAsyncGeneration && UnityEngine.Application.isPlaying)
                _generateRoutine = StartCoroutine(GenerateRoutine());
            else
                GenerateImmediate();
        }

        private void LateUpdate()
        {
            UpdateBackgroundPayloadChunkRendererLifecycle();
            UpdateGroundPayloadChunkRendererLifecycle();
            UpdateBiomeMaskChunkRendererLifecycle();

            if (UseWorldStreaming && _streamingActive)
            {
                UpdateStreaming();
                UpdateFarViewBoundsVisibility();
            }
            if (!UseFarViewBake)
            {
                if (_farViewActive)
                    ApplyFarViewState(false);
                UpdateFarViewHud();
                UpdateFarViewBoundsVisibility();
                return;
            }

            ApplyFarViewLoadingVisibility();
            UpdateFarViewHud();
            UpdateFarViewBoundsVisibility();
            if (_generateRoutine != null)
                return;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            bool shouldUse = ShouldUseFarView(cam);
            bool canBakeFarViewNow = !UseWorldStreaming || !_streamingActive || _streamQueue.Count == 0;
            if (FarViewBakeOnStart && !_farViewBakeQueuedOnStart && !_farViewBaking && _farViewBakeRoutine == null && UnityEngine.Application.isPlaying)
            {
                if (canBakeFarViewNow)
                    QueueFarViewBake();
            }
            if (!_farViewHasContent && !_farViewDirty && !_farViewBaking && _farViewBakeRoutine == null && UnityEngine.Application.isPlaying)
            {
                if (canBakeFarViewNow && (FarViewBakeOnStart || shouldUse))
                    QueueFarViewBake();
            }
            if (!_farViewBaking && shouldUse != _farViewActive)
                ApplyFarViewState(shouldUse);

        }

        private IEnumerator GenerateRoutine()
        {
            ApplyFarViewLoadingVisibility();
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
            {
                _generateRoutine = null;
                yield break;
            }

            int total = width * height;
            bool preconvert = ConvertGroundTilesRuntime && PreconvertGroundTilesRuntime;
            if (ConvertGroundTilesRuntime)
                _skipRuntimeConversionInResolve = true;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);
            _skipRuntimeConversionInResolve = false;
            if (preconvert)
            {
                TileBase[] converted = groundPalette;
                yield return ConvertGroundPaletteRoutine(groundPalette, result => converted = result);
                groundPalette = converted;
            }
            var treePalette = FilterNullTiles(ApplyTreeWeighting(TreeTiles));
            var treeAccentPalette = FilterNullTiles(ResolveTreeAccentTiles());

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;

            bool hasRuleset = TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset);
            int[] layerIndex = null;
            if (hasRuleset && FillGround && _ground != null)
            {
                layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
                yield return ApplyRulesetToGroundRoutine(width, height, false, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
            }
            else if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                ClearGroundRenderPayloadCache();
                int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                for (int row = 0; row < height; row += rowsPerFrame)
                {
                    int rowCount = Mathf.Min(rowsPerFrame, height - row);
                    FillGroundTilesBlock(_ground, width, row, rowCount, rng, groundPalette);
                    yield return null;
                }
            }
            ClearBackgroundRenderInputCache();
            if (UseBackgroundTilemap && FillBackground && _background != null)
            {
                bool needConvert = BackgroundUseConvertedTiles && !ConvertGroundTilesRuntime;
                TileBase[] conversionPalette = hasRuleset ? CollectUniqueLayerTiles(layers) : groundPalette;
                if (hasRuleset && UseWaterBiome && AutoTerrainGroupByPrefix && groundPalette != null)
                {
                    var waterTiles = ResolveBiomeTilesByName(groundPalette, WaterTileNameKeywords);
                    var waterInteriorTiles = ResolveBiomeTilesByName(groundPalette, WaterInteriorTileNameKeywords);
                    var rockTiles = ResolveBiomeTilesByName(groundPalette, RockTileNameKeywords);
                    conversionPalette = CombineTiles(conversionPalette, waterTiles);
                    conversionPalette = CombineTiles(conversionPalette, waterInteriorTiles);
                    conversionPalette = CombineTiles(conversionPalette, rockTiles);
                }
                Dictionary<TileBase, TileBase> backgroundLookup = null;
                TileBase[] backgroundPalette = conversionPalette ?? groundPalette;
                if (needConvert && backgroundPalette != null && backgroundPalette.Length > 0)
                {
                    backgroundLookup = BuildConvertedLookup(backgroundPalette);
                    backgroundPalette = MapPalette(backgroundPalette, backgroundLookup);
                }
                if (TryConfigureBackground(width, height, backgroundPalette, out var bgWidth, out var bgHeight))
                {
                    if (hasRuleset)
                    {
                        Vector2 cellSize = _backgroundGrid != null ? (Vector2)_backgroundGrid.cellSize : ResolveBackgroundCellSize(backgroundPalette);
                        int[] bgLayerIndex = BuildLayerIndexRect(bgWidth, bgHeight, layers, noiseOffset, cellSize, ruleset);
                        yield return ApplyRulesetToBackgroundRoutine(bgWidth, bgHeight, layers, tileSeed, bgLayerIndex, backgroundLookup);
                    }
                    else
                    {
                        int rowsPerFrame = Mathf.Max(1, GroundRowsPerFrame);
                        for (int row = 0; row < bgHeight; row += rowsPerFrame)
                        {
                            int rowCount = Mathf.Min(rowsPerFrame, bgHeight - row);
                            FillGroundTilesBlock(_background, bgWidth, row, rowCount, rng, backgroundPalette);
                            yield return null;
                        }
                    }
                }
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            int treeTargetCount = TreeCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeCoverage))
                : TreeCount;
            treeTargetCount = Mathf.Clamp(treeTargetCount, 0, total);
            int treeAccentTargetCount = TreeAccentCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeAccentCoverage))
                : TreeAccentCount;
            treeAccentTargetCount = Mathf.Clamp(treeAccentTargetCount, 0, total);
            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            bool directBlockTrees = TreesBlockMovement && UseDirectWalkableUpdates;
            bool useOptimizedProps = UseOptimizedPropPlacement;
            int rockPropTargetCount = RockPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(RockPropCoverage))
                : RockPropCount;
            rockPropTargetCount = Mathf.Clamp(rockPropTargetCount, 0, total);
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (useOptimizedProps)
            {
                BuildPropCandidateCaches(width, height, out var landCells, out var rockCells, out var anyCells);
                if (landCells.Count == 0 && anyCells.Count > 0)
                    landCells = anyCells;
                int maxMinDistance = Mathf.Max(BlockingMinHexDistance, TreeMinHexDistance, RockPropMinHexDistance, PropMinHexDistance);
                var hash = maxMinDistance > 0 ? new SpatialHash(maxMinDistance) : null;
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                    yield return BuildPlacementsBatched(blockingPlacements, anyCells, blockingTargetCount, BlockingMinHexDistance, rng, blockingPalette, occupied, occupiedSet, hash, null, directBlockBlockers, bounds);
                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                    yield return BuildPlacementsBatched(treePlacements, landCells, treeTargetCount, TreeMinHexDistance, rng, treePalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                    yield return BuildPlacementsBatched(treeAccentPlacements, landCells, treeAccentTargetCount, TreeMinHexDistance, rng, treeAccentPalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                    yield return BuildPlacementsBatched(rockPlacements, rockCells, rockPropTargetCount, RockPropMinHexDistance, rng, RockPropTiles, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                    yield return BuildPlacementsBatched(propPlacements, landCells, propTargetCount, PropMinHexDistance, rng, propPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            yield return BuildPlacementsBatched(boostPlacements, landCells, boostTargetCount, PropMinHexDistance, rng, boostPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                    }
                }

                ApplyPlacements(_blockers, blockingPlacements);
                Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                ApplyPlacements(treeMap, treePlacements);
                ApplyPlacements(treeMap, treeAccentPlacements);
                ApplyPlacements(_props, rockPlacements);
                ApplyPlacements(_props, propPlacements);
                ApplyPlacements(_props, boostPlacements);
                CachePlacementRenderPayloads(
                    blockingPlacements,
                    treePlacements,
                    treeAccentPlacements,
                    rockPlacements,
                    propPlacements,
                    boostPlacements);
            }
            else
            {
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                {
                    yield return PlacePropsBatched(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers, PropBiomeFilter.Any, blockingPlacements);
                }

                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                {
                    Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                    if (treeMap != null)
                    {
                        yield return PlaceTreesBatched(treeMap, width, height, rng, treePalette, treeTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees, treePlacements);
                        if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                            yield return PlaceTreesBatched(treeMap, width, height, rng, treeAccentPalette, treeAccentTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees, treeAccentPlacements);
                    }
                }

                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                {
                    yield return PlacePropsBatched(_props, width, height, rng, RockPropTiles, rockPropTargetCount, RockPropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.RockOnly, rockPlacements);
                }

                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    yield return PlacePropsBatched(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly, propPlacements);
                }
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            yield return PlacePropsBatched(_props, width, height, rng, boostPalette, boostTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly, boostPlacements);
                    }
                }

                CachePlacementRenderPayloads(
                    blockingPlacements,
                    treePlacements,
                    treeAccentPlacements,
                    rockPlacements,
                    propPlacements,
                    boostPlacements);
            }

            int blockerBakeCount = blockingTargetCount + (TreesBlockMovement ? treeTargetCount : 0);
            if (BuildBlockingColliders)
            {
                if (blockerBakeCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockerBakeCount, propTargetCount, bounds);
            CenterCameraOnMap(width, height);
            if (UseFarViewBake && FarViewBakeOnStart)
                QueueFarViewBake();
            _generateRoutine = null;
        }

        private void GenerateImmediate()
        {
            if (!PrepareGeneration(out var rng, out var width, out var height, out var occupied, out var occupiedSet, out var bounds))
                return;

            int total = width * height;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);
            var treePalette = FilterNullTiles(ApplyTreeWeighting(TreeTiles));
            var treeAccentPalette = FilterNullTiles(ResolveTreeAccentTiles());

            bool directBlockBlockers = BlockingPropsBlockMovement && UseDirectWalkableUpdates;

            bool hasRuleset = TryPrepareRuleset(groundPalette, out var ruleset, out var layers, out var edgeLookup, out var edgeByBits, out var tileSeed, out var noiseOffset);
            int[] layerIndex = null;
            if (hasRuleset && FillGround && _ground != null)
            {
                layerIndex = BuildLayerIndex(width, height, layers, noiseOffset, ruleset);
                ApplyRulesetToGround(width, height, false, layers, edgeLookup, edgeByBits, tileSeed, layerIndex, ruleset);
            }
            else if (FillGround && groundPalette != null && groundPalette.Length > 0 && _ground != null)
            {
                ClearGroundRenderPayloadCache();
                FillGroundTiles(_ground, width, height, rng, groundPalette);
            }
            ClearBackgroundRenderInputCache();
            if (UseBackgroundTilemap && FillBackground && _background != null)
            {
                bool needConvert = BackgroundUseConvertedTiles && !ConvertGroundTilesRuntime;
                TileBase[] conversionPalette = hasRuleset ? CollectUniqueLayerTiles(layers) : groundPalette;
                if (hasRuleset && UseWaterBiome && AutoTerrainGroupByPrefix && groundPalette != null)
                {
                    var waterTiles = ResolveBiomeTilesByName(groundPalette, WaterTileNameKeywords);
                    var waterInteriorTiles = ResolveBiomeTilesByName(groundPalette, WaterInteriorTileNameKeywords);
                    var rockTiles = ResolveBiomeTilesByName(groundPalette, RockTileNameKeywords);
                    conversionPalette = CombineTiles(conversionPalette, waterTiles);
                    conversionPalette = CombineTiles(conversionPalette, waterInteriorTiles);
                    conversionPalette = CombineTiles(conversionPalette, rockTiles);
                }
                Dictionary<TileBase, TileBase> backgroundLookup = null;
                TileBase[] backgroundPalette = conversionPalette ?? groundPalette;
                if (needConvert && backgroundPalette != null && backgroundPalette.Length > 0)
                {
                    backgroundLookup = BuildConvertedLookup(backgroundPalette);
                    backgroundPalette = MapPalette(backgroundPalette, backgroundLookup);
                }
                if (TryConfigureBackground(width, height, backgroundPalette, out var bgWidth, out var bgHeight))
                {
                    if (hasRuleset)
                    {
                        Vector2 cellSize = _backgroundGrid != null ? (Vector2)_backgroundGrid.cellSize : ResolveBackgroundCellSize(backgroundPalette);
                        int[] bgLayerIndex = BuildLayerIndexRect(bgWidth, bgHeight, layers, noiseOffset, cellSize, ruleset);
                        ApplyRulesetToBackground(bgWidth, bgHeight, layers, tileSeed, bgLayerIndex, backgroundLookup);
                    }
                    else
                    {
                        FillGroundTiles(_background, bgWidth, bgHeight, rng, backgroundPalette);
                    }
                }
            }

            int blockingTargetCount = BlockingPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(BlockingPropCoverage))
                : BlockingPropCount;
            blockingTargetCount = Mathf.Clamp(blockingTargetCount, 0, total);
            int treeTargetCount = TreeCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeCoverage))
                : TreeCount;
            treeTargetCount = Mathf.Clamp(treeTargetCount, 0, total);
            int treeAccentTargetCount = TreeAccentCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(TreeAccentCoverage))
                : TreeAccentCount;
            treeAccentTargetCount = Mathf.Clamp(treeAccentTargetCount, 0, total);
            bool directBlockProps = PropsBlockMovement && UseDirectWalkableUpdates;
            bool directBlockTrees = TreesBlockMovement && UseDirectWalkableUpdates;
            bool useOptimizedProps = UseOptimizedPropPlacement;
            int rockPropTargetCount = RockPropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(RockPropCoverage))
                : RockPropCount;
            rockPropTargetCount = Mathf.Clamp(rockPropTargetCount, 0, total);
            int propTargetCount = PropCoverage > 0f
                ? Mathf.RoundToInt(total * Mathf.Clamp01(PropCoverage))
                : PropCount;
            propTargetCount = Mathf.Clamp(propTargetCount, 0, total);
            if (useOptimizedProps)
            {
                BuildPropCandidateCaches(width, height, out var landCells, out var rockCells, out var anyCells);
                if (landCells.Count == 0 && anyCells.Count > 0)
                    landCells = anyCells;
                int maxMinDistance = Mathf.Max(BlockingMinHexDistance, TreeMinHexDistance, RockPropMinHexDistance, PropMinHexDistance);
                var hash = maxMinDistance > 0 ? new SpatialHash(maxMinDistance) : null;
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                    BuildPlacements(blockingPlacements, anyCells, blockingTargetCount, BlockingMinHexDistance, rng, blockingPalette, occupied, occupiedSet, hash, null, directBlockBlockers, bounds);
                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                    BuildPlacements(treePlacements, landCells, treeTargetCount, TreeMinHexDistance, rng, treePalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                    BuildPlacements(treeAccentPlacements, landCells, treeAccentTargetCount, TreeMinHexDistance, rng, treeAccentPalette, occupied, occupiedSet, hash, GetTreePlacementWeight, directBlockTrees, bounds);
                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                    BuildPlacements(rockPlacements, rockCells, rockPropTargetCount, RockPropMinHexDistance, rng, RockPropTiles, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                    BuildPlacements(propPlacements, landCells, propTargetCount, PropMinHexDistance, rng, propPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            BuildPlacements(boostPlacements, landCells, boostTargetCount, PropMinHexDistance, rng, boostPalette, occupied, occupiedSet, hash, null, directBlockProps, bounds);
                    }
                }

                ApplyPlacements(_blockers, blockingPlacements);
                Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                ApplyPlacements(treeMap, treePlacements);
                ApplyPlacements(treeMap, treeAccentPlacements);
                ApplyPlacements(_props, rockPlacements);
                ApplyPlacements(_props, propPlacements);
                ApplyPlacements(_props, boostPlacements);
                CachePlacementRenderPayloads(
                    blockingPlacements,
                    treePlacements,
                    treeAccentPlacements,
                    rockPlacements,
                    propPlacements,
                    boostPlacements);
            }
            else
            {
                var blockingPlacements = new List<Placement>(blockingTargetCount);
                var treePlacements = new List<Placement>(treeTargetCount);
                var treeAccentPlacements = new List<Placement>(treeAccentTargetCount);
                var rockPlacements = new List<Placement>(rockPropTargetCount);
                var propPlacements = new List<Placement>(propTargetCount);
                var boostPlacements = new List<Placement>();

                if (blockingTargetCount > 0 && blockingPalette != null && blockingPalette.Length > 0 && _blockers != null)
                {
                    PlaceProps(_blockers, width, height, rng, blockingPalette, blockingTargetCount, BlockingMinHexDistance, occupied, occupiedSet, bounds, directBlockBlockers, PropBiomeFilter.Any, blockingPlacements);
                }

                if (treeTargetCount > 0 && treePalette != null && treePalette.Length > 0)
                {
                    Tilemap treeMap = TreesBlockMovement ? _blockers : _props;
                    if (treeMap != null)
                    {
                        PlaceTrees(treeMap, width, height, rng, treePalette, treeTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees, treePlacements);
                        if (treeAccentTargetCount > 0 && treeAccentPalette != null && treeAccentPalette.Length > 0)
                            PlaceTrees(treeMap, width, height, rng, treeAccentPalette, treeAccentTargetCount, TreeMinHexDistance, occupied, occupiedSet, bounds, directBlockTrees, treeAccentPlacements);
                    }
                }

                if (rockPropTargetCount > 0 && RockPropTiles != null && RockPropTiles.Length > 0 && _props != null)
                {
                    PlaceProps(_props, width, height, rng, RockPropTiles, rockPropTargetCount, RockPropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.RockOnly, rockPlacements);
                }

                if (propTargetCount > 0 && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    PlaceProps(_props, width, height, rng, propPalette, propTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly, propPlacements);
                }
                if (PropBoostMultiplier > 0f && propPalette != null && propPalette.Length > 0 && _props != null)
                {
                    int boostTargetCount = Mathf.RoundToInt(propTargetCount * PropBoostMultiplier);
                    boostTargetCount = Mathf.Clamp(boostTargetCount, 0, total);
                    if (boostTargetCount > 0)
                    {
                        var boostPalette = BuildPropBoostPalette(propPalette);
                        if (boostPalette != null && boostPalette.Length > 0)
                            PlaceProps(_props, width, height, rng, boostPalette, boostTargetCount, PropMinHexDistance, occupied, occupiedSet, bounds, directBlockProps, PropBiomeFilter.LandOnly, boostPlacements);
                    }
                }

                CachePlacementRenderPayloads(
                    blockingPlacements,
                    treePlacements,
                    treeAccentPlacements,
                    rockPlacements,
                    propPlacements,
                    boostPlacements);
            }

            int blockerBakeCount = blockingTargetCount + (TreesBlockMovement ? treeTargetCount : 0);
            if (BuildBlockingColliders)
            {
                if (blockerBakeCount > 0 && _blockers != null)
                {
                    EnsurePropCollider(_blockers);
                    ApplyObstacleLayer(_blockers);
                }
                if (propTargetCount > 0 && _props != null && PropsBlockMovement)
                {
                    EnsurePropCollider(_props);
                    ApplyObstacleLayer(_props);
                }
            }
            BakeBlockingIfNeeded(blockerBakeCount, propTargetCount, bounds);
            CenterCameraOnMap(width, height);
            if (UseFarViewBake && FarViewBakeOnStart)
                QueueFarViewBake();
        }

        private bool PrepareGeneration(out System.Random rng, out int width, out int height, out List<Vector2Int> occupied,
            out HashSet<Vector2Int> occupiedSet, out BlockBounds bounds)
        {
            rng = UseRandomSeed ? new System.Random() : new System.Random(Seed);
            width = 0;
            height = 0;
            occupied = new List<Vector2Int>(256);
            occupiedSet = new HashSet<Vector2Int>();
            bounds = _blockBounds;

            if (!Enabled) return false;
            EnsureRefs();
            if (_hex == null) return false;

            EnsureGrid();
            EnsureTilemaps();
            if (UseBackgroundTilemap)
            {
                EnsureBackgroundGrid();
                EnsureBackgroundTilemap();
            }
            ApplyDebugOutlineVisibility();
            if (UseFarViewBake)
            {
                if (_farViewBakeRoutine != null)
                {
                    StopCoroutine(_farViewBakeRoutine);
                    _farViewBakeRoutine = null;
                }
                _farViewDirty = false;
                EnsureFarViewObjects();
                ApplyFarViewState(false);
                _farViewHasContent = false;
            }

            if (ClearBeforeGenerate)
            {
                _ground?.ClearAllTiles();
                _background?.ClearAllTiles();
                _props?.ClearAllTiles();
                _blockers?.ClearAllTiles();
                _transitions?.ClearAllTiles();
                if (UseDirectWalkableUpdates)
                    ClearDirectBlocks();
            }

            width = Mathf.Max(0, _hex.Width);
            height = Mathf.Max(0, _hex.Height);
            if (width == 0 || height == 0) return false;

            if (UseGroundNoise)
            {
                if (RandomizeGroundNoiseOffset)
                {
                    _groundNoiseOffset = new Vector2(
                        (float)rng.NextDouble() * 1000f,
                        (float)rng.NextDouble() * 1000f);
                }
                else
                {
                    _groundNoiseOffset = GroundNoiseOffset;
                }
            }
            if (UseTreeBiomeNoise)
            {
                if (TreeBiomeRandomizeOffset)
                {
                    _treeNoiseOffset = new Vector2(
                        (float)rng.NextDouble() * 1000f,
                        (float)rng.NextDouble() * 1000f);
                }
                else
                {
                    _treeNoiseOffset = TreeBiomeNoiseOffset;
                }
            }

            bounds.Reset(width, height);
            return true;
        }

        private void EnsureRefs()
        {
            if (_hex == null || !_hex.isActiveAndEnabled)
                _hex = UnityEngine.Object.FindAnyObjectByType<HexPathfindingBootstrap>();
        }

        private void EnsureGrid()
        {
            if (_grid != null) return;
            var existing = GameObject.Find(GridObjectName);
            if (existing != null)
                _grid = existing.GetComponent<Grid>();
            if (_grid == null)
            {
                var go = existing ?? new GameObject(GridObjectName);
                _grid = go.GetComponent<Grid>();
                if (_grid == null) _grid = go.AddComponent<Grid>();
            }

            _grid.cellLayout = CellLayout;
            _grid.cellSize = ResolveCellSize();
            _grid.transform.position = new Vector3(_hex.Origin.x, _hex.Origin.y, 0f);
        }

        private void EnsureBackgroundGrid()
        {
            if (_backgroundGrid != null) return;
            var existing = GameObject.Find(BackgroundGridName);
            if (existing != null)
                _backgroundGrid = existing.GetComponent<Grid>();
            if (_backgroundGrid == null)
            {
                var go = existing ?? new GameObject(BackgroundGridName);
                _backgroundGrid = go.GetComponent<Grid>();
                if (_backgroundGrid == null) _backgroundGrid = go.AddComponent<Grid>();
            }
            _backgroundGrid.cellLayout = GridLayout.CellLayout.Rectangle;
            _backgroundGrid.cellSize = Vector3.one;
            _backgroundGrid.transform.position = new Vector3(_hex.Origin.x, _hex.Origin.y, 0f);
        }

        private void EnsureTilemaps()
        {
            _ground = FindOrCreateTilemap(_grid.transform, GroundTilemapName, GroundSortingOrder);
            _props = FindOrCreateTilemap(_grid.transform, PropTilemapName, PropSortingOrder);
            _blockers = FindOrCreateTilemap(_grid.transform, BlockerTilemapName, BlockerSortingOrder);
            _transitions = null;
            if (UseTerrainRuleset && UseTransitionTilemap && !string.IsNullOrEmpty(TransitionTilemapName))
                _transitions = FindOrCreateTilemap(_grid.transform, TransitionTilemapName, TransitionSortingOrder);
            ApplyTilemapRendererMode(_ground);
            ApplyTilemapRendererMode(_props);
            ApplyTilemapRendererMode(_blockers);
            ApplyTilemapRendererMode(_transitions);
        }

        private void EnsureBackgroundTilemap()
        {
            if (_backgroundGrid == null || string.IsNullOrEmpty(BackgroundTilemapName)) return;
            _background = FindOrCreateTilemap(_backgroundGrid.transform, BackgroundTilemapName, BackgroundSortingOrder);
            ApplyTilemapRendererMode(_background);
        }

        private void CenterCameraOnMap(int width, int height)
        {
            if (!CenterCameraOnGenerate) return;
            var cam = Camera.main;
            if (cam == null || _hex == null || width <= 0 || height <= 0) return;
            ComputeHexWorldBounds(width, height, out var min, out var max);
            var center = (min + max) * 0.5f;
            var pos = cam.transform.position;
            cam.transform.position = new Vector3(center.x, center.y, pos.z);
        }

        private void ApplyDebugOutlineVisibility()
        {
            bool debug = GroundTileDebugOutlineOnly;
            bool allow = !_farViewActive;
            SetTilemapRendererEnabled(_ground, allow && !debug);
            SetTilemapRendererEnabled(_props, allow && !debug);
            SetTilemapRendererEnabled(_blockers, allow && !debug);
            SetTilemapRendererEnabled(_transitions, allow && !debug);
            SetTilemapRendererEnabled(_background, allow && UseBackgroundTilemap);
        }

        private static void SetTilemapRendererEnabled(Tilemap map, bool enabled)
        {
            if (map == null) return;
            var renderer = map.GetComponent<TilemapRenderer>();
            if (renderer != null)
                renderer.enabled = enabled;
        }

        private void ApplyTilemapRendererMode(Tilemap map)
        {
            if (map == null) return;
            var renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null) return;
            renderer.mode = UseTilemapChunkMode ? TilemapRenderer.Mode.Chunk : TilemapRenderer.Mode.Individual;
        }
    }
}
