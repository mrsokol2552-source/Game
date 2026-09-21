/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileGpu.cs
@module: presentation.pathfinding.worldgen.farview_tile_gpu
@purpose: Renders far-view tile snapshots through an isolated offscreen tilemap rig so chunk bake can use a GPU path for tile payloads before falling back to CPU sprite raster.
@entry: PENV-31, ProceduralEnvironment.TryComposeChunkPayloadRenderTexture
@api: partial class implementation for ProceduralEnvironment
@deps: far-view tile snapshot source, temporary tilemap rig, far-view composite material
@data: offscreen tilemap rig state, isolated render layer, and tile GPU/fallback bake counters
@perf: medium; tile snapshot population is CPU-side but rendering/composition moves chunk underlays onto GPU
@thread: main thread only
@tests: indirect coverage via Unity recompilation, far-view HUD counters, and repo audits
@config: far-view chunked bake, bake layer, and chunk render settings in ProceduralEnvironment
@assets: hidden far-view composite shader and runtime tile sprites/materials
@notes: keep GPU tile snapshot rendering separate from CPU raster so the fallback path stays isolated and replaceable
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-PROCEDURALENVIRONMENT-FARVIEWTILEGPU]
// Logical block: Scripts/Presentation/Pathfinding/ProceduralEnvironment.FarViewTileGpu.

namespace Game.Presentation.Pathfinding
{
    public partial class ProceduralEnvironment
    {
        private sealed class FarViewTileRasterLayer
        {
            public readonly string Name;
            public readonly int DefaultSortingOrder;
            public GameObject Root;
            public Tilemap Tilemap;
            public TilemapRenderer Renderer;

            public FarViewTileRasterLayer(string name, int defaultSortingOrder)
            {
                Name = name;
                DefaultSortingOrder = defaultSortingOrder;
            }
        }

        private GameObject _farViewTileRasterRoot;
        private Camera _farViewTileRasterCamera;
        private Grid _farViewTileRasterGrid;
        private int _farViewTileRasterLayer = -1;
        private readonly FarViewTileRasterLayer _farViewTileRasterGroundLayer = new FarViewTileRasterLayer("Ground", 0);
        private readonly FarViewTileRasterLayer _farViewTileRasterTransitionLayer = new FarViewTileRasterLayer("Transitions", 16);
        private readonly FarViewTileRasterLayer _farViewTileRasterPropLayer = new FarViewTileRasterLayer("Props", 32);
        private readonly FarViewTileRasterLayer _farViewTileRasterBlockerLayer = new FarViewTileRasterLayer("Blockers", 48);
        private int _farViewTileGpuChunks;
        private int _farViewTileGpuFallbackChunks;

        private string ResolveFarViewTileBackendDebugLabel()
        {
            if (!UseFarViewChunkedBake)
                return "disabled";
            if (_farViewTileRasterLayer >= 0 && _farViewTileRasterCamera != null)
                return "gpu-rig";
            return "cpu-raster";
        }

        private void ReleaseFarViewTileRasterRig()
        {
            if (_farViewTileRasterRoot != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(_farViewTileRasterRoot);
                else
                    DestroyImmediate(_farViewTileRasterRoot);
            }

            _farViewTileRasterRoot = null;
            _farViewTileRasterCamera = null;
            _farViewTileRasterGrid = null;
            _farViewTileRasterLayer = -1;
            ResetFarViewTileRasterLayerState(_farViewTileRasterGroundLayer);
            ResetFarViewTileRasterLayerState(_farViewTileRasterTransitionLayer);
            ResetFarViewTileRasterLayerState(_farViewTileRasterPropLayer);
            ResetFarViewTileRasterLayerState(_farViewTileRasterBlockerLayer);
        }

        private static void ResetFarViewTileRasterLayerState(FarViewTileRasterLayer layer)
        {
            if (layer == null)
                return;

            layer.Root = null;
            layer.Tilemap = null;
            layer.Renderer = null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private bool IsFarViewTileRasterLayerAvailable(int layer)
        {
            if (layer < 0 || layer > 31)
                return false;

            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject == null || !renderer.gameObject.scene.IsValid())
                    continue;
                if (_farViewTileRasterRoot != null && renderer.transform.IsChildOf(_farViewTileRasterRoot.transform))
                    continue;
                if (renderer.gameObject.layer == layer)
                    return false;
            }

