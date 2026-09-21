/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewChunks.cs
@module: presentation.pathfinding.worldgen.farview_chunks
@purpose: Hosts far-view chunk bake, chunk sprite/texture lifecycle, GPU readback handling, and bake progress counters.
@entry: PENV-13, ProceduralEnvironment.BakeFarViewChunksSync, ProceduralEnvironment.ProcessFarViewReadbacks
@api: partial class implementation for ProceduralEnvironment
@deps: far-view bake source, far-view payload sources, Unity RenderTexture/AsyncGPUReadback
@data: FarViewChunk instances, chunk textures/sprites, readback queue, far-view bake counters
@perf: hot path during far-view chunk bake; keep allocation and GPU readback handling localized
@thread: main thread orchestration with Unity async GPU readback polling
@tests: indirect coverage via Unity recompilation, repo audits, and manual far-view bake verification
@config: FarViewChunkPixels, FarViewPixelsPerUnit, FarViewDirectChunkRender, readback timeout/budget settings
@assets: far-view chunk SpriteRenderer objects and generated Texture2D/Sprite assets
@notes: chunk bake logic remains separate from shader/backend migration so payload extraction can be verified independently
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWCHUNKS]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewChunks.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private void BakeFarViewChunksSync(FarViewBakeSource source, int bakeLayer)
        {
            var bounds = source.Bounds;
            var renderers = source.Renderers;
            var keep = new HashSet<Renderer>(renderers);
            if (_farViewRenderer != null)
                keep.Add(_farViewRenderer);
            float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
            int maxPixels = Mathf.Max(256, Mathf.Min(FarViewChunkPixels, FarViewMaxTextureSize));
            float chunkWorld = Mathf.Max(1f, maxPixels / ppu);
            int chunksX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / chunkWorld));
            int chunksY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / chunkWorld));
            BeginFarViewBakeStats(chunksX * chunksY);

            EnsureFarViewChunkList(chunksX * chunksY);
            SetFarViewChunkVisibility(false);
            _farViewHasContent = false;

            int idx = 0;
            bool any = false;
            List<Renderer> disabled = null;
            if (bakeLayer < 0)
                disabled = DisableNonTilemapRenderers(keep);
            bool hasLiveRenderers = renderers != null && renderers.Count > 0;
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
                        continue;
                    }
                    if (!hasLiveRenderers)
                    {
                        ClearChunkSprite(chunk);
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                        UpdateChunkTransform(chunk, chunkBounds);
                        continue;
                    }
                    if (UseFarViewDirectChunkRender)
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

                        bool payloadSourcesComposited = TryComposeChunkPayloadRenderTexture(chunk, ref rt, texW, texH);
                        UpdateChunkSprite(chunk, rt, texW, texH, ppu, payloadSourcesComposited);
                        RenderTexture.ReleaseTemporary(rt);
                        any = true;
                        MarkFarViewChunkSubmitted();
                        MarkFarViewChunkCompleted();
                    }
                    UpdateChunkTransform(chunk, chunkBounds);
                }
            }
            if (disabled != null)
                RestoreRenderers(disabled);
            _farViewHasContent = any;
        }

        private void EnsureFarViewChunkList(int count)
        {
            for (int i = _farViewChunks.Count; i < count; i++)
            {
                var go = new GameObject($"FarViewChunk {i}");
                go.transform.SetParent(_farViewRoot.transform, false);
                _farViewChunks.Add(new FarViewChunk
                {
                    Root = go,
                    Renderer = null
                });
            }
            for (int i = 0; i < _farViewChunks.Count; i++)
                _farViewChunks[i].Active = i < count;

            for (int i = 0; i < count; i++)
                EnsureChunkRenderer(_farViewChunks[i]);
        }

        private void EnsureChunkRenderer(FarViewChunk chunk)
        {
            if (chunk.Renderer == null)
            {
                chunk.Renderer = chunk.Root.GetComponent<SpriteRenderer>();
                if (chunk.Renderer == null)
                    chunk.Renderer = chunk.Root.AddComponent<SpriteRenderer>();
            }
            chunk.Renderer.enabled = false;
            chunk.Renderer.sortingOrder = FarViewSortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                chunk.Renderer.sortingLayerName = SortingLayerName;
        }

        private void EnsureChunkTexture(FarViewChunk chunk, int width, int height)
        {
            if (chunk.Texture != null && chunk.Texture.width == width && chunk.Texture.height == height)
                return;
            if (chunk.Texture != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(chunk.Texture);
                else
                    DestroyImmediate(chunk.Texture);
            }
            chunk.Texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void EnsureChunkSprite(FarViewChunk chunk, int width, int height, float ppu)
        {
            if (chunk.Sprite == null || chunk.Sprite.texture != chunk.Texture)
            {
                if (chunk.Sprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Sprite);
                    else
                        DestroyImmediate(chunk.Sprite);
                }
                chunk.Sprite = Sprite.Create(chunk.Texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
            }
            chunk.Renderer.sprite = chunk.Sprite;
        }

        private void ClearChunkSprite(FarViewChunk chunk)
        {
            if (chunk == null || chunk.Renderer == null)
                return;

            chunk.Renderer.sprite = null;
        }

        private void UpdateChunkDirectRender(FarViewChunk chunk, RenderTexture rt, int width, int height, float ppu, bool payloadSourcesComposited)
        {
            if (!chunk.Active || chunk.Renderer == null) return;
            EnsureChunkTexture(chunk, width, height);
            bool needsBackgroundPayload = ShouldRasterizeFarViewBackground(chunk);
            if ((payloadSourcesComposited || !needsBackgroundPayload) && (SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0)
            {
                Graphics.CopyTexture(rt, chunk.Texture);
            }
            else
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                chunk.Texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                chunk.Texture.Apply();
                RenderTexture.active = prev;
            }
            if (!payloadSourcesComposited)
                CompositeChunkBackgroundPayload(chunk, width, height);
            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkSprite(FarViewChunk chunk, RenderTexture rt, int width, int height, float ppu, bool payloadSourcesComposited)
        {
            if (!chunk.Active || chunk.Renderer == null) return;
            EnsureChunkTexture(chunk, width, height);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            chunk.Texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            chunk.Texture.Apply();
            RenderTexture.active = prev;

            if (!payloadSourcesComposited)
                CompositeChunkBackgroundPayload(chunk, width, height);
            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkSpriteFromReadback(FarViewChunk chunk, AsyncGPUReadbackRequest request, int width, int height, float ppu, bool payloadSourcesComposited)
        {
            if (!chunk.Active || chunk.Renderer == null || request.hasError) return;
            EnsureChunkTexture(chunk, width, height);
            var data = request.GetData<byte>();
            if (data.Length != width * height * 4)
                return;
            chunk.Texture.LoadRawTextureData(data);
            chunk.Texture.Apply();
            if (!payloadSourcesComposited)
                CompositeChunkBackgroundPayload(chunk, width, height);
            EnsureChunkSprite(chunk, width, height, ppu);
        }

        private void UpdateChunkTransform(FarViewChunk chunk, Bounds bounds)
        {
            if (chunk.Root == null) return;
            chunk.Root.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            chunk.Root.transform.localScale = Vector3.one;
        }

        private void BeginFarViewBakeStats(int totalChunks)
        {
            _farViewChunkTotal = Mathf.Max(0, totalChunks);
            _farViewChunkSubmitted = 0;
            _farViewChunkCompleted = 0;
            _farViewChunkErrors = 0;
            _farViewPayloadOnlyChunks = 0;
            _farViewBackgroundSliceCacheHits = 0;
            _farViewBackgroundSliceCacheMisses = 0;
            _farViewTileSliceCacheHits = 0;
            _farViewTileSliceCacheMisses = 0;
            _farViewBackgroundChunkCacheHits = 0;
            _farViewBackgroundChunkCacheMisses = 0;
            _farViewTileUnderlayCacheHits = 0;
            _farViewTileUnderlayCacheMisses = 0;
            _farViewTileGpuChunks = 0;
            _farViewTileGpuFallbackChunks = 0;
            _farViewBakeStartTime = Time.realtimeSinceStartup;
        }

        private void MarkFarViewChunkSubmitted()
        {
            _farViewChunkSubmitted++;
        }

        private void MarkFarViewChunkCompleted()
        {
            _farViewChunkCompleted++;
        }

        private void MarkFarViewChunkError()
        {
            _farViewChunkErrors++;
            _farViewChunkCompleted++;
        }

        private void ClearFarViewReadbacks()
        {
            for (int i = 0; i < _farViewReadbacks.Count; i++)
            {
                var entry = _farViewReadbacks[i];
                if (entry.RenderTexture != null)
                    RenderTexture.ReleaseTemporary(entry.RenderTexture);
            }
            _farViewReadbacks.Clear();
            _farViewBakeVersion++;
        }

        private void ProcessFarViewReadbacks(ref bool any)
        {
            if (_farViewReadbacks.Count == 0) return;
            int budget = Mathf.Max(1, FarViewReadbacksPerFrame);
            float now = Time.realtimeSinceStartup;
            for (int i = _farViewReadbacks.Count - 1; i >= 0 && budget > 0; i--)
            {
                var entry = _farViewReadbacks[i];
                bool timedOut = FarViewFallbackToSyncReadback
                    && FarViewReadbackTimeout > 0f
                    && (now - entry.SubmittedTime) >= FarViewReadbackTimeout;
                if (!entry.Request.done && !timedOut) continue;
                budget--;
                bool validBake = entry.BakeVersion == _farViewBakeVersion;
                bool requestError = entry.Request.hasError;
                bool useSync = timedOut || requestError || !entry.Request.done;
                if (validBake)
                {
                    if (useSync)
                        UpdateChunkSprite(entry.Chunk, entry.RenderTexture, entry.Width, entry.Height, entry.Ppu, entry.PayloadSourcesComposited);
                    else
                        UpdateChunkSpriteFromReadback(entry.Chunk, entry.Request, entry.Width, entry.Height, entry.Ppu, entry.PayloadSourcesComposited);
                    UpdateChunkTransform(entry.Chunk, entry.Bounds);
                    any = true;
                }
                if (!validBake || requestError || timedOut)
                    MarkFarViewChunkError();
                else
                    MarkFarViewChunkCompleted();
                if (entry.RenderTexture != null)
                    RenderTexture.ReleaseTemporary(entry.RenderTexture);
                _farViewReadbacks.RemoveAt(i);
            }
        }

        private void SetFarViewChunkVisibility(bool visible)
        {
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk.Renderer == null) continue;
                chunk.Renderer.enabled = visible && chunk.Active && chunk.Renderer.sprite != null;
            }
        }

        private void ClearFarViewChunks()
        {
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk.Renderer != null)
                    chunk.Renderer.sprite = null;
                if (chunk.Sprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Sprite);
                    else
                        DestroyImmediate(chunk.Sprite);
                }
                if (chunk.Texture != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Texture);
                    else
                        DestroyImmediate(chunk.Texture);
                }
                if (chunk.BackgroundTexture != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.BackgroundTexture);
                    else
                        DestroyImmediate(chunk.BackgroundTexture);
                }
                if (chunk.TileUnderlayTexture != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.TileUnderlayTexture);
                    else
                        DestroyImmediate(chunk.TileUnderlayTexture);
                }
                if (chunk.Root != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(chunk.Root);
                    else
                        DestroyImmediate(chunk.Root);
                }
            }
            _farViewChunks.Clear();
        }
    }
}
