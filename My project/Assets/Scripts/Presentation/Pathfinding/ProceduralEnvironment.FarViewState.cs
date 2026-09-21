/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewState.cs
@module: presentation.pathfinding.worldgen.farview_state
@purpose: Hosts far-view texture lifecycle, capture bounds/camera helpers, renderer collection, and baked/base-map visibility state.
@entry: PENV-12, ProceduralEnvironment.ReleaseFarViewTexture, ProceduralEnvironment.ShouldUseFarView, ProceduralEnvironment.ApplyFarViewState
@api: partial class implementation for ProceduralEnvironment
@deps: Unity Camera/Renderer/Tilemap, CameraZoom2D, far-view chunk visibility
@data: far-view render texture/sprite state, capture bounds, renderer visibility flags, loading visibility state
@perf: medium; called during far-view bake and camera zoom state updates
@thread: main thread only
@tests: indirect coverage via Unity recompilation, repo audits, and manual far-view bake verification
@config: far-view threshold, hysteresis, sorting, included tilemap layers, loading visibility settings
@assets: generated far-view RenderTexture, Texture2D, SpriteRenderer sprite
@notes: keep state toggles separate from chunk bake internals so render/backend migration can target the bake files directly
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWSTATE]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewState.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        internal readonly struct FarViewDiagnostics
        {
            public readonly bool Enabled;
            public readonly bool Active;
            public readonly bool HasContent;
            public readonly bool Baking;
            public readonly bool Dirty;
            public readonly bool HasBakeRoutine;
            public readonly bool MainRendererEnabled;
            public readonly int ChunkCount;
            public readonly int ActiveChunkCount;
            public readonly int VisibleChunkCount;
            public readonly int SpriteChunkCount;
            public readonly int ExpectedContentChunkCount;
            public readonly int ReadyContentChunkCount;

            public FarViewDiagnostics(
                bool enabled,
                bool active,
                bool hasContent,
                bool baking,
                bool dirty,
                bool hasBakeRoutine,
                bool mainRendererEnabled,
                int chunkCount,
                int activeChunkCount,
                int visibleChunkCount,
                int spriteChunkCount,
                int expectedContentChunkCount,
                int readyContentChunkCount)
            {
                Enabled = enabled;
                Active = active;
                HasContent = hasContent;
                Baking = baking;
                Dirty = dirty;
                HasBakeRoutine = hasBakeRoutine;
                MainRendererEnabled = mainRendererEnabled;
                ChunkCount = chunkCount;
                ActiveChunkCount = activeChunkCount;
                VisibleChunkCount = visibleChunkCount;
                SpriteChunkCount = spriteChunkCount;
                ExpectedContentChunkCount = expectedContentChunkCount;
                ReadyContentChunkCount = readyContentChunkCount;
            }
        }

        internal FarViewDiagnostics GetFarViewDiagnostics()
        {
            int activeChunks = 0;
            int visibleChunks = 0;
            int spriteChunks = 0;
            int expectedContentChunks = 0;
            int readyContentChunks = 0;
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (chunk == null)
                    continue;
                if (chunk.Active)
                    activeChunks++;
                if (chunk.Renderer != null && chunk.Renderer.enabled)
                    visibleChunks++;
                if (chunk.Renderer != null && chunk.Renderer.sprite != null)
                    spriteChunks++;
                if (FarViewChunkExpectsContent(chunk))
                {
                    expectedContentChunks++;
                    if (FarViewChunkHasSprite(chunk))
                        readyContentChunks++;
                }
            }

            return new FarViewDiagnostics(
                UseFarViewBake,
                _farViewActive,
                _farViewHasContent,
                _farViewBaking,
                _farViewDirty,
                _farViewBakeRoutine != null,
                _farViewRenderer != null && _farViewRenderer.enabled,
                _farViewChunks.Count,
                activeChunks,
                visibleChunks,
                spriteChunks,
                expectedContentChunks,
                readyContentChunks);
        }

        private void EnsureFarViewTexture(int width, int height)
        {
            if (_farViewTexture != null && _farViewTexture.width == width && _farViewTexture.height == height)
                return;
            ReleaseFarViewTexture();
            _farViewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _farViewTexture.Create();
        }

        private void ReleaseFarViewTexture()
        {
            ClearFarViewReadbacks();
            ReleaseFarViewTileRasterRig();
            if (_farViewTexture != null)
            {
                _farViewTexture.Release();
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewTexture);
                else
                    DestroyImmediate(_farViewTexture);
                _farViewTexture = null;
            }
            if (_farViewSprite != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewSprite);
                else
                    DestroyImmediate(_farViewSprite);
                _farViewSprite = null;
            }
            if (_farViewTexture2D != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewTexture2D);
                else
                    DestroyImmediate(_farViewTexture2D);
                _farViewTexture2D = null;
            }
            if (_farViewRenderer != null)
                _farViewRenderer.sprite = null;
            ClearFarViewChunks();
            _farViewHasContent = false;
            _farViewBaking = false;
        }

        private List<Renderer> CollectFarViewRenderers()
        {
            var list = new List<Renderer>(6);
            if (FarViewIncludeGround) AddTilemapRenderer(_ground, list);
            if (FarViewIncludeProps) AddTilemapRenderer(_props, list);
            if (FarViewIncludeBlockers) AddTilemapRenderer(_blockers, list);
            if (FarViewIncludeTransitions) AddTilemapRenderer(_transitions, list);
            if (FarViewIncludeBackground && UseBackgroundTilemap) AddTilemapRenderer(_background, list);
            return list;
        }

        private static void AddTilemapRenderer(Tilemap map, List<Renderer> list)
        {
            if (map == null) return;
            var renderer = map.GetComponent<Renderer>();
            if (renderer != null)
                list.Add(renderer);
        }

        private bool TryGetFarViewBounds(List<Renderer> renderers, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasAny = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var b = r.bounds;
                if (b.size.x <= 0f || b.size.y <= 0f) continue;
                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }
            return hasAny;
        }

        private void ConfigureFarViewCamera(Bounds bounds)
        {
            ConfigureFarViewCamera(_farViewCamera, bounds);
        }

        private void ConfigureFarViewCamera(Camera targetCamera, Bounds bounds)
        {
            if (targetCamera == null) return;
            float sizeY = Mathf.Max(0.01f, bounds.size.y);
            float sizeX = Mathf.Max(0.01f, bounds.size.x);
            targetCamera.orthographicSize = sizeY * 0.5f;
            targetCamera.aspect = sizeX / sizeY;
            float z = -10f;
            var main = Camera.main;
            if (main != null)
                z = main.transform.position.z;
            targetCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, z);
        }

        private void UpdateFarViewQuad(Bounds bounds)
        {
            if (_farViewRoot == null) return;
            _farViewRoot.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            if (_farViewSprite != null)
            {
                var size = _farViewSprite.bounds.size;
                if (size.x > 0f && size.y > 0f)
                {
                    _farViewRoot.transform.localScale = new Vector3(
                        bounds.size.x / size.x,
                        bounds.size.y / size.y,
                        1f);
                }
                else
                {
                    _farViewRoot.transform.localScale = Vector3.one;
                }
            }
            else
            {
                _farViewRoot.transform.localScale = Vector3.one;
            }
        }

        private void UpdateFarViewSprite(int width, int height)
        {
            if (_farViewTexture == null || _farViewRenderer == null) return;
            if (_farViewTexture2D == null || _farViewTexture2D.width != width || _farViewTexture2D.height != height)
            {
                if (_farViewTexture2D != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(_farViewTexture2D);
                    else
                        DestroyImmediate(_farViewTexture2D);
                }
                _farViewTexture2D = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            var prev = RenderTexture.active;
            RenderTexture.active = _farViewTexture;
            _farViewTexture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            _farViewTexture2D.Apply();
            RenderTexture.active = prev;

            if (_farViewSprite == null || _farViewSprite.texture != _farViewTexture2D)
            {
                if (_farViewSprite != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Destroy(_farViewSprite);
                    else
                        DestroyImmediate(_farViewSprite);
                }
                float ppu = Mathf.Max(1f, FarViewPixelsPerUnit);
                _farViewSprite = Sprite.Create(_farViewTexture2D, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
            }
            _farViewRenderer.sprite = _farViewSprite;
            _farViewHasContent = _farViewSprite != null;
        }

        private int ResolveFarViewBakeLayer()
        {
            if (string.IsNullOrEmpty(FarViewBakeLayerName)) return -1;
            int layer = LayerMask.NameToLayer(FarViewBakeLayerName);
            return layer >= 0 ? layer : -1;
        }

        private bool ShouldUseFarView(Camera cam)
        {
            if (cam == null || !cam.orthographic) return false;
            if (FarViewAlwaysActive) return true;
            float threshold = FarViewOrthoThreshold;
            if (UseFarViewThresholdFromCameraZoom)
            {
                var zoom = cam.GetComponent<Game.Presentation.CameraControl.CameraZoom2D>();
                if (zoom != null && zoom.MaxOrthoSize > 0f)
                    threshold = zoom.MaxOrthoSize * Mathf.Clamp(FarViewOrthoThresholdPercent, 0.1f, 1f);
            }
            float hysteresis = Mathf.Max(0f, FarViewOrthoHysteresis);
            if (_farViewActive)
                return cam.orthographicSize >= (threshold - hysteresis);
            return cam.orthographicSize >= threshold;
        }

        private void ApplyFarViewState(bool useBaked)
        {
            bool hasChunks = UseFarViewChunkedBake && HasCompleteFarViewChunkSprites();
            _farViewActive = useBaked && _farViewHasContent && !_farViewBaking && (!UseFarViewChunkedBake || hasChunks);
            if (_farViewRenderer != null && !UseFarViewChunkedBake)
                _farViewRenderer.enabled = _farViewActive;
            if (UseFarViewChunkedBake)
                SetFarViewChunkVisibility(_farViewActive);

            if (_farViewActive)
            {
                SetTilemapRendererEnabled(_ground, false);
                SetTilemapRendererEnabled(_props, false);
                SetTilemapRendererEnabled(_blockers, false);
                SetTilemapRendererEnabled(_transitions, false);
                SetTilemapRendererEnabled(_background, false);
            }
            else
            {
                ApplyDebugOutlineVisibility();
            }
        }

        private static bool FarViewChunkHasSprite(FarViewChunk chunk)
        {
            return chunk != null
                && chunk.Renderer != null
                && chunk.Renderer.sprite != null;
        }

        private static bool FarViewChunkExpectsContent(FarViewChunk chunk)
        {
            if (chunk == null || !chunk.Active)
                return false;

            var source = chunk.RenderSource;
            if (source != null && source.HasAnyPayloadSources)
                return true;

            return FarViewChunkHasSprite(chunk);
        }

        private bool HasCompleteFarViewChunkSprites()
        {
            if (_farViewChunks.Count == 0) return false;
            bool hasReadyContent = false;
            for (int i = 0; i < _farViewChunks.Count; i++)
            {
                var chunk = _farViewChunks[i];
                if (!FarViewChunkExpectsContent(chunk))
                    continue;
                if (!FarViewChunkHasSprite(chunk))
                    return false;
                hasReadyContent = true;
            }
            return hasReadyContent;
        }

        private bool IsFarViewLoading()
        {
            if (!UseFarViewBake || !FarViewHideMapWhileBaking) return false;
            // Only treat active far-view bake as "loading" to avoid hiding the map on a stalled coroutine.
            return _farViewBaking;
        }

        private void ApplyFarViewLoadingVisibility()
        {
            bool loading = IsFarViewLoading();
            bool hideMap = FarViewHideMapWhileBaking && _farViewBaking;
            if (loading)
            {
                if (hideMap)
                {
                    _farViewLoadingApplied = true;
                    if (_farViewRenderer != null && !UseFarViewChunkedBake)
                        _farViewRenderer.enabled = false;
                    if (UseFarViewChunkedBake)
                        SetFarViewChunkVisibility(false);

                    SetTilemapRendererEnabled(_ground, false);
                    SetTilemapRendererEnabled(_props, false);
                    SetTilemapRendererEnabled(_blockers, false);
                    SetTilemapRendererEnabled(_transitions, false);
                    SetTilemapRendererEnabled(_background, false);
                }
                return;
            }

            if (_farViewLoadingApplied)
            {
                _farViewLoadingApplied = false;
                var cam = Camera.main;
                if (cam != null && cam.orthographic)
                    ApplyFarViewState(ShouldUseFarView(cam));
                else
                    ApplyFarViewState(false);
                if (!_farViewActive)
                    ForceShowBaseMap();
            }
        }

        private void ForceShowBaseMap()
        {
            SetTilemapRendererEnabled(_ground, true);
            SetTilemapRendererEnabled(_props, true);
            SetTilemapRendererEnabled(_blockers, true);
            SetTilemapRendererEnabled(_transitions, true);
            SetTilemapRendererEnabled(_background, UseBackgroundTilemap);
        }
    }
}