            return true;
        }

        private bool TryResolveFarViewTileRasterLayer(out int layer)
        {
            layer = _farViewTileRasterLayer;
            if (IsFarViewTileRasterLayerAvailable(layer))
                return true;

            int reservedBakeLayer = ResolveFarViewBakeLayer();
            for (int candidate = 31; candidate >= 8; candidate--)
            {
                if (candidate == reservedBakeLayer)
                    continue;
                if (!IsFarViewTileRasterLayerAvailable(candidate))
                    continue;

                _farViewTileRasterLayer = candidate;
                layer = candidate;
                if (_farViewTileRasterRoot != null)
                    SetLayerRecursively(_farViewTileRasterRoot, candidate);
                return true;
            }

            _farViewTileRasterLayer = -1;
            layer = -1;
            return false;
        }

        private void EnsureFarViewTileRasterLayerObject(FarViewTileRasterLayer layer)
        {
            if (layer == null || _farViewTileRasterRoot == null)
                return;

            if (layer.Root == null)
            {
                var existing = _farViewTileRasterRoot.transform.Find(layer.Name);
                layer.Root = existing != null ? existing.gameObject : new GameObject(layer.Name);
                layer.Root.transform.SetParent(_farViewTileRasterRoot.transform, false);
                layer.Root.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }

            if (layer.Tilemap == null)
            {
                layer.Tilemap = layer.Root.GetComponent<Tilemap>();
                if (layer.Tilemap == null)
                    layer.Tilemap = layer.Root.AddComponent<Tilemap>();
            }

            if (layer.Renderer == null)
            {
                layer.Renderer = layer.Root.GetComponent<TilemapRenderer>();
                if (layer.Renderer == null)
                    layer.Renderer = layer.Root.AddComponent<TilemapRenderer>();
            }
        }

        private bool EnsureFarViewTileRasterRig()
        {
            if (_farViewTileRasterRoot == null)
            {
                _farViewTileRasterRoot = new GameObject("FarViewTileRasterRig (Auto)");
                _farViewTileRasterRoot.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }

            if (!TryResolveFarViewTileRasterLayer(out var layer))
                return false;

            SetLayerRecursively(_farViewTileRasterRoot, layer);

            if (_farViewTileRasterGrid == null)
            {
                _farViewTileRasterGrid = _farViewTileRasterRoot.GetComponent<Grid>();
                if (_farViewTileRasterGrid == null)
                    _farViewTileRasterGrid = _farViewTileRasterRoot.AddComponent<Grid>();
            }

            if (_farViewTileRasterCamera == null)
            {
                var existing = _farViewTileRasterRoot.transform.Find("FarViewTileRasterCamera (Auto)");
                var cameraGo = existing != null ? existing.gameObject : new GameObject("FarViewTileRasterCamera (Auto)");
                cameraGo.transform.SetParent(_farViewTileRasterRoot.transform, false);
                cameraGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                _farViewTileRasterCamera = cameraGo.GetComponent<Camera>();
                if (_farViewTileRasterCamera == null)
                    _farViewTileRasterCamera = cameraGo.AddComponent<Camera>();
            }

            _farViewTileRasterCamera.enabled = false;
            _farViewTileRasterCamera.orthographic = true;
            _farViewTileRasterCamera.clearFlags = CameraClearFlags.SolidColor;
            _farViewTileRasterCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _farViewTileRasterCamera.nearClipPlane = 0.01f;
            _farViewTileRasterCamera.farClipPlane = 1000f;
            _farViewTileRasterCamera.cullingMask = 1 << layer;

            EnsureFarViewTileRasterLayerObject(_farViewTileRasterGroundLayer);
            EnsureFarViewTileRasterLayerObject(_farViewTileRasterTransitionLayer);
            EnsureFarViewTileRasterLayerObject(_farViewTileRasterPropLayer);
            EnsureFarViewTileRasterLayerObject(_farViewTileRasterBlockerLayer);
            return true;
        }

