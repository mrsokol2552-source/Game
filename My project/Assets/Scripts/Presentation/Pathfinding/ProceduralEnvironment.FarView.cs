/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.cs
@module: presentation.pathfinding.worldgen.farview
@purpose: Holds far-view bake scheduling, render-state helpers, HUD/debug helpers, and far-view chunk/readback types for ProceduralEnvironment.
@entry: PENV-11, PENV-12, PENV-13, PENV-14, PENV-15, ProceduralEnvironment.OnGUI
@api: partial class implementation for ProceduralEnvironment
@deps: Unity UI, Renderers, far-view runtime state from ProceduralEnvironment
@data: far-view HUD state, debug overlay text, far-view chunk metadata, readback metadata
@perf: medium to hotpath during far-view bake; scheduling, render-state toggles, and HUD/debug
@thread: main thread only
@tests: indirect coverage via runtime audits and manual far-view verification
@config: ProceduralEnvironment far-view HUD/debug inspector fields
@assets: LegacyRuntime.ttf, Sprites/Default
@notes: keep all far-view scheduling, bake, renderer-state, and HUD/debug logic together in this partial
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEW]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarView.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly bool Enabled;

            public RendererState(Renderer renderer, bool enabled)
            {
                Renderer = renderer;
                Enabled = enabled;
            }
        }

        private sealed class FarViewChunk
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public Texture2D Texture;
            public Texture2D BackgroundTexture;
            public Texture2D TileUnderlayTexture;
            public Sprite Sprite;
            public bool Active;
            public Bounds CaptureBounds;
            public readonly FarViewChunkRenderSource RenderSource = new FarViewChunkRenderSource();
            public int BackgroundRasterizedPayloadVersion;
            public int BackgroundRasterizedWidth;
            public int BackgroundRasterizedHeight;
            public RectInt BackgroundRasterizedCellRect;
            public Bounds BackgroundRasterizedCaptureBounds;
            public int TileUnderlayRasterizedTileVersion;
            public int TileUnderlayRasterizedBackgroundVersion;
            public int TileUnderlayRasterizedWidth;
            public int TileUnderlayRasterizedHeight;
            public Bounds TileUnderlayRasterizedCaptureBounds;
        }

        private struct FarViewReadback
        {
            public AsyncGPUReadbackRequest Request;
            public RenderTexture RenderTexture;
            public FarViewChunk Chunk;
            public int Width;
            public int Height;
            public float Ppu;
            public Bounds Bounds;
            public int BakeVersion;
            public float SubmittedTime;
            public bool PayloadSourcesComposited;
        }

        private void QueueFarViewBake()
        {
            if (!UseFarViewBake) return;
            if (_farViewBaking || _farViewBakeRoutine != null) return;
            if (_farViewDirty) return;
            _farViewDirty = true;
            _farViewBakeQueuedOnStart = true;
            if (UnityEngine.Application.isPlaying)
            {
                if (_farViewBakeRoutine != null)
                    StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = StartCoroutine(BakeFarViewNextFrame());
            }
            else
                BakeFarViewTexture();
        }

        // [PENV-11]
        // Deferred far-view bake scheduling used to spread capture work across frames.
        private IEnumerator BakeFarViewNextFrame()
        {
            yield return null;
            BakeFarViewTexture();
            _farViewBakeRoutine = null;
        }

        private void BakeFarViewTexture()
        {
            if (!UseFarViewBake)
            {
                _farViewDirty = false;
                _farViewBakeRoutine = null;
                return;
            }

            EnsureFarViewObjects();
            ClearFarViewReadbacks();
            _farViewDirty = false;
            _farViewBakeRoutine = null;

            if (!TryBuildFarViewBakeSource(out var source))
            {
                _farViewHasContent = false;
                ApplyFarViewState(false);
                return;
            }

            if (LogFarViewPayloadSourcesOnBake)
                Debug.Log(BuildFarViewPayloadSmokeText(source));

            var renderers = source.Renderers;
            var bounds = source.Bounds;
            _farViewBounds = bounds;
            UpdateFarViewBoundsRenderer(bounds);
            var rendererStates = new List<RendererState>(renderers.Count);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                rendererStates.Add(new RendererState(r, r.enabled));
                r.enabled = true;
            }

            bool prevFarViewEnabled = _farViewRenderer != null && _farViewRenderer.enabled;
            if (_farViewRenderer != null)
                _farViewRenderer.enabled = false;

            List<(GameObject go, int layer)> layerRestore = null;
            List<Renderer> disabledRenderers = null;
            int bakeLayer = ResolveFarViewBakeLayer();
            if (bakeLayer >= 0)
            {
                layerRestore = new List<(GameObject, int)>(renderers.Count);
                for (int i = 0; i < renderers.Count; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    layerRestore.Add((r.gameObject, r.gameObject.layer));
                    r.gameObject.layer = bakeLayer;
                }
                _farViewCamera.cullingMask = 1 << bakeLayer;
            }
            else
            {
                var keep = new HashSet<Renderer>(renderers);
                if (_farViewRenderer != null)
                    keep.Add(_farViewRenderer);
                disabledRenderers = DisableNonTilemapRenderers(keep);
                _farViewCamera.cullingMask = ~0;
            }

            if (UseFarViewChunkedBake && UnityEngine.Application.isPlaying)
            {
                if (_farViewBakeRoutine != null)
                    StopCoroutine(_farViewBakeRoutine);
                _farViewBakeRoutine = StartCoroutine(BakeFarViewChunksRoutine(
                    source,
                    rendererStates,
                    layerRestore,
                    bakeLayer,
                    prevFarViewEnabled));
                return;
            }

            if (UseFarViewChunkedBake)
            {
                BakeFarViewChunksSync(source, bakeLayer);
            }
            else
            {
                BeginFarViewBakeStats(1);
                int texWidth = Mathf.CeilToInt(bounds.size.x * Mathf.Max(1, FarViewPixelsPerUnit));
                int texHeight = Mathf.CeilToInt(bounds.size.y * Mathf.Max(1, FarViewPixelsPerUnit));
                texWidth = Mathf.Clamp(texWidth, 64, Mathf.Max(64, FarViewMaxTextureSize));
                texHeight = Mathf.Clamp(texHeight, 64, Mathf.Max(64, FarViewMaxTextureSize));
                EnsureFarViewTexture(texWidth, texHeight);

                ConfigureFarViewCamera(bounds);
                _farViewCamera.targetTexture = _farViewTexture;
                _farViewCamera.Render();
                _farViewCamera.targetTexture = null;
                UpdateFarViewSprite(texWidth, texHeight);
                UpdateFarViewQuad(bounds);
                _farViewHasContent = _farViewSprite != null;
                MarkFarViewChunkSubmitted();
                MarkFarViewChunkCompleted();
            }

            if (layerRestore != null)
            {
                for (int i = 0; i < layerRestore.Count; i++)
                {
                    var entry = layerRestore[i];
                    if (entry.go != null)
                        entry.go.layer = entry.layer;
                }
            }
            if (disabledRenderers != null)
                RestoreRenderers(disabledRenderers);

            for (int i = 0; i < rendererStates.Count; i++)
            {
                var state = rendererStates[i];
                if (state.Renderer != null)
                    state.Renderer.enabled = state.Enabled;
            }

            if (_farViewRenderer != null)
                _farViewRenderer.enabled = prevFarViewEnabled;

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
                ApplyFarViewState(ShouldUseFarView(cam));
            else
                ApplyFarViewState(false);
        }

        // [PENV-12]
        // Runtime creation and maintenance of far-view rendering objects and materials.
        private void EnsureFarViewObjects()
        {
            if (_farViewRoot == null)
            {
                var existing = GameObject.Find("FarViewBaked (Auto)");
                _farViewRoot = existing ?? new GameObject("FarViewBaked (Auto)");
                _farViewRoot.transform.SetParent(transform, false);
            }

            var renderer = _farViewRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = _farViewRoot.AddComponent<SpriteRenderer>();
            _farViewRenderer = renderer;
            _farViewRenderer.enabled = false;
            _farViewRenderer.sortingOrder = FarViewSortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                _farViewRenderer.sortingLayerName = SortingLayerName;
            if (UseFarViewChunkedBake)
                _farViewRenderer.enabled = false;

            var existingCam = _farViewRoot.transform.Find("FarViewBakeCamera (Auto)");
            var camGo = existingCam != null ? existingCam.gameObject : new GameObject("FarViewBakeCamera (Auto)");
            camGo.transform.SetParent(_farViewRoot.transform, false);
            var cam = camGo.GetComponent<Camera>();
            if (cam == null)
                cam = camGo.AddComponent<Camera>();
            _farViewCamera = cam;

            if (_farViewCamera == null) return;
            _farViewCamera.enabled = false;
            _farViewCamera.orthographic = true;
            _farViewCamera.clearFlags = CameraClearFlags.SolidColor;
            _farViewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _farViewCamera.nearClipPlane = 0.01f;
            _farViewCamera.farClipPlane = 1000f;
        }

        // [PENV-13]
        // Chunked far-view bake pipeline that renders the world into coarse cached textures.
        private IEnumerator BakeFarViewChunksRoutine(
            FarViewBakeSource source,
            List<RendererState> rendererStates,
            List<(GameObject go, int layer)> layerRestore,
            int bakeLayer,
            bool prevFarViewEnabled)
        {
            bool completed = false;
            var renderers = source.Renderers;
            var bounds = source.Bounds;
            var keep = new HashSet<Renderer>(renderers);
            if (_farViewRenderer != null)
                keep.Add(_farViewRenderer);
            float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
            int maxPixels = Mathf.Max(256, Mathf.Min(FarViewChunkPixels, FarViewMaxTextureSize));
            float chunkWorld = Mathf.Max(1f, maxPixels / ppu);
            int chunksX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / chunkWorld));
            int chunksY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / chunkWorld));
            bool useDirect = UseFarViewDirectChunkRender;
            bool useAsyncReadback = !useDirect && UseFarViewAsyncReadback && SystemInfo.supportsAsyncGPUReadback;
            float frameBudgetMs = Mathf.Max(0f, FarViewBakeFrameBudgetMs);
            int perFrame = Mathf.Max(1, FarViewChunksPerFrame);
            if (frameBudgetMs > 0f)
                perFrame = int.MaxValue;

            EnsureFarViewChunkList(chunksX * chunksY);
            SetFarViewChunkVisibility(false);
            _farViewHasContent = false;
            _farViewBaking = true;
            ApplyFarViewLoadingVisibility();
            ClearFarViewReadbacks();
            BeginFarViewBakeStats(chunksX * chunksY);

            int idx = 0;
            int processed = 0;
            bool any = false;
            List<Renderer> disabled = null;
            if (bakeLayer < 0)
                disabled = DisableNonTilemapRenderers(keep);
            bool hasLiveRenderers = renderers != null && renderers.Count > 0;
            int maxPending = Mathf.Max(1, FarViewMaxPendingReadbacks);
            float frameStart = Time.realtimeSinceStartup;
            int processedThisFrame = 0;
            try
            {
                for (int y = 0; y < chunksY; y++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        float minX = bounds.min.x + (x * chunkWorld);
                        float minY = bounds.min.y + (y * chunkWorld);
                        float sizeX = Mathf.Min(chunkWorld, bounds.max.x - minX);
                        float sizeY = Mathf.Min(chunkWorld, bounds.max.y - minY);
                        if (sizeX <= 0f || sizeY <= 0f) continue;
                        var chunkBounds = new Bounds(
                            new Vector3(minX + (sizeX * 0.5f), minY + (sizeY * 0.5f), 0f),
                            new Vector3(sizeX, sizeY, 0f));

                        int texW = Mathf.Clamp(Mathf.CeilToInt(sizeX * ppu), 64, maxPixels);
                        int texH = Mathf.Clamp(Mathf.CeilToInt(sizeY * ppu), 64, maxPixels);

                        if (useAsyncReadback)
                        {
                            while (_farViewReadbacks.Count >= maxPending)
                            {
                                ProcessFarViewReadbacks(ref any);
                                yield return null;
                            }
                        }

                        var chunk = _farViewChunks[idx++];
                        chunk.CaptureBounds = chunkBounds;
                        if (TryBuildFarViewBackgroundChunkSource(source, chunk, chunkBounds, out var backgroundChunkSource))
                            ApplyFarViewBackgroundChunkSource(chunk, backgroundChunkSource);
                        else
                            ApplyFarViewBackgroundChunkSource(chunk, default);
                        if (TryBuildFarViewTileChunkSource(chunk, chunkBounds, out var tileChunkSource))
                            ApplyFarViewTileChunkSource(chunk, tileChunkSource, chunkBounds);
                        else
                            ApplyFarViewTileChunkSource(chunk, default, chunkBounds);
                        if (TryUpdateChunkFromPayloadSources(chunk, texW, texH, ppu))
                        {
                            _farViewPayloadOnlyChunks++;
                            any = true;
                            MarkFarViewChunkSubmitted();
                            MarkFarViewChunkCompleted();
                            UpdateChunkTransform(chunk, chunkBounds);
                            processed++;
                            processedThisFrame++;
                            if (processedThisFrame >= perFrame || (frameBudgetMs > 0f && (Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs))
                            {
                                ProcessFarViewReadbacks(ref any);
                                yield return null;
                                frameStart = Time.realtimeSinceStartup;
                                processedThisFrame = 0;
                            }
                            continue;
                        }
                        if (!hasLiveRenderers)
                        {
                            ClearChunkSprite(chunk);
                            MarkFarViewChunkSubmitted();
                            MarkFarViewChunkCompleted();
                            UpdateChunkTransform(chunk, chunkBounds);
                            processed++;
                            processedThisFrame++;
                            if (processedThisFrame >= perFrame || (frameBudgetMs > 0f && (Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs))
                            {
                                ProcessFarViewReadbacks(ref any);
                                yield return null;
                                frameStart = Time.realtimeSinceStartup;
                                processedThisFrame = 0;
                            }
                            continue;
                        }
                        if (useDirect)
                        {
                            var rt = RenderTexture.GetTemporary(
                                texW,
                                texH,
                                16,
                                RenderTextureFormat.ARGB32,
                                RenderTextureReadWrite.Linear);
                            rt.filterMode = FilterMode.Point;
                            rt.wrapMode = TextureWrapMode.Clamp;
                            ConfigureFarViewCamera(chunkBounds);
                            _farViewCamera.targetTexture = rt;
                            _farViewCamera.Render();
                            _farViewCamera.targetTexture = null;
                            bool payloadSourcesComposited = TryComposeChunkPayloadRenderTexture(chunk, ref rt, texW, texH);
                            UpdateChunkDirectRender(chunk, rt, texW, texH, ppu, payloadSourcesComposited);
                            RenderTexture.ReleaseTemporary(rt);
                            any = true;
                            MarkFarViewChunkSubmitted();
                            MarkFarViewChunkCompleted();
                        }
                        else
                        {
                            var rt = RenderTexture.GetTemporary(
                                texW,
                                texH,
                                16,
                                RenderTextureFormat.ARGB32,
                                RenderTextureReadWrite.Linear);
                            rt.filterMode = FilterMode.Point;
                            rt.wrapMode = TextureWrapMode.Clamp;

                            ConfigureFarViewCamera(chunkBounds);
                            _farViewCamera.targetTexture = rt;
                            _farViewCamera.Render();
                            _farViewCamera.targetTexture = null;

                            if (useAsyncReadback)
                            {
                                bool payloadSourcesComposited = TryComposeChunkPayloadRenderTexture(chunk, ref rt, texW, texH);
                                var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
                                _farViewReadbacks.Add(new FarViewReadback
                                {
                                    Request = request,
                                    RenderTexture = rt,
                                    Chunk = chunk,
                                    Width = texW,
                                    Height = texH,
                                    Ppu = ppu,
                                    Bounds = chunkBounds,
                                    BakeVersion = _farViewBakeVersion,
                                    SubmittedTime = Time.realtimeSinceStartup,
                                    PayloadSourcesComposited = payloadSourcesComposited
                                });
                                MarkFarViewChunkSubmitted();
                            }
                            else
                            {
                                bool payloadSourcesComposited = TryComposeChunkPayloadRenderTexture(chunk, ref rt, texW, texH);
                                UpdateChunkSprite(chunk, rt, texW, texH, ppu, payloadSourcesComposited);
                                RenderTexture.ReleaseTemporary(rt);
                                any = true;
                                MarkFarViewChunkSubmitted();
                                MarkFarViewChunkCompleted();
                            }
                        }
                        UpdateChunkTransform(chunk, chunkBounds);

                        processed++;
                        processedThisFrame++;
                        if (processedThisFrame >= perFrame || (frameBudgetMs > 0f && (Time.realtimeSinceStartup - frameStart) * 1000f >= frameBudgetMs))
                        {
                            ProcessFarViewReadbacks(ref any);
                            yield return null;
                            frameStart = Time.realtimeSinceStartup;
                            processedThisFrame = 0;
                        }
                    }
                }

                if (useAsyncReadback)
                {
                    while (_farViewReadbacks.Count > 0)
                    {
                        ProcessFarViewReadbacks(ref any);
                        yield return null;
                    }
                }

                if (disabled != null)
                    RestoreRenderers(disabled);
                _farViewHasContent = any;

                if (layerRestore != null)
                {
                    for (int i = 0; i < layerRestore.Count; i++)
                    {
                        var entry = layerRestore[i];
                        if (entry.go != null)
                            entry.go.layer = entry.layer;
                    }
                }

                for (int i = 0; i < rendererStates.Count; i++)
                {
                    var state = rendererStates[i];
                    if (state.Renderer != null)
                        state.Renderer.enabled = state.Enabled;
                }

                if (_farViewRenderer != null)
                    _farViewRenderer.enabled = prevFarViewEnabled;

                ApplyFarViewState(ShouldUseFarView(Camera.main));
                completed = true;
            }
            finally
            {
                _farViewBaking = false;
                _farViewBakeRoutine = null;
                if (!completed)
                {
                    if (disabled != null)
                        RestoreRenderers(disabled);
                    if (layerRestore != null)
                    {
                        for (int i = 0; i < layerRestore.Count; i++)
                        {
                            var entry = layerRestore[i];
                            if (entry.go != null)
                                entry.go.layer = entry.layer;
                        }
                    }
                    for (int i = 0; i < rendererStates.Count; i++)
                    {
                        var state = rendererStates[i];
                        if (state.Renderer != null)
                            state.Renderer.enabled = state.Enabled;
                    }
                    if (_farViewRenderer != null)
                        _farViewRenderer.enabled = prevFarViewEnabled;
                    ApplyFarViewState(ShouldUseFarView(Camera.main));
                }
                ApplyFarViewLoadingVisibility();
            }
        }

        private string BuildFarViewHudText()
        {
            string status = _farViewBaking ? "baking" : (_farViewHasContent ? "ready" : "idle");
            var payloadSources = GetFarViewPayloadSourceDiagnostics();
            int chunkPayloads = 0;
            int chunkPayloadCells = 0;
            int chunkGroundSnapshots = 0;
            int chunkGroundCells = 0;
            int chunkTransitionSnapshots = 0;
            int chunkTransitionCells = 0;
            int chunkPropSnapshots = 0;
            int chunkPropCells = 0;
            int chunkBlockerSnapshots = 0;
            int chunkBlockerCells = 0;
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk == null || !chunk.Active)
                    continue;
                var renderSource = chunk.RenderSource;
                if (renderSource.HasBackgroundPayload)
                {
                    chunkPayloads++;
                    chunkPayloadCells += renderSource.BackgroundCellCount;
                }
                if (renderSource.GroundSnapshot != null && renderSource.GroundSnapshot.HasTiles)
                {
                    chunkGroundSnapshots++;
                    chunkGroundCells += renderSource.GroundCellCount;
                }
                if (renderSource.TransitionSnapshot != null && renderSource.TransitionSnapshot.HasTiles)
                {
                    chunkTransitionSnapshots++;
                    chunkTransitionCells += renderSource.TransitionCellCount;
                }
                if (renderSource.PropSnapshot != null && renderSource.PropSnapshot.HasTiles)
                {
                    chunkPropSnapshots++;
                    chunkPropCells += renderSource.PropCellCount;
                }
                if (renderSource.BlockerSnapshot != null && renderSource.BlockerSnapshot.HasTiles)
                {
                    chunkBlockerSnapshots++;
                    chunkBlockerCells += renderSource.BlockerCellCount;
                }
            }
            float elapsed = _farViewBakeStartTime > 0f ? (Time.realtimeSinceStartup - _farViewBakeStartTime) : 0f;
            float speed = (elapsed > 0f) ? (_farViewChunkCompleted / elapsed) : 0f;
            int remaining = Mathf.Max(0, _farViewChunkTotal - _farViewChunkCompleted);
            float eta = speed > 0f ? (remaining / speed) : 0f;
            int pending = Mathf.Max(0, _farViewChunkSubmitted - _farViewChunkCompleted);

            return $"FarView: {status}\n" +
                   $"Background source: {payloadSources.BackgroundSource}\n" +
                   $"Ground source: {payloadSources.GroundSource}\n" +
                   $"Placement source: {payloadSources.PlacementSource}\n" +
                   $"Background backend: {ResolveFarViewBackgroundBackendDebugLabel()}\n" +
                   $"Tile backend: {ResolveFarViewTileBackendDebugLabel()}\n" +
                   $"Payload-only chunks: {_farViewPayloadOnlyChunks}\n" +
                   $"Tile GPU: {_farViewTileGpuChunks} | Fallbacks: {_farViewTileGpuFallbackChunks}\n" +
                   $"Background slices: {_farViewBackgroundSliceCacheHits} hits / {_farViewBackgroundSliceCacheMisses} misses\n" +
                   $"Tile slices: {_farViewTileSliceCacheHits} hits / {_farViewTileSliceCacheMisses} misses\n" +
                   $"Tile underlay cache: {_farViewTileUnderlayCacheHits} hits / {_farViewTileUnderlayCacheMisses} misses\n" +
                   $"Background cache: {_farViewBackgroundChunkCacheHits} hits / {_farViewBackgroundChunkCacheMisses} misses\n" +
                   $"Background payload chunks: {chunkPayloads}/{_farViewChunks.Count} | Cells: {chunkPayloadCells}\n" +
                   $"Ground snapshots: {chunkGroundSnapshots}/{_farViewChunks.Count} | Cells: {chunkGroundCells}\n" +
                   $"Transition snapshots: {chunkTransitionSnapshots}/{_farViewChunks.Count} | Cells: {chunkTransitionCells}\n" +
                   $"Prop snapshots: {chunkPropSnapshots}/{_farViewChunks.Count} | Cells: {chunkPropCells}\n" +
                   $"Blocker snapshots: {chunkBlockerSnapshots}/{_farViewChunks.Count} | Cells: {chunkBlockerCells}\n" +
                   $"Chunks: {_farViewChunkCompleted}/{_farViewChunkTotal} (submitted {_farViewChunkSubmitted}, pending {pending})\n" +
                   $"Readbacks: {_farViewReadbacks.Count} | Errors: {_farViewChunkErrors}\n" +
                   $"Speed: {speed:0.0} chunks/s | ETA: {eta:0.0}s\n" +
                   $"Elapsed: {elapsed:0.0}s";
        }

        private string BuildBakeDebugOverlayText()
        {
            var sb = new System.Text.StringBuilder(256);
            if (UseWorldStreaming)
            {
                int loaded = 0;
                foreach (var kvp in _streamChunks)
                {
                    if (kvp.Value.Generated)
                        loaded++;
                }

                sb.AppendLine($"Streaming: {(_streamingActive ? "active" : "idle")}");
                sb.AppendLine($"Chunks: {loaded}/{_streamChunks.Count} | Queue: {_streamQueue.Count}");
                if (_streamRequiredChunkCount > 0)
                    sb.AppendLine($"Required (view+prefetch): {_streamRequiredChunkCount}");
                sb.AppendLine($"Speed: {_streamSpeed:0.0} chunks/s");
                if (StreamBakeAllChunks)
                    sb.AppendLine($"AllQueue: {_streamAllQueue.Count}");
                sb.AppendLine($"KeepGenerated: {(StreamKeepGeneratedChunks ? "yes" : "no")}");
                if (StreamBakeAllChunksOnIdle)
                    sb.AppendLine($"Idle bake: {StreamIdleSeconds:0.0}s (queued: {_streamAllQueued})");
            }
            else
            {
                sb.AppendLine("Streaming: off");
            }

            if (UseFarViewBake)
            {
                string status = _farViewBaking ? "baking" : (_farViewHasContent ? "ready" : "idle");
                float elapsed = _farViewBakeStartTime > 0f ? (Time.realtimeSinceStartup - _farViewBakeStartTime) : 0f;
                float speed = (elapsed > 0f) ? (_farViewChunkCompleted / elapsed) : 0f;
                int pending = Mathf.Max(0, _farViewChunkSubmitted - _farViewChunkCompleted);
                sb.AppendLine($"FarView: {status} | Pending: {pending}");
                sb.AppendLine($"FarView speed: {speed:0.0} chunks/s");
                if (_farViewHasBounds)
                    sb.AppendLine($"FarView bounds: {_farViewLastBounds.size.x:0.0} x {_farViewLastBounds.size.y:0.0}");
            }
            else
            {
                sb.AppendLine("FarView: off");
            }

            if (UseWorldStreaming && UseFarViewBake)
                sb.AppendLine("Note: FarView is disabled while streaming.");

            return sb.ToString();
        }

        // [PENV-14]
        // Bake progress HUD and loading overlay management.
        private void EnsureFarViewHud()
        {
            if (_farViewHudRoot == null)
            {
                _farViewHudRoot = new GameObject("FarViewBakeHUD (Auto)");
                _farViewHudRoot.transform.SetParent(transform, false);
                var canvas = _farViewHudRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 20000;

                var overlayGo = new GameObject("Overlay");
                overlayGo.transform.SetParent(_farViewHudRoot.transform, false);
                _farViewHudOverlay = overlayGo.AddComponent<Image>();
                _farViewHudOverlay.raycastTarget = false;
                var overlayRt = _farViewHudOverlay.rectTransform;
                overlayRt.anchorMin = Vector2.zero;
                overlayRt.anchorMax = Vector2.one;
                overlayRt.offsetMin = Vector2.zero;
                overlayRt.offsetMax = Vector2.zero;

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(overlayGo.transform, false);
                _farViewHudOverlayText = titleGo.AddComponent<Text>();
                _farViewHudOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _farViewHudOverlayText.fontSize = 24;
                _farViewHudOverlayText.alignment = TextAnchor.MiddleCenter;
                _farViewHudOverlayText.raycastTarget = false;
                var titleRt = _farViewHudOverlayText.rectTransform;
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.sizeDelta = new Vector2(600f, 60f);
                titleRt.anchoredPosition = new Vector2(0f, 60f);

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(_farViewHudRoot.transform, false);
                _farViewHudText = textGo.AddComponent<Text>();
                _farViewHudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _farViewHudText.fontSize = 14;
                _farViewHudText.alignment = TextAnchor.UpperLeft;
                _farViewHudText.raycastTarget = false;
                var rt = _farViewHudText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(420f, 120f);
            }

            if (_farViewHudOverlay != null)
                _farViewHudOverlay.color = FarViewBakeOverlayColor;
            if (_farViewHudOverlayText != null)
                _farViewHudOverlayText.color = FarViewBakeHUDColor;
            if (_farViewHudText != null)
                _farViewHudText.color = FarViewBakeHUDColor;
            _farViewHudRoot.SetActive(ShowFarViewBakeHUD);
        }

        // [PENV-15]
        // Debug bounds rendering for the active bake/stream area.
        private void EnsureFarViewBoundsRenderer()
        {
            if (_farViewBoundsRenderer != null) return;
            EnsureFarViewObjects();
            if (_farViewRoot == null) return;

            var existing = _farViewRoot.transform.Find("FarViewBakeBounds (Auto)");
            var go = existing != null ? existing.gameObject : new GameObject("FarViewBakeBounds (Auto)");
            go.transform.SetParent(_farViewRoot.transform, false);
            _farViewBoundsRenderer = go.GetComponent<LineRenderer>();
            if (_farViewBoundsRenderer == null)
                _farViewBoundsRenderer = go.AddComponent<LineRenderer>();
            _farViewBoundsRenderer.useWorldSpace = true;
            _farViewBoundsRenderer.loop = false;
            _farViewBoundsRenderer.positionCount = 5;
            _farViewBoundsRenderer.textureMode = LineTextureMode.Stretch;
            _farViewBoundsRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _farViewBoundsRenderer.sortingOrder = FarViewSortingOrder + 5;
            if (!string.IsNullOrEmpty(SortingLayerName))
                _farViewBoundsRenderer.sortingLayerName = SortingLayerName;
        }

        private void UpdateFarViewBoundsRenderer(Bounds bounds)
        {
            _farViewLastBounds = bounds;
            _farViewHasBounds = true;
            if (!ShowFarViewBakeBounds)
            {
                if (_farViewBoundsRenderer != null)
                    _farViewBoundsRenderer.enabled = false;
                return;
            }

            EnsureFarViewBoundsRenderer();
            if (_farViewBoundsRenderer == null) return;

            _farViewBoundsRenderer.startColor = FarViewBakeBoundsColor;
            _farViewBoundsRenderer.endColor = FarViewBakeBoundsColor;
            _farViewBoundsRenderer.startWidth = FarViewBakeBoundsLineWidth;
            _farViewBoundsRenderer.endWidth = FarViewBakeBoundsLineWidth;

            float z = 0f;
            var min = bounds.min;
            var max = bounds.max;
            _farViewBoundsRenderer.SetPosition(0, new Vector3(min.x, min.y, z));
            _farViewBoundsRenderer.SetPosition(1, new Vector3(max.x, min.y, z));
            _farViewBoundsRenderer.SetPosition(2, new Vector3(max.x, max.y, z));
            _farViewBoundsRenderer.SetPosition(3, new Vector3(min.x, max.y, z));
            _farViewBoundsRenderer.SetPosition(4, new Vector3(min.x, min.y, z));
        }

        private void UpdateFarViewBoundsVisibility()
        {
            if (_farViewBoundsRenderer == null) return;
            bool show = ShowFarViewBakeBounds && _farViewHasBounds && (_farViewBaking || _farViewHasContent);
            _farViewBoundsRenderer.enabled = show;
        }

        private void UpdateFarViewHud()
        {
            if (!ShowFarViewBakeHUD)
            {
                if (_farViewHudRoot != null)
                    _farViewHudRoot.SetActive(false);
                return;
            }

            EnsureFarViewHud();
            ApplyFarViewLoadingVisibility();
            bool loading = IsFarViewLoading();
            bool showStats = !FarViewShowHudOnlyWhileBaking || loading;

            if (_farViewHudOverlay != null)
                _farViewHudOverlay.gameObject.SetActive(loading);
            if (_farViewHudOverlayText != null)
            {
                _farViewHudOverlayText.text = FarViewBakeOverlayText;
                _farViewHudOverlayText.gameObject.SetActive(loading);
            }
            if (_farViewHudText != null)
            {
                _farViewHudText.text = BuildFarViewHudText();
                _farViewHudText.gameObject.SetActive(showStats);
                var rt = _farViewHudText.rectTransform;
                if (loading)
                {
                    _farViewHudText.alignment = TextAnchor.MiddleCenter;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(520f, 140f);
                    rt.anchoredPosition = Vector2.zero;
                }
                else
                {
                    _farViewHudText.alignment = TextAnchor.UpperLeft;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(420f, 120f);
                    rt.anchoredPosition = new Vector2(FarViewBakeHUDOffset.x, -FarViewBakeHUDOffset.y);
                }
            }
        }

        private void OnGUI()
        {
            if (ShowFarViewBakeHUD)
            {
                bool allow = !FarViewShowHudOnlyWhileBaking || IsFarViewLoading();
                if (allow && _farViewHudRoot == null)
                {
                    if (_farViewHudStyle == null)
                    {
                        _farViewHudStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 14,
                            normal = { textColor = FarViewBakeHUDColor }
                        };
                    }

                    GUI.Label(
                        new Rect(FarViewBakeHUDOffset.x, FarViewBakeHUDOffset.y, 420, 100),
                        BuildFarViewHudText(),
                        _farViewHudStyle);
                }
            }

            if (ShowBakeDebugOverlay && UnityEngine.Application.isPlaying)
            {
                if (_bakeDebugStyle == null)
                {
                    _bakeDebugStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 13,
                        normal = { textColor = BakeDebugOverlayColor }
                    };
                }

                string debugText = BuildBakeDebugOverlayText();
                if (!string.IsNullOrEmpty(debugText))
                {
                    float width = 520f;
                    float height = 120f;
                    float x = BakeDebugOverlayOffset.x;
                    float y = Screen.height - BakeDebugOverlayOffset.y - height;
                    GUI.Label(new Rect(x, y, width, height), debugText, _bakeDebugStyle);
                }
            }
        }

        private static List<Renderer> DisableNonTilemapRenderers(HashSet<Renderer> keep)
        {
            var disabled = new List<Renderer>();
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                var renderer = all[i];
                if (renderer == null || keep.Contains(renderer) || !renderer.enabled)
                    continue;
                renderer.enabled = false;
                disabled.Add(renderer);
            }

            return disabled;
        }

        private static void RestoreRenderers(List<Renderer> disabled)
        {
            for (int i = 0; i < disabled.Count; i++)
            {
                var renderer = disabled[i];
                if (renderer != null)
                    renderer.enabled = true;
            }
        }

    }
}
