/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.cs
@module: presentation.pathfinding.worldgen.streaming
@purpose: Holds streaming bootstrap, tilemap parenting, scheduling, queueing, and chunk-bounds helpers for ProceduralEnvironment.
@entry: PENV-03, PENV-04, PENV-05, ProceduralEnvironment.StartStreaming, ProceduralEnvironment.UpdateStreaming
@api: partial class implementation for ProceduralEnvironment plus internal streaming diagnostics for tests
@deps: Tilemap/Grid, HexPathfindingBootstrap, Camera.main, far-view state in ProceduralEnvironment
@data: stream chunk queues, chunk bounds, streaming palette caches, streaming speed counters
@perf: hotpath; frame-budgeted streaming scheduler and queue management
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/SampleSceneBootSmokeTests.cs, runtime audits, manual streaming verification
@config: streaming and far-view inspector fields in ProceduralEnvironment
@assets: GroundTiles, PropTiles, TreeTiles, Background tilemap/grid
@notes: keep streaming orchestration separate from per-chunk generation and rendering helpers while decomposing ProceduralEnvironment
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-STREAMING]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.Streaming.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        internal readonly struct StreamingDiagnostics
        {
            public readonly bool Active;
            public readonly int TrackedChunkCount;
            public readonly int GeneratedChunkCount;
            public readonly int PendingQueueCount;
            public readonly int PendingAllQueueCount;
            public readonly int GeneratedTotal;
            public readonly int CreatedChunkStateTotal;
            public readonly int ReusedChunkStateTotal;
            public readonly int UnloadedChunkTotal;
            public readonly int RequiredChunkCount;
            public readonly Vector2Int CenterChunk;
            public readonly Vector2Int ActiveMinChunk;
            public readonly Vector2Int ActiveMaxChunk;
            public readonly Vector2Int PrefetchMinChunk;
            public readonly Vector2Int PrefetchMaxChunk;
            public readonly float ChunksPerSecond;

            public StreamingDiagnostics(
                bool active,
                int trackedChunkCount,
                int generatedChunkCount,
                int pendingQueueCount,
                int pendingAllQueueCount,
                int generatedTotal,
                int createdChunkStateTotal,
                int reusedChunkStateTotal,
                int unloadedChunkTotal,
                int requiredChunkCount,
                Vector2Int centerChunk,
                Vector2Int activeMinChunk,
                Vector2Int activeMaxChunk,
                Vector2Int prefetchMinChunk,
                Vector2Int prefetchMaxChunk,
                float chunksPerSecond)
            {
                Active = active;
                TrackedChunkCount = trackedChunkCount;
                GeneratedChunkCount = generatedChunkCount;
                PendingQueueCount = pendingQueueCount;
                PendingAllQueueCount = pendingAllQueueCount;
                GeneratedTotal = generatedTotal;
                CreatedChunkStateTotal = createdChunkStateTotal;
                ReusedChunkStateTotal = reusedChunkStateTotal;
                UnloadedChunkTotal = unloadedChunkTotal;
                RequiredChunkCount = requiredChunkCount;
                CenterChunk = centerChunk;
                ActiveMinChunk = activeMinChunk;
                ActiveMaxChunk = activeMaxChunk;
                PrefetchMinChunk = prefetchMinChunk;
                PrefetchMaxChunk = prefetchMaxChunk;
                ChunksPerSecond = chunksPerSecond;
            }
        }

        internal StreamingDiagnostics GetStreamingDiagnostics()
        {
            int generatedChunkCount = 0;
            foreach (var kv in _streamChunks)
            {
                if (kv.Value.Generated)
                    generatedChunkCount++;
            }

            return new StreamingDiagnostics(
                _streamingActive,
                _streamChunks.Count,
                generatedChunkCount,
                _streamQueue.Count,
                _streamAllQueue.Count,
                _streamGeneratedTotal,
                _streamCreatedChunkStateTotal,
                _streamReusedChunkStateTotal,
                _streamUnloadedChunkTotal,
                _streamRequiredChunkCount,
                _streamCenterChunk,
                _streamActiveMinChunk,
                _streamActiveMaxChunk,
                _streamPrefetchMinChunk,
                _streamPrefetchMaxChunk,
                _streamSpeed);
        }

        private int _streamCreatedChunkStateTotal;
        private int _streamReusedChunkStateTotal;
        private int _streamUnloadedChunkTotal;

        // [PENV-03]
        // Streaming bootstrap: resolve palettes, precompute masks, configure tilemaps,
        // and seed chunk queues for the current world.
        private void StartStreaming()
        {
            BumpFarViewTileSnapshotVersion();
            ClearGroundRenderPayloadCache();
            ClearBackgroundRenderInputCache();
            ClearPlacementRenderPayloadCache();
            _streamingActive = false;
            _streamChunks.Clear();
            _streamQueue.Clear();
            _streamAllQueue.Clear();
            _streamAllQueued = false;
                _streamGeneratedTotal = 0;
                _streamGeneratedSinceSample = 0;
                _streamCreatedChunkStateTotal = 0;
                _streamReusedChunkStateTotal = 0;
                _streamUnloadedChunkTotal = 0;
                _streamSpeed = 0f;
            _streamLastSampleTime = Time.realtimeSinceStartup;
            _streamLastCamMoveTime = Time.realtimeSinceStartup;
            _streamLastCamPos = Vector3.zero;
            if (!PrepareStreaming(out _streamWidth, out _streamHeight))
                return;

            var seedRng = UseRandomSeed ? new System.Random() : new System.Random(Seed);
            _streamSeed = UseRandomSeed ? seedRng.Next() : Seed;
            _streamWaterOffset = new Vector2((_streamSeed % 683) * 0.019f, (_streamSeed % 743) * 0.021f);
            _streamRockOffset = new Vector2((_streamSeed % 887) * 0.011f, (_streamSeed % 919) * 0.015f);
            if (UseTreeBiomeNoise)
            {
                if (TreeBiomeRandomizeOffset)
                {
                    _treeNoiseOffset = new Vector2(
                        (float)seedRng.NextDouble() * 1000f,
                        (float)seedRng.NextDouble() * 1000f);
                }
                else
                {
                    _treeNoiseOffset = TreeBiomeNoiseOffset;
                }
            }

            _skipRuntimeConversionInResolve = true;
            ResolvePalettes(out var groundPalette, out var propPalette, out var blockingPalette);
            _skipRuntimeConversionInResolve = false;
            groundPalette = FilterNullTiles(groundPalette);
            _streamPropTiles = FilterNullTiles(propPalette);
            _streamBlockingTiles = FilterNullTiles(blockingPalette);
            _streamTreeTiles = FilterNullTiles(ApplyTreeWeighting(TreeTiles));
            _streamTreeAccentTiles = FilterNullTiles(ResolveTreeAccentTiles());

            bool streamUseVariants = UseGroundTileRandomRotation
                || GroundTileMirrorX
                || GroundTileMirrorY
                || UseGroundTileEdgeColorMatch
                || UseWaterEdgeColorMatch
                || UseGroundTileAntiRepeat;

            _streamWaterExclude = UseWaterBiome ? ResolveWaterExcludeKeywords() : null;
            _streamLandVariants = null;
            _streamWaterVariants = null;
            _streamWaterInteriorVariants = null;
            _streamRockVariants = null;
            _streamSharedVariants = null;
            _streamWaterSet = null;
            _streamRockSet = null;

            if (UseWaterBiome && groundPalette != null && groundPalette.Length > 0)
            {
                var waterTiles = ResolveBiomeTilesByName(groundPalette, WaterTileNameKeywords);
                if (waterTiles != null && waterTiles.Length > 0)
                    waterTiles = ExcludeTilesByNameOrSprite(waterTiles, _streamWaterExclude);
                var rockTiles = ResolveBiomeTilesByName(groundPalette, RockTileNameKeywords);
                var waterInteriorTiles = ResolveBiomeTilesByName(groundPalette, WaterInteriorTileNameKeywords);
                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0)
                    waterInteriorTiles = ExcludeTilesByNameOrSprite(waterInteriorTiles, _streamWaterExclude);

                if (UseWaterAutoInteriorByColor)
                {
                    var allWaterTiles = CombineTiles(waterTiles, waterInteriorTiles);
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
                            waterInteriorTiles = CombineTiles(waterInteriorTiles, autoInterior.ToArray());
                    }
                }

                if (waterInteriorTiles != null && waterInteriorTiles.Length > 0 && waterTiles != null && waterTiles.Length > 0)
                {
                    var interiorSet = new HashSet<TileBase>(waterInteriorTiles);
                    var filtered = new List<TileBase>(waterTiles.Length);
                    for (int t = 0; t < waterTiles.Length; t++)
                    {
                        var tile = waterTiles[t];
                        if (tile != null && !interiorSet.Contains(tile))
                            filtered.Add(tile);
                    }
                    waterTiles = filtered.Count > 0 ? filtered.ToArray() : null;
                }

                _streamWaterTiles = waterTiles;
                _streamWaterInteriorTiles = waterInteriorTiles;
                _streamRockTiles = rockTiles;
                _streamWaterSet = BuildTileSet(CombineTiles(_streamWaterTiles, _streamWaterInteriorTiles));
                _streamRockSet = BuildTileSet(_streamRockTiles);

                var landTiles = ExcludeTiles(groundPalette, _streamWaterTiles, _streamRockTiles);
                if (_streamWaterExclude != null && _streamWaterExclude.Length > 0)
                    landTiles = ExcludeTilesByNameOrSprite(landTiles, _streamWaterExclude);
                _streamLandTiles = landTiles != null && landTiles.Length > 0 ? landTiles : groundPalette;
            }
            else
            {
                _streamLandTiles = groundPalette;
                _streamWaterTiles = null;
                _streamWaterInteriorTiles = null;
                _streamRockTiles = null;
            }

            if (_hex != null)
                _hex.EnsureInitialized();

            Dictionary<TileBase, TileBase> streamLookup = null;
            if (ConvertGroundTilesRuntime && groundPalette != null && groundPalette.Length > 0)
            {
                streamLookup = BuildConvertedLookup(groundPalette);
                _streamLandTiles = MapPalette(_streamLandTiles, streamLookup);
                _streamWaterTiles = MapPalette(_streamWaterTiles, streamLookup);
                _streamWaterInteriorTiles = MapPalette(_streamWaterInteriorTiles, streamLookup);
                _streamRockTiles = MapPalette(_streamRockTiles, streamLookup);
            }

            if (streamUseVariants)
            {
                if (_streamWaterTiles != null && _streamWaterTiles.Length > 0)
                {
                    _streamWaterVariants = BuildTileVariants(
                        _streamWaterTiles,
                        UseGroundTileRandomRotation && WaterTilesAllowRotation,
                        (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                    _streamWaterVariants = ExcludeVariantsByNameOrSprite(_streamWaterVariants, _streamWaterExclude);
                }
                if (_streamWaterInteriorTiles != null && _streamWaterInteriorTiles.Length > 0)
                {
                    _streamWaterInteriorVariants = BuildTileVariants(
                        _streamWaterInteriorTiles,
                        UseGroundTileRandomRotation && WaterTilesAllowRotation,
                        (GroundTileMirrorX || GroundTileMirrorY) && WaterTilesAllowMirroring);
                    _streamWaterInteriorVariants = ExcludeVariantsByNameOrSprite(_streamWaterInteriorVariants, _streamWaterExclude);
                }
                if (_streamWaterVariants == null && _streamWaterInteriorVariants != null && _streamWaterInteriorVariants.Length > 0)
                    _streamWaterVariants = _streamWaterInteriorVariants;
                if (_streamRockTiles != null && _streamRockTiles.Length > 0)
                {
                    _streamRockVariants = BuildTileVariants(
                        _streamRockTiles,
                        UseGroundTileRandomRotation && RockTilesAllowRotation,
                        (GroundTileMirrorX || GroundTileMirrorY) && RockTilesAllowMirroring);
                }
                if (_streamLandTiles != null && _streamLandTiles.Length > 0)
                    _streamLandVariants = BuildTileVariants(_streamLandTiles);

                if (UseSharedGroundTiles && SharedGroundTileChance > 0f)
                {
                    var sharedTiles = ResolveSharedGroundTiles(null);
                    if (sharedTiles != null && sharedTiles.Length > 0 && streamLookup != null)
                    {
                        for (int i = 0; i < sharedTiles.Length; i++)
                            sharedTiles[i] = MapBackgroundTile(sharedTiles[i], streamLookup);
                    }
                    if (sharedTiles != null && sharedTiles.Length > 0)
                        _streamSharedVariants = BuildTileVariants(sharedTiles);
                }
            }

            _streamWaterSet = BuildTileSet(CombineTiles(_streamWaterTiles, _streamWaterInteriorTiles));
            _streamRockSet = BuildTileSet(_streamRockTiles);

            if (UseBackgroundTilemap && _background != null)
            {
                if (TryConfigureBackground(_streamWidth, _streamHeight, _streamLandTiles ?? GroundTiles, out var bgWidth, out var bgHeight))
                    _background.ClearAllTiles();

                bool hasWaterTiles = (_streamWaterTiles != null && _streamWaterTiles.Length > 0)
                    || (_streamWaterInteriorTiles != null && _streamWaterInteriorTiles.Length > 0);
                if (UseWaterBiome && hasWaterTiles && bgWidth > 0 && bgHeight > 0)
                    BuildStreamingBackgroundMasks(bgWidth, bgHeight, _streamSeed);
                else
                    ClearStreamingBackgroundMasks();
            }

            EnsureStreamingTilemapParents();

            _streamingActive = true;
            _streamCenterChunk = new Vector2Int(int.MinValue, int.MinValue);
            if (StreamBakeAllChunks)
            {
                EnqueueAllStreamChunks();
                _streamAllQueued = true;
            }

            UpdateStreaming(true);
        }

        // [PENV-04]
        // Helpers that switch props/blockers between the hex grid and the background rect grid.
        private void EnsureStreamingTilemapParents()
        {
            if (_grid == null) return;
            if (UseBackgroundTilemap && _backgroundGrid != null && StreamPropsUseBackgroundGrid)
            {
                ReparentTilemap(_props, _backgroundGrid.transform);
                ReparentTilemap(_blockers, _backgroundGrid.transform);
                ReparentTilemap(_transitions, _backgroundGrid.transform);
            }
            else
            {
                ReparentTilemap(_props, _grid.transform);
                ReparentTilemap(_blockers, _grid.transform);
                ReparentTilemap(_transitions, _grid.transform);
            }
        }

        private static void ReparentTilemap(Tilemap map, Transform parent)
        {
            if (map == null || parent == null) return;
            if (map.transform.parent == parent) return;
            map.transform.SetParent(parent, false);
            map.transform.localPosition = Vector3.zero;
        }

        private bool PrepareStreaming(out int width, out int height)
        {
            width = 0;
            height = 0;
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
                _farViewBaking = false;
                _farViewHasContent = false;
                ApplyFarViewState(false);
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

            CenterCameraOnMap(width, height);
            return true;
        }

        // [PENV-05]
        // Streaming scheduler that decides which chunks are required, queued, baked, or evicted.
        private void UpdateStreaming(bool force = false)
        {
            if (!_streamingActive || _hex == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            float now = Time.realtimeSinceStartup;
            if (_streamLastCamPos == Vector3.zero)
                _streamLastCamPos = cam.transform.position;
            if ((cam.transform.position - _streamLastCamPos).sqrMagnitude > (StreamIdleMoveEpsilon * StreamIdleMoveEpsilon))
            {
                _streamLastCamMoveTime = now;
                _streamLastCamPos = cam.transform.position;
            }

            bool idle = StreamBakeAllChunksOnIdle && (now - _streamLastCamMoveTime) >= Mathf.Max(0f, StreamIdleSeconds);
            if (idle && !_streamAllQueued)
            {
                EnqueueAllStreamChunks();
                _streamAllQueued = true;
            }

            int chunkSize = Mathf.Max(8, StreamChunkSize);
            bool useViewBounds = TryGetStreamingChunkBounds(cam, chunkSize,
                out var activeMin, out var activeMax,
                out var prefetchMin, out var prefetchMax);

            if (useViewBounds)
            {
                if (force || activeMin != _streamActiveMinChunk || activeMax != _streamActiveMaxChunk
                    || prefetchMin != _streamPrefetchMinChunk || prefetchMax != _streamPrefetchMaxChunk)
                {
                    _streamActiveMinChunk = activeMin;
                    _streamActiveMaxChunk = activeMax;
                    _streamPrefetchMinChunk = prefetchMin;
                    _streamPrefetchMaxChunk = prefetchMax;
                    EnqueueStreamingChunks(activeMin, activeMax, prefetchMin, prefetchMax);
                }
            }
            else
            {
                var camCell = _hex.WorldToGrid(cam.transform.position);
                var center = new Vector2Int(camCell.x / chunkSize, camCell.y / chunkSize);
                if (force || center != _streamCenterChunk)
                {
                    _streamCenterChunk = center;
                    EnqueueStreamingChunks(center);
                }

                int prefetchRadius = Mathf.Max(StreamActiveRadius, StreamPrefetchRadius);
                int maxX = Mathf.Max(0, (_streamWidth - 1) / chunkSize);
                int maxY = Mathf.Max(0, (_streamHeight - 1) / chunkSize);
                var minChunk = new Vector2Int(Mathf.Clamp(center.x - prefetchRadius, 0, maxX), Mathf.Clamp(center.y - prefetchRadius, 0, maxY));
                var maxChunk = new Vector2Int(Mathf.Clamp(center.x + prefetchRadius, 0, maxX), Mathf.Clamp(center.y + prefetchRadius, 0, maxY));
                _streamPrefetchMinChunk = minChunk;
                _streamPrefetchMaxChunk = maxChunk;
                int reqW = Mathf.Max(0, maxChunk.x - minChunk.x + 1);
                int reqH = Mathf.Max(0, maxChunk.y - minChunk.y + 1);
                _streamRequiredChunkCount = reqW * reqH;
            }

            if (StreamSkipIfOverBudget && StreamTargetFps > 0)
            {
                float frameMs = Time.unscaledDeltaTime * 1000f;
                float targetMs = 1000f / StreamTargetFps;
                if (frameMs > targetMs)
                {
                    UpdateStreamSpeed(0);
                    return;
                }
            }

            int perFrame = Mathf.Max(1, StreamChunksPerFrame);
            int generated = 0;
            float budget = Mathf.Max(0f, StreamFrameBudgetMs);
            float start = Time.realtimeSinceStartup;
            while (_streamQueue.Count > 0 && generated < perFrame)
            {
                var coord = _streamQueue.Dequeue();
                if (_streamChunks.TryGetValue(coord, out var state) && state.Generated)
                    continue;
                GenerateStreamChunk(coord);
                generated++;
                if (budget > 0f && (Time.realtimeSinceStartup - start) * 1000f >= budget)
                    break;
            }

            while (generated < perFrame && StreamBakeAllChunks && _streamAllQueue.Count > 0)
            {
                var coord = _streamAllQueue.Dequeue();
                if (_streamChunks.TryGetValue(coord, out var state) && state.Generated)
                    continue;
                if (!IsValidStreamChunk(coord))
                    continue;
                GenerateStreamChunk(coord);
                generated++;
                if (budget > 0f && (Time.realtimeSinceStartup - start) * 1000f >= budget)
                    break;
            }

            while (generated < perFrame && idle && _streamAllQueue.Count > 0)
            {
                var coord = _streamAllQueue.Dequeue();
                if (_streamChunks.TryGetValue(coord, out var state) && state.Generated)
                    continue;
                if (!IsValidStreamChunk(coord))
                    continue;
                GenerateStreamChunk(coord);
                generated++;
                if (budget > 0f && (Time.realtimeSinceStartup - start) * 1000f >= budget)
                    break;
            }

            UpdateStreamSpeed(generated);

            UnloadDistantChunks(_streamPrefetchMinChunk, _streamPrefetchMaxChunk);
            EnforceStreamCacheLimit();
        }

        private bool TryGetStreamingChunkBounds(Camera cam, int chunkSize,
            out Vector2Int activeMin, out Vector2Int activeMax,
            out Vector2Int prefetchMin, out Vector2Int prefetchMax)
        {
            activeMin = default;
            activeMax = default;
            prefetchMin = default;
            prefetchMax = default;
            if (cam == null || _hex == null || !cam.orthographic) return false;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            var pos = cam.transform.position;
            var minWorld = new Vector3(pos.x - halfW, pos.y - halfH, 0f);
            var maxWorld = new Vector3(pos.x + halfW, pos.y + halfH, 0f);
            var minCell = _hex.WorldToGrid(minWorld);
            var maxCell = _hex.WorldToGrid(maxWorld);

            int minCol = Mathf.Min(minCell.x, maxCell.x);
            int maxCol = Mathf.Max(minCell.x, maxCell.x);
            int minRow = Mathf.Min(minCell.y, maxCell.y);
            int maxRow = Mathf.Max(minCell.y, maxCell.y);

            int minChunkX = Mathf.FloorToInt(minCol / (float)chunkSize);
            int maxChunkX = Mathf.FloorToInt(maxCol / (float)chunkSize);
            int minChunkY = Mathf.FloorToInt(minRow / (float)chunkSize);
            int maxChunkY = Mathf.FloorToInt(maxRow / (float)chunkSize);

            int pad = Mathf.Max(0, StreamPrefetchRadius);
            int maxX = Mathf.Max(0, (_streamWidth - 1) / chunkSize);
            int maxY = Mathf.Max(0, (_streamHeight - 1) / chunkSize);

            activeMin = new Vector2Int(Mathf.Clamp(minChunkX, 0, maxX), Mathf.Clamp(minChunkY, 0, maxY));
            activeMax = new Vector2Int(Mathf.Clamp(maxChunkX, 0, maxX), Mathf.Clamp(maxChunkY, 0, maxY));
            prefetchMin = new Vector2Int(Mathf.Clamp(minChunkX - pad, 0, maxX), Mathf.Clamp(minChunkY - pad, 0, maxY));
            prefetchMax = new Vector2Int(Mathf.Clamp(maxChunkX + pad, 0, maxX), Mathf.Clamp(maxChunkY + pad, 0, maxY));

            int reqW = Mathf.Max(0, prefetchMax.x - prefetchMin.x + 1);
            int reqH = Mathf.Max(0, prefetchMax.y - prefetchMin.y + 1);
            _streamRequiredChunkCount = reqW * reqH;
            return true;
        }

        private void UpdateStreamSpeed(int generatedThisFrame)
        {
            if (generatedThisFrame <= 0)
            {
                float now = Time.realtimeSinceStartup;
                if (_streamLastSampleTime <= 0f)
                    _streamLastSampleTime = now;
                return;
            }

            _streamGeneratedTotal += generatedThisFrame;
            _streamGeneratedSinceSample += generatedThisFrame;
            float t = Time.realtimeSinceStartup;
            if (_streamLastSampleTime <= 0f)
            {
                _streamLastSampleTime = t;
                return;
            }

            float dt = t - _streamLastSampleTime;
            if (dt >= 0.5f)
            {
                _streamSpeed = _streamGeneratedSinceSample / dt;
                _streamGeneratedSinceSample = 0;
                _streamLastSampleTime = t;
            }
        }

        private void EnqueueStreamingChunks(Vector2Int center)
        {
            int activeRadius = Mathf.Max(0, StreamActiveRadius);
            int prefetchRadius = Mathf.Max(activeRadius, StreamPrefetchRadius);
            for (int dy = -prefetchRadius; dy <= prefetchRadius; dy++)
            {
                for (int dx = -prefetchRadius; dx <= prefetchRadius; dx++)
                {
                    var coord = new Vector2Int(center.x + dx, center.y + dy);
                    if (!IsValidStreamChunk(coord)) continue;
                    int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    bool active = dist <= activeRadius;
                    if (_streamChunks.TryGetValue(coord, out var state))
                    {
                        _streamReusedChunkStateTotal++;
                        if (active)
                            state.LastTouchedFrame = Time.frameCount;
                        _streamChunks[coord] = state;
                        if (!state.Generated)
                            _streamQueue.Enqueue(coord);
                        continue;
                    }

                    var chunkState = new StreamChunkState
                    {
                        Coord = coord,
                        HexBounds = GetHexChunkBounds(coord),
                        BackgroundBounds = GetBackgroundBoundsForHexChunk(coord),
                        Generated = false,
                        LastTouchedFrame = active ? Time.frameCount : 0,
                        BlockedCells = UseDirectWalkableUpdates ? new List<Vector2Int>() : null
                    };
                    _streamCreatedChunkStateTotal++;
                    _streamChunks[coord] = chunkState;
                    _streamQueue.Enqueue(coord);
                }
            }
        }

        private void EnqueueStreamingChunks(Vector2Int activeMin, Vector2Int activeMax, Vector2Int prefetchMin, Vector2Int prefetchMax)
        {
            for (int y = activeMin.y; y <= activeMax.y; y++)
            {
                for (int x = activeMin.x; x <= activeMax.x; x++)
                {
                    var coord = new Vector2Int(x, y);
                    if (!IsValidStreamChunk(coord)) continue;
                    if (_streamChunks.TryGetValue(coord, out var state))
                    {
                        _streamReusedChunkStateTotal++;
                        state.LastTouchedFrame = Time.frameCount;
                        _streamChunks[coord] = state;
                        if (!state.Generated)
                            _streamQueue.Enqueue(coord);
                        continue;
                    }

                    var chunkState = new StreamChunkState
                    {
                        Coord = coord,
                        HexBounds = GetHexChunkBounds(coord),
                        BackgroundBounds = GetBackgroundBoundsForHexChunk(coord),
                        Generated = false,
                        LastTouchedFrame = Time.frameCount,
                        BlockedCells = UseDirectWalkableUpdates ? new List<Vector2Int>() : null
                    };
                    _streamCreatedChunkStateTotal++;
                    _streamChunks[coord] = chunkState;
                    _streamQueue.Enqueue(coord);
                }
            }

            for (int y = prefetchMin.y; y <= prefetchMax.y; y++)
            {
                for (int x = prefetchMin.x; x <= prefetchMax.x; x++)
                {
                    if (x >= activeMin.x && x <= activeMax.x && y >= activeMin.y && y <= activeMax.y)
                        continue;
                    var coord = new Vector2Int(x, y);
                    if (!IsValidStreamChunk(coord)) continue;
                    if (_streamChunks.TryGetValue(coord, out var state))
                    {
                        _streamReusedChunkStateTotal++;
                        _streamChunks[coord] = state;
                        if (!state.Generated)
                            _streamQueue.Enqueue(coord);
                        continue;
                    }

                    var chunkState = new StreamChunkState
                    {
                        Coord = coord,
                        HexBounds = GetHexChunkBounds(coord),
                        BackgroundBounds = GetBackgroundBoundsForHexChunk(coord),
                        Generated = false,
                        LastTouchedFrame = 0,
                        BlockedCells = UseDirectWalkableUpdates ? new List<Vector2Int>() : null
                    };
                    _streamCreatedChunkStateTotal++;
                    _streamChunks[coord] = chunkState;
                    _streamQueue.Enqueue(coord);
                }
            }
        }

        private void EnqueueAllStreamChunks()
        {
            int chunkSize = Mathf.Max(8, StreamChunkSize);
            int maxX = Mathf.Max(0, (_streamWidth - 1) / chunkSize);
            int maxY = Mathf.Max(0, (_streamHeight - 1) / chunkSize);
            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    _streamAllQueue.Enqueue(new Vector2Int(x, y));
                }
            }
        }

        private bool IsValidStreamChunk(Vector2Int coord)
        {
            int chunkSize = Mathf.Max(8, StreamChunkSize);
            int maxX = Mathf.Max(0, (_streamWidth - 1) / chunkSize);
            int maxY = Mathf.Max(0, (_streamHeight - 1) / chunkSize);
            return coord.x >= 0 && coord.y >= 0 && coord.x <= maxX && coord.y <= maxY;
        }

        private BoundsInt GetHexChunkBounds(Vector2Int coord)
        {
            int chunkSize = Mathf.Max(8, StreamChunkSize);
            int startCol = coord.x * chunkSize;
            int startRow = coord.y * chunkSize;
            int width = Mathf.Min(chunkSize, _streamWidth - startCol);
            int height = Mathf.Min(chunkSize, _streamHeight - startRow);
            return new BoundsInt(startCol, startRow, 0, width, height, 1);
        }

        private BoundsInt GetBackgroundBoundsForHexChunk(Vector2Int coord)
        {
            if (_background == null || _hex == null) return default;
            var hexBounds = GetHexChunkBounds(coord);
            if (hexBounds.size.x <= 0 || hexBounds.size.y <= 0) return default;

            Vector3 minWorld = _hex.GridToWorld(hexBounds.xMin, hexBounds.yMin);
            Vector3 maxWorld = _hex.GridToWorld(hexBounds.xMax - 1, hexBounds.yMax - 1);
            var minCell = _background.WorldToCell(minWorld);
            var maxCell = _background.WorldToCell(maxWorld);
            int bx = Mathf.Min(minCell.x, maxCell.x);
            int by = Mathf.Min(minCell.y, maxCell.y);
            int bw = Mathf.Abs(maxCell.x - minCell.x) + 1;
            int bh = Mathf.Abs(maxCell.y - minCell.y) + 1;
            return new BoundsInt(bx, by, 0, bw, bh, 1);
        }
    }
}