        private static TilemapChunkSnapshot ResolveFarViewTileRasterReference(FarViewChunkRenderSource renderSource)
        {
            if (renderSource == null)
                return null;
            if (HasFarViewTileSnapshot(renderSource.GroundSnapshot))
                return renderSource.GroundSnapshot;
            if (HasFarViewTileSnapshot(renderSource.TransitionSnapshot))
                return renderSource.TransitionSnapshot;
            if (HasFarViewTileSnapshot(renderSource.PropSnapshot))
                return renderSource.PropSnapshot;
            if (HasFarViewTileSnapshot(renderSource.BlockerSnapshot))
                return renderSource.BlockerSnapshot;
            return null;
        }

        private void ApplyFarViewTileRasterGridConfig(TilemapChunkSnapshot reference)
        {
            if (_farViewTileRasterRoot == null || _farViewTileRasterGrid == null || reference == null || !reference.HasGridConfig)
                return;

            _farViewTileRasterRoot.transform.SetPositionAndRotation(reference.GridPosition, reference.GridRotation);
            _farViewTileRasterRoot.transform.localScale = reference.GridScale;
            _farViewTileRasterGrid.cellLayout = reference.GridCellLayout;
            _farViewTileRasterGrid.cellGap = reference.GridCellGap;
            _farViewTileRasterGrid.cellSize = reference.GridCellSize;
            _farViewTileRasterGrid.cellSwizzle = reference.GridCellSwizzle;
        }

        private void ApplyFarViewTileRasterLayer(FarViewTileRasterLayer targetLayer, TilemapChunkSnapshot snapshot)
        {
            if (targetLayer == null)
                return;

            EnsureFarViewTileRasterLayerObject(targetLayer);
            if (targetLayer.Tilemap == null || targetLayer.Renderer == null || targetLayer.Root == null)
                return;

            targetLayer.Tilemap.ClearAllTiles();
            targetLayer.Renderer.sortingOrder = targetLayer.DefaultSortingOrder;
            if (!string.IsNullOrEmpty(SortingLayerName) && SortingLayerExists(SortingLayerName))
                targetLayer.Renderer.sortingLayerName = SortingLayerName;

            if (snapshot == null || !snapshot.HasTiles || !snapshot.HasTilemapTransform)
                return;

            if (snapshot.HasGridConfig)
            {
                targetLayer.Root.transform.localPosition = snapshot.TilemapLocalPosition;
                targetLayer.Root.transform.localRotation = snapshot.TilemapLocalRotation;
                targetLayer.Root.transform.localScale = snapshot.TilemapLocalScale;
            }
            else
            {
                targetLayer.Root.transform.SetPositionAndRotation(snapshot.TilemapWorldPosition, snapshot.TilemapWorldRotation);
                targetLayer.Root.transform.localScale = snapshot.TilemapWorldScale;
            }

            targetLayer.Tilemap.orientation = snapshot.TilemapOrientation;
            targetLayer.Tilemap.orientationMatrix = snapshot.TilemapOrientationMatrix;
            targetLayer.Tilemap.tileAnchor = snapshot.TilemapAnchor;
            targetLayer.Tilemap.color = snapshot.TilemapColor;

            if (snapshot.HasRendererConfig)
            {
                targetLayer.Renderer.sortingLayerID = snapshot.SortingLayerId;
                targetLayer.Renderer.sortingOrder = snapshot.SortingOrder;
                if (snapshot.SharedMaterial != null)
                    targetLayer.Renderer.sharedMaterial = snapshot.SharedMaterial;
            }

            BoundsInt bounds = snapshot.CellBounds;
            for (int row = bounds.yMin; row < bounds.yMax; row++)
            {
                for (int col = bounds.xMin; col < bounds.xMax; col++)
                {
                    if (!snapshot.TryGetCell(col, row, out var cell) || !cell.HasTile)
                        continue;

                    var cellPosition = new Vector3Int(cell.Col, cell.Row, 0);
                    targetLayer.Tilemap.SetTile(cellPosition, cell.Tile);
                    targetLayer.Tilemap.SetTileFlags(cellPosition, TileFlags.None);
                    targetLayer.Tilemap.SetColor(cellPosition, cell.Tint);
                    targetLayer.Tilemap.SetTransformMatrix(cellPosition, ResolveFarViewTileTransform(cell.Tile, cell.Transform));
                }
            }
        }

        private bool TryBuildChunkTilePayloadRenderTexture(FarViewChunk chunk, int width, int height, out RenderTexture rt)
        {
            rt = null;
            if (chunk == null || !HasFarViewTilePayload(chunk))
                return false;
            if (!EnsureFarViewTileRasterRig())
                return false;

            var renderSource = chunk.RenderSource;
            var reference = ResolveFarViewTileRasterReference(renderSource);
            if (reference == null)
                return false;

            ApplyFarViewTileRasterGridConfig(reference);
            ApplyFarViewTileRasterLayer(_farViewTileRasterGroundLayer, renderSource.GroundSnapshot);
            ApplyFarViewTileRasterLayer(_farViewTileRasterTransitionLayer, renderSource.TransitionSnapshot);
            ApplyFarViewTileRasterLayer(_farViewTileRasterPropLayer, renderSource.PropSnapshot);
            ApplyFarViewTileRasterLayer(_farViewTileRasterBlockerLayer, renderSource.BlockerSnapshot);

            rt = RenderTexture.GetTemporary(
                width,
                height,
                16,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;

            ConfigureFarViewCamera(_farViewTileRasterCamera, chunk.CaptureBounds);
            _farViewTileRasterCamera.targetTexture = rt;
            _farViewTileRasterCamera.Render();
            _farViewTileRasterCamera.targetTexture = null;
            return true;
        }

        private static int ResolveChunkTileUnderlayBackgroundVersion(FarViewChunk chunk)
        {
            var renderSource = chunk != null ? chunk.RenderSource : null;
            if (chunk == null
                || renderSource == null
                || !renderSource.HasBackgroundPayload
                || !renderSource.HasAnyPayloadSources)
            {
                return -1;
            }

            return renderSource.BackgroundPayloadVersion;
        }

        private void EnsureChunkTileUnderlayTexture(FarViewChunk chunk, int width, int height)
        {
            if (chunk == null)
                return;
            if (chunk.TileUnderlayTexture != null
                && chunk.TileUnderlayTexture.width == width
                && chunk.TileUnderlayTexture.height == height)
            {
                return;
            }

            if (chunk.TileUnderlayTexture != null)
            {
                if (UnityEngine.Application.isPlaying)
                    Destroy(chunk.TileUnderlayTexture);
                else
                    DestroyImmediate(chunk.TileUnderlayTexture);
            }

            chunk.TileUnderlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            chunk.TileUnderlayRasterizedTileVersion = 0;
            chunk.TileUnderlayRasterizedBackgroundVersion = int.MinValue;
            chunk.TileUnderlayRasterizedWidth = 0;
            chunk.TileUnderlayRasterizedHeight = 0;
            chunk.TileUnderlayRasterizedCaptureBounds = default;
        }

        private bool CanReuseChunkTileUnderlayTexture(FarViewChunk chunk, int width, int height)
        {
            var renderSource = chunk != null ? chunk.RenderSource : null;
            return chunk != null
                && renderSource != null
                && chunk.TileUnderlayTexture != null
                && renderSource.TileSnapshotVersion != 0
                && renderSource.HasTileSnapshots
                && chunk.TileUnderlayRasterizedTileVersion == renderSource.TileSnapshotVersion
                && chunk.TileUnderlayRasterizedBackgroundVersion == ResolveChunkTileUnderlayBackgroundVersion(chunk)
                && chunk.TileUnderlayRasterizedWidth == width
                && chunk.TileUnderlayRasterizedHeight == height
                && AreFarViewBoundsApproximatelyEqual(chunk.TileUnderlayRasterizedCaptureBounds, chunk.CaptureBounds);
        }

        private bool TryBuildChunkTileUnderlayTexture(FarViewChunk chunk, int width, int height)
        {
            if (chunk == null || !HasFarViewTilePayload(chunk))
                return false;

            if (CanReuseChunkTileUnderlayTexture(chunk, width, height))
            {
                _farViewTileUnderlayCacheHits++;
                return true;
            }

            _farViewTileUnderlayCacheMisses++;
            if (!TryBuildChunkTilePayloadRenderTexture(chunk, width, height, out var tileRt))
                return false;

            RenderTexture underlayRt = tileRt;
            if (ShouldRasterizeFarViewBackground(chunk)
                && TryBuildChunkBackgroundPayloadTexture(chunk, width, height)
                && chunk.BackgroundTexture != null)
            {
                if (TryComposeFarViewTextures(tileRt, chunk.BackgroundTexture, width, height, out var composedUnderlay))
                {
                    RenderTexture.ReleaseTemporary(tileRt);
                    underlayRt = composedUnderlay;
                }
                else
                {
                    RenderTexture.ReleaseTemporary(tileRt);
                    return false;
                }
            }

            EnsureChunkTileUnderlayTexture(chunk, width, height);
            if (chunk.TileUnderlayTexture == null)
            {
                RenderTexture.ReleaseTemporary(underlayRt);
                return false;
            }

            var prev = RenderTexture.active;
            RenderTexture.active = underlayRt;
            chunk.TileUnderlayTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            chunk.TileUnderlayTexture.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(underlayRt);

            var renderSource = chunk.RenderSource;
            chunk.TileUnderlayRasterizedTileVersion = renderSource != null ? renderSource.TileSnapshotVersion : 0;
            chunk.TileUnderlayRasterizedBackgroundVersion = ResolveChunkTileUnderlayBackgroundVersion(chunk);
            chunk.TileUnderlayRasterizedWidth = width;
            chunk.TileUnderlayRasterizedHeight = height;
            chunk.TileUnderlayRasterizedCaptureBounds = chunk.CaptureBounds;
            return true;
        }

        private bool TryComposeFarViewTextures(Texture mainTexture, Texture underlayTexture, int width, int height, out RenderTexture composedRt)
        {
            composedRt = null;
            if (mainTexture == null || underlayTexture == null)
                return false;
            if (!TryGetFarViewBackgroundCompositeMaterial(out var material) || material == null)
                return false;

            composedRt = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            composedRt.filterMode = FilterMode.Point;
            composedRt.wrapMode = TextureWrapMode.Clamp;
            material.SetTexture(FarViewBackgroundTextureId, underlayTexture);
            Graphics.Blit(mainTexture, composedRt, material);
            return true;
        }

        private bool TryComposeChunkPayloadRenderTexture(FarViewChunk chunk, ref RenderTexture rt, int width, int height)
        {
            if (rt == null || chunk == null)
                return false;

            bool hasTilePayload = HasFarViewTilePayload(chunk);
            if (hasTilePayload)
            {
                if (TryBuildChunkTileUnderlayTexture(chunk, width, height)
                    && chunk.TileUnderlayTexture != null)
                {
                    if (TryComposeFarViewTextures(rt, chunk.TileUnderlayTexture, width, height, out var composedRt))
                    {
                        RenderTexture.ReleaseTemporary(rt);
                        rt = composedRt;
                        _farViewTileGpuChunks++;
                        return true;
                    }
                }

                _farViewTileGpuFallbackChunks++;
                return false;
            }

            return TryComposeChunkBackgroundPayloadRenderTexture(chunk, ref rt, width, height);
        }

        private bool TryUpdateChunkFromPayloadSourcesGpu(FarViewChunk chunk, int width, int height, float ppu)
        {
            if (chunk == null || !HasFarViewTilePayload(chunk))
                return false;

            if (!TryBuildChunkTileUnderlayTexture(chunk, width, height) || chunk.TileUnderlayTexture == null)
            {
                _farViewTileGpuFallbackChunks++;
                return false;
            }

            EnsureChunkTexture(chunk, width, height);
            if (chunk.Texture == null)
            {
                _farViewTileGpuFallbackChunks++;
                return false;
            }

            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0)
            {
                Graphics.CopyTexture(chunk.TileUnderlayTexture, chunk.Texture);
            }
            else
            {
                chunk.Texture.SetPixels32(chunk.TileUnderlayTexture.GetPixels32());
                chunk.Texture.Apply(false, false);
            }

            EnsureChunkSprite(chunk, width, height, ppu);
            _farViewTileGpuChunks++;
            return chunk.Texture != null;
        }
    }
}
